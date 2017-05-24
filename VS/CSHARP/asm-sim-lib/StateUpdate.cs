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
using System.Diagnostics;
using System.Text;

namespace AsmSim
{
    public class StateUpdate
    {
        #region Fields
        private readonly Tools _tools;
        private string _nextKey;
        private string _prevKey;
        private BoolExpr _branchCondition;
        private string _prevKey2;
        public bool Empty { get; private set; }
        private BranchInfo _branchInfo;
        #endregion

        #region Flags
        private BoolExpr _cf = null;
        private BoolExpr _pf = null;
        private BoolExpr _af = null;
        private BoolExpr _zf = null;
        private BoolExpr _sf = null;
        private BoolExpr _of = null;

        private BoolExpr _cf_U = null;
        private BoolExpr _pf_U = null;
        private BoolExpr _af_U = null;
        private BoolExpr _zf_U = null;
        private BoolExpr _sf_U = null;
        private BoolExpr _of_U = null;
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
            this._branchCondition = null;
            this._prevKey = prevKey;
            this._prevKey2 = null;
            this._nextKey = nextKey;
            this._tools = tools;
            this.Empty = true;
        }

        //TODO consider creating a special StateUpdateMerge class
        public StateUpdate(BoolExpr branchCondition, string prevKey1, string prevKey2, string nextKey, Tools tools)
        {
            this._branchCondition = branchCondition;
            this._prevKey = prevKey1;
            this._prevKey2 = prevKey2;
            this._nextKey = nextKey;
            this._tools = tools;
            this.Empty = true;
        }


        #endregion

        //TODO
        public BitVecExpr NextLineNumberExpr { get; set; }

