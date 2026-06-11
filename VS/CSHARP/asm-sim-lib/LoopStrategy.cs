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
    using System.Collections.Generic;

    using AsmTools;

    /// <summary>
    /// How the simulator summarizes a loop (a CFG cycle) when computing per-line states. Selectable so the
    /// precision/runtime trade-off can be measured empirically (the strategies are interchangeable behind
    /// <see cref="ILoopStrategy"/>). The only thing that varies between them is the state computed for a loop
    /// header that is valid for every iteration; everything acyclic is identical.
    /// </summary>
    public enum LoopHandling
    {
        /// <summary>Loop header becomes fully unknown (cheapest, least precise). Matches the legacy engine.</summary>
        Accept,

        /// <summary>Havoc only the registers/flags the loop body writes; values it does not touch are kept
        /// (recovers loop invariants). Cheap — no Z3 iteration, the write-set is syntactic.</summary>
        ModSetHavoc,

        /// <summary>Unroll the first iteration concretely (for the body display), summarize the rest via
        /// <see cref="ModSetHavoc"/>.</summary>
        PeelOnce,

        /// <summary>Unroll until the back-edge is unsatisfiable or the budget cap is hit (precise for
        /// statically-bounded loops); havoc the residual.</summary>
        FullUnroll,

        /// <summary>Iterate body + join (a sound widening for the finite-height Tv lattice) to a least
        /// fixpoint. Most precise, most expensive.</summary>
        Fixpoint,
    }

    /// <summary>Bounds on the per-loop work a strategy may do (so cost is tunable for experiments).</summary>
    public readonly struct LoopBudget
    {
        public readonly int MaxIterations;
        public readonly int MaxUnroll;

        public LoopBudget(int maxIterations, int maxUnroll)
        {
            this.MaxIterations = maxIterations;
            this.MaxUnroll = maxUnroll;
        }

        public static LoopBudget Default => new(maxIterations: 16, maxUnroll: 8);
    }

    /// <summary>The registers and flags a loop body may write (its syntactic write-set). Cheap to compute
    /// and the only thing <see cref="LoopHandling.ModSetHavoc"/> needs.</summary>
    public readonly struct ModSet
    {
        public readonly IReadOnlySet<Rn> Regs;
        public readonly Flags Flags;

        public ModSet(IReadOnlySet<Rn> regs, Flags flags)
        {
            this.Regs = regs ?? new HashSet<Rn>();
            this.Flags = flags;
        }

        public static ModSet Empty => new(new HashSet<Rn>(), Flags.NONE);
    }

    /// <summary>
    /// Computes the state on entry to a loop header that is sound for every iteration, resolving the
    /// back-edge cycle. The driver (forward worklist) supplies the entry state, a "run the body once"
    /// transfer function, and the body's write-set; the strategy decides how hard to work.
    /// </summary>
    public interface ILoopStrategy
    {
        LoopHandling Kind { get; }

        /// <param name="entry">Join of the loop header's non-back-edge predecessors (acyclic, already computed).</param>
        /// <param name="body">Run the loop body once from a given header state; returns the state arriving
        /// back at the header along the back-edge(s). May be invoked zero or more times.</param>
        /// <param name="modSet">Registers/flags the loop body writes.</param>
        /// <param name="budget">Iteration/unroll caps.</param>
        /// <returns>A NEW state (caller owns/disposes) summarizing the header for all iterations.</returns>
        State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget);
    }

    /// <summary>Maps the <see cref="LoopHandling"/> knob to its implementation.</summary>
    public static class LoopStrategies
    {
        public static ILoopStrategy For(LoopHandling handling) => handling switch
        {
            LoopHandling.Accept => new AcceptStrategy(),
            LoopHandling.ModSetHavoc => new ModSetHavocStrategy(),
            LoopHandling.PeelOnce => new PeelOnceStrategy(),
            LoopHandling.FullUnroll => new FullUnrollStrategy(),
            LoopHandling.Fixpoint => new FixpointStrategy(),
            _ => new AcceptStrategy(),
        };
    }

    /// <summary>1. Header becomes fully unknown. Crudest; matches the legacy engine's loop behavior.</summary>
    public sealed class AcceptStrategy : ILoopStrategy
    {
        public LoopHandling Kind => LoopHandling.Accept;

        public State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget)
        {
            ArgumentNullException.ThrowIfNull(entry);
            // A fresh state over the same key/tools is fully unknown — the soundest crude summary.
            return new State(entry.Ctx, entry.Tools, entry.TailKey, entry.HeadKey);
        }
    }

    /// <summary>2. Havoc only the body's write-set; invariants survive. No Z3 iteration.</summary>
    public sealed class ModSetHavocStrategy : ILoopStrategy
    {
        public LoopHandling Kind => LoopHandling.ModSetHavoc;

        public State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return entry.Havoc(modSet.Regs, modSet.Flags);
        }
    }

    /// <summary>3. Peel iteration 1, summarize the residual via ModSetHavoc. (Header summary only; the
    /// concrete first-iteration body display is the worklist's job.)</summary>
    public sealed class PeelOnceStrategy : ILoopStrategy
    {
        public LoopHandling Kind => LoopHandling.PeelOnce;

        public State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(body);
            using State afterOne = body(entry);
            using State joined = new(entry, afterOne, merge: true);
            return joined.Havoc(modSet.Regs, modSet.Flags);
        }
    }

    /// <summary>4. Unroll until the back-edge is unsatisfiable or the budget cap is hit; havoc residual.</summary>
    public sealed class FullUnrollStrategy : ILoopStrategy
    {
        public LoopHandling Kind => LoopHandling.FullUnroll;

        public State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(body);

            State current = new(entry);
            for (int i = 0; i < budget.MaxUnroll; ++i)
            {
                State next = body(current);
                if (next.IsConsistent == Tv.ZERO) // back-edge can't be taken again ⇒ loop exits here
                {
                    next.Dispose();
                    return current;
                }
                current.Dispose();
                current = next;
            }
            // Budget exhausted (unbounded / large loop): fall back to a sound summary of the residual.
            State summary = current.Havoc(modSet.Regs, modSet.Flags);
            current.Dispose();
            return summary;
        }
    }

    /// <summary>5. Iterate body + join to a least fixpoint. Join is a valid widening here because the
    /// per-bit Tv lattice has finite height (a bit only ever moves known → unknown), so it converges.</summary>
    public sealed class FixpointStrategy : ILoopStrategy
    {
        public LoopHandling Kind => LoopHandling.Fixpoint;

        public State ResolveHeader(State entry, Func<State, State> body, ModSet modSet, LoopBudget budget)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(body);

            State current = new(entry);
            for (int i = 0; i < budget.MaxIterations; ++i)
            {
                using State afterBody = body(current);
                State widened = new(current, afterBody, merge: true); // join = widening (finite height)
                if (State.Equiv(current, widened))
                {
                    current.Dispose();
                    return widened; // fixpoint reached
                }
                current.Dispose();
                current = widened;
            }
            return current; // budget cap: sound (join is monotone), just may be one step short
        }
    }
}
