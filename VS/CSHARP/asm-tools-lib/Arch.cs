// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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
using System.Collections.Generic;
using System.Text;

namespace AsmTools {

    public enum Arch {

        NONE,

        ARCH_8086,
        ARCH_186,
        ARCH_286,
        ARCH_386,
        ARCH_486,
        /// <summary>1993 (also knonw as i586)</summary>
        PENT,
        /// <summary>1995 (also known as i686)</summary>
        P6,

        ARCH_3DNOW,
        MMX,

        SSE,
        SSE2,
        SSE3,
        SSSE3,
        SSE4_1,
        SSE4_2,
        SSE4A,
        /// <summary>AMD</summary>
        SSE5,

        AVX,
        AVX2,

        ///<summary>AVX512 foundation (Knights Landing, Intel Xeon)</summary>
        AVX512F,

        ///<summary>AVX512 conflict detection (Knights Landing, Intel Xeon)</summary>
        AVX512CD,

        ///<summary>AVX512 exponential and reciprocal (Knights Landing)</summary>
        AVX512ER,

        ///<summary>AVX512 prefetch (Knights Landing)</summary>
        AVX512PF,

        ///<summary>AVX512 byte and word (Intel Xeon)</summary>
        AVX512BW,

        ///<summary>AVX512 doubleword and quadword (Intel Xeon)</summary>
        AVX512DQ,
        
        ///<summary>AVX512 Vector Length Extensions (Intel Xeon)</summary>
        ///An additional orthogonal capability known as Vector Length Extensions provide for most AVX-512 instructions 
        ///to operate on 128 or 256 bits, instead of only 512. Vector Length Extensions can currently be applied to
        ///most Foundation Instructions, the Conflict Detection Instructions as well as the new Byte, Word, Doubleword 
        ///and Quadword instructions. These AVX-512 Vector Length Extensions are indicated by the AVX512VL CPUID flag. 
        ///The use of Vector Length Extensions extends most AVX-512 operations to also operate on XMM (128-bit, SSE) 
        ///registers and YMM (256-bit, AVX) registers. The use of Vector Length Extensions allows the capabilities of 
        ///EVEX encodings, including the use of mask registers and access to registers 16..31, to be applied to XMM 
        ///and YMM registers instead of only to ZMM registers.
        AVX512VL,


        #region Misc Intel
        /// <summary>Two instruction ADCX and ADOX</summary>
        ADX,

        /// <summary>Advanced Encryption Standard Instruction Set </summary>
        AES,

        /// <summary>Bit Manipulation Instructions Sets 1</summary>
        BMI1,

        /// <summary>Bit Manipulation Instructions Sets 2</summary>
        BMI2,

        /// <summary>half precision floating point conversion (also known as CVT16) </summary>
        F16C,

        /// <summary>Fused Multiply-Add</summary>
        FMA,

        /// <summary>TODO</summary>
        FSGSBASE,

        ///<summary>Hardware Lock Elision</summary>
        HLE,
        
        /// <summary>Invalidates TLBs, two instructions</summary>
        INVPCID,
        
        /// <summary>Secure Hash Algorithm Extensions</summary>
        SHA,

        /// <summary>Transactional Synchronization Extensions</summary>
        RTM,

        /// <summary>Memory Protection Extensions</summary>
        MPX,
        
        /// <summary>Two instruction PCLMULQDQ (Carry-Less Multiplication Quadword)</summary>
        PCLMULQDQ,

        /// <summary>One instruction LZCNT</summary>
        LZCNT,

        /// <summary>One instruction: PREFETCHWT1</summary>
        PREFETCHWT1,

        /// <summary>One instruction: PREFETCHW</summary>
        PRFCHW,

        /// <summary>One instruction: RDPID (Read Processor ID)</summary>
        RDPID,

        /// <summary>One instruction: RDRAND (Read Random Number)</summary>
        RDRAND,

        /// <summary>One instruction: RDSEED (Read Random SEED)</summary>
        RDSEED,

        /// <summary>One instruction: XSAVEOPT (Save Processor Extended States Optimized)</summary>
        XSAVEOPT,
        #endregion

        #region Misc Other
        X64,
        /// <summary>AMD</summary>
        X86_64,
        IA64,

        /// <summary>AMD: Trailing Bit Manipulation</summary>
        TBM,

        AMD,
        /// <summary>Privileged instructions</summary>
        PRIV,

        PROT,
        CYRIX,
        CYRIXM,
        VMX,
        MIB,
        #endregion 

        #region unused
        OPT,
        ND,
        LONG,
        NOLONG,
        UNDOC,
        #endregion
    }

    public static class ArchTools {

