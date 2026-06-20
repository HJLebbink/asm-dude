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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsmSourceToolsAlias = AsmTools.AsmSourceTools;

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

    #region rev-091 / 2026 SDM additions
    /// <summary>AVX-512 FP16 (half-precision) instructions: Sapphire Rapids onward.</summary>
    ARCH_AVX512_FP16,

    /// <summary>AVX10 (converged vector ISA). In the SDM most EVEX instructions are now listed as
    /// "(AVX512xx AND AVX512xx) OR AVX10.1"; this models the AVX10.1/AVX10.2 alternative.</summary>
    ARCH_AVX10,

    /// <summary>AVX-VNNI (VEX-encoded VNNI), Alder Lake onward.</summary>
    ARCH_AVX_VNNI,

    /// <summary>CMPccXADD (compare-and-add), Sierra Forest / Granite Rapids.</summary>
    ARCH_CMPCCXADD,

    /// <summary>CET shadow stack instructions (CLRSSBSY, INCSSP*, RDSSP*, RSTORSSP, SAVEPREVSSP, WRSS*, WRUSS*).</summary>
    ARCH_CET_SS,

    /// <summary>CET indirect-branch tracking (ENDBR32/ENDBR64).</summary>
    ARCH_CET_IBT,

    /// <summary>Key Locker (AESKLE / wide KL): AES{ENC,DEC}*KL, ENCODEKEY*, LOADIWKEY.</summary>
    ARCH_KEYLOCKER,

    /// <summary>User Interrupts: CLUI, SENDUIPI, STUI, TESTUI, UIRET.</summary>
    ARCH_UINTR,

    /// <summary>Platform key/Total Storage Encryption: PBNDKB.</summary>
    ARCH_PBNDKB,

    /// <summary>Supervisor-Mode Access Prevention: CLAC, STAC.</summary>
    ARCH_SMAP,

    /// <summary>SERIALIZE instruction.</summary>
    ARCH_SERIALIZE,

    /// <summary>WBNOINVD instruction.</summary>
    ARCH_WBNOINVD,

    /// <summary>History reset: HRESET.</summary>
    ARCH_HRESET,

    /// <summary>RDMSRLIST / WRMSRLIST.</summary>
    ARCH_MSRLIST,

    /// <summary>WRMSRNS (non-serializing WRMSR).</summary>
    ARCH_WRMSRNS,

    /// <summary>PTWRITE instruction.</summary>
    ARCH_PTWRITE,

    /// <summary>TSX suspend load address tracking: XRESLDTRK, XSUSLDTRK.</summary>
    ARCH_TSXLDTRK,

    /// <summary>PREFETCHIT0 / PREFETCHIT1 (instruction prefetch).</summary>
    ARCH_PREFETCHI,

    /// <summary>SHA-512 new instructions (VSHA512*).</summary>
    ARCH_SHA512,

    /// <summary>SM3 new instructions (VSM3*).</summary>
    ARCH_SM3,

    /// <summary>SM4 new instructions (VSM4*).</summary>
    ARCH_SM4,

    /// <summary>Advanced Matrix Extensions (tiles): LDTILECFG, TILE*, TDP*, TCMM* (AMX_TILE/INT8/BF16/FP16/COMPLEX).</summary>
    ARCH_AMX,

    /// <summary>AVX VNNI INT8 / INT16 (VEX-encoded): VPDPB*/VPDPW* integer dot-product.</summary>
    ARCH_AVX_VNNI_INT,

    /// <summary>AVX-NE-CONVERT: VCVTNE*, VBCSTNE* (BF16/FP16 conversions).</summary>
    ARCH_AVX_NE_CONVERT,

    /// <summary>AVX-IFMA (VEX-encoded): VPMADD52LUQ/HUQ.</summary>
    ARCH_AVX_IFMA,

    /// <summary>MOVBE (move with byte swap).</summary>
    ARCH_MOVBE,

    /// <summary>Protection Keys for User pages (OSPKE): RDPKRU, WRPKRU.</summary>
    ARCH_PKU,
    #endregion

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

    /// <summary> Cache Line Write Back: CLWB (CPUID.(EAX=0x7, ECX=0):EBX[bit24])</summary>
    ARCH_CLWB,

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
        ArgumentNullException.ThrowIfNull(str);

        string str2 = AsmSourceToolsAlias.ToCapitals(str, strIsCapitals).Replace("_", string.Empty);
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

            // rev-091 / 2026 SDM additions
            case "AVX512FP16": return Arch.ARCH_AVX512_FP16;
            case "AVX10":
            case "AVX10.1":
            case "AVX10.2": return Arch.ARCH_AVX10;
            case "AVXVNNI": return Arch.ARCH_AVX_VNNI;
            case "CMPCCXADD": return Arch.ARCH_CMPCCXADD;
            case "CETSS": return Arch.ARCH_CET_SS;
            case "CETIBT": return Arch.ARCH_CET_IBT;
            case "AESKLE":
            case "KL":
            case "WIDEKL":
            case "AESWIDE":         // wide Key Locker "AES_WIDE" CPUID flag (AES{ENC,DEC}WIDE*KL)
            case "KEYLOCK":         // repairs the "KEY_LOCK ER" PDF line-wrap artifact (LOADIWKEY)
            case "KEYLOCKER": return Arch.ARCH_KEYLOCKER;
            case "UINTR": return Arch.ARCH_UINTR;
            case "PBNDKB": return Arch.ARCH_PBNDKB;
            case "SMAP": return Arch.ARCH_SMAP;
            case "SERIALIZE": return Arch.ARCH_SERIALIZE;
            case "WBNOINVD": return Arch.ARCH_WBNOINVD;
            case "HRESET": return Arch.ARCH_HRESET;
            case "MSRLIST": return Arch.ARCH_MSRLIST;
            case "WRMSRNS": return Arch.ARCH_WRMSRNS;
            case "PTWRITE": return Arch.ARCH_PTWRITE;
            case "TSXLDTRK": return Arch.ARCH_TSXLDTRK;
            case "PREFETCHI":
            case "PREFETCHITI":
            case "PREFETCHIT0":
            case "PREFETCHIT1": return Arch.ARCH_PREFETCHI;
            case "SHA512": return Arch.ARCH_SHA512;
            case "SM3": return Arch.ARCH_SM3;
            case "SM4": return Arch.ARCH_SM4;
            case "AMX":
            case "AMXTILE":
            case "AMXINT8":
            case "AMXBF16":
            case "AMXFP16":
            case "AMXCOMPLEX": return Arch.ARCH_AMX;
            case "AVXVNNIINT":
            case "AVXVNNIINT8":
            case "AVXVNNIINT16": return Arch.ARCH_AVX_VNNI_INT;
            case "AVXNECONVERT": return Arch.ARCH_AVX_NE_CONVERT;
            case "AVXIFMA": return Arch.ARCH_AVX_IFMA;
            case "MOVBE": return Arch.ARCH_MOVBE;
            case "PKU":
            case "OSPKE": return Arch.ARCH_PKU;

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
            case "XSAVES": return Arch.ARCH_XSAVEOPT;
            case "XRSTORS": return Arch.ARCH_XSAVEOPT;

            case "FSGSBASE": return Arch.ARCH_FSGSBASE;
            case "LZCNT": return Arch.ARCH_LZCNT;
            case "F16C": return Arch.ARCH_F16C;
            case "MPX": return Arch.ARCH_MPX;
            case "SHA": return Arch.ARCH_SHA;
            case "RTM": return Arch.ARCH_RTM;
            case "PREFETCHWT1": return Arch.ARCH_PREFETCHWT1;
            case "PREFETCHW":               // SDM "PREFETCHW" CPUID-flag cell == the PRFCHW feature
            case "PRFCHW": return Arch.ARCH_PRFCHW;
            case "CLWB": return Arch.ARCH_CLWB;

            case "SGX1": return Arch.ARCH_SGX1;
            case "EDECCSSA":            // SGX2 enhancement leaf (ENCLU[EDECCSSA]); SDM lists its own CPUID flag
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
                    AsmLog.Warn("TOOLS", $"parseArch: no arch for str \"{str}\"");
                }
                return Arch.ARCH_NONE;
        }
    }

    public static Arch[] ParseArchList(string str, bool strIsCapitals, bool warn)
    {
        //Console.WriteLine($"Arch: ParseArchList \"{str}\"");
        var substrArray = str.Split(',');
        var result = new Arch[substrArray.Length];
        for (int i = 0; i < substrArray.Length; ++i)
        {
            result[i] = ParseArch(substrArray[i], strIsCapitals, warn);
        }
        return result;
    }

    /// <summary>
    /// Parse an architecture requirement in disjunctive normal form (DNF) from the signature-file
    /// arch column: groups separated by ',' are OR-alternatives, members within a group separated
    /// by '+' are AND-ed. E.g. "AVX512_VL+AVX512_F,AVX10" => (VL AND F) OR (AVX10). Backward
    /// compatible: a plain comma list (no '+') yields singleton AND-groups, i.e. a pure OR — the
    /// historical meaning of the column. Unknown tokens (ARCH_NONE) are dropped; a group that
    /// becomes empty is dropped; an entirely empty requirement returns an empty array (= no
    /// architecture constraint, always allowed).
    /// </summary>
    public static Arch[][] ParseArchDnf(string str, bool strIsCapitals, bool warn)
    {
        ArgumentNullException.ThrowIfNull(str);
        var groups = new List<Arch[]>();
        foreach (string orPart in str.Split(','))
        {
            var members = new List<Arch>();
            foreach (string andPart in orPart.Split('+'))
            {
                string t = andPart.Trim();
                if (t.Length == 0)
                {
                    continue;
                }

                Arch a = ParseArch(t, strIsCapitals, warn);
                if ((a != Arch.ARCH_NONE) && !members.Contains(a))
                {
                    members.Add(a);
                }
            }
            if (members.Count > 0)
            {
                groups.Add([.. members]);
            }
        }
        return [.. groups];
    }

    /// <summary>Render a DNF arch requirement to the signature-file machine format
    /// ("AVX512_VL+AVX512_F,AVX10"): '+' between AND-members, ',' between OR-groups.</summary>
    public static string ToStringDnf(IEnumerable<IEnumerable<Arch>> dnf)
    {
        ArgumentNullException.ThrowIfNull(dnf);
        var orParts = new List<string>();
        foreach (IEnumerable<Arch> group in dnf)
        {
            var andParts = new List<string>();
            foreach (Arch a in group)
            {
                andParts.Add(ToString(a));
            }
            if (andParts.Count > 0)
            {
                orParts.Add(string.Join("+", andParts));
            }
        }
        return string.Join(",", orParts);
    }

    /// <summary>
    /// Parse a CPUID feature-flag boolean expression (as written in the Intel SDM) into an
    /// architecture requirement in disjunctive normal form (DNF). The expression is over
    /// feature-flag tokens using AND, OR, parentheses, and implicit AND (juxtaposition), e.g.:
    ///   "(AVX512VL AND AVX512F) OR AVX10.1" => [[VL,F],[AVX10]]
    ///   "AVX512F OR AVX10.1"                => [[F],[AVX10]]
    ///   "(AVX512F OR AVX10.1) GFNI"         => [[F,GFNI],[AVX10,GFNI]]
    ///   "AVX SM4"                           => [[AVX,SM4]]
    /// Unknown tokens (ARCH_NONE) are dropped, empty groups removed, duplicate arches/groups
    /// de-duplicated. An empty/unparseable expression yields an empty array (no constraint).
    /// Distinct from <see cref="ParseArchDnf"/>, which parses the already-flattened signature-file
    /// format ("VL+F,AVX10"); this parses the original SDM boolean text.
    /// </summary>
    public static Arch[][] ParseArchExpression(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        List<string> tokens = TokenizeArchExpr(str);
        int pos = 0;
        List<List<Arch>> dnf = (tokens.Count == 0) ? [] : ParseExprOr(tokens, ref pos);

        var cleaned = new List<Arch[]>();
        var seen = new HashSet<string>();
        foreach (List<Arch> group in dnf)
        {
            var g = new List<Arch>();
            foreach (Arch a in group)
            {
                if ((a != Arch.ARCH_NONE) && !g.Contains(a))
                {
                    g.Add(a);
                }
            }
            if (g.Count == 0)
            {
                continue;
            }
            string key = string.Join("+", g.Select(a => a.ToString()).OrderBy(x => x));
            if (seen.Add(key))
            {
                cleaned.Add([.. g]);
            }
        }
        return [.. cleaned];
    }

    // Split a CPUID-flag expression into tokens: '(' ')' are single tokens; whitespace and ','
    // separate tokens; "AND"/"OR" are keywords; everything else is a feature-flag token.
    // "_ " is a wrap artifact inside one flag ("AVX_NE_ CONVERT" -> "AVX_NE_CONVERT").
    private static List<string> TokenizeArchExpr(string str)
    {
        string s = str.Replace("_ ", "_");
        var tokens = new List<string>();
        var sb = new StringBuilder();
        void Flush()
        {
            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }
        }
        foreach (char c in s)
        {
            if ((c == '(') || (c == ')'))
            {
                Flush();
                tokens.Add(c.ToString());
            }
            else if (char.IsWhiteSpace(c) || (c == ','))
            {
                Flush();
            }
            else
            {
                sb.Append(c);
            }
        }
        Flush();
        return tokens;
    }

    // OR := AND ( "OR" AND )*   — OR is the union of the operands' groups.
    private static List<List<Arch>> ParseExprOr(List<string> t, ref int pos)
    {
        List<List<Arch>> result = ParseExprAnd(t, ref pos);
        while ((pos < t.Count) && t[pos].Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            pos++; // consume OR
            result.AddRange(ParseExprAnd(t, ref pos));
        }
        return result;
    }

    // AND := primary ( ("AND")? primary )*   — implicit AND by juxtaposition; cartesian product.
    private static List<List<Arch>> ParseExprAnd(List<string> t, ref int pos)
    {
        List<List<Arch>> result = ParseExprPrimary(t, ref pos);
        while (pos < t.Count)
        {
            string tok = t[pos];
            if ((tok == ")") || tok.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (tok.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                pos++; // consume AND
            }
            int before = pos;
            List<List<Arch>> rhs = ParseExprPrimary(t, ref pos);
            if (pos == before)
            {
                break; // safety: no progress (e.g. stray AND at end)
            }
            result = DistributeArch(result, rhs);
        }
        return result;
    }

    // primary := "(" OR ")" | FLAG
    private static List<List<Arch>> ParseExprPrimary(List<string> t, ref int pos)
    {
        if (pos >= t.Count)
        {
            return [];
        }
        string tok = t[pos];
        if (tok == "(")
        {
            pos++; // consume (
            List<List<Arch>> inner = ParseExprOr(t, ref pos);
            if ((pos < t.Count) && (t[pos] == ")"))
            {
                pos++; // consume )
            }
            return inner;
        }
        if (tok == ")")
        {
            return []; // unmatched, let caller handle
        }
        pos++; // consume flag
        return [[ParseArch(tok, false, false)]];
    }

    // (a OR b) AND (c OR d) => ac, ad, bc, bd
    private static List<List<Arch>> DistributeArch(List<List<Arch>> left, List<List<Arch>> right)
    {
        if (left.Count == 0)
        {
            return right;
        }
        if (right.Count == 0)
        {
            return left;
        }
        var result = new List<List<Arch>>();
        foreach (List<Arch> lg in left)
        {
            foreach (List<Arch> rg in right)
            {
                var combined = new List<Arch>(lg);
                combined.AddRange(rg);
                result.Add(combined);
            }
        }
        return result;
    }

    /// <summary>Render a DNF arch requirement for humans ("(AVX512_VL AND AVX512_F) OR AVX10").
    /// Single-member groups are not parenthesised. Returns an empty string for no constraint.</summary>
    public static string ToStringDnfHuman(IEnumerable<IEnumerable<Arch>> dnf)
    {
        ArgumentNullException.ThrowIfNull(dnf);
        var orParts = new List<string>();
        foreach (IEnumerable<Arch> group in dnf)
        {
            var andParts = new List<string>();
            foreach (Arch a in group)
            {
                andParts.Add(ToString(a));
            }
            if (andParts.Count == 1)
            {
                orParts.Add(andParts[0]);
            }
            else if (andParts.Count > 1)
            {
                orParts.Add("(" + string.Join(" AND ", andParts) + ")");
            }
        }
        return string.Join(" OR ", orParts);
    }

    public static string ArchDocumentation(Arch arch)
    {
        return arch switch
        {
            Arch.ARCH_NONE => string.Empty,
            Arch.ARCH_8086 => string.Empty,
            Arch.ARCH_186 => string.Empty,
            Arch.ARCH_286 => string.Empty,
            Arch.ARCH_386 => string.Empty,
            Arch.ARCH_486 => string.Empty,
            Arch.ARCH_PENT => "Instruction set of the Pentium, 1994 (also known as i585)",
            Arch.ARCH_P6 => "Instruction set of the Pentium 6, 1995 (also knows as i686)",
            Arch.ARCH_MMX => string.Empty,
            Arch.ARCH_SSE => string.Empty,
            Arch.ARCH_SSE2 => string.Empty,
            Arch.ARCH_SSE3 => string.Empty,
            Arch.ARCH_SSSE3 => string.Empty,
            Arch.ARCH_SSE4_1 => string.Empty,
            Arch.ARCH_SSE4_2 => string.Empty,
            Arch.ARCH_SSE4A => "Instruction set SSE4A, AMD",
            Arch.ARCH_SSE5 => "Instruction set SSE5, AMD",
            Arch.ARCH_AVX => string.Empty,
            Arch.ARCH_AVX2 => string.Empty,
            Arch.ARCH_AVX512_F => "AVX512-F - Foundation",
            Arch.ARCH_AVX512_CD => "AVX512-CD - Conflict Detection",
            Arch.ARCH_AVX512_ER => "AVX512-ER - Exponential and Reciprocal",
            Arch.ARCH_AVX512_PF => "AVX512-PF - Prefetch",
            Arch.ARCH_AVX512_BW => "AVX512-BW - Byte and Word",
            Arch.ARCH_AVX512_DQ => "AVX512-DQ - Doubleword and QuadWord",
            Arch.ARCH_AVX512_VL => "AVX512-VL - Vector Length Extensions",
            Arch.ARCH_AVX512_IFMA => "AVX512-IFMA - Integer Fused Multiply Add",
            Arch.ARCH_AVX512_VBMI => "AVX512-VBMI - Vector Byte Manipulation Instructions",
            Arch.ARCH_AVX512_VPOPCNTDQ => "AVX512-VPOPCNTDQ - Vector Population Count instructions for Dwords and Qwords",
            Arch.ARCH_AVX512_4VNNIW => "AVX512-4VNNIW - Vector Neural Network Instructions Word variable precision",
            Arch.ARCH_AVX512_4FMAPS => "AVX512-4FMAPS - Fused Multiply Accumulation Packed Single precision",
            Arch.ARCH_AVX512_VBMI2 => "AVX512-VBMI2 - Vector Byte Manipulation Instructions 2",
            Arch.ARCH_AVX512_VNNI => "AVX512-VNNI - Vector Neural Network Instructions",
            Arch.ARCH_AVX512_BITALG => "AVX512-BITALG - Bit Algorithms",
            Arch.ARCH_AVX512_GFNI => " AVX512-GFNI - Galois Field New Instructions",
            Arch.ARCH_AVX512_VAES => "AVX512-VPCLMULQDQ - EVEX-encoded Advanced Encryption Standard",
            Arch.ARCH_AVX512_VPCLMULQDQ => "AVX512-VPCLMULQDQ",
            Arch.ARCH_AVX512_BF16 => "AVX512-BF16 - Brain Float 16 extension (Bfloat16)",
            Arch.ARCH_AVX512_VP2INTERSECT => "AVX512-VP2INTERSECT - ",
            Arch.ARCH_ADX => "Multi-Precision Add-Carry Instruction Extension",
            Arch.ARCH_AES => "Advanced Encryption Standard Extension",
            Arch.ARCH_VMX => "Virtual Machine Extension",
            Arch.ARCH_BMI1 => "Bit Manipulation Instruction Set 1",
            Arch.ARCH_BMI2 => "Bit Manipulation Instruction Set 2",
            Arch.ARCH_F16C => "Half Precision Floating Point Conversion Instructions",
            Arch.ARCH_FMA => "Fused Multiply-Add Instructions",
            Arch.ARCH_FSGSBASE => string.Empty,
            Arch.ARCH_HLE => "Hardware Lock Elision Instructions",
            Arch.ARCH_INVPCID => "Invalidate Translation Lookaside Buffers (TLBs)",
            Arch.ARCH_SHA => "Secure Hash Algorithm Extensions",
            Arch.ARCH_RTM => "Transactional Synchronization Extensions",
            Arch.ARCH_MPX => "Memory Protection Extensions",
            Arch.ARCH_PCLMULQDQ => "Carry-Less Multiplication Instructions",
            Arch.ARCH_LZCNT => "Leading zero count",
            Arch.ARCH_PREFETCHWT1 => string.Empty,
            Arch.ARCH_PRFCHW => string.Empty,
            Arch.ARCH_RDPID => "Read processor ID",
            Arch.ARCH_RDRAND => "Read random number",
            Arch.ARCH_RDSEED => "Reed random seed",
            Arch.ARCH_XSAVEOPT => "Save Processor Extended States Optimized",
            Arch.ARCH_X64 => "64-bit Mode Instructions",
            Arch.ARCH_IA64 => "Intel Architecture 64",
            Arch.ARCH_UNDOC => "Undocumented Instructions",
            Arch.ARCH_AMD => "AMD",
            Arch.ARCH_TBM => "Trailing Bit Manipulation (AMD)",
            Arch.ARCH_3DNOW => "3DNow (AMD)",
            Arch.ARCH_CYRIX => "Cyrix Instructions Set",
            Arch.ARCH_CYRIXM => "Cyrix M Instruction Set",
            Arch.ARCH_CLDEMOTE => string.Empty,
            Arch.ARCH_MOVDIR64B => string.Empty,
            Arch.ARCH_MOVDIRI => string.Empty,
            Arch.ARCH_PCONFIG => string.Empty,
            Arch.ARCH_WAITPKG => string.Empty,
            Arch.ARCH_ENQCMD => "Enqueue Stores",
            Arch.ARCH_AVX512_FP16 => "AVX512-FP16 - Half-precision floating-point instructions",
            Arch.ARCH_AVX10 => "AVX10 - Converged vector ISA (AVX10.1 / AVX10.2)",
            Arch.ARCH_AVX_VNNI => "AVX-VNNI - VEX-encoded Vector Neural Network Instructions",
            Arch.ARCH_CMPCCXADD => "CMPccXADD - Compare and add if condition is met",
            Arch.ARCH_CET_SS => "CET - Control-flow Enforcement Technology (shadow stack)",
            Arch.ARCH_CET_IBT => "CET - Control-flow Enforcement Technology (indirect branch tracking)",
            Arch.ARCH_KEYLOCKER => "Key Locker - AES key wrapping instructions",
            Arch.ARCH_UINTR => "User Interrupts",
            Arch.ARCH_PBNDKB => "Total Storage Encryption - PBNDKB",
            Arch.ARCH_SMAP => "Supervisor-Mode Access Prevention",
            Arch.ARCH_SERIALIZE => "Serialize instruction execution",
            Arch.ARCH_WBNOINVD => "Write Back and Do Not Invalidate Cache",
            Arch.ARCH_HRESET => "History reset",
            Arch.ARCH_MSRLIST => "Read/Write list of MSRs",
            Arch.ARCH_WRMSRNS => "Non-serializing Write to Model Specific Register",
            Arch.ARCH_PTWRITE => "Write data to a Processor Trace packet",
            Arch.ARCH_TSXLDTRK => "TSX Suspend Load Address Tracking",
            Arch.ARCH_PREFETCHI => "Prefetch instruction into caches",
            Arch.ARCH_SHA512 => "SHA-512 Secure Hash Algorithm Extensions",
            Arch.ARCH_SM3 => "SM3 Hash Extensions",
            Arch.ARCH_SM4 => "SM4 Cipher Extensions",
            Arch.ARCH_AMX => "AMX - Advanced Matrix Extensions (tiles)",
            Arch.ARCH_AVX_VNNI_INT => "AVX-VNNI-INT8/INT16 - VEX-encoded integer dot-product",
            Arch.ARCH_AVX_NE_CONVERT => "AVX-NE-CONVERT - BF16/FP16 conversion instructions",
            Arch.ARCH_AVX_IFMA => "AVX-IFMA - VEX-encoded Integer Fused Multiply-Add",
            Arch.ARCH_MOVBE => "MOVBE - Move data after swapping bytes",
            Arch.ARCH_PKU => "Protection Keys for User pages",
            _ => string.Empty,
        };
    }

    public static string ToString(IEnumerable<Arch> archs)
    {
        ArgumentNullException.ThrowIfNull(archs);

        bool empty = true;
        StringBuilder sb = new();
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

    // ── Instruction-set profiles ────────────────────────────────────────────────────────────────
    // A profile is a one-click preset selecting a whole family of architectures, so users need not
    // toggle the ~100 individual CPUID flags by hand (see ArchProfileKeys). The v1–v4 sets follow the
    // x86-64 psABI microarchitecture levels but are pragmatically inclusive (each level also enables
    // its era's widely-available crypto/bit extensions) because this drives editor completion.

    private static readonly Arch[] ProfileV1 =
    [
        Arch.ARCH_8086, Arch.ARCH_186, Arch.ARCH_286, Arch.ARCH_386, Arch.ARCH_486,
        Arch.ARCH_PENT, Arch.ARCH_P6, Arch.ARCH_X64, Arch.ARCH_MMX, Arch.ARCH_SSE, Arch.ARCH_SSE2,
    ];

    private static readonly Arch[] ProfileV2Add =
    [
        Arch.ARCH_SSE3, Arch.ARCH_SSSE3, Arch.ARCH_SSE4_1, Arch.ARCH_SSE4_2, Arch.ARCH_AES, Arch.ARCH_PCLMULQDQ,
    ];

    private static readonly Arch[] ProfileV3Add =
    [
        Arch.ARCH_AVX, Arch.ARCH_AVX2, Arch.ARCH_FMA, Arch.ARCH_BMI1, Arch.ARCH_BMI2, Arch.ARCH_F16C,
        Arch.ARCH_LZCNT, Arch.ARCH_MOVBE, Arch.ARCH_RDRAND, Arch.ARCH_RDSEED, Arch.ARCH_ADX,
        Arch.ARCH_FSGSBASE, Arch.ARCH_INVPCID, Arch.ARCH_RDPID,
    ];

    private static readonly Arch[] ProfileV4Add =
    [
        Arch.ARCH_AVX512_F, Arch.ARCH_AVX512_CD, Arch.ARCH_AVX512_ER, Arch.ARCH_AVX512_PF,
        Arch.ARCH_AVX512_BW, Arch.ARCH_AVX512_DQ, Arch.ARCH_AVX512_VL, Arch.ARCH_AVX512_IFMA,
        Arch.ARCH_AVX512_VBMI, Arch.ARCH_AVX512_VPOPCNTDQ, Arch.ARCH_AVX512_4VNNIW, Arch.ARCH_AVX512_4FMAPS,
        Arch.ARCH_AVX512_VBMI2, Arch.ARCH_AVX512_VNNI, Arch.ARCH_AVX512_BITALG, Arch.ARCH_AVX512_GFNI,
        Arch.ARCH_AVX512_VAES, Arch.ARCH_AVX512_VPCLMULQDQ, Arch.ARCH_AVX512_BF16,
        Arch.ARCH_AVX512_VP2INTERSECT, Arch.ARCH_AVX512_FP16, Arch.ARCH_SHA,
    ];

    // Deprecated / vendor-legacy arches excluded from the "Latest" profile (still reachable via Custom or Everything).
    private static readonly Arch[] LatestExclusions =
    [
        Arch.ARCH_SSE4A, Arch.ARCH_SSE5, Arch.ARCH_AMD, Arch.ARCH_TBM, Arch.ARCH_3DNOW,
        Arch.ARCH_CYRIX, Arch.ARCH_CYRIXM, Arch.ARCH_IA64, Arch.ARCH_UNDOC,
    ];

    // Built once, read-only thereafter (TryGetProfileArchs per arch check). Frozen to document
    // immutability and speed the per-mnemonic membership test; never mutate after build.
    private static readonly FrozenDictionary<string, FrozenSet<Arch>> ProfileSets = BuildProfileSets();

    private static FrozenDictionary<string, FrozenSet<Arch>> BuildProfileSets()
    {
        var v1 = new HashSet<Arch>(ProfileV1);
        var v2 = new HashSet<Arch>(v1); v2.UnionWith(ProfileV2Add);
        var v3 = new HashSet<Arch>(v2); v3.UnionWith(ProfileV3Add);
        var v4 = new HashSet<Arch>(v3); v4.UnionWith(ProfileV4Add);

        var everything = new HashSet<Arch>();
        foreach (Arch a in Enum.GetValues<Arch>())
        {
            if (a != Arch.ARCH_NONE) everything.Add(a);
        }

        var latest = new HashSet<Arch>(everything);
        latest.ExceptWith(LatestExclusions);

        return new Dictionary<string, FrozenSet<Arch>>(StringComparer.OrdinalIgnoreCase)
        {
            [ArchProfileKeys.V1] = v1.ToFrozenSet(),
            [ArchProfileKeys.V2] = v2.ToFrozenSet(),
            [ArchProfileKeys.V3] = v3.ToFrozenSet(),
            [ArchProfileKeys.V4] = v4.ToFrozenSet(),
            [ArchProfileKeys.Latest] = latest.ToFrozenSet(),
            [ArchProfileKeys.Everything] = everything.ToFrozenSet(),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Expands an instruction-set <paramref name="profile"/> key (see <see cref="ArchProfileKeys"/>) into
    /// the concrete set of architectures it enables. Returns <c>false</c> for <see cref="ArchProfileKeys.Custom"/>
    /// (and any unknown key), signalling the caller to fall back to the individual ARCH_* toggles.
    /// <c>Arch.ARCH_NONE</c> (always-available instructions) is never part of a profile set; callers treat it
    /// as always-on regardless.
    /// </summary>
    public static bool TryGetProfileArchs(string? profile, out IReadOnlySet<Arch> archs)
    {
        if (!string.IsNullOrWhiteSpace(profile) && ProfileSets.TryGetValue(profile.Trim(), out FrozenSet<Arch>? set))
        {
            archs = set;
            return true;
        }
        archs = new HashSet<Arch>();
        return false;
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

            case Arch.ARCH_AVX512_FP16: return "AVX512_FP16";
            case Arch.ARCH_AVX10: return "AVX10";
            case Arch.ARCH_AVX_VNNI: return "AVX_VNNI";
            case Arch.ARCH_CMPCCXADD: return "CMPCCXADD";
            case Arch.ARCH_CET_SS: return "CET_SS";
            case Arch.ARCH_CET_IBT: return "CET_IBT";
            case Arch.ARCH_KEYLOCKER: return "KEYLOCKER";
            case Arch.ARCH_UINTR: return "UINTR";
            case Arch.ARCH_PBNDKB: return "PBNDKB";
            case Arch.ARCH_SMAP: return "SMAP";
            case Arch.ARCH_SERIALIZE: return "SERIALIZE";
            case Arch.ARCH_WBNOINVD: return "WBNOINVD";
            case Arch.ARCH_HRESET: return "HRESET";
            case Arch.ARCH_MSRLIST: return "MSRLIST";
            case Arch.ARCH_WRMSRNS: return "WRMSRNS";
            case Arch.ARCH_PTWRITE: return "PTWRITE";
            case Arch.ARCH_TSXLDTRK: return "TSXLDTRK";
            case Arch.ARCH_PREFETCHI: return "PREFETCHI";
            case Arch.ARCH_SHA512: return "SHA512";
            case Arch.ARCH_SM3: return "SM3";
            case Arch.ARCH_SM4: return "SM4";
            case Arch.ARCH_AMX: return "AMX";
            case Arch.ARCH_AVX_VNNI_INT: return "AVX_VNNI_INT";
            case Arch.ARCH_AVX_NE_CONVERT: return "AVX_NE_CONVERT";
            case Arch.ARCH_AVX_IFMA: return "AVX_IFMA";
            case Arch.ARCH_MOVBE: return "MOVBE";
            case Arch.ARCH_PKU: return "PKU";

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
            case Arch.ARCH_CLWB: return "CLWB";
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
