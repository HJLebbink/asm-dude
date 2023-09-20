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

namespace AsmSim
{
    using System;
    using System.Diagnostics.Contracts;
    using AsmTools;
    using Microsoft.Z3;

    public static class BitOperations
    {
        #region Logical Function
        public static (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) Neg(
            BitVecExpr a, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Requires(a != null);

            BitVecExpr zero = ctx.MkBV(0, a.SortSize);
            return Substract(zero, a, ctx);
        }

        #endregion

        #region Arithmetic

        public static (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) Addition(
            BitVecExpr a, BitVecExpr b, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Requires(a != null);

            BitVecExpr result = ctx.MkBVAdd(a, b);
            BoolExpr cf = ToolsFlags.Create_CF_Add(a, b, a.SortSize, ctx);
            BoolExpr of = ToolsFlags.Create_OF_Add(a, b, a.SortSize, ctx);
            BoolExpr af = ToolsFlags.Create_AF_Add(a, b, ctx);
            return (result: result, cf: cf, of: of, af: af);
        }

        public static (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) Addition(
            BitVecExpr a, BitVecExpr b, BoolExpr carry, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Requires(a != null);

            //if (carry.IsFalse) return Addition(a, b, ctx);

            uint nBits = a.SortSize;

            BitVecExpr bv0_1bit = ctx.MkBV(0, 1);
            BitVecExpr bv1_1bit = ctx.MkBV(1, 1);

            BitVecExpr ax = ctx.MkZeroExt(1, a);
            BitVecExpr bx = ctx.MkZeroExt(1, b);
            BitVecExpr carryBV = ctx.MkITE(carry, ctx.MkBV(1, nBits + 1), ctx.MkBV(0, nBits + 1)) as BitVecExpr;
            BitVecExpr bx2 = ctx.MkBVAdd(bx, carryBV);
            BitVecExpr rx = ctx.MkBVAdd(ax, bx2);
            BitVecExpr result = ctx.MkExtract(nBits - 1, 0, rx);

            BoolExpr cf = ToolsFlags.Create_CF_Add(ax, bx2, nBits, ctx);
            BoolExpr of = ToolsFlags.Create_OF_Add(ax, bx2, nBits, ctx);
            BoolExpr af = ToolsFlags.Create_AF_Add(ax, bx2, ctx);
            return (result: result, cf: cf, of: of, af: af);
        }

        public static (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) Substract(
            BitVecExpr a, BitVecExpr b, Context ctx)
        {
            Contract.Requires(ctx != null);
            Contract.Requires(a != null);

            uint nBits = a.SortSize;
            BitVecExpr result = ctx.MkBVSub(a, b);
            BoolExpr cf = ToolsFlags.Create_CF_Sub(a, b, nBits, ctx);
            BoolExpr of = ToolsFlags.Create_OF_Sub(a, b, nBits, ctx);
            BoolExpr af = ToolsFlags.Create_AF_Sub(a, b, ctx);
            return (result: result, cf: cf, of: of, af: af);
        }

        public static (BitVecExpr result, BoolExpr cf, BoolExpr of, BoolExpr af) Substract(
            BitVecExpr a, BitVecExpr b, BoolExpr carry, Context ctx)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Contract.Requires(carry != null);
            Contract.Requires(ctx != null);

            if (carry.IsFalse)
            {
                return Substract(a, b, ctx);
            }

            uint nBits = a.SortSize;

            BitVecExpr bv0_1bit = ctx.MkBV(0, 1);
            BitVecExpr bv1_1bit = ctx.MkBV(1, 1);

            BitVecExpr ax = ctx.MkConcat(bv0_1bit, a);
            BitVecExpr bx = ctx.MkConcat(bv0_1bit, b);
            BitVecExpr bx2 = ctx.MkBVAdd(bx, ctx.MkITE(carry, ctx.MkBV(1, nBits + 1), ctx.MkBV(0, nBits + 1)) as BitVecExpr);
            BitVecExpr rx = ctx.MkBVSub(ax, bx2);
            BitVecExpr result = ctx.MkExtract(nBits - 1, 0, rx);

            BoolExpr cf = ToolsFlags.Create_CF_Sub(ax, bx2, nBits, ctx);
            BoolExpr of = ToolsFlags.Create_OF_Sub(ax, bx2, nBits, ctx);
            BoolExpr af = ToolsFlags.Create_AF_Sub(ax, bx2, ctx);
            return (result: result, cf: cf, of: of, af: af);
        }

        #endregion

