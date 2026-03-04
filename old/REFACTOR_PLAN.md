# AsmDude2 Major Refactor Plan

**Date**: December 2025
**Objective**: Revive the AsmDude2 extension by migrating from dead/abandoned packages to modern, actively maintained alternatives

---

## Executive Summary

The AsmDude2 extension is at risk of obsolescence due to reliance on **Microsoft.VisualStudio.LanguageServer.Protocol 17.2.8**, which was last updated in **May 2022** and is effectively abandoned. This refactor will migrate to **Microsoft.VisualStudio.LanguageServer.Client 17.14.60** (updated **May 2025**), ensuring long-term viability.

---

## Requirements Gathered

### 1. Scope

**In Scope:**
- ✅ **VS Extension (asm-dude2-vsix)** - Primary focus, clean rewrite
- ✅ **LSP Server (asm-dude2-ls + ls-lib)** - Only if necessary for Client 17.14.60 integration
- ✅ **Architecture** - Keep LSP server separate (modern approach)

**Out of Scope:**
- ❌ Core libraries (asm-tools-lib) - Unless required for integration
- ❌ Simulator (asm-sim-lib) - Separate effort due to Z3/.NET 10 issues
- ❌ Merging LSP into VSIX - Rejected, keeping separation

### 2. Modernization Goals

**What "Up to Date" Means:**
1. **Modern C# Patterns** (C# 14/.NET 10)
   - Nullable reference types
   - Pattern matching
   - Records where appropriate
   - Modern async/await patterns
   - File-scoped namespaces
   - Global usings

2. **VS SDK Best Practices**
   - Latest Visual Studio extensibility patterns
   - Community Toolkit usage (if applicable)
   - Proper async/await throughout
   - Thread-safe practices
   - Modern MEF (Managed Extensibility Framework) v2

3. **LSP Protocol Updates**
   - Support LSP 3.17+ features
   - Semantic tokens for better syntax highlighting
   - Inlay hints (if relevant for assembly)
   - Pull diagnostics
   - Modern protocol capabilities

### 3. Reference Implementations

**Gold Standards to Study:**
- **Microsoft Official Samples**
  - VS SDK samples repository
  - LSP sample extensions
  - Modern VSIX patterns

