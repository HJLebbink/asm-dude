using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_ExecutionTree
    {

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
            return new State(tools, tailKey, headKey, 0);
        }

        [TestMethod]
        public void Test_ExecutionTree_Leafs_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;

            string programStr =
                "           mov     rax,        0               " + Environment.NewLine +
                "           mov     rbx,        10              " + Environment.NewLine +
                "           mov     rbx,        rax             ";
            CFlow flow = new CFlow(programStr);

            ExecutionTree2 tree0 = Runner.Construct_ExecutionTree2_Forward(flow, 0, 100, tools);



        }
    }
}
