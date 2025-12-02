using AsmDude3.Server;
using AsmDude3.Server.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Tests for completion provider (code completion / IntelliSense)
/// </summary>
public class CompletionProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly CompletionProvider _provider;

    public CompletionProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new CompletionProvider(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CompletionProvider(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var provider = new CompletionProvider(_loggerMock.Object);

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region Mnemonic Completion Tests

    [Fact]
    public void ProvideCompletions_WithEmptyPrefix_ReturnsAllMnemonics()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();
        completions.Should().Contain(c => c.Label == "mov");
        completions.Should().Contain(c => c.Label == "add");
        completions.Should().Contain(c => c.Label == "sub");
        completions.Should().Contain(c => c.Label == "call");
        completions.Should().Contain(c => c.Label == "ret");
    }

    [Fact]
    public void ProvideCompletions_WithMoPrefix_ReturnsMovVariants()
    {
        // Arrange
        var line = "mo";
        var position = 2;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();
        completions.Should().Contain(c => c.Label == "mov");
        completions.Should().Contain(c => c.Label == "movb");
        completions.Should().Contain(c => c.Label == "movw");
        completions.Should().Contain(c => c.Label == "movl");
        completions.Should().Contain(c => c.Label == "movq");
        completions.Should().Contain(c => c.Label == "movzx");
        completions.Should().Contain(c => c.Label == "movsx");
        completions.Should().Contain(c => c.Label == "movsb");
    }

    [Fact]
    public void ProvideCompletions_WithJPrefix_ReturnsJumpInstructions()
    {
        // Arrange
        var line = "j";
        var position = 1;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();
        completions.Should().Contain(c => c.Label == "jmp");
        completions.Should().Contain(c => c.Label == "je");
        completions.Should().Contain(c => c.Label == "jne");
        completions.Should().Contain(c => c.Label == "jz");
        completions.Should().Contain(c => c.Label == "jnz");
        completions.Should().Contain(c => c.Label == "jg");
        completions.Should().Contain(c => c.Label == "jl");
    }

    [Fact]
    public void ProvideCompletions_MnemonicContext_IncludesDescriptions()
    {
        // Arrange
        var line = "mov";
        var position = 3;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        var movCompletion = completions.First(c => c.Label == "mov");
        movCompletion.Detail.Should().NotBeNullOrEmpty();
        movCompletion.Documentation.Should().NotBeNullOrEmpty();
        movCompletion.Kind.Should().Be(CompletionItemKind.Keyword);
    }

    [Fact]
    public void ProvideCompletions_CaseInsensitive_MatchesMnemonics()
    {
        // Arrange
        var line = "MO";
        var position = 2;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().Contain(c => c.Label == "mov");
        completions.Should().Contain(c => c.Label == "movb");
    }

    #endregion

    #region Register Completion Tests

    [Fact]
    public void ProvideCompletions_WithEmptyRegisterPrefix_ReturnsAllRegisters()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert
        completions.Should().NotBeEmpty();
        completions.Should().Contain(c => c.Label == "rax");
        completions.Should().Contain(c => c.Label == "rbx");
        completions.Should().Contain(c => c.Label == "eax");
        completions.Should().Contain(c => c.Label == "ax");
    }

    [Fact]
    public void ProvideCompletions_WithRPrefix_Returns64BitRegisters()
    {
        // Arrange
        var line = "r";
        var position = 1;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert
        completions.Should().Contain(c => c.Label == "rax");
        completions.Should().Contain(c => c.Label == "rbx");
        completions.Should().Contain(c => c.Label == "rcx");
        completions.Should().Contain(c => c.Label == "rdx");
        completions.Should().Contain(c => c.Label == "rsi");
        completions.Should().Contain(c => c.Label == "rdi");
        completions.Should().Contain(c => c.Label == "r8");
        completions.Should().Contain(c => c.Label == "r9");
    }

    [Fact]
    public void ProvideCompletions_WithEPrefix_Returns32BitRegisters()
    {
        // Arrange
        var line = "e";
        var position = 1;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert
        completions.Should().Contain(c => c.Label == "eax");
        completions.Should().Contain(c => c.Label == "ebx");
        completions.Should().Contain(c => c.Label == "ecx");
        completions.Should().Contain(c => c.Label == "edx");
    }

    [Fact]
    public void ProvideCompletions_RegisterContext_IncludesDescriptions()
    {
        // Arrange
        var line = "rax";
        var position = 3;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert
        var raxCompletion = completions.First(c => c.Label == "rax");
        raxCompletion.Detail.Should().NotBeNullOrEmpty();
        raxCompletion.Documentation.Should().NotBeNullOrEmpty();
        raxCompletion.Kind.Should().Be(CompletionItemKind.Variable);
    }

    [Fact]
    public void ProvideCompletions_WithR8Prefix_ReturnsExtendedRegisters()
    {
        // Arrange
        var line = "r8";
        var position = 2;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert
        completions.Should().Contain(c => c.Label == "r8");
        completions.Should().Contain(c => c.Label == "r8d");
        completions.Should().Contain(c => c.Label == "r8w");
        completions.Should().Contain(c => c.Label == "r8b");
    }

    #endregion

    #region Context Detection Tests

    [Fact]
    public void DetermineContext_AtStartOfLine_ReturnsMnemonic()
    {
        // Arrange
        var line = "mov";
        var position = 0;

        // Act
        var context = _provider.DetermineContext(line, position);

        // Assert
        context.Should().Be(CompletionContext.Mnemonic);
    }

    [Fact]
    public void DetermineContext_AfterComma_ReturnsRegister()
    {
        // Arrange
        var line = "mov rax, ";
        var position = 9;

        // Act
        var context = _provider.DetermineContext(line, position);

        // Assert
        context.Should().Be(CompletionContext.Register);
    }

    [Fact]
    public void DetermineContext_AfterOpenBracket_ReturnsRegister()
    {
        // Arrange
        var line = "mov [";
        var position = 5;

        // Act
        var context = _provider.DetermineContext(line, position);

        // Assert
        context.Should().Be(CompletionContext.Register);
    }

    [Fact]
    public void DetermineContext_AfterWhitespace_ReturnsRegister()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var context = _provider.DetermineContext(line, position);

        // Assert
        context.Should().Be(CompletionContext.Register);
    }

    [Fact]
    public void DetermineContext_AfterMnemonic_ReturnsRegister()
    {
        // Arrange
        var line = "push ";
        var position = 5;

        // Act
        var context = _provider.DetermineContext(line, position);

        // Assert
        context.Should().Be(CompletionContext.Register);
    }

    #endregion

    #region Sorting and Ranking Tests

    [Fact]
    public void ProvideCompletions_SortsResults_ByRelevance()
    {
        // Arrange
        var line = "mov";
        var position = 3;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();

        // Exact match should come first
        completions.First().Label.Should().StartWith("mov");
    }

    [Fact]
    public void ProvideCompletions_PrioritizesExactMatch()
    {
        // Arrange
        var line = "jmp";
        var position = 3;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.First().Label.Should().Be("jmp");
    }

    [Fact]
    public void ProvideCompletions_FiltersByPrefix()
    {
        // Arrange
        var line = "xyz";
        var position = 3;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().BeEmpty();
    }

    #endregion

    #region Special Cases Tests

    [Fact]
    public void ProvideCompletions_WithNullLine_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideCompletions(null!, 0, CompletionContext.Mnemonic);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideCompletions_WithNegativePosition_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _provider.ProvideCompletions("mov", -1, CompletionContext.Mnemonic);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProvideCompletions_PositionBeyondLine_UsesLineEnd()
    {
        // Arrange
        var line = "mov";
        var position = 100;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();
    }

    [Fact]
    public void ProvideCompletions_EmptyLine_ReturnsMnemonics()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert
        completions.Should().NotBeEmpty();
        completions.Should().Contain(c => c.Label == "mov");
    }

    [Fact]
    public void ProvideCompletions_WhitespaceOnly_ReturnsMnemonics()
    {
        // Arrange
        var line = "   ";
        var position = 3;

        // Act
        var context = _provider.DetermineContext(line, position);
        var completions = _provider.ProvideCompletions(line, position, context);

        // Assert
        completions.Should().NotBeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideCompletions_CompleteWorkflow_MovInstruction()
    {
        // Arrange
        var lines = new[]
        {
            "m",      // User types 'm'
            "mo",     // User types 'o'
            "mov"     // User types 'v'
        };

        // Act & Assert
        foreach (var line in lines)
        {
            var context = _provider.DetermineContext(line, line.Length);
            var completions = _provider.ProvideCompletions(line, line.Length, context);

            completions.Should().Contain(c => c.Label == "mov");
        }
    }

    [Fact]
    public void ProvideCompletions_CompleteWorkflow_WithOperands()
    {
        // Test: "mov r" → should suggest registers starting with 'r'
        var line = "mov r";
        var position = line.Length;

        var context = _provider.DetermineContext(line, position);
        context.Should().Be(CompletionContext.Register);

        var completions = _provider.ProvideCompletions(line, position, context);
        completions.Should().Contain(c => c.Label == "rax");
        completions.Should().Contain(c => c.Label == "rbx");
    }

    [Fact]
    public void ProvideCompletions_MultipleContexts_InSingleLine()
    {
        // Test different positions in the same line
        var line = "mov rax, rbx";

        // Position 0: should suggest mnemonics
        var context1 = _provider.DetermineContext(line, 0);
        context1.Should().Be(CompletionContext.Mnemonic);

        // Position 4 (after "mov "): should suggest registers
        var context2 = _provider.DetermineContext(line, 4);
        context2.Should().Be(CompletionContext.Register);

        // Position 9 (after "mov rax, "): should suggest registers
        var context3 = _provider.DetermineContext(line, 9);
        context3.Should().Be(CompletionContext.Register);
    }

    [Fact]
    public void ProvideCompletions_AllMnemonicCategories_Represented()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Mnemonic);

        // Assert - check various categories
        completions.Should().Contain(c => c.Label == "mov");    // Data movement
        completions.Should().Contain(c => c.Label == "add");    // Arithmetic
        completions.Should().Contain(c => c.Label == "and");    // Logical
        completions.Should().Contain(c => c.Label == "shl");    // Shift
        completions.Should().Contain(c => c.Label == "jmp");    // Control flow
        completions.Should().Contain(c => c.Label == "push");   // Stack
        completions.Should().Contain(c => c.Label == "call");   // Procedure
        completions.Should().Contain(c => c.Label == "test");   // Comparison
    }

    [Fact]
    public void ProvideCompletions_AllRegisterSizes_Represented()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var completions = _provider.ProvideCompletions(line, position, CompletionContext.Register);

        // Assert - check various sizes
        completions.Should().Contain(c => c.Label == "rax");   // 64-bit
        completions.Should().Contain(c => c.Label == "eax");   // 32-bit
        completions.Should().Contain(c => c.Label == "ax");    // 16-bit
        completions.Should().Contain(c => c.Label == "al");    // 8-bit low
        completions.Should().Contain(c => c.Label == "ah");    // 8-bit high
    }

    #endregion
}
