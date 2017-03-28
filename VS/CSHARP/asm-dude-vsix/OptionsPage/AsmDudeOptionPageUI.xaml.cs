// The MIT License (MIT)
//
// Copyright (c) 2017 H.J. Lebbink
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
        public bool AsmDoc_On {
            get { return this.asmDoc_On_UI.IsChecked ?? false; }
            set { this.asmDoc_On_UI.IsChecked = value; }
        }

        public string AsmDoc_Url {
            get { return this.asmDoc_Url_UI.Text; }
            set { this.asmDoc_Url_UI.Text = value; }
        }
        #endregion Asm Documentation

        #region Code Folding
        public bool CodeFolding_On {
            get { return this.codeFolding_On_UI.IsChecked ?? false; }
            set { this.codeFolding_On_UI.IsChecked = value; }
        }

        public bool CodeFolding_IsDefaultCollaped {
            get { return this.codeFolding_IsDefaultCollaped_UI.IsChecked ?? false; }
            set { this.codeFolding_IsDefaultCollaped_UI.IsChecked = value; }
        }

        public string CodeFolding_BeginTag {
            get { return this.codeFolding_BeginTag_UI.Text; }
            set { this.codeFolding_BeginTag_UI.Text = value; }
        }

        public string CodeFolding_EndTag {
            get { return this.codeFolding_EndTag_UI.Text; }
            set { this.codeFolding_EndTag_UI.Text = value; }
        }
        #endregion Code Folding

        #region Syntax Highlighting

        public bool SyntaxHighlighting_On {
            get { return this.syntaxHighlighting_On_UI.IsChecked ?? false; }
            set { this.syntaxHighlighting_On_UI.IsChecked = value; }
        }

        public AssemblerEnum UsedAssembler {
            get {
                if (this.usedAssemblerMasm_UI.IsChecked.HasValue && this.usedAssemblerMasm_UI.IsChecked.Value) return AssemblerEnum.MASM;
                if (this.usedAssemblerNasm_UI.IsChecked.HasValue && this.usedAssemblerNasm_UI.IsChecked.Value) return AssemblerEnum.NASM;
                return AssemblerEnum.MASM;
            }
            set {
                if (value.HasFlag(AssemblerEnum.MASM))
                {
                    this.usedAssemblerMasm_UI.IsChecked = true;
                    this.usedAssemblerNasm_UI.IsChecked = false;
                } else if (value.HasFlag(AssemblerEnum.NASM))
                {
                    this.usedAssemblerMasm_UI.IsChecked = false;
                    this.usedAssemblerNasm_UI.IsChecked = true;
                }
            }
        }

        public System.Drawing.Color ColorMnemonic {
            get {
                if (this.colorMnemonic_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorMnemonic_UI.SelectedColor.Value);
                } else {
                    //AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPageUI.xaml: colorMnemonic_UI has no value, assuming BLUE");
                    return System.Drawing.Color.Blue;
                }
            }
            set { this.colorMnemonic_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorRegister {
            get {
                if (this.colorRegister_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorRegister_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkRed;
                }
            }
            set { this.colorRegister_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorRemark {
            get {
                if (this.colorRemark_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorRemark_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Green;
                }
            }
            set { this.colorRemark_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorDirective {
            get {
                if (this.colorDirective_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorDirective_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Magenta;
                }
            }
            set { this.colorDirective_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorConstant {
            get {
                if (this.colorConstant_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorConstant_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Chocolate;
                }
            }
            set { this.colorConstant_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorJump {
            get {
                if (this.colorJump_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorJump_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Blue;
                }
            }
            set { this.colorJump_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorLabel {
            get {
                if (this.colorLabel_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorLabel_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.OrangeRed;
                }
            }
            set { this.colorLabel_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public System.Drawing.Color ColorMisc {
            get {
                if (this.colorMisc_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.ConvertColor(this.colorMisc_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkOrange;
                }
            }
            set { this.colorMisc_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }
        #endregion Syntax Highlighting

        #region Keyword Highlighting
        public bool KeywordHighlight_On {
            get { return this.useKeywordHighlighting_UI.IsChecked ?? false; }
            set { this.useKeywordHighlighting_UI.IsChecked = value; }
        }
        public System.Drawing.Color KeywordHighlight_backgroundColor {
            get {
                return (this.backgroundColor_UI.SelectedColor.HasValue)
                    ? AsmDudeToolsStatic.ConvertColor(this.backgroundColor_UI.SelectedColor.Value)
                    : System.Drawing.Color.Cyan;
            }
            set { this.backgroundColor_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }
        public System.Drawing.Color KeywordHighlight_fontColor
        {
            get
            {
                return (this.fontColor_UI.SelectedColor.HasValue) 
                    ? AsmDudeToolsStatic.ConvertColor(this.fontColor_UI.SelectedColor.Value)
                    : System.Drawing.Color.Cyan;
            }
            set { this.fontColor_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        #endregion

        #region Code Completion
        public bool UseCodeCompletion {
            get { return this.useCodeCompletion_UI.IsChecked ?? false; }
            set { this.useCodeCompletion_UI.IsChecked = value; }
        }
        public bool UseSignatureHelp {
            get { return this.useSignatureHelp_UI.IsChecked ?? false; }
            set { this.useSignatureHelp_UI.IsChecked = value; }
        }

        public bool UseArch_8086 {
            get { return this.useArch_8086_UI.IsChecked ?? false; }
            set { this.useArch_8086_UI.IsChecked = value; }
        }
        public bool UseArch_186 {
            get { return this.useArch_186_UI.IsChecked ?? false; }
            set { this.useArch_186_UI.IsChecked = value; }
        }
        public bool UseArch_286 {
            get { return this.useArch_286_UI.IsChecked ?? false; }
            set { this.useArch_286_UI.IsChecked = value; }
        }
        public bool UseArch_386 {
            get { return this.useArch_386_UI.IsChecked ?? false; }
            set { this.useArch_386_UI.IsChecked = value; }
        }
        public bool UseArch_486 {
            get { return this.useArch_486_UI.IsChecked ?? false; }
            set { this.useArch_486_UI.IsChecked = value; }
        }
        public bool UseArch_MMX {
            get { return this.useArch_MMX_UI.IsChecked ?? false; }
            set { this.useArch_MMX_UI.IsChecked = value; }
        }
        public bool UseArch_SSE {
            get { return this.useArch_SSE_UI.IsChecked ?? false; }
            set { this.useArch_SSE_UI.IsChecked = value; }
        }
        public bool UseArch_SSE2 {
            get { return this.useArch_SSE2_UI.IsChecked ?? false; }
            set { this.useArch_SSE2_UI.IsChecked = value; }
        }
        public bool UseArch_SSE3 {
            get { return this.useArch_SSE3_UI.IsChecked ?? false; }
            set { this.useArch_SSE3_UI.IsChecked = value; }
        }
        public bool UseArch_SSSE3 {
            get { return this.useArch_SSSE3_UI.IsChecked ?? false; }
            set { this.useArch_SSSE3_UI.IsChecked = value; }
        }
        public bool UseArch_SSE41 {
            get { return this.useArch_SSE41_UI.IsChecked ?? false; }
            set { this.useArch_SSE41_UI.IsChecked = value; }
        }
        public bool UseArch_SSE42 {
            get { return this.useArch_SSE42_UI.IsChecked ?? false; }
            set { this.useArch_SSE42_UI.IsChecked = value; }
        }
        public bool UseArch_SSE4A {
            get { return this.useArch_SSE4A_UI.IsChecked ?? false; }
            set { this.useArch_SSE4A_UI.IsChecked = value; }
        }

        public bool UseArch_SSE5 {
            get { return this.useArch_SSE5_UI.IsChecked ?? false; }
            set { this.useArch_SSE5_UI.IsChecked = value; }
        }
        public bool UseArch_AVX {
            get { return this.useArch_AVX_UI.IsChecked ?? false; }
            set { this.useArch_AVX_UI.IsChecked = value; }
        }
        public bool UseArch_AVX2 {
            get { return this.useArch_AVX2_UI.IsChecked ?? false; }
            set { this.useArch_AVX2_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512PF {
            get { return this.useArch_AVX512PF_UI.IsChecked ?? false; }
            set { this.useArch_AVX512PF_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512VL {
            get { return this.useArch_AVX512VL_UI.IsChecked ?? false; }
            set { this.useArch_AVX512VL_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512DQ {
            get { return this.useArch_AVX512DQ_UI.IsChecked ?? false; }
            set { this.useArch_AVX512DQ_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512BW {
            get { return this.useArch_AVX512BW_UI.IsChecked ?? false; }
            set { this.useArch_AVX512BW_UI.IsChecked = value; }
        }

        public bool UseArch_AVX512ER {
            get { return this.useArch_AVX512ER_UI.IsChecked ?? false; }
            set { this.useArch_AVX512ER_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512F {
            get { return this.useArch_AVX512F_UI.IsChecked ?? false; }
            set { this.useArch_AVX512F_UI.IsChecked = value; }
        }
        public bool UseArch_AVX512CD {
            get { return this.useArch_AVX512CD_UI.IsChecked ?? false; }
            set { this.useArch_AVX512CD_UI.IsChecked = value; }
        }
        public bool UseArch_X64 {
            get { return this.useArch_X64_UI.IsChecked ?? false; }
            set { this.useArch_X64_UI.IsChecked = value; }
        }
        public bool UseArch_BMI1 {
            get { return this.useArch_BMI1_UI.IsChecked ?? false; }
            set { this.useArch_BMI1_UI.IsChecked = value; }
        }
        public bool UseArch_BMI2 {
            get { return this.useArch_BMI2_UI.IsChecked ?? false; }
            set { this.useArch_BMI2_UI.IsChecked = value; }
        }
        public bool UseArch_P6 {
            get { return this.useArch_P6_UI.IsChecked ?? false; }
            set { this.useArch_P6_UI.IsChecked = value; }
        }
        public bool UseArch_IA64 {
            get { return this.useArch_IA64_UI.IsChecked ?? false; }
            set { this.useArch_IA64_UI.IsChecked = value; }
        }
        public bool UseArch_FMA {
            get { return this.useArch_FMA_UI.IsChecked ?? false; }
            set { this.useArch_FMA_UI.IsChecked = value; }
        }
        public bool UseArch_TBM {
            get { return this.useArch_TBM_UI.IsChecked ?? false; }
            set { this.useArch_TBM_UI.IsChecked = value; }
        }
        public bool UseArch_AMD {
            get { return this.useArch_AMD_UI.IsChecked ?? false; }
            set { this.useArch_AMD_UI.IsChecked = value; }
        }
        public bool UseArch_PENT {
            get { return this.useArch_PENT_UI.IsChecked ?? false; }
            set { this.useArch_PENT_UI.IsChecked = value; }
        }
         public bool UseArch_3DNOW {
            get { return this.useArch_3DNOW_UI.IsChecked ?? false; }
            set { this.useArch_3DNOW_UI.IsChecked = value; }
        }
        public bool UseArch_CYRIX {
            get { return this.useArch_CYRIX_UI.IsChecked ?? false; }
            set { this.useArch_CYRIX_UI.IsChecked = value; }
        }
        public bool UseArch_CYRIXM {
            get { return this.useArch_CYRIXM_UI.IsChecked ?? false; }
            set { this.useArch_CYRIXM_UI.IsChecked = value; }
        }
        public bool UseArch_VMX {
            get { return this.useArch_VMX_UI.IsChecked ?? false; }
            set { this.useArch_VMX_UI.IsChecked = value; }
        }
        public bool UseArch_RTM {
            get { return this.useArch_RTM_UI.IsChecked ?? false; }
            set { this.useArch_RTM_UI.IsChecked = value; }
        }
        public bool UseArch_MPX {
            get { return this.useArch_MPX_UI.IsChecked ?? false; }
            set { this.useArch_MPX_UI.IsChecked = value; }
        }
        public bool UseArch_SHA {
            get { return this.useArch_SHA_UI.IsChecked ?? false; }
            set { this.useArch_SHA_UI.IsChecked = value; }
        }

        public bool UseArch_ADX {
            get { return this.useArch_ADX_UI.IsChecked ?? false; }
            set { this.useArch_ADX_UI.IsChecked = value; }
        }
        public bool UseArch_F16C {
            get { return this.useArch_F16C_UI.IsChecked ?? false; }
            set { this.useArch_F16C_UI.IsChecked = value; }
        }
        public bool UseArch_FSGSBASE {
            get { return this.useArch_FSGSBASE_UI.IsChecked ?? false; }
            set { this.useArch_FSGSBASE_UI.IsChecked = value; }
        }
        public bool UseArch_HLE {
            get { return this.useArch_HLE_UI.IsChecked ?? false; }
            set { this.useArch_HLE_UI.IsChecked = value; }
        }
        public bool UseArch_INVPCID {
            get { return this.useArch_INVPCID_UI.IsChecked ?? false; }
            set { this.useArch_INVPCID_UI.IsChecked = value; }
        }
        public bool UseArch_PCLMULQDQ {
            get { return this.useArch_PCLMULQDQ_UI.IsChecked ?? false; }
            set { this.useArch_PCLMULQDQ_UI.IsChecked = value; }
        }
        public bool UseArch_LZCNT {
            get { return this.useArch_LZCNT_UI.IsChecked ?? false; }
            set { this.useArch_LZCNT_UI.IsChecked = value; }
        }
        public bool UseArch_PREFETCHWT1 {
            get { return this.useArch_PREFETCHWT1_UI.IsChecked ?? false; }
            set { this.useArch_PREFETCHWT1_UI.IsChecked = value; }
        }
        public bool UseArch_PREFETCHW {
            get { return this.useArch_PREFETCHW_UI.IsChecked ?? false; }
            set { this.useArch_PREFETCHW_UI.IsChecked = value; }
        }
        public bool UseArch_RDPID {
            get { return this.useArch_RDPID_UI.IsChecked ?? false; }
            set { this.useArch_RDPID_UI.IsChecked = value; }
        }
        public bool UseArch_RDRAND {
            get { return this.useArch_RDRAND_UI.IsChecked ?? false; }
            set { this.useArch_RDRAND_UI.IsChecked = value; }
        }
        public bool UseArch_RDSEED {
            get { return this.useArch_RDSEED_UI.IsChecked ?? false; }
            set { this.useArch_RDSEED_UI.IsChecked = value; }
        }
        public bool UseArch_XSAVEOPT {
            get { return this.useArch_XSAVEOPT_UI.IsChecked ?? false; }
            set { this.useArch_XSAVEOPT_UI.IsChecked = value; }
        }
        public bool UseArch_UNDOC {
            get { return this.useArch_UNDOC_UI.IsChecked ?? false; }
            set { this.useArch_UNDOC_UI.IsChecked = value; }
        }
        public bool UseArch_AES {
            get { return this.useArch_AES_UI.IsChecked ?? false; }
            set { this.useArch_AES_UI.IsChecked = value; }
        }

        #endregion

        #region Intellisense
        public bool ShowUndefinedLabels {
            get { return this.showUndefinedLabels_UI.IsChecked ?? false; }
            set { this.showUndefinedLabels_UI.IsChecked = value; }
        }
        public bool ShowClashingLabels {
            get { return this.showClashingLabels_UI.IsChecked ?? false; }
            set { this.showClashingLabels_UI.IsChecked = value; }
        }
        public bool DecorateUndefinedLabels {
            get { return this.decorateUndefinedLabels_UI.IsChecked ?? false; }
            set { this.decorateUndefinedLabels_UI.IsChecked = value; }
        }
        public bool DecorateClashingLabels {
            get { return this.decorateClashingLabels_UI.IsChecked ?? false; }
            set { this.decorateClashingLabels_UI.IsChecked = value; }
        }
        #endregion
    }
}
