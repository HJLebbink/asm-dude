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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using AsmTools;
    using AsmDude3.Tools;

    /// <summary>
    /// Tokenizes disassembly output (debugger window) in NASM AT&T format.
    /// Uses Parse.ParseAttDisassembly() from AsmTools to parse debugger disassembly output.
    /// </summary>
    internal sealed class NasmAttDisassemblyTokenTagger : ITagger<AsmTokenTag>
    {
        // Pre-allocate token tags for performance
        private readonly AsmTokenTag _mnemonicTag;
        private readonly AsmTokenTag _registerTag;
        private readonly AsmTokenTag _remarkTag;
        private readonly AsmTokenTag _directiveTag;
        private readonly AsmTokenTag _constantTag;
        private readonly AsmTokenTag _jumpTag;
        private readonly AsmTokenTag _labelTag;
        private readonly AsmTokenTag _labelDefTag;
        private readonly AsmTokenTag _miscTag;
        private readonly AsmTokenTag _unknownTag;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public NasmAttDisassemblyTokenTagger(ITextBuffer buffer)
        {
            // Pre-allocate one token tag for each token type
            this._mnemonicTag = new AsmTokenTag(AsmTokenType.Mnemonic);
            this._registerTag = new AsmTokenTag(AsmTokenType.Register);
            this._remarkTag = new AsmTokenTag(AsmTokenType.Remark);
            this._directiveTag = new AsmTokenTag(AsmTokenType.Directive);
            this._constantTag = new AsmTokenTag(AsmTokenType.Constant);
            this._jumpTag = new AsmTokenTag(AsmTokenType.Jump);
            this._labelTag = new AsmTokenTag(AsmTokenType.Label);
            this._labelDefTag = new AsmTokenTag(AsmTokenType.LabelDef);
            this._miscTag = new AsmTokenTag(AsmTokenType.Misc);
            this._unknownTag = new AsmTokenTag(AsmTokenType.UNKNOWN);
        }

        /// <summary>
        /// Gets token tags for the specified spans in disassembly output (NASM AT&T format)
        /// </summary>
        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime startTime = DateTime.Now;

            if (spans.Count == 0)
            {
                yield break;
            }

            try
            {
                foreach (SnapshotSpan curSpan in spans)
                {
                    string content = curSpan.GetText();
                    string[] lines = content.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length > 0)
                    {
                        int startLineNumber = curSpan.Start.GetContainingLineNumber();
                        var snapshot = curSpan.Snapshot;
                        int offset = curSpan.Start.GetContainingLine().Start.Position;

                        for (int i = 0; i < lines.Length; ++i)
                        {
                            string lineStr = lines[i];
                            if (lineStr.Length > 0)
                            {
                                foreach ((int beginPos, int endPos, AsmTokenType type) in Parse.ParseAttDisassembly(lineStr, null))
                                {
                                    int length = endPos - beginPos;
                                    int beginPosOverall = beginPos + offset;
                                    yield return new TagSpan<AsmTokenTag>(new SnapshotSpan(snapshot, new Span(beginPosOverall, length)), this.GetAsmTokenTag(type));
                                }
                            }
                            offset += lineStr.Length;
                        }
                    }
                }
            }
            finally
            {
                AsmDudeToolsStatic.Print_Speed_Warning(startTime, "NasmAttDisassemblyTokenTagger.GetTags");
            }
        }

        /// <summary>
        /// Maps an AsmTokenType to a pre-allocated AsmTokenTag
        /// </summary>
        private AsmTokenTag GetAsmTokenTag(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Mnemonic:
                case AsmTokenType.MnemonicOff:
                    return this._mnemonicTag;
                case AsmTokenType.Register:
                    return this._registerTag;
                case AsmTokenType.Remark:
                    return this._remarkTag;
                case AsmTokenType.Directive:
                    return this._directiveTag;
                case AsmTokenType.Constant:
                    return this._constantTag;
                case AsmTokenType.Jump:
                    return this._jumpTag;
                case AsmTokenType.Label:
                    return this._labelTag;
                case AsmTokenType.LabelDef:
                    return this._labelDefTag;
                case AsmTokenType.Misc:
                    return this._miscTag;
                default:
                    return this._unknownTag;
            }
        }
    }
}
