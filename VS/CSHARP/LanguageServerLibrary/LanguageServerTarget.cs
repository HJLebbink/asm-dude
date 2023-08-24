using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LanguageServer
{
    public class LanguageServerTarget
    {
        private int version = 1;
        private readonly LanguageServer server;
        private int completionItemsNumberRoot = 0;
        private TraceSource traceSource;

        public LanguageServerTarget(LanguageServer server, TraceSource traceSource)
        {
            this.server = server;
            this.traceSource = traceSource;
        }

        public event EventHandler OnInitializeCompletion;

        public event EventHandler OnInitialized;

        public bool SuggestionMode
        {
            get;
            set;
        } = false;

        public bool IsIncomplete
        {
            get;
            set;
        } = false;

        public bool CompletionServerError
        {
            get;
            set;
        } = false;

        public bool ServerCommitCharacters { get; internal set; } = true;
        public bool ItemCommitCharacters { get; internal set; } = false;

        private readonly string[] ServerCommitCharacterArray = new[] { " ", "[", "]", "(", ")", ";", "." };
        private readonly string[] ItemCommitCharacterArray = new[] { " ", "[", "]", "(", ")", ";", "-" };

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");

            var capabilities = new MockServerCapabilities();
            capabilities.TextDocumentSync = new TextDocumentSyncOptions();
            capabilities.TextDocumentSync.OpenClose = true;
            capabilities.TextDocumentSync.Change = TextDocumentSyncKind.Full;
            capabilities.CompletionProvider = new CompletionOptions();
            capabilities.CompletionProvider.ResolveProvider = false;
            capabilities.CompletionProvider.TriggerCharacters = new string[] { ",", ".", "@" };
            capabilities.CompletionProvider.AllCommitCharacters = ServerCommitCharacterArray;
            capabilities.SignatureHelpProvider = new MockSignatureHelpOptions()
            {
                TriggerCharacters = new string[] { "(", "," },
                RetriggerCharacters = new string[] { ")" },
                MockSignatureHelp = true,
            };
            capabilities.RenameProvider = true;
            capabilities.FoldingRangeProvider = new FoldingRangeOptions();
            capabilities.ReferencesProvider = true;
            capabilities.DocumentHighlightProvider = true;
            capabilities.DocumentSymbolProvider = true;
            capabilities.CodeActionProvider = new CodeActionOptions() { ResolveProvider = true };
            capabilities.ProjectContextProvider = true;
            capabilities.HoverProvider = true;
            capabilities.Mock = true;
            var result = new InitializeResult();
            result.Capabilities = capabilities;

            OnInitializeCompletion?.Invoke(this, new EventArgs());

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");

            return result;
        }

        [JsonRpcMethod(Methods.InitializedName)]
        public void Initialized(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover OnHover(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var result = new Hover()
            {
                Contents = new SumType<string, MarkedString>("Mock Hover"),
                Range = new Range()
                {
                    Start = new Position(0, 0),
                    End = new Position(0, 10),
                },
            };

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");

            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");
            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version);
            server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {JToken.FromObject(parameter)}");
            var result =  server.SendReferences(parameter, returnLocationsOnly: true, token: token);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object GetCodeActions(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = server.GetCodeActions(parameter);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName)]
        public object GetResolvedCodeAction(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<CodeAction>();
            var result = server.GetResolvedCodeAction(parameter);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList OnTextDocumentCompletion(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();
            List<CompletionItem> items = new List<CompletionItem>();

            this.server.LastCompletionRequest = arg.ToString();
            var allKinds = Enum.GetValues(typeof(CompletionItemKind)) as CompletionItemKind[];
            var itemName = this.IsIncomplete ? "Incomplete" : "Item";
            for (int i = 0; i < 10; i++)
            {
                var item = new CompletionItem();
                item.Label = $"{itemName} {i + completionItemsNumberRoot}";
                item.InsertText = $"{itemName}{i + completionItemsNumberRoot}";
                item.SortText = item.Label;
                item.Kind = allKinds[(completionItemsNumberRoot + i) % allKinds.Length];
                item.Detail = $"Detail for {itemName} {i + completionItemsNumberRoot}";
                item.Documentation = $"Documentation for {itemName} {i + completionItemsNumberRoot}";
                if (ItemCommitCharacters)
                {
                    item.CommitCharacters = ItemCommitCharacterArray;
                }
                else
                {
                    item.CommitCharacters = null;
                }

                items.Add(item);
            }

            this.completionItemsNumberRoot += 10;

            // Items to test sorting, when SortText is equal items should be ordered by label
            // So the following 3 items will be sorted: B, A, C.
            // B comes first being SortText the first sorting criteria
            // Then A and C have same SortText, so they are sorted by Label coming A before C
            var cItem = new CompletionItem();
            cItem.Label = "C";
            cItem.InsertText = "C Kind";
            cItem.SortText = "2";
            cItem.Kind = 0;
            items.Add(cItem);

            var bItem = new CompletionItem();
            bItem.Label = "B";
            bItem.InsertText = "B Kind";
            bItem.SortText = "1";
            bItem.Kind = 0;
            items.Add(bItem);

            var aItem = new CompletionItem();
            aItem.Label = "A";
            aItem.InsertText = "A Kind";
            aItem.SortText = "2";
            aItem.Kind = 0;
            items.Add(aItem);

            var invalidItem = new CompletionItem();
            invalidItem.Label = "Invalid";
            invalidItem.InsertText = "Invalid Kind";
            invalidItem.SortText = "Invalid";
            invalidItem.Kind = 0;
            items.Add(invalidItem);

            var fileNames = new[] { "sample.txt", "myHeader.h", "src/Feature/MyClass.cs", "../resources/img/sample.png", "http://contoso.com/awesome/Index.razor", "http://schemas.microsoft.com/winfx/2006/xaml/file.xml" };
            for (int i = 0; i < fileNames.Length; i++)
            {
                var item = new CompletionItem();
                item.Label = fileNames[i];
                item.InsertText = fileNames[i];
                item.SortText = fileNames[i];
                item.Kind = CompletionItemKind.File;
                item.Documentation = $"Verifies whether IVsImageService provided correct icon for {fileNames[i]}";
                item.CommitCharacters = new string[] { "." };
                items.Add(item);
            }

            var list = new CompletionList()
            {
                IsIncomplete = this.IsIncomplete || parameter.Context.TriggerCharacter == "@" || (parameter.Context.TriggerKind == CompletionTriggerKind.TriggerForIncompleteCompletions && this.completionItemsNumberRoot % 50 != 0),
                Items = items.ToArray(),
            };

            this.server.IsIncomplete = false;

            if (this.CompletionServerError)
            {
                throw new InvalidOperationException("Simulated server error.");
            }

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(list)}");

            return list;
        }

        private int callCounter = 0;
        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp OnTextDocumentSignatureHelp(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");

            SignatureHelp retVal = null;
            if (callCounter < 4)
            {
                retVal = new SignatureHelp()
                {
                    ActiveParameter = callCounter % 2,
                    ActiveSignature = callCounter / 2,
                    Signatures = new SignatureInformation[]
                    {
                        new SignatureInformation()
                        {
                            Label = "foo(param1, param2)",
                            Parameters = new ParameterInformation[]
                            {
                                new ParameterInformation()
                                {
                                    Label = "param1"
                                },
                                new ParameterInformation()
                                {
                                    Label = "param2"
                                }
                            },
                        },
                        new SignatureInformation()
                        {
                            Label = "foo(param1, param2, param3)",
                            Parameters = new ParameterInformation[]
                            {
                                new ParameterInformation()
                                {
                                    Label = "param1"
                                },
                                new ParameterInformation()
                                {
                                    Label = "param2"
                                },
                                new ParameterInformation()
                                {
                                    Label = "param3"
                                }
                            },
                        }
                    },
                };
            }

            callCounter = (callCounter + 1) % 5;

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(retVal)}");

            return retVal;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received Shutdown notification");
            return null;
        }

        [JsonRpcMethod(Methods.ExitName)]
        public void Exit()
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received Exit notification");
            server.Exit();
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit Rename(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
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

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName)]
        public object GetFoldingRanges(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            return this.server.GetFoldingRanges();
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            var result = this.server.GetProjectContexts();
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName)]
        public object GetDocumentSymbols(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");
            return this.server.GetDocumentSymbols();
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, token);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
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

        public string GetText()
        {
            return string.IsNullOrWhiteSpace(this.server.CustomText) ? "custom text from language server target" : this.server.CustomText;
        }
    }
}
