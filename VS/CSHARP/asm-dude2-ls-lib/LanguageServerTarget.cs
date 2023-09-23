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
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmDude2LS
{
    public class LanguageServerTarget
    {
        private int version = 1;
        public TraceSetting traceSetting;
        private readonly LanguageServer server;
        private readonly TraceSource traceSource;

        public LanguageServerTarget(LanguageServer server, TraceSource traceSource)
        {
            this.server = server;
            this.traceSource = traceSource;
        }

        public event EventHandler OnInitializeCompletion;

        public event EventHandler OnInitialized;

        private void LogInfo(string message)
        {
            if (this.traceSetting == TraceSetting.Verbose) {
                //Console.WriteLine($"INFO {message}");
                this.traceSource.TraceEvent(TraceEventType.Information, 0, message);
            }
        }

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            LogInfo($"Initialize: Received: {arg}");
            var parameter = arg.ToObject<InitializeParams>();
            
            #if DEBUG
                this.traceSetting = TraceSetting.Verbose;
            #else
                this.traceSetting = parameter.Trace;
            #endif

            LogInfo($"Initialize: traceSetting={this.traceSetting}");

            AsmLanguageServerOptions options = (parameter.InitializationOptions as JToken).ToObject<AsmLanguageServerOptions>();

            this.server.Initialize(options);

            const char backspace = (char)8;
            string backspaceStr = backspace + string.Empty;

            var result = new InitializeResult
            {
                Capabilities = new VSServerCapabilities
                {
                    TextDocumentSync = new TextDocumentSyncOptions
                    {
                        OpenClose = true,
                        Change = TextDocumentSyncKind.Full,
                    },
                    CompletionProvider = new CompletionOptions
                    {
                        TriggerCharacters = new string[] { backspaceStr },
                        AllCommitCharacters = new string[] { "\t" },
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { " ", ",", backspaceStr, "\t" },
                        RetriggerCharacters = new string[] { "," },                       
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


                    //DefinitionProvider = true,

                    //TypeDefinitionProvider = true,

                    //ImplementationProvider = true,

                    CodeLensProvider = new CodeLensOptions
                    {
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },

                    DocumentLinkProvider = new DocumentLinkOptions
                    {
                        ResolveProvider = false,
                    },

                    // The document on type formatting request is sent from the client to the server to format parts of the document during typing.
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

            OnInitializeCompletion?.Invoke(this, EventArgs.Empty);
            LogInfo($"Initialize: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.InitializedName)]
        public void Initialized(JToken arg)
        {
            LogInfo($"Initialized: Received: {arg}");
            this.server.Initialized();
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.ProgressNotificationName)]
        public void ProgressNotification(JToken arg)
        {
            LogInfo($"ProgressNotification: Received: {arg}");
        }

        [JsonRpcMethod(Methods.PartialResultTokenName)]
        public void PartialResultToken(JToken arg)
        {
            LogInfo($"PartialResultToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.PartialResultTokenPropertyName)]
        public void PartialResultTokenProperty(JToken arg)
        {
            LogInfo($"PartialResultTokenProperty: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.WorkDoneTokenName)]
        public void WorkDoneToken(JToken arg)
        {
            LogInfo($"WorkDoneToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.ProgressNotificationTokenName)]
        public void ProgressNotificationToken(JToken arg)
        {
            LogInfo($"ProgressNotificationToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object TextDocumentCodeAction(JToken arg)
        {
            LogInfo($"TextDocumentCodeAction: Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = server.GetCodeActions(parameter);
            LogInfo($"TextDocumentCodeAction: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeLensName)]
        public object TextDocumentCodeLens(JToken arg)
        {
            LogInfo($"TextDocumentCodeLens: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            // var parameter = arg.ToObject<ImplementationParams>();
            return null;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName)]
        public object GetResolvedCodeAction(JToken arg)
        {
            LogInfo($"GetResolvedCodeAction: Received: {arg}");
            var parameter = arg.ToObject<CodeAction>();
            var result = this.server.GetResolvedCodeAction(parameter);
            LogInfo($"GetResolvedCodeAction: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeLensResolveName)]
        public object CodeLensResolve(JToken arg)
        {
            LogInfo($"CodeLensResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList OnTextDocumentCompletion(JToken arg)
        {
            LogInfo($"OnTextDocumentCompletion: Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();
            var result = this.server.GetTextDocumentCompletion(parameter);
            LogInfo($"OnTextDocumentCompletion: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionResolveName)]
        public object TextDocumentCompletionResolve(JToken arg)
        {
            LogInfo($"TextDocumentCompletionResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDefinitionName)]
        public object TextDocumentDefinition(JToken arg)
        {
            LogInfo($"TextDocumentDefinition: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            LogInfo($"OnTextDocumentOpened: Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            LogInfo($"OnTextDocumentClosed: Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            //Console.WriteLine($"OnTextDocumentChanged: Received:{arg}");
            LogInfo($"OnTextDocumentChanged: Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");

            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version, parameter.TextDocument.Uri);
            server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidSaveName)]
        public object TextDocumentDidSave(JToken arg)
        {
            LogInfo($"TextDocumentDidSave: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            LogInfo($"GetDocumentHighlights: Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, arg.TextDocument.Uri, token);
            LogInfo($"GetDocumentHighlights: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentLinkName)]
        public object TextDocumentDocumentLink(JToken arg)
        {
            LogInfo($"TextDocumentDocumentLink: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.DocumentLinkResolveName)]
        public object DocumentLinkResolve(JToken arg)
        {
            LogInfo($"DocumentLinkResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentColorName)]
        public object TextDocumentDocumentColor(JToken arg)
        {
            LogInfo($"TextDocumentDocumentColor: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName)]
        public object GetDocumentSymbols(JToken arg)
        {
            LogInfo($"GetDocumentSymbols: Received: {arg}");
            var parameters = arg.ToObject<DocumentSymbolParams>();
            var result = this.server.GetDocumentSymbols(parameters);
            LogInfo($"GetDocumentSymbols: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName)]
        public object GetFoldingRanges(JToken arg)
        {
            LogInfo($"GetFoldingRanges: Received: {arg}");
            var parameter = arg.ToObject<FoldingRangeParams>();
            var result = this.server.GetFoldingRanges(parameter);
            LogInfo($"GetFoldingRanges: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFormattingName)]
        public object TextDocumentFormatting(JToken arg)
        {
            LogInfo($"TextDocumentFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover OnHover(JToken arg)
        {
            LogInfo($"OnHover: Received: {arg}");
            var parameter = arg.ToObject<TextDocumentPositionParams>();
            var result = server.GetHover(parameter);
            LogInfo($"OnHover: Sent: {((result == null) ? "NULL" : JToken.FromObject(result))}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentOnTypeFormattingName)]
        public object TextDocumentOnTypeFormatting(JToken arg)
        {
            LogInfo($"TextDocumentOnTypeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentPublishDiagnosticsName)]
        public object TextDocumentPublishDiagnostics(JToken arg)
        {
            LogInfo($"TextDocumentPublishDiagnostics: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentRangeFormattingName)]
        public object TextDocumentRangeFormatting(JToken arg)
        {
            LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentImplementationName)]
        public object TextDocumentImplementation(JToken arg)
        {
            LogInfo($"TextDocumentImplementation: Received: {arg}");
            //var parameter = arg.ToObject<ImplementationParams>();
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentTypeDefinitionName)]
        public object TextDocumentTypeDefinition(JToken arg)
        {
            LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            LogInfo($"OnTextDocumentFindReferences: Received: {JToken.FromObject(parameter)}");
            var result = server.SendReferences(args: parameter, returnLocationsOnly: true, token: token);
            LogInfo($"OnTextDocumentFindReferences: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit TextDocumentRename(JToken arg)
        {
            LogInfo($"TextDocumentRename: Received: {arg}");
            var renameParams = arg.ToObject<RenameParams>();
            string fullText = File.ReadAllText(renameParams.TextDocument.Uri.LocalPath);
            string wordToReplace = GetWordAtPosition(fullText, renameParams.Position);
            Range[] placesToReplace = GetWordRangesInText(fullText, wordToReplace);

            var result = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new() {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = renameParams.TextDocument.Uri,
                            Version = ++version
                        },
                        Edits = placesToReplace.Select(range =>
                            new TextEdit
                            {
                                NewText = renameParams.NewName,
                                Range = range
                            }).ToArray()
                    }
                }
            };

            LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullName)]
        public object TextDocumentSemanticTokensFullName(JToken arg)
        {
            LogInfo($"TextDocumentSemanticTokensFull: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensRangeName)]
        public object TextDocumentSemanticTokensRange(JToken arg)
        {
            LogInfo($"TextDocumentSemanticTokensRange: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullDeltaName)]
        public object TextDocumentSemanticTokensFullDelta(JToken arg)
        {
            LogInfo($"TextDocumentSemanticTokensFullDelta: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp TextDocumentSignatureHelp(JToken arg)
        {
            LogInfo($"TextDocumentSignatureHelp: Received: {arg}");
            var parameter = arg.ToObject<SignatureHelpParams>();
            var result = this.server.GetTextDocumentSignatureHelp(parameter);
            LogInfo($"TextDocumentSignatureHelp: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveName)]
        public object TextDocumentWillSave(JToken arg)
        {
            LogInfo($"TextDocumentWillSave: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentLinkedEditingRangeName)]
        public object TextDocumentLinkedEditingRange(JToken arg)
        {
            LogInfo($"TextDocumentLinkedEditingRange: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveWaitUntilName)]
        public object TextDocumentWillSaveWaitUntil(JToken arg)
        {
            LogInfo($"TextDocumentWillSaveWaitUntil: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowLogMessageName)]
        public object WindowLogMessage(JToken arg)
        {
            LogInfo($"WindowLogMessage: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageName)]
        public object WindowShowMessage(JToken arg)
        {
            LogInfo($"WindowShowMessage: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageRequestName)]
        public object WindowShowMessageRequest(JToken arg)
        {
            LogInfo($"WindowShowMessageRequest: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceApplyEditName)]
        public object WorkspaceApplyEdit(JToken arg)
        {
            LogInfo($"WorkspaceApplyEdit: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceConfigurationName)]
        public object WorkspaceConfiguration(JToken arg)
        {
            LogInfo($"WorkspaceConfiguration: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            LogInfo($"OnDidChangeConfiguration: Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.WorkspaceExecuteCommandName)]
        public object WorkspaceExecuteCommand(JToken arg)
        {
            LogInfo($"WorkspaceExecuteCommand: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceSymbolName)]
        public object WorkspaceSymbol(JToken arg)
        {
            LogInfo($"WorkspaceSymbol: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeWatchedFilesName)]
        public object WorkspaceDidChangeWatchedFiles(JToken arg)
        {
            LogInfo($"WorkspaceDidChangeWatchedFiles: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
        {
            LogInfo($"Received Shutdown notification");
            return null;
        }

        [JsonRpcMethod(Methods.ExitName)]
        public void Exit()
        {
            LogInfo($"Received Exit notification");
            server.Exit();
        }

        [JsonRpcMethod(Methods.TelemetryEventName)]
        public object TelemetryEvent(JToken arg)
        {
            LogInfo($"TelemetryEvent: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.ClientUnregisterCapabilityName)]
        public object ClientUnregisterCapability(JToken arg)
        {
            LogInfo($"ClientUnregisterCapability: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }





        [JsonRpcMethod("textDocument/prepareRename")] // NOTE: not provided in Methods
        public object PrepareRename(JToken arg)
        {
            LogInfo($"PrepareRename: Received: {arg}");
            //var renameParams = arg.ToObject<PrepareRenameParams>();
            return null; 
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            LogInfo($"GetProjectContexts: Received: {arg}");
            var result = this.server.GetProjectContexts();
            LogInfo($"GetProjectContexts: Sent: {JToken.FromObject(result)}");
            return result;
        }

 
        public Range[] GetWordRangesInText(string fullText, string word)
        {
            List<Range> ranges = new();
            string[] textLines = fullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
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

            return ranges.ToArray();
        }

        public string GetWordAtPosition(string fullText, Position position)
        {
            string[] textLines = fullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
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
}
