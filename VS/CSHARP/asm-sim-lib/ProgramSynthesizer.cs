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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmTools;

using Microsoft.Z3;

using System.Diagnostics;

namespace AsmSim
{
    /// <summary>
    /// Synthesis goals for <see cref="ProgramSynthesizer.Synthesize(SynthSpec, int, IList{Rn})"/>. Each is a
    /// relation between the initial register values (<c>reg!0</c>) and the final ones (<c>reg!nLines</c>) that
    /// the synthesized straight-line program must satisfy <b>for all inputs</b>.
    /// </summary>
    public enum SynthSpec
    {
        /// <summary><c>RAX!n == 0</c> — zero a register. Idiomatic answer: <c>XOR RAX, RAX</c>.</summary>
        ZeroRax,

        /// <summary><c>∀ RAX!0. RAX!n == RAX!0 + 1</c> — increment. Answer: <c>INC RAX</c>.</summary>
        IncRax,

        /// <summary><c>∀ RAX!0. RAX!n == -RAX!0</c> — two's-complement negate. Answer: <c>NOT RAX; INC RAX</c>.</summary>
        NegRax,

        /// <summary>
        /// <c>∀ RAX!0, RBX!0. RAX!n == RBX!0 ∧ RBX!n == RAX!0</c> — swap two registers without a temporary.
        /// Answer: the classic 3-instruction XOR swap <c>XOR RAX,RBX; XOR RBX,RAX; XOR RAX,RBX</c>.
        /// </summary>
        SwapRaxRbx,
    }

    /// <summary>
    /// The instruction families <see cref="ProgramSynthesizer.Synthesize(SynthSpec, int, IList{Rn}, SynthInstr, bool)"/>
    /// is allowed to use — the knob that controls the size of the search space. Binary ops are emitted for every
    /// <b>ordered</b> register pair (including <c>r,r</c>); unary ops and NOP once per register. All are
    /// register-to-register (no immediates), so per line the candidate count is
    /// <c>[Nop?1:0] + |regs|·(#unary) + |regs|²·(#binary)</c> and the program space is that raised to <c>nLines</c>.
    /// </summary>
    [Flags]
    public enum SynthInstr
    {
        None = 0,
        Inc = 1 << 0,  // r := r + 1
        Dec = 1 << 1,  // r := r - 1
        Not = 1 << 2,  // r := ~r
        Neg = 1 << 3,  // r := -r
        Xor = 1 << 4,  // r1 := r1 ^ r2
        And = 1 << 5,  // r1 := r1 & r2
        Or = 1 << 6,  // r1 := r1 | r2
        Add = 1 << 7,  // r1 := r1 + r2
        Sub = 1 << 8,  // r1 := r1 - r2
        Mov = 1 << 9, // r1 := r2

        /// <summary>The minimal menu that covers the four demo specs.</summary>
        Default = Inc | Not | Xor,

        /// <summary>Every modelled instruction — the largest search space.</summary>
        All = Inc | Dec | Not | Neg | Xor | And | Or | Add | Sub | Mov,
    }

    /// <summary>
    /// Outcome of one <see cref="ProgramSynthesizer.Synthesize(SynthSpec, int, IList{Rn}, SynthInstr, bool)"/> run:
    /// the search-space metrics plus the wall-clock timings, for studying time vs. space.
    /// </summary>
    /// <param name="Found">Number of distinct correct programs enumerated.</param>
    /// <param name="InstrsPerLine">Candidate instructions per line (k).</param>
    /// <param name="Switches">Total boolean switch variables (k · nLines).</param>
    /// <param name="ProgramSpace">Size of the straight-line program space (k ^ nLines).</param>
    /// <param name="FirstMs">Milliseconds to the first solution (-1 if none found).</param>
    /// <param name="TotalMs">Milliseconds to enumerate all solutions (incl. the final exhaustion check).</param>
    /// <param name="TimedOut">True if a solver check returned UNKNOWN (e.g. hit the per-check timeout).</param>
    public readonly record struct SynthResult(int Found, int InstrsPerLine, int Switches, double ProgramSpace, long FirstMs, long TotalMs, bool TimedOut);

