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

using FluentAssertions;

using Xunit;

namespace AsmDude2LS.Tests;

/// <summary>
/// Process-based integration tests that start the LSP server as a real process
/// and communicate via JSON-RPC over stdin/stdout.
///
/// These tests avoid the Roslyn internal type serialization issues by using
/// System.Text.Json.JsonNode for responses, making them more robust.
///
/// Note: These tests require the server project to be built first.
/// Run: dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj
/// </summary>
[Collection("ProcessTests")] // Ensures tests run serially to avoid port conflicts
public class LspProcessIntegrationTests : IAsyncLifetime
{
    private LspProcessTestClient? _client;

    public async ValueTask InitializeAsync()
    {
        try
        {
            this._client = await LspProcessTestClient.StartAsync();
        }
        catch (Exception ex)
        {
            // If server fails to start, tests will be skipped
            throw new SkipException($"Failed to start LSP server: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this._client != null)
        {
            await this._client.DisposeAsync();
        }
    }

    #region Initialize / Lifecycle Tests

    [Fact]
    public async Task Initialize_ShouldReturnCapabilities()
    {
        // Act
        var result = await this._client!.InitializeAsync();

        // Assert
        result.Should().NotBeNull();
        var capabilities = result!["capabilities"];
        capabilities.Should().NotBeNull("server should return capabilities");

        // Verify key capabilities
        capabilities!["textDocumentSync"].Should().NotBeNull();
        capabilities["completionProvider"].Should().NotBeNull();
        capabilities["hoverProvider"].Should().NotBeNull();
        capabilities["signatureHelpProvider"].Should().NotBeNull();
        capabilities["foldingRangeProvider"].Should().NotBeNull();
        capabilities["semanticTokensProvider"].Should().NotBeNull();
    }

    [Fact]
    public async Task Shutdown_ShouldComplete()
    {
        // Arrange
        await this._client!.InitializeAsync();

        // Act
        await this._client.ShutdownAsync();

        // Assert - If we get here without exception, shutdown succeeded
    }

    #endregion

    #region Hover Tests

    [Fact]
    public async Task Hover_OnMnemonic_ShouldReturnDescription()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");

        // Act
        var result = await this._client.HoverAsync("file:///test.asm", line: 0, character: 1);

        // Assert. This client advertises no hover contentFormat (empty capabilities), which the server
        // treats like Visual Studio → PlainText, so it returns a VS-style rich hover under
        // `_vs_rawContent` (monospace classified text) rather than standard `contents`. Either way the
        // mnemonic description must be present in the payload.
        result.Should().NotBeNull("hover on MOV should return documentation");
        result!["_vs_rawContent"].Should().NotBeNull(
            "a plaintext client (like VS) gets the monospace _vs_rawContent hover");
        result.ToJsonString().Should().Contain("MOV", "the hover payload should mention the mnemonic");
    }

    [Fact]
    public async Task Hover_OnRegister_ShouldReturnDescription()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");

        // Act
        var result = await this._client.HoverAsync("file:///test.asm", line: 0, character: 5);

        // Assert
        result.Should().NotBeNull("hover on RAX should return register documentation");
    }

    [Fact]
    public async Task DocumentHighlight_OnRegister_ReturnsFamilyInResponse_WithPartialResultToken()
    {
        // Guards the WHOLE register-highlight path over real JSON-RPC: the word-boundary fix (caret at the
        // END of "al", on the ','), the register-family expansion (AL -> RAX), AND the delivery path — VS
        // always sends a partialResultToken, and the ranges must come back IN THE RESPONSE (the old
        // "$/progress + return null" path computed the family but rendered nothing).
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test_hl.asm", "mov rax, rbx\nmov al, cl");

        // line 1 = "mov al, cl"; char 6 is the ',' immediately after "al".
        var result = await this._client.DocumentHighlightAsync("file:///test_hl.asm", line: 1, character: 6);

        result.Should().NotBeNull("documentHighlight must return ranges in the response, not only via $/progress");
        var arr = result!.AsArray();
        arr.Count.Should().BeGreaterThanOrEqualTo(2, "AL and RAX (same register family) should both be highlighted");
        arr.Any(n => (int)n!["range"]!["start"]!["line"]! == 0)
            .Should().BeTrue("RAX on line 0 must be highlighted when AL is selected");
    }

