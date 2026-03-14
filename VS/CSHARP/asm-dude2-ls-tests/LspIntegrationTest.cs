using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AsmDude2.Tests;

/// <summary>
/// Integration test that starts the LSP server directly and tests LSP protocol communication.
/// This tests the core LSP functionality without needing VS.
/// </summary>
public class LspIntegrationTest
{
    [Fact(Skip = "Manual test - starts LSP server process")]
    public async Task CanStartLspServerAndHandleInitialize()
    {
        var lspExePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "asm-dude2-ls", "bin", "Debug", "net10.0-windows",
            "AsmDude2.LSP.exe"
        );

        Assert.True(File.Exists(lspExePath), $"LSP server not found at {lspExePath}");

        // Start LSP server with --stdio flag
        var processInfo = new ProcessStartInfo
        {
            FileName = lspExePath,
            Arguments = "--stdio",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        var process = Process.Start(processInfo);
        Assert.NotNull(process);

        try
        {
            // Send Initialize request
            var initRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    processId = Environment.ProcessId,
                    rootPath = "/tmp",
                    capabilities = new { }
                }
            };

            var json = JsonSerializer.Serialize(initRequest);
            var message = $"Content-Length: {json.Length}\r\n\r\n{json}";

            await process.StandardInput.WriteAsync(message);
            await process.StandardInput.FlushAsync();

            // Read response
            var response = await process.StandardOutput.ReadLineAsync();
            Assert.NotNull(response);
            Assert.Contains("initialize", response);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill();
        }
    }
}
