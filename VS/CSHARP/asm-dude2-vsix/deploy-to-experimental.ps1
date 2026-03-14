# Deploy extension to experimental instance

param(
    [string]$VsixPath
)

if (-not (Test-Path $VsixPath)) {
    Write-Error "VSIX file not found: $VsixPath"
    exit 1
}

# Use Local folder (not Roaming) - VS 2026 uses Local\Microsoft\VisualStudio for extension metadata
$expRoot = "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp"
$extPath = "$expRoot\Extensions\HenkJanLebbink.AsmDude2\1.0.0"

Write-Host "Deploying extension to experimental instance..."
Write-Host "  From: $VsixPath"
Write-Host "  To: $extPath"

# Clean up old deployment
if (Test-Path $extPath) {
    Write-Host "  Removing old deployment..."
    Remove-Item $extPath -Recurse -Force
}

# Clear extension metadata caches to force VS to rescan
Write-Host "  Clearing extension metadata caches..."
@(
    "$expRoot\ExtensionMetadata2.0.mpack",
    "$expRoot\ExtensionMetadataCache.mpack",
    "$expRoot\extensions.configurationchanged"
) | ForEach-Object {
    if (Test-Path $_) {
        Remove-Item $_ -Force -ErrorAction SilentlyContinue
    }
}

# Extract and deploy new VSIX
$tempDir = "$env:TEMP\deploy-vsix-$([System.DateTime]::UtcNow.Ticks)"
$zipPath = "$env:TEMP\temp-vsix-$([System.DateTime]::UtcNow.Ticks).zip"

try {
    [System.IO.File]::Copy($VsixPath, $zipPath, $true)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempDir)

    mkdir $extPath -Force | Out-Null
    Copy-Item "$tempDir\*" -Destination $extPath -Recurse -Force

    Write-Host "Extension deployed successfully to:"
    Write-Host "  $extPath"
}
catch {
    Write-Error "Deployment failed: $_"
    exit 1
}
finally {
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
}
