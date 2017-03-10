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
        {
            ulong value = 0;
            Operand op = new Operand(value + "");
            Assert.IsTrue(op.IsImm);
            Assert.IsTrue(op.Imm == 0);
            Assert.IsTrue(op.NBits == 8);

            op.SignExtend(16);
            Assert.IsTrue(op.Imm == 0);
            Assert.IsTrue(op.NBits == 16);

            op.SignExtend(32);
            Assert.IsTrue(op.Imm == 0);
            Assert.IsTrue(op.NBits == 32);

            op.SignExtend(64);
            Assert.IsTrue(op.Imm == 0);
            Assert.IsTrue(op.NBits == 64);
        }

        [TestMethod]
        public void Test_Operand_SignExtend_2()
        {
            Operand op = new Operand("0xFF");
            Assert.IsTrue(op.IsImm);
            Assert.IsTrue(op.Imm == 0xFF);
            Assert.IsTrue(op.NBits == 8);

            op.SignExtend(16);
            Assert.IsTrue(op.Imm == 0xFFFF);
            Assert.IsTrue(op.NBits == 16);

            op.SignExtend(32);
            Assert.IsTrue(op.Imm == 0xFFFFFFFF);
            Assert.IsTrue(op.NBits == 32);

            op.SignExtend(64);
            Assert.IsTrue(op.Imm == 0xFFFFFFFFFFFFFFFF);
            Assert.IsTrue(op.NBits == 64);
        }

        [TestMethod]
        public void Test_Operand_SignExtend_3()
        {
            Operand op = new Operand("0x1FFF");
            Assert.IsTrue(op.IsImm);
            Assert.IsTrue(op.Imm == 0x1FFF);
            Assert.IsTrue(op.NBits == 16);

            op.SignExtend(32);
            Assert.IsTrue(op.Imm == 0x1FFF);
            Assert.IsTrue(op.NBits == 32);

            op.SignExtend(64);
            Assert.IsTrue(op.Imm == 0x1FFF);
            Assert.IsTrue(op.NBits == 64);
        }
    }
}
