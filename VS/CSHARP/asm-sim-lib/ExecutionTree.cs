// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

namespace AsmSim
{
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
    using QuickGraph;

    public class ExecutionTree : IDisposable
    {
        #region Fields
        private readonly Tools _tools;
        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool Branch, string AsmCode)>> _graph;
        private readonly IDictionary<string, State> _states;

        //private readonly object _updateLock = new object();
        #endregion

        public ExecutionTree(Tools tools)
        {
            this._tools = tools;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, string AsmCode)>>(true);
            this._states = new Dictionary<string, State>();
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

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ExecutionTree() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
