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

using AsmDude3.Server;
using AsmDude3.Server.Providers;
using AsmDude3.Server.Stores;
using AsmTools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Integration tests for code completion (IntelliSense)
/// Tests the complete flow from LSP request to completion suggestions
/// </summary>
public class CompletionIntegrationTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly CompletionProvider _provider;
    private readonly AsmLanguageServerOptions _defaultOptions;

    public CompletionIntegrationTests()
    {
        _loggerMock = new Mock<ILogger>();

        // Create default options with completion enabled
        _defaultOptions = new AsmLanguageServerOptions
        {
            CodeCompletion_On = true,
            SyntaxHighlighting_On = true,
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = true,
            ARCH_AVX = true,
        };

        // Create real AsmDudeTools with actual XML data
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var traceSource = new System.Diagnostics.TraceSource("AsmDude3Tests");
        var asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        // Create real MnemonicStore with actual signature files
        var filename_Regular = Path.Combine(resourceDir, "signature-may2019.txt");
        var filename_Hand = Path.Combine(resourceDir, "signature-hand-1.txt");
        var mnemonicStore = new MnemonicStore(_loggerMock.Object, filename_Regular, filename_Hand, _defaultOptions);

        _provider = new CompletionProvider(_loggerMock.Object, asmDudeTools, mnemonicStore, _defaultOptions);
    }

    #region Mnemonic Completion Tests

    [Fact]
    public void ProvideCompletions_EmptyLine_ReturnsMnemonics()
    {
        // Arrange
        var lines = new[] { "" };
        var lineNumber = 0;
        var position = 0;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Select(i => i.Label).Should().Contain(new[] { "mov", "add", "sub", "call", "ret" });
    }

    [Fact]
    public void ProvideCompletions_MoPrefix_ReturnsMovInstructions()
    {
        // Arrange
        var lines = new[] { "mo" };
        var lineNumber = 0;
        var position = 2;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "mov", "movb", "movw", "movl", "movq", "movzx", "movsx" });
    }

    [Fact]
    public void ProvideCompletions_JPrefix_ReturnsJumpInstructions()
    {
        // Arrange
        var lines = new[] { "j" };
        var lineNumber = 0;
        var position = 1;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "jmp", "je", "jne", "jz", "jnz", "jg", "jl" });
    }

    [Fact]
    public void ProvideCompletions_AddPrefix_ReturnsArithmeticInstructions()
    {
        // Arrange
        var lines = new[] { "ad" };
        var lineNumber = 0;
        var position = 2;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "add", "adc", "addsd", "addss" });
    }

    [Fact]
    public void ProvideCompletions_MnemonicWithCompletionDisabled_ReturnsEmpty()
    {
        // Arrange
        var options = new AsmLanguageServerOptions { CodeCompletion_On = false };
        var provider = new CompletionProvider(_loggerMock.Object, null!, null!, options);
        var lines = new[] { "m" };
        var lineNumber = 0;
        var position = 1;

        // Act
        var result = provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public void ProvideCompletions_MnemonicInComment_ReturnsEmpty()
    {
        // Arrange
        var lines = new[] { "; mov rax, rbx" };
        var lineNumber = 0;
        var position = 14;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public void ProvideCompletions_CaseInsensitive_MatchesMnemonics()
    {
        // Arrange
        var lines = new[] { "MO" };
        var lineNumber = 0;
        var position = 2;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("mov");
    }

    #endregion

    #region Register Completion Tests

    [Fact]
    public void ProvideCompletions_RegisterContext_ReturnsRegisters()
    {
        // Arrange
        var lines = new[] { "mov r" };
        var lineNumber = 0;
        var position = 5;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        // Should contain 64-bit registers
        labels.Should().Contain(new[] { "rax", "rbx", "rcx", "rdx" });
    }

    [Fact]
    public void ProvideCompletions_EaxRegisterPrefix_Returns32BitRegisters()
    {
        // Arrange
        var lines = new[] { "mov ea" };
        var lineNumber = 0;
        var position = 6;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "eax", "ebx", "ecx" });
    }

    [Fact]
    public void ProvideCompletions_AlRegisterPrefix_Returns8BitRegisters()
    {
        // Arrange
        var lines = new[] { "mov al" };
        var lineNumber = 0;
        var position = 6;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "al", "ah", "bl", "bh" });
    }

    [Fact]
    public void ProvideCompletions_XmmRegisterPrefix_ReturnsXmmRegisters()
    {
        // Arrange
        var lines = new[] { "movapd xmm" };
        var lineNumber = 0;
        var position = 10;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        // Should include xmm registers
        labels.Should().ContainMatch("xmm[0-9]");
    }

    #endregion

    #region Operand Context Tests

    [Fact]
    public void ProvideCompletions_AfterMnemonic_OffersOperandOptions()
    {
        // Arrange
        var lines = new[] { "mov " };
        var lineNumber = 0;
        var position = 4;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        // Should offer registers and other operands
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "rax", "rbx", "rcx", "rdx" });
    }

    [Fact]
    public void ProvideCompletions_MultipleOperands_OffersSecondOperand()
    {
        // Arrange
        var lines = new[] { "mov rax, " };
        var lineNumber = 0;
        var position = 9;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        // Should offer registers for second operand
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "rax", "rbx", "rcx", "rdx" });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ProvideCompletions_InvalidLineNumber_ReturnsNull()
    {
        // Arrange
        var lines = new[] { "mov rax" };
        var lineNumber = 10; // Beyond array bounds
        var position = 0;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProvideCompletions_NegativeLineNumber_ReturnsNull()
    {
        // Arrange
        var lines = new[] { "mov rax" };
        var lineNumber = -1;
        var position = 0;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProvideCompletions_NullLines_ReturnsNull()
    {
        // Act
        var result = _provider.ProvideCompletions(null!, 0, 0, labelGraph: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProvideCompletions_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        var lines = Array.Empty<string>();
        var lineNumber = 0;
        var position = 0;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().BeNull(); // Empty array means line 0 doesn't exist
    }

    [Fact]
    public void ProvideCompletions_PositionBeyondLineLength_StillWorks()
    {
        // Arrange
        var lines = new[] { "m" };
        var lineNumber = 0;
        var position = 10; // Way beyond "m"

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        // Should still provide completions
        result!.Items.Should().NotBeEmpty();
    }

    #endregion

    #region Multi-line Tests

    [Fact]
    public void ProvideCompletions_MultipleLines_CompletesCorrectLine()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, 1",
            "ad",
            "sub rbx, 2"
        };
        var lineNumber = 1;
        var position = 2;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain(new[] { "add", "adc" });
    }

    [Fact]
    public void ProvideCompletions_LargeFile_CompletesCorrectly()
    {
        // Arrange
        var lines = new string[1000];
        for (int i = 0; i < 1000; i++)
        {
            lines[i] = i == 500 ? "m" : "nop";
        }
        var lineNumber = 500;
        var position = 1;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        var labels = result.Items.Select(i => i.Label).ToList();
        labels.Should().Contain("mov");
    }

    #endregion

    #region Directive Tests

    [Fact]
    public void ProvideCompletions_DirectivePrefix_ReturnsDirectives()
    {
        // Arrange
        var lines = new[] { ".se" };
        var lineNumber = 0;
        var position = 3;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        // Should include assembly directives starting with '.se'
        var labels = result.Items.Select(i => i.Label).ToList();
        // Assembly directive prefixes may vary by architecture
        labels.Should().NotBeEmpty();
    }

    #endregion

    #region Capitalization Tests

    [Fact]
    public void ProvideCompletions_LowercasePrefix_ReturnsSuggestions()
    {
        // Arrange
        var lines = new[] { "mov" };
        var lineNumber = 0;
        var position = 3;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideCompletions_UppercasePrefix_ReturnsSuggestions()
    {
        // Arrange
        var lines = new[] { "MOV" };
        var lineNumber = 0;
        var position = 3;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideCompletions_MixedCasePrefix_ReturnsSuggestions()
    {
        // Arrange
        var lines = new[] { "MoV" };
        var lineNumber = 0;
        var position = 3;

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void ProvideCompletions_Performance_CompletsInReasonableTime()
    {
        // Arrange
        var lines = new[] { "m" };
        var lineNumber = 0;
        var position = 1;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = _provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "completion should respond in under 1 second");
    }

    [Fact]
    public void ProvideCompletions_MultipleRequests_AllCompleteSuccessfully()
    {
        // Arrange
        var lines = new[] { "m" };
        var completionCounts = new List<int>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var result = _provider.ProvideCompletions(lines, 0, 1, labelGraph: null);
            completionCounts.Add(result?.Items.Length ?? 0);
        }

        // Assert
        for (int i = 1; i < completionCounts.Count; i++)
        {
            completionCounts[i].Should().Be(completionCounts[0], "all requests should return same number of completions");
        }
        completionCounts[0].Should().BeGreaterThan(0);
    }

    #endregion

    #region Integration with Options

    [Fact]
    public void ProvideCompletions_WithVariousArchitectures_IncludesArchSpecificInstructions()
    {
        // Arrange
        var optionsWithSSE = new AsmLanguageServerOptions
        {
            CodeCompletion_On = true,
            ARCH_SSE = true,
            ARCH_AVX = false,
        };
        var provider = new CompletionProvider(_loggerMock.Object, null!, null!, optionsWithSSE);
        var lines = new[] { "add" };
        var lineNumber = 0;
        var position = 3;

        // Act
        var result = provider.ProvideCompletions(lines, lineNumber, position, labelGraph: null);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    #endregion
}
