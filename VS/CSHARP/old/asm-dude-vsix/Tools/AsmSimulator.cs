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

namespace AsmDude.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Amib.Threading;
    using AsmDude.SyntaxHighlighting;
    using AsmSim;
    using AsmSim.Mnemonics;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    public sealed class AsmSimulator : IDisposable
    {
        public static readonly int MAX_LINES = 200;

        #region Fields
        private readonly ITextBuffer buffer_;
        private readonly ITagAggregator<AsmTokenTag> aggregator_;
        private readonly StaticFlow sFlow_;
        private DynamicFlow dFlow_;

        private readonly IDictionary<int, State> cached_States_Before_;
        private readonly IDictionary<int, State> cached_States_After_;
        private readonly IDictionary<int, (string message, Mnemonic mnemonic)> usage_Undefined_;
        private readonly IDictionary<int, (string message, Mnemonic mnemonic)> redundant_Instruction_;
        private readonly IDictionary<int, (string message, Mnemonic mnemonic)> unreachable_Instruction_;
        private readonly IDictionary<int, (string message, Mnemonic mnemonic)> syntax_Errors_;
        private readonly ISet<int> isNotImplemented_;

        private readonly ISet<int> bussy_States_Before_;
        private readonly ISet<int> bussy_States_After_;

        public bool Enabled { get; set; }

        private readonly SmartThreadPool threadPool_;
        private SmartThreadPool threadPool2_;
        private IWorkItemResult thread_Result_;
        public readonly AsmSim.Tools Tools;

        private readonly Delay delay_;
        private int last_Changed_LineNumber_ = 0;
        private readonly object resetLock_ = new object();
        private readonly object updateLock_ = new object();
        #endregion

        #region Constuctors

        /// <summary>Factory return singleton</summary>
        public static AsmSimulator GetOrCreate_AsmSimulator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            Contract.Requires(buffer != null);

            AsmSimulator sc()
            {
                return new AsmSimulator(buffer, aggregatorFactory);
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }

        private AsmSimulator(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            this.buffer_ = buffer;
            this.aggregator_ = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, aggregatorFactory);

            this.Enabled = Settings.Default.AsmSim_On;
            if (this.Enabled)
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: switched on");

                this.cached_States_After_ = new Dictionary<int, State>();
                this.cached_States_Before_ = new Dictionary<int, State>();
                this.bussy_States_After_ = new HashSet<int>();
                this.bussy_States_Before_ = new HashSet<int>();
                this.usage_Undefined_ = new Dictionary<int, (string message, Mnemonic mnemonic)>();
                this.redundant_Instruction_ = new Dictionary<int, (string message, Mnemonic mnemonic)>();
                this.unreachable_Instruction_ = new Dictionary<int, (string message, Mnemonic mnemonic)>();
                this.syntax_Errors_ = new Dictionary<int, (string message, Mnemonic mnemonic)>();
                this.isNotImplemented_ = new HashSet<int>();

                this.threadPool_ = AsmDudeTools.Instance.Thread_Pool;
                this.threadPool2_ = new SmartThreadPool(60000, Settings.Default.AsmSim_Number_Of_Threads, 1);
                Dictionary<string, string> settings = new Dictionary<string, string>
                {
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
                    { "timeout", Settings.Default.AsmSim_Z3_Timeout_MS.ToString(CultureInfo.InvariantCulture) },
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
                this.sFlow_ = new StaticFlow(this.Tools);
                this.dFlow_ = new DynamicFlow(this.Tools);

                this.delay_ = new Delay(AsmDudePackage.MsSleepBeforeAsyncExecution, 1000, this.threadPool_);

                // after a delay, reset this AsmSimulator
                this.delay_.Done_Event += (o, i) => { this.Schedule_Reset_Async().ConfigureAwait(false); };

                this.Reset(); // wait to give the system some breathing time
                this.buffer_.ChangedLowPriority += (o, i) =>
                {
                    if (i.Changes.Count > 0)
                    {
                        ITextChange v = i.Changes[0];
                        this.last_Changed_LineNumber_ = i.After.GetLineNumberFromPosition(v.NewPosition);
                        //AsmDudeToolsStatic.Output_INFO("AsmSimulator: changes: newText=" + v.NewText +"; oldText="+v.OldText +"; lineNumber="+ this._last_Changed_LineNumber);
                        this.Reset();
                    }
                };
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: switched off");
            }
        }

        #endregion Constructors

        public event EventHandler<LineUpdatedEventArgs> Line_Updated_Event;

        public event EventHandler<EventArgs> Reset_Done_Event;

        #region IDisposable Support
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AsmSimulator()
        {
            this.Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources

                if (this.cached_States_After_ != null)
                {
                    foreach (KeyValuePair<int, State> v in this.cached_States_After_)
                    {
                        v.Value.Dispose();
                    }
                    this.cached_States_After_.Clear();
                }
                if (this.cached_States_Before_ != null)
                {
                    foreach (KeyValuePair<int, State> v in this.cached_States_Before_)
                    {
                        v.Value.Dispose();
                    }
                    this.cached_States_Before_.Clear();
                }
                if (this.threadPool2_ != null)
                {
                    this.threadPool2_.Dispose();
                    this.threadPool2_ = null;
                }
                if (this.dFlow_ != null)
                {
                    this.dFlow_.Dispose();
                    this.dFlow_ = null;
                }
                this.aggregator_.Dispose();
            }
            // free native resources if there are any.
        }
        #endregion

        #region Reset

        public void Reset(int delay = -1)
        {
            this.delay_.Reset(delay);
        }

        private void Clear()
        {
            foreach (KeyValuePair<int, State> v in this.cached_States_After_)
            {
                v.Value.Dispose();
            }

            this.cached_States_After_.Clear();
            foreach (KeyValuePair<int, State> v in this.cached_States_Before_)
            {
                v.Value.Dispose();
            }

            this.cached_States_Before_.Clear();

            this.bussy_States_After_.Clear();
            this.bussy_States_Before_.Clear();
            this.redundant_Instruction_.Clear();
            this.unreachable_Instruction_.Clear();
            this.usage_Undefined_.Clear();
            this.syntax_Errors_.Clear();
            this.isNotImplemented_.Clear();
        }

        private async System.Threading.Tasks.Task Schedule_Reset_Async()
        {
            System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(() =>
            {
                bool changed;
                lock (this.resetLock_)
                {
                    string program_upcase = this.buffer_.CurrentSnapshot.GetText().ToUpperInvariant();
                    string[] lines_upcase = program_upcase.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

                    #region Restrict input to max number of lines
                    if (lines_upcase.Length > MAX_LINES)
                    {
                        Array.Resize(ref lines_upcase, MAX_LINES);
                    }
                    #endregion

                    StringBuilder sb = new StringBuilder();
                    string pragmaKeyword_upcase = Settings.Default.AsmSim_Pragma_Assume.ToUpperInvariant();
                    int pragmaKeywordLength = pragmaKeyword_upcase.Length;

                    for (int lineNumber = 0; lineNumber < lines_upcase.Length; ++lineNumber)
                    {
                        #region Handle Pragma Assume
                        string line = lines_upcase[lineNumber];
                        int startPos = line.IndexOf(pragmaKeyword_upcase, StringComparison.Ordinal);
                        if (startPos != -1)
                        {
                            line = line.Substring(startPos + pragmaKeywordLength);
                        }
                        #endregion

                        sb.AppendLine(line);
                    }
                    changed = this.sFlow_.Update(sb.ToString());
                }
                if (changed)
                {
                    if ((this.thread_Result_ != null) && !this.thread_Result_.IsCompleted && !this.thread_Result_.IsCanceled)
                    {
                        AsmDudeToolsStatic.Output_INFO("AsmSimulator:Schedule_Reset_Async: cancaling an active reset thread.");
                        this.thread_Result_.Cancel();
                    }
                    this.threadPool2_.Cancel(false);

                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Schedule_Reset_Async: going to start an new reset thread.");
#pragma warning disable IDE0009 // Member access should be qualified.
                    this.thread_Result_ = this.threadPool2_.QueueWorkItem(Reset_Private, WorkItemPriority.Lowest);
#pragma warning restore IDE0009 // Member access should be qualified.
                }
                else
                {
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Schedule_Reset_Async: but static flow update did not result in a different static flow.");
                }
            });
            await task.ConfigureAwait(true);

            void Reset_Private()
            {
                lock (this.resetLock_)
                {
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: Create_StateConfig");
                    this.Tools.StateConfig = this.sFlow_.Create_StateConfig();

                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: dFlow Reset");
                    this.dFlow_.Reset(this.sFlow_, true);

                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: AsmSimulator Clear");
                    this.Clear();

                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: GC");
                    //System.GC.Collect();

                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: Staring PreCalculate_LOCAL");
                    PreCalculate_LOCAL();
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Reset_Private: Done with PreCalculate_LOCAL");

                    this.Reset_Done_Event?.Invoke(this, new EventArgs());
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
                        else
                        {
                            continue1 = false;
                        }

                        int x2 = center + i;
                        if (x2 < last)
                        {
                            yield return x2;
                        }
                        else
                        {
                            continue2 = false;
                        }

                        if (!continue1 && !continue2)
                        {
                            yield break;
                        }
                    }
                }

                void PreCalculate_LOCAL()
                {
                    bool update_Syntax_Error = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Syntax_Errors || Settings.Default.AsmSim_Decorate_Syntax_Errors);
                    bool decorate_Not_Implemented = Settings.Default.AsmSim_On && Settings.Default.AsmSim_Decorate_Unimplemented;

                    bool update_Usage_Undefined = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Usage_Of_Undefined || Settings.Default.AsmSim_Decorate_Usage_Of_Undefined);
                    bool update_Redundant_Instruction = Settings.Default.AsmSim_On && (Settings.Default.AsmSim_Show_Redundant_Instructions || Settings.Default.AsmSim_Decorate_Redundant_Instructions);
                    bool update_Unreachable_Instruction = Settings.Default.AsmSim_On && Settings.Default.AsmSim_Decorate_Unreachable_Instructions;
                    bool update_Known_Register = Settings.Default.AsmSim_On && Settings.Default.AsmSim_Decorate_Registers;

                    foreach (int lineNumber in LineNumber_Centered_LOCAL(this.sFlow_.FirstLineNumber, this.last_Changed_LineNumber_, this.sFlow_.LastLineNumber))
                    {
                        this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.USAGE_OF_UNDEFINED));
                    }
                    foreach (int lineNumber in LineNumber_Centered_LOCAL(this.sFlow_.FirstLineNumber, this.last_Changed_LineNumber_, this.sFlow_.LastLineNumber))
                    {
                        // try
                        {
                            (bool isImplemented, Mnemonic mnemonic, string message) = this.Calculate_Syntax_Errors(lineNumber);
                            if (!isImplemented) // the operation is not implemented
                            {
                                if (decorate_Not_Implemented)
                                {
                                    this.isNotImplemented_.Add(lineNumber);
                                    this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.NOT_IMPLEMENTED));
                                }
                            }
                            else
                            {
                                if (message != null) // found a syntax error
                                {
                                    if (update_Syntax_Error)
                                    {
                                        this.syntax_Errors_.Add(lineNumber, (message, mnemonic));
                                        this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.SYNTAX_ERROR));
                                    }
                                }
                                else // operation is implemented and no syntax error
                                {
                                    if (update_Known_Register)
                                    {
                                        (Mnemonic mnemonic, string[] args) content = this.sFlow_.Get_Line(lineNumber);
                                        foreach (string v in content.args)
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
                                        (string message, Mnemonic mnemonic) info = this.Calculate_Usage_Undefined_Warnings(lineNumber);
                                        if (info.message.Length > 0)
                                        {
                                            this.usage_Undefined_.Add(lineNumber, info);
                                            this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.USAGE_OF_UNDEFINED));
                                        }
                                    }
                                    if (update_Redundant_Instruction)
                                    {
                                        (string message, Mnemonic mnemonic) info = this.Calculate_Redundant_Instruction_Warnings(lineNumber);
                                        if (info.message.Length > 0)
                                        {
                                            this.redundant_Instruction_.Add(lineNumber, info);
                                            this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.REDUNDANT));
                                        }
                                    }
                                    if (update_Unreachable_Instruction)
                                    {
                                        (string message, Mnemonic mnemonic) info = this.Calculate_Unreachable_Instruction_Warnings(lineNumber);
                                        if (info.message.Length > 0)
                                        {
                                            this.unreachable_Instruction_.Add(lineNumber, info);
                                            this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.UNREACHABLE));
                                        }
                                    }
                                }
                            }
                        }
                        //catch (Exception e)
                        // {
                        //    AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:PreCalculate_LOCAL; e={1}", ToString(), e.ToString()));
                        // }
                    }
                }
                #endregion
            }
        }
        #endregion

        #region Not Implemented

        public bool Is_Implemented(int lineNumber)
        {
            return !this.isNotImplemented_.Contains(lineNumber);
        }
        #endregion

        #region Syntax Errors
        public IEnumerable<(int lineNumber, (string message, Mnemonic mnemonic) info)> Syntax_Errors
        {
            get
            {
                foreach (KeyValuePair<int, (string message, Mnemonic mnemonic)> x in this.syntax_Errors_)
                {
                    yield return (x.Key, x.Value);
                }
            }
        }

        public bool Has_Syntax_Error(int lineNumber)
        {
            return this.syntax_Errors_.ContainsKey(lineNumber);
        }

        public (string message, Mnemonic mnemonic) Get_Syntax_Error(int lineNumber)
        {
            return this.syntax_Errors_.TryGetValue(lineNumber, out (string message, Mnemonic mnemonic) info) ? info : (string.Empty, Mnemonic.NONE);
        }

        private (bool isImplemented, Mnemonic mnemonic, string message) Calculate_Syntax_Errors(int lineNumber)
        {
            (string, string, string) dummyKeys = (string.Empty, string.Empty, string.Empty);
            (Mnemonic mnemonic, string[] args) = this.sFlow_.Get_Line(lineNumber);
            OpcodeBase opcodeBase = Runner.InstantiateOpcode(mnemonic, args, dummyKeys, this.Tools);
            if (opcodeBase == null)
            {
                return (isImplemented: false, mnemonic: Mnemonic.NONE, message: null);
            }

            Type type = opcodeBase.GetType();

            if (type == typeof(NotImplemented))
            {
                return (isImplemented: false, mnemonic: mnemonic, message: null);
            }
            else if (type == typeof(DummySIMD))
            {
                return (isImplemented: false, mnemonic: mnemonic, message: null);
            }
            else
            {
                return opcodeBase.IsHalted
                    ? (isImplemented: true, mnemonic: mnemonic, message: opcodeBase.SyntaxError)
                    : (isImplemented: true, mnemonic: mnemonic, message: null);
            }
        }
        #endregion

        #region Usage Undefined
        public IEnumerable<(int lineNumber, (string message, Mnemonic mnemonic) info)> Usage_Undefined
        {
            get
            {
                foreach (KeyValuePair<int, (string message, Mnemonic mnemonic)> x in this.usage_Undefined_)
                {
                    yield return (x.Key, x.Value);
                }
            }
        }

        public bool Has_Usage_Undefined_Warning(int lineNumber)
        {
            return this.usage_Undefined_.ContainsKey(lineNumber);
        }

        public (string message, Mnemonic mnemonic) Get_Usage_Undefined_Warning(int lineNumber)
        {
            return this.usage_Undefined_.TryGetValue(lineNumber, out (string message, Mnemonic mnemonic) info) ? info : (string.Empty, Mnemonic.NONE);
        }

        private (string message, Mnemonic mnemonic) Calculate_Usage_Undefined_Warnings(int lineNumber)
        {
            //lock (this._updateLock)
            {
                State state = this.Get_State_Before(lineNumber, false, false).state;
                if (state == null)
                {
                    return (string.Empty, Mnemonic.NONE);
                }

                (string, string, string) dummyKeys = (string.Empty, string.Empty, string.Empty);
                (Mnemonic mnemonic, string[] args) content = this.sFlow_.Get_Line(lineNumber);
                using (OpcodeBase opcodeBase = Runner.InstantiateOpcode(content.mnemonic, content.args, dummyKeys, this.Tools))
                {
                    string message = string.Empty;
                    Mnemonic mnemonic = Mnemonic.NONE;
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
                                foreach (Tv tv in regContent)
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
                        mnemonic = opcodeBase.mnemonic_;
                        // cleanup
                        opcodeBase.Updates.regular?.Dispose();
                        opcodeBase.Updates.branch?.Dispose();
                    }
                    return (message: message, mnemonic: mnemonic);
                }
            }
        }
        #endregion

        #region Redundant Instruction
        public IEnumerable<(int lineNumber, (string message, Mnemonic mnemonic) info)> Redundant_Instruction
        {
            get
            {
                foreach (KeyValuePair<int, (string message, Mnemonic mnemonic)> x in this.redundant_Instruction_)
                {
                    yield return (x.Key, x.Value);
                }
            }
        }

        public bool Has_Redundant_Instruction_Warning(int lineNumber)
        {
            return this.redundant_Instruction_.ContainsKey(lineNumber);
        }

        public (string message, Mnemonic mnemonic) Get_Redundant_Instruction_Warning(int lineNumber)
        {
            return this.redundant_Instruction_.TryGetValue(lineNumber, out (string message, Mnemonic mnemonic) info) ? info : (string.Empty, Mnemonic.NONE);
        }

        private (string message, Mnemonic mnemonic) Calculate_Redundant_Instruction_Warnings(int lineNumber)
        {
            (Mnemonic mnemonic, string[] args) = this.sFlow_.Get_Line(lineNumber);
            if (mnemonic == Mnemonic.NONE)
            {
                return (string.Empty, Mnemonic.NONE);
            }

            if (mnemonic == Mnemonic.NOP)
            {
                return (string.Empty, Mnemonic.NONE); // do not give a warning for NOP instruction, we know it is redundant...
            }

            {// test if the instustruction is actually implemented properly.
                OpcodeBase opcodeBase = Runner.InstantiateOpcode(mnemonic, args, ("dummy1", "dummy2", "dummy3"), this.Tools);
                if (opcodeBase == null)
                {
                    return (string.Empty, Mnemonic.NONE); // instruction is not implemented: not redundant
                }

                if (opcodeBase.GetType() == typeof(DummySIMD))
                {
                    return (string.Empty, Mnemonic.NONE); // instruction is implemented with a mock: not redundant
                }
            }

            //TODO allow redundant branch points (related to unreachable code)
            if (this.dFlow_.Is_Branch_Point(lineNumber))
            {
                return (string.Empty, Mnemonic.NONE);
            }

            if (this.dFlow_.Is_Merge_Point(lineNumber))
            {
                return (string.Empty, Mnemonic.NONE);
            }

            State state = this.Get_State_After(lineNumber, false, true).state;
            if (state == null)
            {
                return (string.Empty, Mnemonic.NONE);
            }

            string key1 = this.dFlow_.Key(lineNumber);
            string key2 = this.dFlow_.Key_Next(lineNumber);

            lock (this.updateLock_)
            {
                StateConfig stateConfig = this.sFlow_.Create_StateConfig(lineNumber, lineNumber);
                foreach (Flags flag in stateConfig.GetFlagOn())
                {
                    if (!state.Is_Redundant(flag, key1, key2))
                    {
                        return (string.Empty, Mnemonic.NONE);
                    }
                }
                foreach (Rn reg in stateConfig.GetRegOn())
                {
                    if (!state.Is_Redundant(reg, key1, key2))
                    {
                        return (string.Empty, Mnemonic.NONE);
                    }
                }
                if (stateConfig.Mem)
                {
                    if (state.Is_Redundant_Mem(key1, key2) != Tv.ONE)
                    {
                        return (string.Empty, Mnemonic.NONE);
                    }
                }
            }
            string message = "\"" + this.sFlow_.Get_Line_Str(lineNumber) + "\" is redundant.";
            //AsmDudeToolsStatic.Output_INFO("AsmSimulator: Has_Redundant_Instruction_Warnings: lineNumber " + lineNumber + ": " + message);
            return (message: message, mnemonic);
        }
        #endregion

        #region Unreachable Instruction
        public IEnumerable<(int lineNumber, (string message, Mnemonic mnemonic) info)> Unreachable_Instruction
        {
            get
            {
                foreach (KeyValuePair<int, (string message, Mnemonic mnemonic)> x in this.unreachable_Instruction_)
                {
                    yield return (x.Key, x.Value);
                }
            }
        }

        public bool Has_Unreachable_Instruction_Warning(int lineNumber)
        {
            return this.unreachable_Instruction_.ContainsKey(lineNumber);
        }

        public (string message, Mnemonic mnemonic) Get_Unreachable_Instruction_Warning(int lineNumber)
        {
            return this.unreachable_Instruction_.TryGetValue(lineNumber, out (string message, Mnemonic mnemonic) info) ? info : (string.Empty, Mnemonic.NONE);
        }

        private (string message, Mnemonic mnemonic) Calculate_Unreachable_Instruction_Warnings(int lineNumber)
        {
            State state = this.Get_State_Before(lineNumber, false, true).state;
            if (state == null)
            {
                return (string.Empty, Mnemonic.NONE);
            }

            if (state.IsConsistent == Tv.ZERO)
            {
                return (message: "\"" + this.sFlow_.Get_Line_Str(lineNumber) + "\" is unreachable.", mnemonic: this.sFlow_.Get_Line(lineNumber).mnemonic);
            }
            else
            {
                return (string.Empty, Mnemonic.NONE);
            }
        }
        #endregion

        #region Getters for State and Registers
        private void PreCompute_Register_Value(Rn name, int lineNumber, bool before)
        {
            // get the register value and discard the result, the value will be added to the cache
            this.Get_Register_Value(name, lineNumber, before, false, true);
        }

        public string Get_Register_Value_If_Already_Computed(Rn name, int lineNumber, bool before, NumerationEnum numeration)
        {
            if (!this.Enabled)
            {
                return string.Empty;
            }

            (State state, bool bussy) state = before ? this.Get_State_Before(lineNumber, false, false) : this.Get_State_After(lineNumber, false, false);
            if (state.bussy)
            {
                return null;
            }

            if (state.state == null)
            {
                return null;
            }

            Tv[] reg = state.state.GetTvArray_Cached(name);
            if (reg == null)
            {
                return null;
            }

            switch (numeration)
            {
                case NumerationEnum.HEX: return ToolsZ3.ToStringHex(reg);
                case NumerationEnum.BIN: return ToolsZ3.ToStringBin(reg);
                case NumerationEnum.DEC: return ToolsZ3.ToStringDec(reg);
                case NumerationEnum.OCT: return ToolsZ3.ToStringOct(reg);
                default: return ToolsZ3.ToStringHex(reg);
            }
        }

        public string Get_Flag_Value_If_Already_Computed(Flags name, int lineNumber, bool before)
        {
            if (!this.Enabled)
            {
                return string.Empty;
            }

            (State state, bool bussy) state = before ? this.Get_State_Before(lineNumber, false, false) : this.Get_State_After(lineNumber, false, false);
            if (state.bussy)
            {
                return null;
            }

            if (state.state == null)
            {
                return null;
            }

            Tv? content = state.state.GetTv_Cached(name);
            if (content == null)
            {
                return null;
            }

            return content.Value.ToString();
        }

        public string Get_Register_Value_and_Block(Rn name, int lineNumber, bool before, NumerationEnum numeration)
        {
            if (!this.Enabled)
            {
                return null;
            }

            (State state, bool bussy) state = before ? this.Get_State_Before(lineNumber, false, true) : this.Get_State_After(lineNumber, false, true);
            if (state.state == null)
            {
                return null;
            }

            Tv[] reg = state.state.GetTvArray_Cached(name);
            if (reg == null)
            {
                reg = state.state.GetTvArray(name);
            }

            if (reg == null)
            {
                return null;
            }

            switch (numeration)
            {
                case NumerationEnum.HEX: return ToolsZ3.ToStringHex(reg);
                case NumerationEnum.BIN: return ToolsZ3.ToStringBin(reg);
                case NumerationEnum.DEC: return ToolsZ3.ToStringDec(reg);
                case NumerationEnum.OCT: return ToolsZ3.ToStringOct(reg);
                default: return ToolsZ3.ToStringHex(reg);
            }
        }

        public string Get_Flag_Value_and_Block(Flags name, int lineNumber, bool before)
        {
            if (!this.Enabled)
            {
                return null;
            }

            (State state, bool bussy) state = before ? this.Get_State_Before(lineNumber, false, true) : this.Get_State_After(lineNumber, false, true);
            if (state.state == null)
            {
                return null;
            }

            Tv? content = state.state.GetTv_Cached(name);
            if (content == null)
            {
                content = state.state.GetTv(name);
            }

            if (content == null)
            {
                return null;
            }

            return content.Value.ToString();
        }

        public (string value, bool bussy) Get_Register_Value(Rn name, int lineNumber, bool before, bool async, bool create, NumerationEnum numeration = NumerationEnum.BIN)
        {
            if (!this.Enabled)
            {
                return (string.Empty, false);
            }

            (State state, bool bussy) state = before ? this.Get_State_Before(lineNumber, async, create) : this.Get_State_After(lineNumber, async, create);
            if (state.bussy)
            {
                this.threadPool2_.QueueWorkItem(Update_State_And_TvArray_LOCAL);
                this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                return ("[I'm bussy and haven't acquired the state of line " + (lineNumber + 1) + " yet.]", true); // plus 1 for the lineNumber because lineNumber 0 is shown as lineNumber 1
            }
            if (state.state == null)
            {
                this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
                return ("[I'm confused, sorry about that.]", true);
            }

            Tv[] reg = state.state.GetTvArray_Cached(name);
            if (reg == null)
            {
                this.threadPool2_.QueueWorkItem(Update_TvArray_LOCAL, state.state);
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
                (State state, bool bussy) state2 = before ? this.Get_State_Before(lineNumber, false, true) : this.Get_State_After(lineNumber, false, true);
                if (state2.state != null)
                {
                    Update_TvArray_LOCAL(state2.state);
                }
            }

            void Update_TvArray_LOCAL(State state2)
            {
                state2.Update_TvArray_Cached(name);
                this.Line_Updated_Event?.Invoke(this, new LineUpdatedEventArgs(lineNumber, AsmMessageEnum.DECORATE_REG));
            }

            string ToString_LOCAL(Tv[] array)
            {
                switch (numeration)
                {
                    case NumerationEnum.HEX: return ToolsZ3.ToStringHex(array);
                    case NumerationEnum.BIN: return ToolsZ3.ToStringBin(array);
                    case NumerationEnum.DEC: return ToolsZ3.ToStringDec(array);
                    case NumerationEnum.OCT: return ToolsZ3.ToStringOct(array);
                    default: return ToolsZ3.ToStringHex(array);
                }
            }
            #endregion
        }

        public (bool hasValue, bool bussy) Has_Register_Value(Rn name, int lineNumber, bool before, bool create = false)
        {
            try
            {
                if (this.syntax_Errors_.ContainsKey(lineNumber))
                {
                    return (hasValue: false, bussy: false);
                }

                if (this.isNotImplemented_.Contains(lineNumber))
                {
                    return (hasValue: false, bussy: false);
                }

                (State state, bool bussy) state = before
                    ? this.Get_State_Before(lineNumber, false, false)
                    : this.Get_State_After(lineNumber, false, false);
                if (state.bussy)
                {
                    return (false, true);
                }
                else if (state.state == null)
                {
                    if (create)
                    {
                        this.PreCompute_Register_Value(name, lineNumber, before);
                    }

                    return (false, true);
                }
                else
                {
                    Tv[] content = state.state.GetTvArray_Cached(name);
                    if (content == null)
                    {
                        if (create)
                        {
                            this.PreCompute_Register_Value(name, lineNumber, before);
                        }

                        return (false, true);
                    }
                    foreach (Tv tv in content)
                    {
                        if ((tv == Tv.ONE) || (tv == Tv.ZERO) || (tv == Tv.UNDEFINED) || (tv == Tv.INCONSISTENT))
                        {
                            return (true, false);
                        }
                    }
                    return (false, false);
                }
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Has_Register_Value; e={1}", this.ToString(), e.ToString()));
                return (false, false);
            }
        }

        /// <summary>If async is false, return the state of the provided lineNumber.
        /// If async is true, returns the state of the provided lineNumber when it exists in the case,
        /// returns null otherwise and schedules its computation.
        /// if the state is not computed yet,
        /// return null and create one in a different thread according to the provided createState boolean.</summary>
        private (State state, bool bussy) Get_State_After(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled)
            {
                return (state: null, bussy: false);
            }

            if (this.cached_States_After_.TryGetValue(lineNumber, out State result))
            {
                return (state: result, bussy: false);
            }
            if (this.bussy_States_After_.Contains(lineNumber))
            {
                return (state: null, bussy: true);
            }
            if (create)
            {
                if (async)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: going to execute this in a different thread.");
                    this.threadPool2_.QueueWorkItem(Calculate_State_After_LOCAL, WorkItemPriority.Lowest);
                    return (state: null, bussy: true);
                }
                else
                {
                    Calculate_State_After_LOCAL();
                    this.cached_States_After_.TryGetValue(lineNumber, out State result2);
                    return (state: result2, bussy: false);
                }
            }
            return (state: null, bussy: false);

            #region Local Methods
            void Calculate_State_After_LOCAL()
            {
                this.bussy_States_After_.Add(lineNumber);

                State state = null;
                List<State> statesBefore = new List<State>(this.dFlow_.Create_States_After(lineNumber));
                switch (statesBefore.Count)
                {
                    case 0:
                        string key = this.dFlow_.Key(lineNumber);
                        state = new State(this.Tools, key, key);
                        break;
                    case 1:
                        state = statesBefore[0];
                        break;
                    default:
                        state = Tools.Collapse(statesBefore);
                        foreach (State v in statesBefore)
                        {
                            v.Dispose();
                        }

                        break;
                }
                state.Frozen = true;

                //lock (this._updateLock)
                {
                    if (this.cached_States_After_.ContainsKey(lineNumber))
                    {
                        this.cached_States_After_[lineNumber].Dispose();
                        this.cached_States_After_.Remove(lineNumber);
                    }
                    this.cached_States_After_.Add(lineNumber, state);
                    this.bussy_States_After_.Remove(lineNumber);
                }
            }
            #endregion
        }

        private (State state, bool bussy) Get_State_Before(int lineNumber, bool async, bool create)
        {
            if (!this.Enabled)
            {
                return (state: null, bussy: false);
            }

            if (this.cached_States_Before_.TryGetValue(lineNumber, out State result))
            {
                return (state: result, bussy: false);
            }
            if (this.bussy_States_Before_.Contains(lineNumber))
            {
                return (state: null, bussy: true);
            }
            if (create)
            {
                if (async)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_Before: going to execute this in a different thread.");
                    this.threadPool2_.QueueWorkItem(Create_State_Before_LOCAL, WorkItemPriority.Lowest);
                    return (state: null, bussy: true);
                }
                else
                {
                    Create_State_Before_LOCAL();
                    this.cached_States_Before_.TryGetValue(lineNumber, out State result2);
                    return (state: result2, bussy: false);
                }
            }
            return (state: null, bussy: false);

            #region Local Methods
            void Create_State_Before_LOCAL()
            {
                this.bussy_States_Before_.Add(lineNumber);

                State state = null;
                List<State> statesBefore = new List<State>(this.dFlow_.Create_States_Before(lineNumber));
                switch (statesBefore.Count)
                {
                    case 0:
                        string key = this.dFlow_.Key(lineNumber);
                        state = new State(this.Tools, key, key);
                        break;
                    case 1:
                        state = statesBefore[0];
                        break;
                    default:
                        state = Tools.Collapse(statesBefore);
                        foreach (State v in statesBefore)
                        {
                            v.Dispose();
                        }

                        break;
                }
                state.Frozen = true;

                //lock (this._updateLock)
                {
                    if (this.cached_States_Before_.ContainsKey(lineNumber))
                    {
                        this.cached_States_Before_[lineNumber].Dispose();
                        this.cached_States_Before_.Remove(lineNumber);
                    }
                    this.cached_States_Before_.Add(lineNumber, state);
                    this.bussy_States_Before_.Remove(lineNumber);
                }
            }
            #endregion
        }

        #endregion

        public (IEnumerable<Rn> readReg, IEnumerable<Rn> writeReg, Flags readFlag, Flags writeFlag, bool memRead, bool memWrite) Get_Usage(int lineNumber)
        {
            if (this.sFlow_.HasLine(lineNumber))
            {
                (Mnemonic mnemonic, string[] args) = this.sFlow_.Get_Line(lineNumber);
                (string, string, string) dummyKeys = ("0", "1", "1B");
                OpcodeBase opcode = Runner.InstantiateOpcode(mnemonic, args, dummyKeys, this.Tools);
                if (opcode != null)
                {
                    return (
                        readReg: opcode.RegsReadStatic,
                        writeReg: opcode.RegsWriteStatic,
                        readFlag: opcode.FlagsReadStatic,
                        writeFlag: opcode.FlagsWriteStatic,
                        memRead: opcode.MemReadStatic,
                        memWrite: opcode.MemWriteStatic);
                }
            }
            return (
                readReg: Enumerable.Empty<Rn>(),
                writeReg: Enumerable.Empty<Rn>(),
                readFlag: Flags.NONE,
                writeFlag: Flags.NONE,
                memRead: false,
                memWrite: false);
        }

        #region Getters for usage
        #endregion
    }
}
