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

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmTools;

namespace AsmDude {

    internal sealed class MasmTokenTagger : ITagger<AsmTokenTag> {

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


        internal MasmTokenTagger(ITextBuffer buffer) {
            this._buffer = buffer;
            this._asmDudeTools = AsmDudeTools.Instance;

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
                        SnapshotSpan label = NasmTokenTagger.newSpan(pos[k], offset, curSpan);
                        string labelString = label.GetText();
                        //AsmDudeToolsStatic.Output(string.Format("INFO: found label {0}", labelString));
                        if (labelString.Equals("@@")) {
                            // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                        } else {
                            yield return new TagSpan<AsmTokenTag>(label, this._labelDef);
                        }
                        continue;
                    }

                    string asmToken = NasmTokenTagger.keyword(pos[k], line);
                    if (AsmSourceTools.isRemarkChar(asmToken[0])) {
                        yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._remark);
                        continue;
                    }

                    AsmTokenType keywordType = this._asmDudeTools.getTokenType(asmToken);
                    switch (keywordType) {
                        case AsmTokenType.Jump:
                            #region Jump
                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._jump);

                            k++;
                            if (k == nKeywords) break;

                            string asmToken2 = NasmTokenTagger.keyword(pos[k], line);
                            if (!asmToken2[0].Equals('.')) {
                                switch (asmToken2) {
                                    case "$":
                                    case "$B":
                                    case "$F":
                                        // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                        break;
                                    case "WORD":
                                    case "DWORD":
                                    case "QWORD":
                                    case "SHORT":
                                    case "NEAR":
                                        yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._misc);

                                        k++;
                                        if (k == nKeywords) break;
                                        string asmToken3 = NasmTokenTagger.keyword(pos[k], line);
                                        if (!asmToken3[0].Equals('.')) {
                                            switch (asmToken3) {
                                                case "$":
                                                case "$B":
                                                case "$F":
                                                    // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                                    break;
                                                case "PTR":
                                                    yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._misc);
                                                    break;
                                                default:
                                                    yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._label);
                                                    break;
                                            }
                                        }
                                        break;

                                    default:
                                        if (RegisterTools.isRegister(asmToken2)) {
                                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._register);
                                        } else {
                                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._label);
                                        }
                                        break;
                                }
                            }
                            break;
                            #endregion Jump
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            #region UNKNOWN
                            if (AsmTools.AsmSourceTools.isConstant(asmToken)) {
                                yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._constant);

                            } else if (asmToken.StartsWith("\"") && asmToken.EndsWith("\"")) {
                                yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._constant);

                            } else {
                                bool isUnknown = true;

                                // do one word lookahead; see whether we can understand the current unknown word
                                if ((k + 1) < nKeywords) {
                                    k++;
                                    string nextKeyword = NasmTokenTagger.keyword(pos[k], line);

                                    switch (nextKeyword) {
                                        case "PROC":
                                        case "EQU":
                                        case "LABEL":
                                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k - 1], offset, curSpan), this._labelDef);
                                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._directive);
                                            isUnknown = false;
                                            break;

                                        default:
                                            k--;
                                            break;
                                    }
                                }

                                // do one word look back; see whether we can understand the current unknown word
                                if (k > 0) {
                                    string previousKeyword = NasmTokenTagger.keyword(pos[k-1], line);
                                    switch (previousKeyword) {
                                        case "ALIAS":
                                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._labelDef);
                                            isUnknown = false;
                                            break;
                                        default:
                                            break;
                                    }

                                }

                                if (isUnknown) {
                                    yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._UNKNOWN);
                                }

                            }
                            break;
                        #endregion UNKNOWN
                        case AsmTokenType.Directive:
                            #region Directive
                            switch (this._asmDudeTools.getAssembler(asmToken)) {
                                case AssemblerEnum.MASM:
                                case AssemblerEnum.UNKNOWN:
                                    yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), this._directive);
                                    break;
                                default:
                                    break;
                            }
                            break;
                        #endregion Directive
                        default:
                            yield return new TagSpan<AsmTokenTag>(NasmTokenTagger.newSpan(pos[k], offset, curSpan), new AsmTokenTag(keywordType));
                            break;
                    }
                }
            }
            AsmDudeToolsStatic.printSpeedWarning(time1, "MasmTokenTagger");
        }
    }
}
