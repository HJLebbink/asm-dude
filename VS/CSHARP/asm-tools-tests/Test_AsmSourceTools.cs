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
    using AsmSourceTools;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;

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
                Assert.AreEqual("\"This string contains the word jmp inside of it\"", line[result[1].Item1..result[1].Item2]);
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
                Assert.AreEqual("??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z", line[result[1].Item1..result[1].Item2]);
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
            foreach (Mnemonic x in Enum.GetValues<Mnemonic>())
            {
                Assert.AreEqual(AsmSourceTools.ParseMnemonic(x.ToString(), true), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }

        [TestMethod]
        public void Test_AsmSourceTools_parseArch()
        {
            foreach (Arch x in Enum.GetValues<Arch>())
            {
                Assert.AreEqual(ArchTools.ParseArch(ArchTools.ToString(x), true, true), x,
                    "Parsing string " + x.ToString() + " does not yield the same enumeration.");
            }
        }

        // Canonicalise a DNF requirement for comparison: members sorted within each AND-group,
        // groups sorted; '+' joins AND-members, '|' joins OR-groups. Uses only ArchTools.ToString
        // (rendering), so a wrong grouping from the parser under test produces a different string.
        private static string NormalizeDnf(Arch[][] dnf)
        {
            var groups = new List<string>();
            foreach (Arch[] g in dnf)
            {
                groups.Add(string.Join("+", g.Select(a => ArchTools.ToString(a)).OrderBy(x => x)));
            }
            groups.Sort(StringComparer.Ordinal);
            return string.Join("|", groups);
        }

        [TestMethod]
        public void Test_ArchTools_ParseArchExpression_Dnf()
        {
            // (VL AND F) OR AVX10.1  =>  (AVX512_VL AND AVX512_F) OR AVX10
            Assert.AreEqual(
                "AVX10|AVX512_F+AVX512_VL",
                NormalizeDnf(ArchTools.ParseArchExpression("(AVX512VL AND AVX512F) OR AVX10.1")));

            // simple OR
            Assert.AreEqual(
                "AVX10|AVX512_F",
                NormalizeDnf(ArchTools.ParseArchExpression("AVX512F OR AVX10.1")));

            // distribution of a trailing implicit-AND factor over an OR group:
            // (F OR AVX10.1) GFNI => (F AND GFNI) OR (AVX10 AND GFNI)   [GFNI maps to AVX512_GFNI]
            Assert.AreEqual(
                "AVX10+AVX512_GFNI|AVX512_F+AVX512_GFNI",
                NormalizeDnf(ArchTools.ParseArchExpression("(AVX512F OR AVX10.1) GFNI")));

            // implicit AND by juxtaposition
            Assert.AreEqual("AVX+SM4", NormalizeDnf(ArchTools.ParseArchExpression("AVX SM4")));

            // single flag
            Assert.AreEqual("AVX", NormalizeDnf(ArchTools.ParseArchExpression("AVX")));

            // empty / unparseable => no constraint (empty DNF)
            Assert.AreEqual(string.Empty, NormalizeDnf(ArchTools.ParseArchExpression(string.Empty)));
            Assert.AreEqual(0, ArchTools.ParseArchExpression(string.Empty).Length);
        }

        [TestMethod]
        public void Test_ArchTools_ParseArchDnf_AndOr_BackwardCompatible()
        {
            // '+' = AND within a group, ',' = OR between groups
            Assert.AreEqual(
                "AVX10|AVX512_F+AVX512_VL",
                NormalizeDnf(ArchTools.ParseArchDnf("AVX512_VL+AVX512_F,AVX10", false, false)));

            // backward compatible: a comma-only list (the historical format) parses as a pure OR of
            // singleton groups — so old signature files keep their exact meaning.
            Assert.AreEqual("AVX|AVX2", NormalizeDnf(ArchTools.ParseArchDnf("AVX,AVX2", false, false)));

            // ToStringDnf writes the machine format and ParseArchDnf reads it back unchanged
            Arch[][] dnf = [[Arch.ARCH_AVX512_VL, Arch.ARCH_AVX512_F], [Arch.ARCH_AVX10]];
            string s = ArchTools.ToStringDnf(dnf);
            Assert.AreEqual("AVX512_VL+AVX512_F,AVX10", s);
            Assert.AreEqual(NormalizeDnf(dnf), NormalizeDnf(ArchTools.ParseArchDnf(s, false, false)));
        }

        [TestMethod]
        public void Test_RegisterTools_TileRegisters()
        {
            // Tile registers are parsed (reflective Register_cache_), classified, and arch-tagged.
            Assert.AreEqual(Rn.TMM3, RegisterTools.ParseRn("TMM3", true), "TMM3 must parse");
            Assert.AreEqual(Rn.TMM5, RegisterTools.ParseRn("tmm5", false), "lowercase tmm5 must parse");
            Assert.IsTrue(RegisterTools.IsRn("TMM0", true));

            Assert.AreEqual(Arch.ARCH_AMX, RegisterTools.GetArch(Rn.TMM0), "tiles belong to AMX");
            Assert.AreEqual(RegisterType.TILE, RegisterTools.GetRegisterType(Rn.TMM7));
            Assert.IsTrue(RegisterTools.IsTileRegister(Rn.TMM0));
            Assert.IsFalse(RegisterTools.IsTileRegister(Rn.ZMM0), "a ZMM is not a tile");
        }

        [TestMethod]
        public void Test_AsmSignatureTools_TileOperand()
        {
            // "TMM" (and digit-bearing variants the generator may leave) map to the TMMREG operand.
            CollectionAssert.AreEqual(
                new[] { AsmSignatureEnum.TMMREG },
                AsmSignatureTools.Parse_Operand_Type_Enum("TMM", true));
            CollectionAssert.AreEqual(
                new[] { AsmSignatureEnum.TMMREG },
                AsmSignatureTools.Parse_Operand_Type_Enum("TMM3", true));

            // A tile register is allowed exactly where a TMMREG operand is expected (drives completion).
            var tmmOperand = new HashSet<AsmSignatureEnum> { AsmSignatureEnum.TMMREG };
            var zmmOperand = new HashSet<AsmSignatureEnum> { AsmSignatureEnum.ZMMREG };
            Assert.IsTrue(AsmSignatureTools.Is_Allowed_Reg(Rn.TMM0, tmmOperand), "tile allowed for TMMREG");
            Assert.IsFalse(AsmSignatureTools.Is_Allowed_Reg(Rn.ZMM0, tmmOperand), "ZMM not allowed for TMMREG");
            Assert.IsFalse(AsmSignatureTools.Is_Allowed_Reg(Rn.TMM0, zmmOperand), "tile not allowed for ZMMREG");
        }

        [TestMethod]
        public void Test_AsmSourceTools_OperandType()
        {
            foreach (Ot1 x1 in Enum.GetValues<Ot1>())
            {
                foreach (Ot1 x2 in Enum.GetValues<Ot1>())
                {
                    (Ot1, Ot1) t = AsmSourceTools.SplitOt(AsmSourceTools.MergeOt(x1, x2));
                    Assert.AreEqual(t.Item1, x1, string.Empty);
                    Assert.AreEqual(t.Item2, x2, string.Empty);
                }
            }
        }

        /// <summary>
        /// Invariant guarded: each Ot2 value is a SINGLE distinct bit, so OR-combining allowed operand forms
        /// and testing membership with HasFlag is a true set test. A 2-bit "packed nibble" encoding (the
        /// historical bug, MA0062) makes HasFlag report PHANTOM members — e.g. {reg_mem | mem_reg} would
        /// falsely contain reg_reg. This test fails under that encoding (PopCount != 1 and the phantom asserts).
        /// </summary>
        [TestMethod]
        public void Test_AsmSourceTools_Ot2_SingleBitFlags_NoPhantomMembers()
        {
            Ot2[] values = Enum.GetValues<Ot2>();
            foreach (Ot2 v in values)
            {
                Assert.AreEqual(1, BitOperations.PopCount((uint)v), $"{v} must be a single bit");
            }
            Assert.AreEqual(values.Length, values.Select(v => (uint)v).Distinct().Count(), "all bits distinct");

            // MergeOt must land on the matching named value (HasFlag membership relies on this).
            Assert.AreEqual(Ot2.reg_mem, AsmSourceTools.MergeOt(Ot1.reg, Ot1.mem));
            Assert.AreEqual(Ot2.mem_reg, AsmSourceTools.MergeOt(Ot1.mem, Ot1.reg));
            Assert.AreEqual(Ot2.UNKNOWN_UNKNOWN, AsmSourceTools.MergeOt(Ot1.UNKNOWN, Ot1.UNKNOWN));

            // The bug case: a set of two "crossed" forms must NOT gain phantom members.
            Ot2 allowed = Ot2.reg_mem | Ot2.mem_reg;
            Assert.IsTrue(allowed.HasFlag(Ot2.reg_mem));
            Assert.IsTrue(allowed.HasFlag(Ot2.mem_reg));
            Assert.IsFalse(allowed.HasFlag(Ot2.reg_reg), "reg_reg is NOT allowed and must not be a phantom member");
            Assert.IsFalse(allowed.HasFlag(Ot2.mem_mem), "mem_mem is NOT allowed and must not be a phantom member");
        }

        /// <summary>Same single-bit / no-phantom invariant for the 3-operand Ot3 set (backed by ulong).</summary>
        [TestMethod]
        public void Test_AsmSourceTools_Ot3_SingleBitFlags_NoPhantomMembers()
        {
            Ot3[] values = Enum.GetValues<Ot3>();
            foreach (Ot3 v in values)
            {
                Assert.AreEqual(1, BitOperations.PopCount((ulong)v), $"{v} must be a single bit");
            }
            Assert.AreEqual(values.Length, values.Select(v => (ulong)v).Distinct().Count(), "all 64 bits distinct");

            Assert.AreEqual(Ot3.reg_reg_imm, AsmSourceTools.MergeOt(Ot1.reg, Ot1.reg, Ot1.imm));
            Assert.AreEqual(Ot3.mem_reg_imm, AsmSourceTools.MergeOt(Ot1.mem, Ot1.reg, Ot1.imm));

            Ot3 allowed = Ot3.reg_mem_imm | Ot3.mem_reg_imm;
            Assert.IsTrue(allowed.HasFlag(Ot3.reg_mem_imm));
            Assert.IsTrue(allowed.HasFlag(Ot3.mem_reg_imm));
            Assert.IsFalse(allowed.HasFlag(Ot3.reg_reg_imm), "reg_reg_imm must not be a phantom member");
            Assert.IsFalse(allowed.HasFlag(Ot3.mem_mem_imm), "mem_mem_imm must not be a phantom member");
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

            Rn[] bases32 = [Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX, Rn.ESP, Rn.EBP, Rn.ESI, Rn.EDI];
            Rn[] index32 = [Rn.EAX, Rn.EBX, Rn.ECX, Rn.EDX, Rn.EBP, Rn.ESI, Rn.EDI];
            _ = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };
            _ = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX, Rn.RSP, Rn.RBP, Rn.RSI, Rn.RDI };

            int[] scales = [1, 2, 4, 8];

            for (int i = 0; i < bases32.Length; ++i)
            {
                Rn b = bases32[i];
                {
                    string str = "[" + b + "]";
                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
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
                        (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                        Assert.AreEqual(true, valid, str);
                        Assert.AreEqual(b, baseReg, "base: " + str);
                        Assert.AreEqual(idx, indexReg, "index: " + str);
                        Assert.AreEqual(1, scale, "scale: " + str);
                        Assert.AreEqual(0, displacement, "displacement: " + str);
                    }
                    {
                        string str = "[" + idx + "+" + b + "]";
                        (bool valid, Rn _, Rn _, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
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
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + b + "+" + s + " * " + idx + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + s + " * " + idx + "+" + b + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                            Assert.AreEqual(true, valid, str);
                            Assert.AreEqual(b, baseReg, "base: " + str);
                            Assert.AreEqual(idx, indexReg, "index: " + str);
                            Assert.AreEqual(s, scale, "scale: " + str);
                            Assert.AreEqual(0, displacement, "displacement: " + str);
                        }
                        {
                            string str = "[" + idx + " * " + s + "+" + b + "]";
                            (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
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
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + s + " * " + idx + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + b + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + b + "+" + disp + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
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
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + idx + " * " + s + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + b + "+" + disp + "+" + s + " * " + idx + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + s + " * " + idx + "+" + disp + "+" + b + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
                                    Assert.AreEqual(true, valid, str);
                                    Assert.AreEqual(b, baseReg, "base: " + str);
                                    Assert.AreEqual(idx, indexReg, "index: " + str);
                                    Assert.AreEqual(s, scale, "scale: " + str);
                                    Assert.AreEqual(disp, displacement, "displacement: " + str);
                                }
                                {
                                    string str = "[" + idx + " * " + s + "+" + disp + "+" + b + "]";
                                    (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int _, string _) = AsmSourceTools.Parse_Mem_Operand(str);
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
