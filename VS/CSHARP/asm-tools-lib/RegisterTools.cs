using System;

namespace AsmTools {

    public enum RegisterType {
        UNKNOWN, BIT8, BIT16, BIT32, BIT64, MMX, XMM, YMM, ZMM, SEGMENT, MASK, CONTROL, DEBUG, BOUND
    }

    public static class RegisterTools {

        public static Tuple<bool, Rn, int> toRn(string str) {
            Rn rn = parseRn(str);
            if (rn == Rn.NOREG) {
                return new Tuple<bool, Rn, int>(false, Rn.NOREG, 0);
            } else {
                return new Tuple<bool, Rn, int>(true, rn, nBits(rn));
            }
        }

        public static Rn parseRn(string str) {
            switch (str.ToUpper()) {
                case "RAX": return Rn.RAX;
                case "EAX": return Rn.EAX;
                case "AX": return Rn.AX;
                case "AL": return Rn.AL;
                case "AH": return Rn.AH;

                case "RBX": return Rn.RBX;
                case "EBX": return Rn.EBX;
                case "BX": return Rn.BX;
                case "BL": return Rn.BL;
                case "BH": return Rn.BH;

                case "RCX": return Rn.RCX;
                case "ECX": return Rn.ECX;
                case "CX": return Rn.CX;
                case "CL": return Rn.CL;
                case "CH": return Rn.CH;

                case "RDX": return Rn.RDX;
                case "EDX": return Rn.EDX;
                case "DX": return Rn.DX;
                case "DL": return Rn.DL;
                case "DH": return Rn.DH;

                case "RSI": return Rn.RSI;
                case "ESI": return Rn.ESI;
                case "SI": return Rn.SI;
                case "SIL": return Rn.SIL;

                case "RDI": return Rn.RDI;
                case "EDI": return Rn.EDI;
                case "DI": return Rn.DI;
                case "DIL": return Rn.DIL;

                case "RBP": return Rn.RBP;
                case "EBP": return Rn.EBP;
                case "BP": return Rn.BP;
                case "BPL": return Rn.BPL;

                case "RSP": return Rn.RSP;
                case "ESP": return Rn.ESP;
                case "SP": return Rn.SP;
                case "SPL": return Rn.SPL;

                case "R8": return Rn.R8;
                case "R8D": return Rn.R8D;
                case "R8W": return Rn.R8W;
                case "R8B": return Rn.R8B;

                case "R9": return Rn.R9;
                case "R9D": return Rn.R9D;
                case "R9W": return Rn.R9W;
                case "R9B": return Rn.R9B;

                case "R10": return Rn.R10;
                case "R10D": return Rn.R10D;
                case "R10W": return Rn.R10W;
                case "R10B": return Rn.R10B;

                case "R11": return Rn.R11;
                case "R11D": return Rn.R11D;
                case "R11W": return Rn.R11W;
                case "R11B": return Rn.R11B;

                case "R12": return Rn.R12;
                case "R12D": return Rn.R12D;
                case "R12W": return Rn.R12W;
                case "R12B": return Rn.R12B;

                case "R13": return Rn.R13;
                case "R13D": return Rn.R13D;
                case "R13W": return Rn.R13W;
                case "R13B": return Rn.R13B;

                case "R14": return Rn.R14;
                case "R14D": return Rn.R14D;
                case "R14W": return Rn.R14W;
                case "R14B": return Rn.R14B;

                case "R15": return Rn.R15;
                case "R15D": return Rn.R15D;
                case "R15W": return Rn.R15W;
                case "R15B": return Rn.R15B;

                case "MM0": return Rn.MM0;
                case "MM1": return Rn.MM1;
                case "MM2": return Rn.MM2;
                case "MM3": return Rn.MM3;
                case "MM4": return Rn.MM4;
                case "MM5": return Rn.MM5;
                case "MM6": return Rn.MM6;
                case "MM7": return Rn.MM7;

                case "XMM0": return Rn.XMM0;
                case "XMM1": return Rn.XMM1;
                case "XMM2": return Rn.XMM2;
                case "XMM3": return Rn.XMM3;
                case "XMM4": return Rn.XMM4;
                case "XMM5": return Rn.XMM5;
                case "XMM6": return Rn.XMM6;
                case "XMM7": return Rn.XMM7;

                case "XMM8": return Rn.XMM8;
                case "XMM9": return Rn.XMM9;
                case "XMM10": return Rn.XMM10;
                case "XMM11": return Rn.XMM11;
                case "XMM12": return Rn.XMM12;
                case "XMM13": return Rn.XMM13;
                case "XMM14": return Rn.XMM14;
                case "XMM15": return Rn.XMM15;

                case "YMM0": return Rn.YMM0;
                case "YMM1": return Rn.YMM1;
                case "YMM2": return Rn.YMM2;
                case "YMM3": return Rn.YMM3;
                case "YMM4": return Rn.YMM4;
                case "YMM5": return Rn.YMM5;
                case "YMM6": return Rn.YMM6;
                case "YMM7": return Rn.YMM7;

                case "YMM8": return Rn.YMM8;
                case "YMM9": return Rn.YMM9;
                case "YMM10": return Rn.YMM10;
                case "YMM11": return Rn.YMM11;
                case "YMM12": return Rn.YMM12;
                case "YMM13": return Rn.YMM13;
                case "YMM14": return Rn.YMM14;
                case "YMM15": return Rn.YMM15;

                case "ZMM0": return Rn.ZMM0;
                case "ZMM1": return Rn.ZMM1;
                case "ZMM2": return Rn.ZMM2;
                case "ZMM3": return Rn.ZMM3;
                case "ZMM4": return Rn.ZMM4;
                case "ZMM5": return Rn.ZMM5;
                case "ZMM6": return Rn.ZMM6;
                case "ZMM7": return Rn.ZMM7;

                case "ZMM8": return Rn.ZMM8;
                case "ZMM9": return Rn.ZMM9;
                case "ZMM10": return Rn.ZMM10;
                case "ZMM11": return Rn.ZMM11;
                case "ZMM12": return Rn.ZMM12;
                case "ZMM13": return Rn.ZMM13;
                case "ZMM14": return Rn.ZMM14;
                case "ZMM15": return Rn.ZMM15;

                case "ZMM16": return Rn.ZMM16;
                case "ZMM17": return Rn.ZMM17;
                case "ZMM18": return Rn.ZMM18;
                case "ZMM19": return Rn.ZMM19;
                case "ZMM20": return Rn.ZMM20;
                case "ZMM21": return Rn.ZMM21;
                case "ZMM22": return Rn.ZMM22;
                case "ZMM23": return Rn.ZMM23;

                case "ZMM24": return Rn.ZMM24;
                case "ZMM25": return Rn.ZMM25;
                case "ZMM26": return Rn.ZMM26;
                case "ZMM27": return Rn.ZMM27;
                case "ZMM28": return Rn.ZMM28;
                case "ZMM29": return Rn.ZMM29;
                case "ZMM30": return Rn.ZMM30;
                case "ZMM31": return Rn.ZMM31;

                case "K0": return Rn.K0;
                case "K1": return Rn.K1;
                case "K2": return Rn.K2;
                case "K3": return Rn.K3;
                case "K4": return Rn.K4;
                case "K5": return Rn.K5;
                case "K6": return Rn.K6;
                case "K7": return Rn.K7;

                case "CS": return Rn.CS;
                case "DS": return Rn.DS;
                case "ES": return Rn.ES;
                case "SS": return Rn.SS;
                case "FS": return Rn.FS;
                case "GS": return Rn.GS;

                case "CR0": return Rn.CR0;
                case "CR1": return Rn.CR1;
                case "CR2": return Rn.CR2;
                case "CR3": return Rn.CR3;
                case "CR4": return Rn.CR4;
                case "CR5": return Rn.CR5;
                case "CR6": return Rn.CR6;
                case "CR7": return Rn.CR7;
                case "CR8": return Rn.CR8;

                case "DR0": return Rn.DR0;
                case "DR1": return Rn.DR1;
                case "DR2": return Rn.DR2;
                case "DR3": return Rn.DR3;
                case "DR4": return Rn.DR4;
                case "DR5": return Rn.DR5;
                case "DR6": return Rn.DR6;
                case "DR7": return Rn.DR7;

                case "BND0": return Rn.BND0;
                case "BND1": return Rn.BND1;
                case "BND2": return Rn.BND2;
                case "BND3": return Rn.BND3;

                default:
                    return Rn.NOREG;
            }
        }

