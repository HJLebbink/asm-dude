// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmDude.OptionsPage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    public enum PropertyEnum
    {
        Global_MaxFileLines,

        AsmDoc_On,
        AsmDoc_Url,

        CodeFolding_On,
        CodeFolding_IsDefaultCollapsed,
        CodeFolding_BeginTag,
        CodeFolding_EndTag,

        SyntaxHighlighting_On,
        SyntaxHighlighting_Opcode,
        SyntaxHighlighting_Opcode_Italic,
        SyntaxHighlighting_Register,
        SyntaxHighlighting_Register_Italic,
        SyntaxHighlighting_Remark,
        SyntaxHighlighting_Remark_Italic,
        SyntaxHighlighting_Directive,
        SyntaxHighlighting_Directive_Italic,
        SyntaxHighlighting_Constant,
        SyntaxHighlighting_Constant_Italic,
        SyntaxHighlighting_Jump,
        SyntaxHighlighting_Jump_Italic,
        SyntaxHighlighting_Label,
        SyntaxHighlighting_Label_Italic,
        SyntaxHighlighting_Misc,
        SyntaxHighlighting_Misc_Italic,
        SyntaxHighlighting_Userdefined1,
        SyntaxHighlighting_Userdefined1_Italic,
        SyntaxHighlighting_Userdefined2,
        SyntaxHighlighting_Userdefined2_Italic,
        SyntaxHighlighting_Userdefined3,
        SyntaxHighlighting_Userdefined3_Italic,

        KeywordHighlighting_BackgroundColor_On,
        KeywordHighlighting_BackgroundColor,
        KeywordHighlighting_BorderColor_On,
        KeywordHighlighting_BorderColor,

        PerformanceInfo_On,
        PerformanceInfo_IsDefaultCollapsed,
        PerformanceInfo_SandyBridge_On,
        PerformanceInfo_IvyBridge_On,
        PerformanceInfo_Haswell_On,
        PerformanceInfo_Broadwell_On,
        PerformanceInfo_Skylake_On,
        PerformanceInfo_SkylakeX_On,
        PerformanceInfo_KnightsLanding_On,

        CodeCompletion_On,
        SignatureHelp_On,

        IntelliSense_Label_Analysis_On,
        IntelliSense_Show_Undefined_Labels,
        IntelliSense_Decorate_Undefined_Labels,
        IntelliSense_Show_Clashing_Labels,
        IntelliSense_Decorate_Clashing_Labels,
        IntelliSense_Show_Undefined_Includes,
        IntelliSense_Decorate_Undefined_Includes,

        AsmSim_On,
        AsmSim_Z3_Timeout_MS,
        AsmSim_Number_Of_Threads,
        AsmSim_64_Bits,
        AsmSim_Show_Syntax_Errors,
        AsmSim_Decorate_Syntax_Errors,
        AsmSim_Show_Usage_Of_Undefined,
        AsmSim_Decorate_Usage_Of_Undefined,
        AsmSim_Show_Redundant_Instructions,
        AsmSim_Decorate_Redundant_Instructions,
        AsmSim_Show_Unreachable_Instructions,

        AsmSim_Decorate_Unreachable_Instructions,
        AsmSim_Decorate_Registers,
        AsmSim_Show_Register_In_Code_Completion,
        AsmSim_Show_Register_In_Code_Completion_Numeration,
        AsmSim_Show_Register_In_Register_Tooltip,
        AsmSim_Show_Register_In_Register_Tooltip_Numeration,
        AsmSim_Show_Register_In_Instruction_Tooltip,
        AsmSim_Show_Register_In_Instruction_Tooltip_Numeration,
        AsmSim_Decorate_Unimplemented,
        AsmSim_Pragma_Assume,
    }

    [Guid(Guids.GuidOptionsPageAsmDude)]
    public class AsmDudeOptionsPage : UIElementDialogPage
    {
        private readonly AsmDudeOptionsPageUI _asmDudeOptionsPageUI;

        public AsmDudeOptionsPage()
        {
            this._asmDudeOptionsPageUI = new AsmDudeOptionsPageUI();
        }

        protected override System.Windows.UIElement Child
        {
            get { return this._asmDudeOptionsPageUI; }
        }

        #region Private Methods

        private bool Setting_Changed(string key, StringBuilder sb)
        {
            object persisted_value = Settings.Default[key];
            object gui_value = this._asmDudeOptionsPageUI.GetPropValue(key);
            if (gui_value.Equals(persisted_value))
            {
                return false;
            }
            sb.AppendLine(key + ": old = " + persisted_value + "; new = " + gui_value);
            return true;
        }

        private bool Setting_Changed(PropertyEnum key, StringBuilder sb)
        {
            return this.Setting_Changed(key.ToString(), sb);
        }

        private bool Setting_Changed(Arch key, StringBuilder sb)
        {
            return (key == Arch.ARCH_NONE) ? false : this.Setting_Changed(key.ToString(), sb);
        }

        private bool Setting_Changed_RGB(PropertyEnum key, StringBuilder sb)
        {
            string k = key.ToString();
            Color persisted_value = (Color)Settings.Default[k];
            Color gui_value = (Color)this._asmDudeOptionsPageUI.GetPropValue(k);

            if (gui_value.ToArgb() != persisted_value.ToArgb())
            {
                sb.AppendLine(k + " old " + persisted_value.Name + "; new " + gui_value.Name);
                return true;
            }
            return false;
        }

        private bool Setting_Update(string key)
        {
            object persisted_value = Settings.Default[key];
            object gui_value = this._asmDudeOptionsPageUI.GetPropValue(key);
            if (gui_value.Equals(persisted_value))
            {
                return false;
            }
            Settings.Default[key] = gui_value;
            return true;
        }

        private bool Setting_Update(PropertyEnum key)
        {
            return this.Setting_Update(key.ToString());
        }

        private bool Setting_Update(Arch key)
        {
            return (key == Arch.ARCH_NONE) ? false : this.Setting_Update(key.ToString());
        }

        private bool Setting_Update_RGB(PropertyEnum key)
        {
            string k = key.ToString();
            Color persisted_value = (Color)Settings.Default[k];
            Color gui_value = (Color)this._asmDudeOptionsPageUI.GetPropValue(k);

            if (gui_value.ToArgb() != persisted_value.ToArgb())
            {
                Settings.Default[k] = this._asmDudeOptionsPageUI.GetPropValue(k);
                return true;
            }
            return false;
        }

        private void Set_GUI(PropertyEnum key)
        {
            string k = key.ToString();
            this._asmDudeOptionsPageUI.SetPropValue(k, Settings.Default[k]);
        }

        private void Set_GUI_ARCH(Arch arch)
        {
            string MakeToolTip()
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
                    sb.Append(docArch + ":\n");
                }
                if (usedMnemonics.Count > 0)
                {
                    foreach (Mnemonic mnemonic in usedMnemonics)
                    {
                        sb.Append(mnemonic.ToString());
                        sb.Append(", ");
                    }
                    sb.Length -= 2; // get rid of last comma.
                }
                else
                {
                    sb.Append("empty");
                }
                return AsmSourceTools.Linewrap(sb.ToString(), AsmDudePackage.MaxNumberOfCharsInToolTips);
            }
            void SetToolTip(string tooltip)
            {
                switch (arch)
                {
                    case Arch.ARCH_NONE: break;
                    case Arch.ARCH_8086: this._asmDudeOptionsPageUI.ARCH_8086_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_186: this._asmDudeOptionsPageUI.ARCH_186_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_286: this._asmDudeOptionsPageUI.ARCH_286_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_386: this._asmDudeOptionsPageUI.ARCH_386_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_486: this._asmDudeOptionsPageUI.ARCH_486_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_PENT: this._asmDudeOptionsPageUI.ARCH_PENT_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_P6: this._asmDudeOptionsPageUI.ARCH_P6_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_MMX: this._asmDudeOptionsPageUI.ARCH_MMX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE: this._asmDudeOptionsPageUI.ARCH_SSE_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE2: this._asmDudeOptionsPageUI.ARCH_SSE2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE3: this._asmDudeOptionsPageUI.ARCH_SSE3_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSSE3: this._asmDudeOptionsPageUI.ARCH_SSSE3_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE4_1: this._asmDudeOptionsPageUI.ARCH_SSE4_1_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE4_2: this._asmDudeOptionsPageUI.ARCH_SSE4_2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE4A: this._asmDudeOptionsPageUI.ARCH_SSE4A_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SSE5: this._asmDudeOptionsPageUI.ARCH_SSE5_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX: this._asmDudeOptionsPageUI.ARCH_AVX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX2: this._asmDudeOptionsPageUI.ARCH_AVX2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_F: this._asmDudeOptionsPageUI.ARCH_AVX512_F_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_CD: this._asmDudeOptionsPageUI.ARCH_AVX512_CD_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_ER: this._asmDudeOptionsPageUI.ARCH_AVX512_ER_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_PF: this._asmDudeOptionsPageUI.ARCH_AVX512_PF_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_BW: this._asmDudeOptionsPageUI.ARCH_AVX512_BW_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_DQ: this._asmDudeOptionsPageUI.ARCH_AVX512_DQ_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VL: this._asmDudeOptionsPageUI.ARCH_AVX512_VL_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_IFMA: this._asmDudeOptionsPageUI.ARCH_AVX512_IFMA_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VBMI: this._asmDudeOptionsPageUI.ARCH_AVX512_VBMI_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VPOPCNTDQ: this._asmDudeOptionsPageUI.ARCH_AVX512_VPOPCNTDQ_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_4VNNIW: this._asmDudeOptionsPageUI.ARCH_AVX512_4VNNIW_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_4FMAPS: this._asmDudeOptionsPageUI.ARCH_AVX512_4FMAPS_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VBMI2: this._asmDudeOptionsPageUI.ARCH_AVX512_VBMI2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VNNI: this._asmDudeOptionsPageUI.ARCH_AVX512_VNNI_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_BITALG: this._asmDudeOptionsPageUI.ARCH_AVX512_BITALG_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_GFNI: this._asmDudeOptionsPageUI.ARCH_AVX512_GFNI_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VAES: this._asmDudeOptionsPageUI.ARCH_AVX512_VAES_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VPCLMULQDQ: this._asmDudeOptionsPageUI.ARCH_AVX512_VPCLMULQDQ_UI.ToolTip = tooltip; break;

                    case Arch.ARCH_AVX512_BF16: this._asmDudeOptionsPageUI.ARCH_AVX512_BF16_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AVX512_VP2INTERSECT: this._asmDudeOptionsPageUI.ARCH_AVX512_VP2INTERSECT_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_ENQCMD: this._asmDudeOptionsPageUI.ARCH_ENQCMD_UI.ToolTip = tooltip; break;

                    case Arch.ARCH_ADX: this._asmDudeOptionsPageUI.ARCH_ADX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AES: this._asmDudeOptionsPageUI.ARCH_AES_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_VMX: this._asmDudeOptionsPageUI.ARCH_VMX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_BMI1: this._asmDudeOptionsPageUI.ARCH_BMI1_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_BMI2: this._asmDudeOptionsPageUI.ARCH_BMI2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_F16C: this._asmDudeOptionsPageUI.ARCH_F16C_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_FMA: this._asmDudeOptionsPageUI.ARCH_FMA_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_FSGSBASE: this._asmDudeOptionsPageUI.ARCH_FSGSBASE_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_HLE: this._asmDudeOptionsPageUI.ARCH_HLE_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_INVPCID: this._asmDudeOptionsPageUI.ARCH_INVPCID_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SHA: this._asmDudeOptionsPageUI.ARCH_SHA_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_RTM: this._asmDudeOptionsPageUI.ARCH_RTM_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_MPX: this._asmDudeOptionsPageUI.ARCH_MPX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_PCLMULQDQ: this._asmDudeOptionsPageUI.ARCH_PCLMULQDQ_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_LZCNT: this._asmDudeOptionsPageUI.ARCH_LZCNT_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_PREFETCHWT1: this._asmDudeOptionsPageUI.ARCH_PREFETCHWT1_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_PRFCHW: this._asmDudeOptionsPageUI.ARCH_PRFCHW_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_RDPID: this._asmDudeOptionsPageUI.ARCH_RDPID_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_RDRAND: this._asmDudeOptionsPageUI.ARCH_RDRAND_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_RDSEED: this._asmDudeOptionsPageUI.ARCH_RDSEED_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_XSAVEOPT: this._asmDudeOptionsPageUI.ARCH_XSAVEOPT_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SGX1: this._asmDudeOptionsPageUI.ARCH_SGX1_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SGX2: this._asmDudeOptionsPageUI.ARCH_SGX2_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_SMX: this._asmDudeOptionsPageUI.ARCH_SMX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_CLDEMOTE: this._asmDudeOptionsPageUI.ARCH_CLDEMOTE_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_MOVDIR64B: this._asmDudeOptionsPageUI.ARCH_MOVDIR64B_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_MOVDIRI: this._asmDudeOptionsPageUI.ARCH_MOVDIRI_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_PCONFIG: this._asmDudeOptionsPageUI.ARCH_PCONFIG_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_WAITPKG: this._asmDudeOptionsPageUI.ARCH_WAITPKG_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_X64: this._asmDudeOptionsPageUI.ARCH_X64_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_IA64: this._asmDudeOptionsPageUI.ARCH_IA64_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_UNDOC: this._asmDudeOptionsPageUI.ARCH_UNDOC_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_AMD: this._asmDudeOptionsPageUI.ARCH_AMD_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_TBM: this._asmDudeOptionsPageUI.ARCH_TBM_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_3DNOW: this._asmDudeOptionsPageUI.ARCH_3DNOW_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_CYRIX: this._asmDudeOptionsPageUI.ARCH_CYRIX_UI.ToolTip = tooltip; break;
                    case Arch.ARCH_CYRIXM: this._asmDudeOptionsPageUI.ARCH_CYRIXM_UI.ToolTip = tooltip; break;
                    default:
                        break;
                }
            }

            if (arch == Arch.ARCH_NONE)
            {
                return;
            }

            string k = arch.ToString();
            this._asmDudeOptionsPageUI.SetPropValue(k, Settings.Default[k]);
            SetToolTip(MakeToolTip());
        }

        private void Set_Settings(PropertyEnum key)
        {
            string k = key.ToString();
            Settings.Default[k] = this._asmDudeOptionsPageUI.GetPropValue(k);
        }
        #endregion

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
            this._asmDudeOptionsPageUI.UsedAssemblerDisassemblyWindow = AsmDudeToolsStatic.Used_Assembler_Disassembly_Window;

            #region Global
            this.Set_GUI(PropertyEnum.Global_MaxFileLines);
            #endregion

            #region AsmDoc
            this.Set_GUI(PropertyEnum.AsmDoc_On);
            this.Set_GUI(PropertyEnum.AsmDoc_Url);
            #endregion

            #region CodeFolding
            this.Set_GUI(PropertyEnum.CodeFolding_On);
            this.Set_GUI(PropertyEnum.CodeFolding_IsDefaultCollapsed);
            this.Set_GUI(PropertyEnum.CodeFolding_BeginTag);
            this.Set_GUI(PropertyEnum.CodeFolding_EndTag);
            #endregion

            #region Syntax Highlighting
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_On);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Opcode);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Opcode_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Register);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Register_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Remark);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Remark_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Directive);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Directive_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Constant);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Constant_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Jump);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Jump_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Label);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Label_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Misc);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Misc_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined1);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined1_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined2);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined2_Italic);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined3);
            this.Set_GUI(PropertyEnum.SyntaxHighlighting_Userdefined3_Italic);
            #endregion

            #region Keyword Highlighting
            this.Set_GUI(PropertyEnum.KeywordHighlighting_BackgroundColor_On);
            this.Set_GUI(PropertyEnum.KeywordHighlighting_BackgroundColor);
            this.Set_GUI(PropertyEnum.KeywordHighlighting_BorderColor_On);
            this.Set_GUI(PropertyEnum.KeywordHighlighting_BorderColor);
            #endregion

            #region Latency and Throughput Information (Performance Info)
            this.Set_GUI(PropertyEnum.PerformanceInfo_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_IsDefaultCollapsed);
            this.Set_GUI(PropertyEnum.PerformanceInfo_SandyBridge_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_IvyBridge_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_Haswell_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_Broadwell_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_Skylake_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_SkylakeX_On);
            this.Set_GUI(PropertyEnum.PerformanceInfo_KnightsLanding_On);
            #endregion

            #region Code Completion
            this.Set_GUI(PropertyEnum.CodeCompletion_On);
            this.Set_GUI(PropertyEnum.SignatureHelp_On);
            #endregion

            #region ARCH
            foreach (Arch arch in Enum.GetValues(typeof(Arch)))
            {
                this.Set_GUI_ARCH(arch);
            }
            #endregion

            #region Intellisense
            this.Set_GUI(PropertyEnum.IntelliSense_Label_Analysis_On);
            this.Set_GUI(PropertyEnum.IntelliSense_Show_Undefined_Labels);
            this.Set_GUI(PropertyEnum.IntelliSense_Decorate_Undefined_Labels);
            this.Set_GUI(PropertyEnum.IntelliSense_Show_Clashing_Labels);
            this.Set_GUI(PropertyEnum.IntelliSense_Decorate_Clashing_Labels);
            this.Set_GUI(PropertyEnum.IntelliSense_Show_Undefined_Includes);
            this.Set_GUI(PropertyEnum.IntelliSense_Decorate_Undefined_Includes);
            #endregion

            #region AsmSim
            this.Set_GUI(PropertyEnum.AsmSim_On);
            this.Set_GUI(PropertyEnum.AsmSim_Z3_Timeout_MS);
            this.Set_GUI(PropertyEnum.AsmSim_Number_Of_Threads);
            this.Set_GUI(PropertyEnum.AsmSim_64_Bits);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Syntax_Errors);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Syntax_Errors);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Usage_Of_Undefined);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Usage_Of_Undefined);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Redundant_Instructions);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Redundant_Instructions);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Unreachable_Instructions);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Unreachable_Instructions);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Registers);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Register_In_Code_Completion);
            //TODO
            this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Code_Completion_Numeration = AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Register_In_Register_Tooltip);
            //TODO
            this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Register_Tooltip_Numeration = AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration);
            this.Set_GUI(PropertyEnum.AsmSim_Show_Register_In_Instruction_Tooltip);
            //TODO
            this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration);
            this.Set_GUI(PropertyEnum.AsmSim_Decorate_Unimplemented);
            this.Set_GUI(PropertyEnum.AsmSim_Pragma_Assume);
            #endregion
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

            #region Global
            changed |= this.Setting_Changed(PropertyEnum.Global_MaxFileLines, sb);
            #endregion

            #region Assembly Flavour
            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                sb.AppendLine("UsedAssembler_MainWindow=" + this._asmDudeOptionsPageUI.UsedAssembler);
                changed = true;
            }
            if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window != this._asmDudeOptionsPageUI.UsedAssemblerDisassemblyWindow)
            {
                sb.AppendLine("UsedAssembler_DisassemblyWindow=" + this._asmDudeOptionsPageUI.UsedAssemblerDisassemblyWindow);
                changed = true;
            }
            #endregion

            #region AsmDoc
            changed |= this.Setting_Changed(PropertyEnum.AsmDoc_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmDoc_Url, sb);
            #endregion

            #region CodeFolding
            changed |= this.Setting_Changed(PropertyEnum.CodeFolding_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.CodeFolding_IsDefaultCollapsed, sb);
            changed |= this.Setting_Changed(PropertyEnum.CodeFolding_BeginTag, sb);
            changed |= this.Setting_Changed(PropertyEnum.CodeFolding_EndTag, sb);
            #endregion

            #region Syntax Highlighting
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_On, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Opcode, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Opcode_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Register, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Register_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Remark, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Remark_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Directive, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Directive_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Constant, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Constant_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Jump, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Jump_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Label, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Label_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Misc, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Misc_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Userdefined1, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Userdefined1_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Userdefined2, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Userdefined2_Italic, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.SyntaxHighlighting_Userdefined3, sb);
            changed |= this.Setting_Changed(PropertyEnum.SyntaxHighlighting_Userdefined3_Italic, sb);
            #endregion

            #region Keyword Highlighting
            changed |= this.Setting_Changed(PropertyEnum.KeywordHighlighting_BackgroundColor_On, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.KeywordHighlighting_BackgroundColor, sb);
            changed |= this.Setting_Changed(PropertyEnum.KeywordHighlighting_BorderColor_On, sb);
            changed |= this.Setting_Changed_RGB(PropertyEnum.KeywordHighlighting_BorderColor, sb);
            #endregion

            #region Latency and Throughput Information (Performance Info)
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_IsDefaultCollapsed, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_SandyBridge_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_IvyBridge_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_Haswell_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_Broadwell_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_Skylake_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_SkylakeX_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.PerformanceInfo_KnightsLanding_On, sb);
            #endregion

            #region Code Completion
            changed |= this.Setting_Changed(PropertyEnum.CodeCompletion_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.SignatureHelp_On, sb);
            #endregion

            #region ARCH
            foreach (Arch arch in Enum.GetValues(typeof(Arch)))
            {
                changed |= this.Setting_Changed(arch, sb);
            }
            #endregion

            #region Intellisense
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Label_Analysis_On, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Show_Undefined_Labels, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Show_Clashing_Labels, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Decorate_Undefined_Labels, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Decorate_Clashing_Labels, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Show_Undefined_Includes, sb);
            changed |= this.Setting_Changed(PropertyEnum.IntelliSense_Decorate_Undefined_Includes, sb);
            #endregion

            #region AsmSim
            if (this.Setting_Changed(PropertyEnum.AsmSim_On, sb))
            {
                changed = true;
                if (!this._asmDudeOptionsPageUI.AsmSim_On)
                {
                    string title = null;
                    string message = "I'm sorry " + Environment.UserName + ", I'm afraid I can't do that.";
                    int result = VsShellUtilities.ShowMessageBox(this.Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Z3_Timeout_MS, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Number_Of_Threads, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_64_Bits, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Syntax_Errors, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Syntax_Errors, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Usage_Of_Undefined, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Usage_Of_Undefined, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Redundant_Instructions, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Redundant_Instructions, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Unreachable_Instructions, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Unreachable_Instructions, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Registers, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Register_In_Code_Completion, sb);
            if (Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Code_Completion_Numeration.ToString())
            {
                sb.AppendLine("AsmSim_Show_Register_In_Code_Completion_Numeration=" + this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Code_Completion_Numeration);
                changed = true;
            }
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Register_In_Register_Tooltip, sb);
            if (Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Register_Tooltip_Numeration.ToString())
            {
                sb.AppendLine("AsmSim_Show_Register_In_Register_Tooltip_Numeration=" + this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Register_Tooltip_Numeration);
                changed = true;
            }
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Show_Register_In_Instruction_Tooltip, sb);
            if (Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration.ToString())
            {
                sb.AppendLine("AsmSim_Show_Register_In_Instruction_Tooltip_Numeration=" + this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration);
                changed = true;
            }
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Decorate_Unimplemented, sb);
            changed |= this.Setting_Changed(PropertyEnum.AsmSim_Pragma_Assume, sb);
            #endregion

            if (changed)
            {
                string title = "Microsoft Visual Studio";
                string text = "Unsaved changes exist.\n\n" + sb.ToString() + "\nWould you like to save?";

                if (MessageBox.Show(text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    this.SaveAsync().ConfigureAwait(false);
                }
                else
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
            this.SaveAsync().ConfigureAwait(false);
            base.OnApply(e);
        }

        private async System.Threading.Tasks.Task UpdateFontAsync(string colorKeyName, Color c)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            Guid guid2 = Guid.Parse("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}");
            __FCSTORAGEFLAGS flags = __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES;

            IVsFontAndColorStorage store = this.GetService(typeof(SVsFontAndColorStorage)) as IVsFontAndColorStorage;
            if (store.OpenCategory(ref guid2, (uint)flags) != VSConstants.S_OK)
            {
                return;
            }

            _ = store.SetItem(colorKeyName, new[] { new ColorableItemInfo { bForegroundValid = 1, crForeground = (uint)ColorTranslator.ToWin32(c) } });
            store.CloseCategory();
        }

        private async System.Threading.Tasks.Task UpdateItalicAsync(string colorKeyName, bool b)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            Guid textEditorFontCategoryGuid = Guid.Parse("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}");
            __FCSTORAGEFLAGS flags = __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES;

            IVsFontAndColorStorage store = this.GetService(typeof(SVsFontAndColorStorage)) as IVsFontAndColorStorage;
            if (store.OpenCategory(ref textEditorFontCategoryGuid, (uint)flags) != VSConstants.S_OK)
            {
                return;
            }

            ColorableItemInfo x = new ColorableItemInfo
            {
                //bFontFlagsValid = 1,
                //dwFontFlags = 1 // bold
                //dwFontFlags = 2 // italic
                //dwFontFlags = 4 // nothing
                //dwFontFlags = 8
            };

            ColorableItemInfo[] y = new[] { x };
            AsmDudeToolsStatic.Output_INFO("UpdateItalic: flags before: " + y[0].dwFontFlags);
            store.GetItem(colorKeyName, y);
            AsmDudeToolsStatic.Output_INFO("UpdateItalic: flags before save: " + y[0].dwFontFlags);
            if (b)
            {
                y[0].dwFontFlags = y[0].dwFontFlags | 0x0002;
            }
            else
            {
                y[0].dwFontFlags = y[0].dwFontFlags & 0xFFFD;
            }
            y[0].bFontFlagsValid = 1;

            AsmDudeToolsStatic.Output_INFO("UpdateItalic: flags after save:  " + y[0].dwFontFlags);

            store.SetItem(colorKeyName, y);
            store.CloseCategory();

            /*
            if (store.OpenCategory(ref TextEditorFontCategoryGuid, (uint)flags) != VSConstants.S_OK) return;
            SelectedTextSymbol symbol = sender as SelectedTextSymbol;


            IClassificationFormatMap map = symbol.ClassificationMap;
            map.BeginBatchUpdate();
            TextFormattingRunProperties props = map.GetTextProperties(symbol.ClassificationType);

            props = props.SetItalic(symbol.Italic);
            map.SetTextProperties(symbol.ClassificationType, props);
            map.EndBatchUpdate();
            store.CloseCategory();
            */
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:save", this.ToString()));
            bool changed = false;
            bool restartNeeded = false;
            bool archChanged = false;

            #region Global
            if (this.Setting_Update(PropertyEnum.Global_MaxFileLines)) { changed = true; }
            #endregion

            #region Assembler Flavour
            if (AsmDudeToolsStatic.Used_Assembler != this._asmDudeOptionsPageUI.UsedAssembler)
            {
                AsmDudeToolsStatic.Used_Assembler = this._asmDudeOptionsPageUI.UsedAssembler;
                changed = true;
                restartNeeded = true;
            }
            if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window != this._asmDudeOptionsPageUI.UsedAssemblerDisassemblyWindow)
            {
                AsmDudeToolsStatic.Used_Assembler_Disassembly_Window = this._asmDudeOptionsPageUI.UsedAssemblerDisassemblyWindow;
                changed = true;
                restartNeeded = true;
            }
            #endregion
            if (this.Setting_Update(PropertyEnum.CodeFolding_On)) { changed = true; restartNeeded = true; }
            #region AsmDoc
            if (this.Setting_Update(PropertyEnum.AsmDoc_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmDoc_Url)) { changed = true; restartNeeded = true; }
            #endregion

            #region CodeFolding
            if (this.Setting_Update(PropertyEnum.CodeFolding_On)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.CodeFolding_IsDefaultCollapsed)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.CodeFolding_BeginTag)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.CodeFolding_EndTag)) { changed = true; restartNeeded = true; }
            #endregion

            #region Syntax Highlighting
            {
                bool refreshRegistry = false;

                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_On)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Opcode))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic, this._asmDudeOptionsPageUI.SyntaxHighlighting_Opcode);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Opcode_Italic))
                {
                    //TODO fix that toggling italic is displayed immediately
                    //UpdateItalic(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic, this._asmDudeOptionsPageUI.ColorMnemonic_Italic);
                    changed = true; restartNeeded = true;
                }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Register))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Register, this._asmDudeOptionsPageUI.SyntaxHighlighting_Register);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Register_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Remark))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Remark, this._asmDudeOptionsPageUI.SyntaxHighlighting_Remark);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Remark_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Directive))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Directive, this._asmDudeOptionsPageUI.SyntaxHighlighting_Directive);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Directive_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Constant))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Constant, this._asmDudeOptionsPageUI.SyntaxHighlighting_Constant);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Constant_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Jump))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Jump, this._asmDudeOptionsPageUI.SyntaxHighlighting_Jump);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Jump_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Label))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Label, this._asmDudeOptionsPageUI.SyntaxHighlighting_Label);
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.LabelDef, this._asmDudeOptionsPageUI.SyntaxHighlighting_Label);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Label_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Misc))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.Misc, this._asmDudeOptionsPageUI.SyntaxHighlighting_Misc);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Misc_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Userdefined1))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.UserDefined1, this._asmDudeOptionsPageUI.SyntaxHighlighting_Userdefined1);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Userdefined1_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Userdefined2))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.UserDefined2, this._asmDudeOptionsPageUI.SyntaxHighlighting_Userdefined2);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Userdefined2_Italic)) { changed = true; restartNeeded = true; }
                if (this.Setting_Update_RGB(PropertyEnum.SyntaxHighlighting_Userdefined3))
                {
                    await this.UpdateFontAsync(AsmClassificationDefinition.ClassificationTypeNames.UserDefined3, this._asmDudeOptionsPageUI.SyntaxHighlighting_Userdefined3);
                    changed = true; refreshRegistry = true;
                }
                if (this.Setting_Update(PropertyEnum.SyntaxHighlighting_Userdefined3_Italic)) { changed = true; restartNeeded = true; }

                if (refreshRegistry)
                {
                    IVsFontAndColorCacheManager cacheManager = this.GetService(typeof(SVsFontAndColorCacheManager)) as IVsFontAndColorCacheManager;
                    cacheManager.ClearAllCaches();
                    Guid guid = new Guid("00000000-0000-0000-0000-000000000000");
                    cacheManager.RefreshCache(ref guid);
                    guid = new Guid("{A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0}"); // Text editor category
                }
            }
            #endregion

            #region Keyword Highlighting
            if (this.Setting_Update(PropertyEnum.KeywordHighlighting_BackgroundColor_On)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update_RGB(PropertyEnum.KeywordHighlighting_BackgroundColor)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.KeywordHighlighting_BorderColor_On)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update_RGB(PropertyEnum.KeywordHighlighting_BorderColor)) { changed = true; restartNeeded = true; }
            #endregion

            #region Latency and Throughput Information (Performance Info)
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_IsDefaultCollapsed)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_SandyBridge_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_IvyBridge_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_Haswell_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_Broadwell_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_Skylake_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_SkylakeX_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.PerformanceInfo_KnightsLanding_On)) { changed = true; }
            #endregion

            #region Code Completion
            if (this.Setting_Update(PropertyEnum.CodeCompletion_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.SignatureHelp_On)) { changed = true; }
            #endregion

            #region ARCH
            foreach (Arch arch in Enum.GetValues(typeof(Arch)))
            {
                if (this.Setting_Update(arch))
                {
                    changed = true; archChanged = true;
                }
            }
            #endregion

            #region Intellisense
            if (this.Setting_Update(PropertyEnum.IntelliSense_Label_Analysis_On)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Show_Undefined_Labels)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Show_Clashing_Labels)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Decorate_Undefined_Labels)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Decorate_Clashing_Labels)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Show_Undefined_Includes)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.IntelliSense_Decorate_Undefined_Includes)) { changed = true; }
            #endregion

            #region AsmSim
            if (this.Setting_Update(PropertyEnum.AsmSim_On)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Z3_Timeout_MS)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Number_Of_Threads)) { changed = true; restartNeeded = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_64_Bits)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Syntax_Errors)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Syntax_Errors)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Usage_Of_Undefined)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Usage_Of_Undefined)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Redundant_Instructions)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Redundant_Instructions)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Unreachable_Instructions)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Unreachable_Instructions)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Registers)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Register_In_Code_Completion)) { changed = true; }
            if (Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Code_Completion_Numeration.ToString())
            {
                Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration = this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Code_Completion_Numeration.ToString();
                changed = true;
            }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Register_In_Register_Tooltip)) { changed = true; }
            if (Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Register_Tooltip_Numeration.ToString())
            {
                Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration = this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Register_Tooltip_Numeration.ToString();
                changed = true;
            }
            if (this.Setting_Update(PropertyEnum.AsmSim_Show_Register_In_Instruction_Tooltip)) { changed = true; }
            if (Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration != this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration.ToString())
            {
                Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = this._asmDudeOptionsPageUI.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration.ToString();
                changed = true;
            }
            if (this.Setting_Update(PropertyEnum.AsmSim_Decorate_Unimplemented)) { changed = true; }
            if (this.Setting_Update(PropertyEnum.AsmSim_Pragma_Assume)) { changed = true; }
            #endregion

            if (archChanged) //TODO HJ 02-06-19 changes will propagate before save-yes is hit
            {
                AsmDudeTools.Instance.UpdateMnemonicSwitchedOn();
                AsmDudeTools.Instance.UpdateRegisterSwitchedOn();
            }
            if (changed)
            {
                Settings.Default.Save();
                await ClearMefCache.ClearMefCache.ClearAsync();
            }
            if (restartNeeded)
            {
                string title = "Microsoft Visual Studio";
                string text1 = "Do you like to restart Visual Studio now?";

                if (MessageBox.Show(text1, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    await ClearMefCache.ClearMefCache.RestartAsync();
                }
                else
                {
                    if (false)
                    {
                        string text2 = "You may need to close and open assembly files, or \nrestart visual studio for the changes to take effect.";
                        MessageBox.Show(text2, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        #endregion Event Handlers
    }
}
