// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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

    public event EventHandler OnInitializeCompletion;

        public event EventHandler OnInitialized;

        private static AsmLanguageServerOptions CreateDefaultOptions() => new AsmLanguageServerOptions
        {
            AsmDoc_On = true,
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
            useAssemblerAutoDetect = true,
        };

        [JsonRpcMethod(Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
        public object Initialize(InitializeParams parameter)
        {
            LanguageServer.LogInfo($"Initialize: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");

#if DEBUG
            traceSetting = TraceSetting.Verbose;
#else
            traceSetting = TraceSetting.Off;
#endif

            LanguageServer.LogInfo($"Initialize: traceSetting={traceSetting}");

            // Parse InitializationOptions - it comes as a JsonElement when using System.Text.Json
            // IncludeFields = true is required because AsmLanguageServerOptions uses public fields, not properties
            var jsonOptionsWithFields = new System.Text.Json.JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            jsonOptionsWithFields.Converters.Add(new ColorJsonConverter());
            var options = parameter.InitializationOptions switch
            {
                System.Text.Json.JsonElement jsonElement => System.Text.Json.JsonSerializer.Deserialize<AsmLanguageServerOptions>(jsonElement.GetRawText(), jsonOptionsWithFields),
                _ => null
            } ?? CreateDefaultOptions();
            LanguageServer.LogInfo($"Initialize: AsmDoc_On={options.AsmDoc_On}, CodeCompletion_On={options.CodeCompletion_On}, ARCH_8086={options.ARCH_8086}");

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
                        Full = true,
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
                    // Shows instruction latency, memory sizes, and value conversions
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
                    
                    //CodeActionProvider = new CodeActionOptions()
                    //{
                    //    ResolveProvider = true
                    //},
                    
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

                    //TypeDefinitionProvider = true,

                    //ImplementationProvider = true,

                    //CodeLensProvider = new CodeLensOptions
                    //{
                    //    ResolveProvider = false,
                    //    WorkDoneProgress = false,
                    //},

                    //DocumentLinkProvider = new DocumentLinkOptions
                    //{
                    //    ResolveProvider = false,
                    //},

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
            LanguageServer.LogInfo($"Initialize: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.InitializedName, UseSingleObjectParameterDeserialization = true)]
        public void Initialized(InitializedParams parameter)
        {
            LanguageServer.LogInfo($"Initialized: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            server.Initialized();
            OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.ProgressNotificationName, UseSingleObjectParameterDeserialization = true)]
        public void ProgressNotification(object parameter)
        {
            LanguageServer.LogInfo($"ProgressNotification: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
        }

        [JsonRpcMethod(Methods.PartialResultTokenName, UseSingleObjectParameterDeserialization = true)]
        public void PartialResultToken(object parameter)
        {
            LanguageServer.LogInfo($"PartialResultToken: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
        }

        [JsonRpcMethod(Methods.ProgressNotificationTokenName, UseSingleObjectParameterDeserialization = true)]
        public void ProgressNotificationToken(object parameter)
        {
            LanguageServer.LogInfo($"ProgressNotificationToken: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentCodeAction(CodeActionParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentCodeAction: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetCodeActions(parameter);
            LanguageServer.LogInfo($"TextDocumentCodeAction: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeLensName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentCodeLens(CodeLensParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentCodeLens: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName, UseSingleObjectParameterDeserialization = true)]
        public object GetResolvedCodeAction(CodeAction parameter)
        {
            LanguageServer.LogInfo($"GetResolvedCodeAction: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetResolvedCodeAction(parameter);
            LanguageServer.LogInfo($"GetResolvedCodeAction: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeLensResolveName, UseSingleObjectParameterDeserialization = true)]
        public object CodeLensResolve(CodeLens parameter)
        {
            LanguageServer.LogInfo($"CodeLensResolve: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
        public CompletionList OnTextDocumentCompletion(CompletionParams parameter)
        {
            LanguageServer.LogInfo($"OnTextDocumentCompletion: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetTextDocumentCompletion(parameter);
            LanguageServer.LogInfo($"OnTextDocumentCompletion: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionResolveName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentCompletionResolve(CompletionItem parameter)
        {
            LanguageServer.LogInfo($"TextDocumentCompletionResolve: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentOpened(DidOpenTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"OnTextDocumentOpened: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri}");
            server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentClosed(DidCloseTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"OnTextDocumentClosed: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentChanged(DidChangeTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"OnTextDocumentChanged: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri}");
            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version, parameter.TextDocument.Uri.ToString());
            server.SendDiagnostics(parameter.TextDocument.Uri.ToString());
        }

        [JsonRpcMethod(Methods.TextDocumentDidSaveName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentDidSave(DidSaveTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentDidSave: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams parameter, CancellationToken token)
        {
            LanguageServer.LogInfo($"GetDocumentHighlights: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // Create a simple progress adapter that discards intermediate results
            // Full progress support would require JsonRpc partial result handling
            var progress = new Progress<DocumentHighlight[]>(_ => { });
            var result = server.GetDocumentHighlights(progress, parameter.Position, parameter.TextDocument.Uri.ToString(), token);
            LanguageServer.LogInfo($"GetDocumentHighlights: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentLinkName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentDocumentLink(DocumentLinkParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentDocumentLink: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.DocumentLinkResolveName, UseSingleObjectParameterDeserialization = true)]
        public object DocumentLinkResolve(DocumentLink parameter)
        {
            LanguageServer.LogInfo($"DocumentLinkResolve: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentColorName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentDocumentColor(DocumentColorParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentDocumentColor: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullName, UseSingleObjectParameterDeserialization = true)]
        public SemanticTokens GetSemanticTokensFull(SemanticTokensParams parameter)
        {
            LanguageServer.LogInfo($"GetSemanticTokensFull: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetSemanticTokens(parameter);
            LanguageServer.LogInfo($"GetSemanticTokensFull: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentInlayHintName, UseSingleObjectParameterDeserialization = true)]
        public InlayHint[] GetInlayHints(InlayHintParams parameter)
        {
            LanguageServer.LogInfo($"GetInlayHints: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetInlayHints(parameter);
            LanguageServer.LogInfo($"GetInlayHints: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
        public object GetDocumentSymbols(DocumentSymbolParams parameter)
        {
            LanguageServer.LogInfo($"GetDocumentSymbols: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetDocumentSymbols(parameter);
            LanguageServer.LogInfo($"GetDocumentSymbols: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName, UseSingleObjectParameterDeserialization = true)]
        public object GetFoldingRanges(FoldingRangeParams parameter)
        {
            LanguageServer.LogInfo($"GetFoldingRanges: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetFoldingRanges(parameter);
            LanguageServer.LogInfo($"GetFoldingRanges: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFormattingName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentFormatting(DocumentFormattingParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentFormatting: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        /// <summary>
        /// Handle hover request.
        /// Returns VSInternalHover with clickable links when URL is available, otherwise standard Hover.
        /// </summary>
        [JsonRpcMethod(Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
        public object OnHover(TextDocumentPositionParams parameter)
        {
            LanguageServer.LogInfo($"OnHover: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetHover(parameter);
            LanguageServer.LogInfo($"OnHover: Sent: {((result == null) ? "NULL" : System.Text.Json.JsonSerializer.Serialize(result))}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentOnTypeFormattingName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentOnTypeFormatting(DocumentOnTypeFormattingParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentOnTypeFormatting: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentPublishDiagnosticsName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentPublishDiagnostics(PublishDiagnosticParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentPublishDiagnostics: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentRangeFormattingName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentRangeFormatting(DocumentRangeFormattingParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentDefinition(TextDocumentPositionParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentDefinition: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetDefinition(parameter);
            LanguageServer.LogInfo($"TextDocumentDefinition: Sent: {(result != null ? System.Text.Json.JsonSerializer.Serialize(result) : "null")}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentImplementationName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentImplementation(TextDocumentPositionParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentImplementation: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentTypeDefinitionName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentTypeDefinition(TextDocumentPositionParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentTypeDefinition: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            LanguageServer.LogInfo($"OnTextDocumentFindReferences: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.SendReferences(args: parameter, returnLocationsOnly: true, token: token);
            LanguageServer.LogInfo($"OnTextDocumentFindReferences: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName, UseSingleObjectParameterDeserialization = true)]
        public WorkspaceEdit TextDocumentRename(RenameParams renameParams)
        {
            LanguageServer.LogInfo($"TextDocumentRename: Received: {System.Text.Json.JsonSerializer.Serialize(renameParams)}");
            string fullText = File.ReadAllText(new Uri(renameParams.TextDocument.Uri.ToString()).LocalPath);
            string wordToReplace = GetWordAtPosition(fullText, renameParams.Position);
            Range[] placesToReplace = GetWordRangesInText(fullText, wordToReplace);

            var result = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new()
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = renameParams.TextDocument.Uri,
                            Version = ++version
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

            LanguageServer.LogInfo($"Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensRangeName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentSemanticTokensRange(SemanticTokensRangeParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentSemanticTokensRange: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullDeltaName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentSemanticTokensFullDelta(SemanticTokensDeltaParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentSemanticTokensFullDelta: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName, UseSingleObjectParameterDeserialization = true)]
        public SignatureHelp TextDocumentSignatureHelp(SignatureHelpParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentSignatureHelp: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetTextDocumentSignatureHelp(parameter);
            LanguageServer.LogInfo($"TextDocumentSignatureHelp: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentWillSave(WillSaveTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentWillSave: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentLinkedEditingRangeName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentLinkedEditingRange(LinkedEditingRangeParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentLinkedEditingRange: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveWaitUntilName, UseSingleObjectParameterDeserialization = true)]
        public object TextDocumentWillSaveWaitUntil(WillSaveTextDocumentParams parameter)
        {
            LanguageServer.LogInfo($"TextDocumentWillSaveWaitUntil: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowLogMessageName, UseSingleObjectParameterDeserialization = true)]
        public object WindowLogMessage(LogMessageParams parameter)
        {
            LanguageServer.LogInfo($"WindowLogMessage: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageName, UseSingleObjectParameterDeserialization = true)]
        public object WindowShowMessage(ShowMessageParams parameter)
        {
            LanguageServer.LogInfo($"WindowShowMessage: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageRequestName, UseSingleObjectParameterDeserialization = true)]
        public object WindowShowMessageRequest(ShowMessageRequestParams parameter)
        {
            LanguageServer.LogInfo($"WindowShowMessageRequest: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceApplyEditName, UseSingleObjectParameterDeserialization = true)]
        public object WorkspaceApplyEdit(ApplyWorkspaceEditParams parameter)
        {
            LanguageServer.LogInfo($"WorkspaceApplyEdit: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceConfigurationName, UseSingleObjectParameterDeserialization = true)]
        public object WorkspaceConfiguration(ConfigurationParams parameter)
        {
            LanguageServer.LogInfo($"WorkspaceConfiguration: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName, UseSingleObjectParameterDeserialization = true)]
        public void OnDidChangeConfiguration(DidChangeConfigurationParams parameter)
        {
            LanguageServer.LogInfo($"OnDidChangeConfiguration: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.WorkspaceExecuteCommandName, UseSingleObjectParameterDeserialization = true)]
        public object WorkspaceExecuteCommand(ExecuteCommandParams parameter)
        {
            LanguageServer.LogInfo($"WorkspaceExecuteCommand: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceSymbolName, UseSingleObjectParameterDeserialization = true)]
        public object WorkspaceSymbol(WorkspaceSymbolParams parameter)
        {
            LanguageServer.LogInfo($"WorkspaceSymbol: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeWatchedFilesName, UseSingleObjectParameterDeserialization = true)]
        public object WorkspaceDidChangeWatchedFiles(DidChangeWatchedFilesParams parameter)
        {
            LanguageServer.LogInfo($"WorkspaceDidChangeWatchedFiles: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
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
        public object TelemetryEvent(object parameter)
        {
            LanguageServer.LogInfo($"TelemetryEvent: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.ClientUnregisterCapabilityName, UseSingleObjectParameterDeserialization = true)]
        public object ClientUnregisterCapability(UnregistrationParams parameter)
        {
            LanguageServer.LogInfo($"ClientUnregisterCapability: NOT IMPLEMENTED. Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            // TODO
            return null;
        }

        [JsonRpcMethod("textDocument/prepareRename", UseSingleObjectParameterDeserialization = true)]
        public object PrepareRename(PrepareRenameParams parameter)
        {
            LanguageServer.LogInfo($"PrepareRename: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            return null;
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName, UseSingleObjectParameterDeserialization = true)]
        public object GetProjectContexts(VSGetProjectContextsParams parameter)
        {
            LanguageServer.LogInfo($"GetProjectContexts: Received: {System.Text.Json.JsonSerializer.Serialize(parameter)}");
            var result = server.GetProjectContexts();
            LanguageServer.LogInfo($"GetProjectContexts: Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");
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
