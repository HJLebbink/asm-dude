# Fix Plan for `dotnet10` Branch

## Current State

Branch `dotnet10` at commit `5a309e0` ("update, but broken again") has 7 bugs preventing the
.NET 10 OOP extensibility VSIX from working. The LSP server (`asm-dude2-ls`) is already working
and does NOT need changes.

## Files on `dotnet10` branch (VSIX only)

```
VS/CSHARP/asm-dude2-vsix/
  AsmDocumentTypes.cs          <- BUG: missing BaseDocumentType, wrong location
  AsmLanguageServerProvider.cs <- BUG: wrong constructor, named pipes, wrong exe name
  Extension.cs                 <- OK (minor: debug logging clutter)
  Properties/launchSettings.json <- BUG: wrong commandName
  asm-dude2-vsix.csproj        <- BUG: wrong SDK, missing packages, wrong bundling
  deploy-to-experimental.ps1   <- can keep (utility script)
  fix-build-output.ps1         <- can keep (utility script)
  fix-extension-manifest.ps1   <- can keep (utility script)
```

## Verified API Signatures (from NuGet XML docs)

These are CONFIRMED correct from `Microsoft.VisualStudio.Extensibility` 17.14.2098:

| Type | Constructor/Usage |
|------|-------------------|
| `ExtensionMetadata` | `(string id, Version version, string publisherName, string displayName, string description)` — 5 positional params |
| `LanguageServerProviderConfiguration` | `(string displayName, DocumentFilter[] appliesTo)` — 2 params only |
| `LanguageServerProvider` | Parameterless base constructor. SDK creates instances via DI. |
| `DocumentTypeConfiguration` | `(string name)` with properties `FileExtensions`, `BaseDocumentType` |
| `LanguageServerBaseDocumentType` | Static field on `LanguageServerProvider` — only accessible from subclass |
| `DocumentFilter` | `DocumentFilter.FromDocumentType(DocumentTypeConfiguration)` |
| `SettingCategory` | `(string id, string displayName, SettingCategory? parent)` — IDs must be lowercase letters+numbers only |

## Fix 1: Delete `AsmDocumentTypes.cs`

**Problem**: `LanguageServerBaseDocumentType` is a static field on `LanguageServerProvider`.
A separate static class cannot access it. Also, the current file groups all extensions into
one document type — they should be separate for proper filtering.

**Fix**: Delete `AsmDocumentTypes.cs`. Move document type definitions INTO `AsmLanguageServerProvider`
(the subclass), where `LanguageServerBaseDocumentType` is accessible.

## Fix 2: Rewrite `AsmLanguageServerProvider.cs`

**Problems**:
1. Wrong constructor: `(ExtensionCore, VisualStudioExtensibility)` — base class is parameterless
2. Uses named pipes — should use stdio (`Process.RedirectStandardInput/Output`)
3. References `AsmDude2.LSP.exe` — actual name is `asm-dude2-ls.exe`
4. Overly complex path searching — `Assembly.Location` is sufficient

**Correct implementation**:

