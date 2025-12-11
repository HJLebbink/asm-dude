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
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Tests for Document Highlights feature
/// </summary>
public class DocumentHighlightTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly DocumentHighlightProvider _provider;

    public DocumentHighlightTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new DocumentHighlightProvider(_loggerMock.Object);
    }

    [Fact]
    public void ProvideDocumentHighlights_SimpleWord_FindsAllOccurrences()
    {
        // Arrange
        var lines = new[]
        {
            "mov myvar, 10",
            "add myvar, 5",
            "mov eax, myvar"
        };

        // Act - click on "myvar" in first line (position 4)
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 4);

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(3, "myvar appears 3 times");

        // Verify each highlight
        result[0].Range.StartLine.Should().Be(0);
        result[0].Range.StartChar.Should().Be(4);
        result[0].Range.EndChar.Should().Be(9); // "myvar" length = 5

        result[1].Range.StartLine.Should().Be(1);
        result[1].Range.StartChar.Should().Be(4);

        result[2].Range.StartLine.Should().Be(2);
        result[2].Range.StartChar.Should().Be(9);
    }

    [Fact]
    public void ProvideDocumentHighlights_Register_FindsRelatedRegisters()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, 10",
            "mov eax, 5",
            "mov ax, 3",
            "mov ah, 1",
            "mov al, 2"
        };

        // Act - click on "rax" in first line
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 4);

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterOrEqualTo(5, "should find all related registers");
    }

    [Fact]
    public void ProvideDocumentHighlights_NoMatches_ReturnsNull()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, 10",
            "mov rbx, 5"
        };

        // Act - click on "rax" but rbx is different
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 4);

        // Assert - should find rax but not rbx (they are different registers)
        result.Should().NotBeNull();
        result!.Count.Should().Be(1, "only rax should be highlighted");
    }

    [Fact]
    public void ProvideDocumentHighlights_ClickOnSeparator_ReturnsNull()
    {
        // Arrange
        var lines = new[] { "mov rax, 10" };

        // Act - click on comma (separator)
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 7);

        // Assert
        result.Should().BeNull("clicking on separator should return null");
    }

    [Fact]
    public void ProvideDocumentHighlights_InvalidLine_ReturnsNull()
    {
        // Arrange
        var lines = new[] { "mov rax, 10" };

        // Act - invalid line number
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 10, character: 0);

        // Assert
        result.Should().BeNull("invalid line number should return null");
    }

    [Fact]
    public void ProvideDocumentHighlights_CaseInsensitive_FindsMatches()
    {
        // Arrange
        var lines = new[]
        {
            "MOV MyVar, 10",
            "add myvar, 5",
            "MOV eax, MYVAR"
        };

        // Act - click on "MyVar" in first line
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 4);

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(3, "should find case-insensitive matches");
    }

    [Fact]
    public void ProvideDocumentHighlights_PartialMatch_DoesNotHighlight()
    {
        // Arrange
        var lines = new[]
        {
            "mov var, 10",
            "mov myvar, 5"
        };

        // Act - click on "var" in first line
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 0, character: 4);

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(1, "should not match 'myvar' when searching for 'var'");
        result[0].Range.StartChar.Should().Be(4);
        result[0].Range.EndChar.Should().Be(7); // "var" length = 3
    }

    [Fact]
    public void ProvideDocumentHighlights_MultipleLines_FindsAll()
    {
        // Arrange
        var lines = new[]
        {
            "section .data",
            "    myvar db 0",
            "section .text",
            "    mov al, myvar",
            "    inc myvar",
            "    cmp myvar, 10",
            "    jne myvar"
        };

        // Act - click on "myvar" in line 1
        var result = _provider.ProvideDocumentHighlights(lines, lineNumber: 1, character: 4);

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(5, "myvar appears 5 times");
    }
}
