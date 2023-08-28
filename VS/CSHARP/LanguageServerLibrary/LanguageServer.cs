using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LanguageServer
{
    public class LanguageServer : INotifyPropertyChanged
    {
        private int maxProblems = -1;
        private readonly JsonRpc rpc;
        private readonly HeaderDelimitedMessageHandler messageHandler;
        private readonly LanguageServerTarget target;
        private readonly ManualResetEvent disconnectEvent = new ManualResetEvent(false);
        private List<DiagnosticsInfo> diagnostics;
        private TextDocumentItem textDocument = null;
        private string referenceToFind;
        private int referencesChunkSize;
        private int referencesDelay;
        private int highlightChunkSize;
        private int highlightsDelay;
        private readonly Dictionary<VSTextDocumentIdentifier, int> diagnosticsResults;

        public LanguageServer(Stream sender, Stream reader, List<DiagnosticsInfo> initialDiagnostics = null)
        {
            var jsonRpcTraceSource = LogUtils.CreateTraceSource();
            this.target = new LanguageServerTarget(this, jsonRpcTraceSource);
            this.messageHandler = new HeaderDelimitedMessageHandler(sender, reader);
            this.rpc = new JsonRpc(this.messageHandler, this.target);
            this.rpc.Disconnected += OnRpcDisconnected;

            this.rpc.ActivityTracingStrategy = new CorrelationManagerTracingStrategy()
            {
                TraceSource = jsonRpcTraceSource,
            };
            this.rpc.TraceSource = jsonRpcTraceSource;

            ((JsonMessageFormatter)this.messageHandler.Formatter).JsonSerializer.Converters.Add(new VSExtensionConverter<TextDocumentIdentifier, VSTextDocumentIdentifier>());

            this.rpc.StartListening();

            this.diagnostics = initialDiagnostics;
            this.diagnosticsResults = new Dictionary<VSTextDocumentIdentifier, int>();

            this.FoldingRanges = Array.Empty<FoldingRange>();
            this.Symbols = Array.Empty<VSSymbolInformation>();

            this.target.OnInitializeCompletion += OnTargetInitializeCompletion;
            this.target.OnInitialized += OnTargetInitialized;
        }

        public string CustomText
        {
            get;
            set;
        }

        public bool IsIncomplete
        {
            get
            {
                return this.target.IsIncomplete;
            }
            set
            {
                if (this.target.IsIncomplete != value)
                {
                    this.target.IsIncomplete = value;
                    NotifyPropertyChanged(nameof(IsIncomplete));
                }
            }
        }

        public bool CompletionServerError
        {
            get
            {
                return this.target.CompletionServerError;
            }
            set
            {
                this.target.CompletionServerError = value;
            }
        }

        public bool ItemCommitCharacters
        {
            get
            {
                return this.target.ItemCommitCharacters;
            }
            set
            {
                this.target.ItemCommitCharacters = value;
            }
        }

        public string CurrentSettings
        {
            get; private set;
        }

        public JsonRpc Rpc
        {
            get => this.rpc;
        }

        public IEnumerable<FoldingRange> FoldingRanges
        {
            get;
            set;
        }

        public IEnumerable<VSSymbolInformation> Symbols
        {
            get;
            set;
        }

        public IEnumerable<VSProjectContext> Contexts
        {
            get;
            set;
        } = Array.Empty<VSProjectContext>();

        public bool UsePublishModelDiagnostic { get; set; } = true;

        private string lastCompletionRequest = string.Empty;
        public string LastCompletionRequest
        {
            get => this.lastCompletionRequest;
            set
            {
                this.lastCompletionRequest = value ?? string.Empty;
                NotifyPropertyChanged(nameof(LastCompletionRequest));
            }
        }

        public event EventHandler OnInitialized;
        public event EventHandler Disconnected;
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnTargetInitializeCompletion(object sender, EventArgs e)
        {
            var timer = new Timer(LogMessage, null, 0, 5 * 1000);
        }

        private void OnTargetInitialized(object sender, EventArgs e)
        {
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        public void OnTextDocumentOpened(DidOpenTextDocumentParams messageParams)
        {
            this.textDocument = messageParams.TextDocument;
            // following line gives an error in LSPClient.AsmDude2.AsmLanguageClient
            //this.ShowMessage("text document is set to something!!", MessageType.Info); 
            SendDiagnostics();
        }

        public void OnTextDocumentClosed(DidCloseTextDocumentParams messageParams)
        {
            this.textDocument = null;
        }

        public void SendDiagnostics(List<DiagnosticsInfo> sentDiagnostics, bool pushDiagnostics)
        {
            if (this.diagnostics == null)
            {
                this.diagnostics = new List<DiagnosticsInfo>();
            }

            this.diagnostics = sentDiagnostics;

            if (pushDiagnostics)
            {
                SendDiagnostics(sentDiagnostics);
            }
        }

        public void SetFindReferencesParams(string wordToFind, int chunkSize, int delay = 0)
        {
            this.referenceToFind = wordToFind;
            this.referencesChunkSize = chunkSize;
            this.referencesDelay = delay;
        }

        public void SetDocumentHighlightsParams(int chunkSize, int delay = 0)
        {
            this.highlightChunkSize = chunkSize;
            this.highlightsDelay = delay * 1000;
        }

        public void UpdateServerSideTextDocument(string text, int version)
        {
            if (this.textDocument != null)
            {
                this.textDocument.Text = text;
                this.textDocument.Version = version;

                var x = new FoldingRange[] { new FoldingRange
                {
                    StartLine = 10,
                    StartCharacter = 0,
                    EndLine  = 15,
                    EndCharacter = 0,
                    Kind = FoldingRangeKind.Region,
                    
                } };

                this.SetFoldingRanges(x);
            }
        }

        public void SendDiagnostics()
        {
            SendDiagnostics(this.diagnostics);
        }

        public void SendDiagnostics(List<DiagnosticsInfo> sentDiagnostics)
        {
            if (this.textDocument == null || sentDiagnostics == null || !this.UsePublishModelDiagnostic)
            {
                return;
            }

            string[] lines = this.textDocument.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            List<Diagnostic> diagnostics = new List<Diagnostic>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                int j = 0;
                while (j < line.Length)
                {
                    Diagnostic diagnostic = null;
                    foreach (var tag in sentDiagnostics)
                    {
                        diagnostic = this.GetDiagnostic(line, i, ref j, tag, textDocumentIdentifier: null);

                        if (diagnostic != null)
                        {
                            break;
                        }
                    }

                    if (diagnostic == null)
                    {
                        ++j;
                    }
                    else
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
            }

            PublishDiagnosticParams parameter = new PublishDiagnosticParams();
            parameter.Uri = textDocument.Uri;
            parameter.Diagnostics = diagnostics.ToArray();

            if (this.maxProblems > -1)
            {
                parameter.Diagnostics = parameter.Diagnostics.Take(this.maxProblems).ToArray();
            }

            _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnostics, parameter);
        }

        public void SendDiagnostics(Uri uri)
        {
            if (this.diagnostics == null || !this.UsePublishModelDiagnostic)
            {
                return;
            }

            string[] lines = this.textDocument.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            IReadOnlyList<Diagnostic> diagnostics = GetDocumentDiagnostics(lines, textDocumentIdentifier: null);

            PublishDiagnosticParams parameter = new PublishDiagnosticParams();
            parameter.Uri = uri;
            parameter.Diagnostics = diagnostics.ToArray();

            if (this.maxProblems > -1)
            {
                parameter.Diagnostics = parameter.Diagnostics.Take(this.maxProblems).ToArray();
            }

            _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnostics, parameter);
        }

        private IReadOnlyList<VSDiagnostic> GetDocumentDiagnostics(string[] lines, TextDocumentIdentifier textDocumentIdentifier)
        {
            var diagnostics = new List<VSDiagnostic>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                int j = 0;
                while (j < line.Length)
                {
                    VSDiagnostic diagnostic = null;
                    foreach (var tag in this.diagnostics)
                    {
                        diagnostic = GetDiagnostic(line, i, ref j, tag, textDocumentIdentifier);

                        if (diagnostic != null)
                        {
                            break;
                        }
                    }

                    if (diagnostic == null)
                    {
                        ++j;
                    }
                    else
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
            }

            return diagnostics;
        }

        public CodeAction GetResolvedCodeAction(CodeAction parameter)
        {
            var token = JObject.FromObject(parameter.Data);
            var resolvedCodeAction = token.ToObject<CodeAction>();

            return resolvedCodeAction;
        }

        public object[] GetCodeActions(CodeActionParams parameter)
        {
            #region File Operation actions

            Uri documentUri = parameter.TextDocument.Uri;
            string absolutePath = documentUri.LocalPath.TrimStart('/');
            string documentFilePath = Path.GetFullPath(absolutePath);
            string documentDirectory = Path.GetDirectoryName(documentFilePath);
            string documentNameNoExtension = Path.GetFileNameWithoutExtension(documentFilePath);
            string createFilePath = Path.Combine(documentDirectory, documentNameNoExtension + ".txt");

            Uri createFileUri = new UriBuilder()
            {
                Path = createFilePath,
                Host = string.Empty,
                Scheme = Uri.UriSchemeFile,
            }.Uri;

            CodeAction createFileAction = new CodeAction
            {
                Title = "Create <TheCurrentFile>.txt",
                Edit = new WorkspaceEdit
                {
                    DocumentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
                    {
                        new CreateFile()
                        {
                            Uri = createFileUri,
                            Options = new CreateFileOptions()
                            {
                                Overwrite = true,
                            }
                        },
                    }
                },
            };

            string renameNewFilePath = Path.Combine(documentDirectory, documentNameNoExtension + "_Renamed.txt");

            Uri renameNewFileUri = new UriBuilder()
            {
                Path = renameNewFilePath,
                Host = string.Empty,
                Scheme = Uri.UriSchemeFile,
            }.Uri;

            CodeAction renameFileAction = new CodeAction
            {
                Title = "Rename <TheCurrentFile>.txt to <TheCurrentFile>_Renamed.txt",
                Edit = new WorkspaceEdit
                {
                    DocumentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
                    {
                        new RenameFile()
                        {
                            OldUri = createFileUri,
                            NewUri = renameNewFileUri,
                            Options = new RenameFileOptions()
                            {
                                Overwrite = true,
                            }
                        },
                    }
                },
            };

            #endregion

            #region Add Text actions
            TextEdit[] addTextEdit = new TextEdit[]
            {
                new TextEdit
                {
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = 0,
                            Character = 0
                        },
                        End = new Position
                        {
                            Line = 0,
                            Character = 0
                        }
                    },
                    NewText = "Added text!"
                }
            };

            CodeAction addTextAction = new CodeAction
            {
                Title = "Add Text Action - DocumentChanges property",
                Edit = new WorkspaceEdit
                {
                    DocumentChanges = new TextDocumentEdit[]
                        {
                            new TextDocumentEdit()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = parameter.TextDocument.Uri,
                                },
                                Edits = addTextEdit,
                            },
                        }
                },
                Kind = CodeActionKind.QuickFix,
            };

            Dictionary<string, TextEdit[]> changes = new Dictionary<string, TextEdit[]>();
            changes.Add(parameter.TextDocument.Uri.AbsoluteUri, addTextEdit);

            CodeAction addTextActionChangesProperty = new CodeAction
            {
                Title = "Add Text Action - Changes property",
                Edit = new WorkspaceEdit
                {
                    Changes = changes,
                },
                Kind = CodeActionKind.QuickFix,
            };

            CodeAction addUnderscoreAction = new CodeAction
            {
                Title = "Add _",
                Edit = new WorkspaceEdit
                {
                    DocumentChanges = new TextDocumentEdit[]
                        {
                            new TextDocumentEdit()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = parameter.TextDocument.Uri,
                                },
                                Edits = new TextEdit[]
                                    {
                                        new TextEdit
                                        {
                                            Range = new Range
                                            {
                                                Start = new Position
                                                {
                                                    Line = 0,
                                                    Character = 0
                                                },
                                                End = new Position
                                                {
                                                    Line = 0,
                                                    Character = 0
                                                }
                                            },
                                            NewText = "_"
                                        }
                                    }
                            },
                        }
                },
                Kind = CodeActionKind.QuickFix,
            };

            CodeAction addTextActionWithError = new CodeAction
            {
                Title = "Add Text Action - with error diagnostic",
                Edit = new WorkspaceEdit
                {
                    Changes = changes,
                },
                Diagnostics = new Diagnostic[]
                {
                    new Diagnostic()
                    {
                        Range = new Range
                        {
                            Start = new Position
                            {
                                Line = 0,
                                Character = 0
                            },
                            End = new Position
                            {
                                Line = 0,
                                Character = 0
                            }
                        },
                        Message = "Test Error",
                        Severity = DiagnosticSeverity.Error,
                    }
                },
                Kind = CodeActionKind.QuickFix,
            };

            string editFilePath = Path.Combine(documentDirectory, documentNameNoExtension + "2.foo");
            CodeAction addTextActionToOtherFile = new CodeAction
            {
                Title = "Add Text Action - Edit on different file",
                Edit = new WorkspaceEdit
                {
                    DocumentChanges = new TextDocumentEdit[]
                        {
                            new TextDocumentEdit()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = new Uri(editFilePath),
                                },
                                Edits = addTextEdit,
                            },
                        }
                },
                Kind = CodeActionKind.QuickFix,
            };
            #endregion

            #region Unresolved actions
            var unresolvedAddText = new CodeAction
            {
                Title = "Unresolved Add Text Action",
                Data = addTextAction,
            };
            #endregion

            return new CodeAction[] {
                addTextAction,
                addTextActionChangesProperty,
                addTextActionWithError,
                addTextActionToOtherFile,
                unresolvedAddText,
                addUnderscoreAction,
                createFileAction,
                renameFileAction,
            };
        }

        public object[] SendReferences(ReferenceParams args, bool returnLocationsOnly, CancellationToken token)
        {
            this.rpc.TraceSource.TraceEvent(TraceEventType.Information, 0, $"Received: {JToken.FromObject(args)}");

            IProgress<object[]> progress = args.PartialResultToken;
            int delay = this.referencesDelay * 1000;

            // Set default values if no custom values are set
            if (referencesChunkSize <= 0 || string.IsNullOrEmpty(referenceToFind))
            {
                this.referenceToFind = "error";
                this.referencesChunkSize = 1;
            }

            string referenceWord = this.referenceToFind;

            if (this.textDocument == null || progress == null)
            {
                return Array.Empty<object>();
            }

            string[] lines = this.textDocument.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            List<Location> locations = new List<Location>();

            List<Location> locationsChunk = new List<Location>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                for (int j = 0; j < line.Length; j++)
                {
                    var location = GetLocation(line, i, ref j, referenceWord);

                    if (location != null)
                    {
                        locations.Add(location);
                        locationsChunk.Add(location);

                        if (locationsChunk.Count == this.referencesChunkSize)
                        {
                            Debug.WriteLine($"Reporting references of {referenceWord}");
                            this.rpc.TraceSource.TraceEvent(TraceEventType.Information, 0, $"Report: {JToken.FromObject(locationsChunk)}");
                            progress.Report(locationsChunk.ToArray());
                            Thread.Sleep(delay);  // Wait between chunks
                            locationsChunk.Clear();
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        Debug.WriteLine($"Cancellation Requested for {referenceWord} references");
                    }

                    token.ThrowIfCancellationRequested();
                }
            }

            // Report last chunk if it has elements since it didn't reached the specified size
            if (locationsChunk.Count() > 0)
            {
                progress.Report(locationsChunk.ToArray());
                Thread.Sleep(delay);  // Wait between chunks
            }
            
            return locations.ToArray();
        }

        public void SetFoldingRanges(IEnumerable<FoldingRange> foldingRanges)
        {
            this.FoldingRanges = foldingRanges;
        }

        public FoldingRange[] GetFoldingRanges()
        {
            return this.FoldingRanges.ToArray();
        }

        public DocumentHighlight[] GetDocumentHighlights(IProgress<DocumentHighlight[]> progress, Position position, CancellationToken token)
        {
            if (this.textDocument == null || progress == null)
            {
                return Array.Empty<DocumentHighlight>();
            }

            int delay = this.highlightsDelay;
            string[] lines = this.textDocument.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            // Default to "error" if no word is selected
            var currentHighlightedWord = this.GetWordAtPosition(position, lines);
            if (string.IsNullOrEmpty(currentHighlightedWord))
            {
                currentHighlightedWord = "error";
            }

            this.highlightChunkSize = Math.Max(this.highlightChunkSize, 1);

            List<DocumentHighlight> highlights = new List<DocumentHighlight>();
            List<DocumentHighlight> chunk = new List<DocumentHighlight>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                for (int j = 0; j < line.Length; j++)
                {
                    Range range = GetHighlightRange(line, i, ref j, currentHighlightedWord);

                    if (range != null)
                    {
                        var highlight = new DocumentHighlight() { Range = range, Kind = DocumentHighlightKind.Text };
                        highlights.Add(highlight);
                        chunk.Add(highlight);

                        if (chunk.Count == highlightChunkSize)
                        {
                            progress.Report(chunk.ToArray());
                            Thread.Sleep(delay);  // Wait between chunks
                            chunk.Clear();
                        }
                    }

                    token.ThrowIfCancellationRequested();
                }
            }

            // Report last chunk if it has elements since it didn't reached the specified size
            if (chunk.Count() > 0)
            {
                progress.Report(chunk.ToArray());
            }

            return highlights.ToArray();
        }

        public void SetDocumentSymbols(IEnumerable<VSSymbolInformation> symbolsInfo)
        {
            if (this.textDocument == null)
            {
                this.Symbols = Array.Empty<VSSymbolInformation>();
                return;
            }

            string[] lines = this.textDocument.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                for (int j = 0; j < line.Length; j++)
                {

                    foreach (var symbolInfo in symbolsInfo)
                    {
                        Location loc = null;

                        loc = GetLocation(line, i, ref j, symbolInfo.Name);

                        if (loc != null)
                        {
                            symbolInfo.Location = loc;
                        }
                    }

                }
            }

            this.Symbols = symbolsInfo;
        }

        public VSSymbolInformation[] GetDocumentSymbols()
        {
            return this.Symbols.ToArray();
        }

        public void SetProjectContexts(IEnumerable<VSProjectContext> contexts)
        {
            this.Contexts = contexts;
        }

        public VSProjectContextList GetProjectContexts()
        {
            var result = new VSProjectContextList();
            result.ProjectContexts = this.Contexts.ToArray();
            result.DefaultIndex = 0;

            return result;
        }

        #region Logging

        public void LogMessage(object arg)
        {
            this.LogMessage(arg, MessageType.Info);
        }

        public void LogMessage(object arg, MessageType messageType)
        {
            this.LogMessage(arg, "BLAH", messageType);
        }

        public void LogMessage(object arg, string message, MessageType messageType)
        {
            LogMessageParams parameter = new LogMessageParams
            {
                Message = message,
                MessageType = messageType
            };
            _ = this.SendMethodNotificationAsync(Methods.WindowLogMessage, parameter);
        }

        public void ShowMessage(string message, MessageType messageType)
        {
            ShowMessageParams parameter = new ShowMessageParams
            {
                Message = message,
                MessageType = messageType
            };
            _ = this.SendMethodNotificationAsync(Methods.WindowShowMessage, parameter);
        }

        public async Task<MessageActionItem> ShowMessageRequestAsync(string message, MessageType messageType, string[] actionItems)
        {
            ShowMessageRequestParams parameter = new ShowMessageRequestParams
            {
                Message = message,
                MessageType = messageType,
                Actions = actionItems.Select(a => new MessageActionItem { Title = a }).ToArray()
            };

            return await this.SendMethodRequestAsync(Methods.WindowShowMessageRequest, parameter);
        }

        #endregion

        public void SendSettings(DidChangeConfigurationParams parameter)
        {
            this.CurrentSettings = parameter.Settings.ToString();
            this.NotifyPropertyChanged(nameof(CurrentSettings));

            JToken parsedSettings = JToken.Parse(this.CurrentSettings);
            int newMaxProblems = parsedSettings.Children().First().Values<int>("maxNumberOfProblems").First();
            if (this.maxProblems != newMaxProblems)
            {
                this.maxProblems = newMaxProblems;
                this.SendDiagnostics();
            }
        }

        public void WaitForExit()
        {
            this.disconnectEvent.WaitOne();
        }

        public void Exit()
        {
            this.disconnectEvent.Set();

            Disconnected?.Invoke(this, new EventArgs());
        }

        public void ApplyTextEdit(string text)
        {
            if (this.textDocument == null)
            {
                return;

            }
            TextEdit[] addTextEdit = new TextEdit[]
            {
                new TextEdit
                {
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = 0,
                            Character = 0
                        },
                        End = new Position
                        {
                            Line = 0,
                            Character = 0
                        }
                    },
                    NewText = text,
                }
            };

            ApplyWorkspaceEditParams parameter = new ApplyWorkspaceEditParams()
            {
                Label = "Test Edit",
                Edit = new WorkspaceEdit()
                {
                    DocumentChanges = new TextDocumentEdit[]
                        {
                            new TextDocumentEdit()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = this.textDocument.Uri,
                                },
                                Edits = addTextEdit,
                            },
                        }
                }
            };

            _ = Task.Run(async () =>
            {
                var response = await this.SendMethodRequestAsync(Methods.WorkspaceApplyEdit, parameter);

                if (!response.Applied)
                {
                    Console.WriteLine($"Failed to apply edit: {response.FailureReason}");
                }
            });
        }

        private VSDiagnostic GetDiagnostic(
            string line,
            int lineOffset,
            ref int characterOffset,
            DiagnosticsInfo diagnosticInfo,
            TextDocumentIdentifier textDocumentIdentifier = null)
        {
            string wordToMatch = diagnosticInfo.Text;
            VSProjectContext context = diagnosticInfo.Context;
            VSProjectContext requestedContext = null;

            VSDiagnosticProjectInformation projectAndContext = null;
            if (textDocumentIdentifier != null
                && textDocumentIdentifier is VSTextDocumentIdentifier vsTextDocumentIdentifier
                && vsTextDocumentIdentifier.ProjectContext != null)
            {
                requestedContext = vsTextDocumentIdentifier.ProjectContext;
                projectAndContext = new VSDiagnosticProjectInformation
                {
                    ProjectName = vsTextDocumentIdentifier.ProjectContext.Label,
                    ProjectIdentifier = vsTextDocumentIdentifier.ProjectContext.Id,
                    Context = "Win32"
                };
            }

            if ((characterOffset + wordToMatch?.Length) <= line.Length)
            {
                var subString = line.Substring(characterOffset, wordToMatch.Length);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase) && context == requestedContext)
                {
                    var diagnostic = new VSDiagnostic();
                    diagnostic.Message = "This is an " + Enum.GetName(typeof(DiagnosticSeverity), diagnosticInfo.Severity);
                    diagnostic.Severity = diagnosticInfo.Severity;
                    diagnostic.Range = new Range();
                    diagnostic.Range.Start = new Position(lineOffset, characterOffset);
                    diagnostic.Range.End = new Position(lineOffset, characterOffset + wordToMatch.Length);
                    diagnostic.Code = "Test" + Enum.GetName(typeof(DiagnosticSeverity), diagnosticInfo.Severity);
                    diagnostic.CodeDescription = new CodeDescription
                    {
                        Href = new Uri("https://www.microsoft.com")
                    };

                    if (projectAndContext != null)
                    {
                        diagnostic.Projects = new VSDiagnosticProjectInformation[] { projectAndContext };
                    }

                    diagnostic.Identifier = lineOffset + "," + characterOffset + " " + lineOffset + "," + diagnostic.Range.End.Character;
                    characterOffset = characterOffset + wordToMatch.Length;

                    // Our Mock UI only allows setting one tag at a time
                    diagnostic.Tags = new DiagnosticTag[1];
                    diagnostic.Tags[0] = diagnosticInfo.Tag;

                    return diagnostic;
                }
            }

            return null;
        }

        private Location GetLocation(string line, int lineOffset, ref int characterOffset, string wordToMatch)
        {
            if ((characterOffset + wordToMatch.Length) <= line.Length)
            {
                var subString = line.Substring(characterOffset, wordToMatch.Length);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    var location = new Location();
                    location.Uri = textDocument.Uri;
                    location.Range = new Range();
                    location.Range.Start = new Position(lineOffset, characterOffset);
                    location.Range.End = new Position(lineOffset, characterOffset + wordToMatch.Length);

                    return location;
                }
            }

            return null;
        }

        private Range GetHighlightRange(string line, int lineOffset, ref int characterOffset, string wordToMatch)
        {
            if ((characterOffset + wordToMatch.Length) <= line.Length)
            {
                var subString = line.Substring(characterOffset, wordToMatch.Length);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    var range = new Range();
                    range.Start = new Position(lineOffset, characterOffset);
                    range.End = new Position(lineOffset, characterOffset + wordToMatch.Length);

                    return range;
                }
            }

            return null;
        }

        private string GetWordAtPosition(Position position, string[] lines)
        {
            string line = lines.ElementAtOrDefault(position.Line);

            StringBuilder result = new StringBuilder();

            int startIdx = position.Character;
            int endIdx = startIdx + 1;

            while (char.IsLetter(line.ElementAtOrDefault(startIdx)))
            {
                result.Insert(0, line[startIdx]);
                startIdx--;
            }

            while (char.IsLetter(line.ElementAtOrDefault(endIdx)))
            {
                result.Append(line[endIdx]);
                endIdx++;
            }

            return result.ToString();
        }

        private void OnRpcDisconnected(object sender, JsonRpcDisconnectedEventArgs e)
        {
            Exit();
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Task SendMethodNotificationAsync<TIn>(LspNotification<TIn> method, TIn param)
        {
            return this.rpc.NotifyWithParameterObjectAsync(method.Name, param);
        }

        private Task<TOut> SendMethodRequestAsync<TIn, TOut>(LspRequest<TIn, TOut> method, TIn param)
        {
            return this.rpc.InvokeWithParameterObjectAsync<TOut>(method.Name, param);
        }
    }

    public class DiagnosticsInfo
    {
        public DiagnosticsInfo(string Text, VSProjectContext context, DiagnosticTag tag, DiagnosticSeverity severity)
        {
            this.Text = Text;
            this.Context = context;
            this.Tag = tag;
            this.Severity = severity;
        }

        public string Text
        {
            get;
            set;
        }

        public VSProjectContext Context
        {
            get;
            set;
        }

        public DiagnosticTag Tag
        {
            get;
            set;
        }

        public DiagnosticSeverity Severity
        {
            get;
            set;
        }
    }

    public class SymbolInfo
    {
        public SymbolInfo(string name, SymbolKind kind, string container)
        {
            this.Name = name;
            this.Kind = kind;
            this.Container = container;
        }

        public string Name
        {
            get;
            set;
        }

        public SymbolKind Kind
        {
            get;
            set;
        }

        public string Container
        {
            get;
            set;
        }
    }
}
