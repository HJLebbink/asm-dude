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

    internal sealed class MasmTokenTagger : ITagger<AsmTokenTag>
    {
        private readonly ITextBuffer buffer_;
        private readonly AsmDude2Tools asmDudeTools_ = null;

        private readonly AsmTokenTag mnemonic_;
        private readonly AsmTokenTag mnemonicOff_;
        private readonly AsmTokenTag register_;
        private readonly AsmTokenTag remark_;
        private readonly AsmTokenTag directive_;
        private readonly AsmTokenTag constant_;
        private readonly AsmTokenTag jump_;
        private readonly AsmTokenTag label_;
        private readonly AsmTokenTag labelDef_;
        private readonly AsmTokenTag labelDef_PROTO_;
        private readonly AsmTokenTag misc_;
        private readonly AsmTokenTag userDefined1_;
        private readonly AsmTokenTag userDefined2_;
        private readonly AsmTokenTag userDefined3_;
        private readonly AsmTokenTag UNKNOWN_;

        internal MasmTokenTagger(ITextBuffer buffer)
        {
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.asmDudeTools_ = AsmDude2Tools.Instance;

            this.mnemonic_ = new AsmTokenTag(AsmTokenType.Mnemonic);
            this.mnemonicOff_ = new AsmTokenTag(AsmTokenType.MnemonicOff);
            this.register_ = new AsmTokenTag(AsmTokenType.Register);
            this.remark_ = new AsmTokenTag(AsmTokenType.Remark);
            this.directive_ = new AsmTokenTag(AsmTokenType.Directive);
            this.constant_ = new AsmTokenTag(AsmTokenType.Constant);
            this.jump_ = new AsmTokenTag(AsmTokenType.Jump);
            this.label_ = new AsmTokenTag(AsmTokenType.Label);
            this.labelDef_ = new AsmTokenTag(AsmTokenType.LabelDef);
            this.labelDef_PROTO_ = new AsmTokenTag(AsmTokenType.LabelDef, AsmTokenTag.MISC_KEYWORD_PROTO);
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

                string line_upcase = containingLine.GetText().ToUpper(CultureInfo.InvariantCulture);
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
                        SnapshotSpan labelDefSpan = NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan);
                        //AsmDudeToolsStatic.Output_INFO("MasmTokenTagger:GetTags: found label " + asmToken +" at line "+containingLine.LineNumber);
                        if (asmToken.Equals("@@", StringComparison.Ordinal))
                        {
                            // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                        }
                        else
                        {
                            AsmTokenTag v = this.Make_AsmTokenTag_LabelDef(containingLine.LineNumber);
                            yield return new TagSpan<AsmTokenTag>(labelDefSpan, v);
                        }
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
                                    break;
                                }

                                string asmToken2 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                switch (asmToken2)
                                {
                                    case "$":
                                    case "@B":
                                    case "@F":
                                        {
                                            // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                            break;
                                        }
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
                                                case "$":
                                                case "@B":
                                                case "@F":
                                                    {
                                                        // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                                        break;
                                                    }
                                                case "PTR":
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.misc_);
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                                        break;
                                                    }
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
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            {
                                if (AsmSourceTools.Evaluate_Constant(asmToken, true).valid)
                                //if (AsmTools.AsmSourceTools.Parse_Constant(asmToken, true).Valid)
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
                                }
                                else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.constant_);
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
                                            case "PROC":
                                            case "EQU":
                                            case "LABEL":
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k - 1], offset, curSpan), this.labelDef_);
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);
                                                    isUnknown = false;
                                                    break;
                                                }
                                            case "PROTO":
                                                { // a proto is considered a label definition but it should not clash with other label definitions
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k - 1], offset, curSpan), this.labelDef_PROTO_);
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
                                if (assember.HasFlag(AssemblerEnum.MASM)) // this MASM token-tagger only tags MASM directives
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.directive_);

                                    switch (asmToken)
                                    {
                                        case "INVOKE":
                                            {
                                                k++; // goto the next word
                                                if (k == nKeywords)
                                                {
                                                    break;
                                                }

                                                string asmToken2 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                                break;
                                            }
                                        case "EXTRN":
                                        case "EXTERN":
                                            {
                                                k++; // goto the next word
                                                if (k == nKeywords)
                                                {
                                                    break;
                                                }

                                                string asmToken2 = AsmSourceTools.Keyword(pos[k], line_upcase);
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.labelDef_PROTO_);
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                        case AsmTokenType.MnemonicOff:
                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this.mnemonic_);
                            break;
                        default:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), new AsmTokenTag(keywordType));
                                break;
                            }
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "MasmTokenTagger");
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags_NEW(NormalizedSnapshotSpanCollection spans)
        {
            DateTime time1 = DateTime.Now;

            if (spans.Count == 0)
            { //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string line_upcase = containingLine.GetText().ToUpper(CultureInfo.InvariantCulture);
                int offset = containingLine.Start.Position;
                IEnumerator<(int beginPos, int length, bool isLabel)> enumerator = AsmSourceTools.SplitIntoKeywordPos(line_upcase).GetEnumerator();

                bool needToAdvance = false;
                bool hasNext = enumerator.MoveNext();
                if (!hasNext)
                {
                    break;
                }

                (int beginPos, int length, bool isLabel) prev = (0, 0, false);
                (int beginPos, int length, bool isLabel) current = enumerator.Current;

                while (hasNext)
                {
                    string asmToken = AsmSourceTools.Keyword(current, line_upcase);
                    // keyword starts with a remark char
                    if (AsmSourceTools.IsRemarkChar(asmToken[0]))
                    {
                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.remark_);
                        continue;
                    }

                    // keyword k is a label definition
                    if (current.isLabel)
                    {
                        SnapshotSpan labelDefSpan = NasmIntelTokenTagger.New_Span(current, offset, curSpan);
                        //AsmDudeToolsStatic.Output_INFO("MasmTokenTagger:GetTags: found label " + asmToken +" at line "+containingLine.LineNumber);
                        if (asmToken.Equals("@@", StringComparison.Ordinal))
                        {
                            // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                        }
                        else
                        {
                            yield return new TagSpan<AsmTokenTag>(labelDefSpan, this.Make_AsmTokenTag_LabelDef(containingLine.LineNumber));
                        }
                        continue;
                    }

                    AsmTokenType keywordType = this.asmDudeTools_.Get_Token_Type_Intel(asmToken);
                    switch (keywordType)
                    {
                        case AsmTokenType.Jump:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.jump_);
                                { // go to the next word
                                    if (needToAdvance)
                                    {
                                        hasNext = enumerator.MoveNext();
                                        prev = current;
                                        current = enumerator.Current;
                                    }
                                    needToAdvance = true;
                                }
                                string asmToken2 = AsmSourceTools.Keyword(current, line_upcase);
                                switch (asmToken2)
                                {
                                    case "$":
                                    case "@B":
                                    case "@F":
                                        {
                                            // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                            break;
                                        }
                                    case "WORD":
                                    case "DWORD":
                                    case "QWORD":
                                    case "SHORT":
                                    case "NEAR":
                                        {
                                            yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.misc_);
                                            { // go to the next word
                                                if (needToAdvance)
                                                {
                                                    hasNext = enumerator.MoveNext();
                                                    prev = current;
                                                    current = enumerator.Current;
                                                }
                                                needToAdvance = true;
                                            }
                                            switch (AsmSourceTools.Keyword(current, line_upcase))
                                            {
                                                case "$":
                                                case "@B":
                                                case "@F":
                                                    {
                                                        // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                                        break;
                                                    }
                                                case "PTR":
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.misc_);
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                                        break;
                                                    }
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            if (RegisterTools.IsRegister(asmToken2))
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.register_);
                                            }
                                            else
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        case AsmTokenType.UNKNOWN: // asmToken is not a known keyword, check if it is numerical
                            {
                                //if (AsmTools.AsmSourceTools.Evaluate_Constant(asmToken, true).valid)
                                if (AsmSourceTools.Parse_Constant(asmToken, true).valid)
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.constant_);
                                }
                                else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.constant_);
                                }
                                else
                                {
                                    // do one word look back; see whether we can understand the current unknown word
                                    string previousKeyword = AsmSourceTools.Keyword(prev, line_upcase);
                                    switch (previousKeyword)
                                    {
                                        case "ALIAS":
                                            {
                                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.labelDef_);
                                                break;
                                            }
                                        default:
                                            {
                                                break;
                                            }
                                    }

                                    // do one word lookahead; see whether we can understand the current unknown word
                                    // go to the next word
                                    needToAdvance = false;

                                    if (enumerator.MoveNext())
                                    {
                                        prev = current;
                                        current = enumerator.Current;

                                        string nextKeyword = AsmSourceTools.Keyword(current, line_upcase);
                                        switch (nextKeyword)
                                        {
                                            case "PROC":
                                            case "EQU":
                                            case "LABEL":
                                                {
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(prev, offset, curSpan), this.labelDef_);
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.directive_);
                                                    break;
                                                }
                                            case "PROTO":
                                                { // a proto is considered a label definition but it should not clash with other label definitions
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(prev, offset, curSpan), this.labelDef_PROTO_);
                                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.directive_);
                                                    break;
                                                }
                                            default:
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.UNKNOWN_);
                                    }
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                AssemblerEnum assember = this.asmDudeTools_.Get_Assembler(asmToken);
                                if (assember.HasFlag(AssemblerEnum.MASM)) // this MASM token-tagger only tags MASM directives
                                {
                                    yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.directive_);

                                    if (asmToken.Equals("INVOKE", StringComparison.Ordinal))
                                    {
                                        { // go to the next word
                                            if (needToAdvance)
                                            {
                                                hasNext = enumerator.MoveNext();
                                                prev = current;
                                                current = enumerator.Current;
                                            }
                                            needToAdvance = true;
                                        }
                                        //string asmToken2 = NasmTokenTagger.Keyword(current, line);
                                        yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), this.Make_AsmTokenTag_Label(containingLine.LineNumber));
                                    }
                                }
                                break;
                            }
                        default:
                            {
                                yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(current, offset, curSpan), new AsmTokenTag(keywordType));
                                break;
                            }
                    }
                    { // go to the next word
                        if (needToAdvance)
                        {
                            hasNext = enumerator.MoveNext();
                            prev = current;
                            current = enumerator.Current;
                        }
                        needToAdvance = true;
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "MasmTokenTagger");
        }

        private AsmTokenTag Make_AsmTokenTag_LabelDef(int lineNumber)
        {
            string procedure_Name = this.Get_Procedure_Name(lineNumber);
            return (procedure_Name != null)
               ? new AsmTokenTag(AsmTokenType.LabelDef, procedure_Name)
               : this.labelDef_;
        }

        private AsmTokenTag Make_AsmTokenTag_Label(int lineNumber)
        {
            string procedure_Name = this.Get_Procedure_Name(lineNumber);
            return (procedure_Name != null)
               ? new AsmTokenTag(AsmTokenType.Label, procedure_Name)
               : this.label_;
        }

        private string Get_Procedure_Name(int lineNumber)
        {
            //AsmDudeToolsStatic.Output_INFO("MasmTokenTagger:Get_Procedure_Name: lineNumber=" + lineNumber);

            for (int i = lineNumber; i >= 0; --i)
            {
                string line = this.buffer_.CurrentSnapshot.GetLineFromLineNumber(i).GetText();
                IList<(int, int, bool)> positions = new List<(int, int, bool)>(AsmSourceTools.SplitIntoKeywordPos(line));
                if (positions.Count > 1)
                {
                    string keywordStr_upcase = AsmSourceTools.Keyword(positions[1], line).ToUpperInvariant();
                    switch (keywordStr_upcase)
                    {
                        case "PROC": return AsmSourceTools.Keyword(positions[0], line);
                        case "ENDP": return null;
                        default: break;
                    }
                }
            }
            //AsmDudeToolsStatic.Output_INFO("MasmTokenTagger:Get_Procedure_Name: could not find regular label before lineNumber " + lineNumber);
            return string.Empty;
        }
    }
}
