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

using System;
using System.Collections.Generic;

using Microsoft.Z3;
using AsmTools;
using System.Text;

namespace AsmSim
{
    public class ProgramSyntesizer
    {
        private readonly Context _ctx;
        private readonly Solver _solver;
        private readonly Tactic _tactic;
        private readonly IDictionary<int, List<BoolExpr>> _switches;
        private readonly IDictionary<string, BitVecExpr> _constants;

        private readonly int _nLines;
        private readonly IList<Rn> _registers;
        private readonly ISet<string> _validPrograms;

        public ProgramSyntesizer(int nLines, IList<Rn> registers)
        {
            this._nLines = nLines;
            this._registers = registers;


            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "unsat-core", "true" },    // enable generation of unsat cores
                { "model", "true" },         // enable model generation
                { "proof", "true" }         // enable proof generation
            };
            this._ctx = new Context(settings);

            Tactic ta1 = this._ctx.MkTactic("simplify"); // some minor rewrites
            Tactic ta2 = this._ctx.MkTactic("ctx-simplify"); // 
            Tactic ta3 = this._ctx.MkTactic("ctx-solver-simplify"); //VERY SLOW

            this._tactic = ta3;// ctx.AndThen(ta2, ta1);
            this._solver = this._ctx.MkSolver();

