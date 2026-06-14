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

namespace AsmSim
{
    using AsmTools;

    using System;
    using System.Collections.Generic;

    /// <summary>The static read/write footprint of one instruction line — the metadata the dynamic cone
    /// propagates over (no Z3, no solving; just which locations the line reads and writes).</summary>
    /// <param name="ReadRegs">64-bit-canonical registers read.</param>
    /// <param name="WriteRegs">64-bit-canonical registers written (gen-dirty when the line's inputs are dirty).</param>
    /// <param name="KillRegs">The subset of <paramref name="WriteRegs"/> that FULLY overwrite their 64-bit
    /// register (a 64- or 32-bit destination — 32-bit writes zero-extend). Only these may clear a register's
    /// dirtiness; an 8/16-bit write merges with old bits and must NOT kill. Conservative when unsure: omit.</param>
    /// <param name="ReadFlags">Flags read.</param>
    /// <param name="WriteFlags">Flags written (fully overwritten ⇒ killable).</param>
    public sealed record LineEffects(
        IReadOnlySet<Rn> ReadRegs,
        IReadOnlySet<Rn> WriteRegs,
        IReadOnlySet<Rn> KillRegs,
        Flags ReadFlags,
        Flags WriteFlags,
        bool ReadMem,
        bool WriteMem);

    /// <summary>
    /// Computes the <b>dynamic dataflow cone</b> of an edit (INCREMENTAL_SIM_PLAN.md §5 Tier-2): the lines
    /// whose displayed read/write labels can actually change, found by propagating a <em>dirty set</em> of
    /// locations (registers/flags/memory) forward with <em>kill</em>. Tighter than the static cone — a line
    /// that reads only clean locations is excluded even if it is forward-reachable from the edit, and a line
    /// that fully overwrites a dirty register clears it. Monotone forward dataflow (gen/kill, union meet) ⇒
    /// a worklist iteration reaches a fixpoint (loops included).
    /// </summary>
    /// <remarks>
    /// SOUNDNESS SCOPE: this cone is sound for the editor's <b>labels-only</b> output (read label = before
    /// values of read locations; write label = after values of written locations). It is NOT sound for a
    /// full before/after register DUMP, where any dirty location anywhere makes every downstream line's dump
    /// differ — for that, use the <see cref="DataflowCone">static cone</see>. A "kill" line (reads only clean
    /// locations, fully overwrites a dirty register) is correctly excluded: it computes the same value as
    /// before (clean inputs, same instruction), so its labels are unchanged, while its write clears the
    /// dirtiness for everything downstream. Memory is conservative: a memory write never kills (addresses are
    /// not tracked), so once memory is dirty it stays dirty and every later memory read is in the cone.
    /// </remarks>
    public static class DynamicCone
    {
        private sealed class Dirty
        {
            internal readonly HashSet<Rn> Regs = [];
            internal Flags Flags = Flags.NONE;
            internal bool Mem;

            internal Dirty Clone()
            {
                var d = new Dirty { Flags = this.Flags, Mem = this.Mem };
                d.Regs.UnionWith(this.Regs);
                return d;
            }

            internal bool SetEquals(Dirty o)
                => this.Mem == o.Mem && this.Flags == o.Flags && this.Regs.SetEquals(o.Regs);

            internal void UnionWith(Dirty o)
            {
                this.Regs.UnionWith(o.Regs);
                this.Flags |= o.Flags;
                this.Mem |= o.Mem;
            }
        }

