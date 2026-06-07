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

    private readonly JsonRpc rpc = default!;
    private readonly HeaderDelimitedMessageHandler messageHandler = default!;
    private readonly LanguageServerTarget target = default!;
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

    // Tracks semantic token invalidation version per document (incremented when sim unreachable lines change)
    private readonly Dictionary<string, int> simTokenVersions = [];


    private readonly TraceSource traceSource;

    private AsmDude2Tools? asmDudeTools;
    public MnemonicStore? mnemonicStore;

    private readonly LspAsmSimulator asmSimulator_;
    private readonly SimStatePipeServer simStatePipeServer_;
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
        AsmDudeLog.TraceSource = this.traceSource;

        //AsmDudeLog.Info("LanguageServer: constructor"); // This lineNumber produces a crash
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
        AsmDudeLog.Info($"LanguageServer: Starting RPC listener. Sender CanWrite={sender.CanWrite}, Reader CanRead={reader.CanRead}");
        this.rpc.StartListening();
        AsmDudeLog.Info("LanguageServer: RPC listener started");

        this.target.OnInitializeCompletion += this.OnTargetInitializeCompletion;
        this.target.OnInitialized += this.OnTargetInitialized;
        this.asmSimulator_ = new LspAsmSimulator(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        this.simStatePipeServer_ = new SimStatePipeServer(this.asmSimulator_)
        {
            // Server owns label reference counting; the VSIX tagger fetches it over the pipe.
            CodeLensDataProvider = this.GetCodeLensData,
            // Server owns mnemonic->doc-URL resolution; the VSIX command fetches it over the pipe.
            MnemonicUrlProvider = this.GetMnemonicUrl,
        };
        this.simStatePipeServer_.Start();
        AsmDudeLog.Info($"LanguageServer: SimStatePipeServer started on pipe '{this.simStatePipeServer_.PipeName}'");
    }

    /// <summary>
    /// Internal constructor for unit testing - initializes basic state without streams/RPC.
    /// The pipe server is NOT started in this path (no VSIX client in tests).
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
        this.simStatePipeServer_ = new SimStatePipeServer(this.asmSimulator_)
        {
            CodeLensDataProvider = this.GetCodeLensData,
            MnemonicUrlProvider = this.GetMnemonicUrl,
        };
        // Do NOT call Start() in tests — no VSIX client to connect
    }

    #region Tools

    private static (int, int) FindWordBoundary(int position, string lineStr)
    {
        // AsmDudeLog.Info($"FindWordBoundary: position = {position}; lineStr=\"{lineStr}\"");
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
        // Cached lines may not exist yet (debounced UpdateInternals hasn't run).
        // Fall back to splitting the raw document text so signature help / completion
        // work immediately after a keystroke.
        TextDocumentItem? doc = this.GetTextDocument(uri);
        if (doc != null)
        {
            return doc.Text.Split(separator, StringSplitOptions.None);
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
        //AsmDudeLog.Info($"ScheduleDiagnosticMessage {message}");

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
    } = "";

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
        AsmDudeLog.Info("LanguageServer: OnTargetInitializeCompletion");
    }

    /// <summary>
    /// Called when initialization is complete. This is called directly instead of using
    /// the OnInitializeCompletion event because StreamJsonRpc proxies events as notifications.
    /// </summary>
    public void OnInitializeComplete()
    {
        AsmDudeLog.Info("LanguageServer: OnInitializeComplete");
        // Dynamically register inlay hints so VS activates the feature (VS prefers dynamic registration).
        var registrationTask = this.SendMethodRequestAsync<RegistrationParams, object>(Methods.ClientRegisterCapabilityName, new RegistrationParams
        {
            Registrations =
            [
                new Registration
                {
                    Id = "asm-dude2-inlay-hint",
                    Method = Methods.TextDocumentInlayHintName,
                    RegisterOptions = new InlayHintRegistrationOptions
                    {
                        DocumentSelector =
                        [
                            new DocumentFilter { Pattern = "**/*.asm" },
                            new DocumentFilter { Pattern = "**/*.cod" },
                            new DocumentFilter { Pattern = "**/*.inc" },
                            new DocumentFilter { Pattern = "**/*.s" },
                        ],
                        ResolveProvider = false,
                    },
                },
            ],
        });
        registrationTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
                AsmDudeLog.Warning($"[OnInitializeComplete] client/registerCapability failed: {t.Exception?.GetBaseException().Message}");
            else
                AsmDudeLog.Info("[OnInitializeComplete] client/registerCapability succeeded");
        }, System.Threading.Tasks.TaskScheduler.Default);
    }

    private void OnTargetInitialized(object sender, EventArgs e)
    {
        AsmDudeLog.Info("LanguageServer: OnTargetInitialized");
        this.OnInitialized?.Invoke(this, EventArgs.Empty);
    }

    public void Initialize(AsmLanguageServerOptions options)
    {
        Debug.Assert(options != null);
        // AsmDudeLog.Info($"Initialize: Options: {jToken}");
        this.options = options;
    }

    public void Initialized()
    {
        string? assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string path = assemblyLocation != null && Path.GetDirectoryName(assemblyLocation) != null
            ? Path.Combine(Path.GetDirectoryName(assemblyLocation), "Resources")
            : "Resources";
        {
            string filename_Regular = Path.Combine(path, "signature-mar2026.txt");
            string filename_Hand = Path.Combine(path, "signature-hand-1.txt");
            this.mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand, this.options!);
            // WriteMnemonicUrlMapping removed — documentation links now handled by context menu command
        }
        {
            string path_performance = Path.Combine(path, "Performance");
            this.performanceStore = new PerformanceStore(path_performance, this.options!);
        }
        {
            this.asmDudeTools = AsmDude2Tools.Create(path, this.traceSource);
        }
    }

