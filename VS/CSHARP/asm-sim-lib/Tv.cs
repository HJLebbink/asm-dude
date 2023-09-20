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

    [Flags]
    public enum Tv
    {
        /// <summary>
        /// Unknown bit value: the value of a bit is neither 1 or 0. This happens when Z3 determines that 1] the bit value of 1 is consistent, and 2] the bit value of 0 is consistent.
        /// (This is the default value: The default value of an enum E is the value produced by the expression (E)0)
        /// </summary>
        UNKNOWN = 0,

        /// <summary>
        /// Undefined bit value: the value of a bit is undefined. Instructions may produce this value as a result of normal operation
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
        /// Inconsistent bit value: the value of a bit is set to both 1 and 0. This happens when Z3 determines that 1] the bit value of 1 is inconsistent, and 2] the bit value 0 is inconsistent. This happens when unreachable code is evaluated
        /// </summary>
        INCONSISTENT = 1 << 3,

        /// <summary>
        /// Undetermined bit value: the value of a bit could not be determined by Z3. This happens when Z3 cannot determine consistency due to timeouts
        /// </summary>
        UNDETERMINED = 1 << 4,
    }
}
