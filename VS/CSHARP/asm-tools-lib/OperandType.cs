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

namespace AsmTools
{
    using System;
    using System.ComponentModel;
    using System.Text;

    /// <summary>
    /// Operand Type: reg, mem, imm, UNKNOWN
    /// </summary>
    [Flags]
    public enum Ot1
    {
        reg = 1 << 0,
        mem = 1 << 1,
        imm = 1 << 2,
        UNKNOWN = 1 << 3,
    }

    /// <summary>
    /// Operand Type tup (OperandType x OperandType)
    /// </summary>
    [Flags]
    public enum Ot2
    {
        [Description("Reg-Reg")]
        reg_reg = Ot1.reg | (Ot1.reg << 4),
        [Description("Reg-Mem")]
        reg_mem = Ot1.reg | (Ot1.mem << 4),
        [Description("Reg-Imm")]
        reg_imm = Ot1.reg | (Ot1.imm << 4),
        [Description("Reg-Unknown")]
        reg_UNKNOWN = Ot1.reg | (Ot1.UNKNOWN << 4),

        [Description("Mem-Reg")]
        mem_reg = Ot1.mem | (Ot1.reg << 4),
        [Description("Mem-Mem")]
        mem_mem = Ot1.mem | (Ot1.mem << 4),
        [Description("Mem-Imm")]
        mem_imm = Ot1.mem | (Ot1.imm << 4),
        [Description("Mem-Unknown")]
        mem_UNKNOWN = Ot1.mem | (Ot1.UNKNOWN << 4),

        [Description("Imm-Reg")]
        imm_reg = Ot1.imm | (Ot1.reg << 4),
        [Description("Imm-Mem")]
        imm_mem = Ot1.imm | (Ot1.mem << 4),
        [Description("Imm-Imm")]
        imm_imm = Ot1.imm | (Ot1.imm << 4),
        [Description("Imm-Unknown")]
        imm_UNKNOWN = Ot1.imm | (Ot1.UNKNOWN << 4),

