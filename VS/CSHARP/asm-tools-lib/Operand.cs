// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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

namespace AsmTools {

    public class Operand {

        private readonly string _str;
        private readonly Ot _type;
        private readonly Rn _rn;
        private readonly ulong _imm;
        private int _nBits;
        private readonly Tuple<Rn, Rn, int, long> _mem;

        public Operand(string token) {

            //TODO: properly handle optional elements {K}{Z} {AES}{ER}
            string token2 = token.
                Replace("{K0}", "").
                Replace("{K1}", "").
                Replace("{K2}", "").
                Replace("{K3}", "").
                Replace("{K4}", "").
                Replace("{K5}", "").
                Replace("{K6}", "").
                Replace("{K7}", "").
                Replace("{Z}", "").
                Replace("{ER}", "").
                Replace("{SAE}", "").
                Replace("{1TO4}", "").
                Replace("{1TO8}", "").
                Replace("{1TO16}", "");

            this._str = token;

            Tuple<bool, Rn, int> t0 = RegisterTools.ToRn(token2);
            if (t0.Item1) {
                this._type = Ot.reg;
                this._rn = t0.Item2;
                this._nBits = t0.Item3;
            } else {
                Tuple<bool, ulong, int> t1 = AsmSourceTools.ToConstant(token2);
                if (t1.Item1) {
                    this._type = Ot.imm;
                    this._imm = t1.Item2;
                    this._nBits = t1.Item3;
                } else {
                    Tuple<bool, Rn, Rn, int, long, int> t2 = AsmSourceTools.ParseMemOperand(token2);
                    if (t2.Item1) {
                        this._type = Ot.mem;
                        this._mem = new Tuple<Rn, Rn, int, long>(t2.Item2, t2.Item3, t2.Item4, t2.Item5);
                        this._nBits = t2.Item6;
                    } else {
                        this._type = Ot.UNKNOWN;
                        this._nBits = -1;
                    }
                }
            }
        }

        public Ot type { get { return this._type; } }
        public bool isReg { get { return this._type == Ot.reg; } }
        public bool isMem { get { return this._type == Ot.mem; } }
        public bool isImm { get { return this._type == Ot.imm; } }

        public Rn rn { get { return this._rn; } }
        public ulong imm { get { return this._imm; } }
        /// <summary>
        /// Return tuple with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement
        /// </summary>
        public Tuple<Rn, Rn, int, long> mem { get { return this._mem; } }
        public int nBits { get { return this._nBits; } set { this._nBits = value; } }

        public override string ToString() {
            return this._str;
        }
    }
}
