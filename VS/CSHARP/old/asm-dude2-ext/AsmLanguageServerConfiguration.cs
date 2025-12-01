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

namespace AsmDude2;

/// <summary>
/// Configuration options for the AsmDude2 language server
/// </summary>
internal static class AsmLanguageServerConfiguration
{
    /// <summary>
    /// Gets the initialization options to send to the language server
    /// </summary>
    public static AsmLanguageServerOptions GetInitializationOptions()
    {
        // For now, return default options
        // TODO: Integrate with VS settings/options when VisualStudio.Extensibility supports it
        return new AsmLanguageServerOptions
        {
            SyntaxHighlighting_On = true,
            SyntaxHighlighting_Opcode = true,
            SyntaxHighlighting_Register = true,
            SyntaxHighlighting_Remark = true,
            SyntaxHighlighting_Directive = true,
            SyntaxHighlighting_Jump = true,
            SyntaxHighlighting_Label = true,
            SyntaxHighlighting_Constant = true,
            SyntaxHighlighting_Misc = true,

            CodeFolding_On = true,
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",

            CodeCompletion_On = true,
            SignatureHelp_On = true,

            AsmDoc_On = true,
            AsmDoc_Url = "https://www.felixcloutier.com/x86/",

            useAssemblerMasm = true,
            useAssemblerNasm = true,
            useAssemblerNasm_Att = false,
            useAssemblerAutoDetect = true,

            IntelliSense_Show_Undefined_Labels = true,
            IntelliSense_Show_Clashing_Labels = true,
            IntelliSense_Decorate_Undefined_Labels = true,
            IntelliSense_Decorate_Clashing_Labels = true,
            IntelliSense_Show_Undefined_Includes = true,
            IntelliSense_Decorate_Undefined_Includes = true,
            IntelliSense_Label_Analysis_On = true,

            // Enable common architectures by default
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = true,
            ARCH_SSE2 = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_F = true,

            PerformanceInfo_On = true,
            PerformanceInfo_Haswell_On = true,
            PerformanceInfo_Skylake_On = true,

            Global_MaxFileLines = 10000,
        };
    }
}
