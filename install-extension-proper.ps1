# Properly install extension using VSIXInstaller.exe (official method)

param(
    [string]$VsixPath
)

if (-not (Test-Path $VsixPath)) {
    Write-Error "VSIX file not found: $VsixPath"
    exit 1
}

$vsixInstallerPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe"

if (-not (Test-Path $vsixInstallerPath)) {
    Write-Error "VSIXInstaller.exe not found at: $vsixInstallerPath"
    exit 1
}

Write-Host "Installing extension using VSIXInstaller..." -ForegroundColor Cyan
Write-Host "  VSIX: $VsixPath"
Write-Host "  Installer: $vsixInstallerPath"
Write-Host ""

# Kill any running VS instances
Write-Host "Stopping any running VS instances..."
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Install to experimental instance
Write-Host "Running VSIXInstaller..."
& $vsixInstallerPath /rootSuffix Exp $VsixPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "Extension installed successfully" -ForegroundColor Green
}
else {
    Write-Host "Installation failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Extension is now properly registered with VS."
Write-Host ""
Write-Host "Launching VS experimental instance..."
$devenvPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"
Start-Process -FilePath $devenvPath -ArgumentList "/rootSuffix Exp"

Write-Host "VS launched. Check if extension loads now."
