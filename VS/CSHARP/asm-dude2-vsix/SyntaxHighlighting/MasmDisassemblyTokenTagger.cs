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
    using System.Globalization;
    using AsmDude2.SyntaxHighlighting;
    using AsmDude2.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class MasmDisassemblyTokenTagger : ITagger<AsmTokenTag>
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

        internal MasmDisassemblyTokenTagger(ITextBuffer buffer)
        {
            AsmDudeToolsStatic.Output_INFO("MasmDisassemblyTokenTagger:constructor");

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

                // if the line does not contain a Mnemonic, assume it is a source code line and make it a remark
                #region Check source code line
                if (IsSourceCode(line_upcase, pos))
                {
                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span((0, line_upcase.Length, false), offset, curSpan), this.remark_);
                    continue; // go to the next line
                }
                #endregion

                for (int k = 0; k < nKeywords; k++)
                {
                    string asmToken = AsmSourceTools.Keyword(pos[k], line_upcase);

                    // keyword k is a label definition
                    if (pos[k].isLabel)
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.labelDef_);
                        continue;
                    }

                    AsmTokenType keywordType = this.asmDudeTools_.Get_Token_Type_Intel(asmToken);
                    switch (keywordType)
                    {
                        case AsmTokenType.Jump:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.jump_);

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
                                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.misc_);

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
                                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.misc_);
                                                        break;
                                                    }
                                                    // yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.label_);
                                                    // break;
                                            }

                                            break;
                                        }
                                    default:
                                        {
                                            if (RegisterTools.IsRegister(asmToken2))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.register_);
                                            }
                                            else
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.label_);
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            {
                                if (asmToken.Equals("OFFSET", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);
                                    k++; // goto the next word
                                    if (k == nKeywords)
                                    {
                                        break;
                                    }

                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.label_);
                                }
                                else if (IsConstant(asmToken))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                }
                                else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                }
                                else
                                {
                                    //yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._UNKNOWN);
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);
                                break;
                            }
                        case AsmTokenType.Mnemonic:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.mnemonic_);
                                break;
                            }
                        case AsmTokenType.Register:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.register_);
                                break;
                            }
                        default: break;
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "MasmDisassemblyTokenTagger");
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
            if (pos.Count < 2)
            {
                return true;
            }
            // just some rules of thumb
            if (line[0] == ' ')
            {
                return true;
            }
            if (line[0] == '-')
            {
                return true;
            }
            {
                string line2 = line.Trim();
                if (line2.Length < 2)
                {
                    return true;
                }
                if (line2[0] == '{')
                {
                    return true;
                }
                if (line2[0] == '}')
                {
                    return true;
                }
                if (line2[0] == '/')
                {
                    return true;
                }
                if (line2.Contains("__CDECL"))
                {
                    return true;
                }
                if (line2.Contains(";"))
                {
                    return true;
                }
            }
            if (pos[0].isLabel)
            {
                return false;
            }
            foreach ((int beginPos, int length, bool isLabel) v in pos)
            {
                string asmToken = AsmSourceTools.Keyword(v, line);
                if (AsmSourceTools.IsMnemonic(asmToken, true))
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
