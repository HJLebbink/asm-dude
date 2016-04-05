using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AsmTools;

namespace unit_tests {

    [TestClass]
    public class UnitTest_Tools {
        [TestMethod]
        public void Test_toConstant() {
            {
                ulong i = 0ul;
                string s = i + "";
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0ul;
                string s = "0x"+i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0ul;
                string s = i.ToString("X")+"h";
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X") + "h";
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = "0x"+i.ToString("X"); ;
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X") +"h";
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }

            {
                ulong i = 0xFFul;
                string s = "0x"+i.ToString("X"); ;
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0xFFul;
                string s = i.ToString("X") +"h";
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0x100ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(16, t.Item3, s);
            }
            {
                ulong i = 0xFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(16, t.Item3, s);
            }
            {
                ulong i = 0x10000ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(32, t.Item3, s);
            }
            {
                ulong i = 0xFFFFFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(32, t.Item3, s);
            }
            {
                ulong i = 0x100000000ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(64, t.Item3, s);
            }
            {
                ulong i = 0xFFFFFFFFFFFFFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.Tools.toConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(64, t.Item3, s);
            }
        }
        [TestMethod]
        public void Test_parseMnemonic() {
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic))) {
                Assert.AreEqual(AsmTools.Tools.parseMnemonic(mnemonic.ToString()), mnemonic);
            }
        }

    }
}
