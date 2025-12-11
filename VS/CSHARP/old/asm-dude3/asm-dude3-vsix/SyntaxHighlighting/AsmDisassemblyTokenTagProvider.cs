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

namespace AsmDude3.SyntaxHighlighting
{
    using System;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using AsmTools;
    using AsmDude3.Tools;

    /// <summary>
    /// Provides token taggers for disassembly output in the debugger window.
    /// Creates the appropriate tokenizer (MASM or NASM AT&T) based on:
    /// 1. User settings (Used_Assembler_Disassembly_Window)
    /// 2. Auto-detection if user has selected AUTO_DETECT mode
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDude3Package.DisassemblyContentType)]
    [TagType(typeof(AsmTokenTag))]
    [Name("AsmDude3 Disassembly Token Tag Provider")]
    internal sealed class AsmDisassemblyTokenTagProvider : ITaggerProvider
    {
        /// <summary>
        /// Creates a token tagger for disassembly output in the given buffer.
        /// Automatically selects the appropriate tokenizer based on user settings
        /// or auto-detects the assembler flavor from disassembly content.
        /// </summary>
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() =>
            {
                ITagger<AsmTokenTag> tokenTagger = this.CreateTokenTagger(buffer);
                return tokenTagger;
            }) as ITagger<T>;
        }

        /// <summary>
        /// Creates the appropriate disassembly tokenizer based on assembler selection
        /// </summary>
        private ITagger<AsmTokenTag> CreateTokenTagger(ITextBuffer buffer)
        {
            AssemblerEnum assembler = AsmDudeToolsStatic.Used_Assembler_Disassembly_Window;

            // If auto-detect is enabled, guess the assembler from disassembly content
            if (assembler == AssemblerEnum.AUTO_DETECT)
            {
                assembler = this.GuessAssemblerFromDisassembly(buffer);
            }

            // Return appropriate tokenizer based on selected assembler
            switch (assembler)
            {
                case AssemblerEnum.MASM:
                    return new MasmDisassemblyTokenTagger(buffer);

                case AssemblerEnum.NASM_ATT:
                    return new NasmAttDisassemblyTokenTagger(buffer);

                default:
                    // Default to MASM if unknown
                    return new MasmDisassemblyTokenTagger(buffer);
            }
        }

        /// <summary>
        /// Guesses the assembler flavor from the first 40 lines of disassembly output.
        /// Heuristics:
        /// - MASM: uses $ for immediates in MASM style
        /// - NASM AT&T: uses % prefix for registers, $ for immediates with AT&T syntax
        /// </summary>
        private AssemblerEnum GuessAssemblerFromDisassembly(ITextBuffer buffer)
        {
            int lineCount = buffer.CurrentSnapshot.LineCount;
            int linesToCheck = System.Math.Min(40, lineCount);

            for (int i = 0; i < linesToCheck; i++)
            {
                var line = buffer.CurrentSnapshot.GetLineFromLineNumber(i);
                string text = line.GetText().ToLowerInvariant();

                // NASM AT&T indicators (% prefix for registers, suffixes like 'q', 'l')
                if (text.Contains("%") || text.Contains("movl") || text.Contains("movq") ||
                    text.Contains("pushq") || text.Contains("popq"))
                {
                    return AssemblerEnum.NASM_ATT;
                }
            }

            // Default to MASM for disassembly
            return AssemblerEnum.MASM;
        }
    }
}
