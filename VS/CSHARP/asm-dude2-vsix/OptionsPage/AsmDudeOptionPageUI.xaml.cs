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

namespace AsmDude2
{
    using System;
    using System.Windows.Controls;
    using AsmDude2.Tools;
    using AsmTools;

    /// <summary>
    /// Interaction logic for AsmDudeOptionPageUI.xaml
    /// </summary>
    public partial class AsmDudeOptionsPageUI : UserControl
    {
        public AsmDudeOptionsPageUI()
        {
            this.InitializeComponent();

            string vsixVersion = ApplicationInformation.VsixVersion();
            string vsixBuildInfo = ApplicationInformation.VsixBuildInfo();
            string lspVersion = ApplicationInformation.LspVersion();

            this.version_UI.Content = $"AsmDude2 VSIX v{vsixVersion} ({vsixBuildInfo})\nAsmDude2 LSP v{lspVersion}";

            #region setup handlers
            this.SyntaxHighlighting_On_UI.Click += (o, i) => { this.SyntaxHighlighting_Update(this.SyntaxHighlighting_On); };
            this.SyntaxHighlighting_Update(Settings.Default.SyntaxHighlighting_On);

            this.PerformanceInfo_On_UI.Click += (o, i) => { this.PerformanceInfo_Update(this.PerformanceInfo_On); };
            this.PerformanceInfo_Update(Settings.Default.PerformanceInfo_On);

            this.AsmDoc_On_UI.Click += (o, i) => { this.AsmDoc_Update(this.AsmDoc_On); };
            this.AsmDoc_Update(Settings.Default.AsmDoc_On);

            this.CodeFolding_On_UI.Click += (o, i) => { this.CodeFolding_Update(this.CodeFolding_On); };
            this.CodeFolding_Update(Settings.Default.CodeFolding_On);

            this.AsmSim_On_UI.Click += (o, i) => { this.AsmSim_Update(this.AsmSim_On); };
            this.AsmSim_Update(Settings.Default.AsmSim_On);
            #endregion
        }

        public object GetPropValue(string propName)
        {
            try
            {
                var value = this.GetType().GetProperty(propName).GetValue(this, null);
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:GetPropValue: propName={1}; o={2}", this.ToString(), propName, value.ToString()));
                return value;
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("Could not find property " + propName + "; " + e);
                return "ERROR";
            }
        }

        public void SetPropValue(string propName, object o)
        {
            if (o != null)
            {
                try
                {
                   // AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:SetPropValue: propName={1}; o={2}", this.ToString(), propName, o.ToString()));
                    this.GetType().GetProperty(propName).SetValue(this, o);
                }
                catch (Exception)
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:SetPropValue: Could not find property={1}; o={2}", this.ToString(), propName, o.ToString()));
                }
            }
        }

        #region Event Handlers to disable options
        private void SyntaxHighlighting_Update(bool value)
        {
            this.ColorMnemonic_UI.IsEnabled = value;
            this.ColorMnemonic_Italic_UI.IsEnabled = value;
            this.ColorRegister_UI.IsEnabled = value;
            this.ColorRegister_Italic_UI.IsEnabled = value;
            this.ColorRemark_UI.IsEnabled = value;
            this.ColorRemark_Italic_UI.IsEnabled = value;
            this.ColorDirective_UI.IsEnabled = value;
            this.ColorDirective_Italic_UI.IsEnabled = value;
            this.ColorConstant_UI.IsEnabled = value;
            this.ColorConstant_Italic_UI.IsEnabled = value;
            this.ColorJump_UI.IsEnabled = value;
            this.ColorJump_Italic_UI.IsEnabled = value;
            this.ColorLabel_UI.IsEnabled = value;
            this.ColorLabel_Italic_UI.IsEnabled = value;
            this.ColorMisc_UI.IsEnabled = value;
            this.ColorMisc_Italic_UI.IsEnabled = value;
            this.ColorUserDefined1_UI.IsEnabled = value;
            this.ColorUserDefined1_Italic_UI.IsEnabled = value;
            this.ColorUserDefined2_UI.IsEnabled = value;
            this.ColorUserDefined2_Italic_UI.IsEnabled = value;
            this.ColorUserDefined3_UI.IsEnabled = value;
            this.ColorUserDefined3_Italic_UI.IsEnabled = value;
        }

        private void PerformanceInfo_Update(bool value)
        {
            //this.PerformanceInfo_SandyBridge_UI.IsEnabled = value;
            this.PerformanceInfo_IvyBridge_UI.IsEnabled = value;
            this.PerformanceInfo_Haswell_UI.IsEnabled = value;
            this.PerformanceInfo_Broadwell_UI.IsEnabled = value;
            this.PerformanceInfo_Skylake_UI.IsEnabled = value;
            this.PerformanceInfo_SkylakeX_UI.IsEnabled = value;
            //this.PerformanceInfo_KnightsLanding_UI.IsEnabled = value;
        }

