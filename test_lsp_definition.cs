using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class TestLspDefinition
{
    static async Task Main()
    {
        var dllPath = @"C:\Source\Github\asm-dude\VS\CSHARP\asm-dude2-ls\bin\Debug\net10.0-windows\AsmDude2.LSP.dll";
        var asmFile = @"C:\Source\Github\asm-dude\VS\CSHARP\asm-dude2-vsix\Resources\examples\example_semantic_analysis.asm";
        
        Console.WriteLine("[TEST] Starting LSP server in stdio mode...");
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\" --stdio",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        
        var process = Process.Start(processInfo);
        if (process == null)
        {
            Console.WriteLine("[ERROR] Failed to start process");
            return;
        }
        
        var output = new StringBuilder();
        var error = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => {
            if (e.Data != null) error.AppendLine(e.Data);
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await Task.Delay(2000);
        
        Console.WriteLine("[TEST] Server started, sending initialize...");
        
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            parameters = new
            {
                processId = Environment.ProcessId,
                rootPath = "/tmp",
                capabilities = new { }
            }
        };
        
        await SendRequest(process.StandardInput, initRequest);
        await Task.Delay(500);
        
        Console.WriteLine("[TEST] Sending didOpen...");
        
        var documentUri = $"file:///{asmFile.Replace("\\", "/")}";
        var docContent = await File.ReadAllTextAsync(asmFile);
        
        var didOpen = new
        {
            jsonrpc = "2.0",
            method = "textDocument/didOpen",
            parameters = new
            {
                textDocument = new
                {
                    uri = documentUri,
                    languageId = "assembly",
                    version = 1,
                    text = docContent
                }
            }
        };
        
        await SendRequest(process.StandardInput, didOpen);
        await Task.Delay(1000);
        
        Console.WriteLine("[TEST] Sending definition request for line 10, character 1...");
        
        var definitionRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "textDocument/definition",
            parameters = new
            {
                textDocument = new { uri = documentUri },
                position = new { line = 10, character = 1 }
            }
        };
        
        await SendRequest(process.StandardInput, definitionRequest);
        await Task.Delay(1000);
        
        Console.WriteLine("\n=== SERVER OUTPUT ===");
        Console.WriteLine(output.ToString());
        
        Console.WriteLine("\n=== SERVER ERROR ===");
        Console.WriteLine(error.ToString());
        
        process.Kill();
        process.WaitForExit();
    }
    
    static async Task SendRequest(StreamWriter writer, object request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var message = $"Content-Length: {json.Length}\r\n\r\n{json}";
        await writer.WriteAsync(message);
        await writer.FlushAsync();
    }
}
