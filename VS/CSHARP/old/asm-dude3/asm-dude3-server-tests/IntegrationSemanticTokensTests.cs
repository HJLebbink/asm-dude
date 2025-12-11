using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Integration tests for LSP Semantic Tokens functionality (syntax highlighting)
/// Tests the actual LSP server process via stdio communication
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationSemanticTokensTests
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

    private async Task<JArray?> GetSemanticTokens(string documentText, string uri = "file:///test.asm")
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

            // 4. Request semantic tokens
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/semanticTokens/full",
                @params = new
                {
                    textDocument = new { uri = uri }
                }
            });

            var tokensResponse = await ReadMessage(reader);
            Assert.NotNull(tokensResponse);
            Assert.Equal(2, tokensResponse["id"]?.Value<int>());

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

            var result = tokensResponse["result"];
            return result?["data"] as JArray;
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

    [Fact]
    public async Task Test01_SimpleInstruction_ReturnsTokens()
    {
        var text = "mov rax, rbx";
        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
        // Each token is 5 integers: deltaLine, deltaStartChar, length, tokenType, tokenModifiers
        Assert.Equal(0, tokens.Count % 5);
    }

    [Fact]
    public async Task Test02_MultipleInstructions_ReturnsMultipleTokens()
    {
        var text = @"mov rax, rbx
add rax, rcx
sub rax, rdx";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
        Assert.True(tokens.Count >= 15); // At least 3 tokens (3 * 5)
    }

    [Fact]
    public async Task Test03_WithComments_TokenizesCorrectly()
    {
        var text = @"; This is a comment
mov rax, rbx  ; inline comment
add rax, rcx";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test04_WithLabels_TokenizesCorrectly()
    {
        var text = @"my_function:
    mov rax, rdi
    ret";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test05_ComplexProgram_FullTokenization()
    {
        var text = @"; Calculate factorial
factorial:
    push rbp
    mov rbp, rsp
    mov rax, 1
.loop:
    cmp rdi, 1
    jle .done
    imul rax, rdi
    dec rdi
    jmp .loop
.done:
    pop rbp
    ret";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
        Assert.True(tokens.Count >= 50); // Many tokens expected
    }

    [Fact]
    public async Task Test06_AVXInstructions_AreTokenized()
    {
        var text = @"vaddpd ymm0, ymm1, ymm2
vsubpd ymm3, ymm4, ymm5
vmulpd ymm6, ymm7, ymm8";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test07_SSEInstructions_AreTokenized()
    {
        var text = @"movaps xmm0, xmm1
addps xmm2, xmm3
mulps xmm4, xmm5";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test08_MemoryOperands_AreTokenized()
    {
        var text = @"mov rax, [rbx]
mov rcx, [rdx + 8]
mov rsi, [rdi + rax * 4 + 0x10]";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test09_NumericLiterals_AreTokenized()
    {
        var text = @"mov rax, 42
mov rbx, 0x1234
mov rcx, 0b1010
mov rdx, 777o";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test10_EmptyFile_ReturnsEmptyTokens()
    {
        var text = "";
        var tokens = await GetSemanticTokens(text);

        // Empty file should return empty or null tokens
        Assert.True(tokens == null || tokens.Count == 0);
    }

    [Fact]
    public async Task Test11_OnlyComments_ReturnsCommentTokens()
    {
        var text = @"; Comment line 1
; Comment line 2
; Comment line 3";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test12_OnlyWhitespace_ReturnsNoTokens()
    {
        var text = @"


        ";

        var tokens = await GetSemanticTokens(text);

        Assert.True(tokens == null || tokens.Count == 0);
    }

    [Fact]
    public async Task Test13_MixedCaseInstructions_AreTokenized()
    {
        var text = @"MOV RAX, RBX
Add RaX, RcX
SuB rAx, rDx";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test14_AllRegisterTypes_AreTokenized()
    {
        var text = @"mov al, bl
mov ax, bx
mov eax, ebx
mov rax, rbx";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test15_SpecialRegisters_AreTokenized()
    {
        var text = @"mov rsp, rbp
mov rip, [rax]
push rsi
pop rdi";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test16_ConditionalJumps_AreTokenized()
    {
        var text = @"je label1
jne label2
jg label3
jl label4
jge label5
jle label6";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test17_StackOperations_AreTokenized()
    {
        var text = @"push rax
pop rbx
pushf
popf";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test18_StringOperations_AreTokenized()
    {
        var text = @"movsb
movsw
movsd
movsq
rep movsb";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test19_BitwiseOperations_AreTokenized()
    {
        var text = @"and rax, rbx
or rcx, rdx
xor rsi, rdi
not r8
shl r9, 2
shr r10, 3";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test20_MultiplicationDivision_AreTokenized()
    {
        var text = @"imul rax, rbx
mul rcx
idiv rdx
div rsi";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test21_CompareAndTest_AreTokenized()
    {
        var text = @"cmp rax, rbx
test rcx, rdx
cmovz rsi, rdi";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public async Task Test22_TokenDeltaEncoding_IsValid()
    {
        var text = @"mov rax, rbx
add rcx, rdx";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);

        // Verify tokens are in groups of 5
        Assert.Equal(0, tokens.Count % 5);

        // Verify all values are non-negative
        for (int i = 0; i < tokens.Count; i++)
        {
            var value = tokens[i].Value<int>();
            Assert.True(value >= 0, $"Token value at index {i} should be non-negative");
        }
    }

    [Fact]
    public async Task Test23_LargeFile_IsTokenizedEfficiently()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"mov rax, {i}");
            sb.AppendLine($"add rbx, {i}");
            sb.AppendLine($"sub rcx, {i}");
        }

        var text = sb.ToString();
        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
        Assert.True(tokens.Count >= 300 * 5); // At least 300 instructions * 5 values per token
    }

    [Fact]
    public async Task Test24_DifferentDocuments_IndependentTokenization()
    {
        var text1 = "mov rax, rbx";
        var text2 = "add rcx, rdx";

        var tokens1 = await GetSemanticTokens(text1, "file:///doc1.asm");
        var tokens2 = await GetSemanticTokens(text2, "file:///doc2.asm");

        Assert.NotNull(tokens1);
        Assert.NotNull(tokens2);
        Assert.NotEmpty(tokens1);
        Assert.NotEmpty(tokens2);
    }

    [Fact]
    public async Task Test25_MacroLikeStructures_AreTokenized()
    {
        var text = @"%define MY_CONST 42
mov rax, MY_CONST
%include 'file.asm'";

        var tokens = await GetSemanticTokens(text);

        // Should tokenize what it can
        Assert.NotNull(tokens);
    }

    [Fact]
    public async Task Test26_DataDirectives_AreTokenized()
    {
        var text = @"section .data
    msg db 'Hello', 0
    num dw 1234
    val dd 0x12345678";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
    }

    [Fact]
    public async Task Test27_SectionDirectives_AreTokenized()
    {
        var text = @"section .text
    mov rax, rbx
section .data
    db 0";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
    }

    [Fact]
    public async Task Test28_GlobalAndExtern_AreTokenized()
    {
        var text = @"global main
extern printf
main:
    mov rax, 0
    ret";

        var tokens = await GetSemanticTokens(text);

        Assert.NotNull(tokens);
        Assert.NotEmpty(tokens);
    }
}