        public static int nBits(Rn rn) {
            switch (rn) {
                case Rn.RAX:
                case Rn.RBX:
                case Rn.RCX:
                case Rn.RDX:
                case Rn.RSI:
                case Rn.RDI:
                case Rn.RBP:
                case Rn.RSP:
                case Rn.R8:
                case Rn.R9:
                case Rn.R10:
                case Rn.R11:
                case Rn.R12:
                case Rn.R13:
                case Rn.R14:
                case Rn.R15:
                    return 64;

                case Rn.EAX:
                case Rn.EBX:
                case Rn.ECX:
                case Rn.EDX:
                case Rn.ESI:
                case Rn.EDI:
                case Rn.EBP:
                case Rn.ESP:
                case Rn.R8D:
                case Rn.R9D:
                case Rn.R10D:
                case Rn.R11D:
                case Rn.R12D:
                case Rn.R13D:
                case Rn.R14D:
                case Rn.R15D:
                    return 32;

                case Rn.AX:
                case Rn.BX:
                case Rn.CX:
                case Rn.DX:
                case Rn.SI:
                case Rn.DI:
                case Rn.BP:
                case Rn.SP:
                case Rn.R8W:
                case Rn.R9W:
                case Rn.R10W:
                case Rn.R11W:
                case Rn.R12W:
                case Rn.R13W:
                case Rn.R14W:
                case Rn.R15W:
                    return 16;

                case Rn.AL:
                case Rn.BL:
                case Rn.CL:
                case Rn.DL:
                case Rn.AH:
                case Rn.BH:
                case Rn.CH:
                case Rn.DH:
                case Rn.SIL:
                case Rn.DIL:
                case Rn.BPL:
                case Rn.SPL:
                case Rn.R8B:
                case Rn.R9B:
                case Rn.R10B:
                case Rn.R11B:
                case Rn.R12B:
                case Rn.R13B:
                case Rn.R14B:
                case Rn.R15B:
                    return 8;

                case Rn.MM0:
                case Rn.MM1:
                case Rn.MM2:
                case Rn.MM3:
                case Rn.MM4:
                case Rn.MM5:
                case Rn.MM6:
                case Rn.MM7:
                    return 64;

                case Rn.XMM0:
                case Rn.XMM1:
                case Rn.XMM2:
                case Rn.XMM3:
                case Rn.XMM4:
                case Rn.XMM5:
                case Rn.XMM6:
                case Rn.XMM7:
                case Rn.XMM8:
                case Rn.XMM9:
                case Rn.XMM10:
                case Rn.XMM11:
                case Rn.XMM12:
                case Rn.XMM13:
                case Rn.XMM14:
                case Rn.XMM15:
                    return 128;

                case Rn.YMM0:
                case Rn.YMM1:
                case Rn.YMM2:
                case Rn.YMM3:
                case Rn.YMM4:
                case Rn.YMM5:
                case Rn.YMM6:
                case Rn.YMM7:
                case Rn.YMM8:
                case Rn.YMM9:
                case Rn.YMM10:
                case Rn.YMM11:
                case Rn.YMM12:
                case Rn.YMM13:
                case Rn.YMM14:
                case Rn.YMM15:
                    return 256;

                case Rn.ZMM0:
                case Rn.ZMM1:
                case Rn.ZMM2:
                case Rn.ZMM3:
                case Rn.ZMM4:
                case Rn.ZMM5:
                case Rn.ZMM6:
                case Rn.ZMM7:
                case Rn.ZMM8:
                case Rn.ZMM9:
                case Rn.ZMM10:
                case Rn.ZMM11:
                case Rn.ZMM12:
                case Rn.ZMM13:
                case Rn.ZMM14:
                case Rn.ZMM15:

                case Rn.ZMM16:
                case Rn.ZMM17:
                case Rn.ZMM18:
                case Rn.ZMM19:
                case Rn.ZMM20:
                case Rn.ZMM21:
                case Rn.ZMM22:
                case Rn.ZMM23:
                case Rn.ZMM24:
                case Rn.ZMM25:
                case Rn.ZMM26:
                case Rn.ZMM27:
                case Rn.ZMM28:
                case Rn.ZMM29:
                case Rn.ZMM30:
                case Rn.ZMM31:
                    return 512;
            }
            return 0;
        }

