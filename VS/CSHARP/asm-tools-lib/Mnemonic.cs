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

namespace AsmTools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;

    public enum Mnemonic
    {
        NONE,
        /// <summary>Halt the CPU</summary>
        HLT,

        #region Data Transfer Instructions
        // The data transfer instructions move data between memory and the general-purpose and segment registers. They
        // also perform specific operations such as conditional moves, stack access, and data conversion.

        /// <summary>Move data between general-purpose registers; move data between memory and general purpose or segment registers; move immediates to general-purpose registers</summary>
        MOV,
        /// <summary>Conditional move if equal (ZF=1) (CMOVE=CMOVZ)</summary>
        CMOVE,
        /// <summary>Conditional move if zero (ZF=1) (CMOVE=CMOVZ)</summary>
        CMOVZ,
        /// <summary>Conditional move if not equal (ZF=0) (CMOVNE=CMOVNZ)</summary>
        CMOVNE,
        /// <summary>Conditional move if not zero (ZF=0) (CMOVNE=CMOVNZ)</summary>
        CMOVNZ,
        /// <summary>Conditional move if above (CF=0 and ZF=0) (CMOVA=CMOVNBE)</summary>
        CMOVA,
        /// <summary>Conditional move if not below or equal (CF=0 and ZF=0) (CMOVA=CMOVNBE)</summary>
        CMOVNBE,
        /// <summary>Conditional move if above or equal (CF=0) (CMOVAE=CMOVNB=CMOVNC)</summary>
        CMOVAE,
        /// <summary>Conditional move if not below (CF=0) (CMOVAE=CMOVNB=CMOVNC)</summary>
        CMOVNB,
        /// <summary>Conditional move if below (CF=1) (CMOVB=CMOVC=CMOVNAE)</summary>
        CMOVB,
        /// <summary>Conditional move if not above or equal (CF=1) (CMOVB=CMOVC=CMOVNAE)</summary>
        CMOVNAE,
        /// <summary>Conditional move if below or equal (CF=1 or ZF=1) (CMOVBE=CMOVNA)</summary>
        CMOVBE,
        /// <summary>Conditional move if not above (CF=1 or ZF=1) (CMOVBE=CMOVNA)</summary>
        CMOVNA,
        /// <summary>Conditional move if greater (ZF=0 or SF=OF) (CMOVG=CMOVNLE)</summary>
        CMOVG,
        /// <summary>Conditional move if not less or equal (ZF=0 and SF=OF) (CMOVG=CMOVNLE)</summary>
        CMOVNLE,
        /// <summary>Conditional move if greater or equal (SF=OF) (CMOVGE=CMOVNL)</summary>
        CMOVGE,
        /// <summary>Conditional move if not less (SF=OF) (CMOVGE=CMOVNL)</summary>
        CMOVNL,
        /// <summary>Conditional move if less (SF!=OF) (CMOVL=CMOVNGE)</summary>
        CMOVL,
        /// <summary>Conditional move if not greater or equal (SF!=OF) (CMOVL=CMOVNGE)</summary>
        CMOVNGE,
        /// <summary>Conditional move if less or equal (ZF=1 or SF!=OF) (CMOVLE=CMOVNG)</summary>
        CMOVLE,
        /// <summary>Conditional move if not greater (ZF=1 or SF!=OF) (CMOVLE=CMOVNG)</summary>
        CMOVNG,
        /// <summary>Conditional move if carry (CF=1) (CMOVB=CMOVC=CMOVNAE)</summary>
        CMOVC,
        /// <summary>Conditional move if not carry (CF=0) (CMOVAE=CMOVNB=CMOVNC)</summary>
        CMOVNC,
        /// <summary>Conditional move if overflow (OF=1)</summary>
        CMOVO,
        /// <summary>Conditional move if not overflow (OF=0)</summary>
        CMOVNO,
        /// <summary>Conditional move if sign(negative) (SF=1)</summary>
        CMOVS,
        /// <summary>Conditional move if not sign(non-negative) (SF=0)</summary>
        CMOVNS,
        /// <summary>Conditional move if parity (PF=1) (CMOVP=CMOVPE)</summary>
        CMOVP,
        /// <summary>Conditional move if parity even (PF=1) (CMOVP=CMOVPE)</summary>
        CMOVPE,
        /// <summary>Conditional move if not parity (PF=0) (CMOVNP=CMOVPO)</summary>
        CMOVNP,
        /// <summary>Conditional move if parity odd (PF=0) (CMOVNP=CMOVPO)</summary>
        CMOVPO,
        /// <summary>Exchange</summary>
        XCHG,
        /// <summary>Byte swap</summary>
        BSWAP,
        /// <summary>Exchange and add</summary>
        XADD,
        /// <summary>Compare and exchange</summary>
        CMPXCHG,
        /// <summary>Compare and exchange 8 bytes</summary>
        CMPXCHG8B,
        /// <summary>Compare and exchange 16 bytes</summary>
        CMPXCHG16B,
        /// <summary> Push onto stack</summary>
        PUSH,
        /// <summary>Pop off of stack</summary>
        POP,
        /// <summary>Push general-purpose registers onto stack</summary>
        PUSHA,
        /// <summary>Push general-purpose registers onto stack</summary>
        PUSHAD,
        /// <summary> Pop general-purpose registers from stack</summary>
        POPA,
        /// <summary> Pop general-purpose registers from stack</summary>
        POPAD,
        /// <summary>Convert word to doubleword</summary>
        CWD,
        /// <summary>Convert doubleword to quadword</summary>
        CDQ,
        /// <summary>Convert quadword to octoword</summary>
        CQO,
        /// <summary>Convert byte to word</summary>
        CBW,
        /// <summary>Convert Word to Doubleword</summary>
        CWDE,
        /// <summary>Convert Doubleword to Quadword </smmary>
        CDQE,
        /// <summary>Move and sign extend</summary>
        MOVSX,
        /// <summary>Move and sign extend</summary>
        MOVSXD,
        /// <summary>Move and zero extend</summary>
        MOVZX,

        #endregion
        #region Binary Arithmetic Instructions
        // The binary arithmetic instructions perform basic binary integer computations on byte, word, and doubleword integers
        // located in memory and/or the general purpose registers.
        /// <summary>Unsigned integer add with carry</summary>
        ADCX,
        /// <summary>Unsigned integer add with overflow</summary>
        ADOX,
        /// <summary>Integer add</summary>
        ADD,
        /// <summary>Add with carry</summary>
        ADC,
        /// <summary>Subtract</summary>
        SUB,
        /// <summary>Subtract with borrow</summary>
        SBB,
        /// <summary>Signed multiply</summary>
        IMUL,
        /// <summary>Unsigned multiply</summary>
        MUL,
        /// <summary>Signed divide</summary>
        IDIV,
        /// <summary>Unsigned divide</summary>
        DIV,
        /// <summary>Increment</summary>
        INC,
        /// <summary>Decrement</summary>
        DEC,
        /// <summary>Negate</summary>
        NEG,
        /// <summary>Compare</summary>
        CMP,
        #endregion
        #region Decimal Arithmetic Instructions
        // The decimal arithmetic instructions perform decimal arithmetic on binary coded decimal (BCD) data.

        /// <summary>Decimal adjust after addition</summary>
        DAA,
        /// <summary>Decimal adjust after subtraction</summary>
        DAS,
        /// <summary>ASCII adjust after addition</summary>
        AAA,
        /// <summary>ASCII adjust after subtraction</summary>
        AAS,
        /// <summary>ASCII adjust after multiplication</summary>
        AAM,
        /// <summary>ASCII adjust before division</summary>
        AAD,
        #endregion
        #region Logical Instructions
        // The logical instructions perform basic AND, OR, XOR, and NOT logical operations on byte, word, and doubleword
        // values.
        /// <summary>Perform bitwise logical AND</summary>
        AND,
        /// <summary> Perform bitwise logical OR</summary>
        OR,
        /// <summary>Perform bitwise logical exclusive OR</summary>
        XOR,
        /// <summary>Perform bitwise logical NOT</summary>
        NOT,
        #endregion
        #region Shift and Rotate Instructions
        // The shift and rotate instructions shift and rotate the bits in word and doubleword operands.
        /// <summary>Shift arithmetic right</summary>
        SAR,
        /// <summary>Shift logical right</summary>
        SHR,
        /// <summary>Shift arithmetic left</summary>
        SAL,
        /// <summary>Shift logical left</summary>
        SHL,
        /// <summary>Shift right double</summary>
        SHRD,
        /// <summary>Shift left double</summary>
        SHLD,
        /// <summary>Rotate right</summary>
        ROR,
        /// <summary> Rotate left</summary>
        ROL,
        /// <summary> Rotate through carry right</summary>
        RCR,
        /// <summary>Rotate through carry left</summary>
        RCL,
        #endregion
        #region Bit and Byte Instructions
        // Bit instructions test and modify individual bits in word and doubleword operands. Byte instructions set the value of
        // a byte operand to indicate the status of flags in the EFLAGS register.
        /// <summary>Bit test</summary>
        BT,
        /// <summary>Bit test and set</summary>
        BTS,
        /// <summary>Bit test and reset</summary>
        BTR,
        /// <summary>Bit test and complement</summary>
        BTC,
        /// <summary>Bit scan forward</summary>
        BSF,
        /// <summary>Bit scan reverse</summary>
        BSR,
        /// <summary>Set byte if equal (ZF=1) (SETE=ZETZ)</summary>
        SETE,
        /// <summary>Set byte if zero (ZF=1) (SETE=ZETZ)</summary>
        SETZ,
        /// <summary>Set byte if not equal (ZF=0) (SETNE=SETNZ)</summary>
        SETNE,
        /// <summary>Set byte if not zero (ZF=0) (SETNE=SETNZ)</summary>
        SETNZ,
        /// <summary>Set byte if above (CF=0 and ZF=0) (SETA=SETNBE)</summary>
        SETA,
        /// <summary>Set byte if not below or equal (CF=0 and ZF=0) (SETA=SETNBE)</summary>
        SETNBE,
        /// <summary>Set byte if above or equal (CF=0) (SETAE=SETNC=SETNB)</summary>
        SETAE,
        /// <summary>Set byte if not below (CF=0) (SETAE=SETNC=SETNB)</summary>
        SETNB,
        /// <summary>Set byte if not carry (CF=0) (SETAE=SETNC=SETNB)</summary>
        SETNC,
        /// <summary>Set byte if below (CF=1) (SETB=SETC=SETNAE)</summary>
        SETB,
        /// <summary>Set byte if not above or equal (CF=1) (SETB=SETC=SETNAE)</summary>
        SETNAE,
        /// <summary>Set byte if carry (CF=1) (SETB=SETC=SETNAE)</summary>
        SETC,
        /// <summary>Set byte if below or equal (CF=1 or ZF=1) (SETBE=SETNA)</summary>
        SETBE,
        /// <summary>Set byte if not above (CF=1 or ZF=1) (SETBE=SETNA)</summary>
        SETNA,
        /// <summary>Set byte if greater (ZF=0 and SF=OF) (SETG=SETNLE)</summary>
        SETG,
        /// <summary>Set byte if not less or equal (ZF=0 and SF=OF) (SETG=SETNLE)summary>
        SETNLE,
        /// <summary>Set byte if greater or equal (SF=OF) (SETGE=SETNL)</summary>
        SETGE,
        /// <summary>Set byte if not less (SF=OF) (SETGE=SETNL)(</summary>
        SETNL,
        /// <summary>Set byte if less (SF!=OF) (SETL=SETNGE)</summary>
        SETL,
        /// <summary>Set byte if not greater or equal (SF!=OF) (SETL=SETNGE)</summary>
        SETNGE,
        /// <summary>Set byte if less or equal (ZF=1, SF!=OF) (SETLE=SETNG)</summary>
        SETLE,
        /// <summary>Set byte if not greater (ZF=1, SF!=OF) (SETLE=SETNG)</summary>
        SETNG,
        /// <summary>Set byte if sign (negative) (SF=1) (</summary>
        SETS,
        /// <summary>Set byte if not sign (non-negative) (SF=0)</summary>
        SETNS,
        /// <summary>Set byte if overflow (OF=1)</summary>
        SETO,
        /// <summary>Set byte if not overflow (OF=0)</summary>
        SETNO,
        /// <summary>Set byte if parity even (PF=1) (SETP=SETPE)</summary>
        SETPE,
        /// <summary>Set byte if parity (PF=1) (SETP=SETPE)</summary>
        SETP,
        /// <summary>Set byte if parity odd (PF=0) (SETNP=SETPO)</summary>
        SETPO,
        /// <summary>Set byte if not parity (PF=0) (SETNP=SETPO)</summary>
        SETNP,
        /// <summary>Logical compare</summary>
        TEST,
        /// <summary>Provides hardware acceleration to calculate cyclic redundancy checks for fast and efficient implementation of data integrity protocols.</summary>
        CRC32,
        /// <summary>This instruction calculates of number of bits set to 1 in the second operand (source) and returns the count in the first operand (a destination register)</summary>
        POPCNT,
        #endregion
        #region Control Transfer Instructions
        // The control transfer instructions provide jump, conditional jump, loop, and call and return operations to control
        // program flow.
        /// <summary>Jump</summary>
        JMP,
        /// <summary>Jump if equal</summary>
        JE,
        /// <summary>Jump if zero</summary>
        JZ,
        /// <summary>Jump if not equal</summary>
        JNE,
        /// <summary>Jump if not zero</summary>
        JNZ,
        /// <summary>Jump if above</summary>
        JA,
        /// <summary>Jump if not below or equal</summary>
        JNBE,
        /// <summary>Jump if above or equal</summary>
        JAE,
        /// <summary>Jump if not below</summary>
        JNB,
        /// <summary>Jump if below</summary>
        JB,
        /// <summary>Jump if not above or equal</summary>
        JNAE,
        /// <summary>Jump if below or equal</summary>
        JBE,
        /// <summary>Jump if not above</summary>
        JNA,
        /// <summary>Jump if greater</summary>
        JG,
        /// <summary>Jump if not less or equal</summary>
        JNLE,
        /// <summary>Jump if greater or equal</summary>
        JGE,
        /// <summary>Jump if not less</summary>
        JNL,
        /// <summary>Jump if less</summary>
        JL,
        /// <summary>Jump if not greater or equal</summary>
        JNGE,
        /// <summary>Jump if less or equal</summary>
        JLE,
        /// <summary>Jump if not greater</summary>
        JNG,
        /// <summary>Jump if carry</summary>
        JC,
        /// <summary>Jump if not carry</summary>
        JNC,
        /// <summary>Jump if overflow</summary>
        JO,
        /// <summary>Jump if not overflow</summary>
        JNO,
        /// <summary>Jump if sign (negative)</summary>
        JS,
        /// <summary>Jump if not sign (non-negative)</summary>
        JNS,
        /// <summary>Jump if parity odd</summary>
        JPO,
        /// <summary>Jump if not parity</summary>
        JNP,
        /// <summary>Jump if parity eve</summary>
        JPE,
        /// <summary>Jump if parity</summary>
        JP,
        /// <summary> Jump register CX zero</summary>
        JCXZ,
        /// <summary>Jump register ECX zero</summary>
        JECXZ,
        /// <summary>Jump register RCX zero</summary>
        JRCXZ,
        /// <summary>Loop with ECX counter</summary>
        LOOP,
        /// <summary>Loop with ECX and zero</summary>
        LOOPZ,
        /// <summary>Loop with ECX and equal</summary>
        LOOPE,
        /// <summary>Loop with ECX and not zero</summary>
        LOOPNZ,
        /// <summary>Loop with ECX and not equal</summary>
        LOOPNE,
        /// <summary>Call procedure</summary>
        CALL,
        /// <summary>Return</summary>
        RET,
        /// <summary>Return from interrupt</summary>
        IRET,
#pragma warning disable CA1720 // Identifier contains type name
        /// <summary>Software interrupt</summary>
        INT,
#pragma warning restore CA1720 // Identifier contains type name
        /// <summary>Interrupt on overflow</summary>
        INTO,
        /// <summary>Detect value out of range</summary>
        BOUND,
        /// <summary>High-level procedure entry</summary>
        ENTER,
        /// <summary> High-level procedure exit</summary>
        LEAVE,
        #endregion
        #region String Instructions
        /// <summary>The string instructions operate on strings of bytes, allowing them to be moved to and from memory.</summary>
        MOVS,
        /// <summary>Move string</summary>
        MOVSB,
        /// <summary>Move word string</summary>
        MOVSW,
        /// <summary>Move doubleword string</summary>
        MOVSD,
        /// <summary>Move quadword string</summary>
        MOVSQ,
        /// <summary>Compare string</summary>
        CMPS,
        /// <summary>Compare byte </summary>
        CMPSB,
        /// <summary>Compare word string</summary>
        CMPSW,
        /// <summary>Compare doubleword string</summary>
        CMPSD,
        /// <summary>Scan string</summary>
        SCAS,
        /// <summary>Scan byte string</summary>
        SCASB,
        /// <summary>Scan word string</summary>
        SCASW,
        /// <summary>Scan doubleword string</summary>
        SCASD,
        /// <summary>Load string</summary>
        LODS,
        /// <summary>Load byte string</summary>
        LODSB,
        /// <summary>Load word string</summary>
        LODSW,
        /// <summary>Load doubleword string</summary>
        LODSD,
        /// <summary>Load quadword string</summary>
        LODSQ,

        /// <summary>Store string</summary>
        STOS,
        /// <summary>Store byte string</summary>
        STOSB,
        /// <summary>Store word string</summary>
        STOSW,
        /// <summary>Store doubleword string</summary>
        STOSD,
        /// <summary>Store quadword string</summary>
        STOSQ,

        #region REP prefix versions
        /// <summary>Repeat while equal (termination condition: RCX or (E)CX = 0)</summary>
        REP,
        /// <summary>Repeat while zero [REPE=REPZ] (termination condition: RCX or (E)CX = 0; or ZF=0)</summary>
        REPE,
        /// <summary>Repeat while zero [REPE=REPZ] (termination condition: RCX or (E)CX = 0; or ZF=0)</summary>
        REPZ,
        /// <summary>Repeat while not equal [REPNE=REPNZ] (termination condition: RCX or (E)CX = 0; or ZF=1)</summary>
        REPNE,
        /// <summary>Repeat while not equal [REPNE=REPNZ] (termination condition: RCX or (E)CX = 0; or ZF=1)</summary>
        REPNZ,

        REP_MOVS,
        REP_MOVSB,
        REP_MOVSW,
        REP_MOVSD,
        REP_MOVSQ,

        REP_LODS,
        REP_LODSB,
        REP_LODSW,
        REP_LODSD,
        REP_LODSQ,

        REP_STOS,
        REP_STOSB,
        REP_STOSW,
        REP_STOSD,
        REP_STOSQ,

        REPE_CMPS,
        REPE_CMPSB,
        REPE_CMPSW,
        REPE_CMPSD,
        REPE_CMPSQ,

        REPE_SCAS,
        REPE_SCASB,
        REPE_SCASW,
        REPE_SCASD,
        REPE_SCASQ,

        REPZ_CMPS,
        REPZ_CMPSB,
        REPZ_CMPSW,
        REPZ_CMPSD,
        REPZ_CMPSQ,

        REPZ_SCAS,
        REPZ_SCASB,
        REPZ_SCASW,
        REPZ_SCASD,
        REPZ_SCASQ,

        REPNE_CMPS,
        REPNE_CMPSB,
        REPNE_CMPSW,
        REPNE_CMPSD,
        REPNE_CMPSQ,

        REPNE_SCAS,
        REPNE_SCASB,
        REPNE_SCASW,
        REPNE_SCASD,
        REPNE_SCASQ,

        REPNZ_CMPS,
        REPNZ_CMPSB,
        REPNZ_CMPSW,
        REPNZ_CMPSD,
        REPNZ_CMPSQ,

        REPNZ_SCAS,
        REPNZ_SCASB,
        REPNZ_SCASW,
        REPNZ_SCASD,
        REPNZ_SCASQ,
        #endregion
        #endregion
        #region I/O Instructions
        // These instructions move data between the processor’s I/O ports and a register or memory.
        /// <summary>Read from a port</summary>
        IN,
        /// <summary>Write to a port</summary>
        OUT,
        /// <summary>Input string from port</summary>
        INS,
        /// <summary>Input byte string from port</summary>
        INSB,
        /// <summary>Input string from port</summary>
        INSW,
        /// <summary>Input doubleword string from port</summary>
        INSD,
        /// <summary>Output string to port</summary>
        OUTS,
        /// <summary>Output byte string to port</summary>
        OUTSB,
        /// <summary>Output word string to port</summary>
        OUTSW,
        /// <summary>Output doubleword string to port</summary>
        OUTSD,

        REP_INS,
        REP_INSB,
        REP_INSW,
        REP_INSD,

        REP_OUTS,
        REP_OUTSB,
        REP_OUTSW,
        REP_OUTSD,
        #endregion
        #region Flag Control (EFLAG) Instructions
        // The flag control instructions operate on the flags in the EFLAGS register.
        /// <summary>Set carry flag</summary>
        STC,
        /// <summary>Clear the carry flag</summary>
        CLC,
        /// <summary>Complement the carry flag</summary>
        CMC,
        /// <summary>Clear the direction flag</summary>
        CLD,
        /// <summary>Set direction flag</summary>
        STD,
        /// <summary>Load flags into AH register</summary>
        LAHF,
        /// <summary>Store AH register into flags</summary>
        SAHF,
        /// <summary>Push EFLAGS onto stack</summary>
        PUSHF,
        /// <summary></summary>
        PUSHFD,
        /// <summary>Pop EFLAGS from stack</summary>
        POPF,
        /// <summary>Pop EFLAGS from stack</summary>
        POPFD,
        /// <summary>Set interrupt flag</summary>
        STI,
        /// <summary>Clear the interrupt flag</summary>
        CLI,
        #endregion
        #region Segment Register Instructions
        // The segment register instructions allow far pointers (segment addresses) to be loaded into the segment registers.
        /// <summary>Load far pointer using DS</summary>
        LDS,
        /// <summary> Load far pointer using ES</summary>
        LES,
        /// <summary>Load far pointer using FS</summary>
        LFS,
        /// <summary>Load far pointer using GS</summary>
        LGS,
        /// <summary>Load far pointer using SS</summary>
        LSS,
        #endregion
        #region Miscellaneous Instructions
        // The miscellaneous instructions provide such functions as loading an effective address, executing a “no-operation,”
        // and retrieving processor identification information.
        /// <summary>Load effective address</summary>
        LEA,
        /// <summary>No operation</summary>
        NOP,
        /// <summary>Generates an invalid opcode. This instruction is provided for software testing to explicitly generate an invalid opcode. The opcode for this instruction is reserved for this purpose. Other than raising the invalid opcode exception, this instruction is the same as the NOP instruction.</summary>

        UD01,
        UD1,
        UD2,

        /// <summary>Table lookup translation</summary>
        XLAT,
        /// <summary>Table lookup translation</summary>
        XLATB,
        /// <summary>Processor identification</summary>
        CPUID,
        /// <summary>Move data after swapping data bytes</summary>
        MOVBE,
        /// <summary>Prefetch data into cache in anticipation of write</summary>
        PREFETCHW,
        /// <summary>Prefetch hint T1 with intent to write</summary>
        PREFETCHWT1,
        /// <summary>Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy</summary>
        CLFLUSH,
        /// <summary>Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy with optimized memory system throughput</summary>
        CLFLUSHOPT,
        /// <summary>Writes back modified cache line containing m8, and may retain the line in cache hierarchy in non-modified state.</summary>
        CLWB,
        #endregion
        #region User Mode Extended Sate Save/Restore Instructions
        /// <summary>Save processor extended states to memory</summary>
        XSAVE,
        /// <summary>Save processor extended states with compaction to memory</summary>
        XSAVEC,
        /// <summary>Save processor extended states to memory, optimized</summary>
        XSAVEOPT,
        /// <summary>Restore processor extended states from memory</summary>
        XRSTOR,
        /// <summary>Reads the state of an extended control register</summary>
        XGETBV,
        #endregion
        #region Random Number Generator Instructions
        /// <summary>Retrieves a random number generated from hardware</summary>
        RDRAND,
        /// <summary>Retrieves a random number generated from hardware</summary>
        RDSEED,
        #endregion
        #region BMI1, BMI2
        /// <summary>Bitwise AND of first source with inverted 2nd source operands.</summary>
        ANDN,
        /// <summary>Contiguous bitwise extract</summary>
        BEXTR,
        /// <summary>Extract lowest set </summary>
        BLSI,
        /// <summary>Set all lower bits below first set bit to </summary>
        BLSMSK,
        /// <summary>Reset lowest set bit</summary>
        BLSR,
        /// <summary>Zero high bits starting from specified bit position</summary>
        BZHI,

        /// <summary>Count the number leading zero bits</summary>
        LZCNT,
        /// <summary>Unsigned multiply without affecting arithmetic flags</summary>
        MULX,
        /// <summary>Parallel deposit of bits using a mask</summary>
        PDEP,
        /// <summary>Parallel extraction of bits using a mask</summary>
        PEXT,
        /// <summary>Rotate right without affecting arithmetic flags</summary>
        RORX,
        /// <summary>Shift arithmetic right</summary>
        SARX,
        /// <summary>Shift logic left</summary>
        SHLX,
        /// <summary>Shift logic right</summary>
        SHRX,
        /// <summary>Count the number trailing zero bits</summary>
        TZCNT,
        #endregion

        #region other
        ARPL,
        BB0_RESET,
        BB1_RESET,
        /// <summary>Clear Task-Switched Flag in CR0</summary>
        CLTS,
        CMPSQ,
        CMPXCHG486,
        CPU_READ,
        CPU_WRITE,
        DMINT,
        EMMS,
        F2XM1,
        FABS,
        FADD,
        FADDP,
        FBLD,
        FBSTP,
        FCHS,
        FCLEX,
        FCMOVB,
        FCMOVBE,
        FCMOVE,
        FCMOVNB,
        FCMOVNBE,
        FCMOVNE,
        FCMOVNU,
        FCMOVU,
        FCOM,
        FCOMI,
        FCOMIP,
        FCOMP,
        FCOMPP,
        FCOS,
        FDECSTP,
        FDISI,
        FDIV,
        FDIVP,
        FDIVR,
        FDIVRP,
        FEMMS,
        FENI,
        FFREE,
        FFREEP,
        FIADD,
        FICOM,
        FICOMP,
        FIDIV,
        FIDIVR,
        FILD,
        FIMUL,
        FINCSTP,
        FINIT,
        FIST,
        FISTP,
        FISTTP,
        FISUB,
        FISUBR,
        FLD,
        FLD1,
        FLDCW,
        FLDENV,
        FLDL2E,
        FLDL2T,
        FLDLG2,
        FLDLN2,
        FLDPI,
        FLDZ,
        FMUL,
        FMULP,
        FNCLEX,
        FNDISI,
        FNENI,
        FNINIT,
        FNOP,
        FNSAVE,
        FNSTCW,
        FNSTENV,
        FNSTSW,
        FPATAN,
        FPREM,
        FPREM1,
        FPTAN,
        FRNDINT,
        FRSTOR,
        FSAVE,
        FSCALE,
        FSETPM,
        FSIN,
        FSINCOS,
        FSQRT,
        FST,
        FSTCW,
        FSTENV,
        FSTP,
        FSTSW,
        FSUB,
        FSUBP,
        FSUBR,
        FSUBRP,
        FTST,
        FUCOM,
        FUCOMI,
        FUCOMIP,
        FUCOMP,
        FUCOMPP,
        FXAM,
        FXCH,
        FXTRACT,
        FYL2X,
        FYL2XP1,
        IBTS,
        ICEBP,
        INCBIN,
        INT01,
        INT1,
        INT03,
        INT3,
        INVD,
        INVPCID,
        INVLPG,
        INVLPGA,
        IRETD,
        IRETQ,
        IRETW,
        JMPE,
        LAR,
        LFENCE,
        LGDT,
        LIDT,
        LLDT,
        LMSW,
        LOADALL,
        LOADALL286,
        LSL,
        LTR,
        #endregion

        #region SSE/AVX
        MFENCE,
        MONITOR,
        MONITORX,
        MOVD,
        MOVQ,
        MWAIT,
        MWAITX,
        PACKSSDW,
        PACKSSWB,
        PACKUSWB,
        PADDB,
        PADDD,
        PADDSB,
        PADDSW,
        PADDUSB,
        PADDUSW,
        PADDW,
        PAND,
        PANDN,
        PAUSE,
        PAVGUSB,
        PCMPEQB,
        PCMPEQD,
        PCMPEQW,
        PCMPGTB,
        PCMPGTD,
        PCMPGTW,
        PF2ID,
        PFACC,
        PFADD,
        PFCMPEQ,
        PFCMPGE,
        PFCMPGT,
        PFMAX,
        PFMIN,
        PFMUL,
        PFRCP,
        PFRCPIT1,
        PFRCPIT2,
        PFRSQIT1,
        PFRSQRT,
        PFSUB,
        PFSUBR,
        PI2FD,
        PMADDWD,
        PMULHRWA,
        PMULHRWC,
        PMULHW,
        PMULLW,
        POPAW,
        POPFQ,
        POPFW,
        POR,
        PREFETCH,
        PSLLD,
        PSLLQ,
        PSLLW,
        PSRAD,
        PSRAW,
        PSRLD,
        PSRLQ,
        PSRLW,
        PSUBB,
        PSUBD,
        PSUBSB,
        PSUBSW,
        PSUBUSB,
        PSUBUSW,
        PSUBW,
        PUNPCKHBW,
        PUNPCKHDQ,
        PUNPCKHWD,
        PUNPCKLBW,
        PUNPCKLDQ,
        PUNPCKLWD,
        PUSHAW,
        PUSHFQ,
        PUSHFW,
        PXOR,
        RDMSR,
        RDPMC,
        RDTSC,
        RDTSCP,
        RETF,
        RETN,
        RDM,
        RSDC,
        RSLDT,
        RSM,
        RSTS,
        SALC,
        SCASQ,
        SFENCE,
        SGDT,
        SIDT,
        SLDT,
        SKINIT,
        SMI,
        SMINT,
        SMINTOLD,
        SMSW,
        STR,
        SVDC,
        SVLDT,
        SVTS,
        SWAPGS,
        SYSCALL,
        SYSENTER,
        SYSEXIT,
        SYSRET,
        UMOV,
        VERR,
        VERW,
        FWAIT,
        WBINVD,
        WRSHR,
        WRMSR,
        XBTS,
        ADDPS,
        ADDSS,
        ANDNPS,
        ANDPS,
        CMPEQPS,
        CMPEQSS,
        CMPLEPS,
        CMPLESS,
        CMPLTPS,
        CMPLTSS,
        CMPNEQPS,
        CMPNEQSS,
        CMPNLEPS,
        CMPNLESS,
        CMPNLTPS,
        CMPNLTSS,
        CMPORDPS,
        CMPORDSS,
        CMPUNORDPS,
        CMPUNORDSS,
        CMPPS,
        CMPSS,
        COMISS,
        CVTPI2PS,
        CVTPS2PI,
        CVTSI2SS,
        CVTSS2SI,
        CVTTPS2PI,
        CVTTSS2SI,
        DIVPS,
        DIVSS,
        LDMXCSR,
        MAXPS,
        MAXSS,
        MINPS,
        MINSS,
        MOVAPS,
        MOVHPS,
        MOVLHPS,
        MOVLPS,
        MOVHLPS,
        MOVMSKPS,
        MOVNTPS,
        MOVSS,
        MOVUPS,
        MULPS,
        MULSS,
        ORPS,
        RCPPS,
        RCPSS,
        RSQRTPS,
        RSQRTSS,
        SHUFPS,
        SQRTPS,
        SQRTSS,
        STMXCSR,
        SUBPS,
        SUBSS,
        UCOMISS,
        UNPCKHPS,
        UNPCKLPS,
        FXRSTOR,
        FXRSTOR64,
        FXSAVE,
        FXSAVE64,
        XORPS,
        XORPD,
        XSETBV,
        XSAVE64,
        XSAVEC64,
        XSAVEOPT64,
        XSAVES,
        XSAVES64,
        XRSTOR64,
        XRSTORS,
        XRSTORS64,
        PREFETCHNTA,
        PREFETCHT0,
        PREFETCHT1,
        PREFETCHT2,
        MASKMOVQ,
        MOVNTQ,
        PAVGB,
        PAVGW,
        PEXTRW,
        PINSRW,
        PMAXSW,
        PMAXUB,
        PMINSW,
        PMINUB,
        PMOVMSKB,
        PMULHUW,
        PSADBW,
        PSHUFW,
        PF2IW,
        PFNACC,
        PFPNACC,
        PI2FW,
        PSWAPD,
        MASKMOVDQU,
        MOVNTDQ,
        MOVNTI,
        MOVNTPD,
        MOVDQA,
        MOVDQU,
        MOVDQ2Q,
        MOVQ2DQ,
        PADDQ,
        PMULUDQ,
        PSHUFD,
        PSHUFHW,
        PSHUFLW,
        PSLLDQ,
        PSRLDQ,
        PSUBQ,
        PUNPCKHQDQ,
        PUNPCKLQDQ,
        ADDPD,
        ADDSD,
        ANDNPD,
        ANDPD,
        CMPEQPD,
        CMPEQSD,
        CMPLEPD,
        CMPLESD,
        CMPLTPD,
        CMPLTSD,
        CMPNEQPD,
        CMPNEQSD,
        CMPNLEPD,
        CMPNLESD,
        CMPNLTPD,
        CMPNLTSD,
        CMPORDPD,
        CMPORDSD,
        CMPUNORDPD,
        CMPUNORDSD,
        CMPPD,
        COMISD,
        CVTDQ2PD,
        CVTDQ2PS,
        CVTPD2DQ,
        CVTPD2PI,
        CVTPD2PS,
        CVTPI2PD,
        CVTPS2DQ,
        CVTPS2PD,
        CVTSD2SI,
        CVTSD2SS,
        CVTSI2SD,
        CVTSS2SD,
        CVTTPD2PI,
        CVTTPD2DQ,
        CVTTPS2DQ,
        CVTTSD2SI,
        DIVPD,
        DIVSD,
        MAXPD,
        MAXSD,
        MINPD,
        MINSD,
        MOVAPD,
        MOVHPD,
        MOVLPD,
        MOVMSKPD,
        MOVUPD,
        MULPD,
        MULSD,
        ORPD,
        SHUFPD,
        SQRTPD,
        SQRTSD,
        SUBPD,
        SUBSD,
        UCOMISD,
        UNPCKHPD,
        UNPCKLPD,
        ADDSUBPD,
        ADDSUBPS,
        HADDPD,
        HADDPS,
        HSUBPD,
        HSUBPS,
        LDDQU,
        MOVDDUP,
        MOVSHDUP,
        MOVSLDUP,
        CLGI,
        STGI,
        VMCALL,
        VMCLEAR,
        VMFUNC,
        VMLAUNCH,
        VMLOAD,
        VMMCALL,
        VMPTRLD,
        VMPTRST,
        VMREAD,
        VMRESUME,
        VMRUN,
        VMSAVE,
        VMWRITE,
        VMXOFF,
        VMXON,
        INVEPT,
        INVVPID,
        PABSB,
        PABSW,
        PABSD,
        PALIGNR,
        PHADDW,
        PHADDD,
        PHADDSW,
        PHSUBW,
        PHSUBD,
        PHSUBSW,
        PMADDUBSW,
        PMULHRSW,
        PSHUFB,
        PSIGNB,
        PSIGNW,
        PSIGND,
        EXTRQ,
        INSERTQ,
        MOVNTSD,
        MOVNTSS,
        BLENDPD,
        BLENDPS,
        BLENDVPD,
        BLENDVPS,
        DPPD,
        DPPS,
        EXTRACTPS,
        INSERTPS,
        MOVNTDQA,
        MPSADBW,
        PACKUSDW,
        PBLENDVB,
        PBLENDW,
        PCMPEQQ,
        PEXTRB,
        PEXTRD,
        PEXTRQ,
        PHMINPOSUW,
        PINSRB,
        PINSRD,
        PINSRQ,
        PMAXSB,
        PMAXSD,
        PMAXUD,
        PMAXUW,
        PMINSB,
        PMINSD,
        PMINUD,
        PMINUW,
        PMOVSXBW,
        PMOVSXBD,
        PMOVSXBQ,
        PMOVSXWD,
        PMOVSXWQ,
        PMOVSXDQ,
        PMOVZXBW,
        PMOVZXBD,
        PMOVZXBQ,
        PMOVZXWD,
        PMOVZXWQ,
        PMOVZXDQ,
        PMULDQ,
        PMULLD,
        PTEST,
        ROUNDPD,
        ROUNDPS,
        ROUNDSD,
        ROUNDSS,
        PCMPESTRI,
        PCMPESTRM,
        PCMPISTRI,
        PCMPISTRM,
        PCMPGTQ,
        GETSEC,
        PFRCPV,
        PFRSQRTV,
        AESENC,
        AESENCLAST,
        AESDEC,
        AESDECLAST,
        AESIMC,
        AESKEYGENASSIST,
        VAESIMC,
        VAESKEYGENASSIST,
        VADDPD,
        VADDPS,
        VADDSD,
        VADDSS,
        VADDSUBPD,
        VADDSUBPS,
        VANDPD,
        VANDPS,
        VANDNPD,
        VANDNPS,
        VBLENDPD,
        VBLENDPS,
        VBLENDVPD,
        VBLENDVPS,
        VBROADCASTSS,
        VBROADCASTSD,
        VBROADCASTF128,
        VCMPEQ_OSPD,
        VCMPEQPD,
        VCMPLT_OSPD,
        VCMPLTPD,
        VCMPLE_OSPD,
        VCMPLEPD,
        VCMPUNORD_QPD,
        VCMPUNORDPD,
        VCMPNEQ_UQPD,
        VCMPNEQPD,
        VCMPNLT_USPD,
        VCMPNLTPD,
        VCMPNLE_USPD,
        VCMPNLEPD,
        VCMPORD_QPD,
        VCMPORDPD,
        VCMPEQ_UQPD,
        VCMPNGE_USPD,
        VCMPNGEPD,
        VCMPNGT_USPD,
        VCMPNGTPD,
        VCMPFALSE_OQPD,
        VCMPFALSEPD,
        VCMPNEQ_OQPD,
        VCMPGE_OSPD,
        VCMPGEPD,
        VCMPGT_OSPD,
        VCMPGTPD,
        VCMPTRUE_UQPD,
        VCMPTRUEPD,
        VCMPLT_OQPD,
        VCMPLE_OQPD,
        VCMPUNORD_SPD,
        VCMPNEQ_USPD,
        VCMPNLT_UQPD,
        VCMPNLE_UQPD,
        VCMPORD_SPD,
        VCMPEQ_USPD,
        VCMPNGE_UQPD,
        VCMPNGT_UQPD,
        VCMPFALSE_OSPD,
        VCMPNEQ_OSPD,
        VCMPGE_OQPD,
        VCMPGT_OQPD,
        VCMPTRUE_USPD,
        VCMPPD,
        VCMPEQ_OSPS,
        VCMPEQPS,
        VCMPLT_OSPS,
        VCMPLTPS,
        VCMPLE_OSPS,
        VCMPLEPS,
        VCMPUNORD_QPS,
        VCMPUNORDPS,
        VCMPNEQ_UQPS,
        VCMPNEQPS,
        VCMPNLT_USPS,
        VCMPNLTPS,
        VCMPNLE_USPS,
        VCMPNLEPS,
        VCMPORD_QPS,
        VCMPORDPS,
        VCMPEQ_UQPS,
        VCMPNGE_USPS,
        VCMPNGEPS,
        VCMPNGT_USPS,
        VCMPNGTPS,
        VCMPFALSE_OQPS,
        VCMPFALSEPS,
        VCMPNEQ_OQPS,
        VCMPGE_OSPS,
        VCMPGEPS,
        VCMPGT_OSPS,
        VCMPGTPS,
        VCMPTRUE_UQPS,
        VCMPTRUEPS,
        VCMPLT_OQPS,
        VCMPLE_OQPS,
        VCMPUNORD_SPS,
        VCMPNEQ_USPS,
        VCMPNLT_UQPS,
        VCMPNLE_UQPS,
        VCMPORD_SPS,
        VCMPEQ_USPS,
        VCMPNGE_UQPS,
        VCMPNGT_UQPS,
        VCMPFALSE_OSPS,
        VCMPNEQ_OSPS,
        VCMPGE_OQPS,
        VCMPGT_OQPS,
        VCMPTRUE_USPS,
        VCMPPS,
        VCMPEQ_OSSD,
        VCMPEQSD,
        VCMPLT_OSSD,
        VCMPLTSD,
        VCMPLE_OSSD,
        VCMPLESD,
        VCMPUNORD_QSD,
        VCMPUNORDSD,
        VCMPNEQ_UQSD,
        VCMPNEQSD,
        VCMPNLT_USSD,
        VCMPNLTSD,
        VCMPNLE_USSD,
        VCMPNLESD,
        VCMPORD_QSD,
        VCMPORDSD,
        VCMPEQ_UQSD,
        VCMPNGE_USSD,
        VCMPNGESD,
        VCMPNGT_USSD,
        VCMPNGTSD,
        VCMPFALSE_OQSD,
        VCMPFALSESD,
        VCMPNEQ_OQSD,
        VCMPGE_OSSD,
        VCMPGESD,
        VCMPGT_OSSD,
        VCMPGTSD,
        VCMPTRUE_UQSD,
        VCMPTRUESD,
        VCMPLT_OQSD,
        VCMPLE_OQSD,
        VCMPUNORD_SSD,
        VCMPNEQ_USSD,
        VCMPNLT_UQSD,
        VCMPNLE_UQSD,
        VCMPORD_SSD,
        VCMPEQ_USSD,
        VCMPNGE_UQSD,
        VCMPNGT_UQSD,
        VCMPFALSE_OSSD,
        VCMPNEQ_OSSD,
        VCMPGE_OQSD,
        VCMPGT_OQSD,
        VCMPTRUE_USSD,
        VCMPSD,
        VCMPEQ_OSSS,
        VCMPEQSS,
        VCMPLT_OSSS,
        VCMPLTSS,
        VCMPLE_OSSS,
        VCMPLESS,
        VCMPUNORD_QSS,
        VCMPUNORDSS,
        VCMPNEQ_UQSS,
        VCMPNEQSS,
        VCMPNLT_USSS,
        VCMPNLTSS,
        VCMPNLE_USSS,
        VCMPNLESS,
        VCMPORD_QSS,
        VCMPORDSS,
        VCMPEQ_UQSS,
        VCMPNGE_USSS,
        VCMPNGESS,
        VCMPNGT_USSS,
        VCMPNGTSS,
        VCMPFALSE_OQSS,
        VCMPFALSESS,
        VCMPNEQ_OQSS,
        VCMPGE_OSSS,
        VCMPGESS,
        VCMPGT_OSSS,
        VCMPGTSS,
        VCMPTRUE_UQSS,
        VCMPTRUESS,
        VCMPLT_OQSS,
        VCMPLE_OQSS,
        VCMPUNORD_SSS,
        VCMPNEQ_USSS,
        VCMPNLT_UQSS,
        VCMPNLE_UQSS,
        VCMPORD_SSS,
        VCMPEQ_USSS,
        VCMPNGE_UQSS,
        VCMPNGT_UQSS,
        VCMPFALSE_OSSS,
        VCMPNEQ_OSSS,
        VCMPGE_OQSS,
        VCMPGT_OQSS,
        VCMPTRUE_USSS,
        VCMPSS,
        VCOMISD,
        VCOMISS,
        VCVTDQ2PD,
        VCVTDQ2PS,
        VCVTPD2DQ,
        VCVTPD2PS,
        VCVTPS2DQ,
        VCVTPS2PD,
        VCVTSD2SI,
        VCVTSD2SS,
        VCVTSI2SD,
        VCVTSI2SS,
        VCVTSS2SD,
        VCVTSS2SI,
        VCVTTPD2DQ,
        VCVTTPS2DQ,
        VCVTTSD2SI,
        VCVTTSS2SI,
        VDIVPD,
        VDIVPS,
        VDIVSD,
        VDIVSS,
        VDPPD,
        VDPPS,
        VEXTRACTF128,
        VEXTRACTPS,
        VHADDPD,
        VHADDPS,
        VHSUBPD,
        VHSUBPS,
        VINSERTF128,
        VINSERTPS,
        VLDDQU,
        VLDQQU,
        VLDMXCSR,
        VMASKMOVDQU,
        VMASKMOVPS,
        VMASKMOVPD,
        VMAXPD,
        VMAXPS,
        VMAXSD,
        VMAXSS,
        VMINPD,
        VMINPS,
        VMINSD,
        VMINSS,
        VMOVAPD,
        VMOVAPS,
        VMOVD,
        VMOVQ,
        VMOVDDUP,
        VMOVDQA,
        VMOVQQA,
        VMOVDQU,
        VMOVQQU,
        VMOVHLPS,
        VMOVHPD,
        VMOVHPS,
        VMOVLHPS,
        VMOVLPD,
        VMOVLPS,
        VMOVMSKPD,
        VMOVMSKPS,
        VMOVNTDQ,
        VMOVNTQQ,
        VMOVNTDQA,
        VMOVNTPD,
        VMOVNTPS,
        VMOVSD,
        VMOVSHDUP,
        VMOVSLDUP,
        VMOVSS,
        VMOVUPD,
        VMOVUPS,
        VMPSADBW,
        VMULPD,
        VMULPS,
        VMULSD,
        VMULSS,
        VORPD,
        VORPS,
        VPABSB,
        VPABSW,
        VPABSD,
        VPACKSSWB,
        VPACKSSDW,
        VPACKUSWB,
        VPACKUSDW,
        VPADDB,
        VPADDW,
        VPADDD,
        VPADDQ,
        VPADDSB,
        VPADDSW,
        VPADDUSB,
        VPADDUSW,
        VPALIGNR,
        VPAND,
        VPANDN,
        VPAVGB,
        VPAVGW,
        VPBLENDVB,
        VPBLENDW,
        VPCMPESTRI,
        VPCMPESTRM,
        VPCMPISTRI,
        VPCMPISTRM,
        VPCMPEQB,
        VPCMPEQW,
        VPCMPEQD,
        VPCMPEQQ,
        VPCMPGTB,
        VPCMPGTW,
        VPCMPGTD,
        VPCMPGTQ,
        VPERMILPD,
        VPERMILPS,
        VPERM2F128,
        VPEXTRB,
        VPEXTRW,
        VPEXTRD,
        VPEXTRQ,
        VPHADDW,
        VPHADDD,
        VPHADDSW,
        VPHMINPOSUW,
        VPHSUBW,
        VPHSUBD,
        VPHSUBSW,
        VPINSRB,
        VPINSRW,
        VPINSRD,
        VPINSRQ,
        VPMADDWD,
        VPMADDUBSW,
        VPMAXSB,
        VPMAXSW,
        VPMAXSD,
        VPMAXUB,
        VPMAXUW,
        VPMAXUD,
        VPMINSB,
        VPMINSW,
        VPMINSD,
        VPMINUB,
        VPMINUW,
        VPMINUD,
        VPMOVMSKB,
        VPMOVSXBW,
        VPMOVSXBD,
        VPMOVSXBQ,
        VPMOVSXWD,
        VPMOVSXWQ,
        VPMOVSXDQ,
        VPMOVZXBW,
        VPMOVZXBD,
        VPMOVZXBQ,
        VPMOVZXWD,
        VPMOVZXWQ,
        VPMOVZXDQ,
        VPMULHUW,
        VPMULHRSW,
        VPMULHW,
        VPMULLW,
        VPMULLD,
        VPMULUDQ,
        VPMULDQ,
        VPOR,
        VPSADBW,
        VPSHUFB,
        VPSHUFD,
        VPSHUFHW,
        VPSHUFLW,
        VPSIGNB,
        VPSIGNW,
        VPSIGND,
        VPSLLDQ,
        VPSRLDQ,
        VPSLLW,
        VPSLLD,
        VPSLLQ,
        VPSRAW,
        VPSRAD,
        VPSRLW,
        VPSRLD,
        VPSRLQ,
        VPTEST,
        VPSUBB,
        VPSUBW,
        VPSUBD,
        VPSUBQ,
        VPSUBSB,
        VPSUBSW,
        VPSUBUSB,
        VPSUBUSW,
        VPUNPCKHBW,
        VPUNPCKHWD,
        VPUNPCKHDQ,
        VPUNPCKHQDQ,
        VPUNPCKLBW,
        VPUNPCKLWD,
        VPUNPCKLDQ,
        VPUNPCKLQDQ,
        VPXOR,
        VRCPPS,
        VRCPSS,
        VRSQRTPS,
        VRSQRTSS,
        VROUNDPD,
        VROUNDPS,
        VROUNDSD,
        VROUNDSS,
        VSHUFPD,
        VSHUFPS,
        VSQRTPD,
        VSQRTPS,
        VSQRTSD,
        VSQRTSS,
        VSTMXCSR,
        VSUBPD,
        VSUBPS,
        VSUBSD,
        VSUBSS,
        VTESTPS,
        VTESTPD,
        VUCOMISD,
        VUCOMISS,
        VUNPCKHPD,
        VUNPCKHPS,
        VUNPCKLPD,
        VUNPCKLPS,
        VXORPD,
        VXORPS,
        VZEROALL,
        VZEROUPPER,
        PCLMULLQLQDQ,
        PCLMULHQLQDQ,
        PCLMULLQHQDQ,
        PCLMULHQHQDQ,
        PCLMULQDQ,
        VPCLMULLQLQDQ,
        VPCLMULHQLQDQ,
        VPCLMULLQHQDQ,
        VPCLMULHQHQDQ,
        VFMADD132PS,
        VFMADD132PD,
        VFMADD312PS,
        VFMADD312PD,
        VFMADD213PS,
        VFMADD213PD,
        VFMADD123PS,
        VFMADD123PD,
        VFMADD231PS,
        VFMADD231PD,
        VFMADD321PS,
        VFMADD321PD,
        VFMADDSUB132PS,
        VFMADDSUB132PD,
        VFMADDSUB312PS,
        VFMADDSUB312PD,
        VFMADDSUB213PS,
        VFMADDSUB213PD,
        VFMADDSUB123PS,
        VFMADDSUB123PD,
        VFMADDSUB231PS,
        VFMADDSUB231PD,
        VFMADDSUB321PS,
        VFMADDSUB321PD,
        VFMSUB132PS,
        VFMSUB132PD,
        VFMSUB312PS,
        VFMSUB312PD,
        VFMSUB213PS,
        VFMSUB213PD,
        VFMSUB123PS,
        VFMSUB123PD,
        VFMSUB231PS,
        VFMSUB231PD,
        VFMSUB321PS,
        VFMSUB321PD,
        VFMSUBADD132PS,
        VFMSUBADD132PD,
        VFMSUBADD312PS,
        VFMSUBADD312PD,
        VFMSUBADD213PS,
        VFMSUBADD213PD,
        VFMSUBADD123PS,
        VFMSUBADD123PD,
        VFMSUBADD231PS,
        VFMSUBADD231PD,
        VFMSUBADD321PS,
        VFMSUBADD321PD,
        VFNMADD132PS,
        VFNMADD132PD,
        VFNMADD312PS,
        VFNMADD312PD,
        VFNMADD213PS,
        VFNMADD213PD,
        VFNMADD123PS,
        VFNMADD123PD,
        VFNMADD231PS,
        VFNMADD231PD,
        VFNMADD321PS,
        VFNMADD321PD,
        VFNMSUB132PS,
        VFNMSUB132PD,
        VFNMSUB312PS,
        VFNMSUB312PD,
        VFNMSUB213PS,
        VFNMSUB213PD,
        VFNMSUB123PS,
        VFNMSUB123PD,
        VFNMSUB231PS,
        VFNMSUB231PD,
        VFNMSUB321PS,
        VFNMSUB321PD,
        VFMADD132SS,
        VFMADD132SD,
        VFMADD312SS,
        VFMADD312SD,
        VFMADD213SS,
        VFMADD213SD,
        VFMADD123SS,
        VFMADD123SD,
        VFMADD231SS,
        VFMADD231SD,
        VFMADD321SS,
        VFMADD321SD,
        VFMSUB132SS,
        VFMSUB132SD,
        VFMSUB312SS,
        VFMSUB312SD,
        VFMSUB213SS,
        VFMSUB213SD,
        VFMSUB123SS,
        VFMSUB123SD,
        VFMSUB231SS,
        VFMSUB231SD,
        VFMSUB321SS,
        VFMSUB321SD,
        VFNMADD132SS,
        VFNMADD132SD,
        VFNMADD312SS,
        VFNMADD312SD,
        VFNMADD213SS,
        VFNMADD213SD,
        VFNMADD123SS,
        VFNMADD123SD,
        VFNMADD231SS,
        VFNMADD231SD,
        VFNMADD321SS,
        VFNMADD321SD,
        VFNMSUB132SS,
        VFNMSUB132SD,
        VFNMSUB312SS,
        VFNMSUB312SD,
        VFNMSUB213SS,
        VFNMSUB213SD,
        VFNMSUB123SS,
        VFNMSUB123SD,
        VFNMSUB231SS,
        VFNMSUB231SD,
        VFNMSUB321SS,
        VFNMSUB321SD,
        RDFSBASE,
        RDGSBASE,
        WRFSBASE,
        WRGSBASE,
        VCVTPH2PS,
        VCVTPS2PH,
        CLAC,
        STAC,
        XSTORE,
        XCRYPTECB,
        XCRYPTCBC,
        XCRYPTCTR,
        XCRYPTCFB,
        XCRYPTOFB,
        MONTMUL,
        XSHA1,
        XSHA256,
        LLWPCB,
        SLWPCB,
        LWPVAL,
        LWPINS,
        VFMADDPD,
        VFMADDPS,
        VFMADDSD,
        VFMADDSS,
        VFMADDSUBPD,
        VFMADDSUBPS,
        VFMSUBADDPD,
        VFMSUBADDPS,
        VFMSUBPD,
        VFMSUBPS,
        VFMSUBSD,
        VFMSUBSS,
        VFNMADDPD,
        VFNMADDPS,
        VFNMADDSD,
        VFNMADDSS,
        VFNMSUBPD,
        VFNMSUBPS,
        VFNMSUBSD,
        VFNMSUBSS,
        VFRCZPD,
        VFRCZPS,
        VFRCZSD,
        VFRCZSS,
        VPCMOV,
        VPCOMB,
        VPCOMD,
        VPCOMQ,
        VPCOMUB,
        VPCOMUD,
        VPCOMUQ,
        VPCOMUW,
        VPCOMW,
        VPHADDBD,
        VPHADDBQ,
        VPHADDBW,
        VPHADDDQ,
        VPHADDUBD,
        VPHADDUBQ,
        VPHADDUBW,
        VPHADDUDQ,
        VPHADDUWD,
        VPHADDUWQ,
        VPHADDWD,
        VPHADDWQ,
        VPHSUBBW,
        VPHSUBDQ,
        VPHSUBWD,
        VPMACSDD,
        VPMACSDQH,
        VPMACSDQL,
        VPMACSSDD,
        VPMACSSDQH,
        VPMACSSDQL,
        VPMACSSWD,
        VPMACSSWW,
        VPMACSWD,
        VPMACSWW,
        VPMADCSSWD,
        VPMADCSWD,
        VPPERM,
        VPROTB,
        VPROTD,
        VPROTQ,
        VPROTW,
        VPSHAB,
        VPSHAD,
        VPSHAQ,
        VPSHAW,
        VPSHLB,
        VPSHLQ,
        VPSHLW,
        VBROADCASTI128,
        VPBLENDD,
        VPBROADCASTB,
        VPBROADCASTW,
        VPBROADCASTD,
        VPBROADCASTQ,
        VPERMD,
        VPERMPD,
        VPERMPS,
        VPERMQ,
        VPERM2I128,
        VEXTRACTI128,
        VINSERTI128,
        VPMASKMOVD,
        VPMASKMOVQ,
        VPSLLVD,
        VPSLLVQ,
        VPSRAVD,
        VPSRLVD,
        VPSRLVQ,
        VGATHERDPD,
        VGATHERQPD,
        VGATHERDPS,
        VGATHERQPS,
        VPGATHERDD,
        VPGATHERQD,
        VPGATHERDQ,
        VPGATHERQQ,
        XABORT,
        XBEGIN,
        XEND,
        XTEST,
        BLCI,
        BLCIC,
        BLSIC,
        BLCFILL,
        BLSFILL,
        BLCMSK,
        BLCS,
        TZMSK,
        T1MSKC,

        /// <summary>MPX:</summary>
        BNDMK,
        /// <summary>MPX:</summary>
        BNDCL,
        /// <summary>MPX:</summary>
        BNDCU,
        /// <summary>MPX:</summary>
        BNDCN,
        /// <summary>MPX:</summary>
        BNDMOV,
        /// <summary>MPX:</summary>
        BNDLDX,
        /// <summary>MPX:</summary>
        BNDSTX,
        /// <summary>MPX: prefix to instruct that the next instruction is MPX-instrumented code</summary>
        BND,

        KADDB,
        KADDD,
        KADDQ,
        KADDW,
        KANDB,
        KANDD,
        KANDNB,
        KANDND,
        KANDNQ,
        KANDNW,
        KANDQ,
        KANDW,
        KMOVB,
        KMOVD,
        KMOVQ,
        KMOVW,
        KNOTB,
        KNOTD,
        KNOTQ,
        KNOTW,
        KORB,
        KORD,
        KORQ,
        KORTESTB,
        KORTESTD,
        KORTESTQ,
        KORTESTW,
        KORW,
        KSHIFTLB,
        KSHIFTLD,
        KSHIFTLQ,
        KSHIFTLW,
        KSHIFTRB,
        KSHIFTRD,
        KSHIFTRQ,
        KSHIFTRW,
        KTESTB,
        KTESTD,
        KTESTQ,
        KTESTW,
        KUNPCKBW,
        KUNPCKDQ,
        KUNPCKWD,
        KXNORB,
        KXNORD,
        KXNORQ,
        KXNORW,
        KXORB,
        KXORD,
        KXORQ,
        KXORW,
        SHA1MSG1,
        SHA1MSG2,
        SHA1NEXTE,
        SHA1RNDS4,
        SHA256MSG1,
        SHA256MSG2,
        SHA256RNDS2,
        VALIGND,
        VALIGNQ,
        VBLENDMPD,
        VBLENDMPS,
        VBROADCASTF32X2,
        VBROADCASTF32X4,
        VBROADCASTF32X8,
        VBROADCASTF64X2,
        VBROADCASTF64X4,
        VBROADCASTI32X2,
        VBROADCASTI32X4,
        VBROADCASTI32X8,
        VBROADCASTI64X2,
        VBROADCASTI64X4,
        VCOMPRESSPD,
        VCOMPRESSPS,
        VCVTPD2QQ,
        VCVTPD2UDQ,
        VCVTPD2UQQ,
        VCVTPS2QQ,
        VCVTPS2UDQ,
        VCVTPS2UQQ,
        VCVTQQ2PD,
        VCVTQQ2PS,
        VCVTSD2USI,
        VCVTSS2USI,
        VCVTTPD2QQ,
        VCVTTPD2UDQ,
        VCVTTPD2UQQ,
        VCVTTPS2QQ,
        VCVTTPS2UDQ,
        VCVTTPS2UQQ,
        VCVTTSD2USI,
        VCVTTSS2USI,
        VCVTUDQ2PD,
        VCVTUDQ2PS,
        VCVTUQQ2PD,
        VCVTUQQ2PS,
        VCVTUSI2SD,
        VCVTUSI2SS,
        VDBPSADBW,
        VEXP2PD,
        VEXP2PS,
        VEXPANDPD,
        VEXPANDPS,
        VEXTRACTF32X4,
        VEXTRACTF32X8,
        VEXTRACTF64X2,
        VEXTRACTF64X4,
        VEXTRACTI32X4,
        VEXTRACTI32X8,
        VEXTRACTI64X2,
        VEXTRACTI64X4,
        VFIXUPIMMPD,
        VFIXUPIMMPS,
        VFIXUPIMMSD,
        VFIXUPIMMSS,
        VFPCLASSPD,
        VFPCLASSPS,
        VFPCLASSSD,
        VFPCLASSSS,
        VGATHERPF0DPD,
        VGATHERPF0DPS,
        VGATHERPF0QPD,
        VGATHERPF0QPS,
        VGATHERPF1DPD,
        VGATHERPF1DPS,
        VGATHERPF1QPD,
        VGATHERPF1QPS,
        VGETEXPPD,
        VGETEXPPS,
        VGETEXPSD,
        VGETEXPSS,
        VGETMANTPD,
        VGETMANTPS,
        VGETMANTSD,
        VGETMANTSS,
        VINSERTF32X4,
        VINSERTF32X8,
        VINSERTF64X2,
        VINSERTF64X4,
        VINSERTI32X4,
        VINSERTI32X8,
        VINSERTI64X2,
        VINSERTI64X4,
        VMOVDQA32,
        VMOVDQA64,
        VMOVDQU16,
        VMOVDQU32,
        VMOVDQU64,
        VMOVDQU8,
        VPABSQ,
        VPANDD,
        VPANDND,
        VPANDNQ,
        VPANDQ,
        VPBLENDMB,
        VPBLENDMD,
        VPBLENDMQ,
        VPBLENDMW,
        VPBROADCASTMB2Q,
        VPBROADCASTMW2D,
        VPCMPB,
        VPCMPD,
        VPCMPQ,
        VPCMPUB,
        VPCMPUD,
        VPCMPUQ,
        VPCMPUW,
        VPCMPW,
        VPCOMPRESSD,
        VPCOMPRESSQ,
        VPCONFLICTD,
        VPCONFLICTQ,
        VPERMB,
        VPERMI2B,
        VPERMI2D,
        VPERMI2PD,
        VPERMI2PS,
        VPERMI2Q,
        VPERMI2W,
        VPERMT2B,
        VPERMT2D,
        VPERMT2PD,
        VPERMT2PS,
        VPERMT2Q,
        VPERMT2W,
        VPERMW,
        VPEXPANDD,
        VPEXPANDQ,
        VPLZCNTD,
        VPLZCNTQ,
        VPMADD52HUQ,
        VPMADD52LUQ,
        VPMAXSQ,
        VPMAXUQ,
        VPMINSQ,
        VPMINUQ,
        VPMOVB2M,
        VPMOVD2M,
        VPMOVDB,
        VPMOVDW,
        VPMOVM2B,
        VPMOVM2D,
        VPMOVM2Q,
        VPMOVM2W,
        VPMOVQ2M,
        VPMOVQB,
        VPMOVQD,
        VPMOVQW,
        VPMOVSDB,
        VPMOVSDW,
        VPMOVSQB,
        VPMOVSQD,
        VPMOVSQW,
        VPMOVSWB,
        VPMOVUSDB,
        VPMOVUSDW,
        VPMOVUSQB,
        VPMOVUSQD,
        VPMOVUSQW,
        VPMOVUSWB,
        VPMOVW2M,
        VPMOVWB,
        VPMULLQ,
        VPMULTISHIFTQB,
        VPORD,
        VPORQ,
        VPROLD,
        VPROLQ,
        VPROLVD,
        VPROLVQ,
        VPRORD,
        VPRORQ,
        VPRORVD,
        VPRORVQ,
        VPSCATTERDD,
        VPSCATTERDQ,
        VPSCATTERQD,
        VPSCATTERQQ,
        VPSLLVW,
        VPSRAQ,
        VPSRAVQ,
        VPSRAVW,
        VPSRLVW,
        VPTERNLOGD,
        VPTERNLOGQ,
        VPTESTMB,
        VPTESTMD,
        VPTESTMQ,
        VPTESTMW,
        VPTESTNMB,
        VPTESTNMD,
        VPTESTNMQ,
        VPTESTNMW,
        VPXORD,
        VPXORQ,
        VRANGEPD,
        VRANGEPS,
        VRANGESD,
        VRANGESS,
        VRCP14PD,
        VRCP14PS,
        VRCP14SD,
        VRCP14SS,
        VRCP28PD,
        VRCP28PS,
        VRCP28SD,
        VRCP28SS,
        VREDUCEPD,
        VREDUCEPS,
        VREDUCESD,
        VREDUCESS,
        VRNDSCALEPD,
        VRNDSCALEPS,
        VRNDSCALESD,
        VRNDSCALESS,
        VRSQRT14PD,
        VRSQRT14PS,
        VRSQRT14SD,
        VRSQRT14SS,
        VRSQRT28PD,
        VRSQRT28PS,
        VRSQRT28SD,
        VRSQRT28SS,
        VSCALEFPD,
        VSCALEFPS,
        VSCALEFSD,
        VSCALEFSS,
        VSCATTERDPD,
        VSCATTERDPS,
        VSCATTERPF0DPD,
        VSCATTERPF0DPS,
        VSCATTERPF0QPD,
        VSCATTERPF0QPS,
        VSCATTERPF1DPD,
        VSCATTERPF1DPS,
        VSCATTERPF1QPD,
        VSCATTERPF1QPS,
        VSCATTERQPD,
        VSCATTERQPS,
        VSHUFF32X4,
        VSHUFF64X2,
        VSHUFI32X4,
        VSHUFI64X2,
        RDPKRU,
        WRPKRU,
        CLZERO,

        XRELEASE,
        XACQUIRE,
        WAIT,
        LOCK,

        PABSQ,
        PMAXSQ,
        PMAXUQ,
        PMINSQ,
        PMINUQ,
        PMULLQ,
        PROLVD,
        PROLVQ,
        PROLD,
        PROLQ,
        PRORQ,
        PRORD,
        PRORVQ,
        PRORVD,
        PSRAQ,
        PTWRITE,
        RDPID,
        #endregion

        #region CYRIX
        /// <summary>CYRIX: Packed Add with Saturation.</summary>
        PADDSIW,
        /// <summary>CYRIX: Packed Subtract with Saturation.</summary>
        PSUBSIW,
        /// <summary>CYRIX: Packed Average.</summary>
        PAVEB,
        /// <summary>CYRIX: Packed Distance and Accumulate.</summary>
        PDISTIB,
        /// <summary>CYRIX: Packed Multiply and Accumulate with Rounding.</summary>
        PMACHRIW,
        /// <summary>CYRIX: Packed Magnitude.</summary>
        PMAGW,
        /// <summary>CYRIX: Packed Multiply High with Rounding.</summary>
        PMULHRW,
        /// <summary>CYRIX: Packed Multiply High with Rounding using implied destination.</summary>
        PMULHRIW,
        /// <summary>CYRIX: Packed Conditional Move(zero).</summary>
        PMVZB,
        /// <summary>CYRIX: Packed Conditional Move(not zero).</summary>
        PMVNZB,
        /// <summary>CYRIX: Packed Conditional Move(less than zero).</summary>
        PMVLZB,
        /// <summary>CYRIX: Packed Conditional Move(greater than or equal to zero).</summary>
        PMVGEZB,

        /// <summary>CYRIXM</summary>
        RDSHR,
        #endregion

        PPMULHRWA,

        /// <summary>Dot Product of Signed Words with Dword Accumulation (4-iterations)</summary>
        VP4DPWSSD,
        /// <summary>Dot Product of Signed Words with Dword Accumulation and Saturation (4-iterations)</summary>
        VP4DPWSSDS,
        /// <summary>Packed Single-Precision Floating-Point Fused Multiply-Add (4-iterations)</summary>
        V4FMADDPS,
        /// <summary>Packed Single-Precision Floating-Point Fused Multiply-Add (4-iterations)</summary>
        V4FNMADDPS,
        /// <summary>Scalar Single-Precision Floating-Point Fused Multiply-Add (4-iterations)</summary>
        V4FMADDSS,
        /// <summary>Scalar Single-Precision Floating-Point Fused Multiply-Add (4-iterations)</summary>
        V4FNMADDSS,

        /// <summary></summary>
        VPOPCNTD,
        /// <summary></summary>
        VPOPCNTQ,

        /// <summary> Galois Field Affine Transformation Inverse</summary>
        GF2P8AFFINEINVQB,
        VGF2P8AFFINEINVQB,
        /// <summary>Galois Field Affine Transformation</summary>
        GF2P8AFFINEQB,
        VGF2P8AFFINEQB,
        /// <summary>Galois Field Multiply Bytes</summary>
        GF2P8MULB,
        VGF2P8MULB,
        /// <summary>Perform One Round of an AES Decryption Flow</summary>
        VAESDEC,
        /// <summary>Perform Last Round of an AES Decryption Flow</summary>
        VAESDECLAST,
        /// <summary>Perform One Round of an AES Encryption Flow</summary>
        VAESENC,
        /// <summary>Perform Last Round of an AES Encryption Flow</summary>
        VAESENCLAST,
        /// <summary>Carry-Less Multiplication Quadword</summary>
        VPCLMULQDQ,
        /// <summary>Store Sparse Packed Byte/Word Integer Values into Dense Memory/Register</summary>
        VPCOMPRESSB,
        VPCOMPRESSW,
        /// <summary>Multiply and Add Unsigned and Signed Bytes</summary>
        VPDPBUSD,
        /// <summary>Multiply and Add Unsigned and Signed Bytes with Saturation</summary>
        VPDPBUSDS,
        /// <summary>Multiply and Add Signed Word Integers</summary>
        VPDPWSSD,
        /// <summary>Multiply and Add Word Integers with Saturation</summary>
        VPDPWSSDS,
        /// <summary>Expand Byte/Word Values</summary>
        VPEXPANDB,
        VPEXPANDW,
        /// <summary>Return the Count of Number of Bits Set to 1 in BYTE/WORD/DWORD/QWORD</summary>
        VPOPCNTB,
        VPOPCNTW,

        /// <summary>SSE5</summary>
        VPSHLD,

        /// <summary>Concatenate and Shift Packed Data Left Logical</summary>
        VPSHLDW,
        VPSHLDD,
        VPSHLDQ,
        /// <summary>Concatenate and Variable Shift Packed Data Left Logical</summary>
        VPSHLDVW,
        VPSHLDVD,
        VPSHLDVQ,
        /// <summary>Concatenate and Shift Packed Data Right Logical</summary>
        VPSHRDW,
        VPSHRDD,
        VPSHRDQ,
        /// <summary>Concatenate and Variable Shift Packed Data Right Logical</summary>
        VPSHRDVW,
        VPSHRDVD,
        VPSHRDVQ,
        /// <summary>Shuffle Bits from Quadword Elements Using Byte Indexes into Mask</summary>
        VPSHUFBITQMB,

        /// <summary>This instruction is used to execute privileged Intel SGX leaf functions that are used for managing and debugging the enclaves.</summary>
        ENCLS,

        /// <summary>This instruction is used to execute non-privileged Intel SGX leaf functions.</summary>
        ENCLU,

        ENCLV,
        EADD,
        EAUG,
        EBLOCK,
        ECREATE,
        EDBGRD,
        EDBGWR,
        EEXTEND,
        EINIT,
        ELDB,
        ELDU,
        ELDBC,
        ELBUC,
        EMODPR,
        EMODT,
        EPA,
        ERDINFO,
        EREMOVE,
        ETRACK,
        ETRACKC,
        EWB,
        EACCEPT,
        EACCEPTCOPY,
        EENTER,
        EEXIT,
        EGETKEY,
        EMODPE,
        EREPORT,
        ERESUME,
        EDECVIRTCHILD,
        EINCVIRTCHILD,
        ESETCONTEXT,

        EXITAC,
        PARAMETERS,
        SENTER,
        SEXIT,
        SMCTRL,
        WAKEUP,

        CLDEMOTE,
        MOVDIR64B,
        MOVDIRI,
        PCONFIG,
        TPAUSE,
        UMONITOR,
        UMWAIT,
        WBNOINVD,

        ENQCMD,
        ENQCMDS,

        VCVTNE2PS2BF16,
        VCVTNEPS2BF16,
        VDPBF16PS,

        VP2INTERSECTD,
        VP2INTERSECTQ,
    }

    /// <summary>
    /// Suffix for AT&T mnemonic
    /// </summary>
    public enum AttType
    {
        B = (byte)'B',
        S = (byte)'S',
        W = (byte)'W',
        L = (byte)'L',
        Q = (byte)'Q',
        T = (byte)'T',
        NONE = 0xFF,
    }

    public static partial class AsmSourceTools
    {
        private static readonly Dictionary<string, Mnemonic> Mnemonic_cache_;

        /// <summary>Static class initializer for AsmSourceTools</summary>
        static AsmSourceTools()
        {
            Mnemonic_cache_ = new Dictionary<string, Mnemonic>();
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
                Mnemonic_cache_.Add(mnemonic.ToString(), mnemonic);
            }
        }

        public static string ToCapitals(string str, bool strIsCapitals)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

