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

using AsmTools;
using Microsoft.Z3;
using QuickGraph;
using System;
using System.Collections.Generic;

namespace AsmSim
{
    public class ExecutionTree : IDisposable
    {
        #region Fields
        private readonly Tools _tools;
        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> _graph;
        private readonly IDictionary<int, IList<string>> _lineNumber_2_Key;
        private readonly IDictionary<string, int> _key_2_LineNumber;
        private int _target_LineNumber;

        /// <summary>
        /// The states for _target_LineNumber computed upto the key in the graph
        /// </summary>
        private readonly IDictionary<string, State> _states;

        private string _rootKey;
        private object _updateLock = new object();
        #endregion

        #region Constructors

        public ExecutionTree(Tools tools)
        {
            this._tools = tools;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>>(true);
            this._lineNumber_2_Key = new Dictionary<int, IList<string>>();
            this._key_2_LineNumber = new Dictionary<string, int>();
            this._states = new Dictionary<string, State>();
        }

        #endregion

        #region Getters

        public int LineNumber(string key)
        {
            return (this._key_2_LineNumber.TryGetValue(key, out var v)) ? v : -1;
        }

        private bool Has_Edge(string source, string target, bool isBranch)
        {
            return (this._graph.TryGetEdge(source, target, out var tag)) ? (tag.Tag.Branch == isBranch) : false;
        }

        #endregion

        #region Setters


        /// <summary> Reset this ExecutionTree to the provided sFlow. The resulting execution tree will compute the lineNumber's state</summary>
        public void Reset(DynamicFlow dFlow, int targetLineNumber)
        {
            this._target_LineNumber = targetLineNumber;
            lock (this._updateLock)
            {
                this._rootKey = "!" + targetLineNumber;
                this.Clear();
                this.Update_Backward(dFlow, targetLineNumber, 20);
            }
        }

        private string Get_Key(int lineNumber, DynamicFlow dFlow)
        {
            if (true)
            {
                return dFlow.Get_Key(lineNumber) + "-"+Tools.CreateKey(this._tools.Rand);
            } 
            else
            {
                return dFlow.Get_Key(lineNumber);
            }
        }

        private void Update_Backward(DynamicFlow dFlow, int startLineNumber, int maxSteps)
        {
            var prevKeys = new Stack<(string Key, int Step)>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            string rootKey = this.Get_Key(startLineNumber, dFlow);
            prevKeys.Push((Key:rootKey, Step:20));
            this.Add_Vertex(rootKey, startLineNumber);
            #endregion

            while (prevKeys.Count > 0)
            {
                (string prevKey, int prevStep) = prevKeys.Pop();
                if (prevStep > 0)
                {
                    #region Create the state 
                    State currentState = null;
                    var incoming = new List<StateUpdate>(dFlow.Get_Incoming_StateUpdate(prevKey));
                    switch (incoming.Count)
                    {
                        case 0:
                            currentState = new State(this._tools, prevKey, prevKey);
                            break;
                        case 1:
                            currentState = this._states[prevKey];
                            currentState.Update_Backward(incoming[0], prevKey);
                            break;
                        default:
                            currentState = this._states[prevKey];
                            //TODO create an StateUpdate by merging the incoming stateUpdates.
                            StateUpdate mergedStateUpdate = null;
                            currentState.Update_Backward(mergedStateUpdate, prevKey);
                            break;
                    }

                    this._states.Add()
                    #endregion

                    #region Add the previous states to the stack 
                    foreach (string prevKey in dFlow.Get_Incoming_Key(prevKey))
                    {

                            var updates = Runner.Execute(sFlow, prev.LineNumber, (prevKey, prevKey, prevKey), this._tools);
                            StateUpdate update = (prev.IsBranch) ? updates.Branch : updates.Regular;
                            ((prev.IsBranch) ? updates.Regular : updates.Branch)?.Dispose();

                            this.Add_Vertex(prevKey, prev.LineNumber);
                            this.Add_Edge(prev.IsBranch, update, prevKey, prevKey);

                            //Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: scheduling key " + prevKey);
                            prevKeys.Push((Key: prevKey, Step: prevStep + 1)); // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                            #region Display
                            if (!this._tools.Quiet) Console.WriteLine("=====================================");
                            if (!this._tools.Quiet) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: LINE " + prev.LineNumber + ": \"" + sFlow.Get_Line_Str(prev.LineNumber) + "; branch=" + prev.IsBranch);
                            if (!this._tools.Quiet && sFlow.Get_Line(prev.LineNumber).Mnemonic != Mnemonic.NONE) Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: " + update);
                            //if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.NONE) Console.WriteLine("INFO: " + stateTree.State_After(rootKey));
                            #endregion
                        
                    }
                    #endregion
                }
            }
        }

        private void Add_Vertex(string key, int lineNumber)
        {
            if (!this._graph.ContainsVertex(key))
            {
                this._graph.AddVertex(key);
                this._key_2_LineNumber.Add(key, lineNumber);
            }
        }

        private void Add_Edge(bool isBranch, StateUpdate stateUpdate, string source, string target)
        {
            if (this._graph.TryGetEdge(source, target, out var tag))
            {
                if (tag.Tag.Branch == isBranch)
                {
                    Console.WriteLine("WARNING: DynamicFlow.Add_Edge: edge " + source + "->" + target + " with branch=" + isBranch + " already exists");
                    return;
                }
            }
            //Console.WriteLine("INFO: DynamicFlow.Add_Edge: adding edge " + source + "->" + target + " with branch=" + isBranch + ".");
            this._graph.AddEdge(new TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>(source, target, (isBranch, stateUpdate)));
        }

        #endregion

        public void Clear()
        {
            foreach (var v in this._graph.Edges) v.Tag.StateUpdate.Dispose();
            this._graph.Clear();
            this._lineNumber_2_Key.Clear();
            this._key_2_LineNumber.Clear();
        }

        #region IDisposable Support
        public void Dispose()
        {
            
        }
        #endregion
    }
}