        /// <summary>
        /// return regular pattern to select the provided register and aliased register names
        /// </summary>
        /// <param name="reg"></param>
        /// <returns></returns>
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

                case "XMM0":
                case "YMM0":
                case "ZMM0":
                    return "\\b(XMM0|YMM0|ZMM0)\\b";

                case "XMM1":
                case "YMM1":
                case "ZMM1":
                    return "\\b(XMM1|YMM1|ZMM1)\\b";
                case "XMM2":
                case "YMM2":
                case "ZMM2":
                    return "\\b(XMM2|YMM2|ZMM2)\\b";
                case "XMM3":
                case "YMM3":
                case "ZMM3":
                    return "\\b(XMM3|YMM3|ZMM3)\\b";
                case "XMM4":
                case "YMM4":
                case "ZMM4":
                    return "\\b(XMM4|YMM4|ZMM4)\\b";
                case "XMM5":
                case "YMM5":
                case "ZMM5":
                    return "\\b(XMM5|YMM5|ZMM5)\\b";
                case "XMM6":
                case "YMM6":
                case "ZMM6":
                    return "\\b(XMM6|YMM6|ZMM6)\\b";
                case "XMM7":
                case "YMM7":
                case "ZMM7":
                    return "\\b(XMM7|YMM7|ZMM7)\\b";
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

