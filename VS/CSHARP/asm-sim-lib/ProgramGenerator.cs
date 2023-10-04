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
            switch(this._rand.Next(4))
            {
                case 0: return Rn.RAX;
                case 1: return Rn.RBX;
                case 2: return Rn.RCX;
                case 3: return Rn.RDX;
                default: throw new Exception();
            }
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
            switch (mnemonic)
            {
                case Mnemonic.CMOVE:
                case Mnemonic.CMOVZ:
                case Mnemonic.CMOVNE:
                case Mnemonic.CMOVNZ:
                case Mnemonic.CMOVA:
                case Mnemonic.CMOVNBE:
                case Mnemonic.CMOVAE:
                case Mnemonic.CMOVNB:
                case Mnemonic.CMOVB:
                case Mnemonic.CMOVNAE:
                case Mnemonic.CMOVBE:
                case Mnemonic.CMOVNA:
                case Mnemonic.CMOVG:
                case Mnemonic.CMOVNLE:
                case Mnemonic.CMOVGE:
                case Mnemonic.CMOVNL:
                case Mnemonic.CMOVL:
                case Mnemonic.CMOVNGE:
                case Mnemonic.CMOVLE:
                case Mnemonic.CMOVNG:
                case Mnemonic.CMOVC:
                case Mnemonic.CMOVNC:
                case Mnemonic.CMOVO:
                case Mnemonic.CMOVNO:
                case Mnemonic.CMOVS:
                case Mnemonic.CMOVNS:
                case Mnemonic.CMOVP:
                case Mnemonic.CMOVPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.CMOVPO:
                    return mnemonic +" "+ reg1 + "," + reg2;

                case Mnemonic.MOV:
                case Mnemonic.ADD:
                case Mnemonic.ADC:
                case Mnemonic.SUB:
                case Mnemonic.SBB:
                    return mnemonic + " " + reg1 + "," + reg2;

                case Mnemonic.INC:
                case Mnemonic.DEC:
                    return mnemonic + " " + reg1;

                case Mnemonic.AND:
                case Mnemonic.OR:
                case Mnemonic.XOR:
                case Mnemonic.TEST:
                    return mnemonic + " " + reg1 + "," + reg2;

                case Mnemonic.NEG:
                    return mnemonic + " " + reg1;

                case Mnemonic.SAR:
                case Mnemonic.SHR:
                case Mnemonic.SAL:
                case Mnemonic.SHL:

                case Mnemonic.ROR:
                case Mnemonic.ROL:
                case Mnemonic.RCR:
                case Mnemonic.RCL:
                    return mnemonic + " " + reg1 + ",cl";

                case Mnemonic.BT:
                case Mnemonic.BTS:
                case Mnemonic.BTR:
                case Mnemonic.BTC:

                case Mnemonic.BSF:
                case Mnemonic.BSR:
                    return mnemonic + " " + reg1 + "," + reg2;

                case Mnemonic.SETE:
                case Mnemonic.SETZ:
                case Mnemonic.SETNE:
                case Mnemonic.SETNZ:
                case Mnemonic.SETA:
                case Mnemonic.SETNBE:
                case Mnemonic.SETAE:
                case Mnemonic.SETNB:
                case Mnemonic.SETNC:
                case Mnemonic.SETB:
                case Mnemonic.SETNAE:
                case Mnemonic.SETC:
                case Mnemonic.SETBE:
                case Mnemonic.SETNA:
                case Mnemonic.SETG:
                case Mnemonic.SETNLE:
                case Mnemonic.SETGE:
                case Mnemonic.SETNL:
                case Mnemonic.SETL:
                case Mnemonic.SETNGE:
                case Mnemonic.SETLE:
                case Mnemonic.SETNG:
                case Mnemonic.SETS:
                case Mnemonic.SETNS:
                case Mnemonic.SETO:
                case Mnemonic.SETNO:
                case Mnemonic.SETPE:
                case Mnemonic.SETP:
                case Mnemonic.SETPO:
                case Mnemonic.SETNP:
                    return mnemonic + " " + RegisterTools.Get8BitsLowerPart(reg1);

                default:
                    return "nop";
            }
        }

        private static IList<Mnemonic> EligibleMnemonics()
        {
            return new List<Mnemonic>() {
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
            };
        }
    }
}
