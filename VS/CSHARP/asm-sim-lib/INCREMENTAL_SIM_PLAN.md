# Plan: Shared-Context Rewrite + Incremental Simulation

**Status:** approved design, not yet implemented (2026-06-03).
**Decisions:** per-CFG-component Z3 contexts (one context per function/cluster) + the **DynamicFlow** (CFG/graph) engine.
**Supersedes:** the per-object-context model. This is "Option B" from `Z3_CONTEXT_LIFECYCLE_BUG.md`, extended to incremental.

> **⚠ BEFORE WRITING ANY PHASE-2 CODE, read ["Pre-implementation risk analysis (2026-06-11)"](#pre-implementation-risk-analysis-2026-06-11--read-before-coding) at the bottom.**
> It records code-verified hazards the original phase text does NOT cover — most importantly a
> **shared `System.Random` that makes naive parallelism a data race** — plus a safe, debuggable
> rollout (shadow-diff harness + engine flag + deterministic seeding). Several Phase-1 items still
> listed as "Still TODO" below are in fact **DONE** (verified 2026-06-11); see that section.

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
- `AsmSimulator.DisposeList` now disposes in **reverse** order so the context-owning root state is disposed last (pairs with the copy-ctor sharing).
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
  Construct use it. ~~Also map `Create_States_Before/After` semantics for components ending in
  `ret`/unconditional `jmp` (an exploratory test hit empty results there).~~ **RESOLVED by the S1 spike
  (2026-06-11, `Test_DynamicFlowComponent`): the "empty results" fear does NOT reproduce** — ret- and
  jmp-terminated components produce non-empty per-line states; `Create_States_After(0)`/`_Before(ret)`
  prove the values (`rax=10`). The post-`ret` end state is havoc/UNKNOWN (correct ret semantics,
  irrelevant to per-line editor extraction). So Phase 2 construction is mechanical wiring after all —
  EXCEPT the new boundary the spike found (below).
- Then: simulate each component into its own context; cache per-line strings; keep its context alive.

**Phase 3 — Incremental invalidation.**
- On edit: diff lines → first changed line(s) → affected component(s) via line→component map. If the edit changes CFG structure (label/jump added/removed), re-partition the affected region.
- Recompute only affected components; reuse unaffected components' cached strings + live contexts untouched.
- Optional finer step: `Solver.Push/Pop` checkpoints to resume from the exact changed line within a component.

**Phase 4 — Memory policy & wiring.**
- A context accumulates AST across rebuilds → recreate a component's context on rebuild (don't reuse forever); cap live contexts with LRU eviction to strings-only for cold components.
- Wire `AsmSimulator` to the component cache. Keep read paths (`GetCachedString`/`GetSimStatesSummary`) string-based and context-independent (already true).

## Notes / invariants
- Read paths must remain string-based (plain managed strings, independent of Z3) so disposing a component's context never invalidates cached display data. (This invariant is what the 2026-06-03 leak fix established.)
- Run sim tests via `vstest.console.dll`, not `dotnet test` (silent failure on .NET 10 + MSTest).
- Current baseline to protect: `asm-sim-tests` 178 passed / 0 failed / 3 skipped (post Phase 1); `asm-dude2-ls-tests` 120 / 6 / 37.

See also: `Z3_CONTEXT_LIFECYCLE_BUG.md` (the crash this resolves) and memory `asmsim-baseline-and-z3-bug.md`.

---

## Phase 2 — feasibility review (2026-06-03, code-studied, parked for hands-on experiment)

Studied: `DynamicFlow.cs`, `Runner.cs`, `Tools.cs`, `StaticFlow.cs` (`ComputeLineToComponent`,
`Get_Key`), `AsmSimulator.cs`. Conclusion: **one Z3 context per weakly-connected component is
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
- Production sim path is `AsmSimulator`, which **deliberately avoids DynamicFlow**: linear
  `SimpleStep_Forward` walk that **resets** state at labels/ZERO-consistent points instead of merging
  (`AsmSimulator.cs:53`, `:676-685`). `DynamicFlow` is exercised only by `asm-sim-main/ProgramZ3.cs`
  (CLI playground) and the test suite.
- So Phases 2–4 optimize an engine the editor doesn't run. Phase 4's "wire `AsmSimulator` to the
  component cache" is really **replace the linear simulator with the multi-entry DynamicFlow engine** —
  i.e. bring the merge machinery back onto the editor hot path. That is the real scope, not a wiring step.

### The original Phase-2 BLOCKER note is still valid but incomplete
`Construct_DynamicFlow_Forward(sFlow, startLine, …)` ignores `startLine` (Runner.cs:52-56 →
`Reset(sFlow,true)` hardcodes `FirstLineNumber`). Adding `Reset(sFlow, forward, startLine)` is
necessary but **not sufficient** — a single arbitrary root still under-covers multi-entry components
(Problem 1). The real fix is multi-root seeding, not a single relocatable root.

### Two decision branches before writing code
1. **Goal = editor responsiveness** → cheaper path: make the *linear* `AsmSimulator` incremental
   (cache per-line before/after; on edit recompute only from first changed line). Line-based keys make
   this deterministic. No DynamicFlow, no merge, no context-lifecycle hazard. Matches the engine the
   editor already uses.
2. **Goal = real CFG/merge semantics in the editor** → do the multi-root DynamicFlow-per-component
   work above AND move `AsmSimulator` onto it. Construction work only pays off once this is chosen.

Status: **parked** — HJL to experiment with the code first.

---

## Pre-implementation risk analysis (2026-06-11) — READ BEFORE CODING

Goal of this section: surface the **preventable analysis flaws** before they bork the project, and
replace "big-bang engine swap + re-baseline" with a staged, instantly-reversible, **observable**
rollout. Every claim below was verified against the current `dotnet10` code (file:line cited).

### 0. Phase-1 status correction (so we don't redo finished work)
The Phase-1 "Still TODO" list (§Phase 1) is **DONE** as of 2026-06-11:
- Borrowed-context (`ownsCtx_` + `Tools.SharedCtx`) is landed in **`State`**, **`StateUpdate.cs:141-170`**
  (both ctors), and **`OpcodeBase`/`Mnemonics.cs:102-110`** — not just `State`.
