// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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
    using System;
    //using System.Collections.Frozen; TODO enable .NET 8
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using AsmSourceTools;

    using AsmTools;

    using Microsoft.VisualStudio.LanguageServer.Protocol;

    public class MnemonicStore
    {
        private readonly AsmLanguageServerOptions options;

        private readonly Dictionary<Mnemonic, List<AsmSignatureInformation>> data_;
        private readonly Dictionary<Mnemonic, List<Arch>> arch_;
        private readonly Dictionary<Mnemonic, string> htmlRef_;
        private readonly Dictionary<Mnemonic, string> description_;
        private readonly HashSet<Mnemonic> mnemonics_switched_on_;
        private readonly HashSet<Rn> register_switched_on_;

        //private readonly FrozenDictionary<Mnemonic, List<AsmSignatureInformation>> data_;
        //private readonly FrozenDictionary<Mnemonic, List<Arch>> arch_;
        //private readonly FrozenDictionary<Mnemonic, string> htmlRef_;
        //private readonly FrozenDictionary<Mnemonic, string> description_;
        //private readonly FrozenSet<Mnemonic> mnemonics_switched_on_;
        //private readonly FrozenSet<Rn> register_switched_on_;

        public MnemonicStore(string filename_RegularData, string filename_HandcraftedData, AsmLanguageServerOptions options)
        {
            this.options = options;
            LanguageServer.LogInfo($"MnemonicStore: constructor: regularData = {filename_RegularData}; handcraftedData = {filename_HandcraftedData}");

            var (data, arch, htmlRef, description) = this.CalcSignatureInformation(filename_RegularData, filename_HandcraftedData);

            this.data_ = data;
            this.arch_ = arch;
            this.htmlRef_ = htmlRef;
            this.description_ = description;
            this.mnemonics_switched_on_ = this.CalcMnemonicsSwitchedOn();
            this.register_switched_on_ = this.CalcRegisterSwitchedOn();

            //this.data_ = FrozenDictionary.ToFrozenDictionary(data);
            //this.arch_ = FrozenDictionary.ToFrozenDictionary(arch);
            //this.htmlRef_ = FrozenDictionary.ToFrozenDictionary(htmlRef);
            //this.description_ = FrozenDictionary.ToFrozenDictionary(description);
            //this.mnemonics_switched_on_ = FrozenSet.ToFrozenSet(this.CalcMnemonicsSwitchedOn());
            //this.register_switched_on_ = FrozenSet.ToFrozenSet(this.CalcRegisterSwitchedOn());
        }

        public bool HasElement(Mnemonic mnemonic)
        {
            return this.data_.ContainsKey(mnemonic);
        }

        public IEnumerable<AsmSignatureInformation> GetSignatures(Mnemonic mnemonic)
        {
            return this.data_.TryGetValue(mnemonic, out List<AsmSignatureInformation> list) ? list : Enumerable.Empty<AsmSignatureInformation>();
        }

        public IEnumerable<Arch> GetArch(Mnemonic mnemonic)
        {
            return this.arch_.TryGetValue(mnemonic, out List<Arch> value) ? value : Enumerable.Empty<Arch>();
        }

        public string GetHtmlRef(Mnemonic mnemonic)
        {
            return this.htmlRef_.TryGetValue(mnemonic, out string value) ? value : string.Empty;
        }

        public string GetDescription(Mnemonic mnemonic)
        {
            return this.description_.TryGetValue(mnemonic, out string value) ? value : string.Empty;
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            foreach (KeyValuePair<Mnemonic, List<AsmSignatureInformation>> element in this.data_)
            {
                Mnemonic mnemonic = element.Key;
                string s1 = mnemonic.ToString();
                string s6 = this.htmlRef_[mnemonic];

                foreach (AsmSignatureInformation sig in element.Value)
                {
                    string s2 = sig.SignatureInformation.Label;
                    string s3 = "ARCH";// sig.Arch_Str;
                    string s4 = "TODO XYZZY";// sig.Parameters.ToString();
                    var s5 = sig.SignatureInformation.Documentation;
                    sb.AppendLine(s1 + "\t" + s2 + "\t" + s3 + "\t" + s4 + "\t" + s5 + "\t" + s6);
                }
            }
            return sb.ToString();
        }

        private AsmSignatureInformation CreateAsmSignatureElement(Mnemonic mnemonic, string args, string arch, string sign, string doc)
        {
            // EG: mnemonic=VADDPS
            // args=XMM{K}{Z},XMM,XMM/M128/M32BCST
            // arch=AVX512_VL,AVX512_F
            // sign=VADDPS XMM1{K1}{Z},XMM2,XMM3/M128/M32BCST
            // doc=Add packed SP FP values from xmm3/m128/m32bcst to xmm2 and store result in xmm1 with writemask k1.

            List<AsmSignatureEnum> ParseOperands(string str)
            {
                List<AsmSignatureEnum> result = new();
                str = str.Replace("R/M", "R_M");
                foreach (string op in str.Split('/'))
                {
                    result.AddRange(AsmSignatureTools.Parse_Operand_Type_Enum(op, true));
                }
                return result;
            }

            string ParamDoc(IList<AsmSignatureEnum> x)
            {
                StringBuilder argDoc = new();
                foreach (AsmSignatureEnum op in x)
                {
                    argDoc.Append(AsmSignatureTools.Get_Doc(op) + " or ");
                }
                argDoc.Length -= 4;
                return argDoc.ToString();
            }
            
            Tuple<int, int>[] FindParamPositions(string signature)
            {
                int startPos = -1;
                for (int i = 0; i < signature.Length; ++i)
                {
                    if (signature[i] == ' ')
                    {
                        startPos = i + 1;
                        break;
                    }
                }
                if (startPos == -1)
                {
                    return Array.Empty<Tuple<int, int>>();
                }
                var result = new List<Tuple<int, int>>();
                int previousPos = startPos;
                for (int i = startPos; i < signature.Length; ++i)
                {
                    if (signature[i] == ',')
                    {
                        result.Add(new Tuple<int, int>(previousPos, i));
                        previousPos = i + 1;
                    }
                }
                if (previousPos < signature.Length)
                {
                    result.Add(new Tuple<int, int>(previousPos, signature.Length));
                }
                return result.ToArray<Tuple<int, int>>();
            }

            var parameters = new List<ParameterInformation>();
            var operands = (args.Length == 0) ? Array.Empty<string>() : args.Split(',');
            var parameterOffsets = FindParamPositions(sign);

            if (operands.Length != parameterOffsets.Length)
            {
                LanguageServer.LogError($"MnemonicStore:CreateAsmSignatureElement: inconsistent signature information: args={args}; parameterOffsets={parameterOffsets}");
                for (int i = 0; i < operands.Length; ++i)
                {
                    LanguageServer.LogError($"MnemonicStore:CreateAsmSignatureElement: operands[{i}]={operands[i]}");
                }
                for (int i = 0; i < parameterOffsets.Length; ++i)
                {
                    LanguageServer.LogError($"MnemonicStore:CreateAsmSignatureElement: parameterOffsets[{i}]={parameterOffsets[i]}; sign={sign}");
                }
            }

            var operandList = new List<IList<AsmSignatureEnum>>();

            var archs = ArchTools.ParseArchList(arch, false, true);
            if (archs[0] == Arch.ARCH_NONE)
            {
                Console.WriteLine($"MnemonicStore: CreateAsmSignatureElement: arch is \"{arch}\": mnemonic={mnemonic}; doc ={doc}");
            }

            for (int j = 0; j < operands.Length; ++j)
            {
                var operand = ParseOperands(operands[j]);
                operandList.Add(operand);

                parameters.Add(new ParameterInformation
                {
                    Label = parameterOffsets[j],
                    Documentation = ParamDoc(operandList[j]),
                });
            }
            return new AsmSignatureInformation{
                Mnemonic = mnemonic,
                Arch = archs,
                Operands = operandList,
                SignatureInformation = new SignatureInformation
                {
                    Label = sign,
                    Parameters = parameters.ToArray<ParameterInformation>(),
                    Documentation = doc,
                }
            };
        }

        private (Dictionary<Mnemonic, List<AsmSignatureInformation>> data, Dictionary<Mnemonic, List<Arch>> arch, Dictionary<Mnemonic, string> htmlRef, Dictionary<Mnemonic, string> description) CalcSignatureInformation(string filename_RegularData, string filename_HandcraftedData)
        {
            Dictionary<Mnemonic, List<AsmSignatureInformation>> data = new();
            Dictionary<Mnemonic, List<Arch>> arch = new();
            Dictionary<Mnemonic, string> htmlRef = new();
            Dictionary<Mnemonic, string> description = new();

            /// <summary>
            /// Add (and overwrite) return true if an existing signature element is overwritten;
            /// </summary>
            /// <param name="asmSignatureElement"></param>
            bool Add(AsmSignatureInformation asmSignatureElement, ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data)
            {
                //LanguageServer.LogInfo($"MnemonicStore: Add: {asmSignatureElement.SignatureInformation.Label}; number of elements before {this.data_.Count}");
                bool result = false;

                if (data.TryGetValue(asmSignatureElement.Mnemonic, out List<AsmSignatureInformation> signatureElementList))
                {
                    result = signatureElementList.Remove(asmSignatureElement);
                    signatureElementList.Add(asmSignatureElement);
                }
                else
                {
                    data.Add(asmSignatureElement.Mnemonic, new List<AsmSignatureInformation> { asmSignatureElement });
                }
                //LanguageServer.LogInfo($"MnemonicStore: Add: number of elements after {this.data_.Count}");
                return result;
            }

            void LoadRegularData(
                string filename,
                ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data,
                ref Dictionary<Mnemonic, List<Arch>> arch,
                ref Dictionary<Mnemonic, string> htmlRef,
                ref Dictionary<Mnemonic, string> description)
            {
                LanguageServer.LogInfo("MnemonicStore:loadRegularData: filename=" + filename);
                try
                {
                    StreamReader file = new(filename);
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if ((line.Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                        {
                            string[] columns = line.Split('\t');
                            if (columns.Length == 4)
                            { // general description
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[1], false);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    // ignore the unknown mnemonic
                                    LanguageServer.LogWarning("MnemonicStore:loadRegularData: unknown mnemonic in line: " + line);
                                }
                                else
                                {
                                    if (!description.ContainsKey(mnemonic))
                                    {
                                        description.Add(mnemonic, columns[2]);
                                    }
                                    else
                                    {
                                        // this happens when the mnemonic is defined in multiple files, using the data from the first file
                                        //LanguageServer.LogWarning("MnemonicStore:loadRegularData: mnemonic " + mnemonic + " already has a description");
                                    }
                                    if (!htmlRef.ContainsKey(mnemonic))
                                    {
                                        htmlRef.Add(mnemonic, columns[3]);
                                    }
                                    else
                                    {
                                        // this happens when the mnemonic is defined in multiple files, using the data from the first file
                                        //LanguageServer.LogWarning("MnemonicStore:loadRegularData: mnemonic " + mnemonic + " already has a html ref");
                                    }
                                }
                            }
                            else if ((columns.Length == 5) || (columns.Length == 6))
                            { // signature description, ignore an old sixth column
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    LanguageServer.LogWarning("MnemonicStore:loadRegularData: unknown mnemonic in line: " + line);
                                }
                                else
                                {
                                    var se = this.CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                    LanguageServer.LogInfo($"MnemonicStore: adding AsmSignatureInformation {se.SignatureInformation.Label}");
                                    if (Add(se, ref data))
                                    {
                                        LanguageServer.LogWarning("MnemonicStore:loadRegularData: signature already exists" + se.ToString());
                                    }
                                }
                            }
                            else
                            {
                                LanguageServer.LogWarning("MnemonicStore:loadRegularData: s.Length=" + columns.Length + "; funky line" + line);
                            }
                        }
                    }
                    file.Close();

                    foreach ((Mnemonic key, List<AsmSignatureInformation> value) in data)
                    {
                        HashSet<Arch> archs = new();
                        foreach (AsmSignatureInformation signatureElement in value)
                        {
                            archs.UnionWith(signatureElement.Arch);
                        }
                        arch[key] = archs.ToList();
                    }
                }
                catch (FileNotFoundException)
                {
                    LanguageServer.LogError("MnemonicStore:loadRegularData: could not find file \"" + filename + "\".");
                }
                catch (Exception e)
                {
                    LanguageServer.LogError("MnemonicStore:loadRegularData: error while reading file \"" + filename + "\"." + e);
                }
            }

            void LoadHandcraftedData(
                string filename,
                ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data,
                ref Dictionary<Mnemonic, List<Arch>> arch,
                ref Dictionary<Mnemonic, string> htmlRef,
                ref Dictionary<Mnemonic, string> description)
            {
                LanguageServer.LogInfo("MnemonicStore:load_data_intel: filename=" + filename);
                try
                {
                    StreamReader file = new(filename);
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if ((line.Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                        {
                            string[] columns = line.Split('\t');
                            if (columns.Length == 4)
                            { // general description
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[1], false);
                                // LogInfo($"MnemonicStore:LoadHandcraftedData? line={line}, mnemonic={mnemonic}");

                                if (mnemonic == Mnemonic.NONE)
                                {
                                    LanguageServer.LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                                }
                                else
                                {
                                    description.Remove(mnemonic);
                                    // LogInfo($"MnemonicStore:LoadHandcraftedData adding description for mnemonic={mnemonic}; descr={columns[2]}");
                                    description.Add(mnemonic, columns[2]);

                                    htmlRef.Remove(mnemonic);
                                    // LogInfo($"LoadHandcraftedData adding description for mnemonic={mnemonic}; url={columns[3]}");
                                    htmlRef.Add(mnemonic, columns[3]);
                                }
                            }
                            else if ((columns.Length == 5) || (columns.Length == 6))
                            { // signature description, ignore an old sixth column
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    LanguageServer.LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                                }
                                else
                                {
                                    var se = this.CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                    // LogInfo($"MnemonicStore: LoadHandcraftedData: adding AsmSignatureInformation {se.SignatureInformation.Label}");
                                    if (Add(se, ref data))
                                    {
                                        LanguageServer.LogWarning("MnemonicStore:LoadHandcraftedData: signature already exists" + se.ToString());
                                    }
                                }
                            }
                            else
                            {
                                LanguageServer.LogWarning("MnemonicStore:loadHandcraftedData: s.Length=" + columns.Length + "; funky line" + line);
                            }
                        }
                    }
                    file.Close();

                    foreach ((Mnemonic key, List<AsmSignatureInformation> value) in data)
                    {
                        HashSet<Arch> archs = new();
                        foreach (AsmSignatureInformation signatureElement in value)
                        {
                            archs.UnionWith(signatureElement.Arch);
                        }
                        arch[key] = archs.ToList();
                    }
                }
                catch (FileNotFoundException)
                {
                    LanguageServer.LogError("MnemonicStore:LoadHandcraftedData: could not find file \"" + filename + "\".");
                }
                catch (Exception e)
                {
                    LanguageServer.LogError("MnemonicStore:LoadHandcraftedData: error while reading file \"" + filename + "\"." + e);
                }
            }

            if (File.Exists(filename_RegularData))
            {
                LoadRegularData(filename_RegularData, ref data, ref arch, ref htmlRef, ref description);
            }
            else
            {
                LanguageServer.LogError($"MnemonicStore: constructor: regularData = {filename_RegularData} does not exist");
            }

            if (filename_HandcraftedData != null)
            {
                if (File.Exists(filename_HandcraftedData))
                {
                    LoadHandcraftedData(filename_HandcraftedData, ref data, ref arch, ref htmlRef, ref description);
                }
                else
                {
                    LanguageServer.LogError($"MnemonicStore: constructor: handcraftedData = {filename_HandcraftedData} does not exist");
                }
            }
            return (data, arch, htmlRef, description);
        }

        public bool IsMnemonicSwitchedOn(Mnemonic mnemonic)
        {
            return this.mnemonics_switched_on_.Contains(mnemonic);
        }

        public HashSet<Mnemonic> Get_Allowed_Mnemonics()
        {
            return this.mnemonics_switched_on_;
        }

        private HashSet<Mnemonic> CalcMnemonicsSwitchedOn()
        {
            HashSet<Mnemonic> result = new();

            ISet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
                foreach (Arch a in this.GetArch(mnemonic))
                {
                    if (arch_switched_on.Contains(a))
                    {
                        result.Add(mnemonic);
                        break;
                    }
                }
            }
            return result;
        }

        public bool IsRegisterSwitchedOn(Rn reg)
        {
            return this.register_switched_on_.Contains(reg);
        }

        public HashSet<Rn> Get_Allowed_Registers()
        {
            return this.register_switched_on_;
        }

        private HashSet<Rn> CalcRegisterSwitchedOn()
        {
            HashSet<Rn> result = new();

            ISet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
            foreach (Rn reg in Enum.GetValues(typeof(Rn)))
            {
                if (reg != Rn.NOREG)
                {
                    if (arch_switched_on.Contains(RegisterTools.GetArch(reg)))
                    {
                        result.Add(reg);
                    }
                }
            }
            return result;
        }
    }
}
