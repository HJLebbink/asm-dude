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

namespace AsmSim.Host
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
        Redundant,
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
    internal sealed class AsmSimulator : IDisposable
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

        /// <summary>The instruction sequence of the PREVIOUS simulation of each document, retained so an
        /// edit can be diffed against it (<see cref="AsmSim.InstructionDiff"/>) to compute the dataflow cone
        /// — the foundation of incremental simulation (INCREMENTAL_SIM_PLAN.md M0). Populated only when
        /// <see cref="SimIncremental"/> is on; guarded by <see cref="lockObj_"/>.</summary>
        private readonly Dictionary<Uri, IReadOnlyList<AsmSim.Instruction>> prevInstr_ = [];

        /// <summary>The (instructions, result-cache) of the last COMPLETED simulation of each document — the
        /// reusable unit for incremental simulation (INCREMENTAL_SIM_PLAN.md M1). Unlike
        /// <see cref="prevInstr_"/> (updated eagerly for the diff log), this pairs the instructions with the
        /// DocCache they produced, and is updated only when a run finishes, so the two are always coherent.
        /// The cache is strings-only ⇒ retaining it is cheap and Z3-independent. <c>Lines</c> are the old
        /// source lines, kept so the OLD CFG can be rebuilt for the topology edge-diff (M2 topology relax).
        /// Guarded by <see cref="lockObj_"/>.</summary>
        private readonly Dictionary<Uri, (IReadOnlyList<AsmSim.Instruction> Instr, DocCache Cache, IReadOnlyList<string> Lines)> committed_ = [];

        internal AsmSimulator()
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
                // RETAIN the previous run's display strings (only create an empty slot on first sim). Reads
                // (CodeLens) keep showing the last-known values until the new run OVERWRITES them in place —
                // line-by-line via Emit, or wholesale via the cone path's remapped reuse-base. Blanking the
                // cache here instead makes every keystroke collapse all CodeLens to nothing for ~1 s (the lens
                // row's vertical space is reclaimed, so the code below jumps up, then back down when the new
                // values arrive). The cache holds only strings; the previous run's Z3 states are owned and
                // disposed by that run's own RunSimulation finally (the bumped version + cancelled token make it
                // stop writing and unwind), so retaining the entry leaks nothing. Note: on a non-incremental
                // FULL re-sim a line that changed from instruction→comment keeps a stale lens until the run
                // finishes; the incremental cone path (the editor default) replaces the whole entry with the
                // remapped reuse-base, so it has no such orphan.
                if (!this.cache_.ContainsKey(uri))
                {
                    this.cache_[uri] = new DocCache();
                }
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
        /// headless <c>AsmSimulatorTests</c> (the golden baseline for the planned engine swap), which
        /// avoids the flaky sleep-and-poll the older <c>AsmSimTests</c> uses.
        /// </summary>
        internal void SimulateSynchronouslyForTest(Uri uri, IReadOnlyList<string> lines, SimEngineMode engine = SimEngineMode.Linear, bool computeFullState = true)
        {
            // Tests pin the engine explicitly (default Linear, the golden-baseline engine) so they are
            // independent of the production default and of the ASMDUDE_SIM_ENGINE env var.
            this.engineMode_ = engine;
            // This seam is the golden FULL-sim baseline: pin incremental OFF so a dev machine that happens to
            // have ASMDUDE_SIM_INCREMENTAL set can't make a re-sim silently take the reuse fast path. The
            // Tier-0/cone paths are exercised by their own seams (SimulateTier0ForTest/SimulateConeForTest).
            this.incremental_ = false;
            // Most tests assert on the full before/after dumps (golden register values, getProvenStates), so
            // default to computing them even though the editor skips them. A parity test that compares against
            // the editor (out-of-process) path passes false to match. See computeFullState_.
            this.computeFullState_ = computeFullState;
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

        /// <summary>Test seam — run the REAL incremental decision path synchronously (incremental ON), so a
        /// soak test drives the production <see cref="RunSimulation"/> dispatch (Tier-0 → cone → full fallback)
        /// exactly as the editor would. Requires a committed baseline from a prior run; commits a new baseline
        /// for the next edit. Defaults to the editor's config (Component engine, labels-only).</summary>
        internal void SimulateIncrementalForTest(Uri uri, IReadOnlyList<string> lines, SimEngineMode engine = SimEngineMode.Component, bool computeFullState = false)
        {
            this.engineMode_ = engine;
            this.computeFullState_ = computeFullState;
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
            this.incremental_ = true; // exercise the real reuse dispatch
            this.RunSimulation(uri, version, lines, CancellationToken.None, onCompleted: null, onProgress: null);
        }

        /// <summary>Test seam — run the real dispatch synchronously but DO NOT touch <see cref="incremental_"/>,
        /// so a test can verify that <see cref="ApplySettings"/> (i.e. the settings) actually controls reuse:
        /// enable via ApplySettings, then a newline edit should reuse; disable, and it should recompute.</summary>
        internal void SimulateRespectingFlagForTest(Uri uri, IReadOnlyList<string> lines, SimEngineMode engine = SimEngineMode.Component, bool computeFullState = false)
        {
            this.engineMode_ = engine;
            this.computeFullState_ = computeFullState;
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

        /// <summary>Test hook: the current value of the runtime incremental switch (set by <see cref="ApplySettings"/>).</summary>
        internal bool IncrementalEnabledForTest => this.incremental_;

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
                this.prevInstr_.Remove(uri);
                this.committed_.Remove(uri);
            }
        }

        /// <summary>
        /// MIRROR write path (out-of-process mode): replace this document's cache with a snapshot streamed
        /// from the AsmSim server (<see cref="AsmSimLineResultsParams"/>). This simulator does NOT run Z3
        /// here — it is just the read cache the LSP server queries; the server process did the solving and
        /// pushes results. The snapshot is authoritative (the server sends the full per-line set each
        /// throttle), so the entry is rebuilt; a stale (older-version) snapshot is dropped.
        /// </summary>
        internal void ApplyLineResults(Uri uri, long version, IReadOnlyList<AsmSimLineDto> lines)
        {
            var entry = new DocCache();
            foreach (AsmSimLineDto dto in lines)
            {
                if (dto.Before != null) entry.lineStringsBefore[dto.Line] = dto.Before;
                if (dto.After != null) entry.lineStringsAfter[dto.Line] = dto.After;
                if (dto.ReadLabel != null) entry.lineStringsReadLabels[dto.Line] = dto.ReadLabel;
                if (dto.WriteLabel != null) entry.lineStringsWriteLabels[dto.Line] = dto.WriteLabel;
                if (dto.Diagnostics != null)
                {
                    foreach (string d in dto.Diagnostics)
                    {
                        // ToResultSet flattened each diagnostic to "Kind:Message"; parse it back.
                        int c = d.IndexOf(':');
                        string kindStr = c > 0 ? d[..c] : string.Empty;
                        string msg = c >= 0 ? d[(c + 1)..] : d;
                        SimDiagnosticKind kind = Enum.TryParse(kindStr, out SimDiagnosticKind k) ? k : SimDiagnosticKind.NotImplemented;
                        entry.diagnostics.Add(new SimDiagnostic(dto.Line, msg, kind));
                    }
                }
            }

            lock (this.lockObj_)
            {
                if (this.simVersion_.TryGetValue(uri, out long cur) && cur > version)
                {
                    return; // a newer mirror snapshot already applied
                }
                this.simVersion_[uri] = version;
                this.cache_[uri] = entry;
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
            /// <summary>The linear single-step sim. Single-path (does NOT follow jump targets, resets at
            /// labels) so join-point values are imprecise (read as unknown). Incremental. Kept for tests
            /// and as a rollback (<c>ASMDUDE_SIM_ENGINE=linear</c>).</summary>
            Linear,

            /// <summary>Editor uses the linear sim; the component engine runs compute-only and SIMDIFF-logs.</summary>
            Shadow,

            /// <summary>The DYNAMIC per-component (DynamicFlow) engine — the EDITOR DEFAULT. More precise at
            /// join points (follows jumps, merges branch states). Now INCREMENTAL (streams each line as it
            /// resolves) AND parallel (Phase P: per-line value-solve on a bounded worker pool, each on its
            /// own cloned Z3 context). Roll back with <c>ASMDUDE_SIM_ENGINE=linear</c>; tune workers with
            /// <c>ASMDUDE_SIM_PARALLEL</c>. See INCREMENTAL_SIM_PLAN.md "Phase P".</summary>
            Component,
        }

        /// <summary>The configured engine from the environment; the editor default is <see cref="SimEngineMode.Component"/>.</summary>
        private static readonly SimEngineMode SimEngine = ParseSimEngine();

        /// <summary>Per-instance engine (defaults to <see cref="SimEngine"/>). The test seam pins this to
        /// <see cref="SimEngineMode.Linear"/> so the linear golden-baseline tests still exercise linear.</summary>
        private SimEngineMode engineMode_ = SimEngine;

        /// <summary>When false (the editor default) the per-line FULL register/flag dump — the all-registers
        /// <see cref="ComputeStateString"/> before/after strings (≈16 register-solves each, ×2 states) — is
        /// SKIPPED. The editor only needs the read/write CodeLens labels (the 1-3 registers the instruction
        /// actually touches, via <see cref="ComputeReadLabel"/>/<see cref="ComputeWriteLabel"/>), so the dump
        /// is pure waste there: it only ever fed the now-removed hover before/after view and the test-only
        /// <c>asm/getProvenStates</c>. The test seam (<see cref="SimulateSynchronouslyForTest"/>) sets this
        /// true so getProvenStates and the linear-vs-component shadow parity still get the full dumps.</summary>
        private bool computeFullState_;

        /// <summary>Per-instance loop-handling strategy the component engine uses (defaults to the
        /// <see cref="SimLoopHandling"/> env value; overridable at runtime via <see cref="ApplySettings"/>).</summary>
        private AsmSim.LoopHandling loopHandling_ = SimLoopHandling;

        private static SimEngineMode ParseSimEngine() => ParseEngine(Environment.GetEnvironmentVariable("ASMDUDE_SIM_ENGINE"));

        /// <summary>Map an engine name to a mode; the editor DEFAULT (unrecognized/null) is Component — it
        /// reads DynamicFlow's symbolic ITE-merged states (branch-aware, ~on par with linear) and is strictly
        /// more correct than linear, which executes jumped-over code and resets at labels. Force the old
        /// single-path engine with <c>linear</c>.</summary>
        private static SimEngineMode ParseEngine(string? v)
        {
            if (string.Equals(v, "linear", StringComparison.OrdinalIgnoreCase)) return SimEngineMode.Linear;
            if (string.Equals(v, "shadow", StringComparison.OrdinalIgnoreCase)) return SimEngineMode.Shadow;
            return SimEngineMode.Component;
        }

        /// <summary>Loop-handling strategy the component engine uses (ASMDUDE_SIM_LOOP env var:
        /// accept|modsethavoc|peelonce|fullunroll|fixpoint). Default Accept (legacy loop behavior).</summary>
        private static readonly AsmSim.LoopHandling SimLoopHandling =
            Enum.TryParse(Environment.GetEnvironmentVariable("ASMDUDE_SIM_LOOP"), ignoreCase: true, out AsmSim.LoopHandling lh)
                ? lh
                : AsmSim.LoopHandling.Accept;

        /// <summary>Apply engine/loop settings to this instance at RUNTIME (the out-of-process server's
        /// <c>settingsChanged</c> path — see <c>AsmSimRpcServer.SettingsChanged</c>). Takes effect on the
        /// next simulation; the caller re-sends open documents to pick it up. Parallelism is structural
        /// (pool size fixed at construction) and intentionally NOT changed here.</summary>
        public void ApplySettings(string? engine, string? loop, bool incremental, bool showRedundant = false)
        {
            this.engineMode_ = ParseEngine(engine);
            if (Enum.TryParse(loop, ignoreCase: true, out AsmSim.LoopHandling lh2))
            {
                this.loopHandling_ = lh2;
            }
            this.incremental_ = incremental;
            this.showRedundant_ = showRedundant;
            AsmLog.Info("ASMSIM", $"ApplySettings: engine={this.engineMode_}, loop={this.loopHandling_}, incremental={this.incremental_}, showRedundant={this.showRedundant_}");
        }

        /// <summary>When true, the engines run the (Z3-costly) redundant-instruction check per line — the
        /// faithful port of AsmDude1's <c>Calculate_Redundant_Instruction_Warnings</c>. Gated by the
        /// production setting <c>AsmSim_Show_Redundant_Instructions || AsmSim_Decorate_Redundant_Instructions</c>
        /// (see <c>BuildSimSettings</c>), threaded in via <see cref="ApplySettings"/>. Default off so the extra
        /// solves are never paid unless a redundant surface is enabled. See REDUNDANT_DIAGNOSTICS_PLAN.md.</summary>
        private bool showRedundant_;

        /// <summary>Max concurrent per-line value-extractions the component engine runs (ASMDUDE_SIM_PARALLEL
        /// env var). Extraction (solve the displayed read/write registers of each line — see
        /// <see cref="ExtractComponentLine"/>) is independent per line; each worker solves on its OWN cloned
        /// Z3 context (Z3 contexts are not thread-safe, so a shared one cannot be used concurrently — see
        /// INCREMENTAL_SIM_PLAN.md "Phase P"). Default ≈ processor count (capped). 1 = effectively sequential.</summary>
        private static readonly int SimParallelism = ParseParallelism();

        private static int ParseParallelism()
        {
            string? v = Environment.GetEnvironmentVariable("ASMDUDE_SIM_PARALLEL");
            if (int.TryParse(v, out int n) && n >= 1) return Math.Min(n, 64);
            return Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
        }

        /// <summary>Back-compat env seed for <see cref="incremental_"/> (<c>ASMDUDE_SIM_INCREMENTAL</c>). Only
        /// the PRE-settings value; the actual control is the <c>AsmSim_Incremental</c> setting (settings.json /
        /// VS settings UI, **default ON**), applied at runtime via <see cref="ApplySettings"/> right after the
        /// server initializes — no env var or restart needed. See INCREMENTAL_SIM_PLAN.md "Phase 3".</summary>
        private static readonly bool SimIncremental = ParseBool(Environment.GetEnvironmentVariable("ASMDUDE_SIM_INCREMENTAL"));

        /// <summary>Per-instance incremental switch (settings-driven via <see cref="ApplySettings"/>; seeded
        /// from the <see cref="SimIncremental"/> env default). Gates the Tier-0 / cone reuse fast paths in
        /// <see cref="RunSimulation"/>; the test seams bypass it.</summary>
        private bool incremental_ = SimIncremental;

        private static bool ParseBool(string? v)
            => string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "on", StringComparison.OrdinalIgnoreCase);

        /// <summary>Retain the new instruction sequence for <paramref name="uri"/> and return the diff vs the
        /// PREVIOUS retained sequence (null when there was none — a cold first run). This is the shared
        /// retain+diff core of the incremental pipeline. Unconditional (the <see cref="SimIncremental"/> gate
        /// lives in the callers) so the test seam can exercise it deterministically.</summary>
        private AsmSim.InstructionDiff? RetainAndDiff(Uri uri, IReadOnlyList<string> lines)
        {
            IReadOnlyList<AsmSim.Instruction> newInstr = AsmSim.Instruction.ParseProgram(lines);
            IReadOnlyList<AsmSim.Instruction>? prev;
            lock (this.lockObj_)
            {
                this.prevInstr_.TryGetValue(uri, out prev);
                this.prevInstr_[uri] = newInstr;
            }
            return prev == null ? null : AsmSim.InstructionDiff.Compute(prev, newInstr);
        }

        /// <summary>Test seam — the real retain+diff path (<see cref="RetainAndDiff"/>), independent of the
        /// <see cref="SimIncremental"/> env flag, so a host test can prove the cross-edit behavior (Tier-0
        /// recognition, line remapping, change localization) against the actual production state.</summary>
        internal AsmSim.InstructionDiff? ComputeIncrementalDiffForTest(Uri uri, IReadOnlyList<string> lines)
            => this.RetainAndDiff(uri, lines);

        /// <summary>M0 plumbing (behavior-neutral): diff this edit against the previous run, log a one-line
        /// summary of what an incremental pass WOULD reuse vs re-solve, and retain the new sequence for the
        /// next edit. No reuse happens yet — the full re-simulation still runs — so this only OBSERVES the
        /// cone an edit implies. No-op unless <see cref="SimIncremental"/>. Never throws into the sim path.</summary>
        private void RecordIncrementalDiff(Uri uri, IReadOnlyList<string> lines)
        {
            if (!this.incremental_) return;
            try
            {
                AsmSim.InstructionDiff? diff = this.RetainAndDiff(uri, lines);
                if (diff == null)
                {
                    Log($"[INC] {uri.Segments[^1]}: no previous sim — full simulation (cold)");
                }
                else if (diff.HasNoInstructionChange)
                {
                    Log($"[INC] {uri.Segments[^1]}: Tier-0 reusable (no instruction change; +{diff.AddedNewLines.Count}/-{diff.RemovedOldLines.Count} non-instruction line(s))");
                }
                else
                {
                    Log($"[INC] {uri.Segments[^1]}: instruction change — added new line(s) [{string.Join(",", diff.AddedNewLines)}], removed old line(s) [{string.Join(",", diff.RemovedOldLines)}]; {diff.NewToOld.Count} line(s) matched/reusable");
                }
            }
            catch (Exception ex)
            {
                Log($"[INC] diff failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Record the just-completed simulation of <paramref name="uri"/> (its instruction sequence
        /// paired with the cache it produced) as the reusable baseline for the next edit (M1). Called on the
        /// success path of every engine. Under <see cref="lockObj_"/> + a version guard so a superseded run
        /// never overwrites a newer baseline. Cheap (one parse + a reference to the strings-only cache).</summary>
        private void CommitSim(Uri uri, long version, IReadOnlyList<string> lines)
        {
            try
            {
                IReadOnlyList<AsmSim.Instruction> instr = AsmSim.Instruction.ParseProgram(lines);
                lock (this.lockObj_)
                {
                    if (this.simVersion_.TryGetValue(uri, out long cur) && cur == version
                        && this.cache_.TryGetValue(uri, out DocCache? entry))
                    {
                        this.committed_[uri] = (instr, entry, lines);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[INC] commit failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Remap a completed document's cache onto a Tier-0 edit: copy every per-line string and
        /// diagnostic from <paramref name="prev"/> to the line it moved to (<paramref name="diff"/>'s
        /// <see cref="AsmSim.InstructionDiff.NewToOld"/> for the per-line strings, the inverse for the
        /// per-line diagnostics). Only valid when <see cref="AsmSim.InstructionDiff.HasOnlyInertChanges"/> —
        /// then every instruction matched, so each instruction's symbolic state (hence its cached strings) is
        /// unchanged and only its line number shifted. Pure string/struct copying — no Z3.</summary>
        private static DocCache RemapCache(DocCache prev, AsmSim.InstructionDiff diff)
        {
            var next = new DocCache();
            foreach (KeyValuePair<int, int> kv in diff.NewToOld)
            {
                int n = kv.Key, o = kv.Value;
                if (prev.lineStringsBefore.TryGetValue(o, out string? b) && b != null) next.lineStringsBefore[n] = b;
                if (prev.lineStringsAfter.TryGetValue(o, out string? a) && a != null) next.lineStringsAfter[n] = a;
                if (prev.lineStringsReadLabels.TryGetValue(o, out string? r) && r != null) next.lineStringsReadLabels[n] = r;
                if (prev.lineStringsWriteLabels.TryGetValue(o, out string? w) && w != null) next.lineStringsWriteLabels[n] = w;
            }
            foreach (SimDiagnostic d in prev.diagnostics)
            {
                if (diff.OldToNew.TryGetValue(d.Line, out int nn))
                {
                    next.diagnostics.Add(d with { Line = nn });
                }
            }
            return next;
        }

        /// <summary>M1 Tier-0 fast path: if this edit changed no instruction and no label (only blank/comment
        /// lines shifted — <see cref="AsmSim.InstructionDiff.HasOnlyInertChanges"/>), rebuild the cache by
        /// remapping the last completed run's strings onto the shifted line numbers and install it WITHOUT
        /// running Z3, then fire the same progress/completed callbacks a real run would. Returns true when it
        /// reused (the caller must then return); false when a full simulation is required. Flag-agnostic — the
        /// production gate (<see cref="SimIncremental"/>) is at the call site so the test seam can drive it.</summary>
        private bool TryTier0Reuse(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted, Action<Uri>? onProgress)
        {
            try
            {
                IReadOnlyList<AsmSim.Instruction> newInstr = AsmSim.Instruction.ParseProgram(lines);
                (IReadOnlyList<AsmSim.Instruction> Instr, DocCache Cache, IReadOnlyList<string> Lines) committed;
                lock (this.lockObj_)
                {
                    if (!this.committed_.TryGetValue(uri, out committed))
                    {
                        return false; // cold — nothing to reuse
                    }
                }

                AsmSim.InstructionDiff diff = AsmSim.InstructionDiff.Compute(committed.Instr, newInstr);
                if (!diff.HasOnlyInertChanges)
                {
                    return false; // a real instruction/label changed — needs a full (or cone) re-sim
                }

                DocCache remapped = RemapCache(committed.Cache, diff);
                lock (this.lockObj_)
                {
                    if (ct.IsCancellationRequested
                        || !this.simVersion_.TryGetValue(uri, out long cur) || cur != version)
                    {
                        return true; // superseded; still "handled" (no full sim wanted for this stale version)
                    }
                    this.cache_[uri] = remapped;
                    this.committed_[uri] = (newInstr, remapped, lines);
                }
                LogInfo($"[INC] Tier-0 reuse: {remapped.lineStringsReadLabels.Count + remapped.lineStringsWriteLabels.Count} label(s) remapped, Z3 SKIPPED, {uri.Segments[^1]}");
                onProgress?.Invoke(uri);
                onCompleted?.Invoke(uri);
                return true;
            }
            catch (Exception ex)
            {
                Log($"[INC] Tier-0 reuse failed (non-fatal, falling back to full sim): {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Test seam — drive the real <see cref="TryTier0Reuse"/> synchronously, independent of the
        /// <see cref="SimIncremental"/> env flag. Returns true iff the edit was satisfied by Tier-0 reuse (no
        /// Z3). Used by the incremental==full shadow oracle tests.</summary>
        internal bool SimulateTier0ForTest(Uri uri, IReadOnlyList<string> lines)
        {
            long version;
            lock (this.lockObj_)
            {
                version = this.simVersion_.TryGetValue(uri, out long v) ? v + 1 : 1;
                this.simVersion_[uri] = version;
            }
            return this.TryTier0Reuse(uri, version, lines, CancellationToken.None, onCompleted: null, onProgress: null);
        }

        // ── M2: Tier-1 static-cone partial re-solve (component engine) ──────────────────────────────────

        /// <summary>Build the reuse-base cache for a cone re-solve: remap every matched line's strings from the
        /// baseline onto its new line number. This INCLUDES the cone lines: seeding them with their previous
        /// (now-stale) value keeps their CodeLens visible — holding its vertical space — during the ~1-2 s
        /// re-solve; the component engine's per-line <c>Emit</c> then OVERWRITES each cone line in place with the
        /// freshly-solved value. Without this seed the cone lines have no entry until solved, so their lenses
        /// collapse to nothing (the row's space is reclaimed → the code below jumps up, then back down when the
        /// new value arrives). A line ADDED by the edit has no <c>NewToOld</c> entry, so it correctly gets no
        /// stale value. DIAGNOSTICS for cone lines are NOT carried forward — a stale squiggle is more confusing
        /// than a brief absence, and the engine re-emits the correct ones as it solves each cone line. Pure
        /// string/struct copying.</summary>
        internal static DocCache RemapConeReuse(DocCache prev, AsmSim.InstructionDiff diff, IReadOnlySet<int> cone)
        {
            var next = new DocCache();
            foreach (KeyValuePair<int, int> kv in diff.NewToOld)
            {
                int n = kv.Key, o = kv.Value;
                if (prev.lineStringsBefore.TryGetValue(o, out string? b) && b != null) next.lineStringsBefore[n] = b;
                if (prev.lineStringsAfter.TryGetValue(o, out string? a) && a != null) next.lineStringsAfter[n] = a;
                if (prev.lineStringsReadLabels.TryGetValue(o, out string? r) && r != null) next.lineStringsReadLabels[n] = r;
                if (prev.lineStringsWriteLabels.TryGetValue(o, out string? w) && w != null) next.lineStringsWriteLabels[n] = w;
            }
            foreach (SimDiagnostic d in prev.diagnostics)
            {
                if (diff.OldToNew.TryGetValue(d.Line, out int nn) && !cone.Contains(nn))
                {
                    next.diagnostics.Add(d with { Line = nn });
                }
            }
            return next;
        }

        /// <summary>Set <paramref name="map"/>[<paramref name="line"/>] to <paramref name="value"/>, or REMOVE
        /// the entry when <paramref name="value"/> is null — so a freshly-solved line's strings fully replace any
        /// previous (e.g. cone-seed) value, leaving no stale residue when the new solve has no value for a field.</summary>
        private static void SetOrRemove(Dictionary<int, string?> map, int line, string? value)
        {
            if (value != null)
            {
                map[line] = value;
            }
            else
            {
                map.Remove(line);
            }
        }

        /// <summary>M2 fast path (component engine only): for a topology-PRESERVING instruction edit, re-solve
        /// only the forward dataflow cone and reuse the remapped baseline for everything else. Returns true
        /// when it handled the edit (cone re-solve dispatched); false to fall back to a full simulation
        /// (cold, a topology change, the cone covering the whole document, or any error). Flag-agnostic — the
        /// production gate is the call site.</summary>
        private bool TryConeComponentReuse(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted, Action<Uri>? onProgress)
        {
            try
            {
                IReadOnlyList<AsmSim.Instruction> newInstr = AsmSim.Instruction.ParseProgram(lines);
                (IReadOnlyList<AsmSim.Instruction> Instr, DocCache Cache, IReadOnlyList<string> Lines) committed;
                lock (this.lockObj_)
                {
                    if (!this.committed_.TryGetValue(uri, out committed)) return false; // cold
                }

                AsmSim.InstructionDiff diff = AsmSim.InstructionDiff.Compute(committed.Instr, newInstr);
                if (diff.HasOnlyInertChanges) return false;            // Tier-0 territory (handled before this)

                // Build the new CFG (pragma-lifted, same as the component engine).
                var settings = new Dictionary<string, string> { { "timeout", "5000" }, { "random_seed", "0" } };
                var cfgTools = new AsmSimTools(settings);
                var sFlow = new StaticFlow(cfgTools);
                sFlow.Update(string.Join(Environment.NewLine, RewritePragmasForCfg(lines)), removeEmptyLines: false);

                HashSet<int> cone;
                string coneKind;
                if (!AsmSim.DataflowCone.IsTopologyPreserving(diff))
                {
                    // Topology change (label moved/added/removed, jump retargeted): the dirty-set/forward cones
                    // are unsound (an edge deletion can change a node's merged value off the forward path), so
                    // use the old∪new CFG-edge-diff cone — sound for both labels and full dumps.
                    var oldFlow = new StaticFlow(new AsmSimTools(settings));
                    oldFlow.Update(string.Join(Environment.NewLine, RewritePragmasForCfg(committed.Lines)), removeEmptyLines: false);
                    cone = AsmSim.DataflowCone.StaticConeWithTopology(oldFlow, sFlow, diff);
                    coneKind = "topology";
                }
                else
                {
                    // Topology-preserving edit: re-solve every line forward-reachable from the edit (the static
                    // cone). This is used for BOTH the full register dump and the editor's labels-only output.
                    // The tighter DYNAMIC cone (dirty-set + kill) was retired from the editor path: it over-pruned
                    // on branch/loop/label code — leaving downstream CodeLens labels showing stale values — and its
                    // oracle tests only covered straight-line programs. The static cone is a sound superset (a
                    // forward-reachable line is always re-solved), so it cannot leave a downstream line stale.
                    cone = AsmSim.DataflowCone.StaticCone(sFlow, diff);
                    coneKind = "static";
                }

                DocCache reuseBase = RemapConeReuse(committed.Cache, diff, cone);
                AsmLog.Info("ASMSIM", $"[INC] {coneKind}-cone re-solve: {cone.Count} cone line(s), {reuseBase.lineStringsReadLabels.Count + reuseBase.lineStringsWriteLabels.Count} label(s) reused, {uri.Segments[^1]}");
                this.RunComponentSimulation(uri, version, lines, onCompleted, onProgress, ct, cone: cone, reuseBase: reuseBase);
                return true;
            }
            catch (Exception ex)
            {
                Log($"[INC] cone re-solve failed (non-fatal, falling back to full sim): {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Test seam — drive the real <see cref="TryConeComponentReuse"/> synchronously, independent
        /// of the <see cref="SimIncremental"/> env flag. Returns true iff the edit was handled by a cone
        /// re-solve. The instance must already have a committed Component baseline (run a full Component sim
        /// first). Used by the incremental==full M2 oracle tests.</summary>
        internal bool SimulateConeForTest(Uri uri, IReadOnlyList<string> lines)
        {
            long version;
            lock (this.lockObj_)
            {
                version = this.simVersion_.TryGetValue(uri, out long v) ? v + 1 : 1;
                this.simVersion_[uri] = version;
                this.cache_[uri] = new DocCache(); // fresh slot; the cone path installs the reuse-base into it
            }
            return this.TryConeComponentReuse(uri, version, lines, CancellationToken.None, onCompleted: null, onProgress: null);
        }

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
            tools.StateConfig.PF = true;
            tools.StateConfig.AF = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.SF = true;
            tools.StateConfig.OF = true;
            tools.StateConfig.Mem = true;
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
            Action<int, ComponentLine>? onLine = null,
            CancellationToken ct = default,
            IReadOnlySet<int>? onlyLines = null)
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

                // CFG branch/merge points — the redundant-instruction check must skip them (redundancy at a
                // phi/join is unsound). Precomputed here (sFlow is fully built) into an immutable set so the
                // parallel ExtractComponentLine workers can read it without locking. AsmDude1 used the same
                // dFlow.Is_Branch_Point / Is_Merge_Point guard.
                var branchMergeLines = new HashSet<int>();
                foreach (int ln in lineToComponent.Keys)
                {
                    if (sFlow.Is_Branch_Point(ln) || sFlow.Is_Merge_Point(ln)) branchMergeLines.Add(ln);
                }

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

                // ── Bounded worker pool for the per-line VALUE extraction (Phase P) ─────────────────────
                // DynamicFlow.Create_States_Before/After gives each line's SYMBOLIC state cheaply (no solving);
                // turning that into the concrete RAX=0x… read/write labels (the Z3 solve of the displayed
                // registers) is the per-line cost, and it is independent per line. So we clone each line's
                // state into its OWN context (serial, cheap AST Translate) and solve it on a background
                // worker — N lines at once. A SemaphoreSlim bounds concurrency (back-pressure + capped Z3
                // native memory); results stream via onLine as each finishes. Z3 contexts are NOT
                // thread-safe, hence the per-worker clone (NOT a shared ctx).
                using var pool = new System.Threading.SemaphoreSlim(SimParallelism, SimParallelism);
                var tasks = new List<Task>();
                var resultLock = new object();
                var tasksLock = new object();

                foreach (var (componentId, roots) in entriesByComponent)
                {
                    if (ct.IsCancellationRequested) break;

                    // M2 cone: a component the edit's dataflow cone never touches is skipped WHOLESALE — its
                    // DynamicFlow is never even built; its lines reuse the remapped baseline strings.
                    if (onlyLines != null)
                    {
                        bool touched = false;
                        if (linesByComponent.TryGetValue(componentId, out List<int>? clCheck))
                        {
                            foreach (int ln in clCheck)
                            {
                                if (onlyLines.Contains(ln)) { touched = true; break; }
                            }
                        }
                        if (!touched) continue;
                    }

                    try
                    {
                        // Distinct seed per component ⇒ parallel-safe (no shared Random) + reproducible.
                        var compTools = new AsmSimTools(settings, string.Empty, componentId);
                        EnableFullStateConfig(compTools);
                        compTools.Quiet = true;
                        compTools.LoopHandling = this.loopHandling_; // ASMDUDE_SIM_LOOP / runtime ApplySettings

                        int compLineCount = linesByComponent.TryGetValue(componentId, out List<int>? cls) ? cls.Count : 0;
                        var constructClock = System.Diagnostics.Stopwatch.StartNew();
                        using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, roots, compTools);
                        constructClock.Stop();
                        AsmLog.Info("ASMSIM", $"[component] component {componentId}: DynamicFlow built in {constructClock.ElapsedMilliseconds} ms ({compLineCount} lines, parallel={SimParallelism})");

                        // Per-line before/after states come straight from the DynamicFlow's SYMBOLIC engine
                        // (Create_States_Before/After → the ITE/phi merge in Create_State_Private), exactly as
                        // the original AsmDude1 simulator did — NOT collapsed to per-bit Tv values at every
                        // vertex. We then clone each line's state into its own Z3 context (serial) and solve
                        // ONLY the displayed registers on the worker pool (parallel). Symbolic build is cheap
                        // (no solving); the only Z3 solving is the per-line extraction of read/write labels.
                        List<int> compLines = linesByComponent.TryGetValue(componentId, out List<int>? cl2) ? cl2 : [];
                        compLines.Sort(); // stream CodeLens top-to-bottom
                        foreach (int line in compLines)
                        {
                            if (ct.IsCancellationRequested) break;
                            if (line < 0 || line >= lines.Count || !dFlow.Has_LineNumber(line)) continue;
                            if (onlyLines != null && !onlyLines.Contains(line)) continue; // M2 cone: extract only cone lines

                            // The line's single symbolic before/after state. The ITE/phi merge at join vertices
                            // already happened INSIDE Create_State_Private (DynamicFlow.Merge_State_Update_LOCAL),
                            // so there is exactly one state per line (one key per line); index 0 is it, or null if
                            // the line is unreached. The clone below READS the shared dFlow context, so this stays
                            // on this thread; the caller disposes these originals in the finally.
                            AsmSimState? before = dFlow.Create_States_Before(line, 0);
                            if (before == null) continue;
                            AsmSimState? after = dFlow.Create_States_After(line, 0);

                            CloneSet clone;
                            try
                            {
                                clone = this.CloneLineForWorker(line, before, after, settings);
                            }
                            catch (Exception ex)
                            {
                                Log($"[component] line {line} clone failed: {ex.GetType().Name}: {ex.Message}");
                                continue;
                            }
                            finally
                            {
                                before.Dispose();
                                after?.Dispose();
                            }

                            try
                            {
                                pool.Wait(ct); // back-pressure: at most SimParallelism clones solving at once
                            }
                            catch (OperationCanceledException)
                            {
                                clone.Dispose();
                                break;
                            }

                            int capturedLine = line;
                            CloneSet capturedClone = clone;
                            // PARALLEL (worker thread, isolated context): solve + extract + emit + dispose.
                            Task task = Task.Run(() =>
                            {
                                try
                                {
                                    var lineClock = System.Diagnostics.Stopwatch.StartNew();
                                    ComponentLine? cl = this.ExtractComponentLine(lines, capturedLine, capturedClone.Before, capturedClone.After, capturedClone.Tools, branchMergeLines);
                                    lineClock.Stop();
                                    if (cl != null && !ct.IsCancellationRequested)
                                    {
                                        lock (resultLock) result[capturedLine] = cl;
                                        onLine?.Invoke(capturedLine, cl);
                                        AsmLog.Debug("ASMSIM", $"[component] line {capturedLine + 1}: extracted in {lineClock.ElapsedMilliseconds} ms");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"[component] line {capturedLine} extraction failed: {ex.GetType().Name}: {ex.Message}");
                                }
                                finally
                                {
                                    capturedClone.Dispose();
                                    pool.Release();
                                }
                            });
                            lock (tasksLock) tasks.Add(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[SHADOW] component {componentId} failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                // Wait for every extraction worker before returning: the result dict + editor cache must be
                // complete for the (synchronous) shadow consumer; the editor path already streamed each line.
                Task[] pending;
                lock (tasksLock) pending = [.. tasks];
                // Intentional synchronous wait: ComputeComponentLines runs on the dedicated sim BACKGROUND
                // thread (never the UI/RPC thread), and must return a COMPLETE result for the synchronous
                // shadow/test consumer. No UI affinity ⇒ no deadlock.
#pragma warning disable VSTHRD002
                try { Task.WhenAll(pending).GetAwaiter().GetResult(); }
                catch (Exception ex) { Log($"[component] worker wait: {ex.GetType().Name}: {ex.Message}"); }
#pragma warning restore VSTHRD002
            }
            catch (Exception ex)
            {
                Log($"[SHADOW] component engine failed: {ex.GetType().Name}: {ex.Message}");
            }
            return result;
        }

        /// <summary>A line's before/after states cloned into their OWN Z3 context for parallel solving
        /// (Phase P). OWNS the context — <see cref="Dispose"/> releases the states then the context.</summary>
        private readonly struct CloneSet(Microsoft.Z3.Context ctx, AsmSimTools tools, AsmSimState before, AsmSimState? after)
        {
            public AsmSimTools Tools { get; } = tools;
            public AsmSimState Before { get; } = before;
            public AsmSimState? After { get; } = after;
            private readonly Microsoft.Z3.Context ctx_ = ctx;

            public void Dispose()
            {
                this.After?.Dispose();
                this.Before.Dispose();
                this.ctx_.Dispose();
                AsmSim.Z3ContextTracker.Disposed();
            }
        }

        /// <summary>Clone a line's before/after states into a fresh, isolated Z3 context so they can be
        /// solved on a worker thread without touching the shared component context. MUST be called on the
        /// evaluator thread — the clone (Z3 <c>Translate</c>) READS the shared source context. Cheap: copies
        /// AST, no solving. The per-line <see cref="Tools"/> uses the SEEDED ctor (distinct, isolated
        /// <see cref="Random"/>) — the copy ctor would SHARE one non-thread-safe Random across workers.</summary>
        private CloneSet CloneLineForWorker(int line, AsmSimState before, AsmSimState? after, Dictionary<string, string> settings)
        {
            var lineCtx = new Microsoft.Z3.Context(new Dictionary<string, string>(settings));
            AsmSim.Z3ContextTracker.Created();
            var lineTools = new AsmSimTools(new Dictionary<string, string>(settings), string.Empty, line) { SharedCtx = lineCtx };
            EnableFullStateConfig(lineTools);
            lineTools.Quiet = true;
            lineTools.LoopHandling = this.loopHandling_;

            var bClone = new AsmSimState(lineCtx, lineTools, before.TailKey ?? string.Empty, before.HeadKey ?? string.Empty);
            before.Copy(bClone);
            AsmSimState? aClone = null;
            if (after != null)
            {
                aClone = new AsmSimState(lineCtx, lineTools, after.TailKey ?? string.Empty, after.HeadKey ?? string.Empty);
                after.Copy(aClone);
            }
            return new CloneSet(lineCtx, lineTools, bClone, aClone);
        }

        /// <summary>Turn one line's dynamic before/after states into its editor annotations (state strings,
        /// CodeLens read/write labels, diagnostics) — the SAME per-line logic the linear sim uses. The heavy
        /// Z3 value-solving (ComputeStateString → solve each register) happens HERE, per line, so this is
        /// what dominates a slow run. Returns null for non-instruction lines / nothing-to-show.</summary>
        private ComponentLine? ExtractComponentLine(IReadOnlyList<string> lines, int line, AsmSimState? before, AsmSimState? after, AsmSimTools compTools, IReadOnlySet<int>? branchMergeLines = null)
        {
            // Match the linear sim EXACTLY: only real instruction lines carry annotations.
            (_, _, Mnemonic mnemonic, string[] args, _) = AsmSourceTools.ParseLine(lines[line].Trim(), -1, -1, AssemblerEnum.UNKNOWN);
            if (mnemonic == Mnemonic.NONE) return null;

            // Full register dumps are skipped in the editor (computeFullState_ == false) — only the read/write
            // labels below are needed for CodeLens. See computeFullState_.
            string? beforeStr = (this.computeFullState_ && before is not null) ? ComputeStateString(before) : null;
            string? afterStr = (this.computeFullState_ && after is not null) ? ComputeStateString(after) : null;

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
                // Redundant check: re-applies the instruction to the (unfrozen-probe of the) BEFORE state, so
                // it sees both keys. Skip CFG branch/merge points (redundancy across a phi/join isn't meaningful).
                bool branchMerge = branchMergeLines?.Contains(line) ?? false;
                this.TryCollectRedundant(lines[line], line, before, branchMerge, diags);
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
        private void RunComponentSimulation(Uri uri, long version, IReadOnlyList<string> lines, Action<Uri>? onCompleted, Action<Uri>? onProgress, CancellationToken ct,
            IReadOnlySet<int>? cone = null, DocCache? reuseBase = null)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int linesWritten = 0;
            int diagCount = 0;
            long lastProgressMs = -ProgressNotifyThrottleMs;

            // M2 cone path: seed the cache with the remapped reuse of NON-cone lines so they are visible
            // immediately; the cone lines below overwrite their entries with freshly-solved values.
            if (reuseBase != null)
            {
                lock (this.lockObj_)
                {
                    if (ct.IsCancellationRequested
                        || !this.simVersion_.TryGetValue(uri, out long cv) || cv != version)
                    {
                        return;
                    }
                    this.cache_[uri] = reuseBase;
                }
            }

            // Called per line as each worker resolves it — CONCURRENTLY (Phase P). Writes that one line to
            // the cache under lockObj_ (vs the read paths) and decides the throttled refresh under the same
            // lock (so linesWritten/diagCount/lastProgressMs are race-free); fires onProgress outside the lock.
            void Emit(int line, ComponentLine cl)
            {
                bool fireProgress = false;
                lock (this.lockObj_)
                {
                    if (!ct.IsCancellationRequested
                        && this.simVersion_.TryGetValue(uri, out long curVer) && curVer == version
                        && this.cache_.TryGetValue(uri, out DocCache? entry))
                    {
                        // Overwrite this line's display strings WHOLESALE (set when the freshly-solved value is
                        // non-null, REMOVE when null). The remove is essential on the cone path: RemapConeReuse
                        // seeds cone lines with their previous value (so the lens holds its space during the
                        // re-solve instead of collapsing); if the new solve yields no value for a field, the
                        // stale seed must be cleared or it lingers (and would break the incremental==full oracle).
                        SetOrRemove(entry.lineStringsBefore, line, cl.Before);
                        SetOrRemove(entry.lineStringsAfter, line, cl.After);
                        SetOrRemove(entry.lineStringsReadLabels, line, cl.ReadLabel);
                        SetOrRemove(entry.lineStringsWriteLabels, line, cl.WriteLabel);
                        if (cl.Diagnostics.Count > 0)
                        {
                            entry.diagnostics.AddRange(cl.Diagnostics);
                            diagCount += cl.Diagnostics.Count;
                        }
                        linesWritten++;

                        long now = clock.ElapsedMilliseconds;
                        if (now - lastProgressMs >= ProgressNotifyThrottleMs)
                        {
                            lastProgressMs = now;
                            fireProgress = true;
                        }
                    }
                }
                if (fireProgress) onProgress?.Invoke(uri);
            }

            try
            {
                this.ComputeComponentLines(lines, Emit, ct, onlyLines: cone);
            }
            catch (Exception ex)
            {
                Log($"[THREAD] COMPONENT EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            if (ct.IsCancellationRequested) return;

            string scope = cone != null ? $"[component+cone] SUCCESS: {linesWritten} cone line(s) re-solved (cone={cone.Count})" : $"[component] SUCCESS: {linesWritten} lines";
            LogInfo($"{scope}, {diagCount} diagnostics, loop={this.loopHandling_}, total {clock.ElapsedMilliseconds} ms; Z3 ctx live={AsmSim.Z3ContextTracker.Live} peak={AsmSim.Z3ContextTracker.Peak}");
            this.CommitSim(uri, version, lines); // reusable baseline for the next edit (M1 Tier-0)
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
            LogInfo($"sim started: engine={this.engineMode_} loop={this.loopHandling_}, {lines.Count} lines, {uri.Segments[^1]}");
            LogCfgPartition(uri, lines);
            this.RecordIncrementalDiff(uri, lines); // M0: observe the edit's cone; no behavior change (no-op unless ASMDUDE_SIM_INCREMENTAL)

            // M1 Tier-0: if only blank/comment lines shifted, remap the previous result and skip Z3 entirely.
            // Gated by the AsmSim_Incremental setting (default OFF ⇒ the full sim below always runs, unchanged).
            if (this.incremental_ && this.TryTier0Reuse(uri, version, lines, ct, onCompleted, onProgress))
            {
                return;
            }

            // M2 Tier-1: a topology-preserving instruction edit re-solves only the forward dataflow cone and
            // reuses the remapped baseline for the rest (component engine only). Falls through to a full sim
            // when cold / on a topology change / on error.
            if (this.incremental_ && this.engineMode_ == SimEngineMode.Component
                && this.TryConeComponentReuse(uri, version, lines, ct, onCompleted, onProgress))
            {
                return;
            }

            // The merge engine is the editor default; drive the editor from the dynamic per-component
            // engine instead of the linear walk below. Read paths are unchanged (same cache).
            if (this.engineMode_ == SimEngineMode.Component)
            {
                this.RunComponentSimulation(uri, version, lines, onCompleted, onProgress, ct);
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
                        Log($"[THREAD] Line {i}: not an instruction ('{line[..Math.Min(30, line.Length)]}'), skipping");
                        continue;
                    }

                    Log($"[THREAD] Line {i} (editor line {i + 1}): processing '{mnemonic} {string.Join(", ", args)}'...");

                    // Time the Z3-heavy work for this instruction (before/after state strings,
                    // opcode instantiation, the SimpleStep_Forward solve) so slow lines are greppable.
                    var lineClock = System.Diagnostics.Stopwatch.StartNew();

                    // ── Before-state full dump (only when computeFullState_; editor skips it) ──
                    string? beforeStr = null;
                    if (this.computeFullState_ && !stateToString.TryGetValue(state, out beforeStr))
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
                    // The 4 base checks need only the BEFORE state. The redundant check needs the AFTER state,
                    // so it runs below (after the step) and appends to the same list; newDiagnostics is
                    // accumulated AFTER that so its count includes redundant diagnostics.
                    var lineDiags = new List<SimDiagnostic>();
                    this.CollectDiagnostics(line, i, state, tools, lineDiags, opcodeBase);

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

                    // ── After-state full dump (only when computeFullState_; editor skips it) ──
                    string? afterStr = null;
                    if (this.computeFullState_ && !stateToString.TryGetValue(state, out afterStr))
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

                    // Redundant-instruction check against the BEFORE state (`stateBeforeStep`). The linear sim
                    // is single-path (no merges) and branch instructions write nothing, so no branch/merge
                    // guard is needed here. Appends to lineDiags before they are committed to the cache +
                    // counted in newDiagnostics.
                    this.TryCollectRedundant(line, i, stateBeforeStep, isBranchOrMergePoint: false, lineDiags);
                    newDiagnostics.AddRange(lineDiags);

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
                // Record this completed run as the reusable baseline for the next edit (M1 Tier-0).
                this.CommitSim(uri, version, lines);
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
                        $"\"{CodeOnly(line)}\" is not (fully) implemented in the simulator.",
                        SimDiagnosticKind.NotImplemented));
                    return;
                }

                // Syntax error
                if (opcodeBase.IsHalted)
                {
                    string msg = opcodeBase.SyntaxError ?? $"Syntax error in \"{CodeOnly(line)}\"";
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
                            $"\"{CodeOnly(line)}\" is unreachable.",
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
                            // Show the proven flag value (the simulator may know some bits even when "undefined"
                            // overall — e.g. a flag that is partly constrained); '?' marks the unknown bit. The
                            // "Usage of undefined value" prefix already states these are undefined, so no suffix.
                            undefinedItems.Append(flag).Append("=").Append(ToolsZ3.ToStringBin(beforeState.GetTv(flag))).Append("; ");
                        }
                    }

                    foreach (Rn reg in opcodeBase.RegsReadStatic)
                    {
                        Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                        if (cfg.IsRegOn(reg64) && beforeState.Is_Undefined(reg))
                        {
                            // Append the simulator-determined (partial) value so the message carries the data we
                            // already computed: known nibbles show as hex, unknown nibbles as '?'.
                            string val = ToolsZ3.ToStringHex(beforeState.GetTvArray(reg64));
                            undefinedItems.Append(reg64).Append("=").Append(val).Append("; ");
                        }
                    }

                    if (undefinedItems.Length > 0)
                    {
                        diagnostics.Add(new SimDiagnostic(lineIndex,
                            $"Usage of undefined value in \"{CodeOnly(line)}\": {undefinedItems}".TrimEnd(' ', ';'),
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

        /// <summary>
        /// Emits a <see cref="SimDiagnosticKind.Redundant"/> diagnostic when the instruction on
        /// <paramref name="line"/> provably cannot change the tracked machine state. This is the editor
        /// POLICY layer: it gates on the setting and skips CFG branch/merge points (where redundancy across a
        /// phi/join is not meaningful); the symbolic decision is delegated to
        /// <see cref="Runner.IsRedundantInstruction(string, AsmSimState)"/>, which is the only place that
        /// answers it correctly (it queries an unfrozen probe so an overwritten register's pre-value is still
        /// present — the freeze/Compress that keeps models small would otherwise drop it). NOP / not-implemented
        /// / mock-SIMD / non-instruction lines are reported as "not redundant" by that method (null verdict).
        /// See REDUNDANT_DIAGNOSTICS_PLAN.md.
        /// </summary>
        /// <summary>
        /// The instruction text shown in a diagnostic: the source line with any trailing comment
        /// (<c>#</c>/<c>;</c> remark) stripped, then trimmed. Diagnostics quote the code, not the prose.
        /// </summary>
        private static string CodeOnly(string line)
        {
            int remarkPos = AsmSourceTools.GetRemarkCharPosition(line);
            return (remarkPos >= 0 ? line[..remarkPos] : line).Trim();
        }

        private void TryCollectRedundant(string line, int lineIndex, AsmSimState? beforeState, bool isBranchOrMergePoint, List<SimDiagnostic> diagnostics)
        {
            // Redundancy is computed unconditionally, like the other sim diagnostic categories (unreachable/
            // undefined). The showRedundant_ field remains the explicit opt-out seam for tests; production
            // keeps it on (BuildSimSettings forces ShowRedundant=true). Branch/merge points and a missing
            // before-state are still skipped (redundancy across a phi/join is not meaningful).
            if (!this.showRedundant_ || beforeState == null || isBranchOrMergePoint)
            {
                return;
            }
            try
            {
                RedundancyVerdict? verdict = Runner.IsRedundantInstruction(line, beforeState);
                if (verdict is { IsRedundant: true })
                {
                    diagnostics.Add(new SimDiagnostic(lineIndex, $"\"{CodeOnly(line)}\" is redundant.", SimDiagnosticKind.Redundant));
                    Log($"[DIAG] Line {lineIndex}: REDUNDANT ({verdict.Value.RedundantCount}/{verdict.Value.WrittenCount} writes unchanged)");
                }
            }
            catch (Exception ex)
            {
                AsmLog.Debug("ASMSIM", $"redundant check failed line {lineIndex}: {ex.Message}");
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
                this.prevInstr_.Clear();
                this.committed_.Clear();
            }
        }
    }
}
