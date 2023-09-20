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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmSim
{
    namespace Mnemonics
    {
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Diagnostics.Contracts;
        using System.Globalization;
        using System.Linq;
        using AsmTools;
        using Microsoft.Z3;

        #region Instructions
        #region Abstract OpcodeBases
        public abstract class OpcodeBase : IDisposable
        {
            #region Fields
            public static readonly CultureInfo Culture = CultureInfo.CurrentUICulture;

            public readonly Mnemonic mnemonic_;
            private readonly string[] args_;
            public readonly Tools tools_;
            protected readonly Context ctx_;

            protected (string prevKey, string nextKey, string nextKeyBranch) keys_;

            private bool halted_;
            private string haltMessage_;
            private string warningMessage_;

            private StateUpdate regularUpdate_;
            private StateUpdate branchUpdate_;
            #endregion

            protected void Create_RegularUpdate()
            {
                if (this.regularUpdate_ == null)
                {
                    this.regularUpdate_ = new StateUpdate(this.keys_.prevKey, this.keys_.nextKey, this.tools_);
                }
            }

            protected void Create_BranchUpdate()
            {
                if (this.branchUpdate_ == null)
                {
                    this.branchUpdate_ = new StateUpdate(this.keys_.prevKey, this.keys_.nextKeyBranch, this.tools_);
                }
            }

            protected StateUpdate RegularUpdate
            {
                get
                {
                    if (this.regularUpdate_ == null)
                    {
                        this.regularUpdate_ = new StateUpdate(this.keys_.prevKey, this.keys_.nextKey, this.tools_);
                    }

                    return this.regularUpdate_;
                }
            }

            protected StateUpdate BranchUpdate
            {
                get
                {
                    if (this.branchUpdate_ == null)
                    {
                        this.branchUpdate_ = new StateUpdate(this.keys_.prevKey, this.keys_.nextKeyBranch, this.tools_);
                    }

                    return this.branchUpdate_;
                }
            }

            public OpcodeBase(Mnemonic m, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
            {
                Contract.Requires(t != null);
                Contract.Requires(args != null);

                this.mnemonic_ = m;
                this.args_ = args;
                this.tools_ = t;
                this.keys_ = keys;
                try {
                    this.ctx_ = new Context(t.ContextSettings);
                } catch
                {
                    //TODO
                }
            }

            public abstract void Execute();

            #region Registers/Flags Getters

            /// <summary>Get the current value of the provided register</summary>
            public BitVecExpr Get(Rn regName)
            {
                return Tools.Create_Key(regName, this.keys_.prevKey, this.ctx_);
            }

            public static BitVecExpr Get(Rn regName, string prevKey, Context ctx)
            {
                return Tools.Create_Key(regName, prevKey, ctx);
            }

            public BitVecExpr Undef(Rn regName)
            {
                return Tools.Create_Reg_Key_Fresh(regName, this.tools_.Rand, this.ctx_);
            }

            public static BitVecExpr Undef(Rn regName, Random rand, Context ctx)
            {
                return Tools.Create_Reg_Key_Fresh(regName, rand, ctx);
            }

            /// <summary>Get the current value of the provided flag</summary>
            public BoolExpr Get(Flags flagName)
            {
                return Tools.Create_Key(flagName, this.keys_.prevKey, this.ctx_);
            }

            public static BoolExpr Get(Flags flagName, string prevKey, Context ctx)
            {
                return Tools.Create_Key(flagName, prevKey, ctx);
            }

            public BoolExpr Undef(Flags flagName)
            {
                return Tools.Create_Flag_Key_Fresh(flagName, this.tools_.Rand, this.ctx_);
            }

            public static BoolExpr Undef(Flags flagName, Random rand, Context ctx)
            {
                return Tools.Create_Flag_Key_Fresh(flagName, rand, ctx);
            }

            public BitVecExpr GetMem(Rn regName, int nBytes)
            {
                return this.GetMem(this.Get(regName), nBytes);
            }

            public BitVecExpr GetMem(BitVecExpr address, int nBytes)
            {
                return Tools.Create_Value_From_Mem(address, nBytes, this.keys_.prevKey, this.ctx_);
            }
            #endregion

            public (StateUpdate regular, StateUpdate branch) Updates
            {
                get { return (this.regularUpdate_, this.branchUpdate_); }
            }

            #region Register/Flags read/write

            /// <summary>Gets the flags that are read by this Mnemnonic</summary>
            public virtual Flags FlagsReadStatic { get { return Flags.NONE; } }

            /// <summary>Gets the flags that are written by this Mnemnonic</summary>
            public virtual Flags FlagsWriteStatic { get { return Flags.NONE; } }

            public virtual IEnumerable<Rn> RegsReadStatic { get { return Enumerable.Empty<Rn>(); } }

            public virtual IEnumerable<Rn> RegsWriteStatic { get { return Enumerable.Empty<Rn>(); } }

            public virtual bool MemReadStatic { get { return false; } }

            public virtual bool MemWriteStatic { get { return false; } }
            #endregion

            public string Warning
            {
                get
                {
                    return this.warningMessage_;
                }

                protected set
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

            public bool IsHalted { get { return this.halted_; } }

            public string SyntaxError
            {
                get
                {
                    return this.haltMessage_;
                }

                protected set
                {
                    if (this.haltMessage_ == null)
                    {
                        this.haltMessage_ = value;
                    }
                    else
                    {
                        this.haltMessage_ += Environment.NewLine + value;
                    }
                    this.halted_ = true;
                }
            }

            public override string ToString()
            {
                return this.mnemonic_ + " " + string.Join(", ", this.args_);
            }

            #region Protected stuff

            /// <summary>Gets number of operand of the arguments of this instruction</summary>
            protected int NOperands { get { return this.args_.Length; } }

            public static BitVecExpr OpValue(
                Operand operand,
                string key,
                Context ctx,
                int nBits = -1)
            {
                Contract.Requires(operand != null);
                Contract.Requires(ctx != null);

                try
                {
                    if (operand == null)
                    {
                        return null;
                    }
                    if (nBits == -1)
                    {
                        nBits = operand.NBits;
                    }
                    switch (operand.Type)
                    {
                        case Ot1.reg:
                            {
                                return Tools.Create_Key(operand.Rn, key, ctx);
                            }
                        case Ot1.mem:
                            {
                                BitVecExpr address = Tools.Calc_Effective_Address(operand, key, ctx);
                                int nBytes = nBits >> 3;
                                return Tools.Create_Value_From_Mem(address, nBytes, key, ctx);
                            }
                        case Ot1.imm:
                            {
                                return ctx.MkBV(operand.Imm, (uint)nBits);
                            }
                        case Ot1.UNKNOWN:
                        default:
                            {
                                Console.WriteLine("WARNING: OpcodeBase:OpValue. unknown operand type, unknown what to do.");
                                return null;
                            }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: OpcodeBase:OpValue: op=" + operand.ToString() + ": exception " + e.ToString());
                    return null;
                }
            }

            protected bool ToMemReadWrite(Operand op1)
            {
                return (op1 == null) ? false : op1.IsMem;
            }

            protected bool ToMemReadWrite(Operand op1, Operand op2)
            {
                return ((op1 == null) ? false : op1.IsMem) || ((op2 == null) ? false : op2.IsMem);
            }

            protected bool ToMemReadWrite(Operand op1, Operand op2, Operand op3)
            {
                return ((op1 == null) ? false : op1.IsMem) || ((op2 == null) ? false : op2.IsMem) || ((op3 == null) ? false : op3.IsMem);
            }

            protected static IEnumerable<Rn> ReadRegs(Operand op1, bool op1_IsWrite)
            {
                if (op1 != null)
                {
                    if (op1.IsMem)
                    {
                        (Rn baseReg, Rn indexReg, int scale, long displacement) = op1.Mem;
                        if (baseReg != Rn.NOREG)
                        {
                            yield return baseReg;
                        }

                        if (indexReg != Rn.NOREG)
                        {
                            yield return indexReg;
                        }
                    }
                    if ((!op1_IsWrite) && op1.IsReg)
                    {
                        yield return op1.Rn;
                    }
                }
            }

            protected static IEnumerable<Rn> ReadRegs(Operand op1, bool op1_IsWrite, Operand op2, bool op2_IsWrite)
            {
                foreach (Rn r in ReadRegs(op1, op1_IsWrite))
                {
                    yield return r;
                }

                foreach (Rn r in ReadRegs(op2, op2_IsWrite))
                {
                    yield return r;
                }
            }

            protected static IEnumerable<Rn> ReadRegs(Operand op1, bool op1_IsWrite, Operand op2, bool op2_IsWrite, Operand op3, bool op3_IsWrite)
            {
                foreach (Rn r in ReadRegs(op1, op1_IsWrite))
                {
                    yield return r;
                }

                foreach (Rn r in ReadRegs(op2, op2_IsWrite))
                {
                    yield return r;
                }

                foreach (Rn r in ReadRegs(op3, op3_IsWrite))
                {
                    yield return r;
                }
            }

            protected static IEnumerable<Rn> WriteRegs(Operand op1)
            {
                if ((op1 != null) && op1.IsReg)
                {
                    yield return op1.Rn;
                }
            }

            protected static IEnumerable<Rn> WriteRegs(Operand op1, Operand op2)
            {
                foreach (Rn r in WriteRegs(op1))
                {
                    yield return r;
                }

                foreach (Rn r in WriteRegs(op2))
                {
                    yield return r;
                }
            }

            protected static IEnumerable<Rn> WriteRegs(Operand op1, Operand op2, Operand op3)
            {
                foreach (Rn r in WriteRegs(op1))
                {
                    yield return r;
                }

                foreach (Rn r in WriteRegs(op2))
                {
                    yield return r;
                }

                foreach (Rn r in WriteRegs(op3))
                {
                    yield return r;
                }
            }

            /// <summary>Create Syntax Error that op1 and op2 should have been equal size</summary>
            protected void CreateSyntaxError1(Operand op1, Operand op2)
            {
                Contract.Requires(op1 != null);
                Contract.Requires(op2 != null);
                this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 and 2 should have same number of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), op1, op1.Type, op1.NBits, op2, op2.Type, op2.NBits);
            }

            #endregion

            #region IDisposable Support
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~OpcodeBase()
            {
                this.Dispose(false);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // free managed resources
                    this.ctx_?.Dispose();
                    /* // TODO HJ 26 okt 2019: why does disposing this does not work?
                    if (this.branchUpdate_ != null)
                    {
                        this.branchUpdate_.Dispose();
                        this.branchUpdate_ = null;
                    }
                    if (this.regularUpdate_ != null)
                    {
                        this.regularUpdate_.Dispose();
                        this.regularUpdate_ = null;
                    }
                    */
                }
                // free native resources if there are any.
            }
            #endregion
        }

        public abstract class Opcode0Base : OpcodeBase
        {
            public Opcode0Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                Contract.Requires(args != null);

                if (this.NOperands != 0)
                {
                    this.SyntaxError = (this.NOperands == 1)
                        ? string.Format(Culture, "\"{0}\": Expected no operands. Found 1 operand with value \"{1}\".", this.ToString(), args[0])
                        : string.Format(Culture, "\"{0}\": Expected no operands. Found {1} operands with values \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                }
            }
        }

        public abstract class Opcode1Base : OpcodeBase
        {
            protected readonly Operand op1_;

            public Opcode1Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                Contract.Requires(args != null);

                if (this.NOperands == 1)
                {
                    this.op1_ = new Operand(args[0], false);
                    if (this.op1_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1_.ErrorMessage);
                    }
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 1 operand. Found 0 operands.", this.ToString());
                    }
                    else
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 1 operand. Found {1} operands with values \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }

            public Opcode1Base(Mnemonic mnemonic, string[] args, Ot1 allowedOperands1, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!allowedOperands1.HasFlag(this.op1_.Type))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": First operand ({1}) cannot be of type {2}. Allowed types: {3}.", this.ToString(), this.op1_, this.op1_.Type, AsmSourceTools.ToString(allowedOperands1));
                }
            }

            public BitVecExpr Op1Value { get { return OpValue(this.op1_, this.keys_.prevKey, this.ctx_); } }

            public override bool MemReadStatic { get { return this.ToMemReadWrite(this.op1_); } }

            public override bool MemWriteStatic { get { return this.ToMemReadWrite(this.op1_); } }
        }

        public abstract class Opcode2Base : OpcodeBase
        {
            protected readonly Operand op1_;
            protected readonly Operand op2_;

            public Opcode2Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                Contract.Requires(args != null);

                if (this.NOperands == 2)
                {
                    this.op1_ = new Operand(args[0], false);
                    this.op2_ = new Operand(args[1], false);
                    if (this.op1_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1_.ErrorMessage);
                    }

                    if (this.op2_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2_.ErrorMessage);
                    }
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 2 operands. Found 0 operands.", this.ToString());
                    }
                    else
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 2 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }

            public Opcode2Base(Mnemonic mnemonic, string[] args, Ot2 allowedOperands2, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!allowedOperands2.HasFlag(AsmSourceTools.MergeOt(this.op1_.Type, this.op2_.Type)))
                {
                    this.SyntaxError = string.Format(
                        "\"{0}\": Invalid combination of opcode and operands. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}). Allowed types: {7}.",
                        this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, AsmSourceTools.ToString(allowedOperands2));
                }
            }

            public BitVecExpr Op1Value { get { return OpValue(this.op1_, this.keys_.prevKey, this.ctx_); } }

            public BitVecExpr Op2Value { get { return OpValue(this.op2_, this.keys_.prevKey, this.ctx_); } }

            public override bool MemReadStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_); } }

            public override bool MemWriteStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_); } }
        }

        public abstract class Opcode3Base : OpcodeBase
        {
            protected readonly Operand op1_;
            protected readonly Operand op2_;
            protected readonly Operand op3_;

            public Opcode3Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                Contract.Requires(args != null);

                if (this.NOperands == 3)
                {
                    this.op1_ = new Operand(args[0], false);
                    this.op2_ = new Operand(args[1], false);
                    this.op3_ = new Operand(args[2], false);

                    if (this.op1_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1_.ErrorMessage);
                    }

                    if (this.op2_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2_.ErrorMessage);
                    }

                    if (this.op3_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 3 is malformed: {1}", this.ToString(), this.op3_.ErrorMessage);
                    }
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 3 operands. Found 0 operands.", this.ToString());
                    }
                    else
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 3 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }

            public Opcode3Base(Mnemonic mnemonic, string[] args, Ot3 allowedOperands3, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!allowedOperands3.HasFlag(AsmSourceTools.MergeOt(this.op1_.Type, this.op2_.Type, this.op3_.Type)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}) Allowed types: {10}.", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits, AsmSourceTools.ToString(allowedOperands3));
                }
            }

            public BitVecExpr Op1Value { get { return OpValue(this.op1_, this.keys_.prevKey, this.ctx_); } }

            public BitVecExpr Op2Value { get { return OpValue(this.op2_, this.keys_.prevKey, this.ctx_); } }

            public BitVecExpr Op3Value { get { return OpValue(this.op3_, this.keys_.prevKey, this.ctx_); } }

            public override bool MemReadStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_, this.op3_); } }

            public override bool MemWriteStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_, this.op3_); } }
        }

        public abstract class OpcodeNBase : OpcodeBase
        {
            protected readonly Operand op1_;
            protected readonly Operand op2_;
            protected readonly Operand op3_;

            public OpcodeNBase(Mnemonic mnemonic, string[] args, int maxNArgs, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                Contract.Requires(args != null);

                if (args.Length > maxNArgs)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Only {1} operand(s) are allowed, and received {2} operand(s).", this.ToString(), maxNArgs, args.Length);
                }
                if (this.NOperands >= 1)
                {
                    this.op1_ = new Operand(args[0], false);
                    if (this.op1_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1_.ErrorMessage);
                    }
                }
                if (this.NOperands >= 2)
                {
                    this.op2_ = new Operand(args[1], false);
                    if (this.op2_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2_.ErrorMessage);
                    }
                }
                if (this.NOperands >= 3)
                {
                    this.op3_ = new Operand(args[2], false);
                    if (this.op3_.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 3 is malformed: {1}", this.ToString(), this.op3_.ErrorMessage);
                    }
                }
            }

            public BitVecExpr Op1Value { get { return OpValue(this.op1_, this.keys_.prevKey, this.ctx_); } }

            public BitVecExpr Op2Value { get { return OpValue(this.op2_, this.keys_.prevKey, this.ctx_); } }

            public BitVecExpr Op3Value { get { return OpValue(this.op3_, this.keys_.prevKey, this.ctx_); } }

            public override bool MemReadStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_, this.op3_); } }

            public override bool MemWriteStatic { get { return this.ToMemReadWrite(this.op1_, this.op2_, this.op3_); } }
        }

        public abstract class Opcode2Type1 : Opcode2Base
        {
            public Opcode2Type1(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.IsImm)
                {
                    if (this.op1_.NBits < this.op2_.NBits)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                    if ((this.op1_.NBits == 64) && (this.op2_.NBits == 32))
                    {
                        this.op2_.SignExtend(64);
                    }
                    else if (this.op2_.NBits < this.op1_.NBits)
                    {
                        this.op2_.ZeroExtend(this.op1_.NBits);
                    }
                }
                else if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }
        }
        #endregion Abstract OpcodeBases

        public sealed class NotImplemented : OpcodeBase
        {
            public NotImplemented(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NOP, args, keys, t)
            {
                this.SyntaxError = string.Format(Culture, "\"{0}\": Mnemonic {1} is not implemented", this.ToString(), mnemonic.ToString());
            }

            public override void Execute()
            {
                // do not create updates
            }
        }

        public sealed class Ignore : OpcodeBase
        {
            public Ignore(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NOP, args, keys, t) { }

            public override void Execute()
            {
                this.Create_RegularUpdate(); // do nothing, only create an empty update
            }
        }

        /// <summary>
        /// Dummy SIMD instruction implementation. Threats all operands as destructive, but leaves all untouched registers as unchanged.
        /// </summary>
        public sealed class DummySIMD : OpcodeNBase
        {
            public DummySIMD(Mnemonic mnemnonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NOP, args, 3, keys, t) { }

            public override void Execute()
            {
                this.Create_RegularUpdate();
                bool memAlreadyCleared = false;
                if (this.NOperands > 0)
                {
                    if (this.op1_.IsReg)
                    {
                        this.RegularUpdate.Set(this.op1_.Rn, Tv.UNKNOWN);
                    }
                    else if (this.op1_.IsMem && (!memAlreadyCleared))
                    {
                        this.RegularUpdate.Set_Mem_Unknown();
                        memAlreadyCleared = true;
                    }
                }
                if (this.NOperands > 1)
                {
                    if (this.op2_.IsReg)
                    {
                        this.RegularUpdate.Set(this.op2_.Rn, Tv.UNKNOWN);
                    }
                    else if (this.op2_.IsMem && (!memAlreadyCleared))
                    {
                        this.RegularUpdate.Set_Mem_Unknown();
                        memAlreadyCleared = true;
                    }
                }
                if (this.NOperands > 2)
                {
                    if (this.op3_.IsReg)
                    {
                        this.RegularUpdate.Set(this.op3_.Rn, Tv.UNKNOWN);
                    }
                    else if (this.op3_.IsMem && (!memAlreadyCleared))
                    {
                        this.RegularUpdate.Set_Mem_Unknown();
                        memAlreadyCleared = true;
                    }
                }
            }
        }

        #region Data Transfer Instructions
        public sealed class Mov : Opcode2Type1
        {
            public Mov(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MOV, args, keys, t) { }

            public override void Execute()
            {
                if (this.op1_.Type == Ot1.UNKNOWN)
                {
                    //TODO The moffs8, moffs16, moffs32 and moffs64 operands specify a simple offset relative to the segment base, where 8, 16, 32 and 64 refer to the size of the data.The address-size attribute of the instruction determines the size of the offset, either 16, 32 or 64 bits.
                    this.SyntaxError = string.Format(Culture, "\"{0}\": execute: Unknown memory address in op1; Operand1={1} ({2}); Operand2={3} ({4})", this.ToString(), this.op1_, this.op1_.Type, this.op2_, this.op2_.Type);
                }
                else
                {
                    this.RegularUpdate.Set(this.op1_, this.Op2Value);
                }
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Cmovcc : Opcode2Base
        {
            private readonly ConditionalElement ce_;

            public Cmovcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                this.ce_ = ce;
            }

            public override void Execute()
            {
                BoolExpr conditional = ToolsAsmSim.ConditionalTaken(this.ce_, this.keys_.prevKey, this.ctx_);
                BitVecExpr op1 = this.Op1Value;
                BitVecExpr op2 = this.Op2Value;
                //Console.WriteLine("Cmovcc ce="+this._ce+"; conditional=" + conditional);
                BitVecExpr value = this.ctx_.MkITE(conditional, op2, op1) as BitVecExpr;
                BitVecExpr undef = this.ctx_.MkBVXOR(op1, op2);
                this.RegularUpdate.Set(this.op1_, value, undef);
            }

            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this.ce_); } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Exchange Register/Memory with Register</summary>
        public sealed class Xchg : Opcode2Base
        {
            public Xchg(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.XCHG, args, Ot2.reg_reg | Ot2.reg_mem | Ot2.mem_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                this.RegularUpdate.Set(this.op1_, this.Op2Value);
                this.RegularUpdate.Set(this.op2_, this.Op1Value);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_, this.op2_); } }
        }

        /// <summary>Byte swap</summary>
        public sealed class Bswap : Opcode1Base
        {
            public Bswap(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BSWAP, args, Ot1.reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!((this.op1_.NBits == 32) || (this.op1_.NBits == 64)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be 32-bits or 64-bits. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                BitVecExpr s = this.Op1Value;
                BitVecExpr dest;
                if (this.op1_.NBits == 32)
                {
                    BitVecExpr b0 = ctx.MkExtract((1 * 8) - 1, 0 * 8, s);
                    BitVecExpr b1 = ctx.MkExtract((2 * 8) - 1, 1 * 8, s);
                    BitVecExpr b2 = ctx.MkExtract((3 * 8) - 1, 2 * 8, s);
                    BitVecExpr b3 = ctx.MkExtract((4 * 8) - 1, 3 * 8, s);
                    dest = ctx.MkConcat(ctx.MkConcat(b0, b1), ctx.MkConcat(b2, b3));
                }
                else
                {
                    BitVecExpr b0 = ctx.MkExtract((1 * 8) - 1, 0 * 8, s);
                    BitVecExpr b1 = ctx.MkExtract((2 * 8) - 1, 1 * 8, s);
                    BitVecExpr b2 = ctx.MkExtract((3 * 8) - 1, 2 * 8, s);
                    BitVecExpr b3 = ctx.MkExtract((4 * 8) - 1, 3 * 8, s);
                    BitVecExpr b4 = ctx.MkExtract((5 * 8) - 1, 4 * 8, s);
                    BitVecExpr b5 = ctx.MkExtract((6 * 8) - 1, 5 * 8, s);
                    BitVecExpr b6 = ctx.MkExtract((7 * 8) - 1, 6 * 8, s);
                    BitVecExpr b7 = ctx.MkExtract((8 * 8) - 1, 7 * 8, s);
                    dest = ctx.MkConcat(ctx.MkConcat(ctx.MkConcat(b0, b1), ctx.MkConcat(b2, b3)), ctx.MkConcat(ctx.MkConcat(b4, b5), ctx.MkConcat(b6, b7)));
                }
                this.RegularUpdate.Set(this.op1_, dest);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Exchange and add</summary>
        public sealed class Xadd : Opcode2Base
        {
            public Xadd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.XADD, args, Ot2.mem_reg | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                BitVecExpr a = this.Op1Value;
                BitVecExpr b = this.Op2Value;

                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Addition(a, b, this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(this.op2_, a); // swap op1 and op2
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_, this.op2_); } }
        }

        /// <summary>Compare and exchange</summary>
        public sealed class Cmpxchg : Opcode2Base
        {
            public Cmpxchg(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CMPXCHG, args, Ot2.mem_reg | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                /* Compare AL with r/m8 (op1). If equal, ZF is set and r8 (op2) is loaded into r/m8 (op1). Else, clear ZF and load r/m8 (op1) into AL.
                 *
                 * if (AL==op1) {
                 *      ZF  = 1
                 *      op1 = op2
                 *      op2 = op2
                 *      AL  = AL
                 * } else {
                 *      ZF  = 0
                 *      op1 = op1
                 *      op2 = op2
                 *      AL  = op1
                 * }
                 */

                Rn regA = Rn.NOREG;
                switch (this.op1_.NBits)
                {
                    case 8: regA = Rn.AL; break;
                    case 16: regA = Rn.AX; break;
                    case 32: regA = Rn.EAX; break;
                    case 64: regA = Rn.RAX; break;
                }
                BitVecExpr regA_Expr_Curr = this.Get(regA);
                BoolExpr zf = this.ctx_.MkEq(regA_Expr_Curr, this.Op1Value);
                BitVecExpr op1 = this.Op1Value;
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkITE(zf, this.Op2Value, op1) as BitVecExpr);
                this.RegularUpdate.Set(regA, this.ctx_.MkITE(zf, regA_Expr_Curr, op1) as BitVecExpr);

                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.Op1Value, this.Op2Value, this.ctx_);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set(Flags.SF, ToolsFlags.Create_SF(result, result.SortSize, this.ctx_));
                this.RegularUpdate.Set(Flags.ZF, zf);
                this.RegularUpdate.Set(Flags.PF, ToolsFlags.Create_PF(result, this.ctx_));
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.op1_ != null)
                    {
                        switch (this.op1_.NBits)
                        {
                            case 8: yield return Rn.AL; break;
                            case 16: yield return Rn.AX; break;
                            case 32: yield return Rn.EAX; break;
                            case 64: yield return Rn.RAX; break;
                            default: break;
                        }
                    }
                    foreach (Rn r in ReadRegs(this.op1_, true, this.op2_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1_ != null)
                    {
                        switch (this.op1_.NBits)
                        {
                            case 8: yield return Rn.AL; break;
                            case 16: yield return Rn.AX; break;
                            case 32: yield return Rn.EAX; break;
                            case 64: yield return Rn.RAX; break;
                            default: break;
                        }
                    }
                    foreach (Rn r in WriteRegs(this.op1_))
                    {
                        yield return r;
                    }
                }
            }
        }

        /// <summary>Compare and exchange 8 bytes</summary>
        public sealed class Cmpxchg8b : Opcode1Base
        {
            public Cmpxchg8b(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CMPXCHG8B, args, Ot1.mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != 64)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be a 64-bit memory operand. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public override void Execute()
            {
                //Compare EDX:EAX with m64. If equal, set ZF and load ECX:EBX into m64. Else, clear ZF and load m64 into EDX:EAX.

                BitVecExpr op1 = this.Op1Value;
                BoolExpr zf = this.ctx_.MkEq(this.ctx_.MkConcat(this.Get(Rn.EDX), this.Get(Rn.EAX)), op1);

                this.RegularUpdate.Set(this.op1_, this.ctx_.MkConcat(this.Get(Rn.ECX), this.Get(Rn.EBX)));
                this.RegularUpdate.Set(Rn.ECX, this.ctx_.MkITE(zf, this.ctx_.MkExtract(64 - 1, 32, op1), this.Get(Rn.ECX)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.EBX, this.ctx_.MkITE(zf, this.ctx_.MkExtract(32 - 1, 0, op1), this.Get(Rn.EBX)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.EDX, this.ctx_.MkITE(zf, this.Get(Rn.EDX), this.ctx_.MkExtract(64 - 1, 32, op1)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.EAX, this.ctx_.MkITE(zf, this.Get(Rn.EAX), this.ctx_.MkExtract(32 - 1, 0, op1)) as BitVecExpr);
                this.RegularUpdate.Set(Flags.ZF, zf);
            }

            public override Flags FlagsWriteStatic { get { return Flags.ZF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return Rn.EAX;
                    yield return Rn.EBX;
                    yield return Rn.ECX;
                    yield return Rn.EDX;
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return Rn.EAX;
                    yield return Rn.EBX;
                    yield return Rn.ECX;
                    yield return Rn.EDX;
                }
            }
        }

        /// <summary>Compare and exchange 8 bytes</summary>
        public sealed class Cmpxchg16b : Opcode1Base
        {
            public Cmpxchg16b(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CMPXCHG16B, args, Ot1.mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != 128)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be a 128-bit memory operand. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public override void Execute()
            {
                //Compare RDX:RAX with m128. If equal, set ZF and load RCX:RBX into m128. Else, clear ZF and load m128 into RDX:RAX.

                BitVecExpr op1 = this.Op1Value;
                BoolExpr zf = this.ctx_.MkEq(this.ctx_.MkConcat(this.Get(Rn.RDX), this.Get(Rn.RAX)), op1);

                this.RegularUpdate.Set(this.op1_, this.ctx_.MkConcat(this.Get(Rn.RCX), this.Get(Rn.RBX)));
                this.RegularUpdate.Set(Rn.RCX, this.ctx_.MkITE(zf, this.ctx_.MkExtract(128 - 1, 64, op1), this.Get(Rn.RCX)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.RBX, this.ctx_.MkITE(zf, this.ctx_.MkExtract(64 - 1, 0, op1), this.Get(Rn.RBX)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.RDX, this.ctx_.MkITE(zf, this.Get(Rn.RDX), this.ctx_.MkExtract(128 - 1, 64, op1)) as BitVecExpr);
                this.RegularUpdate.Set(Rn.RAX, this.ctx_.MkITE(zf, this.Get(Rn.RAX), this.ctx_.MkExtract(64 - 1, 0, op1)) as BitVecExpr);
                this.RegularUpdate.Set(Flags.ZF, zf);
            }

            public override Flags FlagsWriteStatic { get { return Flags.ZF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return Rn.RAX;
                    yield return Rn.RBX;
                    yield return Rn.RCX;
                    yield return Rn.RDX;
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return Rn.RAX;
                    yield return Rn.RBX;
                    yield return Rn.RCX;
                    yield return Rn.RDX;
                }
            }
        }

        /// <summary>Push onto stack</summary>
        public sealed class Push : Opcode1Base
        {
            public Push(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.PUSH, args, Ot1.reg | Ot1.mem | Ot1.imm, keys, t)
            {
                Contract.Requires(t != null);

                if (this.IsHalted)
                {
                    return;
                }

                if ((this.op1_.NBits == 8) && (this.op1_.IsReg || this.op1_.IsMem))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
                else if ((this.op1_.NBits == 64) && this.op1_.IsImm)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }

                if (this.op1_.IsReg && t.Parameters.mode_64bit)
                {
                    Rn reg = this.op1_.Rn;
                    if ((reg == Rn.CS) || (reg == Rn.SS) || (reg == Rn.DS) || (reg == Rn.ES))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid register in 64-bit mode. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                    }
                }
            }

            public override void Execute()
            {
                if (this.tools_.Parameters.mode_64bit) // the stackAddrSize == 64
                {
                    BitVecExpr value = this.Op1Value;
                    if (this.op1_.IsImm)
                    {
                        if (value.SortSize < 64)
                        {
                            value = this.ctx_.MkSignExt(64 - value.SortSize, value);
                        }
                    }
                    else if (this.op1_.IsReg && RegisterTools.IsSegmentRegister(this.op1_.Rn))
                    {
                        value = this.ctx_.MkZeroExt(64 - value.SortSize, value);
                    }
                    BitVecExpr rspExpr = this.Get(Rn.RSP);
                    this.RegularUpdate.Set(Rn.RSP, this.ctx_.MkBVSub(rspExpr, this.ctx_.MkBV(8, 64)));
                    this.RegularUpdate.Set_Mem(rspExpr, value);
                }
                else if (this.tools_.Parameters.mode_32bit)
                {
                    BitVecExpr value = this.Op1Value;
                    if (this.op1_.IsImm)
                    {
                        if (value.SortSize < 32)
                        {
                            value = this.ctx_.MkSignExt(32 - value.SortSize, value);
                        }
                    }
                    else if (this.op1_.IsReg && RegisterTools.IsSegmentRegister(this.op1_.Rn))
                    {
                        value = this.ctx_.MkZeroExt(32 - value.SortSize, value);
                    }
                    BitVecExpr espExpr = this.Get(Rn.ESP);
                    this.RegularUpdate.Set(Rn.ESP, this.ctx_.MkBVSub(espExpr, this.ctx_.MkBV(4, 32)));
                    this.RegularUpdate.Set_Mem(espExpr, value);
                }
                else if (this.tools_.Parameters.mode_16bit)
                {
                    BitVecExpr value = this.Op1Value;
                    BitVecExpr spExpr = this.Get(Rn.SP);
                    this.RegularUpdate.Set(Rn.SP, this.ctx_.MkBVSub(spExpr, this.ctx_.MkBV(2, 16)));
                    this.RegularUpdate.Set_Mem(spExpr, value);
                }
                else
                {
                    return;
                }
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }
                    else if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }
                    else if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }

                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }
                    else if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }
                    else if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }
                }
            }

            public override bool MemWriteStatic { get { return true; } }
        }

        /// <summary>Pop off of stack</summary>
        public sealed class Pop : Opcode1Base
        {
            public Pop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.POP, args, Ot1.reg | Ot1.mem, keys, t)
            {
                Contract.Requires(t != null);

                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": 8-bit operand is not allowed. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }

                if (this.op1_.IsReg && t.Parameters.mode_64bit)
                {
                    Rn reg = this.op1_.Rn;
                    if ((reg == Rn.SS) || (reg == Rn.DS) || (reg == Rn.ES))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid register in 64-bit mode. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                    }
                }
            }

            public override void Execute()
            {
                int operand_Size = this.op1_.NBits;
                if (this.tools_.Parameters.mode_64bit) // stackAddrSize == 64
                {
                    BitVecExpr rspExpr = this.Get(Rn.RSP);
                    BitVecExpr newRspExpr;
                    if (operand_Size == 64)
                    {
                        newRspExpr = this.ctx_.MkBVAdd(rspExpr, this.ctx_.MkBV(8, 64));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newRspExpr, 8));
                    }
                    else if (operand_Size == 16)
                    {
                        newRspExpr = this.ctx_.MkBVAdd(rspExpr, this.ctx_.MkBV(2, 64));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newRspExpr, 2));
                    }
                    else
                    {
                        this.SyntaxError += "UNKNOWN";
                        return;
                    }
                    this.RegularUpdate.Set(Rn.RSP, newRspExpr);
                }
                else if (this.tools_.Parameters.mode_32bit) // stackAddrSize == 32
                {
                    BitVecExpr espExpr = this.Get(Rn.ESP);
                    BitVecExpr newEspExpr;
                    if (operand_Size == 32)
                    {
                        newEspExpr = this.ctx_.MkBVAdd(espExpr, this.ctx_.MkBV(4, 32));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newEspExpr, 4));
                    }
                    else if (operand_Size == 16)
                    {
                        newEspExpr = this.ctx_.MkBVAdd(espExpr, this.ctx_.MkBV(2, 32));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newEspExpr, 2));
                    }
                    else
                    {
                        this.SyntaxError += "UNKNOWN";
                        return;
                    }
                    this.RegularUpdate.Set(Rn.ESP, newEspExpr);
                }
                else if (this.tools_.Parameters.mode_16bit)
                {
                    BitVecExpr spExpr = this.Get(Rn.SP);
                    BitVecExpr newSpExpr;
                    if (operand_Size == 32)
                    {
                        newSpExpr = this.ctx_.MkBVAdd(spExpr, this.ctx_.MkBV(4, 16));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newSpExpr, 4));
                    }
                    else if (operand_Size == 16)
                    {
                        newSpExpr = this.ctx_.MkBVAdd(spExpr, this.ctx_.MkBV(2, 16));
                        this.RegularUpdate.Set(this.op1_, this.GetMem(newSpExpr, 2));
                    }
                    else
                    {
                        this.SyntaxError += "UNKNOWN";
                        return;
                    }
                    this.RegularUpdate.Set(Rn.SP, newSpExpr);
                }
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }
                    else if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }
                    else if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }
                    else if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }
                    else if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }

                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override bool MemWriteStatic { get { return true; } }
        }
        /*
        /// <summary>Push general-purpose registers onto stack</summary>
        PUSHA,
        /// <summary>Push general-purpose registers onto stack</summary>
        PUSHAD,
        /// <summary> Pop general-purpose registers from stack</summary>
        POPA,
        /// <summary> Pop general-purpose registers from stack</summary>
        POPAD,
        */

        /// <summary>Convert word to doubleword</summary>
        public sealed class Cwd : Opcode0Base
        {
            public Cwd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CWD, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.DX, this.ctx_.MkExtract(32, 16, this.ctx_.MkSignExt(16, this.Get(Rn.AX))));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.DX; } }
        }

        /// <summary>Convert doubleword to quadword</summary>
        public sealed class Cdq : Opcode0Base
        {
            public Cdq(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CDQ, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.EDX, this.ctx_.MkExtract(64, 32, this.ctx_.MkSignExt(32, this.Get(Rn.EAX))));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.EAX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.EDX; } }
        }

        /// <summary>Convert quadword to octoword</summary>
        public sealed class Cqo : Opcode0Base
        {
            public Cqo(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CQO, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.RDX, this.ctx_.MkExtract(128, 64, this.ctx_.MkSignExt(64, this.Get(Rn.RAX))));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.RAX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.RDX; } }
        }

        /// <summary>Convert byte to word</summary>
        public sealed class Cbw : Opcode0Base
        {
            public Cbw(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CBW, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.AX, this.ctx_.MkSignExt(8, this.Get(Rn.AL)));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AL; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AH; } }
        }

        /// <summary>Convert word to doubleword in EAX register</summary>
        public sealed class Cwde : Opcode0Base
        {
            public Cwde(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CWDE, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.EAX, this.ctx_.MkSignExt(16, this.Get(Rn.AX)));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.EAX; } }
        }

        /// <summary>Move and sign extend</summary>
        public sealed class Cdqe : Opcode0Base
        {
            public Cdqe(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CDQE, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.RAX, this.ctx_.MkSignExt(32, this.Get(Rn.EAX)));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.EAX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.RAX; } }
        }

        /// <summary>Move and sign extend</summary>
        public sealed class Movsx : Opcode2Base
        {
            public Movsx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MOVSX, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                else if (this.op1_.NBits == 16)
                {
                    if (this.op2_.NBits != 8)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else if (this.op1_.NBits == 32)
                {
                    if ((this.op2_.NBits != 8) && (this.op2_.NBits != 16))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else if (this.op1_.NBits == 64)
                {
                    if ((this.op2_.NBits != 8) && (this.op2_.NBits != 16))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1_.NBits - this.op2_.NBits);
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkSignExt(nBitsAdded, this.Op2Value));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Move and sign extend</summary>
        public sealed class Movsxd : Opcode2Base
        {
            public Movsxd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MOVSXD, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != 64)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }

                if (this.op2_.NBits == 32)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1_.NBits - this.op2_.NBits);
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkSignExt(nBitsAdded, this.Op2Value));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Move and zero extend</summary>
        public sealed class Movzx : Opcode2Base
        {
            public Movzx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MOVZX, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                else if (this.op1_.NBits == 16)
                {
                    if (this.op2_.NBits != 8)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else if (this.op1_.NBits == 32)
                {
                    if ((this.op2_.NBits != 8) && (this.op2_.NBits != 16))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else if (this.op1_.NBits == 64)
                {
                    if ((this.op2_.NBits != 8) && (this.op2_.NBits != 16))
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1_.NBits - this.op2_.NBits);
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkZeroExt(nBitsAdded, this.Op2Value));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        #endregion Data Transfer Instructions

        #region Binary Arithmetic Instructions

        /// <summary>Unsigned integer add with carry, leaves overflow flag unchanged</summary>
        public sealed class Adcx : Opcode2Type1
        {
            public Adcx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ADCX, args, keys, t) { }

            public override void Execute()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>Unsigned integer add with overflow flag instead of carry flag</summary>
        public sealed class Adox : Opcode2Type1
        {
            public Adox(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ADOX, args, keys, t) { }

            public override void Execute()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>Integer add</summary>
        public sealed class Add : Opcode2Type1
        {
            public Add(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ADD, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Addition(this.Op1Value, this.Op2Value, this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Add with carry</summary>
        public sealed class Adc : Opcode2Type1
        {
            public Adc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ADC, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Addition(this.Op1Value, this.Op2Value, this.Get(Flags.CF), this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Subtracts</summary>
        /// the second operand (source operand) from the first operand (destination operand) and stores the result in the destination operand. The destination operand can be a register or a memory location; the source operand can be an immediate, register, or memory location. (However, two memory operands cannot be used in one instruction.) When an immediate value is used as an operand, it is sign-extended to the length of the destination operand format.
        ///
        /// The SUB instruction performs integer subtraction.It evaluates the result for both signed and unsigned integer operands and sets the OF and CF flags to indicate an overflow in the signed or unsigned result, respectively.The SF flag indicates the sign of the signed result.
        ///
        ///In 64-bit mode, the instruction’s default operation size is 32 bits.Using a REX prefix in the form of REX.R permits access to additional registers (R8-R15). Using a REX prefix in the form of REX.W promotes operation to 64 bits.See the summary chart at the beginning of this section for encoding data and limits.
        ///
        public sealed class Sub : Opcode2Type1
        {
            public Sub(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SUB, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.Op1Value, this.Op2Value, this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Subtract with borrow</summary>
        public sealed class Sbb : Opcode2Type1
        {
            public Sbb(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SBB, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.Op1Value, this.Op2Value, this.Get(Flags.CF), this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Signed multiply</summary>
        public sealed class Imul : OpcodeNBase
        {
            public Imul(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.IMUL, args, 3, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                switch (this.NOperands)
                {
                    case 1:
                        {
                            Ot1 allowedOperands1 = Ot1.reg | Ot1.mem;
                            if (!allowedOperands1.HasFlag(this.op1_.Type))
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand1={1} ({2}, bits={3}). Allowed types: {4}.",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, AsmSourceTools.ToString(allowedOperands1));
                            }
                            break;
                        }
                    case 2:
                        {
                            Ot2 allowedOperands2 = Ot2.reg_reg | Ot2.reg_mem;
                            if (!allowedOperands2.HasFlag(AsmSourceTools.MergeOt(this.op1_.Type, this.op2_.Type)))
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}). Allowed types: {7}.",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, AsmSourceTools.ToString(allowedOperands2));
                            }
                            if (this.op1_.NBits == 8)
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand 1 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}).",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                            }
                            if (this.op1_.NBits != this.op2_.NBits)
                            {
                                this.CreateSyntaxError1(this.op1_, this.op2_);
                            }

                            break;
                        }
                    case 3:
                        {
                            Ot3 allowedOperands3 = Ot3.reg_reg_imm | Ot3.reg_mem_imm;
                            if (!allowedOperands3.HasFlag(AsmSourceTools.MergeOt(this.op1_.Type, this.op2_.Type, this.op3_.Type)))
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}) Allowed types: {10}.",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits, AsmSourceTools.ToString(allowedOperands3));
                            }
                            if (this.op1_.NBits == 8)
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand 1 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}).",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits);
                            }
                            if (this.op1_.NBits != this.op2_.NBits)
                            {
                                this.CreateSyntaxError1(this.op1_, this.op2_);
                            }

                            if (this.op3_.NBits < this.op1_.NBits)
                            {
                                this.op3_.SignExtend(Math.Min(this.op1_.NBits, 32));
                            }
                            if (((this.op1_.NBits == 16) && (this.op3_.NBits == 8)) ||
                                ((this.op1_.NBits == 32) && (this.op3_.NBits == 8)) ||
                                ((this.op1_.NBits == 64) && (this.op3_.NBits == 8)) ||
                                ((this.op1_.NBits == 16) && (this.op3_.NBits == 16)) ||
                                ((this.op1_.NBits == 32) && (this.op3_.NBits == 32)) ||
                                ((this.op1_.NBits == 64) && (this.op3_.NBits == 32)))
                            {
                                // ok
                            }
                            else
                            {
                                this.SyntaxError = string.Format(
                                    "\"{0}\": Operand 1 and 3 cannot have the provided combination of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}).",
                                    this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits);
                            }
                            break;
                        }
                    default:
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Expected 1 or 2 or 3 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                        break;
                }
            }

            public override void Execute()
            {
                /*
                IMUL r/m8*                      AX← AL ∗ r/m byte.
                IMUL r/m16                      DX:AX ← AX ∗ r/m word.
                IMUL r/m32                      EDX:EAX ← EAX ∗ r/m32.
                IMUL r/m64                      RDX:RAX ← RAX ∗ r/m64.
                IMUL r16, r/m16                 word register ← word register ∗ r/m16.
                IMUL r32, r/m32                 doubleword register ← doubleword register ∗ r/m32.
                IMUL r64, r/m64                 Quadword register ← Quadword register ∗ r/m64.
                IMUL r16, r/m16, imm8           word register ← r/m16 ∗ sign-extended immediate byte.
                IMUL r32, r/m32, imm8           doubleword register ← r/m32 ∗ sign-extended immediate byte.
                IMUL r64, r/m64, imm8           Quadword register ← r/m64 ∗ sign-extended immediate byte.
                IMUL r16, r/m16, imm16          word register ← r/m16 ∗ immediate word.
                IMUL r32, r/m32, imm32          doubleword register ← r/m32 ∗ immediate doubleword.
                IMUL r64, r/m64, imm32          Quadword register ← r/m64 ∗ immediate doubleword.
                */
                Context ctx = this.ctx_;
                BoolExpr cf;

                uint nBits = (uint)this.op1_.NBits;

                switch (this.NOperands)
                {
                    case 1:
                        {
                            switch (nBits)
                            {
                                case 8:
                                    {
                                        BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Get(Rn.AL)), ctx.MkSignExt(nBits, this.Op1Value));
                                        this.RegularUpdate.Set(Rn.AX, newValue);
                                        cf = ctx.MkNot(ctx.MkEq(ctx.MkSignExt(8, ctx.MkExtract(nBits - 1, 0, newValue)), newValue));
                                        break;
                                    }
                                case 16:
                                    {
                                        BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Get(Rn.AX)), ctx.MkSignExt(nBits, this.Op1Value));
                                        BitVecExpr axValue = ctx.MkExtract(nBits - 1, 0, newValue);
                                        BitVecExpr dxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                                        this.RegularUpdate.Set(Rn.AX, axValue);
                                        this.RegularUpdate.Set(Rn.DX, dxValue);
                                        cf = ctx.MkNot(ctx.MkEq(ctx.MkSignExt(16, axValue), newValue));
                                        break;
                                    }
                                case 32:
                                    {
                                        BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Get(Rn.EAX)), ctx.MkSignExt(nBits, this.Op1Value));
                                        BitVecExpr eaxValue = ctx.MkExtract(nBits - 1, 0, newValue);
                                        BitVecExpr edxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                                        this.RegularUpdate.Set(Rn.EAX, eaxValue);
                                        this.RegularUpdate.Set(Rn.EDX, edxValue);
                                        cf = ctx.MkNot(ctx.MkEq(ctx.MkSignExt(nBits, eaxValue), newValue));
                                        break;
                                    }
                                case 64:
                                    {
                                        BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Get(Rn.RAX)), ctx.MkSignExt(nBits, this.Op1Value));
                                        BitVecExpr raxValue = ctx.MkExtract(nBits - 1, 0, newValue);
                                        BitVecExpr rdxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                                        this.RegularUpdate.Set(Rn.RAX, raxValue);
                                        this.RegularUpdate.Set(Rn.RDX, rdxValue);
                                        cf = ctx.MkNot(ctx.MkEq(ctx.MkSignExt(nBits, raxValue), newValue));
                                        break;
                                    }
                                default: return;
                            }
                            break;
                        }
                    case 2:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Op1Value), ctx.MkSignExt(nBits, this.Op2Value));
                            BitVecExpr truncatedValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr signExtendedTruncatedValue = ctx.MkSignExt(nBits, truncatedValue);
                            this.RegularUpdate.Set(this.op1_.Rn, truncatedValue);
                            cf = ctx.MkNot(ctx.MkEq(signExtendedTruncatedValue, newValue));
                            break;
                        }
                    case 3:
                        {
                            this.op3_.SignExtend((int)nBits); // sign extend the imm
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Op2Value), ctx.MkSignExt(nBits, this.Op3Value));
                            BitVecExpr truncatedValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr signExtendedTruncatedValue = ctx.MkSignExt(nBits, truncatedValue);
                            this.RegularUpdate.Set(this.op1_.Rn, truncatedValue);
                            cf = ctx.MkNot(ctx.MkEq(signExtendedTruncatedValue, newValue));
                            break;
                        }
                    default: return;
                }

                BoolExpr of = cf;

                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.NOperands == 1)
                    {
                        if (this.op1_ == null)
                        {
                            yield break;
                        }

                        switch (this.op1_.NBits)
                        {
                            case 8:
                                yield return Rn.AL;
                                break;
                            case 16:
                                yield return Rn.AX;
                                break;
                            case 32:
                                yield return Rn.EAX;
                                break;
                            case 64:
                                yield return Rn.RAX;
                                break;
                            default: break;
                        }
                        foreach (Rn r in ReadRegs(this.op1_, false))
                        {
                            yield return r;
                        }
                    }
                    else
                    {
                        foreach (Rn r in ReadRegs(this.op1_, true, this.op2_, false))
                        {
                            yield return r;
                        }
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    if (this.NOperands == 1)
                    {
                        switch (this.op1_.NBits)
                        {
                            case 8:
                                yield return Rn.AX;
                                break;
                            case 16:
                                yield return Rn.AX;
                                yield return Rn.DX;
                                break;
                            case 32:
                                yield return Rn.EAX;
                                yield return Rn.EDX;
                                break;
                            case 64:
                                yield return Rn.RAX;
                                yield return Rn.RDX;
                                break;
                            default: break;
                        }
                    }
                    else
                    {
                        foreach (Rn r in WriteRegs(this.op1_))
                        {
                            yield return r;
                        }
                    }
                }
            }
        }

        /// <summary>Unsigned multiply</summary>
        public sealed class Mul : Opcode1Base
        {
            public Mul(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MUL, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                BoolExpr cf;
                uint nBits = (uint)this.op1_.NBits;

                switch (nBits)
                {
                    case 8:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkZeroExt(nBits, this.Get(Rn.AL)), ctx.MkZeroExt(nBits, this.Op1Value));
                            this.RegularUpdate.Set(Rn.AX, newValue);
                            cf = ctx.MkNot(ctx.MkEq(ctx.MkZeroExt(nBits, ctx.MkExtract(nBits - 1, 0, newValue)), newValue));
                            break;
                        }
                    case 16:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkZeroExt(nBits, this.Get(Rn.AX)), ctx.MkZeroExt(nBits, this.Op1Value));
                            BitVecExpr axValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr dxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                            this.RegularUpdate.Set(Rn.AX, axValue);
                            this.RegularUpdate.Set(Rn.DX, dxValue);
                            cf = ctx.MkNot(ctx.MkEq(ctx.MkZeroExt(nBits, axValue), newValue));
                            break;
                        }
                    case 32:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkZeroExt(nBits, this.Get(Rn.EAX)), ctx.MkZeroExt(nBits, this.Op1Value));
                            BitVecExpr eaxValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr edxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                            this.RegularUpdate.Set(Rn.EAX, eaxValue);
                            this.RegularUpdate.Set(Rn.EDX, edxValue);
                            cf = ctx.MkNot(ctx.MkEq(ctx.MkZeroExt(nBits, eaxValue), newValue));
                            break;
                        }
                    case 64:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkZeroExt(nBits, this.Get(Rn.RAX)), ctx.MkZeroExt(nBits, this.Op1Value));
                            BitVecExpr raxValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr rdxValue = ctx.MkExtract((nBits * 2) - 1, nBits, newValue);
                            this.RegularUpdate.Set(Rn.RAX, raxValue);
                            this.RegularUpdate.Set(Rn.RDX, rdxValue);
                            cf = ctx.MkNot(ctx.MkEq(ctx.MkZeroExt(nBits, raxValue), newValue));
                            break;
                        }
                    default: return;
                }

                BoolExpr of = cf;

                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AL;
                            break;
                        case 16:
                            yield return Rn.AX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            break;
                        default: break;
                    }
                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AX;
                            break;
                        case 16:
                            yield return Rn.AX;
                            yield return Rn.DX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            yield return Rn.EDX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            yield return Rn.RDX;
                            break;
                        default: break;
                    }
                }
            }
        }

        /// <summary>Signed divide</summary>
        public sealed class Idiv : Opcode1Base
        {
            public Idiv(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.DIV, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                BitVecExpr term1;
                BitVecExpr maxValue;

                switch (this.op1_.NBits)
                {
                    case 8:
                        term1 = this.Get(Rn.AX);
                        maxValue = ctx.MkBV(0xFF, nBits * 2);
                        break;
                    case 16:
                        term1 = ctx.MkConcat(this.Get(Rn.DX), this.Get(Rn.AX));
                        maxValue = ctx.MkBV(0xFFFF, nBits * 2);
                        break;
                    case 32:
                        term1 = ctx.MkConcat(this.Get(Rn.EDX), this.Get(Rn.EAX));
                        maxValue = ctx.MkBV(0xFFFF_FFFF, nBits * 2);
                        break;
                    case 64:
                        term1 = ctx.MkConcat(this.Get(Rn.RDX), this.Get(Rn.RAX));
                        maxValue = ctx.MkBV(0xFFFF_FFFF_FFFF_FFFF, nBits * 2);
                        break;
                    default: return;
                }

                BitVecExpr op1Value = this.Op1Value;

                BitVecExpr term2 = ctx.MkSignExt(nBits, op1Value);
                BitVecExpr quotient = ctx.MkBVSDiv(term1, term2);
                BitVecExpr remainder = ctx.MkBVSRem(term1, term2);

                //Console.WriteLine("op1Value=" + op1Value + "; term1=" + term1 + "; term2=" + term2 + "; quotient=" + quotient + "; remainder=" + remainder);

                BoolExpr op1IsZero = ctx.MkEq(op1Value, ctx.MkBV(0, nBits));
                BoolExpr quotientTooLarge = ctx.MkBVSGT(quotient, maxValue);
                BoolExpr dE_Excepton = ctx.MkOr(op1IsZero, quotientTooLarge);

                switch (this.op1_.NBits)
                {
                    case 8:
                        this.RegularUpdate.Set(Rn.AL, ctx.MkITE(dE_Excepton, this.Undef(Rn.AL), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.AH, ctx.MkITE(dE_Excepton, this.Undef(Rn.AH), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 16:
                        this.RegularUpdate.Set(Rn.AX, ctx.MkITE(dE_Excepton, this.Undef(Rn.AX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.DX, ctx.MkITE(dE_Excepton, this.Undef(Rn.DX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 32:
                        this.RegularUpdate.Set(Rn.EAX, ctx.MkITE(dE_Excepton, this.Undef(Rn.EAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.EDX, ctx.MkITE(dE_Excepton, this.Undef(Rn.EDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 64:
                        this.RegularUpdate.Set(Rn.RAX, ctx.MkITE(dE_Excepton, this.Undef(Rn.RAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.RDX, ctx.MkITE(dE_Excepton, this.Undef(Rn.RDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    default: return;
                }

                this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AL;
                            break;
                        case 16:
                            yield return Rn.AX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            break;
                        default: break;
                    }
                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AX;
                            break;
                        case 16:
                            yield return Rn.AX;
                            yield return Rn.DX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            yield return Rn.EDX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            yield return Rn.RDX;
                            break;
                        default: break;
                    }
                }
            }
        }

        /// <summary>Unsigned divide</summary>
        public sealed class Div : Opcode1Base
        {
            public Div(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.DIV, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                BitVecExpr term1;
                BitVecExpr maxValue;
                switch (nBits)
                {
                    case 8:
                        term1 = this.Get(Rn.AX);
                        maxValue = ctx.MkBV(0xFF, nBits * 2);
                        break;
                    case 16:
                        term1 = ctx.MkConcat(this.Get(Rn.DX), this.Get(Rn.AX));
                        maxValue = ctx.MkBV(0xFFFF, nBits * 2);
                        break;
                    case 32:
                        term1 = ctx.MkConcat(this.Get(Rn.EDX), this.Get(Rn.EAX));
                        maxValue = ctx.MkBV(0xFFFF_FFFF, nBits * 2);
                        break;
                    case 64:
                        term1 = ctx.MkConcat(this.Get(Rn.RDX), this.Get(Rn.RAX));
                        maxValue = ctx.MkBV(0xFFFF_FFFF_FFFF_FFFF, nBits * 2);
                        break;
                    default: return;
                }

                BitVecExpr op1Value = this.Op1Value;

                BitVecExpr term2 = ctx.MkZeroExt(nBits, op1Value);
                BitVecExpr quotient = ctx.MkBVUDiv(term1, term2);
                BitVecExpr remainder = ctx.MkBVURem(term1, term2);

                //Console.WriteLine("op1Value=" + op1Value + "; term1=" + term1 + "; term2=" + term2 + "; quotient=" + quotient + "; remainder=" + remainder);

                BoolExpr op1IsZero = ctx.MkEq(op1Value, ctx.MkBV(0, nBits));
                BoolExpr quotientTooLarge = ctx.MkBVUGT(quotient, maxValue);
                BoolExpr dE_Excepton = ctx.MkOr(op1IsZero, quotientTooLarge);

                switch (nBits)
                {
                    case 8:
                        BitVecExpr al = ctx.MkITE(dE_Excepton, this.Undef(Rn.AL), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr;
                        BitVecExpr ah = ctx.MkITE(dE_Excepton, this.Undef(Rn.AH), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr;
                        this.RegularUpdate.Set(Rn.AX, ctx.MkConcat(ah, al));
                        break;
                    case 16:
                        this.RegularUpdate.Set(Rn.AX, ctx.MkITE(dE_Excepton, this.Undef(Rn.AX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.DX, ctx.MkITE(dE_Excepton, this.Undef(Rn.DX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 32:
                        this.RegularUpdate.Set(Rn.EAX, ctx.MkITE(dE_Excepton, this.Undef(Rn.EAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.EDX, ctx.MkITE(dE_Excepton, this.Undef(Rn.EDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 64:
                        this.RegularUpdate.Set(Rn.RAX, ctx.MkITE(dE_Excepton, this.Undef(Rn.RAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.RDX, ctx.MkITE(dE_Excepton, this.Undef(Rn.RDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    default: return;
                }

                this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AL;
                            break;
                        case 16:
                            yield return Rn.AX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            break;
                        default: break;
                    }
                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1_ == null)
                    {
                        yield break;
                    }

                    switch (this.op1_.NBits)
                    {
                        case 8:
                            yield return Rn.AX;
                            break;
                        case 16:
                            yield return Rn.AX;
                            yield return Rn.DX;
                            break;
                        case 32:
                            yield return Rn.EAX;
                            yield return Rn.EDX;
                            break;
                        case 64:
                            yield return Rn.RAX;
                            yield return Rn.RDX;
                            break;
                        default: break;
                    }
                }
            }
        }

        /// <summary>Increment</summary>
        public sealed class Inc : Opcode1Base
        {
            public Inc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.INC, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Addition(this.Op1Value, this.ctx_.MkBV(1, (uint)this.op1_.NBits), this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                //CF is not updated!
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Decrement</summary>
        public sealed class Dec : Opcode1Base
        {
            public Dec(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.DEC, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.Op1Value, this.ctx_.MkBV(1, (uint)this.op1_.NBits), this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                //CF is not updated!
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Negate</summary>
        public sealed class Neg : Opcode1Base
        {
            public Neg(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NEG, args, Ot1.reg | Ot1.mem, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Neg(this.Op1Value, this.ctx_);
                this.RegularUpdate.Set(this.op1_, result);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Compare</summary>
        public sealed class Cmp : Opcode2Type1
        {
            public Cmp(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CMP, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.Op1Value, this.Op2Value, this.ctx_);
                this.RegularUpdate.Set(Flags.CF, cf);
                this.RegularUpdate.Set(Flags.OF, of);
                this.RegularUpdate.Set(Flags.AF, af);
                this.RegularUpdate.Set_SF_ZF_PF(result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, false, this.op2_, false); } }
        }
        #endregion Binary Arithmetic Instructions

        #region Decimal Arithmetic Instructions

        ///<summary> DAA - Decimal adjust after addition</summary>
        public sealed class Daa : Opcode0Base
        {
            public Daa(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.DAA, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;

                BitVecExpr al = this.Get(Rn.AL);
                BoolExpr af = this.Get(Flags.AF);
                BoolExpr cf = this.Get(Flags.CF);

                BoolExpr condition1 = ctx.MkOr(ctx.MkBVUGT(ctx.MkExtract(3, 0, al), ctx.MkBV(9, 4)), af);
                BoolExpr condition2 = ctx.MkOr(ctx.MkBVUGT(al, ctx.MkBV(0x99, 8)), cf);
                BoolExpr carry_x = ToolsFlags.Create_CF_Add(al, ctx.MkBV(6, 8), 8, ctx);

                BitVecExpr al_new = ctx.MkITE(condition1, ctx.MkBVAdd(al, ctx.MkBV(6, 8)), ctx.MkITE(condition2, ctx.MkBVAdd(al, ctx.MkBV(0x60, 8)), al)) as BitVecExpr;
                BoolExpr cf_new = ctx.MkITE(condition1, ctx.MkOr(cf, carry_x), condition2) as BoolExpr;
                BoolExpr af_new = condition1;

                this.RegularUpdate.Set(Rn.AL, al_new);
                this.RegularUpdate.Set(Flags.CF, cf_new);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, af_new);
                this.RegularUpdate.Set_SF_ZF_PF(al_new);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF | Flags.AF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AL; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AL; } }
        }

        ///<summary> DAS - Decimal adjust after subtraction</summary>
        public sealed class Das : Opcode0Base
        {
            public Das(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.DAS, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;

                BitVecExpr al = this.Get(Rn.AL);
                BoolExpr af = this.Get(Flags.AF);
                BoolExpr cf = this.Get(Flags.CF);

                BoolExpr condition1 = ctx.MkOr(ctx.MkBVUGT(ctx.MkExtract(3, 0, al), ctx.MkBV(9, 4)), af);
                BoolExpr condition2 = ctx.MkOr(ctx.MkBVUGT(al, ctx.MkBV(0x99, 8)), cf);
                BoolExpr carry_x = ToolsFlags.Create_CF_Sub(al, ctx.MkBV(6, 8), 8, ctx);

                BitVecExpr al_new = ctx.MkITE(condition1, ctx.MkBVSub(al, ctx.MkBV(6, 8)), ctx.MkITE(condition2, ctx.MkBVSub(al, ctx.MkBV(0x60, 8)), al)) as BitVecExpr;
                BoolExpr cf_new = ctx.MkITE(condition1, ctx.MkOr(cf, carry_x), condition2) as BoolExpr;
                BoolExpr af_new = condition1;

                this.RegularUpdate.Set(Rn.AL, al_new);
                this.RegularUpdate.Set(Flags.CF, cf_new);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, af_new);
                this.RegularUpdate.Set_SF_ZF_PF(al_new);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF | Flags.AF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AL; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AL; } }
        }

        /// <summary>AAA - ASCII adjust after addition</summary>
        public sealed class Aaa : Opcode0Base
        {
            public Aaa(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.AAA, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;

                BitVecExpr ax = this.Get(Rn.AX);
                BoolExpr af = this.Get(Flags.AF);
                BoolExpr cf = this.Get(Flags.CF);

                BoolExpr condition1 = ctx.MkOr(ctx.MkBVUGT(ctx.MkExtract(3, 0, ax), ctx.MkBV(9, 4)), af);

                BitVecExpr ax_new = ctx.MkBVAND(ctx.MkITE(condition1, ctx.MkBVAdd(ax, ctx.MkBV(0x106, 16)), ax) as BitVecExpr, ctx.MkBV(0xFF0F, 16));
                BoolExpr cf_new = condition1;
                BoolExpr af_new = condition1;

                this.RegularUpdate.Set(Rn.AX, ax_new);
                this.RegularUpdate.Set(Flags.CF, cf_new);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, af_new);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF | Flags.AF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AX; } }
        }

        /// <summary>AAS - ASCII adjust after subtraction</summary>
        public sealed class Aas : Opcode0Base
        {
            public Aas(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.AAS, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;

                BitVecExpr ax = this.Get(Rn.AX);
                BoolExpr af = this.Get(Flags.AF);
                BoolExpr cf = this.Get(Flags.CF);

                BoolExpr condition1 = ctx.MkOr(ctx.MkBVUGT(ctx.MkExtract(3, 0, ax), ctx.MkBV(9, 4)), af);

                //TODO bug next line: see documentation
                BitVecExpr ax_new = ctx.MkBVAND(ctx.MkITE(condition1, ctx.MkBVSub(ax, ctx.MkBV(0x106, 16)), ax) as BitVecExpr, ctx.MkBV(0xFF0F, 16));
                BoolExpr cf_new = condition1;
                BoolExpr af_new = condition1;

                this.RegularUpdate.Set(Rn.AX, ax_new);
                this.RegularUpdate.Set(Flags.CF, cf_new);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, af_new);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.ZF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF | Flags.AF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AX; } }
        }

        /// <summary>AAM - ASCII adjust after multiplication</summary>
        public sealed class Aam : OpcodeNBase
        {
            private readonly int imm;

            public Aam(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.AAM, args, 1, keys, t)
            {
                Contract.Requires(args != null);

                if (this.IsHalted)
                {
                    return;
                }

                if (args.Length == 0)
                {
                    this.imm = 10;
                }
                else if (this.op1_.IsImm && (this.op1_.NBits == 8))
                {
                    this.imm = (byte)this.op1_.Imm;
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be an 8-bits imm. Operand1={1} ({2}, bits={3}))", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                BitVecNum bv_imm = ctx.MkBV(this.imm, 8);
                BitVecExpr al = this.Get(Rn.AL);
                BitVecExpr al_new = ctx.MkBVSDiv(al, bv_imm);
                BitVecExpr ah_new = ctx.MkBVSMod(al, bv_imm);
                BitVecExpr ax_new = ctx.MkConcat(al_new, ah_new);

                this.RegularUpdate.Set(Rn.AX, ax_new);
                this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(al_new);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AL; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AX; } }
        }

        /// <summary>AAD - ASCII adjust after division</summary>
        public sealed class Aad : OpcodeNBase
        {
            private readonly int imm;

            public Aad(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.AAD, args, 1, keys, t)
            {
                Contract.Requires(args != null);

                if (this.IsHalted)
                {
                    return;
                }

                if (args.Length == 0)
                {
                    this.imm = 10;
                }
                else if (this.op1_.IsImm && (this.op1_.NBits == 8))
                {
                    this.imm = (byte)this.op1_.Imm;
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should be an 8-bits imm. Operand1={1} ({2}, bits={3}))", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                BitVecNum bv_imm_16 = ctx.MkBV(this.imm, 16);
                BitVecExpr al_16 = ctx.MkZeroExt(8, this.Get(Rn.AL));
                BitVecExpr ah_16 = ctx.MkZeroExt(8, this.Get(Rn.AH));

                BitVecExpr al_new = ctx.MkExtract(7, 0, ctx.MkBVAdd(al_16, ctx.MkBVMul(ah_16, bv_imm_16)));
                BitVecExpr ax_new = ctx.MkZeroExt(8, al_new);

                this.RegularUpdate.Set(Rn.AX, ax_new);
                this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(al_new);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }

            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AX; } }
        }
        #endregion Decimal Arithmetic Instructions

        #region Logical Instructions

        public abstract class LogicalBase : Opcode2Base
        {
            public LogicalBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.IsImm)
                {
                    if (this.op1_.NBits < this.op2_.NBits)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                    if ((this.op1_.NBits == 64) && (this.op2_.NBits == 32))
                    {
                        this.op2_.SignExtend(this.op1_.NBits);
                    }
                    else if (this.op2_.NBits == 8)
                    {
                        this.op2_.SignExtend(this.op1_.NBits);
                    }
                    else if (this.op2_.NBits < this.op1_.NBits)
                    {
                        this.op2_.ZeroExtend(this.op1_.NBits);
                    }
                }
                else if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                BitVecExpr value;
                switch (this.mnemonic_)
                {
                    case Mnemonic.XOR: value = this.ctx_.MkBVXOR(this.Op1Value, this.Op2Value); break;
                    case Mnemonic.AND: value = this.ctx_.MkBVAND(this.Op1Value, this.Op2Value); break;
                    case Mnemonic.OR: value = this.ctx_.MkBVOR(this.Op1Value, this.Op2Value); break;
                    default: return;
                }
                this.RegularUpdate.Set(this.op1_, value);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(value);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Xor : LogicalBase
        {
            public Xor(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.XOR, args, keys, t) { }
        }

        public sealed class And : LogicalBase
        {
            public And(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.AND, args, keys, t) { }
        }

        public sealed class Or : LogicalBase
        {
            public Or(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.OR, args, keys, t) { }
        }

        public sealed class Not : Opcode1Base
        {
            public Not(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NOT, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkBVNot(this.Op1Value));
                // Flags are unaffected
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Test : Opcode2Base
        {
            public Test(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.TEST, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.IsImm)
                {
                    if (this.op1_.NBits < this.op2_.NBits)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 should smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                    if (this.op2_.NBits < this.op1_.NBits)
                    {
                        this.op2_.SignExtend(this.op1_.NBits);
                    }
                }
                else if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                BitVecExpr value = this.ctx_.MkBVAND(this.Op1Value, this.Op2Value);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(value);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, false, this.op2_, false); } }
        }

        #endregion Logical Instructions

        #region Shift and Rotate Instructions

        public abstract class ShiftRotateBase : Opcode2Base
        {
            public ShiftRotateBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.IsReg && (this.op2_.Rn != Rn.CL))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": If operand 2 is a registers, only GPR cl is allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                if (this.Op2Value.SortSize != 8)
                {
                    this.Warning = string.Format(Culture, "\"{0}\": value of operand 2 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public static (BitVecExpr shiftCount, BoolExpr tooLarge) GetShiftCount(BitVecExpr value, int nBits, Context ctx)
            {
                Contract.Requires(ctx != null);
                Contract.Requires(value != null);
                Contract.Requires(value.SortSize == 8);

                BitVecNum shiftMask = ctx.MkBV((nBits == 64) ? 0x3F : 0x1F, 8);
                BoolExpr tooLarge = ctx.MkBVSGE(value, ctx.MkBV((nBits == 64) ? 64 : 32, 8));
                BitVecExpr shiftCount = ctx.MkBVAND(value, shiftMask);
                return (shiftCount: shiftCount, tooLarge: tooLarge);
            }

            public void UpdateFlagsShift(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, BoolExpr shiftTooLarge, bool left)
            {
                UpdateFlagsShift(value, cfIn, shiftCount, shiftTooLarge, left, this.keys_.prevKey, this.RegularUpdate, this.tools_.Rand, this.ctx_);
            }

            public static void UpdateFlagsShift(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, BoolExpr shiftTooLarge, bool left, string prevKey, StateUpdate stateUpdate, Random rand, Context ctx)
            {
                Contract.Requires(shiftCount != null);
                Contract.Requires(ctx != null);
                Contract.Requires(stateUpdate != null);
                Contract.Requires(value != null);

                uint nBits = shiftCount.SortSize;
                BoolExpr isZero = ctx.MkEq(shiftCount, ctx.MkBV(0, nBits));
                BoolExpr isOne = ctx.MkEq(shiftCount, ctx.MkBV(1, nBits));
                BitVecNum one = ctx.MkBV(1, 1);

                #region Calculate Overflow Flag
                BoolExpr of_tmp;
                if (left)
                {
                    BoolExpr b1 = ToolsZ3.GetBit(value, nBits - 1, one, ctx);
                    BoolExpr b2 = ToolsZ3.GetBit(value, nBits - 2, one, ctx);
                    of_tmp = ctx.MkXor(b1, b2);
                }
                else
                {
                    of_tmp = ToolsZ3.GetBit(value, nBits - 1, one, ctx);
                }

                BoolExpr of =
                    ctx.MkITE(shiftTooLarge, Undef(Flags.OF, rand, ctx),
                        ctx.MkITE(isZero, Get(Flags.OF, prevKey, ctx),
                            ctx.MkITE(isOne, of_tmp, Undef(Flags.OF, rand, ctx)))) as BoolExpr;
                #endregion

                #region Set the Flags
                stateUpdate.Set(Flags.OF, of);
                // if the shift is too larte than CF is undefined; if the shift is zero, than CF is unchanged; otherwise it is the provided value;
                stateUpdate.Set(Flags.CF, ctx.MkITE(shiftTooLarge, Undef(Flags.CF, rand, ctx), ctx.MkITE(isZero, Get(Flags.CF, prevKey, ctx), cfIn)) as BoolExpr);
                stateUpdate.Set(Flags.PF, ctx.MkITE(isZero, Get(Flags.PF, prevKey, ctx), ToolsFlags.Create_PF(value, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.ZF, ctx.MkITE(isZero, Get(Flags.ZF, prevKey, ctx), ToolsFlags.Create_ZF(value, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.SF, ctx.MkITE(isZero, Get(Flags.SF, prevKey, ctx), ToolsFlags.Create_SF(value, value.SortSize, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.AF, ctx.MkITE(isZero, Get(Flags.AF, prevKey, ctx), Undef(Flags.AF, rand, ctx)) as BoolExpr);
                #endregion
            }

            public void UpdateFlagsRotate(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, bool left)
            {
                Contract.Requires(shiftCount != null);

                /* The OF flag is defined only for the 1-bit rotates; it is undefined in all other
                 * cases (except RCL and RCR instructions only: a zero - bit rotate does nothing, that
                 * is affects no flags). For left rotates, the OF flag is set to the exclusive OR of
                 * the CF bit(after the rotate) and the most-significant bit of the result. For
                 * right rotates, the OF flag is set to the exclusive OR of the two most-significant
                 * bits of the result.
                 */

                Context ctx = this.ctx_;
                Contract.Assume(ctx != null);

                uint nBits = shiftCount.SortSize;
                BoolExpr isZero = ctx.MkEq(shiftCount, ctx.MkBV(0, nBits));
                BoolExpr isOne = ctx.MkEq(shiftCount, ctx.MkBV(1, nBits));

                #region Calculate Overflow Flag
                BoolExpr of_tmp;
                if (left)
                {
                    BoolExpr b1 = ToolsZ3.GetBit(value, nBits - 1, ctx.MkBV(1, 1), ctx);
                    BoolExpr b2 = ToolsZ3.GetBit(value, nBits - 2, ctx.MkBV(1, 1), ctx);
                    of_tmp = ctx.MkXor(b1, b2);
                }
                else
                {
                    of_tmp = ToolsZ3.GetBit(value, nBits - 1, ctx.MkBV(1, 1), ctx);
                }
                this.RegularUpdate.Set(Flags.OF, ctx.MkITE(isZero, this.Get(Flags.OF), ctx.MkITE(isOne, of_tmp, this.Undef(Flags.OF))) as BoolExpr);
                #endregion

                #region Calculate Carry Flag
                BoolExpr isLessOrEqualTo = ctx.MkBVULE(shiftCount, ctx.MkBV(nBits, nBits));
                this.RegularUpdate.Set(Flags.CF, ctx.MkITE(isZero, this.Get(Flags.CF), ctx.MkITE(isLessOrEqualTo, cfIn, this.Undef(Flags.CF))) as BoolExpr);
                #endregion
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        #region Shift

        ///<summary>Shift arithmetic right</summary>
        public sealed class Sar : ShiftRotateBase
        {
            public Sar(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SAR, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SAR, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsShift(result, cf, shiftCount.shiftCount, shiftCount.tooLarge, false);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }

        /// <summary>Shift arithmetic left</summary>
        public sealed class Sal : ShiftRotateBase
        {
            public Sal(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SAL, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SAL, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsShift(result, cf, shiftCount.shiftCount, shiftCount.tooLarge, true);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }

        ///<summary>Shift logical right</summary>
        public sealed class Shr : ShiftRotateBase
        {
            public Shr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHR, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SHR, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsShift(result, cf, shiftCount.shiftCount, shiftCount.tooLarge, false);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }

        /// <summary>Shift logical left</summary>
        public sealed class Shl : ShiftRotateBase
        {
            public Shl(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHL, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SHL, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsShift(result, cf, shiftCount.shiftCount, shiftCount.tooLarge, true);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }
        #endregion Shift

        #region Rotate

        /// <summary>Rotate right</summary>
        public sealed class Ror : ShiftRotateBase
        {
            public Ror(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ROR, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.ROR, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsRotate(result, cf, shiftCount.shiftCount, false);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }

        /// <summary>Rotate through carry right</summary>
        public sealed class Rcr : ShiftRotateBase
        {
            public Rcr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.RCR, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.RCR, this.Op1Value, shiftCount.shiftCount, this.Get(Flags.CF), this.keys_.prevKey, this.ctx_);
                this.UpdateFlagsRotate(result, cf, shiftCount.shiftCount, false);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }

        /// <summary>Rotate through carry left</summary>
        public sealed class Rcl : ShiftRotateBase
        {
            public Rcl(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.RCL, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.RCL, this.Op1Value, shiftCount.shiftCount, this.Get(Flags.CF), this.keys_.prevKey, this.ctx_);
                this.UpdateFlagsRotate(result, cf, shiftCount.shiftCount, true);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsReadStatic { get { return Flags.CF; } }

            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }

        /// <summary>Rotate left</summary>
        public sealed class Rol : ShiftRotateBase
        {
            public Rol(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ROL, args, keys, t) { }

            public override void Execute()
            {
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_);
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.ROL, this.Op1Value, shiftCount.shiftCount, this.ctx_, this.tools_.Rand);
                this.UpdateFlagsRotate(result, cf, shiftCount.shiftCount, true);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }
        #endregion Rotate

        #region Shift/Rotate X (no flags updates)
        public abstract class ShiftBaseX : Opcode3Base
        {
            public ShiftBaseX(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot3.reg_reg_imm | Ot3.reg_mem_imm, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }

                if (this.Op3Value.SortSize != 8)
                {
                    this.Warning = string.Format(Culture, "\"{0}\": value of operand 3 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Rorx : ShiftBaseX
        {
            public Rorx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.RORX, args, keys, t) { }

            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_).shiftCount;
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.ROR, this.Op1Value, shiftCount, this.ctx_, this.tools_.Rand);
                this.RegularUpdate.Set(this.op1_, result);
            }
        }

        public sealed class Sarx : ShiftBaseX
        {
            public Sarx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SARX, args, keys, t) { }

            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_).shiftCount;
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SAR, this.Op1Value, shiftCount, this.ctx_, this.tools_.Rand);
                this.RegularUpdate.Set(this.op1_, result);
            }
        }

        public sealed class Shlx : ShiftBaseX
        {
            public Shlx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHLX, args, keys, t) { }

            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_).shiftCount;
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SHL, this.Op1Value, shiftCount, this.ctx_, this.tools_.Rand);
                this.RegularUpdate.Set(this.op1_, result);
            }
        }

        public sealed class Shrx : ShiftBaseX
        {
            public Shrx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHRX, args, keys, t) { }

            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1_.NBits, this.ctx_).shiftCount;
                (BitVecExpr result, BoolExpr cf) = BitOperations.ShiftOperations(Mnemonic.SHR, this.Op1Value, shiftCount, this.ctx_, this.tools_.Rand);
                this.RegularUpdate.Set(this.op1_, result);
            }
        }
        #endregion  Shift/Rotate X (no flags updates)

        #region Shift Double

        public abstract class ShiftDoubleBase : Opcode3Base
        {
            public ShiftDoubleBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot3.reg_reg_imm | Ot3.reg_reg_reg | Ot3.mem_reg_imm | Ot3.mem_reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if ((this.op1_.NBits == 8) || (this.op2_.NBits == 8))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 and 2 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits);
                }
                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }

                if (this.op3_.IsReg && (this.op3_.Rn != Rn.CL))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": If operand 3 is a registers, only GPR cl is allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits);
                }
                if (this.Op3Value.SortSize != 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": value of operand 3 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits, this.op3_, this.op3_.Type, this.op3_.NBits);
                }
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false, this.op3_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Shift right double</summary>
        public sealed class Shrd : ShiftDoubleBase
        {
            public Shrd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHRD, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                (BitVecExpr shiftCount, BoolExpr tooLarge) shiftCount = ShiftRotateBase.GetShiftCount(this.Op3Value, (int)nBits, ctx);
                BitVecExpr nShifts8 = shiftCount.shiftCount; // nShifts8 is 8bits
                BitVecExpr nShifts = ctx.MkZeroExt(nBits - 8, nShifts8);
                BitVecExpr value_in = this.Op1Value;

                // calculate new value of register
                BitVecExpr value_a = ctx.MkBVLSHR(value_in, nShifts); // shift op1 nShift to the right while shifiting in 0
                BitVecExpr value_b = ctx.MkBVSHL(this.Op2Value, ctx.MkBVSub(ctx.MkBV(nBits, nBits), nShifts));
                BitVecExpr value_out = ctx.MkBVOR(value_a, value_b);

                // calculate value of CF
                BoolExpr cf = ToolsZ3.GetBit(value_in, ctx.MkBVSub(nShifts, ctx.MkBV(1, nBits)), ctx);

                //TODO: check COUNT > SIZE
                //THEN(*Bad parameters *)
                //DEST is undefined;
                //CF, OF, SF, ZF, AF, PF are undefined;

                ShiftRotateBase.UpdateFlagsShift(value_out, cf, shiftCount.shiftCount, shiftCount.tooLarge, false, this.keys_.prevKey, this.RegularUpdate, this.tools_.Rand, this.ctx_);
                this.RegularUpdate.Set(this.op1_, value_out);
            }
        }

        /// <summary>Shift left double</summary>
        public sealed class Shld : ShiftDoubleBase
        {
            public Shld(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SHLD, args, keys, t) { }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                (BitVecExpr shiftCount, BoolExpr tooLarge) = ShiftRotateBase.GetShiftCount(this.Op3Value, (int)nBits, ctx);
                BitVecExpr nShifts = shiftCount;
                BitVecExpr nShifts64 = ctx.MkZeroExt(nBits - 8, nShifts);
                BitVecExpr value_in = this.Op1Value;

                // calculate new value of register
                BitVecExpr bitPos = ctx.MkBVSub(nShifts, ctx.MkBV(1, 8));
                BitVecExpr value_a = ctx.MkBVLSHR(value_in, nShifts64);
                BitVecExpr value_b = ctx.MkBVSHL(this.Op2Value, ctx.MkBVSub(ctx.MkBV(nBits, nBits), nShifts64));
                BitVecExpr value_out = ctx.MkBVOR(value_a, value_b);

                // calculate value of CF
                BitVecExpr bitPos64 = ctx.MkZeroExt(nBits - 8, bitPos);
                BoolExpr bitValue = ToolsZ3.GetBit(value_in, bitPos64, ctx);
                BoolExpr cf = ctx.MkITE(ctx.MkEq(nShifts, ctx.MkBV(0, 8)), this.Undef(Flags.CF), bitValue) as BoolExpr;

                ShiftRotateBase.UpdateFlagsShift(value_out, cf, shiftCount, tooLarge, true, this.keys_.prevKey, this.RegularUpdate, this.tools_.Rand, this.ctx_);
                this.RegularUpdate.Set(this.op1_, value_out);
            }
        }
        #endregion Shift Double
        #endregion Shift and Rotate Instructions

        #region Bit and Byte Instructions

        public sealed class Setcc : Opcode1Base
        {
            private readonly ConditionalElement ce_;

            public Setcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot1.reg | Ot1.mem, keys, t)
            {
                this.ce_ = ce;
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != 8)
                {
                    this.SyntaxError = string.Format(Culture, "Invalid operands size. Operands can only have size 8. Operand1={0}", this.op1_);
                }
            }

            public override void Execute()
            {
                BoolExpr conditional = ToolsAsmSim.ConditionalTaken(this.ce_, this.keys_.prevKey, this.ctx_);
                BitVecExpr result = this.ctx_.MkITE(conditional, this.ctx_.MkBV(0, 8), this.ctx_.MkBV(1, 8)) as BitVecExpr;
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this.ce_); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public abstract class BitTestBase : Opcode2Base
        {
            public BitTestBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.IsImm)
                {
                    if (this.op2_.NBits != 8)
                    {
                        this.SyntaxError = string.Format(Culture, "Operand 2 is imm and should have 8 bits. Operand1={0} ({1}, bits={2}); Operand2={3} ({4}, bits={5})", this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            private BitVecExpr GetBitPos(BitVecExpr value, uint nBits)
            {
                BitVecNum mask = this.ctx_.MkBV((nBits == 64) ? 0x3F : 0x1F, nBits);
                return this.ctx_.MkBVAND(value, mask);
            }

            protected void SetBitValue(Mnemonic opcode)
            {
                //Debug.Assert(this.Op1Value.SortSize == this.Op2Value.SortSize, "nBits op1 = " + this.Op1Value.SortSize +"; nBits op2 = "+ this.Op2Value.SortSize);

                uint nBits = (uint)this.op1_.NBits;
                BitVecExpr bitPos = this.GetBitPos(this.Op2Value, nBits);
                BitVecExpr mask = this.ctx_.MkBVSHL(this.ctx_.MkBV(1, nBits), bitPos);
                BitVecExpr mask_INV = this.ctx_.MkBVNeg(mask);
                BitVecExpr v1 = this.Op1Value;

                switch (opcode)
                {
                    case Mnemonic.BTC:
                        {
                            BitVecExpr bitSet = this.ctx_.MkBVOR(v1, mask);
                            BitVecExpr bitCleared = this.ctx_.MkBVAND(v1, mask_INV);
                            BoolExpr bitSetAtPos = ToolsZ3.GetBit(v1, bitPos, this.ctx_);
                            BitVecExpr value = this.ctx_.MkITE(bitSetAtPos, bitCleared, bitSet) as BitVecExpr;
                            this.RegularUpdate.Set(this.op1_, value);
                        }
                        break;
                    case Mnemonic.BTS:
                        {
                            BitVecExpr bitSet = this.ctx_.MkBVOR(v1, mask);
                            this.RegularUpdate.Set(this.op1_, bitSet);
                        }
                        break;
                    case Mnemonic.BTR:
                        {
                            BitVecExpr bitCleared = this.ctx_.MkBVAND(v1, mask_INV);
                            this.RegularUpdate.Set(this.op1_, bitCleared);
                        }
                        break;
                    case Mnemonic.BT:
                        break;
                    default:
                        return;
                }
                // zero flag is unaffected
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.CF, ToolsZ3.GetBit(v1, bitPos, this.ctx_));
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }
        }

        public sealed class Bt_Opcode : BitTestBase
        {
            public Bt_Opcode(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BT, args, keys, t) { }

            public override void Execute() { this.SetBitValue(Mnemonic.BT); }
        }

        public sealed class Bts : BitTestBase
        {
            public Bts(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BTS, args, keys, t) { }

            public override void Execute() { this.SetBitValue(Mnemonic.BTS); }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Btr : BitTestBase
        {
            public Btr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BTR, args, keys, t) { }

            public override void Execute() { this.SetBitValue(Mnemonic.BTR); }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Btc : BitTestBase
        {
            public Btc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BTC, args, keys, t) { }

            public override void Execute() { this.SetBitValue(Mnemonic.BTC); }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Bsf : Opcode2Base
        {
            public Bsf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BSF, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operands cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            private BitVecExpr MakeBsfExpr(uint nBits, uint pos, BitVecExpr sourceOperand, BitVecNum one)
            {
                BitVecExpr result;
                if (pos == nBits - 1)
                {
                    result = this.ctx_.MkBV(pos, 6);
                }
                else
                {
                    BitVecExpr expr1 = this.MakeBsfExpr(nBits, pos + 1, sourceOperand, one);
                    result = this.ctx_.MkITE(ToolsZ3.GetBit(sourceOperand, pos, one, this.ctx_), this.ctx_.MkBV(pos, 6), expr1) as BitVecExpr;
                }
                return result;
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                {
                    BitVecNum one = ctx.MkBV(1, 1);
                    BitVecExpr answer = ctx.MkConcat(ctx.MkBV(0, nBits - 6), this.MakeBsfExpr(nBits, 0, this.Op2Value, one));
                    Debug.Assert(answer.SortSize == nBits);
                    BitVecExpr expr_Fresh = this.Undef(this.op1_.Rn);
                    BitVecExpr expr = ctx.MkITE(ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)), expr_Fresh, answer) as BitVecExpr;
                    this.RegularUpdate.Set(this.op1_, expr);
                }
                { // update flags
                    this.RegularUpdate.Set(Flags.ZF, ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)));
                    this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                }
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Bsr : Opcode2Base
        {
            public Bsr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.BSR, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operands cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            private BitVecExpr MakeBsrExpr(uint nBits, uint pos, BitVecExpr sourceOperand, BitVecNum one)
            {
                BitVecExpr result;
                if (pos == 1)
                {
                    result = this.ctx_.MkBV(0, 6);
                }
                else
                {
                    BitVecExpr expr1 = this.MakeBsrExpr(nBits, pos - 1, sourceOperand, one);
                    result = this.ctx_.MkITE(ToolsZ3.GetBit(sourceOperand, pos - 1, one, this.ctx_), this.ctx_.MkBV(pos - 1, 6), expr1) as BitVecExpr;
                }
                return result;
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                {
                    BitVecNum one = ctx.MkBV(1, 1);
                    BitVecExpr answer = ctx.MkConcat(ctx.MkBV(0, nBits - 6), this.MakeBsrExpr(nBits, nBits, this.Op2Value, one));
                    Debug.Assert(answer.SortSize == nBits);
                    BitVecExpr expr_Fresh = this.Undef(this.op1_.Rn);
                    BitVecExpr expr = ctx.MkITE(ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)), expr_Fresh, answer) as BitVecExpr;
                    this.RegularUpdate.Set(this.op1_, expr);
                }
                { // update flags
                    this.RegularUpdate.Set(Flags.ZF, ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)));
                    this.RegularUpdate.Set(Flags.CF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                }
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }
        #endregion

        #region Control Transfer Instructions
        public abstract class OpcodeJumpBase : Opcode1Base
        {
            public OpcodeJumpBase(Mnemonic mnemonic, string[] args, Ot1 allowedOperands1, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, allowedOperands1, keys, t) { }

            protected abstract BoolExpr Jump { get; }

            public int LineNumber
            {
                get
                {
                    int lineNumber = -1;
                    switch (this.op1_.Type)
                    {
                        case Ot1.reg:
                            this.SyntaxError = "WARNING: OpcodeJumpBase: jumping based on registry value is not supported.";
                            break;
                        case Ot1.mem:
                            // unsupported
                            this.SyntaxError = "WARNING: OpcodeJumpBase: jumping based on memory value is not supported.";
                            break;
                        case Ot1.imm: // assuming the imm is an line number
                            lineNumber = (int)this.op1_.Imm;
                            break;
                        case Ot1.UNKNOWN: // assuming it is a string with a line number in it.
                            lineNumber = ToolsZ3.GetLineNumberFromLabel(this.op1_.ToString(), StaticFlow.LINENUMBER_SEPARATOR);
                            break;
                        default:
                            this.SyntaxError = "WARNING: OpcodeJumpBase: UNKNOWN.";
                            break;
                    }
                    if (lineNumber < 0)
                    {
                        this.Warning = "line number is -1";
                    }
                    //Console.WriteLine("INFO: OpcodeJumpBase: lineNumber return " + lineNumber + ".");
                    return lineNumber;
                }
            }

            public override void Execute()
            {
                BoolExpr jumpConditional = this.Jump;
                if (jumpConditional.IsTrue)
                {
                    //this.RegularUpdate is not updated
                    this.Create_BranchUpdate();
                    //this.BranchUpdate.Add(new BranchInfo(jumpConditional, true));
                }
                else if (jumpConditional.IsFalse)
                {
                    //this.RegularUpdate.Add(new BranchInfo(jumpConditional, false));
                    //this.BranchUpdate is not updated
                    this.Create_RegularUpdate();
                }
                else
                {
                    this.RegularUpdate.BranchInfo = new BranchInfo(jumpConditional, false);
                    this.BranchUpdate.BranchInfo = new BranchInfo(jumpConditional, true);
                }
            }
        }

        public sealed class Jmp : OpcodeJumpBase
        {
            public Jmp(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.JMP, args, Ot1.imm | Ot1.mem | Ot1.reg | Ot1.UNKNOWN, keys, t) { }

            protected sealed override BoolExpr Jump
            {
                get { return this.ctx_.MkTrue(); }
            }
        }

        public sealed class Jmpcc : OpcodeJumpBase
        {
            private readonly ConditionalElement ce_;

            public Jmpcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot1.imm | Ot1.UNKNOWN, keys, t)
            {
                this.ce_ = ce;
            }

            protected sealed override BoolExpr Jump { get { return ToolsAsmSim.ConditionalTaken(this.ce_, this.keys_.prevKey, this.ctx_); } }

            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this.ce_); } }
        }

        #region Loop
        public abstract class OpcodeLoopBase : OpcodeJumpBase
        {
            public OpcodeLoopBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, Ot1.UNKNOWN, keys, t) { }

            public override void Execute()
            {
                throw new NotImplementedException();
                /*
                var tup = BitOperationsZ3.Substract(this.Get(Rn.ECX), this.Ctx.MkBV(1, 32), this.Ctx);
                State2 newState = new State2(this.State);
                newState.Set(Rn.ECX, tup.result);
                this.Execute(newState);
                // Flags are unaffected
                */
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return new List<Rn>(1) { Rn.ECX }; } }
        }

        public sealed class Loop : OpcodeLoopBase
        {
            public Loop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LOOP, args, keys, t) { }

            protected sealed override BoolExpr Jump { get { return this.ctx_.MkEq(this.Get(Rn.ECX), this.ctx_.MkBV(0, 32)); } }
        }

        public sealed class Loopz : OpcodeLoopBase
        {
            public Loopz(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LOOPZ, args, keys, t) { }

            protected sealed override BoolExpr Jump { get { return this.ctx_.MkAnd(this.ctx_.MkEq(this.Get(Rn.ECX), this.ctx_.MkBV(0, 32)), this.Get(Flags.ZF)); } }

            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }

        public sealed class Loope : OpcodeLoopBase
        {
            public Loope(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LOOPE, args, keys, t) { }

            protected sealed override BoolExpr Jump { get { return this.ctx_.MkAnd(this.ctx_.MkEq(this.Get(Rn.ECX), this.ctx_.MkBV(0, 32)), this.Get(Flags.ZF)); } }

            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }

        public sealed class Loopnz : OpcodeLoopBase
        {
            public Loopnz(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LOOPNZ, args, keys, t) { }

            protected sealed override BoolExpr Jump { get { return this.ctx_.MkAnd(this.ctx_.MkEq(this.Get(Rn.ECX), this.ctx_.MkBV(0, 32)), this.ctx_.MkNot(this.Get(Flags.ZF))); } }

            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }

        public sealed class Loopne : OpcodeLoopBase
        {
            public Loopne(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LOOPNE, args, keys, t) { }

            protected sealed override BoolExpr Jump { get { return this.ctx_.MkAnd(this.ctx_.MkEq(this.Get(Rn.ECX), this.ctx_.MkBV(0, 32)), this.ctx_.MkNot(this.Get(Flags.ZF))); } }

            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }
        #endregion Loop

        public sealed class Call : Opcode1Base
        {
            public Call(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CALL, args, Ot1.imm | Ot1.mem | Ot1.reg | Ot1.UNKNOWN, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                }
            }

            public int LineNumber //TODO consider using BitVecExpr as lineNumber
            {
                get
                {
                    int lineNumber = -1;
                    switch (this.op1_.Type)
                    {
                        case Ot1.reg:
                            this.SyntaxError = "WARNING: Call: jumping based on registry value is not supported.";
                            lineNumber = -1;
                            break;
                        case Ot1.mem:
                            // unsupported
                            this.SyntaxError = "WARNING: Call: jumping based on memory value is not supported.";
                            lineNumber = -1;
                            break;
                        case Ot1.imm: // assuming the imm is an line number
                            lineNumber = (int)this.op1_.Imm;
                            break;
                        case Ot1.UNKNOWN: // assuming it is a string with a line number in it.
                            lineNumber = ToolsZ3.GetLineNumberFromLabel(this.op1_.ToString(), StaticFlow.LINENUMBER_SEPARATOR);
                            break;
                        default:
                            lineNumber = -1;
                            break;
                    }
                    if (lineNumber < 0)
                    {
                        this.Warning = "line number is -1";
                    }
                    //Console.WriteLine("INFO: OpcodeJumpBase: lineNumber return " + lineNumber + ".");
                    return lineNumber;
                }
            }

            public override void Execute()
            {
                throw new NotImplementedException();
                /*
                this.So1.LineNumber = this.LineNumber; // overwriting default next line number
                if (this._t.Parameters.mode_64bit)
                {
                    BitVecExpr rspExpr = this.Get(Rn.RSP);
                    BitVecExpr newRspExpr = this.Ctx.MkBVAdd(rspExpr, this.Ctx.MkBV(8, 64));
                    this.UpdateRegular.SetMem(rspExpr, this.Ctx.MkBV(this.State.LineNumber, 64));
                    this.UpdateRegular.Set(Rn.RSP, newRspExpr);
                }
                else if (this._t.Parameters.mode_32bit)
                {
                    BitVecExpr espExpr = this.Get(Rn.ESP);
                    BitVecExpr newEspExpr = this.Ctx.MkBVAdd(espExpr, this.Ctx.MkBV(4, 32));
                    this.UpdateRegular.SetMem(espExpr, this.Ctx.MkBV(this.State.LineNumber, 32));
                    this.UpdateRegular.Set(Rn.ESP, newEspExpr);
                }
                else if (this._t.Parameters.mode_16bit)
                {
                    BitVecExpr spExpr = this.Get(Rn.SP);
                    BitVecExpr newSpExpr = this.Ctx.MkBVAdd(spExpr, this.Ctx.MkBV(2, 16));
                    this.UpdateRegular.SetMem(spExpr, this.Ctx.MkBV(this.State.LineNumber, 16));
                    this.UpdateRegular.Set(Rn.SP, newSpExpr);
                }
                else throw new Exception();
                */
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }

                    if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }

                    if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }

                    if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }

                    if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }

                    foreach (Rn r in ReadRegs(this.op1_, false))
                    {
                        yield return r;
                    }
                }
            }

            public override bool MemReadStatic { get { return true; } }

            public override bool MemWriteStatic { get { return true; } }
        }

        public sealed class Ret : OpcodeNBase
        {
            public Ret(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.RET, args, 1, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.NOperands == 1)
                {
                    if (this.op1_.IsImm)
                    {
                        if (this.op1_.NBits != 16)
                        {
                            this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                        }
                    }
                    else
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits);
                    }
                }
            }

            public override void Execute()
            {
                BitVecExpr nextLineNumberExpr;

                if (this.tools_.Parameters.mode_64bit)
                {
                    BitVecExpr newRspExpr = this.ctx_.MkBVSub(this.Get(Rn.RSP), this.ctx_.MkBV(8, 64));
                    nextLineNumberExpr = this.GetMem(newRspExpr, 8);
                    this.RegularUpdate.Set(Rn.RSP, newRspExpr);
                }
                else if (this.tools_.Parameters.mode_32bit)
                {
                    BitVecExpr newEspExpr = this.ctx_.MkBVSub(this.Get(Rn.ESP), this.ctx_.MkBV(4, 32));
                    nextLineNumberExpr = this.GetMem(newEspExpr, 4);
                    this.RegularUpdate.Set(Rn.ESP, newEspExpr);
                }
                else if (this.tools_.Parameters.mode_16bit)
                {
                    BitVecExpr newSpExpr = this.ctx_.MkBVSub(this.Get(Rn.SP), this.ctx_.MkBV(2, 16));
                    nextLineNumberExpr = this.GetMem(newSpExpr, 2);
                    this.RegularUpdate.Set(Rn.SP, newSpExpr);
                }
                else
                {
                    return;
                }

                this.RegularUpdate.NextLineNumberExpr = nextLineNumberExpr;
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }

                    if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }

                    if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.tools_.Parameters.mode_64bit)
                    {
                        yield return Rn.RSP;
                    }

                    if (this.tools_.Parameters.mode_32bit)
                    {
                        yield return Rn.ESP;
                    }

                    if (this.tools_.Parameters.mode_16bit)
                    {
                        yield return Rn.SP;
                    }

                    if (this.op1_ != null)
                    {
                        foreach (Rn r in ReadRegs(this.op1_, false))
                        {
                            yield return r;
                        }
                    }
                }
            }

            public override bool MemReadStatic { get { return true; } }

            public override bool MemWriteStatic { get { return true; } }
        }
        #endregion Control Transfer Instructions

        #region String Instructions

        /// <summary>
        /// The string instructions operate on strings of bytes, allowing them to be moved to and from memory.
        /// </summary>
        public abstract class StringOperationAbstract : OpcodeNBase
        {
            protected readonly Mnemonic prefix_;
            protected int nBytes_ = 0;

            public StringOperationAbstract(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, 2, keys, t)
            {
                this.prefix_ = prefix;
                if (this.IsHalted)
                {
                    return;
                }

                if (this.NOperands == 2)
                {
                    if (this.op1_.NBits != this.op2_.NBits)
                    {
                        this.CreateSyntaxError1(this.op1_, this.op2_);
                    }
                }
            }

            protected Rn SourceIndexReg { get { return this.tools_.Parameters.mode_64bit ? Rn.RSI : (this.tools_.Parameters.mode_32bit ? Rn.ESI : Rn.SI); } }

            protected Rn DestinationIndexReg { get { return this.tools_.Parameters.mode_64bit ? Rn.RDI : (this.tools_.Parameters.mode_32bit ? Rn.EDI : Rn.DI); } }

            protected Rn CounterReg { get { return this.tools_.Parameters.mode_64bit ? Rn.RCX : (this.tools_.Parameters.mode_32bit ? Rn.ECX : Rn.CX); } }

            protected Rn AccumulatorReg
            {
                get
                {
                    switch (this.nBytes_)
                    {
                        case 1: return Rn.AL;
                        case 2: return Rn.AX;
                        case 4: return Rn.EAX;
                        case 8: return Rn.RAX;
                        default:
                            Console.WriteLine("WARNING: StringOperationAbstract: AccumulatorReg: nBytes has invalid value " + this.nBytes_ + ".");
                            return Rn.AL;
                    }
                }
            }

            protected BitVecNum IncrementBV
            {
                get
                {
                    int nBits = this.tools_.Parameters.mode_64bit ? 64 : (this.tools_.Parameters.mode_32bit ? 32 : 16);
                    return this.ctx_.MkBV(this.nBytes_, (uint)nBits);
                }
            }

            public override Flags FlagsReadStatic { get { return Flags.DF; } }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return this.SourceIndexReg;
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return this.SourceIndexReg;
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }
        }

        public sealed class Movs : StringOperationAbstract
        {
            public Movs(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, prefix, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!((prefix == Mnemonic.NONE) || (prefix == Mnemonic.REP)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid prefix {1}. Only REP is allowed.", this.ToString(), prefix);
                }
                if (this.NOperands == 2)
                {
                    this.nBytes_ = this.op1_.NBits >> 3;
                }
                else if (this.NOperands == 0)
                {
                    switch (mnemonic)
                    {
                        case Mnemonic.MOVSB: this.nBytes_ = 1; break;
                        case Mnemonic.MOVSW: this.nBytes_ = 2; break;
                        case Mnemonic.MOVSD: this.nBytes_ = 4; break;
                        case Mnemonic.MOVSQ: this.nBytes_ = 8; break;
                        default: break;
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid number of operands. Expected 0 or 2 operands, found {1} operand(s)", this.ToString(), this.NOperands);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                Rn src = this.SourceIndexReg;
                Rn dst = this.DestinationIndexReg;
                BitVecExpr srcBV = this.Get(src);
                BitVecExpr dstBV = this.Get(dst);

                BoolExpr df = this.Get(Flags.DF);

                if (this.prefix_ == Mnemonic.NONE)
                {
                    BitVecExpr totalBytes = this.IncrementBV;
                    this.RegularUpdate.Set(src, ctx.MkITE(df, ctx.MkBVSub(srcBV, totalBytes), ctx.MkBVAdd(srcBV, totalBytes)) as BitVecExpr);
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);

                    //Update memory: copy memory location
                    this.RegularUpdate.Set_Mem(srcBV, this.GetMem(dstBV, this.nBytes_));
                }
                else if (this.prefix_ == Mnemonic.REP)
                {
                    Rn counter = this.CounterReg;
                    BitVecExpr counterBV = this.Get(counter);
                    BitVecExpr totalBytes = ctx.MkBVMul(this.IncrementBV, counterBV);

                    this.RegularUpdate.Set(counter, 0UL);
                    this.RegularUpdate.Set(src, ctx.MkITE(df, ctx.MkBVSub(srcBV, totalBytes), ctx.MkBVAdd(srcBV, totalBytes)) as BitVecExpr);
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);

                    //Update memory: set all memory as unknown
                    this.RegularUpdate.Set_Mem_Unknown();
                }
            }
        }

        public sealed class Cmps : StringOperationAbstract
        {
            public Cmps(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, prefix, args, keys, t)
            {
                Context ctx = this.ctx_;

                if (this.IsHalted)
                {
                    return;
                }

                if (!((prefix == Mnemonic.NONE) || (prefix == Mnemonic.REPE) || (prefix == Mnemonic.REPZ) || (prefix == Mnemonic.REPNE) || (prefix == Mnemonic.REPNZ)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid prefix {1}. Only REPE, REPZ, REPZE or REPNZ are allowed.", this.ToString(), prefix);
                }
                if (this.NOperands == 2)
                {
                    this.nBytes_ = this.op1_.NBits >> 3;
                }
                else if (this.NOperands == 0)
                {
                    switch (mnemonic)
                    {
                        case Mnemonic.CMPSB: this.nBytes_ = 1; break;
                        case Mnemonic.CMPSW: this.nBytes_ = 2; break;
                        case Mnemonic.CMPSD: this.nBytes_ = 4; break;
                        case Mnemonic.CMPSQ: this.nBytes_ = 8; break;
                        default: break;
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid number of operands. Expected 0 or 2 operands, found {1} operand(s)", this.ToString(), this.NOperands);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                Rn src = this.SourceIndexReg;
                Rn dst = this.DestinationIndexReg;
                BitVecExpr srcBV = this.Get(src);
                BitVecExpr dstBV = this.Get(dst);

                BoolExpr df = this.Get(Flags.DF);

                if (this.prefix_ == Mnemonic.NONE)
                {
                    (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(this.GetMem(src, this.nBytes_), this.GetMem(dst, this.nBytes_), this.ctx_);

                    this.RegularUpdate.Set(Flags.CF, cf);
                    this.RegularUpdate.Set(Flags.OF, of);
                    this.RegularUpdate.Set(Flags.AF, af);
                    this.RegularUpdate.Set_SF_ZF_PF(result);

                    BitVecExpr totalBytes = this.IncrementBV;
                    this.RegularUpdate.Set(src, ctx.MkITE(df, ctx.MkBVSub(srcBV, totalBytes), ctx.MkBVAdd(srcBV, totalBytes)) as BitVecExpr);
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);
                }
                else if ((this.prefix_ == Mnemonic.REPE) || (this.prefix_ == Mnemonic.REPZ))
                {
                    this.RegularUpdate.Set(Flags.CF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.ZF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNKNOWN);

                    this.RegularUpdate.Set(this.CounterReg, Tv.UNKNOWN); // I know that counterreg is between zero and the old value...
                    this.RegularUpdate.Set(src, Tv.UNKNOWN);
                    this.RegularUpdate.Set(dst, Tv.UNKNOWN);
                }
                else if ((this.prefix_ == Mnemonic.REPNE) || (this.prefix_ == Mnemonic.REPNZ))
                {
                    this.RegularUpdate.Set(Flags.CF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.ZF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNKNOWN);

                    this.RegularUpdate.Set(this.CounterReg, Tv.UNKNOWN);
                    this.RegularUpdate.Set(src, Tv.UNKNOWN);
                    this.RegularUpdate.Set(dst, Tv.UNKNOWN);
                }
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }

        public sealed class Stos : StringOperationAbstract
        {
            public Stos(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, prefix, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!((prefix == Mnemonic.NONE) || (prefix == Mnemonic.REP)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid prefix {1}. Only REP is allowed.", this.ToString(), prefix);
                }
                if (this.NOperands == 2)
                {
                    this.nBytes_ = this.op1_.NBits >> 3;
                }
                else if (this.NOperands == 0)
                {
                    switch (mnemonic)
                    {
                        case Mnemonic.STOSB: this.nBytes_ = 1; break;
                        case Mnemonic.STOSW: this.nBytes_ = 2; break;
                        case Mnemonic.STOSD: this.nBytes_ = 4; break;
                        case Mnemonic.STOSQ: this.nBytes_ = 8; break;
                        default: break;
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid number of operands. Expected 0 or 2 operands, found {1} operand(s)", this.ToString(), this.NOperands);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                Rn dst = this.DestinationIndexReg;
                BitVecExpr dstBV = this.Get(dst);

                BoolExpr df = this.Get(Flags.DF);
                BitVecExpr value = this.Get(this.AccumulatorReg);

                if (this.prefix_ == Mnemonic.NONE)
                {
                    BitVecExpr totalBytes = this.IncrementBV;
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);

                    //Update memory: copy memory location
                    this.RegularUpdate.Set_Mem(dstBV, value);
                }
                else if (this.prefix_ == Mnemonic.REP)
                {
                    Rn counter = this.CounterReg;
                    BitVecExpr counterBV = this.Get(counter);
                    BitVecExpr totalBytes = ctx.MkBVMul(this.IncrementBV, counterBV);
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);

                    this.RegularUpdate.Set(counter, 0UL);

                    //Update memory: set all memory as unknown
                    this.RegularUpdate.Set_Mem_Unknown();
                }
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return this.AccumulatorReg;
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }
        }

        /// <summary>
        /// SCAS - Scan string for value of al, ax, eax or rax
        /// SCASB - Scan string/Scan byte string
        /// SCASW - Scan string/Scan word string
        /// SCASD - Scan string/Scan doubleword string
        /// </summary>
        public sealed class Scas : StringOperationAbstract
        {
            public Scas(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, prefix, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!((prefix == Mnemonic.NONE) || (prefix == Mnemonic.REPE) || (prefix == Mnemonic.REPZ) || (prefix == Mnemonic.REPNE) || (prefix == Mnemonic.REPNZ)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid prefix {1}. Only REPE, REPZ, REPZE or REPNZ are allowed.", this.ToString(), prefix);
                }
                if (this.NOperands == 1)
                {
                    this.nBytes_ = this.op1_.NBits >> 3;
                }
                else if (this.NOperands == 0)
                {
                    switch (mnemonic)
                    {
                        case Mnemonic.SCASB: this.nBytes_ = 1; break;
                        case Mnemonic.SCASW: this.nBytes_ = 2; break;
                        case Mnemonic.SCASD: this.nBytes_ = 4; break;
                        case Mnemonic.SCASQ: this.nBytes_ = 8; break;
                        default: break;
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid number of operands. Expected 0 or 1 operand, found {1} operand(s)", this.ToString(), this.NOperands);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                Rn dst = this.DestinationIndexReg;
                BitVecExpr dstBV = this.Get(dst);

                BoolExpr df = this.Get(Flags.DF);
                BitVecExpr accumulator = this.Get(this.AccumulatorReg);

                if (this.prefix_ == Mnemonic.NONE)
                {
                    (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) = BitOperations.Substract(accumulator, this.GetMem(dst, this.nBytes_), this.ctx_);

                    this.RegularUpdate.Set(Flags.CF, cf);
                    this.RegularUpdate.Set(Flags.OF, of);
                    this.RegularUpdate.Set(Flags.AF, af);
                    this.RegularUpdate.Set_SF_ZF_PF(result);

                    BitVecExpr totalBytes = this.IncrementBV;
                    this.RegularUpdate.Set(dst, ctx.MkITE(df, ctx.MkBVSub(dstBV, totalBytes), ctx.MkBVAdd(dstBV, totalBytes)) as BitVecExpr);
                }
                else if ((this.prefix_ == Mnemonic.REPE) || (this.prefix_ == Mnemonic.REPZ))
                {
                    this.RegularUpdate.Set(Flags.CF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.ZF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNKNOWN);

                    this.RegularUpdate.Set(this.CounterReg, Tv.UNKNOWN); // I know that counterreg is between zero and the old value...
                    this.RegularUpdate.Set(dst, Tv.UNKNOWN);
                }
                else if ((this.prefix_ == Mnemonic.REPNE) || (this.prefix_ == Mnemonic.REPNZ))
                {
                    this.RegularUpdate.Set(Flags.CF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.PF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.AF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.ZF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.SF, Tv.UNKNOWN);
                    this.RegularUpdate.Set(Flags.OF, Tv.UNKNOWN);

                    this.RegularUpdate.Set(this.CounterReg, Tv.UNKNOWN);
                    this.RegularUpdate.Set(dst, Tv.UNKNOWN);
                }
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return this.AccumulatorReg;
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return this.DestinationIndexReg;
                    if (this.prefix_ != Mnemonic.NONE)
                    {
                        yield return this.CounterReg;
                    }
                }
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }

        /// <summary>
        /// LODS,
        /// LODSB - Load string/Load byte string into al
        /// LODSW - Load string/Load word string int ax
        /// LODSD - Load string/Load doubleword string into eax
        /// LODSQ - load string/load quadword string into rax
        /// </summary>
        public sealed class Lods : StringOperationAbstract
        {
            public Lods(Mnemonic mnemonic, Mnemonic prefix, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, prefix, args, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (!(prefix == Mnemonic.NONE))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid prefix {1}. No Prefix is allowed.", this.ToString(), prefix);
                }
                if (this.NOperands == 1)
                {
                    this.nBytes_ = this.op1_.NBits >> 3;
                }
                else if (this.NOperands == 0)
                {
                    switch (mnemonic)
                    {
                        case Mnemonic.LODSB: this.nBytes_ = 1; break;
                        case Mnemonic.LODSW: this.nBytes_ = 2; break;
                        case Mnemonic.LODSD: this.nBytes_ = 4; break;
                        case Mnemonic.LODSQ: this.nBytes_ = 8; break;
                        default: break;
                    }
                }
                else
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Invalid number of operands. Expected 0 or 1 operand, found {1} operand(s)", this.ToString(), this.NOperands);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                Rn src = this.SourceIndexReg;
                BitVecExpr srcBV = this.Get(src);

                BoolExpr df = this.Get(Flags.DF);

                if (this.prefix_ == Mnemonic.NONE)
                {
                    BitVecExpr totalBytes = this.IncrementBV;
                    this.RegularUpdate.Set(src, ctx.MkITE(df, ctx.MkBVSub(srcBV, totalBytes), ctx.MkBVAdd(srcBV, totalBytes)) as BitVecExpr);

                    //Update accumulator: copy memory location
                    this.RegularUpdate.Set(this.AccumulatorReg, this.GetMem(srcBV, this.nBytes_));
                }
                else
                {
                    throw new Exception();
                }
            }

            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    yield return this.AccumulatorReg;
                    yield return this.SourceIndexReg;
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    yield return this.AccumulatorReg;
                    yield return this.SourceIndexReg;
                }
            }
        }
        #endregion String Instructions

        #region I/O Instructions
        //These instructions move data between the processor’s I/O ports and a register or memory.

        /// <summary>Read from a port</summary>
        public sealed class In : Opcode2Base
        {
            public In(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.IN, args, Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 64)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                Rn regOp1 = this.op1_.Rn;
                if (!((regOp1 == Rn.AL) || (regOp1 == Rn.AX) || (regOp1 == Rn.EAX)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }

                if (this.op2_.IsImm)
                {
                    if (this.op2_.NBits != 8)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else
                {
                    if (this.op2_.Rn != Rn.DX)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
            }

            public override void Execute()
            {
                // special case: set the truth value to an defined but unknown value
                this.RegularUpdate.Set(this.op1_.Rn, Tv.UNKNOWN);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Write to a port</summary>
        public sealed class Out : Opcode2Base
        {
            public Out(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.OUT, args, Ot2.imm_reg | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op2_.NBits == 64)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                Rn regOp2 = this.op2_.Rn;
                if (!((regOp2 == Rn.AL) || (regOp2 == Rn.AX) || (regOp2 == Rn.EAX)))
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }

                if (this.op1_.IsImm)
                {
                    if (this.op1_.NBits != 8)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
                else
                {
                    if (this.op1_.Rn != Rn.DX)
                    {
                        this.SyntaxError = string.Format(Culture, "\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                    }
                }
            }

            public override void Execute()
            {
                // state is not changed
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op2_, false); } }
        }

        //INS,

        /// <summary>XXX</summary>
        //INSB,// Input string from port/Input byte string from port
        /// <summary>XXX</summary>
        //INSW,// Input string from port/Input word string from port
        /// <summary>XXX</summary>
        //INSD,// Input string from port/Input doubleword string from port
        /// <summary>XXX</summary>
        //OUTS,
        /// <summary>XXX</summary>
        //OUTSB,// Output string to port/Output byte string to port
        /// <summary>XXX</summary>
        //OUTSW,// Output string to port/Output word string to port
        /// <summary>XXX</summary>
        //OUTSD,// Output string to port/Output doubleword string to port
        #endregion I/O Instructions

        #region Flag Control (EFLAG) Instructions
        /// <summary>Set carry flag</summary>
        public sealed class Stc : Opcode0Base
        {
            public Stc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.STC, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, Tv.ONE);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }

        /// <summary>Clear carry flag</summary>
        public sealed class Clc : Opcode0Base
        {
            public Clc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CLC, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }

        /// <summary>Complement carry flag</summary>
        public sealed class Cmc : Opcode0Base
        {
            public Cmc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CMC, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, this.ctx_.MkNot(this.Get(Flags.CF)));
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }

        /// <summary>Set direction flag</summary>
        public sealed class Std : Opcode0Base
        {
            public Std(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.STD, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.DF, Tv.ONE);
            }

            public override Flags FlagsWriteStatic { get { return Flags.DF; } }
        }

        /// <summary>Clear direction flag</summary>
        public sealed class Cld : Opcode0Base
        {
            public Cld(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.CLD, args, keys, t) { }

            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.DF, Tv.ZERO);
            }

            public override Flags FlagsWriteStatic { get { return Flags.DF; } }
        }

        public sealed class Lahf : Opcode0Base
        {
            public Lahf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LAHF, args, keys, t) { }

            public override void Execute()
            {
                BitVecNum zERO = this.ctx_.MkBV(0, 1);
                BitVecNum oNE = this.ctx_.MkBV(1, 1);

                BitVecExpr ahExpr = this.ctx_.MkITE(this.Get(Flags.SF), oNE, zERO) as BitVecExpr;
                ahExpr = this.ctx_.MkConcat(ahExpr, this.ctx_.MkITE(this.Get(Flags.ZF), oNE, zERO) as BitVecExpr);
                ahExpr = this.ctx_.MkConcat(ahExpr, zERO);
                ahExpr = this.ctx_.MkConcat(ahExpr, this.ctx_.MkITE(this.Get(Flags.AF), oNE, zERO) as BitVecExpr);
                ahExpr = this.ctx_.MkConcat(ahExpr, zERO);
                ahExpr = this.ctx_.MkConcat(ahExpr, this.ctx_.MkITE(this.Get(Flags.PF), oNE, zERO) as BitVecExpr);
                ahExpr = this.ctx_.MkConcat(ahExpr, oNE);
                ahExpr = this.ctx_.MkConcat(ahExpr, this.ctx_.MkITE(this.Get(Flags.CF), oNE, zERO) as BitVecExpr);

                this.RegularUpdate.Set(Rn.AH, ahExpr);
            }

            public override Flags FlagsReadStatic { get { return Flags.SF | Flags.ZF | Flags.AF | Flags.PF | Flags.CF; } }
        }

        public sealed class Sahf : Opcode0Base
        {
            public Sahf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.SAHF, args, keys, t) { }

            public override void Execute()
            {
                BitVecNum oNE = this.ctx_.MkBV(1, 1);
                BitVecExpr ahExpr = this.Get(Rn.AH);

                this.RegularUpdate.Set(Flags.SF, ToolsZ3.GetBit(ahExpr, 7, oNE, this.ctx_));
                this.RegularUpdate.Set(Flags.ZF, ToolsZ3.GetBit(ahExpr, 6, oNE, this.ctx_));
                this.RegularUpdate.Set(Flags.AF, ToolsZ3.GetBit(ahExpr, 4, oNE, this.ctx_));
                this.RegularUpdate.Set(Flags.PF, ToolsZ3.GetBit(ahExpr, 2, oNE, this.ctx_));
                this.RegularUpdate.Set(Flags.CF, ToolsZ3.GetBit(ahExpr, 0, oNE, this.ctx_));
            }

            public override Flags FlagsWriteStatic { get { return Flags.SF | Flags.ZF | Flags.AF | Flags.PF | Flags.CF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return new List<Rn>(1) { Rn.AH }; } } // defaults
        }
        #endregion

        #region Segment Register Instructions
        //The segment register instructions allow far pointers (segment addresses) to be loaded into the segment registers.

        /// <summary>XXX</summary>
        //LDS,// Load far pointer using DS
        /// <summary>XXX</summary>
        //LES,// Load far pointer using ES
        /// <summary>XXX</summary>
        //LFS,// Load far pointer using FS
        /// <summary>XXX</summary>
        //LGS,// Load far pointer using GS
        /// <summary>XXX</summary>
        //LSS,// Load far pointer using SS
        #endregion Segment Register Instructions

        #region Miscellaneous Instructions
        public sealed class Lea : Opcode2Base
        {
            public Lea(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.LEA, args, Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 cannot be 8 bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override void Execute()
            {
                BitVecExpr address = Tools.Calc_Effective_Address(this.op2_, this.keys_.prevKey, this.ctx_);
                uint addressSize = address.SortSize;
                uint operandSize = (uint)this.op1_.NBits;

                if (operandSize == addressSize)
                {
                    this.RegularUpdate.Set(this.op1_, address);
                }
                else if ((operandSize == 16) && (addressSize == 32))
                {
                    this.RegularUpdate.Set(this.op1_, this.ctx_.MkExtract(16 - 1, 0, address));
                }
                else if ((operandSize == 16) && (addressSize == 64))
                {
                    this.RegularUpdate.Set(this.op1_, this.ctx_.MkExtract(16 - 1, 0, address));
                }
                else if ((operandSize == 32) && (addressSize == 64))
                {
                    this.RegularUpdate.Set(this.op1_, this.ctx_.MkExtract(32 - 1, 0, address));
                }
                else if ((operandSize == 64) && (addressSize == 32))
                {
                    this.RegularUpdate.Set(this.op1_, this.ctx_.MkZeroExt(32, address));
                }
            }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Nop : Opcode0Base
        {
            public Nop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.NOP, args, keys, t) { }

            public override void Execute()
            {
                this.Create_RegularUpdate(); // do nothing, only create an empty update
            }
        }

        /// <summary>Generates an invalid opcode. This instruction is provided for software testing to explicitly generate an invalid opcode. The opcode for this instruction is reserved for this purpose. Other than raising the invalid opcode exception, this instruction is the same as the NOP instruction.</summary>
        //UD2,
        /// <summary>Table lookup translation</summary>
        //XLAT,
        /// <summary>Table lookup translation</summary>
        //XLATB,
        /// <summary>Processor identification</summary>
        //CPUID,

        /// <summary>Move data after swapping data bytes</summary>
        public sealed class Movbe : Opcode2Base
        {
            public Movbe(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.MOVBE, args, Ot2.mem_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": Operand 1 cannot be 8 bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                BitVecExpr src = this.Op2Value;
                BitVecExpr swapped = null;
                switch (this.op1_.NBits)
                {
                    case 16:
                        {
                            swapped = ctx.MkConcat(ctx.MkExtract(7, 0, src), ctx.MkExtract(15, 8, src));
                            break;
                        }
                    case 32:
                        {
                            BitVecExpr swapped1 = ctx.MkConcat(ctx.MkExtract(7, 0, src), ctx.MkExtract(15, 8, src));
                            BitVecExpr swapped2 = ctx.MkConcat(ctx.MkExtract(23, 16, src), ctx.MkExtract(31, 24, src));
                            swapped = ctx.MkConcat(swapped1, swapped2);
                            break;
                        }
                    case 64:
                        {
                            BitVecExpr swapped1 = ctx.MkConcat(ctx.MkExtract(7, 0, src), ctx.MkExtract(15, 8, src));
                            BitVecExpr swapped2 = ctx.MkConcat(ctx.MkExtract(23, 16, src), ctx.MkExtract(31, 24, src));
                            BitVecExpr swapped3 = ctx.MkConcat(ctx.MkExtract(39, 32, src), ctx.MkExtract(47, 40, src));
                            BitVecExpr swapped4 = ctx.MkConcat(ctx.MkExtract(55, 48, src), ctx.MkExtract(63, 56, src));
                            swapped = ctx.MkConcat(ctx.MkConcat(swapped1, swapped2), ctx.MkConcat(swapped3, swapped4));
                            break;
                        }
                    default: throw new Exception();
                }
                this.RegularUpdate.Set(this.op1_, swapped);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Prefetch data into cache in anticipation of write</summary>
        //PREFETCHW,
        /// <summary>Prefetch hint T1 with intent to write</summary>
        //PREFETCHWT1,
        /// <summary>Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy</summary>
        //CLFLUSH,
        /// <summary>Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy with optimized memory system throughput</summary>
        //CLFLUSHOPT,
        #endregion

        #region User Mode Extended Sate Save/Restore Instructions
        /// <summary>XXX</summary>
        //XSAVE,// Save processor extended states to memory
        /// <summary>XXX</summary>
        //XSAVEC,// Save processor extended states with compaction to memory
        /// <summary>XXX</summary>
        //XSAVEOPT,// Save processor extended states to memory, optimized
        /// <summary>XXX</summary>
        //XRSTOR,// Restore processor extended states from memory
        /// <summary>XXX</summary>
        //XGETBV,// Reads the state of an extended control register
        #endregion  User Mode Extended Sate Save/Restore Instructions

        #region Random Number Generator Instructions
        /// <summary>XXX</summary>
        //RDRAND,// Retrieves a random number generated from hardware
        /// <summary>XXX</summary>
        //RDSEED,// Retrieves a random number generated from hardware
        #endregion Random Number Generator Instructions

        #region BMI1, BMI2
        /// <summary>XXX</summary>
        //ANDN,// Bitwise AND of first source with inverted 2nd source operands.
        /// <summary>XXX</summary>
        //BEXTR,// Contiguous bitwise extract
        /// <summary>XXX</summary>
        //BLSI,// Extract lowest set bit
        /// <summary>XXX</summary>
        //BLSMSK,// Set all lower bits below first set bit to 1
        /// <summary>XXX</summary>
        //BLSR,// Reset lowest set bit
        /// <summary>XXX</summary>
        //BZHI,// Zero high bits starting from specified bit position

        /// <summary>Count the number leading zero bits</summary>
        //LZCNT,
        /// <summary>Unsigned multiply without affecting arithmetic flags</summary>
        //MULX,
        /// <summary>Parallel deposit of bits using a mask</summary>
        //PDEP,
        /// <summary>Parallel extraction of bits using a mask</summary>
        //PEXT,
        /// <summary>Rotate right without affecting arithmetic flags</summary>
        //RORX,
        /// <summary>Shift arithmetic right</summary>
        //SARX,
        /// <summary>Shift logic left</summary>
        //SHLX,
        /// <summary>Shift logic right</summary>
        //SHRX,
        /// <summary>Count the number trailing zero bits</summary>
        //TZCNT,
        #endregion BMI1, BMI2

        #region SSE

        /// <summary>Add Parallel Double FP</summary>
        public sealed class AddPD : Opcode2Base
        {
            public AddPD(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.ADDPD, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                FPRMExpr roundingMode = ctx.MkFPRoundTowardZero();

                BitVecExpr a = this.Op1Value;
                BitVecExpr b = this.Op2Value;

                IList<FPExpr> a_FP = new List<FPExpr>(ToolsFloatingPoint.BV_2_Doubles(a, ctx));
                IList<FPExpr> b_FP = new List<FPExpr>(ToolsFloatingPoint.BV_2_Doubles(b, ctx));

                for (int i = 0; i < a_FP.Count; ++i)
                {
                    a_FP[i] = ctx.MkFPAdd(roundingMode, a_FP[i], b_FP[i]);
                }
                BitVecExpr result = ToolsFloatingPoint.FP_2_BV(a_FP, ctx);
                this.RegularUpdate.Set(this.op1_, result);
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        /// <summary>Xor Parallel Double FP</summary>
        public sealed class XorPD : Opcode2Base
        {
            public XorPD(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.XORPD, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }
            }

            public override void Execute()
            {
                this.RegularUpdate.Set(this.op1_, this.ctx_.MkBVXOR(this.Op1Value, this.Op2Value));
            }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }

        public sealed class Popcnt : Opcode2Base
        {
            public Popcnt(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(Mnemonic.POPCNT, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted)
                {
                    return;
                }

                if (this.op1_.NBits != this.op2_.NBits)
                {
                    this.CreateSyntaxError1(this.op1_, this.op2_);
                }

                if (this.op1_.NBits == 8)
                {
                    this.SyntaxError = string.Format(Culture, "\"{0}\": 8 bits operands are not allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1_, this.op1_.Type, this.op1_.NBits, this.op2_, this.op2_.Type, this.op2_.NBits);
                }
            }

            public override void Execute()
            {
                Context ctx = this.ctx_;
                uint nBits = (uint)this.op1_.NBits;
                BitVecExpr b = this.Op2Value;

                BitVecExpr result = ctx.MkZeroExt(6, ToolsZ3.GetBit_BV(b, 0, ctx));
                for (uint bit = 1; bit < nBits; ++bit)
                {
                    result = ctx.MkBVAdd(result, ctx.MkZeroExt(6, ToolsZ3.GetBit_BV(b, bit, ctx)));
                }

                result = ctx.MkZeroExt(nBits - 7, result);
                this.RegularUpdate.Set(this.op1_, result);

                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.SF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.ZF, ctx.MkEq(b, ctx.MkBV(0, nBits)));
                this.RegularUpdate.Set(Flags.AF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.PF, Tv.ZERO);
            }

            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }

            public override IEnumerable<Rn> RegsReadStatic { get { return ReadRegs(this.op1_, true, this.op2_, false); } }

            public override IEnumerable<Rn> RegsWriteStatic { get { return WriteRegs(this.op1_); } }
        }
        #endregion
        #endregion Instructions
    }
}
