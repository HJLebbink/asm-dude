using System;
using System.Collections.Generic;
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
        reg_reg = Ot.reg | (Ot.reg << 4),
        reg_mem = Ot.reg | (Ot.mem << 4),
        reg_imm = Ot.reg | (Ot.imm << 4),
        reg_UNKNOWN = Ot.reg | (Ot.UNKNOWN << 4),

        mem_reg = Ot.mem | (Ot.reg << 4),
        mem_mem = Ot.mem | (Ot.mem << 4),
        mem_imm = Ot.mem | (Ot.imm << 4),
        mem_UNKNOWN = Ot.mem | (Ot.UNKNOWN << 4),

        imm_reg = Ot.imm | (Ot.reg << 4),
        imm_mem = Ot.imm | (Ot.mem << 4),
        imm_imm = Ot.imm | (Ot.imm << 4),
        imm_UNKNOWN = Ot.imm | (Ot.UNKNOWN << 4),

        UNKNOWN_reg = Ot.UNKNOWN | (Ot.reg << 4),
        UNKNOWN_mem = Ot.UNKNOWN | (Ot.mem << 4),
        UNKNOWN_imm = Ot.UNKNOWN | (Ot.imm << 4),
        UNKNOWN_UNKNOWN = Ot.UNKNOWN | (Ot.UNKNOWN << 4),
    }

    public static partial class Tools {
        public static Tuple<Ot, Ot> splitOt(Ot2 operandTuple) {
            switch (operandTuple) {
                case Ot2.reg_reg: return new Tuple<Ot, Ot>(Ot.reg, Ot.reg);
                case Ot2.reg_mem: return new Tuple<Ot, Ot>(Ot.reg, Ot.mem);
                case Ot2.reg_imm: return new Tuple<Ot, Ot>(Ot.reg, Ot.imm);
                case Ot2.reg_UNKNOWN: return new Tuple<Ot, Ot>(Ot.reg, Ot.UNKNOWN);
                case Ot2.mem_reg: return new Tuple<Ot, Ot>(Ot.mem, Ot.reg);
                case Ot2.mem_mem: return new Tuple<Ot, Ot>(Ot.mem, Ot.mem);
                case Ot2.mem_imm: return new Tuple<Ot, Ot>(Ot.mem, Ot.imm);
                case Ot2.mem_UNKNOWN: return new Tuple<Ot, Ot>(Ot.mem, Ot.reg);
                case Ot2.imm_reg: return new Tuple<Ot, Ot>(Ot.imm, Ot.reg);
                case Ot2.imm_mem: return new Tuple<Ot, Ot>(Ot.imm, Ot.mem);
                case Ot2.imm_imm: return new Tuple<Ot, Ot>(Ot.imm, Ot.imm);
                case Ot2.imm_UNKNOWN: return new Tuple<Ot, Ot>(Ot.imm, Ot.reg);
                case Ot2.UNKNOWN_reg: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.reg);
                case Ot2.UNKNOWN_mem: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.mem);
                case Ot2.UNKNOWN_imm: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.imm);
                case Ot2.UNKNOWN_UNKNOWN:
                default:
                    return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.reg);
            }
        }
        public static Ot2 mergeOt(Ot ot1, Ot ot2) {
            return (Ot2)(((byte)ot1) | (((byte)ot2) << 4));
        }
    }
}
