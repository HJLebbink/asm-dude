# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET net10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.

2. Ensure that the SDK version specified in global.json files is compatible with the .NET net10.0 upgrade.

3. Upgrade `CSHARP\asm-tools-lib\asm-tools-lib.csproj`

4. Upgrade `CSHARP\asm-sim-lib\asm-sim-lib.csproj`

5. Upgrade `CSHARP\asm-dude2-ls-lib\asm-dude2-ls-lib.csproj`

6. Upgrade `CSHARP\asm-sim-tests\asm-sim-tests.csproj`

7. Upgrade `CSHARP\asm-tools-tests\asm-tools-tests.csproj`

8. Upgrade `CPP\ConsoleTest\ConsoleTest\ConsoleTest.vcxproj`

9. Upgrade `CSHARP\asm-sim-main\asm-sim-main.csproj`

10. Upgrade `CSHARP\asm-dude2-ls\asm-dude2-ls.csproj`

11. Upgrade `CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj`

12. Upgrade `CSHARP\asm-annotate\asm-annotate.csproj`

13. Upgrade `CSHARP\intel-doc-2-data\intel-doc-2-data.csproj`

14. Upgrade `Python\intel-doc-2-md\intel-doc-2-md.pyproj`


## Settings

This section contains settings and data used by execution steps.

### Excluded projects

Table below contains projects that do belong to the dependency graph for selected projects and should not be included in the upgrade.

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|


### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                        | Current Version    | New Version  | Description                                   |
|:------------------------------------|:------------------:|:------------:|:----------------------------------------------|
| Microsoft.VisualStudio.SDK          | 17.14.40265        | 16.0.208     | Incompatible with project; recommendation from analysis to use 16.0.208 |


### Project upgrade details
This section contains details about each project upgrade and modifications that need to be done in the project.

#### CSHARP\\asm-tools-lib\\asm-tools-lib.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\asm-sim-lib\\asm-sim-lib.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\asm-dude2-ls-lib\\asm-dude2-ls-lib.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\asm-sim-tests\\asm-sim-tests.csproj modifications

Project properties changes:
  - Review test project's TargetFramework and test SDK compatibility with net10.0.

NuGet packages changes:
  - Update test-related packages (MSTest, Microsoft.NET.Test.Sdk, coverlet) to versions compatible with net10.0 if needed.

Other changes:
  - Run tests after upgrading dependent projects.

#### CSHARP\\asm-tools-tests\\asm-tools-tests.csproj modifications

Project properties changes:
  - Current TargetFramework: net10.0-windows — verify runtime-specific settings remain correct after upgrade.

NuGet packages changes:
  - Update test-related packages as needed.

Other changes:
  - Run tests after upgrading dependent projects.

#### CPP\\ConsoleTest\\ConsoleTest\\ConsoleTest.vcxproj modifications

Project properties changes:
  - Convert project file to SDK-style per analysis recommendation.

Other changes:
  - Validate C++ build tools and project settings after conversion.

#### CSHARP\\asm-sim-main\\asm-sim-main.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\asm-dude2-ls\\asm-dude2-ls.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\asm-dude2-vsix\\asm-dude2-vsix.csproj modifications

Project properties changes:
  - Target framework should be changed from `net48` to `net10.0-windows`.

NuGet packages changes:
  - `Microsoft.VisualStudio.SDK` should be changed from `17.14.40265` to `16.0.208` as recommended by analysis (verify compatibility with VS extension tooling).

Other changes:
  - Review VS extension compatibility and adjust extension manifest and APIs as needed.

#### CSHARP\\asm-annotate\\asm-annotate.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### CSHARP\\intel-doc-2-data\\intel-doc-2-data.csproj modifications

Project properties changes:
  - Review project TargetFramework; ensure it's compatible with net10.0 or multi-target as needed.

NuGet packages changes:
  - Review referenced NuGet packages and update to versions compatible with net10.0.

Other changes:
  - Build project on net10.0 and fix any compilation or API compatibility issues discovered.

#### Python\\intel-doc-2-md\\intel-doc-2-md.pyproj modifications

Project properties changes:
  - Convert project file to SDK-style and update target framework from `.NETFramework,Version=v4.7.2` to `net10.0` as recommended by analysis.

Other changes:
  - Validate Python/tooling integration after conversion.

