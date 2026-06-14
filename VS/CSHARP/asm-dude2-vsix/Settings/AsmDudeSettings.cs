// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

#pragma warning disable VSEXTPREVIEW_SETTINGS // Settings API is in preview

namespace AsmDude2.Settings;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

/// <summary>
/// AsmDude2 settings for Tools > Options.
/// </summary>
internal static class AsmDudeSettings
{
    // ─── Root category ───
    [VisualStudioContribution]
    internal static SettingCategory AsmDude2Category { get; } =
        new("asmDude2", "AsmDude2", null) { Description = "Assembly language support settings" };

    // ═══════════════════════════════════════════════════════════════
    // General (Order=0 → first page)
    // ═══════════════════════════════════════════════════════════════
    [VisualStudioContribution]
    internal static SettingCategory GeneralCategory { get; } =
        new("general", "General", AsmDude2Category) { Order = 0 };

    [VisualStudioContribution]
    internal static Setting.Enum AssemblerFlavor { get; } =
        new("assemblerFlavor", "Assembler syntax", GeneralCategory,
            [
                new("auto", "Auto Detect"),
                new("masm", "Intel MASM"),
                new("nasm", "Intel NASM"),
                new("att", "AT&T"),
            ],
            defaultValue: "auto")
        {
            Description = "Assembly syntax flavor for the main editor window",
        };

    [VisualStudioContribution]
    internal static Setting.Enum AssemblerFlavorDisassembly { get; } =
        new("assemblerFlavorDisassembly", "Assembler syntax (disassembly)", GeneralCategory,
            [
                new("auto", "Auto Detect"),
                new("masm", "Intel MASM"),
                new("att", "AT&T"),
            ],
            defaultValue: "auto")
        {
            Description = "Assembly syntax flavor for the disassembly window",
        };

    [VisualStudioContribution]
    internal static Setting.Integer MaxFileLines { get; } =
        new("maxFileLines", "Maximum file size (lines)", GeneralCategory, defaultValue: 10000)
        {
            Description = "Files with more lines than this will disable syntax highlighting, code folding, and label analysis",
            Minimum = 1000,
            Maximum = 1000000,
        };

