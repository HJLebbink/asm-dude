using Newtonsoft.Json;

namespace AsmDude3.Server;

#region Initialize

public record InitializeParams
{
    [JsonProperty("processId")]
    public int? ProcessId { get; init; }

    [JsonProperty("clientInfo")]
    public ClientInfo? ClientInfo { get; init; }

    [JsonProperty("rootUri")]
    public string? RootUri { get; init; }

    [JsonProperty("capabilities")]
    public ClientCapabilities? Capabilities { get; init; }
}

public record ClientInfo
{
    [JsonProperty("name")]
    public string Name { get; init; } = string.Empty;

    [JsonProperty("version")]
    public string? Version { get; init; }
}

public record ClientCapabilities
{
    // Simplified for now, extend as needed
}

public record InitializeResult
{
    [JsonProperty("capabilities")]
    public ServerCapabilities Capabilities { get; init; } = new();

    [JsonProperty("serverInfo")]
    public ServerInfo? ServerInfo { get; init; }
}

public record ServerInfo
{
    [JsonProperty("name")]
    public string Name { get; init; } = string.Empty;

    [JsonProperty("version")]
    public string? Version { get; init; }
}

public record ServerCapabilities
{
    [JsonProperty("textDocumentSync")]
    public TextDocumentSyncOptions? TextDocumentSync { get; init; }

    [JsonProperty("completionProvider")]
    public CompletionOptions? CompletionProvider { get; init; }

    [JsonProperty("hoverProvider")]
    public bool? HoverProvider { get; init; }

    [JsonProperty("signatureHelpProvider")]
    public SignatureHelpOptions? SignatureHelpProvider { get; init; }

    [JsonProperty("documentSymbolProvider")]
    public bool? DocumentSymbolProvider { get; init; }

    [JsonProperty("foldingRangeProvider")]
    public bool? FoldingRangeProvider { get; init; }

    [JsonProperty("documentHighlightProvider")]
    public bool? DocumentHighlightProvider { get; init; }

    [JsonProperty("referencesProvider")]
    public bool? ReferencesProvider { get; init; }

    [JsonProperty("semanticTokensProvider")]
    public SemanticTokensOptions? SemanticTokensProvider { get; init; }
}

#endregion

#region Text Document Sync

public record TextDocumentSyncOptions
{
    [JsonProperty("openClose")]
    public bool? OpenClose { get; init; }

    [JsonProperty("change")]
    public TextDocumentSyncKind? Change { get; init; }

    [JsonProperty("save")]
    public SaveOptions? Save { get; init; }
}

public enum TextDocumentSyncKind
{
    None = 0,
    Full = 1,
    Incremental = 2
}

public record SaveOptions
{
    [JsonProperty("includeText")]
    public bool? IncludeText { get; init; }
}

#endregion

#region Completion

public record CompletionOptions
{
    [JsonProperty("triggerCharacters")]
    public string[]? TriggerCharacters { get; init; }

    [JsonProperty("resolveProvider")]
    public bool? ResolveProvider { get; init; }
}

#endregion

#region Signature Help

public record SignatureHelpOptions
{
    [JsonProperty("triggerCharacters")]
    public string[]? TriggerCharacters { get; init; }
}

#endregion

#region Semantic Tokens

public record SemanticTokensOptions
{
    [JsonProperty("legend")]
    public SemanticTokensLegend Legend { get; init; } = new();

    [JsonProperty("full")]
    public bool? Full { get; init; }

    [JsonProperty("range")]
    public bool? Range { get; init; }
}

public record SemanticTokensLegend
{
    [JsonProperty("tokenTypes")]
    public string[] TokenTypes { get; init; } = Array.Empty<string>();

    [JsonProperty("tokenModifiers")]
    public string[] TokenModifiers { get; init; } = Array.Empty<string>();
}

public record SemanticTokensParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();
}

public record SemanticTokensResponse
{
    [JsonProperty("data")]
    public int[] Data { get; init; } = Array.Empty<int>();
}

#endregion

#region Completion

public record CompletionParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("position")]
    public Position Position { get; init; } = new();

    [JsonProperty("context")]
    public LspCompletionContext? Context { get; init; }
}

public record Position
{
    [JsonProperty("line")]
    public int Line { get; init; }

    [JsonProperty("character")]
    public int Character { get; init; }
}

public record LspCompletionContext
{
    [JsonProperty("triggerKind")]
    public CompletionTriggerKind TriggerKind { get; init; }

    [JsonProperty("triggerCharacter")]
    public string? TriggerCharacter { get; init; }
}

public enum CompletionTriggerKind
{
    Invoked = 1,
    TriggerCharacter = 2,
    TriggerForIncompleteCompletions = 3
}

public record CompletionList
{
    [JsonProperty("isIncomplete")]
    public bool IsIncomplete { get; init; }

    [JsonProperty("items")]
    public LspCompletionItem[] Items { get; init; } = Array.Empty<LspCompletionItem>();
}

public record LspCompletionItem
{
    [JsonProperty("label")]
    public string Label { get; init; } = string.Empty;

    [JsonProperty("kind")]
    public int? Kind { get; init; }

    [JsonProperty("detail")]
    public string? Detail { get; init; }

    [JsonProperty("documentation")]
    public string? Documentation { get; init; }

    [JsonProperty("insertText")]
    public string? InsertText { get; init; }

