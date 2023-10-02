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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace unit_tests_asm_tools
{
    using System;
    using AsmSim;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Test_ToolsZ3
    {
        [TestMethod]
        public void Test_ToStringBin_1()
        {
            {
                string str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "0b00001010");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 16));
                Assert.AreEqual(str, "0b00000000_00001010");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 32));
                Assert.AreEqual(str, "0b00000000_00000000_00000000_00001010");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(10, 64));
                Assert.AreEqual(str, "0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00001010");
                Console.WriteLine(str);
            }
        }

        [TestMethod]
        public void Test_ToStringHex_1()
        {
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 8));
                Console.WriteLine(str);
                Assert.AreEqual("0x0A", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 16));
                Console.WriteLine(str);
                Assert.AreEqual("0x000A", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 32));
                Console.WriteLine(str);
                Assert.AreEqual("0x0000_000A", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(10, 64));
                Console.WriteLine(str);
                Assert.AreEqual("0x0000_0000_0000_000A", str);
            }
        }

        [TestMethod]
        public void Test_ToStringHex_2()
        {
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFF, 8));
                Console.WriteLine(str);
                Assert.AreEqual("0xFF", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF, 16));
                Console.WriteLine(str);
                Assert.AreEqual("0xFFFF", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF_FFFF, 32));
                Console.WriteLine(str);
                Assert.AreEqual("0xFFFF_FFFF", str);
            }
            {
                string str = ToolsZ3.ToStringHex(ToolsZ3.GetTvArray(0xFFFF_FFFF_FFFF_FFFF, 64));
                Console.WriteLine(str);
                Assert.AreEqual("0xFFFF_FFFF_FFFF_FFFF", str);
            }
        }

        [TestMethod]
        public void Test_ToStringOct_1()
        {
            {
                string str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(10, 8));
                Console.WriteLine(str);
                Assert.AreEqual(str, "0o012");
            }
            {
                string str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(200, 8));
                Console.WriteLine(str);
                Assert.AreEqual(str, "0o310");
            }
            {
                string str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(511, 16));
                Console.WriteLine(str);
                Assert.AreEqual(str, "0o000_777");
            }
            {
                string str = ToolsZ3.ToStringOct(ToolsZ3.GetTvArray(512, 10));
                Console.WriteLine(str);
                Assert.AreEqual(str, "0o1_000");
            }
        }

        [TestMethod]
        public void Test_ToStringDec_1()
        {
            {
                string str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(10, 8));
                Assert.AreEqual(str, "10d");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(200, 8));
                Assert.AreEqual(str, "200d");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(511, 16));
                Assert.AreEqual(str, "511d");
                Console.WriteLine(str);
            }
            {
                string str = ToolsZ3.ToStringDec(ToolsZ3.GetTvArray(512, 10));
                Assert.AreEqual(str, "512d");
                Console.WriteLine(str);
            }
        }
    }
}
