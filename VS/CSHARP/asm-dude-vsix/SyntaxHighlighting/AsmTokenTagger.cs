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

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmTools;

namespace AsmDude {

    internal sealed class AsmTokenTagger : ITagger<AsmTokenTag> {

        private readonly ITextBuffer _buffer;
        private readonly AsmDudeTools _asmDudeTools = null;

        static char[] splitChars = { ' ', ',', '\t', '+', '-', '*', '[', ']', '(', ')', ':' }; //TODO remove this to AsmDudeTools

        internal AsmTokenTagger(ITextBuffer buffer) {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeToolsStatic.getAsmDudeTools(buffer);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

            DateTime time1 = DateTime.Now;

            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans) {
                // split everything before the remark char according to this template
                // LABEL: MNEMONIC OPERATOR1, OPERATOR2, OPERATOR3 (with OPERATOR1-3 are optional)

                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                string line = containingLine.GetText();
                int offset = containingLine.Start.Position;

                #region Handle Labels Definitions

                Tuple<bool, int, int> labelPos = AsmTools.AsmSourceTools.getLabelDefPos(line);
                bool labelExists = labelPos.Item1;

                if (labelExists) {
                    int labelBeginPos = labelPos.Item2;
                    int labelEndPos = labelPos.Item3;

                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(labelBeginPos + offset, labelEndPos - labelBeginPos));
                    if (tokenSpan.IntersectsWith(curSpan)) {
                        //AsmDudeToolsStatic.Output("found label " + line.Substring(labelBeginPos, labelEndPos) + "; begin pos="+ labelBeginPos+"; end pos="+ labelEndPos);
                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.LabelDef));
                    }
                }
                #endregion Handle Labels Definitions

                #region Handle Remarks
                // 1] find the first position (if any) of the remark char
                Tuple<bool, int, int> remarkPos = AsmTools.AsmSourceTools.getRemarkPos(line);
                bool remarkExists = remarkPos.Item1;

                if (remarkExists) {
                    int remarkBeginPos = remarkPos.Item2;
                    int remarkEndPos = remarkPos.Item3;

                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(remarkBeginPos + offset, remarkEndPos - remarkBeginPos));
                    if (tokenSpan.IntersectsWith(curSpan)) {
                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Remark));
                    }
                }
                #endregion Handle Remarks

                #region Handle Opcodes
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

                            case AsmTokenType.Jump: {
                                    #region Jump
                                    //AsmDudeToolsStatic.Output("current jump token \"" + asmToken + "\"");

                                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                    if (tokenSpan.IntersectsWith(curSpan)) {
                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Jump));
                                    } else {
                                        //AsmDudeToolsStatic.Output("AsmTokenTagger:GetTags: does this even happen? A");
                                        // Yes this does happen, but why?
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
                                            if (!asmToken.Equals("short", StringComparison.CurrentCultureIgnoreCase)) {
                                                if (RegisterTools.isRegister(asmToken)) {
                                                    yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Register));
                                                } else {
                                                    yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Label));
                                                }
                                           } else { 
                                                yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Misc));

                                                tup = getNextToken(tokenId, nextLoc, tokens);
                                                tokenId = tup.Item2;
                                                nextLoc = tup.Item3;

                                                if (tup.Item1) {
                                                    asmToken = tup.Item4;
                                                    curLoc = nextLoc - (asmToken.Length + 1);

                                                    tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                                    if (tokenSpan.IntersectsWith(curSpan)) {
                                                        //AsmDudeToolsStatic.Output(string.Format("AsmTokenTagger:GetTags: label token {0} at location {1} = \"{2}\"", tokenId, curLoc, asmToken));
                                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Label));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                                    #endregion
                                }
                            case AsmTokenType.UNKNOWN: {// asmToken is not a known keyword, check if it is numerical
                                    #region UNKNOWN

                                    // just peek the next token to check whether this unknown word is a part of a Mask directive
                                    var tup2 = getNextToken(tokenId, nextLoc, tokens);
                                    string nextToken = tup2.Item4;
                                    if (nextToken.Equals("proc", StringComparison.CurrentCultureIgnoreCase)) {
                                        AsmDudeToolsStatic.Output(string.Format("INFO: found Masm directive " + asmToken));
                                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.LabelDef));

                                    } else {
                                        if (AsmTools.AsmSourceTools.isConstant(asmToken)) {
                                            var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                            if (tokenSpan.IntersectsWith(curSpan)) {
                                                yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenType.Constant));
                                            }
                                        }
                                    }
                                    break;
                                    #endregion
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
                #endregion Handle Opcodes
            }

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took AsmTokenTagger {0:F3} seconds to tag", elapsedSec));
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
