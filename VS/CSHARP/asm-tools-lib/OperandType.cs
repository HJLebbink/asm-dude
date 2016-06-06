using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {

    /// <summary>
    /// Operand Type: reg, mem, imm, UNKNOWN
    /// </summary>
    [Flags]
    public enum Ot : byte {
        reg     = 1 << 0,
        mem     = 1 << 1,
        imm     = 1 << 2,
        UNKNOWN = 1 << 3
    }

    /// <summary>
    /// Operand Type tuple (OperandType x OperandType)
    /// </summary>
    [Flags]
    public enum Ot2 : byte {
        [Description("Reg-Reg")]
        reg_reg = Ot.reg | (Ot.reg << 4),
        [Description("Reg-Mem")]
        reg_mem = Ot.reg | (Ot.mem << 4),
        [Description("Reg-Imm")]
        reg_imm = Ot.reg | (Ot.imm << 4),
        [Description("Reg-Unknown")]
        reg_UNKNOWN = Ot.reg | (Ot.UNKNOWN << 4),

        [Description("Mem-Reg")]
        mem_reg = Ot.mem | (Ot.reg << 4),
        [Description("Mem-Mem")]
        mem_mem = Ot.mem | (Ot.mem << 4),
        [Description("Mem-Imm")]
        mem_imm = Ot.mem | (Ot.imm << 4),
        [Description("Mem-Unknown")]
        mem_UNKNOWN = Ot.mem | (Ot.UNKNOWN << 4),

        [Description("Imm-Reg")]
        imm_reg = Ot.imm | (Ot.reg << 4),
        [Description("Imm-Mem")]
        imm_mem = Ot.imm | (Ot.mem << 4),
        [Description("Imm-Imm")]
        imm_imm = Ot.imm | (Ot.imm << 4),
        [Description("Imm-Unknown")]
        imm_UNKNOWN = Ot.imm | (Ot.UNKNOWN << 4),

        [Description("Unknown-Reg")]
        UNKNOWN_reg = Ot.UNKNOWN | (Ot.reg << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_mem = Ot.UNKNOWN | (Ot.mem << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_imm = Ot.UNKNOWN | (Ot.imm << 4),
        [Description("Unknown-Unknown")]
        UNKNOWN_UNKNOWN = Ot.UNKNOWN | (Ot.UNKNOWN << 4),
    }
    /// <summary>
    ///  Operand Type tuple (OperandType x OperandType x OperandType)
    /// </summary>
    [Flags]
    public enum Ot3 : short {
        reg_reg_reg = Ot.reg | (Ot.reg << 4) | (Ot.reg << 8),
        reg_mem_reg = Ot.reg | (Ot.mem << 4) | (Ot.reg << 8),
        reg_imm_reg = Ot.reg | (Ot.imm << 4) | (Ot.reg << 8),
        reg_UNKNOWN_reg = Ot.reg | (Ot.UNKNOWN << 4) | (Ot.reg << 8),

        mem_reg_reg = Ot.mem | (Ot.reg << 4) | (Ot.reg << 8),
        mem_mem_reg = Ot.mem | (Ot.mem << 4) | (Ot.reg << 8),
        mem_imm_reg = Ot.mem | (Ot.imm << 4) | (Ot.reg << 8),
        mem_UNKNOWN_reg = Ot.mem | (Ot.UNKNOWN << 4) | (Ot.reg << 8),

        imm_reg_reg = Ot.imm | (Ot.reg << 4) | (Ot.reg << 8),
        imm_mem_reg = Ot.imm | (Ot.mem << 4) | (Ot.reg << 8),
        imm_imm_reg = Ot.imm | (Ot.imm << 4) | (Ot.reg << 8),
        imm_UNKNOWN_reg = Ot.imm | (Ot.UNKNOWN << 4) | (Ot.reg << 8),

        UNKNOWN_reg_reg = Ot.UNKNOWN | (Ot.reg << 4) | (Ot.reg << 8),
        UNKNOWN_mem_reg = Ot.UNKNOWN | (Ot.mem << 4) | (Ot.reg << 8),
        UNKNOWN_imm_reg = Ot.UNKNOWN | (Ot.imm << 4) | (Ot.reg << 8),
        UNKNOWN_UNKNOWN_reg = Ot.UNKNOWN | (Ot.UNKNOWN << 4) | (Ot.reg << 8),
        ///
        reg_rem_mem = Ot.reg | (Ot.reg << 4) | (Ot.mem << 8),
        reg_mem_mem = Ot.reg | (Ot.mem << 4) | (Ot.mem << 8),
        reg_imm_mem = Ot.reg | (Ot.imm << 4) | (Ot.mem << 8),
        reg_UNKNOWN_mem = Ot.reg | (Ot.UNKNOWN << 4) | (Ot.mem << 8),

        mem_reg_mem = Ot.mem | (Ot.reg << 4) | (Ot.mem << 8),
        mem_mem_mem = Ot.mem | (Ot.mem << 4) | (Ot.mem << 8),
        mem_imm_mem = Ot.mem | (Ot.imm << 4) | (Ot.mem << 8),
        mem_UNKNOWN_mem = Ot.mem | (Ot.UNKNOWN << 4) | (Ot.mem << 8),

        imm_reg_mem = Ot.imm | (Ot.reg << 4) | (Ot.mem << 8),
        imm_mem_mem = Ot.imm | (Ot.mem << 4) | (Ot.mem << 8),
        imm_imm_mem = Ot.imm | (Ot.imm << 4) | (Ot.mem << 8),
        imm_UNKNOWN_mem = Ot.imm | (Ot.UNKNOWN << 4) | (Ot.mem << 8),

        UNKNOWN_reg_mem = Ot.UNKNOWN | (Ot.reg << 4) | (Ot.mem << 8),
        UNKNOWN_mem_mem = Ot.UNKNOWN | (Ot.mem << 4) | (Ot.mem << 8),
        UNKNOWN_imm_mem = Ot.UNKNOWN | (Ot.imm << 4) | (Ot.mem << 8),
        UNKNOWN_UNKNOWN_mem = Ot.UNKNOWN | (Ot.UNKNOWN << 4) | (Ot.mem << 8),
        ///
        reg_reg_imm = Ot.reg | (Ot.reg << 4) | (Ot.imm << 8),
        reg_mem_imm = Ot.reg | (Ot.mem << 4) | (Ot.imm << 8),
        reg_imm_imm = Ot.reg | (Ot.imm << 4) | (Ot.imm << 8),
        reg_UNKNOWN_imm = Ot.reg | (Ot.UNKNOWN << 4) | (Ot.imm << 8),

        mem_reg_imm = Ot.mem | (Ot.reg << 4) | (Ot.imm << 8),
        mem_mem_imm = Ot.mem | (Ot.mem << 4) | (Ot.imm << 8),
        mem_imm_imm = Ot.mem | (Ot.imm << 4) | (Ot.imm << 8),
        mem_UNKNOWN_imm = Ot.mem | (Ot.UNKNOWN << 4) | (Ot.imm << 8),

        imm_reg_imm = Ot.imm | (Ot.reg << 4) | (Ot.imm << 8),
        imm_mem_imm = Ot.imm | (Ot.mem << 4) | (Ot.imm << 8),
        imm_imm_imm = Ot.imm | (Ot.imm << 4) | (Ot.imm << 8),
        imm_UNKNOWN_imm = Ot.imm | (Ot.UNKNOWN << 4) | (Ot.imm << 8),

        UNKNOWN_reg_imm = Ot.UNKNOWN | (Ot.reg << 4) | (Ot.imm << 8),
        UNKNOWN_mem_imm = Ot.UNKNOWN | (Ot.mem << 4) | (Ot.imm << 8),
        UNKNOWN_imm_imm = Ot.UNKNOWN | (Ot.imm << 4) | (Ot.imm << 8),
        UNKNOWN_UNKNOWN_imm = Ot.UNKNOWN | (Ot.UNKNOWN << 4) | (Ot.imm << 8),
        ///
        reg_reg_UNKNOWN = Ot.reg | (Ot.reg << 4) | (Ot.UNKNOWN << 8),
        reg_mem_UNKNOWN = Ot.reg | (Ot.mem << 4) | (Ot.UNKNOWN << 8),
        reg_imm_UNKNOWN = Ot.reg | (Ot.imm << 4) | (Ot.UNKNOWN << 8),
        reg_UNKNOWN_UNKNOWN = Ot.reg | (Ot.UNKNOWN << 4) | (Ot.UNKNOWN << 8),

        mem_reg_UNKNOWN = Ot.mem | (Ot.reg << 4) | (Ot.UNKNOWN << 8),
        mem_mem_UNKNOWN = Ot.mem | (Ot.mem << 4) | (Ot.UNKNOWN << 8),
        mem_imm_UNKNOWN = Ot.mem | (Ot.imm << 4) | (Ot.UNKNOWN << 8),
        mem_UNKNOWN_UNKNOWN = Ot.mem | (Ot.UNKNOWN << 4) | (Ot.UNKNOWN << 8),

        imm_reg_UNKNOWN = Ot.imm | (Ot.reg << 4) | (Ot.UNKNOWN << 8),
        imm_mem_UNKNOWN = Ot.imm | (Ot.mem << 4) | (Ot.UNKNOWN << 8),
        imm_imm_UNKNOWN = Ot.imm | (Ot.imm << 4) | (Ot.UNKNOWN << 8),
        imm_UNKNOWN_UNKNOWN = Ot.imm | (Ot.UNKNOWN << 4) | (Ot.UNKNOWN << 8),

        UNKNOWN_reg_UNKNOWN = Ot.UNKNOWN | (Ot.reg << 4) | (Ot.UNKNOWN << 8),
        UNKNOWN_mem_UNKNOWN = Ot.UNKNOWN | (Ot.mem << 4) | (Ot.UNKNOWN << 8),
        UNKNOWN_imm_UNKNOWN = Ot.UNKNOWN | (Ot.imm << 4) | (Ot.UNKNOWN << 8),
        UNKNOWN_UNKNOWN_UNKNOWN = Ot.UNKNOWN | (Ot.UNKNOWN << 4) | (Ot.UNKNOWN << 8),

    }


    public static partial class AsmSourceTools {

        public static string ToString(Ot ot) {
            StringBuilder sb = new StringBuilder();
            foreach (Ot value in Enum.GetValues(ot.GetType())) {
                if (ot.HasFlag(value)) {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2) sb.Length -= 2;
            return sb.ToString();
        }
        public static string ToString(Ot2 ot2) {
            StringBuilder sb = new StringBuilder();
            foreach (Ot2 value in Enum.GetValues(ot2.GetType())) {
                if (ot2.HasFlag(value)) {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2) sb.Length -= 2;
            return sb.ToString();
        }
        public static string ToString(Ot3 ot) {
            StringBuilder sb = new StringBuilder();
            foreach (Ot3 value in Enum.GetValues(ot.GetType())) {
                if (ot.HasFlag(value)) {
                    sb.Append(value.ToString() + ", ");
                }
            }
            if (sb.Length > 2) sb.Length -= 2;
            return sb.ToString();
        }

        public static Tuple<Ot, Ot> splitOt(Ot2 operandTuple) {
            switch (operandTuple) {
                case Ot2.reg_reg: return new Tuple<Ot, Ot>(Ot.reg, Ot.reg);
                case Ot2.reg_mem: return new Tuple<Ot, Ot>(Ot.reg, Ot.mem);
                case Ot2.reg_imm: return new Tuple<Ot, Ot>(Ot.reg, Ot.imm);
                case Ot2.reg_UNKNOWN: return new Tuple<Ot, Ot>(Ot.reg, Ot.UNKNOWN);
                case Ot2.mem_reg: return new Tuple<Ot, Ot>(Ot.mem, Ot.reg);
                case Ot2.mem_mem: return new Tuple<Ot, Ot>(Ot.mem, Ot.mem);
                case Ot2.mem_imm: return new Tuple<Ot, Ot>(Ot.mem, Ot.imm);
                case Ot2.mem_UNKNOWN: return new Tuple<Ot, Ot>(Ot.mem, Ot.UNKNOWN);
                case Ot2.imm_reg: return new Tuple<Ot, Ot>(Ot.imm, Ot.reg);
                case Ot2.imm_mem: return new Tuple<Ot, Ot>(Ot.imm, Ot.mem);
                case Ot2.imm_imm: return new Tuple<Ot, Ot>(Ot.imm, Ot.imm);
                case Ot2.imm_UNKNOWN: return new Tuple<Ot, Ot>(Ot.imm, Ot.UNKNOWN);
                case Ot2.UNKNOWN_reg: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.reg);
                case Ot2.UNKNOWN_mem: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.mem);
                case Ot2.UNKNOWN_imm: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.imm);
                case Ot2.UNKNOWN_UNKNOWN:
                default:
                    return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.UNKNOWN);
            }
        }
        public static Ot2 mergeOt(Ot ot1, Ot ot2) {
            return (Ot2)(((byte)ot1) | (((byte)ot2) << 4));
        }
        public static Ot2 mergeOt(Ot ot1, Ot ot2, Ot ot3) {
            return (Ot2)(((byte)ot1) | (((byte)ot2) << 4) | (((byte)ot3) << 8));
        }
    }
}
