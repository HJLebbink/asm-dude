# Code Annotation Agent - AsmDude2 LSP Server

**Project**: AsmDude2 - Visual Studio 2022/2026 assembly language support via LSP  
**Language**: C# 14 (.NET 10.0)  
**Documentation Style**: XML comments (`///`) with LLM-enhanced sections  
**Location**: `VS/CSHARP/asm-dude2-ls-lib/` (primary), `VS/CSHARP/asm-dude2-ls/` (secondary)

---

## Purpose

Generate comprehensive C# XML documentation for the AsmDude2 LSP server. This agent prioritizes:

1. **LSP protocol methods** (`LanguageServer.cs`, `LanguageServerTarget.cs`)
2. **Public API methods** (especially JSON-RPC exposed via `[JsonRpcMethod]`)
3. **Complex algorithms** (signature constraint, folding ranges, semantic token encoding)
4. **Performance-critical code** (debounced updates, chunked results, caching)
5. **Undocumented/incomplete methods** (fill XML gaps)

---

## Documentation Standards

### 1. XML Comment Tags (C# Standard)

Use these XML tags consistently:

```csharp
/// <summary>
/// Brief description of what the method does (1-2 sentences).
/// Include key purpose and return value summary.
/// </summary>
/// <param name="parameterName">Description of parameter and its requirements.</param>
/// <returns>Description of return value or null if nullable.</returns>
/// <remarks>
/// Important implementation notes, edge cases, or usage warnings.
/// Use this for advanced details that don't fit in summary.
/// </remarks>
/// <example>
/// Example usage (optional, but recommended for public APIs):
/// <code>
/// var result = MethodName(param1, param2);
/// </code>
/// </example>
```

### 2. LLM-Enhanced Annotations

After standard XML, add these LLM-specific sections:

```csharp
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: [comma-separated keywords for LLM indexing]
/// USED IN: [list of calling methods/files]
/// SEE ALSO: [related methods/files with brief rationale]
```

### 3. Inline Comments

Use `//` comments for complex logic, unusual patterns, or performance-critical sections:

```csharp
// Debounce document updates - cancel pending update and schedule new one
// Use 100ms delay to avoid excessive re-parsing during typing
```

---

## Documentation Priority Matrix

| Priority | Method Type | Annotation Requirements |
|----------|-------------|------------------------|
| **P0** | `[JsonRpcMethod]` handlers | Full XML: summary, params, returns, remarks |
| **P1** | LSP protocol overrides (IGetter/ISetter) | Full XML with LSP context |
| **P2** | Core algorithms (constraints, parsing) | Full XML with algorithm rationale |
| **P3** | Performance-critical methods | Full XML + inline comments for optimizations |
| **P4** | Private/helpers | Minimal XML (summary + key params) |
| **P5** | Properties/fields | Summary only unless complex |

---

## Code Patterns & Documentation

### 1. JsonRpcMethod Handlers

Document LSP protocol method, parameters, purpose, and return value:

```csharp
/// <summary>
/// Handle semantic tokens full request for rich syntax highlighting.
/// Returns delta-encoded token data matching the semantic tokens legend.
/// </summary>
/// <param name="parameter">Request parameters with document URI.</param>
/// <returns>SemanticTokens with ResultId and delta-encoded Data array.</returns>
/// <remarks>
/// Token types map AsmTokenType to LSP indices:
///   0: keyword (mnemonics), 1: variable (registers), 2: label (labels),
///   3: macro (directives), 4: number (constants), 5: operator (memory),
///   6: comment (remarks), 7: string, 8: function (CALL targets).
/// </remarks>
/// <example>
/// Client requests: textDocument/semanticTokens/full
/// Server returns: { resultId: "1", data: [deltaLine, deltaChar, length, type, modifiers] }
/// </example>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: semantic tokens, syntax highlighting, LSP, delta encoding, token types
/// USED IN: LanguageServer.GetSemanticTokens, LanguageServerTarget.GetSemanticTokensFull
/// SEE ALSO: GetSemanticTokensDelta for delta requests
```

### 2. Complex Algorithms

Document algorithm logic, constraints, and rationale:

```csharp
/// <summary>
/// Constrain signature list based on operand types and selected architectures.
/// Filters out incompatible signatures before presenting to user.
/// </summary>
/// <param name="data">All available signatures for a mnemonic.</param>
/// <param name="operands2">Current operand list from parser.</param>
/// <param name="selectedArchitectures2">Architectures enabled in options.</param>
/// <returns>Filtered sequence of compatible signatures.</returns>
/// <remarks>
/// Constraint logic:
///   1. Remove signatures not supporting selected architectures
///   2. Check each operand against signature operand definitions
///   3. Allow only signatures matching all operand constraints
/// 
/// Returns via yield return for deferred execution and memory efficiency.
/// </remarks>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: signature constraint, architecture filter, operand matching, filtering algorithm
/// USED IN: LanguageServer.GetTextDocumentSignatureHelp
/// SEE ALSO: MnemonicStore.GetSignatures, AsmSignatureInformation.Is_Allowed
```