        /// <summary>
        /// The dynamic cone: instruction lines that must be re-solved. <paramref name="newEffects"/> /
        /// <paramref name="oldEffects"/> give per-line footprints keyed by NEW / OLD line index respectively
        /// (the old ones are needed only to seed the dirtiness of a DELETED instruction's writes at the line
        /// that now follows it). Changed (added) instruction lines are always in the cone.
        /// </summary>
        public static HashSet<int> Compute(
            StaticFlow newFlow,
            InstructionDiff diff,
            IReadOnlyDictionary<int, LineEffects> newEffects,
            IReadOnlyDictionary<int, LineEffects> oldEffects)
        {
            ArgumentNullException.ThrowIfNull(newFlow);
            ArgumentNullException.ThrowIfNull(diff);
            ArgumentNullException.ThrowIfNull(newEffects);
            ArgumentNullException.ThrowIfNull(oldEffects);

            var changed = new HashSet<int>(diff.AddedNewLines);

            // Seed: a deleted instruction's writes are dirty entering the new line that now follows it.
            var inject = new Dictionary<int, Dirty>();
            foreach (int removedOld in diff.RemovedOldLines)
            {
                if (!oldEffects.TryGetValue(removedOld, out LineEffects? e)) continue;
                int succ = SuccessorNewLine(diff, removedOld);
                if (succ < 0) continue;
                if (!inject.TryGetValue(succ, out Dirty? seed))
                {
                    seed = new Dirty();
                    inject[succ] = seed;
                }
                seed.Regs.UnionWith(e.WriteRegs);
                seed.Flags |= e.WriteFlags;
                seed.Mem |= e.WriteMem;
            }

            int last = newFlow.LastLineNumber;
            var inSet = new Dictionary<int, Dirty>();
            var outSet = new Dictionary<int, Dirty>();
            for (int l = 0; l <= last; l++)
            {
                if (!newFlow.HasLine(l)) continue;
                inSet[l] = new Dirty();
                outSet[l] = new Dirty();
            }

            // Worklist to fixpoint. Monotone (union meet + gen/kill) ⇒ terminates.
            var work = new Queue<int>();
            var queued = new HashSet<int>();
            foreach (int l in inSet.Keys)
            {
                work.Enqueue(l);
                queued.Add(l);
            }

            while (work.Count > 0)
            {
                int l = work.Dequeue();
                queued.Remove(l);

                // in[l] = injected ∪ ⋃ out[pred]
                var inNew = new Dirty();
                if (inject.TryGetValue(l, out Dirty? seed)) inNew.UnionWith(seed);
                foreach ((int p, bool _) in newFlow.Get_Prev_LineNumber(l))
                {
                    if (outSet.TryGetValue(p, out Dirty? op)) inNew.UnionWith(op);
                }
                inSet[l] = inNew;

                Dirty outNew = Transfer(l, inNew, changed, newEffects);
                if (!outNew.SetEquals(outSet[l]))
                {
                    outSet[l] = outNew;
                    (int regular, int branch) = newFlow.Get_Next_LineNumber(l);
                    foreach (int s in new[] { regular, branch })
                    {
                        if (s >= 0 && inSet.ContainsKey(s) && queued.Add(s)) work.Enqueue(s);
                    }
                }
            }

            // Cone = changed instruction lines + lines that read a dirty location.
            var cone = new HashSet<int>();
            foreach (int l in inSet.Keys)
            {
                bool isInstr = newEffects.ContainsKey(l);
                if (changed.Contains(l) && isInstr) cone.Add(l);
                else if (isInstr && ReadsDirty(inSet[l], newEffects[l])) cone.Add(l);
            }
            return cone;
        }

        private static Dirty Transfer(int line, Dirty inDirty, HashSet<int> changed, IReadOnlyDictionary<int, LineEffects> newEffects)
        {
            Dirty outDirty = inDirty.Clone();
            if (!newEffects.TryGetValue(line, out LineEffects? e))
            {
                return outDirty; // non-instruction line: pass through
            }

            bool readsDirty = changed.Contains(line) || ReadsDirty(inDirty, e);
            if (readsDirty)
            {
                // Output depends on dirty inputs (or the instruction itself changed) ⇒ its writes are dirty.
                outDirty.Regs.UnionWith(e.WriteRegs);
                outDirty.Flags |= e.WriteFlags;
                if (e.WriteMem) outDirty.Mem = true;
            }
            else
            {
                // Reads only clean locations ⇒ it recomputes the same value: a FULL write clears dirtiness.
                outDirty.Regs.ExceptWith(e.KillRegs);
                outDirty.Flags &= ~e.WriteFlags;
                // Memory is never killed (addresses untracked): leave outDirty.Mem as-is.
            }
            return outDirty;
        }

        private static bool ReadsDirty(Dirty inDirty, LineEffects e)
        {
            if ((inDirty.Flags & e.ReadFlags) != Flags.NONE) return true;
            if (inDirty.Mem && e.ReadMem) return true;
            foreach (Rn r in e.ReadRegs)
            {
                if (inDirty.Regs.Contains(r)) return true;
            }
            return false;
        }

        /// <summary>The new-line index of the nearest old line after <paramref name="removedOld"/> that
        /// survived (matched), or -1 if the deletion is at the tail.</summary>
        private static int SuccessorNewLine(InstructionDiff diff, int removedOld)
        {
            for (int o = removedOld + 1; o < diff.Old.Count; o++)
            {
                if (diff.OldToNew.TryGetValue(o, out int n)) return n;
            }
            return -1;
        }
    }
}
