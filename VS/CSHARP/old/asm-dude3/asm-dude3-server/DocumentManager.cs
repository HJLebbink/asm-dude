using AsmDude3.Server.Providers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AsmDude3.Server;

/// <summary>
/// Manages open text documents and their content
/// </summary>
public class DocumentManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, DocumentState> _documents = new();
    private readonly ConcurrentDictionary<string, List<FoldingRangeInfo>> _foldingRanges = new();

    public DocumentManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Open a new document
    /// </summary>
    public void OpenDocument(TextDocumentItem document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var state = new DocumentState
        {
            Uri = document.Uri,
            LanguageId = document.LanguageId,
            Version = document.Version,
            Text = document.Text,
            Lines = SplitIntoLines(document.Text)
        };

        if (_documents.TryAdd(document.Uri, state))
        {
            _logger.LogInformation("Opened document: {Uri}, Lines: {LineCount}", document.Uri, state.Lines.Length);
        }
        else
        {
            _logger.LogWarning("Document already open: {Uri}", document.Uri);
        }
    }

    /// <summary>
    /// Update an existing document
    /// </summary>
    public void UpdateDocument(string uri, TextDocumentContentChangeEvent[] changes)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ArgumentNullException.ThrowIfNull(changes);

        if (!_documents.TryGetValue(uri, out var state))
        {
            _logger.LogWarning("Attempted to update non-existent document: {Uri}", uri);
            return;
        }

        // For now, we only support full document sync
        if (changes.Length > 0)
        {
            var newText = changes[0].Text;
            var newState = state with
            {
                Version = state.Version + 1,
                Text = newText,
                Lines = SplitIntoLines(newText)
            };

            _documents[uri] = newState;
            _logger.LogDebug("Updated document: {Uri}, Version: {Version}, Lines: {LineCount}",
                uri, newState.Version, newState.Lines.Length);
        }
    }

    /// <summary>
    /// Close a document
    /// </summary>
    public void CloseDocument(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        if (_documents.TryRemove(uri, out var state))
        {
            _foldingRanges.TryRemove(uri, out _);
            _logger.LogInformation("Closed document: {Uri}", uri);
        }
        else
        {
            _logger.LogWarning("Attempted to close non-existent document: {Uri}", uri);
        }
    }

    /// <summary>
    /// Get a document by URI
    /// </summary>
    public DocumentState? GetDocument(string uri)
    {
        _documents.TryGetValue(uri, out var state);
        return state;
    }

    /// <summary>
    /// Check if a document is open
    /// </summary>
    public bool IsDocumentOpen(string uri)
    {
        return _documents.ContainsKey(uri);
    }

    /// <summary>
    /// Get all open document URIs
    /// </summary>
    public IReadOnlyCollection<string> GetOpenDocumentUris()
    {
        return _documents.Keys.ToList();
    }

    /// <summary>
    /// Get the number of open documents
    /// </summary>
    public int DocumentCount => _documents.Count;

    /// <summary>
    /// Set folding ranges for a document
    /// </summary>
    public void SetFoldingRanges(string uri, List<FoldingRangeInfo> ranges)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ArgumentNullException.ThrowIfNull(ranges);

        _foldingRanges[uri] = ranges;
        _logger.LogDebug("Set {Count} folding ranges for document: {Uri}", ranges.Count, uri);
    }

    /// <summary>
    /// Get folding ranges for a document
    /// </summary>
    public List<FoldingRangeInfo>? GetFoldingRanges(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        return _foldingRanges.TryGetValue(uri, out var ranges) ? ranges : null;
    }

    /// <summary>
    /// Split text into lines for easier processing
    /// </summary>
    private static string[] SplitIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }
}

/// <summary>
/// Represents the state of an open document
/// </summary>
public record DocumentState
{
    public required string Uri { get; init; }
    public required string LanguageId { get; init; }
    public required int Version { get; init; }
    public required string Text { get; init; }
    public required string[] Lines { get; init; }
}
