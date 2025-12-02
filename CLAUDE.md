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

1. **Language Server (asm-dude3-server)**: Modern LSP server (.NET 10.0 LTS) with enhanced features
   - Location: `VS\CSHARP\asm-dude3\asm-dude3-server\`
   - Main class: `LanguageServer.cs` manages LSP communication via StreamJsonRpc
   - **NEW: Clickable Hyperlinks** - Hover over mnemonics shows clickable links to documentation
   - Features: syntax highlighting, code completion, signature help, hover info, folding ranges, performance data
   - Uses custom LSP types in `Protocol\LspTypes.cs` for LSP communication
   - Uses `VSInternalHover` with `ClassifiedTextElement` for clickable hyperlinks in Visual Studio

2. **Language Server (asm-dude2-ls)**: Original LSP server (.NET 10.0 LTS) [Legacy]
   - Entry point: `VS\CSHARP\asm-dude2-ls\` (executable)
   - Core implementation: `VS\CSHARP\asm-dude2-ls-lib\` (library)
   - Main class: `LanguageServer.cs` manages LSP communication via StreamJsonRpc
   - Handles: syntax highlighting, code completion, signature help, hover info, folding ranges

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
# LSP Server
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
```

Test framework: MSTest

**Note**: 28 tests in `asm-sim-tests` are skipped due to a known regression (see Known Issues below).

## Known Issues

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

## Package Dependencies (Updated for VS 2026)

**Current Packages** (All Stable & Available):
- `Microsoft.VisualStudio.LanguageServer.Protocol` **17.2.8** (Last stable)
- `StreamJsonRpc` **2.22.23** (Latest - Oct 2024)
- `Microsoft.VisualStudio.SDK` **17.14.40265** (Latest for VS 2022/2026)
- `Microsoft.VSSDK.BuildTools` **17.12.40391** (Latest)
- `Extended.Wpf.Toolkit` **4.6.1** (Latest)

**Previous Problem** (RESOLVED):
- Used unavailable preview package: 17.8.9-preview
- Blocked all new contributors from building

**Solution Applied**:
- Downgraded LSP Protocol to stable 17.2.8
- Upgraded all other packages to latest
- Extended VS target range to [17.0,19.0) for VS 2026 support

See `SOLUTION.md` for complete details.

## Project Structure

### Active Projects (Current Development)
- `asm-dude2-vsix`: **Main extension** for VS 2022/2026 (.NET Framework 4.8)
- `asm-dude2-ls`: Language server executable (.NET 10.0 LTS)
- `asm-dude2-ls-lib`: Language server implementation (.NET 10.0 LTS)
- `asm-tools-lib`: Core assembly language tools (.NET 10.0 LTS)
- `asm-tools-lib-net48`: .NET Framework 4.8 version
- `asm-sim-lib`: Assembly simulator using Z3 (.NET 10.0 LTS)
- `asm-annotate`: Assembly annotation utility (.NET 10.0 LTS)

### Archived Projects (`VS\CSHARP\old\`)
- `asm-dude-vsix`: Legacy extension for VS2015/17/19
- `asm-dude2-ext`: Failed VisualStudio.Extensibility migration
- `asm-irony`: Experimental parser (unused)

**Focus development on `asm-dude2-vsix`** and the LSP server projects.
