// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmSim
{
    using System.Collections.Generic;
    using AsmTools;

    public class StateConfig
    {
        public bool CF = false;
        public bool PF = false;
        public bool AF = false;
        public bool ZF = false;
        public bool SF = false;
        public bool OF = false;
        public bool DF = false;

        public bool RAX = false;
        public bool RBX = false;
        public bool RCX = false;
        public bool RDX = false;

        public bool RSI = false;
        public bool RDI = false;
        public bool RBP = false;
        public bool RSP = false;

        public bool R8 = false;
        public bool R9 = false;
        public bool R10 = false;
        public bool R11 = false;

        public bool R12 = false;
        public bool R13 = false;
        public bool R14 = false;
        public bool R15 = false;

        public bool Mem = false;

        public bool SIMD = false;

        #region Setters
        public void Set_All_On()
        {
            this.Set_All_Flags_On();
            this.Set_All_Reg_On();
            this.Mem = true;
        }

        public void Set_All_Flags_On()
        {
            this.CF = true;
            this.PF = true;
            this.AF = true;
            this.ZF = true;
            this.SF = true;
            this.OF = true;
            this.DF = true;
        }

        public void Set_All_Reg_On()
        {
            this.RAX = true;
            this.RBX = true;
            this.RCX = true;
            this.RDX = true;

            this.RSI = true;
            this.RDI = true;
            this.RBP = true;
            this.RSP = true;

            this.R8 = true;
            this.R9 = true;
            this.R10 = true;
            this.R11 = true;

            this.R12 = true;
            this.R13 = true;
            this.R14 = true;
            this.R15 = true;

            this.SIMD = true;
        }

        public void Set_All_Flags_Off()
        {
            this.CF = false;
            this.PF = false;
            this.AF = false;
            this.ZF = false;
            this.SF = false;
            this.OF = false;
            this.DF = false;
        }

        public void Set_All_Reg_Off()
        {
            this.RAX = false;
            this.RBX = false;
            this.RCX = false;
            this.RDX = false;

            this.RSI = false;
            this.RDI = false;
            this.RBP = false;
            this.RSP = false;

            this.R8 = false;
            this.R9 = false;
            this.R10 = false;
            this.R11 = false;

            this.R12 = false;
            this.R13 = false;
            this.R14 = false;
            this.R15 = false;

            this.SIMD = false;
        }

        public void Set_All_Off()
        {
            this.Set_All_Flags_Off();
            this.Set_All_Reg_Off();
            this.Mem = false;
        }

        public void Set_Flags_On(Flags flags)
        {
            if (flags.HasFlag(Flags.CF))
            {
                this.CF = true;
            }

            if (flags.HasFlag(Flags.PF))
            {
                this.PF = true;
            }

            if (flags.HasFlag(Flags.AF))
            {
                this.AF = true;
            }

            if (flags.HasFlag(Flags.ZF))
            {
                this.ZF = true;
            }

            if (flags.HasFlag(Flags.SF))
            {
                this.SF = true;
            }

            if (flags.HasFlag(Flags.OF))
            {
                this.OF = true;
            }

            if (flags.HasFlag(Flags.DF))
            {
                this.DF = true;
            }
        }

        public void Set_Reg_On(Rn reg)
        {
            switch (reg)
            {
                case Rn.RAX: this.RAX = true; break;
                case Rn.RBX: this.RBX = true; break;
                case Rn.RCX: this.RCX = true; break;
                case Rn.RDX: this.RDX = true; break;

                case Rn.RSI: this.RSI = true; break;
                case Rn.RDI: this.RDI = true; break;
                case Rn.RBP: this.RBP = true; break;
                case Rn.RSP: this.RSP = true; break;

                case Rn.R8: this.R8 = true; break;
                case Rn.R9: this.R9 = true; break;
                case Rn.R10: this.R10 = true; break;
                case Rn.R11: this.R11 = true; break;

                case Rn.R12: this.R12 = true; break;
                case Rn.R13: this.R13 = true; break;
                case Rn.R14: this.R14 = true; break;
                case Rn.R15: this.R15 = true; break;
                default: break;
            }
        }

        #endregion
        #region Getters
        public bool IsRegOn(Rn reg)
        {
            return reg switch
            {
                Rn.RAX => this.RAX,
                Rn.RBX => this.RBX,
                Rn.RCX => this.RCX,
                Rn.RDX => this.RDX,
                Rn.RSI => this.RSI,
                Rn.RDI => this.RDI,
                Rn.RBP => this.RBP,
                Rn.RSP => this.RSP,
                Rn.R8 => this.R8,
                Rn.R9 => this.R9,
                Rn.R10 => this.R10,
                Rn.R11 => this.R11,
                Rn.R12 => this.R12,
                Rn.R13 => this.R13,
                Rn.R14 => this.R14,
                Rn.R15 => this.R15,
                _ => false,
            };
        }

        public bool IsFlagOn(Flags flag)
        {
            return flag switch
            {
                Flags.CF => this.CF,
                Flags.PF => this.PF,
                Flags.AF => this.AF,
                Flags.ZF => this.ZF,
                Flags.SF => this.SF,
                Flags.OF => this.OF,
                Flags.DF => this.DF,
                _ => false,
            };
        }

        public IEnumerable<Rn> GetRegOn()
        {
            if (this.RAX)
            {
                yield return Rn.RAX;
            }

            if (this.RBX)
            {
                yield return Rn.RBX;
            }

            if (this.RCX)
            {
                yield return Rn.RCX;
            }

            if (this.RDX)
            {
                yield return Rn.RDX;
            }

            if (this.RSI)
            {
                yield return Rn.RSI;
            }

            if (this.RDI)
            {
                yield return Rn.RDI;
            }

            if (this.RBP)
            {
                yield return Rn.RBP;
            }

            if (this.RSP)
            {
                yield return Rn.RSP;
            }

            if (this.R8)
            {
                yield return Rn.R8;
            }

            if (this.R9)
            {
                yield return Rn.R9;
            }

            if (this.R10)
            {
                yield return Rn.R10;
            }

            if (this.R11)
            {
                yield return Rn.R11;
            }

            if (this.R12)
            {
                yield return Rn.R12;
            }

            if (this.R13)
            {
                yield return Rn.R13;
            }

            if (this.R14)
            {
                yield return Rn.R14;
            }

            if (this.R15)
            {
                yield return Rn.R15;
            }
        }

        public IEnumerable<Flags> GetFlagOn()
        {
            if (this.CF)
            {
                yield return Flags.CF;
            }

            if (this.PF)
            {
                yield return Flags.PF;
            }

            if (this.AF)
            {
                yield return Flags.AF;
            }

            if (this.ZF)
            {
                yield return Flags.ZF;
            }

            if (this.SF)
            {
                yield return Flags.SF;
            }

            if (this.OF)
            {
                yield return Flags.OF;
            }

            if (this.DF)
            {
                yield return Flags.DF;
            }
        }
        #endregion
    }
}
