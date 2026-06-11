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

    using AsmSim;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// End-to-end tests for <see cref="ComponentEvaluator"/> (the forward worklist that applies a loop
    /// strategy over a real CFG component). Acyclic programs must agree with the legacy demand-driven
    /// extraction; loops exercise the per-strategy precision (the post-loop invariant is the discriminator).
    /// </summary>
    [TestClass]
    public class Test_ComponentEvaluator
    {
        private static Tools CreateTools(LoopHandling loop)
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },
                { "model", "false" },
                { "proof", "false" },
                { "timeout", "60000" },
            };
            return new Tools(settings) { LoopHandling = loop };
        }

        private static (StaticFlow sFlow, DynamicFlow dFlow) Build(string program, LoopHandling loop)
        {
            Tools tools = CreateTools(loop);
            StaticFlow sFlow = new(tools);
            sFlow.Update(program, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            return (sFlow, dFlow);
        }

        private static bool IsFullyConcrete(State s, Rn reg)
        {
            foreach (Tv t in s.GetTvArray(reg))
            {
                if (t != Tv.ZERO && t != Tv.ONE)
                {
                    return false;
                }
            }
            return true;
        }

        // ── Acyclic: the worklist agrees with the legacy extraction on straight-line code ───────────────
        [TestMethod]
        public void StraightLine_AgreesWithLegacyExtraction()
        {
            string program =
                "          mov rax, 10      " + Environment.NewLine +   // 0
                "          mov rbx, 20      " + Environment.NewLine +   // 1
                "          mov rcx, rax     ";                          // 2

            (StaticFlow sFlow, DynamicFlow dFlow) = Build(program, LoopHandling.Accept);
            using (dFlow)
            using (ComponentEvaluator ev = new(dFlow, sFlow))
            {
                for (int line = 0; line <= 2; ++line)
                {
                    State? evBefore = ev.Before(line);
                    State? legacy = dFlow.Create_States_Before(line, 0);
                    Assert.IsNotNull(evBefore, $"evaluator has a before-state for line {line}");
                    Assert.IsNotNull(legacy, $"legacy has a before-state for line {line}");
                    Assert.IsTrue(State.Equiv(evBefore, legacy), $"line {line}: worklist must match legacy on straight-line code");
                    legacy.Dispose();
                }
            }
        }

        // ── Loop: post-loop invariant is kept by ModSetHavoc/Fixpoint, lost by Accept ───────────────────
        private static Tv[] PostLoopRax(LoopHandling loop)
        {
            // rax=5 is set before the loop and never touched inside ⇒ a loop invariant. The counter rcx is
            // decremented each iteration. At the post-loop line, rax should still be 5 unless the strategy
            // discards invariants (Accept).
            string program =
                "          mov rax, 5       " + Environment.NewLine +   // 0  invariant
                "          mov rcx, 3       " + Environment.NewLine +   // 1  counter
                "lbl:      dec rcx          " + Environment.NewLine +   // 2  loop head
                "          jnz lbl          " + Environment.NewLine +   // 3  back-edge
                "          mov rdx, rax     ";                          // 4  post-loop

            (StaticFlow sFlow, DynamicFlow dFlow) = Build(program, loop);
            using (dFlow)
            using (ComponentEvaluator ev = new(dFlow, sFlow))
            {
                State? before4 = ev.Before(4);
                Assert.IsNotNull(before4, "post-loop line has a before-state");
                Assert.AreEqual(1, ev.LoopCount, "exactly one loop detected");
                return before4.GetTvArray(Rn.RAX);
            }
        }

        [TestMethod]
        public void Loop_Accept_LosesInvariant()
        {
            Tv[] rax = PostLoopRax(LoopHandling.Accept);
            bool concrete = true;
            foreach (Tv t in rax)
            {
                if (t != Tv.ZERO && t != Tv.ONE)
                {
                    concrete = false;
                }
            }
            Assert.IsFalse(concrete, "Accept makes the post-loop invariant unknown");
        }

        [TestMethod]
        public void Loop_ModSetHavoc_KeepsInvariant()
        {
            Tv[] rax = PostLoopRax(LoopHandling.ModSetHavoc);
            ulong? value = ToolsZ3.ToUlong(rax);
            Assert.IsTrue(value.HasValue, "rax must be concrete after the loop (invariant preserved)");
            Assert.AreEqual(5UL, value.Value, "the loop-invariant rax=5 survives ModSetHavoc");
        }

        [TestMethod]
        public void Loop_Fixpoint_KeepsInvariant()
        {
            Tv[] rax = PostLoopRax(LoopHandling.Fixpoint);
            ulong? value = ToolsZ3.ToUlong(rax);
            Assert.IsTrue(value.HasValue, "rax must be concrete after the loop (invariant recovered by fixpoint)");
            Assert.AreEqual(5UL, value.Value, "the loop-invariant rax=5 survives the fixpoint");
        }

        // ── Bounded loop with a KNOWN trip count (4) and a known result, across all strategies ──────────
        // rax += 10 four times ⇒ rax = 40 after the loop. Only an unrolling strategy can recover the exact
        // 40; the summarizing strategies (Accept/ModSetHavoc/Fixpoint) must report rax as non-concrete
        // because rax is written in the loop body (so it is forgotten/widened).
        private static (string strategy, string raxBin, ulong? raxVal, int loops, int iters) RunBounded(LoopHandling loop)
        {
            string program =
                "          mov rax, 0       " + Environment.NewLine +   // 0  accumulator
                "          mov rcx, 4       " + Environment.NewLine +   // 1  trip count = 4
                "lbl:      add rax, 10      " + Environment.NewLine +   // 2  loop head
                "          dec rcx          " + Environment.NewLine +   // 3
                "          jnz lbl          " + Environment.NewLine +   // 4  back-edge
                "          mov rdx, rax     ";                          // 5  post-loop: rax == 40 iff precise

            (StaticFlow sFlow, DynamicFlow dFlow) = Build(program, loop);
            using (dFlow)
            using (ComponentEvaluator ev = new(dFlow, sFlow))
            {
                State? before5 = ev.Before(5);
                Assert.IsNotNull(before5, "post-loop line has a before-state");
                Tv[] rax = before5.GetTvArray(Rn.RAX);
                return (loop.ToString(), ToolsZ3.ToStringBin(rax), ToolsZ3.ToUlong(rax), ev.LoopCount, ev.LoopIterations);
            }
        }

        [TestMethod]
        public void BoundedLoop_FullUnroll_ComputesExactResult()
        {
            // `add rax,10` four times ⇒ rax = 40. Unrolling the known-bounded loop recovers it exactly.
            (_, _, ulong? val, int loops, int iters) = RunBounded(LoopHandling.FullUnroll);
            Assert.AreEqual(1, loops, "one loop");
            Assert.AreEqual(4, iters, "unrolled exactly the trip count (4)");
            Assert.IsTrue(val.HasValue, "FullUnroll yields a concrete post-loop rax");
            Assert.AreEqual(40UL, val.Value, "rax = 4 * 10");
        }

        [TestMethod]
        public void BoundedLoop_Fixpoint_RecoversEvenInvariant_ButNotTheValue()
        {
            // rax starts 0 and += 10 each iteration ⇒ always EVEN: bit 0 is a sound loop invariant the
            // fixpoint must recover (merge of all-even values keeps bit 0 = 0), independent of the budget.
            // The accumulator VALUE is still forgotten (varying ⇒ non-concrete). ModSetHavoc, which havocs
            // the whole register, loses even the even-ness — so this is a real precision difference, not
            // budget-dependent. (NB: the fixpoint's high "bounded" bits are a budget artifact and are NOT
            // asserted here — only bit 0, which is sound.)
            Tv[] fixRax = RunBoundedRax(LoopHandling.Fixpoint);
            Assert.AreEqual(Tv.ZERO, fixRax[0], "fixpoint recovers the even invariant (bit 0 = 0)");
            Assert.IsFalse(ToolsZ3.ToUlong(fixRax).HasValue, "the accumulator value itself is still forgotten");

            Tv[] havocRax = RunBoundedRax(LoopHandling.ModSetHavoc);
            Assert.AreEqual(Tv.UNKNOWN, havocRax[0], "ModSetHavoc forgets even the even-ness (bit 0 unknown)");
        }

        private static Tv[] RunBoundedRax(LoopHandling loop)
        {
            string program =
                "          mov rax, 0       " + Environment.NewLine +   // 0
                "          mov rcx, 4       " + Environment.NewLine +   // 1
                "lbl:      add rax, 10      " + Environment.NewLine +   // 2
                "          dec rcx          " + Environment.NewLine +   // 3
                "          jnz lbl          " + Environment.NewLine +   // 4
                "          mov rdx, rax     ";                          // 5

            (StaticFlow sFlow, DynamicFlow dFlow) = Build(program, loop);
            using (dFlow)
            using (ComponentEvaluator ev = new(dFlow, sFlow))
            {
                State? before5 = ev.Before(5);
                Assert.IsNotNull(before5);
                return before5.GetTvArray(Rn.RAX);
            }
        }

        [TestMethod]
        public void BoundedLoop_Summarizing_ForgetsTheAccumulator()
        {
            // rax is written in the body ⇒ a summarizing strategy must NOT report a concrete post-loop rax.
            foreach (LoopHandling loop in new[] { LoopHandling.Accept, LoopHandling.ModSetHavoc, LoopHandling.Fixpoint })
            {
                (_, _, ulong? val, _, _) = RunBounded(loop);
                Assert.IsFalse(val.HasValue, $"{loop} must forget the body-written accumulator (got {val})");
            }
        }
    }
}
