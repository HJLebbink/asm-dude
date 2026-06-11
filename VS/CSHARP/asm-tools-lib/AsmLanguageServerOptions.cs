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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmTools;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Server-side settings type: the shared data contract (<see cref="AsmSettingsData"/>) plus the
/// arch / micro-arch / assembler helper logic that depends on AsmTools enums. The VSIX produces an
/// <see cref="AsmSettingsData"/>; the server deserializes settings.json into this derived type, so
/// all fields round-trip while only the server carries the behavior.
/// </summary>
public class AsmLanguageServerOptions : AsmSettingsData
{
    public MicroArch Get_MicroArch_Switched_On()
    {
        MicroArch result = MicroArch.NONE;
        foreach (MicroArch microArch in Enum.GetValues(typeof(MicroArch)))
        {
            if (this.Is_MicroArch_Switched_On(microArch))
            {
                result |= microArch;
            }
        }
        return result;
    }

    public bool Is_MicroArch_Switched_On(MicroArch microArch)
    {
        switch (microArch)
        {
            case MicroArch.NONE: return false;

            case MicroArch.Conroe: return this.PerformanceInfo_Conroe_On;
            case MicroArch.Wolfdale: return this.PerformanceInfo_Wolfdale_On;
            case MicroArch.Nehalem: return this.PerformanceInfo_Nehalem_On;
            case MicroArch.Westmere: return this.PerformanceInfo_Westmere_On;
            case MicroArch.SandyBridge: return this.PerformanceInfo_SandyBridge_On;
            case MicroArch.IvyBridge: return this.PerformanceInfo_IvyBridge_On;
            case MicroArch.Haswell: return this.PerformanceInfo_Haswell_On;
            case MicroArch.Broadwell: return this.PerformanceInfo_Broadwell_On;
            case MicroArch.Skylake: return this.PerformanceInfo_Skylake_On;
            case MicroArch.SkylakeX: return this.PerformanceInfo_SkylakeX_On;
            case MicroArch.Kabylake: return this.PerformanceInfo_Kabylake_On;
            case MicroArch.CoffeeLake: return this.PerformanceInfo_CoffeeLake_On;
            case MicroArch.Cannonlake: return this.PerformanceInfo_Cannonlake_On;
            case MicroArch.CascadeLake: return this.PerformanceInfo_CascadeLake_On;
            case MicroArch.Icelake: return this.PerformanceInfo_Icelake_On;
            case MicroArch.Tigerlake: return this.PerformanceInfo_Tigerlake_On;
            case MicroArch.RocketLake: return this.PerformanceInfo_RocketLake_On;
            case MicroArch.EmeraldRapids: return this.PerformanceInfo_EmeraldRapids_On;

            case MicroArch.Bonnell: return this.PerformanceInfo_Bonnell_On;
            case MicroArch.Airmont: return this.PerformanceInfo_Airmont_On;
            case MicroArch.Goldmont: return this.PerformanceInfo_Goldmont_On;
            case MicroArch.GoldmontPlus: return this.PerformanceInfo_GoldmontPlus_On;
            case MicroArch.Tremont: return this.PerformanceInfo_Tremont_On;

            case MicroArch.Zen2: return this.PerformanceInfo_Zen2_On;
            case MicroArch.Zen3: return this.PerformanceInfo_Zen3_On;
            case MicroArch.Zen4: return this.PerformanceInfo_Zen4_On;
            case MicroArch.Zen5: return this.PerformanceInfo_Zen5_On;

            default:
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:AsmDudeToolsStatic::Is_MicroArch_Switched_On: unsupported arch {0}", microArch));
                return false;
        }
    }

    public HashSet<Arch> Get_Arch_Switched_On()
    {
        HashSet<Arch> set = [];
        foreach (Arch arch in Enum.GetValues(typeof(Arch)))
        {
            if (this.Is_Arch_Switched_On(arch))
            {
                set.Add(arch);
            }
        }
        return set;
    }

