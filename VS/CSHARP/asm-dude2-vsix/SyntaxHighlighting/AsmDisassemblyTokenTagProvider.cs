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
    using System.ComponentModel.Composition;
    using AsmDude2.SyntaxHighlighting;
    using AsmDude2.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDude2Package.DisassemblyContentType)]
    [TagType(typeof(AsmTokenTag))]
    [Name("AsmDude Disassembly Token Tag Provider")]
    internal sealed class AsmDisassemblyTokenTagProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:CreateTagger", this.ToString()));
            ITagger<T> sc()
            {
                if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    int nLines = Math.Min(40, buffer.CurrentSnapshot.LineCount);
                    string[] selection = new string[nLines];
                    for (int i = 0; i < nLines; ++i)
                    {
                        selection[i] = buffer.CurrentSnapshot.GetLineFromLineNumber(i).GetText();
                    }
                    bool has_intel_syntax = AsmSourceTools.Guess_Intel_Syntax(selection);

                    return (has_intel_syntax)
                        ? new MasmDisassemblyTokenTagger(buffer) as ITagger<T>
                        : new NasmAttDisassemblyTokenTagger(buffer) as ITagger<T>;
                }
                if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    return new NasmAttDisassemblyTokenTagger(buffer) as ITagger<T>;
                }
                if (AsmDudeToolsStatic.Used_Assembler_Disassembly_Window.HasFlag(AssemblerEnum.MASM))
                {
                    return new MasmDisassemblyTokenTagger(buffer) as ITagger<T>;
                }
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:CreateTagger: could not determine the used assembler", this.ToString()));
                return new MasmDisassemblyTokenTagger(buffer) as ITagger<T>;
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
