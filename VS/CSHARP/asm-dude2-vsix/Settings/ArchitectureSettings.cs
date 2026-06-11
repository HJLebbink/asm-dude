// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

#pragma warning disable VSEXTPREVIEW_SETTINGS // Settings API is in preview

namespace AsmDude2.Settings;

using AsmTools;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

/// <summary>
/// Instruction set architecture toggles. Controls which instructions appear
/// in code completion and signature help. All in one flat page, ordered by era.
///
/// <para>The <see cref="ArchProfile"/> dropdown is the primary control: it selects a whole family of
/// architectures in one click. The ~100 individual toggles below it are only used when the profile is
/// <see cref="ArchProfileKeys.Custom"/> (they are disabled otherwise via <see cref="SettingRule"/>).</para>
/// </summary>
internal static class ArchitectureSettings
{
    [VisualStudioContribution]
    internal static SettingCategory ArchCategory { get; } =
        new("architectures", "Instruction Sets", AsmDudeSettings.AsmDude2Category)
        {
            Description = "Select which instruction set architectures to include in code completion and signature help",
            Order = 4,
        };

    // One-click instruction-set profile. Anything other than Custom OVERRIDES every individual toggle
    // below (interpreted server-side by ArchTools.TryGetProfileArchs); Custom honors the toggles. Keys
    // are shared with the server via ArchProfileKeys so a rename is a compile error.
    [VisualStudioContribution]
    internal static Setting.Enum ArchProfile { get; } =
        new("archProfile", "Instruction set profile", ArchCategory,
            [
                new(ArchProfileKeys.Everything, "Everything (all architectures incl. legacy/vendor)"),
                new(ArchProfileKeys.Latest, "Latest Intel (everything modern; no Cyrix/3DNow/IA-64)"),
                new(ArchProfileKeys.V4, "x86-64-v4 (baseline → AVX-512)"),
                new(ArchProfileKeys.V3, "x86-64-v3 (baseline → AVX2/FMA/BMI)"),
                new(ArchProfileKeys.V2, "x86-64-v2 (baseline → SSE4.2/AES)"),
                new(ArchProfileKeys.V1, "x86-64-v1 (baseline → SSE2)"),
                new(ArchProfileKeys.Custom, "Custom (use the individual toggles below)"),
            ],
            defaultValue: ArchProfileKeys.V4)
        {
            Description = "Pick a CPU instruction-set level instead of toggling individual feature flags. "
                + "Only 'Custom' uses the detailed toggles below; any other profile overrides them.",
        };

    // The individual toggles below only take effect when the profile is Custom.
    private static SettingRule ArchProfileIsCustom => SettingRule.Equal(ArchProfile, ArchProfileKeys.Custom);

