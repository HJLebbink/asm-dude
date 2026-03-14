# AsmDude2 Extension Debugging Guide

## Current Status (March 14, 2026)

✅ **Build**: Successful, no compilation errors
✅ **Extension Package**: AsmDude2.vsix (37 MB) created with bundled LSP server
✅ **Manifest**: extension.json properly configured
✅ **LSP Server**: AsmDude2.LSP.exe bundled in Server/ directory

## How to Launch the Extension

### Method 1: From Visual Studio IDE (Recommended)

1. Open `VS\AsmDude.sln` in Visual Studio 2022
2. Right-click on `asm-dude2-vsix` project → **Set as Startup Project**
3. Press **F5** (Debug → Start Debugging)
4. VS should:
   - Build the extension
   - Deploy it to the experimental instance (`17.0_Exp`)
   - Launch a new VS window labeled "Visual Studio Experimental Instance"

### Method 2: Manual VSIX Installation (Debugging)

If Method 1 doesn't work:

```powershell
# Open PowerShell as Administrator
cd C:\Source\Github\asm-dude

$vsixPath = "VS\CSHARP\asm-dude2-vsix\bin\Debug\net10.0-windows\AsmDude2.vsix"
$vsInstall = "C:\Program Files\Microsoft Visual Studio\18\Community"
$installer = "$vsInstall\Common7\IDE\VSIXInstaller.exe"

# Kill existing devenv processes
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force

# Install to experimental instance
& $installer /rootSuffix:Exp "$vsixPath" /quiet

# Launch experimental instance
& "$vsInstall\Common7\IDE\devenv.exe" /rootSuffix Exp /log:$env:TEMP\asmdude-log.txt
```

## Verification Checklist

### ✅ Step 1: Extension Loads

**Expected**: Experimental VS window opens without errors

**If fails**: Check ComponentModelCache for exceptions:
- Look in: `%APPDATA%\Microsoft\VisualStudio\17.0_Exp\ComponentModelCache\`
- Review all .log files for errors related to "AsmDude"
- Check the original VS instance's error output

### ✅ Step 2: Extension Appears in Extensions Menu

1. In experimental VS, go to **Extensions → Manage Extensions**
2. Search for "AsmDude"
3. Should see: "AsmDude2 - Syntax highlighting and code assistance for assembly source code"

**If not found**: Extension wasn't deployed. Go back to Method 2 and check VSIXInstaller output.

### ✅ Step 3: Syntax Highlighting Works

1. Create or open a file named `test.asm` with assembly code:
   ```asm
   mov eax, 0x10
   add eax, ebx
   jmp skip
   ```

2. **Expected**: Keywords (MOV, ADD, JMP) should be colored differently from operands

3. **If no colors appear**:
   - Document type might not be registered
   - Try other extensions: `.cod`, `.inc`, `.s`

### ✅ Step 4: LSP Server Starts

**Check the LSP output pane**:

1. **View → Output** (or Ctrl+Alt+O)
2. Look for output from "AsmDude2.LSP" or similar

**Expected log messages**:
```
[AsmDude2.LSP STARTED] BuildTime=2026-03-14 HH:MM:ss.fff UTC
[AsmDude2.VSIX LOADED]: Assembly=AsmDude2.dll, BuildTime=2026-03-14 HH:MM:ss.fff UTC
AsmDude2: CreateServerConnectionAsync starting
AsmDude2: Trying path 1: ...Server/AsmDude2.LSP.exe
AsmDude2: Found LSP server at path X
AsmDude2: Starting LSP server from ...
AsmDude2: LSP server connected via named pipes
```

**If LSP doesn't start**:
- Check Windows Event Viewer → Applications for crashes
- LSP server might not be finding its location - see Path Discovery below

### ✅ Step 5: Features Activate

1. **Hover**: Move mouse over a mnemonic (MOV, ADD, etc.)
   - Expected: Tooltip with instruction description

2. **Inlay Hints**: Should show register values/conversions above lines
   - Look for small gray text above assembly lines

3. **Code Completion**: In assembly code, type `mov ` and press Ctrl+Space
   - Expected: List of available operands

4. **Code Folding**: Click arrow next to line numbers to collapse/expand blocks

## Diagnostic Logging

All major components now have Debug.WriteLine() logging:

- **Extension Loading**: `AsmLanguageServerProvider` static constructor
- **LSP Server Discovery**: `CreateServerConnectionAsync()` tries 4 different paths
- **Path Resolution**: Logs `AppContext.BaseDirectory`, `AppDomain.BaseDirectory`, etc.

### View Debug Output

1. In VS, go to **Debug → Windows → Output** (Ctrl+Alt+O)
2. Change dropdown from "Debug" to "All Output"
3. Press F5 to launch experimental instance
4. Watch the output window for "AsmDude2:" messages

###  Capture Detailed Logs

Save logs to a file for analysis:

```powershell
$experimentalRoot = "$env:APPDATA\Microsoft\VisualStudio\17.0_Exp"
$logDir = "C:\Temp\AsmDude_Logs"
mkdir $logDir -Force

