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
    /// <summary>
    /// The outcome of asking "does this instruction change the machine state?": of the tracked locations
    /// the instruction writes, <see cref="WrittenCount"/> is how many were considered and
    /// <see cref="RedundantCount"/> how many are provably UNCHANGED (value before == value after).
    /// Produced by <see cref="Runner.IsRedundantInstruction(string, State)"/>.
    /// </summary>
    public readonly record struct RedundancyVerdict(int WrittenCount, int RedundantCount)
    {
        /// <summary>True iff the instruction writes at least one tracked location and EVERY written location
        /// is provably unchanged — i.e. executing the instruction cannot change the tracked machine state.
        /// An instruction that writes nothing (e.g. a jump, NOP, or a bare CMP whose flags are off) is not
        /// "redundant" by this definition.</summary>
        public bool IsRedundant => this.WrittenCount > 0 && this.RedundantCount == this.WrittenCount;
    }
}
