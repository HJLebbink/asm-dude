# Archived / Deprecated Projects

This directory contains old code that is no longer actively used or maintained.

## Projects in This Directory

### asm-dude-vsix
**Status**: Legacy / Deprecated
**Target**: Visual Studio 2015, 2017, 2019
**Reason for archival**: Superseded by asm-dude2-vsix

The original AsmDude extension for older Visual Studio versions. Uses older extension architecture and targets VS2015/17/19.

**Superseded by**: `asm-dude2-vsix` which targets VS 2022 & 2026 with LSP-based architecture.

### asm-dude2-ext
**Status**: Incomplete / Non-functional
**Created**: December 2024
**Reason for archival**: Failed migration attempt

An attempt to migrate AsmDude2 to the modern **VisualStudio.Extensibility SDK** (.NET 8.0, out-of-process model).

**Why it failed**:
- LSP (Language Server Protocol) support in VisualStudio.Extensibility is incomplete/abandoned
- Key APIs like `DocumentTypeConfiguration` are missing
- Preview features (`VSEXTPREVIEW_LSP`) never reached stable state
- Microsoft appears to have deprioritized this extensibility model for LSP scenarios

**What's implemented**:
- Basic project structure
- Extension entry point (`Extension.cs`)
- Partial LanguageServerProvider implementation
- Document type definitions
- Configuration/options handling
- Comprehensive migration documentation (`MIGRATION.md`)

**Why keep it**:
- Documents the migration attempt for future reference
- Shows what doesn't work with new SDK
- May be useful if Microsoft ever completes LSP support
- Contains good examples of the new extensibility patterns (for non-LSP scenarios)

### asm-irony
**Status**: Experimental / Unused
**Created**: Earlier development phase
**Reason for archival**: Not used in AsmDude2

An experimental parser that was tested but never integrated into the main AsmDude2 extension.

**Why keep it**:
- Historical reference
- May contain useful parsing ideas
- Documentation of alternative approaches tried

## Active Projects (Not in This Directory)

These are the **currently active and maintained** projects:

- **asm-dude2-vsix**: Main Visual Studio extension (.NET Framework 4.8, VSSDK)
- **asm-dude2-ls**: Language Server executable (.NET 10.0)
- **asm-dude2-ls-lib**: Language Server implementation library (.NET 10.0)
- **asm-tools-lib**: Core assembly language tools (.NET 10.0)
- **asm-tools-lib-net48**: .NET Framework 4.8 version of asm-tools-lib
- **asm-sim-lib**: Assembly simulator using Z3 solver (.NET 10.0)
- **asm-annotate**: Assembly code annotation utility (.NET 10.0)

## Can I Delete This Directory?

**Yes**, if you want to:
- Clean up the repository
- Remove non-functional code
- Simplify the codebase

**No**, if you want to:
- Preserve migration history
- Keep reference implementations
- Document "what we tried"

The code in this directory **does not affect** the working extension in any way.

## Related Documentation

- `../../../CLAUDE.md` - **Current project documentation** (LSP types, build instructions, known issues)
- `../../../SOLUTION.md` - Historical: earlier package fix (superseded)
- `../../../FINAL_SUMMARY.md` - Historical: earlier fix summary (superseded)
- `asm-dude2-ext/MIGRATION.md` - Detailed VS Extensibility SDK migration documentation (if you want to understand what was attempted)

---

*Last Updated: December 2024*
