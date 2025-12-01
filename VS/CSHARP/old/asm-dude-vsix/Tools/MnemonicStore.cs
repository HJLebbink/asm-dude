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

namespace AsmDude.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using AsmDude.SignatureHelp;
    using AsmTools;

    public class MnemonicStore
    {
        private readonly IDictionary<Mnemonic, IList<AsmSignatureElement>> data_;
        private readonly IDictionary<Mnemonic, IList<Arch>> arch_;
        private readonly IDictionary<Mnemonic, string> htmlRef_;
        private readonly IDictionary<Mnemonic, string> description_;

        public MnemonicStore(string filename_RegularData, string filename_HandcraftedData)
        {
            this.data_ = new Dictionary<Mnemonic, IList<AsmSignatureElement>>();
            this.arch_ = new Dictionary<Mnemonic, IList<Arch>>();
            this.htmlRef_ = new Dictionary<Mnemonic, string>();
            this.description_ = new Dictionary<Mnemonic, string>();

            this.LoadRegularData(filename_RegularData);
            if (filename_HandcraftedData != null)
            {
                this.LoadHandcraftedData(filename_HandcraftedData);
            }
        }

        public bool HasElement(Mnemonic mnemonic)
        {
            return this.data_.ContainsKey(mnemonic);
        }

        public IEnumerable<AsmSignatureElement> GetSignatures(Mnemonic mnemonic)
        {
            return this.data_.TryGetValue(mnemonic, out IList<AsmSignatureElement> list) ? list : Enumerable.Empty<AsmSignatureElement>();
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
                foreach (AsmSignatureElement e in this.data_[mnemonic])
                {
                    e.Documentation = value;
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
            foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> element in this.data_)
            {
                Mnemonic mnemonic = element.Key;
                string s1 = mnemonic.ToString();
                string s6 = this.htmlRef_[mnemonic];

                foreach (AsmSignatureElement sig in element.Value)
                {
                    string s2 = sig.Operands_Str;
                    string s3 = sig.Arch_Str;
                    string s4 = sig.Sigature_Doc();
                    string s5 = sig.Documentation;
                    sb.AppendLine(s1 + "\t" + s2 + "\t" + s3 + "\t" + s4 + "\t" + s5 + "\t" + s6);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Add (and overwrite) return true if an existing signature element is overwritten;
        /// </summary>
        /// <param name="asmSignatureElement"></param>
        private bool Add(AsmSignatureElement asmSignatureElement)
        {
            Mnemonic mnemonic = asmSignatureElement.mnemonic;
            bool result = false;

            if (this.data_.TryGetValue(mnemonic, out IList<AsmSignatureElement> signatureElementList))
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
                this.data_.Add(mnemonic, new List<AsmSignatureElement> { asmSignatureElement });
            }
            return result;
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
                            #region
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
                            #endregion
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            #region
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: unknown mnemonic in line: " + line);
                            }
                            else
                            {
                                AsmSignatureElement se = new AsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                if (this.Add(se))
                                {
                                    AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: signature already exists" + se.ToString());
                                }
                            }
                            #endregion
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: s.Length=" + columns.Length + "; funky line" + line);
                        }
                    }
                }
                file.Close();

                #region Fill Arch
                foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> pair in this.data_)
                {
                    ISet<Arch> archs = new HashSet<Arch>();
                    foreach (AsmSignatureElement signatureElement in pair.Value)
                    {
                        foreach (Arch arch in signatureElement.Arch)
                        {
                            if (arch == Arch.ARCH_NONE)
                            {
                                AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadRegularData: found ARCH NONE.");
                            }
                            else
                            {
                                archs.Add(arch);
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
                AsmDudeToolsStatic.Output_ERROR("MnemonicStore:loadRegularData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("MnemonicStore:loadRegularData: error while reading file \"" + filename + "\"." + e);
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
                            #region
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[1], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                            }
                            else
                            {
                                if (this.description_.ContainsKey(mnemonic))
                                {
                                    this.description_.Remove(mnemonic);
                                }
                                this.description_.Add(mnemonic, columns[2]);

                                if (this.htmlRef_.ContainsKey(mnemonic))
                                {
                                    this.htmlRef_.Remove(mnemonic);
                                }
                                this.htmlRef_.Add(mnemonic, columns[3]);
                            }
                            #endregion
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            #region
                            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadHandcraftedData: unknown mnemonic in line" + line);
                            }
                            else
                            {
                                AsmSignatureElement se = new AsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                this.Add(se);
                            }
                            #endregion
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_WARNING("MnemonicStore:loadHandcraftedData: s.Length=" + columns.Length + "; funky line" + line);
                        }
                    }
                }
                file.Close();

                #region Fill Arch
                foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> pair in this.data_)
                {
                    ISet<Arch> archs = new HashSet<Arch>();
                    foreach (AsmSignatureElement signatureElement in pair.Value)
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
                AsmDudeToolsStatic.Output_ERROR("MnemonicStore:LoadHandcraftedData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("MnemonicStore:LoadHandcraftedData: error while reading file \"" + filename + "\"." + e);
            }
        }
    }
}
