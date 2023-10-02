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
    public class Test_FPTools
    {
        [TestMethod]
        public void Test_FPTools_BV_2_Doubles()
        {
            Context ctx = new Context(); // housekeeping OK!
            Solver solver = ctx.MkSolver();
            FPSort fpSort64 = ctx.MkFPSort64();

            FPExpr[] values_FP = new FPExpr[] { ctx.MkFP(2, fpSort64), ctx.MkFP(4, fpSort64) };

            BitVecExpr value_BV = ToolsFloatingPoint.FP_2_BV(values_FP, ctx);
            Console.WriteLine(ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(value_BV, 128, solver, ctx)));

            IList<FPExpr> results_FP = new List<FPExpr>(ToolsFloatingPoint.BV_2_Doubles(value_BV, ctx));

            for (int i = 0; i < values_FP.Length; ++i)
            {
                string expected = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(ctx.MkFPToIEEEBV(values_FP[i]), 64, solver, ctx));
                string actual = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(ctx.MkFPToIEEEBV(results_FP[i]), 64, solver, ctx));
                Assert.AreEqual(expected, actual);
            }
            ctx.Dispose();
        }
    }
}
