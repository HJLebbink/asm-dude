using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Xml;

using AsmTools;

namespace asm_annotate
{
    /// <summary>
    /// VCVTTPS2QQ_ZMMi64_MASKmskw_YMMf32_AVX512,256,no,1.0,7.0
    /// VCVTTPS2QQ_ZMMi64_MASKmskw_YMMf32_AVX512,256,yes,1.0,7.0
    /// VCVTTPS2UDQ_XMMu32_MASKmskw_MEMf32_AVX512,128,no,0.5,10.0
    /// VCVTTPS2UDQ_XMMu32_MASKmskw_MEMf32_AVX512,128,yes,0.5,10.0
    /// VCVTTPS2UDQ_XMMu32_MASKmskw_XMMf32_AVX512,128,no,0.5,4.0
    /// </summary>

    enum IformRegister
    {
        ZMMu128,
        ZMMi64,
        ZMMf64,
        ZMMu64,
        ZMMi32,
        ZMMf32,
        ZMMu32,
        ZMMi16,
        ZMMu16,
        ZMMf16,
        ZMMi8,
        ZMMu8,

        YMMu128,
        YMMi64,
        YMMf64,
        YMMu64,
        YMMi32,
        YMMf32,
        YMMu32,
        YMMi16,
        YMMu16,
        YMMf16,
        YMMi8,
        YMMu8,
        YMMqq,
        YMMdq,

        XMMu128,
        XMMi64,
        XMMf64,
        XMMu64,
        XMMi32,
        XMMf32,
        XMMu32,
        XMMi16,
        XMMu16,
        XMMf16,
        XMMi8,
        XMMu8,
        XMMdq,
        XMMq,
        XMMd,
        XMMw,
        XMMb,

        MMXq,
        MMXd,

        MEMu128,
        MEMi64,
        MEMu64,
        MEMf64,
        MEMi32,
        MEMf32,
        MEMu32,
        MEMu32d,
        MEMi16,
        MEMu16,
        MEMf16,
        MEMi8,
        MEMu8,
        MEMq,
        MEMd,
        MEMdq,
        MEMqq,
        MEMsd,
        MEMss,
        MEMv,
        MEMw,
        MEMb,

        GPR64q,
        GPR64,
        GPR32d,
        GPR32,
        GPR8,
        GPRv,

        VGPR64q,
        VGPR32d,

        GPR64u64,
        GPR32u32,
        GPR32u16,
        GPR32u8,

        IMM8,
        IMMb,
        IMMz,
        CL,

        MASKmskw,
        AVX512,
        IGNORE,
        UNKNOWN
    }

    class ParserXed
    {
        public static string ToCapitals(string str, bool strIsCapitals)
        {
            Contract.Requires(str != null);

#if DEBUG
            if (strIsCapitals && (str != str.ToUpperInvariant()))
            {
                throw new Exception();
            }
#endif
            return (strIsCapitals) ? str : str.ToUpperInvariant();
        }

