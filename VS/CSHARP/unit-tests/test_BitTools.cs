using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AsmTools;

namespace unit_tests {

    [TestClass]
    public class test_BitTools {

        #region Private Stuff

        private static ulong randUlong(int nBits, Random rand) {
            ulong i1 = (ulong)rand.Next();
            if (nBits < 32) {
                return (i1 & ((1UL << nBits) - 1));
            } else {
                ulong i2 = (ulong)rand.Next();
                if (nBits < 63) {
                    ulong r = (i1 << 31) | i2;
                    return (r & ((1UL << nBits) - 1));
                } else {
                    ulong i3 = (ulong)rand.Next();
                    ulong r = (i1 << 33) | (i2 << 2) | (i3 & 0x3);
                    return (r & ((1UL << nBits) - 1));
                }
            }
        }
        private static long randLong(int nBits, Random rand) {
            ulong raw = randUlong(nBits, rand);
            bool sign = ((raw & (1UL << (nBits - 1))) != 0);
            if (sign) {
                for (int i=nBits; i<64; ++i) {
                    raw |= (1UL << i);
                }
            }
            return (long)raw;
        }

        private static bool calcOverflowValue(int nBits, ulong a, ulong b, ulong c) {
            bool signA = (a & (1UL << (nBits - 1))) != 0;
            bool signB = (b & (1UL << (nBits - 1))) != 0;
            bool signC = (c & (1UL << (nBits - 1))) != 0;
            return ((signA == signB) && (signA != signC));
        }
        #endregion

        [TestMethod]
        public void Test_BitTools_get_setUlongValue() {
            const int nBits = 16;
            const int nTests = 10000;
            Random rnd = new Random();
            Bt[] a = new Bt[nBits];

            for (int k = 0; k < nTests; ++k) {
                ulong aValue = randUlong(nBits, rnd);
                BitTools.setUlongValue(ref a, aValue);
                Assert.AreEqual(aValue, BitTools.getUlongValue(a), "test " + k + "/" + nTests);
            }
        }
        [TestMethod]
        public void Test_BitTools_get_setLongValue() {
            const int nBits = 32;
            const int nTests = 10000;
            Random rnd = new Random();
            Bt[] a = new Bt[nBits];

            int nNegativeNumbers = 0;
            int nPositiveNumbers = 0;

            for (int k = 0; k < nTests; ++k) {
                long aValue = randLong(nBits, rnd);
                if (aValue > 0) {
                    nPositiveNumbers++;
                } else {
                    nNegativeNumbers++;
                }
                BitTools.setLongValue(ref a, aValue);
                Assert.AreEqual(aValue, BitTools.getLongValue(a), "test " + k + "/" + nTests);
            }
            Assert.IsTrue(nPositiveNumbers > 0, "nPositiveNumbers="+ nPositiveNumbers);
            Assert.IsTrue(nNegativeNumbers > 0, "nNegativeNumbers=" + nNegativeNumbers);
        }

