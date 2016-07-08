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

using AsmDude.Tools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace AsmDude.OptionsPage {

    [Guid(Guids.GuidOptionsPageAsmDude)]
    public class AsmDudeOptionsPage : UIElementDialogPage {

        private const bool logInfo = true;

        private AsmDudeOptionsPageUI _asmDudeOptionsPageUI;

        public AsmDudeOptionsPage() {
            this._asmDudeOptionsPageUI = new AsmDudeOptionsPageUI();
        }

        protected override System.Windows.UIElement Child {
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
        protected override void OnActivate(CancelEventArgs e) {
            base.OnActivate(e);

            #region AsmDoc
            this._asmDudeOptionsPageUI.useAsmDoc = Settings.Default.AsmDoc_On;
            this._asmDudeOptionsPageUI.asmDocUrl = Settings.Default.AsmDoc_url;
            #endregion

            #region CodeFolding
            this._asmDudeOptionsPageUI.useCodeFolding = Settings.Default.CodeFolding_On;
            this._asmDudeOptionsPageUI.isDefaultCollaped = Settings.Default.CodeFolding_IsDefaultCollapsed;
            this._asmDudeOptionsPageUI.beginTag = Settings.Default.CodeFolding_BeginTag;
            this._asmDudeOptionsPageUI.endTag = Settings.Default.CodeFolding_EndTag;
            #endregion

            #region Syntax Highlighting
            this._asmDudeOptionsPageUI.useSyntaxHighlighting = Settings.Default.SyntaxHighlighting_On;
            this._asmDudeOptionsPageUI.usedAssembler = AsmDudeToolsStatic.usedAssembler;
            this._asmDudeOptionsPageUI.colorMnemonic = Settings.Default.SyntaxHighlighting_Opcode;
            this._asmDudeOptionsPageUI.colorRegister = Settings.Default.SyntaxHighlighting_Register;
            this._asmDudeOptionsPageUI.colorRemark = Settings.Default.SyntaxHighlighting_Remark;
            this._asmDudeOptionsPageUI.colorDirective = Settings.Default.SyntaxHighlighting_Directive;
            this._asmDudeOptionsPageUI.colorConstant = Settings.Default.SyntaxHighlighting_Constant;
            this._asmDudeOptionsPageUI.colorJump = Settings.Default.SyntaxHighlighting_Jump;
            this._asmDudeOptionsPageUI.colorLabel = Settings.Default.SyntaxHighlighting_Label;
            this._asmDudeOptionsPageUI.colorMisc = Settings.Default.SyntaxHighlighting_Misc;
            #endregion

            #region Keyword Highlighting
            this._asmDudeOptionsPageUI.useCodeKeywordHighlighting = Settings.Default.KeywordHighlight_On;
            this._asmDudeOptionsPageUI.backgroundColor = Settings.Default.KeywordHighlightColor;
            #endregion

            #region Code Completion
            this._asmDudeOptionsPageUI.useCodeCompletion = Settings.Default.CodeCompletion_On;
            this._asmDudeOptionsPageUI.useSignatureHelp = Settings.Default.SignatureHelp_On;

            this._asmDudeOptionsPageUI.useArch_8086 = Settings.Default.ARCH_8086;
            this._asmDudeOptionsPageUI.useArch_186 = Settings.Default.ARCH_186;
            this._asmDudeOptionsPageUI.useArch_286 = Settings.Default.ARCH_286;
            this._asmDudeOptionsPageUI.useArch_386 = Settings.Default.ARCH_386;
            this._asmDudeOptionsPageUI.useArch_486 = Settings.Default.ARCH_486;
            this._asmDudeOptionsPageUI.useArch_MMX = Settings.Default.ARCH_MMX;
            this._asmDudeOptionsPageUI.useArch_SSE = Settings.Default.ARCH_SSE;
            this._asmDudeOptionsPageUI.useArch_SSE2 = Settings.Default.ARCH_SSE2;
            this._asmDudeOptionsPageUI.useArch_SSE3 = Settings.Default.ARCH_SSE3;
            this._asmDudeOptionsPageUI.useArch_SSSE3 = Settings.Default.ARCH_SSSE3;
            this._asmDudeOptionsPageUI.useArch_SSE41 = Settings.Default.ARCH_SSE41;
            this._asmDudeOptionsPageUI.useArch_SSE42 = Settings.Default.ARCH_SSE42;
            this._asmDudeOptionsPageUI.useArch_SSE4A = Settings.Default.ARCH_SSE4A;
            this._asmDudeOptionsPageUI.useArch_SSE5 = Settings.Default.ARCH_SSE5;
            this._asmDudeOptionsPageUI.useArch_AVX = Settings.Default.ARCH_AVX;
            this._asmDudeOptionsPageUI.useArch_AVX2 = Settings.Default.ARCH_AVX2;
            this._asmDudeOptionsPageUI.useArch_AVX512 = Settings.Default.ARCH_AVX512;

            this._asmDudeOptionsPageUI.useArch_AVX512VL = Settings.Default.ARCH_AVX512VL;
            this._asmDudeOptionsPageUI.useArch_AVX512DQ = Settings.Default.ARCH_AVX512DQ;
            this._asmDudeOptionsPageUI.useArch_AVX512BW = Settings.Default.ARCH_AVX512BW;
            this._asmDudeOptionsPageUI.useArch_AVX512ER = Settings.Default.ARCH_AVX512ER;
            this._asmDudeOptionsPageUI.useArch_AVX512PF = Settings.Default.ARCH_AVX512PF;
            this._asmDudeOptionsPageUI.useArch_AVX512CD = Settings.Default.ARCH_AVX512CD;
            this._asmDudeOptionsPageUI.useArch_AVX512VBMI = Settings.Default.ARCH_AVX512VBMI;
            this._asmDudeOptionsPageUI.useArch_AVX512IFMA = Settings.Default.ARCH_AVX512IFMA;

            this._asmDudeOptionsPageUI.useArch_X64 = Settings.Default.ARCH_X64;
            this._asmDudeOptionsPageUI.useArch_BMI1 = Settings.Default.ARCH_BMI1;
            this._asmDudeOptionsPageUI.useArch_BMI2 = Settings.Default.ARCH_BMI2;
            this._asmDudeOptionsPageUI.useArch_P6 = Settings.Default.ARCH_P6;
            this._asmDudeOptionsPageUI.useArch_X86_64 = Settings.Default.ARCH_X86_64;
            this._asmDudeOptionsPageUI.useArch_IA64 = Settings.Default.ARCH_IA64;
            this._asmDudeOptionsPageUI.useArch_FPU = Settings.Default.ARCH_FPU;
            this._asmDudeOptionsPageUI.useArch_FMA = Settings.Default.ARCH_FMA;
            this._asmDudeOptionsPageUI.useArch_TBM = Settings.Default.ARCH_TBM;
            this._asmDudeOptionsPageUI.useArch_AMD = Settings.Default.ARCH_AMD;
            this._asmDudeOptionsPageUI.useArch_PRIV = Settings.Default.ARCH_PRIV;
            this._asmDudeOptionsPageUI.useArch_PENT = Settings.Default.ARCH_PENT;
            this._asmDudeOptionsPageUI.useArch_PENTM = Settings.Default.ARCH_PENTM;
            this._asmDudeOptionsPageUI.useArch_NEHALEM = Settings.Default.ARCH_NEHALEM;
            this._asmDudeOptionsPageUI.useArch_WILLAMETTE = Settings.Default.ARCH_WILLAMETTE;
            this._asmDudeOptionsPageUI.useArch_PRESCOTT = Settings.Default.ARCH_PRESCOTT;
            this._asmDudeOptionsPageUI.useArch_WESTMERE = Settings.Default.ARCH_WESTMERE;
            this._asmDudeOptionsPageUI.useArch_SANDYBRIDGE = Settings.Default.ARCH_SANDYBRIDGE;
            this._asmDudeOptionsPageUI.useArch_KATMAI = Settings.Default.ARCH_KATMAI;
            this._asmDudeOptionsPageUI.useArch_FUTURE = Settings.Default.ARCH_FUTURE;
            this._asmDudeOptionsPageUI.useArch_OPT = Settings.Default.ARCH_OPT;
            this._asmDudeOptionsPageUI.useArch_NOHLE = Settings.Default.ARCH_NOHLE;
            this._asmDudeOptionsPageUI.useArch_3DNOW = Settings.Default.ARCH_3DNOW;
            this._asmDudeOptionsPageUI.useArch_PROT = Settings.Default.ARCH_PROT;
            this._asmDudeOptionsPageUI.useArch_CYRIX = Settings.Default.ARCH_CYRIX;
            this._asmDudeOptionsPageUI.useArch_INVPCID = Settings.Default.ARCH_INVPCID;
            this._asmDudeOptionsPageUI.useArch_CYRIXM = Settings.Default.ARCH_CYRIXM;
            this._asmDudeOptionsPageUI.useArch_VMX = Settings.Default.ARCH_VMX;
            this._asmDudeOptionsPageUI.useArch_RTM = Settings.Default.ARCH_RTM;
            this._asmDudeOptionsPageUI.useArch_HLE = Settings.Default.ARCH_HLE;
            this._asmDudeOptionsPageUI.useArch_MPX = Settings.Default.ARCH_MPX;
            this._asmDudeOptionsPageUI.useArch_MIB = Settings.Default.ARCH_MIB;
            this._asmDudeOptionsPageUI.useArch_SHA = Settings.Default.ARCH_SHA;
            #endregion

            #region Intellisense
            this._asmDudeOptionsPageUI.showUndefinedLabels = Settings.Default.IntelliSenseShowUndefinedLabels;
            this._asmDudeOptionsPageUI.showClashingLabels = Settings.Default.IntelliSenseShowClashingLabels;
            this._asmDudeOptionsPageUI.decorateUndefinedLabels = Settings.Default.IntelliSenseDecorateUndefinedLabels;
            this._asmDudeOptionsPageUI.decorateClashingLabels = Settings.Default.IntelliSenseDecorateClashingLabels;
            #endregion
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e) {}

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
        protected override void OnDeactivate(CancelEventArgs e) {
            bool changed = false;

            #region AsmDoc
            if (Settings.Default.AsmDoc_On != this._asmDudeOptionsPageUI.useAsmDoc) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useAsmDoc=" + this._asmDudeOptionsPageUI.useAsmDoc);
                changed = true;
            }
            if (Settings.Default.AsmDoc_url != this._asmDudeOptionsPageUI.asmDocUrl) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: asmDocUrl=" + this._asmDudeOptionsPageUI.asmDocUrl);
                changed = true;
            }
            #endregion
            
            #region CodeFolding
            if (Settings.Default.CodeFolding_On != this._asmDudeOptionsPageUI.useCodeFolding) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeFolding=" + this._asmDudeOptionsPageUI.useCodeFolding);
                changed = true;
            }
            if (Settings.Default.CodeFolding_IsDefaultCollapsed != this._asmDudeOptionsPageUI.isDefaultCollaped) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: isDefaultCollaped=" + this._asmDudeOptionsPageUI.isDefaultCollaped);
                changed = true;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._asmDudeOptionsPageUI.beginTag) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: beginTag=" + this._asmDudeOptionsPageUI.beginTag);
                changed = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._asmDudeOptionsPageUI.endTag) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: endTag=" + this._asmDudeOptionsPageUI.endTag);
                changed = true;
            }
            #endregion
            
            #region Syntax Highlighting
            if (Settings.Default.SyntaxHighlighting_On != this._asmDudeOptionsPageUI.useSyntaxHighlighting) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useSyntaxHighlighting=" + this._asmDudeOptionsPageUI.useSyntaxHighlighting);
                changed = true;
            }
            if (AsmDudeToolsStatic.usedAssembler != this._asmDudeOptionsPageUI.usedAssembler) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: usedAssembler=" + this._asmDudeOptionsPageUI.usedAssembler);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.colorMnemonic.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: stored="+ Settings.Default.SyntaxHighlighting_Opcode + "; new colorMnemonic=" + this._asmDudeOptionsPageUI.colorMnemonic);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.colorRegister.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorRegister=" + this._asmDudeOptionsPageUI.colorRegister);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.colorRemark.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorRemark=" + this._asmDudeOptionsPageUI.colorRemark);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.colorDirective.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorDirective=" + this._asmDudeOptionsPageUI.colorDirective);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.colorConstant.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorConstant=" + this._asmDudeOptionsPageUI.colorConstant);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.colorJump.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorJump=" + this._asmDudeOptionsPageUI.colorJump);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.colorLabel.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorLabel=" + this._asmDudeOptionsPageUI.colorLabel);
                changed = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.colorMisc.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: colorMisc=" + this._asmDudeOptionsPageUI.colorMisc);
                changed = true;
            }
            #endregion

            #region Keyword Highlighting
            if (Settings.Default.KeywordHighlight_On != this._asmDudeOptionsPageUI.useCodeKeywordHighlighting) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeKeywordHighlighting=" + this._asmDudeOptionsPageUI.useCodeKeywordHighlighting);
                changed = true;
            }
            if (Settings.Default.KeywordHighlightColor.ToArgb() != this._asmDudeOptionsPageUI.backgroundColor.ToArgb()) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: backgroundColor=" + this._asmDudeOptionsPageUI.backgroundColor);
                changed = true;
            }
            #endregion

            #region Code Completion
            if (Settings.Default.CodeCompletion_On != this._asmDudeOptionsPageUI.useCodeCompletion) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion=" + this._asmDudeOptionsPageUI.useCodeCompletion);
                changed = true;
            }
            #endregion

            #region Intellisense
            if (Settings.Default.IntelliSenseShowUndefinedLabels != this._asmDudeOptionsPageUI.showUndefinedLabels) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: showUndefinedLabels=" + this._asmDudeOptionsPageUI.showUndefinedLabels);
                changed = true;
            }
            if (Settings.Default.IntelliSenseShowClashingLabels != this._asmDudeOptionsPageUI.showClashingLabels) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: showClashingLabels=" + this._asmDudeOptionsPageUI.showClashingLabels);
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateUndefinedLabels != this._asmDudeOptionsPageUI.decorateUndefinedLabels) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: decorateUndefinedLabels=" + this._asmDudeOptionsPageUI.decorateUndefinedLabels);
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateClashingLabels != this._asmDudeOptionsPageUI.decorateClashingLabels) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: decorateClashingLabels=" + this._asmDudeOptionsPageUI.decorateClashingLabels);
                changed = true;
            }
            #endregion

            if (changed) {
                string title = null;
                string message = "Unsaved changes exist. Would you like to save.";
                int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if (result == (int)VSConstants.MessageBoxResult.IDOK) {
                    this.save();
                } else if (result == (int)VSConstants.MessageBoxResult.IDCANCEL) {
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
        protected override void OnApply(PageApplyEventArgs e) {
            this.save();
            base.OnApply(e);
        }

        private void save() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:save", this.ToString()));
            bool changed = false;
            bool restartNeeded = false;
            #region AsmDoc
            if (Settings.Default.AsmDoc_On != this._asmDudeOptionsPageUI.useAsmDoc) {
                Settings.Default.AsmDoc_On = this._asmDudeOptionsPageUI.useAsmDoc;
                changed = true;
            }
            if (Settings.Default.AsmDoc_url != this._asmDudeOptionsPageUI.asmDocUrl) {
                Settings.Default.AsmDoc_url = this._asmDudeOptionsPageUI.asmDocUrl;
                changed = true;
                restartNeeded = true;
            }
            #endregion
            
            #region CodeFolding
            if (Settings.Default.CodeFolding_On != this._asmDudeOptionsPageUI.useCodeFolding) {
                Settings.Default.CodeFolding_On = this._asmDudeOptionsPageUI.useCodeFolding;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_IsDefaultCollapsed != this._asmDudeOptionsPageUI.isDefaultCollaped) {
                Settings.Default.CodeFolding_IsDefaultCollapsed = this._asmDudeOptionsPageUI.isDefaultCollaped;
                changed = true;
                restartNeeded = false;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._asmDudeOptionsPageUI.beginTag) {
                Settings.Default.CodeFolding_BeginTag = this._asmDudeOptionsPageUI.beginTag;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._asmDudeOptionsPageUI.endTag) {
                Settings.Default.CodeFolding_EndTag = this._asmDudeOptionsPageUI.endTag;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Syntax Highlighting
            if (Settings.Default.SyntaxHighlighting_On != this._asmDudeOptionsPageUI.useSyntaxHighlighting) {
                Settings.Default.SyntaxHighlighting_On = this._asmDudeOptionsPageUI.useSyntaxHighlighting;
                changed = true;
                restartNeeded = true;
            }
            if (AsmDudeToolsStatic.usedAssembler != this._asmDudeOptionsPageUI.usedAssembler) {
                AsmDudeToolsStatic.usedAssembler = this._asmDudeOptionsPageUI.usedAssembler;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Opcode.ToArgb() != this._asmDudeOptionsPageUI.colorMnemonic.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Opcode = this._asmDudeOptionsPageUI.colorMnemonic;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Register.ToArgb() != this._asmDudeOptionsPageUI.colorRegister.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Register = this._asmDudeOptionsPageUI.colorRegister;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Remark.ToArgb() != this._asmDudeOptionsPageUI.colorRemark.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Remark = this._asmDudeOptionsPageUI.colorRemark;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Directive.ToArgb() != this._asmDudeOptionsPageUI.colorDirective.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Directive = this._asmDudeOptionsPageUI.colorDirective;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Constant.ToArgb() != this._asmDudeOptionsPageUI.colorConstant.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Constant = this._asmDudeOptionsPageUI.colorConstant;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Jump.ToArgb() != this._asmDudeOptionsPageUI.colorJump.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Jump = this._asmDudeOptionsPageUI.colorJump;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Label.ToArgb() != this._asmDudeOptionsPageUI.colorLabel.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Label = this._asmDudeOptionsPageUI.colorLabel;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.SyntaxHighlighting_Misc.ToArgb() != this._asmDudeOptionsPageUI.colorMisc.ToArgb()) {
                Settings.Default.SyntaxHighlighting_Misc = this._asmDudeOptionsPageUI.colorMisc;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Keyword Highlighting
            if (Settings.Default.KeywordHighlight_On != this._asmDudeOptionsPageUI.useCodeKeywordHighlighting) {
                Settings.Default.KeywordHighlight_On = this._asmDudeOptionsPageUI.useCodeKeywordHighlighting;
                changed = true;
            }
            if (Settings.Default.KeywordHighlightColor.ToArgb() != this._asmDudeOptionsPageUI.backgroundColor.ToArgb()) {
                Settings.Default.KeywordHighlightColor = this._asmDudeOptionsPageUI.backgroundColor;
                changed = true;
                restartNeeded = true;
            }
            #endregion

            #region Code Completion
            if (Settings.Default.CodeCompletion_On != this._asmDudeOptionsPageUI.useCodeCompletion) {
                Settings.Default.CodeCompletion_On = this._asmDudeOptionsPageUI.useCodeCompletion;
                changed = true;
            }
            #endregion

            #region Intellisense
            if (Settings.Default.IntelliSenseShowUndefinedLabels != this._asmDudeOptionsPageUI.showUndefinedLabels) {
                Settings.Default.IntelliSenseShowUndefinedLabels = this._asmDudeOptionsPageUI.showUndefinedLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseShowClashingLabels != this._asmDudeOptionsPageUI.showClashingLabels) {
                Settings.Default.IntelliSenseShowClashingLabels = this._asmDudeOptionsPageUI.showClashingLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateUndefinedLabels != this._asmDudeOptionsPageUI.decorateUndefinedLabels) {
                Settings.Default.IntelliSenseDecorateUndefinedLabels = this._asmDudeOptionsPageUI.decorateUndefinedLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateClashingLabels != this._asmDudeOptionsPageUI.decorateClashingLabels) {
                Settings.Default.IntelliSenseDecorateClashingLabels = this._asmDudeOptionsPageUI.decorateClashingLabels;
                changed = true;
            }
            #endregion

            if (changed) {
                Settings.Default.Save();
            }
            if (restartNeeded) {
                string title = null;
                string message = "You may need to restart visual studio for the changes to take effect.";
                int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        #endregion Event Handlers
    }
}
