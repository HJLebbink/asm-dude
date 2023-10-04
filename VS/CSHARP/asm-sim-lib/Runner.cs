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

namespace AsmSim
{
    using System;
    using System.Diagnostics.Contracts;
    using AsmSim.Mnemonics;
    using AsmTools;

    public static class Runner
    {
        public static DynamicFlow Construct_DynamicFlow_Backward(StaticFlow sFlow, Tools tools)
        {
            Contract.Requires(sFlow != null);
            return Construct_DynamicFlow_Backward(sFlow, sFlow.LastLineNumber, sFlow.NLines * 2, tools);
        }

        public static DynamicFlow Construct_DynamicFlow_Backward(StaticFlow sFlow, int startLine, int nSteps, Tools tools)
        {
            DynamicFlow dFlow = new(tools);
            dFlow.Reset(sFlow, false);
            return dFlow;
        }

        public static DynamicFlow Construct_DynamicFlow_Forward(StaticFlow sFlow, Tools tools)
        {
            Contract.Requires(sFlow != null);
            return Construct_DynamicFlow_Forward(sFlow, sFlow.FirstLineNumber, sFlow.NLines * 2, tools);
        }

        public static DynamicFlow Construct_DynamicFlow_Forward(StaticFlow sFlow, int startLine, int nSteps, Tools tools)
        {
            DynamicFlow dFlow = new(tools);
            dFlow.Reset(sFlow, true);
            return dFlow;
        }

        /// <summary>Perform one step forward and return the regular branch</summary>
        public static State SimpleStep_Forward(string line, State state)
        {
            if (state == null)
            {
                Console.WriteLine("WARNING: Runner:SimpleStep_Forward: provided state is null");
                return null;
            }
            try
            {
                Tools tools = state.Tools;
                string nextKey = Tools.CreateKey(tools.Rand);
                string nextKeyBranch = "DUMMY_NOT_USED";
                (KeywordID[] _, string label, Mnemonic mnemonic, string[] args, string remark) = AsmSourceTools.ParseLine(line, -1, -1);
                using OpcodeBase opcodeBase = InstantiateOpcode(mnemonic, args, (state.HeadKey, nextKey, nextKeyBranch), tools);
                if (opcodeBase == null)
                {
                    return null;
                }

                if (opcodeBase.IsHalted)
                {
                    Console.WriteLine("WARNING: Runner:SimpleStep_Forward: line: " + line + " is halted. Message: " + opcodeBase.SyntaxError);
                    return null;
                }
                opcodeBase.Execute();
                State stateOut = new(state);
                stateOut.Update_Forward(opcodeBase.Updates.regular);
                stateOut.Frozen = true;

                opcodeBase.Updates.regular?.Dispose();
                opcodeBase.Updates.branch?.Dispose();

                if (!tools.Quiet)
                {
                    Console.WriteLine("INFO: Runner:SimpleStep_Forward: after \"" + line + "\" we know:");
                }

                if (!tools.Quiet)
                {
                    Console.WriteLine(stateOut);
                }

                return stateOut;
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: Runner:SimpleStep_Forward: Exception at line: " + line + "; e=" + e.Message);
                return new State(state);
            }
        }

        /// <summary>Perform onestep forward and return the state of the regular branch</summary>
        public static State SimpleStep_Backward(string line, State state)
        {
            Contract.Requires(state != null);

            try
            {
                string prevKey = Tools.CreateKey(state.Tools.Rand);
                (KeywordID[], string label, Mnemonic mnemonic, string[] args, string remark) content = AsmSourceTools.ParseLine(line, -1, -1);
                using OpcodeBase opcodeBase = InstantiateOpcode(content.mnemonic, content.args, (prevKey, state.TailKey, state.TailKey), state.Tools);
                if (opcodeBase == null)
                {
                    return null;
                }

                if (opcodeBase.IsHalted)
                {
                    return null;
                }

                opcodeBase.Execute();
                State stateOut = new(state);
                stateOut.Update_Backward(opcodeBase.Updates.regular, prevKey);

                opcodeBase.Updates.regular?.Dispose();
                opcodeBase.Updates.branch?.Dispose();

                if (!state.Tools.Quiet)
                {
                    Console.WriteLine("INFO: Runner:SimpleStep_Backward: after \"" + line + "\" we know:");
                }

                if (!state.Tools.Quiet)
                {
                    Console.WriteLine(stateOut);
                }

                return stateOut;
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: Runner:SimpleStep_Backward: Exception at line: " + line + "; e=" + e.Message);
                return new State(state);
            }
        }