        [TestMethod]
        public void Test_BitTools_and() {
            {
                Bt[] a = new Bt[] { Bt.ONE, Bt.ZERO, Bt.UNDEFINED, Bt.KNOWN };
                Bt[] b = new Bt[] { Bt.ONE, Bt.ONE, Bt.KNOWN, Bt.ONE };
                Bt[] expected = new Bt[] { Bt.ONE, Bt.ZERO, Bt.UNDEFINED, Bt.KNOWN };
                Bt[] actual = AsmTools.BitTools.and(a, b);
                for (int i = 0; i < a.Length; ++i) {
                    Assert.AreEqual(expected[i], actual[i], "i=" + i);
                }
            }
        }
        [TestMethod]
        public void Test_BitTools_or() {
            {
                Bt[] a = new Bt[] { Bt.ONE, Bt.ZERO, Bt.UNDEFINED, Bt.KNOWN };
                Bt[] b = new Bt[] { Bt.ONE, Bt.ONE, Bt.KNOWN, Bt.ONE };
                Bt[] expected = new Bt[] { Bt.ONE, Bt.ONE, Bt.UNDEFINED, Bt.ONE };
                Bt[] actual = AsmTools.BitTools.or(a, b);
                for (int i = 0; i < a.Length; ++i) {
                    Assert.AreEqual(expected[i], actual[i], "i=" + i);
                }
            }
        }
        [TestMethod]
        public void Test_BitTools_add() {

            const int nTests = 10000;
            const int nBits = 16;
            Random rnd = new Random();

            for (int k = 0; k < nTests; ++k) {
                ulong aValue = randUlong(nBits, rnd);
                ulong bValue = randUlong(nBits, rnd);
                ulong cValueWithCarry = aValue + bValue;
                ulong cValue = cValueWithCarry & ((1L << nBits) - 1);

                CarryFlag cf = ((cValueWithCarry & (1UL << nBits)) != 0) ? Bt.ONE : Bt.ZERO;
                OverflowFlag of = (calcOverflowValue(nBits, aValue, bValue, cValue)) ? Bt.ONE : Bt.ZERO;
                AuxiliaryFlag af = (((aValue & 0xF) + (bValue & 0xF)) > 0xF) ? Bt.ONE : Bt.ZERO;

                Bt[] a = new Bt[nBits];
                Bt[] b = new Bt[nBits];
                Bt[] c = new Bt[nBits];

                BitTools.setUlongValue(ref a, (ulong)aValue);
                BitTools.setUlongValue(ref b, (ulong)bValue);
                BitTools.setUlongValue(ref c, (ulong)cValue);

                Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> actual = AsmTools.BitTools.add(a, b, Bt.ZERO);

                ulong cValueComputed = BitTools.getUlongValue(actual.Item1);
                Assert.AreEqual(cValue, cValueComputed, "Error in add(" + aValue + ", " + bValue + ")=" + cValue + "; test " + k + " / " + nTests);
                for (int i = 0; i < a.Length; ++i) {
                    Assert.AreEqual(c[i], actual.Item1[i], "Error in add(" + aValue + ", " + bValue + ")=" + cValue + ": i=" + i + "; test " + k + " / " + nTests);
                }
                Assert.AreEqual(cf.val, actual.Item2.val, "Error in add(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; carry flag is incorrect; test " + k + " / " + nTests);
                Assert.AreEqual(of.val, actual.Item3.val, "Error in add(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; overflow flag is incorrect; test " + k + " / " + nTests);
                Assert.AreEqual(af.val, actual.Item4.val, "Error in add(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; auxiliary flag is incorrect; test " + k + " / " + nTests);

            }
        }
        [TestMethod]
        public void Test_BitTools_sub() {
            {   //0111 – 0001 = 0110
                Bt[] a = new Bt[] { Bt.ZERO, Bt.ONE, Bt.ONE, Bt.ONE };
                Bt[] b = new Bt[] { Bt.ZERO, Bt.ZERO, Bt.ZERO, Bt.ONE };
                Bt[] c = new Bt[] { Bt.ZERO, Bt.ONE, Bt.ONE, Bt.ZERO };

                ulong aValue = BitTools.getUlongValue(a);
                ulong bValue = BitTools.getUlongValue(b);
                ulong cValue = BitTools.getUlongValue(c);

                CarryFlag cf = Bt.ZERO;
                OverflowFlag of = (calcOverflowValue(4, aValue, bValue, cValue)) ? Bt.ONE : Bt.ZERO;
                AuxiliaryFlag af = ((aValue & 0xF) < (bValue & 0xF)) ? Bt.ONE : Bt.ZERO;

                Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> r = AsmTools.BitTools.sub(a, b, Bt.ZERO);

                for (int i = 0; i < a.Length; ++i) {
                    Assert.AreEqual(c[i], r.Item1[i], "i=" + i);
                }

                Assert.AreEqual(cf.val, r.Item2.val, "carry");
                Assert.AreEqual(of.val, r.Item3.val, "overflow");
                Assert.AreEqual(af.val, r.Item4.val, "aux");
            }
            if (true) {
                const int nBits = 16;
                const int nTests = 10000;
                Random rnd = new Random();

                for (int k = 0; k < nTests; ++k) {
                    ulong aValue = randUlong(nBits, rnd);
                    ulong bValue = randUlong(nBits, rnd);
                    ulong cValueWithCarry = aValue - bValue;
                    ulong cValue = cValueWithCarry & ((1L << nBits) - 1);

                    CarryFlag cf = ((cValueWithCarry & (1UL << nBits)) != 0) ? Bt.ONE : Bt.ZERO;
                    OverflowFlag of = (calcOverflowValue(nBits, aValue, bValue, cValue)) ? Bt.ONE : Bt.ZERO;
                    AuxiliaryFlag af = ((aValue & 0xF) < (bValue & 0xF)) ? Bt.ONE : Bt.ZERO;

                    Bt[] a = new Bt[nBits];
                    Bt[] b = new Bt[nBits];
                    Bt[] c = new Bt[nBits];

                    BitTools.setUlongValue(ref a, (ulong)aValue);
                    BitTools.setUlongValue(ref b, (ulong)bValue);
                    BitTools.setUlongValue(ref c, (ulong)cValue);

                    Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> actual = AsmTools.BitTools.sub(a, b, Bt.ZERO);

                    ulong cValueComputed = BitTools.getUlongValue(actual.Item1);
                    Assert.AreEqual(cValue, cValueComputed, "Error in sub(" + aValue + ", " + bValue + ")=" + cValue + "; test " + k + " / " + nTests);
                    for (int i = 0; i < a.Length; ++i) {
                        Assert.AreEqual(c[i], actual.Item1[i], "Error in sub(" + aValue + ", " + bValue + ")=" + cValue + ": i=" + i + "; test " + k + " / " + nTests + "; test " + k + " / " + nTests);
                    }
                    Assert.AreEqual(cf.val, actual.Item2.val, "Error in sub(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; carry flag is incorrect; test " + k + " / " + nTests);
                    Assert.AreEqual(of.val, actual.Item3.val, "Error in sub(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; overflow flag is incorrect; test " + k + " / " + nTests);
                    Assert.AreEqual(af.val, actual.Item4.val, "Error in sub(" + aValue + ", " + bValue + ")=" + cValueWithCarry + "; auxiliary flag is incorrect; test " + k + " / " + nTests);

                }
            }
        }
    }
}
