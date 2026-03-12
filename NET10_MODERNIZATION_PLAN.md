# .NET 10 Modernization Plan

## Executive Summary

This document outlines the comprehensive plan to modernize the asm-* codebase to proper .NET 10 standards while maintaining functionality and fixing build issues.

## Current Architecture Issues

### 1. Circular Dependency
```
asm-tools-lib ──────> asm-sim-lib
     ^───────────────
```

**Root Cause**: 
- asm-tools-lib contains core parsing utilities
- asm-sim-lib depends on asm-tools-lib for type definitions
- But asm-tools-lib may reference asm-sim-lib types in its code

**Solution Options**:
- **Option A**: Extract shared types to `asm-core-lib` (recommended)
- **Option B**: Remove circular dependency by refactoring
- **Option C**: Keep as-is if build works (not recommended)

### 2. Project Reference Mismatch
The asm-tools-lib.csproj shows:
```xml
<ItemGroup>
  <ProjectReference Include="..\asm-sim-lib\asm-sim-lib.csproj" />
</ItemGroup>
```

But the README.md and PROJECT_STRUCTURE.md indicate the dependency order should be:
1. asm-tools-lib (base)
2. asm-sim-lib (depends on asm-tools-lib)

This suggests asm-tools-lib should NOT depend on asm-sim-lib.

### 3. AsmLanguageServerOptions Design
Current design uses public fields:
```csharp
[DataMember]
public System.Drawing.Color SyntaxHighlighting_Opcode;
```

Modern C# should use properties:
```csharp
[DataMember]
public System.Drawing.Color SyntaxHighlighting_Opcode { get; init; } = Colors.Black;
```

## Implementation Steps

### Step 1: Analyze Dependencies (Current Session)
- [ ] Identify all circular dependencies
- [ ] Document type usage between libraries
- [ ] Create dependency graph

### Step 2: Fix Project Structure
- [ ] Remove circular dependency
- [ ] Ensure correct ProjectReferences
- [ ] Verify build order

### Step 3: Modernize Core Types
- [ ] Convert fields to properties
- [ ] Add init setters
- [ ] Add default values

### Step 4: Clean Up Code
- [ ] Remove commented-out code
- [ ] Fix naming conventions
- [ ] Complete nullable annotations

### Step 5: Verify Build
- [ ] Build all projects
- [ ] Fix any remaining errors
- [ ] Run tests

## Immediate Actions Required

### Action 1: Remove Circular Dependency
**File**: `VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj`
**Change**: Remove the ProjectReference to asm-sim-lib if it exists

### Action 2: Modernize AsmLanguageServerOptions
**File**: `VS/CSHARP/asm-tools-lib/AsmLanguageServerOptions.cs`
**Change**: Convert all public fields to auto-properties

### Action 3: Clean Up Code
**Files**: All .cs files
**Change**: Remove commented-out code, fix naming conventions

## Build Verification Commands

```bash
# Build all projects
dotnet build VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj
dotnet build VS/CSHARP/asm-sim-lib/asm-sim-lib.csproj
dotnet build VS/CSHARP/asm-dude2-ls-lib/asm-dude2-ls-lib.csproj

# Check for warnings
dotnet build /warn:4

# Analyze dependencies
dotnet list reference
```

## Success Criteria

- [ ] All projects build without errors
- [ ] No circular dependencies
- [ ] All public fields converted to properties
- [ ] No commented-out code
- [ ] Consistent naming conventions (PascalCase)
- [ ] Complete nullable annotations
- [ ] All tests pass
