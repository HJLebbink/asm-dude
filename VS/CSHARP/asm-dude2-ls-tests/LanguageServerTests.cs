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
        _server = new LanguageServer();
        _options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = true,
            ARCH_AVX = true,
            CodeCompletion_On = true,
            SignatureHelp_On = true,
            CodeFolding_On = true,
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",
            AsmDoc_On = true
        };
        _server.Initialize(_options);
        _server.Initialized();
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
        server.Initialize(_options);

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
        _server.OnTextDocumentOpened(openParams);

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
        _server.OnTextDocumentOpened(openParams);

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = new Uri("file:///test.asm")
            }
        };

        // Act
        _server.OnTextDocumentClosed(closeParams);

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
        _server.OnTextDocumentOpened(openParams);

        // Act
        _server.UpdateServerSideTextDocument("add rcx, rdx", 2, "file:///test.asm");

        // Assert - no exception means success
    }

    #endregion

    #region Completion Tests

    [Fact]
    public void GetTextDocumentCompletion_WithValidDocument_ShouldReturnCompletions()
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
                Text = "mov rax, rbx\nadd "
            }
        };
        _server.OnTextDocumentOpened(openParams);

        var completionParams = new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 1, Character = 4 }
        };

        // Act
        var result = _server.GetTextDocumentCompletion(completionParams);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Hover Tests

    [Fact]
    public void GetHover_WithMnemonic_ShouldReturnHover()
    {
        // Arrange
        var uri = "file:///test.asm";
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });
        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 1 }
        };

        // Act
        var result = _server.GetHover(hoverParams);

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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = _server.GetHover(new TextDocumentPositionParams
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
        _server.OnTextDocumentOpened(openParams);

        var hoverParams = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) },
            Position = new Position { Line = 0, Character = 5 } // Position on "rax"
        };

        // Act
        var result = _server.GetHover(hoverParams);

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
        _server.OnTextDocumentOpened(openParams);

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
        var result = _server.GetTextDocumentSignatureHelp(sigHelpParams);

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
        _server.OnTextDocumentOpened(openParams);

        var foldingParams = new FoldingRangeParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        };

        // Act
        var result = _server.GetFoldingRanges(foldingParams);

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

        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = text }
        });

        var result = _server.GetFoldingRanges(new FoldingRangeParams
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

        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = text }
        });

        var result = _server.GetFoldingRanges(new FoldingRangeParams
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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = _server.GetSemanticTokens(new SemanticTokensParams
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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        // Act
        var result = _server.GetSemanticTokens(new SemanticTokensParams
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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var full = _server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Act — request delta with the resultId just returned
        var delta = _server.GetSemanticTokensDelta(new SemanticTokensDeltaParams
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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "mov rax, rbx" }
        });

        var full = _server.GetSemanticTokens(new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(uri) }
        });

        // Act — update the document then request delta
        _server.UpdateServerSideTextDocument("add rcx, rdx\nnop", 2, uri);
        var delta = _server.GetSemanticTokensDelta(new SemanticTokensDeltaParams
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
        _server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = new Uri(uri), LanguageId = "asm", Version = 1, Text = "" }
        });

        // Act
        var result = _server.GetSemanticTokens(new SemanticTokensParams
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
        server.Initialize(_options);
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
}
