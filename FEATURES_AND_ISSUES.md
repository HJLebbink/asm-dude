# AsmDude2 — Features & Known Issues

**Purpose:** Track incomplete features, bugs, and technical debt for prioritization.

**Last Updated:** 2026-03-13

---

## Status Legend

- 🔴 **Critical** — Blocks functionality or causes crashes
- 🟠 **High** — Important feature or regression
- 🟡 **Medium** — Nice-to-have or moderate issue
- 🟢 **Low** — Polish, future optimization, or minor edge case
- ✅ **Resolved** — Fixed or completed
- ⏳ **In Progress** — Currently being worked on
- ❌ **Known Limitation** — Architectural constraint, may not be solvable

---

## Critical Issues 🔴

### 1. Z3 Context Lifecycle Bug in DynamicFlow (Regression)
- **Status**: ❌ **Known Limitation** (28 tests skipped)
- **Severity**: 🔴 Critical
- **Affected**: asm-sim-lib, assembly instruction simulator
- **Issue**: StateUpdate objects create their own Z3 contexts. During DynamicFlow state merging, BranchInfo.Translate attempts to translate Z3 expressions between disposed contexts → native crash (0xC0000005)
- **Impact**: 28 simulator tests marked `[Ignore]`, functionality degraded
- **Tests Affected**:
  - `Test_DynamicFlow` (2 tests)
  - `Test_BitTricks_LegatosMultiplier` (1 test)
  - `Test_Runner.cs` all except `Test_Runner_Several_Mnemonics` (24 tests)
- **Code Locations**:
  - `StateUpdate.cs:139, 153` — context creation
  - `BranchInfoStore.cs:211` — translation call
  - `BranchInfo.cs:45` — crash site
  - `DynamicFlow.cs:800` — premature context disposal
- **Solutions Considered**:
  1. Use single shared Z3 context across all operations (architectural refactor)
  2. Translate expressions immediately when stored, not on retrieval (performance vs. correctness trade-off)
- **Effort**: ⏳ Medium-High
- **Priority**: HIGH — blocks simulator verification features

---

## High-Priority Features 🟠

### 2. Re-enable Undefined Label Diagnostics
- **Status**: 🔴 **Broken** (disabled + diagnostic flow bug)
- **Severity**: 🟠 High
- **Location**:
  - `LanguageServer.cs:459` — feature hardcoded to `if (false)` since Sept 30, 2023
  - `LanguageServer.cs:449-467` — `UpdateLabelGraph()` method
  - `LanguageServer.cs:416-428` — diagnostic flow in `UpdateInternals()`
- **Current Issues**:
  1. Feature hardcoded to `if (false)` — disabled intentionally
  2. **CRITICAL**: Even if enabled, squiggles won't appear because `UpdateLabelGraph()` is **never called** from the main document update flow (line 428: `SendDiagnostics()` called with empty `this.diagnostics`)
- **What's Implemented** ✅:
  - Squiggles for undefined labels (red underlines)
  - Squiggles for duplicate/clashing labels (red underlines)
  - Both types added to error list
  - All options exist in code (but UI not built)
- **Root Cause of No Squiggles**:
  - Line 416: `this.diagnostics.Clear()` — empties list
  - Line 418: `this.labelGraphDirty.Add(uri)` — marks as dirty
  - Line 459: `if (false)` — `UpdateLabelGraph()` never called, diagnostics never populated
  - Line 428: `SendDiagnostics()` — sends empty list to client
  - `GetLabelGraph()` only called from hover/codelens, not main update flow
- **The Fix**:
  - Change `if (false)` to `if (this.options.IntelliSense_Label_Analysis_On)`
  - OR: Call `this.GetLabelGraph(uri)` before `SendDiagnostics()` in `UpdateInternals()` (line 418)
  - This ensures label diagnostics are added to `this.diagnostics` before publishing
- **Also Blocked By**: Unknown false positives (reason for original disable Sept 30, 2023)
- **Effort**: Very Low (1-line fix, but need to test for false positives)
- **Priority**: HIGH — quick win once false positives are understood
- **Dependencies**: Investigation of false positives; User Options Pages (to control via UI)

---

### 2b. AsmSim Register Value Integration ✅

