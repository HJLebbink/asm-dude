# Options Page Integration Testing Guide

## Overview

This document describes how to test the AsmDude3 Options page functionality, including both manual testing and automated testing approaches.

## What Was Fixed

The AsmDude3 Options page had two critical issues:

1. **UI Element Access Pattern** - Fixed in `AsmDudeOptionPageUI.xaml.cs`:
   - Changed from reflection-based property lookup to `FindName()`
   - Now correctly accesses XAML-defined UI elements
   - Returns actual CheckBox/TextBox/ColorPicker values instead of defaults

2. **Default Settings Initialization** - Added to `AsmDudeOptionsPage.cs`:
   - Ensures settings are properly initialized when Options page loads
   - Calls `EnsureDefaultsInitialized()` in `OnActivate()` method
   - Mirrors AsmDude2's automatic default loading pattern

## Manual Testing Checklist

### Test 1: Open Options Page and Verify Defaults
**Steps:**
1. Launch Visual Studio with asm-dude3-vsix extension
2. Go to `Tools → Options → AsmDude3 → General`
3. Observe the Options page

**Expected Results:**
- ✅ All UI elements load without errors
- ✅ All checkboxes appear checked (enabled):
  - Syntax highlighting enabled
  - Code folding enabled
  - AsmDoc enabled
  - Performance info enabled
  - SkylakeX enabled
  - Code completion enabled
  - Signature help enabled
  - IntelliSense features enabled
- ✅ All color pickers show pre-configured colors:
  - Opcode: Lavender
  - Register: MistyRose
  - Remark: PaleGreen
  - Directive: Thistle
  - Constant: LemonChiffon
  - Jump: LightSteelBlue
  - Label: OldLace
  - Misc: PeachPuff
  - User-defined: Silver (3 levels)
- ✅ Assembler selection shows "Auto Detect" selected

### Test 2: Change a Setting and Verify Detection
**Steps:**
1. Open Options page (Tools → Options → AsmDude3 → General)
2. Uncheck "Enable syntax highlighting"
3. Watch the Debug Output window
4. Click OK to save

**Expected Results:**
- ✅ Debug output shows setting changed: `SyntaxHighlighting_On: old = True; new = False`
- ✅ Change is properly detected (not false positive)
- ✅ Setting is persisted to registry

### Test 3: Verify Settings Persistence
**Steps:**
1. Make changes and click OK (from Test 2)
2. Reopen `Tools → Options → AsmDude3 → General`
3. Observe the state of the settings

**Expected Results:**
- ✅ User's previous changes persist:
  - Syntax highlighting remains unchecked
  - All other settings remain as previously set
- ✅ Settings are NOT reset to defaults

### Test 4: Test Property Isolation
**Steps:**
1. Open Options page
2. Change multiple different settings:
   - Uncheck "Enable code folding"
   - Uncheck "Enable AsmDoc"
   - Keep "Syntax highlighting" checked
3. Verify each setting independently
4. Click OK

**Expected Results:**
- ✅ Each property maintains its own value
- ✅ Changes to one property don't affect others
- ✅ Debug output correctly reports each change

### Test 5: Test Color Selection
**Steps:**
1. Open Options page
2. Click on the color picker for "Mnemonic"
3. Select a different color (e.g., Red)
4. Click OK

**Expected Results:**
- ✅ Color change is detected and reported
- ✅ Color is persisted to registry
- ✅ Syntax highlighting in editor updates with new color

### Test 6: Test Assembler Selection
**Steps:**
1. Open Options page
2. Change "Main Window" assembler from "Auto Detect" to "Intel Masm"
3. Click OK
4. Reopen Options page

**Expected Results:**
- ✅ Selection changes are detected
- ✅ "Intel Masm" remains selected after reopening
- ✅ LSP server receives notification of assembler change

### Test 7: Test Reset to Defaults Behavior
**Steps:**
1. Make several setting changes and save (click OK)
2. Close Visual Studio completely
3. Delete the registry keys for asm-dude3 settings:
   - Path: `HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\[version]\Profiles\_[timestamp]\UserSettings\[...]\AsmDude3`
4. Reopen Visual Studio with asm-dude3-vsix

**Expected Results:**
- ✅ Options page shows defaults again
- ✅ All key features are enabled by default
- ✅ Colors are pre-configured correctly

## Automated Test Structure

Due to the VSIX architecture using internal types and application domains, traditional unit tests aren't practical. However, the main test patterns would be:

### Unit Tests for GetPropValue/SetPropValue

```csharp
[Fact]
public void TestGetPropValue_CheckBox_ReturnsActualValue()
{
    // Arrange
    var ui = new AsmDudeOptionsPageUI();
    var checkBox = ui.FindName("SyntaxHighlighting_On_UI") as CheckBox;
    checkBox.IsChecked = true;

    // Act
    var value = ui.GetPropValue("SyntaxHighlighting_On");

    // Assert
    Assert.Equal(true, value);
}

[Fact]
public void TestSetPropValue_CheckBox_SetsCorrectly()
{
    // Arrange
    var ui = new AsmDudeOptionsPageUI();

    // Act
    ui.SetPropValue("SyntaxHighlighting_On", false);

    // Assert
    var checkBox = ui.FindName("SyntaxHighlighting_On_UI") as CheckBox;
    Assert.False(checkBox.IsChecked);
}
```

### Property Isolation Tests

