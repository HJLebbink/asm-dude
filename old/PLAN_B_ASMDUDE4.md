# Plan B: AsmDude4 - Minimal Upgrade Strategy

## Executive Summary

**Goal:** Create AsmDude4 by copying the **complete, working** asmdude2-vsix codebase and **only** upgrading the Microsoft.VisualStudio.LanguageServer.Client library to version 17.14.60.

**Rationale:**
- AsmDude2-vsix is **fully functional** with all features working
- AsmDude3 port is missing critical functionality (font/color updates, user dialogs, MEF cache clearing)
- Upgrading only the LSP client library minimizes risk and preserves all working features
- Estimated effort: **2-3 hours** vs. **20+ hours** to complete asmdude3 port

---

## Current State Analysis

### AsmDude2-vsix (Source - WORKING) ✅
**Location:** `VS\CSHARP\asm-dude2-vsix\`

**LanguageServer.Client Version:** 17.2.8 (stable)

**Complete Features:**
- ✅ LSP server integration
- ✅ Font/Color registry updates via IVsFontAndColorStorage
- ✅ Font/Color cache refresh via IVsFontAndColorCacheManager
- ✅ User confirmation dialogs before saving
- ✅ LSP server restart on settings change
- ✅ Settings persistence to registry
- ✅ Architecture tooltips (60+ ARCH_* flags)
- ✅ Numeration parsing (HEX/DEC/BIN/OCT conversion)
- ✅ Complete Options page with all 157 settings
- ✅ Syntax highlighting (MASM, NASM Intel, NASM AT&T)
- ✅ Code completion, hover, signature help
- ✅ Performance data integration

**Package Versions:**
```xml
<PackageReference Include="Microsoft.VisualStudio.LanguageServer.Protocol" Version="17.2.8" />
<PackageReference Include="StreamJsonRpc" Version="2.22.23" />
<PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.14.40265" />
<PackageReference Include="Extended.Wpf.Toolkit" Version="4.6.1" />
```

**Target Framework:** .NET Framework 4.8
**Visual Studio Target:** [17.0,19.0) (VS 2022 & 2026)

---

### AsmDude3 (Current Port - INCOMPLETE) ⚠️
**Location:** `VS\CSHARP\asm-dude3\asm-dude3-vsix\`

**Status:** 60% complete, missing critical features

**Missing Features:**
- ❌ Font/Color registry updates
- ❌ Font/Color cache refresh
- ❌ User confirmation dialogs
- ❌ Restart-required tracking
- ❌ Architecture tooltips
- ❌ Numeration parsing (partially fixed)
- ❌ Complete settings save/load flow

**Estimated Work to Complete:** 15-20 hours

---

## Plan B: AsmDude4 Implementation Strategy

### Phase 1: Copy Complete Codebase (30 minutes)

**1.1 Create AsmDude4 Directory Structure**

```bash
# Create new directory
mkdir -p VS/CSHARP/asm-dude4/asm-dude4-vsix

