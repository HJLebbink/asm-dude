# .NET 10 Modernization TODO List

## Status: CRITICAL BUILD BLOCKED

**Current Status**: ✅ Analysis complete | ❌ Build fails with 168 errors

**Urgent Action Required**: Fix critical build issues BEFORE modernization work can proceed

**Fixed Issue Documents**:
1. `NET10_CRITICAL_BUILD_FIX.md` - Detailed fix plan for 3 blocking issues
2. This TODO list updated with Phase 0 (Critical Build Fixes)

---

## Executive Summary

| Category | Count | Priority | Status |
|----------|-------|----------|--------|
| **Critical build issues** | 3 | 🔴 Blocking | Need immediate fix |
| **File-scoped namespaces needed** | 43+ files | ⚠️ High | Post-build |
| **Convert MSTest to xUnit** | 13 test files | ⚠️ High | Post-build |
| **Convert to record structs/classes** | 8+ types | ⚠️ Medium | Post-build |
| **Add nullable annotations** | 5+ files | ⚠️ Medium | Post-build |
| **Span<T> optimizations** | 3+ methods | ⚠️ Medium | Post-build |
| **Performance improvements** | 2+ areas | ⚠️ Medium | Post-build |
| **Cleanup commented code** | multiple | ⚠️ Low | Post-build |
| **Modernize tests** | 16 files | ⚠️ High | Post-build |

**Timeline**: 
- Blockers: **2-4 hours** to fix (NET10_CRITICAL_BUILD_FIX.md)
- Full modernization: **4-6 weeks** (once build works)

---

## Phase 0: CRITICAL BUILD FIXES (URGENT - Do First!)

**Status**: 🔴 Build blocked by 168 errors  
**Goal**: Get build to 0 errors so modernization can proceed  
**Time Estimate**: 2-4 hours

**Documentation**: See `NET10_CRITICAL_BUILD_FIX.md` for detailed fix plan

### 0.1 Issue 1: asm-tools-lib References Z3 Types 🔴 CRITICAL

**Problem**: `Tools.cs` and `ToolsZ3.cs` use Microsoft.Z3 types but asm-tools-lib shouldn't reference Z3

**Fix**:
1. Remove Z3 package reference from `asm-tools-lib.csproj` (if exists)
2. Move Z3-dependent code to `asm-sim-lib` (Z3 is simulation-only)
3. Remove `using Microsoft.Z3;` from asm-tools-lib files

**Estimated Time**: 30 minutes  
**Risk**: Low - Z3 is already correctly in asm-sim-lib

**Files**: See NET10_CRITICAL_BUILD_FIX.md Section 0.1

---

### 0.2 Issue 2: Microsoft.CodeAnalysis.Scripting Missing 🔴 CRITICAL

**Problem**: `ExpressionEvaluator.cs` uses `Microsoft.CodeAnalysis.Scripting.CSharp` but package not referenced

**Fix Options**:
- **Option A (Easy)**: Add package reference to `Microsoft.CodeAnalysis.Scripting.CSharp` v4.12.0
- **Option B (Surgical)**: Comment out `#if NET10_0` block (only `Evaluate_Constant` uses it)

**Estimated Time**: 15 minutes  
**Risk**: Very Low - can be easily reverted

**Files**: 
- `VS/CSHARP/asm-tools-lib/ExpressionEvaluator.cs` (lines 32-213)
- `VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj` (add package reference)

---

### 0.3 Issue 3: asm-sim-tests Assembly Ambiguity 🔴 CRITICAL

**Problem**: `Tools` class exists in both `AsmTools` and `AsmSim` namespaces causing ambiguity

**Fix Options**:
- **Option A (Quick)**: Fully qualify all references (`AsmTools.Tools` vs `AsmSim.Tools`)
- **Option B (Better)**: Rename `AsmSim.Tools` → `AsmSim.SimTools`

**Estimated Time**: 30 minutes  
**Risk**: Low - just rename/qualify

