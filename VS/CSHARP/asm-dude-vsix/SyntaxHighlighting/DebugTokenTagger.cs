// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmTools;

namespace AsmDude
{
    internal sealed class DebugTokenTagger : ITagger<AsmTokenTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly AsmDudeTools _asmDudeTools = null;

        private readonly AsmTokenTag _mnemonic;
        private readonly AsmTokenTag _jump;

        internal DebugTokenTagger(ITextBuffer buffer)
        {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeTools.Instance;

            this._mnemonic = new AsmTokenTag(AsmTokenType.Mnemonic);
            this._jump = new AsmTokenTag(AsmTokenType.Jump);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime time1 = DateTime.Now;

            if (spans.Count == 0)
            {  //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string line = containingLine.GetText().ToUpper();
                IList<(int, int, bool)> pos = AsmSourceTools.SplitIntoKeywordPos(line);

                int offset = containingLine.Start.Position;
                int nKeywords = pos.Count;

                for (int k = 0; k < nKeywords; k++)
                {
                    string asmToken = Keyword(pos[k], line);
                    AsmTokenType keywordType = this._asmDudeTools.Get_Token_Type(asmToken);
                    if ((keywordType == AsmTokenType.Mnemonic) ||
                        (keywordType == AsmTokenType.Jump))
                    {
                        yield return new TagSpan<AsmTokenTag>(New_Span(pos[k], offset, curSpan), new AsmTokenTag(keywordType));
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "DebugTokenTagger");
        }

        #region Public Static Methods

        public static string Keyword((int, int, bool) pos, string line)
        {
            return line.Substring(pos.Item1, pos.Item2 - pos.Item1);
        }

        public static SnapshotSpan New_Span((int, int, bool) pos, int offset, SnapshotSpan lineSnapShot)
        {
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.Item1 + offset, pos.Item2 - pos.Item1));
        }
        #endregion Public Static Methods
    }
}
