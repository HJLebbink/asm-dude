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

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using Newtonsoft.Json.Linq;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        public int maxProblems = 10;

        private readonly JsonRpc rpc;
        private readonly HeaderDelimitedMessageHandler messageHandler;
        private readonly LanguageServerTarget target;
        private readonly ManualResetEvent disconnectEvent = new ManualResetEvent(false);
        private readonly IList<Diagnostic> diagnostics;
 
        private readonly IDictionary<Uri, TextDocumentItem> textDocuments;
        private readonly IDictionary<Uri, string[]> textDocumentLines;
        private readonly IDictionary<Uri, IEnumerable<AsmFoldingRange>> foldingRanges;
        private readonly IDictionary<Uri, LabelGraph> labelGraphs;

        private readonly int referencesChunkSize = 10;
        private readonly int referencesDelayMs = 10;

        private readonly int highlightChunkSize = 10; // number of highlights returned before going to sleep for some delay
        private readonly int highlightsDelayMs = 10; // delay between highlight results returned

        private readonly TraceSource traceSource;

        private AsmDude2Tools asmDudeTools;
        public MnemonicStore mnemonicStore;
        public PerformanceStore performanceStore;
        public AsmLanguageServerOptions options;

        public LanguageServer(Stream sender, Stream reader)
        {
            this.traceSource = LogUtils.CreateTraceSource();
            //LogInfo("LanguageServer: constructor"); // This line produces a crash
            this.target = new LanguageServerTarget(this, traceSource);
            this.textDocuments = new Dictionary<Uri, TextDocumentItem>();
            this.textDocumentLines = new Dictionary<Uri, string[]>();

            this.labelGraphs = new Dictionary<Uri, LabelGraph>();            
            this.foldingRanges = new Dictionary<Uri, IEnumerable<AsmFoldingRange>>();
            this.diagnostics = new List<Diagnostic>();
            this.Symbols = Array.Empty<VSSymbolInformation>();


            this.messageHandler = new HeaderDelimitedMessageHandler(sender, reader);
            this.rpc = new JsonRpc(this.messageHandler, this.target);
            this.rpc.Disconnected += OnRpcDisconnected;

            this.rpc.ActivityTracingStrategy = new CorrelationManagerTracingStrategy()
            {
                TraceSource = traceSource,
            };
            this.rpc.TraceSource = traceSource;

            ((JsonMessageFormatter)this.messageHandler.Formatter).JsonSerializer.Converters.Add(new VSExtensionConverter<TextDocumentIdentifier, VSTextDocumentIdentifier>());

            this.rpc.StartListening();

            this.target.OnInitializeCompletion += OnTargetInitializeCompletion;
            this.target.OnInitialized += OnTargetInitialized;
        }

        private static string Truncate(string text)
        {
            if (text.Length < MAX_LENGTH_DESCR_TEXT)
            {
                return text;
            }
            return text.Substring(0, MAX_LENGTH_DESCR_TEXT) + "...";
        }

        private string[] GetLines(Uri uri)
        {
            if (this.textDocumentLines.TryGetValue(uri, out var lines))
            {
                return lines;
            }
            return Array.Empty<string>();
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

        private void OnTargetInitializeCompletion(object sender, EventArgs e)
        {
            LogInfo("LanguageServer: OnTargetInitializeCompletion");
            //Timer timer = new Timer(LogMessage, null, 0, 5 * 1000);
        }

        private void OnTargetInitialized(object sender, EventArgs e)
        {
            LogInfo("LanguageServer: OnTargetInitialized");
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        public void Initialize(AsmLanguageServerOptions options)
        {
            if (options == null)
            {
                LogError($"LanguageServer: Initialize: InitializationOptions is null");
                return;
            }
            //LogInfo($"Initialize: Options: {jToken}");
            this.options = options;
        }

        public void Initialized()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
            {
                string filename_Regular = Path.Combine(path, "signature-may2019.txt");
                string filename_Hand = Path.Combine(path, "signature-hand-1.txt");
                this.mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand, this.traceSource, this.options);
            }
            {
                string path_performance = Path.Combine(path, "Performance");
                this.performanceStore = new PerformanceStore(path_performance, this.traceSource, this.options);
            }
            {
                this.asmDudeTools = new AsmDude2Tools(path, this.traceSource);
            }
        }

        private void UpdateInternals(Uri uri)
        {
            TextDocumentItem document = GetTextDocument(uri);
            if (document != null) {
                if (this.textDocumentLines.ContainsKey(uri))
                {
                    this.textDocumentLines.Remove(uri);
                }
                this.textDocumentLines.Add(uri, document.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None));
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

        private char GetChar(string str, int offset)
        {
            return ((offset < 0) || (offset >= str.Length)) ? ' ' : str.ElementAt(offset);
        }

        private TextDocumentItem GetTextDocument(Uri uri)
        {
            if (this.textDocuments.TryGetValue(uri, out TextDocumentItem document))
            {
                return document;
            }
            return null;
        }

        public void OnTextDocumentOpened(DidOpenTextDocumentParams messageParams)
        {
            this.textDocuments.Add(messageParams.TextDocument.Uri, messageParams.TextDocument);
            this.UpdateInternals(messageParams.TextDocument.Uri);
        }

        public void OnTextDocumentClosed(DidCloseTextDocumentParams messageParams)
        {
            var uri = messageParams.TextDocument.Uri;
            this.textDocuments.Remove(uri);
            this.textDocumentLines.Remove(uri);
        }

        private void ScheduleDiagnosticMessage(
            string message, 
            DiagnosticSeverity severity, 
            Range range,
            TextDocumentIdentifier textDocumentIdentifier)
        {
            VSDiagnosticProjectInformation projectAndContext = null;
            if (textDocumentIdentifier != null
                && textDocumentIdentifier is VSTextDocumentIdentifier vsTextDocumentIdentifier
                && vsTextDocumentIdentifier.ProjectContext != null)
            {
                projectAndContext = new VSDiagnosticProjectInformation
                {
                    ProjectName = vsTextDocumentIdentifier.ProjectContext.Label,
                    ProjectIdentifier = vsTextDocumentIdentifier.ProjectContext.Id,
                    Context = "Win32"
                };
            }

            VSDiagnosticProjectInformation[] projects = null;


            if (projectAndContext != null)
            {
                projects = new VSDiagnosticProjectInformation[] { projectAndContext };
            }

            VSDiagnostic d = new VSDiagnostic
            {
                Message = message,
                Severity = severity,
                Range = range,
                //Code = "Error Code Here",
                //CodeDescription = new CodeDescription
                //{
                //    Href = new Uri("https://www.microsoft.com")
                //},

                Projects = projects,
                //Identifier = $"{lineNumber},{offsetStart} {lineNumber},{offsetEnd}",
                Tags = new DiagnosticTag[1] { (DiagnosticTag)AsmDiagnosticTag.IntellisenseError }
            };

            this.diagnostics.Add(d);
        }

        private void UpdateLabelGraph(Uri uri)
        {
            LogInfo("UpdateLabelGraph");

            if (this.labelGraphs.ContainsKey(uri))
            {
                this.labelGraphs.Remove(uri);
            }

            var textDocument = this.GetTextDocument(uri);
            string filename = textDocument.Uri.LocalPath;
            string[] lines = this.GetLines(uri);
            LabelGraph labelGraph = new LabelGraph(lines, filename, this.traceSource, this.options);


            foreach (var (key, value) in labelGraph.Label_Clashes)
            {
                LogInfo($"UpdateLabelGraph: found a label clash for label {value}");
                //TextDocumentIdentifier id = null;
                //Range range = null;
                //this.ScheduleDiagnosticMessage("label clash", DiagnosticSeverity.Error, range, id);
            }

            foreach (var (key, value) in labelGraph.Undefined_Labels)
            {
                LogInfo($"UpdateLabelGraph: found an undefined label {value}");
                //TextDocumentIdentifier id = null;
                //Range range = null;
                //this.ScheduleDiagnosticMessage("undefined clash", DiagnosticSeverity.Error, range, id);
            }

            this.labelGraphs.Add(uri, labelGraph);
        }

        private void UpdateFoldingRanges(Uri uri)
        {
            string NextWord(int startPos, string line)
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

            List<AsmFoldingRange> foldingRanges = new List<AsmFoldingRange>();
            Stack<int> startLineNumbers = new Stack<int>();
            Stack<int> startCharacters = new Stack<int>();

            var lines = this.GetLines(uri);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string lineStr = lines[lineNumber].ToUpper();
                int offsetRegion = lineStr.IndexOf(StartKeyword);
                if (offsetRegion != -1) {
                    startLineNumbers.Push(lineNumber);
                    startCharacters.Push(offsetRegion);
                }
                else
                {
                    int offsetEndRegion = lineStr.IndexOf(EndKeyword);
                    if (offsetEndRegion != -1)
                    {
                        if (startLineNumbers.Count() == 0)
                        {
                            var severity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity.Warning;
                            TextDocumentIdentifier textDocumentIdentifier = null;//TODO

                            string message = $"keyword {EndKeyword} has no matching {StartKeyword} keyword";
                            Range range = new Range()
                            {
                                Start = new Position(lineNumber, offsetEndRegion),
                                End = new Position(lineNumber, offsetEndRegion + endKeywordLength),
                            };
                            this.ScheduleDiagnosticMessage(message, severity, range, textDocumentIdentifier);
                        } else 
                        {
                            int startLine = startLineNumbers.Pop();
                            int startCharacter = startCharacters.Pop();
                            foldingRanges.Add(new AsmFoldingRange
                            {
                                StartLine = startLine,
                                StartCharacter = startCharacter,
                                EndLine = lineNumber,
                                EndCharacter = offsetEndRegion + endKeywordLength,
                                Kind = FoldingRangeKind.Region,
                                CollapsedText = NextWord(startCharacter + startKeywordLength + 1, lines[startLine]),
                            });
                        }
                    }
                }
            }
            this.SetFoldingRanges(foldingRanges, uri);
        }

        public void UpdateServerSideTextDocument(string text, int version, Uri uri)
        {
            TextDocumentItem document = GetTextDocument(uri);
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
            PublishDiagnosticParams parameter = new PublishDiagnosticParams
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
            CodeAction unresolvedAddText = new CodeAction
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
            List<Location> locations = new List<Location>();
            List<Location> locationsChunk = new List<Location>();

            for (int i = 0; i < lines.Length; i++)
            {
                string lineStr = lines[i];

                for (int j = 0; j < lineStr.Length; j++)
                {
                    Location location = GetLocation(lineStr, i, ref j, referenceWord, uri);

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
                IList<Operand> operands2,
                ISet<Arch> selectedArchitectures2)
        {
            foreach (AsmSignatureInformation asmSignatureElement in data)
            {
                bool allowed = true;

                //1] constrain on architecture
                if (!asmSignatureElement.Is_Allowed(selectedArchitectures2))
                {
                    allowed = false;
                }

                //2] constrain on operands
                if (allowed)
                {
                    if ((operands2 == null) || (operands2.Count == 0))
                    {
                        // do nothing
                    }
                    else
                    {
                        for (int i = 0; i < operands2.Count; ++i)
                        {
                            Operand operand = operands2[i];
                            if (operand != null)
                            {
                                if (!asmSignatureElement.Is_Allowed(operand, i))
                                {
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
 
            if (!options.SignatureHelp_On)
            {
                LogInfo($"OnTextDocumentSignatureHelp: switched off");
                return null;
            }

            var lines = this.GetLines(parameter.TextDocument.Uri);
            string lineStr = lines[parameter.Position.Line];
            LogInfo($"OnTextDocumentSignatureHelp: parameter={parameter}; line=\"{lineStr}\";");

            (string _, Mnemonic mnemonic, string[] _, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr);
            if ((mnemonic == Mnemonic.NONE) || (remark.Length > 0))
            {
                return null;
            }

            int offset = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            if (offset == -1)
            {
                LogError($"OnTextDocumentSignatureHelp: should not happen: investigate");
                return null;
            }

            offset += mnemonic.ToString().Length + 1;
            int length = parameter.Position.Character - offset;
            LogInfo($"OnTextDocumentSignatureHelp: offset={offset}; parameter.Position.Character={parameter.Position.Character}, length={length}");
            string[] args;

            if (length <= 0)
            {
                args = Array.Empty<string>();
            }
            else
            {
                string argsStr = lineStr.Substring(offset, length);
                args = argsStr.Split(',');
            }
            LogInfo($"OnTextDocumentSignatureHelp: current line: line=\"{lineStr}\"; mnemonic={mnemonic}, args={string.Join(",", args)}");

            IList<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
            ISet<Arch> selectedArchitectures = this.options.Get_Arch_Switched_On();

            IEnumerable<AsmSignatureInformation> x = this.mnemonicStore.GetSignatures(mnemonic);
            IEnumerable<AsmSignatureInformation> y = Constrain_Signatures(x, operands, selectedArchitectures);
            List<SignatureInformation> z = new List<SignatureInformation>();
            foreach (AsmSignatureInformation asmSignatureElement in y)
            {
                LogInfo($"OnTextDocumentSignatureHelp: adding SignatureInformation: {asmSignatureElement.SignatureInformation.Label}");
                z.Add(asmSignatureElement.SignatureInformation);
            }

            int nCommas = operands.Count;

            LogInfo($"OnTextDocumentSignatureHelp: args={args}");

            LogInfo($"OnTextDocumentSignatureHelp: lineStr\"{lineStr}\"; pos={parameter.Position.Character}; {mnemonic}, nCommas {nCommas}");
            return new SignatureHelp()
            {
                ActiveSignature = 0,
                ActiveParameter = nCommas,
                Signatures = z.ToArray<SignatureInformation>(),
            };
        }

        public void SetFoldingRanges(IEnumerable<AsmFoldingRange> foldingRanges, Uri uri)
        {
            if (this.foldingRanges.ContainsKey(uri))
            {
                this.foldingRanges.Remove(uri);
            }
            this.foldingRanges.Add(uri, foldingRanges);
        }

        public AsmFoldingRange[] GetFoldingRanges(FoldingRangeParams parameter)
        {
            if (!this.options.CodeFolding_On)
            {
                return Array.Empty<AsmFoldingRange>();
            }
            if (this.foldingRanges.TryGetValue(parameter.TextDocument.Uri, out IEnumerable<AsmFoldingRange> value))
            {
                return value.ToArray();
            }
            return Array.Empty<AsmFoldingRange>();
        }

        private IEnumerable<CompletionItem> Mnemonic_Operand_Completions(bool useCapitals, ISet<AsmSignatureEnum> allowedOperands, int lineNumber)
        {
            //bool use_AsmSim_In_Code_Completion = this.asmSimulator_.Enabled && Settings.Default.AsmSim_Show_Register_In_Code_Completion;
            bool att_Syntax = this.options.Used_Assembler == AssemblerEnum.NASM_ATT;

            SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

            foreach (Rn regName in this.mnemonicStore.Get_Allowed_Registers())
            {
                //string additionalInfo = null;
                if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
                {
                    string keyword = regName.ToString();
                    //if (use_AsmSim_In_Code_Completion && this.asmSimulator_.Tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(regName)))
                    //{
                    //    (string value, bool bussy) = this.asmSimulator_.Get_Register_Value(regName, lineNumber, true, false, false, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration, false));
                    //    if (!bussy)
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
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword : keyword.ToLowerInvariant();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this.asmDudeTools.Get_Description(keyword); //TODO add additional info
                    descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncate(keyword + archStr + descriptionStr);

                    completions.Add(new CompletionItem
                    {
                        Kind = GetCompletionItemKind(AsmTokenType.Register),
                        Label = displayText,
                        InsertText = insertionText,
                        SortText = insertionText,
                        Documentation = descriptionStr
                    });
                }
            }

            foreach (string keyword in this.asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this.asmDudeTools.Get_Token_Type_Intel(keyword);
                Arch arch = this.asmDudeTools.Get_Architecture(keyword);

                string keyword2 = keyword;
                bool selected = true;

                //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected +"; arch="+arch);

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
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword2 : keyword2.ToLowerInvariant();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this.asmDudeTools.Get_Description(keyword);
                    descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncate(keyword2 + archStr + descriptionStr);

                    completions.Add(new CompletionItem
                    {
                        Kind = GetCompletionItemKind(type),
                        Label = displayText,
                        InsertText = insertionText,
                        SortText = insertionText,
                        Documentation = descriptionStr
                    });
                }
            }
            return completions;
        }

        public CompletionList GetTextDocumentCompletion(CompletionParams parameter)
        {
            IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
            {
                SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

                //Add the completions of AsmDude directives (such as code folding directives)
                #region
                if (addSpecialKeywords && this.options.CodeFolding_On)
                {
                    {
                        string labelText = this.options.CodeFolding_BeginTag;     //the characters that start the outlining region
                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Macro,
                            Label = $"{labelText} - keyword to start code folding",
                            InsertText = labelText.Substring(1), // remove the prefix #
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
                            InsertText = labelText.Substring(1), // remove the prefix #
                            SortText = labelText,
                            //Documentation = $"keyword to end code folding",
                        });
                    }
                }
                #endregion

                AssemblerEnum usedAssember = this.options.Used_Assembler;

                #region Add completions
                if (selectedTypes.Contains(AsmTokenType.Mnemonic))
                {
                    foreach (Mnemonic mnemonic2 in this.mnemonicStore.Get_Allowed_Mnemonics())
                    {
                        string keyword_upcase = mnemonic2.ToString();
                        string insertionText = useCapitals ? keyword_upcase : keyword_upcase.ToLowerInvariant();
                        string archStr = ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic2));

                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Keyword,
                            Label = $"{keyword_upcase} {archStr}",
                            InsertText = insertionText,
                            SortText = insertionText,
                            Documentation = this.mnemonicStore.GetDescription(mnemonic2),
                        });
                    }
                }
                //Add the completions that are defined in the xml file
                foreach (string keyword_upcase in this.asmDudeTools.Get_Keywords())
                {
                    AsmTokenType type = this.asmDudeTools.Get_Token_Type_Intel(keyword_upcase);
                    if (selectedTypes.Contains(type))
                    {
                        Arch arch = Arch.ARCH_NONE;
                        bool selected = true;

                        if (type == AsmTokenType.Directive)
                        {
                            AssemblerEnum assembler = this.asmDudeTools.Get_Assembler(keyword_upcase);
                            if (assembler.HasFlag(AssemblerEnum.MASM))
                            {
                                if (!usedAssember.HasFlag(AssemblerEnum.MASM))
                                {
                                    selected = false;
                                }
                            }
                            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                            {
                                if (!usedAssember.HasFlag(AssemblerEnum.NASM_INTEL))
                                {
                                    selected = false;
                                }
                            }
                        }
                        else
                        {
                            arch = this.asmDudeTools.Get_Architecture(keyword_upcase);
                            selected = this.options.Is_Arch_Switched_On(arch);
                        }

                        LogInfo("CodeCompletionSource:Selected_Completions; keyword=" + keyword_upcase + "; arch=" + arch + "; selected=" + selected);

                        if (selected)
                        {
                            // by default, the entry.Key is with capitals
                            string insertionText = useCapitals ? keyword_upcase : keyword_upcase.ToLowerInvariant();
                            string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                            string descriptionStr = this.asmDudeTools.Get_Description(keyword_upcase);
                            descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                            string displayTextFull = keyword_upcase + archStr + descriptionStr;
                            string displayText = Truncate(displayTextFull);
 
                            completions.Add(new CompletionItem
                            {
                                Kind = GetCompletionItemKind(type),
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

            if (!this.options.CodeCompletion_On)
            {
                LogInfo($"OnTextDocumentCompletion: switched off");
                return new CompletionList();
            }

            var lines = this.GetLines(parameter.TextDocument.Uri);
            string lineStr = lines[parameter.Position.Line];
            //LogInfo($"OnTextDocumentCompletion: lineStr: \"{lineStr}\"");

            int pos = parameter.Position.Character - 1;
            char currentChar = GetChar(lineStr, pos);
            //LogInfo($"OnTextDocumentCompletion: currentChar={currentChar}");

            if (AsmTools.AsmSourceTools.IsSeparatorChar(currentChar))
            {
                return new CompletionList();
            }
            (string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr);

            LogInfo($"OnTextDocumentCompletion: label={label}; mnemonic={mnemonic}; args={args}; remark={remark}");

            List<CompletionItem> items = new List<CompletionItem>();

            if (remark.Length > 0)
            {
                // do not recommend anything while typing comments
            } else
            {
                if (mnemonic == Mnemonic.NONE)
                {
                    ISet<AsmTokenType> selected1 = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                    bool useCapitals = true; //TODO
                    items.AddRange(Selected_Completions(useCapitals, selected1, true));
                }
                else
                {
                    // the line contains a mnemonic but we are not in a comment
                    if (AsmTools.AsmSourceTools.IsJump(mnemonic))
                    {
                        //TODO select labels
                    } else
                    {
                        IList<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
                        ISet<Arch> selectedArchitectures = this.options.Get_Arch_Switched_On();
                        ISet<AsmSignatureEnum> allowed = new HashSet<AsmSignatureEnum>();
                        int nCommas = operands.Count;

                        IEnumerable<AsmSignatureInformation> allSignatures = this.mnemonicStore.GetSignatures(mnemonic);

                        foreach (AsmSignatureInformation se in this.Constrain_Signatures(allSignatures, operands, selectedArchitectures))
                        {
                            if (nCommas < se.Operands.Count)
                            {
                                foreach (AsmSignatureEnum s in se.Operands[nCommas])
                                {
                                    allowed.Add(s);
                                }
                            }
                        }
                        bool useCapitals = true; //TODO
                        IEnumerable<CompletionItem> completions = this.Mnemonic_Operand_Completions(useCapitals, allowed, parameter.Position.Line);
                        if (completions.Any())
                        {
                            items.AddRange(completions);
                        }
                    }
                }
            }

            return new CompletionList()
            {
                IsIncomplete = false,
                Items = items.ToArray(),
            };
        }

        public DocumentHighlight[] GetDocumentHighlights(IProgress<DocumentHighlight[]> progress, Position position, CancellationToken token, Uri uri)
        {
            if (progress == null)
            {
                LogInfo($"LanguageServer:GetDocumentHighlights: progress is null");
                return Array.Empty<DocumentHighlight>();
            }
            TextDocumentItem document = GetTextDocument(uri);
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
                LogInfo($"LanguageServer:GetDocumentHighlights: length too small ({length})");
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
            } else
            {
                foreach (string x in RegisterTools.GetRelatedRegisterNew(reg))
                {
                    currentHighlightedWords.Add(x);
                }
            }
            LogInfo($"LanguageServer:GetDocumentHighlights: currentHighlightedWords={string.Join(",", currentHighlightedWords)}");

            List<DocumentHighlight> highlights = new List<DocumentHighlight>();
            List<DocumentHighlight> chunk = new List<DocumentHighlight>();

            for (int i = 0; i < lines.Length; i++)
            {
                string lineStr = lines[i];

                for (int j = 0; j < lineStr.Length; j++)
                {
                    Range range = GetHighlightRangeMultiple(lineStr, i, ref j, currentHighlightedWords);
                    if (range != null)
                    {
                        j++;
                        DocumentHighlight highlight = new DocumentHighlight() { 
                            Range = range, 
                            Kind = DocumentHighlightKind.Text 
                        };
                        highlights.Add(highlight);
                        chunk.Add(highlight);

                        if (chunk.Count == highlightChunkSize)
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
            if (chunk.Count() > 0)
            {
                progress.Report(chunk.ToArray());
            }

            return highlights.ToArray();
        }

        private (int, int) FindWordBoundary(int position, string lineStr)
        {
            LogInfo($"FindWordBoundary: position = {position}; lineStr=\"{lineStr}\"");
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

            for (int i = position+1; i < lineLength; ++i)
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

        private AsmTokenType GetAsmTokenType(string keyword_upcase)
        {
            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_upcase, true);
            if (mnemonic != Mnemonic.NONE)
            {
                if (AsmTools.AsmSourceTools.IsJump(mnemonic))
                {
                    return AsmTokenType.Jump;
                }
                return AsmTokenType.Mnemonic;
            }
            if (RegisterTools.IsRn(keyword_upcase))
            {
                return AsmTokenType.Register;
            }

            //TODO labels and constants

            return AsmTokenType.UNKNOWN;
        }

        private (string, int, int) GetWord(int pos, string lineStr)
        {
            (int startPos, int endPos) = FindWordBoundary(pos, lineStr);
            int length = endPos - startPos;

            if (length <= 0)
            {
                return (String.Empty, -1, -1);
            }
            return (lineStr.Substring(startPos, length), startPos, endPos);
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
            string keyword_upcase = keyword.ToUpperInvariant();

            SumType<string, MarkedString>[] hoverContent = null;

            switch (GetAsmTokenType(keyword_upcase))
            {
                case AsmTokenType.Mnemonic: // intentional fall through
                case AsmTokenType.Jump:
                    {
                        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_upcase, true);
                        string mnemonicStr = mnemonic.ToString();
                        string archStr = ":" + ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));
                        string descr = this.mnemonicStore.GetDescription(mnemonic);
                        string full_Descr = AsmTools.AsmSourceTools.Linewrap($"{mnemonicStr} {archStr} {descr}", MaxNumberOfCharsInToolTips);
                        string performanceStr = "No performance info";

                        if (this.options.PerformanceInfo_On)
                        {
                            bool first = true;
                            string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                            MicroArch selectedMicroArchs = MicroArch.SkylakeX | MicroArch.Haswell;// AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                            foreach (PerformanceItem item in this.performanceStore.GetPerformance(mnemonic, selectedMicroArchs))
                            {
                                if (first)
                                {
                                    first = false;

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
                            new SumType<string, MarkedString>(new MarkedString
                            {
                                Language = MarkupKind.PlainText.ToString(),
                                Value = full_Descr + "\n",
                            }),
                            new SumType<string, MarkedString>(new MarkedString
                            {
                                Language = MarkupKind.Markdown.ToString(),
                                Value = "**Performance:**",
                            }),
                            new SumType<string, MarkedString>(new MarkedString
                            {
                                Language = MarkupKind.Markdown.ToString(),
                                Value = "```text\n" + performanceStr + "\n```",
                            })
                        };
                        break;
                    }
                case AsmTokenType.Register:
                    {
                       // int lineNumber = //AsmTools.AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                        if (keyword_upcase.StartsWith("%", StringComparison.Ordinal))
                        {
                            keyword_upcase = keyword_upcase.Substring(1); // remove the preceding % in AT&T syntax
                        }

                        Rn reg = RegisterTools.ParseRn(keyword_upcase, true);
                        if (this.mnemonicStore.RegisterSwitchedOn(reg))
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
                                new SumType<string, MarkedString>(new MarkedString
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
                        string descr = this.asmDudeTools.Get_Description(keyword_upcase);
                        if (descr.Length > 0)
                        {
                            if (keyword_upcase.Length > (MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }
                            descr = AsmTools.AsmSourceTools.Linewrap(descr, MaxNumberOfCharsInToolTips);
                            hoverContent = new SumType<string, MarkedString>[]{
                                new SumType<string, MarkedString>(new MarkedString
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
                        string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(labelPrefix, label, AsmDudeToolsStatic.Used_Assembler);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                            full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(extra_Tag_Info, label, AsmDudeToolsStatic.Used_Assembler);
                        }

                        AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: found label def " + full_Qualified_Label);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                            ? value + "d = " + value.ToString("X", AsmDudeToolsStatic.CultureUI) + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
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
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined1))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined2))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined3))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                description.FontSize = AsmDudeToolsStatic.GetFontSize() + 2;
                description.FontFamily = AsmDudeToolsStatic.GetFontType();
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                //quickInfoContent.Add(description);
                return (new List<object> { "other" }, keywordSpan.Value);
            }
            */

            if (hoverContent != null)
            {
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

            for (int line = 0; line < lines.Length; ++line)
            {
                string lineStr = lines[line];
                (string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr);
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
                                    Line = line,
                                    Character = pos,
                                },
                                End = new Position
                                {
                                    Line = line,
                                    Character = pos+label.Length,
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
                        HintText = "some hinttext here?",
                        Description = "some description here?",
                        Location = new Location
                        {
                            Uri = uri,
                            Range = new Range
                            {
                                Start = new Position
                                {
                                    Line = line,
                                    Character = pos,
                                },
                                End = new Position
                                {
                                    Line = line,
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
                                HintText = "some hinttext here?",
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
            VSProjectContextList result = new VSProjectContextList
            {
                ProjectContexts = this.Contexts.ToArray(),
                DefaultIndex = 0
            };

            return result;
        }

        CompletionItemKind GetCompletionItemKind(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Directive: return CompletionItemKind.Value;
                case AsmTokenType.Register: return CompletionItemKind.Variable;
                case AsmTokenType.Misc: return CompletionItemKind.Struct;
                default: return CompletionItemKind.None;
            }
        }

        #region Logging

        public void LogInfo(string message)
        {
            if (this.target.traceSetting == TraceSetting.Verbose)
            {
                this.traceSource.TraceEvent(TraceEventType.Information, 0, message);
            }
        }

        private void LogWarning(string message)
        {
            this.traceSource.TraceEvent(TraceEventType.Warning, 0, message);
        }

        private void LogError(string message)
        {
            this.traceSource.TraceEvent(TraceEventType.Error, 0, message);
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

        // store incoming settings from VS
        public void SendSettings(DidChangeConfigurationParams parameter)
        {
            this.CurrentSettings = parameter.Settings.ToString();
            this.NotifyPropertyChanged(nameof(CurrentSettings));

            JToken parsedSettings = JToken.Parse(this.CurrentSettings);
            int newMaxProblems = parsedSettings.Children().First().Values<int>("maxNumberOfProblems").First();

            LogInfo($"SendSettings: received {parameter}");
            if (this.maxProblems != newMaxProblems)
            {
                this.maxProblems = newMaxProblems;
            }
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
        //            Console.WriteLine($"Failed to apply edit: {response.FailureReason}");
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
                char before = GetChar(lineStr, characterOffset - 1);
                char after = GetChar(lineStr, characterOffset +  wordLength);

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
                var range = GetHighlightRange(line, lineOffset, ref characterOffset, wordToMatch);
                if (range != null)
                {
                    return range;
                }
            }
            return null;
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
