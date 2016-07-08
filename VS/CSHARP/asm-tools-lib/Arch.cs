using System;

namespace AsmTools {

    public enum Arch {

        NONE,

        ARCH_8086,
        ARCH_186,
        ARCH_286,
        ARCH_386,
        ARCH_486,

        MMX,
        SSE,
        SSE2,
        SSE3,
        SSSE3,
        SSE41,
        SSE42,
        SSE4A,
        SSE5,

        AVX,
        AVX2,
        AVX512VL,
        AVX512,
        AVX512DQ,
        AVX512BW,
        AVX512ER,
        AVX512PF,
        AVX512CD,
        AVX512VBMI,
        AVX512IFMA,


        X64,
        BMI1,
        BMI2,
        P6,
        X86_64,
        IA64,
        FPU,
        FMA,
        TBM,
        AMD,
        /// <summary>Privileged instructions</summary>
        PRIV,

        #region Specific Processors
        PENT,
        PENTM,
        NEHALEM,
        WILLAMETTE,
        PRESCOTT,
        WESTMERE,
        SANDYBRIDGE,
        KATMAI,
        FUTURE,
        #endregion


        OPT,
        NOHLE,
        ARCH_3DNOW,
        PROT,
        CYRIX,
        INVPCID,
        CYRIXM,
        VMX,

        RTM,
        HLE,
        MPX,
        MIB,
        SHA,

        #region unused
        SO,
        SW,
        SD,
        SX,
        SY,
        ND,
        LONG,
        NOLONG,
        SIZE,
        LOCK,
        BND,
        UNDOC,
        PREFETCHWT1,
        AR0,
        AR1

        #endregion
    }

    public static partial class AsmSourceTools {

        public static Arch parseArch(string str) {
            switch (str.ToUpper()) {
                case "8086": return Arch.ARCH_8086;
                case "186": return Arch.ARCH_186;
                case "286": return Arch.ARCH_286;
                case "386": return Arch.ARCH_386;
                case "486": return Arch.ARCH_486;

                case "MMX": return Arch.MMX;
                case "SSE": return Arch.SSE;
                case "SSE2": return Arch.SSE2;
                case "SSE3": return Arch.SSE3;
                case "SSSE3": return Arch.SSSE3;
                case "SSE41": return Arch.SSE41;
                case "SSE42": return Arch.SSE42;
                case "SSE4A": return Arch.SSE4A;
                case "SSE5": return Arch.SSE5;

                case "AVX": return Arch.AVX;
                case "AVX2": return Arch.AVX2;
                case "AVX512": return Arch.AVX512;
                case "AVX512VL": return Arch.AVX512VL;
                case "AVX512DQ": return Arch.AVX512DQ;
                case "AVX512BW": return Arch.AVX512BW;
                case "AVX512ER": return Arch.AVX512ER;
                case "AVX512PF": return Arch.AVX512PF;
                case "AVX512CD": return Arch.AVX512CD;
                case "AVX512VBMI": return Arch.AVX512VBMI;
                case "AVX512IFMA": return Arch.AVX512IFMA;

                case "X64": return Arch.X64;
                case "BMI1": return Arch.BMI1;
                case "BMI2": return Arch.BMI2;
                case "P6": return Arch.P6;
                case "X86_64": return Arch.X86_64;
                case "IA64": return Arch.IA64;
                case "FPU": return Arch.FPU;
                case "FMA": return Arch.FMA;
                case "TBM": return Arch.TBM;
                case "AMD": return Arch.AMD;
                /// <summary>Privileged instructions</summary>
                case "PRIV": return Arch.PRIV;

                #region Specific Processors
                case "PENT": return Arch.PENT;
                case "PENTM": return Arch.PENTM;
                case "NEHALEM": return Arch.NEHALEM;
                case "WILLAMETTE": return Arch.WILLAMETTE;
                case "PRESCOTT": return Arch.PRESCOTT;
                case "WESTMERE": return Arch.WESTMERE;
                case "SANDYBRIDGE": return Arch.SANDYBRIDGE;
                case "KATMAI": return Arch.KATMAI;
                case "FUTURE": return Arch.FUTURE;
                #endregion

                case "OPT": return Arch.OPT;
                case "NOHLE": return Arch.NOHLE;
                case "3DNOW": return Arch.ARCH_3DNOW;
                case "PROT": return Arch.PROT;
                case "CYRIX": return Arch.CYRIX;
                case "INVPCID": return Arch.INVPCID;
                case "CYRIXM": return Arch.CYRIXM;
                case "VMX": return Arch.VMX;

                case "RTM": return Arch.RTM;
                case "HLE": return Arch.HLE;
                case "MPX": return Arch.MPX;
                case "MIB": return Arch.MIB;
                case "SHA": return Arch.SHA;

                #region unused
                case "SO": return Arch.SO;
                case "SW": return Arch.SW;
                case "SD": return Arch.SD;
                case "SX": return Arch.SX;
                case "SY": return Arch.SY;
                case "ND": return Arch.ND;
                case "LONG": return Arch.LONG;
                case "NOLONG": return Arch.NOLONG;
                case "SIZE": return Arch.SIZE;
                case "LOCK": return Arch.LOCK;
                case "BND": return Arch.BND;
                case "UNDOC": return Arch.UNDOC;
                case "PREFETCHWT1": return Arch.PREFETCHWT1;
                case "AR0": return Arch.AR0;
                case "AR1": return Arch.AR1;
                #endregion
            }
            return Arch.NONE;
        }

        public static string ToString(Arch arch) {
            switch (arch) {
                case Arch.ARCH_8086: return "8086";
                case Arch.ARCH_186: return "186";
                case Arch.ARCH_286: return "286";
                case Arch.ARCH_386: return "386";
                case Arch.ARCH_486: return "486";
                case Arch.ARCH_3DNOW: return "3DNOW";
                default:
                    break;
            }
            return arch.ToString();
        }
    }
}