        /// <summary>Perform one step forward and return states for both branches</summary>
        public static (State? regular, State? branch) Step_Forward(string line, State state)
        {
            Contract.Requires(state != null);
            Contract.Assume(state != null);
            try
            {
                string nextKey = Tools.CreateKey(state.Tools.Rand);
                string nextKeyBranch = nextKey + "!BRANCH";
                (KeywordID[] _, string label, Mnemonic mnemonic, string[] args, string remark) content = AsmSourceTools.ParseLine(line, -1, -1);
                using OpcodeBase opcodeBase = InstantiateOpcode(content.mnemonic, content.args, (state.HeadKey, nextKey, nextKeyBranch), state.Tools);
                if (opcodeBase == null)
                {
                    return (regular: null, branch: null);
                }

                if (opcodeBase.IsHalted)
                {
                    return (regular: null, branch: null);
                }

                opcodeBase.Execute();
                State stateRegular = null;
                if (opcodeBase.Updates.regular != null)
                {
                    stateRegular = new State(state);
                    stateRegular.Update_Forward(opcodeBase.Updates.regular);
                    opcodeBase.Updates.regular.Dispose();
                }
                State stateBranch = null;
                if (opcodeBase.Updates.branch != null)
                {
                    stateBranch = new State(state);
                    stateBranch.Update_Forward(opcodeBase.Updates.branch);
                    opcodeBase.Updates.branch.Dispose();
                }
                return (regular: stateRegular, branch: stateBranch);
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: Runner:Step_Forward: Exception at line: " + line + "; e=" + e.Message);
                return (regular: null, branch: null);
            }
        }

        public static (StateUpdate? regular, StateUpdate? branch) Execute(
            StaticFlow sFlow,
            int lineNumber,
            (string prevKey, string nextKey, string nextKeyBranch) keys,
            Tools tools)
        {
            Contract.Requires(sFlow != null);
            Contract.Assume(sFlow != null);

            try
            {
                (Mnemonic mnemonic, string[] args) = sFlow.Get_Line(lineNumber);
                using OpcodeBase opcodeBase = InstantiateOpcode(mnemonic, args, keys, tools);
                if ((opcodeBase == null) || opcodeBase.IsHalted)
                {
                    StateUpdate resetState = new(keys.prevKey, keys.nextKey, tools)
                    {
                        Reset = true,
                    };
                    return (regular: resetState, branch: null);
                }
                opcodeBase.Execute();
                return opcodeBase.Updates;
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: Runner:Step_Forward: Exception e=" + e.Message);
                return (regular: null, branch: null);
            }
        }

