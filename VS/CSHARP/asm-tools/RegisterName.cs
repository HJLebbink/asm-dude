using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {

    /// <summary>
    /// Register Name
    /// </summary>
    public enum Rn {
        NOREG,
        rax, eax, ax, al, ah,
        rbx, ebx, bx, bl, bh,
        rcx, ecx, cx, cl, ch,
        rdx, edx, dx, dl, dh,

        rsi, esi, si, sil,
        rdi, edi, di, dil,
        rbp, ebp, bp, bpl,
        rsp, esp, sp, spl,

        R8, R8D, R8W, R8B,
        R9, R9D, R9W, R9B,
        R10, R10D, R10W, R10B,
        R11, R11D, R11W, R11B,

        R12, R12D, R12W, R12B,
        R13, R13D, R13W, R13B,
        R14, R14D, R14W, R14B,
        R15, R15D, R15W, R15B,

        MM0, MM1, MM2, MM3, MM4, MM5, MM6, MM7,
        XMM0, XMM1, XMM2, XMM3, XMM4, XMM5, XMM6, XMM7,
        XMM8, XMM9, XMM10, XMM11, XMM12, XMM13, XMM14, XMM15,

        YMM0, YMM1, YMM2, YMM3, YMM4, YMM5, YMM6, YMM7,
        YMM8, YMM9, YMM10, YMM11, YMM12, YMM13, YMM14, YMM15,

        ZMM0, ZMM1, ZMM2, ZMM3, ZMM4, ZMM5, ZMM6, ZMM7,
        ZMM8, ZMM9, ZMM10, ZMM11, ZMM12, ZMM13, ZMM14, ZMM15,
        ZMM16, ZMM17, ZMM18, ZMM19, ZMM20, ZMM21, ZMM22, ZMM23,
        ZMM24, ZMM25, ZMM26, ZMM27, ZMM28, ZMM29, ZMM30, ZMM31
    };

    public static partial class AsmSourceTools {

        public static Rn parseRn(string str) {
            switch (str.ToUpper()) {
                case "RAX": return Rn.rax;
                case "EAX": return Rn.eax;
                case "AX": return Rn.ax;
                case "AL": return Rn.al;
                case "AH": return Rn.ah;

                case "RBX": return Rn.rbx;
                case "EBX": return Rn.ebx;
                case "BX": return Rn.bx;
                case "BL": return Rn.bl;
                case "BH": return Rn.bh;

                case "RCX": return Rn.rcx;
                case "ECX": return Rn.ecx;
                case "CX": return Rn.cx;
                case "CL": return Rn.cl;
                case "CH": return Rn.ch;

                case "RDX": return Rn.rdx;
                case "EDX": return Rn.edx;
                case "DX": return Rn.dx;
                case "DL": return Rn.dl;
                case "DH": return Rn.dh;

                case "RSI": return Rn.rsi;
                case "ESI": return Rn.esi;
                case "SI": return Rn.si;
                case "SIL": return Rn.sil;

                case "RDI": return Rn.rdi;
                case "EDI": return Rn.edi;
                case "DI": return Rn.di;
                case "DIL": return Rn.dil;

                case "RBP": return Rn.rbp;
                case "EBP": return Rn.ebp;
                case "BP": return Rn.bp;
                case "BPL": return Rn.bpl;

                case "RSP": return Rn.rsp;
                case "ESP": return Rn.esp;
                case "SP": return Rn.sp;
                case "SPL": return Rn.spl;

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
                case "ZMM31":
                    return Rn.ZMM31;
                default:
                    return Rn.NOREG;
            }
        }

        public static int nBits(Rn rn) {
            switch (rn) {
                case Rn.rax:
                case Rn.rbx:
                case Rn.rcx:
                case Rn.rdx:
                case Rn.rsi:
                case Rn.rdi:
                case Rn.rbp:
                case Rn.rsp:
                case Rn.R8:
                case Rn.R9:
                case Rn.R10:
                case Rn.R11:
                case Rn.R12:
                case Rn.R13:
                case Rn.R14:
                case Rn.R15:
                    return 64;

                case Rn.eax:
                case Rn.ebx:
                case Rn.ecx:
                case Rn.edx:
                case Rn.esi:
                case Rn.edi:
                case Rn.ebp:
                case Rn.esp:
                case Rn.R8D:
                case Rn.R9D:
                case Rn.R10D:
                case Rn.R11D:
                case Rn.R12D:
                case Rn.R13D:
                case Rn.R14D:
                case Rn.R15D:
                    return 32;

                case Rn.ax:
                case Rn.bx:
                case Rn.cx:
                case Rn.dx:
                case Rn.si:
                case Rn.di:
                case Rn.bp:
                case Rn.sp:
                case Rn.R8W:
                case Rn.R9W:
                case Rn.R10W:
                case Rn.R11W:
                case Rn.R12W:
                case Rn.R13W:
                case Rn.R14W:
                case Rn.R15W:
                    return 16;

                case Rn.al:
                case Rn.bl:
                case Rn.cl:
                case Rn.dl:
                case Rn.ah:
                case Rn.bh:
                case Rn.ch:
                case Rn.dh:
                case Rn.sil:
                case Rn.dil:
                case Rn.bpl:
                case Rn.spl:
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

        public static bool isMmx(Rn rn) {
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
        public static bool isSse(Rn rn) {
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
        public static bool isAvx(Rn rn) {
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
        public static bool isAvx512(Rn rn) { 
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