    /// <summary>
    /// Experimental Z3-based <b>program synthesizer</b> (a tiny superoptimizer) — the inverse of the AsmSim
    /// simulator. Where the simulator takes a program and computes its symbolic effect, this fixes the desired
    /// effect (a specification) and asks Z3 "which straight-line assembly programs of length <c>nLines</c> over
    /// the given <c>registers</c> compute it?", then enumerates them.
    /// <para>
    /// Encoding (SyGuS style): for every line, <see cref="BuildMenu"/> generates a menu of candidate
    /// instructions from a <see cref="SynthInstr"/> flags mask, each guarded by a boolean "switch".
    /// <c>MkAtMost(switches,1) + MkOr(switches)</c> forces exactly one instruction per line; a true switch
    /// asserts that instruction's symbolic state transition. The entire transition-system implication is
    /// wrapped in a universal quantifier over initial/intermediate register values:
    /// <c>∃ switches . ∀ regs . (transitions ⇒ target)</c>, making the spec input-independent.
    /// </para>
    /// <para>
    /// <b>Status: dormant research scratchpad — not on the editor/LSP path.</b> The only references are
    /// CLI calls in <c>asm-sim-main/ProgramZ3.cs</c>. It reuses the engine's Z3 / symbolic-register
    /// infrastructure in "synthesis" mode rather than "simulation" mode.
    /// </para>
    /// </summary>
    public static class ProgramSynthesizer
    {
        private static BitVecExpr GetReg(Rn reg, int lineNumber, Context ctx)
        {
            return ctx.MkBVConst(reg + "!" + lineNumber, (uint)RegisterTools.NBits(reg));
        }

        #region Clean SyGuS-style synthesis (∃ switches . ∀ inputs)

        /// <summary>
        /// Synthesize every straight-line program of length <paramref name="nLines"/> over
        /// <paramref name="registers"/> that satisfies <paramref name="spec"/> for ALL inputs, and log each one
        /// (category "SIM"). Returns the number of distinct programs found.
        /// <para>
        /// The target is asserted under a universal quantifier over the initial/intermediate register values:
        /// <c>∃ switches . ∀ regs . (transitions ⇒ target)</c>. This makes an input-dependent spec sound —
        /// Z3 can no longer "satisfy" <c>RAX!n == RAX!0 + 1</c> by choosing a convenient <c>RAX!0</c>;
        /// the relation must hold for every input.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Fully self-contained: creates its own Z3 <c>Context</c> and <c>Solver</c>, independent of any
        /// prior state. <paramref name="allowed"/> (default <c>SynthInstr.Default</c> = INC | NOT | XOR) selects
        /// the instruction families; <see cref="BuildMenu"/> emits one candidate per (family, register, optional
        /// source-register) tuple. Each candidate is guarded by a boolean switch;
        /// <c>MkAtMost(1)+MkOr</c> force exactly one instruction per line. Programs are enumerated by blocking
        /// each solution's switch combination and re-checking until UNSAT (capped at 256).
        /// </remarks>
        public static SynthResult Synthesize(SynthSpec spec, int nLines, IList<Rn> registers, SynthInstr allowed = SynthInstr.Default, bool logPrograms = true)
        {
            Dictionary<string, string> settings = new()
            {
                { "model", "true" },
                { "timeout", "60000" }, // a stuck quantified-BV check returns UNKNOWN and ends enumeration
            };
            using Context ctx = new(settings);
            Solver solver = ctx.MkSolver();

            // line -> the menu of (switch, human-readable asm) candidate instructions for that line.
            Dictionary<int, List<(BoolExpr sw, string asm)>> switches = [];
            List<BoolExpr> transitions = []; // every "switch ⇒ full-next-state" implication, for the ∀ body

            for (int line = 1; line <= nLines; ++line)
            {
                switches[line] = BuildMenu(ctx, registers, line, allowed, transitions);
                BoolExpr[] sws = new BoolExpr[switches[line].Count];
                for (int i = 0; i < sws.Length; ++i) sws[i] = switches[line][i].sw;
                solver.Assert(ctx.MkAtMost(sws, 1)); // at most one instruction per line ...
                solver.Assert(ctx.MkOr(sws));        // ... and at least one (so exactly one)
            }

            // ∀ (every register at every line) . (AND transitions) ⇒ target
            List<Expr> quantified = [];
            foreach (Rn r in registers)
            {
                for (int line = 0; line <= nLines; ++line)
                {
                    quantified.Add(GetReg(r, line, ctx));
                }
            }

            BoolExpr target = BuildTarget(ctx, spec, nLines);
            BoolExpr body = ctx.MkImplies(ctx.MkAnd([.. transitions]), target);
            solver.Assert(ctx.MkForall([.. quantified], body));

            int k = switches[1].Count;
            double programSpace = Math.Pow(k, nLines);
            if (logPrograms)
            {
                AsmLog.Info("SIM", $"Synthesizing {spec} over [{string.Join(",", registers)}], nLines={nLines}, " +
                    $"instrs/line={k}, switches={k * nLines}, program-space≈{programSpace:G4} ...");
            }

            Stopwatch sw = Stopwatch.StartNew();
            long firstMs = -1;
            int found = 0;
            Status status = Status.UNKNOWN;
            while (found < 256 && (status = solver.Check()) == Status.SATISFIABLE)
            {
                Model model = solver.Model;
                List<BoolExpr> chosen = [];
                List<string> program = [];
                for (int line = 1; line <= nLines; ++line)
                {
                    foreach ((BoolExpr sw2, string asm) in switches[line])
                    {
                        if (model.Eval(sw2, true).IsTrue)
                        {
                            chosen.Add(sw2);
                            program.Add("  L" + line + ": " + asm.Replace('_', ' '));
                        }
                    }
                }
                found++;
                if (found == 1) firstMs = sw.ElapsedMilliseconds;
                if (logPrograms) AsmLog.Info("SIM", "program #" + found + ":\n" + string.Join(Environment.NewLine, program));
                solver.Assert(ctx.MkNot(ctx.MkAnd([.. chosen]))); // block this exact program; look for the next
            }
            sw.Stop();

            bool timedOut = status == Status.UNKNOWN;
            if (logPrograms)
            {
                AsmLog.Info("SIM", found == 0
                    ? $"No program of length {nLines} implements {spec}{(timedOut ? " (solver UNKNOWN/timeout)" : string.Empty)}."
                    : $"Done: {found} program(s) for {spec} — first in {firstMs} ms, total {sw.ElapsedMilliseconds} ms{(timedOut ? " (stopped: solver UNKNOWN/timeout)" : string.Empty)}.");
            }
            return new SynthResult(found, k, k * nLines, programSpace, firstMs, sw.ElapsedMilliseconds, timedOut);
        }

