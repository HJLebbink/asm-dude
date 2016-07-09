// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

using AsmDude.Tools;
using AsmTools;
using System.Collections.Generic;
using System.Text;

namespace AsmDude.SignatureHelp {

    public enum AsmSignatureEnum : byte {
        none,
        UNKNOWN,

        MEM, MEM8, MEM16, MEM32, MEM64, MEM80, MEM128, MEM256, MEM512,
        REG8, REG16, REG32, REG64,
        REG_AL, REG_AX, REG_EAX, REG_RAX,
        REG_CL, REG_CX, REG_ECX, REG_RCX,
        REG_DX, REG_EDX,
        REG_CS, REG_DS, REG_ES, REG_SS, REG_FS, REG_GS,

        IMM, IMM8, IMM16, IMM32, IMM64,
        imm_imm, imm16_imm, imm_imm16, imm32_imm, imm_imm32,


        RM8, RM16, RM32, RM64,
        sbyteword, sbytedword,
        sbyteword16, sbytedword16,
        sbytedword32,
        sbytedword64,
        udword, sdword,

        near, far, short_ENUM,
        unity,

        #region FPU
        fpureg, to, fpu0,
        #endregion

        #region SIMD
        //TODO what is the difference between mask and kreg?
        mask, kreg,
        krm8, krm16, krm32, krm64,

        z, sae, er,
        xmem32, xmem64, ymem32, ymem64, zmem32, zmem64,

        REG_XMM0, MMXREG, xmmreg, ymmreg, zmmreg,

        /// <summary>Bound register</summary>
        bndreg,

        MMXRM, MMXRM64,

        XMMRM, XMMRM8, XMMRM16, XMMRM32, XMMRM64, XMMRM128,
        YMMRM, YMMRM256,
        ZMMRM512,
        b32, b64,
        #endregion

        mem_offs,
        reg_sreg, reg_creg, reg_dreg, reg_treg,
        reg32na
    }

    public static class AsmSignatureTools {

