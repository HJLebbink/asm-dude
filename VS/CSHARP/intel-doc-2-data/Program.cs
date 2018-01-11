using AsmTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace intel_doc_2_data
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;

            Payload();

            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            Console.WriteLine(string.Format("Elapsed time " + elapsedSec + " sec"));
            Console.WriteLine(string.Format("Press any key to continue."));
            Console.ReadKey();
        }

        static void Payload()
        {
            string path = "../../../../asm-dude.wiki/doc";
            StringBuilder sb = new StringBuilder();

            foreach (string filename in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
            {
                Console.WriteLine(filename);
                StreamReader file_Stream = File.OpenText(filename);
                string file_Content = file_Stream.ReadToEnd();
                var Results = Parse(file_Content);
                file_Stream.Close();

                sb.AppendLine(";--------------------------------------------------------");
                foreach (Mnemonic m in Results.Mnemonics)
                {
                    sb.AppendLine("GENERAL\t" + m.ToString() + "\t" + Results.Description + "\t" + Path.GetFileNameWithoutExtension(filename));
                }

                foreach (Signature s in Results.Signatures)
                {
                    sb.AppendLine(s.ToString());
                }
                System.IO.File.WriteAllText(@"C:\Temp\VS\signature-dec2018.txt", sb.ToString());
            }
        }

        static (IList<Mnemonic> Mnemonics, string Description, IList<Signature> Signatures) Parse(string content)
        {
            //1] get everthing before the first occurance of "<table>"
            int pos_Start_Table = content.IndexOf("<table>");
            string substr1 = content.Substring(0, pos_Start_Table);
            int pos_Hyphen = Find_First_Hyphen_Position(substr1);
            string Description = substr1.Substring(pos_Hyphen + 1).Trim().Replace("\r\n", " ");
            IList<Mnemonic> Mnemonics = Retrieve_Mnemonics(substr1.Substring(0, pos_Hyphen));
            int pos_End_Table = content.IndexOf("</table>");
            var table = Parse_Table(content.Substring(pos_Start_Table, pos_End_Table - pos_Start_Table).Replace("<table>",""));
            var signatures = To_Signature(table, Mnemonics);
            return (Mnemonics, Description, signatures);
        }

        struct Signature
        {
            public Mnemonic mnemonic;
            public string parameters;
            public string parameter_descriptions;
            public IList<Arch> archs;
            public string description;

            override
            public string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(this.mnemonic.ToString() + "\t");
                sb.Append(this.parameters + "\t");
                sb.Append(this.parameter_descriptions + "\t");

                for (int i = 0; i<this.archs.Count; ++i)
                {
                    sb.Append(ArchTools.ToString(this.archs[i]));
                    if (i < (this.archs.Count - 1)) sb.Append(" ");
                }
                sb.Append("\t");

                sb.Append(this.description);
                return sb.ToString();
            }
        }

        static IList<Signature> To_Signature(IList<IList<string>> table, IList<Mnemonic> mnemonics)
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

                IList<Arch> archs;
                if (arch_column == -1)
                {
                    archs = new List<Arch> { Arch.ARCH_8086 };
                }
                else
                {
                    if (arch_column < row.Count)
                    {
                        archs = Parse_Archs(row[arch_column]); 
                    }
                    else
                    {
                        archs = new List<Arch> { Arch.NONE };
                    }
                }

                string description = (description_column < row.Count) ? row[description_column] : "";
                description = description.Replace("floating-point", "FP").Replace("floating- point", "FP").Replace("double-precision", "DP").Replace("single-precision", "SP");

                if (mnemonic_column < row.Count)
                {
                    var Parameters = Parse_Parameters(row[mnemonic_column], mnemonics);

                    Signature sig = new Signature
                    {
                        mnemonic = Parameters.mnemonic,
                        parameters = Parameters.Parameters,
                        parameter_descriptions = Parameters.Parameter_Descriptions,
                        archs = archs,
                        description = description
                    };

                    Results.Add(sig);
                } else
                {
                    Console.WriteLine("WARNING: malformed row");
                    //Console.ReadKey();
                }
            }
            return Results;
        }

        static (Mnemonic mnemonic, string Parameters, string Parameter_Descriptions) Parse_Parameters(string str, IList<Mnemonic> mnemonics)
        {
            Mnemonic mnemonic = Mnemonic.NONE;
            string parameters = "";
            string parameter_descriptions = "";
            foreach (Mnemonic m in mnemonics)
            {
                string mnemonic_str = m.ToString();
                string str_Upper = str.ToUpper();
                int pos_mnemonic = str_Upper.IndexOf(mnemonic_str + " ");
                if (pos_mnemonic == -1)
                {
                    pos_mnemonic = str_Upper.IndexOf(mnemonic_str);
                }
                if (pos_mnemonic != -1)
                {
                    mnemonic = m;
                    string tmp = str.Substring(pos_mnemonic + mnemonic_str.Length).Replace(" ", "").Replace("*", "").Trim().ToUpper();
                    parameters = tmp.
                        Replace("XMM1", "XMM").Replace("XMM2", "XMM").Replace("XMM3", "XMM").
                        Replace("YMM1", "YMM").Replace("YMM2", "YMM").Replace("YMM3", "YMM").
                        Replace("ZMM1", "ZMM").Replace("ZMM2", "ZMM").Replace("ZMM3", "ZMM").
                        Replace("K1", "K").Replace("K2", "K").Replace("K3", "K").
                        Replace("R32A", "R32").Replace("R32B", "R32").Replace("R64B", "R64").Replace("R64B", "R64");
                    if (tmp.Length > 0)
                    {
                        parameter_descriptions = mnemonic.ToString() + " " + tmp;
                    }
                    else
                    {
                        parameter_descriptions = mnemonic.ToString();
                    }
                    break;
                }
            }
            if (mnemonic==Mnemonic.NONE)
            {
                Console.WriteLine("Could not find a mnemonic in string " + str);
                //Console.ReadKey();
            }
            return (mnemonic, parameters, parameter_descriptions);
        }

        static IList<Arch> Parse_Archs(string str)
        {
            IList<Arch> Results = new List<Arch>();
            foreach (string s in str.Replace(",", " ").Split(' '))
            {
                Arch a = ArchTools.ParseArch(s.Trim(), false);
                if (a != Arch.NONE)
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

            string subStr = str.Substring(pos_Tr_Begin, pos_Tr_End - pos_Tr_Begin).Replace("<tr>", "");
            IList<string> Row = Parse_Table_Cells(subStr);

            string Remainder = str.Substring(pos_Tr_End + 5).Trim();
            return (Row, Remainder);
        }

        static IList<string> Parse_Table_Cells(string str)
        {
            IList<string> Results = new List<string>();
            // remove the first <td> and all </td>

            string str2 = str.Replace("</td>", "");
            int pos_td = str2.IndexOf("<td>");
            str2 = str2.Substring(pos_td + 4);

            foreach (string s1 in str2.Split("<td>"))
            {
                string s2 = s1.Trim();
                Results.Add(s2);
            }
            return Results;
        }

        static IList<Mnemonic> Retrieve_Mnemonics(string str)
        {
            var Mnemonics = new List<Mnemonic>();

            int pos_b = str.IndexOf("<b>");
            string str2 = (pos_b == -1) ? str : str.Substring(pos_b);

            foreach (string mnemonicStr in str2.Replace("<b>", "").Replace("</b>", "").Split('/'))
            {
                string mnemonicStr2 = mnemonicStr.Trim().ToUpper();
                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr2, true);
                if (mnemonic == Mnemonic.NONE)
                {
                    if (mnemonicStr2.Equals("CMOVCC"))
                    {
                        foreach (Mnemonic m in new Mnemonic[] {
                            Mnemonic.CMOVZ,Mnemonic.CMOVNE,Mnemonic.CMOVNZ,Mnemonic.CMOVA,Mnemonic.CMOVNBE,Mnemonic.CMOVAE,Mnemonic.CMOVNB,Mnemonic.CMOVB,Mnemonic.CMOVNAE,Mnemonic.CMOVBE,Mnemonic.CMOVNA,
                            Mnemonic.CMOVG,Mnemonic.CMOVNLE,Mnemonic.CMOVGE,Mnemonic.CMOVNL,Mnemonic.CMOVL,Mnemonic.CMOVNGE,Mnemonic.CMOVLE,Mnemonic.CMOVNG,Mnemonic.CMOVC,Mnemonic.CMOVNC,Mnemonic.CMOVO,
                            Mnemonic.CMOVNO,Mnemonic.CMOVS,Mnemonic.CMOVNS,Mnemonic.CMOVPO,Mnemonic.CMOVNP,Mnemonic.CMOVPE,Mnemonic.CMOVP, Mnemonic.CMOVE})
                        {
                            Mnemonics.Add(m);
                        }
                    }
                    else if (mnemonicStr2.Equals("JCC"))
                    {
                        foreach (Mnemonic m in new Mnemonic[] {
                            Mnemonic.JZ,Mnemonic.JNE,Mnemonic.JNZ,Mnemonic.JA,Mnemonic.JNBE,Mnemonic.JAE,Mnemonic.JNB,Mnemonic.JB,Mnemonic.JNAE,Mnemonic.JBE,Mnemonic.JNA,
                            Mnemonic.JG,Mnemonic.JNLE,Mnemonic.JGE,Mnemonic.JNL,Mnemonic.JL,Mnemonic.JNGE,Mnemonic.JLE,Mnemonic.JNG,Mnemonic.JC,Mnemonic.JNC,Mnemonic.JO,
                            Mnemonic.JNO,Mnemonic.JS,Mnemonic.JNS,Mnemonic.JPO,Mnemonic.JNP,Mnemonic.JPE,Mnemonic.JP, Mnemonic.JE})
                        {
                            Mnemonics.Add(m);
                        }
                    }
                    else if (mnemonicStr2.Equals("SETCC"))
                    {
                        foreach (Mnemonic m in new Mnemonic[] {
                            Mnemonic.SETZ,Mnemonic.SETNE,Mnemonic.SETNZ,Mnemonic.SETA,Mnemonic.SETNBE,Mnemonic.SETAE,Mnemonic.SETNB,Mnemonic.SETB,Mnemonic.SETNAE,Mnemonic.SETBE,Mnemonic.SETNA,
                            Mnemonic.SETG,Mnemonic.SETNLE,Mnemonic.SETGE,Mnemonic.SETNL,Mnemonic.SETL,Mnemonic.SETNGE,Mnemonic.SETLE,Mnemonic.SETNG,Mnemonic.SETC,Mnemonic.SETNC,Mnemonic.SETO,
                            Mnemonic.SETNO,Mnemonic.SETS,Mnemonic.SETNS,Mnemonic.SETPO,Mnemonic.SETNP,Mnemonic.SETPE,Mnemonic.SETP, Mnemonic.SETE})
                        {
                            Mnemonics.Add(m);
                        }
                    }
                    else if (mnemonicStr2.Equals("LOOPCC"))
                    {
                        foreach (Mnemonic m in new Mnemonic[] {
                            Mnemonic.LOOP,Mnemonic.LOOPE,Mnemonic.LOOPNE,Mnemonic.LOOPNZ,Mnemonic.LOOPZ})
                        {
                            Mnemonics.Add(m);
                        }
                    }
                    else
                    {
                        Console.WriteLine("WARNING: Retrieve_Mnemonics: found unsupported mnemonic str \"" + mnemonicStr2 + "\".");
                        //Console.ReadKey();
                    }
                }
                else
                {
                    Mnemonics.Add(mnemonic);
                }
            }
            return Mnemonics;
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
