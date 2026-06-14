# AsmSim diagnostics plan — restore redundant-instruction warnings + enrich messages

Status: **IMPLEMENTED (2026-06-15).** Detects structural, value-rewrite, flag and memory redundancy on
both engines. Step 8 (richer Error-List fields) remains a documented follow-up.

## Implementation notes (final design)

Detection is symbolic and **computed before the prune**, so it catches BOTH structural and value
redundancy without retaining history or weakening Compress:

- **`Runner.IsRedundantInstruction(line, before)`** (engine) is the one place that answers "does this
  instruction change the tracked state?". It applies the instruction to an **unfrozen copy** of the
  before-state and queries `State.ComputeRedundancy(prevKey, nextKey, writtenRegs, writtenFlags, writesMem)`
  **while both keys are still live**, then discards the probe. Returns a `RedundancyVerdict`
  (`WrittenCount`/`RedundantCount`; `IsRedundant => Written>0 && Redundant==Written`), or `null` for a
  non-instruction / halted / not-implemented / mock-SIMD line.
- **`State.ComputeRedundancy`** reuses the existing `Is_Redundant`/`Is_Redundant_Mem` primitives (symbolic,
  so it handles unknown-but-equal as well as concrete-equal — strictly better than the deleted `Tv`-value
  compare, which only did concrete and cost extra solves).
- **Host `AsmSimulator.TryCollectRedundant`** is now just policy + reporting: gate on the setting, skip CFG
  branch/merge points, call `Runner.IsRedundantInstruction`, emit the diagnostic. No engine internals, no
  after-state.

### Why this design (the root cause it sidesteps)

When a `State` is **frozen** (`Frozen` setter → `Simplify()` → `Remove_History()` → `Compress(keep)`) the
solver is reset and only assertions reachable from the current-key roots survive — a GC of dead history
that keeps Z3 models small/swift (essential; introduced `fe50f29`, 2017-06-28, present continuously since —
so register value-rewrite redundancy was never provable on the *frozen* after-state, in 2019 or ever;
git-bisect-verified, run-confirmed LOST at `e81ab4e`/`b3b9046`, 2023). An overwritten register's prior-key
binding (`RAX!key1=0x10`) is unreachable from `RAX!key2` and is dropped. The fix is **ordering, not
retention**: ask the redundancy question on the still-unfrozen probe (where `RAX!key1` is present), which
is exactly what `Runner.IsRedundantInstruction` does. Compress is untouched; persistent states stay compact.

- Structural redundancy (`mov rax,rax`) and memory redundancy work even on the frozen state — the former
  because the update asserts `RAX!key2=RAX!key1` directly, the latter because memory is a Z3 array whose
  update chain links the keys.
- Tests: `Test_State.Test_State_Frozen_DropsOverwrittenRegisterHistory` characterizes the (intentional)
  freeze GC; `Test_Runner.Test_Runner_IsRedundant_*` test the primitive directly (value rewrite, structural
  identity, state-changing negatives, NOP/non-instruction); `AsmSimulatorTests.Redundant_*` exercise the
  full editor path on both engines.
- **Branch/merge guard:** the component engine precomputes a `branchMergeLines` set from `StaticFlow`
  (`Is_Branch_Point`/`Is_Merge_Point`) and passes it to `ExtractComponentLine`; the linear engine passes
  `false` (single-path, and branch instructions write nothing so are skipped by the "writes ≥1" rule).
- **Squiggle color:** left as-is per decision. `Redundant` is `DiagnosticSeverity.Warning`, code `SIM-W003`.

## Background / why

AsmDude**1** (the legacy VS2015/17/19 in-proc plugin, `old/asm-dude-vsix/Tools/AsmSimulator.cs`,
deleted in commit `33a0376`) had a full set of Z3-backed semantic diagnostics, each gated by its own
settings pair, rendered through two independent VS surfaces (`SquigglesTagger` for the editor squiggle,
the `ErrorListProvider` Task API for the Error List).

The LSP rewrite (`asm-sim-host-lib/AsmSimulator.cs` + `asm-dude2-ls-lib/LanguageServer.cs`) ported only
**three** of those checks and dropped the per-category gating:

