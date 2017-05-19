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

namespace AsmSim
{
    public class ExecutionTree
    {
        #region Fields

        private readonly Tools _tools;
        public bool Forward { get; private set; }

        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> _graph;
        private readonly IDictionary<int, IList<string>> _lineNumber_2_Key;
        private readonly IDictionary<string, (int LineNumber, int Step)> _key_2_LineNumber_Step;
        private readonly string _rootKey;

        #endregion 

        #region Constructors
        public ExecutionTree(string rootKey, bool forward, Tools tools)
        {
            this._rootKey = rootKey;
            this._tools = tools;
            this.Forward = forward;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>>(false);
            this._lineNumber_2_Key = new Dictionary<int, IList<string>>();
            this._key_2_LineNumber_Step = new Dictionary<string, (int LineNumber, int Step)>();
        }
        #endregion

        #region Getters

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
                foreach (string key in keys) yield return Get_State_Private(key, false);
            }
        }

        public IEnumerable<State> States_After(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out IList<string> keys))
            {
                foreach (string key in keys) yield return Get_State_Private(key, true);
            }
        }

        public State State_After(string key)
        {
            if (!this._graph.ContainsVertex(key)) return null;
            return Get_State_Private(key, true);
        }

        public State State_Before(string key)
        {
            if (!this._graph.ContainsVertex(key)) return null;
            return Get_State_Private(key, false);
        }

        public IEnumerable<State> Leafs
        {
            get
            {
                if (this.Forward)
                    foreach (string key in Get_Leafs_Forward_LOCAL(this._rootKey))
                        yield return this.Get_State_Private(key, true);
                else
                    foreach (string key in Get_Leafs_Backward_LOCAL(this._rootKey))
                        yield return this.Get_State_Private(key, true);

                IEnumerable<string> Get_Leafs_Forward_LOCAL(string key)
                {
                    if (this._graph.IsOutEdgesEmpty(key))
                        yield return key;
                    else
                        foreach (var edge in this._graph.OutEdges(key))
                            foreach (string v in Get_Leafs_Forward_LOCAL(edge.Target))
                                yield return v;
                }
                IEnumerable<string> Get_Leafs_Backward_LOCAL(string key)
                {
                    if (this._graph.IsInEdgesEmpty(key))
                        yield return key;
                    else
                        foreach (var edge in this._graph.InEdges(key))
                            foreach (string v in Get_Leafs_Backward_LOCAL(edge.Source))
                                yield return v;
                }
            }
        }
    
        public State EndState { get { return Tools.Collapse(this.Leafs); } }

        #endregion

        #region Setters

        public void Add_Vertex(string key, int lineNumber, int step)
        {
            if (this._graph.ContainsVertex(key))
            {
                Console.WriteLine("WARNING: ExecutionTree: Add_Vertex: key " + key + " already exists");
            }
            else
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

        public void Add_Edge(bool isBranch, StateUpdate stateUpdate)
        {
            string prevKey = stateUpdate.PrevKey;
            string nextKey = stateUpdate.NextKey;
            this._graph.AddEdge(new TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>(prevKey, nextKey, (isBranch, stateUpdate)));
        }

        #endregion

        #region ToString
        public override string ToString()
        {
            return this.ToString(null);
        }
        public string ToString(CFlow flow)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var k in this._key_2_LineNumber_Step)
            {
                sb.AppendLine("Key " + k.Key + " -> LineNumber " + k.Value.LineNumber + " Step " + k.Value.Step);
            }

            this.ToString(this._rootKey, flow, ref sb);
            return sb.ToString();
        }

        private void ToString(string key, CFlow flow, ref StringBuilder sb)
        {
            int lineNumber = this.LineNumber(key);
            string codeLine = (flow == null) ? "" : flow.GetLineStr(lineNumber);

            if (this.Forward)
            {
                sb.AppendLine("==========================================");
                sb.AppendLine("State " + key + ": " + Get_State_Forward_Private(key, true).ToString());

                foreach (var v in this._graph.OutEdges(key))
                {
                    Debug.Assert(v.Source == key);
                    string nextKey = v.Target;
                    sb.AppendLine("------------------------------------------");
                    sb.AppendLine("Transition from state " + key + " to " + nextKey + "; execute LINE " + lineNumber + ": \"" + codeLine + "\" " + ((v.Tag.Branch) ? "[Forward Branching]" : "[Forward Continue]"));
                    ToString(nextKey, flow, ref sb);
                }
            }
            else
            {
                sb.AppendLine("==========================================");
                sb.AppendLine("State " + key + ": " + Get_State_Backward_Private(key, true).ToString());
                foreach (var v in this._graph.InEdges(key))
                {
                    Debug.Assert(v.Target == key);
                    string prevKey = v.Source;
                    sb.AppendLine("------------------------------------------");
                    sb.AppendLine("Transition from state " + prevKey + " to " + key + "; execute LINE " + lineNumber + ": \"" + codeLine + "\" " + ((v.Tag.Branch) ? "[Backward Branching] " : "[Backward Continue]"));
                    ToString(prevKey, flow, ref sb);
                }
            }
        }

        public string ToStringOverview(CFlow flow, bool showRegisterValues = false)
        {
            return "TODO";
        }
        #endregion

        #region Private Methods

        private State Get_State_Private(string key, bool after)
        {
            return (this.Forward) ? Get_State_Forward_Private(key, after) : Get_State_Backward_Private(key, after);
        }

        private State Get_State_Forward_Private(string key, bool after)
        {
            State result = (this._graph.IsInEdgesEmpty(key))
                ? new State(this._tools, key, key)
                : Tools.Collapse(GetStates_LOCAL());

            if (after)
            {
                int outDegree = this._graph.OutDegree(key);
                if (outDegree == 2)
                {
                    throw new Exception("NOt implemented yet");
                } else if (outDegree == 1)
                {
                    result.Update_Forward(this._graph.OutEdge(key, 0).Tag.StateUpdate);
                }
            }
            return result;

            IEnumerable<State> GetStates_LOCAL()
            {
                foreach (var edge in this._graph.InEdges(key))
                {
                    State s = Get_State_Forward_Private(edge.Source, false);
                    s.Update_Forward(edge.Tag.StateUpdate);
                    yield return s;
                }
            }
        }

        private State Get_State_Backward_OLD_Private(string key, bool after)
        {
            State result = (this._graph.IsInEdgesEmpty(key))
                ? new State(this._tools, key, key)
                : Tools.Collapse(GetStates_LOCAL());

            if (after)
            {
                int outDegree = this._graph.OutDegree(key);
                if (outDegree == 2)
                {
                    throw new Exception("NOt implemented yet");
                }
                else if (outDegree == 1)
                {
                    result.Update_Forward(this._graph.OutEdge(key, 0).Tag.StateUpdate);
                }
            }
            return result;

            IEnumerable<State> GetStates_LOCAL()
            {
                foreach (var edge in this._graph.InEdges(key))
                {
                    State s = Get_State_Backward_OLD_Private(edge.Source, false);
                    s.Update_Forward(edge.Tag.StateUpdate);
                    yield return s;
                }
            }
        }

        private State Get_State_Backward_Private(string key, bool after)
        {
            int inDegree = this._graph.InDegree(key);
            State result = null;
            if (inDegree == 0)
            {
                result = new State(this._tools, key, key);
            }
            else if (inDegree == 1)
            {
                var edge = this._graph.InEdge(key, 0);
                result = Get_State_Backward_Private(edge.Source, false);
                result.Update_Forward(edge.Tag.StateUpdate);
            } 
            else if (inDegree == 2)
            {
                string nextKey;
                State result1;
                {
                    var edge = this._graph.InEdge(key, 0);
                    StateUpdate update = edge.Tag.StateUpdate;
                    nextKey = update.NextKey;
                    update.NextKey = nextKey + "A";
                    result1 = Get_State_Backward_Private(edge.Source, false);
                    result1.Update_Forward(update);
                }
                State result2;
                {
                    var edge = this._graph.InEdge(key, 1);
                    StateUpdate update = edge.Tag.StateUpdate;
                    Debug.Assert(nextKey == update.NextKey);
                    update.NextKey = nextKey + "B";
                    result2 = Get_State_Backward_Private(edge.Source, false);
                    result2.Update_Forward(update);
                }
                result = new State(result1, result2, true);
            }
            else
            {
                throw new Exception();
            }

            if (after)
            {
                int outDegree = this._graph.OutDegree(key);
                if (outDegree == 2)
                {
                    throw new Exception("NOt implemented yet");
                }
                else if (outDegree == 1)
                {
                    result.Update_Forward(this._graph.OutEdge(key, 0).Tag.StateUpdate);
                }
            }
            return result;
        }

        #endregion
    }
}
