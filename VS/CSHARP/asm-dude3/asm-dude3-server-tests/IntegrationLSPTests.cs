using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsmDude3.Server.Tests;

[Trait("Category", "Integration")]
public class IntegrationLSPTests
{
    private static string GetServerPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        return Path.Combine(currentDir, "..", "..", "..", "..", "asm-dude3-server", "bin", "Debug", "net10.0-windows", "asm-dude3-server.exe");
    }

    private static void SendMessage(StreamWriter writer, object message)
    {
        var json = JsonConvert.SerializeObject(message);
        var header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";
        writer.Write(header);
        writer.Write(json);
        writer.Flush();
    }

    private static async Task<JObject?> ReadMessage(StreamReader reader)
    {
        // Read header
        string? headerLine;
        int contentLength = 0;
        while ((headerLine = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(headerLine))
                break;

            if (headerLine.StartsWith("Content-Length:"))
            {
                contentLength = int.Parse(headerLine.Substring("Content-Length:".Length).Trim());
            }
        }

        if (contentLength == 0)
            return null;

        // Read content
        var buffer = new char[contentLength];
        int totalRead = 0;
        while (totalRead < contentLength)
        {
            int read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
            if (read == 0)
                break;
            totalRead += read;
        }

        var json = new string(buffer, 0, totalRead);
        return JObject.Parse(json);
    }

    [Fact]
    public async Task TestFoldingRange_WithRegionTags()
    {
        var serverPath = GetServerPath();
        Assert.True(File.Exists(serverPath), $"Server not found at: {serverPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = serverPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
        var reader = new StreamReader(process.StandardOutput.BaseStream, Encoding.UTF8);

        try
        {
            // 1. Send initialize
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    processId = (int?)null,
                    rootUri = (string?)null,
                    capabilities = new { }
                }
            });

            // Read initialize response
            var initResponse = await ReadMessage(reader);
            Assert.NotNull(initResponse);
            Assert.Equal(1, initResponse["id"]?.Value<int>());
            Console.WriteLine($"Initialize response: {initResponse}");

            // 2. Send initialized notification
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "initialized"
            });

            // 3. Send didOpen with #region tags
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri = "file:///test.asm",
                        languageId = "asm",
                        version = 1,
                        text = "; Test file\n#region My Region\nmov eax, ebx\nadd ecx, edx\n#endregion\n"
                    }
                }
            });

            // Wait a bit for processing
            await Task.Delay(100);

            // 4. Request folding ranges
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/foldingRange",
                @params = new
                {
                    textDocument = new
                    {
                        uri = "file:///test.asm"
                    }
                }
            });

            // Read folding range response
            var foldingResponse = await ReadMessage(reader);
            Assert.NotNull(foldingResponse);
            Assert.Equal(2, foldingResponse["id"]?.Value<int>());

            var result = foldingResponse["result"] as JArray;
            Assert.NotNull(result);
            Assert.True(result.Count > 0);

            var firstRange = result[0];
            Assert.Equal(1, firstRange["startLine"]?.Value<int>());
            Assert.Equal(4, firstRange["endLine"]?.Value<int>());
            Assert.Equal("region", firstRange["kind"]?.Value<string>());

            Console.WriteLine($"Folding ranges: {result}");

            // 5. Send exit
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "exit"
            });

            await Task.Delay(100);
            process.Kill();
        }
        finally
        {
            writer.Close();
            reader.Close();

            if (!process.HasExited)
            {
                process.Kill();
            }
        }
    }
}
