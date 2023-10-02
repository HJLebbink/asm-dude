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
    using System.Globalization;
    using AsmSim;
    using AsmTools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Z3;

    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
    public class Test_BitTricks
    {
        private const bool LogToDisplay = AsmTestTools.LOG_TO_DISPLAY;

        private Tools CreateTools(int timeOut = AsmTestTools.DEFAULT_TIMEOUT)
        {
            /* The following parameters can be set:
                    - proof (Boolean) Enable proof generation
                    - debug_ref_count (Boolean) Enable debug support for Z3_ast reference counting
                    - trace (Boolean) Tracing support for VCC
                    - trace_file_name (String) Trace out file for VCC traces
                    - timeout (unsigned) default timeout (in milliseconds) used for solvers
                    - well_sorted_check type checker
                    - auto_config use heuristics to automatically select solver and configure it
                    - model model generation for solvers, this parameter can be overwritten when creating a solver
                    - model_validate validate models produced by solvers
                    - unsat_core unsat-core generation for solvers, this parameter can be overwritten when creating
                            a solver Note that in previous versions of Z3, this constructor was also used to set
                            global and module parameters. For this purpose we should now use
                            Microsoft.Z3.Global.SetParameter(System.String,System.String)
            */

            Dictionary<string, string> settings = new Dictionary<string, string>
            {
                { "unsat_core", "false" },    // enable generation of unsat cores
                { "model", "true" },          // enable model generation
                { "proof", "false" },         // enable proof generation
                { "timeout", timeOut.ToString(CultureInfo.InvariantCulture) },
            };
            return new Tools(settings);
        }

        private State CreateState(Tools tools)
        {
            string tailKey = "!INIT"; // Tools.CreateKey(tools.Rand);
            string headKey = tailKey;
            return new State(tools, tailKey, headKey);
        }

        [TestMethod]
        public void Test_BitTricks_LegatosMultiplier()
        {
            /*
            LDX #8    ; 1; load X immediate with the 8
            LDA #0    ; 2; load A immediate with the 0
            CLC       ; 3; set C to 0 LOOP
    LOOP:   ROR F1    ; 4; rotate F1 right circular through C
            BCC ZCOEF ; 5; branch to ZCOEF if C = 0
            CLC       ; 6; set C to 0
            ADC F2    ; 7; set A to A+F2+C and C to the carry ZCOEF
    ZCOEF:  ROR A     ; 8; rotate A right circular through C
            ROR LOW   ; 9; rotate LOW right circular through C
            DEX       ;10; set X to X-1
            BNE LOOP  ;11; branch to LOOP if Z = 0
            */

            #region Stateconfig
            Tools tools = this.CreateTools(0);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.CF = true;
            tools.StateConfig.ZF = true;

            tools.StateConfig.RAX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.R8 = true;
            tools.StateConfig.R9 = true;
            tools.StateConfig.R10 = true;
            #endregion

            string programStr =
                "           clc                                 " + Environment.NewLine +
                "           ror al, 1                           " + Environment.NewLine +
                "           jnc ZCOEF                           ";

            StaticFlow sFlow = new StaticFlow(tools);
            sFlow.Update(programStr);
            if (LogToDisplay)
            {
                Console.WriteLine(sFlow);
            }

            DynamicFlow dFlow = Runner.Construct_DynamicFlow_Backward(sFlow, tools);
            if (LogToDisplay)
            {
                Console.WriteLine("DynamicFlow:\n" + dFlow.ToString(sFlow));
            }

            State state = dFlow.Create_EndState;
        }

        [TestMethod]
        public void Test_BitTricks_Mod3()
        {
            /*
            mod3_A    PROC
            ; parameter 1: rcx
                mov       r8, 0aaaaaaaaaaaaaaabH      ;; (scaled) reciprocal of 3
                mov       rax, rcx
                mul       r8                          ;; multiply with reciprocal
                shr       rdx, 1                      ;; quotient
                lea       r9, QWORD PTR [rdx+rdx*2]   ;; back multiply with 3
                neg       r9
                add       rcx, r9                     ;; subtract from dividend
                mov       rax, rcx                    ;; remainder
                ret
            mod3_A    ENDP

            mod3_B    PROC
            ; parameter 1: rcx
                mov       r8, 3
                mov       rax, rcx
                xor       rdx, rdx
                idiv      r8
                mov       rax, rdx
                ret
            mod3_B    ENDP
            */

            Tools tools = this.CreateTools(0);
            tools.StateConfig.Set_All_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.R8 = true;
            tools.StateConfig.R9 = true;
            tools.StateConfig.R10 = true;

            string line0 = "mov       rcx, r10";

            string line1 = "mov       r8, 0aaaaaaaaaaaaaaabH";
            string line2 = "mov       rax, rcx";
            string line3 = "mul       r8";
            string line4 = "shr       rdx, 1";
            string line5 = "lea       r9, QWORD PTR [rdx+rdx*2]";
            string line6 = "neg       r9";
            string line7 = "add       rcx, r9"; // rcx has result of

            string line8 = "mov       r8, 3";
            string line9 = "mov       rax, r10";
            string line10 = "mov      rdx, 0";
            string line11 = "idiv     r8";

            if (false)
            {
                State state = this.CreateState(tools);

                state = Runner.SimpleStep_Forward(line0, state);
                state = Runner.SimpleStep_Forward(line1, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line2, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line3, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line3 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line4, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line4 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line5, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line5 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line6, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line6 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line7, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line7 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line8, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line8 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line9, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line9 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line10, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line10 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line11, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line11 + "\", we know:\n" + state);
                }

                Context ctx = state.Ctx;
                BoolExpr t = ctx.MkEq(state.Create(Rn.RCX), state.Create(Rn.RDX));

                if (false)
                {// this test does not seem to terminate
                    state.Solver.Push();
                    state.Solver.Assert(t);
                    if (state.Solver.Check() != Status.SATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("UnsatCore has " + state.Solver.UnsatCore.Length + " elements");
                        }

                        foreach (BoolExpr b in state.Solver.UnsatCore)
                        {
                            if (LogToDisplay)
                            {
                                Console.WriteLine("UnsatCore=" + b);
                            }
                        }
                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                if (true)
                { // this test does not seem to terminate
                    state.Solver.Push();
                    state.Solver.Assert(ctx.MkNot(t));
                    if (state.Solver.Check() == Status.SATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("Model=" + state.Solver.Model);
                        }

                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                Assert.AreEqual(Tv.ONE, ToolsZ3.GetTv(t, state.Solver, state.Ctx));
            }
        }

        [TestMethod]
        public void Test_BitTricks_Min_Unsigned()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Reg_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RDX = true;
            tools.StateConfig.CF = true;

            string line1 = "sub rax, rbx";
            string line2 = "sbb rdx, rdx"; // copy CF to all bits of edx
            string line3 = "and rdx, rax";
            string line4 = "add rbx, rdx";

            { // forward
                State state = this.CreateState(tools);

                BitVecExpr rax0 = state.Create(Rn.RAX);
                BitVecExpr rbx0 = state.Create(Rn.RBX);

                state = Runner.SimpleStep_Forward(line1, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line2, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line3, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line3 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line4, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line4 + "\", we know:\n" + state);
                }

                // ebx is minimum of ebx and eax
                Context ctx = state.Ctx;
                BitVecExpr rbx1 = state.Create(Rn.RBX);
                rax0 = rax0.Translate(ctx) as BitVecExpr;
                rbx0 = rbx0.Translate(ctx) as BitVecExpr;
                BoolExpr t = ctx.MkEq(rbx1, ctx.MkITE(ctx.MkBVUGT(rax0, rbx0), rbx0, rax0));

                {
                    state.Solver.Push();
                    state.Solver.Assert(t);
                    if (state.Solver.Check() != Status.SATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("UnsatCore has " + state.Solver.UnsatCore.Length + " elements");
                        }

                        foreach (BoolExpr b in state.Solver.UnsatCore)
                        {
                            if (LogToDisplay)
                            {
                                Console.WriteLine("UnsatCore=" + b);
                            }
                        }
                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                {
                    state.Solver.Push();
                    state.Solver.Assert(ctx.MkNot(t));
                    if (state.Solver.Check() == Status.SATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("Model=" + state.Solver.Model);
                        }

                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                Assert.AreEqual(Tv.ONE, ToolsZ3.GetTv(t, state.Solver, state.Ctx));
            }
        }

        [TestMethod]
        public void Test_BitTricks_Min_Signed()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Reg_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RDX = true;

            return; // this trick does not seem to be correct?!

            string line1 = "sub rax, rbx";  // Will not work if overflow here!
            string line2 = "cqo";           // rdx1 = (rax0 > rbx0) ? -1 : 0
            string line3 = "and rdx, rax";  // rdx2 = (rax0 > rbx0) ? 0 : (rax0 - rbx0)
            string line4 = "add rbx, rdx";  // rbx1 = (rax0 > rbx0) ? (rbx0 + 0) : (rbx0 + rax0 - rbx0)

            { // forward
                State state = this.CreateState(tools);
                Context ctx = state.Ctx;

                if (true)
                {
                    ulong rax_value = 0x61a4292198602827;
                    ulong rbx_value = 0x8739140220c24080;
                    StateUpdate updateState = new StateUpdate("!PREVKEY", "!NEXTKEY", state.Tools);
                    updateState.Set(Rn.RAX, rax_value);
                    updateState.Set(Rn.RBX, rbx_value);
                    state.Update_Forward(updateState);
                    if (LogToDisplay)
                    {
                        Console.WriteLine("Initially, we know:\n" + state);
                    }
                }

                BitVecExpr rax0 = state.Create(Rn.RAX);
                BitVecExpr rbx0 = state.Create(Rn.RBX);

                {
                    state.Solver.Assert(state.Ctx.MkNot(ToolsFlags.Create_OF_Sub(rax0, rbx0, rax0.SortSize, ctx))); // this code only works when there is no overflow in line1
                }
                { // line 1
                    state = Runner.SimpleStep_Forward(line1, state);
                    // retrieve the overflow after line 1, OF has to be zero for the code to work
                    state.Solver.AssertAndTrack(ctx.MkNot(state.Create(Flags.OF)), ctx.MkBoolConst("OF-ZERO"));
                    Assert.AreEqual(Status.SATISFIABLE, state.Solver.Check());
                    if (LogToDisplay)
                    {
                        Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
                    }
                }
                { // line 2
                    state = Runner.SimpleStep_Forward(line2, state);
                    // if (logToDisplay) Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
                    BoolExpr t2 = ctx.MkEq(state.Create(Rn.RDX), ctx.MkITE(ctx.MkBVSGT(rax0, rbx0), ctx.MkBV(0xFFFF_FFFF_FFFF_FFFF, 64), ctx.MkBV(0, 64)));
                    // Assert.AreEqual(Tv5.ONE, ToolsZ3.GetTv5(t2, state.Solver, state.Ctx));
                }
                {
                    state = Runner.SimpleStep_Forward(line3, state);
                    // if (logToDisplay) Console.WriteLine("After \"" + line3 + "\", we know:\n" + state);
                    // BoolExpr t2 = ctx.MkEq(state.Get(Rn.RDX), ctx.MkITE(ctx.MkBVSGT(rax0, rbx0), ctx.MkBV(0, 64), ctx.MkBVSub(rax0, rbx0)));
                    // Assert.AreEqual(Tv5.ONE, ToolsZ3.GetTv5(t2, state.Solver, state.Ctx));
                }
                {
                    state = Runner.SimpleStep_Forward(line4, state);
                    if (LogToDisplay)
                    {
                        Console.WriteLine("After \"" + line4 + "\", we know:\n" + state);
                    }
                }

                // ebx is minimum of ebx and eax
                BitVecExpr rbx1 = state.Create(Rn.RBX);
                BoolExpr t = ctx.MkEq(rbx1, ctx.MkITE(ctx.MkBVSGT(rax0, rbx0), rbx0, rax0));

                if (false)
                {
                    state.Solver.Push();
                    state.Solver.AssertAndTrack(t, ctx.MkBoolConst("MIN_RAX_RBX"));
                    Status s = state.Solver.Check();
                    if (LogToDisplay)
                    {
                        Console.WriteLine("Status A = " + s + "; expected " + Status.SATISFIABLE);
                    }

                    if (s == Status.UNSATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("UnsatCore has " + state.Solver.UnsatCore.Length + " elements");
                        }

                        foreach (BoolExpr b in state.Solver.UnsatCore)
                        {
                            if (LogToDisplay)
                            {
                                Console.WriteLine("UnsatCore=" + b);
                            }
                        }

                        if (LogToDisplay)
                        {
                            Console.WriteLine(state.Solver);
                        }

                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                if (true)
                {
                    state.Solver.Push();
                    state.Solver.Assert(ctx.MkNot(t), ctx.MkBoolConst("NOT_MIN_RAX_RBX"));
                    Status s = state.Solver.Check();
                    if (LogToDisplay)
                    {
                        Console.WriteLine("Status B = " + s + "; expected " + Status.UNSATISFIABLE);
                    }

                    if (s == Status.SATISFIABLE)
                    {
                        if (LogToDisplay)
                        {
                            Console.WriteLine("Model=" + state.Solver.Model);
                        }

                        Assert.Fail();
                    }
                    state.Solver.Pop();
                }
                Assert.AreEqual(Tv.ONE, ToolsZ3.GetTv(t, state.Solver, state.Ctx));
            }
        }

        [TestMethod]
        public void Test_BitTricks_Parallel_Search_GPR_1()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Reg_Off();
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RDX = true;

            string line1 = "mov ebx, 0x01_00_02_03";        // EBX contains four bytes
            string line2 = "lea ecx, [ebx-0x01_01_01_01]";  // substract 1 from each byte
            string line3 = "not ebx";                       // invert all bytes
            string line4 = "and ecx, ebx";                  // and these two
            string line5 = "and ecx, 80808080h";

            { // forward
                State state = this.CreateState(tools);
                BitVecExpr bytes = state.Create(Rn.EBX);

                if (false)
                { // line 1
                    state = Runner.SimpleStep_Forward(line1, state);
                    // if (logToDisplay) Console.WriteLine("After \"" + line1 + "\", we know:\n" + state);
                }
                state = Runner.SimpleStep_Forward(line2, state);
                // if (logToDisplay) Console.WriteLine("After \"" + line2 + "\", we know:\n" + state);
                state = Runner.SimpleStep_Forward(line3, state);
                // if (logToDisplay) Console.WriteLine("After \"" + line3 + "\", we know:\n" + state);
                state = Runner.SimpleStep_Forward(line4, state);
                // if (logToDisplay) Console.WriteLine("After \"" + line4 + "\", we know:\n" + state);
                state = Runner.SimpleStep_Forward(line5, state);
                // if (logToDisplay) Console.WriteLine("After \"" + line5 + "\", we know:\n" + state);

                Context ctx = state.Ctx;
                BitVecExpr zero = ctx.MkBV(0, 8);
                bytes = bytes.Translate(ctx) as BitVecExpr;
                BitVecExpr byte1 = ctx.MkExtract((1 * 8) - 1, 0 * 8, bytes);
                BitVecExpr byte2 = ctx.MkExtract((2 * 8) - 1, 1 * 8, bytes);
                BitVecExpr byte3 = ctx.MkExtract((3 * 8) - 1, 2 * 8, bytes);
                BitVecExpr byte4 = ctx.MkExtract((4 * 8) - 1, 3 * 8, bytes);

                {
                    // if at least one of the bytes is equal to zero, then ECX cannot be equal to zero
                    // if ECX is zero, then none of the bytes is equal to zero.

                    BoolExpr property = ctx.MkEq(
                        ctx.MkOr(
                            ctx.MkEq(byte1, zero),
                            ctx.MkEq(byte2, zero),
                            ctx.MkEq(byte3, zero),
                            ctx.MkEq(byte4, zero)
                        ),
                        ctx.MkNot(ctx.MkEq(state.Create(Rn.ECX), ctx.MkBV(0, 32)))
                    );
                    AsmTestTools.AreEqual(Tv.ONE, ToolsZ3.GetTv(property, state.Solver, state.Ctx));
                }
                {
                    state.Solver.Push();
                    BoolExpr p = ctx.MkOr(ctx.MkEq(byte1, zero), ctx.MkEq(byte2, zero), ctx.MkEq(byte3, zero), ctx.MkEq(byte4, zero));
                    state.Solver.Assert(p);
                    if (LogToDisplay)
                    {
                        Console.WriteLine("After \"" + p + "\", we know:\n" + state);
                    }

                    state.Solver.Pop();
                }
                {
                    state.Solver.Push();
                    BoolExpr p = ctx.MkAnd(
                        ctx.MkEq(ctx.MkEq(byte1, zero), ctx.MkFalse()),
                        ctx.MkEq(ctx.MkEq(byte2, zero), ctx.MkFalse()),
                        ctx.MkEq(ctx.MkEq(byte3, zero), ctx.MkTrue()),
                        ctx.MkEq(ctx.MkEq(byte4, zero), ctx.MkFalse())
                    );
                    state.Solver.Assert(p);
                    if (LogToDisplay)
                    {
                        Console.WriteLine("After \"" + p + "\", we know:\n" + state);
                    }
                    // state.Solver.Pop();
                }
            }
        }

        [TestMethod]
        public void Test_BitTricks_Parallel_Search_GPR_2()
        {
            Tools tools = this.CreateTools();
            tools.StateConfig.Set_All_Reg_Off();
            tools.StateConfig.RAX = true;
            tools.StateConfig.RBX = true;
            tools.StateConfig.RCX = true;
            tools.StateConfig.RSP = true;

            string line1 = "mov rax, 0x80_80_80_80_80_80_80_80";
            string line2 = "mov rsp, 0x01_01_01_01_01_01_01_01";

            string line3 = "mov rbx, 0x01_02_03_04_05_06_07_08";    // EBX contains 8 bytes
            string line4a = "mov rcx, rbx";             // cannot substract with lea, now we need an extra mov
            string line4b = "sub rcx, rsp";              // substract 1 from each byte
            string line5 = "not rbx";                   // invert all bytes
            string line6 = "and rcx, rbx";              // and these two
            string line7 = "and rcx, rax";

            { // forward
                State state = this.CreateState(tools);
                BitVecExpr bytes = state.Create(Rn.RBX);

                state = Runner.SimpleStep_Forward(line1, state);
                state = Runner.SimpleStep_Forward(line2, state);
                if (false)
                {
                    state = Runner.SimpleStep_Forward(line3, state);
                    if (LogToDisplay)
                    {
                        Console.WriteLine("After \"" + line3 + "\", we know:\n" + state);
                    }
                }
                state = Runner.SimpleStep_Forward(line4a, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line4a + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line4b, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line4b + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line5, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line5 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line6, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line6 + "\", we know:\n" + state);
                }

                state = Runner.SimpleStep_Forward(line7, state);
                if (LogToDisplay)
                {
                    Console.WriteLine("After \"" + line7 + "\", we know:\n" + state);
                }

                {
                    // if at least one of the bytes is equal to zero, then ECX cannot be equal to zero
                    // if ECX is zero, then none of the bytes is equal to zero.
                    Context ctx = state.Ctx;
                    BitVecExpr zero8 = ctx.MkBV(0, 8);
                    bytes = bytes.Translate(ctx) as BitVecExpr;

                    BitVecExpr byte1 = ctx.MkExtract((1 * 8) - 1, 0 * 8, bytes);
                    BitVecExpr byte2 = ctx.MkExtract((2 * 8) - 1, 1 * 8, bytes);
                    BitVecExpr byte3 = ctx.MkExtract((3 * 8) - 1, 2 * 8, bytes);
                    BitVecExpr byte4 = ctx.MkExtract((4 * 8) - 1, 3 * 8, bytes);
                    BitVecExpr byte5 = ctx.MkExtract((5 * 8) - 1, 4 * 8, bytes);
                    BitVecExpr byte6 = ctx.MkExtract((6 * 8) - 1, 5 * 8, bytes);
                    BitVecExpr byte7 = ctx.MkExtract((7 * 8) - 1, 6 * 8, bytes);
                    BitVecExpr byte8 = ctx.MkExtract((8 * 8) - 1, 7 * 8, bytes);

                    BoolExpr property = ctx.MkEq(
                        ctx.MkOr(
                            ctx.MkEq(byte1, zero8),
                            ctx.MkEq(byte2, zero8),
                            ctx.MkEq(byte3, zero8),
                            ctx.MkEq(byte4, zero8),
                            ctx.MkEq(byte5, zero8),
                            ctx.MkEq(byte6, zero8),
                            ctx.MkEq(byte7, zero8),
                            ctx.MkEq(byte8, zero8)
                        ),
                        ctx.MkNot(ctx.MkEq(state.Create(Rn.RCX), ctx.MkBV(0, 64)))
                    );
                    AsmTestTools.AreEqual(Tv.ONE, ToolsZ3.GetTv(property, state.Solver, ctx));
                }
            }
        }
    }
}
