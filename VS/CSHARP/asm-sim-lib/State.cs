// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

namespace AsmSim
{
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
    using System.Diagnostics.Contracts;
    using System.Text;
    using AsmTools;
    using Microsoft.Z3;

    public class State : IDisposable
    {
        #region Fields
        public static readonly bool ADD_COMPUTED_VALUES = true;

        private readonly Tools _tools;

        public Tools Tools { get { return this._tools; } }

        private readonly Context _ctx;

        public Context Ctx { get { return this._ctx; } }

        public Solver Solver { get; private set; }

        public Solver Solver_U { get; private set; }

        private bool solver_Dirty = false;
        private bool solver_U_Dirty = false;

        private string _warningMessage;
        private string _synstaxErrorMessage;

        public bool IsHalted { get; private set; }

        public string HeadKey = null;
        public string TailKey = null;

        private bool _frozen;
        private readonly IDictionary<Rn, Tv[]> _cached_Reg_Values;
        private readonly IDictionary<Flags, Tv> _cached_Flag_Values;

        private readonly object _ctxLock = new object();

        private BranchInfoStore _branchInfoStore;

        public BranchInfoStore BranchInfoStore { get { return this._branchInfoStore; } }
        #endregion

        #region Constructors

        /// <summary>Private constructor for internal use</summary>
        private State(Tools tools)
        {
            this._tools = new Tools(tools);
            this._ctx = new Context(this._tools.Settings); // housekeeping in Dispose();
            this.Solver = MakeSolver(this._ctx);
            this.Solver_U = MakeSolver(this._ctx);
            this._branchInfoStore = new BranchInfoStore(this._ctx);
            this._cached_Reg_Values = new Dictionary<Rn, Tv[]>();
            this._cached_Flag_Values = new Dictionary<Flags, Tv>();
        }

        public static Solver MakeSolver(Context ctx)
        {
            Contract.Requires(ctx != null);

            Solver s = ctx.MkSolver(ctx.MkTactic("qfbv"));
            //Params p = ctx.MkParams();
            //p.Add("mbqi", false); // use Model-based Quantifier Instantiation
            //s.Parameters = p;
            return s;
        }

        /// <summary>Regular constructor</summary>
        public State(Tools tools, string tailKey, string headKey)
            : this(tools)
        {
            this.TailKey = tailKey;
            this.HeadKey = headKey;
        }

        /// <summary>Copy constructor</summary>
        public State(State other)
            : this(other.Tools)
        {
            Contract.Requires(other != null);
            lock (this._ctxLock)
            {
                other.Copy(this);
            }
        }

        /// <summary>Copy this state to the provided other State</summary>
        public void Copy(State other)
        {
            Contract.Requires(other != null);

            if (this == other)
            {
                return;
            }

            lock (this._ctxLock)
            {
                other.TailKey = this.TailKey;
                other.HeadKey = this.HeadKey;
                this.UndefGrounding = false;

                Context ctx = other._ctx;
                {
                    other.Solver.Reset();
                    other.Assert(this.Solver.Assertions, false, true);
                    other.solver_Dirty = true;
                }
                {
                    other.Solver_U.Reset();
                    other.Assert(this.Solver_U.Assertions, true, true);
                    other.solver_U_Dirty = true;
                }
                {
                    other.BranchInfoStore.Clear();
                    foreach (BranchInfo v in this.BranchInfoStore.Values)
                    {
                        other.BranchInfoStore.Add(v, true);
                    }
                }
            }
        }

        /// <summary>Merge and Diff constructor</summary>
        public State(State state1, State state2, bool merge)
            : this(state1.Tools)
        {
            Contract.Requires(state1 != null);
            Contract.Requires(state2 != null);

            if (merge)
            {
                this.MergeConstructor(state1, state2);
            }
            else
            {
                this.DiffConstructor(state1, state2);
            }
        }