        [Description("Unknown-Reg")]
        UNKNOWN_reg = Ot1.UNKNOWN | (Ot1.reg << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_mem = Ot1.UNKNOWN | (Ot1.mem << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_imm = Ot1.UNKNOWN | (Ot1.imm << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_UNKNOWN = Ot1.UNKNOWN | (Ot1.UNKNOWN << 4),
    }
    /// <summary>
    ///  Operand Type tup (OperandType x OperandType x OperandType)
    /// </summary>
    [Flags]
    public enum Ot3
    {
        reg_reg_reg = Ot1.reg | (Ot1.reg << 4) | (Ot1.reg << 8),
        reg_mem_reg = Ot1.reg | (Ot1.mem << 4) | (Ot1.reg << 8),
        reg_imm_reg = Ot1.reg | (Ot1.imm << 4) | (Ot1.reg << 8),
        reg_UNKNOWN_reg = Ot1.reg | (Ot1.UNKNOWN << 4) | (Ot1.reg << 8),

        mem_reg_reg = Ot1.mem | (Ot1.reg << 4) | (Ot1.reg << 8),
        mem_mem_reg = Ot1.mem | (Ot1.mem << 4) | (Ot1.reg << 8),
        mem_imm_reg = Ot1.mem | (Ot1.imm << 4) | (Ot1.reg << 8),
        mem_UNKNOWN_reg = Ot1.mem | (Ot1.UNKNOWN << 4) | (Ot1.reg << 8),

        imm_reg_reg = Ot1.imm | (Ot1.reg << 4) | (Ot1.reg << 8),
        imm_mem_reg = Ot1.imm | (Ot1.mem << 4) | (Ot1.reg << 8),
        imm_imm_reg = Ot1.imm | (Ot1.imm << 4) | (Ot1.reg << 8),
        imm_UNKNOWN_reg = Ot1.imm | (Ot1.UNKNOWN << 4) | (Ot1.reg << 8),

        UNKNOWN_reg_reg = Ot1.UNKNOWN | (Ot1.reg << 4) | (Ot1.reg << 8),
        UNKNOWN_mem_reg = Ot1.UNKNOWN | (Ot1.mem << 4) | (Ot1.reg << 8),
        UNKNOWN_imm_reg = Ot1.UNKNOWN | (Ot1.imm << 4) | (Ot1.reg << 8),
        UNKNOWN_UNKNOWN_reg = Ot1.UNKNOWN | (Ot1.UNKNOWN << 4) | (Ot1.reg << 8),
        //
        reg_rem_mem = Ot1.reg | (Ot1.reg << 4) | (Ot1.mem << 8),
        reg_mem_mem = Ot1.reg | (Ot1.mem << 4) | (Ot1.mem << 8),
        reg_imm_mem = Ot1.reg | (Ot1.imm << 4) | (Ot1.mem << 8),
        reg_UNKNOWN_mem = Ot1.reg | (Ot1.UNKNOWN << 4) | (Ot1.mem << 8),

        mem_reg_mem = Ot1.mem | (Ot1.reg << 4) | (Ot1.mem << 8),
        mem_mem_mem = Ot1.mem | (Ot1.mem << 4) | (Ot1.mem << 8),
        mem_imm_mem = Ot1.mem | (Ot1.imm << 4) | (Ot1.mem << 8),
        mem_UNKNOWN_mem = Ot1.mem | (Ot1.UNKNOWN << 4) | (Ot1.mem << 8),

        imm_reg_mem = Ot1.imm | (Ot1.reg << 4) | (Ot1.mem << 8),
        imm_mem_mem = Ot1.imm | (Ot1.mem << 4) | (Ot1.mem << 8),
        imm_imm_mem = Ot1.imm | (Ot1.imm << 4) | (Ot1.mem << 8),
        imm_UNKNOWN_mem = Ot1.imm | (Ot1.UNKNOWN << 4) | (Ot1.mem << 8),

        UNKNOWN_reg_mem = Ot1.UNKNOWN | (Ot1.reg << 4) | (Ot1.mem << 8),
        UNKNOWN_mem_mem = Ot1.UNKNOWN | (Ot1.mem << 4) | (Ot1.mem << 8),
        UNKNOWN_imm_mem = Ot1.UNKNOWN | (Ot1.imm << 4) | (Ot1.mem << 8),
        UNKNOWN_UNKNOWN_mem = Ot1.UNKNOWN | (Ot1.UNKNOWN << 4) | (Ot1.mem << 8),
        //
        reg_reg_imm = Ot1.reg | (Ot1.reg << 4) | (Ot1.imm << 8),
        reg_mem_imm = Ot1.reg | (Ot1.mem << 4) | (Ot1.imm << 8),
        reg_imm_imm = Ot1.reg | (Ot1.imm << 4) | (Ot1.imm << 8),
        reg_UNKNOWN_imm = Ot1.reg | (Ot1.UNKNOWN << 4) | (Ot1.imm << 8),

        mem_reg_imm = Ot1.mem | (Ot1.reg << 4) | (Ot1.imm << 8),
        mem_mem_imm = Ot1.mem | (Ot1.mem << 4) | (Ot1.imm << 8),
        mem_imm_imm = Ot1.mem | (Ot1.imm << 4) | (Ot1.imm << 8),
        mem_UNKNOWN_imm = Ot1.mem | (Ot1.UNKNOWN << 4) | (Ot1.imm << 8),

        imm_reg_imm = Ot1.imm | (Ot1.reg << 4) | (Ot1.imm << 8),
        imm_mem_imm = Ot1.imm | (Ot1.mem << 4) | (Ot1.imm << 8),
        imm_imm_imm = Ot1.imm | (Ot1.imm << 4) | (Ot1.imm << 8),
        imm_UNKNOWN_imm = Ot1.imm | (Ot1.UNKNOWN << 4) | (Ot1.imm << 8),

        UNKNOWN_reg_imm = Ot1.UNKNOWN | (Ot1.reg << 4) | (Ot1.imm << 8),
        UNKNOWN_mem_imm = Ot1.UNKNOWN | (Ot1.mem << 4) | (Ot1.imm << 8),
        UNKNOWN_imm_imm = Ot1.UNKNOWN | (Ot1.imm << 4) | (Ot1.imm << 8),
        UNKNOWN_UNKNOWN_imm = Ot1.UNKNOWN | (Ot1.UNKNOWN << 4) | (Ot1.imm << 8),
        //
        reg_reg_UNKNOWN = Ot1.reg | (Ot1.reg << 4) | (Ot1.UNKNOWN << 8),
        reg_mem_UNKNOWN = Ot1.reg | (Ot1.mem << 4) | (Ot1.UNKNOWN << 8),
        reg_imm_UNKNOWN = Ot1.reg | (Ot1.imm << 4) | (Ot1.UNKNOWN << 8),
        reg_UNKNOWN_UNKNOWN = Ot1.reg | (Ot1.UNKNOWN << 4) | (Ot1.UNKNOWN << 8),

        mem_reg_UNKNOWN = Ot1.mem | (Ot1.reg << 4) | (Ot1.UNKNOWN << 8),
        mem_mem_UNKNOWN = Ot1.mem | (Ot1.mem << 4) | (Ot1.UNKNOWN << 8),
        mem_imm_UNKNOWN = Ot1.mem | (Ot1.imm << 4) | (Ot1.UNKNOWN << 8),
        mem_UNKNOWN_UNKNOWN = Ot1.mem | (Ot1.UNKNOWN << 4) | (Ot1.UNKNOWN << 8),

        imm_reg_UNKNOWN = Ot1.imm | (Ot1.reg << 4) | (Ot1.UNKNOWN << 8),
        imm_mem_UNKNOWN = Ot1.imm | (Ot1.mem << 4) | (Ot1.UNKNOWN << 8),
        imm_imm_UNKNOWN = Ot1.imm | (Ot1.imm << 4) | (Ot1.UNKNOWN << 8),
        imm_UNKNOWN_UNKNOWN = Ot1.imm | (Ot1.UNKNOWN << 4) | (Ot1.UNKNOWN << 8),

        UNKNOWN_reg_UNKNOWN = Ot1.UNKNOWN | (Ot1.reg << 4) | (Ot1.UNKNOWN << 8),
        UNKNOWN_mem_UNKNOWN = Ot1.UNKNOWN | (Ot1.mem << 4) | (Ot1.UNKNOWN << 8),
        UNKNOWN_imm_UNKNOWN = Ot1.UNKNOWN | (Ot1.imm << 4) | (Ot1.UNKNOWN << 8),
        UNKNOWN_UNKNOWN_UNKNOWN = Ot1.UNKNOWN | (Ot1.UNKNOWN << 4) | (Ot1.UNKNOWN << 8),
    }

    public static partial class AsmSourceTools
    {
        public static string ToString(Ot1 ot)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Ot1 value in Enum.GetValues(ot.GetType()))
            {
                if (ot.HasFlag(value))
                {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2)
            {
                sb.Length -= 2;
            }

            return sb.ToString();
        }

        public static string ToString(Ot2 ot2)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Ot2 value in Enum.GetValues(ot2.GetType()))
            {
                if (ot2.HasFlag(value))
                {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2)
            {
                sb.Length -= 2;
            }

            return sb.ToString();
        }

        public static string ToString(Ot3 ot)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Ot3 value in Enum.GetValues(ot.GetType()))
            {
                if (ot.HasFlag(value))
                {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2)
            {
                sb.Length -= 2;
            }

            return sb.ToString();
        }

