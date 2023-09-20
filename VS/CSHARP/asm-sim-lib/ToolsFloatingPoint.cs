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

namespace AsmSim
{
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
    using System.Diagnostics.Contracts;
    using Microsoft.Z3;

    public static class ToolsFloatingPoint
    {
        public static IEnumerable<FPExpr> BV_2_Doubles(BitVecExpr value, Context ctx)
        {
            Contract.Requires(value != null);
            Contract.Requires(ctx != null);

            uint nBits = value.SortSize;

            if (nBits == 128)
            {
                uint pos = nBits;
                {
                    BitVecExpr sgn = ctx.MkExtract(pos - 1, pos - 1, value);
                    BitVecExpr sig = ctx.MkExtract(pos - 2, pos - 13, value);
                    BitVecExpr exp = ctx.MkExtract(pos - 14, pos - 64, value);
                    yield return ctx.MkFP(sgn, sig, exp);
                }
                pos -= 64;
                {
                    BitVecExpr sgn = ctx.MkExtract(pos - 1, pos - 1, value);
                    BitVecExpr sig = ctx.MkExtract(pos - 2, pos - 13, value);
                    BitVecExpr exp = ctx.MkExtract(pos - 14, pos - 64, value);
                    yield return ctx.MkFP(sgn, sig, exp);
                }
            }
            else if (nBits == 256)
            {
            }
            else if (nBits == 512)
            {
            }
            else
            {
                throw new Exception();
            }
        }

        public static BitVecExpr FP_2_BV(IEnumerable<FPExpr> fps, Context ctx)
        {
            Contract.Requires(fps != null);
            Contract.Requires(ctx != null);

            BitVecExpr result = null;
            foreach (FPExpr fp in fps)
            {
                result = (result == null) ? ctx.MkFPToIEEEBV(fp) : ctx.MkConcat(result, ctx.MkFPToIEEEBV(fp));
            }
            return result;
        }
    }
}
