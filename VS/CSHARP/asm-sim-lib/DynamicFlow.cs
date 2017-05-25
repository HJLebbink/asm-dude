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

namespace AsmSim
{
    public class DynamicFlow
    {
        #region Fields

        private readonly Tools _tools;
        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> _graph;
        private readonly IDictionary<int, IList<string>> _lineNumber_2_Key;
        private readonly IDictionary<string, (int LineNumber, int Step)> _key_2_LineNumber_Step;
        private readonly string _rootKey;
        public BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> Graph { get { return this._graph; } }
        #endregion 

        #region Constructors
        public DynamicFlow(string rootKey, Tools tools)
        {
            this._rootKey = rootKey;
            this._tools = tools;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>>(true);
            this._lineNumber_2_Key = new Dictionary<int, IList<string>>();
            this._key_2_LineNumber_Step = new Dictionary<string, (int LineNumber, int Step)>();
        }
        #endregion

        #region Getters

        public bool Has_Vertex(string key)
        {
            return this._graph.ContainsVertex(key);
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

        public IEnumerable<State> Leafs
        {
            get
            {
                var alreadyVisisted = new HashSet<string>();
                foreach (string key in Get_Leafs_Forward_LOCAL(this._rootKey))
                {
                    yield return this.Construct_State_Private(key, true);
                }

                #region Local Methods
                IEnumerable<string> Get_Leafs_Forward_LOCAL(string key)
                {
                    if (alreadyVisisted.Contains(key)) yield break;
                    if (this._graph.IsOutEdgesEmpty(key))
                    {
                        alreadyVisisted.Add(key);
                        yield return key;
                    }
                    else
                        foreach (var edge in this._graph.OutEdges(key))
                            foreach (string v in Get_Leafs_Forward_LOCAL(edge.Target))
                                yield return v;
                }
                #endregion
            }
        }
    
        public State EndState { get { return Tools.Collapse(this.Leafs); } }

        #endregion

        #region Setters

        public void Add_Vertex(string key, int lineNumber, int step)
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

        private void ToString(string key, StaticFlow flow, ref StringBuilder sb)
        {
            int lineNumber = this.LineNumber(key);
            string codeLine = (flow == null) ? "" : flow.Get_Line_Str(lineNumber);

            sb.AppendLine("==========================================");
            sb.AppendLine("State " + key + ": " + Construct_State_Private(key, true).ToString());

            foreach (var v in this._graph.OutEdges(key))
            {
                Debug.Assert(v.Source == key);
                string nextKey = v.Target;
                sb.AppendLine("------------------------------------------");
                sb.AppendLine("Transition from state " + key + " to " + nextKey + "; execute LINE " + lineNumber + ": \"" + codeLine + "\" " + ((v.Tag.Branch) ? "[Forward Branching]" : "[Forward Continue]"));
                ToString(nextKey, flow, ref sb);
            }
        }

        public string ToStringOverview(StaticFlow flow, bool showRegisterValues = false)
        {
            return "TODO";
        }
        #endregion

        #region Private Methods

        private State Construct_State_Private(string key, bool after)
        {
            State result;

            switch (this._graph.InDegree(key))
            {
                case 0:
                    result = new State(this._tools, key, key);
                    break;
                case 1:
                    var edge = this._graph.InEdge(key, 0);
                    result = Construct_State_Private(edge.Source, false); // recursive call
                    result.Update_Forward(edge.Tag.StateUpdate);
                    break;
                case 2:
                    var edge1 = this._graph.InEdge(key, 0);
                    var edge2 = this._graph.InEdge(key, 1);
                    result = Merge_State_Update_LOCAL(key, edge1.Source, edge1.Tag.StateUpdate, edge2.Source, edge2.Tag.StateUpdate);
                    break;
                default:
                    throw new Exception("Not implemented yet");
                    result = Tools.Collapse(GetStates_LOCAL());
                    break;
            }

            if (after)
            {
                switch (this._graph.OutDegree(key))
                {
                    case 0:
                        break;
                    case 1:
                        var edge = this._graph.OutEdge(key, 0);
                        result.Update_Forward(edge.Tag.StateUpdate);
                        break;
                    default:
                        throw new Exception("NOt implemented yet");
                }
            }
            return result;

            #region Local Methods
            IEnumerable<State> GetStates_LOCAL()
            {
                foreach (var edge in this._graph.InEdges(key))
                {
                    State s = Construct_State_Private(edge.Source, false);
                    s.Update_Forward(edge.Tag.StateUpdate);
                    yield return s;
                }
            }

            State Merge_State_Update_LOCAL(string target, string source1, StateUpdate update1, string source2, StateUpdate update2)
            {
                State state1 = this.Construct_State_Private(source1, false); // recursive call
                State state2 = this.Construct_State_Private(source2, false); // recursive call
                         
                StateUpdate mergeStateUpdate;
                {
                    string nextKey1 = target + "A";
                    update1.NextKey = nextKey1;
                    state1.Update_Forward(update1);

                    string nextKey2 = target + "B";
                    update2.NextKey = nextKey2;
                    state2.Update_Forward(update2);

                    string branchKey = GraphTools<(bool, StateUpdate)>.Get_Branch_Point(source1, source2, this._graph);
                    Console.WriteLine("INFO: DynamicFlow:Construct_State_Private:GetStates_LOCAL: retrieved branchKey " + branchKey);
                    BranchInfo branchInfo1 = Get_Branch_Condition_LOCAL(branchKey, source1);
                    BoolExpr bc = branchInfo1.BranchCondition;

                    mergeStateUpdate = (branchInfo1.BranchTaken)
                        ? new StateUpdate(bc, nextKey2, nextKey1, target, this._tools)
                        : new StateUpdate(bc, nextKey1, nextKey2, target, this._tools);
                }

                State state3 = new State(this._tools, state1.TailKey, state1.HeadKey);
                if (state1.TailKey != state2.TailKey) throw new Exception();

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

            BranchInfo Get_Branch_Condition_LOCAL(string branchKey, string source1)
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

                //if (edge2.)

                return edge1.Tag.StateUpdate.BranchInfo;
            }

            #endregion
        }

        #endregion
    }
}
