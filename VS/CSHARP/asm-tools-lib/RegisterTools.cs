// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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
            Register_cache_ = [];
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
        public static bool IsRn(string str, bool strIsCapitals = false)
        {
            return Register_cache_.ContainsKey(AsmSourceTools.ToCapitals(str, strIsCapitals));
        }

        public static int NBits(Rn rn)
        {
            return rn switch
            {
                Rn.RAX or Rn.RBX or Rn.RCX or Rn.RDX or Rn.RSI or Rn.RDI or Rn.RBP or Rn.RSP or Rn.R8 or Rn.R9 or Rn.R10 or Rn.R11 or Rn.R12 or Rn.R13 or Rn.R14 or Rn.R15 => 64,
                Rn.EAX or Rn.EBX or Rn.ECX or Rn.EDX or Rn.ESI or Rn.EDI or Rn.EBP or Rn.ESP or Rn.R8D or Rn.R9D or Rn.R10D or Rn.R11D or Rn.R12D or Rn.R13D or Rn.R14D or Rn.R15D => 32,
                Rn.AX or Rn.BX or Rn.CX or Rn.DX or Rn.SI or Rn.DI or Rn.BP or Rn.SP or Rn.R8W or Rn.R9W or Rn.R10W or Rn.R11W or Rn.R12W or Rn.R13W or Rn.R14W or Rn.R15W => 16,
                Rn.AL or Rn.BL or Rn.CL or Rn.DL or Rn.AH or Rn.BH or Rn.CH or Rn.DH or Rn.SIL or Rn.DIL or Rn.BPL or Rn.SPL or Rn.R8B or Rn.R9B or Rn.R10B or Rn.R11B or Rn.R12B or Rn.R13B or Rn.R14B or Rn.R15B => 8,
                Rn.MM0 or Rn.MM1 or Rn.MM2 or Rn.MM3 or Rn.MM4 or Rn.MM5 or Rn.MM6 or Rn.MM7 => 64,
                Rn.XMM0 or Rn.XMM1 or Rn.XMM2 or Rn.XMM3 or Rn.XMM4 or Rn.XMM5 or Rn.XMM6 or Rn.XMM7 or Rn.XMM8 or Rn.XMM9 or Rn.XMM10 or Rn.XMM11 or Rn.XMM12 or Rn.XMM13 or Rn.XMM14 or Rn.XMM15 or Rn.XMM16 or Rn.XMM17 or Rn.XMM18 or Rn.XMM19 or Rn.XMM20 or Rn.XMM21 or Rn.XMM22 or Rn.XMM23 or Rn.XMM24 or Rn.XMM25 or Rn.XMM26 or Rn.XMM27 or Rn.XMM28 or Rn.XMM29 or Rn.XMM30 or Rn.XMM31 => 128,
                Rn.YMM0 or Rn.YMM1 or Rn.YMM2 or Rn.YMM3 or Rn.YMM4 or Rn.YMM5 or Rn.YMM6 or Rn.YMM7 or Rn.YMM8 or Rn.YMM9 or Rn.YMM10 or Rn.YMM11 or Rn.YMM12 or Rn.YMM13 or Rn.YMM14 or Rn.YMM15 or Rn.YMM16 or Rn.YMM17 or Rn.YMM18 or Rn.YMM19 or Rn.YMM20 or Rn.YMM21 or Rn.YMM22 or Rn.YMM23 or Rn.YMM24 or Rn.YMM25 or Rn.YMM26 or Rn.YMM27 or Rn.YMM28 or Rn.YMM29 or Rn.YMM30 or Rn.YMM31 => 256,
                Rn.ZMM0 or Rn.ZMM1 or Rn.ZMM2 or Rn.ZMM3 or Rn.ZMM4 or Rn.ZMM5 or Rn.ZMM6 or Rn.ZMM7 or Rn.ZMM8 or Rn.ZMM9 or Rn.ZMM10 or Rn.ZMM11 or Rn.ZMM12 or Rn.ZMM13 or Rn.ZMM14 or Rn.ZMM15 or Rn.ZMM16 or Rn.ZMM17 or Rn.ZMM18 or Rn.ZMM19 or Rn.ZMM20 or Rn.ZMM21 or Rn.ZMM22 or Rn.ZMM23 or Rn.ZMM24 or Rn.ZMM25 or Rn.ZMM26 or Rn.ZMM27 or Rn.ZMM28 or Rn.ZMM29 or Rn.ZMM30 or Rn.ZMM31 => 512,
                _ => 0,
            };
        }

        public static bool Is8BitHigh(Rn reg)
        {
            return reg switch
            {
                Rn.AH or Rn.BH or Rn.CH or Rn.DH => true,
                _ => false,
            };
        }

        /// <summary>
        /// return regular pattern to select the provided register and aliased register names
        /// </summary>
        public static string[] GetRelatedRegisterNew(Rn reg)
        {
            // NOTE: first return longer string before shorter string, such that the first match can be used.
            return reg switch
            {
                Rn.RAX or Rn.EAX or Rn.AX or Rn.AL or Rn.AH => new string[] { "RAX", "EAX", "AX", "AH", "AL" },
                Rn.RBX or Rn.EBX or Rn.BX or Rn.BL or Rn.BH => new string[] { "RBX", "EBX", "BX", "BH", "BL" },
                Rn.RCX or Rn.ECX or Rn.CX or Rn.CL or Rn.CH => new string[] { "RCX", "ECX", "CX", "CH", "CL" },
                Rn.RDX or Rn.EDX or Rn.DX or Rn.DL or Rn.DH => new string[] { "RDX", "EDX", "DX", "DH", "DL" },
                Rn.RSI or Rn.ESI or Rn.SI or Rn.SIL => new string[] { "RSI", "ESI", "SIL", "SI", },
                Rn.RDI or Rn.EDI or Rn.DI or Rn.DIL => new string[] { "RDI", "EDI", "DIL", "DI" },
                Rn.RBP or Rn.EBP or Rn.BP or Rn.BPL => new string[] { "RBP", "EBP", "BPL", "BP" },
                Rn.RSP or Rn.ESP or Rn.SP or Rn.SPL => new string[] { "RSP", "ESP", "SPL", "SP" },
                Rn.R8 or Rn.R8D or Rn.R8W or Rn.R8B => new string[] { "R8D", "R8W", "R8B", "R8" },
                Rn.R9 or Rn.R9D or Rn.R9W or Rn.R9B => new string[] { "R9D", "R9W", "R9B", "R9" },
                Rn.R10 or Rn.R10D or Rn.R10W or Rn.R10B => new string[] { "R10D", "R10W", "R10B", "R10" },
                Rn.R11 or Rn.R11D or Rn.R11W or Rn.R11B => new string[] { "R11D", "R11W", "R11B", "R11" },
                Rn.R12 or Rn.R12D or Rn.R12W or Rn.R12B => new string[] { "R12D", "R12W", "R12B", "R12" },
                Rn.R13 or Rn.R13D or Rn.R13W or Rn.R13B => new string[] { "R13D", "R13W", "R13B", "R13" },
                Rn.R14 or Rn.R14D or Rn.R14W or Rn.R14B => new string[] { "R14D", "R14W", "R14B", "R14" },
                Rn.R15 or Rn.R15D or Rn.R15W or Rn.R15B => new string[] { "R15D", "R15W", "R15B", "R15" },
                Rn.XMM0 or Rn.YMM0 or Rn.ZMM0 => new string[] { "XMM0", "YMM0", "ZMM0" },
                Rn.XMM1 or Rn.YMM1 or Rn.ZMM1 => new string[] { "XMM1", "YMM1", "ZMM1" },
                Rn.XMM2 or Rn.YMM2 or Rn.ZMM2 => new string[] { "XMM2", "YMM2", "ZMM2" },
                Rn.XMM3 or Rn.YMM3 or Rn.ZMM3 => new string[] { "XMM3", "YMM3", "ZMM3" },
                Rn.XMM4 or Rn.YMM4 or Rn.ZMM4 => new string[] { "XMM4", "YMM4", "ZMM4" },
                Rn.XMM5 or Rn.YMM5 or Rn.ZMM5 => new string[] { "XMM5", "YMM5", "ZMM5" },
                Rn.XMM6 or Rn.YMM6 or Rn.ZMM6 => new string[] { "XMM6", "YMM6", "ZMM6" },
                Rn.XMM7 or Rn.YMM7 or Rn.ZMM7 => new string[] { "XMM7", "YMM7", "ZMM7" },
                Rn.XMM8 or Rn.YMM8 or Rn.ZMM8 => new string[] { "XMM8", "YMM8", "ZMM8" },
                Rn.XMM9 or Rn.YMM9 or Rn.ZMM9 => new string[] { "XMM9", "YMM9", "ZMM9" },
                Rn.XMM10 or Rn.YMM10 or Rn.ZMM10 => new string[] { "XMM10", "YMM10", "ZMM10" },
                Rn.XMM11 or Rn.YMM11 or Rn.ZMM11 => new string[] { "XMM11", "YMM11", "ZMM11" },
                Rn.XMM12 or Rn.YMM12 or Rn.ZMM12 => new string[] { "XMM12", "YMM12", "ZMM12" },
                Rn.XMM13 or Rn.YMM13 or Rn.ZMM13 => new string[] { "XMM13", "YMM13", "ZMM13" },
                Rn.XMM14 or Rn.YMM14 or Rn.ZMM14 => new string[] { "XMM14", "YMM14", "ZMM14" },
                Rn.XMM15 or Rn.YMM15 or Rn.ZMM15 => new string[] { "XMM15", "YMM15", "ZMM15" },
                Rn.XMM16 or Rn.YMM16 or Rn.ZMM16 => new string[] { "XMM16", "YMM16", "ZMM16" },
                Rn.XMM17 or Rn.YMM17 or Rn.ZMM17 => new string[] { "XMM17", "YMM17", "ZMM17" },
                Rn.XMM18 or Rn.YMM18 or Rn.ZMM18 => new string[] { "XMM18", "YMM18", "ZMM18" },
                Rn.XMM19 or Rn.YMM19 or Rn.ZMM19 => new string[] { "XMM19", "YMM19", "ZMM19" },
                Rn.XMM20 or Rn.YMM20 or Rn.ZMM20 => new string[] { "XMM20", "YMM20", "ZMM20" },
                Rn.XMM21 or Rn.YMM21 or Rn.ZMM21 => new string[] { "XMM21", "YMM21", "ZMM21" },
                Rn.XMM22 or Rn.YMM22 or Rn.ZMM22 => new string[] { "XMM22", "YMM22", "ZMM22" },
                Rn.XMM23 or Rn.YMM23 or Rn.ZMM23 => new string[] { "XMM23", "YMM23", "ZMM23" },
                Rn.XMM24 or Rn.YMM24 or Rn.ZMM24 => new string[] { "XMM24", "YMM24", "ZMM24" },
                Rn.XMM25 or Rn.YMM25 or Rn.ZMM25 => new string[] { "XMM25", "YMM25", "ZMM25" },
                Rn.XMM26 or Rn.YMM26 or Rn.ZMM26 => new string[] { "XMM26", "YMM26", "ZMM26" },
                Rn.XMM27 or Rn.YMM27 or Rn.ZMM27 => new string[] { "XMM27", "YMM27", "ZMM27" },
                Rn.XMM28 or Rn.YMM28 or Rn.ZMM28 => new string[] { "XMM28", "YMM28", "ZMM28" },
                Rn.XMM29 or Rn.YMM29 or Rn.ZMM29 => new string[] { "XMM29", "YMM29", "ZMM29" },
                Rn.XMM30 or Rn.YMM30 or Rn.ZMM30 => new string[] { "XMM30", "YMM30", "ZMM30" },
                Rn.XMM31 or Rn.YMM31 or Rn.ZMM31 => new string[] { "XMM31", "YMM31", "ZMM31" },
                _ => [],
            };
        }

        /// <summary>
        /// return regular pattern to select the provided register and aliased register names
        /// </summary>
        public static string GetRelatedRegister(Rn reg)
        {
            return reg switch
            {
                Rn.RAX or Rn.EAX or Rn.AX or Rn.AL or Rn.AH => "\\b(RAX|EAX|AX|AH|AL)\\b",
                Rn.RBX or Rn.EBX or Rn.BX or Rn.BL or Rn.BH => "\\b(RBX|EBX|BX|BH|BL)\\b",
                Rn.RCX or Rn.ECX or Rn.CX or Rn.CL or Rn.CH => "\\b(RCX|ECX|CX|CH|CL)\\b",
                Rn.RDX or Rn.EDX or Rn.DX or Rn.DL or Rn.DH => "\\b(RDX|EDX|DX|DH|DL)\\b",
                Rn.RSI or Rn.ESI or Rn.SI or Rn.SIL => "\\b(RSI|ESI|SI|SIL)\\b",
                Rn.RDI or Rn.EDI or Rn.DI or Rn.DIL => "\\b(RDI|EDI|DI|DIL)\\b",
                Rn.RBP or Rn.EBP or Rn.BP or Rn.BPL => "\\b(RBP|EBP|BP|BPL)\\b",
                Rn.RSP or Rn.ESP or Rn.SP or Rn.SPL => "\\b(RSP|ESP|SP|SPL)\\b",
                Rn.R8 or Rn.R8D or Rn.R8W or Rn.R8B => "\\b(R8|R8D|R8W|R8B)\\b",
                Rn.R9 or Rn.R9D or Rn.R9W or Rn.R9B => "\\b(R9|R9D|R9W|R9B)\\b",
                Rn.R10 or Rn.R10D or Rn.R10W or Rn.R10B => "\\b(R10|R10D|R10W|R10B)\\b",
                Rn.R11 or Rn.R11D or Rn.R11W or Rn.R11B => "\\b(R11|R11D|R11W|R11B)\\b",
                Rn.R12 or Rn.R12D or Rn.R12W or Rn.R12B => "\\b(R12|R12D|R12W|R12B)\\b",
                Rn.R13 or Rn.R13D or Rn.R13W or Rn.R13B => "\\b(R13|R13D|R13W|R13B)\\b",
                Rn.R14 or Rn.R14D or Rn.R14W or Rn.R14B => "\\b(R14|R14D|R14W|R14B)\\b",
                Rn.R15 or Rn.R15D or Rn.R15W or Rn.R15B => "\\b(R15|R15D|R15W|R15B)\\b",
                Rn.XMM0 or Rn.YMM0 or Rn.ZMM0 => "\\b(XMM0|YMM0|ZMM0)\\b",
                Rn.XMM1 or Rn.YMM1 or Rn.ZMM1 => "\\b(XMM1|YMM1|ZMM1)\\b",
                Rn.XMM2 or Rn.YMM2 or Rn.ZMM2 => "\\b(XMM2|YMM2|ZMM2)\\b",
                Rn.XMM3 or Rn.YMM3 or Rn.ZMM3 => "\\b(XMM3|YMM3|ZMM3)\\b",
                Rn.XMM4 or Rn.YMM4 or Rn.ZMM4 => "\\b(XMM4|YMM4|ZMM4)\\b",
                Rn.XMM5 or Rn.YMM5 or Rn.ZMM5 => "\\b(XMM5|YMM5|ZMM5)\\b",
                Rn.XMM6 or Rn.YMM6 or Rn.ZMM6 => "\\b(XMM6|YMM6|ZMM6)\\b",
                Rn.XMM7 or Rn.YMM7 or Rn.ZMM7 => "\\b(XMM7|YMM7|ZMM7)\\b",
                Rn.XMM8 or Rn.YMM8 or Rn.ZMM8 => "\\b(XMM8|YMM8|ZMM8)\\b",
                Rn.XMM9 or Rn.YMM9 or Rn.ZMM9 => "\\b(XMM9|YMM9|ZMM9)\\b",
                Rn.XMM10 or Rn.YMM10 or Rn.ZMM10 => "\\b(XMM10|YMM10|ZMM10)\\b",
                Rn.XMM11 or Rn.YMM11 or Rn.ZMM11 => "\\b(XMM11|YMM11|ZMM11)\\b",
                Rn.XMM12 or Rn.YMM12 or Rn.ZMM12 => "\\b(XMM12|YMM12|ZMM12)\\b",
                Rn.XMM13 or Rn.YMM13 or Rn.ZMM13 => "\\b(XMM13|YMM13|ZMM13)\\b",
                Rn.XMM14 or Rn.YMM14 or Rn.ZMM14 => "\\b(XMM14|YMM14|ZMM14)\\b",
                Rn.XMM15 or Rn.YMM15 or Rn.ZMM15 => "\\b(XMM15|YMM15|ZMM15)\\b",
                Rn.XMM16 or Rn.YMM16 or Rn.ZMM16 => "\\b(XMM16|YMM16|ZMM16)\\b",
                Rn.XMM17 or Rn.YMM17 or Rn.ZMM17 => "\\b(XMM17|YMM17|ZMM17)\\b",
                Rn.XMM18 or Rn.YMM18 or Rn.ZMM18 => "\\b(XMM18|YMM18|ZMM18)\\b",
                Rn.XMM19 or Rn.YMM19 or Rn.ZMM19 => "\\b(XMM19|YMM19|ZMM19)\\b",
                Rn.XMM20 or Rn.YMM20 or Rn.ZMM20 => "\\b(XMM20|YMM20|ZMM20)\\b",
                Rn.XMM21 or Rn.YMM21 or Rn.ZMM21 => "\\b(XMM21|YMM21|ZMM21)\\b",
                Rn.XMM22 or Rn.YMM22 or Rn.ZMM22 => "\\b(XMM22|YMM22|ZMM22)\\b",
                Rn.XMM23 or Rn.YMM23 or Rn.ZMM23 => "\\b(XMM23|YMM23|ZMM23)\\b",
                Rn.XMM24 or Rn.YMM24 or Rn.ZMM24 => "\\b(XMM24|YMM24|ZMM24)\\b",
                Rn.XMM25 or Rn.YMM25 or Rn.ZMM25 => "\\b(XMM25|YMM25|ZMM25)\\b",
                Rn.XMM26 or Rn.YMM26 or Rn.ZMM26 => "\\b(XMM26|YMM26|ZMM26)\\b",
                Rn.XMM27 or Rn.YMM27 or Rn.ZMM27 => "\\b(XMM27|YMM27|ZMM27)\\b",
                Rn.XMM28 or Rn.YMM28 or Rn.ZMM28 => "\\b(XMM28|YMM28|ZMM28)\\b",
                Rn.XMM29 or Rn.YMM29 or Rn.ZMM29 => "\\b(XMM29|YMM29|ZMM29)\\b",
                Rn.XMM30 or Rn.YMM30 or Rn.ZMM30 => "\\b(XMM30|YMM30|ZMM30)\\b",
                Rn.XMM31 or Rn.YMM31 or Rn.ZMM31 => "\\b(XMM31|YMM31|ZMM31)\\b",
                _ => reg.ToString(),
            };
        }

        private static bool IsNumber(char c)
        {
            return c switch
            {
                '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' => true,
                _ => false,
            };
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
            return rn switch
            {
                Rn.RAX or Rn.EAX or Rn.AX or Rn.AL => Rn.AL,
                Rn.RBX or Rn.EBX or Rn.BX or Rn.BL => Rn.BL,
                Rn.RCX or Rn.ECX or Rn.CX or Rn.CL => Rn.CL,
                Rn.RDX or Rn.EDX or Rn.DX or Rn.DL => Rn.DL,
                Rn.RSI or Rn.ESI or Rn.SI or Rn.SIL => Rn.SIL,
                Rn.RDI or Rn.EDI or Rn.DI or Rn.DIL => Rn.DIL,
                Rn.RBP or Rn.EBP or Rn.BP or Rn.BPL => Rn.BPL,
                Rn.RSP or Rn.ESP or Rn.SP or Rn.SPL => Rn.SPL,
                Rn.R8 or Rn.R8D or Rn.R8W or Rn.R8B => Rn.R8B,
                Rn.R9 or Rn.R9D or Rn.R9W or Rn.R9B => Rn.R9B,
                Rn.R10 or Rn.R10D or Rn.R10W or Rn.R10B => Rn.R10B,
                Rn.R11 or Rn.R11D or Rn.R11W or Rn.R11B => Rn.R11B,
                Rn.R12 or Rn.R12D or Rn.R12W or Rn.R12B => Rn.R12B,
                Rn.R13 or Rn.R13D or Rn.R13W or Rn.R13B => Rn.R13B,
                Rn.R14 or Rn.R14D or Rn.R14W or Rn.R14B => Rn.R14B,
                Rn.R15 or Rn.R15D or Rn.R15W or Rn.R15B => Rn.R15B,
                _ => Rn.NOREG,
            };
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
            return rn switch
            {
                Rn.AX or Rn.AL or Rn.AH or Rn.BX or Rn.BL or Rn.BH or Rn.CX or Rn.CL or Rn.CH or Rn.DX or Rn.DL or Rn.DH or Rn.SI or Rn.SIL or Rn.DI or Rn.DIL or Rn.BP or Rn.BPL or Rn.SP or Rn.SPL or Rn.CS or Rn.DS or Rn.ES or Rn.SS => Arch.ARCH_8086,
                Rn.EAX or Rn.EBX or Rn.ECX or Rn.EDX or Rn.ESI or Rn.EDI or Rn.EBP or Rn.ESP or Rn.DR0 or Rn.DR1 or Rn.DR2 or Rn.DR3 or Rn.DR4 or Rn.DR5 or Rn.DR6 or Rn.DR7 or Rn.CR0 or Rn.CR1 or Rn.CR2 or Rn.CR3 or Rn.CR4 or Rn.CR5 or Rn.CR6 or Rn.CR7 or Rn.CR8 => Arch.ARCH_386,
                Rn.RAX or Rn.RBX or Rn.RCX or Rn.RDX or Rn.RSI or Rn.RDI or Rn.RBP or Rn.RSP or Rn.R8 or Rn.R8D or Rn.R8W or Rn.R8B or Rn.R9 or Rn.R9D or Rn.R9W or Rn.R9B or Rn.R10 or Rn.R10D or Rn.R10W or Rn.R10B or Rn.R11 or Rn.R11D or Rn.R11W or Rn.R11B or Rn.R12 or Rn.R12D or Rn.R12W or Rn.R12B or Rn.R13 or Rn.R13D or Rn.R13W or Rn.R13B or Rn.R14 or Rn.R14D or Rn.R14W or Rn.R14B or Rn.R15 or Rn.R15D or Rn.R15W or Rn.R15B or Rn.FS or Rn.GS => Arch.ARCH_X64,
                Rn.MM0 or Rn.MM1 or Rn.MM2 or Rn.MM3 or Rn.MM4 or Rn.MM5 or Rn.MM6 or Rn.MM7 => Arch.ARCH_MMX,
                Rn.XMM0 or Rn.XMM1 or Rn.XMM2 or Rn.XMM3 or Rn.XMM4 or Rn.XMM5 or Rn.XMM6 or Rn.XMM7 => Arch.ARCH_SSE,
                Rn.XMM8 or Rn.XMM9 or Rn.XMM10 or Rn.XMM11 or Rn.XMM12 or Rn.XMM13 or Rn.XMM14 or Rn.XMM15 => Arch.ARCH_X64,
                Rn.YMM0 or Rn.YMM1 or Rn.YMM2 or Rn.YMM3 or Rn.YMM4 or Rn.YMM5 or Rn.YMM6 or Rn.YMM7 or Rn.YMM8 or Rn.YMM9 or Rn.YMM10 or Rn.YMM11 or Rn.YMM12 or Rn.YMM13 or Rn.YMM14 or Rn.YMM15 or Rn.YMM16 => Arch.ARCH_AVX,
                Rn.ZMM0 or Rn.ZMM1 or Rn.ZMM2 or Rn.ZMM3 or Rn.ZMM4 or Rn.ZMM5 or Rn.ZMM6 or Rn.ZMM7 or Rn.ZMM8 or Rn.ZMM9 or Rn.ZMM10 or Rn.ZMM11 or Rn.ZMM12 or Rn.ZMM13 or Rn.ZMM14 or Rn.ZMM15 or Rn.ZMM16 or Rn.ZMM17 or Rn.ZMM18 or Rn.ZMM19 or Rn.ZMM20 or Rn.ZMM21 or Rn.ZMM22 or Rn.ZMM23 or Rn.ZMM24 or Rn.ZMM25 or Rn.ZMM26 or Rn.ZMM27 or Rn.ZMM28 or Rn.ZMM29 or Rn.ZMM30 or Rn.ZMM31 or Rn.K0 or Rn.K1 or Rn.K2 or Rn.K3 or Rn.K4 or Rn.K5 or Rn.K6 or Rn.K7 => Arch.ARCH_AVX512_F,
                Rn.XMM16 or Rn.XMM17 or Rn.XMM18 or Rn.XMM19 or Rn.XMM20 or Rn.XMM21 or Rn.XMM22 or Rn.XMM23 or Rn.XMM24 or Rn.XMM25 or Rn.XMM26 or Rn.XMM27 or Rn.XMM28 or Rn.XMM29 or Rn.XMM30 or Rn.XMM31 or Rn.YMM17 or Rn.YMM18 or Rn.YMM19 or Rn.YMM20 or Rn.YMM21 or Rn.YMM22 or Rn.YMM23 or Rn.YMM24 or Rn.YMM25 or Rn.YMM26 or Rn.YMM27 or Rn.YMM28 or Rn.YMM29 or Rn.YMM30 or Rn.YMM31 => Arch.ARCH_AVX512_VL,
                Rn.BND0 or Rn.BND1 or Rn.BND2 or Rn.BND3 => Arch.ARCH_MPX,
                _ => Arch.ARCH_NONE,
            };
        }

        #region Register Classifications
        public static bool IsOpmaskRegister(Rn rn)
        {
            return rn switch
            {
                Rn.K0 or Rn.K1 or Rn.K2 or Rn.K3 or Rn.K4 or Rn.K5 or Rn.K6 or Rn.K7 => true,
                _ => false,
            };
        }

        public static bool IsBoundRegister(Rn rn)
        {
            return rn switch
            {
                Rn.BND0 or Rn.BND1 or Rn.BND2 or Rn.BND3 => true,
                _ => false,
            };
        }

        public static bool IsControlRegister(Rn rn)
        {
            return rn switch
            {
                Rn.CR0 or Rn.CR1 or Rn.CR2 or Rn.CR3 or Rn.CR4 or Rn.CR5 or Rn.CR6 or Rn.CR7 or Rn.CR8 => true,
                _ => false,
            };
        }

        public static bool IsDebugRegister(Rn rn)
        {
            return rn switch
            {
                Rn.DR0 or Rn.DR1 or Rn.DR2 or Rn.DR3 or Rn.DR4 or Rn.DR5 or Rn.DR6 or Rn.DR7 => true,
                _ => false,
            };
        }

        public static bool IsSegmentRegister(Rn rn)
        {
            return rn switch
            {
                Rn.CS or Rn.DS or Rn.ES or Rn.SS or Rn.FS or Rn.GS => true,
                _ => false,
            };
        }

        public static bool IsGeneralPurposeRegister(Rn rn)
        {
            return rn switch
            {
                Rn.RAX or Rn.EAX or Rn.AX or Rn.AL or Rn.AH or Rn.RBX or Rn.EBX or Rn.BX or Rn.BL or Rn.BH or Rn.RCX or Rn.ECX or Rn.CX or Rn.CL or Rn.CH or Rn.RDX or Rn.EDX or Rn.DX or Rn.DL or Rn.DH or Rn.RSI or Rn.ESI or Rn.SI or Rn.SIL or Rn.RDI or Rn.EDI or Rn.DI or Rn.DIL or Rn.RBP or Rn.EBP or Rn.BP or Rn.BPL or Rn.RSP or Rn.ESP or Rn.SP or Rn.SPL or Rn.R8 or Rn.R8D or Rn.R8W or Rn.R8B or Rn.R9 or Rn.R9D or Rn.R9W or Rn.R9B or Rn.R10 or Rn.R10D or Rn.R10W or Rn.R10B or Rn.R11 or Rn.R11D or Rn.R11W or Rn.R11B or Rn.R12 or Rn.R12D or Rn.R12W or Rn.R12B or Rn.R13 or Rn.R13D or Rn.R13W or Rn.R13B or Rn.R14 or Rn.R14D or Rn.R14W or Rn.R14B or Rn.R15 or Rn.R15D or Rn.R15W or Rn.R15B => true,
                _ => false,
            };
        }

        public static bool IsMmxRegister(Rn rn)
        {
            return rn switch
            {
                Rn.MM0 or Rn.MM1 or Rn.MM2 or Rn.MM3 or Rn.MM4 or Rn.MM5 or Rn.MM6 or Rn.MM7 => true,
                _ => false,
            };
        }

        public static bool Is_SIMD_Register(Rn rn)
        {
            return rn switch
            {
                Rn.XMM0 or Rn.XMM1 or Rn.XMM2 or Rn.XMM3 or Rn.XMM4 or Rn.XMM5 or Rn.XMM6 or Rn.XMM7 or Rn.XMM8 or Rn.XMM9 or Rn.XMM10 or Rn.XMM11 or Rn.XMM12 or Rn.XMM13 or Rn.XMM14 or Rn.XMM15 or Rn.XMM16 or Rn.XMM17 or Rn.XMM18 or Rn.XMM19 or Rn.XMM20 or Rn.XMM21 or Rn.XMM22 or Rn.XMM23 or Rn.XMM24 or Rn.XMM25 or Rn.XMM26 or Rn.XMM27 or Rn.XMM28 or Rn.XMM29 or Rn.XMM30 or Rn.XMM31 or Rn.YMM0 or Rn.YMM1 or Rn.YMM2 or Rn.YMM3 or Rn.YMM4 or Rn.YMM5 or Rn.YMM6 or Rn.YMM7 or Rn.YMM8 or Rn.YMM9 or Rn.YMM10 or Rn.YMM11 or Rn.YMM12 or Rn.YMM13 or Rn.YMM14 or Rn.YMM15 or Rn.YMM16 or Rn.YMM17 or Rn.YMM18 or Rn.YMM19 or Rn.YMM20 or Rn.YMM21 or Rn.YMM22 or Rn.YMM23 or Rn.YMM24 or Rn.YMM25 or Rn.YMM26 or Rn.YMM27 or Rn.YMM28 or Rn.YMM29 or Rn.YMM30 or Rn.YMM31 or Rn.ZMM0 or Rn.ZMM1 or Rn.ZMM2 or Rn.ZMM3 or Rn.ZMM4 or Rn.ZMM5 or Rn.ZMM6 or Rn.ZMM7 or Rn.ZMM8 or Rn.ZMM9 or Rn.ZMM10 or Rn.ZMM11 or Rn.ZMM12 or Rn.ZMM13 or Rn.ZMM14 or Rn.ZMM15 or Rn.ZMM16 or Rn.ZMM17 or Rn.ZMM18 or Rn.ZMM19 or Rn.ZMM20 or Rn.ZMM21 or Rn.ZMM22 or Rn.ZMM23 or Rn.ZMM24 or Rn.ZMM25 or Rn.ZMM26 or Rn.ZMM27 or Rn.ZMM28 or Rn.ZMM29 or Rn.ZMM30 or Rn.ZMM31 => true,
                _ => false,
            };
        }

        public static bool IsSseRegister(Rn rn)
        {
            return rn switch
            {
                Rn.XMM0 or Rn.XMM1 or Rn.XMM2 or Rn.XMM3 or Rn.XMM4 or Rn.XMM5 or Rn.XMM6 or Rn.XMM7 or Rn.XMM8 or Rn.XMM9 or Rn.XMM10 or Rn.XMM11 or Rn.XMM12 or Rn.XMM13 or Rn.XMM14 or Rn.XMM15 or Rn.XMM16 or Rn.XMM17 or Rn.XMM18 or Rn.XMM19 or Rn.XMM20 or Rn.XMM21 or Rn.XMM22 or Rn.XMM23 or Rn.XMM24 or Rn.XMM25 or Rn.XMM26 or Rn.XMM27 or Rn.XMM28 or Rn.XMM29 or Rn.XMM30 or Rn.XMM31 => true,
                _ => false,
            };
        }

        public static bool IsAvxRegister(Rn rn)
        {
            return rn switch
            {
                Rn.YMM0 or Rn.YMM1 or Rn.YMM2 or Rn.YMM3 or Rn.YMM4 or Rn.YMM5 or Rn.YMM6 or Rn.YMM7 or Rn.YMM8 or Rn.YMM9 or Rn.YMM10 or Rn.YMM11 or Rn.YMM12 or Rn.YMM13 or Rn.YMM14 or Rn.YMM15 or Rn.YMM16 or Rn.YMM17 or Rn.YMM18 or Rn.YMM19 or Rn.YMM20 or Rn.YMM21 or Rn.YMM22 or Rn.YMM23 or Rn.YMM24 or Rn.YMM25 or Rn.YMM26 or Rn.YMM27 or Rn.YMM28 or Rn.YMM29 or Rn.YMM30 or Rn.YMM31 => true,
                _ => false,
            };
        }

        public static bool IsAvx512Register(Rn rn)
        {
            return rn switch
            {
                Rn.ZMM0 or Rn.ZMM1 or Rn.ZMM2 or Rn.ZMM3 or Rn.ZMM4 or Rn.ZMM5 or Rn.ZMM6 or Rn.ZMM7 or Rn.ZMM8 or Rn.ZMM9 or Rn.ZMM10 or Rn.ZMM11 or Rn.ZMM12 or Rn.ZMM13 or Rn.ZMM14 or Rn.ZMM15 or Rn.ZMM16 or Rn.ZMM17 or Rn.ZMM18 or Rn.ZMM19 or Rn.ZMM20 or Rn.ZMM21 or Rn.ZMM22 or Rn.ZMM23 or Rn.ZMM24 or Rn.ZMM25 or Rn.ZMM26 or Rn.ZMM27 or Rn.ZMM28 or Rn.ZMM29 or Rn.ZMM30 or Rn.ZMM31 => true,
                _ => false,
            };
        }
        #endregion
    }
}
