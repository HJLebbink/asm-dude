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
using Microsoft.Z3;

using AsmDude.SyntaxHighlighting;
using AsmSim;
using AsmTools;

namespace AsmDude.Tools
{
    public class AsmSimulator
    {
        #region Fields
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly StaticFlow _sFlow;
        private readonly DynamicFlow _dFlow;

        private readonly IDictionary<int, State> _cached_States_After;
        private readonly IDictionary<int, State> _cached_States_Before;

        public readonly AsmSim.Tools Tools;

        private readonly Delay _delay;
        private object _updateLock = new object();

        private bool _busy;
        private ISet<int> _scheduled_After;
        private ISet<int> _scheduled_Before;
        public bool Enabled { get; set; }

        #endregion

        #region Constuctors
        /// <summary>Factory return singleton</summary>
        public static AsmSimulator GetOrCreate_AsmSimulator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            Func<AsmSimulator> sc = delegate ()
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
                this._scheduled_After = new HashSet<int>();
                this._scheduled_Before = new HashSet<int>();

                Dictionary<string, string> settings = new Dictionary<string, string> {
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

                this._delay = new Delay(AsmDudePackage.msSleepBeforeAsyncExecution, 10, AsmDudeTools.Instance.Thread_Pool);
                this._delay.Done_Event += (o, i) => { AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset_Private); };

