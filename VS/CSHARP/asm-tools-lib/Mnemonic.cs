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
    }
}
