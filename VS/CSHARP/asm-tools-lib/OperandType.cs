// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmTools;

using System;
using System.ComponentModel;
using System.Numerics;
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
/// Per-operand index (0..3) of an <see cref="Ot1"/> value. Used only to compute the single-bit position of
/// an <see cref="Ot2"/>/<see cref="Ot3"/> value, so those members can read by operand name
/// (<c>OtIdx.reg</c>, …) while staying genuine one-bit flags. Must equal <c>BitOperations.Log2((uint)Ot1.x)</c>
/// — the mapping <see cref="AsmSourceTools.OtIndex"/> uses at runtime in <c>MergeOt</c>.
/// </summary>
internal static class OtIdx
{
    public const int reg = 0;
    public const int mem = 1;
    public const int imm = 2;
    public const int UNKNOWN = 3;
}

/// <summary>
/// Operand Type tuple (OperandType x OperandType), used as a SET of allowed operand forms: callers OR
/// several values together (e.g. <c>Ot2.reg_reg | Ot2.reg_mem</c>) and test membership with HasFlag.
/// </summary>
/// <remarks>
/// INVARIANT: every value is a SINGLE distinct bit. Bit position = (operand1 index * 4) + operand2 index.
/// This is what makes OR-combining + HasFlag a correct set: a 2-bit "packed nibble" encoding
/// (<c>Ot1.reg | (Ot1.mem &lt;&lt; 4)</c>) would make HasFlag report phantom members — e.g.
/// <c>reg_mem | mem_reg</c> would falsely contain <c>reg_reg</c>. Do not "compress" these back into nibbles.
/// </remarks>
[Flags]
public enum Ot2
{
    [Description("Reg-Reg")] reg_reg = 1 << ((OtIdx.reg * 4) + OtIdx.reg),
    [Description("Reg-Mem")] reg_mem = 1 << ((OtIdx.reg * 4) + OtIdx.mem),
    [Description("Reg-Imm")] reg_imm = 1 << ((OtIdx.reg * 4) + OtIdx.imm),
    [Description("Reg-Unknown")] reg_UNKNOWN = 1 << ((OtIdx.reg * 4) + OtIdx.UNKNOWN),

    [Description("Mem-Reg")] mem_reg = 1 << ((OtIdx.mem * 4) + OtIdx.reg),
    [Description("Mem-Mem")] mem_mem = 1 << ((OtIdx.mem * 4) + OtIdx.mem),
    [Description("Mem-Imm")] mem_imm = 1 << ((OtIdx.mem * 4) + OtIdx.imm),
    [Description("Mem-Unknown")] mem_UNKNOWN = 1 << ((OtIdx.mem * 4) + OtIdx.UNKNOWN),

    [Description("Imm-Reg")] imm_reg = 1 << ((OtIdx.imm * 4) + OtIdx.reg),
    [Description("Imm-Mem")] imm_mem = 1 << ((OtIdx.imm * 4) + OtIdx.mem),
    [Description("Imm-Imm")] imm_imm = 1 << ((OtIdx.imm * 4) + OtIdx.imm),
    [Description("Imm-Unknown")] imm_UNKNOWN = 1 << ((OtIdx.imm * 4) + OtIdx.UNKNOWN),

    [Description("Unknown-Reg")] UNKNOWN_reg = 1 << ((OtIdx.UNKNOWN * 4) + OtIdx.reg),
    [Description("Unknown-Mem")] UNKNOWN_mem = 1 << ((OtIdx.UNKNOWN * 4) + OtIdx.mem),
    [Description("Unknown-Imm")] UNKNOWN_imm = 1 << ((OtIdx.UNKNOWN * 4) + OtIdx.imm),
    [Description("Unknown-Unknown")] UNKNOWN_UNKNOWN = 1 << ((OtIdx.UNKNOWN * 4) + OtIdx.UNKNOWN),
}

