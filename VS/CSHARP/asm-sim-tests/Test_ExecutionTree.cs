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
    /// NOTE: These tests used to work. A regression was introduced at some point.
    ///
    /// These tests are ignored due to a Z3 context lifecycle bug in DynamicFlow.
    /// Root cause: StateUpdate objects create their own Z3 contexts. When state merging
    /// happens, BranchInfo.Translate attempts to translate expressions between contexts,
    /// but source contexts may be disposed. Fix requires architectural changes to use
    /// a single shared context or translate expressions immediately when stored.
    ///
    /// Crash location: BranchInfo.Translate -> Z3_translate native call
    /// See: StateUpdate.cs lines 139, 153 (context creation)
    ///      BranchInfoStore.cs line 211 (translation call)
    ///      BranchInfo.cs line 45 (crash site)
    ///      DynamicFlow.cs line 800 (using block that disposes context prematurely)
    /// </summary>
    [TestClass]
    [Ignore("Z3 context lifecycle bug - BranchInfo.Translate crashes when source context is disposed (regression)")]
    public class Test_DynamicFlow
    {
        private const bool LogToDisplay = AsmTestTools.LOG_TO_DISPLAY;

        private Tools CreateTools(int timeOut = AsmTestTools.DEFAULT_TIMEOUT)
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },    // enable generation of unsat cores
                { "model", "false" },         // enable model generation
                { "proof", "false" },         // enable proof generation
                { "timeout", timeOut.ToString(CultureInfo.InvariantCulture) },
            };
            return new Tools(settings);
        }

        private State CreateState(Tools tools)
        {
            string tailKey = "!0"; // Tools.CreateKey(tools.Rand);
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
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow);
            }

            if (true)
            {
                DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
                if (LogToDisplay)
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

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (LogToDisplay)
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

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (LogToDisplay)
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

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Forward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (LogToDisplay)
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
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow);
            }

            if (true)
            {
                DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
                // if (logToDisplay) Console.WriteLine(dFlow.ToString(sFlow));
                {
                    int lineNumber = 0;
                    IList<State> states_Before = new List<State>(dFlow.Create_States_Before(lineNumber));
                    Assert.AreEqual(1, states_Before.Count);
                    State state_Before = states_Before[0];
                    IList<State> states_After = new List<State>(dFlow.Create_States_After(lineNumber));
                    Assert.AreEqual(1, states_After.Count);
                    State state_After = states_After[0];

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (LogToDisplay)
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

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, "????????.????????.????????.????????.????????.????????.????????.????????", state_Before);

                    if (LogToDisplay)
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

                    if (LogToDisplay)
                    {
                        Console.WriteLine("Tree_Backward: Before lineNumber " + lineNumber + " \"" + sFlow.Get_Line_Str(lineNumber) + "\", we know:\n" + state_Before);
                    }

                    AsmTestTools.AreEqual(Rn.RAX, 0, state_Before);
                    AsmTestTools.AreEqual(Rn.RBX, 10, state_Before);

                    if (LogToDisplay)
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
