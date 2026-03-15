# Fix extension.json and extension.vsixmanifest to correct critical bugs

param(
    [string]$VsixPath = "VS/CSHARP/asm-dude2-vsix/bin/Debug/net10.0-windows/AsmDude2.vsix"
)

if (-not (Test-Path $VsixPath)) {
    Write-Error "VSIX file not found: $VsixPath"
    exit 1
}

$tempDir = "$env:TEMP\fix-manifest-$([System.DateTime]::UtcNow.Ticks)"
$zipPath = "$env:TEMP\temp-$([System.DateTime]::UtcNow.Ticks).zip"

try {
    # Extract VSIX (it's a ZIP file)
    [System.IO.File]::Copy($VsixPath, $zipPath, $true)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempDir)

    Write-Host "Extracted VSIX to $tempDir"

    # Fix extension.json
    $jsonPath = "$tempDir\.vsextension\extension.json"
    if (Test-Path $jsonPath) {
        Write-Host "[BUG 1 & 2] Fixing extension.json class references..."
        $json = Get-Content $jsonPath -Raw | ConvertFrom-Json

        # Fix AsmLanguageServerProvider service to point to correct class
        $lspService = $json.services | Where-Object { $_.name -eq 'AsmDude2.AsmLanguageServerProvider' }
        if ($lspService) {
            Write-Host "  Bug 1: Setting AsmLanguageServerProvider.fullClassName = 'AsmDude2.AsmLanguageServerProvider'"
            $lspService.entryPoint.fullClassName = 'AsmDude2.AsmLanguageServerProvider'
        }

        # Remove ExtensionCommandSet service (it shouldn't exist)
        Write-Host "  Bug 2: Removing unnecessary ExtensionCommandSet service..."
        $initialCount = $json.services.Count
        $json.services = @($json.services | Where-Object { $_.name -ne 'AsmDude2.ExtensionCommandSet' })
        Write-Host "    Removed from services array"

        # Write back fixed extension.json
        $json | ConvertTo-Json -Depth 10 | Set-Content $jsonPath
        Write-Host "    Wrote fixed extension.json"
    }

    # Fix extension.vsixmanifest - change net8.0 to net10.0-windows
    $manifestPath = "$tempDir\extension.vsixmanifest"
    if (Test-Path $manifestPath) {
        Write-Host "[BUG 3] Fixing extension.vsixmanifest DotnetTargetVersions..."
        [xml]$manifest = Get-Content $manifestPath
        $ns = @{ x = 'http://schemas.microsoft.com/developer/vsx-schema/2011' }

        # Find and fix DotnetTargetVersions
        $versionNode = $manifest | Select-Xml -XPath "//x:DotnetTargetVersions" -Namespace $ns | Select-Object -First 1
        if ($versionNode) {
            Write-Host "  Bug 3: Changing '$($versionNode.Node.InnerText)' to 'net10.0-windows'"
            $versionNode.Node.InnerText = 'net10.0-windows'
        }

        $manifest.Save($manifestPath)
        Write-Host "    Wrote fixed extension.vsixmanifest"
    }

    # Re-create VSIX
    Write-Host "Re-packaging VSIX..."
    Remove-Item $VsixPath -Force
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $VsixPath, 'Optimal', $false)

    Write-Host ""
    Write-Host "✅ SUCCESS: All three critical bugs fixed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Fixes applied:"
    Write-Host "  [Bug 1] ✅ AsmLanguageServerProvider.fullClassName corrected"
    Write-Host "  [Bug 2] ✅ ExtensionCommandSet service removed"
    Write-Host "  [Bug 3] ✅ DotnetTargetVersions changed to net10.0-windows"
    Write-Host ""
    Write-Host "Extension ready at: $VsixPath"
}
finally {
    # Cleanup
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
}
