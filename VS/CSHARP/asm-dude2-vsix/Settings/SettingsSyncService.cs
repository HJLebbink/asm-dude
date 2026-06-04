// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

#pragma warning disable VSEXTPREVIEW_SETTINGS // Settings API is in preview

namespace AsmDude2.Settings;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

/// <summary>
/// Subscribes to VS.Extensibility settings changes and writes them to
/// %APPDATA%\AsmDude2\settings.json for the LSP server to pick up via FileSystemWatcher.
/// </summary>
[VisualStudioContribution]
internal class SettingsSyncService : ExtensionPart
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsmDude2");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
    private static readonly string DiagLogFile = Path.Combine(Path.GetTempPath(), "AsmDude2-extension-diag.log");

    private IDisposable? subscription;

    public SettingsSyncService(ExtensionCore container, VisualStudioExtensibility extensibility)
        : base(container, extensibility)
    {
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await base.InitializeAsync(cancellationToken);

        try
        {
            SettingsExtensibility settings = this.Extensibility.Settings();

            // SubscribeAsync calls the handler immediately with current values, then on every change
            this.subscription = await settings.SubscribeAsync(
                [AsmDudeSettings.AsmDude2Category, ArchitectureSettings.ArchCategory],
                cancellationToken,
                this.OnSettingsChanged);

            Log("SettingsSyncService: initialized and subscribed to settings changes");
        }
        catch (Exception ex)
        {
            Log($"SettingsSyncService: init failed: {ex.Message}");
        }
    }

    private void OnSettingsChanged(SettingValues values)
    {
        try
        {
            WriteSettingsFromValues(values);
            Log("SettingsSyncService: settings changed, wrote to JSON");
        }
        catch (Exception ex)
        {
            Log($"SettingsSyncService: failed to sync: {ex.Message}");
        }
    }

    private static void WriteSettingsFromValues(SettingValues v)
    {
        string assembler = v.ValueOrDefault(AsmDudeSettings.AssemblerFlavor, "auto");
        string assemblerDisasm = v.ValueOrDefault(AsmDudeSettings.AssemblerFlavorDisassembly, "auto");

        // Build the SHARED, strongly-typed contract (not an anonymous object) so a renamed/removed
        // field is a compile error here instead of a silently-defaulted value on the server.
        var options = new AsmTools.AsmSettingsData
        {
            // General
            Global_MaxFileLines = v.ValueOrDefault(AsmDudeSettings.MaxFileLines, 10000),
            useAssemblerAutoDetect = assembler == "auto",
            useAssemblerMasm = assembler == "masm",
            useAssemblerNasm = assembler == "nasm",
            useAssemblerNasm_Att = assembler == "att",
            useAssemblerDisassemblyAutoDetect = assemblerDisasm == "auto",
            useAssemblerDisassemblyMasm = assemblerDisasm == "masm",
            useAssemblerDisassemblyNasm_Att = assemblerDisasm == "att",

            // Features
            SyntaxHighlighting_On = v.ValueOrDefault(AsmDudeSettings.SyntaxHighlightingOn, true),
            CodeCompletion_On = v.ValueOrDefault(AsmDudeSettings.CodeCompletionOn, true),
            SignatureHelp_On = v.ValueOrDefault(AsmDudeSettings.SignatureHelpOn, true),
            AsmDoc_On = v.ValueOrDefault(AsmDudeSettings.AsmDocOn, true),
            AsmDoc_Url = v.ValueOrDefault(AsmDudeSettings.AsmDocUrl, "https://github.com/HJLebbink/asm-dude/wiki/"),
            CodeFolding_On = v.ValueOrDefault(AsmDudeSettings.CodeFoldingOn, true),
            CodeFolding_BeginTag = v.ValueOrDefault(AsmDudeSettings.CodeFoldingBeginTag, "#region"),
            CodeFolding_EndTag = v.ValueOrDefault(AsmDudeSettings.CodeFoldingEndTag, "#endregion"),

            // IntelliSense
            IntelliSense_Label_Analysis_On = v.ValueOrDefault(AsmDudeSettings.LabelAnalysisOn, true),
            IntelliSense_Show_Undefined_Labels = v.ValueOrDefault(AsmDudeSettings.ShowUndefinedLabels, true),
            IntelliSense_Decorate_Undefined_Labels = v.ValueOrDefault(AsmDudeSettings.DecorateUndefinedLabels, true),
            IntelliSense_Show_Clashing_Labels = v.ValueOrDefault(AsmDudeSettings.ShowClashingLabels, true),
            IntelliSense_Decorate_Clashing_Labels = v.ValueOrDefault(AsmDudeSettings.DecorateClashingLabels, true),
            IntelliSense_Show_Undefined_Includes = v.ValueOrDefault(AsmDudeSettings.ShowUndefinedIncludes, false),
            IntelliSense_Decorate_Undefined_Includes = v.ValueOrDefault(AsmDudeSettings.DecorateUndefinedIncludes, false),

            // Performance Info
            PerformanceInfo_On = v.ValueOrDefault(AsmDudeSettings.PerformanceInfoOn, true),
            PerformanceInfo_IvyBridge_On = v.ValueOrDefault(AsmDudeSettings.PerfIvyBridge, false),
            PerformanceInfo_Haswell_On = v.ValueOrDefault(AsmDudeSettings.PerfHaswell, true),
            PerformanceInfo_Broadwell_On = v.ValueOrDefault(AsmDudeSettings.PerfBroadwell, false),
            PerformanceInfo_Skylake_On = v.ValueOrDefault(AsmDudeSettings.PerfSkylake, true),
            PerformanceInfo_SkylakeX_On = v.ValueOrDefault(AsmDudeSettings.PerfSkylakeX, false),
            PerformanceInfo_KnightsLanding_On = v.ValueOrDefault(AsmDudeSettings.PerfKnightsLanding, false),

            // AsmSim
            AsmSim_On = v.ValueOrDefault(AsmDudeSettings.AsmSimOn, true),
            AsmSim_Z3_Timeout_MS = v.ValueOrDefault(AsmDudeSettings.AsmSimZ3Timeout, 5000),
            AsmSim_Number_Of_Threads = v.ValueOrDefault(AsmDudeSettings.AsmSimThreads, 4),
            AsmSim_64_Bits = v.ValueOrDefault(AsmDudeSettings.AsmSim64Bits, true),
            AsmSim_Show_Register_In_Code_Completion = v.ValueOrDefault(AsmDudeSettings.AsmSimShowRegisterInCodeCompletion, false),
            AsmSim_Show_Register_In_Register_Tooltip = v.ValueOrDefault(AsmDudeSettings.AsmSimShowRegisterInRegisterTooltip, true),
            AsmSim_Show_Register_In_Instruction_Tooltip = v.ValueOrDefault(AsmDudeSettings.AsmSimShowRegisterInInstructionTooltip, true),
            AsmSim_Show_Syntax_Errors = v.ValueOrDefault(AsmDudeSettings.AsmSimShowSyntaxErrors, true),
            AsmSim_Decorate_Syntax_Errors = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateSyntaxErrors, true),
            AsmSim_Show_Usage_Of_Undefined = v.ValueOrDefault(AsmDudeSettings.AsmSimShowUsageOfUndefined, true),
            AsmSim_Decorate_Usage_Of_Undefined = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateUsageOfUndefined, true),
            AsmSim_Show_Redundant_Instructions = v.ValueOrDefault(AsmDudeSettings.AsmSimShowRedundantInstructions, false),
            AsmSim_Decorate_Redundant_Instructions = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateRedundantInstructions, false),
            AsmSim_Show_Unreachable_Instructions = v.ValueOrDefault(AsmDudeSettings.AsmSimShowUnreachableInstructions, true),
            AsmSim_Decorate_Unreachable_Instructions = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateUnreachableInstructions, true),
            AsmSim_Decorate_Registers = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateRegisters, true),
            AsmSim_Decorate_Unimplemented = v.ValueOrDefault(AsmDudeSettings.AsmSimDecorateUnimplemented, false),

            // Architectures: Processors
            ARCH_8086 = v.ValueOrDefault(ArchitectureSettings.Arch8086, true),
            ARCH_186 = v.ValueOrDefault(ArchitectureSettings.Arch186, true),
            ARCH_286 = v.ValueOrDefault(ArchitectureSettings.Arch286, true),
            ARCH_386 = v.ValueOrDefault(ArchitectureSettings.Arch386, true),
            ARCH_486 = v.ValueOrDefault(ArchitectureSettings.Arch486, true),
            ARCH_PENT = v.ValueOrDefault(ArchitectureSettings.ArchPent, true),
            ARCH_P6 = v.ValueOrDefault(ArchitectureSettings.ArchP6, true),

            // Architectures: SSE
            ARCH_MMX = v.ValueOrDefault(ArchitectureSettings.ArchMMX, true),
            ARCH_SSE = v.ValueOrDefault(ArchitectureSettings.ArchSSE, true),
            ARCH_SSE2 = v.ValueOrDefault(ArchitectureSettings.ArchSSE2, true),
            ARCH_SSE3 = v.ValueOrDefault(ArchitectureSettings.ArchSSE3, true),
            ARCH_SSSE3 = v.ValueOrDefault(ArchitectureSettings.ArchSSSE3, true),
            ARCH_SSE4_1 = v.ValueOrDefault(ArchitectureSettings.ArchSSE41, true),
            ARCH_SSE4_2 = v.ValueOrDefault(ArchitectureSettings.ArchSSE42, true),
            ARCH_SSE4A = v.ValueOrDefault(ArchitectureSettings.ArchSSE4A, false),
            ARCH_SSE5 = v.ValueOrDefault(ArchitectureSettings.ArchSSE5, false),

            // Architectures: AVX
            ARCH_AVX = v.ValueOrDefault(ArchitectureSettings.ArchAVX, true),
            ARCH_AVX2 = v.ValueOrDefault(ArchitectureSettings.ArchAVX2, true),

            // Architectures: AVX-512
            ARCH_AVX512_F = v.ValueOrDefault(ArchitectureSettings.ArchAVX512F, false),
            ARCH_AVX512_CD = v.ValueOrDefault(ArchitectureSettings.ArchAVX512CD, false),
            ARCH_AVX512_ER = v.ValueOrDefault(ArchitectureSettings.ArchAVX512ER, false),
            ARCH_AVX512_PF = v.ValueOrDefault(ArchitectureSettings.ArchAVX512PF, false),
            ARCH_AVX512_BW = v.ValueOrDefault(ArchitectureSettings.ArchAVX512BW, false),
            ARCH_AVX512_DQ = v.ValueOrDefault(ArchitectureSettings.ArchAVX512DQ, false),
            ARCH_AVX512_VL = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VL, false),
            ARCH_AVX512_IFMA = v.ValueOrDefault(ArchitectureSettings.ArchAVX512IFMA, false),
            ARCH_AVX512_VBMI = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VBMI, false),
            ARCH_AVX512_VPOPCNTDQ = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VPOPCNTDQ, false),
            ARCH_AVX512_4VNNIW = v.ValueOrDefault(ArchitectureSettings.ArchAVX5124VNNIW, false),
            ARCH_AVX512_4FMAPS = v.ValueOrDefault(ArchitectureSettings.ArchAVX5124FMAPS, false),
            ARCH_AVX512_VBMI2 = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VBMI2, false),
            ARCH_AVX512_VNNI = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VNNI, false),
            ARCH_AVX512_BITALG = v.ValueOrDefault(ArchitectureSettings.ArchAVX512BITALG, false),
            ARCH_AVX512_GFNI = v.ValueOrDefault(ArchitectureSettings.ArchAVX512GFNI, false),
            ARCH_AVX512_VAES = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VAES, false),
            ARCH_AVX512_VPCLMULQDQ = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VPCLMULQDQ, false),
            ARCH_AVX512_BF16 = v.ValueOrDefault(ArchitectureSettings.ArchAVX512BF16, false),
            ARCH_AVX512_VP2INTERSECT = v.ValueOrDefault(ArchitectureSettings.ArchAVX512VP2INTERSECT, false),
            ARCH_ENQCMD = v.ValueOrDefault(ArchitectureSettings.ArchENQCMD, false),

            // Architectures: rev-091 / 2026 SDM additions
            ARCH_AVX10 = v.ValueOrDefault(ArchitectureSettings.ArchAVX10, false),
            ARCH_AVX512_FP16 = v.ValueOrDefault(ArchitectureSettings.ArchAVX512FP16, false),
            ARCH_AVX_VNNI = v.ValueOrDefault(ArchitectureSettings.ArchAVXVNNI, false),
            ARCH_AVX_VNNI_INT = v.ValueOrDefault(ArchitectureSettings.ArchAVXVNNIINT, false),
            ARCH_AVX_NE_CONVERT = v.ValueOrDefault(ArchitectureSettings.ArchAVXNECONVERT, false),
            ARCH_AVX_IFMA = v.ValueOrDefault(ArchitectureSettings.ArchAVXIFMA, false),
            ARCH_AMX = v.ValueOrDefault(ArchitectureSettings.ArchAMX, false),
            ARCH_CMPCCXADD = v.ValueOrDefault(ArchitectureSettings.ArchCMPCCXADD, false),
            ARCH_CET_SS = v.ValueOrDefault(ArchitectureSettings.ArchCETSS, false),
            ARCH_CET_IBT = v.ValueOrDefault(ArchitectureSettings.ArchCETIBT, false),
            ARCH_KEYLOCKER = v.ValueOrDefault(ArchitectureSettings.ArchKEYLOCKER, false),
            ARCH_UINTR = v.ValueOrDefault(ArchitectureSettings.ArchUINTR, false),
            ARCH_PBNDKB = v.ValueOrDefault(ArchitectureSettings.ArchPBNDKB, false),
            ARCH_SMAP = v.ValueOrDefault(ArchitectureSettings.ArchSMAP, false),
            ARCH_SERIALIZE = v.ValueOrDefault(ArchitectureSettings.ArchSERIALIZE, false),
            ARCH_WBNOINVD = v.ValueOrDefault(ArchitectureSettings.ArchWBNOINVD, false),
            ARCH_HRESET = v.ValueOrDefault(ArchitectureSettings.ArchHRESET, false),
            ARCH_MSRLIST = v.ValueOrDefault(ArchitectureSettings.ArchMSRLIST, false),
            ARCH_WRMSRNS = v.ValueOrDefault(ArchitectureSettings.ArchWRMSRNS, false),
            ARCH_PTWRITE = v.ValueOrDefault(ArchitectureSettings.ArchPTWRITE, false),
            ARCH_TSXLDTRK = v.ValueOrDefault(ArchitectureSettings.ArchTSXLDTRK, false),
            ARCH_PREFETCHI = v.ValueOrDefault(ArchitectureSettings.ArchPREFETCHI, false),
            ARCH_SHA512 = v.ValueOrDefault(ArchitectureSettings.ArchSHA512, false),
            ARCH_SM3 = v.ValueOrDefault(ArchitectureSettings.ArchSM3, false),
            ARCH_SM4 = v.ValueOrDefault(ArchitectureSettings.ArchSM4, false),
            ARCH_MOVBE = v.ValueOrDefault(ArchitectureSettings.ArchMOVBE, false),
            ARCH_PKU = v.ValueOrDefault(ArchitectureSettings.ArchPKU, false),

            // Architectures: Intel Extensions
            ARCH_ADX = v.ValueOrDefault(ArchitectureSettings.ArchADX, false),
            ARCH_AES = v.ValueOrDefault(ArchitectureSettings.ArchAES, false),
            ARCH_BMI1 = v.ValueOrDefault(ArchitectureSettings.ArchBMI1, false),
            ARCH_BMI2 = v.ValueOrDefault(ArchitectureSettings.ArchBMI2, false),
            ARCH_HLE = v.ValueOrDefault(ArchitectureSettings.ArchHLE, false),
            ARCH_MPX = v.ValueOrDefault(ArchitectureSettings.ArchMPX, false),
            ARCH_RTM = v.ValueOrDefault(ArchitectureSettings.ArchRTM, false),
            ARCH_SHA = v.ValueOrDefault(ArchitectureSettings.ArchSHA, false),
            ARCH_VMX = v.ValueOrDefault(ArchitectureSettings.ArchVMX, false),
            ARCH_SMX = v.ValueOrDefault(ArchitectureSettings.ArchSMX, false),
            ARCH_SGX1 = v.ValueOrDefault(ArchitectureSettings.ArchSGX1, false),
            ARCH_SGX2 = v.ValueOrDefault(ArchitectureSettings.ArchSGX2, false),
            ARCH_CLDEMOTE = v.ValueOrDefault(ArchitectureSettings.ArchCLDEMOTE, false),
            ARCH_MOVDIR64B = v.ValueOrDefault(ArchitectureSettings.ArchMOVDIR64B, false),
            ARCH_MOVDIRI = v.ValueOrDefault(ArchitectureSettings.ArchMOVDIRI, false),
            ARCH_PCONFIG = v.ValueOrDefault(ArchitectureSettings.ArchPCONFIG, false),
            ARCH_WAITPKG = v.ValueOrDefault(ArchitectureSettings.ArchWAITPKG, false),

            // Architectures: Misc
            ARCH_X64 = v.ValueOrDefault(ArchitectureSettings.ArchX64, true),
            ARCH_FMA = v.ValueOrDefault(ArchitectureSettings.ArchFMA, false),
            ARCH_F16C = v.ValueOrDefault(ArchitectureSettings.ArchF16C, false),
            ARCH_FSGSBASE = v.ValueOrDefault(ArchitectureSettings.ArchFSGSBASE, false),
            ARCH_INVPCID = v.ValueOrDefault(ArchitectureSettings.ArchINVPCID, false),
            ARCH_PCLMULQDQ = v.ValueOrDefault(ArchitectureSettings.ArchPCLMULQDQ, false),
            ARCH_LZCNT = v.ValueOrDefault(ArchitectureSettings.ArchLZCNT, false),
            ARCH_PREFETCHWT1 = v.ValueOrDefault(ArchitectureSettings.ArchPREFETCHWT1, false),
            ARCH_PRFCHW = v.ValueOrDefault(ArchitectureSettings.ArchPRFCHW, false),
            ARCH_RDPID = v.ValueOrDefault(ArchitectureSettings.ArchRDPID, false),
            ARCH_RDRAND = v.ValueOrDefault(ArchitectureSettings.ArchRDRAND, false),
            ARCH_RDSEED = v.ValueOrDefault(ArchitectureSettings.ArchRDSEED, false),
            ARCH_XSAVEOPT = v.ValueOrDefault(ArchitectureSettings.ArchXSAVEOPT, false),
            ARCH_UNDOC = v.ValueOrDefault(ArchitectureSettings.ArchUNDOC, false),
            ARCH_AMD = v.ValueOrDefault(ArchitectureSettings.ArchAMD, false),
            ARCH_TBM = v.ValueOrDefault(ArchitectureSettings.ArchTBM, false),
            ARCH_3DNOW = v.ValueOrDefault(ArchitectureSettings.Arch3DNOW, false),
            ARCH_IA64 = v.ValueOrDefault(ArchitectureSettings.ArchIA64, false),
            ARCH_CYRIX = v.ValueOrDefault(ArchitectureSettings.ArchCYRIX, false),
            ARCH_CYRIXM = v.ValueOrDefault(ArchitectureSettings.ArchCYRIXM, false),
        };

        Directory.CreateDirectory(SettingsDir);
        string json = JsonSerializer.Serialize(options, new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            // Same converter the server uses (from the shared contract lib) so Color fields match.
            Converters = { new AsmTools.ColorJsonConverter() },
        });
        File.WriteAllText(SettingsFile, json);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            this.subscription?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private static void Log(string message)
    {
        try { File.AppendAllText(DiagLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } catch { }
    }
}