        #region Getters
        public IEnumerable<BoolExpr> Value
        {
            get
            {
                Context ctx = this._tools.Ctx;

                foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, false);
                }
                foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, false);
                }
                if (this._tools.StateConfig.mem)
                {
                    if (this._mem_Full != null)
                    {
                        yield return this._mem_Update ?? ctx.MkEq(Tools.Mem_Key(this.NextKey, ctx), this._mem_Full);
                    }
                    else
                    {
                        yield return this._mem_Update ?? ctx.MkEq(Tools.Mem_Key(this.NextKey, ctx), Tools.Mem_Key(this._prevKey, ctx));
                    }
                }
            }
        }
        public IEnumerable<BoolExpr> Undef
        {
            get
            {
                Context ctx = this._tools.Ctx;

                foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                {
                    yield return this.Get_Private(flag, true);
                }
                foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                {
                    yield return this.Get_Private(reg, true);
                }
                if (this._tools.StateConfig.mem)
                {
                    if (this._mem_Full != null)
                    {
                        yield return this._mem_Update_U ?? ctx.MkEq(Tools.Mem_Key(this.NextKey, ctx), this._mem_Full);
                    }
                    else
                    {
                        if (this._branchCondition == null)
                        {
                            yield return this._mem_Update_U ?? ctx.MkEq(Tools.Mem_Key(this.NextKey, ctx), Tools.Mem_Key(this._prevKey, ctx));
                        }
                        else
                        {
                            throw new Exception("TODO");
                        }
                    }
                }
            }
        }
        public BranchInfo BranchInfo { 
            get { return this._branchInfo; }
            set
            {
                if (value != null) this.Empty = false;
                if (this._branchInfo != null) Console.WriteLine("WARNING: StatusUpdate:BranchInfo.Set: branchInfo is already set.");
                this._branchInfo = value;
            }
        }


        private BoolExpr Get_Private(Rn reg, bool undef)
        {
            Context ctx = this._tools.Ctx;

            if (this._branchCondition == null)
            {
                return Get_Raw_Private(reg, undef) ?? ctx.MkEq(Tools.Reg_Key(reg, this.NextKey, ctx), Tools.Reg_Key(reg, this._prevKey, ctx));
            }
            else
            {
                return ctx.MkEq(
                    Tools.Reg_Key(reg, this.NextKey, ctx), 
                    ctx.MkITE(this._branchCondition, Tools.Reg_Key(reg, this._prevKey, ctx), Tools.Reg_Key(reg, this._prevKey2, ctx))); 
            }

        }
        private BoolExpr Get_Raw_Private(Rn reg, bool undef)
        {
            switch (reg)
            {
                case Rn.RAX: return (undef) ? this._rax_U : this._rax;
                case Rn.RBX: return (undef) ? this._rbx_U : this._rbx;
                case Rn.RCX: return (undef) ? this._rcx_U : this._rcx;
                case Rn.RDX: return (undef) ? this._rdx_U : this._rdx;

                case Rn.RSI: return (undef) ? this._rsi_U : this._rsi;
                case Rn.RDI: return (undef) ? this._rdi_U : this._rdi;
                case Rn.RBP: return (undef) ? this._rbp_U : this._rbp;
                case Rn.RSP: return (undef) ? this._rsp_U : this._rsp;

                case Rn.R8: return (undef) ? this._r8_U : this._r8;
                case Rn.R9: return (undef) ? this._r9_U : this._r9;
                case Rn.R10: return (undef) ? this._r10_U : this._r10;
                case Rn.R11: return (undef) ? this._r11_U : this._r11;

                case Rn.R12: return (undef) ? this._r12_U : this._r12;
                case Rn.R13: return (undef) ? this._r13_U : this._r13;
                case Rn.R14: return (undef) ? this._r14_U : this._r14;
                case Rn.R15: return (undef) ? this._r15_U : this._r15;

                default: throw new Exception();
            }
        }

        private BoolExpr Get_Private(Flags flag, bool undef)
        {
            Context ctx = this._tools.Ctx;
            if (this._branchCondition == null)
            {
                return Get_Raw_Private(flag, undef) ?? ctx.MkEq(Tools.Flag_Key(flag, this.NextKey, ctx), Tools.Flag_Key(flag, this._prevKey, ctx));
            }
            else
            {
                return ctx.MkEq(Tools.Flag_Key(flag, this.NextKey, ctx), ctx.MkITE(this._branchCondition, Tools.Flag_Key(flag, this._prevKey, ctx), Tools.Flag_Key(flag, this._prevKey2, ctx)));
            }

        }
        private BoolExpr Get_Raw_Private(Flags flag, bool undef)
        {
            switch (flag)
            {
                case Flags.CF: return (undef) ? this._cf_U : this._cf;
                case Flags.PF: return (undef) ? this._pf_U : this._pf;
                case Flags.AF: return (undef) ? this._af_U : this._af;
                case Flags.ZF: return (undef) ? this._zf_U : this._zf;
                case Flags.SF: return (undef) ? this._sf_U : this._sf;
                case Flags.OF: return (undef) ? this._of_U : this._of;
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
                    Context ctx = this._tools.Ctx;

                    foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
                    {
                        {
                            var expr = this.Get_Raw_Private(flag, false);
                            if (expr != null) this.Set_Private(flag, expr.Substitute(Tools.Flag_Key(flag, this._nextKey, ctx), Tools.Flag_Key(flag, value, ctx)) as BoolExpr, false);
                        }
                        {
                            var expr = this.Get_Raw_Private(flag, true);
                            if (expr != null) this.Set_Private(flag, expr.Substitute(Tools.Flag_Key(flag, this._nextKey, ctx), Tools.Flag_Key(flag, value, ctx)) as BoolExpr, true);
                        }
                    }
                    foreach (Rn reg in this._tools.StateConfig.GetRegOn())
                    {
                        {
                            var expr = this.Get_Raw_Private(reg, false);
                            if (expr != null) this.Set_Private(reg, expr.Substitute(Tools.Reg_Key(reg, this._nextKey, ctx), Tools.Reg_Key(reg, value, ctx)) as BoolExpr, false);
                        }
                        {
                            var expr = this.Get_Raw_Private(reg, true);
                            if (expr != null) this.Set_Private(reg, expr.Substitute(Tools.Reg_Key(reg, this._nextKey, ctx), Tools.Reg_Key(reg, value, ctx)) as BoolExpr, true);
                        }
                    }
                    if (this._tools.StateConfig.mem)
                    {
                        if (this._mem_Update != null)
                        {
                            this._mem_Update = this._mem_Update.Substitute(Tools.Mem_Key(this._nextKey, ctx), Tools.Mem_Key(value, ctx)) as BoolExpr;
                        }
                        if (this._mem_Update_U != null)
                        {
                            this._mem_Update_U = this._mem_Update_U.Substitute(Tools.Mem_Key(this._nextKey, ctx), Tools.Mem_Key(value, ctx)) as BoolExpr;
                        }
                    }
                    this._nextKey = value;
                }
            }
        }


        #region Set Flag
        public void Set(Flags flag, bool value)
        {
            this.Set(flag, (value) ? Tv.ONE : Tv.ZERO);
        }
        public void Set(Flags flag, Tv value)
        {
            Context ctx = this._tools.Ctx;
            switch (value)
            {
                case Tv.ZERO: this.Set(flag, ctx.MkFalse()); break;
                case Tv.ONE: this.Set(flag, ctx.MkTrue()); break;
                case Tv.UNKNOWN: this.Set(flag, null, ctx.MkTrue()); break;
                case Tv.UNDEFINED: this.Set(flag, null, null); break;
                default: throw new Exception();
            }
        }
        public void Set(Flags flag, BoolExpr value)
        {
            this.Set(flag, value, value);
        }
        public void Set(Flags flag, BoolExpr value, BoolExpr undef)
        {
            this.Empty = false;

            Context ctx = this._tools.Ctx;
            BoolExpr key = Tools.Flag_Key(flag, this.NextKey, ctx);
            BoolExpr value_Constraint;
            {
                if (value == null)
                {
                    value_Constraint = ctx.MkEq(key, Tools.Flag_Key_Fresh(flag, this._tools.Rand, ctx));
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
                    undef_Constraint = ctx.MkEq(key, Tools.Flag_Key_Fresh(flag, this._tools.Rand, ctx));
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
        public void Set_SF_ZF_PF(BitVecExpr value)
        {
            Context ctx = this._tools.Ctx;
            this.Set(Flags.SF, ToolsFlags.Create_SF(value, value.SortSize, ctx));
            this.Set(Flags.ZF, ToolsFlags.Create_ZF(value, ctx));
            this.Set(Flags.PF, ToolsFlags.Create_PF(value, ctx));
        }
        private void Set_Private(Flags flag, BoolExpr value, bool undef)
        {
            switch (flag)
            {
                case Flags.CF: if (undef) this._cf_U = value; else this._cf = value; break;
                case Flags.PF: if (undef) this._pf_U = value; else this._pf = value; break;
                case Flags.AF: if (undef) this._af_U = value; else this._af = value; break;
                case Flags.ZF: if (undef) this._zf_U = value; else this._zf = value; break;
                case Flags.SF: if (undef) this._sf_U = value; else this._sf = value; break;
                case Flags.OF: if (undef) this._of_U = value; else this._of = value; break;
                default: throw new Exception();
            }
        }

        #endregion

        #region Set Register
        public void Set(Rn reg, ulong value)
        {
            BitVecExpr valueExpr = this._tools.Ctx.MkBV(value, (uint)RegisterTools.NBits(reg));
            this.Set(reg, valueExpr, valueExpr);
        }
        public void Set(Rn reg, string value)
        {
            this.Set(reg, ToolsZ3.GetTvArray(value));
        }
        public void Set(Rn reg, Tv[] value)
        {
            var tup = ToolsZ3.MakeVecExpr(value, this._tools.Ctx);
            this.Set(reg, tup.value, tup.undef);
        }
        public void Set(Rn reg, BitVecExpr value)
        {
            this.Set(reg, value, value);
        }
        public void Set(Rn reg, BitVecExpr value, BitVecExpr undef)
        {
            this.Empty = false;

            Debug.Assert(value != null);
            Debug.Assert(undef != null);

            Context ctx = this._tools.Ctx;
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
                        BitVecExpr prefix = ctx.MkExtract(63, 16, Tools.Reg_Key(reg64, this._prevKey, ctx));
                        value = ctx.MkConcat(prefix, value);
                        undef = ctx.MkConcat(prefix, undef);
                        break;
                    }
                case 8:
                    {
                        BitVecExpr reg64Expr = Tools.Reg_Key(reg64, this._prevKey, ctx);
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
                BitVecExpr key = Tools.Reg_Key(reg64, this.NextKey, ctx);
                BoolExpr value_Constraint = ctx.MkEq(key, value) as BoolExpr;
                BoolExpr undef_Constraint = ctx.MkEq(key, undef) as BoolExpr;

                if (this.Get_Raw_Private(reg64, false) != null) throw new Exception("Multiple assignments to register " + reg64);
                this.Set_Private(reg64, value_Constraint, false);
                this.Set_Private(reg64, undef_Constraint, true);
            }
        }
        private void Set_Private(Rn reg, BoolExpr value, bool undef)
        {
            switch (reg)
            {
                case Rn.RAX: if (undef) this._rax_U = value; else this._rax = value; break;
                case Rn.RBX: if (undef) this._rbx_U = value; else this._rbx = value; break;
                case Rn.RCX: if (undef) this._rcx_U = value; else this._rcx = value; break;
                case Rn.RDX: if (undef) this._rdx_U = value; else this._rdx = value; break;

                case Rn.RSI: if (undef) this._rsi_U = value; else this._rsi = value; break;
                case Rn.RDI: if (undef) this._rdi_U = value; else this._rdi = value; break;
                case Rn.RBP: if (undef) this._rbp_U = value; else this._rbp = value; break;
                case Rn.RSP: if (undef) this._rsp_U = value; else this._rsp = value; break;

                case Rn.R8: if (undef) this._r8_U = value; else this._r8 = value; break;
                case Rn.R9: if (undef) this._r9_U = value; else this._r9 = value; break;
                case Rn.R10: if (undef) this._r10_U = value; else this._r10 = value; break;
                case Rn.R11: if (undef) this._r11_U = value; else this._r11 = value; break;

                case Rn.R12: if (undef) this._r12_U = value; else this._r12 = value; break;
                case Rn.R13: if (undef) this._r13_U = value; else this._r13 = value; break;
                case Rn.R14: if (undef) this._r14_U = value; else this._r14 = value; break;
                case Rn.R15: if (undef) this._r15_U = value; else this._r15 = value; break;

                default: throw new Exception();
            }
        }
        #endregion

        #region Set Memory
        public void SetMem(BitVecExpr address, ulong value, int nBytes)
        {
            BitVecExpr valueExpr = this._tools.Ctx.MkBV(value, (uint)nBytes << 3);
            this.SetMem(address, valueExpr);
        }
        public void SetMem(BitVecExpr address, string value)
        {
            this.SetMem(address, ToolsZ3.GetTvArray(value));
        }
        public void SetMem(BitVecExpr address, Tv[] value)
        {
            var tup = ToolsZ3.MakeVecExpr(value, this._tools.Ctx);
            this.SetMem(address, tup.value, tup.undef);
        }
        public void SetMem(BitVecExpr address, BitVecExpr value)
        {
            this.SetMem(address, value, value);
        }
        public void SetMem(BitVecExpr address, BitVecExpr value, BitVecExpr undef)
        {
            this.Empty = false;

            ArrayExpr newMemContent = Tools.Set_Value_To_Mem(value, address, this._prevKey, this._tools.Ctx);
            ArrayExpr newMemContent_U = Tools.Set_Value_To_Mem(undef, address, this._prevKey, this._tools.Ctx);
            ArrayExpr memKey = Tools.Mem_Key(this.NextKey, this._tools.Ctx);

            //Console.WriteLine("SetMem: memKey=" + memKey + "; new Value=" + newMemContent);
            if (this._mem_Update != null) throw new Exception("Multiple memory updates are not allowed");

            this._mem_Update = this._tools.Ctx.MkEq(memKey, newMemContent);
            this._mem_Update_U = this._tools.Ctx.MkEq(memKey, newMemContent_U);
        }
        public void SetMem(ArrayExpr memContent)
        {
            if (this._mem_Full != null) throw new Exception();
            this._mem_Full = memContent;
        }
        #endregion

        #region Set Operand
        public void Set(Operand operand, BitVecExpr value)
        {
            this.Set(operand, value, value);
        }
        public void Set(Operand operand, BitVecExpr value, BitVecExpr undef)
        {
            if (operand.IsReg)
            {
                this.Set(operand.Rn, value, undef);
            }
            else if (operand.IsMem)
            {
                BitVecExpr address = Tools.Calc_Effective_Address(operand, this._prevKey, this._tools.Ctx);
                this.SetMem(address, value, undef);
            }
            else
            {
                throw new Exception();
            }
        }
        #endregion

        #endregion Setters

        /*
        public void Merge(StateUpdate other, BoolExpr branchCondition)
        {
            Debug.Assert(other != null);

            foreach (Rn reg in this._tools.StateConfig.GetRegOn())
            {
                MergeReg(reg, false);
                MergeReg(reg, true);
            }
            foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
            {
                MergeFlag(flag, false);
                MergeFlag(flag, true);
            }

            void MergeReg(Rn reg, bool undef)
            {
                BoolExpr b1 = this.Get_Private(reg, undef);
                BoolExpr b2 = other.Get_Private(reg, undef);

                if (b1 == null)
                {
                    if (b2 != null) this.Set_Private(reg, b2, undef);
                }
                else
                {
                    if (b2 != null) this.Set_Private(reg, this._tools.Ctx.MkITE(branchCondition, b1, b2) as BoolExpr, undef);
                }
            }
            void MergeFlag(Flags flag, bool undef)
            {
                BoolExpr b1 = this.Get_Private(flag, undef);
                BoolExpr b2 = other.Get_Private(flag, undef);

                if (b1 == null)
                {
                    if (b2 != null) this.Set_Private(flag, b2, undef);
                }
                else
                {
                    if (b2 != null) this.Set_Private(flag, this._tools.Ctx.MkITE(branchCondition, b1, b2) as BoolExpr, undef);
                }
            }
        }
        */
        public string ToString2()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null) sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
            }
            foreach (Rn reg in this._tools.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null) sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
            }
            if (this._branchInfo != null)
            {
                sb.AppendLine(this._branchInfo.ToString());
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("StateUpdate: PrevKey=" + this._prevKey + "; NextKey=" + this.NextKey + "\n");
            foreach (Flags flag in this._tools.StateConfig.GetFlagOn())
            {
                BoolExpr b = this.Get_Raw_Private(flag, true);
                if (b != null) sb.AppendLine(flag + ": " + ToolsZ3.ToString(b));
            }
            foreach (Rn reg in this._tools.StateConfig.GetRegOn())
            {
                BoolExpr b = this.Get_Raw_Private(reg, true);
                if (b != null) sb.AppendLine(reg + ": " + ToolsZ3.ToString(b));
            }
            if (this._branchInfo != null)
            {
                sb.AppendLine(this._branchInfo.ToString());
            }
            return sb.ToString();
        }
    }
}
