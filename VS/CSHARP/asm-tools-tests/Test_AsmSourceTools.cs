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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace unit_tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using AsmTools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Test_AsmSourceTools
    {
        private static readonly CultureInfo Culture = CultureInfo.CurrentCulture;

        #region Private Stuff
        private static ulong RandUlong(int nBits, Random rand)
        {
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
                    ulong r = (i1 << 33) | (i2 << 2) | (i3 & 0x3);
                    return r & ((1UL << nBits) - 1);
                }
            }
        }

        private static long RandLong(int nBits, Random rand)
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
        #endregion

        [TestMethod]
        public void Test_AsmSourceTools_splitIntoKeywordsPos()
        {
            {
                const string line = "    db \"This string contains the word jmp inside of it\",0";

                List<(int, int, AsmTokenType)> result = new(AsmSourceTools.SplitIntoKeywordsType(line));
                for (int i = 0; i < result.Count; ++i)
                {
                    Console.WriteLine(line[result[i].Item1..result[i].Item2]);
                }
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual("db", line[result[0].Item1..result[0].Item2]);
                Assert.AreEqual("\"This string contains the word jmp inside of it\"", line.Substring(result[1].Item1, result[1].Item2 - result[1].Item1));
                Assert.AreEqual("0", line[result[2].Item1..result[2].Item2]);
            }
            {
                const string line = "	call		??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z";

                List<(int, int, AsmTokenType)> result = new(AsmSourceTools.SplitIntoKeywordsType(line));
                for (int i = 0; i < result.Count; ++i)
                {
                    Console.WriteLine(line[result[i].Item1..result[i].Item2]);
                }
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("call", line[result[0].Item1..result[0].Item2]);
                Assert.AreEqual("??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z", line.Substring(result[1].Item1, result[1].Item2 - result[1].Item1));
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_GetPreviousKeyword()
        {
            const string line = "    mov rax, rbx;bla";
            {
                int begin = 0;
                int end = 8;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 4;
                int end = 8;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 5;
                int end = 8;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("ov", result, msg);
            }
            {
                int begin = 0;
                int end = 7;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("mov", result, msg);
            }
            {
                int begin = 0;
                int end = 6;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual(string.Empty, result, msg);
            }
            {
                int begin = 0;
                int end = 11;
                string result = AsmSourceTools.GetPreviousKeyword(begin, end, line);
                string msg = "line=\"" + line + "\"; result=\"" + result + "\"; begin=" + begin + "; end=" + end;
                Assert.AreEqual("rax", result, msg);
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_Evaluate_1()
        {
            {
                ulong i = 0ul;
                string s = i + string.Empty;
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 0ul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 0ul;
                string s = i.ToString("X", Culture) + "h";
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X", Culture) + "h";
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 1ul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 1ul;
                string s = i.ToString("X", Culture) + "h";
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            if (false) // TODO fix this!
            {
                ulong i = 0xFFul;
                string s = "0x" + i.ToString("X", Culture) + "h";
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            if (false) // TODO fix this!
            {
                ulong i = 0xFFul;
                string s = i.ToString("X", Culture) + "h";
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                ulong i = 0x100ul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(16, nBits, s);
            }
            {
                ulong i = 0xFFFFul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(16, nBits, s);
            }
            {
                ulong i = 0x10000ul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(32, nBits, s);
            }
            {
                ulong i = 0xFFFFFFFFul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(32, nBits, s);
            }
            {
                ulong i = 0x100000000ul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(64, nBits, s);
            }
            {
                ulong i = 0xFFFFFFFFFFFFFFFFul;
                string s = "0x" + i.ToString("X", Culture);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(64, nBits, s);
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_Evaluate_2()
        {
            {
                string s = "1<<2";
                ulong i = 1 << 2;
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                string s = "1 << 2";
                ulong i = 1 << 2;
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                string s = "(1 << 2)";
                ulong i = 1 << 2;
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                string s = " (1<<2) ";
                ulong i = 1 << 2;
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                string s = "(1<<(1+1))";
                ulong i = 1 << (1 + 1);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
            {
                string s = " ( 1 << ( 1 + 1 ) ) ";
                ulong i = 1 << (1 + 1);
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(s);
                Assert.IsTrue(valid, "could not parse: s=" + s);
                Assert.AreEqual(i, value, s);
                Assert.AreEqual(8, nBits, s);
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_parseMnemonic()
        {
            foreach (Mnemonic x in Enum.GetValues(typeof(Mnemonic)))
            {
                Assert.AreEqual(AsmSourceTools.ParseMnemonic(x.ToString(), true), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_parseArch()
        {
            foreach (Arch x in Enum.GetValues(typeof(Arch)))
            {
                Assert.AreEqual(ArchTools.ParseArch(ArchTools.ToString(x), true, true), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_OperandType()
        {
            foreach (Ot1 x1 in Enum.GetValues(typeof(Ot1)))
            {
                foreach (Ot1 x2 in Enum.GetValues(typeof(Ot1)))
                {
                    (Ot1, Ot1) t = AsmSourceTools.SplitOt(AsmSourceTools.MergeOt(x1, x2));
                    Assert.AreEqual(t.Item1, x1, string.Empty);
                    Assert.AreEqual(t.Item2, x2, string.Empty);
                }
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_parseMemOperand()
        {
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

            Random rnd = new((int)DateTime.Now.Ticks);

            Rn[] bases32 = new Rn[] { Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX, Rn.ESP, Rn.EBP, Rn.ESI, Rn.EDI };
            Rn[] index32 = new Rn[] { Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX, Rn.EBP, Rn.ESI, Rn.EDI };

            Rn[] bases64 = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };
            Rn[] index74 = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };

            int[] scales = new int[] { 1, 2, 4, 8 };

            for (int i = 0; i < bases32.Length; ++i)
            {
                Rn b = bases32[i];
                {
                    string str = "[" + b + "]";
                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                    Assert.AreEqual(true, valid, str);
                    Assert.AreEqual(b, baseReg, "base: " + str);
                    Assert.AreEqual(Rn.NOREG, indexReg, "index: " + str);
                    Assert.AreEqual(0, scale, "scale: " + str);
                    Assert.AreEqual(0, displacement, "displacement: " + str);
                }

                for (int j = 0; j < index32.Length; ++j)
                {
                    Rn idx = index32[j];
                    {
                        string str = "[" + b + "+" + idx + "]";
                        (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                        Assert.AreEqual(true, valid, str);
                        Assert.AreEqual(b, baseReg, "base: " + str);
                        Assert.AreEqual(idx, indexReg, "index: " + str);
                        Assert.AreEqual(1, scale, "scale: " + str);
                        Assert.AreEqual(0, displacement, "displacement: " + str);
                    }
                    {
                        string str = "[" + idx + "+" + b + "]";
                        (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                        Assert.AreEqual(true, valid, str);
                        // idx and base can be interchanged
                        // Assert.AreEqual(b, t.Item2, "base: " + str);
                        // Assert.AreEqual(idx, t.Item3, "index: " + str);
                        Assert.AreEqual(1, scale, "scale: " + str);
                        Assert.AreEqual(0, displacement, "displacement: " + str);
                    }

                    for (int k = 0; k < scales.Length; ++k)
                    {
                        int s = scales[k];

                        // Offset = Base + (Index * Scale) + Displacement
                        {
                            string str = "[" + b + "+" + idx + " * " + s + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + b + "+" + s + " * " + idx + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + s + " * " + idx + "+" + b + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + idx + " * " + s + "+" + b + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }

                        for (int m = 0; m < 10; ++m)
                        {
                            long disp = RandLong(32, rnd);
                            {
                                {
                                    string str = "[" + b + "+" + idx + " * " + s + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + s + " * " + idx + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + b + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + b + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                            }
                            {
                                {
                                    string str = "[" + disp + "+" + b + "+" + idx + " * " + s + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + idx + " * " + s + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + s + " * " + idx + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + disp + "+" + b + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + disp + "+" + b + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_Get_Related_Constant()
        {
            int nBits = 64;

            ulong value = 3352562;
            string original = value.ToString(Culture);
            string related = AsmSourceTools.Get_Related_Constant(original, value, nBits);

            Console.WriteLine(related);
        }
    }
}
