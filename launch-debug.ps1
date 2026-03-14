# Proper way to test: Open solution in VS and press F5
# This opens the solution and triggers proper extension debugging

$slnPath = "C:\Source\Github\asm-dude\VS\AsmDude.sln"
$vsPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"

Write-Host "Opening solution in Visual Studio..." -ForegroundColor Cyan
Write-Host "Once VS opens:"
Write-Host "  1. Right-click asm-dude2-vsix → Set as Startup Project"
Write-Host "  2. Press F5 to launch experimental instance with debugging"
Write-Host "  3. It will open with the test assembly file"
Write-Host "  4. Check if syntax highlighting works"
Write-Host ""

# Open solution
Start-Process -FilePath $vsPath -ArgumentList "`"$slnPath`""

Write-Host "Solution opened in Visual Studio..."
