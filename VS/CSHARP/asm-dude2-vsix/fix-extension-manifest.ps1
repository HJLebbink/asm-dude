# Fix extension.json and extension.vsixmanifest to correct class references and versions

param(
    [string]$VsixPath
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

    # Fix extension.json
    $jsonPath = "$tempDir\.vsextension\extension.json"
    $json = Get-Content $jsonPath -Raw | ConvertFrom-Json

    # Fix AsmLanguageServerProvider service to point to correct class
    $lspService = $json.services | Where-Object { $_.name -eq 'AsmDude2.AsmLanguageServerProvider' }
    if ($lspService) {
        Write-Host "Fixing AsmLanguageServerProvider class reference..."
        $lspService.entryPoint.fullClassName = 'AsmDude2.AsmLanguageServerProvider'
    }

    # Remove ExtensionCommandSet service (it shouldn't exist)
    $initialCount = $json.services.Count
    $json.services = @($json.services | Where-Object { $_.name -ne 'AsmDude2.ExtensionCommandSet' })
    if ($json.services.Count -lt $initialCount) {
        Write-Host "Removed ExtensionCommandSet service"
    }

    # Write back fixed extension.json
    $json | ConvertTo-Json -Depth 10 | Set-Content $jsonPath

    # Fix extension.vsixmanifest - change net8.0 to net10.0
    $manifestPath = "$tempDir\extension.vsixmanifest"
    if (Test-Path $manifestPath) {
        Write-Host "Fixing extension.vsixmanifest version..."
        [xml]$manifest = Get-Content $manifestPath
        $ns = @{ x = 'http://schemas.microsoft.com/developer/vsx-schema/2011' }

        # Find and fix DotnetTargetVersions
        $versionNode = $manifest | Select-Xml -XPath "//x:DotnetTargetVersions" -Namespace $ns | Select-Object -First 1
        if ($versionNode) {
            Write-Host "  Changing '$($versionNode.Node.InnerText)' to 'net10.0'"
            $versionNode.Node.InnerText = 'net10.0'
        }

        $manifest.Save($manifestPath)
    }

    # Re-create VSIX
    Remove-Item $VsixPath -Force
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $VsixPath, 'Optimal', $false)

    Write-Host "Extension manifest fixed successfully"
}
finally {
    # Cleanup
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
}