- **Status**: ✅ **FIXED** — timing bug resolved, simulator cache now properly populated
- **Severity**: 🟢 Low (working feature, enhancement needed)
- **Location**:
  - Hover implementation: `LanguageServer.cs:2166-2207` (GetHover for registers)
  - Code completion (disabled): `LanguageServer.cs:1316-1359` (Mnemonic_Operand_Completions)
  - Simulator integration: `LanguageServer.cs:85-144`, `LspAsmSimulator.cs` (entire file)
  - **CRITICAL BUG**: `LanguageServer.cs:416-428` (UpdateInternals method)
  - Options: `AsmLanguageServerOptions.cs:210-292`

- **What Was Supposed to Work** ✅:
  - **Hover over register** shows simulated values:
    - Line 2190-2191: Code calls `GetRegisterValueBeforeLine()` and `GetRegisterValueAfterLine()`
    - Should display: "Before: 0x1234 = ..., After: 0x5678 = ..."
    - Enabled by default, no option to disable
  - **Simulator engine**:
    - `LspAsmSimulator` class fully implemented and integrated
    - `InvalidateAndSimulate()` called on document change (line 419)
    - Pre-computes register states and caches them (O(1) lookup)
    - Handles: syntax errors, usage-of-undefined, unreachable instructions, not-implemented
    - Uses `SimpleStep_Forward` only (avoids Z3 context bug)

- **The Timing Bug (NOW FIXED)** ✅:
  **Previous problem** (before fix):
  ```csharp
  // LanguageServer.cs:416-428 (UpdateInternals method)
  this.diagnostics.Clear();                                    // Line 416
  this.UpdateFoldingRanges(uri);
  this.labelGraphDirty.Add(uri);
  this.asmSimulator_.InvalidateAndSimulate(new Uri(uri), newLines,
      onCompleted: completedUri => this.SendDiagnostics(completedUri.ToString()));  // Callback

  this.SendDiagnostics(uri);  // Line 428 — CALLED IMMEDIATELY, NOT WAITING FOR SIMULATOR ❌
  ```

  - `InvalidateAndSimulate()` spawns background thread (Task.Run)
  - **Line 428 called `SendDiagnostics()` BEFORE background thread populated cache**
  - Hover queries returned null, no before/after values shown
  - `onCompleted` callback sent diagnostics after, but user already saw empty state

  **The Fix Applied** ✅:
  ```csharp
  this.asmSimulator_.InvalidateAndSimulate(new Uri(uri), newLines,
      onCompleted: completedUri => this.SendDiagnostics(completedUri.ToString()));

  // REMOVED: this.SendDiagnostics(uri);
  ```

  Now diagnostics and register states are sent **only after** simulation completes via callback.

  **Result**:
  - ✅ Cache properly populated before queries
  - ✅ Hover shows before/after values
  - ✅ Inlay hints show proven states
  - ✅ `asm/getProvenStates` returns valid data

- **What Works Now** ✅:
  - **Hover over register**: Shows before/after values (e.g., "Before: 0x1234 → After: 0x1334")
  - **Inlay hints**: Shows proven states at end of each line when `AsmSim_On` is true
  - **Simulator cache**: Properly populated with timing bug fix
  - **LspAsmSimulator**: All methods working correctly
  - **asm/getProvenStates**: Custom LSP method returns valid proven states

- **Code Completion — Still Disabled** ⏳:
  - Lines 1316-1335 completely commented out
  - Old code references obsolete methods:
    - `Settings.Default.AsmSim_Show_Register_In_Code_Completion` — no longer exists
    - `this.asmSimulator_.Tools.StateConfig.IsRegOn()` — method doesn't exist
    - `this.asmSimulator_.Get_Register_Value()` — method doesn't exist
  - Needs: Port to new options-based approach

- **Available Options** (exist in code but no UI):
  - `AsmSim_On` — master switch for simulator
  - `AsmSim_Show_Register_In_Code_Completion` — enable/disable in completion
  - `AsmSim_Show_Register_In_Instruction_Tooltip` — flag for mnemonic hover
  - `AsmSim_Show_Register_In_Register_Tooltip` — flag for register hover
  - `AsmSim_Show_Register_In_Code_Completion_Numeration` — hex/decimal/binary format
  - `AsmSim_Show_Register_In_Instruction_Tooltip_Numeration` — hex/decimal/binary format
  - (Plus 8+ other simulator options for diagnostics, timeout, threading, etc.)

