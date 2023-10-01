// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmSim
{
    using System;
    using System.Collections.Generic;
    using QuikGraph;

    public class ExecutionTree : IDisposable
    {
        #region Fields
        private readonly Tools tools_;
        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool branch, string asmCode)>> graph_;
        private readonly IDictionary<string, State> states_;
        #endregion

        public ExecutionTree(Tools tools)
        {
            this.tools_ = tools;
            this.graph_ = new BidirectionalGraph<string, TaggedEdge<string, (bool branch, string asmCode)>>(true);
            this.states_ = new Dictionary<string, State>();
        }

        public void Init(DynamicFlow dFlow, int startLineNumber)
        {
            // Unroll the provided dFlow in depth first until either the maximum depth is reached or the state is inconsistent.
            /*
            if (!dFlow.Has_LineNumber(startLineNumber))
            {
                Console.WriteLine("WARNING: ExecutionTree: provided startLineNumber " + startLineNumber + " does not exist in the provided dFlow");
            }
            string startKey = dFlow.Key(startLineNumber);

            foreach (var v in dFlow.Get_Incomming_StateUpdate(startKey)) {

            }
            //TODO
            */
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ExecutionTree()
        {
            this.Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.graph_.Clear();

                    foreach (var x in this.states_)
                    {
                        x.Value.Dispose();
                    }
                    this.states_.Clear();
                }
                // free native resources if there are any.
                this.disposedValue = true;
            }
        }
        #endregion
    }
}
