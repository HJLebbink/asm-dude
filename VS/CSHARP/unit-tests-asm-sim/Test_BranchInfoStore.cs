using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;

using AsmTools;
using AsmSim;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_BranchInfoStore
    {
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
        public void Test_BranchInfoStore_forwardMerge_1()
        {
            Tools tools = CreateTools();
            tools.StateConfig.RAX = true;
            tools.StateConfig.ZF = true;

            State state0 = CreateState(tools);
            State state1 = new State(state0);
            State state2 = new State(state0);

            BoolExpr branchCondition = state0.Get(Flags.ZF);
            {
                string nextKey1 = Tools.CreateKey(tools.Rand);
                StateUpdate stateUpdate1 = new StateUpdate(state1.HeadKey, nextKey1, tools);
                stateUpdate1.Set(Rn.RAX, 10);
                stateUpdate1.BranchInfo = new BranchInfo(branchCondition, true, 0);
                state1.Update_Forward(stateUpdate1);
            }
            {
                string nextKey2 = Tools.CreateKey(tools.Rand);
                StateUpdate stateUpdate2 = new StateUpdate(state2.HeadKey, nextKey2, tools);
                stateUpdate2.Set(Rn.RAX, 20);
                stateUpdate2.BranchInfo = new AsmSim.BranchInfo(branchCondition, false, 0);
                state2.Update_Forward(stateUpdate2);
            }

            Console.WriteLine("state1=\n" + state1);
            Console.WriteLine("state2=\n" + state2);

            var sharedBranchInfo = BranchInfoStore.RetrieveSharedBranchInfo(state1.BranchInfoStore, state2.BranchInfoStore, tools);

            Console.WriteLine(sharedBranchInfo.BranchPoint1);
            Console.WriteLine(sharedBranchInfo.BranchPoint2);
            Console.WriteLine(sharedBranchInfo.MergedBranchInfo);

            Assert.AreEqual(0, sharedBranchInfo.MergedBranchInfo.Count);
            Assert.AreEqual(branchCondition, sharedBranchInfo.BranchPoint1.BranchCondition);
            Assert.AreEqual(branchCondition, sharedBranchInfo.BranchPoint2.BranchCondition);
        }
    }
}
