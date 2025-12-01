# AsmDude2 Migration to VisualStudio.Extensibility

This document describes the migration of AsmDude2 from the legacy VSSDK model to the modern VisualStudio.Extensibility SDK.

## Why Migrate?

The legacy `Microsoft.VisualStudio.LanguageServer.Protocol` package (version 17.8.9-preview) is no longer available on public NuGet feeds, making the project unbuildable for new contributors. Additionally, Microsoft has introduced a new extensibility model that offers significant advantages:

### Benefits of VisualStudio.Extensibility

- **Out-of-Process Architecture**: Extensions run outside VS process, preventing crashes
- **No Restart Required**: Install/update extensions without restarting Visual Studio
- **Modern .NET**: Use .NET 8.0 instead of .NET Framework 4.8
- **Latest Dependencies**: Uses StreamJsonRpc 2.22.23 (vs 2.16.36 in old model)
- **Future-Proof**: Official path forward for VS 2026 and beyond
- **Better Performance**: Improved isolation and resource management

## Architecture Changes

### Old Model (asm-dude2-vsix)
- **Framework**: .NET Framework 4.8
- **Pattern**: MEF-based `ILanguageClient` implementation
- **Packages**:
  - `Microsoft.VisualStudio.LanguageServer.Protocol` 17.8.9-preview (unavailable)
  - `Microsoft.VisualStudio.SDK` 17.7.37357
  - `StreamJsonRpc` 2.16.36

### New Model (asm-dude2-ext)
- **Framework**: .NET 8.0
- **Pattern**: `LanguageServerProvider` from VisualStudio.Extensibility
- **Packages**:
  - `Microsoft.VisualStudio.Extensibility.Sdk` 17.14.40608
  - `StreamJsonRpc` 2.22.23 (latest)

## Key Component Mapping

| Old VSSDK | New VisualStudio.Extensibility |
|-----------|--------------------------------|
| `ILanguageClient` | `LanguageServerProvider` |
| `ActivateAsync()` | `CreateServerConnectionAsync()` |
| MEF `[Export]` | `[VisualStudioContribution]` |
| VSIX manifest | `ExtensionConfiguration` |
| `.vsixmanifest` XML | C# metadata in `Extension` class |
| In-process MEF | Out-of-process RPC |

## File Structure

### New Extension (asm-dude2-ext/)
```
asm-dude2-ext/
├── Extension.cs                         # Main extension entry point
├── AsmLanguageServerProvider.cs         # LSP connection handler
├── AsmLanguageServerConfiguration.cs    # Settings/initialization options
├── Resources/                           # Data files (copied from vsix)
│   ├── AsmDudeData.xml
│   ├── signature-hand-1.txt
│   └── signature-may2019.txt
└── asm-dude2-ext.csproj                # .NET 8.0 project file
```

## LSP Server (Unchanged)

The `asm-dude2-ls` language server remains unchanged. It's still a .NET 7.0 console application that:
- Communicates via Named Pipes
- Implements the Language Server Protocol
- Provides syntax highlighting, completion, hover, etc.

The new extension just launches it differently using the modern API.

## Build Instructions

### Prerequisites
- Visual Studio 2022 (17.9 or later)
- .NET 8.0 SDK
- VS Extensibility workload

### Build Steps

```bash
# Build the LSP server (unchanged)
dotnet build VS/CSHARP/asm-dude2-ls/asm-dude2-ls.csproj

# Build the new extension
dotnet build VS/CSHARP/asm-dude2-ext/asm-dude2-ext.csproj

# Or build entire solution
dotnet build VS/AsmDude.sln
```

### Debug/Test

1. Open VS/AsmDude.sln in Visual Studio 2022
2. Set `asm-dude2-ext` as startup project
3. Press F5
4. VS experimental instance will launch with the extension loaded
5. Open an .asm file to test

## Configuration/Settings

**Current Status**: The new model uses hardcoded default settings in `AsmLanguageServerConfiguration.GetInitializationOptions()`.

**Future**: When VisualStudio.Extensibility adds options page support, we can migrate the old settings system from `AsmDudeOptionsPage.cs`.

## Migration Checklist

- [x] Create new .NET 8.0 project with VisualStudio.Extensibility SDK
- [x] Implement `Extension` class with metadata
- [x] Implement `LanguageServerProvider` to replace `ILanguageClient`
- [x] Port LSP connection logic (Named Pipes)
- [x] Copy resource files (AsmDudeData.xml, signatures)
- [x] Configure LSP initialization options
- [ ] Add project to solution file
- [ ] Test extension in VS 2022
- [ ] Test extension in VS 2026 (when available)
- [ ] Publish to Visual Studio Marketplace
- [ ] Archive old asm-dude2-vsix project

## Known Limitations

1. **Options UI**: The old extension had a settings page. VisualStudio.Extensibility doesn't yet support options pages, so settings are currently hardcoded.

2. **Syntax Highlighting**: The old extension had custom classification formats. The new model relies on the LSP server for semantic tokens.

3. **Disassembly Window**: Custom content type for disassembly window may need additional work.

## Backward Compatibility

The old `asm-dude2-vsix` extension will remain in the repository for reference but won't be actively maintained. Users on VS 2022 can continue using it until they upgrade.

## Testing Plan

1. **Basic Functionality**
   - Open .asm, .cod, .inc, .s files
   - Verify syntax highlighting works
   - Test code completion
   - Test signature help
   - Test hover info

2. **LSP Server**
   - Verify server starts correctly
   - Check named pipe communication
   - Test multiple files open simultaneously
   - Test server restart scenario

3. **Performance**
   - Test with large files (10k+ lines)
   - Verify no VS crashes
   - Check memory usage

4. **Installation**
   - Install extension from VSIX
   - Verify no restart required
   - Update extension
   - Uninstall extension

## Rollback Plan

If issues arise, the old VSSDK extension can be restored by:
1. Downgrading LSP packages to 17.2.8 (last stable version)
2. Updating StreamJsonRpc to 2.22.23
3. Rebuilding asm-dude2-vsix

## Resources

- [VisualStudio.Extensibility Documentation](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/)
- [Language Server Provider Guide](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/language-server-provider/language-server-provider)
- [VSExtensibility GitHub](https://github.com/microsoft/VSExtensibility)
- [Migration Guide](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/update-visual-studio-extension)

## Contact

For questions or issues with the migration, open an issue at:
https://github.com/HJLebbink/asm-dude/issues
