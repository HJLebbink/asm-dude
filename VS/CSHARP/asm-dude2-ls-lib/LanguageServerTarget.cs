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

        public LanguageServerTarget(LanguageServer server)
        {
            this.server = server;
        }

        public event EventHandler OnInitializeCompletion;

        public event EventHandler OnInitialized;

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            LanguageServer.LogInfo($"Initialize: Received: {arg}");
            var parameter = arg.ToObject<InitializeParams>();
            
#if DEBUG
                this.traceSetting = TraceSetting.Verbose;
#else
                //this.traceSetting = parameter.Trace;
                this.traceSetting = TraceSetting.Off;
#endif

            LanguageServer.LogInfo($"Initialize: traceSetting={this.traceSetting}");

            AsmLanguageServerOptions options = (parameter.InitializationOptions as JToken).ToObject<AsmLanguageServerOptions>();

            this.server.Initialize(options);

            string backspaceStr = (char)8 + string.Empty;
            //string carriageReturnStr = (char)13 + string.Empty;

            var result = new InitializeResult
            {
                Capabilities = new VSServerCapabilities
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
                        TriggerCharacters = new string[] { backspaceStr },
                        AllCommitCharacters = new string[] { "\t" },
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { " ", ",", backspaceStr },
                        RetriggerCharacters = new string[] { ";" },                       
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

            OnInitializeCompletion?.Invoke(this, EventArgs.Empty);
            LanguageServer.LogInfo($"Initialize: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.InitializedName)]
        public void Initialized(JToken arg)
        {
            LanguageServer.LogInfo($"Initialized: Received: {arg}");
            this.server.Initialized();
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.ProgressNotificationName)]
        public void ProgressNotification(JToken arg)
        {
            LanguageServer.LogInfo($"ProgressNotification: Received: {arg}");
        }

        [JsonRpcMethod(Methods.PartialResultTokenName)]
        public void PartialResultToken(JToken arg)
        {
            LanguageServer.LogInfo($"PartialResultToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.PartialResultTokenPropertyName)]
        public void PartialResultTokenProperty(JToken arg)
        {
            LanguageServer.LogInfo($"PartialResultTokenProperty: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.WorkDoneTokenName)]
        public void WorkDoneToken(JToken arg)
        {
            LanguageServer.LogInfo($"WorkDoneToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.ProgressNotificationTokenName)]
        public void ProgressNotificationToken(JToken arg)
        {
            LanguageServer.LogInfo($"ProgressNotificationToken: NOT IMPLEMENTED. Received: {arg}");
            // TODO
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object TextDocumentCodeAction(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentCodeAction: Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = this.server.GetCodeActions(parameter);
            LanguageServer.LogInfo($"TextDocumentCodeAction: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeLensName)]
        public object TextDocumentCodeLens(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentCodeLens: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            // var parameter = arg.ToObject<ImplementationParams>();
            return null;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName)]
        public object GetResolvedCodeAction(JToken arg)
        {
            LanguageServer.LogInfo($"GetResolvedCodeAction: Received: {arg}");
            var parameter = arg.ToObject<CodeAction>();
            var result = this.server.GetResolvedCodeAction(parameter);
            LanguageServer.LogInfo($"GetResolvedCodeAction: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeLensResolveName)]
        public object CodeLensResolve(JToken arg)
        {
            LanguageServer.LogInfo($"CodeLensResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList OnTextDocumentCompletion(JToken arg)
        {
            LanguageServer.LogInfo($"OnTextDocumentCompletion: Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();
            var result = this.server.GetTextDocumentCompletion(parameter);
            LanguageServer.LogInfo($"OnTextDocumentCompletion: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionResolveName)]
        public object TextDocumentCompletionResolve(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentCompletionResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDefinitionName)]
        public object TextDocumentDefinition(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentDefinition: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            LanguageServer.LogInfo($"OnTextDocumentOpened: Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            this.server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            LanguageServer.LogInfo($"OnTextDocumentClosed: Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            this.server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            LanguageServer.LogInfo($"OnTextDocumentChanged: Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");
            this.server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version, parameter.TextDocument.Uri);
            this.server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidSaveName)]
        public object TextDocumentDidSave(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentDidSave: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            LanguageServer.LogInfo($"GetDocumentHighlights: Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, arg.TextDocument.Uri, token);
            LanguageServer.LogInfo($"GetDocumentHighlights: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentLinkName)]
        public object TextDocumentDocumentLink(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentDocumentLink: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.DocumentLinkResolveName)]
        public object DocumentLinkResolve(JToken arg)
        {
            LanguageServer.LogInfo($"DocumentLinkResolve: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentColorName)]
        public object TextDocumentDocumentColor(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentDocumentColor: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName)]
        public object GetDocumentSymbols(JToken arg)
        {
            LanguageServer.LogInfo($"GetDocumentSymbols: Received: {arg}");
            var parameters = arg.ToObject<DocumentSymbolParams>();
            var result = this.server.GetDocumentSymbols(parameters);
            LanguageServer.LogInfo($"GetDocumentSymbols: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName)]
        public object GetFoldingRanges(JToken arg)
        {
            LanguageServer.LogInfo($"GetFoldingRanges: Received: {arg}");
            var parameter = arg.ToObject<FoldingRangeParams>();
            var result = this.server.GetFoldingRanges(parameter);
            LanguageServer.LogInfo($"GetFoldingRanges: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFormattingName)]
        public object TextDocumentFormatting(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover OnHover(JToken arg)
        {
            LanguageServer.LogInfo($"OnHover: Received: {arg}");
            var parameter = arg.ToObject<TextDocumentPositionParams>();
            var result = this.server.GetHover(parameter);
            LanguageServer.LogInfo($"OnHover: Sent: {((result == null) ? "NULL" : JToken.FromObject(result))}");           
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentOnTypeFormattingName)]
        public object TextDocumentOnTypeFormatting(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentOnTypeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentPublishDiagnosticsName)]
        public object TextDocumentPublishDiagnostics(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentPublishDiagnostics: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentRangeFormattingName)]
        public object TextDocumentRangeFormatting(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentImplementationName)]
        public object TextDocumentImplementation(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentImplementation: Received: {arg}");
            //var parameter = arg.ToObject<ImplementationParams>();
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentTypeDefinitionName)]
        public object TextDocumentTypeDefinition(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentRangeFormatting: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            LanguageServer.LogInfo($"OnTextDocumentFindReferences: Received: {JToken.FromObject(parameter)}");
            var result = this.server.SendReferences(args: parameter, returnLocationsOnly: true, token: token);
            LanguageServer.LogInfo($"OnTextDocumentFindReferences: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit TextDocumentRename(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentRename: Received: {arg}");
            var renameParams = arg.ToObject<RenameParams>();
            string fullText = File.ReadAllText(renameParams.TextDocument.Uri.LocalPath);
            string wordToReplace = this.GetWordAtPosition(fullText, renameParams.Position);
            Range[] placesToReplace = this.GetWordRangesInText(fullText, wordToReplace);

            var result = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new() {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = renameParams.TextDocument.Uri,
                            Version = ++this.version
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

            LanguageServer.LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullName)]
        public object TextDocumentSemanticTokensFullName(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentSemanticTokensFull: NOT IMPLEMENTED. Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensRangeName)]
        public object TextDocumentSemanticTokensRange(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentSemanticTokensRange: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensFullDeltaName)]
        public object TextDocumentSemanticTokensFullDelta(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentSemanticTokensFullDelta: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp TextDocumentSignatureHelp(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentSignatureHelp: Received: {arg}");
            var parameter = arg.ToObject<SignatureHelpParams>();
            var result = this.server.GetTextDocumentSignatureHelp(parameter);
            LanguageServer.LogInfo($"TextDocumentSignatureHelp: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveName)]
        public object TextDocumentWillSave(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentWillSave: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentLinkedEditingRangeName)]
        public object TextDocumentLinkedEditingRange(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentLinkedEditingRange: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.TextDocumentWillSaveWaitUntilName)]
        public object TextDocumentWillSaveWaitUntil(JToken arg)
        {
            LanguageServer.LogInfo($"TextDocumentWillSaveWaitUntil: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowLogMessageName)]
        public object WindowLogMessage(JToken arg)
        {
            LanguageServer.LogInfo($"WindowLogMessage: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageName)]
        public object WindowShowMessage(JToken arg)
        {
            LanguageServer.LogInfo($"WindowShowMessage: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WindowShowMessageRequestName)]
        public object WindowShowMessageRequest(JToken arg)
        {
            LanguageServer.LogInfo($"WindowShowMessageRequest: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceApplyEditName)]
        public object WorkspaceApplyEdit(JToken arg)
        {
            LanguageServer.LogInfo($"WorkspaceApplyEdit: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceConfigurationName)]
        public object WorkspaceConfiguration(JToken arg)
        {
            LanguageServer.LogInfo($"WorkspaceConfiguration: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            LanguageServer.LogInfo($"OnDidChangeConfiguration: Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.WorkspaceExecuteCommandName)]
        public object WorkspaceExecuteCommand(JToken arg)
        {
            LanguageServer.LogInfo($"WorkspaceExecuteCommand: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceSymbolName)]
        public object WorkspaceSymbol(JToken arg)
        {
            LanguageServer.LogInfo($"WorkspaceSymbol: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeWatchedFilesName)]
        public object WorkspaceDidChangeWatchedFiles(JToken arg)
        {
            LanguageServer.LogInfo($"WorkspaceDidChangeWatchedFiles: NOT IMPLEMENTED.  Received: {arg}");
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
            this.server.Exit();
        }

        [JsonRpcMethod(Methods.TelemetryEventName)]
        public object TelemetryEvent(JToken arg)
        {
            LanguageServer.LogInfo($"TelemetryEvent: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }

        [JsonRpcMethod(Methods.ClientUnregisterCapabilityName)]
        public object ClientUnregisterCapability(JToken arg)
        {
            LanguageServer.LogInfo($"ClientUnregisterCapability: NOT IMPLEMENTED.  Received: {arg}");
            // TODO
            return null;
        }





        [JsonRpcMethod("textDocument/prepareRename")] // NOTE: not provided in Methods
        public object PrepareRename(JToken arg)
        {
            LanguageServer.LogInfo($"PrepareRename: Received: {arg}");
            //var renameParams = arg.ToObject<PrepareRenameParams>();
            return null; 
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            LanguageServer.LogInfo($"GetProjectContexts: Received: {arg}");
            var result = this.server.GetProjectContexts();
            LanguageServer.LogInfo($"GetProjectContexts: Sent: {JToken.FromObject(result)}");
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
