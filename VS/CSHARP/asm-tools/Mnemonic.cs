using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {

    public enum ConditionalElement {

        UNCONDITIONAL,

        /// <summary>if above (CF = 0 and ZF = 0></summary>
        A,
        /// <summary>if above or equal (CF = 0)</summary>
        AE,
        /// <summary>Set byte if below (CF = 1)</summary>
        B,
        /// <summary>Set byte if below or equal (CF = 1 or ZF = 1)</summary>
        BE,
        /// <summary>Set byte if carry (CF = 1)</summary>
        C,
        /// <summary>Set byte if equal (ZF = 1)</summary>
        E,
        /// <summary>Set byte if greater (ZF = 0 and SF = OF)</summary>
        G,
        /// <summary>Set byte if greater or equal (SF = OF)</summary>
        GE,
        /// <summary>Set byte if less (SF ≠ OF)</summary>
        L,
        /// <summary>Set byte if less or equal (ZF = 1 or SF≠ OF)/summary>
        LE,
        /// <summary>Set byte if not above (CF = 1 or ZF = 1)</summary>
        NA,
        /// <summary>Set byte if not above or equal (CF = 1)</summary>
        NAE,
        /// <summary>Set byte if not below (CF = 0)</summary>
        NB,
        /// <summary>Set byte if not below or equal (CF = 0 and ZF = 0)</summary>
        NBE,
        /// <summary>Set byte if not carry (CF = 0)</summary>
        NC,
        /// <summary>Set byte if not equal (ZF = 0)</summary>
        NE,
        /// <summary>Set byte if not greater (ZF = 1 or SF ≠ OF)</summary>
        NG,
        /// <summary>Set byte if not greater or equal (SF ≠ OF)</summary>
        NGE,
        /// <summary>Set byte if not less (SF = OF)</summary>
        NL,
        /// <summary>Set byte if not less or equal (ZF = 0 and SF = OF)</summary>
        NLE,
        /// <summary>Set byte if not overflow (OF = 0)</summary>
        NO,
        /// <summary>Set byte if not parity (PF = 0)</summary>
        NP,
        /// <summary>Set byte if not sign (SF = 0)</summary>
        NS,
        /// <summary>Set byte if not zero (ZF = 0)</summary>
        NZ,
        /// <summary>Set byte if overflow (OF = 1)</summary>
        O,
        /// <summary>Set byte if parity (PF = 1)</summary>
        P,
        /// <summary>Set byte if parity even (PF = 1)</summary>
        PE,
        /// <summary>Set byte if parity odd (PF = 0)</summary>
        PO,
        /// <summary>Set byte if sign (SF = 1)</summary>
        S,
        /// <summary>Set byte if zero (ZF = 1)</summary>
        Z
    }

    public enum Mnemonic {
        UNKNOWN,
        #region Data Transfer Instructions
        //The data transfer instructions move data between memory and the general-purpose and segment registers. They
        //also perform specific operations such as conditional moves, stack access, and data conversion.

        /// <summary>
        /// Move data between general-purpose registers; move data between memory and general purpose or segment registers; move immediates to general-purpose registers
        /// </summary>
        MOV,
        CMOVE,
        CMOVZ,// Conditional move if equal/Conditional move if zero
        CMOVNE,
        CMOVNZ,// Conditional move if not equal/Conditional move if not zero
        CMOVA,
        CMOVNBE,// Conditional move if above/Conditional move if not below or equal
        CMOVAE,
        CMOVNB,// Conditional move if above or equal/Conditional move if not below
        CMOVB,
        CMOVNAE,// Conditional move if below/Conditional move if not above or equal
        CMOVBE,
        CMOVNA,// Conditional move if below or equal/Conditional move if not above
        CMOVG,
        CMOVNLE,// Conditional move if greater/Conditional move if not less or equal
        CMOVGE,
        CMOVNL,// Conditional move if greater or equal/Conditional move if not less
        CMOVL,
        CMOVNGE,// Conditional move if less/Conditional move if not greater or equal
        CMOVLE,
        CMOVNG,// Conditional move if less or equal/Conditional move if not greater
        CMOVC,// Conditional move if carry
        CMOVNC,// Conditional move if not carry
        CMOVO,// Conditional move if overflow
        CMOVNO,// Conditional move if not overflow
        CMOVS,// Conditional move if sign(negative)
        CMOVNS,// Conditional move if not sign(non-negative)
        CMOVP,
        CMOVPE,// Conditional move if parity/Conditional move if parity even
        CMOVNP,
        CMOVPO,// Conditional move if not parity/Conditional move if parity odd
        XCHG,// Exchange
        BSWAP,// Byte swap
        XADD,// Exchange and add
        CMPXCHG,// Compare and exchange
        CMPXCHG8B,// Compare and exchange 8 bytes
        PUSH,// Push onto stack
        POP,// Pop off of stack
        PUSHA,
        PUSHAD,// Push general-purpose registers onto stack
        POPA,
        POPAD,// Pop general-purpose registers from stack
        CWD,
        CDQ,// Convert word to doubleword/Convert doubleword to quadword
        CBW,
        CWDE,// Convert byte to word/Convert word to doubleword in EAX register
        MOVSX,// Move and sign extend
        MOVSXD,
        MOVZX,// Move and zero extend
        MOVZXD,
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
        //The decimal arithmetic instructions perform decimal arithmetic on binary coded decimal (BCD) data.

        /// <summary>XXX</summary>
        DAA,// Decimal adjust after addition
        /// <summary>XXX</summary>
        DAS,// Decimal adjust after subtraction
        /// <summary>XXX</summary>
        AAA,// ASCII adjust after addition
        /// <summary>XXX</summary>
        AAS,// ASCII adjust after subtraction
        /// <summary>XXX</summary>
        AAM,// ASCII adjust after multiplication
        /// <summary>XXX</summary>
        AAD,// ASCII adjust before division
        #endregion
        #region Logical Instructions
        //The logical instructions perform basic AND, OR, XOR, and NOT logical operations on byte, word, and doubleword
        //values.
        /// <summary>XXX</summary>
        AND,// Perform bitwise logical AND
        /// <summary>XXX</summary>
        OR,// Perform bitwise logical OR
        /// <summary>XXX</summary>
        XOR,// Perform bitwise logical exclusive OR
        /// <summary>XXX</summary>
        NOT,// Perform bitwise logical NOT
        #endregion
        #region Shift and Rotate Instructions
        //The shift and rotate instructions shift and rotate the bits in word and doubleword operands.
        /// <summary>XXX</summary>
        SAR,// Shift arithmetic right
        /// <summary>XXX</summary>
        SHR,// Shift logical right
        /// <summary>XXX</summary>
        SAL,
        /// <summary>XXX</summary>
        SHL,// Shift arithmetic left/Shift logical left
        /// <summary>XXX</summary>
        SHRD,// Shift right double
        /// <summary>XXX</summary>
        SHLD,//Shift left double
        /// <summary>XXX</summary>
        ROR,// Rotate right
        /// <summary>XXX</summary>
        ROL,// Rotate left
        /// <summary>XXX</summary>
        RCR,// Rotate through carry right
        /// <summary>XXX</summary>
        RCL,// Rotate through carry left
        #endregion
        #region Bit and Byte Instructions
        //Bit instructions test and modify individual bits in word and doubleword operands. Byte instructions set the value of
        //a byte operand to indicate the status of flags in the EFLAGS register.
        /// <summary>XXX</summary>
        BT,// Bit test
        /// <summary>XXX</summary>
        BTS,// Bit test and set
        /// <summary>XXX</summary>
        BTR,// Bit test and reset
        /// <summary>XXX</summary>
        BTC,// Bit test and complement
        /// <summary>XXX</summary>
        BSF,// Bit scan forward
        /// <summary>XXX</summary>
        BSR,// Bit scan reverse
        /// <summary>XXX</summary>
        SETE,
        /// <summary>XXX</summary>
        SETZ,// Set byte if equal/Set byte if zero
        /// <summary>XXX</summary>
        SETNE,
        /// <summary>XXX</summary>
        SETNZ,// Set byte if not equal/Set byte if not zero
        /// <summary>XXX</summary>
        SETA,
        /// <summary>XXX</summary>
        SETNBE,// Set byte if above/Set byte if not below or equal
        /// <summary>XXX</summary>
        SETAE,
        /// <summary>XXX</summary>
        SETNB,
        /// <summary>XXX</summary>
        SETNC,// Set byte if above or equal/Set byte if not below/Set byte if not carry
        /// <summary>XXX</summary>
        SETB,
        /// <summary>XXX</summary>
        SETNAE,
        /// <summary>XXX</summary>
        SETC,//Set byte if below/Set byte if not above or equal/Set byte if carry
        /// <summary>XXX</summary>
        SETBE,
        /// <summary>XXX</summary>
        SETNA,// Set byte if below or equal/Set byte if not above
        /// <summary>XXX</summary>
        SETG,
        /// <summary>XXX</summary>
        SETNLE,// Set byte if greater/Set byte if not less or equal
        /// <summary>XXX</summary>
        SETGE,
        /// <summary>XXX</summary>
        SETNL,// Set byte if greater or equal/Set byte if not less
        /// <summary>XXX</summary>
        SETL,
        /// <summary>XXX</summary>
        SETNGE,// Set byte if less/Set byte if not greater or equal
        /// <summary>XXX</summary>
        SETLE,
        /// <summary>XXX</summary>
        SETNG,// Set byte if less or equal/Set byte if not greater
        /// <summary>XXX</summary>
        SETS,// Set byte if sign (negative)
        /// <summary>XXX</summary>
        SETNS,// Set byte if not sign (non-negative)
        /// <summary>XXX</summary>
        SETO,// Set byte if overflow
        /// <summary>XXX</summary>
        SETNO,// Set byte if not overflow
        /// <summary>XXX</summary>
        SETPE,
        /// <summary>XXX</summary>
        SETP,// Set byte if parity even/Set byte if parity
        /// <summary>XXX</summary>
        SETPO,
        /// <summary>XXX</summary>
        SETNP,// Set byte if parity odd/Set byte if not parity
        /// <summary>XXX</summary>
        TEST,// Logical compare
        /// <summary>XXX</summary>
        CRC32,// Provides hardware acceleration to calculate cyclic redundancy checks for fast and efficient implementation of data integrity protocols.
        /// <summary>XXX</summary>
        POPCNT,// This instruction calculates of number of bits set to 1 in the second operand (source) and returns the count in the first operand (a destination register)
        #endregion
        #region Control Transfer Instructions
        // The control transfer instructions provide jump, conditional jump, loop, and call and return operations to control
        //program flow.
        /// <summary>XXX</summary>
        JMP,// Jump
        /// <summary>XXX</summary>
        JE,
        /// <summary>XXX</summary>
        JZ,// Jump if equal/Jump if zero
        /// <summary>XXX</summary>
        JNE,
        /// <summary>XXX</summary>
        JNZ,// Jump if not equal/Jump if not zero
        /// <summary>XXX</summary>
        JA,
        /// <summary>XXX</summary>
        JNBE,// Jump if above/Jump if not below or equal
        /// <summary>XXX</summary>
        JAE,
        /// <summary>XXX</summary>
        JNB,// Jump if above or equal/Jump if not below
        /// <summary>XXX</summary>
        JB,
        /// <summary>XXX</summary>
        JNAE,// Jump if below/Jump if not above or equal
        /// <summary>XXX</summary>
        JBE,
        /// <summary>XXX</summary>
        JNA,// Jump if below or equal/Jump if not above
        /// <summary>XXX</summary>
        JG,
        /// <summary>XXX</summary>
        JNLE,// Jump if greater/Jump if not less or equal
        /// <summary>XXX</summary>
        JGE,
        /// <summary>XXX</summary>
        JNL,// Jump if greater or equal/Jump if not less
        /// <summary>XXX</summary>
        JL,
        /// <summary>XXX</summary>
        JNGE,// Jump if less/Jump if not greater or equal
        /// <summary>XXX</summary>
        JLE,
        /// <summary>XXX</summary>
        JNG,// Jump if less or equal/Jump if not greater
        /// <summary>XXX</summary>
        JC,// Jump if carry
        /// <summary>XXX</summary>
        JNC,// Jump if not carry
        /// <summary>XXX</summary>
        JO,// Jump if overflow
        /// <summary>XXX</summary>
        JNO,// Jump if not overflow
        /// <summary>XXX</summary>
        JS,// Jump if sign (negative)
        /// <summary>XXX</summary>
        JNS,// Jump if not sign (non-negative)
        /// <summary>XXX</summary>
        JPO,
        /// <summary>XXX</summary>
        JNP,// Jump if parity odd/Jump if not parity
        /// <summary>XXX</summary>
        JPE,
        /// <summary>XXX</summary>
        JP,// Jump if parity even/Jump if parity
        /// <summary>XXX</summary>
        JCXZ,
        /// <summary>XXX</summary>
        JECXZ,// Jump register CX zero/Jump register ECX zero
        /// <summary>XXX</summary>
        JRCXZ,
        /// <summary>XXX</summary>
        LOOP,// Loop with ECX counter
        /// <summary>XXX</summary>
        LOOPZ,
        /// <summary>XXX</summary>
        LOOPE,// Loop with ECX and zero/Loop with ECX and equal
        /// <summary>XXX</summary>
        LOOPNZ,
        /// <summary>XXX</summary>
        LOOPNE,// Loop with ECX and not zero/Loop with ECX and not equal
        /// <summary>XXX</summary>
        CALL,// Call procedure
        /// <summary>XXX</summary>
        RET,// Return
        /// <summary>XXX</summary>
        IRET,// Return from interrupt
        /// <summary>XXX</summary>
        INT,// Software interrupt
        /// <summary>XXX</summary>
        INTO,// Interrupt on overflow
        /// <summary>XXX</summary>
        BOUND,// Detect value out of range
        /// <summary>XXX</summary>
        ENTER,// High-level procedure entry
        /// <summary>XXX</summary>
        LEAVE,// High-level procedure exit
        #endregion
        #region String Instructions
        //The string instructions operate on strings of bytes, allowing them to be moved to and from memory.
        /// <summary>XXX</summary>
        MOVS,
        /// <summary>XXX</summary>
        MOVSB,// Move string/Move byte string
        /// <summary>XXX</summary>
        MOVSW,// Move string/Move word string
        /// <summary>XXX</summary>
        MOVSD,// Move string/Move doubleword string
        /// <summary>XXX</summary>
        CMPS,
        /// <summary>XXX</summary>
        CMPSB,// Compare string/Compare byte string
        /// <summary>XXX</summary>
        CMPSW,// Compare string/Compare word string
        /// <summary>XXX</summary>
        CMPSD,// Compare string/Compare doubleword string
        /// <summary>XXX</summary>
        SCAS,
        /// <summary>XXX</summary>
        SCASB,// Scan string/Scan byte string
        /// <summary>XXX</summary>
        SCASW,// Scan string/Scan word string
        /// <summary>XXX</summary>
        SCASD,// Scan string/Scan doubleword string
        /// <summary>XXX</summary>
        LODS,
        /// <summary>XXX</summary>
        LODSB,// Load string/Load byte string
        /// <summary>XXX</summary>
        LODSW,// Load string/Load word string
        /// <summary>XXX</summary>
        LODSD,// Load string/Load doubleword string
        /// <summary>XXX</summary>
        STOS,
        /// <summary>XXX</summary>
        STOSB,// Store string/Store byte string
        /// <summary>XXX</summary>
        STOSW,// Store string/Store word string
        /// <summary>XXX</summary>
        STOSD,// Store string/Store doubleword string
        /// <summary>XXX</summary>
        REP,// Repeat while ECX not zero
        /// <summary>XXX</summary>
        REPE,
        /// <summary>XXX</summary>
        REPZ,// Repeat while equal/Repeat while zero
        /// <summary>XXX</summary>
        REPNE,
        /// <summary>XXX</summary>
        REPNZ,// Repeat while not equal/Repeat while not zero
        #endregion
        #region I/O Instructions
        //These instructions move data between the processor’s I/O ports and a register or memory.
        //IN Read from a port
        /// <summary>XXX</summary>
        OUT,// Write to a port
        /// <summary>XXX</summary>
        INS,
        /// <summary>XXX</summary>
        INSB,// Input string from port/Input byte string from port
        /// <summary>XXX</summary>
        INSW,// Input string from port/Input word string from port
        /// <summary>XXX</summary>
        INSD,// Input string from port/Input doubleword string from port
        /// <summary>XXX</summary>
        OUTS,
        /// <summary>XXX</summary>
        OUTSB,// Output string to port/Output byte string to port
        /// <summary>XXX</summary>
        OUTSW,// Output string to port/Output word string to port
        /// <summary>XXX</summary>
        OUTSD,// Output string to port/Output doubleword string to port
        #endregion
        #region Flag Control (EFLAG) Instructions
        //The flag control instructions operate on the flags in the EFLAGS register.
        /// <summary>XXX</summary>
        STC,// Set carry flag
        /// <summary>XXX</summary>
        CLC,// Clear the carry flag
        /// <summary>XXX</summary>
        CMC,// Complement the carry flag
        /// <summary>XXX</summary>
        CLD,// Clear the direction flag
        /// <summary>XXX</summary>
        STD,// Set direction flag
        /// <summary>XXX</summary>
        LAHF,// Load flags into AH register
        /// <summary>XXX</summary>
        SAHF,// Store AH register into flags
        /// <summary>XXX</summary>
        PUSHF,
        /// <summary>XXX</summary>
        PUSHFD,// Push EFLAGS onto stack
        /// <summary>XXX</summary>
        POPF,
        /// <summary>XXX</summary>
        POPFD,// Pop EFLAGS from stack
        /// <summary>XXX</summary>
        STI,// Set interrupt flag
        /// <summary>XXX</summary>
        CLI,// Clear the interrupt flag
        #endregion
        #region Segment Register Instructions
        //The segment register instructions allow far pointers (segment addresses) to be loaded into the segment registers.
        /// <summary>XXX</summary>
        LDS,// Load far pointer using DS
        /// <summary>XXX</summary>
        LES,// Load far pointer using ES
        /// <summary>XXX</summary>
        LFS,// Load far pointer using FS
        /// <summary>XXX</summary>
        LGS,// Load far pointer using GS
        /// <summary>XXX</summary>
        LSS,// Load far pointer using SS
        #endregion
        #region Miscellaneous Instructions
        //The miscellaneous instructions provide such functions as loading an effective address, executing a “no-operation,”
        //and retrieving processor identification information.
        /// <summary>XXX</summary>
        LEA,// Load effective address
        /// <summary>XXX</summary>
        NOP,// No operation
        /// <summary>Generates an invalid opcode. This instruction is provided for software testing to explicitly generate an invalid opcode. The opcode for this instruction is reserved for this purpose. Other than raising the invalid opcode exception, this instruction is the same as the NOP instruction.</summary>
        UD2,
        /// <summary>XXX</summary>
        XLAT,
        /// <summary>XXX</summary>
        XLATB,// Table lookup translation
        /// <summary>XXX</summary>
        CPUID,// Processor identification
        /// <summary>XXX</summary>
        MOVBE,// Move data after swapping data bytes
        /// <summary>XXX</summary>
        PREFETCHW,// Prefetch data into cache in anticipation of write
        /// <summary>XXX</summary>
        PREFETCHWT1,// Prefetch hint T1 with intent to write
        /// <summary>XXX</summary>
        CLFLUSH,//Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy
        /// <summary>XXX</summary>
        CLFLUSHOPT,// Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy with optimized memory system throughput.
        #endregion
        #region User Mode Extended Sate Save/Restore Instructions
        /// <summary>XXX</summary>
        XSAVE,// Save processor extended states to memory
        /// <summary>XXX</summary>
        XSAVEC,// Save processor extended states with compaction to memory
        /// <summary>XXX</summary>
        XSAVEOPT,// Save processor extended states to memory, optimized
        /// <summary>XXX</summary>
        XRSTOR,// Restore processor extended states from memory
        /// <summary>XXX</summary>
        XGETBV,// Reads the state of an extended control register
        #endregion
        #region Random Number Generator Instructions
        /// <summary>XXX</summary>
        RDRAND,// Retrieves a random number generated from hardware
        /// <summary>XXX</summary>
        RDSEED,// Retrieves a random number generated from hardware
        #endregion
        #region BMI1, BMI2
        /// <summary>XXX</summary>
        ANDN,// Bitwise AND of first source with inverted 2nd source operands.
        /// <summary>XXX</summary>
        BEXTR,// Contiguous bitwise extract
        /// <summary>XXX</summary>
        BLSI,// Extract lowest set bit
        /// <summary>XXX</summary>
        BLSMSK,// Set all lower bits below first set bit to 1
        /// <summary>XXX</summary>
        BLSR,// Reset lowest set bit
        /// <summary>XXX</summary>
        BZHI,// Zero high bits starting from specified bit position

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

        #region SSE
        /// <summary>XXX</summary>
        XORPS,
        /// <summary>XXX</summary>
        XORPD
        #endregion

    }

    public static partial class AsmSourceTools {

        public static Bt conditionalTaken(ConditionalElement ce, CarryFlag CF, ZeroFlag ZF, SignFlag SF, OverflowFlag OF, ParityFlag PF) {
            switch (ce) {
                case ConditionalElement.UNCONDITIONAL: return Bt.ONE;
                case ConditionalElement.A: return BitOperations.or(BitOperations.neg(CF), BitOperations.neg(ZF));
                case ConditionalElement.AE: return BitOperations.neg(CF);
                case ConditionalElement.B: return CF;
                case ConditionalElement.BE: return BitOperations.and(BitOperations.neg(CF), BitOperations.neg(ZF));
                case ConditionalElement.C: return CF;
                case ConditionalElement.E: return ZF;
                case ConditionalElement.G: return BitOperations.and(BitOperations.neg(ZF), BitOperations.eq(SF, OF));
                case ConditionalElement.GE: return BitOperations.neg(BitOperations.xor(SF, OF));
                case ConditionalElement.L: return BitOperations.xor(SF, OF);
                case ConditionalElement.LE: return BitOperations.or(BitOperations.xor(SF, OF), ZF);
                case ConditionalElement.NA: return BitOperations.and(BitOperations.neg(CF), BitOperations.neg(ZF));
                case ConditionalElement.NAE: return CF;
                case ConditionalElement.NB: return BitOperations.neg(CF);
                case ConditionalElement.NBE: return BitOperations.or(BitOperations.neg(CF), BitOperations.neg(ZF));
                case ConditionalElement.NC: return BitOperations.neg(CF);
                case ConditionalElement.NE: return BitOperations.neg(ZF);
                case ConditionalElement.NG: return BitOperations.or(BitOperations.xor(SF, OF), ZF);
                case ConditionalElement.NGE: return BitOperations.xor(SF, OF);
                case ConditionalElement.NL: return BitOperations.neg(BitOperations.xor(SF, OF));
                case ConditionalElement.NLE: return BitOperations.and(BitOperations.neg(ZF), BitOperations.eq(SF, OF));
                case ConditionalElement.NO: return BitOperations.neg(OF);
                case ConditionalElement.NP: return BitOperations.neg(PF);
                case ConditionalElement.NS: return BitOperations.neg(SF);
                case ConditionalElement.NZ: return BitOperations.neg(ZF);
                case ConditionalElement.O: return OF;
                case ConditionalElement.P: return PF;
                case ConditionalElement.PE: return PF;
                case ConditionalElement.PO: return BitOperations.neg(PF);
                case ConditionalElement.S: return SF;
                case ConditionalElement.Z: return ZF;
                default: return Bt.UNDEFINED;
            }
        }

        public static Mnemonic parseMnemonic(string str) {
            switch (str.ToUpper()) {
                case "UNKNOWN": return Mnemonic.UNKNOWN;
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
                case "MOVSX": return Mnemonic.MOVSX;
                case "MOVSXD": return Mnemonic.MOVSXD;
                case "MOVZX": return Mnemonic.MOVZX;
                case "MOVZXD": return Mnemonic.MOVZXD;
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
                case "MOVS": return Mnemonic.MOVS;
                case "MOVSB": return Mnemonic.MOVSB;
                case "MOVSW": return Mnemonic.MOVSW;
                case "MOVSD": return Mnemonic.MOVSD;
                case "CMPS": return Mnemonic.CMPS;
                case "CMPSB": return Mnemonic.CMPSB;
                case "CMPSW": return Mnemonic.CMPSW;
                case "CMPSD": return Mnemonic.CMPSD;
                case "SCAS": return Mnemonic.SCAS;
                case "SCASB": return Mnemonic.SCASB;
                case "SCASW": return Mnemonic.SCASW;
                case "SCASD": return Mnemonic.SCASD;
                case "LODS": return Mnemonic.LODS;
                case "LODSB": return Mnemonic.LODSB;
                case "LODSW": return Mnemonic.LODSW;
                case "LODSD": return Mnemonic.LODSD;
                case "STOS": return Mnemonic.STOS;
                case "STOSB": return Mnemonic.STOSB;
                case "STOSW": return Mnemonic.STOSW;
                case "STOSD": return Mnemonic.STOSD;
                case "REP": return Mnemonic.REP;
                case "REPE": return Mnemonic.REPE;
                case "REPZ": return Mnemonic.REPZ;
                case "REPNE": return Mnemonic.REPNE;
                case "REPNZ": return Mnemonic.REPNZ;
                case "OUT": return Mnemonic.OUT;
                case "INS": return Mnemonic.INS;
                case "INSB": return Mnemonic.INSB;
                case "INSW": return Mnemonic.INSW;
                case "INSD": return Mnemonic.INSD;
                case "OUTS": return Mnemonic.OUTS;
                case "OUTSB": return Mnemonic.OUTSB;
                case "OUTSW": return Mnemonic.OUTSW;
                case "OUTSD": return Mnemonic.OUTSD;
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
                case "UD2": return Mnemonic.UD2;
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

                case "XORPS": return Mnemonic.XORPS;
                case "XORPD": return Mnemonic.XORPD;
                default:
                    Console.WriteLine("WARNING;parseMnemonic. unknown str=\"" + str + "\".");
                    return Mnemonic.UNKNOWN;
            }
        }
    }
}
