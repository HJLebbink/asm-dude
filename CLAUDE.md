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

**asm-dude2-vsix** (.NET 10.0-windows) — **Modern out-of-process extension**
   - ✅ Uses VisualStudio.Extensibility SDK v17.14.40608
   - ✅ LSP-based architecture (launches separate asm-dude2-ls process)
   - ✅ Targets Visual Studio 2022 & 2026
   - ✅ Fully debuggable with F5 (launches experimental instance via launchSettings.json)
   - Bundles LSP server (asm-dude2-ls) in `Server/` subdirectory of VSIX package

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
  - `asm-fuzz` (fuzzing via QuickCheck)
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

- **asm-tools-lib**: Core assembly language parsing and analysis (.NET 10.0-windows)
  - Defines fundamental types: `Mnemonic`, `Register`, `Operand`, `KeywordID`
  - Contains instruction data and architecture definitions
  - Single-targeted for .NET 10.0-windows (dropped net48 support)

- **asm-sim-lib**: Assembly instruction simulator using Z3 solver (.NET 10.0 LTS)

- **asm-annotate**: Utility for annotating assembly code

- **intel-doc-2-data**: Processes Intel instruction documentation

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
| asm-tools-tests | 27 | 0 | Core assembly tools |
| asm-sim-tests | 149 | 28 | Z3 simulator (28 skipped due to known issue) |
| asm-dude2-ls-tests | 66 | 36 | 66 unit tests pass, 36 integration tests skipped |

**Note**: 28 tests in `asm-sim-tests` are skipped due to a known regression (see Known Issues below).

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
   Expected: **149 passed, 28 skipped, 0 failed**.
7. Verify `libz3.dll` appears in `VS/CSHARP/asm-dude2-ls/bin/Debug/net10.0-windows/`.

The NuGet source `local-z3` → `local-nuget/` is already configured in `NuGet.config`.

---

### Z3 Context Lifecycle Bug in DynamicFlow (Regression)

**Status**: Tests skipped, awaiting architectural fix

**Affected tests** (27 DynamicFlow-related, 28 total skipped):
- `Test_DynamicFlow` class (2 tests) in `Test_ExecutionTree.cs`
- `Test_BitTricks_LegatosMultiplier` in `Test_BitTricks.cs`
- 24 tests in `Test_Runner.cs` (all except `Test_Runner_Several_Mnemonics`)

**Root cause**: `StateUpdate` objects create their own Z3 contexts. When state merging happens in `DynamicFlow`, `BranchInfo.Translate` attempts to translate Z3 expressions between contexts, but source contexts may already be disposed.

**Crash location**: `BranchInfo.Translate` → `Z3_translate` native call (0xC0000005)

**Key code locations**:
- `StateUpdate.cs` lines 139, 153 - context creation
- `BranchInfoStore.cs` line 211 - translation call
- `BranchInfo.cs` line 45 - crash site
- `DynamicFlow.cs` line 800 - `using` block that disposes context prematurely

**Workaround**: Tests are marked with `[Ignore]` attribute. The working test `Test_Runner_Several_Mnemonics` uses `SimpleStep` instead of `DynamicFlow`.

**Fix approach**: Requires architectural changes to either:
1. Use a single shared Z3 context across all operations, or
2. Translate expressions immediately when stored rather than on retrieval

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
- Performance data: TSV files in `asm-dude2-ls-lib\Resources\Performance\` (Haswell, Skylake, etc.)
- Signature files: Hand-curated instruction signatures in `Resources\signature-*.txt`

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

**Token Types** (defined in `LanguageServerTarget.cs`):
| Index | Type | Used For |
|-------|------|----------|
| 0 | `keyword` | Mnemonics (MOV, ADD, etc.) |
| 1 | `variable` | Registers (RAX, EAX, etc.) |
| 2 | `label` | Labels (loop_start:, etc.) |
| 3 | `macro` | Directives (.data, PROC, etc.) |
| 4 | `number` | Immediate values (0x10, 42, etc.) |
| 5 | `operator` | Memory operands ([rax], etc.) |
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
Mnemonic    → keyword (0)
MnemonicOff → keyword (0) + deprecated modifier
Register    → variable (1)
Label       → label (2)
LabelDef    → label (2) + declaration+definition modifiers
Jump        → function (8)
Directive   → macro (3)
Constant    → number (4) + readonly modifier
Remark      → comment (6)
Misc        → operator (5)
```

**Key Files:**
- `LanguageServerTarget.cs` - Defines `SemanticTokensProvider` capability and `SemanticTokensLegend`
- `LanguageServer.cs` - Implements `GetSemanticTokens()`, `MapTokenType()`, `GetTokenModifiers()`