    [JsonProperty("sortText")]
    public string? SortText { get; init; }
}

#endregion

#region Hover

public record HoverParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("position")]
    public Position Position { get; init; } = new();
}

public record Hover
{
    [JsonProperty("contents")]
    public MarkupContent Contents { get; init; } = new();

    [JsonProperty("range")]
    public Range? Range { get; init; }
}

public record MarkupContent
{
    [JsonProperty("kind")]
    public string Kind { get; init; } = "markdown";

    [JsonProperty("value")]
    public string Value { get; init; } = string.Empty;
}

public record Range
{
    [JsonProperty("start")]
    public Position Start { get; init; } = new();

    [JsonProperty("end")]
    public Position End { get; init; } = new();
}

#endregion

#region Signature Help

public record SignatureHelpParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("position")]
    public Position Position { get; init; } = new();

    [JsonProperty("context")]
    public SignatureHelpContext? Context { get; init; }
}

public record SignatureHelpContext
{
    [JsonProperty("triggerKind")]
    public int TriggerKind { get; init; }

    [JsonProperty("triggerCharacter")]
    public string? TriggerCharacter { get; init; }

    [JsonProperty("isRetrigger")]
    public bool IsRetrigger { get; init; }

    [JsonProperty("activeSignatureHelp")]
    public SignatureHelp? ActiveSignatureHelp { get; init; }
}

public record SignatureHelp
{
    [JsonProperty("signatures")]
    public LspSignatureInformation[] Signatures { get; init; } = Array.Empty<LspSignatureInformation>();

    [JsonProperty("activeSignature")]
    public int? ActiveSignature { get; init; }

    [JsonProperty("activeParameter")]
    public int? ActiveParameter { get; init; }
}

public record LspSignatureInformation
{
    [JsonProperty("label")]
    public string Label { get; init; } = string.Empty;

    [JsonProperty("documentation")]
    public MarkupContent? Documentation { get; init; }

    [JsonProperty("parameters")]
    public LspParameterInformation[]? Parameters { get; init; }
}

public record LspParameterInformation
{
    [JsonProperty("label")]
    public string Label { get; init; } = string.Empty;

    [JsonProperty("documentation")]
    public MarkupContent? Documentation { get; init; }
}

#endregion

#region Document Symbols

public record DocumentSymbolParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();
}

public record DocumentSymbol
{
    [JsonProperty("name")]
    public string Name { get; init; } = string.Empty;

    [JsonProperty("kind")]
    public int Kind { get; init; }

    [JsonProperty("range")]
    public Range Range { get; init; } = new();

    [JsonProperty("selectionRange")]
    public Range SelectionRange { get; init; } = new();
}

#endregion

#region Text Document Items

public record TextDocumentItem
{
    [JsonProperty("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonProperty("languageId")]
    public string LanguageId { get; init; } = string.Empty;

    [JsonProperty("version")]
    public int Version { get; init; }

    [JsonProperty("text")]
    public string Text { get; init; } = string.Empty;
}

public record TextDocumentIdentifier
{
    [JsonProperty("uri")]
    public string Uri { get; init; } = string.Empty;
}

public record VersionedTextDocumentIdentifier : TextDocumentIdentifier
{
    [JsonProperty("version")]
    public int Version { get; init; }
}

#endregion

#region Document Notifications

public record InitializedParams
{
    // Empty for now
}

public record DidOpenTextDocumentParams
{
    [JsonProperty("textDocument")]
    public TextDocumentItem TextDocument { get; init; } = new();
}

public record DidChangeTextDocumentParams
{
    [JsonProperty("textDocument")]
    public VersionedTextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("contentChanges")]
    public TextDocumentContentChangeEvent[] ContentChanges { get; init; } = Array.Empty<TextDocumentContentChangeEvent>();
}

public record TextDocumentContentChangeEvent
{
    [JsonProperty("text")]
    public string Text { get; init; } = string.Empty;
}

public record DidCloseTextDocumentParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();
}

#endregion

#region Folding Range

public record FoldingRangeParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();
}

public record FoldingRange
{
    [JsonProperty("startLine")]
    public int StartLine { get; init; }

    [JsonProperty("endLine")]
    public int EndLine { get; init; }

    [JsonProperty("startCharacter")]
    public int? StartCharacter { get; init; }

    [JsonProperty("endCharacter")]
    public int? EndCharacter { get; init; }

    [JsonProperty("kind")]
    public string? Kind { get; init; }

    [JsonProperty("collapsedText")]
    public string? CollapsedText { get; init; }
}

#endregion

#region Document Highlights

public record DocumentHighlightParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("position")]
    public Position Position { get; init; } = new();
}

public record DocumentHighlight
{
    [JsonProperty("range")]
    public Range Range { get; init; } = new();

    [JsonProperty("kind")]
    public int? Kind { get; init; }
}

#endregion

#region Find References

public record ReferenceParams
{
    [JsonProperty("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = new();

    [JsonProperty("position")]
    public Position Position { get; init; } = new();

    [JsonProperty("context")]
    public ReferenceContext? Context { get; init; }
}

public record ReferenceContext
{
    [JsonProperty("includeDeclaration")]
    public bool IncludeDeclaration { get; init; }
}

public record Location
{
    [JsonProperty("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonProperty("range")]
    public Range Range { get; init; } = new();
}

#endregion
