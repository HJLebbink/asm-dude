// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmTools;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmDude2LS;

public class LanguageServerTarget(LanguageServer server)
{
    private int version = 1;
    public TraceSetting traceSetting;

    public event EventHandler? OnInitializeCompletion;

    public event EventHandler? OnInitialized;

    private static AsmLanguageServerOptions CreateDefaultOptions() => new()
    {
        AsmDoc_On = true,
        AsmDoc_Url = "https://github.com/HJLebbink/asm-dude/wiki/",
        CodeCompletion_On = true,
        SignatureHelp_On = true,
        CodeFolding_On = true,
        CodeFolding_BeginTag = "#region",
        CodeFolding_EndTag = "#endregion",
        ARCH_8086 = true,
        ARCH_X64 = true,
        ARCH_SSE = true,
        ARCH_SSE2 = true,
        ARCH_AVX = true,
        ARCH_AVX2 = true,
        IntelliSense_Label_Analysis_On = true,
        Global_MaxFileLines = 10000,
        useAssemblerAutoDetect = true,

        // AsmSim (Z3 proven states) - enabled by default
        AsmSim_On = true,
        AsmSim_Z3_Timeout_MS = 5000,
        AsmSim_Number_Of_Threads = 4,
        AsmSim_64_Bits = true,
        AsmSim_Show_Syntax_Errors = true,
        AsmSim_Decorate_Syntax_Errors = true,
        AsmSim_Show_Usage_Of_Undefined = true,
        AsmSim_Decorate_Usage_Of_Undefined = true,
        AsmSim_Decorate_Registers = true,
        AsmSim_Show_Unreachable_Instructions = true,
        AsmSim_Decorate_Unreachable_Instructions = true,
        AsmSim_Show_Register_In_Instruction_Tooltip = true,
        AsmSim_Show_Register_In_Register_Tooltip = true,

        // Performance info - enabled by default
        PerformanceInfo_On = true,
        PerformanceInfo_Haswell_On = true,
        PerformanceInfo_Skylake_On = true,
    };

