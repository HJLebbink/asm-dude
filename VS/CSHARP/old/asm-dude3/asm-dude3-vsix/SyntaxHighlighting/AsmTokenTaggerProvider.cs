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
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using AsmTools;
    using AsmDude3.Tools;

    /// <summary>
    /// Provides token taggers for assembly code.
    /// Creates the appropriate tokenizer (MASM, NASM Intel, or NASM AT&T) based on:
    /// 1. User settings (useAssemblerMasm, useAssemblerNasm, etc.)
    /// 2. Auto-detection if user has selected AUTO_DETECT mode
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDude3Package.AsmDudeContentType)]
    [TagType(typeof(AsmTokenTag))]
    [Name("AsmDude3 Assembly Token Tag Provider")]
    public class AsmTokenTaggerProvider : ITaggerProvider
    {
        private static AsmDude2Tools _asmDudeTools;

        public AsmTokenTaggerProvider()
        {
            // Create AsmDude2Tools singleton on first initialization
            if (_asmDudeTools == null)
            {
                TraceSource traceSource = new TraceSource("AsmDude3");
                string assemblyPath = AsmDudeToolsStatic.Get_Install_Path();
                _asmDudeTools = AsmDude2Tools.Create(assemblyPath, traceSource);
            }
        }
        /// <summary>
        /// Creates a token tagger for the given buffer.
        /// Automatically selects the appropriate tokenizer based on user settings
        /// or auto-detects the assembler flavor from file content.
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
        /// Creates the appropriate tokenizer based on assembler selection and auto-detection
        /// </summary>
        private ITagger<AsmTokenTag> CreateTokenTagger(ITextBuffer buffer)
        {
            AssemblerEnum assembler = AsmDudeToolsStatic.Used_Assembler;

            // If auto-detect is enabled, guess the assembler from file content
            if (assembler == AssemblerEnum.AUTO_DETECT)
            {
                assembler = this.GuessAssemblerFromBuffer(buffer);
            }

            // Return appropriate tokenizer based on selected assembler
            switch (assembler)
            {
                case AssemblerEnum.MASM:
                    return new MasmTokenTagger(_asmDudeTools);

                case AssemblerEnum.NASM_INTEL:
                    return new NasmIntelTokenTagger(_asmDudeTools);

                case AssemblerEnum.NASM_ATT:
                    return new NasmAttTokenTagger(_asmDudeTools);

                default:
                    // Default to MASM if unknown
                    return new MasmTokenTagger(_asmDudeTools);
            }
        }

        /// <summary>
        /// Guesses the assembler flavor from the first 40 lines of the buffer.
        /// Heuristics:
        /// - MASM: uses "PROC", "ENDP", "SEGMENT", "ENDS"
        /// - NASM Intel: uses "db", "dw", "dd", "dq"
        /// - NASM AT&T: uses % prefix for registers, $ for immediates
        /// </summary>
        private AssemblerEnum GuessAssemblerFromBuffer(ITextBuffer buffer)
        {
            int lineCount = buffer.CurrentSnapshot.LineCount;
            int linesToCheck = System.Math.Min(40, lineCount);

            for (int i = 0; i < linesToCheck; i++)
            {
                var line = buffer.CurrentSnapshot.GetLineFromLineNumber(i);
                string text = line.GetText().ToLowerInvariant();

                // MASM indicators
                if (text.Contains("proc") || text.Contains("endp") ||
                    text.Contains("segment") || text.Contains("ends") ||
                    text.Contains("assume"))
                {
                    return AssemblerEnum.MASM;
                }

                // NASM AT&T indicators (% prefix for registers, $ for immediates)
                if (text.Contains("%") || text.Contains("movl") || text.Contains("movq"))
                {
                    return AssemblerEnum.NASM_ATT;
                }

                // NASM Intel indicators
                if (text.Contains("resb") || text.Contains("resw") ||
                    text.Contains("resd") || text.Contains("resq"))
                {
                    return AssemblerEnum.NASM_INTEL;
                }
            }

            // Default to AUTO_DETECT which will be handled by caller
            return AssemblerEnum.AUTO_DETECT;
        }
    }
}
