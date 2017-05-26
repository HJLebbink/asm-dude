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
using System.Linq;

namespace AsmSim
{
    namespace Mnemonics
    {
        #region Instructions
        #region Abstract OpcodeBases
        public abstract class OpcodeBase
        {
            #region Fields
            protected readonly Mnemonic _mnemonic;
            private readonly string[] _args;
            protected readonly Tools _t;
            protected readonly Context Ctx;

            protected (string PrevKey, string NextKey, string NextKeyBranch) keys;

            private bool _halted;
            private string _haltMessage;
            private string _warningMessage;

            private StateUpdate _regularUpdate;
            private StateUpdate _branchUpdate;
            #endregion

            protected void CreateRegularUpdate()
            {
                if (this._regularUpdate == null) this._regularUpdate = new StateUpdate(this.keys.PrevKey, this.keys.NextKey, this._t);
            }
            protected void CreateBranchUpdate()
            {
                if (this._branchUpdate == null) this._branchUpdate = new StateUpdate(this.keys.PrevKey, this.keys.NextKeyBranch, this._t);
            }

            protected StateUpdate RegularUpdate
            {
                get
                {
                    if (this._regularUpdate == null) this._regularUpdate = new StateUpdate(this.keys.PrevKey, this.keys.NextKey, this._t);
                    return this._regularUpdate;
                }
            }
            protected StateUpdate BranchUpdate
            {
                get
                {
                    if (this._branchUpdate == null) this._branchUpdate = new StateUpdate(this.keys.PrevKey, this.keys.NextKeyBranch, this._t);
                    return this._branchUpdate;
                }
            }

