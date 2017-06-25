using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;
using System.Collections.Generic;

using AsmSim;
using AsmTools;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_Runner
    {
        const bool logToDisplay = TestTools.LOG_TO_DISPLAY;

        #region Private Methods

        private Tools CreateTools(int timeOut = TestTools.DEFAULT_TIMEOUT)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "unsat-core", "false" },    // enable generation of unsat cores
                { "model", "false" },         // enable model generation
                { "proof", "false" },         // enable proof generation
                { "timeout", timeOut.ToString() }
            };
            return new Tools(settings);
        }

        private State CreateState(Tools tools)
        {
            string tailKey = "!0";// Tools.CreateKey(tools.Rand);
            string headKey = tailKey;
            return new State(tools, tailKey, headKey);
        }

        /// <summary>Returns Forward, Backward State</summary>
        private State Equal_Forward_Backward(string programStr, bool logToDispay2, Tools tools)
        {
            StaticFlow sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDispay2) Console.WriteLine(sFlow.ToString());

            DynamicFlow dFlow0 = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            DynamicFlow dFlow1 = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            //Console.WriteLine("Forward:" + tree0.ToString(dFlow0));
            //Console.WriteLine("Backward:" + tree1.ToString(dFlow1));

            State state0 = dFlow0.EndState;
            State state1 = dFlow1.EndState;

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
            TestTools.AreEqual(state0, state1);
            return state0;
        }
        #endregion Private Methods

        [TestMethod]
        public void Test_Runner_Several_Mnemonics()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = true;

            Mnemonic[] a = new Mnemonic[] { Mnemonic.AND, Mnemonic.ADD, Mnemonic.OR, Mnemonic.SUB, Mnemonic.XOR, Mnemonic.ADC, Mnemonic.SBB };

            ulong value_rax = 10;
            ulong value_rbx = 20;
            foreach (Mnemonic mnemonic in a)
            {
                Console.WriteLine("Mnemonic: " + mnemonic);

                string line1 = "mov rax, " + value_rax.ToString();
                string line2 = "mov rbx, " + value_rbx.ToString();
                string line3 = mnemonic + " rax, rbx";

                State state_forward = CreateState(tools);
                state_forward = Runner.SimpleStep_Forward(line1, state_forward);
                state_forward = Runner.SimpleStep_Forward(line2, state_forward);
                state_forward = Runner.SimpleStep_Forward(line3, state_forward);

                State state_backward = CreateState(tools);
                state_backward = Runner.SimpleStep_Backward(line3, state_backward);
                state_backward = Runner.SimpleStep_Backward(line2, state_backward);
                state_backward = Runner.SimpleStep_Backward(line1, state_backward);

                TestTools.AreEqual(state_backward, state_forward);
            }
        }

        [TestMethod]
        public void Test_Runner_CF_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.CF = true;

            string programStr =
                "           clc       ; clear CF                " + Environment.NewLine +
                "           stc       ; set CF                  " + Environment.NewLine +
                "           cmc       ; complement CF           ";
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools);
            TestTools.AreEqual(Flags.CF, Tv.ZERO, state);
        }

        [TestMethod]
        public void Test_Runner_Mov_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        0               " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "           mov     rbx,        rax             ";
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools);
            TestTools.AreEqual(Rn.RAX, 0, state);
            TestTools.AreEqual(Rn.RBX, 0, state);
        }

        [TestMethod]
        public void Test_Runner_Add_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        10              " + Environment.NewLine +
                "           mov     rbx,        20              " + Environment.NewLine +
                "           add     rax,        rbx             ";
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools);

            ulong result = 10 + 20;
            uint nBits = 64;
            TestTools.AreEqual(Rn.RAX, result, state);
            TestTools.AreEqual(Rn.RBX, 20, state);
            TestTools.AreEqual(Flags.CF, TestTools.ToTv5(TestTools.Calc_CF_Add(nBits, 10, 20)), state);
            TestTools.AreEqual(Flags.OF, TestTools.ToTv5(TestTools.Calc_OF_Add(nBits, 10, 20)), state);
            TestTools.AreEqual(Flags.AF, TestTools.ToTv5(TestTools.Calc_AF_Add(10, 20)), state);
            TestTools.AreEqual(Flags.PF, TestTools.ToTv5(TestTools.Calc_PF(result)), state);
            TestTools.AreEqual(Flags.ZF, TestTools.ToTv5(TestTools.Calc_ZF(result)), state);
            TestTools.AreEqual(Flags.SF, TestTools.ToTv5(TestTools.Calc_SF(nBits, result)), state);
        }

        [TestMethod]
        public void Test_Runner_Add_2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        10              " + Environment.NewLine +
                "" + Environment.NewLine +
                "           mov     rbx,        20              " + Environment.NewLine +
                "" + Environment.NewLine +
                "           add     rax,        rbx             ";
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools);

            ulong result = 10 + 20;
            uint nBits = 64;
            TestTools.AreEqual(Rn.RAX, result, state);
            TestTools.AreEqual(Rn.RBX, 20, state);
            TestTools.AreEqual(Flags.CF, TestTools.ToTv5(TestTools.Calc_CF_Add(nBits, 10, 20)), state);
            TestTools.AreEqual(Flags.OF, TestTools.ToTv5(TestTools.Calc_OF_Add(nBits, 10, 20)), state);
            TestTools.AreEqual(Flags.AF, TestTools.ToTv5(TestTools.Calc_AF_Add(10, 20)), state);
            TestTools.AreEqual(Flags.PF, TestTools.ToTv5(TestTools.Calc_PF(result)), state);
            TestTools.AreEqual(Flags.ZF, TestTools.ToTv5(TestTools.Calc_ZF(result)), state);
            TestTools.AreEqual(Flags.SF, TestTools.ToTv5(TestTools.Calc_SF(nBits, result)), state);
        }

        [TestMethod]
        public void Test_Runner_Xor_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.Set_All_Flags_On();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        20              " + Environment.NewLine +
                "           mov     rbx,        rax             " + Environment.NewLine +
                "           xor     rbx,        rax             ";
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools);

            ulong result = 20 ^ 20;
            uint nBits = 64;
            TestTools.AreEqual(Rn.RBX, result, state);
            TestTools.AreEqual(Flags.CF, Tv.ZERO, state);
            TestTools.AreEqual(Flags.OF, Tv.ZERO, state);
            TestTools.AreEqual(Flags.AF, Tv.UNDEFINED, state);
            TestTools.AreEqual(Flags.PF, TestTools.ToTv5(TestTools.Calc_PF(result)), state);
            TestTools.AreEqual(Flags.ZF, TestTools.ToTv5(TestTools.Calc_ZF(result)), state);
            TestTools.AreEqual(Flags.SF, TestTools.ToTv5(TestTools.Calc_SF(nBits, result)), state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_1()
        {
            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        0               " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = CreateTools();
            StaticFlow sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            var state = dFlow.EndState;
            //if (logToDisplay) Console.WriteLine("DynamicFlow:\n" + dFlow.ToString(staticFlow));
            if (logToDisplay) Console.WriteLine(state);

            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 0, state);
        }

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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            if (logToDisplay) Console.WriteLine(state);

            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RBX, 10, state);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State state = dFlow.EndState;
            if (logToDisplay) Console.WriteLine(state);

            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 10, state);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            //if (logToDisplay) Console.WriteLine(dFlow.ToString(sFlow));

            State state = dFlow.EndState;
            //if (logToDisplay) Console.WriteLine(state);

            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 20, state);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            bool logToDisplay2 = true;
            tools.Quiet = true;// !logToDisplay2;

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay2) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state);

            var branch_Condition = dFlow.Get_Branch_Condition(0);
            if (logToDisplay2) Console.WriteLine("Branch Condition:" + branch_Condition);

            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (logToDisplay2) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.IsTrue(state2a.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 10, state2a); // TODO why is 10 / 20 reversed?
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (logToDisplay2) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.IsTrue(state2b.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 20, state2b);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State state0 = dFlow.Create_States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 20, state);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            State state0 = dFlow.Create_States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 10, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_6()
        {
            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "label1:                                        ";

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            //DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);
            var branch_Condition = dFlow.Get_Branch_Condition(0);

            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (logToDisplay) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (logToDisplay) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            tools.StateConfig = sFlow.Create_StateConfig();
            //var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            //DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state);

            var branch_Condition = dFlow.Get_Branch_Condition(0);
            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(branch_Condition, true));
                if (logToDisplay) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.IsTrue(state2a.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 10, state2a);
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(branch_Condition, false));
                if (logToDisplay) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.IsTrue(state2b.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 20, state2b);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay) Console.WriteLine(sFlow.ToString());
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);

            var branch_Condition_jz = dFlow.Get_Branch_Condition(0);
            var branch_Condition_jc = dFlow.Get_Branch_Condition(2);

            if (true)
            {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
            if (true)
            {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
            }
            if (true)
            {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    TestTools.AreEqual(Rn.RBX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                    TestTools.AreEqual(Rn.RBX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
                    TestTools.AreEqual(Rn.RCX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay) Console.WriteLine(sFlow.ToString());
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);

            var branch_Condition_jz = dFlow.Get_Branch_Condition(0);
            var branch_Condition_jc = dFlow.Get_Branch_Condition(2);
            var branch_Condition_jp = dFlow.Get_Branch_Condition(4);

            if (true) {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    TestTools.AreEqual(Rn.RAX, "????????_????????_????????_????????_????????_????????_????????_????????", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, true));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    state2.Add(new BranchInfo(branch_Condition_jc, false));
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011", state2);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay) Console.WriteLine(sFlow.ToString());
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);

            var branch_Condition_jz1 = dFlow.Get_Branch_Condition(1);
            var branch_Condition_jz2 = dFlow.Get_Branch_Condition(3);

            if (true)
            {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, true));
                    state2.Add(new BranchInfo(branch_Condition_jz2, true));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000000", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, true));
                    state2.Add(new BranchInfo(branch_Condition_jz2, false));
                    TestTools.AreEqual(Rn.RAX, "XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, false));
                    state2.Add(new BranchInfo(branch_Condition_jz2, true));
                    TestTools.AreEqual(Rn.RAX, "XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX_XXXXXXXX", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jz1, false));
                    state2.Add(new BranchInfo(branch_Condition_jz2, false));
                    TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010", state2);
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

            Tools tools = CreateTools();
            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay) Console.WriteLine(sFlow.ToString());
            tools.StateConfig = sFlow.Create_StateConfig();
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay) Console.WriteLine("state:\n" + state);
            TestTools.IsTrue(state.IsConsistent);

            var branch_Condition_jp = dFlow.Get_Branch_Condition(1);
            var branch_Condition_jz = dFlow.Get_Branch_Condition(3);

            if (true)
            {
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    TestTools.AreEqual(Rn.AL, "00000000", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, true));
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    TestTools.AreEqual(Rn.AL, "????????", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    state2.Add(new BranchInfo(branch_Condition_jz, true));
                    TestTools.AreEqual(Rn.AL, "XXXXXXXX", state2);
                }
                if (true)
                {
                    State state2 = new State(state);
                    state2.Add(new BranchInfo(branch_Condition_jp, false));
                    state2.Add(new BranchInfo(branch_Condition_jz, false));
                    TestTools.AreEqual(Rn.AL, "00000010", state2);
                }
            }
        }

        [TestMethod]
        public void Test_Runner_Mem_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.mem = true;

            string programStr =
                "           mov     rcx,        20              " + Environment.NewLine +
                "           mov     qword ptr[rax],  rcx        " + Environment.NewLine +
                "           mov     rdx,        qword ptr[rax]  ";

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.EndState;

            TestTools.AreEqual(Rn.RCX, 20, state);
            TestTools.AreEqual(Rn.RDX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.mem = true;
            tools.Quiet = true;

            string programStr =
                "           mov     rax, 0                      " + Environment.NewLine +
                "           mov     qword ptr[0], 10            " + Environment.NewLine +
                "           mov     qword ptr[10], 20           " + Environment.NewLine +
                "           mov     rbx, qword ptr [rax]        " + Environment.NewLine +
                "           mov     rcx, qword ptr [rbx]        ";

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.EndState;

            TestTools.AreEqual(Rn.RAX, 0, state);
            TestTools.AreEqual(Rn.RBX, 10, state);
            TestTools.AreEqual(Rn.RCX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_3()
        {
            Tools tools = CreateTools(); // test is slow (9min - 17min)
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.mem = true;
            tools.Quiet = true;
            bool logToDisplay2 = false;// logToDisplay;

            string programStr =
                "           mov     qword ptr[0], 10            " + Environment.NewLine +
                "           mov     qword ptr[10], 20           " + Environment.NewLine +
                "           mov     rbx, qword ptr [rax]        " + Environment.NewLine +
                "           mov     rcx, qword ptr [rbx]        ";

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state0 = dFlow.Create_States_Before(0, 0);
            State state = dFlow.EndState;

            var rax = state0.Create(Rn.RAX).Translate(state.Ctx);

            state.Add(new BranchInfo(state.Ctx.MkEq(rax, state.Ctx.MkBV(0, 64)), true));
            if (logToDisplay2) Console.WriteLine("Forward:\n" + state);
            TestTools.AreEqual(Rn.RAX, 0, state);
            TestTools.AreEqual(Rn.RBX, 10, state);
            TestTools.AreEqual(Rn.RCX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem_Merge_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.mem = true;
            tools.Quiet = true;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     byte ptr[rax],     10      " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     byte ptr[rax],     20      " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     bl, byte ptr[rax]         ";

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            State state = dFlow.EndState;

            State state3 = new State(state);
            State state4 = new State(state);
            var branch_Condition = dFlow.Get_Branch_Condition(0);
            state3.Add(new BranchInfo(branch_Condition, true));
            if (logToDisplay) Console.WriteLine("state3:\n" + state3);
            TestTools.AreEqual(Rn.BL, 10, state3);

            state4.Add(new BranchInfo(branch_Condition, false));
            if (logToDisplay) Console.WriteLine("state4:\n" + state4);
            TestTools.AreEqual(Rn.BL, 20, state4);
        }

        [TestMethod]
        public void Test_Runner_Loop_1()
        {
            Tools tools = CreateTools();
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

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay2) Console.WriteLine(sFlow);
            var dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);

            if (false)
            { 
                State state = dFlow.EndState;
                if (logToDisplay2) Console.WriteLine(state);
                TestTools.AreEqual(Rn.RAX, 0, state);
                TestTools.AreEqual(Rn.RBX, 3, state);
            }
            else Assert.Inconclusive("TODO");
        }

        [TestMethod]
        public void Test_Runner_Loop_2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.Quiet = false;

            bool logToDisplay2 = true;

            string programStr =
                "           mov        rax,     0x2             " + Environment.NewLine +
                "label1:    dec        rax                      " + Environment.NewLine +
                "           jnz        label1                   ";

            var sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay2) Console.WriteLine(sFlow);
            var dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);

            if (false)
            {   // backward
                State state = dFlow.EndState;
                if (logToDisplay2) Console.WriteLine(state);
                TestTools.AreEqual(Rn.RAX, 0, state);
            }
            else Assert.Inconclusive("TODO");
        }
    }
}
