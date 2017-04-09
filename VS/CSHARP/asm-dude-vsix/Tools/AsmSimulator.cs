using AsmSimZ3;
using AsmTools;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AsmDude.Tools
{
    public class AsmSimulator
    {
        private readonly ITextBuffer _buffer;
        private readonly IBufferTagAggregatorFactoryService _aggregatorFactory;
        private readonly RunnerZ3 _runner;
        private readonly CFlow _cflow;
        private readonly IDictionary<int, IState_R> _cachedStates;

        private object _updateLock = new object();

        private bool _busy;
        private ISet<int> _scheduled;

        // Declare the event using EventHandler<T>
        public event EventHandler<CustomEventArgs> Simulate_Done_Event;
        public bool Is_Enabled { get; set; }

        private AsmSimulator(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory, AsmParameters p = null)
        {
            this._buffer = buffer;
            this._aggregatorFactory = aggregatorFactory;
            this._cflow = new CFlow(this._buffer.CurrentSnapshot.GetText());
            this._cachedStates = new Dictionary<int, IState_R>();
            this.Is_Enabled = true;
            this._scheduled = new HashSet<int>();

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
                { "proof", "false" }         // enable proof generation
            };
            Context ctx = new Context(settings);
            if (p==null) p = new AsmParameters();
            bool useForward = true;
            bool useUndef = true;
            this._runner = new RunnerZ3(p, ctx, useForward, useUndef, true)
            {
                OutputPane = AsmDudeToolsStatic.GetOutputPane()
            };
            this._buffer.Changed += this.Buffer_Changed;
        }

        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("AsmSimulation:Buffer_Changed");

            bool nonSpaceAdded = false;
            foreach (var c in e.Changes)
            {
                if (c.NewText != " ")
                {
                    nonSpaceAdded = true;
                }
            }
            if (!nonSpaceAdded) return;

            string sourceCode = this._buffer.CurrentSnapshot.GetText();
            //IState_R state = this._runner.ExecuteTree_PseudoBackward(sourceCode, lineNumber, 3);
            if (this._cflow.Update(sourceCode))
            {
                this._cachedStates.Clear();
                //int maxStepBack = 6;
                //this._runner.ExecuteTree_Backward(this._cflow, this._cflow.NLines - 1, maxStepBack);
            }

        }

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

        public void UpdateState(int lineNumber)
        {
            if (!(this._cachedStates.ContainsKey(lineNumber)))
            {
                IState_R result = this._runner.ExecuteTree_Backward(this._cflow, lineNumber, 6);
                this._cachedStates.Add(lineNumber, result);
                On_Simulate_Done_Event(new CustomEventArgs("Simulate has finished"));
            }
        }

        public string GetRegisterValue(Rn name, IState_R state)
        {
            if (state == null) return "";
            Tv5[] reg = state.GetTv5Array(name);
            return string.Format("{0} = {1}", ToolsZ3.ToStringBin(reg), ToolsZ3.ToStringHex(reg));
        }

        /// <summary>Return the state of the provided lineNumber, if the state is not computed yet, 
        /// return null and create one in a different thread according to the provided createState boolean.</summary>
        public IState_R GetState(int lineNumber, bool createState = false)
        {
            if (this._cachedStates.TryGetValue(lineNumber, out IState_R result))
            {
                return result;
            }
            if (createState)
            {
                if (this._busy)
                {
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:Simulate_Delayed: busy; scheduling this line " + lineNumber);
                    this._scheduled.Add(lineNumber);
                }
                else
                {
                    AsmDudeToolsStatic.Output_INFO("AsmSimulator:GetState: going to execute this in a different thread.");
                    AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Simulate, lineNumber);
                }
            }
            return null;
        }

        private void Simulate(int lineNumber)
        {
            if (!this.Is_Enabled) return;

            #region Payload
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;

                this._busy = true;
                this._scheduled.Remove(lineNumber);
                IState_R result = this._runner.ExecuteTree_Backward(this._cflow, lineNumber, 6);
                this._cachedStates.Remove(lineNumber);
                this._cachedStates.Add(lineNumber, result);
                this._busy = false;
                On_Simulate_Done_Event(new CustomEventArgs("Simulate has finished"));

                AsmDudeToolsStatic.Print_Speed_Warning(time1, "AsmSimulator");
            }
            #endregion Payload

            if (this._scheduled.Count > 0)
            {
                int lineNumber2;
                lock (this._updateLock)
                {
                    lineNumber2 = this._scheduled.GetEnumerator().Current;
                    this._scheduled.Remove(lineNumber2);
                }
                this.Simulate(lineNumber2);
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
