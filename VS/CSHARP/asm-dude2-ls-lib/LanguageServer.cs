// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmDude2LS;

public class LanguageServer : INotifyPropertyChanged, IDisposable
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
    private int _disposed = 0;
    private readonly List<Diagnostic> diagnostics;

    private readonly Dictionary<string, TextDocumentItem> textDocuments;
    private readonly Dictionary<string, string[]> textDocumentLines;
    private readonly Dictionary<string, KeywordID[][]> parsedDocuments;

    private readonly Dictionary<string, IEnumerable<FoldingRange>> foldingRanges;
    private readonly Dictionary<string, LabelGraph> labelGraphs;
    // Store assembler type per document for MASM/NASM-specific highlighting
    private readonly Dictionary<string, AssemblerEnum> _documentAssemblerTypes = new();
    private readonly HashSet<string> labelGraphDirty;

    private readonly int referencesChunkSize = 10;
    private readonly int referencesDelayMs = 10;

    private readonly int highlightChunkSize = 10; // number of highlights returned before going to sleep for some delay
    private readonly int highlightsDelayMs = 10; // delay between highlight results returned

    private readonly object updateLock = new();
    private readonly Dictionary<string, CancellationTokenSource> pendingUpdates = [];

    // Debounce for mnemonic doc URL opening (Ctrl+Click fires definition twice)
    private string? _lastDocUrl;
    private DateTime _lastDocTime = DateTime.MinValue;

    private readonly TraceSource traceSource;

    private AsmDude2Tools? asmDudeTools;
    public MnemonicStore? mnemonicStore;

    private readonly LspAsmSimulator asmSimulator_;
    public PerformanceStore? performanceStore;
    public AsmLanguageServerOptions? options;

    public static LanguageServer Create(Stream sender, Stream reader)
    {
        Instance ??= new LanguageServer(sender, reader);
        return Instance;
    }

    /// <summary>
    /// Creates a new LanguageServer instance for unit/integration testing, bypassing the singleton.
    /// Each test should call this to get an isolated server connected to its own streams.
    /// </summary>
    internal static LanguageServer CreateForTest(Stream sender, Stream reader)
        => new(sender, reader);

    private static LanguageServer? Instance { get; set; }

    private LanguageServer(Stream sender, Stream reader)
    {
        this.traceSource = Tools.CreateTraceSource();

        // Log build timestamp to verify we're not running stale code
        try
        {
            var asm = typeof(LanguageServer).Assembly;
            var buildTime = System.IO.File.GetLastWriteTimeUtc(asm.Location);
            Console.Error.WriteLine($"[LanguageServer INIT] BuildTime={buildTime:yyyy-MM-dd HH:mm:ss.fff} UTC, Assembly={System.IO.Path.GetFileName(asm.Location)}");
        }
        catch { }

        //LogInfo("LanguageServer: constructor"); // This lineNumber produces a crash
        this.target = new LanguageServerTarget(this);
        this.textDocuments = [];
        this.textDocumentLines = [];
        this.parsedDocuments = [];

        this.labelGraphs = [];
        this.labelGraphDirty = [];
        this.foldingRanges = [];
        this.diagnostics = [];
        this.Symbols = [];

        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        formatter.JsonSerializerOptions.Converters.Add(new ColorJsonConverter());
        this.messageHandler = new HeaderDelimitedMessageHandler(sender, reader, formatter);
        this.rpc = new JsonRpc(this.messageHandler, this.target);
        this.rpc.Disconnected += this.OnRpcDisconnected;

        /* 30-09-23 why would we need the following code?
        rpc.ActivityTracingStrategy = new CorrelationManagerTracingStrategy()
        {
            TraceSource = traceSource,
        };
        rpc.TraceSource = traceSource;
        */

        // Note: VSExtensionConverter is not available in LspTypes package
        // The VS-specific type conversion is handled manually where needed

        // Always log startup info to stderr for debugging (regardless of trace setting)
        Console.Error.WriteLine($"LanguageServer: Starting RPC listener. Sender CanWrite={sender.CanWrite}, Reader CanRead={reader.CanRead}");
        this.rpc.StartListening();
        Console.Error.WriteLine("LanguageServer: RPC listener started");

        this.target.OnInitializeCompletion += this.OnTargetInitializeCompletion;
        this.target.OnInitialized += this.OnTargetInitialized;
        this.asmSimulator_ = new LspAsmSimulator(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }

    /// <summary>
    /// Internal constructor for unit testing - initializes basic state without streams/RPC
    /// </summary>
    internal LanguageServer()
    {
        this.traceSource = Tools.CreateTraceSource();
        this.textDocuments = [];
        this.textDocumentLines = [];
        this.parsedDocuments = [];
        this.labelGraphs = [];
        this.labelGraphDirty = [];
        this.foldingRanges = [];
        this.diagnostics = [];
        this.Symbols = [];
        this.asmSimulator_ = new LspAsmSimulator(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
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

        ReadOnlySpan<char> lineSpan = lineStr.AsSpan();
        int startPos = 0;
        int endPos = lineLength;

        for (int i = position + 1; i < lineLength; ++i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineSpan[i]))
            {
                endPos = i;
                break;
            }
        }
        for (int i = position; i >= 0; --i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineSpan[i]))
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
        // Avoid allocation if span can be used directly (returning as string for API compatibility)
        return (lineStr.Substring(startPos, length), startPos, endPos);
    }

    private static string Truncate(string text, int maxLength = MAX_LENGTH_DESCR_TEXT)
    {
        return (text.Length < maxLength) ? text : string.Concat(text.AsSpan(0, maxLength), "...");
    }

    private char GetChar(string str, int offset)
    {
        return ((offset < 0) || (offset >= str.Length)) ? ' ' : str.AsSpan()[offset];
    }

    #endregion

    private string[] GetLines(string uri)
    {
        if (this.textDocumentLines.TryGetValue(uri, out var lines))
        {
            return lines;
        }
        return [];
    }

    private TextDocumentItem? GetTextDocument(string uri)
    {
        if (this.textDocuments.TryGetValue(uri, out TextDocumentItem? document))
        {
            return document;
        }
        return null;
    }

    private LabelGraph? GetLabelGraph(string uri)
    {
        if (this.labelGraphDirty.Remove(uri))
        {
            this.UpdateLabelGraph(uri);
        }
        if (this.labelGraphs.TryGetValue(uri, out var graph))
        {
            return graph;
        }
        return null;
    }

    public static VSDiagnosticProjectInformation[]? GetVSDiagnosticProjectInformation(VSTextDocumentIdentifier? vsTextDocumentIdentifier)
    {
        VSDiagnosticProjectInformation? projectAndContext = null;
        if ((vsTextDocumentIdentifier != null) && (vsTextDocumentIdentifier.ProjectContext != null))
        {
            projectAndContext = new VSDiagnosticProjectInformation
            {
                ProjectName = vsTextDocumentIdentifier.ProjectContext.Label,
                ProjectIdentifier = vsTextDocumentIdentifier.ProjectContext.Id,
                Context = "Win32"
            };
        }
        return (projectAndContext == null) ? null : [projectAndContext];
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
            Tags = [(DiagnosticTag)AsmDiagnosticTag.IntellisenseError]
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
    } = [];

    public bool UsePublishModelDiagnostic { get; set; } = true;

    public event EventHandler? OnInitialized;
    public event EventHandler? Disconnected;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ShowWindow;

    private void OnTargetInitializeCompletion(object sender, EventArgs e)
    {
        LogInfo("LanguageServer: OnTargetInitializeCompletion");
    }

    /// <summary>
    /// Called when initialization is complete. This is called directly instead of using
    /// the OnInitializeCompletion event because StreamJsonRpc proxies events as notifications.
    /// </summary>
    public void OnInitializeComplete()
    {
        LogInfo("LanguageServer: OnInitializeComplete");
    }

    private void OnTargetInitialized(object sender, EventArgs e)
    {
        LogInfo("LanguageServer: OnTargetInitialized");
        this.OnInitialized?.Invoke(this, EventArgs.Empty);
    }

    public void Initialize(AsmLanguageServerOptions options)
    {
        Debug.Assert(options != null);
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
            this.WriteMnemonicUrlMapping();
        }
        {
            string path_performance = Path.Combine(path, "Performance");
            this.performanceStore = new PerformanceStore(path_performance, this.options);
        }
        {
            this.asmDudeTools = AsmDude2Tools.Create(path, this.traceSource);
        }
    }

