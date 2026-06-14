// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmTools;

using System.Runtime.Serialization;

/// <summary>
/// Pure-data settings contract shared between the VSIX (producer) and the LSP server (consumer).
/// Contains ONLY serializable fields — no behavior and no dependency on AsmTools enums — so the
/// VSIX can reference it without pulling in asm-tools-lib (and its Roslyn dependency).
///
/// <see cref="AsmLanguageServerOptions"/> in asm-tools-lib derives from this and adds the
/// arch/micro-arch/assembler helper logic. Because both sides bind to the same field names, the
/// settings.json contract is compile-checked: a renamed/removed field is a build error, not a
/// silent default. Serialized with <see cref="ColorJsonConverter"/> (also in this lib).
/// </summary>
[DataContract]
public class AsmSettingsData
{
#pragma warning disable SA1401 // Fields should be private
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Opcode;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Register;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Remark;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Directive;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Jump;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Label;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Constant;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Misc;
    [DataMember]
    public bool SyntaxHighlighting_On;
    [DataMember]
    public bool CodeFolding_On;
    [DataMember]
    public string CodeFolding_BeginTag;
    [DataMember]
    public string CodeFolding_EndTag;
    [DataMember]
    public bool CodeCompletion_On;
    [DataMember]
    public string AsmDoc_Url;
    [DataMember]
    public bool AsmDoc_On;
    [DataMember]
    public bool KeywordHighlighting_BackgroundColor_On;
    [DataMember]
    public System.Drawing.Color KeywordHighlighting_BackgroundColor;
    [DataMember]
    public bool useAssemblerMasm;
    [DataMember]
    public bool useAssemblerNasm;
    [DataMember]
    public bool IntelliSense_Show_Undefined_Labels;
    [DataMember]
    public bool IntelliSense_Show_Clashing_Labels;
    [DataMember]
    public bool IntelliSense_Decorate_Undefined_Labels;
    [DataMember]
    public bool IntelliSense_Decorate_Clashing_Labels;
    [DataMember]
    public bool ARCH_8086;
    [DataMember]
    public bool ARCH_186;
    [DataMember]
    public bool ARCH_286;
    [DataMember]
    public bool ARCH_386;
    [DataMember]
    public bool ARCH_486;
    [DataMember]
    public bool ARCH_MMX;
    [DataMember]
    public bool ARCH_SSE;
    [DataMember]
    public bool ARCH_SSE2;
    [DataMember]
    public bool ARCH_SSE3;
    [DataMember]
    public bool ARCH_SSSE3;
    [DataMember]
    public bool ARCH_SSE4_1;
    [DataMember]
    public bool ARCH_SSE4_2;
    [DataMember]
    public bool ARCH_SSE4A;
    [DataMember]
    public bool ARCH_SSE5;
    [DataMember]
    public bool ARCH_AVX;
    [DataMember]
    public bool ARCH_AVX2;
    [DataMember]
    public bool ARCH_AVX512_VL;
    [DataMember]
    public bool ARCH_AVX512_PF;
    [DataMember]
    public bool ARCH_AVX512_DQ;
    [DataMember]
    public bool ARCH_AVX512_BW;
    [DataMember]
    public bool ARCH_AVX512_ER;
    [DataMember]
    public bool ARCH_AVX512_F;
    [DataMember]
    public bool ARCH_AVX512_CD;
    [DataMember]
    public bool ARCH_X64;
    [DataMember]
    public bool ARCH_BMI1;
    [DataMember]
    public bool ARCH_BMI2;
    [DataMember]
    public bool ARCH_P6;
    [DataMember]
    public bool ARCH_IA64;
    [DataMember]
    public bool ARCH_FMA;
    [DataMember]
    public bool ARCH_TBM;
    [DataMember]
    public bool ARCH_AMD;
    [DataMember]
    public bool ARCH_PENT;
    [DataMember]
    public bool ARCH_3DNOW;
    [DataMember]
    public bool ARCH_CYRIX;
    [DataMember]
    public bool ARCH_CYRIXM;
    [DataMember]
    public bool ARCH_VMX;
    [DataMember]
    public bool ARCH_RTM;
    [DataMember]
    public bool ARCH_MPX;
    [DataMember]
    public bool ARCH_SHA;
    [DataMember]
    public bool ARCH_BND;
    [DataMember]
    public bool SignatureHelp_On;
    [DataMember]
    public bool ARCH_ADX;
    [DataMember]
    public bool ARCH_F16C;
    [DataMember]
    public bool ARCH_FSGSBASE;
    [DataMember]
    public bool ARCH_HLE;
    [DataMember]
    public bool ARCH_INVPCID;
    [DataMember]
    public bool ARCH_PCLMULQDQ;
    [DataMember]
    public bool ARCH_LZCNT;
    [DataMember]
    public bool ARCH_PREFETCHWT1;
    [DataMember]
    public bool ARCH_RDPID;
    [DataMember]
    public bool ARCH_RDRAND;
    [DataMember]
    public bool ARCH_RDSEED;
    [DataMember]
    public bool ARCH_XSAVEOPT;
    [DataMember]
    public bool ARCH_UNDOC;
    [DataMember]
    public bool ARCH_AES;
    [DataMember]
    public bool IntelliSense_Show_Undefined_Includes;
    [DataMember]
    public System.Drawing.Color KeywordHighlighting_BorderColor;
    [DataMember]
    public bool PerformanceInfo_SandyBridge_On;
    [DataMember]
    public bool PerformanceInfo_IvyBridge_On;
    [DataMember]
    public bool PerformanceInfo_Haswell_On;
    [DataMember]
    public bool PerformanceInfo_Broadwell_On;
    [DataMember]
    public bool PerformanceInfo_Skylake_On;
    // Additional microarchitectures (uops.info data set). SkylakeX/IvyBridge/Haswell/Broadwell/Skylake/SandyBridge declared elsewhere in this file.
    [DataMember]
    public bool PerformanceInfo_Conroe_On;
    [DataMember]
    public bool PerformanceInfo_Wolfdale_On;
    [DataMember]
    public bool PerformanceInfo_Nehalem_On;
    [DataMember]
    public bool PerformanceInfo_Westmere_On;
    [DataMember]
    public bool PerformanceInfo_Kabylake_On;
    [DataMember]
    public bool PerformanceInfo_CoffeeLake_On;
    [DataMember]
    public bool PerformanceInfo_Cannonlake_On;
    [DataMember]
    public bool PerformanceInfo_CascadeLake_On;
    [DataMember]
    public bool PerformanceInfo_Icelake_On;
    [DataMember]
    public bool PerformanceInfo_Tigerlake_On;
    [DataMember]
    public bool PerformanceInfo_RocketLake_On;
    [DataMember]
    public bool PerformanceInfo_EmeraldRapids_On;
    [DataMember]
    public bool PerformanceInfo_Bonnell_On;
    [DataMember]
    public bool PerformanceInfo_Airmont_On;
    [DataMember]
    public bool PerformanceInfo_Goldmont_On;
    [DataMember]
    public bool PerformanceInfo_GoldmontPlus_On;
    [DataMember]
    public bool PerformanceInfo_Tremont_On;
    [DataMember]
    public bool PerformanceInfo_Zen2_On;
    [DataMember]
    public bool PerformanceInfo_Zen3_On;
    [DataMember]
    public bool PerformanceInfo_Zen4_On;
    [DataMember]
    public bool PerformanceInfo_Zen5_On;
    [DataMember]
    public bool KeywordHighlighting_BorderColor_On;
    [DataMember]
    public bool AsmSim_On;
    /// <summary>Incremental simulation: on an edit, reuse the previous result for unaffected lines and
    /// re-solve only the dataflow cone (INCREMENTAL_SIM_PLAN.md). Runtime-settable (no restart); falls back
    /// to a full simulation whenever it can't safely reuse. <b>Nullable on purpose:</b> an OLDER settings.json
    /// (written before this field existed) has it ABSENT, which must mean "use the default (ON)", not "off" —
    /// a plain <c>bool</c> would deserialize absent → false and silently disable the feature. Resolve via
    /// <see cref="AsmSimIncrementalEffective"/>, never read this raw.</summary>
    [DataMember]
    public bool? AsmSim_Incremental;