**How It Works:**
1. Client requests `textDocument/semanticTokens/full` for a document
2. Server retrieves parsed tokens from `parsedDocuments` dictionary
3. Each `KeywordID` (with `AsmTokenType`) is mapped to LSP semantic token type/modifiers
4. Returns encoded `uint[]` array with delta-encoded positions and token info

### Clickable Hyperlinks in Hover (asm-dude2-ls-lib)

**Implementation:** `VS\CSHARP\asm-dude2-ls-lib\`

The LSP server implements clickable hyperlinks in hover tooltips using custom types that serialize to Visual Studio's internal format:

**Key Files:**
- `VSInternalTypes.cs` - Custom types for VS-specific hover extensions:
  - `VSInternalHover` - Enhanced hover with `RawContent` property
  - `ClassifiedTextElement` - Text with classification and navigation
  - `ClassifiedTextRun` - Individual text run with URL navigation action
  - `ContainerElement` - Container for organizing hover elements

- `HoverBuilder.cs` - Factory for creating hover responses:
  - `CreateHoverWithLink()` - Builds VSInternalHover with clickable links
  - `CreateStandardHover()` - Fallback for standard LSP hover

- `MnemonicStore.cs` - Provides URLs via `GetHtmlRef()` method
- `LanguageServer.cs` - Returns `object` (VSInternalHover or Hover) based on URL availability

**How It Works:**
1. When hovering over a mnemonic (e.g., "MOV"), the server retrieves:
   - Description from signature files
   - URL from MnemonicStore.GetHtmlRef()
   - Performance data from PerformanceStore (if enabled)

2. If URL exists, LanguageServer creates VSInternalHover with:
   - `Contents` - Standard markdown (LSP compatibility)
   - `RawContent` - ClassifiedTextElement with clickable link
   - NavigationAction - URL that opens when clicked

3. Visual Studio recognizes VSInternalHover and renders clickable hyperlink

**Why Custom Types:**
VS-internal hover types (`ClassifiedTextElement`, `ClassifiedTextRun`) with navigation actions are not part of the standard LSP protocol. These custom types in `VSInternalTypes.cs` serialize to the JSON format VS expects for clickable hyperlinks.

## Package Dependencies (Updated for VS 2026)

**Current Packages** (All Stable & Available):
- `Microsoft.VisualStudio.LanguageServer.Protocol` **18.5.3** (LSP types - public API, vssdk feed)
- `Microsoft.VisualStudio.LanguageServer.Protocol.Extensions` **18.5.3** (VS-specific LSP extensions)
- `StreamJsonRpc` **2.25.9** (`asm-dude2-ls-lib` only) / **2.24.84** (`asm-dude2-vsix` only — see note below)
- `Microsoft.VisualStudio.SDK` **17.14.40265** (VS 2022/2026 — see note below)
- `Microsoft.VSSDK.BuildTools` **17.14.2120** (VS 2022/2026 — see note below)
- `Microsoft.Extensions.Logging.Abstractions` **10.0.3** (Logging)

**⚠ DO NOT upgrade `Microsoft.VisualStudio.SDK`, `Microsoft.VSSDK.BuildTools`, or `Microsoft.VisualStudio.Threading.Analyzers` to version 18.x.**
The plugin must support **both VS 2022 (17.x) and VS 2026 (18.x)**. These three packages at 18.x target VS 2026 only and drop VS 2022 compatibility. Stay on 17.14.x.

**⚠ DO NOT upgrade `StreamJsonRpc` in `asm-dude2-vsix` beyond 2.24.84.**
The VSIX references StreamJsonRpc with `<ExcludeAssets>runtime</ExcludeAssets>` — it does **not** bundle the DLL but relies on Visual Studio to provide it at runtime. VS 2022 ships StreamJsonRpc 2.24.x; upgrading the reference to 2.25.x causes a `FileNotFoundException` ("StreamJsonRpc Version 2.25.0.0 is not found") when the extension loads. The LSP server (`asm-dude2-ls-lib`) bundles its own copy and may use a newer version independently.

**NuGet Sources**: Requires both nuget.org and vssdk feed (configured in `NuGet.config`):
```
https://pkgs.dev.azure.com/azure-public/vside/_packaging/vssdk/nuget/v3/index.json
```

## Project Structure

### Active Projects (Current Development)
- `asm-dude2-vsix`: **Main extension** for VS 2022/2026 (.NET Framework 4.8)
- `asm-dude2-ls`: Language server executable (.NET 10.0 LTS)
- `asm-dude2-ls-lib`: Language server implementation (.NET 10.0 LTS)
- `asm-dude2-ls-tests`: Unit tests for LSP server (xUnit)
- `asm-tools-lib`: Core assembly language tools (.NET 10.0 LTS)
- `asm-tools-lib-net48`: .NET Framework 4.8 version
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

### Assembly CodeLens (asm-dude2-vsix)

**Implementation:** `VS\CSHARP\asm-dude2-vsix\CodeLens\`

Shows "N references" above each assembly label definition line, with click-to-navigate. Implemented as a WPF adornment (not the official `IAsyncCodeLensDataPointProvider`, which requires complex async provider/tagger/aggregator machinery and doesn't integrate with LSP easily).

**Why adornment-based, not `IAsyncCodeLensDataPointProvider`:**
The official Roslyn CodeLens path (`src/VisualStudio/Core/Def/CodeLens/` in `github.com/dotnet/roslyn`) uses `IAsyncCodeLensDataPointProvider` with VS-internal types. It requires a `ICodeLensCallbackListener` service and is tightly coupled to the Roslyn workspace model. Our LSP-based approach is simpler: request data from the language server, render a TextBlock adornment above the line.

**Reference implementations:**
- Roslyn CodeLens: `github.com/dotnet/roslyn`, `src/VisualStudio/Core/Def/CodeLens/`
- IntraText adornments: `github.com/microsoft/VSSDK-Extensibility-Samples`

**Files:**

| File | Role |
|------|------|
| `AsmCodeLensProvider.cs` | MEF exports: `IWpfTextViewCreationListener` (creates manager), `ILineTransformSourceProvider` (creates transform source) |
| `AsmCodeLensLineTransformSource.cs` | `ILineTransformSource` — adds `topSpace` pixels above label lines to make room for the TextBlock |
| `AsmCodeLensAdornmentManager.cs` | Requests LSP data, renders TextBlock adornments, handles clicks and hover underline |
| `AsmCodeLensMouseProcessor.cs` | `IMouseProcessor` — routes `MouseMove` to manager for hover underline tracking |

**Data flow:**
1. `AsmCodeLensAdornmentManager.RequestCodeLensData()` calls `AsmLanguageClient.SendCodeLensDataRequestAsync()` (custom `asm/codeLensData` LSP method)
2. LSP server returns JSON array: `[{ label, definitionLine, referenceLines[] }, ...]`
3. Manager stores data in `codeLensData` dict, calls `lineTransformSource.UpdateLabelLines()` with definition line numbers
4. `ILineTransformSource.GetLineTransform()` returns `LineTransform(topSpace, 0, 1.0)` for label lines — this adds blank space above the line
5. On `LayoutChanged`, manager renders a `TextBlock` per visible label line, positioned at `viewLine.Top - topSpace`

**Font metrics (via `IClassificationFormatMap`):**
Derived from the live editor format map so they scale correctly when the user changes font/size:
- `IClassificationFormatMapService.GetClassificationFormatMap(textView)` → injected via MEF `[Import]` in `AsmCodeLensLineTransformSourceProvider`
- `formatMap.DefaultTextProperties.FontRenderingEmSize` = editor font size (e.g. 16px at Consolas 12pt)
- `CodeLensFontSize = editorFontSize × 0.70` (CodeLens text at 70% of editor font)
- `topSpace = Math.Ceiling(CodeLensFontSize × GlyphTypeface.Baseline)` — typographic ascent only (NOT full line height); `GlyphTypeface.Baseline ≈ 0.727` for Consolas
- `ClassificationFormatMappingChanged` event invalidates cache; VS re-layouts the view, which re-calls `GetLineTransform` with fresh values

**Why typographic ascent, not full TextBlock height:**
`GlyphTypeface.Baseline = sTypoAscender / unitsPerEm` is the ratio of the font's capital/ascender height to the em square. Using the full `TextBlock.DesiredSize.Height` as `topSpace` includes transparent descender space and leading, creating a visible empty gap above the text. Using just the ascent makes the visible ink sit flush above the label line.

**Click mechanism:**
`MouseLeftButtonDown` is attached directly to each `TextBlock` (cursor changes to `Hand`, proving the TextBlock is hit-test visible and WPF routes clicks to it). Clicking navigates to a reference line (single reference) or shows a `Popup` list (multiple references). The popup items also use `MouseLeftButtonDown` for navigation.

**Hover underline:**
`AsmCodeLensMouseProcessor.PreprocessMouseMove` calls `manager.UpdateHover(position)`. The manager checks if the mouse position is inside any active block's `Rect bounds` (stored in `activeBlocks` list) and sets/clears `TextDecorations.Underline` on the hovered `TextBlock`.
