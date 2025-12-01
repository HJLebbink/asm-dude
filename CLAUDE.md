# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AsmDude2 is a Visual Studio 2022 extension that provides assembly language support (x86/x64, SSE, AVX, AVX2, AVX-512) through a Language Server Protocol (LSP) implementation. The project evolved from the original AsmDude VS2015/17/19 plugin into a modern LSP-based architecture.

## Architecture

The codebase is split into two main components:

1. **Language Server (asm-dude2-ls)**: Standalone LSP server (.NET 7.0) that provides all language features
   - Entry point: `VS\CSHARP\asm-dude2-ls\` (executable)
   - Core implementation: `VS\CSHARP\asm-dude2-ls-lib\` (library)
   - Main class: `LanguageServer.cs` manages LSP communication via StreamJsonRpc
   - Handles: syntax highlighting, code completion, signature help, hover info, folding ranges

2. **VS Extension (asm-dude2-vsix)**: Lightweight Visual Studio 2022 extension (.NET Framework 4.8)
   - Location: `VS\CSHARP\asm-dude2-vsix\`
   - Launches and communicates with the LSP server
   - Build process bundles the LSP server into the VSIX package (see `IncludeLanguageServers` target)

### Supporting Libraries

- **asm-tools-lib**: Core assembly language parsing and analysis (.NET 7.0)
  - Defines fundamental types: `Mnemonic`, `Register`, `Operand`, `KeywordID`
  - Contains instruction data and architecture definitions
  - Shared by both LSP server and simulator

- **asm-tools-lib-net48**: .NET 4.8 version for VSIX compatibility

- **asm-sim-lib**: Assembly instruction simulator using Z3 solver

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
dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj
dotnet build VS\CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj
```

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

## Development Workflow

To debug the extension:
1. Open `VS\AsmDude.sln` in Visual Studio 2022
2. Set `asm-dude2-vsix` as startup project
3. Press F5 - launches VS experimental instance
4. The extension will be deployed with DeployExtension=True in Debug mode

The VSIX project has a dependency on `asm-dude2-ls` to ensure the LSP server builds first.

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

## Project History

The codebase contains legacy projects from AsmDude (VS2015/17/19):
- `asm-dude-vsix`: Old extension (not actively developed)
- `asm-irony`: Experimental parser (not used in AsmDude2)

Focus development on AsmDude2 components (projects with "2" suffix or "ls" for language server).
