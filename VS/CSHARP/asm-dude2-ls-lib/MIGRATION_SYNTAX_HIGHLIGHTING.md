# Migration Plan: Restore MASM/NASM-specific Syntax Highlighting via LSP Semantic Tokens

## Goal
Restore the assembler-specific (MASM/NASM) syntax highlighting from the original MEF-based implementation into the modern LSP semantic tokens framework, preserving existing functionality and user color configuration capabilities.

## Background
The original MEF-based syntax highlighting system (removed in commit 33a0376176c465321309f044290ba328c292e467) included:
- Assembler-specific taggers (`MasmTokenTagger`, `NasmAttTokenTagger`, `NasmIntelTokenTagger`)
- Classification definitions with GUID-based token types (`AsmClassificationDefinition.cs`)
- Visual format mappings (`AsmClassificationFormat.cs`)

The current LSP implementation uses semantic tokens for syntax highlighting:
- Token legend defined in `LanguageServerTarget.cs` (indices 0-9: keyword, variable, label, macro, number, operator, comment, string, function, decorator)
- Token mapping handled in `LanguageServer.cs` via `MapTokenType()` and `GetTokenModifiers()`
- Parsing performed by `AsmSourceTools.ParseLine()` in `asm-tools-lib\AsmSourceTools.cs`, returning `KeywordID[][]` structures

## Migration Strategy
Extend the existing semantic token system to recognize MASM/NASM-specific constructs while preserving backward compatibility:
1. Extend `AsmTokenType` enum with assembler-specific types ✓
2. Update semantic tokens legend in `LanguageServerTarget.cs` (append new types, preserve existing indices) ✓
3. Enhance `MapTokenType()` and `GetTokenModifiers()` in `LanguageServer.cs` to map new token types ✓
4. Improve `AsmSourceTools.ParseLine()` to detect MASM/NASM directives and assign appropriate token types ✓
5. Store per-document assembler type context (MASM vs NASM) for accurate token assignment ✓
6. Optionally enhance downstream features (CodeLens, hover, etc.) to utilize assembler context

## Implementation Steps

### Step 1: Extend AsmTokenType enum
**File**: `VS\CSHARP\asm-tools-lib\AsmTokenTypes.cs`
- Add new token types: `MasmDirective`, `NasmDirective`, `MasmOperator`, `NasmOperator`, `MasmPseudoOp`, `NasmPseudoOp`
- Preserve existing enum values and order

### Step 2: Update Semantic Tokens Legend
**File**: `VS\CSHARP\asm-dude2-ls-lib\LanguageServerTarget.cs`
- In the `SemanticTokensOptions.Legend.TokenTypes` array, append new token type strings after existing ones
- Maintain existing indices 0-9 for backward compatibility
- New indices will be assigned sequentially (10, 11, 12, ...)

### Step 3: Enhance Token Mapping Functions
**File**: `VS\CSHARP\asm-dude2-ls-lib\LanguageServer.cs`
- Update `MapTokenType(AsmTokenType type)` to map new token types to appropriate LSP semantic token indices
- Update `GetTokenModifiers(AsmTokenType type)` to assign appropriate modifiers for new token types if needed

### Step 4: Enhance Parser for Assembler-specific Constructs
**File**: `VS\CSHARP\asm-tools-lib\AsmSourceTools.cs`
- Modify `ParseLine()` to recognize MASM/NASM-specific directives, operators, and pseudo-ops
- Assign appropriate `AsmTokenType` values to recognized constructs
- Preserve existing tokenization for backward compatibility

### Step 5: Store Per-document Assembler Context
**File**: `VS\CSHARP\asm-dude2-ls-lib\LanguageServer.cs`
- Add a dictionary to store assembler type per document URI: `Dictionary<string, AssemblerType> _documentAssemblerTypes;`
- Implement logic to detect assembler type (MASM vs NASM) based on:
  - File content heuristics (e.g., presence of MASM/NASM-specific directives)
  - User settings (if available)
  - Fallback to default assembler type
- Use this context in `ParseLine()` to influence token assignment

### Step 6: Optional - Enhance Downstream Features
Consider enhancing features like CodeLens, hover tooltips, or completion to utilize assembler context for improved accuracy.

## Backward Compatibility Guarantees
- Existing semantic token indices (0-9) remain unchanged
- Existing token types (keyword, variable, label, etc.) continue to map as before
- Standard .asm files without assembler-specific constructs will highlight identically to before
- User color configurations for existing token types remain functional

## Performance Considerations
- Parser enhancements should aim for <5% performance degradation
- Consider caching assembler type detection results per document
- Profile performance with typical assembly files

## Testing Approach
1. Verify backward compatibility: Standard .asm files produce identical semantic token output
2. Verify MASM/NASM specificity: Files with MASM/NASM constructs receive appropriate new token types
3. Verify mixed-file handling: Files containing both assembler styles are handled appropriately
4. Verify user settings: Color configurations apply to new token types as expected

## Files to Modify
1. `VS\CSHARP\asm-tools-lib\AsmTokenTypes.cs`
2. `VS\CSHARP\asm-dude2-ls-lib\LanguageServerTarget.cs`
3. `VS\CSHARP\asm-dude2-ls-lib\LanguageServer.cs`
4. `VS\CSHARP\asm-tools-lib\AsmSourceTools.cs`

## Reference Implementation (Old Code)
Refer to the archived SyntaxHighlighting directory for reference implementation:
- `VS\CSHARP\old\asm-dude2-vsix-archived\SyntaxHighlighting\MasmTokenTagger.cs`
- `VS\CSHARP\old\asm-dude2-vsix-archived\SyntaxHighlighting\NasmAttTokenTagger.cs`
- `VS\CSHARP\old\asm-dude2-vsix-archived\SyntaxHighlighting\NasmIntelTokenTagger.cs`
- `VS\CSHARP\old\asm-dude2-vsix-archived\SyntaxHighlighting\AsmClassificationDefinition.cs`
- `VS\CSHARP\old\asm-dude2-vsix-archived\SyntaxHighlighting\AsmClassificationFormat.cs`