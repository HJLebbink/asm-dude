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

using AsmTools;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsmSim
{
    public class State
    {
        #region Fields
        public static readonly bool SIMPLIFY_ON = true;
        public static readonly bool ADD_COMPUTED_VALUES = true;

        public readonly Tools _tools;
        public Tools Tools { get { return this._tools; } }

        public Solver Solver { get; private set; }
        public Solver Solver_U { get; private set; }

        private bool Solver_Dirty = false;
        private bool Solver_U_Dirty = false;

        private string _warningMessage;
        private string _synstaxErrorMessage;
        public bool IsHalted { get; private set; }

        public string HeadKey = null;
        public string TailKey = null;

        private bool _frozen;
        private readonly IDictionary<Rn, Tv[]> _cached_Reg_Values;
        private readonly IDictionary<Flags, Tv> _cached_Flag_Values;

        public Context Ctx { get { return this.Tools.Ctx; } }

        private BranchInfoStore _branchInfoStore;
        public BranchInfoStore BranchInfoStore { get { return this._branchInfoStore; } }
        #endregion

        #region Constructors
        /// <summary>Regular constructor</summary>
        public State(Tools tools, string tailKey, string headKey)
        {
            this._tools = tools;
            this.TailKey = tailKey;
            this.HeadKey = headKey;

            this.Solver = CreateSolver(tools.Ctx);
            this.Solver_U = CreateSolver(tools.Ctx);
            this._branchInfoStore = new BranchInfoStore(this.Tools);
            this._cached_Reg_Values = new Dictionary<Rn, Tv[]>();
            this._cached_Flag_Values = new Dictionary<Flags, Tv>();
        }

        /// <summary>Copy constructor</summary>
        public State(State other)
        {
            this._tools = other.Tools;
            this._cached_Reg_Values = new Dictionary<Rn, Tv[]>();
            this._cached_Flag_Values = new Dictionary<Flags, Tv>();

            this.CopyConstructor(other);
        }
        /// <summary>Merge and Diff constructor</summary>
        public State(State state1, State state2, bool merge)
        {
            this._tools = state1.Tools;
            this.Solver = CreateSolver(this._tools.Ctx);
            this.Solver_U = CreateSolver(this._tools.Ctx);

            this._cached_Reg_Values = new Dictionary<Rn, Tv[]>();
            this._cached_Flag_Values = new Dictionary<Flags, Tv>();

            if (merge)
            {
                this.MergeConstructor(state1, state2);
            }
            else
            {
                this.DiffConstructor(state1, state2);
            }
        }
        private Solver CreateSolver(Context ctx)
        {
            if (true)
            {
                Tactic tactic = ctx.MkTactic("qfbv");
                return ctx.MkSolver(tactic);
            }
            else
            {
                return ctx.MkSolver("QF_ABV");
            }
        }
        /// <summary>Copy Constructor Method</summary>
        private void CopyConstructor(State other)
        {
            this.HeadKey = other.HeadKey;
            this.TailKey = other.TailKey;

            other.UndefGrounding = false;
            this.Solver = CreateSolver(this._tools.Ctx);
            this.Solver_U = CreateSolver(this._tools.Ctx);

            this.Solver.Assert(other.Solver.Assertions);
            this.Solver_U.Assert(other.Solver_U.Assertions);

            this._branchInfoStore = new BranchInfoStore(other.BranchInfoStore);
        }
        /// <summary>Merge Constructor Method</summary>
        private void MergeConstructor(State state1, State state2)
        {
            state1.UndefGrounding = false;
            state2.UndefGrounding = false;

            state1.Simplify();
            state2.Simplify();

            var shared = BranchInfoStore.RetrieveSharedBranchInfo(state1.BranchInfoStore, state2.BranchInfoStore, this.Tools);
            this._branchInfoStore = shared.MergedBranchInfo;

            BoolExpr sharedCondition1 = shared.BranchPoint1?.BranchCondition;
            BoolExpr sharedCondition2 = shared.BranchPoint2?.BranchCondition;

            BoolExpr freshBranchExpr = this.Ctx.MkBoolConst("Branch" + Tools.CreateKey(state1.Tools.Rand));
            if (sharedCondition1 == null) sharedCondition1 = freshBranchExpr;
            string sharedConditionStr1 = sharedCondition1.FuncDecl.Name.ToString();
            if (sharedCondition2 == null) sharedCondition2 = freshBranchExpr;
            string sharedConditionStr2 = sharedCondition2.FuncDecl.Name.ToString();

            if (sharedCondition1 != sharedCondition2)
            {
                Console.WriteLine("INFO: merge: sharedCondition1=" + sharedCondition1);
                Console.WriteLine("INFO: merge: sharedCondition2=" + sharedCondition2);
                this.Solver.Assert(this.Tools.Ctx.MkEq(sharedCondition1, sharedCondition2));
            }

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
                    this.CopyConstructor(state2);
                    this.BranchInfoStore.RemoveKey(sharedConditionStr1);
                    return;
                }
                if (!consistent2)
                {
                    this.CopyConstructor(state1);
                    this.BranchInfoStore.RemoveKey(sharedConditionStr2);
                    return;
                }
            }
            #endregion

            // merge the contents of both solvers
            {
                ISet<BoolExpr> mergedContent = new HashSet<BoolExpr>(state1.Solver.Assertions);
                foreach (BoolExpr b in state2.Solver.Assertions) mergedContent.Add(b);
                foreach (BoolExpr b in mergedContent) this.Solver.Assert(b);

                ISet<BoolExpr> mergedContent_U = new HashSet<BoolExpr>(state1.Solver_U.Assertions);
                foreach (BoolExpr b in state2.Solver_U.Assertions) mergedContent_U.Add(b);
                foreach (BoolExpr b in mergedContent_U) this.Solver_U.Assert(b);
            }
            // retrieve whether branch 1 is the branch condition
            bool state1BranchCondition;
            {
                bool found = false;
                foreach (BranchInfo v in state1.BranchInfoStore.Values)
                {
                    if (v.Key == sharedConditionStr1)
                    {
                        state1BranchCondition = v.BranchTaken;
                        found = true;
                    }
                }
                if (!found)
                {
                    Console.WriteLine("WARNING: State:MergeConstructor: could not find branchConditionStr=" + sharedConditionStr1);
                    state1BranchCondition = true;
                }
            }
            // merge the head and tail
            {
                Context ctx = this.Tools.Ctx;
                if (state1.HeadKey == state2.HeadKey)
                {
                    this.HeadKey = state1.HeadKey;
                }
                else
                {
                    this.HeadKey = Tools.CreateKey(this.Tools.Rand);
                    string head1 = state1.HeadKey;
                    string head2 = state2.HeadKey;

                    StateUpdate stateUpdateForward = new StateUpdate("!ERROR_1", this.HeadKey, this.Tools);

                    foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                    {
                        stateUpdateForward.Set(reg, ctx.MkITE(sharedCondition1, Tools.Reg_Key(reg, head1, ctx), Tools.Reg_Key(reg, head2, ctx)) as BitVecExpr);
                    }
                    foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                    {
                        stateUpdateForward.Set(flag, ctx.MkITE(sharedCondition1, Tools.Flag_Key(flag, head1, ctx), Tools.Flag_Key(flag, head2, ctx)) as BoolExpr);
                    }
                    stateUpdateForward.SetMem(ctx.MkITE(sharedCondition1, Tools.Mem_Key(head1, ctx), Tools.Mem_Key(head2, ctx)) as ArrayExpr);

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

        private void DiffConstructor(State state1, State state2)
        {
            //TODO
        }
        #endregion

        #region Setters

        public bool Frozen
        {
            get { return this._frozen; }
            set
            {
                if (value)
                {
                    if (!this._frozen)
                    {
                        this._frozen = true;
                    }
                }
                else
                {
                    if (this._frozen)
                    {
                        this._frozen = false;
                        this._cached_Reg_Values.Clear();
                        this._cached_Flag_Values.Clear();
                    }
                }
            }
        }

        public void Update(StateUpdate stateUpdate)
        {
            if (stateUpdate == null) return;
            //if (stateUpdate.Empty) return;

            this.UndefGrounding = false;
            foreach (BoolExpr expr in stateUpdate.Value) this.Solver.Assert(expr);
            foreach (BoolExpr expr in stateUpdate.Undef) this.Solver_U.Assert(expr);
            this.BranchInfoStore.Add(stateUpdate.BranchInfo);

            this.Solver_Dirty = true;
            this.Solver_U_Dirty = true;
        }
        public void Update_Forward(StateUpdate stateUpdate)
        {
            if (stateUpdate == null) return;
            //if (stateUpdate.Empty) return;
            this.Update(stateUpdate);
            this.HeadKey = stateUpdate.NextKey;
        }
        public void Update_Backward(StateUpdate stateUpdate, string prevKey)
        {
            if (stateUpdate == null) return;
            if (stateUpdate.Empty) return;

            this.Update(stateUpdate);
            this.TailKey = prevKey;
        }

        public void Add(BranchInfo branchInfo)
        {
            this.BranchInfoStore.Add(branchInfo);
        }
        #endregion

        #region Getters 
        public bool IsUndefined(Flags flagName, bool addBranchInfo = true)
        {
            return (this.GetTv(flagName, addBranchInfo) == Tv.UNDEFINED);
        }
        public bool IsUndefined(Rn regName, bool addBranchInfo = true)
        {
            Tv[] result = this.GetTvArray(regName, addBranchInfo);
            foreach (Tv tv in result)
            {
                if (tv == Tv.UNDEFINED) return true;
            }
            return false;
        }

        public Tv GetTv_Cached(Flags flagName)
        {
            this._cached_Flag_Values.TryGetValue(flagName, out var value);
            return value;
        }

        public Tv GetTv(Flags flagName, bool addBranchInfo = true)
        {
            if (!addBranchInfo) throw new Exception(); //TODO

            if (this.Frozen && this._cached_Flag_Values.ContainsKey(flagName))
            {
                return this._cached_Flag_Values[flagName];
            }

            this.UndefGrounding = true; // needed!

            bool popNeeded = false;
            if ((this.BranchInfoStore != null) && addBranchInfo && (this.BranchInfoStore.Count > 0))
            {
                this.Solver.Push();
                this.Solver_U.Push();
                this.AssertBranchInfoToSolver();
                popNeeded = true;
            }

            BoolExpr flagExpr = this.Get(flagName);
            Tv result = ToolsZ3.GetTv(flagExpr, flagExpr, this.Solver, this.Solver_U, this.Ctx);

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

        public Tv[] GetTvArray_Cached(Rn regName)
        {
            this._cached_Reg_Values.TryGetValue(regName, out var value);
            return value;
        }

        public Tv[] GetTvArray(Rn regName, bool addBranchInfo = true)
        {
            if (!addBranchInfo) throw new Exception(); //TODO

            if (this.Frozen && this._cached_Reg_Values.ContainsKey(regName))
            {
                return this._cached_Reg_Values[regName];
            }

            Rn reg64 = RegisterTools.Get64BitsRegister(regName);
            if (reg64 == Rn.NOREG) return new Tv[RegisterTools.NBits(regName)];

            this.UndefGrounding = true; // needed!

            bool popNeeded = false;
            if ((this.BranchInfoStore != null) && addBranchInfo && (this.BranchInfoStore.Count > 0))
            {
                this.Solver.Push();
                this.Solver_U.Push();
                this.AssertBranchInfoToSolver();
                popNeeded = true;
            }

            BitVecExpr regExpr = this.Get(reg64);
            if (RegisterTools.Is8BitHigh(regName))
            {
                regExpr = this.Ctx.MkExtract(15, 8, regExpr);
            }

            Tv[] result = ToolsZ3.GetTvArray(regExpr, RegisterTools.NBits(regName), this.Solver, this.Solver_U, this.Ctx);

            if (popNeeded)
            {
                this.Solver.Pop();
                this.Solver_U.Pop();
            }

            if (this.Frozen)
            {
                if (ADD_COMPUTED_VALUES && RegisterTools.NBits(regName) == 64)
                {
                    ulong? value = ToolsZ3.GetUlong(result);
                    if (value != null)
                    {
                        this.Solver.Assert(this.Ctx.MkEq(regExpr, this.Ctx.MkBV(value.Value, 64)));
                        this.Solver_Dirty = true;
                    }
                }

                this._cached_Reg_Values[regName] = result;
            }

            return result;
        }

        public Tv[] GetTvArrayMem(BitVecExpr address, int nBytes, bool addBranchInfo = true)
        {
            if (!addBranchInfo) throw new Exception(); //TODO

            this.UndefGrounding = true; // needed!

            bool popNeeded = false;
            if (addBranchInfo && (this.BranchInfoStore.Count > 0))
            {
                this.Solver.Push();
                this.Solver_U.Push();
                this.AssertBranchInfoToSolver();
                popNeeded = true;
            }

            BitVecExpr valueExpr = this.GetMem(address, nBytes);
            Tv[] result = ToolsZ3.GetTvArray(valueExpr, nBytes << 3, this.Solver, this.Solver_U, this.Ctx);

            if (popNeeded)
            {
                this.Solver.Pop();
                this.Solver_U.Pop();
            }
            return result;
        }

        public BitVecExpr Get(Rn regName)
        {
            return Tools.Reg_Key(regName, this.HeadKey, this.Ctx);
        }
        public BoolExpr Get(Flags flagName)
        {
            return Tools.Flag_Key(flagName, this.HeadKey, this.Ctx);
        }

        public BitVecExpr GetTail(Rn regName)
        {
            return Tools.Reg_Key(regName, this.TailKey, this.Ctx);
        }
        public BoolExpr GetTail(Flags flagName)
        {
            return Tools.Flag_Key(flagName, this.TailKey, this.Ctx);
        }

        public BitVecExpr GetMem(BitVecExpr address, int nBytes)
        {
            return Tools.Get_Value_From_Mem(address, nBytes, this.HeadKey, this.Ctx);
        }

        #endregion

        #region UndefGrounding
        private bool _hasUndefGrounding = false;
        public bool UndefGrounding
        {
            get { return this._hasUndefGrounding; }
            set
            {
                if (value != this._hasUndefGrounding)
                {
                    this._hasUndefGrounding = value;
                    Context ctx = this.Ctx;

                    if (value)
                    {
                        this._undefStore = this.Solver_U.Assertions;

                        string key = this.TailKey;
                        BoolExpr flagValue = ctx.MkTrue();
                        foreach (Flags flag in this.Tools.StateConfig.GetFlagOn())
                        {
                            this.Solver_U.Assert(ctx.MkEq(Tools.Flag_Key(flag, key, ctx), flagValue));
                        }
                        BitVecExpr regValue = ctx.MkBV(0, 64);
                        foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
                        {
                            this.Solver_U.Assert(ctx.MkEq(Tools.Reg_Key(reg, key, ctx), regValue));
                        }

                        ArrayExpr memKey = Tools.Mem_Key(key, ctx);
                        ArrayExpr initialMem = ctx.MkConstArray(ctx.MkBitVecSort(64), ctx.MkBV(0xFF, 8));
                        this.Solver_U.Assert(ctx.MkEq(memKey, initialMem));
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
            this.UndefGrounding = true; // needed!

            bool popNeeded = false;
            if ((this.BranchInfoStore != null) && (this.BranchInfoStore.Count > 0))
            {
                this.Solver.Push();
                this.Solver_U.Push();
                this.AssertBranchInfoToSolver();
                popNeeded = true;
            }

            string result = this.ToString("");

            if (popNeeded)
            {
                this.Solver.Pop();
                this.Solver_U.Pop();
            }
            return result;
        }
        public string ToString(string identStr)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(ToStringConstraints(identStr));

            Tv consistent = this.IsConsistent;
            if (consistent != Tv.ONE)
            {
                sb.Append("State consistency: " + consistent);
            }
            else
            {
                sb.Append(ToStringFlags(identStr));
                sb.Append(ToStringRegs(identStr));
            }
            //sb.AppendLine(ToStringWarning(identStr));
            return sb.ToString();
        }
        public string ToStringFlags(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Flags flag in new Flags[] { Flags.CF, Flags.ZF, Flags.PF, Flags.OF, Flags.SF, Flags.AF })
            {
                char c = ' ';
                if (this.Tools.StateConfig.IsFlagOn(flag))
                {
                    c = ToolsZ3.ToStringBin(this.GetTv(flag, true));
                }
                sb.Append(flag.ToString() + "=" + c + "; ");
            }
            sb.AppendLine("");
            return sb.ToString();
        }
        public string ToStringRegs(string identStr)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Rn reg in this.Tools.StateConfig.GetRegOn())
            {
                Tv[] regContent = this.GetTvArray(reg, true);
                var t = ToolsZ3.HasOneValue(regContent);
                bool showReg = (!(t.hasOneValue && t.value == Tv.UNKNOWN));
                if (showReg) sb.Append("\n" + identStr + string.Format(reg + " = {0} = {1}", ToolsZ3.ToStringBin(regContent), ToolsZ3.ToStringHex(regContent)));
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
                if (this.Solver_U.NumAssertions > 0)
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

            sb.Append("TailKey=" + this.TailKey+ "; HeadKey=" + this.HeadKey );
            return sb.ToString();
        }
        #endregion

        #region Misc

        public void UpdateConstName(string postfix)
        {
            this.HeadKey = this.HeadKey + postfix;
            this.TailKey = this.TailKey + postfix;

            {
                BoolExpr[] content = this.Solver.Assertions;
                this.Solver.Reset();
                foreach (BoolExpr e in content)
                {
                    this.Solver.Assert(ToolsZ3.UpdateConstName(e, postfix, this.Ctx) as BoolExpr);
                }
            }
            {
                BoolExpr[] content = this.Solver_U.Assertions;
                this.Solver_U.Reset();
                foreach (BoolExpr e in content)
                {
                    this.Solver_U.Assert(ToolsZ3.UpdateConstName(e, postfix, this.Ctx) as BoolExpr);
                }
            }
        }

        public Tv IsConsistent
        {
            get
            {
                this.Solver.Push();
                this.AssertBranchInfoToSolver(false);
                Status result = this.Solver.Check();
                this.Solver.Pop();

                if (result == Status.SATISFIABLE)
                    return Tv.ONE;
                else if (result == Status.UNSATISFIABLE)
                    return Tv.ZERO;
                else
                    return Tv.UNDETERMINED;
            }
        }

        public Tv EqualValues(Rn reg1, Rn reg2)
        {
            return EqualValues(this.Get(reg1), this.Get(reg2));
        }
        public Tv EqualValues(Expr value1, Expr value2)
        {
            //Console.WriteLine("INFO: MemZ3:isEqual: testing whether a=" + a + " is equal to b=" + b);
            const bool method1 = true; // the other method seems not to work

            Tv eq = Tv.UNKNOWN;
            Tv uneq = Tv.UNKNOWN;

            this.Solver.Push();
            this.AssertBranchInfoToSolver(false);
            {
                Status status;
                BoolExpr tmp1 = this.Ctx.MkEq(value1, value2);
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
                BoolExpr tmp1 = this.Ctx.MkNot(this.Ctx.MkEq(value1, value2));
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

        public void Simplify()
        {
            if (SIMPLIFY_ON)
            {
                if (this.Solver_Dirty)
                {
                    ToolsZ3.Consolidate(false, this.Solver, this.Solver_U, this.Ctx);
                    this.Solver_Dirty = false;
                }
                if (this.Solver_U_Dirty)
                {
                    ToolsZ3.Consolidate(true, this.Solver, this.Solver_U, this.Ctx);
                    this.Solver_U_Dirty = false;
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
            Context ctx = this.Ctx;
            foreach (BranchInfo e in this.BranchInfoStore.Values)
            {
                BoolExpr expr = e.GetData(ctx);
                this.Solver.Assert(expr);
                if (addUndef)
                {
                    this.Solver_U.Assert(expr);
                }
            }
        }
        #endregion
    }
}
