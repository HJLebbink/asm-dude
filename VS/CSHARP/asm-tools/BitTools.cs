using System;
using System.Diagnostics;
using System.Text;

namespace AsmTools {

    /// <summary>
    /// Tools for handling bits and arrays of bits
    /// </summary>
    public abstract class BitTools {

        #region Conversion

        public static ulong getUlongValue(Bt[] a) {
            Debug.Assert(isGrounded(a));

            ulong v = 0;
            ulong mask = 0x1;

            for (int i = 0; i < a.Length; ++i) {
                switch (a[i]) {
                    case Bt.ZERO:
                        break;
                    case Bt.ONE:
                        v |= mask;
                        break;
                    default:
                        throw new Exception();
                }
                mask <<= 1;
            }
            return v;
        }
        public static long getLongValue(Bt[] a) {
            ulong v = getUlongValue(a);

            if (a.Length < 64) {
                bool sign = (a[a.Length - 1] == Bt.ONE);
                if (sign) {
                    v |= ~((1UL << a.Length) - 1);
                    //for (int i = a.Length; i < 64; ++i) {
                    //    v |= (1UL << i);
                    //}
                }
            }
            return (long)v;
        }
        public static uint getUintValue(Bt[] a) {
           return (uint) getUlongValue(a);
        }
        public static int getIntValue(Bt[] a) {
            ulong v = getUlongValue(a);

            if (a.Length < 32) {
                bool sign = (a[a.Length - 1] == Bt.ONE);
                if (sign) {
                    v |= ~((1UL << a.Length) - 1);
                    //for (int i = a.Length; i < 32; ++i) {
                    //   v |= (1UL << i);
                    //}
                }
            }
            return (int)v;
        }
        public static ushort getUshortValue(Bt[] a) {
            return (ushort)getUlongValue(a);
        }
        public static short getShortValue(Bt[] a) {
            ulong v = getUlongValue(a);

            if (a.Length < 16) {
                bool sign = (a[a.Length - 1] == Bt.ONE);
                if (sign) {
                    v |= ~((1UL << a.Length) - 1);
                    //for (int i = a.Length; i < 16; ++i) {
                    //    v |= (1UL << i);
                    //}
                }
            }
            return (short)v;
        }
        public static byte getByteValue(Bt[] a) {
            return (byte)getUlongValue(a);
        }
        public static sbyte getSbyteValue(Bt[] a) {
            ulong v = getUlongValue(a);

            if (a.Length < 8) {
                bool sign = (a[a.Length - 1] == Bt.ONE);
                if (sign) {
                    v |= ~((1UL << a.Length) - 1);
                    //for (int i = a.Length; i < 8; ++i) {
                    //    v |= (1UL << i);
                    //}
                }
            }
            return (sbyte)v;
        }

        public static void setUlongValue(ref Bt[] a, ulong v) {
            //Debug.Assert(a.Length < Tools.nBitsStorageNeeded(v));
            ulong mask = 0x1;
            for (int i = 0; i < a.Length; ++i) {
                a[i] = ((v & mask) == 0) ? Bt.ZERO : Bt.ONE;
                mask <<= 1;
            }
        }

        public static void setLongValue(ref Bt[] a, long v) {
            //Debug.Assert(a.Length < Tools.nBitsStorageNeeded(v));
            ulong v2 = (ulong)v;
            ulong mask = 0x1;
            for (int i = 0; i < a.Length; ++i) {
                a[i] = ((v2 & mask) == 0) ? Bt.ZERO : Bt.ONE;
                mask <<= 1;
            }
        }

        #endregion Conversion

        /// <summary>Returns true if all bits are either ONE, ZERO or KNOWN.</summary>
        public static bool isKnown(Bt[] a) {
            for (int i = 0; i < a.Length; ++i) {
                if (a[i] == Bt.UNDEFINED) return false;
            }
            return true;
        }

        /// <summary>Returns true if all bits are either ONE or ZERO.</summary>
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
        public static Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> neg(Bt[] a) {
            Bt[] zero = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                zero[i] = Bt.ZERO;
            }
            return sub(zero, a, Bt.ZERO);
        }

