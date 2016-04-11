using System;
using System.Diagnostics;
using System.Text;

namespace AsmTools {

    /// <summary>
    /// Tools for handling bits and arrays of bits
    /// </summary>
    public abstract class BitTools {


        #region Conversion

        public static ulong intValue(Bt[] a) {
            ulong v = 0;
            for (int i = 0; i < a.Length; ++i) {
                switch (a[i]) {
                    case Bt.ZERO:
                        break;
                    case Bt.ONE:
                        v |= (1ul << i);
                        break;
                    default:
                        throw new Exception();
                }
            }
            return v;
        }

        #endregion Conversion

        /// <summary>
        /// Returns true if all bits are either ONE, ZERO or KNOWN
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static bool isKnown(Bt[] a) {
            for (int i = 0; i < a.Length; ++i) {
                if (a[i] == Bt.UNDEFINED) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if all bits are either one or zero
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static bool isGrounded(Bt[] a) {
            for (int i = 0; i < a.Length; ++i) {
                if ((a[i] == Bt.ONE) || (a[i] == Bt.ZERO)) {
                    // OK
                } else {
                    return false;
                }
            }
            return true;
        }

        #region BitType operations



        public static void copy(Bt[] src, int srcBegin, Bt[] dst, int dstBegin, int length) {
            for (int i = 0; i < length; ++i) {
                dst[dstBegin + i] = src[srcBegin + i];
            }
        }
        public static void fill(Bt[] src, Bt init) {
            for (int i = 0; i < src.Length; ++i) {
                src[i] = init;
            }
        }

        public static Bt neg(Bt a) {
            switch (a) {
                case Bt.ONE: return Bt.ZERO;
                case Bt.ZERO: return Bt.ONE;
                case Bt.UNDEFINED: return Bt.UNDEFINED;
                case Bt.KNOWN: return Bt.KNOWN;
            }
            // unreachable:
            return Bt.UNDEFINED;
        }
        /// <summary>
        /// returns neg, OF, AF
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Tuple<Bt[], OverflowFlag, AuxiliaryFlag> neg(Bt[] a) {
            OverflowFlag oFlag = Bt.UNDEFINED;
            AuxiliaryFlag aFlag = Bt.UNDEFINED;
            //TODO
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = neg(a[i]);
            }
            return new Tuple<Bt[], OverflowFlag, AuxiliaryFlag>(r, oFlag, aFlag);
        }

        public static Bt and(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    return Bt.ZERO;
                case Bt.ONE:
                    return b;
                case Bt.UNDEFINED:
                    switch (b) {
                        case Bt.ZERO: return Bt.ZERO;
                        case Bt.ONE: return Bt.UNDEFINED;
                        case Bt.UNDEFINED: return Bt.UNDEFINED;
                        case Bt.KNOWN: return Bt.UNDEFINED;
                    }
                    break;
                case Bt.KNOWN:
                    switch (b) {
                        case Bt.ZERO: return Bt.ZERO;
                        case Bt.ONE: return Bt.KNOWN;
                        case Bt.UNDEFINED: return Bt.UNDEFINED;
                        case Bt.KNOWN: return Bt.KNOWN;
                    }
                    break;
            }
            // unreachable:
            return Bt.UNDEFINED;
        }
        public static Bt[] and(Bt[] a, Bt[] b) {
            Debug.Assert(a.Length == b.Length);
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = and(a[i], b[i]);
            }
            return r;
        }
        public static Bt or(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    return b;
                case Bt.ONE:
                    return Bt.ONE;
                case Bt.UNDEFINED:
                    switch (b) {
                        case Bt.ZERO: return Bt.ZERO;
                        case Bt.ONE: return Bt.UNDEFINED;
                        case Bt.UNDEFINED: return Bt.UNDEFINED;
                        case Bt.KNOWN: return Bt.UNDEFINED;
                    }
                    break;
                case Bt.KNOWN:
                    switch (b) {
                        case Bt.ZERO: return Bt.ZERO;
                        case Bt.ONE: return Bt.KNOWN;
                        case Bt.UNDEFINED: return Bt.UNDEFINED;
                        case Bt.KNOWN: return Bt.KNOWN;
                    }
                    break;
            }
            // unreachable:
            return Bt.UNDEFINED;
        }
        public static Bt[] or(Bt[] a, Bt[] b) {
            Debug.Assert(a.Length == b.Length);
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = or(a[i], b[i]);
            }
            return r;
        }

        public static Bt xor(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    return b;
                case Bt.ONE:
                    return neg(b);
                case Bt.UNDEFINED:
                    return Bt.UNDEFINED;
                case Bt.KNOWN:
                    switch (b) {
                        case Bt.ZERO: return Bt.KNOWN;
                        case Bt.ONE: return Bt.KNOWN;
                        case Bt.UNDEFINED: return Bt.UNDEFINED;
                        case Bt.KNOWN: return Bt.KNOWN;
                    }
                    break;
            }
            // unreachable:
            return Bt.UNDEFINED;
        }
        public static Bt[] xor(Bt[] a, Bt[] b) {
            Debug.Assert(a.Length == b.Length);
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = xor(a[i], b[i]);
            }
            return r;
        }

        public static Bt eq(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    return neg(b);
                case Bt.ONE:
                    return b;
                case Bt.UNDEFINED:
                    return Bt.UNDEFINED;
                case Bt.KNOWN:
                    if (b == Bt.UNDEFINED) {
                        return Bt.UNDEFINED;
                    } else {
                        return Bt.KNOWN;
                    }
            }
            // unreachable:
            return Bt.UNDEFINED;
        }
        public static Bt eq(Bt[] a, Bt[] b) {
            Debug.Assert(a.Length == b.Length);

            bool existsZERO = false;
            bool existsKNOWN = false;

            for (int i = 0; i < a.Length; ++i) {
                switch (eq(a[i], b[i])) {
                    case Bt.ZERO: existsZERO = true; break;
                    case Bt.ONE: break;
                    case Bt.UNDEFINED: return Bt.UNDEFINED;
                    case Bt.KNOWN: existsKNOWN = true; break;
                }
            }
            if (existsKNOWN) {
                return Bt.KNOWN;
            }
            if (existsZERO) {
                return Bt.ZERO;
            }
            return Bt.ONE;
        }

        public static Bt[] eq_bitwise(Bt[] a, Bt[] b) {
            Debug.Assert(a.Length == b.Length);
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = eq(a[i], b[i]);
            }
            return r;
        }

        public static Tuple<Bt[], CarryFlag> shr1(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            CarryFlag carry = a[0];
            for (int i = 1; i < a.Length; ++i) {
                r[i - 1] = a[i];
            }
            r[a.Length - 1] = Bt.ZERO;
            return new Tuple<Bt[], CarryFlag>(r, carry);
        }

        public static Tuple<Bt[], Bt> sar1(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            Bt carry = a[0];
            for (int i = 1; i < a.Length; ++i) {
                r[i - 1] = a[i];
            }
            r[a.Length - 1] = r[a.Length - 2];
            return new Tuple<Bt[], Bt>(r, carry);
        }

        public static Tuple<Bt[], Bt> shl1(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            r[0] = Bt.ZERO;
            for (int i = 0; i < (a.Length - 1); ++i) {
                r[i + 1] = a[i];
            }
            Bt carry = a[a.Length - 1];
            return new Tuple<Bt[], Bt>(r, carry);
        }

        public static Tuple<Bt[], Bt> sal1(Bt[] a) {
            return shl1(a);
        }
        #endregion

        #region Binary Arithmetic

        /// <summary>
        /// Half adder
        /// </summary>
        public static Tuple<Bt, Bt> add(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    switch (b) {
                        case Bt.ZERO: return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
                        case Bt.ONE: return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
                        default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
                    }
                case Bt.ONE:
                    switch (b) {
                        case Bt.ZERO: return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
                        case Bt.ONE: return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
                        default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
                    }
                default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            }
        }
        /// <summary>
        /// Full adder
        /// </summary>
        public static Tuple<Bt, Bt> add(Bt a, Bt b, Bt c) {
            if ((a == Bt.ZERO) && (b == Bt.ZERO) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
            if ((a == Bt.ZERO) && (b == Bt.ZERO) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((a == Bt.ZERO) && (b == Bt.ONE) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((a == Bt.ZERO) && (b == Bt.ONE) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);

            if ((a == Bt.ONE) && (b == Bt.ZERO) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((a == Bt.ONE) && (b == Bt.ZERO) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
            if ((a == Bt.ONE) && (b == Bt.ONE) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
            if ((a == Bt.ONE) && (b == Bt.ONE) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ONE);

            if ((a == Bt.ZERO) && (b == Bt.ZERO) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((a == Bt.ZERO) && (b == Bt.UNDEFINED) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((a == Bt.ZERO) && (b == Bt.UNDEFINED) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((a == Bt.UNDEFINED) && (b == Bt.ZERO) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((a == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (c == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((a == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((a == Bt.ONE) && (b == Bt.ONE) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((a == Bt.ONE) && (b == Bt.UNDEFINED) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((a == Bt.ONE) && (b == Bt.UNDEFINED) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((a == Bt.UNDEFINED) && (b == Bt.ONE) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((a == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (c == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            //if ((a == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            // unreachable
            return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
        }

        public static OverflowFlag calcOverflow(Bt a, Bt b, Bt c) {
            if ((a == Bt.ONE) && (b == Bt.ONE) && (c == Bt.ZERO)) return Bt.ONE;
            if ((a == Bt.ZERO) && (b == Bt.ZERO) && (c == Bt.ONE)) return Bt.ONE;

            if (a == Bt.UNDEFINED) return Bt.UNDEFINED;
            if (b == Bt.UNDEFINED) return Bt.UNDEFINED;
            if (c == Bt.UNDEFINED) return Bt.UNDEFINED;

            return Bt.ZERO;
        }

        public static Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> add(Bt[] a, Bt[] b) {

            Debug.Assert(a.Length == b.Length);
            int length = a.Length;

            Bt[] r = new Bt[a.Length];
            CarryFlag carry = Bt.ZERO;
            AuxiliaryFlag auxiliary = Bt.UNDEFINED;
            for (int i = 0; i < length; ++i) {
                Tuple<Bt, Bt> t = add(a[i], b[i], carry);
                r[i] = t.Item1;
                carry = t.Item2;
                if (i == 2) {
                    auxiliary = new AuxiliaryFlag(carry);
                }
            }
            OverflowFlag overflow = calcOverflow(a[length - 2], b[length - 2], r[length - 2]);
            return new Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag>(r, carry, overflow, auxiliary);
        }

        public static Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> sub(Bt[] a, Bt[] b) {

            Debug.Assert(a.Length == b.Length);
            int length = a.Length;

            Bt[] r = new Bt[a.Length];
            CarryFlag carry = Bt.ZERO;
            AuxiliaryFlag auxiliary = Bt.UNDEFINED;
            //TODO

            /*
            for (int i = 0; i < length; ++i) {
                Tuple<Bt, Bt> t = add(a[i], b[i], carry);
                r[i] = t.Item1;
                carry = t.Item2;
                if (i == 2) {
                    auxiliary = new AuxiliaryFlag(carry);
                }
            }
            */
            OverflowFlag overflow = calcOverflow(a[length - 2], b[length - 2], r[length - 2]);
            return new Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag>(r, carry, overflow, auxiliary);
        }



        #endregion

        #region ToString

        public static char bitToChar(Bt bit) {
            switch (bit) {
                case Bt.ZERO: return '0';
                case Bt.ONE: return '1';
                case Bt.UNDEFINED: return 'U';
                case Bt.KNOWN: return 'K';
                default: return '?';
            }
        }

        public static char bitToCharHex(Bt b0, Bt b1, Bt b2, Bt b3) {

            if ((b3 == Bt.UNDEFINED) || (b2 == Bt.UNDEFINED) || (b1 == Bt.UNDEFINED) || (b0 == Bt.UNDEFINED)) return 'U';
            if ((b3 == Bt.KNOWN) || (b2 == Bt.KNOWN) || (b1 == Bt.KNOWN) || (b0 == Bt.KNOWN)) return 'K';

            if ((b3 == Bt.ZERO) && (b2 == Bt.ZERO) && (b1 == Bt.ZERO) && (b0 == Bt.ZERO)) return '0';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ZERO) && (b1 == Bt.ZERO) && (b0 == Bt.ONE)) return '1';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ZERO) && (b1 == Bt.ONE) && (b0 == Bt.ZERO)) return '2';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ZERO) && (b1 == Bt.ONE) && (b0 == Bt.ONE)) return '3';

            if ((b3 == Bt.ZERO) && (b2 == Bt.ONE) && (b1 == Bt.ZERO) && (b0 == Bt.ZERO)) return '4';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ONE) && (b1 == Bt.ZERO) && (b0 == Bt.ONE)) return '5';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ONE) && (b1 == Bt.ONE) && (b0 == Bt.ZERO)) return '6';
            if ((b3 == Bt.ZERO) && (b2 == Bt.ONE) && (b1 == Bt.ONE) && (b0 == Bt.ONE)) return '7';

            if ((b3 == Bt.ONE) && (b2 == Bt.ZERO) && (b1 == Bt.ZERO) && (b0 == Bt.ZERO)) return '8';
            if ((b3 == Bt.ONE) && (b2 == Bt.ZERO) && (b1 == Bt.ZERO) && (b0 == Bt.ONE)) return '9';
            if ((b3 == Bt.ONE) && (b2 == Bt.ZERO) && (b1 == Bt.ONE) && (b0 == Bt.ZERO)) return 'A';
            if ((b3 == Bt.ONE) && (b2 == Bt.ZERO) && (b1 == Bt.ONE) && (b0 == Bt.ONE)) return 'B';

            if ((b3 == Bt.ONE) && (b2 == Bt.ONE) && (b1 == Bt.ZERO) && (b0 == Bt.ZERO)) return 'C';
            if ((b3 == Bt.ONE) && (b2 == Bt.ONE) && (b1 == Bt.ZERO) && (b0 == Bt.ONE)) return 'D';
            if ((b3 == Bt.ONE) && (b2 == Bt.ONE) && (b1 == Bt.ONE) && (b0 == Bt.ZERO)) return 'E';
            if ((b3 == Bt.ONE) && (b2 == Bt.ONE) && (b1 == Bt.ONE) && (b0 == Bt.ONE)) return 'F';

            // unreachable
            return '?';
        }

        public static string toStringBinary(Bt[] a) {
            StringBuilder sb = new StringBuilder();
            for (int i = (a.Length - 1); i >= 0; --i) {
                sb.Append(BitTools.bitToChar(a[i]));
                if ((i > 0) && (i != a.Length - 1) && (i % 8 == 0)) sb.Append('.');
            }
            return sb.ToString();
        }

        public static string toStringDecimal(Bt[] a) {
            if (BitTools.isKnown(a)) {
                return BitTools.intValue(a) + "";
            }
            //TODO:
            throw new Exception();
        }

        public static string toStringHex(Bt[] a) {
            if ((a.Length % 4) != 0) {
                Console.WriteLine("WARNING: toStringHex: found odd number of bits:" + a.Length);
                return "";
            }
            StringBuilder sb = new StringBuilder();
            int nChars = a.Length >> 2;

            for (int j = (nChars - 1); j >= 0; --j) {
                int offset = (j << 2);
                sb.Append(BitTools.bitToCharHex(a[offset], a[offset + 1], a[offset + 2], a[offset + 3]));

                if ((j > 0) && ((j % 8) == 0)) sb.Append('.');
            }
            return sb.ToString();
        }

        #endregion ToString

    }
}
