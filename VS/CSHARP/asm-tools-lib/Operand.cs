// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmTools
{
    using System;
    using System.Diagnostics.Contracts;

    public class Operand
    {
        private readonly string _str;
        private readonly Ot1 _type;
        private readonly Rn _rn = Rn.NOREG;
        private ulong _imm = 0;

        public int NBits { get; set; }

        private readonly (Rn baseReg, Rn indexReg, int scale, long displacement) _mem;
        public readonly string ErrorMessage;

        /// <summary>constructor</summary>
        public Operand(string token, bool isCapitals, AsmParameters p = null)
        {
            Contract.Requires(token != null);

            token = AsmSourceTools.ToCapitals(token, isCapitals);
            this._str = token;

            // TODO: properly handle optional elements {K}{Z} {AES}{ER}
            string token2 = token.Contains("{")
                ? token.
                    Replace("{K0}", string.Empty).
                    Replace("{K1}", string.Empty).
                    Replace("{K2}", string.Empty).
                    Replace("{K3}", string.Empty).
                    Replace("{K4}", string.Empty).
                    Replace("{K5}", string.Empty).
                    Replace("{K6}", string.Empty).
                    Replace("{K7}", string.Empty).
                    Replace("{Z}", string.Empty).
                    Replace("{ER}", string.Empty).
                    Replace("{SAE}", string.Empty).
                    Replace("{1TO4}", string.Empty).
                    Replace("{1TO8}", string.Empty).
                    Replace("{1TO16}", string.Empty)
                : token2 = token;

            (bool valid, Rn reg, int nBits) t0 = RegisterTools.ToRn(token2, true);
            if (t0.valid)
            {
                this._type = Ot1.reg;
                this._rn = t0.reg;
                this.NBits = t0.nBits;
            }
            else
            {
                this._rn = Rn.NOREG;

                (bool valid, ulong value, int nBits) = AsmSourceTools.Evaluate_Constant(token2, true);
                if (valid)
                {
                    this._type = Ot1.imm;
                    this._imm = value;
                    this.NBits = nBits;
                }
                else
                {
                    (bool valid2, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits2, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(token2, true);
                    if (valid2)
                    {
                        this._type = Ot1.mem;
                        this._mem = (baseReg, indexReg, scale, displacement);
                        this.NBits = nBits2;
                    }
                    else
                    {
                        this.ErrorMessage = errorMessage;
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

        /// <summary> Gets tup with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement </summary>
        public (Rn baseReg, Rn indexReg, int scale, long displacement) Mem { get { return this._mem; } }

        /// <summary> Sign Extend the imm to the provided number of bits;</summary>
        public void SignExtend(int nBits)
        {
            if (this.IsImm)
            {
                if (nBits > this.NBits)
                {
                    bool signBit = ((this._imm >> (this.NBits - 1)) & 1) == 1;
                    if (signBit)
                    {
                        for (int bit = this.NBits; bit < nBits; ++bit)
                        {
                            this._imm |= 1ul << bit;
                        }
                    }
                    else
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
            }
            else
            {
                Console.WriteLine("WARNING: Operand:ZeroExtend: can only zero extend imm.");
            }
        }

        public override string ToString()
        {
            Contract.Assert(this._str != null);
            return this._str;
        }
    }
}