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

    using Microsoft.Extensions.Logging;

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

        private readonly ILogger logger_;

        internal sealed class DocCache
        {
            internal readonly Dictionary<int, string?> lineStringsAfter = [];
            internal readonly Dictionary<int, string?> lineStringsBefore = [];
            internal readonly List<AsmSimState> ownedStates = [];
            internal readonly List<SimDiagnostic> diagnostics = [];

            internal string? GetBeforeState(int lineNumber) => this.lineStringsBefore.TryGetValue(lineNumber, out var s) ? s : null;
            internal string? GetAfterState(int lineNumber) => this.lineStringsAfter.TryGetValue(lineNumber, out var s) ? s : null;
        }

        private readonly Dictionary<Uri, DocCache> cache_ = [];
        private readonly Dictionary<Uri, CancellationTokenSource> pendingTasks_ = [];
        private readonly Dictionary<Uri, long> simVersion_ = [];
        private readonly object lockObj_ = new();

        internal LspAsmSimulator(ILogger logger)
        {
            this.logger_ = logger;
        }

        private static void Log(string msg) => AsmDudeLog.Debug($"[ASMSIM] {msg}");

        /// <summary>
        /// Trigger a background re-simulation for the given document.
        /// Called whenever the document content changes.
        /// The optional <paramref name="onCompleted"/> callback is invoked (from the background
        /// thread) after simulation finishes, so callers can re-publish diagnostics.
        /// </summary>
        internal void InvalidateAndSimulate(Uri uri, IReadOnlyList<string> lines, Action<Uri>? onCompleted = null)
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
                // Put a fresh (empty) cache entry immediately so stale data from the
                // previous simulation is invisible while the new one runs.
                if (this.cache_.TryGetValue(uri, out DocCache? old))
                    DisposeList(old.ownedStates);
                this.cache_[uri] = new DocCache();
            }

            _ = Task.Run(() => this.RunSimulation(uri, version, lines, cts.Token, onCompleted), cts.Token);
        }

        // ── After-state queries ────────────────────────────────────────────────

        /// <summary>
        /// Pre-computed register+flag state string after the given line, or null if not available.
        /// </summary>
        internal string? GetRegisterStatesAfterLine(Uri uri, int lineNumber)
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

        // ── Background simulation ──────────────────────────────────────────────

        private void RunSimulation(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted)
        {
            Log($"[THREAD] RunSimulation started for {uri}, {lines.Count} lines");
            try
            {
                var settings = new Dictionary<string, string>
                {
                    { "timeout", "5000" },
                };
                AsmSimTools tools = new(settings);
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
                tools.Quiet = true;

                var newDiagnostics = new List<SimDiagnostic>();
                var ownedStates = new List<AsmSimState>();
                int linesWritten = 0;

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
                        DisposeList(ownedStates);
                        return;
                    }

                    string line = lines[i].Trim();

                    // Blank/comment: skip (no Z3 work, state unchanged).
                    if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                    {
                        Log($"[THREAD] Line {i}: blank/comment/directive, skipping");
                        continue;
                    }

                    // Parse to check if this is a real instruction the simulator can handle.
                    (AsmTools.KeywordID[] _, string _label, Mnemonic mnemonic, string[] args, string _remark)
                        = AsmTools.AsmSourceTools.ParseLine(line, -1, -1, AssemblerEnum.UNKNOWN);

                    // Skip lines that aren't real instructions: labels, directives, includes, etc.
                    if (mnemonic == Mnemonic.NONE)
                    {
                        Log($"[THREAD] Line {i}: not an instruction ('{line.Substring(0, Math.Min(30, line.Length))}'), skipping");
                        continue;
                    }

                    Log($"[THREAD] Line {i}: processing '{mnemonic} {string.Join(", ", args)}'...");

                    // ── Before-state: state at entry to this instruction ──────────
                    if (!stateToString.TryGetValue(state, out string? beforeStr))
                    {
                        beforeStr = ComputeStateString(state);
                        stateToString[state] = beforeStr;
                    }

                    // ── Diagnostics ───────────────────────────────────────────────
                    this.CollectDiagnostics(line, i, state, tools, newDiagnostics);

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
                        this.logger_.LogDebug("LspAsmSimulator: line {Line}: {Ex}", i, ex.Message);
                    }

                    // ── After-state ───────────────────────────────────────────────
                    if (!stateToString.TryGetValue(state, out string? afterStr))
                    {
                        afterStr = ComputeStateString(state);
                        stateToString[state] = afterStr;
                    }

                    // ── Write to cache incrementally so hover can see results immediately ──
                    lock (this.lockObj_)
                    {
                        if (!ct.IsCancellationRequested
                            && this.simVersion_.TryGetValue(uri, out long curVer) && curVer == version
                            && this.cache_.TryGetValue(uri, out DocCache? entry))
                        {
                            if (beforeStr != null) entry.lineStringsBefore[i] = beforeStr;
                            if (afterStr != null) entry.lineStringsAfter[i] = afterStr;
                            linesWritten++;
                        }
                    }

                    Log($"[THREAD] Line {i}: {mnemonic} done");
                }

                // Finalize: attach owned states and diagnostics to the cache entry.
                // String data was already written incrementally above.
                lock (this.lockObj_)
                {
                    if (ct.IsCancellationRequested
                        || !this.simVersion_.TryGetValue(uri, out long curVer) || curVer != version)
                    {
                        DisposeList(ownedStates);
                        return;
                    }
                    if (this.cache_.TryGetValue(uri, out DocCache? entry))
                    {
                        entry.diagnostics.AddRange(newDiagnostics);
                        ownedStates.ForEach(s => entry.ownedStates.Add(s));
                    }
                    else
                    {
                        DisposeList(ownedStates);
                    }
                }

                Log($"[THREAD] SUCCESS: {linesWritten} lines written, {newDiagnostics.Count} diagnostics");
                onCompleted?.Invoke(uri);
            }
            catch (OperationCanceledException)
            {
                // Expected — a newer simulation replaced this one.
            }
            catch (Exception ex)
            {
                Log($"[THREAD] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                this.logger_.LogWarning("LspAsmSimulator: simulation failed for {Uri}: {Ex}", uri, ex.Message);
            }
        }

        private void CollectDiagnostics(string line, int lineIndex, AsmSimState beforeState, AsmSimTools tools, List<SimDiagnostic> diagnostics)
        {
            try
            {
(AsmTools.KeywordID[] _, string _label, Mnemonic mnemonic, string[] args, string _remark)
                     = AsmTools.AsmSourceTools.ParseLine(line, -1, -1, AssemblerEnum.UNKNOWN);

                if (mnemonic == Mnemonic.NONE)
                    return;

                var dummyKeys = ("dummy_prev", "dummy_next", "dummy_branch");
                using OpcodeBase? opcodeBase = Runner.InstantiateOpcode(mnemonic, args, dummyKeys, tools);

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
                    if (beforeState.IsConsistent == Tv.ZERO)
                    {
                        diagnostics.Add(new SimDiagnostic(lineIndex,
                            $"\"{line.Trim()}\" is unreachable.",
                            SimDiagnosticKind.Unreachable));
                        return; // no point checking usage-undefined on unreachable code
                    }
                }
                catch (Exception ex)
                {
                    this.logger_.LogDebug("LspAsmSimulator: IsConsistent failed line {L}: {Ex}", lineIndex, ex.Message);
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
                    this.logger_.LogDebug("LspAsmSimulator: usage-undefined check failed line {L}: {Ex}", lineIndex, ex.Message);
                }
            }
            catch (Exception ex)
            {
                this.logger_.LogDebug("LspAsmSimulator: diagnostics collection failed line {L}: {Ex}", lineIndex, ex.Message);
            }
        }

        private static void DisposeList(List<AsmSimState> states)
        {
            foreach (AsmSimState s in states)
            {
                s.Dispose();
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

                foreach (DocCache entry in this.cache_.Values)
                {
                    DisposeList(entry.ownedStates);
                }
                this.cache_.Clear();
            }
        }
    }
}
