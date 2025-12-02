using AsmDude3.Server.Providers;
using AsmDude3.Server.Stores;
using AsmTools;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Diagnostics;
using System.Reflection;

namespace AsmDude3.Server;

/// <summary>
/// Core Language Server implementation handling LSP protocol
/// </summary>
public class LanguageServer
{
    private readonly ILogger _logger;
    private readonly TaskCompletionSource _exitTcs = new();
    private readonly DocumentManager _documentManager;
    private readonly SemanticTokensProvider _semanticTokensProvider;
    private readonly CompletionProvider _completionProvider;
    private readonly HoverProvider _hoverProvider;
    private readonly SignatureHelpProvider _signatureHelpProvider;
    private readonly DocumentSymbolProvider _documentSymbolProvider;
    private readonly FoldingRangeProvider _foldingRangeProvider;
    private readonly AsmDude2Tools _asmDudeTools;
    private readonly MnemonicStore _mnemonicStore;
    private readonly AsmLanguageServerOptions _options;
    private ServerCapabilities? _capabilities;
    private bool _isInitialized;

    public LanguageServer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Extract embedded resources to temp directory
        string resourceDir = ExtractEmbeddedResources();
        _logger.LogInformation("Extracted resources to: {Path}", resourceDir);

        var traceSource = new TraceSource("AsmDude3");

        // Initialize AsmDudeTools with XML data
        _asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        // Create default options with all architectures enabled
        _options = CreateDefaultOptions();

        // Initialize MnemonicStore with signature files
        string filename_Regular = Path.Combine(resourceDir, "signature-may2019.txt");
        string filename_Hand = Path.Combine(resourceDir, "signature-hand-1.txt");
        _mnemonicStore = new MnemonicStore(logger, filename_Regular, filename_Hand, _options);

        // Initialize PerformanceStore with performance data
        string performancePath = Path.Combine(resourceDir, "Performance");
        var performanceStore = new PerformanceStore(logger, performancePath, _options);