            this._validPrograms = new HashSet<string>();
            this._constants = new Dictionary<string, BitVecExpr>();
            this._switches = new Dictionary<int, List<BoolExpr>>();
            for (int i = 1; i <= this._nLines; ++i)
            {
                this._switches.Add(i, new List<BoolExpr>());
            }
        }

        public void Run()
        {
            Context ctx = this._ctx;

            #region Fill Solver
            for (int lineNumber = 1; lineNumber <= this._nLines; ++lineNumber)
            {
                //this.AddInstruction_Shl(lineNumber);

                foreach (Rn reg1 in this._registers)
                {
                    foreach (Rn reg2 in this._registers)
                    {
                        //this.AddInstruction_Add(reg1, reg2, lineNumber);
                        //this.AddInstruction_Sub(reg1, reg2, lineNumber);
                        //this.AddInstruction_Mov(reg1, reg2, lineNumber);
                        this.AddInstruction_Xor(reg1, reg2, lineNumber);
                    }
                    this.AddInstruction_Inc(reg1, lineNumber);
                    //this.AddInstruction_Dec(reg1, lineNumber);
                    //this.AddInstruction_Mov(reg1, lineNumber);
                }
                this.AddInstruction_Nop(lineNumber);

                BoolExpr[] switches_line = this._switches[lineNumber].ToArray();
                this._solver.Assert(ctx.MkAtMost(switches_line, 1));
                this._solver.Assert(ctx.MkOr(switches_line));
            }

            // assume all registers are initially unknown
            foreach (Rn reg in this._registers)
            {
                //this._solver.Assert(ctx.MkEq(GetRegKnown(reg, 0, ctx), MakeUnknownConst(reg, ctx)));
            }
            #endregion

            #region Add Target Constains

            // rax_1: negate the lowest bit
            BitVecExpr rax_1 = ctx.MkConcat(ctx.MkExtract(63, 1, GetReg(Rn.RAX, 0, ctx)), ctx.MkBVNeg(ctx.MkExtract(0, 0, GetReg(Rn.RAX, 0, ctx))));
            // rax_2: add 1
            BitVecExpr rax_2 = ctx.MkBVAdd(GetReg(Rn.RAX, 0, ctx), ctx.MkBV(1, 64));
            // rax_3: constant 32
            BitVecExpr rax_3 = ctx.MkBV(32, 64);

            BitVecExpr rax_n = GetReg(Rn.RAX, this._nLines, ctx);
            BitVecExpr rax_0 = GetReg(Rn.RAX, 0, ctx);

            if (true)
            {
                if (true)
                {   // increment rax with 1
                    this._solver.Assert(ctx.MkEq(rax_n, ctx.MkBVAdd(GetReg(Rn.RAX, 0, ctx), ctx.MkBV(1, 64))));
                    this._solver.Assert(GetRegGoal(Rn.RAX, this._nLines, ctx));
                    this._solver.Assert(ctx.MkNot(GetRegProvided(Rn.RAX, 0, ctx)));
                    this._solver.Assert(GetRegProvided(Rn.RAX, this._nLines, ctx));
                }
                else
                {   // set rax to constant 32
                    this._solver.Assert(ctx.MkEq(rax_n, ctx.MkBV(0, 64)));
                    this._solver.Assert(GetRegGoal(Rn.RAX, this._nLines, ctx));
                    this._solver.Assert(ctx.MkNot(GetRegProvided(Rn.RAX, 0, ctx)));
                    this._solver.Assert(GetRegProvided(Rn.RAX, this._nLines, ctx));
                }
            }
            else
            {
                this._solver.Assert(ctx.MkEq(rax_n, rax_2));
                this._solver.Assert(IsKnownTest(Rn.RAX, this._nLines, ctx));
            }

            #endregion

            Console.WriteLine(ToString(this._solver));
            if (this._solver.Check() == Status.SATISFIABLE)
            {
                this.GetAllModels(this._solver, ctx);
            }
            else
            {
                Console.WriteLine("INFO: No code exists that implements the target constraints.");
                foreach (BoolExpr b in this._solver.UnsatCore)
                {
                    Console.WriteLine(b);
                }
            }
        }
        private void GetAllModels(Solver solver, Context ctx)
        {
            IList<int> freeLines = new List<int>();
            for (int lineNumber = 1; lineNumber <= this._nLines; ++lineNumber) freeLines.Add(lineNumber);
            int count = 0;
            this.GetMostModels(ref count, solver, ctx, freeLines);
        }
        private void GetMostModels(ref int counter, Solver solver, Context ctx, IList<int> freeLines)
        {
            if (false)
            {
                Console.Write("GetMostModels2: entering: freeLines=");
                foreach (int i in freeLines) Console.Write(i + ",");
                Console.WriteLine();
            }

            foreach (int lineNumber in freeLines)
            {
                foreach (BoolExpr codeSwitch in this._switches[lineNumber])
                {
                    //Add target to solver and check if target is forced in the model

                    //Console.WriteLine("GetMostModels2: checking codeSwitch " + codeSwitch);
                    if (solver.Check(codeSwitch) == Status.SATISFIABLE)
                    {
                        (string programStr, string programId) = Model2Asm(solver.Model);
                        if (!this._validPrograms.Contains(programId))
                        {
                            solver.Push();

                            this._validPrograms.Add(programId);
                            Console.WriteLine("-----------------\ncount " + counter);
                            counter++;
                            Console.Write(programStr);
                            Console.WriteLine(ToString(solver.Model));
                            //Console.WriteLine(ToString_Constants(solver.Model));
                            //return;

                            if (freeLines.Count > 1)
                            {
                                IList<int> freeLines2 = new List<int>(freeLines);
                                freeLines2.Remove(lineNumber);
                                GetMostModels(ref counter, solver, ctx, freeLines2);
                            }
                            solver.Pop();
                            //Console.WriteLine("GetMostModels2: --------------------------");
                        }
                    }
                    else
                    {
                        //Console.WriteLine("codeSwitch " + codeSwitch + " cannot be satisfied");
                    }
                }
            }

            if (false)
            {
                Console.Write("GetMostModels2: exiting: freeLines=");
                foreach (int i in freeLines) Console.Write(i + ",");
                Console.WriteLine();
            }
        }


        #region Flags

        private static BoolExpr SetFlag(Flags flag, Tv tv, int lineNumber, Context ctx)
        {
            BoolExpr value;
            switch (tv)
            {
                case Tv.ONE: value = ctx.MkTrue(); break;
                case Tv.ZERO: value = ctx.MkFalse(); break;
                default: value = ctx.MkBoolConst(flag + "!" + lineNumber + "U"); break;
            }
            return ctx.MkEq(GetFlag(flag, lineNumber, ctx), value);
        }


        private static BoolExpr ZeroFlag(Rn reg, int lineNumber, Context ctx)
        {
            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;
            uint nBits = (uint)RegisterTools.NBits(reg);
            BitVecExpr ZERO = ctx.MkBV(0, nBits);
            return ctx.MkEq(GetFlag(Flags.ZF, ln1, ctx), ctx.MkEq(GetReg(reg, ln1, ctx), ZERO));
        }

        private BoolExpr OverFlowFlag(Rn reg, int lineNumber, Context ctx)
        {
            return SetFlag(Flags.OF, Tv.UNKNOWN, lineNumber, ctx);
        }

        private BoolExpr SignFlag(Rn reg, int lineNumber, Context ctx)
        {
            return SetFlag(Flags.OF, Tv.UNKNOWN, lineNumber, ctx);
        }

        #endregion


        #region Instuctions
        private static BoolExpr IsKnownTest(Rn reg, int lineNumber, Context ctx)
        {
            return IsKnownTest(GetRegKnown(reg, lineNumber, ctx), ctx);
        }
        private static BoolExpr IsKnownTest(BitVecExpr expr, Context ctx)
        {
            //return ctx.MkITE(ctx.MkEq(expr, MakeKnownConst((int)expr.SortSize, ctx)), ctx.MkTrue(), ctx.MkEq(ctx.MkTrue(), ctx.MkFalse())) as BoolExpr;
            return ctx.MkImplies(ctx.MkNot(ctx.MkEq(expr, MakeKnownConst((int)expr.SortSize, ctx))), ctx.MkEq(ctx.MkTrue(), ctx.MkFalse()));
        }

        private static BitVecExpr MakeKnownConst(Rn reg, Context ctx)
        {
            return MakeKnownConst(RegisterTools.NBits(reg), ctx);
        }
        private static BitVecExpr MakeKnownConst(int nBits, Context ctx)
        {
            return ctx.MkBV(0, (uint)nBits);
        }
        private static BitVecExpr MakeUnknownConst(Rn reg, Context ctx)
        {
            switch (RegisterTools.NBits(reg))
            {
                case 64: return ctx.MkBV(0xFFFF_FFFF_FFFF_FFFF, 64);
                case 32: return ctx.MkBV(0xFFFF_FFFF, 32);
                case 16: return ctx.MkBV(0xFFFF, 16);
                case 8: return ctx.MkBV(0xFF, 8);
                default: throw new Exception();
            }
        }

        private static BoolExpr MakeRuleRegResult(Rn selectedReg, IList<Rn> regs, BitVecExpr newState, int lineNumber, Context ctx)
        {
            BoolExpr[] r = new BoolExpr[regs.Count];
            for (int i = 0; i < regs.Count; ++i)
            {
                Rn reg1 = regs[i];
                r[i] = ctx.MkEq(GetReg(reg1, lineNumber, ctx), (selectedReg == reg1) ? newState : GetReg(reg1, lineNumber - 1, ctx));
            }
            return ctx.MkAnd(r);
        }

        private void AddInstruction_Shl(int lineNumber)
        {
            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            Context ctx = this._ctx;

            foreach (Rn reg in this._registers)
            {
                if (true)
                {
                    string asm = "SHL_" + reg + "_CL";
                    BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
                    this._switches[lineNumber].Add(instruction_Switch);

                    {
                        BitVecExpr x = ctx.MkBVSHL(GetReg(reg, ln0, ctx), ctx.MkBVAND(GetReg(Rn.RCX, ln0, ctx), ctx.MkBV(0x3F, 64)));
                        BoolExpr newState = MakeRuleRegResult(reg, this._registers, x, lineNumber, ctx);
                        this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                    }
                    {
                        BitVecExpr shiftedValue = ctx.MkBVSHL(GetRegKnown(reg, ln0, ctx), ctx.MkBVAND(GetReg(Rn.RCX, ln0, ctx), ctx.MkBV(0x3F, 64)));
                        BoolExpr newState = ctx.MkAnd(
                            ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg == Rn.RAX) ? shiftedValue : GetRegKnown(Rn.RAX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg == Rn.RBX) ? shiftedValue : GetRegKnown(Rn.RBX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg == Rn.RCX) ? shiftedValue : GetRegKnown(Rn.RCX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg == Rn.RDX) ? shiftedValue : GetRegKnown(Rn.RDX, ln0, ctx))
                        );
                        this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                        //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg, ln1, ctx)));
                        //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(ctx.MkBVAND(GetRegKnown(Rn.RCX, ln0, ctx), ctx.MkBV(0x3F, 64)), ctx)));
                    }
                }
                if (true)
                {


                    #region Create Constant
                    string constantName_prev = "Const-SHL-" + reg + "-L" + (lineNumber - 1);
                    string constantName = "Const-SHL-" + reg + "-L" + lineNumber;
                    BitVecExpr constant;

                    if (true)
                    {
                        constant = ctx.MkBVConst(constantName, 64);
                        this._constants.Add(constantName, constant);
                        BoolExpr constantConstraint = ctx.MkNot(ctx.MkEq(ctx.MkBVAND(constant, ctx.MkBV(0x3F, 64)), ctx.MkBV(0, 64)));
                        this._solver.Assert(constantConstraint);
                    }
                    else
                    {
                        //IntNum t1 = ctx.MkInt("ShiftCount");
                        //BitVecExpr constant = ctx.MkBVConst(constantName, 64);

                        //IntExpr t1 = ctx.MkInt2BV(64, constant);
                        //BoolExpr constantConstraint = ctx.MkAnd(ctx.MkGt(ctx.MkInt(64), t1), ctx.MkGt(t1, ctx.MkInt(0)));
                        //this._constants.Add(constantName, constant);
                        // this._solver.Assert(constantConstraint);
                    }
                    #endregion

                    string asm = "SHL_" + reg + "_" + constantName;
                    BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
                    this._switches[lineNumber].Add(instruction_Switch);

                    {
                        BitVecExpr shiftedValue = ctx.MkBVSHL(GetReg(reg, ln0, ctx), ctx.MkBVAND(constant, ctx.MkBV(0x3F, 64)));
                        BoolExpr newState = ctx.MkAnd(
                            ctx.MkEq(GetReg(Rn.RAX, ln1, ctx), (reg == Rn.RAX) ? shiftedValue : GetReg(Rn.RAX, ln0, ctx)),
                            ctx.MkEq(GetReg(Rn.RBX, ln1, ctx), (reg == Rn.RBX) ? shiftedValue : GetReg(Rn.RBX, ln0, ctx)),
                            ctx.MkEq(GetReg(Rn.RCX, ln1, ctx), (reg == Rn.RCX) ? shiftedValue : GetReg(Rn.RCX, ln0, ctx)),
                            ctx.MkEq(GetReg(Rn.RDX, ln1, ctx), (reg == Rn.RDX) ? shiftedValue : GetReg(Rn.RDX, ln0, ctx))
                        );
                        this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                    }
                    {
                        BitVecExpr shiftedValue = ctx.MkBVSHL(GetRegKnown(reg, ln0, ctx), ctx.MkBVAND(constant, ctx.MkBV(0x3F, 64)));
                        BoolExpr newState = ctx.MkAnd(
                            ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg == Rn.RAX) ? shiftedValue : GetRegKnown(Rn.RAX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg == Rn.RBX) ? shiftedValue : GetRegKnown(Rn.RBX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg == Rn.RCX) ? shiftedValue : GetRegKnown(Rn.RCX, ln0, ctx)),
                            ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg == Rn.RDX) ? shiftedValue : GetRegKnown(Rn.RDX, ln0, ctx))
                        );
                        this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                        //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg, ln1, ctx)));
                    }
                }
            }
        }

        private void AddInstruction_Mov(Rn reg, int lineNumber)
        {
            Context ctx = this._ctx;

            #region Create Constant
            string constantName_prev = "Const-Mov-" + reg + "-L" + (lineNumber - 1);
            string constantName = "Const-Mov-" + reg + "-L" + lineNumber;

            BitVecExpr constant = ctx.MkBVConst(constantName, 64);
            this._constants.Add(constantName, constant);
            #endregion

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_MOV_" + reg + "_" + constantName);
            this._switches[lineNumber].Add(instruction_Switch);

            {
                BoolExpr newState = MakeRuleRegResult(reg, this._registers, constant, lineNumber, ctx);
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
            }
            {
                BitVecExpr knownConstant = MakeKnownConst(reg, ctx);
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg == Rn.RAX) ? knownConstant : GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg == Rn.RBX) ? knownConstant : GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg == Rn.RCX) ? knownConstant : GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg == Rn.RDX) ? knownConstant : GetRegKnown(Rn.RDX, ln0, ctx))
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
            }

            if (true)
            {
                if (lineNumber > 1)
                {
                    // prevent destructive writes : try not to overwrite registers that have been written in the privious line.

                    BoolExpr switch_Mov_Previous = ctx.MkBoolConst("L" + (lineNumber - 1) + "_MOV_" + reg + "_" + constantName_prev);
                    this._solver.Assert(ctx.MkNot(ctx.MkAnd(switch_Mov_Previous, instruction_Switch)));

                    BoolExpr switch_Inc_Previous = ctx.MkBoolConst("L" + (lineNumber - 1) + "_INC_" + reg);
                    this._solver.Assert(ctx.MkNot(ctx.MkAnd(switch_Inc_Previous, instruction_Switch)));

                    BoolExpr switch_Dec_Previous = ctx.MkBoolConst("L" + (lineNumber - 1) + "_DEC_" + reg);
                    this._solver.Assert(ctx.MkNot(ctx.MkAnd(switch_Dec_Previous, instruction_Switch)));
                }
            }
        }

        private void AddInstruction_Mov(Rn reg1, Rn reg2, int lineNumber)
        {
            if (reg1 == reg2) return;

            // assume reg1 = RAX
            // assume reg2 = RBX

            Context ctx = this._ctx;
            string asm = "MOV_" + reg1 + "_" + reg2;

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);

            {
                BitVecExpr x = GetReg(reg2, ln0, ctx);
                BoolExpr newState = MakeRuleRegResult(reg1, this._registers, x, lineNumber, ctx);
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
            }
            {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg1 == Rn.RAX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg1 == Rn.RBX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg1 == Rn.RCX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg1 == Rn.RDX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RDX, ln0, ctx))
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg2, ln0, ctx)));
            }
            if (lineNumber > 1)
            {
                foreach (Rn reg3 in this._registers)
                {
                    BoolExpr switch_previous = ctx.MkBoolConst("L" + (lineNumber - 1) + "_MOV " + reg1 + "_" + reg3);
                    this._solver.Assert(ctx.MkNot(ctx.MkAnd(switch_previous, instruction_Switch)));
                }
            }
        }

        private void AddInstruction_Add(Rn reg1, Rn reg2, int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "ADD_" + reg1 + "_" + reg2;

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + " " + asm);
            this._switches[lineNumber].Add(instruction_Switch);
            {
                BitVecExpr x = ctx.MkBVAdd(GetReg(reg1, ln0, ctx), GetReg(reg2, ln0, ctx));
                BoolExpr newRegState = MakeRuleRegResult(reg1, this._registers, x, lineNumber, ctx);
                BoolExpr newFlagState = ctx.MkAnd(
                    ZeroFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newRegState));
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newFlagState));
            }
            {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg1 == Rn.RAX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg1 == Rn.RBX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg1 == Rn.RCX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg1 == Rn.RDX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RDX, ln0, ctx)),
                    ZeroFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
               // this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg1, ln0, ctx)));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg2, ln0, ctx)));
            }
        }

        private void AddInstruction_Sub(Rn reg1, Rn reg2, int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "SUB_" + reg1 + "_" + reg2;

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);
            {
                BitVecExpr x = ctx.MkBVSub(GetReg(reg1, ln0, ctx), GetReg(reg2, ln0, ctx));
                BoolExpr newRegState = MakeRuleRegResult(reg1, this._registers, x, lineNumber, ctx);
                BoolExpr newFlagState = ctx.MkAnd(
                    ZeroFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newRegState));
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newFlagState));
            }
            {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg1 == Rn.RAX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg1 == Rn.RBX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg1 == Rn.RCX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg1 == Rn.RDX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RDX, ln0, ctx)),
                    ZeroFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg1, ln0, ctx)));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg2, ln0, ctx)));
            }
        }

        private void AddInstruction_Xor(Rn reg1, Rn reg2, int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "XOR_" + reg1 + "_" + reg2;

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;
            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);

            if (true)
            {
                BitVecExpr x = ctx.MkBVXOR(GetReg(Rn.RAX, ln0, ctx), GetReg(reg2, ln0, ctx));
                this._solver.Assert(ctx.MkImplies(
                    instruction_Switch,
                    MakeRuleRegResult(reg1, this._registers, x, lineNumber, ctx)
                ));


                if (reg1 == reg2)
                {
                    this._solver.Assert(ctx.MkImplies(
                        instruction_Switch,
                        ctx.MkAnd(
                            //GetRegGoal(reg1, ln0, ctx), // rax0_goal is irelevant 
                            GetRegGoal(reg1, ln1, ctx), // make application of this rule goal directed
                            //GetRegProvided(reg1, ln0, ctx), // TODO: could this create inconsistencies with other instructions that updated rax!0
                            GetRegProvided(reg1, ln1, ctx) // rax1 is not based on (variable) input but is a constant
                        )
                    ));
                } else
                {

                }
            }

            if (false) {
                BitVecExpr x = ctx.MkBVXOR(GetReg(Rn.RAX, ln0, ctx), GetReg(reg2, ln0, ctx));
                BoolExpr newRegState = MakeRuleRegResult(reg1, this._registers, x, lineNumber, ctx);

                BoolExpr newFlagState = ctx.MkAnd(
                    SetFlag(Flags.OF, Tv.ZERO, lineNumber, ctx),
                    SetFlag(Flags.CF, Tv.ZERO, lineNumber, ctx),
                    SetFlag(Flags.AF, Tv.UNDEFINED, lineNumber, ctx),

                    ZeroFlag(reg1, lineNumber, ctx),
                    SignFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
               // this._solver.Assert(ctx.MkImplies(instruction_Switch, newFlagState));
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newRegState));
            }
            if (false) {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), (reg1 == Rn.RAX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), (reg1 == Rn.RBX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), (reg1 == Rn.RCX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), (reg1 == Rn.RDX) ? GetRegKnown(reg2, ln0, ctx) : GetRegKnown(Rn.RDX, ln0, ctx)),
                    ZeroFlag(reg1, lineNumber, ctx),
                    OverFlowFlag(reg1, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg1, ln0, ctx)));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg2, ln0, ctx)));
            }
        }

        private void AddInstruction_Inc(Rn reg, int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "INC_" + reg;
            uint nBits = (uint)RegisterTools.NBits(reg);

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);

            if (true)
            {
                BitVecExpr x = ctx.MkBVAdd(GetReg(reg, ln0, ctx), ctx.MkBV(1, nBits));
                this._solver.Assert(ctx.MkImplies(
                    instruction_Switch,
                    MakeRuleRegResult(reg, this._registers, x, lineNumber, ctx)
                ));

                this._solver.Assert(ctx.MkImplies(
                    instruction_Switch,
                    ctx.MkAnd(
                        GetRegGoal(reg, ln0, ctx), // make the prerequisite a goal
                        GetRegGoal(reg, ln1, ctx), // make application of this rule goal directed
                        GetRegProvided(reg, ln0, ctx), //rax0 is based on (variable) input but is not a constant
                        GetRegProvided(reg, ln1, ctx)  //rax1 is based on (variable) input but is not a constant
                    )
                ));
            }
            if (false) {
                BitVecExpr x = ctx.MkBVAdd(GetReg(reg, ln0, ctx), ctx.MkBV(1, nBits));
                BoolExpr newRegState = MakeRuleRegResult(reg, this._registers, x, lineNumber, ctx);
                BoolExpr newFlagState = ctx.MkAnd(
                    ZeroFlag(reg, lineNumber, ctx),
                    OverFlowFlag(reg, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newRegState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, newFlagState));
            }
            if (false) {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), GetRegKnown(Rn.RDX, ln0, ctx)),

                    ZeroFlag(reg, lineNumber, ctx),
                    OverFlowFlag(reg, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg, ln1, ctx)));
            }
        }

        private void AddInstruction_Dec(Rn reg, int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "DEC_" + reg;
            uint nBits = (uint)RegisterTools.NBits(reg);

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);

            {
                BitVecExpr x = ctx.MkBVSub(GetReg(reg, ln0, ctx), ctx.MkBV(1, nBits));
                BoolExpr newRegState = MakeRuleRegResult(reg, this._registers, x, lineNumber, ctx);

                BoolExpr newFlagState = ctx.MkAnd(
                    ZeroFlag(reg, lineNumber, ctx),
                    OverFlowFlag(reg, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newFlagState));
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newRegState));
            }
            {
                BoolExpr newState = ctx.MkAnd(
                    ctx.MkEq(GetRegKnown(Rn.RAX, ln1, ctx), GetRegKnown(Rn.RAX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RBX, ln1, ctx), GetRegKnown(Rn.RBX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RCX, ln1, ctx), GetRegKnown(Rn.RCX, ln0, ctx)),
                    ctx.MkEq(GetRegKnown(Rn.RDX, ln1, ctx), GetRegKnown(Rn.RDX, ln0, ctx)),

                    ZeroFlag(reg, lineNumber, ctx),
                    OverFlowFlag(reg, lineNumber, ctx)
                );
                this._solver.Assert(ctx.MkImplies(instruction_Switch, newState));
                //this._solver.Assert(ctx.MkImplies(instruction_Switch, IsKnownTest(reg, ln1, ctx)));
            }
        }

        private void AddInstruction_Nop(int lineNumber)
        {
            Context ctx = this._ctx;
            string asm = "NOP";

            int ln0 = lineNumber - 1;
            int ln1 = lineNumber;

            BoolExpr instruction_Switch = ctx.MkBoolConst("L" + lineNumber + "_" + asm);
            this._switches[lineNumber].Add(instruction_Switch);

            IList<BoolExpr> r = new List<BoolExpr>();
            for (int i = 0; i < this._registers.Count; ++i)
            {
                Rn reg = this._registers[i];
                r.Add(ctx.MkEq(GetReg(reg, lineNumber, ctx), GetReg(reg, lineNumber - 1, ctx)));

                r.Add(ctx.MkEq(GetRegGoal(reg, ln1, ctx), GetRegGoal(reg, ln0, ctx))); // make the prerequisite a goal
                r.Add(ctx.MkEq(GetRegProvided(reg, ln1, ctx), GetRegProvided(reg, ln0, ctx))); //rax0 is based on (variable) input but is not a constant
            }

            this._solver.Assert(ctx.MkImplies(
                instruction_Switch,
                ctx.MkAnd(r)
            ));
        }

        #endregion

        private bool IsGrounded(BitVecExpr value, Solver solver, Context ctx)
        {
            // TODO return ToolsZ3.GetUlong(value, value.SortSize, solver, ctx) != null;
            throw new NotImplementedException();
        }

        private bool Contains(FuncDecl f, IEnumerable<FuncDecl> e)
        {
            foreach (FuncDecl f2 in e)
            {
                if (f2.Id == f.Id) return true;
            }
            return false;
        }


        private static BitVecExpr GetReg(Rn reg, int lineNumber, Context ctx)
        {
            return ctx.MkBVConst(reg + "!" + lineNumber, (uint)RegisterTools.NBits(reg));
        }
        private static BoolExpr GetRegGoal(Rn reg, int lineNumber, Context ctx)
        {
            return ctx.MkBoolConst(reg + "!" + lineNumber + "!GOAL");
        }
        private static BoolExpr GetRegProvided(Rn reg, int lineNumber, Context ctx)
        {
            return ctx.MkBoolConst(reg + "!" + lineNumber + "!PROVIDED");
        }


        private static BoolExpr GetFlag(Flags flag, int lineNumber, Context ctx)
        {
            return ctx.MkBoolConst(flag + "!" + lineNumber);
        }
        private static BitVecExpr GetRegKnown(Rn reg, int lineNumber, Context ctx)
        {
            return ctx.MkBVConst(reg + "!" + lineNumber + "K", (uint)RegisterTools.NBits(reg));
        }

        private (string code, string codeID) Model2Asm(Model model)
        {
            //sb.Append(ToString(model));
            ISet<string> program = new SortedSet<string>();
            ISet<string> program2 = new SortedSet<string>();

            foreach (FuncDecl funcDecl in model.ConstDecls)
            {
                Expr value = model.ConstInterp(funcDecl);
                if (value.IsBool && value.IsTrue)
                {
                    //Console.WriteLine(funcDecl);
                    string codeLine = funcDecl.Name.ToString();
                    string codeLine2 = funcDecl.Name.ToString();

                    foreach (string constant in this._constants.Keys)
                    {
                        if (codeLine.Contains(constant))
                        {
                            FuncDecl constantFuncDecl = this._constants[constant].FuncDecl;
                            string constantValue;
                            if (Contains(constantFuncDecl, model.ConstDecls))
                            {
                                constantValue = model.ConstInterp(this._constants[constant].FuncDecl).ToString();
                            }
                            else
                            {
                                constantValue = "?";
                            }
                            //string constantValue = ToolsZ3.ToStringHex(ToolsZ3.GetTv5Array(this._constants[constant], 64, this._solver, this._ctx));
                            codeLine = codeLine2.Replace('_', ' ').Replace(constant, constantValue);
                            codeLine2 = codeLine2.Replace('_', ' ');
                            break;
                        }
                    }
                    program.Add(codeLine);
                    program2.Add(codeLine2);
                }
            }

            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();
            foreach (string s in program) sb.AppendLine(s);
            foreach (string s in program2) sb2.AppendLine(s);
            return (sb.ToString(), sb2.ToString());
        }
        private string ToString(Model model)
        {
            ISet<string> program = new SortedSet<string>();
            foreach (FuncDecl funcDecl in model.ConstDecls)
            {
                Expr value = model.ConstInterp(funcDecl);

                if (true)
                {
                    program.Add(funcDecl.Name + " = " + ToString(value));
                }
                else
                {
                    if (value.IsBool && value.IsTrue)
                    {
                        program.Add(funcDecl.Name + " = " + ToString(value));
                    }
                    else
                    {
                        program.Add(funcDecl.Name + " = " + ToString(value));
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\nModel:");
            foreach (string s in program)
            {
                sb.AppendLine(s);
            }
            return sb.ToString();
        }
        private string ToString_Constants(Model model)
        {
            ISet<string> program = new SortedSet<string>();

            foreach (KeyValuePair<string, BitVecExpr> pair in this._constants)
            {
                BitVecExpr constant = pair.Value;
                program.Add(pair.Key + " = " + ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(constant, 64, this._solver, this._ctx)));
            }
            foreach (Rn reg in new List<Rn>() { Rn.RAX })//, Rn.RBX, Rn.RCX, Rn.RDX})
            {
                for (int lineNumber = 0; lineNumber <= this._nLines; ++lineNumber)
                {
                    BitVecExpr regValue = GetReg(reg, lineNumber, this._ctx);
                    program.Add(regValue.FuncDecl.Name + " = " + ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(regValue, 64, this._solver, this._ctx)));
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Constants:");
            foreach (string s in program)
            {
                sb.AppendLine(s);
            }
            return sb.ToString();
        }

        private string ToString(Expr expr)
        {
            try
            {
                if (expr.IsTrue) return "true";
                if (expr.IsFalse) return "false";
                if (expr.IsNumeral)
                {
                    BitVecNum num = expr as BitVecNum;
                    if (num != null)
                    {
                        ulong longValue = (ulong)num.BigInteger;
                        return "0x" + longValue.ToString("X");
                    }
                }
            }
            catch (Exception) { }
            return expr.ToString();
        }


        private string ToString(Solver solver)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("solver:");
            foreach (BoolExpr e in solver.Assertions)
            {
                sb.AppendLine(ToolsZ3.ToString(e));
            }
            return sb.ToString();
        }
        private string Solver2Asm(Solver solver, Context ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\nAsm:");
            for (int lineNumber = 1; lineNumber <= this._nLines; ++lineNumber)
            {
                string codeLine = "";
                foreach (BoolExpr instruction_Switch in this._switches[lineNumber])
                {
                    Tv tv = ToolsZ3.GetTv(instruction_Switch, solver, ctx);

                    if (false)
                    {
                        if (solver.Check() == Status.SATISFIABLE)
                        {
                            Console.WriteLine(instruction_Switch + " = " + tv);
                            Console.WriteLine(ToString(solver.Model));
                        }
                    }
                    if (tv == Tv.ONE)
                    {
                        codeLine = instruction_Switch.FuncDecl.Name.ToString();
                        break;
                    }
                    else if (tv == Tv.UNKNOWN)
                    {
                        codeLine = instruction_Switch.FuncDecl.Name + " | " + codeLine;
                    }
                }
                sb.AppendLine(codeLine);
            }
            if (false)
            {
                foreach (KeyValuePair<string, BitVecExpr> pair in this._constants)
                {
                    BitVecExpr constant = pair.Value;
                    sb.AppendLine(pair.Key + " = " + ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(constant, 64, this._solver, this._ctx)));
                }

                foreach (Rn reg in new List<Rn>() { Rn.RAX })//, Rn.RBX, Rn.RCX, Rn.RDX})
                {
                    for (int lineNumber = 0; lineNumber <= this._nLines; ++lineNumber)
                    {
                        BitVecExpr regValue = GetReg(reg, lineNumber, ctx);
                        sb.AppendLine(regValue.FuncDecl.Name + " = " + ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(regValue, 64, this._solver, this._ctx)));
                    }
                }
            }
            return sb.ToString();
        }
    }
}