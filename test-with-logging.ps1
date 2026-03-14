# Test extension with VS logging enabled

$expRoot = "$env:APPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp"

# Remove entire experimental instance
if (Test-Path $expRoot) {
    Write-Host "Removing experimental instance..."
    Remove-Item $expRoot -Recurse -Force
    Start-Sleep -Milliseconds 500
}

# Re-deploy extension
Write-Host "Re-deploying extension..."
$vsixPath = "VS\CSHARP\asm-dude2-vsix\bin\Debug\net10.0-windows\AsmDude2.vsix"
$tempExt = "$env:TEMP\redeploy-vsix"
if (Test-Path $tempExt) { Remove-Item $tempExt -Recurse -Force }

# Extract
$zipPath = "$env:TEMP\redeploy.zip"
Copy-Item $vsixPath $zipPath -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempExt)

# Copy to experimental instance
$extPath = "$expRoot\Extensions\HenkJanLebbink.AsmDude2\1.0.0"
mkdir $extPath -Force | Out-Null
Copy-Item "$tempExt\*" -Destination $extPath -Recurse -Force

Write-Host "Deployed to: $extPath"

# Create test file
$testFile = "$env:TEMP\test-asmdude.asm"
@"
mov eax, 0x10
add eax, ebx
xor ecx, ecx
cmp eax, 0x20
je done
mov edx, [rax]
loop_start:
    inc eax
    jne loop_start
done:
    ret
"@ | Set-Content $testFile

# Launch VS with logging
$devenvPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"
Write-Host "Launching VS with logging..."
$logDir = "$env:TEMP\vs-logs"
if (Test-Path $logDir) { Remove-Item $logDir -Recurse -Force }
mkdir $logDir | Out-Null

$process = Start-Process -FilePath $devenvPath `
    -ArgumentList "`"$testFile`" /rootSuffix Exp /log `"$logDir\VSActivityLog.xml`"" `
    -PassThru `
    -NoNewWindow

Write-Host "VS launched (PID: $($process.Id))"
Write-Host "Wait for VS to load, then check: $logDir\VSActivityLog.xml"
Write-Host ""
Write-Host "Checking startup logs..."
Start-Sleep -Seconds 3

if (Test-Path "$env:TEMP\AsmDude2_Startup.log") {
    Write-Host "SUCCESS! Extension loaded!"
    Get-Content "$env:TEMP\AsmDude2_Startup.log"
} else {
    Write-Host "Extension still not loading. Checking VS log..."
    if (Test-Path "$logDir\VSActivityLog.xml") {
        Get-Content "$logDir\VSActivityLog.xml" | Select-String -Pattern "AsmDude|error|failed" -Context 2 | head -30
    }
}
