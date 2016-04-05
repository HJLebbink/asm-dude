using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmTools {


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
        MOVZX,// Move and zero extend
        #endregion
        #region Binary Arithmetic Instructions
        // The binary arithmetic instructions perform basic binary integer computations on byte, word, and doubleword integers
        // located in memory and/or the general purpose registers.

        ADCX,// Unsigned integer add with carry
        ADOX,// Unsigned integer add with overflow
        ADD,// Integer add
        ADC,// Add with carry
        SUB,// Subtract
        SBB,// Subtract with borrow
        IMUL,// Signed multiply
        MUL,// Unsigned multiply
        IDIV,// Signed divide
        DIV,// Unsigned divide
        INC,// Increment
        DEC,// Decrement
        NEG,// Negate
        CMP,// Compare
        #endregion
        #region Decimal Arithmetic Instructions
        //The decimal arithmetic instructions perform decimal arithmetic on binary coded decimal (BCD) data.
        DAA,// Decimal adjust after addition
        DAS,// Decimal adjust after subtraction
        AAA,// ASCII adjust after addition
        AAS,// ASCII adjust after subtraction
        AAM,// ASCII adjust after multiplication
        AAD,// ASCII adjust before division
        #endregion
        #region Logical Instructions
        //The logical instructions perform basic AND, OR, XOR, and NOT logical operations on byte, word, and doubleword
        //values.
        AND,// Perform bitwise logical AND
        OR,// Perform bitwise logical OR
        XOR,// Perform bitwise logical exclusive OR
        NOT,// Perform bitwise logical NOT
        #endregion
        #region Shift and Rotate Instructions
        //The shift and rotate instructions shift and rotate the bits in word and doubleword operands.
        SAR,// Shift arithmetic right
        SHR,// Shift logical right
        SAL,
        SHL,// Shift arithmetic left/Shift logical left
        SHRD,// Shift right double
        SHLD,//Shift left double
        ROR,// Rotate right
        ROL,// Rotate left
        RCR,// Rotate through carry right
        RCL,// Rotate through carry left
        #endregion
        #region Bit and Byte Instructions
        //Bit instructions test and modify individual bits in word and doubleword operands. Byte instructions set the value of
        //a byte operand to indicate the status of flags in the EFLAGS register.
        BT,// Bit test
        BTS,// Bit test and set
        BTR,// Bit test and reset
        BTC,// Bit test and complement
        BSF,// Bit scan forward
        BSR,// Bit scan reverse
        SETE,
        SETZ,// Set byte if equal/Set byte if zero
        SETNE,
        SETNZ,// Set byte if not equal/Set byte if not zero
        SETA,
        SETNBE,// Set byte if above/Set byte if not below or equal
        SETAE,
        SETNB,
        SETNC,// Set byte if above or equal/Set byte if not below/Set byte if not carry
        SETB,
        SETNAE,
        SETC,//Set byte if below/Set byte if not above or equal/Set byte if carry
        SETBE,
        SETNA,// Set byte if below or equal/Set byte if not above
        SETG,
        SETNLE,// Set byte if greater/Set byte if not less or equal
        SETGE,
        SETNL,// Set byte if greater or equal/Set byte if not less
        SETL,
        SETNGE,// Set byte if less/Set byte if not greater or equal
        SETLE,
        SETNG,// Set byte if less or equal/Set byte if not greater
        SETS,// Set byte if sign (negative)
        SETNS,// Set byte if not sign (non-negative)
        SETO,// Set byte if overflow
        SETNO,// Set byte if not overflow
        SETPE,
        SETP,// Set byte if parity even/Set byte if parity
        SETPO,
        SETNP,// Set byte if parity odd/Set byte if not parity
        TEST,// Logical compare
        CRC32,// Provides hardware acceleration to calculate cyclic redundancy checks for fast and efficient implementation of data integrity protocols.
        POPCNT,// This instruction calculates of number of bits set to 1 in the second operand (source) and returns the count in the first operand (a destination register)
        #endregion
        #region Control Transfer Instructions
        // The control transfer instructions provide jump, conditional jump, loop, and call and return operations to control
        //program flow.
        JMP,// Jump
        JE,
        JZ,// Jump if equal/Jump if zero
        JNE,
        JNZ,// Jump if not equal/Jump if not zero
        JA,
        JNBE,// Jump if above/Jump if not below or equal
        JAE,
        JNB,// Jump if above or equal/Jump if not below
        JB,
        JNAE,// Jump if below/Jump if not above or equal
        JBE,
        JNA,// Jump if below or equal/Jump if not above
        JG,
        JNLE,// Jump if greater/Jump if not less or equal
        JGE,
        JNL,// Jump if greater or equal/Jump if not less
        JL,
        JNGE,// Jump if less/Jump if not greater or equal
        JLE,
        JNG,// Jump if less or equal/Jump if not greater
        JC,// Jump if carry
        JNC,// Jump if not carry
        JO,// Jump if overflow
        JNO,// Jump if not overflow
        JS,// Jump if sign (negative)
        JNS,// Jump if not sign (non-negative)
        JPO,
        JNP,// Jump if parity odd/Jump if not parity
        JPE,
        JP,// Jump if parity even/Jump if parity
        JCXZ,
        JECXZ,// Jump register CX zero/Jump register ECX zero
        JRCXZ,
        LOOP,// Loop with ECX counter
        LOOPZ,
        LOOPE,// Loop with ECX and zero/Loop with ECX and equal
        LOOPNZ,
        LOOPNE,// Loop with ECX and not zero/Loop with ECX and not equal
        CALL,// Call procedure
        RET,// Return
        IRET,// Return from interrupt
        INT,// Software interrupt
        INTO,// Interrupt on overflow
        BOUND,// Detect value out of range
        ENTER,// High-level procedure entry
        LEAVE,// High-level procedure exit
        #endregion
        #region String Instructions
        //The string instructions operate on strings of bytes, allowing them to be moved to and from memory.
        MOVS,
        MOVSB,// Move string/Move byte string
        MOVSW,// Move string/Move word string
        MOVSD,// Move string/Move doubleword string
        CMPS,
        CMPSB,// Compare string/Compare byte string
        CMPSW,// Compare string/Compare word string
        CMPSD,// Compare string/Compare doubleword string
        SCAS,
        SCASB,// Scan string/Scan byte string
        SCASW,// Scan string/Scan word string
        SCASD,// Scan string/Scan doubleword string
        LODS,
        LODSB,// Load string/Load byte string
        LODSW,// Load string/Load word string
        LODSD,// Load string/Load doubleword string
        STOS,
        STOSB,// Store string/Store byte string
        STOSW,// Store string/Store word string
        STOSD,// Store string/Store doubleword string
        REP,// Repeat while ECX not zero
        REPE,
        REPZ,// Repeat while equal/Repeat while zero
        REPNE,
        REPNZ,// Repeat while not equal/Repeat while not zero
        #endregion
        #region I/O Instructions
        //These instructions move data between the processor’s I/O ports and a register or memory.
        //IN Read from a port
        OUT,// Write to a port
        INS,
        INSB,// Input string from port/Input byte string from port
        INSW,// Input string from port/Input word string from port
        INSD,// Input string from port/Input doubleword string from port
        OUTS,
        OUTSB,// Output string to port/Output byte string to port
        OUTSW,// Output string to port/Output word string to port
        OUTSD,// Output string to port/Output doubleword string to port
        #endregion
        #region Flag Control (EFLAG) Instructions
        //The flag control instructions operate on the flags in the EFLAGS register.
        STC,// Set carry flag
        CLC,// Clear the carry flag
        CMC,// Complement the carry flag
        CLD,// Clear the direction flag
        STD,// Set direction flag
        LAHF,// Load flags into AH register
        SAHF,// Store AH register into flags
        PUSHF,
        PUSHFD,// Push EFLAGS onto stack
        POPF,
        POPFD,// Pop EFLAGS from stack
        STI,// Set interrupt flag
        CLI,// Clear the interrupt flag
        #endregion
        #region Segment Register Instructions
        //The segment register instructions allow far pointers (segment addresses) to be loaded into the segment registers.
        LDS,// Load far pointer using DS
        LES,// Load far pointer using ES
        LFS,// Load far pointer using FS
        LGS,// Load far pointer using GS
        LSS,// Load far pointer using SS
        #endregion
        #region Miscellaneous Instructions
        //The miscellaneous instructions provide such functions as loading an effective address, executing a “no-operation,”
        //and retrieving processor identification information.
        LEA,// Load effective address
        NOP,// No operation
        UD2,// Undefined instruction
        XLAT,
        XLATB,// Table lookup translation
        CPUID,// Processor identification
        MOVBE,// Move data after swapping data bytes
        PREFETCHW,// Prefetch data into cache in anticipation of write
        PREFETCHWT1,// Prefetch hint T1 with intent to write
        CLFLUSH,//Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy
        CLFLUSHOPT,// Flushes and invalidates a memory operand and its associated cache line from all levels of the processor’s cache hierarchy with optimized memory system throughput.
        #endregion
        #region User Mode Extended Sate Save/Restore Instructions
        XSAVE,// Save processor extended states to memory
        XSAVEC,// Save processor extended states with compaction to memory
        XSAVEOPT,// Save processor extended states to memory, optimized
        XRSTOR,// Restore processor extended states from memory
        XGETBV,// Reads the state of an extended control register
        #endregion
        #region Random Number Generator Instructions
        RDRAND,// Retrieves a random number generated from hardware
        RDSEED,// Retrieves a random number generated from hardware
        #endregion
        #region BMI1, BMI2
        ANDN,// Bitwise AND of first source with inverted 2nd source operands.
        BEXTR,// Contiguous bitwise extract
        BLSI,// Extract lowest set bit
        BLSMSK,// Set all lower bits below first set bit to 1
        BLSR,// Reset lowest set bit
        BZHI,// Zero high bits starting from specified bit position
        LZCNT,// Count the number leading zero bits
        MULX,// Unsigned multiply without affecting arithmetic flags
        PDEP,// Parallel deposit of bits using a mask
        PEXT,// Parallel extraction of bits using a mask
        RORX,// Rotate right without affecting arithmetic flags
        SARX,// Shift arithmetic right
        SHLX,// Shift logic left
        SHRX,// Shift logic right
        TZCNT,// Count the number trailing zero bits
        #endregion
    }
}
