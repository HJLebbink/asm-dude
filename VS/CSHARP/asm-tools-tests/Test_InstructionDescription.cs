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

namespace unit_tests
{
    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // Tests InstructionDescription.Render — the operand-substitution used to make a placeholder-bearing
    // instruction description operand-aware ("Move {1} into {0}" + mov rax,rbx -> "Move rbx into rax").
    // The placeholder TEXT itself lives in the instruction data (GENERAL rows in signature-hand-1.txt) and
    // is verified end-to-end by GetHover_Mnemonic_IsOperandAware.
    [TestClass]
    public class Test_InstructionDescription
    {
        [TestMethod]
        public void Render_FillsPlaceholdersFromOperandsInOrder()
        {
            Assert.AreEqual("Move rbx into rax", InstructionDescription.Render("Move {1} into {0}", ["rax", "rbx"]));
            Assert.AreEqual("Add 0x10 to [rbp-8]", InstructionDescription.Render("Add {1} to {0}", ["[rbp-8]", "0x10"]));
            Assert.AreEqual("Increment rcx", InstructionDescription.Render("Increment {0}", ["rcx"]));
        }

        [TestMethod]
        public void Render_PassesThroughDescriptionsWithoutPlaceholders()
        {
            // The common case: a normal description is returned verbatim.
            Assert.AreEqual("Set CF flag.", InstructionDescription.Render("Set CF flag.", []));
            Assert.AreEqual("Near return to calling procedure.", InstructionDescription.Render("Near return to calling procedure.", ["ignored"]));
        }

        [TestMethod]
        public void Render_UsesGenericOpName_WhenNoConcreteOperand()
        {
            // Completion has no operands, so placeholders must stay readable (not leak literal "{1}").
            Assert.AreEqual("Add op2 to op1", InstructionDescription.Render("Add {1} to {0}", []));
            Assert.AreEqual("Move op2 into rax", InstructionDescription.Render("Move {1} into {0}", ["rax"]));
        }
    }
}
