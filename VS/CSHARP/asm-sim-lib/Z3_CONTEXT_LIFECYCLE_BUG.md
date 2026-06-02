# Z3 Context Lifecycle Bug in AsmSim

## Executive Summary

**Location:** `DynamicFlow.cs:800-822`  
**Severity:** Critical (Access Violation 0xC0000005)  
**Type:** Native Z3 context lifecycle violation, not a C# nullable reference issue

---

## The Bug

When merging states in `DynamicFlow.Merge_State_Update_LOCAL`, the code retrieves a `BranchInfo` object from the graph structure that was created in a `StateUpdate` context. That context has already been disposed, but the `BranchInfo.BranchCondition` (a Z3 `BoolExpr`) still references the native Z3 memory from the now-dead context.

### Code Path

```csharp
// DynamicFlow.cs:800-822
BoolExpr? bc = null;
{
    using (Context ctx = new(this.tools_.ContextSettings))
    {
        string branchKey = GraphTools.Get_Branch_Point(source1, source2, this.graph_);
        BranchInfo branchInfo = Get_Branch_Condition_LOCAL(branchKey);  // Line 805
        
        if (branchInfo == null)
        {
            bc = ctx.MkBoolConst("BC" + target);  // Safe: created in current context
        }
        else
        {
            bc = branchInfo.BranchCondition;  // Line 813: CRASH HERE
            // branchInfo came from StateUpdate stored in graph edge
            // That StateUpdate owned a Context that was disposed
            sharedBranchConditions.Add(bc.ToString());
        }
    }
    // ctx disposed
    
    using StateUpdate stateUpdate = new(bc, nextKey2, nextKey1, nextKey3, this.tools_);  // Line 819
    // StateUpdate constructor calls Translate() on bc
}
```

### Why It Crashes

1. **Line 509 in `Update_Backward`** (or similar): A `StateUpdate` is created with its own `Context`, then store in the graph and disposed:
   ```csharp
   // StateUpdate has field:
   private readonly Context ctx_;
   private BoolExpr? branch_Condition_;  // Created in ctx_
   
   // Then disposed:
   branch?.Dispose();  // Disposes ctx_ -- native Z3 memory freed!
   ```

2. **Line 805:** `Get_Branch_Condition_LOCAL` retrieves the `BranchInfo` from the graph edge:
   ```csharp
   BranchInfo? Get_Branch_Condition_LOCAL(string branchKey)
   {
       // ...retrieve from graph...
       return edge1.Tag.stateUpdate.BranchInfo;  // BranchInfo created in disposed context!
   }
   ```

3. **Line 813:** `branchInfo.BranchCondition` is assigned to `bc` without translating to the new `using (Context ctx = ...)`. The `BoolExpr` still holds a handle to native Z3 memory from the old, disposed context.

4. **Line 819:** `new StateUpdate(bc, ...)` constructor calls `branchCondition.Translate(this.ctx_)` inside (line 150 of StateUpdate.cs), which calls native `Z3_translate` on a freed context handle → **Access Violation (0xC0000005)**.n

---

## Root Cause: Two-Phased Disposal

1. **Phase 1:** `StateUpdate` constructor creates `Context ctx_` and creates `BoolExpr` inside it
2. **Phase 2:** `StateUpdate.Dispose()` is called, disposing `ctx_` and freeing native memory
3. **Phase 3:** Graph edges still contain references to `BranchInfo` containing `BoolExpr` with dangling native pointers
4. **Phase 4:** Dereferencing the dangling pointer causes the crash

---

## Why No Compile-Time Warning?

- `BranchInfo` marks `BranchCondition` as non-nullable: `public readonly BoolExpr BranchCondition`
- The crash is **not about nullability** - it's about **Z3 native resource lifecycles**
- C# nullable reference types track C# reference nullability, not native handle validity

---

## Solution Options

### Option A: Translation-Immutation Pattern (Recommended)

