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

namespace AsmDude2LS
{
    using System;
    using System.Threading;
    using Amib.Threading;

    public class Delay
    {
        private readonly SmartThreadPool threadPool_;
        private readonly int defaultDelayInMs_;
        private readonly int maxResets_;
        private int nResets_;
        private IWorkItemResult current_;

        public Delay(int defaultDelayInMs, int maxResets, SmartThreadPool threadPool)
        {
            this.defaultDelayInMs_ = defaultDelayInMs;
            this.maxResets_ = maxResets;
            this.threadPool_ = threadPool;
        }

        public void Reset(int delay = -1)
        {
            if ((this.current_ == null) || this.current_.IsCompleted || this.current_.IsCanceled)
            {
                //AsmDudeToolsStatic.Output_INFO("Delay:Reset: starting a new timer");
                this.nResets_ = 0;
                this.current_ = this.threadPool_.QueueWorkItem(this.Timer, delay);
            }
            else
            {
                if (this.nResets_ < this.maxResets_)
                {
                    //AsmDudeToolsStatic.Output_INFO("Delay:Reset: resetting the timer: "+this._nResets);
                    this.current_.Cancel(true);
                    this.nResets_++;
                    this.current_ = this.threadPool_.QueueWorkItem(this.Timer, delay);
                }
            }
        }

        private void Timer(int delay)
        {
            Thread.Sleep((delay == -1) ? this.defaultDelayInMs_ : delay);
            //AsmDudeToolsStatic.Output_INFO("Delay:Timer: delay elapsed");
            this.Done_Event?.Invoke(this, new EventArgs());
        }

        public event EventHandler<EventArgs> Done_Event;
    }
}