    /// <summary>The effective incremental-simulation switch: the explicit setting if present, else the
    /// default (ON). Single source of truth for the absent-means-default-ON rule.</summary>
    public bool AsmSimIncrementalEffective => this.AsmSim_Incremental ?? true;
    [DataMember]
    public bool AsmSim_Show_Syntax_Errors;
    [DataMember]
    public bool AsmSim_Decorate_Syntax_Errors;
    [DataMember]
    public bool AsmSim_Show_Usage_Of_Undefined;
    [DataMember]
    public bool AsmSim_Decorate_Usage_Of_Undefined;
    [DataMember]
    public bool AsmSim_Decorate_Registers;
    [DataMember]
    public bool AsmSim_Show_Register_In_Code_Completion;
    [DataMember]
    public bool AsmSim_Decorate_Unimplemented;
    [DataMember]
    public bool IntelliSense_Decorate_Undefined_Includes;
    [DataMember]
    public bool ARCH_AVX512_IFMA;
    [DataMember]
    public bool ARCH_AVX512_VBMI;
    [DataMember]
    public bool ARCH_AVX512_VPOPCNTDQ;
    [DataMember]
    public bool ARCH_AVX512_4VNNIW;
    [DataMember]
    public bool ARCH_AVX512_4FMAPS;
    [DataMember]
    public bool AsmSim_64_Bits;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Userdefined1;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Userdefined2;
    [DataMember]
    public System.Drawing.Color SyntaxHighlighting_Userdefined3;
    [DataMember]
    public int AsmSim_Z3_Timeout_MS;
    [DataMember]
    public bool AsmSim_Show_Redundant_Instructions;
    [DataMember]
    public bool AsmSim_Decorate_Redundant_Instructions;
    [DataMember]
    public bool SyntaxHighlighting_Opcode_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Register_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Remark_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Directive_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Constant_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Jump_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Label_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Misc_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Userdefined1_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Userdefined2_Italic;
    [DataMember]
    public bool SyntaxHighlighting_Userdefined3_Italic;
    [DataMember]
    public bool IntelliSense_Label_Analysis_On;
    [DataMember]
    public bool useAssemblerNasm_Att;
    [DataMember]
    public int AsmSim_Number_Of_Threads;
    [DataMember]
    public bool AsmSim_Show_Unreachable_Instructions;
    [DataMember]
    public bool AsmSim_Decorate_Unreachable_Instructions;
    [DataMember]
    public string AsmSim_Pragma_Assume;
    [DataMember]
    public bool AsmSim_Show_Register_In_Instruction_Tooltip;
    [DataMember]
    public bool AsmSim_Show_Register_In_Register_Tooltip;
    [DataMember]
    public string AsmSim_Show_Register_In_Code_Completion_Numeration;
    [DataMember]
    public string AsmSim_Show_Register_In_Instruction_Tooltip_Numeration;
    [DataMember]
    public string AsmSim_Show_Register_In_Register_Tooltip_Numeration;
    [DataMember]
    public bool ARCH_AVX512_VBMI2;
    [DataMember]
    public bool ARCH_AVX512_VNNI;
    [DataMember]
    public bool ARCH_AVX512_BITALG;
    [DataMember]
    public bool ARCH_AVX512_GFNI;
    [DataMember]
    public bool ARCH_AVX512_VAES;
    [DataMember]
    public bool ARCH_AVX512_VPCLMULQDQ;
    [DataMember]
    public bool ARCH_SMX;
    [DataMember]
    public bool ARCH_SGX1;
    [DataMember]
    public bool ARCH_SGX2;
    [DataMember]
    public bool PerformanceInfo_SkylakeX_On;
    [DataMember]
    public bool PerformanceInfo_On;
    [DataMember]
    public bool ARCH_CLDEMOTE;
    [DataMember]
    public bool ARCH_CLWB;

