# AsmDude3 Default Settings Configuration

## Changes Applied

Following AsmDude2's proven defaults and user requirements, the following settings have been enabled by default in AsmDude3:

### ✅ Syntax Highlighting & UI Features (ENABLED by default)
| Setting | Default | Purpose |
|---------|---------|---------|
| **SyntaxHighlighting_On** | `True` | Enable assembly syntax highlighting |
| **CodeFolding_On** | `True` | Enable code folding regions |
| **AsmDoc_On** | `True` | Enable documentation links |
| **SignatureHelp_On** | `True` | Enable signature help in IntelliSense |
| **CodeCompletion_On** | `True` | Enable code completion |

### ✅ Performance Information & Latency (ENABLED by default)
| Setting | Default | Purpose |
|---------|---------|---------|
| **PerformanceInfo_On** | `True` | **Enable performance metrics display** |
| **PerformanceInfo_SkylakeX_On** | `True` | **Enable Skylake-X latency data (ThroughPut)** |

### ✅ Assembly Flavor Auto-detection (ENABLED by default)
| Setting | Default | Purpose |
|---------|---------|---------|
| **useAssemblerAutoDetect** | `True` | **Auto-detect assembly flavor (MASM/NASM/ATT)** |
| **useAssemblerMasm** | `True` | Support MASM syntax |
| **useAssemblerNasm** | `False` | Don't default to NASM (auto-detect instead) |

### ℹ️ IntelliSense Features (ENABLED by default)
| Setting | Default | Purpose |
|---------|---------|---------|
| **IntelliSense_Label_Analysis_On** | `True` | Analyze labels in code |
| **IntelliSense_Show_Undefined_Labels** | `True` | Show undefined labels |
| **IntelliSense_Show_Undefined_Includes** | `True` | Show undefined includes |
| **IntelliSense_Decorate_Undefined_Labels** | `True` | Highlight undefined labels |
| **IntelliSense_Decorate_Clashing_Labels** | `True` | Highlight label conflicts |

### 🏗️ Syntax Highlighting Colors (Pre-configured)
All standard syntax highlighting colors are configured with readable defaults:
- **Opcode**: Lavender
- **Register**: MistyRose
- **Remark/Comment**: PaleGreen (italicized)
- **Directive**: Thistle
- **Jump**: LightSteelBlue
- **Label**: OldLace
- **Constant**: LemonChiffon
- **Misc**: PeachPuff
- **User-defined**: Silver (3 levels)

### ℹ️ Architecture Support (Modern processors enabled by default)
| Architecture | Default | Notes |
|---|---|---|
| x86/x64 | ✅ Enabled | 8086-486, MMX, P6, X64 |
| SSE/AVX | ✅ Enabled | SSE through AVX2 |
| AVX-512 | ✅ Enabled | Core features (F, VL, DQ, BW, CD) |
| Modern Extensions | ✅ Enabled | BMI, FMA, ADX, AES, SHA, RTM, etc. |
| Legacy | ❌ Disabled | 3DNow, CYRIX, IA64 |
| Experimental | ❌ Disabled | ER, PF, GFNI, etc. |

## Key Features Now Active by Default

### 1. **Syntax Highlighting** ✅
- Colorized assembly code with consistent, readable colors
- Italics for comments and remarks
- Full support for all assembly syntax elements

### 2. **Latency/Throughput Information** ✅
- Performance metrics for Skylake-X processors
- Shows instruction cycle counts and throughput data
- Helps optimize code for modern CPUs
- Collapsible by default to avoid clutter

### 3. **Code Folding** ✅
- Fold/unfold code regions (marked with `#region`/`#endregion`)
- Improves code navigation in large assembly files

### 4. **Auto-detect Assembly Flavor** ✅
- Automatically detects MASM, NASM (Intel), or NASM (AT&T) syntax
- No need to manually select assembler type
- Seamless support for mixed-syntax files

### 5. **Documentation Links** ✅
- Hover over mnemonics to see documentation
- Clickable links to instruction references
- Default URL: https://github.com/HJLebbink/asm-dude/wiki/

### 6. **IntelliSense/Code Completion** ✅
- Auto-complete for mnemonics and registers
- Label analysis and detection
- Undefined/clashing label warnings

## Settings File Structure

### File Location
```
VS/CSHARP/asm-dude3/asm-dude3-vsix/Settings.Designer.cs
```

### Total Settings Defined
- **Total Properties**: 195 (in PropertyEnum)
- **Defined in Settings**: 71 (auto-generated from Visual Studio settings designer)
- **Architecture Entries**: 65+ (8086 through latest Intel/AMD)

### Sample Default Values
```csharp
[DefaultSettingValueAttribute("True")]
public bool SyntaxHighlighting_On { get; set; }

[DefaultSettingValueAttribute("True")]
public bool CodeFolding_On { get; set; }

[DefaultSettingValueAttribute("True")]
public bool PerformanceInfo_SkylakeX_On { get; set; }

[DefaultSettingValueAttribute("True")]
public bool useAssemblerAutoDetect { get; set; }
```

## Build Status
✅ **Build Succeeded**
- Errors: 0
- Warnings: 54 (non-critical nullability warnings)
- Build Time: ~2.6 seconds

## How to Verify Settings

### Visual Studio Options Page
```
Tools → Options → AsmDude3 → General
```

You'll see:
1. ✅ Syntax highlighting controls with colors enabled
2. ✅ Performance info section (SkylakeX enabled)
3. ✅ Code folding enabled
4. ✅ Assembly auto-detect selected
5. ✅ All syntax highlighting colors configured

### Debug Output (on extension load)
```
AsmDudeOptionsPageUI: InitializeComponent invoked successfully
AsmDudeOptionsPageUI: Found version_UI label
AsmDudeOptionsPageUI: Set version info to X.X.X.X/X.X.X.X
```

## Comparison with AsmDude2

These defaults match the proven, user-tested settings from AsmDude2, ensuring:
- Consistent user experience
- Sensible out-of-box configuration
- Performance data available (SkylakeX latency)
- Automatic assembly flavor detection
- Clean, readable syntax highlighting

## Notes

1. **Performance Info Enabled**: Unlike some settings that default to False, PerformanceInfo_SkylakeX_On defaults to **True** for automatic throughput data display
2. **Auto-detect Active**: Assembly flavor auto-detection is **enabled by default** - users don't need to manually select MASM, NASM, or ATT syntax
3. **Settings Persistence**: All settings are stored in Windows registry (per user) and persist across VS sessions
4. **Graceful Degradation**: If a setting isn't yet defined in Settings.Designer.cs, the Options page silently skips it (no crashes)

## Future Enhancements

- [ ] Populate remaining 124 settings in Settings.Designer.cs
- [ ] Add settings validation (e.g., numeric ranges)
- [ ] Implement settings profiles (Quick/Balanced/Performance)
- [ ] Add settings import/export
- [ ] Create settings presets for different architectures
