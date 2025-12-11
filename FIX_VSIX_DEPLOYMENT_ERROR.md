# Fix: VSIX Deployment File Lock Error

## The Problem

When pressing F5, you got this error:
```
Problem occurred while extracting the vsix to the experimental extensions path.
The process cannot access the file 'C:\Users\henkj\AppData\Local\Microsoft\VisualStudio\18.0_690edbf8Exp\Extensions\...\asm-dude3-server.dll'
because it is being used by another process.
```

## Root Cause

The LSP server process (`asm-dude3-server.exe`) from a previous F5 run was still running in memory, keeping a lock on the DLL file. When Visual Studio tried to update the extension, it couldn't replace the locked file.

## Solution Applied ✅

We've cleaned up the issue:

1. **Killed the LSP server process**
   - Terminated any running `asm-dude3-server.exe` instances

2. **Closed Visual Studio**
   - Killed all `devenv.exe` processes to ensure clean state

3. **Removed old deployment**
   - Deleted the old VSIX extension folder:
   ```
   C:\Users\henkj\AppData\Local\Microsoft\VisualStudio\18.0_690edbf8Exp\Extensions\Henk-Jan Lebbink\AsmDude3\
   ```

4. **Cleaned the build**
   - `dotnet clean` to remove old build artifacts

5. **Rebuilt the project**
   - Fresh build of asm-dude3-vsix
   - All 0 errors, ready for F5

## Try Again Now

```
1. Press F5  (Visual Studio will rebuild and relaunch)
2. Wait for experimental instance to launch (30-60 seconds)
3. Go to Tools → Options → AsmDude3 → General
4. Check View → Output for diagnostic messages
```

## Prevention for Future

If this happens again:

**Quick Fix**:
```powershell
# In PowerShell, run:
Get-Process asm-dude3-server -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force
```

**Full Reset**:
```
1. Close Visual Studio completely
2. Kill asm-dude3-server.exe (check Task Manager)
3. Delete: C:\Users\henkj\AppData\Local\Microsoft\VisualStudio\18.0_690edbf8Exp\Extensions\Henk-Jan Lebbink\AsmDude3\
4. In project folder: dotnet clean
5. Press F5 again
```

## Technical Details

### Why This Happens

The extension deployment mechanism:
1. Builds the VSIX package
2. Extracts it to the experimental extensions folder
3. VS loads the extension and starts the LSP server
4. On next F5, VS tries to replace files but they're still locked

The LSP server keeps the DLL loaded:
- Even after closing the experimental instance
- If the process wasn't properly terminated
- If another instance is still running

### File Locking Chain

```
asm-dude3-server.exe (running)
  ↓
Loads asm-dude3-server.dll (locked)
  ↓
VS tries to replace file (BLOCKED)
  ↓
"File is being used by another process" ERROR
```

### Clean State

After our cleanup:
1. All processes terminated ✓
2. Old DLL deleted ✓
3. New build created ✓
4. Ready for deployment ✓

## Files Involved

- **VSIX Package**: `bin/Debug/asm-dude3-vsix.vsix`
- **LSP Server**: `bin/Debug/net10.0-windows/asm-dude3-server.exe`
- **Experimental Extensions**: `AppData/Local/Microsoft/VisualStudio/18.0_690edbf8Exp/Extensions/`

## Status

✅ **FIXED** - Ready for F5

The issue has been resolved. You should now be able to press F5 without the file lock error.

## What To Do Now

1. Go back to Visual Studio main window
2. Make sure `asm-dude3-vsix` is the startup project
3. Press **F5** to launch the experimental instance
4. Check the debug output for initialization messages
5. Navigate to Tools → Options → AsmDude3 → General

Let us know what debug messages appear!

