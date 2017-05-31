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
using Amib.Threading;

namespace AsmDude.Tools
{
    public class AsmSimulator
    {
        #region Fields
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly StaticFlow _sFlow;
        private readonly DynamicFlow _dFlow;

        private readonly IDictionary<int, State> _cached_States_Before;
        private readonly IDictionary<int, State> _cached_States_After;

        private readonly ISet<int> _bussy_States_Before;
        private readonly ISet<int> _bussy_States_After;

        public bool Enabled { get; set; }

        private readonly SmartThreadPool _threadPool;
        private readonly SmartThreadPool _threadPool2;
        public readonly AsmSim.Tools Tools;

        private readonly Delay _delay;
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

                this._delay = new Delay(AsmDudePackage.msSleepBeforeAsyncExecution, 10, this._threadPool);
                this._delay.Done_Event += (o, i) => { this._threadPool.QueueWorkItem(this.Reset_Private); };

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
                lock (this._resetLock)
                {
                    this._dFlow.Reset(this._sFlow, true);
                    this._cached_States_After.Clear();
                    this._cached_States_Before.Clear();
                    this._bussy_States_After.Clear();
                    this._bussy_States_Before.Clear();
                }
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


                State stateB = new State(state_Before);
                stateB.UpdateConstName("!B");

                State stateA = new State(state_After);
                stateA.UpdateConstName("!A");

                State diffState = new State(this.Tools, "!0", "!0");
                Context ctx = diffState.Ctx;

                foreach (var v in stateB.Solver.Assertions)
                {
                    diffState.Solver.Assert(v.Translate(ctx) as BoolExpr);
                }
                foreach (var v in stateA.Solver.Assertions)
                {
                    diffState.Solver.Assert(v.Translate(ctx) as BoolExpr);
                }

                foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                {
                    diffState.Solver.Assert(ctx.MkEq(stateB.GetTail(flag).Translate(ctx), stateA.GetTail(flag).Translate(ctx)));
                }
                foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                {
                    diffState.Solver.Assert(ctx.MkEq(stateB.GetTail(reg).Translate(ctx), stateA.GetTail(reg).Translate(ctx)));
                }
                diffState.Solver.Assert(ctx.MkEq(AsmSim.Tools.Mem_Key(stateB.TailKey, ctx), AsmSim.Tools.Mem_Key(stateA.TailKey, ctx)));
                //AsmDudeToolsStatic.Output_INFO(diffState.ToString());

                StateConfig written = Runner.GetUsage_StateConfig(this._sFlow, lineNumber, lineNumber, this.Tools);

                foreach (Flags flag in written.GetFlagOn())
                { 
                    BoolExpr value = ctx.MkEq(stateB.Get(flag).Translate(ctx), stateA.Get(flag).Translate(ctx));
                    Tv tv = ToolsZ3.GetTv(value, diffState.Solver, ctx);
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator: Get_Redundant_Instruction_Warnings: line " + lineNumber + ": tv=" + tv + "; value=" + value);
                    if (tv != Tv.ONE) return "";
                }
                foreach (Rn reg in written.GetRegOn())
                {
                    BoolExpr value = ctx.MkEq(stateB.Get(reg).Translate(ctx), stateA.Get(reg).Translate(ctx));
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

        public void PreCompte_Register_Value(Rn name, int lineNumber, bool before)
        {
            // get the register value and discard the result, the value will be added to the cache
            Get_Register_Value(name, lineNumber, before, false, true);
        }

        public string Get_Register_Value(Rn name, int lineNumber, bool before, bool async, bool create)
        {
            if (!this.Enabled) return "";

            var state = (before) ? this.Get_State_Before(lineNumber, async, create) : this.Get_State_After(lineNumber, async, create);
            if (state.Bussy)
            {
                this._threadPool2.QueueWorkItem(Update_State_And_TvArray_LOCAL);
                return "[I'm bussy and haven't acquired the state of line " + (lineNumber+1) + " yet]"; // plus 1 for the lineNumber because lineNumber 0 is shown as lineNumber 1
            }

            Tv[] reg = state.State.GetTvArray_Cached(name);
            if (reg == null)
            {
                this._threadPool2.QueueWorkItem(Update_TvArray_LOCAL, state.State);
                return "[I'm bussy determining the bits of " + name + "]";
            }
            else
            {
                return ToString_LOCAL(reg);
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
                this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
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
    
        public bool Has_Register_Value(Rn name, int lineNumber, bool before, bool create = false)
        {
            State state = (before) 
                ? this.Get_State_Before(lineNumber, false, false).State
                : this.Get_State_After(lineNumber, false, false).State;
            if (state == null)
            {
                if (create) PreCompte_Register_Value(name, lineNumber, before);
                return false;
            }
            Tv[] content = state.GetTvArray_Cached(name);
            if (content == null)
            {
                if (create) PreCompte_Register_Value(name, lineNumber, before);
                return false;
            }

            foreach (Tv tv in content)
            {
                if ((tv == Tv.ONE) || (tv == Tv.ZERO) || (tv == Tv.UNDEFINED)) return true;
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
                    //this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                }
                this._bussy_States_After.Remove(lineNumber);
            }
            #endregion
        }

        public (State State, bool Bussy) Get_State_Before(int lineNumber, bool async, bool create)
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
                    //this.Line_Updated_Event(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                    this._bussy_States_Before.Remove(lineNumber);
                }
            }
            #endregion
        }
    }
}