# Copy entire working codebase
cp -r VS/CSHARP/asm-dude2-vsix/* VS/CSHARP/asm-dude4/asm-dude4-vsix/

# Copy LSP server (if using separate server project)
cp -r VS/CSHARP/asm-dude2-ls VS/CSHARP/asm-dude4/asm-dude4-server
```

**1.2 File Inventory (What Gets Copied)**

**VSIX Extension Files:**
- `asm-dude2-vsix.csproj` → `asm-dude4-vsix.csproj`
- `AsmDude2Package.cs` → `AsmDude4Package.cs`
- `AsmLanguageClient.cs` (LSP integration)
- `source.extension.vsixmanifest`
- `OptionsPage/` (complete options page implementation)
  - `AsmDudeOptionsPage.cs` (with SaveAsync, font updates)
  - `AsmDudeOptionPageUI.xaml`
  - `AsmDudeOptionPageUI.xaml.cs`
- `Settings.Designer.cs` (all 157 settings)
- `Settings.settings`
- `SyntaxHighlighting/` (classification definitions)
- `Tools/` (AsmDudeToolsStatic, ApplicationInformation)
- `Properties/` (Guids.cs, AssemblyInfo.cs)

**LSP Server Files:**
- `asm-dude2-ls/` → `asm-dude4-server/`
- Complete LSP protocol implementation
- Providers (Completion, Hover, SemanticTokens, DocumentHighlight)
- Data stores (MnemonicStore, PerformanceStore)

**Supporting Libraries:**
- `asm-tools-lib-net48` (already compatible)
- No changes needed

---

### Phase 2: Rename and Update Namespaces (30 minutes)

**2.1 Project File Renaming**

```bash
cd VS/CSHARP/asm-dude4/asm-dude4-vsix

# Rename project file
mv asm-dude2-vsix.csproj asm-dude4-vsix.csproj

# Update assembly name and root namespace in csproj
sed -i 's/<RootNamespace>AsmDude2/<RootNamespace>AsmDude4/g' asm-dude4-vsix.csproj
sed -i 's/<AssemblyName>asm-dude2-vsix/<AssemblyName>asm-dude4-vsix/g' asm-dude4-vsix.csproj
```

**2.2 Namespace Updates (Automated)**

```bash
# Update all C# files
find . -name "*.cs" -type f -exec sed -i 's/namespace AsmDude2/namespace AsmDude4/g' {} \;
find . -name "*.cs" -type f -exec sed -i 's/using AsmDude2/using AsmDude4/g' {} \;

# Update XAML files
find . -name "*.xaml" -type f -exec sed -i 's/clr-namespace:AsmDude2/clr-namespace:AsmDude4/g' {} \;
find . -name "*.xaml" -type f -exec sed -i 's/x:Class="AsmDude2/x:Class="AsmDude4/g' {} \;
```

**2.3 Settings File Updates**

```xml
<!-- Settings.settings -->
<SettingsFile ...
  GeneratedClassNamespace="AsmDude4"
  GeneratedClassName="Settings">
```

**2.4 VSIX Manifest Updates**

```xml
<!-- source.extension.vsixmanifest -->
<Identity Id="AsmDude4.Henk-Jan-Lebbink.12345678" Version="4.0.0" .../>
<DisplayName>AsmDude4</DisplayName>
<Description>Assembly language extension for Visual Studio 2022/2026 (LSP-based)</Description>
```

**2.5 GUID Updates**

```csharp
// Properties/Guids.cs
public const string PackageGuidString = "NEW-GUID-HERE"; // Generate new GUID

// Update in AsmDude4Package.cs
[Guid(Guids.GuidPackageAsmDude4)]
```

**Generate New GUIDs:**
```bash
# In PowerShell
[guid]::NewGuid().ToString()
# Or online: https://www.guidgenerator.com/
```

---

### Phase 3: Upgrade LanguageServer.Client Library (1 hour)

**3.1 Update Package Reference**

**Current (asmdude2-vsix):**
```xml
<PackageReference Include="Microsoft.VisualStudio.LanguageServer.Protocol" Version="17.2.8" />
```

**Target (asmdude4-vsix):**
```xml
<PackageReference Include="Microsoft.VisualStudio.LanguageServer.Protocol" Version="17.14.60" />
```

**3.2 Build and Identify Breaking Changes**

```bash
cd VS/CSHARP/asm-dude4/asm-dude4-vsix
dotnet build > build-errors.txt 2>&1
```

**Expected Breaking Changes:**

**Type 1: Protocol Type Changes**
- Namespace changes (Microsoft.VisualStudio.LanguageServer.Protocol → Microsoft.VisualStudio.LanguageServer.Protocol.Internal)
- Type renames (Position, Range, TextDocumentIdentifier)
- Property renames or type changes

**Type 2: Interface Changes**
- ILanguageClient interface method signature changes
- New required methods/properties
- Deprecated method removal

**Type 3: Connection/Activation Changes**
- ActivateAsync signature changes
- Connection creation changes

**3.3 Fix Breaking Changes Systematically**

**Strategy:**
1. **Compile** → Note first error
2. **Fix** → Use new API/type
3. **Repeat** until build succeeds

**Common Fixes:**

**Example 1: Type Import Changes**
```csharp
// Old (17.2.8)
using Microsoft.VisualStudio.LanguageServer.Protocol;

// New (17.14.60) - May need to add
using Microsoft.VisualStudio.LanguageServer.Protocol.Internal;
```

**Example 2: Position/Range Types**
```csharp
// If Position type changed, update all usages
// Check intellisense for new constructor/property names
```

**Example 3: ILanguageClient Interface**
```csharp
// Compare old vs new interface definition
// Add any new required members
// Update method signatures to match new interface
```

**3.4 Test Compilation**

```bash
# Should have ZERO errors after fixes
dotnet build
# Expected: Build succeeded. 0 Error(s)
```

---

### Phase 4: Update Solution and Project References (15 minutes)

**4.1 Add to Solution**

```xml
<!-- AsmDude.sln -->
<Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "asm-dude4-vsix", "CSHARP\asm-dude4\asm-dude4-vsix\asm-dude4-vsix.csproj", "{NEW-GUID}"
</Project>
```

**4.2 Project References**

Ensure references to shared libraries:
```xml
<ProjectReference Include="..\..\asm-tools-lib-net48\asm-tools-lib-net48.csproj" />
```

**4.3 LSP Server Reference**

If using separate LSP server project:
```xml
<!-- In asm-dude4-vsix.csproj -->
<ProjectReference Include="..\asm-dude4-server\asm-dude4-server.csproj" />
```

---

### Phase 5: Testing and Validation (1 hour)

**5.1 Build Validation**

```bash
# Clean build
dotnet clean
dotnet build --no-incremental

# Expected output:
# Build succeeded.
#     0 Error(s)
#     X Warning(s)  (warnings are OK)
```

**5.2 Deployment Test**

**In Visual Studio:**
1. Open `VS\AsmDude.sln`
2. Set `asm-dude4-vsix` as startup project
3. Press **F5**
4. **Expected:** Experimental VS instance launches without errors

**5.3 Functionality Checklist**

**Extension Loading:**
- [ ] Extension loads in experimental instance
- [ ] No errors in Output → Debug window
- [ ] LSP server process starts (check Task Manager: `asm-dude4-server.exe`)

**Options Page:**
- [ ] `Tools → Options → AsmDude4 → General` opens
- [ ] All 157 settings visible
- [ ] Colors displayed correctly
- [ ] Architecture checkboxes present (60+)
- [ ] Click "Apply" → No errors
- [ ] Close and reopen → Settings persisted

**Syntax Highlighting:**
- [ ] Open/create `.asm` file
- [ ] Mnemonics colored (Lavender by default)
- [ ] Registers colored (MistyRose by default)
- [ ] Comments colored (PaleGreen by default)
- [ ] Change color in Options → Apply → Verify color updates

**LSP Features:**
- [ ] Hover over mnemonic → Tooltip appears with description
- [ ] Code completion (Ctrl+Space) → Suggestions appear
- [ ] Signature help (Ctrl+Shift+Space) → Parameter info appears

**Settings Save/Restart:**
- [ ] Change a setting → Click Apply
- [ ] Debug Output shows: "RestartServerAsync called"
- [ ] LSP server restarts successfully
- [ ] Settings take effect immediately

---

## Phase 6: Documentation and Cleanup (30 minutes)

**6.1 Update README**

Add section:
```markdown
## AsmDude4 (Recommended)

Latest version using modern LSP client library (17.14.60).

**Location:** `VS\CSHARP\asm-dude4\`

**Features:**
- Full LSP support (completion, hover, signature help)
- Complete options page with 157 settings
- Font/color customization
- Performance data integration
- Visual Studio 2022 & 2026 compatible

**Build:**
```bash
dotnet build VS/CSHARP/asm-dude4/asm-dude4-vsix/asm-dude4-vsix.csproj
```

**Run:**
Open `VS/AsmDude.sln`, set `asm-dude4-vsix` as startup project, press F5.
```

**6.2 Create Migration Guide**

Document: `MIGRATION_ASMDUDE2_TO_ASMDUDE4.md`

**6.3 Update CLAUDE.md**

```markdown
## Active Projects

- **asm-dude4-vsix**: Main extension for VS 2022/2026 (RECOMMENDED)
  - Uses LanguageServer.Client 17.14.60
  - Complete feature parity with asmdude2
  - All functionality tested and working

- **asm-dude3-vsix**: Incomplete port (DO NOT USE)
  - Missing critical features
  - Use asm-dude4 instead

- **asm-dude2-vsix**: Legacy working version
  - Uses LanguageServer.Client 17.2.8
  - Superseded by asm-dude4
```

---

## Risk Analysis and Mitigation

### Risk 1: Breaking API Changes in 17.14.60
**Probability:** Medium
**Impact:** Medium
**Mitigation:**
- Version 17.2.8 → 17.14.60 is not a major version jump
- API should be mostly compatible
- Breaking changes will be caught at compile time
- Fix systematically one error at a time

### Risk 2: Runtime Behavior Changes
**Probability:** Low
**Impact:** Medium
**Mitigation:**
- Thoroughly test all LSP features after upgrade
- Compare behavior with asmdude2-vsix side-by-side
- Keep asmdude2-vsix as reference implementation

### Risk 3: Package Dependencies Conflicts
**Probability:** Low
**Impact:** Low
**Mitigation:**
- Use same StreamJsonRpc version (2.22.23)
- Use same VS SDK version (17.14.40265)
- Only change LSP Protocol package

---

## Comparison: Plan A vs. Plan B

| Aspect | Plan A (Complete AsmDude3) | Plan B (AsmDude4 from AsmDude2) |
|--------|---------------------------|--------------------------------|
| **Estimated Effort** | 15-20 hours | 2-3 hours |
| **Risk** | High (missing functionality) | Low (proven codebase) |
| **Features** | Incomplete (60%) | Complete (100%) |
| **Testing Needed** | Extensive | Moderate |
| **Maintenance** | High (new codebase) | Low (proven patterns) |
| **LSP Client Version** | 17.14.60 ✅ | 17.14.60 ✅ |
| **All Features Working** | ❌ No | ✅ Yes |

**Recommendation:** **Plan B (AsmDude4)** is strongly recommended.

---

## Implementation Timeline

### Day 1 (2-3 hours)
- **Phase 1:** Copy codebase (30 min)
- **Phase 2:** Rename/update namespaces (30 min)
- **Phase 3:** Upgrade LSP library + fix breaks (1 hour)
- **Phase 4:** Update solution (15 min)
- **Phase 5:** Testing (1 hour)

### Day 2 (Optional - Polish)
- **Phase 6:** Documentation (30 min)
- Additional testing
- Performance validation

---

## Success Criteria

**AsmDude4 is successful when:**
1. ✅ Builds with 0 errors
2. ✅ All 157 settings save/load correctly
3. ✅ Syntax highlighting works for MASM, NASM Intel, NASM AT&T
4. ✅ LSP features work (completion, hover, signature help)
5. ✅ Font/color customization works and persists
6. ✅ Options page displays all controls correctly
7. ✅ LSP server restarts when settings change
8. ✅ No regressions from asmdude2-vsix

---

## Rollback Plan

If Plan B fails:
1. **Keep asmdude2-vsix as working version**
2. Document issues encountered with 17.14.60 upgrade
3. Consider alternative: Stay on 17.2.8 and focus on stability

**Safety Net:** asmdude2-vsix remains untouched and working throughout Plan B execution.

---

## Next Steps

1. **Decision Point:** Approve Plan B execution
2. **Execute Phase 1-2:** Copy and rename (1 hour)
3. **Execute Phase 3:** Upgrade library (1 hour)
4. **Test and Validate:** Full functionality check (1 hour)
5. **Deploy:** If successful, deprecate asmdude3, promote asmdude4

---

## Appendix A: Key Files to Monitor During Upgrade

**Files Most Likely to Need Updates:**

1. **AsmLanguageClient.cs**
   - ActivateAsync signature
   - Connection creation
   - Protocol type references

2. **Providers/*.cs** (if in VSIX)
   - CompletionProvider
   - HoverProvider
   - SemanticTokensProvider

3. **OptionsPage/AsmDudeOptionsPage.cs**
   - No changes expected (Settings logic unchanged)

4. **Settings.Designer.cs**
   - Auto-generated, should not change

---

## Appendix B: Package Version Matrix

| Package | AsmDude2 | AsmDude4 Target |
|---------|----------|-----------------|
| Microsoft.VisualStudio.LanguageServer.Protocol | 17.2.8 | **17.14.60** |
| StreamJsonRpc | 2.22.23 | 2.22.23 (no change) |
| Microsoft.VisualStudio.SDK | 17.14.40265 | 17.14.40265 (no change) |
| Microsoft.VSSDK.BuildTools | 17.12.40391 | 17.12.40391 (no change) |
| Extended.Wpf.Toolkit | 4.6.1 | 4.6.1 (no change) |

---

## Appendix C: Command Reference

**Copy and Create AsmDude4:**
```bash
# From repository root
mkdir -p VS/CSHARP/asm-dude4
cp -r VS/CSHARP/asm-dude2-vsix VS/CSHARP/asm-dude4/asm-dude4-vsix

cd VS/CSHARP/asm-dude4/asm-dude4-vsix

# Rename project
mv asm-dude2-vsix.csproj asm-dude4-vsix.csproj

# Update namespaces (all .cs files)
find . -name "*.cs" -type f -exec sed -i 's/namespace AsmDude2/namespace AsmDude4/g' {} \;
find . -name "*.cs" -type f -exec sed -i 's/using AsmDude2/using AsmDude4/g' {} \;
find . -name "*.cs" -type f -exec sed -i 's/AsmDude2\./AsmDude4./g' {} \;

# Update XAML files
find . -name "*.xaml" -type f -exec sed -i 's/AsmDude2/AsmDude4/g' {} \;

# Update project file
sed -i 's/AsmDude2/AsmDude4/g' asm-dude4-vsix.csproj

# Update Settings namespace
sed -i 's/AsmDude2/AsmDude4/g' Settings.settings

# Upgrade LSP package
sed -i 's/Version="17.2.8"/Version="17.14.60"/g' asm-dude4-vsix.csproj

# Build
dotnet build
```

**Test Build:**
```bash
dotnet clean
dotnet build --no-incremental
```

---

## Conclusion

**Plan B (AsmDude4 from AsmDude2)** provides the fastest, lowest-risk path to a modern, fully-functional extension using the latest LSP client library.

**Estimated Total Time:** 2-3 hours
**Success Probability:** 90%+
**Maintenance Burden:** Low (proven codebase)

**Recommendation:** **PROCEED WITH PLAN B**

---

**Document Version:** 1.0
**Date:** 2025-12-11
**Author:** Claude Code Assistant
**Status:** Ready for Execution
