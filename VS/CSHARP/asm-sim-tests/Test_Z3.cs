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

namespace unit_tests_asm_z3
{
    using System;
    using System.Collections.Generic;
    using AsmSim;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Z3;

    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
    public class Test_Z3
    {
        /*
        #region Memory With Functions
        [TestMethod]
        public void Test_Z3_FuncDecl_1() {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_10 = ctx.MkBV(10, nBits);
            BitVecExpr bv_20 = ctx.MkBV(20, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            Sort sort = ctx.MkBitVecSort(nBits);
            FuncDecl memFunc = ctx.MkFuncDecl("mem", sort, sort);
            Goal state = ctx.MkGoal();
            #endregion

            // mov qword ptr[0], 10
            state.Assert(ctx.MkEq(ctx.MkApp(memFunc, bv_0), bv_10));
            // mov qword ptr[10], 20
            state.Assert(ctx.MkEq(ctx.MkApp(memFunc, bv_10), bv_20));
            // mov rbx, qword ptr[rax]
            state.Assert(ctx.MkEq(rbx, ctx.MkApp(memFunc, rax)));
            // mov rcx, qword ptr[rbx]
            state.Assert(ctx.MkEq(rcx, ctx.MkApp(memFunc, rbx)));
            // cmp rax, 10;
            // jnz label1:
            state.Assert(ctx.MkEq(rax, bv_0));

            #region Write to console
            Console.WriteLine("state1=" + state);
            if (false) {
                Tactic tactic1 = ctx.MkTactic("propagate-values");
                Goal state2 = tactic1.Apply(state).Subgoals[0];
                Console.WriteLine("state2=" + state2.ToString());
                Goal state3 = tactic1.Apply(state2).Subgoals[0];
                Console.WriteLine("state3=" + state3.ToString());
            }
            Solver solver = ctx.MkSolver();
            solver.Assert(state.Formulas);
            Tv5[] raxTV = ToolsZ3.getTvArray(rax, nBits, solver, ctx);
            Console.WriteLine("rax = " + ToolsZ3.toStringBin(raxTV) + " = " + ToolsZ3.toStringHex(raxTV));
            Tv5[] rbxTV = ToolsZ3.getTvArray(rbx, nBits, solver, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.toStringBin(rbxTV) + " = " + ToolsZ3.toStringHex(rbxTV));
            Tv5[] rcxTV = ToolsZ3.getTvArray(rcx, nBits, solver, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.toStringBin(rcxTV) + " = " + ToolsZ3.toStringHex(rcxTV));
            Tv5[] rdxTV = ToolsZ3.getTvArray(rdx, nBits, solver, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.toStringBin(rdxTV) + " = " + ToolsZ3.toStringHex(rdxTV));
            #endregion
        }
        [TestMethod]
        public void Test_Z3_FuncDecl_2() {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_10 = ctx.MkBV(10, nBits);
            BitVecExpr bv_20 = ctx.MkBV(20, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            Sort sort = ctx.MkBitVecSort(nBits);
            FuncDecl memFunc = ctx.MkFuncDecl("mem", new Sort[] { sort, ctx.MkIntSort() }, sort);
            Goal state = ctx.MkGoal();
            #endregion

            // mov rax, qword ptr[0] ; load rax with value at address 0
            IntExpr memId1 = ctx.MkInt(0);
            state.Assert(ctx.MkEq(rax, ctx.MkApp(memFunc, bv_0, memId1)).Simplify() as BoolExpr);

            // mov qword ptr[rcx], 10 ; store at unknown address rcx value 10
            IntExpr memId2 = ctx.MkITE(ctx.MkEq(rcx, bv_0), ctx.MkAdd(memId1, ctx.MkInt(1)), memId1) as IntExpr;
            state.Assert(ctx.MkEq(ctx.MkApp(memFunc, rcx, memId2), bv_10).Simplify() as BoolExpr);

            // mov rbx, qword ptr[0] ; load rbx with value at address 0
            IntExpr memId3 = memId2;
            state.Assert(ctx.MkEq(rbx, ctx.MkApp(memFunc, bv_0, memId3)).Simplify() as BoolExpr);

            //Theory: is rax == rbx
            // cmp rcx, 0;
            // jnz label1:
            state.Assert(ctx.MkEq(rcx, bv_0));

            #region Write to console
            Console.WriteLine("state1=" + state);

            Solver solver = ctx.MkSolver();
            solver.Assert(state.Formulas);

            Console.WriteLine("");
            Tv5[] raxTV = ToolsZ3.getTvArray(rax, nBits, solver, ctx);
            Console.WriteLine("rax = " + ToolsZ3.toStringBin(raxTV) + " = " + ToolsZ3.toStringHex(raxTV));
            Tv5[] rbxTV = ToolsZ3.getTvArray(rbx, nBits, solver, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.toStringBin(rbxTV) + " = " + ToolsZ3.toStringHex(rbxTV));
            Tv5[] rcxTV = ToolsZ3.getTvArray(rcx, nBits, solver, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.toStringBin(rcxTV) + " = " + ToolsZ3.toStringHex(rcxTV));
            Tv5[] rdxTV = ToolsZ3.getTvArray(rdx, nBits, solver, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.toStringBin(rdxTV) + " = " + ToolsZ3.toStringHex(rdxTV));
            #endregion
        }
        [TestMethod]
        public void Test_Z3_FuncDecl_3() {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_10 = ctx.MkBV(10, nBits);
            BitVecExpr bv_20 = ctx.MkBV(20, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            Sort sort = ctx.MkBitVecSort(nBits);
            IntSort memId = ctx.MkIntSort();
            FuncDecl memFunc = ctx.MkFuncDecl("mem", new Sort[] { sort, memId }, sort);

            Goal state = ctx.MkGoal();
            #endregion

            // mov rcx, qword ptr[rax]
            state.Assert(ctx.MkEq(rcx, ctx.MkApp(memFunc, rax, ctx.MkInt(0))));
            // mov rdx, qword ptr[rbx]
            state.Assert(ctx.MkEq(rdx, ctx.MkApp(memFunc, rbx, ctx.MkInt(0))));
            // cmp rax, rbx;
            // jnz label1:
            state.Assert(ctx.MkEq(rax, rbx));
            // cmp rax, 0
            // jnz label2:
            state.Assert(ctx.MkEq(rcx, bv_10));

            #region Write to console
            Console.WriteLine("state1=" + state);
            if (false) {
                Tactic tactic1 = ctx.MkTactic("propagate-values");
                Goal state2 = tactic1.Apply(state).Subgoals[0];
                Console.WriteLine("state2=" + state2.ToString());
                Goal state3 = tactic1.Apply(state2).Subgoals[0];
                Console.WriteLine("state3=" + state3.ToString());
            }
            Solver solver = ctx.MkSolver();
            solver.Assert(state.Formulas);

            Tv5[] raxTV = ToolsZ3.getTvArray(rax, nBits, solver, ctx);
            Console.WriteLine("rax = " + ToolsZ3.toStringBin(raxTV) + " = " + ToolsZ3.toStringHex(raxTV));
            Tv5[] rbxTV = ToolsZ3.getTvArray(rbx, nBits, solver, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.toStringBin(rbxTV) + " = " + ToolsZ3.toStringHex(rbxTV));
            Tv5[] rcxTV = ToolsZ3.getTvArray(rcx, nBits, solver, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.toStringBin(rcxTV) + " = " + ToolsZ3.toStringHex(rcxTV));
            Tv5[] rdxTV = ToolsZ3.getTvArray(rdx, nBits, solver, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.toStringBin(rdxTV) + " = " + ToolsZ3.toStringHex(rdxTV));
            #endregion
        }
        [TestMethod]
        public void Test_Z3_FuncDecl_4() {
            #region Definitions
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, 64);
            BitVecExpr bv_10 = ctx.MkBV(10, 64);
            BitVecExpr bv_20 = ctx.MkBV(20, 64);

            BitVecExpr rax = ctx.MkBVConst("RAX!0-1955042C05A090D2", 64);
            BitVecExpr rbx = ctx.MkBVConst("RBX!1-5000C87A5EB2FB98", 64);
            BitVecExpr rcx = ctx.MkBVConst("RCX!1-68FC98BF6AFBF63E", 64);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0-231D57E228F579AD", 64);

            //IntExpr time = ctx.MkIntConst("time");

            Sort sort = ctx.MkBitVecSort(64);
            IntSort memId = ctx.MkIntSort();
            FuncDecl memFunc = ctx.MkFuncDecl("mem", new Sort[] { sort, memId }, sort);

            Goal state = ctx.MkGoal();
            #endregion

            Console.WriteLine("mov rbx, 10");
            state.Assert(ctx.MkEq(rbx, bv_10));
            Console.WriteLine("mov rcx, 20");
            state.Assert(ctx.MkEq(rcx, bv_20));
            Console.WriteLine("mov qword ptr[rax], rbx");
            state.Assert(ctx.MkEq(ctx.MkApp(memFunc, rax, ctx.MkInt(0)), rbx));
            Console.WriteLine("mov qword ptr[rax], rcx");
            state.Assert(ctx.MkEq(ctx.MkApp(memFunc, rax, ctx.MkInt(1)), rcx));
            Console.WriteLine("mov rdx, qword ptr[rax]");
            state.Assert(ctx.MkEq(rdx, ctx.MkApp(memFunc, rax, ctx.MkInt(1))));

            #region Write to console
            Console.WriteLine("state1=" + state);
            if (false) {
                Tactic tactic1 = ctx.MkTactic("propagate-values");
                Goal state2 = tactic1.Apply(state).Subgoals[0];
                Console.WriteLine("state2=" + state2.ToString());
                Goal state3 = tactic1.Apply(state2).Subgoals[0];
                Console.WriteLine("state3=" + state3.ToString());
            }
            Solver solver = ctx.MkSolver();
            solver.Assert(state.Formulas);

            Tv5[] raxTV = ToolsZ3.getTvArray(rax, 64, solver, ctx);
            Console.WriteLine("rax = 0b" + ToolsZ3.toStringBin(raxTV) + " = 0x" + ToolsZ3.toStringHex(raxTV));
            Tv5[] rbxTV = ToolsZ3.getTvArray(rbx, 64, solver, ctx);
            Console.WriteLine("rbx = 0b" + ToolsZ3.toStringBin(rbxTV) + " = 0x" + ToolsZ3.toStringHex(rbxTV));
            Tv5[] rcxTV = ToolsZ3.getTvArray(rcx, 64, solver, ctx);
            Console.WriteLine("rcx = 0b" + ToolsZ3.toStringBin(rcxTV) + " = 0x" + ToolsZ3.toStringHex(rcxTV));
            Tv5[] rdxTV = ToolsZ3.getTvArray(rdx, 64, solver, ctx);
            Console.WriteLine("rdx = 0b" + ToolsZ3.toStringBin(rdxTV) + " = 0x" + ToolsZ3.toStringHex(rdxTV));
            #endregion
        }
        #endregion
        */
        #region Memory with Arrays
        [TestMethod]
        public void Test_Z3_MemWithArray_1()
        {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_10 = ctx.MkBV(10, nBits);
            BitVecExpr bv_20 = ctx.MkBV(20, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            ArrayExpr mem = ctx.MkArrayConst("mem", ctx.MkBitVecSort(nBits), ctx.MkBitVecSort(nBits));

            Goal state = ctx.MkGoal();
            #endregion

            // Test chaining with memory loads

            // mov qword ptr[0], 10
            mem = ctx.MkStore(mem, bv_0, bv_10);
            // mov qword ptr[10], 20
            mem = ctx.MkStore(mem, bv_10, bv_20);
            // mov rbx, qword ptr[rax]
            state.Assert(ctx.MkEq(rbx, ctx.MkSelect(mem, rax)));
            // mov rcx, qword ptr[rbx]
            state.Assert(ctx.MkEq(rcx, ctx.MkSelect(mem, rbx)));
            // cmp rax, 10;
            // jnz label1:
            state.Assert(ctx.MkEq(rax, bv_0));

            #region Write to console
            Console.WriteLine("mem=" + mem);

            Solver solver = ctx.MkSolver();
            Solver solver_U = ctx.MkSolver();
            solver.Assert(state.Formulas);
            Console.WriteLine("state1=" + state);
            Console.WriteLine(string.Empty);
            Tv[] raxTV = ToolsZ3.GetTvArray(rax, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rax = " + ToolsZ3.ToStringBin(raxTV) + " = " + ToolsZ3.ToStringHex(raxTV));
            Tv[] rbxTV = ToolsZ3.GetTvArray(rbx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.ToStringBin(rbxTV) + " = " + ToolsZ3.ToStringHex(rbxTV));
            Tv[] rcxTV = ToolsZ3.GetTvArray(rcx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.ToStringBin(rcxTV) + " = " + ToolsZ3.ToStringHex(rcxTV));
            Tv[] rdxTV = ToolsZ3.GetTvArray(rdx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.ToStringBin(rdxTV) + " = " + ToolsZ3.ToStringHex(rdxTV));
            #endregion
        }

        [TestMethod]
        public void Test_Z3_MemWithArray_2()
        {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_16 = ctx.MkBV(16, nBits);
            BitVecExpr bv_32 = ctx.MkBV(32, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            ArrayExpr mem = ctx.MkArrayConst("mem", ctx.MkBitVecSort(nBits), ctx.MkBitVecSort(nBits));

            Goal state = ctx.MkGoal();
            #endregion

            // Test possible memory overwrite
            Console.WriteLine("mov qword ptr[16], 0 ; store at address=16 value=0");
            mem = ctx.MkStore(mem, bv_16, bv_0);
            Console.WriteLine("mov rax, qword ptr[16] ; load rax with the value at address=16");
            state.Assert(ctx.MkEq(rax, ctx.MkSelect(mem, bv_16)));
            Console.WriteLine("mov qword ptr[rcx], 32 ; store at unknown address rcx value 32, appreciate that address 16 could be overwritten");
            mem = ctx.MkStore(mem, rcx, bv_32);
            Console.WriteLine("mov rbx, qword ptr[16] ; load rbx with value at address 16, appreciate that rbx need not be equal to rax");
            state.Assert(ctx.MkEq(rbx, ctx.MkSelect(mem, bv_16)));

            if (true)
            {
                Console.WriteLine("cmp rcx, 16 ;");
                Console.WriteLine("jnz label1: ");
                state.Assert(ctx.MkEq(rcx, bv_16));
            }
            #region Write to console
            Console.WriteLine("mem=" + mem);

            Solver solver = ctx.MkSolver();
            Solver solver_U = ctx.MkSolver();
            solver.Assert(state.Formulas);
            Console.WriteLine("state1=" + state);
            if (true)
            {
                Tactic tactic1 = ctx.MkTactic("propagate-values");
                Goal state2 = tactic1.Apply(state).Subgoals[0];
                Console.WriteLine("state2=" + state2.ToString());
            }
            Console.WriteLine(string.Empty);
            Tv[] raxTV = ToolsZ3.GetTvArray(rax, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rax = " + ToolsZ3.ToStringBin(raxTV) + " = " + ToolsZ3.ToStringHex(raxTV));
            Tv[] rbxTV = ToolsZ3.GetTvArray(rbx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.ToStringBin(rbxTV) + " = " + ToolsZ3.ToStringHex(rbxTV));
            Tv[] rcxTV = ToolsZ3.GetTvArray(rcx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.ToStringBin(rcxTV) + " = " + ToolsZ3.ToStringHex(rcxTV));
            // Tv5[] rdxTV = ToolsZ3.getTvArray(rdx, nBits, solver, ctx);
            // Console.WriteLine("rdx = " + ToolsZ3.toStringBin(rdxTV) + " = " + ToolsZ3.toStringHex(rdxTV));
            #endregion
        }

        [TestMethod]
        public void Test_Z3_MemWithArray_3()
        {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_16 = ctx.MkBV(16, nBits);
            BitVecExpr bv_32 = ctx.MkBV(32, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            ArrayExpr mem = ctx.MkArrayConst("mem", ctx.MkBitVecSort(nBits), ctx.MkBitVecSort(nBits));

            Goal state = ctx.MkGoal();
            #endregion

            // Test if loading from two unknown addresses and then learning that the addresses were equal yields the same result

            // mov rcx, qword ptr[rax]
            state.Assert(ctx.MkEq(rcx, ctx.MkSelect(mem, rax)));
            // mov rdx, qword ptr[rbx]
            state.Assert(ctx.MkEq(rdx, ctx.MkSelect(mem, rbx)));
            // cmp rax, rbx;
            // jnz label1:
            state.Assert(ctx.MkEq(rax, rbx));
            // cmp rax, 0
            // jnz label2:
            state.Assert(ctx.MkEq(rcx, bv_16));

            #region Write to console
            Console.WriteLine("mem=" + mem);

            Solver solver = ctx.MkSolver();
            Solver solver_U = ctx.MkSolver();

            solver.Assert(state.Formulas);
            Console.WriteLine("state1=" + state);
            Console.WriteLine(string.Empty);
            Tv[] raxTV = ToolsZ3.GetTvArray(rax, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rax = " + ToolsZ3.ToStringBin(raxTV) + " = " + ToolsZ3.ToStringHex(raxTV));
            Tv[] rbxTV = ToolsZ3.GetTvArray(rbx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.ToStringBin(rbxTV) + " = " + ToolsZ3.ToStringHex(rbxTV));
            Tv[] rcxTV = ToolsZ3.GetTvArray(rcx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.ToStringBin(rcxTV) + " = " + ToolsZ3.ToStringHex(rcxTV));
            Tv[] rdxTV = ToolsZ3.GetTvArray(rdx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.ToStringBin(rdxTV) + " = " + ToolsZ3.ToStringHex(rdxTV));
            #endregion
        }

        [TestMethod]
        public void Test_Z3_MemWithArray_4()
        {
            #region Definitions
            uint nBits = 8;
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, nBits);
            BitVecExpr bv_16 = ctx.MkBV(16, nBits);
            BitVecExpr bv_32 = ctx.MkBV(32, nBits);

            BitVecExpr rax = ctx.MkBVConst("RAX!0", nBits);
            BitVecExpr rbx = ctx.MkBVConst("RBX!0", nBits);
            BitVecExpr rcx = ctx.MkBVConst("RCX!0", nBits);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0", nBits);

            IList<(BitVecExpr, BitVecExpr)> writes = new List<(BitVecExpr, BitVecExpr)>();

            Goal state = ctx.MkGoal();
            ArrayExpr mem = ctx.MkArrayConst("mem", ctx.MkBitVecSort(nBits), ctx.MkBitVecSort(nBits));

            #endregion

            // Test if overwriting a address works

            Console.WriteLine("mov rbx, 16");
            state.Assert(ctx.MkEq(rbx, bv_16));
            Console.WriteLine("mov rcx, 32");
            state.Assert(ctx.MkEq(rcx, bv_32));
            // Console.WriteLine("mov rax, 0");
            // state.Assert(ctx.MkEq(rax, bv_0));
            Console.WriteLine("mov qword ptr[rax], rbx");
            mem = ctx.MkStore(mem, rax, rbx);
            Console.WriteLine("mov qword ptr[rax], rcx");
            mem = ctx.MkStore(mem, rax, rcx);
            Console.WriteLine("mov rdx, qword ptr[rax]");
            state.Assert(ctx.MkEq(rdx, ctx.MkSelect(mem, rax)));

            #region Write to console

            Solver solver = ctx.MkSolver();
            Solver solver_U = ctx.MkSolver();

            solver.Assert(state.Formulas);
            Console.WriteLine(string.Empty);
            Console.WriteLine("state1=" + state);
            if (true)
            {
                Tactic tactic1 = ctx.MkTactic("propagate-values");
                Goal state2 = tactic1.Apply(state).Subgoals[0];
                Console.WriteLine("state2=" + state2.ToString());
            }
            Console.WriteLine(string.Empty);
            Tv[] raxTV = ToolsZ3.GetTvArray(rax, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rax = " + ToolsZ3.ToStringBin(raxTV) + " = " + ToolsZ3.ToStringHex(raxTV));
            Tv[] rbxTV = ToolsZ3.GetTvArray(rbx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rbx = " + ToolsZ3.ToStringBin(rbxTV) + " = " + ToolsZ3.ToStringHex(rbxTV));
            Tv[] rcxTV = ToolsZ3.GetTvArray(rcx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rcx = " + ToolsZ3.ToStringBin(rcxTV) + " = " + ToolsZ3.ToStringHex(rcxTV));
            Tv[] rdxTV = ToolsZ3.GetTvArray(rdx, (int)nBits, solver, solver_U, ctx);
            Console.WriteLine("rdx = " + ToolsZ3.ToStringBin(rdxTV) + " = " + ToolsZ3.ToStringHex(rdxTV));
            #endregion
        }
        #endregion

        [TestMethod]
        public void Test_Z3_MemWithImplies()
        {
            Context ctx = new Context();

            BitVecExpr bv_0 = ctx.MkBV(0, 64);
            BitVecExpr bv_10 = ctx.MkBV(10, 64);
            BitVecExpr bv_20 = ctx.MkBV(20, 64);

            BitVecExpr addr = ctx.MkBVConst("ADDR", 64);
            BitVecExpr value = ctx.MkBVConst("VALUE", 64);

            BitVecExpr value1 = ctx.MkBVConst("value1", 64);
            BitVecExpr value2 = ctx.MkBVConst("value2", 64);

            BitVecExpr rax = ctx.MkBVConst("RAX!0-1955042C05A090D2", 64);
            BitVecExpr rbx = ctx.MkBVConst("RBX!1-5000C87A5EB2FB98", 64);
            BitVecExpr rcx = ctx.MkBVConst("RCX!1-68FC98BF6AFBF63E", 64);
            BitVecExpr rdx = ctx.MkBVConst("RDX!0-231D57E228F579AD", 64);

            Goal state = ctx.MkGoal();
            // mov qword ptr[0], 10
            state.Assert(ctx.MkImplies(ctx.MkEq(addr, bv_0), ctx.MkEq(value, bv_10)));
            // mov qword ptr[10], 20
            state.Assert(ctx.MkImplies(ctx.MkEq(addr, bv_10), ctx.MkEq(value, bv_20)));
            // mov rbx, qword ptr[rax]
            // state.Assert(ctx.MkEq(rbx, ctx.MkEq(addr, value1));
            // mov rcx, qword ptr[rbx]
            // state.Assert(ctx.MkEq(rcx, ctx.MkEq(addr, value2));
            // cmp rax, 10;
            // jnz label1:
            state.Assert(ctx.MkEq(rax, bv_0));

            Tactic tactic1 = ctx.MkTactic("propagate-values");
            Console.WriteLine("state1=" + state);
            Console.WriteLine("state2=" + tactic1.Apply(state).ToString());

            Solver solver = ctx.MkSolver();
            Solver solver_U = ctx.MkSolver();

            solver.Assert(state.Formulas);
            Tv[] f = ToolsZ3.GetTvArray(value, 64, solver, solver_U, ctx);
            Console.WriteLine("Value = 0b" + ToolsZ3.ToStringBin(f) + " = 0x" + ToolsZ3.ToStringHex(f));
        }

        [TestMethod]
        public void Test_Z3_GoalsAndProbes()
        {
            using (Context ctx = new Context())
            {
                Solver solver = ctx.MkSolver();

                #region Print all Probes
                if (false)
                {
                    for (int i = 0; i < ctx.ProbeNames.Length; ++i)
                    {
                        Console.WriteLine(i + ": probe " + ctx.ProbeNames[i] + "; " + ctx.ProbeDescription(ctx.ProbeNames[i]));
                    }
                    /*
                    0: probe is-quasi-pb; true if the goal is quasi-pb.
                    1: probe is-unbounded; true if the goal contains integer/real constants that do not have lower/upper bounds.
                    2: probe is-pb; true if the goal is a pseudo-boolean problem.
                    3: probe arith-max-deg; max polynomial total degree of an arithmetic atom.
                    4: probe arith-avg-deg; avg polynomial total degree of an arithmetic atom.
                    5: probe arith-max-bw; max coefficient bit width.
                    6: probe arith-avg-bw; avg coefficient bit width.
                    7: probe is-qflia; true if the goal is in QF_LIA.
                    8: probe is-qfauflia; true if the goal is in QF_AUFLIA.
                    9: probe is-qflra; true if the goal is in QF_LRA.
                    10: probe is-qflira; true if the goal is in QF_LIRA.
                    11: probe is-ilp; true if the goal is ILP.
                    12: probe is-qfnia; true if the goal is in QF_NIA (quantifier-free nonlinear integer arithmetic).
                    13: probe is-qfnra; true if the goal is in QF_NRA (quantifier-free nonlinear real arithmetic).
                    14: probe is-nia; true if the goal is in NIA (nonlinear integer arithmetic, formula may have quantifiers).
                    15: probe is-nra; true if the goal is in NRA (nonlinear real arithmetic, formula may have quantifiers).
                    16: probe is-nira; true if the goal is in NIRA (nonlinear integer and real arithmetic, formula may have quantifiers).
                    17: probe is-lia; true if the goal is in LIA (linear integer arithmetic, formula may have quantifiers).
                    18: probe is-lra; true if the goal is in LRA (linear real arithmetic, formula may have quantifiers).
                    19: probe is-lira; true if the goal is in LIRA (linear integer and real arithmetic, formula may have quantifiers).
                    20: probe is-qfufnra; true if the goal is QF_UFNRA (quantifier-free nonlinear real arithmetic with other theories).
                    21: probe memory; ammount of used memory in megabytes.
                    22: probe depth; depth of the input goal.
                    23: probe size; number of assertions in the given goal.
                    24: probe num-exprs; number of expressions/terms in the given goal.
                    25: probe num-consts; number of non Boolean constants in the given goal.
                    26: probe num-bool-consts; number of Boolean constants in the given goal.
                    27: probe num-arith-consts; number of arithmetic constants in the given goal.
                    28: probe num-bv-consts; number of bit-vector constants in the given goal.
                    29: probe produce-proofs; true if proof generation is enabled for the given goal.
                    30: probe produce-model; true if model generation is enabled for the given goal.
                    31: probe produce-unsat-cores; true if unsat-core generation is enabled for the given goal.
                    32: probe has-patterns; true if the goal contains quantifiers with patterns.
                    33: probe is-propositional; true if the goal is in propositional logic.
                    34: probe is-qfbv; true if the goal is in QF_BV.
                    35: probe is-qfaufbv; true if the goal is in QF_AUFBV.
                    36: probe is-qfbv-eq; true if the goal is in a fragment of QF_BV which uses only =, extract, concat.
                    37: probe is-qffp; true if the goal is in QF_FP (floats).
                    38: probe is-qffpbv; true if the goal is in QF_FPBV (floats+bit-vectors).
                     */
                }
                #endregion Print all Probes
                #region Print all Tactics
                if (false)
                {
                    for (int i = 0; i < ctx.TacticNames.Length; ++i)
                    {
                        Console.WriteLine(i + ": tactic " + ctx.TacticNames[i] + "; " + ctx.TacticDescription(ctx.TacticNames[i]));
                    }
                    /*
                    0: tactic qfbv; builtin strategy for solving QF_BV problems.
                    1: tactic qflia; builtin strategy for solving QF_LIA problems.
                    2: tactic qflra; builtin strategy for solving QF_LRA problems.
                    3: tactic qfnia; builtin strategy for solving QF_NIA problems.
                    4: tactic qfnra; builtin strategy for solving QF_NRA problems.
                    5: tactic qfufnra; builtin strategy for solving QF_UNFRA problems.
                    6: tactic add-bounds; add bounds to unbounded variables(under approximation).
                    7: tactic card2bv; convert pseudo-boolean constraints to bit - vectors.
                    8: tactic degree-shift; try to reduce degree of polynomials(remark: :mul2power simplification is automatically applied).
                    9: tactic diff-neq; specialized solver for integer arithmetic problems that contain only atoms of the form(<= k x)(<= x k) and(not(= (-x y) k)), where x and y are constants and k is a numberal, and all constants are bounded.
                    10: tactic elim01; eliminate 0 - 1 integer variables, replace them by Booleans.
                    11: tactic eq2bv; convert integer variables used as finite domain elements to bit-vectors.
                    12: tactic factor; polynomial factorization.
                    13: tactic fix-dl - var; if goal is in the difference logic fragment, then fix the variable with the most number of occurrences at 0.
                    14: tactic fm; eliminate variables using fourier - motzkin elimination.
                    15: tactic lia2card; introduce cardinality constraints from 0 - 1 integer.
                    16: tactic lia2pb; convert bounded integer variables into a sequence of 0 - 1 variables.
                    17: tactic nla2bv; convert a nonlinear arithmetic problem into a bit-vector problem, in most cases the resultant goal is an under approximation and is useul for finding models.
                    18: tactic normalize - bounds; replace a variable x with lower bound k <= x with x' = x - k.
                    19: tactic pb2bv; convert pseudo-boolean constraints to bit - vectors.
                    20: tactic propagate-ineqs; propagate ineqs/ bounds, remove subsumed inequalities.
                    21: tactic purify-arith; eliminate unnecessary operators: -, /, div, mod, rem, is- int, to - int, ^, root - objects.
                    22: tactic recover-01; recover 0 - 1 variables hidden as Boolean variables.
                    23: tactic blast-term-ite; blast term if-then -else by hoisting them.
                    24: tactic cofactor-term-ite; eliminate term if-the -else using cofactors.
                    25: tactic ctx-simplify; apply contextual simplification rules.
                    26: tactic der; destructive equality resolution.
                    27: tactic distribute-forall; distribute forall over conjunctions.
                    28: tactic elim-term-ite; eliminate term if-then -else by adding fresh auxiliary declarations.
                    29: tactic elim-uncnstr; eliminate application containing unconstrained variables.
                    30: tactic snf; put goal in skolem normal form.
                    31: tactic nnf; put goal in negation normal form.
                    32: tactic occf; put goal in one constraint per clause normal form (notes: fails if proof generation is enabled; only clauses are considered).
                    33: tactic pb-preprocess; pre - process pseudo - Boolean constraints a la Davis Putnam.
                    34: tactic propagate-values; propagate constants.
                    35: tactic reduce-args; reduce the number of arguments of function applications, when for all occurrences of a function f the i - th is a value.
                    36: tactic simplify; apply simplification rules.
                    37: tactic elim-and; convert(and a b) into(not(or(not a)(not b))).
                    38: tactic solve-eqs; eliminate variables by solving equations.
                    39: tactic split-clause; split a clause in many subgoals.
                    40: tactic symmetry-reduce; apply symmetry reduction.
                    41: tactic tseitin-cnf; convert goal into CNF using tseitin - like encoding(note: quantifiers are ignored).
                    42: tactic tseitin-cnf-core; convert goal into CNF using tseitin - like encoding(note: quantifiers are ignored).This tactic does not apply required simplifications to the input goal like the tseitin - cnf tactic.
                    43: tactic skip; do nothing tactic.
                    44: tactic fail; always fail tactic.
                    45: tactic fail-if-undecided; fail if goal is undecided.
                    46: tactic bit-blast; reduce bit-vector expressions into SAT.
                    47: tactic bv1-blast; reduce bit-vector expressions into bit - vectors of size 1(notes: only equality, extract and concat are supported).
                    48: tactic reduce-bv-size; try to reduce bit - vector sizes using inequalities.
                    49: tactic max-bv-sharing; use heuristics to maximize the sharing of bit-vector expressions such as adders and multipliers.
                    50: tactic nlsat; (try to) solve goal using a nonlinear arithmetic solver.
                    51: tactic qfnra-nlsat; builtin strategy for solving QF_NRA problems using only nlsat.
                    52: tactic sat; (try to) solve goal using a SAT solver.
                    53: tactic sat-preprocess; Apply SAT solver preprocessing procedures(bounded resolution, Boolean constant propagation, 2 - SAT, subsumption, subsumption resolution).
                    54: tactic ctx-solver-simplify; apply solver-based contextual simplification rules.
                    55: tactic smt; apply a SAT based SMT solver.
                    56: tactic unit-subsume-simplify; unit subsumption simplification.
                    57: tactic aig; simplify Boolean structure using AIGs.
                    58: tactic horn; apply tactic for horn clauses.
                    59: tactic horn-simplify; simplify horn clauses.
                    60: tactic qe-light; apply light-weight quantifier elimination.
                    61: tactic qe-sat; check satisfiability of quantified formulas using quantifier elimination.
                    62: tactic qe; apply quantifier elimination.
                    63: tactic vsubst; checks satsifiability of quantifier-free non - linear constraints using virtual substitution.
                    64: tactic nl-purify; Decompose goal into pure NL-sat formula and formula over other theories.
                    65: tactic macro-finder; Identifies and applies macros.
                    66: tactic quasi-macros; Identifies and applies quasi-macros.
                    67: tactic bv; builtin strategy for solving BV problems(with quantifiers).
                    68: tactic ufbv; builtin strategy for solving UFBV problems(with quantifiers).
                    69: tactic fpa2bv; convert floating point numbers to bit-vectors.
                    70: tactic qffp; (try to) solve goal using the tactic for QF_FP.
                    71: tactic qffpbv; (try to) solve goal using the tactic for QF_FPBV(floats+bit-vectors).
                    72: tactic qfbv-sls; (try to) solve using stochastic local search for QF_BV.
                    73: tactic subpaving; tactic for testing subpaving module.
                    */
                }
                #endregion Print all Tactics

                BitVecExpr rax = ctx.MkBVConst("rax", 64);
                BitVecExpr rbx = ctx.MkBVConst("rbx", 64);

                BoolExpr a1 = ctx.MkEq(rax, ctx.MkBV(0, 64));
                BoolExpr a2 = ctx.MkEq(rbx, rax);

                Goal goal1 = ctx.MkGoal(true, false, false);
                goal1.Assert(a1, a2);
                Console.WriteLine("goal1=" + goal1 + "; inconsistent=" + goal1.Inconsistent);

                Tactic tactic1 = ctx.MkTactic("simplify");
                // Console.WriteLine("tactic1=" + tactic1.ToString());
                ApplyResult applyResult = tactic1.Apply(goal1);

                Console.WriteLine("applyResult=" + applyResult.ToString() + "; nSubGoals=" + applyResult.NumSubgoals);

                // Console.WriteLine("AsBoolExpr=" + goal1.AsBoolExpr());

                #region Probe Tests
                if (false)
                {
                    Probe probe1 = ctx.MkProbe("is-qfbv");
                    double d = probe1.Apply(goal1);
                    Console.WriteLine("d=" + d);
                }
                #endregion Probe Tests
            }
        }
    }
}