- **To Complete (Code Completion Only)**:
  1. Uncomment lines 1316-1335 in LanguageServer.cs
  2. Replace old `Settings.Default` references with new `options` approach
  3. Wire in `this.options.AsmSim_Show_Register_In_Code_Completion` to enable/disable
  4. Call `GetRegisterValueBeforeLine()` and `GetRegisterValueAfterLine()` to populate completion

- **Effort**:
  - ✅ Timing fix: **DONE** (1 line removed)
  - Medium (code completion refactoring)
- **Priority**: MEDIUM — code completion is enhancement, hover already provides same info
- **Dependencies**: User Options Pages (for UI control)

---

### 2c. Expose AsmSim Proven States to Tools & AIs ✅

- **Status**: ✅ **IMPLEMENTED** — `asm/getProvenStates` LSP method + inlay hints
- **Severity**: 🟠 High (enables new capabilities for analysis tools)
- **Location**:
  - Z3 proven states computed: `LspAsmSimulator.cs` (cache, working)
  - Custom LSP method: `LanguageServerTarget.cs:358`, `LanguageServer.cs:1897`
  - Types: `ProvenStatesTypes.cs` (GetProvenStatesParams, ProvenLineState, ProvenStatesResponse)
  - Inlay hints: `LanguageServer.cs:GetInlayHints()` (enhanced to show Z3 states)

- **What Was Implemented** ✅:

  **Phase 1: `asm/getProvenStates` Custom LSP Method** (COMPLETE)
  - New custom LSP method for tools/AIs to query proven states programmatically
  - **Request**: `asm/getProvenStates` with URI and optional `lineRange` [startLine, endLine]
  - **Response**: `ProvenStatesResponse` with array of `ProvenLineState` objects
  - **Each state includes**:
    - `Line` (0-indexed)
    - `BeforeState` (register/flag values before instruction)
    - `AfterState` (register/flag values after instruction)
    - `ProvenBy` ("Z3 SimpleStep")
    - `Confidence` ("complete" if no timeout)
  - **Use cases**: External tools, AI context injection, proof-driven code suggestions
  - **Implementation**: ~70 lines of code
    - `ProvenStatesTypes.cs`: 3 classes (request, response, line state)
    - `LanguageServerTarget.cs`: Handler delegates to server
    - `LanguageServer.cs`: Queries simulator cache, returns serialized response
  - **Files created/modified**:
    - ✅ Created: `VS/CSHARP/asm-dude2-ls-lib/ProvenStatesTypes.cs`
    - ✅ Modified: `LanguageServerTarget.cs` (added handler)
    - ✅ Modified: `LanguageServer.cs` (added GetProvenStates method)

  **Phase 2: Inlay Hints Showing Z3 States** (COMPLETE)
  - Enhanced existing `textDocument/inlayHint` handler to show before/after register states
  - **Shows inline** (in editor, at end of line):
    - "Before: RAX=0x1234 | RBX=0 | ZF=0 → After: RAX=0x1334 | RBX=0 | ZF=1"
  - **Controlled by**: `options.AsmSim_On` (when simulator is enabled)
  - **Uses InlayHintKind.Parameter** for visual distinction
  - **Tooltip**: "Z3-proven register and flag states (SimpleStep forward analysis)"
  - **Implementation**: ~30 lines added to GetInlayHints()
  - **Files modified**:
    - ✅ Modified: `LanguageServer.cs` (enhanced GetInlayHints with AsmSim states)

- **How It Works**:
  1. **Simulator runs in background** on every document change (already implemented)
  2. **Cache populated** with register states before/after each instruction
  3. **asm/getProvenStates called** by tools/AIs: returns machine-readable JSON
  4. **Inlay hints displayed** in editor when AsmSim_On is true
  5. **No user action needed** — happens automatically

- **Example Usage (AI/Tool)**:
  ```python
  import requests
  import json

  request = {
      "jsonrpc": "2.0",
      "id": 1,
      "method": "asm/getProvenStates",
      "params": {
          "uri": "file:///code/test.asm",
          "lineRange": [0, 20]
      }
  }

  response = requests.post("http://localhost:7000", json=request)
  states = response.json()["result"]["states"]

  for state in states:
      print(f"Line {state['line']}: {state['beforeState']} → {state['afterState']}")
  ```

- **What's NOT Yet Implemented** (Future Options):
  - Option 3: Code Actions with proof context (suggest optimizations)
  - Option 4: Extended code lens with proof data
  - Option 5: Semantic token modifier for proven values