        public static bool ignoreArch(Arch arch) {
            switch (arch) {
                case Arch.NONE:
                case Arch.OPT:
                case Arch.ND:
                case Arch.LONG:
                case Arch.NOLONG:
                case Arch.UNDOC:
                    return true;
                default: return false;
            }
        }
        public static Arch parseArch(string str) {
            switch (str.ToUpper()) {
                case "NONE": return Arch.NONE;

                case "8086": return Arch.ARCH_8086;
                case "186": return Arch.ARCH_186;
                case "286": return Arch.ARCH_286;
                case "386": return Arch.ARCH_386;
                case "486": return Arch.ARCH_486;

                case "3DNOW": return Arch.ARCH_3DNOW;
                case "MMX": return Arch.MMX;

                case "SSE": return Arch.SSE;
                case "SSE2": return Arch.SSE2;
                case "SSE3": return Arch.SSE3;
                case "SSSE3": return Arch.SSSE3;
                case "SSE4_1": return Arch.SSE4_1;
                case "SSE4_2": return Arch.SSE4_2;
                case "SSE4A": return Arch.SSE4A;
                case "SSE5": return Arch.SSE5;

                case "AVX": return Arch.AVX;
                case "AVX2": return Arch.AVX2;
                case "AVX512VL": return Arch.AVX512VL;
                case "AVX512DQ": return Arch.AVX512DQ;
                case "AVX512BW": return Arch.AVX512BW;
                case "AVX512ER": return Arch.AVX512ER;
                case "AVX512F": return Arch.AVX512F;
                case "AVX512CD": return Arch.AVX512CD;
                case "AVX512PF": return Arch.AVX512PF;

                case "HLE": return Arch.HLE;
                case "BMI1": return Arch.BMI1;
                case "BMI2": return Arch.BMI2;
                case "P6": return Arch.P6;
                case "X86_64": return Arch.X86_64;
                case "IA64": return Arch.IA64;
                case "FMA": return Arch.FMA;
                case "AES": return Arch.AES;
                case "TBM": return Arch.TBM;
                case "AMD": return Arch.AMD;
                case "PRIV": return Arch.PRIV;

                #region Specific Processors
                case "PENT": return Arch.PENT;
                #endregion


                case "OPT": return Arch.OPT;
                case "PROT": return Arch.PROT;
                case "CYRIX": return Arch.CYRIX;
                case "INVPCID": return Arch.INVPCID;
                case "CYRIXM": return Arch.CYRIXM;
                case "VMX": return Arch.VMX;
                case "ADX": return Arch.ADX;

                case "X64": return Arch.X64;
                case "PCLMULQDQ": return Arch.PCLMULQDQ;
                case "PRFCHW": return Arch.PRFCHW;
                case "RDPID": return Arch.RDPID;
                case "RDRAND": return Arch.RDRAND;
                case "RDSEED": return Arch.RDSEED;
                case "XSAVEOPT": return Arch.XSAVEOPT;
                case "FSGSBASE": return Arch.FSGSBASE;
                case "LZCNT": return Arch.LZCNT;
                case "F16C": return Arch.F16C;
                case "MPX": return Arch.MPX;
                case "MIB": return Arch.MIB;
                case "SHA": return Arch.SHA;

                #region unused
                case "RTM": return Arch.RTM;

                case "ND": return Arch.ND;
                case "LONG": return Arch.LONG;
                case "NOLONG": return Arch.NOLONG;
                case "UNDOC": return Arch.UNDOC;
                case "PREFETCHWT1": return Arch.PREFETCHWT1;
                #endregion
            }
            Console.WriteLine("WARNING: parseArch: no arch for str " + str);


            return Arch.NONE;
        }

        public static string ToString(IEnumerable<Arch> archs) {
            bool empty = true;
            StringBuilder sb = new StringBuilder();
            foreach (Arch arch in archs) {
                if (!ArchTools.ignoreArch(arch)) {
                    sb.Append(ArchTools.ToString(arch));
                    sb.Append(",");
                    empty = false;
                }
            }
            if (empty) {
                return "";
            } else {
                sb.Length--; // get rid of the last comma;
                sb.Append("]");
                return " [" + sb.ToString();
            }
        }

        public static string ToString(Arch arch) {
            switch (arch) {
                case Arch.ARCH_8086: return "8086";
                case Arch.ARCH_186: return "186";
                case Arch.ARCH_286: return "286";
                case Arch.ARCH_386: return "386";
                case Arch.ARCH_486: return "486";
                case Arch.ARCH_3DNOW: return "3DNOW";
                default:
                    break;
            }
            return arch.ToString();
        }
    }
}