```csharp
namespace AsmDude2;

using System.Diagnostics;
using System.IO.Pipelines;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.LanguageServer;

[VisualStudioContribution]
internal class AsmLanguageServerProvider : LanguageServerProvider
{
    // Document types MUST be inside this class to access LanguageServerBaseDocumentType
    [VisualStudioContribution]
    internal static DocumentTypeConfiguration AsmDocumentType => new("asm")
    {
        FileExtensions = [".asm"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration CodDocumentType => new("cod")
    {
        FileExtensions = [".cod"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration IncDocumentType => new("inc")
    {
        FileExtensions = [".inc"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration SDocumentType => new("s")
    {
        FileExtensions = [".s"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration =>
        new("AsmDude2 Language Server",
        [
            DocumentFilter.FromDocumentType(AsmDocumentType),
            DocumentFilter.FromDocumentType(CodDocumentType),
            DocumentFilter.FromDocumentType(IncDocumentType),
            DocumentFilter.FromDocumentType(SDocumentType),
        ]);

    public override Task<IDuplexPipe?> CreateServerConnectionAsync(
        CancellationToken cancellationToken)
    {
        string? extensionDir = Path.GetDirectoryName(
            typeof(AsmLanguageServerProvider).Assembly.Location);
        if (extensionDir is null)
            return Task.FromResult<IDuplexPipe?>(null);

        string lspPath = Path.Combine(extensionDir, "Server", "asm-dude2-ls.exe");
        if (!File.Exists(lspPath))
            return Task.FromResult<IDuplexPipe?>(null);

        var info = new ProcessStartInfo
        {
            FileName = lspPath,
            Arguments = "--stdio",
            WorkingDirectory = Path.GetDirectoryName(lspPath),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = info };
        if (process.Start())
        {
            return Task.FromResult<IDuplexPipe?>(new DuplexPipe(
                PipeReader.Create(process.StandardOutput.BaseStream),
                PipeWriter.Create(process.StandardInput.BaseStream)));
        }

        return Task.FromResult<IDuplexPipe?>(null);
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;
        public PipeWriter Output { get; } = output;
    }
}
```

**Why stdio instead of named pipes**: The LSP server already supports `--stdio` mode
(see `Worker.cs` lines 42-54). In stdio mode, the server reads JSON-RPC from stdin and writes
to stdout. `CreateServerConnectionAsync` returns an `IDuplexPipe` wrapping those streams.
This is simpler, more reliable, and the standard pattern for OOP LSP extensions.

## Fix 3: Clean up `Extension.cs`

**Problem**: Debug file logging is noisy but not harmful. The core logic is correct.

