# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Important Guidelines

**MINIMIZE USER INTERRUPTIONS**: Only ask the user for help as a LAST RESORT. Before asking:
1. Try to test and verify changes yourself using command-line tools
2. Use `dotnet build` to check if code compiles
3. Run the LSP server directly with test inputs to verify behavior
4. Check log files and debug output
5. Search documentation and code for answers
6. Only ask the user when you have exhausted all self-service options and are completely blocked

**DO NOT STOP WHEN YOU HAVE COMPILATION ERRORS**: Continue implementing features and fixing compilation errors incrementally. Create new features/files even if earlier files have compilation issues. The build will eventually succeed as you address errors systematically.

**ABSOLUTE REQUIREMENT — TESTS MUST BE MEANINGFUL**: Every test must exercise real production code and be capable of catching a real bug. The following are forbidden:
- Tests that only test a local helper function written inside the test file itself (tautological — they can never fail due to a bug in production code)
- Tests that re-implement logic from production code in the test and verify that re-implementation (testing a copy, not the original)
- Tests whose assertions would still pass even if the production code was deleted or completely wrong
- "Circus" tests: multiple tests dressed up as coverage that all test the same trivial condition

Before writing a test, ask: **"If I introduced a bug in the production code this test is supposed to cover, would this test fail?"** If the answer is no, do not write the test.

## Project Overview

AsmDude2 is a Visual Studio 2022/2026 extension that provides assembly language support (x86/x64, SSE, AVX, AVX2, AVX-512) through a Language Server Protocol (LSP) implementation. The project evolved from the original AsmDude VS2015/17/19 plugin into a modern LSP-based architecture.

**✅ PACKAGE ISSUE RESOLVED**: Extension now uses stable, publicly available packages and supports **Visual Studio 2022 & 2026**.

## Architecture

### Active Extension

**asm-dude2-vsix** (net10.0-windows8.0) — **Modern out-of-process extension**
   - ✅ Uses VisualStudio.Extensibility SDK v18.8.1030-Preview (matched to the installed VS 18.8; see matching rule below)
   - ✅ LSP-based architecture (launches separate asm-dude2-ls process)
   - ✅ Targets Visual Studio 2022 & 2026
   - ✅ Fully debuggable with F5 (debug target provided by Extensibility.Build package)
   - Bundles LSP server (asm-dude2-ls) in `Server/` subdirectory of VSIX package

   **⚠ CRITICAL: Extension MUST use OOP (out-of-process) mode.**
   - **NEVER add `VssdkCompatibleExtension=true`** to the csproj — it forces in-proc hosting which silently breaks LSP activation (`CreateServerConnectionAsync` is never called, no errors, no logs)
   - **NEVER add `RequiresInProcessHosting = true`** to Extension.cs — same effect
   - Extension.cs MUST use `Metadata = new(...)` with id/version/publisher (OOP mode)
   - Correct deployment path: `VSExtensions/Henk-Jan Lebbink/AsmDude2/<version>/`
   - Wrong deployment path (in-proc): `Extensions/<random>/` — if you see this, the mode is wrong
   - This has been broken and re-debugged 6+ times. Do not deviate.

