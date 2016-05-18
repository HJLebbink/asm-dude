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

        internal AsmTokenTagger(ITextBuffer buffer) {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeToolsStatic.getAsmDudeTools(buffer);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged {
            add { }
            remove { }
        }

        private bool advance(
            ref int tokenId, 
            ref int curLoc,
            ref int nextLoc, 
            out string asmToken, 
            out SnapshotSpan? asmTokenSpan,
            string[] tokens,
            SnapshotSpan curSpan) {

            var tup = getNextToken(tokenId, nextLoc, tokens);
            tokenId = tup.Item2;
            nextLoc = tup.Item3;

            if (tup.Item1) {
                asmToken = tup.Item4;
                curLoc = nextLoc - (asmToken.Length + 1);

                asmTokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                //if (asmTokenSpan.Value.IntersectsWith(curSpan)) {
                    return true;
                //TODO find out what it means if not asmTokenSpan.Value.IntersectsWith(curSpan)
                //}
            }

            asmTokenSpan = null;
            asmToken = null;
            return false;
        }

        private string keyword(Tuple<int, int, bool> pos, string line) {
            return line.Substring(pos.Item1, pos.Item2 - pos.Item1);
        }

        private SnapshotSpan newSpan(Tuple<int, int, bool> pos, int offset, SnapshotSpan lineSnapShot) {
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.Item1 + offset, pos.Item2 - pos.Item1));
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
                string lineOpcodes = containingLine.GetText().ToUpper();
                int offset = containingLine.Start.Position;

                IList<Tuple<int, int, bool>> pos = AsmSourceTools.splitIntoKeywordPos(lineOpcodes);
                int nKeywords = pos.Count;

                for (int k = 0; k < nKeywords; k++) {

                    if (pos[k].Item3) {
                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.LabelDef));
                        continue;
                    }

                    string asmToken = keyword(pos[k], lineOpcodes);
                    if (AsmSourceTools.isRemarkChar(asmToken[0])) {
                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Remark));
                        continue;
                    }

                    switch (this._asmDudeTools.getTokenType(asmToken)) {
                        case AsmTokenType.Jump:
                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Jump));

                            k++;
                            if (k == nKeywords) break;

                            string asmToken2 = keyword(pos[k], lineOpcodes);
                            switch (asmToken2) {
                                case "$":
                                    break;
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                    yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Misc));
                                    break;
                                case "SHORT":
                                case "NEAR":
                                    yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Misc));

                                    k++;
                                    if (k == nKeywords) break;

                                    switch (keyword(pos[k], lineOpcodes)) {
                                        case "$": break;
                                        case "PTR":
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Misc));

                                            k++;
                                            if (k == nKeywords) break;

                                            if (!keyword(pos[k], lineOpcodes).Equals("$")) {
                                                k++;
                                                if (k == nKeywords) break;
                                                yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Label));
                                            }
                                            break;
                                        default:
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Label));
                                            break;
                                    }
                                    break;

                                default:
                                    if (RegisterTools.isRegister(asmToken2)) {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Register));
                                    } else {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Label));
                                    }
                                    break;
                            }
                            break;

                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            if (AsmTools.AsmSourceTools.isConstant(asmToken)) {
                                yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Constant));

                            } else if (asmToken.StartsWith("\"") && asmToken.EndsWith("\"")) {
                                yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Constant));

                            } else {
                                bool isUnknown = true;

                                if ((k + 1) < nKeywords) {
                                    k++;
                                    string nextKeyword = keyword(pos[k], lineOpcodes);

                                    if (nextKeyword.Equals("PROC")) {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k - 1], offset, curSpan), new AsmTokenTag(AsmTokenType.LabelDef));
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Directive));
                                        isUnknown = false;
                                    } else if (nextKeyword.Equals("EQU")) {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k - 1], offset, curSpan), new AsmTokenTag(AsmTokenType.LabelDef));
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.Directive));
                                        isUnknown = false;
                                    } else {
                                        k--;
                                    }
                                }

                                if (isUnknown) {
                                    yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(AsmTokenType.UNKNOWN));
                                }

                            }
                            break;

                        default:
                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(this._asmDudeTools.getTokenType(asmToken)));
                            break;
                    }
                }
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