    // ── Base processors (Order 0-9) ──────────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean Arch8086 { get; } =
        new("arch8086", "8086", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Original 8086/8088: MOV, ADD, SUB, MUL, DIV, PUSH, POP, JMP, CALL, RET, INT, CMP, TEST, AND, OR, XOR, SHL, SHR, LEA, LDS, LES, REP, MOVSB, LODSB, STOSB, CMPSB, SCASB",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean Arch186 { get; } =
        new("arch186", "186", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "80186: BOUND, ENTER, LEAVE, INS, OUTS, PUSHA, POPA, IMUL imm",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean Arch286 { get; } =
        new("arch286", "286", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "80286 protected mode: ARPL, CLTS, LAR, LGDT, LIDT, LLDT, LMSW, LSL, LTR, SGDT, SIDT, SLDT, SMSW, STR, VERR, VERW",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean Arch386 { get; } =
        new("arch386", "386", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "80386 32-bit: BSF, BSR, BT, BTC, BTR, BTS, CDQ, CWDE, MOVSX, MOVZX, SETcc, SHLD, SHRD, BSWAP",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean Arch486 { get; } =
        new("arch486", "486", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "80486: BSWAP, CMPXCHG, INVD, INVLPG, WBINVD, XADD",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPent { get; } =
        new("archPent", "Pentium", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Pentium (P5): CMPXCHG8B, CPUID, RDMSR, RDTSC, WRMSR, RSM",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchP6 { get; } =
        new("archP6", "P6", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Pentium Pro (P6): CMOVcc, FCMOV, FCOMI, FCOMIP, RDPMC, SYSENTER, SYSEXIT, UD2",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchX64 { get; } =
        new("archX64", "X64 (AMD64/Intel 64)", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "64-bit long mode: MOVSXD, CDQE, CQO, SYSCALL, SYSRET, SWAPGS, plus 64-bit register variants (RAX, R8-R15)",
        };

    // ── SIMD (Order 10-29) ───────────────────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMMX { get; } =
        new("archMMX", "MMX", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "MultiMedia Extensions (64-bit, MM0-MM7): MOVD, MOVQ, PADDB/W/D, PSUBB/W/D, PMULLW, PMULHW, PAND, POR, PXOR, PCMPEQB/W/D, PACKSSWB/DW, PUNPCKLBW/WD/DQ",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE { get; } =
        new("archSSE", "SSE", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Streaming SIMD Extensions (128-bit, XMM0-XMM7): MOVAPS, MOVUPS, ADDPS, MULPS, SUBPS, DIVPS, MINPS, MAXPS, SQRTPS, RCPPS, RSQRTPS, CMPPS, SHUFPS, UNPCKLPS, CVTPS2PI, PREFETCH, SFENCE",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE2 { get; } =
        new("archSSE2", "SSE2", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE2 (double-precision + integer in XMM): MOVAPD, ADDPD, MULPD, SUBPD, DIVPD, MOVDQA, PADDQ, PSUBQ, PMULUDQ, PSHUFD, PUNPCKLQDQ, CVTPD2PS, MFENCE, LFENCE, CLFLUSH",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE3 { get; } =
        new("archSSE3", "SSE3", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE3 (Prescott): ADDSUBPS, ADDSUBPD, HADDPS, HADDPD, HSUBPS, HSUBPD, MOVDDUP, MOVSHDUP, MOVSLDUP, LDDQU, FISTTP, MONITOR, MWAIT",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSSE3 { get; } =
        new("archSSSE3", "SSSE3", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Supplemental SSE3 (Core 2): PSHUFB, PHADDW/D, PHSUBW/D, PMADDUBSW, PMULHRSW, PABSB/W/D, PSIGNB/W/D, PALIGNR",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE41 { get; } =
        new("archSSE41", "SSE4.1", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE4.1 (Penryn): PBLENDVB, BLENDVPS/PD, BLENDPS/PD, DPPS, DPPD, INSERTPS, EXTRACTPS, PINSRB/D/Q, PEXTRB/W/D/Q, PMOVSX/ZX, PMULDQ, PMULLD, PMINUW/UD/SB/SD, PMAXUW/UD/SB/SD, PTEST, ROUNDPS/PD/SS/SD, MPSADBW, PHMINPOSUW, MOVNTDQA",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE42 { get; } =
        new("archSSE42", "SSE4.2", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE4.2 (Nehalem): PCMPISTRI, PCMPISTRM, PCMPESTRI, PCMPESTRM (string comparison), CRC32, POPCNT",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX { get; } =
        new("archAVX", "AVX", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Advanced Vector Extensions (256-bit, YMM0-YMM15): VEX-encoded 3-operand versions of SSE instructions, VBROADCASTSS/SD/F128, VINSERTF128, VEXTRACTF128, VPERMILPS/PD, VPERM2F128, VMASKMOVPS/PD, VTESTPS/PD, VZEROALL, VZEROUPPER",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX2 { get; } =
        new("archAVX2", "AVX2", ArchCategory, defaultValue: true)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX2 (Haswell): 256-bit integer SIMD. VPBROADCASTB/W/D/Q, VPERMD, VPERMPS, VPERMQ, VPERMPD, VPERM2I128, VINSERTI128, VEXTRACTI128, VGATHERDPS/DPD/QPD/QPS, VPSLLVD/Q, VPSRLVD/Q, VPSRAVD, VPMASKMOVD/Q",
        };

    // ── AVX-512 (Order 30-49) ────────────────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512F { get; } =
        new("archAVX512F", "AVX-512 Foundation", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512F (Skylake-X, KNL): 512-bit vectors (ZMM0-ZMM31), opmask registers (k0-k7). EVEX encoding, VADDPS/PD, VMULPS/PD, VFMADD132PS/PD, VPTERNLOGD/Q, VBLENDMPS/PD, VCOMPRESS, VEXPAND, VPERMI2D/Q/PS/PD, VSCATTER, VGATHER, VCVTPS2UDQ, VPMOVQD, VRNDSCALE, VFIXUPIMM",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512CD { get; } =
        new("archAVX512CD", "AVX-512 Conflict Detection", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512CD (Skylake-X, KNL): VPCONFLICTD/Q, VPLZCNTD/Q, VPBROADCASTMB2Q, VPBROADCASTMW2D — conflict detection for vectorized scatter/gather",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512ER { get; } =
        new("archAVX512ER", "AVX-512 Exponential & Reciprocal", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512ER (KNL, Knights Mill): VEXP2PS/PD, VRCP28PS/PD/SS/SD, VRSQRT28PS/PD/SS/SD — fast approximate transcendentals for HPC",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512PF { get; } =
        new("archAVX512PF", "AVX-512 Prefetch", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512PF (KNL, Knights Mill): VGATHERPF0DPS/QPD, VGATHERPF1DPS/QPD, VSCATTERPF0DPS/QPD, VSCATTERPF1DPS/QPD — gather/scatter prefetch for HPC",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512BW { get; } =
        new("archAVX512BW", "AVX-512 Byte & Word", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512BW (Skylake-X): 512-bit byte/word integer operations. VPADDB/W, VPSUBB/W, VPMULLW, VPACKSSWB/USWB, VPSHUFB, VPALIGNR, VPMOVWB, VPERMW, VPSLLW, VPSRLW, VDBPSADBW — extends AVX-512F from 32/64-bit to 8/16-bit element width",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512DQ { get; } =
        new("archAVX512DQ", "AVX-512 Doubleword & Quadword", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512DQ (Skylake-X): VCVTQQ2PS/PD, VCVTPS2QQ, VCVTTPD2QQ, VPMULLQ, VRANGEPS/PD, VREDUCEPS/PD, VFPCLASSPS/PD, VPMOVM2D/Q, VPMOVD2M/Q2M — additional 32/64-bit integer and FP operations",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VL { get; } =
        new("archAVX512VL", "AVX-512 Vector Length", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512VL (Skylake-X): Allows AVX-512 instructions to operate on 128-bit (XMM) and 256-bit (YMM) vectors with EVEX encoding, opmasks, and embedded broadcast — no new instructions, enables AVX-512 features at shorter vector lengths",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512IFMA { get; } =
        new("archAVX512IFMA", "AVX-512 IFMA", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Integer Fused Multiply-Add (Cannon Lake, Ice Lake): VPMADD52LUQ, VPMADD52HUQ — 52-bit integer multiply-add for big-number arithmetic and cryptography",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VBMI { get; } =
        new("archAVX512VBMI", "AVX-512 VBMI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Vector Byte Manipulation (Cannon Lake, Ice Lake): VPERMB, VPERMI2B, VPERMT2B, VPMULTISHIFTQB — byte-granularity permutations",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VPOPCNTDQ { get; } =
        new("archAVX512VPOPCNTDQ", "AVX-512 VPOPCNTDQ", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Vector Population Count (Knights Mill, Ice Lake): VPOPCNTD, VPOPCNTQ — vectorized popcount on 32/64-bit elements",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX5124VNNIW { get; } =
        new("archAVX5124VNNIW", "AVX-512 4VNNIW", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 4-register Vector Neural Network (Knights Mill): VP4DPWSSD, VP4DPWSSDS — 4-source dot product for deep learning inference",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX5124FMAPS { get; } =
        new("archAVX5124FMAPS", "AVX-512 4FMAPS", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 4-register Fused Multiply-Add (Knights Mill): V4FMADDPS, V4FMADDSS, V4FNMADDPS, V4FNMADDSS — 4-source FMA for deep learning",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VBMI2 { get; } =
        new("archAVX512VBMI2", "AVX-512 VBMI2", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Vector Byte Manipulation 2 (Ice Lake): VPCOMPRESSB/W, VPEXPANDB/W, VPSHLDW/D/Q, VPSHRDW/D/Q, VPSHLDVW/D/Q, VPSHRDVW/D/Q — compress/expand bytes/words, concatenate and shift",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VNNI { get; } =
        new("archAVX512VNNI", "AVX-512 VNNI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Vector Neural Network (Ice Lake): VPDPBUSD, VPDPBUSDS, VPDPWSSD, VPDPWSSDS — INT8/INT16 multiply-accumulate for inference",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512BITALG { get; } =
        new("archAVX512BITALG", "AVX-512 BITALG", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 Bit Algorithms (Ice Lake): VPOPCNTB/W, VPSHUFBITQMB — byte/word popcount and bit shuffle",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512GFNI { get; } =
        new("archAVX512GFNI", "AVX-512 GFNI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Galois Field New Instructions (Ice Lake): GF2P8MULB, GF2P8AFFINEQB, GF2P8AFFINEINVQB — GF(2^8) arithmetic for cryptography and erasure coding",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VAES { get; } =
        new("archAVX512VAES", "AVX-512 VAES", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Vectorized AES (Ice Lake): VAESENC, VAESENCLAST, VAESDEC, VAESDECLAST on 256/512-bit vectors — parallel AES encryption/decryption",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VPCLMULQDQ { get; } =
        new("archAVX512VPCLMULQDQ", "AVX-512 VPCLMULQDQ", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Vectorized Carry-Less Multiplication (Ice Lake): VPCLMULQDQ on 256/512-bit vectors — parallel CLMUL for CRC/GCM/polynomial math",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512BF16 { get; } =
        new("archAVX512BF16", "AVX-512 BF16", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "BFloat16 (Tiger Lake, Cooper Lake): VCVTNE2PS2BF16, VCVTNEPS2BF16, VDPBF16PS — bfloat16 conversion and dot product for AI workloads",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512VP2INTERSECT { get; } =
        new("archAVX512VP2INTERSECT", "AVX-512 VP2INTERSECT", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Vector Pair Intersection (Tiger Lake): VP2INTERSECTD, VP2INTERSECTQ — compute intersection bitmasks between two vectors for database join acceleration",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchENQCMD { get; } =
        new("archENQCMD", "ENQCMD", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Enqueue Command (Sapphire Rapids): ENQCMD, ENQCMDS — submit work to accelerators via shared work queues (DSA, IAA, QAT)",
        };

    // ── Other ISA extensions (Order 60+) ─────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchFMA { get; } =
        new("archFMA", "FMA", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Fused Multiply-Add (Haswell): VFMADD132/213/231PS/PD/SS/SD, VFMSUB, VFNMADD, VFNMSUB, VFMADDSUB, VFMSUBADD — multiply-add without intermediate rounding",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchADX { get; } =
        new("archADX", "ADX", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Multi-Precision Add-Carry (Broadwell): ADCX, ADOX — two independent carry chains for big-number arithmetic (RSA, ECC)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAES { get; } =
        new("archAES", "AES", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AES-NI (Westmere): AESENC, AESENCLAST, AESDEC, AESDECLAST, AESKEYGENASSIST, AESIMC — hardware AES encryption/decryption rounds",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSHA { get; } =
        new("archSHA", "SHA", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SHA Extensions (Goldmont, Zen): SHA1MSG1/2, SHA1NEXTE, SHA1RNDS4, SHA256MSG1/2, SHA256RNDS2 — hardware SHA-1 and SHA-256 rounds",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchBMI1 { get; } =
        new("archBMI1", "BMI1", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Bit Manipulation 1 (Haswell): ANDN, BEXTR, BLSI, BLSMSK, BLSR, TZCNT — bit extract, isolate lowest set bit, trailing zero count",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchBMI2 { get; } =
        new("archBMI2", "BMI2", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Bit Manipulation 2 (Haswell): BZHI, MULX, PDEP, PEXT, RORX, SARX, SHLX, SHRX — parallel bit deposit/extract, flagless shifts",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchF16C { get; } =
        new("archF16C", "F16C", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Half-Precision Float Conversion (Ivy Bridge): VCVTPH2PS, VCVTPS2PH — convert between FP16 and FP32",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPCLMULQDQ { get; } =
        new("archPCLMULQDQ", "PCLMULQDQ", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Carry-Less Multiplication (Westmere): PCLMULQDQ — 64-bit carry-less multiply for CRC32, GCM, and polynomial arithmetic",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchLZCNT { get; } =
        new("archLZCNT", "LZCNT", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Leading Zero Count (Haswell, ABM on AMD): LZCNT — count leading zeros (unlike BSR, defined for zero input)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchRDRAND { get; } =
        new("archRDRAND", "RDRAND", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Read Random Number (Ivy Bridge): RDRAND — hardware random number from Intel Digital Random Number Generator",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchRDSEED { get; } =
        new("archRDSEED", "RDSEED", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Read Random Seed (Broadwell): RDSEED — hardware random seed (higher entropy than RDRAND, for seeding PRNGs)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchRDPID { get; } =
        new("archRDPID", "RDPID", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Read Processor ID (Ice Lake): RDPID — read IA32_TSC_AUX into a GP register without serializing",
        };

    // ── Virtualization & Security (Order 80+) ────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchVMX { get; } =
        new("archVMX", "VMX", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Virtual Machine Extensions (Pentium 4 w/ VT-x): VMXON, VMXOFF, VMLAUNCH, VMRESUME, VMREAD, VMWRITE, VMPTRLD, VMPTRST, VMCLEAR, VMCALL, VMFUNC, INVEPT, INVVPID",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSMX { get; } =
        new("archSMX", "SMX", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Safer Mode Extensions (TXT-enabled processors): GETSEC — Intel TXT measured launch and secure memory",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSGX1 { get; } =
        new("archSGX1", "SGX1", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Software Guard Extensions 1 (Skylake): ENCLS (ECREATE, EADD, EINIT, EREMOVE, EDBGRD, EDBGWR, EEXTEND, ELDB, ELDU, EBLOCK, EPA, EWB, ETRACK), ENCLU (EENTER, EEXIT, ERESUME, EGETKEY, EREPORT, EACCEPT, EMODPE, EACCEPTCOPY)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSGX2 { get; } =
        new("archSGX2", "SGX2", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Software Guard Extensions 2: ENCLS (EAUG, EMODPR, EMODT), ENCLU (EDECCSSA) — dynamic memory management for enclaves",
        };

    // ── Transactional Memory (Order 85+) ─────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchHLE { get; } =
        new("archHLE", "HLE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Hardware Lock Elision (Haswell): XACQUIRE, XRELEASE prefixes — optimistic lock elision using transactional memory",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchRTM { get; } =
        new("archRTM", "RTM", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Restricted Transactional Memory (Haswell): XBEGIN, XEND, XABORT, XTEST — explicit transactional memory regions",
        };

    // ── Memory & Misc ISA (Order 90+) ────────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMPX { get; } =
        new("archMPX", "MPX", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Memory Protection Extensions (Skylake, deprecated): BNDMK, BNDCL, BNDCU, BNDCN, BNDMOV, BNDLDX, BNDSTX — hardware-assisted bounds checking",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchFSGSBASE { get; } =
        new("archFSGSBASE", "FSGSBASE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "FS/GS Base (Ivy Bridge): RDFSBASE, RDGSBASE, WRFSBASE, WRGSBASE — read/write FS and GS segment base from user mode",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchINVPCID { get; } =
        new("archINVPCID", "INVPCID", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Invalidate Process-Context Identifier (Haswell): INVPCID — fine-grained TLB invalidation by PCID",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchXSAVEOPT { get; } =
        new("archXSAVEOPT", "XSAVEOPT", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Optimized Save (Sandy Bridge): XSAVEOPT, XSAVEC, XSAVES, XRSTORS — optimized save/restore of processor extended state (AVX, SSE)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPREFETCHWT1 { get; } =
        new("archPREFETCHWT1", "PREFETCHWT1", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Prefetch with Intent to Write (Knights Landing): PREFETCHWT1 — prefetch into L2 cache with write intent",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPRFCHW { get; } =
        new("archPRFCHW", "PRFCHW", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "PREFETCHW (3DNow!, AMD, Broadwell): PREFETCHW — prefetch with write intent to reduce RFO latency",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCLDEMOTE { get; } =
        new("archCLDEMOTE", "CLDEMOTE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Cache Line Demote (Tremont): CLDEMOTE — hint to move cache line to a more shared cache level, useful before expected cross-core sharing",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCLWB { get; } =
        new("archCLWB", "CLWB", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Cache Line Write Back (Skylake server): CLWB — write back a modified cache line while (optionally) retaining it, for persistent-memory flushing",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMOVDIRI { get; } =
        new("archMOVDIRI", "MOVDIRI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Direct Store (Tiger Lake, Sapphire Rapids): MOVDIRI — 32/64-bit direct store bypassing cache (write-combining)",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMOVDIR64B { get; } =
        new("archMOVDIR64B", "MOVDIR64B", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Direct Store 64 Bytes (Tiger Lake, Sapphire Rapids): MOVDIR64B — 64-byte atomic direct store bypassing cache",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPCONFIG { get; } =
        new("archPCONFIG", "PCONFIG", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Platform Configuration (Ice Lake server): PCONFIG — configure platform features like TME (Total Memory Encryption) key management",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchWAITPKG { get; } =
        new("archWAITPKG", "WAITPKG", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "User Wait (Tremont, Alder Lake): UMWAIT, UMONITOR, TPAUSE — user-mode wait for memory write or timestamp, for low-latency polling",
        };

    // ── AMD & Legacy (Order 110+) ────────────────────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE4A { get; } =
        new("archSSE4A", "SSE4A (AMD)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE4A (AMD Barcelona/Phenom): EXTRQ, INSERTQ, MOVNTSS, MOVNTSD — AMD-specific extract/insert and non-temporal stores",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSSE5 { get; } =
        new("archSSE5", "SSE5 (AMD)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SSE5 (AMD, proposed then withdrawn): Original AMD proposal for 3-operand SIMD, superseded by XOP/FMA4 and eventually by AVX",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAMD { get; } =
        new("archAMD", "AMD", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AMD-specific instructions: CLGI, STGI, SKINIT, INVLPGA, VMLOAD, VMSAVE, VMRUN, VMMCALL (AMD-V), MONITORX, MWAITX",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchTBM { get; } =
        new("archTBM", "TBM (AMD)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Trailing Bit Manipulation (AMD Piledriver, dropped in Zen): BEXTR, BLCFILL, BLCI, BLCIC, BLCMSK, BLCS, BLSFILL, BLSIC, T1MSKC, TZMSK",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean Arch3DNOW { get; } =
        new("arch3DNOW", "3DNow!", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "3DNow! (AMD K6-2, deprecated): PFADD, PFSUB, PFMUL, PFCMPEQ/GT/GE, PFRCP, PFRSQRT, PI2FD, PF2ID, PAVGUSB, PMULHRW, FEMMS — 64-bit packed float SIMD, superseded by SSE",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchIA64 { get; } =
        new("archIA64", "IA-64 (Itanium)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "IA-64/Itanium compatibility: JMPE and other transition instructions — mostly irrelevant for modern x86 development",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCYRIX { get; } =
        new("archCYRIX", "Cyrix", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Cyrix-specific instructions (6x86, MediaGX): SMINT, RDSHR, WRSHR, SVDC, RSDC, SVLDT, RSLDT, SVTS, RSTS — legacy, no longer manufactured",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCYRIXM { get; } =
        new("archCYRIXM", "Cyrix M", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Cyrix M-series (MII, MediaGXm): PADDSIW, PSUBSIW, PMVZB, PMVNZB, PMVLZB, PMVGEZB, PFRCPV, PFRSQRTV — legacy Cyrix multimedia, no longer manufactured",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchUNDOC { get; } =
        new("archUNDOC", "Undocumented", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Undocumented instructions: SALC, ICEBP (INT1), LOADALL, undocumented opcodes — use at your own risk, behavior may vary between CPU revisions",
        };

    // ── rev-091 / 2026 SDM additions (Order 130+) ────────────────

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX10 { get; } =
        new("archAVX10", "AVX10", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX10 converged vector ISA (Granite Rapids / future). In the SDM nearly every EVEX instruction is now '(AVX512xx AND AVX512yy) OR AVX10.1'. Enabling AVX10 makes ALL of those instructions available regardless of the individual AVX-512 toggles, since AVX10.1 is a superset of AVX-512.",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVX512FP16 { get; } =
        new("archAVX512FP16", "AVX-512 FP16", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-512 FP16 half-precision floating-point (Sapphire Rapids): VADDPH, VMULPH, VFMADD*PH/SH, VCVTPH2*, VCVTSH2*, VFCMADDCPH, VFMULCPH, VGETMANTPH, VRNDSCALEPH — full IEEE FP16 arithmetic in vectors",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVXVNNI { get; } =
        new("archAVXVNNI", "AVX-VNNI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-VNNI (Alder Lake): VEX-encoded VPDPBUSD, VPDPBUSDS, VPDPWSSD, VPDPWSSDS — INT8/INT16 dot-product on 128/256-bit vectors without requiring AVX-512",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVXVNNIINT { get; } =
        new("archAVXVNNIINT", "AVX-VNNI-INT8/INT16", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-VNNI-INT8 / AVX-VNNI-INT16 (Sierra Forest, Arrow Lake): VPDPB[SU][SU]D[S], VPDPW[SU][SU]D[S] — signed/unsigned INT8 and INT16 dot-product variants",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVXNECONVERT { get; } =
        new("archAVXNECONVERT", "AVX-NE-CONVERT", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-NE-CONVERT (Sierra Forest, Arrow Lake): VBCSTNEBF162PS, VBCSTNESH2PS, VCVTNE[OE]BF162PS, VCVTNE[OE]PH2PS, VCVTNEPS2BF16 — BF16/FP16 to FP32 conversions and broadcasts",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAVXIFMA { get; } =
        new("archAVXIFMA", "AVX-IFMA", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "AVX-IFMA (Sierra Forest, Arrow Lake): VEX-encoded VPMADD52LUQ, VPMADD52HUQ — 52-bit integer multiply-add on 128/256-bit vectors without AVX-512",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchAMX { get; } =
        new("archAMX", "AMX (tiles)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Advanced Matrix Extensions (Sapphire Rapids): LDTILECFG, STTILECFG, TILELOADD, TILELOADDT1, TILESTORED, TILERELEASE, TILEZERO, TDPBSSD/SUD/USD/UUD, TDPBF16PS, TDPFP16PS, TCMMIMFP16PS, TCMMRLFP16PS — tile-register matrix multiply for AI",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCMPCCXADD { get; } =
        new("archCMPCCXADD", "CMPccXADD", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Compare-and-add if condition met (Sierra Forest, Granite Rapids): CMPBEXADD, CMPBXADD, CMPLEXADD, CMPLXADD, CMPNBEXADD, CMPNBXADD, CMPNLEXADD, CMPNLXADD, CMPNOXADD, CMPNPXADD, CMPNSXADD, CMPNZXADD, CMPOXADD, CMPPXADD, CMPSXADD, CMPZXADD — atomic compare-and-add primitives for lock-free data structures",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCETSS { get; } =
        new("archCETSS", "CET Shadow Stack", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Control-flow Enforcement Technology — shadow stack (Tiger Lake): CLRSSBSY, INCSSPD/Q, RDSSPD/Q, RSTORSSP, SAVEPREVSSP, SETSSBSY, WRSSD/Q, WRUSSD/Q — return-address protection",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchCETIBT { get; } =
        new("archCETIBT", "CET Indirect Branch Tracking", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Control-flow Enforcement Technology — indirect branch tracking (Tiger Lake): ENDBR32, ENDBR64 — forward-edge control-flow integrity",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchKEYLOCKER { get; } =
        new("archKEYLOCKER", "Key Locker", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Key Locker (Tiger Lake): ENCODEKEY128, ENCODEKEY256, LOADIWKEY, AESENC128KL, AESENC256KL, AESDEC128KL, AESDEC256KL, AESENCWIDE128KL, AESENCWIDE256KL, AESDECWIDE128KL, AESDECWIDE256KL — AES using wrapped keys that never expose the raw key",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchUINTR { get; } =
        new("archUINTR", "User Interrupts", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "User Interrupts (Sapphire Rapids): CLUI, SENDUIPI, STUI, TESTUI, UIRET — deliver interrupts directly to user space without kernel transition",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPBNDKB { get; } =
        new("archPBNDKB", "PBNDKB (TSE)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Total Storage Encryption key binding: PBNDKB — bind a platform key for Total Storage Encryption",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSMAP { get; } =
        new("archSMAP", "SMAP", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Supervisor-Mode Access Prevention (Broadwell): CLAC, STAC — clear/set the AC flag to guard against accidental supervisor access to user pages",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSERIALIZE { get; } =
        new("archSERIALIZE", "SERIALIZE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Serialize instruction execution (Sapphire Rapids, Alder Lake): SERIALIZE — architectural serialization without modifying registers/flags/memory",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchWBNOINVD { get; } =
        new("archWBNOINVD", "WBNOINVD", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Write Back and Do Not Invalidate Cache (Ice Lake server): WBNOINVD — write back modified cache lines without invalidating the cache",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchHRESET { get; } =
        new("archHRESET", "HRESET", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "History Reset (Alder Lake): HRESET — reset selected processor history (e.g. Thread Director feedback) used by hardware prediction",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMSRLIST { get; } =
        new("archMSRLIST", "MSRLIST", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Read/Write list of MSRs (Sierra Forest, Granite Rapids): RDMSRLIST, WRMSRLIST — read/write a list of model-specific registers in one instruction",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchWRMSRNS { get; } =
        new("archWRMSRNS", "WRMSRNS", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Non-Serializing Write to MSR (Sierra Forest, Granite Rapids): WRMSRNS — write an MSR without the serializing semantics of WRMSR",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPTWRITE { get; } =
        new("archPTWRITE", "PTWRITE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Write to Processor Trace (Kaby Lake, Goldmont Plus): PTWRITE — insert a software-defined value into the Intel Processor Trace packet stream",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchTSXLDTRK { get; } =
        new("archTSXLDTRK", "TSXLDTRK", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "TSX Suspend Load Address Tracking (Sapphire Rapids): XSUSLDTRK, XRESLDTRK — suspend/resume load address tracking within a transactional region",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPREFETCHI { get; } =
        new("archPREFETCHI", "PREFETCHI", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Instruction Prefetch (Granite Rapids): PREFETCHIT0, PREFETCHIT1 — prefetch code into the instruction cache hierarchy",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSHA512 { get; } =
        new("archSHA512", "SHA-512", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SHA-512 New Instructions (Arrow Lake, Lunar Lake): VSHA512MSG1, VSHA512MSG2, VSHA512RNDS2 — hardware SHA-512 message schedule and rounds",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSM3 { get; } =
        new("archSM3", "SM3", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SM3 Hash New Instructions (Arrow Lake, Lunar Lake): VSM3MSG1, VSM3MSG2, VSM3RNDS2 — hardware acceleration for the Chinese SM3 hash standard",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchSM4 { get; } =
        new("archSM4", "SM4", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "SM4 Cipher New Instructions (Arrow Lake, Lunar Lake): VSM4KEY4, VSM4RNDS4 — hardware acceleration for the Chinese SM4 block cipher",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchMOVBE { get; } =
        new("archMOVBE", "MOVBE", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Move After Byte Swap (Atom, Haswell): MOVBE — load/store with endianness swap between memory and a GP register",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ArchPKU { get; } =
        new("archPKU", "PKU (Protection Keys)", ArchCategory, defaultValue: false)
        {
            EnabledWhen = ArchProfileIsCustom,
            Description = "Protection Keys for User pages / OSPKE (Skylake server): RDPKRU, WRPKRU — read/write the user page-protection key rights register (PKRU)",
        };
}
