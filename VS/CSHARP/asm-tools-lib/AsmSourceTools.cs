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
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AsmTools
{
    public static partial class AsmSourceTools
    {
        /// <summary>
        /// Parse the provided line. Returns label, mnemonic, args, remarks. Args are in capitals
        /// </summary>
        public static (string Label, Mnemonic Mnemonic, string[] Args, string Remark) ParseLine(string line)
        {
            //Console.WriteLine("INFO: AsmSourceTools:ParseLine: line=" + line + "; length=" + line.Length);

            string label = "";
            Mnemonic mnemonic = Mnemonic.NONE;
            string[] args = Array.Empty<string>();
            string remark = "";

            if (line.Length > 0)
            {
                (bool Valid, int BeginPos, int EndPos) = GetLabelDefPos(line);
                int codeBeginPos = 0;
                if (Valid)
                {
                    label = line.Substring(BeginPos, EndPos - BeginPos);
                    codeBeginPos = EndPos + 1; // plus one to get rid of the colon 
                    if (line.Length > codeBeginPos)
                    {
                        if (line[codeBeginPos] == ':')
                        {
                            codeBeginPos++; // remove a second colon
                        }
                    }
                    //Console.WriteLine("found label " + label);
                }

                (bool Valid, int BeginPos, int EndPos) remarkPos = GetRemarkPos(line);
                int codeEndPos = line.Length;
                if (remarkPos.Valid)
                {
                    remark = line.Substring(remarkPos.BeginPos, remarkPos.EndPos - remarkPos.BeginPos);
                    codeEndPos = remarkPos.BeginPos;
                    //Console.WriteLine("found remark " + remark);
                }

                string codeStr = line.Substring(codeBeginPos, codeEndPos - codeBeginPos).Trim().ToUpper();
                //Console.WriteLine("code string \"" + codeStr + "\".");
                if (codeStr.Length > 0)
                {
                    //Console.WriteLine(codeStr + ":" + codeStr.Length);

                    // get the first keyword, check if it is a mnemonic
                    (int BeginPos, int EndPos) keyword1Pos = GetKeywordPos(0, codeStr); // find a keyword starting a position 0
                    string keyword1 = codeStr.Substring(keyword1Pos.BeginPos, keyword1Pos.EndPos - keyword1Pos.BeginPos);
                    if (keyword1.Length > 0)
                    {
                        int startArgPos = keyword1Pos.EndPos;
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
                                    // find a second keyword starting a postion keywordPos.EndPos
                                    (int BeginPos, int EndPos) keyword2Pos = GetKeywordPos(keyword1Pos.EndPos + 1, codeStr); // find a keyword starting a position 0
                                    string keyword2 = codeStr.Substring(keyword2Pos.BeginPos, keyword2Pos.EndPos - keyword2Pos.BeginPos);
                                    if (keyword2.Length > 0)
                                    {
                                        Mnemonic mnemonic2 = ParseMnemonic(keyword2, true);
                                        if (mnemonic2 != Mnemonic.NONE)
                                        {
                                            startArgPos = keyword2Pos.EndPos;
                                            mnemonic = ParseMnemonic(mnemonic.ToString() + "_" + mnemonic2.ToString(), true);
                                        }
                                    }
                                    break;
                                }
                            default: break;
                        }

                        // find arguments after the last mnemonic
                        if (codeStr.Length > 0)
                        {
                            string argsStr = codeStr.Substring(startArgPos, codeStr.Length - startArgPos);
                            if (argsStr.Length > 0)
                            {
                                args = argsStr.Split(',');
                                for (int i = 0; i < args.Length; ++i)
                                {
                                    args[i] = args[i].Trim();
                                }
                            }
                        }
                    }
                }
            }
            //Console.WriteLine(args[1] + ":" + args[1].Length);
            return (Label: label, Mnemonic: mnemonic, Args: args, Remark: remark);
        }

        public static IList<Operand> MakeOperands(string[] operandStrArray) //TODO consider Enumerable
        {
            int nOperands = operandStrArray.Length;
            if (nOperands <= 1)
            {
                return new List<Operand>(0);
            }
            else
            {
                nOperands--;
                IList<Operand> operands = new List<Operand>(nOperands);
                for (int i = 0; i < nOperands; ++i)
                {
                    string opStr = operandStrArray[i];
                    if (opStr.Length == 0)
                    {
                        operands.Add(null);
                    }
                    else
                    {
                        operands.Add(new Operand(opStr, false));
                    }
                }
                return operands;
            }
        }

        /// <summary>
        /// return label definition position
        /// </summary>
        public static (int beginPos, int length, bool isLabel) Get_First_Keyword(string line)
        {
            bool started = false;
            int keywordBegin = 0;

            for (int i = 0; i < line.Length; ++i)
            {
                char c = line[i];
                if (IsRemarkChar(c))
                {
                    return (beginPos: 0, length: 0, isLabel: false);
                }

                if (c.Equals('"'))
                {
                    return (beginPos: 0, length: 0, isLabel: false);
                }

                if (c.Equals(':'))
                {
                    if (started)
                    {
                        return (beginPos: keywordBegin, length: i, isLabel: true);
                    }
                    else
                    {
                        return (beginPos: 0, length: 0, isLabel: false);
                    }
                }
                else if (IsSeparatorChar(c))
                {
                    if (started)
                    {
                        return (keywordBegin, length: i, false);
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
            return (beginPos: 0, length: 0, isLabel: false);
        }

        /// <summary>
        /// Split the provided line into keyword positions: first: begin pos; second: end pos; third whether the keyword is a label
        /// </summary>
        public static IEnumerable<(int BeginPos, int Length, bool IsLabel)> SplitIntoKeywordPos(string line)
        {
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
                            yield return (keywordBegin, i + 1, false);
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
                            yield return (keywordBegin, i, false);
                            isFirstKeyword = false;
                        }
                        yield return (i, line.Length, false);
                        i = line.Length;
                    }
                    else if (c.Equals('"'))
                    { // start string definition
                        if (keywordBegin < i)
                        {
                            yield return (keywordBegin, i, false);
                            isFirstKeyword = false;
                        }
                        inStringDef = true;
                        keywordBegin = i; // '"' is part of the keyword
                    }
                    //else if (IsSeparatorChar_NoOperator(c))
                    else if (IsSeparatorChar(c))
                    {
                        if (keywordBegin < i)
                        {
                            if (c.Equals(':'))
                            {
                                if (isFirstKeyword)
                                {
                                    yield return (keywordBegin, i, true);
                                }
                                else
                                {
                                    yield return (keywordBegin, i, false);
                                }
                            }
                            else
                            {
                                yield return (keywordBegin, i, false);
                            }
                            isFirstKeyword = false;
                        }
                        keywordBegin = i + 1; // separator is not part of the keyword
                    }
                }
            }

            if (keywordBegin < line.Length)
            {
                yield return (keywordBegin, line.Length, false);
            }
        }


        public static List<string> SplitIntoKeywordsList(string line)
        {
            List<string> keywords = new List<string>();
            foreach ((int BeginPos, int Length, bool IsLabel) pos in AsmSourceTools.SplitIntoKeywordPos(line))
            {
                keywords.Add(AsmSourceTools.Keyword(pos, line));
            }
            return keywords;
        }

        public static string Keyword((int BeginPos, int Length, bool IsLabel) pos, string line)
        {
            return line.Substring(pos.BeginPos, pos.Length - pos.BeginPos);
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
        /// Determine whether the provided pos is in a remark in the provided line.
        /// </summary>
        /// <param name="triggerPoint"></param>
        /// <param name="lineStart"></param>
        /// <returns></returns>
        public static bool IsInRemark(int pos, string line)
        {
            // check if the line contains a remark character before the current point
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
        /// Returns true if the provided line only contains a remark (and no labels or code, it may have white space)
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool IsRemarkOnly(string line)
        {
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
            // did not find a remark character; this line is not a remark.
            return false;
        }

        public static int GetRemarkCharPosition(string line)
        {
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
        public static (bool Valid, ulong Value, int NBits) Evaluate_Constant(string token, bool isCapitals = false)
        {
            if (token.StartsWith("$")) // AT&T syntax constants start with '$'
            {
                token = token.Substring(1);
            }

            //TODO 01-06-19 fix evaluate_constant
            //TODO bugfix: there is a issue with .net 1.6.2 and the evaluation code in Evaluate_Constant
            if (true)
            {
                // dont use expression evaluation, just parse it.
                return ExpressionEvaluator.Parse_Constant(token, isCapitals);
            }
            else
            {
                string token2 = token.Replace("_", string.Empty).Replace(".", string.Empty);
                return ExpressionEvaluator.Evaluate_Constant(token2, isCapitals);
            }
        }

        /// <summary> Check if the provided string is a constant by parsing it. Does not evaluate arithmetic in the string.</summary>
        public static (bool Valid, ulong Value, int NBits) Parse_Constant(string token, bool isCapitals = false)
        {
            return ExpressionEvaluator.Parse_Constant(token, isCapitals);
        }

        public static string Get_Related_Constant(string original, ulong value, int nBits)
        {
            string s0 = original;
            string s1 = value.ToString();
            string h1 = string.Format("{0:X}", value); // just the hex number

            //string h3 = Convert.ToString(value, 8); // no octal

            return "\\b(" + s0 + "|" + s1 + "|0x[0]{0,}" + h1 + "|[0]{0,}" + h1 + "[h]{0,1})\\b";
        }


        /// <summary>
        /// return Offset = Base + (Index * Scale) + Displacement
        /// </summary>
        public static (bool Valid, Rn BaseReg, Rn IndexReg, int Scale, long Displacement, int NBits, string ErrorMessage)
            Parse_Mem_Operand(string token, bool isCapitals = false)
        {
            int length = token.Length;
            if (length < 3)
            {
                return (Valid: false, BaseReg: Rn.NOREG, IndexReg: Rn.NOREG, Scale: 0, Displacement: 0, NBits: 0, ErrorMessage: null); // do not return a error message because the provided token can be a label
            }

            if (!isCapitals)
            {
                token = token.ToUpper();
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
                return (Valid: false, BaseReg: Rn.NOREG, IndexReg: Rn.NOREG, Scale: 0, Displacement: 0, NBits: 0, ErrorMessage: null);// do not return a error message because the provided token can be a label
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

                (bool Valid, ulong Value, int NBits) = ExpressionEvaluator.Parse_Constant(y, true);
                if (Valid)
                {
                    if (foundDisplacement)
                    {
                        // found an second displacement, error
                        return (Valid: false, BaseReg: Rn.NOREG, IndexReg: Rn.NOREG, Scale: 0, Displacement: 0, NBits: 0, ErrorMessage: "Multiple displacements");
                    }
                    else
                    {
                        foundDisplacement = true;
                        displacement = negativeDisplacement ? -(long)Value : (long)Value;
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
                        string scaleRaw = null;
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
                            return (Valid: false, BaseReg: Rn.NOREG, IndexReg: Rn.NOREG, Scale: 0, Displacement: 0, NBits: 0, ErrorMessage: "Invalid scale " + scaleRaw);
                        }
                    }
                }
            }

            if ((baseRn != Rn.NOREG) && (indexRn != Rn.NOREG))
            {
                if (RegisterTools.NBits(baseRn) != RegisterTools.NBits(indexRn))
                {
                    return (Valid: false, BaseReg: Rn.NOREG, IndexReg: Rn.NOREG, Scale: 0, Displacement: 0, NBits: 0, ErrorMessage: "Number of bits of base register " + baseRn + " is not equal to number of bits of index register " + indexRn);
                }
            }
            return (Valid: true, BaseReg: baseRn, IndexReg: indexRn, Scale: scale, Displacement: displacement, NBits: nBits, ErrorMessage: null);

            #region Local Methods
            int ParseScale(string str)
            {
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
                string s = token2.TrimStart();
                if (s.StartsWith("PTR"))
                {
                    s = s.Substring(3, token.Length - 3).TrimStart();
                }

                if (s.StartsWith("BYTE"))
                {
                    return 8; //nasm
                }

                if (s.StartsWith("SBYTE"))
                {
                    return 8;
                }

                if (s.StartsWith("WORD"))
                {
                    return 16; //nasm
                }

                if (s.StartsWith("SWORD"))
                {
                    return 16;
                }

                if (s.StartsWith("DWORD"))
                {
                    return 32; //nasm
                }

                if (s.StartsWith("SDWORD"))
                {
                    return 32;
                }

                if (s.StartsWith("QWORD"))
                {
                    return 64; //nasm
                }

                if (s.StartsWith("TWORD"))
                {
                    return 80; //nasm
                }

                if (s.StartsWith("DQWORD"))
                {
                    return 128;
                }

                if (s.StartsWith("OWORD"))
                {
                    return 128; //nasm
                }

                if (s.StartsWith("XMMWORD"))
                {
                    return 128;
                }

                if (s.StartsWith("XWORD"))
                {
                    return 128;
                }

                if (s.StartsWith("YMMWORD"))
                {
                    return 256;
                }

                if (s.StartsWith("YWORD"))
                {
                    return 256; //nasm
                }

                if (s.StartsWith("ZMMWORD"))
                {
                    return 512;
                }

                if (s.StartsWith("ZWORD"))
                {
                    return 512; //nasm
                }

                //Console.WriteLine("AsmSourceTools:GetNbitsMemOperand: could not determine nBits in token " + token + " assuming 32 bits");

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
            (int beginPos, int endPos) = GetKeywordPos(pos, line);
            return line.Substring(beginPos, endPos - beginPos);
        }

        /// <summary>
        /// Return the previous keyword between begin and end. 
        /// </summary>
        public static string GetPreviousKeyword(int begin, int end, string line)
        {
            Debug.Assert(begin >= 0);
            Debug.Assert(begin <= line.Length);
            Debug.Assert(end <= line.Length);

            if (end <= 0)
            {
                return "";
            }
            if (begin == end)
            {
                return "";
            }

            int pos = (end >= line.Length) ? (line.Length - 1) : end;

            // find the end of current keyword; i.e. read until a separator
            while (pos >= begin)
            {
                if (IsSeparatorChar(line[pos]))
                {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has a separator. Found end of current keyword", line, pos));
                    pos--;
                    break;
                }
                else
                {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has char {2} of current keyword", line, pos, line[pos]));
                    pos--;
                }
            }

            // find the end of previous keyword; i.e. read until a non separator
            int endPrevious = begin;
            while (pos >= begin)
            {
                if (IsSeparatorChar(line[pos]))
                {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has a separator.", line, pos));
                    pos--;
                }
                else
                {
                    endPrevious = pos + 1;
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has char {2} which is the end of previous keyword.", line, pos, line[pos]));
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
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; beginPrevious={1}; pos={2}", line, beginPrevious, pos));
                    break;
                }
                else
                {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; find begin. line=\"{0}\"; pos={1} has char {2}", line, pos, line[pos]));
                    pos--;
                }
            }

            int length = endPrevious - beginPrevious;
            if (length > 0)
            {
                string previousKeyword = line.Substring(beginPrevious, length);
                //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; previousKeyword={0}", previousKeyword));
                return previousKeyword;
            }
            else
            {
                return "";
            }
        }

        /// <summary>Return the begin and end of the keyword</summary>
        public static (int BeginPos, int EndPos) GetKeywordPos(int pos, string line)
        {
            //Debug.WriteLine(string.Format("INFO: getKeyword; pos={0}; line=\"{1}\"", pos, new string(line)));
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
            return (BeginPos: beginPos, EndPos: endPos);
        }

        public static (bool Valid, int BeginPos, int EndPos) GetLabelDefPos(string line)
        {
            (bool Valid, int BeginPos, int EndPos) tup = GetLabelDefPos_Regular(line);
            if (tup.Valid)
            {
                return tup;
            }
            return GetLabelDefPos_Masm(line);
        }

        private static (bool Valid, int BeginPos, int EndPos) GetLabelDefPos_Regular(string line)
        {
            int nChars = line.Length;
            int i = 0;

            // find the start of the first keyword
            for (; i < nChars; ++i)
            {
                char c = line[i];
                if (IsRemarkChar(c))
                {
                    return (Valid: false, BeginPos: 0, EndPos: 0);
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
                return (Valid: false, BeginPos: 0, EndPos: 0);
            }
            int beginPos = i;
            // position i points to the start of the current keyword
            //AsmDudeToolsStatic.Output_INFO("getLabelEndPos: found first char of first keyword "+ line[i]+".");

            for (; i < nChars; ++i)
            {
                char c = line[i];
                if (c.Equals(':'))
                {
                    if (i == 0)
                    { // we found an empty label
                        return (Valid: false, BeginPos: 0, EndPos: 0);
                    }
                    else
                    {
                        return (Valid: true, BeginPos: beginPos, EndPos: i);
                    }
                }
                else if (IsRemarkChar(c))
                {
                    return (Valid: false, BeginPos: 0, EndPos: 0);
                }
                else if (IsSeparatorChar(c))
                {
                    // found another keyword: labels can only be the first keyword on a line
                    break;
                }
            }
            return (Valid: false, BeginPos: 0, EndPos: 0);
        }

        private static (bool Valid, int BeginPos, int EndPos) GetLabelDefPos_Masm(string line)
        {
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
                return (Valid: false, BeginPos: 0, EndPos: 0);
            }

            string line3 = line2.Substring(displacement);
            (bool Valid, int BeginPos, int EndPos) tup = GetLabelDefPos_Regular(line3);
            if (tup.Valid)
            {
                return (Valid: true, BeginPos: tup.BeginPos + displacement, EndPos: tup.EndPos + displacement);
            }
            else
            {
                return tup;
            }
        }


        /// <summary>Get the first position of a remark character in the provided line; 
        /// Valid is true if one such char is found.</summary>
        public static (bool Valid, int BeginPos, int EndPos) GetRemarkPos(string line)
        {
            int nChars = line.Length;
            for (int i = 0; i < nChars; ++i)
            {
                if (IsRemarkChar(line[i]))
                {
                    return (Valid: true, BeginPos: i, EndPos: nChars);
                }
            }
            return (Valid: false, BeginPos: nChars, EndPos: nChars);
        }

        #region Text Wrap
        /// <summary>
        /// Forces the string to word wrap so that each line doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per line.</param>
        /// <returns></returns>
        public static string Linewrap(this string str, int maxLength)
        {
            return Linewrap(str, maxLength, "");
        }

        /// <summary>
        /// Forces the string to word wrap so that each line doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per line.</param>
        /// <param name="prefix">Adds this string to the beginning of each line.</param>
        /// <returns></returns>
        private static string Linewrap(string str, int maxLength, string prefix)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
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
                    // in the line.
                } while (remainingLine.Length > 0);
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
