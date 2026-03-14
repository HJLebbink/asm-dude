# Test extension by launching VS with experimental instance

# Create test assembly file
Write-Host "Creating test assembly file..." -ForegroundColor Cyan
$testFile = "$env:TEMP\test-extension.asm"
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

Write-Host "Test file: $testFile"
Write-Host ""

# Launch VS with experimental instance
Write-Host "Launching VS experimental instance..." -ForegroundColor Cyan
$devenvPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"
$sln = "C:\Source\Github\asm-dude\VS\AsmDude.sln"

$process = Start-Process -FilePath $devenvPath `
    -ArgumentList "`"$sln`" /rootSuffix Exp" `
    -PassThru `
    -NoNewWindow

Write-Host "VS launched (PID: $($process.Id))"
Write-Host "Waiting 10 seconds for startup..."
Start-Sleep -Seconds 10

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Checking Extension Startup Logs" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# Check startup log
$startupLog = "$env:TEMP\AsmDude2_Startup.log"
if (Test-Path $startupLog) {
    Write-Host "✓ Found: AsmDude2_Startup.log" -ForegroundColor Green
    Write-Host ""
    Get-Content $startupLog
} else {
    Write-Host "✗ AsmDude2_Startup.log NOT FOUND - Extension did not instantiate" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Checking LSP Error Log" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

# Check LSP error log
$lspLog = "$env:TEMP\AsmDude2_LSP_Error.log"
if (Test-Path $lspLog) {
    Write-Host "✓ Found: AsmDude2_LSP_Error.log" -ForegroundColor Green
    Write-Host ""
    Get-Content $lspLog | Select-Object -First 50
} else {
    Write-Host "(No LSP error log - either not needed or not yet created)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "VS Process Status" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

$vsProc = Get-Process devenv -ErrorAction SilentlyContinue
if ($vsProc) {
    Write-Host "VS is still running (PID: $($vsProc.Id))" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. In the running VS instance, open the test file: $testFile"
    Write-Host "2. Check if syntax highlighting appears"
    Write-Host "3. Hover over 'mov' and other mnemonics"
    Write-Host "4. Check Output pane (View → Output) for LSP messages"
    Write-Host ""
    Write-Host "Close VS when done testing."
} else {
    Write-Host "VS process ended" -ForegroundColor Yellow
}