        private void AsmDoc_Update(bool value)
        {
            this.AsmDoc_Url_UI.IsEnabled = value;
        }

        private void CodeFolding_Update(bool value)
        {
            this.CodeFolding_BeginTag_UI.IsEnabled = value;
            this.CodeFolding_EndTag_UI.IsEnabled = value;
        }

        private void AsmSim_Update(bool value)
        {
            this.AsmSim_Number_Of_Threads_UI.IsEnabled = value;
            this.AsmSim_Z3_Timeout_MS_UI.IsEnabled = value;
            this.AsmSim_Shown_Syntax_Errors_UI.IsEnabled = value;
            this.AsmSim_Decorate_Syntax_Errors_UI.IsEnabled = value;
            this.AsmSim_Show_Usage_Of_Undefined_UI.IsEnabled = value;
            this.AsmSim_Decorate_Usage_Of_Undefined_UI.IsEnabled = value;
            this.AsmSim_Show_Redundant_Instructions_UI.IsEnabled = value;
            this.AsmSim_Decorate_Redundant_Instructions_UI.IsEnabled = value;
            this.AsmSim_Show_Unreachable_Instructions_UI.IsEnabled = value;
            this.AsmSim_Decorate_Unreachable_Instructions_UI.IsEnabled = value;
            this.AsmSim_Decorate_Registers_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Code_Completion_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Code_Completion_Numeration_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Register_Tooltip_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Register_Tooltip_Numeration_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Instruction_Tooltip_UI.IsEnabled = value;
            this.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration_UI.IsEnabled = value;
            this.AsmSim_Decorate_Unimplemented_UI.IsEnabled = value;
            this.AsmSim_Pragma_Assume_UI.IsEnabled = value;
            this.AsmSim_64_Bits_UI.IsEnabled = value;
        }
        #endregion

        #region Global
        public int Global_MaxFileLines
        {
            get { return this.Global_MaxFileLines_UI.Value.GetValueOrDefault(); }
            set { this.Global_MaxFileLines_UI.Value = value; }
        }
        #endregion

