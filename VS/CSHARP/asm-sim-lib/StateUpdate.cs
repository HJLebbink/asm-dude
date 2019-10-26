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

    public class StateUpdate : IDisposable
    {
        #region Fields
        private readonly Tools _tools;
        private readonly Context _ctx;
        private string _nextKey;
        private readonly string _prevKey_Regular;
        private readonly string _prevKey_Branch;
        private readonly BoolExpr _branch_Condition;

        public bool Empty { get; private set; }

        /// <summary>Gets or sets a value indicating whether gets if this stateUpdate is an update in which the state is reset.</summary>
        public bool Reset { get; set; }

        private BranchInfo _branchInfo;

        private readonly object _ctxLock = new object();

        #endregion

        #region Flags
        private BoolExpr _cf = null;
        private BoolExpr _pf = null;
        private BoolExpr _af = null;
        private BoolExpr _zf = null;
        private BoolExpr _sf = null;
        private BoolExpr _of = null;
        private BoolExpr _df = null;

        private BoolExpr _cf_U = null;
        private BoolExpr _pf_U = null;
        private BoolExpr _af_U = null;
        private BoolExpr _zf_U = null;
        private BoolExpr _sf_U = null;
        private BoolExpr _of_U = null;
        private BoolExpr _df_U = null;
        #endregion

        #region Registers
        private BoolExpr _rax = null;
        private BoolExpr _rbx = null;
        private BoolExpr _rcx = null;
        private BoolExpr _rdx = null;

        private BoolExpr _rsi = null;
        private BoolExpr _rdi = null;
        private BoolExpr _rbp = null;
        private BoolExpr _rsp = null;

        private BoolExpr _r8 = null;
        private BoolExpr _r9 = null;
        private BoolExpr _r10 = null;
        private BoolExpr _r11 = null;

        private BoolExpr _r12 = null;
        private BoolExpr _r13 = null;
        private BoolExpr _r14 = null;
        private BoolExpr _r15 = null;

        private BoolExpr _simd = null;

        private BoolExpr _rax_U = null;
        private BoolExpr _rbx_U = null;
        private BoolExpr _rcx_U = null;
        private BoolExpr _rdx_U = null;

        private BoolExpr _rsi_U = null;
        private BoolExpr _rdi_U = null;
        private BoolExpr _rbp_U = null;
        private BoolExpr _rsp_U = null;

        private BoolExpr _r8_U = null;
        private BoolExpr _r9_U = null;
        private BoolExpr _r10_U = null;
        private BoolExpr _r11_U = null;

        private BoolExpr _r12_U = null;
        private BoolExpr _r13_U = null;
        private BoolExpr _r14_U = null;
        private BoolExpr _r15_U = null;

        private BoolExpr _simd_U = null;
        #endregion

        #region Memory
        private BoolExpr _mem_Update = null;
        private BoolExpr _mem_Update_U = null;
        private ArrayExpr _mem_Full = null;
        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        public StateUpdate(string prevKey, string nextKey, Tools tools)
        {
            Contract.Requires(tools != null);

            this._branch_Condition = null;
            this._prevKey_Regular = prevKey;
            this._prevKey_Branch = null;
            this._nextKey = nextKey;
            this._tools = tools;
            this._ctx = new Context(tools.Settings); // housekeeping in Dispose();
            this.Empty = true;
        }

        //TODO consider creating a special StateUpdateMerge class

        /// <summary>Constructor for merging. prevKey_Regular is the key for the regular continue for the provided branchCondition</summary>
        public StateUpdate(BoolExpr branchCondition, string prevKey_Regular, string prevKey_Branch, string nextKey, Tools tools)
        {
            Contract.Requires(tools != null);
            Contract.Requires(branchCondition != null);

            this._ctx = new Context(tools.Settings); // housekeeping in Dispose();
            this._branch_Condition = branchCondition.Translate(this._ctx) as BoolExpr;
            this._prevKey_Regular = prevKey_Regular;
            this._prevKey_Branch = prevKey_Branch;
            this._nextKey = nextKey;
            this._tools = tools;
            this.Empty = false;
        }
        #endregion

        /// <summary>Update the provided state with this stateUpdate</summary>
        public void Update(State state)
        {
            Contract.Requires(state != null);

            lock (this._ctxLock)
            {
                if (!this.Reset)
                {
                    state.Assert(this.Value, false, true);
                    state.Assert(this._simd, false, true);
                }
                state.Assert(this.Undef, true, true);
                state.Assert(this._simd_U, true, true);

                state.Add(this.BranchInfo);
            }
        }

        //TODO
        public BitVecExpr NextLineNumberExpr { get; set; }

        #region Getters
        private IEnumerable<BoolExpr> Value
        {
            get
            {
                Context ctx = this._ctx;

                foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, false);
                }
                foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, false);
                }
                if (this._tools.StateConfig.Mem)
                {
                    if (this._mem_Full != null)
                    {
                        yield return this._mem_Update ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), this._mem_Full);
                    }
                    else
                    {
                        if (this._branch_Condition == null)
                        {
                            yield return this._mem_Update ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), Tools.Create_Mem_Key(this._prevKey_Regular, ctx));
                        }
                        else
                        {
                            yield return ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), ctx.MkITE(this._branch_Condition, Tools.Create_Mem_Key(this._prevKey_Regular, ctx), Tools.Create_Mem_Key(this._prevKey_Branch, ctx)));
                        }
                    }
                }
            }
        }

        private IEnumerable<BoolExpr> Undef
        {
            get
            {
                Context ctx = this._ctx;

                foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, true);
                }
                foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, true);
                }
                if (this._tools.StateConfig.Mem)
                {
                    if (this._mem_Full != null)
                    {
                        yield return this._mem_Update_U ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), this._mem_Full);
                    }
                    else
                    {
                        if (this._branch_Condition == null)
                        {
                            yield return this._mem_Update_U ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), Tools.Create_Mem_Key(this._prevKey_Regular, ctx));
                        }
                        else
                        {
                            yield return ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), ctx.MkITE(this._branch_Condition, Tools.Create_Mem_Key(this._prevKey_Regular, ctx), Tools.Create_Mem_Key(this._prevKey_Branch, ctx)));
                        }
                    }
                }
            }
        }

        public BranchInfo BranchInfo
        {
            get { return this._branchInfo; }

            set
            {
                if (value != null)
                {
                    this.Empty = false;
                }

                if (this._branchInfo != null)
                {
                    Console.WriteLine("WARNING: StatusUpdate:BranchInfo.Set: branchInfo is already set.");
                }

                this._branchInfo = value;
            }
        }

        private BoolExpr Get_Private(Rn reg, bool undef)
        {
            lock (this._ctxLock)
            {
                Context ctx = this._ctx;
                if (this._branch_Condition == null)
                {
                    return this.Get_Raw_Private(reg, undef) ?? ctx.MkEq(Tools.Create_Key(reg, this.NextKey, ctx), Tools.Create_Key(reg, this._prevKey_Regular, ctx));
                }
                else
                {
                    return ctx.MkEq(
                        Tools.Create_Key(reg, this.NextKey, ctx),
                        ctx.MkITE(this._branch_Condition, Tools.Create_Key(reg, this._prevKey_Regular, ctx), Tools.Create_Key(reg, this._prevKey_Branch, ctx)));
                }
            }
        }

        private BoolExpr Get_Raw_Private(Rn reg, bool undef)
        {
            switch (reg)
            {
                case Rn.RAX: return undef ? this._rax_U : this._rax;
                case Rn.RBX: return undef ? this._rbx_U : this._rbx;
                case Rn.RCX: return undef ? this._rcx_U : this._rcx;
                case Rn.RDX: return undef ? this._rdx_U : this._rdx;

                case Rn.RSI: return undef ? this._rsi_U : this._rsi;
                case Rn.RDI: return undef ? this._rdi_U : this._rdi;
                case Rn.RBP: return undef ? this._rbp_U : this._rbp;
                case Rn.RSP: return undef ? this._rsp_U : this._rsp;

                case Rn.R8: return undef ? this._r8_U : this._r8;
                case Rn.R9: return undef ? this._r9_U : this._r9;
                case Rn.R10: return undef ? this._r10_U : this._r10;
                case Rn.R11: return undef ? this._r11_U : this._r11;

                case Rn.R12: return undef ? this._r12_U : this._r12;
                case Rn.R13: return undef ? this._r13_U : this._r13;
                case Rn.R14: return undef ? this._r14_U : this._r14;
                case Rn.R15: return undef ? this._r15_U : this._r15;

                default: throw new Exception();
            }
        }

        private BoolExpr Get_Private(Flags flag, bool undef)
        {
            lock (this._ctxLock)
            {
                Context ctx = this._ctx;
                if (this._branch_Condition == null)
                {
                    BoolExpr f1 = Tools.Create_Key(flag, this.NextKey, ctx);
                    BoolExpr f2 = Tools.Create_Key(flag, this._prevKey_Regular, ctx);
                    return this.Get_Raw_Private(flag, undef) ?? ctx.MkEq(f1, f2);
                }
                else
                {
                    BoolExpr f1 = Tools.Create_Key(flag, this.NextKey, ctx);
                    BoolExpr f2 = Tools.Create_Key(flag, this._prevKey_Regular, ctx);
                    BoolExpr f3 = Tools.Create_Key(flag, this._prevKey_Branch, ctx);
                    return ctx.MkEq(f1, ctx.MkITE(this._branch_Condition, f2, f3));
                }
            }
        }

        private BoolExpr Get_Raw_Private(Flags flag, bool undef)
        {
            switch (flag)
            {
                case Flags.CF: return undef ? this._cf_U : this._cf;
                case Flags.PF: return undef ? this._pf_U : this._pf;
                case Flags.AF: return undef ? this._af_U : this._af;
                case Flags.ZF: return undef ? this._zf_U : this._zf;
                case Flags.SF: return undef ? this._sf_U : this._sf;
                case Flags.OF: return undef ? this._of_U : this._of;
                case Flags.DF: return undef ? this._df_U : this._df;
                default: throw new Exception();
            }
        }
        #endregion

        #region Setters

        public string NextKey
        {
            get { return this._nextKey; }

            set
            {
                if (this._nextKey == null)
                {
                    this._nextKey = value;
                }
                else if (this._nextKey != value)
                {
                    Context ctx = this._ctx;

                    foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                    {
                        {
                            BoolExpr expr = this.Get_Raw_Private(flag, false);
                            if (expr != null)
                            {
                                this.Set_Private(flag, expr.Substitute(Tools.Create_Key(flag, this._nextKey, ctx), Tools.Create_Key(flag, value, ctx)) as BoolExpr, false);
                            }
                        }
                        {
                            BoolExpr expr = this.Get_Raw_Private(flag, true);
                            if (expr != null)
                            {
                                this.Set_Private(flag, expr.Substitute(Tools.Create_Key(flag, this._nextKey, ctx), Tools.Create_Key(flag, value, ctx)) as BoolExpr, true);
                            }
                        }
                    }
                    foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                    {
                        {
                            BoolExpr expr = this.Get_Raw_Private(reg, false);
                            if (expr != null)
                            {
                                this.Set_Private(reg, expr.Substitute(Tools.Create_Key(reg, this._nextKey, ctx), Tools.Create_Key(reg, value, ctx)) as BoolExpr, false);
                            }
                        }
                        {
                            BoolExpr expr = this.Get_Raw_Private(reg, true);
                            if (expr != null)
                            {
                                this.Set_Private(reg, expr.Substitute(Tools.Create_Key(reg, this._nextKey, ctx), Tools.Create_Key(reg, value, ctx)) as BoolExpr, true);
                            }
                        }
                    }
                    if (this._tools.StateConfig.Mem)
                    {
                        if (this._mem_Update != null)
                        {
                            this._mem_Update = this._mem_Update.Substitute(Tools.Create_Mem_Key(this._nextKey, ctx), Tools.Create_Mem_Key(value, ctx)) as BoolExpr;
                        }
                        if (this._mem_Update_U != null)
                        {
                            this._mem_Update_U = this._mem_Update_U.Substitute(Tools.Create_Mem_Key(this._nextKey, ctx), Tools.Create_Mem_Key(value, ctx)) as BoolExpr;
                        }
                    }
                    this._nextKey = value;
                }
            }
        }

        #region Set Flag
        public void Set(Flags flag, bool value)
        {
            this.Set(flag, value ? Tv.ONE : Tv.ZERO);
        }

        public void Set(Flags flag, Tv value)
        {
            lock (this._ctxLock)
            {
                switch (value)
                {
                    case Tv.ZERO: this.Set(flag, this._ctx.MkFalse()); break;
                    case Tv.ONE: this.Set(flag, this._ctx.MkTrue()); break;
                    case Tv.UNKNOWN: this.Set(flag, null, this._ctx.MkTrue()); break;
                    case Tv.UNDEFINED: this.Set(flag, null, null); break;
                    default: throw new Exception();
                }
            }
        }

        public void Set(Flags flag, BoolExpr value)
        {
            this.Set(flag, value, value);
        }

        public void Set(Flags flag, BoolExpr value, BoolExpr undef)
        {
            this.Empty = false;

            lock (this._ctxLock)
            {
                Context ctx = this._ctx;

                value = value?.Translate(ctx) as BoolExpr;
                undef = undef?.Translate(ctx) as BoolExpr;

                BoolExpr key = Tools.Create_Key(flag, this.NextKey, ctx);
                BoolExpr value_Constraint;
                {
                    if (value == null)
                    {
                        value_Constraint = ctx.MkEq(key, Tools.Create_Flag_Key_Fresh(flag, this._tools.Rand, ctx));
                    }
                    else if (value.IsTrue)
                    {
                        value_Constraint = key;
                    }
                    else if (value.IsFalse)
                    {
                        value_Constraint = ctx.MkNot(key);
                    }
                    else
                    {
                        value_Constraint = ctx.MkEq(key, value);
                    }
                }
                BoolExpr undef_Constraint;
                {
                    if (undef == null)
                    {
                        undef_Constraint = ctx.MkEq(key, Tools.Create_Flag_Key_Fresh(flag, this._tools.Rand, ctx));
                    }
                    else if (undef.IsTrue)
                    {
                        undef_Constraint = key;
                    }
                    else if (undef.IsFalse)
                    {
                        undef_Constraint = ctx.MkNot(key);
                    }
                    else
                    {
                        undef_Constraint = ctx.MkEq(key, undef);
                    }
                }

                this.Set_Private(flag, value_Constraint, false);
                this.Set_Private(flag, undef_Constraint, true);
            }
        }

        public void Set_SF_ZF_PF(BitVecExpr value)
        {
            Contract.Requires(value != null);
            this.Empty = false;

            lock (this._ctxLock)
            {
                Context ctx = this._ctx;
                value = value.Translate(ctx) as BitVecExpr;
                this.Set(Flags.SF, ToolsFlags.Create_SF(value, value.SortSize, ctx));
                this.Set(Flags.ZF, ToolsFlags.Create_ZF(value, ctx));
                this.Set(Flags.PF, ToolsFlags.Create_PF(value, ctx));
            }
        }

        private void Set_Private(Flags flag, BoolExpr value, bool undef)
        {
            switch (flag)
            {
                case Flags.CF:
                    if (undef)
                    {
                        this._cf_U = value;
                    }
                    else
                    {
                        this._cf = value;
                    }
                    break;
                case Flags.PF:
                    if (undef)
                    {
                        this._pf_U = value;
                    }
                    else
                    {
                        this._pf = value;
                    }
                    break;
                case Flags.AF:
                    if (undef)
                    {
                        this._af_U = value;
                    }
                    else
                    {
                        this._af = value;
                    }
                    break;
                case Flags.ZF:
                    if (undef)
                    {
                        this._zf_U = value;
                    }
                    else
                    {
                        this._zf = value;
                    }
                    break;
                case Flags.SF:
                    if (undef)
                    {
                        this._sf_U = value;
                    }
                    else
                    {
                        this._sf = value;
                    }
                    break;
                case Flags.OF:
                    if (undef)
                    {
                        this._of_U = value;
                    }
                    else
                    {
                        this._of = value;
                    }
                    break;
                case Flags.DF:
                    if (undef)
                    {
                        this._df_U = value;
                    }
                    else
                    {
                        this._df = value;
                    }
                    break;
                default: throw new Exception();
            }
        }

        #endregion

        #region Set Register
        public void Set(Rn reg, ulong value)
        {
            BitVecExpr valueExpr = this._ctx.MkBV(value, (uint)RegisterTools.NBits(reg));
            this.Set(reg, valueExpr, valueExpr);
        }

        public void Set(Rn reg, string value)
        {
            this.Set(reg, ToolsZ3.GetTvArray(value));
        }

        public void Set(Rn reg, Tv[] value)
        {
            (BitVecExpr value, BitVecExpr undef) tup = ToolsZ3.MakeVecExpr(value, this._ctx);
            this.Set(reg, tup.value, tup.undef);
        }

        /// <summary> Fill all bits of the provided register with the provided truth-value</summary>
        public void Set(Rn reg, Tv value)
        {
            switch (value)
            {
                case Tv.ZERO: this.Set(reg, 0UL); break;
                case Tv.UNKNOWN:
                    BitVecExpr unknown = Tools.Create_Reg_Key_Fresh(reg, this._tools.Rand, this._ctx);
                    this.Set(reg, unknown, this._ctx.MkBV(0, (uint)RegisterTools.NBits(reg)));
                    break;
                case Tv.INCONSISTENT:
                case Tv.UNDEFINED:
                case Tv.ONE:
                    throw new Exception("Not implemented yet");
                    break;
                default: break;
            }
        }

        public void Set(Rn reg, BitVecExpr value)
        {
            this.Set(reg, value, value);
        }

        public void Set(Rn reg, BitVecExpr value, BitVecExpr undef)
        {
            Contract.Requires(value != null);
            Contract.Requires(undef != null);

            this.Empty = false;

            lock (this._ctxLock)
            {
                Context ctx = this._ctx;

                value = value.Translate(ctx) as BitVecExpr;
                undef = undef.Translate(ctx) as BitVecExpr;

                if (RegisterTools.IsGeneralPurposeRegister(reg))
                {
                    Rn reg64 = RegisterTools.Get64BitsRegister(reg);
                    uint nBits = value.SortSize;
                    switch (nBits)
                    {
                        case 64: break;
                        case 32:
                            {
                                value = ctx.MkZeroExt(32, value);
                                undef = ctx.MkZeroExt(32, undef);
                                break;
                            }
                        case 16:
                            {
                                BitVecExpr reg64Expr = Tools.Create_Key(reg64, this._prevKey_Regular, ctx);
                                BitVecExpr prefix = ctx.MkExtract(63, 16, reg64Expr);
                                value = ctx.MkConcat(prefix, value);
                                undef = ctx.MkConcat(prefix, undef);
                                break;
                            }
                        case 8:
                            {
                                BitVecExpr reg64Expr = Tools.Create_Key(reg64, this._prevKey_Regular, ctx);
                                if (RegisterTools.Is8BitHigh(reg))
                                {
                                    BitVecExpr postFix = ctx.MkExtract(7, 0, reg64Expr);
                                    BitVecExpr prefix = ctx.MkExtract(63, 16, reg64Expr);
                                    value = ctx.MkConcat(ctx.MkConcat(prefix, value), postFix);
                                    undef = ctx.MkConcat(ctx.MkConcat(prefix, undef), postFix);
                                }
                                else
                                {
                                    BitVecExpr prefix = ctx.MkExtract(63, 8, reg64Expr);
                                    value = ctx.MkConcat(prefix, value);
                                    undef = ctx.MkConcat(prefix, undef);
                                }
                                break;
                            }
                        default:
                            {
                                Console.WriteLine("ERROR: Set: bits=" + nBits + "; value=" + value + "; undef=" + undef);
                                throw new Exception();
                            }
                    }
                    {
                        BitVecExpr key = Tools.Create_Key(reg64, this.NextKey, ctx);
                        BoolExpr value_Constraint = ctx.MkEq(key, value) as BoolExpr;
                        BoolExpr undef_Constraint = ctx.MkEq(key, undef) as BoolExpr;

                        if (this.Get_Raw_Private(reg64, false) != null)
                        {
                            throw new Exception("Multiple assignments to register " + reg64);
                        }

                        this.Set_Private(reg64, value_Constraint, false);
                        this.Set_Private(reg64, undef_Constraint, true);
                    }
                }
                else if (RegisterTools.Is_SIMD_Register(reg))
                {
                    uint max = 512 * 32;

                    BitVecExpr prevKey = ctx.MkBVConst(Tools.Reg_Name(reg, this._prevKey_Regular), max);
                    (uint high, uint low) = Tools.SIMD_Extract_Range(reg);

                    BitVecExpr top = null;
                    BitVecExpr bottom = null;
                    if (high < (max - 1))
                    {
                        top = ctx.MkExtract(max - 1, high + 1, prevKey);
                    }

                    if (low > 0)
                    {
                        bottom = ctx.MkExtract(low - 1, 0, prevKey);
                    }

                    Console.WriteLine(top.SortSize + "+" + value.SortSize + "+" + bottom.SortSize + "=" + prevKey.SortSize);

                    BitVecExpr newValue = (top == null) ? value : ctx.MkConcat(top, value) as BitVecExpr;
                    newValue = (bottom == null) ? newValue : ctx.MkConcat(newValue, bottom);
                    BitVecExpr newUndef = (top == null) ? value : ctx.MkConcat(top, value) as BitVecExpr;
                    newUndef = (bottom == null) ? newUndef : ctx.MkConcat(newUndef, bottom);

                    BitVecExpr nextKey = ctx.MkBVConst(Tools.Reg_Name(reg, this.NextKey), 512 * 32);
                    //Debug.Assert(newValue.SortSize == nextKey.SortSize);

                    this._simd = ctx.MkEq(nextKey, newValue);
                    this._simd_U = ctx.MkEq(nextKey, newUndef);
                }
                else
                {
                    // do nothing;
                }
            }
        }

        private void Set_Private(Rn reg, BoolExpr value, bool undef)
        {
            switch (reg)
            {
                case Rn.RAX:
                    if (undef)
                    {
                        this._rax_U = value;
                    }
                    else
                    {
                        this._rax = value;
                    }
                    break;
                case Rn.RBX:
                    if (undef)
                    {
                        this._rbx_U = value;
                    }
                    else
                    {
                        this._rbx = value;
                    }
                    break;
                case Rn.RCX:
                    if (undef)
                    {
                        this._rcx_U = value;
                    }
                    else
                    {
                        this._rcx = value;
                    }
                    break;
                case Rn.RDX:
                    if (undef)
                    {
                        this._rdx_U = value;
                    }
                    else
                    {
                        this._rdx = value;
                    }
                    break;

                case Rn.RSI:
                    if (undef)
                    {
                        this._rsi_U = value;
                    }
                    else
                    {
                        this._rsi = value;
                    }
                    break;
                case Rn.RDI:
                    if (undef)
                    {
                        this._rdi_U = value;
                    }
                    else
                    {
                        this._rdi = value;
                    }
                    break;
                case Rn.RBP:
                    if (undef)
                    {
                        this._rbp_U = value;
                    }
                    else
                    {
                        this._rbp = value;
                    }
                    break;
                case Rn.RSP:
                    if (undef)
                    {
                        this._rsp_U = value;
                    }
                    else
                    {
                        this._rsp = value;
                    }
                    break;

                case Rn.R8:
                    if (undef)
                    {
                        this._r8_U = value;
                    }
                    else
                    {
                        this._r8 = value;
                    }
                    break;
                case Rn.R9:
                    if (undef)
                    {
                        this._r9_U = value;
                    }
                    else
                    {
                        this._r9 = value;
                    }
                    break;
                case Rn.R10:
                    if (undef)
                    {
                        this._r10_U = value;
                    }
                    else
                    {
                        this._r10 = value;
                    }
                    break;
                case Rn.R11:
                    if (undef)
                    {
                        this._r11_U = value;
                    }
                    else
                    {
                        this._r11 = value;
                    }
                    break;

                case Rn.R12:
                    if (undef)
                    {
                        this._r12_U = value;
                    }
                    else
                    {
                        this._r12 = value;
                    }
                    break;
                case Rn.R13:
                    if (undef)
                    {
                        this._r13_U = value;
                    }
                    else
                    {
                        this._r13 = value;
                    }
                    break;
                case Rn.R14:
                    if (undef)
                    {
                        this._r14_U = value;
                    }
                    else
                    {
                        this._r14 = value;
                    }
                    break;
                case Rn.R15:
                    if (undef)
                    {
                        this._r15_U = value;
                    }
                    else
                    {
                        this._r15 = value;
                    }
                    break;

                default: throw new Exception();
            }
        }
        #endregion

        #region Set Memory
        public void Set_Mem(BitVecExpr address, ulong value, int nBytes)
        {
            BitVecExpr valueExpr = this._ctx.MkBV(value, (uint)nBytes << 3);
            this.Set_Mem(address, valueExpr);
        }

        public void Set_Mem(BitVecExpr address, string value)
        {
            this.Set_Mem(address, ToolsZ3.GetTvArray(value));
        }

        public void Set_Mem(BitVecExpr address, Tv[] value)
        {
            (BitVecExpr value, BitVecExpr undef) tup = ToolsZ3.MakeVecExpr(value, this._ctx);
            this.Set_Mem(address, tup.value, tup.undef);
        }

        public void Set_Mem(BitVecExpr address, BitVecExpr value)
        {
            this.Set_Mem(address, value, value);
        }

        public void Set_Mem(BitVecExpr address, BitVecExpr value, BitVecExpr undef)
        {
            Contract.Requires(address != null);
            Contract.Requires(value != null);
            Contract.Requires(undef != null);

            this.Empty = false;

            lock (this._ctxLock)
            {
                Context ctx = this._ctx;
                address = address.Translate(ctx) as BitVecExpr;
                value = value.Translate(ctx) as BitVecExpr;
                undef = undef.Translate(ctx) as BitVecExpr;

                ArrayExpr newMemContent = Tools.Set_Value_To_Mem(value, address, this._prevKey_Regular, ctx);
                ArrayExpr newMemContent_U = Tools.Set_Value_To_Mem(undef, address, this._prevKey_Regular, ctx);
                ArrayExpr memKey = Tools.Create_Mem_Key(this.NextKey, ctx);

                //Console.WriteLine("SetMem: memKey=" + memKey + "; new Value=" + newMemContent);
                if (this._mem_Update != null)
                {
                    Console.WriteLine("WARNING: StateUpdate:SetMem: multiple memory updates are not allowed");
                    //throw new Exception("Multiple memory updates are not allowed");
                }
                this._mem_Update = ctx.MkEq(memKey, newMemContent);
                this._mem_Update_U = ctx.MkEq(memKey, newMemContent_U);
            }
        }

        public void Set_Mem(ArrayExpr memContent)
        {
            Contract.Requires(memContent != null);

            this.Empty = false;

            if (this._mem_Full != null)
            {
                Console.WriteLine("WARNING: StateUpdate:SetMem: multiple memory updates are not allowed");
                //throw new Exception("Multiple memory updates are not allowed");
            }
            this._mem_Full = memContent.Translate(this._ctx) as ArrayExpr;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public void Set_Mem_Unknown()
        {
            this.Set_Mem(Tools.Create_Mem_Key_Fresh(this._tools.Rand, this._ctx));
        }

        #endregion

        #region Set Operand
        public void Set(Operand operand, BitVecExpr value)
        {
            this.Set(operand, value, value);
        }

        public void Set(Operand operand, BitVecExpr value, BitVecExpr undef)
        {
            Contract.Requires(operand != null);

            if (operand.IsReg)
            {
                this.Set(operand.Rn, value, undef);
            }
            else if (operand.IsMem)
            {
                BitVecExpr address = Tools.Calc_Effective_Address(operand, this._prevKey_Regular, this._ctx);
                this.Set_Mem(address, value, undef);
            }
            else
            {
                throw new Exception();
            }
        }
        #endregion

        #endregion Setters

        #region ToString

        public string ToString2()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null)
                {
                    sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
                }
            }
            foreach (Rn reg in this._tools.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null)
                {
                    sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
                }
            }
            if (this._branchInfo != null)
            {
                sb.AppendLine(this._branchInfo.ToString());
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("StateUpdate: PrevKey=" + this._prevKey_Regular + "; NextKey=" + this.NextKey + " ");
            if (this.Empty)
            {
                sb.AppendLine("Empty UpdateState");
            }

            if (this.Reset)
            {
                sb.AppendLine("Reset UpdateState");
            }

            foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null)
                {
                    sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
                }
            }
            foreach (Rn reg in this._tools.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null)
                {
                    sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
                }
            }
            if (this._branchInfo != null)
            {
                sb.AppendLine(this._branchInfo.ToString());
            }
            return sb.ToString();
        }

        #endregion
        public bool Disposed = false;

        public void Dispose()
        {
            this.Disposed = true;
            lock (this._ctxLock)
            {
                this._cf?.Dispose();
                this._pf?.Dispose();
                this._af?.Dispose();
                this._zf?.Dispose();
                this._sf?.Dispose();
                this._of?.Dispose();
                this._df?.Dispose();

                this._cf_U?.Dispose();
                this._pf_U?.Dispose();
                this._af_U?.Dispose();
                this._zf_U?.Dispose();
                this._sf_U?.Dispose();
                this._of_U?.Dispose();
                this._df_U?.Dispose();

                this._rax?.Dispose();
                this._rbx?.Dispose();
                this._rcx?.Dispose();
                this._rdx?.Dispose();

                this._rsi?.Dispose();
                this._rdi?.Dispose();
                this._rbp?.Dispose();
                this._rsp?.Dispose();

                this._r8?.Dispose();
                this._r9?.Dispose();
                this._r10?.Dispose();
                this._r11?.Dispose();

                this._r12?.Dispose();
                this._r13?.Dispose();
                this._r14?.Dispose();
                this._r15?.Dispose();

                this._simd?.Dispose();

                this._rax_U?.Dispose();
                this._rbx_U?.Dispose();
                this._rcx_U?.Dispose();
                this._rdx_U?.Dispose();

                this._rsi_U?.Dispose();
                this._rdi_U?.Dispose();
                this._rbp_U?.Dispose();
                this._rsp_U?.Dispose();

                this._r8_U?.Dispose();
                this._r9_U?.Dispose();
                this._r10_U?.Dispose();
                this._r11_U?.Dispose();

                this._r12_U?.Dispose();
                this._r13_U?.Dispose();
                this._r14_U?.Dispose();
                this._r15_U?.Dispose();

                this._simd_U?.Dispose();
                this._mem_Update?.Dispose();
                this._mem_Update_U?.Dispose();
                this._mem_Full?.Dispose();

                this._ctx.Dispose();
            }
        }
    }
}
