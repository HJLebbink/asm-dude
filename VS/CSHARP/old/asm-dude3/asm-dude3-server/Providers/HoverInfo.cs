namespace AsmDude3.Server.Providers;

/// <summary>
/// Represents hover information (documentation shown on hover)
/// </summary>
public record HoverInfo
{
    /// <summary>
    /// The hover content (documentation text)
    /// </summary>
    public required string Contents { get; init; }

    /// <summary>
    /// The range of text this hover applies to
    /// </summary>
    public HoverRange? Range { get; init; }

    /// <summary>
    /// URL for clickable hyperlink (optional)
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Keyword/title text for the hyperlink (optional)
    /// </summary>
    public string? Keyword { get; init; }
}

/// <summary>
/// Represents a text range for hover highlighting
/// </summary>
public record HoverRange
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
