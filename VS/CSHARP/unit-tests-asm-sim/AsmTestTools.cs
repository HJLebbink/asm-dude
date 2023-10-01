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

namespace unit_tests_asm_z3
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Numerics;
    using AsmSim;
    using AsmTools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Z3;

    public static class AsmTestTools
    {
#if DEBUG
        public const bool LOG_TO_DISPLAY = true;
#else
        public const bool LOG_TO_DISPLAY = true;
#endif
        public const int DEFAULT_TIMEOUT = 10000; // 60000;

        public static ulong RandUlong(int nBits, Random rand)
        {
            Contract.Requires(rand != null);

            ulong i1 = (ulong)rand.Next();
            if (nBits < 32)
            {
                return i1 & ((1UL << nBits) - 1);
            }
            else
            {
                ulong i2 = (ulong)rand.Next();
                if (nBits < 63)
                {
                    ulong r = (i1 << 31) | i2;
                    return r & ((1UL << nBits) - 1);
                }
                else
                {
                    ulong i3 = (ulong)rand.Next();
                    if (nBits == 64)
                    {
                        ulong r = (i1 << 33) | (i2 << 2) | (i3 & 0x3);
                        return r;
                    }
                    else
                    {
                        ulong r = (i1 << 33) | (i2 << 2) | (i3 & 0x3);
                        return r & ((1UL << nBits) - 1);
                    }
                }
            }
        }

        public static long RandLong(int nBits, Random rand)
        {
            ulong raw = RandUlong(nBits, rand);
            bool sign = (raw & (1UL << (nBits - 1))) != 0;
            if (sign)
            {
                for (int i = nBits; i < 64; ++i)
                {
                    raw |= 1UL << i;
                }
            }
            return (long)raw;
        }

        public static Tv ToTv5(bool b)
        {
            return b ? Tv.ONE : Tv.ZERO;
        }

        #region Calculate Flags
        public static bool Calc_OF_Add(uint nBits, ulong a, ulong b)
        {
            bool signA = Calc_SF(nBits, a);
            bool signB = Calc_SF(nBits, b);
            ulong c = a + b;
            bool signC = Calc_SF(nBits, c);
            bool result = (signA == signB) && (signA != signC);
            // Console.WriteLine("TestTools: calc_OF_Add: nBits="+nBits+"; a=" + a + "; b=" + b + "; c=" + c + "; signA=" + signA + "; signB=" + signB + "; signC=" + signC +"; result="+result);
            return result;
        }

        public static bool Calc_OF_Sub(uint nBits, ulong a, ulong b)
        {
            bool signA = Calc_SF(nBits, a);
            bool signB = Calc_SF(nBits, b);
            ulong c = a - b;
            bool signC = Calc_SF(nBits, c);
            bool result = (signA == signB) && (signA != signC);
            // Console.WriteLine("TestTools: calc_OF_Add: nBits="+nBits+"; a=" + a + "; b=" + b + "; c=" + c + "; signA=" + signA + "; signB=" + signB + "; signC=" + signC +"; result="+result);
            return result;
        }

        public static bool Calc_PF(ulong a)
        {
            int count = 0;
            count += (int)(a & 0x1);
            count += (int)((a >> 1) & 0x1);
            count += (int)((a >> 2) & 0x1);
            count += (int)((a >> 3) & 0x1);
            count += (int)((a >> 4) & 0x1);
            count += (int)((a >> 5) & 0x1);
            count += (int)((a >> 6) & 0x1);
            count += (int)((a >> 7) & 0x1);

            bool result = (count & 1) == 0;
            // Console.WriteLine("PF(" + (a & 0xFF) + ") = " + result);
            return result;
        }

        public static bool Calc_ZF(ulong a)
        {
            return a == 0;
        }

        public static bool Calc_SF(uint nBits, ulong a)
        {
            return (a & (1ul << ((int)nBits - 1))) != 0;
        }

        public static bool Calc_AF_Add(ulong a, ulong b)
        {
            // Console.WriteLine("INFO: TestTools:calc_AF_Add: a="+a+"; b="+b+"; a2=" + (a & 0xF) + "; b2=" + (b & 0xF) + "; c=" + ((a & 0xF) + (b & 0xF)));
            return ((a & 0xF) + (b & 0xF)) > 0xF;
        }

        public static bool Calc_CF_Add(uint nBits, ulong a, ulong b)
        {
            return (new BigInteger(a) + new BigInteger(b)) >= BigInteger.Pow(new BigInteger(2), (int)nBits);
        }

        public static bool Calc_CF_Mul(int nBits, ulong a, ulong b)
        {
            return (new BigInteger(a) * new BigInteger(b)) >= BigInteger.Pow(new BigInteger(2), nBits);
        }
        #endregion

        #region IsTrue IsFalse
        public static void IsTrue(Tv tv)
        {
            switch (tv)
            {
                case Tv.ONE:
                    break;
                case Tv.UNKNOWN:
                case Tv.UNDEFINED:
                case Tv.ZERO:
                case Tv.INCONSISTENT:
                    Assert.Fail("Expected True");
                    break;
                case Tv.UNDETERMINED:
                    Assert.Inconclusive("Expected True");
                    break;
                default:
                    break;
            }
        }

        public static void IsFalse(Tv tv)
        {
            switch (tv)
            {
                case Tv.ZERO:
                    break;
                case Tv.UNKNOWN:
                case Tv.UNDEFINED:
                case Tv.INCONSISTENT:
                case Tv.ONE:
                    Assert.Fail("Expected True");
                    break;
                case Tv.UNDETERMINED:
                    Assert.Inconclusive("Expected True");
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region AreEqual
        public static void AreEqual(State state1, State state2)
        {
            Contract.Requires(state1 != null);
            Contract.Requires(state2 != null);
            Assert.IsNotNull(state1);
            Assert.IsNotNull(state2);

            string state1Str = state1.ToStringRegs(string.Empty) + state1.ToStringFlags(string.Empty);
            string state2Str = state2.ToStringRegs(string.Empty) + state2.ToStringFlags(string.Empty);
            if (state1Str.Equals(state2Str, StringComparison.Ordinal))
            {
                // ok
            }
            else
            {
                Console.WriteLine("State1=" + state1Str);
                Console.WriteLine("State2=" + state2Str);
                Assert.Fail("state1 and state2 should have been equal");
            }
        }

        #region AreEqual Flags
        public static Tv GetTv5(Flags flag, State state)
        {
            Contract.Requires(state != null);
            return state.GetTv(flag);
        }

        public static void AreEqual(Flags flags, bool expected, State state)
        {
            AreEqual(flags, expected ? Tv.ONE : Tv.ZERO, state);
        }

        public static void AreEqual(Flags flags, Tv expected, State state)
        {
            Contract.Requires(state != null);
            foreach (Flags flag in FlagTools.GetFlags(flags))
            {
                AreEqual(expected, state.GetTv(flag));
            }
        }
        #endregion

        #region AreEqual Register
        public static void AreEqual(Rn name, int expected, State state)
        {
            AreEqual(name, (ulong)expected, state);
        }

        public static void AreEqual(Rn name, ulong expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected, RegisterTools.NBits(name));
            AreEqual(name, expectedTvArray, state);
        }

        public static void AreEqual(Rn name, string expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected);
            Assert.AreEqual(RegisterTools.NBits(name), expectedTvArray.Length);
            AreEqual(name, expectedTvArray, state);
        }

        public static void AreEqual(Rn name, Tv[] expectedTvArray, State state)
        {
            Contract.Requires(state != null);
            Contract.Requires(expectedTvArray != null);

            Assert.IsNotNull(state);

            int nBits = RegisterTools.NBits(name);

            Assert.AreEqual(nBits, expectedTvArray.Length);
            Tv[] actualTvArray = state.GetTvArray(name);
            Assert.AreEqual(nBits, actualTvArray.Length);

            ulong? actualLong = ToolsZ3.ToUlong(actualTvArray);
            ulong? expectedLong = ToolsZ3.ToUlong(expectedTvArray);

            if (actualLong.HasValue && expectedLong.HasValue)
            {
                Assert.AreEqual(expectedLong.Value, actualLong.Value, "Reg " + name + ": Expected value " + expectedLong.Value + " while actual value is " + actualLong.Value);
            }
            else
            {
                AreEqual(expectedTvArray, actualTvArray);
            }
        }

        /// <summary>
        /// Test whether the provided registers are equal in the provided state
        /// </summary>
        public static void AreEqual(Rn reg1, Rn reg2, State state)
        {
            Contract.Requires(state != null);

            using (BoolExpr eq = state.Ctx.MkEq(state.Create(reg1), state.Create(reg2)))
            {
                Tv tv = ToolsZ3.GetTv(eq, state.Solver, state.Ctx);
                if (tv == Tv.UNDETERMINED)
                {
                    Assert.Inconclusive("Could not determine whether " + reg1 + " and " + reg2 + " are equal");
                }
                else
                {
                    if (tv != Tv.ONE)
                    {
                        Console.WriteLine("TestTools:AreEqual: state:");
                        Console.WriteLine(state);
                    }
                    Assert.AreEqual(Tv.ONE, tv);
                }
            }
        }

        /// <summary>
        /// Test whether the provided registers are unrelated in the provided state. That is, whether the
        /// equality of the two registers is unknown in the provided state.
        /// </summary>
        public static void AreUnrelated(Rn reg1, Rn reg2, State state)
        {
            Contract.Requires(state != null);

            using (BoolExpr eq = state.Ctx.MkEq(state.Create(reg1), state.Create(reg2)))
            {
                Tv tv = ToolsZ3.GetTv(eq, state.Solver, state.Ctx);
                Console.WriteLine("TestTools:AreUnrelated: tv:" + tv);
                if (tv == Tv.UNDETERMINED)
                {
                    Assert.Inconclusive("Could not determine whether " + reg1 + " and " + reg2 + " are unrelated");
                }
                else
                {
                    if (tv != Tv.UNKNOWN)
                    {
                        Console.WriteLine("TestTools:AreUnrelated: state:");
                        Console.WriteLine(state);
                        Assert.Fail();
                    }
                }
            }
        }
        #endregion

        #region AreEqual Expr
        public static void AreEqual(BitVecExpr expr, ulong expected, State state)
        {
            Contract.Requires(expr != null);

            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected, (int)expr.SortSize);
            Assert.IsNotNull(expectedTvArray);
            AreEqual(expr, expectedTvArray, state);
        }

        public static void AreEqual(BitVecExpr expr, string expected, State state)
        {
            Contract.Requires(expr != null);

            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected);
            Assert.AreEqual(expr.SortSize, (uint)expectedTvArray.Length);
            AreEqual(expr, expectedTvArray, state);
        }

        public static void AreEqual(BitVecExpr expr, Tv[] expectedTvArray, State state)
        {
            Contract.Requires(expr != null);
            Contract.Requires(expectedTvArray != null);
            Contract.Requires(state != null);

            int nBits = (int)expr.SortSize;
            Assert.AreEqual(nBits, expectedTvArray.Length);
            Tv[] actualTvArray = ToolsZ3.GetTvArray(expr, (int)expr.SortSize, state.Solver, state.Solver_U, state.Ctx);
            Assert.AreEqual(actualTvArray.Length, nBits);
            Assert.AreEqual(expectedTvArray.Length, nBits);

            ulong? actualLong = ToolsZ3.ToUlong(actualTvArray);
            ulong? expectedLong = ToolsZ3.ToUlong(expectedTvArray);

            if (actualLong.HasValue && expectedLong.HasValue)
            {
                Assert.AreEqual(actualLong.Value, expectedLong.Value, "Expr " + expr + ": Expected value " + expectedLong.Value + " while actual value is " + actualLong.Value);
            }
            else
            {
                for (int i = 0; i < nBits; ++i)
                {
                    Assert.AreEqual(expectedTvArray[i], actualTvArray[i], "Expr " + expr + ": Pos " + i + ": expected value " + ToolsZ3.ToStringBin(expectedTvArray) + " while actual value is " + ToolsZ3.ToStringBin(actualTvArray));
                }
            }
        }
        #endregion

        #region AreEqual TV
        public static void AreEqual(ulong expected, Tv[] actualArray)
        {
            Contract.Requires(actualArray != null);
            AreEqual(ToolsZ3.GetTvArray(expected, actualArray.Length), actualArray);
        }

        public static void AreEqual(Tv[] expectedArray, Tv[] actualArray)
        {
            Contract.Requires(expectedArray != null);
            Contract.Requires(actualArray != null);

            Assert.AreEqual(expectedArray.Length, actualArray.Length);
            for (int i = 0; i < actualArray.Length; ++i)
            {
                Tv actual = actualArray[i];
                Tv expected = expectedArray[i];

                if ((actual == Tv.UNDETERMINED) && (expected != Tv.UNDETERMINED))
                {
                    Assert.Inconclusive("Pos " + i + ": expected value " + ToolsZ3.ToStringBin(expectedArray) + " while actual value is " + ToolsZ3.ToStringBin(actualArray));
                }
                else
                {
                    Assert.AreEqual(expected, actual, "Pos " + i + ": expected value " + ToolsZ3.ToStringBin(expectedArray) + " while actual value is " + ToolsZ3.ToStringBin(actualArray));
                }
            }
        }

        public static void AreEqual(Tv expected, Tv actual)
        {
            if ((actual == Tv.UNDETERMINED) && (expected != Tv.UNDETERMINED))
            {
                Assert.Inconclusive("Expected value " + ToolsZ3.ToStringBin(expected) + " while actual value is " + ToolsZ3.ToStringBin(actual));
            }
            else
            {
                Assert.AreEqual(expected, actual, "Expected value " + ToolsZ3.ToStringBin(expected) + " while actual value is " + ToolsZ3.ToStringBin(actual));
            }
        }

        #endregion
        #endregion
    }
}
