using AsmSim;
using AsmTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System;
using System.Numerics;
using Microsoft.Z3;
using AsmSim.Mnemonics;

namespace unit_tests_asm_z3
{
    public class TestTools
    {
#if DEBUG
        public const bool LOG_TO_DISPLAY = true;
#else
        public const bool LOG_TO_DISPLAY = true;
#endif
        public const int DEFAULT_TIMEOUT = 10000;//60000;


        public static ulong RandUlong(int nBits, Random rand)
        {
            ulong i1 = (ulong)rand.Next();
            if (nBits < 32)
            {
                return (i1 & ((1UL << nBits) - 1));
            }
            else
            {
                ulong i2 = (ulong)rand.Next();
                if (nBits < 63)
                {
                    ulong r = (i1 << 31) | i2;
                    return (r & ((1UL << nBits) - 1));
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
                        return (r & ((1UL << nBits) - 1));
                    }
                }
            }
        }
        public static long RandLong(int nBits, Random rand)
        {
            ulong raw = RandUlong(nBits, rand);
            bool sign = ((raw & (1UL << (nBits - 1))) != 0);
            if (sign)
            {
                for (int i = nBits; i < 64; ++i)
                {
                    raw |= (1UL << i);
                }
            }
            return (long)raw;
        }

        public static Tv ToTv5(bool b)
        {
            return (b) ? Tv.ONE : Tv.ZERO;
        }

        #region Calculate Flags
        public static bool Calc_OF_Add(uint nBits, ulong a, ulong b)
        {
            bool signA = Calc_SF(nBits, a);
            bool signB = Calc_SF(nBits, b);
            ulong c = a + b;
            bool signC = Calc_SF(nBits, c);
            bool result = ((signA == signB) && (signA != signC));
            //Console.WriteLine("TestTools: calc_OF_Add: nBits="+nBits+"; a=" + a + "; b=" + b + "; c=" + c + "; signA=" + signA + "; signB=" + signB + "; signC=" + signC +"; result="+result);
            return result;
        }
        public static bool Calc_OF_Sub(uint nBits, ulong a, ulong b)
        {
            bool signA = Calc_SF(nBits, a);
            bool signB = Calc_SF(nBits, b);
            ulong c = a - b;
            bool signC = Calc_SF(nBits, c);
            bool result = ((signA == signB) && (signA != signC));
            //Console.WriteLine("TestTools: calc_OF_Add: nBits="+nBits+"; a=" + a + "; b=" + b + "; c=" + c + "; signA=" + signA + "; signB=" + signB + "; signC=" + signC +"; result="+result);
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
            //Console.WriteLine("PF(" + (a & 0xFF) + ") = " + result);
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
            //Console.WriteLine("INFO: TestTools:calc_AF_Add: a="+a+"; b="+b+"; a2=" + (a & 0xF) + "; b2=" + (b & 0xF) + "; c=" + ((a & 0xF) + (b & 0xF)));
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

        #region AreEqual
        public static void AreEqual(State state1, State state2)
        {
            Assert.IsNotNull(state1);
            Assert.IsNotNull(state2);

            string state1Str = state1.ToStringRegs("") + state1.ToStringFlags("");
            string state2Str = state2.ToStringRegs("") + state2.ToStringFlags("");
            if (state1Str.Equals(state2Str))
            {
                // ok
            } else
            {
                Console.WriteLine("State1=" + state1Str);
                Console.WriteLine("State2=" + state2Str);
                Assert.Fail("state1 and state2 should have been equal");
            }
        }

        #region AreEqual Flags
        public static Tv GetTv5(Flags flag, State state, bool addBranchInfo)
        {
            return state.GetTv5(flag, addBranchInfo);
        }
        public static void AreEqual(Flags flags, bool expected, State state)
        {
            TestTools.AreEqual(flags, (expected) ? Tv.ONE : Tv.ZERO, state);
        }
        public static void AreEqual(Flags flags, Tv expected, State state)
        {
            foreach (Flags flag in FlagTools.GetFlags(flags))
            {
                TestTools.AreEqual(expected, state.GetTv5(flag));
            }
        }
        #endregion

        #region AreEqual Register
        public static void AreEqual(Rn name, int expected, State state)
        {
            TestTools.AreEqual(name, (ulong)expected, state);
        }
        public static void AreEqual(Rn name, ulong expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected, RegisterTools.NBits(name));
            TestTools.AreEqual(name, expectedTvArray, state);
        }
        public static void AreEqual(Rn name, string expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected);
            Assert.AreEqual(RegisterTools.NBits(name), expectedTvArray.Length);
            TestTools.AreEqual(name, expectedTvArray, state);
        }
        public static void AreEqual(Rn name, Tv[] expectedTvArray, State state)
        {
            int nBits = RegisterTools.NBits(name);

            Assert.AreEqual(nBits, expectedTvArray.Length);
            Tv[] actualTvArray = state.GetTv5Array(name, true);
            Assert.AreEqual(nBits, actualTvArray.Length);

            ulong? actualLong = ToolsZ3.GetUlong(actualTvArray);
            ulong? expectedLong = ToolsZ3.GetUlong(expectedTvArray);

            if (actualLong.HasValue && expectedLong.HasValue)
            {
                Assert.AreEqual(expectedLong.Value, actualLong.Value, "Reg " + name + ": Expected value " + expectedLong.Value + " while actual value is " + actualLong.Value);
            }
            else
            {
                TestTools.AreEqual(expectedTvArray, actualTvArray);
            }
        }
        public static void AreEqual(Rn reg1, Rn reg2, State state)
        {
            BoolExpr eq = state.Ctx.MkEq(state.Get(reg1), state.Get(reg2));
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
        #endregion

        #region AreEqual Expr
        public static void AreEqual(BitVecExpr expr, ulong expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected, (int)expr.SortSize);
            Assert.IsNotNull(expectedTvArray);
            TestTools.AreEqual(expr, expectedTvArray, state);
        }
        public static void AreEqual(BitVecExpr expr, string expected, State state)
        {
            Tv[] expectedTvArray = ToolsZ3.GetTvArray(expected);
            Assert.AreEqual(expr.SortSize, (uint)expectedTvArray.Length);
            TestTools.AreEqual(expr, expectedTvArray, state);
        }
        public static void AreEqual(BitVecExpr expr, Tv[] expectedTvArray, State state)
        {
            int nBits = (int)expr.SortSize;
            Assert.AreEqual(nBits, expectedTvArray.Length);
            Tv[] actualTvArray = ToolsZ3.GetTvArray(expr, (int)expr.SortSize, state.Solver, state.Solver_U, state.Ctx);
            Assert.AreEqual(actualTvArray.Length, nBits);
            Assert.AreEqual(expectedTvArray.Length, nBits);

            ulong? actualLong = ToolsZ3.GetUlong(actualTvArray);
            ulong? expectedLong = ToolsZ3.GetUlong(expectedTvArray);

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
            TestTools.AreEqual(ToolsZ3.GetTvArray(expected, actualArray.Length), actualArray);
        }
        public static void AreEqual(Tv[] expectedArray, Tv[] actualArray)
        {
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