    [VisualStudioContribution]
    internal static Setting.Boolean SyntaxHighlightingOn { get; } =
        new("syntaxHighlightingOn", "Syntax highlighting", GeneralCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.Boolean CodeCompletionOn { get; } =
        new("codeCompletionOn", "Code completion", GeneralCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.Boolean SignatureHelpOn { get; } =
        new("signatureHelpOn", "Signature help", GeneralCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.Boolean AsmDocOn { get; } =
        new("asmDocOn", "Documentation (Ctrl+click)", GeneralCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.String AsmDocUrl { get; } =
        new("asmDocUrl", "Documentation URL", GeneralCategory, defaultValue: "https://github.com/HJLebbink/asm-dude/wiki/")
        {
            Description = "Base URL for instruction documentation",
            EnabledWhen = SettingRule.Equal(AsmDocOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Boolean CodeFoldingOn { get; } =
        new("codeFoldingOn", "Code folding", GeneralCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.String CodeFoldingBeginTag { get; } =
        new("codeFoldingBeginTag", "Folding start tag", GeneralCategory, defaultValue: "#region")
        {
            Description = "Characters that start a folding region. Use in a comment, e.g. ;#region",
            EnabledWhen = SettingRule.Equal(CodeFoldingOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.String CodeFoldingEndTag { get; } =
        new("codeFoldingEndTag", "Folding end tag", GeneralCategory, defaultValue: "#endregion")
        {
            Description = "Characters that end a folding region. Use in a comment, e.g. ;#endregion",
            EnabledWhen = SettingRule.Equal(CodeFoldingOn, true),
        };

    // ═══════════════════════════════════════════════════════════════
    // IntelliSense (Order=1)
    // ═══════════════════════════════════════════════════════════════
    [VisualStudioContribution]
    internal static SettingCategory IntelliSenseCategory { get; } =
        new("intelliSense", "IntelliSense", AsmDude2Category) { Order = 1 };

    [VisualStudioContribution]
    internal static Setting.Boolean LabelAnalysisOn { get; } =
        new("labelAnalysisOn", "Label analysis", IntelliSenseCategory, defaultValue: true)
        {
            Description = "Enables label completions, label tooltips, and warnings for undefined or clashing labels",
        };

    [VisualStudioContribution]
    internal static Setting.Boolean ShowUndefinedLabels { get; } =
        new("showUndefinedLabels", "Undefined labels: show in error list", IntelliSenseCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean DecorateUndefinedLabels { get; } =
        new("decorateUndefinedLabels", "Undefined labels: squiggle", IntelliSenseCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean ShowClashingLabels { get; } =
        new("showClashingLabels", "Clashing labels: show in error list", IntelliSenseCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean DecorateClashingLabels { get; } =
        new("decorateClashingLabels", "Clashing labels: squiggle", IntelliSenseCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean ShowUndefinedIncludes { get; } =
        new("showUndefinedIncludes", "Undefined includes: show in error list", IntelliSenseCategory, defaultValue: false)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean DecorateUndefinedIncludes { get; } =
        new("decorateUndefinedIncludes", "Undefined includes: squiggle", IntelliSenseCategory, defaultValue: false)
        { EnabledWhen = SettingRule.Equal(LabelAnalysisOn, true) };

    // ═══════════════════════════════════════════════════════════════
    // Performance Info (Order=2)
    // ═══════════════════════════════════════════════════════════════
    [VisualStudioContribution]
    internal static SettingCategory PerformanceCategory { get; } =
        new("performance", "Performance Info", AsmDude2Category)
        {
            Description = "Show instruction latency and throughput in hover tooltips",
            Order = 2,
        };

    [VisualStudioContribution]
    internal static Setting.Boolean PerformanceInfoOn { get; } =
        new("performanceInfoOn", "Enable performance info", PerformanceCategory, defaultValue: true);

    // Performance data is from uops.info. A single microarchitecture is shown at a time; the enum
    // values match the keys mapped in SettingsSyncService. Entries are ordered chronologically.
    [VisualStudioContribution]
    internal static Setting.Enum PerfArch { get; } =
        new("perfArch", "Microarchitecture", PerformanceCategory,
            [
                new(PerfArchKeys.Conroe, "Conroe (Intel Core 2, 2006)"),
                new(PerfArchKeys.Wolfdale, "Wolfdale (Intel Core 2, 45nm, 2007)"),
                new(PerfArchKeys.Nehalem, "Nehalem (Intel 1st gen Core, 2008)"),
                new(PerfArchKeys.Westmere, "Westmere (Intel 1st gen Core, 32nm, 2010)"),
                new(PerfArchKeys.SandyBridge, "Sandy Bridge (Intel 2nd gen Core, 2011)"),
                new(PerfArchKeys.IvyBridge, "Ivy Bridge (Intel 3rd gen Core, 2012)"),
                new(PerfArchKeys.Haswell, "Haswell (Intel 4th gen Core, 2013)"),
                new(PerfArchKeys.Broadwell, "Broadwell (Intel 5th gen Core, 2014)"),
                new(PerfArchKeys.Skylake, "Skylake (Intel 6th gen Core, 2015)"),
                new(PerfArchKeys.SkylakeX, "Skylake-X / Skylake server (2017)"),
                new(PerfArchKeys.Kabylake, "Kaby Lake (Intel 7th gen Core, 2016)"),
                new(PerfArchKeys.CoffeeLake, "Coffee Lake (Intel 8th/9th gen Core, 2017)"),
                new(PerfArchKeys.Cannonlake, "Cannon Lake (2018)"),
                new(PerfArchKeys.CascadeLake, "Cascade Lake (Xeon, 2019)"),
                new(PerfArchKeys.Icelake, "Ice Lake (Intel 10th gen Core, 2019)"),
                new(PerfArchKeys.Tigerlake, "Tiger Lake (Intel 11th gen Core, 2020)"),
                new(PerfArchKeys.RocketLake, "Rocket Lake (Intel 11th gen Core desktop, 2021)"),
                new(PerfArchKeys.EmeraldRapids, "Emerald Rapids (Intel 5th gen Xeon, 2023)"),
                new(PerfArchKeys.Bonnell, "Bonnell (Intel Atom, 2008)"),
                new(PerfArchKeys.Airmont, "Airmont (Intel Atom, 2015)"),
                new(PerfArchKeys.Goldmont, "Goldmont (Intel Atom, 2016)"),
                new(PerfArchKeys.GoldmontPlus, "Goldmont Plus (Intel Atom, 2017)"),
                new(PerfArchKeys.Tremont, "Tremont (Intel Atom, 2020)"),
                new(PerfArchKeys.Zen2, "AMD Zen 2 (2019)"),
                new(PerfArchKeys.Zen3, "AMD Zen 3 (2020)"),
                new(PerfArchKeys.Zen4, "AMD Zen 4 (2022)"),
                new(PerfArchKeys.Zen5, "AMD Zen 5 (2024)"),
            ],
            defaultValue: PerfArchKeys.Skylake)
        {
            Description = "Which CPU microarchitecture's latency/throughput to show in hover tooltips and inlay hints",
            EnabledWhen = SettingRule.Equal(PerformanceInfoOn, true),
        };

    // ═══════════════════════════════════════════════════════════════
    // Assembly Simulator (Order=3)
    // ═══════════════════════════════════════════════════════════════
    [VisualStudioContribution]
    internal static SettingCategory AsmSimCategory { get; } =
        new("asmSim", "Assembly Simulator", AsmDude2Category)
        {
            Description = "Z3-based assembly simulator for register state tracking and error detection. "
                + "Note: the simulator does not understand MASM or NASM directives (no 'MOV VAR, 10'). "
                + "Jumping based on register content is also not supported (no 'JMP RAX').",
            Order = 3,
        };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimOn { get; } =
        new("asmSimOn", "Enable assembly simulator", AsmSimCategory, defaultValue: true);

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimIncremental { get; } =
        new("asmSimIncremental", "Incremental simulation", AsmSimCategory, defaultValue: true)
        {
            Description = "On an edit, reuse the previous result for unaffected lines and re-solve only the dataflow cone, instead of re-simulating the whole document. Falls back to a full simulation when it can't safely reuse. Applies at runtime — no restart.",
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Integer AsmSimZ3Timeout { get; } =
        new("asmSimZ3Timeout", "Z3 timeout (ms)", AsmSimCategory, defaultValue: 5000)
        {
            Description = "Milliseconds Z3 may spend per bit. 200 = worst case 12.8s for a 64-bit register. 0 = no timeout (not recommended)",
            Minimum = 0,
            Maximum = 60000,
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Integer AsmSimThreads { get; } =
        new("asmSimThreads", "Worker threads", AsmSimCategory, defaultValue: 4)
        {
            Description = "Number of threads for the assembly simulator",
            Minimum = 1,
            Maximum = 32,
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSim64Bits { get; } =
        new("asmSim64Bits", "64-bit mode (32-bit otherwise)", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowRegisterInCodeCompletion { get; } =
        new("asmSimShowRegisterInCodeCompletion", "Register values in code completion", AsmSimCategory, defaultValue: false)
        {
            Description = "Show known register contents in code completion items",
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowRegisterInRegisterTooltip { get; } =
        new("asmSimShowRegisterInRegisterTooltip", "Register values in register tooltips", AsmSimCategory, defaultValue: true)
        {
            Description = "Show known register contents when hovering over a register",
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowRegisterInInstructionTooltip { get; } =
        new("asmSimShowRegisterInInstructionTooltip", "Register values in instruction tooltips", AsmSimCategory, defaultValue: true)
        {
            Description = "Show known register contents when hovering over an instruction",
            EnabledWhen = SettingRule.Equal(AsmSimOn, true),
        };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowSyntaxErrors { get; } =
        new("asmSimShowSyntaxErrors", "Syntax errors: show in error list", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateSyntaxErrors { get; } =
        new("asmSimDecorateSyntaxErrors", "Syntax errors: squiggle", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowUsageOfUndefined { get; } =
        new("asmSimShowUsageOfUndefined", "Usage of undefined: show in error list", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateUsageOfUndefined { get; } =
        new("asmSimDecorateUsageOfUndefined", "Usage of undefined: squiggle", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowRedundantInstructions { get; } =
        new("asmSimShowRedundantInstructions", "Redundant instructions: show in error list", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateRedundantInstructions { get; } =
        new("asmSimDecorateRedundantInstructions", "Redundant instructions: squiggle", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimShowUnreachableInstructions { get; } =
        new("asmSimShowUnreachableInstructions", "Unreachable instructions: show in error list", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateUnreachableInstructions { get; } =
        new("asmSimDecorateUnreachableInstructions", "Unreachable instructions: squiggle", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateRegisters { get; } =
        new("asmSimDecorateRegisters", "Known register values: squiggle", AsmSimCategory, defaultValue: true)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };

    [VisualStudioContribution]
    internal static Setting.Boolean AsmSimDecorateUnimplemented { get; } =
        new("asmSimDecorateUnimplemented", "Unimplemented instructions: squiggle", AsmSimCategory, defaultValue: false)
        { EnabledWhen = SettingRule.Equal(AsmSimOn, true) };
}
