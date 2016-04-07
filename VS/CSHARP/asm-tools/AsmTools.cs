using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {

    public static class Tools {

        public static bool isRemarkChar(char c) {
            return c.Equals('#') || c.Equals(';');
        }

        public static bool isSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']') || c.Equals('+') || c.Equals('-') || c.Equals('*') || c.Equals(':');
        }

        public static Tuple<bool, int, int> getRemarkPos(string line) {
            int nChars = line.Length;
            for (int i = 0; i < nChars; ++i) {
                if (Tools.isRemarkChar(line[i])) {
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
                if (Tools.isRemarkChar(c)) {
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
                } else if (Tools.isRemarkChar(c)) {
                    return new Tuple<bool, int, int>(false, 0, 0);
                } else if (Tools.isSeparatorChar(c)) {
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
                parsedSuccessfully = ulong.TryParse(token2.Replace(".", string.Empty), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out v);
            } else {
                parsedSuccessfully = ulong.TryParse(token2.Replace(".", string.Empty), NumberStyles.Integer, CultureInfo.CurrentCulture, out v);
            }

            int nBits = -1;
            if (parsedSuccessfully) {
                if ((v & 0xFFFFFFFFFFFFFF00ul) == 0) {
                    nBits = 8;
                } else if ((v & 0xFFFFFFFFFFFF0000ul) == 0) {
                    nBits = 16;
                } else if ((v & 0xFFFFFFFF00000000ul) == 0) {
                    nBits = 32;
                } else {
                    nBits = 64;
                }
            }
            return new Tuple<bool, ulong, int>(parsedSuccessfully, v, nBits);
        }


        public static string getRelatedRegister(string reg) {
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

                default: return reg;
            }
        }

        private static bool isRegisterMethod1(string keyword) {
            //TODO  get this info from AsmDudeData.xml
            switch (keyword.ToUpper()) {
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
                #region

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
                    #endregion
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

        public static string getKeyword(int pos, string line) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: getKeyword; pos={0}; line=\"{1}\"", pos, new string(line)));
            var t = Tools.getKeywordPos(pos, line);
            int beginPos = t.Item1;
            int endPos = t.Item2;
            return line.Substring(beginPos, endPos - beginPos);
        }

        public static Tuple<int, int> getKeywordPos(int pos, string line) {
            //Debug.WriteLine(string.Format("INFO: getKeyword; pos={0}; line=\"{1}\"", pos, new string(line)));
            if ((pos < 0) || (pos >= line.Length)) return null;

            // find the beginning of the keyword
            int beginPos = 0;
            for (int i1 = pos - 1; i1 > 0; --i1) {
                char c = line[i1];
                if (Tools.isSeparatorChar(c) || Char.IsControl(c) || Tools.isRemarkChar(c)) {
                    beginPos = i1 + 1;
                    break;
                }
            }
            // find the end of the keyword
            int endPos = line.Length;
            for (int i2 = pos; i2 < line.Length; ++i2) {
                char c = line[i2];
                if (Tools.isSeparatorChar(c) || Char.IsControl(c) || Tools.isRemarkChar(c)) {
                    endPos = i2;
                    break;
                }
            }
            return new Tuple<int, int>(beginPos, endPos);
        }

        public static Mnemonic parseMnemonic(string str) {
            switch (str.ToUpper()) {
                case "UNKNOWN": return Mnemonic.UNKNOWN;
                case "MOV": return Mnemonic.MOV;
                case "CMOVE": return Mnemonic.CMOVE;
                case "CMOVZ": return Mnemonic.CMOVZ;
                case "CMOVNE": return Mnemonic.CMOVNE;
                case "CMOVNZ": return Mnemonic.CMOVNZ;
                case "CMOVA": return Mnemonic.CMOVA;
                case "CMOVNBE": return Mnemonic.CMOVNBE;
                case "CMOVAE": return Mnemonic.CMOVAE;
                case "CMOVNB": return Mnemonic.CMOVNB;
                case "CMOVB": return Mnemonic.CMOVB;
                case "CMOVNAE": return Mnemonic.CMOVNAE;
                case "CMOVBE": return Mnemonic.CMOVBE;
                case "CMOVNA": return Mnemonic.CMOVNA;
                case "CMOVG": return Mnemonic.CMOVG;
                case "CMOVNLE": return Mnemonic.CMOVNLE;
                case "CMOVGE": return Mnemonic.CMOVGE;
                case "CMOVNL": return Mnemonic.CMOVNL;
                case "CMOVL": return Mnemonic.CMOVL;
                case "CMOVNGE": return Mnemonic.CMOVNGE;
                case "CMOVLE": return Mnemonic.CMOVLE;
                case "CMOVNG": return Mnemonic.CMOVNG;
                case "CMOVC": return Mnemonic.CMOVC;
                case "CMOVNC": return Mnemonic.CMOVNC;
                case "CMOVO": return Mnemonic.CMOVO;
                case "CMOVNO": return Mnemonic.CMOVNO;
                case "CMOVS": return Mnemonic.CMOVS;
                case "CMOVNS": return Mnemonic.CMOVNS;
                case "CMOVP": return Mnemonic.CMOVP;
                case "CMOVPE": return Mnemonic.CMOVPE;
                case "CMOVNP": return Mnemonic.CMOVNP;
                case "CMOVPO": return Mnemonic.CMOVPO;
                case "XCHG": return Mnemonic.XCHG;
                case "BSWAP": return Mnemonic.BSWAP;
                case "XADD": return Mnemonic.XADD;
                case "CMPXCHG": return Mnemonic.CMPXCHG;
                case "CMPXCHG8B": return Mnemonic.CMPXCHG8B;
                case "PUSH": return Mnemonic.PUSH;
                case "POP": return Mnemonic.POP;
                case "PUSHA": return Mnemonic.PUSHA;
                case "PUSHAD": return Mnemonic.PUSHAD;
                case "POPA": return Mnemonic.POPA;
                case "POPAD": return Mnemonic.POPAD;
                case "CWD": return Mnemonic.CWD;
                case "CDQ": return Mnemonic.CDQ;
                case "CBW": return Mnemonic.CBW;
                case "CWDE": return Mnemonic.CWDE;
                case "MOVSX": return Mnemonic.MOVSX;
                case "MOVZX": return Mnemonic.MOVZX;
                case "ADCX": return Mnemonic.ADCX;
                case "ADOX": return Mnemonic.ADOX;
                case "ADD": return Mnemonic.ADD;
                case "ADC": return Mnemonic.ADC;
                case "SUB": return Mnemonic.SUB;
                case "SBB": return Mnemonic.SBB;
                case "IMUL": return Mnemonic.IMUL;
                case "MUL": return Mnemonic.MUL;
                case "IDIV": return Mnemonic.IDIV;
                case "DIV": return Mnemonic.DIV;
                case "INC": return Mnemonic.INC;
                case "DEC": return Mnemonic.DEC;
                case "NEG": return Mnemonic.NEG;
                case "CMP": return Mnemonic.CMP;
                case "DAA": return Mnemonic.DAA;
                case "DAS": return Mnemonic.DAS;
                case "AAA": return Mnemonic.AAA;
                case "AAS": return Mnemonic.AAS;
                case "AAM": return Mnemonic.AAM;
                case "AAD": return Mnemonic.AAD;
                case "AND": return Mnemonic.AND;
                case "OR": return Mnemonic.OR;
                case "XOR": return Mnemonic.XOR;
                case "NOT": return Mnemonic.NOT;
                case "SAR": return Mnemonic.SAR;
                case "SHR": return Mnemonic.SHR;
                case "SAL": return Mnemonic.SAL;
                case "SHL": return Mnemonic.SHL;
                case "SHRD": return Mnemonic.SHRD;
                case "SHLD": return Mnemonic.SHLD;
                case "ROR": return Mnemonic.ROR;
                case "ROL": return Mnemonic.ROL;
                case "RCR": return Mnemonic.RCR;
                case "RCL": return Mnemonic.RCL;
                case "BT": return Mnemonic.BT;
                case "BTS": return Mnemonic.BTS;
                case "BTR": return Mnemonic.BTR;
                case "BTC": return Mnemonic.BTC;
                case "BSF": return Mnemonic.BSF;
                case "BSR": return Mnemonic.BSR;
                case "SETE": return Mnemonic.SETE;
                case "SETZ": return Mnemonic.SETZ;
                case "SETNE": return Mnemonic.SETNE;
                case "SETNZ": return Mnemonic.SETNZ;
                case "SETA": return Mnemonic.SETA;
                case "SETNBE": return Mnemonic.SETNBE;
                case "SETAE": return Mnemonic.SETAE;
                case "SETNB": return Mnemonic.SETNB;
                case "SETNC": return Mnemonic.SETNC;
                case "SETB": return Mnemonic.SETB;
                case "SETNAE": return Mnemonic.SETNAE;
                case "SETC": return Mnemonic.SETC;
                case "SETBE": return Mnemonic.SETBE;
                case "SETNA": return Mnemonic.SETNA;
                case "SETG": return Mnemonic.SETG;
                case "SETNLE": return Mnemonic.SETNLE;
                case "SETGE": return Mnemonic.SETGE;
                case "SETNL": return Mnemonic.SETNL;
                case "SETL": return Mnemonic.SETL;
                case "SETNGE": return Mnemonic.SETNGE;
                case "SETLE": return Mnemonic.SETLE;
                case "SETNG": return Mnemonic.SETNG;
                case "SETS": return Mnemonic.SETS;
                case "SETNS": return Mnemonic.SETNS;
                case "SETO": return Mnemonic.SETO;
                case "SETNO": return Mnemonic.SETNO;
                case "SETPE": return Mnemonic.SETPE;
                case "SETP": return Mnemonic.SETP;
                case "SETPO": return Mnemonic.SETPO;
                case "SETNP": return Mnemonic.SETNP;
                case "TEST": return Mnemonic.TEST;
                case "CRC32": return Mnemonic.CRC32;
                case "POPCNT": return Mnemonic.POPCNT;
                case "JMP": return Mnemonic.JMP;
                case "JE": return Mnemonic.JE;
                case "JZ": return Mnemonic.JZ;
                case "JNE": return Mnemonic.JNE;
                case "JNZ": return Mnemonic.JNZ;
                case "JA": return Mnemonic.JA;
                case "JNBE": return Mnemonic.JNBE;
                case "JAE": return Mnemonic.JAE;
                case "JNB": return Mnemonic.JNB;
                case "JB": return Mnemonic.JB;
                case "JNAE": return Mnemonic.JNAE;
                case "JBE": return Mnemonic.JBE;
                case "JNA": return Mnemonic.JNA;
                case "JG": return Mnemonic.JG;
                case "JNLE": return Mnemonic.JNLE;
                case "JGE": return Mnemonic.JGE;
                case "JNL": return Mnemonic.JNL;
                case "JL": return Mnemonic.JL;
                case "JNGE": return Mnemonic.JNGE;
                case "JLE": return Mnemonic.JLE;
                case "JNG": return Mnemonic.JNG;
                case "JC": return Mnemonic.JC;
                case "JNC": return Mnemonic.JNC;
                case "JO": return Mnemonic.JO;
                case "JNO": return Mnemonic.JNO;
                case "JS": return Mnemonic.JS;
                case "JNS": return Mnemonic.JNS;
                case "JPO": return Mnemonic.JPO;
                case "JNP": return Mnemonic.JNP;
                case "JPE": return Mnemonic.JPE;
                case "JP": return Mnemonic.JP;
                case "JCXZ": return Mnemonic.JCXZ;
                case "JECXZ": return Mnemonic.JECXZ;
                case "JRCXZ": return Mnemonic.JRCXZ;
                case "LOOP": return Mnemonic.LOOP;
                case "LOOPZ": return Mnemonic.LOOPZ;
                case "LOOPE": return Mnemonic.LOOPE;
                case "LOOPNZ": return Mnemonic.LOOPNZ;
                case "LOOPNE": return Mnemonic.LOOPNE;
                case "CALL": return Mnemonic.CALL;
                case "RET": return Mnemonic.RET;
                case "IRET": return Mnemonic.IRET;
                case "INT": return Mnemonic.INT;
                case "INTO": return Mnemonic.INTO;
                case "BOUND": return Mnemonic.BOUND;
                case "ENTER": return Mnemonic.ENTER;
                case "LEAVE": return Mnemonic.LEAVE;
                case "MOVS": return Mnemonic.MOVS;
                case "MOVSB": return Mnemonic.MOVSB;
                case "MOVSW": return Mnemonic.MOVSW;
                case "MOVSD": return Mnemonic.MOVSD;
                case "CMPS": return Mnemonic.CMPS;
                case "CMPSB": return Mnemonic.CMPSB;
                case "CMPSW": return Mnemonic.CMPSW;
                case "CMPSD": return Mnemonic.CMPSD;
                case "SCAS": return Mnemonic.SCAS;
                case "SCASB": return Mnemonic.SCASB;
                case "SCASW": return Mnemonic.SCASW;
                case "SCASD": return Mnemonic.SCASD;
                case "LODS": return Mnemonic.LODS;
                case "LODSB": return Mnemonic.LODSB;
                case "LODSW": return Mnemonic.LODSW;
                case "LODSD": return Mnemonic.LODSD;
                case "STOS": return Mnemonic.STOS;
                case "STOSB": return Mnemonic.STOSB;
                case "STOSW": return Mnemonic.STOSW;
                case "STOSD": return Mnemonic.STOSD;
                case "REP": return Mnemonic.REP;
                case "REPE": return Mnemonic.REPE;
                case "REPZ": return Mnemonic.REPZ;
                case "REPNE": return Mnemonic.REPNE;
                case "REPNZ": return Mnemonic.REPNZ;
                case "OUT": return Mnemonic.OUT;
                case "INS": return Mnemonic.INS;
                case "INSB": return Mnemonic.INSB;
                case "INSW": return Mnemonic.INSW;
                case "INSD": return Mnemonic.INSD;
                case "OUTS": return Mnemonic.OUTS;
                case "OUTSB": return Mnemonic.OUTSB;
                case "OUTSW": return Mnemonic.OUTSW;
                case "OUTSD": return Mnemonic.OUTSD;
                case "STC": return Mnemonic.STC;
                case "CLC": return Mnemonic.CLC;
                case "CMC": return Mnemonic.CMC;
                case "CLD": return Mnemonic.CLD;
                case "STD": return Mnemonic.STD;
                case "LAHF": return Mnemonic.LAHF;
                case "SAHF": return Mnemonic.SAHF;
                case "PUSHF": return Mnemonic.PUSHF;
                case "PUSHFD": return Mnemonic.PUSHFD;
                case "POPF": return Mnemonic.POPF;
                case "POPFD": return Mnemonic.POPFD;
                case "STI": return Mnemonic.STI;
                case "CLI": return Mnemonic.CLI;
                case "LDS": return Mnemonic.LDS;
                case "LES": return Mnemonic.LES;
                case "LFS": return Mnemonic.LFS;
                case "LGS": return Mnemonic.LGS;
                case "LSS": return Mnemonic.LSS;
                case "LEA": return Mnemonic.LEA;
                case "NOP": return Mnemonic.NOP;
                case "UD2": return Mnemonic.UD2;
                case "XLAT": return Mnemonic.XLAT;
                case "XLATB": return Mnemonic.XLATB;
                case "CPUID": return Mnemonic.CPUID;
                case "MOVBE": return Mnemonic.MOVBE;
                case "PREFETCHW": return Mnemonic.PREFETCHW;
                case "PREFETCHWT1": return Mnemonic.PREFETCHWT1;
                case "CLFLUSH": return Mnemonic.CLFLUSH;
                case "CLFLUSHOPT": return Mnemonic.CLFLUSHOPT;
                case "XSAVE": return Mnemonic.XSAVE;
                case "XSAVEC": return Mnemonic.XSAVEC;
                case "XSAVEOPT": return Mnemonic.XSAVEOPT;
                case "XRSTOR": return Mnemonic.XRSTOR;
                case "XGETBV": return Mnemonic.XGETBV;
                case "RDRAND": return Mnemonic.RDRAND;
                case "RDSEED": return Mnemonic.RDSEED;
                case "ANDN": return Mnemonic.ANDN;
                case "BEXTR": return Mnemonic.BEXTR;
                case "BLSI": return Mnemonic.BLSI;
                case "BLSMSK": return Mnemonic.BLSMSK;
                case "BLSR": return Mnemonic.BLSR;
                case "BZHI": return Mnemonic.BZHI;
                case "LZCNT": return Mnemonic.LZCNT;
                case "MULX": return Mnemonic.MULX;
                case "PDEP": return Mnemonic.PDEP;
                case "PEXT": return Mnemonic.PEXT;
                case "RORX": return Mnemonic.RORX;
                case "SARX": return Mnemonic.SARX;
                case "SHLX": return Mnemonic.SHLX;
                case "SHRX": return Mnemonic.SHRX;
                case "TZCNT": return Mnemonic.TZCNT;
                default:
                    Console.WriteLine("WARNING;parseMnemonic. unknown str=\"" + str + "\".");
                    return Mnemonic.UNKNOWN;
            }
        }

    }
}
