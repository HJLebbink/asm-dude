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
    using AsmTools;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Presentation helpers for the (now per-operand-form, uops.info-sourced) performance data.
    ///
    /// uops.info splits every instruction into one row per operand form, so a single mnemonic such as
    /// ADD has dozens of <see cref="PerformanceItem"/>s on each microarchitecture. Two helpers tame that:
    ///   • <see cref="SelectBestMatch"/> — picks the row whose operand shape matches the operands actually
    ///     written on the code line (for the inlay-hint latency badge).
    ///   • <see cref="CollapseByTiming"/> — merges rows that share identical timing within a
    ///     microarchitecture (for the hover table).
    /// </summary>
    internal static class PerformanceDisplay
    {
        /// <summary>
        /// Renders the hover performance table for one mnemonic, including its <c>Performance:</c> title.
        /// The instruction column (operand form) and every numeric column are sized to the WIDEST cell
        /// actually present, so the headers and every data row line up regardless of how long an operand
        /// form is. The previous fixed-width format (a hard-coded 26-char instruction column) broke
        /// alignment for AVX-512 forms whose <c>instr + args</c> overflowed 26 characters, shoving the
        /// µOps / latency / throughput values past their headers and making them ragged from row to row.
        ///
        /// Layout:
        /// <code>
        /// Performance:         µOps   µOps     µOps
        ///   Skylake            Fused  Unfused  Port              Latency  Throughput
        ///   MOV AX, Moffs16    1      2        1*p0156+1*p23     3        1.00        I86
        /// </code>
        /// The first column packs the section title / microarchitecture / instruction: <c>Performance:</c>
        /// shares the line with the <c>µOps</c> spanner, the microarchitecture shares the line with the
        /// <c>Fused/Unfused/Port/Latency/Throughput</c> labels, and the per-form rows (indented two spaces
        /// under the arch) follow. Columns are left-aligned, separated by two spaces, with the raw remark
        /// appended. A second microarchitecture repeats the label line (blank-line separated) with its own
        /// rows; the <c>Performance:</c>/<c>µOps</c> spanner is shown once at the top. Returns the empty
        /// string when there are no items (caller then shows no performance section).
        /// </summary>
        public static string BuildPerformanceTable(IEnumerable<(PerformanceItem item, int formCount)> collapsed)
        {
            List<(PerformanceItem item, int formCount)> list = collapsed as List<(PerformanceItem item, int formCount)> ?? [.. collapsed];
            if (list.Count == 0)
            {
                return string.Empty;
            }

            // Columns 0..5 are padded; the remark (row[6]) is appended raw after them. Column 0 carries the
            // title ("Performance:") on the spanner line, the arch on the label line, and the (two-space
            // indented) instruction on data lines.
            const int ColCount = 6;
            const string Indent = "  ";
            string[] spannerCells = ["Performance:", "µOps", "µOps", "µOps", string.Empty, string.Empty];
            string[] labelCells = [string.Empty, "Fused", "Unfused", "Port", "Latency", "Throughput"];

            List<string[]> rows = [];
            foreach ((PerformanceItem item, int formCount) in list)
            {
                string instr = item.instr_ + " " + item.args_ + (formCount > 1 ? $" (+{formCount - 1})" : string.Empty);
                rows.Add([
                    Indent + instr,
                    item.mu_Ops_Fused_ ?? string.Empty,
                    item.mu_Ops_Merged_ ?? string.Empty,
                    item.mu_Ops_Port_ ?? string.Empty,
                    item.latency_ ?? string.Empty,
                    item.throughput_ ?? string.Empty,
                    item.remark_ ?? string.Empty,
                ]);
            }

            // Each column is as wide as the widest of its header label and every data cell.
            int[] width = new int[ColCount];
            for (int c = 0; c < ColCount; c++)
            {
                width[c] = Math.Max(spannerCells[c].Length, labelCells[c].Length);
                foreach (string[] row in rows)
                {
                    width[c] = Math.Max(width[c], row[c].Length);
                }
            }

            // Column 0 also carries each "  <arch>" label line, so it must fit the widest arch name.
            foreach ((PerformanceItem item, int _) in list)
            {
                width[0] = Math.Max(width[0], Indent.Length + item.microArch_.ToString().Length);
            }

            StringBuilder sb = new();
            AppendRow(sb, spannerCells, null, width);

            MicroArch currentArch = MicroArch.NONE;
            bool firstArch = true;
            for (int r = 0; r < list.Count; r++)
            {
                MicroArch arch = list[r].item.microArch_;
                if (arch != currentArch)
                {
                    currentArch = arch;

                    // The arch label line directly follows the spanner for the first arch; later arches are
                    // separated by a blank line.
                    sb.Append(firstArch ? "\n" : "\n\n");
                    firstArch = false;
                    string[] labelLine = [Indent + currentArch, "Fused", "Unfused", "Port", "Latency", "Throughput"];
                    AppendRow(sb, labelLine, null, width);
                }

                sb.Append('\n');
                AppendRow(sb, rows[r], rows[r][6], width);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Appends one table line: each of the six padded columns (left-aligned to <paramref name="width"/>,
        /// separated by two spaces) followed by the optional raw <paramref name="remark"/>. Trailing
        /// whitespace is trimmed so header/empty cells don't leave a ragged right edge.
        /// </summary>
        private static void AppendRow(StringBuilder sb, string[] cells, string? remark, int[] width)
        {
            StringBuilder line = new();
            for (int c = 0; c < width.Length; c++)
            {
                line.Append(cells[c].PadRight(width[c])).Append("  ");
            }

            if (!string.IsNullOrEmpty(remark))
            {
                line.Append(remark);
            }

            int end = line.Length;
            while (end > 0 && line[end - 1] == ' ')
            {
                end--;
            }

            sb.Append(line.ToString(0, end));
        }

        private enum OpCat { Reg, Mem, Imm, Other }

        private enum RegClass { None, Gpr, Simd, Mmx, Mask, Other }

        private readonly record struct OpShape(OpCat Cat, int Width, RegClass Class);

        /// <summary>Incompatible-position sentinel; any candidate scoring this is rejected.</summary>
        private const int Incompatible = -1000;

        /// <summary>
        /// Selects the <see cref="PerformanceItem"/> whose operand form best matches
        /// <paramref name="lineOperands"/> (the operands parsed from the source line). Falls back to the
        /// first item when no operand-count-compatible row exists (e.g. masked AVX-512, where the line's
        /// <c>{k}</c> decorator and uops' separate K operand make the counts differ). Returns
        /// <c>null</c> only when <paramref name="items"/> is empty.
        /// </summary>
        public static PerformanceItem? SelectBestMatch(IEnumerable<PerformanceItem> items, IReadOnlyList<string> lineOperands)
        {
            List<PerformanceItem> list = items as List<PerformanceItem> ?? [.. items];
            if (list.Count == 0)
            {
                return null;
            }

            List<OpShape> lineShapes = [];
            foreach (string op in lineOperands)
            {
                if (!string.IsNullOrWhiteSpace(op))
                {
                    // Drop EVEX decorators ({k}/{z}/{sae}/{1toN}); the write-mask is folded into the
                    // destination on the line but is a separate "K" operand in the uops form (aligned below).
                    lineShapes.Add(ClassifyLineOperand(StripDecorators(op)));
                }
            }

            bool lineMasked = lineOperands.Any(HasWriteMask);

            PerformanceItem? best = null;
            int bestScore = int.MinValue;
            foreach (PerformanceItem item in list)
            {
                List<string> candTokens = SplitArgs(item.args_);

                // A maskable form carries the write-mask as a separate "K" immediately after the
                // destination (index 1). The line attaches it as a {k} decorator we already stripped, so
                // drop that one K to line the counts up — but only when it is the single extra token, so a
                // genuine mask-register operand (e.g. the destination of VPCMPD/KANDW) is preserved.
                bool candMasked = false;
                if (candTokens.Count == lineShapes.Count + 1 && candTokens.Count > 1 && candTokens[1] == "K")
                {
                    candTokens.RemoveAt(1);
                    candMasked = true;
                }

                if (candTokens.Count != lineShapes.Count)
                {
                    continue;
                }

                int score = 0;
                bool ok = true;
                for (int i = 0; i < candTokens.Count; i++)
                {
                    int s = ScorePosition(lineShapes[i], ClassifyUopsToken(candTokens[i]));
                    if (s <= Incompatible)
                    {
                        ok = false;
                        break;
                    }

                    score += s;
                }

                if (!ok)
                {
                    continue;
                }

                // Prefer the form whose maskedness matches the line (masked line → masked form). Timing is
                // usually identical, but this makes the chosen row reflect what was actually written.
                if (candMasked == lineMasked)
                {
                    score++;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }

            return best ?? list[0];
        }

        /// <summary>
        /// Collapses items that share identical timing — (microarch, µOps fused/unfused, ports, latency,
        /// throughput) — into a single representative, preserving first-seen order. The returned
        /// <c>formCount</c> is how many operand forms folded into that representative (1 = unique).
        /// </summary>
        public static IEnumerable<(PerformanceItem item, int formCount)> CollapseByTiming(IEnumerable<PerformanceItem> items)
        {
            List<(PerformanceItem rep, int count)> groups = [];
            Dictionary<string, int> index = new(StringComparer.Ordinal);

            foreach (PerformanceItem it in items)
            {
                string key = string.Join(
                    '|',
                    (int)it.microArch_,
                    it.mu_Ops_Fused_,
                    it.mu_Ops_Merged_,
                    it.mu_Ops_Port_,
                    it.latency_,
                    it.throughput_);

                if (index.TryGetValue(key, out int gi))
                {
                    groups[gi] = (groups[gi].rep, groups[gi].count + 1);
                }
                else
                {
                    index[key] = groups.Count;
                    groups.Add((it, 1));
                }
            }

            return groups;
        }

        #region operand classification

        private static OpShape ClassifyLineOperand(string op)
        {
            string s = op.Trim();
            if (s.Length == 0)
            {
                return new OpShape(OpCat.Other, -1, RegClass.None);
            }

            (bool valid, Rn reg, int nBits) = RegisterTools.ToRn(s, false);
            if (valid)
            {
                return new OpShape(OpCat.Reg, nBits, ClassOf(reg));
            }

            if (s.Contains('['))
            {
                return new OpShape(OpCat.Mem, MemSizeKeyword(s), RegClass.None);
            }

            (bool valid2, _, int immBits) = AsmSourceTools.Evaluate_Constant(s, false);
            if (valid2)
            {
                return new OpShape(OpCat.Imm, immBits, RegClass.None);
            }

            return new OpShape(OpCat.Other, -1, RegClass.None);
        }

        /// <summary>
        /// Classifies a uops.info operand placeholder token such as <c>R32</c>, <c>M128</c>, <c>I8</c>,
        /// <c>XMM</c>, <c>AL</c>, <c>K</c>, or <c>0</c> (immzero) / <c>1</c> (shift-by-one).
        /// </summary>
        private static OpShape ClassifyUopsToken(string token)
        {
            string t = token.Trim();
            if (t.Length == 0)
            {
                return new OpShape(OpCat.Other, -1, RegClass.None);
            }

            if (t is "0" or "1")
            {
                return new OpShape(OpCat.Imm, 8, RegClass.None);
            }

            char c0 = t[0];

            if (c0 == 'I' && t.Length > 1 && char.IsDigit(t[1]))
            {
                return new OpShape(OpCat.Imm, LeadingWidth(t.AsSpan(1)), RegClass.None);
            }

            if (c0 == 'M' && t.StartsWith("Moffs", StringComparison.Ordinal))
            {
                return new OpShape(OpCat.Mem, LeadingWidth(t.AsSpan(5)), RegClass.None);
            }

            if (c0 == 'M' && t.Length > 1 && char.IsDigit(t[1]))
            {
                return new OpShape(OpCat.Mem, LeadingWidth(t.AsSpan(1)), RegClass.None);
            }

            if (c0 == 'R' && t.Length > 1 && char.IsDigit(t[1]))
            {
                return new OpShape(OpCat.Reg, LeadingWidth(t.AsSpan(1)), RegClass.Gpr);
            }

            switch (t)
            {
                case "XMM": return new OpShape(OpCat.Reg, 128, RegClass.Simd);
                case "YMM": return new OpShape(OpCat.Reg, 256, RegClass.Simd);
                case "ZMM": return new OpShape(OpCat.Reg, 512, RegClass.Simd);
                case "MM": return new OpShape(OpCat.Reg, 64, RegClass.Mmx);
                case "K": return new OpShape(OpCat.Reg, -1, RegClass.Mask);
            }

            // Named registers (AL, AX, EAX, RAX, CL, ...).
            (bool valid, Rn reg, int nBits) = RegisterTools.ToRn(t, false);
            if (valid)
            {
                return new OpShape(OpCat.Reg, nBits, ClassOf(reg));
            }

            return new OpShape(OpCat.Other, -1, RegClass.None);
        }

        private static int ScorePosition(OpShape line, OpShape cand)
        {
            if (line.Cat != cand.Cat)
            {
                return Incompatible;
            }

            int score = 1; // category match
            switch (line.Cat)
            {
                case OpCat.Reg:
                    // Wrong register file (e.g. GPR where the form wants XMM) is a hard mismatch.
                    if (line.Class != RegClass.Other && cand.Class != RegClass.Other &&
                        line.Class != RegClass.None && cand.Class != RegClass.None &&
                        line.Class != cand.Class)
                    {
                        return Incompatible;
                    }

                    if (line.Width > 0 && cand.Width > 0 && line.Width == cand.Width)
                    {
                        score += 2;
                    }

                    break;

                case OpCat.Imm:
                case OpCat.Mem:
                    if (line.Width > 0 && cand.Width > 0 && line.Width == cand.Width)
                    {
                        score += 1;
                    }

                    break;
            }

            return score;
        }

        private static RegClass ClassOf(Rn reg)
        {
            if (RegisterTools.IsGeneralPurposeRegister(reg))
            {
                return RegClass.Gpr;
            }

            if (RegisterTools.Is_SIMD_Register(reg))
            {
                return RegClass.Simd;
            }

            if (RegisterTools.IsOpmaskRegister(reg))
            {
                return RegClass.Mask;
            }

            if (RegisterTools.IsMmxRegister(reg))
            {
                return RegClass.Mmx;
            }

            return RegClass.Other;
        }

        /// <summary>Reads the run of leading digits as a bit width, or -1 when absent.</summary>
        private static int LeadingWidth(ReadOnlySpan<char> s)
        {
            int i = 0;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                i++;
            }

            return (i > 0 && int.TryParse(s[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)) ? w : -1;
        }

        /// <summary>Bit width implied by a MASM/NASM size keyword in a memory operand, or -1 if none.</summary>
        private static int MemSizeKeyword(string op)
        {
            string s = op.ToUpperInvariant();
            if (s.Contains("ZMMWORD")) return 512;
            if (s.Contains("YMMWORD")) return 256;
            if (s.Contains("XMMWORD") || s.Contains("OWORD")) return 128;
            if (s.Contains("QWORD")) return 64;
            if (s.Contains("DWORD")) return 32;
            if (s.Contains("WORD")) return 16;
            if (s.Contains("BYTE")) return 8;
            return -1;
        }

        /// <summary>
        /// Removes EVEX decorators — write-mask <c>{k}</c>, zeroing <c>{z}</c>, rounding/SAE
        /// <c>{rn-sae}</c>, and broadcast <c>{1toN}</c> — from a source operand (e.g. <c>zmm0{k1}{z}</c>
        /// → <c>zmm0</c>) so it classifies by its base register/memory token. Handles multiple braces.
        /// </summary>
        private static string StripDecorators(string op)
        {
            if (op.IndexOf('{') < 0)
            {
                return op;
            }

            System.Text.StringBuilder sb = new(op.Length);
            int depth = 0;
            foreach (char c in op)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (depth == 0)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>True if any operand carries an EVEX write-mask decorator <c>{k0}</c>..<c>{k7}</c>
        /// (distinct from zeroing <c>{z}</c>, broadcast <c>{1toN}</c>, or rounding <c>{rn-sae}</c>).</summary>
        private static bool HasWriteMask(string op)
        {
            if (string.IsNullOrEmpty(op))
            {
                return false;
            }

            int i = op.IndexOf('{');
            while (i >= 0)
            {
                if (i + 2 < op.Length && (op[i + 1] is 'k' or 'K') && char.IsDigit(op[i + 2]))
                {
                    return true;
                }

                i = op.IndexOf('{', i + 1);
            }

            return false;
        }

        private static List<string> SplitArgs(string args)
        {
            List<string> result = [];
            if (string.IsNullOrWhiteSpace(args))
            {
                return result;
            }

            foreach (string part in args.Split(','))
            {
                string p = part.Trim();
                if (p.Length > 0)
                {
                    result.Add(p);
                }
            }

            return result;
        }

        #endregion
    }
}
