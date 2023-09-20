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

namespace AsmTools
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class StopWatch
    {
        private readonly IDictionary<string, long> startTimeTicks_;
        private readonly IDictionary<string, double> totalTimeInSec_;

        /// <summary> Constructor </summary>
        public StopWatch()
        {
            this.On = true;
            this.startTimeTicks_ = new Dictionary<string, long>();
            this.totalTimeInSec_ = new Dictionary<string, double>();
        }
        public bool On { get; set; }

        public void Reset()
        {
            this.startTimeTicks_.Clear();
            this.totalTimeInSec_.Clear();
        }

        public void Start(string key)
        {
            if (this.On)
            {
                this.startTimeTicks_[key] = DateTime.Now.Ticks;
            }
        }

        public void Stop(string key)
        {
            if (this.On)
            {
                double elapsedSec = 0;
                if (this.startTimeTicks_.TryGetValue(key, out long value))
                {
                    elapsedSec = (double)(DateTime.Now.Ticks - value) / 10000000;
                }
                if (this.totalTimeInSec_.TryGetValue(key, out double sum))
                {
                    this.totalTimeInSec_[key] = sum + elapsedSec;
                }
                else
                {
                    this.totalTimeInSec_[key] = elapsedSec;
                }
            }
        }

        public override string ToString()
        {
            double totalTime = 0;
            StringBuilder sb = new StringBuilder();
            if (this.On)
            {
                if (this.totalTimeInSec_.Count == 0)
                {
                    sb.Append("StopWatch: no entries");
                }
                else
                {
                    foreach (KeyValuePair<string, double> entry in this.totalTimeInSec_)
                    {
                        totalTime += entry.Value;
                        sb.Append("StopWatch: ").Append(entry.Key).Append(": ").Append(entry.Value).AppendLine(" sec.");
                    }
                    sb.Append("StopWatch: Total Time: ").Append(totalTime).AppendLine(" sec.");
                }
            }
            else
            {
                sb.Append("StopWatch is switched off");
            }
            return sb.ToString();
        }
    }
}