        public static bool isRegister(string keyword) {
            return RegisterTools.parseRn(keyword) != Rn.NOREG;
        }

        public static RegisterType getRegisterType(Rn rn) {
            switch (rn) {
                case Rn.NOREG:
                    return RegisterType.UNKNOWN;

                case Rn.AL:
                case Rn.AH:
                case Rn.BL:
                case Rn.BH:
                case Rn.CL:
                case Rn.CH:
                case Rn.DL:
                case Rn.DH:
                case Rn.SIL:
                case Rn.DIL:
                case Rn.BPL:
                case Rn.SPL:
                case Rn.R8B:
                case Rn.R9B:
                case Rn.R10B:
                case Rn.R11B:
                case Rn.R12B:
                case Rn.R13B:
                case Rn.R14B:
                case Rn.R15B:
                    return RegisterType.BIT8;

                case Rn.AX:
                case Rn.BX:
                case Rn.CX:
                case Rn.DX:
                case Rn.SI:
                case Rn.DI:
                case Rn.BP:
                case Rn.SP:
                case Rn.R8W:
                case Rn.R9W:
                case Rn.R10W:
                case Rn.R11W:
                case Rn.R12W:
                case Rn.R13W:
                case Rn.R14W:
                case Rn.R15W:
                    return RegisterType.BIT16;

                case Rn.EAX:
                case Rn.EBX:
                case Rn.ECX:
                case Rn.EDX:
                case Rn.ESI:
                case Rn.EDI:
                case Rn.EBP:
                case Rn.ESP:
                case Rn.R8D:
                case Rn.R9D:
                case Rn.R10D:
                case Rn.R11D:
                case Rn.R12D:
                case Rn.R13D:
                case Rn.R14D:
                case Rn.R15D:
                    return RegisterType.BIT32;

                case Rn.RAX:
                case Rn.RBX:
                case Rn.RCX:
                case Rn.RDX:
                case Rn.RSI:
                case Rn.RDI:
                case Rn.RBP:
                case Rn.RSP:
                case Rn.R8:
                case Rn.R9:
                case Rn.R10:
                case Rn.R11:
                case Rn.R12:
                case Rn.R13:
                case Rn.R14:
                case Rn.R15:
                    return RegisterType.BIT64;

                case Rn.MM0:
                case Rn.MM1:
                case Rn.MM2:
                case Rn.MM3:
                case Rn.MM4:
                case Rn.MM5:
                case Rn.MM6:
                case Rn.MM7:
                    return RegisterType.MMX;

                case Rn.XMM0:
                case Rn.XMM1:
                case Rn.XMM2:
                case Rn.XMM3:
                case Rn.XMM4:
                case Rn.XMM5:
                case Rn.XMM6:
                case Rn.XMM7:
                case Rn.XMM8:
                case Rn.XMM9:
                case Rn.XMM10:
                case Rn.XMM11:
                case Rn.XMM12:
                case Rn.XMM13:
                case Rn.XMM14:
                case Rn.XMM15:
                    return RegisterType.XMM;

                case Rn.YMM0:
                case Rn.YMM1:
                case Rn.YMM2:
                case Rn.YMM3:
                case Rn.YMM4:
                case Rn.YMM5:
                case Rn.YMM6:
                case Rn.YMM7:
                case Rn.YMM8:
                case Rn.YMM9:
                case Rn.YMM10:
                case Rn.YMM11:
                case Rn.YMM12:
                case Rn.YMM13:
                case Rn.YMM14:
                case Rn.YMM15:
                    return RegisterType.YMM;

                case Rn.ZMM0:
                case Rn.ZMM1:
                case Rn.ZMM2:
                case Rn.ZMM3:
                case Rn.ZMM4:
                case Rn.ZMM5:
                case Rn.ZMM6:
                case Rn.ZMM7:
                case Rn.ZMM8:
                case Rn.ZMM9:
                case Rn.ZMM10:
                case Rn.ZMM11:
                case Rn.ZMM12:
                case Rn.ZMM13:
                case Rn.ZMM14:
                case Rn.ZMM15:
                case Rn.ZMM16:
                case Rn.ZMM17:
                case Rn.ZMM18:
                case Rn.ZMM19:
                case Rn.ZMM20:
                case Rn.ZMM21:
                case Rn.ZMM22:
                case Rn.ZMM23:
                case Rn.ZMM24:
                case Rn.ZMM25:
                case Rn.ZMM26:
                case Rn.ZMM27:
                case Rn.ZMM28:
                case Rn.ZMM29:
                case Rn.ZMM30:
                case Rn.ZMM31:
                    return RegisterType.ZMM;

                case Rn.CS:
                case Rn.DS:
                case Rn.ES:
                case Rn.SS:
                case Rn.FS:
                case Rn.GS:
                    return RegisterType.SEGMENT;

                case Rn.CR0:
                case Rn.CR1:
                case Rn.CR2:
                case Rn.CR3:
                case Rn.CR4:
                case Rn.CR5:
                case Rn.CR6:
                case Rn.CR7:
                case Rn.CR8:
                    return RegisterType.CONTROL;

                case Rn.DR0:
                case Rn.DR1:
                case Rn.DR2:
                case Rn.DR3:
                case Rn.DR4:
                case Rn.DR5:
                case Rn.DR6:
                case Rn.DR7:
                    return RegisterType.DEBUG;

                case Rn.K0:
                case Rn.K1:
                case Rn.K2:
                case Rn.K3:
                case Rn.K4:
                case Rn.K5:
                case Rn.K6:
                case Rn.K7:
                    return RegisterType.MASK;

                case Rn.BND0:
                case Rn.BND1:
                case Rn.BND2:
                case Rn.BND3:
                    return RegisterType.BOUND;

                default:
                    break;

            }
            return RegisterType.UNKNOWN;
        }

