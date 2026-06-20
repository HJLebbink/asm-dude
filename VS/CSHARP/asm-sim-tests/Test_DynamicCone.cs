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

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Unit tests for the pure dirty-set-with-kill dynamic cone (<see cref="DynamicCone"/>,
    /// INCREMENTAL_SIM_PLAN.md M3). Effects are hand-built so each test pins one algorithmic invariant:
    /// a line reading only CLEAN locations is excluded (tighter than the static cone), a full overwrite KILLS
    /// dirtiness (so even a later reader of that register is excluded), flags and memory propagate, a loop
    /// reaches a fixpoint, and a deletion seeds its writes at the surviving successor.
    /// </summary>
    [TestClass]
    public class Test_DynamicCone
    {
        private static LineEffects E(Rn[] read, Rn[] write, Flags rf = Flags.NONE, Flags wf = Flags.NONE,
            bool rmem = false, bool wmem = false, Rn[]? kill = null)
            => new(new HashSet<Rn>(read), new HashSet<Rn>(write), new HashSet<Rn>(kill ?? write), rf, wf, rmem, wmem);

        private static StaticFlow Flow(string[] lines)
        {
            var flow = new StaticFlow(new Tools());
            flow.Update(string.Join(Environment.NewLine, lines), removeEmptyLines: false);
            return flow;
        }

        /// <summary>An INSERT-style diff: the changed line is <paramref name="changedIdx"/> in the new
        /// program (old = new minus that line), so <c>AddedNewLines == {changedIdx}</c> seeds the cone.</summary>
        private static InstructionDiff InsertDiff(string[] newLines, int changedIdx)
        {
            var old = new List<string>(newLines);
            old.RemoveAt(changedIdx);
            return InstructionDiff.Compute(Instruction.ParseProgram(old.ToArray()), Instruction.ParseProgram(newLines));
        }

        [TestMethod]
        public void LineReadingOnlyCleanRegisters_IsExcluded()
        {
            // Edit line 0 (writes RAX). Line 1 reads nothing dirty (independent), line 2 reads RAX.
            string[] prog = ["mov rax, 1", "mov rbx, 5", "mov rdx, rax"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([], [Rn.RAX]),
                [1] = E([], [Rn.RBX]),
                [2] = E([Rn.RAX], [Rn.RDX]),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(0), "the changed line");
            Assert.IsFalse(cone.Contains(1), "reads only clean registers — excluded (tighter than the static cone)");
            Assert.IsTrue(cone.Contains(2), "reads the dirty RAX");
        }

        [TestMethod]
        public void FullOverwrite_KillsDirtiness_SoLaterReaderIsExcluded()
        {
            // Edit line 0 (dirties RAX). Line 1 fully overwrites RAX with a constant (clean inputs) ⇒ RAX is
            // clean again ⇒ line 2, though it reads RAX, is NOT affected.
            string[] prog = ["mov rax, 1", "mov rax, 9", "mov rcx, rax"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([], [Rn.RAX]),
                [1] = E([], [Rn.RAX]), // full overwrite ⇒ KillRegs defaults to {RAX}
                [2] = E([Rn.RAX], [Rn.RCX]),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(0));
            Assert.IsFalse(cone.Contains(1), "a clean-input full overwrite recomputes the same value — excluded");
            Assert.IsFalse(cone.Contains(2), "RAX was killed clean by line 1, so this reader is unaffected");
        }

        [TestMethod]
        public void PartialWrite_DoesNotKill_RemainsConservative()
        {
            // Line 1 writes RAX but does NOT fully overwrite it (KillRegs empty — e.g. an 8-bit write), so RAX
            // stays dirty and line 2 (reading RAX) remains in the cone.
            string[] prog = ["mov rax, 1", "mov al, 9", "mov rcx, rax"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([], [Rn.RAX]),
                [1] = E([], [Rn.RAX], kill: []), // partial: writes RAX, kills nothing
                [2] = E([Rn.RAX], [Rn.RCX]),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(2), "a partial write must NOT clear dirtiness (sound over-approximation)");
        }

        [TestMethod]
        public void DirtyFlag_PullsAFlagReaderIntoTheCone_ButNotACleanRegReader()
        {
            string[] prog = ["cmp rax, 1", "adc rbx, 0", "mov rdx, rsi"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([Rn.RAX], [], wf: Flags.CF | Flags.ZF | Flags.SF | Flags.OF),
                [1] = E([Rn.RBX], [Rn.RBX], rf: Flags.CF, wf: Flags.CF | Flags.ZF | Flags.SF | Flags.OF),
                [2] = E([Rn.RSI], [Rn.RDX]),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(1), "adc reads the dirty CF");
            Assert.IsFalse(cone.Contains(2), "reads only the clean RSI");
        }

        [TestMethod]
        public void DirtyMemory_PullsAMemoryReaderIntoTheCone()
        {
            string[] prog = ["mov [rsi], rax", "mov rbx, [rdi]"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([Rn.RSI, Rn.RAX], [], wmem: true),
                [1] = E([Rn.RDI], [Rn.RBX], rmem: true),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(1), "a memory read after a dirty memory write is conservatively in the cone");
        }

        [TestMethod]
        public void Loop_ReachesAFixpoint_AndConesTheDependentBody()
        {
            // mov rbx,5 (edited) feeds a loop body that reads RBX and writes flags; the back-edge must not
            // prevent termination, and the body + the flag-reading branch are in the cone.
            string[] prog = ["mov rbx, 5", "spin: add rax, rbx", "jnz spin"];
            StaticFlow flow = Flow(prog);
            InstructionDiff diff = InsertDiff(prog, 0);
            var eff = new Dictionary<int, LineEffects>
            {
                [0] = E([], [Rn.RBX]),
                [1] = E([Rn.RAX, Rn.RBX], [Rn.RAX], wf: Flags.ZF | Flags.CF | Flags.SF | Flags.OF),
                [2] = E([], [], rf: Flags.ZF),
            };

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, eff, new Dictionary<int, LineEffects>());

            Assert.IsTrue(cone.Contains(0), "the edited line");
            Assert.IsTrue(cone.Contains(1), "the loop body reads the dirty RBX");
            Assert.IsTrue(cone.Contains(2), "the branch reads the flag the body dirtied");
        }

        [TestMethod]
        public void Deletion_SeedsTheDeletedWritesAtTheSuccessor()
        {
            // Delete `mov rbx, 5`; the surviving `add rax, rbx` reads RBX and so must be re-solved even though
            // no NEW line is "changed" — the dirtiness is seeded from the deleted instruction's write set.
            string[] oldLines = ["mov rbx, 5", "add rax, rbx"];
            string[] newLines = ["add rax, rbx"];
            StaticFlow flow = Flow(newLines);
            InstructionDiff diff = InstructionDiff.Compute(
                Instruction.ParseProgram(oldLines), Instruction.ParseProgram(newLines));

            var newEff = new Dictionary<int, LineEffects> { [0] = E([Rn.RAX, Rn.RBX], [Rn.RAX]) };
            var oldEff = new Dictionary<int, LineEffects> { [0] = E([], [Rn.RBX]) }; // the deleted mov rbx,5

            IReadOnlySet<int> cone = DynamicCone.Compute(flow, diff, newEff, oldEff);

            Assert.IsTrue(cone.Contains(0), "the consumer of a deleted instruction's output must be re-solved");
        }
    }
}
