# Verify the fixes were applied correctly

$VsixPath = 'VS/CSHARP/asm-dude2-vsix/bin/Debug/net10.0-windows/AsmDude2.vsix'
$tempDir = "$env:TEMP\verify-$([System.DateTime]::UtcNow.Ticks)"
$zipPath = "$env:TEMP\temp-verify-$([System.DateTime]::UtcNow.Ticks).zip"

try {
    [System.IO.File]::Copy($VsixPath, $zipPath, $true)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempDir)

    Write-Host "=== VERIFICATION ===" -ForegroundColor Cyan
    Write-Host ""

    # Check extension.json
    $jsonPath = "$tempDir\.vsextension\extension.json"
    $json = Get-Content $jsonPath -Raw | ConvertFrom-Json

    Write-Host "extension.json services:" -ForegroundColor Yellow
    foreach ($svc in $json.services) {
        Write-Host "  - Name: $($svc.name)"
        Write-Host "    Class: $($svc.entryPoint.fullClassName)"
    }

    Write-Host ""
    $lspService = $json.services | Where-Object { $_.name -eq 'AsmDude2.AsmLanguageServerProvider' }
    if ($lspService -and $lspService.entryPoint.fullClassName -eq 'AsmDude2.AsmLanguageServerProvider') {
        Write-Host "✅ BUG 1 FIXED: Service name matches fullClassName" -ForegroundColor Green
    } else {
        Write-Host "❌ BUG 1 NOT FIXED" -ForegroundColor Red
    }

    if (-not ($json.services | Where-Object { $_.name -eq 'AsmDude2.ExtensionCommandSet' })) {
        Write-Host "✅ BUG 2 FIXED: ExtensionCommandSet removed" -ForegroundColor Green
    } else {
        Write-Host "❌ BUG 2 NOT FIXED" -ForegroundColor Red
    }

    Write-Host ""

    # Check extension.vsixmanifest
    $manifestPath = "$tempDir\extension.vsixmanifest"
    [xml]$manifest = Get-Content $manifestPath
    $ns = @{ x = 'http://schemas.microsoft.com/developer/vsx-schema/2011' }
    $versionNode = $manifest | Select-Xml -XPath "//x:DotnetTargetVersions" -Namespace $ns | Select-Object -First 1

    Write-Host "extension.vsixmanifest:" -ForegroundColor Yellow
    Write-Host "  DotnetTargetVersions: $($versionNode.Node.InnerText)"

    if ($versionNode.Node.InnerText -eq 'net10.0-windows') {
        Write-Host "✅ BUG 3 FIXED: DotnetTargetVersions correct" -ForegroundColor Green
    } else {
        Write-Host "❌ BUG 3 NOT FIXED" -ForegroundColor Red
    }
}
finally {
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
}
