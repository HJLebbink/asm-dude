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
    /// Leak-regression guard for the Z3 native <c>Context</c> lifecycle (the object behind the historical
    /// ~18 GiB native leak and the context-lifecycle AV). <see cref="Z3ContextTracker"/> counts owned
    /// context create/dispose; after a complete create+dispose cycle the live count MUST return to its
    /// baseline. MSTest runs this assembly sequentially, so the process-global counter is uncontaminated.
    /// </summary>
    [TestClass]
    public class Test_Z3ContextTracker
    {
        private static Tools MakeTools() => new(new Dictionary<string, string>());

        [TestMethod]
        public void Test_Z3ContextTracker_State_NoLeak()
        {
            Tools tools = MakeTools();
            long baseline = Z3ContextTracker.Live;
            long createdBefore = Z3ContextTracker.TotalCreated;

            using (State state = new(tools, "!0", "!0"))
            {
                Assert.IsTrue(Z3ContextTracker.Live > baseline, "an owned-context State raises the live count");
            }

            Assert.IsTrue(Z3ContextTracker.TotalCreated > createdBefore, "a State must create at least one Z3 context");
            Assert.AreEqual(baseline, Z3ContextTracker.Live, "disposing the State must release its Z3 context (no leak)");
        }

        [TestMethod]
        public void Test_Z3ContextTracker_DynamicFlow_NoLeak()
        {
            // The per-component engine (INCREMENTAL_SIM_PLAN.md Phase 2) builds one DynamicFlow per
            // component, each owning a Z3 Context (its borrowing states/updates do NOT re-count). After
            // Dispose the live count must return to baseline — guards the engine the refactor introduces
            // and exercises the dispose-on-skip fix on the diamond (jz creates a re-converging join).
            string programStr =
                "           mov rax, 1           " + Environment.NewLine +
                "           jz label1            " + Environment.NewLine +
                "           mov rbx, 2           " + Environment.NewLine +
                "label1:    add rax, rbx         ";

            Tools tools = MakeTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);

            long baseline = Z3ContextTracker.Live; // after StaticFlow settled (its transient opcodes disposed)

            using (DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools))
            {
                Assert.IsTrue(Z3ContextTracker.Live > baseline, "a DynamicFlow owns a Z3 context");
            }

            Assert.AreEqual(baseline, Z3ContextTracker.Live, "disposing the DynamicFlow must release its Z3 context (no leak)");
        }
    }
}
