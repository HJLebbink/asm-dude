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
using System.Text;

namespace AsmDude.SignatureHelp {

    public class AsmSignatureElement {
        private readonly Mnemonic _mnemonic;
        private readonly IList<IList<AsmSignatureEnum>> _operands;
        private readonly IList<Arch> _arch;

        private string _docSignature;
        private string _doc;
//        private string _operandStr;

        public AsmSignatureElement(Mnemonic mnem, string operandStr2, string archStr) {
            this._mnemonic = mnem;
            this._operands = new List<IList<AsmSignatureEnum>>();
            this._arch = new List<Arch>();

            this.operandsStr = operandStr2;
            this.archStr = archStr;
        }

        public static String makeDoc(IList<AsmSignatureEnum> operandType) {
            StringBuilder sb = new StringBuilder();
            foreach (AsmSignatureEnum op in operandType) {
                sb.Append(AsmSignatureTools.getDoc(op) + " or ");
            }
            sb.Length -= 4;
            return sb.ToString();
        }

        /// <summary>Return true if this Signature Element is allowed with the constraints of the provided operand</summary>
        public bool isAllowed(Operand op, int operandIndex) {
            if (op == null) { return true; }
            if (operandIndex >= this.operands.Count) {
                return false;
            }
            foreach (AsmSignatureEnum operandType in this.operands[operandIndex]) {
                if (AsmSignatureTools.isAllowedOperand(op, operandType)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Return true if this Signature Element is allowed in the provided architectures</summary>
        public bool isAllowed(ISet<Arch> selectedArchitectures) {
            foreach (Arch a in this._arch) {
                if (selectedArchitectures.Contains(a)) {
                    //AsmDudeToolsStatic.Output("INFO: AsmSignatureElement: isAllowed: selected architectures=" + ArchTools.ToString(selectedArchitectures) + "; arch = " + ArchTools.ToString(_arch));
                    return true;
                }
            }
            return false;
        }

        public string docSignature {
            get { return (this._docSignature == null) ? this.ToString() : this._docSignature; }
            set { this._docSignature = value; }
        }
        public string doc { get { return this._doc; } set { this._doc = value; } }
        public string archStr {
            get { return ArchTools.ToString(this._arch); }
            set {
                this._arch.Clear();
                foreach (string arch2 in value.Split(',')) {
                    this._arch.Add(ArchTools.parseArch(arch2));
                }
            }
        }
        public IList<Arch> arch { get { return this._arch; } }

        public Mnemonic mnemonic { get { return this._mnemonic; } }

        public string operandsStr {
            get {
                StringBuilder sb = new StringBuilder();
                int nOperands = this._operands.Count;
                for (int i = 0; i < nOperands; ++i) {
                    sb.Append(AsmSignatureTools.ToString(this._operands[i], "|"));
                    if (i < nOperands - 1) sb.Append(", ");
                }
                return sb.ToString();
            }
            set {
                this._operands.Clear();
                foreach (string operandStr in value.Split(',')) {
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
                            this._operands.Add(operandList);
                        }
                    }
                }
            }
        }

        public IList<IList<AsmSignatureEnum>> operands { get { return this._operands; } }

        public override String ToString() {
            return this.mnemonic.ToString() + " " + this.operandsStr;
        }
    }
}
