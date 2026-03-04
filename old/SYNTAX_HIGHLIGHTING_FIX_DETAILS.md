# Syntax Highlighting Double-Load Fix - Detailed Changes

## Issue

Syntax highlighting was appearing twice:
1. First highlighting appears instantly with one color scheme
2. Second highlighting appears ~1 second later with the same highlighting

This caused visible flicker and a ~1 second visual lag.

## Root Cause

Two independent systems were both applying syntax highlighting:

1. **AsmClassifier** (editor-side, `asm-dude3-vsix`)
   - Runs instantly when file opens
   - Uses `AsmTokenTaggerProvider` to tokenize
   - Applies colors from `AsmClassificationFormat`
   - Checked at `AsmClassifier.cs:94`

2. **SemanticTokensProvider** (server-side, `asm-dude3-server`)
   - Runs ~1 second after LSP initialization
   - Returns semantic tokens via LSP protocol
   - Client applies the same highlighting again
   - Both systems applied identical colors, causing visual overlap

## Solution: Hybrid Approach

Modified the server to **skip semantic tokens when the editor classifier is active**.

### Code Change

**File**: `VS/CSHARP/asm-dude3/asm-dude3-server/LanguageServer.cs`
**Method**: `GetSemanticTokensFull()` (lines 376-413)

**BEFORE** (Problem - sends semantic tokens always):
```csharp
[JsonRpcMethod("textDocument/semanticTokens/full")]
public SemanticTokensResponse? GetSemanticTokensFull(TextDocumentIdentifier textDocument)
{
    _logger.LogDebug("Semantic tokens requested for: {Uri}", textDocument.Uri);

    var document = _documentManager.GetDocument(textDocument.Uri);
    if (document == null)
    {
        _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
        return null;
    }

    // Get semantic tokens from provider
    var tokens = _semanticTokensProvider.ProvideSemanticTokens(document.Lines);

    // Convert to LSP format (flat integer array with deltas)
    var data = ConvertTokensToLspFormat(tokens);

    return new SemanticTokensResponse
    {
        Data = data
    };
}
```

**AFTER** (Solution - checks if editor classifier is active):
```csharp
/// <summary>
/// Handle semantic tokens request for full document
/// Hybrid approach: Return empty tokens if editor classifier handles syntax highlighting,
/// avoiding double-highlighting and the 1-second flicker.
/// </summary>
[JsonRpcMethod("textDocument/semanticTokens/full")]
public SemanticTokensResponse? GetSemanticTokensFull(TextDocumentIdentifier textDocument)
{
    _logger.LogDebug("Semantic tokens requested for: {Uri}", textDocument.Uri);

    var document = _documentManager.GetDocument(textDocument.Uri);
    if (document == null)
    {
        _logger.LogWarning("Document not found: {Uri}", textDocument.Uri);
        return null;
    }

    // HYBRID APPROACH: If syntax highlighting is enabled on the editor side,
    // don't send semantic tokens to avoid double-highlighting.
    // The editor classifier provides instant highlighting; LSP would add it again after ~1 second.
    if (_options.SyntaxHighlighting_On)
    {
        _logger.LogDebug("SyntaxHighlighting_On=true: Skipping LSP semantic tokens to avoid double-highlighting");
        // Return empty response to skip overlapping highlighting
        return new SemanticTokensResponse { Data = Array.Empty<int>() };
    }

    // Get semantic tokens from provider (only when editor classifier is disabled)
    var tokens = _semanticTokensProvider.ProvideSemanticTokens(document.Lines);

    // Convert to LSP format (flat integer array with deltas)
    var data = ConvertTokensToLspFormat(tokens);

    return new SemanticTokensResponse
    {
        Data = data
    };
}
```

## Key Changes Explained

### 1. Added Hybrid Logic Check
```csharp
if (_options.SyntaxHighlighting_On)
{
    return new SemanticTokensResponse { Data = Array.Empty<int>() };
}
```

