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

namespace AsmSim
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Text;
    using AsmTools;
    using Microsoft.Z3;

    public class State : IDisposable
    {
        private static readonly CultureInfo Culture = CultureInfo.CurrentUICulture;

        #region Fields
        public static readonly bool ADD_COMPUTED_VALUES = true;

        private readonly Tools tools_;

        public Tools Tools { get { return this.tools_; } }

        private readonly Context ctx_;

        public Context Ctx { get { return this.ctx_; } }

        public Solver Solver { get; private set; }

        public Solver Solver_U { get; private set; }

        private bool solver_Dirty = false;
        private bool solver_U_Dirty = false;

        private string warningMessage_;
        private string synstaxErrorMessage_;

        public bool IsHalted { get; private set; }

        public string HeadKey = null;
        public string TailKey = null;

        private bool frozen_;
        private readonly IDictionary<Rn, Tv[]> cached_Reg_Values_;
        private readonly IDictionary<Flags, Tv> cached_Flag_Values_;

        private readonly object ctxLock_ = new();

        private BranchInfoStore branchInfoStore_;

        public BranchInfoStore BranchInfoStore { get { return this.branchInfoStore_; } }
        #endregion

        #region Constructors

        /// <summary>Private constructor for internal use</summary>
        private State(Tools tools)
        {
            this.tools_ = new Tools(tools);
            this.ctx_ = new Context(this.tools_.ContextSettings); // housekeeping in Dispose();
            this.Solver = MakeSolver(this.ctx_, this.tools_.SolverSetting);
            this.Solver_U = MakeSolver(this.ctx_, this.tools_.SolverSetting);
            this.branchInfoStore_ = new BranchInfoStore(this.ctx_);
            this.cached_Reg_Values_ = new Dictionary<Rn, Tv[]>();
            this.cached_Flag_Values_ = new Dictionary<Flags, Tv>();
        }

        public static Solver MakeSolver(Context ctx, string solverSetting = "qfbv")
        {
            Contract.Requires(ctx != null);

            Solver s = (string.IsNullOrEmpty(solverSetting))
                ? ctx.MkSolver()
                : ctx.MkSolver(ctx.MkTactic(solverSetting));

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
            lock (this.ctxLock_)
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

            lock (this.ctxLock_)
            {
                other.TailKey = this.TailKey;
                other.HeadKey = this.HeadKey;
                this.UndefGrounding = false;

                Context ctx = other.ctx_;
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
                DiffConstructor(state1, state2);
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
                    lock (this.ctxLock_)
                    {
                        state2.Copy(this);
                    }

                    return;
                }
                if (!consistent2)
                {
                    lock (this.ctxLock_)
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

            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;

                this.branchInfoStore_ = BranchInfoStore.RetrieveSharedBranchInfo(state1.BranchInfoStore, state2.BranchInfoStore, ctx);

                // merge the contents of both solvers
                {
                    ISet<BoolExpr> mergedContent = new HashSet<BoolExpr>();
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

                        using StateUpdate stateUpdateForward = new("!ERROR_1", this.HeadKey, this.Tools);
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

        private static void DiffConstructor(State state1, State state2)
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
                BoolExpr? t = expr.Translate(this.ctx_) as BoolExpr;
                if (t == null)
                {
                    return;
                }
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
            get { return this.frozen_; }

            set
            {
                if (value)
                {
                    if (!this.frozen_)
                    {
                        this.Simplify();
                        this.Remove_History();
                        this.frozen_ = true;
                    }
                }
                else
                {
                    if (this.frozen_)
                    {
                        Console.WriteLine("WARNING: State:Frozen: unfreezing a state");

                        this.frozen_ = false;
                        this.cached_Reg_Values_.Clear();
                        this.cached_Flag_Values_.Clear();
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

            if (this.frozen_)
            {
                Console.WriteLine("WARNING: State:Update: state is frozen, nothing added.");
                return;
            }
            lock (this.ctxLock_)
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
            if (this.frozen_)
            {
                Console.WriteLine("WARNING: State:Add: state is frozen, nothing added.");
                return;
            }
            lock (this.ctxLock_)
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
            lock (this.ctxLock_)
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

                using Expr e1 = Tools.Create_Key(flagName, key1, this.ctx_);
                using Expr e2 = Tools.Create_Key(flagName, key2, this.ctx_);
                using BoolExpr e = this.ctx_.MkEq(e1, e2);
                Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this.ctx_);

                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                return result == Tv.ONE;
            }
        }

        public bool Is_Redundant(Rn regName, string key1, string key2)
        {
            lock (this.ctxLock_)
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

                using Expr e1 = Tools.Create_Key(regName, key1, this.ctx_);
                using Expr e2 = Tools.Create_Key(regName, key2, this.ctx_);
                using BoolExpr e = this.ctx_.MkEq(e1, e2);
                Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this.ctx_);

                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                return result == Tv.ONE;
            }
        }

        public Tv Is_Redundant_Mem(string key1, string key2)
        {
            lock (this.ctxLock_)
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

                using Expr e1 = Tools.Create_Mem_Key(key1, this.ctx_);
                using Expr e2 = Tools.Create_Mem_Key(key2, this.ctx_);
                using BoolExpr e = this.ctx_.MkEq(e1, e2);
                Tv result = ToolsZ3.GetTv(e, e, this.Solver, this.Solver_U, this.ctx_, true);
                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                return result;
            }
        }

        public Tv? GetTv_Cached(Flags flagName)
        {
            // NOTO: do not use tryGetValue since then a uninitialized TV value (=TV.UNKNOWN) will be returned instead of null value
            if (this.cached_Flag_Values_.ContainsKey(flagName))
            {
                return this.cached_Flag_Values_[flagName];
            }
            return null;
        }

        public Tv GetTv(Flags flagName)
        {
            if (this.Frozen && this.cached_Flag_Values_.ContainsKey(flagName))
            {
                return this.cached_Flag_Values_[flagName];
            }
            lock (this.ctxLock_)
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

                using BoolExpr flagExpr = this.Create(flagName);
                Tv result = ToolsZ3.GetTv(flagExpr, flagExpr, this.Solver, this.Solver_U, this.ctx_);

                if (popNeeded)
                {
                    this.Solver.Pop();
                    this.Solver_U.Pop();
                }
                if (this.Frozen)
                {
                    this.cached_Flag_Values_[flagName] = result;
                }
                return result;
            }
        }

        public Tv[] GetTvArray_Cached(Rn regName)
        {
            this.cached_Reg_Values_.TryGetValue(regName, out Tv[] value);
            return value;
        }

        public void Update_TvArray_Cached(Rn regName)
        {
            this.GetTvArray(regName);
        }

        public Tv[] GetTvArray(Rn regName)
        {
            lock (this.ctxLock_)
            {
                if (this.Frozen && this.cached_Reg_Values_.TryGetValue(regName, out Tv[] value))
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

                    using BitVecExpr regExpr = this.Create(regName);
                    Tv[] result = ToolsZ3.GetTvArray(regExpr, RegisterTools.NBits(regName), this.Solver, this.Solver_U, this.ctx_);

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
                        this.cached_Reg_Values_[regName] = result;
                    }
                    return result;
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

            using BitVecExpr valueExpr = this.Create_Mem(address, nBytes);
            Tv[] result = ToolsZ3.GetTvArray(valueExpr, nBytes << 3, this.Solver, this.Solver_U, this.ctx_);

            if (popNeeded)
            {
                this.Solver.Pop();
                this.Solver_U.Pop();
            }
            return result;
        }

        public BitVecExpr Create(Rn regName)
        {
            lock (this.ctxLock_)
            {
                return Tools.Create_Key(regName, this.HeadKey, this.ctx_);
            }
        }

        public BoolExpr Create(Flags flagName)
        {
            lock (this.ctxLock_)
            {
                return Tools.Create_Key(flagName, this.HeadKey, this.ctx_);
            }
        }

        public BitVecExpr Create_Tail(Rn regName)
        {
            lock (this.ctxLock_)
            {
                return Tools.Create_Key(regName, this.TailKey, this.ctx_);
            }
        }

        public BoolExpr Create_Tail(Flags flagName)
        {
            lock (this.ctxLock_)
            {
                return Tools.Create_Key(flagName, this.TailKey, this.ctx_);
            }
        }

        public BitVecExpr Create_Mem(BitVecExpr address, int nBytes)
        {
            lock (this.ctxLock_)
            {
                return Tools.Create_Value_From_Mem(address, nBytes, this.HeadKey, this.ctx_);
            }
        }

        #endregion

        #region UndefGrounding
        private bool hasUndefGrounding_ = false;

        private bool UndefGrounding
        {
            get { return this.hasUndefGrounding_; }

            set
            {
                if (value != this.hasUndefGrounding_)
                {
                    this.hasUndefGrounding_ = value;
                    Context ctx = this.ctx_;

                    if (value)
                    {
                        this.undefStore_ = this.Solver_U.Assertions;

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
                        this.Solver_U.Assert(this.undefStore_);
                    }
                }
            }
        }

        private BoolExpr[] undefStore_;

        #endregion

        #region ToString
        public override string ToString()
        {
            lock (this.ctxLock_)
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
            StringBuilder sb = new();

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
            StringBuilder sb = new();
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
            StringBuilder sb = new();
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
            StringBuilder sb = new();
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
            StringBuilder sb = new();
            if (this.Solver.NumAssertions > 0)
            {
                sb.AppendLine(identStr + "Current Value constraints:");
                sb.AppendLine(ToolsZ3.ToString(this.Solver, identStr));
            }
            if (this.Tools.ShowUndefConstraints)
            {
                //if (this.Solver_U.NumAssertions > 0)
                {
                    sb.AppendLine(identStr + "Current Undef constraints:");
                    sb.AppendLine(ToolsZ3.ToString(this.Solver_U, identStr));
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
            HashSet<string> keep = new();
            foreach (Flags v in this.tools_.StateConfig.GetFlagOn())
            {
                using BoolExpr expr = this.Create(v);
                keep.Add(expr.ToString());
            }
            foreach (Rn v in this.tools_.StateConfig.GetRegOn())
            {
                using BitVecExpr expr = this.Create(v);
                keep.Add(expr.ToString());
            }
            if (this.tools_.StateConfig.Mem)
            {
                using ArrayExpr expr = Tools.Create_Mem_Key(this.HeadKey, this.ctx_);
                keep.Add(expr.ToString());
            }
            this.Compress(keep);
        }

        public void Compress(string keep)
        {
            this.Compress(new HashSet<string>() { keep });
        }

        public void Compress(HashSet<string> keep)
        {
            HashSet<string> used = new(keep);
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
            this.HeadKey += postfix;
            this.TailKey += postfix;
            lock (this.ctxLock_)
            {
                {
                    BoolExpr[] content = this.Solver.Assertions;
                    this.Solver.Reset();
                    foreach (BoolExpr e in content)
                    {
                        this.Solver.Assert(ToolsZ3.UpdateConstName(e, postfix, this.ctx_) as BoolExpr);
                    }
                }
                {
                    BoolExpr[] content = this.Solver_U.Assertions;
                    this.Solver_U.Reset();
                    foreach (BoolExpr e in content)
                    {
                        this.Solver_U.Assert(ToolsZ3.UpdateConstName(e, postfix, this.ctx_) as BoolExpr);
                    }
                }
            }
        }

        public Tv IsConsistent
        {
            get
            {
                lock (this.ctxLock_)
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
            using BitVecExpr expr1 = this.Create(reg1);
            using BitVecExpr expr2 = this.Create(reg2);
            return this.EqualValues(expr1, expr2);
        }

        public Tv EqualValues(Expr value1, Expr value2)
        {
            //Console.WriteLine("INFO: MemZ3:isEqual: testing whether a=" + a + " is equal to b=" + b);
            const bool method1 = true; // the other method seems not to work
            lock (this.ctxLock_)
            {
                Tv eq = Tv.UNKNOWN;
                Tv uneq = Tv.UNKNOWN;

                this.Solver.Push();
                this.AssertBranchInfoToSolver(false);
                {
                    Status status;
                    BoolExpr tmp1 = this.ctx_.MkEq(value1, value2);
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
                    //BoolExpr tmp1 = this.ctx_.MkDistinct(value1, value2);
                    BoolExpr tmp1 = this.ctx_.MkNot(this.ctx_.MkEq(value1, value2));
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
            lock (this.ctxLock_)
            {
                if (this.solver_Dirty)
                {
                    ToolsZ3.Consolidate(false, this.Solver, this.Solver_U, this.ctx_);
                    this.solver_Dirty = false;
                }
                if (this.solver_U_Dirty)
                {
                    ToolsZ3.Consolidate(true, this.Solver, this.Solver_U, this.ctx_);
                    this.solver_U_Dirty = false;
                }
            }
        }

        public string Warning
        {
            get { return this.warningMessage_; }

            set
            {
                if (this.warningMessage_ == null)
                {
                    this.warningMessage_ = value;
                }
                else
                {
                    this.warningMessage_ += Environment.NewLine + value;
                }
            }
        }

        public string SyntaxError
        {
            get { return this.synstaxErrorMessage_; }

            set
            {
                if (value != null)
                {
                    if (this.synstaxErrorMessage_ == null)
                    {
                        this.synstaxErrorMessage_ = value;
                    }
                    else
                    {
                        this.synstaxErrorMessage_ += Environment.NewLine + value;
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
                BoolExpr expr = e.GetData(this.ctx_);
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
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~State()
        {
            this.Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                lock (this.ctxLock_)
                {
                    this.Solver.Dispose();
                    this.Solver_U.Dispose();
                    this.ctx_.Dispose();
                }
            }
            // free native resources if there are any.
        }
        #endregion
    }
}
