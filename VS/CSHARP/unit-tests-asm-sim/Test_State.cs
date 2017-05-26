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
            string tailKey = "!INIT";// Tools.CreateKey(tools.Rand);
            string headKey = tailKey;
            return new State(tools, tailKey, headKey);
        }

        [TestMethod]
        public void Test_State_MergeConstructor_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.CF = true;

            State state1 = CreateState(tools);
            State state2 = CreateState(tools);
            {
                StateUpdate updateState1 = new StateUpdate(state1.HeadKey, Tools.CreateKey(state1.Tools.Rand), state1.Tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state1.HeadKey, state1.Ctx), true)
                };
                updateState1.Set(Rn.RAX, 10);
                state1.Update_Forward(updateState1);
            }
            {
                StateUpdate updateState2 = new StateUpdate(state2.HeadKey, Tools.CreateKey(state2.Tools.Rand), state2.Tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state2.HeadKey, state2.Ctx), false)
                };
                updateState2.Set(Rn.RAX, 10);
                state2.Update_Forward(updateState2);
            }
            State state1_2 = new State(state1, state2, true);
            if (logToDisplay) Console.WriteLine(state1_2);

            TestTools.AreEqual(Tv.ONE, state1.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state1_2.IsConsistent);
            TestTools.AreEqual(Rn.RAX, 10, state1_2);
        }

        [TestMethod]
        public void Test_State_MergeConstructor_2()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.CF = true;

            State state1 = CreateState(tools);
            State state2 = CreateState(tools);

            BoolExpr branchCondition = tools.Ctx.MkEq(state1.Get(Flags.CF), tools.Ctx.MkTrue());
            {
                StateUpdate updateState1 = new StateUpdate(state1.HeadKey, Tools.CreateKey(state1.Tools.Rand), state1.Tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state1.HeadKey, state1.Ctx), true)
                };
                updateState1.Set(Rn.RAX, 10);
                state1.Update_Forward(updateState1);
            }
            {
                StateUpdate updateState2 = new StateUpdate(state2.HeadKey, Tools.CreateKey(state2.Tools.Rand), state2.Tools)
                {
                    BranchInfo = new BranchInfo(ToolsAsmSim.ConditionalTaken(ConditionalElement.C, state2.HeadKey, state2.Ctx), false)
                };
                updateState2.Set(Rn.RAX, 20);
                state2.Update_Forward(updateState2);
            }
            State state1_2 = new State(state1, state2, true);
            if (logToDisplay) Console.WriteLine(state1_2);

            TestTools.AreEqual(Tv.ONE, state1.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state1_2.IsConsistent);
            TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state1_2);
        }

        [TestMethod]
        public void Test_State_MergeConstructor_3()
        {
            Tools tools = CreateTools();
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.CF = true;

            Random rand = tools.Rand;
            State state0 = CreateState(tools);
            {
                StateUpdate updateState0 = new StateUpdate(state0.HeadKey, Tools.CreateKey(rand), tools);
                updateState0.Set(Rn.RAX, 0);
                state0.Update_Forward(updateState0);
            }
            State state1 = new State(state0);
            State state2 = new State(state0);

            BoolExpr branchCondition = tools.Ctx.MkEq(state0.Get(Flags.CF), tools.Ctx.MkTrue());
            {
                StateUpdate updateState1 = new StateUpdate(state1.HeadKey, Tools.CreateKey(rand), tools)
                {
                    BranchInfo = new BranchInfo(branchCondition, true)
                };
                updateState1.Set(Rn.RAX, 10);
                state1.Update_Forward(updateState1);
            }
            {
                StateUpdate updateState2 = new StateUpdate(state2.HeadKey, Tools.CreateKey(rand), tools)
                {
                    BranchInfo = new BranchInfo(branchCondition, false)
                };
                updateState2.Set(Rn.RAX, 20);
                state2.Update_Forward(updateState2);
            }

            if (logToDisplay) Console.WriteLine("state0:\n" + state0);
            if (logToDisplay) Console.WriteLine("state1:\n" + state1);
            if (logToDisplay) Console.WriteLine("state2:\n" + state2);

            State state1_2 = new State(state1, state2, true);
            if (logToDisplay) Console.WriteLine(state1_2);

            TestTools.AreEqual(Tv.ONE, state1.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state2.IsConsistent);
            TestTools.AreEqual(Tv.ONE, state1_2.IsConsistent);
            TestTools.AreEqual(Rn.RAX, "00000000_00000000_00000000_00000000_00000000_00000000_00000000_000????0", state1_2);
        }
    }
}