        public static bool isOpmaskRegister(Rn rn) {
            switch (rn) {
                case Rn.K0:
                case Rn.K1:
                case Rn.K2:
                case Rn.K3:
                case Rn.K4:
                case Rn.K5:
                case Rn.K6:
                case Rn.K7:
                    return true;
                default: return false;
            }
        }
        public static bool isBoundRegister(Rn rn) {
            switch (rn) {
                case Rn.BND0:
                case Rn.BND1:
                case Rn.BND2:
                case Rn.BND3:
                    return true;
                default: return false;
            }
        }
        public static bool isControlRegister(Rn rn) {
            switch (rn) {
                case Rn.CR0:
                case Rn.CR1:
                case Rn.CR2:
                case Rn.CR3:
                case Rn.CR4:
                case Rn.CR5:
                case Rn.CR6:
                case Rn.CR7:
                case Rn.CR8:  return true;
                default:
                    return false;
            }
        }
        public static bool isDebugRegister(Rn rn) {
            switch (rn) {
                case Rn.DR0:
                case Rn.DR1:
                case Rn.DR2:
                case Rn.DR3:
                case Rn.DR4:
                case Rn.DR5:
                case Rn.DR6:
                case Rn.DR7: return true;
                default:
                    return false;
            }
        }
        public static bool isSegmentRegister(Rn rn) {
            switch (rn) {
                case Rn.CS:
                case Rn.DS:
                case Rn.ES:
                case Rn.SS:
                case Rn.FS:
                case Rn.GS: return true;
                default:
                    return false;
            }
        }
        public static bool isGeneralPurposeRegister(Rn rn) {
            switch (rn) {
                case Rn.RAX:
                case Rn.EAX:
                case Rn.AX:
                case Rn.AL:
                case Rn.AH:
                case Rn.RBX:
                case Rn.EBX:
                case Rn.BX:
                case Rn.BL:
                case Rn.BH:
                case Rn.RCX:
                case Rn.ECX:
                case Rn.CX:
                case Rn.CL:
                case Rn.CH:
                case Rn.RDX:
                case Rn.EDX:
                case Rn.DX:
                case Rn.DL:
                case Rn.DH:
                case Rn.RSI:
                case Rn.ESI:
                case Rn.SI:
                case Rn.SIL:
                case Rn.RDI:
                case Rn.EDI:
                case Rn.DI:
                case Rn.DIL:
                case Rn.RBP:
                case Rn.EBP:
                case Rn.BP:
                case Rn.BPL:
                case Rn.RSP:
                case Rn.ESP:
                case Rn.SP:
                case Rn.SPL:
                case Rn.R8:
                case Rn.R8D:
                case Rn.R8W:
                case Rn.R8B:
                case Rn.R9:
                case Rn.R9D:
                case Rn.R9W:
                case Rn.R9B:
                case Rn.R10:
                case Rn.R10D:
                case Rn.R10W:
                case Rn.R10B:
                case Rn.R11:
                case Rn.R11D:
                case Rn.R11W:
                case Rn.R11B:
                case Rn.R12:
                case Rn.R12D:
                case Rn.R12W:
                case Rn.R12B:
                case Rn.R13:
                case Rn.R13D:
                case Rn.R13W:
                case Rn.R13B:
                case Rn.R14:
                case Rn.R14D:
                case Rn.R14W:
                case Rn.R14B:
                case Rn.R15:
                case Rn.R15D:
                case Rn.R15W:
                case Rn.R15B:
                    return true;
                default: return false;
            }
        }
        public static bool isMmxRegister(Rn rn) {
            switch (rn) {
                case Rn.MM0:
                case Rn.MM1:
                case Rn.MM2:
                case Rn.MM3:
                case Rn.MM4:
                case Rn.MM5:
                case Rn.MM6:
                case Rn.MM7: return true;
                default: return false;
            }
        }
        public static bool isSseRegister(Rn rn) {
            switch (rn) {
                case Rn.XMM0:
                case Rn.XMM1:
                case Rn.XMM2:
                case Rn.XMM3:
                case Rn.XMM4:
                case Rn.XMM5:
                case Rn.XMM6:
                case Rn.XMM7:
                case Rn.XMM8:
                case Rn.XMM9:
                case Rn.XMM10:
                case Rn.XMM11:
                case Rn.XMM12:
                case Rn.XMM13:
                case Rn.XMM14:
                case Rn.XMM15: return true;
                default: return false;
            }
        }
        public static bool isAvxRegister(Rn rn) {
            switch (rn) {
                case Rn.YMM0:
                case Rn.YMM1:
                case Rn.YMM2:
                case Rn.YMM3:
                case Rn.YMM4:
                case Rn.YMM5:
                case Rn.YMM6:
                case Rn.YMM7:
                case Rn.YMM8:
                case Rn.YMM9:
                case Rn.YMM10:
                case Rn.YMM11:
                case Rn.YMM12:
                case Rn.YMM13:
                case Rn.YMM14:
                case Rn.YMM15: return true;
                default: return false;
            }
        }
        public static bool isAvx512Register(Rn rn) {
            switch (rn) {
                case Rn.ZMM0:
                case Rn.ZMM1:
                case Rn.ZMM2:
                case Rn.ZMM3:
                case Rn.ZMM4:
                case Rn.ZMM5:
                case Rn.ZMM6:
                case Rn.ZMM7:
                case Rn.ZMM8:
                case Rn.ZMM9:
                case Rn.ZMM10:
                case Rn.ZMM11:
                case Rn.ZMM12:
                case Rn.ZMM13:
                case Rn.ZMM14:
                case Rn.ZMM15:
                case Rn.ZMM16:
                case Rn.ZMM17:
                case Rn.ZMM18:
                case Rn.ZMM19:
                case Rn.ZMM20:
                case Rn.ZMM21:
                case Rn.ZMM22:
                case Rn.ZMM23:
                case Rn.ZMM24:
                case Rn.ZMM25:
                case Rn.ZMM26:
                case Rn.ZMM27:
                case Rn.ZMM28:
                case Rn.ZMM29:
                case Rn.ZMM30:
                case Rn.ZMM31: return true;
                default: return false;
            }
        }
    }
}
