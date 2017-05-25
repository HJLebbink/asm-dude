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
        private (State Forward, State Backward) Equal_Forward_Backward(string programStr, bool logToDispay2, Tools tools)
        {
            StaticFlow staticFlow = new StaticFlow(programStr, tools);

            if (logToDispay2) Console.WriteLine(staticFlow.ToString());

            DynamicFlow tree0 = Runner.Construct_DynamicFlow_Forward(staticFlow, 0, 100, tools);
            //DynamicFlow<IExecutionNode> tree0 = Runner.Construct_DynamicFlow_Forward(staticFlow, 0, 100, tools);
            DynamicFlow tree1 = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 100, tools);

            //Console.WriteLine("Forward:" + tree0.ToString(staticFlow));
            //Console.WriteLine("Backward:" + tree1.ToString(staticFlow));

            State state0 = tree0.EndState;
            State state1 = tree1.EndState;

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
            return (Forward: state0, Backward: state1);
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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;
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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;
            TestTools.AreEqual(Rn.RAX, 0, state);
            TestTools.AreEqual(Rn.RBX, 0, state);
            // flags are unaltered
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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;

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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;

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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;

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
        public void Test_Runner_Jmp_Forward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "label1:                                        ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            if (logToDisplay) Console.WriteLine(staticFlow);

            State state2 = Runner.Construct_DynamicFlow_Forward(staticFlow, 0, 10, tools).EndState;
            if (logToDisplay) Console.WriteLine(state2);

            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);
        }

        [TestMethod]
        public void Test_Runner_Jmp_Forward_5()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.ShowUndefConstraints = false;

            bool logToDisplay2 = true;
            tools.Quiet = false;// !logToDisplay2;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(staticFlow);

            State state2 = Runner.Construct_DynamicFlow_Forward(staticFlow, 0, 10, tools).EndState;
            if (logToDisplay2) Console.WriteLine("state2:\n" + state2);
            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);

            {
                State state2a = new State(state2);
                state2a.Add(new BranchInfo(state2a.Get(Flags.ZF), true, 0));
                if (logToDisplay2) Console.WriteLine("state2 with ZF==0:\n" + state2a);
                TestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
                //TestTools.AreEqual(Rn.RAX, 20, state2a);
                //TestTools.AreEqual(Rn.RAX, 20, state2a);
            }
            {
                State state2b = new State(state2);
                state2b.Add(new BranchInfo(state2b.Get(Flags.ZF), false, 0));
                if (logToDisplay2) Console.WriteLine("state2 with ZF==1:\n" + state2b);
                TestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
                //TestTools.AreEqual(Rn.RAX, 10, state2b);
                //TestTools.AreEqual(Rn.RAX, 10, state2b);
            }
        }


        [TestMethod]
        public void Test_Runner_Jmp_Backward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;

            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        0               " + Environment.NewLine +
                "label1:                                        ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            if (logToDisplay) Console.WriteLine(staticFlow);
            
            var dynamicFlow = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 10, tools);
            var state = dynamicFlow.EndState;
            if (logToDisplay) Console.WriteLine("DynamicFlow:\n" + dynamicFlow.ToString(staticFlow));
            if (logToDisplay) Console.WriteLine("Backward:\n" + state);

            TestTools.AreEqual(Tv.ONE, state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 0, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;

            string programStr =
                "           cmp     rax,        0               " + Environment.NewLine +
                "           je      label1                      " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "label2:                                        ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            State state2 = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 10, tools).EndState;
            if (logToDisplay) Console.WriteLine("Backward:\n" + state2);

            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);
            TestTools.AreEqual(Rn.RBX, 10, state2);
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_3()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "label2:                                        ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            State state = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 10, tools).EndState;
            if (logToDisplay) Console.WriteLine("Backward:\n" + state);

            TestTools.AreEqual(Tv.ONE, state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 10, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_4()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     rax,        20              ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            State state = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 10, tools).EndState;
            if (logToDisplay) Console.WriteLine("Backward:\n" + state);

            TestTools.AreEqual(Tv.ONE, state.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_5()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.ShowUndefConstraints = false;

            bool logToDisplay2 = true;
            tools.Quiet = true;// !logToDisplay2;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        ";
 
            StaticFlow sFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(sFlow);

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, sFlow.LastLineNumber, 10, tools);
            State state0 = dFlow.States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay2) Console.WriteLine("state:\n" + state);
            TestTools.AreEqual(Tv.ONE, state.IsConsistent);
            
            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(state0.Get(Flags.ZF), true, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 20, state2a);
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(state0.Get(Flags.ZF), false, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 10, state2b);
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_6()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.ShowUndefConstraints = false;

            bool logToDisplay2 = true;
            tools.Quiet = true;// !logToDisplay2;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "label1:                                        ";

            StaticFlow sFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(sFlow);

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, sFlow.LastLineNumber, 10, tools);
            State state0 = dFlow.States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            //DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            if (logToDisplay2) Console.WriteLine("state:\n" + state);
            TestTools.AreEqual(Tv.ONE, state.IsConsistent);

            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(state0.Get(Flags.ZF), true, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
                //TestTools.AreEqual(Rn.RAX, 20, state2a);
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(state0.Get(Flags.ZF), false, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
                //TestTools.AreEqual(Rn.RAX, 10, state2b);
            }
        }

        [TestMethod]
        public void Test_Runner_Jmp_Backward_7()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;
            tools.ShowUndefConstraints = false;

            bool logToDisplay2 = true;
            tools.Quiet = true;// !logToDisplay2;

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

            StaticFlow sFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(sFlow);

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, sFlow.LastLineNumber, 10, tools);
            DotVisualizer.SaveToDot(sFlow, dFlow, "test1.dot");

            State state0 = dFlow.States_Before(0, 0);
            Assert.IsNotNull(state0);
            State state = dFlow.EndState;
            Assert.IsNotNull(state);

            if (logToDisplay2) Console.WriteLine("state:\n" + state);
            TestTools.AreEqual(Tv.ONE, state.IsConsistent);

            {
                State state2a = new State(state);
                state2a.Add(new BranchInfo(state0.Get(Flags.ZF), true, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = true:\n" + state2a);
                TestTools.AreEqual(Tv.ONE, state2a.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 20, state2a);
            }
            {
                State state2b = new State(state);
                state2b.Add(new BranchInfo(state0.Get(Flags.ZF), false, 0));
                if (logToDisplay2) Console.WriteLine("state with ZF = false:\n" + state2b);
                TestTools.AreEqual(Tv.ONE, state2b.IsConsistent);
                TestTools.AreEqual(Rn.RAX, 10, state2b);
            }
        }

        [TestMethod]
        public void Test_Runner_Mem1()
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
            State state = Equal_Forward_Backward(programStr, logToDisplay, tools).Backward;
            TestTools.AreEqual(Rn.RCX, 20, state);
            TestTools.AreEqual(Rn.RDX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.mem = true;
            tools.Quiet = true;
            bool logToDisplay2 = false;

            string programStr =
                "           mov     rax, 0                      " + Environment.NewLine +
                "           mov     qword ptr[0], 10            " + Environment.NewLine +
                "           mov     qword ptr[10], 20           " + Environment.NewLine +
                "           mov     rbx, qword ptr [rax]        " + Environment.NewLine +
                "           mov     rcx, qword ptr [rbx]        ";

            State state = Equal_Forward_Backward(programStr, logToDisplay2, tools).Backward;
            TestTools.AreEqual(Rn.RAX, 0, state);
            TestTools.AreEqual(Rn.RBX, 10, state);
            TestTools.AreEqual(Rn.RCX, 20, state);
        }

        [TestMethod]
        public void Test_Runner_Mem3()
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

            var tup = Equal_Forward_Backward(programStr, logToDisplay2, tools);
            {
                State state1a = new State(tup.Forward);
                state1a.Add(new BranchInfo(state1a.Ctx.MkEq(state1a.Get(Rn.RAX), state1a.Ctx.MkBV(0, 64)), true, 0));
                if (logToDisplay2) Console.WriteLine("Forward:\n" + state1a);
                TestTools.AreEqual(Rn.RAX, 0, state1a);
                TestTools.AreEqual(Rn.RBX, 10, state1a);
                TestTools.AreEqual(Rn.RCX, 20, state1a);
            }
            {
                State state3a = new State(tup.Backward);
                state3a.Add(new BranchInfo(state3a.Ctx.MkEq(state3a.Get(Rn.RAX), state3a.Ctx.MkBV(0, 64)), true, 0));
                if (logToDisplay2) Console.WriteLine("Backward:\n" + state3a);
                TestTools.AreEqual(Rn.RAX, 0, state3a);
                TestTools.AreEqual(Rn.RBX, 10, state3a);
                TestTools.AreEqual(Rn.RCX, 20, state3a);
            }
        }

        [TestMethod]
        public void Test_Runner_Merge_Mem_Backward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.mem = true;
            tools.Quiet = false;

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     byte ptr[rax],     10      " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     byte ptr[rax],     20      " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     bl, byte ptr[rax]         ";

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            State state2 = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.LastLineNumber, 10, tools).EndState;

            //if (logToDisplay) Console.WriteLine("state2:\n" + state2);

            State state3 = new State(state2);
            State state4 = new State(state2);

            state3.Add(new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.Z, state3.TailKey, state3.Ctx), true, 0));
            if (logToDisplay) Console.WriteLine("state3:\n" + state3);
            TestTools.AreEqual(Rn.BL, 20, state3);

            state4.Add(new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.Z, state4.TailKey, state4.Ctx), false, 0));
            if (logToDisplay) Console.WriteLine("state4:\n" + state4);
            TestTools.AreEqual(Rn.BL, 10, state4);
        }

        [TestMethod]
        public void Test_Runner_Merge_Reg_Forward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.CF = true;
            tools.StateConfig.ZF = true;

            State state0 = CreateState(tools);
            State state1 = new State(state0);
            State state2 = new State(state0);
            {
                StateUpdate updateState1 = new StateUpdate(state1.TailKey, Tools.CreateKey(tools.Rand), tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state1.HeadKey, state1.Ctx), true, 0)
                };
                updateState1.Set(Rn.RAX, 10);
                updateState1.Set(Flags.CF, Tv.ONE);
                updateState1.BranchInfo = new BranchInfo(state1.Get(Flags.ZF), true, 0);
                state1.Update_Forward(updateState1);
            }
            {
                StateUpdate updateState2 = new StateUpdate(state2.TailKey, Tools.CreateKey(tools.Rand), tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state2.HeadKey, state2.Ctx), true, 0)
                };
                updateState2.Set(Rn.RAX, 20);
                updateState2.Set(Flags.CF, Tv.ZERO);
                updateState2.BranchInfo = new BranchInfo(state2.Get(Flags.ZF), false, 0);
                state2.Update_Forward(updateState2);
            }

            if (logToDisplay) Console.WriteLine("=========================================\nstate1: we know:\n" + state1);
            if (logToDisplay) Console.WriteLine("=========================================\nstate2: we know:\n" + state2);


            State mergedState3 = new State(state1, state2, true);
            State mergedState4 = new State(mergedState3);

            mergedState3.Add(new BranchInfo(mergedState3.Get(Flags.ZF), true, 0));
            if (logToDisplay) Console.WriteLine("=========================================\nmergedState3: we know:\n" + mergedState3);
            TestTools.AreEqual(Flags.CF, true, mergedState3);
            TestTools.AreEqual(Rn.RAX, 10, mergedState3);

            mergedState4.Add(new BranchInfo(mergedState4.Get(Flags.ZF), false, 0));
            if (logToDisplay) Console.WriteLine("=========================================\nmergedState4: we know:\n" + mergedState4);
            TestTools.AreEqual(Flags.CF, false, mergedState4);
            TestTools.AreEqual(Rn.RAX, 20, mergedState4);

            if (logToDisplay) Console.WriteLine(state1);
            if (logToDisplay) Console.WriteLine(state2);
            if (logToDisplay) Console.WriteLine(mergedState3);
        }

        [TestMethod]
        public void Test_Runner_Merge_Mem_Forward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;
            tools.StateConfig.mem = true;
            tools.Quiet = false;

            int nBytes = 1;

            State state0 = CreateState(tools);
            State state1 = new State(state0);
            State state2 = new State(state0);

            BoolExpr branchCondition = state0.Get(Flags.ZF);
            {
                StateUpdate updateState1 = new StateUpdate(state1.HeadKey, Tools.CreateKey(state1.Tools.Rand), state1.Tools);
                updateState1.SetMem(state1.Get(Rn.RAX), 1, nBytes);
                updateState1.BranchInfo = new BranchInfo(branchCondition, false, 0);
                state1.Update_Forward(updateState1);
            }
            {
                StateUpdate updateState2 = new StateUpdate(state2.HeadKey, Tools.CreateKey(state2.Tools.Rand), state2.Tools);
                updateState2.SetMem(state2.Get(Rn.RAX), 2, nBytes);
                updateState2.BranchInfo = new BranchInfo(branchCondition, true, 0);
                state2.Update_Forward(updateState2);
            }
            State mergedState3 = new State(state1, state2, true);
            State mergedState4 = new State(mergedState3);

            //if (logToDisplay) Console.WriteLine("state1=\n" + state1);
            //if (logToDisplay) Console.WriteLine("state2=\n" + state2);
            //if (logToDisplay) Console.WriteLine("mergedState3=\n" + mergedState3);

            mergedState3.Add(new BranchInfo(branchCondition, true, 0));
            if (logToDisplay) Console.WriteLine("mergedState3 Plus branchCondition=\n" + mergedState3);

            TestTools.AreEqual(Tv.ONE, mergedState3.IsConsistent);
            TestTools.AreEqual(1, mergedState3.GetTv5ArrayMem(mergedState3.Get(Rn.RAX), nBytes));

            mergedState4.Add(new BranchInfo(branchCondition, false, 0));
            TestTools.AreEqual(Tv.ONE, mergedState4.IsConsistent);
            TestTools.AreEqual(2, mergedState4.GetTv5ArrayMem(mergedState4.Get(Rn.RAX), nBytes));

        }

        [TestMethod]
        public void Test_Runner_Loop_Forward_1()
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

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(staticFlow);

            if (true)
            { 
                State state = Runner.Construct_DynamicFlow_Forward(staticFlow, 0, 20, tools).EndState;
                if (logToDisplay2) Console.WriteLine(state);
                TestTools.AreEqual(Rn.RAX, 0, state);
                TestTools.AreEqual(Rn.RBX, 3, state);
            }
        }

        [TestMethod]
        public void Test_Runner_Loop_Backward_1()
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

            StaticFlow staticFlow = new StaticFlow(programStr, tools);
            if (logToDisplay2) Console.WriteLine(staticFlow);

            if (true)
            {   // backward
                State state = Runner.Construct_DynamicFlow_Backward(staticFlow, staticFlow.NLines - 1, 10, tools).EndState;
                if (logToDisplay2) Console.WriteLine(state);
                TestTools.AreEqual(Rn.RAX, 0, state);
            }
            else Assert.Inconclusive("TODO");
        }
    }
}
