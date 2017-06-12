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
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using AsmDude.SyntaxHighlighting;
using AsmSim;
using AsmTools;
using Amib.Threading;

namespace AsmDude.Tools
{
    public class AsmSimulator : IDisposable
    {
        #region Fields
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly StaticFlow _sFlow;
        private readonly DynamicFlow _dFlow;

        private readonly IDictionary<int, State> _cached_States_Before;
        private readonly IDictionary<int, State> _cached_States_After;
        private readonly IDictionary<int, string> _usage_Undefined;
        private readonly IDictionary<int, string> _redundant_Instruction;
        private readonly IDictionary<int, (Mnemonic Mnemonic, string Message)> _syntax_Errors;
        private readonly ISet<int> _isNotImplemented;

        private readonly ISet<int> _bussy_States_Before;
        private readonly ISet<int> _bussy_States_After;

        public bool Enabled { get; set; }

        private readonly SmartThreadPool _threadPool;
        private readonly SmartThreadPool _threadPool2;
        private IWorkItemResult _thread_Result;
        public readonly AsmSim.Tools Tools;

        private readonly Delay _delay;
        private int _last_Changed_LineNumber = 0;
        private object _resetLock = new object();
        private object _updateLock = new object();
        #endregion

        #region Constuctors
        /// <summary>Factory return singleton</summary>
        public static AsmSimulator GetOrCreate_AsmSimulator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            System.Func<AsmSimulator> sc = delegate ()
            {
                return new AsmSimulator(buffer, aggregatorFactory);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }

        private AsmSimulator(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            this._buffer = buffer;
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, aggregatorFactory);

            if (Settings.Default.AsmSim_On)
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: swithed on");
                this.Enabled = true;

                this._cached_States_After = new Dictionary<int, State>();
                this._cached_States_Before = new Dictionary<int, State>();
                this._bussy_States_After = new HashSet<int>();
                this._bussy_States_Before = new HashSet<int>();
                this._usage_Undefined = new Dictionary<int, string>();
                this._redundant_Instruction = new Dictionary<int, string>();
                this._syntax_Errors = new Dictionary<int, (Mnemonic Mnemonic, string Message)>();
                this._isNotImplemented = new HashSet<int>();

                this._threadPool = AsmDudeTools.Instance.Thread_Pool;
                this._threadPool2 = new SmartThreadPool(60000, 3, 3);
                Dictionary <string, string> settings = new Dictionary<string, string> {
                    /*
                    Legal parameters are:
                        auto_config(bool)(default: true)
                        debug_ref_count(bool)(default: false)
                        dump_models(bool)(default: false)
                        model(bool)(default: true)
                        model_validate(bool)(default: false)
                        proof(bool)(default: false)
                        rlimit(unsigned int)(default: 4294967295)
                        smtlib2_compliant(bool)(default: false)
                        timeout(unsigned int)(default: 4294967295)
                        trace(bool)(default: false)
                        trace_file_name(string)(default: z3.log)
                        type_check(bool)(default: true)
                        unsat_core(bool)(default: false)
                        well_sorted_check(bool)(default: false)
                    */
                    { "unsat-core", "false" },    // enable generation of unsat cores
                    { "model", "false" },         // enable model generation
                    { "proof", "false" },         // enable proof generation
                    { "timeout", Settings.Default.AsmSim_Z3_Timeout_MS.ToString()}
                };
                this.Tools = new AsmSim.Tools(settings);
                if (Settings.Default.AsmSim_64_Bits)
                {
                    this.Tools.Parameters.mode_64bit = true;
                    this.Tools.Parameters.mode_32bit = false;
                    this.Tools.Parameters.mode_16bit = false;
                }
                else
                {
                    this.Tools.Parameters.mode_64bit = false;
                    this.Tools.Parameters.mode_32bit = true;
                    this.Tools.Parameters.mode_16bit = false;
                }
                this._sFlow = new StaticFlow(this._buffer.CurrentSnapshot.GetText(), this.Tools);
                this._dFlow = new DynamicFlow(this.Tools);

                this._delay = new Delay(AsmDudePackage.msSleepBeforeAsyncExecution, 100, this._threadPool);
                this._delay.Done_Event += (o, i) => {

                    if ((this._thread_Result != null) && !this._thread_Result.IsCanceled)
                    {
                        this._thread_Result.Cancel();
                    }
                    this._thread_Result = this._threadPool.QueueWorkItem(this.Reset_Private);
                };

