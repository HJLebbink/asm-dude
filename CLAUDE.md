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

## Project Overview

AsmDude2 is a Visual Studio 2022/2026 extension that provides assembly language support (x86/x64, SSE, AVX, AVX2, AVX-512) through a Language Server Protocol (LSP) implementation. The project evolved from the original AsmDude VS2015/17/19 plugin into a modern LSP-based architecture.

**✅ PACKAGE ISSUE RESOLVED**: Extension now uses stable, publicly available packages and supports **Visual Studio 2022 & 2026**.

## Architecture

### Active Extension

**asm-dude2-vsix** (.NET Framework 4.8)
   - Uses VSSDK and `ILanguageClient`
   - ✅ Updated to latest stable packages (VS 2022/2026 compatible)
   - ✅ Targets Visual Studio [17.0,19.0) - supports VS 2022 & 2026

**Archived Projects** (moved to `VS\CSHARP\old\`)
   - `asm-dude2-ext`: Failed VisualStudio.Extensibility migration attempt
   - `asm-irony`: Experimental parser (not used)
   - See `VS\CSHARP\old\README.md` for details

### Core Components

1. **Language Server (asm-dude2-ls)**: LSP server (.NET 10.0 LTS)
   - Entry point: `VS\CSHARP\asm-dude2-ls\` (executable)
   - Core implementation: `VS\CSHARP\asm-dude2-ls-lib\` (library)
   - Main class: `LanguageServer.cs` manages LSP communication via StreamJsonRpc
   - Features: syntax highlighting, code completion, signature help, hover info, folding ranges
   - **Semantic Tokens**: Rich syntax highlighting via `textDocument/semanticTokens/full`
   - **LSP Types**: Uses Roslyn's `Microsoft.CodeAnalysis.LanguageServer.Protocol` via IgnoresAccessChecksToGenerator (see below)
   - **VS-specific Types**: Uses `VSTypes.cs` and `VSInternalTypes.cs` for Visual Studio extensions

2. **VS Extension (asm-dude2-vsix)**: Lightweight Visual Studio 2022 extension (.NET Framework 4.8)
   - Location: `VS\CSHARP\asm-dude2-vsix\`
   - Launches and communicates with the LSP server
   - Build process bundles the LSP server into the VSIX package (see `IncludeLanguageServers` target)

### Supporting Libraries

- **asm-tools-lib**: Core assembly language parsing and analysis (.NET 10.0 LTS)
  - Defines fundamental types: `Mnemonic`, `Register`, `Operand`, `KeywordID`
  - Contains instruction data and architecture definitions
  - Shared by both LSP server and simulator

- **asm-tools-lib-net48**: .NET Framework 4.8 version for VSIX compatibility

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
# Modern LSP Server (asm-dude3)
dotnet build VS\CSHARP\asm-dude3\asm-dude3-server\asm-dude3-server.csproj

# Legacy LSP Server (asm-dude2)
dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj

# VS Extension (✅ WORKING)
dotnet build VS\CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj
```

**SDK Requirements**:
- **.NET 10.0 SDK** (10.0.100 or later) - Required for LSP server and core libraries
- **.NET Framework 4.8 Developer Pack** - Required for VS extension (included with VS 2022/2026)

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
| asm-dude2-ls-tests | 45 | 33 | 45 unit tests pass, 33 integration tests skipped |

**Note**: 28 tests in `asm-sim-tests` are skipped due to a known regression (see Known Issues below).

**LSP Integration Tests**: `LspIntegrationTests.cs` contains true JSON-RPC tests over streams. These 33 tests are currently skipped due to Roslyn's internal `SumType` and `DocumentUri` types being incompatible with Newtonsoft.Json serialization. The `IgnoresAccessChecksToGenerator` approach allows C# code to access internal types, but Newtonsoft.Json uses reflection which doesn't benefit from this. Visual Studio internally uses custom serializers for these types. The 45 unit tests in `LanguageServerTests.cs` provide comprehensive coverage without requiring JSON-RPC serialization.

## Known Issues

### ✅ RESOLVED: Migration to Roslyn LSP Types Complete

**Status**: COMPLETE

The LSP server now uses Roslyn's `Microsoft.CodeAnalysis.LanguageServer.Protocol` types directly via the `IgnoresAccessChecksToGenerator` CLR hack. This ensures full compatibility with Visual Studio's LSP implementation.

**Current Setup**:
- ✅ `IgnoresAccessChecksToGenerator` package bypasses internal visibility checks
- ✅ `Microsoft.CodeAnalysis.LanguageServer.Protocol 5.0.0-1.25277.114` provides all LSP types
- ✅ Uses `Uri` type for document identifiers (not string)
- ✅ Uses `int` for Position.Line/Character (Roslyn convention)
- ✅ All 45 asm-dude2-ls-tests pass
- ⚠️ ~50 deprecation warnings (`.Uri` → `.DocumentUri`) - cosmetic, not functional

**IgnoresAccessChecksToGenerator CLR Hack**:
The project uses `IgnoresAccessChecksToGenerator` to bypass C# visibility rules and access Roslyn's internal types. This is documented in `asm-dude2-ls-lib.csproj`. References:
- https://github.com/aelij/IgnoresAccessChecksToGenerator
- https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
- https://github.com/dotnet/roslyn/issues/68696 (Roslyn LSP public API tracking)

**Risks**:
- Internal APIs may change without notice in future Roslyn versions
- This is an unsupported usage pattern
- May break on Roslyn updates - version lock recommended

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
3. Press F5 - launches VS experimental instance
4. Extension will be deployed to the experimental instance

Supported Visual Studio versions: **2022 (17.x) and 2026 (18.x)**

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
Standard LSP types come from Roslyn's `Microsoft.CodeAnalysis.LanguageServer.Protocol` package. However, VS-internal hover types (`ClassifiedTextElement`, `ClassifiedTextRun`) with navigation actions are not part of the standard LSP types. These custom types serialize to the JSON format VS expects for clickable hyperlinks.

## Package Dependencies (Updated for VS 2026)

**Current Packages** (All Stable & Available):
- `StreamJsonRpc` **2.23.41-alpha** (JSON-RPC communication)
- `Microsoft.VisualStudio.SDK` **17.14.40265** (Latest for VS 2022/2026)
- `Microsoft.VSSDK.BuildTools` **17.12.40391** (Latest)
- `Newtonsoft.Json` **13.0.4** (JSON serialization)
- `Microsoft.Extensions.Logging.Abstractions` **10.0.0** (Logging)

### LSP Types via IgnoresAccessChecksToGenerator

**asm-dude2-ls-lib uses Roslyn's internal LSP types** via the `IgnoresAccessChecksToGenerator` CLR hack.

**How It Works**:
- `Microsoft.CodeAnalysis.LanguageServer.Protocol 5.0.0-1.25277.114` contains all LSP types
- All types in this package are marked `internal` by Roslyn
- `IgnoresAccessChecksToGenerator 0.8.0` generates reference assemblies where internal types appear public
- At runtime, the CLR recognizes `IgnoresAccessChecksTo` attribute and bypasses visibility checks

**Configuration in csproj**:
```xml
<ItemGroup>
  <IgnoresAccessChecksTo Include="Microsoft.CodeAnalysis.LanguageServer.Protocol" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.LanguageServer.Protocol" Version="5.0.0-1.25277.114" />
  <PackageReference Include="IgnoresAccessChecksToGenerator" Version="0.8.0" PrivateAssets="All" />
</ItemGroup>
```

**Why This Approach**:
- Roslyn's LSP types are what Visual Studio actually uses internally
- Ensures serialization compatibility with VS (no `FormatException` errors)
- Roslyn team intends to make these types public eventually (issue #68696)
- Avoids maintaining custom LSP type definitions

**Alternative Approaches (Rejected)**:
- `LspTypes 3.16.6` - Outdated (Jan 2021), no longer maintained
- `Microsoft.VisualStudio.LanguageServer.Protocol 17.2.8` - Frozen since May 2022, incompatible APIs
- Custom `Protocol/LspTypes.cs` - Works but requires manual maintenance

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
