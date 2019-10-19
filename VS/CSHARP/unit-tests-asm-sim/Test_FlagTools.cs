using AsmSim;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
    [TestClass]
    public class Test_FlagTools
    {
        private Context CreateContext()
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
                { "model", "false" },         // enable model generation
                { "proof", "false" }         // enable proof generation
            };
            return new Context(settings);
        }

        [TestMethod]
        public void Test_FlagTools_Create_OF_Add()
        {
            Context ctx = this.CreateContext();
            {
                uint nBits = 8;
                ulong a = 10;
                ulong b = 20;

                BitVecExpr aExpr = ctx.MkBV(a, nBits);
                BitVecExpr bExpr = ctx.MkBV(b, nBits);

                BoolExpr resultExpr = ToolsFlags.Create_OF_Add(aExpr, bExpr, nBits, ctx).Simplify() as BoolExpr;
                Assert.IsTrue(AsmTestTools.Calc_OF_Add(nBits, a, b) ? resultExpr.IsTrue : resultExpr.IsFalse);
            }
            ctx.Dispose();
        }
    }
}