    /// <summary>
    /// Instruction-set profile (one of <see cref="ArchProfileKeys"/>). When this is anything other than
    /// <see cref="ArchProfileKeys.Custom"/> it OVERRIDES every individual <c>ARCH_*</c> toggle above; only
    /// <see cref="ArchProfileKeys.Custom"/> honors them. Default is <see cref="ArchProfileKeys.V4"/>
    /// (modern x86-64 through AVX-512). Interpreted server-side by <c>ArchTools.TryGetProfileArchs</c>.
    /// </summary>
    [DataMember]
    public string ArchProfile = ArchProfileKeys.V4;

    [DataMember]
    public bool ARCH_MOVDIR64B;
    [DataMember]
    public bool ARCH_MOVDIRI;
    [DataMember]
    public bool ARCH_PCONFIG;
    [DataMember]
    public bool ARCH_WAITPKG;
    [DataMember]
    public bool ARCH_PRFCHW;
    [DataMember]
    public bool ARCH_AVX512_BF16;
    [DataMember]
    public bool ARCH_AVX512_VP2INTERSECT;
    [DataMember]
    public bool ARCH_ENQCMD;

    // rev-091 / 2026 SDM additions
    [DataMember]
    public bool ARCH_AVX512_FP16;
    [DataMember]
    public bool ARCH_AVX10;
    [DataMember]
    public bool ARCH_AVX_VNNI;
    [DataMember]
    public bool ARCH_AVX_VNNI_INT;
    [DataMember]
    public bool ARCH_AVX_NE_CONVERT;
    [DataMember]
    public bool ARCH_AVX_IFMA;
    [DataMember]
    public bool ARCH_AMX;
    [DataMember]
    public bool ARCH_CMPCCXADD;
    [DataMember]
    public bool ARCH_CET_SS;
    [DataMember]
    public bool ARCH_CET_IBT;
    [DataMember]
    public bool ARCH_KEYLOCKER;
    [DataMember]
    public bool ARCH_UINTR;
    [DataMember]
    public bool ARCH_PBNDKB;
    [DataMember]
    public bool ARCH_SMAP;
    [DataMember]
    public bool ARCH_SERIALIZE;
    [DataMember]
    public bool ARCH_WBNOINVD;
    [DataMember]
    public bool ARCH_HRESET;
    [DataMember]
    public bool ARCH_MSRLIST;
    [DataMember]
    public bool ARCH_WRMSRNS;
    [DataMember]
    public bool ARCH_PTWRITE;
    [DataMember]
    public bool ARCH_TSXLDTRK;
    [DataMember]
    public bool ARCH_PREFETCHI;
    [DataMember]
    public bool ARCH_SHA512;
    [DataMember]
    public bool ARCH_SM3;
    [DataMember]
    public bool ARCH_SM4;
    [DataMember]
    public bool ARCH_MOVBE;
    [DataMember]
    public bool ARCH_PKU;

