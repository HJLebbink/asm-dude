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

    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Unit tests for the pure Tier-1 static dataflow cone (<see cref="DataflowCone"/>, INCREMENTAL_SIM_PLAN.md
    /// M2). Each test pins a soundness/tightness invariant the incremental re-solve relies on: an edit's cone
    /// excludes lines BEFORE it and OTHER CFG components (so they can be reused), includes everything forward-
    /// reachable (so nothing affected is missed), and a deletion seeds the surviving successor.
    /// </summary>
    [TestClass]
    public class Test_DataflowCone
    {
        /// <summary>Build the new StaticFlow (CFG) and the edit diff from the same old/new line arrays,
        /// exactly as the incremental pipeline would (no removal of empty lines ⇒ line indices == array
        /// indices, matching the component engine).</summary>
        private static (StaticFlow flow, InstructionDiff diff) Setup(string[] oldLines, string[] newLines)
        {
            var flow = new StaticFlow(new Tools());
            flow.Update(string.Join(Environment.NewLine, newLines), removeEmptyLines: false);
            InstructionDiff diff = InstructionDiff.Compute(
                Instruction.ParseProgram(oldLines), Instruction.ParseProgram(newLines));
            return (flow, diff);
        }

        [TestMethod]
        public void OperandEdit_ConeExcludesEarlierLines_IncludesItselfAndDownstream()
        {
            string[] oldL = ["mov rax, 1", "mov rbx, 2", "add rax, rbx"];
            string[] newL = ["mov rax, 1", "mov rbx, 3", "add rax, rbx"]; // operand edit at line 1
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            Assert.IsFalse(cone.Contains(0), "line 0 precedes the edit and cannot be affected — it must be reusable");
            Assert.IsTrue(cone.Contains(1), "the edited line must be re-solved");
            Assert.IsTrue(cone.Contains(2), "the downstream consumer of the edited register must be re-solved");
        }

        [TestMethod]
        public void EditInOneFunction_DoesNotConeAnotherComponent()
        {
            // Two unconnected functions (ret severs the fall-through), so funcB is a separate CFG component
            // and an edit in funcA must NOT pull funcB's lines into the cone.
            string[] oldL = ["funcA: mov rax, 1", "ret", "funcB: mov rbx, 2", "ret"];
            string[] newL = ["funcA: mov rax, 9", "ret", "funcB: mov rbx, 2", "ret"]; // edit funcA line 0
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            Assert.IsTrue(cone.Contains(0), "the edited funcA line is in the cone");
            Assert.IsFalse(cone.Contains(2), "funcB is a separate component — must be reusable");
            Assert.IsFalse(cone.Contains(3), "funcB is a separate component — must be reusable");
        }

        [TestMethod]
        public void DeletingAnInstruction_ConesTheSurvivingSuccessor()
        {
            // Deleting `mov rbx, 2` changes the input of the following `add rax, rbx`, which survives as new
            // line 1 — the cone must include it even though the deleted line has no new position.
            string[] oldL = ["mov rax, 1", "mov rbx, 2", "add rax, rbx"];
            string[] newL = ["mov rax, 1", "add rax, rbx"];
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            Assert.IsTrue(cone.Contains(1), "the consumer downstream of the deleted instruction must be re-solved");
            Assert.IsFalse(cone.Contains(0), "the line before the deletion is unaffected — reusable");
        }

        [TestMethod]
        public void EditBeforeABranch_ConeReachesBothTargets()
        {
            // Editing the compare feeds the conditional jump; the cone must follow BOTH the fall-through and
            // the branch target (forward reachability over the CFG).
            string[] oldL = ["cmp rax, 0", "je skip", "mov rbx, 1", "skip: mov rcx, 2"];
            string[] newL = ["cmp rax, 1", "je skip", "mov rbx, 1", "skip: mov rcx, 2"]; // edit line 0
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            Assert.IsTrue(cone.Contains(1), "the jump itself");
            Assert.IsTrue(cone.Contains(2), "the fall-through path");
            Assert.IsTrue(cone.Contains(3), "the branch-target path");
        }

        [TestMethod]
        public void EditAfterABranchMerge_DoesNotConeTheCodeBeforeIt()
        {
            // Editing the merge line must not pull the compare/branch/arms (which precede it) into the cone.
            string[] oldL = ["cmp rax, 0", "je skip", "mov rbx, 1", "skip: mov rcx, 2"];
            string[] newL = ["cmp rax, 0", "je skip", "mov rbx, 1", "skip: mov rcx, 9"]; // edit line 3
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            Assert.IsTrue(cone.Contains(3), "the edited merge line");
            Assert.IsFalse(cone.Contains(0), "the compare precedes the edit — reusable");
            Assert.IsFalse(cone.Contains(1), "the branch precedes the edit — reusable");
            Assert.IsFalse(cone.Contains(2), "the other arm precedes the edit — reusable");
        }

        [TestMethod]
        public void TopologyPreserving_PlainOperandEdit_IsTrue()
        {
            (_, InstructionDiff diff) = Setup(
                ["mov rax, 1", "add rax, rbx"],
                ["mov rax, 1", "add rax, rcx"]); // plain unlabeled operand edit
            Assert.IsTrue(DataflowCone.IsTopologyPreserving(diff), "a plain operand edit cannot change the CFG");
        }

        [TestMethod]
        public void TopologyPreserving_PlainInstructionInsert_IsTrue()
        {
            (_, InstructionDiff diff) = Setup(
                ["mov rax, 1", "add rax, rbx"],
                ["mov rax, 1", "mov rcx, 5", "add rax, rbx"]); // inserted plain instruction
            Assert.IsTrue(DataflowCone.IsTopologyPreserving(diff));
        }

        [TestMethod]
        public void TopologyPreserving_EditingAJump_IsFalse()
        {
            (_, InstructionDiff diff) = Setup(
                ["cmp rax, 0", "je a", "a: mov rbx, 1", "b: mov rcx, 2"],
                ["cmp rax, 0", "je b", "a: mov rbx, 1", "b: mov rcx, 2"]); // retargeted jump
            Assert.IsFalse(DataflowCone.IsTopologyPreserving(diff), "retargeting a jump changes topology");
        }

        [TestMethod]
        public void TopologyPreserving_AddingALabel_IsFalse()
        {
            (_, InstructionDiff diff) = Setup(
                ["mov rax, 1", "jmp done", "mov rbx, 2"],
                ["mov rax, 1", "jmp done", "done: mov rbx, 2"]); // label now defined
            Assert.IsFalse(DataflowCone.IsTopologyPreserving(diff), "introducing a label changes jump targets");
        }

        [TestMethod]
        public void TopologyPreserving_EditingALabeledInstruction_IsFalse_Conservative()
        {
            // Conservative M2 rule: a changed line that carries a label forces the full path even though the
            // label itself did not move. (Sound; M3 may refine with a real CFG-edge diff.)
            (_, InstructionDiff diff) = Setup(
                ["loop: mov rax, 1"],
                ["loop: mov rax, 2"]);
            Assert.IsFalse(DataflowCone.IsTopologyPreserving(diff));
        }

        private static (StaticFlow oldF, StaticFlow newF, InstructionDiff diff) Setup2(string[] oldLines, string[] newLines)
        {
            var oldF = new StaticFlow(new Tools());
            oldF.Update(string.Join(Environment.NewLine, oldLines), removeEmptyLines: false);
            var newF = new StaticFlow(new Tools());
            newF.Update(string.Join(Environment.NewLine, newLines), removeEmptyLines: false);
            InstructionDiff diff = InstructionDiff.Compute(
                Instruction.ParseProgram(oldLines), Instruction.ParseProgram(newLines));
            return (oldF, newF, diff);
        }

        [TestMethod]
        public void TopologyCone_JumpRetarget_SeedsBothOldAndNewTargets()
        {
            // je far -> je end: the jump itself, the OLD target (loses an in-edge) and the NEW target (gains
            // one) must all be re-solved; the compare before the jump is reusable.
            string[] oldL = ["cmp rax, 0", "je far", "mov rbx, 1", "far: mov rcx, 2", "end: mov rdx, 3"];
            string[] newL = ["cmp rax, 0", "je end", "mov rbx, 1", "far: mov rcx, 2", "end: mov rdx, 3"];
            (StaticFlow oldF, StaticFlow newF, InstructionDiff diff) = Setup2(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticConeWithTopology(oldF, newF, diff);

            Assert.IsTrue(cone.Contains(1), "the retargeted jump");
            Assert.IsTrue(cone.Contains(3), "the OLD target lost an in-edge");
            Assert.IsTrue(cone.Contains(4), "the NEW target gained an in-edge");
            Assert.IsFalse(cone.Contains(0), "the compare precedes the edit and its edges are unchanged — reusable");
        }

        [TestMethod]
        public void TopologyCone_EditingALabeledLine_WithNoEdgeChange_IsStillTight()
        {
            // Editing a LABELED instruction's operand (no jump targets move, no edge changes) must NOT re-sim
            // the whole file — only the edit and its forward cone; the line before stays reusable. This is the
            // relaxation: the old conservative guard would have forced a full sim here.
            string[] oldL = ["mov rax, 1", "tgt: mov rbx, 5", "mov rcx, rbx", "ret"];
            string[] newL = ["mov rax, 1", "tgt: mov rbx, 9", "mov rcx, rbx", "ret"];
            (StaticFlow oldF, StaticFlow newF, InstructionDiff diff) = Setup2(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticConeWithTopology(oldF, newF, diff);

            Assert.IsFalse(cone.Contains(0), "the line before a no-edge-change labeled edit is reusable");
            Assert.IsTrue(cone.Contains(1), "the edited labeled line");
            Assert.IsTrue(cone.Contains(2), "its downstream consumer");
        }

        [TestMethod]
        public void ConeIsSubsetOfReachableRegion_NeverTheWholeFileForALateEdit()
        {
            // A cone for a late, self-contained edit must be strictly smaller than the whole document — the
            // property that makes incremental re-solve worthwhile.
            string[] oldL = ["mov rax, 1", "mov rbx, 2", "mov rcx, 3", "mov rdx, 4"];
            string[] newL = ["mov rax, 1", "mov rbx, 2", "mov rcx, 3", "mov rdx, 9"]; // edit last line
            (StaticFlow flow, InstructionDiff diff) = Setup(oldL, newL);

            IReadOnlySet<int> cone = DataflowCone.StaticCone(flow, diff);

            CollectionAssert.AreEquivalent(new[] { 3 }, new List<int>(cone),
                "an independent edit to the last instruction cones only itself");
        }
    }
}
