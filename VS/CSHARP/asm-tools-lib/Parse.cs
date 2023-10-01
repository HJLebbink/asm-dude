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

using System;
using System.Collections.Generic;
using System.Globalization;

namespace AsmTools
{
    public class Parse
    {
        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> ParseNasmIntel(string lineStr, AsmDude2Tools asmDudeTools)
        {
            string line_uppercase = lineStr.ToUpperInvariant();
            var pos = new List<(int beginPos, int length, AsmTokenType type)>(AsmSourceTools.SplitIntoKeywordsType(line_uppercase));
            int nKeywords = pos.Count;

            for (int k = 0; k < nKeywords; k++)
            {
                if (pos[k].type != AsmTokenType.UNKNOWN)
                {
                    yield return pos[k];
                    continue;
                }

                string keyword_uppercase = AsmSourceTools.Keyword(pos[k], line_uppercase);
                AsmTokenType keywordType = asmDudeTools.Get_Token_Type_Intel(keyword_uppercase);
                switch (keywordType)
                {
                    case AsmTokenType.Jump:
                        {
                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Jump);

                            k++; // goto the next word
                            if (k == nKeywords)
                            {
                                break; // there are no next words
                            }

                            string asmToken2 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                            switch (asmToken2)
                            {
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    {
                                        yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);

                                        k++;
                                        if (k == nKeywords)
                                        {
                                            break;
                                        }

                                        string asmToken3 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                        if (asmToken3.Equals("PTR", StringComparison.Ordinal))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        }
                                        break;
                                    }
                                default:
                                    {
                                        if (RegisterTools.IsRegister(asmToken2, true))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Register);
                                        }
                                        else if (AsmSourceTools.Evaluate_Constant(asmToken2, true).valid)
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        }