/// <summary>
/// Operand Type tuple (OperandType x OperandType x OperandType). Same set semantics and single-bit
/// invariant as <see cref="Ot2"/>; bit position = (op1 idx * 16) + (op2 idx * 4) + op3 idx. 64 values
/// require a 64-bit backing store, hence <c>: ulong</c>.
/// </summary>
[Flags]
public enum Ot3 : ulong
{
    reg_reg_reg = 1UL << ((OtIdx.reg * 16) + (OtIdx.reg * 4) + OtIdx.reg),
    reg_mem_reg = 1UL << ((OtIdx.reg * 16) + (OtIdx.mem * 4) + OtIdx.reg),
    reg_imm_reg = 1UL << ((OtIdx.reg * 16) + (OtIdx.imm * 4) + OtIdx.reg),
    reg_UNKNOWN_reg = 1UL << ((OtIdx.reg * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.reg),

    mem_reg_reg = 1UL << ((OtIdx.mem * 16) + (OtIdx.reg * 4) + OtIdx.reg),
    mem_mem_reg = 1UL << ((OtIdx.mem * 16) + (OtIdx.mem * 4) + OtIdx.reg),
    mem_imm_reg = 1UL << ((OtIdx.mem * 16) + (OtIdx.imm * 4) + OtIdx.reg),
    mem_UNKNOWN_reg = 1UL << ((OtIdx.mem * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.reg),

    imm_reg_reg = 1UL << ((OtIdx.imm * 16) + (OtIdx.reg * 4) + OtIdx.reg),
    imm_mem_reg = 1UL << ((OtIdx.imm * 16) + (OtIdx.mem * 4) + OtIdx.reg),
    imm_imm_reg = 1UL << ((OtIdx.imm * 16) + (OtIdx.imm * 4) + OtIdx.reg),
    imm_UNKNOWN_reg = 1UL << ((OtIdx.imm * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.reg),

    UNKNOWN_reg_reg = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.reg * 4) + OtIdx.reg),
    UNKNOWN_mem_reg = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.mem * 4) + OtIdx.reg),
    UNKNOWN_imm_reg = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.imm * 4) + OtIdx.reg),
    UNKNOWN_UNKNOWN_reg = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.reg),
    //
    // NOTE: reg_rem_mem is a pre-existing member-name typo for reg_reg_mem (op1=reg, op2=reg, op3=mem);
    // kept as-is because call sites reference this spelling.
    reg_rem_mem = 1UL << ((OtIdx.reg * 16) + (OtIdx.reg * 4) + OtIdx.mem),
    reg_mem_mem = 1UL << ((OtIdx.reg * 16) + (OtIdx.mem * 4) + OtIdx.mem),
    reg_imm_mem = 1UL << ((OtIdx.reg * 16) + (OtIdx.imm * 4) + OtIdx.mem),
    reg_UNKNOWN_mem = 1UL << ((OtIdx.reg * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.mem),

    mem_reg_mem = 1UL << ((OtIdx.mem * 16) + (OtIdx.reg * 4) + OtIdx.mem),
    mem_mem_mem = 1UL << ((OtIdx.mem * 16) + (OtIdx.mem * 4) + OtIdx.mem),
    mem_imm_mem = 1UL << ((OtIdx.mem * 16) + (OtIdx.imm * 4) + OtIdx.mem),
    mem_UNKNOWN_mem = 1UL << ((OtIdx.mem * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.mem),

    imm_reg_mem = 1UL << ((OtIdx.imm * 16) + (OtIdx.reg * 4) + OtIdx.mem),
    imm_mem_mem = 1UL << ((OtIdx.imm * 16) + (OtIdx.mem * 4) + OtIdx.mem),
    imm_imm_mem = 1UL << ((OtIdx.imm * 16) + (OtIdx.imm * 4) + OtIdx.mem),
    imm_UNKNOWN_mem = 1UL << ((OtIdx.imm * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.mem),

    UNKNOWN_reg_mem = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.reg * 4) + OtIdx.mem),
    UNKNOWN_mem_mem = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.mem * 4) + OtIdx.mem),
    UNKNOWN_imm_mem = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.imm * 4) + OtIdx.mem),
    UNKNOWN_UNKNOWN_mem = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.mem),
    //
    reg_reg_imm = 1UL << ((OtIdx.reg * 16) + (OtIdx.reg * 4) + OtIdx.imm),
    reg_mem_imm = 1UL << ((OtIdx.reg * 16) + (OtIdx.mem * 4) + OtIdx.imm),
    reg_imm_imm = 1UL << ((OtIdx.reg * 16) + (OtIdx.imm * 4) + OtIdx.imm),
    reg_UNKNOWN_imm = 1UL << ((OtIdx.reg * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.imm),

    mem_reg_imm = 1UL << ((OtIdx.mem * 16) + (OtIdx.reg * 4) + OtIdx.imm),
    mem_mem_imm = 1UL << ((OtIdx.mem * 16) + (OtIdx.mem * 4) + OtIdx.imm),
    mem_imm_imm = 1UL << ((OtIdx.mem * 16) + (OtIdx.imm * 4) + OtIdx.imm),
    mem_UNKNOWN_imm = 1UL << ((OtIdx.mem * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.imm),

    imm_reg_imm = 1UL << ((OtIdx.imm * 16) + (OtIdx.reg * 4) + OtIdx.imm),
    imm_mem_imm = 1UL << ((OtIdx.imm * 16) + (OtIdx.mem * 4) + OtIdx.imm),
    imm_imm_imm = 1UL << ((OtIdx.imm * 16) + (OtIdx.imm * 4) + OtIdx.imm),
    imm_UNKNOWN_imm = 1UL << ((OtIdx.imm * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.imm),

    UNKNOWN_reg_imm = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.reg * 4) + OtIdx.imm),
    UNKNOWN_mem_imm = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.mem * 4) + OtIdx.imm),
    UNKNOWN_imm_imm = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.imm * 4) + OtIdx.imm),
    UNKNOWN_UNKNOWN_imm = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.imm),
    //
    reg_reg_UNKNOWN = 1UL << ((OtIdx.reg * 16) + (OtIdx.reg * 4) + OtIdx.UNKNOWN),
    reg_mem_UNKNOWN = 1UL << ((OtIdx.reg * 16) + (OtIdx.mem * 4) + OtIdx.UNKNOWN),
    reg_imm_UNKNOWN = 1UL << ((OtIdx.reg * 16) + (OtIdx.imm * 4) + OtIdx.UNKNOWN),
    reg_UNKNOWN_UNKNOWN = 1UL << ((OtIdx.reg * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.UNKNOWN),

    mem_reg_UNKNOWN = 1UL << ((OtIdx.mem * 16) + (OtIdx.reg * 4) + OtIdx.UNKNOWN),
    mem_mem_UNKNOWN = 1UL << ((OtIdx.mem * 16) + (OtIdx.mem * 4) + OtIdx.UNKNOWN),
    mem_imm_UNKNOWN = 1UL << ((OtIdx.mem * 16) + (OtIdx.imm * 4) + OtIdx.UNKNOWN),
    mem_UNKNOWN_UNKNOWN = 1UL << ((OtIdx.mem * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.UNKNOWN),

    imm_reg_UNKNOWN = 1UL << ((OtIdx.imm * 16) + (OtIdx.reg * 4) + OtIdx.UNKNOWN),
    imm_mem_UNKNOWN = 1UL << ((OtIdx.imm * 16) + (OtIdx.mem * 4) + OtIdx.UNKNOWN),
    imm_imm_UNKNOWN = 1UL << ((OtIdx.imm * 16) + (OtIdx.imm * 4) + OtIdx.UNKNOWN),
    imm_UNKNOWN_UNKNOWN = 1UL << ((OtIdx.imm * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.UNKNOWN),

    UNKNOWN_reg_UNKNOWN = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.reg * 4) + OtIdx.UNKNOWN),
    UNKNOWN_mem_UNKNOWN = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.mem * 4) + OtIdx.UNKNOWN),
    UNKNOWN_imm_UNKNOWN = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.imm * 4) + OtIdx.UNKNOWN),
    UNKNOWN_UNKNOWN_UNKNOWN = 1UL << ((OtIdx.UNKNOWN * 16) + (OtIdx.UNKNOWN * 4) + OtIdx.UNKNOWN),
}

