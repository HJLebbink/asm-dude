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
/// Comprehensive integration tests for LSP folding range functionality
/// Tests region folding with various configurations and edge cases
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationFoldingTests
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

    private async Task<JArray?> GetFoldingRanges(string documentText, string uri = "file:///test.asm")
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
            // Initialize
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

            await ReadMessage(reader); // Read initialize response

            // Initialized notification
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "initialized"
            });

            // Open document
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

            await Task.Delay(50); // Allow processing

            // Request folding ranges
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "textDocument/foldingRange",
                @params = new
                {
                    textDocument = new { uri = uri }
                }
            });

            var foldingResponse = await ReadMessage(reader);
            Assert.NotNull(foldingResponse);

            // Exit
            SendMessage(writer, new
            {
                jsonrpc = "2.0",
                method = "exit"
            });

            return foldingResponse["result"] as JArray;
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
    public async Task Test01_BasicSingleRegion()
    {
        var text = @"; Test file
#region MyRegion
mov eax, ebx
add ecx, edx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var range = ranges[0];
        Assert.Equal(1, range["startLine"]?.Value<int>());
        Assert.Equal(4, range["endLine"]?.Value<int>());
        Assert.Equal("region", range["kind"]?.Value<string>());
        Assert.Equal("MyRegion", range["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test02_MultipleSequentialRegions()
    {
        var text = @"#region First
mov eax, 1
#endregion

#region Second
mov ebx, 2
#endregion

#region Third
mov ecx, 3
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Equal(3, ranges.Count);

        Assert.Equal(0, ranges[0]["startLine"]?.Value<int>());
        Assert.Equal(2, ranges[0]["endLine"]?.Value<int>());
        Assert.Equal("First", ranges[0]["collapsedText"]?.Value<string>());

        Assert.Equal(4, ranges[1]["startLine"]?.Value<int>());
        Assert.Equal(6, ranges[1]["endLine"]?.Value<int>());
        Assert.Equal("Second", ranges[1]["collapsedText"]?.Value<string>());

        Assert.Equal(8, ranges[2]["startLine"]?.Value<int>());
        Assert.Equal(10, ranges[2]["endLine"]?.Value<int>());
        Assert.Equal("Third", ranges[2]["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test03_NestedRegions()
    {
        var text = @"#region Outer
mov eax, 1
  #region Inner
  mov ebx, 2
  mov ecx, 3
  #endregion
mov edx, 4
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Equal(2, ranges.Count);

        // Outer region
        var outer = ranges.FirstOrDefault(r => r["collapsedText"]?.Value<string>() == "Outer");
        Assert.NotNull(outer);
        Assert.Equal(0, outer["startLine"]?.Value<int>());
        Assert.Equal(7, outer["endLine"]?.Value<int>());

        // Inner region
        var inner = ranges.FirstOrDefault(r => r["collapsedText"]?.Value<string>() == "Inner");
        Assert.NotNull(inner);
        Assert.Equal(2, inner["startLine"]?.Value<int>());
        Assert.Equal(5, inner["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test04_DeeplyNestedRegions()
    {
        var text = @"#region Level1
  #region Level2
    #region Level3
      mov eax, eax
    #endregion
  #endregion
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Equal(3, ranges.Count);

        var level1 = ranges.FirstOrDefault(r => r["collapsedText"]?.Value<string>() == "Level1");
        var level2 = ranges.FirstOrDefault(r => r["collapsedText"]?.Value<string>() == "Level2");
        var level3 = ranges.FirstOrDefault(r => r["collapsedText"]?.Value<string>() == "Level3");

        Assert.NotNull(level1);
        Assert.NotNull(level2);
        Assert.NotNull(level3);

        Assert.Equal(0, level1["startLine"]?.Value<int>());
        Assert.Equal(6, level1["endLine"]?.Value<int>());

        Assert.Equal(1, level2["startLine"]?.Value<int>());
        Assert.Equal(5, level2["endLine"]?.Value<int>());

        Assert.Equal(2, level3["startLine"]?.Value<int>());
        Assert.Equal(4, level3["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test05_EmptyRegion()
    {
        var text = @"#region Empty
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var range = ranges[0];
        Assert.Equal(0, range["startLine"]?.Value<int>());
        Assert.Equal(1, range["endLine"]?.Value<int>());
        Assert.Equal("Empty", range["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test06_RegionWithNoName()
    {
        var text = @"#region
mov eax, ebx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var range = ranges[0];
        Assert.Equal(0, range["startLine"]?.Value<int>());
        Assert.Equal(2, range["endLine"]?.Value<int>());
        // Collapsed text should be "..." when no name provided
        var collapsedText = range["collapsedText"]?.Value<string>();
        Assert.True(string.IsNullOrEmpty(collapsedText) || collapsedText == "...");
    }

    [Fact]
    public async Task Test07_RegionWithLongName()
    {
        var text = @"#region This is a very long region name with lots of description text
mov eax, ebx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var range = ranges[0];
        Assert.Equal("This is a very long region name with lots of description text",
                     range["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test08_CaseInsensitiveRegionKeywords()
    {
        var text = @"#REGION UpperCase
mov eax, 1
#ENDREGION

#Region MixedCase
mov ebx, 2
#EndRegion

#region lowercase
mov ecx, 3
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Equal(3, ranges.Count);

        Assert.Equal("UpperCase", ranges[0]["collapsedText"]?.Value<string>());
        Assert.Equal("MixedCase", ranges[1]["collapsedText"]?.Value<string>());
        Assert.Equal("lowercase", ranges[2]["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test09_RegionAtStartOfFile()
    {
        var text = @"#region First
mov eax, ebx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal(0, ranges[0]["startLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test10_RegionAtEndOfFile()
    {
        var text = @"mov eax, 1
mov ebx, 2
#region Last
mov ecx, 3
#endregion";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal(2, ranges[0]["startLine"]?.Value<int>());
        Assert.Equal(4, ranges[0]["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test11_RegionWithCommentsInside()
    {
        var text = @"#region WithComments
; This is a comment
mov eax, ebx  ; inline comment
; Another comment
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal(0, ranges[0]["startLine"]?.Value<int>());
        Assert.Equal(4, ranges[0]["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test12_RegionWithEmptyLines()
    {
        var text = @"#region WithBlanks
mov eax, ebx

mov ecx, edx

#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal(0, ranges[0]["startLine"]?.Value<int>());
        Assert.Equal(5, ranges[0]["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test13_UnclosedRegion_ShouldNotFold()
    {
        var text = @"#region Unclosed
mov eax, ebx
mov ecx, edx
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        // Unclosed regions should be ignored
        Assert.Empty(ranges);
    }

    [Fact]
    public async Task Test14_EndRegionWithoutStart_ShouldIgnore()
    {
        var text = @"mov eax, ebx
#endregion
mov ecx, edx
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        // Unmatched end should be ignored
        Assert.Empty(ranges);
    }

    [Fact]
    public async Task Test15_MultipleUnclosedRegions()
    {
        var text = @"#region First
mov eax, 1
#region Second
mov ebx, 2
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        // Both unclosed, no folding
        Assert.Empty(ranges);
    }

    [Fact]
    public async Task Test16_PartiallyNestedUnclosed()
    {
        var text = @"#region Outer
mov eax, 1
  #region Inner
  mov ebx, 2
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        // The single #endregion closes the most recent #region (Inner), Outer remains unclosed
        Assert.Single(ranges);
        Assert.Equal("Inner", ranges[0]["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test17_RegionWithSpecialCharactersInName()
    {
        var text = @"#region Data Structures & Algorithms (v2.0)
mov eax, ebx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal("Data Structures & Algorithms (v2.0)", ranges[0]["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test18_StartAndEndCharacterPositions()
    {
        var text = @"  #region Indented
  mov eax, ebx
  #endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var range = ranges[0];
        // StartCharacter should be at the position of '#' (position 2)
        Assert.NotNull(range["startCharacter"]);
        Assert.Equal(2, range["startCharacter"]?.Value<int>());

        // EndCharacter should be at end of #endregion
        Assert.NotNull(range["endCharacter"]);
    }

    [Fact]
    public async Task Test19_AdjacentRegionsNoGap()
    {
        var text = @"#region First
mov eax, 1
#endregion
#region Second
mov ebx, 2
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Equal(2, ranges.Count);

        Assert.Equal(0, ranges[0]["startLine"]?.Value<int>());
        Assert.Equal(2, ranges[0]["endLine"]?.Value<int>());

        Assert.Equal(3, ranges[1]["startLine"]?.Value<int>());
        Assert.Equal(5, ranges[1]["endLine"]?.Value<int>());
    }

    [Fact]
    public async Task Test20_RegionKindIsAlwaysRegion()
    {
        var text = @"#region Test
mov eax, ebx
#endregion
";
        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);
        Assert.Equal("region", ranges[0]["kind"]?.Value<string>());
    }

    [Fact]
    public async Task Test21_LargeFileWithManyRegions()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"#region Region{i}");
            sb.AppendLine($"mov eax, {i}");
            sb.AppendLine("#endregion");
            sb.AppendLine();
        }

        var ranges = await GetFoldingRanges(sb.ToString());
        Assert.NotNull(ranges);
        Assert.Equal(50, ranges.Count);

        // Verify first and last
        Assert.Equal("Region0", ranges[0]["collapsedText"]?.Value<string>());
        Assert.Equal("Region49", ranges[49]["collapsedText"]?.Value<string>());
    }

    [Fact]
    public async Task Test22_RegionWithTabsAndSpaces()
    {
        var text = "#region\tTabbed Name\t\n" +
                   "mov eax, ebx\n" +
                   "#endregion\n";

        var ranges = await GetFoldingRanges(text);
        Assert.NotNull(ranges);
        Assert.Single(ranges);

        var collapsedText = ranges[0]["collapsedText"]?.Value<string>();
        Assert.NotNull(collapsedText);
        // Should contain "Tabbed Name" after trimming
        Assert.Contains("Tabbed Name", collapsedText);
    }

    [Fact]
    public async Task Test23_MultipleDocumentsSeparately()
    {
        // Test that different documents maintain separate folding state
        var text1 = @"#region Doc1
mov eax, 1
#endregion
";
        var text2 = @"#region Doc2
mov ebx, 2
#endregion
";

        var ranges1 = await GetFoldingRanges(text1, "file:///doc1.asm");
        var ranges2 = await GetFoldingRanges(text2, "file:///doc2.asm");

        Assert.NotNull(ranges1);
        Assert.NotNull(ranges2);
        Assert.Single(ranges1);
        Assert.Single(ranges2);

        Assert.Equal("Doc1", ranges1[0]["collapsedText"]?.Value<string>());
        Assert.Equal("Doc2", ranges2[0]["collapsedText"]?.Value<string>());
    }
}
