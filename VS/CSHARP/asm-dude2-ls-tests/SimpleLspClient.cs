namespace AsmDude2LS.Tests;

internal class SimpleLspClient
{
    // Manual debug client (not an entry point — this is a test assembly; xUnit owns Main).
    static async Task RunAsync()
    {
        Console.WriteLine("=== AsmDude LSP Simple Client ===\n");

        // Create a pipe for communication
        var serverStdIn = new FileStream("nul", FileMode.Append); // placeholder
        var serverStdOut = Console.OpenStandardOutput();

        // Start LSP server in a separate process
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "\"C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-ls/bin/Debug/net10.0-windows/AsmDude2.LSP.dll\" --stdio",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            Console.WriteLine("Failed to start LSP server");
            return;
        }

        Console.WriteLine("LSP server started (PID: " + process.Id + ")");
        Console.WriteLine("To test, send LSP JSON-RPC messages to stdin");
        Console.WriteLine("Example: textDocument/didOpen for an .asm file\n");

        Console.WriteLine("Press Ctrl+C to exit\n");

        // Read server output and display
        var reader = process.StandardOutput;
        var line = await reader.ReadLineAsync();
        while (line != null)
        {
            Console.WriteLine("[LSP Server] " + line);
            line = await reader.ReadLineAsync();
        }
    }
}