- **Build Status**: ✅ **Compiles successfully**
  - No errors, only pre-existing warnings
  - Full solution builds: 1511 warnings, 0 errors

- **Dependencies Resolved**:
  - ✅ AsmSim timing bug (2b) **FIXED** — cache now properly populated
  - ✅ User Options Pages still needed for full UI control
  - ✅ GetProvenStates works immediately with real data
  - ✅ Inlay hints controlled by `AsmSim_On` option
  - ✅ Hover shows before/after register values

---

### Brainstorm: Undefined Label Diagnostics Re-enablement

**Before fixing, we need to understand:**

#### A. Root Causes of False Positives (Sept 30, 2023)

Why was this disabled? Possible causes:

1. **Include File Labels**
   - Labels defined in `.inc` files referenced via `%include "file.inc"`
   - Current code has `undefined_includes_` list (line 54) but unclear if resolved
   - If include files aren't parsed, all their labels appear undefined
   - Question: Does LabelGraph handle multi-file label resolution?

2. **Macro-Generated Labels**
   - Labels created/modified by macros at runtime
   - Example: `%define LABEL label_%d` where `%d` varies
   - Static analysis can't predict these names
   - Should we disable checking for macro-generated code?

3. **Scoped/Qualified Labels**
   - Assembly syntax: `module.label`, `namespace::label`, etc.
   - Current code at line 220: `Tools.Retrieve_Regular_Label()` handles assembler-specific scoping
   - May not work correctly for all assemblers (MASM vs NASM vs AT&T)

4. **Forward References**
   - Label used before definition
   - Some assembly styles allow this (e.g., forward jumps)
   - Should this be flagged as error or warning?

5. **External/Linker Symbols**
   - Labels referenced from linked objects (`.obj` files)
   - Static analysis can't know these exist
   - Need option to disable checking for these

6. **Case Sensitivity Issues**
   - MASM: case-insensitive labels
   - NASM: case-sensitive labels
   - Code assumes case-sensitive (line 457: `caseSensitiveLabels = true`)
   - Might trigger false positives in MASM code

7. **Assembler-Specific Syntax**
   - AT&T syntax: `label:` vs Intel `label:`
   - Different label definition markers
   - Current parsing may miss labels in certain syntaxes

#### B. Testing Strategy Needed

Before re-enabling, we should:

1. **Create Test Cases**
   - Simple: undefined label in same file
   - Include: label in `.inc` file, used in main
   - Macro: macro-generated labels
   - Forward: label used before definition
   - External: reference to external symbol
   - Mixed: multi-assembler syntax in same file

2. **Validation Approach**
   - Get real assembly code samples from users
   - Enable diagnostics on known good code, count false positives
   - Compare against other tools (VS Disassembler, IDA, etc.)
   - Document expected vs actual behavior

3. **Fallback Strategy**
   - Option to disable globally (`IntelliSense_Label_Analysis_On = false`)
   - Per-line disable comment: `; asm-ignore-undefined-label`
   - Suppress for specific patterns (e.g., `jmp .Lxxxx` in AT&T syntax)

#### C. Options Needed (Currently Missing UI)

These already exist in code but need Options UI:

1. **Master Switch**: `IntelliSense_Label_Analysis_On`
   - On/Off entire feature

2. **Show/Hide Undefined Labels**: `IntelliSense_Show_Undefined_Labels`
   - Separate toggle for undefined vs clashing

3. **Show/Hide Clashing Labels**: `IntelliSense_Show_Clashing_Labels`

4. **Decorate (Add Squiggle)**: `IntelliSense_Decorate_Undefined_Labels`
   - Show in error list but no underline?

5. **Decorate (Add Squiggle)**: `IntelliSense_Decorate_Clashing_Labels`

6. **New Options to Consider Adding**:
   - `IntelliSense_Allow_Forward_References` — don't warn on labels used before definition
   - `IntelliSense_Allow_External_Labels` — don't warn on labels that might be external symbols
   - `IntelliSense_Ignore_Macro_Labels` — skip labels that look macro-generated
   - `IntelliSense_Label_Include_Files` — enable/disable checking in `.inc` files
   - `IntelliSense_Label_Severity` — Error vs Warning vs Info

#### D. Implementation Considerations

**Option 1: Full Re-enable**
- Pro: Simple, all labels checked
- Con: Likely false positives, need extensive testing
- Risk: Users disable feature because of noise