                                        break;
                                    }
                            }
                            break;
                        }
                    case AsmTokenType.UNKNOWN: // keyword_uppercase is not a known keyword, check if it is numerical
                        {
                            if (AsmSourceTools.Evaluate_Constant(keyword_uppercase, true).valid)
                            //if (AsmSourceTools.Parse_Constant(keyword_uppercase, true).Valid)
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (keyword_uppercase.StartsWith("\"", StringComparison.Ordinal) && keyword_uppercase.EndsWith("\"", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else
                            {
                                bool isUnknown = true;

                                // do one word lookahead; see whether we can understand the current unknown word
                                if ((k + 1) < nKeywords)
                                {
                                    k++;
                                    string nextKeyword = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                    switch (nextKeyword)
                                    {
                                        case "LABEL":
                                            {
                                                yield return (pos[k - 1].beginPos, pos[k - 1].length, AsmTokenType.LabelDef);
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
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
                                    string previousKeyword = AsmSourceTools.Keyword(pos[k - 1], line_uppercase);
                                    switch (previousKeyword)
                                    {
                                        case "ALIAS":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.LabelDef);
                                                isUnknown = false;
                                                break;
                                            }
                                        case "INCLUDE":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
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
                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.UNKNOWN);
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Directive:
                        {
                            AssemblerEnum assembler = asmDudeTools.Get_Assembler(keyword_uppercase);
                            if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
                            }
                            break;
                        }
                    default:
                        {
                            yield return (pos[k].beginPos, pos[k].length, keywordType);
                            break;
                        }
                }
            }
        }

        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> ParseNasmAtt(string lineStr, AsmDude2Tools asmDudeTools)
        {
            string line_uppercase = lineStr.ToUpperInvariant();
            var pos = new List<(int beginPos, int length, AsmTokenType type)>(AsmSourceTools.SplitIntoKeywordsType(line_uppercase));
            int nKeywords = pos.Count;

            for (int k = 0; k < nKeywords; k++)
            {
                if (pos[k].type != AsmTokenType.UNKNOWN)
                {
                    yield return pos[k];
                    continue;
                }

                string keyword_uppercase = AsmSourceTools.Keyword(pos[k], line_uppercase);
                AsmTokenType keywordType = asmDudeTools.Get_Token_Type_Att(keyword_uppercase);
                switch (keywordType)
                {
                    case AsmTokenType.Jump:
                        {
                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Jump);

                            k++; // goto the next word
                            if (k == nKeywords)
                            {
                                break; // there are no next words                                 
                            }

                            string asmToken2 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                            switch (asmToken2)
                            {
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    {
                                        yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);

                                        k++;
                                        if (k == nKeywords)
                                        {
                                            break;
                                        }

                                        string asmToken3 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                        if (asmToken3.Equals("PTR", StringComparison.Ordinal))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        if (RegisterTools.IsRegister(asmToken2, true))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Register);
                                        }
                                        else if (AsmSourceTools.Evaluate_Constant(asmToken2, true).valid)
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case AsmTokenType.UNKNOWN: // keyword_uppercase is not a known keyword, check if it is numerical
                        {
                            if (AsmSourceTools.Evaluate_Constant(keyword_uppercase, true).valid)
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (keyword_uppercase.StartsWith("\"", StringComparison.Ordinal) && keyword_uppercase.EndsWith("\"", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (keyword_uppercase.StartsWith("$", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos + 1, pos[k].length - 1, AsmTokenType.Constant);
                            }
                            else
                            {
                                bool isUnknown = true;

                                // do one word lookahead; see whether we can understand the current unknown word
                                if ((k + 1) < nKeywords)
                                {
                                    k++;
                                    string nextKeyword = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                    switch (nextKeyword)
                                    {
                                        case "LABEL":
                                            {
                                                yield return (pos[k - 1].beginPos, pos[k - 1].length, AsmTokenType.LabelDef);
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
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
                                    string previousKeyword = AsmSourceTools.Keyword(pos[k - 1], line_uppercase);
                                    switch (previousKeyword)
                                    {
                                        case "ALIAS":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.LabelDef);
                                                isUnknown = false;
                                                break;
                                            }
                                        case "INCLUDE":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
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
                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.UNKNOWN);
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Directive:
                        {
                            AssemblerEnum assembler = asmDudeTools.Get_Assembler(keyword_uppercase);
                            if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
                            }
                            break;
                        }
                    default:
                        {
                            yield return (pos[k].beginPos, pos[k].length, keywordType);
                            break;
                        }
                }
            }

        }

        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> ParseMasm(string lineStr, AsmDude2Tools asmDudeTools)
        {
            string line_uppercase = lineStr.ToUpperInvariant();
            var pos = new List<(int beginPos, int length, AsmTokenType type)>(AsmSourceTools.SplitIntoKeywordsType(line_uppercase));
            int nKeywords = pos.Count;

            for (int k = 0; k < nKeywords; k++)
            {
                if (pos[k].type != AsmTokenType.UNKNOWN)
                {
                    yield return pos[k];
                    continue;
                }

                string keyword_uppercase = AsmSourceTools.Keyword(pos[k], line_uppercase);
                AsmTokenType keywordType = asmDudeTools.Get_Token_Type_Att(keyword_uppercase);
                switch (keywordType)
                {
                    case AsmTokenType.Jump:
                        {
                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Jump);

                            k++; // goto the next word
                            if (k == nKeywords)
                            {
                                break;
                            }

                            string asmToken2 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                            switch (asmToken2)
                            {
                                case "$":
                                case "@B":
                                case "@F":
                                    {
                                        // yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                        break;
                                    }
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    {
                                        yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);

                                        k++;
                                        if (k == nKeywords)
                                        {
                                            break;
                                        }

                                        string asmToken3 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                        switch (asmToken3)
                                        {
                                            case "$":
                                            case "@B":
                                            case "@F":
                                                {
                                                    // yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                                    // TODO: special MASM label, for the moment, ignore it, later: check whether it is used etc.
                                                    break;
                                                }
                                            case "PTR":
                                                {
                                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);
                                                    break;
                                                }
                                            default:
                                                {
                                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                default:
                                    {
                                        if (RegisterTools.IsRegister(asmToken2))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Register);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case AsmTokenType.UNKNOWN: // keyword_uppercase is not a known keyword, check if it is numerical
                        {
                            if (AsmSourceTools.Evaluate_Constant(keyword_uppercase, true).valid)
                            //if (AsmTools.AsmSourceTools.Parse_Constant(keyword_uppercase, true).Valid)
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (keyword_uppercase.StartsWith("\"", StringComparison.Ordinal) && keyword_uppercase.EndsWith("\"", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else
                            {
                                bool isUnknown = true;

                                // do one word lookahead; see whether we can understand the current unknown word
                                if ((k + 1) < nKeywords)
                                {
                                    k++;
                                    string nextKeyword = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                    switch (nextKeyword)
                                    {
                                        case "PROC":
                                        case "EQU":
                                        case "LABEL":
                                            {
                                                yield return (pos[k - 1].beginPos, pos[k - 1].length, AsmTokenType.LabelDef);
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
                                                isUnknown = false;
                                                break;
                                            }
                                        case "PROTO":
                                            { // a proto is considered a label definition but it should not clash with other label definitions
                                                yield return (pos[k - 1].beginPos, pos[k - 1].length, AsmTokenType.LabelDef);
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
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
                                    string previousKeyword = AsmSourceTools.Keyword(pos[k - 1], line_uppercase);
                                    switch (previousKeyword)
                                    {
                                        case "ALIAS":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.LabelDef);
                                                isUnknown = false;
                                                break;
                                            }
                                        case "INCLUDE":
                                            {
                                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
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
                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.UNKNOWN);
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Directive:
                        {
                            AssemblerEnum assember = asmDudeTools.Get_Assembler(keyword_uppercase);
                            if (assember.HasFlag(AssemblerEnum.MASM)) // this MASM token-tagger only tags MASM directives
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);

                                switch (keyword_uppercase)
                                {
                                    case "INVOKE":
                                        {
                                            k++; // goto the next word
                                            if (k == nKeywords)
                                            {
                                                break;
                                            }
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.LabelDef);
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

                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.LabelDef);
                                            break;
                                        }
                                }
                            }
                            break;
                        }
                    default:
                        {
                            yield return (pos[k].beginPos, pos[k].length, keywordType);
                            break;
                        }
                }
            }
        }

        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> ParseDisassembly(string lineStr, AsmDude2Tools asmDudeTools)
        {
            string line_uppercase = lineStr.ToUpperInvariant();
            var pos = new List<(int beginPos, int length, AsmTokenType type)>(AsmSourceTools.SplitIntoKeywordsType(line_uppercase));

            // if the line does not contain a Mnemonic, assume it is a source code line and make it a remark
            if (IsSourceCode(line_uppercase, pos))
            {
                yield return (0, line_uppercase.Length, AsmTokenType.Remark);
            }

            int nKeywords = pos.Count;
            for (int k = 0; k < nKeywords; k++)
            {
                if (pos[k].type != AsmTokenType.UNKNOWN)
                {
                    yield return pos[k];
                    continue;
                }

                string asmToken = AsmSourceTools.Keyword(pos[k], line_uppercase);
                AsmTokenType keywordType = asmDudeTools.Get_Token_Type_Intel(asmToken);
                switch (keywordType)
                {
                    case AsmTokenType.Jump:
                        {
                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Jump);

                            k++; // goto the next word
                            if (k == nKeywords)
                            {
                                break; // there are no next words
                            }

                            string asmToken2 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                            switch (asmToken2)
                            {
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    {
                                        yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);

                                        k++;
                                        if (k == nKeywords)
                                        {
                                            break;
                                        }

                                        string asmToken3 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                        switch (asmToken3)
                                        {
                                            case "PTR":
                                                {
                                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);
                                                    break;
                                                }
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        if (RegisterTools.IsRegister(asmToken2))
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Register);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
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
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Directive);
                                k++; // goto the next word
                                if (k == nKeywords)
                                {
                                    break;
                                }

                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
                            }
                            else if (IsConstant(asmToken))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else
                            {
                                //yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._UNKNOWN);
                            }
                            break;
                        }
                    default:
                        {
                            yield return (pos[k].beginPos, pos[k].length, keywordType);
                            break;
                        }

                }
            }
        }

        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> ParseAttDisassembly(string lineStr, AsmDude2Tools asmDudeTools)
        {
            string line_uppercase = lineStr.ToUpperInvariant();
            var pos = new List<(int beginPos, int length, AsmTokenType type)>(AsmSourceTools.SplitIntoKeywordsType(line_uppercase));

            // if the line does not contain a Mnemonic, assume it is a source code line and make it a remark
            if (IsSourceCode(line_uppercase, pos))
            {
                yield return (0, line_uppercase.Length, AsmTokenType.Remark);
            }

            int nKeywords = pos.Count;
            for (int k = 0; k < nKeywords; k++)
            {
                if (pos[k].type != AsmTokenType.UNKNOWN)
                {
                    yield return pos[k];
                    continue;
                }

                string asmToken = AsmSourceTools.Keyword(pos[k], line_uppercase);
                AsmTokenType keywordType = asmDudeTools.Get_Token_Type_Att(asmToken);
                switch (keywordType)
                {
                    case AsmTokenType.Jump:
                        {
                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Jump);

                            k++; // goto the next word
                            if (k == nKeywords)
                            {
                                break; // there are no next words
                            }

                            string asmToken2 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                            switch (asmToken2)
                            {
                                case "WORD":
                                case "DWORD":
                                case "QWORD":
                                case "SHORT":
                                case "NEAR":
                                    {
                                        yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);

                                        k++;
                                        if (k == nKeywords)
                                        {
                                            break;
                                        }

                                        string asmToken3 = AsmSourceTools.Keyword(pos[k], line_uppercase);
                                        switch (asmToken3)
                                        {
                                            case "PTR":
                                                {
                                                    yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Misc);
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
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Register);
                                        }
                                        else if (AsmSourceTools.Evaluate_Constant(asmToken2, true).valid)
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                                        }
                                        else
                                        {
                                            yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Label);
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
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (asmToken.StartsWith("$", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else if (asmToken.StartsWith("\"", StringComparison.Ordinal) && asmToken.EndsWith("\"", StringComparison.Ordinal))
                            {
                                yield return (pos[k].beginPos, pos[k].length, AsmTokenType.Constant);
                            }
                            else
                            {
                                //yield return new TagSpan<AsmTokenTag>(NasmIntelTokenTagger.New_Span(pos[k], offset, curSpan), this._UNKNOWN);
                            }
                            break;
                        }
                    default:
                        {
                            yield return (pos[k].beginPos, pos[k].length, keywordType);
                            break;
                        }
                }
            }
        }

        private static bool IsSourceCode(string line, List<(int beginPos, int length, AsmTokenType type)> pos)
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
            if (pos[0].type == AsmTokenType.LabelDef)
            {
                return false;
            }
            foreach ((int beginPos, int length, AsmTokenType t) v in pos)
            {
                string asmToken = AsmSourceTools.Keyword(v, line);
                if (AsmSourceTools.IsMnemonic(asmToken, true))
                {
                    return false; // found an assembly instruction, think this is assembly code
                }
            }
            return true;
        }
      
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
    }
}


