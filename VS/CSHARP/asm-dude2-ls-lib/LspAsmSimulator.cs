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

namespace AsmDude2LS
{
    using AsmSim;
    using AsmSim.Mnemonics;

    using AsmTools;

    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using AsmSimState = AsmSim.State;
    using AsmSimTools = AsmSim.Tools;

    internal enum SimDiagnosticKind
    {
        SyntaxError,
        UsageUndefined,
        Unreachable,
        NotImplemented,
    }

    internal readonly record struct SimDiagnostic(int Line, string Message, SimDiagnosticKind Kind);

    /// <summary>
    /// Per-document assembly simulator. Uses SimpleStep_Forward (no DynamicFlow)
    /// so it is safe from the Z3 context lifecycle bug.
    ///
    /// All Z3 operations are performed in the background simulation thread and
    /// results are stored as pre-computed strings. Hover queries are O(1) string lookups.
    ///
    /// Features ported from AsmDude1:
    ///   - Before/after register+flag state per line
    ///   - Syntax error diagnostics (via Runner.InstantiateOpcode / IsHalted)
    ///   - Usage-of-undefined diagnostics (FlagsReadStatic / RegsReadStatic vs before-state)
    ///   - Unreachable-instruction diagnostics (before-state.IsConsistent == Tv.ZERO)
    ///   - Not-implemented instruction marking
    /// </summary>
    internal sealed class LspAsmSimulator : IDisposable
    {
        internal const int MaxLines = 200;

        /// <summary>
        /// Milliseconds of inactivity after the last document change before simulation starts.
        /// Prevents a Z3 thread from launching on every keystroke.
        /// </summary>
        private const int DebounceMs = 3000;

        /// <summary>
        /// Minimum interval between in-loop <c>onProgress</c> notifications during a single
        /// simulation pass. Without this, the simulator notifies the client once per simulated
        /// line, and each notification makes the VS CodeLens tagger re-tag the WHOLE document
        /// (full <c>UpdateTagsAsync</c>), invalidating every lens across the OOP boundary every
        /// few hundred ms. Coalescing keeps the client's lens cache warm so scrolling hits it.
        /// The unconditional final flush after the loop guarantees the last state is published.
        /// </summary>
        private const long ProgressNotifyThrottleMs = 400;

        /// <summary>A per-line wall-time at/above this (ms) is counted as a Z3 timeout in the run
        /// summary: the per-line Z3 timeout is 5000 ms, so a line near it almost certainly timed out.
        /// Heuristic, for observability only.</summary>
        private const long SlowLineThresholdMs = 4500;

        internal sealed class DocCache
        {
            internal readonly Dictionary<int, string?> lineStringsAfter = [];
            internal readonly Dictionary<int, string?> lineStringsBefore = [];
            /// <summary>
            /// Reads of the instruction at key line: r: prefixed, values from before-state.
            /// Shown as CodeLens ABOVE the instruction line.
            /// </summary>
            internal readonly Dictionary<int, string?> lineStringsReadLabels = [];
            /// <summary>
            /// Writes of the instruction at key line: w: prefixed, values from after-state.
            /// Shown as CodeLens BELOW the instruction line (= above key+1).
            /// </summary>
            internal readonly Dictionary<int, string?> lineStringsWriteLabels = [];
            internal readonly List<SimDiagnostic> diagnostics = [];

            internal string? GetBeforeState(int lineNumber) => this.lineStringsBefore.TryGetValue(lineNumber, out var s) ? s : null;
            internal string? GetAfterState(int lineNumber) => this.lineStringsAfter.TryGetValue(lineNumber, out var s) ? s : null;
        }

        private readonly Dictionary<Uri, DocCache> cache_ = [];
        private readonly Dictionary<Uri, CancellationTokenSource> pendingTasks_ = [];
        private readonly Dictionary<Uri, long> simVersion_ = [];
        private readonly object lockObj_ = new();

        internal LspAsmSimulator()
        {
        }

