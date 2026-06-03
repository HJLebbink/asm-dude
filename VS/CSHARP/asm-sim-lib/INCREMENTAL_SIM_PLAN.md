# Plan: Shared-Context Rewrite + Incremental Simulation

**Status:** approved design, not yet implemented (2026-06-03).
**Decisions:** per-CFG-component Z3 contexts (one context per function/cluster) + the **DynamicFlow** (CFG/graph) engine.
**Supersedes:** the per-object-context model. This is "Option B" from `Z3_CONTEXT_LIFECYCLE_BUG.md`, extended to incremental.

## Why one rewrite fixes three things

The branch-merge **crash**, the 18 GiB **leak**, and the missing **incrementality** all have one root cause: **a separate Z3 `Context` per `State`/`StateUpdate`** (per line). A Z3 `Context` is a memory/namespace arena, not a logical scope — many independent solvers and unrelated assertion sets coexist in one context using disjoint key names. So:
- **Crash** disappears: within one shared context there is no cross-context `BranchInfo.Translate` (the AV site in `Merge_State_Update_LOCAL`).
- **Leak** disappears: N states share 1 context instead of N contexts (the `Context` is the heavy object; the `State` is cheap).
- **Incremental** becomes possible: cache each component's context + result; recompute only the components an edit touches.

## Granularity: one context per CFG component (function/cluster)

- "Unconnected clusters / separate functions" = weakly-connected components of the `StaticFlow` CFG.
- Each component gets its **own** Z3 `Context` + `State` chain + cached per-line strings.
- Editing one component rebuilds only that component's context; **other components stay alive and untouched** — this is the "keep the not-touched Z3 state alive" intent, bounded per function (affordable) instead of per line (the leak).
- Whole-file single context was rejected (coarse: structural edits rebuild everything). Per-line push/pop is a possible later refinement *within* a component.

## Phases

**Phase 0 — Audit & decide. ✅ DONE (2026-06-03).**

*Context-creation sites (each must change to "receive the component's shared context" instead of `new Context(...)`):*
- `State.cs:81` — `State.ctx_` (private ctor `State(Tools)`; all public ctors delegate here).
- `StateUpdate.cs:137` and `:149` — `StateUpdate.ctx_` (two ctors).
- `Mnemonics.cs:98` — `OpcodeBase.ctx_` (every opcode instance).
- (`ProgramSyntesizer.cs:55` — separate tool, NOT in the sim hot path; out of scope.)

*Cross-context `Translate` sites (all "translate an incoming expr into my `ctx_`" → become identity/removable once a component shares one context; this is what removes the AV):*
- `State.cs` ×6 (228, 233, 244, 249, 315), `StateUpdate.cs` ×10 (150, 456, 457, 512, 653, 654, 959, 960, 961, 989), `BranchInfo.cs` (43), `BranchInfoStore.cs:211`.

*Feasibility notes:*
- `State.Ctx` is already a public property → threading a shared context out/in is straightforward.
- Disposal: the shared context must be owned by the per-component driver and disposed **once**; `State`/`StateUpdate`/`OpcodeBase.Dispose()` must NOT dispose a borrowed context → add an `ownsContext` flag or a "borrowed-context" ctor.

*CFG partitioning (DONE):* added `StaticFlow.ComputeLineToComponent()` — union-find over CFG vertices → `line → componentId` (smallest line in the component). Test `Test_StaticFlow_ConnectedComponents_SeparateFunctions` proves two unconnected functions get distinct components. Full sim suite green: 153 passed / 0 failed / 28 skipped.

**Phase 1 — Shared context (root fix). ✅ DONE (2026-06-03).** The Z3 context-lifecycle crash is fixed: `Tools.SharedCtx` carries one context per simulation unit; `State`/`StateUpdate`/`OpcodeBase` borrow it (guarded by `ownsCtx_`); `DynamicFlow` owns one context (set on its `Tools`) and disposes it last; the merge block builds the branch condition in that shared context. The 25 skipped DynamicFlow tests are re-enabled and pass — `asm-sim-tests` 178/0/3, `asm-dude2-ls-tests` 120/6/37, full solution builds. NOTE: the context is currently per-`DynamicFlow` (whole flow), not yet per-CFG-component — that refinement is Phases 2–3 below.

