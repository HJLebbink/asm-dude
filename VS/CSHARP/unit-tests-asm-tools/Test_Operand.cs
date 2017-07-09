// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmTools;

namespace unit_tests_asm_tools
{
    [TestClass]
    public class Test_Operand
    {
        [TestMethod]
        public void Test_Operand_Register_1()
        {
            foreach (Rn reg in Enum.GetValues(typeof(Rn)))
            {
                if (reg == Rn.NOREG) continue;

                var regStr = reg.ToString();
                var op = new Operand(regStr, true);
                Assert.IsFalse(op.IsMem, "regStr=" + regStr);
                Assert.IsFalse(op.IsImm, "regStr=" + regStr);
                Assert.IsTrue(op.IsReg, "regStr="+ regStr);

                Assert.AreEqual(reg, op.Rn);
                Assert.AreEqual(RegisterTools.NBits(reg), op.NBits);
                Assert.AreEqual(op.Type, Ot1.reg);
            }
        }

        [TestMethod]
        public void Test_Operand_Constant_SignExtend_1()
        {   // sign extend 8-bits zero
            ulong value = 0;
            Operand op = new Operand(value + "", false);
            Assert.IsTrue(op.IsImm);
            Assert.AreEqual(0ul, op.Imm);
            Assert.AreEqual(8, op.NBits);

            op.SignExtend(16);
            Assert.AreEqual(0ul, op.Imm);
            Assert.AreEqual(16, op.NBits);

            op.SignExtend(32);
            Assert.AreEqual(0ul, op.Imm);
            Assert.AreEqual(32, op.NBits);

            op.SignExtend(64);
            Assert.AreEqual(0ul, op.Imm);
            Assert.AreEqual(64, op.NBits);
        }

        [TestMethod]
        public void Test_Operand_Constant_SignExtend_2()
        {
            // sign extend 8-bit negative number
            Operand op = new Operand("0xFF", false);
            Assert.IsTrue(op.IsImm);
            Assert.AreEqual(0xFFul, op.Imm);
            Assert.AreEqual(8, op.NBits);

            op.SignExtend(16);
            Assert.AreEqual(0xFFFFul, op.Imm);
            Assert.AreEqual(16, op.NBits);

            op.SignExtend(32);
            Assert.AreEqual(0xFFFF_FFFFul, op.Imm);
            Assert.AreEqual(32, op.NBits);

            op.SignExtend(64);
            Assert.AreEqual(0xFFFF_FFFF_FFFF_FFFF, op.Imm);
            Assert.AreEqual(64, op.NBits);
        }

        [TestMethod]
        public void Test_Operand_Constant_SignExtend_3()
        {   // sign extend 16-bit positive number
            Operand op = new Operand("0x1FFF", false);
            Assert.IsTrue(op.IsImm);
            Assert.AreEqual(0x1FFFul, op.Imm);
            Assert.AreEqual(16, op.NBits);

            op.SignExtend(32);
            Assert.AreEqual(0x1FFFul, op.Imm);
            Assert.AreEqual(32, op.NBits);

            op.SignExtend(64);
            Assert.AreEqual(0x1FFFul, op.Imm);
            Assert.AreEqual(64, op.NBits);
        }

        [TestMethod]
        public void Test_Operand_Constant_NegativeDecimal_1()
        {
            {
                long signedValue = -10;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(8, op.NBits);
            }
            {
                long signedValue = -128;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(8, op.NBits);
            }
            {
                long signedValue = -256;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(16, op.NBits);
            }
            {
                long signedValue = -0x4FFF;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(16, op.NBits);
            }
            {
                long signedValue = -0x4FFF_0000;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(32, op.NBits);
            }
            {
                long signedValue = -0x4FFF_0000_0000_0000;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString(), true);
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(64, op.NBits);
            }
        }
    }
}
