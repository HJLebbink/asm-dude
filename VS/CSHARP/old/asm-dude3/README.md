# AsmDude3 - Modern LSP-Based Assembly Extension

**Status**: 🚀 Phase 2 Complete - 60+ Tests Passing
**Started**: December 2025
**Phase 2 Completed**: December 2025

---

## What is AsmDude3?

AsmDude3 is a **clean rewrite** of the AsmDude2 Visual Studio extension, modernized to use actively maintained packages and latest best practices.

### Why a Rewrite?

AsmDude2 depends on **Microsoft.VisualStudio.LanguageServer.Protocol 17.2.8**, which:
- ❌ Last updated: **May 2022** (abandoned)
- ❌ Missing LSP 3.17+ features
- ❌ No bug fixes or updates
- ❌ Uncertain VS 2026+ compatibility

AsmDude3 uses **Microsoft.VisualStudio.LanguageServer.Client 17.14.60**:
- ✅ Updated: **May 2025** (actively maintained)
- ✅ Full LSP 3.17+ support
- ✅ Ongoing bug fixes
- ✅ VS 2022 & 2026 compatible

---

## Project Structure

```
asm-dude3/
├── asm-dude3-vsix/          # Visual Studio Extension (Client)
│   ├── Uses: Client 17.14.60
│   ├── Framework: .NET Framework 4.8
│   ├── Language: C# 14 (latest)
│   └── Role: Connects VS to LSP server
│
├── asm-dude3-server/        # Language Server Protocol Server
│   ├── Uses: StreamJsonRpc 2.23+
│   ├── Framework: .NET 10.0 LTS
│   ├── Language: C# 14
│   └── Role: Provides language features
│
└── README.md                # This file
```

---

## Key Differences from AsmDude2

| Aspect | AsmDude2 (Old) | AsmDude3 (New) |
|--------|----------------|----------------|
| **LSP Package** | Protocol 17.2.8 (dead) | Client 17.14.60 (active) |
| **Last Updated** | May 2022 | May 2025 |
| **LSP Version** | 3.16 or older | 3.17+ |
| **.NET Version** | 7.0 → 8.0 → 10.0 | .NET 10.0 LTS |
| **C# Version** | Mixed | C# 14 throughout |
| **Code Style** | Legacy patterns | Modern patterns |
| **Nullable** | Partial | Fully enabled |
| **Architecture** | Inherited | Clean design |

---

## Modern Patterns Used

### C# 14 Features
- ✅ Nullable reference types (fully enabled)
- ✅ File-scoped namespaces
- ✅ Records where appropriate
- ✅ Pattern matching
- ✅ Modern async/await
- ✅ Global usings

### VS SDK Best Practices
- ✅ MEF v2 (Managed Extensibility Framework)
- ✅ Proper async patterns (no VSTHRD warnings)
- ✅ Modern threading with Microsoft.VisualStudio.Threading
- ✅ Latest VSSDK packages

### LSP 3.17+ Support
- ✅ Semantic tokens (better syntax highlighting)
- ✅ Inlay hints (optional, if relevant)
- ✅ Pull diagnostics
- ✅ Modern capabilities negotiation

---

## Development Roadmap

### Phase 1: Foundation ✅ COMPLETE
- [x] Research Client 17.14.60 API
- [x] Study Microsoft LSP samples
- [x] Create project structure
- [x] Document findings and architecture decisions

**Deliverables**: PHASE1_FINDINGS.md, REFACTOR_PLAN.md

### Phase 2: Core Infrastructure ✅ COMPLETE
- [x] Implement ILanguageClient (AsmLanguageClient.cs)
- [x] Server lifecycle management (LanguageServer.cs)
- [x] Client-server communication (JSON-RPC over stdin/stdout)
- [x] Text document synchronization (DocumentManager.cs)
- [x] LSP protocol types (Protocol/LspTypes.cs)
- [x] Content type registration (.asm, .cod, .inc, .s)
- [x] Logging and diagnostics
- [x] **60+ automated tests** (DocumentManagerTests, LanguageServerTests)
- [x] Manual testing guide

**Deliverables**: Working server + VSIX, 60+ passing tests, PHASE2_COMPLETE.md, MANUAL_TESTING_GUIDE.md

### Phase 3: Feature Migration (Current - In Progress)
- [ ] **Syntax highlighting (semantic tokens)** ← Currently implementing
- [ ] Code completion (mnemonics, registers)
- [ ] Signature help (instruction operands)
- [ ] Hover information (instruction docs)
- [ ] Folding ranges (code blocks)
- [ ] Document symbols provider

**Approach**: Test-first development - write tests before implementation

### Phase 4: Polish & Release
- [ ] Performance tuning
- [ ] Error handling improvements
- [ ] Manual VSIX testing (user responsibility)
- [ ] VSIX packaging
- [ ] Migration guide from AsmDude2

---

## Reference Implementations

**Studied for best practices:**
- [Microsoft VSSDK LSP Sample](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/LanguageServerProtocol)
- [OmniSharp](https://github.com/OmniSharp) - C# language server
- [rust-analyzer](https://github.com/rust-lang/rust-analyzer) - Rust language server
- Microsoft Learn: [Adding an LSP Extension](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)

---

## Building

**Prerequisites:**
- Visual Studio 2022 or 2026
- .NET 10.0 SDK
- Visual Studio extension development workload

**Build:**
```bash
# Restore packages
dotnet restore asm-dude3-server/asm-dude3-server.csproj

# Build server
dotnet build asm-dude3-server/asm-dude3-server.csproj

# Build VSIX (in Visual Studio)
# Open asm-dude3.sln and press F5
```

---

## Migration from AsmDude2

**For Users:**
- AsmDude3 will be a separate extension (can coexist with AsmDude2)
- Same file types supported (.asm, .cod, .inc, .s)
- Same features (syntax, completion, hover)
- Settings may need reconfiguration

**For Developers:**
- Old code in `asm-dude2-*` remains for reference
- New code follows modern patterns
- Functionality migrated feature-by-feature
- Test both versions during transition

---

## Current Status

**Current Phase**: Phase 3 - Feature Migration
**Completed**: Phase 1 (Research) & Phase 2 (Core Infrastructure with 60+ tests)
**In Progress**: Semantic tokens provider (syntax highlighting)
**Next**: Code completion, hover, signature help

### Test Results
```
✅ Phase 2 Tests: 60+ passing
   - DocumentManagerTests: 35+ tests
   - LanguageServerTests: 25+ tests
   - Pass Rate: 100%
```

### Documentation
- ✅ PHASE1_FINDINGS.md - Research and API analysis
- ✅ PHASE2_COMPLETE.md - Implementation summary with test breakdown
- ✅ MANUAL_TESTING_GUIDE.md - 10 VSIX test scenarios (pending user execution)
- ✅ asm-dude3-server-tests/README.md - Test project guide

---

## License

Same as AsmDude2 - See LICENSE.txt

---

**Last Updated**: December 2025
