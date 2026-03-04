# How to Run AsmDude3 with F5 (Debug)

## Quick Start

### Option 1: Build and Run from Visual Studio (Recommended)

1. **Open the solution**:
   ```
   VS\AsmDude.sln
   ```

2. **Set startup project**:
   - Right-click `asm-dude3-vsix` in Solution Explorer
   - Click "Set as Startup Project"

3. **Press F5** or select Debug → Start Debugging

4. **Wait** for the experimental Visual Studio instance to launch (1-2 minutes on first run)

5. **Test the extension**:
   - Open a `.asm` file (or create one)
   - You should see syntax highlighting for assembly code
   - Go to Tools → Options → AsmDude3 → General to access settings

### Option 2: Build from Command Line

1. **Build the VSIX**:
   ```
   dotnet build VS\CSHARP\asm-dude3\asm-dude3-vsix\asm-dude3-vsix.csproj -c Debug
   ```

2. **Build the LSP server**:
   ```
   dotnet build VS\CSHARP\asm-dude3\asm-dude3-server\asm-dude3-server.csproj -c Debug
   ```

3. **Launch from Visual Studio**:
   - The VSIX will be deployed automatically if you set it as the startup project
   - Press F5 from Visual Studio

## Troubleshooting F5

### Build Errors

**Problem**: "571 errors when building solution"

**Solution**: These errors are from the old `asm-dude2-vsix` project. They don't affect AsmDude3.
- Set `asm-dude3-vsix` as the startup project
- It will build and run independently

### Experimental Instance Doesn't Launch

**Problem**: F5 does nothing or hangs

**Check**:
1. Verify `asm-dude3-vsix` is the startup project
2. Check `$(DevenvDir)devenv.exe` path exists (usually in VS 2022 installation)
3. Try building just the project first: `Ctrl+Shift+B`

### Options Page is Empty

**Problem**: Tools → Options → AsmDude3 shows no settings

**Diagnosis**:
1. Open View → Output in the experimental instance
2. Look for messages starting with `AsmDudeOptionsPageUI:`
3. Check `EMPTY_OPTIONS_PAGE_DIAGNOSTIC.md` for detailed troubleshooting

## Build Output Locations

After building, key files are at:

```
VS/CSHARP/asm-dude3/asm-dude3-vsix/bin/Debug/net48/
  ├─ asm-dude3-vsix.dll          (Main extension assembly)
  └─ ... other dependencies

VS/CSHARP/asm-dude3/asm-dude3-server/bin/Debug/net10.0-windows/
  ├─ asm-dude3-server.exe        (LSP server executable)
  ├─ asm-dude3-server.dll
  └─ ... other dependencies
```

The VSIX package will be at:
```
VS/CSHARP/asm-dude3/asm-dude3-vsix/bin/Debug/asm-dude3-vsix.vsix
```

## Debug Output

The experimental VS instance prints debug output. To see it:

1. **In the experimental instance**:
   - View → Output (or press `Ctrl+Alt+O`)
   - Select "Debug" from the dropdown

2. **Look for AsmDude3 messages**:
   - Extension loading messages
   - LSP server startup messages
   - Options page initialization messages
   - Syntax highlighting events

3. **Example output**:
   ```
   [timestamp] AsmDudeLanguageClient: Initializing LSP client
   [timestamp] AsmDudeLanguageClient: LSP server started
   [timestamp] AsmDudeOptionsPageUI: Found 15 methods
   [timestamp] AsmDudeOptionsPageUI: Calling InitializeComponent via reflection
   ```

## Registry Locations

AsmDude3 stores settings in:

```
HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\17.0_<hash>\UserSettings
```

Debug instance uses a special experimental registry hive, so settings don't affect your main VS instance.

## Common Issues and Fixes

| Issue | Solution |
|-------|----------|
| LSP server fails to start | Check asm-dude3-server.exe location in output folder |
| Extension not visible in UI | Restart the experimental instance or rebuild |
| Syntax highlighting not working | Check that .asm file has assembly code with known mnemonics |
| Options page crashes | Check ActivityLog.xml in `%APPDATA%\Microsoft\VisualStudio\` |

## Next Steps

After F5 launches:

1. **Create a test file**: `test.asm` with:
   ```asm
   mov rax, rbx
   add rcx, rdx
   xor r8, r9
   ```

2. **Open it in the experimental instance**:
   - File → Open → test.asm
   - You should see colored syntax highlighting

3. **Check Options**:
   - Tools → Options → AsmDude3 → General
   - Customize syntax highlighting colors and settings

## Support

For detailed diagnostics, see:
- `EMPTY_OPTIONS_PAGE_DIAGNOSTIC.md` - Options page issues
- `F5_BUILD_FIXES.md` - Build issues and their solutions
- `TROUBLESHOOTING.md` - General extension troubleshooting

