// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Diagnostics;
using System.Xml;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Collections.Generic;

using AsmTools;
using AsmDude.Tools;
using Microsoft.VisualStudio.Shell;
using AsmDude.SignatureHelp;
using Amib.Threading;

namespace AsmDude {

    public sealed class AsmDudeTools : IDisposable {

        private XmlDocument _xmlData;
        private IDictionary<string, AsmTokenType> _type;
        private IDictionary<string, AssemblerEnum> _assembler;
        private IDictionary<string, Arch> _arch;
        private IDictionary<string, string> _description;
        private readonly ErrorListProvider _errorListProvider;

        private readonly MnemonicStore _mnemonicStore;
        private readonly SmartThreadPool _smartThreadPool;

        #region Singleton Stuff
        private static readonly Lazy<AsmDudeTools> lazy = new Lazy<AsmDudeTools>(() => new AsmDudeTools());
        public static AsmDudeTools Instance { get { return lazy.Value; } }
        #endregion Singleton Stuff


        /// <summary>
        /// Singleton pattern: use AsmDudeTools.Instance for the instance of this class
        /// </summary>
        private AsmDudeTools() {
            //AsmDudeToolsStatic.Output(string.Format("INFO: AsmDudeTools constructor"));

            #region Initialize ErrorListProvider
            IServiceProvider serviceProvider = new ServiceProvider(Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
            this._errorListProvider = new ErrorListProvider(serviceProvider)
            {
                ProviderName = "Asm Errors",
                ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode)
            };
            #endregion

            this._smartThreadPool = new SmartThreadPool();
            //this._smartThreadPool.Start();

            #region load signature store
            string path = AsmDudeToolsStatic.Get_Install_Path() + "Resources" + Path.DirectorySeparatorChar;
            //string filename = path + "mnemonics-nasm.txt";
            string filename_Regular = path + "signature-june2016.txt";
            string filename_Hand = path + "signature-hand-1.txt";
            this._mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand);
            #endregion

            this.Init_Data();

            #region Experiments

