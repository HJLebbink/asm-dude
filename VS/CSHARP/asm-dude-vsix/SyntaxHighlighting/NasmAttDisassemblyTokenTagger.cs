// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmDude
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class NasmAttDisassemblyTokenTagger : ITagger<AsmTokenTag>
    {
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
        //private readonly AsmTokenTag _userDefined1;
        //private readonly AsmTokenTag _userDefined2;
        //private readonly AsmTokenTag _userDefined3;
        //private readonly AsmTokenTag _UNKNOWN;

        internal NasmAttDisassemblyTokenTagger(ITextBuffer buffer)
        {
            AsmDudeToolsStatic.Output_INFO("NasmAttDisassemblyTokenTagger:constructor");

            this._buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
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
            //this._userDefined1 = new AsmTokenTag(AsmTokenType.UserDefined1);
            //this._userDefined2 = new AsmTokenTag(AsmTokenType.UserDefined2);
            //this._userDefined3 = new AsmTokenTag(AsmTokenType.UserDefined3);
            //this._UNKNOWN = new AsmTokenTag(AsmTokenType.UNKNOWN);
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

                #region Check if the current line is a line of source code
                if (IsSourceCode(line_upcase, pos))
                {
                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span((0, line_upcase.Length, false), offset, curSpan), this._remark);
                    continue; // go to the next line
                }
                #endregion

                for (int k = 0; k < nKeywords; k++)
                {
                    string asmToken = AsmSourceTools.Keyword(pos[k], line_upcase);
                    // keyword starts with a remark char
                    if (AsmSourceTools.IsRemarkChar(asmToken[0]))
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._remark);
                        continue;
                    }

                    // keyword k is a label definition
                    if (pos[k].isLabel)
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._labelDef);
                        continue;
                    }

                    AsmTokenType keywordType = this._asmDudeTools.Get_Token_Type_Att(asmToken);
                    switch (keywordType)
                    {
                        case AsmTokenType.Jump:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._jump);

                                k++; // goto the next word
                                if (k == nKeywords)
                                {
                                    break; // there are no next words
                                }

                                string asmToken2 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                switch (asmToken2)
                                {
                                    case "WORD":
                                    case "DWORD":
                                    case "QWORD":
                                    case "SHORT":
                                    case "NEAR":
                                        {
                                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._misc);

                                            k++;
                                            if (k == nKeywords)
                                            {
                                                break;
                                            }

                                            string asmToken3 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                            switch (asmToken3)
                                            {
                                                case "PTR":
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(New_Span(pos[k], offset, curSpan), this._misc);
                                                        break;
                                                    }
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._label);
                                                    break;
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            if (RegisterTools.IsRegister(asmToken2))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._register);
                                            }
                                            else if (AsmSourceTools.Evaluate_Constant(asmToken2, true).valid)
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                            }
                                            else
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._label);
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
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                }
                                else if (asmToken.StartsWith("$", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                }
                                else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._constant);
                                }
                                else
                                {
                                    //yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._UNKNOWN);
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._directive);
                                break;
                            }
                        case AsmTokenType.Mnemonic:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._mnemonic);
                                break;
                            }
                        case AsmTokenType.Register:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._register);
                                break;
                            }
                        default: break;
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "NasmAttDisassemblyTokenTagger");
        }

        #region Private Member Methods

        private static bool IsConstant(string token)
        {
            if (long.TryParse(token, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out long dummy1))
            {
                return true;
            }
            //if (long.TryParse(token, NumberStyles.Integer, CultureInfo.CurrentCulture, out var dummy2))
            //{
            //    return true;
            //}
            if (token.EndsWith("H", StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }

        private static bool IsSourceCode(string line, List<(int beginPos, int length, bool isLabel)> pos)
        {
            //NOTE: line has only capitals
            if (pos.Count < 2)
            {
                return true;
            }

            // just some rules of thumb
            {
                if (line.Contains("(BAD)"))
                {
                    return false;
                }
                if (line.StartsWith("0X0", StringComparison.Ordinal))
                {
                    return false;
                }
                if (line[0] == ' ')
                {
                    return false;
                }
            }

            if (pos[0].isLabel)
            {
                return false;
            }

            foreach ((int beginPos, int length, bool isLabel) v in pos)
            {
                string word = AsmSourceTools.Keyword(v, line);
                if (AsmSourceTools.IsMnemonic_Att(word, true))
                {
                    return false; // found an assebly instruction, think this is assembly code
                }
            }
            return true;
        }

        #endregion

        #region Public Static Methods

        public static string Keyword((int beginPos, int length, bool isLabel) pos, string line)
        {
            return line.Substring(pos.beginPos, pos.length - pos.beginPos);
        }

        public static SnapshotSpan New_Span((int beginPos, int length, bool isLabel) pos, int offset, SnapshotSpan lineSnapShot)
        {
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.beginPos + offset, pos.length - pos.beginPos));
        }

        #endregion Public Static Methods
    }
}
