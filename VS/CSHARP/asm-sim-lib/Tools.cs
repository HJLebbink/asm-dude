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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using AsmTools;
    using Microsoft.Z3;

    public class Tools
    {
        private readonly Random rand_;
        private readonly AsmParameters p_;

        public StateConfig StateConfig { get; set; }

        public bool ShowUndefConstraints { get; set; }

        public Tools()
            : this(new Dictionary<string, string>(), string.Empty) { }

        public Tools(Tools other)
        {
            Contract.Requires(other != null);

            this.ContextSettings = new Dictionary<string, string>(other.ContextSettings);
            this.rand_ = other.Rand; //new Random();
            this.p_ = other.p_;
            this.Quiet = other.Quiet;
            this.ShowUndefConstraints = other.ShowUndefConstraints;
            this.StateConfig = other.StateConfig;
        }

        public Tools(Dictionary<string, string> contextSettings, string solverSetting = "")
        {
            this.ContextSettings = contextSettings;
            this.SolverSetting = solverSetting;
            this.rand_ = new Random();
            this.p_ = new AsmParameters();
            this.Quiet = true;
            this.ShowUndefConstraints = false;
            this.StateConfig = new StateConfig();
            this.StateConfig.GetRegOn();
        }

        public Dictionary<string, string> ContextSettings { get; private set; }

        public string SolverSetting { get; private set; }

        public Random Rand { get { return this.rand_; } }

        public AsmParameters Parameters { get { return this.p_; } }

        public bool Quiet { get; set; }

        public static string CreateKey(Random rand)
        {
            Contract.Requires(rand != null);
            return "!" + ToolsZ3.GetRandomUlong(rand).ToString("X16");
        }

        public static string Reg_Name(Rn reg, string key)
        {
            Contract.Requires(key != null);
            return (RegisterTools.Is_SIMD_Register(reg)) ? ("SIMD" + key) : (reg.ToString() + key);
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

        public static string Mem_Name_Fresh(Random rand)
        {
            return "MEM" + CreateKey(rand) + "!U";
        }

        public static (uint high, uint low) SIMD_Extract_Range(Rn rn)
        {
            switch (rn)
            {
                case Rn.XMM0: return ((128 * ((4 * 0) + 1)) - 1, 128 * 4 * 0);
                case Rn.XMM1: return ((128 * ((4 * 1) + 1)) - 1, 128 * 4 * 1);
                case Rn.XMM2: return ((128 * ((4 * 2) + 1)) - 1, 128 * 4 * 2);
                case Rn.XMM3: return ((128 * ((4 * 3) + 1)) - 1, 128 * 4 * 3);
                case Rn.XMM4: return ((128 * ((4 * 4) + 1)) - 1, 128 * 4 * 4);
                case Rn.XMM5: return ((128 * ((4 * 5) + 1)) - 1, 128 * 4 * 5);
                case Rn.XMM6: return ((128 * ((4 * 6) + 1)) - 1, 128 * 4 * 6);
                case Rn.XMM7: return ((128 * ((4 * 7) + 1)) - 1, 128 * 4 * 7);
                case Rn.XMM8: return ((128 * ((4 * 8) + 1)) - 1, 128 * 4 * 8);
                case Rn.XMM9: return ((128 * ((4 * 9) + 1)) - 1, 128 * 4 * 9);
                case Rn.XMM10: return ((128 * ((4 * 10) + 1)) - 1, 128 * 4 * 10);
                case Rn.XMM11: return ((128 * ((4 * 11) + 1)) - 1, 128 * 4 * 11);
                case Rn.XMM12: return ((128 * ((4 * 12) + 1)) - 1, 128 * 4 * 12);
                case Rn.XMM13: return ((128 * ((4 * 13) + 1)) - 1, 128 * 4 * 13);
                case Rn.XMM14: return ((128 * ((4 * 14) + 1)) - 1, 128 * 4 * 14);
                case Rn.XMM15: return ((128 * ((4 * 15) + 1)) - 1, 128 * 4 * 15);
                case Rn.XMM16: return ((128 * ((4 * 16) + 1)) - 1, 128 * 4 * 16);
                case Rn.XMM17: return ((128 * ((4 * 17) + 1)) - 1, 128 * 4 * 17);
                case Rn.XMM18: return ((128 * ((4 * 18) + 1)) - 1, 128 * 4 * 18);
                case Rn.XMM19: return ((128 * ((4 * 19) + 1)) - 1, 128 * 4 * 19);
                case Rn.XMM20: return ((128 * ((4 * 20) + 1)) - 1, 128 * 4 * 20);
                case Rn.XMM21: return ((128 * ((4 * 21) + 1)) - 1, 128 * 4 * 21);
                case Rn.XMM22: return ((128 * ((4 * 22) + 1)) - 1, 128 * 4 * 22);
                case Rn.XMM23: return ((128 * ((4 * 23) + 1)) - 1, 128 * 4 * 23);
                case Rn.XMM24: return ((128 * ((4 * 24) + 1)) - 1, 128 * 4 * 24);
                case Rn.XMM25: return ((128 * ((4 * 25) + 1)) - 1, 128 * 4 * 25);
                case Rn.XMM26: return ((128 * ((4 * 26) + 1)) - 1, 128 * 4 * 26);
                case Rn.XMM27: return ((128 * ((4 * 27) + 1)) - 1, 128 * 4 * 27);
                case Rn.XMM28: return ((128 * ((4 * 28) + 1)) - 1, 128 * 4 * 28);
                case Rn.XMM29: return ((128 * ((4 * 29) + 1)) - 1, 128 * 4 * 29);
                case Rn.XMM30: return ((128 * ((4 * 30) + 1)) - 1, 128 * 4 * 30);
                case Rn.XMM31: return ((128 * ((4 * 31) + 1)) - 1, 128 * 4 * 31);

                case Rn.YMM0: return ((128 * ((4 * 0) + 2)) - 1, 128 * 4 * 0);
                case Rn.YMM1: return ((128 * ((4 * 1) + 2)) - 1, 128 * 4 * 1);
                case Rn.YMM2: return ((128 * ((4 * 2) + 2)) - 1, 128 * 4 * 2);
                case Rn.YMM3: return ((128 * ((4 * 3) + 2)) - 1, 128 * 4 * 3);
                case Rn.YMM4: return ((128 * ((4 * 4) + 2)) - 1, 128 * 4 * 4);
                case Rn.YMM5: return ((128 * ((4 * 5) + 2)) - 1, 128 * 4 * 5);
                case Rn.YMM6: return ((128 * ((4 * 6) + 2)) - 1, 128 * 4 * 6);
                case Rn.YMM7: return ((128 * ((4 * 7) + 2)) - 1, 128 * 4 * 7);
                case Rn.YMM8: return ((128 * ((4 * 8) + 2)) - 1, 128 * 4 * 8);
                case Rn.YMM9: return ((128 * ((4 * 9) + 2)) - 1, 128 * 4 * 9);
                case Rn.YMM10: return ((128 * ((4 * 10) + 2)) - 1, 128 * 4 * 10);
                case Rn.YMM11: return ((128 * ((4 * 11) + 2)) - 1, 128 * 4 * 11);
                case Rn.YMM12: return ((128 * ((4 * 12) + 2)) - 1, 128 * 4 * 12);
                case Rn.YMM13: return ((128 * ((4 * 13) + 2)) - 1, 128 * 4 * 13);
                case Rn.YMM14: return ((128 * ((4 * 14) + 2)) - 1, 128 * 4 * 14);
                case Rn.YMM15: return ((128 * ((4 * 15) + 2)) - 1, 128 * 4 * 15);
                case Rn.YMM16: return ((128 * ((4 * 16) + 2)) - 1, 128 * 4 * 16);
                case Rn.YMM17: return ((128 * ((4 * 17) + 2)) - 1, 128 * 4 * 17);
                case Rn.YMM18: return ((128 * ((4 * 18) + 2)) - 1, 128 * 4 * 18);
                case Rn.YMM19: return ((128 * ((4 * 19) + 2)) - 1, 128 * 4 * 19);
                case Rn.YMM20: return ((128 * ((4 * 20) + 2)) - 1, 128 * 4 * 20);
                case Rn.YMM21: return ((128 * ((4 * 21) + 2)) - 1, 128 * 4 * 21);
                case Rn.YMM22: return ((128 * ((4 * 22) + 2)) - 1, 128 * 4 * 22);
                case Rn.YMM23: return ((128 * ((4 * 23) + 2)) - 1, 128 * 4 * 23);
                case Rn.YMM24: return ((128 * ((4 * 24) + 2)) - 1, 128 * 4 * 24);
                case Rn.YMM25: return ((128 * ((4 * 25) + 2)) - 1, 128 * 4 * 25);
                case Rn.YMM26: return ((128 * ((4 * 26) + 2)) - 1, 128 * 4 * 26);
                case Rn.YMM27: return ((128 * ((4 * 27) + 2)) - 1, 128 * 4 * 27);
                case Rn.YMM28: return ((128 * ((4 * 28) + 2)) - 1, 128 * 4 * 28);
                case Rn.YMM29: return ((128 * ((4 * 29) + 2)) - 1, 128 * 4 * 29);
                case Rn.YMM30: return ((128 * ((4 * 30) + 2)) - 1, 128 * 4 * 30);
                case Rn.YMM31: return ((128 * ((4 * 31) + 2)) - 1, 128 * 4 * 31);

                case Rn.ZMM0: return ((128 * ((4 * 0) + 4)) - 1, 128 * 4 * 0);
                case Rn.ZMM1: return ((128 * ((4 * 1) + 4)) - 1, 128 * 4 * 1);
                case Rn.ZMM2: return ((128 * ((4 * 2) + 4)) - 1, 128 * 4 * 2);
                case Rn.ZMM3: return ((128 * ((4 * 3) + 4)) - 1, 128 * 4 * 3);
                case Rn.ZMM4: return ((128 * ((4 * 4) + 4)) - 1, 128 * 4 * 4);
                case Rn.ZMM5: return ((128 * ((4 * 5) + 4)) - 1, 128 * 4 * 5);
                case Rn.ZMM6: return ((128 * ((4 * 6) + 4)) - 1, 128 * 4 * 6);
                case Rn.ZMM7: return ((128 * ((4 * 7) + 4)) - 1, 128 * 4 * 7);
                case Rn.ZMM8: return ((128 * ((4 * 8) + 4)) - 1, 128 * 4 * 8);
                case Rn.ZMM9: return ((128 * ((4 * 9) + 4)) - 1, 128 * 4 * 9);
                case Rn.ZMM10: return ((128 * ((4 * 10) + 4)) - 1, 128 * 4 * 10);
                case Rn.ZMM11: return ((128 * ((4 * 11) + 4)) - 1, 128 * 4 * 11);
                case Rn.ZMM12: return ((128 * ((4 * 12) + 4)) - 1, 128 * 4 * 12);
                case Rn.ZMM13: return ((128 * ((4 * 13) + 4)) - 1, 128 * 4 * 13);
                case Rn.ZMM14: return ((128 * ((4 * 14) + 4)) - 1, 128 * 4 * 14);
                case Rn.ZMM15: return ((128 * ((4 * 15) + 4)) - 1, 128 * 4 * 15);
                case Rn.ZMM16: return ((128 * ((4 * 16) + 4)) - 1, 128 * 4 * 16);
                case Rn.ZMM17: return ((128 * ((4 * 17) + 4)) - 1, 128 * 4 * 17);
                case Rn.ZMM18: return ((128 * ((4 * 18) + 4)) - 1, 128 * 4 * 18);
                case Rn.ZMM19: return ((128 * ((4 * 19) + 4)) - 1, 128 * 4 * 19);
                case Rn.ZMM20: return ((128 * ((4 * 20) + 4)) - 1, 128 * 4 * 20);
                case Rn.ZMM21: return ((128 * ((4 * 21) + 4)) - 1, 128 * 4 * 21);
                case Rn.ZMM22: return ((128 * ((4 * 22) + 4)) - 1, 128 * 4 * 22);
                case Rn.ZMM23: return ((128 * ((4 * 23) + 4)) - 1, 128 * 4 * 23);
                case Rn.ZMM24: return ((128 * ((4 * 24) + 4)) - 1, 128 * 4 * 24);
                case Rn.ZMM25: return ((128 * ((4 * 25) + 4)) - 1, 128 * 4 * 25);
                case Rn.ZMM26: return ((128 * ((4 * 26) + 4)) - 1, 128 * 4 * 26);
                case Rn.ZMM27: return ((128 * ((4 * 27) + 4)) - 1, 128 * 4 * 27);
                case Rn.ZMM28: return ((128 * ((4 * 28) + 4)) - 1, 128 * 4 * 28);
                case Rn.ZMM29: return ((128 * ((4 * 29) + 4)) - 1, 128 * 4 * 29);
                case Rn.ZMM30: return ((128 * ((4 * 30) + 4)) - 1, 128 * 4 * 30);
                case Rn.ZMM31: return ((128 * ((4 * 31) + 4)) - 1, 128 * 4 * 31);
                default: return (0, 0);
            }
        }

        public static BitVecExpr Create_Key(Rn reg, string key, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);

            uint nBits = (uint)RegisterTools.NBits(reg);
            if (RegisterTools.Is_SIMD_Register(reg))
            {
                (uint high, uint low) = SIMD_Extract_Range(reg);
                return ctx.MkExtract(high, low, ctx.MkBVConst(Reg_Name(reg, key), 32 * 512));
            }
            else if (RegisterTools.IsGeneralPurposeRegister(reg))
            {
                if (nBits == 64)
                {
                    return ctx.MkBVConst(Reg_Name(reg, key), 64);
                }
                else
                {
                    Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                    return RegisterTools.Is8BitHigh(reg)
                        ? ctx.MkExtract(15, 8, ctx.MkBVConst(Reg_Name(reg64, key), 64))
                        : ctx.MkExtract(nBits - 1, 0, ctx.MkBVConst(Reg_Name(reg64, key), 64));
                }
            }
            else
            {
                return ctx.MkBVConst(Reg_Name(reg, key), nBits);
            }
        }

        public static BoolExpr Create_Key(Flags flag, string key, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);
            return ctx.MkBoolConst(Flag_Name(flag, key));
        }

        public static ArrayExpr Create_Mem_Key(string key, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);
            return ctx.MkArrayConst(Mem_Name(key), ctx.MkBitVecSort(64), ctx.MkBitVecSort(8));
        }

        public static ArrayExpr Create_Mem_Key_Fresh(Random rand, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);
            return ctx.MkArrayConst(Mem_Name_Fresh(rand), ctx.MkBitVecSort(64), ctx.MkBitVecSort(8));
        }

        public static BitVecExpr Create_Reg_Key_Fresh(Rn reg, Random rand, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);
            return ctx.MkBVConst(Reg_Name_Fresh(reg, rand), (uint)RegisterTools.NBits(reg));
        }

        public static BoolExpr Create_Flag_Key_Fresh(Flags flag, Random rand, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Assume(ctx != null);
            return ctx.MkBoolConst(Flag_Name_Fresh(flag, rand));
        }

        public static BitVecExpr Calc_Effective_Address(string op, string key, Tools tools, Context ctx)
        {
            Contract.Requires(tools != null);
            Contract.Assume(tools != null);
            return Calc_Effective_Address(new Operand(op, false, tools.Parameters), key, ctx);
        }

        public static BitVecExpr Calc_Effective_Address(Operand op, string key, Context ctx)
        {
            Contract.Requires(op != null);
            Contract.Requires(ctx != null);
            Contract.Assume(op != null);
            Contract.Assume(ctx != null);

            uint nBitsOperand = (uint)op.NBits;
            uint nBitsAddress = 64;

            if (op.IsReg)
            {
                return Create_Key(op.Rn, key, ctx);
            }
            else if (op.IsMem)
            {
                //Console.WriteLine("INFO: MemZ3:Calc_Effective_Address: operand=" + op);

                (Rn baseReg, Rn indexReg, int scale, long displacement1) = op.Mem;
                //Console.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: Calc_Effective_Address: base={0}; index={1}; scale={2}; disp={3}", t.Item1, t.Item2, t.Item3, t.Item4));

                BitVecExpr address = null;
                //Offset = Base + (Index * Scale) + Displacement

                //1] set the address to the value of the displacement
                if (displacement1 != 0)
                {
                    BitVecNum displacement = ctx.MkBV(displacement1, nBitsAddress);
                    address = displacement;
                    //Console.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: MemZ3:Calc_Effective_Address: A: address={0}", address));
                }

                //2] add value of the base register
                if (baseReg != Rn.NOREG)
                {
                    BitVecExpr baseRegister;
                    BitVecExpr keyBitVector = Create_Key(baseReg, key, ctx);
                    switch (RegisterTools.NBits(baseReg))
                    {
                        case 64: baseRegister = keyBitVector; break;
                        case 32: baseRegister = ctx.MkZeroExt(32, keyBitVector); break;
                        case 16: baseRegister = ctx.MkZeroExt(48, keyBitVector); break;
                        default: throw new Exception();
                    }
                    //Console.WriteLine("baseRegister.NBits = " + baseRegister.SortSize + "; address.NBits = " + address.SortSize);
                    address = (address == null) ? baseRegister : ctx.MkBVAdd(address, baseRegister);
                    //Console.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: MemZ3:Calc_Effective_Address: B: address={0}", address));
                }

                //3] add the value of (Index * Scale)
                if (indexReg != Rn.NOREG)
                {
                    if (scale > 0)
                    {
                        BitVecExpr indexRegister = Create_Key(indexReg, key, ctx);
                        switch (scale)
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
                            //Console.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: MemZ3:Calc_Effective_Address: C: address={0}", address));
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

        public static BitVecExpr Create_Value_From_Mem(BitVecExpr address, int nBytes, string key, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Requires(nBytes > 0, "Number of bytes has to larger than zero. nBytes=" + nBytes);

            using ArrayExpr mem = Create_Mem_Key(key, ctx);
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
            Contract.Requires(value != null);
            Contract.Requires(address != null);
            Contract.Requires(ctx != null);

            BitVecExpr address2 = (address.SortSize < 64) ? ctx.MkZeroExt(64 - address.SortSize, address) : address;
            Contract.Assume(address2 != null);
            Contract.Assume(address2.SortSize == 64);

            uint nBytes = value.SortSize >> 3;
            ArrayExpr mem = Create_Mem_Key(key, ctx);

            for (uint i = 0; i < nBytes; ++i)
            {
                BitVecExpr address3 = ctx.MkBVAdd(address2, ctx.MkBV(i, 64));
                mem = ctx.MkStore(mem, address3, ctx.MkExtract((8 * (i + 1)) - 1, 8 * i, value));
            }
            return mem;
        }

        public static State Collapse(IEnumerable<State> previousStates)
        {
            Contract.Requires(previousStates != null);

            State result = null;
            int counter = 0;
            foreach (State prev in previousStates)
            {
                if (counter == 0)
                {
                    result = prev;
                }
                else
                {
                    Console.WriteLine("INFO: Tools:Collapse: state1:\n" + result);
                    Console.WriteLine("INFO: Tools:Collapse: state2:\n" + prev);
                    State result2 = new(result, prev, true);
                    if (counter > 2)
                    {
                        //TODO HJ 26 okt 2019 investigate dispose
                        result.Dispose();
                    }

                    result = result2;
                    Console.WriteLine("INFO: Tools:Collapse: merged state:\n" + result);
                }
                counter++;
            }
            return result;
        }
    }
}