        // Forwards the real caller's member:line so each ASMSIM line is greppable to its source.
        private static void Log(string msg,
            [System.Runtime.CompilerServices.CallerMemberName] string member = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
            => AsmLog.Log(AsmLogLevel.Debug, "ASMSIM", msg, member, line);

        /// <summary>Per-RUN sim summary for the VS "AsmDude2 Language Server" pane. Emitted at Info with
        /// <c>force</c> so it shows even when the deployed (Release) build's threshold is Warn — like the
        /// startup banner, and for the same reason (it's once-per-run and the user wants to see the sim
        /// working). A hard <c>ASMDUDE_LOGLEVEL=off</c> still silences it. Keep per-LINE chatter on <see
        /// cref="Log"/> (Debug → disk log only, raise with ASMDUDE_LOGLEVEL=debug).</summary>
        private static void LogInfo(string msg,
            [System.Runtime.CompilerServices.CallerMemberName] string member = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
            => AsmLog.Log(AsmLogLevel.Info, "ASMSIM", msg, member, line, force: true);

        /// <summary>
        /// Trigger a background re-simulation for the given document.
        /// Called whenever the document content changes.
        /// Debounced: simulation only starts after <see cref="DebounceMs"/> ms of inactivity,
        /// so rapid keystrokes each cancel the previous pending run rather than pile up Z3 threads.
        /// The optional <paramref name="onCompleted"/> callback is invoked (from the background
        /// thread) after simulation finishes, so callers can re-publish diagnostics.
        /// </summary>
        internal void InvalidateAndSimulate(Uri uri, IReadOnlyList<string> lines, Action<Uri>? onCompleted = null, Action<Uri>? onProgress = null)
        {
            Log($"InvalidateAndSimulate: {lines.Count} lines");
            CancellationTokenSource cts;
            long version;
            lock (this.lockObj_)
            {
                if (this.pendingTasks_.TryGetValue(uri, out CancellationTokenSource? existing))
                {
                    existing.Cancel();
                    existing.Dispose();
                }
                cts = new CancellationTokenSource();
                this.pendingTasks_[uri] = cts;
                // Bump version: any in-flight thread with the old version will stop writing.
                version = this.simVersion_.TryGetValue(uri, out long v) ? v + 1 : 1;
                this.simVersion_[uri] = version;
                // Put a fresh (empty) cache entry immediately so stale data from the previous simulation
                // is invisible while the new one runs. The cache holds only strings; the Z3 states of the
                // previous run are owned and disposed by that run's own RunSimulation finally (the bumped
                // version + cancelled token make it stop writing and unwind), so there is nothing to
                // dispose here.
                this.cache_[uri] = new DocCache();
            }

            _ = Task.Run(async () =>
            {
                // Debounce: wait for inactivity before starting expensive Z3 work.
                // If another keystroke arrives within DebounceMs, this token is cancelled
                // and the delay throws OperationCanceledException — no simulation starts.
                try { await Task.Delay(DebounceMs, cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }

                this.RunSimulation(uri, version, lines, cts.Token, onCompleted, onProgress);
            }, cts.Token);
        }

        /// <summary>
        /// Test seam — run the simulation SYNCHRONOUSLY and deterministically (no debounce, no
        /// background <see cref="Task"/>), then return once the per-line cache is fully populated. It
        /// performs the same cache/version setup <see cref="InvalidateAndSimulate"/> does and calls the
        /// very same <see cref="RunSimulation"/> body — it is NOT a re-implementation, so a
        /// characterization test built on it exercises the real production simulator. Used by the
        /// headless <c>LspAsmSimulatorTests</c> (the golden baseline for the planned engine swap), which
        /// avoids the flaky sleep-and-poll the older <c>AsmSimTests</c> uses.
        /// </summary>
        internal void SimulateSynchronouslyForTest(Uri uri, IReadOnlyList<string> lines, SimEngineMode engine = SimEngineMode.Linear)
        {
            // Tests pin the engine explicitly (default Linear, the golden-baseline engine) so they are
            // independent of the production default and of the ASMDUDE_SIM_ENGINE env var.
            this.engineMode_ = engine;
            long version;
            lock (this.lockObj_)
            {
                if (this.pendingTasks_.TryGetValue(uri, out CancellationTokenSource? existing))
                {
                    existing.Cancel();
                    existing.Dispose();
                    this.pendingTasks_.Remove(uri);
                }
                version = this.simVersion_.TryGetValue(uri, out long v) ? v + 1 : 1;
                this.simVersion_[uri] = version;
                this.cache_[uri] = new DocCache();
            }
            this.RunSimulation(uri, version, lines, CancellationToken.None, onCompleted: null, onProgress: null);
        }

        /// <summary>
        /// Cancel any in-flight or pending simulation for <paramref name="uri"/> and release
        /// all cached data for that document. Called when the document is closed.
        /// </summary>
        internal void CancelAndRemove(Uri uri)
        {
            Log($"CancelAndRemove: {uri}");
            lock (this.lockObj_)
            {
                if (this.pendingTasks_.TryGetValue(uri, out CancellationTokenSource? cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    this.pendingTasks_.Remove(uri);
                }
                this.simVersion_.Remove(uri);
                // Cancelling the token above makes the in-flight RunSimulation (if any) unwind and dispose
                // its own Z3 states in its finally; the cache itself holds only strings.
                this.cache_.Remove(uri);
            }
        }

        // ── After-state queries ────────────────────────────────────────────────

        /// <summary>
        /// Pre-computed register+flag state string after the given line, or null if not available.
        /// Full state — used for hover tooltips.
        /// </summary>
        internal string? GetRegisterStatesAfterLine(Uri uri, int lineNumber)
            => this.GetCachedString(uri, lineNumber, after: true);

        /// <summary>
        /// Full register+flag state string after the given line (same as <see cref="GetRegisterStatesAfterLine"/>).
        /// Kept for callers that previously used a filtered variant; the full state is returned.
        /// </summary>
        internal string? GetRegisterStatesAfterLineFiltered(Uri uri, int lineNumber)
            => this.GetCachedString(uri, lineNumber, after: true);

        /// <summary>
        /// Pre-computed value string for a specific register after the given line
        /// (e.g. "0000000000001010 = 0x000A"), or null if unavailable.
        /// </summary>
        internal string? GetRegisterValueAfterLine(Uri uri, int lineNumber, Rn reg)
            => ExtractRegisterValue(this.GetRegisterStatesAfterLine(uri, lineNumber), reg);

        // ── Before-state queries ───────────────────────────────────────────────

        /// <summary>
        /// Pre-computed register+flag state string before the given line, or null if not available.
        /// </summary>
        internal string? GetRegisterStatesBeforeLine(Uri uri, int lineNumber)
            => this.GetCachedString(uri, lineNumber, after: false);

        /// <summary>
        /// Pre-computed value string for a specific register before the given line, or null if unavailable.
        /// </summary>
        internal string? GetRegisterValueBeforeLine(Uri uri, int lineNumber, Rn reg)
            => ExtractRegisterValue(this.GetRegisterStatesBeforeLine(uri, lineNumber), reg);

        // ── Diagnostics query ──────────────────────────────────────────────────

        /// <summary>
        /// Returns all simulation diagnostics (syntax errors, usage-of-undefined, unreachable)
        /// for the given document, or an empty list if no results are available yet.
        /// </summary>
        internal IReadOnlyList<SimDiagnostic> GetDiagnostics(Uri uri)
        {
            lock (this.lockObj_)
            {
                if (this.cache_.TryGetValue(uri, out DocCache? entry))
                {
                    return entry.diagnostics;
                }
            }
            return [];
        }

        /// <summary>
        /// Returns the set of line numbers that the simulator has proven unreachable.
        /// Empty if simulation has not yet run or found no unreachable lines.
        /// </summary>
        internal HashSet<int> GetUnreachableLines(Uri uri)
        {
            lock (this.lockObj_)
            {
                if (this.cache_.TryGetValue(uri, out DocCache? entry))
                {
                    var result = new HashSet<int>();
                    foreach (var d in entry.diagnostics)
                    {
                        if (d.Kind == SimDiagnosticKind.Unreachable)
                            result.Add(d.Line);
                    }
                    return result;
                }
            }
            return [];
        }

        /// <summary>
        /// Converts a multi-line state string like "rax = 0x0000000000000008\n..." into
        /// a compact single line like "rax=8, ZF=1".  Returns null if the input is empty.
        /// </summary>
        internal static string? CompactStateString(string? stateStr)
        {
            if (string.IsNullOrEmpty(stateStr)) return null;
            var parts = new List<string>();
            foreach (string rawLine in stateStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string s = rawLine.Trim();

                // A register line is "prefix:NAME = <binary> [= <hex>]" (note the spaces around the first
                // '='). A flag line is already compact ("w:ZF=0 w:CF=1", no surrounding spaces). The
                // register form MUST be normalized to a space-free "prefix:NAME=value" — otherwise
                // ParseCompactItems (which splits on spaces) drops it, and an unknown-valued write then
                // silently vanishes from the merged CodeLens (the write looks "overwritten" by the read).
                int regSep = s.IndexOf(" = ", StringComparison.Ordinal);
                if (regSep > 0)
                {
                    string regName = s[..regSep].Trim();
                    int hexMarker = s.IndexOf("= 0x", StringComparison.Ordinal);
                    string val;
                    if (hexMarker >= 0)
                    {
                        string hexVal = s[(hexMarker + 4)..].Trim().TrimStart('0');
                        if (hexVal.Length == 0) hexVal = "0";
                        val = "0x" + hexVal;
                    }
                    else
                    {
                        // Binary-only value (e.g. an unknown register: 0b????_…). ToStringBin uses '_'
                        // separators, not spaces, so the value is already space-free.
                        val = s[(regSep + 3)..].Trim();
                    }
                    parts.Add($"{regName}={val}");
                }
                else
                {
                    int eq = s.IndexOf('=');
                    if (eq > 0) parts.Add(s.Trim());
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// Returns the cached entry for the given document, or null if not available.
        /// </summary>
        internal DocCache? GetCachedEntry(Uri uri)
        {
            lock (this.lockObj_)
            {
                this.cache_.TryGetValue(uri, out DocCache? entry);
                return entry;
            }
        }

        /// <summary>
        /// Snapshot this document's per-line simulation results as a producer-agnostic
        /// <see cref="SimResultSet"/> (before/after state, read/write CodeLens labels, per-line
        /// diagnostics) — the shape <see cref="SimResultComparer"/> diffs. Used now for determinism
        /// checks and, when the per-component engine lands, as the S0 shadow oracle that compares the
        /// linear engine against it (INCREMENTAL_SIM_PLAN.md §5). Returns an empty set if the document
        /// has no cached simulation.
        /// </summary>
        internal SimResultSet ToResultSet(Uri uri, string label)
        {
            var lines = new Dictionary<int, SimLineResult>();
            lock (this.lockObj_)
            {
                if (this.cache_.TryGetValue(uri, out DocCache? entry))
                {
                    // Diagnostics are a flat list; group them per line (the comparer sorts within a line).
                    var diagByLine = new Dictionary<int, List<string>>();
                    foreach (SimDiagnostic d in entry.diagnostics)
                    {
                        if (!diagByLine.TryGetValue(d.Line, out List<string>? list))
                        {
                            list = [];
                            diagByLine[d.Line] = list;
                        }
                        list.Add($"{d.Kind}:{d.Message}");
                    }

                    var keys = new HashSet<int>();
                    foreach (int k in entry.lineStringsBefore.Keys) keys.Add(k);
                    foreach (int k in entry.lineStringsAfter.Keys) keys.Add(k);
                    foreach (int k in entry.lineStringsReadLabels.Keys) keys.Add(k);
                    foreach (int k in entry.lineStringsWriteLabels.Keys) keys.Add(k);
                    foreach (int k in diagByLine.Keys) keys.Add(k);

                    foreach (int k in keys)
                    {
                        lines[k] = new SimLineResult(
                            entry.lineStringsBefore.GetValueOrDefault(k),
                            entry.lineStringsAfter.GetValueOrDefault(k),
                            entry.lineStringsReadLabels.GetValueOrDefault(k),
                            entry.lineStringsWriteLabels.GetValueOrDefault(k),
                            diagByLine.GetValueOrDefault(k));
                    }
                }
            }
            return new SimResultSet(label, lines);
        }

        // ── S2: per-component DynamicFlow engine + SIMDIFF shadow ───────────────────────────────────
        // The component engine is a SEPARATE path; the linear engine stays authoritative. Selected by the
        // ASMDUDE_SIM_ENGINE env var (linear | shadow). In "shadow" the linear run drives the editor and
        // the component engine runs compute-only, with every per-line before/after difference logged under
        // the SIMDIFF category — so the merge-vs-linear semantic change is OBSERVED before anything flips.
        // INCREMENTAL_SIM_PLAN.md S2 (no editor flip yet).
        internal enum SimEngineMode
        {
            /// <summary>The linear single-step sim — the EDITOR DEFAULT. Single-path (does NOT follow jump
            /// targets, resets at labels) so join-point values are imprecise (read as unknown), BUT it
            /// writes the per-line cache INCREMENTALLY, so CodeLens/hover appear progressively as it runs.</summary>
            Linear,

            /// <summary>Editor uses the linear sim; the component engine runs compute-only and SIMDIFF-logs.</summary>
            Shadow,

            /// <summary>The DYNAMIC per-component (DynamicFlow) engine. More precise at join points (follows
            /// jumps, merges branch states), but NON-INCREMENTAL — it computes the whole document before
            /// writing the cache, so on a real (slow-Z3) file NOTHING shows until it finishes (minutes).
            /// NOT yet usable as the editor default; behind <c>ASMDUDE_SIM_ENGINE=component</c> until it
            /// gains per-line incremental writes. See INCREMENTAL_SIM_PLAN.md.</summary>
            Component,
        }

        /// <summary>The configured engine from the environment; the editor default is <see cref="SimEngineMode.Linear"/>.</summary>
        private static readonly SimEngineMode SimEngine = ParseSimEngine();

        /// <summary>Per-instance engine (defaults to <see cref="SimEngine"/>). The test seam pins this to
        /// <see cref="SimEngineMode.Linear"/> so the linear golden-baseline tests still exercise linear.</summary>
        private SimEngineMode engineMode_ = SimEngine;

        private static SimEngineMode ParseSimEngine()
        {
            string? v = Environment.GetEnvironmentVariable("ASMDUDE_SIM_ENGINE");
            if (string.Equals(v, "linear", StringComparison.OrdinalIgnoreCase)) return SimEngineMode.Linear;
            if (string.Equals(v, "shadow", StringComparison.OrdinalIgnoreCase)) return SimEngineMode.Shadow;
            if (string.Equals(v, "component", StringComparison.OrdinalIgnoreCase)) return SimEngineMode.Component;
            // DEFAULT = Linear. The merge engine (Component) is more precise but currently NON-INCREMENTAL
            // (no CodeLens appear until the whole file finishes — verified empirically), so it can't be the
            // editor default yet. Opt in with ASMDUDE_SIM_ENGINE=component.
            return SimEngineMode.Linear;
        }

        /// <summary>Loop-handling strategy the component engine uses (ASMDUDE_SIM_LOOP env var:
        /// accept|modsethavoc|peelonce|fullunroll|fixpoint). Default Accept (legacy loop behavior).</summary>
        private static readonly AsmSim.LoopHandling SimLoopHandling =
            Enum.TryParse(Environment.GetEnvironmentVariable("ASMDUDE_SIM_LOOP"), ignoreCase: true, out AsmSim.LoopHandling lh)
                ? lh
                : AsmSim.LoopHandling.Accept;

        /// <summary>The same register/flag tracking the linear sim uses (RAX..R15 + CF/ZF/SF/OF). Shared by
        /// both engines so a SIMDIFF comparison is apples-to-apples.</summary>
        private static void EnableFullStateConfig(AsmSimTools tools)
        {
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.RSI = true;
            tools.StateConfig.RDI = true;
            tools.StateConfig.RSP = true;
            tools.StateConfig.RBP = true;
            tools.StateConfig.R8 = true;
            tools.StateConfig.R9 = true;
            tools.StateConfig.R10 = true;
            tools.StateConfig.R11 = true;
            tools.StateConfig.R12 = true;
            tools.StateConfig.R13 = true;
            tools.StateConfig.R14 = true;
            tools.StateConfig.R15 = true;
            tools.StateConfig.CF = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.SF = true;
            tools.StateConfig.OF = true;
        }

        /// <summary>
        /// The per-component engine: partition the document into weakly-connected CFG components and
        /// simulate each with its own multi-root <see cref="DynamicFlow"/> (own Z3 context, distinct
        /// per-component seeded RNG — the §1 parallel-safety seam), extracting per-line before/after state
        /// strings. Returns a before/after-only <see cref="SimResultSet"/> (labels/diagnostics not yet
        /// produced by this engine — S2b). Never throws into the caller; a failing component is logged and
        /// skipped. NOTE: heavier than the linear sim (real merge machinery) — only run in shadow/analysis.
        /// </summary>
        /// <summary>
        /// 1:1 line-preserving rewrite that lifts the linear sim's <c>#pragma assume</c> feature into the
        /// CFG fed to the component engine: <c>#pragma assume X</c> becomes the instruction <c>X</c> (the
        /// assumption becomes a real step on the path) and <c>#pragma assume HLT</c> becomes <c>HLT</c>
        /// (Runner returns null for HLT ⇒ the path halts ⇒ the next line becomes an in-degree-0 root that
        /// the multi-root construction seeds with a FRESH state — reproducing the HLT reset). Indices are
        /// preserved, so the ORIGINAL <paramref name="lines"/> still drive the display filter (a pragma
        /// slot parses to Mnemonic.NONE and is not emitted), matching the linear sim line-for-line.
        /// </summary>
        private static string[] RewritePragmasForCfg(IReadOnlyList<string> lines)
        {
            const string pragmaPrefix = "#pragma assume ";
            var result = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith(pragmaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string assume = trimmed[pragmaPrefix.Length..].Trim();
                    int commentIdx = assume.IndexOf(';');
                    if (commentIdx >= 0) assume = assume[..commentIdx].Trim();
                    result[i] = assume; // "X" or "HLT"
                }
                else
                {
                    result[i] = lines[i];
                }
            }
            return result;
        }

        /// <summary>One line's component-engine output, with diagnostics kept as structured objects (so the
        /// editor cache can consume them; the shadow path stringifies them for comparison).</summary>
        private sealed record ComponentLine(string? Before, string? After, string? ReadLabel, string? WriteLabel, List<SimDiagnostic> Diagnostics);

        private SimResultSet BuildComponentResultSet(string label, IReadOnlyList<string> lines)
        {
            var result = new Dictionary<int, SimLineResult>();
            foreach (KeyValuePair<int, ComponentLine> kv in this.ComputeComponentLines(lines))
            {
                ComponentLine cl = kv.Value;
                List<string>? diagStrings = null;
                if (cl.Diagnostics.Count > 0)
                {
                    diagStrings = [];
                    foreach (SimDiagnostic d in cl.Diagnostics) diagStrings.Add($"{d.Kind}:{d.Message}");
                }
                result[kv.Key] = new SimLineResult(cl.Before, cl.After, cl.ReadLabel, cl.WriteLabel, diagStrings);
            }
            return new SimResultSet(label, result);
        }

        /// <summary>Run the dynamic per-component engine and return per-line before/after state, CodeLens
        /// read/write labels, and diagnostics — driven from the dynamic states via the SAME per-line logic
        /// the linear sim uses. Shared by the shadow comparison and the component-engine editor path.
        /// <para><paramref name="onLine"/>, when given, is invoked the moment each line is extracted (topo
        /// order), so the editor path can write the cache + refresh CodeLens INCREMENTALLY rather than after
        /// the whole document. <paramref name="ct"/> stops the build between vertices.</para></summary>
        private Dictionary<int, ComponentLine> ComputeComponentLines(
            IReadOnlyList<string> lines,
            CancellationToken ct = default,
            Action<int, ComponentLine>? onLine = null)
        {
            var result = new Dictionary<int, ComponentLine>();
            try
            {
                var settings = new Dictionary<string, string>
                {
                    { "timeout", "5000" },
                    { "random_seed", "0" },
                };

                // The CFG sees the pragma-lifted program; display/filtering still uses the original lines.
                string[] effective = RewritePragmasForCfg(lines);
                var sFlow = new StaticFlow(new AsmSimTools(settings));
                sFlow.Update(string.Join(Environment.NewLine, effective), removeEmptyLines: false);

                IReadOnlyDictionary<int, int> lineToComponent = sFlow.ComputeLineToComponent();
                IReadOnlyDictionary<int, List<int>> entriesByComponent = sFlow.ComputeComponentEntryLines();

                var linesByComponent = new Dictionary<int, List<int>>();
                foreach (var (line, componentId) in lineToComponent)
                {
                    if (!linesByComponent.TryGetValue(componentId, out List<int>? bucket))
                    {
                        bucket = [];
                        linesByComponent[componentId] = bucket;
                    }
                    bucket.Add(line);
                }

                foreach (var (componentId, roots) in entriesByComponent)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        // Distinct seed per component ⇒ parallel-safe (no shared Random) + reproducible.
                        var compTools = new AsmSimTools(settings, string.Empty, componentId);
                        EnableFullStateConfig(compTools);
                        compTools.Quiet = true;
                        compTools.LoopHandling = SimLoopHandling; // ASMDUDE_SIM_LOOP

                        int compLineCount = linesByComponent.TryGetValue(componentId, out List<int>? cls) ? cls.Count : 0;
                        var constructClock = System.Diagnostics.Stopwatch.StartNew();
                        using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, roots, compTools);
                        constructClock.Stop();
                        AsmLog.Info("ASMSIM", $"[component] component {componentId}: DynamicFlow built in {constructClock.ElapsedMilliseconds} ms ({compLineCount} lines)");

                        // ComponentEvaluator drives the per-vertex worklist; onLineReady fires the instant a
                        // line's before/after states resolve (topo order), so we extract + stream THAT line
                        // immediately. This is the incrementality the linear sim has — CodeLens/hover appear
                        // progressively instead of after the whole (slow-Z3) component finishes.
                        using var ev = new ComponentEvaluator(dFlow, sFlow,
                            onLineReady: (line, before, after) =>
                            {
                                // Skip phantom vertices past the last instruction (CFG end line N of an
                                // N-line program); the editor/linear sim only annotate real document lines.
                                if (line < 0 || line >= lines.Count || ct.IsCancellationRequested) return;
                                try
                                {
                                    var lineClock = System.Diagnostics.Stopwatch.StartNew();
                                    ComponentLine? cl = this.ExtractComponentLine(lines, line, before, after, compTools);
                                    lineClock.Stop();
                                    if (cl != null)
                                    {
                                        result[line] = cl;
                                        onLine?.Invoke(line, cl);
                                        AsmLog.Info("ASMSIM", $"[component] line {line + 1}: extracted in {lineClock.ElapsedMilliseconds} ms");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"[component] line {line} extraction failed: {ex.GetType().Name}: {ex.Message}");
                                }
                            },
                            cancel: ct);
                    }
                    catch (Exception ex)
                    {
                        Log($"[SHADOW] component {componentId} failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[SHADOW] component engine failed: {ex.GetType().Name}: {ex.Message}");
            }
            return result;
        }

        /// <summary>Turn one line's dynamic before/after states into its editor annotations (state strings,
        /// CodeLens read/write labels, diagnostics) — the SAME per-line logic the linear sim uses. The heavy
        /// Z3 value-solving (ComputeStateString → solve each register) happens HERE, per line, so this is
        /// what dominates a slow run. Returns null for non-instruction lines / nothing-to-show.</summary>
        private ComponentLine? ExtractComponentLine(IReadOnlyList<string> lines, int line, AsmSimState? before, AsmSimState? after, AsmSimTools compTools)
        {
            // Match the linear sim EXACTLY: only real instruction lines carry annotations.
            (_, _, Mnemonic mnemonic, string[] args, _) = AsmSourceTools.ParseLine(lines[line].Trim(), -1, -1, AssemblerEnum.UNKNOWN);
            if (mnemonic == Mnemonic.NONE) return null;

            string? beforeStr = before is null ? null : ComputeStateString(before);
            string? afterStr = after is null ? null : ComputeStateString(after);

            string? readLabel = null;
            string? writeLabel = null;
            var diags = new List<SimDiagnostic>();
            if (before != null)
            {
                var dummyKeys = ("d_p", "d_n", "d_b");
                using OpcodeBase? op = Runner.InstantiateOpcode(mnemonic, args, dummyKeys, compTools);

                var writtenRegs = new HashSet<Rn>();
                Flags writtenFlags = Flags.NONE;
                var readRegs = new HashSet<Rn>();
                Flags readFlags = Flags.NONE;
                if (op != null)
                {
                    foreach (Rn r in op.RegsWriteStatic) writtenRegs.Add(RegisterTools.Get64BitsRegister(r));
                    writtenFlags = op.FlagsWriteStatic;
                    foreach (Rn r in op.RegsReadStatic) readRegs.Add(RegisterTools.Get64BitsRegister(r));
                    readFlags = op.FlagsReadStatic;
                }

                readLabel = ComputeReadLabel(before, readRegs, readFlags);
                if (after != null) writeLabel = ComputeWriteLabel(after, writtenRegs, writtenFlags);
                this.CollectDiagnostics(lines[line], line, before, compTools, diags, op);
            }

            return (beforeStr != null || afterStr != null || readLabel != null || writeLabel != null || diags.Count > 0)
                ? new ComponentLine(beforeStr, afterStr, readLabel, writeLabel, diags)
                : null;
        }

        /// <summary>The flip (ASMDUDE_SIM_ENGINE=component): populate the editor cache from the dynamic
        /// per-component engine instead of the linear walk. Read paths (hover / inlay hints / CodeLens /
        /// diagnostics) are unchanged — they serve from the same cache. Now INCREMENTAL: each line is
        /// written to the cache the instant it is extracted, with a throttled onProgress, so CodeLens appear
        /// progressively (like the linear engine) instead of after the whole document. Reversible via env var.</summary>
        private void RunComponentSimulation(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted, Action<Uri>? onProgress)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int linesWritten = 0;
            int diagCount = 0;
            long lastProgressMs = -ProgressNotifyThrottleMs;

            // Called per line as the engine resolves it: write that one line to the cache (under lock, vs
            // the read paths) and fire a throttled refresh so the editor draws it immediately.
            void Emit(int line, ComponentLine cl)
            {
                bool wrote = false;
                lock (this.lockObj_)
                {
                    if (!ct.IsCancellationRequested
                        && this.simVersion_.TryGetValue(uri, out long curVer) && curVer == version
                        && this.cache_.TryGetValue(uri, out DocCache? entry))
                    {
                        if (cl.Before != null) entry.lineStringsBefore[line] = cl.Before;
                        if (cl.After != null) entry.lineStringsAfter[line] = cl.After;
                        if (cl.ReadLabel != null) entry.lineStringsReadLabels[line] = cl.ReadLabel;
                        if (cl.WriteLabel != null) entry.lineStringsWriteLabels[line] = cl.WriteLabel;
                        if (cl.Diagnostics.Count > 0)
                        {
                            entry.diagnostics.AddRange(cl.Diagnostics);
                            diagCount += cl.Diagnostics.Count;
                        }
                        linesWritten++;
                        wrote = true;
                    }
                }
                if (wrote && clock.ElapsedMilliseconds - lastProgressMs >= ProgressNotifyThrottleMs)
                {
                    lastProgressMs = clock.ElapsedMilliseconds;
                    onProgress?.Invoke(uri);
                }
            }

            try
            {
                this.ComputeComponentLines(lines, ct, Emit);
            }
            catch (Exception ex)
            {
                Log($"[THREAD] COMPONENT EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            if (ct.IsCancellationRequested) return;

            LogInfo($"[component] SUCCESS: {linesWritten} lines, {diagCount} diagnostics, loop={SimLoopHandling}, total {clock.ElapsedMilliseconds} ms; Z3 ctx live={AsmSim.Z3ContextTracker.Live} peak={AsmSim.Z3ContextTracker.Peak}");
            onProgress?.Invoke(uri); // final flush
            onCompleted?.Invoke(uri);
        }

        /// <summary>Shadow mode: compare the just-completed linear result against the component engine and
        /// log the per-line diff under SIMDIFF. Linear stays authoritative — this only observes.</summary>
        private void RunShadowComparison(Uri uri, IReadOnlyList<string> lines)
        {
            SimResultSet linear = this.ToResultSet(uri, "linear"); // full: before/after + labels + diagnostics
            SimResultSet component = BuildComponentResultSet("component", lines);
            SimDiff diff = SimResultComparer.Compare(linear, component);

            if (diff.IsEmpty)
            {
                SimDiffLog($"{uri}: linear == component ({linear.Lines.Count} lines, {component.Lines.Count} component lines)");
            }
            else
            {
                SimDiffLog($"{uri}: {diff.Entries.Count} diff(s) over {diff.ChangedLines.Count} line(s)" + Environment.NewLine + diff.ToReport());
            }
        }

        /// <summary>Test seam: run BOTH engines on the same program (linear synchronously, then component)
        /// and return the SIMDIFF — so tests can assert agreement (straight-line) or the expected
        /// merge-vs-linear divergence (branches), exercising the real production engines.</summary>
        internal SimDiff CompareEnginesForTest(Uri uri, IReadOnlyList<string> lines)
        {
            this.SimulateSynchronouslyForTest(uri, lines);
            SimResultSet linear = this.ToResultSet(uri, "linear"); // full: before/after + labels + diagnostics
            SimResultSet component = BuildComponentResultSet("component", lines);
            return SimResultComparer.Compare(linear, component);
        }

        private static void SimDiffLog(string msg,
            [System.Runtime.CompilerServices.CallerMemberName] string member = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
            => AsmLog.Log(AsmLogLevel.Warn, "SIMDIFF", msg, member, line);

        /// <summary>
        /// Returns a thread-safe snapshot of the CodeLens label strings for each display position.
        /// Used by the pipe server to serve CodeLens requests.
        ///
        /// Each instruction at line N contributes two display positions:
        ///   • N   — reads of instruction N  (r: prefix, before-state values; shown ABOVE line N)
        ///   • N+1 — writes of instruction N (w: prefix, after-state values;  shown BELOW line N)
        ///
        /// When the write label of N and the read label of N+1 share a register or flag,
        /// that item is merged to <c>rw:</c> instead of appearing twice.
        /// </summary>
        internal Dictionary<int, string> GetSimStatesSummary(Uri uri)
        {
            lock (this.lockObj_)
            {
                if (!this.cache_.TryGetValue(uri, out DocCache? entry))
                    return [];

                // Collect all distinct display positions
                var allPositions = new HashSet<int>();
                foreach (int k in entry.lineStringsReadLabels.Keys) allPositions.Add(k);
                foreach (int k in entry.lineStringsWriteLabels.Keys) allPositions.Add(k + 1);

                var result = new Dictionary<int, string>(allPositions.Count);
                foreach (int pos in allPositions)
                {
                    string? writeCompact = entry.lineStringsWriteLabels.TryGetValue(pos - 1, out string? writeRaw)
                        ? CompactStateString(writeRaw) : null;
                    string? readCompact = entry.lineStringsReadLabels.TryGetValue(pos, out string? readRaw)
                        ? CompactStateString(readRaw) : null;

                    string? merged = MergeCompactLabels(writeCompact, readCompact);
                    if (merged != null) result[pos] = merged;
                }
                return result;
            }
        }

        /// <summary>
        /// Parses a compact state string into individual (prefix, name, value) items.
        /// Registers are separated by <c>, </c>; flags within a register-less line are
        /// space-separated (e.g. <c>w:ZF=1 w:CF=0</c>).
        /// </summary>
        private static List<(string prefix, string name, string value)> ParseCompactItems(string? compact)
        {
            var list = new List<(string, string, string)>();
            if (string.IsNullOrEmpty(compact)) return list;

            foreach (string commaPart in compact.Split(", ", StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (string token in commaPart.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = token.IndexOf(':');
                    int eq = token.IndexOf('=');
                    if (colon < 0 || eq <= colon + 1) continue;
                    string prefix = token[..(colon + 1)];   // "w:", "r:", "rw:"
                    string name = token[(colon + 1)..eq]; // "ZF", "RAX"
                    string value = token[(eq + 1)..];      // "1", "0x10", "?"
                    if (name.Length > 0)
                        list.Add((prefix, name, value));
                }
            }
            return list;
        }

        /// <summary>
        /// Merges write-side and read-side compact label strings.
        /// Items present in both (same name) are promoted to <c>rw:</c>.
        /// Write-only items keep <c>w:</c>; read-only items keep <c>r:</c>.
        /// </summary>
        internal static string? MergeCompactLabels(string? writeCompact, string? readCompact)
        {
            if (string.IsNullOrEmpty(writeCompact) && string.IsNullOrEmpty(readCompact)) return null;
            if (string.IsNullOrEmpty(readCompact)) return writeCompact;
            if (string.IsNullOrEmpty(writeCompact)) return readCompact;

            var writeItems = ParseCompactItems(writeCompact);
            var readItems = ParseCompactItems(readCompact);

            // Build name → item lookup for the read side
            var readByName = new Dictionary<string, (string prefix, string value)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (p, n, v) in readItems) readByName[n] = (p, v);

            var resultItems = new List<string>(writeItems.Count + readItems.Count);

            // Write items: promote to rw: if the same name is also read
            var writtenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (prefix, name, value) in writeItems)
            {
                writtenNames.Add(name);
                string merged = readByName.ContainsKey(name) ? "rw:" : prefix;
                resultItems.Add($"{merged}{name}={value}");
            }

            // Read items not already covered by a write
            foreach (var (prefix, name, value) in readItems)
            {
                if (!writtenNames.Contains(name))
                    resultItems.Add($"{prefix}{name}={value}");
            }

            return resultItems.Count > 0 ? string.Join(", ", resultItems) : null;
        }


        // ── Private helpers ────────────────────────────────────────────────────

        private string? GetCachedString(Uri uri, int lineNumber, bool after)
        {
            lock (this.lockObj_)
            {
                if (this.cache_.TryGetValue(uri, out DocCache? entry))
                {
                    var dict = after ? entry.lineStringsAfter : entry.lineStringsBefore;
                    if (dict.TryGetValue(lineNumber, out string? s))
                        return s;
                }
            }
            return null;
        }

        private static string? ExtractRegisterValue(string? allState, Rn reg)
        {
            if (allState == null) return null;
            Rn reg64 = RegisterTools.Get64BitsRegister(reg);
            string prefix = reg64.ToString() + " = ";
            int idx = allState.IndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) return null;
            int valueStart = idx + prefix.Length;
            int valueEnd = allState.IndexOf('\n', valueStart);
            return valueEnd < 0 ? allState[valueStart..] : allState[valueStart..valueEnd];
        }

        /// <summary>
        /// Builds a combined register+flag state string for <paramref name="state"/>.
        /// Only registers with non-UNKNOWN values and flags with concrete 0/1 values are included.
        /// Returns null when nothing is known.
        /// </summary>
        private static string? ComputeStateString(AsmSimState state)
        {
            string regs = state.ToStringRegs(string.Empty);

            var sb = new StringBuilder();
            StateConfig cfg = state.Tools.StateConfig;
            foreach (Flags flag in new[] { Flags.CF, Flags.ZF, Flags.SF, Flags.OF })
            {
                if (!cfg.IsFlagOn(flag)) continue;
                Tv tv = state.GetTv(flag);
                if (tv == Tv.ONE || tv == Tv.ZERO)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(flag.ToString()).Append('=').Append(tv == Tv.ONE ? '1' : '0');
                }
            }
            string flags = sb.Length > 0 ? "\n" + sb : string.Empty;

            string combined = regs + flags;
            return combined.Length > 0 ? combined : null;
        }

        /// <summary>
        /// Builds the read-side CodeLens label for instruction N: registers and flags that N reads,
        /// with <c>r:</c> prefix, values taken from the before-state (what was available when N ran).
        /// Shown ABOVE instruction N.
        /// </summary>
        private static string? ComputeReadLabel(
            AsmSimState beforeState,
            IEnumerable<Rn> readRegs,
            Flags readFlags)
        {
            StateConfig cfg = beforeState.Tools.StateConfig;
            var sb = new StringBuilder();

            static void AppendReg(StringBuilder sb, AsmSimState state, Rn reg64)
            {
                Tv[] content = state.GetTvArray(reg64);
                (bool hasOne, Tv tv) = ToolsZ3.HasOneValue(content);
                if (hasOne && tv is not Tv.ONE and not Tv.ZERO)
                    sb.Append($"\nr:{reg64} = {ToolsZ3.ToStringBin(tv)}");
                else
                    sb.Append($"\nr:{reg64} = {ToolsZ3.ToStringBin(content)} = {ToolsZ3.ToStringHex(content)}");
            }

            var seen = new HashSet<Rn>();
            foreach (Rn reg in readRegs)
            {
                Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                if (cfg.IsRegOn(reg64) && seen.Add(reg64))
                    AppendReg(sb, beforeState, reg64);
            }

            var flagSb = new StringBuilder();
            foreach (Flags flag in FlagTools.GetFlags(readFlags))
            {
                if (!cfg.IsFlagOn(flag)) continue;
                Tv tv = beforeState.GetTv(flag);
                if (flagSb.Length > 0) flagSb.Append(' ');
                flagSb.Append("r:").Append(flag).Append('=')
                      .Append(ToolsZ3.ToStringBin(tv));
            }
            if (flagSb.Length > 0)
                sb.Append('\n').Append(flagSb);

            string result = sb.ToString();
            return result.Length > 0 ? result : null;
        }

        /// <summary>
        /// Builds the write-side CodeLens label for instruction N: registers and flags that N writes,
        /// with <c>w:</c> prefix, values taken from the after-state (what N produced).
        /// Shown BELOW instruction N (i.e., as the CodeLens above line N+1).
        /// </summary>
        private static string? ComputeWriteLabel(
            AsmSimState afterState,
            IEnumerable<Rn> writtenRegs,
            Flags writtenFlags)
        {
            StateConfig cfg = afterState.Tools.StateConfig;
            var sb = new StringBuilder();

            static void AppendReg(StringBuilder sb, AsmSimState state, Rn reg64)
            {
                Tv[] content = state.GetTvArray(reg64);
                (bool hasOne, Tv tv) = ToolsZ3.HasOneValue(content);
                if (hasOne && tv is not Tv.ONE and not Tv.ZERO)
                    sb.Append($"\nw:{reg64} = {ToolsZ3.ToStringBin(tv)}");
                else
                    sb.Append($"\nw:{reg64} = {ToolsZ3.ToStringBin(content)} = {ToolsZ3.ToStringHex(content)}");
            }

            var seen = new HashSet<Rn>();
            foreach (Rn reg in writtenRegs)
            {
                Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                if (cfg.IsRegOn(reg64) && seen.Add(reg64))
                    AppendReg(sb, afterState, reg64);
            }

            var flagSb = new StringBuilder();
            foreach (Flags flag in FlagTools.GetFlags(writtenFlags))
            {
                if (!cfg.IsFlagOn(flag)) continue;
                Tv tv = afterState.GetTv(flag);
                if (flagSb.Length > 0) flagSb.Append(' ');
                flagSb.Append("w:").Append(flag).Append('=')
                      .Append(ToolsZ3.ToStringBin(tv));
            }
            if (flagSb.Length > 0)
                sb.Append('\n').Append(flagSb);

            string result = sb.ToString();
            return result.Length > 0 ? result : null;
        }

        // ── Background simulation ──────────────────────────────────────────────

        /// <summary>
        /// Observability (gated, side-effect-free): logs how the document decomposes into weakly-connected
        /// CFG components and each component's forward-seed entry lines. This is the partition the planned
        /// per-component engine (INCREMENTAL_SIM_PLAN.md Phase 2) will simulate; logging it now lets us SEE
        /// the decomposition on real files and validates <see cref="StaticFlow.ComputeComponentEntryLines"/>
        /// on live editor input — with ZERO effect on the actual (linear) simulation. Only runs when Debug
        /// logging is enabled (building a <see cref="StaticFlow"/> instantiates opcodes), and never throws
        /// into the simulation path.
        /// </summary>
        private static void LogCfgPartition(Uri uri, IReadOnlyList<string> lines)
        {
            if (!AsmLog.IsEnabled(AsmLogLevel.Debug)) return;
            try
            {
                var sFlow = new StaticFlow(new AsmSimTools());
                // StaticFlow.Update splits the program on Environment.NewLine (see Test_StaticFlow), so
                // join with that, NOT "\n" — otherwise the whole document parses as a single line.
                sFlow.Update(string.Join(Environment.NewLine, lines), removeEmptyLines: false);

                IReadOnlyDictionary<int, int> lineToComponent = sFlow.ComputeLineToComponent();
                IReadOnlyDictionary<int, List<int>> entries = sFlow.ComputeComponentEntryLines();

                int componentCount = new HashSet<int>(lineToComponent.Values).Count;
                var componentIds = new List<int>(entries.Keys);
                componentIds.Sort();

                var sb = new StringBuilder();
                sb.Append($"[CFG] {uri}: {sFlow.NLines} lines -> {componentCount} component(s); entries:");
                foreach (int id in componentIds)
                    sb.Append($" c{id}=[{string.Join(",", entries[id])}]");
                Log(sb.ToString());
            }
            catch (Exception ex)
            {
                Log($"[CFG] partition logging failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void RunSimulation(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted, Action<Uri>? onProgress)
        {
            LogInfo($"sim started: engine={this.engineMode_} loop={SimLoopHandling}, {lines.Count} lines, {uri.Segments[^1]}");
            LogCfgPartition(uri, lines);

            // The merge engine is the editor default; drive the editor from the dynamic per-component
            // engine instead of the linear walk below. Read paths are unchanged (same cache).
            if (this.engineMode_ == SimEngineMode.Component)
            {
                this.RunComponentSimulation(uri, version, lines, ct, onCompleted, onProgress);
                return;
            }

            // Declared OUTSIDE the try so the finally can dispose them on EVERY exit path (see finally).
            var ownedStates = new List<AsmSimState>();
            try
            {
                var settings = new Dictionary<string, string>
                {
                    { "timeout", "5000" },
                    // Pin Z3's internal RNG so a run is reproducible (aids debugging + the headless
                    // characterization tests). NOTE: the 5 s timeout above is still wall-clock-dependent,
                    // so determinism only fully holds for programs that never hit it.
                    { "random_seed", "0" },
                };
                AsmSimTools tools = new(settings);
                EnableFullStateConfig(tools); // shared with the component engine so SIMDIFF is apples-to-apples
                tools.Quiet = true;

                var newDiagnostics = new List<SimDiagnostic>();
                int linesWritten = 0;
                int slowLineCount = 0; // lines whose Z3 work hit (≈) the per-line timeout — see run summary

                // Coalesce per-line progress notifications (see ProgressNotifyThrottleMs). The
                // negative seed lets the first simulated line notify immediately; the rest are
                // throttled. The unconditional final flush after the loop publishes the last state.
                var progressClock = System.Diagnostics.Stopwatch.StartNew();
                long lastProgressNotifyMs = -ProgressNotifyThrottleMs;

                // Memoize ComputeStateString per state instance: multiple lines sharing the same
                // state (blank/comment) reuse the pre-computed string without re-running Z3.
                var stateToString = new Dictionary<AsmSimState, string?>(ReferenceEqualityComparer.Instance);

                string initialKey = "!0";
                AsmSimState state = new(tools, initialKey, initialKey);
                ownedStates.Add(state);


                int limit = Math.Min(lines.Count, MaxLines);


                for (int i = 0; i < limit; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        Log($"[THREAD] Cancellation requested at line {i}");
                        return; // finally disposes ownedStates
                    }

                    string line = lines[i].Trim();

                    // Blank/comment: skip (no Z3 work, state unchanged).
                    if (line.Length == 0 || line.StartsWith(';'))
                    {
                        Log($"[THREAD] Line {i}: blank/comment, skipping");
                        continue;
                    }

                    // #pragma assume <instruction>  — apply to simulator state without advancing line counter
                    // #pragma assume HLT            — reset state to fresh initial (unknown everything)
                    if (line.StartsWith('#'))
                    {
                        const string pragmaPrefix = "#pragma assume ";
                        if (line.StartsWith(pragmaPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string assumeInstruction = line[pragmaPrefix.Length..].Trim();
                            // Strip inline comment
                            int commentIdx = assumeInstruction.IndexOf(';');
                            if (commentIdx >= 0) assumeInstruction = assumeInstruction[..commentIdx].Trim();

                            if (assumeInstruction.Equals("HLT", StringComparison.OrdinalIgnoreCase))
                            {
                                // Reset to a fresh unknown state
                                state = new AsmSimState(tools, initialKey, initialKey);
                                ownedStates.Add(state);
                                stateToString.Remove(state);
                                Log($"[THREAD] Line {i}: #pragma assume HLT — state reset");
                            }
                            else
                            {
                                try
                                {
                                    AsmSimState? next = Runner.SimpleStep_Forward(assumeInstruction, state);
                                    if (next != null && !object.ReferenceEquals(next, state))
                                    {
                                        state = next;
                                        ownedStates.Add(state);
                                        stateToString.Remove(state);
                                    }
                                    Log($"[THREAD] Line {i}: #pragma assume '{assumeInstruction}' applied");
                                }
                                catch (Exception ex)
                                {
                                    AsmLog.Debug("ASMSIM", $"#pragma assume failed line {i}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Log($"[THREAD] Line {i}: directive, skipping");
                        }
                        continue;
                    }

                    // Parse to check if this is a real instruction the simulator can handle.
                    (AsmTools.KeywordID[] _, string _label, Mnemonic mnemonic, string[] args, string _remark)
                        = AsmTools.AsmSourceTools.ParseLine(line, -1, -1, AssemblerEnum.UNKNOWN);

                    // Skip lines that aren't real instructions: labels, directives, includes, etc.
                    if (mnemonic == Mnemonic.NONE)
                    {
                        if (!string.IsNullOrEmpty(_label))
                        {
                            // Labels are join points: any number of jump edges may arrive here.
                            // SimpleStep_Forward is single-path, so if the fall-through state is
                            // ZERO-consistent (unreachable on this path) reset to a fresh unknown
                            // state so instructions after the label are not marked unreachable.
                            if (state.IsConsistent == Tv.ZERO)
                            {
                                Log($"[THREAD] Line {i}: label '{_label}' — resetting ZERO-consistent state (branch target, single-path sim)");
                                state = new AsmSimState(tools, initialKey, initialKey);
                                ownedStates.Add(state);
                                stateToString.Remove(state);
                            }
                        }
                        Log($"[THREAD] Line {i}: not an instruction ('{line.Substring(0, Math.Min(30, line.Length))}'), skipping");
                        continue;
                    }

                    Log($"[THREAD] Line {i} (editor line {i + 1}): processing '{mnemonic} {string.Join(", ", args)}'...");

                    // Time the Z3-heavy work for this instruction (before/after state strings,
                    // opcode instantiation, the SimpleStep_Forward solve) so slow lines are greppable.
                    var lineClock = System.Diagnostics.Stopwatch.StartNew();

                    // ── Before-state: state at entry to this instruction ──────────
                    if (!stateToString.TryGetValue(state, out string? beforeStr))
                    {
                        beforeStr = ComputeStateString(state);
                        stateToString[state] = beforeStr;
                    }

                    // ── Instantiate opcode once — used for diagnostics and read/write filter ──
                    var dummyKeys = ("dummy_prev", "dummy_next", "dummy_branch");
                    using OpcodeBase? opcodeBase = Runner.InstantiateOpcode(mnemonic, args, dummyKeys, tools);

                    // ── Read/write sets of this instruction ───────────────────────
                    var writtenRegs = new HashSet<Rn>();
                    Flags writtenFlags = Flags.NONE;
                    var readRegsOfThis = new HashSet<Rn>();
                    Flags readFlagsOfThis = Flags.NONE;
                    if (opcodeBase != null)
                    {
                        foreach (Rn r in opcodeBase.RegsWriteStatic) writtenRegs.Add(RegisterTools.Get64BitsRegister(r));
                        writtenFlags = opcodeBase.FlagsWriteStatic;
                        foreach (Rn r in opcodeBase.RegsReadStatic) readRegsOfThis.Add(RegisterTools.Get64BitsRegister(r));
                        readFlagsOfThis = opcodeBase.FlagsReadStatic;
                    }


                    // ── Diagnostics ───────────────────────────────────────────────
                    var lineDiags = new List<SimDiagnostic>();
                    this.CollectDiagnostics(line, i, state, tools, lineDiags, opcodeBase);
                    newDiagnostics.AddRange(lineDiags);

                    // Save before-state reference for read-only register queries below.
                    AsmSimState stateBeforeStep = state;

                    // ── Advance state ─────────────────────────────────────────────
                    try
                    {
                        AsmSimState? next = Runner.SimpleStep_Forward(line, state);
                        if (next != null && !object.ReferenceEquals(next, state))
                        {
                            state = next;
                            ownedStates.Add(state);
                            stateToString.Remove(state); // force recompute for new state
                        }
                    }
                    catch (Exception ex)
                    {
                        AsmLog.Debug("ASMSIM", $"line {i}: {ex.Message}");
                    }

                    // ── After-state (full — for hover) ────────────────────────────
                    if (!stateToString.TryGetValue(state, out string? afterStr))
                    {
                        afterStr = ComputeStateString(state);
                        stateToString[state] = afterStr;
                    }

                    // ── Per-instruction CodeLens labels ──────────────────────────
                    // Reads of instruction i → shown ABOVE line i (r: prefix, before-state values)
                    // Writes of instruction i → shown BELOW line i (w: prefix, after-state values)
                    //   stored at key i; GetSimStatesSummary places them at display position i+1.
                    string? readLabel = ComputeReadLabel(stateBeforeStep, readRegsOfThis, readFlagsOfThis);
                    string? writeLabel = ComputeWriteLabel(state, writtenRegs, writtenFlags);

                    // ── Write to cache incrementally so hover and diagnostics are visible immediately ──
                    bool hadNewDiag = false;
                    bool shouldRefresh = false;
                    lock (this.lockObj_)
                    {
                        if (!ct.IsCancellationRequested
                            && this.simVersion_.TryGetValue(uri, out long curVer) && curVer == version
                            && this.cache_.TryGetValue(uri, out DocCache? entry))
                        {
                            if (beforeStr != null) entry.lineStringsBefore[i] = beforeStr;
                            if (afterStr != null) entry.lineStringsAfter[i] = afterStr;
                            if (readLabel != null) entry.lineStringsReadLabels[i] = readLabel;
                            if (writeLabel != null) entry.lineStringsWriteLabels[i] = writeLabel;
                            if (lineDiags.Count > 0)
                            {
                                entry.diagnostics.AddRange(lineDiags);
                                hadNewDiag = true;
                            }
                            linesWritten++;
                            shouldRefresh = true;
                        }
                    }
                    // Publish diagnostics immediately so squiggles appear without waiting for full simulation.
                    if (hadNewDiag) onCompleted?.Invoke(uri);
                    // Notify the client to re-request inlay hints for the visible range. Throttled
                    // so a long sim pass doesn't re-tag the whole document once per line.
                    if (shouldRefresh && progressClock.ElapsedMilliseconds - lastProgressNotifyMs >= ProgressNotifyThrottleMs)
                    {
                        lastProgressNotifyMs = progressClock.ElapsedMilliseconds;
                        onProgress?.Invoke(uri);
                    }

                    lineClock.Stop();
                    if (lineClock.ElapsedMilliseconds >= SlowLineThresholdMs) slowLineCount++;
                    AsmLog.Info("ASMSIM", $"[linear] line {i + 1}: {mnemonic} done in {lineClock.ElapsedMilliseconds} ms");
                }

                // Finalize. The per-line state STRINGS were already written to the cache incrementally;
                // the read paths (GetCachedString / GetSimStatesSummary) use ONLY those strings and never
                // touch the live AsmSimState objects again. Each AsmSimState owns a heavy Z3 native Context,
                // so retaining one per line would keep the whole document's worth of Z3 contexts alive —
                // GiBs of *native* memory the GC can't see (observed ~18 GiB for a ~140-line file, idle
                // overnight). The states are disposed in the finally below on ALL exit paths; the cached
                // strings remain valid (plain managed strings, independent of Z3).
                bool stillCurrent;
                lock (this.lockObj_)
                {
                    stillCurrent = !ct.IsCancellationRequested
                        && this.simVersion_.TryGetValue(uri, out long curVer) && curVer == version;
                }
                if (!stillCurrent)
                {
                    return; // finally disposes ownedStates
                }

                LogInfo($"[linear] SUCCESS: {linesWritten} lines written, {newDiagnostics.Count} diagnostics, {slowLineCount} slow/timeout line(s) (>={SlowLineThresholdMs}ms), total {progressClock.ElapsedMilliseconds} ms; Z3 ctx live={AsmSim.Z3ContextTracker.Live} peak={AsmSim.Z3ContextTracker.Peak}");
                // Notify client to refresh inlay hints (final flush) and republish diagnostics.
                onProgress?.Invoke(uri);
                onCompleted?.Invoke(uri);

                // Shadow mode: run the per-component engine compute-only and log per-line diffs vs the
                // linear result. Never affects the editor; wrapped so it can't wedge the sim.
                if (this.engineMode_ == SimEngineMode.Shadow)
                {
                    try
                    {
                        this.RunShadowComparison(uri, lines);
                    }
                    catch (Exception ex)
                    {
                        Log($"[SHADOW] comparison failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected — a newer simulation replaced this one.
            }
            catch (Exception ex)
            {
                Log($"[THREAD] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                AsmLog.Warn("ASMSIM", $"simulation failed for {uri}: {ex.Message}");
            }
            finally
            {
                // Dispose EVERY Z3 state on EVERY exit path: success, in-loop cancellation, OR an
                // exception. Several Z3 calls run OUTSIDE the per-line try (ComputeStateString,
                // InstantiateOpcode, ComputeReadLabel/ComputeWriteLabel); if any of them throws, the
                // outer catch would otherwise return without freeing the document's worth of native Z3
                // contexts — and because the document is re-simulated on every edit, each failed run
                // leaked another full set, accumulating to GiBs that persist while the editor sits idle.
                // The cached strings are independent of these states, so disposing here never invalidates
                // a hover/inlay/CodeLens result.
                lock (this.lockObj_)
                {
                    DisposeList(ownedStates);
                }
            }
        }

        private void CollectDiagnostics(string line, int lineIndex, AsmSimState beforeState, AsmSimTools tools, List<SimDiagnostic> diagnostics, OpcodeBase? opcodeBase)
        {
            try
            {
                (AsmTools.KeywordID[] _, string _label, Mnemonic mnemonic, string[] args, string _remark)
                                     = AsmTools.AsmSourceTools.ParseLine(line, -1, -1, AssemblerEnum.UNKNOWN);

                if (mnemonic == Mnemonic.NONE)
                    return;

                if (opcodeBase == null)
                    return;

                Type opcodeType = opcodeBase.GetType();

                // Not implemented (mock SIMD or truly not implemented)
                if (opcodeType == typeof(NotImplemented) || opcodeType == typeof(DummySIMD))
                {
                    diagnostics.Add(new SimDiagnostic(lineIndex,
                        $"\"{mnemonic}\" is not (fully) implemented in the simulator.",
                        SimDiagnosticKind.NotImplemented));
                    return;
                }

                // Syntax error
                if (opcodeBase.IsHalted)
                {
                    string msg = opcodeBase.SyntaxError ?? $"Syntax error in \"{line.Trim()}\"";
                    diagnostics.Add(new SimDiagnostic(lineIndex, msg, SimDiagnosticKind.SyntaxError));
                    return; // no further checks on a halted instruction
                }

                // Unreachable: before-state is inconsistent (UNSAT)
                try
                {
                    Tv consistency = beforeState.IsConsistent;
                    Log($"[DIAG] Line {lineIndex}: IsConsistent={consistency}");
                    if (consistency == Tv.ZERO)
                    {
                        Log($"[DIAG] Line {lineIndex}: UNREACHABLE detected");
                        diagnostics.Add(new SimDiagnostic(lineIndex,
                            $"\"{line.Trim()}\" is unreachable.",
                            SimDiagnosticKind.Unreachable));
                        return; // no point checking usage-undefined on unreachable code
                    }
                }
                catch (Exception ex)
                {
                    AsmLog.Debug("ASMSIM", $"IsConsistent failed line {lineIndex}: {ex.Message}");
                }

                // Usage of undefined: check flags and registers that this opcode reads
                try
                {
                    var undefinedItems = new StringBuilder();
                    StateConfig cfg = tools.StateConfig;

                    foreach (Flags flag in FlagTools.GetFlags(opcodeBase.FlagsReadStatic))
                    {
                        if (cfg.IsFlagOn(flag) && beforeState.Is_Undefined(flag))
                        {
                            undefinedItems.Append(flag).Append(" is undefined; ");
                        }
                    }

                    foreach (Rn reg in opcodeBase.RegsReadStatic)
                    {
                        Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                        if (cfg.IsRegOn(reg64) && beforeState.Is_Undefined(reg))
                        {
                            undefinedItems.Append(reg).Append(" has undefined content; ");
                        }
                    }

                    if (undefinedItems.Length > 0)
                    {
                        diagnostics.Add(new SimDiagnostic(lineIndex,
                            $"Usage of undefined value in \"{mnemonic}\": {undefinedItems}",
                            SimDiagnosticKind.UsageUndefined));
                    }
                }
                catch (Exception ex)
                {
                    AsmLog.Debug("ASMSIM", $"usage-undefined check failed line {lineIndex}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                AsmLog.Debug("ASMSIM", $"diagnostics collection failed line {lineIndex}: {ex.Message}");
            }
        }

        private static void DisposeList(List<AsmSimState> states)
        {
            // Dispose in REVERSE creation order. Under the shared-context model a state created by
            // the copy constructor borrows the context of the state it was copied from; the original
            // (lower index) owns the context and must be disposed LAST, after every state that shares
            // it has released its solvers. Disposing forward would free the context out from under
            // later states → use-after-free / AV.
            for (int i = states.Count - 1; i >= 0; i--)
            {
                states[i].Dispose();
            }
        }

        public void Dispose()
        {
            lock (this.lockObj_)
            {
                foreach (CancellationTokenSource cts in this.pendingTasks_.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                this.pendingTasks_.Clear();
                this.simVersion_.Clear();

                // Each in-flight RunSimulation disposes its own Z3 states in its finally once its token is
                // cancelled (above); the cache holds only strings, so just drop it.
                this.cache_.Clear();
            }
        }
    }
}
