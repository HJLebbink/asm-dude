# Plan: Register & Label Rename for AsmDude2 LSP

## Goal
Add LSP rename capability (F2) for assembly language, supporting:
- Label renaming (current file only)
- Register renaming with full register family (AX/EAX/RAX, etc.)
- Warning for PUBLIC/EXTERN label renames
- No calling convention validation (user decides)

---

## Phase 1: Infrastructure

### 1.1 Create `CallingConvention.cs`
**Location**: `VS/CSHARP/asm-dude2-ls-lib/CallingConvention.cs`
**Purpose**: Define calling conventions (for documentation, not active validation)
**Content**:
```csharp
public enum CallingConvention
{
    X64_Windows,
    X64_Linux,
    X86_Windows,
    X86_Linux
}
```

### 1.2 Create `RegisterNames.cs`
**Location**: `VS/CSHARP/asm-dude2-ls-lib/RegisterNames.cs`
**Purpose**: Register classification and family mappings
**Content**:
- HashSet<string> for each width: R64, R32, R16, R8, Segment, X87, XMM, YMM, ZMM, Flags, IP
- HashSet<string> AllGPR for rename operations
- Dictionary<string, List<string>> RegisterAliases mapping all variants:
  - AX ↔ EAX ↔ RAX
  - BX ↔ EBX ↔ RBX
  - CX ↔ ECX ↔ RCX
  - DX ↔ EDX ↔ RDX
  - SI ↔ ESI ↔ RSI
  - DI ↔ EDI ↔ RDI
  - SP ↔ ESP ↔ RSP
  - BP ↔ EBP ↔ RBP
  - R8W ↔ R8D ↔ R8
  - ... (all 16 registers)

### 1.3 Create `RenameContext.cs`
**Location**: `VS/CSHARP/asm-dude2-ls-lib/RenameContext.cs`
**Purpose**: Track rename operation state
**Content**:
```csharp
public class RenameContext
{
    public string OldName { get; set; }
    public string NewName { get; set; }
    public Arch Architecture { get; set; }
    public CallingConvention CallingConvention { get; set; }
    public bool IsRegister { get; set; }
    public bool IsLabel { get; set; }
    public List<Location> Locations { get; set; }
    public bool HasFunctionCalls { get; set; }
    public bool IsExternal { get; set; }  // PUBLIC/EXTERN marker
}
```

---

## Phase 2: LSP Protocol Setup

### 2.1 Enable RenameProvider capability
**Location**: `VS/CSHARP/asm-dude2-ls-lib/LanguageServerTarget.cs`
**Change**: Uncomment lines 277-281 (currently commented)

```csharp
RenameProvider = new RenameOptions
{
    PrepareProvider = true
}
```

### 2.2 Add RenameProvider handlers to LanguageServer
**Location**: `VS/CSHARP/asm-dude2-ls-lib/LanguageServer.cs`

#### Method: `PrepareRename` (lines ~3200+)
- Find token at position
- Validate it's a label or register
- Return range for the token

#### Method: `GetRenameLocations` (lines ~3200+)
```csharp
public Task<WorkspaceEdit?> HandleRenameRequest(RenameParams parameter, CancellationToken token)
```
**Logic**:
1. Parse line at position, identify token
2. Check if external label (PUBLIC/EXTERN keyword)
3. If register: get full family via RegisterNames.RegisterAliases[oldName]
4. Collect ALL locations for ALL family members in current file
5. Build RenameContext and store in _renameContexts dict
6. Return Array<Location> for WorkspaceEdit

#### Method: `DoRename` (lines ~3200+)
```csharp
public Task<WorkspaceEdit?> HandleRenameApplyRequest(WorkspaceEdit parameter, CancellationToken token)
```
**Logic**:
1. Retrieve RenameContext from _renameContexts
2. Build array of TextEdit for all locations
3. Apply edits to parsedDocuments and textDocuments
4. Return WorkspaceEdit with all changes

### 2.3 Add rename context tracking
**Location**: `VS/CSHARP/asm-dude2-ls-lib/LanguageServer.cs`

Add field:
```csharp
private readonly Dictionary<string, RenameContext> _renameContexts = new();
```

---

## Phase 3: Implementation Details

### 3.1 Label Rename Logic

**GetRenameLocations logic**:
1. Check if token is a label reference (not definition)
2. If definition line (has colon, no comment before), skip (F12 handles this)
3. Find all references in labelGraphs[labelName]
4. Convert line numbers to Location objects with Range
5. Return locations array

**Edge cases**:
- Labels with same name in different segments (e.g., `@0` labels)
- Local labels (start with `@@`) - check if current file only

### 3.2 Register Rename Logic

**GetRenameLocations logic**:
1. Get register name from token
2. Look up family: `RegisterNames.RegisterAliases[oldName]`
3. For EACH register in family (e.g., EAX, AX, AL, AH, RAX):
   - Find all occurrences in all lines
   - Add to locations list
4. Check each line for CALL mnemonic:
   - If CALL found in current procedure, set HasFunctionCalls = true
5. If label is PUBLIC/EXTERN, set IsExternal = true
6. Store context in _renameContexts

### 3.3 External Label Warning

**Detection**:
- Check if line contains PUBLIC or EXTERN keyword before/with label
- Check if label is defined with `label_name EXTERN` syntax
- Set IsExternal flag in RenameContext

**Warning display**:
- Include in LSP response? No—LSP doesn't support warnings in rename
- Add comment to affected lines in workspace edit? Too invasive
- **Decision**: Log warning internally, show in VS Output window? User decides if this is needed.

---

## Phase 4: Testing

### 4.1 Unit Tests

**File**: `VS/CSHARP/asm-dude2-ls-tests/RegisterNamesTests.cs`
```csharp
[Test]
public void RegisterFamily_AX_ShouldIncludeAllVariants()

[Test]
public void RegisterFamily_EAX_ShouldIncludeAllVariants()

[Test]
public void RegisterAliases_Count_ShouldBeCorrect()
```

**File**: `VS/CSHARP/asm-dude2-ls-tests/RenameTests.cs`
```csharp
[Test]
public void RenameLabel_InCurrentFile_ShouldUpdateAllReferences()

[Test]
public void RenameRegister_EAXToEBX_ShouldUpdateFamily()

[Test]
public void RenameExternalLabel_ShouldSetIsExternalFlag()
```

### 4.2 Integration Tests

**File**: `VS/CSHARP/asm-dude2-ls-tests/LspRenameIntegrationTests.cs`
```csharp
[Test]
public void RenameRequest_Cycle_ShouldReturnWorkspaceEdit()
```

---

## Timeline Summary

| Phase | Tasks | Estimate |
|-------|-------|----------|
| Phase 1 | Infrastructure | 1 hour |
| Phase 2 | LSP Protocol | 1.5 hours |
| Phase 3 | Implementation | 1.5 hours |
| Phase 4 | Testing | 1.5 hours |
| **Total** | | **5.5 hours** |

---

## Open Questions (Answered)

- **Register family**: Yes, rename includes all variants (AX/EAX/RAX, etc.)
- **Calling convention**: No validation, no warnings (user decides)
- **Scope**: Current file only
- **External labels**: Warning flag added, display method TBD

---

## Next Steps

1. Create infrastructure files (CallingConvention, RegisterNames, RenameContext)
2. Enable RenameProvider in LanguageServerTarget
3. Implement GetRenameLocations and DoRename in LanguageServer
4. Add tests
5. Document in CLAUDE.md
