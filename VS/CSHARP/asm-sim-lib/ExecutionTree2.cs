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

using System.Collections.Generic;

namespace AsmSim
{
    public class ExecutionTree2
    {
        private ExecutionNode2 _root;
        private readonly Tools _tools;
        public bool Forward { get; private set; }

        public ExecutionTree2(ExecutionNode2 rootNode, bool forward, Tools tools)
        {
            this._root = rootNode;
            this._tools = tools;
            this.Forward = forward;
        }
        private ExecutionNode2 Root { get { return this._root; } }

        public IEnumerable<State> States(int lineNumber)
        {
            foreach (ExecutionNode2 node in GetExecutionNode_LOCAL(this._root))
            {
                yield return this.GetState(node);
            }
            IEnumerable<ExecutionNode2> GetExecutionNode_LOCAL(ExecutionNode2 startNode)
            {
                if (startNode.LineNumber == lineNumber)
                {
                    yield return startNode;
                }
                else
                {
                    if (this.Forward)
                    {
                        if (startNode.Has_Forward_Continue)
                        {
                            foreach (var x in GetExecutionNode_LOCAL(startNode.Forward_Continue)) yield return x;
                        }
                        if (startNode.Has_Forward_Branch)
                        {
                            foreach (var x in GetExecutionNode_LOCAL(startNode.Forward_Branch)) yield return x;
                        }
                    }
                    else
                    {
                        if (startNode.Has_Backward)
                        {
                            foreach (var y in startNode.Backward) foreach (var x in GetExecutionNode_LOCAL(y)) yield return x;
                        }
                    }
                }
            }
        }

        public IEnumerable<State> Leafs
        {
            get
            {
                foreach (ExecutionNode2 node in GetExecutionNode_LOCAL(this._root))
                {
                    yield return this.GetState(node);
                }
                IEnumerable<ExecutionNode2> GetExecutionNode_LOCAL(ExecutionNode2 startNode)
                {
                    if (!startNode.Has_Forward_Continue && !startNode.Has_Forward_Branch)
                    {
                        yield return startNode; // found a leaf
                    }
                    else
                    {
                        if (startNode.Has_Forward_Continue)
                        {
                            foreach (var x in GetExecutionNode_LOCAL(startNode.Forward_Continue)) yield return x;
                        }
                        if (startNode.Has_Forward_Branch)
                        {
                            foreach (var x in GetExecutionNode_LOCAL(startNode.Forward_Branch)) yield return x;
                        }
                    }
                }
            }
        }

        public State EndState { get { return Tools.Collapse(this.Leafs); } }

        #region ToString
        public override string ToString()
        {
            return this.ToString(null);
        }
        public string ToString(CFlow flow)
        {
            return this.Root.ToString(flow);
        }
        public string ToStringOverview(CFlow flow, bool showRegisterValues = false)
        {
            return this.Root.ToStringOverview(flow, 1, showRegisterValues);
        }
        #endregion

        #region Private Methods
        private State GetState(ExecutionNode2 node)
        {
            if (node == null) return null;

            string key = "!0";
            State result = new State(this._tools, key, key, -1);

            if (this.Forward)
            {
                string headKey = node.StateUpdate.NextKey;
                string tailKey = node.StateUpdate.PrevKey;
                while (node != null)
                {
                    result.Update(node.StateUpdate);
                    tailKey = node.StateUpdate.PrevKey;
                    node = node.Parent;
                }
                result.TailKey = tailKey;
                result.HeadKey = headKey;
            }
            else
            {
                string headKey = node.StateUpdate.NextKey;
                string tailKey = node.StateUpdate.PrevKey;
                while (node != null)
                {
                    result.Update(node.StateUpdate);
                    headKey = node.StateUpdate.NextKey;
                    bug here: node should be one of the node.Backward 

                    node = node.Parent;
                }
                result.TailKey = tailKey;
                result.HeadKey = headKey;
            }
            return result;
        }
        #endregion
    }
}