        public static OpcodeBase? InstantiateOpcode(
            Mnemonic mnemonic,
            string[] args,
            (string prevKey, string nextKey, string nextKeyBranch) keys,
            Tools t)
        {
            switch (mnemonic)
            {
                #region NonSse
                case Mnemonic.NONE: return new Ignore(mnemonic, args, keys, t);
                case Mnemonic.HLT: return null;

                case Mnemonic.MOV: return new Mov(args, keys, t);
                case Mnemonic.CMOVE:
                case Mnemonic.CMOVZ:
                case Mnemonic.CMOVNE:
                case Mnemonic.CMOVNZ:
                case Mnemonic.CMOVA:
                case Mnemonic.CMOVNBE:
                case Mnemonic.CMOVAE:
                case Mnemonic.CMOVNB:
                case Mnemonic.CMOVB:
                case Mnemonic.CMOVNAE:
                case Mnemonic.CMOVBE:
                case Mnemonic.CMOVNA:
                case Mnemonic.CMOVG:
                case Mnemonic.CMOVNLE:
                case Mnemonic.CMOVGE:
                case Mnemonic.CMOVNL:
                case Mnemonic.CMOVL:
                case Mnemonic.CMOVNGE:
                case Mnemonic.CMOVLE:
                case Mnemonic.CMOVNG:
                case Mnemonic.CMOVC:
                case Mnemonic.CMOVNC:
                case Mnemonic.CMOVO:
                case Mnemonic.CMOVNO:
                case Mnemonic.CMOVS:
                case Mnemonic.CMOVNS:
                case Mnemonic.CMOVP:
                case Mnemonic.CMOVPE:
                case Mnemonic.CMOVNP:
                case Mnemonic.CMOVPO: return new Cmovcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.XCHG: return new Xchg(args, keys, t);
                case Mnemonic.BSWAP: return new Bswap(args, keys, t);
                case Mnemonic.XADD: return new Xadd(args, keys, t);
                case Mnemonic.CMPXCHG: return new Cmpxchg(args, keys, t);
                case Mnemonic.CMPXCHG8B: return new Cmpxchg8b(args, keys, t);
                case Mnemonic.CMPXCHG16B: return new Cmpxchg16b(args, keys, t);
                case Mnemonic.PUSH: return new Push(args, keys, t);
                case Mnemonic.POP: return new Pop(args, keys, t);
                case Mnemonic.PUSHA: break;
                case Mnemonic.PUSHAD: break;
                case Mnemonic.POPA: break;
                case Mnemonic.POPAD: break;

                case Mnemonic.CWD: return new Cwd(args, keys, t);
                case Mnemonic.CDQ: return new Cdq(args, keys, t);
                case Mnemonic.CQO: return new Cqo(args, keys, t);

                case Mnemonic.CBW: return new Cbw(args, keys, t);
                case Mnemonic.CWDE: return new Cwde(args, keys, t);
                case Mnemonic.CDQE: return new Cdqe(args, keys, t);

                case Mnemonic.MOVSX: return new Movsx(args, keys, t);
                case Mnemonic.MOVSXD: return new Movsxd(args, keys, t);
                case Mnemonic.MOVZX: return new Movzx(args, keys, t);
                case Mnemonic.ADCX: return new Adcx(args, keys, t);
                case Mnemonic.ADOX: return new Adox(args, keys, t);
                case Mnemonic.ADD: return new Add(args, keys, t);
                case Mnemonic.ADC: return new Adc(args, keys, t);
                case Mnemonic.SUB: return new Sub(args, keys, t);
                case Mnemonic.SBB: return new Sbb(args, keys, t);
                case Mnemonic.IMUL: return new Imul(args, keys, t);
                case Mnemonic.MUL: return new Mul(args, keys, t);
                case Mnemonic.IDIV: return new Idiv(args, keys, t);
                case Mnemonic.DIV: return new Div(args, keys, t);
                case Mnemonic.INC: return new Inc(args, keys, t);
                case Mnemonic.DEC: return new Dec(args, keys, t);
                case Mnemonic.NEG: return new Neg(args, keys, t);
                case Mnemonic.CMP: return new Cmp(args, keys, t);

                case Mnemonic.DAA: return new Daa(args, keys, t);
                case Mnemonic.DAS: return new Das(args, keys, t);
                case Mnemonic.AAA: return new Aaa(args, keys, t);
                case Mnemonic.AAS: return new Aas(args, keys, t);
                case Mnemonic.AAM: return new Aam(args, keys, t);
                case Mnemonic.AAD: return new Aad(args, keys, t);

                case Mnemonic.AND: return new And(args, keys, t);
                case Mnemonic.OR: return new Or(args, keys, t);
                case Mnemonic.XOR: return new Xor(args, keys, t);
                case Mnemonic.NOT: return new Not(args, keys, t);
                case Mnemonic.SAR: return new Sar(args, keys, t);
                case Mnemonic.SHR: return new Shr(args, keys, t);
                case Mnemonic.SAL: return new Sal(args, keys, t);
                case Mnemonic.SHL: return new Shl(args, keys, t);
                case Mnemonic.SHRD: return new Shrd(args, keys, t);
                case Mnemonic.SHLD: return new Shld(args, keys, t);
                case Mnemonic.ROR: return new Ror(args, keys, t);
                case Mnemonic.ROL: return new Rol(args, keys, t);
                case Mnemonic.RCR: return new Rcr(args, keys, t);
                case Mnemonic.RCL: return new Rcl(args, keys, t);

                case Mnemonic.BT: return new Bt_Opcode(args, keys, t);
                case Mnemonic.BTS: return new Bts(args, keys, t);
                case Mnemonic.BTR: return new Btr(args, keys, t);
                case Mnemonic.BTC: return new Btc(args, keys, t);
                case Mnemonic.BSF: return new Bsf(args, keys, t);
                case Mnemonic.BSR: return new Bsr(args, keys, t);
                case Mnemonic.TEST: return new Test(args, keys, t);
                case Mnemonic.CRC32: break; //return new Crc32(args, keys, t);

                case Mnemonic.SETE:
                case Mnemonic.SETZ:
                case Mnemonic.SETNE:
                case Mnemonic.SETNZ:
                case Mnemonic.SETA:
                case Mnemonic.SETNBE:
                case Mnemonic.SETAE:
                case Mnemonic.SETNB:
                case Mnemonic.SETNC:
                case Mnemonic.SETB:
                case Mnemonic.SETNAE:
                case Mnemonic.SETC:
                case Mnemonic.SETBE:
                case Mnemonic.SETNA:
                case Mnemonic.SETG:
                case Mnemonic.SETNLE:
                case Mnemonic.SETGE:
                case Mnemonic.SETNL:
                case Mnemonic.SETL:
                case Mnemonic.SETNGE:
                case Mnemonic.SETLE:
                case Mnemonic.SETNG:
                case Mnemonic.SETS:
                case Mnemonic.SETNS:
                case Mnemonic.SETO:
                case Mnemonic.SETNO:
                case Mnemonic.SETPE:
                case Mnemonic.SETP:
                case Mnemonic.SETNP:
                case Mnemonic.SETPO: return new Setcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.JMP: return new Jmp(args, keys, t);

                case Mnemonic.JE:
                case Mnemonic.JZ:
                case Mnemonic.JNE:
                case Mnemonic.JNZ:
                case Mnemonic.JA:
                case Mnemonic.JNBE:
                case Mnemonic.JAE:
                case Mnemonic.JNB:
                case Mnemonic.JB:
                case Mnemonic.JNAE:
                case Mnemonic.JBE:
                case Mnemonic.JNA:
                case Mnemonic.JG:
                case Mnemonic.JNLE:
                case Mnemonic.JGE:
                case Mnemonic.JNL:
                case Mnemonic.JL:
                case Mnemonic.JNGE:
                case Mnemonic.JLE:
                case Mnemonic.JNG:
                case Mnemonic.JC:
                case Mnemonic.JNC:
                case Mnemonic.JO:
                case Mnemonic.JNO:
                case Mnemonic.JS:
                case Mnemonic.JNS:
                case Mnemonic.JPO:
                case Mnemonic.JNP:
                case Mnemonic.JPE:
                case Mnemonic.JP:
                case Mnemonic.JCXZ:
                case Mnemonic.JECXZ:
                case Mnemonic.JRCXZ: return new Jmpcc(mnemonic, args, ToolsAsmSim.GetCe(mnemonic), keys, t);

                case Mnemonic.LOOP: return new Loop(args, keys, t);
                case Mnemonic.LOOPZ: return new Loopz(args, keys, t);
                case Mnemonic.LOOPE: return new Loope(args, keys, t);
                case Mnemonic.LOOPNZ: return new Loopnz(args, keys, t);
                case Mnemonic.LOOPNE: return new Loopne(args, keys, t);

                case Mnemonic.CALL: break; // return new Call(args, keys, t);
                case Mnemonic.RET: break; // return new Ret(args, keys, t);
                case Mnemonic.IRET: break;
                case Mnemonic.INT: break;
                case Mnemonic.INTO: break;
                case Mnemonic.BOUND: break;
                case Mnemonic.ENTER: break;
                case Mnemonic.LEAVE: break;

                case Mnemonic.MOVS: return new Movs(Mnemonic.MOVS, Mnemonic.NONE, args, keys, t);
                case Mnemonic.MOVSB: return new Movs(Mnemonic.MOVSB, Mnemonic.NONE, args, keys, t);
                case Mnemonic.MOVSW: return new Movs(Mnemonic.MOVSW, Mnemonic.NONE, args, keys, t);
                case Mnemonic.MOVSD: return new Movs(Mnemonic.MOVSD, Mnemonic.NONE, args, keys, t);
                case Mnemonic.MOVSQ: return new Movs(Mnemonic.MOVSQ, Mnemonic.NONE, args, keys, t);

                case Mnemonic.CMPS: return new Cmps(Mnemonic.CMPS, Mnemonic.NONE, args, keys, t);
                case Mnemonic.CMPSB: return new Cmps(Mnemonic.CMPSB, Mnemonic.NONE, args, keys, t);
                case Mnemonic.CMPSW: return new Cmps(Mnemonic.CMPSW, Mnemonic.NONE, args, keys, t);
                case Mnemonic.CMPSD: return new Cmps(Mnemonic.CMPSD, Mnemonic.NONE, args, keys, t);
                case Mnemonic.CMPSQ: return new Cmps(Mnemonic.CMPSQ, Mnemonic.NONE, args, keys, t);

                case Mnemonic.SCAS: return new Scas(Mnemonic.SCAS, Mnemonic.NONE, args, keys, t);
                case Mnemonic.SCASB: return new Scas(Mnemonic.SCASB, Mnemonic.NONE, args, keys, t);
                case Mnemonic.SCASW: return new Scas(Mnemonic.SCASW, Mnemonic.NONE, args, keys, t);
                case Mnemonic.SCASD: return new Scas(Mnemonic.SCASD, Mnemonic.NONE, args, keys, t);
                case Mnemonic.SCASQ: return new Scas(Mnemonic.SCASQ, Mnemonic.NONE, args, keys, t);

                case Mnemonic.LODS: return new Lods(Mnemonic.LODS, Mnemonic.NONE, args, keys, t);
                case Mnemonic.LODSB: return new Lods(Mnemonic.LODSB, Mnemonic.NONE, args, keys, t);
                case Mnemonic.LODSW: return new Lods(Mnemonic.LODSQ, Mnemonic.NONE, args, keys, t);
                case Mnemonic.LODSD: return new Lods(Mnemonic.LODSD, Mnemonic.NONE, args, keys, t);
                case Mnemonic.LODSQ: return new Lods(Mnemonic.LODSQ, Mnemonic.NONE, args, keys, t);

                case Mnemonic.STOS: return new Stos(Mnemonic.STOS, Mnemonic.NONE, args, keys, t);
                case Mnemonic.STOSB: return new Stos(Mnemonic.STOSB, Mnemonic.NONE, args, keys, t);
                case Mnemonic.STOSW: return new Stos(Mnemonic.STOSW, Mnemonic.NONE, args, keys, t);
                case Mnemonic.STOSD: return new Stos(Mnemonic.STOSD, Mnemonic.NONE, args, keys, t);
                case Mnemonic.STOSQ: return new Stos(Mnemonic.STOSQ, Mnemonic.NONE, args, keys, t);

                #region REP Prefix
                case Mnemonic.REP_MOVS: return new Movs(Mnemonic.MOVS, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_MOVSB: return new Movs(Mnemonic.MOVSB, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_MOVSW: return new Movs(Mnemonic.MOVSW, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_MOVSD: return new Movs(Mnemonic.MOVSD, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_MOVSQ: return new Movs(Mnemonic.MOVSQ, Mnemonic.REP, args, keys, t);

                case Mnemonic.REP_LODS: return new Lods(Mnemonic.LODS, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_LODSB: return new Lods(Mnemonic.LODSB, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_LODSW: return new Lods(Mnemonic.LODSW, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_LODSD: return new Lods(Mnemonic.LODSD, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_LODSQ: return new Lods(Mnemonic.LODSQ, Mnemonic.REP, args, keys, t);

                case Mnemonic.REP_STOS: return new Stos(Mnemonic.STOS, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_STOSB: return new Stos(Mnemonic.STOSB, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_STOSW: return new Stos(Mnemonic.STOSW, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_STOSD: return new Stos(Mnemonic.STOSD, Mnemonic.REP, args, keys, t);
                case Mnemonic.REP_STOSQ: return new Stos(Mnemonic.STOSQ, Mnemonic.REP, args, keys, t);

                case Mnemonic.REPE_CMPS: return new Cmps(Mnemonic.CMPS, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_CMPSB: return new Cmps(Mnemonic.CMPSB, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_CMPSW: return new Cmps(Mnemonic.CMPSW, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_CMPSD: return new Cmps(Mnemonic.CMPSD, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_CMPSQ: return new Cmps(Mnemonic.CMPSQ, Mnemonic.REPE, args, keys, t);

                case Mnemonic.REPZ_CMPS: return new Cmps(Mnemonic.CMPS, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_CMPSB: return new Cmps(Mnemonic.CMPSB, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_CMPSW: return new Cmps(Mnemonic.CMPSW, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_CMPSD: return new Cmps(Mnemonic.CMPSD, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_CMPSQ: return new Cmps(Mnemonic.CMPSQ, Mnemonic.REPZ, args, keys, t);

                case Mnemonic.REPNE_CMPS: return new Cmps(Mnemonic.CMPS, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_CMPSB: return new Cmps(Mnemonic.CMPSB, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_CMPSW: return new Cmps(Mnemonic.CMPSW, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_CMPSD: return new Cmps(Mnemonic.CMPSD, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_CMPSQ: return new Cmps(Mnemonic.CMPSQ, Mnemonic.REPNE, args, keys, t);

                case Mnemonic.REPNZ_CMPS: return new Cmps(Mnemonic.CMPS, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_CMPSB: return new Cmps(Mnemonic.CMPSB, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_CMPSW: return new Cmps(Mnemonic.CMPSW, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_CMPSD: return new Cmps(Mnemonic.CMPSD, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_CMPSQ: return new Cmps(Mnemonic.CMPSQ, Mnemonic.REPNZ, args, keys, t);

                case Mnemonic.REPE_SCAS: return new Scas(Mnemonic.SCAS, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_SCASB: return new Scas(Mnemonic.SCASB, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_SCASW: return new Scas(Mnemonic.SCASW, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_SCASD: return new Scas(Mnemonic.SCASD, Mnemonic.REPE, args, keys, t);
                case Mnemonic.REPE_SCASQ: return new Scas(Mnemonic.SCASQ, Mnemonic.REPE, args, keys, t);

                case Mnemonic.REPZ_SCAS: return new Scas(Mnemonic.SCAS, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_SCASB: return new Scas(Mnemonic.SCASB, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_SCASW: return new Scas(Mnemonic.SCASW, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_SCASD: return new Scas(Mnemonic.SCASD, Mnemonic.REPZ, args, keys, t);
                case Mnemonic.REPZ_SCASQ: return new Scas(Mnemonic.SCASQ, Mnemonic.REPZ, args, keys, t);

                case Mnemonic.REPNE_SCAS: return new Scas(Mnemonic.SCAS, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_SCASB: return new Scas(Mnemonic.SCASB, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_SCASW: return new Scas(Mnemonic.SCASW, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_SCASD: return new Scas(Mnemonic.SCASD, Mnemonic.REPNE, args, keys, t);
                case Mnemonic.REPNE_SCASQ: return new Scas(Mnemonic.SCASQ, Mnemonic.REPNE, args, keys, t);

                case Mnemonic.REPNZ_SCAS: return new Scas(Mnemonic.SCAS, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_SCASB: return new Scas(Mnemonic.SCASB, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_SCASW: return new Scas(Mnemonic.SCASW, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_SCASD: return new Scas(Mnemonic.SCASD, Mnemonic.REPNZ, args, keys, t);
                case Mnemonic.REPNZ_SCASQ: return new Scas(Mnemonic.SCASQ, Mnemonic.REPNZ, args, keys, t);
                #endregion

                case Mnemonic.IN: return new In(args, keys, t);
                case Mnemonic.INS: break;
                case Mnemonic.INSB: break;
                case Mnemonic.INSW: break;
                case Mnemonic.INSD: break;

                case Mnemonic.OUT: return new Out(args, keys, t);
                case Mnemonic.OUTS: break;
                case Mnemonic.OUTSB: break;
                case Mnemonic.OUTSW: break;
                case Mnemonic.OUTSD: break;

                #region REP Prefix
                case Mnemonic.REP_INS: break;
                case Mnemonic.REP_INSB: break;
                case Mnemonic.REP_INSW: break;
                case Mnemonic.REP_INSD: break;

                case Mnemonic.REP_OUTS: break;
                case Mnemonic.REP_OUTSB: break;
                case Mnemonic.REP_OUTSW: break;
                case Mnemonic.REP_OUTSD: break;
                #endregion

                case Mnemonic.STC: return new Stc(args, keys, t);
                case Mnemonic.CLC: return new Clc(args, keys, t);
                case Mnemonic.CMC: return new Cmc(args, keys, t);
                case Mnemonic.CLD: return new Cld(args, keys, t);
                case Mnemonic.STD: return new Std(args, keys, t);
                case Mnemonic.STI: break;
                case Mnemonic.CLI: break;
                case Mnemonic.LAHF: return new Lahf(args, keys, t);
                case Mnemonic.SAHF: return new Sahf(args, keys, t);
                case Mnemonic.PUSHF: break;
                case Mnemonic.PUSHFD: break;
                case Mnemonic.POPF: break;
                case Mnemonic.POPFD: break;
                case Mnemonic.LDS: break;
                case Mnemonic.LES: break;
                case Mnemonic.LFS: break;
                case Mnemonic.LGS: break;
                case Mnemonic.LSS: break;
                case Mnemonic.LEA: return new Lea(args, keys, t);
                case Mnemonic.NOP: return new Nop(args, keys, t);
                case Mnemonic.UD2: return new Nop(args, keys, t);
                case Mnemonic.XLAT: break;
                case Mnemonic.XLATB: break;
                case Mnemonic.CPUID: break;
                case Mnemonic.MOVBE: return new Movbe(args, keys, t);
                case Mnemonic.PREFETCHW: return new Nop(args, keys, t);
                case Mnemonic.PREFETCHWT1: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSH: return new Nop(args, keys, t);
                case Mnemonic.CLFLUSHOPT: return new Nop(args, keys, t);
                case Mnemonic.XSAVE: break;
                case Mnemonic.XSAVEC: break;
                case Mnemonic.XSAVEOPT: break;
                case Mnemonic.XRSTOR: break;
                case Mnemonic.XGETBV: break;
                case Mnemonic.RDRAND: break;
                case Mnemonic.RDSEED: break;
                case Mnemonic.ANDN: break;
                case Mnemonic.BEXTR: break;
                case Mnemonic.BLSI: break;
                case Mnemonic.BLSMSK: break;
                case Mnemonic.BLSR: break;
                case Mnemonic.BZHI: break;
                case Mnemonic.LZCNT: break;
                case Mnemonic.MULX: break;
                case Mnemonic.PDEP: break;
                case Mnemonic.PEXT: break;
                case Mnemonic.RORX: return new Rorx(args, keys, t);
                case Mnemonic.SARX: return new Sarx(args, keys, t);
                case Mnemonic.SHLX: return new Shlx(args, keys, t);
                case Mnemonic.SHRX: return new Shrx(args, keys, t);
                case Mnemonic.TZCNT: break;

                #endregion NonSse

                #region SSE
                case Mnemonic.ADDPD: return new AddPD(args, keys, t);
                case Mnemonic.XORPD: return new XorPD(args, keys, t);
                case Mnemonic.POPCNT: return new Popcnt(args, keys, t);

                #endregion SSE

                default: return new DummySIMD(mnemonic, args, keys, t);
            }
            return null;
        }
    }
}
