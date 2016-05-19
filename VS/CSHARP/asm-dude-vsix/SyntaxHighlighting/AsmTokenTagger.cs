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

        private readonly AsmTokenTag _mnemonic;
        private readonly AsmTokenTag _register;
        private readonly AsmTokenTag _remark;
        private readonly AsmTokenTag _directive;
        private readonly AsmTokenTag _constant;
        private readonly AsmTokenTag _jump;
        private readonly AsmTokenTag _label;
        private readonly AsmTokenTag _labelDef;
        private readonly AsmTokenTag _misc;
        private readonly AsmTokenTag _UNKNOWN;


        internal AsmTokenTagger(ITextBuffer buffer) {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeToolsStatic.getAsmDudeTools(buffer);

            this._mnemonic = new AsmTokenTag(AsmTokenType.Mnemonic);
            this._register = new AsmTokenTag(AsmTokenType.Register);
            this._remark = new AsmTokenTag(AsmTokenType.Remark);
            this._directive = new AsmTokenTag(AsmTokenType.Directive);
            this._constant = new AsmTokenTag(AsmTokenType.Constant);
            this._jump = new AsmTokenTag(AsmTokenType.Jump);
            this._label = new AsmTokenTag(AsmTokenType.Label);
            this._labelDef = new AsmTokenTag(AsmTokenType.LabelDef);
            this._misc = new AsmTokenTag(AsmTokenType.Misc);
            this._UNKNOWN = new AsmTokenTag(AsmTokenType.UNKNOWN);
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
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string line = containingLine.GetText().ToUpper();
                IList<Tuple<int, int, bool>> pos = AsmSourceTools.splitIntoKeywordPos(line);

                int offset = containingLine.Start.Position;
                int nKeywords = pos.Count;

                for (int k = 0; k < nKeywords; k++) {

                    if (pos[k].Item3) {
                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._labelDef); 
                        continue;
                    }

                    string asmToken = keyword(pos[k], line);
                    if (AsmSourceTools.isRemarkChar(asmToken[0])) {
                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._remark);
                        continue;
                    }

                    AsmTokenType keywordType = this._asmDudeTools.getTokenType(asmToken);
                    switch (keywordType) {
                        case AsmTokenType.Jump:
                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._jump);

                            k++;
                            if (k == nKeywords) break;

                            string asmToken2 = keyword(pos[k], line);
                            switch (asmToken2) {
                                case "$":
                                    break;
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._misc);

                                    k++;
                                    if (k == nKeywords) break;

                                    switch (keyword(pos[k], line)) {
                                        case "$": break;
                                        case "PTR":
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._misc);
                                            break;
                                        default:
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._label);
                                            break;
                                    }
                                    break;

                                default:
                                    if (RegisterTools.isRegister(asmToken2)) {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._register);
                                    } else {
                                        yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._label);
                                    }
                                    break;
                            }
                            break;

                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            if (AsmTools.AsmSourceTools.isConstant(asmToken)) {
                                yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._constant);

                            } else if (asmToken.StartsWith("\"") && asmToken.EndsWith("\"")) {
                                yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._constant);

                            } else {
                                bool isUnknown = true;

                                if ((k + 1) < nKeywords) {
                                    k++;
                                    string nextKeyword = keyword(pos[k], line);

                                    switch (nextKeyword) {
                                        case "PROC":
                                        case "EQU":
                                        case "LABEL":
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k - 1], offset, curSpan), this._labelDef);
                                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._directive);
                                            isUnknown = false;
                                            break;

                                        default:
                                            k--;
                                            break;
                                    }
                                }

                                if (isUnknown) {
                                    yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), this._UNKNOWN);
                                }

                            }
                            break;

                        default:
                            yield return new TagSpan<AsmTokenTag>(newSpan(pos[k], offset, curSpan), new AsmTokenTag(keywordType));
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
