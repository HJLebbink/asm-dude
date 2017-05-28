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
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace AsmDude.Tools
{
    internal sealed class SemanticAnalysis
    {
        #region Private Fields
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmSim.Tools _tools;
        private readonly IDictionary<int, string> _usage_Undefined;
        private readonly IDictionary<int, string> _redundant_Instruction;

        private readonly Delay _delay;
        private object _updateLock = new object();
        #endregion Private Fields

        public SemanticAnalysis(AsmSimulator asmSimulator)
        {
            this._asmSimulator = asmSimulator;
            this._tools = asmSimulator.Tools;
            this._usage_Undefined = new Dictionary<int, string>();
            this._redundant_Instruction = new Dictionary<int, string>();

            this._delay = new Delay(AsmDudePackage.msSleepBeforeAsyncExecution, 10, AsmDudeTools.Instance.Thread_Pool);
            this._delay.Done_Event += (o, i) => { AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset_Private); };

            this._asmSimulator.Reset_Done_Event += (o, i) => { this._delay.Reset(); };
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


        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;
        public event EventHandler<EventArgs> Reset_Done_Event;

        #region Private Methods

        private void Reset_Private()
        {
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;
                this._usage_Undefined.Clear();
                this._redundant_Instruction.Clear();
                this.Add_All();
                this.Reset_Done_Event(this, new EventArgs());
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "SemanticAnalysis");
            }
        }

        private void Add_All()
        {
            bool update_Usage_Undefined = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Usage_Of_Undefined || Settings.Default.AsmSim_Decorate_Usage_Of_Undefined);
            bool update_Redundant_Instruction = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Redundant_Instructions || Settings.Default.AsmSim_Decorate_Redundant_Instructions);
            bool update_Known_Register = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Decorate_Registers);

            StaticFlow sFlow = this._asmSimulator.StaticFlow;
            for (int lineNumber = sFlow.FirstLineNumber; lineNumber < sFlow.LastLineNumber; ++lineNumber)
            {
                {
                    if (update_Known_Register)
                    {
                        this._asmSimulator.Get_State_After(lineNumber, false, true);
                        this._asmSimulator.Get_State_Before(lineNumber, false, true);
                        this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmErrorEnum.NONE));
                    }
                }
                {
                    if (update_Usage_Undefined)
                    {
                        string message = this._asmSimulator.Get_Usage_Undefined_Warnings(lineNumber);
                        if (message.Length > 0)
                        {
                            this._usage_Undefined.Add(lineNumber, message);
                            this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmErrorEnum.USAGE_OF_UNDEFINED));
                        }
                    }
                }
                {
                    if (update_Redundant_Instruction)
                    {
                        string message = this._asmSimulator.Get_Redundant_Instruction_Warnings(lineNumber);
                        if (message.Length > 0)
                        {
                            this._redundant_Instruction.Add(lineNumber, message);
                            this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmErrorEnum.REDUNDANT));
                        }
                    }
                }
            }
        }
        #endregion
    }
}
