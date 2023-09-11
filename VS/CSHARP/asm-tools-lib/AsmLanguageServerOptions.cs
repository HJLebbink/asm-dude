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
        public bool CodeFolding_IsDefaultCollapsed;
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
        public bool PerformanceInfo_IsDefaultCollapsed;
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
            CodeFolding_BeginTag = string.Empty;
            CodeFolding_EndTag = string.Empty;
            AsmDoc_Url = string.Empty;
            AsmSim_Pragma_Assume = string.Empty;
            AsmSim_Show_Register_In_Code_Completion_Numeration = string.Empty;
            AsmSim_Show_Register_In_Register_Tooltip_Numeration = string.Empty;
            AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = string.Empty;
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

        public ISet<Arch> Get_Arch_Switched_On()
        {
            ISet<Arch> set = new HashSet<Arch>();
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
            switch (arch)
            {
                case Arch.ARCH_NONE:
                    return true;
                case Arch.ARCH_8086:
                    return this.ARCH_8086;
                case Arch.ARCH_186:
                    return this.ARCH_186;
                case Arch.ARCH_286:
                    return this.ARCH_286;
                case Arch.ARCH_386:
                    return this.ARCH_386;
                case Arch.ARCH_486:
                    return this.ARCH_486;
                case Arch.ARCH_PENT:
                    return this.ARCH_PENT;
                case Arch.ARCH_P6:
                    return this.ARCH_P6;
                case Arch.ARCH_MMX:
                    return this.ARCH_MMX;
                case Arch.ARCH_SSE:
                    return this.ARCH_SSE;
                case Arch.ARCH_SSE2:
                    return this.ARCH_SSE2;
                case Arch.ARCH_SSE3:
                    return this.ARCH_SSE3;
                case Arch.ARCH_SSSE3:
                    return this.ARCH_SSSE3;
                case Arch.ARCH_SSE4_1:
                    return this.ARCH_SSE4_1;
                case Arch.ARCH_SSE4_2:
                    return this.ARCH_SSE4_2;
                case Arch.ARCH_SSE4A:
                    return this.ARCH_SSE4A;
                case Arch.ARCH_SSE5:
                    return this.ARCH_SSE5;
                case Arch.ARCH_AVX:
                    return this.ARCH_AVX;
                case Arch.ARCH_AVX2:
                    return this.ARCH_AVX2;
                case Arch.ARCH_AVX512_F:
                    return this.ARCH_AVX512_F;
                case Arch.ARCH_AVX512_CD:
                    return this.ARCH_AVX512_CD;
                case Arch.ARCH_AVX512_ER:
                    return this.ARCH_AVX512_ER;
                case Arch.ARCH_AVX512_PF:
                    return this.ARCH_AVX512_PF;
                case Arch.ARCH_AVX512_BW:
                    return this.ARCH_AVX512_BW;
                case Arch.ARCH_AVX512_DQ:
                    return this.ARCH_AVX512_DQ;
                case Arch.ARCH_AVX512_VL:
                    return this.ARCH_AVX512_VL;
                case Arch.ARCH_AVX512_IFMA:
                    return this.ARCH_AVX512_IFMA;
                case Arch.ARCH_AVX512_VBMI:
                    return this.ARCH_AVX512_VBMI;
                case Arch.ARCH_AVX512_VPOPCNTDQ:
                    return this.ARCH_AVX512_VPOPCNTDQ;
                case Arch.ARCH_AVX512_4VNNIW:
                    return this.ARCH_AVX512_4VNNIW;
                case Arch.ARCH_AVX512_4FMAPS:
                    return this.ARCH_AVX512_4FMAPS;
                case Arch.ARCH_AVX512_VBMI2:
                    return this.ARCH_AVX512_VBMI2;
                case Arch.ARCH_AVX512_VNNI:
                    return this.ARCH_AVX512_VNNI;
                case Arch.ARCH_AVX512_BITALG:
                    return this.ARCH_AVX512_BITALG;
                case Arch.ARCH_AVX512_GFNI:
                    return this.ARCH_AVX512_GFNI;
                case Arch.ARCH_AVX512_VAES:
                    return this.ARCH_AVX512_VAES;
                case Arch.ARCH_AVX512_VPCLMULQDQ:
                    return this.ARCH_AVX512_VPCLMULQDQ;
                case Arch.ARCH_AVX512_BF16:
                    return this.ARCH_AVX512_BF16;
                case Arch.ARCH_AVX512_VP2INTERSECT:
                    return this.ARCH_AVX512_VP2INTERSECT;
                case Arch.ARCH_ADX:
                    return this.ARCH_ADX;
                case Arch.ARCH_AES:
                    return this.ARCH_AES;
                case Arch.ARCH_VMX:
                    return this.ARCH_VMX;
                case Arch.ARCH_BMI1:
                    return this.ARCH_BMI1;
                case Arch.ARCH_BMI2:
                    return this.ARCH_BMI2;
                case Arch.ARCH_F16C:
                    return this.ARCH_F16C;
                case Arch.ARCH_FMA:
                    return this.ARCH_FMA;
                case Arch.ARCH_FSGSBASE:
                    return this.ARCH_FSGSBASE;
                case Arch.ARCH_HLE:
                    return this.ARCH_HLE;
                case Arch.ARCH_INVPCID:
                    return this.ARCH_INVPCID;
                case Arch.ARCH_SHA:
                    return this.ARCH_SHA;
                case Arch.ARCH_RTM:
                    return this.ARCH_RTM;
                case Arch.ARCH_MPX:
                    return this.ARCH_MPX;
                case Arch.ARCH_PCLMULQDQ:
                    return this.ARCH_PCLMULQDQ;
                case Arch.ARCH_LZCNT:
                    return this.ARCH_LZCNT;
                case Arch.ARCH_PREFETCHWT1:
                    return this.ARCH_PREFETCHWT1;
                case Arch.ARCH_PRFCHW:
                    return this.ARCH_PRFCHW;
                case Arch.ARCH_RDPID:
                    return this.ARCH_RDPID;
                case Arch.ARCH_RDRAND:
                    return this.ARCH_RDRAND;
                case Arch.ARCH_RDSEED:
                    return this.ARCH_RDSEED;
                case Arch.ARCH_XSAVEOPT:
                    return this.ARCH_XSAVEOPT;
                case Arch.ARCH_SGX1:
                    return this.ARCH_SGX1;
                case Arch.ARCH_SGX2:
                    return this.ARCH_SGX2;
                case Arch.ARCH_SMX:
                    return this.ARCH_SMX;
                case Arch.ARCH_CLDEMOTE:
                    return this.ARCH_CLDEMOTE;
                case Arch.ARCH_MOVDIR64B:
                    return this.ARCH_MOVDIR64B;
                case Arch.ARCH_MOVDIRI:
                    return this.ARCH_MOVDIRI;
                case Arch.ARCH_PCONFIG:
                    return this.ARCH_PCONFIG;
                case Arch.ARCH_WAITPKG:
                    return this.ARCH_WAITPKG;
                case Arch.ARCH_ENQCMD:
                    return this.ARCH_ENQCMD;
                case Arch.ARCH_X64:
                    return this.ARCH_X64;
                case Arch.ARCH_IA64:
                    return this.ARCH_IA64;
                case Arch.ARCH_UNDOC:
                    return this.ARCH_UNDOC;
                case Arch.ARCH_AMD:
                    return this.ARCH_AMD;
                case Arch.ARCH_TBM:
                    return this.ARCH_TBM;
                case Arch.ARCH_3DNOW:
                    return this.ARCH_3DNOW;
                case Arch.ARCH_CYRIX:
                    return this.ARCH_CYRIX;
                case Arch.ARCH_CYRIXM:
                    return this.ARCH_CYRIXM;
                default:
                    return false; // TODO return error;
            }
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