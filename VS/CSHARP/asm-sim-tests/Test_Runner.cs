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
    using AsmSim.Mnemonics;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// NOTE: Most tests in this class used to work. A regression was introduced.
    /// See Test_DynamicFlow class comment in Test_ExecutionTree.cs for root cause.
    /// Only Test_Runner_Several_Mnemonics works (uses SimpleStep, not DynamicFlow).
    /// </summary>
    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
    public class Test_Runner
    {
        private const bool LogToDisplay = AsmTestTools.LOG_TO_DISPLAY;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        #region Private Methods

        private Tools CreateTools(int timeOut = AsmTestTools.DEFAULT_TIMEOUT)
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },    // enable generation of unsat cores
                { "model", "false" },         // enable model generation
                { "proof", "false" },         // enable proof generation
                { "timeout", timeOut.ToString(Culture) },
            };
            return new Tools(settings);
        }

        private State CreateState(Tools tools)
        {
            string tailKey = "!0"; // Tools.CreateKey(tools.Rand);
            string headKey = tailKey;
            return new State(tools, tailKey, headKey);
        }

        // ── Runner.IsRedundantInstruction — the pre-freeze redundancy primitive ─────────────────────────
        // This is the canonical "does this instruction change the tracked state?" query. Unlike asking
        // Is_Redundant on a frozen after-state (see Test_State_Frozen_DropsOverwrittenRegisterHistory), it
        // queries an UNFROZEN probe, so it sees both the before- and after-values — including for a VALUE
        // rewrite over a register that the freeze/Compress would otherwise prune. See REDUNDANT_DIAGNOSTICS_PLAN.md.

        /// <summary>Steps `before` through `setup` (left to right), returning the frozen state after the last.</summary>
        private State StepThrough(Tools tools, params string[] setup)
        {
            State s = this.CreateState(tools);
            foreach (string line in setup)
            {
                s = AsmTestTools.Step_Forward(line, s);
            }
            return s;
        }

        [TestMethod]
        public void Test_Runner_IsRedundant_ValueRewrite()
        {
            // rax already holds 10 ⇒ a second `mov rax,10` is redundant. This is the case the frozen
            // after-state cannot prove (history pruned) but the unfrozen probe can.
            Tools tools = this.CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;

            State before = this.StepThrough(tools, "mov rax, 10"); // rax = 10
            RedundancyVerdict? v = Runner.IsRedundantInstruction("mov rax, 10", before);

            Assert.IsTrue(v.HasValue && v.Value.IsRedundant, "rewriting rax with the value it already holds is redundant");
        }

        [TestMethod]
        public void Test_Runner_IsRedundant_StructuralIdentity()
        {
            // `mov rax, rax` is redundant even when rax is UNKNOWN (the update links the two keys directly).
            Tools tools = this.CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;

            State before = this.CreateState(tools); // rax unknown
            RedundancyVerdict? v = Runner.IsRedundantInstruction("mov rax, rax", before);

            Assert.IsTrue(v.HasValue && v.Value.IsRedundant, "an identity move is redundant regardless of value");
        }

        [TestMethod]
        public void Test_Runner_IsRedundant_StateChangingNotRedundant()
        {
            // A real definition (rax was unknown) and a value change must NOT be flagged.
            Tools tools = this.CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;

            State unknownRax = this.CreateState(tools);
            RedundancyVerdict? define = Runner.IsRedundantInstruction("mov rax, 10", unknownRax);
            Assert.IsFalse(define.HasValue && define.Value.IsRedundant, "defining rax (unknown -> 10) changes it");

            State raxIs10 = this.StepThrough(tools, "mov rax, 10");
            RedundancyVerdict? change = Runner.IsRedundantInstruction("mov rax, 11", raxIs10);
            Assert.IsFalse(change.HasValue && change.Value.IsRedundant, "10 -> 11 changes rax");
        }

        [TestMethod]
        public void Test_Runner_IsRedundant_NopAndNonInstruction_AreNull()
        {
            // NOP writes nothing ⇒ not "redundant" (WrittenCount 0). A non-instruction line ⇒ null.
            Tools tools = this.CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;

            State s = this.CreateState(tools);
            RedundancyVerdict? nop = Runner.IsRedundantInstruction("nop", s);
            Assert.IsFalse(nop.HasValue && nop.Value.IsRedundant, "NOP writes nothing, so it is not flagged redundant");

            RedundancyVerdict? label = Runner.IsRedundantInstruction("some_label:", s);
            Assert.IsFalse(label.HasValue, "a non-instruction line has no redundancy verdict");
        }

        /// <summary>Returns Forward, Backward State</summary>
        private State Equal_Forward_Backward(string programStr, bool logToDispay2, Tools tools)
        {
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (logToDispay2)
            {
                Console.WriteLine(sFlow.ToString());
            }

            DynamicFlow dFlow0 = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            DynamicFlow dFlow1 = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            // Console.WriteLine("Forward:" + tree0.ToString(dFlow0));
            // Console.WriteLine("Backward:" + tree1.ToString(dFlow1));

            State state0 = dFlow0.Create_EndState;
            State state1 = dFlow1.Create_EndState;

            if (logToDispay2)
            {
                Console.WriteLine("=================================================================");
                Console.WriteLine("Forward:");
                Console.WriteLine(state0);
                Console.WriteLine("=================================================================");
                Console.WriteLine("Backward:");
                Console.WriteLine(state1);
                Console.WriteLine("=================================================================");
            }
            AsmTestTools.AreEqual(state0, state1);
            return state0;
        }
        #endregion Private Methods

        [TestMethod]
        public void Test_Runner_Several_Mnemonics()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = true;

            Mnemonic[] a = [Mnemonic.AND, Mnemonic.ADD, Mnemonic.OR, Mnemonic.SUB, Mnemonic.XOR, Mnemonic.ADC, Mnemonic.SBB];

            ulong value_rax = 10;
            ulong value_rbx = 20;
            foreach (Mnemonic mnemonic in a)
            {
                Console.WriteLine("Mnemonic: " + mnemonic);

                string line1 = "mov rax, " + value_rax.ToString(Culture);
                string line2 = "mov rbx, " + value_rbx.ToString(Culture);
                string line3 = mnemonic + " rax, rbx";

                State state_forward = this.CreateState(tools);
                state_forward = AsmTestTools.Step_Forward(line1, state_forward);
                state_forward = AsmTestTools.Step_Forward(line2, state_forward);
                state_forward = AsmTestTools.Step_Forward(line3, state_forward);

                State state_backward = this.CreateState(tools);
                state_backward = AsmTestTools.Step_Backward(line3, state_backward);
                state_backward = AsmTestTools.Step_Backward(line2, state_backward);
                state_backward = AsmTestTools.Step_Backward(line1, state_backward);

                Assert.IsNotNull(state_backward);
                Assert.IsNotNull(state_forward);
                AsmTestTools.AreEqual(state_backward, state_forward);
            }
        }

        [TestMethod]
        public void Test_Runner_CF_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.CF = true;

            string programStr =
                "           clc       ; clear CF                " + Environment.NewLine +
                "           stc       ; set CF                  " + Environment.NewLine +
                "           cmc       ; complement CF           ";
            State state = this.Equal_Forward_Backward(programStr, LogToDisplay, tools);
            AsmTestTools.AreEqual(Flags.CF, Tv.ZERO, state);
        }

        [TestMethod]
        public void Test_Runner_Mov_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        0               " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "           mov     rbx,        rax             ";
            State state = this.Equal_Forward_Backward(programStr, LogToDisplay, tools);
            AsmTestTools.AreEqual(Rn.RAX, 0, state);
            AsmTestTools.AreEqual(Rn.RBX, 0, state);
        }

        [TestMethod]
        public void Test_Runner_Add_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        10              " + Environment.NewLine +
                "           mov     rbx,        20              " + Environment.NewLine +
                "           add     rax,        rbx             ";
            State state = this.Equal_Forward_Backward(programStr, LogToDisplay, tools);

            ulong result = 10 + 20;
            uint nBits = 64;
            AsmTestTools.AreEqual(Rn.RAX, result, state);
            AsmTestTools.AreEqual(Rn.RBX, 20, state);
            AsmTestTools.AreEqual(Flags.CF, AsmTestTools.ToTv5(AsmTestTools.Calc_CF_Add(nBits, 10, 20)), state);
            AsmTestTools.AreEqual(Flags.OF, AsmTestTools.ToTv5(AsmTestTools.Calc_OF_Add(nBits, 10, 20)), state);
            AsmTestTools.AreEqual(Flags.AF, AsmTestTools.ToTv5(AsmTestTools.Calc_AF_Add(10, 20)), state);
            AsmTestTools.AreEqual(Flags.PF, AsmTestTools.ToTv5(AsmTestTools.Calc_PF(result)), state);
            AsmTestTools.AreEqual(Flags.ZF, AsmTestTools.ToTv5(AsmTestTools.Calc_ZF(result)), state);
            AsmTestTools.AreEqual(Flags.SF, AsmTestTools.ToTv5(AsmTestTools.Calc_SF(nBits, result)), state);
        }

        [TestMethod]
        public void Test_Runner_Add_2()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        10              " + Environment.NewLine +
                string.Empty + Environment.NewLine +
                "           mov     rbx,        20              " + Environment.NewLine +
                string.Empty + Environment.NewLine +
                "           add     rax,        rbx             ";
            State state = this.Equal_Forward_Backward(programStr, LogToDisplay, tools);

            ulong result = 10 + 20;
            uint nBits = 64;
            AsmTestTools.AreEqual(Rn.RAX, result, state);
            AsmTestTools.AreEqual(Rn.RBX, 20, state);
            AsmTestTools.AreEqual(Flags.CF, AsmTestTools.ToTv5(AsmTestTools.Calc_CF_Add(nBits, 10, 20)), state);
            AsmTestTools.AreEqual(Flags.OF, AsmTestTools.ToTv5(AsmTestTools.Calc_OF_Add(nBits, 10, 20)), state);
            AsmTestTools.AreEqual(Flags.AF, AsmTestTools.ToTv5(AsmTestTools.Calc_AF_Add(10, 20)), state);
            AsmTestTools.AreEqual(Flags.PF, AsmTestTools.ToTv5(AsmTestTools.Calc_PF(result)), state);
            AsmTestTools.AreEqual(Flags.ZF, AsmTestTools.ToTv5(AsmTestTools.Calc_ZF(result)), state);
            AsmTestTools.AreEqual(Flags.SF, AsmTestTools.ToTv5(AsmTestTools.Calc_SF(nBits, result)), state);
        }

        [TestMethod]
        public void Test_Runner_Xor_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        20              " + Environment.NewLine +
                "           mov     rbx,        rax             " + Environment.NewLine +
                "           xor     rbx,        rax             ";
            State state = this.Equal_Forward_Backward(programStr, LogToDisplay, tools);

            ulong result = 20 ^ 20;
            uint nBits = 64;
            AsmTestTools.AreEqual(Rn.RBX, result, state);
            AsmTestTools.AreEqual(Flags.CF, Tv.ZERO, state);
            AsmTestTools.AreEqual(Flags.OF, Tv.ZERO, state);
            AsmTestTools.AreEqual(Flags.AF, Tv.UNDEFINED, state);
            AsmTestTools.AreEqual(Flags.PF, AsmTestTools.ToTv5(AsmTestTools.Calc_PF(result)), state);
            AsmTestTools.AreEqual(Flags.ZF, AsmTestTools.ToTv5(AsmTestTools.Calc_ZF(result)), state);
            AsmTestTools.AreEqual(Flags.SF, AsmTestTools.ToTv5(AsmTestTools.Calc_SF(nBits, result)), state);
        }

        /// <summary>
        /// If rax is already 0, jump to label, if it is not yet 0, make it 0. Afterwards rax is ALWAYS 0.
        /// </summary>
        [TestMethod]
        public void Test_Runner_Jmp_1()
        {

            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        0               " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            if (LogToDisplay)
            {
                Console.WriteLine("DynamicFlow:\n" + dFlow.ToString(sFlow));
                Console.WriteLine(state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, 0, state);
        }


        /// <summary>
        /// If rax is 0, jump to label1, and set rbx 10, otherwise set rbx 10. Afterwards rbx is ALWAYS 10.
        /// </summary>
        [TestMethod]
        public void Test_Runner_Jmp_2()
        {
            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           je      label1                      " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "label2:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            if (LogToDisplay)
            {
                Console.WriteLine(state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RBX, 10, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_3()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "label2:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State state = dFlow.Create_EndState;
            if (LogToDisplay)
            {
                Console.WriteLine(state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, 10, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_4()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     rax,        20              ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            // if (logToDisplay) Console.WriteLine(dFlow.ToString(sFlow));

            State state = dFlow.Create_EndState;
            // if (logToDisplay) Console.WriteLine(state);

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_5()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            bool logToDisplay2 = true;
            tools.Quiet = true; // !logToDisplay2;

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (logToDisplay2)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state);

            Microsoft.Z3.BoolExpr branch_Condition = dFlow.Get_Branch_Condition(0);
            if (logToDisplay2)
            {
                Console.WriteLine("Branch Condition:" + branch_Condition);
            }

            {
                State state2a = new(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (logToDisplay2)
                {
                    Console.WriteLine("state with ZF = true:\n" + state2a);
                }

                AsmTestTools.IsTrue(state2a.IsConsistent);
                AsmTestTools.AreEqual(Rn.RAX, 10, state2a); // TODO why is 10 / 20 reversed?
            }
            {
                State state2b = new(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (logToDisplay2)
                {
                    Console.WriteLine("state with ZF = false:\n" + state2b);
                }

                AsmTestTools.IsTrue(state2b.IsConsistent);
                AsmTestTools.AreEqual(Rn.RAX, 20, state2b);
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_5a()
        {
            string programStr =
                "           mov     rax,        1               " + Environment.NewLine +
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State? state0 = dFlow.Create_States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_5b()
        {
            string programStr =
                "           mov     rax,        0               " + Environment.NewLine +
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State? state0 = dFlow.Create_States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, 10, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_6()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            // DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            Microsoft.Z3.BoolExpr branch_Condition = dFlow.Get_Branch_Condition(0);

            {
                State state2a = new(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (LogToDisplay)
                {
                    Console.WriteLine("state with ZF = true:\n" + state2a);
                }

                AsmTestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
            }
            {
                State state2b = new(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (LogToDisplay)
                {
                    Console.WriteLine("state with ZF = false:\n" + state2b);
                }

                AsmTestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_7()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     rbx,        rax             " + Environment.NewLine +
                "           jz      label3                      " + Environment.NewLine +
                "label3:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            // var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            // DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);
            AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state);

            Microsoft.Z3.BoolExpr branch_Condition = dFlow.Get_Branch_Condition(0);
            {
                State state2a = new(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (LogToDisplay)
                {
                    Console.WriteLine("state with ZF = true:\n" + state2a);
                }

                AsmTestTools.IsTrue(state2a.IsConsistent);
                AsmTestTools.AreEqual(Rn.RAX, 10, state2a);
            }
            {
                State state2b = new(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (LogToDisplay)
                {
                    Console.WriteLine("state with ZF = false:\n" + state2b);
                }

                AsmTestTools.IsTrue(state2b.IsConsistent);
                AsmTestTools.AreEqual(Rn.RAX, 20, state2b);
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_8()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        1               " + Environment.NewLine +
                "           jc      label1                      " + Environment.NewLine +
                "           mov     rbx,        2               " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rcx,        3               ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow.ToString());
            }

            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);

            Microsoft.Z3.BoolExpr branch_Condition_jz = dFlow.Get_Branch_Condition(0);
            Microsoft.Z3.BoolExpr branch_Condition_jc = dFlow.Get_Branch_Condition(2);

            if (true)
            {
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
            if (true)
            {
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
            if (true)
            {
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    AsmTestTools.AreEqual(Rn.RBX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
                    AsmTestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_9()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        1               " + Environment.NewLine +
                "           jc      label1                      " + Environment.NewLine +
                "           mov     rax,        2               " + Environment.NewLine +
                "           jp      label1                      " + Environment.NewLine +
                "           mov     rax,        3               " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow.ToString());
            }

            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);

            Microsoft.Z3.BoolExpr branch_Condition_jz = dFlow.Get_Branch_Condition(0);
            Microsoft.Z3.BoolExpr branch_Condition_jc = dFlow.Get_Branch_Condition(2);
            Microsoft.Z3.BoolExpr branch_Condition_jp = dFlow.Get_Branch_Condition(4);

            if (true)
            {
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    AsmTestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_10()
        {
            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        1               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        2               " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow.ToString());
            }

            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);

            Microsoft.Z3.BoolExpr branch_Condition_jz1 = dFlow.Get_Branch_Condition(1);
            Microsoft.Z3.BoolExpr branch_Condition_jz2 = dFlow.Get_Branch_Condition(3);

            if (true)
            {
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, true));
                    state2.Add(new BranchInfo(branch_Condition_jz2, true));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000000", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, true));
                    state2.Add(new BranchInfo(branch_Condition_jz2, false));
                    AsmTestTools.AreEqual(Rn.RAX, "XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, false));
                    state2.Add(new BranchInfo(branch_Condition_jz2, true));
                    AsmTestTools.AreEqual(Rn.RAX, "XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX", state2);
                }
                if (true)
                {
                    State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, false));
                    state2.Add(new BranchInfo(branch_Condition_jz2, false));
                    AsmTestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
                }
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_11()
        {
            string programStr =
                "           cmp     al,         0               " + Environment.NewLine +
                "           jp      label1                      " + Environment.NewLine +
                "           mov     al,         1               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     al,         2               " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = this.CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow.ToString());
            }

            tools.StateConfig = sFlow.Create_StateConfig();
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.Create_EndState;
            Assert.IsNotNull(state);

            if (LogToDisplay)
            {
                Console.WriteLine("state:\n" + state);
            }

            AsmTestTools.IsTrue(state.IsConsistent);

            Microsoft.Z3.BoolExpr branch_Condition_jp = dFlow.Get_Branch_Condition(1);
            Microsoft.Z3.BoolExpr branch_Condition_jz = dFlow.Get_Branch_Condition(3);

            if (true)
            {
                if (true)
                {
                    using State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    AsmTestTools.AreEqual(Rn.AL, "00000000", state2);
                }
                if (true)
                {
                    using State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    AsmTestTools.AreEqual(Rn.AL, "????????", state2);
                }
                if (true)
                {
                    using State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    AsmTestTools.AreEqual(Rn.AL, "XXXXXXXX", state2);
                }
                if (true)
                {
                    using State state2 = new(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    AsmTestTools.AreEqual(Rn.AL, "00000010", state2);
                }
            }
        }

        [TestMethod]
        public void Test_Runner_Mem_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.Mem = true;

            string programStr =
                "           mov     rcx,        20              " + Environment.NewLine +
                "           mov     qword ptr[rax],  rcx        " + Environment.NewLine +
                "           mov     rdx,        qword ptr[rax]  ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.Create_EndState;

            AsmTestTools.AreEqual(Rn.RCX, 20, state);
            AsmTestTools.AreEqual(Rn.RDX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_2()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.Mem = true;
            tools.Quiet = true;

            string programStr =
                "           mov     rax, 0                      " + Environment.NewLine +
                "           mov     qword ptr[0], 10            " + Environment.NewLine +
                "           mov     qword ptr[10], 20           " + Environment.NewLine +
                "           mov     rbx, qword ptr [rax]        " + Environment.NewLine +
                "           mov     rcx, qword ptr [rbx]        ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.Create_EndState;

            AsmTestTools.AreEqual(Rn.RAX, 0, state);
            AsmTestTools.AreEqual(Rn.RBX, 10, state);
            AsmTestTools.AreEqual(Rn.RCX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_3()
        {
            Tools tools = this.CreateTools(); // test is slow (9min - 17min)
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.Mem = true;
            tools.Quiet = true;
            bool logToDisplay2 = false; // logToDisplay;

            string programStr =
                "           mov     qword ptr[0], 10            " + Environment.NewLine +
                "           mov     qword ptr[10], 20           " + Environment.NewLine +
                "           mov     rbx, qword ptr [rax]        " + Environment.NewLine +
                "           mov     rcx, qword ptr [rbx]        ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State? state0 = dFlow.Create_States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.Create_EndState;

            Microsoft.Z3.Expr rax = state0.Create(Rn.RAX).Translate(state.Ctx);
            state.Frozen = false;
            state.Add(new BranchInfo(state.Ctx.MkEq(rax, state.Ctx.MkBV(0, 64)), true));
            if (logToDisplay2)
            {
                Console.WriteLine("Forward:\n" + state);
            }

            AsmTestTools.AreEqual(Rn.RAX, 0, state);
            AsmTestTools.AreEqual(Rn.RBX, 10, state);
            AsmTestTools.AreEqual(Rn.RCX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_Merge_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.Mem = true;
            tools.Quiet = true;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     byte ptr[rax],     10      " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     byte ptr[rax],     20      " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     bl, byte ptr[rax]         ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.Create_EndState;

            State state3 = new(state);
            State state4 = new(state);
            Microsoft.Z3.BoolExpr branch_Condition = dFlow.Get_Branch_Condition(0);
            state3.Add(new BranchInfo(branch_Condition, true));
            if (LogToDisplay)
            {
                Console.WriteLine("state3:\n" + state3);
            }

            AsmTestTools.AreEqual(Rn.BL, 10, state3);

            state4.Add(new BranchInfo(branch_Condition, false));
            if (LogToDisplay)
            {
                Console.WriteLine("state4:\n" + state4);
            }

            AsmTestTools.AreEqual(Rn.BL, 20, state4);
        }

        [TestMethod]
        public void Test_Runner_Loop_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;
            tools.Quiet = true;
            bool logToDisplay2 = false;

            string programStr =
                "           mov        rbx,     0               " + Environment.NewLine +
                "           mov        rax,     0x3             " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           inc        rbx                      " + Environment.NewLine +
                "           dec        rax                      " + Environment.NewLine +
                "           jnz        label1                   ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (logToDisplay2)
            {
                Console.WriteLine(sFlow);
            }

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            if (false)
            {
                State state = dFlow.Create_EndState;
                if (logToDisplay2)
                {
                    Console.WriteLine(state);
                }

                AsmTestTools.AreEqual(Rn.RAX, 0, state);
                AsmTestTools.AreEqual(Rn.RBX, 3, state);
            }
            else
            {
                Assert.Inconclusive("TODO");
            }
        }

        [TestMethod]
        public void Test_Runner_Loop_2()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.Quiet = false;

            bool logToDisplay2 = true;

            string programStr =
                "           mov        rax,     0x2             " + Environment.NewLine +
                "label1:    dec        rax                      " + Environment.NewLine +
                "           jnz        label1                   ";

            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (logToDisplay2)
            {
                Console.WriteLine(sFlow);
            }

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            if (false)
            { // backward
                State state = dFlow.Create_EndState;
                if (logToDisplay2)
                {
                    Console.WriteLine(state);
                }

                AsmTestTools.AreEqual(Rn.RAX, 0, state);
            }
            else
            {
                Assert.Inconclusive("TODO");
            }
        }

        // ── Operand-type validation (Opcode2Base) end-to-end ─────────────────────────────────────────────
        // Guards the REAL production path (parse line -> InstantiateOpcode -> Opcode2Base operand check),
        // not just the Ot2 encoding. Opcode2Type1 (ALU ops) allows {reg_reg, reg_mem, reg_imm, mem_reg,
        // mem_imm} but NOT mem_mem. Under the old packed-nibble Ot2 encoding, OR-ing the allowed set produced
        // a bit-union that wrongly "contained" mem_mem (a phantom HasFlag member), so `add mem,mem` was
        // silently accepted. See OperandType.cs and Test_AsmSourceTools_Ot2_SingleBitFlags_NoPhantomMembers.
        [TestMethod]
        public void Test_Runner_OperandValidation_RejectsMemMem_ForAluOp()
        {
            Tools tools = this.CreateTools();
            (string prevKey, string nextKey, string nextKeyBranch) keys = ("!0", "!1", "!1B");

            (_, _, Mnemonic mn1, string[] args1, _) =
                AsmSourceTools.ParseLine("add ptr qword [rax], ptr qword [rbx]", -1, -1, AssemblerEnum.UNKNOWN);
            using OpcodeBase? memMem = Runner.InstantiateOpcode(mn1, args1, keys, tools);
            Assert.IsNotNull(memMem);
            Assert.IsTrue(memMem!.IsHalted, "add mem,mem is an illegal operand form and must be rejected");
            StringAssert.Contains(memMem.SyntaxError, "Invalid combination",
                "rejection must come from operand-type validation, not a parse/size error");

            (_, _, Mnemonic mn2, string[] args2, _) =
                AsmSourceTools.ParseLine("add rax, rbx", -1, -1, AssemblerEnum.UNKNOWN);
            using OpcodeBase? regReg = Runner.InstantiateOpcode(mn2, args2, keys, tools);
            Assert.IsNotNull(regReg);
            Assert.IsFalse(regReg!.IsHalted, "add reg,reg is a valid operand form");
        }
    }
}