        /// <summary>
        /// Builds the candidate-instruction menu for one line from the <paramref name="allowed"/> families, and
        /// appends each candidate's guarded "switch ⇒ next-state" implication to <paramref name="transitions"/>.
        /// </summary>
        private static List<(BoolExpr sw, string asm)> BuildMenu(Context ctx, IList<Rn> registers, int line, SynthInstr allowed, List<BoolExpr> transitions)
        {
            List<(BoolExpr sw, string asm)> menu = [];

            foreach (Rn r in registers)
            {
                BitVecExpr r0 = GetReg(r, line - 1, ctx);
                uint nBits = (uint)RegisterTools.NBits(r);

                if (allowed.HasFlag(SynthInstr.Inc)) AddCandidate(ctx, menu, transitions, line, "INC_" + r, NextState(ctx, registers, r, ctx.MkBVAdd(r0, ctx.MkBV(1, nBits)), line));
                if (allowed.HasFlag(SynthInstr.Dec)) AddCandidate(ctx, menu, transitions, line, "DEC_" + r, NextState(ctx, registers, r, ctx.MkBVSub(r0, ctx.MkBV(1, nBits)), line));
                if (allowed.HasFlag(SynthInstr.Not)) AddCandidate(ctx, menu, transitions, line, "NOT_" + r, NextState(ctx, registers, r, ctx.MkBVNot(r0), line));
                if (allowed.HasFlag(SynthInstr.Neg)) AddCandidate(ctx, menu, transitions, line, "NEG_" + r, NextState(ctx, registers, r, ctx.MkBVNeg(r0), line));

                foreach (Rn r2 in registers)
                {
                    BitVecExpr r2v = GetReg(r2, line - 1, ctx);
                    if (allowed.HasFlag(SynthInstr.Xor)) AddCandidate(ctx, menu, transitions, line, "XOR_" + r + "_" + r2, NextState(ctx, registers, r, ctx.MkBVXOR(r0, r2v), line));
                    if (allowed.HasFlag(SynthInstr.And)) AddCandidate(ctx, menu, transitions, line, "AND_" + r + "_" + r2, NextState(ctx, registers, r, ctx.MkBVAND(r0, r2v), line));
                    if (allowed.HasFlag(SynthInstr.Or)) AddCandidate(ctx, menu, transitions, line, "OR_" + r + "_" + r2, NextState(ctx, registers, r, ctx.MkBVOR(r0, r2v), line));
                    if (allowed.HasFlag(SynthInstr.Add)) AddCandidate(ctx, menu, transitions, line, "ADD_" + r + "_" + r2, NextState(ctx, registers, r, ctx.MkBVAdd(r0, r2v), line));
                    if (allowed.HasFlag(SynthInstr.Sub)) AddCandidate(ctx, menu, transitions, line, "SUB_" + r + "_" + r2, NextState(ctx, registers, r, ctx.MkBVSub(r0, r2v), line));
                    if (allowed.HasFlag(SynthInstr.Mov)) AddCandidate(ctx, menu, transitions, line, "MOV_" + r + "_" + r2, NextState(ctx, registers, r, r2v, line));
                }
            }

            return menu;
        }

