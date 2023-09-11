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
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text;

    public enum Arch
    {
        ARCH_NONE,

        ARCH_8086,
        ARCH_186,
        ARCH_286,
        ARCH_386,
        ARCH_486,
        /// <summary>1993 (also knonw as i586)</summary>
        ARCH_PENT,
        /// <summary>1995 (also known as i686)</summary>
        ARCH_P6,

        ARCH_MMX,

        ARCH_SSE,
        ARCH_SSE2,
        ARCH_SSE3,
        ARCH_SSSE3,
        ARCH_SSE4_1,
        ARCH_SSE4_2,
        ARCH_SSE4A,
        /// <summary>AMD</summary>
        ARCH_SSE5,

        ARCH_AVX,
        ARCH_AVX2,

        /// <summary>AVX512 foundation (Knights Landing, Intel Xeon)</summary>
        ARCH_AVX512_F,

        /// <summary>AVX512 conflict detection (Knights Landing, Intel Xeon)</summary>
        ARCH_AVX512_CD,

        /// <summary>AVX512 exponential and reciprocal (Knights Landing)</summary>
        ARCH_AVX512_ER,

        /// <summary>AVX512 prefetch (Knights Landing)</summary>
        ARCH_AVX512_PF,

        /// <summary>AVX512 byte and word (Intel Xeon)</summary>
        ARCH_AVX512_BW,

        /// <summary>AVX512 doubleword and quadword (Intel Xeon)</summary>
        ARCH_AVX512_DQ,

        /// <summary>AVX512 Vector Length Extensions (Intel Xeon)</summary>
        /// An additional orthogonal capability known as Vector Length Extensions provide for most AVX-512 instructions
        /// to operate on 128 or 256 bits, instead of only 512. Vector Length Extensions can currently be applied to
        /// most Foundation Instructions, the Conflict Detection Instructions as well as the new Byte, Word, Doubleword
        /// and Quadword instructions. These AVX-512 Vector Length Extensions are indicated by the AVX512VL CPUID flag.
        /// The use of Vector Length Extensions extends most AVX-512 operations to also operate on XMM (128-bit, SSE)
        /// registers and YMM (256-bit, AVX) registers. The use of Vector Length Extensions allows the capabilities of
        /// EVEX encodings, including the use of mask registers and access to registers 16..31, to be applied to XMM
        /// and YMM registers instead of only to ZMM registers.
        ARCH_AVX512_VL,

        /// <summary> Cannon Lake</summary>
        ARCH_AVX512_IFMA,

        /// <summary> Cannon Lake</summary>
        ARCH_AVX512_VBMI,

        /// <summary> Knight Mill, Ice Lake</summary>
        ARCH_AVX512_VPOPCNTDQ,

        /// <summary> Knight Mill</summary>
        ARCH_AVX512_4VNNIW,

        /// <summary> Knight Mill</summary>
        ARCH_AVX512_4FMAPS,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_VBMI2,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_VNNI,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_BITALG,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_GFNI,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_VAES,

        /// <summary> Ice Lake</summary>
        ARCH_AVX512_VPCLMULQDQ,

        /// <summary> Cooper Lake: Support for BFLOAT16 instructions.</summary>
        ARCH_AVX512_BF16,

        /// <summary>Tiger Lake: Support for VP2INTERSECT[D,Q]</summary>
        ARCH_AVX512_VP2INTERSECT,

        #region Misc Intel
        /// <summary>Multi-Precision Add-Carry Instruction Extensions</summary>
        ARCH_ADX,

        /// <summary>Advanced Encryption Standard Instruction Set </summary>
        ARCH_AES,

        /// <summary>Virtual Machine Extensions (VMX)</summary>
        ARCH_VMX,

        /// <summary>Bit Manipulation Instructions Sets 1</summary>
        ARCH_BMI1,

        /// <summary>Bit Manipulation Instructions Sets 2</summary>
        ARCH_BMI2,

        /// <summary>half precision floating point conversion (also known as CVT16) </summary>
        ARCH_F16C,

        /// <summary>Fused Multiply-Add</summary>
        ARCH_FMA,

        /// <summary>TODO</summary>
        ARCH_FSGSBASE,

        /// <summary>Hardware Lock Elision</summary>
        ARCH_HLE,

        /// <summary>Invalidates TLBs, two instructions</summary>
        ARCH_INVPCID,

        /// <summary>Secure Hash Algorithm Extensions</summary>
        ARCH_SHA,

        /// <summary>Transactional Synchronization Extensions</summary>
        ARCH_RTM,

        /// <summary>Memory Protection Extensions</summary>
        ARCH_MPX,

        /// <summary>Two instruction PCLMULQDQ (Carry-Less Multiplication Quadword)</summary>
        ARCH_PCLMULQDQ,

        /// <summary>One instruction LZCNT</summary>
        ARCH_LZCNT,

        /// <summary>One instruction: PREFETCHWT1</summary>
        ARCH_PREFETCHWT1,

        /// <summary>One instruction: PREFETCHW</summary>
        ARCH_PRFCHW,

        /// <summary>One instruction: RDPID (Read Processor ID)</summary>
        ARCH_RDPID,

        /// <summary>One instruction: RDRAND (Read Random Number)</summary>
        ARCH_RDRAND,

        /// <summary>One instruction: RDSEED (Read Random SEED)</summary>
        ARCH_RDSEED,

        /// <summary>One instruction: XSAVEOPT (Save Processor Extended States Optimized)</summary>
        ARCH_XSAVEOPT,
        #endregion

        /// <summary>Software Guard Extensions 1</summary>
        ARCH_SGX1,

        /// <summary>Software Guard Extensions 2</summary>
        ARCH_SGX2,

        /// <summary>SAFER MODE EXTENSIONS</summary>
        ARCH_SMX,

        /// <summary> Cache Line DEMOTE (CPUID.(EAX=0x7, ECX=0):ECX[bit25])</summary>
        ARCH_CLDEMOTE,

        /// <summary> Direct store instructions – Direct store using write combining (WC) for 64B (CPUID.(EAX=?, ECX=?):ECX[bit?])</summary>
        ARCH_MOVDIR64B,

        /// <summary> Direct store instructions – Direct store using write combining (WC) for doublewords (CPUID.(EAX=?, ECX=?):ECX[bit?])</summary>
        ARCH_MOVDIRI,

        /// <summary> (CPUID.(EAX=0x?, ECX=?):ECX[bit?])</summary>
        ARCH_PCONFIG,

        /// <summary> User wait – TPAUSE, UMONITOR, UMWAIT (CPUID.(EAX=0x?, ECX=?):ECX[bit?])</summary>
        ARCH_WAITPKG,

        /// <summary> Sapphire Rapids</summary>
        ARCH_ENQCMD,

        #region Misc Other
        ARCH_X64,

        ARCH_IA64,

        ARCH_UNDOC,
        #endregion

        #region AMD
        ARCH_AMD,

        /// <summary>AMD: Trailing Bit Manipulation</summary>
        ARCH_TBM,

        ARCH_3DNOW,

        #endregion
        ARCH_CYRIX,
        ARCH_CYRIXM,
    }

    public static class ArchTools
    {
        public static Arch ParseArch(string str, bool strIsCapitals, bool warn)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            string str2 = AsmSourceTools.ToCapitals(str, strIsCapitals).Replace("_", string.Empty);
            switch (str2)
            {
                case "NONE": return Arch.ARCH_NONE;

                case "8086": return Arch.ARCH_8086;
                case "186": return Arch.ARCH_186;
                case "286": return Arch.ARCH_286;
                case "386": return Arch.ARCH_386;
                case "486": return Arch.ARCH_486;
                case "PENT": return Arch.ARCH_PENT;
                case "P6": return Arch.ARCH_P6;

                case "MMX": return Arch.ARCH_MMX;
                case "SSE": return Arch.ARCH_SSE;
                case "SSE2": return Arch.ARCH_SSE2;
                case "SSE3": return Arch.ARCH_SSE3;
                case "SSSE3": return Arch.ARCH_SSSE3;
                case "SSE41": return Arch.ARCH_SSE4_1;
                case "SSE42": return Arch.ARCH_SSE4_2;
                case "SSE4A": return Arch.ARCH_SSE4A;
                case "SSE5": return Arch.ARCH_SSE5;

                case "AVX": return Arch.ARCH_AVX;
                case "AVX2": return Arch.ARCH_AVX2;
                case "AVX512VL": return Arch.ARCH_AVX512_VL;
                case "AVX512DQ": return Arch.ARCH_AVX512_DQ;
                case "AVX512BW": return Arch.ARCH_AVX512_BW;
                case "AVX512ER": return Arch.ARCH_AVX512_ER;
                case "AVX512F": return Arch.ARCH_AVX512_F;
                case "AVX512CD": return Arch.ARCH_AVX512_CD;
                case "AVX512PF": return Arch.ARCH_AVX512_PF;

                case "AVX512IFMA": return Arch.ARCH_AVX512_IFMA;
                case "AVX512VBMI": return Arch.ARCH_AVX512_VBMI;
                case "AVX512VPOPCNTDQ": return Arch.ARCH_AVX512_VPOPCNTDQ;
                case "AVX5124VNNIW": return Arch.ARCH_AVX512_4VNNIW;
                case "AVX5124FMAPS": return Arch.ARCH_AVX512_4FMAPS;

                case "VBMI2":
                case "AVX512VBMI2": return Arch.ARCH_AVX512_VBMI2;
                case "VNNI":
                case "AVX512VNNI": return Arch.ARCH_AVX512_VNNI;
                case "BITALG":
                case "AVX512BITALG": return Arch.ARCH_AVX512_BITALG;
                case "GFNI":
                case "AVX512GFNI": return Arch.ARCH_AVX512_GFNI;
                case "VAES":
                case "AVX512VAES": return Arch.ARCH_AVX512_VAES;
                case "VPCLMULQDQ":
                case "AVX512VPCLMULQDQ": return Arch.ARCH_AVX512_VPCLMULQDQ;

                case "AVX512BF16": return Arch.ARCH_AVX512_BF16;
                case "AVX512VP2INTERSECT": return Arch.ARCH_AVX512_VP2INTERSECT;

                case "HLE": return Arch.ARCH_HLE;
                case "BMI1": return Arch.ARCH_BMI1;
                case "BMI2": return Arch.ARCH_BMI2;
                case "FMA": return Arch.ARCH_FMA;
                case "AES": return Arch.ARCH_AES;
                case "TBM": return Arch.ARCH_TBM;

                case "AMD": return Arch.ARCH_AMD;
                case "3DNOW": return Arch.ARCH_3DNOW;
                case "IA64": return Arch.ARCH_IA64;

                case "CYRIX": return Arch.ARCH_CYRIX;
                case "CYRIXM": return Arch.ARCH_CYRIXM;
                case "INVPCID": return Arch.ARCH_INVPCID;
                case "VMX": return Arch.ARCH_VMX;
                case "ADX": return Arch.ARCH_ADX;

                case "X64": return Arch.ARCH_X64;
                case "PCLMULQDQ": return Arch.ARCH_PCLMULQDQ;
                case "RDPID": return Arch.ARCH_RDPID;
                case "RDRAND": return Arch.ARCH_RDRAND;
                case "RDSEED": return Arch.ARCH_RDSEED;

                case "XSAVEOPT": return Arch.ARCH_XSAVEOPT;
                case "XSS": return Arch.ARCH_XSAVEOPT;
                case "XSAVE": return Arch.ARCH_XSAVEOPT;
                case "XSAVEC": return Arch.ARCH_XSAVEOPT;

                case "FSGSBASE": return Arch.ARCH_FSGSBASE;
                case "LZCNT": return Arch.ARCH_LZCNT;
                case "F16C": return Arch.ARCH_F16C;
                case "MPX": return Arch.ARCH_MPX;
                case "SHA": return Arch.ARCH_SHA;
                case "RTM": return Arch.ARCH_RTM;
                case "PREFETCHWT1": return Arch.ARCH_PREFETCHWT1;
                case "PRFCHW": return Arch.ARCH_PRFCHW;

                case "SGX1": return Arch.ARCH_SGX1;
                case "SGX2": return Arch.ARCH_SGX2;
                case "SMX": return Arch.ARCH_SMX;

                case "CLDEMOTE": return Arch.ARCH_CLDEMOTE;
                case "MOVDIR64B": return Arch.ARCH_MOVDIR64B;
                case "MOVDIRI": return Arch.ARCH_MOVDIRI;
                case "PCONFIG": return Arch.ARCH_PCONFIG;
                case "WAITPKG": return Arch.ARCH_WAITPKG;
                case "ENQCMD": return Arch.ARCH_ENQCMD;

                case "UNDOC": return Arch.ARCH_UNDOC;
                default:
                    if (warn)
                    {
                        Console.WriteLine("WARNING: parseArch: no arch for str " + str);
                    }

                    return Arch.ARCH_NONE;
            }
        }

        public static Arch[] ParseArchList(string str, bool strIsCapitals, bool warn)
        {
            var substrArray = str.Split(' ');
            var result = new Arch[substrArray.Length];
            for (int i = 0; i < substrArray.Length; ++i)
            {
                result[i] = ParseArch(substrArray[i], strIsCapitals, warn);
            }
            return result;
        }

        public static string ArchDocumentation(Arch arch)
        {
            switch (arch)
            {
                case Arch.ARCH_NONE: return string.Empty;
                case Arch.ARCH_8086: return string.Empty;
                case Arch.ARCH_186: return string.Empty;
                case Arch.ARCH_286: return string.Empty;
                case Arch.ARCH_386: return string.Empty;
                case Arch.ARCH_486: return string.Empty;
                case Arch.ARCH_PENT: return "Instruction set of the Pentium, 1994 (also known as i585)";
                case Arch.ARCH_P6: return "Instruction set of the Pentium 6, 1995 (also knows as i686)";
                case Arch.ARCH_MMX: return string.Empty;
                case Arch.ARCH_SSE: return string.Empty;
                case Arch.ARCH_SSE2: return string.Empty;
                case Arch.ARCH_SSE3: return string.Empty;
                case Arch.ARCH_SSSE3: return string.Empty;
                case Arch.ARCH_SSE4_1: return string.Empty;
                case Arch.ARCH_SSE4_2: return string.Empty;
                case Arch.ARCH_SSE4A: return "Instruction set SSE4A, AMD";
                case Arch.ARCH_SSE5: return "Instruction set SSE5, AMD";
                case Arch.ARCH_AVX: return string.Empty;
                case Arch.ARCH_AVX2: return string.Empty;
                case Arch.ARCH_AVX512_F: return "AVX512-F - Foundation";
                case Arch.ARCH_AVX512_CD: return "AVX512-CD - Conflict Detection";
                case Arch.ARCH_AVX512_ER: return "AVX512-ER - Exponential and Reciprocal";
                case Arch.ARCH_AVX512_PF: return "AVX512-PF - Prefetch";
                case Arch.ARCH_AVX512_BW: return "AVX512-BW - Byte and Word";
                case Arch.ARCH_AVX512_DQ: return "AVX512-DQ - Doubleword and QuadWord";
                case Arch.ARCH_AVX512_VL: return "AVX512-VL - Vector Length Extensions";
                case Arch.ARCH_AVX512_IFMA: return "AVX512-IFMA - Integer Fused Multiply Add";
                case Arch.ARCH_AVX512_VBMI: return "AVX512-VBMI - Vector Byte Manipulation Instructions";
                case Arch.ARCH_AVX512_VPOPCNTDQ: return "AVX512-VPOPCNTDQ - Vector Population Count instructions for Dwords and Qwords";
                case Arch.ARCH_AVX512_4VNNIW: return "AVX512-4VNNIW - Vector Neural Network Instructions Word variable precision";
                case Arch.ARCH_AVX512_4FMAPS: return "AVX512-4FMAPS - Fused Multiply Accumulation Packed Single precision";
                case Arch.ARCH_AVX512_VBMI2: return "AVX512-VBMI2 - Vector Byte Manipulation Instructions 2";
                case Arch.ARCH_AVX512_VNNI: return "AVX512-VNNI - Vector Neural Network Instructions";
                case Arch.ARCH_AVX512_BITALG: return "AVX512-BITALG - Bit Algorithms";
                case Arch.ARCH_AVX512_GFNI: return " AVX512-GFNI - Galois Field New Instructions";
                case Arch.ARCH_AVX512_VAES: return "AVX512-VPCLMULQDQ - EVEX-encoded Advanced Encryption Standard";
                case Arch.ARCH_AVX512_VPCLMULQDQ: return "AVX512-VPCLMULQDQ";
                case Arch.ARCH_AVX512_BF16: return "AVX512-BF16 - Brain Float 16 extension (Bfloat16)";
                case Arch.ARCH_AVX512_VP2INTERSECT: return "AVX512-VP2INTERSECT - ";

                case Arch.ARCH_ADX: return "Multi-Precision Add-Carry Instruction Extension";
                case Arch.ARCH_AES: return "Advanced Encryption Standard Extension";
                case Arch.ARCH_VMX: return "Virtual Machine Extension";
                case Arch.ARCH_BMI1: return "Bit Manipulation Instruction Set 1";
                case Arch.ARCH_BMI2: return "Bit Manipulation Instruction Set 2";
                case Arch.ARCH_F16C: return "Half Precision Floating Point Conversion Instructions";
                case Arch.ARCH_FMA: return "Fused Multiply-Add Instructions";
                case Arch.ARCH_FSGSBASE: return string.Empty;
                case Arch.ARCH_HLE: return "Hardware Lock Elision Instructions";
                case Arch.ARCH_INVPCID: return "Invalidate Translation Lookaside Buffers (TLBs)";
                case Arch.ARCH_SHA: return "Secure Hash Algorithm Extensions";
                case Arch.ARCH_RTM: return "Transactional Synchronization Extensions";
                case Arch.ARCH_MPX: return "Memory Protection Extensions";
                case Arch.ARCH_PCLMULQDQ: return "Carry-Less Multiplication Instructions";
                case Arch.ARCH_LZCNT: return "Leading zero count";
                case Arch.ARCH_PREFETCHWT1: return string.Empty;
                case Arch.ARCH_PRFCHW: return string.Empty;
                case Arch.ARCH_RDPID: return "Read processor ID";
                case Arch.ARCH_RDRAND: return "Read random number";
                case Arch.ARCH_RDSEED: return "Reed random seed";
                case Arch.ARCH_XSAVEOPT: return "Save Processor Extended States Optimized";
                case Arch.ARCH_X64: return "64-bit Mode Instructions";
                case Arch.ARCH_IA64: return "Intel Architecture 64";
                case Arch.ARCH_UNDOC: return "Undocumented Instructions";
                case Arch.ARCH_AMD: return "AMD";
                case Arch.ARCH_TBM: return "Trailing Bit Manipulation (AMD)";
                case Arch.ARCH_3DNOW: return "3DNow (AMD)";
                case Arch.ARCH_CYRIX: return "Cyrix Instructions Set";
                case Arch.ARCH_CYRIXM: return "Cyrix M Instruction Set";

                case Arch.ARCH_CLDEMOTE: return string.Empty;
                case Arch.ARCH_MOVDIR64B: return string.Empty;
                case Arch.ARCH_MOVDIRI: return string.Empty;
                case Arch.ARCH_PCONFIG: return string.Empty;
                case Arch.ARCH_WAITPKG: return string.Empty;
                case Arch.ARCH_ENQCMD: return "Enqueue Stores";

                default:
                    return string.Empty;
            }
        }

        public static string ToString(IEnumerable<Arch> archs)
        {
            Contract.Requires(archs != null);
            Contract.Assume(archs != null);

            bool empty = true;
            StringBuilder sb = new StringBuilder();
            foreach (Arch arch in archs)
            {
                sb.Append(ToString(arch));
                sb.Append(',');
                empty = false;
            }
            if (empty)
            {
                return string.Empty;
            }
            else
            {
                sb.Length--; // get rid of the last comma;
                sb.Append(']');
                return " [" + sb.ToString();
            }
        }

        public static string ToString(Arch arch)
        {
            switch (arch)
            {
                case Arch.ARCH_NONE: return "NONE";
                case Arch.ARCH_8086: return "8086";
                case Arch.ARCH_186: return "186";
                case Arch.ARCH_286: return "286";
                case Arch.ARCH_386: return "386";
                case Arch.ARCH_486: return "486";
                case Arch.ARCH_PENT: return "PENT";
                case Arch.ARCH_P6: return "P6";
                case Arch.ARCH_MMX: return "MMX";
                case Arch.ARCH_SSE: return "SSE";
                case Arch.ARCH_SSE2: return "SSE2";
                case Arch.ARCH_SSE3: return "SSE3";
                case Arch.ARCH_SSSE3: return "SSSE3";
                case Arch.ARCH_SSE4_1: return "SSE4_1";
                case Arch.ARCH_SSE4_2: return "SSE4_2";
                case Arch.ARCH_SSE4A: return "SSE4A";
                case Arch.ARCH_SSE5: return "SSE5";
                case Arch.ARCH_AVX: return "AVX";
                case Arch.ARCH_AVX2: return "AVX2";
                case Arch.ARCH_AVX512_F: return "AVX512_F";
                case Arch.ARCH_AVX512_CD: return "AVX512_CD";
                case Arch.ARCH_AVX512_ER: return "AVX512_ER";
                case Arch.ARCH_AVX512_PF: return "AVX512_PF";
                case Arch.ARCH_AVX512_BW: return "AVX512_BW";
                case Arch.ARCH_AVX512_DQ: return "AVX512_DQ";
                case Arch.ARCH_AVX512_VL: return "AVX512_VL";
                case Arch.ARCH_AVX512_IFMA: return "AVX512_IFMA";
                case Arch.ARCH_AVX512_VBMI: return "AVX512_VBMI";
                case Arch.ARCH_AVX512_VPOPCNTDQ: return "AVX512_VPOPCNTDQ";
                case Arch.ARCH_AVX512_4VNNIW: return "AVX512_4VNNIW";
                case Arch.ARCH_AVX512_4FMAPS: return "AVX512_4FMAPS";
                case Arch.ARCH_AVX512_VBMI2: return "AVX512_VBMI2";
                case Arch.ARCH_AVX512_VNNI: return "AVX512_VNNI";
                case Arch.ARCH_AVX512_BITALG: return "AVX512_BITALG";
                case Arch.ARCH_AVX512_GFNI: return "AVX512_GFNI";
                case Arch.ARCH_AVX512_VAES: return "AVX512_VAES";
                case Arch.ARCH_AVX512_VPCLMULQDQ: return "AVX512_VPCLMULQDQ";
                case Arch.ARCH_AVX512_BF16: return "AVX512_BF16";
                case Arch.ARCH_AVX512_VP2INTERSECT: return "AVX512_VP2INTERSECT";

                case Arch.ARCH_ADX: return "ADX";
                case Arch.ARCH_AES: return "AES";
                case Arch.ARCH_BMI1: return "BMI1";
                case Arch.ARCH_BMI2: return "BMI2";
                case Arch.ARCH_F16C: return "F16C";
                case Arch.ARCH_FMA: return "FMA";
                case Arch.ARCH_FSGSBASE: return "FSGSBASE";
                case Arch.ARCH_HLE: return "HLE";
                case Arch.ARCH_INVPCID: return "INVPCID";
                case Arch.ARCH_SHA: return "SHA";
                case Arch.ARCH_RTM: return "RTM";
                case Arch.ARCH_MPX: return "MPX";
                case Arch.ARCH_PCLMULQDQ: return "PCLMULQDQ";
                case Arch.ARCH_LZCNT: return "LZCNT";
                case Arch.ARCH_PREFETCHWT1: return "PREFETCHWT1";
                case Arch.ARCH_PRFCHW: return "PRFCHW";
                case Arch.ARCH_RDPID: return "RDPID";
                case Arch.ARCH_RDRAND: return "RDRAND";
                case Arch.ARCH_RDSEED: return "RDSEED";
                case Arch.ARCH_XSAVEOPT: return "XSAVEOPT";
                case Arch.ARCH_SGX1: return "SGX1";
                case Arch.ARCH_SGX2: return "SGX2";
                case Arch.ARCH_SMX: return "SMX";
                case Arch.ARCH_CLDEMOTE: return "CLDEMOTE";
                case Arch.ARCH_MOVDIR64B: return "MOVDIR64B";
                case Arch.ARCH_MOVDIRI: return "MOVDIRI";
                case Arch.ARCH_PCONFIG: return "PCONFIG";
                case Arch.ARCH_WAITPKG: return "WAITPKG";
                case Arch.ARCH_ENQCMD: return "ENQCMD";
                case Arch.ARCH_X64: return "X64";
                case Arch.ARCH_IA64: return "IA64";
                case Arch.ARCH_UNDOC: return "UNDOC";
                case Arch.ARCH_AMD: return "AMD";
                case Arch.ARCH_TBM: return "TBM";
                case Arch.ARCH_3DNOW: return "3DNOW";
                case Arch.ARCH_CYRIX: return "CYRIX";
                case Arch.ARCH_CYRIXM: return "CYRIXM";
                case Arch.ARCH_VMX: return "VMX";
                default:
                    break;
            }
            return arch.ToString();
        }
    }
}
