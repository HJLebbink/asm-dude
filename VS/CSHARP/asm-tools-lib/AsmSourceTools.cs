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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmTools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    public static partial class AsmSourceTools
    {
        private static readonly CultureInfo Culture = CultureInfo.CurrentCulture;


        /// <summary>Guess whether the provided buffer has assembly in Intel syntax (return true) or AT&T syntax (return false)</summary>
        public static bool Guess_Intel_Syntax(string[] lines)
        {
            bool contains_register_att(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (asmToken[0].Equals('%'))
                    {
                        string asmToken2 = asmToken.Substring(1);
                        if (RegisterTools.IsRn(asmToken2, true))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            bool contains_register_intel(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (RegisterTools.IsRn(asmToken, true))
                    {
                        return true;
                    }
                }
                return false;
            }
            bool contains_constant_att(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (asmToken[0].Equals('$'))
                    {
                        return true;
                    }
                }
                return false;
            }
            bool contains_constant_intel(List<string> line)
            {
                return false;
            }
            bool contains_mnemonic_att(List<string> line)
            {
                foreach (string word in line)
                {
                    if (!AsmSourceTools.IsMnemonic(word, true))
                    {
                        if (AsmSourceTools.IsMnemonic_Att(word, true))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            bool contains_mnemonic_intel(List<string> line)
            {
                return false;
            }

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Intel_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
            int registers_i = 0;
            int constants_i = 0;
            int mnemonics_i = 0;

            for (int i = 0; i < lines.Length; ++i)
            {
                string line_uppercase = lines[i].ToUpper();
                //Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_uppercase));

                List<string> keywords_uppercase = SplitIntoKeywordsList(line_uppercase);

                if (contains_register_att(keywords_uppercase))
                {
                    registers_i++;
                }

                if (contains_register_intel(keywords_uppercase))
                {
                    registers_i--;
                }

                if (contains_constant_att(keywords_uppercase))
                {
                    constants_i++;
                }

                if (contains_constant_intel(keywords_uppercase))
                {
                    constants_i--;
                }

                if (contains_mnemonic_att(keywords_uppercase))
                {
                    mnemonics_i++;
                }

                if (contains_mnemonic_intel(keywords_uppercase))
                {
                    mnemonics_i--;
                }
            }
            int total =
                Math.Max(Math.Min(1, registers_i), -1) +
                Math.Max(Math.Min(1, constants_i), -1) +
                Math.Max(Math.Min(1, mnemonics_i), -1);

            bool result = (total <= 0);
            //Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax; result {1}; file=\"{2}\"; registers {3}; constants {4}; mnemonics {5}", "AsmDudeToolsStatic", result, GetFilename(buffer), registers_i, constants_i, mnemonics_i));
            return result;
        }

        /// <summary>Guess whether the provided buffer has assembly in Masm syntax (return true) or Gas syntax (return false)</summary>
        public static bool Guess_Masm_Syntax(string[] lines)
        {
            Contract.Requires(lines != null);

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
            int evidence_masm = 0;

            for (int i = 0; i < lines.Length; ++i)
            {
                string line_uppercase = lines[i].ToUpper();
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_capitals));

                List<string> keywords_uppercase = AsmSourceTools.SplitIntoKeywordsList(line_uppercase);

                foreach (string keyword_uppercase in keywords_uppercase)
                {
                    switch (keyword_uppercase)
                    {
                        case "PTR":
                        case "@B":
                        case "@F":
                            evidence_masm++;
                            break;
                        case ".INTEL_SYNTAX":
                        case ".ATT_SYNTAX":
                            return false; // we know for sure
                    }
                }
            }
            bool result = (evidence_masm > 0);
            //Output_INFO(string.Format(Culture, "{0}:Guess_Masm_Syntax; result {1}; evidence_masm {3}", "AsmDudeToolsStatic", result, evidence_masm));
            return result;
        }

        /// <summary>
        /// Parse the provided lineStr. Returns label, mnemonic, args, remarks. Args are in capitals
        /// </summary>
        public static (KeywordID[] keywords, string label, Mnemonic mnemonic, string[] args, string remark) ParseLine(string lineStr, int lineNumber, int fileID)
        {
            Contract.Requires(lineStr != null);
            Contract.Assume(lineStr != null);

            // Console.WriteLine("INFO: AsmSourceTools:ParseLine: lineStr=" + lineStr + "; length=" + lineStr.Length);

            var k = new List<KeywordID>();
            string label = string.Empty;
            Mnemonic mnemonic = Mnemonic.NONE;
            string[] args = Array.Empty<string>();
            string remark = string.Empty;

            if (lineStr.Length > 0)
            {
                (bool valid, int startPos, int endPos) = GetLabelDefPos(lineStr);
                int codeBeginPos = 0;
                if (valid)
                {
                    label = lineStr.Substring(startPos, endPos - startPos);
                    codeBeginPos = endPos + 1; // plus one to get rid of the colon
                    if (lineStr.Length > codeBeginPos)
                    {
                        if (lineStr[codeBeginPos] == ':')
                        {
                            codeBeginPos++; // remove a second colon
                        }
                    }

                    k.Add(new KeywordID(lineNumber, fileID, startPos, endPos, AsmTokenType.LabelDef));
                    // Console.WriteLine("found label " + label);
                }

                (bool valid, int startPos, int endPos) remarkPos = GetRemarkPos(lineStr);
                KeywordID remarkKeyword = new KeywordID();
                int codeEndPos = lineStr.Length;
                if (remarkPos.valid)
                {
                    remark = lineStr.Substring(remarkPos.startPos, remarkPos.endPos - remarkPos.startPos);
                    codeEndPos = remarkPos.startPos;
                    remarkKeyword = new KeywordID(lineNumber, fileID, startPos, endPos, AsmTokenType.Remark);
                    // Console.WriteLine("found remark " + remark);
                }

                string codeStr = lineStr.Substring(codeBeginPos, codeEndPos - codeBeginPos).Trim();
                // Console.WriteLine("code string \"" + codeStr + "\".");
                if (codeStr.Length > 0)
                {
                    string codeStr_uppercase = codeStr.ToUpperInvariant();
                    // Console.WriteLine(codeStr + ":" + codeStr.Length);

                    // get the first keyword, check if it is a mnemonic
                    (int startPos, int endPos) keyword1Pos = GetKeywordPos(0, codeStr_uppercase); // find a keyword starting a position 0
                    string keyword1 = codeStr_uppercase.Substring(keyword1Pos.startPos, keyword1Pos.endPos - keyword1Pos.startPos);
                    if (keyword1.Length > 0)
                    {
                        int startArgPos = keyword1Pos.endPos;
                        mnemonic = ParseMnemonic(keyword1, true);
                        switch (mnemonic)
                        {
                            case Mnemonic.NONE: break;
                            case Mnemonic.REP:
                            case Mnemonic.REPE:
                            case Mnemonic.REPZ:
                            case Mnemonic.REPNE:
                            case Mnemonic.REPNZ:
                                {
                                    // find a second keyword starting a position keywordPos.EndPos
                                    (int startPos, int endPos) keyword2Pos = GetKeywordPos(keyword1Pos.endPos + 1, codeStr_uppercase); // find a keyword starting a position 0
                                    string keyword2 = codeStr_uppercase.Substring(keyword2Pos.startPos, keyword2Pos.endPos - keyword2Pos.startPos);
                                    if (keyword2.Length > 0)
                                    {
                                        Mnemonic mnemonic2 = ParseMnemonic(keyword2, true);
                                        if (mnemonic2 != Mnemonic.NONE)
                                        {
                                            startArgPos = keyword2Pos.endPos;
                                            mnemonic = ParseMnemonic(mnemonic.ToString() + "_" + mnemonic2.ToString(), true);
                                        }
                                    }
                                    break;
                                }
                            default: break;
                        }
                        // TODO the start and end positions for a special mnemonic are incorrect
                        k.Add(new KeywordID(lineNumber, fileID, keyword1Pos.startPos, keyword1Pos.endPos, AsmTokenType.Mnemonic));

                        // find arguments after the last mnemonic

                        int argLength = codeStr.Length - startArgPos;
                        if (argLength > 0)
                        {
                            args = codeStr.Substring(startArgPos, argLength).Split(',');
                            for (int i = 0; i < args.Length; ++i)
                            {
                                args[i] = args[i].Trim();
                                //k.Add() //TODO add keywords
                            }
                        }
                    }
                }
            
                if (remarkPos.valid)
                {
                    k.Add(remarkKeyword);
                }

            }
            // Console.WriteLine(args[1] + ":" + args[1].Length);


            Contract.Ensures(label != null);
            Contract.Ensures(args != null);
            Contract.Ensures(remark != null);
            Contract.Assume(label != null);
            Contract.Assume(args != null);
            Contract.Assume(remark != null);

            return (k.ToArray<KeywordID>(), label, mnemonic, args, remark);
        }

        public static List<Operand> MakeOperands(string[] operandStrArray) // TODO consider Array
        {
            Contract.Requires(operandStrArray != null);
            Contract.Assume(operandStrArray != null);

            int nOperands = operandStrArray.Length;
            if (nOperands <= 1)
            {
                return new List<Operand>(0);
            }
            else
            {
                var operands = new List<Operand>(nOperands);
                foreach (string opStr in operandStrArray)
                {
                    operands.Add(new Operand(opStr, false));
                }
                return operands;
            }
        }

        /// <summary>
        /// return label definition position
        /// </summary>
        public static (int beginPos, int length, AsmTokenType type) Get_First_Keyword(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            bool started = false;
            int keywordBegin = 0;

            for (int i = 0; i < line.Length; ++i)
            {
                char c = line[i];
                if (IsRemarkChar(c))
                {
                    return (beginPos: 0, length: 0, AsmTokenType.Remark);
                }

                if (c.Equals('"'))
                {
                    return (beginPos: 0, length: 0, AsmTokenType.Constant);
                }

                if (c.Equals(':'))
                {
                    if (started)
                    {
                        return (beginPos: keywordBegin, length: i, AsmTokenType.LabelDef);
                    }
                    else
                    {
                        return (beginPos: 0, length: 0, AsmTokenType.UNKNOWN);
                    }
                }
                else if (IsSeparatorChar(c))
                {
                    if (started)
                    {
                        return (beginPos: keywordBegin, length: i, AsmTokenType.UNKNOWN);
                    }
                    else
                    {
                        keywordBegin = i + 1;
                    }
                }
                else
                {
                    started = true;
                }
            }
            return (beginPos: 0, length: 0, AsmTokenType.UNKNOWN);
        }

        /// <summary>
        /// split the provided lineStr into keywords, and if the type is already known return the type.
        /// </summary>
        public static IEnumerable<(int beginPos, int length, AsmTokenType type)> SplitIntoKeywordsType(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            int keywordBegin = 0;
            bool inStringDef = false;
            bool isFirstKeyword = true;

            for (int i = 0; i < line.Length; ++i)
            {
                char c = line[i];

                if (inStringDef)
                {
                    if (c.Equals('"'))
                    {
                        inStringDef = false;
                        if (keywordBegin < i)
                        {
                            yield return (keywordBegin, i + 1, AsmTokenType.Constant);
                            isFirstKeyword = false;
                        }
                        keywordBegin = i + 1; // next keyword starts at the next char
                    }
                }
                else
                {
                    if (IsRemarkChar(c))
                    {
                        if (keywordBegin < i)
                        {
                            yield return (keywordBegin, i, AsmTokenType.UNKNOWN);
                            isFirstKeyword = false;
                        }
                        yield return (i, line.Length, AsmTokenType.Remark);
                        i = line.Length;
                    }
                    else if (c.Equals('"'))
                    { // start string definition
                        if (keywordBegin < i)
                        {
                            yield return (keywordBegin, i, AsmTokenType.UNKNOWN);
                            isFirstKeyword = false;
                        }
                        inStringDef = true;
                        keywordBegin = i; // '"' is part of the keyword
                    }
                    // else if (IsSeparatorChar_NoOperator(c))
                    else if (IsSeparatorChar(c))
                    {
                        if (keywordBegin < i)
                        {
                            if (c.Equals(':'))
                            {
                                if (isFirstKeyword)
                                {
                                    yield return (beginPos: keywordBegin, length: i, AsmTokenType.LabelDef);
                                }
                                else
                                {
                                    yield return (beginPos: keywordBegin, length: i, AsmTokenType.UNKNOWN);
                                }
                            }
                            else
                            {
                                yield return (beginPos: keywordBegin, length: i, AsmTokenType.UNKNOWN);
                            }
                            isFirstKeyword = false;
                        }
                        keywordBegin = i + 1; // separator is not part of the keyword
                    }
                }
            }

            if (keywordBegin < line.Length)
            {
                yield return (beginPos: keywordBegin, length: line.Length, AsmTokenType.UNKNOWN);
            }
        }

        public static List<string> SplitIntoKeywordsList(string line)
        {
            List<string> keywords = new List<string>();
            foreach ((int beginPos, int length, AsmTokenType _) pos in SplitIntoKeywordsType(line))
            {
                keywords.Add(Keyword(pos, line));
            }
            return keywords;
        }

        public static string Keyword((int beginPos, int length, AsmTokenType _) pos, string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);
            return line.Substring(pos.beginPos, pos.length - pos.beginPos);
        }

        public static bool IsSeparatorChar_NoOperator(char c)
        {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals('{') || c.Equals('}') || c.Equals(':');
        }

        public static bool IsSeparatorChar(char c)
        {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals('+') || c.Equals('-') || c.Equals('*') || c.Equals('{') || c.Equals('}') || c.Equals(':');
        }

        #region Remark Methods
        public static bool IsRemarkChar(char c)
        {
            return c.Equals('#') || c.Equals(';');
        }

        /// <summary>
        /// Determine whether the provided pos is in a remark in the provided lineStr.
        /// </summary>
        public static bool IsInRemark(int pos, string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            // check if the lineStr contains a remark character before the current point
            int nChars = line.Length;
            int startPos = (pos >= nChars) ? nChars - 1 : pos;
            for (int i = startPos; i >= 0; --i)
            {
                if (IsRemarkChar(line[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the provided lineStr only contains a remark (and no labels or code, it may have white space)
        /// </summary>
        public static bool IsRemarkOnly(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            int nChars = line.Length;
            for (int i = 0; i < nChars; ++i)
            {
                char c = line[i];
                if (IsRemarkChar(c))
                {
                    return true;
                }
                else
                {
                    if (char.IsWhiteSpace(c))
                    {
                        // OK
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            // did not find a remark character; this lineStr is not a remark.
            return false;
        }

        public static int GetRemarkCharPosition(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            for (int i = 0; i < line.Length; ++i)
            {
                if (IsRemarkChar(line[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion Remark Methods

        public static int NBitsStorageNeeded(ulong v, bool isNegative)
        {
            if (isNegative)
            {
                if ((v | 0x0000_0000_0000_007Ful) == 0xFFFF_FFFF_FFFF_FFFFul)
                {
                    return 8;
                }

                if ((v | 0x0000_0000_0000_7FFFul) == 0xFFFF_FFFF_FFFF_FFFFul)
                {
                    return 16;
                }

                if ((v | 0x0000_0000_7FFF_FFFFul) == 0xFFFF_FFFF_FFFF_FFFFul)
                {
                    return 32;
                }

                return 64;
            }
            else
            {
                if ((v & 0xFFFF_FFFF_FFFF_FF00ul) == 0)
                {
                    return 8;
                }

                if ((v & 0xFFFF_FFFF_FFFF_0000ul) == 0)
                {
                    return 16;
                }

                if ((v & 0xFFFF_FFFF_0000_0000ul) == 0)
                {
                    return 32;
                }

                return 64;
            }
        }

        /// <summary> Check if the provided string is a constant by evaluating it.</summary>
        public static (bool valid, ulong value, int nBits) Evaluate_Constant(string token, bool isCapitals = false)
        {
            Contract.Requires(token != null);
            Contract.Assume(token != null);

            if (token.StartsWith("$", StringComparison.Ordinal)) // AT&T syntax constants start with '$'
            {
                token = token.Substring(1);
            }

            // TODO 01-06-19 fix evaluate_constant
            // TODO bugfix: there is a issue with .net 1.6.2 and the evaluation code in Evaluate_Constant
            if (true)
            {
                // don't use expression evaluation, just parse it.
                return ExpressionEvaluator.Parse_Constant(token, isCapitals);
            }
            else
            {
                // string token2 = token.Replace("_", string.Empty).Replace(".", string.Empty);
                // return ExpressionEvaluator.Evaluate_Constant(token2, isCapitals);
            }
        }

        /// <summary> Check if the provided string is a constant by parsing it. Does not evaluate arithmetic in the string.</summary>
        public static (bool valid, ulong value, int nBits) Parse_Constant(string token, bool isCapitals = false)
        {
            return ExpressionEvaluator.Parse_Constant(token, isCapitals);
        }

        public static string Get_Related_Constant(string original, ulong value, int nBits)
        {
            string s0 = original;
            string s1 = value.ToString(Culture);
            string h1 = string.Format(Culture, "{0:X}", value); // just the hex number

            // string h3 = Convert.ToString(value, 8); // no octal

            return "\\b(" + s0 + "|" + s1 + "|0x[0]{0,}" + h1 + "|[0]{0,}" + h1 + "[h]{0,1})\\b";
        }

        /// <summary>
        /// return Offset = Base + (Index * Scale) + Displacement
        /// </summary>
        public static (bool valid, Rn baseReg, Rn indexReg, int scale, long displacement, int nBits, string errorMessage)
            Parse_Mem_Operand(string token, bool isCapitals = false)
        {
            Contract.Requires(token != null);
            Contract.Assume(token != null);

            int length = token.Length;
            if (length < 3)
            {
                return (valid: false, baseReg: Rn.NOREG, indexReg: Rn.NOREG, scale: 0, displacement: 0, nBits: 0, errorMessage: string.Empty); // do not return a error message because the provided token can be a label
            }

            if (!isCapitals)
            {
                token = token.ToUpper(CultureInfo.InvariantCulture);
                Contract.Assume(token != null);
            }

            // 1] select everything between []
            int beginPos = length;

            for (int i = 0; i < length; ++i)
            {
                if (token[i] == '[')
                {
                    beginPos = i + 1;
                }
            }

            int endPos = length;
            for (int i = beginPos; i < length; ++i)
            {
                if (token[i] == ']')
                {
                    endPos = i;
                }
            }

            int nBits = Get_Nbits_Mem_Operand(token);

            token = token.Substring(beginPos, endPos - beginPos).Trim();
            length = token.Length;
            if (length == 0)
            {
                return (valid: false, baseReg: Rn.NOREG, indexReg: Rn.NOREG, scale: 0, displacement: 0, nBits: 0, errorMessage: string.Empty); // do not return a error message because the provided token can be a label
            }

            // 2] check if the displacement is negative
            bool negativeDisplacement = token.Contains('-');
            if (negativeDisplacement)
            {
                token = token.Replace('-', '+');
            }

            // 3] remove superfluous initial +
            if (token[0] == '+')
            {
                token = token.Substring(1, length - 1).Trim();
            }

            // 4] split based on +
            string[] x = token.Split('+');

            Rn baseRn = Rn.NOREG;
            Rn indexRn = Rn.NOREG;
            int scale = 0;
            long displacement = 0;

            bool foundDisplacement = false;

            for (int i = 0; i < x.Length; ++i)
            {
                string y = x[i].Trim();

                (bool valid, ulong value, int nBits_NOTUSED) = ExpressionEvaluator.Parse_Constant(y, true);
                if (valid)
                {
                    if (foundDisplacement)
                    {
                        // found an second displacement, error
                        return (valid: false, baseReg: Rn.NOREG, indexReg: Rn.NOREG, scale: 0, displacement: 0, nBits: 0, errorMessage: "Multiple displacements");
                    }
                    else
                    {
                        foundDisplacement = true;
                        displacement = negativeDisplacement ? -(long)value : (long)value;
                    }
                }
                else
                {
                    Rn t1 = RegisterTools.ParseRn(y, true);
                    if (t1 != Rn.NOREG)
                    {
                        if (baseRn == Rn.NOREG)
                        {
                            baseRn = t1;
                        }
                        else
                        {
                            indexRn = t1;
                            scale = 1;
                        }
                    }

                    if (y.Contains('*'))
                    {
                        string[] z = y.Split('*');
                        string z0 = z[0].Trim();
                        string z1 = z[1].Trim();
                        string scaleRaw = string.Empty;
                        Rn z0r = RegisterTools.ParseRn(z0, true);
                        if (z0r != Rn.NOREG)
                        {
                            indexRn = z0r;
                            scaleRaw = z1;
                            scale = ParseScale(z1);
                        }
                        else
                        {
                            Rn z1r = RegisterTools.ParseRn(z1, true);
                            if (z1r != Rn.NOREG)
                            {
                                indexRn = z1r;
                                scaleRaw = z0;
                                scale = ParseScale(z0);
                            }
                        }
                        if (scale == -1)
                        {
                            return (valid: false, baseReg: Rn.NOREG, indexReg: Rn.NOREG, scale: 0, displacement: 0, nBits: 0, errorMessage: "Invalid scale " + scaleRaw);
                        }
                    }
                }
            }

            if ((baseRn != Rn.NOREG) && (indexRn != Rn.NOREG))
            {
                if (RegisterTools.NBits(baseRn) != RegisterTools.NBits(indexRn))
                {
                    return (valid: false, baseReg: Rn.NOREG, indexReg: Rn.NOREG, scale: 0, displacement: 0, nBits: 0, errorMessage: "Number of bits of base register " + baseRn + " is not equal to number of bits of index register " + indexRn);
                }
            }
            return (valid: true, baseReg: baseRn, indexReg: indexRn, scale, displacement, nBits, errorMessage: string.Empty);

            #region Local Methods
            int ParseScale(string str)
            {
                Contract.Requires(str != null);

                switch (str)
                {
                    case "1": return 1;
                    case "2": return 2;
                    case "4": return 4;
                    case "8": return 8;
                    default: return -1;
                }
            }

            /// <summary> Return the number of bits of the provided operand (assumes 64-bits) </summary>
            int Get_Nbits_Mem_Operand(string token2)
            {
                Contract.Requires(token2 != null);
                Contract.Assume(token2 != null);

                string s = token2.TrimStart();
                if (s.StartsWith("PTR", StringComparison.Ordinal))
                {
                    s = s.Substring(3, token.Length - 3).TrimStart();
                }

                if (s.StartsWith("BYTE", StringComparison.Ordinal))
                {
                    return 8; // nasm
                }

                if (s.StartsWith("SBYTE", StringComparison.Ordinal))
                {
                    return 8;
                }

                if (s.StartsWith("WORD", StringComparison.Ordinal))
                {
                    return 16; // nasm
                }

                if (s.StartsWith("SWORD", StringComparison.Ordinal))
                {
                    return 16;
                }

                if (s.StartsWith("DWORD", StringComparison.Ordinal))
                {
                    return 32; // nasm
                }

                if (s.StartsWith("SDWORD", StringComparison.Ordinal))
                {
                    return 32;
                }

                if (s.StartsWith("QWORD", StringComparison.Ordinal))
                {
                    return 64; // nasm
                }

                if (s.StartsWith("TWORD", StringComparison.Ordinal))
                {
                    return 80; // nasm
                }

                if (s.StartsWith("DQWORD", StringComparison.Ordinal))
                {
                    return 128;
                }

                if (s.StartsWith("OWORD", StringComparison.Ordinal))
                {
                    return 128; // nasm
                }

                if (s.StartsWith("XMMWORD", StringComparison.Ordinal))
                {
                    return 128;
                }

                if (s.StartsWith("XWORD", StringComparison.Ordinal))
                {
                    return 128;
                }

                if (s.StartsWith("YMMWORD", StringComparison.Ordinal))
                {
                    return 256;
                }

                if (s.StartsWith("YWORD", StringComparison.Ordinal))
                {
                    return 256; // nasm
                }

                if (s.StartsWith("ZMMWORD", StringComparison.Ordinal))
                {
                    return 512;
                }

                if (s.StartsWith("ZWORD", StringComparison.Ordinal))
                {
                    return 512; // nasm
                }

                // Console.WriteLine("AsmSourceTools:GetNbitsMemOperand: could not determine nBits in token " + token + " assuming 32 bits");

                return 32;
            }
            #endregion
        }

        private static int FindEndNextWord_UNUSED(string str, int begin)
        {
            for (int i = begin; i < str.Length; ++i)
            {
                char c = str[i];
                if (char.IsWhiteSpace(c) || c.Equals('+') || c.Equals('*') || c.Equals('-') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals(':'))
                {
                    return i;
                }
            }
            return str.Length;
        }

        public static string GetKeyword(int pos, string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            (int beginPos, int endPos) = GetKeywordPos(pos, line);
            return line.Substring(beginPos, endPos - beginPos);
        }

        /// <summary>
        /// Return the previous keyword between begin and end.
        /// </summary>
        public static string GetPreviousKeyword(int begin, int end, string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            Contract.Requires(begin >= 0);
            Contract.Requires(begin <= line.Length);
            Contract.Requires(end <= line.Length);

            if (end <= 0)
            {
                return string.Empty;
            }
            if (begin == end)
            {
                return string.Empty;
            }

            int pos = (end >= line.Length) ? (line.Length - 1) : end;

            // find the end of current keyword; i.e. read until a separator
            while (pos >= begin)
            {
                if (IsSeparatorChar(line[pos]))
                {
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; lineStr=\"{0}\"; pos={1} has a separator. Found end of current keyword", lineStr, pos));
                    pos--;
                    break;
                }
                else
                {
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; lineStr=\"{0}\"; pos={1} has char {2} of current keyword", lineStr, pos, lineStr[pos]));
                    pos--;
                }
            }

            // find the end of previous keyword; i.e. read until a non separator
            int endPrevious = begin;
            while (pos >= begin)
            {
                if (IsSeparatorChar(line[pos]))
                {
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; lineStr=\"{0}\"; pos={1} has a separator.", lineStr, pos));
                    pos--;
                }
                else
                {
                    endPrevious = pos + 1;
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; lineStr=\"{0}\"; pos={1} has char {2} which is the end of previous keyword.", lineStr, pos, lineStr[pos]));
                    pos--;
                    break;
                }
            }

            // find the begin of the previous keyword; i.e. read until a separator
            int beginPrevious = begin; // set the begin of the previous keyword to the begin of search window, such that if no separator is found this will be the begin
            while (pos >= begin)
            {
                if (IsSeparatorChar(line[pos]))
                {
                    beginPrevious = pos + 1;
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; lineStr=\"{0}\"; beginPrevious={1}; pos={2}", lineStr, beginPrevious, pos));
                    break;
                }
                else
                {
                    // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; find begin. lineStr=\"{0}\"; pos={1} has char {2}", lineStr, pos, lineStr[pos]));
                    pos--;
                }
            }

            int length = endPrevious - beginPrevious;
            if (length > 0)
            {
                string previousKeyword = line.Substring(beginPrevious, length);
                // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getPreviousKeyword; previousKeyword={0}", previousKeyword));
                return previousKeyword;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>Return the begin and end of the keyword</summary>
        public static (int beginPos, int endPos) GetKeywordPos(int pos, string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            // Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getKeyword; pos={0}; lineStr=\"{1}\"", pos, new string(lineStr)));
            if ((pos < 0) || (pos >= line.Length))
            {
                return (pos, pos);
            }
            // find the beginning of the keyword
            int beginPos = 0;
            for (int i1 = pos - 1; i1 >= 0; --i1)
            {
                char c = line[i1];
                if (IsSeparatorChar(c) || char.IsControl(c) || IsRemarkChar(c))
                {
                    beginPos = i1 + 1;
                    break;
                }
            }
            // find the end of the keyword
            int endPos = line.Length;
            for (int i2 = pos; i2 < line.Length; ++i2)
            {
                char c = line[i2];
                if (IsSeparatorChar(c) || char.IsControl(c) || IsRemarkChar(c))
                {
                    endPos = i2;
                    break;
                }
            }
            return (beginPos, endPos);
        }

        public static (bool valid, int beginPos, int endPos) GetLabelDefPos(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            (bool valid, int beginPos, int endPos) tup = GetLabelDefPos_Regular(line);
            if (tup.valid)
            {
                return tup;
            }
            return GetLabelDefPos_Masm(line);
        }

        private static (bool valid, int beginPos, int endPos) GetLabelDefPos_Regular(string line)
        {
            int nChars = line.Length;
            int i = 0;

            // find the start of the first keyword
            for (; i < nChars; ++i)
            {
                char c = line[i];
                if (IsRemarkChar(c))
                {
                    return (valid: false, beginPos: 0, endPos: 0);
                }
                else if (char.IsWhiteSpace(c))
                {
                    // do nothing
                }
                else
                {
                    break;
                }
            }
            if (i >= nChars)
            {
                return (valid: false, beginPos: 0, endPos: 0);
            }
            int beginPos = i;
            // position i points to the start of the current keyword
            // AsmDudeToolsStatic.Output_INFO("getLabelEndPos: found first char of first keyword "+ lineStr[i]+".");

            for (; i < nChars; ++i)
            {
                char c = line[i];
                if (c.Equals(':'))
                {
                    if (i == 0)
                    { // we found an empty label
                        return (valid: false, beginPos: 0, endPos: 0);
                    }
                    else
                    {
                        return (valid: true, beginPos, endPos: i);
                    }
                }
                else if (IsRemarkChar(c))
                {
                    return (valid: false, beginPos: 0, endPos: 0);
                }
                else if (IsSeparatorChar(c))
                {
                    // found another keyword: labels can only be the first keyword on a lineStr
                    break;
                }
            }
            return (valid: false, beginPos: 0, endPos: 0);
        }

        private static (bool valid, int beginPos, int endPos) GetLabelDefPos_Masm(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            string line2 = line.TrimStart();
            int displacement = 0;

            if (line2.StartsWith("EXTRN", StringComparison.OrdinalIgnoreCase))
            {
                displacement = 5;
            }
            else if (line2.StartsWith("EXTERN", StringComparison.OrdinalIgnoreCase))
            {
                displacement = 6;
            }
            else
            {
                return (valid: false, beginPos: 0, endPos: 0);
            }

            string line3 = line2.Substring(displacement);
            (bool valid, int beginPos, int endPos) tup = GetLabelDefPos_Regular(line3);
            if (tup.valid)
            {
                return (valid: true, beginPos: tup.beginPos + displacement, endPos: tup.endPos + displacement);
            }
            else
            {
                return tup;
            }
        }

        /// <summary>Get the first position of a remark character in the provided lineStr;
        /// Valid is true if one such char is found.</summary>
        public static (bool valid, int beginPos, int endPos) GetRemarkPos(string line)
        {
            Contract.Requires(line != null);
            Contract.Assume(line != null);

            int nChars = line.Length;
            for (int i = 0; i < nChars; ++i)
            {
                if (IsRemarkChar(line[i]))
                {
                    return (valid: true, beginPos: i, endPos: nChars);
                }
            }
            return (valid: false, beginPos: nChars, endPos: nChars);
        }

        #region Text Wrap
        /// <summary>
        /// Forces the string to word wrap so that each lineStr doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per lineStr.</param>
        public static string Linewrap(this string str, int maxLength)
        {
            return Linewrap(str, maxLength, string.Empty);
        }

        /// <summary>
        /// Forces the string to word wrap so that each lineStr doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per lineStr.</param>
        /// <param name="prefix">Adds this string to the beginning of each lineStr.</param>
        private static string Linewrap(string str, int maxLength, string prefix)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            if (maxLength <= 0)
            {
                return prefix + str;
            }

            List<string> lines = new List<string>();

            // breaking the string into lines makes it easier to process.
            foreach (string line in str.Split("\n".ToCharArray()))
            {
                string remainingLine = line.Trim();
                do
                {
                    string newLine = GetLine(remainingLine, maxLength - prefix.Length);
                    lines.Add(newLine);
                    remainingLine = remainingLine.Substring(newLine.Length).Trim();
                    // Keep iterating as int as we've got words remaining
                    // in the lineStr.
                }
                while (remainingLine.Length > 0);
            }

            return string.Join(Environment.NewLine + prefix, lines.ToArray());
        }

        private static string GetLine(string str, int maxLength)
        {
            // The string is less than the max length so just return it.
            if (str.Length <= maxLength)
            {
                return str;
            }

            // Search backwords in the string for a whitespace char
            // starting with the char one after the maximum length
            // (if the next char is a whitespace, the last word fits).
            for (int i = maxLength; i >= 0; i--)
            {
                if (IsTextSeparatorChar(str[i]))
                {
                    return str.Substring(0, i).TrimEnd();
                }
            }

            // No whitespace chars, just break the word at the maxlength.
            return str.Substring(0, maxLength);
        }

        private static bool IsTextSeparatorChar(char c)
        {
            return char.IsWhiteSpace(c) || c.Equals('.') || c.Equals(',') || c.Equals(';') || c.Equals('?') || c.Equals('!') || c.Equals(')') || c.Equals(']') || c.Equals('-');
        }

        public static string ToStringBin(ulong value, int nBits)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = nBits - 1; i >= 0; --i)
            {
                int bit = (int)((value >> i) & 1);
                sb.Append(bit);
                if ((i > 0) && (i != nBits - 1) && (i % 8 == 0))
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }

        #endregion Text Wrap
    }
}
