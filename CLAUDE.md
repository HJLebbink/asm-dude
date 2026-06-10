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
   - ✅ Uses VisualStudio.Extensibility SDK v18.5.39115-Preview
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

- **asm-sim-lib**: Assembly instruction simulator using Z3 solver (.NET 10.0 LTS)

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

**Test frameworks**: xUnit (asm-dude2-ls-tests), MSTest (asm-tools-tests, asm-sim-tests)

**Test Results Summary**:
| Project | Passed | Skipped | Notes |
|---------|--------|---------|-------|
| asm-tools-tests | 31 | 0 | Core assembly tools (incl. arch DNF parse, tile/operand) |
| asm-sim-tests | 178 | 3 | Z3 simulator (DynamicFlow merge crash FIXED; 3 skips unrelated) |
| asm-dude2-ls-tests | 126 | 37 | Unit + AsmSim integration; 5 pre-existing failures (hover/semantic-token, unrelated to signatures) |
| asm-annotate-tests | 16 | 0 | **NEW** — stage-1 PDF→MD text/title heuristics |
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

#### 1. Experimental VS — Output Window → "Extensions" pane (check first)
In the **experimental** VS instance (the one that opens), go to View → Output → select "Extensions" or "VisualStudio.Extensibility" from the dropdown. Shows:
- Extension host loading your VSIX
- Errors from `CreateServerConnectionAsync`
- LSP communication issues

#### 2. ServiceHub Extension Host Log (extension won't load at all)
The OOP extension host process logs here:
```
%TEMP%\ServiceHub\logs\*ServiceHub.Host.Extensibility*
```
Look for the most recent file. This is where assembly loading errors appear (e.g., wrong .NET target framework).

#### 3. LSP Server Log (extension loaded, LSP misbehaves)
The LSP server (`AsmDude2.LSP.exe`) writes to `LanguageServer.log` in its working directory. When deployed, this is inside the extension's `Server/` folder:
```
%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_<id>Exp\VSExtensions\Henk-Jan Lebbink\AsmDude2\<version>\Server\
```

#### 4. Activity Log (VS startup/discovery issues)
```
%APPDATA%\Microsoft\VisualStudio\18.0_<id>Exp\ActivityLog.xml
```
Useful for extension discovery and registration problems, rarely needed for runtime debugging.

#### Not relevant for runtime debugging:
- **Host VS** (where you press F5): its Output window and logs only show build/deploy status, not extension runtime errors
- **Build logs**: only relevant for compile and deployment errors

**Check order**: 1 → 2 → 3 → 4

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

### Instruction-data pipeline (pdf → md → txt)
`asm-annotate extract` (PDF→MD, stage 1) → copy `output/*.md` to `asm-dude.wiki/doc/` →
`asm-annotate gen-signatures` (MD→`signature-mar2026.txt` + `overview.txt` + wiki `Home.md`, stage 2) →
LSP server (stage 3). All stages are now subcommands of the one `asm-annotate` tool (the old
`intel-doc-2-data` project was folded in as `gen-signatures`).
The arch column is **DNF** (`+`=AND, `,`=OR, e.g. `AVX512_VL+AVX512_F,AVX10`); `Home.md`'s arch column
is a flattened union. New ISA covered incl. AMX tile registers (`TMM0-7`), AVX10, FP16, Key Locker.
Each stage has tests in `asm-annotate-tests` (+ `asm-dude2-ls-tests` for the server stage).

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
| 0x4 | `deprecated` | Deprecated instructions (MnemonicOff) |
| 0x8 | `readonly` | Immediate values |

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
2. Server retrieves parsed tokens from `parsedDocuments` dictionary
3. Each `KeywordID` (with `AsmTokenType`) is mapped to LSP semantic token type/modifiers
4. Returns encoded `uint[]` array with delta-encoded positions and token info

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
The SDK generates `Microsoft.VisualStudio.RpcContracts` with a matching version at build time. If the SDK minor version is higher than VS (e.g., SDK 18.6 on VS 18.5), VS rejects the extension because it doesn't have the newer RpcContracts. If the SDK version is too old (e.g., SDK 18.2 on VS 18.5), commands may silently fail to register. Check your VS version via Help → About, then use the matching SDK preview from the vssdk feed. Example: VS 2026 **18.5** → SDK **18.5**.39115-Preview.

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
- `asm-options-lib`: Shared, dependency-light settings contract (`AsmSettingsData` + `ColorJsonConverter`); referenced by both the VSIX and the server
- `asm-tools-lib`: Core assembly language tools (.NET 10.0 LTS; single-targeted, no net48)
- `asm-tools-tests`: Tests for asm-tools-lib (MSTest)
- `asm-sim-lib`: Assembly simulator using Z3 (.NET 10.0 LTS)
- `asm-sim-tests`: Tests for asm-sim-lib (MSTest)
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
| `AsmCodeLensTagger.cs` | `TextViewTagger<CodeLensTag>` — scans the document, produces `CodeLensTag`s for label defs and sim-state lines; encodes payload in `CodeElement.Description` |
| `AsmLabelCodeLens.cs` | `InvokableCodeLens` — renders "N references" from the tag's `refcount:N\|...` description |
| `AsmSimStateCodeLens.cs` | `InvokableCodeLens` — renders the sim-state label from the tag's `simstate:\|...` description (display-only; click is a no-op) |

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
