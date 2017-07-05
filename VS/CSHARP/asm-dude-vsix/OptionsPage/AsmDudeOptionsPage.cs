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

using AsmDude.Tools;
using AsmTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AsmDude.OptionsPage
{
    [Guid(Guids.GuidOptionsPageAsmDude)]
    public class AsmDudeOptionsPage : UIElementDialogPage
    {
        private const bool logInfo = true;

        private AsmDudeOptionsPageUI _asmDudeOptionsPageUI;

        public AsmDudeOptionsPage()
        {
            this._asmDudeOptionsPageUI = new AsmDudeOptionsPageUI();
        }

        protected override System.Windows.UIElement Child
        {
            get { return this._asmDudeOptionsPageUI; }
        }

        #region Event Handlers

        /// <summary>
        /// Handles "activate" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when Visual Studio wants to activate this page.  
        /// </devdoc>
        /// <remarks>If this handler sets e.Cancel to true, the activation will not occur.</remarks>
        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);

            this._asmDudeOptionsPageUI.UsedAssembler = AsmDudeToolsStatic.Used_Assembler;

            #region AsmDoc
            this._asmDudeOptionsPageUI.AsmDoc_On = Settings.Default.AsmDoc_On;
            this._asmDudeOptionsPageUI.AsmDoc_Url = Settings.Default.AsmDoc_url;
            #endregion

            #region CodeFolding
            this._asmDudeOptionsPageUI.CodeFolding_On = Settings.Default.CodeFolding_On;
            this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped = Settings.Default.CodeFolding_IsDefaultCollapsed;
            this._asmDudeOptionsPageUI.CodeFolding_BeginTag = Settings.Default.CodeFolding_BeginTag;
            this._asmDudeOptionsPageUI.CodeFolding_EndTag = Settings.Default.CodeFolding_EndTag;
            #endregion

            #region Syntax Highlighting
            this._asmDudeOptionsPageUI.SyntaxHighlighting_On = Settings.Default.SyntaxHighlighting_On;
            this._asmDudeOptionsPageUI.ColorMnemonic = Settings.Default.SyntaxHighlighting_Opcode;
            this._asmDudeOptionsPageUI.ColorMnemonic_Italic = Settings.Default.SyntaxHighlighting_Opcode_Italic;
            this._asmDudeOptionsPageUI.ColorRegister = Settings.Default.SyntaxHighlighting_Register;
            this._asmDudeOptionsPageUI.ColorRegister_Italic = Settings.Default.SyntaxHighlighting_Register_Italic;
            this._asmDudeOptionsPageUI.ColorRemark = Settings.Default.SyntaxHighlighting_Remark;
            this._asmDudeOptionsPageUI.ColorRemark_Italic = Settings.Default.SyntaxHighlighting_Remark_Italic;
            this._asmDudeOptionsPageUI.ColorDirective = Settings.Default.SyntaxHighlighting_Directive;
            this._asmDudeOptionsPageUI.ColorDirective_Italic = Settings.Default.SyntaxHighlighting_Directive_Italic;
            this._asmDudeOptionsPageUI.ColorConstant = Settings.Default.SyntaxHighlighting_Constant;
            this._asmDudeOptionsPageUI.ColorConstant_Italic = Settings.Default.SyntaxHighlighting_Constant_Italic;
            this._asmDudeOptionsPageUI.ColorJump = Settings.Default.SyntaxHighlighting_Jump;
            this._asmDudeOptionsPageUI.ColorJump_Italic = Settings.Default.SyntaxHighlighting_Jump_Italic;
            this._asmDudeOptionsPageUI.ColorLabel = Settings.Default.SyntaxHighlighting_Label;
            this._asmDudeOptionsPageUI.ColorLabel_Italic = Settings.Default.SyntaxHighlighting_Label_Italic;
            this._asmDudeOptionsPageUI.ColorMisc = Settings.Default.SyntaxHighlighting_Misc;
            this._asmDudeOptionsPageUI.ColorMisc_Italic = Settings.Default.SyntaxHighlighting_Misc_Italic;
            this._asmDudeOptionsPageUI.ColorUserDefined1 = Settings.Default.SyntaxHighlighting_Userdefined1;
            this._asmDudeOptionsPageUI.ColorUserDefined1_Italic = Settings.Default.SyntaxHighlighting_Userdefined1_Italic;
            this._asmDudeOptionsPageUI.ColorUserDefined2 = Settings.Default.SyntaxHighlighting_Userdefined2;
            this._asmDudeOptionsPageUI.ColorUserDefined2_Italic = Settings.Default.SyntaxHighlighting_Userdefined2_Italic;
            this._asmDudeOptionsPageUI.ColorUserDefined3 = Settings.Default.SyntaxHighlighting_Userdefined3;
            this._asmDudeOptionsPageUI.ColorUserDefined3_Italic = Settings.Default.SyntaxHighlighting_Userdefined3_Italic;
            #endregion

            #region Keyword Highlighting
            this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On = Settings.Default.KeywordHighlighting_BackgroundColor_On;
            this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor = Settings.Default.KeywordHighlighting_BackgroundColor;
            this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor_On = Settings.Default.KeywordHighlighting_BorderColor_On;
            this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor = Settings.Default.KeywordHighlighting_BorderColor;
            #endregion

            #region Latency and Throughput Information (Performance Info)
            this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On = Settings.Default.PerformanceInfo_SandyBridge_On;
            this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On = Settings.Default.PerformanceInfo_IvyBridge_On;
            this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On = Settings.Default.PerformanceInfo_Haswell_On;
            this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On = Settings.Default.PerformanceInfo_Broadwell_On;
            this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On = Settings.Default.PerformanceInfo_Skylake_On;
            this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On = Settings.Default.PerformanceInfo_KnightsLanding_On;
            #endregion

            #region Code Completion
            this._asmDudeOptionsPageUI.UseCodeCompletion = Settings.Default.CodeCompletion_On;
            this._asmDudeOptionsPageUI.UseSignatureHelp = Settings.Default.SignatureHelp_On;

            this._asmDudeOptionsPageUI.UseArch_8086 = Settings.Default.ARCH_8086;
            this._asmDudeOptionsPageUI.UseArch_8086_UI.ToolTip = MakeToolTip(Arch.ARCH_8086);
            this._asmDudeOptionsPageUI.UseArch_186 = Settings.Default.ARCH_186;
            this._asmDudeOptionsPageUI.UseArch_186_UI.ToolTip = MakeToolTip(Arch.ARCH_186);
            this._asmDudeOptionsPageUI.UseArch_286 = Settings.Default.ARCH_286;
            this._asmDudeOptionsPageUI.UseArch_286_UI.ToolTip = MakeToolTip(Arch.ARCH_286);
            this._asmDudeOptionsPageUI.UseArch_386 = Settings.Default.ARCH_386;
            this._asmDudeOptionsPageUI.useArch_386_UI.ToolTip = MakeToolTip(Arch.ARCH_386);
            this._asmDudeOptionsPageUI.UseArch_486 = Settings.Default.ARCH_486;
            this._asmDudeOptionsPageUI.useArch_486_UI.ToolTip = MakeToolTip(Arch.ARCH_486);
            this._asmDudeOptionsPageUI.UseArch_MMX = Settings.Default.ARCH_MMX;
            this._asmDudeOptionsPageUI.useArch_MMX_UI.ToolTip = MakeToolTip(Arch.MMX);
            this._asmDudeOptionsPageUI.UseArch_SSE = Settings.Default.ARCH_SSE;
            this._asmDudeOptionsPageUI.useArch_SSE_UI.ToolTip = MakeToolTip(Arch.SSE);
            this._asmDudeOptionsPageUI.UseArch_SSE2 = Settings.Default.ARCH_SSE2;
            this._asmDudeOptionsPageUI.useArch_SSE2_UI.ToolTip = MakeToolTip(Arch.SSE2);
            this._asmDudeOptionsPageUI.UseArch_SSE3 = Settings.Default.ARCH_SSE3;
            this._asmDudeOptionsPageUI.useArch_SSE3_UI.ToolTip = MakeToolTip(Arch.SSE3);
            this._asmDudeOptionsPageUI.UseArch_SSSE3 = Settings.Default.ARCH_SSSE3;
            this._asmDudeOptionsPageUI.useArch_SSSE3_UI.ToolTip = MakeToolTip(Arch.SSSE3);
            this._asmDudeOptionsPageUI.UseArch_SSE41 = Settings.Default.ARCH_SSE41;
            this._asmDudeOptionsPageUI.useArch_SSE41_UI.ToolTip = MakeToolTip(Arch.SSE4_1);
            this._asmDudeOptionsPageUI.UseArch_SSE42 = Settings.Default.ARCH_SSE42;
            this._asmDudeOptionsPageUI.useArch_SSE42_UI.ToolTip = MakeToolTip(Arch.SSE4_2);
            this._asmDudeOptionsPageUI.UseArch_SSE4A = Settings.Default.ARCH_SSE4A;
            this._asmDudeOptionsPageUI.useArch_SSE4A_UI.ToolTip = MakeToolTip(Arch.SSE4A);
            this._asmDudeOptionsPageUI.UseArch_SSE5 = Settings.Default.ARCH_SSE5;
            this._asmDudeOptionsPageUI.useArch_SSE5_UI.ToolTip = MakeToolTip(Arch.SSE5);
            this._asmDudeOptionsPageUI.UseArch_AVX = Settings.Default.ARCH_AVX;
            this._asmDudeOptionsPageUI.useArch_AVX_UI.ToolTip = MakeToolTip(Arch.AVX);
            this._asmDudeOptionsPageUI.UseArch_AVX2 = Settings.Default.ARCH_AVX2;
            this._asmDudeOptionsPageUI.useArch_AVX2_UI.ToolTip = MakeToolTip(Arch.AVX2);

            this._asmDudeOptionsPageUI.UseArch_AVX512_F = Settings.Default.ARCH_AVX512F;
            this._asmDudeOptionsPageUI.UseArch_AVX512_F_UI.ToolTip = MakeToolTip(Arch.AVX512_F);
            this._asmDudeOptionsPageUI.UseArch_AVX512_VL = Settings.Default.ARCH_AVX512VL;
            this._asmDudeOptionsPageUI.UseArch_AVX512_VL_UI.ToolTip = MakeToolTip(Arch.AVX512_VL);
            this._asmDudeOptionsPageUI.UseArch_AVX512_DQ = Settings.Default.ARCH_AVX512DQ;
            this._asmDudeOptionsPageUI.UseArch_AVX512_DQ_UI.ToolTip = MakeToolTip(Arch.AVX512_DQ);
            this._asmDudeOptionsPageUI.UseArch_AVX512_BW = Settings.Default.ARCH_AVX512BW;
            this._asmDudeOptionsPageUI.UseArch_AVX512_BW_UI.ToolTip = MakeToolTip(Arch.AVX512_BW);
            this._asmDudeOptionsPageUI.UseArch_AVX512_ER = Settings.Default.ARCH_AVX512ER;
            this._asmDudeOptionsPageUI.UseArch_AVX512_ER_UI.ToolTip = MakeToolTip(Arch.AVX512_ER);
            this._asmDudeOptionsPageUI.UseArch_AVX512_CD = Settings.Default.ARCH_AVX512CD;
            this._asmDudeOptionsPageUI.UseArch_AVX512_CD_UI.ToolTip = MakeToolTip(Arch.AVX512_CD);
            this._asmDudeOptionsPageUI.UseArch_AVX512_PF = Settings.Default.ARCH_AVX512PF;
            this._asmDudeOptionsPageUI.UseArch_AVX512_PF_UI.ToolTip = MakeToolTip(Arch.AVX512_PF);
            this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA = Settings.Default.ARCH_AVX512_IFMA;
            this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA_UI.ToolTip = MakeToolTip(Arch.AVX512_IFMA);
            this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI = Settings.Default.ARCH_AVX512_VBMI;
            this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI_UI.ToolTip = MakeToolTip(Arch.AVX512_VBMI);
            this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ = Settings.Default.ARCH_AVX512_VPOPCNTDQ;
            this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ_UI.ToolTip = MakeToolTip(Arch.AVX512_VPOPCNTDQ);
            this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW = Settings.Default.ARCH_AVX512_4VNNIW;
            this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW_UI.ToolTip = MakeToolTip(Arch.AVX512_4VNNIW);
            this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS = Settings.Default.ARCH_AVX512_4FMAPS;
            this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS_UI.ToolTip = MakeToolTip(Arch.AVX512_4FMAPS);

            this._asmDudeOptionsPageUI.UseArch_X64 = Settings.Default.ARCH_X64;
            this._asmDudeOptionsPageUI.useArch_X64_UI.ToolTip = MakeToolTip(Arch.X64);
            this._asmDudeOptionsPageUI.UseArch_BMI1 = Settings.Default.ARCH_BMI1;
            this._asmDudeOptionsPageUI.useArch_BMI1_UI.ToolTip = MakeToolTip(Arch.BMI1);
            this._asmDudeOptionsPageUI.UseArch_BMI2 = Settings.Default.ARCH_BMI2;
            this._asmDudeOptionsPageUI.useArch_BMI2_UI.ToolTip = MakeToolTip(Arch.BMI2);
            this._asmDudeOptionsPageUI.UseArch_P6 = Settings.Default.ARCH_P6;
            this._asmDudeOptionsPageUI.useArch_P6_UI.ToolTip = MakeToolTip(Arch.P6);
            this._asmDudeOptionsPageUI.UseArch_IA64 = Settings.Default.ARCH_IA64;
            this._asmDudeOptionsPageUI.useArch_IA64_UI.ToolTip = MakeToolTip(Arch.IA64);
            this._asmDudeOptionsPageUI.UseArch_FMA = Settings.Default.ARCH_FMA;
            this._asmDudeOptionsPageUI.useArch_FMA_UI.ToolTip = MakeToolTip(Arch.FMA);
            this._asmDudeOptionsPageUI.UseArch_TBM = Settings.Default.ARCH_TBM;
            this._asmDudeOptionsPageUI.useArch_TBM_UI.ToolTip = MakeToolTip(Arch.TBM);
            this._asmDudeOptionsPageUI.UseArch_AMD = Settings.Default.ARCH_AMD;
            this._asmDudeOptionsPageUI.useArch_AMD_UI.ToolTip = MakeToolTip(Arch.AMD);
            this._asmDudeOptionsPageUI.UseArch_PENT = Settings.Default.ARCH_PENT;
            this._asmDudeOptionsPageUI.useArch_PENT_UI.ToolTip = MakeToolTip(Arch.PENT);
            this._asmDudeOptionsPageUI.UseArch_3DNOW = Settings.Default.ARCH_3DNOW;
            this._asmDudeOptionsPageUI.useArch_3DNOW_UI.ToolTip = MakeToolTip(Arch.ARCH_3DNOW);
            this._asmDudeOptionsPageUI.UseArch_CYRIX = Settings.Default.ARCH_CYRIX;
            this._asmDudeOptionsPageUI.useArch_CYRIX_UI.ToolTip = MakeToolTip(Arch.CYRIX);
            this._asmDudeOptionsPageUI.UseArch_CYRIXM = Settings.Default.ARCH_CYRIXM;
            this._asmDudeOptionsPageUI.useArch_CYRIXM_UI.ToolTip = MakeToolTip(Arch.CYRIXM);
            this._asmDudeOptionsPageUI.UseArch_VMX = Settings.Default.ARCH_VMX;
            this._asmDudeOptionsPageUI.useArch_VMX_UI.ToolTip = MakeToolTip(Arch.VMX);
            this._asmDudeOptionsPageUI.UseArch_RTM = Settings.Default.ARCH_RTM;
            this._asmDudeOptionsPageUI.useArch_RTM_UI.ToolTip = MakeToolTip(Arch.RTM);
            this._asmDudeOptionsPageUI.UseArch_MPX = Settings.Default.ARCH_MPX;
            this._asmDudeOptionsPageUI.useArch_MPX_UI.ToolTip = MakeToolTip(Arch.MPX);
            this._asmDudeOptionsPageUI.UseArch_SHA = Settings.Default.ARCH_SHA;
            this._asmDudeOptionsPageUI.useArch_SHA_UI.ToolTip = MakeToolTip(Arch.SHA);

            this._asmDudeOptionsPageUI.UseArch_ADX = Settings.Default.ARCH_ADX;
            this._asmDudeOptionsPageUI.useArch_ADX_UI.ToolTip = MakeToolTip(Arch.ADX);
            this._asmDudeOptionsPageUI.UseArch_F16C = Settings.Default.ARCH_F16C;
            this._asmDudeOptionsPageUI.useArch_F16C_UI.ToolTip = MakeToolTip(Arch.F16C);
            this._asmDudeOptionsPageUI.UseArch_FSGSBASE = Settings.Default.ARCH_FSGSBASE;
            this._asmDudeOptionsPageUI.useArch_FSGSBASE_UI.ToolTip = MakeToolTip(Arch.FSGSBASE);
            this._asmDudeOptionsPageUI.UseArch_HLE = Settings.Default.ARCH_HLE;
            this._asmDudeOptionsPageUI.useArch_HLE_UI.ToolTip = MakeToolTip(Arch.HLE);
            this._asmDudeOptionsPageUI.UseArch_INVPCID = Settings.Default.ARCH_INVPCID;
            this._asmDudeOptionsPageUI.useArch_INVPCID_UI.ToolTip = MakeToolTip(Arch.INVPCID);
            this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ = Settings.Default.ARCH_PCLMULQDQ;
            this._asmDudeOptionsPageUI.useArch_PCLMULQDQ_UI.ToolTip = MakeToolTip(Arch.PCLMULQDQ);
            this._asmDudeOptionsPageUI.UseArch_LZCNT = Settings.Default.ARCH_LZCNT;
            this._asmDudeOptionsPageUI.useArch_LZCNT_UI.ToolTip = MakeToolTip(Arch.LZCNT);
            this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1 = Settings.Default.ARCH_PREFETCHWT1;
            this._asmDudeOptionsPageUI.useArch_PREFETCHWT1_UI.ToolTip = MakeToolTip(Arch.PREFETCHWT1);
            this._asmDudeOptionsPageUI.UseArch_PREFETCHW = Settings.Default.ARCH_PRFCHW;
            this._asmDudeOptionsPageUI.useArch_PREFETCHW_UI.ToolTip = MakeToolTip(Arch.PRFCHW);
            this._asmDudeOptionsPageUI.UseArch_RDPID = Settings.Default.ARCH_RDPID;
            this._asmDudeOptionsPageUI.useArch_RDPID_UI.ToolTip = MakeToolTip(Arch.RDPID);
            this._asmDudeOptionsPageUI.UseArch_RDRAND = Settings.Default.ARCH_RDRAND;
            this._asmDudeOptionsPageUI.useArch_RDRAND_UI.ToolTip = MakeToolTip(Arch.RDRAND);
            this._asmDudeOptionsPageUI.UseArch_RDSEED = Settings.Default.ARCH_RDSEED;
            this._asmDudeOptionsPageUI.useArch_RDSEED_UI.ToolTip = MakeToolTip(Arch.RDSEED);
            this._asmDudeOptionsPageUI.UseArch_XSAVEOPT = Settings.Default.ARCH_XSAVEOPT;
            this._asmDudeOptionsPageUI.useArch_XSAVEOPT_UI.ToolTip = MakeToolTip(Arch.XSAVEOPT);
            this._asmDudeOptionsPageUI.UseArch_UNDOC = Settings.Default.ARCH_UNDOC;
            this._asmDudeOptionsPageUI.useArch_UNDOC_UI.ToolTip = MakeToolTip(Arch.UNDOC);
            this._asmDudeOptionsPageUI.UseArch_AES = Settings.Default.ARCH_AES;
            this._asmDudeOptionsPageUI.useArch_AES_UI.ToolTip = MakeToolTip(Arch.AES);
            #endregion

            #region Intellisense
            this._asmDudeOptionsPageUI.Intellisense_UseLabelAnalysis = Settings.Default.IntelliSense_Label_Analysis_On;
            this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Labels = Settings.Default.IntelliSense_Show_UndefinedLabels;
            this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Labels = Settings.Default.IntelliSense_Decorate_UndefinedLabels;
            this._asmDudeOptionsPageUI.IntelliSense_Show_Clashing_Labels = Settings.Default.IntelliSense_Show_ClashingLabels;
            this._asmDudeOptionsPageUI.IntelliSense_Decorate_Clashing_Labels = Settings.Default.IntelliSense_Decorate_ClashingLabels;
            this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Includes = Settings.Default.IntelliSense_Show_Undefined_Includes;
            this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Includes = Settings.Default.IntelliSense_Decorate_Undefined_Includes;
            #endregion

            #region AsmSim
            this._asmDudeOptionsPageUI.AsmSim_On = Settings.Default.AsmSim_On;
            this._asmDudeOptionsPageUI.AsmSim_Z3_Timeout_MS = Settings.Default.AsmSim_Z3_Timeout_MS;
            this._asmDudeOptionsPageUI.AsmSim_Number_Of_Threads = Settings.Default.AsmSim_Number_Of_Threads;
            this._asmDudeOptionsPageUI.AsmSim_Number_Of_Steps = Settings.Default.AsmSim_Number_Of_Steps;
            this._asmDudeOptionsPageUI.AsmSim_64_Bits = Settings.Default.AsmSim_64_Bits;
            this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors = Settings.Default.AsmSim_Show_Syntax_Errors;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors = Settings.Default.AsmSim_Decorate_Syntax_Errors;
            this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined = Settings.Default.AsmSim_Show_Usage_Of_Undefined;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined = Settings.Default.AsmSim_Decorate_Usage_Of_Undefined;
            this._asmDudeOptionsPageUI.AsmSim_Show_Redundant_Instructions = Settings.Default.AsmSim_Show_Redundant_Instructions;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Redundant_Instructions = Settings.Default.AsmSim_Decorate_Redundant_Instructions;
            this._asmDudeOptionsPageUI.AsmSim_Show_Unreachable_Instructions = Settings.Default.AsmSim_Show_Unreachable_Instructions;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Unreachable_Instructions = Settings.Default.AsmSim_Decorate_Unreachable_Instructions;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers = Settings.Default.AsmSim_Decorate_Registers;
            this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion = Settings.Default.AsmSim_Use_In_Code_Completion;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented = Settings.Default.AsmSim_Decorate_Unimplemented;
            this._asmDudeOptionsPageUI.AsmSim_Pragma_Assume = Settings.Default.AsmSim_Pragma_Assume;
            #endregion
        }

        private string MakeToolTip(Arch arch)
        {
            MnemonicStore store = AsmDudeTools.Instance.Mnemonic_Store;
            SortedSet<Mnemonic> usedMnemonics = new SortedSet<Mnemonic>();

            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)).Cast<Mnemonic>())
            {
                if (store.GetArch(mnemonic).Contains(arch))
                {
                    usedMnemonics.Add(mnemonic);
                }
            }
            StringBuilder sb = new StringBuilder();
            string docArch = ArchTools.ArchDocumentation(arch);
            if (docArch.Length > 0)
            {
                sb.Append(docArch + ": ");
            }
            if (usedMnemonics.Count > 0)
            {
                foreach (Mnemonic mnemonic in usedMnemonics)
                {
                    sb.Append(mnemonic.ToString());
                    sb.Append(", ");
                }
                sb.Length -= 2; // get rid of last comma.
            } else
            {
                sb.Append("empty");
            }
            return AsmSourceTools.Linewrap(sb.ToString(), AsmDudePackage.maxNumberOfCharsInToolTips);
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e) { }

        /// <summary>
        /// Handles "deactivate" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when VS wants to deactivate this
        /// page.  If this handler sets e.Cancel, the deactivation will not occur.
        /// </devdoc>
        /// <remarks>
        /// A "deactivate" message is sent when focus changes to a different page in
        /// the dialog.
        /// </remarks>
        protected override void OnDeactivate(CancelEventArgs e)
        {
            bool changed = false;
            StringBuilder sb = new StringBuilder();

            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                sb.AppendLine("UsedAssembler=" + this._asmDudeOptionsPageUI.UsedAssembler);
                changed = true;
            }

            #region AsmDoc
            if (Settings.Default.AsmDoc_On != this._asmDudeOptionsPageUI.AsmDoc_On)
            {
                sb.AppendLine("AsmDoc_On=" + this._asmDudeOptionsPageUI.AsmDoc_On);
                changed = true;
            }
            if (Settings.Default.AsmDoc_url != this._asmDudeOptionsPageUI.AsmDoc_Url)
            {
                sb.AppendLine("AsmDoc_Url=" + this._asmDudeOptionsPageUI.AsmDoc_Url);
                changed = true;
            }
            #endregion

            #region CodeFolding
            if (Settings.Default.CodeFolding_On != this._asmDudeOptionsPageUI.CodeFolding_On)
            {
                sb.AppendLine("CodeFolding_On=" + this._asmDudeOptionsPageUI.CodeFolding_On);
                changed = true;
            }
            if (Settings.Default.CodeFolding_IsDefaultCollapsed != this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped)
            {
                sb.AppendLine("CodeFolding_IsDefaultCollaped=" + this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped);
                changed = true;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._asmDudeOptionsPageUI.CodeFolding_BeginTag)
            {
                sb.AppendLine("CodeFolding_BeginTag=" + this._asmDudeOptionsPageUI.CodeFolding_BeginTag);
                changed = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._asmDudeOptionsPageUI.CodeFolding_EndTag)
            {
                sb.AppendLine("CodeFolding_EndTag=" + this._asmDudeOptionsPageUI.CodeFolding_EndTag);
                changed = true;
            }
            #endregion

            #region Syntax Highlighting
            if (Settings.Default.SyntaxHighlighting_On != this._asmDudeOptionsPageUI.SyntaxHighlighting_On)
            {
                sb.AppendLine("SyntaxHighlighting_On=" + this._asmDudeOptionsPageUI.SyntaxHighlighting_On);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.ColorMnemonic.ToArgb())
            {
                sb.AppendLine("ColorMnemonic: old=" + Settings.Default.SyntaxHighlighting_Opcode.Name + "; new=" + this._asmDudeOptionsPageUI.ColorMnemonic.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode_Italic != this._asmDudeOptionsPageUI.ColorMnemonic_Italic)
            {
                sb.AppendLine("ColorMnemonic_Italic: old=" + Settings.Default.SyntaxHighlighting_Opcode_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorMnemonic_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.ColorRegister.ToArgb())
            {
                sb.AppendLine("ColorRegister: old=" + Settings.Default.SyntaxHighlighting_Register.Name + "; new=" + this._asmDudeOptionsPageUI.ColorRegister.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register_Italic != this._asmDudeOptionsPageUI.ColorRegister_Italic)
            {
                sb.AppendLine("ColorRegister_Italic: old=" + Settings.Default.SyntaxHighlighting_Register_Italic + "; new =" + this._asmDudeOptionsPageUI.ColorRegister_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.ColorRemark.ToArgb())
            {
                sb.AppendLine("ColorRemark: old=" + Settings.Default.SyntaxHighlighting_Remark.Name + "; new=" + this._asmDudeOptionsPageUI.ColorRemark.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark_Italic != this._asmDudeOptionsPageUI.ColorRemark_Italic)
            {
                sb.AppendLine("ColorRemark_Italic: old=" + Settings.Default.SyntaxHighlighting_Remark_Italic + "; new="+ this._asmDudeOptionsPageUI.ColorRemark_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.ColorDirective.ToArgb())
            {
                sb.AppendLine("colorDirective: old=" + Settings.Default.SyntaxHighlighting_Directive.Name + "; new=" + this._asmDudeOptionsPageUI.ColorDirective.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive_Italic != this._asmDudeOptionsPageUI.ColorDirective_Italic)
            {
                sb.AppendLine("ColorDirective_Italic: old=" + Settings.Default.SyntaxHighlighting_Directive_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorDirective_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.ColorConstant.ToArgb())
            {
                sb.AppendLine("colorConstant: old=" + Settings.Default.SyntaxHighlighting_Constant.Name + "; new=" + this._asmDudeOptionsPageUI.ColorConstant.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant_Italic != this._asmDudeOptionsPageUI.ColorConstant_Italic)
            {
                sb.AppendLine("ColorConstant_Italic: old=" + Settings.Default.SyntaxHighlighting_Constant_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorConstant_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.ColorJump.ToArgb())
            {
                sb.AppendLine("colorJump: old=" + Settings.Default.SyntaxHighlighting_Jump.Name + "; new=" + this._asmDudeOptionsPageUI.ColorJump.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump_Italic != this._asmDudeOptionsPageUI.ColorJump_Italic)
            {
                sb.AppendLine("ColorJump_Italic: old=" + Settings.Default.SyntaxHighlighting_Jump_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorJump_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.ColorLabel.ToArgb())
            {
                sb.AppendLine("colorLabel: old=" + Settings.Default.SyntaxHighlighting_Label.ToKnownColor() + "; new=" + this._asmDudeOptionsPageUI.ColorLabel.ToKnownColor());
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label_Italic != this._asmDudeOptionsPageUI.ColorLabel_Italic)
            {
                sb.AppendLine("ColorLabel_Italic: old=" + Settings.Default.SyntaxHighlighting_Label_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorLabel_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.ColorMisc.ToArgb())
            {
                sb.AppendLine("colorMisc: old=" + Settings.Default.SyntaxHighlighting_Misc.Name + "; new=" + this._asmDudeOptionsPageUI.ColorMisc.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc_Italic != this._asmDudeOptionsPageUI.ColorMisc_Italic)
            {
                sb.AppendLine("ColorMisc_Italic: old=" + Settings.Default.SyntaxHighlighting_Misc_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorMisc_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined1.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined1.ToArgb())
            {
                sb.AppendLine("ColorUserDefined1: old=" + Settings.Default.SyntaxHighlighting_Userdefined1.Name + "; old=" + this._asmDudeOptionsPageUI.ColorUserDefined1.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined1_Italic != this._asmDudeOptionsPageUI.ColorUserDefined1_Italic)
            {
                sb.AppendLine("ColorUserDefined1_Italic: old=" + Settings.Default.SyntaxHighlighting_Userdefined1_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorUserDefined1_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined2.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined2.ToArgb())
            {
                sb.AppendLine("ColorUserDefined2: old=" + Settings.Default.SyntaxHighlighting_Userdefined2.Name + "; new=" + this._asmDudeOptionsPageUI.ColorUserDefined2.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined2_Italic != this._asmDudeOptionsPageUI.ColorUserDefined2_Italic)
            {
                sb.AppendLine("ColorUserDefined2_Italic: old=" + Settings.Default.SyntaxHighlighting_Userdefined2_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorUserDefined2_Italic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined3.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined3.ToArgb())
            {
                sb.AppendLine("ColorUserDefined3: old=" + Settings.Default.SyntaxHighlighting_Userdefined3.Name + "; new=" + this._asmDudeOptionsPageUI.ColorUserDefined3.Name);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined3_Italic != this._asmDudeOptionsPageUI.ColorUserDefined3_Italic)
            {
                sb.AppendLine("ColorUserDefined3_Italic: old=" + Settings.Default.SyntaxHighlighting_Userdefined3_Italic + "; new=" + this._asmDudeOptionsPageUI.ColorUserDefined3_Italic);
                changed = true;
            }
            #endregion

            #region Keyword Highlighting
            if (Settings.Default.KeywordHighlighting_BackgroundColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On)
            {
                sb.AppendLine("KeywordHighlighting_BackgroundColor_On=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BackgroundColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor.ToArgb())
            {
                sb.AppendLine("KeywordHighlighting_BackgroundColor=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor_On)
            {
                sb.AppendLine("KeywordHighlighting_BorderColor_On=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor.ToArgb())
            {
                sb.AppendLine("KeywordHighlighting_BorderColor=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor);
                changed = true;
            }
            #endregion

            #region Latency and Throughput Information (Performance Info)
            if (Settings.Default.PerformanceInfo_SandyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On)
            {
                sb.AppendLine("PerformanceInfo_SandyBridge_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_IvyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On)
            {
                sb.AppendLine("PerformanceInfo_IvyBridge_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Haswell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On)
            {
                sb.AppendLine("PerformanceInfo_Haswell_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Broadwell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On)
            {
                sb.AppendLine("PerformanceInfo_Broadwell_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Skylake_On != this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On)
            {
                sb.AppendLine("PerformanceInfo_Skylake_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_KnightsLanding_On != this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On)
            {
                sb.AppendLine("PerformanceInfo_KnightsLanding_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On);
                changed = true;
            }
            #endregion

            #region Code Completion
            if (Settings.Default.CodeCompletion_On != this._asmDudeOptionsPageUI.UseCodeCompletion)
            {
                sb.AppendLine("UseCodeCompletion=" + this._asmDudeOptionsPageUI.UseCodeCompletion);
                changed = true;
            }
            if (Settings.Default.SignatureHelp_On != this._asmDudeOptionsPageUI.UseSignatureHelp)
            {
                sb.AppendLine("UseSignatureHelp=" + this._asmDudeOptionsPageUI.UseSignatureHelp);
                changed = true;
            }

            if (Settings.Default.ARCH_8086 != this._asmDudeOptionsPageUI.UseArch_8086)
            {
                sb.AppendLine("UseArch_8086=" + this._asmDudeOptionsPageUI.UseArch_8086);
                changed = true;
            }
            if (Settings.Default.ARCH_186 != this._asmDudeOptionsPageUI.UseArch_186)
            {
                sb.AppendLine("UseArch_186=" + this._asmDudeOptionsPageUI.UseArch_186);
                changed = true;
            }
            if (Settings.Default.ARCH_286 != this._asmDudeOptionsPageUI.UseArch_286)
            {
                sb.AppendLine("UseArch_286=" + this._asmDudeOptionsPageUI.UseArch_286);
                changed = true;
            }
            if (Settings.Default.ARCH_386 != this._asmDudeOptionsPageUI.UseArch_386)
            {
                sb.AppendLine("UseArch_386=" + this._asmDudeOptionsPageUI.UseArch_386);
                changed = true;
            }
            if (Settings.Default.ARCH_486 != this._asmDudeOptionsPageUI.UseArch_486)
            {
                sb.AppendLine("UseArch_486=" + this._asmDudeOptionsPageUI.UseArch_486);
                changed = true;
            }
            if (Settings.Default.ARCH_MMX != this._asmDudeOptionsPageUI.UseArch_MMX)
            {
                sb.AppendLine("UseArch_MMX=" + this._asmDudeOptionsPageUI.UseArch_MMX);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE != this._asmDudeOptionsPageUI.UseArch_SSE)
            {
                sb.AppendLine("UseArch_SSE=" + this._asmDudeOptionsPageUI.UseArch_SSE);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE2 != this._asmDudeOptionsPageUI.UseArch_SSE2)
            {
                sb.AppendLine("UseArch_SSE2=" + this._asmDudeOptionsPageUI.UseArch_SSE2);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE3 != this._asmDudeOptionsPageUI.UseArch_SSE3)
            {
                sb.AppendLine("UseArch_SSE3=" + this._asmDudeOptionsPageUI.UseArch_SSE3);
                changed = true;
            }
            if (Settings.Default.ARCH_SSSE3 != this._asmDudeOptionsPageUI.UseArch_SSSE3)
            {
                sb.AppendLine("UseArch_SSSE3=" + this._asmDudeOptionsPageUI.UseArch_SSSE3);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE41 != this._asmDudeOptionsPageUI.UseArch_SSE41)
            {
                sb.AppendLine("UseArch_SSE41=" + this._asmDudeOptionsPageUI.UseArch_SSE41);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE42 != this._asmDudeOptionsPageUI.UseArch_SSE42)
            {
                sb.AppendLine("UseArch_SSE42=" + this._asmDudeOptionsPageUI.UseArch_SSE42);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE4A != this._asmDudeOptionsPageUI.UseArch_SSE4A)
            {
                sb.AppendLine("UseArch_SSE4A=" + this._asmDudeOptionsPageUI.UseArch_SSE4A);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE5 != this._asmDudeOptionsPageUI.UseArch_SSE5)
            {
                sb.AppendLine("UseArch_SSE5=" + this._asmDudeOptionsPageUI.UseArch_SSE5);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX != this._asmDudeOptionsPageUI.UseArch_AVX)
            {
                sb.AppendLine("UseArch_AVX=" + this._asmDudeOptionsPageUI.UseArch_AVX);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX2 != this._asmDudeOptionsPageUI.UseArch_AVX2)
            {
                sb.AppendLine("UseArch_AVX2=" + this._asmDudeOptionsPageUI.UseArch_AVX2);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512PF != this._asmDudeOptionsPageUI.UseArch_AVX512_PF)
            {
                sb.AppendLine("UseArch_AVX512_PF=" + this._asmDudeOptionsPageUI.UseArch_AVX512_PF);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512VL != this._asmDudeOptionsPageUI.UseArch_AVX512_VL)
            {
                sb.AppendLine("UseArch_AVX512_VL=" + this._asmDudeOptionsPageUI.UseArch_AVX512_VL);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512DQ != this._asmDudeOptionsPageUI.UseArch_AVX512_DQ)
            {
                sb.AppendLine("UseArch_AVX512_DQ=" + this._asmDudeOptionsPageUI.UseArch_AVX512_DQ);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512BW != this._asmDudeOptionsPageUI.UseArch_AVX512_BW)
            {
                sb.AppendLine("UseArch_AVX512_BW=" + this._asmDudeOptionsPageUI.UseArch_AVX512_BW);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512ER != this._asmDudeOptionsPageUI.UseArch_AVX512_ER)
            {
                sb.AppendLine("UseArch_AVX512_ER=" + this._asmDudeOptionsPageUI.UseArch_AVX512_ER);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512F != this._asmDudeOptionsPageUI.UseArch_AVX512_F)
            {
                sb.AppendLine("UseArch_AVX512_F=" + this._asmDudeOptionsPageUI.UseArch_AVX512_F);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512CD != this._asmDudeOptionsPageUI.UseArch_AVX512_CD)
            {
                sb.AppendLine("UseArch_AVX512_CD=" + this._asmDudeOptionsPageUI.UseArch_AVX512_CD);
                changed = true;
            }

            if (Settings.Default.ARCH_AVX512_IFMA != this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA)
            {
                sb.AppendLine("UseArch_AVX512_IFMA=" + this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_VBMI != this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI)
            {
                sb.AppendLine("UseArch_AVX512_VBMI=" + this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_VPOPCNTDQ != this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ)
            {
                sb.AppendLine("UseArch_AVX512_VPOPCNTDQ=" + this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_4VNNIW != this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW)
            {
                sb.AppendLine("UseArch_AVX512_4VNNIW=" + this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_4FMAPS != this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS)
            {
                sb.AppendLine("UseArch_AVX512_4FMAPS=" + this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS);
                changed = true;
            }

            if (Settings.Default.ARCH_X64 != this._asmDudeOptionsPageUI.UseArch_X64)
            {
                sb.AppendLine("UseArch_X64=" + this._asmDudeOptionsPageUI.UseArch_X64);
                changed = true;
            }
            if (Settings.Default.ARCH_BMI1 != this._asmDudeOptionsPageUI.UseArch_BMI1)
            {
                sb.AppendLine("UseArch_BMI1=" + this._asmDudeOptionsPageUI.UseArch_BMI1);
                changed = true;
            }
            if (Settings.Default.ARCH_BMI2 != this._asmDudeOptionsPageUI.UseArch_BMI2)
            {
                sb.AppendLine("UseArch_BMI2=" + this._asmDudeOptionsPageUI.UseArch_BMI2);
                changed = true;
            }
            if (Settings.Default.ARCH_P6 != this._asmDudeOptionsPageUI.UseArch_P6)
            {
                sb.AppendLine("UseArch_P6=" + this._asmDudeOptionsPageUI.UseArch_P6);
                changed = true;
            }
            if (Settings.Default.ARCH_IA64 != this._asmDudeOptionsPageUI.UseArch_IA64)
            {
                sb.AppendLine("UseArch_IA64=" + this._asmDudeOptionsPageUI.UseArch_IA64);
                changed = true;
            }
            if (Settings.Default.ARCH_FMA != this._asmDudeOptionsPageUI.UseArch_FMA)
            {
                sb.AppendLine("UseArch_FMA=" + this._asmDudeOptionsPageUI.UseArch_FMA);
                changed = true;
            }
            if (Settings.Default.ARCH_TBM != this._asmDudeOptionsPageUI.UseArch_TBM)
            {
                sb.AppendLine("UseArch_TBM=" + this._asmDudeOptionsPageUI.UseArch_TBM);
                changed = true;
            }
            if (Settings.Default.ARCH_AMD != this._asmDudeOptionsPageUI.UseArch_AMD)
            {
                sb.AppendLine("UseArch_AMD=" + this._asmDudeOptionsPageUI.UseArch_AMD);
                changed = true;
            }
            if (Settings.Default.ARCH_PENT != this._asmDudeOptionsPageUI.UseArch_PENT)
            {
                sb.AppendLine("useArch_PENT=" + this._asmDudeOptionsPageUI.UseArch_PENT);
                changed = true;
            }
            if (Settings.Default.ARCH_3DNOW != this._asmDudeOptionsPageUI.UseArch_3DNOW)
            {
                sb.AppendLine("UseArch_3DNOW=" + this._asmDudeOptionsPageUI.UseArch_3DNOW);
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIX != this._asmDudeOptionsPageUI.UseArch_CYRIX)
            {
                sb.AppendLine("UseArch_CYRIX=" + this._asmDudeOptionsPageUI.UseArch_CYRIX);
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIXM != this._asmDudeOptionsPageUI.UseArch_CYRIXM)
            {
                sb.AppendLine("UseArch_CYRIXM=" + this._asmDudeOptionsPageUI.UseArch_CYRIXM);
                changed = true;
            }
            if (Settings.Default.ARCH_VMX != this._asmDudeOptionsPageUI.UseArch_VMX)
            {
                sb.AppendLine("UseArch_VMX=" + this._asmDudeOptionsPageUI.UseArch_VMX);
                changed = true;
            }
            if (Settings.Default.ARCH_RTM != this._asmDudeOptionsPageUI.UseArch_RTM)
            {
                sb.AppendLine("UseArch_RTM=" + this._asmDudeOptionsPageUI.UseArch_RTM);
                changed = true;
            }
            if (Settings.Default.ARCH_MPX != this._asmDudeOptionsPageUI.UseArch_MPX)
            {
                sb.AppendLine("UseArch_MPX=" + this._asmDudeOptionsPageUI.UseArch_MPX);
                changed = true;
            }
            if (Settings.Default.ARCH_SHA != this._asmDudeOptionsPageUI.UseArch_SHA)
            {
                sb.AppendLine("UseArch_SHA=" + this._asmDudeOptionsPageUI.UseArch_SHA);
                changed = true;
            }

            if (Settings.Default.ARCH_ADX != this._asmDudeOptionsPageUI.UseArch_ADX)
            {
                sb.AppendLine("UseArch_ADX=" + this._asmDudeOptionsPageUI.UseArch_ADX);
                changed = true;
            }
            if (Settings.Default.ARCH_F16C != this._asmDudeOptionsPageUI.UseArch_F16C)
            {
                sb.AppendLine("UseArch_F16C=" + this._asmDudeOptionsPageUI.UseArch_F16C);
                changed = true;
            }
            if (Settings.Default.ARCH_FSGSBASE != this._asmDudeOptionsPageUI.UseArch_FSGSBASE)
            {
                sb.AppendLine("UseArch_FSGSBASE=" + this._asmDudeOptionsPageUI.UseArch_FSGSBASE);
                changed = true;
            }
            if (Settings.Default.ARCH_HLE != this._asmDudeOptionsPageUI.UseArch_HLE)
            {
                sb.AppendLine("UseArch_HLE=" + this._asmDudeOptionsPageUI.UseArch_HLE);
                changed = true;
            }
            if (Settings.Default.ARCH_INVPCID != this._asmDudeOptionsPageUI.UseArch_INVPCID)
            {
                sb.AppendLine("UseArch_INVPCID=" + this._asmDudeOptionsPageUI.UseArch_INVPCID);
                changed = true;
            }
            if (Settings.Default.ARCH_PCLMULQDQ != this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ)
            {
                sb.AppendLine("UseArch_PCLMULQDQ=" + this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ);
                changed = true;
            }
            if (Settings.Default.ARCH_LZCNT != this._asmDudeOptionsPageUI.UseArch_LZCNT)
            {
                sb.AppendLine("UseArch_LZCNT=" + this._asmDudeOptionsPageUI.UseArch_LZCNT);
                changed = true;
            }
            if (Settings.Default.ARCH_PREFETCHWT1 != this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1)
            {
                sb.AppendLine("UseArch_PREFETCHWT1=" + this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1);
                changed = true;
            }
            if (Settings.Default.ARCH_PRFCHW != this._asmDudeOptionsPageUI.UseArch_PREFETCHW)
            {
                sb.AppendLine("UseArch_PREFETCHW=" + this._asmDudeOptionsPageUI.UseArch_PREFETCHW);
                changed = true;
            }
            if (Settings.Default.ARCH_RDPID != this._asmDudeOptionsPageUI.UseArch_RDPID)
            {
                sb.AppendLine("UseArch_RDPID=" + this._asmDudeOptionsPageUI.UseArch_RDPID);
                changed = true;
            }
            if (Settings.Default.ARCH_RDRAND != this._asmDudeOptionsPageUI.UseArch_RDRAND)
            {
                sb.AppendLine("UseArch_RDRAND=" + this._asmDudeOptionsPageUI.UseArch_RDRAND);
                changed = true;
            }
            if (Settings.Default.ARCH_RDSEED != this._asmDudeOptionsPageUI.UseArch_RDSEED)
            {
                sb.AppendLine("UseArch_RDSEED=" + this._asmDudeOptionsPageUI.UseArch_RDSEED);
                changed = true;
            }
            if (Settings.Default.ARCH_XSAVEOPT != this._asmDudeOptionsPageUI.UseArch_XSAVEOPT)
            {
                sb.AppendLine("UseArch_XSAVEOPT=" + this._asmDudeOptionsPageUI.UseArch_XSAVEOPT);
                changed = true;
            }
            if (Settings.Default.ARCH_UNDOC != this._asmDudeOptionsPageUI.UseArch_UNDOC)
            {
                sb.AppendLine("UseArch_UNDOC=" + this._asmDudeOptionsPageUI.UseArch_UNDOC);
                changed = true;
            }
            if (Settings.Default.ARCH_AES != this._asmDudeOptionsPageUI.UseArch_AES)
            {
                sb.AppendLine("UseArch_AES=" + this._asmDudeOptionsPageUI.UseArch_AES);
                changed = true;
            }
            #endregion

            #region Intellisense
            if (Settings.Default.IntelliSense_Label_Analysis_On != this._asmDudeOptionsPageUI.Intellisense_UseLabelAnalysis)
            {
                sb.AppendLine("Intellisense_UseLabelAnalysis=" + this._asmDudeOptionsPageUI.Intellisense_UseLabelAnalysis);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_UndefinedLabels != this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Labels)
            {
                sb.AppendLine("IntelliSense_Show_Undefined_Labels=" + this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_ClashingLabels != this._asmDudeOptionsPageUI.IntelliSense_Show_Clashing_Labels)
            {
                sb.AppendLine("IntelliSense_Show_Clashing_Labels=" + this._asmDudeOptionsPageUI.IntelliSense_Show_Clashing_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_UndefinedLabels != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Labels)
            {
                sb.AppendLine("IntelliSense_Decorate_Undefined_Labels=" + this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_ClashingLabels != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Clashing_Labels)
            {
                sb.AppendLine("IntelliSense_Decorate_Clashing_Labels=" + this._asmDudeOptionsPageUI.IntelliSense_Decorate_Clashing_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_Undefined_Includes != this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Includes)
            {
                sb.AppendLine("IntelliSense_Show_Undefined_Includes=" + this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Includes);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_Undefined_Includes != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Includes)
            {
                sb.AppendLine("IntelliSense_Decorate_Undefined_Includes=" + this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Includes);
                changed = true;
            }
            #endregion

            #region AsmSim
            if (Settings.Default.AsmSim_On != this._asmDudeOptionsPageUI.AsmSim_On)
            {
                sb.AppendLine("AsmSim_On=" + this._asmDudeOptionsPageUI.AsmSim_On);
                changed = true;

                if (!this._asmDudeOptionsPageUI.AsmSim_On)
                {
                    string title = null;
                    string message = "I'm sorry "+ Environment.UserName + ", I'm afraid I can't do that.";
                    int result = VsShellUtilities.ShowMessageBox(this.Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            if (Settings.Default.AsmSim_Z3_Timeout_MS != this._asmDudeOptionsPageUI.AsmSim_Z3_Timeout_MS)
            {
                sb.AppendLine("AsmSim_Z3_Timeout_MS=" + this._asmDudeOptionsPageUI.AsmSim_Z3_Timeout_MS);
                changed = true;
            }
            if (Settings.Default.AsmSim_Number_Of_Threads != this._asmDudeOptionsPageUI.AsmSim_Number_Of_Threads)
            {
                sb.AppendLine("AsmSim_Number_Of_Threads=" + this._asmDudeOptionsPageUI.AsmSim_Number_Of_Threads);
                changed = true;
            }
            if (Settings.Default.AsmSim_Number_Of_Steps != this._asmDudeOptionsPageUI.AsmSim_Number_Of_Steps)
            {
                sb.AppendLine("AsmSim_Number_Of_Steps=" + this._asmDudeOptionsPageUI.AsmSim_Number_Of_Steps);
                changed = true;
            }
            if (Settings.Default.AsmSim_64_Bits != this._asmDudeOptionsPageUI.AsmSim_64_Bits)
            {
                sb.AppendLine("AsmSim_64_Bits=" + this._asmDudeOptionsPageUI.AsmSim_64_Bits);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors)
            {
                sb.AppendLine("AsmSim_Show_Syntax_Errors=" + this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors)
            {
                sb.AppendLine("AsmSim_Decorate_Syntax_Errors=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined)
            {
                sb.AppendLine("AsmSim_Show_Usage_Of_Undefined=" + this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined)
            {
                sb.AppendLine("AsmSim_Decorate_Usage_Of_Undefined=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Redundant_Instructions != this._asmDudeOptionsPageUI.AsmSim_Show_Redundant_Instructions)
            {
                sb.AppendLine("AsmSim_Show_Redundant_Instructions=" + this._asmDudeOptionsPageUI.AsmSim_Show_Redundant_Instructions);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Redundant_Instructions != this._asmDudeOptionsPageUI.AsmSim_Decorate_Redundant_Instructions)
            {
                sb.AppendLine("AsmSim_Decorate_Redundant_Instructions=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Redundant_Instructions);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Unreachable_Instructions != this._asmDudeOptionsPageUI.AsmSim_Show_Unreachable_Instructions)
            {
                sb.AppendLine("AsmSim_Show_Unreachable_Instructions=" + this._asmDudeOptionsPageUI.AsmSim_Show_Unreachable_Instructions);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Unreachable_Instructions != this._asmDudeOptionsPageUI.AsmSim_Decorate_Unreachable_Instructions)
            {
                sb.AppendLine("AsmSim_Decorate_Unreachable_Instructions=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Unreachable_Instructions);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Registers != this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers)
            {
                sb.AppendLine("AsmSim_Decorate_Registers=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers);
                changed = true;
            }
            if (Settings.Default.AsmSim_Use_In_Code_Completion != this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion)
            {
                sb.AppendLine("AsmSim_Use_In_Code_Completion=" + this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Unimplemented != this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented)
            {
                sb.AppendLine("AsmSim_Decorate_Unimplemented=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented);
                changed = true;
            }
            if (Settings.Default.AsmSim_Pragma_Assume != this._asmDudeOptionsPageUI.AsmSim_Pragma_Assume)
            {
                sb.AppendLine("AsmSim_Pragma_Assume=" + this._asmDudeOptionsPageUI.AsmSim_Pragma_Assume);
                changed = true;
            }

            #endregion

            if (changed)
            {
                string title = null;

                string message = "Unsaved changes exist.\n\n"+ sb.ToString() + "\nWould you like to save?";
                int result = VsShellUtilities.ShowMessageBox(this.Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if (result == (int)VSConstants.MessageBoxResult.IDOK)
                {
                    Save();
                }
                else if (result == (int)VSConstants.MessageBoxResult.IDCANCEL)
                {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Handles "apply" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when VS wants to save the user's 
        /// changes (for example, when the user clicks OK in the dialog).
        /// </devdoc>
        protected override void OnApply(PageApplyEventArgs e)
        {
            Save();
            base.OnApply(e);
        }

        private void Save()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:save", this.ToString()));
            bool changed = false;
            bool restartNeeded = false;

            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                AsmDudeToolsStatic.Used_Assembler = this._asmDudeOptionsPageUI.UsedAssembler;
                changed = true;
                restartNeeded = true;
            }

            #region AsmDoc
            if (Settings.Default.AsmDoc_On != this._asmDudeOptionsPageUI.AsmDoc_On)
            {
                Settings.Default.AsmDoc_On = this._asmDudeOptionsPageUI.AsmDoc_On;
                changed = true;
            }
            if (Settings.Default.AsmDoc_url != this._asmDudeOptionsPageUI.AsmDoc_Url)
            {
                Settings.Default.AsmDoc_url = this._asmDudeOptionsPageUI.AsmDoc_Url;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region CodeFolding
            if (Settings.Default.CodeFolding_On != this._asmDudeOptionsPageUI.CodeFolding_On)
            {
                Settings.Default.CodeFolding_On = this._asmDudeOptionsPageUI.CodeFolding_On;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_IsDefaultCollapsed != this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped)
            {
                Settings.Default.CodeFolding_IsDefaultCollapsed = this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped;
                changed = true;
                restartNeeded = false;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._asmDudeOptionsPageUI.CodeFolding_BeginTag)
            {
                Settings.Default.CodeFolding_BeginTag = this._asmDudeOptionsPageUI.CodeFolding_BeginTag;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._asmDudeOptionsPageUI.CodeFolding_EndTag)
            {
                Settings.Default.CodeFolding_EndTag = this._asmDudeOptionsPageUI.CodeFolding_EndTag;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Syntax Highlighting
            if (Settings.Default.SyntaxHighlighting_On != this._asmDudeOptionsPageUI.SyntaxHighlighting_On)
            {
                Settings.Default.SyntaxHighlighting_On = this._asmDudeOptionsPageUI.SyntaxHighlighting_On;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.ColorMnemonic.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Opcode = this._asmDudeOptionsPageUI.ColorMnemonic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode_Italic != this._asmDudeOptionsPageUI.ColorMnemonic_Italic)
            {
                Settings.Default.SyntaxHighlighting_Opcode_Italic = this._asmDudeOptionsPageUI.ColorMnemonic_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.ColorRegister.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Register = this._asmDudeOptionsPageUI.ColorRegister;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register_Italic != this._asmDudeOptionsPageUI.ColorRegister_Italic)
            {
                Settings.Default.SyntaxHighlighting_Register_Italic = this._asmDudeOptionsPageUI.ColorRegister_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.ColorRemark.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Remark = this._asmDudeOptionsPageUI.ColorRemark;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark_Italic != this._asmDudeOptionsPageUI.ColorRemark_Italic)
            {
                Settings.Default.SyntaxHighlighting_Remark_Italic = this._asmDudeOptionsPageUI.ColorRemark_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.ColorDirective.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Directive = this._asmDudeOptionsPageUI.ColorDirective;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive_Italic != this._asmDudeOptionsPageUI.ColorDirective_Italic)
            {
                Settings.Default.SyntaxHighlighting_Directive_Italic = this._asmDudeOptionsPageUI.ColorDirective_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.ColorConstant.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Constant = this._asmDudeOptionsPageUI.ColorConstant;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant_Italic != this._asmDudeOptionsPageUI.ColorConstant_Italic)
            {
                Settings.Default.SyntaxHighlighting_Constant_Italic = this._asmDudeOptionsPageUI.ColorConstant_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.ColorJump.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Jump = this._asmDudeOptionsPageUI.ColorJump;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump_Italic != this._asmDudeOptionsPageUI.ColorJump_Italic)
            {
                Settings.Default.SyntaxHighlighting_Jump_Italic = this._asmDudeOptionsPageUI.ColorJump_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.ColorLabel.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Label = this._asmDudeOptionsPageUI.ColorLabel;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label_Italic != this._asmDudeOptionsPageUI.ColorLabel_Italic)
            {
                Settings.Default.SyntaxHighlighting_Label_Italic = this._asmDudeOptionsPageUI.ColorLabel_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.ColorMisc.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Misc = this._asmDudeOptionsPageUI.ColorMisc;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc_Italic != this._asmDudeOptionsPageUI.ColorMisc_Italic)
            {
                Settings.Default.SyntaxHighlighting_Misc_Italic = this._asmDudeOptionsPageUI.ColorMisc_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined1.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined1.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Userdefined1 = this._asmDudeOptionsPageUI.ColorUserDefined1;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined1_Italic != this._asmDudeOptionsPageUI.ColorUserDefined1_Italic)
            {
                Settings.Default.SyntaxHighlighting_Userdefined1_Italic = this._asmDudeOptionsPageUI.ColorUserDefined1_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined2.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined2.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Userdefined2 = this._asmDudeOptionsPageUI.ColorUserDefined2;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined2_Italic != this._asmDudeOptionsPageUI.ColorUserDefined2_Italic)
            {
                Settings.Default.SyntaxHighlighting_Userdefined2_Italic = this._asmDudeOptionsPageUI.ColorUserDefined2_Italic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined3.ToArgb() != this._asmDudeOptionsPageUI.ColorUserDefined3.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Userdefined3 = this._asmDudeOptionsPageUI.ColorUserDefined3;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Userdefined3_Italic != this._asmDudeOptionsPageUI.ColorUserDefined3_Italic)
            {
                Settings.Default.SyntaxHighlighting_Userdefined3_Italic = this._asmDudeOptionsPageUI.ColorUserDefined3_Italic;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Keyword Highlighting
            if (Settings.Default.KeywordHighlighting_BackgroundColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On)
            {
                Settings.Default.KeywordHighlighting_BackgroundColor_On = this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.KeywordHighlighting_BackgroundColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor.ToArgb())
            {
                Settings.Default.KeywordHighlighting_BackgroundColor = this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor_On)
            {
                Settings.Default.KeywordHighlighting_BorderColor_On = this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor_On;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor.ToArgb())
            {
                Settings.Default.KeywordHighlighting_BorderColor = this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Latency and Throughput Information (Performance Info)
            if (Settings.Default.PerformanceInfo_SandyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On)
            {
                Settings.Default.PerformanceInfo_SandyBridge_On = this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On;
                restartNeeded = Settings.Default.PerformanceInfo_SandyBridge_On = this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_IvyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On)
            {
                Settings.Default.PerformanceInfo_IvyBridge_On = this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On;
                restartNeeded = this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Haswell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On)
            {
                Settings.Default.PerformanceInfo_Haswell_On = this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On;
                restartNeeded = this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Broadwell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On)
            {
                Settings.Default.PerformanceInfo_Broadwell_On = this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On;
                restartNeeded = Settings.Default.PerformanceInfo_Broadwell_On = this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Skylake_On != this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On)
            {
                Settings.Default.PerformanceInfo_Skylake_On = this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On;
                restartNeeded = this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_KnightsLanding_On != this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On)
            {
                Settings.Default.PerformanceInfo_KnightsLanding_On = this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On;
                restartNeeded = this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On;
                changed = true;
            }
            #endregion

            #region Code Completion
            if (Settings.Default.CodeCompletion_On != this._asmDudeOptionsPageUI.UseCodeCompletion)
            {
                Settings.Default.CodeCompletion_On = this._asmDudeOptionsPageUI.UseCodeCompletion;
                changed = true;
            }
            if (Settings.Default.SignatureHelp_On != this._asmDudeOptionsPageUI.UseSignatureHelp)
            {
                Settings.Default.SignatureHelp_On = this._asmDudeOptionsPageUI.UseSignatureHelp;
                changed = true;
            }
            if (Settings.Default.ARCH_8086 != this._asmDudeOptionsPageUI.UseArch_8086)
            {
                Settings.Default.ARCH_8086 = this._asmDudeOptionsPageUI.UseArch_8086;
                changed = true;
            }
            if (Settings.Default.ARCH_186 != this._asmDudeOptionsPageUI.UseArch_186)
            {
                Settings.Default.ARCH_186 = this._asmDudeOptionsPageUI.UseArch_186;
                changed = true;
            }
            if (Settings.Default.ARCH_286 != this._asmDudeOptionsPageUI.UseArch_286)
            {
                Settings.Default.ARCH_286 = this._asmDudeOptionsPageUI.UseArch_286;
                changed = true;
            }
            if (Settings.Default.ARCH_386 != this._asmDudeOptionsPageUI.UseArch_386)
            {
                Settings.Default.ARCH_386 = this._asmDudeOptionsPageUI.UseArch_386;
                changed = true;
            }
            if (Settings.Default.ARCH_486 != this._asmDudeOptionsPageUI.UseArch_486)
            {
                Settings.Default.ARCH_486 = this._asmDudeOptionsPageUI.UseArch_486;
                changed = true;
            }
            if (Settings.Default.ARCH_MMX != this._asmDudeOptionsPageUI.UseArch_MMX)
            {
                Settings.Default.ARCH_MMX = this._asmDudeOptionsPageUI.UseArch_MMX;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE != this._asmDudeOptionsPageUI.UseArch_SSE)
            {
                Settings.Default.ARCH_SSE = this._asmDudeOptionsPageUI.UseArch_SSE;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE2 != this._asmDudeOptionsPageUI.UseArch_SSE2)
            {
                Settings.Default.ARCH_SSE2 = this._asmDudeOptionsPageUI.UseArch_SSE2;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE3 != this._asmDudeOptionsPageUI.UseArch_SSE3)
            {
                Settings.Default.ARCH_SSE3 = this._asmDudeOptionsPageUI.UseArch_SSE3;
                changed = true;
            }
            if (Settings.Default.ARCH_SSSE3 != this._asmDudeOptionsPageUI.UseArch_SSSE3)
            {
                Settings.Default.ARCH_SSSE3 = this._asmDudeOptionsPageUI.UseArch_SSSE3;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE41 != this._asmDudeOptionsPageUI.UseArch_SSE41)
            {
                Settings.Default.ARCH_SSE41 = this._asmDudeOptionsPageUI.UseArch_SSE41;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE42 != this._asmDudeOptionsPageUI.UseArch_SSE42)
            {
                Settings.Default.ARCH_SSE42 = this._asmDudeOptionsPageUI.UseArch_SSE42;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE4A != this._asmDudeOptionsPageUI.UseArch_SSE4A)
            {
                Settings.Default.ARCH_SSE4A = this._asmDudeOptionsPageUI.UseArch_SSE4A;
                changed = true;
            }
            if (Settings.Default.ARCH_SSE5 != this._asmDudeOptionsPageUI.UseArch_SSE5)
            {
                Settings.Default.ARCH_SSE5 = this._asmDudeOptionsPageUI.UseArch_SSE5;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX != this._asmDudeOptionsPageUI.UseArch_AVX)
            {
                Settings.Default.ARCH_AVX = this._asmDudeOptionsPageUI.UseArch_AVX;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX2 != this._asmDudeOptionsPageUI.UseArch_AVX2)
            {
                Settings.Default.ARCH_AVX2 = this._asmDudeOptionsPageUI.UseArch_AVX2;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512PF != this._asmDudeOptionsPageUI.UseArch_AVX512_PF)
            {
                Settings.Default.ARCH_AVX512PF = this._asmDudeOptionsPageUI.UseArch_AVX512_PF;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512VL != this._asmDudeOptionsPageUI.UseArch_AVX512_VL)
            {
                Settings.Default.ARCH_AVX512VL = this._asmDudeOptionsPageUI.UseArch_AVX512_VL;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512DQ != this._asmDudeOptionsPageUI.UseArch_AVX512_DQ)
            {
                Settings.Default.ARCH_AVX512DQ = this._asmDudeOptionsPageUI.UseArch_AVX512_DQ;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512BW != this._asmDudeOptionsPageUI.UseArch_AVX512_BW)
            {
                Settings.Default.ARCH_AVX512BW = this._asmDudeOptionsPageUI.UseArch_AVX512_BW;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512ER != this._asmDudeOptionsPageUI.UseArch_AVX512_ER)
            {
                Settings.Default.ARCH_AVX512ER = this._asmDudeOptionsPageUI.UseArch_AVX512_ER;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512F != this._asmDudeOptionsPageUI.UseArch_AVX512_F)
            {
                Settings.Default.ARCH_AVX512F = this._asmDudeOptionsPageUI.UseArch_AVX512_F;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512CD != this._asmDudeOptionsPageUI.UseArch_AVX512_CD)
            {
                Settings.Default.ARCH_AVX512CD = this._asmDudeOptionsPageUI.UseArch_AVX512_CD;
                changed = true;
            }

            if (Settings.Default.ARCH_AVX512_IFMA != this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA)
            {
                Settings.Default.ARCH_AVX512_IFMA = this._asmDudeOptionsPageUI.UseArch_AVX512_IFMA;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_VBMI != this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI)
            {
                Settings.Default.ARCH_AVX512_VBMI = this._asmDudeOptionsPageUI.UseArch_AVX512_VBMI;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_VPOPCNTDQ != this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ)
            {
                Settings.Default.ARCH_AVX512_VPOPCNTDQ = this._asmDudeOptionsPageUI.UseArch_AVX512_VPOPCNTDQ;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_4VNNIW != this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW)
            {
                Settings.Default.ARCH_AVX512_4VNNIW = this._asmDudeOptionsPageUI.UseArch_AVX512_4VNNIW;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512_4FMAPS != this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS)
            {
                Settings.Default.ARCH_AVX512_4FMAPS = this._asmDudeOptionsPageUI.UseArch_AVX512_4FMAPS;
                changed = true;
            }

            if (Settings.Default.ARCH_X64 != this._asmDudeOptionsPageUI.UseArch_X64)
            {
                Settings.Default.ARCH_X64 = this._asmDudeOptionsPageUI.UseArch_X64;
                changed = true;
            }
            if (Settings.Default.ARCH_BMI1 != this._asmDudeOptionsPageUI.UseArch_BMI1)
            {
                Settings.Default.ARCH_BMI1 = this._asmDudeOptionsPageUI.UseArch_BMI1;
                changed = true;
            }
            if (Settings.Default.ARCH_BMI2 != this._asmDudeOptionsPageUI.UseArch_BMI2)
            {
                Settings.Default.ARCH_BMI2 = this._asmDudeOptionsPageUI.UseArch_BMI2;
                changed = true;
            }
            if (Settings.Default.ARCH_P6 != this._asmDudeOptionsPageUI.UseArch_P6)
            {
                Settings.Default.ARCH_P6 = this._asmDudeOptionsPageUI.UseArch_P6;
                changed = true;
            }
            if (Settings.Default.ARCH_IA64 != this._asmDudeOptionsPageUI.UseArch_IA64)
            {
                Settings.Default.ARCH_IA64 = this._asmDudeOptionsPageUI.UseArch_IA64;
                changed = true;
            }
            if (Settings.Default.ARCH_FMA != this._asmDudeOptionsPageUI.UseArch_FMA)
            {
                Settings.Default.ARCH_FMA = this._asmDudeOptionsPageUI.UseArch_FMA;
                changed = true;
            }
            if (Settings.Default.ARCH_TBM != this._asmDudeOptionsPageUI.UseArch_TBM)
            {
                Settings.Default.ARCH_TBM = this._asmDudeOptionsPageUI.UseArch_TBM;
                changed = true;
            }
            if (Settings.Default.ARCH_AMD != this._asmDudeOptionsPageUI.UseArch_AMD)
            {
                Settings.Default.ARCH_AMD = this._asmDudeOptionsPageUI.UseArch_AMD;
                changed = true;
            }
            if (Settings.Default.ARCH_PENT != this._asmDudeOptionsPageUI.UseArch_PENT)
            {
                Settings.Default.ARCH_PENT = this._asmDudeOptionsPageUI.UseArch_PENT;
                changed = true;
            }
            if (Settings.Default.ARCH_3DNOW != this._asmDudeOptionsPageUI.UseArch_3DNOW)
            {
                Settings.Default.ARCH_3DNOW = this._asmDudeOptionsPageUI.UseArch_3DNOW;
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIX != this._asmDudeOptionsPageUI.UseArch_CYRIX)
            {
                Settings.Default.ARCH_CYRIX = this._asmDudeOptionsPageUI.UseArch_CYRIX;
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIXM != this._asmDudeOptionsPageUI.UseArch_CYRIXM)
            {
                Settings.Default.ARCH_CYRIXM = this._asmDudeOptionsPageUI.UseArch_CYRIXM;
                changed = true;
            }
            if (Settings.Default.ARCH_VMX != this._asmDudeOptionsPageUI.UseArch_VMX)
            {
                Settings.Default.ARCH_VMX = this._asmDudeOptionsPageUI.UseArch_VMX;
                changed = true;
            }
            if (Settings.Default.ARCH_RTM != this._asmDudeOptionsPageUI.UseArch_RTM)
            {
                Settings.Default.ARCH_RTM = this._asmDudeOptionsPageUI.UseArch_RTM;
                changed = true;
            }
            if (Settings.Default.ARCH_MPX != this._asmDudeOptionsPageUI.UseArch_MPX)
            {
                Settings.Default.ARCH_MPX = this._asmDudeOptionsPageUI.UseArch_MPX;
                changed = true;
            }
            if (Settings.Default.ARCH_SHA != this._asmDudeOptionsPageUI.UseArch_SHA)
            {
                Settings.Default.ARCH_SHA = this._asmDudeOptionsPageUI.UseArch_SHA;
                changed = true;
            }

            if (Settings.Default.ARCH_ADX != this._asmDudeOptionsPageUI.UseArch_ADX)
            {
                Settings.Default.ARCH_ADX = this._asmDudeOptionsPageUI.UseArch_ADX;
                changed = true;
            }
            if (Settings.Default.ARCH_F16C != this._asmDudeOptionsPageUI.UseArch_F16C)
            {
                Settings.Default.ARCH_F16C = this._asmDudeOptionsPageUI.UseArch_F16C;
                changed = true;
            }
            if (Settings.Default.ARCH_FSGSBASE != this._asmDudeOptionsPageUI.UseArch_FSGSBASE)
            {
                Settings.Default.ARCH_FSGSBASE = this._asmDudeOptionsPageUI.UseArch_FSGSBASE;
                changed = true;
            }
            if (Settings.Default.ARCH_HLE != this._asmDudeOptionsPageUI.UseArch_HLE)
            {
                Settings.Default.ARCH_HLE = this._asmDudeOptionsPageUI.UseArch_HLE;
                changed = true;
            }
            if (Settings.Default.ARCH_INVPCID != this._asmDudeOptionsPageUI.UseArch_INVPCID)
            {
                Settings.Default.ARCH_INVPCID = this._asmDudeOptionsPageUI.UseArch_INVPCID;
                changed = true;
            }
            if (Settings.Default.ARCH_PCLMULQDQ != this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ)
            {
                Settings.Default.ARCH_PCLMULQDQ = this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ;
                changed = true;
            }
            if (Settings.Default.ARCH_LZCNT != this._asmDudeOptionsPageUI.UseArch_LZCNT)
            {
                Settings.Default.ARCH_LZCNT = this._asmDudeOptionsPageUI.UseArch_LZCNT;
                changed = true;
            }
            if (Settings.Default.ARCH_PREFETCHWT1 != this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1)
            {
                Settings.Default.ARCH_PREFETCHWT1 = this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1;
                changed = true;
            }
            if (Settings.Default.ARCH_PRFCHW != this._asmDudeOptionsPageUI.UseArch_PREFETCHW)
            {
                Settings.Default.ARCH_PRFCHW = this._asmDudeOptionsPageUI.UseArch_PREFETCHW;
                changed = true;
            }
            if (Settings.Default.ARCH_RDPID != this._asmDudeOptionsPageUI.UseArch_RDPID)
            {
                Settings.Default.ARCH_RDPID = this._asmDudeOptionsPageUI.UseArch_RDPID;
                changed = true;
            }
            if (Settings.Default.ARCH_RDRAND != this._asmDudeOptionsPageUI.UseArch_RDRAND)
            {
                Settings.Default.ARCH_RDRAND = this._asmDudeOptionsPageUI.UseArch_RDRAND;
                changed = true;
            }
            if (Settings.Default.ARCH_RDSEED != this._asmDudeOptionsPageUI.UseArch_RDSEED)
            {
                Settings.Default.ARCH_RDSEED = this._asmDudeOptionsPageUI.UseArch_RDSEED;
                changed = true;
            }
            if (Settings.Default.ARCH_XSAVEOPT != this._asmDudeOptionsPageUI.UseArch_XSAVEOPT)
            {
                Settings.Default.ARCH_XSAVEOPT = this._asmDudeOptionsPageUI.UseArch_XSAVEOPT;
                changed = true;
            }
            if (Settings.Default.ARCH_UNDOC != this._asmDudeOptionsPageUI.UseArch_UNDOC)
            {
                Settings.Default.ARCH_UNDOC = this._asmDudeOptionsPageUI.UseArch_UNDOC;
                changed = true;
            }
            if (Settings.Default.ARCH_AES != this._asmDudeOptionsPageUI.UseArch_AES)
            {
                Settings.Default.ARCH_AES = this._asmDudeOptionsPageUI.UseArch_AES;
                changed = true;
            }
            #endregion

            #region Intellisense
            if (Settings.Default.IntelliSense_Label_Analysis_On != this._asmDudeOptionsPageUI.Intellisense_UseLabelAnalysis)
            {
                Settings.Default.IntelliSense_Label_Analysis_On = this._asmDudeOptionsPageUI.Intellisense_UseLabelAnalysis;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.IntelliSense_Show_UndefinedLabels != this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Labels)
            {
                Settings.Default.IntelliSense_Show_UndefinedLabels = this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_ClashingLabels != this._asmDudeOptionsPageUI.IntelliSense_Show_Clashing_Labels)
            {
                Settings.Default.IntelliSense_Show_ClashingLabels = this._asmDudeOptionsPageUI.IntelliSense_Show_Clashing_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_UndefinedLabels != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Labels)
            {
                Settings.Default.IntelliSense_Decorate_UndefinedLabels = this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_ClashingLabels != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Clashing_Labels)
            {
                Settings.Default.IntelliSense_Decorate_ClashingLabels = this._asmDudeOptionsPageUI.IntelliSense_Decorate_Clashing_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_Undefined_Includes != this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Includes)
            {
                Settings.Default.IntelliSense_Show_Undefined_Includes = this._asmDudeOptionsPageUI.IntelliSense_Show_Undefined_Includes;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_Undefined_Includes != this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Includes)
            {
                Settings.Default.IntelliSense_Decorate_Undefined_Includes = this._asmDudeOptionsPageUI.IntelliSense_Decorate_Undefined_Includes;
                changed = true;
            }
            #endregion

            #region AsmSim
            if (Settings.Default.AsmSim_On != this._asmDudeOptionsPageUI.AsmSim_On)
            {
                Settings.Default.AsmSim_On = this._asmDudeOptionsPageUI.AsmSim_On;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.AsmSim_Z3_Timeout_MS != this._asmDudeOptionsPageUI.AsmSim_Z3_Timeout_MS)
            {
                Settings.Default.AsmSim_Z3_Timeout_MS = this._asmDudeOptionsPageUI.AsmSim_Z3_Timeout_MS;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.AsmSim_Number_Of_Threads != this._asmDudeOptionsPageUI.AsmSim_Number_Of_Threads)
            {
                Settings.Default.AsmSim_Number_Of_Threads = this._asmDudeOptionsPageUI.AsmSim_Number_Of_Threads;
                changed = true;
            }
            if (Settings.Default.AsmSim_Number_Of_Steps != this._asmDudeOptionsPageUI.AsmSim_Number_Of_Steps)
            {
                Settings.Default.AsmSim_Number_Of_Steps = this._asmDudeOptionsPageUI.AsmSim_Number_Of_Steps;
                changed = true;
            }
            if (Settings.Default.AsmSim_64_Bits != this._asmDudeOptionsPageUI.AsmSim_64_Bits)
            {
                Settings.Default.AsmSim_64_Bits = this._asmDudeOptionsPageUI.AsmSim_64_Bits;
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors)
            {
                Settings.Default.AsmSim_Show_Syntax_Errors = this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors)
            {
                Settings.Default.AsmSim_Decorate_Syntax_Errors = this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors;
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined)
            {
                Settings.Default.AsmSim_Show_Usage_Of_Undefined = this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined)
            {
                Settings.Default.AsmSim_Decorate_Usage_Of_Undefined = this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined;
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Redundant_Instructions != this._asmDudeOptionsPageUI.AsmSim_Show_Redundant_Instructions)
            {
                Settings.Default.AsmSim_Show_Redundant_Instructions = this._asmDudeOptionsPageUI.AsmSim_Show_Redundant_Instructions;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Redundant_Instructions != this._asmDudeOptionsPageUI.AsmSim_Decorate_Redundant_Instructions)
            {
                Settings.Default.AsmSim_Decorate_Redundant_Instructions = this._asmDudeOptionsPageUI.AsmSim_Decorate_Redundant_Instructions;
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Unreachable_Instructions != this._asmDudeOptionsPageUI.AsmSim_Show_Unreachable_Instructions)
            {
                Settings.Default.AsmSim_Show_Unreachable_Instructions = this._asmDudeOptionsPageUI.AsmSim_Show_Unreachable_Instructions;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Unreachable_Instructions != this._asmDudeOptionsPageUI.AsmSim_Decorate_Unreachable_Instructions)
            {
                Settings.Default.AsmSim_Decorate_Unreachable_Instructions = this._asmDudeOptionsPageUI.AsmSim_Decorate_Unreachable_Instructions;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Registers != this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers)
            {
                Settings.Default.AsmSim_Decorate_Registers = this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers;
                changed = true;
            }
            if (Settings.Default.AsmSim_Use_In_Code_Completion != this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion)
            {
                Settings.Default.AsmSim_Use_In_Code_Completion = this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion;
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Unimplemented != this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented)
            {
                Settings.Default.AsmSim_Decorate_Unimplemented = this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented;
                changed = true;
            }
            if (Settings.Default.AsmSim_Pragma_Assume != this._asmDudeOptionsPageUI.AsmSim_Pragma_Assume)
            {
                Settings.Default.AsmSim_Pragma_Assume = this._asmDudeOptionsPageUI.AsmSim_Pragma_Assume;
                changed = true;
            }
            #endregion

            if (changed)
            {
                Settings.Default.Save();
            }
            if (restartNeeded)
            {
                string title = null;
                string message = "You may need to close and open assembly files, or \nrestart visual studio for the changes to take effect.";
                int result = VsShellUtilities.ShowMessageBox(this.Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        #endregion Event Handlers
    }
}
