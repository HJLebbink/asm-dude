# Critical Build Fix Plan

**Status**: Build currently blocked by 168 errors  
**Timeline**: 2-4 hours to fix  
**Risk**: Low (surgical fixes)

---

## Issue 1: asm-tools-lib References Z3 (Critical - Blocker)

### Problem
```
error CS0234: The type or namespace name 'Z3' does not exist in the namespace 'Microsoft'
error CS0246: The type or namespace name 'Context' could not be found
error CS0246: The type or namespace name 'BitVecExpr' could not be found
... 100+ more Z3-related errors
```

### Root Cause
- asm-tools-lib (`net10.0`) references `Microsoft.Z3` but this should only be in asm-sim-lib (`net10.0-windows`)
- Multiple methods in asm-tools-lib's `Tools.cs` and `ToolsZ3.cs` use Z3 types

### Solution
1. **Remove Z3 package reference from asm-tools-lib** (if it exists)
2. **Move Z3-dependent code to asm-sim-lib**:
   - `Tools.cs` lines 73-152 (Z3-related methods)
   - `ToolsZ3.cs` entire file should be in asm-sim-lib
3. **Update using directives**:
   - Remove `using Microsoft.Z3;` from asm-tools-lib files
   - Keep `using Microsoft.Z3;` only in asm-sim-lib files

### Files to Check
- `VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj` - Remove Z3 package reference
- `VS/CSHARP/asm-tools-lib/Tools.cs` - Remove Z3 methods, add stub comments pointing to asm-sim-lib
- `VS/CSHARP/asm-tools-lib/ToolsZ3.cs` - Delete or move to asm-sim-lib
- `VS/CSHARP/asm-sim-lib/Tools.cs` - Move Z3 methods here
- `VS/CSHARP/asm-sim-lib/ToolsZ3.cs` - Keep here (already correct)

---

## Issue 2: Microsoft.CodeAnalysis.Scripting Missing (Critical - Blocker)

### Problem
```
error CS0234: The type or namespace name 'CodeAnalysis' does not exist in the namespace 'Microsoft'
error CS0234: The type or namespace name 'Scripting' does not exist in the namespace 'Microsoft.CodeAnalysis'
error CS0234: The type or namespace name 'CSharp' does not exist in the namespace 'Microsoft.CodeAnalysis'
error CS0234: The type or namespace name 'Script' does not exist in the namespace 'Microsoft.CodeAnalysis.CSharp'
error CS0234: The type or namespace name 'CSharpScript' does not exist in the current context
```

### Root Cause
- `ExpressionEvaluator.cs` (line 33) uses `using Microsoft.CodeAnalysis.Scripting.CSharp;`
- Package `Microsoft.CodeAnalysis.Scripting.CSharp` is not referenced in `asm-tools-lib.csproj`

### Solution A (Recommended): Add Missing Package
**File**: `VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj`
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.Scripting.CSharp" Version="4.12.0" />
</ItemGroup>
```

### Solution B (Alternative): Remove Script Support
If scripting evaluation isn't needed, comment out the script-based evaluation path in `ExpressionEvaluator.cs`

**Check if needed**: Search for `CSharpScript` usage
```bash
grep -n "CSharpScript" VS/CSHARP/asm-tools-lib/ExpressionEvaluator.cs
```

If only used in one method and tests pass without it, consider removing that code path.

---

## Issue 3: asm-sim-tests Assembly Ambiguity (Critical - Blocker)

### Problem
```
error CS0104: 'Tools' is an ambiguous reference between 'AsmSim.Tools' and 'AsmTools.Tools'
```

### Root Cause
Both assemblies have a class named `Tools`:
- `AsmTools.Tools` in asm-tools-lib
- `AsmSim.Tools` in asm-sim-lib

### Solution: Fully Qualify References

**Option 1 (Quick Fix)**: Update test files to use fully qualified names
```csharp
// Before
var result = Tools.CreateKey(...);

// After
var result = AsmTools.Tools.CreateKey(...);
// OR
var result = AsmSim.Tools.CreateKey(...);
```

**Option 2 (Better)**: Rename one of the classes
- Rename `AsmSim.Tools` → `AsmSim.SimTools`
- Update all references in asm-sim-lib and tests

**Files affected**:
- `Test_Mnemonic.cs` (lines 48, 76, etc.)
- `Test_MemZ3.cs` (all Tools references)
- All other test files in asm-sim-tests

**Action**:
1. Run to count occurrences: `grep -rn "'Tools' is an ambiguous" VS/CSHARP/asm-sim-tests/`
2. Decide on fix approach (qualified names vs rename)
3. Apply fix to all affected files

---

## Build Verification Steps

After fixes, run:
```bash
# Restore packages
dotnet restore VS/AsmDude.sln

# Build
dotnet build VS/AsmDude.sln --configuration Debug

# Expected: 0 errors, possibly some warnings

# Run tests
dotnet test VS/AsmDude.sln --configuration Debug
```

---

## Immediate Action Items

1. **[30 min]** Fix Issue 1: Remove Z3 from asm-tools-lib
2. **[15 min]** Fix Issue 2: Add Microsoft.CodeAnalysis.Scripting package OR remove script code
3. **[30 min]** Fix Issue 3: Resolve Tools ambiguity in tests
4. **[15 min]** Build and verify 0 errors
5. **[1 hour]** Run tests and fix any remaining issues

**Total**: 2-4 hours

---

## Root Cause Analysis

### Why Did This Happen?

The migration from .NET Framework 4.8 to .NET 10 exposed pre-existing dependency issues:

1. ** asm-tools-lib should be "Z3-agnostic"**
   - It's a core library used by both simulator and LSP server
   - Z3 is only needed for simulation (asm-sim-lib)
   - The Z3 references in asm-tools-lib were added as temporary hacks

2. **Package reference drift**
   - Some Z3 types may have been used in asm-tools-lib for convenience
   - This created a circular dependency pattern: `asm-tools-lib → Microsoft.Z3 → asm-sim-lib`

3. **Class name collision**
   - Both assemblies using `Tools` as class name is convenient but creates ambiguity
   - Better naming: `AsmTools` vs `AsmSim.Tools` or `SimTools`

---

## Prevention

After fixing, add these checks to CI/CD:

1. **Dependency graph check**:
   ```bash
   dotnet graph list --dependees VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj
   ```

2. **Namespace collision check**:
   - Use static analysis to detect ambiguous type references
   - Add to build: `<EnablePackageAnalysis>true</EnablePackageAnalysis>`

3. **Test build in isolation**:
   ```bash
   dotnet build VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj --no-restore
   dotnet build VS/CSHARP/asm-sim-lib/asm-sim-lib.csproj --no-restore
   ```

---

## Next Steps

1. Execute the 3 fixes above
2. Verify build succeeds
3. Return to NET10_MODERNIZATION_TODO.md for remaining modernization tasks
