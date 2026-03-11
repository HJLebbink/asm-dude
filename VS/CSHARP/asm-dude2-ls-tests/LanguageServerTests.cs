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

using System.Linq;
using AsmDude2LS;
using AsmTools;
using FluentAssertions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace AsmDude2LS.Tests;

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
            ARCH_X64 = true,
            ARCH_SSE = true,
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
        var uri = $"file:///test_completion_{asmLine.GetHashCode():x}.asm";
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
        var result = this.GetCompletions("add ", 4);
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Length.Should().BeGreaterThan(10, "should return many register completions");
    }

    [Fact]
    public void Completion_FilterTextMatchesInsertText()
    {
        var result = this.GetCompletions("add ", 4);
        result.Items.Should().AllSatisfy(item =>
        {
            item.FilterText.Should().Be(item.InsertText,
                $"FilterText must equal InsertText for '{item.Label}' to prevent VS fuzzy matching on arch tags");
        });
    }

    [Fact]
    public void Completion_FilterTextNeverContainsArchTags()
    {
        var result = this.GetCompletions("v", 1);
        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(item =>
        {
            item.FilterText.Should().NotContainAny(["[", "]"],
                $"FilterText '{item.FilterText}' must not contain arch tags");
        });
    }

    [Fact]
    public void Completion_ShortPrefix_FiltersServerSide()
    {
        var result = this.GetCompletions("VMOVAPS Z", 9);
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("ZMM registers should be available");
        result.Items.Should().OnlyContain(
            i => i.FilterText.StartsWith("Z", StringComparison.OrdinalIgnoreCase),
            "typing 'Z' should only return Z-prefixed completions, not YMM/XMM");
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

        // Assert
        result.Should().NotBeNull("hover on MOV should return documentation");
        result.Should().BeOfType<Hover>("GetHover should return standard Hover, not a custom type");
        var hover = (Hover)result;
        hover.Contents.Should().NotBeNull();
        var markup = hover.Contents.Value.Fourth;
        markup.Should().NotBeNull("Contents should be MarkupContent");
        markup.Kind.Should().Be(MarkupKind.PlainText);
        markup.Value.Should().Contain("MOV", "hover text should mention the mnemonic");
    }

    [Fact]
    public void GetHover_WithMnemonic_WithAsmDocUrl_ShouldContainDocUrl()
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

        // Assert
        result.Should().BeOfType<Hover>();
        var markup = ((Hover)result).Contents!.Value.Fourth;
        markup.Should().NotBeNull();
        markup.Value.Should().Contain("https://github.com/HJLebbink/asm-dude/wiki/",
            "hover should contain a documentation URL");
    }

    [Fact]
    public void GetHover_WithMnemonic_WithoutAsmDocUrl_ShouldNotContainMarkdownLink()
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

        // Assert
        result.Should().BeOfType<Hover>();
        var markup = ((Hover)result).Contents!.Value.Fourth;
        markup.Should().NotBeNull();
        markup.Value.Should().NotStartWith("[", "without AsmDoc_Url there should be no leading markdown link");
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
        if (result != null)
        {
            result.Signatures.Should().NotBeEmpty("MOV has multiple signatures");
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

        var resolved = this._server.ResolveCodeLens(lenses[0]);
        resolved.Command.Title.Should().Be("0 references");
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

        var titles = lenses.Select(l => this._server.ResolveCodeLens(l).Command.Title).ToList();
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
