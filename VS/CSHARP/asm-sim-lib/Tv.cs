// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

    [Flags]
    public enum Tv
    {
        /// <summary>
        /// Unknown bit value: the bit is either 0 or 1 at runtime, but which value is not (yet) tracked.
        /// Typical cause: an uninitialized register or flag at function entry, or a value that depends on
        /// an unresolved runtime input. Z3 reports both 0 and 1 as consistent with the current constraints.
        /// (This is the default value: The default value of an enum E is the value produced by the expression (E)0)
        /// </summary>
        UNKNOWN = 0,

        /// <summary>
        /// Undefined bit value: the ISA specification (Intel/AMD manual) explicitly states that the value
        /// of this bit after the instruction is architecturally unpredictable and must not be relied upon.
        /// Examples: AF after SHR/SAR/SHL, OF after a multi-bit shift.
        /// This is an architectural property of the instruction — independent of the concrete input values.
        /// Reading an UNDEFINED bit is a code bug; the simulator can flag such reads as diagnostics.
        /// </summary>
        UNDEFINED = 1 << 0,

        /// <summary>
        /// Set bit value: the value of a bit is set to 1/True. Instructions may produce this value as a result of normal operation
        /// </summary>
        ONE = 1 << 1,

        /// <summary>
        /// Cleared bit value: the value of a bit is set to 0/False. Instructions may produce this value as a result of normal operation
        /// </summary>
        ZERO = 1 << 2,

        /// <summary>
        /// Inconsistent bit value: Z3 determines that neither 0 nor 1 is consistent with the current
        /// path constraints — i.e. this code path is unreachable. Both possible values lead to
        /// a contradiction, so no concrete execution can reach this point.
        /// </summary>
        INCONSISTENT = 1 << 3,

        /// <summary>
        /// Undetermined bit value: Z3 could not decide consistency within the allotted timeout.
        /// The value may be 0, 1, or unknown — the solver simply ran out of time to prove it.
        /// </summary>
        UNDETERMINED = 1 << 4,
    }
}