        /// <summary>Merge Constructor Method</summary>
        private void MergeConstructor(State state1, State state2)
        {
            #region Handle Inconsistent states
            {
                bool consistent1 = state1.IsConsistent == Tv.ONE;
                bool consistent2 = state2.IsConsistent == Tv.ONE;

                if (!consistent1 && !consistent2)
                {
                    Console.WriteLine("WARNING: State: merge constructor: states have to be consistent. state1 consistent = " + consistent1 + "; state2 consistent = " + consistent2);
                }
                if (!consistent1)
                {
                    lock (this._ctxLock)
                    {
                        state2.Copy(this);
                    }

                    return;
                }
                if (!consistent2)
                {
                    lock (this._ctxLock)
                    {
                        state1.Copy(this);
                    }

                    return;
                }
            }
            #endregion

            #region Prepare States
            state1.UndefGrounding = false;
            state2.UndefGrounding = false;

            state1.Simplify();
            state2.Simplify();
            #endregion

            lock (this._ctxLock)
            {
                Context ctx = this._ctx;

                this._branchInfoStore = BranchInfoStore.RetrieveSharedBranchInfo(state1.BranchInfoStore, state2.BranchInfoStore, ctx);

                // merge the contents of both solvers
                {
                    ISet<BoolExpr> mergedContent = new HashSet<BoolExpr>();
#pragma warning disable DisposableFixer // Undisposed ressource.
                    foreach (BoolExpr b in state1.Solver.Assertions)
                    {
                        mergedContent.Add(b.Translate(ctx) as BoolExpr);
                    }

                    foreach (BoolExpr b in state2.Solver.Assertions)
                    {
                        mergedContent.Add(b.Translate(ctx) as BoolExpr);
                    }

                    foreach (BoolExpr b in mergedContent)
                    {
                        this.Solver.Assert(b);
                    }

                    ISet<BoolExpr> mergedContent_U = new HashSet<BoolExpr>();
                    foreach (BoolExpr b in state1.Solver_U.Assertions)
                    {
                        mergedContent_U.Add(b.Translate(ctx) as BoolExpr);
                    }

                    foreach (BoolExpr b in state2.Solver_U.Assertions)
                    {
                        mergedContent_U.Add(b.Translate(ctx) as BoolExpr);
                    }

                    foreach (BoolExpr b in mergedContent_U)
                    {
                        this.Solver_U.Assert(b);
                    }
#pragma warning restore DisposableFixer // Undisposed resource.
                }

                // merge the head and tail
                {
                    if (state1.HeadKey == state2.HeadKey)
                    {
                        this.HeadKey = state1.HeadKey;
                    }
                    else
                    {
                        this.HeadKey = Tools.CreateKey(this.Tools.Rand);
                        string head1 = state1.HeadKey;
                        string head2 = state2.HeadKey;

                        using (StateUpdate stateUpdateForward = new StateUpdate("!ERROR_1", this.HeadKey, this.Tools))
                        {
                            BoolExpr dummyBranchCondttion = ctx.MkBoolConst("DymmyBC" + this.HeadKey);
                            foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                            {
                                stateUpdateForward.Set(reg, ctx.MkITE(dummyBranchCondttion, Tools.Create_Key(reg, head1, ctx), Tools.Create_Key(reg, head2, ctx)) as BitVecExpr);
                            }
                            foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                            {
                                stateUpdateForward.Set(flag, ctx.MkITE(dummyBranchCondttion, Tools.Create_Key(flag, head1, ctx), Tools.Create_Key(flag, head2, ctx)) as BoolExpr);
                            }
                            stateUpdateForward.Set_Mem(ctx.MkITE(dummyBranchCondttion, Tools.Create_Mem_Key(head1, ctx), Tools.Create_Mem_Key(head2, ctx)) as ArrayExpr);

                            this.Update_Forward(stateUpdateForward);
                        }
                    }
                    if (state1.TailKey == state2.TailKey)
                    {
                        this.TailKey = state1.TailKey;
                    }
                    else
                    {
                        this.TailKey = Tools.CreateKey(this.Tools.Rand);
                        //TODO does merging the tail make any sense?
                    }
                }
            }
        }

