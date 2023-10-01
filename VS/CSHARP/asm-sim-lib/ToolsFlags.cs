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

namespace AsmSim
{
    using System;
    using System.Diagnostics.Contracts;
    using Microsoft.Z3;

    public static class ToolsFlags
    {
        #region Carry Flag
        public static BoolExpr Create_CF_Add(BitVecExpr a, BitVecExpr b, uint nBits, Context ctx)
        {
            Contract.Requires(ctx != null);

            BitVecExpr ax = ctx.MkZeroExt(1, a);
            BitVecExpr bx = ctx.MkZeroExt(1, b);
            BitVecExpr sum = ctx.MkBVAdd(ax, bx);
            return ToolsZ3.GetBit(sum, nBits, ctx.MkBV(1, 1), ctx);
            //return ctx.MkNot(ctx.MkBVAddNoOverflow(a, b, false));
        }

        public static BoolExpr Create_CF_Sub(BitVecExpr a, BitVecExpr b, uint nBits, Context ctx)
        {
            Contract.Requires(ctx != null);

            BitVecExpr ax = ctx.MkZeroExt(1, a);
            BitVecExpr bx = ctx.MkZeroExt(1, b);
            BitVecNum oNE = ctx.MkBV(1, 1);
            return ToolsZ3.GetBit(ctx.MkBVSub(ax, bx), nBits, oNE, ctx);
            //return ctx.MkNot(ctx.MkBVSubNoUnderflow(a, b, false));
        }
        #endregion

        #region Auxiliary Flag
        public static BoolExpr Create_AF_Add(BitVecExpr a, BitVecExpr b, Context ctx)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Contract.Requires(ctx != null);
            Contract.Requires(a.SortSize >= 4);
            Contract.Requires(b.SortSize >= 4);

            return Create_CF_Add(ctx.MkExtract(3, 0, a), ctx.MkExtract(3, 0, b), 4, ctx);
        }

        public static BoolExpr Create_AF_Sub(BitVecExpr a, BitVecExpr b, Context ctx)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Contract.Requires(ctx != null);
            Contract.Requires(a.SortSize >= 4);
            Contract.Requires(b.SortSize >= 4);

            return Create_CF_Sub(ctx.MkExtract(3, 0, a), ctx.MkExtract(3, 0, b), 4, ctx);
        }
        #endregion

        #region Overflow Flag
        public static BoolExpr Create_OF_Add(BitVecExpr a, BitVecExpr b, uint nBits, Context ctx)
        {
            //return ctx.MkNot(ctx.MkBVAddNoOverflow(a, b, true));
            Contract.Requires(ctx != null, "State Context cannot be null");
            return Create_OF(a, b, ctx.MkBVAdd(a, b), nBits, ctx);
        }

        public static BoolExpr Create_OF_Sub(BitVecExpr a, BitVecExpr b, uint nBits, Context ctx)
        {
            //return ctx.MkNot(ctx.MkBVSubNoUnderflow(a, b, true));
            Contract.Requires(ctx != null, "State Context cannot be null");
            return Create_OF(a, b, ctx.MkBVSub(a, b), nBits, ctx);
        }

        public static BoolExpr Create_OF_Mul(BitVecExpr a, BitVecExpr b, uint nBits, Context ctx)
        {
            //return ctx.MkNot(ctx.MkBVMulNoOverflow(a, b, true));
            Contract.Requires(ctx != null, "State Context cannot be null");
            return Create_OF(a, b, ctx.MkBVMul(a, b), nBits, ctx);
        }

        private static BoolExpr Create_OF(BitVecExpr a, BitVecExpr b, BitVecExpr result, uint nBits, Context ctx)
        {
            Contract.Requires(a != null, "BitVecExpr a cannot be null");
            Contract.Requires(b != null, "BitVecExpr a cannot be null");
            Contract.Requires(result != null, "BitVecExpr result cannot be null");
            Contract.Requires(ctx != null, "State Context cannot be null");

            Contract.Requires(a.SortSize == b.SortSize, "number of bits of a and b should be equal");
            Contract.Requires(a.SortSize == result.SortSize, "number of bits of a and result should be equal");
            Contract.Requires(nBits <= a.SortSize);

            using (BitVecExpr signA = Create_SF_BV(a, nBits, ctx))
            using (BitVecExpr signB = Create_SF_BV(b, nBits, ctx))
            using (BitVecExpr signC = Create_SF_BV(result, nBits, ctx))
            using (BitVecExpr oNE = ctx.MkBV(1, 1))
            {
                return ctx.MkAnd(ctx.MkEq(signA, signB), ctx.MkEq(ctx.MkBVXOR(signA, signC), oNE));
            }
        }
        #endregion

        public static BoolExpr Create_SF(BitVecExpr value, uint nBits, Context ctx)
        {
            Contract.Requires(value != null, "BitVecExpr value cannot be null");
            Contract.Requires(ctx != null, "Context cannot be null");
            Contract.Requires(nBits <= value.SortSize);
            Contract.Requires(nBits >= 1);
            uint bitPos = nBits - 1;
            return ToolsZ3.GetBit(value, bitPos, ctx.MkBV(1, 1), ctx);
        }

        public static BitVecExpr Create_SF_BV(BitVecExpr value, uint nBits, Context ctx)
        {
            Contract.Requires(value != null, "BitVecExpr value cannot be null");
            Contract.Requires(ctx != null, "Context cannot be null");
            Contract.Requires(nBits <= value.SortSize);
            Contract.Requires(nBits >= 1);
            uint bitPos = nBits - 1;
            return ToolsZ3.GetBit_BV(value, bitPos, ctx);
        }

        public static BoolExpr Create_PF(BitVecExpr value, Context ctx)
        {
            Contract.Requires(value != null, "BitVecExpr value cannot be null");
            Contract.Requires(ctx != null, "Context cannot be null");
            BitVecExpr v01 = ctx.MkBVAdd(ToolsZ3.GetBit_BV(value, 0, ctx), ToolsZ3.GetBit_BV(value, 1, ctx));
            BitVecExpr v23 = ctx.MkBVAdd(ToolsZ3.GetBit_BV(value, 2, ctx), ToolsZ3.GetBit_BV(value, 3, ctx));
            BitVecExpr v45 = ctx.MkBVAdd(ToolsZ3.GetBit_BV(value, 4, ctx), ToolsZ3.GetBit_BV(value, 5, ctx));
            BitVecExpr v67 = ctx.MkBVAdd(ToolsZ3.GetBit_BV(value, 6, ctx), ToolsZ3.GetBit_BV(value, 7, ctx));
            BitVecExpr v0123 = ctx.MkBVAdd(v01, v23);
            BitVecExpr v4567 = ctx.MkBVAdd(v45, v67);
            BitVecExpr v01234567 = ctx.MkBVAdd(v0123, v4567);
            return ctx.MkEq(v01234567, ctx.MkBV(0, 1));
        }

        public static BoolExpr Create_ZF(BitVecExpr value, Context ctx)
        {
            Contract.Requires(value != null, "BitVecExpr value cannot be null");
            Contract.Requires(ctx != null, "Context cannot be null");
            return ctx.MkEq(value, ctx.MkBV(0, value.SortSize));
        }
    }
}
