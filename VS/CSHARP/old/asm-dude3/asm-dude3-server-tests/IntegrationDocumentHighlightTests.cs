// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Integration tests for Document Highlights through the LSP server
/// </summary>
[Trait("Category", "Integration")]
[Trait("LongRunning", "true")]
public class IntegrationDocumentHighlightTests
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

    private async Task<JObject?> GetDocumentHighlights(string documentText, int line, int character, string uri = "file:///test.asm")
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
            // 1. Initialize
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

            var initResponse = await ReadMessage(reader);
            Assert.NotNull(initResponse);

            // 2. Initialized
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "initialized"
            });

            // 3. Open document
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri = uri,
                        languageId = "asm",
                        version = 1,
                        text = documentText
                    }
                }
            });

            await Task.Delay(100);

            // 4. Request document highlights
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/documentHighlight",
                @params = new
                {
                    textDocument = new { uri = uri },
                    position = new { line = line, character = character }
                }
            });

            var highlightResponse = await ReadMessage(reader);
            Assert.NotNull(highlightResponse);
            Assert.Equal(2, highlightResponse["id"]?.Value<int>());

            return highlightResponse;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
            catch { }
        }
    }

    [Fact]
    public async Task DocumentHighlight_SimpleWord_ShouldHighlightAllOccurrences()
    {
        // Arrange
        var content = @"mov myvar, 10
add myvar, 5
mov eax, myvar";

        // Act
        var response = await GetDocumentHighlights(content, line: 0, character: 4);

        // Assert
        Assert.NotNull(response);

        // Debug output
        Console.WriteLine($"Full response: {response.ToString()}");

        var result = response["result"];
        if (result == null)
        {
            var error = response["error"];
            if (error != null)
            {
                Console.WriteLine($"ERROR: {error.ToString()}");
            }
        }

        Assert.NotNull(result);

        var highlights = result.ToObject<JArray>();
        Assert.NotNull(highlights);
        Assert.Equal(3, highlights.Count); // myvar appears 3 times
    }

    [Fact]
    public async Task DocumentHighlight_Register_ShouldFindRelatedRegisters()
    {
        // Arrange
        var content = @"mov rax, 10
mov eax, 5
mov ax, 3";

        // Act
        var response = await GetDocumentHighlights(content, line: 0, character: 4);

        // Assert
        Assert.NotNull(response);
        var result = response["result"];
        Assert.NotNull(result);

        var highlights = result.ToObject<JArray>();
        Assert.NotNull(highlights);
        Assert.True(highlights.Count >= 3, $"Expected at least 3 highlights (rax, eax, ax), got {highlights.Count}");
    }

    [Fact]
    public async Task DocumentHighlight_CaseInsensitive_ShouldFindAllVariants()
    {
        // Arrange
        var content = @"MOV MyVar, 10
add myvar, 5
MOV eax, MYVAR";

        // Act
        var response = await GetDocumentHighlights(content, line: 0, character: 4);

        // Assert
        Assert.NotNull(response);
        var result = response["result"];
        Assert.NotNull(result);

        var highlights = result.ToObject<JArray>();
        Assert.NotNull(highlights);
        Assert.Equal(3, highlights.Count); // should find all case variants
    }
}