### 3. Performance-Critical Methods

Document optimization rationale with inline comments:

```csharp
private string[] GetLines(string uri)
{
    // Cache lookup for parsed document lines
    // Returns empty array if URI not found (avoids null checks in calling code)
    if (this.textDocumentLines.TryGetValue(uri, out var lines))
    {
        return lines;
    }
    return [];
}
```

### 4. Public Properties

Document purpose and usage context:

```csharp
/// <summary>
/// Current assembler type detected for this document (MASM/NASM/UNKNOWN).
/// Used for MASM/NASM-specific token highlighting and parsing.
/// </summary>
/// <remarks>
/// Auto-detected by checking for MASM-specific directives (PROC, ENDP, MACRO, etc.)
/// or NASM-specific directives (SECTION, GLOBAL, CPU, %ifdef, etc.) in first 20 lines.
/// Can be MASM | NASM_INTEL | NASM_ATT if both types detected.
/// </remarks>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: assembler detection, MASM, NASM, auto-detect, document type
/// USED IN: UpdateInternals, ParseLine
/// SEE ALSO: _documentAssemblerTypes dictionary, AssemblerEnum
```

### 5. Event Handlers

Document event relationship and purpose:

```csharp
/// <summary>
/// Event triggered after server initialization completes.
/// Clients use this to know when the server is ready for requests.
/// </summary>
/// <remarks>
/// Notified via OnInitializeComplete() called after Initialize() response is sent.
/// See: LanguageServer.OnInitializeComplete, LanguageServerTarget.Initialize
/// </remarks>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: initialization, lifecycle, event, notification
/// USED IN: OnInitializeComplete, OnTargetInitializeCompletion
/// SEE ALSO: Initialized event, OnInitialized
```

---

## LSP Protocol Methods

Document these first—these are the server's public API surface:

| Method | Protocol | Priority | File |
|--------|----------|----------|------|
| `Initialize` | `workspace/initialize` | P0 | LanguageServerTarget.cs |
| `Initialized` | `initialized` | P0 | LanguageServerTarget.cs |
| `OnTextDocumentCompletion` | `textDocument/completion` | P0 | LanguageServerTarget.cs |
| `GetTextDocumentSignatureHelp` | `textDocument/signatureHelp` | P0 | LanguageServerTarget.cs |
| `OnHover` | `textDocument/hover` | P0 | LanguageServerTarget.cs |
| `GetSemanticTokensFull` | `textDocument/semanticTokens/full` | P0 | LanguageServerTarget.cs |
| `TextDocumentDefinition` | `textDocument/definition` | P0 | LanguageServerTarget.cs |
| `OnTextDocumentFindReferences` | `textDocument/references` | P0 | LanguageServerTarget.cs |
| `GetFoldingRanges` | `textDocument/foldingRange` | P0 | LanguageServerTarget.cs |
| `TextDocumentCodeAction` | `textDocument/codeAction` | P0 | LanguageServerTarget.cs |
| `GetCodeLensData` | `asm/codeLensData` | P0 | LanguageServerTarget.cs (custom) |

---

## Documentation Checklist

Before marking a method as documented, verify:

- [ ] **Summary**: 1-2 sentences describing purpose and return value
- [ ] **Params**: All parameters documented with purpose and requirements
- [ ] **Returns**: Return value or nullability explained
- [ ] **Remarks**: Edge cases, performance notes, or implementation details
- [ ] **Example**: At least one usage example for public APIs
- [ ] **LLM-ANNOTATION**: Keywords, usage, and related methods
- [ ] **Inline Comments**: Complex logic, optimizations, or non-obvious patterns

---

## VS-Specific Types

### VSInternalHover

Document VS-compatible hover types with `_vs_rawContent` property:

```csharp
/// <summary>
/// VS-internal hover type with RawContent for styled text rendering.
/// Uses _vs_rawContent property with ClassifiedTextElement for monospace/font-styled tooltips.
/// </summary>
/// <remarks>
/// CRITICAL: Clickable links are NOT possible over LSP. NavigationAction is an Action delegate
/// (C# callback), not a URL string. Delegates cannot be serialized over JSON-RPC.
/// 
/// To get clickable links, use an in-process MEF extension with IAsyncQuickInfoSource
/// (requires hybrid VSSDK+VisualStudio.Extensibility architecture).
/// See: VS/CSHARP/old/asm-dude2-vsix-archived/QuickInfo/AsmQuickInfoSource.cs
/// 
/// What works over LSP:
///   - Classified text via ClassificationTypeName ("formal language", "keyword", etc.)
///   - Monospace font via UseClassificationFont (0x8) + "formal language"
///   - Bold/italic/underline via ClassifiedTextRunStyle flags
///   - Stacked/wrapped layout via ContainerElement
/// </remarks>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: hover, tooltip, VSInternalHover, classified text, LSP
/// USED IN: HoverBuilder, LanguageServer.GetHover
/// SEE ALSO: ClassifiedTextElement, ClassifiedTextRun, ContainerElement
```

