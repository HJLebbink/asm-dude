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
    public class ExecutionNode
    {
        public int Step { get; set; }
        public State State { get; private set; }

        private readonly ExecutionNode _parent;
        private ExecutionNode _forward_Continue;
        private ExecutionNode _forward_Branch;
        private IList<ExecutionNode> _backward;

        public bool LoopTerminationNode = false;


        public ExecutionNode(int step, State state, ExecutionNode parent)
        {
            this.Step = step;
            this.State = state;
            this._parent = parent;
        }

        public IEnumerable<State> GetFromLine(int lineNumber)
        {
            if (this.State.LineNumber == lineNumber)
            {
                yield return this.State;
            }
            if (this.Has_Forward_Continue)
            {
                foreach (var x in this.Forward_Continue.GetFromLine(lineNumber)) yield return x;
            }
            if (this.Has_Forward_Branch)
            {
                foreach (var x in this.Forward_Branch.GetFromLine(lineNumber)) yield return x;
            }
        }

        public IEnumerable<State> Leafs_Forward
        {
            get
            {
                if (!this.Has_Forward_Branch && !this.Has_Forward_Continue)
                {
                    if (this.State.IsConsistent)
                    {
                        yield return this.State;
                    }
                    else yield break;
                }
                else
                {
                    if (this.Has_Forward_Continue)
                    {
                        foreach (var e in this.Forward_Continue.Leafs_Forward) yield return e;
                    }
                    if (this.Has_Forward_Branch)
                    {
                        foreach (var e in this.Forward_Branch.Leafs_Forward) yield return e;
                    }
                }
            }
        }

        public IEnumerable<State> Leafs_Backward
        {
            get
            {
                if (!this.Has_Backward)
                {
                    if (this.State.IsConsistent)
                    {
                        yield return this.State;
                    }
                    else yield break;
                }
                else
                {
                    foreach (var b in this.Backward) foreach (var e in b.Leafs_Backward) yield return e;
                }
            }
        }

        public bool HasParent { get { return this._parent != null; } }
        public ExecutionNode Parent
        {
            get { return this.Parent; }
        }

        public bool Has_Forward_Continue { get { return (this._forward_Continue != null); } }
        public bool Has_Forward_Branch { get { return (this._forward_Branch != null); } }
        public bool Has_Backward { get { return (this._backward != null); } }


        /// <summary>Regular Control Flow node</summary>
        public ExecutionNode Forward_Continue
        {
            get { return this._forward_Continue; }
            set
            {
                if (this._forward_Continue != null) throw new Exception();
                this._forward_Continue = value;
            }
        }

        /// <summary>Branch Control Flow node</summary>
        public ExecutionNode Forward_Branch
        {
            get { return this._forward_Branch; }
            set
            {
                if (this._forward_Branch != null) throw new Exception();
                this._forward_Branch = value;
            }
        }

        public IList<ExecutionNode> Backward
        {
            get { return this._backward; }
        }

        public void Add_Backward(ExecutionNode node)
        {
            if (this._backward == null) this._backward = new List<ExecutionNode>(0);
            this._backward.Add(node);
        }

        #region ToString
        public override string ToString()
        {
            return this.ToString(null);
        }
        public string ToString(CFlow flow)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==========================================");

            if (this.State.Warning != null)
            {
                sb.AppendLine(this.State.Warning);
            }
            if (this.State.IsHalted)
            {
                sb.AppendLine("Halted at Step " + this.Step + ", Line " + (this.State.LineNumber + 1) + ": " + flow.GetLineStr(this.State.LineNumber) + "\n" + this.State.ToString());
                sb.AppendLine("Halt message " + this.State.SyntaxError);
            }
            else
            {
                if (this.State.LineNumber >= 0)
                {
                    sb.AppendLine("Step " + this.Step + ", Line " + (this.State.LineNumber + 1) + ": " + flow.GetLineStr(this.State.LineNumber) + "\n" + this.State.ToString());
                }
                else
                {
                    sb.AppendLine("Step " + this.Step + ":" + this.State.ToString());
                }
                if (this.Has_Forward_Continue)
                {
                    sb.AppendLine(this.Forward_Continue.ToString(flow));
                }
                if (this.Has_Forward_Branch)
                {
                    sb.AppendLine("Entering Branch: Step " + this.Step + ", Line " + (this.State.LineNumber + 1) + ": " + flow.GetLineStr(this.State.LineNumber));
                    sb.AppendLine(this.Forward_Branch.ToString(flow));
                }
            }
            return sb.ToString();
        }

        public string ToStringOverview(CFlow flow, int depth, bool showRegisterValues = false)
        {
            String identStr = "";
            for (int i = 0; i < depth; ++i) identStr += "  ";

            StringBuilder sb = new StringBuilder();

            if (showRegisterValues)
            {
                sb.Append(this.State.ToStringFlags(identStr + "* "));
                sb.Append(this.State.ToStringRegs(identStr + "* "));
                sb.Append(this.State.ToStringConstraints(identStr + "* "));
            }
            int lineNumber = this.State.LineNumber;
            sb.AppendLine(identStr + flow.GetLineStr(lineNumber) + " [lineNumber=" + lineNumber + "]");

            if (this.Has_Forward_Continue)
            {
                sb.Append(identStr);
                sb.AppendLine("[continue:]");
                sb.Append(this.Forward_Continue.ToStringOverview(flow, depth + 1, showRegisterValues));
            }
            if (this.Has_Forward_Branch)
            {
                sb.Append(identStr);
                sb.AppendLine("[branch:]");
                sb.Append(this.Forward_Branch.ToStringOverview(flow, depth + 1, showRegisterValues));
            }
            return sb.ToString();
        }
        #endregion
    }
}