```csharp
[Fact]
public void TestPropertyIsolation_ChangesIndependent()
{
    // Arrange
    var ui = new AsmDudeOptionsPageUI();

    // Act - Set different values
    ui.SetPropValue("SyntaxHighlighting_On", true);
    ui.SetPropValue("CodeFolding_On", false);

    // Assert - Each maintains its own value
    Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_On"));
    Assert.False((bool)ui.GetPropValue("CodeFolding_On"));
}
```

### FindName vs Reflection Pattern Verification

```csharp
[Fact]
public void TestFindNameLocatesXAMLElements()
{
    // This verifies the fix: FindName works correctly
    var ui = new AsmDudeOptionsPageUI();

    // All XAML-defined elements should be locatable
    var elements = new[]
    {
        "SyntaxHighlighting_On_UI",
        "CodeFolding_On_UI",
        "AsmDoc_On_UI",
        "PerformanceInfo_On_UI",
        "AsmDoc_Url_UI",
        "Global_MaxFileLines_UI"
    };

    foreach (var name in elements)
    {
        var element = ui.FindName(name);
        Assert.NotNull(element);
    }
}
```

## Integration Testing via LSP Server

The most reliable way to test Options page changes is through the LSP server:

### Test via LSP Communication

1. Start the asm-dude3-server directly
2. Send LSP messages to test configuration changes
3. Verify the LSP server responds with updated behavior

Example test scenario:
```
1. Initialize LSP server
2. Send textDocument/didOpen with assembly file
3. Disable syntax highlighting via Options
4. Send textDocument/didChange
5. Verify semantic tokens response is empty (no syntax highlighting)
6. Re-enable syntax highlighting
7. Verify semantic tokens response contains highlighting
```

## Build and Run Tests

### Option 1: Manual Testing
Simply build and run the extension:
```bash
dotnet build VS/CSHARP/asm-dude3/asm-dude3-vsix/asm-dude3-vsix.csproj
```
Then press F5 in Visual Studio to launch experimental instance with the extension.

### Option 2: Minimal Unit Tests (Framework Independent)
Create test utilities that don't require the full VSIX infrastructure:

```csharp
public class UIElementAccessPatternTests
{
    [Fact]
    public void VerifyFindNamePattern()
    {
        // Test the core pattern without VSIX dependencies
        var ui = new AsmDudeOptionsPageUI();
        var result = ui.FindName("SyntaxHighlighting_On_UI");
        Assert.NotNull(result);
    }
}
```

## Key Test Scenarios to Cover

| Scenario | Priority | Type | Status |
|----------|----------|------|--------|
| Open Options page loads UI correctly | Critical | Manual | ✅ Verified |
| Default settings are displayed | Critical | Manual | ✅ Verified |
| GetPropValue returns actual values | Critical | Code Review | ✅ Fixed |
| SetPropValue updates UI correctly | Critical | Code Review | ✅ Fixed  |
| Settings changes are detected | High | Manual | ✅ Verified |
| Settings persist to registry | High | Manual | ✅ Verified |
| Color pickers work | High | Manual | ⏳ TBD |
| Assembler selection works | High | Manual | ⏳ TBD |
| Property isolation maintained | Medium | Code Review | ✅ Fixed |
| Error handling for missing properties | Medium | Code Review | ✅ Implemented |

## Files Modified for Testing

### AsmDudeOptionPageUI.xaml.cs (Lines 91-268)
- **GetPropValue()** - Uses FindName() instead of reflection
- **SetPropValue()** - Uses FindName() instead of reflection
- **GetDefaultValueForProperty()** - Returns sensible defaults

### AsmDudeOptionsPage.cs (Line 294, Lines 495-532)
- **OnActivate()** - Calls EnsureDefaultsInitialized()
- **EnsureDefaultsInitialized()** - New method to trigger default initialization

## Troubleshooting Failed Tests

### Symptoms: "Settings property not found"
**Cause:** Property doesn't exist in Settings.Designer.cs
**Fix:** Add property to Settings.Designer.cs with DefaultSettingValueAttribute

### Symptoms: "UI returns False for all values"
**Cause:** FindName() not finding XAML element
**Fix:** Verify x:Name in XAML matches property name + "_UI"

### Symptoms: "Settings not persisting"
**Cause:** settings.Save() not called
**Fix:** Verify OnApply calls Settings.Default.Save()

### Symptoms: "Changes not detected"
**Cause:** GetPropValue still using old reflection pattern
**Fix:** Use FindName() implementation from fixed code

## Regression Prevention

Once these tests pass:

1. **Monitor debug output** for any null reference exceptions when opening Options page
2. **Check build output** for compilation warnings related to reflection
3. **Verify on each build** that the Options page still loads successfully
4. **Test after each change** to Settings.Designer.cs to ensure new properties work

## References

- **File:** `UI_ELEMENT_ACCESS_FIX.md` - Details of the FindName vs Reflection fix
- **File:** `DEFAULT_SETTINGS_INITIALIZATION.md` - Details of defaults initialization
- **XAML Location:** `VS/CSHARP/asm-dude3/asm-dude3-vsix/OptionsPage/AsmDudeOptionPageUI.xaml`
- **Code-Behind:** `VS/CSHARP/asm-dude3/asm-dude3-vsix/OptionsPage/AsmDudeOptionPageUI.xaml.cs`
- **Options Logic:** `VS/CSHARP/asm-dude3/asm-dude3-vsix/OptionsPage/AsmDudeOptionsPage.cs`
- **Settings:** `VS/CSHARP/asm-dude3/asm-dude3-vsix/Settings.Designer.cs`
