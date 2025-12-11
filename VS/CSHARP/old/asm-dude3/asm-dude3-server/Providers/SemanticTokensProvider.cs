using AsmTools;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides semantic tokens for assembly language syntax highlighting
/// Matches AsmDude2's tokenization using AsmDude2Tools.Get_Token_Type_Intel()
/// </summary>
public class SemanticTokensProvider
{
    private readonly ILogger _logger;
    private readonly AsmDude2Tools _asmDudeTools;

    // DEPRECATED: Replaced with AsmSourceTools.ParseMnemonic()
    // Common x86/x64 mnemonics
    private static readonly HashSet<string> Mnemonics_DEPRECATED = new(StringComparer.OrdinalIgnoreCase)
    {
        // Data movement
        "mov", "movb", "movw", "movl", "movq", "movzx", "movsx", "lea", "push", "pop",

        // Arithmetic
        "add", "sub", "mul", "imul", "div", "idiv", "inc", "dec", "neg",
        "adc", "sbb",

        // Logical
        "and", "or", "xor", "not", "test", "cmp",

        // Shift/Rotate
        "shl", "shr", "sal", "sar", "rol", "ror", "rcl", "rcr",

        // Control flow
        "jmp", "je", "jz", "jne", "jnz", "jg", "jge", "jl", "jle",
        "ja", "jae", "jb", "jbe", "jo", "jno", "js", "jns",
        "call", "ret", "retn", "retf", "loop", "loope", "loopne",

        // Stack
        "enter", "leave",

        // String operations
        "movs", "movsb", "movsw", "movsd", "movsq",
        "cmps", "cmpsb", "cmpsw", "cmpsd", "cmpsq",
        "scas", "scasb", "scasw", "scasd", "scasq",
        "lods", "lodsb", "lodsw", "lodsd", "lodsq",
        "stos", "stosb", "stosw", "stosd", "stosq",
        "rep", "repe", "repz", "repne", "repnz",

        // Bit manipulation
        "bt", "bts", "btr", "btc", "bsf", "bsr",

        // Conditional move
        "cmove", "cmovz", "cmovne", "cmovnz", "cmovg", "cmovge",
        "cmovl", "cmovle", "cmova", "cmovae", "cmovb", "cmovbe",

        // System
        "nop", "hlt", "int", "syscall", "sysenter", "sysexit",

        // Misc
        "xchg", "cwd", "cdq", "cqo", "cbw", "cwde", "cdqe",
        "setne", "sete", "setg", "setl", "seta", "setb"
    };

    // x86/x64 registers
    private static readonly HashSet<string> Registers = new(StringComparer.OrdinalIgnoreCase)
    {
        // 64-bit general purpose
        "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rsp", "rbp",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",

        // 32-bit general purpose
        "eax", "ebx", "ecx", "edx", "esi", "edi", "esp", "ebp",
        "r8d", "r9d", "r10d", "r11d", "r12d", "r13d", "r14d", "r15d",

        // 16-bit general purpose
        "ax", "bx", "cx", "dx", "si", "di", "sp", "bp",
        "r8w", "r9w", "r10w", "r11w", "r12w", "r13w", "r14w", "r15w",

        // 8-bit general purpose
        "al", "ah", "bl", "bh", "cl", "ch", "dl", "dh",
        "sil", "dil", "spl", "bpl",
        "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b",

        // Segment registers
        "cs", "ds", "es", "fs", "gs", "ss",

        // Instruction pointer
        "rip", "eip", "ip",

        // Flags
        "rflags", "eflags", "flags"
    };

    // Operators in assembly
    private static readonly HashSet<char> Operators = new()
    {
        ',', ':', '[', ']', '+', '-', '*', '(', ')'
    };

    // Regular expressions for numbers
    private static readonly Regex HexNumberRegex = new(@"\b0[xX][0-9a-fA-F]+\b", RegexOptions.Compiled);
    private static readonly Regex BinaryNumberRegex = new(@"\b0[bB][01]+\b", RegexOptions.Compiled);
    private static readonly Regex DecimalNumberRegex = new(@"\b-?\d+\b", RegexOptions.Compiled);

