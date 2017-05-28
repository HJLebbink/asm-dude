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
using System.Threading;
using Amib.Threading;

namespace AsmDude.Tools
{
    public class Delay
    {
        private readonly SmartThreadPool _pool;
        private readonly int _delayInMs;
        private readonly int _maxResets;
        private int _nResets;
        private IWorkItemResult _current;

        public Delay(int delayInMs, int maxResets, SmartThreadPool pool)
        {
            this._delayInMs = delayInMs;
            this._maxResets = maxResets;
            this._pool = pool;
        }

        public void Reset()
        {
            if ((this._current == null) || (this._current.IsCompleted) || (this._current.IsCanceled))
            {
                //AsmDudeToolsStatic.Output_INFO("Delay:Reset: starting a new timer");
                this._nResets = 0;
                this._current = this._pool.QueueWorkItem(this.Timer);
            }
            else
            {
                if (this._nResets < this._maxResets)
                {
                    //AsmDudeToolsStatic.Output_INFO("Delay:Reset: resetting the timer: "+this._nResets);
                    this._current.Cancel(true);
                    this._nResets++;
                    this._current = this._pool.QueueWorkItem(this.Timer);
                }
            }
        }

        private void Timer()
        {
            Thread.Sleep(this._delayInMs);
            //AsmDudeToolsStatic.Output_INFO("Delay:Timer: delay elapsed");
            this.Done(this, new EventArgs());
        }

        public event EventHandler<EventArgs> Done;
    }
}
