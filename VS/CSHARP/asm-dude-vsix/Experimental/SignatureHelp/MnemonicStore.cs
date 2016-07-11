// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

using AsmDude.Tools;
using AsmTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace AsmDude.SignatureHelp {
    //the data is retrieved from http://www.nasm.us/doc/nasmdocb.html

    public class MnemonicStore {
        private readonly IDictionary<Mnemonic, IList<AsmSignatureElement>> _data;
        private readonly IDictionary<Mnemonic, IList<Arch>> _arch;
        private readonly IDictionary<Mnemonic, string> _htmlRef;
        private readonly IDictionary<Mnemonic, string> _description;

        public MnemonicStore(string filename) {
            this._data = new Dictionary<Mnemonic, IList<AsmSignatureElement>>();
            this._arch = new Dictionary<Mnemonic, IList<Arch>>();
            this._htmlRef = new Dictionary<Mnemonic, string>();
            this._description = new Dictionary<Mnemonic, string>();
            this.load(filename);
        }

        public bool hasElement(Mnemonic mnemonic) {
            return this._data.ContainsKey(mnemonic);
        }

        public IList<AsmSignatureElement> getSignatures(Mnemonic mnemonic) {
            IList<AsmSignatureElement> list;
            if (this._data.TryGetValue(mnemonic, out list)) {
                return list;
            }
            return new List<AsmSignatureElement>(0);
        }

        public IList<Arch> getArch(Mnemonic mnemonic) {
            IList<Arch> value;
            if (this._arch.TryGetValue(mnemonic, out value)) {
                return value;
            }
            return new List<Arch>(0);
        }

        public string getHtmlRef(Mnemonic mnemonic) {
            string value;
            if (this._htmlRef.TryGetValue(mnemonic, out value)) {
                return value;
            }
            return "";
        }

        public void setHtmlRef(Mnemonic mnemonic, string value) {
            this._htmlRef[mnemonic] = value;
        }
        public void setDescription(Mnemonic mnemonic, string value) {
            this._description[mnemonic] = value;
            if (this._data.ContainsKey(mnemonic)) {
                foreach (AsmSignatureElement e in _data[mnemonic]) {
                    e.doc = value;
                }
            }
        }

        public string getDescription(Mnemonic mnemonic) {

            //TODO

            // for the time being, the description of the first signatureElement
            if (this.hasElement(mnemonic)) {
                return this._data[mnemonic][0].doc;
            } else {
                return "";
            }
            /*
            string value;
            if (this._description.TryGetValue(mnemonic, out value)) {
                return value;
            }
            return "";
            */
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> element in _data) {
                Mnemonic mnemonic = element.Key;
                string s1 = mnemonic.ToString().ToUpper();
                string s6 = this._htmlRef[mnemonic];

                foreach (AsmSignatureElement sig in element.Value) {
                    string s2 = sig.operandsStr;
                    string s3 = sig.archStr;
                    string s4 = sig.docSignature;
                    string s5 = sig.doc;
                    sb.AppendLine(s1 + "\t" + s2 + "\t" + s3 + "\t" + s4 + "\t" + s5 + "\t" + s6);
                }
            }
            return sb.ToString();
        }


        private void load(string filename) {
            //AsmDudeToolsStatic.Output("INFO: SignatureStore:load: filename=" + filename);
            try {
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null) {
                    if ((line.Length > 0) && (!line.StartsWith(";"))) {
                        //string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
                        //string[] s = cleanedString.Trim().Split('\t');
                        string[] s = line.Trim().Split('\t');
                        if ((s.Length == 5) || (s.Length == 6)) {

                            Mnemonic mnemonic = AsmSourceTools.parseMnemonic(s[0]);
                            if (mnemonic == Mnemonic.UNKNOWN) {
                                AsmDudeToolsStatic.Output("WARNING: SignatureStore:load: unknown mnemonic in line" + line);
                            } else {
                                AsmSignatureElement se = new AsmSignatureElement(mnemonic, s[1], s[2]);
                                se.docSignature = s[3];
                                se.doc = s[4];
                                if (s.Length > 5) this.setHtmlRef(mnemonic, s[5]);
                                IList<AsmSignatureElement> signatureElementList = null;
                                if (this._data.TryGetValue(mnemonic, out signatureElementList)) {
                                    signatureElementList.Add(se);
                                } else {
                                    this._data.Add(mnemonic, new List<AsmSignatureElement> { se });
                                }
                            }
                        } else {
                            AsmDudeToolsStatic.Output("WARNING: SignatureStore:load: s.Length="+s.Length+"; funky line" + line);
                        }
                    }
                }
                file.Close();

                #region Fill Arch
                foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> pair in this._data) {
                    ISet<Arch> archs = new HashSet<Arch>();
                    foreach (AsmSignatureElement signatureElement in pair.Value) {
                        foreach (Arch arch in signatureElement.arch) {
                            archs.Add(arch);
                        }
                    }
                    IList<Arch> list = new List<Arch>();
                    foreach (Arch a in archs) {
                        list.Add(a);
                    }
                    this._arch[pair.Key] = list;
                }
                #endregion

            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmTokenTagger: could not find file \"" + filename + "\".");
            } catch (Exception e) {
                MessageBox.Show("ERROR: AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
            }
        }
    }
}
