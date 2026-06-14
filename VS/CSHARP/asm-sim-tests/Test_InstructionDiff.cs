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

namespace unit_tests_asm_z3
{
    using AsmSim;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System.Collections.Generic;

    /// <summary>
    /// Unit tests for the pure, Z3-free incremental-simulation building block <see cref="InstructionDiff"/>
    /// (INCREMENTAL_SIM_PLAN.md M0). Each test pins an invariant the cone pipeline relies on: that a pure
    /// line shift is recognized as "no instruction change" (Tier 0), and that an operand edit / insert /
    /// delete is localized to exactly the changed instruction (so the cone can be seeded from it).
    /// </summary>
    [TestClass]
    public class Test_InstructionDiff
    {
        private static IReadOnlyList<Instruction> Parse(params string[] lines) => Instruction.ParseProgram(lines);

        [TestMethod]
        public void IdenticalPrograms_NoChange()
        {
            var a = Parse("mov rax, 1", "add rax, rbx", "mov rcx, rax");
            InstructionDiff d = InstructionDiff.Compute(a, Parse("mov rax, 1", "add rax, rbx", "mov rcx, rax"));

            Assert.IsTrue(d.HasNoInstructionChange);
            Assert.AreEqual(0, d.AddedNewLines.Count);
            Assert.AreEqual(0, d.RemovedOldLines.Count);
            Assert.AreEqual(3, d.NewToOld.Count);
            Assert.AreEqual(0, d.NewToOld[0]);
            Assert.AreEqual(1, d.NewToOld[1]);
            Assert.AreEqual(2, d.NewToOld[2]);
        }

        [TestMethod]
        public void OperandEdit_IsLocalizedToTheChangedLine()
        {
            // `add rax, rbx` -> `add rax, rcx`: only line 1 changes; lines 0 and 2 keep their mapping.
            var oldP = Parse("mov rax, 1", "add rax, rbx", "mov rcx, rax");
            var newP = Parse("mov rax, 1", "add rax, rcx", "mov rcx, rax");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            Assert.IsFalse(d.HasNoInstructionChange);
            CollectionAssert.AreEqual(new[] { 1 }, new List<int>(d.AddedNewLines));
            CollectionAssert.AreEqual(new[] { 1 }, new List<int>(d.RemovedOldLines));
            // The unchanged instructions on either side still align.
            Assert.AreEqual(0, d.NewToOld[0]);
            Assert.AreEqual(2, d.NewToOld[2]);
            Assert.IsFalse(d.NewToOld.ContainsKey(1));
        }

        [TestMethod]
        public void RegToImm_IsDetectedAsAChange()
        {
            // `mov rax, rbx` -> `mov rax, 7` (reg operand replaced by immediate).
            InstructionDiff d = InstructionDiff.Compute(Parse("mov rax, rbx"), Parse("mov rax, 7"));
            Assert.IsFalse(d.HasNoInstructionChange);
            CollectionAssert.AreEqual(new[] { 0 }, new List<int>(d.AddedNewLines));
            CollectionAssert.AreEqual(new[] { 0 }, new List<int>(d.RemovedOldLines));
        }

        [TestMethod]
        public void InsertingABlankLine_IsNotAnInstructionChange_AndRemapsTheShift()
        {
            // A blank line inserted before the last instruction renumbers it 1 -> 2, but no instruction
            // was added/removed: Tier 0 must hold and the mapping must carry the +1 shift.
            var oldP = Parse("mov rax, 1", "add rax, rbx");
            var newP = Parse("mov rax, 1", "", "add rax, rbx");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            Assert.IsTrue(d.HasNoInstructionChange, "a blank-line insert must not count as an instruction change");
            Assert.IsTrue(d.HasOnlyInertChanges, "a blank line is inert, so Tier-0 reuse is valid");
            Assert.AreEqual(0, d.NewToOld[0]);          // mov stays at line 0
            Assert.AreEqual(1, d.NewToOld[2]);          // add shifted from old line 1 to new line 2
            // The only added line is the blank (a non-instruction), so it does not break Tier 0.
            CollectionAssert.AreEqual(new[] { 1 }, new List<int>(d.AddedNewLines));
        }