    [DataMember]
    public bool useAssemblerDisassemblyMasm;
    [DataMember]
    public bool useAssemblerDisassemblyNasm_Att;
    [DataMember]
    public bool useAssemblerDisassemblyAutoDetect;
    [DataMember]
    public bool useAssemblerAutoDetect;
    [DataMember]
    public int Global_MaxFileLines;

    /// <summary>
    /// Diagnostic log verbosity: trace|debug|info|warn|error|off (blank = build default). Honored by
    /// both the VSIX and the LSP server via <see cref="AsmLog.TryParseLevel"/>. The ASMDUDE_LOGLEVEL
    /// env var overrides this.
    /// </summary>
    [DataMember]
    public string LogLevel = string.Empty;
#pragma warning restore SA1401 // Fields should be private

    public AsmSettingsData()
    {
        this.CodeFolding_BeginTag = string.Empty;
        this.CodeFolding_EndTag = string.Empty;
        this.AsmDoc_Url = string.Empty;
        this.AsmSim_Pragma_Assume = string.Empty;
        this.AsmSim_Show_Register_In_Code_Completion_Numeration = string.Empty;
        this.AsmSim_Show_Register_In_Register_Tooltip_Numeration = string.Empty;
        this.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = string.Empty;

        // Redundant-instruction detection defaults ON (an absent field in an older settings.json keeps this
        // default; an explicit value in the JSON still wins). The other AsmSim flags default to bool-false
        // here and get their real defaults from the VSIX setting definitions / SettingsSyncService.
        this.AsmSim_Show_Redundant_Instructions = true;
        this.AsmSim_Decorate_Redundant_Instructions = true;
    }
}
