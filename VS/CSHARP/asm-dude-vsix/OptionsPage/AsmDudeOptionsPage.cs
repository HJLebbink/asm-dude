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
            this._asmDudeOptionsPageUI.useCodeCompletion_x86 = Settings.Default.CodeCompletion_x86;
            this._asmDudeOptionsPageUI.useCodeCompletion_i686 = Settings.Default.CodeCompletion_i686;
            this._asmDudeOptionsPageUI.useCodeCompletion_MMX = Settings.Default.CodeCompletion_mmx;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSE = Settings.Default.CodeCompletion_sse;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSE2 = Settings.Default.CodeCompletion_sse2;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSE3 = Settings.Default.CodeCompletion_sse3;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSSE3 = Settings.Default.CodeCompletion_ssse3;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSE41 = Settings.Default.CodeCompletion_sse41;
            this._asmDudeOptionsPageUI.useCodeCompletion_SSE42 = Settings.Default.CodeCompletion_sse42;
            this._asmDudeOptionsPageUI.useCodeCompletion_AVX = Settings.Default.CodeCompletion_avx;
            this._asmDudeOptionsPageUI.useCodeCompletion_AVX2 = Settings.Default.CodeCompletion_avx2;
            this._asmDudeOptionsPageUI.useCodeCompletion_KNC = Settings.Default.CodeCompletion_knc;
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
            if (Settings.Default.CodeCompletion_x86 != this._asmDudeOptionsPageUI.useCodeCompletion_x86) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_x86=" + this._asmDudeOptionsPageUI.useCodeCompletion_x86);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_i686 != this._asmDudeOptionsPageUI.useCodeCompletion_i686) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_i686=" + this._asmDudeOptionsPageUI.useCodeCompletion_i686);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_mmx != this._asmDudeOptionsPageUI.useCodeCompletion_MMX) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_MMX=" + this._asmDudeOptionsPageUI.useCodeCompletion_MMX);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_sse != this._asmDudeOptionsPageUI.useCodeCompletion_SSE) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSE=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSE);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_sse2 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE2) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSE2=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSE2);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_sse3 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE3) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSE3=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSE3);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_ssse3 != this._asmDudeOptionsPageUI.useCodeCompletion_SSSE3) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSSE3=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSSE3);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_sse41 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE41) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSE41=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSE41);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_sse42 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE42) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_SSE42=" + this._asmDudeOptionsPageUI.useCodeCompletion_SSE42);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_avx != this._asmDudeOptionsPageUI.useCodeCompletion_AVX) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_AVX=" + this._asmDudeOptionsPageUI.useCodeCompletion_AVX);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_avx2 != this._asmDudeOptionsPageUI.useCodeCompletion_AVX2) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_AVX2=" + this._asmDudeOptionsPageUI.useCodeCompletion_AVX2);
                changed = true;
            }
            if (Settings.Default.CodeCompletion_knc != this._asmDudeOptionsPageUI.useCodeCompletion_KNC) {
                if (logInfo) AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPage: OnDeactivate: change detected: useCodeCompletion_KNC=" + this._asmDudeOptionsPageUI.useCodeCompletion_KNC);
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
            if (Settings.Default.CodeCompletion_x86 != this._asmDudeOptionsPageUI.useCodeCompletion_x86) {
                Settings.Default.CodeCompletion_x86 = this._asmDudeOptionsPageUI.useCodeCompletion_x86;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_i686 != this._asmDudeOptionsPageUI.useCodeCompletion_i686) {
                Settings.Default.CodeCompletion_i686 = this._asmDudeOptionsPageUI.useCodeCompletion_i686;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_mmx != this._asmDudeOptionsPageUI.useCodeCompletion_MMX) {
                Settings.Default.CodeCompletion_mmx = this._asmDudeOptionsPageUI.useCodeCompletion_MMX;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_sse != this._asmDudeOptionsPageUI.useCodeCompletion_SSE) {
                Settings.Default.CodeCompletion_sse = this._asmDudeOptionsPageUI.useCodeCompletion_SSE;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_sse2 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE2) {
                Settings.Default.CodeCompletion_sse2 = this._asmDudeOptionsPageUI.useCodeCompletion_SSE2;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_sse3 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE3) {
                Settings.Default.CodeCompletion_sse3 = this._asmDudeOptionsPageUI.useCodeCompletion_SSE3;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_ssse3 != this._asmDudeOptionsPageUI.useCodeCompletion_SSSE3) {
                Settings.Default.CodeCompletion_ssse3 = this._asmDudeOptionsPageUI.useCodeCompletion_SSSE3;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_sse41 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE41) {
                Settings.Default.CodeCompletion_sse41 = this._asmDudeOptionsPageUI.useCodeCompletion_SSE41;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_sse42 != this._asmDudeOptionsPageUI.useCodeCompletion_SSE42) {
                Settings.Default.CodeCompletion_sse42 = this._asmDudeOptionsPageUI.useCodeCompletion_SSE42;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_avx != this._asmDudeOptionsPageUI.useCodeCompletion_AVX) {
                Settings.Default.CodeCompletion_avx = this._asmDudeOptionsPageUI.useCodeCompletion_AVX;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_avx2 != this._asmDudeOptionsPageUI.useCodeCompletion_AVX2) {
                Settings.Default.CodeCompletion_avx2 = this._asmDudeOptionsPageUI.useCodeCompletion_AVX2;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeCompletion_knc != this._asmDudeOptionsPageUI.useCodeCompletion_KNC) {
                Settings.Default.CodeCompletion_knc = this._asmDudeOptionsPageUI.useCodeCompletion_KNC;
                changed = true;
                restartNeeded = true;
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
