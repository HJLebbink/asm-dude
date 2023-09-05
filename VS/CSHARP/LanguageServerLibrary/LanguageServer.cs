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
using Microsoft.Win32;

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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LanguageServerLibrary
{
    public class LanguageServer : INotifyPropertyChanged
    {
        private const int MAX_LENGTH_DESCR_TEXT = 120;
        private const int MaxNumberOfCharsInToolTips = 150;
        public static readonly CultureInfo CultureUI = CultureInfo.CurrentUICulture;

        public int maxProblems = 10;

        private readonly JsonRpc rpc;
        private readonly HeaderDelimitedMessageHandler messageHandler;
        private readonly LanguageServerTarget target;
        private readonly ManualResetEvent disconnectEvent = new ManualResetEvent(false);
        private IList<DiagnosticsInfo> diagnostics;
        private readonly IDictionary<Uri, TextDocumentItem> textDocuments;
        private readonly IDictionary<Uri, string[]> textDocumentLines;
        private readonly IDictionary<Uri, IEnumerable<AsmFoldingRange>> foldingRanges;

        private string referenceToFind;
        private int referencesChunkSize;
        private int referencesDelay;

        private int highlightChunkSize; // number of highlights returned before going to sleep for some delay
        private int highlightsDelayMs; // delay between highlight results returned

        private readonly Dictionary<VSTextDocumentIdentifier, int> diagnosticsResults;
        private readonly TraceSource traceSource;

        private AsmDude2Tools asmDudeTools;
        public MnemonicStore mnemonicStore;
        public PerformanceStore performanceStore;
        public AsmLanguageServerOptions options;

        public LanguageServer(Stream sender, Stream reader, List<DiagnosticsInfo> initialDiagnostics = null)
        {
            this.traceSource = LogUtils.CreateTraceSource();
            //LogInfo("LanguageServer: constructor"); // This line produces a crash
            this.target = new LanguageServerTarget(this, traceSource);
            this.textDocuments = new Dictionary<Uri, TextDocumentItem>();
            this.textDocumentLines = new Dictionary<Uri, string[]>();
            this.foldingRanges = new Dictionary<Uri, IEnumerable<AsmFoldingRange>>();
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

            this.diagnostics = initialDiagnostics;
            this.diagnosticsResults = new Dictionary<VSTextDocumentIdentifier, int>();
            this.Symbols = Array.Empty<VSSymbolInformation>();

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
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "Resources");
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
                this.UpdateFoldingRanges(uri);
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

        public void SetFindReferencesParams(string wordToFind, int chunkSize, int delay = 0)
        {
            this.referenceToFind = wordToFind;
            this.referencesChunkSize = chunkSize;
            this.referencesDelay = delay;
        }

        public void SetDocumentHighlightsParams(int chunkSize, int delayMs = 0)
        {
            this.highlightChunkSize = chunkSize;
            this.highlightsDelayMs = delayMs;
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
                    if (startLineNumbers.Count() > 0) // TODO if startLineNumbers is empty we could give an error because of an closing region marker without an opening marker.
                    {
                        int offsetEndRegion = lineStr.IndexOf(EndKeyword);
                        if (offsetEndRegion != -1)
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
                            }); ;
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
            this.SendDiagnostics(this.diagnostics, uri);
        }

        public void SendDiagnostics(IList<DiagnosticsInfo> sentDiagnostics, Uri uri)
        {
            TextDocumentItem document = GetTextDocument(uri);
            if (document == null || sentDiagnostics == null || !this.UsePublishModelDiagnostic)
            {
                return;
            }

            List<Diagnostic> diagnostics = new List<Diagnostic>();
            var lines = this.GetLines(uri);
            for (int i = 0; i < lines.Length; i++)
            {
                string lineStr = lines[i];

                int j = 0;
                while (j < lineStr.Length)
                {
                    Diagnostic diagnostic = null;
                    foreach (DiagnosticsInfo tag in sentDiagnostics)
                    {
                        diagnostic = this.GetDiagnostic(lineStr, i, ref j, tag, textDocumentIdentifier: null);

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
            parameter.Uri = uri;
            parameter.Diagnostics = diagnostics.ToArray();

            if (this.maxProblems > -1)
            {
                parameter.Diagnostics = parameter.Diagnostics.Take(this.maxProblems).ToArray();
            }

            _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnostics, parameter);
        }

        public void SendDiagnostics2(Uri uri)
        {
            if (this.diagnostics == null || !this.UsePublishModelDiagnostic)
            {
                return;
            }
            var lines = this.GetLines(uri);
            IReadOnlyList<Diagnostic> diagnostics = this.GetDocumentDiagnostics(lines, textDocumentIdentifier: null);

            PublishDiagnosticParams parameter = new PublishDiagnosticParams
            {
                Uri = uri,
                Diagnostics = diagnostics.ToArray()
            };

            if (this.maxProblems > -1)
            {
                parameter.Diagnostics = parameter.Diagnostics.Take(this.maxProblems).ToArray();
            }

            _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnostics, parameter);
        }
       
        public void SendDiagnostics(List<DiagnosticsInfo> sentDiagnostics, bool pushDiagnostics, Uri uri)
        {
            if (this.diagnostics == null)
            {
                this.diagnostics = new List<DiagnosticsInfo>();
            }

            this.diagnostics = sentDiagnostics;

            if (pushDiagnostics)
            {
                this.SendDiagnostics(sentDiagnostics, uri);
            }
        }

        private IReadOnlyList<VSDiagnostic> GetDocumentDiagnostics(string[] lines, TextDocumentIdentifier textDocumentIdentifier)
        {
            List<VSDiagnostic> diagnostics = new List<VSDiagnostic>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                int j = 0;
                while (j < line.Length)
                {
                    VSDiagnostic diagnostic = null;
                    foreach (DiagnosticsInfo tag in this.diagnostics)
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

        public object[] SendReferences(ReferenceParams args, bool returnLocationsOnly, CancellationToken token, Uri uri)
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
            TextDocumentItem document = GetTextDocument(uri);
            if (document == null || progress == null)
            {
                return Array.Empty<object>();
            }

            //TODO why not use VSLocation??
            List<Location> locations = new List<Location>();
            List<Location> locationsChunk = new List<Location>();

            var lines = this.GetLines(uri);
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

        public SignatureHelp GetTextDocumentSignatureHelp(SignatureHelpParams parameter)
        {
            /// <summary>
            /// Constrain the list of signatures given: 1) the currently operands provided by the user; and 2) the selected architectures
            /// </summary>
            /// <param name="data"></param>
            /// <param name="operands"></param>
            /// <returns></returns>
            IEnumerable<AsmSignatureInformation> Constrain_Signatures(
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

            if (!options.SignatureHelp_On)
            {
                LogInfo($"OnTextDocumentSignatureHelp: switched off");
                return null;
            }

            var lines = this.GetLines(parameter.TextDocument.Uri);
            string lineStr = lines[parameter.Position.Line];
            LogInfo($"OnTextDocumentSignatureHelp: parameter={parameter}; line=\"{lineStr}\";");

            (string _, Mnemonic mnemonic, string[] _, string _) = AsmTools.AsmSourceTools.ParseLine(lineStr);
            if (mnemonic == Mnemonic.NONE)
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

        public CompletionList GetTextDocumentCompletion(CompletionParams parameter)
        {
            IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
            {
                SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

                //Add the completions of AsmDude directives (such as code folding directives)
                #region
                bool codeFoldingOn = true; //Settings.Default.CodeFolding_On
                if (addSpecialKeywords && codeFoldingOn)
                {
                    {
                        string labelText = "#region"; // Settings.Default.CodeFolding_BeginTag;     //the characters that start the outlining region
                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Keyword,
                            Label = labelText,
                            InsertText = labelText,
                            SortText = labelText,
                            Documentation = $"{labelText} - keyword to start code folding",
                        });
                    }
                    {
                        string labelText = "#endregion"; // Settings.Default.CodeFolding_EndTag;       //the characters that end the outlining region
                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Keyword,
                            Label = labelText,
                            InsertText = labelText,
                            SortText = labelText,
                            Documentation = $"{labelText} - keyword to end code folding",
                        });
                    }
                }
                #endregion

                // AssemblerEnum usedAssember = AssemblerEnum.NASM_INTEL; //AsmDudeToolsStatic.Used_Assembler;

                #region Add completions
                if (true)
                {
                    if (true)
                    //if (selectedTypes.Contains(AsmDude2.AsmTokenType.Mnemonic))
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
                }
                /*
                //Add the completions that are defined in the xml file
                foreach (string keyword_upcase in this.asmDudeTools_.Get_Keywords())
                {
                    AsmTokenType type = this.asmDudeTools_.Get_Token_Type_Intel(keyword_upcase);
                    if (selectedTypes.Contains(type))
                    {
                        Arch arch = Arch.ARCH_NONE;
                        bool selected = true;

                        if (type == AsmTokenType.Directive)
                        {
                            AssemblerEnum assembler = this.asmDudeTools_.Get_Assembler(keyword_upcase);
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
                            arch = this.asmDudeTools_.Get_Architecture(keyword_upcase);
                            selected = AsmDudeToolsStatic.Is_Arch_Switched_On(arch);
                        }

                        //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Selected_Completions; keyword=" + keyword + "; arch=" + arch + "; selected=" + selected);

                        if (selected)
                        {
                            //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");

                            // by default, the entry.Key is with capitals
                            string insertionText = useCapitals ? keyword_upcase : keyword_upcase.ToLowerInvariant();
                            string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                            string descriptionStr = this.asmDudeTools_.Get_Description(keyword_upcase);
                            descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                            string displayTextFull = keyword_upcase + archStr + descriptionStr;
                            string displayText = Truncate(displayTextFull);
                            //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                            this.icons_.TryGetValue(type, out ImageSource imageSource);
                            completions.Add(new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty));
                        }
                    }
                }
                */
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

            if (mnemonic == Mnemonic.NONE)
            {
                ISet<AsmTokenType> selected1 = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                bool useCapitals = true;
                items.AddRange(Selected_Completions(useCapitals, selected1, true));
            }

            this.IsIncomplete = false;
            return new CompletionList()
            {
                //IsIncomplete = this.IsIncomplete || (parameter.Context.TriggerKind == CompletionTriggerKind.TriggerForIncompleteCompletions),
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
            //LogInfo($"OnHover: lineStr=\"{lineStr}\"; startPos={startPos}; endPos={endPos}");

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

        //TODO use ElementAtOrDefault in FindWordBoundary
        private string GetWordAtPositionOLD(Position position, string[] lines)
        {
            string lineStr = lines.ElementAtOrDefault(position.Line);

            StringBuilder result = new StringBuilder();

            int startIdx = position.Character;
            int endIdx = startIdx + 1;


            while (char.IsLetter(lineStr.ElementAtOrDefault(startIdx)))
            {
                result.Insert(0, lineStr[startIdx]);
                startIdx--;
            }

            while (char.IsLetter(lineStr.ElementAtOrDefault(endIdx)))
            {
                result.Append(lineStr[endIdx]);
                endIdx++;
            }

            return result.ToString();
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

        public Hover GetHover(TextDocumentPositionParams parameter)
        {
            if (!this.options.AsmDoc_On)
            {
                LogInfo($"OnHover: switched off");
                return null;
            }
            var lines = this.GetLines(parameter.TextDocument.Uri);
            string lineStr = lines[parameter.Position.Line];
            (int startPos, int endPos) = FindWordBoundary(parameter.Position.Character, lineStr);
            int length = endPos - startPos;

            //LogInfo($"OnHover: lineStr=\"{lineStr}\"; startPos={startPos}; endPos={endPos}");

            if (length <= 0)
            {
                return null;
            }
            string keyword = lineStr.Substring(startPos, length).ToUpperInvariant();
            string keyword_upcase = keyword;
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
                                Value = "**Performance:**\n",
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
                            if (keyword.Length > (MaxNumberOfCharsInToolTips / 2))
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

        public void SetProjectContexts(IEnumerable<VSProjectContext> contexts)
        {
            this.Contexts = contexts;
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
            LogMessageParams parameter = new LogMessageParams
            {
                Message = message,
                MessageType = messageType
            };
            _ = this.SendMethodNotificationAsync(Methods.WindowLogMessage, parameter);
        }

        public void ShowMessage(string message, MessageType messageType)
        {
            //LogInfo($"LanguageServer: ShowMessage: message={message}; messageType={messageType.ToString()}");
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
                //this.SendDiagnostics();
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

        public void ApplyTextEdit(string text, Uri uri)
        {
            TextDocumentItem document = GetTextDocument(uri);
            if (document == null)
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
                                    Uri = uri,
                                },
                                Edits = addTextEdit,
                            },
                        }
                }
            };

            _ = Task.Run(async () =>
            {
                ApplyWorkspaceEditResponse response = await this.SendMethodRequestAsync(Methods.WorkspaceApplyEdit, parameter);

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
            //SEE https://github.com/microsoft/VSExtensibility/blob/main/docs/lsp/lsp-extensions-specifications.md


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
                string subString = line.Substring(characterOffset, wordToMatch.Length);
                if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase) && context == requestedContext)
                {
                    VSDiagnostic diagnostic = new VSDiagnostic();
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