        public static AsmSignatureEnum parseOperandTypeEnum(string str) {
            switch (str.ToUpper()) {
                case "NONE": return AsmSignatureEnum.none;

                case "MEM": return AsmSignatureEnum.MEM;
                case "MEM8": return AsmSignatureEnum.MEM8;
                case "MEM16": return AsmSignatureEnum.MEM16;
                case "MEM32": return AsmSignatureEnum.MEM32;
                case "MEM64": return AsmSignatureEnum.MEM64;
                case "MEM80": return AsmSignatureEnum.MEM80;
                case "MEM128": return AsmSignatureEnum.MEM128;
                case "MEM256": return AsmSignatureEnum.MEM256;
                case "MEM512": return AsmSignatureEnum.MEM512;

                case "REG8": return AsmSignatureEnum.REG8;
                case "REG16": return AsmSignatureEnum.REG16;
                case "REG32": return AsmSignatureEnum.REG32;
                case "REG64": return AsmSignatureEnum.REG64;

                case "REG_AL": return AsmSignatureEnum.REG_AL;
                case "REG_AX": return AsmSignatureEnum.REG_AX;
                case "REG_EAX": return AsmSignatureEnum.REG_EAX;
                case "REG_RAX": return AsmSignatureEnum.REG_RAX;

                case "REG_CL": return AsmSignatureEnum.REG_CL;
                case "REG_CX": return AsmSignatureEnum.REG_CX;
                case "REG_ECX": return AsmSignatureEnum.REG_ECX;
                case "REG_RCX": return AsmSignatureEnum.REG_RCX;

                case "REG_DX": return AsmSignatureEnum.REG_DX;
                case "REG_EDX": return AsmSignatureEnum.REG_EDX;

                case "REG_CS": return AsmSignatureEnum.REG_CS;
                case "REG_DS": return AsmSignatureEnum.REG_DS;
                case "REG_ES": return AsmSignatureEnum.REG_ES;
                case "REG_SS": return AsmSignatureEnum.REG_SS;
                case "REG_FS": return AsmSignatureEnum.REG_FS;
                case "REG_GS": return AsmSignatureEnum.REG_GS;

                case "IMM": return AsmSignatureEnum.IMM;
                case "IMM8": return AsmSignatureEnum.IMM8;
                case "IMM16": return AsmSignatureEnum.IMM16;
                case "IMM32": return AsmSignatureEnum.IMM32;
                case "IMM64": return AsmSignatureEnum.IMM64;

                case "IMM:IMM": return AsmSignatureEnum.imm_imm;
                case "IMM16:IMM": return AsmSignatureEnum.imm16_imm;
                case "IMM:IMM16": return AsmSignatureEnum.imm_imm16;
                case "IMM32:IMM": return AsmSignatureEnum.imm32_imm;
                case "IMM:IMM32": return AsmSignatureEnum.imm_imm32;

                case "RM8": return AsmSignatureEnum.RM8;
                case "RM16": return AsmSignatureEnum.RM16;
                case "RM32": return AsmSignatureEnum.RM32;
                case "RM64": return AsmSignatureEnum.RM64;

                case "SBYTEWORD": return AsmSignatureEnum.sbyteword;
                case "SBYTEWORD16": return AsmSignatureEnum.sbyteword16;

                case "SBYTEDWORD": return AsmSignatureEnum.sbytedword;
                case "SBYTEDWORD16": return AsmSignatureEnum.sbytedword16;
                case "SBYTEDWORD32": return AsmSignatureEnum.sbytedword32;
                case "SBYTEDWORD64": return AsmSignatureEnum.sbytedword64;
                case "UDWORD": return AsmSignatureEnum.udword;
                case "SDWORD": return AsmSignatureEnum.sdword;

                case "NEAR": return AsmSignatureEnum.near;
                case "FAR": return AsmSignatureEnum.far;
                case "SHORT": return AsmSignatureEnum.short_ENUM;
                case "UNITY": return AsmSignatureEnum.unity;

                case "FPU0": return AsmSignatureEnum.fpu0;
                case "FPUREG": return AsmSignatureEnum.fpureg;
                case "TO": return AsmSignatureEnum.to;

                case "MASK": return AsmSignatureEnum.mask;
                case "Z": return AsmSignatureEnum.z;
                case "KREG": return AsmSignatureEnum.kreg;
                case "SAE": return AsmSignatureEnum.sae;
                case "ER": return AsmSignatureEnum.er;

                case "KRM8": return AsmSignatureEnum.krm8;
                case "KRM16": return AsmSignatureEnum.krm16;
                case "KRM32": return AsmSignatureEnum.krm32;
                case "KRM64": return AsmSignatureEnum.krm64;

                case "XMEM32": return AsmSignatureEnum.xmem32;
                case "XMEM64": return AsmSignatureEnum.xmem64;
                case "YMEM32": return AsmSignatureEnum.ymem32;
                case "YMEM64": return AsmSignatureEnum.ymem64;
                case "ZMEM32": return AsmSignatureEnum.zmem32;
                case "ZMEM64": return AsmSignatureEnum.zmem64;

                case "XMM0": return AsmSignatureEnum.REG_XMM0;
                case "MMXREG": return AsmSignatureEnum.MMXREG;
                case "XMMREG":
                case "XMMREG*": return AsmSignatureEnum.xmmreg;
                case "YMMREG":
                case "YMMREG*": return AsmSignatureEnum.ymmreg;
                case "ZMMREG":
                case "ZMMREG*": return AsmSignatureEnum.zmmreg;

                case "BNDREG": return AsmSignatureEnum.bndreg;

                case "MMXRM": return AsmSignatureEnum.MMXRM;
                case "MMXRM64": return AsmSignatureEnum.MMXRM64;

                case "XMMRM": return AsmSignatureEnum.XMMRM;
                case "XMMRM8": return AsmSignatureEnum.XMMRM8;
                case "XMMRM16": return AsmSignatureEnum.XMMRM16;
                case "XMMRM32":
                case "XMMRM32*": return AsmSignatureEnum.XMMRM32;
                case "XMMRM64*":
                case "XMMRM64": return AsmSignatureEnum.XMMRM64;
                case "XMMRM128":
                case "XMMRM128*": return AsmSignatureEnum.XMMRM128;

                case "YMMRM": return AsmSignatureEnum.YMMRM;
                case "YMMRM256":
                case "YMMRM256*": return AsmSignatureEnum.YMMRM256;

                case "ZMMRM512": return AsmSignatureEnum.ZMMRM512;

                case "B32": return AsmSignatureEnum.b32;
                case "B64": return AsmSignatureEnum.b64;

                case "REG_SREG": return AsmSignatureEnum.reg_sreg;
                case "REG_CREG": return AsmSignatureEnum.reg_creg;
                case "REG_DREG": return AsmSignatureEnum.reg_dreg;
                case "REG_TREG": return AsmSignatureEnum.reg_treg;
                case "REG32NA": return AsmSignatureEnum.reg32na;
                case "MEM_OFFS": return AsmSignatureEnum.mem_offs;

                default:
                    AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource:parseOperandTypeEnum: unknown content " + str);
                    return AsmSignatureEnum.UNKNOWN;
            }
        }

