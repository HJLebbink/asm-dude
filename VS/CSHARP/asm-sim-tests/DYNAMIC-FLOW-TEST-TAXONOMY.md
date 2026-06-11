# DynamicFlow control-flow test taxonomy

Living coverage map for the **dynamic (DynamicFlow) engine** — the control-flow behavior the linear
single-step simulator cannot express, and the engine the editor will flip to (INCREMENTAL_SIM_PLAN.md).
Each scenario maps to its test (or is marked TODO / BUG) so feature coverage is tracked and nothing
silently slips. **Update this file whenever a control-flow test is added, fixed, or a gap is found.**

Status legend: ✅ covered · ⬜ TODO · 🐞 found-bug (test exists as spec, `[Ignore]`d until fixed)

Test homes: `Test_DynamicFlowSemantics.cs` (control-flow semantics), `Test_DynamicFlowComponent.cs`
(construction / multi-root / spikes), `Test_Runner.cs` (legacy fwd/bwd), all in **asm-sim-tests**.
Editor/shadow integration lives in `asm-dude2-ls-tests/LspAsmSimulatorTests.cs` (not here).

| ID | Scenario | Status | Test |
|----|----------|--------|------|
| **A. Sequencing** ||||
| A1 | Straight-line sequence (no branches) | ✅ | `Shadow_StraightLineProgram_EnginesAgree` (ls); `Test_Mnemonic` (linear) |
| **B. Unconditional jumps** ||||
| B1 | Forward `jmp` (skip code) | ✅ | exercised within E1/E2/D4 |
| B2 | Backward `jmp` / self-loop | ✅ | `Spike_JmpTerminatedComponent_PerLineStates` |
| B3 | `jmp`/`ret` terminal (no successor) | ✅ | `Spike_RetTerminatedComponent_PerLineStates` |
| **C. Conditional branch — KNOWN condition (one path dead)** ||||
| C1 | Known-true ⇒ always taken ⇒ **fall-through unreachable** | ✅ | `Component_DetectsUnreachableCode_AfterAlwaysTakenConditionalJump` |
| C2 | Known-false ⇒ never taken ⇒ **branch target unreachable** | ✅ | `KnownConditionFalse_BranchTargetIsUnreachable` |
| C3 | Dead code after an unconditional `jmp` (no label targets it) | ✅ | `DeadCodeAfterUnconditionalJump_NotReached` |
| **D. Conditional branch — UNKNOWN condition (merge)** ||||
| D1 | Merge, paths **agree** ⇒ value stays KNOWN | ✅ | `Merge_BranchOnUnknown_PathsAgree_ValueStaysKnown` |
| D2 | Merge, paths **disagree** ⇒ value UNKNOWN | ✅ | `Merge_BranchOnUnknown_PathsDisagree_ValueBecomesUnknown` |
| D3 | Merge with one path unreachable ⇒ reachable value kept | ✅ | `Component_MergeAtLabel_KeepsValueFromOnlyReachablePath` |
| D4 | **In-degree ≥ 3 join, jump-only (no fall-through pred)** | ✅ | `MultiPredecessorJoin_PreservesCommonValue` (was an NRE crash — FIXED 2026-06-11, see below) |
| D5 | In-degree ≥ 3 join WITH a fall-through predecessor | ✅ | `MultiPredecessorJoin_WithFallThrough_PreservesCommonValue` |
| D6 | A merged (non-concrete) value drives a later branch ⇒ both paths live | ✅ | `MergedUnknownValue_DrivesLiveBranch_BothPathsReachable` |
| **E. If/else structures** ||||
| E1 | Diamond (if/else/join): preserve common + merge divergent | ✅ | `Diamond_IfElse_PreservesCommonValue_AndMergesDivergentValue` |
| E2 | Forward `if` (no else): conditional modification | ✅ | `ForwardIf_ConditionalModification_MergesModifiedWithUnmodified` |
| E3 | Nested branches (3 paths converge) | ✅ | `NestedBranches_ThreePathsConverge_AgreementSurvives` |
| E4 | Chained diamonds — merges compose | ✅ | `ChainedDiamonds_ValuesComposeThroughTwoMerges` |
| **F. Loops** ||||
| F1 | Self-loop (`jmp self`) | ✅ | `Spike_JmpTerminatedComponent_PerLineStates` (characterization) |
| F2 | Backward conditional loop (counter) | ✅ | `BackwardLoop_Characterization` — single-pass ⇒ post-loop counter UNKNOWN (no fixpoint) |
| F3 | Nested loops | ✅ | `NestedLoops_ConstructionTerminates_ExitReachable` (terminates, exit reachable; counters unknown) |
| F4 | Loop with known fixed trip count (precise result) | ✅ | `BoundedLoop_FullUnroll_ComputesExactResult` (`FullUnroll` unrolls 4× ⇒ exact `rax=40`); summarizing strategies forget the body-written accumulator (`BoundedLoop_Summarizing_ForgetsTheAccumulator`) |
| **G. Multi-entry / structure** ||||
| G1 | Multi-entry component (two funcs, shared tail) | ✅ | `MultiRoot_CoversAllEntries_OfMultiEntryComponent` |
| G2 | `ret` terminal component | ✅ | `Spike_RetTerminatedComponent_PerLineStates` |
| **H. Flag / state propagation** ||||
| H1 | Flag agrees across both paths ⇒ KNOWN at merge | ✅ | `FlagPropagation_BothPathsSetSameFlag_KnownAtMerge` |
| H2 | Flag disagrees across paths ⇒ UNKNOWN at merge | ✅ | `FlagPropagation_PathsSetDifferentFlag_UnknownAtMerge` |
| H3 | Flag set before branch, preserved per-path | ✅ | `FlagPropagation_PreservedAcrossNonFlagBranch` (via `stc`+`jrcxz`) |
| **J. Hard / irreducible CFG** ||||
| J1 | Irreducible CFG (jump into a loop's middle ⇒ 2 loop entries) | ✅ | `IrreducibleCfg_JumpIntoLoopMiddle_TerminatesAndExitReachable` (terminates, exit reachable) |
| J2 | Computed/indirect jump (jump table) | ⬜ | likely unsupported — verify behavior |
| **K. Merge precision / Tv classification & parsing** ||||
| K1 | Merge of defined concretes ⇒ UNKNOWN (not UNDEFINED) | ✅ | `Merge_OfConcreteValues_ShouldBeUnknownNotUndefined` |
| K2 | Label name (substring of a mnemonic/register, or local `.loop`) must not corrupt instructions | ✅ | `LabelName_SubstringOfMnemonicOrRegister_DoesNotCorruptInstructions` |
| **I. Instruction coverage** (linear suite, separate effort) ||||
| I1 | Per-instruction semantics | ⬜ | `Test_Mnemonic` covers ~41 of ~325 mnemonics — big gap: all `Jcc` (except JC/JNZ/JZ), all `SETcc`, all `CMOVcc` (except CMOVE/CMOVZ), all string/`REP`, and `AND/OR/NOT/TEST/SHR/SAR/ROL/ROR/RET/JMP/CALL/MOVSX/MOVZX/ADC/SBB` |

## Known limitation
- **Local-label scoping:** `StaticFlow.GetLabels` keys labels by bare name, so the same local label
  (e.g. NASM `.loop`) reused across functions clashes and mis-resolves (a warning is logged). See the
  `GetLabels` remark in `StaticFlow.cs`.

## Notes
- These are CI correctness tests; **slow is fine** (real merges + Z3), they don't run in the edit loop.
- Coverage is currently inverted vs. the flip's needs: linear is heavily tested (`Test_Mnemonic`, 119
  methods), dynamic is light — this suite is closing that gap.
