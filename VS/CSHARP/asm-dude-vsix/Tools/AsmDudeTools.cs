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
            this._errorListProvider = new ErrorListProvider(serviceProvider);
            this._errorListProvider.ProviderName = "Asm Errors";
            this._errorListProvider.ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode);
            #endregion

            this._smartThreadPool = new SmartThreadPool();

            #region load signature store
            string path = AsmDudeToolsStatic.getInstallPath() + "Resources" + Path.DirectorySeparatorChar;
            //string filename = path + "mnemonics-nasm.txt";
            string filename_Regular = path + "signature-june2016.txt";
            string filename_Hand = path + "signature-hand-1.txt";
            this._mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand);
            #endregion

            this.initData();

            #region Experiments

            if (false) {
                string filename2 = AsmDudeToolsStatic.getInstallPath() + "Resources" + Path.DirectorySeparatorChar + "mnemonics-nasm.txt";
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
                        string instruction = e.mnemonic.ToString() + " " + e.operandsStr;
                        if (signaturesIntel.ContainsKey(instruction)) {
                            AsmDudeToolsStatic.Output("WARNING: Intel " + instruction + ": is already present with arch "+ signaturesIntel[instruction] +"; new arch "+ e.archStr);
                        } else {
                            signaturesIntel.Add(instruction, e.archStr);
                        }
                    }
                    foreach (AsmSignatureElement e in nasm) {
                        string instruction = e.mnemonic.ToString() + " " + e.operandsStr;
                        if (signaturesNasm.ContainsKey(instruction)) {
                           // AsmDudeToolsStatic.Output("WARNING: Nasm " + instruction + ": is already present with arch " + signaturesNasm[instruction] + "; new arch " + e.archStr);
                        } else {
                            signaturesNasm.Add(instruction, e.archStr);
                        }
                    }

                    foreach (AsmSignatureElement e in intel) {
                        string instruction = e.mnemonic.ToString() + " " + e.operandsStr;


                        //AsmDudeToolsStatic.Output("Intel " + instruction + ": arch" + e.archStr);
                        if ((e.archStr == null) || (e.archStr.Length == 0)) {
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
                        if (this.mnemonicStore.getArch(mnemonic).Contains(arch)) {
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
                        string reference = this.getUrl(keyword);

                        this.mnemonicStore.setHtmlRef(mnemonic, reference);

                    }
                }
                AsmDudeToolsStatic.Output(this.mnemonicStore.ToString());
            }
            if (false) {

                ISet<string> archs = new HashSet<string>();

                foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                    if (!this._mnemonicStore.hasElement(mnemonic)) {
                        AsmDudeToolsStatic.Output("INFO: AsmDudeTools constructor: mnemonic " + mnemonic + " is not present");
                    }
                    foreach (AsmSignatureElement e in this._mnemonicStore.getSignatures(mnemonic)) {
                        foreach (string s in e.archStr.Split(',')) {
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

        public ErrorListProvider errorListProvider { get { return this._errorListProvider; } }

        public MnemonicStore mnemonicStore { get { return this._mnemonicStore; } }

        public SmartThreadPool threadPool { get { return this._smartThreadPool; } }

        public ICollection<string> getKeywords() {
            if (this._type == null) initData();
            return this._type.Keys;
        }

        public AsmTokenType getTokenType(string keyword) {
            string keyword2 = keyword.ToUpper();
            Mnemonic mnemonic = AsmSourceTools.parseMnemonic(keyword2);
            if (mnemonic != Mnemonic.UNKNOWN) {
                if (AsmSourceTools.isJump(mnemonic)) {
                    return AsmTokenType.Jump;
                }
                return AsmTokenType.Mnemonic;
            } 
            AsmTokenType tokenType;
            if (this._type.TryGetValue(keyword2, out tokenType)) {
                return tokenType;
            }
            return AsmTokenType.UNKNOWN;
        }

        public AssemblerEnum getAssembler(string keyword) {
            AssemblerEnum value;
            if (this._assembler.TryGetValue(keyword, out value)) {
                return value;
            }
            return AssemblerEnum.UNKNOWN;
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string getUrl(string keyword) {
            // no need to pre-process this information.
            try {
                string keywordUpper = keyword.ToUpper();
                Mnemonic mnemonic = AsmSourceTools.parseMnemonic(keyword);
                if (mnemonic != Mnemonic.UNKNOWN) {
                    string url = this.mnemonicStore.getHtmlRef(mnemonic);
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
        public string getDescription(string keyword) {
            string description;
            if (!this._description.TryGetValue(keyword, out description)) {
                description = "";
            }
            return description;
        }

        /// <summary>
        /// Get architecture of the provided keyword
        /// </summary>
        public Arch getArchitecture(string keyword) {
            return this._arch[keyword.ToUpper()];
        }

        public void invalidateData() {
            this._xmlData = null;
            this._type = null;
            this._description = null;
        }


        #endregion Public Methods
        #region Private Methods

        private void initData() {
            this._type = new Dictionary<string, AsmTokenType>();
            this._arch = new Dictionary<string, Arch>();
            this._assembler = new Dictionary<string, AssemblerEnum>();
            this._description = new Dictionary<string, string>();

            // fill the dictionary with keywords
            XmlDocument xmlDoc = this.getXmlData();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found misc with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found misc " + name);
                    this._type[name] = AsmTokenType.Misc;
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = this.retrieveDescription(node);
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
                    this._arch[name] = this.retrieveArch(node);
                    this._assembler[name] = this.retrieveAssembler(node);
                    this._description[name] = this.retrieveDescription(node);
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
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = retrieveDescription(node);
                }
            }
        }

        private Arch retrieveArch(XmlNode node) {
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

        private AssemblerEnum retrieveAssembler(XmlNode node) {
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

        private string retrieveDescription(XmlNode node) {
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

        private XmlDocument getXmlData() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
            if (this._xmlData == null) {
                string filename = AsmDudeToolsStatic.getInstallPath() + "Resources" + Path.DirectorySeparatorChar + "AsmDudeData.xml";
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
        }

        #endregion
    }
}