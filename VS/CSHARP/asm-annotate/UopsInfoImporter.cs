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

namespace asm_annotate
{
    using AsmTools;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// Converts the uops.info <c>instructions.xml</c> database into the 8-column TSV format
    /// consumed by <c>asm-dude2-ls-lib</c>'s <c>PerformanceStore</c>
    /// (Instruction, args, µOps-fused, µOps-unfused, ports, latency, throughput, remark).
    ///
    /// One TSV file is emitted per supported microarchitecture. The big XML (~140 MB) is read
    /// with a streaming <see cref="XmlReader"/>: only a single &lt;instruction&gt; subtree is held
    /// in memory at a time.
    ///
    /// Source: https://uops.info/instructions.xml  (machine-measured latency/throughput/port-usage,
    /// keyed by Intel XED iforms). See https://uops.info/xml.html for terms.
    /// </summary>
    public static class UopsInfoImporter
    {
        /// <summary>
        /// Maps a uops.info <c>architecture name=""</c> code to the output TSV file name
        /// (matching the existing <c>Resources/Performance/*.tsv</c> naming, which mirrors the
        /// <c>AsmTools.MicroArch</c> enum members).
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> ArchToFile = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CON"] = "Conroe",
            ["WOL"] = "Wolfdale",
            ["NHM"] = "Nehalem",
            ["WSM"] = "Westmere",
            ["SNB"] = "SandyBridge",
            ["IVB"] = "IvyBridge",
            ["HSW"] = "Haswell",
            ["BDW"] = "Broadwell",
            ["SKL"] = "Skylake",
            ["SKX"] = "SkylakeX",
            ["KBL"] = "Kabylake",
            ["CFL"] = "CoffeeLake",
            ["CNL"] = "Cannonlake",
            ["CLX"] = "CascadeLake",
            ["ICL"] = "Icelake",
            ["TGL"] = "Tigerlake",
            ["RKL"] = "RocketLake",
            ["EMR"] = "EmeraldRapids",
            ["BNL"] = "Bonnell",
            ["AMT"] = "Airmont",
            ["GLM"] = "Goldmont",
            ["GLP"] = "GoldmontPlus",
            ["TRM"] = "Tremont",
            ["ZEN2"] = "Zen2",
            ["ZEN3"] = "Zen3",
            ["ZEN4"] = "Zen4",
            ["ZEN5"] = "Zen5",
        };

