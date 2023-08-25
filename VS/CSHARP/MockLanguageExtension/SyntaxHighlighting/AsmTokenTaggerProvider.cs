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
    using System.ComponentModel.Composition;
    using AsmDude2.SyntaxHighlighting;
    using AsmDude2.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [TagType(typeof(AsmTokenTag))]
    [Name("AsmDude Assembly Token Tag Provider")]
    [Order(Before = "default")]
    internal sealed class AsmTokenTagProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:CreateTagger", this.ToString()));
            ITagger<T> sc()
            {
                if (AsmDudeToolsStatic.Used_Assembler.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    int nLinesMax = 40;
                    bool has_intel_syntax = AsmDudeToolsStatic.Guess_Intel_Syntax(buffer, nLinesMax);
                    bool has_masm_syntax = AsmDudeToolsStatic.Guess_Masm_Syntax(buffer, nLinesMax);

                    return (has_masm_syntax)
                        ? (has_intel_syntax)
                            ? new MasmTokenTagger(buffer) as ITagger<T>
                            : new MasmTokenTagger(buffer) as ITagger<T>
                        : (has_intel_syntax)
                            ? new NasmIntelTokenTagger(buffer) as ITagger<T>
                            : new NasmAttTokenTagger(buffer) as ITagger<T>;
                }
                if (AsmDudeToolsStatic.Used_Assembler.HasFlag(AssemblerEnum.MASM))
                {
                    return new MasmTokenTagger(buffer) as ITagger<T>;
                }
                if (AsmDudeToolsStatic.Used_Assembler.HasFlag(AssemblerEnum.NASM_INTEL))
                {
                    return new NasmIntelTokenTagger(buffer) as ITagger<T>;
                }
                if (AsmDudeToolsStatic.Used_Assembler.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    return new NasmAttTokenTagger(buffer) as ITagger<T>;
                }
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:CreateTagger: could not determine the used assembler", this.ToString()));
                return new MasmTokenTagger(buffer) as ITagger<T>;
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
