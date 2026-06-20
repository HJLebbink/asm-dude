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

    /// <summary>
    /// Computes the <b>forward dataflow cone</b> of an edit — the set of (new) lines whose simulation result
    /// can change — so an incremental pass re-solves only those and reuses cached strings everywhere else
    /// (INCREMENTAL_SIM_PLAN.md §5, M2). Pure and Z3-free: it reasons purely over the new
    /// <see cref="StaticFlow"/> (the CFG) and an <see cref="InstructionDiff"/> (what the edit touched).
    /// </summary>
    /// <remarks>
    /// This is the <b>Tier-1 static cone</b>: forward CFG-reachability from every changed line. It is a sound
    /// OVER-approximation of the true (dynamic, dirty-set) cone — it may re-solve a line whose value turns
    /// out identical, which is harmless (proving it equal would require solving, defeating the purpose). It
    /// is seeded from <em>"the instruction at this line changed"</em>, not from <em>"a value changed"</em>.
    /// Only meaningful for an edit that is NOT <see cref="InstructionDiff.HasOnlyInertChanges"/> (those are
    /// the Tier-0 whole-reuse case, M1); when a real instruction/label changed, this narrows the re-solve.
    /// </remarks>
    public static class DataflowCone
    {
        /// <summary>
        /// The (new-side) lines that an edit directly disturbs and from which the cone must be grown:
        /// <list type="bullet">
        /// <item>every <see cref="InstructionDiff.AddedNewLines"/> — an inserted line and the new side of a
        /// modification (an operand edit shows as removed-old + added-new);</item>
        /// <item>for every removed old line, the surviving new line that now <em>follows</em> the deletion —
        /// because the instructions downstream of a deleted one can change even though the deleted line
        /// itself has no new position. Found as the nearest matched old line after the deletion, mapped to
        /// its new index.</item>
        /// </list>
        /// A deletion at the very tail (nothing downstream survives) contributes no seed.
        /// </summary>
        public static IReadOnlySet<int> ChangedLineSeeds(InstructionDiff diff)
        {
            ArgumentNullException.ThrowIfNull(diff);
            var seeds = new HashSet<int>();

            foreach (int n in diff.AddedNewLines)
            {
                seeds.Add(n);
            }

            foreach (int removedOld in diff.RemovedOldLines)
            {
                int succ = NextMatchedNewLine(diff, removedOld);
                if (succ >= 0)
                {
                    seeds.Add(succ);
                }
            }
            return seeds;
        }

        /// <summary>The new-line index of the nearest old line AFTER <paramref name="removedOld"/> that
        /// survived the edit (is matched), or -1 if the deletion is at the tail.</summary>
        private static int NextMatchedNewLine(InstructionDiff diff, int removedOld)
        {
            for (int o = removedOld + 1; o < diff.Old.Count; o++)
            {
                if (diff.OldToNew.TryGetValue(o, out int n))
                {
                    return n;
                }
            }
            return -1;
        }

        /// <summary>
        /// The Tier-1 static cone for the given <paramref name="seeds"/> (typically
        /// <see cref="ChangedLineSeeds"/>): the union of each seed's forward CFG-reachable set
        /// (<see cref="StaticFlow.FutureLineNumbers"/>, which includes the seed itself). Lines NOT in the
        /// returned set are guaranteed unaffected by the edit and can reuse their cached result; lines IN it
        /// must be re-solved. A seed that is not a CFG vertex contributes only itself.
        /// </summary>
        public static IReadOnlySet<int> StaticCone(StaticFlow newFlow, IEnumerable<int> seeds)
        {
            ArgumentNullException.ThrowIfNull(newFlow);
            ArgumentNullException.ThrowIfNull(seeds);

            var cone = new HashSet<int>();
            foreach (int seed in seeds)
            {
                if (!newFlow.HasLine(seed))
                {
                    continue;
                }
                foreach (int reachable in newFlow.FutureLineNumbers(seed))
                {
                    cone.Add(reachable);
                }
                cone.Add(seed); // FutureLineNumbers includes the seed, but be explicit for an isolated vertex
            }
            return cone;
        }

        /// <summary>Convenience: the static cone of an edit, directly from its diff and the new CFG.</summary>
        public static IReadOnlySet<int> StaticCone(StaticFlow newFlow, InstructionDiff diff)
            => StaticCone(newFlow, ChangedLineSeeds(diff));

        /// <summary>
        /// True when the edit cannot have changed the CFG <em>shape</em>, so the narrow static cone is sound
        /// and a caller may re-solve only the cone and reuse the rest. The guard: every added/removed line is
        /// a plain (unlabeled, non-control-flow) instruction or inert.
        /// </summary>
        /// <remarks>
        /// Why a label or jump change breaks the static cone: removing/moving a label or retargeting a jump
        /// can <em>delete an in-edge</em> to some node, changing that node's merged value — yet the node is
        /// NOT forward-reachable from the edited line, so the forward cone misses it (unsound). Per
        /// INCREMENTAL_SIM_PLAN.md §7 the M2 fallback is a whole-(component) re-sim on any such edit.
        /// Conservative by design: editing a <em>labeled</em> instruction (e.g. <c>foo: mov…</c>) or any
        /// jump/call/ret forces the full path; a real old-vs-new CFG-edge diff (which would permit those) is
        /// an M3 refinement. NOTE: "no changed line carries a label" is what guarantees no label was
        /// added/removed/moved — a moved label necessarily makes its old and new lines both "changed".
        /// </remarks>
        public static bool IsTopologyPreserving(InstructionDiff diff)
        {
            ArgumentNullException.ThrowIfNull(diff);
            foreach (int n in diff.AddedNewLines)
            {
                if (DisturbsTopology(diff.New[n])) return false;
            }
            foreach (int o in diff.RemovedOldLines)
            {
                if (DisturbsTopology(diff.Old[o])) return false;
            }
            return true;
        }

        /// <summary>
        /// A sound static cone for a TOPOLOGY-CHANGING edit (label moved/added/removed, jump retargeted) — the
        /// relaxation of <see cref="IsTopologyPreserving"/>'s whole-sim fallback (INCREMENTAL_SIM_PLAN.md §7,
        /// "reachable in old ∪ new graph"). Seeds = changed instruction lines PLUS every new line whose set of
        /// incoming CFG edges DIFFERS from its old counterpart's (a merge point that gained/lost a predecessor,
        /// or whose predecessor was deleted), then forward CFG-reachability in the NEW graph.
        /// </summary>
        /// <remarks>
        /// Why this is sound: a line's value changes iff a CFG-ancestor instruction changed (caught by the
        /// changed-line seeds + forward reachability) OR its incoming-path structure changed. The latter shows
        /// up either as L's OWN in-edge set differing (L becomes a seed) or as an ancestor's in-edge set
        /// differing (that ancestor is a seed, and forward reachability covers L). A retargeted jump
        /// <c>jmp A → jmp B</c> seeds the jump (changed) AND A (lost an in-edge) AND B (gained one). Old edges
        /// are mapped into new-line space via <see cref="InstructionDiff.OldToNew"/>; a predecessor with no new
        /// counterpart (deleted) counts as a lost edge.
        /// </remarks>
        public static IReadOnlySet<int> StaticConeWithTopology(StaticFlow oldFlow, StaticFlow newFlow, InstructionDiff diff)
        {
            ArgumentNullException.ThrowIfNull(oldFlow);
            ArgumentNullException.ThrowIfNull(newFlow);
            ArgumentNullException.ThrowIfNull(diff);

            var seeds = new HashSet<int>(diff.AddedNewLines);

            int last = newFlow.LastLineNumber;
            for (int n = 0; n <= last; n++)
            {
                if (!newFlow.HasLine(n)) continue;
                if (!diff.NewToOld.TryGetValue(n, out int o)) continue; // added line: already a seed

                var newPreds = new HashSet<int>();
                foreach ((int p, bool _) in newFlow.Get_Prev_LineNumber(n)) newPreds.Add(p);

                var oldPredsMapped = new HashSet<int>();
                bool lostEdge = false;
                if (oldFlow.HasLine(o))
                {
                    foreach ((int p, bool _) in oldFlow.Get_Prev_LineNumber(o))
                    {
                        if (diff.OldToNew.TryGetValue(p, out int pn)) oldPredsMapped.Add(pn);
                        else lostEdge = true; // a predecessor was deleted ⇒ this in-edge is gone
                    }
                }

                if (lostEdge || !newPreds.SetEquals(oldPredsMapped)) seeds.Add(n);
            }

            return StaticCone(newFlow, seeds);
        }

        private static bool DisturbsTopology(Instruction ins)
        {
            if (ins.IsInert) return false;       // blank/comment — never a CFG vertex
            if (ins.Label.Length > 0) return true; // a label def/move/remove changes jump targets
            return IsControlFlow(ins.Mnemonic);
        }

        /// <summary>Mnemonics that create or terminate a CFG edge: the jumps/calls/loops
        /// (<see cref="AsmSourceTools.IsJump"/>) plus the path terminators (RET/IRET/INT/INTO/UD2).</summary>
        private static bool IsControlFlow(Mnemonic m)
            => AsmSourceTools.IsJump(m)
            || m is Mnemonic.RET or Mnemonic.IRET or Mnemonic.INT or Mnemonic.INTO or Mnemonic.UD2;
    }
}
