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
        reg = 1,
        mem = 2,
        imm = 4,
        UNKNOWN = 8
    }

    /// <summary>
    /// Operand Type tuple (OperandType x OperandType)
    /// </summary>
    [Flags]
    public enum Ot2 : byte {
        reg_x_reg = Ot.reg | (Ot.reg << 4),
        reg_x_mem = Ot.reg | (Ot.mem << 4),
        reg_x_imm = Ot.reg | (Ot.imm << 4),
        reg_x_UNKNOWN = Ot.reg | (Ot.UNKNOWN << 4),

        mem_x_reg = Ot.mem | (Ot.reg << 4),
        mem_x_mem = Ot.mem | (Ot.mem << 4),
        mem_x_imm = Ot.mem | (Ot.imm << 4),
        mem_x_UNKNOWN = Ot.mem | (Ot.UNKNOWN << 4),

        imm_x_reg = Ot.imm | (Ot.reg << 4),
        imm_x_mem = Ot.imm | (Ot.mem << 4),
        imm_x_imm = Ot.imm | (Ot.imm << 4),
        imm_x_UNKNOWN = Ot.imm | (Ot.UNKNOWN << 4),

        UNKNOWN_x_reg = Ot.UNKNOWN | (Ot.reg << 4),
        UNKNOWN_x_mem = Ot.UNKNOWN | (Ot.mem << 4),
        UNKNOWN_x_imm = Ot.UNKNOWN | (Ot.imm << 4),
        UNKNOWN_x_UNKNOWN = Ot.UNKNOWN | (Ot.UNKNOWN << 4),
    }

    public static partial class Tools {
        public static Tuple<Ot, Ot> split(Ot2 operandTuple) {
            switch (operandTuple) {
                case Ot2.reg_x_reg: return new Tuple<Ot, Ot>(Ot.reg, Ot.reg);
                case Ot2.reg_x_mem: return new Tuple<Ot, Ot>(Ot.reg, Ot.mem);
                case Ot2.reg_x_imm: return new Tuple<Ot, Ot>(Ot.reg, Ot.imm);
                case Ot2.reg_x_UNKNOWN: return new Tuple<Ot, Ot>(Ot.reg, Ot.UNKNOWN);
                case Ot2.mem_x_reg: return new Tuple<Ot, Ot>(Ot.mem, Ot.reg);
                case Ot2.mem_x_mem: return new Tuple<Ot, Ot>(Ot.mem, Ot.mem);
                case Ot2.mem_x_imm: return new Tuple<Ot, Ot>(Ot.mem, Ot.imm);
                case Ot2.mem_x_UNKNOWN: return new Tuple<Ot, Ot>(Ot.mem, Ot.reg);
                case Ot2.imm_x_reg: return new Tuple<Ot, Ot>(Ot.imm, Ot.reg);
                case Ot2.imm_x_mem: return new Tuple<Ot, Ot>(Ot.imm, Ot.mem);
                case Ot2.imm_x_imm: return new Tuple<Ot, Ot>(Ot.imm, Ot.imm);
                case Ot2.imm_x_UNKNOWN: return new Tuple<Ot, Ot>(Ot.imm, Ot.reg);
                case Ot2.UNKNOWN_x_reg: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.reg);
                case Ot2.UNKNOWN_x_mem: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.mem);
                case Ot2.UNKNOWN_x_imm: return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.imm);
                case Ot2.UNKNOWN_x_UNKNOWN: 
                default:
                    return new Tuple<Ot, Ot>(Ot.UNKNOWN, Ot.reg);
            }
        }

    }
}
