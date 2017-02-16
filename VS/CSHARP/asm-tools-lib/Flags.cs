// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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

namespace AsmTools {

    public static class FlagTools {
        public static bool SingleFlag(Flags flags) {
            int intVal = ((int)flags);
            return (intVal != 0) && ((intVal & (intVal - 1)) == 0);
        }

        public static Flags Parse(string str) {
            switch (str.ToUpper()) {
                case "CF": return Flags.CF;
                case "PF": return Flags.PF;
                case "AF": return Flags.AF;
                case "ZF": return Flags.ZF;
                case "SF": return Flags.SF;
                case "OF": return Flags.OF;
                default: return Flags.NONE;
            }
        }
    }

    public abstract class FlagValue {
        private Bt _value;
        public FlagValue(Bt v) {
            this._value = v;
        }
        public Bt Val { get { return this._value; } set { this._value = value; } }
    }

    public sealed class CarryFlag : FlagValue {
        public CarryFlag(Bt v) : base(v) { }
        public static implicit operator Bt(CarryFlag v) { return v.Val; }
        public static implicit operator CarryFlag(Bt v) { return new CarryFlag(v); }
    }
    public sealed class AuxiliaryFlag : FlagValue {
        public AuxiliaryFlag(Bt v) : base(v) { }
        public static implicit operator Bt(AuxiliaryFlag v) { return v.Val; }
        public static implicit operator AuxiliaryFlag(Bt v) { return new AuxiliaryFlag(v); }
    }
    public sealed class ZeroFlag : FlagValue {
        public ZeroFlag(Bt v) : base(v) { }
        public static implicit operator Bt(ZeroFlag v) { return v.Val; }
        public static implicit operator ZeroFlag(Bt v) { return new ZeroFlag(v); }
    }
    public sealed class SignFlag : FlagValue {
        public SignFlag(Bt v) : base(v) { }
        public static implicit operator Bt(SignFlag v) { return v.Val; }
        public static implicit operator SignFlag(Bt v) { return new SignFlag(v); }
    }
    public sealed class ParityFlag : FlagValue {
        public ParityFlag(Bt v) : base(v) { }
        public static implicit operator Bt(ParityFlag v) { return v.Val; }
        public static implicit operator ParityFlag(Bt v) { return new ParityFlag(v); }
    }
    public sealed class OverflowFlag : FlagValue {
        public OverflowFlag(Bt v) : base(v) { }
        public static implicit operator Bt(OverflowFlag v) { return v.Val; }
        public static implicit operator OverflowFlag(Bt v) { return new OverflowFlag(v); }
    }
    public sealed class DirectionFlag : FlagValue {
        public DirectionFlag(Bt v) : base(v) { }
        public static implicit operator Bt(DirectionFlag v) { return v.Val; }
        public static implicit operator DirectionFlag(Bt v) { return new DirectionFlag(v); }
    }

    /// <summary>
    /// Flags, CF, PF, AF, ZF, SF, OF, DF
    /// </summary>
    public enum Flags : byte {
        NONE = 0,
        /// <summary>
        /// CF (bit 0) Carry flag — Set if an arithmetic operation generates a carry
        /// or a borrow out of the most significant bit of the result; cleared otherwise.
        /// This flag indicates an overflow condition for unsigned-integer arithmetic.
        /// It is also used in multiple-precision arithmetic.
        /// </summary>
        CF = 1 << 0,
        /// <summary>
        /// PF (bit 2) Parity flag — Set if the least-significant byte of the result contains an even number of 1 bits;
        /// cleared otherwise.
        /// </summary>
        PF = 1 << 1,
        /// <summary>
        /// AF (bit 4) Auxiliary Carry flag — Set if an arithmetic operation generates a carry or a borrow out of bit
        /// 3 of the result; cleared otherwise. This flag is used in binary-coded decimal (BCD) arithmetic.
        /// </summary>
        AF = 1 << 2,
        /// <summary>
        /// ZF (bit 6) Zero flag — Set if the result is zero; cleared otherwise.
        /// </summary>
        ZF = 1 << 3,
        /// <summary>
        /// SF (bit 7) Sign flag — Set equal to the most-significant bit of the result, which is the sign bit of a signed
        /// integer. (0 indicates a positive value and 1 indicates a negative value.)
        /// </summary>
        SF = 1 << 4,
        /// <summary>
        /// OF (bit 11) Overflow flag — Set if the integer result is too large a positive number or too small a negative
        /// number (excluding the sign-bit) to fit in the destination operand; cleared otherwise. This
        /// flag indicates an overflow condition for signed-integer (two’s complement) arithmetic.
        /// </summary>
        OF = 1 << 5,
        /// <summary>
        /// DF (bit ?) Direction flag
        /// </summary>
        DF = 1 << 6,


        ALL = CF | PF | AF | ZF | SF | OF | DF
    }
}
