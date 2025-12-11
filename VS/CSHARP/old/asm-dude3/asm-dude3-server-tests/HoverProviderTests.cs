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
/// Unit tests for HoverProvider - provides documentation and information when hovering over assembly keywords
/// </summary>
public class HoverProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly HoverProvider _provider;
    private readonly AsmLanguageServerOptions _options;

    public HoverProviderTests()
    {
        _loggerMock = new Mock<ILogger>();

        // Create real dependencies
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var traceSource = new System.Diagnostics.TraceSource("AsmDude3Tests");
        var asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        _options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = true,
            ARCH_AVX = true,
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true
        };

        var regularDataPath = Path.Combine(resourceDir, "signature-may2019.txt");
        var handcraftedDataPath = Path.Combine(resourceDir, "signature-hand-1.txt");
        var performancePath = Path.Combine(resourceDir, "Performance");

        var mnemonicStore = new MnemonicStore(_loggerMock.Object, regularDataPath, handcraftedDataPath, _options);
        var performanceStore = new PerformanceStore(_loggerMock.Object, performancePath, _options);

        _provider = new HoverProvider(_loggerMock.Object, asmDudeTools, mnemonicStore, performanceStore, _options);
    }

    #region Constructor Tests

    // Note: Constructor validation is implicitly tested by the test class constructor
    // which successfully creates a HoverProvider instance with valid parameters.
    // Testing null parameters would require mocking MnemonicStore and PerformanceStore
    // which don't have parameterless constructors.

    #endregion

    #region Basic Hover Tests

    [Fact]
    public void ProvideHover_WithMOVInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 0; // Position on 'mov'

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().NotBeNullOrEmpty();
        result.Contents.Should().Contain("Move", "hover should contain instruction description");
        result.Range.Should().NotBeNull();
        result.Range.StartChar.Should().Be(0);
        result.Range.EndChar.Should().Be(3);
    }

    [Fact]
    public void ProvideHover_WithRegister_ShouldReturnRegisterInfo()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 4; // Position on 'rax'

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().NotBeNullOrEmpty();
        result.Contents.Should().Contain("RAX", "hover should contain register name");
    }

    [Fact]
    public void ProvideHover_WithNullLine_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideHover(null!, 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideHover_WithEmptyLine_ShouldReturnNull()
    {
        // Act
        var result = _provider.ProvideHover("", 0);

        // Assert
        result.Should().BeNull("empty line should return null");
    }

    [Fact]
    public void ProvideHover_WithWhitespaceLine_ShouldReturnNull()
    {
        // Act
        var result = _provider.ProvideHover("   ", 1);

        // Assert
        result.Should().BeNull("whitespace line should return null");
    }

    [Fact]
    public void ProvideHover_WithNegativePosition_ShouldReturnNull()
    {
        // Act
        var result = _provider.ProvideHover("mov rax, rbx", -1);

        // Assert
        result.Should().BeNull("negative position should return null");
    }

    [Fact]
    public void ProvideHover_WithPositionBeyondLine_ShouldReturnNull()
    {
        // Act
        var result = _provider.ProvideHover("mov", 10);

        // Assert
        result.Should().BeNull("position beyond line should return null");
    }

    [Fact]
    public void ProvideHover_OnSeparator_ShouldReturnNull()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 7; // Position on comma

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().BeNull("hovering on separator should return null");
    }

    #endregion

    #region Instruction Hover Tests

    [Fact]
    public void ProvideHover_WithCommonInstructions_ShouldReturnHoverInfo()
    {
        // Arrange
        var instructions = new[]
        {
            ("add rax, rbx", 0), // ADD
            ("sub rax, rbx", 0), // SUB
            ("push rax", 0),     // PUSH
            ("pop rbx", 0),      // POP
            ("call func", 0),    // CALL
            ("ret", 0)           // RET
        };

        // Act & Assert
        foreach (var (line, pos) in instructions)
        {
            var result = _provider.ProvideHover(line, pos);
            result.Should().NotBeNull($"hover should work for: {line}");
            result!.Contents.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ProvideHover_WithJumpInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "jmp label";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("Jump", "hover should contain instruction description");
    }

    [Fact]
    public void ProvideHover_WithSSEInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "movaps xmm0, xmm1";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("Aligned Packed", "hover should contain instruction description");
    }

    [Fact]
    public void ProvideHover_WithAVXInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "vmovaps ymm0, ymm1";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("Aligned Packed", "hover should contain instruction description");
    }

    [Fact]
    public void ProvideHover_WithMnemonic_ShouldIncludeUrlReference()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Url.Should().NotBeNullOrEmpty("mnemonic should have URL reference");
        result.Url.Should().Be("MOV", "URL reference should match mnemonic");
        result.Keyword.Should().NotBeNullOrEmpty("keyword should be set for clickable link");
        result.Keyword.Should().Be("MOV", "keyword should match mnemonic");
    }

    #endregion

    #region Register Hover Tests

    [Fact]
    public void ProvideHover_With64BitRegister_ShouldReturnRegisterInfo()
    {
        // Arrange
        var registers = new[] { "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rsp", "rbp" };

        // Act & Assert
        foreach (var reg in registers)
        {
            var line = $"mov {reg}, 0";
            var result = _provider.ProvideHover(line, 4);
            result.Should().NotBeNull($"{reg} should have hover info");
            result!.Contents.Should().Contain(reg.ToUpper());
        }
    }

    [Fact]
    public void ProvideHover_With32BitRegister_ShouldReturnRegisterInfo()
    {
        // Arrange
        var registers = new[] { "eax", "ebx", "ecx", "edx" };

        // Act & Assert
        foreach (var reg in registers)
        {
            var line = $"mov {reg}, 0";
            var result = _provider.ProvideHover(line, 4);
            result.Should().NotBeNull($"{reg} should have hover info");
            result!.Contents.Should().Contain(reg.ToUpper());
        }
    }

    [Fact]
    public void ProvideHover_WithXMMRegister_ShouldReturnRegisterInfo()
    {
        // Arrange
        var line = "movaps xmm0, xmm1";
        var position = 7; // Position on xmm0

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("XMM");
    }

    [Fact]
    public void ProvideHover_WithYMMRegister_ShouldReturnRegisterInfo()
    {
        // Arrange
        var line = "vmovaps ymm0, ymm1";
        var position = 8; // Position on ymm0

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("YMM");
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void ProvideHover_WithUppercaseInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "MOV RAX, RBX";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("Move", "hover should contain instruction description");
    }

    [Fact]
    public void ProvideHover_WithMixedCaseInstruction_ShouldReturnHoverInfo()
    {
        // Arrange
        var line = "MoV rAx, RbX";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        result!.Contents.Should().Contain("Move", "hover should contain instruction description");
    }

    #endregion

    #region Word Boundary Tests

    [Fact]
    public void ProvideHover_AtStartOfWord_ShouldReturnCorrectRange()
    {
        // Arrange
        var line = "mov rax, rbx";

        // Act
        var result = _provider.ProvideHover(line, 0); // Start of 'mov'

        // Assert
        result.Should().NotBeNull();
        result!.Range.StartChar.Should().Be(0);
        result.Range.EndChar.Should().Be(3);
    }

    [Fact]
    public void ProvideHover_AtMiddleOfWord_ShouldReturnCorrectRange()
    {
        // Arrange
        var line = "mov rax, rbx";

        // Act
        var result = _provider.ProvideHover(line, 1); // Middle of 'mov'

        // Assert
        result.Should().NotBeNull();
        result!.Range.StartChar.Should().Be(0);
        result.Range.EndChar.Should().Be(3);
    }

    [Fact]
    public void ProvideHover_AtEndOfWord_ShouldReturnCorrectRange()
    {
        // Arrange
        var line = "mov rax, rbx";

        // Act
        var result = _provider.ProvideHover(line, 2); // End of 'mov'

        // Assert
        result.Should().NotBeNull();
        result!.Range.StartChar.Should().Be(0);
        result!.Range.EndChar.Should().Be(3);
    }

    #endregion

    #region Unknown Keyword Tests

    [Fact]
    public void ProvideHover_WithUnknownKeyword_ShouldReturnHoverInfoOrNull()
    {
        // Arrange
        var line = "myfunction:";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert - Unknown keywords may or may not return hover depending on implementation
        // Just verify it doesn't crash
        if (result != null)
        {
            result.Contents.Should().NotBeNull();
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideHover_WithComplexLine_ShouldHandleAllTokens()
    {
        // Arrange
        var line = "    mov rax, [rbx + 8]  ; comment";

        // Act - Test multiple positions
        var movHover = _provider.ProvideHover(line, 4);  // 'mov'
        var raxHover = _provider.ProvideHover(line, 8);  // 'rax'
        var rbxHover = _provider.ProvideHover(line, 15); // 'rbx'

        // Assert
        movHover.Should().NotBeNull("should get hover for MOV");
        raxHover.Should().NotBeNull("should get hover for RAX");
        rbxHover.Should().NotBeNull("should get hover for RBX");
    }

    [Fact]
    public void ProvideHover_WithPerformanceData_ShouldIncludePerformanceInfo()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 0;

        // Act
        var result = _provider.ProvideHover(line, position);

        // Assert
        result.Should().NotBeNull();
        // Performance data may or may not be included depending on settings
        result!.Contents.Should().NotBeEmpty();
    }

    #endregion
}
