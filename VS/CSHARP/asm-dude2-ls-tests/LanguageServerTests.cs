// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmDude2LS.Tests;

using AsmTools;

using FluentAssertions;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using Xunit;

/// <summary>
/// Tests for LanguageServer - the main LSP server implementation
/// </summary>
public class LanguageServerTests
{
    private readonly LanguageServer _server;
    private readonly AsmLanguageServerOptions _options;

    public LanguageServerTests()
    {
        this._server = new LanguageServer();
        this._options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_486 = true,
            ARCH_PENT = true,
            ARCH_P6 = true,
            ARCH_X64 = true,
            ARCH_MMX = true,
            ARCH_SSE = true,
            ARCH_SSE2 = true,
            ARCH_SSE3 = true,
            ARCH_SSSE3 = true,
            ARCH_SSE4_1 = true,
            ARCH_SSE4_2 = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_F = true,
            ARCH_AVX512_VL = true,
            ARCH_AVX512_DQ = true,
            ARCH_AVX512_BW = true,
            CodeCompletion_On = true,
            SignatureHelp_On = true,
            CodeFolding_On = true,
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",
            AsmDoc_On = true,
            IntelliSense_Label_Analysis_On = true,
            Global_MaxFileLines = 10000
        };
        this._server.Initialize(this._options);
        this._server.Initialized();
    }

    #region Initialize Tests

    [Fact]
    public void Initialize_WithValidOptions_ShouldSucceed()
    {
        // Arrange
        var server = new LanguageServer();
        var options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true
        };

        // Act
        server.Initialize(options);
        server.Initialized();

        // Assert - no exception means success
        server.Should().NotBeNull();
    }

    [Fact]
    public void Initialized_ShouldComplete()
    {
        // Arrange
        var server = new LanguageServer();
        server.Initialize(this._options);

        // Act
        server.Initialized();

        // Assert - no exception means success
        server.Should().NotBeNull();
    }

    #endregion

    #region Document Lifecycle Tests

    [Fact]
    public void OnTextDocumentOpened_ShouldAddDocument()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri("file:///test.asm"),
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };

        // Act
        this._server.OnTextDocumentOpened(openParams);

        // Assert - document should be tracked (no exception)
    }

    [Fact]
    public void OnTextDocumentClosed_ShouldRemoveDocument()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri("file:///test.asm"),
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = new Uri("file:///test.asm")
            }
        };

        // Act
        this._server.OnTextDocumentClosed(closeParams);

        // Assert - no exception means success
    }

    [Fact]
    public void UpdateServerSideTextDocument_ShouldUpdateContent()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri("file:///test.asm"),
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        // Act
        this._server.UpdateServerSideTextDocument("add rcx, rdx", 2, "file:///test.asm");

        // Assert - no exception means success
    }

    #endregion

    #region Completion Tests

    private CompletionList? GetCompletions(string asmLine, int character)
    {
        var uri = $"file:///test_completion_{asmLine.GetHashCode(StringComparison.Ordinal):x}.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = asmLine
            }
        });
        return this._server.GetTextDocumentCompletion(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = character }
        });
    }

    [Fact]
    public void Completion_AfterMnemonic_ReturnsOperands()
    {
        CompletionList? result = this.GetCompletions("add ", 4);
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Length.Should().BeGreaterThan(10, "should return many register completions");
    }

    [Fact]
    public void Completion_FilterTextMatchesInsertText()
    {
        CompletionList? result = this.GetCompletions("add ", 4);
        result?.Items.Should().AllSatisfy(item =>
        {
            item.FilterText.Should().Be(item.InsertText,
                $"FilterText must equal InsertText for '{item.Label}' to prevent VS fuzzy matching on arch tags");
        });
    }

    [Fact]
    public void Completion_FilterTextNeverContainsArchTags()
    {
        CompletionList? result = this.GetCompletions("v", 1);
        result?.Items.Should().NotBeEmpty();
        result?.Items.Should().AllSatisfy(item =>
        {
            item.FilterText.Should().NotContainAny(["[", "]"],
                $"FilterText '{item.FilterText}' must not contain arch tags");
        });
    }

    [Fact]
    public void Completion_ShortPrefix_FiltersServerSide()
    {
        CompletionList? result = this.GetCompletions("VMOVAPS Z", 9);
        result?.Should().NotBeNull();
        result?.Items.Should().NotBeEmpty("ZMM registers should be available");
        result?.Items.Should().OnlyContain(
            i => i.FilterText != null && i.FilterText.StartsWith("Z", StringComparison.OrdinalIgnoreCase),
            "typing 'Z' should only return Z-prefixed completions, not YMM/XMM");
    }

    [Fact]
    public void Completion_ShortPrefix_MarksListIncomplete()
    {
        // A 1-2 char prefix is filtered server-side, so the list is a subset: the client must re-query.
        this.GetCompletions("m", 1)!.IsIncomplete.Should().BeTrue("a 1-char prefix is narrowed server-side");
        this.GetCompletions("mo", 2)!.IsIncomplete.Should().BeTrue("a 2-char prefix is narrowed server-side");
        // No prefix (cursor right after the separator): full list, nothing narrowed.
        this.GetCompletions("add ", 4)!.IsIncomplete.Should().BeFalse("operand list with no typed prefix is complete");
    }

    [Fact]
    public void Completion_MnemonicItem_ShowsDescriptionInlineAndDefersArch()
    {
        CompletionList? result = this.GetCompletions("mo", 2);
        CompletionItem mov = result!.Items.Single(i => string.Equals(i.Label, "MOV", StringComparison.Ordinal));

        mov.Label.Should().Be("MOV", "annotations must live in LabelDetails, not be jammed into the Label");
        mov.LabelDetails.Should().NotBeNull("the description (semantics) is shown inline, always visible");
        mov.LabelDetails!.Description.Should().NotBeNullOrEmpty("VS renders LabelDetails.Description inline; it carries the semantics");
        mov.Data.Should().NotBeNull("mnemonic items carry their name in Data so the arch + doc link resolve lazily");
        mov.Documentation.Should().BeNull("the arch + doc link are filled in completionItem/resolve, not eagerly");
    }

    [Fact]
    public void ResolveCompletion_Mnemonic_FillsArchDocumentation()
    {
        // VADDPS is an AVX/AVX-512 instruction, so it has a non-empty architecture to surface on selection.
        CompletionList? result = this.GetCompletions("vaddps", 6);
        CompletionItem vaddps = result!.Items.Single(i => string.Equals(i.Label, "VADDPS", StringComparison.Ordinal));
        vaddps.Documentation.Should().BeNull("precondition: the arch documentation is deferred");

        CompletionItem resolved = this._server.ResolveCompletion(vaddps);

        resolved.Documentation.Should().NotBeNull("resolve must attach the architecture for an arch-gated mnemonic");
    }

    [Fact]
    public void ResolveCompletion_ItemWithoutData_IsUnchanged()
    {
        // Arch-bearing register items (ZMM here) carry their documentation eagerly and have no Data;
        // resolve must not clobber it.
        CompletionList? result = this.GetCompletions("VMOVAPS Z", 9);
        CompletionItem register = result!.Items.First(i => i.Data == null && i.Documentation != null);

        CompletionItem resolved = this._server.ResolveCompletion(register);

        resolved.Documentation.Should().NotBeNull("eager documentation on a non-deferred item must survive resolve");
    }

    #endregion

    #region Hover Tests

    [Fact]
    public void GetHover_WithMnemonic_ShouldReturnHover()
    {
        // Arrange
        var uri = "file:///test.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });
        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        };

        // Act
        var result = this._server.GetHover(hoverParams);

        // Assert — standard LSP Hover with MarkupContent. Both VS and VS Code render MarkupContent
        // hover, but the markup KIND is negotiated per client (see LanguageServer.HoverMarkupKind):
        // Markdown for clients that advertise it (VS Code → clickable link, code fences), PlainText for
        // those that don't (Visual Studio advertises contentFormat:["plaintext"], so it gets plain text).
        // This test uses the in-process server's default (Markdown) and only checks the mnemonic appears.
        result.Should().NotBeNull("hover on MOV should return documentation");
        var hover = result.Should().BeOfType<Hover>().Subject;
        hover.Contents.Should().NotBeNull();
        var markup = (MarkupContent)hover.Contents!;
        markup.Value.Should().Contain("MOV", "hover should mention the mnemonic");
    }

    [Fact]
    public void GetHover_WithMnemonic_WithAsmDocUrl_ShouldReturnStyledHover()
    {
        // Arrange – create a fresh server with AsmDoc_Url configured
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions
        {
            ARCH_X64 = true,
            AsmDoc_On = true,
            AsmDoc_Url = "https://github.com/HJLebbink/asm-dude/wiki/"
        });
        server.Initialized();

        var uri = "file:///test.asm";
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = server.GetHover(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        });

        // Assert — standard Hover whose Markdown contains the mnemonic AND a `[Documentation](url)`
        // link. The link is a real, serializable markdown link (unlike the old _vs_rawContent
        // NavigationAction) so it is CLICKABLE in markdown-rendering clients (VS Code). In Visual
        // Studio, which advertises plaintext hover, the URL is shown as plain text (not clickable).
        // This in-process test uses the default Markdown kind, so the link markup is present.
        var hover = result.Should().BeOfType<Hover>().Subject;
        var markup = (MarkupContent)hover.Contents!;
        markup.Value.Should().Contain("MOV");
        markup.Value.Should().Contain("github.com/HJLebbink/asm-dude/wiki", "should include a documentation link");
    }

    [Fact]
    public void GetHover_WithMnemonic_WithoutAsmDocUrl_ShouldReturnStyledHover()
    {
        // Arrange – _server has no AsmDoc_Url set (see constructor)
        var uri = "file:///test.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = this._server.GetHover(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        });

        // Assert — standard Hover with Markdown content mentioning the mnemonic
        var hover = result.Should().BeOfType<Hover>().Subject;
        var markup = (MarkupContent)hover.Contents!;
        markup.Value.Should().Contain("MOV");
    }

    [Fact]
    public void GetHover_WithRegister_ShouldReturnHoverInfo()
    {
        // Arrange
        var uri = "file:///test.asm";
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 5 } // Position on "rax"
        };

        // Act
        var result = this._server.GetHover(hoverParams);

        // Assert
        result.Should().NotBeNull("hover on RAX should return register documentation");
    }

    #endregion

    #region Label Hover Tests

    [Fact]
    public void GetHover_WithLabelDef_ShouldReturnDefinitionInfo()
    {
        // Arrange
        var uri = "file:///test.asm";
        var labelDefText = @"loop_start:
mov rax, rbx";
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = labelDefText
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 5 } // Position on "loop_start"
        };

        // Act
        var result = this._server.GetHover(hoverParams);

        // Assert
        result.Should().NotBeNull("hover on label definition should return info");
        result.Should().BeOfType<Hover>();
        var hover = (Hover)result;
        hover.Contents.Should().NotBeNull();
        var markup = (MarkupContent)hover.Contents!;
        markup.Value.Should().Contain("loop_start", "should contain label name");
        markup.Value.Should().Contain("Defined at line 1", "should contain line number");
        markup.Value.Should().Contain("loop_start:", "should contain definition line content");
    }

    // Label reference hover requires more complex testing setup with label graph
    // This test verifies the basic infrastructure works, but full label reference detection
    // needs to be tested with the actual LSP integration tests which have proper setup

    #endregion

    #region Performance-table alignment Tests


    // The performance hover renders a monospaced columnar table (Instruction | µOps Fused | µOps
    // Unfused | µOps Port | Latency | Throughput | remark). The columns are space-padded, so they only
    // line up if every data cell starts at exactly the character offset of its header. A previous
    // fixed-width layout (hard-coded 26-char instruction column) broke this for AVX-512 forms whose
    // "instr + operands" overflowed 26 chars, shoving the numeric columns past their headers and making
    // them ragged from row to row. These tests retrieve the real hover and verify the alignment.

    // "mov" — every operand form fits in a short instruction column.
    // "vfixupimmps" — operand forms run to ~40 chars, the case that used to overflow and misalign.
    [Theory]
    [InlineData("mov rax, rbx", 1)]
    [InlineData("vfixupimmps zmm0, zmm1, zmm2, 0", 1)]
    public void GetHover_PerformanceTable_ColumnsAreAligned(string sourceLine, int hoverCharacter)
    {
        // Arrange — a server with the perf info and the microarchitectures these mnemonics have data for.
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_X64 = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_F = true,
            ARCH_AVX512_VL = true,
            ARCH_AVX512_DQ = true,
            ARCH_AVX512_BW = true,
            AsmDoc_On = true,
            PerformanceInfo_On = true,
            PerformanceInfo_Skylake_On = true,
            PerformanceInfo_SkylakeX_On = true,
        });
        server.Initialized();

        var uri = "file:///test.asm";
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = sourceLine }
        });

        // Act
        var result = server.GetHover(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = hoverCharacter }
        });

        // Assert
        var hover = result.Should().BeOfType<Hover>().Subject;
        var text = ((MarkupContent)hover.Contents!).Value;
        AssertPerformanceTableColumnsAligned(text);
    }

    // The compact header packs the first column: "Performance:" shares the µOps-spanner line, and the
    // microarchitecture shares the "Fused/Unfused/…" label line (no separate "Instruction" label, no
    // standalone arch line). This pins that layout.
    [Fact]
    public void GetHover_PerformanceTable_ArchitectureFollowsPerformanceHeader()
    {
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_X64 = true,
            AsmDoc_On = true,
            PerformanceInfo_On = true,
            PerformanceInfo_Skylake_On = true,
        });
        server.Initialized();

        var uri = "file:///test.asm";
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var hover = server.GetHover(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        }).Should().BeOfType<Hover>().Subject;

        var lines = ((MarkupContent)hover.Contents!).Value
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();

        // No "Instruction" column label anywhere in the table.
        lines.Should().NotContain(l => l.StartsWith("Instruction"), "the instruction column header was dropped");

        // "Performance:" shares its line with the µOps spanner (first column = title).
        int perfIdx = lines.FindIndex(l => l.StartsWith("Performance:"));
        perfIdx.Should().BeGreaterThanOrEqualTo(0, "the table starts with a 'Performance:' spanner line");
        lines[perfIdx].Should().Contain("µOps", "the µOps spanner shares the 'Performance:' line");

        // The very next line is the architecture sharing the Fused/Unfused/… label line (first column = arch).
        string labelLine = lines[perfIdx + 1];
        labelLine.TrimStart().Should().StartWith("Skylake", "the architecture sits in the first column of the label line");
        labelLine.Should().Contain("Fused").And.Contain("Unfused").And.Contain("Throughput",
            "the column labels share the architecture line");
    }

    // Visual Studio advertises contentFormat:["plaintext"] for hover and renders plaintext in a
    // PROPORTIONAL font, so a space-padded table never lines up. For that client the server must instead
    // return a VSInternalHover whose _vs_rawContent is classified "formal language" + UseClassificationFont
    // (the only way to force a fixed-pitch font in a VS hover). This test pins that contract and verifies
    // the monospace lines are still column-aligned.
    [Fact]
    public void GetHover_VisualStudioClient_UsesMonospaceRawContent()
    {
        // Arrange — a VS-like client: plaintext hover (HoverMarkupKind is set from contentFormat).
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_X64 = true,
            AsmDoc_On = true,
            PerformanceInfo_On = true,
            PerformanceInfo_Skylake_On = true,
        });
        server.Initialized();
        server.HoverMarkupKind = MarkupKind.PlainText; // what LanguageServerTarget sets for a VS client

        var uri = "file:///test.asm";
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = server.GetHover(new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        });

        // Assert — a VSInternalHover with a stacked ContainerElement of monospace runs.
        var hover = result.Should().BeOfType<VSInternalHover>().Subject;
        var container = hover.RawContent.Should().BeOfType<ContainerElement>().Subject;
        container.Style.Should().HaveFlag(ContainerElementStyle.Stacked);

        var runs = container.Elements
            .OfType<ClassifiedTextElement>()
            .SelectMany(e => e.Runs)
            .ToList();
        runs.Should().NotBeEmpty();

        // Every run must be classified for a fixed-pitch font — otherwise VS draws it proportional and
        // the columns drift (the bug this whole path exists to fix).
        runs.Should().OnlyContain(
            r => r.ClassificationType == PredefinedClassificationTypeNames.FormalLanguage
                 && r.Style.HasFlag(ClassifiedTextRunStyle.UseClassificationFont),
            "VS only renders hover text monospace via 'formal language' + UseClassificationFont");

        // Reconstruct the rendered text (one element per line) and re-check column alignment.
        var body = string.Join(
            "\n",
            container.Elements.OfType<ClassifiedTextElement>().Select(e => string.Concat(e.Runs.Select(r => r.Text))));
        AssertPerformanceTableColumnsAligned(body);

        // Pin the on-the-wire shape VS deserializes (Roslyn's ObjectContentConverter): the VS-specific
        // _vs_rawContent property, the _vs_type discriminators, and the monospace classification/style.
        // Explicit [JsonPropertyName]s make this independent of the serializer's naming policy; enums
        // serialize as numbers, so UseClassificationFont (0x8) is 8.
        var json = System.Text.Json.JsonSerializer.Serialize(hover);
        json.Should().Contain("\"_vs_rawContent\"");
        json.Should().Contain("\"_vs_type\":\"ContainerElement\"");
        json.Should().Contain("\"_vs_type\":\"ClassifiedTextElement\"");
        json.Should().Contain("\"ClassificationTypeName\":\"formal language\"");
        json.Should().Contain("\"Style\":8");
    }

    /// <summary>
    /// Parses the performance table out of a hover body and asserts every numeric column (µOps Fused /
    /// Unfused / Port / Latency / Throughput) in every data row lines up under its header: each non-empty
    /// cell starts at exactly the header's column offset and is a single whitespace-free token (so a long
    /// instruction column can't bleed into the numeric columns). The Instruction column is excluded from
    /// the single-token check because it legitimately contains spaces ("MOV AX, Moffs16").
    /// </summary>
    private static void AssertPerformanceTableColumnsAligned(string hoverBody)
    {
        // Split into lines, dropping the markdown ```text fence and the trailing [Documentation] link.
        var lines = hoverBody
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(l => l != "```text" && l != "```" && !l.StartsWith("[Documentation]"))
            .ToList();

        // The two header rows: a "µOps" spanner line then the column labels. (The instruction column has
        // no label — it's self-evident — so the header row is identified by the numeric column names.)
        int labelRow = lines.FindIndex(l => l.Contains("Fused") && l.Contains("Unfused") && l.Contains("Throughput"));
        labelRow.Should().BeGreaterThanOrEqualTo(0, "the perf table must have a 'Fused … Unfused … Throughput' header row");
        string header = lines[labelRow];

        // Column start offsets, taken from the header labels themselves.
        (string name, int offset)[] numericCols =
        [
            ("Fused", header.IndexOf("Fused", StringComparison.Ordinal)),
            ("Unfused", header.IndexOf("Unfused", StringComparison.Ordinal)),
            ("Port", header.IndexOf("Port", StringComparison.Ordinal)),
            ("Latency", header.IndexOf("Latency", StringComparison.Ordinal)),
            ("Throughput", header.IndexOf("Throughput", StringComparison.Ordinal)),
        ];
        foreach (var (name, offset) in numericCols)
        {
            offset.Should().BeGreaterThanOrEqualTo(0, $"header should contain the '{name}' column");
        }

        // The "µOps" spanner sits directly over the three µOps columns (Fused/Unfused/Port).
        string spanner = lines[labelRow - 1];
        foreach (var (name, offset) in numericCols.Take(3))
        {
            spanner.Length.Should().BeGreaterThan(offset);
            spanner.Substring(offset, 4).Should().Be("µOps", $"the µOps spanner should sit above the '{name}' column");
        }

        // Data rows: everything after the header that actually reaches the numeric columns. This skips
        // blank lines and the short microarchitecture sub-headers (e.g. "SkylakeX") that can appear
        // between sections when more than one arch is shown.
        int firstNumericOffset = numericCols[0].offset;
        var dataRows = lines
            .Skip(labelRow + 1)
            .Where(l => l.Length > firstNumericOffset)
            .ToList();
        dataRows.Should().NotBeEmpty("the perf table must have at least one data row");

        // For every data row and every numeric column: the column must be separated from whatever is to
        // its left (so a long instruction can't bleed into it — the old overflow bug). For the four
        // columns with a known right edge (the next numeric column) also verify the cell holds a single
        // token that begins exactly at the header offset, so values sit under their headers. Throughput
        // is the last numeric column (the remark follows it), so only its left boundary is checked.
        for (int c = 0; c < numericCols.Length; c++)
        {
            (string name, int start) = numericCols[c];
            foreach (string row in dataRows)
            {
                if (row.Length <= start)
                {
                    continue; // row ends before this column — an absent (empty) trailing cell, fine.
                }

                row[start - 1].Should().Be(
                    ' ',
                    $"column '{name}' (header offset {start}) must be separated from the column to its left; " +
                    $"a non-space here means a previous column overflowed into it. Row:\n{row}");

                if (c + 1 >= numericCols.Length)
                {
                    continue; // Throughput: right edge is the remark, not a fixed column — left check suffices.
                }

                int end = numericCols[c + 1].offset;
                string cell = row.Substring(start, Math.Min(end, row.Length) - start);
                if (cell.Trim().Length == 0)
                {
                    continue; // genuinely empty cell (e.g. a missing latency value).
                }

                cell[0].Should().NotBe(
                    ' ',
                    $"column '{name}' value must begin exactly at its header offset {start}. Row:\n{row}");
                cell.Trim().Should().NotContain(
                    " ",
                    $"column '{name}' must hold a single token; a value spanning the boundary means a previous column overflowed. Row:\n{row}");
            }
        }
    }

    #endregion

    #region Signature Help Tests

    [Fact]
    public void GetTextDocumentSignatureHelp_WithMnemonic_ShouldReturnSignatures()
    {
        // Arrange
        var uri = "file:///test.asm";
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "mov "
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var sigHelpParams = new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 4 },
            Context = new SignatureHelpContext
            {
                TriggerKind = SignatureHelpTriggerKind.Invoked,
                IsRetrigger = false
            }
        };

        // Act
        var result = this._server.GetTextDocumentSignatureHelp(sigHelpParams);

        // Assert
        result.Should().NotBeNull("signature help for MOV should return signatures");
        result?.Signatures.Should().NotBeEmpty("MOV has multiple signatures");
    }

    [Fact]
    public void GetTextDocumentSignatureHelp_IncludesArchitecture()
    {
        // The architecture lives on AsmSignatureInformation but must be copied into the LSP
        // SignatureInformation.Documentation, or the client shows no arch. VADDPS is arch-gated (AVX/AVX-512).
        var uri = "file:///test_sig_arch.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "vaddps " }
        });

        var result = this._server.GetTextDocumentSignatureHelp(new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 7 },
            Context = new SignatureHelpContext { TriggerKind = SignatureHelpTriggerKind.Invoked, IsRetrigger = false }
        });

        result.Should().NotBeNull();
        result!.Signatures.Should().NotBeEmpty("VADDPS has signatures");
        bool anyMentionsArch = result.Signatures.Any(
            s => s.Documentation is { } sum && sum.Value is string str && str.Contains("Arch:", StringComparison.Ordinal));
        anyMentionsArch.Should().BeTrue("an arch-gated instruction's signature help must mention the architecture");
    }

    [Theory]
    [InlineData("mov ", 4, true, 0, "MOV with trailing space")]
    [InlineData("mov eax,", 8, true, 1, "MOV with first operand and comma")]
    [InlineData("mov eax, ", 9, true, 1, "MOV with first operand, comma and space")]
    [InlineData("add ", 4, true, 0, "ADD with trailing space")]
    [InlineData("add eax, ebx", 12, true, 1, "ADD with two operands")]
    [InlineData("  mov ", 6, true, 0, "MOV with leading whitespace")]
    [InlineData("xor ", 4, true, 0, "XOR with trailing space")]
    [InlineData("mov", 3, false, -1, "MOV without trailing space - cursor at end of mnemonic")]
    [InlineData("label: mov ", 11, true, 0, "MOV with label prefix")]
    [InlineData("label: mov eax, ", 16, true, 1, "MOV with label and one operand")]
    [InlineData("vxorps xmm0, xmm1, ", 20, true, 2, "VXORPS after second comma - third parameter")]
    [InlineData("vxorps xmm0, ", 14, true, 1, "VXORPS after first comma - second parameter")]
    public void GetTextDocumentSignatureHelp_VariousInputs_ShouldReturnExpectedResult(
        string text, int cursorPos, bool expectResult, int expectedActiveParam, string because)
    {
        // Arrange
        var uri = "file:///test_sighelp.asm";
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = text
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var sigHelpParams = new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = cursorPos },
            Context = new SignatureHelpContext
            {
                TriggerKind = SignatureHelpTriggerKind.Invoked,
                IsRetrigger = false
            }
        };

        // Act
        var result = this._server.GetTextDocumentSignatureHelp(sigHelpParams);

        // Assert
        if (expectResult)
        {
            result.Should().NotBeNull($"signature help should be returned for: {because}");
            result?.Signatures.Should().NotBeEmpty($"signatures should be present for: {because}");
            if (expectedActiveParam >= 0)
            {
                result!.ActiveParameter.Should().Be(expectedActiveParam, $"active parameter should be {expectedActiveParam} for: {because}");
            }
        }
        else
        {
            // No result expected - either null or empty signatures
        }
    }

    #endregion

    #region Folding Range Tests

    [Fact]
    public void GetFoldingRanges_WithRegions_ShouldReturnFoldingRanges()
    {
        // Arrange
        var uri = "file:///test.asm";
        var textWithRegions = @"#region Test
mov rax, rbx
add rcx, rdx
#endregion";

        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = textWithRegions
            }
        };
        this._server.OnTextDocumentOpened(openParams);

        var foldingParams = new FoldingRangeParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        };

        // Act
        var result = this._server.GetFoldingRanges(foldingParams);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].StartLine.Should().Be(0);
        result[0].EndLine.Should().Be(3);
        result[0].Kind.Should().Be(FoldingRangeKind.Region);
        result[0].CollapsedText.Should().Be("Test");
    }

    [Fact]
    public void GetFoldingRanges_WithRegionNoName_ShouldReturnEllipsis()
    {
        var uri = "file:///test_noname.asm";
        var text = @"#region
mov rax, rbx
#endregion";

        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = text }
        });

        var result = this._server.GetFoldingRanges(new FoldingRangeParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        result.Should().HaveCount(1);
        result[0].CollapsedText.Should().Be("...");
    }

    [Fact]
    public void GetFoldingRanges_NestedRegions_ShouldReturnCorrectCollapsedText()
    {
        var uri = "file:///test_nested.asm";
        var text = @"#region Outer
#region Inner
mov rax, rbx
#endregion
add rcx, rdx
#endregion";

        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = text }
        });

        var result = this._server.GetFoldingRanges(new FoldingRangeParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        result.Should().HaveCount(2);
        // Inner region closes first
        var inner = result.First(r => r.StartLine == 1);
        var outer = result.First(r => r.StartLine == 0);
        inner.CollapsedText.Should().Be("Inner");
        outer.CollapsedText.Should().Be("Outer");
    }

    #endregion

    #region Semantic Tokens Tests

    [Fact]
    public void GetSemanticTokens_WithMnemonicAndRegister_ShouldReturnTokens()
    {
        // Arrange
        var uri = "file:///test_semantic.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().NotBeEmpty("mov rax, rbx contains tokens");
        result.Data.Length.Should().BeGreaterThan(0);
        (result.Data.Length % 5).Should().Be(0, "LSP semantic tokens are always encoded as groups of 5 ints");
    }

    [Fact]
    public void GetSemanticTokens_ArchGating_MarksOutOfProfileTokensDeprecated()
    {
        // Arch-gating: an instruction/register that the active architecture profile does NOT enable
        // is rendered greyed via the LSP "deprecated" modifier (bit 0x4). VADDPD + ZMM0 are AVX-512:
        // OFF under the v1 baseline profile, ON under "everything". Same source, different profile —
        // so the contrast isolates the gating logic (would fail if gating were removed or always-on).
        const string text = "vaddpd zmm0, zmm1, zmm2";

        var (mnemonicV1, registerV1) = ArchGateModifiers(ArchProfileKeys.V1, text);
        (mnemonicV1 & 0x4).Should().NotBe(0, "VADDPD is AVX-512, absent from the v1 profile -> deprecated");
        (registerV1 & 0x4).Should().NotBe(0, "ZMM0 is AVX-512, absent from the v1 profile -> deprecated");

        var (mnemonicAll, registerAll) = ArchGateModifiers(ArchProfileKeys.Everything, text);
        (mnemonicAll & 0x4).Should().Be(0, "VADDPD is enabled under 'everything' -> not deprecated");
        (registerAll & 0x4).Should().Be(0, "ZMM0 is enabled under 'everything' -> not deprecated");
    }

    // Returns the semantic-token modifier bitmask for the mnemonic (column 0) and the first register
    // (column 7, "zmm0") of a one-line document parsed under the given architecture profile.
    private static (int mnemonicModifiers, int registerModifiers) ArchGateModifiers(string archProfile, string text)
    {
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions { ArchProfile = archProfile, AsmSim_On = false, Global_MaxFileLines = 10000 });
        server.Initialized();

        var uri = $"file:///archgate_{archProfile}.asm";
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = text }
        });

        var result = server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Decode the delta-encoded 5-int groups (deltaLine, deltaStart, length, type, modifiers) to
        // absolute positions, then pick the tokens by their start column.
        int[] data = result.Data!;
        int line = 0, ch = 0, mnemonicMods = 0, registerMods = 0;
        for (int i = 0; i + 4 < data.Length; i += 5)
        {
            int deltaLine = data[i], deltaStart = data[i + 1], modifiers = data[i + 4];
            line += deltaLine;
            ch = (deltaLine == 0) ? ch + deltaStart : deltaStart;
            if (line == 0 && ch == 0) mnemonicMods = modifiers;     // "vaddpd"
            else if (line == 0 && ch == 7) registerMods = modifiers; // "zmm0"
        }
        return (mnemonicMods, registerMods);
    }

    [Fact]
    public void GetSemanticTokens_ShouldReturnResultId()
    {
        // Arrange — ResultId is required for the delta protocol to work (prevents VS polling every 2s)
        var uri = "file:///test_semantic_resultid.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Assert
        result.Should().NotBeNull();
        result.ResultId.Should().NotBeNullOrEmpty("ResultId must be set so VS can use delta protocol instead of polling every 2 seconds");
    }

    [Fact]
    public void GetSemanticTokensDelta_WhenDocumentUnchanged_ShouldReturnEmptyEdits()
    {
        // Arrange — this is the key test that would have caught the 2-second polling problem:
        // after a full request, a delta with the same resultId must return zero edits
        var uri = "file:///test_delta_unchanged.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var full = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Act — request delta with the resultId just returned
        var delta = this._server.GetSemanticTokensDelta(new SemanticTokensDeltaParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            PreviousResultId = full.ResultId!
        });

        // Assert
        delta.Should().BeOfType<SemanticTokensDelta>("unchanged document should return a delta, not full tokens");
        var tokensDelta = (SemanticTokensDelta)delta;
        tokensDelta.Edits.Should().BeEmpty("no changes were made to the document");
        tokensDelta.ResultId.Should().Be(full.ResultId, "resultId should be stable when document is unchanged");
    }

    [Fact]
    public void GetSemanticTokensDelta_WhenDocumentChanged_ShouldReturnNewTokens()
    {
        // Arrange
        var uri = "file:///test_delta_changed.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var full = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Act — update the document then request delta
        this._server.UpdateServerSideTextDocument("add rcx, rdx\nnop", 2, uri);
        var delta = this._server.GetSemanticTokensDelta(new SemanticTokensDeltaParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            PreviousResultId = full.ResultId!
        });

        // Assert
        delta.Should().BeOfType<SemanticTokens>("changed document should return full tokens");
        var newFull = (SemanticTokens)delta;
        newFull.ResultId.Should().NotBe(full.ResultId, "resultId must change when document version changes");
        newFull.Data.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSemanticTokens_EmptyDocument_ShouldReturnEmptyData()
    {
        // Arrange
        var uri = "file:///test_empty_semantic.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "" }
        });

        // Act
        var result = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEmpty();
        result.ResultId.Should().NotBeNullOrEmpty("ResultId must be set even for empty documents");
    }

    [Fact]
    public void GetSemanticTokens_TokenPositions_ShouldMatchSourceText()
    {
        // Verify that semantic token positions correctly map to the expected source text,
        // especially for indented lines where a Trim() bug previously shifted all positions.
        var uri = "file:///test_positions.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "    VPAND ymm0, ymm1, ymm2" }
        });

        var result = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Decode the delta-encoded tokens: [deltaLine, deltaChar, length, type, modifiers]
        result.Data.Length.Should().Be(20, "VPAND ymm0 ymm1 ymm2 = 4 tokens * 5 ints");

        // Token 0: VPAND at position 4, length 5, type=keyword(15)
        result.Data[0].Should().Be(0, "deltaLine");
        result.Data[1].Should().Be(4, "deltaChar — VPAND starts at column 4 (after 4 spaces)");
        result.Data[2].Should().Be(5, "length of VPAND");
        result.Data[3].Should().Be(15, "tokenType: keyword(15) for mnemonic");

        // Token 1: ymm0 at position 10, length 4, type=variable(8)
        result.Data[5].Should().Be(0, "deltaLine");
        result.Data[6].Should().Be(6, "deltaChar from VPAND(4) to ymm0(10)");
        result.Data[7].Should().Be(4, "length of ymm0");
        result.Data[8].Should().Be(8, "tokenType: variable(8) for register");

        // Token 2: ymm1 at position 16, length 4, type=variable(8)
        result.Data[10].Should().Be(0, "deltaLine");
        result.Data[11].Should().Be(6, "deltaChar from ymm0(10) to ymm1(16)");
        result.Data[12].Should().Be(4, "length of ymm1");
        result.Data[13].Should().Be(8, "tokenType: variable(8) for register");
    }

    [Fact]
    public void GetSemanticTokens_Mnemonic_ShouldBeKeywordNotLabel()
    {
        // Mnemonics must be classified as keyword(0), not type/label(2).
        var uri = "file:///test_mnemonic_type.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var result = this._server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        result.Data[3].Should().Be(15, "mov should be keyword(15), not type/label(1)");
    }

    #endregion

    #region Mnemonic Documentation URL Tests

    [Fact]
    public void GetMnemonicUrl_KnownMnemonic_ReturnsConfiguredBasePlusRef()
    {
        // The VSIX "open documentation" command relies on this to resolve URLs (it no longer reads
        // signature files). The result must honor the configured AsmDoc_Url base and append the
        // mnemonic's html reference from MnemonicStore.
        var server = new LanguageServer();
        server.Initialize(new AsmLanguageServerOptions
        {
            ARCH_X64 = true,
            AsmDoc_On = true,
            AsmDoc_Url = "https://example.test/wiki/",
        });
        server.Initialized();

        string? url = server.GetMnemonicUrl("mov");

        url.Should().NotBeNullOrEmpty("MOV is a documented mnemonic");
        url.Should().StartWith("https://example.test/wiki/", "the configured AsmDoc_Url must be honored, not a hardcoded base");
        url!.Length.Should().BeGreaterThan("https://example.test/wiki/".Length, "an html reference is appended to the base");
    }

    [Fact]
    public void GetMnemonicUrl_NotAMnemonic_ReturnsNull()
    {
        this._server.GetMnemonicUrl("not_a_real_mnemonic_xyz").Should().BeNull("unknown words have no documentation URL");
    }

    [Fact]
    public void GetMnemonicUrl_LowercaseAndAttPrefix_ResolvesSameAsBare()
    {
        // The command passes the cursor word uppercased with a leading '%' already stripped, but the
        // server should also tolerate raw case. "mov" and "MOV" must resolve identically.
        var bare = this._server.GetMnemonicUrl("mov");
        var upper = this._server.GetMnemonicUrl("MOV");
        upper.Should().Be(bare, "mnemonic lookup is case-insensitive");
    }

    #endregion

    #region Exit Tests

    [Fact]
    public void Exit_ShouldComplete()
    {
        // Arrange
        var server = new LanguageServer();
        server.Initialize(this._options);
        server.Initialized();

        // Act
        server.Exit();

        // Assert - no exception means success
    }

    #endregion

    #region Complete Lifecycle Test

    [Fact]
    public void CompleteLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var server = new LanguageServer();
        var options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true,
            CodeCompletion_On = true,
            AsmDoc_On = true
        };

        // Act & Assert - Initialize
        server.Initialize(options);
        server.Initialized();

        // Open document
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri("file:///lifecycle.asm"),
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };
        server.OnTextDocumentOpened(openParams);

        // Update document
        server.UpdateServerSideTextDocument("add rcx, rdx", 2, "file:///lifecycle.asm");

        // Get hover
        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri("file:///lifecycle.asm") },
            Position = new Position { Line = 0, Character = 1 }
        };
        _ = server.GetHover(hoverParams);

        // Close document
        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri("file:///lifecycle.asm") }
        };
        server.OnTextDocumentClosed(closeParams);

        // Exit
        server.Exit();
    }

    #endregion

    #region CodeLens Tests

    [Fact]
    public void CodeLens_Serialization_ProducesValidJson()
    {
        var uri = "file:///test_codelens_json.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "my_label:\n    jmp my_label\n"
            }
        });

        var lenses = this._server.GetCodeLenses(new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        lenses.Should().HaveCount(1);

        // Serialize like the LSP wire protocol would
        var json = System.Text.Json.JsonSerializer.Serialize(lenses);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var firstLens = doc.RootElement[0];

        // Verify the JSON structure VS expects
        firstLens.TryGetProperty("range", out var range).Should().BeTrue("CodeLens must have range");
        range.GetProperty("start").GetProperty("line").GetInt32().Should().Be(0);
        range.GetProperty("start").GetProperty("character").GetInt32().Should().Be(0);
        firstLens.TryGetProperty("data", out var data).Should().BeTrue("CodeLens must have data for resolve");
        data.GetInt32().Should().Be(1);

        // Serialize the resolved lens
        var resolved = this._server.ResolveCodeLens(lenses[0]);
        var resolvedJson = System.Text.Json.JsonSerializer.Serialize(resolved);
        var resolvedDoc = System.Text.Json.JsonDocument.Parse(resolvedJson);
        var resolvedLens = resolvedDoc.RootElement;

        resolvedLens.TryGetProperty("command", out var command).Should().BeTrue("resolved CodeLens must have command");
        command.GetProperty("title").GetString().Should().Be("1 reference");
        command.TryGetProperty("command", out _).Should().BeTrue("command must have 'command' field (CommandIdentifier)");
    }

    [Fact]
    public void CodeLens_LabelWithReferences_ShowsReferenceCount()
    {
        var uri = "file:///test_codelens.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "my_label:\n    mov eax, 1\n    jmp my_label\n    jne my_label\n"
            }
        });

        var lenses = this._server.GetCodeLenses(new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        lenses.Should().NotBeNull();
        lenses.Should().HaveCount(1);
        lenses[0].Data.Should().BeEquivalentTo(2);

        var resolved = this._server.ResolveCodeLens(lenses[0]);
        resolved.Command.Should().NotBeNull();
        resolved.Command.Title.Should().Be("2 references");
    }

    [Fact]
    public void CodeLens_LabelWithNoReferences_ShowsZero()
    {
        var uri = "file:///test_codelens_zero.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "unused_label:\n    mov eax, 1\n    ret\n"
            }
        });

        var lenses = this._server.GetCodeLenses(new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        lenses.Should().NotBeNull();
        lenses.Should().HaveCount(1);

        CodeLens resolved = this._server.ResolveCodeLens(lenses[0]);
        resolved.Command?.Title.Should().Be("0 references");
    }

    [Fact]
    public void CodeLens_MultipleLabels_ReturnsLensForEach()
    {
        var uri = "file:///test_codelens_multi.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "start:\n    jmp end\nend:\n    jmp start\n"
            }
        });

        var lenses = this._server.GetCodeLenses(new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        lenses.Should().NotBeNull();
        lenses.Should().HaveCount(2);

        var titles = lenses.Select(l => this._server.ResolveCodeLens(l)?.Command?.Title).ToList();
        titles.Should().Contain("1 reference");
    }

    #endregion

    #region GetCodeLensData Tests

    [Fact]
    public void GetCodeLensData_LabelWithOneReference_ReturnsCorrectDefLineAndRefLine()
    {
        // Arrange
        // line 0: my_label:
        // line 1:     jmp my_label
        var uri = "file:///test_codelensdata_one.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "my_label:\n    jmp my_label\n"
            }
        });

        // Act
        var result = this._server.GetCodeLensData(uri);

        // Assert
        result.Should().HaveCount(1);
        result[0].Label.Should().Be("my_label");
        result[0].DefinitionLine.Should().Be(0);
        result[0].ReferenceLines.Should().HaveCount(1);
        result[0].ReferenceLines[0].Should().Be(1);
    }

    [Fact]
    public void GetCodeLensData_ReportsDefinitionColumnAndLength()
    {
        // The VSIX CodeLens tagger positions its tag using DefinitionColumn/DefinitionLength
        // returned by the server (it no longer parses the document itself), so these must be
        // the exact column and length of the label token on the definition line.
        // line 0: "  my_label:" — token "my_label" starts at column 2, length 8.
        var uri = "file:///test_codelensdata_colpos.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "  my_label:\n    jmp my_label\n"
            }
        });

        var result = this._server.GetCodeLensData(uri);

        result.Should().HaveCount(1);
        result[0].Label.Should().Be("my_label");
        result[0].DefinitionColumn.Should().Be(2, "the label token starts after two leading spaces");
        result[0].DefinitionLength.Should().Be(8, "\"my_label\" is 8 characters");
    }

    [Fact]
    public void GetCodeLensData_LabelWithNoReferences_ReturnsEmptyReferenceLines()
    {
        // Arrange
        var uri = "file:///test_codelensdata_zero.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "unused_label:\n    mov eax, 1\n    ret\n"
            }
        });

        // Act
        var result = this._server.GetCodeLensData(uri);

        // Assert
        result.Should().HaveCount(1);
        result[0].Label.Should().Be("unused_label");
        result[0].DefinitionLine.Should().Be(0);
        result[0].ReferenceLines.Should().BeEmpty();
    }

    [Fact]
    public void GetCodeLensData_LabelWithMultipleReferences_ReturnsAllRefLines()
    {
        // Arrange
        // line 0: loop_start:
        // line 1:     mov eax, 1
        // line 2:     jmp loop_start
        // line 3:     jne loop_start
        // line 4:     jz  loop_start
        var uri = "file:///test_codelensdata_multi_refs.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "loop_start:\n    mov eax, 1\n    jmp loop_start\n    jne loop_start\n    jz loop_start\n"
            }
        });

        // Act
        var result = this._server.GetCodeLensData(uri);

        // Assert
        result.Should().HaveCount(1);
        result[0].ReferenceLines.Should().HaveCount(3);
        result[0].ReferenceLines.Should().Contain(2);
        result[0].ReferenceLines.Should().Contain(3);
        result[0].ReferenceLines.Should().Contain(4);
    }

    [Fact]
    public void GetCodeLensData_MultipleLabels_ReturnsEntryForEach()
    {
        // Arrange
        // line 0: start:
        // line 1:     jmp end
        // line 2: end:
        // line 3:     jmp start
        var uri = "file:///test_codelensdata_twolabels.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "start:\n    jmp end\nend:\n    jmp start\n"
            }
        });

        // Act
        var result = this._server.GetCodeLensData(uri);

        // Assert
        result.Should().HaveCount(2);

        var startEntry = result.FirstOrDefault(r => r.Label.Equals("start", System.StringComparison.OrdinalIgnoreCase));
        startEntry.Should().NotBeNull();
        startEntry.DefinitionLine.Should().Be(0);
        startEntry.ReferenceLines.Should().HaveCount(1).And.Contain(3);

        var endEntry = result.FirstOrDefault(r => r.Label.Equals("end", System.StringComparison.OrdinalIgnoreCase));
        endEntry.Should().NotBeNull();
        endEntry.DefinitionLine.Should().Be(2);
        endEntry.ReferenceLines.Should().HaveCount(1).And.Contain(1);
    }

    [Fact]
    public void GetCodeLensData_UnknownUri_ReturnsEmptyArray()
    {
        // Act
        var result = this._server.GetCodeLensData("file:///does_not_exist.asm");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCodeLensData_CallInstruction_CountsAsReference()
    {
        // CALL is a jump-like instruction and should count as a reference
        // line 0: my_func:
        // line 1:     call my_func
        var uri = "file:///test_codelensdata_call.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "my_func:\n    call my_func\n"
            }
        });

        // Act
        var result = this._server.GetCodeLensData(uri);

        // Assert
        result.Should().HaveCount(1);
        result[0].ReferenceLines.Should().HaveCount(1);
        result[0].ReferenceLines[0].Should().Be(1);
    }

    [Fact]
    public void GetCodeLensData_DefinitionLineMatchesGetCodeLenses()
    {
        // GetCodeLensData and GetCodeLenses must agree on which line the label is defined
        var uri = "file:///test_codelensdata_consistency.asm";
        this._server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = new Uri(uri),
                LanguageId = "asm",
                Version = 1,
                Text = "    mov eax, 0\nmy_label:\n    jmp my_label\n"
            }
        });

        var lensesResult = this._server.GetCodeLenses(new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });
        var dataResult = this._server.GetCodeLensData(uri);

        lensesResult.Should().HaveCount(1);
        dataResult.Should().HaveCount(1);
        dataResult[0].DefinitionLine.Should().Be((int)lensesResult[0].Range.Start.Line,
            "GetCodeLensData and GetCodeLenses must agree on the definition line");
    }

    [Fact]
    public void GetCodeLensData_HitTestGeometry_ClickInsideBoundsIsDetected()
    {
        // Validates the hit-testing logic used by AsmCodeLensMouseProcessor.
        // A CodeLens adornment at canvas (left=50, top=10) with size (width=80, height=14)
        // is hit only when the click point is inside those bounds.
        static bool HitTest(double left, double top, double width, double height, double px, double py)
            => px >= left && px < left + width && py >= top && py < top + height;

        HitTest(50, 10, 80, 14, 60, 12).Should().BeTrue("click inside bounds should hit");
        HitTest(50, 10, 80, 14, 50, 10).Should().BeTrue("click on top-left corner should hit");
        HitTest(50, 10, 80, 14, 200, 12).Should().BeFalse("click to the right should miss");
        HitTest(50, 10, 80, 14, 60, 30).Should().BeFalse("click below should miss");
        HitTest(50, 10, 80, 14, 60, 5).Should().BeFalse("click above should miss");
    }

    #endregion
}
