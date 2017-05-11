using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;
using AsmSim;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_FPTools
    {
        [TestMethod]
        public void Test_FPTools_BV_2_Doubles()
        {
            Context ctx = new Context();
            Solver solver = ctx.MkSolver();
            FPSort fpSort64 = ctx.MkFPSort64();

            FPExpr[] values_FP = new FPExpr[]{ ctx.MkFP(2, fpSort64), ctx.MkFP(4, fpSort64)};

            BitVecExpr value_BV = ToolsFloatingPoint.FP_2_BV(values_FP, ctx);
            Console.WriteLine(ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(value_BV, 128, solver, ctx)));

            IList<FPExpr> results_FP = new List<FPExpr>(ToolsFloatingPoint.BV_2_Doubles(value_BV, ctx));

            for (int i = 0; i<values_FP.Length; ++i)
            {
                string expected = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(ctx.MkFPToIEEEBV(values_FP[i]), 64, solver, ctx));
                string actual = ToolsZ3.ToStringBin(ToolsZ3.GetTvArray(ctx.MkFPToIEEEBV(results_FP[i]), 64, solver, ctx));
                Assert.AreEqual(expected, actual);
            }
        }
    }
}
