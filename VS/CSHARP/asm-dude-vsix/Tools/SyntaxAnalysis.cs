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
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AsmDude.Tools
{
    internal sealed class SyntaxAnalysis
    {
        #region Fields
        private readonly ITextBuffer _sourceBuffer;
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmSimZ3.Mnemonics_ng.Tools _tools;
        private readonly IDictionary<int, (Mnemonic Mnemonic, string Message)> _syntax_Errors;
        private readonly ISet<int> _isNotImplemented;

        private object _updateLock = new object();

        private bool _busy;
        private bool _waiting;
        private bool _scheduled;
        #endregion Fields

        public SyntaxAnalysis(ITextBuffer buffer, AsmSimulator asmSimulator)
        {
            this._sourceBuffer = buffer;
            this._asmSimulator = asmSimulator;
            this._tools = asmSimulator.Tools;
            this._syntax_Errors = new Dictionary<int, (Mnemonic Mnemonic, string Message)>();
            this._isNotImplemented = new HashSet<int>();

            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this._sourceBuffer.ChangedLowPriority += this.Buffer_Changed;
            this.Reset_Delayed();
        }

        public IEnumerable<(int LineNumber, Mnemonic Mnemonic, string Message)> SyntaxErrors
        {
            get
            {
                foreach (var x in this._syntax_Errors)
                {
                    yield return (x.Key, x.Value.Mnemonic, x.Value.Message);
                }
            }
        }
        public bool IsImplemented(int lineNumber)
        {
            return !this._isNotImplemented.Contains(lineNumber);
        }

        public bool HasSyntaxError(int lineNumber)
        {
            return this._syntax_Errors.ContainsKey(lineNumber);
        }
        public (Mnemonic Mnemonic, string Message) GetSyntaxError(int lineNumber)
        {
            return this._syntax_Errors.TryGetValue(lineNumber, out (Mnemonic Mnemonic, string Message) error) ? error : (Mnemonic.NONE, ""); 
        }
        public void Reset_Delayed()
        {
            if (this._waiting)
            {
                AsmDudeToolsStatic.Output_INFO("SyntaxErrorList:Reset_Delayed: already waiting for execution. Skipping this call.");
                return;
            }
            if (this._busy)
            {
                AsmDudeToolsStatic.Output_INFO("SyntaxErrorList:Reset_Delayed: busy; scheduling this call.");
                this._scheduled = true;
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("SyntaxErrorList:Reset_Delayed: going to execute this call.");
                AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset);
            }
        }

        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;
        public event EventHandler<EventArgs> Reset_Done_Event;

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

                this._syntax_Errors.Clear();
                this._isNotImplemented.Clear();
                this.Add_All();
                this.Reset_Done_Event(this, new EventArgs());
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "SyntaxAnalysis");
            }
            #endregion Payload

            this._busy = false;
            if (this._scheduled)
            {
                this._scheduled = false;
                Reset_Delayed();
            }
        }

        private void Add_All()
        {
            ITextSnapshot snapShot = this._sourceBuffer.CurrentSnapshot;
            for (int lineNumber = 0; lineNumber < snapShot.LineCount; ++lineNumber)
            {
                var syntaxInfo = this._asmSimulator.Get_Syntax_Errors(lineNumber);

                if (syntaxInfo.IsImplemented)
                {
                    if (syntaxInfo.Message != null)
                    {
                        this._syntax_Errors.Add(lineNumber, (syntaxInfo.Mnemonic, syntaxInfo.Message));
                        this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmErrorEnum.SYNTAX_ERROR));
                    }
                }
                else
                {
                    this._isNotImplemented.Add(lineNumber);
                    this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmErrorEnum.NOT_IMPLEMENTED));
                }
            }
        }

        #endregion
    }
}
