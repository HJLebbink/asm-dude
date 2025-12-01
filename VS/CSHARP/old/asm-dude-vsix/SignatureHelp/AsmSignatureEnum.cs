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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude.SignatureHelp
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using AsmDude.Tools;
    using AsmTools;

    public enum AsmSignatureEnum
    {
        NONE,
        UNKNOWN,

        //memory operands
        MEM, M8, M16, M32, M64, M80, M128, M256, M512,

        //register operands
        R8, R16, R32, R64,

        //specific register operands
        REG_AL, REG_AX, REG_EAX, REG_RAX,
        REG_CL, REG_CX, REG_ECX, REG_RCX,
        REG_DX, REG_EDX,
        REG_CS, REG_DS, REG_ES, REG_SS, REG_FS, REG_GS,

        /// <summary>the IMM equal to 0</summary>
        ZERO,

        /// <summary>the IMM equal to 1</summary>
        UNITY,

        IMM,
        IMM8,
        IMM16,
        IMM32,
        IMM64,

        REL8,
        REL16,
        REL32,
        REL64,

        imm_imm,
        imm16_imm,
        imm_imm16,
        imm32_imm,
        imm_imm32,

        NEAR,
        FAR,
        SHORT_ENUM,

        #region FPU
        FPU0,
        FPUREG,
        M2BYTE,
        M14BYTE,
        M28BYTE,
        M94BYTE,
        M108BYTE,
        M512BYTE,
        #endregion

        #region SIMD

        /// <summary>Opmask register</summary>
        K,

        /// <summary> Zero mask. Nasm use {Z} or nothing</summary>
        Z,

        /// <summary>Suppress All Exceptions. Nasm use {SAE} or nothing</summary>
        SAE,

        /// <summary>
        /// Rounding mode. Nasm: use either:
        /// 1] round nearest even {rn-sae};
        /// 2] round down {rd-sae};
        /// 3] round up {ru-sae};
        /// 4] truncate {rz-sae};
        /// or nothing</summary>
        ER,

        /// <summary>memory destination of type [gpr+xmm*scale+offset] </summary>
        VM32X,
        VM64X,

        /// <summary>memory destination of type [gpr+ymm*scale+offset] with scale=1|4|8</summary>
        VM32Y,
        VM64Y,

        /// <summary>memory destination of type [gpr+zmm*scale+offset] with scale=1|4|8</summary>
        VM32Z,
        VM64Z,

        REG_XMM0, MMXREG, mmxreg, XMMREG, YMMREG, ZMMREG,

        /// <summary>Bound register</summary>
        BNDREG,

        /// <summary>vector broadcasted from a 32-bit memory location</summary>
        M32BCST,

        /// <summary>vector broadcasted from a 64-bit memory location</summary>
        M64BCST,
        #endregion

        MEM_OFFSET,
        REG_SREG,
        REG_DREG,
        CR0, CR1, CR2, CR3, CR4, CR5, CR6, CR7, CR8,
    }

    public static class AsmSignatureTools
    {
        public static AsmSignatureEnum[] Parse_Operand_Type_Enum(string str, bool strIsCapitals)
        {
            Contract.Requires(str != null);

            switch (AsmSourceTools.ToCapitals(str, strIsCapitals).Trim())
            {
                #region Memory
                case "M": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "MEM": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M8": return new AsmSignatureEnum[] { AsmSignatureEnum.M8 };
                case "M16": return new AsmSignatureEnum[] { AsmSignatureEnum.M16 };
                case "M32": return new AsmSignatureEnum[] { AsmSignatureEnum.M32 };
                case "M32{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.M32, AsmSignatureEnum.K };
                case "M64": return new AsmSignatureEnum[] { AsmSignatureEnum.M64 };
                case "M64{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.M64, AsmSignatureEnum.K };
                case "M80": return new AsmSignatureEnum[] { AsmSignatureEnum.M80 };
                case "M128": return new AsmSignatureEnum[] { AsmSignatureEnum.M128 };
                case "M128{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.M128, AsmSignatureEnum.K };
                case "M256": return new AsmSignatureEnum[] { AsmSignatureEnum.M256 };
                case "M256{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.M256, AsmSignatureEnum.K };
                case "M512": return new AsmSignatureEnum[] { AsmSignatureEnum.M512 };
                case "M512{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.M512, AsmSignatureEnum.K };

                case "M16&16": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M16&32": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M16&64": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M32&32": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };

                case "M16:16": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M16:32": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                case "M16:64": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };

                case "PTR16:16": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM };
                case "PTR16:32": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM };
                case "PTR16:64": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM };

                #endregion

                #region Register
                case "R8": return new AsmSignatureEnum[] { AsmSignatureEnum.R8 };
                case "R16": return new AsmSignatureEnum[] { AsmSignatureEnum.R16 };
                case "R32": return new AsmSignatureEnum[] { AsmSignatureEnum.R32 };
                case "R64": return new AsmSignatureEnum[] { AsmSignatureEnum.R64 };
                case "R16/R32/R64": return new AsmSignatureEnum[] { AsmSignatureEnum.R16, AsmSignatureEnum.R32, AsmSignatureEnum.R64 };
                case "R32/64": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.R64 };

                case "REG": return new AsmSignatureEnum[] { AsmSignatureEnum.R32 };
                case "AL": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_AL };
                case "AX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_AX };
                case "EAX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_EAX };
                case "RAX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_RAX };

                case "CL": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_CL };
                case "CX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_CX };
                case "ECX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_ECX };
                case "RCX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_RCX };

                case "DX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_DX };
                case "EDX": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_EDX };

                case "CS": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_CS };
                case "DS": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_DS };
                case "ES": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_ES };
                case "SS": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_SS };
                case "FS": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_FS };
                case "GS": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_GS };

                case "REG_SREG":
                case "SREG": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_SREG };
                case "CR0–CR7": return new AsmSignatureEnum[] { AsmSignatureEnum.CR0, AsmSignatureEnum.CR1, AsmSignatureEnum.CR2, AsmSignatureEnum.CR3, AsmSignatureEnum.CR4, AsmSignatureEnum.CR5, AsmSignatureEnum.CR6, AsmSignatureEnum.CR7 };
                case "CR8": return new AsmSignatureEnum[] { AsmSignatureEnum.CR8 };
                case "REG_DREG":
                case "DR0–DR7": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_DREG };

                #endregion

                #region Register or Memory
                case "R/M8": return new AsmSignatureEnum[] { AsmSignatureEnum.R8, AsmSignatureEnum.M8 };
                case "R/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R16, AsmSignatureEnum.M16 };
                case "R/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M32 };
                case "R/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.R64, AsmSignatureEnum.M64 };
                case "R/M32{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M32, AsmSignatureEnum.ER };
                case "R/M64{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.R64, AsmSignatureEnum.M64, AsmSignatureEnum.ER };

                case "REG/M8": return new AsmSignatureEnum[] { AsmSignatureEnum.R8, AsmSignatureEnum.M8 };
                case "REG/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R16, AsmSignatureEnum.M16 };
                case "REG/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M32 };

                case "R16/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R16, AsmSignatureEnum.M16 };
                case "R32/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M16 };
                case "R64/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R64, AsmSignatureEnum.M16 };
                case "R32/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M32 };
                case "R64/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.R64, AsmSignatureEnum.M64 };
                case "R32/M8": return new AsmSignatureEnum[] { AsmSignatureEnum.R32, AsmSignatureEnum.M8 };

                case "R16/R32/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.R16, AsmSignatureEnum.R32, AsmSignatureEnum.M16 };
                #endregion

                #region Constants Immediates
                case "0": return new AsmSignatureEnum[] { AsmSignatureEnum.ZERO };
                case "1": return new AsmSignatureEnum[] { AsmSignatureEnum.UNITY };

                case "MOFFS8": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM8 };
                case "MOFFS16": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM16 };
                case "MOFFS32": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM32 };
                case "MOFFS64": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM64 };

                case "REL8": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM8 };
                case "REL16": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM16 };
                case "REL32": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM32 };
                case "REL64": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM64 };

                case "IMM": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM };
                case "IMM8": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM8 };
                case "IMM16": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM16 };
                case "IMM32": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM32 };
                case "IMM64": return new AsmSignatureEnum[] { AsmSignatureEnum.IMM64 };

                case "IMM:IMM": return new AsmSignatureEnum[] { AsmSignatureEnum.imm_imm };
                case "IMM16:IMM": return new AsmSignatureEnum[] { AsmSignatureEnum.imm16_imm };
                case "IMM:IMM16": return new AsmSignatureEnum[] { AsmSignatureEnum.imm_imm16 };
                case "IMM32:IMM": return new AsmSignatureEnum[] { AsmSignatureEnum.imm32_imm };
                case "IMM:IMM32": return new AsmSignatureEnum[] { AsmSignatureEnum.imm_imm32 };
                #endregion

                #region FPU
                case "ST(I)": return new AsmSignatureEnum[] { AsmSignatureEnum.FPUREG };
                case "ST(0)": return new AsmSignatureEnum[] { AsmSignatureEnum.FPU0 };
                case "ST": return new AsmSignatureEnum[] { AsmSignatureEnum.FPUREG };
                case "M32FP": return new AsmSignatureEnum[] { AsmSignatureEnum.M32, AsmSignatureEnum.FPUREG };
                case "M64FP": return new AsmSignatureEnum[] { AsmSignatureEnum.M64, AsmSignatureEnum.FPUREG };
                case "M80FP": return new AsmSignatureEnum[] { AsmSignatureEnum.M80, AsmSignatureEnum.FPUREG };
                case "M16INT": return new AsmSignatureEnum[] { AsmSignatureEnum.M16 };
                case "M32INT": return new AsmSignatureEnum[] { AsmSignatureEnum.M32 };
                case "M64INT": return new AsmSignatureEnum[] { AsmSignatureEnum.M64 };

                case "M14/28BYTE": return new AsmSignatureEnum[] { AsmSignatureEnum.M14BYTE, AsmSignatureEnum.M28BYTE };
                case "M94/108BYTE": return new AsmSignatureEnum[] { AsmSignatureEnum.M94BYTE, AsmSignatureEnum.M108BYTE };
                case "M2BYTE": return new AsmSignatureEnum[] { AsmSignatureEnum.M2BYTE };
                case "M512BYTE": return new AsmSignatureEnum[] { AsmSignatureEnum.M512BYTE };
                case "M80BCD": return new AsmSignatureEnum[] { AsmSignatureEnum.M80 };
                case "M80DEC": return new AsmSignatureEnum[] { AsmSignatureEnum.M80 };
                #endregion

                #region SIMD
                case "MM": return new AsmSignatureEnum[] { AsmSignatureEnum.MMXREG };
                case "MM/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.MMXREG, AsmSignatureEnum.M32 };
                case "MM/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.MMXREG, AsmSignatureEnum.M64 };
                case "MM/MEM": return new AsmSignatureEnum[] { AsmSignatureEnum.MMXREG, AsmSignatureEnum.M64 };

                case "Z": return new AsmSignatureEnum[] { AsmSignatureEnum.Z };
                case "K": return new AsmSignatureEnum[] { AsmSignatureEnum.K };
                case "K+1": return new AsmSignatureEnum[] { AsmSignatureEnum.K };
                case "K{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.K };
                case "SAE": return new AsmSignatureEnum[] { AsmSignatureEnum.SAE };
                case "ER": return new AsmSignatureEnum[] { AsmSignatureEnum.ER };

                case "K/M8": return new AsmSignatureEnum[] { AsmSignatureEnum.K, AsmSignatureEnum.M8 };
                case "K/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.K, AsmSignatureEnum.M16 };
                case "K/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.K, AsmSignatureEnum.M32 };
                case "K/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.K, AsmSignatureEnum.M64 };

                case "VM32X": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32X };
                case "VM64X": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64X };
                case "VM32Y": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32Y };
                case "VM64Y": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64Y };
                case "VM32Z": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32Z };
                case "VM64Z": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64Z };

                case "VM32X{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32X, AsmSignatureEnum.K };
                case "VM64X{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64X, AsmSignatureEnum.K };
                case "VM32Y{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32Y, AsmSignatureEnum.K };
                case "VM64Y{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64Y, AsmSignatureEnum.K };
                case "VM32Z{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM32Z, AsmSignatureEnum.K };
                case "VM64Z{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.VM64Z, AsmSignatureEnum.K };

                case "XMM": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG };
                case "XMM_ZERO": return new AsmSignatureEnum[] { AsmSignatureEnum.REG_XMM0 };
                case "XMM{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.K };
                case "XMM{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "XMM/M8": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M8 };
                case "XMM/M16": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M16 };
                case "XMM/M16{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M16 };
                case "XMM/M32": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M32, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "XMM/M32{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M32, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "XMM/M32{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M32, AsmSignatureEnum.ER };
                case "XMM/M32{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M32, AsmSignatureEnum.SAE };
                case "XMM/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M64 };
                case "XMM/M64{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M64, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "XMM/M64{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M64, AsmSignatureEnum.ER };
                case "XMM/M64{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M64, AsmSignatureEnum.SAE };
                case "XMM/M64/M32BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M64, AsmSignatureEnum.M32BCST };
                case "XMM/M128": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M128 };
                case "XMM/M128{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M128, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "XMM/M128/M32BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M128, AsmSignatureEnum.M32BCST };
                case "XMM/M128/M64BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.XMMREG, AsmSignatureEnum.M128, AsmSignatureEnum.M64BCST };

                case "YMM": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG };
                case "YMM{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.K };
                case "YMM{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "YMM/M256": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256 };
                case "YMM/M256{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.SAE };
                case "YMM/M256{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "YMM/M256/M32BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.M32BCST };
                case "YMM/M256/M32BCST{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.M32BCST, AsmSignatureEnum.ER };
                case "YMM/M256/M32BCST{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.M32BCST, AsmSignatureEnum.SAE };
                case "YMM/M256/M64BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.YMMREG, AsmSignatureEnum.M256, AsmSignatureEnum.M64BCST };

                case "ZMM": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG };
                case "ZMM{K}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.K };
                case "ZMM{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "ZMM{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.SAE };
                case "ZMM/M512": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "ZMM/M512{K}{Z}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.K, AsmSignatureEnum.Z };
                case "ZMM/M512/M32BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M32BCST };
                case "ZMM/M512/M32BCST{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M32BCST, AsmSignatureEnum.ER };
                case "ZMM/M512/M32BCST{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M32BCST, AsmSignatureEnum.SAE };
                case "ZMM/M512/M64BCST": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M64BCST };
                case "ZMM/M512/M64BCST{ER}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M64BCST, AsmSignatureEnum.ER };
                case "ZMM/M512/M64BCST{SAE}": return new AsmSignatureEnum[] { AsmSignatureEnum.ZMMREG, AsmSignatureEnum.M512, AsmSignatureEnum.M64BCST, AsmSignatureEnum.SAE };

                #endregion

                #region Misc
                case "NEAR": return new AsmSignatureEnum[] { AsmSignatureEnum.NEAR };
                case "FAR": return new AsmSignatureEnum[] { AsmSignatureEnum.FAR };
                case "SHORT": return new AsmSignatureEnum[] { AsmSignatureEnum.SHORT_ENUM };
                case "MEM_OFFS": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM_OFFSET };

                case "BND": return new AsmSignatureEnum[] { AsmSignatureEnum.BNDREG };
                case "BND/M64": return new AsmSignatureEnum[] { AsmSignatureEnum.BNDREG, AsmSignatureEnum.M64 };
                case "BND/M128": return new AsmSignatureEnum[] { AsmSignatureEnum.BNDREG, AsmSignatureEnum.M128 };
                case "MIB": return new AsmSignatureEnum[] { AsmSignatureEnum.MEM };
                #endregion

                case "NONE": return new AsmSignatureEnum[] { AsmSignatureEnum.NONE };

                default:
                    AsmDudeToolsStatic.Output_INFO("AsmSignatureTools:parseOperandTypeEnum: unknown content " + str);
                    return new AsmSignatureEnum[] { AsmSignatureEnum.UNKNOWN };
            }
        }

        /// <summary>Get brief description of the operand</summary>
        public static string Get_Doc(AsmSignatureEnum operandType)
        {
            switch (operandType)
            {
                case AsmSignatureEnum.MEM: return "memory operand";
                case AsmSignatureEnum.M8: return "8-bits memory operand";
                case AsmSignatureEnum.M16: return "16-bits memory operand";
                case AsmSignatureEnum.M32: return "32-bits memory operand";
                case AsmSignatureEnum.M64: return "64-bits memory operand";
                case AsmSignatureEnum.M80: return "80-bits memory operand";
                case AsmSignatureEnum.M128: return "128-bits memory operand";
                case AsmSignatureEnum.M256: return "256-bits memory operand";
                case AsmSignatureEnum.M512: return "512-bits memory operand";
                case AsmSignatureEnum.R8: return "8-bits register";
                case AsmSignatureEnum.R16: return "16-bits register";
                case AsmSignatureEnum.R32: return "32-bits register";
                case AsmSignatureEnum.R64: return "64-bits register";
                case AsmSignatureEnum.REG_AL: return "AL register";
                case AsmSignatureEnum.REG_AX: return "AX register";
                case AsmSignatureEnum.REG_EAX: return "EAX register";
                case AsmSignatureEnum.REG_RAX: return "RAX register";
                case AsmSignatureEnum.REG_CL: return "CL register";
                case AsmSignatureEnum.REG_CX: return "CX register";
                case AsmSignatureEnum.REG_ECX: return "ECX register";
                case AsmSignatureEnum.REG_RCX: return "RCX register";
                case AsmSignatureEnum.REG_DX: return "DX register";
                case AsmSignatureEnum.REG_EDX: return "EDX register";
                case AsmSignatureEnum.REG_CS: return "CS register";
                case AsmSignatureEnum.REG_DS: return "DS register";
                case AsmSignatureEnum.REG_ES: return "ES register";
                case AsmSignatureEnum.REG_SS: return "SS register";
                case AsmSignatureEnum.REG_FS: return "FS register";
                case AsmSignatureEnum.REG_GS: return "GS register";
                case AsmSignatureEnum.IMM: return "immediate constant";
                case AsmSignatureEnum.IMM8: return "8-bits immediate constant";
                case AsmSignatureEnum.IMM16: return "16-bits immediate constant";
                case AsmSignatureEnum.IMM32: return "32-bits immediate constant";
                case AsmSignatureEnum.IMM64: return "64-bits immediate constant";
                case AsmSignatureEnum.imm_imm: return "immediate constants";
                case AsmSignatureEnum.imm16_imm: return "immediate constants";
                case AsmSignatureEnum.imm_imm16: return "immediate constants";
                case AsmSignatureEnum.imm32_imm: return "immediate constants";
                case AsmSignatureEnum.imm_imm32: return "immediate constants";
                case AsmSignatureEnum.NEAR: return "near ptr";
                case AsmSignatureEnum.FAR: return "far ptr";
                case AsmSignatureEnum.SHORT_ENUM: return "short ptr";
                case AsmSignatureEnum.UNITY: return "immediate value 1";
                case AsmSignatureEnum.ZERO: return "immediate value 0";

                case AsmSignatureEnum.SAE: return "Optional Suppress All Exceptions {SAE}";
                case AsmSignatureEnum.ER: return "Optional Rounding Mode {RN-SAE}/{RU-SAE}/{RD-SAE}/{RZ-SAE}";
                case AsmSignatureEnum.Z: return "Optional Zero Mask {Z}";

                case AsmSignatureEnum.REG_XMM0: return "XMM0 register";
                case AsmSignatureEnum.XMMREG: return "xmm register";
                case AsmSignatureEnum.YMMREG: return "ymm register";
                case AsmSignatureEnum.ZMMREG: return "zmm register";

                case AsmSignatureEnum.K: return "mask register";

                case AsmSignatureEnum.M32BCST: return "vector broadcasted from a 32-bit memory location";
                case AsmSignatureEnum.M64BCST: return "vector broadcasted from a 64-bit memory location";
                case AsmSignatureEnum.MEM_OFFSET: return "memory offset";
                case AsmSignatureEnum.REG_SREG: return "segment register";
                case AsmSignatureEnum.REG_DREG: return "debug register";
                default:
                    AsmDudeToolsStatic.Output_WARNING("SignatureStore:getDoc: add " + operandType);
                    return operandType.ToString();
                    break;
            }
        }

        public static string ToString(IList<AsmSignatureEnum> list, string concat)
        {
            Contract.Requires(list != null);

            int nOperands = list.Count;
            if (nOperands == 0)
            {
                return string.Empty;
            }
            else if (nOperands == 1)
            {
                return ToString(list[0]);
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < nOperands; ++i)
                {
                    sb.Append(ToString(list[i]));
                    if (i < nOperands - 1)
                    {
                        sb.Append(concat);
                    }
                }
                return sb.ToString();
            }
        }

        public static string ToString(AsmSignatureEnum operandType)
        {
            switch (operandType)
            {
                case AsmSignatureEnum.REG_AL: return "AL";
                case AsmSignatureEnum.REG_AX: return "AX";
                case AsmSignatureEnum.REG_EAX: return "EAX";
                case AsmSignatureEnum.REG_RAX: return "RAX";
                case AsmSignatureEnum.REG_CL: return "CL";
                case AsmSignatureEnum.REG_CX: return "CX";
                case AsmSignatureEnum.REG_ECX: return "ECX";
                case AsmSignatureEnum.REG_RCX: return "RCX";
                case AsmSignatureEnum.REG_DX: return "DX";
                case AsmSignatureEnum.REG_EDX: return "EDX";
                case AsmSignatureEnum.REG_CS: return "CS";
                case AsmSignatureEnum.REG_DS: return "DS";
                case AsmSignatureEnum.REG_ES: return "ES";
                case AsmSignatureEnum.REG_SS: return "SS";
                case AsmSignatureEnum.REG_FS: return "FS";
                case AsmSignatureEnum.REG_GS: return "GS";
                case AsmSignatureEnum.IMM: return "IMM";
                case AsmSignatureEnum.IMM8: return "IMM8";
                case AsmSignatureEnum.IMM16: return "IMM16";
                case AsmSignatureEnum.IMM32: return "IMM32";
                case AsmSignatureEnum.IMM64: return "IMM64";
                case AsmSignatureEnum.imm_imm: return "imm:imm";
                case AsmSignatureEnum.imm16_imm: return "imm16:imm";
                case AsmSignatureEnum.imm_imm16: return "imm:imm16";
                case AsmSignatureEnum.imm32_imm: return "imm32:imm";
                case AsmSignatureEnum.imm_imm32: return "imm:imm32";
                case AsmSignatureEnum.NEAR: return "near";
                case AsmSignatureEnum.FAR: return "far";
                case AsmSignatureEnum.SHORT_ENUM: return "short";
                case AsmSignatureEnum.UNITY: return "unity 1";
                case AsmSignatureEnum.Z: return "z";
                case AsmSignatureEnum.ER: return "er";

                case AsmSignatureEnum.REG_XMM0: return "XMM0";
                case AsmSignatureEnum.XMMREG: return "XMM";
                case AsmSignatureEnum.YMMREG: return "YMM";
                case AsmSignatureEnum.ZMMREG: return "ZMM";

                case AsmSignatureEnum.VM32X: return "xmem32";
                case AsmSignatureEnum.VM64X: return "xmem64";
                case AsmSignatureEnum.VM32Y: return "ymem32";
                case AsmSignatureEnum.VM64Y: return "ymem64";
                case AsmSignatureEnum.VM32Z: return "zmem32";
                case AsmSignatureEnum.VM64Z: return "zmem64";

                case AsmSignatureEnum.M32BCST: return "M32bcst";
                case AsmSignatureEnum.M64BCST: return "M64bcst";
                case AsmSignatureEnum.MEM_OFFSET: return "mem_offs";
                case AsmSignatureEnum.REG_SREG: return "segment register";
                case AsmSignatureEnum.REG_DREG: return "debug register";

                case AsmSignatureEnum.MMXREG: return "XMM";
                case AsmSignatureEnum.BNDREG: return "BND";

                case AsmSignatureEnum.FPUREG: return "fpureg";

                case AsmSignatureEnum.K: return "K";
                case AsmSignatureEnum.SAE: return "{SAE}";

                default:
                    // AsmDudeToolsStatic.Output_WARNING("AsmSignatureTools:ToString: " + operandType);
                    return operandType.ToString();
            }
        }

        public static bool Is_Allowed_Operand(Operand op, AsmSignatureEnum operandType)
        {
            Contract.Requires(op != null);

            switch (operandType)
            {
                case AsmSignatureEnum.UNKNOWN: return true;
                case AsmSignatureEnum.MEM: return op.IsMem;
                case AsmSignatureEnum.M8: return op.IsMem && op.NBits == 8;
                case AsmSignatureEnum.M16: return op.IsMem && op.NBits == 16;
                case AsmSignatureEnum.M32: return op.IsMem && op.NBits == 32;
                case AsmSignatureEnum.M64: return op.IsMem && op.NBits == 64;
                case AsmSignatureEnum.M80: return op.IsMem && op.NBits == 80;
                case AsmSignatureEnum.M128: return op.IsMem && op.NBits == 128;
                case AsmSignatureEnum.M256: return op.IsMem && op.NBits == 256;
                case AsmSignatureEnum.M512: return op.IsMem && op.NBits == 512;

                case AsmSignatureEnum.R8: return op.IsReg && op.NBits == 8;
                case AsmSignatureEnum.R16: return op.IsReg && op.NBits == 16;
                case AsmSignatureEnum.R32: return op.IsReg && op.NBits == 32;
                case AsmSignatureEnum.R64: return op.IsReg && op.NBits == 64;
                case AsmSignatureEnum.REG_AL: return op.IsReg && op.Rn == Rn.AL;
                case AsmSignatureEnum.REG_AX: return op.IsReg && op.Rn == Rn.AX;
                case AsmSignatureEnum.REG_EAX: return op.IsReg && op.Rn == Rn.EAX;
                case AsmSignatureEnum.REG_RAX: return op.IsReg && op.Rn == Rn.RAX;
                case AsmSignatureEnum.REG_CL: return op.IsReg && op.Rn == Rn.CL;
                case AsmSignatureEnum.REG_CX: return op.IsReg && op.Rn == Rn.CX;
                case AsmSignatureEnum.REG_ECX: return op.IsReg && op.Rn == Rn.ECX;
                case AsmSignatureEnum.REG_RCX: return op.IsReg && op.Rn == Rn.RCX;
                case AsmSignatureEnum.REG_DX: return op.IsReg && op.Rn == Rn.DX;
                case AsmSignatureEnum.REG_EDX: return op.IsReg && op.Rn == Rn.EDX;
                case AsmSignatureEnum.REG_XMM0: return op.IsReg && op.Rn == Rn.XMM0;

                case AsmSignatureEnum.REG_CS: return op.IsReg && op.Rn == Rn.CS;
                case AsmSignatureEnum.REG_DS: return op.IsReg && op.Rn == Rn.DS;
                case AsmSignatureEnum.REG_ES: return op.IsReg && op.Rn == Rn.ES;
                case AsmSignatureEnum.REG_SS: return op.IsReg && op.Rn == Rn.SS;
                case AsmSignatureEnum.REG_FS: return op.IsReg && op.Rn == Rn.FS;
                case AsmSignatureEnum.REG_GS: return op.IsReg && op.Rn == Rn.GS;

                case AsmSignatureEnum.IMM: return op.IsImm;
                case AsmSignatureEnum.IMM8: return op.IsImm && op.NBits == 8;
                case AsmSignatureEnum.IMM16: return op.IsImm && op.NBits == 16;
                case AsmSignatureEnum.IMM32: return op.IsImm && op.NBits == 32;
                case AsmSignatureEnum.IMM64: return op.IsImm && op.NBits == 64;

                case AsmSignatureEnum.imm_imm: return true;
                case AsmSignatureEnum.imm16_imm: return true;
                case AsmSignatureEnum.imm_imm16: return true;
                case AsmSignatureEnum.imm32_imm: return true;
                case AsmSignatureEnum.imm_imm32: return true;

                case AsmSignatureEnum.NEAR: return op.IsImm;
                case AsmSignatureEnum.FAR: return op.IsImm;
                case AsmSignatureEnum.SHORT_ENUM: return op.IsImm;
                case AsmSignatureEnum.UNITY: return op.IsImm && (op.Imm == 1);

                case AsmSignatureEnum.Z: return false;
                case AsmSignatureEnum.ER: return false;
                case AsmSignatureEnum.SAE: return false;

                case AsmSignatureEnum.K: return op.IsReg && RegisterTools.IsOpmaskRegister(op.Rn);
                case AsmSignatureEnum.XMMREG: return op.IsReg && RegisterTools.IsSseRegister(op.Rn);
                case AsmSignatureEnum.YMMREG: return op.IsReg && RegisterTools.IsAvxRegister(op.Rn);
                case AsmSignatureEnum.ZMMREG: return op.IsReg && RegisterTools.IsAvx512Register(op.Rn);

                case AsmSignatureEnum.M32BCST: return op.IsMem && op.NBits == 32;
                case AsmSignatureEnum.M64BCST: return op.IsMem && op.NBits == 64;
                case AsmSignatureEnum.MEM_OFFSET: return op.IsImm;
                case AsmSignatureEnum.REG_SREG: return op.IsReg && RegisterTools.IsSegmentRegister(op.Rn);
                case AsmSignatureEnum.CR0: return op.IsReg && (op.Rn == Rn.CR0);
                case AsmSignatureEnum.CR1: return op.IsReg && (op.Rn == Rn.CR1);
                case AsmSignatureEnum.CR2: return op.IsReg && (op.Rn == Rn.CR2);
                case AsmSignatureEnum.CR3: return op.IsReg && (op.Rn == Rn.CR3);
                case AsmSignatureEnum.CR4: return op.IsReg && (op.Rn == Rn.CR4);
                case AsmSignatureEnum.CR5: return op.IsReg && (op.Rn == Rn.CR5);
                case AsmSignatureEnum.CR6: return op.IsReg && (op.Rn == Rn.CR6);
                case AsmSignatureEnum.CR7: return op.IsReg && (op.Rn == Rn.CR7);
                case AsmSignatureEnum.CR8: return op.IsReg && (op.Rn == Rn.CR8);
                case AsmSignatureEnum.REG_DREG: return op.IsReg && RegisterTools.IsDebugRegister(op.Rn);
                case AsmSignatureEnum.BNDREG: return op.IsReg && RegisterTools.IsBoundRegister(op.Rn);

                default:
                    AsmDudeToolsStatic.Output_WARNING("AsmSignatureTools:isAllowed: add " + operandType);
                    break;
            }
            return true;
        }

        public static bool Is_Allowed_Misc(string misc, ISet<AsmSignatureEnum> allowedOperands)
        {
            Contract.Requires(misc != null);
            Contract.Requires(misc == misc.ToUpperInvariant());
            Contract.Requires(allowedOperands != null);

            switch (misc)
            {
                case "PTR":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M16))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M32))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M64))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M128))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M256))
                    {
                        return true;
                    }

                    if (allowedOperands.Contains(AsmSignatureEnum.M512))
                    {
                        return true;
                    }

                    break;

                case "BYTE":
                case "SBYTE":
                    if (allowedOperands.Contains(AsmSignatureEnum.M8))
                    {
                        return true;
                    }

                    break;
                case "WORD":
                case "SWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.M16))
                    {
                        return true;
                    }

                    break;
                case "DWORD":
                case "SDWORD":
                case "REAL4":
                    if (allowedOperands.Contains(AsmSignatureEnum.M32))
                    {
                        return true;
                    }

                    break;
                case "QWORD":
                case "MMWORD":
                case "REAL8":
                    if (allowedOperands.Contains(AsmSignatureEnum.M64))
                    {
                        return true;
                    }

                    break;
                case "TWORD":
                case "TBYTE":
                case "REAL10":
                    if (allowedOperands.Contains(AsmSignatureEnum.M80))
                    {
                        return true;
                    }

                    break;
                case "XMMWORD":
                case "OWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.M128))
                    {
                        return true;
                    }

                    break;
                case "YMMWORD":
                case "YWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.M256))
                    {
                        return true;
                    }

                    break;
                case "ZWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.M512))
                    {
                        return true;
                    }

                    break;
                default: break;
            }
            return false;
        }

        public static bool Is_Allowed_Reg(Rn regName, ISet<AsmSignatureEnum> allowedOperands)
        {
            Contract.Requires(allowedOperands != null);

            RegisterType type = RegisterTools.GetRegisterType(regName);
            switch (type)
            {
                case RegisterType.UNKNOWN:
                    AsmDudeToolsStatic.Output_INFO("AsmSignatureTools: isAllowedReg: registername " + regName + " could not be classified");
                    break;
                case RegisterType.BIT8:
                    if (allowedOperands.Contains(AsmSignatureEnum.R8))
                    {
                        return true;
                    }

                    if ((regName == Rn.AL) && allowedOperands.Contains(AsmSignatureEnum.REG_AL))
                    {
                        return true;
                    }

                    if ((regName == Rn.CL) && allowedOperands.Contains(AsmSignatureEnum.REG_CL))
                    {
                        return true;
                    }

                    break;
                case RegisterType.BIT16:
                    if (allowedOperands.Contains(AsmSignatureEnum.R16))
                    {
                        return true;
                    }

                    if ((regName == Rn.AX) && allowedOperands.Contains(AsmSignatureEnum.REG_AX))
                    {
                        return true;
                    }

                    if ((regName == Rn.CX) && allowedOperands.Contains(AsmSignatureEnum.REG_CX))
                    {
                        return true;
                    }

                    if ((regName == Rn.DX) && allowedOperands.Contains(AsmSignatureEnum.REG_DX))
                    {
                        return true;
                    }

                    break;
                case RegisterType.BIT32:
                    if (allowedOperands.Contains(AsmSignatureEnum.R32))
                    {
                        return true;
                    }

                    if ((regName == Rn.EAX) && allowedOperands.Contains(AsmSignatureEnum.REG_EAX))
                    {
                        return true;
                    }

                    if ((regName == Rn.ECX) && allowedOperands.Contains(AsmSignatureEnum.REG_ECX))
                    {
                        return true;
                    }

                    if ((regName == Rn.EDX) && allowedOperands.Contains(AsmSignatureEnum.REG_EDX))
                    {
                        return true;
                    }

                    break;
                case RegisterType.BIT64:
                    if (allowedOperands.Contains(AsmSignatureEnum.R64))
                    {
                        return true;
                    }

                    if ((regName == Rn.RAX) && allowedOperands.Contains(AsmSignatureEnum.REG_RAX))
                    {
                        return true;
                    }

                    if ((regName == Rn.RCX) && allowedOperands.Contains(AsmSignatureEnum.REG_RCX))
                    {
                        return true;
                    }

                    break;
                case RegisterType.MMX:
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXREG))
                    {
                        return true;
                    }

                    break;
                case RegisterType.XMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMREG))
                    {
                        return true;
                    }

                    if ((regName == Rn.XMM0) && allowedOperands.Contains(AsmSignatureEnum.REG_XMM0))
                    {
                        return true;
                    }

                    break;
                case RegisterType.YMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.YMMREG))
                    {
                        return true;
                    }

                    break;
                case RegisterType.ZMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.ZMMREG))
                    {
                        return true;
                    }

                    break;
                case RegisterType.OPMASK:
                    if (allowedOperands.Contains(AsmSignatureEnum.K))
                    {
                        return true;
                    }

                    break;
                case RegisterType.SEGMENT:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG_SREG))
                    {
                        return true;
                    }

                    switch (regName)
                    {
                        case Rn.CS: if (allowedOperands.Contains(AsmSignatureEnum.REG_CS)) { return true; } break;
                        case Rn.DS: if (allowedOperands.Contains(AsmSignatureEnum.REG_DS)) { return true; } break;
                        case Rn.ES: if (allowedOperands.Contains(AsmSignatureEnum.REG_ES)) { return true; } break;
                        case Rn.SS: if (allowedOperands.Contains(AsmSignatureEnum.REG_SS)) { return true; } break;
                        case Rn.FS: if (allowedOperands.Contains(AsmSignatureEnum.REG_FS)) { return true; } break;
                        case Rn.GS: if (allowedOperands.Contains(AsmSignatureEnum.REG_GS)) { return true; } break;
                    }
                    break;
                case RegisterType.CONTROL:
                    if ((regName == Rn.CR0) && allowedOperands.Contains(AsmSignatureEnum.CR0))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR1) && allowedOperands.Contains(AsmSignatureEnum.CR1))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR2) && allowedOperands.Contains(AsmSignatureEnum.CR2))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR3) && allowedOperands.Contains(AsmSignatureEnum.CR3))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR4) && allowedOperands.Contains(AsmSignatureEnum.CR4))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR5) && allowedOperands.Contains(AsmSignatureEnum.CR5))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR6) && allowedOperands.Contains(AsmSignatureEnum.CR6))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR7) && allowedOperands.Contains(AsmSignatureEnum.CR7))
                    {
                        return true;
                    }

                    if ((regName == Rn.CR8) && allowedOperands.Contains(AsmSignatureEnum.CR8))
                    {
                        return true;
                    }

                    break;
                case RegisterType.DEBUG:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG_DREG))
                    {
                        return true;
                    }

                    break;
                case RegisterType.BOUND:
                    if (allowedOperands.Contains(AsmSignatureEnum.BNDREG))
                    {
                        return true;
                    }

                    break;
                default:
                    break;
            }
            return false;
        }
    }
}
