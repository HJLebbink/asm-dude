# AsmSim out-of-process server (`AsmSim.Server.exe`) — design & protocol plan

**Status:** plan (2026-06-13). Not started.

## Why
The Z3 simulation runs **in-process inside the LSP server** today (`AsmSimulator` on background
threads, writing `DocCache`; hover/inlay/CodeLens/diagnostics read that cache). That couples a heavy,
crash-prone, memory-hungry workload to the latency-sensitive editor services:

- **Crash/OOM blast radius.** Z3 history here: the merge access-violation, the ~18 GiB native leak. In
  process, those kill syntax highlighting, completion, hover — everything. Out of process, a Z3 crash
  kills only the sim server; the LSP restarts it and the editor stays alive.
- **Responsiveness.** The sim shares the LSP process and `lockObj_` with request handling — the exact
  contention behind sluggish hover/CodeLens during a long sim. A separate process decouples them.
- **Resource control.** Cap cores/memory, lower its priority, kill+restart on edit — independently of the
  responsive LSP. A leak becomes a restartable subprocess, not a session-ending problem.

Note this is **orthogonal to Phase P parallelism** (which already works in-process): a separate process
doesn't make Z3 faster, it makes the system robust and non-blocking. Sequencing: prove the in-proc
parallel engine first, then lift it out.

## Architecture

```
 VS  ──LSP (named pipes)──▶  asm-dude2-ls  ──AsmSim JSON-RPC (stdio)──▶  AsmSim.Server.exe
     ◀────────────────────   (LSP server)  ◀──lineResults / simStatus──   (Z3, worker pool, DocCache)
     ──SimStatePipe──▶ (CodeLens; reads the LS's MIRROR cache — unchanged)
```

- **`AsmSim.Server.exe`** owns the simulation: `AsmSimulator`, the linear +
  component engines, the Phase-P worker pool, Z3, and the authoritative `DocCache`.
- **`asm-dude2-ls`** becomes a thin client: it forwards document changes + settings to the sim server,
  and **mirrors** the streamed per-line results into a local `DocCache`. All read paths
  (`GetHover`/`GetInlayHints`/`GetCodeLensData`/`GetSimStatesSummary`/diagnostics) read the mirror —
  **synchronous, no per-request IPC** — so hover latency is unchanged. This is the key design choice:
  **push-into-mirror**, not pull-per-request.
- **`asm-dude2-vsix`** is unaffected: it still talks to the LS's `SimStatePipeServer`, which now reads the
  mirror cache.
- **Ownership:** the LSP server spawns `AsmSim.Server.exe` as a child (as the VSIX spawns the LS) and owns its
  lifecycle. The VSIX never knows about it.

## Transport & wire format — DECISION