Store only the **string representation** of branch conditions in the graph, not the `BoolExpr` itself, then reconstruct them:

```csharp
// Change from storing BranchInfo to storing string key + branchTaken flag
public struct BranchInfoKey
{
    public readonly string ConditionString;  // String representation
    public readonly bool BranchTaken;
}

// In Merge_State_Update_LOCAL:
string branchKey = GraphTools.Get_Branch_Point(...);
BranchInfo stored = Get_Branch_Info_As_String(...);  // Reconstruct from string

// OR reconstruct in current context:
using (Context ctx = new(...))
{
    string conditionStr = Get_Branch_Condition_String(branchKey);
    // Parse or use fresh constant instead of sharing dead expression
}
```

### Option B: Persistent "Global" Context

Create a single `Context` that lives as long as the `DynamicFlow` object:

```csharp
public class DynamicFlow : IDisposable
{
    private readonly Context persistentCtx_;  // Lives as long as this
    
    public DynamicFlow(Tools tools)
    {
        this.persistentCtx_ = new Context(tools.ContextSettings);
        // ...
    }
    
    // When creating StateUpates for the graph, ALWAYS translate to persistentCtx_:
    edgeInfo = (taken: true, update: createUpdate);
    createUpdate.BranchInfo = new BranchInfo(branchCondition.Translate(persistentCtx_) as BoolExpr!, ...);
    //...
}
```

### Option C: BranchInfo Translation at Retrieval

When retrieving from the graph, immediately translate to the query context:

```csharp
// Inside the using (Context ctx) block:
BranchInfo branchInfo = Get_Branch_Condition_LOCAL(branchKey);
if (branchInfo != null)
{
    // Translate to current context immediately!
    bc = branchInfo.BranchCondition.Translate(ctx) as BoolExpr!;  // Safety critical
}
```

**Current Code at line 813:**
```csharp
bc = branchInfo.BranchCondition;  // Just passes through! Bad!
```

**Fixed:**
```csharp
bc = branchInfo.BranchCondition.Translate(ctx) as BoolExpr!;  
if (bc == null) throw new ...;  // Should have a safety check here
```

---

## Line Summary

| File | Lines | Issue |
|------|-------|--------|
| `DynamicFlow.cs` | 509, 511 | `StateUpdate` disposed, leaves `BranchInfo` in graph with dead context |
| `DynamicFlow.cs` | 802 | `using (Context ctx = ...)` scope is too short |
| `DynamicFlow.cs` | 805 | Retrieves `BranchInfo` from graph without translating to current context |
| `DynamicFlow.cs` | 813 | `bc = branchInfo.BranchCondition` uses dangling native ptr without translation |
| `DynamicFlow.cs` | 819 | Crash: `new StateUpdate(bc, ...)` calls `Translate()` on dead context |

---

## Verification

Run the skipped tests in `asm-sim-tests/Test_ExecutionTree.cs` or `Test_Runner.cs` (DynamicFlow tests) to reproduce:

```bash
# Expected to see 149 passed, 28 skipped, 0 failed
# The 28 skipped are precisely the DynamicFlow-related tests hitting this bug
rtk dotnet test VS/CSHARP/asm-sim-tests/asm-sim-tests.csproj
```

**Specifically:** Tests in `test_DynamicFlow` class and `test_BitTricks_Legatos_Multiplier` hit this crash path.

---

## Fixed Code

No file changes made to `DynamicFlow.cs` (still needs to be refactored - this is architecture, not nullable).

**Files Changed:**
1. `GraphTools.cs:157`: `string` → `string?` (nullability contract fix)
2. `BranchInfoStore.cs:211`: Added throw on null translation instead of silent failure

**Still TODO:** Fix `DynamicFlow.cs:813` to translate the condition to the local `using` context before assignment.

---

**Reference:** CLAUDE.md Known Issues "Z3 Context Lifecycle Bug in Dynamic Flow (Regression)"
