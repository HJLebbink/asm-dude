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

namespace AsmTools
{
    public class Operand
    {
        private readonly string _str;
        private readonly Ot _type;
        private readonly Rn _rn;
        private ulong _imm;
        private int _nBits;
        private readonly (Rn baseReg, Rn indexReg, int scale, long displacement) _mem;

        /// <summary>constructor</summary>
        public Operand(string token)
        {
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

            (bool, Rn, int) t0 = RegisterTools.ToRn(token2);
            if (t0.Item1)
            {
                this._type = Ot.reg;
                this._rn = t0.Item2;
                this._nBits = t0.Item3;
            }
            else
            {
                (bool valid, ulong value, int nBits) t1 = AsmSourceTools.ToConstant(token2);
                if (t1.valid)
                {
                    this._type = Ot.imm;
                    this._imm = t1.value;
                    this._nBits = t1.nBits;
                }
                else
                {
                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits) t2 = AsmSourceTools.ParseMemOperand(token2);
                    if (t2.valid)
                    {
                        this._type = Ot.mem;
                        this._mem = (t2.baseReg, t2.indexReg, t2.scale, t2.displacement);
                        this._nBits = t2.nBits;
                    }
                    else
                    {
                        this._type = Ot.UNKNOWN;
                        this._nBits = -1;
                    }
                }
            }
        }

        public Ot Type { get { return this._type; } }
        public bool IsReg { get { return this._type == Ot.reg; } }
        public bool IsMem { get { return this._type == Ot.mem; } }
        public bool IsImm { get { return this._type == Ot.imm; } }

        public Rn Rn { get { return this._rn; } }
        public ulong Imm { get { return this._imm; } }
        
        /// <summary> Return tup with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement </summary>
        public (Rn, Rn, int, long) Mem { get { return this._mem; } }

        public int NBits
        {
            get { return this._nBits; }
            set { this._nBits = value; }
        }

        /// <summary> Sign Extend the imm to the provided number of bits;</summary>
        public void SignExtend(int nBits)
        {
            if (this.IsImm)
            {
                if (nBits > this._nBits)
                {
                    bool signBit = (this._imm >> (this._nBits - 1) & 1) == 1;
                    if (signBit)
                    {
                        for (int bit = this._nBits; bit < nBits; ++bit)
                        {
                            this._imm |= (1ul << bit);
                        }
                    } else
                    {
                        // no need to change _imm
                    }
                    this._nBits = nBits;
                }
            }
            else
            {
                Console.WriteLine("WARNING: Operand:SignExtend: can only sign extend imm.");
            }
        }

        /// <summary> Zero Extend the imm to the provided number of bits;</summary>
        public void ZeroExtend(int nBits)
        {
            if (this.IsImm)
            {
                if (nBits > this._nBits)
                {
                    this._nBits = nBits;
                }
            } else
            {
                Console.WriteLine("WARNING: Operand:ZeroExtend: can only zero extend imm.");
            }
        }
        public override string ToString()
        {
            return this._str;
        }
    }
}