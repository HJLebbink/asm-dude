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
    using AsmSourceTools;

    using AsmTools;

    using Microsoft.VisualStudio.LanguageServer.Protocol;

    using System;
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class MnemonicStore
    {
        private readonly AsmLanguageServerOptions options;

        private readonly FrozenDictionary<Mnemonic, List<AsmSignatureInformation>> data_;
        private readonly FrozenDictionary<Mnemonic, List<Arch>> arch_;
        private readonly FrozenDictionary<Mnemonic, string> htmlRef_;
        private readonly FrozenDictionary<Mnemonic, string> description_;
        private readonly FrozenSet<Mnemonic> mnemonics_switched_on_;
        private readonly FrozenSet<Rn> register_switched_on_;

        public MnemonicStore(string filename_RegularData, string filename_HandcraftedData, AsmLanguageServerOptions options)
        {
            this.options = options;
            LanguageServer.LogInfo($"MnemonicStore: constructor: regularData = {filename_RegularData}; handcraftedData = {filename_HandcraftedData}");

            var (data, arch, htmlRef, description) = this.CalcSignatureInformation(filename_RegularData, filename_HandcraftedData);

            this.data_ = FrozenDictionary.ToFrozenDictionary(data);
            this.arch_ = FrozenDictionary.ToFrozenDictionary(arch);
            this.htmlRef_ = FrozenDictionary.ToFrozenDictionary(htmlRef);
            this.description_ = FrozenDictionary.ToFrozenDictionary(description);
            this.mnemonics_switched_on_ = FrozenSet.ToFrozenSet(this.CalcMnemonicsSwitchedOn());
            this.register_switched_on_ = FrozenSet.ToFrozenSet(this.CalcRegisterSwitchedOn());
        }

        public bool HasElement(Mnemonic mnemonic)
        {
            return this.data_.ContainsKey(mnemonic);
        }

        public IEnumerable<AsmSignatureInformation> GetSignatures(Mnemonic mnemonic)
        {
            return this.data_.TryGetValue(mnemonic, out List<AsmSignatureInformation>? list) ? list : Enumerable.Empty<AsmSignatureInformation>();
        }

        public IEnumerable<Arch> GetArch(Mnemonic mnemonic)
        {
            return this.arch_.TryGetValue(mnemonic, out List<Arch>? value) ? value : Enumerable.Empty<Arch>();
        }

        public string GetHtmlRef(Mnemonic mnemonic)
        {
            return this.htmlRef_.TryGetValue(mnemonic, out string? value) ? value : string.Empty;
        }

        public string GetDescription(Mnemonic mnemonic)
        {
            return this.description_.TryGetValue(mnemonic, out string? value) ? value : string.Empty;
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
                    string s3 = "ARCH TODO";// sig.Arch_Str;
                    string s4 = "PARAM TODO";// sig.Parameters.ToString();
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
                List<AsmSignatureEnum> result = [];
                str = str.Replace("R/M", "R_M")
                         .Replace("R32/64", "R32_64")
                         .Replace("R16/32/64", "R16_32_64")
                         .Replace("M14/28", "M14_28")
                         .Replace("M94/108", "M94_108");
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
                    return [];
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
                return [.. result];
            }

            var parameters = new List<ParameterInformation>();
            var operands = (args.Length == 0) ? [] : args.Split(',');
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
            return new AsmSignatureInformation
            {
                Mnemonic = mnemonic,
                Arch = archs,
                Operands = operandList,
                SignatureInformation = new SignatureInformation
                {
                    Label = sign,
                    Parameters = [.. parameters],
                    Documentation = doc,
                }
            };
        }

        private (Dictionary<Mnemonic, List<AsmSignatureInformation>> data, Dictionary<Mnemonic, List<Arch>> arch, Dictionary<Mnemonic, string> htmlRef, Dictionary<Mnemonic, string> description) CalcSignatureInformation(string filename_RegularData, string filename_HandcraftedData)
        {
            Dictionary<Mnemonic, List<AsmSignatureInformation>> data = [];
            Dictionary<Mnemonic, List<Arch>> arch = [];
            Dictionary<Mnemonic, string> htmlRef = [];
            Dictionary<Mnemonic, string> description = [];

        /// <summary>
        /// Add (and overwrite) return true if an existing signature element is overwritten;
        /// </summary>
        /// <param name="asmSignatureElement">AsmSignatureInformation to add or overwrite.</param>
        /// <param name="data">Dictionary of mnemonic to signature list (passed by ref for update).</param>
        /// <returns>true if existing signature was overwritten; false if new entry was added.</returns>
        /// <remarks>
        /// Helper closure used during data loading to manage signature dictionaries.
        /// Removes existing signature if present, then adds the new one.
        /// </remarks>
        /// <!-- LLM-ANNOTATION -->
        /// LLM KEYWORDS: signature management, dictionary update, overwrite logic, helper closure
        /// USED IN: CalcSignatureInformation.LoadRegularData, LoadHandcraftedData
        /// SEE ALSO: CreateAsmSignatureElement, GetSignatures
        bool Add(AsmSignatureInformation asmSignatureElement, ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data)
            {
                //LanguageServer.LogInfo($"MnemonicStore: Add: {asmSignatureElement.SignatureInformation.Label}; number of elements before {this.data_.Count}");
                bool result = false;

                if (data.TryGetValue(asmSignatureElement.Mnemonic, out List<AsmSignatureInformation>? signatureElementList))
                {
                    result = signatureElementList.Remove(asmSignatureElement);
                    signatureElementList.Add(asmSignatureElement);
                }
                else
                {
                    data.Add(asmSignatureElement.Mnemonic, [asmSignatureElement]);
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
                    string? line;
                    while ((line = file.ReadLine()) is not null)
                    {
                        if ((line.Length > 0) && (!line.StartsWith(';')))
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
                                    //LanguageServer.LogInfo($"MnemonicStore: adding AsmSignatureInformation {se.SignatureInformation.Label}");
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
                        HashSet<Arch> archs = [];
                        foreach (AsmSignatureInformation signatureElement in value)
                        {
                            archs.UnionWith(signatureElement.Arch);
                        }
                        arch[key] = [.. archs];
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
                    string? line;
                    while ((line = file.ReadLine()) is not null)
                    {
                        if ((line.Length > 0) && (!line.StartsWith(';')))
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
                        HashSet<Arch> archs = [];
                        foreach (AsmSignatureInformation signatureElement in value)
                        {
                            archs.UnionWith(signatureElement.Arch);
                        }
                        arch[key] = [.. archs];
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

        /// <summary>
        /// Constrain signature list based on operand types and selected architectures.
        /// Filters out incompatible signatures before presenting to user.
        /// </summary>
        /// <param name="data">All available signatures for a mnemonic.</param>
        /// <param name="operands2">Current operand list from parser.</param>
        /// <param name="selectedArchitectures2">Architectures enabled in options.</param>
        /// <returns>Filtered sequence of compatible signatures.</returns>
        /// <remarks>
        /// Constraint logic:
        ///   1. Remove signatures not supporting selected architectures
        ///   2. Check each operand against signature operand definitions
        ///   3. Allow only signatures matching all operand constraints
        /// 
        /// Returns via yield return for deferred execution and memory efficiency.
        /// </remarks>
        /// <!-- LLM-ANNOTATION -->
        /// LLM KEYWORDS: signature constraint, architecture filter, operand matching, filtering algorithm
        /// USED IN: LanguageServer.GetTextDocumentSignatureHelp
        /// SEE ALSO: LanguageServer.Constrain_Signatures, Is_Allowed
        public bool IsMnemonicSwitchedOn(Mnemonic mnemonic)
        {
            return this.mnemonics_switched_on_.Contains(mnemonic);
        }

        /// <summary>
        /// Gets all allowed mnemonics based on currently enabled architectures.
        /// Returns frozen set for thread-safe read access.
        /// </summary>
        /// <returns>FrozenSet of mnemonics enabled by selected architectures.</returns>
        /// <remarks>
        /// Called during code completion and signature help to filter available instructions.
        /// Uses mnemonics_switched_on_ dictionary populated during constructor.
        /// </remarks>
        /// <!-- LLM-ANNOTATION -->
        /// LLM KEYWORDS: allowed mnemonics, architecture filter, frozen set, immutable collection
        /// USED IN: LanguageServer.GetTextDocumentCompletion, LanguageServer.Constrain_Signatures
        /// SEE ALSO: Get_Allowed_Registers, IsMnemonicSwitchedOn, mnemonics_switched_on_
        public FrozenSet<Mnemonic> Get_Allowed_Mnemonics()
        {
            return this.mnemonics_switched_on_;
        }

        private HashSet<Mnemonic> CalcMnemonicsSwitchedOn()
        {
            HashSet<Mnemonic> result = [];

            HashSet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
            foreach (Mnemonic mnemonic in Enum.GetValues<Mnemonic>())
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

        public FrozenSet<Rn> Get_Allowed_Registers()
        {
            return this.register_switched_on_;
        }

        private HashSet<Rn> CalcRegisterSwitchedOn()
        {
            HashSet<Rn> result = [];

            HashSet<Arch> arch_switched_on = this.options.Get_Arch_Switched_On();
            foreach (Rn reg in Enum.GetValues<Rn>())
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
