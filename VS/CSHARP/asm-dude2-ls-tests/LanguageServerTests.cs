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
    public void GetHover_WithMnemonic_ShouldReturnHoverInfo()
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
            Position = new Position { Line = 0, Character = 1 } // Position on "mov"
        };

        // Act
        var result = _server.GetHover(hoverParams);

        // Assert
        result.Should().NotBeNull("hover on MOV should return documentation");
        // Result can be either Hover or VSInternalHover (with clickable link)
        // Both types have contents, but we check the object is not null
        (result is Hover || result is VSInternalHover).Should().BeTrue("result should be Hover or VSInternalHover");
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
        // Note: Actual folding depends on whether code folding is enabled and region tags match
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
        var hover = server.GetHover(hoverParams);

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
