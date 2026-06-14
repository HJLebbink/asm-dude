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
    /// One parsed source line reduced to the only fields that can change a simulation: its label, its
    /// mnemonic, and its (normalized) operands. Two instructions are equal iff all three match — so a
    /// pure line shift (a blank/comment inserted above) produces equal instructions that the diff can
    /// match across the renumber. This is the unit the <see cref="InstructionDiff"/> aligns; it is the
    /// "line-delta" that dissolves the line-key problem (INCREMENTAL_SIM_PLAN.md §6/§4).
    /// </summary>
    /// <remarks>
    /// A blank/comment/label-only line parses to <see cref="AsmTools.Mnemonic.NONE"/> (it carries no
    /// instruction). Such lines still appear in the sequence (so line indices stay 1:1 with the source),
    /// but a label-only line keeps its label so adding/removing a label is detected as a change (a topology
    /// edit the higher tiers must respect). KNOWN LIMITATION (sound over-invalidation): operands are
    /// compared as parsed text, so constant-fold equivalents (<c>1+1</c> vs <c>2</c>) and a jump whose
    /// target line moved but whose label text is unchanged read as "unchanged" here — topology handling
    /// lives in the cone tiers, not in this syntactic diff.
    /// </remarks>
    public readonly struct Instruction : IEquatable<Instruction>
    {
        public string Label { get; }

        public Mnemonic Mnemonic { get; }

        public IReadOnlyList<string> Args { get; }

        /// <summary>True when this line carries a real instruction (not a blank/comment/label-only line).</summary>
        public bool IsInstruction => this.Mnemonic != Mnemonic.NONE;

        /// <summary>True when this line is simulation-inert: a blank or comment-only line — no instruction
        /// AND no label. Such a line is not a CFG vertex and carries no value, so inserting/removing it
        /// cannot change any other line's simulation. A label-only line (<c>done:</c>) is NOT inert: it is a
        /// jump target, so adding/removing it changes topology. This distinction is the soundness gate for
        /// Tier-0 cache reuse (<see cref="InstructionDiff.HasOnlyInertChanges"/>).</summary>
        public bool IsInert => this.Mnemonic == Mnemonic.NONE && this.Label.Length == 0;

        public Instruction(string label, Mnemonic mnemonic, IReadOnlyList<string> args)
        {
            this.Label = label ?? string.Empty;
            this.Mnemonic = mnemonic;
            this.Args = args ?? Array.Empty<string>();
        }

        /// <summary>Parse one source line into an <see cref="Instruction"/> using the same parser the
        /// simulator and <see cref="StaticFlow"/> use, so the diff aligns exactly the lines the sim treats
        /// as instructions.</summary>
        public static Instruction Parse(string sourceLine)
        {
            (_, string label, Mnemonic mnemonic, string[] args, _) =
                AsmSourceTools.ParseLine(sourceLine ?? string.Empty, -1, -1, AssemblerEnum.UNKNOWN);
            return new Instruction(label, mnemonic, args);
        }

        /// <summary>Parse a whole program (one entry per source line, indices preserved).</summary>
        public static IReadOnlyList<Instruction> ParseProgram(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            var result = new Instruction[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                result[i] = Parse(lines[i]);
            }
            return result;
        }

        // Equality is field-wise (label + mnemonic + ordered operands) — exact, with no separator-string to
        // inject into. The operand comparison is ordinal and order-sensitive.
        public bool Equals(Instruction other)
        {
            if (this.Mnemonic != other.Mnemonic) return false;
            if (!string.Equals(this.Label, other.Label, StringComparison.Ordinal)) return false;
            if (this.Args.Count != other.Args.Count) return false;
            for (int i = 0; i < this.Args.Count; i++)
            {
                if (!string.Equals(this.Args[i], other.Args[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is Instruction other && this.Equals(other);

        public override int GetHashCode()
        {
            // Mnemonic + label + operand COUNT only: cheap and stable, and equal instructions always
            // collide (per-operand content is left out so the hash stays O(1); Equals does the exact check).
            var hash = new HashCode();
            hash.Add((int)this.Mnemonic);
            hash.Add(this.Label, StringComparer.Ordinal);
            hash.Add(this.Args.Count);
            return hash.ToHashCode();
        }

        public override string ToString()
            => this.Args.Count == 0
                ? $"{this.Label}|{this.Mnemonic}"
                : $"{this.Label}|{this.Mnemonic} {string.Join(",", this.Args)}";
    }

    /// <summary>
    /// A line-level alignment of two parsed programs (the previous sim input vs the new edit), via the
    /// Longest-Common-Subsequence of their <see cref="Instruction"/> sequences. It answers the questions
    /// the incremental pipeline (INCREMENTAL_SIM_PLAN.md §4) asks of an edit:
    /// <list type="bullet">
    /// <item><see cref="NewToOld"/> — for each unchanged new line, the old line it maps from (so cached
    /// strings can be remapped through the renumber).</item>
    /// <item><see cref="AddedNewLines"/>/<see cref="RemovedOldLines"/> — the edit script.</item>
    /// <item><see cref="HasNoInstructionChange"/> — Tier 0: no instruction was added/removed/changed, so
    /// the whole previous result can be reused (only blank/comment/label-less shifts happened).</item>
    /// </list>
    /// Pure and Z3-free; O(n·m) DP over the line count (documents are capped at a few hundred lines).
    /// </summary>
    public sealed class InstructionDiff
    {
        public IReadOnlyList<Instruction> Old { get; }

        public IReadOnlyList<Instruction> New { get; }

        /// <summary>new line index → old line index, for lines that are equal AND on the matched
        /// subsequence (i.e. genuinely the same instruction, just possibly renumbered).</summary>
        public IReadOnlyDictionary<int, int> NewToOld { get; }

        /// <summary>old line index → new line index (the inverse of <see cref="NewToOld"/>).</summary>
        public IReadOnlyDictionary<int, int> OldToNew { get; }

        /// <summary>New lines with no match: insertions and the "new" side of a modification, sorted.</summary>
        public IReadOnlyList<int> AddedNewLines { get; }

        /// <summary>Old lines with no match: deletions and the "old" side of a modification, sorted.</summary>
        public IReadOnlyList<int> RemovedOldLines { get; }

        /// <summary>No <em>instruction</em> line was added, removed, or changed — every real instruction
        /// matched 1:1 (only <see cref="Mnemonic.NONE"/> lines shifted). Weaker than
        /// <see cref="HasOnlyInertChanges"/>: a label-only line may still have been added/removed.</summary>
        public bool HasNoInstructionChange { get; }

        /// <summary>Tier-0 reuse gate: every added and removed line is <see cref="Instruction.IsInert"/>
        /// (blank/comment), so neither the CFG nor any instruction's parsed form changed — only inert lines
        /// shifted. When true, the previous per-line simulation is valid for the new text after remapping
        /// line numbers through <see cref="NewToOld"/>, and NO Z3 is needed. Implies
        /// <see cref="HasNoInstructionChange"/> and additionally excludes label add/remove (a topology edit).</summary>
        public bool HasOnlyInertChanges { get; }

        private InstructionDiff(
            IReadOnlyList<Instruction> oldInstr,
            IReadOnlyList<Instruction> newInstr,
            Dictionary<int, int> newToOld,
            Dictionary<int, int> oldToNew,
            List<int> addedNew,
            List<int> removedOld,
            bool noInstructionChange,
            bool onlyInertChanges)
        {
            this.Old = oldInstr;
            this.New = newInstr;
            this.NewToOld = newToOld;
            this.OldToNew = oldToNew;
            this.AddedNewLines = addedNew;
            this.RemovedOldLines = removedOld;
            this.HasNoInstructionChange = noInstructionChange;
            this.HasOnlyInertChanges = onlyInertChanges;
        }

        public static InstructionDiff Compute(IReadOnlyList<Instruction> oldInstr, IReadOnlyList<Instruction> newInstr)
        {
            ArgumentNullException.ThrowIfNull(oldInstr);
            ArgumentNullException.ThrowIfNull(newInstr);

            int n = oldInstr.Count;
            int m = newInstr.Count;

            // LCS DP table: lcs[i,j] = LCS length of oldInstr[i..] and newInstr[j..].
            // (Suffix form so the backtrack walks forward, producing ascending matched indices.)
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    lcs[i, j] = oldInstr[i].Equals(newInstr[j])
                        ? lcs[i + 1, j + 1] + 1
                        : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }

            var newToOld = new Dictionary<int, int>();
            var oldToNew = new Dictionary<int, int>();
            {
                int i = 0, j = 0;
                while (i < n && j < m)
                {
                    if (oldInstr[i].Equals(newInstr[j]))
                    {
                        newToOld[j] = i;
                        oldToNew[i] = j;
                        i++;
                        j++;
                    }
                    else if (lcs[i + 1, j] >= lcs[i, j + 1])
                    {
                        i++; // old[i] is removed
                    }
                    else
                    {
                        j++; // new[j] is added
                    }
                }
            }

            var addedNew = new List<int>();
            bool added_instruction = false;
            bool added_noninert = false;
            for (int j = 0; j < m; j++)
            {
                if (!newToOld.ContainsKey(j))
                {
                    addedNew.Add(j);
                    if (newInstr[j].IsInstruction) added_instruction = true;
                    if (!newInstr[j].IsInert) added_noninert = true;
                }
            }

            var removedOld = new List<int>();
            bool removed_instruction = false;
            bool removed_noninert = false;
            for (int i = 0; i < n; i++)
            {
                if (!oldToNew.ContainsKey(i))
                {
                    removedOld.Add(i);
                    if (oldInstr[i].IsInstruction) removed_instruction = true;
                    if (!oldInstr[i].IsInert) removed_noninert = true;
                }
            }

            bool noInstructionChange = !added_instruction && !removed_instruction;
            bool onlyInertChanges = !added_noninert && !removed_noninert;
            return new InstructionDiff(oldInstr, newInstr, newToOld, oldToNew, addedNew, removedOld, noInstructionChange, onlyInertChanges);
        }
    }
}
