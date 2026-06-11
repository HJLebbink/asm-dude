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

namespace unit_tests_asm_z3
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AsmSim;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Control-flow semantics of the DYNAMIC (DynamicFlow) engine — the behavior the linear single-step
    /// simulator cannot express, and which the editor hits on every real branch. These verify the merge
    /// at a join, the reason the dynamic engine exists. Slow is fine (real merges + Z3); these are CI
    /// correctness tests, not edit-loop tests.
    /// </summary>
    [TestClass]
    public class Test_DynamicFlowSemantics
    {
        private static Tools CreateTools(int timeOut = 60000)
        {
            Dictionary<string, string> settings = new()
            {
                { "unsat-core", "false" },
                { "model", "false" },
                { "proof", "false" },
                { "timeout", timeOut.ToString(CultureInfo.InvariantCulture) },
            };
            return new Tools(settings);
        }

        /// <summary>Build a forward DynamicFlow for a program (state config derived from the program).</summary>
        private static DynamicFlow BuildForward(string programStr)
        {
            Tools tools = CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();
            return Runner.Construct_DynamicFlow_Forward(sFlow, tools);
        }

        // True if the register is NOT a single concrete value, i.e. some bit is neither 0 nor 1 — a
        // merge result that lost precision. Covers both Tv.UNKNOWN ('?') and Tv.UNDEFINED ('U'); the sim
        // may use either for a merged-divergent value (e.g. {1,2} reads '?', {0,1} reads 'U').
        private static bool HasNonConcreteBit(State state, Rn reg)
        {
            foreach (Tv t in state.GetTvArray(reg))
            {
                if (t != Tv.ZERO && t != Tv.ONE) return true;
            }
            return false;
        }

        [TestMethod]
        public void Merge_BranchOnUnknown_PathsAgree_ValueStaysKnown()
        {
            // cmp on UNKNOWN regs ⇒ the jz can go either way ⇒ BOTH paths reachable. Both arms set rcx=5,
            // so the merge at the join must KEEP rcx=5 (merge preserves agreement). This is the canonical
            // "the dynamic engine does NOT needlessly lose a value that both paths agree on".
            string programStr =
                "          cmp rax, rbx     " + Environment.NewLine +   // 0
                "          jz agree         " + Environment.NewLine +   // 1
                "          mov rcx, 5       " + Environment.NewLine +   // 2  path A
                "          jmp join         " + Environment.NewLine +   // 3
                "agree:    mov rcx, 5       " + Environment.NewLine +   // 4  path B
                "join:     mov rdx, rcx     ";                          // 5  the merge point

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(beforeJoin, "the join is reachable");
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable (both paths live)");
            AsmTestTools.AreEqual(Rn.RCX, 5, beforeJoin); // both arms agree ⇒ rcx=5 survives the merge
        }

        [TestMethod]
        public void Merge_BranchOnUnknown_PathsDisagree_ValueBecomesUnknown()
        {
            // Same diamond, but the arms set DIFFERENT values (1 vs 2). The merge must make rcx UNKNOWN —
            // the canonical "merge loses precision when paths disagree".
            string programStr =
                "          cmp rax, rbx     " + Environment.NewLine +   // 0
                "          jz two           " + Environment.NewLine +   // 1
                "          mov rcx, 1       " + Environment.NewLine +   // 2  path A
                "          jmp join         " + Environment.NewLine +   // 3
                "two:      mov rcx, 2       " + Environment.NewLine +   // 4  path B
                "join:     mov rdx, rcx     ";                          // 5

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(beforeJoin, "the join is reachable");
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable (both paths live)");
            // Merge of {1,2}: 1=...01, 2=...10 differ in bits 0 and 1 ⇒ both UNKNOWN; bits 2..63 are 0 on
            // both paths ⇒ ZERO. Concrete check, strictly tighter than HasNonConcreteBit (a regression to
            // UNDEFINED here would FAIL — the broad predicate would not).
            AsmTestTools.AreEqual(Rn.RCX, new string('0', 62) + "??", beforeJoin);
        }

        [TestMethod]
        public void Diamond_IfElse_PreservesCommonValue_AndMergesDivergentValue()
        {
            // A full if/else diamond. rsi is set BEFORE the branch (common to both arms) ⇒ preserved
            // through the merge. rdi is set differently per arm ⇒ unknown after the merge. One structure
            // exercises BOTH merge behaviors at once.
            string programStr =
                "          mov rsi, 7       " + Environment.NewLine +   // 0  common (pre-branch)
                "          cmp rax, 0       " + Environment.NewLine +   // 1
                "          jz elsx          " + Environment.NewLine +   // 2
                "          mov rdi, 1       " + Environment.NewLine +   // 3  then
                "          jmp endx         " + Environment.NewLine +   // 4
                "elsx:     mov rdi, 2       " + Environment.NewLine +   // 5  else
                "endx:     mov rax, rdi     ";                          // 6  join

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeEnd = dFlow.Create_States_Before(6, 0);

            Assert.IsNotNull(beforeEnd, "the join is reachable");
            Assert.AreNotEqual(Tv.ZERO, beforeEnd.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Rn.RSI, 7, beforeEnd);                       // common value preserved
            // rdi = merge{1,2}: bits 0,1 differ ⇒ UNKNOWN, bits 2..63 agree at 0 ⇒ ZERO.
            AsmTestTools.AreEqual(Rn.RDI, new string('0', 62) + "??", beforeEnd);
        }

        [TestMethod]
        public void BackwardLoop_Characterization()
        {
            // A real loop with a BACKWARD conditional jump. DynamicFlow does a single-pass merge (no
            // fixpoint), so this CHARACTERIZES what the engine actually computes for the loop counter
            // after the loop — the value is encoded from the observed result, not assumed.
            string programStr =
                "          mov rcx, 3       " + Environment.NewLine +   // 0  counter
                "lbl:      dec rcx          " + Environment.NewLine +   // 1  loop body
                "          jnz lbl          " + Environment.NewLine +   // 2  back-edge while rcx != 0
                "          mov rax, rcx     ";                          // 3  after the loop

            using DynamicFlow dFlow = BuildForward(programStr);

            // The loop must terminate construction (Has_Edge guard) and line 3 must be reachable.
            State? before3 = dFlow.Create_States_Before(3, 0);
            Assert.IsNotNull(before3, "construction terminated and the post-loop line is reachable");

            // Observe the loop-exit value of rcx. The exit edge (jnz not taken) carries rcx==0, so the
            // engine MAY recover rcx=0 even without a fixpoint — or it may be unknown. Print + assert
            // the actual so the behavior is locked and visible.
            bool rcxUnknown = HasNonConcreteBit(before3, Rn.RCX);
            Console.WriteLine($"[LOOP] post-loop rcx unknown? {rcxUnknown}; consistent={before3.IsConsistent}");
            Console.WriteLine($"[LOOP] post-loop rcx = {ToolsZ3.ToStringBin(before3.GetTvArray(Rn.RCX))}");
            Assert.AreNotEqual(Tv.ZERO, before3.IsConsistent, "the loop exit is reachable");

            // CHARACTERIZED LIMITATION (single-pass merge, no fixpoint): the post-loop counter is fully
            // UNKNOWN — logically rcx=0, but the merge loses it and the exit condition rcx==0 does NOT
            // recover it. Sound (over-approximation) but imprecise. If a future fixpoint analysis makes
            // this precise (rcx=0), this assertion fails ON PURPOSE → revisit with review.
            Assert.IsTrue(rcxUnknown, "single-pass merge yields an UNKNOWN post-loop counter (no fixpoint)");
        }

        // ── Taxonomy E2: forward `if` (no else) — conditional modification ──────────────────────────
        [TestMethod]
        public void ForwardIf_ConditionalModification_MergesModifiedWithUnmodified()
        {
            // `cmp; jz skip; <body>; skip:` — the body runs only on the not-taken path. At the join, a
            // register the body writes is the merge of {modified, unmodified} ⇒ unknown; a register the
            // body never touches stays known. This is the "if without else" pattern.
            string programStr =
                "          mov rbx, 9       " + Environment.NewLine +   // 0  rbx=9 (pre-branch)
                "          mov rdx, 5       " + Environment.NewLine +   // 1  rdx=5 (body never touches it)
                "          cmp rax, 0       " + Environment.NewLine +   // 2
                "          jz skip          " + Environment.NewLine +   // 3  skip the body when taken
                "          mov rbx, 1       " + Environment.NewLine +   // 4  body: conditionally writes rbx
                "skip:     mov rcx, rbx     ";                          // 5  join

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(beforeJoin);
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Rn.RDX, 5, beforeJoin);                       // untouched by the body ⇒ known
            // rbx = merge{9,1}: 9=...1001, 1=...0001 differ only in bit 3 ⇒ that bit UNKNOWN; bit 0 = 1 on
            // both, bits 1,2 and 4..63 = 0 on both.
            AsmTestTools.AreEqual(Rn.RBX, new string('0', 60) + "?001", beforeJoin);
        }

        // ── Taxonomy C2: known-condition ⇒ the NOT-taken branch target is unreachable ───────────────
        [TestMethod]
        public void KnownConditionFalse_BranchTargetIsUnreachable()
        {
            // Mirror of the always-taken case: al=5, cmp al,8 ⇒ ZF=0, so `jz dead` is NEVER taken, and
            // `dead:` (reachable ONLY via that jump) is unreachable. Component must detect it inconsistent.
            string programStr =
                "          mov al, 5        " + Environment.NewLine +   // 0
                "          cmp al, 8        " + Environment.NewLine +   // 1  -> ZF=0
                "          jz dead          " + Environment.NewLine +   // 2  -> never taken
                "          mov rbx, 1       " + Environment.NewLine +   // 3  reachable
                "          jmp end          " + Environment.NewLine +   // 4
                "dead:     mov rbx, 2       " + Environment.NewLine +   // 5  UNREACHABLE (only via never-taken jz)
                "end:      mov rcx, rbx     ";                          // 6

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeDead = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(beforeDead, "the never-taken branch edge still builds a vertex");
            Assert.AreEqual(Tv.ZERO, beforeDead.IsConsistent, "the never-taken branch target is UNREACHABLE");
        }

        // ── Taxonomy D4: in-degree >= 3 join (multi-predecessor merge) ──────────────────────────────
        // FIXED 2026-06-11: a join reached ONLY by branch/jump edges (no fall-through predecessor) used to
        // NRE in DynamicFlow.Merge_State_Update_LOCAL (null incoming_Regular.stateUpdate). The merge now
        // promotes the first branch to be the base when there is no regular edge. This test is the gate.
        [TestMethod]
        public void MultiPredecessorJoin_PreservesCommonValue()
        {
            // Three distinct jumps converge on one label (in-degree 3). A value set before all of them
            // (rsi=4) is common to every predecessor and must survive the 3-way merge.
            string programStr =
                "          mov rsi, 4       " + Environment.NewLine +   // 0  common
                "          cmp rax, 0       " + Environment.NewLine +   // 1
                "          jz target        " + Environment.NewLine +   // 2  jump 1
                "          cmp rbx, 0       " + Environment.NewLine +   // 3
                "          jz target        " + Environment.NewLine +   // 4  jump 2
                "          jmp target       " + Environment.NewLine +   // 5  jump 3 (unconditional)
                "target:   mov rdx, rsi     ";                          // 6  the >=3-predecessor join

            Tools tools = CreateTools();
            StaticFlow sFlow = new(tools);
            sFlow.Update(programStr, removeEmptyLines: false);
            tools.StateConfig = sFlow.Create_StateConfig();

            int preds = 0;
            foreach (var _ in sFlow.Get_Prev_LineNumber(6)) preds++;
            Assert.IsTrue(preds >= 3, $"target must have >=3 predecessors (had {preds})");

            using DynamicFlow dFlow = Runner.Construct_DynamicFlow_Forward(sFlow, tools);
            State? beforeTarget = dFlow.Create_States_Before(6, 0);

            Assert.IsNotNull(beforeTarget);
            Assert.AreNotEqual(Tv.ZERO, beforeTarget.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Rn.RSI, 4, beforeTarget); // common to all 3 predecessors ⇒ survives
        }

        // ── Taxonomy E3: nested branches ────────────────────────────────────────────────────────────
        [TestMethod]
        public void NestedBranches_ThreePathsConverge_AgreementSurvives()
        {
            // A branch nested inside a branch ⇒ three converging paths. All set rdi=5, so the (nested)
            // merge must yield rdi=5 — the nesting must not break agreement; a pre-branch rsi=8 survives.
            string programStr =
                "          mov rsi, 8       " + Environment.NewLine +   // 0  common
                "          cmp rax, 0       " + Environment.NewLine +   // 1
                "          jz oelse         " + Environment.NewLine +   // 2  outer branch
                "          cmp rbx, 0       " + Environment.NewLine +   // 3  inner branch (in outer-then)
                "          jz ielse         " + Environment.NewLine +   // 4
                "          mov rdi, 5       " + Environment.NewLine +   // 5  path 1
                "          jmp end          " + Environment.NewLine +   // 6
                "ielse:    mov rdi, 5       " + Environment.NewLine +   // 7  path 2
                "          jmp end          " + Environment.NewLine +   // 8
                "oelse:    mov rdi, 5       " + Environment.NewLine +   // 9  path 3
                "end:      mov rax, rdi     ";                          // 10 join of all three

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeEnd = dFlow.Create_States_Before(10, 0);

            Assert.IsNotNull(beforeEnd);
            Assert.AreNotEqual(Tv.ZERO, beforeEnd.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Rn.RDI, 5, beforeEnd); // all 3 nested paths agree ⇒ rdi=5
            AsmTestTools.AreEqual(Rn.RSI, 8, beforeEnd); // pre-branch value survives the nesting
        }

        // ── Taxonomy H1: flag propagation across a branch/merge ─────────────────────────────────────
        [TestMethod]
        public void FlagPropagation_BothPathsSetSameFlag_KnownAtMerge()
        {
            // Both arms execute `cmp reg,reg` ⇒ ZF=1 regardless of the register's (unknown) value. The
            // merge must keep ZF=1 — flags merge by the same agree/disagree rule as registers.
            string programStr =
                "          cmp rax, rbx     " + Environment.NewLine +   // 0  unknown condition
                "          jz pb            " + Environment.NewLine +   // 1
                "          cmp rcx, rcx     " + Environment.NewLine +   // 2  path A: ZF=1
                "          jmp join         " + Environment.NewLine +   // 3
                "pb:       cmp rdx, rdx     " + Environment.NewLine +   // 4  path B: ZF=1
                "join:     mov rsi, 1       ";                          // 5

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(beforeJoin);
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Flags.ZF, Tv.ONE, beforeJoin); // both paths ZF=1 ⇒ ZF=1 at the merge
        }

        // ── Taxonomy D5: in-degree >= 3 join WITH a fall-through predecessor ─────────────────────────
        [TestMethod]
        public void MultiPredecessorJoin_WithFallThrough_PreservesCommonValue()
        {
            // The complement to D4: a >=3 join that DOES have a regular (fall-through) predecessor — the
            // normal multi-branch+regular merge path. rsi=9 is common to all 3 predecessors ⇒ survives.
            string programStr =
                "          mov rsi, 9       " + Environment.NewLine +   // 0  common
                "          cmp rax, 0       " + Environment.NewLine +   // 1
                "          jz target        " + Environment.NewLine +   // 2  jump 1
                "          cmp rbx, 0       " + Environment.NewLine +   // 3
                "          jz target        " + Environment.NewLine +   // 4  jump 2
                "          mov rdx, 1       " + Environment.NewLine +   // 5  fall-through (3rd pred, regular)
                "target:   mov rcx, rsi     ";                          // 6

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeTarget = dFlow.Create_States_Before(6, 0);

            Assert.IsNotNull(beforeTarget);
            Assert.AreNotEqual(Tv.ZERO, beforeTarget.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Rn.RSI, 9, beforeTarget);
        }

        // ── Taxonomy H2: flag disagrees across paths ⇒ unknown at merge ─────────────────────────────
        [TestMethod]
        public void FlagPropagation_PathsSetDifferentFlag_UnknownAtMerge()
        {
            // Path A ⇒ ZF=1, path B ⇒ ZF=0. The merge must make ZF UNKNOWN (flags follow the same
            // agree/disagree rule as registers).
            string programStr =
                "          cmp rax, rbx     " + Environment.NewLine +   // 0  unknown condition
                "          jz pb            " + Environment.NewLine +   // 1
                "          mov r8, 5        " + Environment.NewLine +   // 2  path A
                "          cmp r8, 5        " + Environment.NewLine +   // 3  ZF=1
                "          jmp join         " + Environment.NewLine +   // 4
                "pb:       mov r8, 5        " + Environment.NewLine +   // 5  path B
                "          cmp r8, 6        " + Environment.NewLine +   // 6  ZF=0
                "join:     mov rsi, 1       ";                          // 7

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(7, 0);

            Assert.IsNotNull(beforeJoin);
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Flags.ZF, Tv.UNKNOWN, beforeJoin); // 1 vs 0 ⇒ unknown
        }

        // ── Taxonomy H3: flag set before a branch is preserved (branch doesn't clobber flags) ───────
        [TestMethod]
        public void FlagPropagation_PreservedAcrossNonFlagBranch()
        {
            // jrcxz branches on RCX (not on a flag) and modifies no flags; mov modifies none either. So
            // CF set by stc BEFORE the branch is still CF=1 at the join on both paths.
            string programStr =
                "          stc              " + Environment.NewLine +   // 0  CF=1
                "          jrcxz skip       " + Environment.NewLine +   // 1  branch on rcx (unknown) — no flag clobber
                "          mov rax, 1       " + Environment.NewLine +   // 2  body — no flag clobber
                "skip:     mov rbx, 2       ";                          // 3  join

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeJoin = dFlow.Create_States_Before(3, 0);

            Assert.IsNotNull(beforeJoin);
            Assert.AreNotEqual(Tv.ZERO, beforeJoin.IsConsistent, "the join is reachable");
            AsmTestTools.AreEqual(Flags.CF, Tv.ONE, beforeJoin); // CF=1 set before branch, untouched by both paths
        }

        // ── Taxonomy F3: nested loops ───────────────────────────────────────────────────────────────
        [TestMethod]
        public void NestedLoops_ConstructionTerminates_ExitReachable()
        {
            // Two nested loops with back-edges. Construction must terminate (Has_Edge guard) and the
            // post-loop line must be reachable — a stress test for the graph build + nested merges.
            // Counters are unknown (single-pass merge, see F2); this characterizes no-crash/no-hang.
            string programStr =
                "          mov rcx, 2       " + Environment.NewLine +   // 0  outer counter
                "outer:    mov rdx, 2       " + Environment.NewLine +   // 1  inner counter
                "inner:    dec rdx          " + Environment.NewLine +   // 2  inner body
                "          jnz inner        " + Environment.NewLine +   // 3  inner back-edge
                "          dec rcx          " + Environment.NewLine +   // 4
                "          jnz outer        " + Environment.NewLine +   // 5  outer back-edge
                "          mov rax, rcx     ";                          // 6  after both loops

            using DynamicFlow dFlow = BuildForward(programStr);
            State? before6 = dFlow.Create_States_Before(6, 0);

            Assert.IsNotNull(before6, "nested-loop construction terminated and the exit is reachable");
            Assert.AreNotEqual(Tv.ZERO, before6.IsConsistent, "the loop exit is reachable");
        }

        // ── Taxonomy C3: dead code after an unconditional jmp (no label targets it) ──────────────────
        [TestMethod]
        public void DeadCodeAfterUnconditionalJump_NotReached()
        {
            // `jmp done` has no fall-through and nothing targets line 2, so it is dead. The forward flow
            // must not reach it; the live path carries rax=1 into `done`.
            string programStr =
                "          mov rax, 1       " + Environment.NewLine +   // 0
                "          jmp done         " + Environment.NewLine +   // 1
                "          mov rax, 2       " + Environment.NewLine +   // 2  DEAD
                "done:     mov rbx, rax     ";                          // 3

            using DynamicFlow dFlow = BuildForward(programStr);

            State? beforeDead = dFlow.Create_States_Before(2, 0);
            bool dead = beforeDead is null || beforeDead.IsConsistent == Tv.ZERO;
            Assert.IsTrue(dead, "code after an unconditional jmp (no label) must be unreachable");

            State? beforeDone = dFlow.Create_States_Before(3, 0);
            Assert.IsNotNull(beforeDone);
            AsmTestTools.AreEqual(Rn.RAX, 1, beforeDone); // only the live path (rax=1) reaches done
        }

        // ── Taxonomy E4: chained diamonds — merges compose ──────────────────────────────────────────
        [TestMethod]
        public void ChainedDiamonds_ValuesComposeThroughTwoMerges()
        {
            // Two sequential if/else diamonds. A pre-branch value (rsi) must survive BOTH merges, and a
            // value each diamond agrees on must be known after its merge — verifying merge-of-merge.
            string programStr =
                "          mov rsi, 3       " + Environment.NewLine +   // 0  common
                "          cmp rax, 0       " + Environment.NewLine +   // 1  diamond 1
                "          jz e1            " + Environment.NewLine +   // 2
                "          mov rdi, 1       " + Environment.NewLine +   // 3
                "          jmp m1           " + Environment.NewLine +   // 4
                "e1:       mov rdi, 1       " + Environment.NewLine +   // 5  (agrees: rdi=1)
                "m1:       cmp rbx, 0       " + Environment.NewLine +   // 6  diamond 2
                "          jz e2            " + Environment.NewLine +   // 7
                "          mov r8, 2        " + Environment.NewLine +   // 8
                "          jmp m2           " + Environment.NewLine +   // 9
                "e2:       mov r8, 2        " + Environment.NewLine +   // 10 (agrees: r8=2)
                "m2:       mov rax, rdi     ";                          // 11 final join

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeEnd = dFlow.Create_States_Before(11, 0);

            Assert.IsNotNull(beforeEnd);
            Assert.AreNotEqual(Tv.ZERO, beforeEnd.IsConsistent, "the final join is reachable");
            AsmTestTools.AreEqual(Rn.RSI, 3, beforeEnd); // survives both diamonds
            AsmTestTools.AreEqual(Rn.RDI, 1, beforeEnd); // diamond 1 agreed
            AsmTestTools.AreEqual(Rn.R8, 2, beforeEnd);  // diamond 2 agreed
        }

        // ── Taxonomy D6: a merged (unknown) value drives a later branch ─────────────────────────────
        [TestMethod]
        public void MergedUnknownValue_DrivesLiveBranch_BothPathsReachable()
        {
            // rbx merges to {0,1} (unknown). `cmp rbx,0; jz` then has an UNKNOWN condition, so BOTH the
            // taken and not-taken paths must be reachable — proving the merge kept rbx unknown and fed it
            // into the branch (if the merge had wrongly collapsed rbx, one path would be dead).
            string programStr =
                "          cmp rax, 0       " + Environment.NewLine +   // 0
                "          jz a1            " + Environment.NewLine +   // 1
                "          mov rbx, 0       " + Environment.NewLine +   // 2  path A: rbx=0
                "          jmp m            " + Environment.NewLine +   // 3
                "a1:       mov rbx, 1       " + Environment.NewLine +   // 4  path B: rbx=1
                "m:        cmp rbx, 0       " + Environment.NewLine +   // 5  rbx={0,1} ⇒ ZF unknown
                "          jz b1            " + Environment.NewLine +   // 6
                "          mov rcx, 7       " + Environment.NewLine +   // 7  not-taken path (must be live)
                "b1:       mov rdx, 8       ";                          // 8  taken path

            using DynamicFlow dFlow = BuildForward(programStr);

            State? beforeCmp = dFlow.Create_States_Before(5, 0);
            Assert.IsNotNull(beforeCmp);
            // rbx = merge{0,1}: bit 0 differs ⇒ UNKNOWN, bits 1..63 agree at 0 ⇒ ZERO. Concrete check.
            AsmTestTools.AreEqual(Rn.RBX, new string('0', 63) + "?", beforeCmp);

            State? beforeNotTaken = dFlow.Create_States_Before(7, 0);
            Assert.IsNotNull(beforeNotTaken);
            Assert.AreNotEqual(Tv.ZERO, beforeNotTaken.IsConsistent,
                "the not-taken path is live ⇒ the merged-unknown value drove a real branch");
        }

        // ── Taxonomy J1: irreducible CFG — jump into the middle of a loop (two loop entries) ─────────
        [TestMethod]
        public void IrreducibleCfg_JumpIntoLoopMiddle_TerminatesAndExitReachable()
        {
            // line 0 jumps INTO the loop at `mid`, while the back-edge targets `top` (above mid) — two
            // entries into the loop body (classic irreducible CFG). Construction must terminate and the
            // exit must be reachable (stress test; no crash / no hang).
            string programStr =
                "          jmp mid          " + Environment.NewLine +   // 0  jump into the loop
                "top:      mov rax, 1       " + Environment.NewLine +   // 1  loop entry via back-edge
                "mid:      mov rbx, 2       " + Environment.NewLine +   // 2  loop entry via jmp
                "          cmp rcx, 0       " + Environment.NewLine +   // 3
                "          jnz top          " + Environment.NewLine +   // 4  back-edge to top
                "          mov rdx, rcx     ";                          // 5  exit

            using DynamicFlow dFlow = BuildForward(programStr);
            State? before5 = dFlow.Create_States_Before(5, 0);

            Assert.IsNotNull(before5, "irreducible-CFG construction terminated and the exit is reachable");
            Assert.AreNotEqual(Tv.ZERO, before5.IsConsistent, "the exit is reachable");
        }

        // ── Taxonomy K1: merge of two defined concretes is UNKNOWN, not UNDEFINED ────────────────────
        // At the join rbx is 0 (path A) or 1 (path B) per the untracked runtime branch ⇒ a defined-but-
        // untracked value ⇒ Tv.UNKNOWN on the differing bit; nothing is architecturally UNDEFINED.
        [TestMethod]
        public void Merge_OfConcreteValues_ShouldBeUnknownNotUndefined()
        {
            string programStr =
                "          cmp rax, 0       " + Environment.NewLine +   // 0  unknown condition
                "          jz a1            " + Environment.NewLine +   // 1
                "          mov rbx, 0       " + Environment.NewLine +   // 2  path A: rbx=0 (fully defined)
                "          jmp m            " + Environment.NewLine +   // 3
                "a1:       mov rbx, 1       " + Environment.NewLine +   // 4  path B: rbx=1 (fully defined)
                "m:        mov rcx, rbx     ";                          // 5  merge; rbx must be 0...0?, not undefined

            using DynamicFlow dFlow = BuildForward(programStr);
            State? beforeMerge = dFlow.Create_States_Before(5, 0);
            Assert.IsNotNull(beforeMerge);

            // Expected RBX at the merge: bit 0 differs across the two paths (0 vs 1) => a defined-but-
            // untracked runtime value => Tv.UNKNOWN ('?'); bits 1..63 are 0 on BOTH paths => Tv.ZERO.
            // Nothing is architecturally UNDEFINED ('U') -- `mov imm` is fully defined.
            string expected = new string('0', 63) + "?";   // 0000_..._000?  (63 zeros, low bit unknown)
            AsmTestTools.AreEqual(Rn.RBX, expected, beforeMerge);
        }

        // ── Taxonomy K2: a label name must not collide with instruction tokens ───────────────────────
        // Label annotation is token-level, so a label whose name is a substring of a mnemonic/register
        // ("m" in mov/cmp/jmp, "x" in rax/rcx, "c" in cmp/rcx) — and a NASM local label (.loop) — must
        // simulate identically to a safe label: the {1,2} merge of rcx is 0...0?? for every label name.
        [TestMethod]
        public void LabelName_SubstringOfMnemonicOrRegister_DoesNotCorruptInstructions()
        {
            string expected = new string('0', 62) + "??"; // {1,2} merge: bits 0,1 unknown, rest zero
            foreach (string join in new[] { "m", "x", "c", "mm", ".loop", "safe1" })
            {
                string prog =
                    "          cmp rax, 0       " + Environment.NewLine +   // 0
                    "          jz t1            " + Environment.NewLine +   // 1
                    "          mov rcx, 1       " + Environment.NewLine +   // 2
                    "          jmp " + join + "         " + Environment.NewLine + // 3
                    "t1:       mov rcx, 2       " + Environment.NewLine +   // 4
                    join + ":     mov rdx, rcx     ";                       // 5  merge of rcx={1,2}

                using DynamicFlow dFlow = BuildForward(prog);
                State? beforeMerge = dFlow.Create_States_Before(5, 0);
                Assert.IsNotNull(beforeMerge, $"join label '{join}': the merge state must exist");
                AsmTestTools.AreEqual(Rn.RCX, expected, beforeMerge);
            }
        }
    }
}
