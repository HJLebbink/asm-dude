# AsmDude3 Manual Testing Guide

**Purpose**: This guide helps you manually test the VSIX extension components that cannot be automatically tested.

**When to use**: After running all automated tests successfully, use this guide to verify the VS integration works correctly.

---

## Prerequisites

✅ **Before Testing:**
1. All automated tests pass: `dotnet test asm-dude3-server-tests`
2. Server builds successfully: `dotnet build asm-dude3-server`
3. VSIX builds successfully in Visual Studio
4. .NET 10.0 SDK installed
5. Visual Studio 2022 or 2026 with Extension Development workload

---

## Test Environment Setup

### 1. Build the Solution

```bash
# From VS/CSHARP/asm-dude3/
dotnet restore asm-dude3-server
dotnet build asm-dude3-server
```

### 2. Open in Visual Studio

1. Open `AsmDude.sln` in Visual Studio 2022/2026
2. Set **asm-dude3-vsix** as the startup project
3. Press **F5** to build and launch VS Experimental Instance

**Expected**: VS Experimental Instance launches without errors

---

## Test Suite

## Test 1: Extension Loads

**Objective**: Verify the extension installs and loads

### Steps:
1. In VS Experimental Instance, go to **Extensions** → **Manage Extensions**
2. Look for "AsmDude3" in the installed list

### Expected Results:
- ✅ AsmDude3 appears in extension list
- ✅ Version shows 3.0.0.0
- ✅ No error dialogs appear

### If It Fails:
- Check Output window → Show output from: "Extensions"
- Look for MEF composition errors
- Verify `source.extension.vsixmanifest` is valid

---

## Test 2: Content Type Registration

**Objective**: Verify .asm files are recognized

### Steps:
1. In VS Experimental, create a new file: **File** → **New** → **File**
2. Save as `test.asm`
3. Check the bottom-right corner for language indicator

### Expected Results:
- ✅ File extension `.asm` is recognized
- ✅ No "Plain Text" mode (means content type registered)

### Also Test:
- Create `test.cod` - should be recognized
- Create `test.inc` - should be recognized
- Create `test.s` - should be recognized

### If It Fails:
- Check Output window → Show output from: "Editor"
- Verify `AsmContentDefinition.cs` was compiled into VSIX
- Check MEF composition cache: Delete `%LocalAppData%\Microsoft\VisualStudio\[version]Exp\ComponentModelCache`

---

## Test 3: Language Server Activation

**Objective**: Verify LSP server process starts

### Steps:
1. Open or create a `.asm` file with some content:
   ```asm
   mov rax, rbx
   add rcx, rdx
   ```
2. Open **Task Manager**
3. Look for `AsmDude2.LSP.exe` process

### Expected Results:
- ✅ Server process appears in Task Manager
- ✅ Output window shows no errors (View → Output → "AsmDude3 Language Client")
- ✅ No crash dialogs

### Debug Output to Check:
```
Tools → Options → Debugging → Output Window
Enable: "LSP Infrastructure" messages
```

Look for:
- "AsmDude3 Language Client activating..."
- "Language server started successfully"
- "Initialize handshake complete"

### If It Fails:

**Server Not Starting:**
- Check server executable exists: `[Extension Dir]\Server\AsmDude2.LSP.exe`
- Verify server builds: `dotnet build asm-dude3-server`
- Check `AsmLanguageClient.cs` GetServerExecutablePath() logic
- Look at Debug output in VS Experimental

**Server Crashes:**
- Run server standalone: `dotnet run --project asm-dude3-server`
- Check if it waits for input (should hang waiting for stdin)
- Look for unhandled exceptions in Output window

---

## Test 4: Initialize Handshake

**Objective**: Verify client and server complete LSP initialization

### Steps:
1. Open a `.asm` file
2. Wait 2-3 seconds for initialization
3. Check **Output** window → "AsmDude3 Language Client"

### Expected Results:
- ✅ See "OnServerInitializedAsync called" or similar message
- ✅ No "OnServerInitializeFailedAsync" messages
- ✅ No timeout errors

### Debug Info:
Enable detailed logging in `AsmLanguageClient.cs`:
```csharp
System.Diagnostics.Debug.WriteLine("Server process started: " + _serverProcess.Id);
```

### If It Fails:

**Timeout:**
- Server taking too long to respond
- Check server logs in Debug output
- Verify JSON-RPC communication working

**Protocol Error:**
- Client and server using incompatible LSP versions
- Check InitializeParams match InitializeResult structure
- Verify Newtonsoft.Json serialization settings

---

## Test 5: Document Synchronization

**Objective**: Verify text changes are sent to server

### Steps:
1. Open a `.asm` file
2. Type some text: `mov rax, 0x10`
3. Watch server process memory in Task Manager
4. Make several edits

### Expected Results:
- ✅ No crashes when typing
- ✅ Server process memory stable (not growing infinitely)
- ✅ No exceptions in Output window

### Test Scenarios:
1. **Type text** - DidChange should fire
2. **Delete text** - DidChange should fire
3. **Close file** - DidClose should fire, server releases document
4. **Reopen file** - DidOpen should fire

### Debugging:
Add logging to `LanguageServer.cs`:
```csharp
[JsonRpcMethod("textDocument/didChange")]
public void DidChangeTextDocument(DidChangeTextDocumentParams @params)
{
    System.Diagnostics.Debug.WriteLine($"Document changed: {params.TextDocument.Uri}");
    // ... existing code
}
```