        private void DiffConstructor(State state1, State state2)
        {
            //TODO
        }

        #endregion

        #region Setters

        public void Assert(BoolExpr expr, bool undef, bool translate)
        {
            if (expr == null)
            {
                return;
            }

            if (translate)
            {
                BoolExpr t = expr.Translate(this._ctx) as BoolExpr;
                if (undef)
                {
                    this.Solver_U.Assert(t);
                }
                else
                {
                    this.Solver.Assert(t);
                }
            }
            else
            {
                if (undef)
                {
                    this.Solver_U.Assert(expr);
                }
                else
                {
                    this.Solver.Assert(expr);
                }
            }

            if (undef)
            {
                this.solver_U_Dirty = true;
            }
            else
            {
                this.solver_Dirty = true;
            }
        }

        public void Assert(IEnumerable<BoolExpr> exprs, bool undef, bool translate)
        {
            Contract.Requires(exprs != null);

            foreach (BoolExpr v in exprs)
            {
                this.Assert(v, undef, translate);
            }
        }

        public bool Frozen
        {
            get { return this._frozen; }

            set
            {
                if (value)
                {
                    if (!this._frozen)
                    {
                        this.Simplify();
                        this.Remove_History();
                        this._frozen = true;
                    }
                }
                else
                {
                    if (this._frozen)
                    {
                        Console.WriteLine("WARNING: State:Frozen: unfreezing a state");

                        this._frozen = false;
                        this._cached_Reg_Values.Clear();
                        this._cached_Flag_Values.Clear();
                    }
                }
            }
        }

        public void Update(StateUpdate stateUpdate)
        {
            if (stateUpdate == null)
            {
                return;
            }
            //if (stateUpdate.Empty) return;

            if (this._frozen)
            {
                Console.WriteLine("WARNING: State:Update: state is frozen, nothing added.");
                return;
            }
            lock (this._ctxLock)
            {
                this.UndefGrounding = false;
                stateUpdate.Update(this);
            }
            this.solver_Dirty = true;
            this.solver_U_Dirty = true;
        }

        public void Update_Forward(StateUpdate stateUpdate)
        {
            if (stateUpdate == null)
            {
                return;
            }
            //if (stateUpdate.Empty) return;
            this.Update(stateUpdate);
            this.HeadKey = stateUpdate.NextKey;
        }

        public void Update_Backward(StateUpdate stateUpdate, string prevKey)
        {
            if (stateUpdate == null)
            {
                return;
            }

            if (stateUpdate.Empty)
            {
                return;
            }

            this.Update(stateUpdate);
            this.TailKey = prevKey;
        }

        public void Add(BranchInfo branchInfo)
        {
            if (this._frozen)
            {
                Console.WriteLine("WARNING: State:Add: state is frozen, nothing added.");
                return;
            }
            lock (this._ctxLock)
            {
                this.BranchInfoStore.Add(branchInfo, true);
            }
        }
        #endregion

        #region Getters
        public bool Is_Undefined(Flags flagName)
        {
            return this.GetTv(flagName) == Tv.UNDEFINED;
        }

        public bool Is_Undefined(Rn regName)
        {
            Tv[] result = this.GetTvArray(regName);
            foreach (Tv tv in result)
            {
                if (tv == Tv.UNDEFINED)
                {
                    return true;
                }
            }
            return false;
        }

        public bool Is_Redundant(Flags flagName, string key1, string key2)
        {
            lock (this._ctxLock)
            {
                this.UndefGrounding = true; // needed!

                bool popNeeded = false;
                if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                {
                    this.Solver.Push();
                    this.Solver_U.Push();
                    this.AssertBranchInfoToSolver();
                    popNeeded = true;
                }

                using (Expr e1 = Tools.Create_Key(flagName, key1, this._ctx))
                using (Expr e2 = Tools.Create_Key(flagName, key2, this._ctx))
                using (BoolExpr e = this._ctx.MkEq(e1, e2))
                {
                    Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this._ctx);

                    if (popNeeded)
                    {
                        this.Solver.Pop();
                        this.Solver_U.Pop();
                    }
                    return result == Tv.ONE;
                }
            }
        }