        [TestMethod]
        public void CommentOnlyEdit_IsNotAnInstructionChange()
        {
            // Changing a comment's text must produce zero instruction change (comments don't affect the sim).
            var oldP = Parse("mov rax, 1", "; first version", "add rax, rbx");
            var newP = Parse("mov rax, 1", "; totally different comment", "add rax, rbx");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            Assert.IsTrue(d.HasNoInstructionChange);
            Assert.AreEqual(0, d.NewToOld[0]);
            Assert.AreEqual(2, d.NewToOld[2]);
        }

        [TestMethod]
        public void InsertingAnInstruction_IsAnInstructionChange()
        {
            var oldP = Parse("mov rax, 1", "mov rbx, 2");
            var newP = Parse("mov rax, 1", "mov rcx, 3", "mov rbx, 2");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            Assert.IsFalse(d.HasNoInstructionChange);
            CollectionAssert.AreEqual(new[] { 1 }, new List<int>(d.AddedNewLines));
            Assert.AreEqual(0, d.RemovedOldLines.Count);
            // Both surrounding instructions are preserved, the second one shifted down by one.
            Assert.AreEqual(0, d.NewToOld[0]);
            Assert.AreEqual(1, d.NewToOld[2]);
        }

        [TestMethod]
        public void DeletingAnInstruction_IsAnInstructionChange()
        {
            var oldP = Parse("mov rax, 1", "mov rcx, 3", "mov rbx, 2");
            var newP = Parse("mov rax, 1", "mov rbx, 2");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            Assert.IsFalse(d.HasNoInstructionChange);
            Assert.AreEqual(0, d.AddedNewLines.Count);
            CollectionAssert.AreEqual(new[] { 1 }, new List<int>(d.RemovedOldLines));
            Assert.AreEqual(0, d.NewToOld[0]);
            Assert.AreEqual(2, d.NewToOld[1]); // surviving mov rbx,2 maps from old line 2
        }

        [TestMethod]
        public void AddingALabel_IsDetectedAsAChange()
        {
            // A label-only line is NONE-mnemonic, but its LABEL participates in equality, so introducing a
            // label is an added (non-instruction) NEW line — Tier 0 still holds (no instruction moved), yet
            // the label line itself is flagged as added so a topology-aware tier can react to it.
            var oldP = Parse("mov rax, 1", "jmp done", "mov rbx, 2");
            var newP = Parse("mov rax, 1", "jmp done", "done:", "mov rbx, 2");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);

            CollectionAssert.AreEqual(new[] { 2 }, new List<int>(d.AddedNewLines));
            Assert.IsTrue(d.New[2].Label.Length > 0);
            Assert.IsFalse(d.New[2].IsInstruction);
            // A label-only line is NONE-mnemonic, so HasNoInstructionChange stays true — but it is a jump
            // target (NOT inert), so Tier-0 reuse MUST be blocked (adding a label is a topology change).
            Assert.IsTrue(d.HasNoInstructionChange);
            Assert.IsFalse(d.HasOnlyInertChanges, "adding a label changes topology — Tier-0 reuse must not fire");
        }

        [TestMethod]
        public void CommentEdit_AllowsTier0Reuse()
        {
            var oldP = Parse("mov rax, 1", "; first", "add rax, rbx");
            var newP = Parse("mov rax, 1", "; second", "add rax, rbx");
            InstructionDiff d = InstructionDiff.Compute(oldP, newP);
            Assert.IsTrue(d.HasOnlyInertChanges, "a comment-only change is inert");
        }

        [TestMethod]
        public void EmptyToNonEmpty_AllNewLinesAreAdditions()
        {
            InstructionDiff d = InstructionDiff.Compute(Parse(), Parse("mov rax, 1", "ret"));
            Assert.IsFalse(d.HasNoInstructionChange);
            Assert.AreEqual(2, d.AddedNewLines.Count);
            Assert.AreEqual(0, d.RemovedOldLines.Count);
            Assert.AreEqual(0, d.NewToOld.Count);
        }

        [TestMethod]
        public void Instruction_EqualityIsFieldWise()
        {
            Assert.AreEqual(Instruction.Parse("add rax, rbx"), Instruction.Parse("add  rax,  rbx")); // whitespace-insensitive parse
            Assert.AreNotEqual(Instruction.Parse("add rax, rbx"), Instruction.Parse("add rax, rcx"));
            Assert.AreNotEqual(Instruction.Parse("mov rax, 1"), Instruction.Parse("add rax, 1"));
        }
    }
}
