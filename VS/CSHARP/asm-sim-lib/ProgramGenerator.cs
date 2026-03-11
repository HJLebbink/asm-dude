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
using System.Collections.Generic;

using Microsoft.Z3;
using AsmTools;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using AsmSim.Mnemonics;

namespace AsmSim
{
    public class ProgramGenerator
    {
        private readonly Random _rand;
        private readonly IList<Mnemonic> _eligibleMnemonics;

        public ProgramGenerator()
        {
            this._rand = new Random();
            this._eligibleMnemonics = EligibleMnemonics();
        }

        public string RandomProgram(int nInstructions)
        {
            StringBuilder sb = new();
            for (int i = 0; i < nInstructions; ++i)
            {
                Mnemonic m = this._eligibleMnemonics[this._rand.Next(this._eligibleMnemonics.Count)];
                Rn reg1 = this.RandomReg();
                Rn reg2 = this.RandomReg();
                sb.AppendLine(MakeCodeLine(m, reg1, reg2));
            }
            return sb.ToString().TrimEnd();
        }

        public static string ShuffleProgram(string program) {
            return program;
        }



        public Rn RandomReg()
        {
            return this._rand.Next(4) switch
            {
                0 => Rn.RAX,
                1 => Rn.RBX,
                2 => Rn.RCX,
                3 => Rn.RDX,
                _ => throw new Exception(),
            };
        }

        private static string ToString(Flags flag, State state)
        {
            char c = ToolsZ3.ToStringBin(state.GetTv(flag));
            return c+"";
        }
        private static string ToString(Rn name, State state)
        {
            Tv[] array = state.GetTvArray(name);
            var tup = ToolsZ3.HasOneValue(array);
            if (tup.hasOneValue)
            {
                return ToolsZ3.ToStringBin(tup.value) +"";
            } else {
                return ToolsZ3.ToStringBin(array);
            }
        }

        private static string MakeCodeLine(Mnemonic mnemonic, Rn reg1, Rn reg2)
        {
            return mnemonic switch
            {
                Mnemonic.CMOVE or Mnemonic.CMOVZ or Mnemonic.CMOVNE or Mnemonic.CMOVNZ or Mnemonic.CMOVA or Mnemonic.CMOVNBE or Mnemonic.CMOVAE or Mnemonic.CMOVNB or Mnemonic.CMOVB or Mnemonic.CMOVNAE or Mnemonic.CMOVBE or Mnemonic.CMOVNA or Mnemonic.CMOVG or Mnemonic.CMOVNLE or Mnemonic.CMOVGE or Mnemonic.CMOVNL or Mnemonic.CMOVL or Mnemonic.CMOVNGE or Mnemonic.CMOVLE or Mnemonic.CMOVNG or Mnemonic.CMOVC or Mnemonic.CMOVNC or Mnemonic.CMOVO or Mnemonic.CMOVNO or Mnemonic.CMOVS or Mnemonic.CMOVNS or Mnemonic.CMOVP or Mnemonic.CMOVPE or Mnemonic.CMOVNP or Mnemonic.CMOVPO => mnemonic + " " + reg1 + "," + reg2,
                Mnemonic.MOV or Mnemonic.ADD or Mnemonic.ADC or Mnemonic.SUB or Mnemonic.SBB => mnemonic + " " + reg1 + "," + reg2,
                Mnemonic.INC or Mnemonic.DEC => mnemonic + " " + reg1,
                Mnemonic.AND or Mnemonic.OR or Mnemonic.XOR or Mnemonic.TEST => mnemonic + " " + reg1 + "," + reg2,
                Mnemonic.NEG => mnemonic + " " + reg1,
                Mnemonic.SAR or Mnemonic.SHR or Mnemonic.SAL or Mnemonic.SHL or Mnemonic.ROR or Mnemonic.ROL or Mnemonic.RCR or Mnemonic.RCL => mnemonic + " " + reg1 + ",cl",
                Mnemonic.BT or Mnemonic.BTS or Mnemonic.BTR or Mnemonic.BTC or Mnemonic.BSF or Mnemonic.BSR => mnemonic + " " + reg1 + "," + reg2,
                Mnemonic.SETE or Mnemonic.SETZ or Mnemonic.SETNE or Mnemonic.SETNZ or Mnemonic.SETA or Mnemonic.SETNBE or Mnemonic.SETAE or Mnemonic.SETNB or Mnemonic.SETNC or Mnemonic.SETB or Mnemonic.SETNAE or Mnemonic.SETC or Mnemonic.SETBE or Mnemonic.SETNA or Mnemonic.SETG or Mnemonic.SETNLE or Mnemonic.SETGE or Mnemonic.SETNL or Mnemonic.SETL or Mnemonic.SETNGE or Mnemonic.SETLE or Mnemonic.SETNG or Mnemonic.SETS or Mnemonic.SETNS or Mnemonic.SETO or Mnemonic.SETNO or Mnemonic.SETPE or Mnemonic.SETP or Mnemonic.SETPO or Mnemonic.SETNP => mnemonic + " " + RegisterTools.Get8BitsLowerPart(reg1),
                _ => "nop",
            };
        }