    public bool Is_Arch_Switched_On(Arch arch)
    {
        // A non-Custom instruction-set profile overrides the individual ARCH_* toggles: an arch is on iff
        // it belongs to the profile's set (ARCH_NONE — always-available instructions — is always on).
        if (ArchTools.TryGetProfileArchs(this.ArchProfile, out HashSet<Arch> profileArchs))
        {
            return (arch == Arch.ARCH_NONE) || profileArchs.Contains(arch);
        }

        // Custom (or unknown) profile: honor the per-arch toggles (the historical behavior).
        return arch switch
        {
            Arch.ARCH_NONE => true,
            Arch.ARCH_8086 => this.ARCH_8086,
            Arch.ARCH_186 => this.ARCH_186,
            Arch.ARCH_286 => this.ARCH_286,
            Arch.ARCH_386 => this.ARCH_386,
            Arch.ARCH_486 => this.ARCH_486,
            Arch.ARCH_PENT => this.ARCH_PENT,
            Arch.ARCH_P6 => this.ARCH_P6,
            Arch.ARCH_MMX => this.ARCH_MMX,
            Arch.ARCH_SSE => this.ARCH_SSE,
            Arch.ARCH_SSE2 => this.ARCH_SSE2,
            Arch.ARCH_SSE3 => this.ARCH_SSE3,
            Arch.ARCH_SSSE3 => this.ARCH_SSSE3,
            Arch.ARCH_SSE4_1 => this.ARCH_SSE4_1,
            Arch.ARCH_SSE4_2 => this.ARCH_SSE4_2,
            Arch.ARCH_SSE4A => this.ARCH_SSE4A,
            Arch.ARCH_SSE5 => this.ARCH_SSE5,
            Arch.ARCH_AVX => this.ARCH_AVX,
            Arch.ARCH_AVX2 => this.ARCH_AVX2,
            Arch.ARCH_AVX512_F => this.ARCH_AVX512_F,
            Arch.ARCH_AVX512_CD => this.ARCH_AVX512_CD,
            Arch.ARCH_AVX512_ER => this.ARCH_AVX512_ER,
            Arch.ARCH_AVX512_PF => this.ARCH_AVX512_PF,
            Arch.ARCH_AVX512_BW => this.ARCH_AVX512_BW,
            Arch.ARCH_AVX512_DQ => this.ARCH_AVX512_DQ,
            Arch.ARCH_AVX512_VL => this.ARCH_AVX512_VL,
            Arch.ARCH_AVX512_IFMA => this.ARCH_AVX512_IFMA,
            Arch.ARCH_AVX512_VBMI => this.ARCH_AVX512_VBMI,
            Arch.ARCH_AVX512_VPOPCNTDQ => this.ARCH_AVX512_VPOPCNTDQ,
            Arch.ARCH_AVX512_4VNNIW => this.ARCH_AVX512_4VNNIW,
            Arch.ARCH_AVX512_4FMAPS => this.ARCH_AVX512_4FMAPS,
            Arch.ARCH_AVX512_VBMI2 => this.ARCH_AVX512_VBMI2,
            Arch.ARCH_AVX512_VNNI => this.ARCH_AVX512_VNNI,
            Arch.ARCH_AVX512_BITALG => this.ARCH_AVX512_BITALG,
            Arch.ARCH_AVX512_GFNI => this.ARCH_AVX512_GFNI,
            Arch.ARCH_AVX512_VAES => this.ARCH_AVX512_VAES,
            Arch.ARCH_AVX512_VPCLMULQDQ => this.ARCH_AVX512_VPCLMULQDQ,
            Arch.ARCH_AVX512_BF16 => this.ARCH_AVX512_BF16,
            Arch.ARCH_AVX512_VP2INTERSECT => this.ARCH_AVX512_VP2INTERSECT,
            Arch.ARCH_ADX => this.ARCH_ADX,
            Arch.ARCH_AES => this.ARCH_AES,
            Arch.ARCH_VMX => this.ARCH_VMX,
            Arch.ARCH_BMI1 => this.ARCH_BMI1,
            Arch.ARCH_BMI2 => this.ARCH_BMI2,
            Arch.ARCH_F16C => this.ARCH_F16C,
            Arch.ARCH_FMA => this.ARCH_FMA,
            Arch.ARCH_FSGSBASE => this.ARCH_FSGSBASE,
            Arch.ARCH_HLE => this.ARCH_HLE,
            Arch.ARCH_INVPCID => this.ARCH_INVPCID,
            Arch.ARCH_SHA => this.ARCH_SHA,
            Arch.ARCH_RTM => this.ARCH_RTM,
            Arch.ARCH_MPX => this.ARCH_MPX,
            Arch.ARCH_PCLMULQDQ => this.ARCH_PCLMULQDQ,
            Arch.ARCH_LZCNT => this.ARCH_LZCNT,
            Arch.ARCH_PREFETCHWT1 => this.ARCH_PREFETCHWT1,
            Arch.ARCH_PRFCHW => this.ARCH_PRFCHW,
            Arch.ARCH_RDPID => this.ARCH_RDPID,
            Arch.ARCH_RDRAND => this.ARCH_RDRAND,
            Arch.ARCH_RDSEED => this.ARCH_RDSEED,
            Arch.ARCH_XSAVEOPT => this.ARCH_XSAVEOPT,
            Arch.ARCH_SGX1 => this.ARCH_SGX1,
            Arch.ARCH_SGX2 => this.ARCH_SGX2,
            Arch.ARCH_SMX => this.ARCH_SMX,
            Arch.ARCH_CLDEMOTE => this.ARCH_CLDEMOTE,
            Arch.ARCH_CLWB => this.ARCH_CLWB,
            Arch.ARCH_MOVDIR64B => this.ARCH_MOVDIR64B,
            Arch.ARCH_MOVDIRI => this.ARCH_MOVDIRI,
            Arch.ARCH_PCONFIG => this.ARCH_PCONFIG,
            Arch.ARCH_WAITPKG => this.ARCH_WAITPKG,
            Arch.ARCH_ENQCMD => this.ARCH_ENQCMD,
            Arch.ARCH_X64 => this.ARCH_X64,
            Arch.ARCH_IA64 => this.ARCH_IA64,
            Arch.ARCH_UNDOC => this.ARCH_UNDOC,
            Arch.ARCH_AMD => this.ARCH_AMD,
            Arch.ARCH_TBM => this.ARCH_TBM,
            Arch.ARCH_3DNOW => this.ARCH_3DNOW,
            Arch.ARCH_CYRIX => this.ARCH_CYRIX,
            Arch.ARCH_CYRIXM => this.ARCH_CYRIXM,

            // rev-091 / 2026 SDM additions (user-toggleable via ArchitectureSettings, default off,
            // consistent with the AVX-512/advanced convention). NOTE on AVX10: in the SDM virtually
            // every EVEX instruction now lists "... OR AVX10.1", so turning AVX10 ON makes ALL those
            // instructions visible regardless of their AVX512_* toggles (OR semantics in
            // CalcMnemonicsSwitchedOn) — that's the correct meaning of AVX10 as a converged superset.
            Arch.ARCH_AVX10 => this.ARCH_AVX10,
            Arch.ARCH_AVX512_FP16 => this.ARCH_AVX512_FP16,
            Arch.ARCH_AVX_VNNI => this.ARCH_AVX_VNNI,
            Arch.ARCH_AVX_VNNI_INT => this.ARCH_AVX_VNNI_INT,
            Arch.ARCH_AVX_NE_CONVERT => this.ARCH_AVX_NE_CONVERT,
            Arch.ARCH_AVX_IFMA => this.ARCH_AVX_IFMA,
            Arch.ARCH_AMX => this.ARCH_AMX,
            Arch.ARCH_CMPCCXADD => this.ARCH_CMPCCXADD,
            Arch.ARCH_CET_SS => this.ARCH_CET_SS,
            Arch.ARCH_CET_IBT => this.ARCH_CET_IBT,
            Arch.ARCH_KEYLOCKER => this.ARCH_KEYLOCKER,
            Arch.ARCH_UINTR => this.ARCH_UINTR,
            Arch.ARCH_PBNDKB => this.ARCH_PBNDKB,
            Arch.ARCH_SMAP => this.ARCH_SMAP,
            Arch.ARCH_SERIALIZE => this.ARCH_SERIALIZE,
            Arch.ARCH_WBNOINVD => this.ARCH_WBNOINVD,
            Arch.ARCH_HRESET => this.ARCH_HRESET,
            Arch.ARCH_MSRLIST => this.ARCH_MSRLIST,
            Arch.ARCH_WRMSRNS => this.ARCH_WRMSRNS,
            Arch.ARCH_PTWRITE => this.ARCH_PTWRITE,
            Arch.ARCH_TSXLDTRK => this.ARCH_TSXLDTRK,
            Arch.ARCH_PREFETCHI => this.ARCH_PREFETCHI,
            Arch.ARCH_SHA512 => this.ARCH_SHA512,
            Arch.ARCH_SM3 => this.ARCH_SM3,
            Arch.ARCH_SM4 => this.ARCH_SM4,
            Arch.ARCH_MOVBE => this.ARCH_MOVBE,
            Arch.ARCH_PKU => this.ARCH_PKU,
            _ => false,// TODO return error;
        };
    }

