using AsmDude3.Server;
using AsmDude3.Server.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Tests for semantic tokens provider (syntax highlighting)
/// </summary>
public class SemanticTokensProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly SemanticTokensProvider _provider;

    public SemanticTokensProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new SemanticTokensProvider(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SemanticTokensProvider(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var provider = new SemanticTokensProvider(_loggerMock.Object);

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region Mnemonic Tests

    [Fact]
    public void ProvideTokens_WithMovInstruction_ReturnsKeywordToken()
    {
        // Arrange
        var text = "mov rax, rbx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().NotBeNull();
        tokens.Should().HaveCountGreaterThan(0);

        // First token should be "mov" as keyword
        var firstToken = tokens[0];
        firstToken.Line.Should().Be(0);
        firstToken.StartChar.Should().Be(0);
        firstToken.Length.Should().Be(3);
        firstToken.TokenType.Should().Be(SemanticTokenType.Keyword);
    }

    [Fact]
    public void ProvideTokens_WithMultipleMnemonics_ReturnsAllKeywordTokens()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx",
            "add rcx, rdx",
            "sub rsi, rdi",
            "call my_function"
        };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var keywordTokens = tokens.Where(t => t.TokenType == SemanticTokenType.Keyword).ToList();
        keywordTokens.Should().HaveCount(4);

        // Check each mnemonic
        keywordTokens[0].StartChar.Should().Be(0); // mov
        keywordTokens[1].StartChar.Should().Be(0); // add
        keywordTokens[2].StartChar.Should().Be(0); // sub
        keywordTokens[3].StartChar.Should().Be(0); // call
    }

    [Theory]
    [InlineData("mov")]
    [InlineData("add")]
    [InlineData("sub")]
    [InlineData("push")]
    [InlineData("pop")]
    [InlineData("call")]
    [InlineData("ret")]
    [InlineData("jmp")]
    [InlineData("je")]
    [InlineData("jne")]
    [InlineData("xor")]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("test")]
    [InlineData("cmp")]
    public void ProvideTokens_WithCommonMnemonic_RecognizesAsKeyword(string mnemonic)
    {
        // Arrange
        var text = $"{mnemonic} rax, rbx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var keywordToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Keyword);
        keywordToken.Should().NotBeNull();
        keywordToken!.Length.Should().Be(mnemonic.Length);
    }

    [Fact]
    public void ProvideTokens_WithUppercaseMnemonic_RecognizesAsKeyword()
    {
        // Arrange
        var text = "MOV RAX, RBX";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var keywordToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Keyword);
        keywordToken.Should().NotBeNull();
        keywordToken!.Length.Should().Be(3); // MOV
    }

    [Fact]
    public void ProvideTokens_WithMixedCaseMnemonic_RecognizesAsKeyword()
    {
        // Arrange
        var text = "MoV rAx, RbX";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var keywordToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Keyword);
        keywordToken.Should().NotBeNull();
    }

    #endregion

    #region Register Tests

    [Fact]
    public void ProvideTokens_WithRegisters_ReturnsRegisterTokens()
    {
        // Arrange
        var text = "mov rax, rbx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var registerTokens = tokens.Where(t => t.TokenType == SemanticTokenType.Parameter).ToList();
        registerTokens.Should().HaveCount(2);
        registerTokens[0].Length.Should().Be(3); // rax
        registerTokens[1].Length.Should().Be(3); // rbx
    }

    [Theory]
    [InlineData("rax")]
    [InlineData("rbx")]
    [InlineData("rcx")]
    [InlineData("rdx")]
    [InlineData("rsi")]
    [InlineData("rdi")]
    [InlineData("rsp")]
    [InlineData("rbp")]
    [InlineData("r8")]
    [InlineData("r9")]
    [InlineData("r10")]
    [InlineData("r11")]
    [InlineData("r12")]
    [InlineData("r13")]
    [InlineData("r14")]
    [InlineData("r15")]
    public void ProvideTokens_With64BitRegister_RecognizesAsRegister(string register)
    {
        // Arrange
        var text = $"mov {register}, 0";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var registerToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Parameter);
        registerToken.Should().NotBeNull();
    }

    [Theory]
    [InlineData("eax")]
    [InlineData("ebx")]
    [InlineData("ecx")]
    [InlineData("edx")]
    [InlineData("esi")]
    [InlineData("edi")]
    [InlineData("esp")]
    [InlineData("ebp")]
    public void ProvideTokens_With32BitRegister_RecognizesAsRegister(string register)
    {
        // Arrange
        var text = $"mov {register}, 0";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var registerToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Parameter);
        registerToken.Should().NotBeNull();
    }

    [Theory]
    [InlineData("ax")]
    [InlineData("bx")]
    [InlineData("cx")]
    [InlineData("dx")]
    [InlineData("si")]
    [InlineData("di")]
    [InlineData("sp")]
    [InlineData("bp")]
    public void ProvideTokens_With16BitRegister_RecognizesAsRegister(string register)
    {
        // Arrange
        var text = $"mov {register}, 0";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var registerToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Parameter);
        registerToken.Should().NotBeNull();
    }

    [Theory]
    [InlineData("al")]
    [InlineData("ah")]
    [InlineData("bl")]
    [InlineData("bh")]
    [InlineData("cl")]
    [InlineData("ch")]
    [InlineData("dl")]
    [InlineData("dh")]
    public void ProvideTokens_With8BitRegister_RecognizesAsRegister(string register)
    {
        // Arrange
        var text = $"mov {register}, 0";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var registerToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Parameter);
        registerToken.Should().NotBeNull();
    }

    #endregion

    #region Number Tests

    [Fact]
    public void ProvideTokens_WithDecimalNumber_ReturnsNumberToken()
    {
        // Arrange
        var text = "mov rax, 42";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Number);
        numberToken.Should().NotBeNull();
        numberToken!.Length.Should().Be(2); // 42
    }

    [Fact]
    public void ProvideTokens_WithHexNumber_ReturnsNumberToken()
    {
        // Arrange
        var text = "mov rax, 0x10";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Number);
        numberToken.Should().NotBeNull();
        numberToken!.Length.Should().Be(4); // 0x10
    }

    [Fact]
    public void ProvideTokens_WithHexNumberUppercase_ReturnsNumberToken()
    {
        // Arrange
        var text = "mov rax, 0X10";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Number);
        numberToken.Should().NotBeNull();
    }

    [Fact]
    public void ProvideTokens_WithBinaryNumber_ReturnsNumberToken()
    {
        // Arrange
        var text = "mov rax, 0b1010";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Number);
        numberToken.Should().NotBeNull();
        numberToken!.Length.Should().Be(6); // 0b1010
    }

    [Fact]
    public void ProvideTokens_WithNegativeNumber_ReturnsNumberToken()
    {
        // Arrange
        var text = "mov rax, -42";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Number);
        numberToken.Should().NotBeNull();
        numberToken!.Length.Should().Be(3); // -42
    }

    #endregion

    #region Comment Tests

    [Fact]
    public void ProvideTokens_WithComment_ReturnsCommentToken()
    {
        // Arrange
        var text = "mov rax, rbx ; copy rbx to rax";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var commentToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Comment);
        commentToken.Should().NotBeNull();
        commentToken!.StartChar.Should().Be(13); // position of ';'
        commentToken.Length.Should().Be(17); // "; copy rbx to rax" (17 chars from semicolon to end)
    }

    [Fact]
    public void ProvideTokens_WithLineStartingWithComment_ReturnsCommentToken()
    {
        // Arrange
        var text = "; This is a comment";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].TokenType.Should().Be(SemanticTokenType.Comment);
        tokens[0].StartChar.Should().Be(0);
        tokens[0].Length.Should().Be(text.Length);
    }

    [Fact]
    public void ProvideTokens_WithMultipleLines_TokensHaveCorrectLineNumbers()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx  ; line 0",
            "add rcx, rdx  ; line 1",
            "sub rsi, rdi  ; line 2"
        };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Where(t => t.Line == 0).Should().NotBeEmpty();
        tokens.Where(t => t.Line == 1).Should().NotBeEmpty();
        tokens.Where(t => t.Line == 2).Should().NotBeEmpty();

        // Check comment positions
        var commentTokens = tokens.Where(t => t.TokenType == SemanticTokenType.Comment).ToList();
        commentTokens.Should().HaveCount(3);
        commentTokens[0].Line.Should().Be(0);
        commentTokens[1].Line.Should().Be(1);
        commentTokens[2].Line.Should().Be(2);
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void ProvideTokens_WithOperators_ReturnsOperatorTokens()
    {
        // Arrange
        var text = "mov [rax + rbx * 8], rcx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var operatorTokens = tokens.Where(t => t.TokenType == SemanticTokenType.Operator).ToList();
        operatorTokens.Should().HaveCountGreaterOrEqualTo(3); // [, +, *, ], ,
    }

    [Theory]
    [InlineData(",")]
    [InlineData(":")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    public void ProvideTokens_WithOperator_RecognizesAsOperator(string op)
    {
        // Arrange
        var text = $"mov rax{op} rbx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var operatorToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Operator);
        operatorToken.Should().NotBeNull();
    }

    #endregion

    #region Label Tests

    [Fact]
    public void ProvideTokens_WithLabel_ReturnsLabelToken()
    {
        // Arrange
        var text = "my_function:";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var labelToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Function);
        labelToken.Should().NotBeNull();
        labelToken!.StartChar.Should().Be(0);
        labelToken.Length.Should().Be(11); // my_function
    }

    [Fact]
    public void ProvideTokens_WithLabelReference_ReturnsVariableToken()
    {
        // Arrange
        var text = "call my_function";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        var variableToken = tokens.FirstOrDefault(t => t.TokenType == SemanticTokenType.Variable);
        variableToken.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ProvideTokens_WithEmptyLine_ReturnsEmptyList()
    {
        // Arrange
        var lines = new[] { "" };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void ProvideTokens_WithWhitespaceOnlyLine_ReturnsEmptyList()
    {
        // Arrange
        var lines = new[] { "   \t  " };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact]
    public void ProvideTokens_WithMultipleSpaces_PreservesCorrectPositions()
    {
        // Arrange
        var text = "mov     rax,    rbx";
        var lines = new[] { text };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().NotBeEmpty();

        // Verify positions are correct despite extra spaces
        var movToken = tokens.First(t => t.TokenType == SemanticTokenType.Keyword);
        movToken.StartChar.Should().Be(0);
        movToken.Length.Should().Be(3);

        var raxToken = tokens.First(t => t.TokenType == SemanticTokenType.Parameter && t.StartChar > 3);
        raxToken.StartChar.Should().Be(8); // after "mov     "
    }

    [Fact]
    public void ProvideTokens_WithNullLines_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideSemanticTokens(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideTokens_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var lines = Array.Empty<string>();

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().BeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideTokens_WithComplexAssembly_ReturnsAllTokenTypes()
    {
        // Arrange
        var lines = new[]
        {
            "; Function to add two numbers",
            "add_numbers:",
            "    push rbp",
            "    mov rbp, rsp",
            "    mov rax, [rbp + 0x10]",
            "    add rax, 42",
            "    pop rbp",
            "    ret  ; return result"
        };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().NotBeEmpty();

        // Should have all token types
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Comment);
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Function);
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Keyword);
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Parameter);
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Number);
        tokens.Should().Contain(t => t.TokenType == SemanticTokenType.Operator);
    }

    [Fact]
    public void ProvideTokens_TokensAreSortedByPosition()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx",
            "add rcx, rdx"
        };

        // Act
        var tokens = _provider.ProvideSemanticTokens(lines);

        // Assert
        tokens.Should().NotBeEmpty();

        // Tokens should be sorted by line, then by start char
        for (int i = 1; i < tokens.Count; i++)
        {
            var prev = tokens[i - 1];
            var curr = tokens[i];

            if (prev.Line == curr.Line)
            {
                prev.StartChar.Should().BeLessThan(curr.StartChar);
            }
            else
            {
                prev.Line.Should().BeLessThan(curr.Line);
            }
        }
    }

    #endregion
}
