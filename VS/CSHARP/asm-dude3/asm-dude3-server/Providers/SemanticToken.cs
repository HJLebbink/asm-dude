namespace AsmDude3.Server.Providers;

/// <summary>
/// Represents a semantic token in the document
/// </summary>
public record SemanticToken
{
    /// <summary>
    /// The line number (0-based)
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The starting character position on the line (0-based)
    /// </summary>
    public required int StartChar { get; init; }

    /// <summary>
    /// The length of the token
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// The type of token
    /// </summary>
    public required SemanticTokenType TokenType { get; init; }

    /// <summary>
    /// Token modifiers (optional)
    /// </summary>
    public int Modifiers { get; init; } = 0;
}

/// <summary>
/// Types of semantic tokens (must match order in Legend.TokenTypes)
/// </summary>
public enum SemanticTokenType
{
    Keyword = 0,    // Mnemonics (mov, add, sub, etc.)
    Operator = 1,   // Operators (+, -, *, [, ], etc.)
    Variable = 2,   // Label references
    Parameter = 3,  // Registers (rax, rbx, etc.) - mapped to standard LSP 'parameter' type
    Number = 4,     // Numeric literals
    Comment = 5,    // Comments (;)
    Function = 6    // Label definitions (name:) - mapped to standard LSP 'function' type
}
