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

namespace AsmDude2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using AsmDude2.SyntaxHighlighting;
    using AsmDude2.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class NasmAttTokenTagger : ITagger<AsmTokenTag>
    {
        private readonly ITextBuffer buffer_;
        private readonly AsmDude2Tools asmDudeTools_ = null;

        private readonly AsmTokenTag mnemonic_;
        private readonly AsmTokenTag register_;
        private readonly AsmTokenTag remark_;
        private readonly AsmTokenTag directive_;
        private readonly AsmTokenTag constant_;
        private readonly AsmTokenTag jump_;
        private readonly AsmTokenTag label_;
        private readonly AsmTokenTag labelDef_;
        private readonly AsmTokenTag misc_;
        private readonly AsmTokenTag userDefined1_;
        private readonly AsmTokenTag userDefined2_;
        private readonly AsmTokenTag userDefined3_;
        private readonly AsmTokenTag UNKNOWN_;

        internal NasmAttTokenTagger(ITextBuffer buffer)
        {
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.asmDudeTools_ = AsmDude2Tools.Instance;

            this.mnemonic_ = new AsmTokenTag(AsmTokenType.Mnemonic);
            this.register_ = new AsmTokenTag(AsmTokenType.Register);
            this.remark_ = new AsmTokenTag(AsmTokenType.Remark);
            this.directive_ = new AsmTokenTag(AsmTokenType.Directive);
            this.constant_ = new AsmTokenTag(AsmTokenType.Constant);
            this.jump_ = new AsmTokenTag(AsmTokenType.Jump);
            this.label_ = new AsmTokenTag(AsmTokenType.Label);
            this.labelDef_ = new AsmTokenTag(AsmTokenType.LabelDef);
            this.misc_ = new AsmTokenTag(AsmTokenType.Misc);
            this.userDefined1_ = new AsmTokenTag(AsmTokenType.UserDefined1);
            this.userDefined2_ = new AsmTokenTag(AsmTokenType.UserDefined2);
            this.userDefined3_ = new AsmTokenTag(AsmTokenType.UserDefined3);
            this.UNKNOWN_ = new AsmTokenTag(AsmTokenType.UNKNOWN);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime time1 = DateTime.Now;

            if (spans.Count == 0)
            { //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string line_upcase = containingLine.GetText().ToUpperInvariant();
                List<(int beginPos, int length, bool isLabel)> pos = new List<(int beginPos, int length, bool isLabel)>(AsmSourceTools.SplitIntoKeywordPos(line_upcase));

                int offset = containingLine.Start.Position;
                int nKeywords = pos.Count;

                for (int k = 0; k < nKeywords; k++)
                {
                    string asmToken = AsmSourceTools.Keyword(pos[k], line_upcase);

                    // keyword starts with a remark char
                    if (AsmSourceTools.IsRemarkChar(asmToken[0]))
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.remark_);
                        continue;
                    }

                    // keyword k is a label definition
                    if (pos[k].isLabel)
                    {
                        //AsmDudeToolsStatic.Output_INFO("NasmTokenTagger:GetTags: found label " +asmToken);
                        if (this.IsProperLabelDef(asmToken, containingLine.LineNumber, out AsmTokenTag asmTokenTag))
                        {
                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), asmTokenTag);
                            continue;
                        }
                    }
                    AsmTokenType keywordType = this.asmDudeTools_.Get_Token_Type_Att(asmToken);
                    switch (keywordType)
                    {
                        case AsmTokenType.Jump:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.jump_);

                                k++; // goto the next word
                                if (k == nKeywords)
                                {
                                    break; // there are no next words
                                    //TODO HJ 01-06-19 should be a warning that there is no label
                                }

                                string asmToken2 = AsmSourceTools.Keyword(pos[k], line_upcase);

                                if (AsmSourceTools.IsRemarkChar(asmToken2[0]))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.remark_);
                                    continue;
                                    //TODO HJ 01-06-19 should be a warning that there is no label
                                }

                                switch (asmToken2)
                                {
                                    case "WORD":
                                    case "DWORD":
                                    case "QWORD":
                                    case "SHORT":
                                    case "NEAR":
                                        {
                                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.misc_);

                                            k++;
                                            if (k == nKeywords)
                                            {
                                                break;
                                            }

                                            string asmToken3 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                            if (asmToken3.Equals("PTR", StringComparison.Ordinal))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(New_Span(pos[k], offset, curSpan), this.misc_);
                                            }
                                            else
                                            {
                                                if (this.IsProperLabel(asmToken3, containingLine.LineNumber, out AsmTokenTag asmTokenTag))
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), asmTokenTag);
                                                }
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            if (RegisterTools.IsRegister(asmToken2, true))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.register_);
                                            }
                                            else if (AsmSourceTools.Evaluate_Constant(asmToken2, true).valid)
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                            }
                                            else if (this.IsProperLabel(asmToken2, containingLine.LineNumber, out AsmTokenTag asmTokenTag))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), asmTokenTag);
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            {
                                if (AsmSourceTools.Evaluate_Constant(asmToken, true).valid)
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                }
                                else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                }
                                else if (asmToken.StartsWith("$", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset + 1, curSpan), this.constant_);
                                }
                                else
                                {
                                    bool isUnknown = true;

                                    // do one word lookahead; see whether we can understand the current unknown word
                                    if ((k + 1) < nKeywords)
                                    {
                                        k++;
                                        string nextKeyword = AsmSourceTools.Keyword(pos[k], line_upcase);
                                        switch (nextKeyword)
                                        {
                                            case "LABEL":
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k - 1], offset, curSpan), this.labelDef_);
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);
                                                    isUnknown = false;
                                                    break;
                                                }
                                            default:
                                                {
                                                    k--;
                                                    break;
                                                }
                                        }
                                    }

                                    // do one word look back; see whether we can understand the current unknown word
                                    if (k > 0)
                                    {
                                        string previousKeyword = AsmSourceTools.Keyword(pos[k - 1], line_upcase);
                                        switch (previousKeyword)
                                        {
                                            case "ALIAS":
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.labelDef_);
                                                    isUnknown = false;
                                                    break;
                                                }
                                            case "INCLUDE":
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                                    isUnknown = false;
                                                    break;
                                                }
                                            default:
                                                {
                                                    break;
                                                }
                                        }
                                    }
                                    if (isUnknown)
                                    {
                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.UNKNOWN_);
                                    }
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                AssemblerEnum assember = this.asmDudeTools_.Get_Assembler(asmToken);
                                if (assember.HasFlag(AssemblerEnum.NASM_INTEL) || assember.HasFlag(AssemblerEnum.NASM_ATT))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);
                                }
                                break;
                            }
                        default:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), new AsmTokenTag(keywordType));
                                break;
                            }
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "NasmAttTokenTagger");
        }

        #region Public Static Methods

        public static bool Advance(
            ref int tokenId,
            ref int curLoc,
            ref int nextLoc,
            out string asmToken,
            out SnapshotSpan? asmTokenSpan,
            string[] tokens,
            SnapshotSpan curSpan)
        {
            Contract.Requires(curSpan != null);

            (bool valid, int nextTokenId, int tokenEndPos, string tokenSting) = Get_Next_Token(tokenId, nextLoc, tokens);
            tokenId = nextTokenId;
            nextLoc = tokenEndPos;

            if (valid)
            {
                asmToken = tokenSting;
                curLoc = nextLoc - (asmToken.Length + 1);

                asmTokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                //if (asmTokenSpan.Value.IntersectsWith(curSpan)) {
                return true;
                //TODO find out what it means if not asmTokenSpan.Value.IntersectsWith(curSpan)
                //}
            }
            else
            {
                asmToken = null;
                asmTokenSpan = null;
                return false;
            }
        }

        // return true, nextTokenId, tokenEndPos, tokenString
        public static (bool valid, int nextTokenId, int tokenEndPos, string tokenSting) Get_Next_Token(int tokenId, int startLoc, string[] tokens)
        {
            Contract.Requires(tokens != null);

            int nextTokenId = tokenId;
            int nextLoc = startLoc;

            while (nextTokenId < tokens.Length)
            {
                string asmToken = tokens[nextTokenId];
                nextTokenId++;
                if (asmToken.Length > 0)
                {
                    nextLoc += asmToken.Length + 1; //add an extra char location because of the separator
                    return (valid: true, nextTokenId: nextTokenId, tokenEndPos: nextLoc, tokenSting: asmToken.ToUpperInvariant());
                }
                else
                {
                    nextLoc++;
                }
            }
            return (valid: false, nextTokenId: nextTokenId, tokenEndPos: nextLoc, tokenSting: string.Empty);
        }

        public static string Keyword((int beginPos, int length, bool isLabel) pos, string line)
        {
            Contract.Requires(line != null);
            return line.Substring(pos.beginPos, pos.length - pos.beginPos);
        }

        public static SnapshotSpan New_Span((int beginPos, int length, bool isLabel) pos, int offset, SnapshotSpan lineSnapShot)
        {
            Contract.Requires(lineSnapShot != null);
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.beginPos + offset, pos.length - pos.beginPos));
        }
        #endregion Public Static Methods

        #region Private Member Methods

        private bool IsProperLabelDef(string asmToken, int lineNumber, out AsmTokenTag labelDefSpan)
        {
            Contract.Requires(asmToken != null);

            labelDefSpan = null;
            if (!this.IsExecutableCode(lineNumber))
            {
                return false;
            }

            if (asmToken.StartsWith(".", StringComparison.Ordinal))
            {
                if (this.Get_Last_Non_Local_Label(lineNumber, out string lastNonLocalLabel))
                {
                    labelDefSpan = new AsmTokenTag(AsmTokenType.LabelDef, lastNonLocalLabel);
                    return true;
                }
            }
            else
            {
                labelDefSpan = this.labelDef_;
                return true;
            }
            return false;
        }

        private bool IsProperLabel(string asmToken, int lineNumber, out AsmTokenTag labelSpan)
        {
            Contract.Requires(asmToken != null);

            labelSpan = null;

            //AsmDudeToolsStatic.Output_INFO("NasmTokenTagger:GetTags: found label " +asmToken);
            if (asmToken.StartsWith(".", StringComparison.Ordinal))
            {
                if (this.Get_Last_Non_Local_Label(lineNumber, out string lastNonLocalLabel))
                {
                    labelSpan = new AsmTokenTag(AsmTokenType.Label, lastNonLocalLabel);
                    return true;
                }
            }
            else
            {
                labelSpan = this.label_;
                return true;
            }
            labelSpan = null;
            return false;
        }

        private bool IsExecutableCode(int lineNumber)
        {
            for (int i = lineNumber - 1; i >= 0; --i)
            {
                string line_upcase = this.buffer_.CurrentSnapshot.GetLineFromLineNumber(i).GetText().ToUpperInvariant();
                IList<(int, int, bool)> pos = new List<(int, int, bool)>(AsmSourceTools.SplitIntoKeywordPos(line_upcase));
                if ((pos.Count > 0) && !pos[0].Item3)
                {
                    string keyword_upcase = AsmSourceTools.Keyword(pos[0], line_upcase);
                    if (AsmSourceTools.IsMnemonic(keyword_upcase, true))
                    {
                        return true;
                    }
                    switch (keyword_upcase)
                    {
                        case "STRUC": return false;
                        case "ENDSTRUC": return true;
                    }
                }
                if ((pos.Count > 1) && !pos[1].Item3)
                {
                    string keywordString = AsmSourceTools.Keyword(pos[1], line_upcase);
                    if (AsmSourceTools.IsMnemonic(keywordString, true))
                    {
                        return true;
                    }
                }
            }
            return true;
        }

        private bool Get_Last_Non_Local_Label(int lineNumber, out string lastNonLocalLabel)
        {
            //AsmDudeToolsStatic.Output_INFO("NasmTokenTagger:get_Last_Non_Local_Label: lineNumber=" + lineNumber);
            lastNonLocalLabel = null;

            for (int i = lineNumber - 1; i >= 0; --i)
            {
                string line = this.buffer_.CurrentSnapshot.GetLineFromLineNumber(i).GetText();
                (int beginPos, int length, bool isLabel) pos = AsmSourceTools.Get_First_Keyword(line);
                string keywordString = AsmSourceTools.Keyword(pos, line);

                if (pos.isLabel)
                {
                    if (!keywordString[0].Equals('.'))
                    {
                        //AsmDudeToolsStatic.Output_INFO("NasmTokenTagger:Get_Last_Non_Local_Label: found label \"" + keywordString + "\" at lineNumber " + i + "; beginPos=" + pos.Item1 + "; endPos=" + pos.Item2);
                        lastNonLocalLabel = keywordString;
                        return true;
                    }
                }
            }
            //AsmDudeToolsStatic.Output_INFO("NasmTokenTagger:get_Last_Non_Local_Label: could not find regular label before lineNumber " + lineNumber);
            return false;
        }
        #endregion
    }
}
