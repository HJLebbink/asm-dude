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

using AsmTools;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AsmSim
{
    public class Tools
    {
        private readonly Context _ctx;
        private readonly Random _rand;
        private readonly AsmParameters _p;
        public StateConfig StateConfig { get; set; }
        public bool ShowUndefConstraints { get; set; }

        public Tools() : this(new Dictionary<string, string>()) {}

        public Tools(Dictionary<string, string> settings)
        {
            this.Settings = settings;
            this._ctx = new Context(settings);
            this._rand = new Random();
            this._p = new AsmParameters();
            this.Quiet = true;
            this.ShowUndefConstraints = false;
            this.StateConfig = new StateConfig();
            this.StateConfig.GetRegOn();
        }

        public Dictionary<string, string> Settings { get; private set; }
        public Context Ctx { get { return this._ctx; } }
        public Random Rand { get { return this._rand; } }
        public AsmParameters Parameters { get { return this._p; } }
        public bool Quiet { get; set; }

        public static string CreateKey(Random rand)
        {
            return "!" + ToolsZ3.GetRandomUlong(rand).ToString("X16");
        }
        public static string Reg_Name(Rn reg, string key)
        {
            return reg.ToString() + key;
        }
        public static string Reg_Name_Fresh(Rn reg, Random rand)
        {
            return reg.ToString() + CreateKey(rand) + "!U";
        }
        public static string Flag_Name(Flags flag, string key)
        {
            return flag.ToString() + key;
        }
        public static string Flag_Name_Fresh(Flags flag, Random rand)
        {
            return flag.ToString() + CreateKey(rand) + "!U";
        }
        public static string Mem_Name(string key)
        {
            return "MEM" + key;
        }

        public static BitVecExpr Reg_Key(Rn reg, String key, Context ctx)
        {
            uint nBits = (uint)RegisterTools.NBits(reg);
            if (RegisterTools.IsGeneralPurposeRegister(reg))
            {
                if (nBits == 64)
                {
                    return ctx.MkBVConst(Reg_Name(reg, key), 64);
                }
                else
                {
                    Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                    return (RegisterTools.Is8BitHigh(reg))
                        ? ctx.MkExtract(15, 8, ctx.MkBVConst(Reg_Name(reg64, key), 64))
                        : ctx.MkExtract(nBits - 1, 0, ctx.MkBVConst(Reg_Name(reg64, key), 64));
                }
            }
            else
            {
                return ctx.MkBVConst(Reg_Name(reg, key), nBits);
            }
        }
        public static BoolExpr Flag_Key(Flags flag, string key, Context ctx)
        {
            return ctx.MkBoolConst(Flag_Name(flag, key));
        }
        public static ArrayExpr Mem_Key(string key, Context ctx)
        {
            return ctx.MkArrayConst(Mem_Name(key), ctx.MkBitVecSort(64), ctx.MkBitVecSort(8));
        }
        public static BitVecExpr Reg_Key_Fresh(Rn reg, Random rand, Context ctx)
        {
            return ctx.MkBVConst(Reg_Name_Fresh(reg, rand), (uint)RegisterTools.NBits(reg));
        }
        public static BoolExpr Flag_Key_Fresh(Flags flag, Random rand, Context ctx)
        {
            return ctx.MkBoolConst(Flag_Name_Fresh(flag, rand));
        }

        public static BitVecExpr Calc_Effective_Address(string op, string key, Tools tools)
        {
            return Calc_Effective_Address(new Operand(op, tools.Parameters), key, tools.Ctx);
        }
        public static BitVecExpr Calc_Effective_Address(Operand op, string key, Context ctx)
        {
            uint nBitsOperand = (uint)op.NBits;
            uint nBitsAddress = 64;

            if (op.IsReg)
            {
                return Tools.Reg_Key(op.Rn, key, ctx);
            }
            else if (op.IsMem)
            {
                //Console.WriteLine("INFO: MemZ3:Calc_Effective_Address: operand=" + op);

                var t = op.Mem;
                //Console.WriteLine(string.Format("INFO: Calc_Effective_Address: base={0}; index={1}; scale={2}; disp={3}", t.Item1, t.Item2, t.Item3, t.Item4));

                BitVecExpr address = null;
                //Offset = Base + (Index * Scale) + Displacement

                //1] set the address to the value of the displacement
                if (t.Displacement != 0)
                {
                    BitVecNum displacement = ctx.MkBV(t.Displacement, nBitsAddress);
                    address = displacement;
                    //Console.WriteLine(string.Format("INFO: MemZ3:Calc_Effective_Address: A: address={0}", address));
                }

                //2] add value of the base register
                if (t.BaseReg != Rn.NOREG)
                {
                    BitVecExpr baseRegister;
                    switch (RegisterTools.NBits(t.BaseReg))
                    {
                        case 64: baseRegister = Tools.Reg_Key(t.BaseReg, key, ctx); break;
                        case 32: baseRegister = ctx.MkZeroExt(32, Tools.Reg_Key(t.BaseReg, key, ctx)); break;
                        case 16: baseRegister = ctx.MkZeroExt(48, Tools.Reg_Key(t.BaseReg, key, ctx)); break;
                        default: throw new Exception();
                    }
                    //Console.WriteLine("baseRegister.NBits = " + baseRegister.SortSize + "; address.NBits = " + address.SortSize);
                    address = (address == null) ? baseRegister : ctx.MkBVAdd(address, baseRegister);
                    //Console.WriteLine(string.Format("INFO: MemZ3:Calc_Effective_Address: B: address={0}", address));
                }

                //3] add the value of (Index * Scale)
                if (t.IndexReg != Rn.NOREG)
                {
                    if (t.Scale > 0)
                    {
                        BitVecExpr indexRegister = Tools.Reg_Key(t.IndexReg, key, ctx);
                        switch (t.Scale)
                        {
                            case 0:
                                indexRegister = null;
                                break;
                            case 1:
                                break;
                            case 2:
                                indexRegister = ctx.MkBVSHL(indexRegister, ctx.MkBV(1, nBitsAddress));
                                break;
                            case 4:
                                indexRegister = ctx.MkBVSHL(indexRegister, ctx.MkBV(2, nBitsAddress));
                                break;
                            case 8:
                                indexRegister = ctx.MkBVSHL(indexRegister, ctx.MkBV(3, nBitsAddress));
                                break;
                            default:
                                throw new Exception();
                        }
                        if (address == null)
                        {
                            address = indexRegister;
                        }
                        else if (indexRegister != null)
                        {
                            address = ctx.MkBVAdd(address, indexRegister);
                            //Console.WriteLine(string.Format("INFO: MemZ3:Calc_Effective_Address: C: address={0}", address));
                        }
                    }
                }
                if (address == null)
                { // then the operand was "qword ptr [0]"
                    return ctx.MkBV(0, nBitsAddress);
                }
                return address;
            }
            else
            {
                throw new Exception();
            }
        }
        public static BitVecExpr Get_Value_From_Mem(BitVecExpr address, int nBytes, string key, Context ctx)
        {
            Debug.Assert(nBytes > 0, "Number of bytes has to larger than zero. nBytes=" + nBytes);

            ArrayExpr mem = Tools.Mem_Key(key, ctx);
            BitVecExpr result = ctx.MkSelect(mem, address) as BitVecExpr;

            for (uint i = 1; i < nBytes; ++i)
            {
                BitVecExpr result2 = ctx.MkSelect(mem, ctx.MkBVAdd(address, ctx.MkBV(i, 64))) as BitVecExpr;
                result = ctx.MkConcat(result2, result);
            }
            Debug.Assert(result.SortSize == (nBytes * 8));
            return result;
        }
        public static ArrayExpr Set_Value_To_Mem(BitVecExpr value, BitVecExpr address, string key, Context ctx)
        {
            if (address.SortSize < 64)
            {
                address = ctx.MkZeroExt(64 - address.SortSize, address);
            }
            Debug.Assert(address.SortSize == 64);

            uint nBytes = value.SortSize >> 3;
            ArrayExpr mem = Mem_Key(key, ctx);

            for (uint i = 0; i < nBytes; ++i)
            {
                BitVecExpr address2 = ctx.MkBVAdd(address, ctx.MkBV(i, 64));
                mem = ctx.MkStore(mem, address2, ctx.MkExtract((8 * (i + 1)) - 1, (8 * i), value));
            }
            return mem.Simplify() as ArrayExpr;
        }
        public static State Collapse(IEnumerable<State> previousStates)
        {
            State result = null;
            bool first = true;
            foreach (State prev in previousStates)
            {
                if (first)
                {
                    result = prev;
                    first = false;
                }
                else
                {
                    Console.WriteLine("INFO: Tools:Collapse: state1:\n" + result);
                    Console.WriteLine("INFO: Tools:Collapse: state2:\n" + prev);
                    result = new State(result, prev, true);
                    Console.WriteLine("INFO: Tools:Collapse: merged state:\n" + result);
                }
            }
            return result;
        }
    }
}