    public SemanticTokensProvider(ILogger logger, AsmDude2Tools asmDudeTools)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _asmDudeTools = asmDudeTools ?? throw new ArgumentNullException(nameof(asmDudeTools));
    }

    /// <summary>
    /// Provides semantic tokens for the given lines of assembly code
    /// </summary>
    public List<SemanticToken> ProvideSemanticTokens(string[] lines)
    {
        if (lines == null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        var tokens = new List<SemanticToken>();

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Check for comment - everything after ';' is a comment
            var commentIndex = line.IndexOf(';');
            if (commentIndex >= 0)
            {
                // Add comment token
                tokens.Add(new SemanticToken
                {
                    Line = lineIndex,
                    StartChar = commentIndex,
                    Length = line.Length - commentIndex,
                    TokenType = SemanticTokenType.Comment
                });

                // Only process text before comment
                line = line.Substring(0, commentIndex);
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Tokenize the line
            tokens.AddRange(TokenizeLine(line, lineIndex));
        }

        // Sort tokens by line, then by position
        tokens.Sort((a, b) =>
        {
            var lineCompare = a.Line.CompareTo(b.Line);
            return lineCompare != 0 ? lineCompare : a.StartChar.CompareTo(b.StartChar);
        });

        return tokens;
    }

    private List<SemanticToken> TokenizeLine(string line, int lineIndex)
    {
        var tokens = new List<SemanticToken>();
        var originalLine = line;
        var offset = 0; // Track offset for position adjustments

        // Check for label (identifier followed by colon)
        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            var labelText = line.Substring(0, colonIndex).Trim();
            if (!string.IsNullOrEmpty(labelText) && IsValidIdentifier(labelText))
            {
                var labelStart = line.IndexOf(labelText);
                tokens.Add(new SemanticToken
                {
                    Line = lineIndex,
                    StartChar = labelStart,
                    Length = labelText.Length,
                    TokenType = SemanticTokenType.Function  // Labels map to 'function' in standard LSP
                });

                // Process rest of line after colon
                offset = colonIndex + 1;
                line = line.Substring(offset);
            }
        }

        // Tokenize remaining content
        var pos = 0;
        while (pos < line.Length)
        {
            // Skip whitespace
            while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            {
                pos++;
            }

            if (pos >= line.Length)
            {
                break;
            }

            // Check for numbers first (to handle negative numbers before operator check)
            var numberMatch = TryMatchNumber(line, pos);
            if (numberMatch.HasValue)
            {
                tokens.Add(new SemanticToken
                {
                    Line = lineIndex,
                    StartChar = numberMatch.Value.start + offset,
                    Length = numberMatch.Value.length,
                    TokenType = SemanticTokenType.Number
                });
                pos = numberMatch.Value.start + numberMatch.Value.length;
                continue;
            }

            // Check for operators
            if (Operators.Contains(line[pos]))
            {
                tokens.Add(new SemanticToken
                {
                    Line = lineIndex,
                    StartChar = pos + offset,
                    Length = 1,
                    TokenType = SemanticTokenType.Operator
                });
                pos++;
                continue;
            }

            // Check for identifiers (mnemonics, registers, labels)
            var identifierMatch = TryMatchIdentifier(line, pos);
            if (identifierMatch.HasValue)
            {
                var (start, length) = identifierMatch.Value;
                var text = line.Substring(start, length);
                string textUpper = text.ToUpperInvariant();

                // Use AsmDudeTools.Get_Token_Type_Intel() - same as AsmDude2
                AsmTokenType asmTokenType = _asmDudeTools.Get_Token_Type_Intel(textUpper);

                // Map AsmTokenType to SemanticTokenType
                SemanticTokenType tokenType = asmTokenType switch
                {
                    AsmTokenType.Mnemonic => SemanticTokenType.Keyword,
                    AsmTokenType.Jump => SemanticTokenType.Keyword,
                    AsmTokenType.Register => SemanticTokenType.Parameter,
                    AsmTokenType.Directive => SemanticTokenType.Keyword,
                    AsmTokenType.Constant => SemanticTokenType.Number,
                    AsmTokenType.Label => SemanticTokenType.Variable,
                    AsmTokenType.LabelDef => SemanticTokenType.Function,
                    AsmTokenType.Misc => SemanticTokenType.Variable,
                    _ => SemanticTokenType.Variable
                };

                tokens.Add(new SemanticToken
                {
                    Line = lineIndex,
                    StartChar = start + offset,
                    Length = length,
                    TokenType = tokenType
                });

                pos = start + length;
                continue;
            }

            // Unknown character, skip it
            pos++;
        }

        return tokens;
    }

    private (int start, int length)? TryMatchNumber(string line, int pos)
    {
        // Try hex number (0x or 0X prefix)
        if (pos + 2 < line.Length && line[pos] == '0' && (line[pos + 1] == 'x' || line[pos + 1] == 'X'))
        {
            var start = pos;
            pos += 2;
            while (pos < line.Length && IsHexDigit(line[pos]))
            {
                pos++;
            }
            if (pos > start + 2)
            {
                return (start, pos - start);
            }
        }

        // Try binary number (0b or 0B prefix)
        if (pos + 2 < line.Length && line[pos] == '0' && (line[pos + 1] == 'b' || line[pos + 1] == 'B'))
        {
            var start = pos;
            pos += 2;
            while (pos < line.Length && (line[pos] == '0' || line[pos] == '1'))
            {
                pos++;
            }
            if (pos > start + 2)
            {
                return (start, pos - start);
            }
        }

        // Try decimal number (including negative)
        if (char.IsDigit(line[pos]) || (line[pos] == '-' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
        {
            var start = pos;
            if (line[pos] == '-')
            {
                pos++;
            }
            while (pos < line.Length && char.IsDigit(line[pos]))
            {
                pos++;
            }
            return (start, pos - start);
        }

        return null;
    }

    private (int start, int length)? TryMatchIdentifier(string line, int pos)
    {
        if (!IsIdentifierStart(line[pos]))
        {
            return null;
        }

        var start = pos;
        while (pos < line.Length && IsIdentifierChar(line[pos]))
        {
            pos++;
        }

        return (start, pos - start);
    }

    private bool IsValidIdentifier(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!IsIdentifierStart(text[0]))
        {
            return false;
        }

        return text.All(IsIdentifierChar);
    }

    private bool IsIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_' || c == '.';
    }

    private bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

    private bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
