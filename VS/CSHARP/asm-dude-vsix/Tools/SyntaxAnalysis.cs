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

using AsmSim;
using AsmTools;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace AsmDude.Tools
{
    internal sealed class SyntaxAnalysis
    {
        #region Fields
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmSim.Tools _tools;
        private readonly IDictionary<int, (Mnemonic Mnemonic, string Message)> _syntax_Errors;
        private readonly ISet<int> _isNotImplemented;

        private readonly Delay _delay;
        private object _updateLock = new object();
        #endregion Fields

        public SyntaxAnalysis(AsmSimulator asmSimulator)
        {
            this._asmSimulator = asmSimulator;
            this._tools = asmSimulator.Tools;
            this._syntax_Errors = new Dictionary<int, (Mnemonic Mnemonic, string Message)>();
            this._isNotImplemented = new HashSet<int>();

            this._delay = new Delay(AsmDudePackage.msSleepBeforeAsyncExecution, 10, AsmDudeTools.Instance.Thread_Pool);
            this._delay.Done_Event += (o, i) => { AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset_Private); };

            this._asmSimulator.Reset_Done_Event += (o, i) => { this._delay.Reset(); };
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
        public (Mnemonic Mnemonic, string Message) Get_Syntax_Error(int lineNumber)
        {
            return this._syntax_Errors.TryGetValue(lineNumber, out (Mnemonic Mnemonic, string Message) error) ? error : (Mnemonic.NONE, ""); 
        }

        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;
       // public event EventHandler<EventArgs> Reset_Done_Event;

        #region Private Methods

        private void Reset_Private()
        {
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;
                this._syntax_Errors.Clear();
                this._isNotImplemented.Clear();
                this.Add_All();
                //this.Reset_Done_Event(this, new EventArgs());
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "SyntaxAnalysis");
            }
        }

        private void Add_All()
        {
            bool update_Syntax_Error = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Syntax_Errors || Settings.Default.AsmSim_Decorate_Syntax_Errors);
            bool update_Not_Implemented = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Decorate_Unimplemented);

            StaticFlow sFlow = this._asmSimulator.StaticFlow;
            for (int lineNumber = sFlow.FirstLineNumber; lineNumber < sFlow.LastLineNumber; ++lineNumber)
            {
                var syntaxInfo = this._asmSimulator.Get_Syntax_Errors(lineNumber);

                if (syntaxInfo.IsImplemented)
                {
                    if (syntaxInfo.Message != null)
                    {
                        if (update_Syntax_Error)
                        {
                            this._syntax_Errors.Add(lineNumber, (syntaxInfo.Mnemonic, syntaxInfo.Message));
                            this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.SYNTAX_ERROR));
                        }
                    }
                }
                else
                {
                    if (update_Not_Implemented)
                    {
                        this._isNotImplemented.Add(lineNumber);
                        this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.NOT_IMPLEMENTED));
                    }
                }
            }
        }

        #endregion
    }
}
