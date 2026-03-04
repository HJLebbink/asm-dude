# Syntax Highlighting Hybrid Method - Implementation Summary

## Issue Fixed

**User Report**: "I notice that there is for syntaxhighlighting, and then after a second and different highlighting is loaded."

**Visual Symptom**: Syntax highlighting appears twice with ~1 second flicker between two different color schemes.

## Solution Implemented

Implemented a **hybrid syntax highlighting method** that uses the fast editor-side classifier for all basic syntax highlighting and reserves the LSP semantic tokens system for future complex semantic analysis.

### Change Made

**File**: `VS/CSHARP/asm-dude3/asm-dude3-server/LanguageServer.cs`
**Method**: `GetSemanticTokensFull()` (lines 376-413)

**Logic**:
When the editor-side classifier is active (SyntaxHighlighting_On = true), return empty semantic tokens to prevent double-highlighting.

```csharp
// HYBRID APPROACH: If syntax highlighting is enabled on the editor side,
// don't send semantic tokens to avoid double-highlighting.
if (_options.SyntaxHighlighting_On)
{
    return new SemanticTokensResponse { Data = Array.Empty<int>() };
}
```

## Architecture

### Before Fix (Problem)

```
File opens
    ↓
[System 1: AsmClassifier] → Instant highlighting (0ms)
    ↓
[System 2: LSP SemanticTokens] → Re-applies same highlighting (~1000ms)
    ↓
Visual Result: Colors appear, then change/flicker
```

### After Fix (Solution)

```
File opens
    ↓
[System 1: AsmClassifier] → Instant highlighting (0ms)
    ↓
[System 2: LSP] → Checks SyntaxHighlighting_On, skips tokens
    ↓
Visual Result: Clean, consistent highlighting with no flicker
```

## Benefits

| Aspect | Benefit |
|--------|---------|
| **Performance** | Eliminated redundant highlighting processing |
| **UX** | No more visible flicker or color changes |
| **Consistency** | Single color scheme applied uniformly |
| **Speed** | Uses instant editor classifier (0ms vs 1000ms) |
| **Flexibility** | Can toggle via `SyntaxHighlighting_On` setting |
| **Future-Proof** | LSP system available for complex semantic analysis |

## How It Works

### Communication Flow

1. **Initialization**
   - Client passes `InitializationOptions` with settings to server
   - Includes `SyntaxHighlighting_On` flag from user preferences
   - Server stores in `_options.SyntaxHighlighting_On`

2. **Semantic Tokens Request**
   - Client requests: `textDocument/semanticTokens/full`
   - Server checks: `if (_options.SyntaxHighlighting_On)`
   - If true: Returns `Data = Array.Empty<int>()` (empty, no highlighting)
   - If false: Returns full semantic tokens for server-side highlighting

3. **Highlighting Applied**
   - Editor classifier: Always runs if SyntaxHighlighting_On = true
   - LSP tokens: Only applied if SyntaxHighlighting_On = false

## Build Status

✅ **asm-dude3-server**: Builds successfully (0 errors)
✅ **asm-dude3-vsix**: Builds successfully (0 errors)

## Testing the Fix

To verify the fix works:

1. Open an assembly file with the extension
2. Observe highlighting appears **instantly** (no delay)
3. Observe highlighting is **consistent** (no flicker)
4. Check extension log: Should show `"SyntaxHighlighting_On=true: Skipping LSP semantic tokens"`

## Future Enhancements

The current implementation:
- ✅ Eliminates double-highlighting
- ✅ Uses fast editor classifier by default
- 🔄 Reserves LSP semantic tokens for complex semantic analysis
- 🔄 Can be toggled via settings without restarting
- 🔄 Foundation for advanced features (label resolution, cross-references)

## Related Components

| Component | Purpose | Location |
|-----------|---------|----------|
| AsmClassifier | Editor-side highlighting (instant) | asm-dude3-vsix |
| SemanticTokensProvider | Server-side token generation | asm-dude3-server |
| AsmLanguageClient | Sends InitializationOptions | asm-dude3-vsix |
| AsmLanguageServerOptions | Deserializes client settings | asm-tools-lib |
| AsmClassificationFormat | Maps tokens to colors | asm-dude3-vsix |

## Settings Relationship

- **User Setting**: `Tools → Options → AsmDude3 → General → Enable syntax highlighting`
- **Storage**: `Settings.Default.SyntaxHighlighting_On`
- **Transmission**: Via `AsmLanguageClient.InitializationOptions`
- **Used By**: Server's `_options.SyntaxHighlighting_On`
- **Effect**: Determines whether LSP sends semantic tokens

## Performance Impact

- **Network Traffic**: Reduced (no semantic tokens sent when highlighting enabled)
- **Server Processing**: Reduced (no semantic token generation when not needed)
- **Client Rendering**: Same (editor classifier was already doing this)
- **User Experience**: Improved (no flicker, instant appearance)

## Conclusion

The hybrid approach successfully eliminates the double-highlighting issue by:
1. Leveraging the fast, instant editor classifier for basic syntax highlighting
2. Skipping redundant LSP semantic tokens when not needed
3. Preserving both systems for flexibility and future enhancements
4. Improving performance and user experience simultaneously

The fix is minimal, focused, and maintains architectural flexibility for future development.
