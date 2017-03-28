using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmTools;

namespace unit_tests_asm_tools
{
    [TestClass]
    public class Test_Operand
    {
        [TestMethod]
        public void Test_Operand_SignExtend_1()
        {   // sign extend 8-bits zero
            ulong value = 0;
            Operand op = new Operand(value + "");
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
        public void Test_Operand_SignExtend_2()
        {
            // sign extend 8-bit negative number
            Operand op = new Operand("0xFF");
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
        public void Test_Operand_SignExtend_3()
        {   // sign extend 16-bit positive number
            Operand op = new Operand("0x1FFF");
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
        public void Test_Operand_NegativeDecimal_1()
        {
            {
                long signedValue = -10;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(8, op.NBits);
            }
            {
                long signedValue = -128;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(8, op.NBits);
            }
            {
                long signedValue = -256;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(16, op.NBits);
            }
            {
                long signedValue = -0x4FFF;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(16, op.NBits);
            }
            {
                long signedValue = -0x4FFF_0000;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(32, op.NBits);
            }
            {
                long signedValue = -0x4FFF_0000_0000_0000;
                ulong unsignedValue = (ulong)signedValue;

                Operand op = new Operand(signedValue.ToString());
                Assert.IsTrue(op.IsImm);
                Assert.AreEqual(unsignedValue, op.Imm);
                Assert.AreEqual(64, op.NBits);
            }
        }
    }
}