**Archived Projects** (moved to `VS\CSHARP\old\`)
   - `asm-dude2-vsix`: Legacy .NET Framework 4.8 in-process extension (VSSDK/MEF) — archived
   - `asm-dude2-ext`: Failed VisualStudio.Extensibility migration attempt
   - `asm-irony`: Experimental parser (not used)
   - See `VS\CSHARP\old\README.md` for details

### Core Components

#### Language Server Architecture (Two-Project Pattern)

The LSP server is split into two projects following the **library + executable pattern**:

**asm-dude2-ls-lib** (Library - `VS/CSHARP/asm-dude2-ls-lib/`)
- Contains all LSP protocol implementation (`LanguageServer.cs`, `LanguageServerTarget.cs`, etc.)
- Hosts all resource files (AsmDudeData.xml, instruction signatures, performance data)
- Declares all dependencies (LSP protocol, logging, Z3 simulator)
- Uses `InternalsVisibleTo` to expose internals to tests and fuzzer:
  - `asm-dude2-ls-tests` (unit tests via xUnit)
  - `asm-fuzz` (coverage-guided fuzzing via SharpFuzz + libFuzzer)
- Platform-independent (no process/service hosting code)

**asm-dude2-ls** (Executable - `VS/CSHARP/asm-dude2-ls/`)
- Thin entry point that bootstraps the library as a .NET Hosted Service
- Configures dependency injection and logging via `Host.CreateApplicationBuilder()`
- Handles stdio flag for test/CLI usage: `--stdio` redirects LSP protocol to stdin/stdout (instead of named pipes)
- Minimal code: Program.cs calls `Worker` hosted service (which uses asm-dude2-ls-lib)
- Launched by asm-dude2-vsix via named pipes (`asmdude2-output`, `asmdude2-input`)

**Why two projects?**
1. **Testability**: Tests reference the library directly, not the executable; avoids subprocess spawning in unit tests
2. **Reusability**: Fuzzer and other tools can consume the library without launching a service
3. **Separation of concerns**: Protocol logic (library) vs. service hosting (executable)
4. **Debuggability**: Tests can debug the library code without navigating through service lifecycle

---

1. **Language Server (asm-dude2-ls / asm-dude2-ls-lib)**: LSP server (.NET 10.0 LTS)
   - Main class: `LanguageServer.cs` manages LSP communication via StreamJsonRpc
   - Features: syntax highlighting, code completion, signature help, hover info, folding ranges
   - **Semantic Tokens**: Rich syntax highlighting via `textDocument/semanticTokens/full`
   - **LSP Types**: Uses `Microsoft.VisualStudio.LanguageServer.Protocol` 18.5.3 (public API, no hacks needed)
   - **VS-specific Types**: Uses `VSTypes.cs` and `VSInternalTypes.cs` for Visual Studio extensions

2. **VS Extension (asm-dude2-vsix)**: Modern Visual Studio 2022/2026 extension (.NET 10.0-windows)
   - Location: `VS\CSHARP\asm-dude2-vsix\`
   - Launches asm-dude2-ls.exe and communicates via named pipes (`asmdude2-output`, `asmdude2-input`)
   - Build process bundles the LSP server into the VSIX package via post-build 7z command
   - Provides document type configuration and language server provider for .asm files

### Supporting Libraries

- **asm-options-lib**: Dependency-light shared **settings contract** (`net10.0`, no Roslyn/Z3)
  - `AsmSettingsData` — the serializable settings DTO (all the `ARCH_*`, `AsmSim_*`, etc. fields)
  - `ColorJsonConverter` — Color↔JSON converter used by both sides
  - Referenced by **both** the VSIX (settings producer) and `asm-tools-lib`/server (consumer), so the
    `settings.json` contract is compile-checked on both ends. The VSIX references THIS (not
    `asm-tools-lib`) to avoid dragging Roslyn into the extension. See [Settings flow](#settings-flow-vsix--server).

- **asm-tools-lib**: Core assembly language parsing and analysis (.NET 10.0-windows)
  - Defines fundamental types: `Mnemonic`, `Register`, `Operand`, `KeywordID`
  - Contains instruction data and architecture definitions
  - `AsmLanguageServerOptions : AsmSettingsData` adds the arch/micro-arch/assembler helper logic
    (`Get_Arch_Switched_On`, `Used_Assembler`, …) on top of the shared data contract
  - Single-targeted for .NET 10.0-windows (dropped net48 support)

#### The AsmSim subsystem — the `asm-sim-*` family (3 layers + tests)

The symbolic Z3 simulation is split into **three layers**, each its own project, all prefixed `asm-sim-*`
(the prefix deliberately distinguishes the *simulation subsystem* from the `asm-dude2-*` app/editor layer).
**Do not merge these — they are genuinely different layers** (verified 2026-06-13):

- **`asm-sim-lib`** — the **ENGINE** (namespace `AsmSim`). Pure Z3 symbolic execution: `DynamicFlow`
  (the per-component CFG + its SYMBOLIC ITE/phi merge — `Create_States_Before/After` →
  `Create_State_Private`/`MergeConstructor`), `State`, `StateUpdate`, `Runner`, `LoopStrategy`, `Tools`
  (`Tools.Collapse` = the n-ary merge), `Tv`, `StaticFlow`, `SimResultComparer`. **Has no
  editor/LSP/document concept.** Reused by the fuzzer, the CLI, and `asm-sim-tests` — so it must stay pure.
  *Never put document-caching or editor-facing code here.* (The bespoke `ComponentEvaluator` Tv-level
  merge was deleted 2026-06-13 — it solved all 16 registers per vertex; the editor now reads DynamicFlow's
  symbolic states directly, ~100× faster. See `INCREMENTAL_SIM_PLAN.md`.)

- **`asm-sim-host-lib`** — the **HOST / orchestration** (namespace `AsmSim.Host`). Drives the engine per
  document: `AsmSimulator` (the linear walk + the component engine = DynamicFlow's symbolic states + only
  the displayed registers solved per line on the Phase-P parallel worker pool; runtime `ApplySettings`),
  `DocCache` (per-line before/after state + CodeLens read/write labels + diagnostics), `SimDiagnostic`,
  and `AsmSimProtocol` (the out-of-process wire DTOs). **Has NO LSP-protocol dependency** (so it can be
  hosted either in-process by the LSP server or out-of-process by the sim server). Depends on
  `asm-sim-lib` + `asm-tools-lib` + `asm-options-lib`. *(Was `asm-dude2-sim-lib`; `AsmSimulator` was
  `LspAsmSimulator` — both renamed 2026-06-13 to stop lying about being LSP-specific.)*
  - **`AsmSim_On` gates COMPUTATION, not just display (fixed 2026-06-21):** `LanguageServer.UpdateInternals`
    now skips the whole sim invocation when `options.AsmSim_On != true`. Previously it called
    `InvalidateAndSimulate`/`DocumentChanged` unconditionally, so a user who switched AsmSim **off** still
    paid a full background Z3 run ~`DebounceMs` (3 s) after every edit/open — `AsmSim_On` only gated the
    *display* (diagnostics/decorations). Read paths (`GetUnreachableLines`, CodeLens sim-state) already
    no-op on an empty cache, so gating the run is safe.

- **`asm-sim-server`** — the **out-of-process server exe** (AssemblyName `AsmSim.Server`, namespace
  `AsmSim.Host`). Hosts `asm-sim-host-lib` behind a `StreamJsonRpc` stdio interface (`AsmSimProtocol`) so
  the heavy/crash-prone Z3 sim can run in a separate process and not block or kill the LSP. Mirrors how
  `asm-dude2-ls` hosts `asm-dude2-ls-lib`. **Status: working & tested** — `AsmSimClient` (in the LSP server)
  launches it, mirrors streamed results, **respawns on crash** (re-sends open docs), and pushes runtime
  **`settingsChanged`** (engine switch without restart). Opt-in via `ASMDUDE_SIM_OUTOFPROC=1` (default
  in-process). See `VS/CSHARP/ASMSIM_SERVER_PLAN.md`.

- **`asm-sim-tests`** (MSTest, via `vstest.console.dll`) tests the **engine** (`asm-sim-lib`):
  `Test_DynamicFlow*`, etc. **`asm-sim-host-tests`** (xUnit) tests the **host** (`asm-sim-host-lib`):
  `AsmSimulatorTests` (golden register values, `CompactStateString`/`MergeCompactLabels`, linear-vs-component
  shadow), `AsmSimComponentEngineTests` (the symbolic component engine on a real file), `AsmSimServer*Tests`
  (in-memory + launched-process out-of-proc), `AsmSimSettingsChangedTests`. The `AsmSimClient` respawn +
  settings-push tests live in `asm-dude2-ls-tests`. Tests live with the layer they test.

**Engine selection / tuning** (env vars read by `asm-sim-host-lib`, visible to whichever process hosts it):
`ASMDUDE_SIM_ENGINE=linear|component|shadow` (default **component** since 2026-06-13 — the per-component
engine that reads DynamicFlow's symbolic ITE-merged states + solves only displayed registers per line;
branch-aware and ~on par with linear. Force the old single-path engine with `=linear`),
`ASMDUDE_SIM_PARALLEL=<N>` (component worker count, default
≈ min(cores, 8)), `ASMDUDE_SIM_LOOP=accept|modsethavoc|peelonce|fullunroll|fixpoint`. All three are also
runtime-settable on the out-of-process server via `settingsChanged` (no restart).

#### Known limitation: truth-value flattening loses relational information (display sites)

Symbolic register/flag **values** are flattened to per-bit truth-values (`Tv[]`) for display. The lossy
primitive is **`ToolsZ3.GetTvArray(BitVecExpr, …)`** (`asm-sim-lib/ToolsZ3.cs:765/770/792`): it solves
each bit **independently**, so everything *relational* the solver knows is discarded — `RAX == RBX`,
`RAX = RBX + 1`, "RAX is even", "exactly one of these bits is set". Two registers Z3 knows are equal but
otherwise unconstrained both render as `0x????????????????`, and the equality is **invisible** in the
display. This is sound for "what is each bit pinned to" but is the wrong representation for showing or
reasoning about *relationships*.

- **Flatten primitives:** `ToolsZ3.GetTvArray`/`GetTv` (`ToolsZ3.cs`); `State.GetTvArray(Rn)` /
  `GetTv(Flags)` / `GetTvArrayMem` (`State.cs:814/768/867`, memoized per-`State` when `Frozen` via
  `cached_Reg_Values_`/`cached_Flag_Values_`).
- **Display/diagnostic consumers (all in the host `AsmSimulator.cs` + `State.ToStringRegs`):**
  `ComputeStateString` (`:1587`) → CodeLens sim-state; `ComputeReadLabel`/`ComputeWriteLabel`
  (`:1614/:1661`) → CodeLens `r:`/`w:` labels; usage-of-undefined diagnostic message (`:2145/:2156`).
  These feed CodeLens via `GetSimStatesSummary` (`:1460`) over the side pipe. **They are cached** — the
  flattened per-line strings/labels live in `DocCache` (`lineStrings*`, `:89-107`), computed once per
  (re)simulated line on the Phase-P worker pool and carried forward for unchanged lines on edit
  (`RemapCache`/`RemapConeReuse`); the CodeLens request path only reads cached strings (no re-solve).
- **NOT a value-flatten (the correct pattern):** redundancy `State.Is_Redundant(Rn/Flags)`
  (`State.cs:617/646`) flattens only the *proposition* `MkEq(reg@key1, reg@key2)` to one `Tv`, preserving
  the relationship. `State.EqualValues` (`:1213`) likewise. Inlay hints don't touch sim values.
- **Forward-looking fix (not done):** a relational-aware label — e.g. render `RAX = RBX` when
  `Is_Redundant`/`EqualValues` proves equality even though both registers are otherwise unknown — would
  recover the most useful lost relationship for display without abandoning the `Tv[]` fast path.

- **asm-annotate**: The instruction-data toolchain (CLI). Subcommands: `extract` (Intel SDM PDF→Markdown,
  stage 1), `gen-signatures` (wiki Markdown→`signature-*.txt`, stage 2 — formerly the standalone
  `intel-doc-2-data` project, folded in here), `perf-uops` (uops.info XML→perf TSVs), plus `check-latest`/
  `find`/`dump`. Tested by `asm-annotate-tests`.

## Build Commands

Build the entire solution:
```
dotnet build VS\AsmDude.sln
```

Build in Visual Studio or press F5 to launch experimental VS instance with extension.

Build specific projects:
```
# LSP Server (library)
dotnet build VS\CSHARP\asm-dude2-ls-lib\asm-dude2-ls-lib.csproj

# LSP Server (executable)
dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj

# VS Extension (✅ MODERN, F5 DEBUGGABLE)
dotnet build VS\CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj
```

**SDK Requirements**:
- **.NET 10.0 SDK** (10.0.100 or later) - Required for extension, LSP server, and core libraries

Download .NET 10 from: https://dotnet.microsoft.com/download/dotnet/10.0

**Language Version**: C# 14 (included with .NET 10)

### Code analyzers

Analyzer configuration is **centralized** — do not re-add these per-project:

- **`VS/CSHARP/Directory.Build.props`** turns on the built-in .NET analyzers (`EnableNETAnalyzers`,
  `AnalysisLevel=latest`, `EnforceCodeStyleInBuild`) for every active project and adds three analyzer
  packages (all `PrivateAssets=all`): **Meziantou.Analyzer** (correctness / immutability / readonly),
  **Microsoft.VisualStudio.Threading.Analyzers** (VSTHRD async / JoinableTask / StreamJsonRpc), and
  **Microsoft.CodeAnalysis.BannedApiAnalyzers**. `VS/CSHARP/old/Directory.Build.props` deliberately does
  **not** import the parent, so archived code is exempt (`RunAnalyzers=false`).
- **Policy: warnings, never errors.** Nothing here sets `TreatWarningsAsErrors`, so the build stays green
  (currently ~103 warnings) while findings are triaged. Per-rule severities live in **`VS/.editorconfig`**:
  readonly/immutability rules are turned **up** (`IDE0044`, `dotnet_style_readonly_field`, `CA2227`);
  deliberate patterns are kept quiet (`CA1051` for the `AsmSettingsData` public-field DTO, `CA1819` for the
  `Tv[]`/semantic-token fast paths); and the noisiest opinionated Meziantou rules (`MA0006`, `MA0002`,
  `MA0026`, …) are tamed to `none`/`suggestion`.
- **`VS/CSHARP/BannedSymbols.txt`** enforces the [Logging](#logging-asmlog) rule: `System.Console` and
  `File.AppendAll*` are banned (`RS0030`) so logging goes through `AsmLog`. **Exemptions:** the CLI tools
  (`asm-annotate`, `asm-sim-main`), the fuzzer (`asm-fuzz`) and all `*-tests` projects opt out via a scoped
  `NoWarn RS0030` in `Directory.Build.props` (Console is legit output there); `AsmLog.cs` itself — the
  sanctioned sink — opts out with a file-level `#pragma warning disable RS0030`. Remaining `RS0030`
  warnings (in `asm-dude2-ls`, `asm-dude2-vsix`, `asm-sim-server`, `asm-tools-lib`) are real ad-hoc-logging
  sites to migrate to `AsmLog`.

## Testing

Run all tests:
```
dotnet test VS\AsmDude.sln
```

Run specific test projects:
```
dotnet test VS\CSHARP\asm-tools-tests\asm-tools-tests.csproj
dotnet test VS\CSHARP\asm-sim-tests\asm-sim-tests.csproj
dotnet test VS\CSHARP\asm-dude2-ls-tests\asm-dude2-ls-tests.csproj
```

**Test frameworks**: xUnit (asm-dude2-ls-tests, asm-dude2-vsix-tests), MSTest (asm-tools-tests, asm-sim-tests)

**Test Results Summary**:
| Project | Passed | Skipped | Notes |
|---------|--------|---------|-------|
| asm-tools-tests | 42 | 0 | Core assembly tools (incl. arch DNF parse, tile/operand, `InstructionDescription.Render`) |
| asm-sim-tests | 178 | 3 | Z3 simulator (DynamicFlow merge crash FIXED; 3 skips unrelated) |
| asm-dude2-ls-tests | 168 | 37 | Unit + AsmSim integration (incl. `GetSemanticTokens_ArchGating_*`, `GetHover_Mnemonic_*` operand-aware/per-form); `LspProcessIntegrationTests` hover/semantic-token are flaky under parallel load (pass in isolation) |
| asm-dude2-vsix-tests | 8 | 0 | **NEW** — `CodeLensPublishPlanner` (CodeLens scroll-loop fix), replays the captured VS request stream |
| asm-annotate-tests | 16 | 0 | stage-1 PDF→MD text/title heuristics |
| asm-annotate-tests (gen-signatures) | 14 | 0 | stage-2 MD→signature generator (was intel-doc-2-data-tests, now folded into asm-annotate-tests) |

**Note**: The DynamicFlow **branch-merge** Z3 context-lifecycle crash is **FIXED** (shared-context rewrite — see Known Issues); the 25 previously-skipped DynamicFlow tests are re-enabled and pass. Only 3 sim tests remain skipped for unrelated reasons. Run sim tests via `vstest.console.dll`, not `dotnet test`.

**REP-prefix parse fix**: `AsmSourceTools.ParseLine` now combines a REP-family prefix with the following string-op into the combined mnemonic (`"rep movsb"` → `REP_MOVSB`); previously the prefix was dropped and the simulator skipped the REP loop semantics.

**LSP Integration Tests**: `LspIntegrationTests.cs` contains true JSON-RPC tests over streams. Some integration tests are skipped due to serialization complexity. The unit tests in `LanguageServerTests.cs` provide comprehensive coverage.

## Known Issues

### ✅ RESOLVED: Migration to Public LSP Types Complete

**Status**: COMPLETE

The LSP server uses `Microsoft.VisualStudio.LanguageServer.Protocol` 18.5.1 (from vssdk feed) with 261 public types. No CLR hacks needed.

**Current Setup**:
- ✅ `Microsoft.VisualStudio.LanguageServer.Protocol` 18.5.1 - all LSP types public
- ✅ `Microsoft.VisualStudio.LanguageServer.Protocol.Extensions` 18.5.1 - VS-specific extensions
- ✅ Built-in System.Text.Json serialization (no custom converters needed)
- ✅ Uses `Uri` type for document identifiers
- ✅ Uses `int` for Position.Line/Character
- ✅ All 66 asm-dude2-ls-tests pass
- ✅ No more `IgnoresAccessChecksToGenerator` CLR hack

**NuGet Source**: Requires vssdk feed in NuGet.config:
```
https://pkgs.dev.azure.com/azure-public/vside/_packaging/vssdk/nuget/v3/index.json
```

---

### Upgrading Z3

Z3 is **not on nuget.org** — it is distributed as `.nupkg` files on GitHub releases only.

**Steps to upgrade to a new Z3 version:**

1. Go to https://github.com/Z3Prover/z3/releases and find the new release.
2. Download `Microsoft.Z3.<version>.nupkg` (and optionally `.snupkg`) from the release assets.
3. Place the file(s) in `local-nuget/` at the repo root (replace the old files).
4. Update the `Microsoft.Z3` version in:
   - `VS/CSHARP/asm-sim-lib/asm-sim-lib.csproj`
   - `VS/CSHARP/asm-sim-tests/asm-sim-tests.csproj`
5. Run `dotnet build VS/AsmDude.sln` — verify 0 errors.
6. Run simulator tests via `vstest.console.dll` (not `dotnet test` — silent failure on .NET 10 + MSTest 4.1):
   ```
   dotnet "C:/Program Files/dotnet/sdk/10.0.100/vstest.console.dll" VS/CSHARP/asm-sim-tests/bin/Debug/net10.0-windows/asm-sim-tests.dll
   ```
   Expected: **152 passed, 28 skipped, 0 failed**.
7. Verify `libz3.dll` appears in `VS/CSHARP/asm-dude2-ls/bin/Debug/net10.0-windows/`.

The NuGet source `local-z3` → `local-nuget/` is already configured in `NuGet.config`.

---

### Z3 Context Lifecycle Bug in DynamicFlow (Regression) — ✅ FIXED (2026-06-03)

**Status**: RESOLVED via the shared-context rewrite (Phase 1). The 25 previously-skipped DynamicFlow tests (`Test_DynamicFlow` class, `Test_BitTricks_LegatosMultiplier`, the `Test_Runner_*` jump/merge tests) are re-enabled and pass; `asm-sim-tests` is now 178 passed / 0 failed / 3 skipped.

**Was**: each `State`/`StateUpdate`/`OpcodeBase` created its OWN Z3 `Context`, so `DynamicFlow` state-merging translated expressions between contexts and crashed (AV 0xC0000005 in `BranchInfo.Translate` → `Z3_translate`) when a source context had been disposed.

**Fix**: `Tools.SharedCtx` carries one Z3 `Context` per simulation unit. `State`/`StateUpdate`/`OpcodeBase` BORROW it when set (tracked by an `ownsCtx_` flag; they don't dispose a borrowed context). `DynamicFlow` creates and owns one `Context`, sets it on its internal `Tools`, and disposes it last (after the graph's borrowing states/updates). With everything in one context, the cross-context `Translate` calls become identities and the merge AV is gone. (See `VS/CSHARP/asm-sim-lib/Z3_CONTEXT_LIFECYCLE_BUG.md` and `INCREMENTAL_SIM_PLAN.md`.)

**Next (incremental sim)**: the shared context is per-`DynamicFlow`; the planned next step is per-**CFG-component** contexts + reusing unaffected components on edit (`StaticFlow.ComputeLineToComponent`). See `INCREMENTAL_SIM_PLAN.md` Phases 2–3.

## Development Workflow

To debug the extension:
1. Open `VS\AsmDude.sln` in Visual Studio 2022 or 2026
2. Set `asm-dude2-vsix` as startup project
3. Press F5 — builds, deploys to Exp hive, launches experimental VS instance
4. Open a `.asm` file in the experimental instance to activate the LSP server

**Prerequisites**: The **"Visual Studio extension development"** workload must be installed (provides the F5 debug launcher for extensibility projects).

Supported Visual Studio versions: **2022 (17.x) and 2026 (18.x)**

### Debugging: Log Locations

When F5 is pressed, two VS instances run plus the LSP server process. Each has its own logs:

#### 1. AsmDude log files (check first — this is where everything goes)
All AsmDude logging (plugin, LSP server, AsmSim) flows through the unified `AsmLog` engine (see
[Logging](#logging-asmlog) below). Two files in `%TEMP%`:
```
%TEMP%\AsmDude2-extension.log   ← the VSIX plugin (categories: CodeLens, Pipe, Settings, Server, Command)
%TEMP%\asmdude-execution.log    ← the LSP server + AsmSim (categories: LS, ASMSIM, SIM, + host/framework)
```
Each line is `[HH:mm:ss.fff] [LVL] [Category] (member:line) message` — grep by level (`WRN`/`ERR`),
category, or the `(member:line)` call-site key. Raise verbosity with `ASMDUDE_LOGLEVEL=trace` (see below).

#### 2. AsmDude output panes inside VS
In the **experimental** VS instance, View → Output → dropdown:
- **"AsmDude2"** — the plugin's own Info+ logs (clean columnar form).
- **the language-server pane** — the server's Info+ logs, delivered via LSP `window/logMessage`.

#### 3. Extensions pane (extension host loading / activation)
View → Output → "Extensions" / "VisualStudio.Extensibility". Errors from `CreateServerConnectionAsync`,
VSIX load failures, LSP activation issues.

#### 4. ServiceHub Extension Host Log (extension won't load at all)
```
%TEMP%\ServiceHub\logs\*ServiceHub.Host.Extensibility*
```
Most recent file; assembly-loading errors appear here (e.g. wrong .NET target framework).

#### 5. Activity Log (VS startup/discovery issues)
```
%APPDATA%\Microsoft\VisualStudio\18.0_<id>Exp\ActivityLog.xml
```
Extension discovery/registration problems; rarely needed for runtime debugging.

#### Not relevant for runtime debugging:
- **Host VS** (where you press F5): its Output window only shows build/deploy status.
- **Build logs**: only relevant for compile/deployment errors.

**Check order**: 1 → 2 → 3 → 4 → 5

### Logging (AsmLog)

Unified, structured logging across all three components, with **one engine** in the assembly they all
share. **When debugging, prefer adding `AsmLog` statements + reading the files above over guessing.**

- **Engine:** `AsmTools.AsmLog` in **`asm-options-lib`** (the leaf every component transitively
  references). Levels `Trace < Debug < Info < Warn < Error < Off`; default threshold **Debug** (DEBUG
  build) / **Warn** (Release). Sinks are `Action<AsmLogEntry>`; each entry renders **detailed** (file/
  console, with the `(member:line)` call-site key, auto-filled via `[CallerMemberName]`/`[CallerLineNumber]`)
  or **clean columnar** (VS panes, `ToDisplayString`). Below-threshold calls early-out before formatting.
- **Call it as:** `AsmLog.Info("SIM", "msg")` / `Debug` / `Warn` / `Error`. Category is a short subsystem
  tag (`SIM`, `ASMSIM`, `LS`, `CodeLens`, `Pipe`, `Settings`, `Server`, `Command`).
- **Per process:**
  - **Server** — `AsmDude2LS.AsmDudeLog` is a thin facade (category `LS`) that wires the disk sink
    (`asmdude-execution.log`) + **stderr** console (stderr so it never corrupts `--stdio` JSON-RPC) +
    the LSP `window/logMessage` sink. `AsmLogLoggerProvider` bridges `Microsoft.Extensions.Logging`
    (host/`Worker`) into `AsmLog`. **AsmSim** (`asm-sim-lib`) logs via `AsmLog("SIM")` — its old
    `Console.WriteLine`s are gone (they had corrupted stdout in `--stdio`).
  - **Plugin** — `AsmDude2.VsixLog` wires the disk sink (`AsmDude2-extension.log`) + the "AsmDude2" VS
    output pane (created in `AsmLanguageServerProvider.CreateServerConnectionAsync`). The old 5 ad-hoc
    `File.AppendAllText` loggers now delegate to `AsmLog`.
- **Runtime verbosity** (no rebuild): env var **`ASMDUDE_LOGLEVEL`**=`trace|debug|info|warn|error|off`
  (both processes; wins), or the **`LogLevel`** field in `%APPDATA%\AsmDude2\settings.json`
  (`SettingsManager.ApplyLogLevel`; env var takes precedence).
- **Do NOT** reintroduce `Console.WriteLine`/`File.AppendAllText` for logging, and keep `AsmLog` in
  `asm-options-lib` dependency-free (the VSIX references it to avoid pulling heavy deps). A Serilog/MEL
  backend can sit behind `AsmLog` as a sink with zero call-site churn if ever needed.

## Data Files

- `AsmDudeData.xml`: Instruction descriptions and metadata (bundled with VSIX and LSP)
- Performance data: one TSV per microarchitecture in `asm-dude2-ls-lib\Resources\Performance\`
  (`Skylake.tsv`, `Haswell.tsv`, … 27 files: Conroe→Emerald Rapids, the Atom line, and AMD Zen2–Zen5).
  **Generated from [uops.info](https://uops.info)** (`instructions.xml`, measured latency/throughput/ports/µOps,
  XED-iform-keyed) by `asm-annotate`'s `perf-uops` command — see the pipeline note below. Format is 8
  tab-separated columns: `Instruction  operands  µOps-fused  µOps-unfused  ports  latency  throughput  remark`;
  the first column is a single mnemonic (`PerformanceStore` parses it directly — the old
  `Instructions-Translations.tsv` name-map is gone). Bundled via a `Resources\Performance\*.tsv` glob in the
  csproj, so new arch files need no csproj edit. `PerformanceStore.ArchFiles` maps each `MicroArch` → file;
  the VS settings expose a **single-select** `perfArch` dropdown (the server stays multi-arch-capable for VS Code).
- Signature files in `Resources\signature-*.txt`. **The LSP server loads `signature-mar2026.txt`**
  (generated from the wiki by `asm-annotate gen-signatures`, rev-091/March 2026) **+ `signature-hand-1.txt`**
  (hand-maintained, OVERRIDES the regular file by `(Mnemonic, signature-label)`). (`signature-may2019.txt`
  was retired and deleted.) Loaded name is hard-coded in `LanguageServer.cs:~420` + bundled via the csproj.
  `signature-hand-1.txt` is the single hand-authored data home (signature/description overrides). The
  per-mnemonic description it overrides may embed `{0}`,`{1}`,… operand placeholders that the hover fills in
  — see [Operand-aware mnemonic hover](#hover-tooltips-asm-dude2-ls-lib). (No separate template file/column.)

### Instruction-data pipeline (pdf → md → txt)
`asm-annotate extract` (PDF→MD, stage 1) → copy `output/*.md` to `asm-dude.wiki/doc/` →
`asm-annotate gen-signatures` (MD→`signature-mar2026.txt` + wiki `Home.md`, stage 2) →
LSP server (stage 3). All stages are now subcommands of the one `asm-annotate` tool (the old
`intel-doc-2-data` project was folded in as `gen-signatures`).
The arch column is **DNF** (`+`=AND, `,`=OR, e.g. `AVX512_VL+AVX512_F,AVX10`); `Home.md`'s arch column
is a flattened union. New ISA covered incl. AMX tile registers (`TMM0-7`), AVX10, FP16, Key Locker.
Each stage has tests in `asm-annotate-tests` (+ `asm-dude2-ls-tests` for the server stage).

**Arch extraction** can miss the CPUID feature when the SDM table has no CPUID column (legacy
instructions — handled by `To_Signature`'s operand-width heuristic) or a garbled cell (a small
`SignatureGenerator.KnownArchExceptions` map fills known SGX/Key Locker cases). `gen-signatures` warns
(`AsmLog.Warn("ANNOTATE", "no arch for …")`) on any remaining gap — empty arch is a real bug, not just
noise, since it parses to `ARCH_NONE` = always-on, leaking the instruction into every arch profile. See
[`asm-annotate/KNOWN-DATA-ISSUES.md`](VS/CSHARP/asm-annotate/KNOWN-DATA-ISSUES.md) for the fixed/open
issues (incl. the EEXIT scrambled-table stage-1 defect and the FXSAVE false-positive warning). **Generator
fixes are inert until `gen-signatures` is re-run** (rewrites `signature-mar2026.txt` + wiki `Home.md`).

### Performance data pipeline (uops.info → TSV), separate from the SDM pipeline
The latency/throughput TSVs are regenerated independently of the PDF/signature pipeline:
```
# 1. download the uops.info database (~140 MB; gitignored, keep under tmp-uops/)
curl -sSL -o tmp-uops/instructions.xml https://uops.info/instructions.xml
# 2. convert to one TSV per microarchitecture
dotnet run --project VS/CSHARP/asm-annotate -- perf-uops tmp-uops/instructions.xml tmp-uops/out
# 3. copy the generated TSVs over the bundled ones
cp tmp-uops/out/*.tsv VS/CSHARP/asm-dude2-ls-lib/Resources/Performance/
```
`UopsInfoImporter` streams the XML, normalizes uops iclass suffixes to real mnemonics
(`CALL_NEAR`→`CALL`, `ADD_LOCK`→`ADD` + "lock" remark, `CMPSD_XMM`→`CMPSD`), and maps the 27
`architecture name=""` codes to file names. Rows with a mnemonic unknown to the `Mnemonic` enum are
dropped by the loader with a warning. To add an arch: extend the enum, `PerformanceStore.ArchFiles`,
`Is_MicroArch_Switched_On`, the `AsmSettingsData` field, the VSIX `perfArch` dropdown + `PerfArchKeys`,
and `UopsInfoImporter.ArchToFile`. Tests: `UopsInfoImporterTests`, `PerformanceStoreTests`,
`PerformanceDisplayTests`.

## Key Implementation Details

The LSP server maintains several dictionaries indexed by document URI:
- `textDocuments`: Raw document content
- `textDocumentLines`: Parsed lines
- `parsedDocuments`: Tokenized keywords per line
- `foldingRanges`: Code folding regions
- `labelGraphs`: Assembly label relationships

Communication between VS extension and LSP server uses named pipes (Windows) with NamedPipeServerStream. Recent fix addresses UnauthorizedAccessException on Windows 10.

### Semantic Tokens (asm-dude2-ls-lib)

**Implementation:** `VS\CSHARP\asm-dude2-ls-lib\`

The asm-dude2-ls-lib implements LSP Semantic Tokens for rich syntax highlighting. This allows Visual Studio to apply distinct colors to different assembly language elements.

**Token Types** (defined in `LanguageServerTarget.cs`) — all standard LSP 3.17 names:
| Index | Type | Used For |
|-------|------|----------|
| 0 | `keyword` | Mnemonics (MOV, ADD, etc.) |
| 1 | `variable` | Registers (RAX, EAX, etc.) |
| 2 | `type` | Labels (loop_start:, etc.) |
| 3 | `macro` | Directives (.data, PROC, MASM/NASM directives, pseudo-ops) |
| 4 | `number` | Immediate values (0x10, 42, etc.) |
| 5 | `operator` | Memory operands ([rax], MASM/NASM operators) |
| 6 | `comment` | Comments (; this is a comment) |
| 7 | `string` | String literals ("hello") |
| 8 | `function` | CALL/jump targets |

**Token Modifiers**:
| Bit | Modifier | Used For |
|-----|----------|----------|
| 0x1 | `declaration` | Label definitions |
| 0x2 | `definition` | Procedure definitions |
| 0x4 | `deprecated` | Unreachable lines, **and arch-gated tokens** (instructions/registers not enabled by the active `archProfile`) |
| 0x8 | `readonly` | Immediate values |

> **Arch-gating (the `MnemonicOff` story):** `MnemonicOff` exists in the enum but is **never emitted** —
> `MnemonicOff` would be `Mnemonic` + the `0x4 deprecated` modifier, so `GetSemanticTokens` instead applies
> `0x4` *directly* to any `Mnemonic`/`Jump`/`Register` token the active profile doesn't enable
> (`mnemonicStore.IsMnemonicSwitchedOn` / `IsRegisterSwitchedOn`). Display-only — the parsed `AsmTokenType`
> is unchanged. There is no `RegisterOff` type (the modifier covers it). This is the live implementation of
> the old `//TODO MnemonicSwitchedOn` markers that sat in the **dead** `AsmDude2Tools.Get_Token_Type_*` /
> `Parse.cs` path (see note below).

**AsmTokenType to LSP Mapping** (in `LanguageServer.cs`):
```csharp
Mnemonic       → keyword (0)
MnemonicOff    → keyword (0) + deprecated modifier
Register       → variable (1)
Label          → type (2)
LabelDef       → type (2) + declaration+definition modifiers
Jump           → function (8)
Directive      → macro (3)
MasmDirective  → macro (3)
NasmDirective  → macro (3)
MasmPseudoOp   → macro (3)
NasmPseudoOp   → macro (3)
Constant       → number (4) + readonly modifier
Remark         → comment (6)
Misc           → operator (5)
MasmOperator   → operator (5)
NasmOperator   → operator (5)
```

**Key Files:**
- `LanguageServerTarget.cs` - Defines `SemanticTokensProvider` capability and `SemanticTokensLegend`
- `LanguageServer.cs` - Implements `GetSemanticTokens()`, `MapTokenType()`, `GetTokenModifiers()`

**How It Works:**
1. Client requests `textDocument/semanticTokens/full` for a document
2. Server retrieves parsed tokens from `parsedDocuments` dictionary (populated by `AsmSourceTools.ParseLine`)
3. Each `KeywordID` (with `AsmTokenType`) is mapped to LSP semantic token type/modifiers
4. Arch-gating: a `Mnemonic`/`Jump`/`Register` token not enabled by the active `archProfile` gets the
   `0x4 deprecated` modifier OR'd in (greyed) — same mechanism as unreachable lines (skipped on unreachable
   lines, which are already greyed). Tested by `LanguageServerTests.GetSemanticTokens_ArchGating_*`.
5. Returns encoded `uint[]` array with delta-encoded positions and token info

> **⚠ The live tokenizer is `AsmSourceTools.ParseLine`, NOT `Parse.cs` / `AsmDude2Tools.Get_Token_Type_*`.**
> `Parse.cs` (per-assembler classifier) + `AsmDude2Tools.Get_Token_Type_Att` are **dead in the active build**:
> their only callers were the old MEF/WPF taggers (`NasmIntelTokenTagger`, `MasmTokenTagger`, …) removed in the
> LSP migration (commit `33a0376`) and now living only in the non-built archive
> `old/asm-dude2-vsix-archived/SyntaxHighlighting/` (which still references `AsmTools.Parse`). They are kept,
> not deleted, so that archive stays coherent. `AsmDude2Tools.Get_Token_Type_Intel`/`Get_Assembler`/
> `Get_Description`/`Get_Keywords` ARE still live (completion + hover). Don't "implement" the arch-gating
> TODOs there — it's done at the display layer in `GetSemanticTokens` (above).

### Hover Tooltips (asm-dude2-ls-lib)

**Implementation:** `VS\CSHARP\asm-dude2-ls-lib\` (`HoverBuilder.cs`, `LanguageServer.GetHover`)

Hover (`HoverBuilder.CreateHover(kind, sections, …, docUrl)`, returns `object?`) **branches by client**,
chosen from `LanguageServer.HoverMarkupKind` (negotiated from the client's advertised
`textDocument.hover.contentFormat` at `initialize`: **Markdown** when offered, **PlainText** otherwise):
- **Markdown clients (VS Code):** standard LSP `Hover` + `MarkupContent` — body in a ```` ```text ````
  fence (keeps the perf table's monospace columns) + a clickable `[Documentation](url)` link.
- **Visual Studio (PlainText):** a **`VSInternalHover`** whose `_vs_rawContent` stacks one
  `ClassifiedTextElement` per line, each a `ClassifiedTextRun("formal language", text,
  UseClassificationFont)`. **This is required:** VS draws plaintext hover in the *proportional*
  environment font, so a space-padded table does NOT line up (verified by pixel-measuring a real VS
  screenshot). `"formal language"` + `UseClassificationFont` is the only way to force a fixed-pitch font
  in a VS hover. The doc URL is a plain line (links unserializable over LSP — see below).

The performance table itself is built by `PerformanceDisplay.BuildPerformanceTable(collapsed)`, which
sizes **every column to its widest cell** (header + all rows) so long operand forms (e.g. AVX-512
`VFIXUPIMMPS ZMM, K, ZMM, M32_1to16, I8`) can't overflow a fixed column and shove the numeric columns
past their headers. Tested by `GetHover_PerformanceTable_ColumnsAreAligned` and
`GetHover_VisualStudioClient_UsesMonospaceRawContent` in `LanguageServerTests.cs`.

`GetHover` classifies the hovered token from the **parsed document tokens** (`parsedDocuments`, real
`AsmTokenType` incl. `LabelDef`/`Constant`), falling back to a string heuristic. Mnemonic/Jump,
Register, Constant, Label and LabelDef all produce content; `null` is returned only for a genuinely
unknown word (correct LSP semantics).

**Operand-aware mnemonic hover (the description IS the template):** for a Mnemonic/Jump token, `GetHover`
re-parses the hovered line (`AsmSourceTools.ParseLine`) for its operands and **substitutes them into the
mnemonic's existing description** — e.g. `add rax, rbx` → "Add rbx into rax". Design — deliberately minimal,
**no new field/column/row/map**:
- **Placeholders live on the PER-FORM signature rows, NOT the GENERAL row.** The per-form description
  column (exposed as `AsmSignatureInformation.RawDescription`) may contain `{0}`,`{1}`,… ({0}=first operand),
  authored in `signature-hand-1.txt`'s 5-column signature rows (`ADD ⇥ R/M64,R64 ⇥ X64 ⇥ ADD R/M64,R64 ⇥
  Add {1} to {0}, result in {0}`). **`GetHover` prefers a per-form description matched by operand count that
  contains `{`**, else falls back to the (clean) GENERAL description; then renders. This is what lets
  multi-form mnemonics differ per form — `imul rax,rbx` → "Multiply rax by rbx (signed), result in rax";
  `imul rax,rbx,4` → "Multiply rbx by 4 (signed), result in rax". (Matching is by operand *count*, not type,
  so a same-arity/different-size form like 1-operand `imul r/m8` vs `r/m64` uses a size-agnostic line.)
- **The per-mnemonic `GENERAL ⇥ MNEM ⇥ desc ⇥ htmlref` rows stay CLEAN (no placeholders).** `GetDescription`
  returns them and **completion** uses them, so a templatized mnemonic shows its normal description in the
  completion list (e.g. `XOR` → "Logical Exclusive OR"), NOT operand noise. *Putting placeholders in GENERAL
  is a bug — it leaks "op1/op2" into completion.* **To add operand-aware hover: put `{i}` on the form rows.**
- **The engine is one pure function:** `AsmTools.InstructionDescription.Render(description, operands)` —
  replaces `{i}` with `operands[i]`; an index with no operand renders as a generic `op{i+1}` (defensive).
  Descriptions without `{` pass through unchanged.
Pure/testable: `Test_InstructionDescription` (the `Render` engine, asm-tools-tests) +
`GetHover_Mnemonic_IsOperandAware` / `GetHover_Mnemonic_PicksPerFormTemplate_ByOperandCount` (end-to-end over
the real placeholder descriptions, asm-dude2-ls-tests).
> History: an earlier draft used a separate `templates.tsv`, then `TEMPLATE` rows + a `(mnemonic,opCount)`
> map. Both removed — the description field doubles as the template (no parallel structure).

> **History:** June 2026 hover was rewritten from `VSInternalHover`/`_vs_rawContent` to the portable
> `Hover`/`MarkupContent` for VS Code support — but that dropped VS's monospace tables (proportional font
> misaligns space padding). It was then re-split into the **dual path above**: VS Code keeps
> `MarkupContent`, VS gets `VSInternalHover`/`_vs_rawContent` (monospace) again. `VSInternalTypes.cs`
> holds the hand-rolled serialization types and IS used for the VS path.

**Clickable links in hover — nuanced:**
- Markdown `[text](url)` links **DO** serialize over LSP and **are clickable** in markdown-rendering
  clients (**VS Code**). This supersedes the old blanket claim that hover links are impossible — that
  was only true for the VS-specific `_vs_rawContent` path (`ClassifiedTextRun.NavigationAction` is an
  unserializable `Action` delegate).
- **Visual Studio advertises `contentFormat:["plaintext"]` for hover** (CONFIRMED in the server log
  `%TEMP%\asmdude-execution.log` — `"hover":{"contentFormat":["plaintext"]}`, and the server logs
  `Initialize: hover contentFormat -> PlainText`). So **in VS the doc URL is plain text, not a
  clickable link.** Forcing Markdown for VS would render literal ```` ``` ```` fences and `[..](..)`.
- To get a clickable doc link **in VS** specifically: a right-click "Open documentation" command, the
  `textDocument/documentLink` Ctrl+Click path (was removed — spawned an unwanted tab), or a hybrid
  in-proc `IAsyncQuickInfoSource` (heavyweight; see `VS/CSHARP/old/asm-dude2-vsix-archived/QuickInfo/`).

## Package Dependencies (Updated for VS 2026)

**Current Packages** (All Stable & Available):
- `Microsoft.VisualStudio.LanguageServer.Protocol` **18.5.3** (LSP types - public API, vssdk feed)
- `Microsoft.VisualStudio.LanguageServer.Protocol.Extensions` **18.5.3** (VS-specific LSP extensions)
- `StreamJsonRpc` **2.25.9** (`asm-dude2-ls-lib` only) / **2.24.84** (`asm-dude2-vsix` only — see note below)
- `Microsoft.VisualStudio.SDK` **17.14.40265** (VS 2022/2026 — see note below)
- `Microsoft.VSSDK.BuildTools` **17.14.2120** (VS 2022/2026 — see note below)
- `Microsoft.Extensions.Logging.Abstractions` **10.0.3** (Logging)


**Note:** Starting with Extensibility SDK 18.5, the VSIX can target `net10.0-windows8.0`. The extension host in VS 18.5+ supports .NET 10. Previous versions required `net8.0-windows8.0` (see [microsoft/VSExtensibility#544](https://github.com/microsoft/VSExtensibility/issues/544)).

**⚠ The `Microsoft.VisualStudio.Extensibility.Sdk` minor version MUST match the installed Visual Studio minor version.**
The SDK generates `Microsoft.VisualStudio.RpcContracts` with a matching version at build time. If the SDK minor version is higher than VS (e.g., SDK 18.6 on VS 18.5), VS rejects the extension because it doesn't have the newer RpcContracts. If the SDK version is too old (e.g., SDK 18.2 on VS 18.5), commands may silently fail to register. Check your VS version via Help → About (or `vswhere -property installationVersion`), then use the matching SDK preview from the vssdk feed. Current: installed VS is **18.8** (Insiders, the `0fafafd7` Exp hive) → SDK **18.8**.1030-Preview. Do NOT jump to the absolute-latest 18.9 SDK while VS is 18.8 — a higher SDK than VS is rejected (RpcContracts mismatch). Latest per-band on the vssdk feed (June 2026): 18.7.1240 / 18.8.1030 / 18.9.750.

**NuGet Sources**: Requires both nuget.org and vssdk feed (configured in `NuGet.config`):
```
https://pkgs.dev.azure.com/azure-public/vside/_packaging/vssdk/nuget/v3/index.json
```

## Project Structure

### Active Projects (Current Development)
- `asm-dude2-vsix`: **Main extension** for VS 2026 (.NET 10.0)
- `asm-dude2-ls`: Language server executable (.NET 10.0 LTS)
- `asm-dude2-ls-lib`: Language server implementation (.NET 10.0 LTS)
- `asm-dude2-ls-tests`: Unit tests for LSP server (xUnit)
- `asm-dude2-vsix-tests`: Unit tests for VSIX-side pure logic (xUnit; links source files like `CodeLensPublishPlanner` directly so tests exercise production code without referencing the VS.Extensibility SDK)
- `asm-options-lib`: Shared, dependency-light settings contract (`AsmSettingsData` + `ColorJsonConverter`) **and the `AsmLog` logging engine**; referenced by both the VSIX and the server
- `asm-tools-lib`: Core assembly language tools (.NET 10.0 LTS; single-targeted, no net48)
- `asm-tools-tests`: Tests for asm-tools-lib (MSTest)
- **AsmSim subsystem** (`asm-sim-*` — 3 layers; see "The AsmSim subsystem" above for the engine/host/server distinction):
  - `asm-sim-lib`: **engine** — Z3 symbolic execution (ns `AsmSim`); pure, no editor/LSP concept
  - `asm-sim-host-lib`: **host** — `AsmSimulator`/`DocCache`/parallel pool/protocol DTOs (ns `AsmSim.Host`); LSP-free
  - `asm-sim-server`: **out-of-process sim server exe** (`AsmSim.Server`, StreamJsonRpc/stdio) — server half built, client TODO
  - `asm-sim-tests`: engine tests (MSTest, run via `vstest.console.dll`)
  - `asm-sim-host-tests`: host tests (xUnit) — `AsmSimulatorTests`
- `asm-annotate`: Assembly annotation utility (.NET 10.0 LTS)

### Archived Projects (`VS\CSHARP\old\`)
- `asm-dude-vsix`: Legacy extension for VS2015/17/19
- `asm-dude2-ext`: Failed VisualStudio.Extensibility migration
- `asm-dude3`: Experimental modern LSP server (broken project references)
- `asm-irony`: Experimental parser (unused)

**Focus development on `asm-dude2-ls-lib`** (LSP server) and `asm-dude2-vsix` (VS extension).

### Settings flow (VSIX ↔ server)

Runtime config push over LSP isn't supported in the VS.Extensibility model (microsoft/VSExtensibility#426), so settings travel via a JSON file, but the **type is shared** for compile-time safety:

1. The VSIX `SettingsSyncService` subscribes to the VS.Extensibility Settings API and, on change, maps `SettingValues` into a strongly-typed **`AsmSettingsData`** (from `asm-options-lib`) and serializes it to `%APPDATA%\AsmDude2\settings.json` (using the shared `ColorJsonConverter`).
2. The server `SettingsManager` deserializes that file into **`AsmLanguageServerOptions`** (which derives from `AsmSettingsData`) and watches it via `FileSystemWatcher`.

Because both sides bind to the same field names through the shared `AsmSettingsData`, a renamed/removed field is a **compile error**, not a silently-defaulted value. The VSIX references only `asm-options-lib` (not `asm-tools-lib`) so Roslyn isn't pulled into the extension. This mirrors what the original (in-proc / `ILanguageClient`) AsmDude did with `Settings.Default` → `AsmLanguageServerOptions`, adapted to the file transport this platform forces.

#### Instruction-set profile (`archProfile`) — overrides the ~100 `ARCH_*` toggles

Rather than expecting a user to flip ~100 individual `ARCH_*` feature-flag checkboxes, the VSIX exposes a
**single-select `archProfile` dropdown** (`ArchitectureSettings.ArchProfile`, default **`v4`**): `everything` /
`latest` / `v4` / `v3` / `v2` / `v1` / `custom` (keys in `ArchProfileKeys`, **asm-options-lib**, shared both
ways). The VSIX **cannot** expand a profile (it has no `Arch` enum — only `asm-options-lib`), so the profile
string travels in `AsmSettingsData.ArchProfile` and is interpreted **server-side** by
`ArchTools.TryGetProfileArchs` (**asm-tools-lib**). `AsmLanguageServerOptions.Is_Arch_Switched_On` checks the
profile **first**: a non-`custom` profile returns membership in the profile's `Arch` set and **ignores** the
individual toggles; `custom` falls back to the per-arch bools (historical behavior). The 104 detailed toggles
are `EnabledWhen = SettingRule.Equal(ArchProfile, custom)` so they grey out unless the profile is `custom`.
v1–v4 are the psABI levels but **pragmatically inclusive** (each adds its era's common crypto/bit ISA, not
just the strict baseline); `latest` = everything except deprecated/vendor-legacy (Cyrix/3DNow/IA-64/SSE4A/…).
To add a new `Arch` to a profile, edit only the `ProfileV*`/`LatestExclusions` arrays in `ArchTools`. Tests:
`Test_ArchProfile` (asm-tools-tests). **Note:** because the default is `v4`, existing installs that relied on
the detailed toggles will be overridden on upgrade — switch the profile to `custom` to restore per-flag control.

### Assembly CodeLens (asm-dude2-vsix)

**Implementation:** `VS\CSHARP\asm-dude2-vsix\CodeLens\`

CodeLens shows two kinds of inline annotations on assembly lines:
- **Label references** — "N references" above each label definition.
- **Sim-state** — a Z3-proven register/flag summary (e.g. `→RAX=0x10, ←ZF=0`) above instruction lines.

Implemented with the modern **VisualStudio.Extensibility** CodeLens API (`TextViewTagger<CodeLensTag>` + `ICodeLensProvider`), NOT the old WPF-adornment approach and NOT the classic `IAsyncCodeLensDataPointProvider`.

> **Note:** Earlier revisions used a WPF-adornment approach (`AsmCodeLensAdornmentManager`, `AsmCodeLensLineTransformSource`, `AsmCodeLensMouseProcessor`, `AsmLanguageClient`). Those files no longer exist. If you find references to them, they are stale.

**Files:**

| File | Role |
|------|------|
| `AsmCodeLensProvider.cs` | `[VisualStudioContribution] ICodeLensProvider` — `TryCreateCodeLensAsync` dispatches on `CodeElementKind` to create the right CodeLens object |
| `AsmCodeLensTaggerProvider.cs` | Creates/owns the per-document `AsmCodeLensTagger` instances |
| `AsmCodeLensTagger.cs` | `TextViewTagger<CodeLensTag>` — fetches data, builds `CodeLensTag`s for label defs and sim-state lines, and publishes them via `PublishAsync`; encodes payload in `CodeElement.Description` |
| `CodeLensPublishPlanner.cs` | **Pure, VS-free** decision logic (unit-tested in `asm-dude2-vsix-tests`): given a tag request or sim refresh, decides which lines to (re)publish — keyed on per-line content + a recency window |
| `AsmLabelCodeLens.cs` | `InvokableCodeLens` — renders "N references" from the tag's `refcount:N\|...` description |
| `AsmSimStateCodeLens.cs` | `InvokableCodeLens` — renders the sim-state label from the tag's `simstate:\|...` description (display-only; click is a no-op) |

> **⚠ Scroll-loop fix (do not regress):** `UpdateTagsAsync` makes VS immediately re-issue
> `OnRequestTagsAsync(recalculateAll=true)`; answering with the **whole document** (`[0, document.Length]`)
> outdates every lens → VS re-requests everything → a ~40×/scroll publish↔request loop that thrashes the
> lenses. `PublishAsync` therefore (a) publishes **only changed lines' ranges**, never the whole document,
> and (b) routes the decision through `CodeLensPublishPlanner`, which suppresses re-publishing lines VS
> already has (per-line content + 2 s recency window). The sim side also throttles its per-line
> `onProgress` (`LspAsmSimulator.ProgressNotifyThrottleMs`). See memory `codelens-scroll-loop`.

**Data flow:**
1. `AsmCodeLensTagger.CreateTagsAsync` fetches label data from the LSP server via `SimStatePipeClient.GetCodeLensDataAsync` — a list of `AsmLabelRef` (label name, definition line/column/length, reference count). The tagger does **not** parse the document or count references itself; that logic lives only on the server (`GetCodeLensData` → assembler-aware `LabelGraph`, jump/call targets only).
2. For sim-state, it also fetches a `Dictionary<int,string>` (line → label) from the server via `SimStatePipeClient.GetSimStatesAsync` (see the pipe note below).
3. It emits one `CodeLensTag` per annotation, packing the data into `CodeElement.Description` (`refcount:N|Label: name` or `simstate:|<label>`); label tags are positioned using the server-reported definition column/length.
4. `AsmCodeLensProvider.TryCreateCodeLensAsync` turns each tag into an `AsmLabelCodeLens` or `AsmSimStateCodeLens`, which unpacks the description in `GetLabelAsync`.
5. The tagger re-runs on document change and when `SimStatePipeClient.SimStateUpdated` fires for this document's URI.

> **Reference semantics:** the server counts **jump/call targets only** (via `LabelGraph`), so a label used only by a data reference (`lea`/`mov`/`dq`) shows 0 references. This is intentional.

**⚠ Why sim-state arrives over a separate named pipe, not LSP:**
The VisualStudio.Extensibility OOP LSP model exposes **no** API for an extension part (tagger/command/CodeLens) to send a custom LSP request or receive server-initiated messages — the only documented surface is `CreateServerConnectionAsync` + `OnServerInitializationResultAsync` + startup `InitializationOptions`. So a tagger cannot reach the LSP connection. The sim-state therefore travels over a dedicated named pipe `asmdude2-simstate-{pid}` (`SimStatePipeServer` in the LS, `SimStatePipeClient` in the VSIX). This is a platform limitation, not a design preference. See `SimStatePipeServer.cs` for the protocol. (Settings are delivered the same way — via `%APPDATA%\AsmDude2\settings.json` + a FileSystemWatcher — because runtime config push over LSP is also unsupported in this model: microsoft/VSExtensibility#426.)

**Server owns reference counting (no VSIX-side parsing):**
`AsmCodeLensTagger` is a thin renderer — it calls `SimStatePipeClient.GetCodeLensDataAsync` and emits tags from the server's answer. All label detection and reference counting is the server's `GetCodeLensData`/`LabelGraph`, exposed over the pipe via `SimStatePipeServer.CodeLensDataProvider` (wired in the `LanguageServer` constructor). The pipe protocol gained a `getCodeLensData` request → `{"codeLensData":[{Label,DefinitionLine,DefinitionColumn,DefinitionLength,ReferenceLines}]}` response.

**Two CodeLens delivery paths (by client) — both intentionally kept:**

| Client | Path | Server entry points |
|--------|------|---------------------|
| **Visual Studio** (VisualStudio.Extensibility) | Side named pipe (tagger → `SimStatePipeClient` → `SimStatePipeServer.CodeLensDataProvider`) | `LanguageServer.GetCodeLensData(string)` |
| **VS Code / other LSP clients** (future, intended) | Standard LSP | `textDocument/codeLens` (`GetCodeLenses`), `codeLens/resolve` (`ResolveCodeLens`), `codeLensProvider` capability; optional custom `asm/codeLensData` |

VS.Extensibility never calls the standard LSP CodeLens methods (confirmed via server logs in a live hive session — zero `textDocument/codeLens` requests arrived) because its CodeLens tagger has no access to the LSP connection. **Do not remove the standard LSP CodeLens surface or its tests** — this server is intended to also drive VS Code later, where the standard path is the only one available. Both paths share the same `LabelGraph` logic, so reference counting lives only on the server regardless of client.
