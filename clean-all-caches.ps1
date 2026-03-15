# Clean ALL VS caches to start fresh after reverting commits

Write-Host "Cleaning all Visual Studio caches..." -ForegroundColor Cyan
Write-Host ""

# Kill VS first
Write-Host "Stopping Visual Studio..."
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process VSIXInstaller -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 1. Clean experimental instance caches
Write-Host "[1/6] Clearing experimental instance caches..."
$cacheList = @(
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp\ComponentModelCache",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp\MEFCacheBackup",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp\Extensions\HenkJanLebbink.AsmDude2",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp\ExtensionMetadata2.0.mpack",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp\ExtensionMetadataCache.mpack"
)

$cacheList | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "  Removing: $_"
        Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 2. Clean main VS instance caches
Write-Host "[2/6] Clearing main VS instance caches..."
$mainCaches = @(
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0\ComponentModelCache",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0\MEFCacheBackup"
)

$mainCaches | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "  Removing: $_"
        Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 3. Clean build artifacts
Write-Host "[3/6] Cleaning build artifacts..."
Push-Location "C:\Source\Github\asm-dude\VS\CSHARP"

$buildDirs = @(
    "asm-dude2-vsix\bin",
    "asm-dude2-vsix\obj",
    "asm-dude2-ls\bin",
    "asm-dude2-ls\obj",
    "asm-dude2-ls-lib\bin",
    "asm-dude2-ls-lib\obj",
    "asm-tools-lib\bin",
    "asm-tools-lib\obj"
)

$buildDirs | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "  Removing: $_"
        Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Pop-Location

# 4. Clean solution cache
Write-Host "[4/6] Cleaning solution cache..."
$vsFolderPath = "C:\Source\Github\asm-dude\VS\.vs"
if (Test-Path $vsFolderPath) {
    Write-Host "  Removing: .vs folder"
    Remove-Item $vsFolderPath -Recurse -Force -ErrorAction SilentlyContinue
}

# 5. Clean NuGet cache (optional but recommended)
Write-Host "[5/6] Clearing NuGet cache..."
$nugetCache = Join-Path $env:USERPROFILE ".nuget\packages"
Write-Host "  Note: NuGet cache at: $nugetCache"
Write-Host "  (Not auto-deleting to avoid re-downloading packages)"

# 6. Clean temp logs
Write-Host "[6/6] Clearing temp logs..."
$logFiles = @(
    "$env:TEMP\AsmDude2_Startup.log",
    "$env:TEMP\AsmDude2_LSP_Error.log"
)

$logFiles | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "  Removing: $_"
        Remove-Item $_ -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Done! Cache cleanup complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. dotnet build VS/AsmDude.sln"
Write-Host "  2. Open VS/AsmDude.sln"
Write-Host "  3. Set asm-dude2-vsix as Startup Project"
Write-Host "  4. Press F5"
