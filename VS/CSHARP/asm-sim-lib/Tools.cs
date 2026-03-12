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
    using System.Collections.Generic;
    using System.Diagnostics;
    using AsmTools;
    using Microsoft.Z3;

    public class Tools
    {
        private readonly Random rand_;
        private readonly AsmParameters p_;

        public StateConfig StateConfig { get; set; }

        public bool ShowUndefConstraints { get; set; }

        public Tools()
            : this([], string.Empty) { }

        public Tools(Tools other)
        {
            ArgumentNullException.ThrowIfNull(other);

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
            ArgumentNullException.ThrowIfNull(rand);
            return "!" + ToolsZ3.GetRandomUlong(rand).ToString("X16");
        }

        public static string Reg_Name(Rn reg, string key)
        {
            ArgumentNullException.ThrowIfNull(key);
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
            return rn switch
            {
                Rn.XMM0 => ((uint high, uint low))((128 * ((4 * 0) + 1)) - 1, 128 * 4 * 0),
                Rn.XMM1 => ((uint high, uint low))((128 * ((4 * 1) + 1)) - 1, 128 * 4 * 1),
                Rn.XMM2 => ((uint high, uint low))((128 * ((4 * 2) + 1)) - 1, 128 * 4 * 2),
                Rn.XMM3 => ((uint high, uint low))((128 * ((4 * 3) + 1)) - 1, 128 * 4 * 3),
                Rn.XMM4 => ((uint high, uint low))((128 * ((4 * 4) + 1)) - 1, 128 * 4 * 4),
                Rn.XMM5 => ((uint high, uint low))((128 * ((4 * 5) + 1)) - 1, 128 * 4 * 5),
                Rn.XMM6 => ((uint high, uint low))((128 * ((4 * 6) + 1)) - 1, 128 * 4 * 6),
                Rn.XMM7 => ((uint high, uint low))((128 * ((4 * 7) + 1)) - 1, 128 * 4 * 7),
                Rn.XMM8 => ((uint high, uint low))((128 * ((4 * 8) + 1)) - 1, 128 * 4 * 8),
                Rn.XMM9 => ((uint high, uint low))((128 * ((4 * 9) + 1)) - 1, 128 * 4 * 9),
                Rn.XMM10 => ((uint high, uint low))((128 * ((4 * 10) + 1)) - 1, 128 * 4 * 10),
                Rn.XMM11 => ((uint high, uint low))((128 * ((4 * 11) + 1)) - 1, 128 * 4 * 11),
                Rn.XMM12 => ((uint high, uint low))((128 * ((4 * 12) + 1)) - 1, 128 * 4 * 12),
                Rn.XMM13 => ((uint high, uint low))((128 * ((4 * 13) + 1)) - 1, 128 * 4 * 13),
                Rn.XMM14 => ((uint high, uint low))((128 * ((4 * 14) + 1)) - 1, 128 * 4 * 14),
                Rn.XMM15 => ((uint high, uint low))((128 * ((4 * 15) + 1)) - 1, 128 * 4 * 15),
                Rn.XMM16 => ((uint high, uint low))((128 * ((4 * 16) + 1)) - 1, 128 * 4 * 16),
                Rn.XMM17 => ((uint high, uint low))((128 * ((4 * 17) + 1)) - 1, 128 * 4 * 17),
                Rn.XMM18 => ((uint high, uint low))((128 * ((4 * 18) + 1)) - 1, 128 * 4 * 18),
                Rn.XMM19 => ((uint high, uint low))((128 * ((4 * 19) + 1)) - 1, 128 * 4 * 19),
                Rn.XMM20 => ((uint high, uint low))((128 * ((4 * 20) + 1)) - 1, 128 * 4 * 20),
                Rn.XMM21 => ((uint high, uint low))((128 * ((4 * 21) + 1)) - 1, 128 * 4 * 21),
                Rn.XMM22 => ((uint high, uint low))((128 * ((4 * 22) + 1)) - 1, 128 * 4 * 22),
                Rn.XMM23 => ((uint high, uint low))((128 * ((4 * 23) + 1)) - 1, 128 * 4 * 23),
                Rn.XMM24 => ((uint high, uint low))((128 * ((4 * 24) + 1)) - 1, 128 * 4 * 24),
                Rn.XMM25 => ((uint high, uint low))((128 * ((4 * 25) + 1)) - 1, 128 * 4 * 25),
                Rn.XMM26 => ((uint high, uint low))((128 * ((4 * 26) + 1)) - 1, 128 * 4 * 26),
                Rn.XMM27 => ((uint high, uint low))((128 * ((4 * 27) + 1)) - 1, 128 * 4 * 27),
                Rn.XMM28 => ((uint high, uint low))((128 * ((4 * 28) + 1)) - 1, 128 * 4 * 28),
                Rn.XMM29 => ((uint high, uint low))((128 * ((4 * 29) + 1)) - 1, 128 * 4 * 29),
                Rn.XMM30 => ((uint high, uint low))((128 * ((4 * 30) + 1)) - 1, 128 * 4 * 30),
                Rn.XMM31 => ((uint high, uint low))((128 * ((4 * 31) + 1)) - 1, 128 * 4 * 31),
                Rn.YMM0 => ((uint high, uint low))((128 * ((4 * 0) + 2)) - 1, 128 * 4 * 0),
                Rn.YMM1 => ((uint high, uint low))((128 * ((4 * 1) + 2)) - 1, 128 * 4 * 1),
                Rn.YMM2 => ((uint high, uint low))((128 * ((4 * 2) + 2)) - 1, 128 * 4 * 2),
                Rn.YMM3 => ((uint high, uint low))((128 * ((4 * 3) + 2)) - 1, 128 * 4 * 3),
                Rn.YMM4 => ((uint high, uint low))((128 * ((4 * 4) + 2)) - 1, 128 * 4 * 4),
                Rn.YMM5 => ((uint high, uint low))((128 * ((4 * 5) + 2)) - 1, 128 * 4 * 5),
                Rn.YMM6 => ((uint high, uint low))((128 * ((4 * 6) + 2)) - 1, 128 * 4 * 6),
                Rn.YMM7 => ((uint high, uint low))((128 * ((4 * 7) + 2)) - 1, 128 * 4 * 7),
                Rn.YMM8 => ((uint high, uint low))((128 * ((4 * 8) + 2)) - 1, 128 * 4 * 8),
                Rn.YMM9 => ((uint high, uint low))((128 * ((4 * 9) + 2)) - 1, 128 * 4 * 9),
                Rn.YMM10 => ((uint high, uint low))((128 * ((4 * 10) + 2)) - 1, 128 * 4 * 10),
                Rn.YMM11 => ((uint high, uint low))((128 * ((4 * 11) + 2)) - 1, 128 * 4 * 11),
                Rn.YMM12 => ((uint high, uint low))((128 * ((4 * 12) + 2)) - 1, 128 * 4 * 12),
                Rn.YMM13 => ((uint high, uint low))((128 * ((4 * 13) + 2)) - 1, 128 * 4 * 13),
                Rn.YMM14 => ((uint high, uint low))((128 * ((4 * 14) + 2)) - 1, 128 * 4 * 14),
                Rn.YMM15 => ((uint high, uint low))((128 * ((4 * 15) + 2)) - 1, 128 * 4 * 15),
                Rn.YMM16 => ((uint high, uint low))((128 * ((4 * 16) + 2)) - 1, 128 * 4 * 16),
                Rn.YMM17 => ((uint high, uint low))((128 * ((4 * 17) + 2)) - 1, 128 * 4 * 17),
                Rn.YMM18 => ((uint high, uint low))((128 * ((4 * 18) + 2)) - 1, 128 * 4 * 18),
                Rn.YMM19 => ((uint high, uint low))((128 * ((4 * 19) + 2)) - 1, 128 * 4 * 19),
                Rn.YMM20 => ((uint high, uint low))((128 * ((4 * 20) + 2)) - 1, 128 * 4 * 20),
                Rn.YMM21 => ((uint high, uint low))((128 * ((4 * 21) + 2)) - 1, 128 * 4 * 21),
                Rn.YMM22 => ((uint high, uint low))((128 * ((4 * 22) + 2)) - 1, 128 * 4 * 22),
                Rn.YMM23 => ((uint high, uint low))((128 * ((4 * 23) + 2)) - 1, 128 * 4 * 23),
                Rn.YMM24 => ((uint high, uint low))((128 * ((4 * 24) + 2)) - 1, 128 * 4 * 24),
                Rn.YMM25 => ((uint high, uint low))((128 * ((4 * 25) + 2)) - 1, 128 * 4 * 25),
                Rn.YMM26 => ((uint high, uint low))((128 * ((4 * 26) + 2)) - 1, 128 * 4 * 26),
                Rn.YMM27 => ((uint high, uint low))((128 * ((4 * 27) + 2)) - 1, 128 * 4 * 27),
                Rn.YMM28 => ((uint high, uint low))((128 * ((4 * 28) + 2)) - 1, 128 * 4 * 28),
                Rn.YMM29 => ((uint high, uint low))((128 * ((4 * 29) + 2)) - 1, 128 * 4 * 29),
                Rn.YMM30 => ((uint high, uint low))((128 * ((4 * 30) + 2)) - 1, 128 * 4 * 30),
                Rn.YMM31 => ((uint high, uint low))((128 * ((4 * 31) + 2)) - 1, 128 * 4 * 31),
                Rn.ZMM0 => ((uint high, uint low))((128 * ((4 * 0) + 4)) - 1, 128 * 4 * 0),
                Rn.ZMM1 => ((uint high, uint low))((128 * ((4 * 1) + 4)) - 1, 128 * 4 * 1),
                Rn.ZMM2 => ((uint high, uint low))((128 * ((4 * 2) + 4)) - 1, 128 * 4 * 2),
                Rn.ZMM3 => ((uint high, uint low))((128 * ((4 * 3) + 4)) - 1, 128 * 4 * 3),
                Rn.ZMM4 => ((uint high, uint low))((128 * ((4 * 4) + 4)) - 1, 128 * 4 * 4),
                Rn.ZMM5 => ((uint high, uint low))((128 * ((4 * 5) + 4)) - 1, 128 * 4 * 5),
                Rn.ZMM6 => ((uint high, uint low))((128 * ((4 * 6) + 4)) - 1, 128 * 4 * 6),
                Rn.ZMM7 => ((uint high, uint low))((128 * ((4 * 7) + 4)) - 1, 128 * 4 * 7),
                Rn.ZMM8 => ((uint high, uint low))((128 * ((4 * 8) + 4)) - 1, 128 * 4 * 8),
                Rn.ZMM9 => ((uint high, uint low))((128 * ((4 * 9) + 4)) - 1, 128 * 4 * 9),
                Rn.ZMM10 => ((uint high, uint low))((128 * ((4 * 10) + 4)) - 1, 128 * 4 * 10),
                Rn.ZMM11 => ((uint high, uint low))((128 * ((4 * 11) + 4)) - 1, 128 * 4 * 11),
                Rn.ZMM12 => ((uint high, uint low))((128 * ((4 * 12) + 4)) - 1, 128 * 4 * 12),
                Rn.ZMM13 => ((uint high, uint low))((128 * ((4 * 13) + 4)) - 1, 128 * 4 * 13),
                Rn.ZMM14 => ((uint high, uint low))((128 * ((4 * 14) + 4)) - 1, 128 * 4 * 14),
                Rn.ZMM15 => ((uint high, uint low))((128 * ((4 * 15) + 4)) - 1, 128 * 4 * 15),
                Rn.ZMM16 => ((uint high, uint low))((128 * ((4 * 16) + 4)) - 1, 128 * 4 * 16),
                Rn.ZMM17 => ((uint high, uint low))((128 * ((4 * 17) + 4)) - 1, 128 * 4 * 17),
                Rn.ZMM18 => ((uint high, uint low))((128 * ((4 * 18) + 4)) - 1, 128 * 4 * 18),
                Rn.ZMM19 => ((uint high, uint low))((128 * ((4 * 19) + 4)) - 1, 128 * 4 * 19),
                Rn.ZMM20 => ((uint high, uint low))((128 * ((4 * 20) + 4)) - 1, 128 * 4 * 20),
                Rn.ZMM21 => ((uint high, uint low))((128 * ((4 * 21) + 4)) - 1, 128 * 4 * 21),
                Rn.ZMM22 => ((uint high, uint low))((128 * ((4 * 22) + 4)) - 1, 128 * 4 * 22),
                Rn.ZMM23 => ((uint high, uint low))((128 * ((4 * 23) + 4)) - 1, 128 * 4 * 23),
                Rn.ZMM24 => ((uint high, uint low))((128 * ((4 * 24) + 4)) - 1, 128 * 4 * 24),
                Rn.ZMM25 => ((uint high, uint low))((128 * ((4 * 25) + 4)) - 1, 128 * 4 * 25),
                Rn.ZMM26 => ((uint high, uint low))((128 * ((4 * 26) + 4)) - 1, 128 * 4 * 26),
                Rn.ZMM27 => ((uint high, uint low))((128 * ((4 * 27) + 4)) - 1, 128 * 4 * 27),
                Rn.ZMM28 => ((uint high, uint low))((128 * ((4 * 28) + 4)) - 1, 128 * 4 * 28),
                Rn.ZMM29 => ((uint high, uint low))((128 * ((4 * 29) + 4)) - 1, 128 * 4 * 29),
                Rn.ZMM30 => ((uint high, uint low))((128 * ((4 * 30) + 4)) - 1, 128 * 4 * 30),
                Rn.ZMM31 => ((uint high, uint low))((128 * ((4 * 31) + 4)) - 1, 128 * 4 * 31),
                _ => ((uint high, uint low))(0, 0),
            };
        }

        public static BitVecExpr Create_Key(Rn reg, string key, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);

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
            ArgumentNullException.ThrowIfNull(ctx);
            return ctx.MkBoolConst(Flag_Name(flag, key));
        }

        public static ArrayExpr Create_Mem_Key(string key, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            return ctx.MkArrayConst(Mem_Name(key), ctx.MkBitVecSort(64), ctx.MkBitVecSort(8));
        }

        public static ArrayExpr Create_Mem_Key_Fresh(Random rand, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            return ctx.MkArrayConst(Mem_Name_Fresh(rand), ctx.MkBitVecSort(64), ctx.MkBitVecSort(8));
        }

        public static BitVecExpr Create_Reg_Key_Fresh(Rn reg, Random rand, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            return ctx.MkBVConst(Reg_Name_Fresh(reg, rand), (uint)RegisterTools.NBits(reg));
        }

        public static BoolExpr Create_Flag_Key_Fresh(Flags flag, Random rand, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            return ctx.MkBoolConst(Flag_Name_Fresh(flag, rand));
        }

        public static BitVecExpr Calc_Effective_Address(string op, string key, Tools tools, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(tools);
            var token = new CapitalToken(op);
            return Calc_Effective_Address(new Operand(token, tools.Parameters), key, ctx);
        }

        public static BitVecExpr Calc_Effective_Address(Operand op, string key, Context ctx)
        {
            ArgumentNullException.ThrowIfNull(op);
            ArgumentNullException.ThrowIfNull(ctx);
            _ = (uint)op.NBits;
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
                    BitVecExpr keyBitVector = Create_Key(baseReg, key, ctx);
                    BitVecExpr baseRegister = RegisterTools.NBits(baseReg) switch
                    {
                        64 => keyBitVector,
                        32 => ctx.MkZeroExt(32, keyBitVector),
                        16 => ctx.MkZeroExt(48, keyBitVector),
                        _ => throw new Exception(),
                    };
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
            ArgumentNullException.ThrowIfNull(ctx);
            Debug.Assert(nBytes > 0, "Number of bytes has to larger than zero. nBytes=" + nBytes);

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
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(address);
            ArgumentNullException.ThrowIfNull(ctx);

            BitVecExpr address2 = (address.SortSize < 64) ? ctx.MkZeroExt(64 - address.SortSize, address) : address;

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
            ArgumentNullException.ThrowIfNull(previousStates);

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
