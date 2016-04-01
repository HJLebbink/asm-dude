// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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
using System.ComponentModel.Composition;

namespace AsmDude {

    public class AsmTokenTag : ITag {
        public TokenType type { get; private set; }

        public AsmTokenTag(TokenType type) {
            this.type = type;
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
        }
    }

    internal sealed class AsmTokenTagger : ITagger<AsmTokenTag> {

        private ITextBuffer _buffer;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        static char[] splitChars = { ' ', ',', '\t', '+', '-', '*', '[', ']' }; //TODO remove this to AsmDudeTools

        internal AsmTokenTagger(ITextBuffer buffer) {
            this._buffer = buffer;
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged {
            add { }
            remove { }
        }


        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            foreach (SnapshotSpan curSpan in spans) {
                // split everything before the remark char according to this template
                // LABEL: MNEMONIC OPERATOR1, OPERATOR2, OPERATOR3 (with OPERATOR1-3 are optional)

                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                string line = containingLine.GetText();
                int offset = containingLine.Start.Position;

                #region handle labels

                Tuple<bool, int, int> labelPos = AsmDudeToolsStatic.getLabelPos(line);
                bool labelExists = labelPos.Item1;

                if (labelExists) {
                    int labelBeginPos = labelPos.Item2;
                    int labelEndPos = labelPos.Item3;

                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(labelBeginPos + offset, labelEndPos - labelBeginPos));
                    if (tokenSpan.IntersectsWith(curSpan)) {
                        //AsmDudeToolsStatic.Output("found label " + line.Substring(labelBeginPos, labelEndPos) + "; begin pos="+ labelBeginPos+"; end pos="+ labelEndPos);
                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(TokenType.Label));
                    }
                }
                #endregion

                #region remarks
                // 1] find the first position (if any) of the remark char
                Tuple<bool, int, int> remarkPos = AsmDudeToolsStatic.getRemarkPos(line);
                bool remarkExists = remarkPos.Item1;

                if (remarkExists) {
                    int remarkBeginPos = remarkPos.Item2;
                    int remarkEndPos = remarkPos.Item3;

                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(remarkBeginPos + offset, remarkEndPos - remarkBeginPos));
                    if (tokenSpan.IntersectsWith(curSpan)) {
                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(TokenType.Remark));
                    }
                }
                #endregion

                #region opcode
                // we are only interested in text between labelEndPos+1 and remarkBeginPos
                int beginPos = (labelExists) ? labelPos.Item3 - 1 : 0;
                int endPos = (remarkExists) ? remarkPos.Item2 : line.Length;

                string[] tokens = line.Substring(beginPos, endPos - beginPos).ToUpper().Split(splitChars);
                int curLoc = containingLine.Start.Position + beginPos;
                int nextLoc = curLoc;

                int tokenId = 0;
                while (tokenId < tokens.Length) {

                    var tup = getNextToken(tokenId, nextLoc, tokens);
                    tokenId = tup.Item2;
                    nextLoc = tup.Item3;

                    if (tup.Item1) {
                        string asmToken = tup.Item4;
                        curLoc = nextLoc - (asmToken.Length + 1);

                        //AsmDudeToolsStatic.Output("token "+tokenId+" at location "+curLoc+" = \"" + asmToken + "\"");

                        switch (this._asmDudeTools.getTokenType(asmToken)) {

                            case TokenType.Jump: {
                                    //AsmDudeToolsStatic.Output("current jump token \"" + asmToken + "\"");

                                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                    if (tokenSpan.IntersectsWith(curSpan)) {
                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(TokenType.Jump));
                                    }
                                    tup = getNextToken(tokenId, nextLoc, tokens);
                                    tokenId = tup.Item2;
                                    nextLoc = tup.Item3;

                                    if (tup.Item1) {
                                        asmToken = tup.Item4;
                                        curLoc = nextLoc - (asmToken.Length + 1);
                                        //Debug.WriteLine("label token " + tokenId + " at location " + curLoc + " = \"" + asmToken + "\"");

                                        tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                        if (tokenSpan.IntersectsWith(curSpan)) {
                                            yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(TokenType.Label));
                                        }
                                    }
                                    break;
                                }
                            case TokenType.UNKNOWN: {// asmToken is not a known keyword, check if it is numerical
                                    if (AsmDudeToolsStatic.isConstant(asmToken)) {
                                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                        if (tokenSpan.IntersectsWith(curSpan)) {
                                            yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(TokenType.Constant));
                                        }
                                    }
                                    break;
                                }
                            default: {
                                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                    if (tokenSpan.IntersectsWith(curSpan)) {
                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(this._asmDudeTools.getTokenType(asmToken)));
                                    }
                                    break;
                                }
                        }
                    }
                }
                #endregion
            }
        }

        // return true, nextTokenId, tokenEndPos, tokenString
        private Tuple<bool, int, int, string> getNextToken(int tokenId, int startLoc, string[] tokens) {
            int nextTokenId = tokenId;
            int nextLoc = startLoc;

            while (nextTokenId < tokens.Length) {
                string asmToken = tokens[nextTokenId];
                nextTokenId++;
                //Debug.WriteLine("getNextToken:nextTokenId=" + nextTokenId+ "; asmToken=\""+asmToken+"\"");
                if (asmToken.Length > 0) {
                    nextLoc += asmToken.Length + 1; //add an extra char location because of the separator
                    return new Tuple<bool, int, int, string>(true, nextTokenId, nextLoc, asmToken.ToUpper());
                } else {
                    nextLoc++;
                }
            }
            return new Tuple<bool, int, int, string>(false, nextTokenId, nextLoc, "");
        }
    }
}
