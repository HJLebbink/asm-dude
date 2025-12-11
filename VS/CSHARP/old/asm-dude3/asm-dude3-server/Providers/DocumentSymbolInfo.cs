namespace AsmDude3.Server.Providers;

/// <summary>
/// Represents a document symbol (label, function, section)
/// </summary>
public record DocumentSymbolInfo
{
    /// <summary>
    /// The name of the symbol
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The kind of symbol
    /// </summary>
    public required SymbolKind Kind { get; init; }

    /// <summary>
    /// The line number where the symbol is defined (0-based)
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The character range of the symbol
    /// </summary>
    public SymbolRange? Range { get; init; }
}

/// <summary>
/// Symbol kinds
/// </summary>
public enum SymbolKind
{
    Function = 12,   // Function labels
    Variable = 13,   // Data labels
    Namespace = 3    // Section directives (.text, .data, .bss)
}

/// <summary>
/// Represents a text range for a symbol
/// </summary>
public record SymbolRange
{
    /// <summary>
    /// Start character position (0-based)
    /// </summary>
    public required int StartChar { get; init; }

    /// <summary>
    /// End character position (exclusive, 0-based)
    /// </summary>
    public required int EndChar { get; init; }
}