- `DynamicFlow.cs:62-63` owns one `Context` per flow and threads it via `SharedCtx`.
- `asm-sim-tests` 178/0/3 green. So Phase 2 starts from a solid, crash-free base.
Also: `Construct_DynamicFlow_Forward(sFlow, startLine, nSteps, tools)` (`Runner.cs:52`) ignores
**both** `startLine` **and** `nSteps` (calls `Reset(sFlow,true)`); the original note only flagged
`startLine`. `nSteps` is dead — termination is graph-edge-bounded (see #3), not step-bounded.

### 1. 🔴 BLOCKING parallel-safety hazard — shared `System.Random`
`Tools(Tools other)` does `this.rand_ = other.Rand;` (**`Tools.cs:50`**, note the commented-out
`//new Random()`). Every `DynamicFlow` is built `new Tools(tools){SharedCtx=…}` (`DynamicFlow.cs:63`),
so **all components share ONE `Random`**. `System.Random` is **not thread-safe**: concurrent
`Reg_Name_Fresh`/undef-constant generation (`StateUpdate`/`OpcodeBase` use `tools.Rand`) across
parallel components causes torn internal state → intermittent garbage names or exceptions. This is the
classic silent, non-reproducible, parallel-only bug.
**Fix (precondition for ANY parallelism):** give each component's `Tools` its **own** `Random`,
**deterministically seeded** from the component id (e.g. `new Random(componentMinLine)`). Deterministic
seeding also makes a failing sim **reproducible** — a debuggability win even before parallelism. Do NOT
parallelize until this is fixed and asserted by a test that runs N components concurrently.

### 2. Multi-root soundness rests on an invariant that must be documented + tested
Verified mechanism (why multi-root works with line-based keys): the traversal (`Update_Forward`,
`DynamicFlow.cs:379-393`) only builds the **graph** (vertices/edges + symbolic `StateUpdate`s); actual
state **merge happens lazily at extraction** (`Create_States_Before/After`). Symbolic updates are keyed
by line (`"!"+n`), so a shared tail line is computed once and is correct for every incoming path; the
two entry paths add two edges into the same vertex (in-degree 2) → `Merge_State_Update_LOCAL`, which is
the **safe intra-context** merge (one shared component context, identity Translate).
**Invariant to protect:** *the component graph must be fully built before any `Create_States_*` call.*
A future change that interleaves extraction with construction silently breaks multi-entry merges.
**Gate test:** the funcA/funcB multi-entry example (§Problem 1) — assert funcB lines are simulated and
the shared-tail state is the merge, not a single path.

### 3. Termination & loops (verified OK, document it)
`Update_Forward` pushes a successor only if `!Has_Edge(prev,next,branch)` (`DynamicFlow.cs:404,443`), so
each edge is added once and loops terminate without `nSteps`. Multi-root seeding does not change this
(edges are per `(srcLine,dstLine,branch)`). **Gate test:** a self-loop (`l: jmp l`) and a diamond must
terminate and produce one vertex per line. (Also fix the dispose-on-skip leak at `:404/:443` while here.)

### 4. Extraction parity for non-instruction lines (verify, don't assume regression)
`StaticFlow` skips `Mnemonic.NONE` lines when computing edges (`:144` `while mnemonic==NONE`), so blank/
comment/label/directive lines are **not CFG vertices** → DynamicFlow has no before/after for them. The
**linear** sim also `continue`s on those lines without writing the cache, so this may already be at
parity — **but verify per-line**, because label lines that ARE jump targets carry meaning. The shadow
harness (S0) makes any divergence here visible automatically.

### 5. 🟠 Semantic regression is the thing that "borks silently"
Merge-at-join (DynamicFlow) ≠ the linear sim's label-reset heuristic (`AsmSimulator.cs:~676`). Per-
line displayed register/flag values **will** change (some become `unknown` at joins — more correct;
some single-path concretes disappear). Without a per-line diff you cannot tell a correct change from a
regression. **This is mandatory:** do not flip the editor onto the new engine without the shadow diff
(S0) first.

### 6. 🟠 Phase-3 incrementality is defeated by line-based keys — DECIDE before building it
Keys are `"!"+lineNumber` (`StaticFlow.Get_Key`, the random branch is `if(false)`). Inserting/deleting a
line **renumbers every key** → invalidates **every** component's cached context, even for an edit inside
one function. So Phase 3 "recompute only the touched component" delivers near-zero benefit for the most
common edit (typing a new line). **Pick a strategy before writing Phase 3:**
  (a) accept it (only in-place, same-line edits are incremental — modest, simplest);
  (b) re-key a component by a **content/structural signature** (hash of its normalized instructions) so
      a pure renumber that doesn't change a body reuses the cached strings (remapped to new lines);
  (c) carry an explicit **line-delta** across edits so unchanged components shift line numbers without
      recompute.
Building Phase 3 on (a) unknowingly = wasted effort. This is the single biggest hidden design flaw.

### 7. Parallel peak-memory bound
N live component contexts at once ≈ `maxDegree × largest-component-context` native memory (Z3 contexts
are the heavy object — the 18 GiB-leak history). **Bound concurrency** with a `SemaphoreSlim`
(`maxDegree = min(Environment.ProcessorCount, K)`) and **dispose each context immediately after string
extraction** (read paths are string-based, invariant §Notes). Unbounded fan-out can spike memory worse
than today's single chain.

### Revised rollout — staged, reversible, observable
- **S0 — Differential shadow harness (build FIRST, zero user risk).** Implement the component engine as
  a **separate** method; do NOT touch `RunSimulation`. Add an engine selector
  `ASMDUDE_SIM_ENGINE = linear | component | shadow` (env var, mirrors `ASMDUDE_LOGLEVEL`). In `shadow`,
  the **linear** engine stays authoritative (drives the editor) and the **component** engine runs
  compute-only; log every per-line before/after/label/diagnostic mismatch at `Warn` under a new
  **`SIMDIFF`** category. Ship this, run real `.asm` files, understand every diff. Only then flip
  `ASMDUDE_SIM_ENGINE=component` (instant rollback = flip back).
- **S1 — DynamicFlow construction (Part A). ✅ DONE (2026-06-11).** No editor change; full sim suite
  **194/0/3** (no regression from the core construction refactor).
  - **Item 5 spike** (`Test_DynamicFlowComponent`): ret/jmp components yield non-empty, value-proving
    per-line states — the "empty results" fear did NOT reproduce. **New finding carried to S2:** DynamicFlow
    does a **single-pass merge, not a fixpoint**, so a loop head reads its merged value as UNKNOWN
    (`mov rax,7; jmp start` ⇒ rax UNKNOWN, not the invariant 7). Not a regression (the linear editor sim
    ignores jumps today); the component engine is just *less precise in loops* — SIMDIFF will surface these
    exact lines as an explicit S2/S3 accept/improve decision, NOT a silent bug.
  - **Items 1/2/6 (multi-root construction):** `DynamicFlow.Update_Forward(IReadOnlyCollection<int>)` seeds
    ALL entry roots (single-int delegates to it); `DynamicFlow.Reset(sFlow, forwardRoots)`; the 4-arg
    `Runner.Construct_DynamicFlow_Forward` now HONORS `startLine` (was ignored) + a new
    `Construct_DynamicFlow_Forward(sFlow, IReadOnlyCollection<int> roots, tools)`. **Gate test**
    `MultiRoot_CoversAllEntries_OfMultiEntryComponent`: a single root from line 0 does NOT reach funcB
    (under-coverage demonstrated), multi-root covers funcB(rbx=2) AND funcA(rax=1). Items 3 (entry-lines
    primitive) + 4 (dispose-on-skip) were already done.
  - **Remaining gate tests still worth adding** (not blockers): self-loop+diamond termination (§3) is
    partly covered by the spike + the `Test_Z3ContextTracker_DynamicFlow_NoLeak` diamond; the parallel-
    Random race (§1) needs the parallel driver (S2).
- **S2 — component extraction + shadow. STARTED (2026-06-11), shadow-only, NO editor flip.**
  - `AsmSimulator.BuildComponentResultSet` — the per-component engine: StaticFlow → ComputeLineToComponent
    + ComputeComponentEntryLines → one multi-root `DynamicFlow` per component (own Z3 context + DISTINCT
    per-component seeded Random via `new Tools(settings,"",componentId)` — the §1 parallel-safety seam) →
    extract per-line before/after via `Create_States_Before/After(line,0)`. Before/after only for now
    (labels/diagnostics = S2b). Robust: each component try/caught, never throws into the linear sim.
  - `ASMDUDE_SIM_ENGINE=linear|shadow` (env). In `shadow`, the linear run stays authoritative and
    `RunShadowComparison` runs the component engine compute-only and logs the SIMDIFF under the **`SIMDIFF`**
    category. `EnableFullStateConfig` is shared by both engines so the comparison is apples-to-apples.
  - Tests (ls-tests, via `CompareEnginesForTest` seam): `Shadow_StraightLineProgram_EnginesAgree` (no
    merges ⇒ identical) and `Shadow_BranchProgram_EnginesDifferAtJoin` (the merge-vs-linear divergence is
    detected at the join — the shadow's whole purpose). **Parity bug the shadow already caught + fixed:**
    the component engine emitted the phantom fall-through "end" vertex (line N of an N-line program); now
    filtered to real document lines (`line < lines.Count`).
  - **Remaining S2:** labels/diagnostics parity (S2b); actual bounded PARALLELISM (components run
    sequentially today — seeded Random already makes them parallel-SAFE) with a `SemaphoreSlim` + dispose-
    after-extract (§7 peak bound via `Z3ContextTracker.Peak`); then — only after shadow diffs on real files
    are understood — the engine FLIP (component authoritative), which is the one deliberately-deferred step.
- **S3 — incrementality (Phase 3)** only after the §6 key-strategy decision.

### Observability requirements (so a problem is diagnosable, not a mystery)
**Ranked by importance for THIS refactor** (the two documented dangers are the Z3 context lifecycle and
silent semantic divergence — observe those FIRST; the partition/timing are necessary but secondary):
1. **🥇 Z3 context leak/crash gauge — DONE (2026-06-11).** `AsmSim.Z3ContextTracker` (Live / Peak /
   TotalCreated) counts every owned `Context` create+dispose (wired at all 5 owned-create sites — `State`,
   `StateUpdate`×2, `OpcodeBase`, `DynamicFlow` — and their 4 owned-dispose sites; borrowed/shared refs are
   not re-counted). This is the object behind the 18 GiB native leak and the lifecycle AV. The sim run
   summary logs `Z3 ctx live=.. peak=..`; **leak-regression tests** `Test_Z3ContextTracker_State_NoLeak` /
   `_DynamicFlow_NoLeak` (asm-sim-tests, sequential ⇒ global counter reliable) assert Live returns to
   baseline after a create/dispose cycle — and the DynamicFlow one exercises the dispose-on-skip fix on a
   diamond. As the per-component engine raises Peak, this bounds peak native memory (§7).
2. **🥈 `SIMDIFF` — differential oracle. Comparator DONE (2026-06-11); engine-swap consumer pending.**
   `AsmSim.SimResultComparer` + `SimResultSet`/`SimLineResult`/`SimDiff` (asm-sim-lib) is a standalone,
   producer-agnostic comparator: two per-line result sets (before/after state, read/write labels,
   diagnostics) → structured per-field diff, with `ToReport()` for the SIMDIFF log / test messages. Pure,
   total, no Z3/LSP. Unit-tested by `Test_SimResultComparer` (8 tests, hand-built sets). Producer adapter
   `AsmSimulator.ToResultSet(uri,label)` snapshots the editor sim. First consumer:
   `SimResultComparer_SameProgramTwice_NoDiff` (determinism, ls-tests). **Reusable beyond the swap**:
   determinism/flakiness (run-twice), timeout-sensitivity (5000 vs 1000), Z3-upgrade validation,
   optimization-equivalence (incl. the linear-per-component parallelism), differential fuzzing oracle,
   corpus golden suite. **Remaining:** wire it as the S0 shadow oracle once the component engine exists
   (linear `ToResultSet` vs component `ToResultSet` → log non-empty diffs under `SIMDIFF`).
3. **Exception context** — when the sim catch fires, log line/mnemonic/version (currently message-only).
4. **Per-component metrics** (needs the engine): `component {id}: {nLines} lines, {nEntries} entries,
   build {ms}ms, sim {ms}ms`, thread id, seed.

**Already landed (secondary but useful):**
- Run summary with `slow/timeout line(s) (>=4500ms)` + total ms; per-line `done in {ms}` timing.
- CFG partition logging `[CFG] <uri>: N lines -> C component(s); entries: ...` (gated on Debug).
- Deterministic per-component seeding seam (§1) so any logged failure reproduces.

**One-line summary:** the plan is directionally sound and sits on a finished Phase 1, but as written it
would (a) data-race on the shared `Random` the moment it parallelizes, (b) ship a silent per-line
semantic change with no diff to catch regressions, and (c) build a Phase-3 cache that line renumbering
silently invalidates. Fix #1, add the S0 shadow harness, and decide #6 — then the rest is safe,
incremental, and reversible.

### Prep landed (2026-06-11) — additive, behavior-neutral, no editor change
Three groundwork changes are in, full sim suite green (**181/0/3** = old 178 + 3 new tests):
1. **`StaticFlow.ComputeComponentEntryLines()`** — the multi-root seeding primitive (§Problem-1 / §2):
   per-component in-degree-0 entry lines ∪ min-line fallback for entryless (self-loop) components. Tests
   `Test_StaticFlow_ComponentEntryLines_MultiEntry` (proves both funcA+funcB entries found, shared tail
   excluded) and `_SelfLoopFallback`.
2. **`Tools(contextSettings, solverSetting, int randomSeed)`** — deterministically-seeded `Random` seam
   for the §1 hazard (per-component distinct seed ⇒ no shared-`Random` race + reproducible). Copy ctor
   still SHARES intra-unit (correct). Test `Test_Tools_SeededRandom_Reproducible_And_Independent`.
3. **DynamicFlow dispose-on-skip leak fix** (§3, the `:404/:443` leak) — re-converged/`-1`-target
   `StateUpdate`s are now disposed instead of leaked. No result change (181/0/3).

### ⚠ Reproducibility caveat the seeded `Random` does NOT cover (record before relying on it)
Deterministic C#-level randomness is necessary but **not sufficient** for reproducible sims/tests:
- **Z3 has its own randomness** — `random_seed` / `sat.random_seed` / `smt.random_seed` are **not set**
  today (only `"timeout":"5000"` is). Set them for determinism if tests assert exact values.
- **The 5 s per-line timeout is wall-clock-dependent** — a query that times out on a slow machine but
  completes on a fast one yields a *different* result (a value vs `unknown`). So any test/program that is
  timeout-sensitive is inherently flaky regardless of seeding. **Reproducible tests must use programs
  small enough that Z3 never hits the timeout** (or raise/remove the timeout in the test).
- Production (`AsmSimulator`, `DynamicFlow`) does **not** use the seeded ctor yet — the seam exists
  and is tested, but determinism is opt-in until the component driver adopts it.

### Headless characterization baseline landed (2026-06-11)
The editor's linear simulator now has a **golden, VS-free** test baseline — the oracle the S0 shadow
harness will diff the new engine against:
- **Seam:** `AsmSimulator.SimulateSynchronouslyForTest(uri, lines)` runs the real `RunSimulation`
  synchronously (no debounce/Task) — not a re-implementation.
- **Tests:** `asm-dude2-ls-tests/AsmSimulatorTests.cs` (xUnit, 4 tests, no VS): straight-line proven
  register values, `xor`-self zero+ZF, **same program simulated twice ⇒ byte-identical output**
  (reproducibility proven for the editor sim), and a branch golden.
- **Z3 determinism:** `random_seed=0` added to the editor sim's Z3 settings (empirically verified Z3
  accepts it as a context param; tests still prove exact values).
- **Captured output format** (so future assertions match): compact state is e.g.
  `RAX=0x_0000_0000_0010, CF=0 ZF=1 SF=0 OF=0` (upper-case reg, underscore-grouped, full 64-bit width).
- **🔎 Finding the baseline exposed:** the linear editor sim **does NOT follow jumps** — it simulates
  every line top-to-bottom, so a `mov rax,2` after a `jmp` over it STILL executes and `rax=2` flows into
  the join. This is precisely the imperfection the merge engine fixes; `Branch_LinearEngine_DoesNotFollow
  Jumps_GoldenBaseline` pins it, so the S2 swap surfaces as an intentional, reviewed change to that test.
- **Log de-spam:** the per-label `LabelGraph.Get_Filename` "no filename" warning (flooded the log on the
  CodeLens/diagnostics path) was demoted Warn→Debug.

### Observability foundation landed (2026-06-11)
The "Observability requirements" above are now partly in place (the parts that don't need the component
engine), in `AsmSimulator`:
- **Per-run summary** — the `[THREAD] SUCCESS` line now reports `slow/timeout line(s) (>=4500ms)` and
  `total ms` (directly diagnoses the slow-sim complaints; the 4500 ms heuristic flags lines that hit the
  5 s Z3 timeout). Per-line `done in {ms}` timing was already added.
- **CFG-partition logging** — `LogCfgPartition(uri, lines)` logs the weakly-connected decomposition +
  per-component entry lines on every run, **gated on `AsmLog.IsEnabled(Debug)`** (building a `StaticFlow`
  instantiates opcodes) and wrapped so it never throws into the sim. Format:
  `[CFG] <uri>: N lines -> C component(s); entries: c0=[..] c16=[..]`. This is the keystone observability
  for Phase 2 — it shows how real files decompose and exercises `ComputeComponentEntryLines` on live input
  with ZERO effect on the linear sim. Verified end-to-end by `Observability_LogsCfgPartition_ForMulti
  FunctionProgram` (captures the log via an `AsmLog` sink). **Gotcha caught by that test:** `StaticFlow.
  Update` splits on `Environment.NewLine`, so editor `lines` must be joined with that, not `"\n"` — the
  same join the future component engine will need.

---

## Phase P — Parallelize the component engine's per-line extraction (2026-06-13)

**STATUS: IMPLEMENTED 2026-06-13** (behind `ASMDUDE_SIM_ENGINE=component`; editor default still Linear).
`AsmSimulator.ComputeComponentLines` clones each resolved line's before/after states into their OWN
fresh Z3 context (`CloneLineForWorker`, serial on the evaluator thread — cheap AST `Translate`) and solves
them on a bounded `SemaphoreSlim(SimParallelism)` worker pool (`ExtractComponentLine`), streaming each via
`Emit` and disposing the clone after. `ASMDUDE_SIM_PARALLEL` sets the degree (default ≈ min(ProcessorCount,
8); 1 = serial). Per-line `Tools` use the SEEDED ctor (isolated `Random` per worker — the copy ctor would
share one non-thread-safe Random). `Emit` is lock-guarded; `Z3ContextTracker` counts each clone ctx.
Validated: build clean (0 warn); shadow tests 4/1 (parallel component == linear where expected, differs at
joins as expected ⇒ parallelism changed NO values); `Test_ComponentEvaluator` 7/7. **Not yet measured on a
real file** — open the example with the env var and read `extracted in … ms` cadence + `total … ms`. Design
write-up follows.

**Goal:** make the merge (component) engine fast enough to be the editor default. The bottleneck is the
per-line **value extraction** (`AsmSimulator.ExtractComponentLine` → `ComputeStateString` → Z3 solves
each register's display value), observed at **~4 s/line** (same order as the linear sim's per-line Z3 cost).
Extraction of line N is **independent** of every other line once the symbolic states exist, so it is
embarrassingly parallel — modulo the Z3 constraint below.

### Prerequisite — DONE (2026-06-13): incremental emission
`ComponentEvaluator` got an `onLineReady(line, before, after)` callback fired in topo order the moment a
vertex's before+after states resolve (its two `Evaluate()` passes were merged into one — `after(v)` only
needs `before(v)` + v's out-edges). `AsmSimulator.RunComponentSimulation` now writes each line to the
`DocCache` and fires a throttled `onProgress` as it's extracted (mirrors the linear loop), so CodeLens/hover
appear progressively instead of after the whole document. Per-phase timing added:
`[component] component K: DynamicFlow built in … ms` + per-line `[component] line N: extracted in … ms`.
**Gate before building Phase P:** read those logs on a real file under `ASMDUDE_SIM_ENGINE=component` and
confirm the per-line `extracted in …` time dominates (vs the gaps between lines = the join-solving inside
`Evaluate()`). If extraction dominates → build P. If `Evaluate()` joins are also multi-second, P helps only
partially (that part is topo-dependent — see "Out of scope").

### The hard constraint
A Z3 `Context` is **not thread-safe** — not even for concurrent *reads*. All states within one component
**share one context** (`Tools.SharedCtx`; this shared-context design is what fixed the cross-context
`Z3_translate` AV). Therefore:
- You may NOT solve two lines' states concurrently on the shared context.
- You may NOT `Translate` two states off the shared context concurrently (Translate reads the source ctx).

### Design — serial clone, parallel solve
Split each line's work into a cheap serial half and the expensive parallel half:

1. **Serial (evaluator thread, in `onLineReady`):** deep-clone the line's `before`/`after` State into its
   OWN fresh `Context` via the existing copy path (`new State(other)` with an owned context ⇒ Z3
   `Translate` of the assertions). Translate copies expression trees only — **no solving** — so it's ~ms
   against the 4 s solve. The clone is independent of the shared context.
2. **Parallel (worker pool):** hand the cloned (own-context) states to a bounded worker. The worker runs
   `ExtractComponentLine` on the clones (the 4 s of `ComputeStateString`/`ComputeReadLabel`/diagnostics
   solving), then **disposes the cloned context**, then `Emit`s the result (cache write under `lockObj_`
   + throttled `onProgress`).

The evaluator keeps marching the worklist (serial, shared ctx) while workers solve — so construction and
extraction overlap, and N lines solve at once.

### Bounding & safety
- **Bounded parallelism:** `SemaphoreSlim(maxDegree)` with `maxDegree ≈ Environment.ProcessorCount`
  (cap, e.g. 4–8). The evaluator `await`s a slot before cloning the next line ⇒ back-pressure ⇒ at most
  `maxDegree` clones alive ⇒ bounded Z3 native memory (the 18 GiB-leak risk). Release the slot in the
  worker's `finally` after disposing the clone.
- **Memory gauge:** assert `AsmSim.Z3ContextTracker.Peak` stays ≤ baseline + maxDegree (clones are owned
  contexts → counted). Add to the `[component] SUCCESS` summary.
- **Cancellation:** check `ct` before cloning and inside the worker; a superseded run abandons cheaply
  (dispose any in-flight clones).
- **Determinism:** extraction is read-only and per-line independent ⇒ result is order-independent; emission
  order varies but the cache is keyed by line, so the final state is identical. (Keep the per-component
  seeded Random as-is; it affects construction, not extraction.)
- **Threading of `Emit`:** the cache write already takes `lockObj_`; keep it. `linesWritten/diagCount` and
  the throttle clock become shared — guard with the same lock or `Interlocked`.

### Implementation sketch
- `ComponentEvaluator` is unchanged (already streams per vertex). The parallelism lives entirely in
  `AsmSimulator`:
  - In the `onLineReady` callback: clone before/after into a fresh context (serial), then `await
    semaphore`, then `Task.Run` the worker (solve+extract+emit+dispose+release).
  - Track the spawned tasks; `Task.WhenAll` them after `ComputeComponentLines` returns, before the
    `SUCCESS` log / `onCompleted`.
- Need a small helper: `State CloneIntoOwnContext(State s, Tools perLineTools)` — verify the copy ctor
  path that allocates an owned `Context` and Translates the assertions (see `State(State other)` and the
  `ownsCtx_`/`SharedCtx` logic). If the copy ctor only ever shares, add an explicit "clone to new ctx"
  constructor/method.
- Per-line tools: each clone needs a `Tools` whose `SharedCtx` is the clone's own context (so
  `ComputeStateString`/solver use it). Build a lightweight per-line `Tools` (seeded deterministically) or
  reuse the component tools with the context swapped — confirm `ComputeStateString` reads the context off
  the State, not off a shared Tools.

