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
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AsmSim;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// INCREMENTAL_SIM_PLAN.md Phase 2 / S1 spike. Resolves Part-A item 5 — the one genuine UNKNOWN:
    /// does a DynamicFlow built for a component that ends in <c>ret</c> or an unconditional <c>jmp</c>
    /// still produce usable per-line before/after states for extraction (the editor needs them per line)?
    /// Test-only observation; no production change, no editor change.
    /// </summary>
    [TestClass]
    public class Test_DynamicFlowComponent
    {
        private static Tools CreateTools(int timeOut = 60000)
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },
                { "model", "false" },
                { "proof", "false" },
                { "timeout", timeOut.ToString(CultureInfo.InvariantCulture) },
            };
            return new Tools(settings);
        }

        [TestMethod]
        public void Spike_RetTerminatedComponent_PerLineStates()
        {
            Tools tools = CreateTools();
            string programStr =
                "mov rax, 10" + Environment.NewLine +
                "ret";
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            int after0 = dFlow.Create_States_After(0).Count();
            int before1 = dFlow.Create_States_Before(1).Count();
            int after1 = dFlow.Create_States_After(1).Count();
            Console.WriteLine($"[SPIKE ret] after(0)={after0}; before(ret=1)={before1}; after(ret=1)={after1}");

            // FINDING (item 5 resolved): a ret-terminated component produces NON-EMPTY per-line states
            // (all counts ≥ 1) — the plan's "empty results" fear does not reproduce. The states the
            // editor extraction needs are the per-instruction before/after, and they prove the values:
            Assert.IsTrue(after0 > 0, "after-state of line 0 must exist for per-line extraction");
            Assert.IsTrue(before1 > 0, "before-state of the ret line must exist (= after line 0)");
            AsmTestTools.AreEqual(Rn.RAX, 10, dFlow.Create_States_After(0).First()); // after `mov rax,10`
            AsmTestTools.AreEqual(Rn.RAX, 10, dFlow.Create_States_Before(1).First()); // entering `ret`

            // OBSERVATION (not a failure): the state AFTER `ret` havocs registers (return to an unknown
            // caller), so the post-ret end state is UNKNOWN — correct, and irrelevant to editor extraction
            // (the editor annotates instruction lines from their before/after, not the post-ret end).
            State end = dFlow.Create_EndState;
            Console.WriteLine($"[SPIKE ret] post-ret end-state RAX is unknown (havoc) — expected. endState null={end is null}");
        }

        [TestMethod]
        public void Spike_JmpTerminatedComponent_PerLineStates()
        {
            // A component whose last reachable instruction is an unconditional jmp to its own start
            // (self-contained loop body). Observe the per-line state availability.
            Tools tools = CreateTools();
            string programStr =
                "start:  mov rax, 7" + Environment.NewLine +
                "        jmp start";
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            int after0 = dFlow.Create_States_After(0).Count();
            int before1 = dFlow.Create_States_Before(1).Count();
            int after1 = dFlow.Create_States_After(1).Count();
            Console.WriteLine($"[SPIKE jmp] after(0)={after0}; before(jmp=1)={before1}; after(jmp=1)={after1}");

            // FINDING: the self-loop also produces NON-EMPTY per-line states (no "empty results"). The
            // RAX value at the loop head is a single-pass MERGE of {initial-unknown, post-jmp}, so it may
            // read UNKNOWN rather than the loop-invariant 7 — DynamicFlow does one merge, not a fixpoint.
            // Captured as an observation (loops are an S2/S3 concern; the linear editor sim doesn't follow
            // jumps at all today), not asserted as a value.
            Assert.IsTrue(after0 > 0, "after-state of line 0 must exist for per-line extraction");
            Assert.IsTrue(before1 > 0, "before-state of the jmp line must exist");
            State s0 = dFlow.Create_States_After(0).First();
            Console.WriteLine($"[SPIKE jmp] RAX after `mov rax,7` at loop head = {ToolsZ3.ToStringBin(s0.GetTvArray(Rn.RAX))} (UNKNOWN ⇒ single-pass merge, no fixpoint)");
        }

        [TestMethod]
        public void MultiRoot_CoversAllEntries_OfMultiEntryComponent()
        {
            // The S1 gate (Problem-1): funcA (line 0) and funcB (line 2) are two entries that share a tail
            // (shared, line 4) — ONE weakly-connected component. A SINGLE forward root from the min line (0)
            // reaches funcA's path but NOT funcB (only reachable by walking 3->4 backwards). Multi-root
            // seeding (all in-degree-0 entries from ComputeComponentEntryLines) must cover funcB too.
            Tools tools = CreateTools();
            string programStr =
                "funcA:     mov rax, 1           " + Environment.NewLine +
                "           jmp shared           " + Environment.NewLine +
                "funcB:     mov rbx, 2           " + Environment.NewLine +
                "           mov rcx, 3           " + Environment.NewLine +
                "shared:    add rax, rbx         " + Environment.NewLine +
                "           ret                  ";
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            IReadOnlyDictionary<int, int> lineToComponent = sFlow.ComputeLineToComponent();
            IReadOnlyDictionary<int, List<int>> entries = sFlow.ComputeComponentEntryLines();
            int componentId = lineToComponent[0];
            List<int> roots = entries[componentId];
            Assert.IsTrue(roots.Contains(0) && roots.Contains(2), "the component has entries at funcA(0) and funcB(2)");

            // Single root from line 0 must NOT cover funcB (line 2) — demonstrates the under-coverage the
            // multi-root fix addresses (and that the 4-arg Construct now honors startLine).
            using (DynamicFlow single = Runner.Construct_DynamicFlow_Forward(sFlow, 0, sFlow.NLines * 2, tools))
            {
                Assert.AreEqual(0, single.Create_States_After(2).Count(), "single-root from line 0 must NOT reach funcB");
            }

            // Multi-root from all entries: funcB IS covered and its body proves rbx=2; funcA still covered.
            using (DynamicFlow multi = Runner.Construct_DynamicFlow_Forward(sFlow, roots, tools))
            {
                List<State> afterFuncB = multi.Create_States_After(2).ToList();
                Assert.IsTrue(afterFuncB.Count > 0, "multi-root must cover funcB (line 2)");
                AsmTestTools.AreEqual(Rn.RBX, 2, afterFuncB.First());

                List<State> afterFuncA = multi.Create_States_After(0).ToList();
                Assert.IsTrue(afterFuncA.Count > 0, "funcA (line 0) is still covered");
                AsmTestTools.AreEqual(Rn.RAX, 1, afterFuncA.First());
            }
        }

        [TestMethod]
        public void Component_DetectsUnreachableCode_AfterAlwaysTakenConditionalJump()
        {
            // The file's "#region Unreachable code" semantic: after `mov al,8; cmp al,8` ⇒ ZF=1, so
            // `jz target` is ALWAYS taken and the fall-through `mov al,1` is UNREACHABLE. The linear sim
            // detects this (marks it inconsistent / all-X). Does the DynamicFlow component engine? If NOT,
            // it has LOST the unreachable-code feature — a regression, not the "improvement" I claimed.
            Tools tools = CreateTools();
            string programStr =
                "        mov al, 8         " + Environment.NewLine +   // 0
                "        cmp al, 8         " + Environment.NewLine +   // 1  -> ZF=1
                "        jz target         " + Environment.NewLine +   // 2  -> always taken
                "        mov al, 1         " + Environment.NewLine +   // 3  -> UNREACHABLE (fall-through)
                "target: mov rbx, rax      ";                          // 4
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State? before3 = dFlow.Create_States_Before(3, 0); // state entering the unreachable mov al,1
            Assert.IsNotNull(before3, "line 3 has a fall-through edge from the jz, so a (before) state exists");
            Assert.AreEqual(Tv.ZERO, before3.IsConsistent,
                "fall-through into line 3 is UNREACHABLE (jz always taken) — component must detect it inconsistent");
        }

        [TestMethod]
        public void Component_MergeAtLabel_KeepsValueFromOnlyReachablePath()
        {
            // Triage of the real file's "line 10: only in B". The label `target` is reached ONLY via the
            // always-taken jz (where al=8); the fall-through from the unreachable `mov al,1` contributes
            // nothing satisfiable. So on every REACHABLE path to the label, al=8. Question: does the
            // component merge KEEP al=8 (⇒ it is genuinely MORE precise than the linear sim, which resets
            // to all-unknown at the label after the inconsistent fall-through — that is why linear emits
            // nothing and the component does, the "only in B"), or does it merge al down to unknown (⇒ the
            // "only in B" is near-empty noise)? Verify, don't assume.
            Tools tools = CreateTools();
            string programStr =
                "        mov al, 8         " + Environment.NewLine +   // 0
                "        cmp al, 8         " + Environment.NewLine +   // 1  -> ZF=1
                "        jz target         " + Environment.NewLine +   // 2  -> always taken
                "        mov al, 1         " + Environment.NewLine +   // 3  -> UNREACHABLE fall-through
                "target: mov rbx, rax      ";                          // 4  -> the merge point (label)
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State? before4 = dFlow.Create_States_Before(4, 0); // entering `target: mov rbx, rax`
            Assert.IsNotNull(before4, "the label is reachable, so a (before) state exists");
            Assert.AreNotEqual(Tv.ZERO, before4.IsConsistent, "the label IS reachable (via the taken jump)");
            AsmTestTools.AreEqual(Rn.AL, 8, before4); // al=8 survives the merge (only the reachable path counts)
        }
    }
}
