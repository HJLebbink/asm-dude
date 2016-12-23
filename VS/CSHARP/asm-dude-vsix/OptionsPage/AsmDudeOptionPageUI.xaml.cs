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
using AsmTools;
using System.Windows.Controls;

namespace AsmDude.OptionsPage {
    /// <summary>
    /// Interaction logic for AsmDudeOptionPageUI.xaml
    /// </summary>
    public partial class AsmDudeOptionsPageUI : UserControl {

        public AsmDudeOptionsPageUI() {
            InitializeComponent();
            this.version_UI.Content = "Asm Dude v" + typeof(AsmDudePackage).Assembly.GetName().Version.ToString() + " (" + ApplicationInformation.CompileDate.ToString()+")";
        }

        #region Asm Documentation
        public bool useAsmDoc {
            get { return (this.useAsmDoc_UI.IsChecked.HasValue) ? this.useAsmDoc_UI.IsChecked.Value : false; }
            set { this.useAsmDoc_UI.IsChecked = value; }
        }

        public string asmDocUrl {
            get { return this.asmDocUrl_UI.Text; }
            set { this.asmDocUrl_UI.Text = value; }
        }
        #endregion Asm Documentation

        #region Code Folding
        public bool useCodeFolding {
            get { return (this.useCodeFolding_UI.IsChecked.HasValue) ? this.useCodeFolding_UI.IsChecked.Value : false; }
            set { this.useCodeFolding_UI.IsChecked = value; }
        }

        public bool isDefaultCollaped {
            get { return (this.isDefaultCollaped_UI.IsChecked.HasValue) ? this.isDefaultCollaped_UI.IsChecked.Value : false; }
            set { this.isDefaultCollaped_UI.IsChecked = value; }
        }

        public string beginTag {
            get { return this.beginTag_UI.Text; }
            set { this.beginTag_UI.Text = value; }
        }

        public string endTag {
            get { return this.endTag_UI.Text; }
            set { this.endTag_UI.Text = value; }
        }
        #endregion Code Folding

        #region Syntax Highlighting

        public bool useSyntaxHighlighting {
            get { return (this.useSyntaxHighlighting_UI.IsChecked.HasValue) ? this.useSyntaxHighlighting_UI.IsChecked.Value : false; }
            set { this.useSyntaxHighlighting_UI.IsChecked = value; }
        }

        public AssemblerEnum usedAssembler {
            get {
                if (this.usedAssemblerMasm_UI.IsChecked.HasValue && this.usedAssemblerMasm_UI.IsChecked.Value) return AssemblerEnum.MASM;
                if (this.usedAssemblerNasm_UI.IsChecked.HasValue && this.usedAssemblerNasm_UI.IsChecked.Value) return AssemblerEnum.NASM;
                return AssemblerEnum.MASM;
            }
            set {
                switch (value) {
                    case AssemblerEnum.MASM:
                    this.usedAssemblerMasm_UI.IsChecked = true;
                    this.usedAssemblerNasm_UI.IsChecked = false;
                        break;
                    case AssemblerEnum.NASM:
                    this.usedAssemblerMasm_UI.IsChecked = false;
                    this.usedAssemblerNasm_UI.IsChecked = true;
                        break;
                }
            }
        }

