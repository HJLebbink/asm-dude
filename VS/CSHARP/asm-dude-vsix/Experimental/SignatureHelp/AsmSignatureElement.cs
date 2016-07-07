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

using AsmTools;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsmDude.SignatureHelp {

    public class AsmSignatureElement {
        public readonly Mnemonic mnemonic;
        public readonly IList<IList<AsmSignatureEnum>> operands;
        public string remark;

        public AsmSignatureElement(Mnemonic mnem) {
            this.mnemonic = mnem;
            this.operands = new List<IList<AsmSignatureEnum>>();
        }

        public static String getDoc(IList<AsmSignatureEnum> operandType) {
            StringBuilder sb = new StringBuilder();
            foreach (AsmSignatureEnum op in operandType) {
                sb.Append(AsmSignatureTools.getDoc(op) + " or ");
            }
            sb.Length -= 4;
            return sb.ToString();
        }

        public bool isAllowed(Operand op, int operandIndex) {
            if (op == null) { return true; }
            if (operandIndex >= this.operands.Count) {
                return false;
            }
            foreach (AsmSignatureEnum operandType in this.operands[operandIndex]) {
                if (AsmSignatureTools.isAllowed(op, operandType)) {
                    return true;
                }
            }
            return false;
        }

        public override String ToString() {
            StringBuilder sb = new StringBuilder(this.mnemonic.ToString() + " ");
            int nOperands = this.operands.Count;
            for (int i = 0; i < nOperands; ++i) {
                sb.Append(AsmSignatureTools.ToString(this.operands[i], "|"));
                if (i < nOperands - 1) sb.Append(", ");
            }
            return sb.ToString();
        }
    }
}