**Files**: 
- `Test_Mnemonic.cs`, `Test_MemZ3.cs` (106+ ambiguous references in asm-sim-tests)

---

### 0.4 Verify Build

After fixes 0.1, 0.2, 0.3:

```bash
dotnet build VS/AsmDude.sln
# Expected: 0 errors

dotnet test VS/AsmDude.sln
# May have 28 skipped tests (Z3 regression - known issue)
```

---

## Phase 1: Critical Build & Namespace Modernization (Week 1-2)

### 1.1 File-Scoped Namespaces (HIGH PRIORITY - ~30 min total)

**Issue**: All projects still use block-scoped `namespace X { ... }` instead of file-scoped `namespace X;`

**Files to convert**:
- ✅ asm-dude2-ls-tests (already done - 6 files)
- ⏳ asm-tools-lib (15 files)
- ⏳ asm-sim-lib (12 files)
- ⏳ asm-dude2-ls-lib (10 files)
- ⏳ asm-dude2-vsix (3 files)

**Tools**:
- Visual Studio: Right-click → Quick Actions → "Convert to file-scoped namespace"
- Or use IDE's automated refactoring

**Impact**: Cleaner, more modern C# 10+ syntax, ~30% reduction in line count

---

### 1.2 Consolidate GlobalUsings (asm-dude2-ls-tests)

**File**: `VS/CSHARP/asm-dude2-ls-tests/GlobalUsings.cs`

**Current**:
```csharp
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
```

**Optimize to**:
```csharp
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using FluentAssertions;
global using Xunit;
```

**Benefit**: Tests can omit `using FluentAssertions;` and `using Xunit;`

---

## Phase 2: Test Framework Modernization (Week 2-3)

### 2.1 Convert asm-tools-tests from MSTest to xUnit (HIGH PRIORITY)

**Current**: MSTest 4.1.0 with legacy `[TestClass]`/`[TestMethod]`

**Files**:
- `Test_AsmSourceTools.cs` - 27 duplicate test cases should use `[Theory]` + `[InlineData]`
- `Test_ToolsZ3.cs`
- `Test_Operand.cs` - hex/decimal tests could use `[Theory]`

**Suggested conversion**:
```csharp
// Before (MSTest)
[TestMethod]
public void Test_Evaluate_1()
{
    Assert.AreEqual(42, Evaluate("42"));
    Assert.AreEqual(0x2A, Evaluate("0x2A"));
    // ... 25 more similar tests
}

// After (xUnit + InlineData)
[Theory]
[InlineData("42", 42)]
[InlineData("0x2A", 42)]
[InlineData("-5", -5)]
public void Test_Evaluate(string input, int expected)
{
    Evaluate(input).Should().Be(expected);
}
```

**Impact**: 70% reduction in test method count, easier maintenance

---

### 2.2 Convert asm-sim-tests from MSTest to xUnit (MEDIUM PRIORITY)

**Current**: MSTest 3.9.0 with legacy attributes

**Files**:
- `Test_Mnemonic.cs`
- `Test_MemZ3.cs`
- `Test_ExecutionTree.cs` (2 tests marked `[Ignore]` - document Z3 regression)
- `Test_BitTricks.cs` (tests ignored)
- `Test_Runner.cs` (24 tests ignored - Z3 context lifecycle bug)
- `Test_Z3.cs` (has commented-out code to remove)

**Notes**:
- 28 tests skipped due to Z3 context lifecycle regression (documented in code)
- Keep `[Ignore]` with explanatory comments
- Remove commented-out code (see line 37-256 in Test_Z3.cs)

---

### 2.3 Update GlobalUsings in test projects (asm-tools-tests, asm-sim-tests)

**Status**: Both projects have **empty** `GlobalUsings.cs` files

**Action**: Delete them (not needed if tests use explicit using directives or file-scoped)

---

## Phase 3: Modern Types (Week 3-4)

### 3.1 Convert KeywordID to record struct (HIGH PRIORITY)

