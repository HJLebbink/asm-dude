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
    using System.Threading;

    /// <summary>
    /// Observability for the Z3 native <c>Microsoft.Z3.Context</c> lifecycle — the object behind BOTH
    /// historical dangers of the simulator: the ~18 GiB native memory leak (a <c>Context</c> per line that
    /// was never disposed) and the context-lifecycle access-violation crash. A <c>Context</c> is heavy,
    /// native, and invisible to the GC, so counting it explicitly is the only way to SEE the leak.
    ///
    /// <para>Each OWNED context creation calls <see cref="Created"/>; each owned disposal calls
    /// <see cref="Disposed"/>. Borrowed / shared-context references are NOT re-counted (only the object that
    /// actually allocates the native context counts it). The <c>ProgramSynthesizer</c> CLI experiment is out
    /// of scope. Therefore:</para>
    /// <list type="bullet">
    ///   <item><see cref="Live"/> — native Z3 contexts the simulator is holding right now (GC-invisible
    ///   memory pressure). Across a clean run it returns to its pre-run value; if it keeps climbing, a
    ///   context is leaking — exactly the 18 GiB regression.</item>
    ///   <item><see cref="Peak"/> — high-water mark (how many contexts a run held simultaneously; the
    ///   per-component engine will raise this, so it bounds peak native memory).</item>
    ///   <item><see cref="TotalCreated"/> — cumulative; the delta across a run is "contexts built this run".</item>
    /// </list>
    /// This is observation-only (no behavior change). The simulator logs these in its run summary, and a
    /// leak-regression test asserts <see cref="Live"/> returns to baseline after a synchronous run.
    /// </summary>
    public static class Z3ContextTracker
    {
        private static long live_;
        private static long peak_;
        private static long total_;

        /// <summary>Native Z3 contexts currently alive (owned, undisposed).</summary>
        public static long Live => Interlocked.Read(ref live_);

        /// <summary>Highest simultaneous <see cref="Live"/> observed since process start.</summary>
        public static long Peak => Interlocked.Read(ref peak_);

        /// <summary>Cumulative owned contexts created since process start.</summary>
        public static long TotalCreated => Interlocked.Read(ref total_);

        /// <summary>Record that an owned native Z3 context was just allocated.</summary>
        public static void Created()
        {
            Interlocked.Increment(ref total_);
            long now = Interlocked.Increment(ref live_);

            // Raise the peak high-water mark (lock-free CAS; approximate is acceptable for a gauge).
            long peak = Interlocked.Read(ref peak_);
            while (now > peak)
            {
                long observed = Interlocked.CompareExchange(ref peak_, now, peak);
                if (observed == peak) break;
                peak = observed;
            }
        }

        /// <summary>Record that an owned native Z3 context was just disposed.</summary>
        public static void Disposed() => Interlocked.Decrement(ref live_);
    }
}