            if (false) {
                string filename2 = AsmDudeToolsStatic.Get_Install_Path() + "Resources" + Path.DirectorySeparatorChar + "mnemonics-nasm.txt";
                MnemonicStore store2 = new MnemonicStore(filename2, null);

                ISet<String> archs = new SortedSet<String>();
                IDictionary<string, string> signaturesIntel = new Dictionary<string, string>();
                IDictionary<string, string> signaturesNasm = new Dictionary<string, string>();

                foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                    IList<AsmSignatureElement> intel = this._mnemonicStore.getSignatures(mnemonic);
                    IList<AsmSignatureElement> nasm = store2.getSignatures(mnemonic);

                    signaturesIntel.Clear();
                    signaturesNasm.Clear();

                    foreach (AsmSignatureElement e in intel) {
                        string instruction = e.Mnemonic.ToString() + " " + e.Operands_Str;
                        if (signaturesIntel.ContainsKey(instruction)) {
                            AsmDudeToolsStatic.Output("WARNING: Intel " + instruction + ": is already present with arch "+ signaturesIntel[instruction] +"; new arch "+ e.Arch_Str);
                        } else {
                            signaturesIntel.Add(instruction, e.Arch_Str);
                        }
                    }
                    foreach (AsmSignatureElement e in nasm) {
                        string instruction = e.Mnemonic.ToString() + " " + e.Operands_Str;
                        if (signaturesNasm.ContainsKey(instruction)) {
                           // AsmDudeToolsStatic.Output("WARNING: Nasm " + instruction + ": is already present with arch " + signaturesNasm[instruction] + "; new arch " + e.archStr);
                        } else {
                            signaturesNasm.Add(instruction, e.Arch_Str);
                        }
                    }

                    foreach (AsmSignatureElement e in intel) {
                        string instruction = e.Mnemonic.ToString() + " " + e.Operands_Str;


                        //AsmDudeToolsStatic.Output("Intel " + instruction + ": arch" + e.archStr);
                        if ((e.Arch_Str == null) || (e.Arch_Str.Length == 0)) {
                            if (signaturesNasm.ContainsKey(instruction)) {
                                AsmDudeToolsStatic.Output("Intel " + instruction + " has no arch, but NASM has \"" + signaturesNasm[instruction] + "\".");
                            } else {
                                if (signaturesNasm.Count == 1) {
                                    AsmDudeToolsStatic.Output("Intel " + instruction + " has no arch, but NASM has \"" + signaturesNasm.GetEnumerator().Current+"\".");
                                } else {
                                    AsmDudeToolsStatic.Output("Intel " + instruction + " has no arch:");
                                    foreach (KeyValuePair<string, string> pair in signaturesNasm) {
                                        AsmDudeToolsStatic.Output("\tNASM has " + pair.Key + ": \"" + pair.Value + "\".");
                                    }
                                    AsmDudeToolsStatic.Output("    ----");
                                }
                            }
                        }
                    }

                    if (false) {
                        if (intel.Count != nasm.Count) {
                            foreach (AsmSignatureElement e in intel) {
                                AsmDudeToolsStatic.Output("INTEL " + mnemonic + ": " + e);
                            }
                            foreach (AsmSignatureElement e in nasm) {
                                AsmDudeToolsStatic.Output("NASM " + mnemonic + ": " + e);
                            }
                        }
                    }
                }
                foreach (String str in archs) {
                    AsmDudeToolsStatic.Output("INTEL arch " + str);
                }
            }
            if (false) {
                foreach (Arch arch in Enum.GetValues(typeof(Arch))) {
                    int counter = 0;
                    ISet<Mnemonic> usedMnemonics = new HashSet<Mnemonic>();
                    foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                        if (this.Mnemonic_Store.getArch(mnemonic).Contains(arch)) {
                            //AsmDudeToolsStatic.Output("INFO: AsmDudeTools constructor: arch="+arch+"; mnemonic=" + mnemonic);
                            counter++;
                            usedMnemonics.Add(mnemonic);
                        }
                    }
                    string str = "";
                    foreach (Mnemonic mnemonic in usedMnemonics) {
                        str += mnemonic.ToString() + ",";
                    }
                    AsmDudeToolsStatic.Output("INFO: AsmDudeTools constructor: Architecture Option " + arch + " enables mnemonics "+str);
                }
            }

            if (false) {
                foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                    string keyword = mnemonic.ToString().ToUpper();
                    if (this._description.ContainsKey(keyword)) {
                        string description = this._description[keyword];
                        string reference = this.Get_Url(keyword);

                        this.Mnemonic_Store.setHtmlRef(mnemonic, reference);

                    }
                }
                AsmDudeToolsStatic.Output(this.Mnemonic_Store.ToString());
            }
            if (false) {

                ISet<string> archs = new HashSet<string>();

                foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                    if (!this._mnemonicStore.hasElement(mnemonic)) {
                        AsmDudeToolsStatic.Output("INFO: AsmDudeTools constructor: mnemonic " + mnemonic + " is not present");
                    }
                    foreach (AsmSignatureElement e in this._mnemonicStore.getSignatures(mnemonic)) {
                        foreach (string s in e.Arch_Str.Split(',')) {
                            archs.Add(s.Trim());
                        }
                    }
                }

                foreach (string s in archs) {
                    AsmDudeToolsStatic.Output(s+ ",");
                }

            }
            #endregion
        }

        #region Public Methods

        public ErrorListProvider Error_List_Provider { get { return this._errorListProvider; } }

        public MnemonicStore Mnemonic_Store { get { return this._mnemonicStore; } }

        public SmartThreadPool Thread_Pool { get { return this._smartThreadPool; } }

        public ICollection<string> Get_Keywords() {
            if (this._type == null) Init_Data();
            return this._type.Keys;
        }

        public AsmTokenType Get_Token_Type(string keyword) {
            string keyword2 = keyword.ToUpper();
            Mnemonic mnemonic = AsmSourceTools.parseMnemonic(keyword2);
            if (mnemonic != Mnemonic.UNKNOWN) {
                if (AsmSourceTools.isJump(mnemonic)) {
                    return AsmTokenType.Jump;
                }
                return AsmTokenType.Mnemonic;
            }

            if (this._type.TryGetValue(keyword2, out var tokenType)) {
                return tokenType;
            }
            return AsmTokenType.UNKNOWN;
        }

        public AssemblerEnum Get_Assembler(string keyword) {
            if (this._assembler.TryGetValue(keyword, out var value)) {
                return value;
            }
            return AssemblerEnum.UNKNOWN;
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string Get_Url(string keyword) {
            // no need to pre-process this information.
            try {
                string keywordUpper = keyword.ToUpper();
                Mnemonic mnemonic = AsmSourceTools.parseMnemonic(keyword);
                if (mnemonic != Mnemonic.UNKNOWN) {
                    string url = this.Mnemonic_Store.getHtmlRef(mnemonic);
                    //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:getUrl: keyword {1}; url {2}.", this.ToString(), keyword, url));
                    return url;
                }
                return "";
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:getUrl: exception {1}.", this.ToString(), e.ToString()));
                return "";
            }
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string Get_Description(string keyword) {
            if (!this._description.TryGetValue(keyword, out string description)) {
                description = "";
            }
            return description;
        }

        /// <summary>
        /// Get architecture of the provided keyword
        /// </summary>
        public Arch Get_Architecture(string keyword) {
            return this._arch[keyword.ToUpper()];
        }

        public void Invalidate_Data() {
            this._xmlData = null;
            this._type = null;
            this._description = null;
        }


        #endregion Public Methods
        #region Private Methods

        private void Init_Data() {
            this._type = new Dictionary<string, AsmTokenType>();
            this._arch = new Dictionary<string, Arch>();
            this._assembler = new Dictionary<string, AssemblerEnum>();
            this._description = new Dictionary<string, string>();

            // fill the dictionary with keywords
            XmlDocument xmlDoc = this.Get_Xml_Data();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found misc with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found misc " + name);
                    this._type[name] = AsmTokenType.Misc;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._description[name] = this.Retrieve_Description(node);
                }
            }

            foreach (XmlNode node in xmlDoc.SelectNodes("//directive")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found directive with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found directive " + name);
                    this._type[name] = AsmTokenType.Directive;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._assembler[name] = this.Retrieve_Assembler(node);
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//register")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found register with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found register " + name);
                    this._type[name] = AsmTokenType.Register;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._description[name] = Retrieve_Description(node);
                }
            }
        }

        private Arch Retrieve_Arch(XmlNode node) {
            try {
                var archAttribute = node.Attributes["arch"];
                if (archAttribute == null) {
                    return Arch.NONE;
                } else {
                    return AsmTools.ArchTools.parseArch(archAttribute.Value.ToUpper());
                }
            } catch (Exception) {
                return Arch.NONE;
            }
        }

        private AssemblerEnum Retrieve_Assembler(XmlNode node) {
            try {
                var archAttribute = node.Attributes["tool"];
                if (archAttribute == null) {
                    return AssemblerEnum.UNKNOWN;
                } else {
                    return AsmTools.AsmSourceTools.parseAssembler(archAttribute.Value.ToUpper());
                }
            } catch (Exception) {
                return AssemblerEnum.UNKNOWN;
            }
        }

        private string Retrieve_Description(XmlNode node) {
            try {
                XmlNode node2 = node.SelectSingleNode("./description");
                if (node2 == null) return "";
                string text = node2.InnerText.Trim();
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getDescription: keyword {1} yields {2}", this.ToString(), keyword, text));
                return text;
            } catch (Exception) {
                return "";
            }
        }

        private XmlDocument Get_Xml_Data() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
            if (this._xmlData == null) {
                string filename = AsmDudeToolsStatic.Get_Install_Path() + "Resources" + Path.DirectorySeparatorChar + "AsmDudeData.xml";
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: AsmDudeTools:getXmlData: going to load file \"{0}\"", filename));
                try {
                    this._xmlData = new XmlDocument();
                    this._xmlData.Load(filename);
                } catch (FileNotFoundException) {
                    MessageBox.Show("ERROR: AsmTokenTagger: could not find file \"" + filename + "\".");
                } catch (XmlException) {
                    MessageBox.Show("ERROR: AsmTokenTagger: xml error while reading file \"" + filename + "\".");
                } catch (Exception e) {
                    MessageBox.Show("ERROR: AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
                }
            }
            return this._xmlData;
        }

        public void Dispose() {
            this._errorListProvider.Dispose();
            this._smartThreadPool.Dispose();
        }

        #endregion
    }
}