        #region Shift operations
        public static (BitVecExpr result, BoolExpr cf) ShiftOperations(
            Mnemonic op,
            BitVecExpr value,
            BitVecExpr nShifts,
            Context ctx,
            Random rand)
        {
            Contract.Requires(nShifts != null);
            Contract.Requires(ctx != null);
            Contract.Requires(value != null);
            Contract.Requires(nShifts.SortSize == 8);

            BitVecExpr value_out;

            BitVecNum one = ctx.MkBV(1, 8);
            BitVecExpr nBitsBV = ctx.MkBV(value.SortSize, 8);
            BitVecExpr bitPos;
            BitVecExpr nShifts64 = ctx.MkZeroExt(value.SortSize - 8, nShifts);
            switch (op)
            {
                case Mnemonic.SHR:
                    {
                        bitPos = ctx.MkBVSub(nShifts, one);
                        value_out = ctx.MkBVLSHR(value, nShifts64);
                    }
                    break;
                case Mnemonic.SAR:
                    {
                        bitPos = ctx.MkBVSub(nShifts, one);
                        value_out = ctx.MkBVASHR(value, nShifts64);
                    }
                    break;
                case Mnemonic.ROR:
                    {
                        bitPos = ctx.MkBVSub(nShifts, one);
                        value_out = ctx.MkBVRotateRight(value, nShifts64);
                    }
                    break;
                case Mnemonic.SHL: // SHL and SAL are equal in functionality
                case Mnemonic.SAL:
                    {
                        bitPos = ctx.MkBVSub(nBitsBV, nShifts);
                        //Console.WriteLine("BitOperations:SHL: bitPos=" + bitPos.Simplify());
                        value_out = ctx.MkBVSHL(value, nShifts64);
                    }
                    break;
                case Mnemonic.ROL:
                    {
                        bitPos = ctx.MkBVSub(nBitsBV, nShifts);
                        value_out = ctx.MkBVRotateLeft(value, nShifts64);
                    }
                    break;
                default: throw new Exception();
            }
            bitPos = ctx.MkZeroExt(value.SortSize - 8, bitPos);
            BoolExpr bitValue = ToolsZ3.GetBit(value, bitPos, ctx);

            BoolExpr cF_undef = Tools.Create_Flag_Key_Fresh(Flags.CF, rand, ctx);
            BoolExpr cf = ctx.MkITE(ctx.MkEq(nShifts, ctx.MkBV(0, 8)), cF_undef, bitValue) as BoolExpr;
            return (result: value_out, cf: cf);
        }

        public static (BitVecExpr result, BoolExpr cf) ShiftOperations(
            Mnemonic op,
            BitVecExpr value,
            BitVecExpr nShifts,
            BoolExpr carryIn,
            string prevKey,
            Context ctx)
        {
            Contract.Requires(value != null);
            Contract.Requires(nShifts != null);
            Contract.Requires(ctx != null);
            Contract.Requires(nShifts.SortSize == 8);
            //Console.WriteLine("ShiftOperations:nShifts=" + nShifts);

            uint nBits = value.SortSize;
            BitVecExpr carryBV = ctx.MkITE(carryIn, ctx.MkBV(1, 1), ctx.MkBV(0, 1)) as BitVecExpr;
            BitVecExpr nShifts65 = ctx.MkZeroExt(nBits + 1 - 8, nShifts);

            BitVecExpr value_out;
            BoolExpr bitValue;
            switch (op)
            {
                case Mnemonic.RCR:
                    {
                        BitVecExpr valueWithCarry = ctx.MkConcat(carryBV, value);
                        BitVecExpr rotatedValue = ctx.MkBVRotateRight(valueWithCarry, nShifts65);
                        value_out = ctx.MkExtract(nBits - 1, 0, rotatedValue);
                        bitValue = ToolsZ3.GetBit(rotatedValue, nBits, ctx.MkBV(1, 1), ctx);
                    }
                    break;
                case Mnemonic.RCL:
                    {
                        BitVecExpr valueWithCary = ctx.MkConcat(value, carryBV);
                        BitVecExpr rotatedValue = ctx.MkBVRotateLeft(valueWithCary, nShifts65);
                        value_out = ctx.MkExtract(nBits, 1, rotatedValue);
                        bitValue = ToolsZ3.GetBit(rotatedValue, 0, ctx.MkBV(1, 1), ctx);
                    }
                    break;
                default:
                    throw new Exception();
            }

            BoolExpr cf_current = Tools.Create_Key(Flags.CF, prevKey, ctx);

            BoolExpr cf = ctx.MkITE(ctx.MkEq(nShifts, ctx.MkBV(0, 8)), cf_current, bitValue) as BoolExpr;
            //Console.WriteLine("ShiftOperations:cf=" + cf);
            return (result: value_out, cf: cf);
        }
        #endregion
    }
}
