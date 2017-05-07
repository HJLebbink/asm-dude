using AsmDude.SyntaxHighlighting;
using AsmSimZ3;
using AsmSimZ3.Mnemonics_ng;
using AsmTools;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.Z3;
using System;
using System.Collections.Generic;

namespace AsmDude.Tools
{
    public class AsmSimulator
    {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly CFlow _cflow;
        private readonly IDictionary<int, State2> _cached_States_After;
        private readonly IDictionary<int, State2> _cached_States_Before;

        public readonly AsmSimZ3.Mnemonics_ng.Tools Tools;
        private object _updateLock = new object();

        private bool _busy;
        private ISet<int> _scheduled_After;
        private ISet<int> _scheduled_Before;

        // Declare the event using EventHandler<T>
        public event EventHandler<CustomEventArgs> Simulate_Done_Event;
        public bool Is_Enabled { get; set; }

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
                this._cflow = new CFlow(this._buffer.CurrentSnapshot.GetText());
                this._cached_States_After = new Dictionary<int, AsmSimZ3.Mnemonics_ng.State2>();
                this._cached_States_Before = new Dictionary<int, AsmSimZ3.Mnemonics_ng.State2>();
                this.Is_Enabled = true;
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
                this.Tools = new AsmSimZ3.Mnemonics_ng.Tools(new Context(settings));
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
                this._buffer.Changed += this.Buffer_Changed;
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO("AsmSimulator:AsmSimulator: swithed off");
            }
        }

        public static (bool IsImplemented, Mnemonic Mnemonic, string Message) GetSyntaxInfo(string line, AsmSimZ3.Mnemonics_ng.Tools tools)
        {
            var dummyKeys = ("", "", "", "");
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);
            if (opcodeBase == null) return (IsImplemented: false, Mnemonic: Mnemonic.NONE, Message: null);

            if (opcodeBase.GetType() == typeof(NotImplemented))
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

        public static (bool IsImplemented, Mnemonic Mnemonic, string Message) GetSemanticInfo(string line, AsmSimZ3.Mnemonics_ng.Tools tools)
        {
            var dummyKeys = ("", "", "", "");
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);
            if (opcodeBase == null) return (IsImplemented: false, Mnemonic: Mnemonic.NONE, Message: null);

            if (opcodeBase.GetType() == typeof(NotImplemented))
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

        public static string Get_Undefined_Warnings(string line, AsmSimZ3.Mnemonics_ng.Tools tools, State2 state)
        {
            var dummyKeys = ("", "", "", "");
            var content = AsmSourceTools.ParseLine(line);
            var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, tools);

            string message = "";
            if (opcodeBase != null)
            {
                foreach (Flags flag in FlagTools.GetFlags(opcodeBase.FlagsReadStatic))
                {
                    if (tools.StateConfig.IsFlagOn(flag))
                    {
                        if (state.IsUndefined(flag))
                        {
                            message = message + "Flag " + flag + " is undefined; ";
                        }
                    }
                }
                foreach (Rn reg in opcodeBase.RegsReadStatic)
                {
                    if (tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(reg)))
                    {
                        if (state.IsUndefined(reg))
                        {
                            message = message + "Register " + reg + " has undefined content";
                        }
                    }
                }
            }
            return message;
        }

        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("AsmSimulation:Buffer_Changed");

            bool nonSpaceAdded = false;
            foreach (var c in e.Changes)
            {
                if (c.NewText != " ") nonSpaceAdded = true;
            }
            if (!nonSpaceAdded) return;

            string sourceCode = this._buffer.CurrentSnapshot.GetText();
            if (this._cflow.Update(sourceCode))
            {
                this._cached_States_After.Clear();
                this._cached_States_Before.Clear();
            }
        }

        public void UpdateState(int lineNumber)
        {
            if (!(this._cached_States_After.ContainsKey(lineNumber)))
            {
                int nSteps = Settings.Default.AsmSim_Number_Of_Steps;
                AsmSimZ3.Mnemonics_ng.ExecutionTree tree = Runner.Construct_ExecutionTree_Backward(this._cflow, lineNumber, nSteps, this.Tools);

                State2 state = tree.EndState;
                this._cached_States_After.Add(lineNumber, state);
                On_Simulate_Done_Event(new CustomEventArgs("Simulate has finished"));
            }
        }

        public string GetRegisterValue(Rn name, State2 state)
        {
            if (state == null) return "";
            Tv5[] reg = state.GetTv5Array(name);

            if (false)
            {
                return string.Format("{0} = {1}\n{2}", ToolsZ3.ToStringBin(reg), ToolsZ3.ToStringHex(reg), state.ToStringConstraints("") + state.ToStringRegs("") + state.ToStringFlags(""));
            }
            else
            {
                return string.Format("{0} = {1}", ToolsZ3.ToStringBin(reg), ToolsZ3.ToStringHex(reg));
            }
        }

        public bool HasRegisterValue(Rn name, State2 state)
        {
            Tv5[] content = state.GetTv5Array(name, true);
            foreach (Tv5 tv in content)
            {
                if ((tv == Tv5.ONE) || (tv == Tv5.ZERO) || (tv == Tv5.UNDEFINED)) return true;
            }
            return false;
        }

        /// <summary>Return the state of the provided lineNumber, if the state is not computed yet, 
        /// return null and create one in a different thread according to the provided createState boolean.</summary>
        public State2 Get_State_After(int lineNumber, bool createState = false)
        {
            if (this._cached_States_After.TryGetValue(lineNumber, out State2 result))
            {
                return result;
            }
            if (createState)
            {
                if (this._busy)
                {
                    if (this._scheduled_After.Contains(lineNumber))
                    {
                        AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: busy; already scheduled line " + lineNumber);
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: busy; scheduling this line " + lineNumber);
                        this._scheduled_Before.Add(lineNumber);
                    }
                }
                else
                {
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Get_State_After: going to execute this in a different thread.");
                    AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Simulate_After, lineNumber);
                }
            }
            return null;
        }

        public State2 Get_State_Before(int lineNumber, bool createState = false)
        {
            if (this._cached_States_Before.TryGetValue(lineNumber, out State2 result))
            {
                return result;
            }
            if (createState)
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
                    AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Simulate_Before, lineNumber);
                }
            }
            return null;
        }

        public State2 Create_State_After(int lineNumber)
        {
            if (this._cached_States_After.TryGetValue(lineNumber, out State2 state))
            {
                return state;
            }
            this._scheduled_Before.Remove(lineNumber);
            this.Tools.StateConfig = Runner.GetUsage_StateConfig(this._cflow, 0, this._cflow.LastLineNumber, this.Tools);
            AsmSimZ3.Mnemonics_ng.ExecutionTree tree = Runner.Construct_ExecutionTree_Backward(this._cflow, lineNumber, 4, this.Tools);
            this._cached_States_After.Remove(lineNumber);
            State2 result = tree.EndState;
            this._cached_States_After.Add(lineNumber, result);
            return result;
        }

        private void Simulate_Before(int lineNumber)
        {
            if (!this.Is_Enabled) return;

            #region Payload
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;

                this._busy = true;
                this._scheduled_Before.Remove(lineNumber);
                this.Tools.StateConfig = Runner.GetUsage_StateConfig(this._cflow, 0, this._cflow.LastLineNumber, this.Tools);

                //TODO get the previous state
                AsmSimZ3.Mnemonics_ng.ExecutionTree tree = Runner.Construct_ExecutionTree_Backward(this._cflow, lineNumber, 4, this.Tools);
                this._cached_States_Before.Remove(lineNumber);
                this._cached_States_Before.Add(lineNumber, tree.EndState);


                this._busy = false;
                On_Simulate_Done_Event(new CustomEventArgs("Simulate has finished"));

                AsmDudeToolsStatic.Print_Speed_Warning(time1, "AsmSimulator");
            }
            #endregion Payload

            if (this._scheduled_Before.Count > 0)
            {
                int lineNumber2;
                lock (this._updateLock)
                {
                    lineNumber2 = this._scheduled_Before.GetEnumerator().Current;
                    this._scheduled_Before.Remove(lineNumber2);
                }
                this.Simulate_Before(lineNumber2);
            }
        }

        private void Simulate_After(int lineNumber)
        {
            if (!this.Is_Enabled) return;

            #region Payload
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;

                this._busy = true;
                this._scheduled_Before.Remove(lineNumber);
                this.Tools.StateConfig = Runner.GetUsage_StateConfig(this._cflow, 0, this._cflow.LastLineNumber, this.Tools);
                AsmSimZ3.Mnemonics_ng.ExecutionTree tree = Runner.Construct_ExecutionTree_Backward(this._cflow, lineNumber, 4, this.Tools);
                this._cached_States_After.Remove(lineNumber);
                this._cached_States_After.Add(lineNumber, tree.EndState);


                this._busy = false;
                On_Simulate_Done_Event(new CustomEventArgs("Simulate has finished"));

                AsmDudeToolsStatic.Print_Speed_Warning(time1, "AsmSimulator");
            }
            #endregion Payload

            if (this._scheduled_Before.Count > 0)
            {
                int lineNumber2;
                lock (this._updateLock)
                {
                    lineNumber2 = this._scheduled_Before.GetEnumerator().Current;
                    this._scheduled_Before.Remove(lineNumber2);
                }
                this.Simulate_After(lineNumber2);
            }
        }

        private void On_Simulate_Done_Event(CustomEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber un-subscribes
            // immediately after the null check and before the event is raised.
            EventHandler<CustomEventArgs> handler = Simulate_Done_Event;

            // Event will be null if there are no subscribers
            if (handler != null)
            {
                // Format the string to send inside the CustomEventArgs parameter
                e.Message += String.Format(" at {0}", DateTime.Now.ToString());

                // Use the () operator to raise the event.
                handler(this, e);
            }
        }
    }
}
