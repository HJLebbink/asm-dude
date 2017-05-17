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
using System.Text;

namespace AsmSim
{
    public class ExecutionTree
    {
        private ExecutionNode _root;
        private readonly Tools _tools;
        public bool Forward { get; private set; }

        public ExecutionTree(ExecutionNode rootNode, bool forward, Tools tools)
        {
            this._root = rootNode;
            this._tools = tools;
            this.Forward = forward;
        }
        public ExecutionNode Root { get { return this._root; } }

        public IEnumerable<State> States_Before(int lineNumber)
        {
            foreach (ExecutionNode node in GetNode_LOCAL(this._root))
            {
                


                State state = Tools.Collapse(this.GetPreviousStates(node));
                if (state != null) yield return state;
            }
            IEnumerable<ExecutionNode> GetNode_LOCAL(ExecutionNode startNode)
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
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Continue)) yield return x;
                        }
                        if (startNode.Has_Forward_Branch)
                        {
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Branch)) yield return x;
                        }
                    }
                    else
                    {
                        if (startNode.Has_Parents)
                        {
                            foreach (var y in startNode.Parents) foreach (var x in GetNode_LOCAL(y)) yield return x;
                        }
                    }
                }
            }
        }

        public IEnumerable<State> States_After(int lineNumber)
        {
            foreach (ExecutionNode node in GetNode_LOCAL(this._root))
            {
                yield return this.GetState(node);
            }
            IEnumerable<ExecutionNode> GetNode_LOCAL(ExecutionNode startNode)
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
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Continue)) yield return x;
                        }
                        if (startNode.Has_Forward_Branch)
                        {
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Branch)) yield return x;
                        }
                    }
                    else
                    {
                        if (startNode.Has_Parents)
                        {
                            foreach (var y in startNode.Parents) foreach (var x in GetNode_LOCAL(y)) yield return x;
                        }
                    }
                }
            }
        }

        public IEnumerable<State> Leafs
        {
            get
            {
                foreach (ExecutionNode node in GetNode_LOCAL(this._root))
                {
                    yield return this.GetState(node);
                }
                IEnumerable<ExecutionNode> GetNode_LOCAL(ExecutionNode startNode)
                {
                    if (!startNode.Has_Forward_Continue && !startNode.Has_Forward_Branch)
                    {
                        yield return startNode; // found a leaf
                    }
                    else
                    {
                        if (startNode.Has_Forward_Continue)
                        {
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Continue)) yield return x;
                        }
                        if (startNode.Has_Forward_Branch)
                        {
                            foreach (var x in GetNode_LOCAL(startNode.Forward_Branch)) yield return x;
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
            StringBuilder sb = new StringBuilder();
            this.ToString(this.Root, flow, ref sb);
            return sb.ToString();
        }

        private void ToString(ExecutionNode node, CFlow flow, ref StringBuilder sb)
        {
            sb.Append(node.ToString(flow));
            if (this.Forward)
            {
                if (node.Has_Forward_Continue)
                {
                    sb.AppendLine("Forward Regular Continue:");
                    ToString(node.Forward_Continue, flow, ref sb);
                }
                if (node.Has_Forward_Branch)
                {
                    sb.AppendLine("Forward Branching:");
                    ToString(node.Forward_Branch, flow, ref sb);
                }
            }
            else
            {
                if (node.Has_Parents)
                {
                    foreach (var n in node.Parents)
                    {
                        ToString(n, flow, ref sb);
                    }
                }
            }
        }

        public string ToStringOverview(CFlow flow, bool showRegisterValues = false)
        {
            return this.Root.ToStringOverview(flow, 1, showRegisterValues);
        }
        #endregion

        #region Private Methods

        private IEnumerable<State> GetPreviousStates(ExecutionNode node)
        {
            if (node.Has_Parents)
            {
                foreach (var v in node.Parents)
                {
                    yield return GetState(v);
                }
            } 
        }

        private State GetState(ExecutionNode node)
        {
            if (node == null) return null;

            string key = "!DUMMY";
            State result = new State(this._tools, key, key, -1);

            if (this.Forward)
            {
                string headKey = node.NextKey;
                string tailKey = node.PrevKey;
                while (node != null)
                {
                    if (node.StateUpdate != null)
                    {
                        result.Update(node.StateUpdate);
                        tailKey = node.StateUpdate.PrevKey;
                    }
                    if (node.Has_Parents)
                    {
                        if (node.Parents.Count != 1) Console.WriteLine("WARNING: GetState: cannot have multiple parents");
                        node = node.Parents[0];
                    } else
                    {
                        node = null;
                    }
                }
                result.TailKey = tailKey;
                result.HeadKey = headKey;
            }
            else
            {
                string headKey = node.NextKey;
                string tailKey = node.PrevKey;
                while (node != null)
                {
                    //Console.WriteLine("INFO: ExecutionTree:GetState: Adding update " + node.StateUpdate);
                    if (node.StateUpdate != null)
                    {
                        result.Update(node.StateUpdate);
                        headKey = node.StateUpdate.NextKey;
                    }
                    if (node.Has_Parents)
                    {
                        if (node.Parents.Count != 1) Console.WriteLine("WARNING: GetState: cannot have multiple parents");
                        node = node.Parents[0];
                    }
                    else
                    {
                        node = null;
                    }
                }
                result.TailKey = tailKey;
                result.HeadKey = headKey;
            }
            return result;
        }
        #endregion
    }
}