        public static (Ot1 operand1, Ot1 operand2) SplitOt(Ot2 optup)
        {
            switch (optup)
            {
                case Ot2.reg_reg: return (Ot1.reg, Ot1.reg);
                case Ot2.reg_mem: return (Ot1.reg, Ot1.mem);
                case Ot2.reg_imm: return (Ot1.reg, Ot1.imm);
                case Ot2.reg_UNKNOWN: return (Ot1.reg, Ot1.UNKNOWN);
                case Ot2.mem_reg: return (Ot1.mem, Ot1.reg);
                case Ot2.mem_mem: return (Ot1.mem, Ot1.mem);
                case Ot2.mem_imm: return (Ot1.mem, Ot1.imm);
                case Ot2.mem_UNKNOWN: return (Ot1.mem, Ot1.UNKNOWN);
                case Ot2.imm_reg: return (Ot1.imm, Ot1.reg);
                case Ot2.imm_mem: return (Ot1.imm, Ot1.mem);
                case Ot2.imm_imm: return (Ot1.imm, Ot1.imm);
                case Ot2.imm_UNKNOWN: return (Ot1.imm, Ot1.UNKNOWN);
                case Ot2.UNKNOWN_reg: return (Ot1.UNKNOWN, Ot1.reg);
                case Ot2.UNKNOWN_mem: return (Ot1.UNKNOWN, Ot1.mem);
                case Ot2.UNKNOWN_imm: return (Ot1.UNKNOWN, Ot1.imm);
                case Ot2.UNKNOWN_UNKNOWN:
                default:
                    return (Ot1.UNKNOWN, Ot1.UNKNOWN);
            }
        }

        public static Ot2 MergeOt(Ot1 ot1, Ot1 ot2)
        {
            return (Ot2)(((byte)ot1) | (((byte)ot2) << 4));
        }

        public static Ot3 MergeOt(Ot1 ot1, Ot1 ot2, Ot1 ot3)
        {
            return (Ot3)(((byte)ot1) | (((byte)ot2) << 4) | (((byte)ot3) << 8));
        }
    }
}