### ClassifiedTextRun

```csharp
/// <summary>
/// A single run of styled text for VS hover tooltips.
/// Cannot include NavigationAction over LSP (Action delegate not serializable).
/// </summary>
/// <param name="ClassificationType">VS classification type name ("formal language", "keyword", etc.).</param>
/// <param name="Text">The text content to render.</param>
/// <param name="Style">Text style flags (Bold, Italic, UseClassificationFont, etc.).</param>
/// <param name="Tooltip">Optional tooltip text shown on hover over this run.</param>
/// <remarks>
/// Use "formal language" with UseClassificationFont (0x8) for monospace font.
/// Use "keyword" for syntax-highlighted keywords with VS's keyword color.
/// </remarks>
/// <!-- LLM-ANNOTATION -->
/// LLM KEYWORDS: classified text, hover tooltip, VS styling, monospace, formatting
/// USED IN: HoverBuilder.CreateKeywordHover, HoverBuilder.CreateStackedHover
/// SEE ALSO: VSInternalHover, PredefinedClassificationTypeNames
```

---

## Testing Documentation Reference

Use test files to understand expected behavior:

| Test File | Coverage | Notes |
|-----------|----------|-------|
| `LanguageServerTests.cs` | Unit tests for core LSP methods | P0 for documenting LSP protocol methods |
| `MnemonicStoreTests.cs` | Signature constraint tests | Verify `Constrain_Signatures` behavior |
| `PerformanceStoreTests.cs` | Performance data loading | Document performance features |
| `AsmSimTests.cs` | Z3 simulator integration | Document `GetProvenStates` and simulation features |

---

## Output Format

```markdown
# Code Annotation Report

## Verification Results

| File | Function | Lines | Status | Notes |
|------|----------|-------|--------|-------|
| LanguageServer.cs | GetSemanticTokens | 1183-1235 | Verified | Docs complete and correct |
| LanguageServerTarget.cs | Initialize | 100-150 | Incomplete | Missing @param, @return |

## Generated Documentation

| File | Function | Lines | Documentation Added/Enhanced |
|------|----------|-------|------------------------------|
| LanguageServerTarget.cs | Initialize | 100-150 | @brief, @param, @return, @note |

## LLM Keywords Added

| File | Function | Keywords |
|------|----------|----------|
| LanguageServerTarget.cs | Initialize | LSP, workspace, initialize, server, client |

## Documentation Improvements

| File | Function | Lines | Issue | Fix |
|------|----------|-------|-------|-----|
| LanguageServerTarget.cs | Initialize | 100-150 | Missing @return | Added return description |
```

---

## Related Files (Primary Documentation Targets)

| File | Lines | Purpose | Priority |
|------|-------|---------|----------|
| `LanguageServer.cs` | ~3007 | Main LSP server implementation | P0 |
| `LanguageServerTarget.cs` | ~865 | LSP protocol method handlers | P0 |
| `HoverBuilder.cs` | ~213 | Hover tooltip builder | P1 |
| `VSInternalTypes.cs` | ~217 | VS-compatible hover types | P1 |
| `VSTypes.cs` | ~15 | VS-specific helper types | P5 |
| `LanguageServerConstants.cs` | ? | Constants and defaults | P5 |

### Supporting Libraries (Reference Only)

| File | Purpose | Notes |
|------|---------|-------|
| `MnemonicStore.cs` | Instruction signature storage | Used by signature help |
| `PerformanceStore.cs` | CPU performance data loader | Document performance features |
| `LspAsmSimulator.cs` | Z3-based assembly simulator | Document simulation features |
| `LabelGraph.cs` | Assembly label relationship graph | Document label analysis |

---

## Agent Configuration Summary

**Language**: C# 14 (.NET 10.0)  
**Documentation Style**: XML comments (`///`) with LLM-enhanced sections  
**Focus Areas**: LSP protocol methods, public API, complex algorithms, performance-critical code  
**Output Format**: Markdown report with verification tables and documentation summaries  
**Test File Reference**: `VS/CSHARP/asm-dude2-ls-tests/` for behavior verification  

---

**Last Updated**: 2026-03-21  
**Agent Version**: 1.0  
**Target Project**: AsmDude2 LSP Server (asm-dude2-ls-lib)
