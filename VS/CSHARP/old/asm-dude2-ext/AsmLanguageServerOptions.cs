// Temporary copy of AsmLanguageServerOptions for .NET 8.0 compatibility
// TODO: Remove this once asm-tools-lib supports multi-targeting

namespace AsmDude2;

/// <summary>
/// Options sent to the language server during initialization
/// </summary>
public class AsmLanguageServerOptions
{
    public bool SyntaxHighlighting_On { get; set; }
    public bool SyntaxHighlighting_Opcode { get; set; }
    public bool SyntaxHighlighting_Register { get; set; }
    public bool SyntaxHighlighting_Remark { get; set; }
    public bool SyntaxHighlighting_Directive { get; set; }
    public bool SyntaxHighlighting_Jump { get; set; }
    public bool SyntaxHighlighting_Label { get; set; }
    public bool SyntaxHighlighting_Constant { get; set; }
    public bool SyntaxHighlighting_Misc { get; set; }

    public bool CodeFolding_On { get; set; }
    public string CodeFolding_BeginTag { get; set; } = string.Empty;
    public string CodeFolding_EndTag { get; set; } = string.Empty;

    public bool CodeCompletion_On { get; set; }
    public bool SignatureHelp_On { get; set; }

    public bool AsmDoc_On { get; set; }
    public string AsmDoc_Url { get; set; } = string.Empty;

    public bool useAssemblerMasm { get; set; }
    public bool useAssemblerNasm { get; set; }
    public bool useAssemblerNasm_Att { get; set; }
    public bool useAssemblerAutoDetect { get; set; }

    public bool IntelliSense_Show_Undefined_Labels { get; set; }
    public bool IntelliSense_Show_Clashing_Labels { get; set; }
    public bool IntelliSense_Decorate_Undefined_Labels { get; set; }
    public bool IntelliSense_Decorate_Clashing_Labels { get; set; }
    public bool IntelliSense_Show_Undefined_Includes { get; set; }
    public bool IntelliSense_Decorate_Undefined_Includes { get; set; }
    public bool IntelliSense_Label_Analysis_On { get; set; }

    public bool ARCH_8086 { get; set; }
    public bool ARCH_X64 { get; set; }
    public bool ARCH_SSE { get; set; }
    public bool ARCH_SSE2 { get; set; }
    public bool ARCH_AVX { get; set; }
    public bool ARCH_AVX2 { get; set; }
    public bool ARCH_AVX512_F { get; set; }

    public bool PerformanceInfo_On { get; set; }
    public bool PerformanceInfo_Haswell_On { get; set; }
    public bool PerformanceInfo_Skylake_On { get; set; }

    public int Global_MaxFileLines { get; set; }
}