When `SyntaxHighlighting_On` is true:
- Editor classifier is already handling highlighting
- Return empty semantic tokens array
- Prevents double-highlighting
- No network/processing overhead

### 2. Preserved Fallback
```csharp
// Get semantic tokens from provider (only when editor classifier is disabled)
var tokens = _semanticTokensProvider.ProvideSemanticTokens(document.Lines);
```

When `SyntaxHighlighting_On` is false:
- Editor classifier is disabled
- LSP provides full semantic tokens
- Allows highlighting to still work via LSP
- Supports future complex semantic analysis

### 3. Added Logging
```csharp
_logger.LogDebug("SyntaxHighlighting_On=true: Skipping LSP semantic tokens to avoid double-highlighting");
```

Logs when hybrid approach activates for debugging.

## How Settings Flow Through System

```
User Preference
↓
Tools → Options → AsmDude3 → General → "Enable syntax highlighting"
↓
Settings.Default.SyntaxHighlighting_On
↓
AsmLanguageClient.InitializationOptions
↓
LSP Client → InitializeRequest → LSP Server
↓
AsmLanguageServerOptions._options
↓
LanguageServer._options.SyntaxHighlighting_On
↓
GetSemanticTokensFull() checks this flag
↓
Decision: Return tokens or return empty
```

## Why This Works

1. **Settings Available**: `_options.SyntaxHighlighting_On` is populated from `InitializationOptions` (line 251 of LanguageServer.cs)

2. **Timing**: Settings are loaded during LSP initialization, before first semantic token request

3. **Behavior**:
   - Default: `SyntaxHighlighting_On = true` → Editor classifier active → LSP returns empty
   - User disables: `SyntaxHighlighting_On = false` → Editor classifier inactive → LSP returns tokens

4. **Performance**: No cost for the check (single boolean comparison)

## Impact Assessment

| Aspect | Before | After | Impact |
|--------|--------|-------|--------|
| Visual Flicker | Yes (~1 sec delay) | No | ✅ Fixed |
| Color Consistency | Inconsistent | Consistent | ✅ Improved |
| Highlighting Speed | Instant (but duplicated) | Instant | ✅ Same |
| Network Traffic | More (sends tokens) | Less (empty) | ✅ Reduced |
| Server Processing | More (generates tokens) | Less (skips) | ✅ Reduced |
| Code Complexity | N/A | Single if statement | ✅ Minimal |

## Compilation

- ✅ `asm-dude3-server`: 0 errors
- ✅ `asm-dude3-vsix`: 0 errors

## Deployment

1. Build with fix
2. VSIX includes updated LSP server
3. When extension loads:
   - Client passes settings to server during `initialize`
   - Server checks `_options.SyntaxHighlighting_On` on first semantic token request
   - Hybrid approach automatically activates

## Testing Checklist

- [ ] Open assembly file - highlighting appears instantly
- [ ] No color flicker visible
- [ ] Colors are consistent throughout file
- [ ] LSP server logs show hybrid approach active
- [ ] Toggle syntax highlighting on/off in options
- [ ] Extension continues to work with highlighting disabled
- [ ] Verify settings persist across restart

## Future Enhancement Opportunity

The current implementation provides a foundation for:
- Complex semantic analysis via LSP tokens
- Label cross-references
- Advanced code intelligence
- Custom semantic highlighting

When ready, modify the condition to enable LSP semantic tokens for specific analysis:
```csharp
if (_options.SyntaxHighlighting_On && !_options.PerformComplexAnalysis)
{
    // Skip only basic syntax tokens, allow semantic analysis
}
```

## References

- **Issue**: User reported double-highlighting with ~1 second flicker
- **Architecture**: Two independent highlighting systems
- **Solution**: Hybrid approach using settings-based coordination
- **Impact**: Eliminates flicker, improves performance, maintains flexibility
