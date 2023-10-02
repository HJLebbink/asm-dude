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

namespace unit_tests_asm_z3
{
    using System.Collections.Generic;
    using AsmSim;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Z3;

    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
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
                { "proof", "false" },         // enable proof generation
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
        }
    }
}