- **Transport: the sim server's stdin/stdout** (the LSP server is the parent and owns the pipes). The LS
  exe already supports a `--stdio` JSON-RPC mode; `AsmSim.Server.exe` does the same. No pipe-name coordination.
  Logs go to **stderr + file** (already the convention — the AsmLog console sink uses stderr precisely so
  it can't corrupt a stdio JSON-RPC channel).
- **Wire format: JSON-RPC 2.0 via `StreamJsonRpc`** — already a dependency, already used for the LSP
  connection, the team knows it. Typed methods, notifications, requests, cancellation, framed messages,
  error propagation, and it's debuggable (log the JSON like the LSP server does). The per-line payload is
  tiny strings; Z3 dominates, so serialization cost is irrelevant — no case for a custom binary protocol.
- **Shared contract types.** Put the request/result DTOs in a small **contract library both processes
  reference** (the same trick as `asm-options-lib` for `settings.json`), so the protocol is
  **compile-checked on both ends** — a renamed field is a build error, not a silent desync.

## Protocol (custom JSON-RPC, NOT full LSP)

It borrows LSP's didOpen/didChange/didClose *shape* for familiarity but is a small custom contract; the
per-line results aren't standard LSP.

### Client (LSP server) → Sim server

| Method | Kind | Params | Effect |
|---|---|---|---|
| `asmsim/initialize` | request | `{ clientPid, settings }` | Start; returns `{ engine, parallelism, serverVersion }`. |
| `asmsim/settingsChanged` | notification | `{ settings: AsmSimSettings }` | Update engine/loop/parallelism/timeout/arch; re-sim open docs if a sim-affecting field changed. |
| `asmsim/documentChanged` | notification | `{ uri, version, text, assembler }` | Replace doc; **cancel** any in-flight sim for `uri`; start a new sim at `version`. (Full text — the sim re-parses + re-simulates; matches today's invalidate-and-simulate.) |
| `asmsim/documentClosed` | notification | `{ uri }` | Cancel + drop cache for `uri`. |
| `asmsim/shutdown` / `asmsim/exit` | request / notification | — | Graceful stop. |

`AsmSimSettings` = the sim-relevant subset only: `AsmSim_On`, `engine` (linear\|component\|shadow),
`loopHandling`, `parallelism`, `z3TimeoutMs`, and the arch flags/profile that change which instructions
simulate. (Derived from the existing `AsmSettingsData`; reuse those fields.)

### Sim server → Client (notifications)

| Method | Params | Client action |
|---|---|---|
| `asmsim/lineResults` | `{ uri, version, lines: [{ line, before?, after?, readLabel?, writeLabel?, diagnostics?: [{kind,message}] }] }` | Mirror each line into the local `DocCache`; ignore if `version < current`. **Batched** on the existing ~400 ms throttle (the current per-line `onProgress`) → few messages, not one per line. |
| `asmsim/simStatus` | `{ uri, version, state: started\|progress\|completed\|cancelled\|failed, linesWritten?, diagCount?, slowLines?, totalMs?, message? }` | On `progress`/`completed`: publish diagnostics + trigger inlay/CodeLens refresh (the LS already does this) + `SimStatePipeServer.NotifySimStateUpdated(uri, completed)`. |

### Versioning & cancellation
Every server→client message carries `version`. The client **discards** results older than its current
document version (an edit superseded them); the server **self-cancels** superseded versions (today's
per-`uri`/`version` `CancellationToken`). `documentChanged` is the implicit cancel+restart.

### Lifecycle / restart (robustness — the whole point)
- LSP `initialize` → spawn `AsmSim.Server.exe`, send `asmsim/initialize` + `settingsChanged` + one
  `documentChanged` per already-open doc.
- **Pipe disconnect (Z3 crash/OOM)** → LSP logs, respawns the exe, re-sends settings + all open docs
  (re-sim from scratch). The mirror cache is kept (shown stale) until fresh results arrive. Add a simple
  restart backoff + a cap (e.g. 5 restarts/min) to avoid crash loops.
- Memory bound stays the server's job: `Z3ContextTracker` + the Phase-P `SemaphoreSlim`. A restart resets
  it — the leak's blast radius is now a subprocess.

## Project structure

- **NEW `asm-sim-host-lib`** — move `AsmSimulator` (→ `AsmSimulator`), `DocCache`, the component-engine
  glue (`ComputeComponentLines`, `CloneLineForWorker`, …), `EnableFullStateConfig`, etc. Depends on
  `asm-sim-lib` + `asm-tools-lib`. No editor/LSP types.
- **NEW `asm-sim-server` (exe = `AsmSim.Server.exe`)** — hosts `asm-sim-host-lib` behind a `StreamJsonRpc`
  stdio server implementing the protocol above. Mirror of how `asm-dude2-ls` hosts `asm-dude2-ls-lib`.
- **NEW contract lib** (or fold into `asm-options-lib`) — the `AsmSimSettings` + result DTOs, referenced by
  both `asm-sim-server` and `asm-dude2-ls-lib`.
- **CHANGED `asm-dude2-ls-lib`** — remove the in-process `AsmSimulator`; add `AsmSimClient` (process
  launcher + `StreamJsonRpc` stdio client + the `lineResults`/`simStatus` handlers) and a **`DocCache`
  mirror**. `UpdateInternals` routes `documentChanged` to the client instead of calling
  `InvalidateAndSimulate`. Read paths read the mirror — minimal change.
- **CHANGED `asm-dude2-vsix`** — bundle `AsmSim.Server.exe` into the VSIX `Server/` dir next to the LS exe (the
  existing post-build copy step), so it ships together.

## Migration — phased & reversible (mirrors the engine-swap approach)

- **S1 — Extract (no behavior change). ✅ DONE 2026-06-13.** `AsmSimulator.cs` (incl. `DocCache`,
  `SimDiagnostic`/`Kind`, `ComponentLine`, the component-engine glue + Phase-P pool) moved via `git mv` into
  the new **`asm-sim-host-lib`** project (net10.0-windows; refs asm-sim-lib + asm-tools-lib + asm-options-lib;
  NO LSP-protocol dependency). Namespace kept `AsmDude2LS`; `InternalsVisibleTo` → ls-lib, ls-tests, asm-fuzz.
  `asm-dude2-ls-lib` now references it. **Zero code changes** in `LanguageServer`/`LanguageServerTarget`/
  `SimStatePipeServer`/tests — only the project reference. Full solution builds 0/0; merge tests green.
  *Still TODO in S1:* define the `AsmSimSettings` + result DTOs in the contract lib (deferred to S2 since
  they're only needed at the process boundary).
- **S2 — Build the server + client, shadow it.**
  - **S2a — server half. ✅ DONE 2026-06-13.** `AsmSimProtocol.cs` (the wire DTOs) added to
    `asm-sim-host-lib`. New **`asm-sim-server` exe (AssemblyName `AsmSim.LS`)**: `Program.cs` hosts a
    `StreamJsonRpc` connection over stdin/stdout; `AsmSimRpcServer` is the target — `documentChanged`
    drives the one `AsmSimulator`, and its throttled `onProgress`/`onCompleted` snapshot the cache
    (`ToResultSet`) and stream `asmsim/lineResults` + `asmsim/simStatus` back, version-stamped. Logs to
    stderr + `asmdude-simserver.log` (never stdout). Builds 0/0; full solution 0/0. (`InternalsVisibleTo`
    uses the **assembly** name `AsmSim.LS`; MessagePack pinned 3.1.7 for the StreamJsonRpc transitive vuln.)
  - **S2b — client half. ✅ DONE 2026-06-13.** Solved the read-surface problem WITHOUT a split: the LS's
    `AsmSimulator` stays the single read source; in out-of-proc mode it just doesn't run Z3 — the client
    FEEDS its cache. New `AsmSimulator.ApplyLineResults(uri, version, lines)` (mirror write: rebuilds the
    doc from each authoritative snapshot, drops stale versions, parses `"Kind:Message"` diagnostics back).
    New `AsmSimClient` (in `asm-dude2-ls-lib`): `TryCreate` launches `AsmSim.Server.exe` (via
    `ASMDUDE_SIM_SERVER_PATH` or next to the LS exe; null ⇒ in-proc fallback), JSON-RPC over its stdio,
    `documentChanged` (client owns the version counter), and `[JsonRpcMethod]` handlers for
    `lineResults`→`ApplyLineResults`+onProgress and `simStatus(completed)`→onCompleted. `LanguageServer`
    dispatches `UpdateInternals` via `EnsureSimClient()` (lazy launch, behind **`ASMDUDE_SIM_OUTOFPROC=1`**,
    DEFAULT in-proc so untouched/safe); disposes the client + sends `documentClosed`. Server notifications
    switched to positional single-object so client/server bind symmetrically. Full solution 0/0.
    **Tests (11 new, `asm-sim-host-tests`):** `AsmSimMirrorTests` (5 — ApplyLineResults round-trip, diag
    parse, snapshot-replace, stale-drop, empty-clear), `AsmSimProtocolSerializationTests` (5 — every DTO
    round-trips through System.Text.Json), and **`AsmSimServerEndToEndTests`** — the real `AsmSimRpcServer`
    ↔ a mirror client over an in-memory full-duplex stream (no process launch): `documentChanged "mov rax,
    0x10"` → real sim → streamed `lineResults` → mirror shows `RAX = 0x…0010`. So the whole protocol +
    server + streaming + mirror path is PROVEN, independent of S3 deployment.
  - **S2c — ✅ DONE 2026-06-13.** Restart/resync: `AsmSimClient` detects the JSON-RPC `Disconnected` event
    (server crash/exit), RESPAWNS the server (capped: 5 restarts / 60 s, then gives up), and RE-SENDS every
    open document (it tracks `openDocs_` = uri→version+lines+callbacks) with bumped versions so late results
    from the dead process are dropped. Settings applied by launching the server with `ASMDUDE_SIM_ENGINE`/
    `_LOOP`/`_PARALLEL` env from `AsmSimSettings`. Shadow-diff: `OutOfProcess_MatchesInProcess_OnStraightLine`
    runs the same program in-process and through the in-memory server and asserts an IDENTICAL
    `SimResultSet` via `SimResultComparer` — proving the RPC/streaming/mirror transport is faithful (the
    parity gate for the flip).
- **S3 — bundle ✅ DONE 2026-06-13; flip/cleanup TODO.** `asm-dude2-ls.csproj` now builds `asm-sim-server`
  alongside it (ProjectReference, `ReferenceOutputAssembly=false`) and the `BundleAsmSimServer` target
  copies its output next to `AsmDude2.LSP.exe`, so `AsmSim.Server.exe` ships in the VSIX `Server/` dir and
  `AsmSimClient` finds it. *Still TODO:* flip the default to out-of-process and delete the in-proc path —
  **only after live editor validation** (keep the flag as the escape hatch).

---

## Remaining work (prioritized) — 2026-06-13

**Server split (this doc):**
1. **Live validation. ✅ DONE 2026-06-13 (HJL in VS).** `ASMDUDE_SIM_OUTOFPROC=1`: `asmdude-simserver.log`
   appears, lenses stream, the runtime engine switch works. Crash-respawn + the settings push also have
   automated coverage (`AsmSimClientRespawnTests`, `AsmSimSettingsChangedTests`).
2. **Flip default. ✅ DONE 2026-06-13.** Out-of-process is now the default (`LanguageServer.SimOutOfProc`);
   opt OUT with `ASMDUDE_SIM_OUTOFPROC=0`/`=inproc`. The in-process `RunSimulation` path is **kept** as the
   automatic fallback (server exe not found / launch fails) and the opt-out — deleting it is deferred (no
   benefit while it's the safety net). Unit tests pin in-proc via a `[ModuleInitializer]`.
   **Engine default: ✅ component (decided 2026-06-13, HJL).** `AsmSimulator.ParseEngine` + the out-of-proc
   `BuildSimSettings` both default to `component` (branch-aware, ~on par with linear, strictly more correct);
   force the old single-path engine with `ASMDUDE_SIM_ENGINE=linear`.
3. **Runtime `settingsChanged`. ✅ DONE 2026-06-13.** `LanguageServer.Initialize` (fired by
   `SettingsManager.SettingsChanged`) → `AsmSimClient.SettingsChanged` → pushes `settingsChanged` (server
   `ApplySettings` applies engine/loop in place) + re-sends open docs. Tested end-to-end.

**Engine precision/perf (`INCREMENTAL_SIM_PLAN.md`, independent of the split):**
5. **Phase 3 — incremental invalidation. Detailed spec written 2026-06-13** (`INCREMENTAL_SIM_PLAN.md`
   "Phase 3 — … via the dataflow cone"). Recompute only the forward **dataflow cone** of an edit, reuse
   cached strings for the rest; an instruction-level diff (not line keys) supplies the line-delta, dissolving
   the old §6 re-keying blocker. Milestones M0 (plumbing+shadow oracle) → M1 (skip-if-unchanged) → M2 (static
   cone) → M3 (dynamic cone), each shippable. **✅ M0–M3 DONE (2026-06-14)** with an `incremental==full`
   oracle throughout; controlled by the **`AsmSim_Incremental` setting** (VS checkbox / settings.json,
   runtime, no restart), default OFF. *Remaining before flipping default ON:* a real-`.asm` shadow soak in
   live VS; a real old-vs-new CFG-edge diff to relax the conservative topology guard (labeled-line /
   jump-operand edits currently full-sim); confirm a reused line's unreachable/usage-undefined diagnostics
   can't depend on a dirty branch condition.
6. **Solver-level (ITE) merge. ✅ DONE 2026-06-13.** The branch join is now the symbolic ITE/phi merge —
   the editor reads `DynamicFlow.Create_States_Before/After` (`MergeConstructor`) directly; the Tv-level
   `ComponentEvaluator` was deleted. Cross-register facts are preserved and per-vertex solving is gone
   (~100× faster; see `INCREMENTAL_SIM_PLAN.md`).
7. **Loop-precision default.** Currently `accept` (least precise); decide whether `fixpoint`/`fullunroll`
   should be the default.

**Parked / known (outside both plans):**
8. **CodeLens scroll redraw** — parked by HJL; unresolved (the publish↔request warning still fires).
9. **Overall sim speed** — component now reads DynamicFlow's symbolic states + solves only displayed
   registers per line on the Phase-P pool: the 150-line example is ~8.7 s (was a >4-min timeout). Remaining
   cost is the per-line Z3 solve of the displayed registers.

## Open questions / risks
- **Settings duplication:** sim-affecting settings now flow to two processes (VSIX→LS via settings.json,
  LS→sim via `settingsChanged`). The contract DTO keeps them in sync; decide whether the sim server reads
  settings.json directly (it could, via a `FileSystemWatcher`, removing one hop) or only via the LS.
- **Stdio vs named pipe:** stdio is simplest (no name coordination). If we ever want a *third* client
  (e.g. a CLI replaying corpora against the sim server), a named pipe is more flexible. Start with stdio.
- **Full-text vs incremental document sync:** start with full text per `documentChanged` (simple; the sim
  re-runs fully on every edit anyway). Incremental sync is a later optimization tied to Phase 3
  (recompute-only-touched-component).
- **Back-pressure:** if edits arrive faster than sims finish, the client should debounce `documentChanged`
  (the LS already debounces ~100 ms) and the server cancels superseded versions — already the model.
- **This is a sizable refactor.** S1 is safe and worth doing regardless (cleaner module boundary). S2/S3
  are the real cost; do them only once the in-proc parallel engine has proven the speedup is worth shipping.
