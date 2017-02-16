using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AsmTools;
using System.Diagnostics;
using System.Collections.Generic;

namespace unit_tests {

    [TestClass]
    public class Test_AsmSourceTools {


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
                for (int i = nBits; i < 64; ++i) {
                    raw |= (1UL << i);
                }
            }
            return (long)raw;
        }
        #endregion


        [TestMethod]
        public void Test_AsmSourceTools_splitIntoKeywordsPos() {
            {
                const string line = "    db \"This string contains the word jmp inside of it\",0";

                IList<Tuple<int, int, bool>> result = AsmSourceTools.SplitIntoKeywordPos(line);
                for (int i = 0; i < result.Count; ++i) {
                    Console.WriteLine(line.Substring(result[i].Item1, result[i].Item2 - result[i].Item1));
                }
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual("db", line.Substring(result[0].Item1, result[0].Item2 - result[0].Item1));
                Assert.AreEqual("\"This string contains the word jmp inside of it\"", line.Substring(result[1].Item1, result[1].Item2 - result[1].Item1));
                Assert.AreEqual("0", line.Substring(result[2].Item1, result[2].Item2 - result[2].Item1));
            }
            {
                const string line = "	call		??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z";

                IList<Tuple<int, int, bool>> result = AsmSourceTools.SplitIntoKeywordPos(line);
                for (int i = 0; i < result.Count; ++i) {
                    Console.WriteLine(line.Substring(result[i].Item1, result[i].Item2 - result[i].Item1));
                }
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("call", line.Substring(result[0].Item1, result[0].Item2 - result[0].Item1));
                Assert.AreEqual("??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z", line.Substring(result[1].Item1, result[1].Item2 - result[1].Item1));
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_getPreviousKeyword() {

            const string line = "    mov rax, rbx;bla";
            {
                int begin = 0;
                int end = 8;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 4;
                int end = 8;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 5;
                int end = 8;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("ov", result, msg);
            }
            {
                int begin = 0;
                int end = 7;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 0;
                int end = 6;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("", result, msg);
            }
            {
                int begin = 0;
                int end = 11;
                string result = AsmTools.AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("rax", result, msg);
            }
        }


        [TestMethod]
        public void Test_AsmSourceTools_toConstant() {
            {
                ulong i = 0ul;
                string s = i + "";
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0ul;
                string s = i.ToString("X") + "h";
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X") + "h";
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = "0x" + i.ToString("X"); ;
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X") + "h";
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }

            {
                ulong i = 0xFFul;
                string s = "0x" + i.ToString("X"); ;
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0xFFul;
                string s = i.ToString("X") + "h";
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(8, t.Item3, s);
            }
            {
                ulong i = 0x100ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(16, t.Item3, s);
            }
            {
                ulong i = 0xFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(16, t.Item3, s);
            }
            {
                ulong i = 0x10000ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(32, t.Item3, s);
            }
            {
                ulong i = 0xFFFFFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(32, t.Item3, s);
            }
            {
                ulong i = 0x100000000ul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(64, t.Item3, s);
            }
            {
                ulong i = 0xFFFFFFFFFFFFFFFFul;
                string s = "0x" + i.ToString("X");
                Tuple<bool, ulong, int> t = AsmTools.AsmSourceTools.ToConstant(s);
                Assert.IsTrue(t.Item1);
                Assert.AreEqual(i, t.Item2, s);
                Assert.AreEqual(64, t.Item3, s);
            }
        }
        [TestMethod]
        public void Test_AsmSourceTools_parseMnemonic() {
            foreach (Mnemonic x in Enum.GetValues(typeof(Mnemonic))) {
                Assert.AreEqual(AsmTools.AsmSourceTools.ParseMnemonic(x.ToString()), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }
        [TestMethod]
        public void Test_AsmSourceTools_parseArch() {
            foreach (Arch x in Enum.GetValues(typeof(Arch))) {
                Assert.AreEqual(AsmTools.ArchTools.ParseArch(ArchTools.ToString(x)), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }
        [TestMethod]
        public void Test_AsmSourceTools_OperandType() {
            foreach (Ot x1 in Enum.GetValues(typeof(Ot))) {
                foreach (Ot x2 in Enum.GetValues(typeof(Ot))) {
                    Tuple<Ot, Ot> t = AsmSourceTools.SplitOt(AsmSourceTools.MergeOt(x1, x2));
                    Assert.AreEqual(t.Item1, x1, "");
                    Assert.AreEqual(t.Item2, x2, "");
                }
            }
        }
        [TestMethod]
        public void Test_AsmSourceTools_parseMemOperand() {

            // see intel manual : 3.7.5 Specifying an Offset

            // 32-bit mode:
            // possible bases: EAX, EBX, ECX, EDX, ESP, EBP, ESI, EDI
            // possible index: EAX, EBX, ECX, EDX,      EBP, ESI, EDI
            // scale: 1, 2, 4, 8
            // displacement none, 8-bit, 16-bit, 32-bit

            // 64-bit mode:
            // possible bases: RAX, RBX, RCX, RDX, RSP, RBP, RSI, RDI
            // possible index: RAX, RBX, RCX, RDX, RSP, RBP, RSI, RDI
            // scale: 1, 2, 4, 8
            // displacement none, 8-bit, 16-bit, 32-bit

            Random rnd = new Random((int)DateTime.Now.Ticks);

            Rn[] bases32 = new Rn[] { Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX, Rn.ESP, Rn.EBP, Rn.ESI, Rn.EDI };
            Rn[] index32 = new Rn[] { Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX,         Rn.EBP, Rn.ESI, Rn.EDI };

            Rn[] bases64 = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };
            Rn[] index74 = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };

            int[] scales = new int[] { 0, 1, 2, 4, 8 };


            for (int i = 0; i < bases32.Length; ++i) {
                Rn b = bases32[i];

                {
                    string str = "[" + b + "]";
                    var t = AsmSourceTools.ParseMemOperand(str);
                    Assert.AreEqual(true, t.Item1, str);
                    Assert.AreEqual(b, t.Item2, "base: " + str);
                    Assert.AreEqual(Rn.NOREG, t.Item3, "index: " + str);
                    Assert.AreEqual(0, t.Item4, "scale: " + str);
                    Assert.AreEqual(0, t.Item5, "displacement: " + str);
                }

                for (int j = 0; j<index32.Length; ++j) {
                    Rn idx = index32[j];

                    {
                        string str = "[" + b + "+" + idx + "]";
                        var t = AsmSourceTools.ParseMemOperand(str);
                        Assert.AreEqual(true, t.Item1, str);
                        Assert.AreEqual(b, t.Item2, "base: " + str);
                        Assert.AreEqual(idx, t.Item3, "index: " + str);
                        Assert.AreEqual(1, t.Item4, "scale: " + str);
                        Assert.AreEqual(0, t.Item5, "displacement: " + str);
                    }
                    {
                        string str = "[" + idx + "+" + b + "]";
                        var t = AsmSourceTools.ParseMemOperand(str);
                        Assert.AreEqual(true, t.Item1, str);
                        //idx and base can be interchanged
                        //Assert.AreEqual(b, t.Item2, "base: " + str); 
                        //Assert.AreEqual(idx, t.Item3, "index: " + str);
                        Assert.AreEqual(1, t.Item4, "scale: " + str);
                        Assert.AreEqual(0, t.Item5, "displacement: " + str);
                    }

                    for (int k = 0; k < scales.Length; ++k) {
                        int s = scales[k];

                        //Offset = Base + (Index * Scale) + Displacement
                        {
                            string str = "[" + b + "+" + idx + " * " + s + "]";
                            var t = AsmSourceTools.ParseMemOperand(str);
                            Assert.AreEqual(true, t.Item1, str);
                            Assert.AreEqual(b, t.Item2, "base: " + str);
                            Assert.AreEqual(idx, t.Item3, "index: " + str);
                            Assert.AreEqual(s, t.Item4, "scale: " + str);
                            Assert.AreEqual(0, t.Item5, "displacement: " + str);
                        }
                        {
                            string str = "[" + b + "+" + s + " * " + idx + "]";
                            var t = AsmSourceTools.ParseMemOperand(str);
                            Assert.AreEqual(true, t.Item1, str);
                            Assert.AreEqual(b, t.Item2, "base: " + str);
                            Assert.AreEqual(idx, t.Item3, "index: " + str);
                            Assert.AreEqual(s, t.Item4, "scale: " + str);
                            Assert.AreEqual(0, t.Item5, "displacement: " + str);
                        }
                        {
                            string str = "[" + s + " * " + idx + "+" + b + "]";
                            var t = AsmSourceTools.ParseMemOperand(str);
                            Assert.AreEqual(true, t.Item1, str);
                            Assert.AreEqual(b, t.Item2, "base: " + str);
                            Assert.AreEqual(idx, t.Item3, "index: " + str);
                            Assert.AreEqual(s, t.Item4, "scale: " + str);
                            Assert.AreEqual(0, t.Item5, "displacement: " + str);
                        }
                        {
                            string str = "[" + idx + " * " + s + "+" + b + "]";
                            var t = AsmSourceTools.ParseMemOperand(str);
                            Assert.AreEqual(true, t.Item1, str);
                            Assert.AreEqual(b, t.Item2, "base: " + str);
                            Assert.AreEqual(idx, t.Item3, "index: " + str);
                            Assert.AreEqual(s, t.Item4, "scale: " + str);
                            Assert.AreEqual(0, t.Item5, "displacement: " + str);
                        }

                        for (int m = 0; m<10; ++m) {
                            long disp = RandLong(32, rnd);
                            {
                                {
                                    string str = "[" + b + "+" + idx + " * " + s + "+" + disp + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + s + " * " + idx + "+" + disp + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + b + "+" + disp + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + b + "+" + disp + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                            }
                            {
                                {
                                    string str = "[" + disp + "+" + b + "+" + idx + " * " + s + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + idx + " * " + s + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + s + " * " + idx + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + disp + "+" + b + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + disp +"+" + b + "]";
                                    var t = AsmSourceTools.ParseMemOperand(str);
                                    Assert.AreEqual(true, t.Item1, str);
                                    Assert.AreEqual(b, t.Item2, "base: " + str);
                                    Assert.AreEqual(idx, t.Item3, "index: " + str);
                                    Assert.AreEqual(s, t.Item4, "scale: " + str);
                                    Assert.AreEqual(disp, t.Item5, "displacement: " + str);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