**Option 2: Incremental Re-enable**
- Start with undefined labels only (most common case)
- Leave clashing labels off (higher false positive rate)
- Pro: Easier to debug, less user noise
- Con: Incomplete feature

**Option 3: Smart Re-enable with Filters**
- Enable by default but filter out:
  - Labels containing `_` (likely macro-generated)
  - Labels starting with `.` (AT&T local labels)
  - Labels in `.inc` files (included files)
- Pro: Reduces false positives automatically
- Con: May hide real errors

**Option 4: Warning System**
- Report potential issues as "Information" not "Error"
- User can promote to error if they want strict checking
- Pro: Doesn't break builds, less aggressive
- Con: Users might ignore warnings

#### E. Documentation Needed

If we re-enable, we need to document:

1. **Known Limitations**
   - Which assemblers fully supported (MASM, NASM, AT&T)
   - Which label patterns are supported
   - Which are false-positive sources

2. **User Guide**
   - How to disable for specific lines/files
   - When to expect false positives
   - How to report false positives

3. **Developer Guide**
   - How label resolution works
   - How to add filters for new patterns
   - How to handle new assembler syntax

#### F. Questions for User/Team

Before implementing, decide:

1. Should undefined labels in `.inc` files be checked?
2. Are forward references allowed in this assembly style?
3. What's the expected false positive rate?
4. Should this be enabled by default, or opt-in?
5. Should clashing labels checking be separate from undefined labels?
6. Any assembler-specific edge cases we should know about?
7. How strict should the checking be (Error vs Warning)?

---

### 3. User Options Pages (Extension Configuration UI)
- **Status**: ⏳ Not Started
- **Severity**: 🟠 High
- **Location**: `VS/CSHARP/asm-dude2-vsix/Options/`
- **Required Files**:
  - `GeneralOptions.cs` — code folding, completion, signature help, assembler selection
  - `SyntaxHighlightingOptions.cs` — 13 token types × (Color + Italic flag)
  - `ArchitectureOptions.cs` — ~60 ARCH_* boolean flags (x86, x64, SSE, AVX, etc.)
  - `IntelliSenseOptions.cs` — label analysis, undefined/clashing label handling
  - `PerformanceInfoOptions.cs` — enable/disable per-microarch (Haswell, Skylake, etc.)
  - `AsmSimOptions.cs` — simulator options (Z3 timeout, thread count, 64-bit mode)
  - `AsmDudeOptionsProvider.cs` — aggregate all options, pass to LSP server via initialization
- **What's Missing**: Currently all features use defaults (hardcoded)
- **User Impact**: No way to customize highlighting colors, disable features, or change behavior
- **Effort**: Medium
- **Priority**: HIGH — expected for any VS extension

### 4. Standard LSP codeLens Support
- **Status**: ⏳ Not Started (partial implementation exists)
- **Severity**: 🟠 High
- **Location**: `asm-dude2-ls-lib/LanguageServer.cs`, `LanguageServerTarget.cs`
- **Current State**: Old custom `asm/codeLensData` method exists
- **TODO**:
  - Add `codeLensProvider` capability to ServerCapabilities
  - Add `[JsonRpcMethod("textDocument/codeLens")]` handler
  - Implement standard LSP `GetCodeLensItems(Uri uri)` → `CodeLens[]`
  - Replace custom method with standard
- **Feature**: Shows reference count badges above label definitions
- **Effort**: Low (mostly wiring existing label graph data)
- **Priority**: HIGH — core IDE feature

### 5. Clickable Instruction Links in Signature Help
- **Status**: ⏳ Not Started (infrastructure exists)
- **Severity**: 🟠 High
- **Location**: `asm-dude2-ls-lib/LanguageServer.cs:966` (GetTextDocumentSignatureHelp method)
- **Current State**: Returns plain `SignatureHelp?` with instruction signatures
- **Feature**: Add clickable hyperlinks to instruction documentation (same as hover feature)
- **TODO**:
  - Modify `GetTextDocumentSignatureHelp()` to return `object` instead of `SignatureHelp?`
  - Wrap `SignatureInformation` with `VSInternalSignatureHelp` (similar to how hover uses `VSInternalHover`)
  - Use `MnemonicStore.GetHtmlRef(mnemonic)` to get instruction URL
  - Create `ClassifiedTextRun` with navigation action (like in `HoverBuilder.cs`)
  - Include instruction description + clickable link in signature