        // Initialize providers
        _documentManager = new DocumentManager(logger);
        _semanticTokensProvider = new SemanticTokensProvider(logger, _asmDudeTools);
        _completionProvider = new CompletionProvider(logger, _asmDudeTools, _mnemonicStore, _options);
        _hoverProvider = new HoverProvider(logger, _asmDudeTools, _mnemonicStore, performanceStore, _options);
        _signatureHelpProvider = new SignatureHelpProvider(logger, _mnemonicStore, _options);
        _documentSymbolProvider = new DocumentSymbolProvider(logger);
        _foldingRangeProvider = new FoldingRangeProvider(logger);
    }

    /// <summary>
    /// Create default options with all architectures enabled
    /// </summary>
    private AsmLanguageServerOptions CreateDefaultOptions()
    {
        return new AsmLanguageServerOptions
        {
            SignatureHelp_On = true,
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_486 = true,
            ARCH_MMX = true,
            ARCH_SSE = true,
            ARCH_SSE2 = true,
            ARCH_SSE3 = true,
            ARCH_SSSE3 = true,
            ARCH_SSE4_1 = true,
            ARCH_SSE4_2 = true,
            ARCH_SSE4A = true,
            ARCH_SSE5 = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_VL = true,
            ARCH_AVX512_PF = true,
            ARCH_AVX512_DQ = true,
            ARCH_AVX512_BW = true,
            ARCH_AVX512_ER = true,
            ARCH_AVX512_F = true,
            ARCH_AVX512_CD = true,
            ARCH_X64 = true,
            ARCH_BMI1 = true,
            ARCH_BMI2 = true,
            ARCH_P6 = true,
            ARCH_IA64 = true,
            ARCH_FMA = true,
            ARCH_TBM = true,
            ARCH_AMD = true,
            ARCH_PENT = true,
            ARCH_3DNOW = true,
            ARCH_CYRIX = true,
            ARCH_CYRIXM = true,
            ARCH_VMX = true,
            ARCH_RTM = true,
            ARCH_MPX = true,
            ARCH_SHA = true,
            ARCH_ADX = true,
            ARCH_F16C = true,
            ARCH_FSGSBASE = true,
            ARCH_HLE = true,
            ARCH_INVPCID = true,
            ARCH_PCLMULQDQ = true,
            ARCH_LZCNT = true,
            ARCH_PREFETCHWT1 = true,
            ARCH_PRFCHW = true,
            ARCH_RDPID = true,
            ARCH_RDRAND = true,
            ARCH_RDSEED = true,
            ARCH_XSAVEOPT = true,
            ARCH_UNDOC = true,
            ARCH_AES = true,
            ARCH_AVX512_IFMA = true,
            ARCH_AVX512_VBMI = true,
            ARCH_AVX512_VPOPCNTDQ = true,
            ARCH_AVX512_4VNNIW = true,
            ARCH_AVX512_4FMAPS = true,
            ARCH_AVX512_VBMI2 = true,
            ARCH_AVX512_VNNI = true,
            ARCH_AVX512_BITALG = true,
            ARCH_AVX512_GFNI = true,
            ARCH_AVX512_VAES = true,
            ARCH_AVX512_VPCLMULQDQ = true,
            ARCH_SMX = true,
            ARCH_SGX1 = true,
            ARCH_SGX2 = true,
            ARCH_CLDEMOTE = true,
            ARCH_MOVDIR64B = true,
            ARCH_MOVDIRI = true,
            ARCH_PCONFIG = true,
            ARCH_WAITPKG = true,
            ARCH_AVX512_BF16 = true,
            ARCH_AVX512_VP2INTERSECT = true,
            ARCH_ENQCMD = true,
        };
    }

    /// <summary>
    /// Extract embedded resources (AsmDudeData.xml and signature files) to a temporary directory
    /// </summary>
    private string ExtractEmbeddedResources()
    {
        var assembly = typeof(LanguageServer).Assembly;

        // List all embedded resources for debugging
        var allResources = assembly.GetManifestResourceNames();
        _logger.LogInformation("Available embedded resources: {Resources}", string.Join(", ", allResources));

        // Create temp directory for resources
        string tempDir = Path.Combine(Path.GetTempPath(), "AsmDude3");
        Directory.CreateDirectory(tempDir);

        // Create Performance subdirectory
        string performanceDir = Path.Combine(tempDir, "Performance");
        Directory.CreateDirectory(performanceDir);

        // Extract all required resources
        string[] requiredResources = new[] {
            "AsmDudeData.xml",
            "signature-may2019.txt",
            "signature-hand-1.txt"
        };

        foreach (string resourceFileName in requiredResources)
        {
            var resourceName = allResources.FirstOrDefault(r => r.EndsWith(resourceFileName));
            if (resourceName == null)
            {
                _logger.LogError("Could not find {FileName} in embedded resources", resourceFileName);
                throw new FileNotFoundException($"{resourceFileName} not found in embedded resources");
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogError("Could not load embedded resource: {ResourceName}", resourceName);
                throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            }

            string tempFilePath = Path.Combine(tempDir, resourceFileName);
            using var fileStream = File.Create(tempFilePath);
            stream.CopyTo(fileStream);

            _logger.LogInformation("Extracted embedded resource ({ResourceName}) to: {Path}", resourceName, tempFilePath);
        }

        // Extract performance TSV files
        var performanceResources = allResources.Where(r => r.Contains("Performance") && r.EndsWith(".tsv"));
        foreach (var resourceName in performanceResources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Extract just the filename (e.g., "Haswell.tsv" from "...Performance.Haswell.tsv")
                var parts = resourceName.Split('.');
                string fileName = parts[^2] + "." + parts[^1]; // Get last two parts

                string tempFilePath = Path.Combine(performanceDir, fileName);
                using var fileStream = File.Create(tempFilePath);
                stream.CopyTo(fileStream);

                _logger.LogInformation("Extracted performance resource ({ResourceName}) to: {Path}", resourceName, tempFilePath);
            }
        }

        // Return directory path
        return tempDir;
    }

    /// <summary>
    /// Wait for the server to exit
    /// </summary>
    public Task WaitForExitAsync() => _exitTcs.Task;

    /// <summary>
    /// Signal the server to exit
    /// </summary>
    public void Exit() => _exitTcs.TrySetResult();

    #region LSP Protocol Methods

    /// <summary>
    /// Handle the initialize request from the client
    /// </summary>
    [JsonRpcMethod("initialize")]
    public InitializeResult Initialize(int? processId, string? rootUri, object? capabilities)
    {
        _logger.LogInformation("Received initialize request from client. ProcessId: {ProcessId}, RootUri: {RootUri}",
            processId, rootUri ?? "null");

        // Build server capabilities
        _capabilities = new ServerCapabilities
        {
            TextDocumentSync = new TextDocumentSyncOptions
            {
                OpenClose = true,
                Change = TextDocumentSyncKind.Full, // Start with full sync, optimize later
                Save = new SaveOptions { IncludeText = false }
            },
            CompletionProvider = new CompletionOptions
            {
                TriggerCharacters = new[] { ".", "[", " " },
                ResolveProvider = false
            },
            HoverProvider = true,
            SignatureHelpProvider = new SignatureHelpOptions
            {
                TriggerCharacters = new[] { " ", "," }
            },
            DocumentSymbolProvider = false,  // Temporarily disabled to avoid VS crash
            FoldingRangeProvider = true,
            SemanticTokensProvider = new SemanticTokensOptions
            {
                Legend = new SemanticTokensLegend
                {
                    // Use standard LSP token types for better VS compatibility
                    TokenTypes = new[] { "keyword", "operator", "variable", "parameter", "number", "comment", "function" },
                    TokenModifiers = new[] { "readonly", "documentation" }
                },
                Full = true,
                Range = false
            }
        };

        var result = new InitializeResult
        {
            Capabilities = _capabilities,
            ServerInfo = new ServerInfo
            {
                Name = "AsmDude3 Language Server",
                Version = "3.0.0"
            }
        };

        _logger.LogInformation("Initialize complete, capabilities sent");
        return result;
    }

    /// <summary>
    /// Handle the initialized notification from the client
    /// </summary>
    [JsonRpcMethod("initialized")]
    public void Initialized()
    {
        _isInitialized = true;
        _logger.LogInformation("Server initialized and ready");
    }

    /// <summary>
    /// Handle the shutdown request from the client
    /// </summary>
    [JsonRpcMethod("shutdown")]
    public object? Shutdown()
    {
        _logger.LogInformation("Shutdown requested");
        _isInitialized = false;
        return null;
    }

    /// <summary>
    /// Handle the exit notification from the client
    /// </summary>
    [JsonRpcMethod("exit")]
    public void ExitNotification()
    {
        _logger.LogInformation("Exit notification received");
        Exit();
    }

    /// <summary>
    /// Handle text document opened notification
    /// </summary>
    [JsonRpcMethod("textDocument/didOpen")]
    public void DidOpenTextDocument(TextDocumentItem textDocument)
    {
        _logger.LogDebug("Document opened: {Uri}", textDocument.Uri);
        _documentManager.OpenDocument(textDocument);
        UpdateFoldingRanges(textDocument.Uri);
    }

    /// <summary>
    /// Handle text document changed notification
    /// </summary>
    [JsonRpcMethod("textDocument/didChange")]
    public void DidChangeTextDocument(VersionedTextDocumentIdentifier textDocument, TextDocumentContentChangeEvent[] contentChanges)
    {
        _logger.LogDebug("Document changed: {Uri}", textDocument.Uri);
        _documentManager.UpdateDocument(textDocument.Uri, contentChanges);
        UpdateFoldingRanges(textDocument.Uri);
    }

    /// <summary>
    /// Handle text document closed notification
    /// </summary>
    [JsonRpcMethod("textDocument/didClose")]
    public void DidCloseTextDocument(TextDocumentIdentifier textDocument)
    {
        _logger.LogDebug("Document closed: {Uri}", textDocument.Uri);
        _documentManager.CloseDocument(textDocument.Uri);
    }

    /// <summary>
    /// Handle semantic tokens request for full document
    /// </summary>
    [JsonRpcMethod("textDocument/semanticTokens/full")]
    public SemanticTokensResponse? GetSemanticTokensFull(TextDocumentIdentifier textDocument)
    {
        _logger.LogDebug("Semantic tokens requested for: {Uri}", textDocument.Uri);

        var document = _documentManager.GetDocument(textDocument.Uri);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
            return null;
        }

        // Get semantic tokens from provider
        var tokens = _semanticTokensProvider.ProvideSemanticTokens(document.Lines);

        // Convert to LSP format (flat integer array with deltas)
        var data = ConvertTokensToLspFormat(tokens);

        return new SemanticTokensResponse
        {
            Data = data
        };
    }

    /// <summary>
    /// Convert our token format to LSP semantic tokens format
    /// </summary>
    private int[] ConvertTokensToLspFormat(List<SemanticToken> tokens)
    {
        var data = new List<int>();
        int prevLine = 0;
        int prevChar = 0;

        foreach (var token in tokens)
        {
            // Calculate deltas
            int deltaLine = token.Line - prevLine;
            int deltaChar = deltaLine == 0 ? token.StartChar - prevChar : token.StartChar;

            // Add token: [deltaLine, deltaStartChar, length, tokenType, tokenModifiers]
            data.Add(deltaLine);
            data.Add(deltaChar);
            data.Add(token.Length);
            data.Add((int)token.TokenType);
            data.Add(token.Modifiers);

            // Update previous position
            prevLine = token.Line;
            prevChar = token.StartChar;
        }

        return data.ToArray();
    }

    /// <summary>
    /// Handle completion request
    /// </summary>
    [JsonRpcMethod("textDocument/completion")]
    public CompletionList? GetCompletions(TextDocumentIdentifier textDocument, Position position)
    {
        _logger.LogDebug("Completion requested for: {Uri} at line {Line}, character {Char}",
            textDocument.Uri, position.Line, position.Character);

        var document = _documentManager.GetDocument(textDocument.Uri);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
            return null;
        }

        // Get the line text
        if (position.Line < 0 || position.Line >= document.Lines.Length)
        {
            _logger.LogWarning("Invalid line number: {Line}", position.Line);
            return null;
        }

        // Get completions using the new provider API
        // Note: LabelGraph is not yet implemented in AsmDude3, so passing null for now
        var internalCompletionList = _completionProvider.ProvideCompletions(
            document.Lines,
            position.Line,
            position.Character,
            labelGraph: null);

        if (internalCompletionList == null)
        {
            return new CompletionList
            {
                IsIncomplete = false,
                Items = Array.Empty<LspCompletionItem>()
            };
        }

        // Convert to LSP format
        var items = internalCompletionList.Items.Select(c => new LspCompletionItem
        {
            Label = c.Label,
            Kind = (int)c.Kind,
            Detail = string.Empty,
            Documentation = c.Documentation ?? string.Empty,
            InsertText = c.InsertText ?? c.Label,
            SortText = c.SortText ?? c.Label
        }).ToArray();

        return new CompletionList
        {
            IsIncomplete = false,
            Items = items
        };
    }

    /// <summary>
    /// Handle hover request
    /// Returns VSInternalHover with clickable links when URL is available, otherwise standard Hover
    /// </summary>
    [JsonRpcMethod("textDocument/hover")]
    public object? GetHover(TextDocumentIdentifier textDocument, Position position)
    {
        _logger.LogDebug("Hover requested for: {Uri} at line {Line}, character {Char}",
            textDocument.Uri, position.Line, position.Character);

        var document = _documentManager.GetDocument(textDocument.Uri);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
            return null;
        }

        // Get the line text
        if (position.Line < 0 || position.Line >= document.Lines.Length)
        {
            _logger.LogWarning("Invalid line number: {Line}", position.Line);
            return null;
        }

        var line = document.Lines[position.Line];
        var charPosition = position.Character;

        // Get hover information
        var hoverInfo = _hoverProvider.ProvideHover(line, charPosition);
        if (hoverInfo == null)
        {
            return null;
        }

        int startChar = hoverInfo.Range?.StartChar ?? charPosition;
        int endChar = hoverInfo.Range?.EndChar ?? charPosition;

        // If URL is available, create VSInternalHover with clickable hyperlink
        if (!string.IsNullOrEmpty(hoverInfo.Url) && !string.IsNullOrEmpty(hoverInfo.Keyword))
        {
            _logger.LogDebug("Creating VSInternalHover with clickable link: {Keyword} -> {Url}",
                hoverInfo.Keyword, hoverInfo.Url);

            return Providers.HoverBuilder.CreateHoverWithLink(
                hoverInfo.Keyword,
                hoverInfo.Url,
                hoverInfo.Contents,
                position.Line,
                startChar,
                endChar
            );
        }

        // Otherwise, return standard Hover
        var hover = new Hover
        {
            Contents = new MarkupContent
            {
                Kind = "markdown",
                Value = hoverInfo.Contents
            }
        };

        if (hoverInfo.Range != null)
        {
            hover = hover with
            {
                Range = new Range
                {
                    Start = new Position { Line = position.Line, Character = hoverInfo.Range.StartChar },
                    End = new Position { Line = position.Line, Character = hoverInfo.Range.EndChar }
                }
            };
        }

        return hover;
    }

    /// <summary>
    /// Handle signature help request
    /// </summary>
    [JsonRpcMethod("textDocument/signatureHelp")]
    public SignatureHelp? GetSignatureHelp(TextDocumentIdentifier textDocument, Position position)
    {
        _logger.LogDebug("Signature help requested for: {Uri} at line {Line}, character {Char}",
            textDocument.Uri, position.Line, position.Character);

        var document = _documentManager.GetDocument(textDocument.Uri);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
            return null;
        }

        // Get the line text
        if (position.Line < 0 || position.Line >= document.Lines.Length)
        {
            _logger.LogWarning("Invalid line number: {Line}", position.Line);
            return null;
        }

        var line = document.Lines[position.Line];
        var charPosition = position.Character;

        // Get signature help
        var signatureHelp = _signatureHelpProvider.ProvideSignatureHelp(line, charPosition, position.Line);
        if (signatureHelp == null)
        {
            return null;
        }

        // Convert to LSP format
        var lspSignatures = signatureHelp.Signatures.Select(sig => new LspSignatureInformation
        {
            Label = sig.Label,
            Documentation = sig.Documentation != null
                ? new MarkupContent { Kind = "markdown", Value = sig.Documentation }
                : null,
            Parameters = sig.Parameters?.Select(param => new LspParameterInformation
            {
                Label = param.Label,
                Documentation = param.Documentation != null
                    ? new MarkupContent { Kind = "markdown", Value = param.Documentation }
                    : null
            }).ToArray()
        }).ToArray();

        return new SignatureHelp
        {
            Signatures = lspSignatures,
            ActiveSignature = signatureHelp.ActiveSignature,
            ActiveParameter = signatureHelp.ActiveParameter
        };
    }

    /// <summary>
    /// Handle document symbol request
    /// </summary>
    [JsonRpcMethod("textDocument/documentSymbol")]
    public DocumentSymbol[]? GetDocumentSymbols(TextDocumentIdentifier textDocument)
    {
        _logger.LogDebug("Document symbols requested for: {Uri}", textDocument.Uri);

        var document = _documentManager.GetDocument(textDocument.Uri);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
            return null;
        }

        // Get symbols
        var symbols = _documentSymbolProvider.ProvideSymbols(document.Lines);

        // Convert to LSP format
        return symbols.Select(s => new DocumentSymbol
        {
            Name = s.Name,
            Kind = (int)s.Kind,
            Range = new Range
            {
                Start = new Position { Line = s.Line, Character = s.Range?.StartChar ?? 0 },
                End = new Position { Line = s.Line, Character = s.Range?.EndChar ?? 0 }
            },
            SelectionRange = new Range
            {
                Start = new Position { Line = s.Line, Character = s.Range?.StartChar ?? 0 },
                End = new Position { Line = s.Line, Character = s.Range?.EndChar ?? 0 }
            }
        }).ToArray();
    }

    /// <summary>
    /// Handle folding range request
    /// </summary>
    [JsonRpcMethod("textDocument/foldingRange")]
    public FoldingRange[]? GetFoldingRanges(TextDocumentIdentifier textDocument)
    {
        _logger.LogDebug("Folding ranges requested for: {Uri}", textDocument.Uri);

        // Get cached folding ranges
        var ranges = _documentManager.GetFoldingRanges(textDocument.Uri);
        if (ranges == null)
        {
            _logger.LogWarning("No folding ranges found for: {Uri}", textDocument.Uri);
            return Array.Empty<FoldingRange>();
        }

        // Convert to LSP format
        return ranges.Select(r => new FoldingRange
        {
            StartLine = r.StartLine,
            EndLine = r.EndLine,
            StartCharacter = r.StartCharacter,
            EndCharacter = r.EndCharacter,
            Kind = r.Kind,
            CollapsedText = r.CollapsedText
        }).ToArray();
    }

    /// <summary>
    /// Update folding ranges for a document (called when document opens or changes)
    /// </summary>
    private void UpdateFoldingRanges(string uri)
    {
        var document = _documentManager.GetDocument(uri);
        if (document == null)
        {
            _logger.LogWarning("Cannot update folding ranges - document not found: {Uri}", uri);
            return;
        }

        // Compute folding ranges
        var ranges = _foldingRangeProvider.ProvideFoldingRanges(document.Lines);

        // Cache them
        _documentManager.SetFoldingRanges(uri, ranges);

        _logger.LogDebug("Updated {Count} folding ranges for: {Uri}", ranges.Count, uri);
    }

    #endregion

    /// <summary>
    /// Get current server state for testing
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Get document manager for testing
    /// </summary>
    public DocumentManager DocumentManager => _documentManager;
}
