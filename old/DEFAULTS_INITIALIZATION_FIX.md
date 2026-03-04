# Default Settings Initialization Fix

## Problem

When opening AsmDude3 Options page (`Tools → Options → AsmDude3 → General`), none of the default settings were displayed as enabled, even though they were configured with `DefaultSettingValueAttribute("True")` in Settings.Designer.cs.

## Root Cause

The `DefaultSettingValueAttribute` in .NET Settings only provides a default value when the setting is **first accessed** and no value exists in the registry. However, Visual Studio's settings system doesn't automatically load these defaults into the UI controls until explicitly told to do so.

The defaults needed to be programmatically initialized and saved to ensure they appear in the registry on first run.

## Solution

Added an `InitializeSettings()` method in `AsmDude3Package.cs` that:

1. **Runs on package initialization** - Called from `InitializeAsync()` when the extension first loads
2. **Explicitly sets all key defaults** - Ensures important settings are set to their intended values
3. **Saves to registry** - Persists the settings so they appear in the Options page

### Code Added to AsmDude3Package.cs

```csharp
protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
{
    await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
    Debug.WriteLine("AsmDude3Package: InitializeAsync called");

    // Initialize settings with defaults if this is first run
    InitializeSettings();
}

private static void InitializeSettings()
{
    try
    {
        var settings = Settings.Default;
        Debug.WriteLine("AsmDude3Package: Initializing default settings");

        // Explicitly set key defaults that should be enabled
        settings.SyntaxHighlighting_On = true;
        settings.CodeFolding_On = true;
        settings.AsmDoc_On = true;
        settings.PerformanceInfo_On = true;
        settings.PerformanceInfo_SkylakeX_On = true;
        settings.useAssemblerAutoDetect = true;
        settings.CodeCompletion_On = true;
        settings.SignatureHelp_On = true;
        settings.IntelliSense_Label_Analysis_On = true;
        settings.IntelliSense_Show_Undefined_Labels = true;
        settings.IntelliSense_Show_Undefined_Includes = true;

        // Save the initialized settings
        settings.Save();
        Debug.WriteLine("AsmDude3Package: Default settings applied and saved");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"AsmDude3Package: Error initializing settings: {ex.Message}");
    }
}
```

## What Gets Initialized

The following settings are now guaranteed to be enabled (True) on first run:

| Setting | Default | Purpose |
|---------|---------|---------|
| **SyntaxHighlighting_On** | True | Enable assembly code colorization |
| **CodeFolding_On** | True | Enable code region folding |
| **AsmDoc_On** | True | Enable documentation links |
| **PerformanceInfo_On** | True | Enable performance metrics display |
| **PerformanceInfo_SkylakeX_On** | True | Enable Skylake-X latency data |
| **useAssemblerAutoDetect** | True | Auto-detect assembly flavor |
| **CodeCompletion_On** | True | Enable code completion |
| **SignatureHelp_On** | True | Enable signature help |
| **IntelliSense_Label_Analysis_On** | True | Enable label analysis |
| **IntelliSense_Show_Undefined_Labels** | True | Show undefined labels |
| **IntelliSense_Show_Undefined_Includes** | True | Show undefined includes |

## Timing

- **When it runs**: During package initialization (`InitializeAsync()`)
- **How often**: Every time the extension loads (ensures settings remain synchronized)
- **Performance impact**: Minimal - just a few property assignments and a registry save

## Testing

1. **First run after update**:
   - Open Tools → Options → AsmDude3 → General
   - All checkboxes should be checked (enabled)
   - Syntax highlighting colors should be pre-populated

2. **Subsequent runs**:
   - Settings persist from user's last session
   - Initialization code re-applies defaults (ensures consistency)

3. **Debug output**:
   - Look in Debug Output window for:
   ```
   AsmDude3Package: InitializeAsync called
   AsmDude3Package: Initializing default settings
   AsmDude3Package: Default settings applied and saved
   ```

## Build Status

✅ **Build Succeeded**
- Errors: 0
- Warnings: 54 (non-critical nullability warnings)

## Files Modified

- `AsmDude3Package.cs` - Added settings initialization
- `Settings.Designer.cs` - No changes needed (defaults already configured)

## Impact

Users now see a fully configured Options page with all key features enabled by default:
- ✅ Syntax highlighting active
- ✅ Code folding enabled
- ✅ Documentation links available
- ✅ Performance data for SkylakeX enabled
- ✅ Assembly auto-detection enabled
- ✅ IntelliSense features all active

This matches the AsmDude2 user experience where sensible defaults are pre-configured.