### Verification
- **Correctness:** SIMDIFF (`ASMDUDE_SIM_ENGINE=shadow`) must show **zero new diffs** vs the sequential
  component run — parallelism must not change values. Add a test: same program, sequential vs parallel
  extraction ⇒ identical `SimResultSet`.
- **Speed:** wall-clock of `[component] SUCCESS total … ms` drops ≈ `min(maxDegree, cores)`× on a
  multi-line file; per-line `extracted in …` unchanged (each still ~4 s, just overlapped).
- **Memory:** `Z3 ctx peak` ≤ baseline + maxDegree.
- **Stability:** no AV / no `Z3_translate` race over a stress file (the whole reason for the serial-clone rule).

### Out of scope (separate, harder)
- Parallelizing `Evaluate()` itself (the merge/join solving) — topo-dependent, not embarrassingly parallel.
  Only attempt if the gate above shows join-solving (not extraction) dominates.
- Cross-component parallelism (each component already owns a context, so it's the *easy* axis) — a cheap
  add once the within-component worker pool exists: run components on the same bounded pool. Do it second.

---

## The real bottleneck is `Evaluate()` join-solving, not extraction (measured 2026-06-13)

> **✅ RESOLVED 2026-06-13 (direction B, AsmDude1's recipe).** `AsmSimulator.ComputeComponentLines` no
> longer runs `ComponentEvaluator`; it reads each line's state straight from `DynamicFlow.Create_States_
> Before/After(line)` (the SYMBOLIC ITE/phi merge in `Create_State_Private`/`MergeConstructor`; `Tools.
> Collapse` for the rare multi-state case) — exactly as AsmDude1's `Get_State_Before/After` did — and solves
> only the displayed read/write registers per line on the Phase-P pool. The bespoke `ComponentEvaluator`
> (`PlantAtKey`/`JoinContribsAt`, the all-16-register-per-vertex Tv solve) was **deleted**. Measured: 3-line
> program 66,836 ms → **587 ms** (~114×); 150-line `example_semantic_analysis.asm` timeout(>4 min) → **8.7 s**.
> The ITE-chain-depth caveat below is moot for the editor (line→key is 1:1 so `Collapse` rarely fires) — it
> would only matter if a future change reintroduced long symbolic merge chains. Analysis kept below for history.

Phase P parallelized **extraction** and the editor was made to skip the full register dump
(`AsmSimulator.computeFullState_` — editor needs only the read/write CodeLens labels, ≈1-3 registers, not
the all-16 `ComputeStateString`). That made extraction cheap (`extracted in` ≈ 0.3-2.5 s/line). But
instrumenting the launched out-of-process server on a 3-line program exposed where the time actually goes:

```
[component] vertex !0 (line 1): symbolic before+after in 2442 ms
[component] vertex !1 (line 2): symbolic before+after in 10686 ms
[component] vertex !2 (line 3): symbolic before+after in 15356 ms     <- CLONE 11 ms, extract 2344 ms
[component] vertex !3 (line 4): symbolic before+after in 15983 ms
[component] SUCCESS: 3 lines … total 44600 ms
```

So **the `ComponentEvaluator.Evaluate()` per-vertex symbolic build dominates** (≈2.4 → 16 s, *growing per
vertex*), while CLONE is ~10 ms and extraction is sub-3 s. This is the "join-solving" the Phase-P "Out of
scope" gate named. The 150-line `example_semantic_analysis.asm` therefore can't finish in minutes (the
opt-in probe `AsmSimComponentEngineTests` is `Skip`ped for this reason).

### Root cause: `PlantAtKey`/`JoinContribsAt` solve ALL `GetRegOn()` registers at EVERY vertex
`ComponentEvaluator.PlantAtKey` (and `JoinContribsAt`) loop `this.tools_.StateConfig.GetRegOn()` (all 16
GP regs, set by `EnableFullStateConfig`) and call `snapshot.GetTvArray(r)` per register — the expensive
per-bit Z3 solve (64 bits × 2-4 `Solver.Check`, see `ToolsZ3.GetTv_Method2`). Run at **every** vertex
(both `ComputeForward` and `ComputeAfter` end in a plant/join), that is `O(vertices × 16 × 64 × checks)`.
It grows per vertex as the planted values feed deeper chains. This is the SAME all-16-register waste removed
from the editor extraction, but here it is structural to the engine's state construction.

### Why it's there (corrected, code-checked — not "to keep expressions shallow")
The documented reason (`ComponentEvaluator.cs` class comment, ~L42-47) is that the engine plants every
vertex at the **canonical key** and joins **value-level (per-bit `Tv`)**, *not* by solver-merging — because
the legacy solver-merge `State(s1,s2,merge:true)` (`State.cs` `MergeConstructor`) asserts the **union** of
both solvers' assertions (`State.cs:277-291`), so two contributors that share a canonical key would
AND `reg!K=A ∧ reg!K=B` → contradiction. **But** that contradiction is *self-inflicted by the canonical-key
plant*: `MergeConstructor`'s normal path (different head keys, `State.cs:312-335`) builds a proper SSA
**ITE/phi** merge `merged_reg = ITE(branch, reg@head1, reg@head2)` and does NOT contradict. So:
- The eager per-register solve is a consequence of *choosing canonical-key planting*, which has two faces:
  (a) it forces the value-level join [documented], and (b) it keeps expressions shallow vs the legacy ITE
  merge's deepening nests [the other side of the same coin]. Neither is the root; the *plant-to-canonical-key
  choice* is.

### Two fix directions
- **A — write-set-only plant (recommended first; low risk).** Keep canonical-key planting, but thread the
  predecessor's already-concrete `Tv[]` forward and `GetTvArray`-solve **only** the registers written on a
  contributing edge (the edge's `StateUpdate` write-set); registers unwritten on all contributors keep their
  carried `Tv[]` (cheap per-bit join, no Z3). Sound (an unwritten register's value is unchanged). Drops
  ≈14/16 of the per-vertex solves. Guard with the shadow-parity test (`SimResultComparer`,
  `OutOfProcess_MatchesInProcess`).
- **B — legacy ITE/phi symbolic merge (AsmDude1's path; bigger).** Drop canonical-key planting, use the
  distinct-key ITE merge + **lazy, cached, per-queried-register** solving (what made AsmDude1 feel fast:
  `Tools.Collapse` + `Create_StateConfig` for only used regs + hover-time `GetTvArray_Cached`).

### ITE-chain depth is NOT a blocker for direction B — depth-collapse is a *missing feature*, not a wall
The objection to B is that ITE merges `ITE(bc, ITE(bc', …), …)` deepen over long merge chains, making the
lazy solve expensive. That depth is **boundable** and the methods are simply **not implemented yet**:
- **Lossless collapse** — prune an ITE subtree once it can no longer affect any truth-value: both arms
  equal, the branch condition becomes determined, or the subexpression feeds a register no longer read
  downstream (dead-subtree / dead-key elimination). Precision preserved.
- **Lossy simplification** — generic over-approximation that caps depth at a precision cost.
Neither exists today, so today B's ITE terms would grow unbounded — but that is a TODO, not a fundamental
limitation of the symbolic-merge approach. Record this so the A-vs-B choice isn't made on the false premise
that "ITE merges must deepen forever."

---

# Phase 3 — Incremental invalidation via the dataflow cone (DETAILED SPEC, 2026-06-13)

> Replaces the coarse "per-component reuse" framing of the old Phase 3/§6 with a precise **dataflow-cone**
> design. The §6 line-key problem is dissolved here: we reason about *instructions and their dataflow*, and
> line numbers become a pure display mapping produced by an instruction-level diff.

## 1. Goal / non-goals
- **Goal:** on an edit, recompute only the lines whose displayed values *can* change (the forward dataflow
  **cone**), reuse cached strings for everything else, and produce output **byte-identical to a full
  re-simulation**. The win is skipping the expensive per-line Z3 **solve** for unaffected lines.
- **Non-goal:** reusing Z3 *contexts/states* across edits. The cache is strings-only (Z3-independent), so
  there is NO context-lifetime / LRU / memory problem (the original Phase-4 worry evaporates). We rebuild
  the cheap symbolic structure and only avoid the expensive solving.

## 2. Why it's tractable now (vs the 2026-06-03 framing)
- Construction is cheap (`DynamicFlow` build ≈ 50 ms); the cost is the per-line **solve/extract** (hundreds
  of ms × lines). So "recompute the structure, skip the solves" is the right lever.
- `DocCache` is strings-only (`lineStringsBefore/After/Read/WriteLabels` + `diagnostics`) → reusing them is
  a pure managed-string remap.
- The cone's ingredients already exist: per-instruction `RegsReadStatic`/`RegsWriteStatic` +
  `FlagsReadStatic`/`FlagsWriteStatic` (`AsmSimulator.cs:907-910`), and the `DynamicFlow` *is* the dataflow
  graph. No Z3 is needed to compute the cone.

## 3. Layering & retained per-document state
- **StaticFlow** = control flow only: per-line `(label, mnemonic, args)` + CFG edges. Cheap, syntactic.
  `Update(text)` returns `changed` by comparing per-line content by index (`StaticFlow.cs:443`, diff
  ~L500-540) — so an operand edit *does* register; a comment/whitespace edit does not.
- **DynamicFlow** = symbolic values, built from StaticFlow; the **cone lives here**.
- **New retained state per URI** (today only `cache_`/`simVersion_`/`pendingTasks_` are kept, and `cache_`
  is *discarded* every edit at `AsmSimulator.cs:163`):
  - `prevInstr_[uri]`: the previous parsed instruction sequence `(label, mnemonic, args)[]` (for the diff).
  - the previous `DocCache` (stop discarding it; build a NEW one by merging reused + recomputed lines).
  - (optional) `prevReadWrite_[uri]`: per-line read/write sets, to avoid re-instantiating opcodes.

## 4. The edit pipeline (replaces "always rebuild")
On a debounced edit with `newLines`:
1. **Parse** `newLines` → `newInstr` (`(label,mnemonic,args)[]`). Cheap; no Z3.
2. **Instruction-level diff** `prevInstr` vs `newInstr` (LCS/Myers on the tuples) → an **edit script** and a
   bijection **`oldLine ↔ newLine`** for the *unchanged* instructions. This is what makes insert/delete
   cheap: a shifted-but-identical instruction is matched, not treated as edited (the "line-delta" that
   defeats §6).
3. **Tier 0 — no instruction change** (only comment/whitespace/blank shifts): **reuse the whole previous
   DocCache**, remapped through `oldLine→newLine`. **No Z3.** Done.
4. **Otherwise** build the new `StaticFlow` (cheap) and:
   - **Classify the edit** by comparing edges/labels: *topology unchanged* (operand-only) → narrow dataflow
     cone valid; *topology changed* (label/jump added/removed/retargeted) → structural cone (§7).
   - **Compute the cone** = the set of `newLine`s to re-solve (§5).
   - **Selective re-solve:** a CFG component with **no** cone line is **skipped entirely** (its `DynamicFlow`
     is never built) and reuses remapped strings; a component **with** cone lines builds its `DynamicFlow`
     (cheap) but **extracts/solves only the cone lines**, reusing remapped strings for the rest.
   - **Merge** into a fresh `DocCache`: cone lines = freshly extracted; others = `oldLine→newLine` remapped.

## 5. Cone computation (no Z3) — a precision ladder
**Inputs:** the changed `newLine`s, the CFG (StaticFlow edges), per-line read/write sets (instantiate
opcodes for the reachable region — metadata only, no solving).
- **Tier 1 — static cone (reachability):** forward BFS/DFS over CFG edges from each changed line; cone =
  union of reachable lines. Sound; excludes everything before the edit and all unreachable components.
- **Tier 2 — dynamic cone (dirty-set with kill):** seed a **dirty set** with what each changed instruction
  *writes* (regs + flags; memory ⇒ §7). Forward-propagate: a line that **reads** a dirty item joins the cone
  and its **writes** join the dirty set; a line that **overwrites** a dirty reg without reading it makes it
  **clean** (the edit's effect is *killed*). Joins: dirty if any predecessor is. Loops: iterate to a
  fixpoint (monotone, finite ⇒ terminates). Short-lived registers ⇒ tiny cones.

`dynamic cone ⊆ static cone ⊆ reachable region`. The cone is based on *"the instruction changed,"* not
*"the value changed"* — so it may re-solve a few lines whose values turn out identical (harmless; proving
them equal would require solving, defeating the purpose).

## 6. Soundness invariants (MUST hold)
- A reused (non-cone) line's before/after symbolic state is identical to the previous run (same instructions
  on its dataflow path, same predecessors) ⇒ its strings are unchanged ⇒ reuse is correct.
- The cone is a **sound over-approximation**: when unsure, widen (memory, topology change, label add/remove,
  unresolved jump target ⇒ fall back to the static cone or the whole affected component).
- **Diagnostics** (usage-of-undefined, redundant, unreachable) are value-dependent ⇒ recomputed for cone
  lines, reused for non-cone lines.

## 7. Edge cases & fallbacks
- **CFG-topology change** ⇒ cone = lines reachable in *old ∪ new* graph from the change; in practice
  **re-solve the affected component(s) wholesale** (still skips untouched components).
- **Memory:** not sliceable ⇒ a memory **write** dirties all downstream memory **reads**.
- **Loops:** dirty propagation around back-edges runs to a fixpoint.
- **Pragmas:** diff/cone run on the **pragma-lifted effective lines** (`RewritePragmasForCfg`), preserving
  the effective↔display mapping (`#pragma assume HLT` ⇒ a component boundary the cone respects for free).
- **Partial mid-typing line** ⇒ `Mnemonic.NONE` ⇒ no instruction, often no cone.
- **Multi-entry / unreachable lines:** cone reachability seeds from `ComputeComponentEntryLines`, not line 0.

## 8. Correctness gate — the shadow oracle (reuse existing infra)
Invariant: **incremental output == full re-simulation output** for the same final text. Reuse
`SimResultComparer` + the shadow harness: a debug/shadow mode runs BOTH the incremental pass and a full
re-sim, diffs the `SimResultSet`s, and **logs any divergence** (an unsound cone). Unit tests assert
`Compare(incremental, full).IsEmpty` for: operand edit, reg→reg vs reg→imm, insert line, delete line,
comment-only edit, label add, jump retarget, edit inside a loop, and an edit in one of several components
(the others must be byte-identical AND not re-solved).

## 9. Milestones (each independently shippable + reversible)

> **Control (2026-06-14):** the switch is the **`AsmSim_Incremental` setting** (VS settings checkbox
> "Incremental simulation (experimental)" → `settings.json` → wire DTO `AsmSimSettings.Incremental` →
> `AsmSimulator.ApplySettings` instance `incremental_`), **runtime-settable with no restart** (applied to both
> the out-of-proc server and the in-proc simulator). Default OFF. The old `ASMDUDE_SIM_INCREMENTAL` env var now
> only seeds the initial value (back-compat). Env vars were abandoned for this because a long-lived parent
> `devenv.exe` passes a stale environment down the whole VS→LSP→AsmSim.Server tree (env is inherited at launch,
> never re-read), so toggling needed a full host-VS restart — the setting avoids that.
- **M0 — plumbing (no behavior change). ✅ DONE (2026-06-14).** Landed behavior-neutral:
  - **`AsmSim.InstructionDiff`** (`asm-sim-lib/InstructionDiff.cs`, pure/Z3-free) — `Instruction`
    (`(label, mnemonic, args)`, field-wise equality, parsed via the same `AsmSourceTools.ParseLine` the
    sim/`StaticFlow` use) + an LCS alignment exposing `NewToOld`/`OldToNew`, `AddedNewLines`/
    `RemovedOldLines`, and **`HasNoInstructionChange`** (Tier-0). Unit-tested by `Test_InstructionDiff`
    (10 tests: operand edit localized, blank-insert is Tier-0 + remaps, comment edit is Tier-0, insert/
    delete/label-add detected). This is the "line-delta" that dissolves the §6 line-key problem.
  - **`AsmSimulator`** retains `prevInstr_[uri]` and diffs each edit via `RetainAndDiff` →
    `RecordIncrementalDiff` (logs the cone summary under category `ASMSIM`/`[INC]`), called at the top of
    `RunSimulation`. Cleared in `CancelAndRemove`/`Dispose`. Gated by **`ASMDUDE_SIM_INCREMENTAL`** (default
    OFF): with the flag off the simulator is byte-for-byte unchanged. Test seam
    `ComputeIncrementalDiffForTest` exercises the real retain+diff path; host tests
    `AsmSimIncrementalDiffTests` (5) prove cold-first-run, cross-edit diff, Tier-0 blank-insert remap,
    per-URI history, and close-forgets-history.
  - **Deliberate deviation from the spec wording:** "stop discarding `cache_`" was NOT done in M0 — keeping
    the cache without remapping would change what the editor serves (a behavior change). Cache retention +
    remap is M1, where the Tier-0 reuse actually consumes it. M0 is observe-only.
  - **Shadow oracle:** the full incremental-vs-full comparison (`SimResultComparer`) can't run until there
    is an incremental OUTPUT to compare (M1+); M0 only computes/logs the diff. The oracle wiring lands with
    M1 reuse.
- **M1 — Tier 0 (skip-if-no-instruction-change). ✅ DONE (2026-06-14).** When an edit shifts only inert
  (blank/comment) lines, the cache is remapped through the line shift and Z3 is skipped entirely.
  - **Soundness refinement:** the reuse gate is **`InstructionDiff.HasOnlyInertChanges`**, NOT
    `HasNoInstructionChange`. A label-only line is `Mnemonic.NONE`, so adding/removing one passes the
    instruction check yet changes CFG topology — `IsInert` (no instruction AND no label) excludes it.
    Comment-on-instruction edits are still Tier-0 (the parser strips the comment ⇒ the instruction matches).
  - **`AsmSimulator`:** `committed_[uri]` pairs each completed run's instructions with the DocCache it
    produced (committed on both engines' SUCCESS path); `TryTier0Reuse` (called at the top of
    `RunSimulation`, gated by `ASMDUDE_SIM_INCREMENTAL`) diffs the edit, and on `HasOnlyInertChanges`
    `RemapCache`s the strings + diagnostics onto the shifted lines, installs the cache, and fires the same
    onProgress/onCompleted a real run would — no Z3. Declines (→ full sim) on any instruction/label change.
  - **Shadow oracle WIRED:** `AsmSimTier0ReuseTests` (10) assert `SimResultComparer.Compare(incremental,
    full).IsEmpty` over blank insert/remove, comment edit, comment-on-code edit, and diagnostic remap, plus
    negative cases (operand edit / label add / cold doc must decline). Test seam `SimulateTier0ForTest`
    drives the real `TryTier0Reuse` flag-independently.
  - **"No recomputation" proven directly:** `Tier0Reuse_CarriesOverCachedStrings_…` asserts the reused
    line's cached string is the SAME object instance (reference-identity) as in the baseline — a re-solve
    would allocate a new string. (Reference-identity, not the global `Z3ContextTracker` counter, because
    xUnit runs other context-creating classes in parallel ⇒ the global counter is not reliable here.)
  - **Component-engine baseline verified (the live-VS config):** `BlankLineInserted_ComponentEngine_…`
    commits the baseline with the **Component** (DynamicFlow) engine — the editor default — and proves it
    reuses to a full Component re-sim. Inert-only edits keep the CFG identical, so every DynamicFlow line
    state is unchanged; reuse is sound for the merge engine too. Full host suite green; solution builds clean.
  - Default still OFF (`ASMDUDE_SIM_INCREMENTAL`); flip to ON is deferred to after M3's cone oracle soak.
- **M2 — Tier 1 (static cone). ✅ DONE (2026-06-14).** A topology-preserving instruction edit re-solves only
  the forward dataflow cone (component engine) and reuses the remapped baseline for everything else.
  - **`asm-sim-lib/DataflowCone.cs`** (pure, Z3-free): `ChangedLineSeeds(diff)` = every `AddedNewLine` + the
    surviving successor of each deletion (the subtle case, found via the nearest matched old line after the
    gap); `StaticCone(newFlow, seeds|diff)` unions each seed's forward CFG-reachable set
    (`StaticFlow.FutureLineNumbers`); `IsTopologyPreserving(diff)` = no added/removed line carries a label or
    is control-flow (jump/call/ret/int) — the soundness guard. Unit-tested by `Test_DataflowCone` (11):
    excludes earlier lines + other components, deletion cones the successor, branch reaches both targets,
    merge edit excludes pre-merge code, + the 5 topology cases (plain edit/insert preserve; jump retarget /
    label add / labeled-line edit do not).
  - **Soundness:** a non-cone line is not forward-reachable from any changed instruction, so none of its CFG
    ancestors changed ⇒ its symbolic state (hence its strings) is unchanged ⇒ reuse is exact. The topology
    guard handles the one case forward-reachability misses — an edit that DELETES an in-edge to a node
    (label/jump change), which can change that node's merged value without it being downstream — by falling
    back to a full sim (conservative: editing a labeled instruction also falls back; M3 may refine).
  - **`AsmSimulator`:** `ComputeComponentLines` gained an `onlyLines` filter (skip a whole untouched
    component; within a touched component extract only cone lines). `TryConeComponentReuse` (in
    `RunSimulation` after the Tier-0 check, gated by `ASMDUDE_SIM_INCREMENTAL` + Component engine) computes
    the cone, builds the reuse-base (`RemapConeReuse` of non-cone lines), and runs
    `RunComponentSimulation(cone, reuseBase)` which pre-installs the reuse-base then overwrites cone lines
    with fresh values. Declines (→ full sim) when cold / topology-changed / on error.
  - **Oracle:** `AsmSimConeReuseTests` (7) assert `SimResultComparer.Compare(coneIncremental, full).IsEmpty`
    on the Component engine for operand/insert/delete + two-component edits, plus a reference-identity proof
    that the untouched component's cached strings are reused (its DynamicFlow never rebuilt), plus negatives
    (labeled-line edit / cold doc decline). Seam `SimulateConeForTest`. Full host suite green; solution clean.
- **M3 — Tier 2 (dynamic cone). ✅ DONE (2026-06-14).** Dirty-set + kill, tighter than the static cone.
  - **`asm-sim-lib/DynamicCone.cs`** (pure, Z3-free): `LineEffects` (per-line read/write regs+flags+mem, with
    `KillRegs` = the full-width writes safe to clear) + `Compute` = a monotone forward dataflow worklist
    (gen/kill, union meet ⇒ fixpoint, loops included). A line is in the cone iff it is a changed instruction
    or READS a dirty location; a clean-input full overwrite KILLS dirtiness; memory is conservative (a write
    never kills; once dirty, every later read is in the cone); a deletion seeds the deleted writes at the
    surviving successor. Unit-tested by `Test_DynamicCone` (7): clean-reader excluded, full-overwrite kill
    skips a later reader, partial write stays dirty, flags + memory propagate, loop fixpoint, deletion seed.
  - **SOUNDNESS SCOPE — labels-only:** the dynamic cone is sound for the editor's read/write CodeLens labels
    (a kill line recomputes the same value ⇒ its labels are unchanged), NOT for a full register DUMP (any
    dirty location makes every downstream line's dump differ). So `TryConeComponentReuse` uses the dynamic
    cone only when `!computeFullState_` (the editor's real mode) and the STATIC cone when dumping.
    `BuildLineEffects` instantiates opcodes for metadata only (no solving); `KillRegs` = writes whose dest is
    ≥32-bit (`RegisterTools.NBits`, since a 32-bit write zero-extends).
  - **Oracle:** `AsmSimDynamicConeReuseTests` (5, labels-only Component engine) assert
    `Compare(coneIncremental, full).IsEmpty` AND tightness via reference-identity: an independent line and a
    KILLED-register's downstream reader are reused (not recomputed), while a genuine dependent reader is
    re-solved to the new value. Pure cone units 29; full host suite green; solution clean.
  - **Topology relaxation ✅ DONE (2026-06-14).** The conservative "decline on any label/jump change" fallback
    is replaced by `DataflowCone.StaticConeWithTopology(oldFlow, newFlow, diff)`: seeds = changed instructions
    ∪ every new line whose set of incoming CFG edges differs from its old counterpart's (old edges mapped to
    new-line space via `OldToNew`; a deleted predecessor = a lost edge), then forward reachability in the new
    graph (the plan's "old ∪ new" cone, §7). So a retargeted jump seeds the jump + the old target (lost edge)
    + the new target (gained edge); editing a labeled line with no edge change stays tight (just its forward
    cone). `TryConeComponentReuse` rebuilds the old CFG from the retained `committed_.Lines` and uses this cone
    on a topology change instead of declining. Pure tests `Test_DataflowCone` (+2: jump-retarget seeds both
    targets, labeled-edit-without-edge-change stays tight); oracle `AsmSimConeReuseTests` (the old "declines"
    tests flipped to "reuses + equals full": labeled-line edit, jump retarget, label-add-resolves-jump).
  - **Automated soak ✅ DONE (2026-06-14).** `AsmSimIncrementalSoakTests` drives the REAL dispatch
    (`SimulateIncrementalForTest` → RunSimulation's Tier-0→cone→full) through a 7-edit cumulative battery
    (operand, insert, comment, blank, flag-input cmp, second-component, jump-retarget) and asserts
    incremental == full at EVERY step — catching state drift across chained reuses. Diagnostics are part of
    that comparison (the cmp/jne flag-input step exercises the dirty-branch concern) and stayed identical.
  - **Default flipped ON (2026-06-14).** The `AsmSim_Incremental` setting now defaults **true** (VSIX
    `AsmSimIncremental` `defaultValue: true` + `SettingsSyncService` fallback true + `BuildSimSettings ?? true`);
    runtime-toggleable, safe per-edit fallback to a full sim. A live-VS soak on real `.asm` files is the only
    remaining (manual) validation; turn the setting off if anything misbehaves. **M0–M3 COMPLETE.**
  - **Future direction (loops):** see `LOOP_SEMANTICS_RESEARCH_NOTES.md` — using a loop's inductive invariant
    (from abstract interpretation or Z3/Spacer CHC solving) as a "focus" to bound the cone at loops
    (cones-and-foci idea), so loop-carried dependencies become reusable instead of swept in by back-edge
    reachability. Exploratory, not committed.

## 10. New code (by location)
- `asm-sim-lib`: an **instruction diff** (LCS over `(label,mnemonic,args)`); a **cone** module (static
  reachability + dirty-set propagation) on `StaticFlow` + read/write sets — pure, testable, no Z3.
- `asm-sim-host-lib/AsmSimulator`: retained per-URI state; the edit pipeline in `InvalidateAndSimulate`/
  `RunSimulation`/`ComputeComponentLines` (skip non-cone components/lines; merge+remap `DocCache`); the
  flag; the shadow hook.
- Tests: `asm-sim-tests` (diff + cone units), `asm-sim-host-tests` (incremental==full shadow battery).

## 11. Risks / open questions
- **Diff stability:** index-compare over-invalidates on insert/delete; LCS on instruction tuples is required.
- **Topology-change detection must be conservative:** a missed topology change ⇒ unsound cone. Start by
  treating ANY change to a label/jump/branch line as topology-changed (whole-component fallback).
- **Memory precision:** the conservative rule may make memory-heavy files barely incremental; measure first.
- **Benefit shape:** single tight function ⇒ Tier 2 helps via *kill* but the cone may still be large;
  multi-component files ⇒ large wins. Instrument "lines re-solved / total".
