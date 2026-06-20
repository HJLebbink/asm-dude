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
    using Microsoft.Z3;

    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Strategy-level tests for the pluggable loop handling (<see cref="ILoopStrategy"/>): each strategy is
    /// driven with a hand-built loop header — an entry state with an invariant register (rax=5) and a
    /// counter (rcx=3), plus a body that decrements rcx and leaves rax alone — so the precision difference
    /// between strategies is asserted directly, independent of the (separate) graph/worklist driver.
    /// </summary>
    [TestClass]
    public class Test_LoopStrategy
    {
        private static (Tools tools, Context ctx) CreateSharedTools()
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },
                { "model", "false" },
                { "proof", "false" },
                { "timeout", "60000" },
            };
            Tools t0 = new(settings);
            Context ctx = new(new Dictionary<string, string>(t0.ContextSettings));
            Tools tools = new(t0) { SharedCtx = ctx };
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RCX = true;
            return (tools, ctx);
        }

        /// <summary>Entry state: rax=5 (invariant), rcx=3 (counter).</summary>
        private static State MakeEntry(Tools tools)
        {
            State entry = new(tools, "!0", "!0");
            using StateUpdate u = new("!0", "!1", tools);
            u.Set(Rn.RAX, 5UL);
            u.Set(Rn.RCX, 3UL);
            entry.Update_Forward(u);
            return entry;
        }

        /// <summary>One loop iteration: rcx := rcx - 1, rax untouched.</summary>
        private static Func<State, State> MakeBody(Tools tools, Context ctx)
        {
            int counter = 0;
            return s =>
            {
                State s2 = new(s); // copy (borrows ctx)
                string nk = s2.HeadKey + "!body" + System.Threading.Interlocked.Increment(ref counter);
                using StateUpdate b = new(s2.HeadKey, nk, tools);
                BitVecExpr prev = Tools.Create_Key(Rn.RCX, s2.HeadKey, ctx);
                b.Set(Rn.RCX, ctx.MkBVSub(prev, ctx.MkBV(1UL, 64)));
                s2.Update_Forward(b);
                return s2;
            };
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

        private static readonly ModSet CounterModSet = new(new HashSet<Rn> { Rn.RCX }, Flags.NONE);

        [TestMethod]
        public void Entry_HasInvariantAndCounter()
        {
            (Tools tools, Context ctx) = CreateSharedTools();
            using (ctx)
            {
                using State entry = MakeEntry(tools);
                AsmTestTools.AreEqual(Rn.RAX, 5, entry);
                AsmTestTools.AreEqual(Rn.RCX, 3, entry);
            }
        }

        [TestMethod]
        public void Accept_LosesEvenTheInvariant()
        {
            (Tools tools, Context ctx) = CreateSharedTools();
            using (ctx)
            {
                using State entry = MakeEntry(tools);
                using State header = new AcceptStrategy().ResolveHeader(entry, MakeBody(tools, ctx), CounterModSet, LoopBudget.Default);
                // Crudest summary: everything is unknown, including the loop-invariant rax.
                Assert.IsFalse(IsFullyConcrete(header, Rn.RAX), "Accept makes the header fully unknown");
                Assert.IsFalse(IsFullyConcrete(header, Rn.RCX), "Accept makes the header fully unknown");
            }
        }

        [TestMethod]
        public void ModSetHavoc_KeepsInvariant_ForgetsCounter()
        {
            (Tools tools, Context ctx) = CreateSharedTools();
            using (ctx)
            {
                using State entry = MakeEntry(tools);
                using State header = new ModSetHavocStrategy().ResolveHeader(entry, MakeBody(tools, ctx), CounterModSet, LoopBudget.Default);
                AsmTestTools.AreEqual(Rn.RAX, 5, header);                       // invariant preserved
                Assert.IsFalse(IsFullyConcrete(header, Rn.RCX), "counter is havoc'd"); // counter forgotten
            }
        }

        [TestMethod]
        public void PeelOnce_KeepsInvariant_ForgetsCounter()
        {
            (Tools tools, Context ctx) = CreateSharedTools();
            using (ctx)
            {
                using State entry = MakeEntry(tools);
                using State header = new PeelOnceStrategy().ResolveHeader(entry, MakeBody(tools, ctx), CounterModSet, LoopBudget.Default);
                AsmTestTools.AreEqual(Rn.RAX, 5, header);
                Assert.IsFalse(IsFullyConcrete(header, Rn.RCX), "counter is non-concrete after peel+havoc");
            }
        }

        [TestMethod]
        public void Fixpoint_KeepsInvariant_ConvergesCounterToUnknown()
        {
            (Tools tools, Context ctx) = CreateSharedTools();
            using (ctx)
            {
                using State entry = MakeEntry(tools);
                using State header = new FixpointStrategy().ResolveHeader(entry, MakeBody(tools, ctx), CounterModSet, LoopBudget.Default);
                AsmTestTools.AreEqual(Rn.RAX, 5, header);                       // invariant recovered by the fixpoint
                Assert.IsFalse(IsFullyConcrete(header, Rn.RCX), "the varying counter converges to unknown");
            }
        }

        [TestMethod]
        public void Factory_MapsEveryHandlingToItsKind()
        {
            foreach (LoopHandling h in Enum.GetValues<LoopHandling>())
            {
                Assert.AreEqual(h, LoopStrategies.For(h).Kind, $"factory must return the {h} strategy");
            }
        }
    }
}
