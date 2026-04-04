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
        
        var outputTask = new StringBuilder();
        var errorTask = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => {
            if (e.Data != null) {
                outputTask.AppendLine(e.Data);
                Console.WriteLine($"[OUT] {e.Data}");
            }
        };
        process.ErrorDataReceived += (sender, e) => {
            if (e.Data != null) {
                errorTask.AppendLine(e.Data);
                Console.WriteLine($"[ERR] {e.Data}");
            }
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await Task.Delay(2000);
        
        Console.WriteLine("\n[TEST] Server started, sending messages...");
        
        try
        {
            // Initialize
            Console.WriteLine("[TEST] Sending initialize...");
            var initJson = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{""processId"":" + Environment.ProcessId + @",""rootPath"":""/tmp"",""capabilities"":{}}}";
            await WriteMessage(process.StandardInput, initJson);
            await Task.Delay(500);
            
            // didOpen
            Console.WriteLine("[TEST] Sending didOpen...");
            var docContent = await File.ReadAllTextAsync(asmFile);
            var docUri = $"file:///{asmFile.Replace("\\", "/")}";
            var didOpenJson = $"{{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didOpen\",\"params\":{{\"textDocument\":{{\"uri\":\"{docUri}\",\"languageId\":\"assembly\",\"version\":1,\"text\":\"{EscapeJson(docContent)}\"}}}}}}";
            await WriteMessage(process.StandardInput, didOpenJson);
            await Task.Delay(1000);
            
            // definition request for line 10, char 1
            Console.WriteLine("[TEST] Sending definition request for line 10, char 1...");
            var defJson = @"{""jsonrpc"":""2.0"",""id"":3,""method"":""textDocument/definition"",""params"":{""textDocument"":{""uri"":""" + docUri + @"""},""position"":{""line"":10,""character"":1}}}";
            await WriteMessage(process.StandardInput, defJson);
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        
        await Task.Delay(1000);
        
        Console.WriteLine("\n=== SERVER OUTPUT ===");
        Console.WriteLine(outputTask.ToString());
        
        Console.WriteLine("\n=== SERVER ERROR ===");
        Console.WriteLine(errorTask.ToString());
        
        process.Kill();
        process.WaitForExit();
    }
    
    static async Task WriteMessage(StreamWriter writer, string json)
    {
        var message = $"Content-Length: {json.Length}\r\n\r\n{json}";
        await writer.WriteAsync(message);
        await writer.FlushAsync();
        Console.WriteLine($"[SEND] Content-Length: {json.Length}");
    }
    
    static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
