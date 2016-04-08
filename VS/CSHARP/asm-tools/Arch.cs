using System;

namespace AsmTools {
    [Flags]
    public enum Arch {
        NONE    = 0,
        X86     = 1,
        I686    = 2,
        MMX     = 4,
        SSE     = 8,
        SSE2    = 16,
        SSE3    = 32,
        SSSE3   = 64,
        SSE41   = 128,
        SSE42   = 256,
        AVX     = 512,
        AVX2    = 1024,
        KNC     = 2048
    }

    public static partial class Tools {
        public static Arch parseArch(string str) {
            switch (str.ToUpper()) {
                case "X86": return Arch.X86;
                case "I686": return Arch.I686;
                case "MMX": return Arch.MMX;
                case "SSE": return Arch.SSE;
                case "SSE2": return Arch.SSE2;
                case "SSE3": return Arch.SSE3;
                case "SSSE3": return Arch.SSSE3;
                case "SSE41": return Arch.SSE41;
                case "SSE42": return Arch.SSE42;
                case "AVX": return Arch.AVX;
                case "AVX2": return Arch.AVX2;
                case "KNC": return Arch.KNC;
                case "NONE": return Arch.NONE;
            }
            return Arch.NONE;
        }
    }
}