        /// <summary>Human-readable long name for the ';' header comment in each generated file.</summary>
        private static readonly IReadOnlyDictionary<string, string> ArchLongName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CON"] = "Intel Conroe (Core 2)",
            ["WOL"] = "Intel Wolfdale (Core 2, 45nm)",
            ["NHM"] = "Intel Nehalem (1st gen Core)",
            ["WSM"] = "Intel Westmere (1st gen Core, 32nm)",
            ["SNB"] = "Intel Sandy Bridge (2nd gen Core)",
            ["IVB"] = "Intel Ivy Bridge (3rd gen Core)",
            ["HSW"] = "Intel Haswell (4th gen Core)",
            ["BDW"] = "Intel Broadwell (5th gen Core)",
            ["SKL"] = "Intel Skylake (6th gen Core)",
            ["SKX"] = "Intel Skylake-X / Skylake server",
            ["KBL"] = "Intel Kaby Lake (7th gen Core)",
            ["CFL"] = "Intel Coffee Lake (8th/9th gen Core)",
            ["CNL"] = "Intel Cannon Lake",
            ["CLX"] = "Intel Cascade Lake (Xeon)",
            ["ICL"] = "Intel Ice Lake (10th gen Core)",
            ["TGL"] = "Intel Tiger Lake (11th gen Core)",
            ["RKL"] = "Intel Rocket Lake (11th gen Core desktop)",
            ["EMR"] = "Intel Emerald Rapids (5th gen Xeon)",
            ["BNL"] = "Intel Bonnell (Atom)",
            ["AMT"] = "Intel Airmont (Atom)",
            ["GLM"] = "Intel Goldmont (Atom)",
            ["GLP"] = "Intel Goldmont Plus (Atom)",
            ["TRM"] = "Intel Tremont (Atom)",
            ["ZEN2"] = "AMD Zen 2",
            ["ZEN3"] = "AMD Zen 3",
            ["ZEN4"] = "AMD Zen 4",
            ["ZEN5"] = "AMD Zen 5",
        };

        /// <summary>
        /// Reads <paramref name="xmlPath"/> (uops.info instructions.xml) and writes one
        /// <c>&lt;arch&gt;.tsv</c> per supported microarchitecture into <paramref name="outputDir"/>.
        /// </summary>
        public static int Run(string xmlPath, string outputDir)
        {
            if (!File.Exists(xmlPath))
            {
                Console.WriteLine($"❌ uops.info XML not found: {xmlPath}");
                Console.WriteLine("   Download it with: curl -sSL -o instructions.xml https://uops.info/instructions.xml");
                return 1;
            }

            Directory.CreateDirectory(outputDir);

            // Open one writer per architecture and seed each with a ';' comment header.
            var writers = new Dictionary<string, StreamWriter>(StringComparer.Ordinal);
            var seen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // de-dup identical rows per file
            var rowCount = new Dictionary<string, int>(StringComparer.Ordinal);

            string? xmlDate = null;
            try
            {
                using (var probe = XmlReader.Create(xmlPath, new XmlReaderSettings { IgnoreWhitespace = true }))
                {
                    if (probe.MoveToContent() == XmlNodeType.Element)
                    {
                        xmlDate = probe.GetAttribute("date");
                    }
                }

                foreach (var (code, file) in ArchToFile)
                {
                    var w = new StreamWriter(Path.Combine(outputDir, file + ".tsv"));
                    w.WriteLine(";" + ArchLongName[code]);
                    w.WriteLine(";Instruction timings and µop breakdown, generated from uops.info instructions.xml" +
                                (xmlDate is null ? "" : " (" + xmlDate + ")") + ".");
                    w.WriteLine(";Columns: Instruction<TAB>operands<TAB>µOps-fused<TAB>µOps-unfused<TAB>ports<TAB>latency<TAB>throughput<TAB>remark");
                    writers[code] = w;
                    seen[code] = new HashSet<string>(StringComparer.Ordinal);
                    rowCount[code] = 0;
                }

                var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };
                using var reader = XmlReader.Create(xmlPath, settings);
                int instrCount = 0;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "instruction")
                    {
                        var instr = (XElement)XNode.ReadFrom(reader);
                        instrCount++;
                        ProcessInstruction(instr, writers, seen, rowCount);
                    }
                }

                Console.WriteLine($"Parsed {instrCount} instructions from {Path.GetFileName(xmlPath)}" +
                                  (xmlDate is null ? "" : $" (dated {xmlDate})") + ".");
                foreach (var (code, file) in ArchToFile)
                {
                    Console.WriteLine($"  {file,-12}.tsv : {rowCount[code]} rows");
                }
            }
            finally
            {
                foreach (var w in writers.Values)
                {
                    w.Flush();
                    w.Dispose();
                }
            }

            return 0;
        }

        private static void ProcessInstruction(
            XElement instr,
            Dictionary<string, StreamWriter> writers,
            Dictionary<string, HashSet<string>> seen,
            Dictionary<string, int> rowCount)
        {
            string iclass = instr.Attribute("iclass")?.Value ?? string.Empty;
            if (iclass.Length == 0)
            {
                return;
            }

            (string mnemonic, string note) = NormalizeMnemonic(iclass);
            string args = OperandsFromString(instr.Attribute("string")?.Value);
            string isaSet = instr.Attribute("isa-set")?.Value ?? string.Empty;
            string remark = note.Length == 0 ? isaSet : (isaSet.Length == 0 ? note : note + " " + isaSet);

            foreach (var arch in instr.Elements("architecture"))
            {
                string code = arch.Attribute("name")?.Value ?? string.Empty;
                if (!writers.TryGetValue(code, out var w))
                {
                    continue; // architecture we don't export
                }

                // Prefer the actual measurement over the IACA estimate.
                var m = arch.Element("measurement");
                if (m is null)
                {
                    continue;
                }

                string uops = m.Attribute("uops")?.Value ?? string.Empty;
                string fused = m.Attribute("uops_retire_slots")?.Value ?? uops; // retire-slots == fused domain
                string ports = m.Attribute("ports")?.Value ?? string.Empty;
                string tp = m.Attribute("TP_unrolled")?.Value ?? m.Attribute("TP_loop")?.Value ?? string.Empty;
                string latency = MaxLatency(m);

                // 8 columns, tab separated, matching PerformanceStore.AddData's column indices.
                string row = string.Join('\t', mnemonic, args, fused, uops, ports, latency, tp, remark);

                if (seen[code].Add(row))
                {
                    w.WriteLine(row);
                    rowCount[code]++;
                }
            }
        }

        /// <summary>
        /// uops.info disambiguation suffixes that the asmdude <see cref="Mnemonic"/> enum does not carry,
        /// paired with the remark note to record when one is stripped. Order matters only in that the
        /// first matching suffix wins. <c>_NEAR</c>/<c>_FAR</c> recover CALL/RET (which have no plain
        /// iclass at all); <c>_LOCK</c> recovers the locked RMW timings; <c>_XMM</c>/<c>_MMX</c>
        /// disambiguate the SSE/MMX forms from the same-named string operations.
        /// </summary>
        private static readonly (string suffix, string note)[] MnemonicSuffixes =
        [
            ("_LOCK", "lock"),
            ("_NEAR", ""),
            ("_FAR", "far"),
            ("_XMM", ""),
            ("_MMX", ""),
        ];

        /// <summary>
        /// Maps a uops.info <c>iclass</c> to a mnemonic the asmdude <see cref="Mnemonic"/> enum recognizes,
        /// returning the mnemonic string plus an optional remark note. If the iclass already parses it is
        /// used verbatim; otherwise a known disambiguation suffix is stripped and the base re-validated
        /// against <see cref="AsmTools.AsmSourceTools.ParseMnemonic"/>. If nothing parses, the original
        /// iclass is returned (the loader will warn and skip it).
        /// </summary>
        internal static (string mnemonic, string note) NormalizeMnemonic(string iclass)
        {
            if (AsmTools.AsmSourceTools.ParseMnemonic(iclass, false) != Mnemonic.NONE)
            {
                return (iclass, string.Empty);
            }

            foreach ((string suffix, string note) in MnemonicSuffixes)
            {
                if (iclass.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string baseName = iclass[..^suffix.Length];
                    if (AsmTools.AsmSourceTools.ParseMnemonic(baseName, false) != Mnemonic.NONE)
                    {
                        return (baseName, note);
                    }
                }
            }

            return (iclass, string.Empty);
        }

        /// <summary>
        /// Extracts the operand list from a uops.info <c>string</c> attribute, e.g.
        /// <c>"ADD (R32, R32)"</c> → <c>"R32, R32"</c>. Returns "" when there are no parentheses.
        /// </summary>
        internal static string OperandsFromString(string? str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            int open = str.IndexOf('(');
            int close = str.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                return str.Substring(open + 1, close - open - 1).Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the conventional single latency number for a measurement: the maximum of the
        /// plain <c>cycles</c> values across its &lt;latency&gt; children. The memory/address
        /// upper-bound variants (<c>cycles_mem</c>, <c>cycles_addr</c>, …) are intentionally ignored
        /// so the value matches the register-operand latency reported by Agner-style tables.
        /// Returns "" when no exact cycle count is available.
        /// </summary>
        internal static string MaxLatency(XElement measurement)
        {
            int max = -1;
            foreach (var lat in measurement.Elements("latency"))
            {
                string? c = lat.Attribute("cycles")?.Value;
                if (c is not null && int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > max)
                {
                    max = v;
                }
            }

            return max < 0 ? string.Empty : max.ToString(CultureInfo.InvariantCulture);
        }
    }
}
