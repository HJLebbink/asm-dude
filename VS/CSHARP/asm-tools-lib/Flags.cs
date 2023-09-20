﻿// The MIT License (MIT)
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

namespace AsmTools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;

    /// <summary>Flags, CF, PF, AF, ZF, SF, OF, DF, IF</summary>
    [Flags]
    public enum Flags
    {
        NONE = 0,
        /// <summary>
        /// CF (bit 0) Carry flag — Status Flag. Set if an arithmetic operation generates a carry
        /// or a borrow out of the most significant bit of the result; cleared otherwise.
        /// This flag indicates an overflow condition for unsigned-integer arithmetic.
        /// It is also used in multiple-precision arithmetic.
        /// </summary>
        CF = 1 << 0,
        /// <summary>
        /// PF (bit 2) Parity flag — Status Flag. Set if the least-significant byte of the result contains an even number of 1 bits;
        /// cleared otherwise.
        /// </summary>
        PF = 1 << 1,
        /// <summary>
        /// AF (bit 4) Auxiliary Carry flag — Status Flag. Set if an arithmetic operation generates a carry or a borrow out of bit
        /// 3 of the result; cleared otherwise. This flag is used in binary-coded decimal (BCD) arithmetic.
        /// </summary>
        AF = 1 << 2,
        /// <summary>
        /// ZF (bit 6) Zero flag — Status Flag. Set if the result is zero; cleared otherwise.
        /// </summary>
        ZF = 1 << 3,
        /// <summary>
        /// SF (bit 7) Sign flag — Status Flag. Set equal to the most-significant bit of the result, which is the sign bit of a signed
        /// integer. (0 indicates a positive value and 1 indicates a negative value.)
        /// </summary>
        SF = 1 << 4,
        /// <summary>
        /// OF (bit 11) Overflow flag — Status Flag. Set if the integer result is too large a positive number or too small a negative
        /// number (excluding the sign-bit) to fit in the destination operand; cleared otherwise. This
        /// flag indicates an overflow condition for signed-integer (two’s complement) arithmetic.
        /// </summary>
        OF = 1 << 5,
        /// <summary>
        /// DF (bit 10) Direction flag — Control Flag. This flag is used to determine the direction (forward or backward) in which several bytes of data will be copied from one place in the memory, to another. The direction is important mainly when the original data position in memory and the target data position overlap.
        /// </summary>
        DF = 1 << 6,
        /// <summary>
        /// IF (bit 9) Interupt enable flag — Control Flag. If the flag is set to 1, maskable hardware interrupts will be handled. If cleared (set to 0), such interrupts will be ignored. IF does not affect the handling of non-maskable interrupts or software interrupts generated by the INT instruction.
        /// </summary>
        IF = 1 << 7,
        /// <summary>
        /// TF (bit 8) Trap flag — Control Flag.
        /// </summary>
        // TF = 1 << 8,

        ALL = CF | PF | AF | ZF | SF | OF | DF,

        CF_PF_AF_SF_OF = CF | PF | AF | SF | OF,
        CF_PF_AF_ZF_SF_OF = CF | PF | AF | ZF | SF | OF,
        PF_AF_ZF_SF_OF = PF | AF | ZF | SF | OF,
    }

    public static class FlagTools
    {
        /// <summary>Test whether provided flags is a single flag</summary>
        public static bool SingleFlag(Flags flags)
        {
            int intVal = (int)flags;
            return (intVal != 0) && ((intVal & (intVal - 1)) == 0);
        }

        public static Flags Parse(string str, bool strIsCapitals)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            switch (AsmSourceTools.ToCapitals(str, strIsCapitals))
            {
                case "CF": return Flags.CF;
                case "PF": return Flags.PF;
                case "AF": return Flags.AF;
                case "ZF": return Flags.ZF;
                case "SF": return Flags.SF;
                case "OF": return Flags.OF;
                case "DF": return Flags.DF;
                default: return Flags.NONE;
            }
        }

        public static string ToString(Flags flags)
        {
            if (flags == Flags.NONE)
            {
                return "NONE";
            }

            if (flags == Flags.ALL)
            {
                return "ALL";
            }

            StringBuilder sb = new StringBuilder();
            foreach (Flags flag in GetFlags(flags))
            {
                sb.Append(flag).Append('|');
            }
            if (sb.Length > 1)
            {
                sb.Length -= 1; // remove the trailing comma space
            }

            return sb.ToString();
        }

        public static IEnumerable<Flags> GetFlags(Flags flags)
        {
            if (flags.HasFlag(Flags.CF))
            {
                yield return Flags.CF;
            }

            if (flags.HasFlag(Flags.PF))
            {
                yield return Flags.PF;
            }

            if (flags.HasFlag(Flags.AF))
            {
                yield return Flags.AF;
            }

            if (flags.HasFlag(Flags.ZF))
            {
                yield return Flags.ZF;
            }

            if (flags.HasFlag(Flags.SF))
            {
                yield return Flags.SF;
            }

            if (flags.HasFlag(Flags.OF))
            {
                yield return Flags.OF;
            }
        }

        public static IEnumerable<Flags> GetFlags()
        {
            yield return Flags.CF;
            yield return Flags.PF;
            yield return Flags.AF;
            yield return Flags.ZF;
            yield return Flags.SF;
            yield return Flags.OF;
        }
    }
}
