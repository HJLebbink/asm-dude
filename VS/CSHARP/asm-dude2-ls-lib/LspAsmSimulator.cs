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
    using System.Linq;
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
                // Put a fresh (empty) cache entry immediately so stale data from the
                // previous simulation is invisible while the new one runs.
                if (this.cache_.TryGetValue(uri, out DocCache? old))
                    DisposeList(old.ownedStates);
                this.cache_[uri] = new DocCache();
            }

            _ = Task.Run(() => this.RunSimulation(uri, version, lines, cts.Token, onCompleted, onProgress), cts.Token);
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
                int hexMarker = s.IndexOf("= 0x", StringComparison.Ordinal);
                if (hexMarker >= 0)
                {
                    string hexVal = s[(hexMarker + 4)..].Trim().TrimStart('0');
                    if (hexVal.Length == 0) hexVal = "0";
                    int firstEq = s.IndexOf(" = ", StringComparison.Ordinal);
                    string regName = firstEq > 0 ? s[..firstEq].Trim() : "?";
                    parts.Add($"{regName}=0x{hexVal}");
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
                foreach (int k in entry.lineStringsReadLabels.Keys)  allPositions.Add(k);
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
                    int eq    = token.IndexOf('=');
                    if (colon < 0 || eq <= colon + 1) continue;
                    string prefix = token[..(colon + 1)];   // "w:", "r:", "rw:"
                    string name   = token[(colon + 1)..eq]; // "ZF", "RAX"
                    string value  = token[(eq + 1)..];      // "1", "0x10", "?"
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
        private static string? MergeCompactLabels(string? writeCompact, string? readCompact)
        {
            if (string.IsNullOrEmpty(writeCompact) && string.IsNullOrEmpty(readCompact)) return null;
            if (string.IsNullOrEmpty(readCompact))  return writeCompact;
            if (string.IsNullOrEmpty(writeCompact)) return readCompact;

            var writeItems = ParseCompactItems(writeCompact);
            var readItems  = ParseCompactItems(readCompact);

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
                if (hasOne && tv == Tv.UNKNOWN)
                    sb.Append($"\nr:{reg64} = ?");
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
                      .Append(tv == Tv.ONE ? '1' : tv == Tv.ZERO ? '0' : '?');
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
                if (hasOne && tv == Tv.UNKNOWN)
                    sb.Append($"\nw:{reg64} = ?");
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
                      .Append(tv == Tv.ONE ? '1' : tv == Tv.ZERO ? '0' : '?');
            }
            if (flagSb.Length > 0)
                sb.Append('\n').Append(flagSb);

            string result = sb.ToString();
            return result.Length > 0 ? result : null;
        }

        // ── Background simulation ──────────────────────────────────────────────

        private void RunSimulation(Uri uri, long version, IReadOnlyList<string> lines, CancellationToken ct, Action<Uri>? onCompleted, Action<Uri>? onProgress)
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
                                    this.logger_.LogDebug("LspAsmSimulator: #pragma assume failed line {L}: {Ex}", i, ex.Message);
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

                    Log($"[THREAD] Line {i}: processing '{mnemonic} {string.Join(", ", args)}'...");

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
                        this.logger_.LogDebug("LspAsmSimulator: line {Line}: {Ex}", i, ex.Message);
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
                    string? readLabel  = ComputeReadLabel (stateBeforeStep, readRegsOfThis,  readFlagsOfThis);
                    string? writeLabel = ComputeWriteLabel(state,           writtenRegs,      writtenFlags);

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
                            if (afterStr != null)  entry.lineStringsAfter[i]  = afterStr;
                            if (readLabel  != null) entry.lineStringsReadLabels[i]  = readLabel;
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
                    // Notify the client to re-request inlay hints for the visible range.
                    if (shouldRefresh) onProgress?.Invoke(uri);

                    Log($"[THREAD] Line {i}: {mnemonic} done");
                }

                // Finalize: attach owned states. Diagnostics and strings were already written incrementally.
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
                        ownedStates.ForEach(s => entry.ownedStates.Add(s));
                    }
                    else
                    {
                        DisposeList(ownedStates);
                    }
                }

                Log($"[THREAD] SUCCESS: {linesWritten} lines written, {newDiagnostics.Count} diagnostics");
                // Notify client to refresh inlay hints (final flush) and republish diagnostics.
                onProgress?.Invoke(uri);
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
