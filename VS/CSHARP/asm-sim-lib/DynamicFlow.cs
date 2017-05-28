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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using QuickGraph;
using Microsoft.Z3;
using AsmTools;

namespace AsmSim
{
    public class DynamicFlow
    {
        #region Fields

        private readonly Tools _tools;
        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> _graph;
        private readonly IDictionary<int, IList<string>> _lineNumber_2_Key;
        private readonly IDictionary<string, (int LineNumber, int Step)> _key_2_LineNumber_Step;
        private string _rootKey;
        private object _updateLock = new object();
        #endregion 

        #region Constructors
        public DynamicFlow(Tools tools)
        {
            this._tools = tools;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>>(true);
            this._lineNumber_2_Key = new Dictionary<int, IList<string>>();
            this._key_2_LineNumber_Step = new Dictionary<string, (int LineNumber, int Step)>();
        }
        #endregion

        #region Getters

        public BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> Graph { get { return this._graph; } }

        public bool Has_Vertex(string key)
        {
            return this._graph.ContainsVertex(key);
        }

        public bool Has_Edge(string source, string target, bool isBranch)
        {
            if (this._graph.TryGetEdge(source, target, out var tag))
            {
                return (tag.Tag.Branch == isBranch);
            }
            return false;
        }

        public int Step(string key)
        {
            return (this._key_2_LineNumber_Step.TryGetValue(key, out var v)) ? v.Step : -1;
        }

        public int LineNumber(string key)
        {
            return (this._key_2_LineNumber_Step.TryGetValue(key, out var v)) ? v.LineNumber : -1;
        }