                this.Reset();
                this._buffer.ChangedLowPriority += (o, i) => { this.Reset(); };
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: swithed off");
                this.Enabled = false;
            }
        }

        #endregion Constructors

        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;
        public event EventHandler<EventArgs> Reset_Done_Event;


        public void Reset()
        {
            this._delay.Reset();
        }

        private void Reset_Private()
        {
            string sourceCode = this._buffer.CurrentSnapshot.GetText();
            if (this._sFlow.Update(sourceCode))
            {
                this.Tools.StateConfig = Runner.GetUsage_StateConfig(this._sFlow, 0, this._sFlow.LastLineNumber, this.Tools);
                //AsmDudeToolsStatic.Output_INFO("AsmSimulation:Reset_Private: updating dFlow");
                lock (this._updateLock)
                {
                    this._dFlow.Reset(this._sFlow, true);
                }

                this._cached_States_After.Clear();
                this._cached_States_Before.Clear();
                this._scheduled_After.Clear();
                this._scheduled_Before.Clear();
            }

            this.Reset_Done_Event(this, new EventArgs());
        }

        public StaticFlow StaticFlow { get { return this._sFlow; } }

        public (bool IsImplemented, Mnemonic Mnemonic, string Message) Get_Syntax_Errors(int lineNumber)
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

        public string Get_Usage_Undefined_Warnings(int lineNumber)
        {
            lock (this._updateLock)
            {
                State state = this.Get_State_Before(lineNumber, false, true).State;
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
                            if (state.IsUndefined(flag))
                            {
                                message = message + flag + " is undefined; ";
                            }
                        }
                    }
                    foreach (Rn reg in opcodeBase.RegsReadStatic)
                    {
                        if (stateConfig.IsRegOn(RegisterTools.Get64BitsRegister(reg)))
                        {
                            Tv[] regContent = state.GetTvArray(reg, true);
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

        public string Get_Redundant_Instruction_Warnings(int lineNumber)
        {
            lock (this._updateLock)
            {
                var content = this._sFlow.Get_Line(lineNumber);
                if (content.Mnemonic == Mnemonic.NONE) return "";
                if (content.Mnemonic == Mnemonic.NOP) return "";
                if (content.Mnemonic == Mnemonic.UNKNOWN) return "";

                State state_Before = this.Get_State_Before(lineNumber, false, true).State;
                if (state_Before == null) return "";
                State state_After = this.Get_State_After(lineNumber, false, true).State;
                if (state_After == null) return "";

                //Context ctx = state_Before.Ctx;
                //AsmSimZ3.Mnemonics_ng.Tools tools2 = new AsmSimZ3.Mnemonics_ng.Tools(this.Tools.Settings);
                Context ctx = this.Tools.Ctx;

                //TODO create a new ctx such that this can be run concurrently

                State stateB = new State(state_Before);
                stateB.UpdateConstName("!B");

                State stateA = new State(state_After);
                stateA.UpdateConstName("!A");

                State diffState = new State(this.Tools, "!0", "!0");
                foreach (var v in stateB.Solver.Assertions)
                {
                    diffState.Solver.Assert(v as BoolExpr);
                }
                foreach (var v in stateA.Solver.Assertions)
                {
                    diffState.Solver.Assert(v as BoolExpr);
                }

                foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                {
                    diffState.Solver.Assert(ctx.MkEq(stateB.GetTail(flag), stateA.GetTail(flag)));
                }
                foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                {
                    diffState.Solver.Assert(ctx.MkEq(stateB.GetTail(reg), stateA.GetTail(reg)));
                }
                diffState.Solver.Assert(ctx.MkEq(AsmSim.Tools.Mem_Key(stateB.TailKey, ctx), AsmSim.Tools.Mem_Key(stateA.TailKey, ctx)));
                //AsmDudeToolsStatic.Output_INFO(diffState.ToString());

                StateConfig written = Runner.GetUsage_StateConfig(this._sFlow, lineNumber, lineNumber, this.Tools);

                foreach (Flags flag in written.GetFlagOn())
                {
                    BoolExpr value = ctx.MkEq(stateB.Get(flag), stateA.Get(flag));
                    Tv tv = ToolsZ3.GetTv(value, diffState.Solver, ctx);
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator: Get_Redundant_Instruction_Warnings: line " + lineNumber + ": tv=" + tv + "; value=" + value);
                    if (tv != Tv.ONE) return "";
                }
                foreach (Rn reg in written.GetRegOn())
                {
                    BoolExpr value = ctx.MkEq(stateB.Get(reg), stateA.Get(reg));
                    Tv tv = ToolsZ3.GetTv(value, diffState.Solver, ctx);
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator: Get_Redundant_Instruction_Warnings: line " + lineNumber + ":tv=" + tv + "; value=" + value);
                    if (tv != Tv.ONE) return "";
                }
                if (written.mem) {
                    BoolExpr value = ctx.MkEq(AsmSim.Tools.Mem_Key(stateB.HeadKey, ctx), AsmSim.Tools.Mem_Key(stateA.HeadKey, ctx));
                    Tv tv = ToolsZ3.GetTv(value, diffState.Solver, ctx);
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator: Get_Redundant_Instruction_Warnings: line " + lineNumber + ":tv=" + tv + "; value=" + value);
                    if (tv != Tv.ONE) return "";
                }
            }
            string message = "\"" + this._sFlow.Get_Line_Str(lineNumber) + "\" is redundant.";
            AsmDudeToolsStatic.Output_INFO("AsmSimulator: Has_Redundant_Instruction_Warnings: lineNumber " + lineNumber + ": " + message);
            return message;
        }

        public string Get_Register_Value(Rn name, int lineNumber, bool before)
        {
            if (!this.Enabled) return "";

            var state = (before) ? this.Get_State_Before(lineNumber, true, true) : this.Get_State_After(lineNumber, true, true);
            if (state.Bussy)
            {
                return "[Dave, I'm bussy acquiring the state for line " + (lineNumber+1) + "]"; // plus 1 for the lineNumber because lineNumber 0 is shown as lineNumber 1
            }

            Tv[] reg = state.State.GetTvArray_Cached(name);
            if (reg == null)
            {
                AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(Calculate_LOCAL);
                return "[Dave, I'm bussy acquiring the bits for " + name + "]";
            }
            else
            {
                return ToString_LOCAL(reg);
            }

            #region Local Methods

            void Calculate_LOCAL()
            {
                lock (this._updateLock)
                {
                    state.State.GetTvArray(name);
                    this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                }
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
    
        public bool Has_Register_Value(Rn name, State state)
        {
            Tv[] content = state.GetTvArray_Cached(name);
            if (content != null)
            {
                foreach (Tv tv in content)
                {
                    if ((tv == Tv.ONE) || (tv == Tv.ZERO) || (tv == Tv.UNDEFINED)) return true;
                }
            }
            return false;
        }

        /// <summary>If async is false, return the state of the provided lineNumber.
        /// If async is true, returns the state of the provided lineNumber when it exists in the case, 
        /// returns null otherwise and schedules its computation. 
        /// if the state is not computed yet, 
        /// return null and create one in a different thread according to the provided createState boolean.</summary>
        public (State State, bool Bussy) Get_State_After(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled) return (State: null, Bussy: false);

            if (this._cached_States_After.TryGetValue(lineNumber, out State result))
            {
                return (State: result, Bussy: false);
            }
            if (create)
            {
                if (async)
                {
                    if (this._busy)
                    {
                        if (this._scheduled_After.Contains(lineNumber))
                        {
                            AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: busy; and line " + lineNumber + " is already scheduled");
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: busy; scheduling line " + lineNumber);
                            this._scheduled_Before.Add(lineNumber);
                        }
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: going to execute this in a different thread.");
                        AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Calculate_State_After, lineNumber, true);
                    }
                    return (State: null, Bussy: true);
                }
                else
                {
                    this.Calculate_State_After(lineNumber, false);
                    this._cached_States_After.TryGetValue(lineNumber, out State result2);
                    return (State: result2, Bussy: false);
                }
            }
            return (State: null, Bussy: false);
        }

        public (State State, bool Bussy) Get_State_Before(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled) return (State: null, Bussy: false);
            if (this._cached_States_Before.TryGetValue(lineNumber, out State result))
            {
                return (State: result, Bussy: false);
            }
            if (create)
            {
                if (async)
                {
                    if (this._busy)
                    {
                        if (this._scheduled_Before.Contains(lineNumber))
                        {
                            AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_Before: busy; already scheduled line " + lineNumber);
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_Before: busy; scheduling this line " + lineNumber);
                            this._scheduled_Before.Add(lineNumber);
                        }
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_Before: going to execute this in a different thread.");
                        AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Calculate_State_Before, lineNumber, async);
                    }
                    return (State: null, Bussy: true);
                }
                else
                {
                    this.Calculate_State_Before(lineNumber, false);
                    this._cached_States_Before.TryGetValue(lineNumber, out State result2);
                    return (State: result2, Bussy: false);
                }
            }
            return (State: null, Bussy: false);
        }

        #region Private

        private void Calculate_State_Before(int lineNumber, bool async)
        {
            lock (this._updateLock)
            {
                this._busy = true;
                this._cached_States_Before.Remove(lineNumber);
                State state = Get_State_Before(lineNumber, this._dFlow);
                state.Frozen = true;
                this._cached_States_Before.Add(lineNumber, state);
                this._scheduled_Before.Remove(lineNumber);
                this._busy = false;
            }
            if (async)
            {
                if (this._scheduled_Before.Count > 0)
                {
                    int lineNumber2;
                    lock (this._updateLock) //TODO is this lock necessary?
                    {
                        lineNumber2 = this._scheduled_Before.GetEnumerator().Current;
                        this._scheduled_Before.Remove(lineNumber2);
                    }
                    this.Calculate_State_Before(lineNumber2, true);
                }
            }            
        }

        private void Calculate_State_After(int lineNumber, bool async)
        {
            lock (this._updateLock)
            {
                this._busy = true;
                this._cached_States_After.Remove(lineNumber);
                State state = Get_State_After(lineNumber, this._dFlow);
                state.Frozen = true;
                this._cached_States_After.Add(lineNumber, state);
                this._scheduled_After.Remove(lineNumber);
                this._busy = false;
            }
            if (async)
            {
                if (this._scheduled_After.Count > 0)
                {
                    int lineNumber2;
                    lock (this._updateLock) //TODO is this lock necessary?
                    {
                        lineNumber2 = this._scheduled_After.GetEnumerator().Current;
                        this._scheduled_After.Remove(lineNumber2);
                    }
                    this.Calculate_State_After(lineNumber2, true);
                }
            }            
        }

        private State Get_State_After(int lineNumber, DynamicFlow dFlow)
        {
            lock (this._updateLock)
            {
                State result = AsmSim.Tools.Collapse(dFlow.States_After(lineNumber));
                if (result == null)
                {
                    string key = dFlow.Key(lineNumber);
                    return new State(this.Tools, key, key);
                }
                return result;
            }
        }

        private State Get_State_Before(int lineNumber, DynamicFlow dFlow)
        {
            lock (this._updateLock)
            {
                State result = AsmSim.Tools.Collapse(dFlow.States_Before(lineNumber));
                if (result == null)
                {
                    string key = dFlow.Key(lineNumber);
                    return new State(this.Tools, key, key);
                }
                return result;
            }
        }

        #endregion Private
    }
}
