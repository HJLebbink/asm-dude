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
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using AsmTools;
    using AsmDude3.Tools;

    /// <summary>
    /// Tokenizes NASM AT&T syntax (nasm -f elf style).
    /// Uses Parse.ParseNasmAtt() from AsmTools to parse NASM AT&T syntax.
    /// </summary>
    public class NasmAttTokenTagger : ITagger<AsmTokenTag>
    {
        // Pre-allocate token tags for performance
        private readonly AsmTokenTag[] _tokenTags = new AsmTokenTag[13];
        private readonly AsmDude2Tools _asmDudeTools;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public NasmAttTokenTagger(AsmDude2Tools asmDudeTools)
        {
            this._asmDudeTools = asmDudeTools;
            // Pre-allocate one token tag for each token type
            this._tokenTags[(int)AsmTokenType.Mnemonic] = new AsmTokenTag(AsmTokenType.Mnemonic);
            this._tokenTags[(int)AsmTokenType.MnemonicOff] = new AsmTokenTag(AsmTokenType.MnemonicOff);
            this._tokenTags[(int)AsmTokenType.Register] = new AsmTokenTag(AsmTokenType.Register);
            this._tokenTags[(int)AsmTokenType.Remark] = new AsmTokenTag(AsmTokenType.Remark);
            this._tokenTags[(int)AsmTokenType.Directive] = new AsmTokenTag(AsmTokenType.Directive);
            this._tokenTags[(int)AsmTokenType.Jump] = new AsmTokenTag(AsmTokenType.Jump);
            this._tokenTags[(int)AsmTokenType.Label] = new AsmTokenTag(AsmTokenType.Label);
            this._tokenTags[(int)AsmTokenType.LabelDef] = new AsmTokenTag(AsmTokenType.LabelDef);
            this._tokenTags[(int)AsmTokenType.Constant] = new AsmTokenTag(AsmTokenType.Constant);
            this._tokenTags[(int)AsmTokenType.Misc] = new AsmTokenTag(AsmTokenType.Misc);
            this._tokenTags[(int)AsmTokenType.UserDefined1] = new AsmTokenTag(AsmTokenType.UserDefined1);
            this._tokenTags[(int)AsmTokenType.UserDefined2] = new AsmTokenTag(AsmTokenType.UserDefined2);
            this._tokenTags[(int)AsmTokenType.UserDefined3] = new AsmTokenTag(AsmTokenType.UserDefined3);
        }

        /// <summary>
        /// Gets token tags for the specified spans in NASM AT&T syntax
        /// </summary>
        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime startTime = DateTime.Now;

            try
            {
                foreach (var span in spans)
                {
                    string line = span.GetText();
                    var tokens = Parse.ParseNasmAtt(line, this._asmDudeTools);

                    foreach (var (beginPos, length, type) in tokens)
                    {
                        // Calculate the actual position in the snapshot
                        int startPos = span.Start.Position + beginPos;
                        int len = length;

                        if (startPos + len > span.End.Position)
                        {
                            // Clip token to span boundaries
                            len = span.End.Position - startPos;
                        }

                        if (len > 0)
                        {
                            var tokenSpan = new SnapshotSpan(span.Snapshot, startPos, len);
                            var tag = this.GetAsmTokenTag(type);
                            if (tag != null)
                            {
                                yield return new TagSpan<AsmTokenTag>(tokenSpan, tag);
                            }
                        }
                    }
                }
            }
            finally
            {
                AsmDudeToolsStatic.Print_Speed_Warning(startTime, "NasmAttTokenTagger.GetTags");
            }
        }

        /// <summary>
        /// Gets the pre-allocated token tag for the given token type
        /// </summary>
        private AsmTokenTag GetAsmTokenTag(AsmTokenType type)
        {
            int index = (int)type;
            if (index >= 0 && index < this._tokenTags.Length)
            {
                return this._tokenTags[index];
            }
            return null;
        }
    }
}
