using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim;

namespace unit_tests_asm_tools
{
    [TestClass]
    public class Test_ToolsZ3
    {
        [TestMethod]
        public void Test_ToStringBin_1()
        {
            {
                var str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "0b00001010");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 16));
                Assert.AreEqual(str, "0b00000000_00001010");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 32));
                Assert.AreEqual(str, "0b00000000_00000000_00000000_00001010");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 64));
                Assert.AreEqual(str, "0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00001010");
                Console.WriteLine(str);
            }
        }

        [TestMethod]
        public void Test_ToStringHex_1()
        {
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "0x0A");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 16));
                Assert.AreEqual(str, "0x000A");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 32));
                Assert.AreEqual(str, "0x0000_000A");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 64));
                Assert.AreEqual(str, "0x0000_0000_0000_000A");
                Console.WriteLine(str);
            }
        }

        [TestMethod]
        public void Test_ToStringHex_2()
        {
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFF, 8));
                Assert.AreEqual(str, "0xFF");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF, 16));
                Assert.AreEqual(str, "0xFFFF");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF_FFFF, 32));
                Assert.AreEqual(str, "0xFFFF_FFFF");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF_FFFF_FFFF_FFFF, 64));
                Assert.AreEqual(str, "0xFFFF_FFFF_FFFF_FFFF");
                Console.WriteLine(str);
            }
        }

        [TestMethod]
        public void Test_ToStringOct_1()
        {
            {
                var str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "0o012");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(200, 8));
                Assert.AreEqual(str, "0o310");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(511, 16));
                Assert.AreEqual(str, "0o000777");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(512, 10));
                Assert.AreEqual(str, "0o1000");
                Console.WriteLine(str);
            }
        }

        [TestMethod]
        public void Test_ToStringDec_1()
        {
            {
                var str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "10d");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(200, 8));
                Assert.AreEqual(str, "200d");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(511, 16));
                Assert.AreEqual(str, "511d");
                Console.WriteLine(str);
            }
            {
                var str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(512, 10));
                Assert.AreEqual(str, "512d");
                Console.WriteLine(str);
            }
        }
    }
}