| Diagnostic | AsmDude1 | AsmDude2 (today) |
| --- | --- | --- |
| Syntax error | yes | yes (`SimDiagnosticKind.SyntaxError`) |
| Usage of undefined | yes | yes (`UsageUndefined`) |
| Unreachable | yes | yes (`Unreachable`) |
| Not implemented | yes | yes (`NotImplemented`) |
| **Redundant instruction** | **yes** | **MISSING** |
| Per-category `Show_*`/`Decorate_*` gates | yes (`PreCalculate_LOCAL`) | **MISSING** (only master `AsmSim_On`) |

Everything for redundant exists *except the middle*: the Z3 primitive `State.Is_Redundant(...)`
(`asm-sim-lib/State.cs:603/632/661`), the settings DTO fields
(`AsmSettingsData.AsmSim_Show_Redundant_Instructions` / `_Decorate_Redundant_Instructions`,
`asm-options-lib/AsmSettingsData.cs:288-290`), and the VSIX writer
(`SettingsSyncService.cs:157-158`). There is **no detection, no enum value, no LSP mapping, and the two
settings reach nothing**. `State.Is_Redundant` is currently called only from `asm-sim-tests/Test_State.cs`.

## Redundancy semantics (fixed by `Test_State.cs:59-120`)

For a line `i`, evaluate on the cumulative **after**-state (which holds both keys' constraints):

```
afterState.Is_Redundant(reg,  beforeKey, afterKey)   // reg unchanged across the step
afterState.Is_Redundant(flag, beforeKey, afterKey)   // flag unchanged
afterState.Is_Redundant_Mem(beforeKey, afterKey) == Tv.ONE   // memory unchanged
```

where `beforeKey` / `afterKey` are the keys flanking the instruction. An instruction is **redundant**
iff it writes ≥1 location AND every written reg + flag (+ mem) is provably unchanged.

`Is_Redundant` returns true only on a **provable** equality, so a missing constraint can only cause a
**false negative** (a miss), never a false positive — safe by construction.

### AsmDude1's `Calculate_Redundant_Instruction_Warnings` — guards to copy

Faithful port must reproduce these skips (the first draft of this plan missed them):

- `mnemonic == NONE` → skip
- `mnemonic == NOP` → skip (intentionally redundant; never warn)
- `Runner.InstantiateOpcode(...) == null` → skip (not implemented)
- `op.GetType() == typeof(DummySIMD)` → skip (mock SIMD)
- `op.IsHalted` → skip (syntax error already reported)
- **branch point** (`Is_Branch_Point`) → skip
- **merge point** (`Is_Merge_Point`) → skip (redundancy at a phi/join is unsound)

`Is_Branch_Point` / `Is_Merge_Point` exist today: `asm-sim-lib/StaticFlow.cs:427/433`,
`asm-sim-lib/DynamicFlow.cs:83/93`.

### Write-set vs whole-`StateConfig` scan

AsmDude1 scanned **every** `StateConfig`-on reg/flag/mem (`GetRegOn`/`GetFlagOn`). This plan scans only
`op.RegsWriteStatic`/`FlagsWriteStatic`/`MemWriteStatic` instead. The two are **equivalent** (a register
the instruction does not write carries the same symbolic var across `beforeKey`/`afterKey`, so it is
trivially redundant), but the write-set form does ~1-3 solves/line instead of ~23. It relies on the
static write-set being authoritative — which it is, because `SimpleStep_Forward` applies exactly those
writes. Treat `Is_Redundant_Mem == Tv.UNKNOWN` as **not** redundant (conservative).

---

## In-scope work (do now)

### 1. New diagnostic kind

`AsmSimulator.cs:39` — add `Redundant` to `SimDiagnosticKind`. The out-of-proc mirror parser at
`AsmSimulator.cs:322-332` already round-trips any kind via `Enum.TryParse`, so no change there.

### 2. Thread the setting into the simulator

- `AsmSimProtocol.cs:31` — add a **trailing, defaulted** field `bool ShowRedundant = false` to the
  `AsmSimSettings` record (trailing + default keeps all existing positional `new AsmSimSettings(...)`
  test constructors compiling).
- `LanguageServer.cs:133` `BuildSimSettings()` — set
  `ShowRedundant: (options?.AsmSim_Show_Redundant_Instructions ?? false) || (options?.AsmSim_Decorate_Redundant_Instructions ?? false)`.
  This is AsmDude1's `update_Redundant = AsmSim_On && (Show || Decorate)` **production gate** — do not pay
  the extra Z3 solves unless at least one surface wants the result.
- `AsmSimulator.ApplySettings` (`:598`) — add a `bool showRedundant` parameter; store in a new
  `private bool showRedundant_` field. Update the **3** call sites: `LanguageServer.cs:472`,
  `asm-sim-server/AsmSimRpcServer.cs:41` and `:50`.

### 3. Detection method

New `private void TryCollectRedundant(AsmSimState before, AsmSimState after, OpcodeBase? op, int lineIndex, AsmSimTools tools, List<SimDiagnostic> diags)`:

1. early-out if `!showRedundant_`;
2. apply all AsmDude1 guards above (NOP, DummySIMD, null op, IsHalted, branch/merge point);
3. require the instruction writes ≥1 location;
4. for each written reg (`op.RegsWriteStatic`, 64-bit-normalized, `StateConfig`-on) require
   `after.Is_Redundant(reg, beforeKey, afterKey)`; same for each `op.FlagsWriteStatic`; if
   `op.MemWriteStatic`, require `after.Is_Redundant_Mem(beforeKey, afterKey) == Tv.ONE`;
5. if all hold, `diags.Add(new SimDiagnostic(lineIndex, message, SimDiagnosticKind.Redundant))` where
   `message` is built per step 7.

### 4. Wire both engines

- **Linear** (`RunSimulation`, `:1910`): the after-state `next` is computed at `:1919`, **after**
  `CollectDiagnostics`. Add the call right after `next` is obtained:
  `TryCollectRedundant(stateBeforeStep, next, opcodeBase, i, tools, lineDiags)`. Keys:
  `beforeKey = stateBeforeStep.HeadKey`, `afterKey = next.HeadKey` (matches the `Test_State.cs` convention).
- **Component** (`ExtractComponentLine`, `:1270`): both `before`/`after` are in hand; call after `:1303`.
  **Risk to verify with a test:** the component `after` clone must carry `before`'s key constraints in
  its solver. If not, redundancy under-reports (never over-reports). If a test shows under-reporting,
  source the keys from the DynamicFlow vertex (`dFlow.Key` / `Key_Next`, as AsmDude1 did) or restrict the
  feature to the linear engine.

### 5. LSP mapping — **all categories unconditional** (the per-category gate was removed)

`LanguageServer.SendDiagnostics`. Map the new `Redundant` kind (severity `Warning`, code `SIM-W003`, plain
squiggle — no `Unnecessary` fade). **Every** sim category — syntax / usage-of-undefined / unreachable /
not-implemented / redundant — is surfaced **unconditionally** whenever the simulator produced it. There is
no per-category `Show_*`/`Decorate_*` gate in the LSP mapping.

> **WHY no per-category gate (do NOT re-add one keyed on the flags).** The VSIX setting definitions carry
> `EnabledWhen = SettingRule.Equal(AsmSimOn, true)`, so VS reports **every** `Show_*`/`Decorate_*` as
> `false` whenever the master `AsmSim_On` is off — and a stale `settings.json` can pin them false anyway.
> The simulator runs and emits unreachable/undefined regardless (the host never reads `AsmSimOn`), so
> gating on those flags makes diagnostics vanish in exactly the state where they are otherwise shown. The
> first attempt gated only `Redundant`; that made redundancy the lone category that never appeared while
> the user got 8 unreachable warnings. **Resolution (2026-06-15): redundancy is unconditional like the
> rest.** Detection is forced on in production (`BuildSimSettings` passes `ShowRedundant: true`); the host
> `showRedundant_` field survives only as a **test-only opt-out seam**
> (`Redundant_Disabled_ProducesNoRedundantDiagnostics`). The VSIX redundant `Show/Decorate` checkboxes are
> now inert — reinstating real per-category gating requires first decoupling the settings from `AsmSim_On`
> (drop the `EnabledWhen`) AND having the host honor `AsmSimOn` so "sim off" genuinely emits nothing.
- **`Redundant` severity:** `DiagnosticSeverity.Warning`, new code `"SIM-W003"` — matches AsmDude1's
  "Semantic Warning". (Optionally add `DiagnosticTag.Unnecessary` when `Decorate` is on to grey the line,
  the VS idiom for redundant code; default to a plain squiggle to keep appearance conventional.)

### 6. Tests (meaningful — must fail if the production code is wrong)

`asm-sim-host-tests/AsmSimulatorTests.cs`, driven by `SimulateSynchronouslyForTest` (Linear engine):

- positives flagged: `mov rax, rax`, `add rax, 0`, `shl rax, 0`;
- negatives not flagged: `add rax, 1`, `mov rax, rbx`, `nop`;
- gate: `ShowRedundant = false` ⇒ zero `Redundant` diagnostics even for `mov rax, rax`.
- Component-engine variant to confirm parity (or to document the under-report fallback from step 4).

### 7. Richer message text (cheap — flows through the existing string field)

The full source line and concrete simulator values are available where the diagnostic is built
(`line` parameter; `before`/`after` states). Value API: `state.GetTvArray(reg64)` →
`ToolsZ3.ToStringHex`/`ToStringBin`, `state.GetTv(flag)` (the same calls `ComputeReadLabel`,
`AsmSimulator.cs:1591`, already makes).

- replace the bare `"{mnemonic}"` with the **full trimmed line** in every message;
- **usage-of-undefined:** e.g. `Usage of undefined value in "mov rax, rbx": RBX is undefined (0b????…) — no definition reaches this line.`
  (append the read register's `GetTvArray` bit pattern so the user sees which bits are unknown);
- **redundant:** e.g. `"add rax, 0" is redundant: RAX = 0x0000000000000010 is unchanged.`
  (append the proven value(s) from the after-state);
- **syntax / not-implemented / unreachable:** include the full line.

This step touches only `CollectDiagnostics` / `TryCollectRedundant` message construction — no protocol or
type changes, so it ships with steps 1-6.

---

## 8. FOLLOW-UP (deferred): structured Error-List enrichment

> **Not in the first change.** This is documented here so it is not lost. It has a real cost: it widens
> the `SimDiagnostic` type **and the out-of-process wire contract**, so it should be a deliberate,
> separately-reviewed change rather than bolted onto steps 1-7.

### Goal

Populate the `Diagnostic` / `VSDiagnostic` fields we currently leave empty, so AsmSim findings are
first-class Error-List citizens: a clickable doc link, a category column, an expanded description, and a
custom squiggle tooltip.

### Available fields (audited against `Protocol` / `Protocol.Extensions` 18.5.3)

Standard LSP `Diagnostic`: `Code`, **`CodeDescription`** (`{ Href }` — rendered as a clickable link from
the code in the Error List), **`RelatedInformation`** (list of `location + message` → expandable
sub-rows), `Source`, `Tags`.

`VSDiagnostic` extensions: **`ExpandedMessage`** (expanded description), **`ToolTip`** (custom text shown
when hovering the squiggle — distinct from `Message`), **`DiagnosticType`** (a category string —
"Correctness", "Performance", … → shows as the Error-List **Category** column), **`DiagnosticRank`**
(`Lowest…Highest`, default sort), `Identifier` (dedup of equivalent diagnostics), `Projects`.

### What to set

- `Source = "AsmDude2 Simulator"` — distinguish sim diagnostics from label/syntax ones.
- `CodeDescription.Href = wikiUrl(mnemonic)` — reuse the per-mnemonic documentation URL `HoverBuilder`
  already builds.
- `VSDiagnostic.ExpandedMessage` = the line's compact before/after state.
- `VSDiagnostic.ToolTip` = before/after state, for squiggle hover.
- `VSDiagnostic.DiagnosticType` = `"Correctness"` for syntax/undefined/unreachable, `"Performance"` for
  redundant.
- `VSDiagnostic.DiagnosticRank` = `High` for errors, `Default` otherwise.
- `RelatedInformation` only when provenance is known (redundant: the earlier line that proves the value;
  undefined: the definition site) — **best-effort/optional**, because the simulator does not currently
  retain definition sites. Adding provenance tracking is its own sub-task.

### Plumbing cost (the reason this is deferred)

1. **Widen `SimDiagnostic`** (`AsmSimulator.cs:47`) from `(int Line, string Message, SimDiagnosticKind Kind)`
   to also carry optional `ExpandedMessage`, `ToolTip`, `DiagnosticType`, `DocUrl`, and `RelatedInfo`
   (line + text). Update the `with { Line = … }` remaps (`RemapCache` `:735`, `RemapConeReuse` `:856`).
2. **Widen the wire contract.** `AsmSimLineDto.Diagnostics` (`AsmSimProtocol.cs:49-55`) is currently
   `IReadOnlyList<string>` flattened as `"Kind:Message"`. Replace with a small DTO (or JSON-encode the
   extra fields) and update both ends:
   - producer: `ToResultSet` (`AsmSimulator.cs:499-507`) and `BuildComponentResultSet` (`:1016-1031`);
   - consumer: `ApplyLineResults` parser (`AsmSimulator.cs:322-332`).
3. **`SendDiagnostics`** (`LanguageServer.cs:903-952`) — read the new `SimDiagnostic` fields and set the
   `Diagnostic`/`VSDiagnostic` properties above.

### Out of scope (explicitly): custom squiggle color

Squiggle color is bound to `DiagnosticSeverity` (Error = red, Warning = green/teal, Information = faint
gray dotted, Hint = "…" suggestion dots). **There is no LSP field for an arbitrary color**; `DiagnosticType`
and `VSDiagnosticTags` change the Error-List category/visibility, not the squiggle color. A genuine
yellow/purple AsmSim color would require an in-proc MEF `ErrorTypeDefinition` + `EditorFormatDefinition`,
which the OOP/LSP extension cannot register (the same heavyweight in-proc hybrid the archived QuickInfo
used). **Decision (2026-06-15): leave squiggle colors as-is.** Severities keep their current defaults;
`Redundant` is a `Warning` (green), consistent with AsmDude1's "Semantic Warning".

---

## File reference index

| Concern | Location |
| --- | --- |
| `SimDiagnosticKind` enum | `asm-sim-host-lib/AsmSimulator.cs:39` |
| `SimDiagnostic` record | `asm-sim-host-lib/AsmSimulator.cs:47` |
| `CollectDiagnostics` (4 existing kinds) | `asm-sim-host-lib/AsmSimulator.cs:2049` |
| Linear engine diag call / after-state | `…/AsmSimulator.cs:1910` / `next` at `:1919` |
| Component engine diag call | `ExtractComponentLine`, `…/AsmSimulator.cs:1303` |
| Out-of-proc diag parse / serialize | `…/AsmSimulator.cs:322-332` / `499-507` |
| Value extraction example | `ComputeReadLabel`, `…/AsmSimulator.cs:1591` |
| `ApplySettings` + call sites | `…/AsmSimulator.cs:598`; `LanguageServer.cs:472`; `AsmSimRpcServer.cs:41,50` |
| `AsmSimSettings` / `AsmSimLineDto` records | `asm-sim-host-lib/AsmSimProtocol.cs:31` / `:49` |
| `BuildSimSettings` | `asm-dude2-ls-lib/LanguageServer.cs:133` |
| `SendDiagnostics` (LSP mapping) | `asm-dude2-ls-lib/LanguageServer.cs:895-970` |
| `AsmDiagnosticTag` / `VSDiagnosticTags` | `asm-dude2-ls-lib/AsmDiagnosticTag.cs` |
| `Is_Redundant` primitives | `asm-sim-lib/State.cs:603/632/661` |
| `Is_Branch_Point` / `Is_Merge_Point` | `asm-sim-lib/StaticFlow.cs:427/433`, `DynamicFlow.cs:83/93` |
| Redundant settings (DTO) | `asm-options-lib/AsmSettingsData.cs:288-290` |
| VSIX settings writer | `asm-dude2-vsix/Settings/SettingsSyncService.cs:157-158` |
| AsmDude1 reference impl (deleted) | `old/asm-dude-vsix/Tools/AsmSimulator.cs` @ `33a0376^`; `SquigglesTagger.cs` |
