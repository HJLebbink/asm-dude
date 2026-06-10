// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace asm_annotate
{
    using AsmTools;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Stage 2 of the instruction-data pipeline (md→txt): loads the AsmDude wiki's HTML opcode tables
    /// (produced from the Intel SDM by stage 1, <see cref="Extractor"/>) and turns them into the AsmDude
    /// signature file (<c>signature-mar2026.txt</c>) + <c>overview.txt</c> + the wiki <c>Home.md</c>.
    /// Invoked via the <c>gen-signatures</c> command. Previously the standalone <c>intel-doc-2-data</c>
    /// project; folded into asm-annotate so the whole data toolchain (extract / gen-signatures / perf-uops)
    /// lives in one tool.
    /// </summary>
    internal static class SignatureGenerator
    {
        /// <summary>
        /// Reads every <c>*.md</c> in <paramref name="wikiDir"/> and writes the signature file to
        /// <paramref name="outFile"/> (plus <c>overview.txt</c> beside it and the wiki <c>Home.md</c>).
        /// </summary>
        public static int Run(string wikiDir, string outFile)
        {
            DateTime startTime = DateTime.Now;

            if (!Payload(wikiDir, outFile))
            {
                return 1;
            }

            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            Console.WriteLine("Elapsed time " + elapsedSec + " sec");
            return 0;
        }

        private static bool Payload(string path, string outFile)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Could not find directory \"" + path + "\".");
                return false;
            }

            StringBuilder sb = new();
            StringBuilder sb2 = new();

            IDictionary<Arch, ISet<Mnemonic>> dictionary = new Dictionary<Arch, ISet<Mnemonic>>();

            sb2.AppendLine("<table>");

            foreach (string filename in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f))
            {
                StreamReader file_Stream = File.OpenText(filename);
                string file_Content = file_Stream.ReadToEnd();
                (string Description, IList<Signature> Signatures) = Parse(file_Content);
                file_Stream.Close();
                // NOTE: the GENERAL/title line keeps the FULL description (no SP/FP abbreviation);
                // only the per-form signature rows are abbreviated (in To_Signature) to stay compact.

                sb.AppendLine(";--------------------------------------------------------");

                ISet<Mnemonic> mnemonics = new HashSet<Mnemonic>();
                foreach (Signature s in Signatures)
                {
                    mnemonics.Add(s.mnemonic);
                    foreach (IList<Arch> group in s.archs)
                    {
                        foreach (Arch a in group)
                        {
                            if (!dictionary.ContainsKey(a)) dictionary.Add(a, new HashSet<Mnemonic>());
                            dictionary[a].Add(s.mnemonic);
                        }
                    }
                }

                foreach (Mnemonic m in mnemonics)
                {
                    // Skip mnemonics not in the Mnemonic enum (ParseMnemonic returned NONE). Writing
                    // them produced junk "GENERAL NONE ..." rows. A NONE here means the instruction
                    // (e.g. an SGX leaf like EDECCSSA) is missing from asm-tools-lib/Mnemonic.cs —
                    // add it there to get a proper signature.
                    if (m == Mnemonic.NONE)
                    {
                        Console.WriteLine("Skipping NONE mnemonic in " + Path.GetFileNameWithoutExtension(filename) + " (add it to the Mnemonic enum)");
                        continue;
                    }

                    sb2.AppendLine("<tr><td><a href=\"https://github.com/HJLebbink/asm-dude/wiki/" + Path.GetFileNameWithoutExtension(filename) + "\">" + m.ToString() + "</a></td><td>" + Description + "</td><td>" + Get_Arch_Str(Signatures, m) + "</td></tr>");

                    #region Handle Signature File
                    sb.AppendLine("GENERAL\t" + m.ToString() + "\t" + Description + "\t" + Path.GetFileNameWithoutExtension(filename));
                    foreach (Signature s in Signatures)
                    {
                        if (s.mnemonic == m)
                        {
                            sb.AppendLine(s.ToString());
                        }
                    }
                    #endregion
                }
            }

            // Write once, after processing every page (was re-writing the whole file each iteration).
            string? outDir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            File.WriteAllText(outFile, sb.ToString());
            Console.WriteLine("Wrote " + sb.ToString().Split('\n').Length + " lines to " + outFile);

            sb2.AppendLine("</table>");
            File.WriteAllText(Path.Combine(outDir ?? ".", "overview.txt"), sb2.ToString());

            // The wiki's Home.md IS this overview (a preamble followed by the instruction table).
            // Update it in place: keep whatever preamble it currently has (everything before the
            // first "<table>") and replace the table with the freshly generated one. `path` is the
            // wiki's doc/ dir, so Home.md sits in its parent.
            string? wikiRoot = Directory.GetParent(path)?.FullName;
            if (wikiRoot != null)
            {
                string homePath = Path.Combine(wikiRoot, "Home.md");
                string preamble = "Welcome to the Asm-Dude wiki!\n\nThis wiki contains a page for every x86 instruction, generated from the official Intel SDM (see asm-dude/VS/CSHARP/asm-annotate).\n\n --- \n\n";
                if (File.Exists(homePath))
                {
                    string old = File.ReadAllText(homePath);
                    int t = old.IndexOf("<table>");
                    if (t >= 0) preamble = old[..t];
                }
                File.WriteAllText(homePath, preamble + sb2.ToString());
                Console.WriteLine("Updated wiki overview: " + homePath);
            }

            foreach (Arch a in dictionary.Keys.OrderBy(f => f))
            {
                Console.WriteLine("#region " + ArchTools.ToString(a));
                foreach (Mnemonic m in dictionary[a].OrderBy(f => f))
                {
                    Console.WriteLine("    " + m.ToString() + "   ; " + Get_Arch_Str(dictionary, m));
                }
                Console.WriteLine("#endregion " + ArchTools.ToString(a));
            }

            return true;
        }

        private static string Get_Arch_Str(IDictionary<Arch, ISet<Mnemonic>> dictionary, Mnemonic m)
        {
            ISet<Arch> archs = new HashSet<Arch>();
            foreach (Arch a in dictionary.Keys) foreach (Mnemonic m2 in dictionary[a]) if (m == m2) archs.Add(a);
            string archStr = "";
            foreach (Arch a in archs) archStr += ArchTools.ToString(a) + " ";
            return archStr;
        }

        private static string Get_Arch_Str(IList<Signature> Signatures, Mnemonic m)
        {
            ISet<Arch> archs = new HashSet<Arch>();
            foreach (Signature s in Signatures) if (s.mnemonic == m) foreach (IList<Arch> group in s.archs) foreach (Arch a in group) archs.Add(a);
            string archStr = "";
            foreach (Arch a in archs) archStr += ArchTools.ToString(a) + " ";
            return archStr.TrimEnd();
        }

        private static (string Description, IList<Signature> Signatures) Parse(string content)
        {
            //1] get everything before the first occurrence of "<table>"
            int pos_Start_Table = content.IndexOf("<table>");
            string substr1 = content[..pos_Start_Table];
            int pos_Hyphen = Find_First_Hyphen_Position(substr1);
            // Collapse ALL newlines (the wiki uses LF, not CRLF) — a long title wraps across lines
            // ("...Floating-Point\nValues"), and a newline here would split the tab-separated GENERAL
            // line across two output lines, breaking the signature file format.
            string Description = Regex.Replace(substr1[(pos_Hyphen + 1)..].Trim(), @"\s+", " ");

            // 2] parse EVERY opcode table (a long instruction's opcode list is split across page
            // breaks into multiple <table> blocks, each with its own "Opcode" header). Reading only
            // the first would drop the last forms (e.g. VFNMADD231PH). Non-opcode tables (the
            // "Instruction Operand Encoding" table) are skipped — they don't define signatures.
            var signatures = new List<Signature>();
            int pos = pos_Start_Table;
            while (pos >= 0)
            {
                int end = content.IndexOf("</table>", pos);
                if (end < 0) break;
                string tableHtml = content[pos..end].Replace("<table>", "");
                if (tableHtml.Contains("Opcode"))
                {
                    foreach (var s in To_Signature(Parse_Table(tableHtml)))
                        signatures.Add(s);
                }
                pos = content.IndexOf("<table>", end);
            }
            return (Description, signatures);
        }

        internal struct Signature
        {
            public Mnemonic mnemonic;
            public string parameters;
            public string parameter_descriptions;

            /// <summary>Architecture requirement in DNF: outer = OR-groups, inner = AND-members.</summary>
            public IList<IList<Arch>> archs;
            public string description;

            public override readonly string ToString()
            {
                StringBuilder sb = new();
                sb.Append(this.mnemonic.ToString() + "\t");
                sb.Append(this.parameters + "\t");

                // DNF machine format: '+' between AND-members, ',' between OR-groups
                // (e.g. "AVX512_VL+AVX512_F,AVX10").
                sb.Append(ArchTools.ToStringDnf(this.archs));
                sb.Append('\t');
                sb.Append(this.parameter_descriptions + "\t");

                sb.Append(this.description);
                return sb.ToString();
            }
        }

        internal static IList<Signature> To_Signature(IList<IList<string>> table)
        {
            #region Determine what is where
            int mnemonic_column = -2;
            int description_column = -2;
            int arch_column = -2;

            IList<string> header = table[0];
            if (header.Count == 6)
            {
                if (header[1].Equals("Instruction"))
                {
                    mnemonic_column = 1;
                    arch_column = -1;
                    description_column = 5;
                }
                else
                {
                    mnemonic_column = 0;
                    arch_column = 4;
                    description_column = 5;
                }
            }
            else if (header.Count == 5)
            {
                if (header[0].Contains("Instruction"))
                {
                    mnemonic_column = 0;
                    arch_column = 3;
                    description_column = 4;
                }
                else if (header[1].Contains("Instruction"))
                {
                    mnemonic_column = 1;
                    arch_column = -1;
                    description_column = 4;
                }
                else
                {
                    mnemonic_column = 0;
                    arch_column = 3;
                    description_column = 4;
                }
                if (header[3].Contains("CPUID"))
                {
                    arch_column = 3;
                }
            }
            else if (header.Count == 4)
            {
                mnemonic_column = 0;
                arch_column = -1;
                description_column = 3;
            }
            else if (header.Count == 3)
            {
                mnemonic_column = 1;
                arch_column = -10;
                description_column = 2;
            }
            else
            {
                Console.WriteLine("WARNING: To_Signature: found header count " + header.Count + ".");
            }
            #endregion

            int n_Signatures = table.Count;
            IList<Signature> Results = new List<Signature>(n_Signatures);

            // Unrecognised header layout (e.g. a malformed/continuation fragment that merely contains
            // the word "Opcode") — mnemonic_column was never set; skip rather than index out of range.
            if (mnemonic_column < 0) return Results;

            for (int row_i = 1; row_i < n_Signatures; ++row_i)
            {
                var row = table[row_i];

                if (mnemonic_column >= row.Count)
                {
                    Console.WriteLine("WARNING: malformed row");
                    break;
                }
                var Parameters = Parse_Parameters(row[mnemonic_column]);

                // The 2026 tables are inconsistent about where the mnemonic sits: sometimes the
                // "Opcode" + "Instruction" header columns are merged into column 0 of the data
                // ("9F LAHF", Instruction cell empty), sometimes the header is the combined
                // "Opcode/Instruction" but the data splits opcode (col0) / instruction (col1)
                // — often with a footnote "1" shoved in as a phantom header column. When the chosen
                // column yields no mnemonic, search the first two columns (opcode/instruction), but
                // NOT the description column (it may name a different instruction).
                if (Parameters.mnemonic == Mnemonic.NONE)
                {
                    foreach (int c in new[] { 0, 1 })
                    {
                        if (c == mnemonic_column || c >= row.Count) continue;
                        var alt = Parse_Parameters(row[c]);
                        if (alt.mnemonic != Mnemonic.NONE) { Parameters = alt; break; }
                    }
                }

                // archs in DNF: a list of OR-groups, each an AND-list. The opcode-pattern fallbacks
                // below are single unconditional archs, so they become a single one-member group.
                IList<IList<Arch>> archs;
                if (arch_column == -1)
                {
                    string descr = " " + Parameters.Parameter_Descriptions;

                    if (descr.Contains(" CMOV"))
                    {
                        if (descr.Contains("R64"))
                        {
                            archs = [[Arch.ARCH_X64]];
                        }
                        else
                        {
                            archs = [[Arch.ARCH_P6]];
                        }
                    }
                    else if (descr.Contains("REL16") || descr.Contains("REL32"))
                    {
                        archs = [[Arch.ARCH_386]];
                    }
                    else if (descr.Contains("REL64"))
                    {
                        archs = [[Arch.ARCH_X64]];
                    }
                    else if (descr.Contains("M64") || descr.Contains("R64") || descr.Contains("RCX"))
                    {
                        archs = [[Arch.ARCH_X64]];
                    }
                    else if (descr.Contains("IMM32") || descr.Contains("M32") || descr.Contains("R32") || descr.Contains("ECX"))
                    {
                        archs = [[Arch.ARCH_386]];
                    }
                    else
                    {
                        archs = [[Arch.ARCH_8086]];
                    }
                }
                else if (arch_column == -10)
                {
                    archs = [[Arch.ARCH_SMX]];
                }
                else
                {
                    if (arch_column < row.Count)
                    {
                        archs = Parse_Archs(row[arch_column]);
                    }
                    else
                    {
                        archs = [];
                    }
                }

                string description = (description_column < row.Count) ? row[description_column] : "";
                description = AbbreviateDescription(description);

                Results.Add(new Signature
                {
                    mnemonic = Parameters.mnemonic,
                    parameters = Parameters.Parameters,
                    parameter_descriptions = Parameters.Parameter_Descriptions,
                    archs = archs,
                    description = description
                });
            }
            return Results;
        }

        internal static (Mnemonic mnemonic, string Parameters, string Parameter_Descriptions) Parse_Parameters(string str)
        {
            string parameters = "";
            string parameter_descriptions = "";
            // Drop a stray "hyphen-space" left by a line break inside the cell ("AES- ENCWIDE128KL"
            // -> "AESENCWIDE128KL"); a real instruction cell never contains "- ".
            string str2 = " " + str.Replace("*", "").Replace("- ", "").Trim() + " ";

            str2 = str2.Replace("REP ", "REP_").Replace("REPE ", "REPE_").Replace("REPNE ", "REPNE_");

            string str_Upper = " " + str2.ToUpper() + " ";

            Mnemonic mnemonic = Mnemonic.NONE;
            foreach (Mnemonic m in Enum.GetValues<Mnemonic>())
            {
                string mnemonic_str = m.ToString();
                int pos_mnemonic = str_Upper.IndexOf(" " + mnemonic_str + " ");
                if (pos_mnemonic != -1)
                {
                    mnemonic = m;
                    string tmp = str2[(pos_mnemonic + mnemonic_str.Length)..].Replace(" ", "").Trim().ToUpper();
                    parameters = Cleanup_Parameters(tmp);
                    parameter_descriptions = (tmp.Length > 0) ? (mnemonic_str + " " + tmp) : mnemonic_str;
                    break;
                }
                else
                {
                    pos_mnemonic = str_Upper.IndexOf("[" + mnemonic_str + "]");
                    if (pos_mnemonic != -1)
                    {
                        mnemonic = m;
                        string tmp = str2[(pos_mnemonic + mnemonic_str.Length)..].Replace("[", "").Replace("]", "").Replace(" ", "").Trim().ToUpper();
                        parameters = Cleanup_Parameters(tmp);
                        parameter_descriptions = (tmp.Length > 0) ? (mnemonic_str + " " + tmp) : mnemonic_str;
                        break;
                    }
                }
            }
            if (mnemonic == Mnemonic.NONE)
            {
                Console.WriteLine("Could not find a mnemonic in string " + str);
            }
            return (mnemonic, parameters, parameter_descriptions);
        }

        internal static string Cleanup_Parameters(string str)
        {
            // Normalise implicit-operand angle-bracket notation FIRST — before the digit-stripping
            // below, which would otherwise mangle "<XMM4-6>" into "<XMM-6>". Every implicit XMM
            // form (<XMM0>, <XMM0-7>, <XMM4-6>, …) maps to the recognised XMM_ZERO token
            // (AsmSignatureEnum.REG_XMM0); any other implicit register ("<EAX>") just loses its
            // brackets. Leaving the angle brackets in would break signature help (unrecognised token).
            str = Regex.Replace(str, "<[XYZ]MM[0-9][^>]*>", "XMM_ZERO");
            str = Regex.Replace(str, "<([A-Za-z][A-Za-z0-9]*)>", "$1");

            var tmp = str.Replace("IMM16", "XYZZY");
            tmp = tmp.
                Replace("+3", "").
                Replace("XMM1", "XMM").Replace("XMM2", "XMM").Replace("XMM3", "XMM").Replace("XMM4", "XMM").
                Replace("YMM1", "YMM").Replace("YMM2", "YMM").Replace("YMM3", "YMM").Replace("YMM4", "YMM").
                Replace("ZMM1", "ZMM").Replace("ZMM2", "ZMM").Replace("ZMM3", "ZMM").
                Replace("MM1", "MM").Replace("MM2", "MM").
                Replace("BND1", "BND").Replace("BND2", "BND").Replace("ZMM3", "ZMM").
                Replace("K1", "K").Replace("K2", "K").Replace("K3", "K").
                Replace("R32A", "R32").Replace("R32B", "R32").Replace("R64A", "R64").Replace("R64B", "R64");
            tmp = tmp.Replace("XYZZY", "IMM16");
            // AMX tile operands: normalise TMM0..TMM7 -> TMM (the MM1/MM2 rules above already catch
            // TMM1/TMM2, but TMM3 and higher need this).
            tmp = Regex.Replace(tmp, "TMM[0-9]", "TMM");
            return tmp;
        }

        /// <summary>
        /// Shrinks the long phrases the SDM repeats in instruction descriptions to keep the
        /// signature file compact: floating-point→FP, double-precision→DP, single-precision→SP.
        /// Matches the hyphenated form ("single-precision"), the space form that recent SDM
        /// revisions use ("single precision"), and the "hyphen-space" PDF artifact
        /// ("single- precision"), case-insensitively.
        /// </summary>
        internal static string AbbreviateDescription(string description)
        {
            description = Regex.Replace(description, @"floating[- ]+point", "FP", RegexOptions.IgnoreCase);
            description = Regex.Replace(description, @"double[- ]+precision", "DP", RegexOptions.IgnoreCase);
            description = Regex.Replace(description, @"single[- ]+precision", "SP", RegexOptions.IgnoreCase);
            return description;
        }

        // Parse the SDM "CPUID Feature Flag" cell into an architecture requirement in DNF
        // (list of OR-groups, each an AND-list). The boolean-expression parser lives in
        // ArchTools.ParseArchExpression so it is unit-testable from asm-tools-tests.
        private static IList<IList<Arch>> Parse_Archs(string str)
        {
            return ArchTools.ParseArchExpression(str).Select(g => (IList<Arch>)g.ToList()).ToList();
        }

        internal static IList<IList<string>> Parse_Table(string str)
        {
            var results = new List<IList<string>>();
            // Drop footnote-reference superscripts ("imm32<sup>1</sup>" -> "imm32") and bold tags
            // so they don't leak into the parsed mnemonic/parameters.
            string str2 = Regex.Replace(str, "<sup>[^<]*</sup>", "").Replace("<b>", "").Replace("</b>", "");

            while (str2.Length > 0)
            {
                (IList<string> Row, string Remainder) = Parse_Table_Row(str2);
                results.Add(Row);
                str2 = Remainder;
            }
            return results;
        }

        private static (IList<string> Row, string Remainder) Parse_Table_Row(string str)
        {
            int pos_Tr_Begin = str.IndexOf("<tr>");
            int pos_Tr_End = str.IndexOf("</tr>");

            string subStr = str[pos_Tr_Begin..pos_Tr_End].Replace("<tr>", "");
            IList<string> Row = Parse_Table_Cells(subStr);

            string Remainder = str[(pos_Tr_End + 5)..].Trim();
            return (Row, Remainder);
        }

        private static IList<string> Parse_Table_Cells(string str)
        {
            IList<string> Results = [];
            // Split on any opening <td ...> (cells may carry colspan/rowspan attributes), dropping
            // the text before the first cell. </td> tags are removed.
            string str2 = str.Replace("</td>", "");
            string[] parts = Regex.Split(str2, "<td[^>]*>");
            for (int i = 1; i < parts.Length; i++) // [0] is the text before the first <td>
            {
                Results.Add(parts[i].Trim());
            }
            return Results;
        }

        private static int Find_First_Hyphen_Position(string str)
        {
            int pos_Hyphen = str.IndexOf('—');
            if (pos_Hyphen == -1)
            {
                Console.WriteLine("WARNING: Find_First_Hyphen_Position: cannot find hyphen in str \"" + str + "\".");
                pos_Hyphen = str.IndexOf('–');
            }
            if (pos_Hyphen == -1)
            {
                Console.WriteLine("WARNING: Find_First_Hyphen_Position: cannot find hyphen in str \"" + str + "\".");
                pos_Hyphen = str.IndexOf('-');
            }
            return pos_Hyphen;
        }
    }
}