        public static IformRegister ParseIformRegister(string str, bool strIsCapitals)
        {
            switch (ToCapitals(str, strIsCapitals))
            {
                case "ZMMU128": return IformRegister.ZMMu128;
                case "ZMMI64": return IformRegister.ZMMi64;
                case "ZMMF64": return IformRegister.ZMMf64;
                case "ZMMU64": return IformRegister.ZMMu64;
                case "ZMMI32": return IformRegister.ZMMi32;
                case "ZMMF32": return IformRegister.ZMMf32;
                case "ZMMU32": return IformRegister.ZMMu32;
                case "ZMMI16": return IformRegister.ZMMi16;
                case "ZMMU16": return IformRegister.ZMMu16;
                case "ZMMF16": return IformRegister.ZMMf16;
                case "ZMMI8": return IformRegister.ZMMi8;
                case "ZMMU8": return IformRegister.ZMMu8;

                case "YMMU128": return IformRegister.YMMu128;
                case "YMMI64": return IformRegister.YMMi64;
                case "YMMF64": return IformRegister.YMMf64;
                case "YMMU64": return IformRegister.YMMu64;
                case "YMMI32": return IformRegister.YMMi32;
                case "YMMF32": return IformRegister.YMMf32;
                case "YMMU32": return IformRegister.YMMu32;
                case "YMMI16": return IformRegister.YMMi16;
                case "YMMU16": return IformRegister.YMMu16;
                case "YMMF16": return IformRegister.YMMf16;
                case "YMMI8": return IformRegister.YMMi8;
                case "YMMU8": return IformRegister.YMMu8;
                case "YMMQQ": return IformRegister.YMMqq;
                case "YMMDQ": return IformRegister.YMMdq;

                case "XMMU128": return IformRegister.XMMu128;
                case "XMMI64": return IformRegister.XMMi64;
                case "XMMF64": return IformRegister.XMMf64;
                case "XMMU64": return IformRegister.XMMu64;
                case "XMMI32": return IformRegister.XMMi32;
                case "XMMF32": return IformRegister.XMMf32;
                case "XMMU32": return IformRegister.XMMu32;
                case "XMMI16": return IformRegister.XMMi16;
                case "XMMU16": return IformRegister.XMMu16;
                case "XMMF16": return IformRegister.XMMf16;
                case "XMMU8": return IformRegister.XMMu8;
                case "XMMI8": return IformRegister.XMMi8;
                case "XMMDQ": return IformRegister.XMMdq;
                case "XMMQ": return IformRegister.XMMq;
                case "XMMD": return IformRegister.XMMd;
                case "XMMW": return IformRegister.XMMw;
                case "XMMB": return IformRegister.XMMb;

                case "MMXQ": return IformRegister.MMXq;
                case "MMXD": return IformRegister.MMXd;

                case "MEMU128": return IformRegister.MEMu128;
                case "MEMI64": return IformRegister.MEMi64;
                case "MEMU64": return IformRegister.MEMu64;
                case "MEMF64": return IformRegister.MEMf64;
                case "MEMI32": return IformRegister.MEMi32;
                case "MEMU32": return IformRegister.MEMu32;
                case "MEMU32D": return IformRegister.MEMu32d;
                case "MEMF32": return IformRegister.MEMf32;
                case "MEMI16": return IformRegister.MEMi16;
                case "MEMU16": return IformRegister.MEMu16;
                case "MEMF16": return IformRegister.MEMf16;
                case "MEMU8": return IformRegister.MEMu8;
                case "MEMI8": return IformRegister.MEMi8;

                case "MEMQ": return IformRegister.MEMq;
                case "MEMD": return IformRegister.MEMd;
                case "MEMDQ": return IformRegister.MEMdq;
                case "MEMQQ": return IformRegister.MEMqq;
                case "MEMSD": return IformRegister.MEMsd;
                case "MEMSS": return IformRegister.MEMss;
                case "MEMV": return IformRegister.MEMv;
                case "MEMW": return IformRegister.MEMw;
                case "MEMB": return IformRegister.MEMb;

                case "GPR64Q": return IformRegister.GPR64q;
                case "GPR64": return IformRegister.GPR64;
                case "GPR32D": return IformRegister.GPR32d;
                case "GPR32": return IformRegister.GPR32;
                case "GPR8": return IformRegister.GPR8;
                case "GPRV": return IformRegister.GPRv;
                case "GPR32U8": return IformRegister.GPR32u8;
                case "GPR32U16": return IformRegister.GPR32u16;
                case "GPR32U32": return IformRegister.GPR32u32;
                case "GPR64U64": return IformRegister.GPR64u64;

                case "VGPR64Q": return IformRegister.VGPR64q;
                case "VGPR32D": return IformRegister.VGPR32d;

                case "IMM8": return IformRegister.IMM8;
                case "IMMB": return IformRegister.IMMb;
                case "IMMZ": return IformRegister.IMMz;
                case "CL": return IformRegister.CL;

                case "MASKMSKW": return IformRegister.MASKmskw;

                case "AVX512":
                case "AVX512CD":
                case "VL128":
                case "VL256":
                case "VL512":
                    //Console.WriteLine("WARNING: ParseIformRegister: unknown usage of " + str);
                    return IformRegister.IGNORE;
                default:
                    Console.WriteLine("WARNING: ParseIformRegister: unknown " + str);
                    return IformRegister.UNKNOWN;
            }
        }
        
        static Tuple<Mnemonic, List<IformRegister>, int, bool, float, float> ParseLine(string line)
        {
            var x = line.Split(',');
            if (x.Length != 5)
            {
                Console.WriteLine("ERROR: could not parse " + line);
            }
            var y = x[0].Split('_');
            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(y[0], true);

            List<IformRegister> args = new List<IformRegister>();
            for (int i = 1; i<y.Length; ++i)
            {
                var z = ParseIformRegister(y[i], false);

                if (z == IformRegister.UNKNOWN)
                {
                    //Console.WriteLine("WARNING: could not parse " + line);
                } else if (z == IformRegister.IGNORE)
                {

                } else
                {
                    args.Add(z);
                }
            }

            int n_bits = -1;
            Int32.TryParse(x[1], out n_bits);
            bool use_mask = (x[2] == "yes");

            float throughput = -1;
            float.TryParse(x[3], out throughput);

            float latency = -1;
            float.TryParse(x[4], out latency);

            return Tuple.Create(mnemonic, args, n_bits, use_mask, throughput, latency);
        }

        static public string ToString(List<IformRegister> iform_reg)
        {
            string result = "";
            foreach (IformRegister x in iform_reg)
            {
                result += x.ToString() + " ";
            }
            return result;
        }

        static public void ParseFile(string filename)
        {
            if (File.Exists(filename))
            {
                string[] lines = File.ReadAllLines(filename);
                foreach (string line in lines)
                {
                    if (!line.StartsWith('#'))
                    {
                        var x = ParseLine(line);
                        Console.WriteLine(x.Item1.ToString() + " " + ToString(x.Item2) + ": nbits " + x.Item3 +", mask "+ x.Item4 + ", throughput=" + x.Item5 + ", latency=" + x.Item6);
                    }
                }
            } else
            {
                Console.WriteLine("could not find file " + filename);
            }
        }
    }
}