public static partial class AsmSourceTools
{
    public static string ToString(Ot1 ot)
    {
        StringBuilder sb = new();
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
        StringBuilder sb = new();
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
        StringBuilder sb = new();
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
        return optup switch
        {
            Ot2.reg_reg => (Ot1.reg, Ot1.reg),
            Ot2.reg_mem => (Ot1.reg, Ot1.mem),
            Ot2.reg_imm => (Ot1.reg, Ot1.imm),
            Ot2.reg_UNKNOWN => (Ot1.reg, Ot1.UNKNOWN),
            Ot2.mem_reg => (Ot1.mem, Ot1.reg),
            Ot2.mem_mem => (Ot1.mem, Ot1.mem),
            Ot2.mem_imm => (Ot1.mem, Ot1.imm),
            Ot2.mem_UNKNOWN => (Ot1.mem, Ot1.UNKNOWN),
            Ot2.imm_reg => (Ot1.imm, Ot1.reg),
            Ot2.imm_mem => (Ot1.imm, Ot1.mem),
            Ot2.imm_imm => (Ot1.imm, Ot1.imm),
            Ot2.imm_UNKNOWN => (Ot1.imm, Ot1.UNKNOWN),
            Ot2.UNKNOWN_reg => (Ot1.UNKNOWN, Ot1.reg),
            Ot2.UNKNOWN_mem => (Ot1.UNKNOWN, Ot1.mem),
            Ot2.UNKNOWN_imm => (Ot1.UNKNOWN, Ot1.imm),
            _ => (Ot1.UNKNOWN, Ot1.UNKNOWN),
        };
    }

    /// <summary>Index 0..3 of a single <see cref="Ot1"/> bit (reg=0, mem=1, imm=2, UNKNOWN=3); see <see cref="OtIdx"/>.</summary>
    private static int OtIndex(Ot1 ot) => BitOperations.Log2((uint)ot);

    public static Ot2 MergeOt(Ot1 ot1, Ot1 ot2)
    {
        return (Ot2)(1 << ((OtIndex(ot1) * 4) + OtIndex(ot2)));
    }

    public static Ot3 MergeOt(Ot1 ot1, Ot1 ot2, Ot1 ot3)
    {
        return (Ot3)(1UL << ((OtIndex(ot1) * 16) + (OtIndex(ot2) * 4) + OtIndex(ot3)));
    }
}
