using Microsoft.VisualStudio.LanguageServer.Protocol;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace LanguageServer
{
    public class LanguageServerTarget
    {
        private int version = 1;
        private readonly LanguageServer server;
        private int completionItemsNumberRoot = 0;
        private readonly TraceSource traceSource;

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

        private readonly string[] ServerCommitCharacterArray = new[] { " ", "[", "]", ";" };
        private readonly string[] ItemCommitCharacterArray = new[] { " ", "[", "]", "(", ")", ";", "-" };

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");

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
                        TriggerCharacters = new string[] { ",", ".", "@" },
                        AllCommitCharacters = ServerCommitCharacterArray,
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { "(", "," },
                        RetriggerCharacters = new string[] { ")" },
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

            string msg = "12: ";

            //https://github.com/JohnLouderback/RadLang/blob/124166195cd62180ea35aca5da775df1eb05e743/RadLanguageServerV2/LanguageRPCServer.cs#L329
            //var parameter = arg.ToObject<TextDocumentPositionParams>()!;
            //var result =
            //  await server.GetHandler<TextDocumentPositionParams, Hover?>(Methods.TextDocumentHoverName)(
            //      parameter
            //    );

            int line = 0;
            int character = 0;

            TextDocumentPositionParams x = arg.ToObject<TextDocumentPositionParams>();
            if (x == null)
            {
                msg += $"arg {arg}";
            }
            else
            {
                line = x.Position.Line;
                character = x.Position.Character;
                msg += $"arg {arg}; Position.Line {line}; Position.Character {character}; TextDocument {x.TextDocument}";
            }

            var result = new Hover()
            {
                Contents = new SumType<string, MarkedString>(msg),
                Range = new Range()
                {
                    Start = new Position(line, character),
                    End = new Position(line, character+10),
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


            if (false)
            {

#pragma warning disable CS0162 // Unreachable code detected
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
#pragma warning restore CS0162 // Unreachable code detected
                this.completionItemsNumberRoot += 10;
            }
        

            // Items to test sorting, when SortText is equal items should be ordered by label
            // So the following 3 items will be sorted: B, A, C.
            // B comes first being SortText the first sorting criteria
            // Then A and C have same SortText, so they are sorted by Label coming A before C
            {
                items.Add(new CompletionItem
                {
                    Kind = CompletionItemKind.Keyword,
                    Label = "VPXORD",
                    InsertText = "VPXORD",
                    SortText = "VPXORD",
                    //Detail = "detail text A",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = "# Header\nSome text 2\n```typescript\nsomeCode2();\n```",
                        //Value = "Documentation: some **text** here, and __here__ much texts here and here and \nhere and here and here",
                    },
                });
            }
            {
                var item = new CompletionItem();
                item.Label = "VPXORQ";
                item.InsertText = item.Label;
                item.SortText = item.Label;
                item.Kind = CompletionItemKind.Keyword;
                item.Documentation = "Documentation: some text here B";
                item.Detail = "Detail: B";
                items.Add(item);
            }
            
            if (false)
            {
#pragma warning disable CS0162 // Unreachable code detected
                var fileNames = new[] { "sample.txt", "myHeader.h", "src/Feature/MyClass.cs", "../resources/img/sample.png", "http://contoso.com/awesome/Index.razor", "http://schemas.microsoft.com/winfx/2006/xaml/file.xml" };
                for (int i = 0; i < fileNames.Length; i++)
                {
                    var item = new CompletionItem();
                    item.Label = fileNames[i];
                    item.InsertText = fileNames[i];
                    item.SortText = fileNames[i];
                    item.Kind = CompletionItemKind.File;
                    item.Detail = "detail: bla";
                    item.Documentation = $"Documentation: Verifies whether IVsImageService provided correct icon for {fileNames[i]}";
                    item.CommitCharacters = new string[] { "." };
                    items.Add(item);
                }
#pragma warning restore CS0162 // Unreachable code detected
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





        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp OnTextDocumentSignatureHelp(JToken arg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {arg}");

            string msg = "1:";
            TextDocumentPositionParams x = arg.ToObject<SignatureHelpParams>();
            if (x == null)
            {
                msg += $"x=null; arg={arg}";
            }
            else
            {
                var line = x.Position.Line;
                var character = x.Position.Character;
                msg += $"Position.Line {line}; Position.Character {character}; TextDocument {x.TextDocument}";
            }



            // 1 find the keyword
            // 2 find the parameter (that is, count the comma's)
            // 3 select from the list of signatures, the ones that are applicable


            SignatureHelp retVal = new SignatureHelp()
            {
                ActiveParameter = 1,
                ActiveSignature = 1,
                Signatures = new SignatureInformation[]
                {
                    new SignatureInformation()
                    {
                        Label = msg,
                        Parameters = new ParameterInformation[]
                        {
                            new ParameterInformation()
                            {
                                Label = "param1",
                                Documentation = "documentation here A",
                            },
                            new ParameterInformation()
                            {
                                Label = "param2",
                                Documentation = "documentation here B",
                            }
                        },
                        Documentation = "documentation here C",
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


        #region logging copied from AsmDudeToolsStatic
        /*
        public static async Task<IVsOutputWindowPane> GetOutputPaneAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsOutputWindow outputWindow = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5142");
                outputWindow.CreatePane(paneGuid, "AsmDude2", 1, 0);
                outputWindow.GetPane(paneGuid, out IVsOutputWindowPane pane);
                return pane;
            }
        }

        public static async System.Threading.Tasks.Task OutputAsync(string msg)
        {
            Contract.Requires(msg != null);

            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);
            IVsOutputWindowPane outputPane = await GetOutputPaneAsync().ConfigureAwait(true);
            if (outputPane == null)
            {
                Debug.Write(msg2);
            }
            else
            {
                outputPane.OutputString(msg2);
                outputPane.Activate();
            }
        }
        */
        #endregion
    }
}