- **Popular LSP Servers**
  - OmniSharp (C# language server)
  - rust-analyzer (Rust language server)
  - TypeScript/JavaScript language servers
  - Focus on architecture and patterns, not language-specific features

### 4. Core Problem Statement

**Current Pain Points (All Present):**
1. ❌ **Missing LSP Features** - 17.2.8 is based on old LSP spec, missing 3.17+ features
2. ❌ **Compatibility Concerns** - Risk of breaking in VS 2028, future versions
3. ❌ **No Bug Fixes** - Known issues will never be fixed
4. ❌ **Package Conflicts** - Old packages clash with modern dependencies

**Root Cause:**
> "The Old LSP from microsoft is last updated in 2022 and is dead. Making this extension practically also dead. It needs to be revived with language support code that is modern and well maintained."

### 5. Solution: Migrate to Client 17.14.60

**Target Package:**
- **Microsoft.VisualStudio.LanguageServer.Client 17.14.60**
- Released: **May 14, 2025**
- Status: **Actively maintained**
- Used by: Roslyn, VS SDK samples, major extensions

**Why This Works:**
| Aspect | Protocol 17.2.8 (Current) | Client 17.14.60 (Target) |
|--------|---------------------------|---------------------------|
| Last Updated | May 2022 | May 2025 |
| Status | Abandoned | Active |
| LSP Version | 3.16 or older | 3.17+ |
| VS 2026 Support | Uncertain | Yes |
| Bug Fixes | Never | Ongoing |

**Current State:**
- Line 184 in `asm-dude2-vsix.csproj` **explicitly removes** Client.dll
- This removal is part of the problem - we should embrace it instead

### 6. Constraints (Non-Negotiables)

**Must Preserve:**
- ✅ **All Existing Features**
  - Syntax highlighting for assembly (.asm, .cod, .inc, .s)
  - Code completion (mnemonics, registers)
  - Signature help (instruction operands)
  - Hover information (instruction descriptions)
  - Folding ranges (code blocks)
  - Architecture support (x86, x64, SSE, AVX, AVX2, AVX-512)

**Nice to Have (But Not Required):**
- User settings migration (can reset if needed)
- VS 2022 support (focus on VS 2026 okay if necessary)
- Performance improvements (but must not regress)

### 7. Approach: Clean Rewrite

**Strategy:**
```
Create new project → Modern patterns → Migrate features → Replace old
```

**Why Clean Rewrite?**
- Old code built around dead packages
- Opportunity to apply modern patterns from the start
- Easier than incremental migration with old baggage
- Can reference old implementation while building new

**Phases:**
1. **Phase 1: Research & Foundation** (Week 1-2)
   - Study Client 17.14.60 APIs
   - Review Microsoft samples
   - Create new project structure
   - Establish modern patterns

2. **Phase 2: Core Infrastructure** (Week 3-4)
   - LSP server lifecycle management
   - Client-server communication
   - Basic text document handling
   - Logging and diagnostics

3. **Phase 3: Feature Migration** (Week 5-8)
   - Syntax highlighting (semantic tokens)
   - Code completion
   - Signature help
   - Hover information
   - Folding ranges

4. **Phase 4: Polish & Deploy** (Week 9-10)
   - Performance tuning
   - Error handling
   - Testing (manual and automated)
   - Packaging and release

---

## Technical Architecture (Proposed)

### New Project Structure

```
asm-dude3/                          # Clean rewrite
├── asm-dude3-vsix/                 # VS Extension (Client)
│   ├── Uses: Client 17.14.60
│   ├── Framework: .NET Framework 4.8 (required by VSSDK)
│   └── Implements: ILanguageClient or modern equivalent
│
├── asm-dude3-server/               # LSP Server (if still separate)
│   ├── Uses: StreamJsonRpc 2.23+
│   ├── Framework: .NET 10.0
│   └── Implements: LSP 3.17+ protocol
│
└── asm-tools-lib/                  # Shared (reuse existing if possible)
    └── Assembly parsing logic
```

### Key Architectural Decisions

**1. Keep LSP Server Separate?**
- ✅ **Yes** - Modern, portable, multi-editor support
- Client 17.14.60 still supports external servers
- Easier to test and develop independently

**2. Communication Protocol**
- StreamJsonRpc 2.23.32-alpha (you already have this)
- Named pipes for Windows
- stdio for cross-platform (if needed)

**3. Text Synchronization**
- Use LSP's built-in sync (incremental if supported)
- Avoid custom sync logic

---

## Migration Path from 17.2.8 → 17.14.60

### What Changes?

**API Differences (Need to Research):**
- Protocol package: Type definitions only
- Client package: Full client infrastructure + types
- May have different initialization patterns
- Likely more modern async patterns

**Benefits:**
- Built-in capabilities negotiation
- Better error handling
- Modern LSP 3.17+ support
- Active maintenance and bug fixes

### Key Research Questions

Before starting Phase 1, we need to answer:
1. What's the Client 17.14.60 API surface? (ILanguageClient still used?)
2. How does server lifecycle management differ?
3. What LSP 3.17+ features are supported?
4. Any breaking changes from old patterns?
5. How do Microsoft samples use it?

---

## Success Criteria

**Definition of Done:**
1. ✅ Extension installs on VS 2022 and 2026
2. ✅ All existing features work (syntax, completion, hover, folding)
3. ✅ Uses Client 17.14.60 (not Protocol 17.2.8)
4. ✅ Modern C# 14 patterns throughout
5. ✅ Follows VS SDK best practices
6. ✅ LSP 3.17+ protocol compliance
7. ✅ Passes manual testing checklist
8. ✅ No compiler errors, minimal warnings

**Nice to Have:**
- Performance improvements over old version
- New LSP 3.17 features (semantic tokens, inlay hints)
- Better error messages and logging
- Automated tests

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Client 17.14.60 API very different | High | Study samples first, prototype early |
| Features don't map to new API | Medium | Keep old code as reference, adapt patterns |
| Performance regression | Medium | Profile early, optimize as needed |
| Breaking changes in VS 2026 | Low | Client 17.14.60 is designed for VS 2026 |
| Time estimate too aggressive | Medium | Phases can be extended, incremental releases |

---

## Next Steps

### Immediate Actions
1. ✅ Document requirements (this file)
2. 🔄 Research Client 17.14.60 API
3. 📋 Study Microsoft LSP samples
4. 📋 Create new project structure (asm-dude3)
5. 📋 Build proof-of-concept with basic syntax highlighting

### Phase 1 Deliverables
- [ ] Client 17.14.60 API understanding documented
- [ ] New Visual Studio solution (asm-dude3)
- [ ] Basic LSP server that responds to initialize
- [ ] Extension that connects to server using Client 17.14.60
- [ ] Minimal syntax highlighting working (proof-of-concept)

---

## Questions for User

Before proceeding to implementation:
1. Should we create `asm-dude3` as a new directory, or replace `asm-dude2` in place?
2. Any specific LSP 3.17+ features you want prioritized (semantic tokens, inlay hints)?
3. Target release date or timeline pressure?
4. Want to keep old `asm-dude2` around for reference, or remove once migrated?

---

**Status**: Requirements Complete ✅
**Next**: Begin Phase 1 - Research & Foundation