        public System.Drawing.Color colorMnemonic {
            get {
                if (this.colorMnemonic_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorMnemonic_UI.SelectedColor.Value);
                } else {
                    //AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPageUI.xaml: colorMnemonic_UI has no value, assuming BLUE");
                    return System.Drawing.Color.Blue;
                }
            }
            set { this.colorMnemonic_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorRegister {
            get {
                if (this.colorRegister_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorRegister_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkRed;
                }
            }
            set { this.colorRegister_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorRemark {
            get {
                if (this.colorRemark_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorRemark_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Green;
                }
            }
            set { this.colorRemark_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorDirective {
            get {
                if (this.colorDirective_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorDirective_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Magenta;
                }
            }
            set { this.colorDirective_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorConstant {
            get {
                if (this.colorConstant_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorConstant_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Chocolate;
                }
            }
            set { this.colorConstant_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorJump {
            get {
                if (this.colorJump_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorJump_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Blue;
                }
            }
            set { this.colorJump_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorLabel {
            get {
                if (this.colorLabel_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorLabel_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.OrangeRed;
                }
            }
            set { this.colorLabel_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }

        public System.Drawing.Color colorMisc {
            get {
                if (this.colorMisc_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.colorMisc_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkOrange;
                }
            }
            set { this.colorMisc_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }
        #endregion Syntax Highlighting

        #region Keyword Highlighting
        public bool useKeywordHighlighting {
            get { return (this.useKeywordHighlighting_UI.IsChecked.HasValue) ? this.useKeywordHighlighting_UI.IsChecked.Value : false; }
            set { this.useKeywordHighlighting_UI.IsChecked = value; }
        }
        public System.Drawing.Color _backgroundColor { get; set; }

        public System.Drawing.Color backgroundColor {
            get {
                if (this.backgroundColor_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.Convert_Color(this.backgroundColor_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Cyan;
                }
            }
            set { this.backgroundColor_UI.SelectedColor = AsmDudeToolsStatic.Convert_Color(value); }
        }
        #endregion

        #region Code Completion
        public bool useCodeCompletion {
            get { return (this.useCodeCompletion_UI.IsChecked.HasValue) ? this.useCodeCompletion_UI.IsChecked.Value : false; }
            set { this.useCodeCompletion_UI.IsChecked = value; }
        }
        public bool useSignatureHelp {
            get { return (this.useSignatureHelp_UI.IsChecked.HasValue) ? this.useSignatureHelp_UI.IsChecked.Value : false; }
            set { this.useSignatureHelp_UI.IsChecked = value; }
        }

        public bool useArch_8086 {
            get { return (this.useArch_8086_UI.IsChecked.HasValue) ? this.useArch_8086_UI.IsChecked.Value : false; }
            set { this.useArch_8086_UI.IsChecked = value; }
        }
        public bool useArch_186 {
            get { return (this.useArch_186_UI.IsChecked.HasValue) ? this.useArch_186_UI.IsChecked.Value : false; }
            set { this.useArch_186_UI.IsChecked = value; }
        }
        public bool useArch_286 {
            get { return (this.useArch_286_UI.IsChecked.HasValue) ? this.useArch_286_UI.IsChecked.Value : false; }
            set { this.useArch_286_UI.IsChecked = value; }
        }
        public bool useArch_386 {
            get { return (this.useArch_386_UI.IsChecked.HasValue) ? this.useArch_386_UI.IsChecked.Value : false; }
            set { this.useArch_386_UI.IsChecked = value; }
        }
        public bool useArch_486 {
            get { return (this.useArch_486_UI.IsChecked.HasValue) ? this.useArch_486_UI.IsChecked.Value : false; }
            set { this.useArch_486_UI.IsChecked = value; }
        }
        public bool useArch_MMX {
            get { return (this.useArch_MMX_UI.IsChecked.HasValue) ? this.useArch_MMX_UI.IsChecked.Value : false; }
            set { this.useArch_MMX_UI.IsChecked = value; }
        }
        public bool useArch_SSE {
            get { return (this.useArch_SSE_UI.IsChecked.HasValue) ? this.useArch_SSE_UI.IsChecked.Value : false; }
            set { this.useArch_SSE_UI.IsChecked = value; }
        }
        public bool useArch_SSE2 {
            get { return (this.useArch_SSE2_UI.IsChecked.HasValue) ? this.useArch_SSE2_UI.IsChecked.Value : false; }
            set { this.useArch_SSE2_UI.IsChecked = value; }
        }
        public bool useArch_SSE3 {
            get { return (this.useArch_SSE3_UI.IsChecked.HasValue) ? this.useArch_SSE3_UI.IsChecked.Value : false; }
            set { this.useArch_SSE3_UI.IsChecked = value; }
        }
        public bool useArch_SSSE3 {
            get { return (this.useArch_SSSE3_UI.IsChecked.HasValue) ? this.useArch_SSSE3_UI.IsChecked.Value : false; }
            set { this.useArch_SSSE3_UI.IsChecked = value; }
        }
        public bool useArch_SSE41 {
            get { return (this.useArch_SSE41_UI.IsChecked.HasValue) ? this.useArch_SSE41_UI.IsChecked.Value : false; }
            set { this.useArch_SSE41_UI.IsChecked = value; }
        }
        public bool useArch_SSE42 {
            get { return (this.useArch_SSE42_UI.IsChecked.HasValue) ? this.useArch_SSE42_UI.IsChecked.Value : false; }
            set { this.useArch_SSE42_UI.IsChecked = value; }
        }
        public bool useArch_SSE4A {
            get { return (this.useArch_SSE4A_UI.IsChecked.HasValue) ? this.useArch_SSE4A_UI.IsChecked.Value : false; }
            set { this.useArch_SSE4A_UI.IsChecked = value; }
        }

        public bool useArch_SSE5 {
            get { return (this.useArch_SSE5_UI.IsChecked.HasValue) ? this.useArch_SSE5_UI.IsChecked.Value : false; }
            set { this.useArch_SSE5_UI.IsChecked = value; }
        }
        public bool useArch_AVX {
            get { return (this.useArch_AVX_UI.IsChecked.HasValue) ? this.useArch_AVX_UI.IsChecked.Value : false; }
            set { this.useArch_AVX_UI.IsChecked = value; }
        }
        public bool useArch_AVX2 {
            get { return (this.useArch_AVX2_UI.IsChecked.HasValue) ? this.useArch_AVX2_UI.IsChecked.Value : false; }
            set { this.useArch_AVX2_UI.IsChecked = value; }
        }
        public bool useArch_AVX512PF {
            get { return (this.useArch_AVX512PF_UI.IsChecked.HasValue) ? this.useArch_AVX512PF_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512PF_UI.IsChecked = value; }
        }
        public bool useArch_AVX512VL {
            get { return (this.useArch_AVX512VL_UI.IsChecked.HasValue) ? this.useArch_AVX512VL_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512VL_UI.IsChecked = value; }
        }
        public bool useArch_AVX512DQ {
            get { return (this.useArch_AVX512DQ_UI.IsChecked.HasValue) ? this.useArch_AVX512DQ_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512DQ_UI.IsChecked = value; }
        }
        public bool useArch_AVX512BW {
            get { return (this.useArch_AVX512BW_UI.IsChecked.HasValue) ? this.useArch_AVX512BW_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512BW_UI.IsChecked = value; }
        }

        public bool useArch_AVX512ER {
            get { return (this.useArch_AVX512ER_UI.IsChecked.HasValue) ? this.useArch_AVX512ER_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512ER_UI.IsChecked = value; }
        }
        public bool useArch_AVX512F {
            get { return (this.useArch_AVX512F_UI.IsChecked.HasValue) ? this.useArch_AVX512F_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512F_UI.IsChecked = value; }
        }
        public bool useArch_AVX512CD {
            get { return (this.useArch_AVX512CD_UI.IsChecked.HasValue) ? this.useArch_AVX512CD_UI.IsChecked.Value : false; }
            set { this.useArch_AVX512CD_UI.IsChecked = value; }
        }
        public bool useArch_X64 {
            get { return (this.useArch_X64_UI.IsChecked.HasValue) ? this.useArch_X64_UI.IsChecked.Value : false; }
            set { this.useArch_X64_UI.IsChecked = value; }
        }
        public bool useArch_BMI1 {
            get { return (this.useArch_BMI1_UI.IsChecked.HasValue) ? this.useArch_BMI1_UI.IsChecked.Value : false; }
            set { this.useArch_BMI1_UI.IsChecked = value; }
        }
        public bool useArch_BMI2 {
            get { return (this.useArch_BMI2_UI.IsChecked.HasValue) ? this.useArch_BMI2_UI.IsChecked.Value : false; }
            set { this.useArch_BMI2_UI.IsChecked = value; }
        }
        public bool useArch_P6 {
            get { return (this.useArch_P6_UI.IsChecked.HasValue) ? this.useArch_P6_UI.IsChecked.Value : false; }
            set { this.useArch_P6_UI.IsChecked = value; }
        }
        public bool useArch_IA64 {
            get { return (this.useArch_IA64_UI.IsChecked.HasValue) ? this.useArch_IA64_UI.IsChecked.Value : false; }
            set { this.useArch_IA64_UI.IsChecked = value; }
        }
        public bool useArch_FMA {
            get { return (this.useArch_FMA_UI.IsChecked.HasValue) ? this.useArch_FMA_UI.IsChecked.Value : false; }
            set { this.useArch_FMA_UI.IsChecked = value; }
        }
        public bool useArch_TBM {
            get { return (this.useArch_TBM_UI.IsChecked.HasValue) ? this.useArch_TBM_UI.IsChecked.Value : false; }
            set { this.useArch_TBM_UI.IsChecked = value; }
        }
        public bool useArch_AMD {
            get { return (this.useArch_AMD_UI.IsChecked.HasValue) ? this.useArch_AMD_UI.IsChecked.Value : false; }
            set { this.useArch_AMD_UI.IsChecked = value; }
        }
        public bool useArch_PENT {
            get { return (this.useArch_PENT_UI.IsChecked.HasValue) ? this.useArch_PENT_UI.IsChecked.Value : false; }
            set { this.useArch_PENT_UI.IsChecked = value; }
        }
         public bool useArch_3DNOW {
            get { return (this.useArch_3DNOW_UI.IsChecked.HasValue) ? this.useArch_3DNOW_UI.IsChecked.Value : false; }
            set { this.useArch_3DNOW_UI.IsChecked = value; }
        }
        public bool useArch_CYRIX {
            get { return (this.useArch_CYRIX_UI.IsChecked.HasValue) ? this.useArch_CYRIX_UI.IsChecked.Value : false; }
            set { this.useArch_CYRIX_UI.IsChecked = value; }
        }
        public bool useArch_CYRIXM {
            get { return (this.useArch_CYRIXM_UI.IsChecked.HasValue) ? this.useArch_CYRIXM_UI.IsChecked.Value : false; }
            set { this.useArch_CYRIXM_UI.IsChecked = value; }
        }
        public bool useArch_VMX {
            get { return (this.useArch_VMX_UI.IsChecked.HasValue) ? this.useArch_VMX_UI.IsChecked.Value : false; }
            set { this.useArch_VMX_UI.IsChecked = value; }
        }
        public bool useArch_RTM {
            get { return (this.useArch_RTM_UI.IsChecked.HasValue) ? this.useArch_RTM_UI.IsChecked.Value : false; }
            set { this.useArch_RTM_UI.IsChecked = value; }
        }
        public bool useArch_MPX {
            get { return (this.useArch_MPX_UI.IsChecked.HasValue) ? this.useArch_MPX_UI.IsChecked.Value : false; }
            set { this.useArch_MPX_UI.IsChecked = value; }
        }
        public bool useArch_SHA {
            get { return (this.useArch_SHA_UI.IsChecked.HasValue) ? this.useArch_SHA_UI.IsChecked.Value : false; }
            set { this.useArch_SHA_UI.IsChecked = value; }
        }

        public bool useArch_ADX {
            get { return (this.useArch_ADX_UI.IsChecked.HasValue) ? this.useArch_ADX_UI.IsChecked.Value : false; }
            set { this.useArch_ADX_UI.IsChecked = value; }
        }
        public bool useArch_F16C {
            get { return (this.useArch_F16C_UI.IsChecked.HasValue) ? this.useArch_F16C_UI.IsChecked.Value : false; }
            set { this.useArch_F16C_UI.IsChecked = value; }
        }
        public bool useArch_FSGSBASE {
            get { return (this.useArch_FSGSBASE_UI.IsChecked.HasValue) ? this.useArch_FSGSBASE_UI.IsChecked.Value : false; }
            set { this.useArch_FSGSBASE_UI.IsChecked = value; }
        }
        public bool useArch_HLE {
            get { return (this.useArch_HLE_UI.IsChecked.HasValue) ? this.useArch_HLE_UI.IsChecked.Value : false; }
            set { this.useArch_HLE_UI.IsChecked = value; }
        }
        public bool useArch_INVPCID {
            get { return (this.useArch_INVPCID_UI.IsChecked.HasValue) ? this.useArch_INVPCID_UI.IsChecked.Value : false; }
            set { this.useArch_INVPCID_UI.IsChecked = value; }
        }
        public bool useArch_PCLMULQDQ {
            get { return (this.useArch_PCLMULQDQ_UI.IsChecked.HasValue) ? this.useArch_PCLMULQDQ_UI.IsChecked.Value : false; }
            set { this.useArch_PCLMULQDQ_UI.IsChecked = value; }
        }
        public bool useArch_LZCNT {
            get { return (this.useArch_LZCNT_UI.IsChecked.HasValue) ? this.useArch_LZCNT_UI.IsChecked.Value : false; }
            set { this.useArch_LZCNT_UI.IsChecked = value; }
        }
        public bool useArch_PREFETCHWT1 {
            get { return (this.useArch_PREFETCHWT1_UI.IsChecked.HasValue) ? this.useArch_PREFETCHWT1_UI.IsChecked.Value : false; }
            set { this.useArch_PREFETCHWT1_UI.IsChecked = value; }
        }
        public bool useArch_PREFETCHW {
            get { return (this.useArch_PREFETCHW_UI.IsChecked.HasValue) ? this.useArch_PREFETCHW_UI.IsChecked.Value : false; }
            set { this.useArch_PREFETCHW_UI.IsChecked = value; }
        }
        public bool useArch_RDPID {
            get { return (this.useArch_RDPID_UI.IsChecked.HasValue) ? this.useArch_RDPID_UI.IsChecked.Value : false; }
            set { this.useArch_RDPID_UI.IsChecked = value; }
        }
        public bool useArch_RDRAND {
            get { return (this.useArch_RDRAND_UI.IsChecked.HasValue) ? this.useArch_RDRAND_UI.IsChecked.Value : false; }
            set { this.useArch_RDRAND_UI.IsChecked = value; }
        }
        public bool useArch_RDSEED {
            get { return (this.useArch_RDSEED_UI.IsChecked.HasValue) ? this.useArch_RDSEED_UI.IsChecked.Value : false; }
            set { this.useArch_RDSEED_UI.IsChecked = value; }
        }
        public bool useArch_XSAVEOPT {
            get { return (this.useArch_XSAVEOPT_UI.IsChecked.HasValue) ? this.useArch_XSAVEOPT_UI.IsChecked.Value : false; }
            set { this.useArch_XSAVEOPT_UI.IsChecked = value; }
        }
        public bool useArch_UNDOC {
            get { return (this.useArch_UNDOC_UI.IsChecked.HasValue) ? this.useArch_UNDOC_UI.IsChecked.Value : false; }
            set { this.useArch_UNDOC_UI.IsChecked = value; }
        }
        public bool useArch_AES {
            get { return (this.useArch_AES_UI.IsChecked.HasValue) ? this.useArch_AES_UI.IsChecked.Value : false; }
            set { this.useArch_AES_UI.IsChecked = value; }
        }

        #endregion

        #region Intellisense
        public bool showUndefinedLabels {
            get { return (this.showUndefinedLabels_UI.IsChecked.HasValue) ? this.showUndefinedLabels_UI.IsChecked.Value : false; }
            set { this.showUndefinedLabels_UI.IsChecked = value; }
        }
        public bool showClashingLabels {
            get { return (this.showClashingLabels_UI.IsChecked.HasValue) ? this.showClashingLabels_UI.IsChecked.Value : false; }
            set { this.showClashingLabels_UI.IsChecked = value; }
        }
        public bool decorateUndefinedLabels {
            get { return (this.decorateUndefinedLabels_UI.IsChecked.HasValue) ? this.decorateUndefinedLabels_UI.IsChecked.Value : false; }
            set { this.decorateUndefinedLabels_UI.IsChecked = value; }
        }
        public bool decorateClashingLabels {
            get { return (this.decorateClashingLabels_UI.IsChecked.HasValue) ? this.decorateClashingLabels_UI.IsChecked.Value : false; }
            set { this.decorateClashingLabels_UI.IsChecked = value; }
        }
        #endregion
    }
}