**Fix**: Remove the file-based logging. Keep the `ExtensionConfiguration` as-is (it's correct).

```csharp
namespace AsmDude2;

using Microsoft.VisualStudio.Extensibility;

[VisualStudioContribution]
public class Extension : Microsoft.VisualStudio.Extensibility.Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "AsmDude2.8f5b1c3a-6d7e-4f8b-9c0d-1e2f3a4b5c6d",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Henk-Jan Lebbink",
            displayName: "AsmDude2",
            description: "Syntax highlighting and code assistance for assembly source code (.asm, .cod, .inc, .s) and the Disassembly Window"),
    };
}
```

## Fix 4: Fix `launchSettings.json`

**Problem**: `"commandName": "Executable"` launches devenv.exe without deploying the extension.

**Fix**:
```json
{
  "profiles": {
    "AsmDude2": {
      "commandName": "DebugExtensionHost",
      "commandLineArgs": "/rootSuffix Exp"
    }
  }
}
```

`DebugExtensionHost` is the VS Extensibility SDK's debug launcher. It deploys the extension
to the experimental VS instance before launching. Without it, F5 just opens VS with no extension.

## Fix 5: Fix `asm-dude2-vsix.csproj`

**Problems**:
1. `Sdk="Microsoft.NET.Sdk.WindowsDesktop"` — should be `Microsoft.NET.Sdk`
2. `<UseWPF>true</UseWPF>` — not needed for OOP extensions (they run out-of-process, no WPF)
3. Missing `StreamJsonRpc` and `Nerdbank.Streams` packages
4. `<ProjectReference>` to `asm-tools-lib` — VSIX doesn't need this (only LSP server does)
5. `BundleLspServer` target uses `Copy` after build — doesn't include files in VSIX package
6. Missing `VSEXTPREVIEW_SETTINGS` in `<NoWarn>` (needed if settings are added later)

**Correct csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ExtensionName>AsmDude2</ExtensionName>
    <ExtensionVersion>3.0.0.0</ExtensionVersion>
    <NoWarn>VSEXTPREVIEW_LSP;VSEXTPREVIEW_SETTINGS</NoWarn>
    <ExtensionDeploymentRootSuffix>Exp</ExtensionDeploymentRootSuffix>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.VisualStudio.Extensibility.Sdk" Version="17.14.40608" />
    <PackageReference Include="Microsoft.VisualStudio.Extensibility" Version="17.14.2098" />
    <PackageReference Include="StreamJsonRpc" Version="2.25.9" />
    <PackageReference Include="Nerdbank.Streams" Version="2.13.16" />
  </ItemGroup>

  <ItemGroup>
    <None Update="Resources\AsmDudeData.xml">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInExtension>true</IncludeInExtension>
    </None>
    <None Update="Resources\signature-hand-1.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInExtension>true</IncludeInExtension>
    </None>
    <None Update="Resources\signature-may2019.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInExtension>true</IncludeInExtension>
    </None>
    <None Update="LICENSE.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInExtension>true</IncludeInExtension>
    </None>
    <None Update="AsmDude2.png">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <IncludeInExtension>true</IncludeInExtension>
    </None>
  </ItemGroup>

  <!-- Include LSP server in VSIX -->
  <Target Name="IncludeLanguageServer" BeforeTargets="GetExtensionContentItems">
    <ItemGroup>
      <Content Include="$(MSBuildProjectDirectory)\..\asm-dude2-ls\bin\$(Configuration)\net10.0-windows\**\*.*">
        <Link>Server\%(RecursiveDir)%(Filename)%(Extension)</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <IncludeInExtension>true</IncludeInExtension>
      </Content>
      <Content Include="$(MSBuildProjectDirectory)\..\asm-dude2-ls-lib\Resources\**\*.*">
        <Link>Server\Resources\%(RecursiveDir)%(Filename)%(Extension)</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <IncludeInExtension>true</IncludeInExtension>
      </Content>
    </ItemGroup>
  </Target>

</Project>
```

**Key differences**:
- `BeforeTargets="GetExtensionContentItems"` — runs before VSIX packaging, so files get included
- `<IncludeInExtension>true</IncludeInExtension>` — tells the SDK to bundle these in the VSIX
- No `<ProjectReference>` to `asm-tools-lib` — the VSIX is just a thin shell that launches the LSP server
- `<ExtensionDeploymentRootSuffix>Exp</ExtensionDeploymentRootSuffix>` — enables F5 deployment

## Fix 6: Build the LSP server FIRST

The VSIX `IncludeLanguageServer` target expects the LSP server to be already built at
`../asm-dude2-ls/bin/$(Configuration)/net10.0-windows/`. Build order:

```bash
dotnet build VS/CSHARP/asm-dude2-ls/asm-dude2-ls.csproj
dotnet build VS/CSHARP/asm-dude2-vsix/asm-dude2-vsix.csproj -p:DeployExtension=false
```

Note: `DeployExtension=false` is needed for `dotnet build` from CLI. F5 in VS handles deployment
via `DebugExtensionHost`.

## Execution Steps

1. Switch to `dotnet10` branch
2. Delete `AsmDocumentTypes.cs`
3. Overwrite `AsmLanguageServerProvider.cs` with corrected version
4. Overwrite `Extension.cs` with cleaned version
5. Overwrite `Properties/launchSettings.json` with corrected version
6. Overwrite `asm-dude2-vsix.csproj` with corrected version
7. Build LSP server, then VSIX
8. Verify 0 errors

## What This Does NOT Change

- `asm-dude2-ls/` (LSP server) — already works, no changes needed
- `asm-dude2-ls-lib/` (LSP library) — already works, no changes needed
- `asm-tools-lib/` — no changes
- `asm-sim-lib/` — no changes
- All existing tests continue to pass

## Future Work (NOT in this fix)

- Settings forwarding (AsmSettings.cs + config file pipeline) — Phase 2
- The `net10-migration` branch has working code for this that can be cherry-picked later
