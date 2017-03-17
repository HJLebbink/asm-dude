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

using System;
using System.Diagnostics;
using System.Text;

namespace AsmTools {

    /// <summary>
    /// Tools for handling bits and arrays of bits
    /// </summary>
    public abstract class BitTools {

        #region Conversion

        public static ulong GetUlongValue(Bt[] a) {
            Debug.Assert(IsGrounded(a));

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
        public static long GetLongValue(Bt[] a) {
            ulong v = GetUlongValue(a);

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
        public static uint GetUintValue(Bt[] a) {
           return (uint) GetUlongValue(a);
        }
        public static int GetIntValue(Bt[] a) {
            ulong v = GetUlongValue(a);

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
        public static ushort GetUshortValue(Bt[] a) {
            return (ushort)GetUlongValue(a);
        }
        public static short GetShortValue(Bt[] a) {
            ulong v = GetUlongValue(a);

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
        public static byte GetByteValue(Bt[] a) {
            return (byte)GetUlongValue(a);
        }
        public static sbyte GetSbyteValue(Bt[] a) {
            ulong v = GetUlongValue(a);

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

        public static void SetUlongValue(ref Bt[] a, ulong v) {
            //Debug.Assert(a.Length < Tools.nBitsStorageNeeded(v));
            ulong mask = 0x1;
            for (int i = 0; i < a.Length; ++i) {
                a[i] = ((v & mask) == 0) ? Bt.ZERO : Bt.ONE;
                mask <<= 1;
            }
        }
        public static void SetLongValue(ref Bt[] a, long v) {
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
        public static bool IsKnown(Bt[] a) {
            for (int i = 0; i < a.Length; ++i) {
                if (a[i] == Bt.UNDEFINED) return false;
            }
            return true;
        }

        /// <summary>Returns true if all bits are either ONE or ZERO.</summary>
        public static bool IsGrounded(Bt[] a) {
            for (int i = 0; i < a.Length; ++i) {
                if ((a[i] == Bt.ONE) || (a[i] == Bt.ZERO)) {
                    // OK
                } else {
                    return false;
                }
            }
            return true;
        }

        public static void Copy(Bt[] src, int srcBegin, Bt[] dst, int dstBegin, int length) {
            for (int i = 0; i < length; ++i) {
                dst[dstBegin + i] = src[srcBegin + i];
            }
        }
        public static void Fill(Bt[] src, Bt init) {
            for (int i = 0; i < src.Length; ++i) {
                src[i] = init;
            }
        }

        /// <summary>
        /// Make BT[64] array from ulong d (data) and ulong du (data UNDEFINED) If bit is 0 in du, then the truth-value is UNDEFINED.
        /// </summary>
        /// <param name="d">DATA</param>
        /// <param name="du">Data UNDEFINED. if bit is 0 then the truth-value is UNDEFINED</param>
        /// <returns></returns>
        public static Bt[] ToBtArray(ulong d, ulong du) {
            Bt[] a = new Bt[64];
            ulong mask = 1;

            for (int i = 0; i < 64; ++i) {
                a[i] = ((du & mask) == 0) ? Bt.UNDEFINED : (((d & mask) == 0) ? Bt.ZERO : Bt.ONE);
                mask <<= 1;
            }
            return a;
        }

        public static int NBitsNeeded(ulong d) {
            for (int i = 0; i < 64; ++i) {
                if ((d >> i) == 0) return i;
            }
            return 64;
        }

        public static bool GetBit(ulong[] data, int bitPos) {
            return (((data[bitPos >> 6] >> (bitPos & 0x3F)) & 0x1) == 1);
        }




        public static (ulong, ulong) ToRaw(Bt[] a) {
            Debug.Assert(a.Length == 64);
            ulong d = 0;
            ulong du = 0; // init all 64 bits to UNDEFINED
            ulong mask = 1;
            for (int i = 0; i < 64; ++i) {
                switch (a[i]) {
                    case Bt.ONE:
                        du |= mask;
                        d |= mask;
                        break;
                    case Bt.ZERO:
                        du |= mask;
                        break;
                    case Bt.KNOWN: break;
                    case Bt.UNDEFINED: break;
                }
                mask <<= 1;
            }
            return (d, du);
        }

        #region ToString

        public static char BitToChar(Bt bit) {
            switch (bit) {
                case Bt.ZERO: return '0';
                case Bt.ONE: return '1';
                case Bt.UNDEFINED: return 'U';
                case Bt.KNOWN: return 'K';
                default: return '?';
            }
        }

        public static char BitToCharHex(Bt b0, Bt b1, Bt b2, Bt b3) {

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

        public static string ToStringBin(Bt[] a) {
            StringBuilder sb = new StringBuilder("0b");
            for (int i = (a.Length - 1); i >= 0; --i) {
                sb.Append(BitTools.BitToChar(a[i]));
                if ((i > 0) && (i != a.Length - 1) && (i % 8 == 0)) sb.Append('.');
            }
            return sb.ToString();
        }

        public static string ToStringBin(ulong a) {
            StringBuilder sb = new StringBuilder("0b");
            for (int i = (64 - 1); i >= 0; --i) {
                sb.Append((((a >> i) & 1) == 1) ? "1" : "0");
                if ((i > 0) && (i != 64 - 1) && (i % 8 == 0)) sb.Append('.');
            }
            return sb.ToString();
        }

        public static string ToStringDec(Bt[] a) {
            if (BitTools.IsKnown(a)) {
                return BitTools.GetUlongValue(a) + "";
            }
            //TODO:
            throw new Exception();
        }

        public static string ToStringHex(ulong a) {
            return string.Format("0x{0:X}", a);
        }

        public static string ToStringHex(Bt[] a) {
            if ((a.Length % 4) != 0) {
                Console.WriteLine("WARNING: toStringHex: found odd number of bits:" + a.Length);
                return "";
            }
            StringBuilder sb = new StringBuilder("0x");
            int nChars = a.Length >> 2;

            for (int j = (nChars - 1); j >= 0; --j) {
                int offset = (j << 2);
                sb.Append(BitTools.BitToCharHex(a[offset], a[offset + 1], a[offset + 2], a[offset + 3]));

                if ((j > 0) && ((j % 8) == 0)) sb.Append('.');
            }
            return sb.ToString();
        }

        #endregion ToString

    }
}
