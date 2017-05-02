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

        private const bool logInfo = false;
        private readonly IDictionary<string, bool> _boolOptions;

        private AsmDudeOptionsPageUI _asmDudeOptionsPageUI;

        public AsmDudeOptionsPage()
        {
            this._asmDudeOptionsPageUI = new AsmDudeOptionsPageUI();
            this._boolOptions = new Dictionary<string, bool>();
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
            this._asmDudeOptionsPageUI.UsedAssembler = AsmDudeToolsStatic.Used_Assembler;
            this._asmDudeOptionsPageUI.ColorMnemonic = Settings.Default.SyntaxHighlighting_Opcode;
            this._asmDudeOptionsPageUI.ColorRegister = Settings.Default.SyntaxHighlighting_Register;
            this._asmDudeOptionsPageUI.ColorRemark = Settings.Default.SyntaxHighlighting_Remark;
            this._asmDudeOptionsPageUI.ColorDirective = Settings.Default.SyntaxHighlighting_Directive;
            this._asmDudeOptionsPageUI.ColorConstant = Settings.Default.SyntaxHighlighting_Constant;
            this._asmDudeOptionsPageUI.ColorJump = Settings.Default.SyntaxHighlighting_Jump;
            this._asmDudeOptionsPageUI.ColorLabel = Settings.Default.SyntaxHighlighting_Label;
            this._asmDudeOptionsPageUI.ColorMisc = Settings.Default.SyntaxHighlighting_Misc;
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

            this._asmDudeOptionsPageUI.UseArch_AVX512F = Settings.Default.ARCH_AVX512F;
            this._asmDudeOptionsPageUI.useArch_AVX512F_UI.ToolTip = MakeToolTip(Arch.AVX512F);
            this._asmDudeOptionsPageUI.UseArch_AVX512VL = Settings.Default.ARCH_AVX512VL;
            this._asmDudeOptionsPageUI.useArch_AVX512VL_UI.ToolTip = MakeToolTip(Arch.AVX512VL);
            this._asmDudeOptionsPageUI.UseArch_AVX512DQ = Settings.Default.ARCH_AVX512DQ;
            this._asmDudeOptionsPageUI.useArch_AVX512DQ_UI.ToolTip = MakeToolTip(Arch.AVX512DQ);
            this._asmDudeOptionsPageUI.UseArch_AVX512BW = Settings.Default.ARCH_AVX512BW;
            this._asmDudeOptionsPageUI.useArch_AVX512BW_UI.ToolTip = MakeToolTip(Arch.AVX512BW);
            this._asmDudeOptionsPageUI.UseArch_AVX512ER = Settings.Default.ARCH_AVX512ER;
            this._asmDudeOptionsPageUI.useArch_AVX512ER_UI.ToolTip = MakeToolTip(Arch.AVX512ER);
            this._asmDudeOptionsPageUI.UseArch_AVX512CD = Settings.Default.ARCH_AVX512CD;
            this._asmDudeOptionsPageUI.useArch_AVX512CD_UI.ToolTip = MakeToolTip(Arch.AVX512CD);
            this._asmDudeOptionsPageUI.UseArch_AVX512PF = Settings.Default.ARCH_AVX512PF;
            this._asmDudeOptionsPageUI.useArch_AVX512PF_UI.ToolTip = MakeToolTip(Arch.AVX512PF);

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
            this._asmDudeOptionsPageUI.Intellisense_Show_Undefined_Labels = Settings.Default.IntelliSense_Show_UndefinedLabels;
            this._asmDudeOptionsPageUI.Intellisense_Show_Clashing_Labels = Settings.Default.IntelliSense_Show_ClashingLabels;
            this._asmDudeOptionsPageUI.Intellisense_Decorate_Undefined_Labels = Settings.Default.IntelliSense_Decorate_UndefinedLabels;
            this._asmDudeOptionsPageUI.Intellisense_Decorate_Clashing_Labels = Settings.Default.IntelliSense_Decorate_ClashingLabels;
            #endregion

            #region AsmSim
            this._asmDudeOptionsPageUI.AsmSim_On = Settings.Default.AsmSim_On;
            this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors = Settings.Default.AsmSim_Show_Syntax_Errors;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors = Settings.Default.AsmSim_Decorate_Syntax_Errors;
            this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined = Settings.Default.AsmSim_Show_Usage_Of_Undefined;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined = Settings.Default.AsmSim_Decorate_Usage_Of_Undefined;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers = Settings.Default.AsmSim_Decorate_Registers;
            this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion = Settings.Default.AsmSim_Use_In_Code_Completion;
            this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented = Settings.Default.AsmSim_Decorate_Unimplemented;
            #endregion
        }

        private string MakeToolTip(Arch arch)
        {
            MnemonicStore store = AsmDudeTools.Instance.Mnemonic_Store;
            ISet<Mnemonic> usedMnemonics = new HashSet<Mnemonic>();

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
            foreach (Mnemonic mnemonic in usedMnemonics)
            {
                sb.Append(mnemonic.ToString());
                sb.Append(", ");
            }
            sb.Length -= 2; // get rid of last comma.
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

            #region AsmDoc
            if (Settings.Default.AsmDoc_On != this._asmDudeOptionsPageUI.AsmDoc_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useAsmDoc=" + this._asmDudeOptionsPageUI.AsmDoc_On);
                changed = true;
            }
            if (Settings.Default.AsmDoc_url != this._asmDudeOptionsPageUI.AsmDoc_Url)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: asmDocUrl=" + this._asmDudeOptionsPageUI.AsmDoc_Url);
                changed = true;
            }
            #endregion

            #region CodeFolding
            if (Settings.Default.CodeFolding_On != this._asmDudeOptionsPageUI.CodeFolding_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeFolding=" + this._asmDudeOptionsPageUI.CodeFolding_On);
                changed = true;
            }
            if (Settings.Default.CodeFolding_IsDefaultCollapsed != this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: isDefaultCollaped=" + this._asmDudeOptionsPageUI.CodeFolding_IsDefaultCollaped);
                changed = true;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._asmDudeOptionsPageUI.CodeFolding_BeginTag)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: beginTag=" + this._asmDudeOptionsPageUI.CodeFolding_BeginTag);
                changed = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._asmDudeOptionsPageUI.CodeFolding_EndTag)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: endTag=" + this._asmDudeOptionsPageUI.CodeFolding_EndTag);
                changed = true;
            }
            #endregion

            #region Syntax Highlighting
            if (Settings.Default.SyntaxHighlighting_On != this._asmDudeOptionsPageUI.SyntaxHighlighting_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useSyntaxHighlighting=" + this._asmDudeOptionsPageUI.SyntaxHighlighting_On);
                changed = true;
            }
            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: usedAssembler=" + this._asmDudeOptionsPageUI.UsedAssembler);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.ColorMnemonic.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: stored=" + Settings.Default.SyntaxHighlighting_Opcode + "; new colorMnemonic=" + this._asmDudeOptionsPageUI.ColorMnemonic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.ColorRegister.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorRegister=" + this._asmDudeOptionsPageUI.ColorRegister);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.ColorRemark.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorRemark=" + this._asmDudeOptionsPageUI.ColorRemark);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.ColorDirective.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorDirective=" + this._asmDudeOptionsPageUI.ColorDirective);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.ColorConstant.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorConstant=" + this._asmDudeOptionsPageUI.ColorConstant);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.ColorJump.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorJump=" + this._asmDudeOptionsPageUI.ColorJump);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.ColorLabel.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorLabel=" + this._asmDudeOptionsPageUI.ColorLabel);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.ColorMisc.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorMisc=" + this._asmDudeOptionsPageUI.ColorMisc);
                changed = true;
            }
            #endregion

            #region Keyword Highlighting
            if (Settings.Default.KeywordHighlighting_BackgroundColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: KeywordHighlighting_BackgroundColor_On=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BackgroundColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: KeywordHighlighting_BackgroundColor=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor_On != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: KeywordHighlighting_BorderColor_On=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BackgroundColor_On);
                changed = true;
            }
            if (Settings.Default.KeywordHighlighting_BorderColor.ToArgb() != this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor.ToArgb())
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: KeywordHighlighting_BorderColor=" + this._asmDudeOptionsPageUI.KeywordHighlighting_BorderColor);
                changed = true;
            }
            #endregion

            #region Latency and Throughput Information (Performance Info)
            if (Settings.Default.PerformanceInfo_SandyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_SandyBridge_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_SandyBridge_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_IvyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_IvyBridge_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Haswell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_Haswell_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Broadwell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_Broadwell_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Skylake_On != this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_Skylake_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On);
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_KnightsLanding_On != this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: PerformanceInfo_KnightsLanding_On=" + this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On);
                changed = true;
            }
            #endregion

            #region Code Completion
            if (Settings.Default.CodeCompletion_On != this._asmDudeOptionsPageUI.UseCodeCompletion)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion=" + this._asmDudeOptionsPageUI.UseCodeCompletion);
                changed = true;
            }
            if (Settings.Default.SignatureHelp_On != this._asmDudeOptionsPageUI.UseSignatureHelp)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useSignatureHelp=" + this._asmDudeOptionsPageUI.UseSignatureHelp);
                changed = true;
            }

            if (Settings.Default.ARCH_8086 != this._asmDudeOptionsPageUI.UseArch_8086)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_8086=" + this._asmDudeOptionsPageUI.UseArch_8086);
                changed = true;
            }
            if (Settings.Default.ARCH_186 != this._asmDudeOptionsPageUI.UseArch_186)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_186=" + this._asmDudeOptionsPageUI.UseArch_186);
                changed = true;
            }
            if (Settings.Default.ARCH_286 != this._asmDudeOptionsPageUI.UseArch_286)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_286=" + this._asmDudeOptionsPageUI.UseArch_286);
                changed = true;
            }
            if (Settings.Default.ARCH_386 != this._asmDudeOptionsPageUI.UseArch_386)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_386=" + this._asmDudeOptionsPageUI.UseArch_386);
                changed = true;
            }
            if (Settings.Default.ARCH_486 != this._asmDudeOptionsPageUI.UseArch_486)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_486=" + this._asmDudeOptionsPageUI.UseArch_486);
                changed = true;
            }
            if (Settings.Default.ARCH_MMX != this._asmDudeOptionsPageUI.UseArch_MMX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_MMX=" + this._asmDudeOptionsPageUI.UseArch_MMX);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE != this._asmDudeOptionsPageUI.UseArch_SSE)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE=" + this._asmDudeOptionsPageUI.UseArch_SSE);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE2 != this._asmDudeOptionsPageUI.UseArch_SSE2)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE2=" + this._asmDudeOptionsPageUI.UseArch_SSE2);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE3 != this._asmDudeOptionsPageUI.UseArch_SSE3)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE3=" + this._asmDudeOptionsPageUI.UseArch_SSE3);
                changed = true;
            }
            if (Settings.Default.ARCH_SSSE3 != this._asmDudeOptionsPageUI.UseArch_SSSE3)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSSE3=" + this._asmDudeOptionsPageUI.UseArch_SSSE3);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE41 != this._asmDudeOptionsPageUI.UseArch_SSE41)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE41=" + this._asmDudeOptionsPageUI.UseArch_SSE41);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE42 != this._asmDudeOptionsPageUI.UseArch_SSE42)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE42=" + this._asmDudeOptionsPageUI.UseArch_SSE42);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE4A != this._asmDudeOptionsPageUI.UseArch_SSE4A)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE4A=" + this._asmDudeOptionsPageUI.UseArch_SSE4A);
                changed = true;
            }
            if (Settings.Default.ARCH_SSE5 != this._asmDudeOptionsPageUI.UseArch_SSE5)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SSE5=" + this._asmDudeOptionsPageUI.UseArch_SSE5);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX != this._asmDudeOptionsPageUI.UseArch_AVX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX=" + this._asmDudeOptionsPageUI.UseArch_AVX);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX2 != this._asmDudeOptionsPageUI.UseArch_AVX2)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX2=" + this._asmDudeOptionsPageUI.UseArch_AVX2);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512PF != this._asmDudeOptionsPageUI.UseArch_AVX512PF)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512=" + this._asmDudeOptionsPageUI.UseArch_AVX512PF);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512VL != this._asmDudeOptionsPageUI.UseArch_AVX512VL)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512VL=" + this._asmDudeOptionsPageUI.UseArch_AVX512VL);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512DQ != this._asmDudeOptionsPageUI.UseArch_AVX512DQ)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512DQ=" + this._asmDudeOptionsPageUI.UseArch_AVX512DQ);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512BW != this._asmDudeOptionsPageUI.UseArch_AVX512BW)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512BW=" + this._asmDudeOptionsPageUI.UseArch_AVX512BW);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512ER != this._asmDudeOptionsPageUI.UseArch_AVX512ER)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512ER=" + this._asmDudeOptionsPageUI.UseArch_AVX512ER);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512F != this._asmDudeOptionsPageUI.UseArch_AVX512F)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512PF=" + this._asmDudeOptionsPageUI.UseArch_AVX512F);
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512CD != this._asmDudeOptionsPageUI.UseArch_AVX512CD)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AVX512CD=" + this._asmDudeOptionsPageUI.UseArch_AVX512CD);
                changed = true;
            }
            if (Settings.Default.ARCH_X64 != this._asmDudeOptionsPageUI.UseArch_X64)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_X64=" + this._asmDudeOptionsPageUI.UseArch_X64);
                changed = true;
            }
            if (Settings.Default.ARCH_BMI1 != this._asmDudeOptionsPageUI.UseArch_BMI1)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_BMI1=" + this._asmDudeOptionsPageUI.UseArch_BMI1);
                changed = true;
            }
            if (Settings.Default.ARCH_BMI2 != this._asmDudeOptionsPageUI.UseArch_BMI2)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_BMI2=" + this._asmDudeOptionsPageUI.UseArch_BMI2);
                changed = true;
            }
            if (Settings.Default.ARCH_P6 != this._asmDudeOptionsPageUI.UseArch_P6)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_P6=" + this._asmDudeOptionsPageUI.UseArch_P6);
                changed = true;
            }
            if (Settings.Default.ARCH_IA64 != this._asmDudeOptionsPageUI.UseArch_IA64)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_IA64=" + this._asmDudeOptionsPageUI.UseArch_IA64);
                changed = true;
            }
            if (Settings.Default.ARCH_FMA != this._asmDudeOptionsPageUI.UseArch_FMA)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_FMA=" + this._asmDudeOptionsPageUI.UseArch_FMA);
                changed = true;
            }
            if (Settings.Default.ARCH_TBM != this._asmDudeOptionsPageUI.UseArch_TBM)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_TBM=" + this._asmDudeOptionsPageUI.UseArch_TBM);
                changed = true;
            }
            if (Settings.Default.ARCH_AMD != this._asmDudeOptionsPageUI.UseArch_AMD)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AMD=" + this._asmDudeOptionsPageUI.UseArch_AMD);
                changed = true;
            }
            if (Settings.Default.ARCH_PENT != this._asmDudeOptionsPageUI.UseArch_PENT)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_PENT=" + this._asmDudeOptionsPageUI.UseArch_PENT);
                changed = true;
            }
            if (Settings.Default.ARCH_3DNOW != this._asmDudeOptionsPageUI.UseArch_3DNOW)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_3DNOW=" + this._asmDudeOptionsPageUI.UseArch_3DNOW);
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIX != this._asmDudeOptionsPageUI.UseArch_CYRIX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_CYRIX=" + this._asmDudeOptionsPageUI.UseArch_CYRIX);
                changed = true;
            }
            if (Settings.Default.ARCH_CYRIXM != this._asmDudeOptionsPageUI.UseArch_CYRIXM)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_CYRIXM=" + this._asmDudeOptionsPageUI.UseArch_CYRIXM);
                changed = true;
            }
            if (Settings.Default.ARCH_VMX != this._asmDudeOptionsPageUI.UseArch_VMX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_VMX=" + this._asmDudeOptionsPageUI.UseArch_VMX);
                changed = true;
            }
            if (Settings.Default.ARCH_RTM != this._asmDudeOptionsPageUI.UseArch_RTM)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_RTM=" + this._asmDudeOptionsPageUI.UseArch_RTM);
                changed = true;
            }
            if (Settings.Default.ARCH_MPX != this._asmDudeOptionsPageUI.UseArch_MPX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_MPX=" + this._asmDudeOptionsPageUI.UseArch_MPX);
                changed = true;
            }
            if (Settings.Default.ARCH_SHA != this._asmDudeOptionsPageUI.UseArch_SHA)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_SHA=" + this._asmDudeOptionsPageUI.UseArch_SHA);
                changed = true;
            }

            if (Settings.Default.ARCH_ADX != this._asmDudeOptionsPageUI.UseArch_ADX)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_ADX=" + this._asmDudeOptionsPageUI.UseArch_ADX);
                changed = true;
            }
            if (Settings.Default.ARCH_F16C != this._asmDudeOptionsPageUI.UseArch_F16C)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_F16C=" + this._asmDudeOptionsPageUI.UseArch_F16C);
                changed = true;
            }
            if (Settings.Default.ARCH_FSGSBASE != this._asmDudeOptionsPageUI.UseArch_FSGSBASE)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_FSGSBASE=" + this._asmDudeOptionsPageUI.UseArch_FSGSBASE);
                changed = true;
            }
            if (Settings.Default.ARCH_HLE != this._asmDudeOptionsPageUI.UseArch_HLE)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_HLE=" + this._asmDudeOptionsPageUI.UseArch_HLE);
                changed = true;
            }
            if (Settings.Default.ARCH_INVPCID != this._asmDudeOptionsPageUI.UseArch_INVPCID)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_INVPCID=" + this._asmDudeOptionsPageUI.UseArch_INVPCID);
                changed = true;
            }
            if (Settings.Default.ARCH_PCLMULQDQ != this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_PCLMULQDQ=" + this._asmDudeOptionsPageUI.UseArch_PCLMULQDQ);
                changed = true;
            }
            if (Settings.Default.ARCH_LZCNT != this._asmDudeOptionsPageUI.UseArch_LZCNT)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_LZCNT=" + this._asmDudeOptionsPageUI.UseArch_LZCNT);
                changed = true;
            }
            if (Settings.Default.ARCH_PREFETCHWT1 != this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_PREFETCHWT1=" + this._asmDudeOptionsPageUI.UseArch_PREFETCHWT1);
                changed = true;
            }
            if (Settings.Default.ARCH_PRFCHW != this._asmDudeOptionsPageUI.UseArch_PREFETCHW)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_PREFETCHW=" + this._asmDudeOptionsPageUI.UseArch_PREFETCHW);
                changed = true;
            }
            if (Settings.Default.ARCH_RDPID != this._asmDudeOptionsPageUI.UseArch_RDPID)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_RDPID=" + this._asmDudeOptionsPageUI.UseArch_RDPID);
                changed = true;
            }
            if (Settings.Default.ARCH_RDRAND != this._asmDudeOptionsPageUI.UseArch_RDRAND)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_RDRAND=" + this._asmDudeOptionsPageUI.UseArch_RDRAND);
                changed = true;
            }
            if (Settings.Default.ARCH_RDSEED != this._asmDudeOptionsPageUI.UseArch_RDSEED)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_RDSEED=" + this._asmDudeOptionsPageUI.UseArch_RDSEED);
                changed = true;
            }
            if (Settings.Default.ARCH_XSAVEOPT != this._asmDudeOptionsPageUI.UseArch_XSAVEOPT)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_XSAVEOPT=" + this._asmDudeOptionsPageUI.UseArch_XSAVEOPT);
                changed = true;
            }
            if (Settings.Default.ARCH_UNDOC != this._asmDudeOptionsPageUI.UseArch_UNDOC)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_UNDOC=" + this._asmDudeOptionsPageUI.UseArch_UNDOC);
                changed = true;
            }
            if (Settings.Default.ARCH_AES != this._asmDudeOptionsPageUI.UseArch_AES)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useArch_AES=" + this._asmDudeOptionsPageUI.UseArch_AES);
                changed = true;
            }
            #endregion

            #region Intellisense
            if (Settings.Default.IntelliSense_Show_UndefinedLabels != this._asmDudeOptionsPageUI.Intellisense_Show_Undefined_Labels)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: showUndefinedLabels=" + this._asmDudeOptionsPageUI.Intellisense_Show_Undefined_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_ClashingLabels != this._asmDudeOptionsPageUI.Intellisense_Show_Clashing_Labels)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: showClashingLabels=" + this._asmDudeOptionsPageUI.Intellisense_Show_Clashing_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_UndefinedLabels != this._asmDudeOptionsPageUI.Intellisense_Decorate_Undefined_Labels)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: decorateUndefinedLabels=" + this._asmDudeOptionsPageUI.Intellisense_Decorate_Undefined_Labels);
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_ClashingLabels != this._asmDudeOptionsPageUI.Intellisense_Decorate_Clashing_Labels)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: decorateClashingLabels=" + this._asmDudeOptionsPageUI.Intellisense_Decorate_Clashing_Labels);
                changed = true;
            }
            #endregion

            #region AsmSim
            if (Settings.Default.AsmSim_On != this._asmDudeOptionsPageUI.AsmSim_On)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_On=" + this._asmDudeOptionsPageUI.AsmSim_On);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Show_Syntax_Errors=" + this._asmDudeOptionsPageUI.AsmSim_Show_Syntax_Errors);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Syntax_Errors != this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Decorate_Syntax_Errors=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Syntax_Errors);
                changed = true;
            }
            if (Settings.Default.AsmSim_Show_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Show_Usage_Of_Undefined=" + this._asmDudeOptionsPageUI.AsmSim_Show_Usage_Of_Undefined);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Usage_Of_Undefined != this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Decorate_Usage_Of_Undefined=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Usage_Of_Undefined);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Registers != this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Decorate_Registers=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Registers);
                changed = true;
            }
            if (Settings.Default.AsmSim_Use_In_Code_Completion != this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Use_In_Code_Completion=" + this._asmDudeOptionsPageUI.AsmSim_Use_In_Code_Completion);
                changed = true;
            }
            if (Settings.Default.AsmSim_Decorate_Unimplemented != this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented)
            {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: AsmSim_Decorate_Unimplemented=" + this._asmDudeOptionsPageUI.AsmSim_Decorate_Unimplemented);
                changed = true;
            }
            #endregion

            if (changed)
            {
                string title = null;
                string message = "Unsaved changes exist. Would you like to save.";
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
            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                AsmDudeToolsStatic.Used_Assembler = this._asmDudeOptionsPageUI.UsedAssembler;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.ColorMnemonic.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Opcode = this._asmDudeOptionsPageUI.ColorMnemonic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.ColorRegister.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Register = this._asmDudeOptionsPageUI.ColorRegister;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.ColorRemark.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Remark = this._asmDudeOptionsPageUI.ColorRemark;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.ColorDirective.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Directive = this._asmDudeOptionsPageUI.ColorDirective;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.ColorConstant.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Constant = this._asmDudeOptionsPageUI.ColorConstant;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.ColorJump.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Jump = this._asmDudeOptionsPageUI.ColorJump;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.ColorLabel.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Label = this._asmDudeOptionsPageUI.ColorLabel;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.ColorMisc.ToArgb())
            {
                Settings.Default.SyntaxHighlighting_Misc = this._asmDudeOptionsPageUI.ColorMisc;
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
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_IvyBridge_On != this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On)
            {
                Settings.Default.PerformanceInfo_IvyBridge_On = this._asmDudeOptionsPageUI.PerformanceInfo_IvyBridge_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Haswell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On)
            {
                Settings.Default.PerformanceInfo_Haswell_On = this._asmDudeOptionsPageUI.PerformanceInfo_Haswell_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Broadwell_On != this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On)
            {
                Settings.Default.PerformanceInfo_Broadwell_On = this._asmDudeOptionsPageUI.PerformanceInfo_Broadwell_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_Skylake_On != this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On)
            {
                Settings.Default.PerformanceInfo_Skylake_On = this._asmDudeOptionsPageUI.PerformanceInfo_Skylake_On;
                changed = true;
            }
            if (Settings.Default.PerformanceInfo_KnightsLanding_On != this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On)
            {
                Settings.Default.PerformanceInfo_KnightsLanding_On = this._asmDudeOptionsPageUI.PerformanceInfo_KnightsLanding_On;
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
            if (Settings.Default.ARCH_AVX512PF != this._asmDudeOptionsPageUI.UseArch_AVX512PF)
            {
                Settings.Default.ARCH_AVX512PF = this._asmDudeOptionsPageUI.UseArch_AVX512PF;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512VL != this._asmDudeOptionsPageUI.UseArch_AVX512VL)
            {
                Settings.Default.ARCH_AVX512VL = this._asmDudeOptionsPageUI.UseArch_AVX512VL;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512DQ != this._asmDudeOptionsPageUI.UseArch_AVX512DQ)
            {
                Settings.Default.ARCH_AVX512DQ = this._asmDudeOptionsPageUI.UseArch_AVX512DQ;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512BW != this._asmDudeOptionsPageUI.UseArch_AVX512BW)
            {
                Settings.Default.ARCH_AVX512BW = this._asmDudeOptionsPageUI.UseArch_AVX512BW;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512ER != this._asmDudeOptionsPageUI.UseArch_AVX512ER)
            {
                Settings.Default.ARCH_AVX512ER = this._asmDudeOptionsPageUI.UseArch_AVX512ER;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512F != this._asmDudeOptionsPageUI.UseArch_AVX512F)
            {
                Settings.Default.ARCH_AVX512F = this._asmDudeOptionsPageUI.UseArch_AVX512F;
                changed = true;
            }
            if (Settings.Default.ARCH_AVX512CD != this._asmDudeOptionsPageUI.UseArch_AVX512CD)
            {
                Settings.Default.ARCH_AVX512CD = this._asmDudeOptionsPageUI.UseArch_AVX512CD;
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
            if (Settings.Default.IntelliSense_Show_UndefinedLabels != this._asmDudeOptionsPageUI.Intellisense_Show_Undefined_Labels)
            {
                Settings.Default.IntelliSense_Show_UndefinedLabels = this._asmDudeOptionsPageUI.Intellisense_Show_Undefined_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Show_ClashingLabels != this._asmDudeOptionsPageUI.Intellisense_Show_Clashing_Labels)
            {
                Settings.Default.IntelliSense_Show_ClashingLabels = this._asmDudeOptionsPageUI.Intellisense_Show_Clashing_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_UndefinedLabels != this._asmDudeOptionsPageUI.Intellisense_Decorate_Undefined_Labels)
            {
                Settings.Default.IntelliSense_Decorate_UndefinedLabels = this._asmDudeOptionsPageUI.Intellisense_Decorate_Undefined_Labels;
                changed = true;
            }
            if (Settings.Default.IntelliSense_Decorate_ClashingLabels != this._asmDudeOptionsPageUI.Intellisense_Decorate_Clashing_Labels)
            {
                Settings.Default.IntelliSense_Decorate_ClashingLabels = this._asmDudeOptionsPageUI.Intellisense_Decorate_Clashing_Labels;
                changed = true;
            }
            #endregion

            #region AsmSim
            if (Settings.Default.AsmSim_On != this._asmDudeOptionsPageUI.AsmSim_On)
            {
                Settings.Default.AsmSim_On = this._asmDudeOptionsPageUI.AsmSim_On;
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
            #endregion


            if (changed)
            {
                Settings.Default.Save();
            }
            if (restartNeeded)
            {
                string title = null;
                string message = "You may need to restart visual studio for the changes to take effect.";
                int result = VsShellUtilities.ShowMessageBox(this.Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        #endregion Event Handlers
    }
}
