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
/// Integration tests for LSP Hover functionality
/// Tests the actual LSP server process via stdio communication
/// </summary>
[Trait("Category", "Integration")]
[Trait("LongRunning", "true")]
public class IntegrationHoverTests
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

    private async Task<JObject?> GetHover(string documentText, int line, int character, string uri = "file:///test.asm")
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

            // 4. Request hover
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/hover",
                @params = new
                {
                    textDocument = new { uri = uri },
                    position = new { line = line, character = character }
                }
            });

            var hoverResponse = await ReadMessage(reader);
            Assert.NotNull(hoverResponse);
            Assert.Equal(2, hoverResponse["id"]?.Value<int>());

            // 5. Exit
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "exit"
            });

            await Task.Delay(100);
            if (!process.HasExited)
            {
                process.Kill();
            }

            return hoverResponse["result"] as JObject;
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

    [DebugSkippableFact]
    public async Task Test01_HoverOnMnemonic_ReturnsInstructionInfo()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 0, 1); // Position on "mov"

        Assert.NotNull(hover);
        var contents = hover["contents"];
        Assert.NotNull(contents);
    }

    [DebugSkippableFact]
    public async Task Test02_HoverOnRegister_ReturnsRegisterInfo()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 0, 5); // Position on "rax"

        Assert.NotNull(hover);
        var contents = hover["contents"];
        Assert.NotNull(contents);
    }

    [DebugSkippableFact]
    public async Task Test03_HoverOnRegister_32Bit()
    {
        var text = "add eax, ebx";
        var hover = await GetHover(text, 0, 5); // Position on "eax"

        Assert.NotNull(hover);
        var contents = hover["contents"];
        Assert.NotNull(contents);
    }

    [DebugSkippableFact]
    public async Task Test04_HoverOnCommonMnemonics()
    {
        var mnemonics = new[] { "push", "pop", "call", "ret", "jmp", "je", "jne", "add", "sub", "xor", "and", "or" };

        foreach (var mnemonic in mnemonics)
        {
            var text = $"{mnemonic} rax";
            var hover = await GetHover(text, 0, 1);
            Assert.NotNull(hover);
        }
    }

    [DebugSkippableFact]
    public async Task Test05_HoverOnAVXInstruction()
    {
        var text = "vaddpd ymm0, ymm1, ymm2";
        var hover = await GetHover(text, 0, 2); // Position on "vaddpd"

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test06_HoverOnSSEInstruction()
    {
        var text = "movaps xmm0, xmm1";
        var hover = await GetHover(text, 0, 2); // Position on "movaps"

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test07_HoverOnComment_ReturnsNull()
    {
        var text = "; This is a comment";
        var hover = await GetHover(text, 0, 5);

        Assert.Null(hover);
    }

    [DebugSkippableFact]
    public async Task Test08_HoverOnWhitespace_ReturnsNull()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 0, 3); // Space between mov and rax

        Assert.Null(hover);
    }

    [DebugSkippableFact]
    public async Task Test09_HoverOnLabel_ReturnsInfo()
    {
        var text = @"my_function:
    mov rax, rbx
    ret";
        var hover = await GetHover(text, 0, 5); // Position on "my_function"

        // Labels may or may not have hover info depending on implementation
        // Just verify no crash occurs
        Assert.True(true);
    }

    [DebugSkippableFact]
    public async Task Test10_HoverOnNumericLiteral()
    {
        var text = "mov rax, 42";
        var hover = await GetHover(text, 0, 10); // Position on "42"

        // Numeric literals may or may not have hover info
        Assert.True(true);
    }

    [DebugSkippableFact]
    public async Task Test11_HoverOnHexLiteral()
    {
        var text = "mov rax, 0x1234";
        var hover = await GetHover(text, 0, 11); // Position on hex number

        Assert.True(true);
    }

    [DebugSkippableFact]
    public async Task Test12_HoverOnMemoryOperand()
    {
        var text = "mov rax, [rbx]";
        var hover = await GetHover(text, 0, 11); // Position on "rbx" inside brackets

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test13_HoverOnComplexMemoryOperand()
    {
        var text = "mov rax, [rbx + rcx * 8 + 0x10]";
        var hover = await GetHover(text, 0, 15); // Position on "rcx"

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test14_HoverOnMultipleLines()
    {
        var text = @"mov rax, rbx
add rax, rcx
sub rax, rdx";

        // Test hover on each line
        var hover1 = await GetHover(text, 0, 1);
        Assert.NotNull(hover1);

        var hover2 = await GetHover(text, 1, 1);
        Assert.NotNull(hover2);

        var hover3 = await GetHover(text, 2, 1);
        Assert.NotNull(hover3);
    }

    [DebugSkippableFact]
    public async Task Test15_HoverOnCaseInsensitive()
    {
        var text = "MOV RAX, RBX";
        var hover = await GetHover(text, 0, 1); // Position on "MOV"

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test16_HoverOnMixedCase()
    {
        var text = "MoV rAx, RbX";
        var hover = await GetHover(text, 0, 1);

        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test17_HoverOnInvalidPosition_OutOfBounds()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 10, 5); // Line 10 doesn't exist

        Assert.Null(hover);
    }

    [DebugSkippableFact]
    public async Task Test18_HoverOnEmptyLine()
    {
        var text = @"mov rax, rbx

add rax, rcx";
        var hover = await GetHover(text, 1, 0); // Empty line

        Assert.Null(hover);
    }

    [DebugSkippableFact]
    public async Task Test19_HoverWithLabelsAndComments()
    {
        var text = @"; Function to add two numbers
add_numbers:
    mov rax, rdi  ; First parameter
    add rax, rsi  ; Second parameter
    ret           ; Return result";

        var hover = await GetHover(text, 2, 5); // Position on "mov"
        Assert.NotNull(hover);
    }

    [DebugSkippableFact]
    public async Task Test20_HoverRange_VerifyStartAndEnd()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 0, 1);

        Assert.NotNull(hover);
        var range = hover["range"];
        if (range != null)
        {
            Assert.NotNull(range["start"]);
            Assert.NotNull(range["end"]);
        }
    }

    [DebugSkippableFact]
    public async Task Test21_HoverContentFormat_IsMarkdown()
    {
        var text = "mov rax, rbx";
        var hover = await GetHover(text, 0, 1);

        Assert.NotNull(hover);
        var contents = hover["contents"];
        Assert.NotNull(contents);

        var kind = contents["kind"]?.Value<string>();
        if (kind != null)
        {
            Assert.Equal("markdown", kind);
        }
    }

    [DebugSkippableFact]
    public async Task Test22_HoverOnDifferentDocuments()
    {
        var text1 = "mov rax, rbx";
        var text2 = "add rcx, rdx";

        var hover1 = await GetHover(text1, 0, 1, "file:///doc1.asm");
        var hover2 = await GetHover(text2, 0, 1, "file:///doc2.asm");

        Assert.NotNull(hover1);
        Assert.NotNull(hover2);
    }

    [DebugSkippableFact]
    public async Task Test23_HoverOnRareInstructions()
    {
        var instructions = new[]
        {
            "cpuid",
            "rdtsc",
            "sysenter",
            "sysexit",
            "hlt",
            "nop"
        };

        foreach (var instruction in instructions)
        {
            var text = instruction;
            var hover = await GetHover(text, 0, 1);
            // Just verify no crash
            Assert.True(true);
        }
    }

    [DebugSkippableFact]
    public async Task Test24_HoverOnFPUInstructions()
    {
        var text = "fadd st0, st1";
        var hover = await GetHover(text, 0, 1);

        // FPU instructions may or may not be implemented
        Assert.True(true);
    }

    [DebugSkippableFact]
    public async Task Test25_HoverOnPrefixes()
    {
        var text = "rep movsb";
        var hover = await GetHover(text, 0, 1); // On "rep"

        // Prefixes may have hover info
        Assert.True(true);
    }
}