        public static string getDoc(AsmSignatureEnum operandType) {
            switch (operandType) {
                case AsmSignatureEnum.MEM: return "memory operand";
                case AsmSignatureEnum.MEM8: return "8-bits memory operand";
                case AsmSignatureEnum.MEM16: return "16-bits memory operand";
                case AsmSignatureEnum.MEM32: return "32-bits memory operand";
                case AsmSignatureEnum.MEM64: return "64-bits memory operand";
                case AsmSignatureEnum.MEM80: return "80-bits memory operand";
                case AsmSignatureEnum.MEM128: return "128-bits memory operand";
                case AsmSignatureEnum.MEM256: return "256-bits memory operand";
                case AsmSignatureEnum.MEM512: return "512-bits memory operand";
                case AsmSignatureEnum.REG8: return "8-bits register";
                case AsmSignatureEnum.REG16: return "16-bits register";
                case AsmSignatureEnum.REG32: return "32-bits register";
                case AsmSignatureEnum.REG64: return "64-bits register";
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
                case AsmSignatureEnum.imm_imm: return "immediate constant";
                case AsmSignatureEnum.imm16_imm: return "immediate constant";
                case AsmSignatureEnum.imm_imm16: return "immediate constant";
                case AsmSignatureEnum.imm32_imm: return "immediate constant";
                case AsmSignatureEnum.imm_imm32: return "immediate constant";
                case AsmSignatureEnum.RM8: return "8-bits register or memory operand";
                case AsmSignatureEnum.RM16: return "16-bits register or memory operand";
                case AsmSignatureEnum.RM32: return "32-bits register or memory operand";
                case AsmSignatureEnum.RM64: return "64-bits register or memory operand";
                case AsmSignatureEnum.sbyteword: return "sbyteword constant";
                case AsmSignatureEnum.sbytedword: return "sbytedword constant";
                case AsmSignatureEnum.sbyteword16: return "sbyteword16 constant";
                case AsmSignatureEnum.sbytedword16: return "sbytedword16 constant";
                case AsmSignatureEnum.sbytedword32: return "sbytedword32 constant";
                case AsmSignatureEnum.sbytedword64: return "sbytedword64 constant";
                case AsmSignatureEnum.udword: return "udword constant";
                case AsmSignatureEnum.sdword: return "sdword constant";
                case AsmSignatureEnum.near: return "near ptr";
                case AsmSignatureEnum.far: return "far ptr";
                case AsmSignatureEnum.short_ENUM: return "short ptr";
                case AsmSignatureEnum.unity: return "immediate value 1";
                case AsmSignatureEnum.mask: return "mask register";
                case AsmSignatureEnum.z: return "z";
                case AsmSignatureEnum.er: return "er";
                case AsmSignatureEnum.REG_XMM0: return "XMM0 register";
                case AsmSignatureEnum.xmmreg: return "xmm register";
                case AsmSignatureEnum.ymmreg: return "ymm register";
                case AsmSignatureEnum.zmmreg: return "zmm register";

                case AsmSignatureEnum.XMMRM: return "xmm register or memory operand";
                case AsmSignatureEnum.XMMRM16: return "xmm register or 16-bits memory operand";
                case AsmSignatureEnum.XMMRM32: return "xmm register or 32-bits memory operand";
                case AsmSignatureEnum.XMMRM64: return "xmm register or 64-bits memory operand";
                case AsmSignatureEnum.XMMRM128: return "xmm register or 128-bits memory operand";
                case AsmSignatureEnum.YMMRM256: return "ymm register or 256-bits memory operand";
                case AsmSignatureEnum.ZMMRM512: return "zmm register or 512-bits memory operand";
                case AsmSignatureEnum.b32: return "b32";
                case AsmSignatureEnum.b64: return "b64";
                case AsmSignatureEnum.mem_offs: return "memory offs";
                case AsmSignatureEnum.reg_sreg: return "segment register";
                case AsmSignatureEnum.reg_creg: return "control register";
                case AsmSignatureEnum.reg_dreg: return "debug register";
                case AsmSignatureEnum.reg_treg: return "trace register";
                case AsmSignatureEnum.reg32na: return "reg32na";
                default:
                    AsmDudeToolsStatic.Output("WARNING: SignatureStore:getDoc: add " + operandType);
                    return operandType.ToString();
                    break;
            }
        }

