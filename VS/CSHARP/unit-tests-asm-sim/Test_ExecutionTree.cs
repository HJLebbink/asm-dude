using AsmSim;
using AsmTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_DynamicFlow
    {
        private const bool logToDisplay = true;// TestTools.LOG_TO_DISPLAY;

        private Tools CreateTools(int timeOut = AsmTestTools.DEFAULT_TIMEOUT)
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
        public void Test_DynamicFlow_Forward_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = true;
            tools.ShowUndefConstraints = false;

            string programStr =
                "           mov     rax,        0       ; line 0        " + Environment.NewLine +
                "           mov     rbx,        10      ; line 1        " + Environment.NewLine +
                "           mov     rbx,        rax     ; line 2        ";
            StaticFlow sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay)
            {
                Console.WriteLine(sFlow);
            }

            if (true)
            {
                DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
                if (logToDisplay)
                {
                    Console.WriteLine(dFlow.ToString(sFlow));
                }

                {
                    int lineNumber = 0;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_After);
                }
                {
                    int lineNumber = 1;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_After);
                }
                {
                    int lineNumber = 2;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, 0, state_After);
                }
            }
        }

        [TestMethod]
        public void Test_DynamicFlow_Backward_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.Quiet = false;
            string programStr =
                "           mov     rax,        0       ; line 0        " + Environment.NewLine +
                "           mov     rbx,        10      ; line 1        " + Environment.NewLine +
                "           mov     rbx,        rax     ; line 2        ";
            StaticFlow sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (logToDisplay)
            {
                Console.WriteLine(sFlow);
            }

            if (true)
            {
                DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
                //if (logToDisplay) Console.WriteLine(dFlow.ToString(sFlow));
                {
                    int lineNumber = 0;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];
                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_After);
                }
                {
                    int lineNumber = 1;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_After);
                }
                {
                    int lineNumber = 2;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];

                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (logToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: After lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_After);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_After);
                    AsmTestTools.AreEqual(Rn.RBX, 0, state_After);
                }
            }
        }
    }
}