*State-layer landed (2026-06-03, lifted from branch `asmsim-state-refactor`, which is now fully mined and can be deleted):*
- `State` got an `ownsCtx_` flag, a borrowed-context private ctor `State(Context, Tools)`, a public `State(Context, Tools, tail, head)`, the copy ctor now **shares** the source's context (and `Copy()` skips Z3 `Translate` when `ReferenceEquals` the contexts), and `Dispose()` only disposes the context when `ownsCtx_`.
- `LspAsmSimulator.DisposeList` now disposes in **reverse** order so the context-owning root state is disposed last (pairs with the copy-ctor sharing).
- Validated: `asm-sim-tests` 153/0/28, `asm-dude2-ls-tests` 120/6/37 — no regression. (BranchInfo/BranchInfoStore null-checks were already on dotnet10; branch's nullable-`Operand?` and configurable-debounce were skipped as non-essential.)

*Still TODO in Phase 1:* extend the borrowed-context pattern to `StateUpdate` and `OpcodeBase`; change the **merge ctor** `State(s1, s2, merge)` to share `s1`'s context; wire `DynamicFlow`/`Runner` to create ONE context per CFG component and pass it everywhere; delete the now-redundant cross-context `Translate` calls. **Gate: the 28 `[Ignore]`d DynamicFlow tests must pass.**

Original Phase 1 outline:
- Thread one owned `Context` per simulation unit through `State`/`StateUpdate`/`Runner` instead of `new Context(...)`. `Tools` carries (or is passed) the active context.
- Delete the cross-context `Translate` calls in `DynamicFlow.Merge_State_Update_LOCAL` & friends (no-ops within one context). This is what un-skips the 28 DynamicFlow tests.
- Lifetime: a component's context lives as long as its cached result; disposed only on recompute/removal.
- **Acceptance gate:** full `asm-sim-tests` green AND the 28 currently-`[Ignore]`d DynamicFlow tests pass (re-enable them).

**Phase 2 — CFG components. NOT YET (partitioning ready; needs real implementation).**
- Partitioning is ready and tested: `StaticFlow.ComputeLineToComponent()`. Since components are
  disconnected, a DynamicFlow built from a component's entry line would cover exactly that component.
- **BLOCKER found (2026-06-03):** `Runner.Construct_DynamicFlow_Forward(sFlow, startLine, nSteps, tools)`
  **ignores `startLine`** — it calls `Reset(sFlow, true)` which hardcodes `FirstLineNumber` (and
  `rootKey_ = "!" + FirstLineNumber`). The private `DynamicFlow.Update_Forward(sFlow, startLine)` does
  support an arbitrary start, so the fix is to add `Reset(sFlow, forward, startLine)` (set
  `rootKey_ = sFlow.Get_Key(startLine)`, call `Update_Forward(sFlow, startLine)`) and have the 4-arg
  Construct use it. Also map `Create_States_Before/After` semantics for components ending in
  `ret`/unconditional `jmp` (an exploratory test hit empty results there). So Phase 2 is genuine
  implementation, not just wiring.
- Then: simulate each component into its own context; cache per-line strings; keep its context alive.

**Phase 3 — Incremental invalidation.**
- On edit: diff lines → first changed line(s) → affected component(s) via line→component map. If the edit changes CFG structure (label/jump added/removed), re-partition the affected region.
- Recompute only affected components; reuse unaffected components' cached strings + live contexts untouched.
- Optional finer step: `Solver.Push/Pop` checkpoints to resume from the exact changed line within a component.

**Phase 4 — Memory policy & wiring.**
- A context accumulates AST across rebuilds → recreate a component's context on rebuild (don't reuse forever); cap live contexts with LRU eviction to strings-only for cold components.
- Wire `LspAsmSimulator` to the component cache. Keep read paths (`GetCachedString`/`GetSimStatesSummary`) string-based and context-independent (already true).

## Notes / invariants
- Read paths must remain string-based (plain managed strings, independent of Z3) so disposing a component's context never invalidates cached display data. (This invariant is what the 2026-06-03 leak fix established.)
- Run sim tests via `vstest.console.dll`, not `dotnet test` (silent failure on .NET 10 + MSTest).
- Current baseline to protect: `asm-sim-tests` 178 passed / 0 failed / 3 skipped (post Phase 1); `asm-dude2-ls-tests` 120 / 6 / 37.

See also: `Z3_CONTEXT_LIFECYCLE_BUG.md` (the crash this resolves) and memory `asmsim-baseline-and-z3-bug.md`.

---

## Phase 2 — feasibility review (2026-06-03, code-studied, parked for hands-on experiment)

Studied: `DynamicFlow.cs`, `Runner.cs`, `Tools.cs`, `StaticFlow.cs` (`ComputeLineToComponent`,
`Get_Key`), `LspAsmSimulator.cs`. Conclusion: **one Z3 context per weakly-connected component is
SOUND**, but the plan's *construction method* and its *target engine* both need correcting.

