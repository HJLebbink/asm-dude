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
using System.Collections.Generic;

namespace AsmSim
{
    public class ExecutionTree
    {
        private ExecutionNode _root;
        private readonly Tools _tools;
        public bool Forward { get; private set; }

        public ExecutionTree(ExecutionNode rootNode, bool forward)
        {
            this._root = rootNode;
            this._tools = rootNode.State.Tools;
            this.Forward = forward;
        }
        public ExecutionNode Root { get { return this._root; } }
        public IEnumerable<State> GetFromLine(int lineNumber)
        {
            return this._root.GetFromLine(lineNumber);
        }
        public IEnumerable<State> Leafs { get { return (this.Forward) ? this._root.Leafs_Forward : this._root.Leafs_Backward; } }
        public State EndState { get { return Tools.Collapse(this.Leafs); } }

        Tv GetTv(Flags flagName, int lineNumber)
        {
            return Tools.Collapse(this.GetFromLine(lineNumber)).GetTv5(flagName);
        }
        Tv[] GetTv5Array(Rn regName, int lineNumber)
        {
            return Tools.Collapse(this.GetFromLine(lineNumber)).GetTv5Array(regName);
        }

        public override string ToString()
        {
            return this.ToString(null);
        }
        public string ToString(CFlow flow)
        {
            return this._root.ToString(flow);
        }
        public string ToStringOverview(CFlow flow, bool showRegisterValues = false)
        {
            return this.Root.ToStringOverview(flow, 1, showRegisterValues);
        }
    }
}