                this.Reset(); // wait to give the system some breathing time
                this._buffer.ChangedLowPriority += (o, i) => {
                    if (i.Changes.Count > 0)
                    {
                        var v = i.Changes[0];
                        this._last_Changed_LineNumber = i.After.GetLineNumberFromPosition(v.NewPosition);
                        //AsmDudeToolsStatic.Output_INFO("AsmSimulator: changes: newText=" + v.NewText +"; oldText="+v.OldText +"; lineNumber="+ this._last_Changed_LineNumber);
                        this.Reset();
                    }
                };
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: switched off");
                this.Enabled = false;
            }
        }

        #endregion Constructors

        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;
        public event EventHandler<EventArgs> Reset_Done_Event;

        public void Dispose()
        {
            this._threadPool2.Dispose();
        }

        #region Reset
        public void Reset(int delay = -1)
        {
            this._delay.Reset(delay);
        }

        private void Reset_Private()
        {
            lock (this._resetLock)
            {
                string sourceCode = this._buffer.CurrentSnapshot.GetText();
                if (this._sFlow.Update(sourceCode))
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulation:Reset_Private: updating dFlow");

                    this.Tools.StateConfig = Runner.GetUsage_StateConfig(this._sFlow, 0, this._sFlow.LastLineNumber, this.Tools);
                    this._dFlow.Reset(this._sFlow, true);
                    this._cached_States_After.Clear();
                    this._cached_States_Before.Clear();
                    this._bussy_States_After.Clear();
                    this._bussy_States_Before.Clear();
                    this._redundant_Instruction.Clear();
                    this._usage_Undefined.Clear();
                    this._syntax_Errors.Clear();
                    this._isNotImplemented.Clear();                  
                    PreCalculate_LOCAL();
                    this.Reset_Done_Event?.Invoke(this, new EventArgs());
                }
            }

            #region Local Methods

            IEnumerable<int> LineNumber_Centered_LOCAL(int first, int center, int last)
            {
                bool continue1 = true;
                bool continue2 = true;

                yield return center;
                for (int i = 1; i < last; ++i)
                {
                    int x1 = center - i;
                    if (x1 >= first)
                    {
                        yield return x1;
                    }
                    else continue1 = false;

                    int x2 = center + i;
                    if (x2 < last)
                    {
                        yield return x2;
                    }
                    else continue2 = false;

                    if (!continue1 && !continue2) yield break;
                }
            }

            void PreCalculate_LOCAL()
            {
                bool update_Syntax_Error = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Syntax_Errors || Settings.Default.AsmSim_Decorate_Syntax_Errors);
                bool decorate_Not_Implemented = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Decorate_Unimplemented);

                bool update_Usage_Undefined = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Usage_Of_Undefined || Settings.Default.AsmSim_Decorate_Usage_Of_Undefined);
                bool update_Redundant_Instruction = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Redundant_Instructions || Settings.Default.AsmSim_Decorate_Redundant_Instructions);
                bool update_Known_Register = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Decorate_Registers);

                foreach (int lineNumber in LineNumber_Centered_LOCAL(this._sFlow.FirstLineNumber, this._last_Changed_LineNumber, this._sFlow.LastLineNumber))
                {
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.USAGE_OF_UNDEFINED));
                }
                foreach (int lineNumber in LineNumber_Centered_LOCAL(this._sFlow.FirstLineNumber, this._last_Changed_LineNumber, this._sFlow.LastLineNumber))
                {
                    try
                    {
                        var syntaxInfo = this.Calculate_Syntax_Errors(lineNumber);
                        if (!syntaxInfo.IsImplemented) // the operation is not implemented
                        {
                            if (decorate_Not_Implemented)
                            {
                                this._isNotImplemented.Add(lineNumber);
                                this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.NOT_IMPLEMENTED));
                            }
                        }
                        else
                        {
                            if (syntaxInfo.Message != null) // found a syntax error
                            {
                                if (update_Syntax_Error)
                                {
                                    this._syntax_Errors.Add(lineNumber, (syntaxInfo.Mnemonic, syntaxInfo.Message));
                                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.SYNTAX_ERROR));
                                }
                            }
                            else // operation is implemented and no syntax error
                            {
                                if (update_Known_Register)
                                {
                                    var content = this._sFlow.Get_Line(lineNumber);
                                    foreach (var v in content.Args)
                                    {
                                        Rn regName = RegisterTools.ParseRn(v, true);
                                        if (regName != Rn.NOREG)
                                        {
                                            this.PreCompute_Register_Value(regName, lineNumber, true);
                                            this.PreCompute_Register_Value(regName, lineNumber, false);
                                            this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                                        }
                                    }
                                }
                                if (update_Usage_Undefined)
                                {
                                    string message = this.Calculate_Usage_Undefined_Warnings(lineNumber);
                                    if (message.Length > 0)
                                    {
                                        this._usage_Undefined.Add(lineNumber, message);
                                        this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.USAGE_OF_UNDEFINED));
                                    }
                                }
                                if (update_Redundant_Instruction)
                                {
                                    string message = this.Calculate_Redundant_Instruction_Warnings(lineNumber);
                                    if (message.Length > 0)
                                    {

                                        this._redundant_Instruction.Add(lineNumber, message);
                                        this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.REDUNDANT));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:PreCalculate_LOCAL; e={1}", ToString(), e.ToString()));
                    }
                }
            }
            #endregion
        }
        #endregion

        #region Syntax Errors
        public IEnumerable<(int LineNumber, Mnemonic Mnemonic, string Message)> Syntax_Errors
        {
            get
            {
                foreach (var x in this._syntax_Errors)
                {
                    yield return (x.Key, x.Value.Mnemonic, x.Value.Message);
                }
            }
        }
        public bool Is_Implemented(int lineNumber)
        {
            return !this._isNotImplemented.Contains(lineNumber);
        }
        public bool Has_Syntax_Error(int lineNumber)
        {
            return this._syntax_Errors.ContainsKey(lineNumber);
        }
        public (Mnemonic Mnemonic, string Message) Get_Syntax_Error(int lineNumber)
        {
            return this._syntax_Errors.TryGetValue(lineNumber, out (Mnemonic Mnemonic, string Message) error) ? error : (Mnemonic.NONE, "");
        }
        private (bool IsImplemented, Mnemonic Mnemonic, string Message) Calculate_Syntax_Errors(int lineNumber)
        {
            var dummyKeys = ("", "", "");
            var content = this._sFlow.Get_Line(lineNumber);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, this.Tools);
            if (opcodeBase == null) return (IsImplemented: false, Mnemonic: Mnemonic.NONE, Message: null);

            if (opcodeBase.GetType() == typeof(AsmSim.Mnemonics.NotImplemented))
            {
                return (IsImplemented: false, Mnemonic: content.Mnemonic, Message: null);
            }
            else
            {
                return (opcodeBase.IsHalted)
                    ? (IsImplemented: true, Mnemonic: content.Mnemonic, Message: opcodeBase.SyntaxError)
                    : (IsImplemented: true, Mnemonic: content.Mnemonic, Message: null);
            }
        }
        #endregion

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
        private string Calculate_Usage_Undefined_Warnings(int lineNumber)
        {
            lock (this._updateLock)
            {
                State state = this.Get_State_Before(lineNumber, false, false).State;
                if (state == null) return "";

                var dummyKeys = ("", "", "");
                var content = this._sFlow.Get_Line(lineNumber);
                var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, this.Tools);

                string message = "";
                if (opcodeBase != null)
                {
                    StateConfig stateConfig = this.Tools.StateConfig;
                    foreach (Flags flag in FlagTools.GetFlags(opcodeBase.FlagsReadStatic))
                    {
                        if (stateConfig.IsFlagOn(flag))
                        {
                            if (state.Is_Undefined(flag))
                            {
                                message = message + flag + " is undefined; ";
                            }
                        }
                    }
                    foreach (Rn reg in opcodeBase.RegsReadStatic)
                    {
                        if (stateConfig.IsRegOn(RegisterTools.Get64BitsRegister(reg)))
                        {
                            Tv[] regContent = state.GetTvArray(reg);
                            bool isUndefined = false;
                            foreach (var tv in regContent)
                            {
                                if (tv == Tv.UNDEFINED)
                                {
                                    isUndefined = true;
                                    break;
                                }
                            }
                            if (isUndefined)
                            {
                                message = message + reg + " has undefined content: " + ToolsZ3.ToStringHex(regContent) + " = " + ToolsZ3.ToStringBin(regContent) + "; ";
                            }
                        }
                    }
                }
                return message;
            }
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
        private string Calculate_Redundant_Instruction_Warnings(int lineNumber)
        {
            var content = this._sFlow.Get_Line(lineNumber);
            if (content.Mnemonic == Mnemonic.NONE) return "";
            if (content.Mnemonic == Mnemonic.NOP) return "";
            if (content.Mnemonic == Mnemonic.UNKNOWN) return "";

            if (this._dFlow.Is_Branch_Point(lineNumber)) return "";
            if (this._dFlow.Is_Merge_Point(lineNumber)) return "";

            State state = this.Get_State_After(lineNumber, false, true).State;
            if (state == null) return "";

            string key1 = this._dFlow.Key(lineNumber);
            string key2 = this._dFlow.Key_Next(lineNumber);

            lock (this._updateLock)
            {
                StateConfig stateConfig = Runner.GetUsage_StateConfig(this._sFlow, lineNumber, lineNumber, this.Tools);

                foreach (Flags flag in stateConfig.GetFlagOn())
                {
                    if (!state.Is_Redundant(flag, key1, key2)) return "";
                }
                foreach (Rn reg in stateConfig.GetRegOn())
                {
                    if (!state.Is_Redundant(reg, key1, key2)) return "";
                }
                if (stateConfig.mem)
                {
                    if (!state.Is_Redundant_Mem(key1, key2)) return "";
                }
            }
            string message = "\"" + this._sFlow.Get_Line_Str(lineNumber) + "\" is redundant.";
            AsmDudeToolsStatic.Output_INFO("AsmSimulator: Has_Redundant_Instruction_Warnings: lineNumber " + lineNumber + ": " + message);
            return message;
        }
        #endregion

        #region Getters
        private void PreCompute_Register_Value(Rn name, int lineNumber, bool before)
        {
            // get the register value and discard the result, the value will be added to the cache
            this.Get_Register_Value(name, lineNumber, before, false, true);
        }

        public (string Value, bool Bussy) Get_Register_Value(Rn name, int lineNumber, bool before, bool async, bool create)
        {
            try
            {
                if (!this.Enabled) return ("", false);

                var state = (before) ? this.Get_State_Before(lineNumber, async, create) : this.Get_State_After(lineNumber, async, create);
                if (state.Bussy)
                {
                    this._threadPool2.QueueWorkItem(Update_State_And_TvArray_LOCAL);
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                    return ("[I'm bussy and haven't acquired the state of line " + (lineNumber + 1) + " yet.]", true); // plus 1 for the lineNumber because lineNumber 0 is shown as lineNumber 1
                }
                if (state.State == null)
                {
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                    return ("[I'm confused, sorry about that.]", true);
                }

                Tv[] reg = state.State.GetTvArray_Cached(name);
                if (reg == null)
                {
                    this._threadPool2.QueueWorkItem(Update_TvArray_LOCAL, state.State);
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                    return ("[I'm bussy determining the bits of " + name + ".]", true);
                }
                else
                {
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                    return (ToString_LOCAL(reg), false);
                }

                #region Local Methods

                void Update_State_And_TvArray_LOCAL()
                {
                    var state2 = (before) ? this.Get_State_Before(lineNumber, false, true) : this.Get_State_After(lineNumber, false, true);
                    Update_TvArray_LOCAL(state2.State);
                }

                void Update_TvArray_LOCAL(State state2)
                {
                    state2.Update_TvArray_Cached(name);
                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                }

                string ToString_LOCAL(Tv[] array)
                {
                    if (false)
                    {
                        return string.Format("{0} = {1}\n{2}", ToolsZ3.ToStringHex(reg), ToolsZ3.ToStringBin(reg), state.State.ToStringConstraints("") + state.State.ToStringRegs("") + state.State.ToStringFlags(""));
                    }
                    else
                    {
                        return string.Format("{0} = {1}", ToolsZ3.ToStringHex(reg), ToolsZ3.ToStringBin(reg));
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Get_Register_Value; e={1}", ToString(), e.ToString()));
                return ("Exception in AsmSimulator:Get_Register_Value", false);
            }
        }

        public (bool HasValue, bool Bussy) Has_Register_Value(Rn name, int lineNumber, bool before, bool create = false)
        {
            try
            {
                var state = (before)
                    ? this.Get_State_Before(lineNumber, false, false)
                    : this.Get_State_After(lineNumber, false, false);
                if (state.Bussy)
                {
                    return (false, true);
                }
                else if (state.State == null)
                {
                    if (create) PreCompute_Register_Value(name, lineNumber, before);
                    return (false, true);
                }
                else
                {
                    Tv[] content = state.State.GetTvArray_Cached(name);
                    if (content == null)
                    {
                        if (create) PreCompute_Register_Value(name, lineNumber, before);
                        return (false, true);
                    }
                    foreach (Tv tv in content)
                    {
                        if ((tv == Tv.ONE) || (tv == Tv.ZERO) || (tv == Tv.UNDEFINED)) return (true, false);
                    }
                    return (false, false);
                }
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Has_Register_Value; e={1}", ToString(), e.ToString()));
                return (false, false);
            }
        }

        /// <summary>If async is false, return the state of the provided lineNumber.
        /// If async is true, returns the state of the provided lineNumber when it exists in the case, 
        /// returns null otherwise and schedules its computation. 
        /// if the state is not computed yet, 
        /// return null and create one in a different thread according to the provided createState boolean.</summary>
        private (State State, bool Bussy) Get_State_After(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled) return (State: null, Bussy: false);
            if (this._cached_States_After.TryGetValue(lineNumber, out State result))
            {
                return (State: result, Bussy: false);
            }
            if (this._bussy_States_After.Contains(lineNumber))
            {
                return (State: null, Bussy: true);
            }
            if (create)
            {
                if (async)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: going to execute this in a different thread.");
                    this._threadPool2.QueueWorkItem(Calculate_State_After_LOCAL, WorkItemPriority.Lowest);
                    return (State: null, Bussy: true);
                }
                else
                {
                    Calculate_State_After_LOCAL();
                    this._cached_States_After.TryGetValue(lineNumber, out State result2);
                    return (State: result2, Bussy: false);
                }
            }
            return (State: null, Bussy: false);

            #region Local Methods
            void Calculate_State_After_LOCAL()
            {
                this._bussy_States_After.Add(lineNumber);

                State state = AsmSim.Tools.Collapse(this._dFlow.States_After(lineNumber));
                if (state == null)
                {
                    string key = this._dFlow.Key(lineNumber);
                    state = new State(this.Tools, key, key);
                }
                state.Frozen = true;

                lock (this._updateLock)
                {
                    this._cached_States_After.Remove(lineNumber);
                    this._cached_States_After.Add(lineNumber, state);
                    this._bussy_States_After.Remove(lineNumber);
                }
            }
            #endregion
        }

        private (State State, bool Bussy) Get_State_Before(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled) return (State: null, Bussy: false);
            if (this._cached_States_Before.TryGetValue(lineNumber, out State result))
            {
                return (State: result, Bussy: false);
            }
            if (this._bussy_States_Before.Contains(lineNumber))
            {
                return (State: null, Bussy: true);
            }
            if (create)
            {
                if (async)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_Before: going to execute this in a different thread.");
                    this._threadPool2.QueueWorkItem(Calculate_State_Before_LOCAL, WorkItemPriority.Lowest);
                    return (State: null, Bussy: true);
                }
                else
                {
                    Calculate_State_Before_LOCAL();
                    this._cached_States_Before.TryGetValue(lineNumber, out State result2);
                    return (State: result2, Bussy: false);
                }
            }
            return (State: null, Bussy: false);

            #region Local Methods
            void Calculate_State_Before_LOCAL()
            {
                this._bussy_States_Before.Add(lineNumber);

                State state = AsmSim.Tools.Collapse(this._dFlow.States_Before(lineNumber));
                if (state == null)
                {
                    string key = this._dFlow.Key(lineNumber);
                    state = new State(this.Tools, key, key);
                }
                state.Frozen = true;

                lock (this._updateLock)
                {
                    this._cached_States_Before.Remove(lineNumber);
                    this._cached_States_Before.Add(lineNumber, state);
                    this._bussy_States_Before.Remove(lineNumber);
                }
            }
            #endregion
        }

        #endregion
    }
}