**File**: `VS/CSHARP/asm-tools-lib/KeywordID.cs`

**Current** (manual struct):
```csharp
public readonly struct KeywordID : IEquatable<KeywordID>
{
    public readonly string Value;
    
    public KeywordID(string value) => Value = value;
    
    public bool Equals(KeywordID other) => Value == other.Value;
    public override bool Equals(object obj) => obj is KeywordID other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public override string ToString() => Value ?? string.Empty;
    
    public static bool operator ==(KeywordID left, KeywordID right) => left.Equals(right);
    public static bool operator !=(KeywordID left, KeywordID right) => !left.Equals(right);
}
```

**Modern**:
```csharp
public record struct KeywordID(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}
```

**Benefit**: Eliminates 25 lines of boilerplate, value-based equality automatic

---

### 3.2 Convert PerformanceItem to record struct (MEDIUM PRIORITY)

**File**: `VS/CSHARP/asm-dude2-ls-lib/PerformanceStore.cs`

**Current**: Manual class with IEquatable

**Suggested**:
```csharp
internal record PerformanceItem(string Mnemonic, string_OPERAND, decimal Latency, throughput);
```

---

### 3.3 Convert Operand/Parameter to record class (MEDIUM PRIORITY)

**Files**: 
- `VS/CSHARP/asm-tools-lib/Operand.cs`
- `VS/CSHARP/asm-tools-lib/Parameter.cs`

**Pattern**: These are immutable data types - perfect for `record class`

---

### 3.4 Convert BranchInfo to record class (MEDIUM PRIORITY)

**File**: `VS/CSHARP/asm-sim-lib/BranchInfo.cs`

**Current**: Simple immutable class (lines 29-44)

**Suggested**: `record class BranchInfo(string Condition, int Target)`

---

## Phase 4: Nullable Reference Types (Week 4)

### 4.1 Add [NotNullWhen] attributes (MEDIUM PRIORITY)

**Files to analyze**:
- `State.cs` (lines 56-57, 115-116)
- `StateUpdate.cs` (lines 56-116)

**Pattern**:
```csharp
// Before
public string? WarningMessage { get; set; }

// After
public string? WarningMessage { get; set; }

public bool TryGetWarning([NotNullWhen(true)] out string? warning)
{
    warning = WarningMessage;
    return warning != null;
}
```

---

### 4.2 Verify nullable annotations across all projects (HIGH PRIORITY)

**Build with strict warnings**:
```bash
dotnet build /warn:4 /warnaserror
```

**Fix warnings**:
- `CS8600`, `CS8603`, `CS8604` (nullable reference types)
- `CA1062` (validate public arguments)
- `CA2263` (prefer generic Math methods)

---

## Phase 5: Span<T> & Performance (Week 4-5)

### 5.1 Optimize string operations with Span<T> (MEDIUM PRIORITY)

**Files with opportunities**:
- `AsmSourceTools.cs` - Lines 180-187 (string ranges already used ✅)
- `Tools.cs` - CreateKey() already uses `stackalloc char[8]` ✅
- `ToolsZ3.cs` - Parse operations could use `ReadOnlySpan<char>`
- `PdfParser.cs` - Multiple string.Replace/Trim operations

**Pattern**:
```csharp
// Before
string trimmed = s.Trim().Replace("-", "_");

// After
Span<char> span = stackalloc char[s.Length];
int len = s.TrimEnd().AsSpan().CopyTo(span);
span = span[..len];
for (int i = 0; i < span.Length; i++)
    if (span[i] == '-') span[i] = '_';
string result = new(string.Create(span.Length, span, (dest, state) => state.CopyTo(dest)));
```

**Note**: Only optimize hot paths - string operations on assembly files are typically small

---

### 5.2 Parallelize large data file loading (MEDIUM PRIORITY)

**Files**:
- `MnemonicStore.cs` (lines 226-450) - loads multiple TSV/JSON files
- `LabelGraph.cs` - processes large .asm files

