# Syntax Highlighting Double-Load Fix

## Problem

The AsmDude3 extension was displaying syntax highlighting twice with a ~1 second delay:
1. **Immediate highlighting** (appears instantly)
2. **Different highlighting** (appears ~1 second later)

This caused visible flicker and inconsistent color schemes as the highlighting switched between two separate systems.

## Root Cause Analysis

Two independent syntax highlighting systems were running in parallel:

### System 1: Editor-Side Classifier (AsmClassifier)
- **Location**: `AsmClassifier.cs` in asm-dude3-vsix
- **Timing**: Instant (no delay)
- **Source**: Uses `AsmTokenTaggerProvider` to tokenize code via `AsmTools.Get_Token_Type_Intel()`
- **Application**: Applies colors from `AsmClassificationFormat.cs` based on `Settings.Default`
- **Check**: Line 94 - `if (!Settings.Default.SyntaxHighlighting_On) yield break;`

### System 2: LSP Semantic Tokens (SemanticTokensProvider)
- **Location**: `SemanticTokensProvider.cs` in asm-dude3-server
- **Timing**: ~1 second delay (runs after LSP initialization)
- **Source**: Server-side tokenization via `LanguageServer.GetSemanticTokensFull()`
- **Application**: Sent via LSP protocol to VS client
- **Problem**: Applied the SAME highlighting as System 1, causing overlap

## Why Visible Flicker Occurred

1. File opens → **System 1 applies highlighting immediately** (0ms)
2. LSP server initializes → **System 2 sends semantic tokens** (~1000ms)
3. VS receives semantic tokens → **System 2 applies highlighting** (overwrites System 1)
4. Visual effect: Colors appear, then change/flicker as System 2 applies

## Solution: Hybrid Approach

Implemented a **hybrid highlighting method** that leverages both systems:
- **System 1 (AsmClassifier)**: Handles all basic syntax highlighting (mnemonics, registers, directives, etc.)
- **System 2 (SemanticTokens)**: Reserved for complex semantic analysis (unused for now)

### Key Implementation

Modified `LanguageServer.GetSemanticTokensFull()` in `asm-dude3-server/LanguageServer.cs`:

```csharp
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
// ... convert and return
```

## Benefits

✅ **No more flicker** - Only one highlighting system applies
✅ **Instant highlighting** - Uses the fast editor classifier (0ms delay)
✅ **Consistent colors** - Settings applied uniformly
✅ **Future-ready** - LSP system available when needed for semantic analysis
✅ **Flexible** - Can be toggled via `SyntaxHighlighting_On` setting
✅ **Performance** - Reduced network traffic and LSP processing

## How It Works

1. **When `SyntaxHighlighting_On = true`** (default):
   - Editor classifier applies highlighting instantly
   - LSP semantic tokens are skipped (returns empty array)
   - Result: Clean, instant, consistent highlighting

2. **When `SyntaxHighlighting_On = false`**:
   - Editor classifier returns no tags
   - LSP semantic tokens are sent by server
   - Result: Server-side highlighting (useful for complex semantic analysis)

## Settings Communication

The solution works because:
- Client passes `InitializationOptions` to LSP server during `initialize` request
- `InitializationOptions` includes `SyntaxHighlighting_On` flag from `AsmLanguageClient.cs`
- Server deserializes into `AsmLanguageServerOptions`
- Server checks this option before generating/sending semantic tokens

## Files Modified

- **LanguageServer.cs** (asm-dude3-server)
  - Lines 376-413: Added hybrid approach logic to `GetSemanticTokensFull()`
  - Returns empty semantic tokens when editor classifier is active

## Testing Recommendations

1. Open an assembly file with the extension
2. Verify highlighting appears **instantly** (no ~1 second delay)
3. Verify highlighting is **consistent** (no color flicker)
4. Test toggling `Tools → Options → AsmDude3 → General → Enable syntax highlighting`
5. Verify LSP server logs show: `"SyntaxHighlighting_On=true: Skipping LSP semantic tokens"`

## Future Enhancements

The current implementation enables:
- LSP semantic tokens can be re-enabled for complex analysis
- Color schemes can be updated without restarting
- Both systems coexist and can be toggled independently
- Foundation for advanced semantic analysis (label resolution, cross-references, etc.)

## Related Files

- `AsmClassifier.cs` - Editor-side classifier (instant highlighting)
- `SemanticTokensProvider.cs` - Server-side token provider (complex analysis)
- `AsmLanguageClient.cs` - Sends InitializationOptions with settings
- `Settings.Designer.cs` - Contains SyntaxHighlighting_On setting
- `AsmClassificationFormat.cs` - Maps token types to colors