private void UpdateInternals(string uri)
        {
            Console.Error.WriteLine($"[UpdateInternals] ENTRY: uri={uri}");
            LogToFile($"[UpdateInternals] ENTRY: uri={uri}");
            LogInfo($"[UpdateInternals] uri={uri}");
            var document = this.GetTextDocument(uri);
            Console.Error.WriteLine($"[UpdateInternals] GetTextDocument returned: {(document != null ? "NOT NULL" : "NULL")}");
            LogToFile($"[UpdateInternals] GetTextDocument returned: {(document != null ? "NOT NULL" : "NULL")}");
            if (document is TextDocumentItem)
            {
                var newLines = document.Text.Split(separator, StringSplitOptions.None);
                LogToFile($"[UpdateInternals] Split into {newLines.Length} lines");
                LogInfo($"[UpdateInternals] Split into {newLines.Length} lines");

                // Detect assembler type for this document
                AssemblerEnum assemblerType = AssemblerEnum.UNKNOWN;
                if (this.options != null && this.options.useAssemblerAutoDetect)
                {
                    // Simple heuristic: check for MASM/NASM specific directives in first few lines
                    int linesToCheck = Math.Min(20, newLines.Length);
                    for (int i = 0; i < linesToCheck; i++)
                    {
                        string line = newLines[i].Trim().ToUpperInvariant();
                        if (line.Length == 0) continue;
                        
                        // Check for MASM-specific indicators
                        if (line.StartsWith("PROC") || line.StartsWith("ENDP") || line.StartsWith("MACRO") || 
                            line.StartsWith("ENDM") || line.StartsWith("SEGMENT") || line.StartsWith("ENDS") ||
                            line.StartsWith("ASSUME") || line.StartsWith("ORG") || line.Contains(" PTR ") || 
                            line.StartsWith("EXTERN") || line.StartsWith("EXTRN") || line.StartsWith("PUBLIC"))
                        {
                            assemblerType |= AssemblerEnum.MASM;
                            break;
                        }
                        
                        // Check for NASM-specific indicators
                        if (line.StartsWith("SECTION") || line.StartsWith("SEGMENT") || line.StartsWith("ABSOLUTE") ||
                            line.StartsWith("EXTERN") || line.StartsWith("GLOBAL") || line.StartsWith("COMMON") ||
                            line.StartsWith("CPU") || line.StartsWith("GROUP") || line.Contains(" EQU ") ||
                            line.StartsWith("RES") || line.StartsWith("TIMES") || line.StartsWith("%include") ||
                            line.StartsWith("%define") || line.StartsWith("%ifdef") || line.StartsWith("%ifndef"))
                        {
                            assemblerType |= (AssemblerEnum.NASM_INTEL | AssemblerEnum.NASM_ATT);
                            break;
                        }
                    }
                }
                
                // Store the detected assembler type for this document
                this._documentAssemblerTypes[uri] = assemblerType;

                string[] oldLines;
                if (this.textDocumentLines.TryGetValue(uri, out var cachedLines) && cachedLines.SequenceEqual(newLines))
                {
                    LogToFile($"[UpdateInternals] Lines unchanged, returning early");
                    LogInfo($"[UpdateInternals] Lines unchanged, returning early");
                    return;
                }
                else
                {
                    oldLines = cachedLines ?? [];
                }

                this.textDocumentLines.Remove(uri);
                this.textDocumentLines.Add(uri, newLines);

                KeywordID[][] lineData;
                if (this.parsedDocuments.TryGetValue(uri, out var oldParsed) && oldParsed.Length == newLines.Length)
                {
                    lineData = new KeywordID[newLines.Length][];
                    for (int lineNumber = 0; lineNumber < newLines.Length; ++lineNumber)
                    {
                        if (lineNumber < oldParsed.Length && oldLines[lineNumber] == newLines[lineNumber] && oldParsed[lineNumber] != null)
                        {
                            lineData[lineNumber] = oldParsed[lineNumber];
                        }
                        else
                        {
                            int fileID = 0;
                            lineData[lineNumber] = AsmTools.AsmSourceTools.ParseLine(newLines[lineNumber], lineNumber, fileID, assemblerType).keywords;
                        }
                    }
                }
                else
                {
                    lineData = new KeywordID[newLines.Length][];
                    int fileID = 0;
                    for (int lineNumber = 0; lineNumber < newLines.Length; ++lineNumber)
                    {
                        lineData[lineNumber] = AsmTools.AsmSourceTools.ParseLine(newLines[lineNumber], lineNumber, fileID, assemblerType).keywords;
                    }
                }

                this.parsedDocuments.Remove(uri);
                this.parsedDocuments.Add(uri, lineData);

                this.diagnostics.Clear();
                this.UpdateFoldingRanges(uri);
                this.labelGraphDirty.Add(uri);
                Console.Error.WriteLine($"[UpdateInternals] About to call InvalidateAndSimulate: asmSimulator_={this.asmSimulator_ != null}, lines={newLines.Length}");
                LogToFile($"[UpdateInternals] About to call InvalidateAndSimulate with {newLines.Length} lines, asmSimulator_={this.asmSimulator_ != null}");
                LogInfo($"[UpdateInternals] Calling InvalidateAndSimulate with {newLines.Length} lines");
                try
                {
                    this.asmSimulator_.InvalidateAndSimulate(new Uri(uri), newLines,
                        onCompleted: completedUri => this.SendDiagnostics(completedUri.ToString()));
                    Console.Error.WriteLine($"[UpdateInternals] InvalidateAndSimulate returned");
                    LogToFile($"[UpdateInternals] InvalidateAndSimulate returned");
                    LogInfo($"[UpdateInternals] InvalidateAndSimulate returned");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[UpdateInternals] EXCEPTION in InvalidateAndSimulate: {ex.GetType().Name}: {ex.Message}");
                    LogToFile($"[UpdateInternals] EXCEPTION in InvalidateAndSimulate: {ex.GetType().Name}: {ex.Message}");
                    LogToFile($"[UpdateInternals] Stack: {ex.StackTrace}");
                    LogInfo($"[UpdateInternals] EXCEPTION: {ex.Message}");
                    throw;
                }

                if (false)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    this.UpdateSymbols(uri);
#pragma warning restore CS0162 // Unreachable code detected
                }
                this.SendDiagnostics(uri);
                LogToFile($"[UpdateInternals] EXIT SUCCESS");
            }
            else
            {
                LogToFile($"[UpdateInternals] EXIT - document is null!");
            }
        }

    public void OnTextDocumentOpened(DidOpenTextDocumentParams messageParams)
    {
        var uri = messageParams.TextDocument.Uri.ToString();
        LogToFile($"[OnTextDocumentOpened] uri={uri}, textLength={messageParams.TextDocument.Text?.Length ?? 0}");
        LogInfo($"[OnTextDocumentOpened] uri={uri}, textLength={messageParams.TextDocument.Text?.Length ?? 0}");

        this.textDocuments.Add(uri, messageParams.TextDocument);
        this.UpdateInternals(uri);
    }

    public void OnTextDocumentClosed(DidCloseTextDocumentParams messageParams)
    {
        var uri = messageParams.TextDocument.Uri.ToString();
        this.textDocuments.Remove(uri);
        this.textDocumentLines.Remove(uri);
        this.parsedDocuments.Remove(uri);
        this.labelGraphs.Remove(uri);
        this.labelGraphDirty.Remove(uri);
    }

    private void UpdateLabelGraph(string uri)
    {
        LogInfo("UpdateLabelGraph");
        this.labelGraphs.Remove(uri);

        TextDocumentItem? textDocument = this.GetTextDocument(uri);
        string filename = new Uri(textDocument.Uri.ToString()).LocalPath;
        string[] lines = this.GetLines(uri);
        bool caseSensitiveLabels = true; //nasm has case sensitive labels
        LabelGraph labelGraph = new(lines, filename, caseSensitiveLabels, this.options);
        if (false)
        { // TODO 30-09-23: switch on the label diagnostics when most of the false positives are removed
#pragma warning disable CS0162 // Unreachable code detected
            labelGraph.UpdateDiagnostics();
            this.diagnostics.AddRange(labelGraph.Diagnostics);
#pragma warning restore CS0162 // Unreachable code detected
        }
        this.labelGraphs.Add(uri, labelGraph);
    }

    private void UpdateFoldingRanges(string uri)
    {
        if (!this.options.CodeFolding_On)
        {
            return;
        }
        string StartKeyword = this.options.CodeFolding_BeginTag.ToUpper();
        string EndKeyword = this.options.CodeFolding_EndTag.ToUpper();

        int startKeywordLength = StartKeyword.Length;
        int endKeywordLength = EndKeyword.Length;

        List<FoldingRange> foldingRanges = [];
        Stack<int> startLineNumbers = new();
        Stack<int> startCharacters = new();
        Stack<string> collapsedTexts = new();

        var lines = this.GetLines(uri);
        for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
        {
            string lineStr = lines[lineNumber].ToUpper();
            int offsetRegion = lineStr.AsSpan().IndexOf(StartKeyword);
            if (offsetRegion != -1)
            {
                startLineNumbers.Push(lineNumber);
                startCharacters.Push(offsetRegion);
                // Extract the text after the #region keyword as collapsed text
                int textStart = offsetRegion + startKeywordLength;
                string collapsedText = textStart < lines[lineNumber].Length
                    ? lines[lineNumber][textStart..].Trim()
                    : string.Empty;
                collapsedTexts.Push(collapsedText.Length > 0 ? collapsedText : "...");
            }
            else
            {
                int offsetEndRegion = lineStr.AsSpan().IndexOf(EndKeyword);
                if (offsetEndRegion != -1)
                {
                    if (startLineNumbers.Count == 0)
                    {
                        var severity = DiagnosticSeverity.Warning;
                        VSTextDocumentIdentifier? textDocumentIdentifier = null;//TODO

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
                        string collapsedText = collapsedTexts.Pop();
                        foldingRanges.Add(new FoldingRange
                        {
                            StartLine = startLine,
                            StartCharacter = startCharacter,
                            EndLine = lineNumber,
                            EndCharacter = offsetEndRegion + endKeywordLength,
                            Kind = FoldingRangeKind.Region,
                            CollapsedText = collapsedText,
                        });
                    }
                }
            }
        }
        this.SetFoldingRanges(foldingRanges, uri);
    }

    public void UpdateServerSideTextDocument(string text, int version, string uri)
    {
        LogInfo($"[UpdateServerSideTextDocument] uri={uri}, version={version}, textLength={text.Length}");
        TextDocumentItem? document = this.GetTextDocument(uri);
        if (document != null)
        {
            document.Text = text;
            document.Version = version;
            LogInfo($"[UpdateServerSideTextDocument] Updated document, scheduling debounced update");

            // Debounce document updates - cancel pending update and schedule new one
            lock (this.updateLock)
            {
                if (this.pendingUpdates.TryGetValue(uri, out var cts))
                {
                    LogInfo($"[UpdateServerSideTextDocument] Cancelling previous pending update");
                    cts.Cancel();
                    this.pendingUpdates.Remove(uri);
                }

                var newCts = new CancellationTokenSource();
                this.pendingUpdates[uri] = newCts;

                // Schedule update after 100ms of inactivity
                Task.Delay(100, newCts.Token).ContinueWith(_ =>
                {
                    if (!newCts.IsCancellationRequested)
                    {
                        LogInfo($"[UpdateServerSideTextDocument] 100ms debounce timeout reached, calling UpdateInternals");
                        this.UpdateInternals(uri);
                        lock (this.updateLock)
                        {
                            this.pendingUpdates.Remove(uri);
                        }
                    }
                    else
                    {
                        LogInfo($"[UpdateServerSideTextDocument] Update was cancelled during debounce");
                    }
                }, TaskScheduler.Default);
            }
        }
    }

    public void SendDiagnostics(string uri)
    {
        var simDiags = this.asmSimulator_.GetDiagnostics(new Uri(uri));
        var allDiags = new List<Diagnostic>(this.diagnostics);
        foreach (SimDiagnostic sd in simDiags)
        {
            DiagnosticSeverity severity = sd.Kind switch
            {
                SimDiagnosticKind.SyntaxError => DiagnosticSeverity.Error,
                SimDiagnosticKind.NotImplemented => DiagnosticSeverity.Information,
                _ => DiagnosticSeverity.Warning,
            };
            allDiags.Add(new VSDiagnostic
            {
                Message = sd.Message,
                Severity = severity,
                Range = new Range
                {
                    Start = new Position(sd.Line, 0),
                    End = new Position(sd.Line, int.MaxValue),
                },
            });
        }

        PublishDiagnosticParams parameter = new()
        {
            Uri = new Uri(uri),
            Diagnostics = [.. allDiags],
        };
        _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnosticsName, parameter);
    }

    public CodeAction? GetResolvedCodeAction(CodeAction parameter)
    {
        // When using System.Text.Json, Data comes as a JsonElement
        if (parameter.Data is System.Text.Json.JsonElement jsonElement)
        {
            CodeAction? resolvedCodeAction = System.Text.Json.JsonSerializer.Deserialize<CodeAction>(jsonElement.GetRawText());
            return resolvedCodeAction;
        }
        return parameter;
    }

    public object[] GetCodeActions(CodeActionParams parameter)
    {
        var uri = parameter.TextDocument.Uri.ToString();
        var lines = this.GetLines(uri);
        if (lines == null || lines.Length == 0) return [];

        // Check if cursor is on a mnemonic that has documentation
        int line = (int)parameter.Range.Start.Line;
        if (line >= lines.Length) return [];

        var (word, _, _) = GetWord((int)parameter.Range.Start.Character, lines[line]);
        if (string.IsNullOrEmpty(word)) return [];

        string wordUpper = word.ToUpperInvariant();
        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(wordUpper, true);
        if (mnemonic == Mnemonic.NONE) return [];

        if (!this.options.AsmDoc_On) return [];

        string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
        if (string.IsNullOrEmpty(htmlRef)) return [];

        string fullUrl = this.options.AsmDoc_Url.TrimEnd('/') + "/" + htmlRef;

        CodeAction openDocsAction = new()
        {
            Title = $"Open {mnemonic} Documentation",
            Kind = CodeActionKind.QuickFix,
            Command = new Command
            {
                Title = $"Open {mnemonic} Documentation",
                CommandIdentifier = "asmdude2.openDocumentation",
                Arguments = [fullUrl],
            },
        };

        return [openDocsAction];

        /* Disabled demo code actions — kept for reference
        #region File Operation actions

        var documentUri = parameter.TextDocument.Uri;
        string absolutePath = new Uri(documentUri.ToString()).LocalPath.TrimStart('/');
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
        TextEdit[] addTextEdit =
        [
            new TextEdit
                {
                    Range = new Range
                    {
                        Start = new Position(0, 0),
                        End = new Position(0, 0)
                    },
                    NewText = "Added text!"
                }
        ];

        var textEdits = addTextEdit;

        CodeAction addTextAction = new()
        {
            Title = "Add Text Action - DocumentChanges property",
            Edit = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                    {
                            new()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = parameter.TextDocument.Uri,
                                },
                                Edits = textEdits,
                            },
                    }
            },
            Kind = CodeActionKind.QuickFix,
        };

        Dictionary<string, TextEdit[]> changes = new()
            {
                { parameter.TextDocument.Uri.ToString(), addTextEdit }
            };

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
                            new()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = parameter.TextDocument.Uri,
                                },
                                Edits =
                                    [
                                        new TextEdit
                                        {
                                            Range = new Range
                                            {
                                                Start = new Position(0, 0),
                                                End = new Position(0, 0)
                                            },
                                            NewText = "_"
                                        }
                                    ]
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
            Diagnostics =
            [
                new Diagnostic()
                    {
                        Range = new Range
                        {
                            Start = new Position(0, 0),
                            End = new Position(0, 0)
                        },
                        Message = "Test Error",
                        Severity = DiagnosticSeverity.Error,
                    }
            ],
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
                            new()
                            {
                                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                                {
                                    Uri = new Uri(editFilePath),
                                },
                                Edits = textEdits,
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

        return [
                addTextAction,
                addTextActionChangesProperty,
                addTextActionWithError,
                addTextActionToOtherFile,
                unresolvedAddText,
                addUnderscoreAction,
                createFileAction,
                renameFileAction,
            ];
        */ // end disabled demo code actions
    }

    public object[] SendReferences(ReferenceParams args, bool returnLocationsOnly, CancellationToken token)
    {
        if (target.traceSetting == TraceSetting.Verbose)
        {
            LogInfo($"Received: {System.Text.Json.JsonSerializer.Serialize(args)}");
        }
        var uri = args.TextDocument.Uri.ToString();

        var lines = this.GetLines(uri);
        if ((int)args.Position.Line >= lines.Length) return [];
        var (referenceWord, _, _) = GetWord((int)args.Position.Character, lines[(int)args.Position.Line]);
        if (referenceWord.Length == 0)
        {
            return [];
        }

        // PartialResultToken is a token, not an IProgress object
        // For now, we'll use a simple progress adapter
        IProgress<object[]> progress = new Progress<object[]>(_ => { });
        int delay = this.referencesDelayMs;

        //TODO why not use VSLocation??
        List<Location> locations = [];
        List<Location> locationsChunk = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string lineStr = lines[i];

            for (int j = 0; j < lineStr.Length; j++)
            {
                Location? location = this.GetLocation(lineStr, i, ref j, referenceWord, new Uri(uri));
                if (location != null)
                {
                    locations.Add(location);
                    locationsChunk.Add(location);

                    if (locationsChunk.Count == this.referencesChunkSize)
                    {
                        Debug.WriteLine($"Reporting references of {referenceWord}");
                        this.rpc.TraceSource.TraceEvent(TraceEventType.Information, 0, $"Report: {System.Text.Json.JsonSerializer.Serialize(locationsChunk)}");
                        progress.Report(locationsChunk.ToArray());
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
        if (locationsChunk.Count > 0)
        {
            progress.Report([.. locationsChunk]);
        }

        return [.. locations];
    }

    /// <summary>
    /// Constrain the list of signatures given: 1) the currently operands provided by the user; and 2) the selected architectures
    /// </summary>
    /// <param name="data">All available signatures for a mnemonic from MnemonicStore.GetSignatures.</param>
    /// <param name="operands2">Current operand list from parser (parsed instruction arguments).</param>
    /// <param name="selectedArchitectures2">Architectures enabled in options (ARCH_8086, ARCH_X64, etc.).</param>
    /// <returns>Filtered sequence of compatible signatures matching operand constraints and architecture support.</returns>
    /// <remarks>
    /// Constraint logic applied in order:
    ///   1. Remove signatures not supporting selected architectures via Is_Allowed(selectedArchitectures2)
    ///   2. Check each operand against signature operand definitions via Is_Allowed(operand, i)
    ///   3. Return only signatures matching ALL operand constraints (yield return for deferred execution)
    /// 
    /// Yield return allows memory-efficient lazy evaluation—only processes signatures as caller enumerates.
    ///</remarks>
    /// <example>
    /// var signatures = mnemonicStore.GetSignatures(Mnemonic.MOV);
    /// var operands = MakeOperands(new string[] { "eax", "ebx" });
    /// var archs = options.Get_Arch_Switched_On();
    /// var filtered = Constrain_Signatures(signatures, operands, archs);
    /// // Returns only MOV signatures compatible with EAX/EBX registers and selected architectures
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: signature constraint, architecture filter, operand matching, filtering algorithm
    /// USED IN: GetTextDocumentSignatureHelp, GetTextDocumentCompletion
    /// SEE ALSO: MnemonicStore.GetSignatures, AsmSignatureInformation.Is_Allowed, Operand
    private IEnumerable<AsmSignatureInformation> Constrain_Signatures(
            IEnumerable<AsmSignatureInformation> data,
            List<Operand> operands2,
            HashSet<Arch> selectedArchitectures2)
    {
#if DEBUG
        bool extraLogging = false;
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

    /// <summary>
    /// Get signature help for the current position in a document (LSP textDocument/signatureHelp).
    /// Returns available method signatures with active parameter highlighted.
    /// </summary>
    /// <param name="parameter">Signature help request with document URI and cursor position.</param>
    /// <returns>SignatureHelp with active signature, active parameter, and available signatures; null if disabled or no signatures.</returns>
    /// <remarks>
    /// Process:
    ///   1. Parse line up to cursor to get mnemonic and current arguments
    ///   2. Get all signatures for mnemonic from MnemonicStore
    ///   3. Constrain signatures based on 1] selected architectures, 2] provided operands
    ///   4. Count commas to determine active parameter index
    /// 
    /// SignatureHelp is used when typing function/method calls to show parameter hints.
    /// </remarks>
    /// <example>
    /// User types: "mov eax, "
    /// Returns: { ActiveSignature: 0, ActiveParameter: 1, Signatures: [MOV r16/r32, r/m16/r32, ...] }
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: signature help, parameter hints, LSP, mnemonic parsing, operand constraints
    /// USED IN: LanguageServerTarget.TextDocumentSignatureHelp
    /// SEE ALSO: Constrain_Signatures, MnemonicStore.GetSignatures, SignatureHelpParams
    public SignatureHelp? GetTextDocumentSignatureHelp(SignatureHelpParams parameter)
    {
        try
        {
#if DEBUG
            bool extraLogging = false;
#else
                bool extraLogging = false;
#endif

            if (!this.options.SignatureHelp_On)
            {
                if (target.traceSetting == TraceSetting.Verbose)
                {
                    LogInfo($"GetTextDocumentSignatureHelp: switched off");
                }
                return null;
            }

            var lines = this.GetLines(parameter.TextDocument.Uri.ToString());
            int lineNumber = (int)parameter.Position.Line;
            if (lineNumber >= lines.Length) return null;
            string completeLineStr = lines[lineNumber];
            int pos = (int)parameter.Position.Character;
            string lineStr = completeLineStr[..pos];

            if (extraLogging && parameter.Context != null)
            {
                LogInfo($"GetTextDocumentSignatureHelp: TriggerKind={parameter.Context.TriggerKind}; triggerChar={parameter.Context.TriggerCharacter}; IsRetrigger={parameter.Context.IsRetrigger}");
            }

            int fileID = 0; //TODO
            (object _, string _, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID, AssemblerEnum.UNKNOWN);
            if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: lineStr=\"{lineStr}\"; mnemonic={mnemonic}");

            if (remark.Length > 0)
            {
                return null;
            }

            // we backspace we may backspace into the mnemonic
            if ((mnemonic == Mnemonic.NONE))
            {
                return null;
            }

            int mnemonicOffset = lineStr.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            if (mnemonicOffset == -1)
            {
                LogError($"GetTextDocumentSignatureHelp: should not happen: investigate");
                return null;
            }

            int argsOffset = mnemonicOffset + mnemonic.ToString().Length + 1;
            int argStrLength = (int)parameter.Position.Character - argsOffset;
            if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: argsOffset={argsOffset}; argStrLength={argStrLength}");
            if (extraLogging) LogInfo($"GetTextDocumentSignatureHelp: current lineNumber: lineNumber=\"{lineStr}\"; mnemonic={mnemonic}, args={string.Join(",", args)}");

            List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
            HashSet<Arch> selectedArchitectures = this.options.Get_Arch_Switched_On();

            IEnumerable<AsmSignatureInformation> x = this.mnemonicStore.GetSignatures(mnemonic);
            IEnumerable<AsmSignatureInformation> y = this.Constrain_Signatures(x, operands, selectedArchitectures);
            List<SignatureInformation> z = [];
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
                Signatures = [.. z],
            };
        }
        catch (Exception e)
        {
            LogError($"GetTextDocumentSignatureHelp: e ={e}");
            return null;
        }
    }

    public void SetFoldingRanges(IEnumerable<FoldingRange> foldingRanges, string uri)
    {
        this.foldingRanges.Remove(uri);
        this.foldingRanges.Add(uri, foldingRanges);
    }

    public FoldingRange[] GetFoldingRanges(FoldingRangeParams parameter)
    {
        if (!this.options.CodeFolding_On)
        {
            return [];
        }
        if (this.foldingRanges.TryGetValue(parameter.TextDocument.Uri.ToString(), out IEnumerable<FoldingRange>? value))
        {
            return [.. value];
        }
        return [];
    }

    /// <summary>
    /// Get semantic tokens for a document. Maps AsmTokenType to LSP semantic token types.
    /// Token types (from legend):
    ///   0: keyword (mnemonics)
    ///   1: variable (registers)
    ///   2: label (labels, jumps)
    ///   3: macro (directives)
    ///   4: number (constants/immediates)
    ///   5: operator (memory operands)
    ///   6: comment (remarks)
    ///   7: string
    ///   8: function (CALL targets)
    /// </summary>
    /// <param name="parameter">Semantic tokens parameters with document URI.</param>
    /// <returns>SemanticTokens with delta-encoded token data for rich syntax highlighting.</returns>
    /// <remarks>
    /// The method processes parsed tokens from parsedDocuments and encodes them as:
    ///   [deltaLine, deltaChar, length, tokenType, tokenModifiers] * N
    /// where deltas are relative to the previous token for efficient compression.
    /// Token types map to LSP indices 0-8 via MapTokenType(), with modifiers from GetTokenModifiers().
    /// </remarks>
    /// <example>
    /// Client requests: textDocument/semanticTokens/full
    /// Server returns: { resultId: "1", data: [0,0,3,0,0, 0,3,3,0,0, ...] }
    /// // line 0, col 0, len 3, type 0 (keyword), mod 0; then line 0, col 3, len 3, type 0, mod 0
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, syntax highlighting, LSP, delta encoding, token types
    /// USED IN: LanguageServerTarget.GetSemanticTokensFull, GetSemanticTokensDelta
    /// SEE ALSO: MapTokenType, GetTokenModifiers, GetSemanticTokensDelta for delta requests
    public SemanticTokens GetSemanticTokens(SemanticTokensParams parameter)
    {
        string uri = parameter.TextDocument.Uri.ToString();

        if (!this.parsedDocuments.TryGetValue(uri, out KeywordID[][]? keywords))
        {
            return new SemanticTokens { ResultId = this.GetDocumentResultId(uri), Data = [] };
        }

        var data = new List<int>();
        int prevLine = 0;
        int prevChar = 0;

        // Process each line
        for (int lineNumber = 0; lineNumber < keywords.Length; lineNumber++)
        {
            var lineTokens = keywords[lineNumber];
            if (lineTokens == null) continue;

            // Tokens are already sorted by the parser, skip unnecessary sorting
            // var sortedTokens = lineTokens.OrderBy(t => t.Start_Pos).ToList();

            foreach (var token in lineTokens)
            {
                if (token.Type == AsmTokenType.UNKNOWN) continue;

                // Map AsmTokenType to semantic token type index
                int tokenType = MapTokenType(token.Type);
                if (tokenType < 0) continue;

                int tokenModifiers = GetTokenModifiers(token.Type);
                int tokenLength = token.End_Pos - token.Start_Pos;
                if (tokenLength <= 0) continue;

                // Encode token as delta from previous token
                int deltaLine = lineNumber - prevLine;
                int deltaStart = (deltaLine == 0) ? (token.Start_Pos - prevChar) : token.Start_Pos;

                // LSP semantic tokens are encoded as 5 ints per token:
                // deltaLine, deltaStartChar, length, tokenType, tokenModifiers
                data.Add(deltaLine);
                data.Add(deltaStart);
                data.Add(tokenLength);
                data.Add(tokenType);
                data.Add(tokenModifiers);

                prevLine = lineNumber;
                prevChar = token.Start_Pos;
            }
        }

        return new SemanticTokens { ResultId = this.GetDocumentResultId(uri), Data = [.. data] };
    }

    /// <summary>
    /// Get semantic tokens delta request. Returns empty edits when the document hasn't changed,
    /// or full tokens when it has. This prevents VS from polling every ~2 seconds.
    /// </summary>
    /// <param name="parameter">Semantic tokens delta parameters with previous result ID.</param>
    /// <returns>SemanticTokensDelta with empty edits if unchanged, or full SemanticTokens if changed.</returns>
    /// <remarks>
    /// Uses document version as result ID. If previousResultId matches current resultId,
    /// returns empty edits (VS already has up-to-date tokens). Otherwise returns full tokens.
    /// </remarks>
    /// <example>
    /// Document unchanged: { resultId: "5", edits: [] }
    /// Document changed: { resultId: "6", data: [0,0,3,0,0, ...] }
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, delta encoding, incremental updates, caching, versioning
    /// USED IN: LanguageServerTarget.TextDocumentSemanticTokensFullDelta
    /// SEE ALSO: GetSemanticTokens, GetDocumentResultId
    public object GetSemanticTokensDelta(SemanticTokensDeltaParams parameter)
    {
        string uri = parameter.TextDocument.Uri.ToString();
        string currentResultId = this.GetDocumentResultId(uri);

        if (currentResultId == parameter.PreviousResultId)
        {
            return new SemanticTokensDelta { ResultId = currentResultId, Edits = [] };
        }

        // Document changed since last request — return full tokens
        return this.GetSemanticTokens(new SemanticTokensParams { TextDocument = parameter.TextDocument });
    }

    private string GetDocumentResultId(string uri)
    {
        if (this.textDocuments.TryGetValue(uri, out var doc))
        {
            return doc.Version.ToString();
        }
        return "0";
    }

    /// <summary>
    /// Map AsmTokenType to semantic token type index (matching the legend in server capabilities)
    /// </summary>
    /// <param name="type">AsmTokenType from parsed document tokens.</param>
    /// <returns>LSP token type index (0-8 for standard types, 10-15 for MASM/NASM-specific types), or -1 for unknown.</returns>
    /// <remarks>
    /// Standard token types:
    ///   0: keyword (Mnemonic, MnemonicOff - deprecated)
    ///   1: variable (Register)
    ///   2: label (Label, LabelDef)
    ///   3: macro (Directive)
    ///   4: number (Constant)
    ///   5: operator (Misc - memory operands)
    ///   6: comment (Remark)
    ///   7: string (not used currently)
    ///   8: function (Jump - CALL targets)
    /// </remarks>
    /// <example>
    /// MapTokenType(AsmTokenType.Mnemonic) → 0
    /// MapTokenType(AsmTokenType.Register) → 1
    /// MapTokenType(AsmTokenType.Label) → 2
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, token type mapping, LSP protocol, type classification
    /// USED IN: GetSemanticTokens, LanguageServerTarget.GetSemanticTokensFull
    /// SEE ALSO: GetTokenModifiers, SemanticTokensLegend, AsmTokenType
private static int MapTokenType(AsmTokenType type)
     {
         return type switch
         {
             AsmTokenType.Mnemonic => 0,    // keyword
             AsmTokenType.MnemonicOff => 0, // keyword (deprecated - will add modifier)
             AsmTokenType.Register => 1,    // variable
             AsmTokenType.Label => 2,       // label
             AsmTokenType.LabelDef => 2,    // label (definition)
             AsmTokenType.Jump => 8,        // function (jump target)
             AsmTokenType.Directive => 3,   // macro
             AsmTokenType.Constant => 4,    // number
             AsmTokenType.Remark => 6,      // comment
             AsmTokenType.Misc => 5,        // operator (memory operands, brackets, etc.)
             // MASM/NASM-specific token types
             AsmTokenType.MasmDirective => 10, // masmDirective
             AsmTokenType.NasmDirective => 11, // nasmDirective
             AsmTokenType.MasmOperator => 12,  // masmOperator
             AsmTokenType.NasmOperator => 13,  // nasmOperator
             AsmTokenType.MasmPseudoOp => 14,  // masmPseudoOp
             AsmTokenType.NasmPseudoOp => 15,  // nasmPseudoOp
             _ => -1, // Skip unknown tokens
         };
     }

    /// <summary>
    /// Get token modifiers based on token type
    /// Modifier flags: 0x1 = declaration, 0x2 = definition, 0x4 = deprecated, 0x8 = readonly
    /// </summary>
    /// <param name="type">AsmTokenType from parsed document tokens.</param>
    /// <returns>Bitmask of TokenModifier flags (0 if none apply).</returns>
    /// <remarks>
    /// Modifier mappings:
    ///   0x3 (0x1|0x2) = LabelDef: label is both declaration and definition
    ///   0x4 = MnemonicOff: deprecated instruction
    ///   0x8 = Constant: immediate value (readonly)
    /// MASM/NASM-specific types have no special modifiers by default.
    /// </remarks>
    /// <example>
    /// GetTokenModifiers(AsmTokenType.LabelDef) → 0x3
    /// GetTokenModifiers(AsmTokenType.MnemonicOff) → 0x4
    /// GetTokenModifiers(AsmTokenType.Constant) → 0x8
    /// GetTokenModifiers(AsmTokenType.Mnemonic) → 0
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, token modifiers, LSP protocol, bit flags
    /// USED IN: GetSemanticTokens
    /// SEE ALSO: MapTokenType, TokenModifiers
private static int GetTokenModifiers(AsmTokenType type)
     {
         return type switch
         {
             AsmTokenType.LabelDef => 0x3,    // declaration + definition
             AsmTokenType.MnemonicOff => 0x4, // deprecated
             AsmTokenType.Constant => 0x8,    // readonly
             // MASM/NASM-specific token types (no special modifiers by default)
             AsmTokenType.MasmDirective => 0,
             AsmTokenType.NasmDirective => 0,
             AsmTokenType.MasmOperator => 0,
             AsmTokenType.NasmOperator => 0,
             AsmTokenType.MasmPseudoOp => 0,
             AsmTokenType.NasmPseudoOp => 0,
             _ => 0,
         };
     }

    /// <summary>
    /// Get inlay hints for a document range (LSP 3.17).
    /// Shows instruction latency, memory sizes, and value conversions inline.
    /// </summary>
    /// <param name="parameter">Inlay hints request with document URI and range.</param>
    /// <returns>Array of InlayHint showing performance data and hex/decimal conversions.</returns>
    /// <remarks>
    /// Hints added for:
    ///   1. Performance: Instruction latency (e.g., "⏱5cy") beside mnemonics when PerformanceInfo_On
    ///   2. Number conversions: Decimal after hex (e.g., "=10") or hex after decimal (e.g., "=0xA") for constants
    /// 
    /// Uses InlayHintKind.Type with PaddingLeft to avoid overlapping existing text.
    /// Performance data shows µops, latency, throughput from PerformanceStore.
    /// </remarks>
    /// <example>
    /// For "mov eax, 10h":
    ///   hint at end: "=16" (decimal after hex)
    /// For "add rax, rbx" on Skylake:
    ///   hint after "add": " ⏱1cy" (latency)
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: inlay hints, performance data, hex conversion, LSP 3.17, inline annotations
    /// USED IN: LanguageServerTarget.GetInlayHints
    /// SEE ALSO: GetInlayHintsInlayHint
    public InlayHint[] GetInlayHints(InlayHintParams parameter)
    {
        string uri = parameter.TextDocument.Uri.ToString();
        LogToFile($"[GetInlayHints] uri={uri}");

        if (!this.textDocumentLines.TryGetValue(uri, out string[]? lines))
        {
            return [];
        }

        var hints = new List<InlayHint>();
        int startLine = parameter.Range.Start.Line;
        int endLine = Math.Min(parameter.Range.End.Line, lines.Length - 1);

        // Get selected microarchitectures for performance info
        MicroArch selectedArch = this.options?.Get_MicroArch_Switched_On() ?? MicroArch.NONE;
        bool showPerformance = this.options?.PerformanceInfo_On == true && selectedArch != MicroArch.NONE;

        for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
        {
            if (lineNumber >= lines.Length) break;
            string lineText = lines[lineNumber];
            if (string.IsNullOrWhiteSpace(lineText)) continue;

            int fileID = 0;
            (_, _, Mnemonic mnemonic, string[] args, _) = AsmTools.AsmSourceTools.ParseLine(lineText, lineNumber, fileID, AssemblerEnum.UNKNOWN);

            // Add clickable documentation link hint for mnemonic
            if (mnemonic != Mnemonic.NONE && this.options?.AsmDoc_On == true)
            {
                string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
                if (!string.IsNullOrEmpty(htmlRef))
                {
                    int mnemonicIdx = lineText.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                    if (mnemonicIdx >= 0)
                    {
                        string fullUrl = this.options.AsmDoc_Url.TrimEnd('/') + "/" + htmlRef;
                        hints.Add(new InlayHint
                        {
                            Position = new Position(lineNumber, mnemonicIdx + mnemonic.ToString().Length),
                            Label = new InlayHintLabelPart[]
                            {
                                new()
                                {
                                    Value = "📖",
                                    ToolTip = $"Open {mnemonic} documentation",
                                    Command = new Command
                                    {
                                        Title = $"Open {mnemonic} Documentation",
                                        CommandIdentifier = "asmdude2.openDocumentation",
                                        Arguments = [fullUrl],
                                    },
                                },
                            },
                            Kind = InlayHintKind.Type,
                            PaddingLeft = true,
                        });
                    }
                }
            }

            // Add performance hint for mnemonic
            if (showPerformance && mnemonic != Mnemonic.NONE && this.performanceStore != null)
            {
                int mnemonicStart = lineText.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                if (mnemonicStart >= 0)
                {
                    int mnemonicEnd = mnemonicStart + mnemonic.ToString().Length;
                    var perfItems = this.performanceStore.GetPerformance(mnemonic, selectedArch);
                    var firstPerf = perfItems.FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstPerf.latency_))
                    {
                        hints.Add(new InlayHint
                        {
                            Position = new Position(lineNumber, mnemonicEnd),
                            Label = $" ⏱{firstPerf.latency_}cy",
                            Kind = InlayHintKind.Type,
                            PaddingLeft = true,
                            ToolTip = new MarkupContent
                            {
                                Kind = MarkupKind.Markdown,
                                Value = $"**Latency:** {firstPerf.latency_} cycles\n\n**Throughput:** {firstPerf.throughput_}\n\n**µops:** {firstPerf.mu_Ops_Fused_}"
                            }
                        });
                    }
                }
            }

            // Scan each operand for numeric constants and add hex/decimal conversion hints.
            // Note: ParseLine does not populate keywords with operand tokens, so we scan args directly.
            int searchStart = 0;
            foreach (string arg in args)
            {
                string trimmedArg = arg.Trim();
                if (trimmedArg.Length == 0) continue;

                // Find the position of this arg in the original line text
                int argStart = lineText.AsSpan(searchStart).IndexOf(trimmedArg.AsSpan(), StringComparison.OrdinalIgnoreCase);
                if (argStart < 0) continue;
                int argEnd = argStart + trimmedArg.Length;
                searchStart = argEnd + searchStart;

                var (valid, value, _) = AsmTools.AsmSourceTools.Evaluate_Constant(trimmedArg);
                if (!valid) continue;

                string hint;
                if (trimmedArg.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                    trimmedArg.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                {
                    hint = $" ={value}";  // Show decimal for hex literals
                }
                else if (value >= 10)
                {
                    hint = $" =0x{value:X}";  // Show hex for large decimal literals
                }
                else
                {
                    continue; // Skip small decimals (0-9)
                }

                hints.Add(new InlayHint
                {
                    Position = new Position(lineNumber, argEnd),
                    Label = hint,
                    Kind = InlayHintKind.Type,
                    PaddingLeft = true,
                    ToolTip = $"Value: {value} (0x{value:X})"
                });
            }
        }

        return [.. hints];
    }

    private HashSet<CompletionItem> Mnemonic_Operand_Completions(bool useCapitals, HashSet<AsmSignatureEnum> allowedOperands, int lineNumber)
    {
        //TODO return array

        //bool use_AsmSim_In_Code_Completion = this.asmSimulator_.Enabled && Settings.Default.AsmSim_Show_Register_In_Code_Completion;
        bool att_Syntax = this.options.Used_Assembler == AssemblerEnum.NASM_ATT;

        HashSet<CompletionItem> completions = [];

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
                    FilterText = insertionText,
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
                    FilterText = insertionText,
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
                FilterText = useCapitals ? "SHORT" : "short",
                SortText = "\tSHORT", // use a tab to get on top when sorting
                Documentation = string.Empty
            };
            yield return new CompletionItem
            {
                Kind = this.GetCompletionItemKind(AsmTokenType.Misc),
                Label = "NEAR",
                InsertText = useCapitals ? "NEAR" : "near",
                FilterText = useCapitals ? "NEAR" : "near",
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
                FilterText = insertionText,
                Documentation = displayTextFull
            };
        }
    }

    public CompletionList? GetTextDocumentCompletion(CompletionParams parameter)
    {
        IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, HashSet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
        {
            HashSet<CompletionItem> completions = [];

            // Add the completions of AsmDude directives (such as code folding directives)
            #region
            if (addSpecialKeywords && this.options.CodeFolding_On)
            {
                {
                    string labelText = this.options.CodeFolding_BeginTag;     //the characters that start the outlining region
                    completions.Add(new CompletionItem
                    {
                        Kind = CompletionItemKind.Snippet,
                        Label = $"{labelText} - keyword to start code folding",
                        InsertText = labelText[1..], // remove the prefix #
                        SortText = labelText,
                        FilterText = labelText[1..],
                    });
                }
                {
                    string labelText = this.options.CodeFolding_EndTag;       //the characters that end the outlining region
                    completions.Add(new CompletionItem
                    {
                        Kind = CompletionItemKind.Snippet,
                        Label = $"{labelText} - keyword to end code folding",
                        InsertText = labelText[1..], // remove the prefix #
                        SortText = labelText,
                        FilterText = labelText[1..],
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
                        FilterText = insertionText,
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

                    //LogInfo("CodeCompletionSource:Selected_Completions; keyword=" + keyword_uppercase + "; arch=" + arch + "; selected=" + selected);

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
                            FilterText = insertionText,
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
            bool extraLogging = false;
#else
                bool extraLogging = false;
#endif

            if (!this.options.CodeCompletion_On)
            {
                LogInfo($"OnTextDocumentCompletion: switched off");
                return new CompletionList();
            }

            var lines = this.GetLines(parameter.TextDocument.Uri.ToString());
            int lineNumber = (int)parameter.Position.Line;
            if (lineNumber >= lines.Length) return new CompletionList();
            string completeLineStr = lines[lineNumber];
            int pos = (int)parameter.Position.Character;

            // we only consider the line till (and including) the current position
            string lineStr = completeLineStr[..pos];

            char currentChar = this.GetChar(completeLineStr, pos - 1);

            int fileID = 0; //TODO
            (object _, string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID, AssemblerEnum.UNKNOWN);
            if (extraLogging) LogInfo($"OnTextDocumentCompletion: lineStr=\"{lineStr}\"; mnemonic={mnemonic}; args={string.Join(',', args)}");

            // if we are typing in a remark: no code completion please
            if (remark.Length > 0)
            {
                return new CompletionList();
            }

            // determine if the current word we are typing is all capitals
            (string currentWord, _, _) = GetWord(pos - 1, lineStr);
            bool useCapitals = (currentWord == currentWord.ToUpper());
            string prefix = currentWord.ToUpperInvariant();

            if (extraLogging) LogInfo($"OnTextDocumentCompletion: currentWord=\"{currentWord}\"; useCapitals={useCapitals}");

            // Filter completion items by the prefix the user has typed so far.
            // With only 1-2 characters typed, VS fuzzy matching is too broad (e.g., "Z" matches YMM via "[AVX512]"),
            // so we apply strict prefix filtering. With 3+ characters, VS fuzzy matching works well.
            CompletionItem[] FilterByPrefix(IEnumerable<CompletionItem> items)
            {
                if (prefix.Length == 0 || prefix.Length > 2) return [.. items];
                return [.. items.Where(i => i.FilterText != null && i.FilterText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
            }

            // if the mnemonic is NONE we should suggest mnemonics
            if (mnemonic == Mnemonic.NONE)
            {
                HashSet<AsmTokenType> selected = [AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic];
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: A");
                return new CompletionList()
                {
                    Items = FilterByPrefix(Selected_Completions(useCapitals, selected, true)),
                };
            }

            int mnemonicOffsetStart = lineStr.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
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
                HashSet<AsmTokenType> selected = [AsmTokenType.Jump, AsmTokenType.Mnemonic];
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: B");
                return new CompletionList()
                {
                    Items = FilterByPrefix(Selected_Completions(useCapitals, selected, true)),
                };
            }

            // if the mnemonic is a jump, we should suggest labels   
            if (AsmTools.AsmSourceTools.IsJump(mnemonic))
            {
                LabelGraph? labelGraph = this.GetLabelGraph(parameter.TextDocument.Uri.ToString());
                if (extraLogging) LogInfo($"OnTextDocumentCompletion: C");
                return new CompletionList()
                {
                    Items = FilterByPrefix(this.Label_Completions(labelGraph, useCapitals, true)),
                };
            }

            // if we are here: there is a mnemonic, and not a jump, the cursor is not in the mnemonic, thus we analyse the
            // parameters of the mnemonic and make suggestions based on the allowed parameters

            HashSet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
            HashSet<AsmSignatureEnum> allowed = [];
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
            if (extraLogging)
            {
                LogInfo($"OnTextDocumentCompletion: D: useCapitals={useCapitals}; allowed.Count={allowed.Count}");
                foreach (AsmSignatureEnum sig in allowed)
                {
                    LogInfo($"OnTextDocumentCompletion: D: allowed signature {sig}");
                }
            }
            return new CompletionList()
            {
                Items = FilterByPrefix(this.Mnemonic_Operand_Completions(useCapitals, allowed, (int)parameter.Position.Line))
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

    public DocumentHighlight[] GetDocumentHighlights(IProgress<DocumentHighlight[]> progress, Position position, string uri, CancellationToken token)
    {
        if (progress == null)
        {
            LogInfo($"LanguageServer:GetDocumentHighlights: progress is null");
            return [];
        }
        TextDocumentItem? document = this.GetTextDocument(uri);
        if (document == null)
        {
            LogInfo($"LanguageServer:GetDocumentHighlights: document is null");
            return [];
        }

        var lines = this.GetLines(uri);
        if ((int)position.Line >= lines.Length) return [];
        var lineStr2 = lines[(int)position.Line];
        (int startPos, int endPos) = FindWordBoundary((int)position.Character, lineStr2);
        int length = endPos - startPos;

        if (length <= 0)
        {
            LogInfo($"LanguageServer:GetDocumentHighlights: argStrLength too small ({length})");
            return [];
        }
        string currentHighlightedWord = new string(lineStr2.AsSpan(startPos, length));
        if (string.IsNullOrEmpty(currentHighlightedWord))
        {
            LogInfo($"LanguageServer:GetDocumentHighlights: currentHighlightedWord is not significant ({currentHighlightedWord})");
            return [];
        }

        IList<string> currentHighlightedWords = [];
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

        List<DocumentHighlight> highlights = [];
        List<DocumentHighlight> chunk = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string lineStr = lines[i];

            for (int j = 0; j < lineStr.Length; j++)
            {
                Range? range = this.GetHighlightRangeMultiple(lineStr, i, ref j, currentHighlightedWords);
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
                        progress.Report([.. chunk]);
                        chunk.Clear();
                    }
                }
                token.ThrowIfCancellationRequested();
            }
        }

        // Report last chunk if it has elements since it didn't reached the specified size
        if (chunk.Count > 0)
        {
            progress.Report([.. chunk]);
        }

        return [.. highlights];
    }

    /// <summary>
    public CodeLens[] GetCodeLenses(CodeLensParams parameter)
    {
        string uri = parameter.TextDocument.Uri.ToString();
        LabelGraph? labelGraph = this.GetLabelGraph(uri);
        if (labelGraph == null || !labelGraph.Enabled)
        {
            return [];
        }

        AssemblerEnum usedAssembler = this.options.Used_Assembler;
        List<CodeLens> lenses = [];

        var definitions = labelGraph.GetFrozenDefinitions();
        var usages = labelGraph.GetFrozenUsages();

        foreach (var kvp in definitions)
        {
            string label = kvp.Key;
            List<KeywordID> defList = kvp.Value;
            KeywordID def = defList[0];
            int lineNumber = def.LineNumber;

            int referenceCount = 0;
            // Check both the full qualified label and the regular label
            if (usages.TryGetValue(label, out List<KeywordID>? usagesList))
            {
                referenceCount = usagesList.Count;
            }

            lenses.Add(new CodeLens
            {
                Range = new Range
                {
                    Start = new Position(lineNumber, def.Start_Pos),
                    End = new Position(lineNumber, def.End_Pos),
                },
                Data = referenceCount,
            });
        }

        return [.. lenses];
    }

    /// <summary>
    /// Returns label definitions with their reference locations for CodeLens adornments.
    /// Each entry contains the label name, definition line, and the line numbers where the label is referenced.
    /// </summary>
    /// <param name="uri">Document URI to analyze.</param>
    /// <returns>Array of AsmCodeLensData with label definitions and reference line numbers.</returns>
    /// <remarks>
    /// Used by CodeLens adornments to show "N references" above label definitions.
    /// Each AsmCodeLensData entry contains:
    ///   - Label: the label name
    ///   - DefinitionLine: line number where label is defined (e.g., "my_label:")
    ///   - ReferenceLines: array of line numbers where label is referenced
    /// 
    /// Returns empty array if LabelGraph not enabled or unavailable.
    /// </remarks>
    /// <example>
    /// Code:
    ///   my_label:    ; definition (line 10)
    ///   mov eax, 1  ; reference (line 12)
    ///   jmp my_label; reference (line 14)
    /// 
    /// Returns: [ { Label: "my_label", DefinitionLine: 10, ReferenceLines: [12, 14] } ]
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: code lens, label references, assembly analysis, label graph
    /// USED IN: LanguageServerTarget.GetCodeLensData
    /// SEE ALSO: GetCodeLensDataAsmCodeLensData, LabelGraph, CodeLens
    public AsmCodeLensData[] GetCodeLensData(string uri)
    {
        LabelGraph? labelGraph = this.GetLabelGraph(uri);
        if (labelGraph == null || !labelGraph.Enabled)
        {
            return [];
        }

        List<AsmCodeLensData> result = [];

        var definitions = labelGraph.GetFrozenDefinitions();
        var usagesDict = labelGraph.GetFrozenUsages();

        foreach (var kvp in definitions)
        {
            string label = kvp.Key;
            List<KeywordID> defList = kvp.Value;
            KeywordID def = defList[0];
            int defLine = def.LineNumber;

            List<int> refLines = [];
            if (usagesDict.TryGetValue(label, out List<KeywordID>? usagesList))
            {
                foreach (KeywordID usage in usagesList!)
                {
                    refLines.Add(usage.LineNumber);
                }
            }

            result.Add(new AsmCodeLensData
            {
                Label = label,
                DefinitionLine = defLine,
                ReferenceLines = [.. refLines],
            });
        }

        return [.. result];
    }

    public CodeLens ResolveCodeLens(CodeLens codeLens)
    {
        int referenceCount = 0;
        if (codeLens.Data is System.Text.Json.JsonElement je && je.TryGetInt32(out int count))
        {
            referenceCount = count;
        }
        else if (codeLens.Data is int intCount)
        {
            referenceCount = intCount;
        }

        codeLens.Command = new Command
        {
            Title = referenceCount == 1 ? "1 reference" : $"{referenceCount} references",
            CommandIdentifier = "asm.showReferences",
        };
        return codeLens;
    }

    /// <summary>
    /// Handle "Go To Definition (F12)" request for assembly labels.
    /// Returns theLocation of label definitions by searching for "label:" patterns.
    /// </summary>
    /// <param name="parameter">Definition request with document URI and cursor position.</param>
    /// <returns>Location of label definition, or null if label not found.</returns>
    /// <remarks>
    /// Algorithm:
    ///   1. Extract word at cursor position
    ///   2. First check LabelGraph if available (cached label analysis)
    ///   3. Fallback: scan document for "label:" pattern (case-insensitive)
    ///   4. Verify word boundary (start of line or after whitespace)
    /// 
    /// Most assemblers are case-insensitive for labels, so comparison uses ToUpperInvariant.
    /// </remarks>
    /// <example>
    /// Code: "my_label: mov eax, ebx"
    /// Cursor at "my_label" → returns Location with range covering "my_label"
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: go to definition, label resolution, LSP, label graph, range searching
    /// USED IN: LanguageServerTarget.TextDocumentDefinition
    /// SEE ALSO: GetLabelGraph, LabelGraph, Location
    public Location? GetDefinition(TextDocumentPositionParams parameter)
    {
        var uri = parameter.TextDocument.Uri.ToString();
        var lines = this.GetLines(uri);
        if (lines == null || lines.Length == 0)
        {
            LogInfo($"GetDefinition: no lines found for {uri}");
            return null;
        }

        int lineNumber = (int)parameter.Position.Line;
        if (lineNumber >= lines.Length)
        {
            LogInfo($"GetDefinition: line {lineNumber} out of range");
            return null;
        }

        var (word, _, _) = GetWord((int)parameter.Position.Character, lines[lineNumber]);
        if (string.IsNullOrEmpty(word))
        {
            LogInfo($"GetDefinition: no word at position");
            return null;
        }

        LogInfo($"GetDefinition: looking for definition of '{word}'");

        // First check if we have a label graph for this document
        LabelGraph? labelGraph = this.GetLabelGraph(uri);
        if (labelGraph != null && labelGraph.Enabled)
        {
            // Search for the label definition in the label graph
            // Most assemblers are case-insensitive for labels
            string labelKey = word.ToUpperInvariant();
            var labelDescriptions = labelGraph.Label_Descriptions;

            if (labelDescriptions.ContainsKey(labelKey))
            {
                // The label exists - find its definition location
                // Label_Descriptions contains format: "LINE n (filename) :content"
                // We need to search the document for the label definition
                for (int i = 0; i < lines.Length; i++)
                {
                    string lineStr = lines[i];
                    // Label definitions end with ':' (e.g., "my_label:")
                    // or are at the start of a line followed by whitespace/instruction
                    int labelDefPos = -1;

                    // Check for "label:" pattern
                    string labelWithColon = word + ":";
                    labelDefPos = lineStr.AsSpan().IndexOf(labelWithColon.AsSpan(), StringComparison.OrdinalIgnoreCase);

                    if (labelDefPos >= 0)
                    {
                        // Verify it's at a word boundary (start of line or after whitespace)
                        if (labelDefPos == 0 || char.IsWhiteSpace(lineStr[labelDefPos - 1]))
                        {
                            LogInfo($"GetDefinition: found label definition at line {i}, position {labelDefPos}");
                            return new Location
                            {
                                Uri = new Uri(uri),
                                Range = new Range
                                {
                                    Start = new Position(i, labelDefPos),
                                    End = new Position(i, labelDefPos + word.Length)
                                }
                            };
                        }
                    }
                }
            }
        }

        // Fallback: scan document for label definitions manually
        // Labels in assembly end with ':' (e.g., "loop_start:")
        for (int i = 0; i < lines.Length; i++)
        {
            string lineStr = lines[i];
            string labelWithColon = word + ":";

            // Use case-insensitive search (most assemblers are case-insensitive for labels)
            int labelDefPos = lineStr.AsSpan().IndexOf(labelWithColon.AsSpan(), StringComparison.OrdinalIgnoreCase);

            if (labelDefPos >= 0)
            {
                // Verify it's at a word boundary
                if (labelDefPos == 0 || char.IsWhiteSpace(lineStr[labelDefPos - 1]))
                {
                    LogInfo($"GetDefinition: found label definition at line {i}, position {labelDefPos}");
                    return new Location
                    {
                        Uri = new Uri(uri),
                        Range = new Range
                        {
                            Start = new Position(i, labelDefPos),
                            End = new Position(i, labelDefPos + word.Length)
                        }
                    };
                }
            }
        }

        // No label definition found — check if it's a mnemonic and open documentation
        string wordUpper = word.ToUpperInvariant();
        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(wordUpper, true);
        if (mnemonic != Mnemonic.NONE && this.options.AsmDoc_On)
        {
            string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
            if (!string.IsNullOrEmpty(htmlRef))
            {
                string fullUrl = this.options.AsmDoc_Url.TrimEnd('/') + "/" + htmlRef;

                // Debounce: Ctrl+Click fires textDocument/definition twice (once on hover, once on click).
                // Skip if same URL was opened within the last 2 seconds.
                var now = DateTime.UtcNow;
                if (fullUrl != _lastDocUrl || (now - _lastDocTime).TotalSeconds > 2)
                {
                    _lastDocUrl = fullUrl;
                    _lastDocTime = now;
                    LogInfo($"GetDefinition: opening documentation for {mnemonic}: {fullUrl}");
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        LogInfo($"GetDefinition: failed to open URL: {ex.Message}");
                    }
                }
                else
                {
                    LogInfo($"GetDefinition: debounced duplicate for {mnemonic}");
                }

                // Write a temp documentation file and return its Location.
                // This makes "Peek Definition" (Alt+F12) show the docs inline.
                return CreateMnemonicDocLocation(mnemonic, fullUrl);
            }
        }

        LogInfo($"GetDefinition: no definition found for '{word}'");
        return null;
    }

    private Location? CreateMnemonicDocLocation(Mnemonic mnemonic, string fullUrl)
    {
        try
        {
            string description = this.mnemonicStore?.GetDescription(mnemonic) ?? string.Empty;
            var signatures = this.mnemonicStore?.GetSignatures(mnemonic);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"; {mnemonic} — Instruction Documentation");
            sb.AppendLine($"; URL: {fullUrl}");
            sb.AppendLine(";");
            sb.AppendLine($"; Description:");
            foreach (string line in description.Split('\n'))
            {
                sb.AppendLine($";   {line.TrimEnd()}");
            }

            if (signatures != null)
            {
                sb.AppendLine(";");
                sb.AppendLine("; Signatures:");
                foreach (var sig in signatures)
                {
                    sb.AppendLine($";   {sig.SignatureInformation.Label}");
                }
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"_asmdude_doc_{mnemonic}.asm");
            File.WriteAllText(tempFile, sb.ToString());

            int lineCount = sb.ToString().Split('\n').Length;
            return new Location
            {
                Uri = new Uri(tempFile),
                Range = new Range
                {
                    Start = new Position(0, 0),
                    End = new Position(lineCount - 1, 0),
                },
            };
        }
        catch (Exception ex)
        {
            LogInfo($"CreateMnemonicDocLocation: failed: {ex.Message}");
            return null;
        }
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


    /// <summary>
    /// Formats a mnemonic as an HTML anchor: <![CDATA[<a href=URL>NAME</a>]]>.
    /// Currently unused — kept for the future hybrid in-proc VSIX extension where
    /// an IAsyncQuickInfoSource (MEF) can parse this HTML and create a WPF
    /// ClassifiedTextRun with a real Action delegate for clickable navigation.
    ///
    /// See: VS/CSHARP/old/asm-dude2-vsix-archived/QuickInfo/AsmQuickInfoSource.cs
    /// for the old in-process implementation that consumed this format.
    /// See: VSInternalTypes.cs for why clickable links are not possible over LSP.
    /// </summary>
    private string AsHtmlUrl(Mnemonic mnemonic)
    {
        string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
        if (htmlRef == null)
        {
            return mnemonic.ToString();
        }
        string fullURL = this.options.AsmDoc_Url.TrimEnd('/') + "/" + htmlRef;
        return "<a href=" + fullURL + ">" + mnemonic.ToString() + "</a>";
    }

    /// <summary>
    /// Write a JSON mapping file (mnemonic → full doc URL) so the in-proc MEF QuickInfo source
    /// can create clickable links without needing to call the LSP server.
    /// </summary>
    private void WriteMnemonicUrlMapping()
    {
        try
        {
            string baseUrl = this.options.AsmDoc_Url.TrimEnd('/') + "/";
            var mapping = new Dictionary<string, string>();
            foreach (Mnemonic m in Enum.GetValues(typeof(Mnemonic)))
            {
                if (m == Mnemonic.NONE) continue;
                string htmlRef = this.mnemonicStore.GetHtmlRef(m);
                if (!string.IsNullOrEmpty(htmlRef))
                {
                    mapping[m.ToString()] = baseUrl + htmlRef;
                }
            }
            string json = System.Text.Json.JsonSerializer.Serialize(mapping);
            string mappingFile = Path.Combine(Path.GetTempPath(), "_asmdude_mnemonic_urls.json");
            File.WriteAllText(mappingFile, json);
            LogToFile($"[WriteMnemonicUrlMapping] Wrote {mapping.Count} entries to {mappingFile}");
        }
        catch (Exception ex)
        {
            LogToFile($"[WriteMnemonicUrlMapping] Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns DocumentLink[] for all mnemonics/jumps that have documentation URLs.
    /// VS renders these as Ctrl+Clickable underlined text that opens the URL in a browser.
    /// </summary>
    public DocumentLink[]? GetDocumentLinks(string uri)
    {
        if (!this.options.AsmDoc_On)
        {
            return null;
        }

        if (!this.parsedDocuments.TryGetValue(uri, out KeywordID[][]? keywords))
        {
            return null;
        }

        string[] lines = this.GetLines(uri);
        var links = new List<DocumentLink>();
        string baseUrl = this.options.AsmDoc_Url.TrimEnd('/') + "/";

        for (int lineNumber = 0; lineNumber < keywords.Length; lineNumber++)
        {
            var lineTokens = keywords[lineNumber];
            if (lineTokens == null) continue;

            foreach (var token in lineTokens)
            {
                if (token.Type is not (AsmTokenType.Mnemonic or AsmTokenType.Jump))
                {
                    continue;
                }

                int tokenLength = token.End_Pos - token.Start_Pos;
                if (tokenLength <= 0) continue;

                string line = (lineNumber < lines.Length) ? lines[lineNumber] : string.Empty;
                if (token.Start_Pos >= line.Length) continue;
                string keyword = line.Substring(token.Start_Pos, Math.Min(tokenLength, line.Length - token.Start_Pos));

                Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword.ToUpperInvariant(), true);
                if (mnemonic == Mnemonic.NONE) continue;

                string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
                if (string.IsNullOrEmpty(htmlRef)) continue;

                string fullUrl = baseUrl + htmlRef;

                links.Add(new DocumentLink
                {
                    Range = new Range
                    {
                        Start = new Position(lineNumber, token.Start_Pos),
                        End = new Position(lineNumber, token.End_Pos),
                    },
                    Target = new Uri(fullUrl),
                });
            }
        }

        LogToFile($"[GetDocumentLinks] uri={uri}, linkCount={links.Count}");
        return links.Count > 0 ? [.. links] : null;
    }

    /// <summary>
    /// Handle hover request. Returns VSInternalHover with styled content for mnemonics, registers, and labels.
    /// </summary>
    /// <param name="parameter">Hover request with document URI and cursor position.</param>
    /// <returns>VSInternalHover with _vs_rawContent for styled text; null if no hover data available.</returns>
    /// <remarks>
    /// Hover responses use VS-specific VSInternalHover with _vs_rawContent because:
    ///   - VS LSP client only supports PlainText in standard Contents (no Markdown rendering)
    ///   - VS-specific ClassifiedTextElement allows monospace font via "formal language" + UseClassificationFont
    ///   - Clickable links impossible over LSP (NavigationAction is an unserializable Action delegate)
    /// 
    /// Token type handling:
    ///   - Mnemonic/Jump: colored keyword + stacked monospace description + performance table
    ///   - Register: monospace "Register RAX: description" + simulated value before/after
    ///   - Label/labelDef: TODO (currently returns null)
    ///   - Keyword: monospace keyword description
    /// 
    /// See VSInternalTypes.cs for full discussion of VS hover limitations.
    /// </remarks>
    /// <example>
    /// Mnemonic hover: [colored "MOV", monospace "Move data", performance table]
    /// Register hover: [monospace "Register RAX: 64-bit accumulator", "Before: 0x1234", "After: 0x5678"]
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: hover tooltip, VSInternalHover, classified text, LSP, styled text
    /// USED IN: LanguageServerTarget.OnHover
    /// SEE ALSO: HoverBuilder, VSInternalHover, PredefinedClassificationTypeNames
    public object? GetHover(TextDocumentPositionParams parameter)
    {
        var uri = parameter.TextDocument.Uri.ToString();
        LogToFile($"[GetHover] uri={uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        if (!this.options.AsmDoc_On)
        {
            LogInfo($"OnHover: switched off");
            return null;
        }
        var lines = this.GetLines(uri);
        if ((int)parameter.Position.Line >= lines.Length) return null;
        var (keyword, startPos, endPos) = GetWord((int)parameter.Position.Character, lines[(int)parameter.Position.Line]);
        if (keyword.Length == 0)
        {
            return null;
        }
        string keyword_uppercase = keyword.ToUpperInvariant();
        string[]? hoverContent = null;
        string? hoverKeyword = null; // keyword text for colored mnemonic display
        AsmTokenType tokenType = this.GetAsmTokenType(keyword_uppercase);

        switch (tokenType)
        {
            case AsmTokenType.Mnemonic: // intentional fall through
            case AsmTokenType.Jump:
                {
                    Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_uppercase, true);
                    hoverKeyword = mnemonic.ToString();

                    string archStr = ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));
                    string descr = this.mnemonicStore.GetDescription(mnemonic);
                    string full_Descr = AsmTools.AsmSourceTools.Linewrap($"{mnemonic} :[{archStr}] {descr}", MaxNumberOfCharsInToolTips);
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

                            performanceStr += string.Format(
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
                        }
                    }

                    // Build doc URL hint for hover
                    string docUrlHint = "";
                    string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
                    if (!string.IsNullOrEmpty(htmlRef))
                    {
                        docUrlHint = "\nCtrl+Click mnemonic for docs: " + this.options.AsmDoc_Url.TrimEnd('/') + "/" + htmlRef;
                    }

                    hoverContent = [
                        full_Descr,
                        (performanceInfoAvailable) ? "\nPerformance:\n" + performanceStr : "",
                        docUrlHint,
                    ];
                    break;
                }
            case AsmTokenType.Register:
                {
                    if (keyword_uppercase.StartsWith('%'))
                    {
                        keyword_uppercase = keyword_uppercase[1..]; // remove the preceding % in AT&T syntax
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

                        // Show simulated register value before and after this line (if known).
                        var simUri = new Uri(parameter.TextDocument.Uri.ToString());
                        int simLine = (int)parameter.Position.Line;
                        string? simBefore = this.asmSimulator_.GetRegisterValueBeforeLine(simUri, simLine, reg);
                        string? simAfter = this.asmSimulator_.GetRegisterValueAfterLine(simUri, simLine, reg);
                        string simSuffix = string.Empty;
                        if (simBefore != null || simAfter != null)
                        {
                            var sb = new System.Text.StringBuilder("\n");
                            if (simBefore != null) sb.Append($"Before: {simBefore}");
                            if (simBefore != null && simAfter != null) sb.Append("  →  ");
                            if (simAfter != null) sb.Append($"After: {simAfter}");
                            simSuffix = sb.ToString();
                        }

                        hoverContent = [
                            $"Register {regStr}: {full_Descr}{simSuffix}",
                            ];
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
                        hoverContent = [
                            $"Keyword {keyword}: {descr}",
                            ];
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


        // Append simulator register+flag state if available.
        // For mnemonic/jump hovers, show state before and after the instruction.
        // Register hovers already show the specific register's value inline (above).
        if (tokenType is AsmTokenType.Mnemonic or AsmTokenType.Jump or AsmTokenType.MnemonicOff)
        {
            var simUri2 = new Uri(parameter.TextDocument.Uri.ToString());
            int simLine2 = (int)parameter.Position.Line;
            string? simBefore2 = this.asmSimulator_.GetRegisterStatesBeforeLine(simUri2, simLine2);
            string? simAfter2 = this.asmSimulator_.GetRegisterStatesAfterLine(simUri2, simLine2);
            if (simBefore2 != null || simAfter2 != null)
            {
                var sb2 = new System.Text.StringBuilder();
                if (simBefore2 != null) sb2.Append("\nBefore:" + simBefore2);
                if (simAfter2 != null) sb2.Append("\nAfter:" + simAfter2);
                hoverContent = [.. (hoverContent ?? []), sb2.ToString()];
            }
        }

        if (hoverContent != null)
        {
            int line = (int)parameter.Position.Line;

            // Hover uses _vs_rawContent for styled text (monospace + colored keywords).
            // For mnemonics, also sets Contents to Markdown with a clickable doc link.
            if (!string.IsNullOrEmpty(hoverKeyword) && (tokenType is AsmTokenType.Mnemonic or AsmTokenType.Jump))
            {
                return HoverBuilder.CreateMnemonicHover(hoverKeyword, hoverContent, line, startPos, endPos);
            }

            return HoverBuilder.CreateStackedHover(hoverContent, line, startPos, endPos);
        }
        return null;
    }

    /// <summary>
    /// Get proven Z3 simulator states for a range of lines.
    /// Returns register states proven by Z3 SimpleStep before and after each instruction.
    /// </summary>
    /// <param name="parameter">Proven states request with URI and optional line range.</param>
    /// <returns>ProvenStatesResponse with States array containing before/after register states.</returns>
    /// <remarks>
    /// Each ProvenLineState entry contains:
    ///   - Line: instruction line number
    ///   - BeforeState: Z3-proven register states before instruction execution
    ///   - AfterState: Z3-proven register states after instruction execution
    ///   - ProvenBy: "Z3 SimpleStep" (proven by solver)
    ///   - Confidence: "complete" (Z3 fully analyzed the instruction)
    /// 
    /// Returns empty States array if no cache entry exists for the document.
    /// </remarks>
    /// <example>
    /// User clicks "Show Proven States" on line 42 → returns Z3-proven register values.
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: Z3 simulator, proven states, register simulation, assembly analysis
    /// USED IN: LanguageServerTarget.GetProvenStates
    /// SEE ALSO: LspAsmSimulator, ProvenStatesResponse, GetCachedEntry
    public ProvenStatesResponse? GetProvenStates(GetProvenStatesParams parameter)
    {
        try
        {
            var uri = parameter.Uri;
            int? startLine = parameter.LineRange?[0];
            int? endLine = parameter.LineRange?[1];

            var lines = this.GetLines(uri);
            var cacheEntry = this.asmSimulator_.GetCachedEntry(new Uri(uri));
            if (cacheEntry == null)
            {
                LogInfo($"GetProvenStates: no cache entry for {uri}");
                return new ProvenStatesResponse
                {
                    States = [],
                    TotalLines = lines.Length,
                    ComputedAt = System.DateTime.UtcNow.ToString("o"),
                };
            }

            var states = new List<ProvenLineState>();
            int limit = Math.Min(lines.Length, LspAsmSimulator.MaxLines);
            for (int i = 0; i < limit; i++)
            {
                if (startLine.HasValue && i < startLine.Value) continue;
                if (endLine.HasValue && i > endLine.Value) break;

                string? beforeStr = cacheEntry.GetBeforeState(i);
                string? afterStr = cacheEntry.GetAfterState(i);

                if (beforeStr != null || afterStr != null)
                {
                    states.Add(new ProvenLineState
                    {
                        Line = i,
                        BeforeState = beforeStr,
                        AfterState = afterStr,
                        ProvenBy = "Z3 SimpleStep",
                        Confidence = "complete",
                    });
                }
            }

            return new ProvenStatesResponse
            {
                States = states,
                TotalLines = lines.Length,
                ComputedAt = System.DateTime.UtcNow.ToString("o"),
            };
        }
        catch (Exception ex)
        {
            LogInfo($"GetProvenStates exception: {ex.Message}");
            return null;
        }
    }

    public void SetDocumentSymbols(IEnumerable<VSSymbolInformation> symbolsInfo)
    {
        this.Symbols = symbolsInfo;
    }

    public VSSymbolInformation[] GetDocumentSymbols(DocumentSymbolParams parameters)
    {
        return [.. this.Symbols];
    }

    private void UpdateSymbols(string uri)
    {
        IList<VSSymbolInformation> symbolInfo = [];
        var lines = this.GetLines(uri);

        int fileID = 0; //TODO

        for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
        {
            string lineStr = lines[lineNumber];
            (object _, string label, Mnemonic mnemonic, _, _) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID, AssemblerEnum.UNKNOWN);
            if (label.Length > 0)
            {
                int pos = lineStr.IndexOf(label);
#pragma warning disable CS0618 // VSSymbolInformation requires deprecated SymbolInformation base properties
                symbolInfo.Add(new VSSymbolInformation
                {
                    Name = label,
                    Kind = SymbolKind.Key,
                    Location = new Location
                    {
                        Uri = new Uri(uri),
                        Range = new Range
                        {
                            Start = new Position(lineNumber, pos),
                            End = new Position(lineNumber, pos + label.Length)
                        }
                    },
                    #region VS specific
                    HintText = "some hinttext here?",
                    Description = "some description here?",
                    //Icon = // If specified, this icon is used instead of SymbolKind.
                    #endregion
                });
#pragma warning restore CS0618
            }
            if (mnemonic != Mnemonic.NONE)
            {
                int pos = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
                string mnemonicStr = mnemonic.ToString();
#pragma warning disable CS0618 // VSSymbolInformation requires deprecated SymbolInformation base properties
                symbolInfo.Add(new VSSymbolInformation
                {
                    Name = mnemonicStr,
                    Kind = SymbolKind.Function,
                    HintText = "some hint text here?",
                    Description = "some description here?",
                    Location = new Location
                    {
                        Uri = new Uri(uri),
                        Range = new Range
                        {
                            Start = new Position(lineNumber, pos),
                            End = new Position(lineNumber, pos + mnemonicStr.Length)
                        }
                    }
                });
#pragma warning restore CS0618
                if (false)
                {
                    string[] args;
#pragma warning disable CS0162 // Unreachable code detected
#pragma warning disable CS0618 // VSSymbolInformation requires deprecated SymbolInformation base properties
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
#pragma warning restore CS0618
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
            ProjectContexts = [.. this.Contexts],
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
            _ => CompletionItemKind.Text,
        };
    }

    #region Logging

    /// <summary>
    /// When true, logging uses stderr instead of stdout to avoid interfering with LSP protocol.
    /// Set this before creating the LanguageServer when using stdio mode.
    /// </summary>
    public static bool UseStdio { get; set; } = false;

    private static TextWriter LogWriter => UseStdio ? Console.Error : Console.Out;

    private static readonly string[] separator = ["\r\n", "\n"];

    public static void LogInfo(string message)
    {
        if (Instance?.target?.traceSetting == TraceSetting.Verbose)
        {
            LogWriter.WriteLine($"INFO {DateTimeOffset.Now.ToString("yyyyMMdd hh.mm.ss.ffffff")}: {message}");
            Instance?.traceSource?.TraceEvent(TraceEventType.Information, 0, message);
        }
    }

    public static void LogWarning(string message)
    {
        LogWriter.WriteLine($"WARNING {DateTimeOffset.Now.ToString("yyyyMMdd hh.mm.ss.ffffff")}: {message}");
        Instance?.traceSource?.TraceEvent(TraceEventType.Warning, 0, message);
    }

    public static void LogToFile(string message)
    {
        try
        {
            string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "asmdude-execution.log");
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
            System.IO.File.AppendAllText(logPath, $"[{timestamp}] {message}\n");
        }
        catch { }
    }

    public static void LogError(string message)
    {
        LogWriter.WriteLine($"ERROR {DateTimeOffset.Now:yyyyMMdd hh.mm.ss.ffffff}: {message}");
        Instance?.MakeWindowVisible();
        Instance?.traceSource?.TraceEvent(TraceEventType.Error, 0, message);
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
        _ = this.SendMethodNotificationAsync(Methods.WindowLogMessageName, new LogMessageParams
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
        _ = this.SendMethodNotificationAsync(Methods.WindowShowMessageName, parameter);
    }

    public async Task<MessageActionItem> ShowMessageRequestAsync(string message, MessageType messageType, string[] actionItems)
    {
        ShowMessageRequestParams parameter = new()
        {
            Message = message,
            MessageType = messageType,
            Actions = [.. actionItems.Select(a => new MessageActionItem { Title = a })]
        };

        return await this.SendMethodRequestAsync<ShowMessageRequestParams, MessageActionItem>(Methods.WindowShowMessageRequestName, parameter);
    }

    #endregion

    // store incoming settings from VS
    public void SendSettings(DidChangeConfigurationParams parameter)
    {
        this.CurrentSettings = parameter.Settings?.ToString() ?? "{}";
        this.NotifyPropertyChanged(nameof(this.CurrentSettings));

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(this.CurrentSettings);
            // Try to get maxNumberOfProblems from the settings
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("maxNumberOfProblems", out var maxProblems) &&
                        maxProblems.TryGetInt32(out int newMaxProblems))
                    {
                        // Use newMaxProblems if needed
                        LogInfo($"SendSettings: maxNumberOfProblems = {newMaxProblems}");
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            LogInfo($"SendSettings: Failed to parse settings: {ex.Message}");
        }

        LogInfo($"SendSettings: received settings update");
    }

    public void Exit()
    {
        this.disconnectEvent.Set();

        Disconnected?.Invoke(this, new EventArgs());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0) return;
        this.Exit();
        this.rpc?.Dispose();
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

    private Location? GetLocation(string lineStr, int lineOffset, ref int characterOffset, string wordToMatch, Uri uri)
    {
        if ((characterOffset + wordToMatch.Length) <= lineStr.Length)
        {
            if (lineStr.AsSpan(characterOffset, wordToMatch.Length).Equals(wordToMatch.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

    private Range? GetHighlightRange(string lineStr, int lineOffset, ref int characterOffset, string wordToMatch)
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

            if (lineStr.AsSpan(characterOffset, wordLength).Equals(wordToMatch.AsSpan(), StringComparison.OrdinalIgnoreCase))
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

    private Range? GetHighlightRangeMultiple(string line, int lineOffset, ref int characterOffset, IEnumerable<string> wordsToMatch)
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
        // Always log disconnection to stderr for debugging (regardless of trace setting)
        Console.Error.WriteLine($"OnRpcDisconnected: Reason={e.Reason}, Description={e.Description}, Exception={e.Exception?.Message}");
        if (e.Exception != null)
        {
            Console.Error.WriteLine($"OnRpcDisconnected Exception: {e.Exception}");
        }
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

    internal Task SendPartialResultAsync(object token, object value) =>
        this.SendMethodNotificationAsync(Methods.ProgressNotificationName, new { token, value });

    private Task SendMethodNotificationAsync<TIn>(string methodName, TIn param)
    {
        if (this.rpc == null)
        {
            return Task.CompletedTask;
        }
        return this.rpc.NotifyWithParameterObjectAsync(methodName, param);
    }

    private Task<TOut> SendMethodRequestAsync<TIn, TOut>(string methodName, TIn param)
    {
        if (this.rpc == null)
        {
            return Task.FromResult<TOut>(default);
        }
        return this.rpc.InvokeWithParameterObjectAsync<TOut>(methodName, param);
    }
}

