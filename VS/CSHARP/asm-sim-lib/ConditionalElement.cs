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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmSim
{
    using System;
    using System.Diagnostics.Contracts;
    using AsmTools;
    using Microsoft.Z3;

    public enum ConditionalElement
    {
        NONE,
        UNCONDITIONAL,

        /// <summary>if carry (CF = 1)</summary>
        C,

        /// <summary>if below (CF = 1)</summary>
        B,

        /// <summary>if not above or equal (CF = 1)</summary>
        NAE,

        /// <summary>if not carry (CF = 0)</summary>
        NC,

        /// <summary>if above or equal (CF = 0)</summary>
        AE,

        /// <summary>if not below (CF = 0)</summary>
        NB,

        /// <summary>if zero (ZF = 1)</summary>
        Z,

        /// <summary>if equal (ZF = 1)</summary>
        E,

        /// <summary>if not zero (ZF = 0)</summary>
        NZ,

        /// <summary>if not equal (ZF = 0)</summary>
        NE,

        /// <summary>if sign (SF = 1)</summary>
        S,

        /// <summary>if not sign (SF = 0)</summary>
        NS,

        /// <summary>if parity (PF = 1)</summary>
        P,

        /// <summary>if parity even (PF = 1)</summary>
        PE,

        /// <summary>if not parity (PF = 0)</summary>
        NP,

        /// <summary>if parity odd (PF = 0)</summary>
        PO,

        /// <summary>if overflow (OF = 1)</summary>
        O,

        /// <summary>if not overflow (OF = 0)</summary>
        NO,

        /// <summary>if above (CF = 0 and ZF = 0)</summary>
        A,

        /// <summary>if not below or equal (CF = 0 and ZF = 0)</summary>
        NBE,

        /// <summary>if below or equal (CF = 1 or ZF = 1)</summary>
        BE,

        /// <summary>if not above (CF = 1 or ZF = 1)</summary>
        NA,

        /// <summary>if greater (ZF = 0 and SF = OF)</summary>
        G,

        /// <summary>if not less or equal (ZF = 0 and SF = OF)</summary>
        NLE,

        /// <summary>if greater or equal (SF = OF)</summary>
        GE,

        /// <summary>if not less (SF = OF)</summary>
        NL,

        /// <summary>if less (SF ≠ OF)</summary>
        L,

        /// <summary>if not greater or equal (SF ≠ OF)</summary>
        NGE,

        /// <summary>if less or equal (ZF = 1 or SF ≠ OF)/summary>
        LE,

        /// <summary>if not greater (ZF = 1 or SF ≠ OF)</summary>
        NG,

        /// <summary>if register CX zero (CX = 0)</summary>
        CXZ,

        /// <summary>if register ECX zero (ECX = 0)</summary>
        ECXZ,

        /// <summary>if register RCX zero (RCX = 0)</summary>
        RCXZ,
    }

    public static partial class ToolsAsmSim
    {
        /// <summary>Get conditional Element</summary>
        public static ConditionalElement GetCe(Mnemonic mnemonic)
        {
            switch (mnemonic)
            {
                case Mnemonic.JMP:
                    return ConditionalElement.UNCONDITIONAL;
                case Mnemonic.JE:
                case Mnemonic.CMOVE:
                case Mnemonic.SETE:
                    return ConditionalElement.E;
                case Mnemonic.JZ:
                case Mnemonic.CMOVZ:
                case Mnemonic.SETZ:
                    return ConditionalElement.Z;
                case Mnemonic.JNE:
                case Mnemonic.CMOVNE:
                case Mnemonic.SETNE:
                    return ConditionalElement.NE;
                case Mnemonic.JNZ:
                case Mnemonic.CMOVNZ:
                case Mnemonic.SETNZ:
                    return ConditionalElement.NZ;
                case Mnemonic.JA:
                case Mnemonic.CMOVA:
                case Mnemonic.SETA:
                    return ConditionalElement.A;
                case Mnemonic.JNBE:
                case Mnemonic.CMOVNBE:
                case Mnemonic.SETNBE:
                    return ConditionalElement.NBE;
                case Mnemonic.JAE:
                case Mnemonic.CMOVAE:
                case Mnemonic.SETAE:
                    return ConditionalElement.AE;
                case Mnemonic.JNB:
                case Mnemonic.CMOVNB:
                case Mnemonic.SETNB:
                    return ConditionalElement.NB;
                case Mnemonic.JB:
                case Mnemonic.CMOVB:
                case Mnemonic.SETB:
                    return ConditionalElement.B;
                case Mnemonic.JNAE:
                case Mnemonic.CMOVNAE:
                case Mnemonic.SETNAE:
                    return ConditionalElement.NAE;
                case Mnemonic.JBE:
                case Mnemonic.CMOVBE:
                case Mnemonic.SETBE:
                    return ConditionalElement.BE;
                case Mnemonic.JNA:
                case Mnemonic.CMOVNA:
                case Mnemonic.SETNA:
                    return ConditionalElement.NA;
                case Mnemonic.JG:
                case Mnemonic.CMOVG:
                case Mnemonic.SETG:
                    return ConditionalElement.G;
                case Mnemonic.JNLE:
                case Mnemonic.CMOVNLE:
                case Mnemonic.SETNLE:
                    return ConditionalElement.NLE;
                case Mnemonic.JGE:
                case Mnemonic.CMOVGE:
                case Mnemonic.SETGE:
                    return ConditionalElement.GE;
                case Mnemonic.JNL:
                case Mnemonic.CMOVNL:
                case Mnemonic.SETNL:
                    return ConditionalElement.NL;
                case Mnemonic.JL:
                case Mnemonic.CMOVL:
                case Mnemonic.SETL:
                    return ConditionalElement.L;
                case Mnemonic.JNGE:
                case Mnemonic.CMOVNGE:
                case Mnemonic.SETNGE:
                    return ConditionalElement.NGE;
                case Mnemonic.JLE:
                case Mnemonic.CMOVLE:
                case Mnemonic.SETLE:
                    return ConditionalElement.LE;
                case Mnemonic.JNG:
                case Mnemonic.CMOVNG:
                case Mnemonic.SETNG:
                    return ConditionalElement.NG;
                case Mnemonic.JC:
                case Mnemonic.CMOVC:
                case Mnemonic.SETC:
                    return ConditionalElement.C;
                case Mnemonic.JNC:
                case Mnemonic.CMOVNC:
                case Mnemonic.SETNC:
                    return ConditionalElement.NC;
                case Mnemonic.JO:
                case Mnemonic.CMOVO:
                case Mnemonic.SETO:
                    return ConditionalElement.O;
                case Mnemonic.JNO:
                case Mnemonic.CMOVNO:
                case Mnemonic.SETNO:
                    return ConditionalElement.NO;
                case Mnemonic.JS:
                case Mnemonic.CMOVS:
                case Mnemonic.SETS:
                    return ConditionalElement.S;
                case Mnemonic.JNS:
                case Mnemonic.CMOVNS:
                case Mnemonic.SETNS:
                    return ConditionalElement.NS;
                case Mnemonic.JPO:
                case Mnemonic.CMOVP:
                case Mnemonic.SETPO:
                    return ConditionalElement.PO;
                case Mnemonic.JNP:
                case Mnemonic.CMOVPE:
                case Mnemonic.SETNP:
                    return ConditionalElement.NP;
                case Mnemonic.JPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.SETPE:
                    return ConditionalElement.PE;
                case Mnemonic.JP:
                case Mnemonic.CMOVPO:
                case Mnemonic.SETP:
                    return ConditionalElement.P;
                default:
                    return ConditionalElement.NONE;
            }
            //unreachable
            throw new Exception();
        }

        public static Flags FlagsUsed(ConditionalElement ce)
        {
            switch (ce)
            {
                case ConditionalElement.NONE: return Flags.NONE;
                case ConditionalElement.UNCONDITIONAL: return Flags.NONE;
                case ConditionalElement.A: return Flags.CF | Flags.ZF;
                case ConditionalElement.AE: return Flags.CF;
                case ConditionalElement.B: return Flags.CF;
                case ConditionalElement.BE: return Flags.CF | Flags.ZF;
                case ConditionalElement.C: return Flags.CF;
                case ConditionalElement.E: return Flags.ZF;
                case ConditionalElement.G: return Flags.ZF | Flags.SF | Flags.OF;
                case ConditionalElement.GE: return Flags.SF | Flags.OF;
                case ConditionalElement.L: return Flags.SF | Flags.OF;
                case ConditionalElement.LE: return Flags.SF | Flags.OF | Flags.ZF;
                case ConditionalElement.NA: return Flags.CF | Flags.ZF;
                case ConditionalElement.NAE: return Flags.CF;
                case ConditionalElement.NB: return Flags.CF;
                case ConditionalElement.NBE: return Flags.CF | Flags.ZF;
                case ConditionalElement.NC: return Flags.CF;
                case ConditionalElement.NE: return Flags.ZF;
                case ConditionalElement.NG: return Flags.SF | Flags.OF | Flags.ZF;
                case ConditionalElement.NGE: return Flags.SF | Flags.OF;
                case ConditionalElement.NL: return Flags.SF | Flags.OF;
                case ConditionalElement.NLE: return Flags.ZF | Flags.SF | Flags.OF;
                case ConditionalElement.NO: return Flags.OF;
                case ConditionalElement.NP: return Flags.PF;
                case ConditionalElement.NS: return Flags.SF;
                case ConditionalElement.NZ: return Flags.ZF;
                case ConditionalElement.O: return Flags.OF;
                case ConditionalElement.P: return Flags.PF;
                case ConditionalElement.PE: return Flags.PF;
                case ConditionalElement.PO: return Flags.PF;
                case ConditionalElement.S: return Flags.SF;
                case ConditionalElement.Z: return Flags.ZF;

                case ConditionalElement.CXZ: return Flags.NONE;
                case ConditionalElement.ECXZ: return Flags.NONE;
                case ConditionalElement.RCXZ: return Flags.NONE;
                default:
                    // unreachable
                    throw new Exception();
            }
        }

        public static BoolExpr ConditionalTaken(ConditionalElement ce, string key, Context ctx)
        {
            Contract.Requires(ctx != null);

            switch (ce)
            {
                case ConditionalElement.NONE: return ctx.MkFalse();
                case ConditionalElement.UNCONDITIONAL: return ctx.MkTrue();

                case ConditionalElement.C:
                case ConditionalElement.B:
                case ConditionalElement.NAE: return CF();

                case ConditionalElement.NC:
                case ConditionalElement.AE:
                case ConditionalElement.NB: return ctx.MkNot(CF());

                case ConditionalElement.Z:
                case ConditionalElement.E: return ZF();

                case ConditionalElement.NZ:
                case ConditionalElement.NE: return ctx.MkNot(ZF());

                case ConditionalElement.S: return SF();
                case ConditionalElement.NS: return ctx.MkNot(SF());

                case ConditionalElement.P:
                case ConditionalElement.PE: return PF();

                case ConditionalElement.PO:
                case ConditionalElement.NP: return ctx.MkNot(PF());

                case ConditionalElement.O: return OF();
                case ConditionalElement.NO: return ctx.MkNot(OF());

                case ConditionalElement.A:
                case ConditionalElement.NBE: return ctx.MkAnd(ctx.MkNot(CF()), ctx.MkNot(ZF()));

                case ConditionalElement.BE:
                case ConditionalElement.NA: return ctx.MkOr(CF(), ZF());

                case ConditionalElement.G:
                case ConditionalElement.NLE: return ctx.MkAnd(ctx.MkNot(ZF()), ctx.MkEq(SF(), OF()));

                case ConditionalElement.GE:
                case ConditionalElement.NL: return ctx.MkEq(SF(), OF());

                case ConditionalElement.LE:
                case ConditionalElement.NG: return ctx.MkOr(ctx.MkXor(SF(), OF()), ZF());

                case ConditionalElement.L:
                case ConditionalElement.NGE: return ctx.MkXor(SF(), OF());

                case ConditionalElement.CXZ: return ctx.MkEq(Tools.Create_Key(Rn.CX, key, ctx), ctx.MkBV(0, 16));
                case ConditionalElement.ECXZ: return ctx.MkEq(Tools.Create_Key(Rn.ECX, key, ctx), ctx.MkBV(0, 32));
                case ConditionalElement.RCXZ: return ctx.MkEq(Tools.Create_Key(Rn.RCX, key, ctx), ctx.MkBV(0, 64));
                default:
                    // unreachable
                    throw new Exception();
            }

            BoolExpr CF() { return Tools.Create_Key(Flags.CF, key, ctx); }
            BoolExpr ZF() { return Tools.Create_Key(Flags.ZF, key, ctx); }
            BoolExpr SF() { return Tools.Create_Key(Flags.SF, key, ctx); }
            BoolExpr OF() { return Tools.Create_Key(Flags.OF, key, ctx); }
            BoolExpr PF() { return Tools.Create_Key(Flags.PF, key, ctx); }
            //BoolExpr AF() { return Mnemonics_ng.Tools.Flag_Key(Flags.AF, key, ctx); }
        }
    }
}