private void UpdateInternals(string uri)
        {
            AsmDudeLog.Debug($"[UpdateInternals] ENTRY uri={uri}");
            try
            {
                var document = this.GetTextDocument(uri);
                if (document is not TextDocumentItem)
                {
                    AsmDudeLog.Debug($"[UpdateInternals] document not found for uri={uri}");
                    return;
                }

                var newLines = document.Text.Split(separator, StringSplitOptions.None);
                AsmDudeLog.Debug($"[UpdateInternals] split into {newLines.Length} lines");

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
                AsmDudeLog.Debug($"[UpdateInternals] detected assembler={assemblerType}");

                string[] oldLines;
                if (this.textDocumentLines.TryGetValue(uri, out var cachedLines) && cachedLines.SequenceEqual(newLines))
                {
                    AsmDudeLog.Debug($"[UpdateInternals] lines unchanged, skipping");
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
                AsmDudeLog.Debug($"[UpdateInternals] parsing complete, updating folding ranges");
                this.UpdateFoldingRanges(uri);
                this.labelGraphDirty.Add(uri);

                AsmDudeLog.Debug($"[UpdateInternals] starting AsmSim simulation");
                try
                {
                    this.asmSimulator_.InvalidateAndSimulate(new Uri(uri), newLines,
                        onCompleted: completedUri => this.SendDiagnostics(completedUri.ToString()),
                        onProgress: progressUri =>
                        {
                            AsmDudeLog.Debug($"[UpdateInternals] sending {Methods.WorkspaceInlayHintRefreshName} + pipe notify");
                            _ = this.SendMethodNotificationAsync<object?>(Methods.WorkspaceInlayHintRefreshName, null);
                            this.simStatePipeServer_.NotifySimStateUpdated(progressUri);
                        });
                }
                catch (Exception ex)
                {
                    AsmDudeLog.Warning($"[UpdateInternals] InvalidateAndSimulate failed: {ex.GetType().Name}: {ex.Message}");
                }

                if (false)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    this.UpdateSymbols(uri);
#pragma warning restore CS0162 // Unreachable code detected
                }
                this.SendDiagnostics(uri);
                AsmDudeLog.Debug($"[UpdateInternals] EXIT uri={uri}");
            }
            catch (Exception ex)
            {
                AsmDudeLog.Error($"[UpdateInternals] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

    public void OnTextDocumentOpened(DidOpenTextDocumentParams messageParams)
    {
        var uri = messageParams.TextDocument.Uri.ToString();
        AsmDudeLog.Debug($"[OnTextDocumentOpened] uri={uri}, textLength={messageParams.TextDocument.Text?.Length ?? 0}");
        AsmDudeLog.Info($"[OnTextDocumentOpened] uri={uri}, textLength={messageParams.TextDocument.Text?.Length ?? 0}");

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
        this.simTokenVersions.Remove(uri);
        this.asmSimulator_.CancelAndRemove(new Uri(uri));
    }

    private void UpdateLabelGraph(string uri)
    {
        AsmDudeLog.Info("UpdateLabelGraph");
        this.labelGraphs.Remove(uri);

        TextDocumentItem? textDocument = this.GetTextDocument(uri);
        if (textDocument == null)
        {
            AsmDudeLog.Error($"UpdateLabelGraph: textDocument is null for uri={uri}");
            return;
        }
        string filename = new Uri(textDocument.Uri.ToString()).LocalPath;
        string[] lines = this.GetLines(uri);
        bool caseSensitiveLabels = true; //nasm has case sensitive labels
        if (this.options == null) return;
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
        if (this.options == null || !this.options.CodeFolding_On)
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
        AsmDudeLog.Info($"[UpdateServerSideTextDocument] uri={uri}, version={version}, textLength={text.Length}");
        TextDocumentItem? document = this.GetTextDocument(uri);
        if (document != null)
        {
            document.Text = text;
            document.Version = version;
            AsmDudeLog.Info($"[UpdateServerSideTextDocument] Updated document, scheduling debounced update");

            // Debounce document updates - cancel pending update and schedule new one
            lock (this.updateLock)
            {
                if (this.pendingUpdates.TryGetValue(uri, out var cts))
                {
                    AsmDudeLog.Info($"[UpdateServerSideTextDocument] Cancelling previous pending update");
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
                        AsmDudeLog.Info($"[UpdateServerSideTextDocument] 100ms debounce timeout reached, calling UpdateInternals");
                        this.UpdateInternals(uri);
                        lock (this.updateLock)
                        {
                            this.pendingUpdates.Remove(uri);
                        }
                    }
                    else
                    {
                        AsmDudeLog.Info($"[UpdateServerSideTextDocument] Update was cancelled during debounce");
                    }
                }, TaskScheduler.Default);
            }
        }
    }

    public void SendDiagnostics(string uri)
    {
        var simDiags = this.asmSimulator_.GetDiagnostics(new Uri(uri));
        AsmDudeLog.Debug($"[SendDiagnostics] uri={uri}, simDiags={simDiags.Count}, baseDiags={this.diagnostics.Count}");
        var allDiags = new List<Diagnostic>(this.diagnostics);
        // Pre-fetch document lines for computing character ranges
        string[]? docLines = this.textDocumentLines.TryGetValue(uri, out var dl) ? dl : null;

        foreach (SimDiagnostic sd in simDiags)
        {
            DiagnosticSeverity severity = sd.Kind switch
            {
                SimDiagnosticKind.SyntaxError    => DiagnosticSeverity.Error,
                SimDiagnosticKind.NotImplemented => DiagnosticSeverity.Information,
                SimDiagnosticKind.Unreachable    => DiagnosticSeverity.Warning,  // full wavy underline under entire instruction
                _                                => DiagnosticSeverity.Warning,
            };

            // Use the actual line content to compute precise start/end character positions:
            // start = first non-whitespace char, end = last non-whitespace char + 1.
            string rawLine = (docLines != null && sd.Line < docLines.Length) ? docLines[sd.Line] : string.Empty;
            int startChar = 0;
            int endChar = rawLine.Length;
            if (rawLine.Length > 0)
            {
                startChar = rawLine.Length - rawLine.TrimStart().Length;
                endChar = startChar + rawLine.TrimStart().TrimEnd().Length;
            }

            // For unreachable code: fade the text (Unnecessary) AND show a full wavy underline
            // (IntellisenseError). Both tags together give the same visual as C# unreachable code:
            // faded/greyed text with a green squiggle covering the entire instruction.
            // For all other diagnostics: keep IntellisenseError so they appear in the error list.
            DiagnosticTag[] tags = sd.Kind == SimDiagnosticKind.Unreachable
                ? [DiagnosticTag.Unnecessary, (DiagnosticTag)AsmDiagnosticTag.IntellisenseError]
                : [(DiagnosticTag)AsmDiagnosticTag.IntellisenseError];

            allDiags.Add(new VSDiagnostic
            {
                Message = sd.Message,
                Severity = severity,
                Source = "AsmDude2",
                Code = sd.Kind switch
                {
                    SimDiagnosticKind.SyntaxError    => "SIM-E001",
                    SimDiagnosticKind.NotImplemented => "SIM-I001",
                    SimDiagnosticKind.Unreachable    => "SIM-W001",
                    _                                => "SIM-W002",
                },
                Range = new Range
                {
                    Start = new Position(sd.Line, startChar),
                    End = new Position(sd.Line, endChar),
                },
                Tags = tags,
            });
            AsmDudeLog.Debug($"[SendDiagnostics] diag: line={sd.Line}, severity={severity}, msg={sd.Message}");
        }

        PublishDiagnosticParams parameter = new()
        {
            Uri = new Uri(uri),
            Diagnostics = [.. allDiags],
        };
        _ = this.SendMethodNotificationAsync(Methods.TextDocumentPublishDiagnosticsName, parameter);

        // If there are any unreachable-code diagnostics, invalidate the semantic token result ID
        // and request VS to re-fetch semantic tokens (so unreachable lines are rendered as deprecated/gray).
        bool hasUnreachable = simDiags.Any(d => d.Kind == SimDiagnosticKind.Unreachable);
        if (hasUnreachable)
        {
            this.simTokenVersions[uri] = (this.simTokenVersions.TryGetValue(uri, out int prev) ? prev : 0) + 1;
            _ = this.SendMethodNotificationAsync<object?>(Methods.WorkspaceSemanticTokensRefreshName, null);
            AsmDudeLog.Debug($"[SendDiagnostics] sent workspace/semanticTokens/refresh for {uri}");
        }
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

        return [];

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
        if (this.target.traceSetting == TraceSetting.Verbose)
        {
            AsmDudeLog.Info($"Received: {System.Text.Json.JsonSerializer.Serialize(args)}");
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
        AsmDudeLog.Debug($"Constrain_Signatures: operands.Count={operands2?.Count ?? 0}, operands=[{string.Join(',', operands2 ?? [])}]");

        foreach (AsmSignatureInformation asmSignatureElement in data)
        {
            bool allowed = true; // first assume the signature element is allowed; constrain it later

            //1] constrain the signature on architecture
            if (!asmSignatureElement.Is_Allowed(selectedArchitectures2))
            {
                AsmDudeLog.Debug($"Constrain_Signatures: '{asmSignatureElement.SignatureInformation.Label}' rejected: arch not allowed");
                allowed = false;
            }

            //2] constrain on operands
            if (allowed)
            {
                if ((operands2 == null) || (operands2.Count == 0))
                {
                    // do nothing — no operand constraints
                    AsmDudeLog.Debug($"Constrain_Signatures: '{asmSignatureElement.SignatureInformation.Label}' no operands to constrain");
                }
                else
                {
                    for (int i = 0; i < operands2.Count; ++i)
                    {
                        Operand operand = operands2[i];
                        if (operand == null)
                        {
                            AsmDudeLog.Error($"Constrain_Signatures: somehow got an operand that is null");
                        }
                        else if (operand.IsReg || operand.IsMem || operand.IsImm)
                        {
                            AsmDudeLog.Debug($"Constrain_Signatures: checking operand[{i}]={operand} (IsReg={operand.IsReg}, IsMem={operand.IsMem}, IsImm={operand.IsImm}, Rn={operand.Rn}, NBits={operand.NBits})");
                            if (!asmSignatureElement.Is_Allowed(operand, i))
                            {
                                AsmDudeLog.Debug($"Constrain_Signatures: '{asmSignatureElement.SignatureInformation.Label}' rejected: operand[{i}]={operand} not allowed");
                                allowed = false;
                                break;
                            }
                        }
                        else
                        {
                            AsmDudeLog.Debug($"Constrain_Signatures: operand[{i}]={operand} is not reg/mem/imm, skipping constraint (IsReg={operand.IsReg}, IsMem={operand.IsMem}, IsImm={operand.IsImm})");
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
            if (!this.options.SignatureHelp_On)
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: switched off");
                return null;
            }

            var lines = this.GetLines(parameter.TextDocument.Uri.ToString());
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: lines.Length={lines.Length}");
            int lineNumber = (int)parameter.Position.Line;
            if (lineNumber >= lines.Length)
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: lineNumber {lineNumber} >= lines.Length {lines.Length}, returning null");
                return null;
            }
            string completeLineStr = lines[lineNumber];
            int pos = Math.Min((int)parameter.Position.Character, completeLineStr.Length);
            string lineStr = completeLineStr[..pos];

            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: completeLineStr=\"{completeLineStr}\", lineStr=\"{lineStr}\", pos={pos}");

            if (parameter.Context != null)
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: TriggerKind={parameter.Context.TriggerKind}; triggerChar={parameter.Context.TriggerCharacter}; IsRetrigger={parameter.Context.IsRetrigger}");
            }

            int fileID = 0; //TODO
            (object _, string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID, AssemblerEnum.UNKNOWN);
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: ParseLine result: mnemonic={mnemonic}, args=[{string.Join(",", args)}], label=\"{label}\", remark=\"{remark}\"");

            if (remark.Length > 0)
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: remark found, returning null");
                return null;
            }

            // we backspace we may backspace into the mnemonic
            if ((mnemonic == Mnemonic.NONE))
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: mnemonic is NONE, returning null");
                return null;
            }

            int mnemonicOffset = lineStr.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            if (mnemonicOffset == -1)
            {
                AsmDudeLog.Error($"GetTextDocumentSignatureHelp: mnemonic '{mnemonic}' not found in lineStr '{lineStr}'");
                return null;
            }

            int argsOffset = mnemonicOffset + mnemonic.ToString().Length + 1;
            int argStrLength = (int)parameter.Position.Character - argsOffset;
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: mnemonicOffset={mnemonicOffset}, argsOffset={argsOffset}, argStrLength={argStrLength}");
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: mnemonic={mnemonic}, args=[{string.Join(",", args)}]");

            List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: operands.Count={operands.Count}");
            HashSet<Arch> selectedArchitectures = this.options.Get_Arch_Switched_On();

            IEnumerable<AsmSignatureInformation> x = this.mnemonicStore.GetSignatures(mnemonic);
            int totalSignatures = x.Count();
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: total signatures for {mnemonic}: {totalSignatures}");
            // Re-enumerate since Count() consumed it
            x = this.mnemonicStore.GetSignatures(mnemonic);
            IEnumerable<AsmSignatureInformation> y = this.Constrain_Signatures(x, operands, selectedArchitectures);
            List<SignatureInformation> z = [];
            foreach (AsmSignatureInformation asmSignatureElement in y)
            {
                if (asmSignatureElement.Operands.Count > 0)
                {
                    AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: adding SignatureInformation: {asmSignatureElement.SignatureInformation.Label}");
                    z.Add(asmSignatureElement.SignatureInformation);
                }
            }
            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: constrained signatures: {z.Count}");
            if (z.Count == 0)
            {
                AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: no signatures after filtering, returning null");
                return null; // no signature help present
            }

            // Count actual commas in the argument portion of the line to determine active parameter.
            // Using operands.Count-1 is wrong because trailing commas produce empty entries
            // that are stripped by Split(RemoveEmptyEntries), e.g. "vxorps xmm0, xmm1, "
            // has 2 commas (active param=2) but only 2 parsed operands.
            int nCommas = 0;
            if (argsOffset >= 0 && argsOffset < lineStr.Length)
            {
                for (int i = argsOffset; i < lineStr.Length; i++)
                {
                    if (lineStr[i] == ',') nCommas++;
                }
            }

            // When VS triggers on comma, the document may not yet contain the comma.
            // Bump activeParameter so the next parameter is highlighted immediately.
            if (parameter.Context?.TriggerCharacter == ",")
            {
                nCommas++;
            }

            AsmDudeLog.Debug($"GetTextDocumentSignatureHelp: lineStr=\"{lineStr}\"; pos={parameter.Position.Character}; mnemonic={mnemonic}, nCommas={nCommas}, resultCount={z.Count}");
            return new SignatureHelp()
            {
                ActiveSignature = 0,
                ActiveParameter = nCommas,
                Signatures = [.. z],
            };
        }
        catch (Exception e)
        {
            AsmDudeLog.Error($"GetTextDocumentSignatureHelp: e ={e}");
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

        // Get unreachable lines from simulator so we can mark them with "deprecated" modifier
        var unreachableLines = this.options?.AsmSim_On == true
            ? this.asmSimulator_.GetUnreachableLines(new Uri(uri))
            : [];

        var data = new List<int>();
        int prevLine = 0;
        int prevChar = 0;

        // Process each line
        for (int lineNumber = 0; lineNumber < keywords.Length; lineNumber++)
        {
            var lineTokens = keywords[lineNumber];
            if (lineTokens == null) continue;

            bool lineIsUnreachable = unreachableLines.Contains(lineNumber);

            foreach (var token in lineTokens)
            {
                if (token.Type == AsmTokenType.UNKNOWN) continue;

                // Map AsmTokenType to semantic token type index
                int tokenType = MapTokenType(token.Type);
                if (tokenType < 0) continue;

                int tokenModifiers = GetTokenModifiers(token.Type);

                // Mark all tokens on unreachable lines with "deprecated" modifier (bit 2 = 0x4)
                // so VS renders them with strikethrough or gray color.
                if (lineIsUnreachable)
                    tokenModifiers |= 0x4;

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
        int docVersion = this.textDocuments.TryGetValue(uri, out var doc) ? doc.Version : 0;
        int simVer = this.simTokenVersions.TryGetValue(uri, out int sv) ? sv : 0;
        return $"{docVersion}.{simVer}";
    }

    /// <summary>
    /// Map AsmTokenType to semantic token type index (matching the legend in server capabilities)
    /// </summary>
    /// <param name="type">AsmTokenType from parsed document tokens.</param>
    /// <returns>LSP token type index (0-11), or -1 for UNKNOWN. All types use standard LSP names.</returns>
    /// <remarks>
    /// Token types (all standard LSP 3.17):
    ///   0: keyword (Mnemonic, MnemonicOff)
    ///   1: variable (Register)
    ///   2: type (Label, LabelDef)
    ///   3: macro (Directive, MasmDirective, NasmDirective, MasmPseudoOp, NasmPseudoOp)
    ///   4: number (Constant)
    ///   5: operator (Misc, MasmOperator, NasmOperator)
    ///   6: comment (Remark)
    ///   7: string (not used currently)
    ///   8: function (Jump - CALL targets)
    ///   9: property (UserDefined1)
    ///  10: enumMember (UserDefined2)
    ///  11: namespace (UserDefined3)
    /// </remarks>
    /// <example>
    /// Indices are the standard VS/LSP token-type ordering (matching the SemanticTokensLegend):
    /// MapTokenType(AsmTokenType.Mnemonic) → 15 (keyword)
    /// MapTokenType(AsmTokenType.Register) → 8  (variable)
    /// MapTokenType(AsmTokenType.Label)    → 1  (type)
    /// MapTokenType(AsmTokenType.Jump)     → 12 (function)
    /// </example>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: semantic tokens, token type mapping, LSP protocol, type classification
    /// USED IN: GetSemanticTokens, LanguageServerTarget.GetSemanticTokensFull
    /// SEE ALSO: GetTokenModifiers, SemanticTokensLegend, AsmTokenType
private static int MapTokenType(AsmTokenType type)
     {
         // Indices must match VS's fixed client token type ordering (not our legend order).
         return type switch
         {
             AsmTokenType.Mnemonic => 15,    // keyword
             AsmTokenType.MnemonicOff => 15, // keyword (deprecated - will add modifier)
             AsmTokenType.Register => 8,     // variable (registers) — must match legend index 8
             AsmTokenType.Label => 1,        // type (labels)
             AsmTokenType.LabelDef => 1,     // type (definition)
             AsmTokenType.Jump => 12,        // function (jump target)
             AsmTokenType.Directive => 14,   // macro
             AsmTokenType.Constant => 19,    // number
             AsmTokenType.Remark => 17,      // comment
             AsmTokenType.Misc => 21,        // operator (memory operands, brackets, etc.)
             // MASM/NASM-specific types mapped to standard equivalents
             AsmTokenType.MasmDirective => 14, // macro
             AsmTokenType.NasmDirective => 14, // macro
             AsmTokenType.MasmOperator => 21,  // operator
             AsmTokenType.NasmOperator => 21,  // operator
             AsmTokenType.MasmPseudoOp => 14,  // macro
             AsmTokenType.NasmPseudoOp => 14,  // macro
             // User-defined token types
             AsmTokenType.UserDefined1 => 9,   // property
             AsmTokenType.UserDefined2 => 10,  // enumMember
             AsmTokenType.UserDefined3 => 0,   // namespace
             _ => -1, // Skip UNKNOWN tokens
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
        AsmDudeLog.Debug($"[GetInlayHints] uri={uri}");

        if (!this.textDocumentLines.TryGetValue(uri, out string[]? lines))
        {
            return [];
        }

        var hints = new List<InlayHint>();
        int startLine = parameter.Range.Start.Line;
        int endLine = Math.Min(parameter.Range.End.Line, lines.Length - 1);

        // Add label reference count hints (e.g., "2 references" after label definitions)
        LabelGraph? labelGraph = this.GetLabelGraph(uri);
        if (labelGraph != null && labelGraph.Enabled)
        {
            var definitions = labelGraph.GetFrozenDefinitions();
            var usages = labelGraph.GetFrozenUsages();

            foreach (var kvp in definitions)
            {
                string label = kvp.Key;
                List<KeywordID> defList = kvp.Value;
                KeywordID def = defList[0];
                int defLine = def.LineNumber;

                if (defLine < startLine || defLine > endLine) continue;

                int referenceCount = 0;
                if (usages.TryGetValue(label, out List<KeywordID>? usagesList))
                {
                    referenceCount = usagesList.Count;
                }

                string refText = referenceCount == 1 ? " 1 reference" : $" {referenceCount} references";
                hints.Add(new InlayHint
                {
                    Position = new Position(defLine, def.End_Pos + 1), // after the colon
                    Label = refText,
                    Kind = InlayHintKind.Parameter,
                    PaddingLeft = true,
                });
            }
        }

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

            // Add AsmSim register/flag state hint at end of line (if simulation has results)
            if (this.options?.AsmSim_On == true && this.options?.AsmSim_Decorate_Registers == true)
            {
                // Filtered: only registers/flags the instruction reads or writes (inlay hint)
                // Full state is still used for hover tooltip
                string? stateStrFiltered = this.asmSimulator_.GetRegisterStatesAfterLineFiltered(new Uri(uri), lineNumber);
                string? stateStrFull = this.asmSimulator_.GetRegisterStatesAfterLine(new Uri(uri), lineNumber);
                string? simLabel = BuildSimInlayLabel(stateStrFiltered);
                if (simLabel != null)
                {
                    hints.Add(new InlayHint
                    {
                        Position = new Position(lineNumber, lineText.TrimEnd().Length),
                        Label = simLabel,
                        Kind = InlayHintKind.Type,
                        PaddingLeft = true,
                        ToolTip = stateStrFull,  // hover shows full state
                    });
                }
            }
        }

        return [.. hints];
    }

    private static string? BuildSimInlayLabel(string? stateStr)
    {
        if (string.IsNullOrEmpty(stateStr)) return null;
        var parts = new List<string>();
        foreach (string rawLine in stateStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Trim only trailing whitespace; leading ↔/→/← (single Unicode chars) must be preserved.
            string line = rawLine.TrimEnd().TrimStart(' ');
            if (line.Length == 0) continue;

            // Register line: "↔RAX = 0000000000001010 = 0x000A"  or  "←RBX = ?" (unknown)
            // The prefix char (↔/→/←) is at index 0, register name follows, then " = ".
            int firstEqIdx = line.IndexOf(" = ", StringComparison.Ordinal);
            if (firstEqIdx > 0)
            {
                // regName includes the prefix symbol, e.g. "↔RAX" or "←RBX"
                string regName = line[..firstEqIdx];
                int hexMarker = line.IndexOf("= 0x", StringComparison.Ordinal);
                if (hexMarker >= 0)
                {
                    string hexVal = line[(hexMarker + 4)..].Trim().TrimStart('0');
                    if (hexVal.Length == 0) hexVal = "0";
                    parts.Add($"{regName}=0x{hexVal}");
                }
                else
                {
                    // Unknown value: "↔RAX = ?"
                    parts.Add($"{regName}=?");
                }
            }
            else
            {
                // Flags line: "↔ZF=1 →CF=0 ←SF=?" — include as-is
                parts.Add(line);
            }
        }
        return parts.Count > 0 ? " ; " + string.Join("  ", parts) : null;
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
                //        AsmDudeLog.Info("AsmCompletionSource:Mnemonic_Operand_Completions; register " + keyword + " is selected and has value " + additionalInfo);
                //    }
                //}

                if (att_Syntax)
                {
                    keyword = "%" + keyword;
                }

                Arch arch = RegisterTools.GetArch(regName);
                //AsmDudeLog.Info("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

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

            //AsmDudeLog.Info("CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected);

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
                //AsmDudeLog.Info("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

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

                    //AsmDudeLog.Info("CodeCompletionSource:Selected_Completions; keyword=" + keyword_uppercase + "; arch=" + arch + "; selected=" + selected);

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
                AsmDudeLog.Info($"OnTextDocumentCompletion: switched off");
                return new CompletionList();
            }

            var lines = this.GetLines(parameter.TextDocument.Uri.ToString());
            int lineNumber = (int)parameter.Position.Line;
            if (lineNumber >= lines.Length) return new CompletionList();
            string completeLineStr = lines[lineNumber];
            int pos = Math.Min((int)parameter.Position.Character, completeLineStr.Length);

            // we only consider the line till (and including) the current position
            string lineStr = completeLineStr[..pos];

            char currentChar = this.GetChar(completeLineStr, pos - 1);

            int fileID = 0; //TODO
            (object _, string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID, AssemblerEnum.UNKNOWN);
            if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: lineStr=\"{lineStr}\"; mnemonic={mnemonic}; args={string.Join(',', args)}");

            // if we are typing in a remark: no code completion please
            if (remark.Length > 0)
            {
                return new CompletionList();
            }

            // determine if the current word we are typing is all capitals
            (string currentWord, _, _) = GetWord(pos - 1, lineStr);
            bool useCapitals = (currentWord == currentWord.ToUpper());
            string prefix = currentWord.ToUpperInvariant();

            if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: currentWord=\"{currentWord}\"; useCapitals={useCapitals}");

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
                if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: A");
                return new CompletionList()
                {
                    Items = FilterByPrefix(Selected_Completions(useCapitals, selected, true)),
                };
            }

            int mnemonicOffsetStart = lineStr.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            if (mnemonicOffsetStart == -1)
            {
                AsmDudeLog.Error($"OnTextDocumentCompletion: should not happen: investigate");
                return null;
            }

            // are we with the cursor in the mnemonic: then we should only suggest mnemonics:
            int mnemonicOffsetEnd = mnemonicOffsetStart + mnemonic.ToString().Length;
            if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: pos={pos}; mnemonicOffsetEnd={mnemonicOffsetEnd}");
            if (pos <= mnemonicOffsetEnd)
            {
                HashSet<AsmTokenType> selected = [AsmTokenType.Jump, AsmTokenType.Mnemonic];
                if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: B");
                return new CompletionList()
                {
                    Items = FilterByPrefix(Selected_Completions(useCapitals, selected, true)),
                };
            }

            // if the mnemonic is a jump, we should suggest labels   
            if (AsmTools.AsmSourceTools.IsJump(mnemonic))
            {
                LabelGraph? labelGraph = this.GetLabelGraph(parameter.TextDocument.Uri.ToString());
                if (extraLogging) AsmDudeLog.Info($"OnTextDocumentCompletion: C");
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

            // Count actual commas in the argument portion to determine current operand position.
            int mnemonicOffset2 = lineStr.AsSpan().IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            int argsOffset2 = (mnemonicOffset2 >= 0) ? mnemonicOffset2 + mnemonic.ToString().Length : 0;
            int nCommas = 0;
            for (int i = argsOffset2; i < lineStr.Length; i++)
            {
                if (lineStr[i] == ',') nCommas++;
            }

            IEnumerable<AsmSignatureInformation> allSignatures = this.mnemonicStore.GetSignatures(mnemonic);

            if (extraLogging)
            {
                AsmDudeLog.Info($"OnTextDocumentCompletion: nCommas={nCommas}; operands={string.Join(',', operands)}; allSignatures.Count={allSignatures.Count<AsmSignatureInformation>()}");
                foreach (AsmSignatureInformation s in allSignatures)
                {
                    AsmDudeLog.Info($"OnTextDocumentCompletion: available signatures: {s}");
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
                AsmDudeLog.Info($"OnTextDocumentCompletion: D: useCapitals={useCapitals}; allowed.Count={allowed.Count}");
                foreach (AsmSignatureEnum sig in allowed)
                {
                    AsmDudeLog.Info($"OnTextDocumentCompletion: D: allowed signature {sig}");
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
                AsmDudeLog.Error($"OnTextDocumentCompletion: e={e}");
                return new CompletionList();
            }
        }
    }

    public DocumentHighlight[] GetDocumentHighlights(IProgress<DocumentHighlight[]> progress, Position position, string uri, CancellationToken token)
    {
        if (progress == null)
        {
            AsmDudeLog.Info($"LanguageServer:GetDocumentHighlights: progress is null");
            return [];
        }
        TextDocumentItem? document = this.GetTextDocument(uri);
        if (document == null)
        {
            AsmDudeLog.Info($"LanguageServer:GetDocumentHighlights: document is null");
            return [];
        }

        var lines = this.GetLines(uri);
        if ((int)position.Line >= lines.Length) return [];
        var lineStr2 = lines[(int)position.Line];
        (int startPos, int endPos) = FindWordBoundary((int)position.Character, lineStr2);
        int length = endPos - startPos;

        if (length <= 0)
        {
            AsmDudeLog.Info($"LanguageServer:GetDocumentHighlights: argStrLength too small ({length})");
            return [];
        }
        string currentHighlightedWord = new string(lineStr2.AsSpan(startPos, length));
        if (string.IsNullOrEmpty(currentHighlightedWord))
        {
            AsmDudeLog.Info($"LanguageServer:GetDocumentHighlights: currentHighlightedWord is not significant ({currentHighlightedWord})");
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
        AsmDudeLog.Info($"LanguageServer:GetDocumentHighlights: currentHighlightedWords={string.Join(",", currentHighlightedWords)}");

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
                DefinitionColumn = def.Start_Pos,
                DefinitionLength = def.End_Pos - def.Start_Pos,
                ReferenceLines = [.. refLines],
            });
        }

        return [.. result];
    }

    /// <summary>
    /// Resolve the documentation URL for a mnemonic: the configured <c>AsmDoc_Url</c> base
    /// concatenated with the mnemonic's html reference from <see cref="MnemonicStore"/>.
    /// Returns null if the word is not a known mnemonic or has no documentation reference.
    /// </summary>
    /// <remarks>
    /// The VSIX "open documentation" command fetches this over the SimState pipe
    /// (<see cref="SimStatePipeServer.MnemonicUrlProvider"/>) so the signature files that carry
    /// the html references are parsed only on the server — the VSIX no longer re-reads them.
    /// Honors the user's configurable <c>AsmDoc_Url</c> instead of a hardcoded base.
    /// </remarks>
    public string? GetMnemonicUrl(string mnemonicStr)
    {
        if (string.IsNullOrWhiteSpace(mnemonicStr) || this.mnemonicStore == null || this.options == null)
        {
            return null;
        }

        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(mnemonicStr.ToUpperInvariant(), true);
        if (mnemonic == Mnemonic.NONE)
        {
            return null;
        }

        string htmlRef = this.mnemonicStore.GetHtmlRef(mnemonic);
        if (string.IsNullOrEmpty(htmlRef))
        {
            return null;
        }

        return (this.options.AsmDoc_Url ?? string.Empty) + htmlRef;
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
            AsmDudeLog.Info($"GetDefinition: no lines found for {uri}");
            return null;
        }

        int lineNumber = (int)parameter.Position.Line;
        if (lineNumber >= lines.Length)
        {
            AsmDudeLog.Info($"GetDefinition: line {lineNumber} out of range");
            return null;
        }

        var (word, _, _) = GetWord((int)parameter.Position.Character, lines[lineNumber]);
        if (string.IsNullOrEmpty(word))
        {
            AsmDudeLog.Info($"GetDefinition: no word at position");
            return null;
        }

        AsmDudeLog.Info($"GetDefinition: looking for definition of '{word}'");

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
                            AsmDudeLog.Info($"GetDefinition: found label definition at line {i}, position {labelDefPos}");
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
                    AsmDudeLog.Info($"GetDefinition: found label definition at line {i}, position {labelDefPos}");
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

        AsmDudeLog.Info($"GetDefinition: no definition found for '{word}'");
        return null;
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

        AsmDudeLog.Debug($"[GetDocumentLinks] uri={uri}, linkCount={links.Count}");
        return links.Count > 0 ? [.. links] : null;
    }

    /// <summary>
    /// The <see cref="MarkupKind"/> used for hover <see cref="MarkupContent"/>. Set at initialize from
    /// the client's advertised <c>textDocument.hover.contentFormat</c> (Markdown when offered, else
    /// PlainText). Defaults to Markdown for direct/test callers that don't go through initialize.
    /// </summary>
    internal MarkupKind HoverMarkupKind { get; set; } = MarkupKind.Markdown;

    /// <summary>
    /// Handle hover request. Returns a standard LSP Hover with MarkupContent for mnemonics, registers, and labels.
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
        AsmDudeLog.Debug($"[GetHover] uri={uri}, line={parameter.Position.Line}, char={parameter.Position.Character}");
        if (!this.options.AsmDoc_On)
        {
            AsmDudeLog.Info($"OnHover: switched off");
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

        // Prefer the real parser's classification at this position — GetAsmTokenType is a string-only
        // heuristic that can't recognise labels/constants, but the parsed document already tagged them.
        if (this.parsedDocuments.TryGetValue(uri, out KeywordID[][]? parsedLines)
            && (int)parameter.Position.Line < parsedLines.Length)
        {
            int ch = (int)parameter.Position.Character;
            foreach (KeywordID kid in parsedLines[(int)parameter.Position.Line])
            {
                if ((kid.Start_Pos <= ch) && (ch < kid.End_Pos) && (kid.Type != AsmTokenType.UNKNOWN))
                {
                    tokenType = kid.Type;
                    break;
                }
            }
        }

        // Fallback: a word immediately followed by ':' is a label definition (covers the case the
        // per-line parse doesn't surface here).
        if (tokenType == AsmTokenType.UNKNOWN)
        {
            string lineText = lines[(int)parameter.Position.Line];
            if ((endPos < lineText.Length) && (lineText[endPos] == ':'))
            {
                tokenType = AsmTokenType.LabelDef;
            }
        }

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

                    hoverContent = [
                        full_Descr,
                        (performanceInfoAvailable) ? "\nPerformance:\n" + performanceStr : "",
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
                            if (simBefore != null) sb.Append($"Before: {simBefore}\n");
                            if (simAfter != null)  sb.Append($"After : {simAfter}");
                            simSuffix = sb.ToString();
                        }

                        hoverContent = [
                            $"Register {regStr}: {full_Descr}{simSuffix}",
                            ];
                    }
                    break;
                }
            case AsmTokenType.Constant:
                {
                    (bool valid, ulong value, int nBits) = AsmTools.AsmSourceTools.Evaluate_Constant(keyword);
                    hoverContent = valid
                        ? [$"Constant {value}d = {value.ToString("X", CultureUI)}h = {AsmTools.AsmSourceTools.ToStringBin(value, nBits)}b"]
                        : [$"Constant {keyword}"];
                    break;
                }
            case AsmTokenType.LabelDef:
                {
                    // The cursor is ON the definition, so the definition line IS the current line.
                    int defLine = (int)parameter.Position.Line;
                    hoverContent = [
                        $"Label definition {keyword}",
                        $"Defined at line {defLine + 1}: {lines[defLine].Trim()}",
                    ];
                    break;
                }
            case AsmTokenType.Label:
                {
                    // A label reference; the definition line would need the label graph (future).
                    hoverContent = [$"Label {keyword}"];
                    break;
                }
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

                    AsmDudeLog.Info(("AsmQuickInfoSource:AugmentQuickInfoSession: found label def " + full_Qualified_Label);

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
            //AsmDudeLog.Info((string.Format(Tools.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
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

            // Standard LSP Hover with MarkupContent (Markdown or PlainText per the client's advertised
            // contentFormat, see HoverMarkupKind). For mnemonics/jumps append a clickable doc link.
            string? docUrl = (!string.IsNullOrEmpty(hoverKeyword) && (tokenType is AsmTokenType.Mnemonic or AsmTokenType.Jump))
                ? this.GetMnemonicUrl(hoverKeyword)
                : null;

            return HoverBuilder.CreateHover(this.HoverMarkupKind, hoverContent, line, startPos, endPos, docUrl);
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
                AsmDudeLog.Info($"GetProvenStates: no cache entry for {uri}");
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
            AsmDudeLog.Info($"GetProvenStates exception: {ex.Message}");
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
    public static bool UseStdio
    {
        get => AsmDudeLog.UseStdio;
        set => AsmDudeLog.UseStdio = value;
    }

    private static readonly string[] separator = ["\r\n", "\n"];

    public static void LogInfo(string message) => AsmDudeLog.Info(message);

    public static void LogWarning(string message) => AsmDudeLog.Warning(message);

    public static void LogToFile(string message) => AsmDudeLog.Debug(message);

    public static void LogError(string message)
    {
        AsmDudeLog.Error(message);
        Instance?.MakeWindowVisible();
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
        AsmDudeLog.Info($"LanguageServer: ShowMessage: message={message}; messageType={messageType.ToString()}");
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
                        AsmDudeLog.Info($"SendSettings: maxNumberOfProblems = {newMaxProblems}");
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            AsmDudeLog.Info($"SendSettings: Failed to parse settings: {ex.Message}");
        }

        AsmDudeLog.Info($"SendSettings: received settings update");
    }

    public void Exit()
    {
        this.disconnectEvent.Set();

        Disconnected?.Invoke(this, new EventArgs());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0) return;
        this.simStatePipeServer_.Dispose();
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
    //            AsmDudeLog.Info($"Failed to apply edit: {response.FailureReason}");
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
        AsmDudeLog.Warning($"OnRpcDisconnected: Reason={e.Reason}, Description={e.Description}, Exception={e.Exception?.Message}");
        if (e.Exception != null)
        {
            AsmDudeLog.Error($"OnRpcDisconnected Exception: {e.Exception}");
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