    public int MaxFileLines
    {
        get
        {
            return this.Global_MaxFileLines;
        }

        set
        {
            this.Global_MaxFileLines = value;
        }
    }

    public AssemblerEnum Used_Assembler
    {
        get
        {
            if (this.useAssemblerAutoDetect)
            {
                return AssemblerEnum.AUTO_DETECT;
            }
            if (this.useAssemblerMasm)
            {
                return AssemblerEnum.MASM;
            }
            if (this.useAssemblerNasm)
            {
                return AssemblerEnum.NASM_INTEL;
            }
            if (this.useAssemblerNasm_Att)
            {
                return AssemblerEnum.NASM_ATT;
            }
            // LogWarning("AsmDudeToolsStatic.Used_Assembler:get: no assembler specified, assuming AUTO_DETECT");
            return AssemblerEnum.AUTO_DETECT;
        }

        set
        {
            this.useAssemblerAutoDetect = false;
            this.useAssemblerMasm = false;
            this.useAssemblerNasm = false;
            this.useAssemblerNasm_Att = false;

            if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
            {
                this.useAssemblerAutoDetect = true;
            }
            else if (value.HasFlag(AssemblerEnum.MASM))
            {
                this.useAssemblerMasm = true;
            }
            else if (value.HasFlag(AssemblerEnum.NASM_INTEL))
            {
                this.useAssemblerNasm = true;
            }
            else if (value.HasFlag(AssemblerEnum.NASM_ATT))
            {
                this.useAssemblerNasm_Att = true;
            }
            else
            {
                // Output_WARNING(string.Format(CultureUI, "{0}:Used_Assembler:set: no assembler specified; value={1}, assuming AUTO_DETECT", "AsmDudeToolsStatic", value));
                this.useAssemblerAutoDetect = true;
            }
        }
    }

