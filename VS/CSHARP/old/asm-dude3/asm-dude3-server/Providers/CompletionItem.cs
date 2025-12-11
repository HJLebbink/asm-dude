namespace AsmDude3.Server.Providers;

/// <summary>
/// Represents a completion item (code suggestion)
/// </summary>
public record CompletionItem
{
    /// <summary>
    /// The label/text of this completion item
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The kind of completion (keyword, variable, etc.)
    /// </summary>
    public required CompletionItemKind Kind { get; init; }

    /// <summary>
    /// A human-readable string with additional information
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// A human-readable string that represents a doc-comment
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The text to insert when this completion is selected
    /// </summary>
    public string? InsertText { get; init; }

    /// <summary>
    /// Sort text (defaults to Label if not specified)
    /// </summary>
    public string SortText { get; init; } = string.Empty;
}

/// <summary>
/// The kind of a completion entry
/// </summary>
public enum CompletionItemKind
{
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18
}

/// <summary>
/// Context for completion (what kind of thing are we completing)
/// </summary>
public enum CompletionContext
{
    /// <summary>
    /// Completing a mnemonic/instruction
    /// </summary>
    Mnemonic,

    /// <summary>
    /// Completing a register
    /// </summary>
    Register,

    /// <summary>
    /// Completing a label
    /// </summary>
    Label,

    /// <summary>
    /// Completing a number/constant
    /// </summary>
    Number
}

/// <summary>
/// Represents a list of completion items (internal type for provider)
/// </summary>
public class InternalCompletionList
{
    /// <summary>
    /// The completion items
    /// </summary>
    public required CompletionItem[] Items { get; init; }
}
