using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace AsmTools {

    public static partial class AsmSourceTools {



        /// <summary>
        /// Parse the provided line. Returns label, mnemonic, args, remarks
        /// </summary>
        public static Tuple<string, Mnemonic, string[], string> parseLine(string line) {
            string label = "";
            Mnemonic mnemonic = Mnemonic.UNKNOWN;
            string[] args = new string[0] { };
            string remark = "";

            if (line.Length > 0) {
                Tuple<bool, int, int> labelPos = AsmTools.AsmSourceTools.getLabelDefPos(line);
                int codeBeginPos = 0;
                if (labelPos.Item1) {
                    label = line.Substring(labelPos.Item2, labelPos.Item3 - labelPos.Item2);
                    codeBeginPos = labelPos.Item3 + 1; // plus one to get rid of the colon 
                    if (line.Length > codeBeginPos) {
                        if (line[codeBeginPos] == ':') codeBeginPos++; // remove a second colon
                    }
                    //Console.WriteLine("found label " + label);
                }

                Tuple<bool, int, int> remarkPos = AsmTools.AsmSourceTools.getRemarkPos(line);
                int codeEndPos = line.Length;
                if (remarkPos.Item1) {
                    remark = line.Substring(remarkPos.Item2, remarkPos.Item3 - remarkPos.Item2);
                    codeEndPos = remarkPos.Item2;
                    //Console.WriteLine("found remark " + remark);
                }

                string codeStr = line.Substring(codeBeginPos, codeEndPos - codeBeginPos).Trim().ToUpper();
                //Console.WriteLine("code string \"" + codeStr + "\".");

                // get the first keyword, check if it is a mnemonic
                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(0, codeStr);
                string firstKeyword = codeStr.Substring(t.Item1, t.Item2 - t.Item1);
                if (firstKeyword.Length > 0) {
                    mnemonic = AsmTools.AsmSourceTools.parseMnemonic(firstKeyword);
                    if (mnemonic == Mnemonic.UNKNOWN) {
                        //Console.WriteLine("INFO: ToolsZ3:parseLine: found unknown first Keyword \"" + firstKeyword + "\". Ignoring this line.");
                    } else {
                        //Console.WriteLine("INFO: ToolsZ3:parseLine: found codeStr " + codeStr);
                        if (codeStr.Length > 0) {
                            string argsStr = codeStr.Substring(t.Item2, codeStr.Length - t.Item2);
                            if (argsStr.Length > 0) {
                                args = argsStr.Split(',');
                                for (int i = 0; i < args.Length; ++i) {
                                    args[i] = args[i].Trim();
                                }
                            }
                        }
                    }
                }
            }
            return new Tuple<string, Mnemonic, string[], string>(label, mnemonic, args, remark);
        }

        public static IList<Operand> makeOperands(string[] operandStrArray) {
            int nOperands = operandStrArray.Length;
            if (nOperands <= 1) {
                return new List<Operand>(0);
            } else {
                nOperands--;
                IList<Operand> operands = new List<Operand>(nOperands);
                for (int i = 0; i < nOperands; ++i) {
                    string opStr = operandStrArray[i];
                    if (opStr.Length == 0) {
                        operands.Add(null);
                    } else {
                        operands.Add(new Operand(opStr));
                    }
                }
                return operands;
            }
        }

        /// <summary>
        /// Split the provided line into keyword positions: first: begin pos; second: end pos; third whether the keyword is the first keyword
        /// </summary>
        public static IList<Tuple<int, int, bool>> splitIntoKeywordPos(string line) {
            IList<Tuple<int, int, bool>> list = new List<Tuple<int, int, bool>>();

            int keywordBegin = 0;
            bool inStringDef = false;
            bool isFirstKeyword = true;

            for (int i = 0; i < line.Length; ++i) {
                char c = line[i];

                if (inStringDef) {
                    if (c.Equals('"')) {
                        inStringDef = false;
                        if (keywordBegin < i) {
                            list.Add(new Tuple<int, int, bool>(keywordBegin, i + 1, false));
                            isFirstKeyword = false;
                        }
                        keywordBegin = i + 1; // next keyword starts at the next char
                    }
                } else {
                    if (isRemarkChar(c)) {
                        if (keywordBegin < i) {
                            list.Add(new Tuple<int, int, bool>(keywordBegin, i, false));
                            isFirstKeyword = false;
                        }
                        list.Add(new Tuple<int, int, bool>(i, line.Length, false));
                        i = line.Length;
                    } else if (c.Equals('"')) { // start string definition
                        if (keywordBegin < i) {
                            list.Add(new Tuple<int, int, bool>(keywordBegin, i, false));
                            isFirstKeyword = false;
                        }
                        inStringDef = true;
                        keywordBegin = i; // '"' is part of the keyword
                    } else if (isSeparatorChar(c)) {
                        if (keywordBegin < i) {
                            if (c.Equals(':')) {
                                if (isFirstKeyword) {
                                    list.Add(new Tuple<int, int, bool>(keywordBegin, i, true));
                                } else {
                                    list.Add(new Tuple<int, int, bool>(keywordBegin, i, false));
                                }
                            } else {
                                list.Add(new Tuple<int, int, bool>(keywordBegin, i, false));
                            }
                            isFirstKeyword = false;
                        }
                        keywordBegin = i + 1; // separator is not part of the keyword
                    }
                }
            }

            if (keywordBegin < line.Length) {
                list.Add(new Tuple<int, int, bool>(keywordBegin, line.Length, false));
            }
            return list;
        }

        public static bool isRemarkChar(char c) {
            return c.Equals('#') || c.Equals(';');
        }

        public static bool isSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals('+') || c.Equals('-') || c.Equals('*') || c.Equals(':');
        }

        /// <summary>
        /// Determine whether the provided pos is in a remark in the provided line.
        /// </summary>
        /// <param name="triggerPoint"></param>
        /// <param name="lineStart"></param>
        /// <returns></returns>
        public static bool isInRemark(int pos, string line) {
            // check if the line contains a remark character before the current point
            int nChars = line.Length;
            int startPos = (pos >= nChars) ? nChars - 1 : pos;
            for (int i = startPos; i > 0; --i) {
                if (AsmSourceTools.isRemarkChar(line[i])) {
                    return true;
                }
            }
            return false;
        }

        public static bool isConstant(string token) { // todo merge this with toConstant
            string token2;
            if (token.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)) {
                token2 = token.Substring(2);
            } else if (token.EndsWith("h", StringComparison.CurrentCultureIgnoreCase)) {
                token2 = token.Substring(0, token.Length - 1);
            } else {
                token2 = token;
            }
            ulong dummy;
            bool parsedSuccessfully = ulong.TryParse(token2, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out dummy);
            return parsedSuccessfully;
        }

        /// <summary>
        /// Check if the provided string is a constant, return (bool Exists, ulong value, int nBits)
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Tuple<bool, ulong, int> toConstant(string token) {
            string token2;
            bool isHex = false;
            if (token.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)) {
                token2 = token.Substring(2);
                isHex = true;
            } else if (token.EndsWith("h", StringComparison.CurrentCultureIgnoreCase)) {
                token2 = token.Substring(0, token.Length - 1);
                isHex = true;
            } else {
                token2 = token;
            }
            ulong v;
            bool parsedSuccessfully;
            if (isHex) {
                parsedSuccessfully = ulong.TryParse(token2.Replace("_", string.Empty), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out v);
            } else {
                parsedSuccessfully = ulong.TryParse(token2.Replace("_", string.Empty), NumberStyles.Integer, CultureInfo.CurrentCulture, out v);
            }

            int nBits = (parsedSuccessfully) ? nBitsStorageNeeded(v) : -1;
            return new Tuple<bool, ulong, int>(parsedSuccessfully, v, nBits);
        }

        public static int nBitsStorageNeeded(ulong v) {
            int nBits = -1;
            if ((v & 0xFFFFFFFFFFFFFF00ul) == 0) {
                nBits = 8;
            } else if ((v & 0xFFFFFFFFFFFF0000ul) == 0) {
                nBits = 16;
            } else if ((v & 0xFFFFFFFF00000000ul) == 0) {
                nBits = 32;
            } else {
                nBits = 64;
            }
            return nBits;
        }

        /// <summary>
        /// Return the number of bits of the provided operand (assumes 64-bits)
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static int getNbitsMemOperand(string token) {

            string s = token.TrimStart().ToUpper();
            if (s.StartsWith("PTR")) token = token.Substring(3, token.Length - 3).TrimStart();

            if (s.StartsWith("BYTE")) return 8;
            if (s.StartsWith("SBYTE")) return 8;
            if (s.StartsWith("WORD")) return 16;
            if (s.StartsWith("SWORD")) return 16;

            if (s.StartsWith("DWORD")) return 32;
            if (s.StartsWith("SDWORD")) return 32;
            if (s.StartsWith("QWORD")) return 64;
            if (s.StartsWith("TWORD")) return 80;

            if (s.StartsWith("XMMWORD")) return 128;
            if (s.StartsWith("XWORD")) return 128;
            if (s.StartsWith("YMMWORD")) return 256;
            if (s.StartsWith("YWORD")) return 256;
            if (s.StartsWith("ZMMWORD")) return 512;
            if (s.StartsWith("ZWORD")) return 512;

            return 32;
        }

        /// <summary>
        /// return Offset = Base + (Index * Scale) + Displacement
        /// </summary>
        public static Tuple<bool, Rn, Rn, int, long, int> parseMemOperand(string token) {

            int length = token.Length;
            if (length < 3) {
                return new Tuple<bool, Rn, Rn, int, long, int>(false, Rn.NOREG, Rn.NOREG, 0, 0, 0);
            }

            // 1] select everything between []
            int beginPos = length;
 
            for (int i = 0; i< length; ++i) {
                if (token[i] == '[') beginPos = i+1;
            }

            int nBits = getNbitsMemOperand(token);

            int endPos = length;
            for (int i = beginPos; i < length; ++i) {
                if (token[i] == ']') endPos = i;
            }

            token = token.Substring(beginPos, endPos - beginPos).Trim();
            length = token.Length;
            if (length == 0) {
                return new Tuple<bool, Rn, Rn, int, long, int>(false, Rn.NOREG, Rn.NOREG, 0, 0, 0);
            }

            // 2] check if the displacement is negative
            bool negativeDisplacement = token.Contains('-');
            if (negativeDisplacement) {
                token = token.Replace('-', '+');
            }

            // 3] remove superfluous initial +
            if (token[0] == '+') {
                token = token.Substring(1, length - 1).Trim();
            }

            // 4] split based on +
            string[] x = token.Split('+');

            Rn baseRn = Rn.NOREG;
            Rn indexRn = Rn.NOREG;
            int scale = 0;
            long displacement = 0;

            bool foundDisplacement = false;
            

            for (int i = 0; i < x.Length; ++i) {
                string y = x[i].Trim();

                var t2 = AsmSourceTools.toConstant(y);
                if (t2.Item1) {
                    if (foundDisplacement) {
                        // found an second displacement, error
                        return new Tuple<bool, Rn, Rn, int, long, int>(false, Rn.NOREG, Rn.NOREG, 0, 0, 0);
                    } else {
                        foundDisplacement = true;
                        displacement = (negativeDisplacement) ? -(long)t2.Item2 : (long)t2.Item2;
                    }
                } else {
                    Rn t1 = RegisterTools.parseRn(y);
                    if (t1 != Rn.NOREG) {
                        if (baseRn == Rn.NOREG) {
                            baseRn = t1;
                        } else {
                            indexRn = t1;
                            scale = 1;
                        }
                    }

                    if (y.Contains('*')) {
                        string[] z = y.Split('*');
                        string z0 = z[0].Trim();
                        string z1 = z[1].Trim();
                        Rn z0r = RegisterTools.parseRn(z0);
                        if (z0r != Rn.NOREG) {
                            indexRn = z0r;
                            scale = parseScale(z1);
                        } else {
                            Rn z1r = RegisterTools.parseRn(z1);
                            if (z1r != Rn.NOREG) {
                                indexRn = z1r;
                                scale = parseScale(z0);
                            }
                        }
                    }
                }
            }

            if (scale == -1) {
                return new Tuple<bool, Rn, Rn, int, long, int>(false, Rn.NOREG, Rn.NOREG, 0, 0, 0);
            }
            if ((baseRn != Rn.NOREG) && (indexRn != Rn.NOREG)) {
                if (RegisterTools.nBits(baseRn) != RegisterTools.nBits(indexRn)) {
                    return new Tuple<bool, Rn, Rn, int, long, int>(false, Rn.NOREG, Rn.NOREG, 0, 0, 0);
                }
            }
            return new Tuple<bool, Rn, Rn, int, long, int>(true, baseRn, indexRn, scale, displacement, nBits);
        }

        private static int parseScale(string str) {
            switch (str) {
                case "0": return 0;
                case "1": return 1;
                case "2": return 2;
                case "4": return 4;
                case "8": return 8;
                default: return -1;
            }
        }

        private static int findEndNextWord(string str, int begin) {
            for (int i = begin; i < str.Length; ++i) {
                char c = str[i];
                if (char.IsWhiteSpace(c) || c.Equals('+') || c.Equals('*') || c.Equals('-') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals(':')) { 
                    return i;
                }
            }
            return str.Length;
        }

        public static string getKeyword(int pos, string line) {
            var t = AsmSourceTools.getKeywordPos(pos, line);
            int beginPos = t.Item1;
            int endPos = t.Item2;
            string result = line.Substring(beginPos, endPos - beginPos);
            return result;
        }

        /// <summary>
        /// Return the previous keyword between begin and end. 
        /// </summary>
        public static string getPreviousKeyword(int begin, int end, string line) {
            Debug.Assert(begin >= 0);
            Debug.Assert(begin <= line.Length);
            Debug.Assert(end <= line.Length);

            if (end <= 0) {
                return "";
            }
            if (begin == end) {
                return "";
            }

            int pos = (end >= line.Length) ? (line.Length-1) : end;

            // find the end of current keyword; i.e. read until a separator
            while (pos >= begin) {
                if (AsmTools.AsmSourceTools.isSeparatorChar(line[pos])) {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has a separator. Found end of current keyword", line, pos));
                    pos--;
                    break;
                } else {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has char {2} of current keyword", line, pos, line[pos]));
                    pos--;
                }
            }

            // find the end of previous keyword; i.e. read until a non separator
            int endPrevious = begin;
            while (pos >= begin) {
                if (AsmTools.AsmSourceTools.isSeparatorChar(line[pos])) {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has a separator.", line, pos));
                    pos--;
                } else {
                    endPrevious = pos+1;
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; pos={1} has char {2} which is the end of previous keyword.", line, pos, line[pos]));
                    pos--;
                    break;
                }
            }

            // find the begin of the previous keyword; i.e. read until a separator
            int beginPrevious = begin; // set the begin of the previous keyword to the begin of search window, such that if no separator is found this will be the begin
            while (pos >= begin) {
                if (AsmTools.AsmSourceTools.isSeparatorChar(line[pos])) {
                    beginPrevious = pos+1;
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; line=\"{0}\"; beginPrevious={1}; pos={2}", line, beginPrevious, pos));
                    break;
                } else {
                    //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; find begin. line=\"{0}\"; pos={1} has char {2}", line, pos, line[pos]));
                    pos--;
                }
            }

            int length = endPrevious - beginPrevious;
            if (length > 0) {
                string previousKeyword = line.Substring(beginPrevious, length);
                //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; previousKeyword={0}", previousKeyword));
                return previousKeyword;
            } else {
                return "";
            }
        }

        /// <summary>
        /// Return the begin and end of the keyword
        /// </summary>
        public static Tuple<int, int> getKeywordPos(int pos, string line) {
            //Debug.WriteLine(string.Format("INFO: getKeyword; pos={0}; line=\"{1}\"", pos, new string(line)));
            if ((pos < 0) || (pos >= line.Length)) {
                return new Tuple<int, int>(pos, pos);
            }
            // find the beginning of the keyword
            int beginPos = 0;
            for (int i1 = pos - 1; i1 >= 0; --i1) {
                char c = line[i1];
                if (AsmSourceTools.isSeparatorChar(c) || Char.IsControl(c) || AsmSourceTools.isRemarkChar(c)) {
                    beginPos = i1 + 1;
                    break;
                }
            }
            // find the end of the keyword
            int endPos = line.Length;
            for (int i2 = pos; i2 < line.Length; ++i2) {
                char c = line[i2];
                if (AsmSourceTools.isSeparatorChar(c) || Char.IsControl(c) || AsmSourceTools.isRemarkChar(c)) {
                    endPos = i2;
                    break;
                }
            }
            return new Tuple<int, int>(beginPos, endPos);
        }

        public static Tuple<bool, int, int> getLabelDefPos(string line) {
            var tup = getLabelDefPos_Regular(line);
            if (tup.Item1) {
                return tup;
            }
            return getLabelDefPos_Masm(line);
        }

        private static Tuple<bool, int, int> getLabelDefPos_Regular(string line) {
            int nChars = line.Length;
            int i = 0;

            // find the start of the first keyword
            for (; i < nChars; ++i) {
                char c = line[i];
                if (AsmSourceTools.isRemarkChar(c)) {
                    return new Tuple<bool, int, int>(false, 0, 0);
                } else if (char.IsWhiteSpace(c)) {
                    // do nothing
                } else {
                    break;
                }
            }
            if (i >= nChars) {
                return new Tuple<bool, int, int>(false, 0, 0);
            }
            int beginPos = i;
            // position i points to the start of the current keyword
            //AsmDudeToolsStatic.Output("getLabelEndPos: found first char of first keyword "+ line[i]+".");

            for (; i < nChars; ++i) {
                char c = line[i];
                if (c.Equals(':')) {
                    if (i == 0) { // we found an empty label
                        return new Tuple<bool, int, int>(false, 0, 0);
                    } else {
                        return new Tuple<bool, int, int>(true, beginPos, i);
                    }
                } else if (AsmSourceTools.isRemarkChar(c)) {
                    return new Tuple<bool, int, int>(false, 0, 0);
                } else if (AsmSourceTools.isSeparatorChar(c)) {
                    // found another keyword: labels can only be the first keyword on a line
                    break;
                }
            }
            return new Tuple<bool, int, int>(false, 0, 0);
        }

        private static Tuple<bool, int, int> getLabelDefPos_Masm(string line) {

            string line2 = line.TrimStart();
            int displacement = 0;

            if (line2.StartsWith("EXTRN", StringComparison.CurrentCultureIgnoreCase)) {
                displacement = 5;
            } else if (line2.StartsWith("EXTERN", StringComparison.CurrentCultureIgnoreCase)) {
                displacement = 6;
            } else {
                return new Tuple<bool, int, int>(false, 0, 0);
            }

            string line3 = line2.Substring(displacement);
            var tup = getLabelDefPos_Regular(line3);
            if (tup.Item1) {
                return new Tuple<bool, int, int>(true, tup.Item2 + displacement, tup.Item3 + displacement);
            } else {
                return tup;
            }
        }

        public static Tuple<bool, int, int> getRemarkPos(string line) {
            int nChars = line.Length;
            for (int i = 0; i < nChars; ++i) {
                if (AsmSourceTools.isRemarkChar(line[i])) {
                    return new Tuple<bool, int, int>(true, i, nChars);
                }
            }
            return new Tuple<bool, int, int>(false, nChars, nChars);
        }

        #region Text Wrap
        /// <summary>
        /// Forces the string to word wrap so that each line doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per line.</param>
        /// <returns></returns>
        public static string linewrap(this string str, int maxLength) {
            return linewrap(str, maxLength, "");
        }

        /// <summary>
        /// Forces the string to word wrap so that each line doesn't exceed the maxLineLength.
        /// </summary>
        /// <param name="str">The string to wrap.</param>
        /// <param name="maxLength">The maximum number of characters per line.</param>
        /// <param name="prefix">Adds this string to the beginning of each line.</param>
        /// <returns></returns>
        private static string linewrap(string str, int maxLength, string prefix) {
            if (string.IsNullOrEmpty(str)) return "";
            if (maxLength <= 0) return prefix + str;

            var lines = new List<string>();

            // breaking the string into lines makes it easier to process.
            foreach (string line in str.Split("\n".ToCharArray())) {
                var remainingLine = line.Trim();
                do {
                    var newLine = getLine(remainingLine, maxLength - prefix.Length);
                    lines.Add(newLine);
                    remainingLine = remainingLine.Substring(newLine.Length).Trim();
                    // Keep iterating as int as we've got words remaining 
                    // in the line.
                } while (remainingLine.Length > 0);
            }

            return string.Join(Environment.NewLine + prefix, lines.ToArray());
        }
        private static string getLine(string str, int maxLength) {
            // The string is less than the max length so just return it.
            if (str.Length <= maxLength) return str;

            // Search backwords in the string for a whitespace char
            // starting with the char one after the maximum length
            // (if the next char is a whitespace, the last word fits).
            for (int i = maxLength; i >= 0; i--) {
                if (isTextSeparatorChar(str[i]))
                    return str.Substring(0, i).TrimEnd();
            }

            // No whitespace chars, just break the word at the maxlength.
            return str.Substring(0, maxLength);
        }

        private static bool isTextSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals('.') || c.Equals(',') || c.Equals(';') || c.Equals('?') || c.Equals('!') || c.Equals(')') || c.Equals(']') || c.Equals('-');
        }

        #endregion Text Wrap
    }
}
