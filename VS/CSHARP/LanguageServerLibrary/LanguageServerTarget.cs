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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace LanguageServerLibrary
{
    public sealed class CompletionComparer : IComparer<CompletionItem>
    {
        public int Compare(CompletionItem x, CompletionItem y)
        {
            Contract.Requires(x != null);
            Contract.Requires(y != null);
            return string.CompareOrdinal(x.SortText, y.SortText);
        }
    }

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

        //public bool SuggestionMode
        //{
        //    get;
        //    set;
        //} = false;

        //public bool IsIncomplete
        //{
        //    get;
        //    set;
        //} = false;

        //public bool CompletionServerError
        //{
        //    get;
        //    set;
        //} = false;

        //public bool ServerCommitCharacters { get; internal set; } = true;
        //public bool ItemCommitCharacters { get; internal set; } = false;

        private void LogInfo(string message)
        {
            if (this.traceSetting == TraceSetting.Verbose) {
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
            this.server.SetDocumentHighlightsParams(20, 10);
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
                        TriggerCharacters = new string[] { },
                        AllCommitCharacters = new string[] { "\t" },
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { " ", "," },
                        RetriggerCharacters = new string[] { "," },                       
                        WorkDoneProgress = false,
                    },
                    RenameProvider = new RenameOptions
                    {
                        PrepareProvider = true,
                        WorkDoneProgress = false,
                    },
                    FoldingRangeProvider = new FoldingRangeOptions
                    {
                        WorkDoneProgress = false,
                    },
                    ReferencesProvider = new ReferenceOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DocumentHighlightProvider = new DocumentHighlightOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DocumentSymbolProvider = true,
                    CodeActionProvider = new CodeActionOptions()
                    {
                        ResolveProvider = true
                    },
                    ProjectContextProvider = true,
                    HoverProvider = new HoverOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DefinitionProvider = false,
                    TypeDefinitionProvider = false,
                    ImplementationProvider = false,
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

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            LogInfo($"OnTextDocumentOpened: Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentOpened(parameter);
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

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            LogInfo($"OnTextDocumentChanged: Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");
            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version, parameter.TextDocument.Uri);
            server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            LogInfo($"OnTextDocumentClosed: Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            LogInfo($"OnTextDocumentFindReferences: Received: {JToken.FromObject(parameter)}");
            var result = server.SendReferences(args: parameter, returnLocationsOnly: true, token: token);
            LogInfo($"OnTextDocumentFindReferences: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object GetCodeActions(JToken arg)
        {
            LogInfo($"GetCodeActions: Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = server.GetCodeActions(parameter);
            LogInfo($"GetCodeActions: Sent: {JToken.FromObject(result)}");
            return result;
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

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList OnTextDocumentCompletion(JToken arg)
        {
            LogInfo($"OnTextDocumentCompletion: Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();
            var result = this.server.GetTextDocumentCompletion(parameter);
            LogInfo($"OnTextDocumentCompletion: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp OnTextDocumentSignatureHelp(JToken arg)
        {
            LogInfo($"OnTextDocumentSignatureHelp: Received: {arg}");
            var parameter = arg.ToObject<SignatureHelpParams>();
            var result = this.server.GetTextDocumentSignatureHelp(parameter);
            LogInfo($"OnTextDocumentSignatureHelp: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            LogInfo($"OnDidChangeConfiguration: Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentImplementationName)]
        public object OnDocumentImplementation(JToken arg)
        {
            LogInfo($"OnDocumentImplementation: Received: {arg}");
            //var parameter = arg.ToObject<ImplementationParams>();
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

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit Rename(JToken arg)
        {
            LogInfo($"Rename: Received: {arg}");
            var renameParams = arg.ToObject<RenameParams>();
            string fullText = File.ReadAllText(renameParams.TextDocument.Uri.LocalPath);
            string wordToReplace = GetWordAtPosition(fullText, renameParams.Position);
            Range[] placesToReplace = GetWordRangesInText(fullText, wordToReplace);

            var result = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new TextDocumentEdit
                    {
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

        [JsonRpcMethod("textDocument/prepareRename")] // NOTE: not provided in Methods
        public object PrepareRename(JToken arg)
        {
            LogInfo($"PrepareRename: Received: {arg}");
            //var renameParams = arg.ToObject<PrepareRenameParams>();
            return null; 
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

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            LogInfo($"GetProjectContexts: Received: {arg}");
            var result = this.server.GetProjectContexts();
            LogInfo($"GetProjectContexts: Sent: {JToken.FromObject(result)}");
            return result;
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

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            LogInfo($"GetDocumentHighlights: Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, token, arg.TextDocument.Uri);
            LogInfo($"GetDocumentHighlights: Sent: {JToken.FromObject(result)}");
            return result;
        }

        public Range[] GetWordRangesInText(string fullText, string word)
        {
            List<Range> ranges = new List<Range>();
            string[] textLines = fullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < textLines.Length; i++)
            {
                foreach (Match match in Regex.Matches(textLines[i], word))
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