**Pattern**:
```csharp
// Before
foreach (var file in dataFiles)
    LoadFile(file);

// After
Parallel.ForEach(dataFiles, LoadFile);
```

**Note**: Profile first - LSP communication overhead may dominate

---

## Phase 0: Critical Build Fixes (URGENT - Fix First!)

### 0.1 Fix Microsoft.CodeAnalysis.Scripting Reference (CRITICAL)

**File**: `VS/CSHARP/asm-tools-lib/ExpressionEvaluator.cs` (line 33)

**Error**: `The type or namespace name 'CodeAnalysis' does not exist in the namespace 'Microsoft'`

**Root cause**: asm-tools-lib references Microsoft.CodeAnalysis.Scripting.CSharp but it's missing from .csproj

**Fix**: Add package reference to `asm-tools-lib.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.Scripting.CSharp" Version="4.12.0" />
</ItemGroup>
```

**Or**: If scripting not needed for .NET 10, comment out the script evaluation code path

---

### 0.2 Fix Microsoft.Z3 Reference (CRITICAL)

**File**: `VS/CSHARP/asm-tools-lib/Tools.cs` (line 73), `ToolsZ3.cs` (all lines)

**Error**: `The type or namespace name 'Z3' does not exist in the namespace 'Microsoft'`

**Root cause**: asm-tools-lib uses Microsoft.Z3 but should NOT reference it (only asm-sim-lib needs Z3)

**Solution**: Move all Z3-dependent code from asm-tools-lib to asm-sim-lib

**Files to move/fix**:
- `Tools.cs`: Remove Z3-using methods (Lines 73-152)
- `ToolsZ3.cs`: Entire file should stay in asm-sim-lib, asm-tools-lib should not reference it

**Current dependency order**:
```
asm-tools-lib (net10.0) ——does not need Z——> Microsoft.Z3
asm-sim-lib (net10.0-windows) ——needs Z——> Microsoft.Z3, asm-tools-lib
```

**What broke**: The error `CS1721: 'Tools': static types cannot be used as parameters` (line 125 in Tools.cs) shows asm-tools-lib is trying to use asm-sim-lib types.

---

### 0.3 Fix asm-sim-tests assembly ambiguity (CRITICAL)

**File**: `VS/CSHARP/asm-sim-tests/Test_Mnemonic.cs` (line 48+) 

**Error**: `'Tools' is an ambiguous reference between 'AsmSim.Tools' and 'AsmTools.Tools'`

**Root cause**: Both asm-tools-lib and asm-sim-lib have a class named `Tools`

**Fix**: Fully qualify References:
- `AsmTools.Tools` → asm-tools-lib's Tools
- `AsmSim.Tools` → asm-sim-lib's Tools

Or rename one of the classes (recommended: `AsmSim.Tools` → `SimTools`)

---

### 0.4 Update TODO list with critical build issues

