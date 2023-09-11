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

    public enum RegisterType
    {
        UNKNOWN,
        BIT8,
        BIT16,
        BIT32,
        BIT64,
        MMX,
        XMM,
        YMM,
        ZMM,
        SEGMENT,
        OPMASK,
        CONTROL,
        DEBUG,
        BOUND,
    }

    public static class RegisterTools
    {
        private static readonly Dictionary<string, Rn> Register_cache_;

        /// <summary>Static class initializer for RegisterTools</summary>
        static RegisterTools()
        {
            Register_cache_ = new Dictionary<string, Rn>();
            foreach (Rn rn in Enum.GetValues(typeof(Rn)))
            {
                Register_cache_.Add(rn.ToString(), rn);
            }
        }

        public static (bool valid, Rn reg, int nBits) ToRn(string str, bool isCapitals = false)
        {
            Rn rn = ParseRn(str, isCapitals);
            return (rn == Rn.NOREG)
                ? (valid: false, reg: Rn.NOREG, nBits: 0)
                : (valid: true, reg: rn, nBits: NBits(rn));
        }

        public static Rn ParseRn(string str, bool strIsCapitals = false)
        {
            return (Register_cache_.TryGetValue(AsmSourceTools.ToCapitals(str, strIsCapitals), out Rn value)) ? value : Rn.NOREG;
        }

        public static Rn ParseRn_OLD(string str, bool strIsCapitals = false)
        {
            switch (AsmSourceTools.ToCapitals(str, strIsCapitals))
            {
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

                case "XMM16": return Rn.XMM16;
                case "XMM17": return Rn.XMM17;
                case "XMM18": return Rn.XMM18;
                case "XMM19": return Rn.XMM19;
                case "XMM20": return Rn.XMM20;
                case "XMM21": return Rn.XMM21;
                case "XMM22": return Rn.XMM22;
                case "XMM23": return Rn.XMM23;

                case "XMM24": return Rn.XMM24;
                case "XMM25": return Rn.XMM25;
                case "XMM26": return Rn.XMM26;
                case "XMM27": return Rn.XMM27;
                case "XMM28": return Rn.XMM28;
                case "XMM29": return Rn.XMM29;
                case "XMM30": return Rn.XMM30;
                case "XMM31": return Rn.XMM31;

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

                case "YMM16": return Rn.YMM16;
                case "YMM17": return Rn.YMM17;
                case "YMM18": return Rn.YMM18;
                case "YMM19": return Rn.YMM19;
                case "YMM20": return Rn.YMM20;
                case "YMM21": return Rn.YMM21;
                case "YMM22": return Rn.YMM22;
                case "YMM23": return Rn.YMM23;

                case "YMM24": return Rn.YMM24;
                case "YMM25": return Rn.YMM25;
                case "YMM26": return Rn.YMM26;
                case "YMM27": return Rn.YMM27;
                case "YMM28": return Rn.YMM28;
                case "YMM29": return Rn.YMM29;
                case "YMM30": return Rn.YMM30;
                case "YMM31": return Rn.YMM31;

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

        public static bool IsRn(string str, bool strIsCapitals = false)
        {
            return Register_cache_.ContainsKey(AsmSourceTools.ToCapitals(str, strIsCapitals));
        }

        public static int NBits(Rn rn)
        {
            switch (rn)
            {
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

                case Rn.XMM16:
                case Rn.XMM17:
                case Rn.XMM18:
                case Rn.XMM19:
                case Rn.XMM20:
                case Rn.XMM21:
                case Rn.XMM22:
                case Rn.XMM23:
                case Rn.XMM24:
                case Rn.XMM25:
                case Rn.XMM26:
                case Rn.XMM27:
                case Rn.XMM28:
                case Rn.XMM29:
                case Rn.XMM30:
                case Rn.XMM31:
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

                case Rn.YMM16:
                case Rn.YMM17:
                case Rn.YMM18:
                case Rn.YMM19:
                case Rn.YMM20:
                case Rn.YMM21:
                case Rn.YMM22:
                case Rn.YMM23:
                case Rn.YMM24:
                case Rn.YMM25:
                case Rn.YMM26:
                case Rn.YMM27:
                case Rn.YMM28:
                case Rn.YMM29:
                case Rn.YMM30:
                case Rn.YMM31:

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

        public static bool Is8BitHigh(Rn reg)
        {
            switch (reg)
            {
                case Rn.AH:
                case Rn.BH:
                case Rn.CH:
                case Rn.DH: return true;
                default: return false;
            }
        }

        /// <summary>
        /// return regular pattern to select the provided register and aliased register names
        /// </summary>
        public static string[] GetRelatedRegisterNew(Rn reg)
        {
            // NOTE: first return longer string before shorter string, such that the first match can be used.
            switch (reg)
            {
                case Rn.RAX:
                case Rn.EAX:
                case Rn.AX:
                case Rn.AL:
                case Rn.AH:
                    return new string[] { "RAX", "EAX", "AX", "AH", "AL" };
                case Rn.RBX:
                case Rn.EBX:
                case Rn.BX:
                case Rn.BL:
                case Rn.BH:
                    return new string[] { "RBX", "EBX", "BX", "BH", "BL" };
                case Rn.RCX:
                case Rn.ECX:
                case Rn.CX:
                case Rn.CL:
                case Rn.CH:
                    return new string[] { "RCX", "ECX", "CX", "CH", "CL" };
                case Rn.RDX:
                case Rn.EDX:
                case Rn.DX:
                case Rn.DL:
                case Rn.DH:
                    return new string[] { "RDX", "EDX", "DX", "DH", "DL" };
                case Rn.RSI:
                case Rn.ESI:
                case Rn.SI:
                case Rn.SIL:
                    return new string[] { "RSI", "ESI", "SIL", "SI", };
                case Rn.RDI:
                case Rn.EDI:
                case Rn.DI:
                case Rn.DIL:
                    return new string[] { "RDI", "EDI", "DIL", "DI" };
                case Rn.RBP:
                case Rn.EBP:
                case Rn.BP:
                case Rn.BPL:
                    return new string[] { "RBP", "EBP", "BPL", "BP" };
                case Rn.RSP:
                case Rn.ESP:
                case Rn.SP:
                case Rn.SPL:
                    return new string[] { "RSP", "ESP", "SPL", "SP" };
                case Rn.R8:
                case Rn.R8D:
                case Rn.R8W:
                case Rn.R8B:
                    return new string[] { "R8D", "R8W", "R8B", "R8" };
                case Rn.R9:
                case Rn.R9D:
                case Rn.R9W:
                case Rn.R9B:
                    return new string[] { "R9D", "R9W", "R9B", "R9" };
                case Rn.R10:
                case Rn.R10D:
                case Rn.R10W:
                case Rn.R10B:
                    return new string[] { "R10D", "R10W", "R10B", "R10" };
                case Rn.R11:
                case Rn.R11D:
                case Rn.R11W:
                case Rn.R11B:
                    return new string[] { "R11D", "R11W", "R11B", "R11" };
                case Rn.R12:
                case Rn.R12D:
                case Rn.R12W:
                case Rn.R12B:
                    return new string[] { "R12D", "R12W", "R12B", "R12" };
                case Rn.R13:
                case Rn.R13D:
                case Rn.R13W:
                case Rn.R13B:
                    return new string[] { "R13D", "R13W", "R13B", "R13" };
                case Rn.R14:
                case Rn.R14D:
                case Rn.R14W:
                case Rn.R14B:
                    return new string[] { "R14D", "R14W", "R14B", "R14" };
                case Rn.R15:
                case Rn.R15D:
                case Rn.R15W:
                case Rn.R15B:
                    return new string[] { "R15D", "R15W", "R15B", "R15" };
                case Rn.XMM0:
                case Rn.YMM0:
                case Rn.ZMM0:
                    return new string[] { "XMM0", "YMM0", "ZMM0" };
                case Rn.XMM1:
                case Rn.YMM1:
                case Rn.ZMM1:
                    return new string[] { "XMM1", "YMM1", "ZMM1" };
                case Rn.XMM2:
                case Rn.YMM2:
                case Rn.ZMM2:
                    return new string[] { "XMM2", "YMM2", "ZMM2" };
                case Rn.XMM3:
                case Rn.YMM3:
                case Rn.ZMM3:
                    return new string[] { "XMM3", "YMM3", "ZMM3" };
                case Rn.XMM4:
                case Rn.YMM4:
                case Rn.ZMM4:
                    return new string[] { "XMM4", "YMM4", "ZMM4" };
                case Rn.XMM5:
                case Rn.YMM5:
                case Rn.ZMM5:
                    return new string[] { "XMM5", "YMM5", "ZMM5" };
                case Rn.XMM6:
                case Rn.YMM6:
                case Rn.ZMM6:
                    return new string[] { "XMM6", "YMM6", "ZMM6" };
                case Rn.XMM7:
                case Rn.YMM7:
                case Rn.ZMM7:
                    return new string[] { "XMM7", "YMM7", "ZMM7" };
                case Rn.XMM8:
                case Rn.YMM8:
                case Rn.ZMM8:
                    return new string[] { "XMM8", "YMM8", "ZMM8" };
                case Rn.XMM9:
                case Rn.YMM9:
                case Rn.ZMM9:
                    return new string[] { "XMM9", "YMM9", "ZMM9" };
                case Rn.XMM10:
                case Rn.YMM10:
                case Rn.ZMM10:
                    return new string[] { "XMM10", "YMM10", "ZMM10" };
                case Rn.XMM11:
                case Rn.YMM11:
                case Rn.ZMM11:
                    return new string[] { "XMM11", "YMM11", "ZMM11" };
                case Rn.XMM12:
                case Rn.YMM12:
                case Rn.ZMM12:
                    return new string[] { "XMM12", "YMM12", "ZMM12" };
                case Rn.XMM13:
                case Rn.YMM13:
                case Rn.ZMM13:
                    return new string[] { "XMM13", "YMM13", "ZMM13" };
                case Rn.XMM14:
                case Rn.YMM14:
                case Rn.ZMM14:
                    return new string[] { "XMM14", "YMM14", "ZMM14" };
                case Rn.XMM15:
                case Rn.YMM15:
                case Rn.ZMM15:
                    return new string[] { "XMM15", "YMM15", "ZMM15" };
                case Rn.XMM16:
                case Rn.YMM16:
                case Rn.ZMM16:
                    return new string[] { "XMM16", "YMM16", "ZMM16" };
                case Rn.XMM17:
                case Rn.YMM17:
                case Rn.ZMM17:
                    return new string[] { "XMM17", "YMM17", "ZMM17" };
                case Rn.XMM18:
                case Rn.YMM18:
                case Rn.ZMM18:
                    return new string[] { "XMM18", "YMM18", "ZMM18" };
                case Rn.XMM19:
                case Rn.YMM19:
                case Rn.ZMM19:
                    return new string[] { "XMM19", "YMM19", "ZMM19" };
                case Rn.XMM20:
                case Rn.YMM20:
                case Rn.ZMM20:
                    return new string[] { "XMM20", "YMM20", "ZMM20" };
                case Rn.XMM21:
                case Rn.YMM21:
                case Rn.ZMM21:
                    return new string[] { "XMM21", "YMM21", "ZMM21" };
                case Rn.XMM22:
                case Rn.YMM22:
                case Rn.ZMM22:
                    return new string[] { "XMM22", "YMM22", "ZMM22" };
                case Rn.XMM23:
                case Rn.YMM23:
                case Rn.ZMM23:
                    return new string[] { "XMM23", "YMM23", "ZMM23" };
                case Rn.XMM24:
                case Rn.YMM24:
                case Rn.ZMM24:
                    return new string[] { "XMM24", "YMM24", "ZMM24" };
                case Rn.XMM25:
                case Rn.YMM25:
                case Rn.ZMM25:
                    return new string[] { "XMM25", "YMM25", "ZMM25" };
                case Rn.XMM26:
                case Rn.YMM26:
                case Rn.ZMM26:
                    return new string[] { "XMM26", "YMM26", "ZMM26" };
                case Rn.XMM27:
                case Rn.YMM27:
                case Rn.ZMM27:
                    return new string[] { "XMM27", "YMM27", "ZMM27" };
                case Rn.XMM28:
                case Rn.YMM28:
                case Rn.ZMM28:
                    return new string[] { "XMM28", "YMM28", "ZMM28" };
                case Rn.XMM29:
                case Rn.YMM29:
                case Rn.ZMM29:
                    return new string[] { "XMM29", "YMM29", "ZMM29" };
                case Rn.XMM30:
                case Rn.YMM30:
                case Rn.ZMM30:
                    return new string[] { "XMM30", "YMM30", "ZMM30" };
                case Rn.XMM31:
                case Rn.YMM31:
                case Rn.ZMM31:
                    return new string[] { "XMM31", "YMM31", "ZMM31" };

                default: return Array.Empty<string>();
            }
        }

        /// <summary>
        /// return regular pattern to select the provided register and aliased register names
        /// </summary>
        public static string GetRelatedRegister(Rn reg)
        {
            switch (reg)
            {
                case Rn.RAX:
                case Rn.EAX:
                case Rn.AX:
                case Rn.AL:
                case Rn.AH:
                    return "\\b(RAX|EAX|AX|AH|AL)\\b";
                case Rn.RBX:
                case Rn.EBX:
                case Rn.BX:
                case Rn.BL:
                case Rn.BH:
                    return "\\b(RBX|EBX|BX|BH|BL)\\b";
                case Rn.RCX:
                case Rn.ECX:
                case Rn.CX:
                case Rn.CL:
                case Rn.CH:
                    return "\\b(RCX|ECX|CX|CH|CL)\\b";
                case Rn.RDX:
                case Rn.EDX:
                case Rn.DX:
                case Rn.DL:
                case Rn.DH:
                    return "\\b(RDX|EDX|DX|DH|DL)\\b";
                case Rn.RSI:
                case Rn.ESI:
                case Rn.SI:
                case Rn.SIL:
                    return "\\b(RSI|ESI|SI|SIL)\\b";
                case Rn.RDI:
                case Rn.EDI:
                case Rn.DI:
                case Rn.DIL:
                    return "\\b(RDI|EDI|DI|DIL)\\b";
                case Rn.RBP:
                case Rn.EBP:
                case Rn.BP:
                case Rn.BPL:
                    return "\\b(RBP|EBP|BP|BPL)\\b";
                case Rn.RSP:
                case Rn.ESP:
                case Rn.SP:
                case Rn.SPL:
                    return "\\b(RSP|ESP|SP|SPL)\\b";
                case Rn.R8:
                case Rn.R8D:
                case Rn.R8W:
                case Rn.R8B:
                    return "\\b(R8|R8D|R8W|R8B)\\b";
                case Rn.R9:
                case Rn.R9D:
                case Rn.R9W:
                case Rn.R9B:
                    return "\\b(R9|R9D|R9W|R9B)\\b";
                case Rn.R10:
                case Rn.R10D:
                case Rn.R10W:
                case Rn.R10B:
                    return "\\b(R10|R10D|R10W|R10B)\\b";
                case Rn.R11:
                case Rn.R11D:
                case Rn.R11W:
                case Rn.R11B:
                    return "\\b(R11|R11D|R11W|R11B)\\b";
                case Rn.R12:
                case Rn.R12D:
                case Rn.R12W:
                case Rn.R12B:
                    return "\\b(R12|R12D|R12W|R12B)\\b";
                case Rn.R13:
                case Rn.R13D:
                case Rn.R13W:
                case Rn.R13B:
                    return "\\b(R13|R13D|R13W|R13B)\\b";
                case Rn.R14:
                case Rn.R14D:
                case Rn.R14W:
                case Rn.R14B:
                    return "\\b(R14|R14D|R14W|R14B)\\b";
                case Rn.R15:
                case Rn.R15D:
                case Rn.R15W:
                case Rn.R15B:
                    return "\\b(R15|R15D|R15W|R15B)\\b";
                case Rn.XMM0:
                case Rn.YMM0:
                case Rn.ZMM0:
                    return "\\b(XMM0|YMM0|ZMM0)\\b";
                case Rn.XMM1:
                case Rn.YMM1:
                case Rn.ZMM1:
                    return "\\b(XMM1|YMM1|ZMM1)\\b";
                case Rn.XMM2:
                case Rn.YMM2:
                case Rn.ZMM2:
                    return "\\b(XMM2|YMM2|ZMM2)\\b";
                case Rn.XMM3:
                case Rn.YMM3:
                case Rn.ZMM3:
                    return "\\b(XMM3|YMM3|ZMM3)\\b";
                case Rn.XMM4:
                case Rn.YMM4:
                case Rn.ZMM4:
                    return "\\b(XMM4|YMM4|ZMM4)\\b";
                case Rn.XMM5:
                case Rn.YMM5:
                case Rn.ZMM5:
                    return "\\b(XMM5|YMM5|ZMM5)\\b";
                case Rn.XMM6:
                case Rn.YMM6:
                case Rn.ZMM6:
                    return "\\b(XMM6|YMM6|ZMM6)\\b";
                case Rn.XMM7:
                case Rn.YMM7:
                case Rn.ZMM7:
                    return "\\b(XMM7|YMM7|ZMM7)\\b";
                case Rn.XMM8:
                case Rn.YMM8:
                case Rn.ZMM8:
                    return "\\b(XMM8|YMM8|ZMM8)\\b";
                case Rn.XMM9:
                case Rn.YMM9:
                case Rn.ZMM9:
                    return "\\b(XMM9|YMM9|ZMM9)\\b";
                case Rn.XMM10:
                case Rn.YMM10:
                case Rn.ZMM10:
                    return "\\b(XMM10|YMM10|ZMM10)\\b";
                case Rn.XMM11:
                case Rn.YMM11:
                case Rn.ZMM11:
                    return "\\b(XMM11|YMM11|ZMM11)\\b";
                case Rn.XMM12:
                case Rn.YMM12:
                case Rn.ZMM12:
                    return "\\b(XMM12|YMM12|ZMM12)\\b";
                case Rn.XMM13:
                case Rn.YMM13:
                case Rn.ZMM13:
                    return "\\b(XMM13|YMM13|ZMM13)\\b";
                case Rn.XMM14:
                case Rn.YMM14:
                case Rn.ZMM14:
                    return "\\b(XMM14|YMM14|ZMM14)\\b";
                case Rn.XMM15:
                case Rn.YMM15:
                case Rn.ZMM15:
                    return "\\b(XMM15|YMM15|ZMM15)\\b";
                case Rn.XMM16:
                case Rn.YMM16:
                case Rn.ZMM16:
                    return "\\b(XMM16|YMM16|ZMM16)\\b";
                case Rn.XMM17:
                case Rn.YMM17:
                case Rn.ZMM17:
                    return "\\b(XMM17|YMM17|ZMM17)\\b";
                case Rn.XMM18:
                case Rn.YMM18:
                case Rn.ZMM18:
                    return "\\b(XMM18|YMM18|ZMM18)\\b";
                case Rn.XMM19:
                case Rn.YMM19:
                case Rn.ZMM19:
                    return "\\b(XMM19|YMM19|ZMM19)\\b";
                case Rn.XMM20:
                case Rn.YMM20:
                case Rn.ZMM20:
                    return "\\b(XMM20|YMM20|ZMM20)\\b";
                case Rn.XMM21:
                case Rn.YMM21:
                case Rn.ZMM21:
                    return "\\b(XMM21|YMM21|ZMM21)\\b";
                case Rn.XMM22:
                case Rn.YMM22:
                case Rn.ZMM22:
                    return "\\b(XMM22|YMM22|ZMM22)\\b";
                case Rn.XMM23:
                case Rn.YMM23:
                case Rn.ZMM23:
                    return "\\b(XMM23|YMM23|ZMM23)\\b";
                case Rn.XMM24:
                case Rn.YMM24:
                case Rn.ZMM24:
                    return "\\b(XMM24|YMM24|ZMM24)\\b";
                case Rn.XMM25:
                case Rn.YMM25:
                case Rn.ZMM25:
                    return "\\b(XMM25|YMM25|ZMM25)\\b";
                case Rn.XMM26:
                case Rn.YMM26:
                case Rn.ZMM26:
                    return "\\b(XMM26|YMM26|ZMM26)\\b";
                case Rn.XMM27:
                case Rn.YMM27:
                case Rn.ZMM27:
                    return "\\b(XMM27|YMM27|ZMM27)\\b";
                case Rn.XMM28:
                case Rn.YMM28:
                case Rn.ZMM28:
                    return "\\b(XMM28|YMM28|ZMM28)\\b";
                case Rn.XMM29:
                case Rn.YMM29:
                case Rn.ZMM29:
                    return "\\b(XMM29|YMM29|ZMM29)\\b";
                case Rn.XMM30:
                case Rn.YMM30:
                case Rn.ZMM30:
                    return "\\b(XMM30|YMM30|ZMM30)\\b";
                case Rn.XMM31:
                case Rn.YMM31:
                case Rn.ZMM31:
                    return "\\b(XMM31|YMM31|ZMM31)\\b";

                default: return reg.ToString();
            }
        }

        private static bool IsNumber(char c)
        {
            switch (c)
            {
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

        public static bool IsRegister(string keyword, bool strIsCapitals = false)
        {
            return Register_cache_.ContainsKey(AsmSourceTools.ToCapitals(keyword, strIsCapitals));
        }

        public static RegisterType GetRegisterType(Rn rn)
        {
            switch (rn)
            {
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
                case Rn.XMM16:
                case Rn.XMM17:
                case Rn.XMM18:
                case Rn.XMM19:
                case Rn.XMM20:
                case Rn.XMM21:
                case Rn.XMM22:
                case Rn.XMM23:
                case Rn.XMM24:
                case Rn.XMM25:
                case Rn.XMM26:
                case Rn.XMM27:
                case Rn.XMM28:
                case Rn.XMM29:
                case Rn.XMM30:
                case Rn.XMM31:
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
                case Rn.YMM16:
                case Rn.YMM17:
                case Rn.YMM18:
                case Rn.YMM19:
                case Rn.YMM20:
                case Rn.YMM21:
                case Rn.YMM22:
                case Rn.YMM23:
                case Rn.YMM24:
                case Rn.YMM25:
                case Rn.YMM26:
                case Rn.YMM27:
                case Rn.YMM28:
                case Rn.YMM29:
                case Rn.YMM30:
                case Rn.YMM31:
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
                    return RegisterType.OPMASK;

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

        public static Rn Get8BitsLowerPart(Rn rn)
        {
            switch (rn)
            {
                case Rn.RAX:
                case Rn.EAX:
                case Rn.AX:
                case Rn.AL:
                    return Rn.AL;
                case Rn.RBX:
                case Rn.EBX:
                case Rn.BX:
                case Rn.BL:
                    return Rn.BL;
                case Rn.RCX:
                case Rn.ECX:
                case Rn.CX:
                case Rn.CL:
                    return Rn.CL;
                case Rn.RDX:
                case Rn.EDX:
                case Rn.DX:
                case Rn.DL:
                    return Rn.DL;
                case Rn.RSI:
                case Rn.ESI:
                case Rn.SI:
                case Rn.SIL:
                    return Rn.SIL;
                case Rn.RDI:
                case Rn.EDI:
                case Rn.DI:
                case Rn.DIL:
                    return Rn.DIL;
                case Rn.RBP:
                case Rn.EBP:
                case Rn.BP:
                case Rn.BPL:
                    return Rn.BPL;
                case Rn.RSP:
                case Rn.ESP:
                case Rn.SP:
                case Rn.SPL:
                    return Rn.SPL;
                case Rn.R8:
                case Rn.R8D:
                case Rn.R8W:
                case Rn.R8B:
                    return Rn.R8B;
                case Rn.R9:
                case Rn.R9D:
                case Rn.R9W:
                case Rn.R9B:
                    return Rn.R9B;
                case Rn.R10:
                case Rn.R10D:
                case Rn.R10W:
                case Rn.R10B:
                    return Rn.R10B;
                case Rn.R11:
                case Rn.R11D:
                case Rn.R11W:
                case Rn.R11B:
                    return Rn.R11B;
                case Rn.R12:
                case Rn.R12D:
                case Rn.R12W:
                case Rn.R12B:
                    return Rn.R12B;
                case Rn.R13:
                case Rn.R13D:
                case Rn.R13W:
                case Rn.R13B:
                    return Rn.R13B;
                case Rn.R14:
                case Rn.R14D:
                case Rn.R14W:
                case Rn.R14B:
                    return Rn.R14B;
                case Rn.R15:
                case Rn.R15D:
                case Rn.R15W:
                case Rn.R15B:
                    return Rn.R15B;

                default:
                    return Rn.NOREG;
            }
        }

        /// <summary>
        /// Get the 64 bits register that belongs to the provided register. eg. ax return rax
        /// </summary>
        public static Rn Get64BitsRegister(Rn rn)
        {
            switch (rn)
            {
                case Rn.RAX:
                case Rn.EAX:
                case Rn.AX:
                case Rn.AL:
                case Rn.AH: return Rn.RAX;
                case Rn.RBX:
                case Rn.EBX:
                case Rn.BX:
                case Rn.BL:
                case Rn.BH: return Rn.RBX;
                case Rn.RCX:
                case Rn.ECX:
                case Rn.CX:
                case Rn.CL:
                case Rn.CH: return Rn.RCX;
                case Rn.RDX:
                case Rn.EDX:
                case Rn.DX:
                case Rn.DL:
                case Rn.DH: return Rn.RDX;
                case Rn.RSI:
                case Rn.ESI:
                case Rn.SI:
                case Rn.SIL: return Rn.RSI;
                case Rn.RDI:
                case Rn.EDI:
                case Rn.DI:
                case Rn.DIL: return Rn.RDI;
                case Rn.RBP:
                case Rn.EBP:
                case Rn.BP:
                case Rn.BPL: return Rn.RBP;
                case Rn.RSP:
                case Rn.ESP:
                case Rn.SP:
                case Rn.SPL: return Rn.RSP;
                case Rn.R8:
                case Rn.R8D:
                case Rn.R8W:
                case Rn.R8B: return Rn.R8;
                case Rn.R9:
                case Rn.R9D:
                case Rn.R9W:
                case Rn.R9B: return Rn.R9;
                case Rn.R10:
                case Rn.R10D:
                case Rn.R10W:
                case Rn.R10B: return Rn.R10;
                case Rn.R11:
                case Rn.R11D:
                case Rn.R11W:
                case Rn.R11B: return Rn.R11;
                case Rn.R12:
                case Rn.R12D:
                case Rn.R12W:
                case Rn.R12B: return Rn.R12;
                case Rn.R13:
                case Rn.R13D:
                case Rn.R13W:
                case Rn.R13B: return Rn.R13;
                case Rn.R14:
                case Rn.R14D:
                case Rn.R14W:
                case Rn.R14B: return Rn.R14;
                case Rn.R15:
                case Rn.R15D:
                case Rn.R15W:
                case Rn.R15B: return Rn.R15;

                case Rn.MM0:
                case Rn.MM1:
                case Rn.MM2:
                case Rn.MM3:
                case Rn.MM4:
                case Rn.MM5:
                case Rn.MM6:
                case Rn.MM7:
                case Rn.XMM0:
                    break;
                case Rn.XMM1:
                    break;
                case Rn.XMM2:
                    break;
                case Rn.XMM3:
                    break;
                case Rn.XMM4:
                    break;
                case Rn.XMM5:
                    break;
                case Rn.XMM6:
                    break;
                case Rn.XMM7:
                    break;
                case Rn.XMM8:
                    break;
                case Rn.XMM9:
                    break;
                case Rn.XMM10:
                    break;
                case Rn.XMM11:
                    break;
                case Rn.XMM12:
                    break;
                case Rn.XMM13:
                    break;
                case Rn.XMM14:
                    break;
                case Rn.XMM15:
                    break;
                case Rn.XMM16:
                    break;
                case Rn.XMM17:
                    break;
                case Rn.XMM18:
                    break;
                case Rn.XMM19:
                    break;
                case Rn.XMM20:
                    break;
                case Rn.XMM21:
                    break;
                case Rn.XMM22:
                    break;
                case Rn.XMM23:
                    break;
                case Rn.XMM24:
                    break;
                case Rn.XMM25:
                    break;
                case Rn.XMM26:
                    break;
                case Rn.XMM27:
                    break;
                case Rn.XMM28:
                    break;
                case Rn.XMM29:
                    break;
                case Rn.XMM30:
                    break;
                case Rn.XMM31:
                    break;
                case Rn.YMM0:
                    break;
                case Rn.YMM1:
                    break;
                case Rn.YMM2:
                    break;
                case Rn.YMM3:
                    break;
                case Rn.YMM4:
                    break;
                case Rn.YMM5:
                    break;
                case Rn.YMM6:
                    break;
                case Rn.YMM7:
                    break;
                case Rn.YMM8:
                    break;
                case Rn.YMM9:
                    break;
                case Rn.YMM10:
                    break;
                case Rn.YMM11:
                    break;
                case Rn.YMM12:
                    break;
                case Rn.YMM13:
                    break;
                case Rn.YMM14:
                    break;
                case Rn.YMM15:
                    break;
                case Rn.YMM16:
                    break;
                case Rn.YMM17:
                    break;
                case Rn.YMM18:
                    break;
                case Rn.YMM19:
                    break;
                case Rn.YMM20:
                    break;
                case Rn.YMM21:
                    break;
                case Rn.YMM22:
                    break;
                case Rn.YMM23:
                    break;
                case Rn.YMM24:
                    break;
                case Rn.YMM25:
                    break;
                case Rn.YMM26:
                    break;
                case Rn.YMM27:
                    break;
                case Rn.YMM28:
                    break;
                case Rn.YMM29:
                    break;
                case Rn.YMM30:
                    break;
                case Rn.YMM31:
                    break;
                case Rn.ZMM0:
                    break;
                case Rn.ZMM1:
                    break;
                case Rn.ZMM2:
                    break;
                case Rn.ZMM3:
                    break;
                case Rn.ZMM4:
                    break;
                case Rn.ZMM5:
                    break;
                case Rn.ZMM6:
                    break;
                case Rn.ZMM7:
                    break;
                case Rn.ZMM8:
                    break;
                case Rn.ZMM9:
                    break;
                case Rn.ZMM10:
                    break;
                case Rn.ZMM11:
                    break;
                case Rn.ZMM12:
                    break;
                case Rn.ZMM13:
                    break;
                case Rn.ZMM14:
                    break;
                case Rn.ZMM15:
                    break;
                case Rn.ZMM16:
                    break;
                case Rn.ZMM17:
                    break;
                case Rn.ZMM18:
                    break;
                case Rn.ZMM19:
                    break;
                case Rn.ZMM20:
                    break;
                case Rn.ZMM21:
                    break;
                case Rn.ZMM22:
                    break;
                case Rn.ZMM23:
                    break;
                case Rn.ZMM24:
                    break;
                case Rn.ZMM25:
                    break;
                case Rn.ZMM26:
                    break;
                case Rn.ZMM27:
                    break;
                case Rn.ZMM28:
                    break;
                case Rn.ZMM29:
                    break;
                case Rn.ZMM30:
                    break;
                case Rn.ZMM31:
                    break;
                case Rn.K0:
                    break;
                case Rn.K1:
                    break;
                case Rn.K2:
                    break;
                case Rn.K3:
                    break;
                case Rn.K4:
                    break;
                case Rn.K5:
                    break;
                case Rn.K6:
                    break;
                case Rn.K7:
                    break;
                case Rn.CS:
                    break;
                case Rn.DS:
                    break;
                case Rn.ES:
                    break;
                case Rn.SS:
                    break;
                case Rn.FS:
                    break;
                case Rn.GS:
                    break;
                case Rn.CR0:
                    break;
                case Rn.CR1:
                    break;
                case Rn.CR2:
                    break;
                case Rn.CR3:
                    break;
                case Rn.CR4:
                    break;
                case Rn.CR5:
                    break;
                case Rn.CR6:
                    break;
                case Rn.CR7:
                    break;
                case Rn.CR8:
                    break;
                case Rn.DR0:
                    break;
                case Rn.DR1:
                    break;
                case Rn.DR2:
                    break;
                case Rn.DR3:
                    break;
                case Rn.DR4:
                    break;
                case Rn.DR5:
                    break;
                case Rn.DR6:
                    break;
                case Rn.DR7:
                    break;
                case Rn.BND0:
                    break;
                case Rn.BND1:
                    break;
                case Rn.BND2:
                    break;
                case Rn.BND3:
                    break;
                default:
                    break;
            }

            return Rn.NOREG;
        }

        public static Arch GetArch(Rn rn)
        {
            switch (rn)
            {
                case Rn.AX:
                case Rn.AL:
                case Rn.AH:
                case Rn.BX:
                case Rn.BL:
                case Rn.BH:
                case Rn.CX:
                case Rn.CL:
                case Rn.CH:
                case Rn.DX:
                case Rn.DL:
                case Rn.DH:
                case Rn.SI:
                case Rn.SIL:
                case Rn.DI:
                case Rn.DIL:
                case Rn.BP:
                case Rn.BPL:
                case Rn.SP:
                case Rn.SPL:
                case Rn.CS:
                case Rn.DS:
                case Rn.ES:
                case Rn.SS: return Arch.ARCH_8086;

                case Rn.EAX:
                case Rn.EBX:
                case Rn.ECX:
                case Rn.EDX:
                case Rn.ESI:
                case Rn.EDI:
                case Rn.EBP:
                case Rn.ESP:
                case Rn.DR0:
                case Rn.DR1:
                case Rn.DR2:
                case Rn.DR3:
                case Rn.DR4:
                case Rn.DR5:
                case Rn.DR6:
                case Rn.DR7:
                case Rn.CR0:
                case Rn.CR1:
                case Rn.CR2:
                case Rn.CR3:
                case Rn.CR4:
                case Rn.CR5:
                case Rn.CR6:
                case Rn.CR7:
                case Rn.CR8: return Arch.ARCH_386;

                case Rn.RAX:
                case Rn.RBX:
                case Rn.RCX:
                case Rn.RDX:
                case Rn.RSI:
                case Rn.RDI:
                case Rn.RBP:
                case Rn.RSP:
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
                case Rn.FS:
                case Rn.GS: return Arch.ARCH_X64;

                case Rn.MM0:
                case Rn.MM1:
                case Rn.MM2:
                case Rn.MM3:
                case Rn.MM4:
                case Rn.MM5:
                case Rn.MM6:
                case Rn.MM7: return Arch.ARCH_MMX;

                case Rn.XMM0:
                case Rn.XMM1:
                case Rn.XMM2:
                case Rn.XMM3:
                case Rn.XMM4:
                case Rn.XMM5:
                case Rn.XMM6:
                case Rn.XMM7: return Arch.ARCH_SSE;

                case Rn.XMM8:
                case Rn.XMM9:
                case Rn.XMM10:
                case Rn.XMM11:
                case Rn.XMM12:
                case Rn.XMM13:
                case Rn.XMM14:
                case Rn.XMM15: return Arch.ARCH_X64;

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
                case Rn.YMM16: return Arch.ARCH_AVX;

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
                case Rn.K0:
                case Rn.K1:
                case Rn.K2:
                case Rn.K3:
                case Rn.K4:
                case Rn.K5:
                case Rn.K6:
                case Rn.K7: return Arch.ARCH_AVX512_F;

                case Rn.XMM16:
                case Rn.XMM17:
                case Rn.XMM18:
                case Rn.XMM19:
                case Rn.XMM20:
                case Rn.XMM21:
                case Rn.XMM22:
                case Rn.XMM23:
                case Rn.XMM24:
                case Rn.XMM25:
                case Rn.XMM26:
                case Rn.XMM27:
                case Rn.XMM28:
                case Rn.XMM29:
                case Rn.XMM30:
                case Rn.XMM31:

                case Rn.YMM17:
                case Rn.YMM18:
                case Rn.YMM19:
                case Rn.YMM20:
                case Rn.YMM21:
                case Rn.YMM22:
                case Rn.YMM23:
                case Rn.YMM24:
                case Rn.YMM25:
                case Rn.YMM26:
                case Rn.YMM27:
                case Rn.YMM28:
                case Rn.YMM29:
                case Rn.YMM30:
                case Rn.YMM31: return Arch.ARCH_AVX512_VL;

                case Rn.BND0:
                case Rn.BND1:
                case Rn.BND2:
                case Rn.BND3: return Arch.ARCH_MPX;

                default: return Arch.ARCH_NONE;
            }
        }

        #region Register Classifications
        public static bool IsOpmaskRegister(Rn rn)
        {
            switch (rn)
            {
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

        public static bool IsBoundRegister(Rn rn)
        {
            switch (rn)
            {
                case Rn.BND0:
                case Rn.BND1:
                case Rn.BND2:
                case Rn.BND3:
                    return true;
                default: return false;
            }
        }

        public static bool IsControlRegister(Rn rn)
        {
            switch (rn)
            {
                case Rn.CR0:
                case Rn.CR1:
                case Rn.CR2:
                case Rn.CR3:
                case Rn.CR4:
                case Rn.CR5:
                case Rn.CR6:
                case Rn.CR7:
                case Rn.CR8: return true;
                default:
                    return false;
            }
        }

        public static bool IsDebugRegister(Rn rn)
        {
            switch (rn)
            {
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

        public static bool IsSegmentRegister(Rn rn)
        {
            switch (rn)
            {
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

        public static bool IsGeneralPurposeRegister(Rn rn)
        {
            switch (rn)
            {
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

        public static bool IsMmxRegister(Rn rn)
        {
            switch (rn)
            {
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

        public static bool Is_SIMD_Register(Rn rn)
        {
            switch (rn)
            {
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
                case Rn.XMM16:
                case Rn.XMM17:
                case Rn.XMM18:
                case Rn.XMM19:
                case Rn.XMM20:
                case Rn.XMM21:
                case Rn.XMM22:
                case Rn.XMM23:
                case Rn.XMM24:
                case Rn.XMM25:
                case Rn.XMM26:
                case Rn.XMM27:
                case Rn.XMM28:
                case Rn.XMM29:
                case Rn.XMM30:
                case Rn.XMM31:
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
                case Rn.YMM16:
                case Rn.YMM17:
                case Rn.YMM18:
                case Rn.YMM19:
                case Rn.YMM20:
                case Rn.YMM21:
                case Rn.YMM22:
                case Rn.YMM23:
                case Rn.YMM24:
                case Rn.YMM25:
                case Rn.YMM26:
                case Rn.YMM27:
                case Rn.YMM28:
                case Rn.YMM29:
                case Rn.YMM30:
                case Rn.YMM31:
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
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSseRegister(Rn rn)
        {
            switch (rn)
            {
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
                case Rn.XMM16:
                case Rn.XMM17:
                case Rn.XMM18:
                case Rn.XMM19:
                case Rn.XMM20:
                case Rn.XMM21:
                case Rn.XMM22:
                case Rn.XMM23:
                case Rn.XMM24:
                case Rn.XMM25:
                case Rn.XMM26:
                case Rn.XMM27:
                case Rn.XMM28:
                case Rn.XMM29:
                case Rn.XMM30:
                case Rn.XMM31:
                    return true;
                default: return false;
            }
        }

        public static bool IsAvxRegister(Rn rn)
        {
            switch (rn)
            {
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
                case Rn.YMM16:
                case Rn.YMM17:
                case Rn.YMM18:
                case Rn.YMM19:
                case Rn.YMM20:
                case Rn.YMM21:
                case Rn.YMM22:
                case Rn.YMM23:
                case Rn.YMM24:
                case Rn.YMM25:
                case Rn.YMM26:
                case Rn.YMM27:
                case Rn.YMM28:
                case Rn.YMM29:
                case Rn.YMM30:
                case Rn.YMM31:
                    return true;
                default: return false;
            }
        }

        public static bool IsAvx512Register(Rn rn)
        {
            switch (rn)
            {
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
                    return true;
                default: return false;
            }
        }
        #endregion
    }
}