# Copy all logs
Copy-Item "$experimentalRoot\ComponentModelCache\*.log" $logDir -ErrorAction SilentlyContinue
Copy-Item "$env:TEMP\asmdude*.log" $logDir -ErrorAction SilentlyContinue

Write-Host "Logs saved to: $logDir"
Get-ChildItem $logDir
```

## Path Discovery (Troubleshooting)

The LSP server location is found via 4 mechanisms (in order):

1. **AppContext.BaseDirectory + /Server/AsmDude2.LSP.exe**
   - For out-of-process extensions, this should point to the extension's directory

2. **AppDomain.CurrentDomain.BaseDirectory + /Server/AsmDude2.LSP.exe**
   - Fallback for different hosting scenarios

3. **Assembly.Location + /Server/AsmDude2.LSP.exe**
   - For in-process deployments (Assembly.Location is empty for embedded assemblies)

4. **Directory search** - Recursively search `AppContext.BaseDirectory` for `AsmDude2.LSP.exe`
   - Last resort, slowest option

### Expected Actual Paths

For VSIX deployment to experimental instance:
- **Extension DLL**: `%APPDATA%\Microsoft\VisualStudio\17.0_Exp\Extensions\<publisher>\AsmDude2\AsmDude2.dll`
- **LSP Server**: `%APPDATA%\Microsoft\VisualStudio\17.0_Exp\Extensions\<publisher>\AsmDude2\Server\AsmDude2.LSP.exe`
- **LSP Libraries**: `%APPDATA%\Microsoft\VisualStudio\17.0_Exp\Extensions\<publisher>\AsmDude2\Server\*.dll`

## Common Issues & Solutions

### Issue: "Extension is not responding"

**Cause**: LSP server didn't start or crashed immediately

**Solution**:
1. Check diagnostic logs (see above)
2. Verify LSP server file exists: `bin\Debug\net10.0-windows\Server\AsmDude2.LSP.exe`
3. Try running it manually from command line to see if there are startup errors
4. Check if Z3 library (`libz3.dll`) is present in the LSP server directory

### Issue: No syntax highlighting

**Cause**: Document type not registered or LSP not responding

**Solution**:
1. Check that `AsmDocumentTypes.cs` has `[VisualStudioContribution]` attribute ✅ Already done
2. Verify file extension `.asm` is registered in `extension.json`
3. Try opening file with different extension (`.cod`, `.inc`, `.s`)

### Issue: Inlay hints not showing

**Cause**: LSP server not responding to `textDocument/inlayHint` requests

**Solution**:
1. Check LSP server logs for errors
2. Verify `GetInlayHints()` method in LanguageServer.cs has `[JsonRpcMethod("textDocument/inlayHint")]` ✅ Already done
3. Check that `AsmSim_On = true` in default options

### Issue: "Assembly.Location returns empty string" warning

**Expected behavior**: This is normal for embedded assemblies in out-of-process extensions. The diagnostic code now tries `AppContext.BaseDirectory` and `AppDomain.BaseDirectory` instead. ✅ Already implemented

## Next Steps If Extension Still Doesn't Load

1. **Verify the build is fresh**:
   ```powershell
   dotnet clean VS\AsmDude.sln
   dotnet build VS\CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj -c Debug
   ```

2. **Check VSIX contents**:
   ```powershell
   # Extract VSIX (it's just a ZIP file)
   Expand-Archive -Path "bin\Debug\net10.0-windows\AsmDude2.vsix" -DestinationPath "C:\Temp\vsix-contents"

   # Verify files exist:
   ls "C:\Temp\vsix-contents\Server\AsmDude2.LSP.exe"
   ls "C:\Temp\vsix-contents\.vsextension\extension.json"
   ```

3. **Check ComponentModelCache errors**:
   ```powershell
   ls $env:APPDATA\Microsoft\VisualStudio\17.0_Exp\ComponentModelCache\
   # Look for CrashRpt.txt or similar error files
   ```

4. **Try removing the experimental instance and starting fresh**:
   ```powershell
   rm -Recurse "$env:APPDATA\Microsoft\VisualStudio\17.0_Exp" -Force
   # Now launch F5 again to create a clean experimental instance
   ```

## Resources

- VisualStudio.Extensibility Documentation: https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio-sdk
- LSP Specification: https://microsoft.github.io/language-server-protocol/
- VS Experimental Instance Docs: https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance

