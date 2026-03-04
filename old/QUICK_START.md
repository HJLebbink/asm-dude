# AsmDude3 Quick Start Guide

Get AsmDude3 up and running in **5 minutes**. This guide covers installation, activation, and your first assembly file.

## Prerequisites

Before installing AsmDude3, ensure you have:

- ✅ **Visual Studio 2022** (version 17.0 or later) OR **Visual Studio 2026** (version 18.0 or later)
- ✅ **.NET 10.0 SDK** or later (automatically included with Visual Studio)
- ✅ **Administrator access** to install extensions

## Installation

### Step 1: Install from Visual Studio Marketplace

1. Open **Visual Studio 2022 or 2026**
2. Go to **Extensions → Manage Extensions**
3. Search for **"AsmDude3"**
4. Click **Download** on the AsmDude3 extension
5. Visual Studio will prompt to close and install the extension
6. Click **Modify** to complete the installation
7. Visual Studio will restart

**Installation Time**: ~2-3 minutes

### Step 2: Verify Installation

After Visual Studio restarts:

1. Go to **Tools → Options → AsmDude3**
2. You should see the AsmDude3 options page
3. If visible, installation was successful ✅

## Your First Assembly File

### Step 1: Create an Assembly File

1. **Create a new file** in Visual Studio:
   - **File → New → File**
   - Or press **Ctrl+N**

2. **Save the file** with an `.asm` extension:
   - Name it: `hello.asm`
   - Save to your project folder

### Step 2: Add Assembly Code

Copy and paste one of these examples based on your assembler preference:

#### Example 1: MASM (Microsoft Assembler)

```asm
; MASM Example - Simple addition
.code
main PROC
    mov rax, 10        ; Load 10 into RAX
    mov rbx, 20        ; Load 20 into RBX
    add rax, rbx       ; Add RAX + RBX
    ret                ; Return
main ENDP
end
```

#### Example 2: NASM Intel Syntax

```asm
; NASM Intel Syntax Example
section .text
    global main
main:
    mov rax, 10        ; Load 10 into RAX
    mov rbx, 20        ; Load 20 into RBX
    add rax, rbx       ; Add RAX + RBX
    ret                ; Return
```

#### Example 3: NASM AT&T Syntax

```asm
# NASM AT&T Syntax Example
.section .text
    .globl main
main:
    movq $10, %rax     # Load 10 into RAX
    movq $20, %rbx     # Load 20 into RBX
    addq %rbx, %rax    # Add RBX to RAX
    ret                # Return
```

### Step 3: Verify Syntax Highlighting

After pasting code, you should see:

✅ **Mnemonics** (MOV, ADD, RET) colored differently
✅ **Registers** (RAX, RBX) colored differently
✅ **Comments** (lines starting with `;` or `#`) in comment color
✅ **Numbers** (10, 20) colored as constants

**Expected behavior**: Different token types display in different colors based on your theme.

### Step 4: Test Auto-Detection

AsmDude3 automatically detects your assembler syntax:

1. Create three files with these names:
   - `masm_sample.asm` - Paste Example 1 above
   - `nasm_intel.asm` - Paste Example 2 above
   - `nasm_att.asm` - Paste Example 3 above

2. Each file will automatically highlight with the correct syntax
3. No manual configuration needed!

## Customization

### Change Syntax Highlighting Colors

1. Go to **Tools → Options → AsmDude3**
2. Expand **Syntax Highlighting** section
3. Click color buttons next to:
   - Opcode (Mnemonic)
   - Register
   - Remark
   - Directive
   - Constant
   - Label
   - Other types
4. Select your preferred color
5. Click **OK** to save

### Select Assembler (Optional)

If auto-detection doesn't work for your file:

1. Go to **Tools → Options → AsmDude3**
2. Expand **Assembly Flavour** section
3. Select one:
   - **Auto-Detect** (default) - Analyzes file content
   - **MASM** - Force Microsoft Assembler syntax
   - **NASM Intel** - Force NASM Intel syntax
   - **NASM AT&T** - Force NASM AT&T syntax
4. Click **OK**

### Enable/Disable Architectures

By default, all CPU architectures are enabled. To disable specific ones:

1. Go to **Tools → Options → AsmDude3**
2. Expand **Architectures** section
3. Uncheck architectures you don't need:
   - ARCH_8086, ARCH_MMX, ARCH_AVX512_*, etc.
4. Click **OK**

**Why disable?** Reduces false positives if you're working on legacy code targeting specific CPUs.

## Troubleshooting

### Issue: No Syntax Highlighting Appears

**Solution 1: File Extension**
- Ensure file has `.asm` extension
- Rename file if needed (e.g., `myfile.asm`)
- Close and reopen the file

**Solution 2: Restart Visual Studio**
- Close Visual Studio
- Reopen the project
- Open your `.asm` file again

**Solution 3: Check Options**
- Go to **Tools → Options → AsmDude3**
- Verify **Syntax Highlighting: On** is checked
- Click **OK** and try again

### Issue: Colors Don't Match My Theme

**Solution: Reconfigure Colors**
1. Go to **Tools → Options → AsmDude3**
2. Expand **Syntax Highlighting** section
3. Click on each color button and select colors that match your theme
4. Click **OK** to apply

### Issue: Auto-Detection Chooses Wrong Assembler

**Solution: Manually Select Assembler**
1. Go to **Tools → Options → AsmDude3**
2. Expand **Assembly Flavour**
3. Disable "Auto-Detect"
4. Select the correct assembler manually
5. Click **OK**

**Or: Force correct syntax by naming**
- MASM files: Use `.asm` with MASM-specific syntax
- NASM files: Use `.asm` with NASM-specific syntax
- Let auto-detect recognize the syntax

### Issue: LSP Server Won't Start

**Solution 1: Restart Extension**
- Go to **Tools → Command Palette** (Ctrl+Shift+P)
- Type "Restart Language Server"
- Select the command
- Wait 5 seconds for server to restart

**Solution 2: Clear Cache**
- Close Visual Studio
- Delete: `%TEMP%\AsmDude3` folder (if exists)
- Reopen Visual Studio

**Solution 3: Reinstall Extension**
- Go to **Extensions → Manage Extensions**
- Search for AsmDude3
- Click the three dots (⋮)
- Click **Uninstall**
- Restart Visual Studio
- Reinstall from Marketplace

## Next Steps

Now that AsmDude3 is installed and working:

1. **Learn more features** → Read [USER_GUIDE.md](USER_GUIDE.md)
2. **Customize colors** → See [CONFIGURATION.md](CONFIGURATION.md)
3. **Need help?** → Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
4. **Want to contribute?** → See [CONTRIBUTING.md](CONTRIBUTING.md)

## What's Next in AsmDude3

**Currently Available (Phase 5 Complete)**:
- ✅ Syntax highlighting for MASM, NASM Intel, NASM AT&T
- ✅ Code folding (procedures, sections)
- ✅ 56+ CPU architectures supported
- ✅ 157 customizable settings
- ✅ Auto-detection of assembler syntax
- ✅ Disassembly output support

**Coming Soon (Phase 3+)**:
- 🔄 Code completion with smart suggestions
- 🔄 Hover tooltips with instruction info
- 🔄 Signature help for instruction operands
- 🔄 Advanced code analysis

## Getting Help

- **Issue not in this guide?** → See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Want to learn more?** → Read [USER_GUIDE.md](USER_GUIDE.md)
- **Found a bug?** → Report on GitHub Issues
- **Have a feature request?** → Suggest on GitHub Discussions

## Quick Reference

| Task | Steps |
|------|-------|
| **Install** | Extensions → Manage Extensions → Search "AsmDude3" → Download → Restart |
| **Create `.asm` file** | File → New → File → Save as `.asm` |
| **Change colors** | Tools → Options → AsmDude3 → Syntax Highlighting → Choose colors → OK |
| **Select assembler** | Tools → Options → AsmDude3 → Assembly Flavour → Choose assembler → OK |
| **Restart extension** | Tools → Command Palette → "Restart Language Server" |
| **See all features** | Visit [FEATURES.md](FEATURES.md) |

---

**Congratulations!** You've successfully installed AsmDude3. Happy assembly coding! 🎉

For detailed documentation, see [USER_GUIDE.md](USER_GUIDE.md).