        public bool Is_Redundant(Rn regName, string key1, string key2)
        {
            lock (this._ctxLock)
            {
                this.UndefGrounding = true; // needed!

                bool popNeeded = false;
                if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                {
                    this.Solver.Push();
                    this.Solver_U.Push();
                    this.AssertBranchInfoToSolver();
                    popNeeded = true;
                }

                using (Expr e1 = Tools.Create_Key(regName, key1, this._ctx))
                using (Expr e2 = Tools.Create_Key(regName, key2, this._ctx))
                using (BoolExpr e = this._ctx.MkEq(e1, e2))
                {
                    Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this._ctx);

                    if (popNeeded)
                    {
                        this.Solver.Pop();
                        this.Solver_U.Pop();
                    }
                    return result == Tv.ONE;
                }
            }
        }

        public Tv Is_Redundant_Mem(string key1, string key2)
        {
            lock (this._ctxLock)
            {
                this.UndefGrounding = true; // needed!

                bool popNeeded = false;
                if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                {
                    this.Solver.Push();
                    this.Solver_U.Push();
                    this.AssertBranchInfoToSolver();
                    popNeeded = true;
                }

                using (Expr e1 = Tools.Create_Mem_Key(key1, this._ctx))
                using (Expr e2 = Tools.Create_Mem_Key(key2, this._ctx))
                using (BoolExpr e = this._ctx.MkEq(e1, e2))
                {
                    Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this._ctx, true);
                    if (popNeeded)
                    {
                        this.Solver.Pop();
                        this.Solver_U.Pop();
                    }
                    return result;
                }
            }
        }

        public Tv? GetTv_Cached(Flags flagName)
        {
            // NOTO: do not use tryGetValue since then a uninitialized TV value (=TV.UNKNOWN) will be returned instead of null value
            if (this._cached_Flag_Values.ContainsKey(flagName))
            {
                return this._cached_Flag_Values[flagName];
            }
            return null;
        }

        public Tv GetTv(Flags flagName)
        {
            if (this.Frozen && this._cached_Flag_Values.ContainsKey(flagName))
            {
                return this._cached_Flag_Values[flagName];
            }
            lock (this._ctxLock)
            {
                this.UndefGrounding = true; // needed!

                bool popNeeded = false;
                if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                {
                    this.Solver.Push();
                    this.Solver_U.Push();
                    this.AssertBranchInfoToSolver();
                    popNeeded = true;
                }

                using (BoolExpr flagExpr = this.Create(flagName))
                {
                    Tv result = ToolsZ3.GetTv(flagExpr, flagExpr, this.Solver, this.Solver_U, this._ctx);

                    if (popNeeded)
                    {
                        this.Solver.Pop();
                        this.Solver_U.Pop();
                    }
                    if (this.Frozen)
                    {
                        this._cached_Flag_Values[flagName] = result;
                    }
                    return result;
                }
            }
        }

        public Tv[] GetTvArray_Cached(Rn regName)
        {
            this._cached_Reg_Values.TryGetValue(regName, out Tv[] value);
            return value;
        }

        public void Update_TvArray_Cached(Rn regName)
        {
            this.GetTvArray(regName);
        }

        public Tv[] GetTvArray(Rn regName)
        {
            lock (this._ctxLock)
            {
                if (this.Frozen && this._cached_Reg_Values.TryGetValue(regName, out Tv[] value))
                {
                    return value;
                }
                try
                {
                    this.UndefGrounding = true; // needed!

                    bool popNeeded = false;
                    if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                    {
                        this.Solver.Push();
                        this.Solver_U.Push();
                        this.AssertBranchInfoToSolver();
                        popNeeded = true;
                    }

                    using (BitVecExpr regExpr = this.Create(regName))
                    {
                        Tv[] result = ToolsZ3.GetTvArray(regExpr, RegisterTools.NBits(regName), this.Solver, this.Solver_U, this._ctx);

                        if (popNeeded)
                        {
                            this.Solver.Pop();
                            this.Solver_U.Pop();
                        }

                        if (this.Frozen)
                        {
                            if (ADD_COMPUTED_VALUES && (RegisterTools.NBits(regName) == 64))
                            {
                                ulong? value2 = ToolsZ3.ToUlong(result);
                                if (value2 != null)
                                {
                                    this.Solver.Assert(this.Ctx.MkEq(regExpr, this.Ctx.MkBV(value2.Value, 64)));
                                    this.solver_Dirty = true;
                                }
                            }
                            this._cached_Reg_Values[regName] = result;
                        }
                        return result;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("WARNING: AsmSimulator: " + e.ToString());
                    return new Tv[RegisterTools.NBits(regName)];
                }
            }
        }

        public Tv[] GetTvArrayMem(BitVecExpr address, int nBytes, bool addBranchInfo = true)
        {
            if (!addBranchInfo)
            {
                throw new Exception(); //TODO
            }

            this.UndefGrounding = true; // needed!

            bool popNeeded = false;
            if (addBranchInfo && (this.BranchInfoStore.Count > 0))
            {
                this.Solver.Push();
                this.Solver_U.Push();
                this.AssertBranchInfoToSolver();
                popNeeded = true;
            }

            using (BitVecExpr valueExpr = this.Create_Mem(address, nBytes))
            {
                Tv[] result = ToolsZ3.GetTvArray(valueExpr, nBytes << 3, this.Solver, this.Solver_U, this._ctx);

                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                return result;
            }
        }

        public BitVecExpr Create(Rn regName)
        {
            lock (this._ctxLock)
            {
                return Tools.Create_Key(regName, this.HeadKey, this._ctx);
            }
        }

        public BoolExpr Create(Flags flagName)
        {
            lock (this._ctxLock)
            {
                return Tools.Create_Key(flagName, this.HeadKey, this._ctx);
            }
        }

        public BitVecExpr Create_Tail(Rn regName)
        {
            lock (this._ctxLock)
            {
                return Tools.Create_Key(regName, this.TailKey, this._ctx);
            }
        }

        public BoolExpr Create_Tail(Flags flagName)
        {
            lock (this._ctxLock)
            {
                return Tools.Create_Key(flagName, this.TailKey, this._ctx);
            }
        }

        public BitVecExpr Create_Mem(BitVecExpr address, int nBytes)
        {
            lock (this._ctxLock)
            {
                return Tools.Create_Value_From_Mem(address, nBytes, this.HeadKey, this._ctx);
            }
        }

        #endregion

        #region UndefGrounding
        private bool _hasUndefGrounding = false;

        private bool UndefGrounding
        {
            get { return this._hasUndefGrounding; }

            set
            {
                if (value != this._hasUndefGrounding)
                {
                    this._hasUndefGrounding = value;
                    Context ctx = this._ctx;

                    if (value)
                    {
                        this._undefStore = this.Solver_U.Assertions;

                        string key = this.TailKey;
                        BoolExpr flagValue = ctx.MkTrue();
                        foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                        {
                            this.Solver_U.Assert(ctx.MkEq(Tools.Create_Key(flag, key, ctx), flagValue));
                        }
                        BitVecExpr regValue = ctx.MkBV(0, 64);
                        foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                        {
                            this.Solver_U.Assert(ctx.MkEq(Tools.Create_Key(reg, key, ctx), regValue));
                        }
                        if (this.Tools.StateConfig.Mem)
                        {
                            ArrayExpr memKey = Tools.Create_Mem_Key(key, ctx);
                            ArrayExpr initialMem = ctx.MkConstArray(ctx.MkBitVecSort(64), ctx.MkBV(0xFF, 8));
                            this.Solver_U.Assert(ctx.MkEq(memKey, initialMem));
                        }
                    }
                    else
                    {
                        this.Solver_U.Reset();
                        this.Solver_U.Assert(this._undefStore);
                    }
                }
            }
        }

        private BoolExpr[] _undefStore;

        #endregion

        #region ToString
        public override string ToString()
        {
            lock (this._ctxLock)
            {
                this.UndefGrounding = true; // needed!

                bool popNeeded = false;
                if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
                {
                    this.Solver.Push();
                    this.Solver_U.Push();
                    this.AssertBranchInfoToSolver();
                    popNeeded = true;
                }

                string result = this.ToString(string.Empty);

                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                return result;
            }
        }

        public string ToString(string identStr)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(this.ToStringConstraints(identStr));

            Tv consistent = this.IsConsistent;
            if (consistent != Tv.ONE)
            {
                sb.Append("State consistency: " + consistent);
            }
            else
            {
                sb.Append(this.ToStringFlags(identStr));
                sb.Append(this.ToStringRegs(identStr));
                //sb.Append(ToStringSIMD(identStr));
            }
            //sb.AppendLine(ToStringWarning(identStr));
            return sb.ToString();
        }

        public string ToStringFlags(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Flags flag in new Flags[] { Flags.CF, Flags.ZF, Flags.PF, Flags.OF, Flags.SF, Flags.AF, Flags.DF })
            {
                char c = ' ';
                if (this.Tools.StateConfig.IsFlagOn(flag))
                {
                    c = ToolsZ3.ToStringBin(this.GetTv(flag));
                }
                sb.Append(flag.ToString() + "=" + c + "; ");
            }
            sb.AppendLine(string.Empty);
            return sb.ToString();
        }

        public string ToStringRegs(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
            {
                Tv[] regContent = this.GetTvArray(reg);
                (bool hasOneValue, Tv value) = ToolsZ3.HasOneValue(regContent);
                bool showReg = !(hasOneValue && value == Tv.UNKNOWN);
                if (showReg)
                {
                    sb.Append("\n" + identStr + string.Format(reg + " = {0} = {1}", ToolsZ3.ToStringBin(regContent), ToolsZ3.ToStringHex(regContent)));
                }
            }
            return sb.ToString();
        }

        public string ToStringSIMD(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            //            foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
            {
                Rn reg = Rn.XMM1;
                Tv[] regContent = this.GetTvArray(reg);
                (bool hasOneValue, Tv value) = ToolsZ3.HasOneValue(regContent);
                bool showReg = !(hasOneValue && value == Tv.UNKNOWN);
                if (showReg)
                {
                    sb.Append("\n" + identStr + string.Format(reg + " = {0} = {1}", ToolsZ3.ToStringBin(regContent), ToolsZ3.ToStringHex(regContent)));
                }
            }
            return sb.ToString();
        }

        public string ToStringConstraints(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            if (this.Solver.NumAssertions > 0)
            {
                sb.AppendLine(identStr + "Current Value constraints:");
                for (int i = 0; i < (int)this.Solver.NumAssertions; ++i)
                {
                    BoolExpr e = this.Solver.Assertions[i];
                    sb.AppendLine(identStr + string.Format("   {0}: {1}", i, ToolsZ3.ToString(e)));
                }
            }
            if (this.Tools.ShowUndefConstraints)
            {
                //if (this.Solver_U.NumAssertions > 0)
                {
                    sb.AppendLine(identStr + "Current Undef constraints:");
                    for (int i = 0; i < (int)this.Solver_U.NumAssertions; ++i)
                    {
                        BoolExpr e = this.Solver_U.Assertions[i];
                        sb.AppendLine(identStr + string.Format("   {0}: {1}", i, ToolsZ3.ToString(e)));
                    }
                }
            }
            sb.AppendLine(this.BranchInfoStore.ToString());

            sb.Append("TailKey=" + this.TailKey + "; HeadKey=" + this.HeadKey);
            return sb.ToString();
        }
        #endregion

        #region Misc
        public void Remove_History()
        {
            ISet<string> keep = new HashSet<string>();
            foreach (Flags v in this._tools.StateConfig.GetFlagOn())
            {
                using (BoolExpr expr = this.Create(v))
                {
                    keep.Add(expr.ToString());
                }
            }
            foreach (Rn v in this._tools.StateConfig.GetRegOn())
            {
                using (BitVecExpr expr = this.Create(v))
                {
                    keep.Add(expr.ToString());
                }
            }
            if (this._tools.StateConfig.Mem)
            {
                using (ArrayExpr expr = Tools.Create_Mem_Key(this.HeadKey, this._ctx))
                {
                    keep.Add(expr.ToString());
                }
            }
            this.Compress(keep);
        }

        public void Compress(string keep)
        {
            this.Compress(new HashSet<string>() { keep });
        }

        public void Compress(ISet<string> keep)
        {
            ISet<string> used = new HashSet<string>(keep);
            BoolExpr[] s = this.Solver.Assertions;
            int nAssertions = s.Length;
            bool[] added = new bool[nAssertions];

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < nAssertions; ++i)
                {
                    if (!added[i])
                    {
                        foreach (string constant in ToolsZ3.Get_Constants(s[i]))
                        {
                            if (used.Contains(constant))
                            {
                                foreach (string constant2 in ToolsZ3.Get_Constants(s[i]))
                                {
                                    used.Add(constant2);
                                }
                                added[i] = true;
                                changed = true;
                                break;
                            }
                        }
                    }
                }
            }
            this.Solver.Reset();
            for (int i = 0; i < nAssertions; ++i)
            {
                if (added[i])
                {
                    this.Solver.Assert(s[i]);
                }
            }
        }

        public void UpdateConstName(string postfix)
        {
            this.HeadKey = this.HeadKey + postfix;
            this.TailKey = this.TailKey + postfix;
            lock (this._ctxLock)
            {
                {
                    BoolExpr[] content = this.Solver.Assertions;
                    this.Solver.Reset();
                    foreach (BoolExpr e in content)
                    {
                        this.Solver.Assert(ToolsZ3.UpdateConstName(e, postfix, this._ctx) as BoolExpr);
                    }
                }
                {
                    BoolExpr[] content = this.Solver_U.Assertions;
                    this.Solver_U.Reset();
                    foreach (BoolExpr e in content)
                    {
                        this.Solver_U.Assert(ToolsZ3.UpdateConstName(e, postfix, this._ctx) as BoolExpr);
                    }
                }
            }
        }

        public Tv IsConsistent
        {
            get
            {
                lock (this._ctxLock)
                {
                    this.Solver.Push();
                    this.AssertBranchInfoToSolver(false);
                    Status result = this.Solver.Check();
                    this.Solver.Pop();

                    if (result == Status.SATISFIABLE)
                    {
                        return Tv.ONE;
                    }
                    else if (result == Status.UNSATISFIABLE)
                    {
                        return Tv.ZERO;
                    }
                    else
                    {
                        return Tv.UNDETERMINED;
                    }
                }
            }
        }

        public Tv EqualValues(Rn reg1, Rn reg2)
        {
            return this.EqualValues(this.Create(reg1), this.Create(reg2));
        }

        public Tv EqualValues(Expr value1, Expr value2)
        {
            //Console.WriteLine("INFO: MemZ3:isEqual: testing whether a=" + a + " is equal to b=" + b);
            const bool method1 = true; // the other method seems not to work
            lock (this._ctxLock)
            {
                Tv eq = Tv.UNKNOWN;
                Tv uneq = Tv.UNKNOWN;

                this.Solver.Push();
                this.AssertBranchInfoToSolver(false);
                {
                    Status status;
                    BoolExpr tmp1 = this._ctx.MkEq(value1, value2);
                    if (method1)
                    {
                        this.Solver.Push();

                        this.Solver.Assert(tmp1);
                        status = this.Solver.Check();
                    }
                    else
                    {
                        status = this.Solver.Check(tmp1);
                    }
                    switch (status)
                    {
                        case Status.SATISFIABLE:
                            eq = Tv.ONE;
                            break;
                        case Status.UNSATISFIABLE:
                            eq = Tv.ZERO;
                            break;
                        case Status.UNKNOWN:
                            Console.WriteLine("WARNING: State:equalValue: A: ReasonUnknown = " + this.Solver.ReasonUnknown);
                            break;
                    }
                    if (method1)
                    {
                        this.Solver.Pop();
                    }
                }
                {
                    Status status;
                    //BoolExpr tmp1 = _ctx.MkDistinct(value1, value2);
                    BoolExpr tmp1 = this._ctx.MkNot(this._ctx.MkEq(value1, value2));
                    if (method1)
                    {
                        this.Solver.Assert(tmp1);
                        status = this.Solver.Check();
                    }
                    else
                    {
                        status = this.Solver.Check(tmp1);
                    }
                    switch (status)
                    {
                        case Status.SATISFIABLE:
                            uneq = Tv.ONE;
                            break;
                        case Status.UNSATISFIABLE:
                            uneq = Tv.ZERO;
                            break;
                        case Status.UNKNOWN:
                            Console.WriteLine("WARNING: State:equalValue: B: ReasonUnknown = " + this.Solver.ReasonUnknown);
                            break;
                    }
                }
                this.Solver.Pop(); // get rid of context that had been added

                if ((eq == Tv.ONE) && (uneq == Tv.ONE))
                {
                    return Tv.UNKNOWN;
                }
                if ((eq == Tv.ONE) && (uneq == Tv.ZERO))
                {
                    return Tv.ONE;
                }
                if ((eq == Tv.ZERO) && (uneq == Tv.ONE))
                {
                    return Tv.ZERO;
                }
                if ((eq == Tv.ONE) && (uneq == Tv.ONE))
                {
                    return Tv.INCONSISTENT;
                }
                return Tv.UNKNOWN;
            }
        }

        public void Simplify()
        {
            lock (this._ctxLock)
            {
                if (this.solver_Dirty)
                {
                    ToolsZ3.Consolidate(false, this.Solver, this.Solver_U, this._ctx);
                    this.solver_Dirty = false;
                }
                if (this.solver_U_Dirty)
                {
                    ToolsZ3.Consolidate(true, this.Solver, this.Solver_U, this._ctx);
                    this.solver_U_Dirty = false;
                }
            }
        }

        public string Warning
        {
            get { return this._warningMessage; }

            set
            {
                if (this._warningMessage == null)
                {
                    this._warningMessage = value;
                }
                else
                {
                    this._warningMessage += Environment.NewLine + value;
                }
            }
        }

        public string SyntaxError
        {
            get { return this._synstaxErrorMessage; }

            set
            {
                if (value != null)
                {
                    if (this._synstaxErrorMessage == null)
                    {
                        this._synstaxErrorMessage = value;
                    }
                    else
                    {
                        this._synstaxErrorMessage += Environment.NewLine + value;
                    }
                    this.IsHalted = true;
                }
            }
        }
        #endregion Misc

        #region Private stuff

        private void AssertBranchInfoToSolver(bool addUndef = true)
        {
            foreach (BranchInfo e in this.BranchInfoStore.Values)
            {
                BoolExpr expr = e.GetData(this._ctx);
                this.Solver.Assert(expr);
                if (addUndef)
                {
                    this.Solver_U.Assert(expr);
                }
            }
        }
        #endregion

        #region IDisposable Support
        public void Dispose()
        {
            lock (this._ctxLock)
            {
                this.Solver.Dispose();
                this.Solver_U.Dispose();
                this._ctx.Dispose();
            }
        }
        #endregion
    }
}