- **Why Feasible**: All infrastructure exists; hover already does this; just need to apply same pattern to signature help
- **User Benefit**: Click instruction name in signature help → opens documentation
- **Effort**: Low-Medium (mostly copying/adapting HoverBuilder pattern)
- **Priority**: HIGH — improves discoverability

---

## Medium-Priority Features 🟡

### 6. Null-Safety Enforcement (.NET 10)
- **Status**: ⏳ Partially Done
- **Severity**: 🟡 Medium
- **What's Done**:
  - ✅ `<Nullable>enable</Nullable>` added to asm-dude2-ls-lib.csproj
  - ✅ Compiles with warnings
- **TODO**:
  - Resolve ~100+ CS8600/CS8602/CS8603 null-safety warnings
  - Add `[NotNullWhen]`, `[NotNullIfNotNull]` attributes where appropriate
  - Convert `T?` → `T` or add proper null-checks
  - Goal: Zero nullable-related warnings
- **Effort**: Medium-High
- **Priority**: MEDIUM — improves code quality, catches bugs

### 7. Code Quality Modernization (from GB10)
- **Status**: ⏳ Not Started
- **Severity**: 🟡 Medium
- **Recommended Changes**:
  - Use **init-only properties** for all configuration objects
  - Use **ValueTask** for VS extension lifecycle hooks (async operations)
  - Use **sealed class** declarations where inheritance isn't needed
  - Extract version from `AssemblyInformationalVersionAttribute` instead of hardcoding
  - Implement `OnDisposeAsync()` for proper async cleanup
  - Remove `[VisualStudioContribution]` from Extension class (only on actual contributions)
  - Use **source generators** for document type registration (if SDK supports)
  - Consider **global usings** to reduce import boilerplate
- **Files Affected**: Extension.cs, AsmDocumentTypes.cs, AsmLanguageServerProvider.cs, configuration classes
- **Effort**: Medium
- **Priority**: MEDIUM — improves maintainability, aligns with modern .NET

### 8. Extended Fuzzer Test Coverage
- **Status**: ⏳ Partially Done
- **Severity**: 🟡 Medium
- **What's Done**:
  - ✅ 8 core parsing targets (parsememoperand, parseline, evaluateconstant, etc.)
  - ✅ 7 LSP protocol targets (getdefinition, sendreferences, getdocumentsymbols, etc.)
  - ✅ 1 document change target (documentchange)
- **TODO** (documented in `asm-fuzz/TODO.md`):
  - [ ] SendSettings — configuration changes
  - [ ] AssemblerSyntax — MASM, NASM, AT&T syntax variants
  - [ ] ArchitectureSpecific — ARCH flags per document
  - [ ] RegisterEdgeCases — abbreviated vs. full register names
  - [ ] ComplexExpression — nested parentheses, operator precedence
  - [ ] NumericBoundary — overflow/underflow testing
  - [ ] MultipleDiagnostics — simultaneous diagnostic triggers
- **Effort**: Low-Medium per target
- **Priority**: MEDIUM — prevents regressions

---

## Low-Priority Features 🟢

### 9. Different Assembler Syntax Support
- **Status**: ❌ Not Implemented
- **Severity**: 🟢 Low
- **Current**: Code assumes MASM/NASM primarily
- **Missing**: AT&T syntax variant support (common in GCC/Clang assembly)
- **Scope**: Would require syntax-specific tokenization and validation
- **Effort**: Medium-High
- **Priority**: LOW — niche use case

### 10. Advanced Architecture-Specific Parsing
- **Status**: ⏳ Partial
- **Severity**: 🟢 Low
- **Current State**: Architecture flags exist but not fully validated per instruction
- **Missing**:
  - Enforce instruction validity based on enabled architectures (e.g., AVX-512 instructions only if ARCH_AVX512 enabled)
  - Warn on instruction not available in selected architecture
  - Suggest alternative instructions for older architectures
- **Effort**: Medium
- **Priority**: LOW — improves validation

### 11. Enhanced Register Edge Case Handling
- **Status**: ⏳ Partial
- **Severity**: 🟢 Low
- **Current**: Basic register name parsing (EAX, RAX, AX, AL)
- **Missing**:
  - Full validation across all register sizes and architectures
  - Better error messages for invalid register combinations
  - Suggestion for commonly confused registers
- **Effort**: Low-Medium
- **Priority**: LOW — polish feature