        private static IList<Mnemonic> EligibleMnemonics()
        {
            return [
                Mnemonic.MOV,
                //Mnemonic.CMOVE, // duplicate
                Mnemonic.CMOVZ,
                Mnemonic.CMOVNE,
                //Mnemonic.CMOVNZ, // duplicate
                Mnemonic.CMOVA,
                //Mnemonic.CMOVNBE, // duplicate
                Mnemonic.CMOVAE,
                //Mnemonic.CMOVNB, // duplicate
                //Mnemonic.CMOVB, // duplicate
                //Mnemonic.CMOVNAE, // duplicate
                Mnemonic.CMOVBE,
                //Mnemonic.CMOVNA, // duplicate
                Mnemonic.CMOVG,
                //Mnemonic.CMOVNLE, // duplicate
                Mnemonic.CMOVGE,
                //Mnemonic.CMOVNL, // duplicate
                Mnemonic.CMOVL,
                //Mnemonic.CMOVNGE, // duplicate
                Mnemonic.CMOVLE,
                //Mnemonic.CMOVNG, // duplicate
                Mnemonic.CMOVC,
                //Mnemonic.CMOVNC, // duplicate
                Mnemonic.CMOVO,
                Mnemonic.CMOVNO,
                Mnemonic.CMOVS,
                Mnemonic.CMOVNS,
                Mnemonic.CMOVP,
                //Mnemonic.CMOVPE, // duplicate
                Mnemonic.CMOVNP,
                //Mnemonic.CMOVPO, // duplicate

                Mnemonic.ADD,
                Mnemonic.ADC,
                Mnemonic.INC,
                Mnemonic.SUB,
                Mnemonic.SBB,
                Mnemonic.DEC,

                Mnemonic.AND,
                Mnemonic.OR,
                Mnemonic.XOR,
                Mnemonic.NEG,

                Mnemonic.SAR,
                Mnemonic.SHR,
                Mnemonic.SAL,
                Mnemonic.SHL,

                //Mnemonic.RORX,
                //Mnemonic.SARX,
                //Mnemonic.SHLX,
                //Mnemonic.SHRX,

                Mnemonic.ROR,
                Mnemonic.ROL,
                Mnemonic.RCR,
                Mnemonic.RCL,

                Mnemonic.BT,
                Mnemonic.BTS,
                Mnemonic.BTR,
                Mnemonic.BTC,
                //Mnemonic.BSF, // not implemented yet
                //Mnemonic.BSR, // not implemented yet

                //Mnemonic.SETE, // duplicate
                Mnemonic.SETZ,
                //Mnemonic.SETNE, // duplicate
                Mnemonic.SETNZ,
                Mnemonic.SETA,
                //Mnemonic.SETNBE, // duplicate
                //Mnemonic.SETAE, // duplicate
                //Mnemonic.SETNB, // duplicate
                Mnemonic.SETNC,
                //Mnemonic.SETB, // duplicate
                //Mnemonic.SETNAE, // duplicate
                Mnemonic.SETC,
                Mnemonic.SETBE,
                //Mnemonic.SETNA, // duplicate
                Mnemonic.SETG,
                //Mnemonic.SETNLE, // duplicate
                Mnemonic.SETGE,
                //Mnemonic.SETNL, // duplicate
                Mnemonic.SETL,
                //Mnemonic.SETNGE, // duplicate
                Mnemonic.SETLE,
                //Mnemonic.SETNG, // duplicate
                Mnemonic.SETS,
                Mnemonic.SETNS,
                Mnemonic.SETO,
                Mnemonic.SETNO,
                //Mnemonic.SETPE, // duplicate
                Mnemonic.SETP,
                Mnemonic.SETPO
                //Mnemonic.SETNP // duplicate
                //Mnemonic.TEST // not implemented yet
            ];
        }
    }
}