    [JsonRpcMethod(Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: LSP initialize, server capabilities, initialization options, protocol handshake
    /// USED IN: LanguageServer initialization, LSP protocol handshake
    /// SEE ALSO: Initialized, AsmLanguageServerOptions, ServerCapabilities

    /// <summary>
    /// Handle workspace/initialize request. Server receives this first and returns server capabilities.
    /// </summary>
    /// <param name="parameter">InitializeParams with client info, initialization options, and workspace folders.</param>
    /// <returns>InitializeResult with server capabilities (textDocumentSync, completion, signatureHelp, hover, etc.).</returns>
    /// <remarks>
    /// Processes initialization options (AsmLanguageServerOptions) from client:
    ///   - Enable/disable features (AsmDoc_On, SignatureHelp_On, CodeFolding_On, etc.)
    ///   - Select architectures (ARCH_8086, ARCH_X64, ARCH_SSE, etc.)
    ///   - Configure code folding markers (#region, #endregion)
    /// 
    /// Returns ServerCapabilities with:
    ///   - TextDocumentSyncKind.Full (full document sync)
    ///   - CompletionProvider with backspace trigger
    ///   - SignatureHelpProvider with space/comma/backspace triggers
    ///   - HoverProvider enabled
    ///   - FoldingRangeProvider enabled
    ///   - SemanticTokensOptions with full delta support
    ///   - DefinitionProvider enabled (label definitions)
    ///   - CodeLensProvider enabled
    /// 
    /// Note: StreamJsonRpc automatically proxies events as notifications, so OnInitializeCompletion
    /// is called directly instead of via event to avoid interference with stdio mode.
    /// </remarks>
    /// <example>
    /// Client sends: { method: "initialize", params: { ... } }
    /// Server returns: { capabilities: { textDocumentSync: 1, completionProvider: { ... }, ... } }
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: LSP initialize, server capabilities, initialization options, protocol handshake
    /// USED IN: LanguageServer initialization, LSP protocol handshake
    /// SEE ALSO: Initialized, AsmLanguageServerOptions, ServerCapabilities
    public object Initialize(InitializeParams parameter)
    {
        LanguageServer.LogInfo($"Initialize: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");

#if DEBUG
        this.traceSetting = TraceSetting.Verbose;
#else
            traceSetting = TraceSetting.Off;
#endif

        LanguageServer.LogInfo($"Initialize: traceSetting={this.traceSetting}");

        // Parse InitializationOptions - it comes as a JsonElement when using System.Text.Json
        // IncludeFields = true is required because AsmLanguageServerOptions uses public fields, not properties
        var jsonOptionsWithFields = new System.Text.Json.JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        jsonOptionsWithFields.Converters.Add(new ColorJsonConverter());
        AsmLanguageServerOptions options;
        try
        {
            options = parameter.InitializationOptions switch
            {
                System.Text.Json.JsonElement jsonElement => System.Text.Json.JsonSerializer.Deserialize<AsmLanguageServerOptions>(jsonElement.GetRawText(), jsonOptionsWithFields),
                _ => null
            } ?? CreateDefaultOptions();
        }
        catch (Exception ex)
        {
            LanguageServer.LogError($"Initialize: Failed to deserialize InitializationOptions: {ex.Message}; using defaults");
            options = CreateDefaultOptions();
        }
        // If AsmDoc_Url was not received (e.g. older client), fall back to the default wiki URL
        if (string.IsNullOrEmpty(options.AsmDoc_Url))
        {
            options.AsmDoc_Url = "https://github.com/HJLebbink/asm-dude/wiki/";
        }
        LanguageServer.LogInfo($"Initialize: AsmDoc_On={options.AsmDoc_On}, AsmDoc_Url=\"{options.AsmDoc_Url}\", CodeCompletion_On={options.CodeCompletion_On}, ARCH_8086={options.ARCH_8086}");

        server.Initialize(options);

        string backspaceStr = (char)8 + string.Empty;
        //string carriageReturnStr = (char)13 + string.Empty;

        var result = new InitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync = new TextDocumentSyncOptions
                {
                    OpenClose = true,
                    Change = TextDocumentSyncKind.Full,
                    //TODO 30-09-23: use TextDocumentSyncKind.Incremental
                    //Change = TextDocumentSyncKind.Incremental,
                },
                CompletionProvider = new CompletionOptions
                {
                    TriggerCharacters = [backspaceStr],
                    AllCommitCharacters = ["\t"],
                    ResolveProvider = false,
                    WorkDoneProgress = false,
                },
                SignatureHelpProvider = new SignatureHelpOptions
                {
                    TriggerCharacters = [" ", ",", backspaceStr],
                    RetriggerCharacters = [";"],
                    WorkDoneProgress = false,
                },

                // enable the popups with descriptions of keywords
                HoverProvider = new HoverOptions
                {
                    WorkDoneProgress = false,
                },

                // enable the folding of line ranges
                FoldingRangeProvider = new FoldingRangeOptions
                {
                    WorkDoneProgress = false,
                },

                // enable highlighting of selected words
                DocumentHighlightProvider = new DocumentHighlightOptions
                {
                    WorkDoneProgress = false,
                },

                // enable the "Find All References (Shift+F12)" when right clicking on a keyword to find all references to this keyword
                ReferencesProvider = new ReferenceOptions
                {
                    WorkDoneProgress = false,
                },

                // enable semantic tokens for rich syntax highlighting
                SemanticTokensOptions = new SemanticTokensOptions
                {
                    Full = new SemanticTokensFullOptions { Delta = true },
                    Range = false,
                    Legend = new SemanticTokensLegend
                    {
// Token types for assembly language (LSP 3.17)
                         TokenTypes =
                         [
                             "keyword",      // 0: mnemonics (MOV, ADD, etc.)
                                 "variable",     // 1: registers (RAX, EAX, etc.)
                                 "label",        // 2: labels (loop_start:, etc.)
                                 "macro",        // 3: directives (.data, PROC, etc.)
                                 "number",       // 4: immediate values (0x10, 42, etc.)
                                 "operator",     // 5: memory operands ([rax], etc.)
                                 "comment",      // 6: comments (; this is a comment)
                                 "string",       // 7: string literals ("hello")
                                 "function",     // 8: CALL targets
                                 "decorator",    // 9: decorators/attributes (LSP 3.17)
                                 "masmDirective",  // 10: MASM-specific directives
                                 "nasmDirective",  // 11: NASM-specific directives
                                 "masmOperator",   // 12: MASM-specific operators
                                 "nasmOperator",   // 13: NASM-specific operators
                                 "masmPseudoOp",   // 14: MASM-specific pseudo-ops
                                 "nasmPseudoOp",   // 15: NASM-specific pseudo-ops
                             ],
                        // Token modifiers for additional classification
                        TokenModifiers =
                        [
                            "declaration",  // 0: label definitions
                                "definition",   // 1: procedure definitions
                                "deprecated",   // 2: deprecated instructions
                                "readonly",     // 3: immediate values
                            ],
                    },
                    WorkDoneProgress = false,
                },

                // enable inlay hints for inline annotations (LSP 3.17)
                // Shows instruction latency, memory sizes, and Z3-proven register states
                InlayHintOptions = new InlayHintOptions
                {
                    ResolveProvider = false,
                    WorkDoneProgress = false,
                },

                // enable the "X' when right clicking on a keyword to rename this keyword
                //RenameProvider = new RenameOptions
                //{
                //    PrepareProvider = true,
                //    WorkDoneProgress = false,
                //},

                // "Peek Definition (Alt+F12)"

                // Unknown what this does
                //DocumentSymbolProvider = true,

                CodeActionProvider = new CodeActionOptions()
                {
                    ResolveProvider = false,
                    CodeActionKinds = [CodeActionKind.QuickFix],
                },

                //ProjectContextProvider = true,

                //DocumentColorProvider = new DocumentColorOptions
                //{
                //   WorkDoneProgress = false,
                //},

                //DocumentFormattingProvider = new DocumentFormattingOptions
                //{
                //    WorkDoneProgress = false,
                //},
                //DocumentRangeFormattingProvider = true,


                // Enable "Go To Definition (F12)" for jumping to label definitions
                DefinitionProvider = new DefinitionOptions
                {
                    WorkDoneProgress = false,
                },

                //CustomCommands = new CustomCommandsOptions(),

                //TypeDefinitionProvider = true,

                //ImplementationProvider = true,

                CodeLensProvider = new CodeLensOptions
                {
                    ResolveProvider = true,
                    WorkDoneProgress = false,
                },

                // DocumentLink removed: VS never sends the textDocument/documentLink request.
                // Clickable doc links are handled by the in-proc MEF QuickInfo source instead.

                //// The document on type formatting request is sent from the client to the server to format parts of the document during typing.
                //DocumentOnTypeFormattingProvider = new DocumentOnTypeFormattingOptions
                //{
                //    FirstTriggerCharacter = ",",
                //    MoreTriggerCharacter = new string[] { "@" },
                //},

                //ExecuteCommandProvider = new ExecuteCommandOptions
                //{
                //    Commands = new string[] { "COMMAND_TODO" },
                //},

                //Experimental = true,

                //LinkedEditingRangeProvider = true,

                //SemanticTokensOptions = new SemanticTokensOptions
                //{
                //    Full = false,
                //    //Legend = SemanticTokensLegend.,
                //    Range = false
                //},

                //WorkspaceSymbolProvider = false
            }
        };

        // Note: OnInitializeCompletion event is not invoked here because StreamJsonRpc
        // automatically proxies events as JSON-RPC notifications, which interferes with
        // the response when using stdio mode. The event handler logic is called directly instead.
        server.OnInitializeComplete();
        LanguageServer.LogToFile($"[Initialize] capabilities sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
        return result;
    }

    [JsonRpcMethod(Methods.InitializedName, UseSingleObjectParameterDeserialization = true)]
    /// <summary>
    /// Handle initialized notification. Called after initialize completes, server is ready for requests.
    /// </summary>
    /// <param name="parameter">InitializedParams (empty in LSP spec, used for client notification).</param>
    /// <remarks>
    /// Server should be fully initialized at this point:
    ///   - MnemonicStore loaded with instruction data
    ///   - PerformanceStore loaded with CPU performance data
    ///   - All options parsed from initialization
    /// 
    /// Triggers OnInitialized event for listeners (e.g., VSIX to知道 server is ready).
    /// </remarks>
    /// <example>
    /// LSP client sends: { method: "initialized", params: {} }
    /// Server calls: server.Initialized() → loads mnemonic/performance data → triggers OnInitialized
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: LSP initialized, lifecycle, event notification, server readiness
    /// USED IN: LanguageServer initialization flow, LSP protocol handshake
    /// SEE ALSO: Initialize, OnInitialized, AsmLanguageServerOptions
    public void Initialized(InitializedParams parameter)
    {
        LanguageServer.LogInfo($"Initialized: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
        server.Initialized();
        OnInitialized?.Invoke(this, EventArgs.Empty);
    }

    [JsonRpcMethod(Methods.ProgressNotificationName, UseSingleObjectParameterDeserialization = true)]
    public void ProgressNotification(object parameter)
    {
        LanguageServer.LogInfo($"ProgressNotification");
    }

    [JsonRpcMethod(Methods.PartialResultTokenName, UseSingleObjectParameterDeserialization = true)]
    public void PartialResultToken(object parameter)
    {
        LanguageServer.LogInfo($"PartialResultToken: NOT IMPLEMENTED");
        // TODO
    }

    [JsonRpcMethod(Methods.ProgressNotificationTokenName, UseSingleObjectParameterDeserialization = true)]
    public void ProgressNotificationToken(object parameter)
    {
        LanguageServer.LogInfo($"ProgressNotificationToken: NOT IMPLEMENTED");
        // TODO
    }

    [JsonRpcMethod(Methods.TextDocumentCodeActionName, UseSingleObjectParameterDeserialization = true)]
    /// <summary>
    /// Handle textDocument/codeAction request. Returns code actions for the specified range (quick fixes/lightbulb).
    /// </summary>
    /// <param name="parameter">CodeActionParams with document URI, range, and context (e.g., diagnostics).</param>
    /// <returns>Array of CodeAction objects for VS to display in UI lightbulb menu.</returns>
    /// <remarks>
    /// Current implementation returns demo/verification actions:
    ///   - Create file: demo action for file creation
    ///   - Rename file: demo action for file renaming
    ///   - Add text: demo quick fixes with various WorkspaceEdit patterns
    ///   - Unresolved action: action with Data field (requires resolveCodeAction)
    /// 
    /// These are demonstration actions showing LSP compatibility. Real code actions should be
    /// context-aware (e.g., fix syntax errors, add missing includes).
    /// </remarks>
    /// <example>
    /// User clicks lightbulb → Client sends: { method: "textDocument/codeAction", params: { ... } }
    /// Server returns: [ { title: "Create file.txt", edit: { ... } }, ... ]
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: code actions, quick fixes, lightbulb, WorkspaceEdit, LSP
    /// USED IN: LanguageServer.GetCodeActions
    /// SEE ALSO: CodeAction, WorkspaceEdit, TextDocumentEdit, GetCodeActions
    public object TextDocumentCodeAction(CodeActionParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentCodeAction: uri={parameter.TextDocument.Uri}, range=[{parameter.Range.Start.Line}:{parameter.Range.Start.Character}-{parameter.Range.End.Line}:{parameter.Range.End.Character}]");
        var result = server.GetCodeActions(parameter);
        LanguageServer.LogInfo($"TextDocumentCodeAction: actionCount={((result as object[])?.Length ?? 0)}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentCodeLensName, UseSingleObjectParameterDeserialization = true)]
    public CodeLens[]? TextDocumentCodeLens(CodeLensParams parameter)
    {
        var result = server.GetCodeLenses(parameter);
        LanguageServer.LogInfo($"TextDocumentCodeLens: uri={parameter.TextDocument.Uri}, lensCount={result?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.CodeActionResolveName, UseSingleObjectParameterDeserialization = true)]
    public object? GetResolvedCodeAction(CodeAction parameter)
    {
        var result = server.GetResolvedCodeAction(parameter);
        LanguageServer.LogInfo($"GetResolvedCodeAction: title={parameter.Title}. result={result}");
        return result;
    }

    [JsonRpcMethod(Methods.CodeLensResolveName, UseSingleObjectParameterDeserialization = true)]
    public CodeLens? CodeLensResolve(CodeLens parameter)
    {
        var result = server.ResolveCodeLens(parameter);
        LanguageServer.LogInfo($"CodeLensResolve: title={result?.Command?.Title}");
        return result;
    }

    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: code lens, label references, assembly analysis, LSP
    /// USED IN: LanguageServer.GetCodeLensData
    /// SEE ALSO: AsmCodeLensData, GetCodeLensData

    /// <summary>
    /// Handle custom asm/codeLensData request. Returns label definitions with reference locations.
    /// </summary>
    /// <param name="parameter">CodeLensParams with document URI.</param>
    /// <returns>Array of AsmCodeLensData with label definitions and reference line numbers.</returns>
    /// <remarks>
    /// Used by CodeLens adornments to show "N references" above label definitions.
    /// Each AsmCodeLensData entry contains Label, DefinitionLine, and ReferenceLines array.
    /// </remarks>
    /// <example>
    /// Client requests: { method: "asm/codeLensData", params: { textDocument: { uri: "..." } } }
    /// Server returns: [ { label: "my_label", definitionLine: 10, referenceLines: [12, 14] } ]
    /// </example>
    [JsonRpcMethod("asm/codeLensData", UseSingleObjectParameterDeserialization = true)]
    public AsmCodeLensData[]? GetCodeLensData(CodeLensParams parameter)
    {
        var result = server.GetCodeLensData(parameter.TextDocument.Uri.ToString());
        LanguageServer.LogInfo($"GetCodeLensData: uri={parameter.TextDocument.Uri}, count={result?.Length ?? 0}");
        return result;
    }

    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: Z3 simulator, proven states, assembly analysis, LSP
    /// USED IN: LanguageServer.GetProvenStates
    /// SEE ALSO: ProvenStatesResponse, LspAsmSimulator

    /// <summary>
    /// Handle custom asm/getProvenStates request. Returns Z3-proven register states for a document range.
    /// </summary>
    /// <param name="parameter">GetProvenStatesParams with URI and optional line range.</param>
    /// <returns>ProvenStatesResponse with States array containing before/after register states.</returns>
    /// <remarks>
    /// Each ProvenLineState contains Line number, BeforeState, AfterState, ProvenBy, and Confidence.
    /// ProvenBy is always "Z3 SimpleStep" and Confidence is "complete" for fully analyzed instructions.
    /// </remarks>
    /// <example>
    /// User clicks "Show Proven States" on line 42 → returns Z3-proven register values.
    /// </example>
    [JsonRpcMethod("asm/getProvenStates", UseSingleObjectParameterDeserialization = true)]
    public ProvenStatesResponse? GetProvenStates(GetProvenStatesParams parameter)
    {
        LanguageServer.LogInfo($"GetProvenStates: uri={parameter.Uri}, lineRange={parameter.LineRange?[0]}-{parameter.LineRange?[1]}");
        var result = server.GetProvenStates(parameter);
        LanguageServer.LogInfo($"GetProvenStates: stateCount={result?.States?.Count ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
    public CompletionList? OnTextDocumentCompletion(CompletionParams parameter)
    {
        LanguageServer.LogInfo($"OnTextDocumentCompletion: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        var result = server.GetTextDocumentCompletion(parameter);
        LanguageServer.LogInfo($"OnTextDocumentCompletion: itemCount={result?.Items?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentCompletionResolveName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentCompletionResolve(CompletionItem parameter)
    {
        LanguageServer.LogInfo($"TextDocumentCompletionResolve: NOT IMPLEMENTED. label={parameter.Label}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public void OnTextDocumentOpened(DidOpenTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"OnTextDocumentOpened: uri={parameter.TextDocument.Uri}");
        Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri}");
        server.OnTextDocumentOpened(parameter);
    }

    [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public void OnTextDocumentClosed(DidCloseTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"OnTextDocumentClosed: uri={parameter.TextDocument.Uri}");
        Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri}");
        server.OnTextDocumentClosed(parameter);
    }

    [JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public void OnTextDocumentChanged(DidChangeTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"OnTextDocumentChanged: uri={parameter.TextDocument.Uri}, version={parameter.TextDocument.Version}");
        Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri}");
        server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version, parameter.TextDocument.Uri.ToString());
        server.SendDiagnostics(parameter.TextDocument.Uri.ToString());
    }

    [JsonRpcMethod(Methods.TextDocumentDidSaveName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentDidSave(DidSaveTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentDidSave: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
    public DocumentHighlight[]? GetDocumentHighlights(DocumentHighlightParams parameter, CancellationToken token)
    {
        LanguageServer.LogInfo($"GetDocumentHighlights: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");

        if (parameter.PartialResultToken != null)
        {
            // LSP spec: when partialResultToken is present, send results via $/progress and return null.
            // VS always sends a partialResultToken and only processes $/progress notifications.
            var progress = new Progress<DocumentHighlight[]>(highlights =>
            {
                _ = server.SendPartialResultAsync(parameter.PartialResultToken, highlights);
            });
            server.GetDocumentHighlights(progress, parameter.Position, parameter.TextDocument.Uri.ToString(), token);
            LanguageServer.LogInfo($"GetDocumentHighlights: Sent via $/progress");
            return null;
        }

        var result = server.GetDocumentHighlights(new Progress<DocumentHighlight[]>(_ => { }), parameter.Position, parameter.TextDocument.Uri.ToString(), token);
        LanguageServer.LogInfo($"GetDocumentHighlights: highlightCount={result?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentDocumentColorName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentDocumentColor(DocumentColorParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentDocumentColor: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullName, UseSingleObjectParameterDeserialization = true)]
    /// <summary>
    /// Handle textDocument/semanticTokens/full request. Returns full semantic token data for syntax highlighting.
    /// </summary>
    /// <param name="parameter">SemanticTokensParams with document URI.</param>
    /// <returns>SemanticTokens with delta-encoded token data; null if document not found.</returns>
    /// <remarks>
    /// Returns complete token data as:
    ///   - resultId: document version (for delta comparison)
    ///   - data: [deltaLine, deltaChar, length, tokenType, tokenModifiers] * N tokens
    /// 
    /// Token types mapped from AsmTokenType:
    ///   0: keyword (mnemonics), 1: variable (registers), 2: label (labels),
    ///   3: macro (directives), 4: number (constants), 5: operator (memory),
    ///   6: comment (remarks), 7: string, 8: function (jumps), 10-15: MASM/NASM-specific
    /// 
    /// VS uses this for rich syntax highlighting with colored tokens.
    /// </remarks>
    /// <example>
    /// Client requests: { method: "textDocument/semanticTokens/full", params: { ... } }
    /// Server returns: { resultId: "3", data: [0,0,3,0,0, 0,3,3,1,0, ...] }
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, LSP, syntax highlighting, token mapping, delta encoding
    /// USED IN: LanguageServer.GetSemanticTokens
    /// SEE ALSO: GetSemanticTokensFullSemanticTokensParams, GetSemanticTokensDelta
    public SemanticTokens? GetSemanticTokensFull(SemanticTokensParams parameter)
    {
        LanguageServer.LogInfo($"GetSemanticTokensFull: uri={parameter.TextDocument.Uri}");
        SemanticTokens? result = server.GetSemanticTokens(parameter);
        LanguageServer.LogInfo($"GetSemanticTokensFull: resultId={result?.ResultId}, tokenCount={result?.Data?.Length / 5 ?? 0}");
        return result;
    }

    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: inlay hints, performance data, hex conversion, LSP 3.17, inline annotations
    /// USED IN: LanguageServer.GetInlayHints
    /// SEE ALSO: InlayHint, GetInlayHintsInlayHint

    /// <summary>
    /// Handle textDocument/inlayHint request. Returns inline annotations for performance data and number conversions.
    /// </summary>
    /// <param name="parameter">InlayHintParams with document URI and range.</param>
    /// <returns>Array of InlayHint showing performance data (latency) and hex/decimal conversions.</returns>
    /// <remarks>
    /// Hints added for:
    ///   1. Performance: Instruction latency (e.g., "⏱5cy") beside mnemonics when PerformanceInfo_On
    ///   2. Number conversions: Decimal after hex (e.g., "=10") or hex after decimal (e.g., "=0xA") for constants
    /// 
    /// Uses InlayHintKind.Type with PaddingLeft to avoid overlapping existing text.
    /// </remarks>
    /// <example>
    /// For "mov eax, 10h":
    ///   hint at end: "=16" (decimal after hex)
    /// For "add rax, rbx" on Skylake:
    ///   hint after "add": " ⏱1cy" (latency)
    /// </example>
    [JsonRpcMethod(Methods.TextDocumentInlayHintName, UseSingleObjectParameterDeserialization = true)]
    public InlayHint[]? GetInlayHints(InlayHintParams parameter)
    {
        Console.Error.WriteLine($"DEBUG: LanguageServerTarget.GetInlayHints called! uri={parameter.TextDocument.Uri}, range=[{parameter.Range.Start.Line}:{parameter.Range.Start.Character}-{parameter.Range.End.Line}:{parameter.Range.End.Character}]");
        LanguageServer.LogInfo($"GetInlayHints: uri={parameter.TextDocument.Uri}, range=[{parameter.Range.Start.Line}:{parameter.Range.Start.Character}-{parameter.Range.End.Line}:{parameter.Range.End.Character}]");
        var result = server.GetInlayHints(parameter);
        Console.Error.WriteLine($"DEBUG: LanguageServerTarget.GetInlayHints returning {result?.Length ?? 0} hints");
        LanguageServer.LogInfo($"GetInlayHints: hintCount={result?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
    public object? GetDocumentSymbols(DocumentSymbolParams parameter)
    {
        LanguageServer.LogInfo($"GetDocumentSymbols: uri={parameter.TextDocument.Uri}");
        var result = server.GetDocumentSymbols(parameter);
        LanguageServer.LogInfo($"GetDocumentSymbols: symbolCount={((result as object[])?.Length ?? 0)}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentFoldingRangeName, UseSingleObjectParameterDeserialization = true)]
    public object? GetFoldingRanges(FoldingRangeParams parameter)
    {
        LanguageServer.LogInfo($"GetFoldingRanges: uri={parameter.TextDocument.Uri}");
        var result = server.GetFoldingRanges(parameter);
        LanguageServer.LogInfo($"GetFoldingRanges: rangeCount={((result as object[])?.Length ?? 0)}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentFormattingName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentFormatting(DocumentFormattingParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentFormatting: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: hover tooltip, VSInternalHover, classified text, style rendering, LSP
    /// USED IN: LanguageServer.GetHover
    /// SEE ALSO: OnHover, VSInternalHover, PredefinedClassificationTypeNames

    /// <summary>
    /// Handle textDocument/hover request. Returns hover tooltip for the word at cursor position.
    /// </summary>
    /// <param name="parameter">TextDocumentPositionParams with document URI and cursor position.</param>
    /// <returns>VSInternalHover with _vs_rawContent for styled text (monospace/color); null if no hover data.</returns>
    /// <remarks>
    /// VS LSP client only supports PlainText in standard hover Contents (no Markdown).
    /// Uses VS-specific VSInternalHover with ClassifiedTextElement for:
    ///   - Monospace font via "formal language" + UseClassificationFont
    ///   - Colored keyword via "keyword" classification for mnemonics
    ///   - Stacked/wrapped layout via ContainerElement
    /// 
    /// Note: Clickable links impossible over LSP (NavigationAction is an unserializable Action delegate).
    /// See VSInternalTypes.cs for full discussion.
    /// </remarks>
    /// <example>
    /// User hovers over "MOV" → Client sends: { method: "textDocument/hover", params: { ... } }
    /// Server returns: { _vs_rawContent: { ... ClassifiedTextElement with keyword + monospace text } }
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: hover tooltip, VSInternalHover, classified text, style rendering, LSP
    /// USED IN: LanguageServer.GetHover
    /// SEE ALSO: OnHover, VSInternalHover, PredefinedClassificationTypeNames
    public object? OnHover(TextDocumentPositionParams parameter)
    {
        Console.Error.WriteLine($"DEBUG: LanguageServerTarget.OnHover called! uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        LanguageServer.LogInfo($"OnHover: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        var result = server.GetHover(parameter);
        Console.Error.WriteLine($"DEBUG: LanguageServerTarget.OnHover returning: {(result != null ? "RESULT" : "NULL")}");
        LanguageServer.LogInfo($"OnHover: hasResult={result != null}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentOnTypeFormattingName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentOnTypeFormatting(DocumentOnTypeFormattingParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentOnTypeFormatting: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentPublishDiagnosticsName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentPublishDiagnostics(PublishDiagnosticParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentPublishDiagnostics: NOT IMPLEMENTED. uri={parameter.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentRangeFormattingName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentRangeFormatting(DocumentRangeFormattingParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: go to definition, label resolution, LSP, label graph, range searching
    /// USED IN: LanguageServer.GetDefinition
    /// SEE ALSO: TextDocumentPositionParams, Location, LabelGraph

    /// <summary>
    /// Handle textDocument/definition request. Returns location of label definitions (LSP "Go To Definition" / F12).
    /// </summary>
    /// <param name="parameter">TextDocumentPositionParams with document URI and cursor position.</param>
    /// <returns>Location of label definition, or null if label not found.</returns>
    /// <remarks>
    /// Algorithm:
    ///   1. Extract word at cursor position
    ///   2. First check LabelGraph if available (cached label analysis)
    ///   3. Fallback: scan document for "label:" pattern (case-insensitive)
    ///   4. Verify word boundary (start of line or after whitespace)
    /// 
    /// Most assemblers are case-insensitive for labels, so comparison uses ToUpperInvariant.
    /// </remarks>
    /// <example>
    /// Code: "my_label: mov eax, ebx"
    /// Cursor at "my_label" → returns Location with range covering "my_label"
    /// </example>
    [JsonRpcMethod(Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentDefinition(TextDocumentPositionParams parameter)
    {
        LanguageServer.LogToFile($"[TextDocumentDefinition] uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        var result = server.GetDefinition(parameter);
        LanguageServer.LogToFile($"[TextDocumentDefinition] hasResult={result != null}");
        return result;
    }

    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: find references, reference analysis, LSP, label graph
    /// USED IN: LanguageServer.SendReferences
    /// SEE ALSO: ReferenceParams, OnTextDocumentFindReferences

    /// <summary>
    /// Handle textDocument/references request. Returns locations where the symbol is referenced (LSP "Find All References" / Shift+F12).
    /// </summary>
    /// <param name="parameter">ReferenceParams with document URI and cursor position.</param>
    /// <param name="token">Cancellation token for long-running reference search.</param>
    /// <returns>Array of Location objects where the symbol is referenced.</returns>
    /// <remarks>
    /// Scans document for occurrences of the symbol at cursor position. Uses word boundary matching
    /// to ensure only complete symbol matches are returned. Implements chunked results for large files.
    /// </remarks>
    /// <example>
    /// User presses Shift+F12 on "my_label" → returns all lines where label is used.
    /// </example>
    [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
    public object[]? OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
    {
        LanguageServer.LogInfo($"OnTextDocumentFindReferences: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        var result = server.SendReferences(args: parameter, returnLocationsOnly: true, token: token);
        LanguageServer.LogInfo($"OnTextDocumentFindReferences: referenceCount={result?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentRenameName, UseSingleObjectParameterDeserialization = true)]
    public WorkspaceEdit TextDocumentRename(RenameParams renameParams)
    {
        LanguageServer.LogInfo($"TextDocumentRename: uri={renameParams.TextDocument.Uri}, line={renameParams.Position.Line}, char={renameParams.Position.Character}, newName={renameParams.NewName}");
        string fullText = File.ReadAllText(new Uri(renameParams.TextDocument.Uri.ToString()).LocalPath);
        string wordToReplace = this.GetWordAtPosition(fullText, renameParams.Position);
        Range[] placesToReplace = this.GetWordRangesInText(fullText, wordToReplace);

        var result = new WorkspaceEdit
        {
            DocumentChanges = new TextDocumentEdit[]
            {
                    new()
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = renameParams.TextDocument.Uri,
                            Version = ++this.version
                        },
                        Edits = [.. placesToReplace.Select(range =>
                            new TextEdit
                            {
                                NewText = renameParams.NewName,
                                Range = range
                            })]
                    }
            }
        };

        LanguageServer.LogInfo($"TextDocumentRename: hasResult={result.DocumentChanges != null}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentSemanticTokensRangeName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentSemanticTokensRange(SemanticTokensRangeParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentSemanticTokensRange: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullDeltaName, UseSingleObjectParameterDeserialization = true)]
    public object TextDocumentSemanticTokensFullDelta(SemanticTokensDeltaParams parameter)
    {
        var result = server.GetSemanticTokensDelta(parameter);
        if (result is SemanticTokensDelta delta)
        {
            if ((delta.Edits?.Length ?? 0) > 0)
                LanguageServer.LogInfo($"TextDocumentSemanticTokensFullDelta: uri={parameter.TextDocument.Uri}, delta edits={delta.Edits?.Length}, resultId={delta.ResultId}");
        }
        else if (result is SemanticTokens full)
            LanguageServer.LogInfo($"TextDocumentSemanticTokensFullDelta: uri={parameter.TextDocument.Uri}, full tokens, tokenCount={full.Data?.Length / 5 ?? 0}, resultId={full.ResultId}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentSignatureHelpName, UseSingleObjectParameterDeserialization = true)]
    public SignatureHelp? TextDocumentSignatureHelp(SignatureHelpParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentSignatureHelp: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        var result = server.GetTextDocumentSignatureHelp(parameter);
        LanguageServer.LogInfo($"TextDocumentSignatureHelp: signatureCount={result?.Signatures?.Length ?? 0}");
        return result;
    }

    [JsonRpcMethod(Methods.TextDocumentWillSaveName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentWillSave(WillSaveTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentWillSave: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentLinkedEditingRangeName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentLinkedEditingRange(LinkedEditingRangeParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentLinkedEditingRange: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.TextDocumentWillSaveWaitUntilName, UseSingleObjectParameterDeserialization = true)]
    public object? TextDocumentWillSaveWaitUntil(WillSaveTextDocumentParams parameter)
    {
        LanguageServer.LogInfo($"TextDocumentWillSaveWaitUntil: NOT IMPLEMENTED. uri={parameter.TextDocument.Uri}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WindowLogMessageName, UseSingleObjectParameterDeserialization = true)]
    public object? WindowLogMessage(LogMessageParams parameter)
    {
        LanguageServer.LogInfo($"WindowLogMessage: NOT IMPLEMENTED. type={parameter.MessageType}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WindowShowMessageName, UseSingleObjectParameterDeserialization = true)]
    public object? WindowShowMessage(ShowMessageParams parameter)
    {
        LanguageServer.LogInfo($"WindowShowMessage: NOT IMPLEMENTED. type={parameter.MessageType}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WindowShowMessageRequestName, UseSingleObjectParameterDeserialization = true)]
    public object? WindowShowMessageRequest(ShowMessageRequestParams parameter)
    {
        LanguageServer.LogInfo($"WindowShowMessageRequest: NOT IMPLEMENTED. type={parameter.MessageType}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WorkspaceApplyEditName, UseSingleObjectParameterDeserialization = true)]
    public object? WorkspaceApplyEdit(ApplyWorkspaceEditParams parameter)
    {
        LanguageServer.LogInfo($"WorkspaceApplyEdit: NOT IMPLEMENTED");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WorkspaceConfigurationName, UseSingleObjectParameterDeserialization = true)]
    public object? WorkspaceConfiguration(ConfigurationParams parameter)
    {
        LanguageServer.LogInfo($"WorkspaceConfiguration: NOT IMPLEMENTED");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName, UseSingleObjectParameterDeserialization = true)]
    public void OnDidChangeConfiguration(DidChangeConfigurationParams parameter)
    {
        LanguageServer.LogInfo($"OnDidChangeConfiguration");
        server.SendSettings(parameter);
    }

    [JsonRpcMethod(Methods.WorkspaceSymbolName, UseSingleObjectParameterDeserialization = true)]
    public object? WorkspaceSymbol(WorkspaceSymbolParams parameter)
    {
        LanguageServer.LogInfo($"WorkspaceSymbol: NOT IMPLEMENTED. query={parameter.Query}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.WorkspaceDidChangeWatchedFilesName, UseSingleObjectParameterDeserialization = true)]
    public object? WorkspaceDidChangeWatchedFiles(DidChangeWatchedFilesParams parameter)
    {
        LanguageServer.LogInfo($"WorkspaceDidChangeWatchedFiles: NOT IMPLEMENTED. changeCount={parameter.Changes?.Length ?? 0}");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.ShutdownName)]
    public object? Shutdown()
    {
        LanguageServer.LogInfo($"Received Shutdown notification");
        return null;
    }

    [JsonRpcMethod(Methods.ExitName)]
    public void Exit()
    {
        LanguageServer.LogInfo($"Received Exit notification");
        server.Exit();
    }

    [JsonRpcMethod(Methods.TelemetryEventName, UseSingleObjectParameterDeserialization = true)]
    public object? TelemetryEvent(object parameter)
    {
        LanguageServer.LogInfo($"TelemetryEvent: NOT IMPLEMENTED");
        // TODO
        return null;
    }

    [JsonRpcMethod(Methods.ClientUnregisterCapabilityName, UseSingleObjectParameterDeserialization = true)]
    public object? ClientUnregisterCapability(UnregistrationParams parameter)
    {
        LanguageServer.LogInfo($"ClientUnregisterCapability: NOT IMPLEMENTED");
        // TODO
        return null;
    }

    [JsonRpcMethod("textDocument/prepareRename", UseSingleObjectParameterDeserialization = true)]
    public object? PrepareRename(PrepareRenameParams parameter)
    {
        LanguageServer.LogInfo($"PrepareRename: uri={parameter.TextDocument.Uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        return null;
    }

    [JsonRpcMethod(VSMethods.GetProjectContextsName, UseSingleObjectParameterDeserialization = true)]
    public object? GetProjectContexts(VSGetProjectContextsParams parameter)
    {
        LanguageServer.LogInfo($"GetProjectContexts: uri={parameter.TextDocument.Uri}");
        var result = server.GetProjectContexts();
        LanguageServer.LogInfo($"GetProjectContexts: contextCount={result?.ProjectContexts?.Length ?? 0}");
        return result;
    }


    public Range[] GetWordRangesInText(string fullText, string word)
    {
        List<Range> ranges = [];
        string[] textLines = fullText.Split([Environment.NewLine], StringSplitOptions.None);
        for (int i = 0; i < textLines.Length; i++)
        {
            foreach (Match match in Regex.Matches(textLines[i], word).Cast<Match>())
            {
                ranges.Add(new Range
                {
                    Start = new Position(i, match.Index),
                    End = new Position(i, match.Index + match.Length)
                });
            }
        }

        return [.. ranges];
    }

    public string GetWordAtPosition(string fullText, Position position)
    {
        string[] textLines = fullText.Split([Environment.NewLine], StringSplitOptions.None);
        string textAtSpecifiedLine = textLines[position.Line];

        string currentWord = string.Empty;
        for (int i = position.Character; i < textAtSpecifiedLine.Length; i++)
        {
            if (textAtSpecifiedLine[i] == ' ')
            {
                break;
            }
            else
            {
                currentWord += textAtSpecifiedLine[i];
            }
        }

        for (int i = position.Character - 1; i > 0; i--)
        {
            if (textAtSpecifiedLine[i] == ' ')
            {
                break;
            }
            else
            {
                currentWord = textAtSpecifiedLine[i] + currentWord;
            }
        }

        return currentWord;
    }
}
