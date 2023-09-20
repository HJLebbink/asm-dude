// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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
        private readonly string str_;
        private readonly Ot1 type_;
        private readonly Rn rn_ = Rn.NOREG;
        private ulong imm_ = 0;
        private readonly (Rn baseReg, Rn indexReg, int scale, long displacement) mem_;
        private readonly string errorMessage_ = string.Empty;

        public string ErrorMessage { get { return this.errorMessage_; } }

        public int NBits { get; set; }

        /// <summary>constructor</summary>
        public Operand(string token, bool isCapitals, AsmParameters p = null)
        {
            Contract.Requires(token != null);
            Contract.Assume(token != null);

            token = AsmSourceTools.ToCapitals(token, isCapitals);
            this.str_ = token;

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
                this.type_ = Ot1.reg;
                this.rn_ = t0.reg;
                this.NBits = t0.nBits;
            }
            else
            {
                this.rn_ = Rn.NOREG;

                (bool valid, ulong value, int nBits) = AsmSourceTools.Evaluate_Constant(token2, true);
                if (valid)
                {
                    this.type_ = Ot1.imm;
                    this.imm_ = value;
                    this.NBits = nBits;
                }
                else
                {
                    (bool valid2, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits2, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(token2, true);
                    if (valid2)
                    {
                        this.type_ = Ot1.mem;
                        this.mem_ = (baseReg, indexReg, scale, displacement);
                        this.NBits = nBits2;
                    }
                    else
                    {
                        this.errorMessage_ = errorMessage;
                        this.type_ = Ot1.UNKNOWN;
                        this.NBits = -1;
                    }
                }
            }
        }

        public Ot1 Type { get { return this.type_; } }

        public bool IsReg { get { return this.type_ == Ot1.reg; } }

        public bool IsMem { get { return this.type_ == Ot1.mem; } }

        public bool IsImm { get { return this.type_ == Ot1.imm; } }

        public Rn Rn { get { return this.rn_; } }

        public ulong Imm { get { return this.imm_; } }

        /// <summary> Gets tup with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement </summary>
        public (Rn baseReg, Rn indexReg, int scale, long displacement) Mem { get { return this.mem_; } }

        /// <summary> Sign Extend the imm to the provided number of bits;</summary>
        public void SignExtend(int nBits)
        {
            if (this.IsImm)
            {
                if (nBits > this.NBits)
                {
                    bool signBit = ((this.imm_ >> (this.NBits - 1)) & 1) == 1;
                    if (signBit)
                    {
                        for (int bit = this.NBits; bit < nBits; ++bit)
                        {
                            this.imm_ |= 1ul << bit;
                        }
                    }
                    else
                    {
                        // no need to change imm_
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
            Contract.Assert(this.str_ != null);
            return this.str_;
        }
    }
}