#if DEBUG
            if (strIsCapitals && (str != str.ToUpperInvariant()))
            {
                throw new Exception($"You promised me a string that is upper and you gave me one that is not. str=\"{str}\"");
            }
#endif
            return (strIsCapitals) ? str : str.ToUpperInvariant();
        }

        private static AttType ParseAttType(char c)
        {
            switch (c)
            {
                case 'B': return AttType.B;
                case 'S': return AttType.S;
                case 'W': return AttType.W;
                case 'L': return AttType.L;
                case 'Q': return AttType.Q;
                case 'T': return AttType.T;
                default: return AttType.NONE;
            }
        }

        private static bool IsAttType(char c)
        {
            switch (c)
            {
                case 'B':
                case 'S':
                case 'W':
                case 'L':
                case 'Q':
                case 'T': return true;
                default: return false;
            }
        }

        public static bool IsJump(Mnemonic mnemonic)
        {
            switch (mnemonic)
            {
                case Mnemonic.JMP:
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
                case Mnemonic.JRCXZ:
                case Mnemonic.LOOP:
                case Mnemonic.LOOPZ:
                case Mnemonic.LOOPE:
                case Mnemonic.LOOPNZ:
                case Mnemonic.LOOPNE:
                case Mnemonic.CALL: return true;
                default: return false;
            }
        }

        /// <summary>Parse the provided string that contains a AT&T syntax mnemonic</summary>
        public static Mnemonic ParseMnemonic_Att_OLD(string str, bool strIsCapitals = false)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            int length = str.Length;
            if (length > 1)
            {
                string str2 = ToCapitals(str, strIsCapitals);

                Mnemonic r = ParseMnemonic(str2, true);
                if (r != Mnemonic.NONE)
                {
                    return r;
                }

                AttType attType = ParseAttType(str2[length - 1]);
                if (attType != AttType.NONE)
                {
                    string keyword2 = str2.Substring(0, length - 1);
                    return ParseMnemonic(keyword2, true);
                }
            }
            return Mnemonic.NONE;
        }

        public static (Mnemonic mnemonic, AttType attribute_type) ParseMnemonic_Att(string str, bool strIsCapitals = false)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            int length = str.Length;
            if (length > 1)
            {
                string str2 = ToCapitals(str, strIsCapitals);

                Mnemonic r = ParseMnemonic(str2, true);
                if (r != Mnemonic.NONE)
                {
                    return (r, AttType.NONE);
                }
                AttType attType = ParseAttType(str2[length - 1]);
                if (attType != AttType.NONE)
                {
                    string keyword2 = str2.Substring(0, length - 1);
                    return (ParseMnemonic(keyword2, true), attType);
                }
            }
            return (Mnemonic.NONE, AttType.NONE);
        }

        public static Mnemonic ParseMnemonic(string str, bool strIsCapitals)
        {
            return (Mnemonic_cache_.TryGetValue(ToCapitals(str, strIsCapitals), out Mnemonic value)) ? value : Mnemonic.NONE;
        }

        /// <summary>Parse the provided string that contains a Intel syntax mnemonic</summary>
        public static Mnemonic ParseMnemonic_OLD(string str, bool strIsCapitals = false)
        {
            switch (ToCapitals(str, strIsCapitals))
            {
                case "NONE": return Mnemonic.NONE;
                case "MOV": return Mnemonic.MOV;
                case "CMOVE": return Mnemonic.CMOVE;
                case "CMOVZ": return Mnemonic.CMOVZ;
                case "CMOVNE": return Mnemonic.CMOVNE;
                case "CMOVNZ": return Mnemonic.CMOVNZ;
                case "CMOVA": return Mnemonic.CMOVA;
                case "CMOVNBE": return Mnemonic.CMOVNBE;
                case "CMOVAE": return Mnemonic.CMOVAE;
                case "CMOVNB": return Mnemonic.CMOVNB;
                case "CMOVB": return Mnemonic.CMOVB;
                case "CMOVNAE": return Mnemonic.CMOVNAE;
                case "CMOVBE": return Mnemonic.CMOVBE;
                case "CMOVNA": return Mnemonic.CMOVNA;
                case "CMOVG": return Mnemonic.CMOVG;
                case "CMOVNLE": return Mnemonic.CMOVNLE;
                case "CMOVGE": return Mnemonic.CMOVGE;
                case "CMOVNL": return Mnemonic.CMOVNL;
                case "CMOVL": return Mnemonic.CMOVL;
                case "CMOVNGE": return Mnemonic.CMOVNGE;
                case "CMOVLE": return Mnemonic.CMOVLE;
                case "CMOVNG": return Mnemonic.CMOVNG;
                case "CMOVC": return Mnemonic.CMOVC;
                case "CMOVNC": return Mnemonic.CMOVNC;
                case "CMOVO": return Mnemonic.CMOVO;
                case "CMOVNO": return Mnemonic.CMOVNO;
                case "CMOVS": return Mnemonic.CMOVS;
                case "CMOVNS": return Mnemonic.CMOVNS;
                case "CMOVP": return Mnemonic.CMOVP;
                case "CMOVPE": return Mnemonic.CMOVPE;
                case "CMOVNP": return Mnemonic.CMOVNP;
                case "CMOVPO": return Mnemonic.CMOVPO;
                case "XCHG": return Mnemonic.XCHG;
                case "BSWAP": return Mnemonic.BSWAP;
                case "XADD": return Mnemonic.XADD;
                case "CMPXCHG": return Mnemonic.CMPXCHG;
                case "CMPXCHG8B": return Mnemonic.CMPXCHG8B;
                case "PUSH": return Mnemonic.PUSH;
                case "POP": return Mnemonic.POP;
                case "PUSHA": return Mnemonic.PUSHA;
                case "PUSHAD": return Mnemonic.PUSHAD;
                case "POPA": return Mnemonic.POPA;
                case "POPAD": return Mnemonic.POPAD;
                case "CWD": return Mnemonic.CWD;
                case "CDQ": return Mnemonic.CDQ;
                case "CBW": return Mnemonic.CBW;
                case "CWDE": return Mnemonic.CWDE;
                case "CDQE": return Mnemonic.CDQE;
                case "MOVSX": return Mnemonic.MOVSX;
                case "MOVSXD": return Mnemonic.MOVSXD;
                case "MOVZX": return Mnemonic.MOVZX;
                case "ADCX": return Mnemonic.ADCX;
                case "ADOX": return Mnemonic.ADOX;
                case "ADD": return Mnemonic.ADD;
                case "ADC": return Mnemonic.ADC;
                case "SUB": return Mnemonic.SUB;
                case "SBB": return Mnemonic.SBB;
                case "IMUL": return Mnemonic.IMUL;
                case "MUL": return Mnemonic.MUL;
                case "IDIV": return Mnemonic.IDIV;
                case "DIV": return Mnemonic.DIV;
                case "INC": return Mnemonic.INC;
                case "DEC": return Mnemonic.DEC;
                case "NEG": return Mnemonic.NEG;
                case "CMP": return Mnemonic.CMP;
                case "DAA": return Mnemonic.DAA;
                case "DAS": return Mnemonic.DAS;
                case "AAA": return Mnemonic.AAA;
                case "AAS": return Mnemonic.AAS;
                case "AAM": return Mnemonic.AAM;
                case "AAD": return Mnemonic.AAD;
                case "AND": return Mnemonic.AND;
                case "OR": return Mnemonic.OR;
                case "XOR": return Mnemonic.XOR;
                case "NOT": return Mnemonic.NOT;
                case "SAR": return Mnemonic.SAR;
                case "SHR": return Mnemonic.SHR;
                case "SAL": return Mnemonic.SAL;
                case "SHL": return Mnemonic.SHL;
                case "SHRD": return Mnemonic.SHRD;
                case "SHLD": return Mnemonic.SHLD;
                case "ROR": return Mnemonic.ROR;
                case "ROL": return Mnemonic.ROL;
                case "RCR": return Mnemonic.RCR;
                case "RCL": return Mnemonic.RCL;
                case "BT": return Mnemonic.BT;
                case "BTS": return Mnemonic.BTS;
                case "BTR": return Mnemonic.BTR;
                case "BTC": return Mnemonic.BTC;
                case "BSF": return Mnemonic.BSF;
                case "BSR": return Mnemonic.BSR;

                case "SETE": return Mnemonic.SETE;
                case "SETZ": return Mnemonic.SETZ;
                case "SETNE": return Mnemonic.SETNE;
                case "SETNZ": return Mnemonic.SETNZ;
                case "SETA": return Mnemonic.SETA;
                case "SETNBE": return Mnemonic.SETNBE;
                case "SETAE": return Mnemonic.SETAE;
                case "SETNB": return Mnemonic.SETNB;
                case "SETNC": return Mnemonic.SETNC;
                case "SETB": return Mnemonic.SETB;
                case "SETNAE": return Mnemonic.SETNAE;
                case "SETC": return Mnemonic.SETC;
                case "SETBE": return Mnemonic.SETBE;
                case "SETNA": return Mnemonic.SETNA;
                case "SETG": return Mnemonic.SETG;
                case "SETNLE": return Mnemonic.SETNLE;
                case "SETGE": return Mnemonic.SETGE;
                case "SETNL": return Mnemonic.SETNL;
                case "SETL": return Mnemonic.SETL;
                case "SETNGE": return Mnemonic.SETNGE;
                case "SETLE": return Mnemonic.SETLE;
                case "SETNG": return Mnemonic.SETNG;
                case "SETS": return Mnemonic.SETS;
                case "SETNS": return Mnemonic.SETNS;
                case "SETO": return Mnemonic.SETO;
                case "SETNO": return Mnemonic.SETNO;
                case "SETPE": return Mnemonic.SETPE;
                case "SETP": return Mnemonic.SETP;
                case "SETPO": return Mnemonic.SETPO;
                case "SETNP": return Mnemonic.SETNP;

                case "TEST": return Mnemonic.TEST;
                case "CRC32": return Mnemonic.CRC32;
                case "POPCNT": return Mnemonic.POPCNT;

                case "JMP": return Mnemonic.JMP;
                case "JE": return Mnemonic.JE;
                case "JZ": return Mnemonic.JZ;
                case "JNE": return Mnemonic.JNE;
                case "JNZ": return Mnemonic.JNZ;
                case "JA": return Mnemonic.JA;
                case "JNBE": return Mnemonic.JNBE;
                case "JAE": return Mnemonic.JAE;
                case "JNB": return Mnemonic.JNB;
                case "JB": return Mnemonic.JB;
                case "JNAE": return Mnemonic.JNAE;
                case "JBE": return Mnemonic.JBE;
                case "JNA": return Mnemonic.JNA;
                case "JG": return Mnemonic.JG;
                case "JNLE": return Mnemonic.JNLE;
                case "JGE": return Mnemonic.JGE;
                case "JNL": return Mnemonic.JNL;
                case "JL": return Mnemonic.JL;
                case "JNGE": return Mnemonic.JNGE;
                case "JLE": return Mnemonic.JLE;
                case "JNG": return Mnemonic.JNG;
                case "JC": return Mnemonic.JC;
                case "JNC": return Mnemonic.JNC;
                case "JO": return Mnemonic.JO;
                case "JNO": return Mnemonic.JNO;
                case "JS": return Mnemonic.JS;
                case "JNS": return Mnemonic.JNS;
                case "JPO": return Mnemonic.JPO;
                case "JNP": return Mnemonic.JNP;
                case "JPE": return Mnemonic.JPE;
                case "JP": return Mnemonic.JP;
                case "JCXZ": return Mnemonic.JCXZ;
                case "JECXZ": return Mnemonic.JECXZ;
                case "JRCXZ": return Mnemonic.JRCXZ;
                case "LOOP": return Mnemonic.LOOP;
                case "LOOPZ": return Mnemonic.LOOPZ;
                case "LOOPE": return Mnemonic.LOOPE;
                case "LOOPNZ": return Mnemonic.LOOPNZ;
                case "LOOPNE": return Mnemonic.LOOPNE;

                case "CALL": return Mnemonic.CALL;
                case "RET": return Mnemonic.RET;
                case "IRET": return Mnemonic.IRET;
                case "INT": return Mnemonic.INT;
                case "INTO": return Mnemonic.INTO;
                case "BOUND": return Mnemonic.BOUND;
                case "ENTER": return Mnemonic.ENTER;
                case "LEAVE": return Mnemonic.LEAVE;

                case "IN": return Mnemonic.IN;
                case "OUT": return Mnemonic.OUT;

                case "MOVS": return Mnemonic.MOVS;
                case "MOVSB": return Mnemonic.MOVSB;
                case "MOVSW": return Mnemonic.MOVSW;
                case "MOVSD": return Mnemonic.MOVSD;
                case "MOVSQ": return Mnemonic.MOVSQ;

                case "CMPS": return Mnemonic.CMPS;
                case "CMPSB": return Mnemonic.CMPSB;
                case "CMPSW": return Mnemonic.CMPSW;
                case "CMPSD": return Mnemonic.CMPSD;
                case "CMPSQ": return Mnemonic.CMPSQ;

                case "SCAS": return Mnemonic.SCAS;
                case "SCASB": return Mnemonic.SCASB;
                case "SCASW": return Mnemonic.SCASW;
                case "SCASD": return Mnemonic.SCASD;
                case "SCASQ": return Mnemonic.SCASQ;

                case "LODS": return Mnemonic.LODS;
                case "LODSB": return Mnemonic.LODSB;
                case "LODSW": return Mnemonic.LODSW;
                case "LODSD": return Mnemonic.LODSD;
                case "LODSQ": return Mnemonic.LODSQ;

                case "STOS": return Mnemonic.STOS;
                case "STOSB": return Mnemonic.STOSB;
                case "STOSW": return Mnemonic.STOSW;
                case "STOSD": return Mnemonic.STOSD;
                case "STOSQ": return Mnemonic.STOSQ;

                case "REP": return Mnemonic.REP;
                case "REPE": return Mnemonic.REPE;
                case "REPZ": return Mnemonic.REPZ;
                case "REPNE": return Mnemonic.REPNE;
                case "REPNZ": return Mnemonic.REPNZ;

                case "REP_MOVS": return Mnemonic.REP_MOVS;
                case "REP_MOVSB": return Mnemonic.REP_MOVSB;
                case "REP_MOVSW": return Mnemonic.REP_MOVSW;
                case "REP_MOVSD": return Mnemonic.REP_MOVSD;
                case "REP_MOVSQ": return Mnemonic.REP_MOVSQ;

                case "REP_LODS": return Mnemonic.REP_LODS;
                case "REP_LODSB": return Mnemonic.REP_LODSB;
                case "REP_LODSW": return Mnemonic.REP_LODSW;
                case "REP_LODSD": return Mnemonic.REP_LODSD;
                case "REP_LODSQ": return Mnemonic.REP_LODSQ;

                case "REP_STOS": return Mnemonic.REP_STOS;
                case "REP_STOSB": return Mnemonic.REP_STOSB;
                case "REP_STOSW": return Mnemonic.REP_STOSW;
                case "REP_STOSD": return Mnemonic.REP_STOSD;
                case "REP_STOSQ": return Mnemonic.REP_STOSQ;

                case "REPE_CMPS": return Mnemonic.REPE_CMPS;
                case "REPE_CMPSB": return Mnemonic.REPE_CMPSB;
                case "REPE_CMPSW": return Mnemonic.REPE_CMPSW;
                case "REPE_CMPSD": return Mnemonic.REPE_CMPSD;
                case "REPE_CMPSQ": return Mnemonic.REPE_CMPSQ;

                case "REPE_SCAS": return Mnemonic.REPE_SCAS;
                case "REPE_SCASB": return Mnemonic.REPE_SCASB;
                case "REPE_SCASW": return Mnemonic.REPE_SCASW;
                case "REPE_SCASD": return Mnemonic.REPE_SCASD;
                case "REPE_SCASQ": return Mnemonic.REPE_SCASQ;

                case "REPZ_CMPS": return Mnemonic.REPZ_CMPS;
                case "REPZ_CMPSB": return Mnemonic.REPZ_CMPSB;
                case "REPZ_CMPSW": return Mnemonic.REPZ_CMPSW;
                case "REPZ_CMPSD": return Mnemonic.REPZ_CMPSD;
                case "REPZ_CMPSQ": return Mnemonic.REPZ_CMPSQ;

                case "REPZ_SCAS": return Mnemonic.REPZ_SCAS;
                case "REPZ_SCASB": return Mnemonic.REPZ_SCASB;
                case "REPZ_SCASW": return Mnemonic.REPZ_SCASW;
                case "REPZ_SCASD": return Mnemonic.REPZ_SCASD;
                case "REPZ_SCASQ": return Mnemonic.REPZ_SCASQ;

                case "REPNE_CMPS": return Mnemonic.REPNE_CMPS;
                case "REPNE_CMPSB": return Mnemonic.REPNE_CMPSB;
                case "REPNE_CMPSW": return Mnemonic.REPNE_CMPSW;
                case "REPNE_CMPSD": return Mnemonic.REPNE_CMPSD;
                case "REPNE_CMPSQ": return Mnemonic.REPNE_CMPSQ;

                case "REPNE_SCAS": return Mnemonic.REPNE_SCAS;
                case "REPNE_SCASB": return Mnemonic.REPNE_SCASB;
                case "REPNE_SCASW": return Mnemonic.REPNE_SCASW;
                case "REPNE_SCASD": return Mnemonic.REPNE_SCASD;
                case "REPNE_SCASQ": return Mnemonic.REPNE_SCASQ;

                case "REPNZ_CMPS": return Mnemonic.REPNZ_CMPS;
                case "REPNZ_CMPSB": return Mnemonic.REPNZ_CMPSB;
                case "REPNZ_CMPSW": return Mnemonic.REPNZ_CMPSW;
                case "REPNZ_CMPSD": return Mnemonic.REPNZ_CMPSD;
                case "REPNZ_CMPSQ": return Mnemonic.REPNZ_CMPSQ;

                case "REPNZ_SCAS": return Mnemonic.REPNZ_SCAS;
                case "REPNZ_SCASB": return Mnemonic.REPNZ_SCASB;
                case "REPNZ_SCASW": return Mnemonic.REPNZ_SCASW;
                case "REPNZ_SCASD": return Mnemonic.REPNZ_SCASD;
                case "REPNZ_SCASQ": return Mnemonic.REPNZ_SCASQ;

                case "REP_INS": return Mnemonic.REP_INS;
                case "REP_INSB": return Mnemonic.REP_INSB;
                case "REP_INSW": return Mnemonic.REP_INSW;
                case "REP_INSD": return Mnemonic.REP_INSD;

                case "INS": return Mnemonic.INS;
                case "INSB": return Mnemonic.INSB;
                case "INSW": return Mnemonic.INSW;
                case "INSD": return Mnemonic.INSD;

                case "OUTS": return Mnemonic.OUTS;
                case "OUTSB": return Mnemonic.OUTSB;
                case "OUTSW": return Mnemonic.OUTSW;
                case "OUTSD": return Mnemonic.OUTSD;

                case "REP_OUTS": return Mnemonic.REP_OUTS;
                case "REP_OUTSB": return Mnemonic.REP_OUTSB;
                case "REP_OUTSW": return Mnemonic.REP_OUTSW;
                case "REP_OUTSD": return Mnemonic.REP_OUTSD;

                case "STC": return Mnemonic.STC;
                case "CLC": return Mnemonic.CLC;
                case "CMC": return Mnemonic.CMC;
                case "CLD": return Mnemonic.CLD;
                case "STD": return Mnemonic.STD;
                case "LAHF": return Mnemonic.LAHF;
                case "SAHF": return Mnemonic.SAHF;
                case "PUSHF": return Mnemonic.PUSHF;
                case "PUSHFD": return Mnemonic.PUSHFD;
                case "POPF": return Mnemonic.POPF;
                case "POPFD": return Mnemonic.POPFD;
                case "STI": return Mnemonic.STI;
                case "CLI": return Mnemonic.CLI;
                case "LDS": return Mnemonic.LDS;
                case "LES": return Mnemonic.LES;
                case "LFS": return Mnemonic.LFS;
                case "LGS": return Mnemonic.LGS;
                case "LSS": return Mnemonic.LSS;
                case "LEA": return Mnemonic.LEA;
                case "NOP": return Mnemonic.NOP;
                case "XLAT": return Mnemonic.XLAT;
                case "XLATB": return Mnemonic.XLATB;
                case "CPUID": return Mnemonic.CPUID;
                case "MOVBE": return Mnemonic.MOVBE;
                case "PREFETCHW": return Mnemonic.PREFETCHW;
                case "PREFETCHWT1": return Mnemonic.PREFETCHWT1;
                case "CLFLUSH": return Mnemonic.CLFLUSH;
                case "CLFLUSHOPT": return Mnemonic.CLFLUSHOPT;
                case "XSAVE": return Mnemonic.XSAVE;
                case "XSAVEC": return Mnemonic.XSAVEC;
                case "XSAVEOPT": return Mnemonic.XSAVEOPT;
                case "XRSTOR": return Mnemonic.XRSTOR;
                case "XGETBV": return Mnemonic.XGETBV;
                case "RDRAND": return Mnemonic.RDRAND;
                case "RDSEED": return Mnemonic.RDSEED;
                case "ANDN": return Mnemonic.ANDN;
                case "BEXTR": return Mnemonic.BEXTR;
                case "BLSI": return Mnemonic.BLSI;
                case "BLSMSK": return Mnemonic.BLSMSK;
                case "BLSR": return Mnemonic.BLSR;
                case "BZHI": return Mnemonic.BZHI;
                case "LZCNT": return Mnemonic.LZCNT;
                case "MULX": return Mnemonic.MULX;
                case "PDEP": return Mnemonic.PDEP;
                case "PEXT": return Mnemonic.PEXT;
                case "RORX": return Mnemonic.RORX;
                case "SARX": return Mnemonic.SARX;
                case "SHLX": return Mnemonic.SHLX;
                case "SHRX": return Mnemonic.SHRX;
                case "TZCNT": return Mnemonic.TZCNT;

                case "ARPL": return Mnemonic.ARPL;
                case "BB0_RESET": return Mnemonic.BB0_RESET;
                case "BB1_RESET": return Mnemonic.BB1_RESET;
                case "CLTS": return Mnemonic.CLTS;
                case "CMPXCHG486": return Mnemonic.CMPXCHG486;
                case "CMPXCHG16B": return Mnemonic.CMPXCHG16B;
                case "CPU_READ": return Mnemonic.CPU_READ;
                case "CPU_WRITE": return Mnemonic.CPU_WRITE;
                case "CQO": return Mnemonic.CQO;
                case "DMINT": return Mnemonic.DMINT;
                case "EMMS": return Mnemonic.EMMS;
                case "F2XM1": return Mnemonic.F2XM1;
                case "FABS": return Mnemonic.FABS;
                case "FADD": return Mnemonic.FADD;
                case "FADDP": return Mnemonic.FADDP;
                case "FBLD": return Mnemonic.FBLD;
                case "FBSTP": return Mnemonic.FBSTP;
                case "FCHS": return Mnemonic.FCHS;
                case "FCLEX": return Mnemonic.FCLEX;
                case "FCMOVB": return Mnemonic.FCMOVB;
                case "FCMOVBE": return Mnemonic.FCMOVBE;
                case "FCMOVE": return Mnemonic.FCMOVE;
                case "FCMOVNB": return Mnemonic.FCMOVNB;
                case "FCMOVNBE": return Mnemonic.FCMOVNBE;
                case "FCMOVNE": return Mnemonic.FCMOVNE;
                case "FCMOVNU": return Mnemonic.FCMOVNU;
                case "FCMOVU": return Mnemonic.FCMOVU;
                case "FCOM": return Mnemonic.FCOM;
                case "FCOMI": return Mnemonic.FCOMI;
                case "FCOMIP": return Mnemonic.FCOMIP;
                case "FCOMP": return Mnemonic.FCOMP;
                case "FCOMPP": return Mnemonic.FCOMPP;
                case "FCOS": return Mnemonic.FCOS;
                case "FDECSTP": return Mnemonic.FDECSTP;
                case "FDISI": return Mnemonic.FDISI;
                case "FDIV": return Mnemonic.FDIV;
                case "FDIVP": return Mnemonic.FDIVP;
                case "FDIVR": return Mnemonic.FDIVR;
                case "FDIVRP": return Mnemonic.FDIVRP;
                case "FEMMS": return Mnemonic.FEMMS;
                case "FENI": return Mnemonic.FENI;
                case "FFREE": return Mnemonic.FFREE;
                case "FFREEP": return Mnemonic.FFREEP;
                case "FIADD": return Mnemonic.FIADD;
                case "FICOM": return Mnemonic.FICOM;
                case "FICOMP": return Mnemonic.FICOMP;
                case "FIDIV": return Mnemonic.FIDIV;
                case "FIDIVR": return Mnemonic.FIDIVR;
                case "FILD": return Mnemonic.FILD;
                case "FIMUL": return Mnemonic.FIMUL;
                case "FINCSTP": return Mnemonic.FINCSTP;
                case "FINIT": return Mnemonic.FINIT;
                case "FIST": return Mnemonic.FIST;
                case "FISTP": return Mnemonic.FISTP;
                case "FISTTP": return Mnemonic.FISTTP;
                case "FISUB": return Mnemonic.FISUB;
                case "FISUBR": return Mnemonic.FISUBR;
                case "FLD": return Mnemonic.FLD;
                case "FLD1": return Mnemonic.FLD1;
                case "FLDCW": return Mnemonic.FLDCW;
                case "FLDENV": return Mnemonic.FLDENV;
                case "FLDL2E": return Mnemonic.FLDL2E;
                case "FLDL2T": return Mnemonic.FLDL2T;
                case "FLDLG2": return Mnemonic.FLDLG2;
                case "FLDLN2": return Mnemonic.FLDLN2;
                case "FLDPI": return Mnemonic.FLDPI;
                case "FLDZ": return Mnemonic.FLDZ;
                case "FMUL": return Mnemonic.FMUL;
                case "FMULP": return Mnemonic.FMULP;
                case "FNCLEX": return Mnemonic.FNCLEX;
                case "FNDISI": return Mnemonic.FNDISI;
                case "FNENI": return Mnemonic.FNENI;
                case "FNINIT": return Mnemonic.FNINIT;
                case "FNOP": return Mnemonic.FNOP;
                case "FNSAVE": return Mnemonic.FNSAVE;
                case "FNSTCW": return Mnemonic.FNSTCW;
                case "FNSTENV": return Mnemonic.FNSTENV;
                case "FNSTSW": return Mnemonic.FNSTSW;
                case "FPATAN": return Mnemonic.FPATAN;
                case "FPREM": return Mnemonic.FPREM;
                case "FPREM1": return Mnemonic.FPREM1;
                case "FPTAN": return Mnemonic.FPTAN;
                case "FRNDINT": return Mnemonic.FRNDINT;
                case "FRSTOR": return Mnemonic.FRSTOR;
                case "FSAVE": return Mnemonic.FSAVE;
                case "FSCALE": return Mnemonic.FSCALE;
                case "FSETPM": return Mnemonic.FSETPM;
                case "FSIN": return Mnemonic.FSIN;
                case "FSINCOS": return Mnemonic.FSINCOS;
                case "FSQRT": return Mnemonic.FSQRT;
                case "FST": return Mnemonic.FST;
                case "FSTCW": return Mnemonic.FSTCW;
                case "FSTENV": return Mnemonic.FSTENV;
                case "FSTP": return Mnemonic.FSTP;
                case "FSTSW": return Mnemonic.FSTSW;
                case "FSUB": return Mnemonic.FSUB;
                case "FSUBP": return Mnemonic.FSUBP;
                case "FSUBR": return Mnemonic.FSUBR;
                case "FSUBRP": return Mnemonic.FSUBRP;
                case "FTST": return Mnemonic.FTST;
                case "FUCOM": return Mnemonic.FUCOM;
                case "FUCOMI": return Mnemonic.FUCOMI;
                case "FUCOMIP": return Mnemonic.FUCOMIP;
                case "FUCOMP": return Mnemonic.FUCOMP;
                case "FUCOMPP": return Mnemonic.FUCOMPP;
                case "FXAM": return Mnemonic.FXAM;
                case "FXCH": return Mnemonic.FXCH;
                case "FXTRACT": return Mnemonic.FXTRACT;
                case "FYL2X": return Mnemonic.FYL2X;
                case "FYL2XP1": return Mnemonic.FYL2XP1;
                case "HLT": return Mnemonic.HLT;
                case "IBTS": return Mnemonic.IBTS;
                case "ICEBP": return Mnemonic.ICEBP;
                case "INCBIN": return Mnemonic.INCBIN;
                case "INT01": return Mnemonic.INT01;
                case "INT1": return Mnemonic.INT1;
                case "INT03": return Mnemonic.INT03;
                case "INT3": return Mnemonic.INT3;
                case "INVD": return Mnemonic.INVD;
                case "INVPCID": return Mnemonic.INVPCID;
                case "INVLPG": return Mnemonic.INVLPG;
                case "INVLPGA": return Mnemonic.INVLPGA;
                case "IRETD": return Mnemonic.IRETD;
                case "IRETQ": return Mnemonic.IRETQ;
                case "IRETW": return Mnemonic.IRETW;
                case "JMPE": return Mnemonic.JMPE;
                case "LAR": return Mnemonic.LAR;
                case "LFENCE": return Mnemonic.LFENCE;
                case "LGDT": return Mnemonic.LGDT;
                case "LIDT": return Mnemonic.LIDT;
                case "LLDT": return Mnemonic.LLDT;
                case "LMSW": return Mnemonic.LMSW;
                case "LOADALL": return Mnemonic.LOADALL;
                case "LOADALL286": return Mnemonic.LOADALL286;
                case "CLWB": return Mnemonic.CLWB;
                case "LSL": return Mnemonic.LSL;
                case "LTR": return Mnemonic.LTR;
                case "MFENCE": return Mnemonic.MFENCE;
                case "MONITOR": return Mnemonic.MONITOR;
                case "MONITORX": return Mnemonic.MONITORX;
                case "MOVD": return Mnemonic.MOVD;
                case "MOVQ": return Mnemonic.MOVQ;
                case "MWAIT": return Mnemonic.MWAIT;
                case "MWAITX": return Mnemonic.MWAITX;
                case "PACKSSDW": return Mnemonic.PACKSSDW;
                case "PACKSSWB": return Mnemonic.PACKSSWB;
                case "PACKUSWB": return Mnemonic.PACKUSWB;
                case "PADDB": return Mnemonic.PADDB;
                case "PADDD": return Mnemonic.PADDD;
                case "PADDSB": return Mnemonic.PADDSB;
                case "PADDSIW": return Mnemonic.PADDSIW;
                case "PADDSW": return Mnemonic.PADDSW;
                case "PADDUSB": return Mnemonic.PADDUSB;
                case "PADDUSW": return Mnemonic.PADDUSW;
                case "PADDW": return Mnemonic.PADDW;
                case "PAND": return Mnemonic.PAND;
                case "PANDN": return Mnemonic.PANDN;
                case "PAUSE": return Mnemonic.PAUSE;
                case "PAVEB": return Mnemonic.PAVEB;
                case "PAVGUSB": return Mnemonic.PAVGUSB;
                case "PCMPEQB": return Mnemonic.PCMPEQB;
                case "PCMPEQD": return Mnemonic.PCMPEQD;
                case "PCMPEQW": return Mnemonic.PCMPEQW;
                case "PCMPGTB": return Mnemonic.PCMPGTB;
                case "PCMPGTD": return Mnemonic.PCMPGTD;
                case "PCMPGTW": return Mnemonic.PCMPGTW;
                case "PDISTIB": return Mnemonic.PDISTIB;
                case "PF2ID": return Mnemonic.PF2ID;
                case "PFACC": return Mnemonic.PFACC;
                case "PFADD": return Mnemonic.PFADD;
                case "PFCMPEQ": return Mnemonic.PFCMPEQ;
                case "PFCMPGE": return Mnemonic.PFCMPGE;
                case "PFCMPGT": return Mnemonic.PFCMPGT;
                case "PFMAX": return Mnemonic.PFMAX;
                case "PFMIN": return Mnemonic.PFMIN;
                case "PFMUL": return Mnemonic.PFMUL;
                case "PFRCP": return Mnemonic.PFRCP;
                case "PFRCPIT1": return Mnemonic.PFRCPIT1;
                case "PFRCPIT2": return Mnemonic.PFRCPIT2;
                case "PFRSQIT1": return Mnemonic.PFRSQIT1;
                case "PFRSQRT": return Mnemonic.PFRSQRT;
                case "PFSUB": return Mnemonic.PFSUB;
                case "PFSUBR": return Mnemonic.PFSUBR;
                case "PI2FD": return Mnemonic.PI2FD;
                case "PMACHRIW": return Mnemonic.PMACHRIW;
                case "PMADDWD": return Mnemonic.PMADDWD;
                case "PMAGW": return Mnemonic.PMAGW;
                case "PMULHRIW": return Mnemonic.PMULHRIW;
                case "PMULHRWA": return Mnemonic.PMULHRWA;
                case "PMULHRWC": return Mnemonic.PMULHRWC;
                case "PMULHW": return Mnemonic.PMULHW;
                case "PMULLW": return Mnemonic.PMULLW;
                case "PMVGEZB": return Mnemonic.PMVGEZB;
                case "PMVLZB": return Mnemonic.PMVLZB;
                case "PMVNZB": return Mnemonic.PMVNZB;
                case "PMVZB": return Mnemonic.PMVZB;
                case "POPAW": return Mnemonic.POPAW;
                case "POPFQ": return Mnemonic.POPFQ;
                case "POPFW": return Mnemonic.POPFW;
                case "POR": return Mnemonic.POR;
                case "PREFETCH": return Mnemonic.PREFETCH;
                case "PSLLD": return Mnemonic.PSLLD;
                case "PSLLQ": return Mnemonic.PSLLQ;
                case "PSLLW": return Mnemonic.PSLLW;
                case "PSRAD": return Mnemonic.PSRAD;
                case "PSRAW": return Mnemonic.PSRAW;
                case "PSRLD": return Mnemonic.PSRLD;
                case "PSRLQ": return Mnemonic.PSRLQ;
                case "PSRLW": return Mnemonic.PSRLW;
                case "PSUBB": return Mnemonic.PSUBB;
                case "PSUBD": return Mnemonic.PSUBD;
                case "PSUBSB": return Mnemonic.PSUBSB;
                case "PSUBSIW": return Mnemonic.PSUBSIW;
                case "PSUBSW": return Mnemonic.PSUBSW;
                case "PSUBUSB": return Mnemonic.PSUBUSB;
                case "PSUBUSW": return Mnemonic.PSUBUSW;
                case "PSUBW": return Mnemonic.PSUBW;
                case "PUNPCKHBW": return Mnemonic.PUNPCKHBW;
                case "PUNPCKHDQ": return Mnemonic.PUNPCKHDQ;
                case "PUNPCKHWD": return Mnemonic.PUNPCKHWD;
                case "PUNPCKLBW": return Mnemonic.PUNPCKLBW;
                case "PUNPCKLDQ": return Mnemonic.PUNPCKLDQ;
                case "PUNPCKLWD": return Mnemonic.PUNPCKLWD;
                case "PUSHAW": return Mnemonic.PUSHAW;
                case "PUSHFQ": return Mnemonic.PUSHFQ;
                case "PUSHFW": return Mnemonic.PUSHFW;
                case "PXOR": return Mnemonic.PXOR;
                case "RDMSR": return Mnemonic.RDMSR;
                case "RDPMC": return Mnemonic.RDPMC;
                case "RDTSC": return Mnemonic.RDTSC;
                case "RDTSCP": return Mnemonic.RDTSCP;
                case "RETF": return Mnemonic.RETF;
                case "RETN": return Mnemonic.RETN;
                case "RDM": return Mnemonic.RDM;
                case "RSDC": return Mnemonic.RSDC;
                case "RSLDT": return Mnemonic.RSLDT;
                case "RSM": return Mnemonic.RSM;
                case "RSTS": return Mnemonic.RSTS;
                case "SALC": return Mnemonic.SALC;
                case "SFENCE": return Mnemonic.SFENCE;
                case "SGDT": return Mnemonic.SGDT;
                case "SIDT": return Mnemonic.SIDT;
                case "SLDT": return Mnemonic.SLDT;
                case "SKINIT": return Mnemonic.SKINIT;
                case "SMI": return Mnemonic.SMI;
                case "SMINT": return Mnemonic.SMINT;
                case "SMINTOLD": return Mnemonic.SMINTOLD;
                case "SMSW": return Mnemonic.SMSW;
                case "STR": return Mnemonic.STR;
                case "SVDC": return Mnemonic.SVDC;
                case "SVLDT": return Mnemonic.SVLDT;
                case "SVTS": return Mnemonic.SVTS;
                case "SWAPGS": return Mnemonic.SWAPGS;
                case "SYSCALL": return Mnemonic.SYSCALL;
                case "SYSENTER": return Mnemonic.SYSENTER;
                case "SYSEXIT": return Mnemonic.SYSEXIT;
                case "SYSRET": return Mnemonic.SYSRET;
                case "UD01": return Mnemonic.UD01;
                case "UD1": return Mnemonic.UD1;
                case "UD2": return Mnemonic.UD2;
                case "UMOV": return Mnemonic.UMOV;
                case "VERR": return Mnemonic.VERR;
                case "VERW": return Mnemonic.VERW;
                case "FWAIT": return Mnemonic.FWAIT;
                case "WBINVD": return Mnemonic.WBINVD;
                case "WRSHR": return Mnemonic.WRSHR;
                case "WRMSR": return Mnemonic.WRMSR;
                case "XBTS": return Mnemonic.XBTS;
                case "ADDPS": return Mnemonic.ADDPS;
                case "ADDSS": return Mnemonic.ADDSS;
                case "ANDNPS": return Mnemonic.ANDNPS;
                case "XORPS": return Mnemonic.XORPS;
                case "XORPD": return Mnemonic.XORPD;
                case "ANDPS": return Mnemonic.ANDPS;
                case "CMPEQPS": return Mnemonic.CMPEQPS;
                case "CMPEQSS": return Mnemonic.CMPEQSS;
                case "CMPLEPS": return Mnemonic.CMPLEPS;
                case "CMPLESS": return Mnemonic.CMPLESS;
                case "CMPLTPS": return Mnemonic.CMPLTPS;
                case "CMPLTSS": return Mnemonic.CMPLTSS;
                case "CMPNEQPS": return Mnemonic.CMPNEQPS;
                case "CMPNEQSS": return Mnemonic.CMPNEQSS;
                case "CMPNLEPS": return Mnemonic.CMPNLEPS;
                case "CMPNLESS": return Mnemonic.CMPNLESS;
                case "CMPNLTPS": return Mnemonic.CMPNLTPS;
                case "CMPNLTSS": return Mnemonic.CMPNLTSS;
                case "CMPORDPS": return Mnemonic.CMPORDPS;
                case "CMPORDSS": return Mnemonic.CMPORDSS;
                case "CMPUNORDPS": return Mnemonic.CMPUNORDPS;
                case "CMPUNORDSS": return Mnemonic.CMPUNORDSS;
                case "CMPPS": return Mnemonic.CMPPS;
                case "CMPSS": return Mnemonic.CMPSS;
                case "COMISS": return Mnemonic.COMISS;
                case "CVTPI2PS": return Mnemonic.CVTPI2PS;
                case "CVTPS2PI": return Mnemonic.CVTPS2PI;
                case "CVTSI2SS": return Mnemonic.CVTSI2SS;
                case "CVTSS2SI": return Mnemonic.CVTSS2SI;
                case "CVTTPS2PI": return Mnemonic.CVTTPS2PI;
                case "CVTTSS2SI": return Mnemonic.CVTTSS2SI;
                case "DIVPS": return Mnemonic.DIVPS;
                case "DIVSS": return Mnemonic.DIVSS;
                case "LDMXCSR": return Mnemonic.LDMXCSR;
                case "MAXPS": return Mnemonic.MAXPS;
                case "MAXSS": return Mnemonic.MAXSS;
                case "MINPS": return Mnemonic.MINPS;
                case "MINSS": return Mnemonic.MINSS;
                case "MOVAPS": return Mnemonic.MOVAPS;
                case "MOVHPS": return Mnemonic.MOVHPS;
                case "MOVLHPS": return Mnemonic.MOVLHPS;
                case "MOVLPS": return Mnemonic.MOVLPS;
                case "MOVHLPS": return Mnemonic.MOVHLPS;
                case "MOVMSKPS": return Mnemonic.MOVMSKPS;
                case "MOVNTPS": return Mnemonic.MOVNTPS;
                case "MOVSS": return Mnemonic.MOVSS;
                case "MOVUPS": return Mnemonic.MOVUPS;
                case "MULPS": return Mnemonic.MULPS;
                case "MULSS": return Mnemonic.MULSS;
                case "ORPS": return Mnemonic.ORPS;
                case "RCPPS": return Mnemonic.RCPPS;
                case "RCPSS": return Mnemonic.RCPSS;
                case "RSQRTPS": return Mnemonic.RSQRTPS;
                case "RSQRTSS": return Mnemonic.RSQRTSS;
                case "SHUFPS": return Mnemonic.SHUFPS;
                case "SQRTPS": return Mnemonic.SQRTPS;
                case "SQRTSS": return Mnemonic.SQRTSS;
                case "STMXCSR": return Mnemonic.STMXCSR;
                case "SUBPS": return Mnemonic.SUBPS;
                case "SUBSS": return Mnemonic.SUBSS;
                case "UCOMISS": return Mnemonic.UCOMISS;
                case "UNPCKHPS": return Mnemonic.UNPCKHPS;
                case "UNPCKLPS": return Mnemonic.UNPCKLPS;
                case "FXRSTOR": return Mnemonic.FXRSTOR;
                case "FXRSTOR64": return Mnemonic.FXRSTOR64;
                case "FXSAVE": return Mnemonic.FXSAVE;
                case "FXSAVE64": return Mnemonic.FXSAVE64;
                case "XSETBV": return Mnemonic.XSETBV;
                case "XSAVE64": return Mnemonic.XSAVE64;
                case "XSAVEC64": return Mnemonic.XSAVEC64;
                case "XSAVEOPT64": return Mnemonic.XSAVEOPT64;
                case "XSAVES": return Mnemonic.XSAVES;
                case "XSAVES64": return Mnemonic.XSAVES64;
                case "XRSTOR64": return Mnemonic.XRSTOR64;
                case "XRSTORS": return Mnemonic.XRSTORS;
                case "XRSTORS64": return Mnemonic.XRSTORS64;
                case "PREFETCHNTA": return Mnemonic.PREFETCHNTA;
                case "PREFETCHT0": return Mnemonic.PREFETCHT0;
                case "PREFETCHT1": return Mnemonic.PREFETCHT1;
                case "PREFETCHT2": return Mnemonic.PREFETCHT2;
                case "MASKMOVQ": return Mnemonic.MASKMOVQ;
                case "MOVNTQ": return Mnemonic.MOVNTQ;
                case "PAVGB": return Mnemonic.PAVGB;
                case "PAVGW": return Mnemonic.PAVGW;
                case "PEXTRW": return Mnemonic.PEXTRW;
                case "PINSRW": return Mnemonic.PINSRW;
                case "PMAXSW": return Mnemonic.PMAXSW;
                case "PMAXUB": return Mnemonic.PMAXUB;
                case "PMINSW": return Mnemonic.PMINSW;
                case "PMINUB": return Mnemonic.PMINUB;
                case "PMOVMSKB": return Mnemonic.PMOVMSKB;
                case "PMULHUW": return Mnemonic.PMULHUW;
                case "PSADBW": return Mnemonic.PSADBW;
                case "PSHUFW": return Mnemonic.PSHUFW;
                case "PF2IW": return Mnemonic.PF2IW;
                case "PFNACC": return Mnemonic.PFNACC;
                case "PFPNACC": return Mnemonic.PFPNACC;
                case "PI2FW": return Mnemonic.PI2FW;
                case "PSWAPD": return Mnemonic.PSWAPD;
                case "MASKMOVDQU": return Mnemonic.MASKMOVDQU;
                case "MOVNTDQ": return Mnemonic.MOVNTDQ;
                case "MOVNTI": return Mnemonic.MOVNTI;
                case "MOVNTPD": return Mnemonic.MOVNTPD;
                case "MOVDQA": return Mnemonic.MOVDQA;
                case "MOVDQU": return Mnemonic.MOVDQU;
                case "MOVDQ2Q": return Mnemonic.MOVDQ2Q;
                case "MOVQ2DQ": return Mnemonic.MOVQ2DQ;
                case "PADDQ": return Mnemonic.PADDQ;
                case "PMULUDQ": return Mnemonic.PMULUDQ;
                case "PSHUFD": return Mnemonic.PSHUFD;
                case "PSHUFHW": return Mnemonic.PSHUFHW;
                case "PSHUFLW": return Mnemonic.PSHUFLW;
                case "PSLLDQ": return Mnemonic.PSLLDQ;
                case "PSRLDQ": return Mnemonic.PSRLDQ;
                case "PSUBQ": return Mnemonic.PSUBQ;
                case "PUNPCKHQDQ": return Mnemonic.PUNPCKHQDQ;
                case "PUNPCKLQDQ": return Mnemonic.PUNPCKLQDQ;
                case "ADDPD": return Mnemonic.ADDPD;
                case "ADDSD": return Mnemonic.ADDSD;
                case "ANDNPD": return Mnemonic.ANDNPD;
                case "ANDPD": return Mnemonic.ANDPD;
                case "CMPEQPD": return Mnemonic.CMPEQPD;
                case "CMPEQSD": return Mnemonic.CMPEQSD;
                case "CMPLEPD": return Mnemonic.CMPLEPD;
                case "CMPLESD": return Mnemonic.CMPLESD;
                case "CMPLTPD": return Mnemonic.CMPLTPD;
                case "CMPLTSD": return Mnemonic.CMPLTSD;
                case "CMPNEQPD": return Mnemonic.CMPNEQPD;
                case "CMPNEQSD": return Mnemonic.CMPNEQSD;
                case "CMPNLEPD": return Mnemonic.CMPNLEPD;
                case "CMPNLESD": return Mnemonic.CMPNLESD;
                case "CMPNLTPD": return Mnemonic.CMPNLTPD;
                case "CMPNLTSD": return Mnemonic.CMPNLTSD;
                case "CMPORDPD": return Mnemonic.CMPORDPD;
                case "CMPORDSD": return Mnemonic.CMPORDSD;
                case "CMPUNORDPD": return Mnemonic.CMPUNORDPD;
                case "CMPUNORDSD": return Mnemonic.CMPUNORDSD;
                case "CMPPD": return Mnemonic.CMPPD;
                case "COMISD": return Mnemonic.COMISD;
                case "CVTDQ2PD": return Mnemonic.CVTDQ2PD;
                case "CVTDQ2PS": return Mnemonic.CVTDQ2PS;
                case "CVTPD2DQ": return Mnemonic.CVTPD2DQ;
                case "CVTPD2PI": return Mnemonic.CVTPD2PI;
                case "CVTPD2PS": return Mnemonic.CVTPD2PS;
                case "CVTPI2PD": return Mnemonic.CVTPI2PD;
                case "CVTPS2DQ": return Mnemonic.CVTPS2DQ;
                case "CVTPS2PD": return Mnemonic.CVTPS2PD;
                case "CVTSD2SI": return Mnemonic.CVTSD2SI;
                case "CVTSD2SS": return Mnemonic.CVTSD2SS;
                case "CVTSI2SD": return Mnemonic.CVTSI2SD;
                case "CVTSS2SD": return Mnemonic.CVTSS2SD;
                case "CVTTPD2PI": return Mnemonic.CVTTPD2PI;
                case "CVTTPD2DQ": return Mnemonic.CVTTPD2DQ;
                case "CVTTPS2DQ": return Mnemonic.CVTTPS2DQ;
                case "CVTTSD2SI": return Mnemonic.CVTTSD2SI;
                case "DIVPD": return Mnemonic.DIVPD;
                case "DIVSD": return Mnemonic.DIVSD;
                case "MAXPD": return Mnemonic.MAXPD;
                case "MAXSD": return Mnemonic.MAXSD;
                case "MINPD": return Mnemonic.MINPD;
                case "MINSD": return Mnemonic.MINSD;
                case "MOVAPD": return Mnemonic.MOVAPD;
                case "MOVHPD": return Mnemonic.MOVHPD;
                case "MOVLPD": return Mnemonic.MOVLPD;
                case "MOVMSKPD": return Mnemonic.MOVMSKPD;
                case "MOVUPD": return Mnemonic.MOVUPD;
                case "MULPD": return Mnemonic.MULPD;
                case "MULSD": return Mnemonic.MULSD;
                case "ORPD": return Mnemonic.ORPD;
                case "SHUFPD": return Mnemonic.SHUFPD;
                case "SQRTPD": return Mnemonic.SQRTPD;
                case "SQRTSD": return Mnemonic.SQRTSD;
                case "SUBPD": return Mnemonic.SUBPD;
                case "SUBSD": return Mnemonic.SUBSD;
                case "UCOMISD": return Mnemonic.UCOMISD;
                case "UNPCKHPD": return Mnemonic.UNPCKHPD;
                case "UNPCKLPD": return Mnemonic.UNPCKLPD;
                case "ADDSUBPD": return Mnemonic.ADDSUBPD;
                case "ADDSUBPS": return Mnemonic.ADDSUBPS;
                case "HADDPD": return Mnemonic.HADDPD;
                case "HADDPS": return Mnemonic.HADDPS;
                case "HSUBPD": return Mnemonic.HSUBPD;
                case "HSUBPS": return Mnemonic.HSUBPS;
                case "LDDQU": return Mnemonic.LDDQU;
                case "MOVDDUP": return Mnemonic.MOVDDUP;
                case "MOVSHDUP": return Mnemonic.MOVSHDUP;
                case "MOVSLDUP": return Mnemonic.MOVSLDUP;
                case "CLGI": return Mnemonic.CLGI;
                case "STGI": return Mnemonic.STGI;
                case "VMCALL": return Mnemonic.VMCALL;
                case "VMCLEAR": return Mnemonic.VMCLEAR;
                case "VMFUNC": return Mnemonic.VMFUNC;
                case "VMLAUNCH": return Mnemonic.VMLAUNCH;
                case "VMLOAD": return Mnemonic.VMLOAD;
                case "VMMCALL": return Mnemonic.VMMCALL;
                case "VMPTRLD": return Mnemonic.VMPTRLD;
                case "VMPTRST": return Mnemonic.VMPTRST;
                case "VMREAD": return Mnemonic.VMREAD;
                case "VMRESUME": return Mnemonic.VMRESUME;
                case "VMRUN": return Mnemonic.VMRUN;
                case "VMSAVE": return Mnemonic.VMSAVE;
                case "VMWRITE": return Mnemonic.VMWRITE;
                case "VMXOFF": return Mnemonic.VMXOFF;
                case "VMXON": return Mnemonic.VMXON;
                case "INVEPT": return Mnemonic.INVEPT;
                case "INVVPID": return Mnemonic.INVVPID;
                case "PABSB": return Mnemonic.PABSB;
                case "PABSW": return Mnemonic.PABSW;
                case "PABSD": return Mnemonic.PABSD;
                case "PALIGNR": return Mnemonic.PALIGNR;
                case "PHADDW": return Mnemonic.PHADDW;
                case "PHADDD": return Mnemonic.PHADDD;
                case "PHADDSW": return Mnemonic.PHADDSW;
                case "PHSUBW": return Mnemonic.PHSUBW;
                case "PHSUBD": return Mnemonic.PHSUBD;
                case "PHSUBSW": return Mnemonic.PHSUBSW;
                case "PMADDUBSW": return Mnemonic.PMADDUBSW;
                case "PMULHRSW": return Mnemonic.PMULHRSW;
                case "PSHUFB": return Mnemonic.PSHUFB;
                case "PSIGNB": return Mnemonic.PSIGNB;
                case "PSIGNW": return Mnemonic.PSIGNW;
                case "PSIGND": return Mnemonic.PSIGND;
                case "EXTRQ": return Mnemonic.EXTRQ;
                case "INSERTQ": return Mnemonic.INSERTQ;
                case "MOVNTSD": return Mnemonic.MOVNTSD;
                case "MOVNTSS": return Mnemonic.MOVNTSS;
                case "BLENDPD": return Mnemonic.BLENDPD;
                case "BLENDPS": return Mnemonic.BLENDPS;
                case "BLENDVPD": return Mnemonic.BLENDVPD;
                case "BLENDVPS": return Mnemonic.BLENDVPS;
                case "DPPD": return Mnemonic.DPPD;
                case "DPPS": return Mnemonic.DPPS;
                case "EXTRACTPS": return Mnemonic.EXTRACTPS;
                case "INSERTPS": return Mnemonic.INSERTPS;
                case "MOVNTDQA": return Mnemonic.MOVNTDQA;
                case "MPSADBW": return Mnemonic.MPSADBW;
                case "PACKUSDW": return Mnemonic.PACKUSDW;
                case "PBLENDVB": return Mnemonic.PBLENDVB;
                case "PBLENDW": return Mnemonic.PBLENDW;
                case "PCMPEQQ": return Mnemonic.PCMPEQQ;
                case "PEXTRB": return Mnemonic.PEXTRB;
                case "PEXTRD": return Mnemonic.PEXTRD;
                case "PEXTRQ": return Mnemonic.PEXTRQ;
                case "PHMINPOSUW": return Mnemonic.PHMINPOSUW;
                case "PINSRB": return Mnemonic.PINSRB;
                case "PINSRD": return Mnemonic.PINSRD;
                case "PINSRQ": return Mnemonic.PINSRQ;
                case "PMAXSB": return Mnemonic.PMAXSB;
                case "PMAXSD": return Mnemonic.PMAXSD;
                case "PMAXUD": return Mnemonic.PMAXUD;
                case "PMAXUW": return Mnemonic.PMAXUW;
                case "PMINSB": return Mnemonic.PMINSB;
                case "PMINSD": return Mnemonic.PMINSD;
                case "PMINUD": return Mnemonic.PMINUD;
                case "PMINUW": return Mnemonic.PMINUW;
                case "PMOVSXBW": return Mnemonic.PMOVSXBW;
                case "PMOVSXBD": return Mnemonic.PMOVSXBD;
                case "PMOVSXBQ": return Mnemonic.PMOVSXBQ;
                case "PMOVSXWD": return Mnemonic.PMOVSXWD;
                case "PMOVSXWQ": return Mnemonic.PMOVSXWQ;
                case "PMOVSXDQ": return Mnemonic.PMOVSXDQ;
                case "PMOVZXBW": return Mnemonic.PMOVZXBW;
                case "PMOVZXBD": return Mnemonic.PMOVZXBD;
                case "PMOVZXBQ": return Mnemonic.PMOVZXBQ;
                case "PMOVZXWD": return Mnemonic.PMOVZXWD;
                case "PMOVZXWQ": return Mnemonic.PMOVZXWQ;
                case "PMOVZXDQ": return Mnemonic.PMOVZXDQ;
                case "PMULDQ": return Mnemonic.PMULDQ;
                case "PMULLD": return Mnemonic.PMULLD;
                case "PTEST": return Mnemonic.PTEST;
                case "ROUNDPD": return Mnemonic.ROUNDPD;
                case "ROUNDPS": return Mnemonic.ROUNDPS;
                case "ROUNDSD": return Mnemonic.ROUNDSD;
                case "ROUNDSS": return Mnemonic.ROUNDSS;
                case "PCMPESTRI": return Mnemonic.PCMPESTRI;
                case "PCMPESTRM": return Mnemonic.PCMPESTRM;
                case "PCMPISTRI": return Mnemonic.PCMPISTRI;
                case "PCMPISTRM": return Mnemonic.PCMPISTRM;
                case "PCMPGTQ": return Mnemonic.PCMPGTQ;
                case "GETSEC": return Mnemonic.GETSEC;
                case "PFRCPV": return Mnemonic.PFRCPV;
                case "PFRSQRTV": return Mnemonic.PFRSQRTV;
                case "AESENC": return Mnemonic.AESENC;
                case "AESENCLAST": return Mnemonic.AESENCLAST;
                case "AESDEC": return Mnemonic.AESDEC;
                case "AESDECLAST": return Mnemonic.AESDECLAST;
                case "AESIMC": return Mnemonic.AESIMC;
                case "AESKEYGENASSIST": return Mnemonic.AESKEYGENASSIST;
                case "VAESENC": return Mnemonic.VAESENC;
                case "VAESENCLAST": return Mnemonic.VAESENCLAST;
                case "VAESDEC": return Mnemonic.VAESDEC;
                case "VAESDECLAST": return Mnemonic.VAESDECLAST;
                case "VAESIMC": return Mnemonic.VAESIMC;
                case "VAESKEYGENASSIST": return Mnemonic.VAESKEYGENASSIST;
                case "VADDPD": return Mnemonic.VADDPD;
                case "VADDPS": return Mnemonic.VADDPS;
                case "VADDSD": return Mnemonic.VADDSD;
                case "VADDSS": return Mnemonic.VADDSS;
                case "VADDSUBPD": return Mnemonic.VADDSUBPD;
                case "VADDSUBPS": return Mnemonic.VADDSUBPS;
                case "VANDPD": return Mnemonic.VANDPD;
                case "VANDPS": return Mnemonic.VANDPS;
                case "VANDNPD": return Mnemonic.VANDNPD;
                case "VANDNPS": return Mnemonic.VANDNPS;
                case "VBLENDPD": return Mnemonic.VBLENDPD;
                case "VBLENDPS": return Mnemonic.VBLENDPS;
                case "VBLENDVPD": return Mnemonic.VBLENDVPD;
                case "VBLENDVPS": return Mnemonic.VBLENDVPS;
                case "VBROADCASTSS": return Mnemonic.VBROADCASTSS;
                case "VBROADCASTSD": return Mnemonic.VBROADCASTSD;
                case "VBROADCASTF128": return Mnemonic.VBROADCASTF128;
                case "VCMPEQ_OSPD": return Mnemonic.VCMPEQ_OSPD;
                case "VCMPEQPD": return Mnemonic.VCMPEQPD;
                case "VCMPLT_OSPD": return Mnemonic.VCMPLT_OSPD;
                case "VCMPLTPD": return Mnemonic.VCMPLTPD;
                case "VCMPLE_OSPD": return Mnemonic.VCMPLE_OSPD;
                case "VCMPLEPD": return Mnemonic.VCMPLEPD;
                case "VCMPUNORD_QPD": return Mnemonic.VCMPUNORD_QPD;
                case "VCMPUNORDPD": return Mnemonic.VCMPUNORDPD;
                case "VCMPNEQ_UQPD": return Mnemonic.VCMPNEQ_UQPD;
                case "VCMPNEQPD": return Mnemonic.VCMPNEQPD;
                case "VCMPNLT_USPD": return Mnemonic.VCMPNLT_USPD;
                case "VCMPNLTPD": return Mnemonic.VCMPNLTPD;
                case "VCMPNLE_USPD": return Mnemonic.VCMPNLE_USPD;
                case "VCMPNLEPD": return Mnemonic.VCMPNLEPD;
                case "VCMPORD_QPD": return Mnemonic.VCMPORD_QPD;
                case "VCMPORDPD": return Mnemonic.VCMPORDPD;
                case "VCMPEQ_UQPD": return Mnemonic.VCMPEQ_UQPD;
                case "VCMPNGE_USPD": return Mnemonic.VCMPNGE_USPD;
                case "VCMPNGEPD": return Mnemonic.VCMPNGEPD;
                case "VCMPNGT_USPD": return Mnemonic.VCMPNGT_USPD;
                case "VCMPNGTPD": return Mnemonic.VCMPNGTPD;
                case "VCMPFALSE_OQPD": return Mnemonic.VCMPFALSE_OQPD;
                case "VCMPFALSEPD": return Mnemonic.VCMPFALSEPD;
                case "VCMPNEQ_OQPD": return Mnemonic.VCMPNEQ_OQPD;
                case "VCMPGE_OSPD": return Mnemonic.VCMPGE_OSPD;
                case "VCMPGEPD": return Mnemonic.VCMPGEPD;
                case "VCMPGT_OSPD": return Mnemonic.VCMPGT_OSPD;
                case "VCMPGTPD": return Mnemonic.VCMPGTPD;
                case "VCMPTRUE_UQPD": return Mnemonic.VCMPTRUE_UQPD;
                case "VCMPTRUEPD": return Mnemonic.VCMPTRUEPD;
                case "VCMPLT_OQPD": return Mnemonic.VCMPLT_OQPD;
                case "VCMPLE_OQPD": return Mnemonic.VCMPLE_OQPD;
                case "VCMPUNORD_SPD": return Mnemonic.VCMPUNORD_SPD;
                case "VCMPNEQ_USPD": return Mnemonic.VCMPNEQ_USPD;
                case "VCMPNLT_UQPD": return Mnemonic.VCMPNLT_UQPD;
                case "VCMPNLE_UQPD": return Mnemonic.VCMPNLE_UQPD;
                case "VCMPORD_SPD": return Mnemonic.VCMPORD_SPD;
                case "VCMPEQ_USPD": return Mnemonic.VCMPEQ_USPD;
                case "VCMPNGE_UQPD": return Mnemonic.VCMPNGE_UQPD;
                case "VCMPNGT_UQPD": return Mnemonic.VCMPNGT_UQPD;
                case "VCMPFALSE_OSPD": return Mnemonic.VCMPFALSE_OSPD;
                case "VCMPNEQ_OSPD": return Mnemonic.VCMPNEQ_OSPD;
                case "VCMPGE_OQPD": return Mnemonic.VCMPGE_OQPD;
                case "VCMPGT_OQPD": return Mnemonic.VCMPGT_OQPD;
                case "VCMPTRUE_USPD": return Mnemonic.VCMPTRUE_USPD;
                case "VCMPPD": return Mnemonic.VCMPPD;
                case "VCMPEQ_OSPS": return Mnemonic.VCMPEQ_OSPS;
                case "VCMPEQPS": return Mnemonic.VCMPEQPS;
                case "VCMPLT_OSPS": return Mnemonic.VCMPLT_OSPS;
                case "VCMPLTPS": return Mnemonic.VCMPLTPS;
                case "VCMPLE_OSPS": return Mnemonic.VCMPLE_OSPS;
                case "VCMPLEPS": return Mnemonic.VCMPLEPS;
                case "VCMPUNORD_QPS": return Mnemonic.VCMPUNORD_QPS;
                case "VCMPUNORDPS": return Mnemonic.VCMPUNORDPS;
                case "VCMPNEQ_UQPS": return Mnemonic.VCMPNEQ_UQPS;
                case "VCMPNEQPS": return Mnemonic.VCMPNEQPS;
                case "VCMPNLT_USPS": return Mnemonic.VCMPNLT_USPS;
                case "VCMPNLTPS": return Mnemonic.VCMPNLTPS;
                case "VCMPNLE_USPS": return Mnemonic.VCMPNLE_USPS;
                case "VCMPNLEPS": return Mnemonic.VCMPNLEPS;
                case "VCMPORD_QPS": return Mnemonic.VCMPORD_QPS;
                case "VCMPORDPS": return Mnemonic.VCMPORDPS;
                case "VCMPEQ_UQPS": return Mnemonic.VCMPEQ_UQPS;
                case "VCMPNGE_USPS": return Mnemonic.VCMPNGE_USPS;
                case "VCMPNGEPS": return Mnemonic.VCMPNGEPS;
                case "VCMPNGT_USPS": return Mnemonic.VCMPNGT_USPS;
                case "VCMPNGTPS": return Mnemonic.VCMPNGTPS;
                case "VCMPFALSE_OQPS": return Mnemonic.VCMPFALSE_OQPS;
                case "VCMPFALSEPS": return Mnemonic.VCMPFALSEPS;
                case "VCMPNEQ_OQPS": return Mnemonic.VCMPNEQ_OQPS;
                case "VCMPGE_OSPS": return Mnemonic.VCMPGE_OSPS;
                case "VCMPGEPS": return Mnemonic.VCMPGEPS;
                case "VCMPGT_OSPS": return Mnemonic.VCMPGT_OSPS;
                case "VCMPGTPS": return Mnemonic.VCMPGTPS;
                case "VCMPTRUE_UQPS": return Mnemonic.VCMPTRUE_UQPS;
                case "VCMPTRUEPS": return Mnemonic.VCMPTRUEPS;
                case "VCMPLT_OQPS": return Mnemonic.VCMPLT_OQPS;
                case "VCMPLE_OQPS": return Mnemonic.VCMPLE_OQPS;
                case "VCMPUNORD_SPS": return Mnemonic.VCMPUNORD_SPS;
                case "VCMPNEQ_USPS": return Mnemonic.VCMPNEQ_USPS;
                case "VCMPNLT_UQPS": return Mnemonic.VCMPNLT_UQPS;
                case "VCMPNLE_UQPS": return Mnemonic.VCMPNLE_UQPS;
                case "VCMPORD_SPS": return Mnemonic.VCMPORD_SPS;
                case "VCMPEQ_USPS": return Mnemonic.VCMPEQ_USPS;
                case "VCMPNGE_UQPS": return Mnemonic.VCMPNGE_UQPS;
                case "VCMPNGT_UQPS": return Mnemonic.VCMPNGT_UQPS;
                case "VCMPFALSE_OSPS": return Mnemonic.VCMPFALSE_OSPS;
                case "VCMPNEQ_OSPS": return Mnemonic.VCMPNEQ_OSPS;
                case "VCMPGE_OQPS": return Mnemonic.VCMPGE_OQPS;
                case "VCMPGT_OQPS": return Mnemonic.VCMPGT_OQPS;
                case "VCMPTRUE_USPS": return Mnemonic.VCMPTRUE_USPS;
                case "VCMPPS": return Mnemonic.VCMPPS;
                case "VCMPEQ_OSSD": return Mnemonic.VCMPEQ_OSSD;
                case "VCMPEQSD": return Mnemonic.VCMPEQSD;
                case "VCMPLT_OSSD": return Mnemonic.VCMPLT_OSSD;
                case "VCMPLTSD": return Mnemonic.VCMPLTSD;
                case "VCMPLE_OSSD": return Mnemonic.VCMPLE_OSSD;
                case "VCMPLESD": return Mnemonic.VCMPLESD;
                case "VCMPUNORD_QSD": return Mnemonic.VCMPUNORD_QSD;
                case "VCMPUNORDSD": return Mnemonic.VCMPUNORDSD;
                case "VCMPNEQ_UQSD": return Mnemonic.VCMPNEQ_UQSD;
                case "VCMPNEQSD": return Mnemonic.VCMPNEQSD;
                case "VCMPNLT_USSD": return Mnemonic.VCMPNLT_USSD;
                case "VCMPNLTSD": return Mnemonic.VCMPNLTSD;
                case "VCMPNLE_USSD": return Mnemonic.VCMPNLE_USSD;
                case "VCMPNLESD": return Mnemonic.VCMPNLESD;
                case "VCMPORD_QSD": return Mnemonic.VCMPORD_QSD;
                case "VCMPORDSD": return Mnemonic.VCMPORDSD;
                case "VCMPEQ_UQSD": return Mnemonic.VCMPEQ_UQSD;
                case "VCMPNGE_USSD": return Mnemonic.VCMPNGE_USSD;
                case "VCMPNGESD": return Mnemonic.VCMPNGESD;
                case "VCMPNGT_USSD": return Mnemonic.VCMPNGT_USSD;
                case "VCMPNGTSD": return Mnemonic.VCMPNGTSD;
                case "VCMPFALSE_OQSD": return Mnemonic.VCMPFALSE_OQSD;
                case "VCMPFALSESD": return Mnemonic.VCMPFALSESD;
                case "VCMPNEQ_OQSD": return Mnemonic.VCMPNEQ_OQSD;
                case "VCMPGE_OSSD": return Mnemonic.VCMPGE_OSSD;
                case "VCMPGESD": return Mnemonic.VCMPGESD;
                case "VCMPGT_OSSD": return Mnemonic.VCMPGT_OSSD;
                case "VCMPGTSD": return Mnemonic.VCMPGTSD;
                case "VCMPTRUE_UQSD": return Mnemonic.VCMPTRUE_UQSD;
                case "VCMPTRUESD": return Mnemonic.VCMPTRUESD;
                case "VCMPLT_OQSD": return Mnemonic.VCMPLT_OQSD;
                case "VCMPLE_OQSD": return Mnemonic.VCMPLE_OQSD;
                case "VCMPUNORD_SSD": return Mnemonic.VCMPUNORD_SSD;
                case "VCMPNEQ_USSD": return Mnemonic.VCMPNEQ_USSD;
                case "VCMPNLT_UQSD": return Mnemonic.VCMPNLT_UQSD;
                case "VCMPNLE_UQSD": return Mnemonic.VCMPNLE_UQSD;
                case "VCMPORD_SSD": return Mnemonic.VCMPORD_SSD;
                case "VCMPEQ_USSD": return Mnemonic.VCMPEQ_USSD;
                case "VCMPNGE_UQSD": return Mnemonic.VCMPNGE_UQSD;
                case "VCMPNGT_UQSD": return Mnemonic.VCMPNGT_UQSD;
                case "VCMPFALSE_OSSD": return Mnemonic.VCMPFALSE_OSSD;
                case "VCMPNEQ_OSSD": return Mnemonic.VCMPNEQ_OSSD;
                case "VCMPGE_OQSD": return Mnemonic.VCMPGE_OQSD;
                case "VCMPGT_OQSD": return Mnemonic.VCMPGT_OQSD;
                case "VCMPTRUE_USSD": return Mnemonic.VCMPTRUE_USSD;
                case "VCMPSD": return Mnemonic.VCMPSD;
                case "VCMPEQ_OSSS": return Mnemonic.VCMPEQ_OSSS;
                case "VCMPEQSS": return Mnemonic.VCMPEQSS;
                case "VCMPLT_OSSS": return Mnemonic.VCMPLT_OSSS;
                case "VCMPLTSS": return Mnemonic.VCMPLTSS;
                case "VCMPLE_OSSS": return Mnemonic.VCMPLE_OSSS;
                case "VCMPLESS": return Mnemonic.VCMPLESS;
                case "VCMPUNORD_QSS": return Mnemonic.VCMPUNORD_QSS;
                case "VCMPUNORDSS": return Mnemonic.VCMPUNORDSS;
                case "VCMPNEQ_UQSS": return Mnemonic.VCMPNEQ_UQSS;
                case "VCMPNEQSS": return Mnemonic.VCMPNEQSS;
                case "VCMPNLT_USSS": return Mnemonic.VCMPNLT_USSS;
                case "VCMPNLTSS": return Mnemonic.VCMPNLTSS;
                case "VCMPNLE_USSS": return Mnemonic.VCMPNLE_USSS;
                case "VCMPNLESS": return Mnemonic.VCMPNLESS;
                case "VCMPORD_QSS": return Mnemonic.VCMPORD_QSS;
                case "VCMPORDSS": return Mnemonic.VCMPORDSS;
                case "VCMPEQ_UQSS": return Mnemonic.VCMPEQ_UQSS;
                case "VCMPNGE_USSS": return Mnemonic.VCMPNGE_USSS;
                case "VCMPNGESS": return Mnemonic.VCMPNGESS;
                case "VCMPNGT_USSS": return Mnemonic.VCMPNGT_USSS;
                case "VCMPNGTSS": return Mnemonic.VCMPNGTSS;
                case "VCMPFALSE_OQSS": return Mnemonic.VCMPFALSE_OQSS;
                case "VCMPFALSESS": return Mnemonic.VCMPFALSESS;
                case "VCMPNEQ_OQSS": return Mnemonic.VCMPNEQ_OQSS;
                case "VCMPGE_OSSS": return Mnemonic.VCMPGE_OSSS;
                case "VCMPGESS": return Mnemonic.VCMPGESS;
                case "VCMPGT_OSSS": return Mnemonic.VCMPGT_OSSS;
                case "VCMPGTSS": return Mnemonic.VCMPGTSS;
                case "VCMPTRUE_UQSS": return Mnemonic.VCMPTRUE_UQSS;
                case "VCMPTRUESS": return Mnemonic.VCMPTRUESS;
                case "VCMPLT_OQSS": return Mnemonic.VCMPLT_OQSS;
                case "VCMPLE_OQSS": return Mnemonic.VCMPLE_OQSS;
                case "VCMPUNORD_SSS": return Mnemonic.VCMPUNORD_SSS;
                case "VCMPNEQ_USSS": return Mnemonic.VCMPNEQ_USSS;
                case "VCMPNLT_UQSS": return Mnemonic.VCMPNLT_UQSS;
                case "VCMPNLE_UQSS": return Mnemonic.VCMPNLE_UQSS;
                case "VCMPORD_SSS": return Mnemonic.VCMPORD_SSS;
                case "VCMPEQ_USSS": return Mnemonic.VCMPEQ_USSS;
                case "VCMPNGE_UQSS": return Mnemonic.VCMPNGE_UQSS;
                case "VCMPNGT_UQSS": return Mnemonic.VCMPNGT_UQSS;
                case "VCMPFALSE_OSSS": return Mnemonic.VCMPFALSE_OSSS;
                case "VCMPNEQ_OSSS": return Mnemonic.VCMPNEQ_OSSS;
                case "VCMPGE_OQSS": return Mnemonic.VCMPGE_OQSS;
                case "VCMPGT_OQSS": return Mnemonic.VCMPGT_OQSS;
                case "VCMPTRUE_USSS": return Mnemonic.VCMPTRUE_USSS;
                case "VCMPSS": return Mnemonic.VCMPSS;
                case "VCOMISD": return Mnemonic.VCOMISD;
                case "VCOMISS": return Mnemonic.VCOMISS;
                case "VCVTDQ2PD": return Mnemonic.VCVTDQ2PD;
                case "VCVTDQ2PS": return Mnemonic.VCVTDQ2PS;
                case "VCVTPD2DQ": return Mnemonic.VCVTPD2DQ;
                case "VCVTPD2PS": return Mnemonic.VCVTPD2PS;
                case "VCVTPS2DQ": return Mnemonic.VCVTPS2DQ;
                case "VCVTPS2PD": return Mnemonic.VCVTPS2PD;
                case "VCVTSD2SI": return Mnemonic.VCVTSD2SI;
                case "VCVTSD2SS": return Mnemonic.VCVTSD2SS;
                case "VCVTSI2SD": return Mnemonic.VCVTSI2SD;
                case "VCVTSI2SS": return Mnemonic.VCVTSI2SS;
                case "VCVTSS2SD": return Mnemonic.VCVTSS2SD;
                case "VCVTSS2SI": return Mnemonic.VCVTSS2SI;
                case "VCVTTPD2DQ": return Mnemonic.VCVTTPD2DQ;
                case "VCVTTPS2DQ": return Mnemonic.VCVTTPS2DQ;
                case "VCVTTSD2SI": return Mnemonic.VCVTTSD2SI;
                case "VCVTTSS2SI": return Mnemonic.VCVTTSS2SI;
                case "VDIVPD": return Mnemonic.VDIVPD;
                case "VDIVPS": return Mnemonic.VDIVPS;
                case "VDIVSD": return Mnemonic.VDIVSD;
                case "VDIVSS": return Mnemonic.VDIVSS;
                case "VDPPD": return Mnemonic.VDPPD;
                case "VDPPS": return Mnemonic.VDPPS;
                case "VEXTRACTF128": return Mnemonic.VEXTRACTF128;
                case "VEXTRACTPS": return Mnemonic.VEXTRACTPS;
                case "VHADDPD": return Mnemonic.VHADDPD;
                case "VHADDPS": return Mnemonic.VHADDPS;
                case "VHSUBPD": return Mnemonic.VHSUBPD;
                case "VHSUBPS": return Mnemonic.VHSUBPS;
                case "VINSERTF128": return Mnemonic.VINSERTF128;
                case "VINSERTPS": return Mnemonic.VINSERTPS;
                case "VLDDQU": return Mnemonic.VLDDQU;
                case "VLDQQU": return Mnemonic.VLDQQU;
                case "VLDMXCSR": return Mnemonic.VLDMXCSR;
                case "VMASKMOVDQU": return Mnemonic.VMASKMOVDQU;
                case "VMASKMOVPS": return Mnemonic.VMASKMOVPS;
                case "VMASKMOVPD": return Mnemonic.VMASKMOVPD;
                case "VMAXPD": return Mnemonic.VMAXPD;
                case "VMAXPS": return Mnemonic.VMAXPS;
                case "VMAXSD": return Mnemonic.VMAXSD;
                case "VMAXSS": return Mnemonic.VMAXSS;
                case "VMINPD": return Mnemonic.VMINPD;
                case "VMINPS": return Mnemonic.VMINPS;
                case "VMINSD": return Mnemonic.VMINSD;
                case "VMINSS": return Mnemonic.VMINSS;
                case "VMOVAPD": return Mnemonic.VMOVAPD;
                case "VMOVAPS": return Mnemonic.VMOVAPS;
                case "VMOVD": return Mnemonic.VMOVD;
                case "VMOVQ": return Mnemonic.VMOVQ;
                case "VMOVDDUP": return Mnemonic.VMOVDDUP;
                case "VMOVDQA": return Mnemonic.VMOVDQA;
                case "VMOVQQA": return Mnemonic.VMOVQQA;
                case "VMOVDQU": return Mnemonic.VMOVDQU;
                case "VMOVQQU": return Mnemonic.VMOVQQU;
                case "VMOVHLPS": return Mnemonic.VMOVHLPS;
                case "VMOVHPD": return Mnemonic.VMOVHPD;
                case "VMOVHPS": return Mnemonic.VMOVHPS;
                case "VMOVLHPS": return Mnemonic.VMOVLHPS;
                case "VMOVLPD": return Mnemonic.VMOVLPD;
                case "VMOVLPS": return Mnemonic.VMOVLPS;
                case "VMOVMSKPD": return Mnemonic.VMOVMSKPD;
                case "VMOVMSKPS": return Mnemonic.VMOVMSKPS;
                case "VMOVNTDQ": return Mnemonic.VMOVNTDQ;
                case "VMOVNTQQ": return Mnemonic.VMOVNTQQ;
                case "VMOVNTDQA": return Mnemonic.VMOVNTDQA;
                case "VMOVNTPD": return Mnemonic.VMOVNTPD;
                case "VMOVNTPS": return Mnemonic.VMOVNTPS;
                case "VMOVSD": return Mnemonic.VMOVSD;
                case "VMOVSHDUP": return Mnemonic.VMOVSHDUP;
                case "VMOVSLDUP": return Mnemonic.VMOVSLDUP;
                case "VMOVSS": return Mnemonic.VMOVSS;
                case "VMOVUPD": return Mnemonic.VMOVUPD;
                case "VMOVUPS": return Mnemonic.VMOVUPS;
                case "VMPSADBW": return Mnemonic.VMPSADBW;
                case "VMULPD": return Mnemonic.VMULPD;
                case "VMULPS": return Mnemonic.VMULPS;
                case "VMULSD": return Mnemonic.VMULSD;
                case "VMULSS": return Mnemonic.VMULSS;
                case "VORPD": return Mnemonic.VORPD;
                case "VORPS": return Mnemonic.VORPS;
                case "VPABSB": return Mnemonic.VPABSB;
                case "VPABSW": return Mnemonic.VPABSW;
                case "VPABSD": return Mnemonic.VPABSD;
                case "VPACKSSWB": return Mnemonic.VPACKSSWB;
                case "VPACKSSDW": return Mnemonic.VPACKSSDW;
                case "VPACKUSWB": return Mnemonic.VPACKUSWB;
                case "VPACKUSDW": return Mnemonic.VPACKUSDW;
                case "VPADDB": return Mnemonic.VPADDB;
                case "VPADDW": return Mnemonic.VPADDW;
                case "VPADDD": return Mnemonic.VPADDD;
                case "VPADDQ": return Mnemonic.VPADDQ;
                case "VPADDSB": return Mnemonic.VPADDSB;
                case "VPADDSW": return Mnemonic.VPADDSW;
                case "VPADDUSB": return Mnemonic.VPADDUSB;
                case "VPADDUSW": return Mnemonic.VPADDUSW;
                case "VPALIGNR": return Mnemonic.VPALIGNR;
                case "VPAND": return Mnemonic.VPAND;
                case "VPANDN": return Mnemonic.VPANDN;
                case "VPAVGB": return Mnemonic.VPAVGB;
                case "VPAVGW": return Mnemonic.VPAVGW;
                case "VPBLENDVB": return Mnemonic.VPBLENDVB;
                case "VPBLENDW": return Mnemonic.VPBLENDW;
                case "VPCMPESTRI": return Mnemonic.VPCMPESTRI;
                case "VPCMPESTRM": return Mnemonic.VPCMPESTRM;
                case "VPCMPISTRI": return Mnemonic.VPCMPISTRI;
                case "VPCMPISTRM": return Mnemonic.VPCMPISTRM;
                case "VPCMPEQB": return Mnemonic.VPCMPEQB;
                case "VPCMPEQW": return Mnemonic.VPCMPEQW;
                case "VPCMPEQD": return Mnemonic.VPCMPEQD;
                case "VPCMPEQQ": return Mnemonic.VPCMPEQQ;
                case "VPCMPGTB": return Mnemonic.VPCMPGTB;
                case "VPCMPGTW": return Mnemonic.VPCMPGTW;
                case "VPCMPGTD": return Mnemonic.VPCMPGTD;
                case "VPCMPGTQ": return Mnemonic.VPCMPGTQ;
                case "VPERMILPD": return Mnemonic.VPERMILPD;
                case "VPERMILPS": return Mnemonic.VPERMILPS;
                case "VPERM2F128": return Mnemonic.VPERM2F128;
                case "VPEXTRB": return Mnemonic.VPEXTRB;
                case "VPEXTRW": return Mnemonic.VPEXTRW;
                case "VPEXTRD": return Mnemonic.VPEXTRD;
                case "VPEXTRQ": return Mnemonic.VPEXTRQ;
                case "VPHADDW": return Mnemonic.VPHADDW;
                case "VPHADDD": return Mnemonic.VPHADDD;
                case "VPHADDSW": return Mnemonic.VPHADDSW;
                case "VPHMINPOSUW": return Mnemonic.VPHMINPOSUW;
                case "VPHSUBW": return Mnemonic.VPHSUBW;
                case "VPHSUBD": return Mnemonic.VPHSUBD;
                case "VPHSUBSW": return Mnemonic.VPHSUBSW;
                case "VPINSRB": return Mnemonic.VPINSRB;
                case "VPINSRW": return Mnemonic.VPINSRW;
                case "VPINSRD": return Mnemonic.VPINSRD;
                case "VPINSRQ": return Mnemonic.VPINSRQ;
                case "VPMADDWD": return Mnemonic.VPMADDWD;
                case "VPMADDUBSW": return Mnemonic.VPMADDUBSW;
                case "VPMAXSB": return Mnemonic.VPMAXSB;
                case "VPMAXSW": return Mnemonic.VPMAXSW;
                case "VPMAXSD": return Mnemonic.VPMAXSD;
                case "VPMAXUB": return Mnemonic.VPMAXUB;
                case "VPMAXUW": return Mnemonic.VPMAXUW;
                case "VPMAXUD": return Mnemonic.VPMAXUD;
                case "VPMINSB": return Mnemonic.VPMINSB;
                case "VPMINSW": return Mnemonic.VPMINSW;
                case "VPMINSD": return Mnemonic.VPMINSD;
                case "VPMINUB": return Mnemonic.VPMINUB;
                case "VPMINUW": return Mnemonic.VPMINUW;
                case "VPMINUD": return Mnemonic.VPMINUD;
                case "VPMOVMSKB": return Mnemonic.VPMOVMSKB;
                case "VPMOVSXBW": return Mnemonic.VPMOVSXBW;
                case "VPMOVSXBD": return Mnemonic.VPMOVSXBD;
                case "VPMOVSXBQ": return Mnemonic.VPMOVSXBQ;
                case "VPMOVSXWD": return Mnemonic.VPMOVSXWD;
                case "VPMOVSXWQ": return Mnemonic.VPMOVSXWQ;
                case "VPMOVSXDQ": return Mnemonic.VPMOVSXDQ;
                case "VPMOVZXBW": return Mnemonic.VPMOVZXBW;
                case "VPMOVZXBD": return Mnemonic.VPMOVZXBD;
                case "VPMOVZXBQ": return Mnemonic.VPMOVZXBQ;
                case "VPMOVZXWD": return Mnemonic.VPMOVZXWD;
                case "VPMOVZXWQ": return Mnemonic.VPMOVZXWQ;
                case "VPMOVZXDQ": return Mnemonic.VPMOVZXDQ;
                case "VPMULHUW": return Mnemonic.VPMULHUW;
                case "VPMULHRSW": return Mnemonic.VPMULHRSW;
                case "VPMULHW": return Mnemonic.VPMULHW;
                case "VPMULLW": return Mnemonic.VPMULLW;
                case "VPMULLD": return Mnemonic.VPMULLD;
                case "VPMULUDQ": return Mnemonic.VPMULUDQ;
                case "VPMULDQ": return Mnemonic.VPMULDQ;
                case "VPOR": return Mnemonic.VPOR;
                case "VPSADBW": return Mnemonic.VPSADBW;
                case "VPSHUFB": return Mnemonic.VPSHUFB;
                case "VPSHUFD": return Mnemonic.VPSHUFD;
                case "VPSHUFHW": return Mnemonic.VPSHUFHW;
                case "VPSHUFLW": return Mnemonic.VPSHUFLW;
                case "VPSIGNB": return Mnemonic.VPSIGNB;
                case "VPSIGNW": return Mnemonic.VPSIGNW;
                case "VPSIGND": return Mnemonic.VPSIGND;
                case "VPSLLDQ": return Mnemonic.VPSLLDQ;
                case "VPSRLDQ": return Mnemonic.VPSRLDQ;
                case "VPSLLW": return Mnemonic.VPSLLW;
                case "VPSLLD": return Mnemonic.VPSLLD;
                case "VPSLLQ": return Mnemonic.VPSLLQ;
                case "VPSRAW": return Mnemonic.VPSRAW;
                case "VPSRAD": return Mnemonic.VPSRAD;
                case "VPSRLW": return Mnemonic.VPSRLW;
                case "VPSRLD": return Mnemonic.VPSRLD;
                case "VPSRLQ": return Mnemonic.VPSRLQ;
                case "VPTEST": return Mnemonic.VPTEST;
                case "VPSUBB": return Mnemonic.VPSUBB;
                case "VPSUBW": return Mnemonic.VPSUBW;
                case "VPSUBD": return Mnemonic.VPSUBD;
                case "VPSUBQ": return Mnemonic.VPSUBQ;
                case "VPSUBSB": return Mnemonic.VPSUBSB;
                case "VPSUBSW": return Mnemonic.VPSUBSW;
                case "VPSUBUSB": return Mnemonic.VPSUBUSB;
                case "VPSUBUSW": return Mnemonic.VPSUBUSW;
                case "VPUNPCKHBW": return Mnemonic.VPUNPCKHBW;
                case "VPUNPCKHWD": return Mnemonic.VPUNPCKHWD;
                case "VPUNPCKHDQ": return Mnemonic.VPUNPCKHDQ;
                case "VPUNPCKHQDQ": return Mnemonic.VPUNPCKHQDQ;
                case "VPUNPCKLBW": return Mnemonic.VPUNPCKLBW;
                case "VPUNPCKLWD": return Mnemonic.VPUNPCKLWD;
                case "VPUNPCKLDQ": return Mnemonic.VPUNPCKLDQ;
                case "VPUNPCKLQDQ": return Mnemonic.VPUNPCKLQDQ;
                case "VPXOR": return Mnemonic.VPXOR;
                case "VRCPPS": return Mnemonic.VRCPPS;
                case "VRCPSS": return Mnemonic.VRCPSS;
                case "VRSQRTPS": return Mnemonic.VRSQRTPS;
                case "VRSQRTSS": return Mnemonic.VRSQRTSS;
                case "VROUNDPD": return Mnemonic.VROUNDPD;
                case "VROUNDPS": return Mnemonic.VROUNDPS;
                case "VROUNDSD": return Mnemonic.VROUNDSD;
                case "VROUNDSS": return Mnemonic.VROUNDSS;
                case "VSHUFPD": return Mnemonic.VSHUFPD;
                case "VSHUFPS": return Mnemonic.VSHUFPS;
                case "VSQRTPD": return Mnemonic.VSQRTPD;
                case "VSQRTPS": return Mnemonic.VSQRTPS;
                case "VSQRTSD": return Mnemonic.VSQRTSD;
                case "VSQRTSS": return Mnemonic.VSQRTSS;
                case "VSTMXCSR": return Mnemonic.VSTMXCSR;
                case "VSUBPD": return Mnemonic.VSUBPD;
                case "VSUBPS": return Mnemonic.VSUBPS;
                case "VSUBSD": return Mnemonic.VSUBSD;
                case "VSUBSS": return Mnemonic.VSUBSS;
                case "VTESTPS": return Mnemonic.VTESTPS;
                case "VTESTPD": return Mnemonic.VTESTPD;
                case "VUCOMISD": return Mnemonic.VUCOMISD;
                case "VUCOMISS": return Mnemonic.VUCOMISS;
                case "VUNPCKHPD": return Mnemonic.VUNPCKHPD;
                case "VUNPCKHPS": return Mnemonic.VUNPCKHPS;
                case "VUNPCKLPD": return Mnemonic.VUNPCKLPD;
                case "VUNPCKLPS": return Mnemonic.VUNPCKLPS;
                case "VXORPD": return Mnemonic.VXORPD;
                case "VXORPS": return Mnemonic.VXORPS;
                case "VZEROALL": return Mnemonic.VZEROALL;
                case "VZEROUPPER": return Mnemonic.VZEROUPPER;
                case "PCLMULLQLQDQ": return Mnemonic.PCLMULLQLQDQ;
                case "PCLMULHQLQDQ": return Mnemonic.PCLMULHQLQDQ;
                case "PCLMULLQHQDQ": return Mnemonic.PCLMULLQHQDQ;
                case "PCLMULHQHQDQ": return Mnemonic.PCLMULHQHQDQ;
                case "PCLMULQDQ": return Mnemonic.PCLMULQDQ;
                case "VPCLMULLQLQDQ": return Mnemonic.VPCLMULLQLQDQ;
                case "VPCLMULHQLQDQ": return Mnemonic.VPCLMULHQLQDQ;
                case "VPCLMULLQHQDQ": return Mnemonic.VPCLMULLQHQDQ;
                case "VPCLMULHQHQDQ": return Mnemonic.VPCLMULHQHQDQ;
                case "VPCLMULQDQ": return Mnemonic.VPCLMULQDQ;
                case "VFMADD132PS": return Mnemonic.VFMADD132PS;
                case "VFMADD132PD": return Mnemonic.VFMADD132PD;
                case "VFMADD312PS": return Mnemonic.VFMADD312PS;
                case "VFMADD312PD": return Mnemonic.VFMADD312PD;
                case "VFMADD213PS": return Mnemonic.VFMADD213PS;
                case "VFMADD213PD": return Mnemonic.VFMADD213PD;
                case "VFMADD123PS": return Mnemonic.VFMADD123PS;
                case "VFMADD123PD": return Mnemonic.VFMADD123PD;
                case "VFMADD231PS": return Mnemonic.VFMADD231PS;
                case "VFMADD231PD": return Mnemonic.VFMADD231PD;
                case "VFMADD321PS": return Mnemonic.VFMADD321PS;
                case "VFMADD321PD": return Mnemonic.VFMADD321PD;
                case "VFMADDSUB132PS": return Mnemonic.VFMADDSUB132PS;
                case "VFMADDSUB132PD": return Mnemonic.VFMADDSUB132PD;
                case "VFMADDSUB312PS": return Mnemonic.VFMADDSUB312PS;
                case "VFMADDSUB312PD": return Mnemonic.VFMADDSUB312PD;
                case "VFMADDSUB213PS": return Mnemonic.VFMADDSUB213PS;
                case "VFMADDSUB213PD": return Mnemonic.VFMADDSUB213PD;
                case "VFMADDSUB123PS": return Mnemonic.VFMADDSUB123PS;
                case "VFMADDSUB123PD": return Mnemonic.VFMADDSUB123PD;
                case "VFMADDSUB231PS": return Mnemonic.VFMADDSUB231PS;
                case "VFMADDSUB231PD": return Mnemonic.VFMADDSUB231PD;
                case "VFMADDSUB321PS": return Mnemonic.VFMADDSUB321PS;
                case "VFMADDSUB321PD": return Mnemonic.VFMADDSUB321PD;
                case "VFMSUB132PS": return Mnemonic.VFMSUB132PS;
                case "VFMSUB132PD": return Mnemonic.VFMSUB132PD;
                case "VFMSUB312PS": return Mnemonic.VFMSUB312PS;
                case "VFMSUB312PD": return Mnemonic.VFMSUB312PD;
                case "VFMSUB213PS": return Mnemonic.VFMSUB213PS;
                case "VFMSUB213PD": return Mnemonic.VFMSUB213PD;
                case "VFMSUB123PS": return Mnemonic.VFMSUB123PS;
                case "VFMSUB123PD": return Mnemonic.VFMSUB123PD;
                case "VFMSUB231PS": return Mnemonic.VFMSUB231PS;
                case "VFMSUB231PD": return Mnemonic.VFMSUB231PD;
                case "VFMSUB321PS": return Mnemonic.VFMSUB321PS;
                case "VFMSUB321PD": return Mnemonic.VFMSUB321PD;
                case "VFMSUBADD132PS": return Mnemonic.VFMSUBADD132PS;
                case "VFMSUBADD132PD": return Mnemonic.VFMSUBADD132PD;
                case "VFMSUBADD312PS": return Mnemonic.VFMSUBADD312PS;
                case "VFMSUBADD312PD": return Mnemonic.VFMSUBADD312PD;
                case "VFMSUBADD213PS": return Mnemonic.VFMSUBADD213PS;
                case "VFMSUBADD213PD": return Mnemonic.VFMSUBADD213PD;
                case "VFMSUBADD123PS": return Mnemonic.VFMSUBADD123PS;
                case "VFMSUBADD123PD": return Mnemonic.VFMSUBADD123PD;
                case "VFMSUBADD231PS": return Mnemonic.VFMSUBADD231PS;
                case "VFMSUBADD231PD": return Mnemonic.VFMSUBADD231PD;
                case "VFMSUBADD321PS": return Mnemonic.VFMSUBADD321PS;
                case "VFMSUBADD321PD": return Mnemonic.VFMSUBADD321PD;
                case "VFNMADD132PS": return Mnemonic.VFNMADD132PS;
                case "VFNMADD132PD": return Mnemonic.VFNMADD132PD;
                case "VFNMADD312PS": return Mnemonic.VFNMADD312PS;
                case "VFNMADD312PD": return Mnemonic.VFNMADD312PD;
                case "VFNMADD213PS": return Mnemonic.VFNMADD213PS;
                case "VFNMADD213PD": return Mnemonic.VFNMADD213PD;
                case "VFNMADD123PS": return Mnemonic.VFNMADD123PS;
                case "VFNMADD123PD": return Mnemonic.VFNMADD123PD;
                case "VFNMADD231PS": return Mnemonic.VFNMADD231PS;
                case "VFNMADD231PD": return Mnemonic.VFNMADD231PD;
                case "VFNMADD321PS": return Mnemonic.VFNMADD321PS;
                case "VFNMADD321PD": return Mnemonic.VFNMADD321PD;
                case "VFNMSUB132PS": return Mnemonic.VFNMSUB132PS;
                case "VFNMSUB132PD": return Mnemonic.VFNMSUB132PD;
                case "VFNMSUB312PS": return Mnemonic.VFNMSUB312PS;
                case "VFNMSUB312PD": return Mnemonic.VFNMSUB312PD;
                case "VFNMSUB213PS": return Mnemonic.VFNMSUB213PS;
                case "VFNMSUB213PD": return Mnemonic.VFNMSUB213PD;
                case "VFNMSUB123PS": return Mnemonic.VFNMSUB123PS;
                case "VFNMSUB123PD": return Mnemonic.VFNMSUB123PD;
                case "VFNMSUB231PS": return Mnemonic.VFNMSUB231PS;
                case "VFNMSUB231PD": return Mnemonic.VFNMSUB231PD;
                case "VFNMSUB321PS": return Mnemonic.VFNMSUB321PS;
                case "VFNMSUB321PD": return Mnemonic.VFNMSUB321PD;
                case "VFMADD132SS": return Mnemonic.VFMADD132SS;
                case "VFMADD132SD": return Mnemonic.VFMADD132SD;
                case "VFMADD312SS": return Mnemonic.VFMADD312SS;
                case "VFMADD312SD": return Mnemonic.VFMADD312SD;
                case "VFMADD213SS": return Mnemonic.VFMADD213SS;
                case "VFMADD213SD": return Mnemonic.VFMADD213SD;
                case "VFMADD123SS": return Mnemonic.VFMADD123SS;
                case "VFMADD123SD": return Mnemonic.VFMADD123SD;
                case "VFMADD231SS": return Mnemonic.VFMADD231SS;
                case "VFMADD231SD": return Mnemonic.VFMADD231SD;
                case "VFMADD321SS": return Mnemonic.VFMADD321SS;
                case "VFMADD321SD": return Mnemonic.VFMADD321SD;
                case "VFMSUB132SS": return Mnemonic.VFMSUB132SS;
                case "VFMSUB132SD": return Mnemonic.VFMSUB132SD;
                case "VFMSUB312SS": return Mnemonic.VFMSUB312SS;
                case "VFMSUB312SD": return Mnemonic.VFMSUB312SD;
                case "VFMSUB213SS": return Mnemonic.VFMSUB213SS;
                case "VFMSUB213SD": return Mnemonic.VFMSUB213SD;
                case "VFMSUB123SS": return Mnemonic.VFMSUB123SS;
                case "VFMSUB123SD": return Mnemonic.VFMSUB123SD;
                case "VFMSUB231SS": return Mnemonic.VFMSUB231SS;
                case "VFMSUB231SD": return Mnemonic.VFMSUB231SD;
                case "VFMSUB321SS": return Mnemonic.VFMSUB321SS;
                case "VFMSUB321SD": return Mnemonic.VFMSUB321SD;
                case "VFNMADD132SS": return Mnemonic.VFNMADD132SS;
                case "VFNMADD132SD": return Mnemonic.VFNMADD132SD;
                case "VFNMADD312SS": return Mnemonic.VFNMADD312SS;
                case "VFNMADD312SD": return Mnemonic.VFNMADD312SD;
                case "VFNMADD213SS": return Mnemonic.VFNMADD213SS;
                case "VFNMADD213SD": return Mnemonic.VFNMADD213SD;
                case "VFNMADD123SS": return Mnemonic.VFNMADD123SS;
                case "VFNMADD123SD": return Mnemonic.VFNMADD123SD;
                case "VFNMADD231SS": return Mnemonic.VFNMADD231SS;
                case "VFNMADD231SD": return Mnemonic.VFNMADD231SD;
                case "VFNMADD321SS": return Mnemonic.VFNMADD321SS;
                case "VFNMADD321SD": return Mnemonic.VFNMADD321SD;
                case "VFNMSUB132SS": return Mnemonic.VFNMSUB132SS;
                case "VFNMSUB132SD": return Mnemonic.VFNMSUB132SD;
                case "VFNMSUB312SS": return Mnemonic.VFNMSUB312SS;
                case "VFNMSUB312SD": return Mnemonic.VFNMSUB312SD;
                case "VFNMSUB213SS": return Mnemonic.VFNMSUB213SS;
                case "VFNMSUB213SD": return Mnemonic.VFNMSUB213SD;
                case "VFNMSUB123SS": return Mnemonic.VFNMSUB123SS;
                case "VFNMSUB123SD": return Mnemonic.VFNMSUB123SD;
                case "VFNMSUB231SS": return Mnemonic.VFNMSUB231SS;
                case "VFNMSUB231SD": return Mnemonic.VFNMSUB231SD;
                case "VFNMSUB321SS": return Mnemonic.VFNMSUB321SS;
                case "VFNMSUB321SD": return Mnemonic.VFNMSUB321SD;
                case "RDFSBASE": return Mnemonic.RDFSBASE;
                case "RDGSBASE": return Mnemonic.RDGSBASE;
                case "WRFSBASE": return Mnemonic.WRFSBASE;
                case "WRGSBASE": return Mnemonic.WRGSBASE;
                case "VCVTPH2PS": return Mnemonic.VCVTPH2PS;
                case "VCVTPS2PH": return Mnemonic.VCVTPS2PH;
                case "CLAC": return Mnemonic.CLAC;
                case "STAC": return Mnemonic.STAC;
                case "XSTORE": return Mnemonic.XSTORE;
                case "XCRYPTECB": return Mnemonic.XCRYPTECB;
                case "XCRYPTCBC": return Mnemonic.XCRYPTCBC;
                case "XCRYPTCTR": return Mnemonic.XCRYPTCTR;
                case "XCRYPTCFB": return Mnemonic.XCRYPTCFB;
                case "XCRYPTOFB": return Mnemonic.XCRYPTOFB;
                case "MONTMUL": return Mnemonic.MONTMUL;
                case "XSHA1": return Mnemonic.XSHA1;
                case "XSHA256": return Mnemonic.XSHA256;
                case "LLWPCB": return Mnemonic.LLWPCB;
                case "SLWPCB": return Mnemonic.SLWPCB;
                case "LWPVAL": return Mnemonic.LWPVAL;
                case "LWPINS": return Mnemonic.LWPINS;
                case "VFMADDPD": return Mnemonic.VFMADDPD;
                case "VFMADDPS": return Mnemonic.VFMADDPS;
                case "VFMADDSD": return Mnemonic.VFMADDSD;
                case "VFMADDSS": return Mnemonic.VFMADDSS;
                case "VFMADDSUBPD": return Mnemonic.VFMADDSUBPD;
                case "VFMADDSUBPS": return Mnemonic.VFMADDSUBPS;
                case "VFMSUBADDPD": return Mnemonic.VFMSUBADDPD;
                case "VFMSUBADDPS": return Mnemonic.VFMSUBADDPS;
                case "VFMSUBPD": return Mnemonic.VFMSUBPD;
                case "VFMSUBPS": return Mnemonic.VFMSUBPS;
                case "VFMSUBSD": return Mnemonic.VFMSUBSD;
                case "VFMSUBSS": return Mnemonic.VFMSUBSS;
                case "VFNMADDPD": return Mnemonic.VFNMADDPD;
                case "VFNMADDPS": return Mnemonic.VFNMADDPS;
                case "VFNMADDSD": return Mnemonic.VFNMADDSD;
                case "VFNMADDSS": return Mnemonic.VFNMADDSS;
                case "VFNMSUBPD": return Mnemonic.VFNMSUBPD;
                case "VFNMSUBPS": return Mnemonic.VFNMSUBPS;
                case "VFNMSUBSD": return Mnemonic.VFNMSUBSD;
                case "VFNMSUBSS": return Mnemonic.VFNMSUBSS;
                case "VFRCZPD": return Mnemonic.VFRCZPD;
                case "VFRCZPS": return Mnemonic.VFRCZPS;
                case "VFRCZSD": return Mnemonic.VFRCZSD;
                case "VFRCZSS": return Mnemonic.VFRCZSS;
                case "VPCMOV": return Mnemonic.VPCMOV;
                case "VPCOMB": return Mnemonic.VPCOMB;
                case "VPCOMD": return Mnemonic.VPCOMD;
                case "VPCOMQ": return Mnemonic.VPCOMQ;
                case "VPCOMUB": return Mnemonic.VPCOMUB;
                case "VPCOMUD": return Mnemonic.VPCOMUD;
                case "VPCOMUQ": return Mnemonic.VPCOMUQ;
                case "VPCOMUW": return Mnemonic.VPCOMUW;
                case "VPCOMW": return Mnemonic.VPCOMW;
                case "VPHADDBD": return Mnemonic.VPHADDBD;
                case "VPHADDBQ": return Mnemonic.VPHADDBQ;
                case "VPHADDBW": return Mnemonic.VPHADDBW;
                case "VPHADDDQ": return Mnemonic.VPHADDDQ;
                case "VPHADDUBD": return Mnemonic.VPHADDUBD;
                case "VPHADDUBQ": return Mnemonic.VPHADDUBQ;
                case "VPHADDUBW": return Mnemonic.VPHADDUBW;
                case "VPHADDUDQ": return Mnemonic.VPHADDUDQ;
                case "VPHADDUWD": return Mnemonic.VPHADDUWD;
                case "VPHADDUWQ": return Mnemonic.VPHADDUWQ;
                case "VPHADDWD": return Mnemonic.VPHADDWD;
                case "VPHADDWQ": return Mnemonic.VPHADDWQ;
                case "VPHSUBBW": return Mnemonic.VPHSUBBW;
                case "VPHSUBDQ": return Mnemonic.VPHSUBDQ;
                case "VPHSUBWD": return Mnemonic.VPHSUBWD;
                case "VPMACSDD": return Mnemonic.VPMACSDD;
                case "VPMACSDQH": return Mnemonic.VPMACSDQH;
                case "VPMACSDQL": return Mnemonic.VPMACSDQL;
                case "VPMACSSDD": return Mnemonic.VPMACSSDD;
                case "VPMACSSDQH": return Mnemonic.VPMACSSDQH;
                case "VPMACSSDQL": return Mnemonic.VPMACSSDQL;
                case "VPMACSSWD": return Mnemonic.VPMACSSWD;
                case "VPMACSSWW": return Mnemonic.VPMACSSWW;
                case "VPMACSWD": return Mnemonic.VPMACSWD;
                case "VPMACSWW": return Mnemonic.VPMACSWW;
                case "VPMADCSSWD": return Mnemonic.VPMADCSSWD;
                case "VPMADCSWD": return Mnemonic.VPMADCSWD;
                case "VPPERM": return Mnemonic.VPPERM;
                case "VPROTB": return Mnemonic.VPROTB;
                case "VPROTD": return Mnemonic.VPROTD;
                case "VPROTQ": return Mnemonic.VPROTQ;
                case "VPROTW": return Mnemonic.VPROTW;
                case "VPSHAB": return Mnemonic.VPSHAB;
                case "VPSHAD": return Mnemonic.VPSHAD;
                case "VPSHAQ": return Mnemonic.VPSHAQ;
                case "VPSHAW": return Mnemonic.VPSHAW;
                case "VPSHLB": return Mnemonic.VPSHLB;
                case "VPSHLD": return Mnemonic.VPSHLD;
                case "VPSHLDW": return Mnemonic.VPSHLDW;
                case "VPSHLDD": return Mnemonic.VPSHLDD;
                case "VPSHLDQ": return Mnemonic.VPSHLDQ;
                case "VPSHLQ": return Mnemonic.VPSHLQ;
                case "VPSHLW": return Mnemonic.VPSHLW;
                case "VBROADCASTI128": return Mnemonic.VBROADCASTI128;
                case "VPBLENDD": return Mnemonic.VPBLENDD;
                case "VPBROADCASTB": return Mnemonic.VPBROADCASTB;
                case "VPBROADCASTW": return Mnemonic.VPBROADCASTW;
                case "VPBROADCASTD": return Mnemonic.VPBROADCASTD;
                case "VPBROADCASTQ": return Mnemonic.VPBROADCASTQ;
                case "VPERMD": return Mnemonic.VPERMD;
                case "VPERMPD": return Mnemonic.VPERMPD;
                case "VPERMPS": return Mnemonic.VPERMPS;
                case "VPERMQ": return Mnemonic.VPERMQ;
                case "VPERM2I128": return Mnemonic.VPERM2I128;
                case "VEXTRACTI128": return Mnemonic.VEXTRACTI128;
                case "VINSERTI128": return Mnemonic.VINSERTI128;
                case "VPMASKMOVD": return Mnemonic.VPMASKMOVD;
                case "VPMASKMOVQ": return Mnemonic.VPMASKMOVQ;
                case "VPSLLVD": return Mnemonic.VPSLLVD;
                case "VPSLLVQ": return Mnemonic.VPSLLVQ;
                case "VPSRAVD": return Mnemonic.VPSRAVD;
                case "VPSRLVD": return Mnemonic.VPSRLVD;
                case "VPSRLVQ": return Mnemonic.VPSRLVQ;
                case "VGATHERDPD": return Mnemonic.VGATHERDPD;
                case "VGATHERQPD": return Mnemonic.VGATHERQPD;
                case "VGATHERDPS": return Mnemonic.VGATHERDPS;
                case "VGATHERQPS": return Mnemonic.VGATHERQPS;
                case "VPGATHERDD": return Mnemonic.VPGATHERDD;
                case "VPGATHERQD": return Mnemonic.VPGATHERQD;
                case "VPGATHERDQ": return Mnemonic.VPGATHERDQ;
                case "VPGATHERQQ": return Mnemonic.VPGATHERQQ;
                case "XABORT": return Mnemonic.XABORT;
                case "XBEGIN": return Mnemonic.XBEGIN;
                case "XEND": return Mnemonic.XEND;
                case "XTEST": return Mnemonic.XTEST;
                case "BLCI": return Mnemonic.BLCI;
                case "BLCIC": return Mnemonic.BLCIC;
                case "BLSIC": return Mnemonic.BLSIC;
                case "BLCFILL": return Mnemonic.BLCFILL;
                case "BLSFILL": return Mnemonic.BLSFILL;
                case "BLCMSK": return Mnemonic.BLCMSK;
                case "BLCS": return Mnemonic.BLCS;
                case "TZMSK": return Mnemonic.TZMSK;
                case "T1MSKC": return Mnemonic.T1MSKC;
                case "BNDMK": return Mnemonic.BNDMK;
                case "BNDCL": return Mnemonic.BNDCL;
                case "BNDCU": return Mnemonic.BNDCU;
                case "BNDCN": return Mnemonic.BNDCN;
                case "BNDMOV": return Mnemonic.BNDMOV;
                case "BNDLDX": return Mnemonic.BNDLDX;
                case "BNDSTX": return Mnemonic.BNDSTX;
                case "BND": return Mnemonic.BND;
                case "KADDB": return Mnemonic.KADDB;
                case "KADDD": return Mnemonic.KADDD;
                case "KADDQ": return Mnemonic.KADDQ;
                case "KADDW": return Mnemonic.KADDW;
                case "KANDB": return Mnemonic.KANDB;
                case "KANDD": return Mnemonic.KANDD;
                case "KANDNB": return Mnemonic.KANDNB;
                case "KANDND": return Mnemonic.KANDND;
                case "KANDNQ": return Mnemonic.KANDNQ;
                case "KANDNW": return Mnemonic.KANDNW;
                case "KANDQ": return Mnemonic.KANDQ;
                case "KANDW": return Mnemonic.KANDW;
                case "KMOVB": return Mnemonic.KMOVB;
                case "KMOVD": return Mnemonic.KMOVD;
                case "KMOVQ": return Mnemonic.KMOVQ;
                case "KMOVW": return Mnemonic.KMOVW;
                case "KNOTB": return Mnemonic.KNOTB;
                case "KNOTD": return Mnemonic.KNOTD;
                case "KNOTQ": return Mnemonic.KNOTQ;
                case "KNOTW": return Mnemonic.KNOTW;
                case "KORB": return Mnemonic.KORB;
                case "KORD": return Mnemonic.KORD;
                case "KORQ": return Mnemonic.KORQ;
                case "KORTESTB": return Mnemonic.KORTESTB;
                case "KORTESTD": return Mnemonic.KORTESTD;
                case "KORTESTQ": return Mnemonic.KORTESTQ;
                case "KORTESTW": return Mnemonic.KORTESTW;
                case "KORW": return Mnemonic.KORW;
                case "KSHIFTLB": return Mnemonic.KSHIFTLB;
                case "KSHIFTLD": return Mnemonic.KSHIFTLD;
                case "KSHIFTLQ": return Mnemonic.KSHIFTLQ;
                case "KSHIFTLW": return Mnemonic.KSHIFTLW;
                case "KSHIFTRB": return Mnemonic.KSHIFTRB;
                case "KSHIFTRD": return Mnemonic.KSHIFTRD;
                case "KSHIFTRQ": return Mnemonic.KSHIFTRQ;
                case "KSHIFTRW": return Mnemonic.KSHIFTRW;
                case "KTESTB": return Mnemonic.KTESTB;
                case "KTESTD": return Mnemonic.KTESTD;
                case "KTESTQ": return Mnemonic.KTESTQ;
                case "KTESTW": return Mnemonic.KTESTW;
                case "KUNPCKBW": return Mnemonic.KUNPCKBW;
                case "KUNPCKDQ": return Mnemonic.KUNPCKDQ;
                case "KUNPCKWD": return Mnemonic.KUNPCKWD;
                case "KXNORB": return Mnemonic.KXNORB;
                case "KXNORD": return Mnemonic.KXNORD;
                case "KXNORQ": return Mnemonic.KXNORQ;
                case "KXNORW": return Mnemonic.KXNORW;
                case "KXORB": return Mnemonic.KXORB;
                case "KXORD": return Mnemonic.KXORD;
                case "KXORQ": return Mnemonic.KXORQ;
                case "KXORW": return Mnemonic.KXORW;
                case "SHA1MSG1": return Mnemonic.SHA1MSG1;
                case "SHA1MSG2": return Mnemonic.SHA1MSG2;
                case "SHA1NEXTE": return Mnemonic.SHA1NEXTE;
                case "SHA1RNDS4": return Mnemonic.SHA1RNDS4;
                case "SHA256MSG1": return Mnemonic.SHA256MSG1;
                case "SHA256MSG2": return Mnemonic.SHA256MSG2;
                case "SHA256RNDS2": return Mnemonic.SHA256RNDS2;
                case "VALIGND": return Mnemonic.VALIGND;
                case "VALIGNQ": return Mnemonic.VALIGNQ;
                case "VBLENDMPD": return Mnemonic.VBLENDMPD;
                case "VBLENDMPS": return Mnemonic.VBLENDMPS;
                case "VBROADCASTF32X2": return Mnemonic.VBROADCASTF32X2;
                case "VBROADCASTF32X4": return Mnemonic.VBROADCASTF32X4;
                case "VBROADCASTF32X8": return Mnemonic.VBROADCASTF32X8;
                case "VBROADCASTF64X2": return Mnemonic.VBROADCASTF64X2;
                case "VBROADCASTF64X4": return Mnemonic.VBROADCASTF64X4;
                case "VBROADCASTI32X2": return Mnemonic.VBROADCASTI32X2;
                case "VBROADCASTI32X4": return Mnemonic.VBROADCASTI32X4;
                case "VBROADCASTI32X8": return Mnemonic.VBROADCASTI32X8;
                case "VBROADCASTI64X2": return Mnemonic.VBROADCASTI64X2;
                case "VBROADCASTI64X4": return Mnemonic.VBROADCASTI64X4;
                case "VCOMPRESSPD": return Mnemonic.VCOMPRESSPD;
                case "VCOMPRESSPS": return Mnemonic.VCOMPRESSPS;
                case "VCVTPD2QQ": return Mnemonic.VCVTPD2QQ;
                case "VCVTPD2UDQ": return Mnemonic.VCVTPD2UDQ;
                case "VCVTPD2UQQ": return Mnemonic.VCVTPD2UQQ;
                case "VCVTPS2QQ": return Mnemonic.VCVTPS2QQ;
                case "VCVTPS2UDQ": return Mnemonic.VCVTPS2UDQ;
                case "VCVTPS2UQQ": return Mnemonic.VCVTPS2UQQ;
                case "VCVTQQ2PD": return Mnemonic.VCVTQQ2PD;
                case "VCVTQQ2PS": return Mnemonic.VCVTQQ2PS;
                case "VCVTSD2USI": return Mnemonic.VCVTSD2USI;
                case "VCVTSS2USI": return Mnemonic.VCVTSS2USI;
                case "VCVTTPD2QQ": return Mnemonic.VCVTTPD2QQ;
                case "VCVTTPD2UDQ": return Mnemonic.VCVTTPD2UDQ;
                case "VCVTTPD2UQQ": return Mnemonic.VCVTTPD2UQQ;
                case "VCVTTPS2QQ": return Mnemonic.VCVTTPS2QQ;
                case "VCVTTPS2UDQ": return Mnemonic.VCVTTPS2UDQ;
                case "VCVTTPS2UQQ": return Mnemonic.VCVTTPS2UQQ;
                case "VCVTTSD2USI": return Mnemonic.VCVTTSD2USI;
                case "VCVTTSS2USI": return Mnemonic.VCVTTSS2USI;
                case "VCVTUDQ2PD": return Mnemonic.VCVTUDQ2PD;
                case "VCVTUDQ2PS": return Mnemonic.VCVTUDQ2PS;
                case "VCVTUQQ2PD": return Mnemonic.VCVTUQQ2PD;
                case "VCVTUQQ2PS": return Mnemonic.VCVTUQQ2PS;
                case "VCVTUSI2SD": return Mnemonic.VCVTUSI2SD;
                case "VCVTUSI2SS": return Mnemonic.VCVTUSI2SS;
                case "VDBPSADBW": return Mnemonic.VDBPSADBW;
                case "VEXP2PD": return Mnemonic.VEXP2PD;
                case "VEXP2PS": return Mnemonic.VEXP2PS;
                case "VEXPANDPD": return Mnemonic.VEXPANDPD;
                case "VEXPANDPS": return Mnemonic.VEXPANDPS;
                case "VEXTRACTF32X4": return Mnemonic.VEXTRACTF32X4;
                case "VEXTRACTF32X8": return Mnemonic.VEXTRACTF32X8;
                case "VEXTRACTF64X2": return Mnemonic.VEXTRACTF64X2;
                case "VEXTRACTF64X4": return Mnemonic.VEXTRACTF64X4;
                case "VEXTRACTI32X4": return Mnemonic.VEXTRACTI32X4;
                case "VEXTRACTI32X8": return Mnemonic.VEXTRACTI32X8;
                case "VEXTRACTI64X2": return Mnemonic.VEXTRACTI64X2;
                case "VEXTRACTI64X4": return Mnemonic.VEXTRACTI64X4;
                case "VFIXUPIMMPD": return Mnemonic.VFIXUPIMMPD;
                case "VFIXUPIMMPS": return Mnemonic.VFIXUPIMMPS;
                case "VFIXUPIMMSD": return Mnemonic.VFIXUPIMMSD;
                case "VFIXUPIMMSS": return Mnemonic.VFIXUPIMMSS;
                case "VFPCLASSPD": return Mnemonic.VFPCLASSPD;
                case "VFPCLASSPS": return Mnemonic.VFPCLASSPS;
                case "VFPCLASSSD": return Mnemonic.VFPCLASSSD;
                case "VFPCLASSSS": return Mnemonic.VFPCLASSSS;
                case "VGATHERPF0DPD": return Mnemonic.VGATHERPF0DPD;
                case "VGATHERPF0DPS": return Mnemonic.VGATHERPF0DPS;
                case "VGATHERPF0QPD": return Mnemonic.VGATHERPF0QPD;
                case "VGATHERPF0QPS": return Mnemonic.VGATHERPF0QPS;
                case "VGATHERPF1DPD": return Mnemonic.VGATHERPF1DPD;
                case "VGATHERPF1DPS": return Mnemonic.VGATHERPF1DPS;
                case "VGATHERPF1QPD": return Mnemonic.VGATHERPF1QPD;
                case "VGATHERPF1QPS": return Mnemonic.VGATHERPF1QPS;
                case "VGETEXPPD": return Mnemonic.VGETEXPPD;
                case "VGETEXPPS": return Mnemonic.VGETEXPPS;
                case "VGETEXPSD": return Mnemonic.VGETEXPSD;
                case "VGETEXPSS": return Mnemonic.VGETEXPSS;
                case "VGETMANTPD": return Mnemonic.VGETMANTPD;
                case "VGETMANTPS": return Mnemonic.VGETMANTPS;
                case "VGETMANTSD": return Mnemonic.VGETMANTSD;
                case "VGETMANTSS": return Mnemonic.VGETMANTSS;
                case "VINSERTF32X4": return Mnemonic.VINSERTF32X4;
                case "VINSERTF32X8": return Mnemonic.VINSERTF32X8;
                case "VINSERTF64X2": return Mnemonic.VINSERTF64X2;
                case "VINSERTF64X4": return Mnemonic.VINSERTF64X4;
                case "VINSERTI32X4": return Mnemonic.VINSERTI32X4;
                case "VINSERTI32X8": return Mnemonic.VINSERTI32X8;
                case "VINSERTI64X2": return Mnemonic.VINSERTI64X2;
                case "VINSERTI64X4": return Mnemonic.VINSERTI64X4;
                case "VMOVDQA32": return Mnemonic.VMOVDQA32;
                case "VMOVDQA64": return Mnemonic.VMOVDQA64;
                case "VMOVDQU16": return Mnemonic.VMOVDQU16;
                case "VMOVDQU32": return Mnemonic.VMOVDQU32;
                case "VMOVDQU64": return Mnemonic.VMOVDQU64;
                case "VMOVDQU8": return Mnemonic.VMOVDQU8;
                case "VPABSQ": return Mnemonic.VPABSQ;
                case "VPANDD": return Mnemonic.VPANDD;
                case "VPANDND": return Mnemonic.VPANDND;
                case "VPANDNQ": return Mnemonic.VPANDNQ;
                case "VPANDQ": return Mnemonic.VPANDQ;
                case "VPBLENDMB": return Mnemonic.VPBLENDMB;
                case "VPBLENDMD": return Mnemonic.VPBLENDMD;
                case "VPBLENDMQ": return Mnemonic.VPBLENDMQ;
                case "VPBLENDMW": return Mnemonic.VPBLENDMW;
                case "VPBROADCASTMB2Q": return Mnemonic.VPBROADCASTMB2Q;
                case "VPBROADCASTMW2D": return Mnemonic.VPBROADCASTMW2D;
                case "VPCMPB": return Mnemonic.VPCMPB;
                case "VPCMPD": return Mnemonic.VPCMPD;
                case "VPCMPQ": return Mnemonic.VPCMPQ;
                case "VPCMPUB": return Mnemonic.VPCMPUB;
                case "VPCMPUD": return Mnemonic.VPCMPUD;
                case "VPCMPUQ": return Mnemonic.VPCMPUQ;
                case "VPCMPUW": return Mnemonic.VPCMPUW;
                case "VPCMPW": return Mnemonic.VPCMPW;
                case "VPCOMPRESSD": return Mnemonic.VPCOMPRESSD;
                case "VPCOMPRESSQ": return Mnemonic.VPCOMPRESSQ;
                case "VPCONFLICTD": return Mnemonic.VPCONFLICTD;
                case "VPCONFLICTQ": return Mnemonic.VPCONFLICTQ;
                case "VPERMB": return Mnemonic.VPERMB;
                case "VPERMI2B": return Mnemonic.VPERMI2B;
                case "VPERMI2D": return Mnemonic.VPERMI2D;
                case "VPERMI2PD": return Mnemonic.VPERMI2PD;
                case "VPERMI2PS": return Mnemonic.VPERMI2PS;
                case "VPERMI2Q": return Mnemonic.VPERMI2Q;
                case "VPERMI2W": return Mnemonic.VPERMI2W;
                case "VPERMT2B": return Mnemonic.VPERMT2B;
                case "VPERMT2D": return Mnemonic.VPERMT2D;
                case "VPERMT2PD": return Mnemonic.VPERMT2PD;
                case "VPERMT2PS": return Mnemonic.VPERMT2PS;
                case "VPERMT2Q": return Mnemonic.VPERMT2Q;
                case "VPERMT2W": return Mnemonic.VPERMT2W;
                case "VPERMW": return Mnemonic.VPERMW;
                case "VPEXPANDD": return Mnemonic.VPEXPANDD;
                case "VPEXPANDQ": return Mnemonic.VPEXPANDQ;
                case "VPLZCNTD": return Mnemonic.VPLZCNTD;
                case "VPLZCNTQ": return Mnemonic.VPLZCNTQ;
                case "VPMADD52HUQ": return Mnemonic.VPMADD52HUQ;
                case "VPMADD52LUQ": return Mnemonic.VPMADD52LUQ;
                case "VPMAXSQ": return Mnemonic.VPMAXSQ;
                case "VPMAXUQ": return Mnemonic.VPMAXUQ;
                case "VPMINSQ": return Mnemonic.VPMINSQ;
                case "VPMINUQ": return Mnemonic.VPMINUQ;
                case "VPMOVB2M": return Mnemonic.VPMOVB2M;
                case "VPMOVD2M": return Mnemonic.VPMOVD2M;
                case "VPMOVDB": return Mnemonic.VPMOVDB;
                case "VPMOVDW": return Mnemonic.VPMOVDW;
                case "VPMOVM2B": return Mnemonic.VPMOVM2B;
                case "VPMOVM2D": return Mnemonic.VPMOVM2D;
                case "VPMOVM2Q": return Mnemonic.VPMOVM2Q;
                case "VPMOVM2W": return Mnemonic.VPMOVM2W;
                case "VPMOVQ2M": return Mnemonic.VPMOVQ2M;
                case "VPMOVQB": return Mnemonic.VPMOVQB;
                case "VPMOVQD": return Mnemonic.VPMOVQD;
                case "VPMOVQW": return Mnemonic.VPMOVQW;
                case "VPMOVSDB": return Mnemonic.VPMOVSDB;
                case "VPMOVSDW": return Mnemonic.VPMOVSDW;
                case "VPMOVSQB": return Mnemonic.VPMOVSQB;
                case "VPMOVSQD": return Mnemonic.VPMOVSQD;
                case "VPMOVSQW": return Mnemonic.VPMOVSQW;
                case "VPMOVSWB": return Mnemonic.VPMOVSWB;
                case "VPMOVUSDB": return Mnemonic.VPMOVUSDB;
                case "VPMOVUSDW": return Mnemonic.VPMOVUSDW;
                case "VPMOVUSQB": return Mnemonic.VPMOVUSQB;
                case "VPMOVUSQD": return Mnemonic.VPMOVUSQD;
                case "VPMOVUSQW": return Mnemonic.VPMOVUSQW;
                case "VPMOVUSWB": return Mnemonic.VPMOVUSWB;
                case "VPMOVW2M": return Mnemonic.VPMOVW2M;
                case "VPMOVWB": return Mnemonic.VPMOVWB;
                case "VPMULLQ": return Mnemonic.VPMULLQ;
                case "VPMULTISHIFTQB": return Mnemonic.VPMULTISHIFTQB;
                case "VPORD": return Mnemonic.VPORD;
                case "VPORQ": return Mnemonic.VPORQ;
                case "VPROLD": return Mnemonic.VPROLD;
                case "VPROLQ": return Mnemonic.VPROLQ;
                case "VPROLVD": return Mnemonic.VPROLVD;
                case "VPROLVQ": return Mnemonic.VPROLVQ;
                case "VPRORD": return Mnemonic.VPRORD;
                case "VPRORQ": return Mnemonic.VPRORQ;
                case "VPRORVD": return Mnemonic.VPRORVD;
                case "VPRORVQ": return Mnemonic.VPRORVQ;
                case "VPSCATTERDD": return Mnemonic.VPSCATTERDD;
                case "VPSCATTERDQ": return Mnemonic.VPSCATTERDQ;
                case "VPSCATTERQD": return Mnemonic.VPSCATTERQD;
                case "VPSCATTERQQ": return Mnemonic.VPSCATTERQQ;
                case "VPSLLVW": return Mnemonic.VPSLLVW;
                case "VPSRAQ": return Mnemonic.VPSRAQ;
                case "VPSRAVQ": return Mnemonic.VPSRAVQ;
                case "VPSRAVW": return Mnemonic.VPSRAVW;
                case "VPSRLVW": return Mnemonic.VPSRLVW;
                case "VPTERNLOGD": return Mnemonic.VPTERNLOGD;
                case "VPTERNLOGQ": return Mnemonic.VPTERNLOGQ;
                case "VPTESTMB": return Mnemonic.VPTESTMB;
                case "VPTESTMD": return Mnemonic.VPTESTMD;
                case "VPTESTMQ": return Mnemonic.VPTESTMQ;
                case "VPTESTMW": return Mnemonic.VPTESTMW;
                case "VPTESTNMB": return Mnemonic.VPTESTNMB;
                case "VPTESTNMD": return Mnemonic.VPTESTNMD;
                case "VPTESTNMQ": return Mnemonic.VPTESTNMQ;
                case "VPTESTNMW": return Mnemonic.VPTESTNMW;
                case "VPXORD": return Mnemonic.VPXORD;
                case "VPXORQ": return Mnemonic.VPXORQ;
                case "VRANGEPD": return Mnemonic.VRANGEPD;
                case "VRANGEPS": return Mnemonic.VRANGEPS;
                case "VRANGESD": return Mnemonic.VRANGESD;
                case "VRANGESS": return Mnemonic.VRANGESS;
                case "VRCP14PD": return Mnemonic.VRCP14PD;
                case "VRCP14PS": return Mnemonic.VRCP14PS;
                case "VRCP14SD": return Mnemonic.VRCP14SD;
                case "VRCP14SS": return Mnemonic.VRCP14SS;
                case "VRCP28PD": return Mnemonic.VRCP28PD;
                case "VRCP28PS": return Mnemonic.VRCP28PS;
                case "VRCP28SD": return Mnemonic.VRCP28SD;
                case "VRCP28SS": return Mnemonic.VRCP28SS;
                case "VREDUCEPD": return Mnemonic.VREDUCEPD;
                case "VREDUCEPS": return Mnemonic.VREDUCEPS;
                case "VREDUCESD": return Mnemonic.VREDUCESD;
                case "VREDUCESS": return Mnemonic.VREDUCESS;
                case "VRNDSCALEPD": return Mnemonic.VRNDSCALEPD;
                case "VRNDSCALEPS": return Mnemonic.VRNDSCALEPS;
                case "VRNDSCALESD": return Mnemonic.VRNDSCALESD;
                case "VRNDSCALESS": return Mnemonic.VRNDSCALESS;
                case "VRSQRT14PD": return Mnemonic.VRSQRT14PD;
                case "VRSQRT14PS": return Mnemonic.VRSQRT14PS;
                case "VRSQRT14SD": return Mnemonic.VRSQRT14SD;
                case "VRSQRT14SS": return Mnemonic.VRSQRT14SS;
                case "VRSQRT28PD": return Mnemonic.VRSQRT28PD;
                case "VRSQRT28PS": return Mnemonic.VRSQRT28PS;
                case "VRSQRT28SD": return Mnemonic.VRSQRT28SD;
                case "VRSQRT28SS": return Mnemonic.VRSQRT28SS;
                case "VSCALEFPD": return Mnemonic.VSCALEFPD;
                case "VSCALEFPS": return Mnemonic.VSCALEFPS;
                case "VSCALEFSD": return Mnemonic.VSCALEFSD;
                case "VSCALEFSS": return Mnemonic.VSCALEFSS;
                case "VSCATTERDPD": return Mnemonic.VSCATTERDPD;
                case "VSCATTERDPS": return Mnemonic.VSCATTERDPS;
                case "VSCATTERPF0DPD": return Mnemonic.VSCATTERPF0DPD;
                case "VSCATTERPF0DPS": return Mnemonic.VSCATTERPF0DPS;
                case "VSCATTERPF0QPD": return Mnemonic.VSCATTERPF0QPD;
                case "VSCATTERPF0QPS": return Mnemonic.VSCATTERPF0QPS;
                case "VSCATTERPF1DPD": return Mnemonic.VSCATTERPF1DPD;
                case "VSCATTERPF1DPS": return Mnemonic.VSCATTERPF1DPS;
                case "VSCATTERPF1QPD": return Mnemonic.VSCATTERPF1QPD;
                case "VSCATTERPF1QPS": return Mnemonic.VSCATTERPF1QPS;
                case "VSCATTERQPD": return Mnemonic.VSCATTERQPD;
                case "VSCATTERQPS": return Mnemonic.VSCATTERQPS;
                case "VSHUFF32X4": return Mnemonic.VSHUFF32X4;
                case "VSHUFF64X2": return Mnemonic.VSHUFF64X2;
                case "VSHUFI32X4": return Mnemonic.VSHUFI32X4;
                case "VSHUFI64X2": return Mnemonic.VSHUFI64X2;
                case "RDPKRU": return Mnemonic.RDPKRU;
                case "WRPKRU": return Mnemonic.WRPKRU;
                case "CLZERO": return Mnemonic.CLZERO;

                case "XRELEASE": return Mnemonic.XRELEASE;
                case "XACQUIRE": return Mnemonic.XACQUIRE;
                case "WAIT": return Mnemonic.WAIT;
                case "LOCK": return Mnemonic.LOCK;

                case "PABSQ": return Mnemonic.PABSQ;
                case "PMAXSQ": return Mnemonic.PMAXSQ;
                case "PMAXUQ": return Mnemonic.PMAXUQ;
                case "PMINSQ": return Mnemonic.PMINSQ;
                case "PMINUQ": return Mnemonic.PMINUQ;
                case "PMULLQ": return Mnemonic.PMULLQ;
                case "PROLVD": return Mnemonic.PROLVD;
                case "PROLVQ": return Mnemonic.PROLVQ;
                case "PROLD": return Mnemonic.PROLD;
                case "PROLQ": return Mnemonic.PROLQ;
                case "PRORQ": return Mnemonic.PRORQ;
                case "PRORD": return Mnemonic.PRORD;
                case "PRORVQ": return Mnemonic.PRORVQ;
                case "PRORVD": return Mnemonic.PRORVD;
                case "PSRAQ": return Mnemonic.PSRAQ;
                case "PTWRITE": return Mnemonic.PTWRITE;
                case "RDPID": return Mnemonic.RDPID;

                case "PMULHRW": return Mnemonic.PMULHRW;
                case "RDSHR": return Mnemonic.RDSHR;
                case "PPMULHRWA": return Mnemonic.PPMULHRWA;

                case "VP4DPWSSD": return Mnemonic.VP4DPWSSD;
                case "VP4DPWSSDS": return Mnemonic.VP4DPWSSDS;
                case "V4FMADDPS": return Mnemonic.V4FMADDPS;
                case "V4FNMADDPS": return Mnemonic.V4FNMADDPS;
                case "V4FMADDSS": return Mnemonic.V4FMADDSS;
                case "V4FNMADDSS": return Mnemonic.V4FNMADDSS;

                case "VPOPCNTB": return Mnemonic.VPOPCNTB;
                case "VPOPCNTW": return Mnemonic.VPOPCNTW;
                case "VPOPCNTD": return Mnemonic.VPOPCNTD;
                case "VPOPCNTQ": return Mnemonic.VPOPCNTQ;

                case "GF2P8AFFINEINVQB": return Mnemonic.GF2P8AFFINEINVQB;
                case "VGF2P8AFFINEINVQB": return Mnemonic.VGF2P8AFFINEINVQB;
                case "GF2P8AFFINEQB": return Mnemonic.GF2P8AFFINEQB;
                case "VGF2P8AFFINEQB": return Mnemonic.VGF2P8AFFINEQB;
                case "GF2P8MULB": return Mnemonic.GF2P8MULB;
                case "VGF2P8MULB": return Mnemonic.VGF2P8MULB;
                case "VPCOMPRESSB": return Mnemonic.VPCOMPRESSB;
                case "VPCOMPRESSW": return Mnemonic.VPCOMPRESSW;
                case "VPDPBUSD": return Mnemonic.VPDPBUSD;
                case "VPDPBUSDS": return Mnemonic.VPDPBUSDS;
                case "VPDPWSSD": return Mnemonic.VPDPWSSD;
                case "VPDPWSSDS": return Mnemonic.VPDPWSSDS;
                case "VPEXPANDB": return Mnemonic.VPEXPANDB;
                case "VPEXPANDW": return Mnemonic.VPEXPANDW;
                case "VPSHLDVW": return Mnemonic.VPSHLDVW;
                case "VPSHLDVD": return Mnemonic.VPSHLDVD;
                case "VPSHLDVQ": return Mnemonic.VPSHLDVQ;
                case "VPSHRDW": return Mnemonic.VPSHRDW;
                case "VPSHRDD": return Mnemonic.VPSHRDD;
                case "VPSHRDQ": return Mnemonic.VPSHRDQ;
                case "VPSHRDVW": return Mnemonic.VPSHRDVW;
                case "VPSHRDVD": return Mnemonic.VPSHRDVD;
                case "VPSHRDVQ": return Mnemonic.VPSHRDVQ;
                case "VPSHUFBITQMB": return Mnemonic.VPSHUFBITQMB;

                case "ENCLS": return Mnemonic.ENCLS;
                case "ENCLU": return Mnemonic.ENCLU;
                case "ENCLV": return Mnemonic.ENCLV;
                case "EADD": return Mnemonic.EADD;
                case "EAUG": return Mnemonic.EAUG;
                case "EBLOCK": return Mnemonic.EBLOCK;
                case "ECREATE": return Mnemonic.ECREATE;
                case "EDBGRD": return Mnemonic.EDBGRD;
                case "EDBGWR": return Mnemonic.EDBGWR;
                case "EEXTEND": return Mnemonic.EEXTEND;
                case "EINIT": return Mnemonic.EINIT;
                case "ELDB": return Mnemonic.ELDB;
                case "ELDU": return Mnemonic.ELDU;
                case "ELDBC": return Mnemonic.ELDBC;
                case "ELBUC": return Mnemonic.ELBUC;
                case "EMODPR": return Mnemonic.EMODPR;
                case "EMODT": return Mnemonic.EMODT;
                case "EPA": return Mnemonic.EPA;
                case "ERDINFO": return Mnemonic.ERDINFO;
                case "EREMOVE": return Mnemonic.EREMOVE;
                case "ETRACK": return Mnemonic.ETRACK;
                case "ETRACKC": return Mnemonic.ETRACKC;
                case "EWB": return Mnemonic.EWB;
                case "EACCEPT": return Mnemonic.EACCEPT;
                case "EACCEPTCOPY": return Mnemonic.EACCEPTCOPY;
                case "EENTER": return Mnemonic.EENTER;
                case "EEXIT": return Mnemonic.EEXIT;
                case "EGETKEY": return Mnemonic.EGETKEY;
                case "EMODPE": return Mnemonic.EMODPE;
                case "EREPORT": return Mnemonic.EREPORT;
                case "ERESUME": return Mnemonic.ERESUME;
                case "EDECVIRTCHILD": return Mnemonic.EDECVIRTCHILD;
                case "EINCVIRTCHILD": return Mnemonic.EINCVIRTCHILD;
                case "ESETCONTEXT": return Mnemonic.ESETCONTEXT;

                case "EXITAC": return Mnemonic.EXITAC;
                case "PARAMETERS": return Mnemonic.PARAMETERS;
                case "SENTER": return Mnemonic.SENTER;
                case "SEXIT": return Mnemonic.SEXIT;
                case "SMCTRL": return Mnemonic.SMCTRL;
                case "WAKEUP": return Mnemonic.WAKEUP;

                case "CLDEMOTE": return Mnemonic.CLDEMOTE;
                case "MOVDIR64B": return Mnemonic.MOVDIR64B;
                case "MOVDIRI": return Mnemonic.MOVDIRI;
                case "PCONFIG": return Mnemonic.PCONFIG;
                case "TPAUSE": return Mnemonic.TPAUSE;
                case "UMONITOR": return Mnemonic.UMONITOR;
                case "UMWAIT": return Mnemonic.UMWAIT;
                case "WBNOINVD": return Mnemonic.WBNOINVD;

                case "ENQCMD": return Mnemonic.ENQCMD;
                case "ENQCMDS": return Mnemonic.ENQCMDS;

                case "VCVTNE2PS2BF16": return Mnemonic.VCVTNE2PS2BF16;
                case "VCVTNEPS2BF16": return Mnemonic.VCVTNEPS2BF16;
                case "VDPBF16PS": return Mnemonic.VDPBF16PS;

                case "VP2INTERSECTD": return Mnemonic.VP2INTERSECTD;
                case "VP2INTERSECTQ": return Mnemonic.VP2INTERSECTQ;

                default:
                    Console.WriteLine("WARNING;parseMnemonic. unknown str=\"" + str + "\".");
                    return Mnemonic.NONE;
            }
        }

        public static bool IsMnemonic(string keyword, bool strIsCapitals)
        {
            return Mnemonic_cache_.ContainsKey(ToCapitals(keyword, strIsCapitals));
        }

        public static bool IsMnemonic_Att(string keyword, bool strIsCapitals = false)
        {
            Contract.Requires(keyword != null);
            Contract.Assume(keyword != null);

            int length = keyword.Length;
            if (length < 2)
            {
                return false;
            }

            string str2 = ToCapitals(keyword, strIsCapitals);
            if (IsMnemonic(str2, true))
            {
                return true;
            }

            if (!IsAttType(str2[length - 1]))
            {
                return false;
            }

            return IsMnemonic(str2.Substring(0, length - 1), true);
        }

        /// <summary>Simple speed test to compare parsing of mnemonics vs a lookup map</summary>
        public static void SpeedTestMnemonicParsing()
        {
            Stopwatch stopwatch1 = new Stopwatch();
            Stopwatch stopwatch2 = new Stopwatch();
            bool strCapitals = true;

            for (int i = 0; i < 1000; ++i)
            {
                foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
                {
                    string str = mnemonic.ToString();
                    {
                        stopwatch1.Start();
                        Mnemonic m1 = ParseMnemonic_OLD(str, strCapitals);
                        stopwatch1.Stop();
                        if (m1 != mnemonic)
                        {
                            Console.WriteLine("NOT OK OLD mnemonic=" + mnemonic.ToString() + "; str=" + str + "; m1=" + m1.ToString());
                        }
                    }
                    {
                        stopwatch2.Start();
                        Mnemonic m1 = ParseMnemonic(str, strCapitals);
                        stopwatch2.Stop();
                        if (m1 != mnemonic)
                        {
                            Console.WriteLine("NOT OK     mnemonic=" + mnemonic.ToString() + "; str=" + str + "; m1=" + m1.ToString());
                        }
                    }
                }
            }
            Console.WriteLine("ParseMnemonic OLD " + stopwatch1.ElapsedMilliseconds + " ms");
            Console.WriteLine("ParseMnemonic     " + stopwatch2.ElapsedMilliseconds + " ms");
        }

        /// <summary>Simple speed test to compare parsing of registers vs a lookup map</summary>
        public static void SpeedTestRegisterParsing()
        {
            Stopwatch stopwatch1 = new Stopwatch();
            Stopwatch stopwatch2 = new Stopwatch();
            bool strCapitals = true;

            for (int i = 0; i < 1000; ++i)
            {
                foreach (Rn mnemonic in Enum.GetValues(typeof(Rn)))
                {
                    string str = mnemonic.ToString();
                    {
                        stopwatch1.Start();
                        Rn m1 = RegisterTools.ParseRn_OLD(str, strCapitals);
                        stopwatch1.Stop();
                        if (m1 != mnemonic)
                        {
                            Console.WriteLine("NOT OK OLD rn=" + mnemonic.ToString() + "; str=" + str + "; m1=" + m1.ToString());
                        }
                    }
                    {
                        stopwatch2.Start();
                        Rn m1 = RegisterTools.ParseRn(str, strCapitals);
                        stopwatch2.Stop();
                        if (m1 != mnemonic)
                        {
                            Console.WriteLine("NOT OK     rn=" + mnemonic.ToString() + "; str=" + str + "; m1=" + m1.ToString());
                        }
                    }
                }
            }
            Console.WriteLine("ParseRn OLD " + stopwatch1.ElapsedMilliseconds + " ms");
            Console.WriteLine("ParseRn     " + stopwatch2.ElapsedMilliseconds + " ms");
        }
    }
}
