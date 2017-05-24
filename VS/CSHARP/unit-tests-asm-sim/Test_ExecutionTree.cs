using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim;
using System.Collections.Generic;
using AsmTools;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_ExecutionGraph
    {
        const bool logToDisplay = true;// TestTools.LOG_TO_DISPLAY;

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

        [TestMethod]
        public void Test_ExecutionGraph_Forward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = true;
            string programStr =
                "           mov     rax,        0       ; line 0        " + Environment.NewLine +
                "           mov     rbx,        10      ; line 1        " + Environment.NewLine +
                "           mov     rbx,        rax     ; line 2        ";
            StaticFlow flow = new StaticFlow(programStr, tools);
            if (logToDisplay) Console.WriteLine(flow);

            if (true) {
                DynamicFlow tree_Forward = Runner.Construct_ExecutionGraph_Forward(flow, 0, 100, tools);
                if (logToDisplay) Console.WriteLine(tree_Forward.ToString(flow));

                {
                    int lineNumber = 0;
                    IList<State> states_Before = new List<State>(tree_Forward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Forward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_After);
                }
                {
                    int lineNumber = 1;
                    IList<State> states_Before = new List<State>(tree_Forward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Forward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, 0, state_Before);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, 10, state_After);
                }
                {
                    int lineNumber = 2;
                    IList<State> states_Before = new List<State>(tree_Forward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Forward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, 0, state_Before);
                    TestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, 0, state_After);
                }
            }
        }

        [TestMethod]
        public void Test_ExecutionGraph_Backward_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = true;
            string programStr =
                "           mov     rax,        0       ; line 0        " + Environment.NewLine +
                "           mov     rbx,        10      ; line 1        " + Environment.NewLine +
                "           mov     rbx,        rax     ; line 2        ";
            StaticFlow flow = new StaticFlow(programStr, tools);
            if (logToDisplay) Console.WriteLine(flow);

            if (true)
            {
                DynamicFlow tree_Backward = Runner.Construct_ExecutionGraph_Backward(flow, flow.LastLineNumber, 100, tools);
                if (logToDisplay) Console.WriteLine(tree_Backward.ToString(flow));
                {
                    int lineNumber = 0;
                    IList<State> states_Before = new List<State>(tree_Backward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Backward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_After);
                }
                {
                    int lineNumber = 1;
                    IList<State> states_Before = new List<State>(tree_Backward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Backward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, 0, state_Before);
                    TestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, 10, state_After);
                }
                {
                    int lineNumber = 2;
                    IList<State> states_Before = new List<State>(tree_Backward.States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(tree_Backward.States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay) Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    TestTools.AreEqual(Rn.RAX, 0, state_Before);
                    TestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (logToDisplay) Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + flow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    TestTools.AreEqual(Rn.RAX, 0, state_After);
                    TestTools.AreEqual(Rn.RBX, 0, state_After);
                }
            }
        }
    }
}
