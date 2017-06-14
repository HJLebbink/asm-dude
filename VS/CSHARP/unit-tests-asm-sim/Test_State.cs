using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim.Mnemonics;
using AsmTools;
using AsmSim;
using Microsoft.Z3;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_State
    {
        const bool logToDisplay = TestTools.LOG_TO_DISPLAY;

        private Tools CreateTools(int timeOut = TestTools.DEFAULT_TIMEOUT)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "unsat-core", "false" },    // enable generation of unsat cores
                { "model", "false" },          // enable model generation
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

        [TestMethod]
        public void Test_State_Redundant_Mem_1()
        {
            Tools tools = CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.mem = true;

            string line1 = "mov ptr qword [rax], 10";
            string line2 = "mov ptr qword [rax], 10";

            State state = CreateState(tools);
            state = Runner.SimpleStep_Forward(line1, state);
            if (logToDisplay) Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
            var key1 = state.HeadKey;

            state = Runner.SimpleStep_Forward(line2, state);
            if (logToDisplay) Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
            var key2 = state.HeadKey;

            TestTools.IsTrue(state.Is_Redundant_Mem(key1, key2));
        }

        [TestMethod]
        public void Test_State_Redundant_Mem_2()
        {
            Tools tools = CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.mem = true;

            string line1 = "mov ptr qword [rax], rbx";
            string line2 = "mov ptr qword [rax], rbx";

            State state = CreateState(tools);
            state = Runner.SimpleStep_Forward(line1, state);
            if (logToDisplay) Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
            var key1 = state.HeadKey;

            state = Runner.SimpleStep_Forward(line2, state);
            if (logToDisplay) Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
            var key2 = state.HeadKey;

            TestTools.IsTrue(state.Is_Redundant_Mem(key1, key2));
        }

        [TestMethod]
        public void Test_State_Redundant_Mem_3()
        {
            Tools tools = CreateTools(100000);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.mem = true;

            string line0 = "mov rbx, rcx";
            string line1 = "mov ptr byte [rax], cl";
            string line2 = "mov ptr byte [rax], bl";

            State state = CreateState(tools);
            state = Runner.SimpleStep_Forward(line0, state);
            state = Runner.SimpleStep_Forward(line1, state);
            if (logToDisplay) Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
            var key1 = state.HeadKey;

            state = Runner.SimpleStep_Forward(line2, state);
            if (logToDisplay) Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
            var key2 = state.HeadKey;

            TestTools.IsTrue(state.Is_Redundant_Mem(key1, key2));
        }
    }
}