        public static Bt[] not(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            for (int i = 0; i < a.Length; ++i) {
                r[i] = neg(a[i]);
            }
            return r;
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
                        case Bt.ZERO: return Bt.KNOWN;
                        case Bt.ONE: return Bt.ONE;
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

        public static Tuple<Bt[], CarryFlag> sar1(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            CarryFlag carry = a[0];
            for (int i = 1; i < a.Length; ++i) {
                r[i - 1] = a[i];
            }
            r[a.Length - 1] = r[a.Length - 2];
            return new Tuple<Bt[], CarryFlag>(r, carry);
        }

        public static Tuple<Bt[], CarryFlag> shl1(Bt[] a) {
            Bt[] r = new Bt[a.Length];
            r[0] = Bt.ZERO;
            for (int i = 0; i < (a.Length - 1); ++i) {
                r[i + 1] = a[i];
            }
            CarryFlag carry = a[a.Length - 1];
            return new Tuple<Bt[], CarryFlag>(r, carry);
        }

        public static Tuple<Bt[], CarryFlag> sal1(Bt[] a) {
            return shl1(a);
        }
        #endregion

        #region Binary Arithmetic

        #region Private Binary Arithmetic
        /// <summary>
        /// Half adder
        /// </summary>
        private static Tuple<Bt, Bt> addHalf(Bt a, Bt b) {
            switch (a) {
                case Bt.ZERO:
                    switch (b) {
                        case Bt.ZERO: return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
                        case Bt.ONE: return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
                        case Bt.KNOWN: return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO);
                        case Bt.UNDEFINED:
                        default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
                    }
                case Bt.ONE:
                    switch (b) {
                        case Bt.ZERO: return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
                        case Bt.ONE: return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
                        case Bt.KNOWN: return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
                        case Bt.UNDEFINED:
                        default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
                    }
                case Bt.KNOWN:
                    switch (b) {
                        case Bt.ZERO: return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO);
                        case Bt.ONE: return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
                        case Bt.KNOWN: return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
                        case Bt.UNDEFINED:
                        default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
                    }
                case Bt.UNDEFINED:
                default: return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            }
        }

        /// <summary>Full adder</summary>
        private static Tuple<Bt, Bt> addFull(Bt x, Bt b, Bt carry) {
            if ((x == Bt.ZERO) && (b == Bt.ZERO) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
            if ((x == Bt.ZERO) && (b == Bt.ZERO) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((x == Bt.ZERO) && (b == Bt.ONE) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((x == Bt.ZERO) && (b == Bt.ONE) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);

            if ((x == Bt.ONE) && (b == Bt.ZERO) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((x == Bt.ONE) && (b == Bt.ZERO) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
            if ((x == Bt.ONE) && (b == Bt.ONE) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
            if ((x == Bt.ONE) && (b == Bt.ONE) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.ONE, Bt.ONE);

            if ((x == Bt.ZERO) && (b == Bt.ZERO) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((x == Bt.ZERO) && (b == Bt.UNDEFINED) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((x == Bt.ZERO) && (b == Bt.UNDEFINED) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((x == Bt.UNDEFINED) && (b == Bt.ZERO) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((x == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (carry == Bt.ZERO)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((x == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((x == Bt.ONE) && (b == Bt.ONE) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((x == Bt.ONE) && (b == Bt.UNDEFINED) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((x == Bt.ONE) && (b == Bt.UNDEFINED) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((x == Bt.UNDEFINED) && (b == Bt.ONE) && (carry == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((x == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (carry == Bt.ONE)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            //if ((a == Bt.UNDEFINED) && (b == Bt.UNDEFINED) && (c == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            // unreachable
            return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
        }

        /// <summary>
        /// returns diff, borrow_out; diff = x-y;
        /// </summary>
        private static Tuple<Bt, Bt> subFull(Bt borrow, Bt y, Bt x) {
           
            // all combinations:
            if ((borrow == Bt.ZERO) && (y == Bt.ZERO) && (x == Bt.ZERO))        return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.ZERO) && (x == Bt.ONE))         return new Tuple<Bt, Bt>(Bt.ONE, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.ZERO) && (x == Bt.KNOWN))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.ZERO) && (x == Bt.UNDEFINED))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);

            if ((borrow == Bt.ZERO) && (y == Bt.ONE) && (x == Bt.ZERO))         return new Tuple<Bt, Bt>(Bt.ONE, Bt.ONE);
            if ((borrow == Bt.ZERO) && (y == Bt.ONE) && (x == Bt.ONE))          return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.ONE) && (x == Bt.KNOWN))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ZERO) && (y == Bt.ONE) && (x == Bt.UNDEFINED))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.ZERO) && (y == Bt.KNOWN) && (x == Bt.ZERO))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ZERO) && (y == Bt.KNOWN) && (x == Bt.ONE))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.KNOWN) && (x == Bt.KNOWN))      return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ZERO) && (y == Bt.KNOWN) && (x == Bt.UNDEFINED))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.ZERO) && (y == Bt.UNDEFINED) && (x == Bt.ZERO))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.ZERO) && (y == Bt.UNDEFINED) && (x == Bt.ONE))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((borrow == Bt.ZERO) && (y == Bt.UNDEFINED) && (x == Bt.KNOWN))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.ZERO) && (y == Bt.UNDEFINED) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            ////////////
            if ((borrow == Bt.ONE) && (y == Bt.ZERO) && (x == Bt.ZERO))         return new Tuple<Bt, Bt>(Bt.ONE, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.ZERO) && (x == Bt.ONE))          return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ZERO);
            if ((borrow == Bt.ONE) && (y == Bt.ZERO) && (x == Bt.KNOWN))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ONE) && (y == Bt.ZERO) && (x == Bt.UNDEFINED))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.ONE) && (y == Bt.ONE) && (x == Bt.ZERO))          return new Tuple<Bt, Bt>(Bt.ZERO, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.ONE) && (x == Bt.ONE))           return new Tuple<Bt, Bt>(Bt.ONE, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.ONE) && (x == Bt.KNOWN))         return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.ONE) && (x == Bt.UNDEFINED))     return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);

            if ((borrow == Bt.ONE) && (y == Bt.KNOWN) && (x == Bt.ZERO))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.KNOWN) && (x == Bt.ONE))         return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ONE) && (y == Bt.KNOWN) && (x == Bt.KNOWN))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.ONE) && (y == Bt.KNOWN) && (x == Bt.UNDEFINED))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.ONE) && (y == Bt.UNDEFINED) && (x == Bt.ZERO))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((borrow == Bt.ONE) && (y == Bt.UNDEFINED) && (x == Bt.ONE))     return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.ONE) && (y == Bt.UNDEFINED) && (x == Bt.KNOWN))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.ONE) && (y == Bt.UNDEFINED) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            ////////////
            if ((borrow == Bt.KNOWN) && (y == Bt.ZERO) && (x == Bt.ZERO))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.ZERO) && (x == Bt.ONE))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO);
            if ((borrow == Bt.KNOWN) && (y == Bt.ZERO) && (x == Bt.KNOWN))      return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.ZERO) && (x == Bt.UNDEFINED))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.KNOWN) && (y == Bt.ONE) && (x == Bt.ZERO))        return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ONE);
            if ((borrow == Bt.KNOWN) && (y == Bt.ONE) && (x == Bt.ONE))         return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.ONE) && (x == Bt.KNOWN))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.ONE) && (x == Bt.UNDEFINED))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.KNOWN) && (y == Bt.KNOWN) && (x == Bt.ZERO))      return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ONE);
            if ((borrow == Bt.KNOWN) && (y == Bt.KNOWN) && (x == Bt.ONE))       return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.KNOWN) && (x == Bt.KNOWN))     return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.KNOWN);
            if ((borrow == Bt.KNOWN) && (y == Bt.KNOWN) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.KNOWN) && (y == Bt.UNDEFINED) && (x == Bt.ZERO))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.KNOWN) && (y == Bt.UNDEFINED) && (x == Bt.ONE))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.KNOWN) && (y == Bt.UNDEFINED) && (x == Bt.KNOWN)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.KNOWN) && (y == Bt.UNDEFINED) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            ////////////
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ZERO) && (x == Bt.ZERO))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ZERO) && (x == Bt.ONE))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ZERO) && (x == Bt.KNOWN))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ZERO) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.UNDEFINED) && (y == Bt.ONE) && (x == Bt.ZERO))    return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ONE);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ONE) && (x == Bt.ONE))     return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ONE) && (x == Bt.KNOWN))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.ONE) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.UNDEFINED) && (y == Bt.KNOWN) && (x == Bt.ZERO))      return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.KNOWN) && (x == Bt.ONE))       return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.KNOWN) && (x == Bt.KNOWN))     return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.KNOWN) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            if ((borrow == Bt.UNDEFINED) && (y == Bt.UNDEFINED) && (x == Bt.ZERO))  return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.UNDEFINED) && (x == Bt.ONE))   return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.UNDEFINED) && (x == Bt.KNOWN)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
            if ((borrow == Bt.UNDEFINED) && (y == Bt.UNDEFINED) && (x == Bt.UNDEFINED)) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);

            
            //TODO make faster switch table
            /*
            switch (borrow) {
                case Bt.ZERO:
                case Bt.ONE:
                case Bt.KNOWN:
                    switch (y) {
                        case Bt.ZERO: break;
                        case Bt.ONE: if (x == Bt.ONE) return new Tuple<Bt, Bt>(Bt.KNOWN, Bt.ZERO); break;
                        case Bt.KNOWN: break;
                        case Bt.UNDEFINED: break;
                    }
                    break;
                case Bt.UNDEFINED:
                    switch (y) {
                        case Bt.ZERO: break;
                        case Bt.ONE: if (x == Bt.ONE) return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.ZERO); break;
                        case Bt.KNOWN: break;
                        case Bt.UNDEFINED: break;
                    }
                    break;
            }
            */
            // unreachable
            return new Tuple<Bt, Bt>(Bt.UNDEFINED, Bt.UNDEFINED);
        }

        /// <summary>Calc the value of the overflow flag in an addition operation</summary>
        private static OverflowFlag calcOverflow(Bt sign1, Bt sign2, Bt c) {

            if (sign1 == Bt.UNDEFINED) return Bt.UNDEFINED;
            if (sign2 == Bt.UNDEFINED) return Bt.UNDEFINED;
            if (c == Bt.UNDEFINED) return Bt.UNDEFINED;

            if (sign1 == Bt.KNOWN) return Bt.KNOWN;
            if (sign2 == Bt.KNOWN) return Bt.KNOWN;
            if (c == Bt.KNOWN) return Bt.KNOWN;

            if ((sign1 == Bt.ONE) && (sign2 == Bt.ONE) && (c == Bt.ZERO)) return Bt.ONE;
            if ((sign1 == Bt.ZERO) && (sign2 == Bt.ZERO) && (c == Bt.ONE)) return Bt.ONE;

            return Bt.ZERO;
        }

        /// <summary>add the provided bit to the provided the array</summary>
        private static Tuple<CarryFlag, OverflowFlag, AuxiliaryFlag> add_one_bit(ref Bt[] a, Bt b) {

            AuxiliaryFlag af = Bt.UNDEFINED;
            Bt signBitBefore = a[a.Length - 1];
            Bt toAdd = b;

            for (int i = 0; i < a.Length; ++i) {
                switch (toAdd) {
                    case Bt.ZERO: // nothing to add, do nothing
                        break;
                    case Bt.ONE: // adding one to position i;
                        switch (a[i]) {
                            case Bt.ZERO:
                                a[i] = Bt.ONE;
                                toAdd = Bt.ZERO;
                                break;
                            case Bt.ONE:
                                a[i] = Bt.ZERO;
                                //toAdd = Bt.ONE;
                                break;
                            case Bt.KNOWN:
                                a[i] = Bt.KNOWN;
                                toAdd = Bt.KNOWN;
                                break;
                            case Bt.UNDEFINED:
                                a[i] = Bt.UNDEFINED;
                                toAdd = Bt.UNDEFINED;
                                break;
                        }
                        break;
                    case Bt.KNOWN:
                        if (a[i] == Bt.UNDEFINED) {
                            a[i] = Bt.UNDEFINED;
                            toAdd = Bt.UNDEFINED;
                        } else {
                            a[i] = Bt.KNOWN;
                        }
                        break;
                    case Bt.UNDEFINED:
                        a[i] = Bt.UNDEFINED;
                        break;
                }
                if (i == 3) {
                    af = toAdd;
                } 
            }

            CarryFlag cf = toAdd;
            OverflowFlag of = calcOverflow(signBitBefore, Bt.ZERO, a[a.Length - 1]);
            return new Tuple<CarryFlag, OverflowFlag, AuxiliaryFlag>(cf, of, af);
        }

        #endregion Private Binary Arithmetic

        public static Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> add(Bt[] a, Bt[] b, CarryFlag cfIn) {
            Debug.Assert(a.Length == b.Length);
            int length = a.Length;

            Bt[] r = new Bt[length];
            CarryFlag cf = cfIn;
            AuxiliaryFlag af = Bt.UNDEFINED;

            for (int i = 0; i < length; ++i) {
                Tuple<Bt, Bt> t = addFull(a[i], b[i], cf);
                r[i] = t.Item1;
                cf = t.Item2;
                if (i == 3) {
                    af = cf.val;
                }
            }
            OverflowFlag of = calcOverflow(a[length - 1], b[length - 1], r[length - 1]);
            return new Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag>(r, cf, of, af);
        }

        public static Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag> sub(Bt[] a, Bt[] b, CarryFlag cfIn) {
            Debug.Assert(a.Length == b.Length);
            int length = a.Length;

            Bt[] r = new Bt[length];
            CarryFlag cf = cfIn;
            AuxiliaryFlag af = Bt.UNDEFINED;

            for (int i = 0; i < length; ++i) {
                Tuple<Bt, Bt> t = subFull(cf, b[i], a[i]);
                r[i] = t.Item1;
                cf = t.Item2;
                if (i == 3) {
                    af = cf.val;
                }
            }
            OverflowFlag of = calcOverflow(a[length - 1], b[length - 1], r[length - 1]);
            return new Tuple<Bt[], CarryFlag, OverflowFlag, AuxiliaryFlag>(r, cf, of, af);
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
                return BitTools.getUlongValue(a) + "";
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
