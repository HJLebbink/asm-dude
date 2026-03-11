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
            return mnemonic switch
            {
                Mnemonic.JMP => ConditionalElement.UNCONDITIONAL,
                Mnemonic.JE or Mnemonic.CMOVE or Mnemonic.SETE => ConditionalElement.E,
                Mnemonic.JZ or Mnemonic.CMOVZ or Mnemonic.SETZ => ConditionalElement.Z,
                Mnemonic.JNE or Mnemonic.CMOVNE or Mnemonic.SETNE => ConditionalElement.NE,
                Mnemonic.JNZ or Mnemonic.CMOVNZ or Mnemonic.SETNZ => ConditionalElement.NZ,
                Mnemonic.JA or Mnemonic.CMOVA or Mnemonic.SETA => ConditionalElement.A,
                Mnemonic.JNBE or Mnemonic.CMOVNBE or Mnemonic.SETNBE => ConditionalElement.NBE,
                Mnemonic.JAE or Mnemonic.CMOVAE or Mnemonic.SETAE => ConditionalElement.AE,
                Mnemonic.JNB or Mnemonic.CMOVNB or Mnemonic.SETNB => ConditionalElement.NB,
                Mnemonic.JB or Mnemonic.CMOVB or Mnemonic.SETB => ConditionalElement.B,
                Mnemonic.JNAE or Mnemonic.CMOVNAE or Mnemonic.SETNAE => ConditionalElement.NAE,
                Mnemonic.JBE or Mnemonic.CMOVBE or Mnemonic.SETBE => ConditionalElement.BE,
                Mnemonic.JNA or Mnemonic.CMOVNA or Mnemonic.SETNA => ConditionalElement.NA,
                Mnemonic.JG or Mnemonic.CMOVG or Mnemonic.SETG => ConditionalElement.G,
                Mnemonic.JNLE or Mnemonic.CMOVNLE or Mnemonic.SETNLE => ConditionalElement.NLE,
                Mnemonic.JGE or Mnemonic.CMOVGE or Mnemonic.SETGE => ConditionalElement.GE,
                Mnemonic.JNL or Mnemonic.CMOVNL or Mnemonic.SETNL => ConditionalElement.NL,
                Mnemonic.JL or Mnemonic.CMOVL or Mnemonic.SETL => ConditionalElement.L,
                Mnemonic.JNGE or Mnemonic.CMOVNGE or Mnemonic.SETNGE => ConditionalElement.NGE,
                Mnemonic.JLE or Mnemonic.CMOVLE or Mnemonic.SETLE => ConditionalElement.LE,
                Mnemonic.JNG or Mnemonic.CMOVNG or Mnemonic.SETNG => ConditionalElement.NG,
                Mnemonic.JC or Mnemonic.CMOVC or Mnemonic.SETC => ConditionalElement.C,
                Mnemonic.JNC or Mnemonic.CMOVNC or Mnemonic.SETNC => ConditionalElement.NC,
                Mnemonic.JO or Mnemonic.CMOVO or Mnemonic.SETO => ConditionalElement.O,
                Mnemonic.JNO or Mnemonic.CMOVNO or Mnemonic.SETNO => ConditionalElement.NO,
                Mnemonic.JS or Mnemonic.CMOVS or Mnemonic.SETS => ConditionalElement.S,
                Mnemonic.JNS or Mnemonic.CMOVNS or Mnemonic.SETNS => ConditionalElement.NS,
                Mnemonic.JPO or Mnemonic.CMOVP or Mnemonic.SETPO => ConditionalElement.PO,
                Mnemonic.JNP or Mnemonic.CMOVPE or Mnemonic.SETNP => ConditionalElement.NP,
                Mnemonic.JPE or Mnemonic.CMOVNP or Mnemonic.SETPE => ConditionalElement.PE,
                Mnemonic.JP or Mnemonic.CMOVPO or Mnemonic.SETP => ConditionalElement.P,
                _ => ConditionalElement.NONE,
            };
            //unreachable
            throw new Exception();
        }

        public static Flags FlagsUsed(ConditionalElement ce)
        {
            return ce switch
            {
                ConditionalElement.NONE => Flags.NONE,
                ConditionalElement.UNCONDITIONAL => Flags.NONE,
                ConditionalElement.A => Flags.CF | Flags.ZF,
                ConditionalElement.AE => Flags.CF,
                ConditionalElement.B => Flags.CF,
                ConditionalElement.BE => Flags.CF | Flags.ZF,
                ConditionalElement.C => Flags.CF,
                ConditionalElement.E => Flags.ZF,
                ConditionalElement.G => Flags.ZF | Flags.SF | Flags.OF,
                ConditionalElement.GE => Flags.SF | Flags.OF,
                ConditionalElement.L => Flags.SF | Flags.OF,
                ConditionalElement.LE => Flags.SF | Flags.OF | Flags.ZF,
                ConditionalElement.NA => Flags.CF | Flags.ZF,
                ConditionalElement.NAE => Flags.CF,
                ConditionalElement.NB => Flags.CF,
                ConditionalElement.NBE => Flags.CF | Flags.ZF,
                ConditionalElement.NC => Flags.CF,
                ConditionalElement.NE => Flags.ZF,
                ConditionalElement.NG => Flags.SF | Flags.OF | Flags.ZF,
                ConditionalElement.NGE => Flags.SF | Flags.OF,
                ConditionalElement.NL => Flags.SF | Flags.OF,
                ConditionalElement.NLE => Flags.ZF | Flags.SF | Flags.OF,
                ConditionalElement.NO => Flags.OF,
                ConditionalElement.NP => Flags.PF,
                ConditionalElement.NS => Flags.SF,
                ConditionalElement.NZ => Flags.ZF,
                ConditionalElement.O => Flags.OF,
                ConditionalElement.P => Flags.PF,
                ConditionalElement.PE => Flags.PF,
                ConditionalElement.PO => Flags.PF,
                ConditionalElement.S => Flags.SF,
                ConditionalElement.Z => Flags.ZF,
                ConditionalElement.CXZ => Flags.NONE,
                ConditionalElement.ECXZ => Flags.NONE,
                ConditionalElement.RCXZ => Flags.NONE,
                _ => throw new Exception(),// unreachable
            };
        }

        public static BoolExpr ConditionalTaken(ConditionalElement ce, string key, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);

            return ce switch
            {
                ConditionalElement.NONE => ctx.MkFalse(),
                ConditionalElement.UNCONDITIONAL => ctx.MkTrue(),
                ConditionalElement.C or ConditionalElement.B or ConditionalElement.NAE => CF(),
                ConditionalElement.NC or ConditionalElement.AE or ConditionalElement.NB => ctx.MkNot(CF()),
                ConditionalElement.Z or ConditionalElement.E => ZF(),
                ConditionalElement.NZ or ConditionalElement.NE => ctx.MkNot(ZF()),
                ConditionalElement.S => SF(),
                ConditionalElement.NS => ctx.MkNot(SF()),
                ConditionalElement.P or ConditionalElement.PE => PF(),
                ConditionalElement.PO or ConditionalElement.NP => ctx.MkNot(PF()),
                ConditionalElement.O => OF(),
                ConditionalElement.NO => ctx.MkNot(OF()),
                ConditionalElement.A or ConditionalElement.NBE => ctx.MkAnd(ctx.MkNot(CF()), ctx.MkNot(ZF())),
                ConditionalElement.BE or ConditionalElement.NA => ctx.MkOr(CF(), ZF()),
                ConditionalElement.G or ConditionalElement.NLE => ctx.MkAnd(ctx.MkNot(ZF()), ctx.MkEq(SF(), OF())),
                ConditionalElement.GE or ConditionalElement.NL => ctx.MkEq(SF(), OF()),
                ConditionalElement.LE or ConditionalElement.NG => ctx.MkOr(ctx.MkXor(SF(), OF()), ZF()),
                ConditionalElement.L or ConditionalElement.NGE => ctx.MkXor(SF(), OF()),
                ConditionalElement.CXZ => ctx.MkEq(Tools.Create_Key(Rn.CX, key, ctx), ctx.MkBV(0, 16)),
                ConditionalElement.ECXZ => ctx.MkEq(Tools.Create_Key(Rn.ECX, key, ctx), ctx.MkBV(0, 32)),
                ConditionalElement.RCXZ => ctx.MkEq(Tools.Create_Key(Rn.RCX, key, ctx), ctx.MkBV(0, 64)),
                _ => throw new Exception(),// unreachable
            };
            BoolExpr CF() { return Tools.Create_Key(Flags.CF, key, ctx); }
            BoolExpr ZF() { return Tools.Create_Key(Flags.ZF, key, ctx); }
            BoolExpr SF() { return Tools.Create_Key(Flags.SF, key, ctx); }
            BoolExpr OF() { return Tools.Create_Key(Flags.OF, key, ctx); }
            BoolExpr PF() { return Tools.Create_Key(Flags.PF, key, ctx); }
            //BoolExpr AF() { return Mnemonics_ng.Tools.Flag_Key(Flags.AF, key, ctx); }
        }
    }
}
