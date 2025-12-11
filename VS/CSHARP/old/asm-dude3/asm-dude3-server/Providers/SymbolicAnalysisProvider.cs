using AsmSim;
using AsmTools;
using Microsoft.Extensions.Logging;
using Microsoft.Z3;
using System.Collections.Concurrent;
using System.Text;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provider for symbolic execution analysis of assembly code using asm-sim
/// </summary>
public class SymbolicAnalysisProvider
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, SymbolicAnalysisResult> _cache = new();

    public SymbolicAnalysisProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyze assembly code document and infer register states
    /// </summary>
    public SymbolicAnalysisResult? AnalyzeDocument(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Length == 0)
        {
            return null;
        }

        // Check cache first
        var cacheKey = GenerateCacheKey(lines);
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        try
        {
            // Filter out empty lines and comments-only lines
            var validLines = new List<string>();
            var lineMapping = new Dictionary<int, int>(); // Maps result line index to original line index

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(';'))
                {
                    lineMapping[validLines.Count] = i;
                    validLines.Add(lines[i]);
                }
            }

            if (validLines.Count == 0)
            {
                return null;
            }

            // Create Tools and configure state tracking
            var tools = CreateTools();

            // Execute symbolically line by line
            State state = new(tools, "!0", "!0");
            var registerStates = new Dictionary<int, RegisterState>();
            var diagnostics = new List<DiagnosticInfo>();
            var optimizations = new List<OptimizationHint>();

            State? previousState = null;

            for (int i = 0; i < validLines.Count; i++)
            {
                var line = validLines[i];
                var originalLineNumber = lineMapping[i];

                // Remove inline comments
                var codeOnly = RemoveInlineComment(line);
                if (string.IsNullOrWhiteSpace(codeOnly))
                {
                    continue;
                }

                try
                {
                    // Check for undefined register usage before execution
                    var undefinedRegs = DetectUndefinedRegisterUse(codeOnly, state, tools);
                    foreach (var reg in undefinedRegs)
                    {
                        diagnostics.Add(new DiagnosticInfo
                        {
                            Line = originalLineNumber,
                            Severity = DiagnosticSeverity.Warning,
                            Message = $"Register {reg} may contain undefined value",
                            Code = "ASM001"
                        });
                    }

                    // Check for redundant operations before execution
                    if (previousState != null && IsRedundantOperation(codeOnly, state, previousState, tools))
                    {
                        diagnostics.Add(new DiagnosticInfo
                        {
                            Line = originalLineNumber,
                            Severity = DiagnosticSeverity.Information,
                            Message = "This instruction appears to be redundant",
                            Code = "ASM002"
                        });

                        optimizations.Add(new OptimizationHint
                        {
                            Line = originalLineNumber,
                            Title = "Remove redundant instruction",
                            Description = "This instruction does not change the program state",
                            Replacement = null // Suggest removal by returning null
                        });
                    }

                    // Execute the instruction symbolically
                    State? newState = Runner.SimpleStep_Forward(codeOnly, state);

                    if (newState == null)
                    {
                        _logger.LogWarning("Symbolic execution failed for line {Line}: {Code}", originalLineNumber, codeOnly);
                        continue;
                    }

                    // Freeze state for querying
                    newState.Frozen = true;

                    // Extract register and flag values
                    var regValues = ExtractRegisterValues(newState, tools);
                    var flagValues = ExtractFlagValues(newState, tools);

                    registerStates[originalLineNumber] = new RegisterState
                    {
                        LineNumber = originalLineNumber,
                        RegisterValues = regValues,
                        FlagValues = flagValues
                    };

                    previousState = state;
                    state = newState;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing line {Line}: {Code}", originalLineNumber, codeOnly);
                    // Continue with next line
                }
            }

            var result = new SymbolicAnalysisResult
            {
                RegisterStates = registerStates,
                Diagnostics = diagnostics,
                Optimizations = optimizations
            };

            // Cache the result
            _cache[cacheKey] = result;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during symbolic analysis");
            return null;
        }
    }

    /// <summary>
    /// Get register state at a specific line
    /// </summary>
    public RegisterState? GetRegisterStateAtLine(string[] lines, int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= lines.Length)
        {
            return null;
        }

        var result = AnalyzeDocument(lines);
        if (result == null)
        {
            return null;
        }

        return result.RegisterStates.GetValueOrDefault(lineNumber);
    }

    /// <summary>
    /// Find all issues in the code
    /// </summary>
    public List<DiagnosticInfo> FindIssues(string[] lines)
    {
        var result = AnalyzeDocument(lines);
        return result?.Diagnostics ?? new List<DiagnosticInfo>();
    }

    /// <summary>
    /// Invalidate cached analysis for a document
    /// </summary>
    public void InvalidateCache(string uri)
    {
        _cache.TryRemove(uri, out _);
    }

    #region Private Helper Methods

    private static Tools CreateTools()
    {
        var tools = new Tools();

        // Configure which registers and flags to track
        tools.StateConfig.Set_All_Off();

        // Track common 64-bit general-purpose registers
        tools.StateConfig.RAX = true;
        tools.StateConfig.RBX = true;
        tools.StateConfig.RCX = true;
        tools.StateConfig.RDX = true;
        tools.StateConfig.RSI = true;
        tools.StateConfig.RDI = true;
        tools.StateConfig.RBP = true;
        tools.StateConfig.RSP = true;
        tools.StateConfig.R8 = true;
        tools.StateConfig.R9 = true;
        tools.StateConfig.R10 = true;
        tools.StateConfig.R11 = true;
        tools.StateConfig.R12 = true;
        tools.StateConfig.R13 = true;
        tools.StateConfig.R14 = true;
        tools.StateConfig.R15 = true;

        // Track flags
        tools.StateConfig.CF = true;
        tools.StateConfig.PF = true;
        tools.StateConfig.AF = true;
        tools.StateConfig.ZF = true;
        tools.StateConfig.SF = true;
        tools.StateConfig.OF = true;
        tools.StateConfig.DF = true;

        return tools;
    }

    private static string RemoveInlineComment(string line)
    {
        var commentIndex = line.IndexOf(';');
        if (commentIndex >= 0)
        {
            return line.Substring(0, commentIndex).Trim();
        }
        return line.Trim();
    }

    private Dictionary<string, string> ExtractRegisterValues(State state, Tools tools)
    {
        var values = new Dictionary<string, string>();

        try
        {
            // Extract values for tracked registers
            if (tools.StateConfig.RAX) TryExtractRegister("RAX", Rn.RAX, state, values);
            if (tools.StateConfig.RBX) TryExtractRegister("RBX", Rn.RBX, state, values);
            if (tools.StateConfig.RCX) TryExtractRegister("RCX", Rn.RCX, state, values);
            if (tools.StateConfig.RDX) TryExtractRegister("RDX", Rn.RDX, state, values);
            if (tools.StateConfig.RSI) TryExtractRegister("RSI", Rn.RSI, state, values);
            if (tools.StateConfig.RDI) TryExtractRegister("RDI", Rn.RDI, state, values);
            if (tools.StateConfig.RBP) TryExtractRegister("RBP", Rn.RBP, state, values);
            if (tools.StateConfig.RSP) TryExtractRegister("RSP", Rn.RSP, state, values);
            if (tools.StateConfig.R8) TryExtractRegister("R8", Rn.R8, state, values);
            if (tools.StateConfig.R9) TryExtractRegister("R9", Rn.R9, state, values);
            if (tools.StateConfig.R10) TryExtractRegister("R10", Rn.R10, state, values);
            if (tools.StateConfig.R11) TryExtractRegister("R11", Rn.R11, state, values);
            if (tools.StateConfig.R12) TryExtractRegister("R12", Rn.R12, state, values);
            if (tools.StateConfig.R13) TryExtractRegister("R13", Rn.R13, state, values);
            if (tools.StateConfig.R14) TryExtractRegister("R14", Rn.R14, state, values);
            if (tools.StateConfig.R15) TryExtractRegister("R15", Rn.R15, state, values);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting register values");
        }

        return values;
    }

    private void TryExtractRegister(string name, Rn register, State state, Dictionary<string, string> values)
    {
        try
        {
            var tvArray = state.GetTvArray(register);
            if (tvArray != null && tvArray.Length > 0)
            {
                // Check if all bits are known (not UNKNOWN or UNDEFINED)
                bool allKnown = true;
                foreach (var tv in tvArray)
                {
                    if (tv != Tv.ZERO && tv != Tv.ONE)
                    {
                        allKnown = false;
                        break;
                    }
                }

                if (allKnown)
                {
                    var hexValue = ToolsZ3.ToStringHex(tvArray);
                    // Strip underscores from hex format (0x0000_0000_0000_000A -> 0x000000000000000A)
                    values[name] = hexValue.Replace("_", "");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract value for register {Register}", name);
        }
    }

    private Dictionary<string, bool> ExtractFlagValues(State state, Tools tools)
    {
        var values = new Dictionary<string, bool>();

        try
        {
            if (tools.StateConfig.CF) TryExtractFlag("CF", Flags.CF, state, values);
            if (tools.StateConfig.PF) TryExtractFlag("PF", Flags.PF, state, values);
            if (tools.StateConfig.AF) TryExtractFlag("AF", Flags.AF, state, values);
            if (tools.StateConfig.ZF) TryExtractFlag("ZF", Flags.ZF, state, values);
            if (tools.StateConfig.SF) TryExtractFlag("SF", Flags.SF, state, values);
            if (tools.StateConfig.OF) TryExtractFlag("OF", Flags.OF, state, values);
            if (tools.StateConfig.DF) TryExtractFlag("DF", Flags.DF, state, values);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting flag values");
        }

        return values;
    }

    private void TryExtractFlag(string name, Flags flag, State state, Dictionary<string, bool> values)
    {
        try
        {
            var tv = state.GetTv(flag);
            if (tv == Tv.ZERO)
            {
                values[name] = false;
            }
            else if (tv == Tv.ONE)
            {
                values[name] = true;
            }
            // If UNKNOWN, UNDEFINED, etc., don't add to dictionary
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract value for flag {Flag}", name);
        }
    }

    private List<string> DetectUndefinedRegisterUse(string instruction, State state, Tools tools)
    {
        var undefined = new List<string>();

        try
        {
            // Parse the instruction to find which registers are read
            var regsRead = GetRegistersRead(instruction);

            foreach (var reg in regsRead)
            {
                if (TryParseRegister(reg, out var rn))
                {
                    var tvArray = state.GetTvArray(rn);
                    if (tvArray != null && tvArray.Length > 0)
                    {
                        // Check if any bits are undefined
                        foreach (var tv in tvArray)
                        {
                            if (tv == Tv.UNDEFINED)
                            {
                                undefined.Add(reg.ToUpperInvariant());
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error detecting undefined registers");
        }

        return undefined;
    }

    private bool IsRedundantOperation(string instruction, State currentState, State previousState, Tools tools)
    {
        try
        {
            var trimmed = instruction.Trim().ToLowerInvariant();

            // Check for mov reg, reg (moving register to itself)
            if (trimmed.StartsWith("mov "))
            {
                var parts = trimmed.Substring(4).Split(',');
                if (parts.Length == 2)
                {
                    var dest = parts[0].Trim();
                    var src = parts[1].Trim();

                    // Same register?
                    if (dest.Equals(src, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Check if destination already contains the value being moved
                    if (int.TryParse(src, out var _) || src.StartsWith("0x"))
                    {
                        // Moving immediate value - check if register already has this value
                        // This would require comparing state before and after
                        // For now, we'll skip this complex check
                    }
                }
            }

            // Check for add/sub with 0
            if (trimmed.StartsWith("add ") || trimmed.StartsWith("sub "))
            {
                if (trimmed.Contains(", 0") || trimmed.EndsWith(" 0"))
                {
                    return true;
                }
            }

            // More complex redundancy detection could compare states
            // For now, these simple checks are sufficient
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking redundancy");
        }

        return false;
    }

    private List<string> GetRegistersRead(string instruction)
    {
        var registers = new List<string>();
        var trimmed = instruction.Trim().ToLowerInvariant();

        // Simple heuristic: split by common delimiters and check for register names
        var parts = trimmed.Split(new[] { ' ', ',', '[', ']', '+', '-', '*' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var cleaned = part.Trim();
            if (IsRegisterName(cleaned))
            {
                registers.Add(cleaned);
            }
        }

        // For most instructions, the first operand is destination, rest are source
        // Remove the first one if it's after a write instruction
        if (registers.Count > 0 && IsWriteInstruction(trimmed))
        {
            registers.RemoveAt(0); // Remove destination register
        }

        return registers.Distinct().ToList();
    }

    private bool IsWriteInstruction(string instruction)
    {
        var mnemonic = instruction.Split(' ')[0].ToLowerInvariant();
        return mnemonic switch
        {
            "mov" or "movb" or "movw" or "movd" or "movq" => true,
            "add" or "sub" or "mul" or "div" => true,
            "inc" or "dec" or "neg" or "not" => true,
            "and" or "or" or "xor" => true,
            "shl" or "shr" or "sal" or "sar" or "rol" or "ror" => true,
            "lea" => true,
            _ => false
        };
    }

    private bool IsRegisterName(string token)
    {
        var lower = token.ToLowerInvariant();
        return lower switch
        {
            "rax" or "rbx" or "rcx" or "rdx" or "rsi" or "rdi" or "rbp" or "rsp" => true,
            "r8" or "r9" or "r10" or "r11" or "r12" or "r13" or "r14" or "r15" => true,
            "eax" or "ebx" or "ecx" or "edx" or "esi" or "edi" or "ebp" or "esp" => true,
            "ax" or "bx" or "cx" or "dx" or "si" or "di" or "bp" or "sp" => true,
            "al" or "bl" or "cl" or "dl" or "sil" or "dil" or "bpl" or "spl" => true,
            "ah" or "bh" or "ch" or "dh" => true,
            _ => false
        };
    }

    private bool TryParseRegister(string name, out Rn register)
    {
        register = Rn.NOREG;

        var lower = name.ToLowerInvariant();
        register = lower switch
        {
            "rax" => Rn.RAX,
            "rbx" => Rn.RBX,
            "rcx" => Rn.RCX,
            "rdx" => Rn.RDX,
            "rsi" => Rn.RSI,
            "rdi" => Rn.RDI,
            "rbp" => Rn.RBP,
            "rsp" => Rn.RSP,
            "r8" => Rn.R8,
            "r9" => Rn.R9,
            "r10" => Rn.R10,
            "r11" => Rn.R11,
            "r12" => Rn.R12,
            "r13" => Rn.R13,
            "r14" => Rn.R14,
            "r15" => Rn.R15,
            _ => Rn.NOREG
        };

        return register != Rn.NOREG;
    }

    private static string GenerateCacheKey(string[] lines)
    {
        // Generate a simple hash of the lines array content
        // Use string concatenation with newlines to create a unique key
        var content = string.Join("\n", lines);

        // Use GetHashCode for a simple cache key
        // For production, consider using a better hash algorithm like SHA256
        return content.GetHashCode().ToString();
    }

    #endregion
}
