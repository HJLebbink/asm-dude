# Build, verify, and deploy AsmDude2 extension
# Single script for development iteration

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========== AsmDude2 Build and Verification ==========" -ForegroundColor Cyan
Write-Host ""

# 1. BUILD
Write-Host "[1/4] Building VSIX extension..." -ForegroundColor White
try {
    $buildOutput = dotnet build "VS\CSHARP\asm-dude2-vsix\asm-dude2-vsix.csproj" -c Debug 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[X] Build failed" -ForegroundColor Red
        $buildOutput | Select-String "error" | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "[OK] Build succeeded" -ForegroundColor Green
} catch {
    Write-Host "[X] Build error: $_" -ForegroundColor Red
    exit 1
}

# 2. VERIFY VSIX
Write-Host "[2/4] Verifying VSIX package..." -ForegroundColor White
$vsixPath = "VS\CSHARP\asm-dude2-vsix\bin\Debug\net10.0-windows\AsmDude2.vsix"
if (-not (Test-Path $vsixPath)) {
    Write-Host "[X] VSIX not found: $vsixPath" -ForegroundColor Red
    exit 1
}
$vsixSize = [math]::Round((Get-Item $vsixPath).Length / 1MB, 2)
Write-Host "[OK] VSIX exists ($vsixSize MB)" -ForegroundColor Green

# 3. VERIFY CONTENTS
Write-Host "[3/4] Checking VSIX contents..." -ForegroundColor White
$tempExt = "$env:TEMP\test-vsix"
if (Test-Path $tempExt) { Remove-Item $tempExt -Recurse -Force }

$zipPath = "$env:TEMP\test.zip"
Copy-Item $vsixPath $zipPath -Force
Expand-Archive -Path $zipPath -DestinationPath $tempExt -Force
Remove-Item $zipPath

$required = @("AsmDude2.dll", ".vsextension\extension.json", "Server\AsmDude2.LSP.exe")
foreach ($file in $required) {
    if (Test-Path (Join-Path $tempExt $file)) {
        Write-Host "[OK] $file" -ForegroundColor Green
    } else {
        Write-Host "[X] Missing: $file" -ForegroundColor Red
        exit 1
    }
}

# 4. DEPLOY (optional - only if -Deploy flag)
if ($args -contains "-Deploy") {
    Write-Host "[4/4] Deploying to experimental instance..." -ForegroundColor White

    Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $experimentalRoot = "$env:APPDATA\Microsoft\VisualStudio\18.0_690edbf8Exp"
    if (Test-Path $experimentalRoot) {
        Remove-Item $experimentalRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    $extPath = "$experimentalRoot\Extensions\HenkJanLebbink.AsmDude2\1.0.0"
    mkdir $extPath -Force | Out-Null
    Copy-Item "$tempExt\*" -Destination $extPath -Recurse -Force

    Write-Host "[OK] Deployed to: $extPath" -ForegroundColor Green

    # Launch VS
    Write-Host ""
    Write-Host "[*] Launching VS with test file..." -ForegroundColor White
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

    $devenvPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"
    $process = Start-Process -FilePath $devenvPath `
        -ArgumentList "`"$testFile`" /rootSuffix Exp" `
        -PassThru `
        -NoNewWindow

    Write-Host "[OK] VS launched (PID: $($process.Id))" -ForegroundColor Green
    Write-Host "[*] Check VS window for syntax highlighting and features" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========== Success ==========" -ForegroundColor Green
Write-Host ""
Write-Host "To deploy and test with VS:" -ForegroundColor Cyan
Write-Host "  powershell -File test-build.ps1 -Deploy" -ForegroundColor White
Write-Host ""
