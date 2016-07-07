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

    public class AsmSignatureStore {
        private readonly IDictionary<Mnemonic, IList<AsmSignatureElement>> _data;

        public AsmSignatureStore(string filename) {
            this._data = new Dictionary<Mnemonic, IList<AsmSignatureElement>>();
            this.load(filename);
        }

        private void load(string filename) {
            AsmDudeToolsStatic.Output("INFO: SignatureStore:load: filename=" + filename);
            try {
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null) {
                    if ((line.Length > 0) && (!line.StartsWith(";"))) {
                        string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
                        string[] s = cleanedString.Trim().Split(' ');

                        Mnemonic mnemonic = AsmSourceTools.parseMnemonic(s[0]);
                        if (mnemonic == Mnemonic.UNKNOWN) {
                            AsmDudeToolsStatic.Output("WARNING: SignatureStore:load: unknown mnemonic in line" + line);
                        } else {
                            AsmSignatureElement se = new AsmSignatureElement(mnemonic);

                            if (s.Length == 3) {
                                foreach (string operandStr in s[1].Split(',')) {
                                    if (operandStr.Length > 0) {
                                        //AsmDudeToolsStatic.Output("INFO: SignatureStore:load: operandStr " + operandStr);

                                        IList<AsmSignatureEnum> operandList = new List<AsmSignatureEnum>();
                                        foreach (string operand2Str in operandStr.Split('|')) {
                                            AsmSignatureEnum operandType = AsmSignatureTools.parseOperandTypeEnum(operand2Str);
                                            if ((operandType == AsmSignatureEnum.none) || (operandType == AsmSignatureEnum.UNKNOWN)) {
                                                // do nothing
                                            } else {
                                                operandList.Add(operandType);
                                            }
                                        }
                                        if (operandList.Count > 0) {
                                            se.operands.Add(operandList);
                                        }
                                    }
                                }
                                se.remark = s[2];
                            } else {
                                AsmDudeToolsStatic.Output("INFO: SignatureStore:load: mnemonic " + mnemonic +"; s.Length="+s.Length);
                            }

                            IList<AsmSignatureElement> signatureElementList = null;
                            if (this._data.TryGetValue(mnemonic, out signatureElementList)) {
                                signatureElementList.Add(se);
                            } else {
                                this._data.Add(mnemonic, new List<AsmSignatureElement> { se });
                            }
                        }
                    }
                }
                file.Close();
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmTokenTagger: could not find file \"" + filename + "\".");
            } catch (Exception e) {
                MessageBox.Show("ERROR: AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
            }
        }

        public IList<AsmSignatureElement> get(Mnemonic mnemonic) {
            IList<AsmSignatureElement> list;
            if (this._data.TryGetValue(mnemonic, out list)) {
                return list;
            }
            return new List<AsmSignatureElement>(0);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<Mnemonic, IList<AsmSignatureElement>> element in _data) {
                sb.AppendLine("Mnemonic " + element.Key + ":");
                foreach (AsmSignatureElement sig in element.Value) {
                    sb.AppendLine("\t" + sig.ToString());
                }
            }
            return sb.ToString();
        }
    }
}
