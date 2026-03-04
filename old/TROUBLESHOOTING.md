# AsmDude3 Troubleshooting Guide

Complete troubleshooting guide for AsmDude3 installation, configuration, and runtime issues.

**Table of Contents**
- [Installation Issues](#installation-issues)
- [Configuration Issues](#configuration-issues)
- [Syntax Highlighting Issues](#syntax-highlighting-issues)
- [Performance Issues](#performance-issues)
- [LSP Server Issues](#lsp-server-issues)
- [Settings & Options Issues](#settings--options-issues)
- [Advanced Issues](#advanced-issues)

---

## Installation Issues

### Issue: Extension Won't Install from Marketplace

**Symptom**: Clicking "Download" in Extensions → Manage Extensions shows error or nothing happens.

**Solutions**:
1. Check internet connection - Marketplace requires active internet
2. Try restarting Visual Studio completely
3. Clear extension cache:
   - Close Visual Studio
   - Delete: `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\ComponentModelCache`
   - Reopen Visual Studio
4. Try manual installation:
   - Download VSIX from GitHub releases
   - Double-click to install
   - Or use: `devenv /install [path-to-vsix]`

### Issue: Installation Completes But Extension Doesn't Load

**Symptom**: Visual Studio restarts but no AsmDude3 in Tools → Options.

**Solutions**:
1. Verify installation location:
   - Check: `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\Extensions\`
   - Should contain AsmDude3 folder
2. Restart Visual Studio again (sometimes needs two restarts)
3. Check for conflicting extensions:
   - Go to **Extensions → Manage Extensions**
   - Look for other assembly-related extensions
   - Disable or uninstall if found
4. Repair Visual Studio:
   - Go to **Programs and Features**
   - Find "Visual Studio 2022"
   - Click "Modify" → "Repair"
   - This takes 10-15 minutes

### Issue: "VSIX Installation Failed" Error

**Symptom**: Error during installation with message about version mismatch.

**Solutions**:
1. Verify Visual Studio version:
   - AsmDude3 requires **VS 2022 (17.0+) or VS 2026 (18.0+)**
   - Older versions (2019, 2017) are not supported
   - Check your version: **Help → About Microsoft Visual Studio**
2. Update Visual Studio:
   - If version < 17.0, update to latest 2022 or 2026
3. Uninstall old AsmDude versions:
   - **Extensions → Manage Extensions** → Search "AsmDude"
   - Uninstall any non-v3 versions
   - Restart Visual Studio
   - Then install AsmDude3

---

## Configuration Issues

### Issue: Options Page Won't Open

**Symptom**: Tools → Options → AsmDude3 is missing or clicking it does nothing.

**Solutions**:
1. Verify extension loaded:
   - Check **Help → About Microsoft Visual Studio** → Copy Info
   - Paste in Notepad, search for "AsmDude3"
   - Should show extension name and version
2. If extension shows but options don't load:
   - Go to **Tools → Command Palette** (Ctrl+Shift+P)
   - Type "Settings: Open User Settings"
   - Search for "asmdude" to verify settings exist
3. Reset to defaults:
   - Close Visual Studio
   - Delete: `%APPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\privateregistry.bin`
   - Reopen Visual Studio (registry will rebuild)

### Issue: Settings Changes Don't Apply

**Symptom**: Changed color/assembler setting but nothing changes in open files.

**Solutions**:
1. Click **OK** (not just Apply):
   - Apply button may not save in some cases
   - Always use OK to ensure save
2. Close and reopen assembly files:
   - Settings apply to newly opened files
   - Already-open files keep old settings
3. Restart Visual Studio:
   - Some settings require full restart
   - Close all tools and reopen
4. Clear LSP cache:
   - Go to **View → Command Palette** (Ctrl+Shift+P)
   - Type "Restart Language Server"
   - Press Enter to restart

### Issue: Color Picker Won't Open

**Symptom**: Clicking color button in Options page does nothing.

**Solutions**:
1. Restart Visual Studio completely
2. Try a different color setting first
3. Check display scaling (100% recommended):
   - If using 125% or 150% scaling, may cause issues
   - Try temporarily setting to 100%
4. Reinstall Extended.Wpf.Toolkit:
   - In solution, right-click asm-dude3-vsix project
   - Select "Manage NuGet Packages"
   - Search "Extended.Wpf.Toolkit"
   - Click "Reinstall"

---

## Syntax Highlighting Issues

### Issue: No Syntax Highlighting at All

**Symptom**: Assembly files open but all text is plain (no colors).

**Solutions**:
1. Verify file extension is `.asm`:
   - Wrong extensions: `.txt`, `.asm.bak`, `.s.bak` don't trigger highlighting
   - Rename file to `.asm` only
2. Restart Visual Studio:
   - Sometimes LSP server fails to start
   - Complete restart often fixes this
3. Check LSP server status:
   - Open **Output** window (View → Output)
   - Select "Language Server Protocol" in dropdown
   - Look for error messages
4. Verify settings enabled:
   - **Tools → Options → AsmDude3 → Syntax Highlighting**
   - Check **"Enable syntax highlighting"** is checked
   - Click OK
5. Restart LSP server:
   - **View → Command Palette** (Ctrl+Shift+P)
   - Type "Restart Language Server"
   - Press Enter

### Issue: Wrong Colors Applied

**Symptom**: Highlighting appears but colors don't match what you configured.

**Solutions**:
1. Clear color cache:
   - Go to **Tools → Options → AsmDude3 → Syntax Highlighting**
   - Change any color and click OK
   - This forces re-evaluation
2. Check your theme:
   - Light theme and dark theme may have different defaults
   - If using dark theme, check dark-specific color settings
3. Check VS color overrides:
   - **Tools → Options → Environment → Fonts and Colors**
   - Look for "AsmDude3" entries
   - If they override your settings, reset them:
     - Select each and click "Reset" button
4. Restart Visual Studio

### Issue: Comments Not Colored Correctly

**Symptom**: Comment color setting doesn't apply to actual comments.

**Solutions**:
1. Check comment syntax for your assembler:
   - MASM: `;` (semicolon)
   - NASM: `;` or `#` depending on variant
   - AT&T: `#`
2. Verify assembler auto-detection:
   - **Tools → Options → AsmDude3 → Assembly Flavour**
   - Check which assembler is detected for your file
   - If wrong, manually select correct one
3. Some comments ignored:
   - Inline comments after code are always highlighted
   - Block comments (multiple lines) follow remark color
   - Check that both are set correctly

### Issue: Specific Token Types Not Highlighted

**Symptom**: Registers show colors but constants don't (or vice versa).

**Solutions**:
1. Check token type is enabled:
   - **Tools → Options → AsmDude3 → Syntax Highlighting**
   - All token types should have colors defined
   - If color is "Automatic" or "Default", may not show
   - Explicitly set to a distinct color
2. Check architecture support:
   - **Tools → Options → AsmDude3 → Architectures**
   - If you disabled architecture for that instruction, it won't highlight
   - Re-enable necessary architectures
3. For user-defined tokens:
   - These are reserved for custom highlighting
   - Not used by default - needs configuration in AsmDudeData.xml

---

## Syntax Highlighting Issues (Specific to Assemblers)

### Issue: MASM Code Highlighting Wrong

**Symptom**: PROC/ENDP keywords not recognized or wrong color.

**Solutions**:
1. Verify MASM detection:
   - First 40 lines must contain MASM keywords
   - Add `PROC` or `ENDP` early in file to help detection
   - Or disable auto-detect: **Tools → Options → AsmDude3 → Assembly Flavour → Manual → MASM**
2. Check casing:
   - Mnemonics must be exact case (MOV, mov, Mov are different)
   - Check your file uses consistent casing
3. Some directives not recognized:
   - Not all MASM directives are supported
   - Only most common ones (PROC, ENDP, SECTION, .code)
   - Others appear as gray "Misc" tokens

### Issue: NASM Code Not Detected Correctly

**Symptom**: NASM file detected as MASM or vice versa.

**Solutions**:
1. Add identifying keywords early:
   - For NASM Intel: Add `section .text` in first 40 lines
   - For NASM AT&T: Add `.section .text` in first 40 lines
   - For MASM: Add `PROC` or `.code` in first 40 lines
2. Manually force assembler:
   - **Tools → Options → AsmDude3 → Assembly Flavour**
   - Uncheck "Auto-Detect Assembler"
   - Select desired assembler (NASM Intel or NASM AT&T)
   - Click OK

### Issue: AT&T Syntax Not Recognized

**Symptom**: Percent-prefixed registers (`%rax`) not highlighted correctly.

**Solutions**:
1. Verify file uses AT&T syntax throughout:
   - AT&T uses: `movl $10, %eax`
   - Intel uses: `mov eax, 10`
   - Check all lines for consistency
2. Enable NASM AT&T:
   - **Tools → Options → AsmDude3 → Assembly Flavour**
   - Select "NASM AT&T" manually (if auto-detect fails)
   - Click OK

---

## Performance Issues

### Issue: Visual Studio Slows Down When Opening Large .ASM Files

**Symptom**: 3+ second delay, freezing, or high CPU/memory when opening large file.

**Solutions**:
1. Check file size:
   - Press Ctrl+End to go to end of file
   - Look at line number shown in status bar
   - If > 50,000 lines, use optimization settings
2. Increase max file lines threshold:
   - **Tools → Options → AsmDude3 → Global → Max File Lines**
   - Increase value (e.g., from 50,000 to 100,000)
   - Higher = slower but more accurate
3. Disable unused architecture support:
   - **Tools → Options → AsmDude3 → Architectures**
   - Uncheck architectures not used in your code
   - E.g., disable AVX-512 if using legacy 32-bit code
4. Split large files:
   - If file > 200,000 lines, consider splitting into modules
   - 50,000 line files are optimal size for performance
5. Close other VS extensions:
   - **Extensions → Manage Extensions**
   - Look for other heavy extensions
   - Disable them to free up resources

### Issue: Memory Usage Too High

**Symptom**: Visual Studio uses 2+ GB RAM, especially after working for hours.

**Solutions**:
1. Close unused assembly files:
   - Each open file uses ~1-5 MB
   - Close files you're not actively editing
2. Restart Visual Studio:
   - After 4+ hours of work, memory accumulates
   - Restarting clears internal caches
3. Disable performance data analysis:
   - **Tools → Options → AsmDude3 → Advanced → Performance Data On**
   - Uncheck this to disable (saves ~50-100 MB)
4. Disable assembly simulator:
   - **Tools → Options → AsmDude3 → Advanced → AsmSim On**
   - Uncheck this if not using (saves ~100-200 MB)

### Issue: Typing is Slow/Laggy

**Symptom**: Delay between keystroke and character appearing.

**Solutions**:
1. Check file size (see above)
2. Disable advanced features:
   - **Tools → Options → AsmDude3**
   - Disable: Code Completion, Signature Help, Label Analysis
   - These cause parsing overhead during typing
3. Disable code folding:
   - **Tools → Options → AsmDude3 → Code Folding → Enable Code Folding**
   - Uncheck to disable (saves ~10-50 ms per edit)
4. Switch to Light theme:
   - Dark theme with syntax highlighting can be slower
   - Try Light theme to see if it improves

---

## LSP Server Issues

### Issue: "Language Server Not Responding" Error

**Symptom**: Error message in editor or IntelliSense stops working.

**Solutions**:
1. Restart language server:
   - **View → Command Palette** (Ctrl+Shift+P)
   - Type "Restart Language Server"
   - Press Enter
   - Wait 5 seconds for server to restart
2. Check server logs:
   - Open **Output** window (View → Output)
   - Select "Language Server Protocol" dropdown
   - Look for error messages
   - Note any stack traces
3. Restart Visual Studio:
   - If server restart doesn't work
   - Close VS completely
   - Reopen it (takes 30 seconds)
4. Check antivirus:
   - Antivirus may block LSP server startup
   - Temporarily disable to test
   - If that fixes it, whitelist VS in antivirus

### Issue: LSP Server Crashes on Startup

**Symptom**: VS starts but immediately shows LSP connection error.

**Solutions**:
1. Check .NET installation:
   - LSP server requires .NET 10.0
   - Verify installed: `dotnet --version`
   - If missing, install from https://dotnet.microsoft.com/download/dotnet/10.0
2. Check VS version compatibility:
   - Requires VS 2022 (17.0+) or VS 2026 (18.0+)
   - Check **Help → About**
3. Clear LSP cache and configs:
   - Close VS
   - Delete: `%TEMP%\AsmDude3` folder
   - Delete: `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\ComponentModelCache`
   - Reopen VS
4. Reinstall extension:
   - **Extensions → Manage Extensions** → Search "AsmDude3"
   - Uninstall
   - Restart VS
   - Reinstall from Marketplace

### Issue: IntelliSense Features Not Working

**Symptom**: Code completion, hover info, or signature help appears but is empty.

**Solutions**:
1. Verify IntelliSense enabled:
   - **Tools → Options → AsmDude3 → IntelliSense**
   - Check desired features are enabled:
     - **Code Completion On** ✓
     - **Signature Help On** ✓
     - **Label Analysis On** ✓
   - Click OK
2. Restart LSP server:
   - **View → Command Palette** (Ctrl+Shift+P)
   - Type "Restart Language Server"
   - Press Enter
3. Wait for initial analysis:
   - First opening a file takes 2-5 seconds for analysis
   - IntelliSense is available after status shows "Ready"
   - Look for "Ready" in bottom status bar

---

## Settings & Options Issues

### Issue: Settings Reset to Defaults

**Symptom**: Custom color/assembler settings disappear after restart.

**Solutions**:
1. Verify you clicked OK (not just Apply):
   - Apply may not save in some VS versions
   - Always use OK button to ensure persistence
2. Check registry permissions:
   - If running VS as non-admin, may not save to registry
   - Try running Visual Studio as Administrator
3. Registry may be corrupted:
   - Close VS
   - Delete: `%APPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\privateregistry.bin`
   - Reopen VS (registry rebuilds)
   - Reconfigure settings

### Issue: Can't Import/Export Settings

**Symptom**: Tools → Options → Import/Export Settings shows error.

**Solutions**:
1. Use standard VS export:
   - **Tools → Import and Export Settings**
   - Select "Export selected environment settings"
   - Choose which settings to export
   - Save to file
2. Restore settings:
   - **Tools → Import and Export Settings**
   - Select "Import selected environment settings"
   - Choose saved settings file
   - VS restarts and applies settings

### Issue: Architecture Flags Not Saving

**Symptom**: Architecture selections (AVX-512, SSE, etc.) reset to enabled.

**Solutions**:
1. Make sure to click OK:
   - Changes to architecture flags require OK button
   - Apply alone may not save
2. If many architectures disabled, saves only disabled ones:
   - This is normal optimization
   - VS stores only changed settings
3. Clear and reconfigure:
   - Go to **Tools → Options → AsmDude3 → Architectures**
   - First enable ALL: Look for "Enable All" button (if available)
   - Then uncheck only what you don't need
   - Click OK

---

## Advanced Issues

### Issue: Custom Macros/Directives Not Highlighted

**Symptom**: Your custom macro names appear as gray "Misc" instead of specific colors.

**Solutions**:
1. Add to AsmDudeData.xml:
   - Find: `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxx\Extensions\AsmDude3\Resources\AsmDudeData.xml`
   - Add your macro under appropriate `<Keywords>` section
   - Restart Visual Studio
2. Or use User Defined token types:
   - **Tools → Options → AsmDude3 → Syntax Highlighting**
   - Set color for "User Defined 1", "User Defined 2", "User Defined 3"
   - These can be used for custom categories
3. Rebuild extension from source:
   - If modifying AsmDudeData.xml, may need rebuild
   - Not recommended unless familiar with build process

### Issue: Disassembly Window Not Highlighted

**Symptom**: Debugger disassembly output appears but no colors.

**Solutions**:
1. Enable disassembly highlighting:
   - **Tools → Options → AsmDude3 → Assembly Flavour**
   - Look for disassembly options
   - Select correct disassembly format
   - Click OK
2. Verify debugger output format:
   - Disassembly must be in recognized format:
     - MASM disassembly (Visual Studio native)
     - GDB/LLDB format
     - IDA Pro format
   - Other formats may not highlight
3. Restart debugger session:
   - Stop debugging
   - Start again (F5)
   - Disassembly window should now highlight

### Issue: "Z3 Theorem Prover Not Found" Warning

**Symptom**: AsmSim features disabled with warning about Z3.

**Solutions**:
1. Z3 is optional:
   - If not using assembly simulator, this warning is harmless
   - Can be safely ignored
2. To enable AsmSim:
   - Download Z3 from: https://github.com/Z3Prover/z3/releases
   - Extract to: `C:\Program Files\Z3\`
   - Restart Visual Studio
3. Or disable warning:
   - **Tools → Options → AsmDude3 → Advanced → AsmSim On**
   - Uncheck to disable and remove warning

---

## Getting More Help

**Still stuck?** Try:
1. **Check docs**: [USER_GUIDE.md](USER_GUIDE.md) or [CONFIGURATION.md](CONFIGURATION.md)
2. **Search issues**: https://github.com/HJLebbink/asm-dude/issues
3. **Report bug**: Create new issue with:
   - VS version (Help → About)
   - AsmDude3 version (Tools → Options → AsmDude3)
   - Steps to reproduce
   - Error message/screenshot
   - Any relevant .asm file code
4. **Check known issues**: [FEATURES.md](FEATURES.md#known-limitations)

---

**Happy assembly coding!** 🎉
