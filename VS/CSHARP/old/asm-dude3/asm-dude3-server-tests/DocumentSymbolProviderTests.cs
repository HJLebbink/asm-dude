using AsmDude3.Server;
using AsmDude3.Server.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Tests for document symbol provider (outline view)
/// </summary>
public class DocumentSymbolProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly DocumentSymbolProvider _provider;

    public DocumentSymbolProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new DocumentSymbolProvider(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new DocumentSymbolProvider(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var provider = new DocumentSymbolProvider(_loggerMock.Object);

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region Label Detection Tests

    [Fact]
    public void ProvideSymbols_SingleLabel_ReturnsOneSymbol()
    {
        // Arrange
        var lines = new[] { "main:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(1);
        symbols[0].Name.Should().Be("main");
        symbols[0].Kind.Should().Be(SymbolKind.Function);
        symbols[0].Line.Should().Be(0);
    }

    [Fact]
    public void ProvideSymbols_MultipleLabels_ReturnsAllSymbols()
    {
        // Arrange
        var lines = new[]
        {
            "main:",
            "    mov rax, rbx",
            "helper:",
            "    ret",
            "data_label:",
            "    db 0"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(3);
        symbols.Should().Contain(s => s.Name == "main");
        symbols.Should().Contain(s => s.Name == "helper");
        symbols.Should().Contain(s => s.Name == "data_label");
    }

    [Fact]
    public void ProvideSymbols_LabelWithLeadingWhitespace_IsDetected()
    {
        // Arrange
        var lines = new[] { "    start:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(1);
        symbols[0].Name.Should().Be("start");
    }

    [Fact]
    public void ProvideSymbols_LabelWithUnderscores_IsDetected()
    {
        // Arrange
        var lines = new[] { "my_function_name:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(1);
        symbols[0].Name.Should().Be("my_function_name");
    }

    [Fact]
    public void ProvideSymbols_LabelWithNumbers_IsDetected()
    {
        // Arrange
        var lines = new[] { "loop1:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(1);
        symbols[0].Name.Should().Be("loop1");
    }

    [Fact]
    public void ProvideSymbols_LabelWithDot_IsDetected()
    {
        // Arrange
        var lines = new[] { ".local_label:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(1);
        symbols[0].Name.Should().Be(".local_label");
    }

    #endregion

    #region Symbol Classification Tests

    [Theory]
    [InlineData("main")]
    [InlineData("_start")]
    [InlineData("WinMain")]
    [InlineData("DllMain")]
    public void ProvideSymbols_CommonFunctionNames_ClassifiedAsFunction(string name)
    {
        // Arrange
        var lines = new[] { $"{name}:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols[0].Kind.Should().Be(SymbolKind.Function);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("buffer")]
    [InlineData("string_data")]
    [InlineData("table")]
    public void ProvideSymbols_DataLabels_ClassifiedAsVariable(string name)
    {
        // Arrange
        var lines = new[] { $"{name}:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols[0].Kind.Should().Be(SymbolKind.Variable);
    }

    [Theory]
    [InlineData(".text")]
    [InlineData(".data")]
    [InlineData(".bss")]
    [InlineData(".rodata")]
    public void ProvideSymbols_SectionDirectives_ClassifiedAsNamespace(string name)
    {
        // Arrange
        var lines = new[] { name };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().Contain(s => s.Kind == SymbolKind.Namespace);
    }

    #endregion

    #region Position Tests

    [Fact]
    public void ProvideSymbols_MultipleLabels_CorrectLineNumbers()
    {
        // Arrange
        var lines = new[]
        {
            "; Comment",
            "first:",
            "    nop",
            "",
            "second:",
            "    ret"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(2);
        symbols[0].Name.Should().Be("first");
        symbols[0].Line.Should().Be(1);
        symbols[1].Name.Should().Be("second");
        symbols[1].Line.Should().Be(4);
    }

    [Fact]
    public void ProvideSymbols_SymbolsAreSorted_ByLineNumber()
    {
        // Arrange
        var lines = new[]
        {
            "third:",
            "first:",
            "second:"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(3);
        symbols[0].Line.Should().Be(0);
        symbols[1].Line.Should().Be(1);
        symbols[2].Line.Should().Be(2);
    }

    #endregion

    #region Filtering Tests

    [Fact]
    public void ProvideSymbols_CommentsIgnored_NoSymbols()
    {
        // Arrange
        var lines = new[]
        {
            "; This is a comment",
            "; label: this is not a label"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().BeEmpty();
    }

    [Fact]
    public void ProvideSymbols_EmptyLines_NoSymbols()
    {
        // Arrange
        var lines = new[] { "", "   ", "\t" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().BeEmpty();
    }

    [Fact]
    public void ProvideSymbols_InstructionsWithoutLabels_NoSymbols()
    {
        // Arrange
        var lines = new[]
        {
            "mov rax, rbx",
            "add rax, 1",
            "ret"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().BeEmpty();
    }

    [Fact]
    public void ProvideSymbols_MixedContent_OnlyLabelsDetected()
    {
        // Arrange
        var lines = new[]
        {
            "; Function prologue",
            "main:",
            "    push rbp",
            "    mov rbp, rsp",
            "",
            "; Loop start",
            "loop:",
            "    dec rcx",
            "    jnz loop",
            "",
            "; Function epilogue",
            "    pop rbp",
            "    ret"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(2);
        symbols.Should().Contain(s => s.Name == "main");
        symbols.Should().Contain(s => s.Name == "loop");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ProvideSymbols_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var lines = Array.Empty<string>();

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().BeEmpty();
    }

    [Fact]
    public void ProvideSymbols_WithNullArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideSymbols(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideSymbols_DuplicateLabels_AllIncluded()
    {
        // Arrange (though this is unusual in assembly)
        var lines = new[]
        {
            "label:",
            "    nop",
            "label:",
            "    nop"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().HaveCount(2);
        symbols.Should().OnlyContain(s => s.Name == "label");
    }

    #endregion

    #region Range Tests

    [Fact]
    public void ProvideSymbols_SymbolRange_CoversLabelName()
    {
        // Arrange
        var lines = new[] { "my_function:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols[0].Range.Should().NotBeNull();
        symbols[0].Range!.StartChar.Should().Be(0);
        symbols[0].Range.EndChar.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProvideSymbols_IndentedLabel_RangeExcludesWhitespace()
    {
        // Arrange
        var lines = new[] { "    start:" };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols[0].Range.Should().NotBeNull();
        symbols[0].Range!.StartChar.Should().Be(4); // After whitespace
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideSymbols_CompleteProgram_DetectsAllSymbols()
    {
        // Arrange - a realistic assembly program
        var lines = new[]
        {
            ".text",
            ".global main",
            "",
            "main:",
            "    push rbp",
            "    mov rbp, rsp",
            "    call helper",
            "    mov rax, 0",
            "    pop rbp",
            "    ret",
            "",
            "helper:",
            "    mov rax, 42",
            "    ret",
            "",
            ".data",
            "message:",
            "    .asciz \"Hello\"",
            "",
            ".bss",
            "buffer:",
            "    .space 100"
        };

        // Act
        var symbols = _provider.ProvideSymbols(lines);

        // Assert
        symbols.Should().Contain(s => s.Name == "main");
        symbols.Should().Contain(s => s.Name == "helper");
        symbols.Should().Contain(s => s.Name == "message");
        symbols.Should().Contain(s => s.Name == "buffer");
        symbols.Should().Contain(s => s.Name == ".text");
        symbols.Should().Contain(s => s.Name == ".data");
        symbols.Should().Contain(s => s.Name == ".bss");
    }

    #endregion
}