        public IEnumerable<State> States_Before(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out IList<string> keys))
            {
                foreach (string key in keys) yield return Construct_State_Private(key, false);
            }
        }

        public State States_Before(int lineNumber, int index)
        {
            int counter = 0;
            foreach (State state in States_Before(lineNumber))
            {
                if (index == counter) return state;
                counter++;
            }
            return null;
        }

        public IEnumerable<State> States_After(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out IList<string> keys))
            {
                foreach (string key in keys) yield return Construct_State_Private(key, true);
            }
        }

        public State States_After(int lineNumber, int index)
        {
            int counter = 0;
            foreach (State state in States_After(lineNumber))
            {
                if (index == counter) return state;
                counter++;
            }
            return null;
        }

        public State State_After(string key)
        {
            if (!this._graph.ContainsVertex(key)) return null;
            return Construct_State_Private(key, true);
        }

        public State State_Before(string key)
        {
            if (!this._graph.ContainsVertex(key)) return null;
            return Construct_State_Private(key, false);
        }

        public bool Has_Branch(int lineNumber)
        {
            string key = this._lineNumber_2_Key[lineNumber][0];
            return (this._graph.OutDegree(key) > 1);
        }

        public BoolExpr Get_Branch_Condition(int lineNumber)
        {
            string key = this._lineNumber_2_Key[lineNumber][0];
            return this._graph.OutEdge(key, 0).Tag.StateUpdate.BranchInfo.BranchCondition;
        }

        public IEnumerable<State> Leafs
        {
            get
            {
                var alreadyVisisted = new HashSet<string>();
                foreach (string key in Get_Leafs_LOCAL(this._rootKey))
                {
                    yield return this.Construct_State_Private(key, true);
                }

                #region Local Methods
                IEnumerable<string> Get_Leafs_LOCAL(string key)
                {
                    if (alreadyVisisted.Contains(key)) yield break;
                    alreadyVisisted.Add(key);

                    if (this._graph.IsOutEdgesEmpty(key))
                    {
                        yield return key;
                    }
                    else
                        foreach (var edge in this._graph.OutEdges(key))
                            foreach (string v in Get_Leafs_LOCAL(edge.Target))
                                yield return v;
                }
                #endregion
            }
        }
    
        public State EndState { get { return Tools.Collapse(this.Leafs); } }

        #endregion

        #region Setters

        public void Reset(StaticFlow sFlow, bool forward)
        {
            this._rootKey = "!" +sFlow.FirstLineNumber;
            this._graph.Clear();
            this._lineNumber_2_Key.Clear();
            this._key_2_LineNumber_Step.Clear();

            lock (this._updateLock)
            {
                if (forward)
                {
                    this.Update_Forward(sFlow, sFlow.FirstLineNumber, sFlow.NLines * 2);
                }
                else
                {
                    this.Update_Backward(sFlow, sFlow.FirstLineNumber, sFlow.NLines * 2);
                }
            }
        }

        private void Update_Forward(
            StaticFlow sFlow,
            int startLineNumber,
            int maxSteps)
        {
            if (!sFlow.HasLine(startLineNumber))
            {
                if (!this._tools.Quiet) Console.WriteLine("WARNING: Construct_DynamicFlow2_Forward: startLine " + startLineNumber + " does not exist in " + sFlow);
                return;
            }
            var nextKeys = new Stack<string>();

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh. 
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            {
                string rootKey = sFlow.Get_Key(startLineNumber);
                nextKeys.Push(rootKey);
                this.Add_Vertex(rootKey, startLineNumber, 0);
            }
            #endregion

            while (nextKeys.Count > 0)
            {
                string prevKey = nextKeys.Pop();
                int step = this.Step(prevKey);
                if (step <= maxSteps)
                {
                    int currentLineNumber = this.LineNumber(prevKey);
                    if (sFlow.HasLine(currentLineNumber))
                    {
                        var nextLineNumber = sFlow.Get_Next_LineNumber(currentLineNumber);
                        (string nextKey, string nextKeyBranch) = sFlow.Get_Key(nextLineNumber);

                        var updates = Runner.Execute(sFlow, currentLineNumber, (prevKey, nextKey, nextKeyBranch), this._tools);
                        int nextStep = step + 1;

                        HandleBranch_LOCAL(currentLineNumber, nextLineNumber.Branch, nextStep, updates.Branch, prevKey, nextKeyBranch);
                        HandleRegular_LOCAL(currentLineNumber, nextLineNumber.Regular, nextStep, updates.Regular, prevKey, nextKey);
                    }
                }
            }
            #region Local Methods
            void HandleBranch_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        //Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a branch yet a branch is computed");
                        return;
                    }
                    if (!this.Has_Edge(prevKey, nextKey, true))
                    {
                        this.Add_Vertex(nextKey, nextLineNumber, nextStep);
                        this.Add_Edge(true, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!this._tools.Quiet) Console.WriteLine("=====================================");
                        if (!this._tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + sFlow.Get_Line_Str(currentLineNumber) + "\" Branches to LINE " + nextLineNumber);
                        if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        //if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + this.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    //Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a branch yet no branch is computed");
                    return;
                }
            }
            void HandleRegular_LOCAL(int currentLineNumber, int nextLineNumber, int nextStep, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a continue yet a continue is computed");
                        //throw new Exception();
                    }
                    if (!this.Has_Edge(prevKey, nextKey, false))
                    {
                        this.Add_Vertex(nextKey, nextLineNumber, nextStep);
                        this.Add_Edge(false, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!this._tools.Quiet) Console.WriteLine("=====================================");
                        if (!this._tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: stepNext " + nextStep + ": LINE " + currentLineNumber + ": \"" + sFlow.Get_Line_Str(currentLineNumber) + "\" Continues to LINE " + nextLineNumber);
                        if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        //if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + this.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a regular continue yet no continue is computed");
                    //throw new Exception();
                }
            }
            #endregion
        }

        private void Update_Backward(
            StaticFlow sFlow, 
            int startLineNumber,
            int maxSteps)
        {
            if (!sFlow.Has_Prev_LineNumber(startLineNumber))
            {
                if (!this._tools.Quiet) Console.WriteLine("WARNING: Construct_DynamicFlow_Backward: startLine " + startLineNumber + " does not exist in " + sFlow);
                return;
            }

            var prevKeys = new Stack<string>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            string rootKey = sFlow.Get_Key(startLineNumber);

                prevKeys.Push(rootKey);
                this.Add_Vertex(rootKey, startLineNumber, 0);
            #endregion

            while (prevKeys.Count > 0)
            {
                string nextKey = prevKeys.Pop();

                int step = this.Step(nextKey);
                if (step <= maxSteps)
                {
                    int nextStep = step + 1;
                    int currentLineNumber = this.LineNumber(nextKey);

                    foreach (var prev in sFlow.Get_Prev_LineNumber(currentLineNumber))
                    {
                        if (sFlow.HasLine(prev.LineNumber))
                        {
                            string prevKey = sFlow.Get_Key(prev.LineNumber);
                            if (!this.Has_Edge(prevKey, nextKey, prev.IsBranch))
                            {
                                var updates = Runner.Execute(sFlow, prev.LineNumber, (prevKey, nextKey, nextKey), this._tools);
                                var update = (prev.IsBranch) ? updates.Branch : updates.Regular;

                                    this.Add_Vertex(prevKey, prev.LineNumber, nextStep);
                                    this.Add_Edge(prev.IsBranch, update, prevKey, nextKey);
                                Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: scheduling key " + prevKey);
                                prevKeys.Push(prevKey); // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                                #region Display
                                if (!this._tools.Quiet) Console.WriteLine("=====================================");
                                if (!this._tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: stepNext " + nextStep + ": LINE " + prev.LineNumber + ": \"" + sFlow.Get_Line_Str(prev.LineNumber) + "; branch=" + prev.IsBranch);
                                if (!this._tools.Quiet && sFlow.Get_Line(prev.LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: " + update);
                                //if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + stateTree.State_After(rootKey));
                                #endregion
                            }
                        }
                    }
                }
            }
        }

        private void Add_Vertex(string key, int lineNumber, int step)
        {
            if (!this._graph.ContainsVertex(key))
            {
                this._graph.AddVertex(key);
                this._key_2_LineNumber_Step.Add(key, (lineNumber, step));
                if (this._lineNumber_2_Key.ContainsKey(lineNumber))
                {
                    this._lineNumber_2_Key[lineNumber].Add(key);
                }
                else
                {
                    this._lineNumber_2_Key.Add(lineNumber, new List<string> { key });
                }
            }
        }

        public void Add_Edge(bool isBranch, StateUpdate stateUpdate, string source, string target)
        { 
            if (this._graph.TryGetEdge(source, target, out var tag))
            {
                if (tag.Tag.Branch == isBranch)
                {
                    Console.WriteLine("WARNING: DynamicFlow.Add_Edge: edge " + source + "->" + target + " with branch=" + isBranch + " already exists");
                    throw new Exception();
                    return;
                }
            }
            Console.WriteLine("INFO: DynamicFlow.Add_Edge: adding edge " + source + "->" + target + " with branch=" + isBranch + ".");
            this._graph.AddEdge(new TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>(source, target, (isBranch, stateUpdate)));
        }

        #endregion

        #region ToString
        public override string ToString()
        {
            return this.ToString(null);
        }
        public string ToString(StaticFlow flow)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var k in this._key_2_LineNumber_Step)
            {
                sb.AppendLine("Key " + k.Key + " -> LineNumber " + k.Value.LineNumber + " Step " + k.Value.Step);
            }

            this.ToString(this._rootKey, flow, ref sb);
            return sb.ToString();
        }

        private void ToString(string key, StaticFlow sFlow, ref StringBuilder sb)
        {
            if (!this.Has_Vertex(key)) return;

            int lineNumber = this.LineNumber(key);
            string codeLine = (sFlow == null) ? "" : sFlow.Get_Line_Str(lineNumber);

            sb.AppendLine("==========================================");
            sb.AppendLine("State " + key + ": " + Construct_State_Private(key, true)?.ToString());

            foreach (var v in this._graph.OutEdges(key))
            {
                Debug.Assert(v.Source == key);
                string nextKey = v.Target;
                sb.AppendLine("------------------------------------------");
                sb.AppendLine("Transition from state " + key + " to " + nextKey + "; execute LINE " + lineNumber + ": \"" + codeLine + "\" " + ((v.Tag.Branch) ? "[Forward Branching]" : "[Forward Continue]"));
                ToString(nextKey, sFlow, ref sb);
            }
        }

        public string ToStringOverview(StaticFlow flow, bool showRegisterValues = false)
        {
            return "TODO";
        }
        #endregion

        #region Private Methods

        private State Construct_State_Private(string key1, bool after1)
        {
            State result;
            var alreadyVisisted = new HashSet<string>();
            return Construct_State_Private_LOCAL(key1, after1);

            #region Local Methods

            State Construct_State_Private_LOCAL(string key_LOCAL, bool after_LOCAL)
            {
                if (alreadyVisisted.Contains(key_LOCAL)) // found a cycle
                {
                    Console.WriteLine("WARNING: DynamicFlow: Construct_State_Private: Found cycle. not implemented yet.");
                    return new State(this._tools, key_LOCAL, key_LOCAL);
                }
                if (!this.Has_Vertex(key_LOCAL))
                {
                    Console.WriteLine("WARNING: DynamicFlow: Construct_State_Private: key " + key_LOCAL + " not found.");
                    return new State(this._tools, key_LOCAL, key_LOCAL);
                }

                alreadyVisisted.Add(key_LOCAL);

                switch (this._graph.InDegree(key_LOCAL))
                {
                    case 0:
                        result = new State(this._tools, key_LOCAL, key_LOCAL);
                        break;
                    case 1:
                        var edge = this._graph.InEdge(key_LOCAL, 0);
                        result = Construct_State_Private_LOCAL(edge.Source, false); // recursive call
                        result.Update_Forward(edge.Tag.StateUpdate);
                        break;
                    case 2:
                        var edge1 = this._graph.InEdge(key_LOCAL, 0);
                        var edge2 = this._graph.InEdge(key_LOCAL, 1);
                        result = Merge_State_Update_LOCAL(key_LOCAL, edge1.Source, edge1.Tag.StateUpdate, edge2.Source, edge2.Tag.StateUpdate);
                        break;
                    default:
                        Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private: inDegree = " + this._graph.InDegree(key_LOCAL) + " is not implemented yet");
                        result = new State(this._tools, key_LOCAL, key_LOCAL);
                        break;
                }
                if (after_LOCAL)
                {
                    switch (this._graph.OutDegree(key_LOCAL))
                    {
                        case 0:
                            break;
                        case 1:
                            var edge = this._graph.OutEdge(key_LOCAL, 0);
                            result.Update_Forward(edge.Tag.StateUpdate);
                            break;
                        case 2:
                            State state1 = new State(result);
                            State state2 = new State(result);
                            {
                                var edge1 = this._graph.OutEdge(key_LOCAL, 0);
                                state1.Update_Forward(edge1.Tag.StateUpdate);
                            }
                            {
                                var edge2 = this._graph.OutEdge(key_LOCAL, 1);
                                state2.Update_Forward(edge2.Tag.StateUpdate);
                            }
                            result = new State(state1, state2, true);
                            break;
                        default:
                            Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private: OutDegree = " + this._graph.OutDegree(key_LOCAL) + " is not implemented yet");
                            result = new State(this._tools, key_LOCAL, key_LOCAL);
                            break;
                    }
                }
                if (result == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private: Returning null!");
                }
                return result;
            }

            State Merge_State_Update_LOCAL(string target, string source1, StateUpdate update1, string source2, StateUpdate update2)
            {
                State state1 = Construct_State_Private_LOCAL(source1, false); // recursive call
                State state2 = Construct_State_Private_LOCAL(source2, false); // recursive call
                         
                StateUpdate mergeStateUpdate;
                {
                    string nextKey1 = target + "A";
                    update1.NextKey = nextKey1;
                    state1.Update_Forward(update1);

                    string nextKey2 = target + "B";
                    update2.NextKey = nextKey2;
                    state2.Update_Forward(update2);

                    string branchKey = GraphTools<(bool, StateUpdate)>.Get_Branch_Point(source1, source2, this._graph);
                    BranchInfo branchInfo = Get_Branch_Condition_LOCAL(branchKey);
                    if (branchInfo == null)
                    {
                        Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private:GetStates_LOCAL: branchInfo1 is null");
                        BoolExpr bc = this._tools.Ctx.MkBoolConst("BC!" + target);
                        mergeStateUpdate = new StateUpdate(bc, nextKey1, nextKey2, target, this._tools);
                    }
                    else
                    {
                        bool branch = false;
                        foreach (var v in state1.BranchInfoStore.Values)
                        {
                            if (v.BranchCondition == branchInfo.BranchCondition)
                            {
                                branch = v.BranchTaken;
                                break;
                            }
                        }
                        mergeStateUpdate = (branch)
                            ? new StateUpdate(branchInfo.BranchCondition, nextKey1, nextKey2, target, this._tools)
                            : new StateUpdate(branchInfo.BranchCondition, nextKey2, nextKey1, target, this._tools);
                    }
                }

                State state3 = new State(this._tools, state1.TailKey, state1.HeadKey);
                if (state1.TailKey != state2.TailKey)
                {
                    Console.WriteLine("WARNING: DynamicFlow: Merge_State_Update_LOCAL: tails are unequal: tail1=" + state1.TailKey + "; tail2=" + state2.TailKey);
                   // throw new Exception();
                }
                {   // merge the states state1 and state2 into state3 
                    {
                        var tempSet = new HashSet<BoolExpr>();
                        foreach (var v1 in state1.Solver.Assertions) tempSet.Add(v1);
                        foreach (var v1 in state2.Solver.Assertions) tempSet.Add(v1);
                        foreach (var v1 in tempSet) state3.Solver.Assert(v1);
                    }
                    {
                        var tempSet = new HashSet<BoolExpr>();
                        foreach (var v1 in state1.Solver_U.Assertions) tempSet.Add(v1);
                        foreach (var v1 in state2.Solver_U.Assertions) tempSet.Add(v1);
                        foreach (var v1 in tempSet) state3.Solver_U.Assert(v1);
                    }
                    var sharedBranchInfo = BranchInfoStore.RetrieveSharedBranchInfo(state1.BranchInfoStore, state2.BranchInfoStore, this._tools);
                    foreach (var branchInfo in sharedBranchInfo.MergedBranchInfo.Values) state3.Add(branchInfo);
                    state3.Update_Forward(mergeStateUpdate);
                }
                return state3;
            }

            BranchInfo Get_Branch_Condition_LOCAL(string branchKey)
            {
                if (branchKey == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: BranchKey is null;");
                    return null;
                }
                if (this._graph.OutDegree(branchKey) != 2)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: incorrect out degree;");
                    return null;
                }
                var edge1 = this._graph.OutEdge(branchKey, 0);
                var edge2 = this._graph.OutEdge(branchKey, 1);

                if (edge1.Tag.StateUpdate.BranchInfo == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: branchinfo of edge1 is null");
                    return null;
                }
                if (edge2.Tag.StateUpdate.BranchInfo == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: branchinfo of edge2 is null");
                    return null;
                }
                return edge1.Tag.StateUpdate.BranchInfo;
            }

            #endregion
        }

        #endregion
    }
}
