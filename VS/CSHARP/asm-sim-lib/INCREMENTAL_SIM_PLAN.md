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
Merge-at-join (DynamicFlow) ≠ the linear sim's label-reset heuristic (`LspAsmSimulator.cs:~676`). Per-
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
- **S1 — DynamicFlow construction (Part A, items 1-6)** behind the existing 178 sim tests **plus** new
  gate tests: multi-entry (§2), self-loop+diamond (§3), `ret`/`jmp` terminal (item 5 — write the
  failing repro FIRST), parallel-Random race (§1). No editor change.
- **S2 — component extraction + bounded parallelism (Part B)**, gated by the flag, validated against
  S0 shadow diffs and the §7 memory bound.
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
   `LspAsmSimulator.ToResultSet(uri,label)` snapshots the editor sim. First consumer:
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
- Production (`LspAsmSimulator`, `DynamicFlow`) does **not** use the seeded ctor yet — the seam exists
  and is tested, but determinism is opt-in until the component driver adopts it.

### Headless characterization baseline landed (2026-06-11)
The editor's linear simulator now has a **golden, VS-free** test baseline — the oracle the S0 shadow
harness will diff the new engine against:
- **Seam:** `LspAsmSimulator.SimulateSynchronouslyForTest(uri, lines)` runs the real `RunSimulation`
  synchronously (no debounce/Task) — not a re-implementation.
- **Tests:** `asm-dude2-ls-tests/LspAsmSimulatorTests.cs` (xUnit, 4 tests, no VS): straight-line proven
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
engine), in `LspAsmSimulator`:
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
