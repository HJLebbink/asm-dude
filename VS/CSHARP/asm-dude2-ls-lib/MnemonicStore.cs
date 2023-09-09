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
        private readonly IDictionary<Mnemonic, IList<AsmSignatureInformation>> data_;
        private readonly IDictionary<Mnemonic, IList<Arch>> arch_;
        private readonly IDictionary<Mnemonic, string> htmlRef_;
        private readonly IDictionary<Mnemonic, string> description_;
        private readonly TraceSource traceSource;
        private readonly AsmLanguageServerOptions options;

        private readonly ISet<Mnemonic> mnemonics_switched_on_;
        private readonly ISet<Rn> register_switched_on_;

        public MnemonicStore(string filename_RegularData, string filename_HandcraftedData, TraceSource traceSource, AsmLanguageServerOptions options)
        {
            this.traceSource = traceSource;
            this.options = options;
            LogInfo($"MnemonicStore: constructor: regularData = {filename_RegularData}; handcraftedData = {filename_HandcraftedData}");

            this.data_ = new Dictionary<Mnemonic, IList<AsmSignatureInformation>>();
            this.arch_ = new Dictionary<Mnemonic, IList<Arch>>();
            this.htmlRef_ = new Dictionary<Mnemonic, string>();
            this.description_ = new Dictionary<Mnemonic, string>();
            if (File.Exists(filename_RegularData))
            {
                LoadRegularData(filename_RegularData);
            } 
            else
            {
                LogError($"MnemonicStore: constructor: regularData = {filename_RegularData} does not exist");
            }

            if (filename_HandcraftedData != null)
            {
                if (File.Exists(filename_HandcraftedData))
                {
                    LoadHandcraftedData(filename_HandcraftedData);
                }
                else
                {
                    LogError($"MnemonicStore: constructor: handcraftedData = {filename_HandcraftedData} does not exist");
                }
            }

            this.mnemonics_switched_on_ = new HashSet<Mnemonic>();
            this.UpdateMnemonicSwitchedOn();

            this.register_switched_on_ = new HashSet<Rn>();
            this.UpdateRegisterSwitchedOn();
        }

        private void LogInfo(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, msg);
        }
        private void LogWarning(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Warning, 0, msg);
        }
        private void LogError(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Error, 0, msg);
        }

        public bool HasElement(Mnemonic mnemonic)
        {
            return this.data_.ContainsKey(mnemonic);
        }

        public IEnumerable<AsmSignatureInformation> GetSignatures(Mnemonic mnemonic)
        {
            return this.data_.TryGetValue(mnemonic, out IList<AsmSignatureInformation> list) ? list : Enumerable.Empty<AsmSignatureInformation>();
        }

        public IEnumerable<Arch> GetArch(Mnemonic mnemonic)
        {
            return this.arch_.TryGetValue(mnemonic, out IList<Arch> value) ? value : Enumerable.Empty<Arch>();
        }

        public string GetHtmlRef(Mnemonic mnemonic)
        {
            return this.htmlRef_.TryGetValue(mnemonic, out string value) ? value : string.Empty;
        }

        public void SetHtmlRef(Mnemonic mnemonic, string value)
        {
            this.htmlRef_[mnemonic] = value;
        }

        public void SetDescription(Mnemonic mnemonic, string value)
        {
            this.description_[mnemonic] = value;
            if (this.data_.ContainsKey(mnemonic))
            {
                foreach (AsmSignatureInformation e in this.data_[mnemonic])
                {
                    e.SignatureInformation.Documentation = value;
                }
            }
        }

        public string GetDescription(Mnemonic mnemonic)
        {
            return this.description_.TryGetValue(mnemonic, out string value) ? value : string.Empty;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<Mnemonic, IList<AsmSignatureInformation>> element in this.data_)
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

        /// <summary>
        /// Add (and overwrite) return true if an existing signature element is overwritten;
        /// </summary>
        /// <param name="asmSignatureElement"></param>
        private bool Add(AsmSignatureInformation asmSignatureElement)
        {
            //this.LogInfo($"MnemonicStore: Add: {asmSignatureElement.SignatureInformation.Label}; number of elements before {this.data_.Count}");

            bool result = false;

            if (this.data_.TryGetValue(asmSignatureElement.Mnemonic, out IList<AsmSignatureInformation> signatureElementList))
            {
                if (signatureElementList.Contains(asmSignatureElement))
                {
                    signatureElementList.Remove(asmSignatureElement);
                    result = true;
                }
                signatureElementList.Add(asmSignatureElement);
            }
            else
            {
                this.data_.Add(asmSignatureElement.Mnemonic, new List<AsmSignatureInformation> { asmSignatureElement });
            }
            //this.LogInfo($"MnemonicStore: Add: number of elements after {this.data_.Count}");
            return result;
        }

        private AsmSignatureInformation CreateAsmSignatureElement(Mnemonic mnemonic, string args, string arch, string sign, string doc)
        {
            // EG: mnemonic=VADDPS
            // args=XMM{K}{Z},XMM,XMM/M128/M32BCST
            // arch=AVX512_VL,AVX512_F
            // sign=VADDPS XMM1{K1}{Z},XMM2,XMM3/M128/M32BCST
            // doc=Add packed SP FP values from xmm3/m128/m32bcst to xmm2 and store result in xmm1 with writemask k1.

            IList<AsmSignatureEnum> ParseOperands(string str)
            {
                var result = new List<AsmSignatureEnum>();
                foreach (string op in str.Split('/'))
                {
                    result.Add(AsmSignatureTools.Parse_Operand_Type_Enum(op, true)[0]);
                }
                return result;
            }

            string ParamDoc(IList<AsmSignatureEnum> x)
            {
                StringBuilder argDoc = new StringBuilder();
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
                LogError($"MnemonicStore:CreateAsmSignatureElement: inconsistent signature information: args={args}; parameterOffsets={parameterOffsets}");
                for (int i = 0; i < operands.Length; ++i)
                {
                    LogError($"MnemonicStore:CreateAsmSignatureElement: operands[{i}]={operands[i]}");
                }
                for (int i = 0; i < parameterOffsets.Length; ++i)
                {
                    LogError($"MnemonicStore:CreateAsmSignatureElement: parameterOffsets[{i}]={parameterOffsets[i]}; sign={sign}");
                }
            }

            var operandList = new List<IList<AsmSignatureEnum>>();

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
                Arch = ArchTools.ParseArchList(arch, false, true),
                Operands = operandList,
                SignatureInformation = new SignatureInformation
                {
                    Label = sign,
                    Parameters = parameters.ToArray<ParameterInformation>(),
                    Documentation = doc,
                }
            };
        }

        private void LoadRegularData(string filename)
        {
            //AsmDudeToolsStatic.Output_INFO("MnemonicStore:loadRegularData: filename=" + filename);
            try
            {
                StreamReader file = new StreamReader(filename);
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
                                //AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: unknown mnemonic in line: " + line);
                            }
                            else
                            {
                                if (!this.description_.ContainsKey(mnemonic))
                                {
                                    this.description_.Add(mnemonic, columns[2]);
                                }
                                else
                                {
                                    // this happens when the mnemonic is defined in multiple files, using the data from the first file
                                    //AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: mnemonic " + mnemonic + " already has a description");
                                }
                                if (!this.htmlRef_.ContainsKey(mnemonic))
                                {
                                    this.htmlRef_.Add(mnemonic, columns[3]);
                                }
                                else
                                {
                                    // this happens when the mnemonic is defined in multiple files, using the data from the first file
                                    //AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: mnemonic " + mnemonic + " already has a html ref");
                                }
                            }
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                LogWarning("MnemonicStore:loadRegularData: unknown mnemonic in line: " + line);
                            }
                            else
                            {
                                var se = this.CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                //LogInfo($"MnemonicStore: adding AsmSignatureInformation {se.SignatureInformation.Label}");
                                if (this.Add(se))
                                {
                                    LogWarning("MnemonicStore:loadRegularData: signature already exists" + se.ToString());
                                }
                            }
                        }
                        else
                        {
                            LogWarning("MnemonicStore:loadRegularData: s.Length=" + columns.Length + "; funky line" + line);
                        }
                    }
                }
                file.Close();

                #region Fill Arch
                foreach (KeyValuePair<Mnemonic, IList<AsmSignatureInformation>> pair in this.data_)
                {
                    ISet<Arch> archs = new HashSet<Arch>();
                    foreach (AsmSignatureInformation signatureElement in pair.Value)
                    {
                        if (signatureElement.Arch == null)
                        {
                            this.LogError("signatureElement.arch_ is null");
                        } else
                        {
                            foreach (Arch arch in signatureElement.Arch)
                            {
                                if (arch == Arch.ARCH_NONE)
                                {
                                   // LogWarning("MnemonicStore:loadRegularData: found ARCH NONE.");
                                }
                                else
                                {
                                    archs.Add(arch);
                                }
                            }
                        }
                    }
                    IList<Arch> list = new List<Arch>();
                    foreach (Arch a in archs)
                    {
                        list.Add(a);
                    }
                    this.arch_[pair.Key] = list;
                }
                #endregion
            }
            catch (FileNotFoundException)
            {
                this.LogError("MnemonicStore:loadRegularData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                this.LogError("MnemonicStore:loadRegularData: error while reading file \"" + filename + "\"." + e);
            }
        }

        private void LoadHandcraftedData(string filename)
        {
            //AsmDudeToolsStatic.Output_INFO("MnemonicStore:load_data_intel: filename=" + filename);
            try
            {
                StreamReader file = new StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 4)
                        { // general description
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[1], false);

                            //LogInfo($"MnemonicStore:LoadHandcraftedData? line={line}, mnemonic={mnemonic}");

                            if (mnemonic == Mnemonic.NONE)
                            {
                                LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                            }
                            else
                            {
                                if (this.description_.ContainsKey(mnemonic))
                                {
                                    this.description_.Remove(mnemonic);
                                }
                                //LogInfo($"MnemonicStore:LoadHandcraftedData adding description for mnemonic={mnemonic}; descr={columns[2]}");
                                this.description_.Add(mnemonic, columns[2]);

                                if (this.htmlRef_.ContainsKey(mnemonic))
                                {
                                    this.htmlRef_.Remove(mnemonic);
                                }
                                //LogInfo($"LoadHandcraftedData adding description for mnemonic={mnemonic}; url={columns[3]}");
                                this.htmlRef_.Add(mnemonic, columns[3]);
                            }
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                            }
                            else
                            {
                                var se = this.CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                //LogInfo($"MnemonicStore: LoadHandcraftedData: adding AsmSignatureInformation {se.SignatureInformation.Label}");
                                if (this.Add(se))
                                {
                                    LogWarning("MnemonicStore:LoadHandcraftedData: signature already exists" + se.ToString());
                                }
                            }
                        }
                        else
                        {
                            LogWarning("MnemonicStore:loadHandcraftedData: s.Length=" + columns.Length + "; funky line" + line);
                        }
                    }
                }
                file.Close();

                #region Fill Arch
                foreach (KeyValuePair<Mnemonic, IList<AsmSignatureInformation>> pair in this.data_)
                {
                    ISet<Arch> archs = new HashSet<Arch>();
                    foreach (AsmSignatureInformation signatureElement in pair.Value)
                    {
                        foreach (Arch arch in signatureElement.Arch)
                        {
                            archs.Add(arch);
                        }
                    }
                    IList<Arch> list = new List<Arch>();
                    foreach (Arch a in archs)
                    {
                        list.Add(a);
                    }
                    this.arch_[pair.Key] = list;
                }
                #endregion
            }
            catch (FileNotFoundException)
            {
                this.LogError("MnemonicStore:LoadHandcraftedData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                this.LogError("MnemonicStore:LoadHandcraftedData: error while reading file \"" + filename + "\"." + e);
            }
        }

        public bool MnemonicSwitchedOn(Mnemonic mnemonic)
        {
            return this.mnemonics_switched_on_.Contains(mnemonic);
        }

        public IEnumerable<Mnemonic> Get_Allowed_Mnemonics()
        {
            return this.mnemonics_switched_on_;
        }

        public void UpdateMnemonicSwitchedOn()
        {
            this.mnemonics_switched_on_.Clear();
            ISet<Arch> selectedArchs = this.options.Get_Arch_Switched_On();
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
                foreach (Arch a in this.GetArch(mnemonic))
                {
                    if (selectedArchs.Contains(a))
                    {
                        this.mnemonics_switched_on_.Add(mnemonic);
                        break;
                    }
                }
            }
        }

        public bool RegisterSwitchedOn(Rn reg)
        {
            return this.register_switched_on_.Contains(reg);
        }

        public IEnumerable<Rn> Get_Allowed_Registers()
        {
            return this.register_switched_on_;
        }

        public void UpdateRegisterSwitchedOn()
        {
            this.register_switched_on_.Clear();
            ISet<Arch> selectedArchs = this.options.Get_Arch_Switched_On();
            foreach (Rn reg in Enum.GetValues(typeof(Rn)))
            {
                if (reg != Rn.NOREG)
                {
                    if (selectedArchs.Contains(RegisterTools.GetArch(reg)))
                    {
                        this.register_switched_on_.Add(reg);
                    }
                }
            }
        }
    }
}
