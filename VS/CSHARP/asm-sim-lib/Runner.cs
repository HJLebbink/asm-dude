// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmSim.Mnemonics;
using AsmTools;
using Microsoft.Z3;
using System;
using System.Collections.Generic;

namespace AsmSim
{
    public static class Runner
    {
        public static ExecutionTree<IExecutionNode> Construct_ExecutionTree_Forward(
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            return Construct_ExecutionTree_Forward(null, flow, startLine, maxSteps, tools);
        }

        public static ExecutionTree<IExecutionNode> Construct_ExecutionTree_Forward(
            State startState,
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLine))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree_Forward: startLine " + startLine + " does not exist in " + flow);
                return null;
            }

            Stack<IExecutionNode> nextNodes = new Stack<IExecutionNode>();

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh. 
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            ExecutionTree<IExecutionNode> stateTree;
            {
                State rootState = null;
                if (startState == null)
                {
                    string rootKey = Tools.CreateKey(tools.Rand);
                    rootState = new State(tools, rootKey, rootKey, startLine);
                }
                else
                {
                    rootState = startState;
                }
                int nextStep = 0;
                var rootNode = new ExecutionNode(nextStep, rootState, null);
                stateTree = new ExecutionTree<IExecutionNode>(rootNode, true);
                nextNodes.Push(rootNode);
            }
            #endregion

            while (nextNodes.Count > 0)
            {
                var node = nextNodes.Pop();
                int nextStep = node.Step + 1;

                if (nextStep <= maxSteps)
                {
                    int lineNumber = node.State.LineNumber;
                    if (flow.HasLine(lineNumber))
                    {
                        string prevKey = node.State.HeadKey; // the head of the state we depart from will be the previous for the current update
                        string prevKeyBranch = prevKey;
                        string nextKey = Tools.CreateKey(tools.Rand);
                        string nextKeyBranch = nextKey + "!BRANCH";

                        //Console.WriteLine("Going to run line " + lineNumber + "; prev=" + prevKey + "; next=" + nextKey);

                        var updates = Execute(flow, lineNumber, (prevKey, prevKeyBranch, nextKey, nextKeyBranch), tools);
                        var nextLineNumber = flow.GetNextLineNumber(lineNumber);

                        #region Handle Branch Control Flow
                        if (updates.Branch != null)
                        {
                            if (nextLineNumber.Branch == -1)
                            {
                                Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a branch yet a branch is computed");
                                throw new Exception();
                            }
                            var nextState = new State(node.State, nextLineNumber.Branch);
                            nextState.Update_Forward(updates.Branch);
                            node.Forward_Branch = new ExecutionNode(nextStep, nextState, node);
                            if (nextState.IsConsistent) nextNodes.Push(node.Forward_Branch);

                            if (!tools.Quiet) Console.WriteLine("=====================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: Branch stepNext " + nextStep + ": LINE " + lineNumber + ": " + flow.GetLineStr(lineNumber));
                            if (!tools.Quiet && flow.GetLine(lineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + nextState);
                        }
                        else if (nextLineNumber.Branch != -1)
                        {
                            Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a branch yet no branch is computed");
                            throw new Exception();
                        }
                        #endregion
                        #region Handle Regular Control Flow
                        if (updates.Regular != null)
                        {
                            if (nextLineNumber.Regular == -1)
                            {
                                Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a continueyet a continue is computed");
                                throw new Exception();
                            }
                            var nextState = new State(node.State, nextLineNumber.Regular);
                            nextState.Update_Forward(updates.Regular);
                            node.Forward_Continue = new ExecutionNode(nextStep, nextState, node);
                            if (nextState.IsConsistent) nextNodes.Push(node.Forward_Continue);

                            if (!tools.Quiet) Console.WriteLine("=====================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: Regular stepNext " + nextStep + ": LINE " + lineNumber + ": " + flow.GetLineStr(lineNumber));
                            if (!tools.Quiet && flow.GetLine(lineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + nextState);
                        }
                        else if (nextLineNumber.Regular != -1)
                        {
                            Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a regular continue yet no continue is computed");
                            throw new Exception();
                        }
                        #endregion
                    }
                }
            }
            return stateTree;
        }

        public static ExecutionTree2 Construct_ExecutionTree2_Forward(
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLine))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree2_Forward: startLine " + startLine + " does not exist in " + flow);
                return null;
            }

            Stack<ExecutionNode2> nextNodes = new Stack<ExecutionNode2>();

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh. 
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            ExecutionTree2 stateTree;
            {
                string rootKey = "!0";// Tools.CreateKey(tools.Rand);
                StateUpdate rootState = new StateUpdate(rootKey, rootKey, tools);
                int step = 0;
                var rootNode = new ExecutionNode2(step, startLine, rootState, null);
                stateTree = new ExecutionTree2(rootNode, true, tools);
                nextNodes.Push(rootNode);
            }
            #endregion

            while (nextNodes.Count > 0)
            {
                var node = nextNodes.Pop();
                int nextStep = node.Step + 1;

                if (nextStep <= maxSteps)
                {
                    int lineNumber = node.LineNumber;
                    if (flow.HasLine(lineNumber))
                    {
                        string prevKey = node.StateUpdate.NextKey; // the head of the state we depart from will be the previous for the current update
                        string prevKeyBranch = prevKey;
                        string nextKey = Tools.CreateKey(tools.Rand);
                        string nextKeyBranch = nextKey + "!BRANCH";

                        //Console.WriteLine("Going to run line " + lineNumber + "; prev=" + prevKey + "; next=" + nextKey);

                        var updates = Execute(flow, lineNumber, (prevKey, prevKeyBranch, nextKey, nextKeyBranch), tools);
                        var nextLineNumber = flow.GetNextLineNumber(lineNumber);

                        #region Handle Branch Control Flow
                        if (updates.Branch != null)
                        {
                            if (nextLineNumber.Branch == -1)
                            {
                                Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a branch yet a branch is computed");
                                throw new Exception();
                            }
                            node.Forward_Branch = new ExecutionNode2(nextStep, nextLineNumber.Branch, updates.Branch, node);
                            //if (nextState.IsConsistent) 
                            nextNodes.Push(node.Forward_Branch);

                            if (!tools.Quiet) Console.WriteLine("=====================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: Branch stepNext " + nextStep + ": LINE " + lineNumber + ": " + flow.GetLineStr(lineNumber));
                            if (!tools.Quiet && flow.GetLine(lineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + updates.Branch);
                        }
                        else if (nextLineNumber.Branch != -1)
                        {
                            Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a branch yet no branch is computed");
                            throw new Exception();
                        }
                        #endregion
                        #region Handle Regular Control Flow
                        if (updates.Regular != null)
                        {
                            if (nextLineNumber.Regular == -1)
                            {
                                Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there does not exists a continueyet a continue is computed");
                                throw new Exception();
                            }
                            node.Forward_Continue = new ExecutionNode2(nextStep, nextLineNumber.Regular, updates.Regular, node);
                            //if (nextState.IsConsistent) 
                            nextNodes.Push(node.Forward_Continue);

                            if (!tools.Quiet) Console.WriteLine("=====================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: Regular stepNext " + nextStep + ": LINE " + lineNumber + ": " + flow.GetLineStr(lineNumber));
                            if (!tools.Quiet && flow.GetLine(lineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Forward: " + updates.Regular);
                        }
                        else if (nextLineNumber.Regular != -1)
                        {
                            Console.WriteLine("WARNING: Runner:Construct_ExecutionTree_Forward: according to flow there exists a regular continue yet no continue is computed");
                            throw new Exception();
                        }
                        #endregion
                    }
                }
            }
            return stateTree;
        }

        public static ExecutionTree<IExecutionNode> Construct_ExecutionTree_Backward(
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            return Construct_ExecutionTree_Backward(null, flow, startLine, maxSteps, tools);
        }

        public static ExecutionTree<IExecutionNode> Construct_ExecutionTree_Backward(
            State startState,
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLine))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree_Backward: startLine " + startLine + " does not exist in " + flow);
                return null;
            }

            Stack<ExecutionNode> nextNodes = new Stack<ExecutionNode>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            ExecutionTree<IExecutionNode> stateTree;
            {
                State rootState = null;
                if (startState == null)
                {
                    string rootKey = "!0";// Tools.CreateKey(tools.Rand);
                    rootState = new State(tools, rootKey, rootKey, startLine); // create an empty state
                }
                else
                {
                    rootState = startState;
                }
                string prevKey = Tools.CreateKey(tools.Rand);
                string prevKeyBranch = prevKey + "!BRANCH";
                //string prevKeyBranch = prevKey;

                string nextKeyBranch = rootState.TailKey;

                var updates = Execute(flow, startLine, (prevKey, prevKeyBranch, rootState.TailKey, nextKeyBranch), tools);
                rootState.Update_Backward(updates.Regular);

                int step = 0;
                var rootNode = new ExecutionNode(step, rootState, null);
                nextNodes.Push(rootNode);
                stateTree = new ExecutionTree<IExecutionNode>(rootNode, false);

                if (!tools.Quiet) Console.WriteLine("===========================================");
                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + rootState.LineNumber + ": " + flow.GetLineStr(rootState.LineNumber));
                if (!tools.Quiet && flow.GetLine(startLine).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + rootState);
            }
            #endregion

            while (nextNodes.Count > 0)
            {
                var node = nextNodes.Pop();
                int step = node.Step + 1;

                //if (!tools.Quiet && node.LoopTerminationNode) Console.WriteLine("Node is LoopTerminationNode!");

                if (step < maxSteps)
                {
                    string nextKey = node.State.TailKey;
                    int lineNumber = node.State.LineNumber;

                    if (flow.IsBranchPoint(lineNumber))
                    {
                        // if a merge point in the two code paths (that are created in this instruction) exists in the existing execution tree,
                        // and this merge point has a branch condition that is not pinpointed, then we have learned something about a branch condition BC.
                        // BC is equal to the branch condition of the current instruction. Assert to all states that are accessible the equality.

                        /*
                        (BoolExpr branchCondition, int mergeLineNumber) = GetMergeCondition(flow, lineNumber, stateTree);
                        (int nextLineNumberRegular, int nextLineNumberBranch) = flow.GetNextLineNumber(lineNumber);

                        AssertControlFlow(nextLineNumberRegular, mergeLineNumber, tools.Ctx.MkEq(branchCondition, ), stateTree);
                        AssertControlFlow(nextLineNumberBranch, mergeLineNumber, branchCondition, stateTree);
                        */
                    }

                    IList<(int LineNumber, bool IsBranch)> prevLines = new List<(int LineNumber, bool IsBranch)>(flow.GetPrevLineNumber(lineNumber));
                    if (prevLines.Count == 0)
                    {
                        // nothing todo
                    }
                    else if (prevLines.Count == 1)
                    {
                        var prev = prevLines[0];
                        int prev_LineNumber = prev.LineNumber;

                        if (flow.HasLine(prev_LineNumber))
                        {
                            string prevKey = Tools.CreateKey(tools.Rand);

                            var updates = Runner.Execute(flow, prev_LineNumber, (prevKey, prevKey, nextKey, nextKey), tools);
                            var state = new State(node.State, prev_LineNumber);
                            state.Update_Backward((prev.IsBranch) ? updates.Branch : updates.Regular);

                            var nextNode = new ExecutionNode(step, state, node);
                            node.Add_Backward(nextNode);
                            if (state.IsConsistent) nextNodes.Push(nextNode);// only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                            if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + state.LineNumber + ": " + flow.GetLineStr(state.LineNumber));
                            if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + state);
                        }
                    }
                    else if (prevLines.Count == 2)
                    {
                        // two code flows merge at this lineNumber
                        var prev1 = prevLines[0];
                        var prev2 = prevLines[1];

                        int prev_LineNumber1 = prev1.LineNumber;
                        int prev_LineNumber2 = prev2.LineNumber;

                        if (flow.HasLine(prev_LineNumber1) && flow.HasLine(prev_LineNumber2))
                        {
                            string prevKey = Tools.CreateKey(tools.Rand);
                            string prevKey1 = prevKey + "!A";
                            string prevKey2 = prevKey + "!B";

                            var mergeState = new State(node.State, lineNumber);
                            {
                                var branchCondition = tools.Ctx.MkBoolConst("BC" + prevKey);
                                StateUpdate stateUpdateMerge = new StateUpdate(prevKey, node.State.TailKey, tools);
                                stateUpdateMerge.Set(Rn.RAX, tools.Ctx.MkITE(branchCondition, Tools.Reg_Key(Rn.RAX, prevKey1, tools.Ctx), Tools.Reg_Key(Rn.RAX, prevKey2, tools.Ctx)) as BitVecExpr);
                                stateUpdateMerge.Set(Rn.RBX, tools.Ctx.MkITE(branchCondition, Tools.Reg_Key(Rn.RBX, prevKey1, tools.Ctx), Tools.Reg_Key(Rn.RBX, prevKey2, tools.Ctx)) as BitVecExpr);
                                stateUpdateMerge.Set(Flags.ZF, tools.Ctx.MkITE(branchCondition, Tools.Flag_Key(Flags.ZF, prevKey1, tools.Ctx), Tools.Flag_Key(Flags.ZF, prevKey2, tools.Ctx)) as BoolExpr);
                                mergeState.Update_Backward(stateUpdateMerge);
                                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: mergeState=" + mergeState);
                            }

                            string prevPrevKey1 = prevKey + "!2!A";
                            string prevPrevKey2 = prevKey + "!2!B";
                            {
                                var updates1 = Execute(flow, prev1.LineNumber, (prevPrevKey1, prevPrevKey1, prevKey1, prevKey1), tools);
                                var stateUpdate1 = (prev1.IsBranch) ? updates1.Branch : updates1.Regular;
                                Console.WriteLine("stateUpdate1:" + stateUpdate1);

                                var state1 = new State(mergeState, prev_LineNumber1);
                                state1.Update_Backward(stateUpdate1);
                                ExecutionNode nextNode1 = new ExecutionNode(step, state1, node);
                                node.Add_Backward(nextNode1);

                                // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.
                                if (state1.IsConsistent) nextNodes.Push(nextNode1);

                                if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: A: step " + step + ": LINE " + state1.LineNumber + ": " + flow.GetLineStr(state1.LineNumber));
                                if (!tools.Quiet && flow.GetLine(prev_LineNumber1).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + state1);
                            }
                            {
                                var updates2 = Execute(flow, prev2.LineNumber, (prevPrevKey2, prevPrevKey2, prevKey2, prevKey2), tools);
                                var stateUpdate2 = (prev2.IsBranch) ? updates2.Branch : updates2.Regular;
                                Console.WriteLine("stateUpdate2:" + stateUpdate2);

                                var state2 = new State(mergeState, prev_LineNumber2);
                                state2.Update_Backward(stateUpdate2);
                                ExecutionNode nextNode2 = new ExecutionNode(step, state2, node);
                                node.Add_Backward(nextNode2);

                                // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.
                                if (state2.IsConsistent) nextNodes.Push(nextNode2);

                                if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: B: step " + step + ": LINE " + state2.LineNumber + ": " + flow.GetLineStr(state2.LineNumber));
                                if (!tools.Quiet && flow.GetLine(prev_LineNumber2).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + state2);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Runner:Construct_ExecutionTree_Backward: not implemented yet");
                        return null;
                        //                            throw new NotImplementedException();
                    }
                }
            }
            return stateTree;
        }

        public static ExecutionTree2 Construct_ExecutionTree2_Backward(
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLine))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree_Backward: startLine " + startLine + " does not exist in " + flow);
                return null;
            }

            Stack<ExecutionNode2> nextNodes = new Stack<ExecutionNode2>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            ExecutionTree2 stateTree;
            {
                string rootKey = "!0";// Tools.CreateKey(tools.Rand);
                var rootState = new StateUpdate(rootKey, rootKey, tools);
                string prevKey = Tools.CreateKey(tools.Rand);
                string prevKeyBranch = prevKey + "!BRANCH";

                var updates = Execute(flow, startLine, (prevKey, prevKeyBranch, rootState.NextKey, rootState.NextKey), tools);
                var nextUpdates = updates.Regular;

                int step = 0;
                var rootNode = new ExecutionNode2(step, startLine, nextUpdates, null);
                nextNodes.Push(rootNode);
                stateTree = new ExecutionTree2(rootNode, false, tools);

                if (!tools.Quiet) Console.WriteLine("===========================================");
                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + startLine + ": " + flow.GetLineStr(startLine));
                if (!tools.Quiet && flow.GetLine(startLine).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + nextUpdates);
            }
            #endregion

            while (nextNodes.Count > 0)
            {
                var node = nextNodes.Pop();
                int step = node.Step + 1;

                //if (!tools.Quiet && node.LoopTerminationNode) Console.WriteLine("Node is LoopTerminationNode!");

                if (step < maxSteps)
                {
                    string nextKey = node.StateUpdate.PrevKey;
                    int lineNumber = node.LineNumber;

                    if (flow.IsBranchPoint(lineNumber))
                    {
                        // if a merge point in the two code paths (that are created in this instruction) exists in the existing execution tree,
                        // and this merge point has a branch condition that is not pinpointed, then we have learned something about a branch condition BC.
                        // BC is equal to the branch condition of the current instruction. Assert to all states that are accessible the equality.

                        /*
                        (BoolExpr branchCondition, int mergeLineNumber) = GetMergeCondition(flow, lineNumber, stateTree);
                        (int nextLineNumberRegular, int nextLineNumberBranch) = flow.GetNextLineNumber(lineNumber);

                        AssertControlFlow(nextLineNumberRegular, mergeLineNumber, tools.Ctx.MkEq(branchCondition, ), stateTree);
                        AssertControlFlow(nextLineNumberBranch, mergeLineNumber, branchCondition, stateTree);
                        */
                    }

                    IList<(int LineNumber, bool IsBranch)> prevLines = new List<(int LineNumber, bool IsBranch)>(flow.GetPrevLineNumber(lineNumber));
                    if (prevLines.Count == 0)
                    {
                        // nothing todo
                    }
                    else if (prevLines.Count == 1)
                    {
                        var prev = prevLines[0];
                        int prev_LineNumber = prev.LineNumber;

                        if (flow.HasLine(prev_LineNumber))
                        {
                            string prevKey = Tools.CreateKey(tools.Rand);

                            var updates = Runner.Execute(flow, prev_LineNumber, (prevKey, prevKey, nextKey, nextKey), tools);
                            var stateUpdate = (prev.IsBranch) ? updates.Branch : updates.Regular;

                            var nextNode = new ExecutionNode2(step, prev_LineNumber, stateUpdate, node);
                            node.Add_Backward(nextNode);
                            //if (state.IsConsistent)
                            nextNodes.Push(nextNode);// only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                            if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + prev_LineNumber + ": " + flow.GetLineStr(prev_LineNumber));
                            if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + stateUpdate);
                        }
                    }
                    else if (prevLines.Count == 2)
                    {
                        // two code flows merge at this lineNumber
                        var prev1 = prevLines[0];
                        var prev2 = prevLines[1];

                        int prev_LineNumber1 = prev1.LineNumber;
                        int prev_LineNumber2 = prev2.LineNumber;

                        if (flow.HasLine(prev_LineNumber1) && flow.HasLine(prev_LineNumber2))
                        {
                            string prevKey = Tools.CreateKey(tools.Rand);
                            string prevKey1 = prevKey + "!A";
                            string prevKey2 = prevKey + "!B";

                            StateUpdate stateUpdateMerge;
                            {
                                var branchCondition = tools.Ctx.MkBoolConst("BC" + prevKey);
                                stateUpdateMerge = new StateUpdate(prevKey, node.StateUpdate.PrevKey, tools);
                                stateUpdateMerge.Set(Rn.RAX, tools.Ctx.MkITE(branchCondition, Tools.Reg_Key(Rn.RAX, prevKey1, tools.Ctx), Tools.Reg_Key(Rn.RAX, prevKey2, tools.Ctx)) as BitVecExpr);
                                stateUpdateMerge.Set(Rn.RBX, tools.Ctx.MkITE(branchCondition, Tools.Reg_Key(Rn.RBX, prevKey1, tools.Ctx), Tools.Reg_Key(Rn.RBX, prevKey2, tools.Ctx)) as BitVecExpr);

                                //TODO 

                                stateUpdateMerge.Set(Flags.ZF, tools.Ctx.MkITE(branchCondition, Tools.Flag_Key(Flags.ZF, prevKey1, tools.Ctx), Tools.Flag_Key(Flags.ZF, prevKey2, tools.Ctx)) as BoolExpr);
                                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: stateUpdateMerge=" + stateUpdateMerge);
                            }

                            string prevPrevKey1 = prevKey + "!2!A";
                            string prevPrevKey2 = prevKey + "!2!B";
                            {
                                var updates1 = Execute(flow, prev1.LineNumber, (prevPrevKey1, prevPrevKey1, prevKey1, prevKey1), tools);
                                var stateUpdate1 = (prev1.IsBranch) ? updates1.Branch : updates1.Regular;
                                Console.WriteLine("stateUpdate1:" + stateUpdate1);

                                var nextNode1 = new ExecutionNode2(step, prev_LineNumber1, stateUpdate1, node);
                                node.Add_Backward(nextNode1);

                                // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.
                                //if (state1.IsConsistent)
                                    nextNodes.Push(nextNode1);

                                if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: A: step " + step + ": LINE " + prev_LineNumber1 + ": " + flow.GetLineStr(prev_LineNumber1));
                                if (!tools.Quiet && flow.GetLine(prev_LineNumber1).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + stateUpdate1);
                            }
                            {
                                var updates2 = Execute(flow, prev2.LineNumber, (prevPrevKey2, prevPrevKey2, prevKey2, prevKey2), tools);
                                var stateUpdate2 = (prev2.IsBranch) ? updates2.Branch : updates2.Regular;
                                Console.WriteLine("stateUpdate2:" + stateUpdate2);

                                var nextNode2 = new ExecutionNode2(step, prev_LineNumber2, stateUpdate2, node);
                                node.Add_Backward(nextNode2);

                                // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.
                                //if (state2.IsConsistent)
                                    nextNodes.Push(nextNode2);

                                if (!tools.Quiet) Console.WriteLine("===========================================\nINFO: Runner:Construct_ExecutionTree_Backward: B: step " + step + ": LINE " + prev_LineNumber2 + ": " + flow.GetLineStr(prev_LineNumber2));
                                if (!tools.Quiet && flow.GetLine(prev_LineNumber2).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + stateUpdate2);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Runner:Construct_ExecutionTree_Backward: not implemented yet");
                        return null;
                        //                            throw new NotImplementedException();
                    }
                }
            }
            return stateTree;
        }

        public static ExecutionTree<IExecutionNode> Construct_ExecutionTree_Backward_OLD(
            State startState,
            CFlow flow,
            int startLine,
            int maxSteps,
            Tools tools)
        {
            if (!flow.HasLine(startLine))
            {
                if (!tools.Quiet) Console.WriteLine("WARNING: Construct_ExecutionTree_Backward: startLine " + startLine + " does not exist in " + flow);
                return null;
            }

            Stack<IExecutionNode> nextNodes = new Stack<IExecutionNode>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            ExecutionTree<IExecutionNode> stateTree;
            {
                State rootState = null;
                if (startState == null)
                {
                    string rootKey = "!0";// Tools.CreateKey(tools.Rand);
                    rootState = new State(tools, rootKey, rootKey, startLine); // create an empty state
                }
                else
                {
                    rootState = startState;
                }
                string prevKey = Tools.CreateKey(tools.Rand);
                string prevKeyBranch = prevKey + "!BRANCH";
                //string prevKeyBranch = prevKey;

                string nextKeyBranch = rootState.TailKey;

                var updates = Execute(flow, startLine, (prevKey, prevKeyBranch, rootState.TailKey, nextKeyBranch), tools);
                rootState.Update_Backward(updates.Regular);

                int step = 0;
                IExecutionNode rootNode = new ExecutionNode(step, rootState, null);
                nextNodes.Push(rootNode);
                stateTree = new ExecutionTree<IExecutionNode>(rootNode, false);

                if (!tools.Quiet) Console.WriteLine("===========================================");
                if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + rootState.LineNumber + ": " + flow.GetLineStr(rootState.LineNumber));
                if (!tools.Quiet && flow.GetLine(startLine).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + rootState);
            }
            #endregion

            while (nextNodes.Count > 0)
            {
                IExecutionNode node = nextNodes.Pop();
                int step = node.Step + 1;

                //if (!tools.Quiet && node.LoopTerminationNode) Console.WriteLine("Node is LoopTerminationNode!");

                if (step < maxSteps)
                {
                    string nextKey = node.State.TailKey;
                    string nextKeyBranch = nextKey;

                    foreach (var prev in flow.GetPrevLineNumber(node.State.LineNumber))
                    {
                        int lineNumber = prev.LineNumber;
                        if (flow.HasLine(lineNumber))
                        {
                            string prevKey = Tools.CreateKey(tools.Rand);
                            string prevKeyBranch = prevKey + "!BRANCH";
                            //string prevKeyBranch = prevKey;

                            var state = new State(node.State, lineNumber);
                            var updates = Execute(flow, prev.LineNumber, (prevKey, prevKeyBranch, nextKey, nextKeyBranch), tools);

                            state.Update_Backward((prev.IsBranch) ? updates.Branch : updates.Regular);

                            IExecutionNode nextNode = new ExecutionNode(step, state, node);
                            node.Add_Backward(nextNode);

                            // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.
                            if (state.IsConsistent) nextNodes.Push(nextNode);

                            if (!tools.Quiet) Console.WriteLine("===========================================");
                            if (!tools.Quiet) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: step " + step + ": LINE " + state.LineNumber + ": " + flow.GetLineStr(state.LineNumber));
                            if (!tools.Quiet && flow.GetLine(lineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_ExecutionTree_Backward: " + state);
                        }
                    }
                }
            }
            return stateTree;
        }

        /// <summary>Perform one step forward and return the regular branch</summary>
        public static State SimpleStep_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state, state.LineNumber + 1);
            stateOut.Update_Forward(opcodeBase.Updates.Regular);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Forward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform onestep forward and return the state of the regular branch</summary>
        public static State SimpleStep_Backward(string line, State state)
        {
            string prevKey = Tools.CreateKey(state.Tools.Rand);
            string prevKeyBranch = prevKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (prevKey, prevKeyBranch, state.TailKey, state.TailKey), state.Tools);
            if (opcodeBase == null) return null;
            if (opcodeBase.IsHalted) return null;

            opcodeBase.Execute();
            State stateOut = new State(state, state.LineNumber - 1);
            stateOut.Update_Backward(opcodeBase.Updates.Regular);
            if (!state.Tools.Quiet) Console.WriteLine("INFO: Runner:SimpleStep_Backward: after \"" + line + "\" we know:");
            if (!state.Tools.Quiet) Console.WriteLine(stateOut);
            return stateOut;
        }

        /// <summary>Perform one step forward and return states for both branches</summary>
        public static (State Regular, State Branch) Step_Forward(string line, State state)
        {
            string nextKey = Tools.CreateKey(state.Tools.Rand);
            string nextKeyBranch = nextKey;// + "!B";
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, (state.HeadKey, state.HeadKey, nextKey, nextKeyBranch), state.Tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            State stateRegular = null;
            if (opcodeBase.Updates.Regular != null)
            {
                stateRegular = new State(state, state.LineNumber + 1);
                stateRegular.Update_Forward(opcodeBase.Updates.Regular);
            }
            State stateBranch = null;
            if (opcodeBase.Updates.Branch != null)
            {
                stateBranch = new State(state, state.LineNumber + 1);
                stateBranch.Update_Forward(opcodeBase.Updates.Branch);
            }
            return (Regular: stateRegular, Branch: stateBranch);
        }

        public static (StateUpdate Regular, StateUpdate Branch) Execute(
            CFlow flow,
            int lineNumber,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.GetLine(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);
            if (opcodeBase.IsHalted) return (Regular: null, Branch: null);

            opcodeBase.Execute();
            return opcodeBase.Updates;
        }

        /// <summary>Get the branch condition for the provided lineNumber</summary>
        public static (BoolExpr Regular, BoolExpr Branch) GetBranchCondition(
            CFlow flow,
            int lineNumber,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            var content = flow.GetLine(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, keys, tools);
            if (opcodeBase == null) return (Regular: null, Branch: null);

            throw new NotImplementedException();
        }

        public static StateConfig GetUsage_StateConfig(
            CFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            StateConfig config = new StateConfig();
            config.Set_All_Off();
            var usage = GetUsage(flow, 0, flow.LastLineNumber, tools);
            config.Set_Flags_On(usage.Flags);
            foreach (Rn reg in usage.Regs) config.Set_Reg_On(reg);
            config.mem = usage.Mem;
            return config;
        }
        public static (ISet<Rn> Regs, Flags Flags, bool Mem) GetUsage(
            CFlow flow,
            int lineNumberBegin,
            int lineNumberEnd,
            Tools tools)
        {
            ISet<Rn> regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            bool mem = false;
            var dummyKeys = ("", "", "", "");
            for (int lineNumber = lineNumberBegin; lineNumber <= lineNumberEnd; lineNumber++)
            {
                var content = flow.GetLine(lineNumber);
                var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);
                if (opcodeBase != null)
                {
                    flags |= (opcodeBase.FlagsReadStatic | opcodeBase.FlagsWriteStatic);
                    foreach (Rn r in opcodeBase.RegsReadStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    foreach (Rn r in opcodeBase.RegsWriteStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                    mem |= opcodeBase.MemReadWriteStatic;
                }
            }
            return (Regs: regs, Flags: flags, Mem: mem);
        }

        public static OpcodeBase InstantiateOpcode(
            Mnemonic mnemonic,
            string[] args,
            (string prevKey, string prevKeyBranch, string nextKey, string nextKeyBranch) keys,
            Tools t)
        {
            switch (mnemonic)
            {
                #region NonSse
                case Mnemonic.NONE:
                case Mnemonic.UNKNOWN: return new Ignore(mnemonic, args, keys, t);
                case Mnemonic.MOV: return new Mov(args, keys, t);
                case Mnemonic.CMOVE:
                case Mnemonic.CMOVZ:
                case Mnemonic.CMOVNE:
                case Mnemonic.CMOVNZ:
                case Mnemonic.CMOVA:
                case Mnemonic.CMOVNBE:
                case Mnemonic.CMOVAE:
                case Mnemonic.CMOVNB:
                case Mnemonic.CMOVB:
                case Mnemonic.CMOVNAE:
                case Mnemonic.CMOVBE:
                case Mnemonic.CMOVNA:
                case Mnemonic.CMOVG:
                case Mnemonic.CMOVNLE:
                case Mnemonic.CMOVGE:
                case Mnemonic.CMOVNL:
                case Mnemonic.CMOVL:
                case Mnemonic.CMOVNGE:
                case Mnemonic.CMOVLE:
                case Mnemonic.CMOVNG:
                case Mnemonic.CMOVC:
                case Mnemonic.CMOVNC:
                case Mnemonic.CMOVO:
                case Mnemonic.CMOVNO:
                case Mnemonic.CMOVS:
                case Mnemonic.CMOVNS:
                case Mnemonic.CMOVP:
                case Mnemonic.CMOVPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.CMOVPO: return new Cmovcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.XCHG: break;
                case Mnemonic.BSWAP: break;
                case Mnemonic.XADD: return new Xadd(args, keys, t);
                case Mnemonic.CMPXCHG: break;
                case Mnemonic.CMPXCHG8B: break;
                case Mnemonic.PUSH: return new Push(args, keys, t);
                case Mnemonic.POP: return new Pop(args, keys, t);
                case Mnemonic.PUSHA: break;
                case Mnemonic.PUSHAD: break;
                case Mnemonic.POPA: break;
                case Mnemonic.POPAD: break;
                case Mnemonic.CWD: return new Cwd(args, keys, t);
                case Mnemonic.CDQ: return new Cdq(args, keys, t);
                case Mnemonic.CBW: return new Cbw(args, keys, t);
                case Mnemonic.CWDE: return new Cwde(args, keys, t);
                case Mnemonic.CQO: return new Cqo(args, keys, t);
                case Mnemonic.MOVSX: return new Movsx(args, keys, t);
                case Mnemonic.MOVSXD: return new Movsxd(args, keys, t);
                case Mnemonic.MOVZX: return new Movzx(args, keys, t);
                //case Mnemonic.MOVZXD: return new Movzxd(args, keys, t);
                case Mnemonic.ADCX: return new Adcx(args, keys, t);
                case Mnemonic.ADOX: return new Adox(args, keys, t);
                case Mnemonic.ADD: return new Add(args, keys, t);
                case Mnemonic.ADC: return new Adc(args, keys, t);
                case Mnemonic.SUB: return new Sub(args, keys, t);
                case Mnemonic.SBB: return new Sbb(args, keys, t);
                case Mnemonic.IMUL: return new Imul(args, keys, t);
                case Mnemonic.MUL: return new Mul(args, keys, t);
                case Mnemonic.IDIV: return new Idiv(args, keys, t);
                case Mnemonic.DIV: return new Div(args, keys, t);
                case Mnemonic.INC: return new Inc(args, keys, t);
                case Mnemonic.DEC: return new Dec(args, keys, t);
                case Mnemonic.NEG: return new Neg(args, keys, t);
                case Mnemonic.CMP: return new Cmp(args, keys, t);

                case Mnemonic.DAA: break;
                case Mnemonic.DAS: break;
                case Mnemonic.AAA: break;
                case Mnemonic.AAS: break;
                case Mnemonic.AAM: break;
                case Mnemonic.AAD: break;

                case Mnemonic.AND: return new And(args, keys, t);
                case Mnemonic.OR: return new Or(args, keys, t);
                case Mnemonic.XOR: return new Xor(args, keys, t);
                case Mnemonic.NOT: return new Not(args, keys, t);
                case Mnemonic.SAR: return new Sar(args, keys, t);
                case Mnemonic.SHR: return new Shr(args, keys, t);
                case Mnemonic.SAL: return new Sal(args, keys, t);
                case Mnemonic.SHL: return new Shl(args, keys, t);
                case Mnemonic.SHRD: return new Shrd(args, keys, t);
                case Mnemonic.SHLD: return new Shld(args, keys, t);
                case Mnemonic.ROR: return new Ror(args, keys, t);
                case Mnemonic.ROL: return new Rol(args, keys, t);
                case Mnemonic.RCR: return new Rcr(args, keys, t);
                case Mnemonic.RCL: return new Rcl(args, keys, t);

                case Mnemonic.BT: return new Bt_Opcode(args, keys, t);
                case Mnemonic.BTS: return new Bts(args, keys, t);
                case Mnemonic.BTR: return new Btr(args, keys, t);
                case Mnemonic.BTC: return new Btc(args, keys, t);
                case Mnemonic.BSF: return new Bsf(args, keys, t);
                case Mnemonic.BSR: return new Bsr(args, keys, t);
                case Mnemonic.TEST: return new Test(args, keys, t);
                case Mnemonic.CRC32: break;//return new Crc32(args, keys, t);

                case Mnemonic.SETE:
                case Mnemonic.SETZ:
                case Mnemonic.SETNE:
                case Mnemonic.SETNZ:
                case Mnemonic.SETA:
                case Mnemonic.SETNBE:
                case Mnemonic.SETAE:
                case Mnemonic.SETNB:
                case Mnemonic.SETNC:
                case Mnemonic.SETB:
                case Mnemonic.SETNAE:
                case Mnemonic.SETC:
                case Mnemonic.SETBE:
                case Mnemonic.SETNA:
                case Mnemonic.SETG:
                case Mnemonic.SETNLE:
                case Mnemonic.SETGE:
                case Mnemonic.SETNL:
                case Mnemonic.SETL:
                case Mnemonic.SETNGE:
                case Mnemonic.SETLE:
                case Mnemonic.SETNG:
                case Mnemonic.SETS:
                case Mnemonic.SETNS:
                case Mnemonic.SETO:
                case Mnemonic.SETNO:
                case Mnemonic.SETPE:
                case Mnemonic.SETP:
                case Mnemonic.SETNP:
                case Mnemonic.SETPO: return new Setcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.JMP: return new Jmp(args, keys, t);

                case Mnemonic.JE:
                case Mnemonic.JZ:
                case Mnemonic.JNE:
                case Mnemonic.JNZ:
                case Mnemonic.JA:
                case Mnemonic.JNBE:
                case Mnemonic.JAE:
                case Mnemonic.JNB:
                case Mnemonic.JB:
                case Mnemonic.JNAE:
                case Mnemonic.JBE:
                case Mnemonic.JNA:
                case Mnemonic.JG:
                case Mnemonic.JNLE:
                case Mnemonic.JGE:
                case Mnemonic.JNL:
                case Mnemonic.JL:
                case Mnemonic.JNGE:
                case Mnemonic.JLE:
                case Mnemonic.JNG:
                case Mnemonic.JC:
                case Mnemonic.JNC:
                case Mnemonic.JO:
                case Mnemonic.JNO:
                case Mnemonic.JS:
                case Mnemonic.JNS:
                case Mnemonic.JPO:
                case Mnemonic.JNP:
                case Mnemonic.JPE:
                case Mnemonic.JP:
                case Mnemonic.JCXZ:
                case Mnemonic.JECXZ:
                case Mnemonic.JRCXZ: return new Jmpcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.LOOP: return new Loop(args, keys, t);
                case Mnemonic.LOOPZ: return new Loopz(args, keys, t);
                case Mnemonic.LOOPE: return new Loope(args, keys, t);
                case Mnemonic.LOOPNZ: return new Loopnz(args, keys, t);
                case Mnemonic.LOOPNE: return new Loopne(args, keys, t);

                case Mnemonic.CALL: break;// return new Call(args, keys, t);
                case Mnemonic.RET: break; // return new Ret(args, keys, t);
                case Mnemonic.IRET: break;
                case Mnemonic.INT: break;
                case Mnemonic.INTO: break;
                case Mnemonic.BOUND: break;
                case Mnemonic.ENTER: break;
                case Mnemonic.LEAVE: break;
                case Mnemonic.MOVS: break;
                case Mnemonic.MOVSB: break;
                case Mnemonic.MOVSW: break;
                case Mnemonic.MOVSD: break;
                case Mnemonic.CMPS: break;
                case Mnemonic.CMPSB: break;
                case Mnemonic.CMPSW: break;
                case Mnemonic.CMPSD: break;
                case Mnemonic.SCAS: break;
                case Mnemonic.SCASB: break;
                case Mnemonic.SCASW: break;
                case Mnemonic.SCASD: break;
                case Mnemonic.LODS: break;
                case Mnemonic.LODSB: break;
                case Mnemonic.LODSW: break;
                case Mnemonic.LODSD: break;
                case Mnemonic.STOS: break;
                case Mnemonic.STOSB: break;
                case Mnemonic.STOSW: break;
                case Mnemonic.STOSD: break;
                case Mnemonic.REP: break;
                case Mnemonic.REPE: break;
                case Mnemonic.REPZ: break;
                case Mnemonic.REPNE: break;
                case Mnemonic.REPNZ: break;
                case Mnemonic.IN: return new In(args, keys, t);
                case Mnemonic.OUT: return new Out(args, keys, t);
                case Mnemonic.INS: break;
                case Mnemonic.INSB: break;
                case Mnemonic.INSW: break;
                case Mnemonic.INSD: break;
                case Mnemonic.OUTS: break;
                case Mnemonic.OUTSB: break;
                case Mnemonic.OUTSW: break;
                case Mnemonic.OUTSD: break;
                case Mnemonic.STC: return new Stc(args, keys, t);
                case Mnemonic.CLC: return new Clc(args, keys, t);
                case Mnemonic.CMC: return new Cmc(args, keys, t);
                case Mnemonic.CLD: break;
                case Mnemonic.STD: break;
                case Mnemonic.LAHF: return new Lahf(args, keys, t);
                case Mnemonic.SAHF: return new Sahf(args, keys, t);
                case Mnemonic.PUSHF: break;
                case Mnemonic.PUSHFD: break;
                case Mnemonic.POPF: break;
                case Mnemonic.POPFD: break;
                case Mnemonic.STI: break;
                case Mnemonic.CLI: break;
                case Mnemonic.LDS: break;
                case Mnemonic.LES: break;
                case Mnemonic.LFS: break;
                case Mnemonic.LGS: break;
                case Mnemonic.LSS: break;
                case Mnemonic.LEA: return new Lea(args, keys, t);
                case Mnemonic.NOP: return new Nop(args, keys, t);
                case Mnemonic.UD2: return new Nop(args, keys, t);
                case Mnemonic.XLAT: break;
                case Mnemonic.XLATB: break;
                case Mnemonic.CPUID: break;
                case Mnemonic.MOVBE: break;
                case Mnemonic.PREFETCHW: return new Nop(args, keys, t);
                case Mnemonic.PREFETCHWT1: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSH: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSHOPT: return new Nop(args, keys, t);
                case Mnemonic.XSAVE: break;
                case Mnemonic.XSAVEC: break;
                case Mnemonic.XSAVEOPT: break;
                case Mnemonic.XRSTOR: break;
                case Mnemonic.XGETBV: break;
                case Mnemonic.RDRAND: break;
                case Mnemonic.RDSEED: break;
                case Mnemonic.ANDN: break;
                case Mnemonic.BEXTR: break;
                case Mnemonic.BLSI: break;
                case Mnemonic.BLSMSK: break;
                case Mnemonic.BLSR: break;
                case Mnemonic.BZHI: break;
                case Mnemonic.LZCNT: break;
                case Mnemonic.MULX: break;
                case Mnemonic.PDEP: break;
                case Mnemonic.PEXT: break;
                case Mnemonic.RORX: return new Rorx(args, keys, t);
                case Mnemonic.SARX: return new Sarx(args, keys, t);
                case Mnemonic.SHLX: return new Shlx(args, keys, t);
                case Mnemonic.SHRX: return new Shrx(args, keys, t);
                case Mnemonic.TZCNT: break;

                #endregion NonSse

                #region SSE
                //case Mnemonic.ADDPD: return new AddPD(args, keys, t); 
                case Mnemonic.POPCNT: return new Popcnt(args, keys, t);

                #endregion SSE

                default: return new NotImplemented(mnemonic, args, keys, t);
            }
            return new NotImplemented(mnemonic, args, keys, t);
        }
    }
}