### If It Fails:

**No Updates Received:**
- Check `TextDocumentSyncOptions` in Initialize response
- Verify `OpenClose = true` and `Change = Full`
- Client might not be sending notifications

**Memory Leak:**
- Documents not being released on close
- Check DocumentManager.CloseDocument() is called

---

## Test 6: Multiple Files

**Objective**: Verify multiple documents work simultaneously

### Steps:
1. Open `test1.asm`
2. Open `test2.asm`
3. Switch between tabs
4. Close one file
5. Verify the other still works

### Expected Results:
- ✅ Both files tracked independently
- ✅ Closing one doesn't affect the other
- ✅ Server tracks document count correctly (check logs)

---

## Test 7: Extension Unload

**Objective**: Verify clean shutdown

### Steps:
1. Close all `.asm` files
2. Close VS Experimental Instance
3. Check Task Manager

### Expected Results:
- ✅ `AsmDude2.LSP.exe` process terminates
- ✅ No zombie processes
- ✅ No crash reports

### If It Fails:
- Server not responding to shutdown
- Check `Shutdown()` and `Exit()` handlers
- Verify `Dispose()` in `AsmLanguageClient.cs` kills process

---

## Test 8: Error Handling

**Objective**: Verify graceful error handling

### Test 8a: Server Executable Missing

1. Temporarily rename server executable
2. Try to open `.asm` file

**Expected:**
- ✅ Error notification shown (ShowNotificationOnInitializeFailed = true)
- ✅ No VS crash
- ✅ Clear error message to user

### Test 8b: Server Crashes

1. Modify server to crash on initialize:
   ```csharp
   public InitializeResult Initialize(InitializeParams @params)
   {
       throw new Exception("Test crash");
   }
   ```
2. Open `.asm` file

**Expected:**
- ✅ OnServerInitializeFailedAsync called
- ✅ Error message shown to user
- ✅ VS remains stable

### Test 8c: Invalid JSON

1. Modify server to send invalid JSON
2. Open `.asm` file

**Expected:**
- ✅ JSON-RPC error caught
- ✅ Extension doesn't crash VS

---

## Test 9: Performance

**Objective**: Verify acceptable performance

### Steps:
1. Create large `.asm` file (1000+ lines)
2. Open it in VS Experimental
3. Scroll through file
4. Make edits

### Expected Results:
- ✅ Opens in < 2 seconds
- ✅ Typing feels responsive
- ✅ No UI freezes
- ✅ Scrolling smooth

### Metrics to Check:
- Server process CPU usage (should be low when idle)
- Memory usage (should be reasonable < 100MB for basic tasks)
- Responsiveness (no beach ball / frozen UI)

---

## Test 10: Stress Testing

**Objective**: Verify stability under load

### Steps:
1. Open 10+ `.asm` files simultaneously
2. Rapidly switch between tabs
3. Make edits in multiple files
4. Close all files

### Expected Results:
- ✅ All files work correctly
- ✅ No memory leaks
- ✅ No deadlocks or hangs
- ✅ Server process remains stable

---

## Logging and Diagnostics

### Enable Detailed Logging

**In Server (Program.cs):**
```csharp
builder.SetMinimumLevel(LogLevel.Trace); // Most verbose
```

**In Visual Studio:**
```
Tools → Options → Environment → Logging
Set "Activity Log" to "On"
```

**View Activity Log:**
```
%AppData%\Microsoft\VisualStudio\[version]Exp\ActivityLog.xml
```

### Common Issues and Solutions

| Issue | Likely Cause | Solution |
|-------|--------------|----------|
| Extension doesn't load | MEF composition error | Check Output → Extensions, clear MEF cache |
| Server doesn't start | Path to executable wrong | Verify GetServerExecutablePath() |
| Initialize timeout | Server hanging | Test server standalone, check JSON-RPC |
| Document changes not syncing | TextDocumentSyncOptions wrong | Verify capabilities in Initialize |
| Memory leak | Documents not released | Check CloseDocument() is called |
| Server crashes | Unhandled exception | Add try-catch, check logs |

---

## Automated Test Results

Before manual testing, verify all automated tests pass:

```bash
cd VS/CSHARP/asm-dude3/asm-dude3-server-tests
dotnet test --verbosity normal
```

**Required:**
- ✅ DocumentManagerTests: All tests pass (35+ tests)
- ✅ LanguageServerTests: All tests pass (25+ tests)
- ✅ 0 failures, 0 errors

---

## Reporting Issues

When reporting issues, include:

1. **Environment:**
   - Visual Studio version (2022/2026, edition)
   - .NET SDK version (`dotnet --version`)
   - Windows version

2. **Repro Steps:**
   - Exact steps to reproduce
   - Sample `.asm` file content if relevant

3. **Logs:**
   - Output window content (all sources)
   - ActivityLog.xml relevant sections
   - Server stderr output if available

4. **Test Results:**
   - Which automated tests passed/failed
   - Which manual tests passed/failed

---

## Success Criteria

✅ **Phase 2 Complete When:**
1. All automated tests pass (60+ tests)
2. All manual tests pass (Tests 1-10)
3. Extension loads without errors
4. Server starts and initializes successfully
5. Documents can be opened, edited, and closed
6. Clean shutdown with no leaks
7. Acceptable performance (< 2s open, responsive typing)

---

**Last Updated**: December 2025
**Next**: Phase 3 - Feature Migration (Syntax Highlighting, Completion, etc.)
