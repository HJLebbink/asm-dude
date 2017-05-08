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
    internal sealed class SemanticErrorAnalysis
    {
        #region Private Fields
        private readonly ITextBuffer _sourceBuffer;
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmSimZ3.Mnemonics_ng.Tools _tools;
        private readonly IDictionary<int, string> _semanticErrors;

        private object _updateLock = new object();

        private bool _busy;
        private bool _waiting;
        private bool _scheduled;

        #endregion Private Fields

        public SemanticErrorAnalysis(ITextBuffer buffer, AsmSimulator asmSimulator, AsmSimZ3.Mnemonics_ng.Tools tools)
        {
            this._sourceBuffer = buffer;
            this._asmSimulator = asmSimulator;
            this._tools = tools;
            this._semanticErrors = new Dictionary<int, string>();

            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this._sourceBuffer.ChangedLowPriority += this.Buffer_Changed;
            this.Reset_Delayed();
        }

        public IEnumerable<(int LineNumber, string Message)> SemanticErrors
        {
            get
            {
                foreach (var x in this._semanticErrors)
                {
                    yield return (x.Key, x.Value);
                }
            }
        }

        public State2 GetStateCache(int lineNumber)
        {
            return this._asmSimulator.Get_State_After(lineNumber, false);
        }

        public bool HasSemanticError(int lineNumber)
        {
            return this._semanticErrors.ContainsKey(lineNumber);
        }
        public string GetSemanticError(int lineNumber)
        {
            return this._semanticErrors.TryGetValue(lineNumber, out string message) ? message : "";
        }
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

                this._semanticErrors.Clear();
                this.Add_All();

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

        private void Add_All()
        {
            ITextSnapshot snapShot = this._sourceBuffer.CurrentSnapshot;
            for (int lineNumber = 0; lineNumber < snapShot.LineCount; ++lineNumber)
            {
                State2 state = this._asmSimulator.Create_State_After(lineNumber);

                string line = snapShot.GetLineFromLineNumber(lineNumber).GetText().Trim();
                string message = AsmSimulator.Get_Undefined_Warnings(line, this._asmSimulator.Tools, state);
                if (message.Length > 0)
                {
                    this._semanticErrors.Add(lineNumber, message);
                }
            }
        }
        #endregion
    }
}
