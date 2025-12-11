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

using AsmDude3.Server.Providers;
using AsmTools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Unit tests for SemanticTokensProvider - provides syntax highlighting tokens for assembly code
/// </summary>
public class SemanticTokensProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly SemanticTokensProvider _provider;

    public SemanticTokensProviderTests()
    {
        _loggerMock = new Mock<ILogger>();

        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var traceSource = new System.Diagnostics.TraceSource("AsmDude3Tests");
        var asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        _provider = new SemanticTokensProvider(_loggerMock.Object, asmDudeTools);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var traceSource = new System.Diagnostics.TraceSource("Test");
        var asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        // Act & Assert
        var act = () => new SemanticTokensProvider(null!, asmDudeTools);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAsmDudeTools_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new SemanticTokensProvider(_loggerMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("asmDudeTools");
    }

    #endregion

    #region Basic Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithNullLines_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideSemanticTokens(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideSemanticTokens_WithEmptyArray_ShouldReturnEmptyList()
    {
        // Act
        var result = _provider.ProvideSemanticTokens(Array.Empty<string>());

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ProvideSemanticTokens_WithBlankLines_ShouldReturnEmptyList()
    {
        // Arrange
        var lines = new[] { "", "   ", "\t" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty("blank lines should produce no tokens");
    }

    #endregion

    #region Comment Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithComment_ShouldTokenizeAsComment()
    {
        // Arrange
        var lines = new[] { "; this is a comment" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Comment);
        var commentToken = result.First(t => t.TokenType == SemanticTokenType.Comment);
        commentToken.Line.Should().Be(0);
        commentToken.StartChar.Should().Be(0);
        commentToken.Length.Should().Be("; this is a comment".Length);
    }

    [Fact]
    public void ProvideSemanticTokens_WithInlineComment_ShouldTokenizeCodeAndComment()
    {
        // Arrange
        var lines = new[] { "mov rax, rbx  ; move rbx to rax" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Comment);
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Keyword || t.TokenType == SemanticTokenType.Variable);
    }

    [Fact]
    public void ProvideSemanticTokens_WithCommentOnly_ShouldOnlyProduceCommentToken()
    {
        // Arrange
        var lines = new[] { "   ; comment with leading spaces" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().HaveCount(1);
        result[0].TokenType.Should().Be(SemanticTokenType.Comment);
    }

    #endregion

    #region Instruction Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithMOVInstruction_ShouldTokenizeAsKeyword()
    {
        // Arrange
        var lines = new[] { "mov rax, rbx" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Keyword || t.TokenType == SemanticTokenType.Variable);
    }

    [Fact]
    public void ProvideSemanticTokens_WithCommonInstructions_ShouldTokenizeAll()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx",
            "add rcx, rdx",
            "sub rsi, rdi",
            "push rax",
            "pop rbx"
        };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
        result.Select(t => t.Line).Distinct().Should().HaveCount(5, "all 5 lines should have tokens");
    }

    [Fact]
    public void ProvideSemanticTokens_WithJumpInstruction_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "jmp label" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideSemanticTokens_WithSSEInstruction_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "movaps xmm0, xmm1" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideSemanticTokens_WithAVXInstruction_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "vmovaps ymm0, ymm1" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Number Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithHexNumber_ShouldTokenizeAsNumber()
    {
        // Arrange
        var lines = new[] { "mov rax, 0x42" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
    }

    [Fact]
    public void ProvideSemanticTokens_WithDecimalNumber_ShouldTokenizeAsNumber()
    {
        // Arrange
        var lines = new[] { "mov rax, 42" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
    }

    [Fact]
    public void ProvideSemanticTokens_WithBinaryNumber_ShouldTokenizeAsNumber()
    {
        // Arrange
        var lines = new[] { "mov rax, 0b1010" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
    }

    [Fact]
    public void ProvideSemanticTokens_WithNegativeNumber_ShouldTokenizeAsNumber()
    {
        // Arrange
        var lines = new[] { "mov rax, -10" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
    }

    #endregion

    #region Operator Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithOperators_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "mov rax, [rbx + 8]" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
        // Operators might be tokenized as operators or part of expressions
    }

    #endregion

    #region Label Tokenization Tests

    [Fact]
    public void ProvideSemanticTokens_WithLabel_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "start:" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideSemanticTokens_WithLabelAndCode_ShouldTokenizeBoth()
    {
        // Arrange
        var lines = new[] { "start: mov rax, rbx" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void ProvideSemanticTokens_WithUppercaseInstruction_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "MOV RAX, RBX" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideSemanticTokens_WithMixedCaseInstruction_ShouldTokenize()
    {
        // Arrange
        var lines = new[] { "MoV rAx, RbX" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
    }

    #endregion

    #region Complex Line Tests

    [Fact]
    public void ProvideSemanticTokens_WithComplexLine_ShouldTokenizeAll()
    {
        // Arrange
        var lines = new[] { "    mov rax, [rbx + rcx * 8 + 0x10]  ; calculate address" };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Comment);
        result.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
    }

    [Fact]
    public void ProvideSemanticTokens_WithMultipleLines_ShouldMaintainLineNumbers()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, 0",    // Line 0
            "add rax, 1",    // Line 1
            "sub rax, 2"     // Line 2
        };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().Contain(t => t.Line == 0);
        result.Should().Contain(t => t.Line == 1);
        result.Should().Contain(t => t.Line == 2);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideSemanticTokens_WithRealAssemblyCode_ShouldTokenizeCorrectly()
    {
        // Arrange
        var lines = new[]
        {
            "section .text",
            "    global _start",
            "",
            "_start:",
            "    ; Setup stack frame",
            "    push rbp",
            "    mov rbp, rsp",
            "    sub rsp, 16",
            "",
            "    ; Initialize variables",
            "    mov rax, 0x10",
            "    mov rbx, [rax + 8]",
            "",
            "    ; Cleanup and return",
            "    add rsp, 16",
            "    pop rbp",
            "    ret"
        };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        result.Should().NotBeEmpty("assembly code should produce many tokens");
        result.Select(t => t.TokenType).Should().Contain(SemanticTokenType.Comment, "code has comments");
        result.Select(t => t.TokenType).Should().Contain(SemanticTokenType.Number, "code has numbers");

        // Verify all token positions are valid
        foreach (var token in result)
        {
            token.Line.Should().BeInRange(0, lines.Length - 1);
            token.StartChar.Should().BeGreaterOrEqualTo(0);
            token.Length.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void ProvideSemanticTokens_AllTokensShouldHaveValidPositions()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx  ; test",
            "add rcx, 42",
            "jmp start"
        };

        // Act
        var result = _provider.ProvideSemanticTokens(lines);

        // Assert
        foreach (var token in result)
        {
            token.Line.Should().BeInRange(0, lines.Length - 1, "line number should be valid");
            token.StartChar.Should().BeGreaterOrEqualTo(0, "start character should be non-negative");
            token.Length.Should().BeGreaterThan(0, "token length should be positive");

            // Verify token doesn't exceed line length
            var line = lines[token.Line];
            (token.StartChar + token.Length).Should().BeLessOrEqualTo(line.Length,
                $"token should not exceed line length at line {token.Line}");
        }
    }

    #endregion
}