### 12. Complex Expression Evaluation Edge Cases
- **Status**: ⏳ Partial
- **Severity**: 🟢 Low
- **Current**: Basic arithmetic (1 + 2 * 3, hex/decimal mixed)
- **Missing**:
  - Deeply nested parentheses validation
  - Better overflow/underflow handling
  - Support for more operators (bitwise AND, OR, XOR, etc.)
  - Improved error messages for malformed expressions
- **Effort**: Low-Medium
- **Priority**: LOW — edge case handling

### 13. Multiple Diagnostics Per Line Aggregation
- **Status**: ⏳ Partial
- **Severity**: 🟢 Low
- **Current**: Generates diagnostics independently
- **Missing**:
  - De-duplication of overlapping diagnostics
  - Smart ordering/prioritization when multiple issues on one line
  - Combined error messages
- **Effort**: Low
- **Priority**: LOW — UX polish

### 14. CodeLens Performance Optimization
- **Status**: ⏳ Partial
- **Severity**: 🟢 Low
- **Current**: Rebuilds label graph on every document change
- **Opportunity**: Cache label graph, only update changed lines
- **Effort**: Medium
- **Priority**: LOW — performance optimization

---

## Resolved/Completed Features ✅

- ✅ Migration from .NET Framework 4.8 → .NET 10.0-windows
- ✅ LSP server modernization (uses public API types, no CLR hacks)
- ✅ Extension uses VisualStudio.Extensibility SDK v17.14
- ✅ Named pipe communication (asmdude2-output/asmdude2-input)
- ✅ Semantic tokens support (syntax highlighting via LSP)
- ✅ Hover with clickable hyperlinks (custom VS internal types)
- ✅ Code folding (foldingRange LSP method)
- ✅ Code completion (LSP completionItem)
- ✅ Signature help (LSP signatureHelp)
- ✅ Inlay hints (LSP inlayHint)
- ✅ Label graph construction (definition/reference tracking)
- ✅ Basic fuzzer with 8 parsing targets + 8 LSP targets

---

## Prioritization Framework

### Recommended Work Order

**Phase 1: Core Extension Polish & Validation** (Weeks 1-3)
1. Re-enable Undefined Label Diagnostics — already coded, fix false positives
2. User Options Pages — critical for user experience
3. Standard LSP codeLens — complete the feature set
4. Clickable Links in Signature Help — improves discoverability
5. Code Quality Modernization — improves maintainability

**Phase 2: Robustness** (Weeks 4-5)
6. Null-Safety Enforcement — catch bugs early
7. Extended Fuzzer Coverage — prevent regressions

**Phase 3: Advanced Features** (Weeks 6+)
8. Z3 Context Bug Fix (if feasible) — unlock simulator features
9. Architecture-Specific Parsing — validation improvements
10. Complex Expression Handling — edge case fixes

**Phase 4: Nice-to-Have** (Future)
- Assembler Syntax Variants
- Register Edge Cases
- Performance Optimization

---

## Dependencies & Blockers

| Task | Blocks | Blocked By |
|------|--------|-----------|
| Re-enable Undefined Label Diagnostics | (none — standalone) | Investigation of false positives needed |
| User Options Pages | Label diagnostics feature (to control it) | (none) |
| Standard LSP codeLens | Nothing critical | (none) |
| Clickable Links in Signature Help | Nothing | (none) |
| Code Quality Modernization | (none) | (none) |
| Null-Safety Enforcement | (none) | Code Quality Modernization (partly) |
| Extended Fuzzer | (none) | (none) |
| Z3 Bug Fix | Nothing but simulator features | Architectural decision needed |

---

## Notes for Contributors

- **Z3 Simulator**: Tests are marked `[Ignore]`, but the code still compiles. Fix is desirable but not blocking core features.
- **Options Pages**: Extension will work without these (uses defaults), but users expect them in any VS extension.
- **Fuzzer**: Each new target should have seed files in `corpus/<target_name>/` for better mutation.
- **Code Quality**: Use Roslyn analyzers to catch null-safety issues; `<EnableNETAnalyzers>true</EnableNETAnalyzers>` is already set.

---

## Success Metrics

- [ ] Zero compilation errors
- [ ] Zero critical null-safety warnings
- [ ] 95%+ test pass rate (currently 66/92 LSP tests)
- [ ] Users can configure extension via Options pages
- [ ] Fuzzer runs without crashing on all targets
- [ ] Z3 simulator tests no longer skipped (if possible)

