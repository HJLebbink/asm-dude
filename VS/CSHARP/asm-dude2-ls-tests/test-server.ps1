# Test script to verify LSP server responds to stdin/stdout
param([int]$Timeout = 10, [string]$Config = "Release")

$lspExeName = "AsmDude2.LSP.exe"
$serverPath = "C:\Source\Github\asm-dude\VS\CSHARP\asm-dude2-ls\bin\$Config\net10.0-windows\$lspExeName"

Write-Host "Starting server: $serverPath --stdio"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $serverPath
$psi.Arguments = "--stdio"
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi

$process.Start() | Out-Null

Write-Host "Server started, PID: $($process.Id)"

Start-Sleep -Seconds 1

# Prepare initialize request
$initRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":1234,"rootUri":"file:///test","capabilities":{}}}'
$header = "Content-Length: $($initRequest.Length)`r`n`r`n"
$message = $header + $initRequest

Write-Host "Sending: $message"

# Send the request
$process.StandardInput.Write($message)
$process.StandardInput.Flush()

Write-Host "Message sent, waiting for responses..."

# Read stderr async
$stderrTask = $process.StandardError.ReadToEndAsync()

# Read multiple messages with longer waits
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$messageCount = 0
$noDataCount = 0

while ($sw.Elapsed.TotalSeconds -lt $Timeout -and $messageCount -lt 5 -and $noDataCount -lt 20) {
    $response = ""
    $contentLength = -1
    $hasData = $false

    # Read headers - wait more patiently
    while ($true) {
        if ($process.StandardOutput.Peek() -lt 0) {
            Start-Sleep -Milliseconds 200  # Increased wait
            if ($process.StandardOutput.Peek() -lt 0) {
                break
            }
        }
        $hasData = $true

        $line = $process.StandardOutput.ReadLine()
        if ([string]::IsNullOrEmpty($line)) {
            break
        }

        if ($line.StartsWith("Content-Length:")) {
            $contentLength = [int]$line.Substring(15).Trim()
        }
    }

    if ($contentLength -le 0) {
        if (-not $hasData) {
            $noDataCount++
            Write-Host "." -NoNewline
        }
        Start-Sleep -Milliseconds 200  # Increased wait
        continue
    }
    $noDataCount = 0

    # Read content
    $buffer = New-Object char[] $contentLength
    $read = $process.StandardOutput.Read($buffer, 0, $contentLength)
    $response = [string]::new($buffer, 0, $read)

    $messageCount++
    Write-Host "`n=== Message $messageCount (ContentLength: $contentLength) ==="
    Write-Host $response
}

Write-Host "`n=== Stderr ==="
if ($stderrTask.Wait(1000)) {
    Write-Host $stderrTask.Result
}

# Cleanup
$process.Kill()
Write-Host "`nServer killed. Received $messageCount messages."