### What holds (per-component context is correct)
- Components are disconnected in the CFG → no `StateUpdate` ever translates an expr from one
  component into another → the cross-context `Translate` that caused the merge AV cannot occur
  between components. One context per component is just the natural extension of Phase 1
  (one `DynamicFlow` = one context).
- **Keys are line-number based**, not random: `StaticFlow.Get_Key(n) = "!" + n` (the random branch is
  `if(false)`, `StaticFlow.cs:108-118`). So distinct components use disjoint variable names by
  construction, and the same line always maps to the same vertex key.

### Problem 1 — weakly-connected component ≠ forward-reachable-from-min-line (CORRECTNESS)
- `ComputeLineToComponent()` builds **weakly-connected** components (union-find, undirected edges,
  `StaticFlow.cs:355-365`). But `Update_Forward` follows **directed** edges. The plan's claim "a
  DynamicFlow built from a component's entry line would cover exactly that component" is **false for
  multi-entry components**.
- Example (two function entries sharing a tail):
  ```
  0: funcA:  mov rax,1
  1:         jmp shared       ; 1→4
  2: funcB:  mov rax,2        ; in-degree 0, no in-file jump targets it
  3:         (fall through)   ; 3→4
  4: shared: add rax,5
  5:         ret
  ```
  Edges `0→1, 1→4, 2→3, 3→4, 4→5`. The `3→4` edge unions funcB into funcA's component → `min=0`.
  Forward from `0` visits `0,1,4,5` and **never reaches funcB (2,3)** (only reachable by walking
  `3→4` backwards). funcB silently un-simulated.
- **Confirmed by HJL's mental model**: both entries DO belong to one component; the issue is purely
  enumeration. Fix = **seed the flow from ALL entry points of the component, not just min-line.**
  - Convergence is automatic: line-based keys mean entry1's `!1→!4` and entry2's `!3→!4` land on the
    same vertex `!4`, giving it in-degree 2 → existing `Merge_State_Update_LOCAL` handles it
    (`DynamicFlow.cs:695-707`). Because both entries share the component's ONE context, this is the
    safe intra-context merge (identity Translate), not the AV path.
  - Concrete change: in `Update_Forward`, push all entry roots onto `nextKeys` initially.
  - **Entry set** = in-degree-0 vertices of the component. A component may have NONE (e.g. self-loop
    `loop: jmp loop`, in-degree 1) → fall back to min-line. So `roots = {in-deg-0} ∪ {min-line if
    empty}`. Min-line remains the stable **cache id** regardless, just not always a valid root.
  - **Latent leak to fix while here**: re-converged vertices get popped twice; the 2nd pop re-runs
    `Runner.Execute`, then hits the `Has_Edge` guard which neither adds nor disposes the fresh
    `StateUpdate` (`DynamicFlow.cs:404`, `:443`) — already leaks at any diamond today; multi-entry
    makes it routine. Dispose-on-skip.

### Problem 2 — the editor does NOT use DynamicFlow (ARCHITECTURE — the bigger one)
- Production sim path is `LspAsmSimulator`, which **deliberately avoids DynamicFlow**: linear
  `SimpleStep_Forward` walk that **resets** state at labels/ZERO-consistent points instead of merging
  (`LspAsmSimulator.cs:53`, `:676-685`). `DynamicFlow` is exercised only by `asm-sim-main/ProgramZ3.cs`
  (CLI playground) and the test suite.
- So Phases 2–4 optimize an engine the editor doesn't run. Phase 4's "wire `LspAsmSimulator` to the
  component cache" is really **replace the linear simulator with the multi-entry DynamicFlow engine** —
  i.e. bring the merge machinery back onto the editor hot path. That is the real scope, not a wiring step.

### The original Phase-2 BLOCKER note is still valid but incomplete
`Construct_DynamicFlow_Forward(sFlow, startLine, …)` ignores `startLine` (Runner.cs:52-56 →
`Reset(sFlow,true)` hardcodes `FirstLineNumber`). Adding `Reset(sFlow, forward, startLine)` is
necessary but **not sufficient** — a single arbitrary root still under-covers multi-entry components
(Problem 1). The real fix is multi-root seeding, not a single relocatable root.

### Two decision branches before writing code
1. **Goal = editor responsiveness** → cheaper path: make the *linear* `LspAsmSimulator` incremental
   (cache per-line before/after; on edit recompute only from first changed line). Line-based keys make
   this deterministic. No DynamicFlow, no merge, no context-lifecycle hazard. Matches the engine the
   editor already uses.
2. **Goal = real CFG/merge semantics in the editor** → do the multi-root DynamicFlow-per-component
   work above AND move `LspAsmSimulator` onto it. Construction work only pays off once this is chosen.

Status: **parked** — HJL to experiment with the code first.