    [Fact]
    public async Task Hover_OnWhitespace_ShouldReturnNull()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");

        // Act
        var result = await this._client.HoverAsync("file:///test.asm", line: 0, character: 3);

        // Assert - whitespace should return null
        result.Should().BeNull("hover on whitespace should return null");
    }

    #endregion

    #region Completion Tests

    [Fact]
    public async Task Completion_OnPartialMnemonic_ShouldReturnSuggestions()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mo");

        // Act
        var result = await this._client.CompletionAsync("file:///test.asm", line: 0, character: 2);

        // Assert
        result.Should().NotBeNull();

        // Result could be CompletionList or CompletionItem[]
        var items = result!["items"];
        if (items != null)
        {
            items.AsArray().Should().NotBeEmpty("typing 'mo' should suggest MOV and other mnemonics");
        }
        else
        {
            // Direct array of completion items
            result.AsArray().Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Completion_AfterMnemonic_ShouldReturnOperandSuggestions()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov ");

        // Act
        var result = await this._client.CompletionAsync("file:///test.asm", line: 0, character: 4);

        // Assert
        result.Should().NotBeNull("should get operand completion suggestions after mnemonic");
    }

    #endregion

    #region Inlay Hints Tests

    [Fact]
    public async Task InlayHints_OnDocument_ShouldReturnHints()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, 0x10\nadd rbx, 255");

        // Act
        var result = await this._client.InlayHintsAsync("file:///test.asm", 0, 0, 2, 20);

        // Assert
        result.Should().NotBeNull();
        // Inlay hints should show hex/decimal conversions for constants
        result.AsArray().Should().NotBeEmpty("document with constants should have inlay hints");
    }

    [Fact]
    public async Task InlayHints_EmptyDocument_ShouldReturnEmpty()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "");

        // Act
        var result = await this._client.InlayHintsAsync("file:///test.asm", 0, 0, 1, 0);

        // Assert
        result.Should().NotBeNull();
        result.AsArray().Should().BeEmpty("empty document should have no inlay hints");
    }

    #endregion

    #region Semantic Tokens Tests

    [Fact]
    public async Task SemanticTokens_WithCode_ShouldReturnTokenData()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");

        // Act
        var result = await this._client.SemanticTokensAsync("file:///test.asm");

        // Assert
        result.Should().NotBeNull();
        var data = result!["data"];
        data.Should().NotBeNull();
        data!.AsArray().Should().NotBeEmpty("document with code should have semantic tokens");

        // Data should be divisible by 5 (deltaLine, deltaStart, length, tokenType, modifiers)
        (data.AsArray().Count % 5).Should().Be(0);
    }

    [Fact]
    public async Task SemanticTokens_EmptyDocument_ShouldReturnEmptyData()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "");

        // Act
        var result = await this._client.SemanticTokensAsync("file:///test.asm");

        // Assert
        result.Should().NotBeNull();
        var data = result!["data"];
        data.Should().NotBeNull();
        data!.AsArray().Should().BeEmpty("empty document should have no semantic tokens");
    }

    [Fact]
    public async Task SemanticTokens_WithLabels_ShouldReturnLabelTokens()
    {
        // Arrange
        await this._client!.InitializeAsync();
        var code = "loop_start:\n    mov rax, rbx\n    jmp loop_start";
        await this._client.OpenDocumentAsync("file:///test.asm", code);

        // Act
        var result = await this._client.SemanticTokensAsync("file:///test.asm");

        // Assert
        result.Should().NotBeNull();
        var data = result!["data"];
        data.Should().NotBeNull();
        data!.AsArray().Should().NotBeEmpty("document with labels should have semantic tokens");
        // Should have multiple tokens: label def, mnemonic, registers, jump, label ref
        data.AsArray().Count.Should().BeGreaterThan(10);
    }

    #endregion

    #region Signature Help Tests

    [Fact]
    public async Task SignatureHelp_AfterMnemonic_ShouldReturnSignatures()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov ");

        // Act
        var result = await this._client.SignatureHelpAsync("file:///test.asm", line: 0, character: 4);

        // Assert
        result.Should().NotBeNull("MOV should have signature help");
        var signatures = result!["signatures"];
        signatures.Should().NotBeNull();
        signatures!.AsArray().Should().NotBeEmpty("MOV has multiple signatures");
    }

    #endregion

    #region Definition Tests

    [Fact]
    public async Task Definition_OnLabelReference_ShouldReturnDefinition()
    {
        // Arrange
        await this._client!.InitializeAsync();
        var code = "my_label:\n    mov rax, rbx\n    jmp my_label";
        await this._client.OpenDocumentAsync("file:///test.asm", code);

        // Act
        var result = await this._client.DefinitionAsync("file:///test.asm", line: 2, character: 8);

        // Assert
        result.Should().NotBeNull("clicking on label reference should go to definition");
    }

    [Fact]
    public async Task Definition_OnMnemonic_ShouldReturnNull()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");

        // Act
        var result = await this._client.DefinitionAsync("file:///test.asm", line: 0, character: 1);

        // Assert - mnemonics don't have definitions
        result.Should().BeNull("mnemonics don't have definitions");
    }

    #endregion

    #region References Tests

    [Fact]
    public async Task References_OnLabel_ShouldFindAllOccurrences()
    {
        // Arrange
        await this._client!.InitializeAsync();
        var code = "my_label:\n    mov rax, rbx\n    jmp my_label\n    call my_label";
        await this._client.OpenDocumentAsync("file:///test.asm", code);

        // Act
        var result = await this._client.ReferencesAsync("file:///test.asm", line: 0, character: 2);

        // Assert
        result.Should().NotBeNull("label should have references");
        result!.AsArray().Should().NotBeEmpty();
        // Should find definition + 2 references
        result.AsArray().Count.Should().BeGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Folding Range Tests

    [Fact]
    public async Task FoldingRanges_WithRegions_ShouldReturnRanges()
    {
        // Arrange
        await this._client!.InitializeAsync();
        var code = "#region Test\nmov rax, rbx\nadd rcx, rdx\n#endregion";
        await this._client.OpenDocumentAsync("file:///test.asm", code);

        // Act
        var result = await this._client.FoldingRangesAsync("file:///test.asm");

        // Assert
        result.Should().NotBeNull();
        result!.AsArray().Should().NotBeEmpty("document with regions should have folding ranges");
    }

    [Fact]
    public async Task FoldingRanges_WithoutRegions_ShouldReturnEmpty()
    {
        // Arrange
        await this._client!.InitializeAsync();
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx\nadd rcx, rdx");

        // Act
        var result = await this._client.FoldingRangesAsync("file:///test.asm");

        // Assert
        result.Should().NotBeNull();
        result!.AsArray().Should().BeEmpty("document without regions should have no folding ranges");
    }

    #endregion

    #region Document Lifecycle Tests

    [Fact]
    public async Task DocumentLifecycle_OpenCloseOpen_ShouldWork()
    {
        // Arrange
        await this._client!.InitializeAsync();

        // Act - Open
        await this._client.OpenDocumentAsync("file:///test.asm", "mov rax, rbx");
        var hover1 = await this._client.HoverAsync("file:///test.asm", 0, 1);
        hover1.Should().NotBeNull("first open should work");

        // Act - Close
        await this._client.CloseDocumentAsync("file:///test.asm");

        // After close, hover should return null (document not found)
        var hover2 = await this._client.HoverAsync("file:///test.asm", 0, 1);
        hover2.Should().BeNull("closed document should not have hover");

        // Act - Reopen
        await this._client.OpenDocumentAsync("file:///test.asm", "add rbx, rcx");
        var hover3 = await this._client.HoverAsync("file:///test.asm", 0, 1);
        hover3.Should().NotBeNull("reopened document should have hover");
    }

    #endregion
}

/// <summary>
/// Custom exception for skipping tests when server cannot be started.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