        private static void AddCandidate(Context ctx, List<(BoolExpr sw, string asm)> menu, List<BoolExpr> transitions, int line, string asm, BoolExpr nextState)
        {
            BoolExpr sw = ctx.MkBoolConst("L" + line + "_" + asm);
            menu.Add((sw, asm));
            transitions.Add(ctx.MkImplies(sw, nextState));
        }

        /// <summary>
        /// Full next-state relation for <paramref name="line"/>: <paramref name="written"/> takes
        /// <paramref name="newValue"/> and every other register is unchanged (<c>r!line == r!line-1</c>). Pass
        /// <paramref name="written"/> = <c>null</c> for NOP (all registers unchanged).
        /// </summary>
        private static BoolExpr NextState(Context ctx, IList<Rn> registers, Rn? written, BitVecExpr? newValue, int line)
        {
            List<BoolExpr> conj = [];
            foreach (Rn r in registers)
            {
                BitVecExpr next = (written.HasValue && r == written.Value) ? newValue! : GetReg(r, line - 1, ctx);
                conj.Add(ctx.MkEq(GetReg(r, line, ctx), next));
            }
            return ctx.MkAnd([.. conj]);
        }

        private static BoolExpr BuildTarget(Context ctx, SynthSpec spec, int nLines)
        {
            return spec switch
            {
                SynthSpec.ZeroRax => ctx.MkEq(GetReg(Rn.RAX, nLines, ctx), ctx.MkBV(0, 64)),
                SynthSpec.IncRax => ctx.MkEq(GetReg(Rn.RAX, nLines, ctx), ctx.MkBVAdd(GetReg(Rn.RAX, 0, ctx), ctx.MkBV(1, 64))),
                SynthSpec.NegRax => ctx.MkEq(GetReg(Rn.RAX, nLines, ctx), ctx.MkBVNeg(GetReg(Rn.RAX, 0, ctx))),
                SynthSpec.SwapRaxRbx => ctx.MkAnd(
                    ctx.MkEq(GetReg(Rn.RAX, nLines, ctx), GetReg(Rn.RBX, 0, ctx)),
                    ctx.MkEq(GetReg(Rn.RBX, nLines, ctx), GetReg(Rn.RAX, 0, ctx))),
                _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null),
            };
        }


        /// <summary>
        /// Investigates how solve time scales with the size of the search space. The spec and program length are
        /// held fixed (SwapRaxRbx, nLines=3, 4 registers); the only thing that grows is the menu of allowed
        /// instructions, which grows the candidates-per-line <c>k</c> and hence the program space <c>k^nLines</c>.
        /// XOR is kept in every set (the swap is unsolvable without it), so a solution always exists and we
        /// isolate "bigger space" from "harder/unsolvable". Prints one table row per instruction set.
        /// </summary>
        public static void RunSearchSpaceStudy()
        {
            Rn[] regs = new Rn[] { Rn.RAX, Rn.RBX, Rn.RCX, Rn.RDX };
            const int nLines = 3;

            // Cumulative: each step adds one more instruction family on top of the previous (XOR always present).
            (string label, SynthInstr set)[] steps =
            {
                ("Xor", SynthInstr.Xor),
                ("+Inc", SynthInstr.Xor | SynthInstr.Inc),
                ("+Dec", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec),
                ("+Not", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not),
                ("+Neg", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not | SynthInstr.Neg),
                ("+And", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not | SynthInstr.Neg | SynthInstr.And),
                ("+Or",  SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not | SynthInstr.Neg | SynthInstr.And | SynthInstr.Or),
                ("+Add", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not | SynthInstr.Neg | SynthInstr.And | SynthInstr.Or | SynthInstr.Add),
                ("+Sub", SynthInstr.Xor | SynthInstr.Inc | SynthInstr.Dec | SynthInstr.Not | SynthInstr.Neg | SynthInstr.And | SynthInstr.Or | SynthInstr.Add | SynthInstr.Sub),
                ("+Mov (All)", SynthInstr.All),
            };

            AsmLog.Info("SIM", "=== Search-space study: SwapRaxRbx, nLines=3, regs=[RAX,RBX,RCX,RDX] ===");
            AsmLog.Info("SIM", string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0,-12} {1,6} {2,8} {3,13} {4,8} {5,8} {6,6}",
                "menu", "k/line", "switches", "prog-space", "1st ms", "tot ms", "found"));
            foreach ((string label, SynthInstr set) in steps)
            {
                SynthResult r = ProgramSynthesizer.Synthesize(SynthSpec.SwapRaxRbx, nLines, regs, set, logPrograms: false);
                AsmLog.Info("SIM", string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0,-12} {1,6} {2,8} {3,13:G4} {4,8} {5,8} {6,6}",
                    label, r.InstrsPerLine, r.Switches, r.ProgramSpace, r.FirstMs, r.TotalMs, r.Found));
            }
        }
    }

    #endregion
}
