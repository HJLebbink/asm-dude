using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {

    public static partial class AsmSourceTools {

        public static bool isRemarkChar(char c) {
            return c.Equals('#') || c.Equals(';');
        }

        public static bool isSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')') || c.Equals('+') || c.Equals('-') || c.Equals('*') || c.Equals(':');
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

        public static Tuple<bool, int, int> getLabelPos(string line) {
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
                    return new Tuple<bool, int, int>(true, beginPos, i);
                } else if (AsmSourceTools.isRemarkChar(c)) {
                    return new Tuple<bool, int, int>(false, 0, 0);
                } else if (AsmSourceTools.isSeparatorChar(c)) {
                    // found another keyword: labels can only be the first keyword on a line
                    break;
                }
            }
            return new Tuple<bool, int, int>(false, 0, 0);
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

        public static string getRelatedRegister(string reg) {

            //TODO use register enum
            switch (reg.ToUpper()) {
                case "RAX":
                case "EAX":
                case "AX":
                case "AL":
                case "AH":
                    return "\\b(RAX|EAX|AX|AH|AL)\\b";
                case "RBX":
                case "EBX":
                case "BX":
                case "BL":
                case "BH":
                    return "\\b(RBX|EBX|BX|BH|BL)\\b";
                case "RCX":
                case "ECX":
                case "CX":
                case "CL":
                case "CH":
                    return "\\b(RCX|ECX|CX|CH|CL)\\b";
                case "RDX":
                case "EDX":
                case "DX":
                case "DL":
                case "DH":
                    return "\\b(RDX|EDX|DX|DH|DL)\\b";
                case "RSI":
                case "ESI":
                case "SI":
                case "SIL":
                    return "\\b(RSI|ESI|SI|SIL)\\b";
                case "RDI":
                case "EDI":
                case "DI":
                case "DIL":
                    return "\\b(RDI|EDI|DI|DIL)\\b";
                case "RBP":
                case "EBP":
                case "BP":
                case "BPL":
                    return "\\b(RBP|EBP|BP|BPL)\\b";
                case "RSP":
                case "ESP":
                case "SP":
                case "SPL":
                    return "\\b(RSP|ESP|SP|SPL)\\b";
                case "R8":
                case "R8D":
                case "R8W":
                case "R8B":
                    return "\\b(R8|R8D|R8W|R8B)\\b";
                case "R9":
                case "R9D":
                case "R9W":
                case "R9B":
                    return "\\b(R9|R9D|R9W|R9B)\\b";
                case "R10":
                case "R10D":
                case "R10W":
                case "R10B":
                    return "\\b(R10|R10D|R10W|R10B)\\b";
                case "R11":
                case "R11D":
                case "R11W":
                case "R11B":
                    return "\\b(R11|R11D|R11W|R11B)\\b";
                case "R12":
                case "R12D":
                case "R12W":
                case "R12B":
                    return "\\b(R12|R12D|R12W|R12B)\\b";
                case "R13":
                case "R13D":
                case "R13W":
                case "R13B":
                    return "\\b(R13|R13D|R13W|R13B)\\b";
                case "R14":
                case "R14D":
                case "R14W":
                case "R14B":
                    return "\\b(R14|R14D|R14W|R14B)\\b";
                case "R15":
                case "R15D":
                case "R15W":
                case "R15B":
                    return "\\b(R15|R15D|R15W|R15B)\\b";

                case "MM0":
                case "XMM0":
                case "YMM0":
                case "ZMM0":
                    return "\\b(MM0|XMM0|YMM0|ZMM0)\\b";

                case "MM1":
                case "XMM1":
                case "YMM1":
                case "ZMM1":
                    return "\\b(MM1|XMM1|YMM1|ZMM1)\\b";
                case "MM2":
                case "XMM2":
                case "YMM2":
                case "ZMM2":
                    return "\\b(MM2|XMM2|YMM2|ZMM2)\\b";
                case "MM3":
                case "XMM3":
                case "YMM3":
                case "ZMM3":
                    return "\\b(MM3|XMM3|YMM3|ZMM3)\\b";
                case "MM4":
                case "XMM4":
                case "YMM4":
                case "ZMM4":
                    return "\\b(MM4|XMM4|YMM4|ZMM4)\\b";
                case "MM5":
                case "XMM5":
                case "YMM5":
                case "ZMM5":
                    return "\\b(MM5|XMM5|YMM5|ZMM5)\\b";
                case "MM6":
                case "XMM6":
                case "YMM6":
                case "ZMM6":
                    return "\\b(MM6|XMM6|YMM6|ZMM6)\\b";
                case "MM7":
                case "XMM7":
                case "YMM7":
                case "ZMM7":
                    return "\\b(MM7|XMM7|YMM7|ZMM7)\\b";
                case "XMM8":
                case "YMM8":
                case "ZMM8":
                    return "\\b(XMM8|YMM8|ZMM8)\\b";
                case "XMM9":
                case "YMM9":
                case "ZMM9":
                    return "\\b(XMM9|YMM9|ZMM9)\\b";
                case "XMM10":
                case "YMM10":
                case "ZMM10":
                    return "\\b(XMM10|YMM10|ZMM10)\\b";
                case "XMM11":
                case "YMM11":
                case "ZMM11":
                    return "\\b(XMM11|YMM11|ZMM11)\\b";
                case "XMM12":
                case "YMM12":
                case "ZMM12":
                    return "\\b(XMM12|YMM12|ZMM12)\\b";
                case "XMM13":
                case "YMM13":
                case "ZMM13":
                    return "\\b(XMM13|YMM13|ZMM13)\\b";
                case "XMM14":
                case "YMM14":
                case "ZMM14":
                    return "\\b(XMM14|YMM14|ZMM14)\\b";
                case "XMM15":
                case "YMM15":
                case "ZMM15":
                    return "\\b(XMM15|YMM15|ZMM15)\\b";

                default: return reg;
            }
        }

        private static bool isRegisterMethod1(string keyword) {
            //TODO  get this info from AsmDudeData.xml
            switch (keyword.ToUpper()) {

                #region GPR
                case "RAX"://
                case "EAX"://
                case "AX"://
                case "AL"://
                case "AH"://

                case "RBX"://
                case "EBX"://
                case "BX"://
                case "BL"://
                case "BH"://

                case "RCX"://
                case "ECX"://
                case "CX"://
                case "CL"://
                case "CH"://

                case "RDX"://
                case "EDX"://
                case "DX"://
                case "DL"://
                case "DH"://

                case "RSI"://
                case "ESI"://
                case "SI"://
                case "SIL"://

                case "RDI"://
                case "EDI"://
                case "DI"://
                case "DIL"://

                case "RBP"://
                case "EBP"://
                case "BP"://
                case "BPL"://

                case "RSP"://
                case "ESP"://
                case "SP"://
                case "SPL"://

                case "R8"://
                case "R8D"://
                case "R8W"://
                case "R8B"://

                case "R9"://
                case "R9D"://
                case "R9W"://
                case "R9B"://

                case "R10"://
                case "R10D"://
                case "R10W"://
                case "R10B"://

                case "R11"://
                case "R11D"://
                case "R11W"://
                case "R11B"://

                case "R12"://
                case "R12D"://
                case "R12W"://
                case "R12B"://

                case "R13"://
                case "R13D"://
                case "R13W"://
                case "R13B"://

                case "R14"://
                case "R14D"://
                case "R14W"://
                case "R14B"://

                case "R15"://
                case "R15D"://
                case "R15W"://
                case "R15B"://

                #endregion GPR
                #region SIMD

                case "MM0"://
                case "MM1"://
                case "MM2"://
                case "MM3"://
                case "MM4"://
                case "MM5"://
                case "MM6"://
                case "MM7"://

                case "XMM0"://
                case "XMM1"://
                case "XMM2"://
                case "XMM3"://
                case "XMM4"://
                case "XMM5"://
                case "XMM6"://
                case "XMM7"://

                case "XMM8"://
                case "XMM9"://
                case "XMM10"://
                case "XMM11"://
                case "XMM12"://
                case "XMM13"://
                case "XMM14"://
                case "XMM15"://

                case "YMM0"://
                case "YMM1"://
                case "YMM2"://
                case "YMM3"://
                case "YMM4"://
                case "YMM5"://
                case "YMM6"://
                case "YMM7"://

                case "YMM8"://
                case "YMM9"://
                case "YMM10"://
                case "YMM11"://
                case "YMM12"://
                case "YMM13"://
                case "YMM14"://
                case "YMM15"://

                case "ZMM0"://
                case "ZMM1"://
                case "ZMM2"://
                case "ZMM3"://
                case "ZMM4"://
                case "ZMM5"://
                case "ZMM6"://
                case "ZMM7"://

                case "ZMM8"://
                case "ZMM9"://
                case "ZMM10":
                case "ZMM11":
                case "ZMM12":
                case "ZMM13":
                case "ZMM14":
                case "ZMM15":

                case "ZMM16":
                case "ZMM17":
                case "ZMM18":
                case "ZMM19":
                case "ZMM20":
                case "ZMM21":
                case "ZMM22":
                case "ZMM23":

                case "ZMM24":
                case "ZMM25":
                case "ZMM26":
                case "ZMM27":
                case "ZMM28":
                case "ZMM29":
                case "ZMM30":
                case "ZMM31":
                    #endregion SIMD
                    return true;
                default:
                    return false;
            }
        }

        private static bool isNumber(char c) {
            switch (c) {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': return true;
                default: return false;
            }
        }

        private static bool isRegisterMethod2(string keyword) {
            int length = keyword.Length;
            string str = keyword.ToUpper();
            char c1 = str[0];
            char c2 = str[1];
            char c3 = (length > 2) ? str[2] : ' ';
            char c4 = (length > 3) ? str[3] : ' ';
            char c5 = (length > 4) ? str[4] : ' ';

            switch (length) {
                #region length2
                case 2:
                    switch (c1) {
                        case 'A': return (c2 == 'X') || (c2 == 'H') || (c2 == 'L');
                        case 'B': return (c2 == 'X') || (c2 == 'H') || (c2 == 'L') || (c2 == 'P');
                        case 'C': return (c2 == 'X') || (c2 == 'H') || (c2 == 'L');
                        case 'D': return (c2 == 'X') || (c2 == 'H') || (c2 == 'L') || (c2 == 'I');
                        case 'S': return (c2 == 'I') || (c2 == 'P');
                        case 'R': return (c2 == '8') || (c2 == '9');
                    }
                    break;
                #endregion
                #region length3
                case 3:
                    switch (c1) {
                        case 'R':
                            switch (c2) {
                                case 'A': return (c3 == 'X');
                                case 'B': return (c3 == 'X') || (c3 == 'P');
                                case 'C': return (c3 == 'X');
                                case 'D': return (c3 == 'X') || (c3 == 'I');
                                case 'S': return (c3 == 'I') || (c3 == 'P');
                                case '8': return (c3 == 'D') || (c3 == 'W') || (c3 == 'B');
                                case '9': return (c3 == 'D') || (c3 == 'W') || (c3 == 'B');
                                case '1': return (c3 == '0') || (c3 == '1') || (c3 == '2') || (c3 == '3') || (c3 == '4');
                            }
                            break;
                        case 'E':
                            switch (c2) {
                                case 'A': return (c3 == 'X');
                                case 'B': return (c3 == 'X') || (c3 == 'P');
                                case 'C': return (c3 == 'X');
                                case 'D': return (c3 == 'X') || (c3 == 'I');
                                case 'S': return (c3 == 'I') || (c3 == 'P');
                            }
                            break;
                        case 'B': return (c2 == 'P') && (c3 == 'L');
                        case 'S':
                            switch (c2) {
                                case 'P': return (c3 == 'L');
                                case 'I': return (c3 == 'L');
                            }
                            break;
                        case 'D': return (c2 == 'I') && (c3 == 'L');
                        case 'M':
                            if (c2 == 'M') {
                                switch (c3) {
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5':
                                    case '6':
                                    case '7': return true;
                                }
                            }
                            break;
                    }
                    break;
                #endregion
                #region length4
                case 4:
                    switch (c1) {
                        case 'R':
                            if (c2 == '1') {
                                switch (c3) {
                                    case '0': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                    case '1': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                    case '2': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                    case '3': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                    case '4': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                    case '5': return (c4 == 'D') || (c4 == 'W') || (c4 == 'B');
                                }
                            }
                            break;
                        case 'X':
                            if ((c2 == 'M') && (c3 == 'M')) {
                                return isNumber(c4);
                            }
                            break;
                        case 'Y':
                            if ((c2 == 'M') && (c3 == 'M')) {
                                return isNumber(c4);
                            }
                            break;
                        case 'Z':
                            if ((c2 == 'M') && (c3 == 'M')) {
                                return isNumber(c4);
                            }
                            break;
                    }
                    break;
                #endregion
                #region length5
                case 5:
                    switch (c1) {
                        case 'X':
                            if ((c2 == 'M') && (c3 == 'M') && (c4 == '1')) {
                                switch (c5) {
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5': return true;
                                }
                            }
                            break;
                        case 'Y':
                            if ((c2 == 'M') && (c3 == 'M') && (c4 == '1')) {
                                switch (c5) {
                                    case '0':
                                    case '1':
                                    case '2':
                                    case '3':
                                    case '4':
                                    case '5': return true;
                                }
                            }
                            break;
                        case 'Z':
                            if ((c2 == 'M') && (c3 == 'M')) {
                                switch (c4) {
                                    case '1': return isNumber(c5);
                                    case '2': return isNumber(c5);
                                    case '3': return ((c5 == '0') || (c5 == '1'));
                                }
                            }
                            break;
                    }
                    break;
                    #endregion
            }
            return false;
        }

        public static bool isRegister(string keyword) {
            int length = keyword.Length;
            if ((length > 5) || (length < 2)) {
                return false;
            }
            bool b2 = isRegisterMethod2(keyword);
#           if _DEBUG
            if (b2 != isRegisterMethod1(keyword)) Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: isRegister; unequal responses"));
#           endif
            return b2;
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
        /// <param name="token"></param>
        /// <returns></returns>
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
                    Rn t1 = AsmSourceTools.parseRn(y);
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
                        Rn z0r = AsmSourceTools.parseRn(z0);
                        if (z0r != Rn.NOREG) {
                            indexRn = z0r;
                            scale = parseScale(z1);
                        } else {
                            Rn z1r = AsmSourceTools.parseRn(z1);
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
                if (AsmSourceTools.nBits(baseRn) != AsmSourceTools.nBits(indexRn)) {
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
                if (char.IsWhiteSpace(c) || c.Equals('+') || c.Equals('*') || c.Equals('-') || c.Equals('[') || c.Equals(']') || c.Equals('(') || c.Equals(')')) { 
                    return i;
                }
            }
            return str.Length;
        }

        public static string getKeyword(int pos, string line) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: getKeyword; pos={0}; line=\"{1}\"", pos, new string(line)));
            var t = AsmSourceTools.getKeywordPos(pos, line);
            int beginPos = t.Item1;
            int endPos = t.Item2;
            return line.Substring(beginPos, endPos - beginPos);
        }

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
    }
}
