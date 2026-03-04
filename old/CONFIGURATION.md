# AsmDude3 Configuration Guide

Complete guide to customizing AsmDude3 settings for your workflow.

**Table of Contents**
- [Accessing Options](#accessing-options)
- [Syntax Highlighting](#syntax-highlighting-settings)
- [Assembler Selection](#assembler-selection)
- [Architecture Configuration](#architecture-configuration)
- [Code Organization](#code-organization-settings)
- [Performance Settings](#performance-settings)
- [Advanced Options](#advanced-options)
- [Resetting to Defaults](#resetting-to-defaults)

---

## Accessing Options

### Open AsmDude3 Options Page

All configuration is in one place:

1. **Tools → Options** (or press `Tools` menu)
2. In left panel, find **AsmDude3**
3. Click to expand and see all sections

### Option Sections

You'll see these sections in the AsmDude3 options tree:

- **Syntax Highlighting** - Color and style settings
- **Assembly Flavour** - Assembler selection (MASM/NASM/etc.)
- **Architectures** - CPU feature flags (SSE, AVX, AVX-512, etc.)
- **Code Folding** - Region folding settings
- **Code Completion** - Coming soon
- **IntelliSense** - Smart analysis options
- **Global** - General performance settings
- **Advanced** - Expert-level configuration

---

## Syntax Highlighting Settings

### Available Token Types

AsmDude3 can color-code 12 different token types:

| Token | Description | Example | Default Color |
|-------|---|---|---|
| **Opcode** | CPU instruction mnemonics | MOV, ADD, JMP | Blue |
| **Register** | CPU registers | RAX, RBX, XMM0 | Green |
| **Remark** | Comments | `; comment` | Gray |
| **Directive** | Assembly directives | PROC, SECTION | Purple |
| **Jump Label** | Labels for jumps | `loop_start:` | Teal |
| **Constant** | Numbers and literals | 0xFF, 42 | Red |
| **Misc** | Other tokens | | Default |
| **User Defined 1** | Reserved for custom | | Custom |
| **User Defined 2** | Reserved for custom | | Custom |
| **User Defined 3** | Reserved for custom | | Custom |

### Customize Colors

#### Step 1: Open Options
1. **Tools → Options**
2. Expand **AsmDude3**
3. Click **Syntax Highlighting**

#### Step 2: Click Color Button
Next to each token type, click the **color picker button**:
- `Opcode Color`
- `Register Color`
- `Remark Color`
- etc.

#### Step 3: Select Color
A color picker dialog appears:
1. Choose color from palette or custom color picker
2. Click **OK**
3. Color preview updates in options dialog

#### Step 4: Apply Changes
1. Click **OK** at bottom of Options dialog
2. Open or reopen `.asm` files to see new colors

### Customize Styling (Italic/Bold)

Make specific tokens italic or bold for emphasis:

1. **Tools → Options → AsmDude3 → Syntax Highlighting**
2. Look for checkbox options:
   - `Opcode_Italic` - Make mnemonics italic
   - `Register_Italic` - Make registers italic
   - `Remark_Italic` - Make comments italic
   - `Directive_Italic` - Make directives italic
   - `Label_Italic` - Make labels italic
   - `Constant_Italic` - Make constants italic
3. Check to enable, uncheck to disable
4. Click **OK**

### Default Color Schemes

| Scheme | Light Theme | Dark Theme |
|--------|---|---|
| **Opcode** | Navy Blue | Light Blue |
| **Register** | Dark Green | Light Green |
| **Remark** | Gray | Light Gray |
| **Directive** | Purple | Light Purple |
| **Label** | Teal | Light Teal |
| **Constant** | Red | Orange |

**Note**: These defaults automatically adapt to your VS theme (Light/Dark mode).

---

## Assembler Selection

### What is Assembler Selection?

AsmDude3 needs to know which assembler syntax to use for proper highlighting:
- **MASM** - Microsoft Assembler (Intel syntax)
- **NASM Intel** - Netwide Assembler Intel syntax
- **NASM AT&T** - Netwide Assembler AT&T/GCC syntax

### Auto-Detection (Default & Recommended)

AsmDude3 automatically detects assembler syntax by analyzing file content:

| Detected Syntax | Looks For | Example |
|---|---|---|
| **MASM** | `PROC`, `ENDP`, `.code` keywords | Modern MASM code |
| **NASM Intel** | `section`, `global`, Intel format | Standard NASM code |
| **NASM AT&T** | AT&T syntax like `%register`, `#` comments | GCC assembly |

**Advantages**:
- ✅ No configuration needed
- ✅ Works with mixed projects
- ✅ Automatic for new files
- ✅ Learns your style

**How accurate?** Analyzes first **40 lines** of file. Very reliable (>95% accuracy).

### Manual Assembly Selection

For cases where auto-detection fails or you need specific syntax:

#### Step 1: Open Options
1. **Tools → Options**
2. Expand **AsmDude3**
3. Click **Assembly Flavour**

#### Step 2: Disable Auto-Detection
1. Uncheck **Auto-Detect Assembler**
2. New options appear

#### Step 3: Select Assembler
Choose ONE (radio button):
- **MASM** - Force Microsoft Assembler
- **NASM Intel** - Force NASM Intel syntax
- **NASM AT&T** - Force NASM AT&T syntax
- **Auto-Detect Disassembly** - For debugger output

#### Step 4: Apply
1. Click **OK**
2. Close and reopen `.asm` files
3. New syntax highlighting applies

### When to Use Manual Selection

**Use Manual Selection if:**
1. Auto-detection guesses wrong assembler
2. Your file is ambiguous (could be either syntax)
3. You have a project standard you want to enforce
4. You're working with disassembly output

**Keep Auto-Detection if:**
1. You want zero configuration
2. You have mixed MASM/NASM files
3. Files follow standard conventions

---

## Architecture Configuration

### What are Architecture Flags?

Architecture flags enable/disable which CPU instruction sets are recognized:

- When enabled → Instructions recognized and highlighted
- When disabled → Instructions treated as undefined words

### Available Architectures

| Category | Architectures |
|---|---|
| **Base** | 8086, 186, 286, 386, 486, Pentium, P6, x64 |
| **SSE** | SSE, SSE2, SSE3, SSSE3, SSE4.1, SSE4.2, SSE4A, SSE5 |
| **AVX** | AVX, AVX2, 14 AVX-512 variants |
| **AMD** | 3DNOW, TBM, CYRIX |
| **Crypto** | AES, SHA, PCLMULQDQ |
| **Other** | FMA, BMI1, BMI2, LZCNT, and many more |

### Why Disable Architecture Flags?

**Use Cases**:
1. **Legacy Code** - Working with 8086/286 code, disable modern extensions
2. **Embedded Systems** - Targeting specific CPU, disable irrelevant features
3. **Validation** - Ensure code only uses allowed instructions
4. **Reduce Noise** - Fewer unrecognized words = cleaner highlighting

### Configure Architecture Support

#### Step 1: Open Options
1. **Tools → Options**
2. Expand **AsmDude3**
3. Scroll down to **Architectures**

#### Step 2: Enable/Disable Each Architecture
You'll see a LONG list of checkboxes:
- ✅ Checked = Architecture enabled
- ☐ Unchecked = Architecture disabled

Examples:
- `ARCH_8086` - Intel 8086
- `ARCH_X64` - x86-64
- `ARCH_SSE` through `ARCH_AVX512_*` - SIMD extensions

#### Step 3: Choose Your Configuration

**Modern 64-bit Code** (Recommended default):
- ✅ ARCH_X64
- ✅ All SSE variants
- ✅ All AVX variants
- ✅ All AVX-512 variants
- ✅ All modern extensions (BMI, FMA, etc.)

**32-bit Legacy Code**:
- ✅ ARCH_386 through ARCH_PENT (Pentium)
- ✅ ARCH_P6
- ✅ SSE, SSE2 only (disable SSE3+)
- ❌ AVX and AVX-512

**Embedded/8086 Code**:
- ✅ ARCH_8086
- ✅ ARCH_186
- ✅ ARCH_286
- ❌ All modern extensions

**Mixed/Unknown Codebase**:
- ✅ All enabled (default)

#### Step 4: Apply
1. Click **OK**
2. Close and reopen `.asm` files
3. Highlighting reflects architecture changes

### Bulk Configure

**Enable All Architectures**:
1. Open Architectures section
2. Look for "Enable All" button (if available)
3. Click to check all boxes

**Disable All Architectures**:
1. Open Architectures section
2. Look for "Disable All" button (if available)
3. Click to uncheck all boxes

Then manually enable only what you need.

---

## Code Organization Settings

### Code Folding

Code folding allows collapsing code regions for readability.

#### Enable/Disable Folding
1. **Tools → Options → AsmDude3 → Code Folding**
2. Check **Enable Code Folding** to turn on
3. Uncheck to disable folding UI
4. Click **OK**

#### Fold Region Tags (MASM)
Define custom fold regions with tags:

```asm
; <region name="MyFunction">
my_function PROC
    ; function code
my_function ENDP
; </region>
```

Configuration:
1. **Tools → Options → AsmDude3 → Code Folding**
2. Set **Begin Tag** (default: `<region`)
3. Set **End Tag** (default: `</region>`)
4. Click **OK**

---

## Performance Settings

### Maximum File Size

Configure how AsmDude3 handles large files:

| Setting | What It Does | Default |
|---------|---|---|
| **Max File Lines** | Files larger than this use simplified highlighting | 50,000 lines |

#### Why This Matters

| File Size | Behavior |
|---|---|
| < 10,000 lines | Full highlighting, instant |
| 10,000-50,000 | Full highlighting, fast |
| > 50,000 | Simplified highlighting, optimized |

#### Configure Limit
1. **Tools → Options → AsmDude3 → Global**
2. Change **Max File Lines** value
3. Lower = More aggressive simplification
4. Higher = More accurate but slower on huge files
5. Click **OK**

**Recommendation**: Keep default (50,000) unless you work with very large files.

### Memory Usage Monitoring

Check how much memory AsmDude3 is using:
1. **Help → About Microsoft Visual Studio**
2. Click **Copy Info**
3. Paste in Notepad
4. Search for "AsmDude3"
5. Shows extension memory usage

---

## Advanced Options

### Performance Data (Microarchitecture Info)

Show detailed performance information for instructions:

1. **Tools → Options → AsmDude3 → Advanced**
2. Check **Performance Data On** to enable
3. Select microarchitectures to analyze:
   - Sandy Bridge
   - Ivy Bridge
   - Haswell
   - Broadwell
   - Skylake
   - Skylake X
   - Knights Landing
4. Click **OK**

**Note**: This is an advanced feature that shows CPU-specific performance characteristics.

### IntelliSense Features

Smart code analysis features (mostly future):

1. **Tools → Options → AsmDude3 → IntelliSense**
2. Available options:
   - **Label Analysis On** - Analyzes label usage
   - **Show Undefined Labels** - Highlights undefined labels
   - **Decorate Undefined Labels** - Adds visual indication
   - Similar options for clashing labels and includes
3. Click **OK**

### AsmSim (Assembly Simulator)

Experimental Z3-based simulation:

1. **Tools → Options → AsmDude3 → Advanced**
2. Check **AsmSim On** to enable
3. Configure Z3 settings:
   - **Z3 Timeout (ms)** - How long solver can run
   - **Number of Threads** - CPU threads to use
   - Other simulation options
4. Click **OK**

**Warning**: AsmSim is experimental. Use only if you understand Z3 constraints.

---

## Resetting to Defaults

### Reset All Settings

If you've made many changes and want to start over:

1. **Tools → Options**
2. Right-click **AsmDude3** in tree
3. Click **Reset Page** (if available)
4. Confirm reset
5. Click **OK**

**Effect**: All AsmDude3 options return to factory defaults.

### Reset Just One Section

Some sections may have individual reset options:

1. **Tools → Options → AsmDude3 → [Section Name]**
2. Look for **Reset** button
3. Click to reset just that section
4. Click **OK** to save

---

## Configuration Profiles

### Save Your Configuration

Unfortunately, AsmDude3 doesn't support named profiles yet. But you can:

1. Write down your settings in Notepad
2. Export via Tools → Options → "Export Settings..."
3. Import on another machine via Tools → Options → "Import Settings..."

**Future Feature**: Named configuration profiles (Phase 3+)

---

## Keyboard Shortcuts for Configuration

| Task | Shortcut |
|---|---|
| **Open Tools → Options** | Alt+T, O |
| **Search Options** | Type in search box (when Options open) |
| **Apply Changes** | OK button (applies all sections) |

---

## Configuration Examples

### Example 1: Dark Theme Optimization

For dark VS theme, optimize colors:

1. **Tools → Options → AsmDude3 → Syntax Highlighting**
2. Set Opcode Color → Light Blue (bright enough for dark background)
3. Set Register Color → Light Green
4. Set Constant Color → Orange (not pure red, easier on eyes)
5. Enable italic styles for better distinction
6. Click **OK**

### Example 2: Strict 32-bit Code Validation

For 32-bit codebase that should never use 64-bit instructions:

1. **Tools → Options → AsmDude3 → Architectures**
2. Keep only:
   - ✅ ARCH_386, ARCH_486, ARCH_PENT, ARCH_P6
   - ✅ ARCH_SSE, ARCH_SSE2 only
   - ❌ ARCH_X64 (disabled)
   - ❌ All AVX/AVX-512 (disabled)
3. Click **OK**
4. Any 64-bit instructions will appear as errors

### Example 3: NASM Strict Enforcement

Force NASM syntax on all files:

1. **Tools → Options → AsmDude3 → Assembly Flavour**
2. Uncheck **Auto-Detect Assembler**
3. Select **NASM Intel** (or NASM AT&T)
4. Click **OK**

Now all files use NASM syntax highlighting regardless of content.

### Example 4: Performance Optimization for Large Project

For large codebase with many 50,000+ line files:

1. **Tools → Options → AsmDude3 → Global**
2. Set **Max File Lines** → 30,000 (more aggressive)
3. **Tools → Options → AsmDude3 → Architectures**
4. Disable unnecessary architectures (keep only what's used)
5. Click **OK**

This speeds up highlighting on huge files.

---

## Troubleshooting Configuration

### Settings Don't Apply

**Problem**: Changed setting but no effect visible.

**Solutions**:
1. Click **OK** (not just Apply) to save
2. Close all `.asm` files
3. Reopen `.asm` files
4. If still no change, restart Visual Studio

### Color Picker Won't Open

**Problem**: Clicking color button does nothing.

**Solutions**:
1. Restart Visual Studio
2. Open Options again
3. Try clicking color button again
4. If problem persists, reinstall extension

### Assembler Auto-Detection Keeps Failing

**Problem**: Extension detects wrong assembler repeatedly.

**Solutions**:
1. Help the auto-detector by adding identifying line:
   - For MASM: Add `PROC` keyword early in file
   - For NASM: Add `section .text` early in file
2. Or disable auto-detection and set manually

### Performance Still Slow Despite Changes

**Problem**: VS still slow even after optimization settings.

**Solutions**:
1. Check file size (`Ctrl+G` → go to end, see line number)
2. If > 50,000 lines, split into smaller files
3. Try lower **Max File Lines** setting
4. Disable more architectures
5. Close other extensions (check View → Extensions)

---

## Summary: Common Configurations

| Use Case | Recommended Settings |
|---|---|
| **General User** | Default settings (auto-detect, all architectures) |
| **Dark Theme** | Adjust colors for dark background, enable italics |
| **Legacy Codebase** | Disable modern architectures (no AVX-512) |
| **Performance** | Lower Max File Lines, disable unused architectures |
| **Strict Validation** | Manual assembler selection, disable advanced architectures |
| **Development** | Enable all options for maximum feedback |

---

**Need more help?** See [USER_GUIDE.md](USER_GUIDE.md) or [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
