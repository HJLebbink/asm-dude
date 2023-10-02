using AsmTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace intel_doc_2_data
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Executable to load the AsmDude wiki pages, and turn them into source files for AsmDude

            DateTime startTime = DateTime.Now;

            Payload();

            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            Console.WriteLine(string.Format("Elapsed time " + elapsedSec + " sec"));
            Console.WriteLine(string.Format("Press any key to continue."));
            Console.ReadKey();
        }

        static void Payload()
        {
            string path = "C:/Source/Github/asm-dude.wiki/doc";

            if (!Directory.Exists(path))
            {
                Console.WriteLine("Could not find directory \"" + path + "\".");
                return;
            }

            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            IDictionary<Arch, ISet<Mnemonic>> dictionary = new Dictionary<Arch, ISet<Mnemonic>>();

            sb2.AppendLine("<table>");

            foreach (string filename in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f))
            {
                //Console.WriteLine(filename);
                StreamReader file_Stream = File.OpenText(filename);
                string file_Content = file_Stream.ReadToEnd();
                (string Description, IList<Signature> Signatures) = Parse(file_Content);
                file_Stream.Close();

                sb.AppendLine(";--------------------------------------------------------");

                ISet<Mnemonic> mnemonics = new HashSet<Mnemonic>();
                foreach (Signature s in Signatures)
                {
                    mnemonics.Add(s.mnemonic);
                    foreach (Arch a in s.archs)
                    {
                        if (!dictionary.ContainsKey(a)) dictionary.Add(a, new HashSet<Mnemonic>());
                        dictionary[a].Add(s.mnemonic);
                    }
                }

                foreach (Mnemonic m in mnemonics)
                {
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
                File.WriteAllText(@"C:\Temp\VS\signature-dec2018.txt", sb.ToString());
            }
            sb2.AppendLine("</table>");
            File.WriteAllText(@"C:\Temp\VS\overview.txt", sb2.ToString());

            foreach (Arch a in dictionary.Keys.OrderBy(f => f))
            {
                Console.WriteLine("#region " + ArchTools.ToString(a));
                foreach (Mnemonic m in dictionary[a].OrderBy(f => f))
                {
                    Console.WriteLine("    " + m.ToString() + "   ; " + Get_Arch_Str(dictionary, m));
                }
                Console.WriteLine("#endregion " + ArchTools.ToString(a));
            }
        }

        static string Get_Arch_Str(IDictionary<Arch, ISet<Mnemonic>> dictionary, Mnemonic m)
        {
            ISet<Arch> archs = new HashSet<Arch>();
            foreach (Arch a in dictionary.Keys) foreach (Mnemonic m2 in dictionary[a]) if (m == m2) archs.Add(a);
            string archStr = "";
            foreach (Arch a in archs) archStr += ArchTools.ToString(a) + " ";
            return archStr;
        }

        static string Get_Arch_Str(IList<Signature> Signatures, Mnemonic m)
        {
            ISet<Arch> archs = new HashSet<Arch>();
            foreach (Signature s in Signatures) if (s.mnemonic == m) foreach (Arch a in s.archs) archs.Add(a);
            string archStr = "";
            foreach (Arch a in archs) archStr += ArchTools.ToString(a) + " ";
            return archStr.TrimEnd();
        }

        static (string Description, IList<Signature> Signatures) Parse(string content)
        {
            //1] get everting before the first occurrence of "<table>"
            int pos_Start_Table = content.IndexOf("<table>");
            string substr1 = content[..pos_Start_Table];
            int pos_Hyphen = Find_First_Hyphen_Position(substr1);
            string Description = substr1[(pos_Hyphen + 1)..].Trim().Replace("\r\n", " ");
            int pos_End_Table = content.IndexOf("</table>");
            var table = Parse_Table(content[pos_Start_Table..pos_End_Table].Replace("<table>", ""));
            var signatures = To_Signature(table);
            return (Description, signatures);
        }

        struct Signature
        {
            public Mnemonic mnemonic;
            public string parameters;
            public string parameter_descriptions;
            public IList<Arch> archs;
            public string description;

            public override readonly string ToString()
            {
                StringBuilder sb = new();
                sb.Append(this.mnemonic.ToString() + "\t");
                sb.Append(this.parameters + "\t");

                for (int i = 0; i < this.archs.Count; ++i)
                {
                    sb.Append(ArchTools.ToString(this.archs[i]));
                    if (i < (this.archs.Count - 1)) sb.Append(',');
                }
                sb.Append('\t');
                sb.Append(this.parameter_descriptions + "\t");

                sb.Append(this.description);
                return sb.ToString();
            }
        }

        static IList<Signature> To_Signature(IList<IList<string>> table)
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
                //Console.ReadKey();
            }
            #endregion

            int n_Signatures = table.Count;
            IList<Signature> Results = new List<Signature>(n_Signatures);

            for (int row_i = 1; row_i < n_Signatures; ++row_i)
            {
                var row = table[row_i];

                if (mnemonic_column >= row.Count)
                {
                    Console.WriteLine("WARNING: malformed row");
                    break;
                }
                var Parameters = Parse_Parameters(row[mnemonic_column]);

                IList<Arch> archs;
                if (arch_column == -1)
                {
                    string descr = " " + Parameters.Parameter_Descriptions;

                    if (descr.Contains(" CMOV"))
                    {
                        if (descr.Contains("R64"))
                        {
                            archs = new List<Arch> { Arch.ARCH_X64 };
                        }
                        else
                        {
                            archs = new List<Arch> { Arch.ARCH_P6 };
                        }
                    }
                    else if (descr.Contains("REL16") || descr.Contains("REL32"))
                    {
                        archs = new List<Arch> { Arch.ARCH_386 };
                    }
                    else if (descr.Contains("REL64"))
                    {
                        archs = new List<Arch> { Arch.ARCH_X64 };
                    }
                    else if (descr.Contains("M64") || descr.Contains("R64") || descr.Contains("RCX"))
                    {
                        archs = new List<Arch> { Arch.ARCH_X64 };
                    }
                    else if (descr.Contains("IMM32") || descr.Contains("M32") || descr.Contains("R32") || descr.Contains("ECX"))
                    {
                        archs = new List<Arch> { Arch.ARCH_386 };
                    }
                    else
                    {
                        archs = new List<Arch> { Arch.ARCH_8086 };
                    }
                }
                else if (arch_column == -10)
                {
                    archs = new List<Arch> { Arch.ARCH_SMX };
                }
                else
                {
                    if (arch_column < row.Count)
                    {
                        archs = Parse_Archs(row[arch_column]);
                    }
                    else
                    {
                        archs = new List<Arch> { Arch.ARCH_NONE };
                    }
                }

                string description = (description_column < row.Count) ? row[description_column] : "";
                description = description.
                    Replace("floating-point", "FP").Replace("floating- point", "FP").Replace("Floating-Point", "FP").Replace("Floating- Point", "FP").
                    Replace("double-precision", "DP").Replace("double- precision", "DP").Replace("Double-Precision", "DP").Replace("Double- Precision", "DP").
                    Replace("single-precision", "SP").Replace("single- precision", "SP").Replace("Single-Precision", "SP").Replace("Single- Precision", "SP");

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

        static (Mnemonic mnemonic, string Parameters, string Parameter_Descriptions) Parse_Parameters(string str)
        {
            string parameters = "";
            string parameter_descriptions = "";
            string str2 = " " + str.Replace("*", "").Trim() + " ";

            str2 = str2.Replace("REP ", "REP_").Replace("REPE ", "REPE_").Replace("REPNE ", "REPNE_");

            string str_Upper = " " + str2.ToUpper() + " ";

            Mnemonic mnemonic = Mnemonic.NONE;
            foreach (Mnemonic m in Enum.GetValues(typeof(Mnemonic)))
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
                //Console.ReadKey();
            }
            return (mnemonic, parameters, parameter_descriptions);
        }

        static string Cleanup_Parameters(string str)
        {
            var tmp = str.Replace("IMM16", "XYZZY");
            tmp = tmp.
                Replace("+3", "").
                Replace("XMM1", "XMM").Replace("XMM2", "XMM").Replace("XMM3", "XMM").Replace("XMM4", "XMM").
                Replace("YMM1", "YMM").Replace("YMM2", "YMM").Replace("YMM3", "YMM").Replace("YMM4", "YMM").
                Replace("ZMM1", "ZMM").Replace("ZMM2", "ZMM").Replace("ZMM3", "ZMM").
                Replace("MM1", "MM").Replace("MM2", "MM").
                Replace("<XMM0>", "XMM_ZERO").
                Replace("BND1", "BND").Replace("BND2", "BND").Replace("ZMM3", "ZMM").
                Replace("K1", "K").Replace("K2", "K").Replace("K3", "K").
                Replace("R32A", "R32").Replace("R32B", "R32").Replace("R64A", "R64").Replace("R64B", "R64");
            tmp = tmp.Replace("XYZZY", "IMM16");
            return tmp;
        }

        static IList<Arch> Parse_Archs(string str)
        {
            IList<Arch> Results = new List<Arch>();
            foreach (string s in str.Replace(",", " ").Split(' '))
            {
                Arch a = ArchTools.ParseArch(s.Trim(), false, false);
                if (a != Arch.ARCH_NONE)
                {
                    Results.Add(a);
                }
            }
            return Results;
        }

        static IList<IList<string>> Parse_Table(string str)
        {
            var results = new List<IList<string>>();
            string str2 = str.Replace("<b>", "").Replace("</b>", "");

            while (str2.Length > 0)
            {
                (IList<string> Row, string Remainder) = Parse_Table_Row(str2);
                results.Add(Row);
                str2 = Remainder;
            }
            return results;
        }

        static (IList<string> Row, string Remainder) Parse_Table_Row(string str)
        {
            int pos_Tr_Begin = str.IndexOf("<tr>");
            int pos_Tr_End = str.IndexOf("</tr>");

            string subStr = str[pos_Tr_Begin..pos_Tr_End].Replace("<tr>", "");
            IList<string> Row = Parse_Table_Cells(subStr);

            string Remainder = str[(pos_Tr_End + 5)..].Trim();
            return (Row, Remainder);
        }

        static IList<string> Parse_Table_Cells(string str)
        {
            IList<string> Results = new List<string>();
            // remove the first <td> and all </td>

            string str2 = str.Replace("</td>", "");
            int pos_td = str2.IndexOf("<td>");
            str2 = str2[(pos_td + 4)..];

            foreach (string s1 in str2.Split("<td>"))
            {
                string s2 = s1.Trim();
                Results.Add(s2);
            }
            return Results;
        }

        static int Find_First_Hyphen_Position(string str)
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
