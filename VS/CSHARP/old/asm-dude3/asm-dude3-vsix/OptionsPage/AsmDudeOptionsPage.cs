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

namespace AsmDude3
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using AsmTools;
    using AsmDude3.Tools;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// PropertyEnum maps Settings property names to UI controls.
    /// Must match all 157 property names in Settings.Designer.cs exactly.
    /// </summary>
    public enum PropertyEnum
    {
        Global_MaxFileLines,

        AsmDoc_On,
        AsmDoc_Url,

        CodeFolding_On,
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

        PerformanceInfo_On,
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

        ARCH_8086,
        ARCH_186,
        ARCH_286,
        ARCH_386,
        ARCH_486,
        ARCH_PENT,
        ARCH_P6,
        ARCH_MMX,
        ARCH_SSE,
        ARCH_SSE2,
        ARCH_SSE3,
        ARCH_SSSE3,
        ARCH_SSE4_1,
        ARCH_SSE4_2,
        ARCH_SSE4A,
        ARCH_SSE5,
        ARCH_AVX,
        ARCH_AVX2,
        ARCH_AVX512_F,
        ARCH_AVX512_CD,
        ARCH_AVX512_ER,
        ARCH_AVX512_PF,
        ARCH_AVX512_BW,
        ARCH_AVX512_DQ,
        ARCH_AVX512_VL,
        ARCH_AVX512_IFMA,
        ARCH_AVX512_VBMI,
        ARCH_AVX512_VPOPCNTDQ,
        ARCH_AVX512_4VNNIW,
        ARCH_AVX512_4FMAPS,
        ARCH_AVX512_VBMI2,
        ARCH_AVX512_VNNI,
        ARCH_AVX512_BITALG,
        ARCH_AVX512_GFNI,
        ARCH_AVX512_VAES,
        ARCH_AVX512_VPCLMULQDQ,
        ARCH_AVX512_BF16,
        ARCH_AVX512_VP2INTERSECT,
        ARCH_ENQCMD,
        ARCH_ADX,
        ARCH_AES,
        ARCH_VMX,
        ARCH_BMI1,
        ARCH_BMI2,
        ARCH_F16C,
        ARCH_FMA,
        ARCH_FSGSBASE,
        ARCH_HLE,
        ARCH_INVPCID,
        ARCH_SHA,
        ARCH_RTM,
        ARCH_MPX,
        ARCH_PCLMULQDQ,
        ARCH_LZCNT,
        ARCH_PREFETCHWT1,
        ARCH_PRFCHW,
        ARCH_RDPID,
        ARCH_RDRAND,
        ARCH_RDSEED,
        ARCH_XSAVEOPT,
        ARCH_SGX1,
        ARCH_SGX2,
        ARCH_SMX,
        ARCH_CLDEMOTE,
        ARCH_MOVDIR64B,
        ARCH_MOVDIRI,
        ARCH_PCONFIG,
        ARCH_WAITPKG,
        ARCH_X64,
        ARCH_IA64,
        ARCH_UNDOC,
        ARCH_AMD,
        ARCH_TBM,
        ARCH_3DNOW,
        ARCH_CYRIX,
        ARCH_CYRIXM,
    }

    /// <summary>
    /// Options page for AsmDude3 - LSP-based architecture with hybrid approach:
    /// - Minimal UI validation (delegate to LSP server)
    /// - Direct settings persistence
    /// - Notify LSP server of changes via restart
    /// </summary>
    [Guid(Guids.GuidOptionsPageAsmDude)]
    public sealed class AsmDudeOptionsPage : UIElementDialogPage
    {
        private readonly AsmDudeOptionsPageUI _ui;

        public AsmDudeOptionsPage()
        {
            this._ui = new AsmDudeOptionsPageUI();
        }

        protected override UIElement Child
        {
            get { return this._ui; }
        }

        #region Private Helper Methods

        private bool Setting_Changed(string key, StringBuilder sb)
        {
            object persistedValue = Settings.Default[key];
            object guiValue = this._ui.GetPropValue(key);
            if (guiValue.Equals(persistedValue))
            {
                return false;
            }
            sb.AppendLine(key + ": old = " + persistedValue + "; new = " + guiValue);
            return true;
        }

        private bool Setting_Changed(PropertyEnum key, StringBuilder sb)
        {
            return this.Setting_Changed(key.ToString(), sb);
        }

        private bool Setting_Update(string key)
        {
            try
            {
                object persistedValue = Settings.Default[key];
                object guiValue = this._ui.GetPropValue(key);
                if (guiValue.Equals(persistedValue))
                {
                    return false;
                }
                Settings.Default[key] = guiValue;
                return true;
            }
            catch (System.Configuration.SettingsPropertyWrongTypeException ex)
            {
                System.Diagnostics.Debug.WriteLine($"AsmDudeOptionsPage.Setting_Update: TYPE MISMATCH for '{key}' - {ex.Message}");
                object guiValue = this._ui.GetPropValue(key);
                object persistedValue = Settings.Default[key];
                System.Diagnostics.Debug.WriteLine($"  GUI type: {guiValue?.GetType().Name ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"  Settings type: {persistedValue?.GetType().Name ?? "null"}");
                throw; // Re-throw to see the stack trace
            }
        }

        private bool Setting_Update(PropertyEnum key)
        {
            return this.Setting_Update(key.ToString());
        }

        private void Set_GUI(PropertyEnum key)
        {
            string k = key.ToString();
            this._ui.SetPropValue(k, Settings.Default[k]);
        }

        /// <summary>
        /// Safely sets GUI value, silently skipping properties that don't exist in Settings.
        /// </summary>
        private void SafeSet_GUI(PropertyEnum key)
        {
            try
            {
                this.Set_GUI(key);
            }
            catch (System.Configuration.SettingsPropertyNotFoundException)
            {
                // Property not defined in Settings.Designer.cs - skip silently
            }
            catch (InvalidCastException)
            {
                // Type mismatch between UI control and setting value - skip silently
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when Visual Studio activates the options page.
        /// Loads all current settings into UI controls.
        /// </summary>
        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);

            // Ensure defaults are properly initialized on first access
            // This mirrors the WinForms Settings behavior in AsmDude2
            EnsureDefaultsInitialized();

            #region Global
            this.SafeSet_GUI(PropertyEnum.Global_MaxFileLines);
            #endregion

            #region Assembly Flavour
            this._ui.UsedAssembler = AsmDudeToolsStatic.Used_Assembler;
            this._ui.UsedAssemblerDisassemblyWindow = AsmDudeToolsStatic.Used_Assembler_Disassembly_Window;
            #endregion

            #region AsmDoc
            this.SafeSet_GUI(PropertyEnum.AsmDoc_On);
            this.SafeSet_GUI(PropertyEnum.AsmDoc_Url);
            #endregion

            #region CodeFolding
            this.SafeSet_GUI(PropertyEnum.CodeFolding_On);
            this.SafeSet_GUI(PropertyEnum.CodeFolding_BeginTag);
            this.SafeSet_GUI(PropertyEnum.CodeFolding_EndTag);
            #endregion

            #region Syntax Highlighting
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_On);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Opcode);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Opcode_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Register);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Register_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Remark);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Remark_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Directive);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Directive_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Constant);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Constant_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Jump);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Jump_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Label);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Label_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Misc);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Misc_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined1);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined1_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined2);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined2_Italic);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined3);
            this.SafeSet_GUI(PropertyEnum.SyntaxHighlighting_Userdefined3_Italic);
            #endregion

            #region Performance Info
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_SandyBridge_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_IvyBridge_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_Haswell_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_Broadwell_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_Skylake_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_SkylakeX_On);
            this.SafeSet_GUI(PropertyEnum.PerformanceInfo_KnightsLanding_On);
            #endregion

            #region Code Completion and IntelliSense
            this.SafeSet_GUI(PropertyEnum.CodeCompletion_On);
            this.SafeSet_GUI(PropertyEnum.SignatureHelp_On);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Label_Analysis_On);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Show_Undefined_Labels);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Decorate_Undefined_Labels);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Show_Clashing_Labels);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Decorate_Clashing_Labels);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Show_Undefined_Includes);
            this.SafeSet_GUI(PropertyEnum.IntelliSense_Decorate_Undefined_Includes);
            #endregion

            #region Assembly Simulator
            this.SafeSet_GUI(PropertyEnum.AsmSim_On);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Z3_Timeout_MS);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Number_Of_Threads);
            this.SafeSet_GUI(PropertyEnum.AsmSim_64_Bits);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Syntax_Errors);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Syntax_Errors);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Usage_Of_Undefined);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Usage_Of_Undefined);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Redundant_Instructions);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Redundant_Instructions);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Unreachable_Instructions);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Unreachable_Instructions);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Registers);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Code_Completion);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Code_Completion_Numeration);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Register_Tooltip);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Register_Tooltip_Numeration);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Instruction_Tooltip);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Decorate_Unimplemented);
            this.SafeSet_GUI(PropertyEnum.AsmSim_Pragma_Assume);
            #endregion

            #region Architecture
            foreach (PropertyEnum arch in Enum.GetValues(typeof(PropertyEnum)))
            {
                if (arch.ToString().StartsWith("ARCH_"))
                {
                    this.SafeSet_GUI(arch);
                }
            }
            #endregion
        }

        /// <summary>
        /// Called when Visual Studio deactivates the options page.
        /// Detects and updates changed settings, then triggers LSP server restart.
        /// </summary>
        protected override void OnDeactivate(CancelEventArgs e)
        {
            bool changed = false;
            StringBuilder sb = new StringBuilder();

            // Check all settings for changes (skip properties that don't exist)
            foreach (PropertyEnum property in Enum.GetValues(typeof(PropertyEnum)))
            {
                try
                {
                    if (this.Setting_Changed(property, sb))
                    {
                        changed = true;
                    }
                }
                catch (System.Configuration.SettingsPropertyNotFoundException)
                {
                    // Property not defined in Settings.Designer.cs - skip silently
                }
                catch (InvalidCastException)
                {
                    // Type mismatch - skip silently
                }
            }

            // Special handling for assembler selections (not in PropertyEnum)
            if (AsmDudeToolsStatic.Used_Assembler != this._ui.UsedAssembler)
            {
                sb.AppendLine("UsedAssembler=" + this._ui.UsedAssembler);
                changed = true;
            }
            if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window != this._ui.UsedAssemblerDisassemblyWindow)
            {
                sb.AppendLine("UsedAssemblerDisassemblyWindow=" + this._ui.UsedAssemblerDisassemblyWindow);
                changed = true;
            }

            if (changed)
            {
                System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.OnDeactivate: Settings changed: " + sb.ToString());
            }

            base.OnDeactivate(e);
        }

        /// <summary>
        /// Called when user clicks Apply button.
        /// For UIElementDialogPage, this triggers SaveSettingsToStorage.
        /// </summary>
        protected override void OnApply(PageApplyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.OnApply: Apply button clicked");
            base.OnApply(e);
        }

        /// <summary>
        /// Ensures default settings are initialized on first load.
        /// This method accesses Settings.Default to trigger property initialization,
        /// which causes DefaultSettingValueAttribute values to be loaded into the registry.
        /// This mirrors the automatic default loading behavior of AsmDude2's WinForms Settings.
        /// </summary>
        private static void EnsureDefaultsInitialized()
        {
            try
            {
                var settings = Settings.Default;

                // Simply accessing Settings.Default triggers the application settings system
                // to check each property and load defaults from DefaultSettingValueAttribute
                // if the property hasn't been set in the registry yet.
                // We access a few key properties to ensure the system initializes them.

                // Key features that should be enabled by default
                _ = settings.SyntaxHighlighting_On;
                _ = settings.CodeFolding_On;
                _ = settings.AsmDoc_On;
                _ = settings.PerformanceInfo_On;
                _ = settings.PerformanceInfo_SkylakeX_On;
                _ = settings.useAssemblerAutoDetect;
                _ = settings.CodeCompletion_On;
                _ = settings.SignatureHelp_On;
                _ = settings.IntelliSense_Label_Analysis_On;
                _ = settings.IntelliSense_Show_Undefined_Labels;
                _ = settings.IntelliSense_Show_Undefined_Includes;

                // Save to ensure defaults are persisted to registry
                settings.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AsmDudeOptionsPage: Error initializing defaults: {ex.Message}");
            }
        }

        /// <summary>
        /// Load settings from storage (required by DialogPage).
        /// Called automatically by VS when the page is loaded.
        /// </summary>
        public override void LoadSettingsFromStorage()
        {
            // Settings are already loaded from registry via Settings.Default
            // OnActivate handles copying them to UI controls
            base.LoadSettingsFromStorage();
        }

        /// <summary>
        /// Save settings to storage (required by DialogPage).
        /// Called automatically by VS when OK or Apply is clicked.
        /// </summary>
        public override void SaveSettingsToStorage()
        {
            System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.SaveSettingsToStorage: Saving settings...");

            // Update all settings from UI to Settings.Default
            foreach (PropertyEnum property in Enum.GetValues(typeof(PropertyEnum)))
            {
                try
                {
                    this.Setting_Update(property);
                }
                catch (System.Configuration.SettingsPropertyNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AsmDudeOptionsPage.SaveSettingsToStorage: Property not found: {property} - {ex.Message}");
                    // Property not defined - skip
                }
                catch (System.Configuration.SettingsPropertyWrongTypeException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AsmDudeOptionsPage.SaveSettingsToStorage: Type mismatch for {property} - {ex.Message}");
                    // Type mismatch - skip this property and continue
                }
                catch (InvalidCastException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AsmDudeOptionsPage.SaveSettingsToStorage: Invalid cast for {property} - {ex.Message}");
                    // Type mismatch - skip
                }
            }

            // Update assembler selections
            if (this._ui.UsedAssembler != AsmDudeToolsStatic.Used_Assembler)
            {
                Settings.Default.useAssemblerAutoDetect = (this._ui.UsedAssembler == AssemblerEnum.AUTO_DETECT);
                Settings.Default.useAssemblerMasm = (this._ui.UsedAssembler == AssemblerEnum.MASM);
                Settings.Default.useAssemblerNasm = (this._ui.UsedAssembler == AssemblerEnum.NASM_INTEL);
                Settings.Default.useAssemblerNasm_Att = (this._ui.UsedAssembler == AssemblerEnum.NASM_ATT);
            }

            if (this._ui.UsedAssemblerDisassemblyWindow != AsmDudeToolsStatic.Used_Assembler_Disassembly_Window)
            {
                Settings.Default.useAssemblerDisassemblyAutoDetect = (this._ui.UsedAssemblerDisassemblyWindow == AssemblerEnum.AUTO_DETECT);
                Settings.Default.useAssemblerDisassemblyMasm = (this._ui.UsedAssemblerDisassemblyWindow == AssemblerEnum.MASM);
                Settings.Default.useAssemblerDisassemblyNasm_Att = (this._ui.UsedAssemblerDisassemblyWindow == AssemblerEnum.NASM_ATT);
            }

            // Persist to registry
            Settings.Default.Save();

            System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.SaveSettingsToStorage: Settings saved successfully.");

            // Restart LSP server to apply changes
            if (AsmLanguageClient.Instance != null)
            {
                System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.SaveSettingsToStorage: Restarting LSP server...");
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await AsmLanguageClient.Instance.RestartServerAsync();
                });
                System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.SaveSettingsToStorage: LSP server restart initiated.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("AsmDudeOptionsPage.SaveSettingsToStorage: WARNING - AsmLanguageClient.Instance is null, cannot restart LSP server.");
            }

            base.SaveSettingsToStorage();
        }

        #endregion
    }
}
