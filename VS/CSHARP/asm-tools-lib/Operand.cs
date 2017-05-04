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
        private readonly Ot1 _type;
        private readonly Rn _rn;
        private ulong _imm;
        public int NBits { get; set; }
        private readonly (Rn baseReg, Rn indexReg, int scale, long displacement) _mem;

        /// <summary>constructor</summary>
        public Operand(string token, AsmParameters p = null)
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

            var t0 = RegisterTools.ToRn(token2);
            if (t0.Valid)
            {
                this._type = Ot1.reg;
                this._rn = t0.Reg;
                this.NBits = t0.NBits;
            }
            else
            {
                this._rn = Rn.NOREG;

                var t1 = AsmSourceTools.ToConstant(token2);
                if (t1.Valid)
                {
                    this._type = Ot1.imm;
                    this._imm = t1.Value;
                    this.NBits = t1.NBits;
                }
                else
                {
                    var t2 = AsmSourceTools.ParseMemOperand(token2);
                    if (t2.Valid)
                    {
                        this._type = Ot1.mem;
                        this._mem = (t2.BaseReg, t2.IndexReg, t2.Scale, t2.Displacement);
                        this.NBits = t2.NBits;
                    }
                    else
                    {
                        this._type = Ot1.UNKNOWN;
                        this.NBits = -1;
                    }
                }
            }
        }

        public Ot1 Type { get { return this._type; } }
        public bool IsReg { get { return this._type == Ot1.reg; } }
        public bool IsMem { get { return this._type == Ot1.mem; } }
        public bool IsImm { get { return this._type == Ot1.imm; } }

        public Rn Rn { get { return this._rn; } }
        public ulong Imm { get { return this._imm; } }
        
        /// <summary> Return tup with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement </summary>
        public (Rn baseReg, Rn indexReg, int scale, long displacement) Mem { get { return this._mem; } }

        /// <summary> Sign Extend the imm to the provided number of bits;</summary>
        public void SignExtend(int nBits)
        {
            if (this.IsImm)
            {
                if (nBits > this.NBits)
                {
                    bool signBit = (this._imm >> (this.NBits - 1) & 1) == 1;
                    if (signBit)
                    {
                        for (int bit = this.NBits; bit < nBits; ++bit)
                        {
                            this._imm |= (1ul << bit);
                        }
                    } else
                    {
                        // no need to change _imm
                    }
                    this.NBits = nBits;
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
                if (nBits > this.NBits)
                {
                    this.NBits = nBits;
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