**Priority reorder**:
1. **CRITICAL**: Fix Microsoft.Z3 dependency ( asm-tools-lib shouldn't use it)
2. **CRITICAL**: Fix Microsoft.CodeAnalysis.Scripting missing
3. **CRITICAL**: Fix Tools class ambiguity in tests
4. **HIGH**: File-scoped namespaces
5. **HIGH**: Convert MSTest to xUnit
6. ... (rest of original priorities)

---

## Phase 6: Code Cleanup (Week 5-6)

### 6.1 Remove commented-out code (LOW PRIORITY)

**Found locations**:
- `VS/CSHARP/asm-annotate/TODO.md` - Multiple TODO comments
- `VS/CSHARP/asm-sim-lib/StateUpdate.cs` (line 1176)
- `VS/CSHARP/asm-dude2-ls-tests/Test_Z3.cs` (lines 37-256)

**Guideline**: Remove comments only if:
- Code is obsolete and no longer needed
- Reason for removal is documented in commit message

---

### 6.2 Review TODO comments in code (LOW PRIORITY)

**Command**:
```bash
grep -r "//\s*TODO" VS/CSHARP --include="*.cs" | wc -l
```

**Action**: For each TODO:
1. If critical → create GitHub issue + prioritize
2. If minor → fix immediately
3. If obsolete → remove

---

## Documentation Updates

### 7.1 Update existing NET10 docs (MEDIUM PRIORITY)

**Files to update**:
- `NET10_MODERNIZATION_CHECKLIST.md` - Add new items from this TODO list
- `NET10_MODERNIZATION_PLAN.md` - Reflect current status
- `NET10_MODERNIZATION_ANALYSIS.md` - Cross-reference with this document

---

### 7.2 Create modernization log (LOW PRIORITY)

**File**: `NET10_MODERNIZATION_LOG.md`

**Purpose**: Track completed modernizations with:
- Date
- File changed
- Brief description
- Git commit hash (when committed)

---

## Build Verification Steps

After each phase, run:

```bash
# Full solution build
dotnet build VS/AsmDude.sln --configuration Release

# Check warnings (treat as errors)
dotnet build /warn:4 /warnaserror

# Run all tests
dotnet test VS/AsmDude.sln --logger "console;verbosity=detailed"

# Specific test project
dotnet test VS/CSHARP/asm-tools-tests/asm-tools-tests.csproj
dotnet test VS/CSHARP/asm-sim-tests/asm-sim-tests.csproj
dotnet test VS/CSHARP/asm-dude2-ls-tests/asm-dude2-ls-tests.csproj
```

---

## Known Issues (No Action Needed Yet)

### 1. Z3 Context Lifecycle Bug (asm-sim-tests)

**Status**: 28 tests skipped with `[Ignore]` attribute

**Cause**: `StateUpdate` creates its own Z3 contexts; merging branches causes `Z3_translate` crash

**Files affected**:
- `Test_ExecutionTree.cs` (2 tests)
- `Test_BitTricks.cs` (1 test)
- `Test_Runner.cs` (24 tests)

**Solution**: Requires architectural changes to use single shared Z3 context

**Reference**: See `VS/CSHARP/asm-sim-lib/StateUpdate.cs` lines 139, 153

---

### 2. Circular Dependency (RESOLVED)

**Status**: Already fixed per NET10_MODERNIZATION_ANALYSIS.md

**Current state**: asm-tools-lib does NOT depend on asm-sim-lib ✅

---

## Quick Wins (Can Do in <1 Hour)

| Task | Time | Impact |
|------|------|--------|
| File-scoped namespace in asm-dude2-ls-lib | 10 min | Medium |
| Convert asm-dude2-ls-tests GlobalUsings | 5 min | Low |
| Remove commented code in Test_Z3.cs | 5 min | Low |
| Add nullable annotations to State.cs | 15 min | Medium |
| Create `record struct KeywordID` | 5 min | High |
| cleanup TODO in asm-annotate/TODO.md | 10 min | Low |

**Total**: ~50 minutes, High priority items only

---

## Success Criteria

| Criterion | Target |
|-----------|--------|
| All projects build with 0 errors | ✅ |
| All projects build with <10 warnings | ✅ |
| All tests pass (excluding intentionally ignored) | ✅ |
| No block-scoped namespaces remain | ✅ |
| MSTest converted to xUnit in all test projects | ✅ |
| Key types converted to records | ✅ |
| Nullable annotations complete | ✅ |
| No commented-out production code | ✅ |

---

## Next Steps

1. **Review this TODO list** with stakeholders
2. **Set priority order** for phases
3. **Create GitHub issues** for each phase
4. **Start with Quick Wins** (Phase 6.1)
5. **Iterate** through remaining phases
6. **Update documentation** after each phase

---

**Last updated**: 2026-03-12  
**Total files affected**: ~60+  
**Estimated completion**: 4-6 weeks  
**Risk level**: Low (incremental, testable changes)
