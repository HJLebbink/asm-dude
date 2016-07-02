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
            version_UI.Content = "AsmDude v" + typeof(AsmDudePackage).Assembly.GetName().Version.ToString() + "; compile date: " + ApplicationInformation.CompileDate.ToString();
        }

        #region Asm Documentation
        public bool useAsmDoc {
            get { return (useAsmDoc_UI.IsChecked.HasValue) ? useAsmDoc_UI.IsChecked.Value : false; }
            set { useAsmDoc_UI.IsChecked = value; }
        }

        public string asmDocUrl {
            get { return asmDocUrl_UI.Text; }
            set { asmDocUrl_UI.Text = value; }
        }
        #endregion Asm Documentation

        #region Code Folding
        public bool useCodeFolding {
            get { return (useCodeFolding_UI.IsChecked.HasValue) ? useCodeFolding_UI.IsChecked.Value : false; }
            set { useCodeFolding_UI.IsChecked = value; }
        }

        public bool isDefaultCollaped {
            get { return (isDefaultCollaped_UI.IsChecked.HasValue) ? isDefaultCollaped_UI.IsChecked.Value : false; }
            set { isDefaultCollaped_UI.IsChecked = value; }
        }

        public string beginTag {
            get { return beginTag_UI.Text; }
            set { beginTag_UI.Text = value; }
        }

        public string endTag {
            get { return endTag_UI.Text; }
            set { endTag_UI.Text = value; }
        }
        #endregion Code Folding

        #region Syntax Highlighting

        public bool useSyntaxHighlighting {
            get { return (useSyntaxHighlighting_UI.IsChecked.HasValue) ? useSyntaxHighlighting_UI.IsChecked.Value : false; }
            set { useSyntaxHighlighting_UI.IsChecked = value; }
        }

        public AssemblerEnum usedAssembler {
            get {
                if (usedAssemblerMasm_UI.IsChecked.HasValue && usedAssemblerMasm_UI.IsChecked.Value) return AssemblerEnum.MASM;
                if (usedAssemblerNasm_UI.IsChecked.HasValue && usedAssemblerNasm_UI.IsChecked.Value) return AssemblerEnum.NASM;
                return AssemblerEnum.MASM;
            }
            set {
                switch (value) {
                    case AssemblerEnum.MASM:
                        usedAssemblerMasm_UI.IsChecked = true;
                        usedAssemblerNasm_UI.IsChecked = false;
                        break;
                    case AssemblerEnum.NASM:
                        usedAssemblerMasm_UI.IsChecked = false;
                        usedAssemblerNasm_UI.IsChecked = true;
                        break;
                }
            }
        }

        public System.Drawing.Color colorMnemonic {
            get {
                if (colorMnemonic_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorMnemonic_UI.SelectedColor.Value);
                } else {
                    //AsmDudeToolsStatic.Output("INFO: AsmDudeOptionsPageUI.xaml: colorMnemonic_UI has no value, assuming BLUE");
                    return System.Drawing.Color.Blue;
                }
            }
            set { colorMnemonic_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorRegister {
            get {
                if (colorRegister_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorRegister_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkRed;
                }
            }
            set { colorRegister_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorRemark {
            get {
                if (colorRemark_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorRemark_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Green;
                }
            }
            set { colorRemark_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorDirective {
            get {
                if (colorDirective_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorDirective_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Magenta;
                }
            }
            set { colorDirective_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorConstant {
            get {
                if (colorConstant_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorConstant_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Chocolate;
                }
            }
            set { colorConstant_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorJump {
            get {
                if (colorJump_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorJump_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Blue;
                }
            }
            set { colorJump_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorLabel {
            get {
                if (colorLabel_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorLabel_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.OrangeRed;
                }
            }
            set { colorLabel_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }

        public System.Drawing.Color colorMisc {
            get {
                if (colorMisc_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(colorMisc_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.DarkOrange;
                }
            }
            set { colorMisc_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }
        #endregion Syntax Highlighting

        #region Keyword Highlighting
        public bool useCodeKeywordHighlighting {
            get { return (useKeywordHighlighting_UI.IsChecked.HasValue) ? useKeywordHighlighting_UI.IsChecked.Value : false; }
            set { useKeywordHighlighting_UI.IsChecked = value; }
        }
        public System.Drawing.Color _backgroundColor { get; set; }

        public System.Drawing.Color backgroundColor {
            get {
                if (backgroundColor_UI.SelectedColor.HasValue) {
                    return AsmDudeToolsStatic.convertColor(backgroundColor_UI.SelectedColor.Value);
                } else {
                    return System.Drawing.Color.Cyan;
                }
            }
            set { backgroundColor_UI.SelectedColor = AsmDudeToolsStatic.convertColor(value); }
        }
        #endregion

        #region Code Completion
        public bool useCodeCompletion {
            get { return (useCodeCompletion_UI.IsChecked.HasValue) ? useCodeCompletion_UI.IsChecked.Value : false; }
            set { useCodeCompletion_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_x86 {
            get { return (useCodeCompletion_x86_UI.IsChecked.HasValue) ? useCodeCompletion_x86_UI.IsChecked.Value : false; }
            set { useCodeCompletion_x86_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_i686 {
            get { return (useCodeCompletion_i686_UI.IsChecked.HasValue) ? useCodeCompletion_i686_UI.IsChecked.Value : false; }
            set { useCodeCompletion_i686_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_MMX {
            get { return (useCodeCompletion_mmx_UI.IsChecked.HasValue) ? useCodeCompletion_mmx_UI.IsChecked.Value : false; }
            set { useCodeCompletion_mmx_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSE {
            get { return (useCodeCompletion_sse_UI.IsChecked.HasValue) ? useCodeCompletion_sse_UI.IsChecked.Value : false; }
            set { useCodeCompletion_sse_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSE2 {
            get { return (useCodeCompletion_sse2_UI.IsChecked.HasValue) ? useCodeCompletion_sse2_UI.IsChecked.Value : false; }
            set { useCodeCompletion_sse2_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSE3 {
            get { return (useCodeCompletion_sse3_UI.IsChecked.HasValue) ? useCodeCompletion_sse3_UI.IsChecked.Value : false; }
            set { useCodeCompletion_sse3_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSSE3 {
            get { return (useCodeCompletion_sse3_UI.IsChecked.HasValue) ? useCodeCompletion_ssse3_UI.IsChecked.Value : false; }
            set { useCodeCompletion_ssse3_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSE41 {
            get { return (useCodeCompletion_sse41_UI.IsChecked.HasValue) ? useCodeCompletion_sse41_UI.IsChecked.Value : false; }
            set { useCodeCompletion_sse41_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_SSE42 {
            get { return (useCodeCompletion_sse42_UI.IsChecked.HasValue) ? useCodeCompletion_sse42_UI.IsChecked.Value : false; }
            set { useCodeCompletion_sse42_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_AVX {
            get { return (useCodeCompletion_avx_UI.IsChecked.HasValue) ? useCodeCompletion_avx_UI.IsChecked.Value : false; }
            set { useCodeCompletion_avx_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_AVX2 {
            get { return (useCodeCompletion_avx2_UI.IsChecked.HasValue) ? useCodeCompletion_avx2_UI.IsChecked.Value : false; }
            set { useCodeCompletion_avx_UI.IsChecked = value; }
        }
        public bool useCodeCompletion_KNC {
            get { return (useCodeCompletion_knc_UI.IsChecked.HasValue) ? useCodeCompletion_knc_UI.IsChecked.Value : false; }
            set { useCodeCompletion_knc_UI.IsChecked = value; }
        }
        #endregion

        #region Intellisense
        public bool showUndefinedLabels {
            get { return (showUndefinedLabels_UI.IsChecked.HasValue) ? showUndefinedLabels_UI.IsChecked.Value : false; }
            set { showUndefinedLabels_UI.IsChecked = value; }
        }
        public bool showClashingLabels {
            get { return (showClashingLabels_UI.IsChecked.HasValue) ? showClashingLabels_UI.IsChecked.Value : false; }
            set { showClashingLabels_UI.IsChecked = value; }
        }
        public bool decorateUndefinedLabels {
            get { return (decorateUndefinedLabels_UI.IsChecked.HasValue) ? decorateUndefinedLabels_UI.IsChecked.Value : false; }
            set { decorateUndefinedLabels_UI.IsChecked = value; }
        }
        public bool decorateClashingLabels {
            get { return (decorateClashingLabels_UI.IsChecked.HasValue) ? decorateClashingLabels_UI.IsChecked.Value : false; }
            set { decorateClashingLabels_UI.IsChecked = value; }
        }
        #endregion
    }
}
