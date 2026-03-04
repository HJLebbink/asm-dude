# AsmDude3 User Guide

Complete reference for using AsmDude3, the modern assembly language extension for Visual Studio 2022/2026.

**Table of Contents**
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Features](#features)
- [Syntax Highlighting](#syntax-highlighting)
- [Code Organization](#code-organization)
- [Configuration](#configuration)
- [Assembler Support](#assembler-support)
- [Architecture Support](#architecture-support)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)
- [Keyboard Shortcuts](#keyboard-shortcuts)

---

## Installation

### System Requirements

- **Operating System**: Windows 10 or later
- **Visual Studio**: 2022 (17.0+) or 2026 (18.0+)
- **.NET Runtime**: .NET 10.0 or later
- **Disk Space**: ~50 MB
- **RAM**: Minimal (< 100 MB for typical projects)

### Install from Visual Studio Marketplace

1. Open Visual Studio 2022 or 2026
2. **Extensions → Manage Extensions**
3. Search for **"AsmDude3"**
4. Click **Download**
5. Close Visual Studio when prompted
6. Click **Modify** to complete installation
7. Visual Studio restarts and extension is ready

**Estimated Time**: 2-3 minutes

### Verify Installation

After restart:
1. **Tools → Options → AsmDude3**
2. You should see the options page
3. Installation is successful ✅

---

## Getting Started

### Creating Your First Assembly File

#### Option 1: New File in Project
1. Right-click project in Solution Explorer
2. **Add → New Item**
3. Choose **Code** or **Text File**
4. Name it `example.asm`
5. Add assembly code (see examples below)

#### Option 2: New File
1. **File → New → File**
2. Save as `example.asm` in your project folder
3. Add assembly code

### Example Assembly Files

#### MASM Example
```asm
; MASM syntax example
INCLUDELIB kernel32.lib

.code
main PROC
    mov eax, 42        ; Set return value
    ret                ; Return from function
main ENDP

end main
```

**Expected Highlighting**:
- `mov`, `ret` → Opcode color
- `eax` → Register color
- `42` → Constant color
- `; ...` → Comment color

#### NASM Intel Example
```asm
; NASM Intel syntax
section .text
    global main

main:
    mov eax, 42        ; Set return value
    ret                ; Return from function
```

#### NASM AT&T Example
```asm
# NASM AT&T syntax
.section .text
    .globl main

main:
    movl $42, %eax     # Set return value
    ret                # Return from function
```

---

## Features

### Syntax Highlighting

AsmDude3 recognizes and color-codes multiple token types across all supported assemblers.

#### Recognized Token Types

| Token | What It Is | Example | Customizable |
|-------|-----------|---------|---|
| **Opcode** | CPU instruction mnemonics | MOV, ADD, JMP, CALL | ✅ Yes |
| **Register** | CPU and vector registers | RAX, RBX, XMM0, YMM1 | ✅ Yes |
| **Remark** | Comments and remarks | `; this is a comment` | ✅ Yes |
| **Directive** | Assembly directives | PROC, SECTION, .globl | ✅ Yes |
| **Label/Jump** | Jump targets | `loop_start:`, `main:` | ✅ Yes |
| **Constant** | Numbers and literals | 0xFF, 0b1010, 42, 3.14 | ✅ Yes |
| **User Defined 1** | Custom (reserved) | Configurable | ✅ Yes |
| **User Defined 2** | Custom (reserved) | Configurable | ✅ Yes |
| **User Defined 3** | Custom (reserved) | Configurable | ✅ Yes |

#### Number Format Recognition

AsmDude3 recognizes multiple number formats:

| Format | Examples | Recognized |
|--------|----------|---|
| **Hexadecimal** | 0xFF, 0x1A2B, FFh, 1A2Bh | ✅ Yes |
| **Binary** | 0b1010, 0b11110000 | ✅ Yes |
| **Decimal** | 10, 255, 1000 | ✅ Yes |
| **Floating Point** | 3.14, 2.0, 1.5e-3 | ✅ Yes |

### Code Folding

Code folding allows collapsing code regions for better readability.

#### Foldable Regions

| Region | Example | Collapses To |
|--------|---------|---|
| **Procedures** | `main PROC ... main ENDP` | `main PROC` |
| **Sections** | `section .text ... end` | `section .text` |
| **Custom Tags** | `; <region>` tags | `; region` |
| **Comments** | Multi-line comments | Comment block |

#### Using Code Folding

- **Collapse region**: Click the `-` icon in the margin
- **Expand region**: Click the `+` icon in the margin
- **Collapse all**: Ctrl+M, Ctrl+O
- **Expand all**: Ctrl+M, Ctrl+X

### Architecture Support

AsmDude3 recognizes 56+ CPU architectures:

#### Base x86/x64
- 8086, 80186, 80286, 80386, 80486
- Pentium, Pentium Pro (P6)
- x86-64 (x64)

#### Vector/SIMD Extensions
- **SSE**: SSE, SSE2, SSE3, SSSE3, SSE4.1, SSE4.2, SSE4A, SSE5
- **AVX**: AVX, AVX2
- **AVX-512**: 14 variants (VL, F, DQ, BW, ER, CD, IFMA, VBMI, VPOPCNTDQ, 4VNNIW, 4FMAPS, VBMI2, VNNI, BITALG, GFNI, VAES, VPCLMULQDQ, BF16, VP2INTERSECT)

#### Specialized Instructions
- **Cryptography**: AES, SHA, PCLMULQDQ
- **BMI**: BMI1, BMI2
- **Other**: FMA, F16C, LZCNT, PREFETCHWT1, ADX, FSGSBASE, HLE, INVPCID, RDPID, RDRAND, RDSEED, XSAVEOPT
- **Virtualization**: VMX, SMX
- **Security**: RTM, MPX, SGX1, SGX2

#### AMD-Specific
- 3DNOW, TBM, CYRIX, CYRIXM

#### Legacy
- IA-64 (reference only), UNDOC (undocumented instructions)

---

## Syntax Highlighting

### Customize Colors

Change syntax highlighting colors to match your theme or preferences.

#### Step 1: Open Options
1. **Tools → Options → AsmDude3**
2. Expand **Syntax Highlighting**

#### Step 2: Select Token Type
Click the color button next to any token type:
- Opcode (Mnemonic)
- Register
- Remark
- Directive
- Jump Label
- Constant
- User Defined 1-3

#### Step 3: Choose Color
1. Click the color picker button
2. Select desired color
3. Click **OK**

#### Step 4: Apply
1. Return to Options dialog
2. Click **OK** to apply all changes
3. Open/reopen your `.asm` files to see new colors

### Toggle Italic/Bold

Make specific token types italic or bold:

1. **Tools → Options → AsmDude3**
2. Expand **Syntax Highlighting**
3. Check boxes for:
   - Opcode_Italic
   - Register_Italic
   - Remark_Italic
   - Other_Italic
4. Click **OK**

### Dark/Light Mode Support

AsmDude3 automatically adapts to your Visual Studio theme:
- Respects Dark Mode colors
- Respects Light Mode colors
- Custom colors override theme defaults

---

## Code Organization

### Working with Labels and Procedures

#### MASM Procedures

```asm
add_numbers PROC
    ; procedure body
    mov eax, ebx
    ret
add_numbers ENDP
```

- **Navigate**: Click label in Code Definition Window (View → Code Definition Window)
- **Find references**: Right-click label → Find All References
- **Go to definition**: Ctrl+Click on label name

#### NASM Labels

```asm
add_numbers:
    mov eax, ebx
    ret
```

- **Navigate**: Same as MASM
- **Find references**: Right-click label → Find All References

### Code Definition Window

**View → Code Definition Window** shows:
- Procedure/function definitions
- Label positions
- Region markers

---

## Configuration

### Assembler Selection

AsmDude3 can auto-detect or manually set your assembler syntax.

#### Auto-Detection (Default)

AsmDude3 analyzes the first 40 lines of your file to determine:
- Is it MASM syntax? (looks for `PROC`, `ENDP`, `.code`)
- Is it NASM Intel? (looks for `section`, `global`, Intel syntax)
- Is it NASM AT&T? (looks for `#` comments, AT&T syntax like `%register`)

**Advantages**:
- Works for mixed projects
- No configuration needed
- Automatic for new files

#### Manual Selection

For files where auto-detection fails:

1. **Tools → Options → AsmDude3**
2. Expand **Assembly Flavour**
3. Uncheck **Auto-Detect**
4. Select one:
   - **MASM** - Force Microsoft Assembler syntax
   - **NASM Intel** - Force NASM Intel syntax
   - **NASM AT&T** - Force NASM AT&T syntax
5. Click **OK**

**When to use manual selection**:
- File is ambiguous (could be either syntax)
- Auto-detection is incorrect
- You want to enforce specific syntax

### Disassembly Output

AsmDude3 recognizes debugger disassembly output:
- Debugger windows
- Copy-pasted disassembly
- Output from external tools

Supported formats:
- ✅ MASM disassembly format
- ✅ GDB/LLDB disassembly format
- ✅ IDA Pro disassembly format

**Configuration**: Tools → Options → AsmDude3 → Assembly Flavour → Disassembly

---

## Architecture Support

### Enable/Disable Architectures

Control which CPU architectures are recognized by AsmDude3.

#### Why Disable Architectures?

- **Older code**: Disable AVX-512 for legacy 8086 code
- **Platform-specific**: Disable features not available on target platform
- **Reduce false positives**: Smaller instruction set = fewer unrecognized words

#### How to Configure

1. **Tools → Options → AsmDude3**
2. Scroll to **Architectures** section
3. Check/uncheck desired architectures:
   - ✅ ARCH_8086 (Intel 8086 - very old)
   - ✅ ARCH_X64 (x86-64 - modern)
   - ✅ ARCH_SSE through ARCH_AVX512_* (SIMD)
   - etc.
4. Click **OK**

#### Recommended Configurations

**Modern Code** (default):
- Enable all architectures

**Legacy Code** (8086/286):
- ARCH_8086
- ARCH_186
- ARCH_286

**32-bit Code**:
- ARCH_386
- ARCH_486
- ARCH_PENT (Pentium)
- ARCH_P6
- ARCH_SSE through ARCH_SSE2

**64-bit Code**:
- ARCH_X64
- ARCH_SSE through ARCH_AVX512_*
- All modern extensions

---

## Performance

### File Size Handling

AsmDude3 automatically optimizes for large files.

| File Size | Behavior | Performance |
|-----------|----------|---|
| < 10,000 lines | Full highlighting | Instant |
| 10,000-50,000 lines | Full highlighting | Fast (< 500ms) |
| 50,000+ lines | Simplified highlighting | Optimized |

#### Configuring Limits

1. **Tools → Options → AsmDude3**
2. Expand **Global**
3. Set **Max File Lines**: (default 50,000)
4. Click **OK**

**Effect**: Files larger than this limit use faster but simpler highlighting.

### Performance Monitoring

Monitor extension performance:

1. **Help → About Microsoft Visual Studio**
2. Click **Copy Info**
3. Paste in Notepad and search for "AsmDude3"
4. Shows:
   - Extension version
   - Load time
   - Memory usage

### Tips for Better Performance

1. **Close unused files** - Reduces memory usage
2. **Use simplified highlighting** - For very large files (> 100,000 lines)
3. **Disable unused architectures** - Reduces processing per file
4. **Restart VS periodically** - Clears memory cache

---

## Advanced Features

### Code Completion (Coming Soon)

Planned for Phase 3+:
- Mnemonic suggestions as you type
- Register name auto-completion
- Directive suggestions
- Smart completion based on context

### Hover Tooltips (Coming Soon)

Planned for Phase 3+:
- Mnemonic documentation
- Register information
- Architecture compatibility
- Hyperlinks to instruction references

### Signature Help (Coming Soon)

Planned for Phase 3+:
- Instruction operand hints
- Valid operand types
- Error detection

---

## Troubleshooting

### No Syntax Highlighting

**Symptom**: Code appears in plain text, no colors.

**Solutions**:
1. Check file extension is `.asm`
2. **Tools → Options → AsmDude3** → Verify "Syntax Highlighting: On"
3. Close and reopen the file
4. Restart Visual Studio
5. Check LSP server status (see below)

### Wrong Colors Applied

**Symptom**: Colors don't match what you configured.

**Solutions**:
1. Clear color cache:
   - Go to **Tools → Options → AsmDude3**
   - Change a color and click OK
   - This forces color re-evaluation
2. Restart Visual Studio
3. Check theme (Dark vs. Light mode)

### Auto-Detection Chooses Wrong Assembler

**Symptom**: MASM code highlighted as NASM or vice versa.

**Solutions**:
1. Disable auto-detection:
   - **Tools → Options → AsmDude3 → Assembly Flavour**
   - Uncheck "Auto-Detect"
   - Manually select correct assembler
2. Or, make your code more obviously the target syntax:
   - Add `PROC`/`ENDP` for MASM
   - Add `section .text` for NASM

### LSP Server Not Responding

**Symptom**: Highlighting suddenly stops, no response to changes.

**Solutions**:
1. **Restart language server**:
   - **View → Command Palette** (Ctrl+Shift+P)
   - Type "Restart Language Server"
   - Press Enter
2. Close and reopen the file
3. Restart Visual Studio
4. Reinstall extension if issue persists

### Performance Issues on Large Files

**Symptom**: Visual Studio slows down with large `.asm` files.

**Solutions**:
1. Increase max file lines limit:
   - **Tools → Options → AsmDude3 → Global**
   - Increase "Max File Lines" value
2. Disable unused architectures:
   - **Tools → Options → AsmDude3 → Architectures**
   - Uncheck unnecessary CPU extensions
3. Split large files into smaller ones
4. Disable highlighting for that file temporarily

### Settings Don't Apply

**Symptom**: Changes in Options don't take effect.

**Solutions**:
1. Click OK to close Options dialog (not just Apply)
2. Close all open `.asm` files
3. Reopen `.asm` files
4. Restart Visual Studio
5. Restart language server (see above)

---

## Keyboard Shortcuts

| Action | Shortcut | Notes |
|--------|----------|-------|
| **Collapse Code Region** | Ctrl+M, Ctrl+C | Collapses current region |
| **Collapse All** | Ctrl+M, Ctrl+O | Collapses all regions |
| **Expand Code Region** | Ctrl+M, Ctrl+E | Expands current region |
| **Expand All** | Ctrl+M, Ctrl+X | Expands all regions |
| **Go to Definition** | Ctrl+Click label | Jump to label definition |
| **Find References** | Right-click → Find All References | Find all uses of label |
| **Open Command Palette** | Ctrl+Shift+P | Access VS commands |
| **Restart Language Server** | Ctrl+Shift+P → "Restart Language Server" | Restart AsmDude3 LSP |

---

## Getting Help

| Need Help With | Where to Find |
|---|---|
| **Installation** | [QUICK_START.md](QUICK_START.md) |
| **Features Overview** | [FEATURES.md](FEATURES.md) |
| **Configuration** | [CONFIGURATION.md](CONFIGURATION.md) |
| **Common Problems** | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| **Architecture** | [ARCHITECTURE.md](ARCHITECTURE.md) |
| **Contributing** | [CONTRIBUTING.md](CONTRIBUTING.md) |

---

## What's Coming Next

**Currently Available**:
- ✅ Syntax highlighting (MASM, NASM Intel, NASM AT&T)
- ✅ Code folding
- ✅ 56+ CPU architectures
- ✅ 157 customizable settings
- ✅ Disassembly support

**Under Development** (Phase 3+):
- 🔄 Code completion
- 🔄 Hover information with docs
- 🔄 Signature help
- 🔄 Advanced code analysis

---

## Tips & Best Practices

### Naming Conventions

1. **Use meaningful label names**:
   - Good: `loop_start:`, `calculate_sum:`, `error_handler:`
   - Poor: `l1:`, `go:`, `x:`

2. **Group related procedures**:
   - MASM: Put related procs in same section
   - NASM: Use comments to group related labels

### Code Organization

1. **Use sections clearly**:
   ```asm
   section .data       ; data
   section .text       ; code
   section .bss        ; uninitialized data
   ```

2. **Comments for clarity**:
   ```asm
   ; Initialize counter
   mov ecx, 10
   ; Loop body
   loop_start:
       ; do something
   ```

### Performance Tips

1. **Keep assembly files under 50,000 lines**
2. **Use fold regions** for better readability
3. **Disable unused architectures** if working with legacy code
4. **Regularly save** for auto-completion suggestions (coming soon)

---

**Need more help?** Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md) or report issues on GitHub.

Happy assembly coding! 🎉
