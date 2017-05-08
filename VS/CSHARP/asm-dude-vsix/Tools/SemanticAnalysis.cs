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

using AsmSimZ3.Mnemonics_ng;
using AsmTools;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AsmDude.Tools
{
    internal sealed class SemanticAnalysis
    {
        #region Private Fields
        private readonly ITextBuffer _sourceBuffer;
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmSimZ3.Mnemonics_ng.Tools _tools;
        private readonly IDictionary<int, string> _usage_Undefined;
        private readonly IDictionary<int, string> _redundant_Instruction;

        private object _updateLock = new object();

        private bool _busy;
        private bool _waiting;
        private bool _scheduled;

        #endregion Private Fields

        public SemanticAnalysis(ITextBuffer buffer, AsmSimulator asmSimulator)
        {
            this._sourceBuffer = buffer;
            this._asmSimulator = asmSimulator;
            this._tools = asmSimulator.Tools;
            this._usage_Undefined = new Dictionary<int, string>();
            this._redundant_Instruction = new Dictionary<int, string>();

            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this._sourceBuffer.ChangedLowPriority += this.Buffer_Changed;
            this.Reset_Delayed();
        }

        #region Usage Undefined
        public IEnumerable<(int LineNumber, string Message)> Usage_Undefined
        {
            get { foreach (var x in this._usage_Undefined) yield return (x.Key, x.Value); }
        }

        public bool Has_Usage_Undefined_Warning(int lineNumber)
        {
            return this._usage_Undefined.ContainsKey(lineNumber);
        }
        public string Get_Usage_Undefined_Warning(int lineNumber)
        {
            return this._usage_Undefined.TryGetValue(lineNumber, out string message) ? message : "";
        }
        #endregion
        
        #region Redundant Instruction
        public IEnumerable<(int LineNumber, string Message)> Redundant_Instruction
        {
            get { foreach (var x in this._redundant_Instruction) yield return (x.Key, x.Value); }
        }
        public bool Has_Redundant_Instruction_Warning(int lineNumber)
        {
            return this._redundant_Instruction.ContainsKey(lineNumber);
        }
        public string Get_Redundant_Instruction_Warning(int lineNumber)
        {
            return this._redundant_Instruction.TryGetValue(lineNumber, out string message) ? message : "";
        }
        #endregion

        public void Reset_Delayed()
        {
            if (this._waiting)
            {
                AsmDudeToolsStatic.Output_INFO("SemanticErrorAnalysis:Reset_Delayed: already waiting for execution. Skipping this call.");
                return;
            }
            if (this._busy)
            {
                AsmDudeToolsStatic.Output_INFO("SemanticErrorAnalysis:Reset_Delayed: busy; scheduling this call.");
                this._scheduled = true;
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("SemanticErrorAnalysis:Reset_Delayed: going to execute this call.");
                AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset);
            }
        }

        public event EventHandler<CustomEventArgs> Reset_Done_Event;

        #region Private Methods
        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            bool nonSpaceAdded = false;
            foreach (var c in e.Changes) if (c.NewText != " ") nonSpaceAdded = true;
            if (!nonSpaceAdded) return;

            this.Reset_Delayed();
        }

        private void Reset()
        {
            this._waiting = true;
            Thread.Sleep(AsmDudePackage.msSleepBeforeAsyncExecution);
            this._busy = true;
            this._waiting = false;

            #region Payload
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;
                AsmSimZ3.Mnemonics_ng.Tools tools = this._asmSimulator.Tools;
                this._usage_Undefined.Clear();
                this._redundant_Instruction.Clear();

                ITextSnapshot snapShot = this._sourceBuffer.CurrentSnapshot;
                for (int lineNumber = 0; lineNumber < snapShot.LineCount; ++lineNumber)
                {
                    string line = snapShot.GetLineFromLineNumber(lineNumber).GetText().Trim();
                    {
                        string message = this._asmSimulator.Get_Usage_Undefined_Warnings(line, lineNumber);
                        if (message.Length > 0) this._usage_Undefined.Add(lineNumber, message);
                    }
                    {
                        string message = this._asmSimulator.Get_Redundant_Instruction_Warnings(line, lineNumber);
                        if (message.Length > 0) this._redundant_Instruction.Add(lineNumber, message);
                    }
                }
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "SemanticErrorAnalysis");
            }
            #endregion Payload

            this.On_Reset_Done_Event(new CustomEventArgs("Resetting SemanticErrorAnalysis is finished"));

            this._busy = false;
            if (this._scheduled)
            {
                this._scheduled = false;
                Reset_Delayed();
            }
        }

        private void On_Reset_Done_Event(CustomEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber un-subscribes
            // immediately after the null check and before the event is raised.
            EventHandler<CustomEventArgs> handler = Reset_Done_Event;

            // Event will be null if there are no subscribers
            if (handler != null)
            {
                // Format the string to send inside the CustomEventArgs parameter
                e.Message += String.Format(" at {0}", DateTime.Now.ToString());

                // Use the () operator to raise the event.
                handler(this, e);
            }
        }
        #endregion
    }
}
