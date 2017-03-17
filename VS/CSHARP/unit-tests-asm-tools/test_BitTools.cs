using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AsmTools;

namespace unit_tests {

    [TestClass]
    public class Test_BitTools {

        #region Private Stuff

        private static ulong RandUlong(int nBits, Random rand) {
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
        private static long RandLong(int nBits, Random rand) {
            ulong raw = RandUlong(nBits, rand);
            bool sign = ((raw & (1UL << (nBits - 1))) != 0);
            if (sign) {
                for (int i=nBits; i<64; ++i) {
                    raw |= (1UL << i);
                }
            }
            return (long)raw;
        }

        private static bool CalcOverflowValue(int nBits, ulong a, ulong b, ulong c) {
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
            Random rnd = new Random((int)DateTime.Now.Ticks);
            Bt[] a = new Bt[nBits];

            for (int k = 0; k < nTests; ++k) {
                ulong aValue = RandUlong(nBits, rnd);
                BitTools.SetUlongValue(ref a, aValue);
                Assert.AreEqual(aValue, BitTools.GetUlongValue(a), "test " + k + "/" + nTests);
            }
        }
        [TestMethod]
        public void Test_BitTools_get_setLongValue() {
            const int nBits = 32;
            const int nTests = 10000;
            Random rnd = new Random((int)DateTime.Now.Ticks);
            Bt[] a = new Bt[nBits];

            int nNegativeNumbers = 0;
            int nPositiveNumbers = 0;

            for (int k = 0; k < nTests; ++k) {
                long aValue = RandLong(nBits, rnd);
                if (aValue > 0) {
                    nPositiveNumbers++;
                } else {
                    nNegativeNumbers++;
                }
                BitTools.SetLongValue(ref a, aValue);
                Assert.AreEqual(aValue, BitTools.GetLongValue(a), "test " + k + "/" + nTests);
            }
            Assert.IsTrue(nPositiveNumbers > 0, "nPositiveNumbers="+ nPositiveNumbers);
            Assert.IsTrue(nNegativeNumbers > 0, "nNegativeNumbers=" + nNegativeNumbers);
        }
        [TestMethod]
        public void Test_BitTools_toBtArray() {

            Random rnd = new Random((int)DateTime.Now.Ticks);
            const int nTests = 10000;
            Bt[] a = new Bt[64];

            for (int k = 0; k < nTests; ++k) {
                long aValue = RandLong(64, rnd);
                BitTools.SetLongValue(ref a, aValue);

                (ulong, ulong) t1 = BitTools.ToRaw(a);
                Bt[] t2 = BitTools.ToBtArray(t1.Item1, t1.Item2);

                for (int i = 0; i<64; ++i) {
                    Assert.AreEqual(a[i], t2[i], "test " + k + "/" + nTests+": i="+i);
                }
            }
        }
    }
}
