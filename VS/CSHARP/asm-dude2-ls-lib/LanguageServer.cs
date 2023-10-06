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

using AsmSourceTools;
using AsmTools;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmDude2LS
{
    public class LanguageServer : INotifyPropertyChanged
    {
        private const int MAX_LENGTH_DESCR_TEXT = 120;
        internal const double SlowWarningThresholdSec = 0.4; // threshold to warn that actions are considered slow
        internal const double SlowShutdownThresholdSec = 4.0; // threshold to switch off components
        internal const int MaxNumberOfCharsInToolTips = 150;
        internal const int MsSleepBeforeAsyncExecution = 1000;

        public static readonly CultureInfo CultureUI = CultureInfo.CurrentUICulture;

        private readonly JsonRpc rpc;
        private readonly HeaderDelimitedMessageHandler messageHandler;
        private readonly LanguageServerTarget target;
        private readonly ManualResetEvent disconnectEvent = new(false);
        private readonly List<Diagnostic> diagnostics;

        private readonly Dictionary<Uri, TextDocumentItem> textDocuments;
        private readonly Dictionary<Uri, string[]> textDocumentLines;
        private readonly Dictionary<Uri, KeywordID[][]> parsedDocuments;

        private readonly Dictionary<Uri, IEnumerable<FoldingRange>> foldingRanges;
        private readonly Dictionary<Uri, LabelGraph> labelGraphs;

        private readonly int referencesChunkSize = 10;
        private readonly int referencesDelayMs = 10;

        private readonly int highlightChunkSize = 10; // number of highlights returned before going to sleep for some delay
        private readonly int highlightsDelayMs = 10; // delay between highlight results returned

        private readonly TraceSource traceSource;

        private AsmDude2Tools asmDudeTools;
        public MnemonicStore mnemonicStore;
        public PerformanceStore performanceStore;
        public AsmLanguageServerOptions options;

        public static LanguageServer Create(Stream sender, Stream reader)
        {
            if (Instance == null)
            {
                Instance = new LanguageServer(sender, reader);
            }
            return Instance;
        }

        private static LanguageServer Instance
        {
            get;
            set;
        }

        private LanguageServer(Stream sender, Stream reader)
        {
            this.traceSource = Tools.CreateTraceSource();
            //LogInfo("LanguageServer: constructor"); // This lineNumber produces a crash
            this.target = new LanguageServerTarget(this);
            this.textDocuments = new Dictionary<Uri, TextDocumentItem>();
            this.textDocumentLines = new Dictionary<Uri, string[]>();
            this.parsedDocuments = new Dictionary<Uri, KeywordID[][]>();

            this.labelGraphs = new Dictionary<Uri, LabelGraph>();
            this.foldingRanges = new Dictionary<Uri, IEnumerable<FoldingRange>>();
            this.diagnostics = new List<Diagnostic>();
            this.Symbols = Array.Empty<VSSymbolInformation>();

            this.messageHandler = new HeaderDelimitedMessageHandler(sender, reader);
            this.rpc = new JsonRpc(this.messageHandler, this.target);
            this.rpc.Disconnected += this.OnRpcDisconnected;

            /* 30-09-23 why would we need the following code?
            this.rpc.ActivityTracingStrategy = new CorrelationManagerTracingStrategy()
            {
                TraceSource = this.traceSource,
            };
            this.rpc.TraceSource = this.traceSource;
            */

            ((JsonMessageFormatter)this.messageHandler.Formatter).JsonSerializer.Converters.Add(new VSExtensionConverter<TextDocumentIdentifier, VSTextDocumentIdentifier>());

            this.rpc.StartListening();

            this.target.OnInitializeCompletion += this.OnTargetInitializeCompletion;
            this.target.OnInitialized += this.OnTargetInitialized;
        }

        #region Tools

        private static (int, int) FindWordBoundary(int position, string lineStr)
        {
            // LogInfo($"FindWordBoundary: position = {position}; lineStr=\"{lineStr}\"");
            int lineLength = lineStr.Length;
            if (position >= lineLength)
            {
                return (-1, -1);
            }
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineStr[position]))
            {
                return (-1, -1);
            }

            int startPos = 0;
            int endPos = lineLength;
            char[] lineChars = lineStr.ToCharArray(0, lineLength);

            for (int i = position + 1; i < lineLength; ++i)
            {
                if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
                {
                    endPos = i;
                    break;
                }
            }
            for (int i = position; i >= 0; --i)
            {
                if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
                {
                    startPos = i + 1;
                    break;
                }
            }
            return (startPos, endPos);
        }

        // Get the word at the current position from the provided string
        private static (string, int, int) GetWord(int pos, string lineStr)
        {
            (int startPos, int endPos) = FindWordBoundary(pos, lineStr);
            int length = endPos - startPos;

            if (length <= 0)
            {
                return (string.Empty, -1, -1);
            }
            return (lineStr[startPos..endPos], startPos, endPos);
        }

        private static string Truncate(string text, int maxLength = MAX_LENGTH_DESCR_TEXT)
        {
            return (text.Length < maxLength) ? text : string.Concat(text.AsSpan(0, maxLength), "...");
        }

        private char GetChar(string str, int offset)
        {
            return ((offset < 0) || (offset >= str.Length)) ? ' ' : str.ElementAt(offset);
        }

        #endregion

        private string[] GetLines(Uri uri)
        {
            if (this.textDocumentLines.TryGetValue(uri, out var lines))
            {
                return lines;
            }
            return Array.Empty<string>();
        }

        private TextDocumentItem GetTextDocument(Uri uri)
        {
            if (this.textDocuments.TryGetValue(uri, out TextDocumentItem document))
            {
                return document;
            }
            return null;
        }

        private LabelGraph GetLabelGraph(Uri uri)
        {
            if (this.labelGraphs.TryGetValue(uri, out var graph))
            {
                return graph;
            }
            return null;
        }

        public static VSDiagnosticProjectInformation[] GetVSDiagnosticProjectInformation(VSTextDocumentIdentifier vsTextDocumentIdentifier)
        {
            VSDiagnosticProjectInformation projectAndContext = null;
            if ((vsTextDocumentIdentifier != null) && (vsTextDocumentIdentifier.ProjectContext != null))
            {
                projectAndContext = new VSDiagnosticProjectInformation
                {
                    ProjectName = vsTextDocumentIdentifier.ProjectContext.Label,
                    ProjectIdentifier = vsTextDocumentIdentifier.ProjectContext.Id,
                    Context = "Win32"
                };
            }
            return (projectAndContext == null) ? null : new VSDiagnosticProjectInformation[] { projectAndContext };
        }

        private void ScheduleDiagnosticMessage(
            string message,
            DiagnosticSeverity severity,
            Range range,
            VSTextDocumentIdentifier vsTextDocumentIdentifier)
        {
            //LogInfo($"ScheduleDiagnosticMessage {message}");

            this.diagnostics.Add(new VSDiagnostic()
            {
                Message = message,
                Severity = severity,
                Range = range,
                //Code = "Error Code Here",
                //CodeDescription = new CodeDescription
                //{
                //    Href = new Uri("https://www.microsoft.com")
                //},

                Projects = GetVSDiagnosticProjectInformation(vsTextDocumentIdentifier),
                //Identifier = $"{lineNumber},{offsetStart} {lineNumber},{offsetEnd}",
                Tags = new DiagnosticTag[1] { (DiagnosticTag)AsmDiagnosticTag.IntellisenseError }
            });
        }

        public string CurrentSettings
        {
            get; private set;
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

        public event EventHandler OnInitialized;
        public event EventHandler Disconnected;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ShowWindow;

        private void OnTargetInitializeCompletion(object sender, EventArgs e)
        {
            LogInfo("LanguageServer: OnTargetInitializeCompletion");
        }

        private void OnTargetInitialized(object sender, EventArgs e)
        {
            LogInfo("LanguageServer: OnTargetInitialized");
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        public void Initialize(AsmLanguageServerOptions options)
        {
            Contract.Assert(options != null);
            // LogInfo($"Initialize: Options: {jToken}");
            this.options = options;
        }

        public void Initialized()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
            {
                string filename_Regular = Path.Combine(path, "signature-may2019.txt");
                string filename_Hand = Path.Combine(path, "signature-hand-1.txt");
                this.mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand, this.options);
            }
            {
                string path_performance = Path.Combine(path, "Performance");
                this.performanceStore = new PerformanceStore(path_performance, this.options);
            }
            {
                this.asmDudeTools = AsmDude2Tools.Create(path, this.traceSource);
            }
        }

        private void UpdateInternals(Uri uri)
        {
            TextDocumentItem document = this.GetTextDocument(uri);
            if (document != null)
            {
                this.textDocumentLines.Remove(uri);
                var lines = document.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                this.textDocumentLines.Add(uri, lines);

                int fileID = 0; //TODO
                KeywordID[][] lineData = new KeywordID[lines.Length][];
                for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
                {
                    (KeywordID[] keywords, string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lines[lineNumber], lineNumber, fileID);
                    lineData[lineNumber] = keywords;
                }

                this.diagnostics.Clear();
                this.UpdateFoldingRanges(uri);
                this.UpdateLabelGraph(uri);

                if (false)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    this.UpdateSymbols(uri);
#pragma warning restore CS0162 // Unreachable code detected
                }
                this.SendDiagnostics(uri);
            }
        }

        public void OnTextDocumentOpened(DidOpenTextDocumentParams messageParams)
        {
            var uri = messageParams.TextDocument.Uri;
            this.textDocuments.Add(uri, messageParams.TextDocument);
            this.UpdateInternals(uri);
        }

        public void OnTextDocumentClosed(DidCloseTextDocumentParams messageParams)
        {
            var uri = messageParams.TextDocument.Uri;
            this.textDocuments.Remove(uri);
            this.textDocumentLines.Remove(uri);
        }

        private void UpdateLabelGraph(Uri uri)
        {
            LogInfo("UpdateLabelGraph");
            this.labelGraphs.Remove(uri);

            var textDocument = this.GetTextDocument(uri);
            string filename = textDocument.Uri.LocalPath;
            string[] lines = this.GetLines(uri);
            bool caseSensitiveLabels = true; //nasm has case sensitive labels
            LabelGraph labelGraph = new(lines, filename, caseSensitiveLabels, this.options);
            if (false) { // TODO 30-09-23: switch on the label diagnostics when most of the false positives are removed
#pragma warning disable CS0162 // Unreachable code detected
                labelGraph.UpdateDiagnostics();
                this.diagnostics.AddRange(labelGraph.Diagnostics);
#pragma warning restore CS0162 // Unreachable code detected
            }
            this.labelGraphs.Add(uri, labelGraph);
        }

        private void UpdateFoldingRanges(Uri uri)
        {
            static string GetCollapsedText(int startPos, string line)
            {
                int length = line.Length - startPos;
                if (length <= 0)
                {
                    return "...";
                }
                return line.Substring(startPos, length).Trim();
            }

            if (!this.options.CodeFolding_On)
            {
                return;
            }
            string StartKeyword = this.options.CodeFolding_BeginTag.ToUpper();
            string EndKeyword = this.options.CodeFolding_EndTag.ToUpper();

            int startKeywordLength = StartKeyword.Length;
            int endKeywordLength = EndKeyword.Length;

            List<FoldingRange> foldingRanges = new();
            Stack<int> startLineNumbers = new();
            Stack<int> startCharacters = new();

            var lines = this.GetLines(uri);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string lineStr = lines[lineNumber].ToUpper();
                int offsetRegion = lineStr.IndexOf(StartKeyword);
                if (offsetRegion != -1)
                {
                    startLineNumbers.Push(lineNumber);
                    startCharacters.Push(offsetRegion);
                }
                else
                {
                    int offsetEndRegion = lineStr.IndexOf(EndKeyword);
                    if (offsetEndRegion != -1)
                    {
                        if (startLineNumbers.Count == 0)
                        {
                            var severity = DiagnosticSeverity.Warning;
                            VSTextDocumentIdentifier textDocumentIdentifier = null;//TODO

                            string message = $"keyword {EndKeyword} has no matching {StartKeyword} keyword";
                            Range range = new()
                            {
                                Start = new Position(lineNumber, offsetEndRegion),
                                End = new Position(lineNumber, offsetEndRegion + endKeywordLength),
                            };
                            this.ScheduleDiagnosticMessage(message, severity, range, textDocumentIdentifier);
                        }
                        else
                        {
                            int startLine = startLineNumbers.Pop();
                            int startCharacter = startCharacters.Pop();
                            foldingRanges.Add(new FoldingRange
                            {
                                StartLine = startLine,
                                StartCharacter = startCharacter,
                                EndLine = lineNumber,
                                EndCharacter = offsetEndRegion + endKeywordLength,
                                Kind = FoldingRangeKind.Region,
                                CollapsedText = GetCollapsedText(startCharacter + startKeywordLength + 1, lines[startLine]),
                            });
                        }
                    }
                }
            }
            this.SetFoldingRanges(foldingRanges, uri);
        }

        public void UpdateServerSideTextDocument(string text, int version, Uri uri)
        {
            TextDocumentItem document = this.GetTextDocument(uri);
            if (document != null)
            {
                document.Text = text;
                document.Version = version;
                //TODO only update the lines that have changed
                this.UpdateInternals(uri);
            }
        }

        public void SendDiagnostics(Uri uri)
        {
            PublishDiagnosticParams parameter = new()
            {
                Uri = uri,
                Diagnostics = this.diagnostics.ToArray(),
            };
            _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnostics, parameter);
        }

        public CodeAction GetResolvedCodeAction(CodeAction parameter)
        {
            JObject token = JObject.FromObject(parameter.Data);
            CodeAction resolvedCodeAction = token.ToObject<CodeAction>();

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

            CodeAction createFileAction = new()
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

            CodeAction renameFileAction = new()
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

            CodeAction addTextAction = new()
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

            Dictionary<string, TextEdit[]> changes = new();
            changes.Add(parameter.TextDocument.Uri.AbsoluteUri, addTextEdit);

            CodeAction addTextActionChangesProperty = new()
            {
                Title = "Add Text Action - Changes property",
                Edit = new WorkspaceEdit
                {
                    Changes = changes,
                },
                Kind = CodeActionKind.QuickFix,
            };

            CodeAction addUnderscoreAction = new()
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

            CodeAction addTextActionWithError = new()
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
            CodeAction addTextActionToOtherFile = new()
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
            CodeAction unresolvedAddText = new()
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
            LogInfo($"Received: {JToken.FromObject(args)}");
            var uri = args.TextDocument.Uri;

            var lines = this.GetLines(uri);
            var (referenceWord, _, _) = GetWord(args.Position.Character, lines[args.Position.Line]);
            if (referenceWord.Length == 0)
            {
                return Array.Empty<object>();
            }

            IProgress<object[]> progress = args.PartialResultToken;
            int delay = this.referencesDelayMs;

            if (progress == null)
            {
                return Array.Empty<object>();
            }

            //TODO why not use VSLocation??
            List<Location> locations = new();
            List<Location> locationsChunk = new();

            for (int i = 0; i < lines.Length; i++)
            {
                string lineStr = lines[i];

                for (int j = 0; j < lineStr.Length; j++)
                {
                    Location location = this.GetLocation(lineStr, i, ref j, referenceWord, uri);

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

        /// <summary>
        /// Constrain the list of signatures given: 1) the currently operands provided by the user; and 2) the selected architectures
        /// </summary>
        /// <param name="data"></param>
        /// <param name="operands"></param>
        /// <returns></returns>
        private IEnumerable<AsmSignatureInformation> Constrain_Signatures(
                IEnumerable<AsmSignatureInformation> data,
                List<Operand> operands2,
                HashSet<Arch> selectedArchitectures2)
        {
#if DEBUG
                bool extraLogging = true;
#else
            bool extraLogging = false;
#endif

            if (extraLogging) LogInfo($"Constrain_Signatures: operands={string.Join(',', operands2)} data.Count={data.Count<AsmSignatureInformation>()}");

            foreach (AsmSignatureInformation asmSignatureElement in data)
            {
                bool allowed = true; // first assume the signature element is allowed; constrain it later

                //1] constrain the signature on architecture
                if (!asmSignatureElement.Is_Allowed(selectedArchitectures2))
                {
                    if (extraLogging) LogInfo($"Constrain_Signatures: asmSignatureElement {asmSignatureElement} is not allowed based on arch");
                    allowed = false;
                }

                //2] constrain on operands
                if (allowed)
                {
                    if ((operands2 == null) || (operands2.Count == 0))
                    {
                        // do nothing
                        if (extraLogging) LogInfo($"Constrain_Signatures: operands2 is null or empty");
                    }
                    else
                    {
                        for (int i = 0; i < operands2.Count; ++i)
                        {
                            Operand operand = operands2[i];
                            if (operand == null)
                            {
                                LogError($"Constrain_Signatures: somehow got an operand that is null");
                            }
                            else if (operand.IsReg || operand.IsMem || operand.IsImm)
                            {
                                if (extraLogging) LogInfo($"Constrain_Signatures: trying operand={operand}");
                                if (!asmSignatureElement.Is_Allowed(operand, i))
                                {
                                    if (extraLogging) LogInfo($"Constrain_Signatures: asmSignatureElement {asmSignatureElement} is not allowed based on mnemonic");
                                    allowed = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (allowed)
                {
                    yield return asmSignatureElement;
                }
            }
        }

        public SignatureHelp GetTextDocumentSignatureHelp(SignatureHelpParams parameter)
        {
            try
            {
#if DEBUG
                bool extraLogging = true;
#else
                bool extraLogging = false;
#endif

                if (!this.options.SignatureHelp_On)
                {
                    LogInfo($"GetTextDocumentSignatureHelp: switched off");
                    return null;
                }

                var lines = this.GetLines(parameter.TextDocument.Uri);
                int lineNumber = parameter.Position.Line;
                string completeLineStr = lines[lineNumber];
                int pos = parameter.Position.Character;
                string lineStr = completeLineStr[..pos];

                //LogInfo($"GetTextDocumentSignatureHelp: lineStr = {lineStr}");
  
                if (extraLogging)
                {
                    LogInfo("===========================");
                    LogInfo($"GetTextDocumentSignatureHelp: TriggerKind={parameter.Context.TriggerKind}; triggerChar={parameter.Context.TriggerCharacter}; IsRetrigger={parameter.Context.IsRetrigger}");
                }

                if (parameter.Context.TriggerCharacter == ";")
                {
                    LogError($"GetTextDocumentSignatureHelp: TriggerCharacter = {parameter.Context.TriggerCharacter}");
                    return null;
                }

                int fileID = 0; //TODO
                (object _, string _, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);
                if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: completeLineStr=\"{completeLineStr}\"; lineStr=\"{lineStr}\"; mnemonic={mnemonic}");
                //if there was a backspace, and the mnemonic becomes null, cancel the signature help, and start the code completion

                if (remark.Length > 0)
                {
                    LogInfo($"GetTextDocumentSignatureHelp: No signature help in a remark");
                    return null;
                }

                // we backspace we may backspace into the mnemonic
                if ((mnemonic == Mnemonic.NONE))
                {
                    return null;
                }

                int mnemonicOffset = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                if (mnemonicOffset == -1)
                {
                    LogError($"GetTextDocumentSignatureHelp: should not happen: investigate");
                    return null;
                }

                int argsOffset = mnemonicOffset + mnemonic.ToString().Length + 1;
                int argStrLength = parameter.Position.Character - argsOffset;
                if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: argsOffset={argsOffset}; argStrLength={argStrLength}");
                if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: current lineNumber: lineNumber=\"{lineStr}\"; mnemonic={mnemonic}, args={string.Join(",", args)}");

                List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
                HashSet<Arch> selectedArchitectures = this.options.Get_Arch_Switched_On();

                IEnumerable<AsmSignatureInformation> x = this.mnemonicStore.GetSignatures(mnemonic);
                IEnumerable<AsmSignatureInformation> y = this.Constrain_Signatures(x, operands, selectedArchitectures);
                List<SignatureInformation> z = new();
                foreach (AsmSignatureInformation asmSignatureElement in y)
                {
                    if (asmSignatureElement.Operands.Count > 0)
                    {
                        if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: adding SignatureInformation: {asmSignatureElement.SignatureInformation.Label}");
                        z.Add(asmSignatureElement.SignatureInformation);
                    }
                }
                if (z.Count == 0)
                {
                    return null; // no signature help present
                }

                int nCommas = Math.Max(0, operands.Count - 1);

                if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: lineStr=\"{lineStr}\"; pos={parameter.Position.Character}; mnemonic={mnemonic}, nCommas={nCommas}");
                return new SignatureHelp()
                {
                    ActiveSignature = 0,
                    ActiveParameter = nCommas,
                    Signatures = z.ToArray<SignatureInformation>(),
                };
            } catch (Exception e)
            {
                LogError($"GetTextDocumentSignatureHelp: e ={e}");
                return null;
            }
        }

        public void SetFoldingRanges(IEnumerable<FoldingRange> foldingRanges, Uri uri)
        {
            this.foldingRanges.Remove(uri);
            this.foldingRanges.Add(uri, foldingRanges);
        }

        public FoldingRange[] GetFoldingRanges(FoldingRangeParams parameter)
        {
            if (!this.options.CodeFolding_On)
            {
                return Array.Empty<FoldingRange>();
            }
            if (this.foldingRanges.TryGetValue(parameter.TextDocument.Uri, out IEnumerable<FoldingRange> value))
            {
                return value.ToArray();
            }
            return Array.Empty<FoldingRange>();
        }

        private HashSet<CompletionItem> Mnemonic_Operand_Completions(bool useCapitals, HashSet<AsmSignatureEnum> allowedOperands, int lineNumber)
        {
            //TODO return array

            //bool use_AsmSim_In_Code_Completion = this.asmSimulator_.Enabled && Settings.Default.AsmSim_Show_Register_In_Code_Completion;
            bool att_Syntax = this.options.Used_Assembler == AssemblerEnum.NASM_ATT;

            HashSet<CompletionItem> completions = new();

            foreach (Rn regName in this.mnemonicStore.Get_Allowed_Registers())
            {
                //string additionalInfo = null;
                if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
                {
                    string keyword = regName.ToString();
                    //if (use_AsmSim_In_Code_Completion && this.asmSimulator_.Tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(regName)))
                    //{
                    //    (string value, bool buzzy) = this.asmSimulator_.Get_Register_Value(regName, lineNumber, true, false, false, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration, false));
                    //    if (!buzzy)
                    //    {
                    //        additionalInfo = value;
                    //        LogInfo("AsmCompletionSource:Mnemonic_Operand_Completions; register " + keyword + " is selected and has value " + additionalInfo);
                    //    }
                    //}

                    if (att_Syntax)
                    {
                        keyword = "%" + keyword;
                    }

                    Arch arch = RegisterTools.GetArch(regName);
                    //LogInfo("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword : keyword.ToLowerInvariant();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this.asmDudeTools.Get_Description(keyword); //TODO add additional info
                    string displayText = Truncate(keyword + archStr);

                    completions.Add(new CompletionItem
                    {
                        Kind = this.GetCompletionItemKind(AsmTokenType.Register),
                        Label = displayText,
                        InsertText = insertionText,
                        SortText = insertionText,
                        Documentation = descriptionStr
                    });
                }
            }

            foreach (string keyword in this.asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this.asmDudeTools.Get_Token_Type_Intel(keyword); //TODO support all assemblers

                string keyword2 = keyword;
                bool selected = true;

                //LogInfo("CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected);

                switch (type)
                {
                    case AsmTokenType.Misc:
                        {
                            if (!AsmSignatureTools.Is_Allowed_Misc(keyword, allowedOperands))
                            {
                                selected = false;
                            }
                            break;
                        }
                    default:
                        {
                            selected = false;
                            break;
                        }
                }
                if (selected)
                {
                    Arch arch = this.asmDudeTools.Get_Architecture(keyword);
                    //LogInfo("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword2 : keyword2.ToLowerInvariant();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this.asmDudeTools.Get_Description(keyword);
                    descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncate(keyword2 + archStr + descriptionStr);

                    completions.Add(new CompletionItem
                    {
                        Kind = this.GetCompletionItemKind(type),
                        Label = displayText,
                        InsertText = insertionText,
                        SortText = insertionText,
                        Documentation = descriptionStr
                    });
                }
            }
            return completions;
        }

        private IEnumerable<CompletionItem> Label_Completions(LabelGraph labelGraph, bool useCapitals, bool addSpecialKeywords)
        {
            if (addSpecialKeywords)
            {
                yield return new CompletionItem
                {
                    Kind = this.GetCompletionItemKind(AsmTokenType.Misc),
                    Label = "SHORT",
                    InsertText = useCapitals ? "SHORT" : "short",
                    SortText = "\tSHORT", // use a tab to get on top when sorting
                    Documentation = string.Empty
                };
                yield return new CompletionItem
                {
                    Kind = this.GetCompletionItemKind(AsmTokenType.Misc),
                    Label = "NEAR",
                    InsertText = useCapitals ? "NEAR" : "near",
                    SortText = "\tNEAR", // use a tab to get on top when sorting
                    Documentation = string.Empty
                };
            }

            AssemblerEnum usedAssembler = this.options.Used_Assembler;

            SortedDictionary<string, string> labels = labelGraph.Label_Descriptions;
            foreach (KeyValuePair<string, string> entry in labels)
            {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                string displayTextFull = entry.Key + " - " + entry.Value;
                string insertionText = Tools.Retrieve_Regular_Label(entry.Key, usedAssembler);
                yield return new CompletionItem
                {
                    Kind = this.GetCompletionItemKind(AsmTokenType.Label),
                    Label = Truncate(insertionText, 30),
                    InsertText = insertionText,
                    Documentation = displayTextFull
                };
            }
        }

        public CompletionList GetTextDocumentCompletion(CompletionParams parameter)
        {
            IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, HashSet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
            {
                HashSet<CompletionItem> completions = new();

                // Add the completions of AsmDude directives (such as code folding directives)
                #region
                if (addSpecialKeywords && this.options.CodeFolding_On)
                {
                    {
                        string labelText = this.options.CodeFolding_BeginTag;     //the characters that start the outlining region
                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Macro,
                            Label = $"{labelText} - keyword to start code folding",
                            InsertText = labelText[1..], // remove the prefix #
                            SortText = labelText,
                            //Documentation = $"keyword to start code folding",
                        });
                    }
                    {
                        string labelText = this.options.CodeFolding_EndTag;       //the characters that end the outlining region
                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Macro,
                            Label = $"{labelText} - keyword to end code folding",
                            InsertText = labelText[1..], // remove the prefix #
                            SortText = labelText,
                            //Documentation = $"keyword to end code folding",
                        });
                    }
                }
                #endregion

                AssemblerEnum usedAssembler = this.options.Used_Assembler;

                #region Add completions
                if (selectedTypes.Contains(AsmTokenType.Mnemonic))
                {
                    foreach (Mnemonic mnemonic2 in this.mnemonicStore.Get_Allowed_Mnemonics())
                    {
                        string keyword_uppercase = mnemonic2.ToString();
                        string insertionText = useCapitals ? keyword_uppercase : keyword_uppercase.ToLowerInvariant();
                        string archStr = ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic2));

                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Keyword,
                            Label = $"{keyword_uppercase} {archStr}",
                            InsertText = insertionText,
                            SortText = insertionText,
                            Documentation = this.mnemonicStore.GetDescription(mnemonic2),
                        });
                    }
                }
                //Add the completions that are defined in the xml file
                foreach (string keyword_uppercase in this.asmDudeTools.Get_Keywords())
                {
                    AsmTokenType type = this.asmDudeTools.Get_Token_Type_Intel(keyword_uppercase);
                    if (selectedTypes.Contains(type))
                    {
                        Arch arch = Arch.ARCH_NONE;
                        bool selected = true;

                        if (type == AsmTokenType.Directive)
                        {
                            AssemblerEnum assembler = this.asmDudeTools.Get_Assembler(keyword_uppercase);
                            if (assembler.HasFlag(AssemblerEnum.MASM))
                            {
                                if (!usedAssembler.HasFlag(AssemblerEnum.MASM))
                                {
                                    selected = false;
                                }
                            }
                            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                            {
                                if (!usedAssembler.HasFlag(AssemblerEnum.NASM_INTEL))
                                {
                                    selected = false;
                                }
                            }
                        }
                        else
                        {
                            arch = this.asmDudeTools.Get_Architecture(keyword_uppercase);
                            selected = this.options.Is_Arch_Switched_On(arch);
                        }

                        LogInfo("CodeCompletionSource:Selected_Completions; keyword=" + keyword_uppercase + "; arch=" + arch + "; selected=" + selected);

                        if (selected)
                        {
                            // by default, the entry.Key is with capitals
                            string insertionText = useCapitals ? keyword_uppercase : keyword_uppercase.ToLowerInvariant();
                            string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                            string descriptionStr = this.asmDudeTools.Get_Description(keyword_uppercase);
                            descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                            string displayTextFull = keyword_uppercase + archStr + descriptionStr;
                            string displayText = Truncate(displayTextFull);

                            completions.Add(new CompletionItem
                            {
                                Kind = this.GetCompletionItemKind(type),
                                Label = displayText,
                                InsertText = insertionText,
                                SortText = insertionText,
                                Documentation = descriptionStr
                            });
                        }
                    }
                }
                #endregion

                return completions;
            }

            try
            {
#if DEBUG
                bool extraLogging = true;
#else
                bool extraLogging = false;
#endif

                if (!this.options.CodeCompletion_On)
                {
                    LogInfo($"OnTextDocumentCompletion: switched off");
                    return new CompletionList();
                }

                var lines = this.GetLines(parameter.TextDocument.Uri);
                int lineNumber = parameter.Position.Line;
                string completeLineStr = lines[lineNumber];
                int pos = parameter.Position.Character;

                if (extraLogging) LogInfo($"===========================\nOnTextDocumentCompletion: completeLineStr=\"{completeLineStr}\"; pos=\'{pos}\'");

                // if the current characters is a asm separator, no code completion
                char currentChar = this.GetChar(completeLineStr, pos - 1);
                //if (AsmTools.AsmSourceTools.IsSeparatorChar(currentChar))
                //{
                //    if (extraLogging) LogInfo($"OnTextDocumentCompletion: we just typed a separator char \'{currentChar}\' thus no code completion");
                //    return new CompletionList();
                //}

                // we only consider the line till (and including) the current position
                string lineStr = completeLineStr[..pos];
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: lineStr=\"{lineStr}\"; currentChar=\'{currentChar}\'");

                int fileID = 0; //TODO
                (object _, string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: label=\"{label}\"; mnemonic={mnemonic}; args={string.Join(',', args)}; remark=\"{remark}\"");

                // if we are typing in a remark: no code completion please
                if (remark.Length > 0)
                {
                    if (extraLogging) LogInfo($"OnTextDocumentCompletion: we are in a remark: no code completion");
                    return new CompletionList();
                }

                // determine if the current word we are typing is all capitals
                (string currentWord, _, _) = GetWord(pos-1, lineStr);
                bool useCapitals = (currentWord == currentWord.ToUpper());

                if (extraLogging) LogInfo($"OnTextDocumentCompletion: currentWord=\"{currentWord}\"; useCapitals={useCapitals}");

                // if the mnemonic is NONE we should suggest mnemonics
                if (mnemonic == Mnemonic.NONE)
                {
                    HashSet<AsmTokenType> selected = new() { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                    if (extraLogging) LogInfo($"OnTextDocumentCompletion: A");
                    return new CompletionList()
                    {
                        Items = Selected_Completions(useCapitals, selected, true).ToArray(),
                    };
                }

                int mnemonicOffsetStart = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                if (mnemonicOffsetStart == -1)
                {
                    LanguageServer.LogError($"OnTextDocumentCompletion: should not happen: investigate");
                    return null;
                }

                // are we with the cursor in the mnemonic: then we should only suggest mnemonics:
                int mnemonicOffsetEnd = mnemonicOffsetStart + mnemonic.ToString().Length;
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: pos={pos}; mnemonicOffsetEnd={mnemonicOffsetEnd}");
                if (pos <= mnemonicOffsetEnd)
                {
                    HashSet<AsmTokenType> selected = new() { AsmTokenType.Jump, AsmTokenType.Mnemonic };
                    if (extraLogging) LogInfo($"OnTextDocumentCompletion: B");
                    return new CompletionList()
                    {
                        Items = Selected_Completions(useCapitals, selected, true).ToArray(),
                    };
                }

                // if the mnemonic is a jump, we should suggest labels   
                if (AsmTools.AsmSourceTools.IsJump(mnemonic))
                {
                    var labelGraph = this.GetLabelGraph(parameter.TextDocument.Uri);
                    if (extraLogging) LogInfo($"OnTextDocumentCompletion: C");
                    return new CompletionList()
                    {
                        Items = this.Label_Completions(labelGraph, useCapitals, true).ToArray(),
                    };
                }

                // if we are here: there is a mnemonic, and not a jump, the cursor is not in the mnemonic, thus we analyse the
                // parameters of the mnemonic and make suggestions based on the allowed parameters

                HashSet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
                HashSet<AsmSignatureEnum> allowed = new();
                List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
                int nCommas = Math.Max(0, operands.Count - 1);

                IEnumerable<AsmSignatureInformation> allSignatures = this.mnemonicStore.GetSignatures(mnemonic);

                if (extraLogging)
                {
                    LogInfo($"OnTextDocumentCompletion: nCommas={nCommas}; operands={string.Join(',', operands)}; allSignatures.Count={allSignatures.Count<AsmSignatureInformation>()}");
                    foreach (AsmSignatureInformation s in allSignatures)
                    {
                        LogInfo($"OnTextDocumentCompletion: available signatures: {s}");
                    }
                }

                // constrain allSignatures of the mnemonic based on 1] architectures that are switched on, and 2] the already provided operands
                foreach (AsmSignatureInformation se in this.Constrain_Signatures(allSignatures, operands, arch_switched_on))
                {
                    if (nCommas < se.Operands.Count)
                    {
                        foreach (AsmSignatureEnum s in se.Operands[nCommas])
                        {
                            allowed.Add(s);
                        }
                    }
                }
                if (extraLogging) {
                    LogInfo($"OnTextDocumentCompletion: D: useCapitals={useCapitals}; allowed.Count={allowed.Count}");
                    foreach (AsmSignatureEnum sig in allowed)
                    {
                        LogInfo($"OnTextDocumentCompletion: D: allowed signature {sig}");
                    }
                }
                return new CompletionList()
                {
                    Items = this.Mnemonic_Operand_Completions(useCapitals, allowed, parameter.Position.Line).ToArray()
                };
            }
            catch (Exception e)
            {
                {
                    LogError($"OnTextDocumentCompletion: e={e}");
                    return new CompletionList();
                }
            }
        }

        public DocumentHighlight[] GetDocumentHighlights(IProgress<DocumentHighlight[]> progress, Position position, Uri uri, CancellationToken token)
        {
            if (progress == null)
            {
                LogInfo($"LanguageServer:GetDocumentHighlights: progress is null");
                return Array.Empty<DocumentHighlight>();
            }
            TextDocumentItem document = this.GetTextDocument(uri);
            if (document == null)
            {
                LogInfo($"LanguageServer:GetDocumentHighlights: document is null");
                return Array.Empty<DocumentHighlight>();
            }

            var lines = this.GetLines(uri);
            var lineStr2 = lines[position.Line];
            (int startPos, int endPos) = FindWordBoundary(position.Character, lineStr2);
            int length = endPos - startPos;

            if (length <= 0)
            {
                LogInfo($"LanguageServer:GetDocumentHighlights: argStrLength too small ({length})");
                return Array.Empty<DocumentHighlight>();
            }
            string currentHighlightedWord = lineStr2.Substring(startPos, length);
            if (string.IsNullOrEmpty(currentHighlightedWord))
            {
                LogInfo($"LanguageServer:GetDocumentHighlights: currentHighlightedWord is not significant ({currentHighlightedWord})");
                return Array.Empty<DocumentHighlight>();
            }

            IList<string> currentHighlightedWords = new List<string>();
            Rn reg = RegisterTools.ParseRn(currentHighlightedWord, false);
            if (reg == Rn.NOREG)
            {
                currentHighlightedWords.Add(currentHighlightedWord);
            }
            else
            {
                foreach (string x in RegisterTools.GetRelatedRegisterNew(reg))
                {
                    currentHighlightedWords.Add(x);
                }
            }
            LogInfo($"LanguageServer:GetDocumentHighlights: currentHighlightedWords={string.Join(",", currentHighlightedWords)}");

            List<DocumentHighlight> highlights = new();
            List<DocumentHighlight> chunk = new();

            for (int i = 0; i < lines.Length; i++)
            {
                string lineStr = lines[i];

                for (int j = 0; j < lineStr.Length; j++)
                {
                    Range range = this.GetHighlightRangeMultiple(lineStr, i, ref j, currentHighlightedWords);
                    if (range != null)
                    {
                        j++;
                        DocumentHighlight highlight = new()
                        {
                            Range = range,
                            Kind = DocumentHighlightKind.Text
                        };
                        highlights.Add(highlight);
                        chunk.Add(highlight);

                        if (chunk.Count == this.highlightChunkSize)
                        {
                            progress.Report(chunk.ToArray());
                            Thread.Sleep(this.highlightsDelayMs);  // Wait between chunks
                            chunk.Clear();
                        }
                    }
                    token.ThrowIfCancellationRequested();
                }
            }

            // Report last chunk if it has elements since it didn't reached the specified size
            if (chunk.Count > 0)
            {
                progress.Report(chunk.ToArray());
            }

            return highlights.ToArray();
        }

        private AsmTokenType GetAsmTokenType(string keyword_uppercase)
        {
            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_uppercase, true);
            if (mnemonic != Mnemonic.NONE)
            {
                if (AsmTools.AsmSourceTools.IsJump(mnemonic))
                {
                    return AsmTokenType.Jump;
                }
                return AsmTokenType.Mnemonic;
            }
            if (RegisterTools.IsRn(keyword_uppercase))
            {
                return AsmTokenType.Register;
            }

            //TODO labels and constants

            return AsmTokenType.UNKNOWN;
        }

        public Hover GetHover(TextDocumentPositionParams parameter)
        {
            if (!this.options.AsmDoc_On)
            {
                LogInfo($"OnHover: switched off");
                return null;
            }
            var lines = this.GetLines(parameter.TextDocument.Uri);
            var (keyword, startPos, endPos) = GetWord(parameter.Position.Character, lines[parameter.Position.Line]);
            if (keyword.Length == 0)
            {
                return null;
            }
            string keyword_uppercase = keyword.ToUpperInvariant();

            SumType<string, MarkedString>[] hoverContent = null;

            switch (this.GetAsmTokenType(keyword_uppercase))
            {
                case AsmTokenType.Mnemonic: // intentional fall through
                case AsmTokenType.Jump:
                    {
                        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_uppercase, true);
                        string mnemonicStr = mnemonic.ToString();
                        string archStr = ":" + ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));
                        string descr = this.mnemonicStore.GetDescription(mnemonic);
                        string full_Descr = AsmTools.AsmSourceTools.Linewrap($"{mnemonicStr} {archStr} {descr}", MaxNumberOfCharsInToolTips);
                        string performanceStr = "";

                        bool performanceInfoAvailable = false;
                        if (this.options.PerformanceInfo_On)
                        {
                            bool first = true;
                            string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                            MicroArch selectedMicroArchs = MicroArch.SkylakeX | MicroArch.Haswell;// Tools.Get_MicroArch_Switched_On();
                            foreach (PerformanceItem item in this.performanceStore.GetPerformance(mnemonic, selectedMicroArchs))
                            {
                                if (first)
                                {
                                    first = false;
                                    performanceInfoAvailable = true;

                                    string msg1 = string.Format(
                                        CultureUI,
                                        format,
                                        string.Empty, string.Empty, "µOps", "µOps", "µOps", string.Empty, string.Empty, string.Empty);

                                    string msg2 = string.Format(
                                        CultureUI,
                                        "\n" + format,
                                        "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", string.Empty);

                                    performanceStr = msg1;
                                    performanceStr += msg2;
                                }

                                string msg3 = string.Format(
                                    CultureUI,
                                    "\n" + format,
                                    item.microArch_ + " ",
                                    item.instr_ + " " + item.args_ + " ",
                                    item.mu_Ops_Fused_ + " ",
                                    item.mu_Ops_Merged_ + " ",
                                    item.mu_Ops_Port_ + " ",
                                    item.latency_ + " ",
                                    item.throughput_ + " ",
                                    item.remark_);

                                performanceStr += msg3;
                            }
                        }

                        hoverContent = new SumType<string, MarkedString>[]{
                            new(new MarkedString
                            {
                                Language = MarkupKind.PlainText.ToString(),
                                Value = full_Descr + "\n",
                            }),
                            new(new MarkedString
                            {
                                Language = MarkupKind.Markdown.ToString(),
                                Value = ((performanceInfoAvailable) ? "**Performance:**\n```text\n" + performanceStr + "\n```" : "No performance info"),
                            })
                        };
                        break;
                    }
                case AsmTokenType.Register:
                    {
                        // int lineNumber = //AsmTools.Tools.Get_LineNumber(tagSpan);
                        if (keyword_uppercase.StartsWith("%", StringComparison.Ordinal))
                        {
                            keyword_uppercase = keyword_uppercase.Substring(1); // remove the preceding % in AT&T syntax
                        }

                        Rn reg = RegisterTools.ParseRn(keyword_uppercase, true);
                        if (this.mnemonicStore.IsRegisterSwitchedOn(reg))
                        {
                            string regStr = reg.ToString();
                            Arch arch = RegisterTools.GetArch(reg);

                            string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "] ";
                            string descr = this.asmDudeTools.Get_Description(regStr);
                            if (regStr.Length > (MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }
                            string full_Descr = AsmTools.AsmSourceTools.Linewrap(archStr + descr, MaxNumberOfCharsInToolTips);
                            hoverContent = new SumType<string, MarkedString>[]{
                                new(new MarkedString
                                {
                                    Language = MarkupKind.PlainText.ToString(),
                                    Value = $"Register {regStr}: {full_Descr}",
                                }),
                            };
                        }
                        break;
                    }
                case AsmTokenType.Constant: //TODO
                    break;
                case AsmTokenType.LabelDef: //TODO
                    break;
                case AsmTokenType.Label: //TODO
                    break;
                case AsmTokenType.UNKNOWN:
                    {
                        string descr = this.asmDudeTools.Get_Description(keyword_uppercase);
                        if (descr.Length > 0)
                        {
                            if (keyword_uppercase.Length > (MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }
                            descr = AsmTools.AsmSourceTools.Linewrap(descr, MaxNumberOfCharsInToolTips);
                            hoverContent = new SumType<string, MarkedString>[]{
                                new(new MarkedString
                                {
                                    Language = MarkupKind.PlainText.ToString(),
                                    Value = $"Keyword {keyword}: {descr}",
                                }),
                            };
                        }
                        break;
                    }
            }

            /*
            switch (tag.Type)
            {
                case AsmTokenType.Misc: // intentional fall through
                case AsmTokenType.Directive:
                    // done...
                case AsmTokenType.Register:
                    // done...
                case AsmTokenType.Mnemonic: // intentional fall through
                case AsmTokenType.MnemonicOff: // intentional fall through
                case AsmTokenType.Jump:
                    // done....
                case AsmTokenType.Label:
                    {
                        string label = keyword;
                        string labelPrefix = tag.Misc;
                        string full_Qualified_Label = Tools.Make_Full_Qualified_Label(labelPrefix, label, Tools.Used_Assembler);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(Tools.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

                        string descr = this.Get_Label_Description(full_Qualified_Label);
                        if (descr.Length == 0)
                        {
                            descr = this.Get_Label_Description(label);
                        }
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.LabelDef:
                    {
                        string label = keyword;
                        string extra_Tag_Info = tag.Misc;
                        string full_Qualified_Label;
                        if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
                        {
                            full_Qualified_Label = label;
                        }
                        else
                        {
                            full_Qualified_Label = Tools.Make_Full_Qualified_Label(extra_Tag_Info, label, Tools.Used_Assembler);
                        }

                        LogInfo(("AsmQuickInfoSource:AugmentQuickInfoSession: found label def " + full_Qualified_Label);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(Tools.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

                        string descr = this.Get_Label_Def_Description(full_Qualified_Label, label);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.Constant:
                    {
                        (bool valid, ulong value, int nBits) = AsmSourceTools.Evaluate_Constant(keyword);
                        string constantStr = valid
                            ? value + "d = " + value.ToString("X", Tools.CultureUI) + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
                            : keyword;

                        var containerElement = new ContainerElement(
                            ContainerElementStyle.Wrapped,
                            new ImageElement(_icon),
                            new ClassifiedTextElement(
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Constant "),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Number, constantStr)));

                        return (new List<object> { containerElement }, keywordSpan.Value);
                    }
                case AsmTokenType.UserDefined1:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 1: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(Tools.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined1))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_uppercase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.UserDefined2:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 2: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(Tools.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined2))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_uppercase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.UserDefined3:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 3: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(Tools.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined3))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_uppercase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                default:
                    //description = new TextBlock();
                    //description.Inlines.Add(makeRun1("Unused tagType " + asmTokenTag.Tag.type));
                    break;
            }
            if (description != null)
            {
                description.Focusable = true;
                description.FontSize = Tools.GetFontSize() + 2;
                description.FontFamily = Tools.GetFontType();
                //LogInfo((string.Format(Tools.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                //quickInfoContent.Add(description);
                return (new List<object> { "other" }, keywordSpan.Value);
            }
            */


            if (hoverContent != null)
            {
                //return new Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalHover
                //{
                //    Range = new Range()
                //    {
                //        Start = new Position(parameter.Position.Line, startPos),
                //        End = new Position(parameter.Position.Line, endPos),
                //    },
                //    Contents = new MarkupContent
                //    {
                //        Kind = MarkupKind.Markdown,
                //        Value = "TODO **bold**"
                //    },
                //    //RawContent = new ClassifiedTextElement(descriptionBuilder.Select(tp => new ClassifiedTextRun(tp.Tag.ToClassificationTypeName(), tp.Text)))
                //};

                return new Hover()
                {
                    Contents = hoverContent,
                    Range = new Range()
                    {
                        Start = new Position(parameter.Position.Line, startPos),
                        End = new Position(parameter.Position.Line, endPos),
                    },
                };
            }
            return null;
        }

        public void SetDocumentSymbols(IEnumerable<VSSymbolInformation> symbolsInfo)
        {
            this.Symbols = symbolsInfo;
        }

        public VSSymbolInformation[] GetDocumentSymbols(DocumentSymbolParams parameters)
        {
            return this.Symbols.ToArray();
        }

        private void UpdateSymbols(Uri uri)
        {
            IList<VSSymbolInformation> symbolInfo = new List<VSSymbolInformation>();
            var lines = this.GetLines(uri);

            int fileID = 0; //TODO

            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string lineStr = lines[lineNumber];
                (object _, string label, Mnemonic mnemonic, string[] args, string _) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);
                if (label.Length > 0)
                {
                    int pos = lineStr.IndexOf(label);
                    symbolInfo.Add(new VSSymbolInformation
                    {
                        Name = label,
                        Kind = SymbolKind.Key,
                        Location = new Location
                        {
                            Uri = uri,
                            Range = new Range
                            {
                                Start = new Position
                                {
                                    Line = lineNumber,
                                    Character = pos,
                                },
                                End = new Position
                                {
                                    Line = lineNumber,
                                    Character = pos + label.Length,
                                }
                            }
                        },
                        #region VS specific
                        HintText = "some hinttext here?",
                        Description = "some description here?",
                        //Icon = // If specified, this icon is used instead of SymbolKind.
                        #endregion
                    });
                }
                if (mnemonic != Mnemonic.NONE)
                {
                    int pos = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                    string mnemonicStr = mnemonic.ToString();
                    symbolInfo.Add(new VSSymbolInformation
                    {
                        Name = mnemonicStr,
                        Kind = SymbolKind.Function,
                        HintText = "some hint text here?",
                        Description = "some description here?",
                        Location = new Location
                        {
                            Uri = uri,
                            Range = new Range
                            {
                                Start = new Position
                                {
                                    Line = lineNumber,
                                    Character = pos,
                                },
                                End = new Position
                                {
                                    Line = lineNumber,
                                    Character = pos + mnemonicStr.Length,
                                }
                            }
                        }
                    });
                    if (false)
                    {
#pragma warning disable CS0162 // Unreachable code detected
                        for (int i = 0; i < args.Length; ++i)
                        {
                            symbolInfo.Add(new VSSymbolInformation
                            {
                                Name = mnemonic.ToString(),
                                Kind = SymbolKind.Function,
                                HintText = "some hint text here?",
                                Description = "some description here?",
                            });
                        }
#pragma warning restore CS0162 // Unreachable code detected
                    }
                }
            }
            this.SetDocumentSymbols(symbolInfo);
        }

        public VSProjectContextList GetProjectContexts()
        {
            VSProjectContextList result = new()
            {
                ProjectContexts = this.Contexts.ToArray(),
                DefaultIndex = 0
            };

            return result;
        }

        CompletionItemKind GetCompletionItemKind(AsmTokenType type)
        {
            return type switch
            {
                AsmTokenType.Directive => CompletionItemKind.Value,
                AsmTokenType.Register => CompletionItemKind.Variable,
                AsmTokenType.Misc => CompletionItemKind.Unit,
                AsmTokenType.Label => CompletionItemKind.Reference,
                _ => CompletionItemKind.None,
            };
        }

        #region Logging

        public static void LogInfo(string message)
        {
            if (Instance.target.traceSetting == TraceSetting.Verbose)
            {
                Console.WriteLine($"INFO {DateTimeOffset.Now.ToString("yyyyMMdd hh.mm.ss.ffffff")}: {message}");
                Instance.traceSource.TraceEvent(TraceEventType.Information, 0, message);
            }
        }

        public static void LogWarning(string message)
        {
            Console.WriteLine($"WARNING {DateTimeOffset.Now.ToString("yyyyMMdd hh.mm.ss.ffffff")}: {message}");
            Instance.traceSource.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public static void LogError(string message)
        {
            Console.WriteLine($"ERROR {DateTimeOffset.Now.ToString("yyyyMMdd hh.mm.ss.ffffff")}: {message}");
            Instance.MakeWindowVisible();
            Instance.traceSource.TraceEvent(TraceEventType.Error, 0, message);
        }

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
            _ = this.SendMethodNotificationAsync(Methods.WindowLogMessage, new LogMessageParams
            {
                Message = message,
                MessageType = messageType
            });
        }

        public void ShowMessage(string message, MessageType messageType)
        {
            LogInfo($"LanguageServer: ShowMessage: message={message}; messageType={messageType.ToString()}");
            ShowMessageParams parameter = new()
            {
                Message = message,
                MessageType = messageType
            };
            _ = this.SendMethodNotificationAsync(Methods.WindowShowMessage, parameter);
        }

        public async Task<MessageActionItem> ShowMessageRequestAsync(string message, MessageType messageType, string[] actionItems)
        {
            ShowMessageRequestParams parameter = new()
            {
                Message = message,
                MessageType = messageType,
                Actions = actionItems.Select(a => new MessageActionItem { Title = a }).ToArray()
            };

            return await this.SendMethodRequestAsync(Methods.WindowShowMessageRequest, parameter);
        }

        #endregion

        // store incoming settings from VS
        public void SendSettings(DidChangeConfigurationParams parameter)
        {
            this.CurrentSettings = parameter.Settings.ToString();
            this.NotifyPropertyChanged(nameof(this.CurrentSettings));

            JToken parsedSettings = JToken.Parse(this.CurrentSettings);
            int newMaxProblems = parsedSettings.Children().First().Values<int>("maxNumberOfProblems").First();

            LogInfo($"SendSettings: received {parameter}");
        }

        public void Exit()
        {
            this.disconnectEvent.Set();

            Disconnected?.Invoke(this, new EventArgs());
        }

        //public void ApplyTextEdit(string text, Uri uri)
        //{
        //    TextDocumentItem document = GetTextDocument(uri);
        //    if (document == null)
        //    {
        //        return;
        //    }
        //    TextEdit[] addTextEdit = new TextEdit[]
        //    {
        //        new TextEdit
        //        {
        //            Range = new Range
        //            {
        //                Start = new Position
        //                {
        //                    Line = 0,
        //                    Character = 0
        //                },
        //                End = new Position
        //                {
        //                    Line = 0,
        //                    Character = 0
        //                }
        //            },
        //            NewText = text,
        //        }
        //    };

        //    ApplyWorkspaceEditParams parameter = new ApplyWorkspaceEditParams()
        //    {
        //        Label = "Test Edit",
        //        Edit = new WorkspaceEdit()
        //        {
        //            DocumentChanges = new TextDocumentEdit[]
        //                {
        //                    new TextDocumentEdit()
        //                    {
        //                        TextDocument = new OptionalVersionedTextDocumentIdentifier()
        //                        {
        //                            Uri = uri,
        //                        },
        //                        Edits = addTextEdit,
        //                    },
        //                }
        //        }
        //    };

        //    _ = Task.Run(async () =>
        //    {
        //        ApplyWorkspaceEditResponse response = await this.SendMethodRequestAsync(Methods.WorkspaceApplyEdit, parameter);

        //        if (!response.Applied)
        //        {
        //            LogInfo($"Failed to apply edit: {response.FailureReason}");
        //        }
        //    });
        //}

        private Location GetLocation(string lineStr, int lineOffset, ref int characterOffset, string wordToMatch, Uri uri)
        {
            if ((characterOffset + wordToMatch.Length) <= lineStr.Length)
            {
                string subString = lineStr.Substring(characterOffset, wordToMatch.Length);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return new Location
                    {
                        Uri = uri,
                        Range = new Range
                        {
                            Start = new Position(lineOffset, characterOffset),
                            End = new Position(lineOffset, characterOffset + wordToMatch.Length)
                        }
                    };
                }
            }
            return null;
        }

        private Range GetHighlightRange(string lineStr, int lineOffset, ref int characterOffset, string wordToMatch)
        {
            int wordLength = wordToMatch.Length;

            if ((characterOffset + wordLength) <= lineStr.Length)
            {
                char before = this.GetChar(lineStr, characterOffset - 1);
                char after = this.GetChar(lineStr, characterOffset + wordLength);

                if (!AsmTools.AsmSourceTools.IsSeparatorChar(before) || !AsmTools.AsmSourceTools.IsSeparatorChar(after))
                {
                    return null;
                }

                string subString = lineStr.Substring(characterOffset, wordLength);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return new Range
                    {
                        Start = new Position(lineOffset, characterOffset),
                        End = new Position(lineOffset, characterOffset + wordLength)
                    };
                }
            }
            return null;
        }

        private Range GetHighlightRangeMultiple(string line, int lineOffset, ref int characterOffset, IEnumerable<string> wordsToMatch)
        {
            foreach (string wordToMatch in wordsToMatch)
            {
                var range = this.GetHighlightRange(line, lineOffset, ref characterOffset, wordToMatch);
                if (range != null)
                {
                    return range;
                }
            }
            return null;
        }

        private void OnRpcDisconnected(object sender, JsonRpcDisconnectedEventArgs e)
        {
            this.Exit();
        }
        
        public void MakeWindowVisible() 
        {
            this.ShowWindow?.Invoke(this, EventArgs.Empty);
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
}