        #region Assembly Flavour
        public AssemblerEnum UsedAssembler
        {
            get
            {
                if (this.usedAssemblerAuto_UI.IsChecked.HasValue && this.usedAssemblerAuto_UI.IsChecked.Value)
                {
                    return AssemblerEnum.AUTO_DETECT;
                }
                if (this.usedAssemblerMasm_UI.IsChecked.HasValue && this.usedAssemblerMasm_UI.IsChecked.Value)
                {
                    return AssemblerEnum.MASM;
                }
                if (this.usedAssemblerNasm_UI.IsChecked.HasValue && this.usedAssemblerNasm_UI.IsChecked.Value)
                {
                    return AssemblerEnum.NASM_INTEL;
                }
                if (this.usedAssemblerAtt_UI.IsChecked.HasValue && this.usedAssemblerAtt_UI.IsChecked.Value)
                {
                    return AssemblerEnum.NASM_ATT;
                }
                AsmDudeToolsStatic.Output_WARNING("Unrecognized UsedAssembler, assuming AUTO");
                return AssemblerEnum.AUTO_DETECT; // if nothing is checked somehow return AUTO
            }

            set
            {
                this.usedAssemblerAuto_UI.IsChecked = false;
                this.usedAssemblerMasm_UI.IsChecked = false;
                this.usedAssemblerNasm_UI.IsChecked = false;
                this.usedAssemblerAtt_UI.IsChecked = false;

                if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    this.usedAssemblerAuto_UI.IsChecked = true;
                }
                else if (value.HasFlag(AssemblerEnum.MASM))
                {
                    this.usedAssemblerMasm_UI.IsChecked = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_INTEL))
                {
                    this.usedAssemblerNasm_UI.IsChecked = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    this.usedAssemblerAtt_UI.IsChecked = true;
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING("Unrecognized UsedAssembler, assuming AUTO");
                    this.usedAssemblerAuto_UI.IsChecked = true;
                }
            }
        }

        public AssemblerEnum UsedAssemblerDisassemblyWindow
        {
            get
            {
                if (this.AssemblerDisassemblyAuto_UI.IsChecked.HasValue && this.AssemblerDisassemblyAuto_UI.IsChecked.Value)
                {
                    return AssemblerEnum.AUTO_DETECT;
                }
                if (this.AssemblerDisassemblyMasm_UI.IsChecked.HasValue && this.AssemblerDisassemblyMasm_UI.IsChecked.Value)
                {
                    return AssemblerEnum.MASM;
                }
                if (this.AssemblerDisassemblyAtt_UI.IsChecked.HasValue && this.AssemblerDisassemblyAtt_UI.IsChecked.Value)
                {
                    return AssemblerEnum.NASM_ATT;
                }
                AsmDudeToolsStatic.Output_WARNING("Unrecognized UsedAssembler, assuming AUTO");
                return AssemblerEnum.AUTO_DETECT; // if nothing is checked somehow return AUTO
            }

            set
            {
                this.AssemblerDisassemblyAuto_UI.IsChecked = false;
                this.AssemblerDisassemblyMasm_UI.IsChecked = false;
                this.AssemblerDisassemblyAtt_UI.IsChecked = false;

                if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    this.AssemblerDisassemblyAuto_UI.IsChecked = true;
                }
                else if (value.HasFlag(AssemblerEnum.MASM))
                {
                    this.AssemblerDisassemblyMasm_UI.IsChecked = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    this.AssemblerDisassemblyAtt_UI.IsChecked = true;
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING("Unrecognized UsedAssembler, assuming AUTO");
                    this.AssemblerDisassemblyAuto_UI.IsChecked = true;
                }
            }
        }
        #endregion

        #region AsmDoc
        public bool AsmDoc_On
        {
            get { return this.AsmDoc_On_UI.IsChecked ?? false; }
            set { this.AsmDoc_On_UI.IsChecked = value; }
        }

        public string AsmDoc_Url
        {
            get { return this.AsmDoc_Url_UI.Text; }
            set { this.AsmDoc_Url_UI.Text = value; }
        }
        #endregion Asm Documentation

        #region CodeFolding
        public bool CodeFolding_On
        {
            get { return this.CodeFolding_On_UI.IsChecked ?? false; }
            set { this.CodeFolding_On_UI.IsChecked = value; }
        }

        public string CodeFolding_BeginTag
        {
            get { return this.CodeFolding_BeginTag_UI.Text; }
            set { this.CodeFolding_BeginTag_UI.Text = value; }
        }

        public string CodeFolding_EndTag
        {
            get { return this.CodeFolding_EndTag_UI.Text; }
            set { this.CodeFolding_EndTag_UI.Text = value; }
        }

        #endregion Code Folding

        #region Syntax Highlighting

        public bool SyntaxHighlighting_On
        {
            get { return this.SyntaxHighlighting_On_UI.IsChecked ?? false; }
            set { this.SyntaxHighlighting_On_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Opcode
        {
            get { return this.ColorMnemonic_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorMnemonic_UI.SelectedColor.Value) : System.Drawing.Color.Blue; }
            set { this.ColorMnemonic_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Opcode_Italic
        {
            get { return this.ColorMnemonic_Italic_UI.IsChecked ?? false; }
            set { this.ColorMnemonic_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Register
        {
            get { return this.ColorRegister_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorRegister_UI.SelectedColor.Value) : System.Drawing.Color.DarkRed; }
            set { this.ColorRegister_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Register_Italic
        {
            get { return this.ColorRegister_Italic_UI.IsChecked ?? false; }
            set { this.ColorRegister_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Remark
        {
            get { return this.ColorRemark_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorRemark_UI.SelectedColor.Value) : System.Drawing.Color.Green; }
            set { this.ColorRemark_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Remark_Italic
        {
            get { return this.ColorRemark_Italic_UI.IsChecked ?? false; }
            set { this.ColorRemark_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Directive
        {
            get { return this.ColorDirective_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorDirective_UI.SelectedColor.Value) : System.Drawing.Color.Magenta; }
            set { this.ColorDirective_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Directive_Italic
        {
            get { return this.ColorDirective_Italic_UI.IsChecked ?? false; }
            set { this.ColorDirective_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Constant
        {
            get { return this.ColorConstant_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorConstant_UI.SelectedColor.Value) : System.Drawing.Color.Chocolate; }
            set { this.ColorConstant_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Constant_Italic
        {
            get { return this.ColorConstant_Italic_UI.IsChecked ?? false; }
            set { this.ColorConstant_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Jump
        {
            get { return this.ColorJump_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorJump_UI.SelectedColor.Value) : System.Drawing.Color.Blue; }
            set { this.ColorJump_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Jump_Italic
        {
            get { return this.ColorJump_Italic_UI.IsChecked ?? false; }
            set { this.ColorJump_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Label
        {
            get { return this.ColorLabel_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorLabel_UI.SelectedColor.Value) : System.Drawing.Color.OrangeRed; }
            set { this.ColorLabel_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Label_Italic
        {
            get { return this.ColorLabel_Italic_UI.IsChecked ?? false; }
            set { this.ColorLabel_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Misc
        {
            get { return this.ColorMisc_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorMisc_UI.SelectedColor.Value) : System.Drawing.Color.DarkOrange; }
            set { this.ColorMisc_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Misc_Italic
        {
            get { return this.ColorMisc_Italic_UI.IsChecked ?? false; }
            set { this.ColorMisc_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Userdefined1
        {
            get { return this.ColorUserDefined1_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorUserDefined1_UI.SelectedColor.Value) : System.Drawing.Color.Silver; }
            set { this.ColorUserDefined1_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Userdefined1_Italic
        {
            get { return this.ColorUserDefined1_Italic_UI.IsChecked ?? false; }
            set { this.ColorUserDefined1_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Userdefined2
        {
            get { return this.ColorUserDefined2_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorUserDefined2_UI.SelectedColor.Value) : System.Drawing.Color.Silver; }
            set { this.ColorUserDefined2_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Userdefined2_Italic
        {
            get { return this.ColorUserDefined2_Italic_UI.IsChecked ?? false; }
            set { this.ColorUserDefined2_Italic_UI.IsChecked = value; }
        }

        public System.Drawing.Color SyntaxHighlighting_Userdefined3
        {
            get { return this.ColorUserDefined3_UI.SelectedColor.HasValue ? AsmDudeToolsStatic.ConvertColor(this.ColorUserDefined3_UI.SelectedColor.Value) : System.Drawing.Color.Silver; }
            set { this.ColorUserDefined3_UI.SelectedColor = AsmDudeToolsStatic.ConvertColor(value); }
        }

        public bool SyntaxHighlighting_Userdefined3_Italic
        {
            get { return this.ColorUserDefined3_Italic_UI.IsChecked ?? false; }
            set { this.ColorUserDefined3_Italic_UI.IsChecked = value; }
        }
        #endregion Syntax Highlighting

        #region Latency and Throughput Information (Performance Info)

        public bool PerformanceInfo_On
        {
            get { return this.PerformanceInfo_On_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_On_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_SandyBridge_On
        {
            get { return false; }
            set { }
            //get { return this.PerformanceInfo_SandyBridge_UI.IsChecked ?? false; }
            //set { this.PerformanceInfo_SandyBridge_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_IvyBridge_On
        {
            get { return this.PerformanceInfo_IvyBridge_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_IvyBridge_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_Haswell_On
        {
            get { return this.PerformanceInfo_Haswell_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_Haswell_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_Broadwell_On
        {
            get { return this.PerformanceInfo_Broadwell_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_Broadwell_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_Skylake_On
        {
            get { return this.PerformanceInfo_Skylake_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_Skylake_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_SkylakeX_On
        {
            get { return this.PerformanceInfo_SkylakeX_UI.IsChecked ?? false; }
            set { this.PerformanceInfo_SkylakeX_UI.IsChecked = value; }
        }

        public bool PerformanceInfo_KnightsLanding_On
        {
            get { return false; }
            set { }
            //get { return this.PerformanceInfo_KnightsLanding_UI.IsChecked ?? false; }
            //set { this.PerformanceInfo_KnightsLanding_UI.IsChecked = value; }
        }
        #endregion

        #region Code Completion
        public bool CodeCompletion_On
        {
            get { return this.Intellisense_Code_Completion_On_UI.IsChecked ?? false; }
            set { this.Intellisense_Code_Completion_On_UI.IsChecked = value; }
        }

        public bool SignatureHelp_On
        {
            get { return this.Intellisense_Signature_Help_On_UI.IsChecked ?? false; }
            set { this.Intellisense_Signature_Help_On_UI.IsChecked = value; }
        }

        public bool ARCH_8086
        {
            get { return this.ARCH_8086_UI.IsChecked ?? false; }
            set { this.ARCH_8086_UI.IsChecked = value; }
        }

        public bool ARCH_186
        {
            get { return this.ARCH_186_UI.IsChecked ?? false; }
            set { this.ARCH_186_UI.IsChecked = value; }
        }

        public bool ARCH_286
        {
            get { return this.ARCH_286_UI.IsChecked ?? false; }
            set { this.ARCH_286_UI.IsChecked = value; }
        }

        public bool ARCH_386
        {
            get { return this.ARCH_386_UI.IsChecked ?? false; }
            set { this.ARCH_386_UI.IsChecked = value; }
        }

        public bool ARCH_486
        {
            get { return this.ARCH_486_UI.IsChecked ?? false; }
            set { this.ARCH_486_UI.IsChecked = value; }
        }

        public bool ARCH_MMX
        {
            get { return this.ARCH_MMX_UI.IsChecked ?? false; }
            set { this.ARCH_MMX_UI.IsChecked = value; }
        }

        public bool ARCH_SSE
        {
            get { return this.ARCH_SSE_UI.IsChecked ?? false; }
            set { this.ARCH_SSE_UI.IsChecked = value; }
        }

        public bool ARCH_SSE2
        {
            get { return this.ARCH_SSE2_UI.IsChecked ?? false; }
            set { this.ARCH_SSE2_UI.IsChecked = value; }
        }

        public bool ARCH_SSE3
        {
            get { return this.ARCH_SSE3_UI.IsChecked ?? false; }
            set { this.ARCH_SSE3_UI.IsChecked = value; }
        }

        public bool ARCH_SSSE3
        {
            get { return this.ARCH_SSSE3_UI.IsChecked ?? false; }
            set { this.ARCH_SSSE3_UI.IsChecked = value; }
        }

        public bool ARCH_SSE4_1
        {
            get { return this.ARCH_SSE4_1_UI.IsChecked ?? false; }
            set { this.ARCH_SSE4_1_UI.IsChecked = value; }
        }

        public bool ARCH_SSE4_2
        {
            get { return this.ARCH_SSE4_2_UI.IsChecked ?? false; }
            set { this.ARCH_SSE4_2_UI.IsChecked = value; }
        }

        public bool ARCH_SSE4A
        {
            get { return this.ARCH_SSE4A_UI.IsChecked ?? false; }
            set { this.ARCH_SSE4A_UI.IsChecked = value; }
        }

        public bool ARCH_SSE5
        {
            get { return this.ARCH_SSE5_UI.IsChecked ?? false; }
            set { this.ARCH_SSE5_UI.IsChecked = value; }
        }

        public bool ARCH_AVX
        {
            get { return this.ARCH_AVX_UI.IsChecked ?? false; }
            set { this.ARCH_AVX_UI.IsChecked = value; }
        }

        public bool ARCH_AVX2
        {
            get { return this.ARCH_AVX2_UI.IsChecked ?? false; }
            set { this.ARCH_AVX2_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_PF
        {
            get { return this.ARCH_AVX512_PF_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_PF_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VL
        {
            get { return this.ARCH_AVX512_VL_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VL_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_DQ
        {
            get { return this.ARCH_AVX512_DQ_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_DQ_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_BW
        {
            get { return this.ARCH_AVX512_BW_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_BW_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_ER
        {
            get { return this.ARCH_AVX512_ER_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_ER_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_F
        {
            get { return this.ARCH_AVX512_F_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_F_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_CD
        {
            get { return this.ARCH_AVX512_CD_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_CD_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_IFMA
        {
            get { return this.ARCH_AVX512_IFMA_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_IFMA_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VBMI
        {
            get { return this.ARCH_AVX512_VBMI_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VBMI_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VPOPCNTDQ
        {
            get { return this.ARCH_AVX512_VPOPCNTDQ_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VPOPCNTDQ_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_4VNNIW
        {
            get { return this.ARCH_AVX512_4VNNIW_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_4VNNIW_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_4FMAPS
        {
            get { return this.ARCH_AVX512_4FMAPS_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_4FMAPS_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VBMI2
        {
            get { return this.ARCH_AVX512_VBMI2_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VBMI2_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VNNI
        {
            get { return this.ARCH_AVX512_VNNI_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VNNI_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_BITALG
        {
            get { return this.ARCH_AVX512_BITALG_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_BITALG_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_GFNI
        {
            get { return this.ARCH_AVX512_GFNI_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_GFNI_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VAES
        {
            get { return this.ARCH_AVX512_VAES_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VAES_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VPCLMULQDQ
        {
            get { return this.ARCH_AVX512_VPCLMULQDQ_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VPCLMULQDQ_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_BF16
        {
            get { return this.ARCH_AVX512_BF16_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_BF16_UI.IsChecked = value; }
        }

        public bool ARCH_AVX512_VP2INTERSECT
        {
            get { return this.ARCH_AVX512_VP2INTERSECT_UI.IsChecked ?? false; }
            set { this.ARCH_AVX512_VP2INTERSECT_UI.IsChecked = value; }
        }

        public bool ARCH_ENQCMD
        {
            get { return this.ARCH_ENQCMD_UI.IsChecked ?? false; }
            set { this.ARCH_ENQCMD_UI.IsChecked = value; }
        }

        public bool ARCH_X64
        {
            get { return this.ARCH_X64_UI.IsChecked ?? false; }
            set { this.ARCH_X64_UI.IsChecked = value; }
        }

        public bool ARCH_BMI1
        {
            get { return this.ARCH_BMI1_UI.IsChecked ?? false; }
            set { this.ARCH_BMI1_UI.IsChecked = value; }
        }

        public bool ARCH_BMI2
        {
            get { return this.ARCH_BMI2_UI.IsChecked ?? false; }
            set { this.ARCH_BMI2_UI.IsChecked = value; }
        }

        public bool ARCH_P6
        {
            get { return this.ARCH_P6_UI.IsChecked ?? false; }
            set { this.ARCH_P6_UI.IsChecked = value; }
        }

        public bool ARCH_IA64
        {
            get { return this.ARCH_IA64_UI.IsChecked ?? false; }
            set { this.ARCH_IA64_UI.IsChecked = value; }
        }

        public bool ARCH_FMA
        {
            get { return this.ARCH_FMA_UI.IsChecked ?? false; }
            set { this.ARCH_FMA_UI.IsChecked = value; }
        }

        public bool ARCH_TBM
        {
            get { return this.ARCH_TBM_UI.IsChecked ?? false; }
            set { this.ARCH_TBM_UI.IsChecked = value; }
        }

        public bool ARCH_AMD
        {
            get { return this.ARCH_AMD_UI.IsChecked ?? false; }
            set { this.ARCH_AMD_UI.IsChecked = value; }
        }

        public bool ARCH_PENT
        {
            get { return this.ARCH_PENT_UI.IsChecked ?? false; }
            set { this.ARCH_PENT_UI.IsChecked = value; }
        }

        public bool ARCH_3DNOW
        {
            get { return this.ARCH_3DNOW_UI.IsChecked ?? false; }
            set { this.ARCH_3DNOW_UI.IsChecked = value; }
        }

        public bool ARCH_CYRIX
        {
            get { return this.ARCH_CYRIX_UI.IsChecked ?? false; }
            set { this.ARCH_CYRIX_UI.IsChecked = value; }
        }

        public bool ARCH_CYRIXM
        {
            get { return this.ARCH_CYRIXM_UI.IsChecked ?? false; }
            set { this.ARCH_CYRIXM_UI.IsChecked = value; }
        }

        public bool ARCH_VMX
        {
            get { return this.ARCH_VMX_UI.IsChecked ?? false; }
            set { this.ARCH_VMX_UI.IsChecked = value; }
        }

        public bool ARCH_RTM
        {
            get { return this.ARCH_RTM_UI.IsChecked ?? false; }
            set { this.ARCH_RTM_UI.IsChecked = value; }
        }

        public bool ARCH_MPX
        {
            get { return this.ARCH_MPX_UI.IsChecked ?? false; }
            set { this.ARCH_MPX_UI.IsChecked = value; }
        }

        public bool ARCH_SHA
        {
            get { return this.ARCH_SHA_UI.IsChecked ?? false; }
            set { this.ARCH_SHA_UI.IsChecked = value; }
        }

        public bool ARCH_ADX
        {
            get { return this.ARCH_ADX_UI.IsChecked ?? false; }
            set { this.ARCH_ADX_UI.IsChecked = value; }
        }

        public bool ARCH_F16C
        {
            get { return this.ARCH_F16C_UI.IsChecked ?? false; }
            set { this.ARCH_F16C_UI.IsChecked = value; }
        }

        public bool ARCH_FSGSBASE
        {
            get { return this.ARCH_FSGSBASE_UI.IsChecked ?? false; }
            set { this.ARCH_FSGSBASE_UI.IsChecked = value; }
        }

        public bool ARCH_HLE
        {
            get { return this.ARCH_HLE_UI.IsChecked ?? false; }
            set { this.ARCH_HLE_UI.IsChecked = value; }
        }

        public bool ARCH_INVPCID
        {
            get { return this.ARCH_INVPCID_UI.IsChecked ?? false; }
            set { this.ARCH_INVPCID_UI.IsChecked = value; }
        }

        public bool ARCH_PCLMULQDQ
        {
            get { return this.ARCH_PCLMULQDQ_UI.IsChecked ?? false; }
            set { this.ARCH_PCLMULQDQ_UI.IsChecked = value; }
        }

        public bool ARCH_LZCNT
        {
            get { return this.ARCH_LZCNT_UI.IsChecked ?? false; }
            set { this.ARCH_LZCNT_UI.IsChecked = value; }
        }

        public bool ARCH_PREFETCHWT1
        {
            get { return this.ARCH_PREFETCHWT1_UI.IsChecked ?? false; }
            set { this.ARCH_PREFETCHWT1_UI.IsChecked = value; }
        }

        public bool ARCH_PRFCHW
        {
            get { return this.ARCH_PRFCHW_UI.IsChecked ?? false; }
            set { this.ARCH_PRFCHW_UI.IsChecked = value; }
        }

        public bool ARCH_RDPID
        {
            get { return this.ARCH_RDPID_UI.IsChecked ?? false; }
            set { this.ARCH_RDPID_UI.IsChecked = value; }
        }

        public bool ARCH_RDRAND
        {
            get { return this.ARCH_RDRAND_UI.IsChecked ?? false; }
            set { this.ARCH_RDRAND_UI.IsChecked = value; }
        }

        public bool ARCH_RDSEED
        {
            get { return this.ARCH_RDSEED_UI.IsChecked ?? false; }
            set { this.ARCH_RDSEED_UI.IsChecked = value; }
        }

        public bool ARCH_XSAVEOPT
        {
            get { return this.ARCH_XSAVEOPT_UI.IsChecked ?? false; }
            set { this.ARCH_XSAVEOPT_UI.IsChecked = value; }
        }

        public bool ARCH_UNDOC
        {
            get { return this.ARCH_UNDOC_UI.IsChecked ?? false; }
            set { this.ARCH_UNDOC_UI.IsChecked = value; }
        }

        public bool ARCH_AES
        {
            get { return this.ARCH_AES_UI.IsChecked ?? false; }
            set { this.ARCH_AES_UI.IsChecked = value; }
        }

        public bool ARCH_SMX
        {
            get { return this.ARCH_SMX_UI.IsChecked ?? false; }
            set { this.ARCH_SMX_UI.IsChecked = value; }
        }

        public bool ARCH_SGX1
        {
            get { return this.ARCH_SGX1_UI.IsChecked ?? false; }
            set { this.ARCH_SGX1_UI.IsChecked = value; }
        }

        public bool ARCH_SGX2
        {
            get { return this.ARCH_SGX2_UI.IsChecked ?? false; }
            set { this.ARCH_SGX2_UI.IsChecked = value; }
        }

        public bool ARCH_CLDEMOTE
        {
            get { return this.ARCH_CLDEMOTE_UI.IsChecked ?? false; }
            set { this.ARCH_CLDEMOTE_UI.IsChecked = value; }
        }

        public bool ARCH_MOVDIR64B
        {
            get { return this.ARCH_MOVDIR64B_UI.IsChecked ?? false; }
            set { this.ARCH_MOVDIR64B_UI.IsChecked = value; }
        }

        public bool ARCH_MOVDIRI
        {
            get { return this.ARCH_MOVDIRI_UI.IsChecked ?? false; }
            set { this.ARCH_MOVDIRI_UI.IsChecked = value; }
        }

        public bool ARCH_PCONFIG
        {
            get { return this.ARCH_PCONFIG_UI.IsChecked ?? false; }
            set { this.ARCH_PCONFIG_UI.IsChecked = value; }
        }

        public bool ARCH_WAITPKG
        {
            get { return this.ARCH_WAITPKG_UI.IsChecked ?? false; }
            set { this.ARCH_WAITPKG_UI.IsChecked = value; }
        }

        #endregion

        #region Intellisense
        public bool IntelliSense_Label_Analysis_On
        {
            get { return this.Intellisense_Label_Analysis_On_UI.IsChecked ?? false; }
            set { this.Intellisense_Label_Analysis_On_UI.IsChecked = value; }
        }

        public bool IntelliSense_Show_Undefined_Labels
        {
            get { return this.Intellisense_Show_Undefined_Labels_UI.IsChecked ?? false; }
            set { this.Intellisense_Show_Undefined_Labels_UI.IsChecked = value; }
        }

        public bool IntelliSense_Show_Clashing_Labels
        {
            get { return this.Intellisense_Show_Clashing_Labels_UI.IsChecked ?? false; }
            set { this.Intellisense_Show_Clashing_Labels_UI.IsChecked = value; }
        }

        public bool IntelliSense_Decorate_Undefined_Labels
        {
            get { return this.Intellisense_Decorate_Undefined_Labels_UI.IsChecked ?? false; }
            set { this.Intellisense_Decorate_Undefined_Labels_UI.IsChecked = value; }
        }

        public bool IntelliSense_Decorate_Clashing_Labels
        {
            get { return this.Intellisense_Decorate_Clashing_Labels_UI.IsChecked ?? false; }
            set { this.Intellisense_Decorate_Clashing_Labels_UI.IsChecked = value; }
        }

        public bool IntelliSense_Show_Undefined_Includes
        {
            get { return this.Intellisense_Show_Undefined_Includes_UI.IsChecked ?? false; }
            set { this.Intellisense_Show_Undefined_Includes_UI.IsChecked = value; }
        }

        public bool IntelliSense_Decorate_Undefined_Includes
        {
            get { return this.Intellisense_Decorate_Undefined_Includes_UI.IsChecked ?? false; }
            set { this.Intellisense_Decorate_Undefined_Includes_UI.IsChecked = value; }
        }
        #endregion

        #region AsmSim
        public bool AsmSim_On
        {
            get { return this.AsmSim_On_UI.IsChecked ?? false; }
            set { this.AsmSim_On_UI.IsChecked = value; }
        }

        public int AsmSim_Z3_Timeout_MS
        {
            get { return this.AsmSim_Z3_Timeout_MS_UI.Value.GetValueOrDefault(); }
            set { this.AsmSim_Z3_Timeout_MS_UI.Value = value; }
        }

        public int AsmSim_Number_Of_Threads
        {
            get { return this.AsmSim_Number_Of_Threads_UI.Value.GetValueOrDefault(); }
            set { this.AsmSim_Number_Of_Threads_UI.Value = value; }
        }

        public bool AsmSim_64_Bits
        {
            get { return this.AsmSim_64_Bits_UI.IsChecked ?? false; }
            set { this.AsmSim_64_Bits_UI.IsChecked = value; }
        }

        public bool AsmSim_Show_Syntax_Errors
        {
            get { return this.AsmSim_Shown_Syntax_Errors_UI.IsChecked ?? false; }
            set { this.AsmSim_Shown_Syntax_Errors_UI.IsChecked = value; }
        }

        public bool AsmSim_Decorate_Syntax_Errors
        {
            get { return this.AsmSim_Decorate_Syntax_Errors_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Syntax_Errors_UI.IsChecked = value; }
        }

        public bool AsmSim_Show_Usage_Of_Undefined
        {
            get { return this.AsmSim_Show_Usage_Of_Undefined_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Usage_Of_Undefined_UI.IsChecked = value; }
        }

        public bool AsmSim_Decorate_Usage_Of_Undefined
        {
            get { return this.AsmSim_Decorate_Usage_Of_Undefined_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Usage_Of_Undefined_UI.IsChecked = value; }
        }

        public bool AsmSim_Show_Redundant_Instructions
        {
            get { return this.AsmSim_Show_Redundant_Instructions_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Redundant_Instructions_UI.IsChecked = value; }
        }

        public bool AsmSim_Decorate_Redundant_Instructions
        {
            get { return this.AsmSim_Decorate_Redundant_Instructions_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Redundant_Instructions_UI.IsChecked = value; }
        }

        public bool AsmSim_Show_Unreachable_Instructions
        {
            get { return this.AsmSim_Show_Unreachable_Instructions_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Unreachable_Instructions_UI.IsChecked = value; }
        }

        public bool AsmSim_Decorate_Unreachable_Instructions
        {
            get { return this.AsmSim_Decorate_Unreachable_Instructions_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Unreachable_Instructions_UI.IsChecked = value; }
        }

        public bool AsmSim_Decorate_Registers
        {
            get { return this.AsmSim_Decorate_Registers_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Registers_UI.IsChecked = value; }
        }

        public bool AsmSim_Show_Register_In_Code_Completion
        {
            get { return this.AsmSim_Show_Register_In_Code_Completion_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Register_In_Code_Completion_UI.IsChecked = value; }
        }

        public NumerationEnum AsmSim_Show_Register_In_Code_Completion_Numeration
        {
            get { return AsmSourceTools.ParseNumeration(this.AsmSim_Show_Register_In_Code_Completion_Numeration_UI.Text, false); }
            set { this.AsmSim_Show_Register_In_Code_Completion_Numeration_UI.Text = value.ToString(); }
        }

        public bool AsmSim_Show_Register_In_Register_Tooltip
        {
            get { return this.AsmSim_Show_Register_In_Register_Tooltip_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Register_In_Register_Tooltip_UI.IsChecked = value; }
        }

        public NumerationEnum AsmSim_Show_Register_In_Register_Tooltip_Numeration
        {
            get { return AsmSourceTools.ParseNumeration(this.AsmSim_Show_Register_In_Register_Tooltip_Numeration_UI.Text, false); }
            set { this.AsmSim_Show_Register_In_Register_Tooltip_Numeration_UI.Text = value.ToString(); }
        }

        public bool AsmSim_Show_Register_In_Instruction_Tooltip
        {
            get { return this.AsmSim_Show_Register_In_Instruction_Tooltip_UI.IsChecked ?? false; }
            set { this.AsmSim_Show_Register_In_Instruction_Tooltip_UI.IsChecked = value; }
        }

        public NumerationEnum AsmSim_Show_Register_In_Instruction_Tooltip_Numeration
        {
            get { return AsmSourceTools.ParseNumeration(this.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration_UI.Text, false); }
            set { this.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration_UI.Text = value.ToString(); }
        }

        public bool AsmSim_Decorate_Unimplemented
        {
            get { return this.AsmSim_Decorate_Unimplemented_UI.IsChecked ?? false; }
            set { this.AsmSim_Decorate_Unimplemented_UI.IsChecked = value; }
        }

        public string AsmSim_Pragma_Assume
        {
            get { return this.AsmSim_Pragma_Assume_UI.Text; }
            set { this.AsmSim_Pragma_Assume_UI.Text = value; }
        }
        #endregion
    }
}