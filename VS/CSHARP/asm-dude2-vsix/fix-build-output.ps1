# Fix extension.json and extension.vsixmanifest in the build output directory
# The SDK generates wrong fullClassName and unnecessary ExtensionCommandSet

param(
    [string]$OutputPath
)

if (-not (Test-Path $OutputPath)) {
    Write-Error "Output path not found: $OutputPath"
    exit 1
}

# Fix extension.json
$jsonPath = "$OutputPath\.vsextension\extension.json"
if (Test-Path $jsonPath) {
    $json = Get-Content $jsonPath -Raw | ConvertFrom-Json

    $changed = $false

    # Fix AsmLanguageServerProvider service to point to correct class
    $lspService = $json.services | Where-Object { $_.name -eq 'AsmDude2.AsmLanguageServerProvider' }
    if ($lspService -and $lspService.entryPoint.fullClassName -ne 'AsmDude2.AsmLanguageServerProvider') {
        Write-Host "  Fixing fullClassName: $($lspService.entryPoint.fullClassName) -> AsmDude2.AsmLanguageServerProvider"
        $lspService.entryPoint.fullClassName = 'AsmDude2.AsmLanguageServerProvider'
        $changed = $true
    }

    # Remove ExtensionCommandSet service
    $initialCount = $json.services.Count
    $json.services = @($json.services | Where-Object { $_.name -ne 'AsmDude2.ExtensionCommandSet' })
    if ($json.services.Count -lt $initialCount) {
        Write-Host "  Removed ExtensionCommandSet service"
        $changed = $true
    }

    # Remove commandSets entirely
    if ($json.commandSets) {
        Write-Host "  Removing commandSets section"
        $json.PSObject.Properties.Remove('commandSets')
        $changed = $true
    }

    if ($changed) {
        $json | ConvertTo-Json -Depth 10 | Set-Content $jsonPath
        Write-Host "  Build output extension.json fixed"
    } else {
        Write-Host "  Build output extension.json already correct"
    }
} else {
    Write-Host "  WARNING: extension.json not found at $jsonPath"
}

# Fix extension.vsixmanifest
$manifestPath = "$OutputPath\extension.vsixmanifest"
if (Test-Path $manifestPath) {
    [xml]$manifest = Get-Content $manifestPath
    $ns = @{ x = 'http://schemas.microsoft.com/developer/vsx-schema/2011' }

    $changed = $false

    # Fix DotnetTargetVersions
    $versionNode = $manifest | Select-Xml -XPath "//x:DotnetTargetVersions" -Namespace $ns | Select-Object -First 1
    if ($versionNode -and $versionNode.Node.InnerText -ne 'net10.0') {
        Write-Host "  Fixing DotnetTargetVersions: $($versionNode.Node.InnerText) -> net10.0"
        $versionNode.Node.InnerText = 'net10.0'
        $changed = $true
    }

    # Fix InstallationTarget version
    $installTargets = $manifest | Select-Xml -XPath "//x:InstallationTarget" -Namespace $ns
    foreach ($target in $installTargets) {
        $versionAttr = $target.Node.Attributes['Version']
        if ($versionAttr -and $versionAttr.Value -match '17\.14') {
            Write-Host "  Fixing InstallationTarget version: $($versionAttr.Value) -> [17.0,)"
            $versionAttr.Value = '[17.0,)'
            $changed = $true
        }
    }

    if ($changed) {
        $manifest.Save($manifestPath)
        Write-Host "  Build output extension.vsixmanifest fixed"
    } else {
        Write-Host "  Build output extension.vsixmanifest already correct"
    }
} else {
    Write-Host "  WARNING: extension.vsixmanifest not found at $manifestPath"
}