            public OpcodeBase(Mnemonic m, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
            {
                this._mnemonic = m;
                this._args = args;
                this._t = t;
                this.keys = keys;
                this.Ctx = t.Ctx;
            }

            public abstract void Execute();

            #region Registers/Flags Getters
            /// <summary>Get the current value of the provided register</summary>
            public BitVecExpr Get(Rn regName)
            {
                var result = Tools.Reg_Key(regName, this.keys.PrevKey, this.Ctx); //TODO: check prevKey
                return result;
            }
            public static BitVecExpr Get(Rn regName, string prevKey, Context ctx)
            {
                return Tools.Reg_Key(regName, prevKey, ctx);
            }

            public BitVecExpr Undef(Rn regName)
            {
                return Tools.Reg_Key_Fresh(regName, this._t.Rand, this.Ctx);
            }
            public static BitVecExpr Undef(Rn regName, Tools t)
            {
                return Tools.Reg_Key_Fresh(regName, t.Rand, t.Ctx);
            }
            /// <summary>Get the current value of the provided flag</summary>
            public BoolExpr Get(Flags flagName)
            {
                return Tools.Flag_Key(flagName, this.keys.PrevKey, this.Ctx);
            }
            public static BoolExpr Get(Flags flagName, string prevKey, Context ctx)
            {
                return Tools.Flag_Key(flagName, prevKey, ctx);
            }

            public BoolExpr Undef(Flags flagName)
            {
                return Tools.Flag_Key_Fresh(flagName, this._t.Rand, this.Ctx);
            }
            public static BoolExpr Undef(Flags flagName, Tools t)
            {
                return Tools.Flag_Key_Fresh(flagName, t.Rand, t.Ctx);
            }

            public BitVecExpr GetMem(BitVecExpr address, int nBytes)
            {
                return Tools.Get_Value_From_Mem(address, nBytes, this.keys.PrevKey, this.Ctx);
            }
            #endregion

            public (StateUpdate Regular, StateUpdate Branch) Updates
            {
                get { return (this._regularUpdate, this._branchUpdate); }
            }

            #region Register/Flags read/write
            /// <summary>Get the flags that are read by this Mnemnonic</summary>
            public virtual Flags FlagsReadStatic { get { return Flags.NONE; } }

            /// <summary>Get the flags that are written by this Mnemnonic</summary>
            public virtual Flags FlagsWriteStatic { get { return Flags.NONE; } }

            public virtual IEnumerable<Rn> RegsReadStatic { get { return Enumerable.Empty<Rn>(); } }
            public virtual IEnumerable<Rn> RegsWriteStatic { get { return Enumerable.Empty<Rn>(); } }
            public virtual bool MemReadWriteStatic { get { return false; } }
            #endregion

            public string Warning
            {
                get
                {
                    return this._warningMessage;
                }
                protected set
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
            public bool IsHalted { get { return this._halted; } }
            public string SyntaxError
            {
                get
                {
                    return this._haltMessage;
                }
                protected set
                {
                    if (this._haltMessage == null)
                    {
                        this._haltMessage = value;
                    }
                    else
                    {
                        this._haltMessage += Environment.NewLine + value;
                    }
                    this._halted = true;
                }
            }
            public override string ToString()
            {
                return this._mnemonic + " " + string.Join(", ", this._args);
            }

            #region Protected stuff
            /// <summary>Return number of operand of the arguments of this instruction</summary>
            protected int NOperands { get { return this._args.Length; } }

            public static BitVecExpr OpValue(
                Operand operand,
                string key,
                Context ctx,
                int nBits = -1)
            {
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
                                Rn reg = operand.Rn;
                                if (nBits == 64)
                                {
                                    return ctx.MkBVConst(Tools.Reg_Name(reg, key), 64);
                                }
                                else
                                {
                                    BitVecExpr regExpr = ctx.MkBVConst(Tools.Reg_Name(RegisterTools.Get64BitsRegister(reg), key), 64);
                                    return (RegisterTools.Is8BitHigh(reg))
                                        ? ctx.MkExtract(16, 8, regExpr)
                                        : ctx.MkExtract((uint)nBits - 1, 0, regExpr);
                                }
                            }
                        case Ot1.mem:
                            {
                                BitVecExpr address = Tools.Calc_Effective_Address(operand, key, ctx);
                                int nBytes = nBits >> 3;
                                return Tools.Get_Value_From_Mem(address, nBytes, key, ctx);
                                //return address;
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
                } catch (Exception e)
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

            protected IEnumerable<Rn> ToRegEnumerable(Operand op1)
            {
                if (op1 != null)
                {
                    if (op1.IsReg)
                    {
                        yield return op1.Rn;
                    }
                    else if (op1.IsMem)
                    {
                        var mem = op1.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
            }
            protected IEnumerable<Rn> ToRegEnumerable(Operand op1, Operand op2)
            {
                if (op1 != null)
                {
                    if (op1.IsReg)
                    {
                        yield return op1.Rn;
                    }
                    else if (op1.IsMem)
                    {
                        var mem = op1.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
                if (op2 != null)
                {
                    if (op2.IsReg)
                    {
                        yield return op2.Rn;
                    }
                    else if (op2.IsMem)
                    {
                        var mem = op2.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
            }
            protected IEnumerable<Rn> ToRegEnumerable(Operand op1, Operand op2, Operand op3)
            {
                if (op1 != null)
                {
                    if (op1.IsReg)
                    {
                        yield return op1.Rn;
                    }
                    else if (op1.IsMem)
                    {
                        var mem = op1.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
                if (op2 != null)
                {
                    if (op2.IsReg)
                    {
                        yield return op2.Rn;
                    }
                    else if (op2.IsMem)
                    {
                        var mem = op2.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
                if (op3 != null)
                {
                    if (op3.IsReg)
                    {
                        yield return op3.Rn;
                    }
                    else if (op3.IsMem)
                    {
                        var mem = op3.Mem;
                        if (mem.BaseReg != Rn.NOREG) yield return mem.BaseReg;
                        if (mem.IndexReg != Rn.NOREG) yield return mem.IndexReg;
                    }
                }
            }
            #endregion
        }
        public abstract class Opcode0Base : OpcodeBase
        {
            public Opcode0Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, keys, t)
            {
                if (this.NOperands != 0)
                {
                    this.SyntaxError = string.Format("\"{0}\": Expected 0 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                }
            }
        }
        public abstract class Opcode1Base : OpcodeBase
        {
            protected readonly Operand op1;
            public Opcode1Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                if (this.NOperands == 1)
                {
                    this.op1 = new Operand(args[0]);
                    if (this.op1.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1.ErrorMessage);
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 1 operand. Found 0 operands.", this.ToString());
                    }
                    else
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 1 operand. Found {1} operands with values \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }
            public Opcode1Base(Mnemonic mnemonic, string[] args, Ot1 allowedOperands1, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted) return;
                if (!allowedOperands1.HasFlag(this.op1.Type))
                {
                    this.SyntaxError = string.Format("\"{0}\": First operand ({1}) cannot be of type {2}. Allowed types: {3}.", this.ToString(), this.op1, this.op1.Type, AsmSourceTools.ToString(allowedOperands1));
                }
            }
            public BitVecExpr Op1Value { get { return OpcodeBase.OpValue(this.op1, this.keys.PrevKey, this.Ctx); } }
            public override bool MemReadWriteStatic { get { return ToMemReadWrite(this.op1); } }
        }
        public abstract class Opcode2Base : OpcodeBase
        {
            protected readonly Operand op1;
            protected readonly Operand op2;
            public Opcode2Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                if (this.NOperands == 2)
                {
                    this.op1 = new Operand(args[0]);
                    this.op2 = new Operand(args[1]);
                    if (this.op1.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1.ErrorMessage);
                    if (this.op2.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2.ErrorMessage);
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 2 operands. Found 0 operands.", this.ToString(), this.NOperands);
                    }
                    else
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 2 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }
            public Opcode2Base(Mnemonic mnemonic, string[] args, Ot2 allowedOperands2, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted) return;
                if (!allowedOperands2.HasFlag(AsmSourceTools.MergeOt(this.op1.Type, this.op2.Type)))
                {
                    this.SyntaxError = string.Format("\"{0}\": Invalid combination of opcode and operands. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}). Allowed types: {7}.",
                        this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, AsmSourceTools.ToString(allowedOperands2));
                }
            }
            public BitVecExpr Op1Value { get { return OpcodeBase.OpValue(this.op1, this.keys.PrevKey, this.Ctx); } }
            public BitVecExpr Op2Value { get { return OpcodeBase.OpValue(this.op2, this.keys.PrevKey, this.Ctx); } }
            public override bool MemReadWriteStatic { get { return ToMemReadWrite(this.op1, this.op2); } }
        }
        public abstract class Opcode3Base : OpcodeBase
        {
            protected readonly Operand op1;
            protected readonly Operand op2;
            protected readonly Operand op3;
            public Opcode3Base(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                if (this.NOperands == 3)
                {
                    this.op1 = new Operand(args[0]);
                    this.op2 = new Operand(args[1]);
                    this.op3 = new Operand(args[2]);

                    if (this.op1.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1.ErrorMessage);
                    if (this.op1.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2.ErrorMessage);
                    if (this.op1.ErrorMessage != null) this.SyntaxError = string.Format("\"{0}\": Operand 3 is malformed: {1}", this.ToString(), this.op3.ErrorMessage);
                }
                else
                {
                    if (this.NOperands == 0)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 3 operands. Found 0 operands.", this.ToString(), this.NOperands);
                    }
                    else
                    {
                        this.SyntaxError = string.Format("\"{0}\": Expected 3 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
                    }
                }
            }
            public Opcode3Base(Mnemonic mnemonic, string[] args, Ot3 allowedOperands3, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : this(mnemonic, args, keys, t)
            {
                if (this.IsHalted) return;
                if (!allowedOperands3.HasFlag(AsmSourceTools.MergeOt(this.op1.Type, this.op2.Type, this.op3.Type)))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={6} ({7}, bits={8}) Allowed types: {9}.", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits, AsmSourceTools.ToString(allowedOperands3));
                }
            }
            public BitVecExpr Op1Value { get { return OpcodeBase.OpValue(this.op1, this.keys.PrevKey, this.Ctx); } }
            public BitVecExpr Op2Value { get { return OpcodeBase.OpValue(this.op2, this.keys.PrevKey, this.Ctx); } }
            public BitVecExpr Op3Value { get { return OpcodeBase.OpValue(this.op3, this.keys.PrevKey, this.Ctx); } }
            public override bool MemReadWriteStatic { get { return ToMemReadWrite(this.op1, this.op2, this.op3); } }
        }
        public abstract class OpcodeNBase : OpcodeBase
        {
            protected readonly Operand op1;
            protected readonly Operand op2;
            protected readonly Operand op3;
            public OpcodeNBase(Mnemonic mnemonic, string[] args, int maxNArgs, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, keys, t)
            {
                if (args.Length > maxNArgs)
                {
                    this.SyntaxError = string.Format("\"{0}\": Only " + maxNArgs + " operands are allowed, and received " + args.Length + " operands.");
                }
                if (this.NOperands >= 1)
                {
                    this.op1 = new Operand(args[0]);
                    if (this.op1.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 1 is malformed: {1}", this.ToString(), this.op1.ErrorMessage);
                    }
                }
                if (this.NOperands >= 2)
                {
                    this.op2 = new Operand(args[1]);
                    if (this.op2.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 2 is malformed: {1}", this.ToString(), this.op2.ErrorMessage);
                    }
                }
                if (this.NOperands >= 3)
                {
                    this.op3 = new Operand(args[2]);
                    if (this.op3.ErrorMessage != null)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 3 is malformed: {1}", this.ToString(), this.op3.ErrorMessage);
                    }
                }
            }
            public BitVecExpr Op1Value { get { return OpcodeBase.OpValue(this.op1, this.keys.PrevKey, this.Ctx); } }
            public BitVecExpr Op2Value { get { return OpcodeBase.OpValue(this.op2, this.keys.PrevKey, this.Ctx); } }
            public BitVecExpr Op3Value { get { return OpcodeBase.OpValue(this.op3, this.keys.PrevKey, this.Ctx); } }
            public override bool MemReadWriteStatic { get { return ToMemReadWrite(this.op1, this.op2, this.op3); } }
        }
        public abstract class Opcode2Type1 : Opcode2Base
        {
            public Opcode2Type1(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.IsImm)
                {
                    if (this.op1.NBits < this.op2.NBits)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 1 should be smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                    if ((this.op1.NBits == 64) && (this.op2.NBits == 32))
                    {
                        this.op2.SignExtend(64);
                    }
                    else if (this.op2.NBits < this.op1.NBits)
                    {
                        this.op2.ZeroExtend(this.op1.NBits);
                    }
                }
                else if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operands should have equal sizes. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
        }
        #endregion Abstract OpcodeBases

        public sealed class NotImplemented : OpcodeBase
        {
            public NotImplemented(Mnemonic mnemnonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.NOP, args, keys, t)
            {
                this.SyntaxError = "Not implemented";
            }
            public override void Execute()
            {
                // do not create updates
            }
        }
        public sealed class Ignore : OpcodeBase
        {
            public Ignore(Mnemonic mnemnonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.NOP, args, keys, t) { }
            public override void Execute()
            {
                this.CreateRegularUpdate(); // do nothing, only create an empty update
            }
        }

        #region Data Transfer Instructions
        public sealed class Mov : Opcode2Type1
        {
            public Mov(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.MOV, args, keys, t) { }
            public override void Execute()
            {
                if (this.op1.Type == Ot1.UNKNOWN)
                {
                    //TODO The moffs8, moffs16, moffs32 and moffs64 operands specify a simple offset relative to the segment base, where 8, 16, 32 and 64 refer to the size of the data.The address-size attribute of the instruction determines the size of the offset, either 16, 32 or 64 bits.
                    this.SyntaxError = string.Format("\"{0}\": execute: Unknown memory address in op1; Operand1={1} ({2}); Operand2={3} ({4})", this.ToString(), this.op1, this.op1.Type, this.op2, this.op2.Type);
                }
                else
                {
                    this.RegularUpdate.Set(this.op1, this.Op2Value);
                }
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Cmovcc : Opcode2Base
        {
            private readonly ConditionalElement _ce;
            public Cmovcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                this._ce = ce;
            }
            public override void Execute()
            {
                BoolExpr conditional = ToolsAsmSim.ConditionalTaken(this._ce, this.keys.PrevKey, this.Ctx);
                BitVecExpr op1 = this.Op1Value;
                BitVecExpr op2 = this.Op2Value;
                //Console.WriteLine("Cmovcc ce="+this._ce+"; conditional=" + conditional);
                BitVecExpr value = this.Ctx.MkITE(conditional, op2, op1) as BitVecExpr;
                BitVecExpr undef = this.Ctx.MkBVXOR(op1, op2);

                this.RegularUpdate.Set(this.op1, value, undef);
            }
            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this._ce); } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        /*
        XCHG,
        /// <summary>Byte swap</summary>
        BSWAP,
        */

        /// <summary>Exchange and add</summary>
        public sealed class Xadd : Opcode2Base
        {
            public Xadd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.XADD, args, Ot2.mem_reg | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 should have equal size. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }

            public override void Execute()
            {
                BitVecExpr a = this.Op1Value;
                BitVecExpr b = this.Op2Value;

                var tup = BitOperations.Addition(a, b, this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(this.op2, a);// swap op1 and op2
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
        }

        /*    
        /// <summary>Compare and exchange and Add</summary>
        CMPXCHG,
        /// <summary>Compare and exchange 8 bytes</summary>
        CMPXCHG8B,
        */

        /// <summary>Push onto stack</summary>
        public sealed class Push : Opcode1Base
        {
            public Push(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.PUSH, args, Ot1.reg | Ot1.mem | Ot1.imm, keys, t)
            {
                if (this.IsHalted) return;
                if ((this.op1.NBits == 8) && (this.op1.IsReg || this.op1.IsMem))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                }
                else if ((this.op1.NBits == 64) && (this.op1.IsImm))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                }

                if (this.op1.IsReg && t.Parameters.mode_64bit)
                {
                    Rn reg = this.op1.Rn;
                    if ((reg == Rn.CS) || (reg == Rn.SS) || (reg == Rn.DS) || (reg == Rn.ES))
                    {
                        this.SyntaxError = string.Format("\"{0}\": Invalid register in 64-bit mode. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                    }
                }

            }
            public override void Execute()
            {
                if (this._t.Parameters.mode_64bit) // the stackAddrSize == 64
                {
                    BitVecExpr value = this.Op1Value;
                    if (this.op1.IsImm)
                    {
                        if (value.SortSize < 64) value = this.Ctx.MkSignExt(64 - value.SortSize, value);
                    }
                    else if (this.op1.IsReg && RegisterTools.IsSegmentRegister(this.op1.Rn))
                    {
                        value = this.Ctx.MkZeroExt(64 - value.SortSize, value);
                    }
                    BitVecExpr rspExpr = this.Get(Rn.RSP);
                    this.RegularUpdate.Set(Rn.RSP, this.Ctx.MkBVSub(rspExpr, this.Ctx.MkBV(8, 64)));
                    this.RegularUpdate.SetMem(rspExpr, value);
                }
                else if (this._t.Parameters.mode_32bit)
                {
                    BitVecExpr value = this.Op1Value;
                    if (this.op1.IsImm)
                    {
                        if (value.SortSize < 32) value = this.Ctx.MkSignExt(32 - value.SortSize, value);
                    }
                    else if (this.op1.IsReg && RegisterTools.IsSegmentRegister(this.op1.Rn))
                    {
                        value = this.Ctx.MkZeroExt(32 - value.SortSize, value);
                    }
                    BitVecExpr espExpr = this.Get(Rn.ESP);
                    this.RegularUpdate.Set(Rn.ESP, this.Ctx.MkBVSub(espExpr, this.Ctx.MkBV(4, 32)));
                    this.RegularUpdate.SetMem(espExpr, value);
                }
                else if (this._t.Parameters.mode_16bit)
                {
                    BitVecExpr value = this.Op1Value;
                    BitVecExpr spExpr = this.Get(Rn.SP);
                    this.RegularUpdate.Set(Rn.SP, this.Ctx.MkBVSub(spExpr, this.Ctx.MkBV(2, 16)));
                    this.RegularUpdate.SetMem(spExpr, value);
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
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    else if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    else if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    else if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    else if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                }
            }
            public override bool MemReadWriteStatic { get {return true; } }
        }
        /// <summary>Pop off of stack</summary>
        public sealed class Pop : Opcode1Base
        {
            public Pop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.POP, args, Ot1.reg | Ot1.mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": 8-bit operand is not allowed. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                }

                if (this.op1.IsReg && t.Parameters.mode_64bit)
                {
                    Rn reg = this.op1.Rn;
                    if ((reg == Rn.SS) || (reg == Rn.DS) || (reg == Rn.ES))
                    {
                        this.SyntaxError = string.Format("\"{0}\": Invalid register in 64-bit mode. Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                    }
                }
            }
            public override void Execute()
            {
                int operand_Size = this.op1.NBits;
                if (this._t.Parameters.mode_64bit) // stackAddrSize == 64
                {
                    BitVecExpr rspExpr = this.Get(Rn.RSP);
                    BitVecExpr newRspExpr;
                    if (operand_Size == 64)
                    {
                        newRspExpr = this.Ctx.MkBVAdd(rspExpr, this.Ctx.MkBV(8, 64));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newRspExpr, 8));
                    }
                    else if (operand_Size == 16)
                    {
                        newRspExpr = this.Ctx.MkBVAdd(rspExpr, this.Ctx.MkBV(2, 64));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newRspExpr, 2));
                    }
                    else throw new Exception();
                    this.RegularUpdate.Set(Rn.RSP, newRspExpr);
                }
                else if (this._t.Parameters.mode_32bit) // stackAddrSize == 32
                {
                    BitVecExpr espExpr = this.Get(Rn.ESP);
                    BitVecExpr newEspExpr;
                    if (operand_Size == 32)
                    {
                        newEspExpr = this.Ctx.MkBVAdd(espExpr, this.Ctx.MkBV(4, 32));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newEspExpr, 4));
                    }
                    else if (operand_Size == 16)
                    {
                        newEspExpr = this.Ctx.MkBVAdd(espExpr, this.Ctx.MkBV(2, 32));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newEspExpr, 2));
                    }
                    else throw new Exception();
                    this.RegularUpdate.Set(Rn.ESP, newEspExpr);
                }
                else if (this._t.Parameters.mode_16bit)
                {
                    BitVecExpr spExpr = this.Get(Rn.SP);
                    BitVecExpr newSpExpr;
                    if (operand_Size == 32)
                    {
                        newSpExpr = this.Ctx.MkBVAdd(spExpr, this.Ctx.MkBV(4, 16));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newSpExpr, 4));
                    }
                    else if (operand_Size == 16)
                    {
                        newSpExpr = this.Ctx.MkBVAdd(spExpr, this.Ctx.MkBV(2, 16));
                        this.RegularUpdate.Set(this.op1, this.GetMem(newSpExpr, 2));
                    }
                    else throw new Exception();
                    this.RegularUpdate.Set(Rn.SP, newSpExpr);
                }
            }
            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    else if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    else if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    else if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    else if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override bool MemReadWriteStatic { get { return true; } }
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
            public Cwd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CWD, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.DX, this.Ctx.MkExtract(32, 16, this.Ctx.MkSignExt(16, this.Get(Rn.AX))));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.DX; } }
        }
        /// <summary>Convert doubleword to quadword</summary>
        public sealed class Cdq : Opcode0Base
        {
            public Cdq(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CDQ, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.EDX, this.Ctx.MkExtract(64, 32, this.Ctx.MkSignExt(32, this.Get(Rn.EAX))));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.EAX; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.EDX; } }
        }
        /// <summary>Convert quadword to octoword</summary>
        public sealed class Cqo : Opcode0Base
        {
            public Cqo(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CQO, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.RDX, this.Ctx.MkExtract(128, 64, this.Ctx.MkSignExt(64, this.Get(Rn.RAX))));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.RAX; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.RDX; } }
        }
        /// <summary>Convert byte to word</summary>
        public sealed class Cbw : Opcode0Base
        {
            public Cbw(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CBW, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.AX, this.Ctx.MkSignExt(8, this.Get(Rn.AL)));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AL; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.AH; } }
        }
        /// <summary>Convert word to doubleword in EAX register</summary>
        public sealed class Cwde : Opcode0Base
        {
            public Cwde(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CWDE, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.EAX, this.Ctx.MkSignExt(16, this.Get(Rn.AX)));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.AX; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.EAX; } }
        }
        /// <summary>Move and sign extend</summary>
        public sealed class Cdqe : Opcode0Base
        {
            public Cdqe(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CDQE, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Rn.RAX, this.Ctx.MkSignExt(32, this.Get(Rn.EAX)));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { yield return Rn.EAX; } }
            public override IEnumerable<Rn> RegsWriteStatic { get { yield return Rn.RAX; } }
        }

        /// <summary>Move and sign extend</summary>
        public sealed class Movsx : Opcode2Base
        {
            public Movsx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.MOVSX, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 16)
                {
                    if (this.op2.NBits != 8)
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 32)
                {
                    if ((this.op2.NBits != 8) && (this.op2.NBits != 16))
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 64)
                {
                    if ((this.op2.NBits != 8) && (this.op2.NBits != 16))
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1.NBits - this.op2.NBits);
                this.RegularUpdate.Set(this.op1, this.Ctx.MkSignExt(nBitsAdded, this.Op2Value));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Move and sign extend</summary>
        public sealed class Movsxd : Opcode2Base
        {
            public Movsxd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.MOVSXD, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != 64)
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);

                if (this.op2.NBits == 32)
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
            }
            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1.NBits - this.op2.NBits);
                this.RegularUpdate.Set(this.op1, this.Ctx.MkSignExt(nBitsAdded, this.Op2Value));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Move and zero extend</summary>
        public sealed class Movzx : Opcode2Base
        {
            public Movzx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.MOVZX, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 16)
                {
                    if (this.op2.NBits != 8)
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 32)
                {
                    if ((this.op2.NBits != 8) && (this.op2.NBits != 16))
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else if (this.op1.NBits == 64)
                {
                    if ((this.op2.NBits != 8) && (this.op2.NBits != 16))
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                else
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                uint nBitsAdded = (uint)(this.op1.NBits - this.op2.NBits);
                this.RegularUpdate.Set(this.op1, this.Ctx.MkZeroExt(nBitsAdded, this.Op2Value));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        #endregion Data Transfer Instructions

        #region Binary Arithmetic Instructions
        /// <summary>Unsigned integer add with carry, leaves overflow flag unchanged</summary>
        public sealed class Adcx : Opcode2Type1
        {
            public Adcx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ADCX, args, keys, t) { }
            public override void Execute()
            {
                throw new NotImplementedException();
            }
        }
        /// <summary>Unsigned integer add with overflow flag instead of carry flag</summary>
        public sealed class Adox : Opcode2Type1
        {
            public Adox(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ADOX, args, keys, t) { }
            public override void Execute()
            {
                throw new NotImplementedException();
            }
        }
        /// <summary>Integer add</summary>
        public sealed class Add : Opcode2Type1
        {
            public Add(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ADD, args, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Addition(this.Op1Value, this.Op2Value, this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Add with carry</summary>
        public sealed class Adc : Opcode2Type1
        {
            public Adc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ADC, args, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Addition(this.Op1Value, this.Op2Value, this.Get(Flags.CF), this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsReadStatic { get { return Flags.CF; } }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
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
            public Sub(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SUB, args, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Substract(this.Op1Value, this.Op2Value, this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Subtract with borrow</summary>
        public sealed class Sbb : Opcode2Type1
        {
            public Sbb(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SBB, args, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Substract(this.Op1Value, this.Op2Value, this.Get(Flags.CF), this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsReadStatic { get { return Flags.CF; } }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Signed multiply</summary>
        public sealed class Imul : OpcodeNBase
        {
            public Imul(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.IMUL, args, 3, keys, t)
            {
                if (this.IsHalted) return;
                switch (this.NOperands)
                {
                    case 1:
                        {
                            Ot1 allowedOperands1 = Ot1.reg | Ot1.mem;
                            if (!allowedOperands1.HasFlag(this.op1.Type))
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}). Allowed types: {4}.",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, AsmSourceTools.ToString(allowedOperands1));
                            }
                            break;
                        }
                    case 2:
                        {
                            Ot2 allowedOperands2 = Ot2.reg_reg | Ot2.reg_mem;
                            if (!allowedOperands2.HasFlag(AsmSourceTools.MergeOt(this.op1.Type, this.op2.Type)))
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}). Allowed types: {7}.",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, AsmSourceTools.ToString(allowedOperands2));
                            }
                            if (this.op1.NBits == 8)
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand 1 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}).",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                            }
                            if (this.op1.NBits != this.op2.NBits)
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 cannot have different number of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}).",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                            }
                            break;
                        }
                    case 3:
                        {
                            Ot3 allowedOperands3 = Ot3.reg_reg_imm | Ot3.reg_mem_imm;
                            if (!allowedOperands3.HasFlag(AsmSourceTools.MergeOt(this.op1.Type, this.op2.Type, this.op3.Type)))
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={6} ({7}, bits={8}) Allowed types: {9}.",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits, AsmSourceTools.ToString(allowedOperands3));
                            }
                            if (this.op1.NBits == 8)
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand 1 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}).",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                            }
                            if (this.op1.NBits != this.op2.NBits)
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 cannot have different number of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}).",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                            }

                            if (this.op3.NBits < this.op1.NBits)
                            {
                                this.op3.SignExtend(Math.Min(this.op1.NBits, 32));
                            }
                            if (((this.op1.NBits == 16) && (this.op3.NBits == 8)) ||
                                ((this.op1.NBits == 32) && (this.op3.NBits == 8)) ||
                                ((this.op1.NBits == 64) && (this.op3.NBits == 8)) ||
                                ((this.op1.NBits == 16) && (this.op3.NBits == 16)) ||
                                ((this.op1.NBits == 32) && (this.op3.NBits == 32)) ||
                                ((this.op1.NBits == 64) && (this.op3.NBits == 32)))
                            {
                                // ok
                            }
                            else
                            {
                                this.SyntaxError = string.Format("\"{0}\": Operand 1 and 3 cannot have the provided combination of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6}); op3={7} ({8}, bits={9}).",
                                    this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);

                            }
                            break;
                        }
                    default:
                        this.SyntaxError = string.Format("\"{0}\": Expected 1 or 2 or 3 operands. Found {1} operand(s) with value \"{2}\".", this.ToString(), this.NOperands, string.Join(", ", args));
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
                Context ctx = this.Ctx;
                BoolExpr cf;

                uint nBits = (uint)this.op1.NBits;

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
                                default: throw new Exception();
                            }
                            break;
                        }
                    case 2:
                        {
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Op1Value), ctx.MkSignExt(nBits, this.Op2Value));
                            BitVecExpr truncatedValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr signExtendedTruncatedValue = ctx.MkSignExt(nBits, truncatedValue);
                            this.RegularUpdate.Set(this.op1.Rn, truncatedValue);
                            cf = ctx.MkNot(ctx.MkEq(signExtendedTruncatedValue, newValue));
                            break;
                        }
                    case 3:
                        {
                            this.op3.SignExtend((int)nBits); // sign extend the imm
                            BitVecExpr newValue = ctx.MkBVMul(ctx.MkSignExt(nBits, this.Op2Value), ctx.MkSignExt(nBits, this.Op3Value));
                            BitVecExpr truncatedValue = ctx.MkExtract(nBits - 1, 0, newValue);
                            BitVecExpr signExtendedTruncatedValue = ctx.MkSignExt(nBits, truncatedValue);
                            this.RegularUpdate.Set(this.op1.Rn, truncatedValue);
                            cf = ctx.MkNot(ctx.MkEq(signExtendedTruncatedValue, newValue));
                            break;
                        }
                    default: throw new Exception();
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
                        if (this.op1 == null) yield break;
                        switch (this.op1.NBits)
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
                            default: throw new Exception();
                        }
                        foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                    }
                    else
                    {
                        foreach (Rn r in ToRegEnumerable(this.op1, this.op2)) yield return r;
                    }
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1 == null) yield break;
                    if (this.NOperands == 1)
                    {
                        switch (this.op1.NBits)
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
                            default: throw new Exception();
                        }
                    }
                    else
                    {
                        foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                    }
                }
            }
        }
        /// <summary>Unsigned multiply</summary>
        public sealed class Mul : Opcode1Base
        {
            public Mul(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.MUL, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                BoolExpr cf;
                uint nBits = (uint)this.op1.NBits;

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
                    default: throw new Exception();
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
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                }
            }
        }
        /// <summary>Signed divide</summary>
        public sealed class Idiv : Opcode1Base
        {
            public Idiv(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.DIV, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                BitVecExpr term1;
                BitVecExpr maxValue;

                switch (this.op1.NBits)
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
                    default: throw new Exception();
                }

                BitVecExpr op1Value = this.Op1Value;

                BitVecExpr term2 = ctx.MkSignExt(nBits, op1Value);
                BitVecExpr quotient = ctx.MkBVSDiv(term1, term2);
                BitVecExpr remainder = ctx.MkBVSRem(term1, term2);

                //Console.WriteLine("op1Value=" + op1Value + "; term1=" + term1 + "; term2=" + term2 + "; quotient=" + quotient + "; remainder=" + remainder);

                BoolExpr op1IsZero = ctx.MkEq(op1Value, ctx.MkBV(0, nBits));
                BoolExpr quotientTooLarge = ctx.MkBVSGT(quotient, maxValue);
                BoolExpr DE_Excepton = ctx.MkOr(op1IsZero, quotientTooLarge);

                switch (this.op1.NBits)
                {
                    case 8:
                        this.RegularUpdate.Set(Rn.AL, ctx.MkITE(DE_Excepton, this.Undef(Rn.AL), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.AH, ctx.MkITE(DE_Excepton, this.Undef(Rn.AH), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 16:
                        this.RegularUpdate.Set(Rn.AX, ctx.MkITE(DE_Excepton, this.Undef(Rn.AX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.DX, ctx.MkITE(DE_Excepton, this.Undef(Rn.DX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 32:
                        this.RegularUpdate.Set(Rn.EAX, ctx.MkITE(DE_Excepton, this.Undef(Rn.EAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.EDX, ctx.MkITE(DE_Excepton, this.Undef(Rn.EDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 64:
                        this.RegularUpdate.Set(Rn.RAX, ctx.MkITE(DE_Excepton, this.Undef(Rn.RAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.RDX, ctx.MkITE(DE_Excepton, this.Undef(Rn.RDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    default: throw new Exception();
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
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                }
            }
        }
        /// <summary>Unsigned divide</summary>
        public sealed class Div : Opcode1Base
        {
            public Div(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.DIV, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
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
                    default: throw new Exception();
                }

                BitVecExpr op1Value = this.Op1Value;

                BitVecExpr term2 = ctx.MkZeroExt(nBits, op1Value);
                BitVecExpr quotient = ctx.MkBVUDiv(term1, term2);
                BitVecExpr remainder = ctx.MkBVURem(term1, term2);

                Console.WriteLine("op1Value=" + op1Value + "; term1=" + term1 + "; term2=" + term2 + "; quotient=" + quotient + "; remainder=" + remainder);

                BoolExpr op1IsZero = ctx.MkEq(op1Value, ctx.MkBV(0, nBits));
                BoolExpr quotientTooLarge = ctx.MkBVUGT(quotient, maxValue);
                BoolExpr DE_Excepton = ctx.MkOr(op1IsZero, quotientTooLarge);

                switch (nBits)
                {
                    case 8:
                        BitVecExpr al = ctx.MkITE(DE_Excepton, this.Undef(Rn.AL), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr;
                        BitVecExpr ah = ctx.MkITE(DE_Excepton, this.Undef(Rn.AH), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr;
                        this.RegularUpdate.Set(Rn.AX, ctx.MkConcat(ah, al));
                        break;
                    case 16:
                        this.RegularUpdate.Set(Rn.AX, ctx.MkITE(DE_Excepton, this.Undef(Rn.AX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.DX, ctx.MkITE(DE_Excepton, this.Undef(Rn.DX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 32:
                        this.RegularUpdate.Set(Rn.EAX, ctx.MkITE(DE_Excepton, this.Undef(Rn.EAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.EDX, ctx.MkITE(DE_Excepton, this.Undef(Rn.EDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    case 64:
                        this.RegularUpdate.Set(Rn.RAX, ctx.MkITE(DE_Excepton, this.Undef(Rn.RAX), ctx.MkExtract(nBits - 1, 0, quotient)) as BitVecExpr);
                        this.RegularUpdate.Set(Rn.RDX, ctx.MkITE(DE_Excepton, this.Undef(Rn.RDX), ctx.MkExtract(nBits - 1, 0, remainder)) as BitVecExpr);
                        break;
                    default: throw new Exception();
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
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this.op1 == null) yield break;
                    switch (this.op1.NBits)
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
                        default: throw new Exception();
                    }
                }
            }
        }
        /// <summary>Increment</summary>
        public sealed class Inc : Opcode1Base
        {
            public Inc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.INC, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Addition(this.Op1Value, this.Ctx.MkBV(1, (uint)this.op1.NBits), this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                //CF is not updated!
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Decrement</summary>
        public sealed class Dec : Opcode1Base
        {
            public Dec(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.DEC, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Substract(this.Op1Value, this.Ctx.MkBV(1, (uint)this.op1.NBits), this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                //CF is not updated!
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Negate</summary>
        public sealed class Neg : Opcode1Base
        {
            public Neg(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.NEG, args, Ot1.reg | Ot1.mem, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Neg(this.Op1Value, this.Ctx);
                this.RegularUpdate.Set(this.op1, tup.result);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        /// <summary>Compare</summary>
        public sealed class Cmp : Opcode2Type1
        {
            public Cmp(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CMP, args, keys, t) { }
            public override void Execute()
            {
                var tup = BitOperations.Substract(this.Op1Value, this.Op2Value, this.Ctx);
                this.RegularUpdate.Set(Flags.CF, tup.cf);
                this.RegularUpdate.Set(Flags.OF, tup.of);
                this.RegularUpdate.Set(Flags.AF, tup.af);
                this.RegularUpdate.Set_SF_ZF_PF(tup.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
        }
        #endregion Binary Arithmetic Instructions

        #region Decimal Arithmetic Instructions
        //DAA,// Decimal adjust after addition
        //DAS,// Decimal adjust after subtraction
        //AAA,// ASCII adjust after addition
        //AAS,// ASCII adjust after subtraction
        //AAM,// ASCII adjust after multiplication
        //AAD,// ASCII adjust before division
        #endregion Decimal Arithmetic Instructions

        #region Logical Instructions

        public abstract class LogicalBase : Opcode2Base
        {
            public LogicalBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t)
                : base(mnemonic, args, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.IsImm)
                {
                    if (this.op1.NBits < this.op2.NBits)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 1 should smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                    if ((this.op1.NBits == 64) && (this.op2.NBits == 32))
                    {
                        this.op2.SignExtend(this.op1.NBits);
                    }
                    else if (this.op2.NBits == 8)
                    {
                        this.op2.SignExtend(this.op1.NBits);
                    }
                    else if (this.op2.NBits < this.op1.NBits)
                    {
                        this.op2.ZeroExtend(this.op1.NBits);
                    }
                }
                else if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operands should have equal sizes. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                BitVecExpr value;
                switch (this._mnemonic)
                {
                    case Mnemonic.XOR: value = this.Ctx.MkBVXOR(this.Op1Value, this.Op2Value); break;
                    case Mnemonic.AND: value = this.Ctx.MkBVAND(this.Op1Value, this.Op2Value); break;
                    case Mnemonic.OR: value = this.Ctx.MkBVOR(this.Op1Value, this.Op2Value); break;
                    default: throw new Exception();
                }
                this.RegularUpdate.Set(this.op1, value);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(value);
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Xor : LogicalBase
        {
            public Xor(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.XOR, args, keys, t) { }
        }
        public sealed class And : LogicalBase
        {
            public And(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.AND, args, keys, t) { }
        }
        public sealed class Or : LogicalBase
        {
            public Or(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.OR, args, keys, t) { }
        }

        public sealed class Not : Opcode1Base
        {
            public Not(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.NOT, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(this.op1, this.Ctx.MkBVNot(this.Op1Value));
                // Flags are unaffected
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Test : Opcode2Base
        {
            public Test(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.TEST, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.IsImm)
                {
                    if (this.op1.NBits < this.op2.NBits)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 1 should smaller or equal than operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                    if (this.op2.NBits < this.op1.NBits)
                    {
                        this.op2.SignExtend(this.op1.NBits);
                    }
                }
                else if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operands should have equal sizes. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                BitVecExpr value = this.Ctx.MkBVAND(this.Op1Value, this.Op2Value);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set_SF_ZF_PF(value);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
        }

        #endregion Logical Instructions

        #region Shift and Rotate Instructions

        public abstract class ShiftRotateBase : Opcode2Base
        {
            public ShiftRotateBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.IsReg && (this.op2.Rn != Rn.CL))
                {
                    this.SyntaxError = string.Format("\"{0}\": If operand 2 is a registers, only GPR cl is allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                if (this.Op2Value.SortSize != 8)
                {
                    this.Warning = string.Format("\"{0}\": value of operand 2 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public static (BitVecExpr shiftCount, BoolExpr tooLarge) GetShiftCount(BitVecExpr value, int nBits, Context ctx)
            {
                Debug.Assert(value.SortSize == 8);
                BitVecNum shiftMask = ctx.MkBV((nBits == 64) ? 0x3F : 0x1F, 8);
                BoolExpr tooLarge = ctx.MkBVSGE(value, ctx.MkBV((nBits == 64) ? 64 : 32, 8));
                BitVecExpr shiftCount = ctx.MkBVAND(value, shiftMask);
                return (shiftCount: shiftCount, tooLarge: tooLarge);
            }
            public void UpdateFlagsShift(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, BoolExpr shiftTooLarge, bool left)
            {
                ShiftRotateBase.UpdateFlagsShift(value, cfIn, shiftCount, shiftTooLarge, left, this.keys.PrevKey, this.RegularUpdate, this._t);
            }
            public static void UpdateFlagsShift(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, BoolExpr shiftTooLarge, bool left, string prevKey, StateUpdate stateUpdate, Tools t)
            {
                Context ctx = t.Ctx;
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

                BoolExpr of =
                    ctx.MkITE(shiftTooLarge, OpcodeBase.Undef(Flags.OF, t),
                        ctx.MkITE(isZero, OpcodeBase.Get(Flags.OF, prevKey, ctx),
                            ctx.MkITE(isOne, of_tmp, OpcodeBase.Undef(Flags.OF, t)))) as BoolExpr;
                #endregion

                #region Set the Flags
                stateUpdate.Set(Flags.OF, of);
                stateUpdate.Set(Flags.CF, ctx.MkITE(shiftTooLarge, OpcodeBase.Undef(Flags.CF, t), ctx.MkITE(isZero, OpcodeBase.Get(Flags.CF, prevKey, ctx), cfIn)) as BoolExpr);
                stateUpdate.Set(Flags.PF, ctx.MkITE(isZero, OpcodeBase.Get(Flags.PF, prevKey, ctx), ToolsFlags.Create_PF(value, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.ZF, ctx.MkITE(isZero, OpcodeBase.Get(Flags.ZF, prevKey, ctx), ToolsFlags.Create_ZF(value, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.SF, ctx.MkITE(isZero, OpcodeBase.Get(Flags.SF, prevKey, ctx), ToolsFlags.Create_SF(value, value.SortSize, ctx)) as BoolExpr);
                stateUpdate.Set(Flags.AF, ctx.MkITE(isZero, OpcodeBase.Get(Flags.AF, prevKey, ctx), OpcodeBase.Undef(Flags.AF, t)) as BoolExpr);
                #endregion

            }
            public void UpdateFlagsRotate(BitVecExpr value, BoolExpr cfIn, BitVecExpr shiftCount, bool left)
            {

                /* The OF flag is defined only for the 1-bit rotates; it is undefined in all other 
                 * cases (except RCL and RCR instructions only: a zero - bit rotate does nothing, that
                 * is affects no flags). For left rotates, the OF flag is set to the exclusive OR of 
                 * the CF bit(after the rotate) and the most-significant bit of the result. For 
                 * right rotates, the OF flag is set to the exclusive OR of the two most-significant 
                 * bits of the result.
                 */

                Context ctx = this.Ctx;

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
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        #region Shift
        ///<summary>Shift arithmetic right</summary>
        public sealed class Sar : ShiftRotateBase
        {
            public Sar(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SAR, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SAR, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsShift(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, shiftCount.tooLarge, false);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }
        /// <summary>Shift arithmetic left</summary>
        public sealed class Sal : ShiftRotateBase
        {
            public Sal(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SAL, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SAL, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsShift(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, shiftCount.tooLarge, true);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }
        ///<summary>Shift logical right</summary>
        public sealed class Shr : ShiftRotateBase
        {
            public Shr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHR, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SHR, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsShift(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, shiftCount.tooLarge, false);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }
        /// <summary>Shift logical left</summary>
        public sealed class Shl : ShiftRotateBase
        {
            public Shl(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHL, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SHL, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsShift(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, shiftCount.tooLarge, true);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
        }
        #endregion Shift

        #region Rotate
        /// <summary>Rotate right</summary>
        public sealed class Ror : ShiftRotateBase
        {
            public Ror(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ROR, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.ROR, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsRotate(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, false);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }
        /// <summary>Rotate through carry right</summary>
        public sealed class Rcr : ShiftRotateBase
        {
            public Rcr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.RCR, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.RCR, this.Op1Value, shiftCount.shiftCount, this.Get(Flags.CF), this.keys.PrevKey, this._t);
                this.UpdateFlagsRotate(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, false);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsReadStatic { get { return Flags.CF; } }
            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }
        /// <summary>Rotate through carry left</summary>
        public sealed class Rcl : ShiftRotateBase
        {
            public Rcl(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.RCL, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.RCL, this.Op1Value, shiftCount.shiftCount, this.Get(Flags.CF), this.keys.PrevKey, this._t);
                this.UpdateFlagsRotate(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, true);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsReadStatic { get { return Flags.CF; } }
            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }
        /// <summary>Rotate left</summary>
        public sealed class Rol : ShiftRotateBase
        {
            public Rol(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ROL, args, keys, t) { }
            public override void Execute()
            {
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx);
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.ROL, this.Op1Value, shiftCount.shiftCount, this._t);
                this.UpdateFlagsRotate(shiftValue.result, shiftValue.cf, shiftCount.shiftCount, true);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF | Flags.OF; } }
        }
        #endregion Rotate

        #region Shift/Rotate X (no flags updates)
        public abstract class ShiftBaseX : Opcode3Base
        {
            public ShiftBaseX(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot3.reg_reg_imm | Ot3.reg_mem_imm, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 should have equal size. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                if (this.Op3Value.SortSize != 8)
                {
                    this.Warning = string.Format("\"{0}\": value of operand 3 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Rorx : ShiftBaseX
        {
            public Rorx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.RORX, args, keys, t) { }
            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx).shiftCount;
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.ROR, this.Op1Value, shiftCount, this._t);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
        }
        public sealed class Sarx : ShiftBaseX
        {
            public Sarx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SARX, args, keys, t) { }
            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx).shiftCount;
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SAR, this.Op1Value, shiftCount, this._t);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
        }
        public sealed class Shlx : ShiftBaseX
        {
            public Shlx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHLX, args, keys, t) { }
            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx).shiftCount;
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SHL, this.Op1Value, shiftCount, this._t);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
        }
        public sealed class Shrx : ShiftBaseX
        {
            public Shrx(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHRX, args, keys, t) { }
            public override void Execute()
            {
                BitVecExpr shiftCount = ShiftRotateBase.GetShiftCount(this.Op2Value, this.op1.NBits, this.Ctx).shiftCount;
                var shiftValue = BitOperations.ShiftOperations(Mnemonic.SHR, this.Op1Value, shiftCount, this._t);
                this.RegularUpdate.Set(this.op1, shiftValue.result);
            }
        }
        #endregion  Shift/Rotate X (no flags updates)

        #region Shift Double

        public abstract class ShiftDoubleBase : Opcode3Base
        {
            public ShiftDoubleBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot3.reg_reg_imm | Ot3.reg_reg_reg | Ot3.mem_reg_imm | Ot3.mem_reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if ((this.op1.NBits == 8) || (this.op2.NBits == 8))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                }
                if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Number of bits of operand 1 and 2 should be equal. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                }
                if (this.op3.IsReg && (this.op3.Rn != Rn.CL))
                {
                    this.SyntaxError = string.Format("\"{0}\": If operand 3 is a registers, only GPR cl is allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                }
                if (this.Op3Value.SortSize != 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": value of operand 3 does not fit in 8-bit field. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6};  op3={7} ({8}, bits={9})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits, this.op3, this.op3.Type, this.op3.NBits);
                }
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2, this.op3); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        /// <summary>Shift right double</summary>
        public sealed class Shrd : ShiftDoubleBase
        {
            public Shrd(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHRD, args, keys, t) { }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                var shiftCount = ShiftRotateBase.GetShiftCount(this.Op3Value, (int)nBits, ctx);
                BitVecExpr nShifts = shiftCount.shiftCount;
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

                ShiftRotateBase.UpdateFlagsShift(value_out, cf, shiftCount.shiftCount, shiftCount.tooLarge, false, this.keys.PrevKey, this.RegularUpdate, this._t);
                this.RegularUpdate.Set(this.op1, value_out);
            }
        }
        /// <summary>Shift left double</summary>
        public sealed class Shld : ShiftDoubleBase
        {
            public Shld(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SHLD, args, keys, t) { }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                var shiftTup = ShiftRotateBase.GetShiftCount(this.Op3Value, (int)nBits, ctx);
                BitVecExpr nShifts = shiftTup.shiftCount;
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

                ShiftRotateBase.UpdateFlagsShift(value_out, cf, shiftTup.shiftCount, shiftTup.tooLarge, true, this.keys.PrevKey, this.RegularUpdate, this._t);
                this.RegularUpdate.Set(this.op1, value_out);
            }
        }
        #endregion Shift Double

        #endregion Shift and Rotate Instructions

        #region Bit and Byte Instructions

        public sealed class Setcc : Opcode1Base
        {
            private readonly ConditionalElement _ce;
            public Setcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot1.reg | Ot1.mem, keys, t)
            {
                this._ce = ce;
                if (this.IsHalted) return;
                if (this.op1.NBits != 8)
                {
                    this.SyntaxError = string.Format("Invalid operands size. Operands can only have size 8. Operand1={0}", this.op1);
                }
            }
            public override void Execute()
            {
                BoolExpr conditional = ToolsAsmSim.ConditionalTaken(this._ce, this.keys.PrevKey, this.Ctx);
                BitVecExpr result = this.Ctx.MkITE(conditional, this.Ctx.MkBV(0, 8), this.Ctx.MkBV(1, 8)) as BitVecExpr;
                this.RegularUpdate.Set(this.op1, result);
            }
            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this._ce); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        public abstract class BitTestBase : Opcode2Base
        {
            public BitTestBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot2.mem_imm | Ot2.mem_reg | Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.IsImm)
                {
                    if (this.op2.NBits != 8)
                    {
                        this.SyntaxError = string.Format("Operand 2 is imm and should have 8 bits. Operand1={0} ({1}, bits={2}); Operand2={3} ({4}, bits={5})", this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }
                else
                {
                    if (this.op1.NBits != this.op2.NBits)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand 1 and 2 should have same number of bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }
            }
            private BitVecExpr GetBitPos(BitVecExpr value, uint nBits)
            {
                BitVecNum mask = this.Ctx.MkBV((nBits == 64) ? 0x3F : 0x1F, nBits);
                return this.Ctx.MkBVAND(value, mask);
            }
            protected void SetBitValue(Mnemonic opcode)
            {
                //Debug.Assert(this.Op1Value.SortSize == this.Op2Value.SortSize, "nBits op1 = " + this.Op1Value.SortSize +"; nBits op2 = "+ this.Op2Value.SortSize);

                uint nBits = (uint)this.op1.NBits;
                BitVecExpr bitPos = this.GetBitPos(this.Op2Value, nBits);
                BitVecExpr mask = this.Ctx.MkBVSHL(this.Ctx.MkBV(1, nBits), bitPos);
                BitVecExpr mask_INV = this.Ctx.MkBVNeg(mask);
                BitVecExpr v1 = this.Op1Value;

                switch (opcode)
                {
                    case Mnemonic.BTC:
                        {
                            BitVecExpr bitSet = this.Ctx.MkBVOR(v1, mask);
                            BitVecExpr bitCleared = this.Ctx.MkBVAND(v1, mask_INV);
                            BoolExpr bitSetAtPos = ToolsZ3.GetBit(v1, bitPos, this.Ctx);
                            BitVecExpr value = this.Ctx.MkITE(bitSetAtPos, bitCleared, bitSet) as BitVecExpr;
                            this.RegularUpdate.Set(this.op1, value);
                        }
                        break;
                    case Mnemonic.BTS:
                        {
                            BitVecExpr bitSet = this.Ctx.MkBVOR(v1, mask);
                            this.RegularUpdate.Set(this.op1, bitSet);
                        }
                        break;
                    case Mnemonic.BTR:
                        {
                            BitVecExpr bitCleared = this.Ctx.MkBVAND(v1, mask_INV);
                            this.RegularUpdate.Set(this.op1, bitCleared);
                        }
                        break;
                    case Mnemonic.BT:
                        break;
                    default:
                        throw new NotImplementedException();
                }
                // zero flag is unaffected
                this.RegularUpdate.Set(Flags.OF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.SF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.AF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.PF, Tv.UNDEFINED);
                this.RegularUpdate.Set(Flags.CF, ToolsZ3.GetBit(v1, bitPos, this.Ctx));
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
        }

        public sealed class Bt_Opcode : BitTestBase
        {
            public Bt_Opcode(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BT, args, keys, t) { }
            public override void Execute() { this.SetBitValue(Mnemonic.BT); }
        }
        public sealed class Bts : BitTestBase
        {
            public Bts(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BTS, args, keys, t) { }
            public override void Execute() { this.SetBitValue(Mnemonic.BTS); }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Btr : BitTestBase
        {
            public Btr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BTR, args, keys, t) { }
            public override void Execute() { this.SetBitValue(Mnemonic.BTR); }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Btc : BitTestBase
        {
            public Btc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BTC, args, keys, t) { }
            public override void Execute() { this.SetBitValue(Mnemonic.BTC); }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        public sealed class Bsf : Opcode2Base
        {
            public Bsf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BSF, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 should be equal to operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                if (this.op1.NBits == 8)
                    this.SyntaxError = string.Format("\"{0}\": Operands cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
            }
            private BitVecExpr MakeBsfExpr(uint nBits, uint pos, BitVecExpr sourceOperand, BitVecNum one)
            {
                BitVecExpr result;
                if (pos == nBits - 1)
                {
                    result = this.Ctx.MkBV(pos, 6);
                }
                else
                {
                    BitVecExpr expr1 = this.MakeBsfExpr(nBits, pos + 1, sourceOperand, one);
                    result = this.Ctx.MkITE(ToolsZ3.GetBit(sourceOperand, pos, one, this.Ctx), this.Ctx.MkBV(pos, 6), expr1) as BitVecExpr;
                }
                return result;
            }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                {
                    BitVecNum one = ctx.MkBV(1, 1);
                    BitVecExpr answer = ctx.MkConcat(ctx.MkBV(0, nBits - 6), this.MakeBsfExpr(nBits, 0, this.Op2Value, one));
                    Debug.Assert(answer.SortSize == nBits);
                    BitVecExpr expr_Fresh = this.Undef(this.op1.Rn);
                    BitVecExpr expr = ctx.MkITE(ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)), expr_Fresh, answer) as BitVecExpr;
                    this.RegularUpdate.Set(this.op1, expr);
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
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        public sealed class Bsr : Opcode2Base
        {
            public Bsr(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.BSR, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 should be equal to operand 2. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                if (this.op1.NBits == 8)
                    this.SyntaxError = string.Format("\"{0}\": Operands cannot be 8-bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
            }
            private BitVecExpr MakeBsrExpr(uint nBits, uint pos, BitVecExpr sourceOperand, BitVecNum one)
            {
                BitVecExpr result;
                if (pos == 1)
                {
                    result = this.Ctx.MkBV(0, 6);
                }
                else
                {
                    BitVecExpr expr1 = this.MakeBsrExpr(nBits, pos - 1, sourceOperand, one);
                    result = this.Ctx.MkITE(ToolsZ3.GetBit(sourceOperand, pos - 1, one, this.Ctx), this.Ctx.MkBV(pos - 1, 6), expr1) as BitVecExpr;
                }
                return result;
            }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                {
                    BitVecNum one = ctx.MkBV(1, 1);
                    BitVecExpr answer = ctx.MkConcat(ctx.MkBV(0, nBits - 6), this.MakeBsrExpr(nBits, nBits, this.Op2Value, one));
                    Debug.Assert(answer.SortSize == nBits);
                    BitVecExpr expr_Fresh = this.Undef(this.op1.Rn);
                    BitVecExpr expr = ctx.MkITE(ctx.MkEq(this.Op2Value, ctx.MkBV(0, nBits)), expr_Fresh, answer) as BitVecExpr;
                    this.RegularUpdate.Set(this.op1, expr);
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
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        #endregion

        #region Control Transfer Instructions
        public abstract class OpcodeJumpBase : Opcode1Base
        {
            public OpcodeJumpBase(Mnemonic mnemonic, string[] args, Ot1 allowedOperands1, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, allowedOperands1, keys, t) { }
            protected abstract BoolExpr Jump { get; }
            public int LineNumber
            {
                get
                {
                    int lineNumber = -1;
                    switch (this.op1.Type)
                    {
                        case Ot1.reg:
                            this.SyntaxError = "WARNING: OpcodeJumpBase: jumping based on registry value is not supported.";
                            //throw new Exception();
                            break;
                        case Ot1.mem:
                            // unsupported
                            this.SyntaxError = "WARNING: OpcodeJumpBase: jumping based on memory value is not supported.";
                            //throw new Exception();
                            break;
                        case Ot1.imm: // assuming the imm is an line number
                            lineNumber = (int)this.op1.Imm;
                            break;
                        case Ot1.UNKNOWN: // assuming it is a string with a line number in it.
                            lineNumber = ToolsZ3.GetLineNumberFromLabel(this.op1.ToString(), StaticFlow.LINENUMBER_SEPARATOR);
                            break;
                        default:
                            throw new Exception();
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
                    this.CreateBranchUpdate();
                    //this.BranchUpdate.Add(new BranchInfo(jumpConditional, true));
                }
                else if (jumpConditional.IsFalse)
                {
                    //this.RegularUpdate.Add(new BranchInfo(jumpConditional, false));
                    //this.BranchUpdate is not updated
                    this.CreateRegularUpdate();
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
            public Jmp(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.JMP, args, Ot1.imm | Ot1.mem | Ot1.reg | Ot1.UNKNOWN, keys, t) { }
            protected sealed override BoolExpr Jump
            {
                get { return this._t.Ctx.MkTrue(); }
            }
        }
        public sealed class Jmpcc : OpcodeJumpBase
        {
            private readonly ConditionalElement _ce;
            public Jmpcc(Mnemonic mnemonic, string[] args, ConditionalElement ce, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot1.imm | Ot1.UNKNOWN, keys, t)
            {
                this._ce = ce;
            }
            protected sealed override BoolExpr Jump { get { return ToolsAsmSim.ConditionalTaken(this._ce, this.keys.PrevKey, this.Ctx); } }
            public override Flags FlagsReadStatic { get { return ToolsAsmSim.FlagsUsed(this._ce); } }
        }

        #region Loop
        public abstract class OpcodeLoopBase : OpcodeJumpBase
        {
            public OpcodeLoopBase(Mnemonic mnemonic, string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(mnemonic, args, Ot1.UNKNOWN, keys, t) { }
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
            public Loop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LOOP, args, keys, t) { }
            protected sealed override BoolExpr Jump { get { return this.Ctx.MkEq(this.Get(Rn.ECX), this.Ctx.MkBV(0, 32)); } }
        }
        public sealed class Loopz : OpcodeLoopBase
        {
            public Loopz(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LOOPZ, args, keys, t) { }
            protected sealed override BoolExpr Jump { get { return this.Ctx.MkAnd(this.Ctx.MkEq(this.Get(Rn.ECX), this.Ctx.MkBV(0, 32)), this.Get(Flags.ZF)); } }
            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }
        public sealed class Loope : OpcodeLoopBase
        {
            public Loope(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LOOPE, args, keys, t) { }
            protected sealed override BoolExpr Jump { get { return this.Ctx.MkAnd(this.Ctx.MkEq(this.Get(Rn.ECX), this.Ctx.MkBV(0, 32)), this.Get(Flags.ZF)); } }
            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }
        public sealed class Loopnz : OpcodeLoopBase
        {
            public Loopnz(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LOOPNZ, args, keys, t) { }
            protected sealed override BoolExpr Jump { get { return this.Ctx.MkAnd(this.Ctx.MkEq(this.Get(Rn.ECX), this.Ctx.MkBV(0, 32)), this.Ctx.MkNot(this.Get(Flags.ZF))); } }
            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }
        public sealed class Loopne : OpcodeLoopBase
        {
            public Loopne(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LOOPNE, args, keys, t) { }
            protected sealed override BoolExpr Jump { get { return this.Ctx.MkAnd(this.Ctx.MkEq(this.Get(Rn.ECX), this.Ctx.MkBV(0, 32)), this.Ctx.MkNot(this.Get(Flags.ZF))); } }
            public override Flags FlagsReadStatic { get { return Flags.ZF; } }
        }
        #endregion Loop

        public sealed class Call : Opcode1Base
        {
            public Call(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CALL, args, Ot1.imm | Ot1.mem | Ot1.reg | Ot1.UNKNOWN, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                }
            }
            public int LineNumber //TODO consider using BitVecExpr as lineNumber
            {
                get
                {
                    int lineNumber = -1;
                    switch (this.op1.Type)
                    {
                        case Ot1.reg:
                            this.SyntaxError = "WARNING: Call: jumping based on registry value is not supported.";
                            throw new Exception();
                        case Ot1.mem:
                            // unsupported
                            this.SyntaxError = "WARNING: Call: jumping based on memory value is not supported.";
                            throw new Exception();
                        case Ot1.imm: // assuming the imm is an line number
                            lineNumber = (int)this.op1.Imm;
                            break;
                        case Ot1.UNKNOWN: // assuming it is a string with a line number in it.
                            lineNumber = ToolsZ3.GetLineNumberFromLabel(this.op1.ToString(), StaticFlow.LINENUMBER_SEPARATOR);
                            break;
                        default:
                            throw new Exception();
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
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                    foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override bool MemReadWriteStatic { get { return true; } }
        }
        public sealed class Ret : OpcodeNBase
        {
            public Ret(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.RET, args, 1, keys, t)
            {
                if (this.IsHalted) return;
                if (this.NOperands == 1)
                {
                    if (this.op1.IsImm)
                    {
                        if (this.op1.NBits != 16)
                        {
                            this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                        }
                    }
                    else
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits);
                    }
                }
            }
            public override void Execute()
            {
                BitVecExpr nextLineNumberExpr;

                if (this._t.Parameters.mode_64bit)
                {
                    BitVecExpr newRspExpr = this.Ctx.MkBVSub(this.Get(Rn.RSP), this.Ctx.MkBV(8, 64));
                    nextLineNumberExpr = this.GetMem(newRspExpr, 8);
                    this.RegularUpdate.Set(Rn.RSP, newRspExpr);
                }
                else if (this._t.Parameters.mode_32bit)
                {
                    BitVecExpr newEspExpr = this.Ctx.MkBVSub(this.Get(Rn.ESP), this.Ctx.MkBV(4, 32));
                    nextLineNumberExpr = this.GetMem(newEspExpr, 4);
                    this.RegularUpdate.Set(Rn.ESP, newEspExpr);
                }
                else if (this._t.Parameters.mode_16bit)
                {
                    BitVecExpr newSpExpr = this.Ctx.MkBVSub(this.Get(Rn.SP), this.Ctx.MkBV(2, 16));
                    nextLineNumberExpr = this.GetMem(newSpExpr, 2);
                    this.RegularUpdate.Set(Rn.SP, newSpExpr);
                }
                else throw new Exception();

                this.RegularUpdate.NextLineNumberExpr = nextLineNumberExpr;
            }
            public override IEnumerable<Rn> RegsReadStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic
            {
                get
                {
                    if (this._t.Parameters.mode_64bit) yield return Rn.RSP;
                    if (this._t.Parameters.mode_32bit) yield return Rn.ESP;
                    if (this._t.Parameters.mode_16bit) yield return Rn.SP;
                    if (this.op1 != null) foreach (Rn r in ToRegEnumerable(this.op1)) yield return r;
                }
            }
            public override bool MemReadWriteStatic { get { return true; } }
        }
        #endregion Control Transfer Instructions

        #region String Instructions
        //The string instructions operate on strings of bytes, allowing them to be moved to and from memory.
        //MOVS,
        //MOVSB,// Move string/Move byte string
        //MOVSW,// Move string/Move word string
        //MOVSD,// Move string/Move doubleword string
        //CMPS,
        //CMPSB,// Compare string/Compare byte string
        //CMPSW,// Compare string/Compare word string
        //CMPSD,// Compare string/Compare doubleword string
        //SCAS,
        //SCASB,// Scan string/Scan byte string
        //SCASW,// Scan string/Scan word string
        //SCASD,// Scan string/Scan doubleword string
        //LODS,
        //LODSB,// Load string/Load byte string
        //LODSW,// Load string/Load word string
        //LODSD,// Load string/Load doubleword string
        //STOS,
        //STOSB,// Store string/Store byte string
        //STOSW,// Store string/Store word string
        //STOSD,// Store string/Store doubleword string
        //REP,// Repeat while ECX not zero
        //REPE,
        //REPZ,// Repeat while equal/Repeat while zero
        //REPNE,
        //REPNZ,// Repeat while not equal/Repeat while not zero
        #endregion String Instructions

        #region I/O Instructions
        //These instructions move data between the processor’s I/O ports and a register or memory.

        /// <summary>Read from a port</summary>
        public sealed class In : Opcode2Base
        {
            public In(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.IN, args, Ot2.reg_imm | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 64)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                Rn regOp1 = this.op1.Rn;
                if (!((regOp1 == Rn.AL) || (regOp1 == Rn.AX) || (regOp1 == Rn.EAX)))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }

                if (this.op2.IsImm)
                {
                    if (this.op2.NBits != 8)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }
                else
                {
                    if (this.op2.Rn != Rn.DX)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }
            }
            public override void Execute()
            {
                // special case: set the truth value to known
                Rn reg = this.op1.Rn;
                BitVecExpr unknown = Tools.Reg_Key_Fresh(reg, this._t.Rand, this.Ctx);
                this.RegularUpdate.Set(reg, unknown, this.Ctx.MkBV(0, (uint)this.op1.NBits));
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        /// <summary>Write to a port</summary>
        public sealed class Out : Opcode2Base
        {
            public Out(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.OUT, args, Ot2.imm_reg | Ot2.reg_reg, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op2.NBits == 64)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                Rn regOp2 = this.op2.Rn;
                if (!((regOp2 == Rn.AL) || (regOp2 == Rn.AX) || (regOp2 == Rn.EAX)))
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }

                if (this.op1.IsImm)
                {
                    if (this.op1.NBits != 8)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }
                else
                {
                    if (this.op1.Rn != Rn.DX)
                    {
                        this.SyntaxError = string.Format("\"{0}\": Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                    }
                }

            }
            public override void Execute()
            {
                // state is not changed
            }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
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
            public Stc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.STC, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, Tv.ONE);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }
        /// <summary>Clear the carry flag</summary>
        public sealed class Clc : Opcode0Base
        {
            public Clc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CLC, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }
        /// <summary>Complement the carry flag</summary>
        public sealed class Cmc : Opcode0Base
        {
            public Cmc(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.CMC, args, keys, t) { }
            public override void Execute()
            {
                this.RegularUpdate.Set(Flags.CF, this.Ctx.MkNot(this.Get(Flags.CF)));
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF; } }
        }

        public sealed class Lahf : Opcode0Base
        {
            public Lahf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LAHF, args, keys, t) { }
            public override void Execute()
            {
                BitVecNum ZERO = this.Ctx.MkBV(0, 1);
                BitVecNum ONE = this.Ctx.MkBV(1, 1);

                BitVecExpr ahExpr = this.Ctx.MkITE(this.Get(Flags.SF), ONE, ZERO) as BitVecExpr;
                ahExpr = this.Ctx.MkConcat(ahExpr, this.Ctx.MkITE(this.Get(Flags.ZF), ONE, ZERO) as BitVecExpr);
                ahExpr = this.Ctx.MkConcat(ahExpr, ZERO);
                ahExpr = this.Ctx.MkConcat(ahExpr, this.Ctx.MkITE(this.Get(Flags.AF), ONE, ZERO) as BitVecExpr);
                ahExpr = this.Ctx.MkConcat(ahExpr, ZERO);
                ahExpr = this.Ctx.MkConcat(ahExpr, this.Ctx.MkITE(this.Get(Flags.PF), ONE, ZERO) as BitVecExpr);
                ahExpr = this.Ctx.MkConcat(ahExpr, ONE);
                ahExpr = this.Ctx.MkConcat(ahExpr, this.Ctx.MkITE(this.Get(Flags.CF), ONE, ZERO) as BitVecExpr);

                this.RegularUpdate.Set(Rn.AH, ahExpr);
            }
            public override Flags FlagsReadStatic { get { return Flags.SF | Flags.ZF | Flags.AF | Flags.PF | Flags.CF; } }
        }

        public sealed class Sahf : Opcode0Base
        {
            public Sahf(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.SAHF, args, keys, t) { }
            public override void Execute()
            {
                BitVecNum ONE = this.Ctx.MkBV(1, 1);
                BitVecExpr ahExpr = this.Get(Rn.AH);

                this.RegularUpdate.Set(Flags.SF, ToolsZ3.GetBit(ahExpr, 7, ONE, this.Ctx));
                this.RegularUpdate.Set(Flags.ZF, ToolsZ3.GetBit(ahExpr, 6, ONE, this.Ctx));
                this.RegularUpdate.Set(Flags.AF, ToolsZ3.GetBit(ahExpr, 4, ONE, this.Ctx));
                this.RegularUpdate.Set(Flags.PF, ToolsZ3.GetBit(ahExpr, 2, ONE, this.Ctx));
                this.RegularUpdate.Set(Flags.CF, ToolsZ3.GetBit(ahExpr, 0, ONE, this.Ctx));
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
            public Lea(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.LEA, args, Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operand 1 cannot be 8 bits. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                BitVecExpr address = Tools.Calc_Effective_Address(this.op2, this.keys.PrevKey, this.Ctx);
                uint addressSize = address.SortSize;
                uint operandSize = (uint)this.op1.NBits;

                if (operandSize == addressSize)
                {
                    this.RegularUpdate.Set(this.op1, address);
                }
                else if ((operandSize == 16) && (addressSize == 32))
                {
                    this.RegularUpdate.Set(this.op1, this.Ctx.MkExtract(16 - 1, 0, address));
                }
                else if ((operandSize == 16) && (addressSize == 64))
                {
                    this.RegularUpdate.Set(this.op1, this.Ctx.MkExtract(16 - 1, 0, address));
                }
                else if ((operandSize == 32) && (addressSize == 64))
                {
                    this.RegularUpdate.Set(this.op1, this.Ctx.MkExtract(32 - 1, 0, address));
                }
                else if ((operandSize == 64) && (addressSize == 32))
                {
                    this.RegularUpdate.Set(this.op1, this.Ctx.MkZeroExt(32, address));
                }
            }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        public sealed class Nop : Opcode0Base
        {
            public Nop(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.NOP, args, keys, t) { }
            public override void Execute()
            {
                this.CreateRegularUpdate(); // do nothing, only create an empty update
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
        //MOVBE,
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
            public AddPD(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.ADDPD, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operands should have equal sizes. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }

            }
            public override void Execute()
            {
                Context ctx = this.Ctx;
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
                this.RegularUpdate.Set(this.op1, result);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op1, this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }

        public sealed class Popcnt : Opcode2Base
        {
            public Popcnt(string[] args, (string prevKey, string nextKey, string nextKeyBranch) keys, Tools t) : base(Mnemonic.POPCNT, args, Ot2.reg_reg | Ot2.reg_mem, keys, t)
            {
                if (this.IsHalted) return;
                if (this.op1.NBits != this.op2.NBits)
                {
                    this.SyntaxError = string.Format("\"{0}\": Operands should have equal sizes. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
                if (this.op1.NBits == 8)
                {
                    this.SyntaxError = string.Format("\"{0}\": 8 bits operands are not allowed. Operand1={1} ({2}, bits={3}); Operand2={4} ({5}, bits={6})", this.ToString(), this.op1, this.op1.Type, this.op1.NBits, this.op2, this.op2.Type, this.op2.NBits);
                }
            }
            public override void Execute()
            {
                Context ctx = this.Ctx;
                uint nBits = (uint)this.op1.NBits;
                BitVecExpr b = this.Op2Value;

                BitVecExpr result = ctx.MkZeroExt(6, ToolsZ3.GetBit_BV(b, 0, ctx));
                for (uint bit = 1; bit < nBits; ++bit)
                {
                    result = ctx.MkBVAdd(result, ctx.MkZeroExt(6, ToolsZ3.GetBit_BV(b, bit, ctx)));
                }

                result = ctx.MkZeroExt(nBits - 7, result);
                this.RegularUpdate.Set(this.op1, result);

                this.RegularUpdate.Set(Flags.OF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.SF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.ZF, ctx.MkEq(b, ctx.MkBV(0, nBits)));
                this.RegularUpdate.Set(Flags.AF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.CF, Tv.ZERO);
                this.RegularUpdate.Set(Flags.PF, Tv.ZERO);
            }
            public override Flags FlagsWriteStatic { get { return Flags.CF_PF_AF_ZF_SF_OF; } }
            public override IEnumerable<Rn> RegsReadStatic { get { return ToRegEnumerable(this.op2); } }
            public override IEnumerable<Rn> RegsWriteStatic { get { return ToRegEnumerable(this.op1); } }
        }
        #endregion
        #endregion Instructions
    }
}