    public AssemblerEnum Used_Assembler_Disassembly_Window
    {
        get
        {
            if (this.useAssemblerDisassemblyAutoDetect)
            {
                return AssemblerEnum.AUTO_DETECT;
            }
            if (this.useAssemblerDisassemblyMasm)
            {
                return AssemblerEnum.MASM;
            }
            if (this.useAssemblerDisassemblyNasm_Att)
            {
                return AssemblerEnum.NASM_ATT;
            }
            // Output_WARNING("AsmDudeToolsStatic.Used_Assembler_Disassembly_Window:get no assembler specified, assuming AUTO_DETECT");
            return AssemblerEnum.AUTO_DETECT;
        }

        set
        {
            this.useAssemblerDisassemblyAutoDetect = false;
            this.useAssemblerDisassemblyMasm = false;
            this.useAssemblerDisassemblyNasm_Att = false;

            if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
            {
                this.useAssemblerDisassemblyAutoDetect = true;
            }
            else if (value.HasFlag(AssemblerEnum.MASM))
            {
                this.useAssemblerDisassemblyMasm = true;
            }
            else if (value.HasFlag(AssemblerEnum.NASM_ATT))
            {
                this.useAssemblerDisassemblyNasm_Att = true;
            }
            else
            {
                // Output_WARNING(string.Format(CultureUI, "{0}:Used_Assembler_Disassembly_Window:set: no assembler specified; value={1}, assuming AUTO_DETECT", "AsmDudeToolsStatic", value));
                this.useAssemblerDisassemblyAutoDetect = true;
            }
        }
    }
}
