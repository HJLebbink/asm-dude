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
        private readonly Tools tools_;
        private readonly Context ctx_;
        private string nextKey_;
        private readonly string prevKey_Regular_;
        private readonly string prevKey_Branch_;
        private readonly BoolExpr branch_Condition_;

        public bool Empty { get; private set; }

        /// <summary>Gets or sets a value indicating whether gets if this stateUpdate is an update in which the state is reset.</summary>
        public bool Reset { get; set; }

        private BranchInfo branchInfo_;

        private readonly object ctxLock_ = new object();

        #endregion

        #region Flags
        private BoolExpr cf_ = null;
        private BoolExpr pf_ = null;
        private BoolExpr af_ = null;
        private BoolExpr zf_ = null;
        private BoolExpr sf_ = null;
        private BoolExpr of_ = null;
        private BoolExpr df_ = null;

        private BoolExpr cf_U_ = null;
        private BoolExpr pf_U_ = null;
        private BoolExpr af_U_ = null;
        private BoolExpr zf_U_ = null;
        private BoolExpr sf_U_ = null;
        private BoolExpr of_U_ = null;
        private BoolExpr df_U_ = null;
        #endregion

        #region Registers
        private BoolExpr rax_ = null;
        private BoolExpr rbx_ = null;
        private BoolExpr rcx_ = null;
        private BoolExpr rdx_ = null;

        private BoolExpr rsi_ = null;
        private BoolExpr rdi_ = null;
        private BoolExpr rbp_ = null;
        private BoolExpr rsp_ = null;

        private BoolExpr r8_ = null;
        private BoolExpr r9_ = null;
        private BoolExpr r10_ = null;
        private BoolExpr r11_ = null;

        private BoolExpr r12_ = null;
        private BoolExpr r13_ = null;
        private BoolExpr r14_ = null;
        private BoolExpr r15_ = null;

        private BoolExpr simd_ = null;

        private BoolExpr rax_U_ = null;
        private BoolExpr rbx_U_ = null;
        private BoolExpr rcx_U_ = null;
        private BoolExpr rdx_U_ = null;

        private BoolExpr rsi_U_ = null;
        private BoolExpr rdi_U_ = null;
        private BoolExpr rbp_U_ = null;
        private BoolExpr rsp_U_ = null;

        private BoolExpr r8_U_ = null;
        private BoolExpr r9_U_ = null;
        private BoolExpr r10_U_ = null;
        private BoolExpr r11_U_ = null;

        private BoolExpr r12_U_ = null;
        private BoolExpr r13_U_ = null;
        private BoolExpr r14_U_ = null;
        private BoolExpr r15_U_ = null;

        private BoolExpr simd_U_ = null;
        #endregion

        #region Memory
        private BoolExpr mem_Update_ = null;
        private BoolExpr mem_Update_U_ = null;
        private ArrayExpr mem_Full_ = null;
        #endregion

        #region Constructor

        /// <summary> Constructor </summary>
        public StateUpdate(string prevKey, string nextKey, Tools tools)
        {
            Contract.Requires(tools != null);

            this.branch_Condition_ = null;
            this.prevKey_Regular_ = prevKey;
            this.prevKey_Branch_ = null;
            this.nextKey_ = nextKey;
            this.tools_ = tools;
            this.ctx_ = new Context(tools.ContextSettings); // housekeeping in Dispose();
            this.Empty = true;
        }

        //TODO consider creating a special StateUpdateMerge class

        /// <summary>Constructor for merging. prevKey_Regular is the key for the regular continue for the provided branchCondition</summary>
        public StateUpdate(BoolExpr branchCondition, string prevKey_Regular, string prevKey_Branch, string nextKey, Tools tools)
        {
            Contract.Requires(tools != null);
            Contract.Requires(branchCondition != null);

            this.ctx_ = new Context(tools.ContextSettings); // housekeeping in Dispose();
            this.branch_Condition_ = branchCondition.Translate(this.ctx_) as BoolExpr;
            this.prevKey_Regular_ = prevKey_Regular;
            this.prevKey_Branch_ = prevKey_Branch;
            this.nextKey_ = nextKey;
            this.tools_ = tools;
            this.Empty = false;
        }
        #endregion

        /// <summary>Update the provided state with this stateUpdate</summary>
        public void Update(State state)
        {
            Contract.Requires(state != null);

            lock (this.ctxLock_)
            {
                if (!this.Reset)
                {
                    state.Assert(this.Value, false, true);
                    state.Assert(this.simd_, false, true);
                }
                state.Assert(this.Undef, true, true);
                state.Assert(this.simd_U_, true, true);

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
                Context ctx = this.ctx_;

                foreach (Flags flag in this.tools_.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, false);
                }
                foreach (Rn reg in this.tools_.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, false);
                }
                if (this.tools_.StateConfig.Mem)
                {
                    if (this.mem_Full_ != null)
                    {
                        yield return this.mem_Update_ ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), this.mem_Full_);
                    }
                    else
                    {
                        if (this.branch_Condition_ == null)
                        {
                            yield return this.mem_Update_ ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), Tools.Create_Mem_Key(this.prevKey_Regular_, ctx));
                        }
                        else
                        {
                            yield return ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), ctx.MkITE(this.branch_Condition_, Tools.Create_Mem_Key(this.prevKey_Regular_, ctx), Tools.Create_Mem_Key(this.prevKey_Branch_, ctx)));
                        }
                    }
                }
            }
        }

        private IEnumerable<BoolExpr> Undef
        {
            get
            {
                Context ctx = this.ctx_;

                foreach (Flags flag in this.tools_.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, true);
                }
                foreach (Rn reg in this.tools_.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, true);
                }
                if (this.tools_.StateConfig.Mem)
                {
                    if (this.mem_Full_ != null)
                    {
                        yield return this.mem_Update_U_ ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), this.mem_Full_);
                    }
                    else
                    {
                        if (this.branch_Condition_ == null)
                        {
                            yield return this.mem_Update_U_ ?? ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), Tools.Create_Mem_Key(this.prevKey_Regular_, ctx));
                        }
                        else
                        {
                            yield return ctx.MkEq(Tools.Create_Mem_Key(this.NextKey, ctx), ctx.MkITE(this.branch_Condition_, Tools.Create_Mem_Key(this.prevKey_Regular_, ctx), Tools.Create_Mem_Key(this.prevKey_Branch_, ctx)));
                        }
                    }
                }
            }
        }

        public BranchInfo BranchInfo
        {
            get { return this.branchInfo_; }

            set
            {
                if (value != null)
                {
                    this.Empty = false;
                }

                if (this.branchInfo_ != null)
                {
                    Console.WriteLine("WARNING: StatusUpdate:BranchInfo.Set: branchInfo is already set.");
                }

                this.branchInfo_ = value;
            }
        }

        private BoolExpr Get_Private(Rn reg, bool undef)
        {
            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;
                if (this.branch_Condition_ == null)
                {
                    return this.Get_Raw_Private(reg, undef) ?? ctx.MkEq(Tools.Create_Key(reg, this.NextKey, ctx), Tools.Create_Key(reg, this.prevKey_Regular_, ctx));
                }
                else
                {
                    return ctx.MkEq(
                        Tools.Create_Key(reg, this.NextKey, ctx),
                        ctx.MkITE(this.branch_Condition_, Tools.Create_Key(reg, this.prevKey_Regular_, ctx), Tools.Create_Key(reg, this.prevKey_Branch_, ctx)));
                }
            }
        }

        private BoolExpr Get_Raw_Private(Rn reg, bool undef)
        {
            switch (reg)
            {
                case Rn.RAX: return undef ? this.rax_U_ : this.rax_;
                case Rn.RBX: return undef ? this.rbx_U_ : this.rbx_;
                case Rn.RCX: return undef ? this.rcx_U_ : this.rcx_;
                case Rn.RDX: return undef ? this.rdx_U_ : this.rdx_;

                case Rn.RSI: return undef ? this.rsi_U_ : this.rsi_;
                case Rn.RDI: return undef ? this.rdi_U_ : this.rdi_;
                case Rn.RBP: return undef ? this.rbp_U_ : this.rbp_;
                case Rn.RSP: return undef ? this.rsp_U_ : this.rsp_;

                case Rn.R8: return undef ? this.r8_U_ : this.r8_;
                case Rn.R9: return undef ? this.r9_U_ : this.r9_;
                case Rn.R10: return undef ? this.r10_U_ : this.r10_;
                case Rn.R11: return undef ? this.r11_U_ : this.r11_;

                case Rn.R12: return undef ? this.r12_U_ : this.r12_;
                case Rn.R13: return undef ? this.r13_U_ : this.r13_;
                case Rn.R14: return undef ? this.r14_U_ : this.r14_;
                case Rn.R15: return undef ? this.r15_U_ : this.r15_;

                default: throw new Exception();
            }
        }

        private BoolExpr Get_Private(Flags flag, bool undef)
        {
            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;
                if (this.branch_Condition_ == null)
                {
                    BoolExpr f1 = Tools.Create_Key(flag, this.NextKey, ctx);
                    BoolExpr f2 = Tools.Create_Key(flag, this.prevKey_Regular_, ctx);
                    return this.Get_Raw_Private(flag, undef) ?? ctx.MkEq(f1, f2);
                }
                else
                {
                    BoolExpr f1 = Tools.Create_Key(flag, this.NextKey, ctx);
                    BoolExpr f2 = Tools.Create_Key(flag, this.prevKey_Regular_, ctx);
                    BoolExpr f3 = Tools.Create_Key(flag, this.prevKey_Branch_, ctx);
                    return ctx.MkEq(f1, ctx.MkITE(this.branch_Condition_, f2, f3));
                }
            }
        }

        private BoolExpr Get_Raw_Private(Flags flag, bool undef)
        {
            switch (flag)
            {
                case Flags.CF: return undef ? this.cf_U_ : this.cf_;
                case Flags.PF: return undef ? this.pf_U_ : this.pf_;
                case Flags.AF: return undef ? this.af_U_ : this.af_;
                case Flags.ZF: return undef ? this.zf_U_ : this.zf_;
                case Flags.SF: return undef ? this.sf_U_ : this.sf_;
                case Flags.OF: return undef ? this.of_U_ : this.of_;
                case Flags.DF: return undef ? this.df_U_ : this.df_;
                default: throw new Exception();
            }
        }
        #endregion

        #region Setters

        public string NextKey
        {
            get { return this.nextKey_; }

            set
            {
                if (this.nextKey_ == null)
                {
                    this.nextKey_ = value;
                }
                else if (this.nextKey_ != value)
                {
                    Context ctx = this.ctx_;

                    foreach (Flags flag in this.tools_.StateConfig.GetFlagOn())
                    {
                        {
                            BoolExpr expr = this.Get_Raw_Private(flag, false);
                            if (expr != null)
                            {
                                this.Set_Private(flag, expr.Substitute(Tools.Create_Key(flag, this.nextKey_, ctx), Tools.Create_Key(flag, value, ctx)) as BoolExpr, false);
                            }
                        }
                        {
                            BoolExpr expr = this.Get_Raw_Private(flag, true);
                            if (expr != null)
                            {
                                this.Set_Private(flag, expr.Substitute(Tools.Create_Key(flag, this.nextKey_, ctx), Tools.Create_Key(flag, value, ctx)) as BoolExpr, true);
                            }
                        }
                    }
                    foreach (Rn reg in this.tools_.StateConfig.GetRegOn())
                    {
                        {
                            BoolExpr expr = this.Get_Raw_Private(reg, false);
                            if (expr != null)
                            {
                                this.Set_Private(reg, expr.Substitute(Tools.Create_Key(reg, this.nextKey_, ctx), Tools.Create_Key(reg, value, ctx)) as BoolExpr, false);
                            }
                        }
                        {
                            BoolExpr expr = this.Get_Raw_Private(reg, true);
                            if (expr != null)
                            {
                                this.Set_Private(reg, expr.Substitute(Tools.Create_Key(reg, this.nextKey_, ctx), Tools.Create_Key(reg, value, ctx)) as BoolExpr, true);
                            }
                        }
                    }
                    if (this.tools_.StateConfig.Mem)
                    {
                        if (this.mem_Update_ != null)
                        {
                            this.mem_Update_ = this.mem_Update_.Substitute(Tools.Create_Mem_Key(this.nextKey_, ctx), Tools.Create_Mem_Key(value, ctx)) as BoolExpr;
                        }
                        if (this.mem_Update_U_ != null)
                        {
                            this.mem_Update_U_ = this.mem_Update_U_.Substitute(Tools.Create_Mem_Key(this.nextKey_, ctx), Tools.Create_Mem_Key(value, ctx)) as BoolExpr;
                        }
                    }
                    this.nextKey_ = value;
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
            lock (this.ctxLock_)
            {
                switch (value)
                {
                    case Tv.ZERO: this.Set(flag, this.ctx_.MkFalse()); break;
                    case Tv.ONE: this.Set(flag, this.ctx_.MkTrue()); break;
                    case Tv.UNKNOWN: this.Set(flag, null, this.ctx_.MkTrue()); break;
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

            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;

                value = value?.Translate(ctx) as BoolExpr;
                undef = undef?.Translate(ctx) as BoolExpr;

                BoolExpr key = Tools.Create_Key(flag, this.NextKey, ctx);
                BoolExpr value_Constraint;
                {
                    if (value == null)
                    {
                        value_Constraint = ctx.MkEq(key, Tools.Create_Flag_Key_Fresh(flag, this.tools_.Rand, ctx));
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
                        undef_Constraint = ctx.MkEq(key, Tools.Create_Flag_Key_Fresh(flag, this.tools_.Rand, ctx));
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

            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;
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
                        this.cf_U_ = value;
                    }
                    else
                    {
                        this.cf_ = value;
                    }
                    break;
                case Flags.PF:
                    if (undef)
                    {
                        this.pf_U_ = value;
                    }
                    else
                    {
                        this.pf_ = value;
                    }
                    break;
                case Flags.AF:
                    if (undef)
                    {
                        this.af_U_ = value;
                    }
                    else
                    {
                        this.af_ = value;
                    }
                    break;
                case Flags.ZF:
                    if (undef)
                    {
                        this.zf_U_ = value;
                    }
                    else
                    {
                        this.zf_ = value;
                    }
                    break;
                case Flags.SF:
                    if (undef)
                    {
                        this.sf_U_ = value;
                    }
                    else
                    {
                        this.sf_ = value;
                    }
                    break;
                case Flags.OF:
                    if (undef)
                    {
                        this.of_U_ = value;
                    }
                    else
                    {
                        this.of_ = value;
                    }
                    break;
                case Flags.DF:
                    if (undef)
                    {
                        this.df_U_ = value;
                    }
                    else
                    {
                        this.df_ = value;
                    }
                    break;
                default: throw new Exception();
            }
        }

        #endregion

        #region Set Register
        public void Set(Rn reg, ulong value)
        {
            BitVecExpr valueExpr = this.ctx_.MkBV(value, (uint)RegisterTools.NBits(reg));
            this.Set(reg, valueExpr, valueExpr);
        }

        public void Set(Rn reg, string value)
        {
            this.Set(reg, ToolsZ3.GetTvArray(value));
        }

        public void Set(Rn reg, Tv[] value)
        {
            (BitVecExpr value, BitVecExpr undef) tup = ToolsZ3.MakeVecExpr(value, this.ctx_);
            this.Set(reg, tup.value, tup.undef);
        }

        /// <summary> Fill all bits of the provided register with the provided truth-value</summary>
        public void Set(Rn reg, Tv value)
        {
            switch (value)
            {
                case Tv.ZERO: this.Set(reg, 0UL); break;
                case Tv.UNKNOWN:
                    BitVecExpr unknown = Tools.Create_Reg_Key_Fresh(reg, this.tools_.Rand, this.ctx_);
                    this.Set(reg, unknown, this.ctx_.MkBV(0, (uint)RegisterTools.NBits(reg)));
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

            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;

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
                                BitVecExpr reg64Expr = Tools.Create_Key(reg64, this.prevKey_Regular_, ctx);
                                BitVecExpr prefix = ctx.MkExtract(63, 16, reg64Expr);
                                value = ctx.MkConcat(prefix, value);
                                undef = ctx.MkConcat(prefix, undef);
                                break;
                            }
                        case 8:
                            {
                                BitVecExpr reg64Expr = Tools.Create_Key(reg64, this.prevKey_Regular_, ctx);
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

                    BitVecExpr prevKey = ctx.MkBVConst(Tools.Reg_Name(reg, this.prevKey_Regular_), max);
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

                    this.simd_ = ctx.MkEq(nextKey, newValue);
                    this.simd_U_ = ctx.MkEq(nextKey, newUndef);
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
                        this.rax_U_ = value;
                    }
                    else
                    {
                        this.rax_ = value;
                    }
                    break;
                case Rn.RBX:
                    if (undef)
                    {
                        this.rbx_U_ = value;
                    }
                    else
                    {
                        this.rbx_ = value;
                    }
                    break;
                case Rn.RCX:
                    if (undef)
                    {
                        this.rcx_U_ = value;
                    }
                    else
                    {
                        this.rcx_ = value;
                    }
                    break;
                case Rn.RDX:
                    if (undef)
                    {
                        this.rdx_U_ = value;
                    }
                    else
                    {
                        this.rdx_ = value;
                    }
                    break;

                case Rn.RSI:
                    if (undef)
                    {
                        this.rsi_U_ = value;
                    }
                    else
                    {
                        this.rsi_ = value;
                    }
                    break;
                case Rn.RDI:
                    if (undef)
                    {
                        this.rdi_U_ = value;
                    }
                    else
                    {
                        this.rdi_ = value;
                    }
                    break;
                case Rn.RBP:
                    if (undef)
                    {
                        this.rbp_U_ = value;
                    }
                    else
                    {
                        this.rbp_ = value;
                    }
                    break;
                case Rn.RSP:
                    if (undef)
                    {
                        this.rsp_U_ = value;
                    }
                    else
                    {
                        this.rsp_ = value;
                    }
                    break;

                case Rn.R8:
                    if (undef)
                    {
                        this.r8_U_ = value;
                    }
                    else
                    {
                        this.r8_ = value;
                    }
                    break;
                case Rn.R9:
                    if (undef)
                    {
                        this.r9_U_ = value;
                    }
                    else
                    {
                        this.r9_ = value;
                    }
                    break;
                case Rn.R10:
                    if (undef)
                    {
                        this.r10_U_ = value;
                    }
                    else
                    {
                        this.r10_ = value;
                    }
                    break;
                case Rn.R11:
                    if (undef)
                    {
                        this.r11_U_ = value;
                    }
                    else
                    {
                        this.r11_ = value;
                    }
                    break;

                case Rn.R12:
                    if (undef)
                    {
                        this.r12_U_ = value;
                    }
                    else
                    {
                        this.r12_ = value;
                    }
                    break;
                case Rn.R13:
                    if (undef)
                    {
                        this.r13_U_ = value;
                    }
                    else
                    {
                        this.r13_ = value;
                    }
                    break;
                case Rn.R14:
                    if (undef)
                    {
                        this.r14_U_ = value;
                    }
                    else
                    {
                        this.r14_ = value;
                    }
                    break;
                case Rn.R15:
                    if (undef)
                    {
                        this.r15_U_ = value;
                    }
                    else
                    {
                        this.r15_ = value;
                    }
                    break;

                default: throw new Exception();
            }
        }
        #endregion

        #region Set Memory
        public void Set_Mem(BitVecExpr address, ulong value, int nBytes)
        {
            BitVecExpr valueExpr = this.ctx_.MkBV(value, (uint)nBytes << 3);
            this.Set_Mem(address, valueExpr);
        }

        public void Set_Mem(BitVecExpr address, string value)
        {
            this.Set_Mem(address, ToolsZ3.GetTvArray(value));
        }

        public void Set_Mem(BitVecExpr address, Tv[] value)
        {
            (BitVecExpr value, BitVecExpr undef) tup = ToolsZ3.MakeVecExpr(value, this.ctx_);
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

            lock (this.ctxLock_)
            {
                Context ctx = this.ctx_;
                address = address.Translate(ctx) as BitVecExpr;
                value = value.Translate(ctx) as BitVecExpr;
                undef = undef.Translate(ctx) as BitVecExpr;

                ArrayExpr newMemContent = Tools.Set_Value_To_Mem(value, address, this.prevKey_Regular_, ctx);
                ArrayExpr newMemContent_U = Tools.Set_Value_To_Mem(undef, address, this.prevKey_Regular_, ctx);
                ArrayExpr memKey = Tools.Create_Mem_Key(this.NextKey, ctx);

                //Console.WriteLine("SetMem: memKey=" + memKey + "; new Value=" + newMemContent);
                if (this.mem_Update_ != null)
                {
                    Console.WriteLine("WARNING: StateUpdate:SetMem: multiple memory updates are not allowed");
                    //throw new Exception("Multiple memory updates are not allowed");
                }
                this.mem_Update_ = ctx.MkEq(memKey, newMemContent);
                this.mem_Update_U_ = ctx.MkEq(memKey, newMemContent_U);
            }
        }

        public void Set_Mem(ArrayExpr memContent)
        {
            Contract.Requires(memContent != null);

            this.Empty = false;

            if (this.mem_Full_ != null)
            {
                Console.WriteLine("WARNING: StateUpdate:SetMem: multiple memory updates are not allowed");
                //throw new Exception("Multiple memory updates are not allowed");
            }
            this.mem_Full_ = memContent.Translate(this.ctx_) as ArrayExpr;
        }

        public void Set_Mem_Unknown()
        {
            this.Set_Mem(Tools.Create_Mem_Key_Fresh(this.tools_.Rand, this.ctx_));
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
                BitVecExpr address = Tools.Calc_Effective_Address(operand, this.prevKey_Regular_, this.ctx_);
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
            foreach (Flags flag in this.tools_.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null)
                {
                    sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
                }
            }
            foreach (Rn reg in this.tools_.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null)
                {
                    sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
                }
            }
            if (this.branchInfo_ != null)
            {
                sb.AppendLine(this.branchInfo_.ToString());
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("StateUpdate: PrevKey=" + this.prevKey_Regular_ + "; NextKey=" + this.NextKey + " ");
            if (this.Empty)
            {
                sb.AppendLine("Empty UpdateState");
            }

            if (this.Reset)
            {
                sb.AppendLine("Reset UpdateState");
            }

            foreach (Flags flag in this.tools_.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null)
                {
                    sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
                }
            }
            foreach (Rn reg in this.tools_.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null)
                {
                    sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
                }
            }
            if (this.branchInfo_ != null)
            {
                sb.AppendLine(this.branchInfo_.ToString());
            }
            return sb.ToString();
        }

        #endregion

        #region IDisposable Support

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~StateUpdate()
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
                    this.cf_?.Dispose();
                    this.pf_?.Dispose();
                    this.af_?.Dispose();
                    this.zf_?.Dispose();
                    this.sf_?.Dispose();
                    this.of_?.Dispose();
                    this.df_?.Dispose();

                    this.cf_U_?.Dispose();
                    this.pf_U_?.Dispose();
                    this.af_U_?.Dispose();
                    this.zf_U_?.Dispose();
                    this.sf_U_?.Dispose();
                    this.of_U_?.Dispose();
                    this.df_U_?.Dispose();

                    this.rax_?.Dispose();
                    this.rbx_?.Dispose();
                    this.rcx_?.Dispose();
                    this.rdx_?.Dispose();

                    this.rsi_?.Dispose();
                    this.rdi_?.Dispose();
                    this.rbp_?.Dispose();
                    this.rsp_?.Dispose();

                    this.r8_?.Dispose();
                    this.r9_?.Dispose();
                    this.r10_?.Dispose();
                    this.r11_?.Dispose();

                    this.r12_?.Dispose();
                    this.r13_?.Dispose();
                    this.r14_?.Dispose();
                    this.r15_?.Dispose();

                    this.simd_?.Dispose();

                    this.rax_U_?.Dispose();
                    this.rbx_U_?.Dispose();
                    this.rcx_U_?.Dispose();
                    this.rdx_U_?.Dispose();

                    this.rsi_U_?.Dispose();
                    this.rdi_U_?.Dispose();
                    this.rbp_U_?.Dispose();
                    this.rsp_U_?.Dispose();

                    this.r8_U_?.Dispose();
                    this.r9_U_?.Dispose();
                    this.r10_U_?.Dispose();
                    this.r11_U_?.Dispose();

                    this.r12_U_?.Dispose();
                    this.r13_U_?.Dispose();
                    this.r14_U_?.Dispose();
                    this.r15_U_?.Dispose();

                    this.simd_U_?.Dispose();
                    this.mem_Update_?.Dispose();
                    this.mem_Update_U_?.Dispose();
                    this.mem_Full_?.Dispose();

                    //TODO HJ 26-10-2019 why when when branch_Condition_ is disposed Get_Private will throw
                    //this.branch_Condition_?.Dispose();

                    this.ctx_.Dispose();
                }
            }
            // free native resources if there are any.
        }
        #endregion
    }
}
