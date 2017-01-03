
using System;

namespace AsmTools {

    [Flags]
    public enum AssemblerEnum : byte {
        UNKNOWN = 0,
        NASM    = 1 << 0,
        MASM    = 1 << 1,
        ALL     = NASM | MASM
    }

    public static partial class AsmSourceTools {
        public static AssemblerEnum parseAssembler(string str) {
            if ((str == null) || (str.Length == 0))
            {
                return AssemblerEnum.UNKNOWN;
            }
            AssemblerEnum result = AssemblerEnum.UNKNOWN;
            foreach (string str2 in str.ToUpper().Split(','))
            {
                switch (str2.Trim())
                {
                    case "MASM": result |= AssemblerEnum.MASM; break;
                    case "NASM": result |= AssemblerEnum.NASM; break;
                }
            }
            return result;
        }
    }
}
