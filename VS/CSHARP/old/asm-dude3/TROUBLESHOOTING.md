# AsmDude3 Troubleshooting Guide

## Activity Log Locations

### Main Visual Studio Instance
```
C:\Users\<username>\AppData\Roaming\Microsoft\VisualStudio\18.0_<instance>\ActivityLog.xml
```

### Experimental Instance (when debugging VSIX)
```
C:\Users\<username>\AppData\Roaming\Microsoft\VisualStudio\18.0_<instance>Exp\ActivityLog.xml
```

### Find All Activity Logs
```powershell
Get-ChildItem -Path "$env:APPDATA\Microsoft\VisualStudio" -Recurse -Filter "ActivityLog.xml" | Sort-Object LastWriteTime -Descending
```

## Visual Studio 2026 Paths

### devenv.exe Location
```
C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe
```

For different editions:
- Community: `...\18\Community\...`
- Professional: `...\18\Professional\...`
- Enterprise: `...\18\Enterprise\...`
- Preview: `...\18\Preview\...`

### Starting with Logging
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /log "C:\path\to\solution.sln"
```

## Common Issues

### Extension Not Loading

**Check if extension is deployed:**
```powershell
Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_*Exp\Extensions" -Recurse -Filter "*.dll"
```

**Check extension manager in experimental instance:**
- Extensions → Manage Extensions
- Look for "AsmDude3"

### FileEnumerationServicePackage Error

This is a known VS 2026 Preview issue, not specific to AsmDude3.

**Fix 1: Reset Experimental Instance**
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /ResetSettings /RootSuffix Exp
```

**Fix 2: Clear Experimental Instance Completely**
```powershell
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_*Exp" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$env:APPDATA\Microsoft\VisualStudio\18.0_*Exp" -ErrorAction SilentlyContinue
```

### LSP Server Not Starting

**Check Debug Output:**
1. View → Output (Ctrl+Alt+O)
2. Select "Debug" from dropdown
3. Look for "AsmDude3 Language Server" messages

**Common causes:**
- Server executable not found at: `<extension-dir>\Server\asm-dude3-server.exe`
- Server crashes on startup (check stderr output)
- Wrong .NET version (requires .NET 10)

**Manual test of LSP server:**
```powershell
& "C:\Source\Github\asm-dude\VS\CSHARP\asm-dude3\asm-dude3-server\bin\Debug\net10.0-windows\asm-dude3-server.exe"
```
Should show: "AsmDude3 Language Server starting..."

## Debugging Steps

### 1. Check Build Output
- Ensure asm-dude3-server builds successfully
- Ensure asm-dude3-vsix builds successfully
- Check that server DLLs are copied to VSIX output

### 2. Verify Deployment
```powershell
# Find where VSIX deployed
$vsixDir = Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_*Exp\Extensions" -Recurse -Filter "AsmDude3*.dll" | Select-Object -First 1 | Split-Path -Parent

# Check server files exist
Test-Path "$vsixDir\Server\asm-dude3-server.exe"
```

### 3. Enable Verbose LSP Logging
In experimental instance:
- Tools → Options → Text Editor → Advanced
- Enable "Log Language Server Client messages"

### 4. Check for Extension Conflicts
Disable other assembly/LSP extensions in experimental instance

## Build Configuration

### Required Versions
- Visual Studio 2026 (18.x)
- .NET 10.0 SDK
- VSSDK Build Tools 17.14.2120+

### Build Order
1. asm-tools-lib
2. asm-sim-lib
3. asm-dude3-server
4. asm-dude3-server-tests (optional)
5. asm-dude3-vsix

## Reporting Issues

When reporting issues, include:
1. Visual Studio version (`Help → About`)
2. .NET SDK version (`dotnet --version`)
3. Relevant portions of ActivityLog.xml
4. Debug Output window contents
5. Steps to reproduce

## Useful Commands

### Find Visual Studio Installations
```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -all -prerelease
```

### List Installed Extensions (Experimental)
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /RootSuffix Exp /Command "Tools.ExtensionsAndUpdates"
```

### View All VS Processes
```powershell
Get-Process devenv | Format-Table Id, ProcessName, StartTime, MainWindowTitle
```
