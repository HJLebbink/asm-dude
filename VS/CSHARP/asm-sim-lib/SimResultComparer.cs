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
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// The per-line simulation output for one document line, in a producer-agnostic form: the full
    /// register/flag state before and after the line (hover), the CodeLens read/write labels, and the
    /// sorted diagnostic strings. Any producer — the linear editor sim, the future per-component
    /// DynamicFlow engine, a different Z3 version, a different RNG seed, or a different timeout — emits
    /// the same shape, so two of them are directly comparable.
    /// </summary>
    public sealed record SimLineResult(
        string? BeforeState,
        string? AfterState,
        string? ReadLabel,
        string? WriteLabel,
        IReadOnlyList<string>? Diagnostics = null);

    /// <summary>
    /// A whole-document simulation result: line number → <see cref="SimLineResult"/>, plus a
    /// human-readable <see cref="Label"/> (e.g. "linear", "component", "seed=1", "z3-4.13") used in diff
    /// reports. This is the producer-agnostic unit the <see cref="SimResultComparer"/> compares.
    /// </summary>
    public sealed class SimResultSet
    {
        public string Label { get; }

        public IReadOnlyDictionary<int, SimLineResult> Lines { get; }

        public SimResultSet(string label, IReadOnlyDictionary<int, SimLineResult> lines)
        {
            this.Label = label ?? string.Empty;
            this.Lines = lines ?? new Dictionary<int, SimLineResult>();
        }
    }

    /// <summary>Why a single <see cref="SimDiffEntry"/> exists.</summary>
    public enum SimDiffKind
    {
        /// <summary>The line is present in the first result set but absent from the second.</summary>
        LineOnlyInA,

        /// <summary>The line is present in the second result set but absent from the first.</summary>
        LineOnlyInB,

        /// <summary>Both result sets have the line, but one field differs.</summary>
        FieldDiffers,
    }

    /// <summary>One difference between two result sets: a line, what kind, and (for a field diff) which
    /// field and the two values.</summary>
    public sealed record SimDiffEntry(int Line, SimDiffKind Kind, string Field, string? ValueA, string? ValueB)
    {
        public override string ToString() => this.Kind switch
        {
            SimDiffKind.LineOnlyInA => $"line {this.Line}: only in A",
            SimDiffKind.LineOnlyInB => $"line {this.Line}: only in B",
            _ => $"line {this.Line} [{this.Field}]: A={Fmt(this.ValueA)} B={Fmt(this.ValueB)}",
        };

        private static string Fmt(string? v) => v is null ? "<none>" : "'" + v + "'";
    }

    /// <summary>The result of comparing two <see cref="SimResultSet"/>s: the (possibly empty) list of
    /// differences, plus convenience accessors for tests and for the <c>SIMDIFF</c> log.</summary>
    public sealed class SimDiff
    {
        public string LabelA { get; }

        public string LabelB { get; }

        public IReadOnlyList<SimDiffEntry> Entries { get; }

        public SimDiff(string labelA, string labelB, IReadOnlyList<SimDiffEntry> entries)
        {
            this.LabelA = labelA;
            this.LabelB = labelB;
            this.Entries = entries;
        }

        /// <summary>True when the two result sets are identical (no differences).</summary>
        public bool IsEmpty => this.Entries.Count == 0;

        /// <summary>Lines that differ in any way (handy for "N lines changed" summaries).</summary>
        public IReadOnlyCollection<int> ChangedLines
        {
            get
            {
                var set = new SortedSet<int>();
                foreach (SimDiffEntry e in this.Entries) set.Add(e.Line);
                return set;
            }
        }

        /// <summary>A multi-line, human-readable report — for a test failure message or the SIMDIFF log.</summary>
        public string ToReport()
        {
            if (this.IsEmpty) return $"SimDiff({this.LabelA} vs {this.LabelB}): identical";

            var sb = new StringBuilder();
            sb.Append($"SimDiff({this.LabelA} vs {this.LabelB}): {this.Entries.Count} difference(s) over {this.ChangedLines.Count} line(s)");
            foreach (SimDiffEntry e in this.Entries)
            {
                sb.Append(Environment.NewLine).Append("  ").Append(e.ToString());
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Standalone, producer-agnostic differential oracle for the simulator. Compares two
    /// <see cref="SimResultSet"/>s line-by-line, field-by-field, and reports every difference.
    ///
    /// <para>It has no Z3, no LSP, and no knowledge of who produced the inputs, so it serves many uses,
    /// not only the per-component engine swap (INCREMENTAL_SIM_PLAN.md S0 shadow):</para>
    /// <list type="bullet">
    ///   <item>old (linear) vs new (component) engine — the refactor correctness gate;</item>
    ///   <item>same engine run twice / two RNG seeds — determinism &amp; flakiness detection;</item>
    ///   <item>timeout=5000 vs timeout=1000 — precision-vs-speed sensitivity data;</item>
    ///   <item>old Z3 vs new Z3 — validate a Z3 upgrade;</item>
    ///   <item>baseline vs optimized — prove an optimization is output-equivalent;</item>
    ///   <item>a differential fuzzing oracle for asm-fuzz.</item>
    /// </list>
    /// Pure and total: deterministic, side-effect-free, never throws on any input.
    /// </summary>
    public static class SimResultComparer
    {
        /// <summary>Compare two result sets and return every per-line, per-field difference.</summary>
        public static SimDiff Compare(SimResultSet a, SimResultSet b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            var entries = new List<SimDiffEntry>();

            var allLines = new SortedSet<int>();
            foreach (int k in a.Lines.Keys) allLines.Add(k);
            foreach (int k in b.Lines.Keys) allLines.Add(k);

            foreach (int line in allLines)
            {
                bool inA = a.Lines.TryGetValue(line, out SimLineResult? la);
                bool inB = b.Lines.TryGetValue(line, out SimLineResult? lb);

                if (inA && !inB)
                {
                    entries.Add(new SimDiffEntry(line, SimDiffKind.LineOnlyInA, "*", null, null));
                    continue;
                }
                if (!inA && inB)
                {
                    entries.Add(new SimDiffEntry(line, SimDiffKind.LineOnlyInB, "*", null, null));
                    continue;
                }

                CompareField(entries, line, "before", la!.BeforeState, lb!.BeforeState);
                CompareField(entries, line, "after", la.AfterState, lb.AfterState);
                CompareField(entries, line, "read", la.ReadLabel, lb.ReadLabel);
                CompareField(entries, line, "write", la.WriteLabel, lb.WriteLabel);
                CompareDiagnostics(entries, line, la.Diagnostics, lb.Diagnostics);
            }

            return new SimDiff(a.Label, b.Label, entries);
        }

        private static void CompareField(List<SimDiffEntry> entries, int line, string field, string? va, string? vb)
        {
            // Treat null and empty string as the same "no value" (a producer may omit vs emit "").
            if (!string.Equals(va ?? string.Empty, vb ?? string.Empty, StringComparison.Ordinal))
            {
                entries.Add(new SimDiffEntry(line, SimDiffKind.FieldDiffers, field, va, vb));
            }
        }

        private static void CompareDiagnostics(List<SimDiffEntry> entries, int line, IReadOnlyList<string>? da, IReadOnlyList<string>? db)
        {
            string a = Join(da);
            string b = Join(db);
            if (!string.Equals(a, b, StringComparison.Ordinal))
            {
                entries.Add(new SimDiffEntry(line, SimDiffKind.FieldDiffers, "diag", a.Length == 0 ? null : a, b.Length == 0 ? null : b));
            }
        }

        // Order-independent join: a producer's diagnostic ordering is not semantically meaningful.
        private static string Join(IReadOnlyList<string>? items)
        {
            if (items is null || items.Count == 0) return string.Empty;
            var sorted = new List<string>(items);
            sorted.Sort(StringComparer.Ordinal);
            return string.Join(" | ", sorted);
        }
    }
}