        public static string ToString(IList<AsmSignatureEnum> list, string concat) {
            int nOperands = list.Count;
            if (nOperands == 0) {
                return "";
            } else if (nOperands == 1) {
                return ToString(list[0]);
            } else {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < nOperands; ++i) {
                    sb.Append(ToString(list[i]));
                    if (i < nOperands - 1) sb.Append(concat);
                }
                return sb.ToString();
            }
        }

        public static bool isAllowed(Operand op, AsmSignatureEnum operandType) {
            switch (operandType) {
                case AsmSignatureEnum.UNKNOWN: return true;
                case AsmSignatureEnum.MEM: return op.isMem;
                case AsmSignatureEnum.MEM8: return (op.isMem && op.nBits == 8);
                case AsmSignatureEnum.MEM16: return (op.isMem && op.nBits == 16);
                case AsmSignatureEnum.MEM32: return (op.isMem && op.nBits == 32);
                case AsmSignatureEnum.MEM64: return (op.isMem && op.nBits == 64);
                case AsmSignatureEnum.MEM80: return (op.isMem && op.nBits == 80);
                case AsmSignatureEnum.MEM128: return (op.isMem && op.nBits == 128);
                case AsmSignatureEnum.MEM256: return (op.isMem && op.nBits == 256);
                case AsmSignatureEnum.MEM512: return (op.isMem && op.nBits == 512);

                case AsmSignatureEnum.REG8: return (op.isReg && op.nBits == 8);
                case AsmSignatureEnum.REG16: return (op.isReg && op.nBits == 16);
                case AsmSignatureEnum.REG32: return (op.isReg && op.nBits == 32);
                case AsmSignatureEnum.REG64: return (op.isReg && op.nBits == 64);
                case AsmSignatureEnum.REG_AL: return (op.isReg && op.rn == Rn.AL);
                case AsmSignatureEnum.REG_AX: return (op.isReg && op.rn == Rn.AX);
                case AsmSignatureEnum.REG_EAX: return (op.isReg && op.rn == Rn.EAX);
                case AsmSignatureEnum.REG_RAX: return (op.isReg && op.rn == Rn.RAX);
                case AsmSignatureEnum.REG_CL: return (op.isReg && op.rn == Rn.CL);
                case AsmSignatureEnum.REG_CX: return (op.isReg && op.rn == Rn.CX);
                case AsmSignatureEnum.REG_ECX: return (op.isReg && op.rn == Rn.ECX);
                case AsmSignatureEnum.REG_RCX: return (op.isReg && op.rn == Rn.RCX);
                case AsmSignatureEnum.REG_DX: return (op.isReg && op.rn == Rn.DX);
                case AsmSignatureEnum.REG_EDX: return (op.isReg && op.rn == Rn.EDX);
                case AsmSignatureEnum.REG_XMM0: return (op.isReg && op.rn == Rn.XMM0);

                case AsmSignatureEnum.REG_CS: return (op.isReg && op.rn == Rn.CS);
                case AsmSignatureEnum.REG_DS: return (op.isReg && op.rn == Rn.DS);
                case AsmSignatureEnum.REG_ES: return (op.isReg && op.rn == Rn.ES);
                case AsmSignatureEnum.REG_SS: return (op.isReg && op.rn == Rn.SS);
                case AsmSignatureEnum.REG_FS: return (op.isReg && op.rn == Rn.FS);
                case AsmSignatureEnum.REG_GS: return (op.isReg && op.rn == Rn.GS);

                case AsmSignatureEnum.IMM: return op.isImm;
                case AsmSignatureEnum.IMM8: return (op.isImm && op.nBits == 8);
                case AsmSignatureEnum.IMM16: return (op.isImm && op.nBits == 16);
                case AsmSignatureEnum.IMM32: return (op.isImm && op.nBits == 32);
                case AsmSignatureEnum.IMM64: return (op.isImm && op.nBits == 64);

                case AsmSignatureEnum.imm_imm: return op.isImm;
                case AsmSignatureEnum.imm16_imm: return op.isImm;
                case AsmSignatureEnum.imm_imm16: return op.isImm;
                case AsmSignatureEnum.imm32_imm: return op.isImm;
                case AsmSignatureEnum.imm_imm32: return op.isImm;

                case AsmSignatureEnum.RM8: return ((op.isReg || op.isMem) && op.nBits == 8);
                case AsmSignatureEnum.RM16: return ((op.isReg || op.isMem) && op.nBits == 16);
                case AsmSignatureEnum.RM32: return ((op.isReg || op.isMem) && op.nBits == 32);
                case AsmSignatureEnum.RM64: return ((op.isReg || op.isMem) && op.nBits == 64);

                case AsmSignatureEnum.sbyteword: return (op.isImm);
                case AsmSignatureEnum.sbytedword: return (op.isImm);
                case AsmSignatureEnum.sbyteword16: return (op.isImm);
                case AsmSignatureEnum.sbytedword16: return (op.isImm);
                case AsmSignatureEnum.sbytedword32: return (op.isImm);
                case AsmSignatureEnum.sbytedword64: return (op.isImm);
                case AsmSignatureEnum.udword: return (op.isImm);
                case AsmSignatureEnum.sdword: return (op.isImm);

                case AsmSignatureEnum.near: return (op.isImm);
                case AsmSignatureEnum.far: return (op.isImm);
                case AsmSignatureEnum.short_ENUM: return (op.isImm);
                case AsmSignatureEnum.unity: return (op.isImm && (op.imm == 1));

                case AsmSignatureEnum.mask: return (op.isReg && (RegisterTools.isOpmaskRegister(op.rn)));
                case AsmSignatureEnum.z: return true;

                case AsmSignatureEnum.xmmreg: return (op.isReg && RegisterTools.isSseRegister(op.rn));
                case AsmSignatureEnum.ymmreg: return (op.isReg && RegisterTools.isAvxRegister(op.rn));
                case AsmSignatureEnum.zmmreg: return (op.isReg && RegisterTools.isAvx512Register(op.rn));

                case AsmSignatureEnum.XMMRM: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || op.isMem);
                case AsmSignatureEnum.XMMRM8: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || (op.isMem && op.nBits == 8));
                case AsmSignatureEnum.XMMRM16: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || (op.isMem && op.nBits == 16));
                case AsmSignatureEnum.XMMRM32: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || (op.isMem && op.nBits == 32));
                case AsmSignatureEnum.XMMRM64: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || (op.isMem && op.nBits == 64));
                case AsmSignatureEnum.XMMRM128: return ((op.isReg && RegisterTools.isSseRegister(op.rn)) || (op.isMem && op.nBits == 128));
                case AsmSignatureEnum.YMMRM256: return ((op.isReg && RegisterTools.isAvxRegister(op.rn)) || (op.isMem && op.nBits == 256));
                case AsmSignatureEnum.ZMMRM512: return ((op.isReg && RegisterTools.isAvx512Register(op.rn)) || (op.isMem && op.nBits == 512));

                case AsmSignatureEnum.b32: return true;
                case AsmSignatureEnum.b64: return true;
                case AsmSignatureEnum.mem_offs: return (op.isImm);
                case AsmSignatureEnum.reg_sreg: return (op.isReg && (RegisterTools.isSegmentRegister(op.rn)));
                case AsmSignatureEnum.reg_creg: return (op.isReg && (RegisterTools.isControlRegister(op.rn)));
                case AsmSignatureEnum.reg_dreg: return (op.isReg && (RegisterTools.isDebugRegister(op.rn)));
                case AsmSignatureEnum.reg_treg: return true;
                case AsmSignatureEnum.bndreg: return (op.isReg && (RegisterTools.isBoundRegister(op.rn)));
                case AsmSignatureEnum.reg32na: return true;

                default:
                    AsmDudeToolsStatic.Output("WARNING: AsmSignatureTools:isAllowed: add " + operandType);
                    break;
            }
            return true;
        }

        public static bool isAllowedMisc(string misc, ISet<AsmSignatureEnum> allowedOperands) {
            switch (misc) {
                case "PTR":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM128)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM256)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM512)) return true;

                    if (allowedOperands.Contains(AsmSignatureEnum.RM8)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXRM)) return true;


                    break;

                case "BYTE":
                case "SBYTE":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM8)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM8)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM8)) return true;
                    break;
                case "WORD":
                case "SWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM16)) return true;
                    break;
                case "DWORD":
                case "SDWORD":
                case "REAL4":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM32)) return true;
                    break;
                case "QWORD":
                case "MMWORD":
                case "REAL8":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXRM)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXRM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM64)) return true;
                    break;
                case "TWORD":
                case "TBYTE":
                case "REAL10":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM80)) return true;
                    break;
                case "XMMWORD":
                case "OWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM128)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM128)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM)) return true;
                    break;
                case "YMMWORD":
                case "YWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM256)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.YMMRM256)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.YMMRM)) return true;
                    break;
                case "ZWORD":
                    if (allowedOperands.Contains(AsmSignatureEnum.MEM512)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.ZMMRM512)) return true;
                    break;
                default: break;
            }
            return false;
        }

        public static bool isAllowedReg(Rn regName, ISet<AsmSignatureEnum> allowedOperands) {
            RegisterType type = RegisterTools.getRegisterType(regName);
            switch (type) {
                case RegisterType.UNKNOWN:
                    AsmDudeToolsStatic.Output("INFO: AsmSignatureTools: isAllowedReg: registername " + regName +" could not be classified");
                    break;
                case RegisterType.BIT8:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG8)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM8)) return true;
                    if ((regName == Rn.AL) && allowedOperands.Contains(AsmSignatureEnum.REG_AL)) return true;
                    if ((regName == Rn.CL) && allowedOperands.Contains(AsmSignatureEnum.REG_CL)) return true;
                    break;
                case RegisterType.BIT16:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM16)) return true;
                    if ((regName == Rn.AX) && allowedOperands.Contains(AsmSignatureEnum.REG_AX)) return true;
                    if ((regName == Rn.CX) && allowedOperands.Contains(AsmSignatureEnum.REG_CX)) return true;
                    if ((regName == Rn.DX) && allowedOperands.Contains(AsmSignatureEnum.REG_DX)) return true;
                    break;
                case RegisterType.BIT32:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM32)) return true;
                    if ((regName == Rn.EAX) && allowedOperands.Contains(AsmSignatureEnum.REG_EAX)) return true;
                    if ((regName == Rn.ECX) && allowedOperands.Contains(AsmSignatureEnum.REG_ECX)) return true;
                    if ((regName == Rn.EDX) && allowedOperands.Contains(AsmSignatureEnum.REG_EDX)) return true;
                    break;
                case RegisterType.BIT64:
                    if (allowedOperands.Contains(AsmSignatureEnum.REG64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.RM64)) return true;
                    if ((regName == Rn.RAX) && allowedOperands.Contains(AsmSignatureEnum.REG_RAX)) return true;
                    if ((regName == Rn.RCX) && allowedOperands.Contains(AsmSignatureEnum.REG_RCX)) return true;
                    break;
                case RegisterType.MMX:
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXREG)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXRM)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.MMXRM64)) return true;
                    break;
                case RegisterType.XMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.xmmreg)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM8)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM16)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM32)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM64)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.XMMRM128)) return true;
                    if ((regName == Rn.XMM0) && allowedOperands.Contains(AsmSignatureEnum.REG_XMM0)) return true;
                    break;
                case RegisterType.YMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.ymmreg)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.YMMRM)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.YMMRM256)) return true;
                    break;
                case RegisterType.ZMM:
                    if (allowedOperands.Contains(AsmSignatureEnum.zmmreg)) return true;
                    if (allowedOperands.Contains(AsmSignatureEnum.ZMMRM512)) return true;
                    break;
                case RegisterType.MASK:
                    if (allowedOperands.Contains(AsmSignatureEnum.mask)) return true;
                    break;
                case RegisterType.SEGMENT:
                    if (allowedOperands.Contains(AsmSignatureEnum.reg_sreg)) return true;
                    switch (regName) {
                        case Rn.CS: if (allowedOperands.Contains(AsmSignatureEnum.REG_CS)) return true; break;
                        case Rn.DS: if (allowedOperands.Contains(AsmSignatureEnum.REG_DS)) return true; break;
                        case Rn.ES: if (allowedOperands.Contains(AsmSignatureEnum.REG_ES)) return true; break;
                        case Rn.SS: if (allowedOperands.Contains(AsmSignatureEnum.REG_SS)) return true; break;
                        case Rn.FS: if (allowedOperands.Contains(AsmSignatureEnum.REG_FS)) return true; break;
                        case Rn.GS: if (allowedOperands.Contains(AsmSignatureEnum.REG_GS)) return true; break;
                    }
                    break;
                case RegisterType.CONTROL:
                    if (allowedOperands.Contains(AsmSignatureEnum.reg_creg)) return true;
                    break;
                case RegisterType.DEBUG:
                    if (allowedOperands.Contains(AsmSignatureEnum.reg_dreg)) return true;
                    break;
                case RegisterType.BOUND:
                    if (allowedOperands.Contains(AsmSignatureEnum.bndreg)) return true;
                    break;
                default:
                    break;
            }
            return false;
        }

        public static string ToString(AsmSignatureEnum operandType) {
            switch (operandType) {
                case AsmSignatureEnum.UNKNOWN: return "unknown";
                case AsmSignatureEnum.MEM: return "mem";
                case AsmSignatureEnum.MEM8: return "mem8";
                case AsmSignatureEnum.MEM16: return "mem16";
                case AsmSignatureEnum.MEM32: return "mem32";
                case AsmSignatureEnum.MEM64: return "mem64";
                case AsmSignatureEnum.MEM80: return "mem80";
                case AsmSignatureEnum.MEM128: return "mem128";
                case AsmSignatureEnum.MEM256: return "mem256";
                case AsmSignatureEnum.MEM512: return "mem512";

                case AsmSignatureEnum.REG8: return "r8";
                case AsmSignatureEnum.REG16: return "r16";
                case AsmSignatureEnum.REG32: return "r32";
                case AsmSignatureEnum.REG64: return "r64";
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
                case AsmSignatureEnum.IMM: return "imm";
                case AsmSignatureEnum.IMM8: return "imm8";
                case AsmSignatureEnum.IMM16: return "imm16";
                case AsmSignatureEnum.IMM32: return "imm32";
                case AsmSignatureEnum.IMM64: return "imm64";
                case AsmSignatureEnum.imm_imm: return "imm:imm";
                case AsmSignatureEnum.imm16_imm: return "imm16:imm";
                case AsmSignatureEnum.imm_imm16: return "imm:imm16";
                case AsmSignatureEnum.imm32_imm: return "imm32:imm";
                case AsmSignatureEnum.imm_imm32: return "imm:imm32";
                case AsmSignatureEnum.RM8: return "r/m8";
                case AsmSignatureEnum.RM16: return "r/m16";
                case AsmSignatureEnum.RM32: return "r/m32";
                case AsmSignatureEnum.RM64: return "r/m64";
                case AsmSignatureEnum.sbyteword: return "sbyteword";
                case AsmSignatureEnum.sbytedword: return "sbytedword";
                case AsmSignatureEnum.sbyteword16: return "sbyteword16";
                case AsmSignatureEnum.sbytedword16: return "sbytedword16";
                case AsmSignatureEnum.sbytedword32: return "sbytedword32";
                case AsmSignatureEnum.sbytedword64: return "sbytedword64";
                case AsmSignatureEnum.udword: return "udword";
                case AsmSignatureEnum.sdword: return "sdword";
                case AsmSignatureEnum.near: return "near";
                case AsmSignatureEnum.far: return "far";
                case AsmSignatureEnum.short_ENUM: return "short";
                case AsmSignatureEnum.unity: return "unity";
                case AsmSignatureEnum.mask: return "mask";
                case AsmSignatureEnum.z: return "z";
                case AsmSignatureEnum.er: return "er";
                case AsmSignatureEnum.REG_XMM0: return "XMM0";
                case AsmSignatureEnum.xmmreg: return "xmm";
                case AsmSignatureEnum.ymmreg: return "ymm";
                case AsmSignatureEnum.zmmreg: return "zmm";
                case AsmSignatureEnum.XMMRM: return "xmm/m128";
                case AsmSignatureEnum.XMMRM8: return "xmm/m8";
                case AsmSignatureEnum.XMMRM16: return "xmm/m16";
                case AsmSignatureEnum.XMMRM32: return "xmm/m32";
                case AsmSignatureEnum.XMMRM64: return "xmm/m64";
                case AsmSignatureEnum.XMMRM128: return "xmm/m128";
                case AsmSignatureEnum.YMMRM: return "ymm/m256";
                case AsmSignatureEnum.YMMRM256: return "ymm/m256";
                case AsmSignatureEnum.ZMMRM512: return "zmm/m512";

                case AsmSignatureEnum.xmem32: return "xmem32";
                case AsmSignatureEnum.xmem64: return "xmem64";
                case AsmSignatureEnum.ymem32: return "ymem32";
                case AsmSignatureEnum.ymem64: return "ymem64";
                case AsmSignatureEnum.zmem32: return "zmem32";
                case AsmSignatureEnum.zmem64: return "zmem64";

                case AsmSignatureEnum.krm8: return "krm8";
                case AsmSignatureEnum.krm16: return "krm16";
                case AsmSignatureEnum.krm32: return "krm32";
                case AsmSignatureEnum.krm64: return "krm64";

                case AsmSignatureEnum.b32: return "b32";
                case AsmSignatureEnum.b64: return "b32";
                case AsmSignatureEnum.mem_offs: return "mem_offs";
                case AsmSignatureEnum.reg_sreg: return "segment register";
                case AsmSignatureEnum.reg_creg: return "control register";
                case AsmSignatureEnum.reg_dreg: return "debug register";
                case AsmSignatureEnum.reg_treg: return "reg_treg";
                case AsmSignatureEnum.reg32na: return "reg32na";

                case AsmSignatureEnum.MMXREG: return "mmxreg";
                case AsmSignatureEnum.MMXRM: return "mmx/mem";
                case AsmSignatureEnum.bndreg: return "bndreg";
                    
                case AsmSignatureEnum.fpureg: return "fpureg";
                case AsmSignatureEnum.to: return "to";
                case AsmSignatureEnum.fpu0: return "FPU0";

                case AsmSignatureEnum.kreg: return "kreg";
                case AsmSignatureEnum.sae: return "sae";



                default:
                    AsmDudeToolsStatic.Output("WARNING: AsmSignatureTools:ToString: " + operandType);
                    return operandType.ToString();
            }
        }
    }
}
