using System;
using System.Collections.Generic;
using System.Text;

namespace AsmTools
{
    public class StopWatch
    {
        private readonly IDictionary<string, long> _startTimeTicks;
        private readonly IDictionary<string, double> _totalTimeInSec;
        public bool On { get; set; }


        /// <summary> Constructor </summary>
        public StopWatch()
        {
            this.On = true;
            this._startTimeTicks = new Dictionary<string, long>();
            this._totalTimeInSec = new Dictionary<string, double>();
        }

        public void Reset()
        {
            this._startTimeTicks.Clear();
            this._totalTimeInSec.Clear();
        }

        public void Start(string key)
        {
            if (this.On) this._startTimeTicks[key] = DateTime.Now.Ticks;
        }

        public void Stop(string key)
        {
            if (this.On)
            {
                double elapsedSec = 0;
                if (this._startTimeTicks.TryGetValue(key, out long value))
                {
                    elapsedSec = (double)(DateTime.Now.Ticks - value) / 10000000;
                }
                if (this._totalTimeInSec.TryGetValue(key, out double sum))
                {
                    this._totalTimeInSec[key] = sum + elapsedSec;
                } else
                {
                    this._totalTimeInSec[key] = elapsedSec;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (this.On)
            {
                if (this._totalTimeInSec.Count == 0)
                {
                    sb.Append("StopWatch: no entries");
                }
                else
                {
                    foreach (KeyValuePair<string, double> entry in this._totalTimeInSec)
                    {
                        sb.Append("StopWatch: ").Append(entry.Key).Append(": ").Append(entry.Value).AppendLine(" sec.");
                    }
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
