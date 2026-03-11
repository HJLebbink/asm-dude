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

namespace AsmTools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.Serialization;

    [DataContract]
    public class AsmLanguageServerOptions
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
        [DataMember]
        public bool PerformanceInfo_KnightsLanding_On;
        [DataMember]
        public bool KeywordHighlighting_BorderColor_On;
        [DataMember]
        public bool AsmSim_On;
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
#pragma warning restore SA1401 // Fields should be private

        public AsmLanguageServerOptions() {
            this.CodeFolding_BeginTag = string.Empty;
            this.CodeFolding_EndTag = string.Empty;
            this.AsmDoc_Url = string.Empty;
            this.AsmSim_Pragma_Assume = string.Empty;
            this.AsmSim_Show_Register_In_Code_Completion_Numeration = string.Empty;
            this.AsmSim_Show_Register_In_Register_Tooltip_Numeration = string.Empty;
            this.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = string.Empty;
        }

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
                case MicroArch.SandyBridge: return this.PerformanceInfo_SandyBridge_On;
                case MicroArch.IvyBridge: return this.PerformanceInfo_IvyBridge_On;
                case MicroArch.Haswell: return this.PerformanceInfo_Haswell_On;
                case MicroArch.Broadwell: return this.PerformanceInfo_Broadwell_On;
                case MicroArch.Skylake: return this.PerformanceInfo_Skylake_On;
                case MicroArch.SkylakeX: return this.PerformanceInfo_SkylakeX_On;
                case MicroArch.Kabylake: return false;
                case MicroArch.Cannonlake: return false;
                case MicroArch.Icelake: return false;
                case MicroArch.Tigerlake: return false;
                case MicroArch.KnightsCorner: return false;
                case MicroArch.KnightsLanding: return this.PerformanceInfo_KnightsLanding_On;

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
}