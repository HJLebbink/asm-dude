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
/// Integration tests for signature help provider (operand format hints)
/// Uses real MnemonicStore with actual signature data
/// </summary>
public class SignatureHelpProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly SignatureHelpProvider _provider;
    private readonly MnemonicStore _mnemonicStore;
    private readonly AsmLanguageServerOptions _options;

    public SignatureHelpProviderTests()
    {
        _loggerMock = new Mock<ILogger>();

        // Create real options with all architectures enabled
        _options = CreateDefaultOptions();

        // Create real MnemonicStore with actual signature files
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var filename_Regular = Path.Combine(resourceDir, "signature-may2019.txt");
        var filename_Hand = Path.Combine(resourceDir, "signature-hand-1.txt");

        _mnemonicStore = new MnemonicStore(_loggerMock.Object, filename_Regular, filename_Hand, _options);
        _provider = new SignatureHelpProvider(_loggerMock.Object, _mnemonicStore, _options);
    }

    private static AsmLanguageServerOptions CreateDefaultOptions()
    {
        return new AsmLanguageServerOptions
        {
            SignatureHelp_On = true,
            ARCH_8086 = true,
            ARCH_186 = true,
            ARCH_286 = true,
            ARCH_386 = true,
            ARCH_486 = true,
            ARCH_MMX = true,
            ARCH_SSE = true,
            ARCH_SSE2 = true,
            ARCH_SSE3 = true,
            ARCH_SSSE3 = true,
            ARCH_SSE4_1 = true,
            ARCH_SSE4_2 = true,
            ARCH_SSE4A = true,
            ARCH_SSE5 = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_VL = true,
            ARCH_AVX512_PF = true,
            ARCH_AVX512_DQ = true,
            ARCH_AVX512_BW = true,
            ARCH_AVX512_ER = true,
            ARCH_AVX512_F = true,
            ARCH_AVX512_CD = true,
            ARCH_X64 = true,
            ARCH_BMI1 = true,
            ARCH_BMI2 = true,
            ARCH_P6 = true,
            ARCH_IA64 = true,
            ARCH_FMA = true,
            ARCH_TBM = true,
            ARCH_AMD = true,
            ARCH_PENT = true,
            ARCH_3DNOW = true,
            ARCH_CYRIX = true,
            ARCH_CYRIXM = true,
            ARCH_VMX = true,
            ARCH_RTM = true,
            ARCH_MPX = true,
            ARCH_SHA = true,
            ARCH_ADX = true,
            ARCH_F16C = true,
            ARCH_FSGSBASE = true,
            ARCH_HLE = true,
            ARCH_INVPCID = true,
            ARCH_PCLMULQDQ = true,
            ARCH_LZCNT = true,
            ARCH_PREFETCHWT1 = true,
            ARCH_PRFCHW = true,
            ARCH_RDPID = true,
            ARCH_RDRAND = true,
            ARCH_RDSEED = true,
            ARCH_XSAVEOPT = true,
            ARCH_UNDOC = true,
            ARCH_AES = true,
            ARCH_AVX512_IFMA = true,
            ARCH_AVX512_VBMI = true,
            ARCH_AVX512_VPOPCNTDQ = true,
            ARCH_AVX512_4VNNIW = true,
            ARCH_AVX512_4FMAPS = true,
            ARCH_AVX512_VBMI2 = true,
            ARCH_AVX512_VNNI = true,
            ARCH_AVX512_BITALG = true,
            ARCH_AVX512_GFNI = true,
            ARCH_AVX512_VAES = true,
            ARCH_AVX512_VPCLMULQDQ = true,
            ARCH_SMX = true,
            ARCH_SGX1 = true,
            ARCH_SGX2 = true,
            ARCH_CLDEMOTE = true,
            ARCH_MOVDIR64B = true,
            ARCH_MOVDIRI = true,
            ARCH_PCONFIG = true,
            ARCH_WAITPKG = true,
            ARCH_AVX512_BF16 = true,
            ARCH_AVX512_VP2INTERSECT = true,
            ARCH_ENQCMD = true,
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SignatureHelpProvider(null!, _mnemonicStore, _options);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullMnemonicStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SignatureHelpProvider(_loggerMock.Object, null!, _options);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mnemonicStore");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SignatureHelpProvider(_loggerMock.Object, _mnemonicStore, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var provider = new SignatureHelpProvider(_loggerMock.Object, _mnemonicStore, _options);

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region MOV Instruction Tests

    [Fact]
    public void ProvideSignatureHelp_ForMov_ReturnsSignatures()
    {
        // Arrange
        var line = "mov ";
        var position = 4; // After "mov "

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("reg, reg"));
        help.Signatures.Should().Contain(s => s.Label.Contains("reg, imm"));
        help.Signatures.Should().Contain(s => s.Label.Contains("reg, mem"));
        help.Signatures.Should().Contain(s => s.Label.Contains("mem, reg"));
    }

    [Fact]
    public void ProvideSignatureHelp_ForMovWithOneOperand_HighlightsSecondParameter()
    {
        // Arrange
        var line = "mov rax, ";
        var position = 9; // After comma

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.ActiveParameter.Should().Be(1); // Second parameter
    }

    [Fact]
    public void ProvideSignatureHelp_ForMovCaseInsensitive_ReturnsSignatures()
    {
        // Arrange
        var line = "MOV ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
    }

    #endregion

    #region ADD Instruction Tests

    [Fact]
    public void ProvideSignatureHelp_ForAdd_ReturnsSignatures()
    {
        // Arrange
        var line = "add ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("reg, reg"));
        help.Signatures.Should().Contain(s => s.Label.Contains("reg, imm"));
    }

    [Fact]
    public void ProvideSignatureHelp_ForAddWithDocumentation_ReturnsDoc()
    {
        // Arrange
        var line = "add ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        var firstSignature = help!.Signatures.First();
        firstSignature.Documentation.Should().NotBeNullOrEmpty();
        firstSignature.Documentation.Should().Contain("Add");
    }

    #endregion

    #region PUSH/POP Tests

    [Fact]
    public void ProvideSignatureHelp_ForPush_ReturnsSingleOperandSignatures()
    {
        // Arrange
        var line = "push ";
        var position = 5;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("reg"));
        help.Signatures.Should().Contain(s => s.Label.Contains("mem"));
        help.Signatures.Should().Contain(s => s.Label.Contains("imm"));
    }

    [Fact]
    public void ProvideSignatureHelp_ForPop_ReturnsSingleOperandSignatures()
    {
        // Arrange
        var line = "pop ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("reg"));
        help.Signatures.Should().Contain(s => s.Label.Contains("mem"));
    }

    #endregion

    #region JMP/CALL Tests

    [Fact]
    public void ProvideSignatureHelp_ForJmp_ReturnsLabelSignature()
    {
        // Arrange
        var line = "jmp ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("label") || s.Label.Contains("addr"));
    }

    [Fact]
    public void ProvideSignatureHelp_ForCall_ReturnsLabelSignature()
    {
        // Arrange
        var line = "call ";
        var position = 5;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().Contain(s => s.Label.Contains("label") || s.Label.Contains("addr"));
    }

    #endregion

    #region Conditional Jump Tests

    [Theory]
    [InlineData("je ")]
    [InlineData("jne ")]
    [InlineData("jg ")]
    [InlineData("jl ")]
    [InlineData("ja ")]
    [InlineData("jb ")]
    public void ProvideSignatureHelp_ForConditionalJumps_ReturnsLabelSignature(string instruction)
    {
        // Arrange
        var position = instruction.Length;

        // Act
        var help = _provider.ProvideSignatureHelp(instruction, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
    }

    #endregion

    #region Parameter Tracking Tests

    [Fact]
    public void ProvideSignatureHelp_NoOperands_ActiveParameterIsZero()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.ActiveParameter.Should().Be(0);
    }

    [Fact]
    public void ProvideSignatureHelp_AfterComma_ActiveParameterIsOne()
    {
        // Arrange
        var line = "mov rax, ";
        var position = 9;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.ActiveParameter.Should().Be(1);
    }

    [Fact]
    public void ProvideSignatureHelp_BeforeComma_ActiveParameterIsZero()
    {
        // Arrange
        var line = "mov rax";
        var position = 7;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.ActiveParameter.Should().Be(0);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ProvideSignatureHelp_UnknownInstruction_ReturnsNull()
    {
        // Arrange
        var line = "xyz ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().BeNull();
    }

    [Fact]
    public void ProvideSignatureHelp_EmptyLine_ReturnsNull()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().BeNull();
    }

    [Fact]
    public void ProvideSignatureHelp_OnlyWhitespace_ReturnsNull()
    {
        // Arrange
        var line = "   ";
        var position = 2;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().BeNull();
    }

    [Fact]
    public void ProvideSignatureHelp_WithNullLine_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideSignatureHelp(null!, 0, 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideSignatureHelp_WithNegativePosition_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _provider.ProvideSignatureHelp("mov ", -1, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProvideSignatureHelp_PositionBeyondLine_ReturnsNull()
    {
        // Arrange
        var line = "mov";
        var position = 100;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().BeNull();
    }

    [Fact]
    public void ProvideSignatureHelp_NoSpaceAfterInstruction_ReturnsNull()
    {
        // Arrange
        var line = "mov";
        var position = 3;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().BeNull();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideSignatureHelp_CompleteWorkflow_MovInstruction()
    {
        // Test progression as user types
        var testCases = new[]
        {
            ("m", 1, false),           // Still typing instruction
            ("mo", 2, false),          // Still typing instruction
            ("mov", 3, false),         // Instruction complete, no space
            ("mov ", 4, true),         // Space after instruction, should show help
            ("mov r", 5, true),        // Typing first operand
            ("mov rax", 7, true),      // First operand complete
            ("mov rax,", 8, true),     // Comma entered, still on first param
            ("mov rax, ", 9, true),    // Space after comma, now on second param
            ("mov rax, rbx", 12, true) // Second operand entered (position at end of line)
        };

        foreach (var (line, position, expectHelp) in testCases)
        {
            var help = _provider.ProvideSignatureHelp(line, position, 0);

            if (expectHelp)
            {
                help.Should().NotBeNull($"Expected help for '{line}' at position {position}");
            }
            else
            {
                help.Should().BeNull($"Did not expect help for '{line}' at position {position}");
            }
        }
    }

    [Fact]
    public void ProvideSignatureHelp_ActiveSignature_IsFirst()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.ActiveSignature.Should().Be(0); // First signature is active by default
    }

    [Fact]
    public void ProvideSignatureHelp_AllSignatures_HaveLabels()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().OnlyContain(s => !string.IsNullOrEmpty(s.Label));
    }

    [Fact]
    public void ProvideSignatureHelp_AllSignatures_HaveDocumentation()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Should().NotBeEmpty();
        help.Signatures.Should().OnlyContain(s => !string.IsNullOrEmpty(s.Documentation));
    }

    #endregion

    #region Multiple Signature Tests

    [Fact]
    public void ProvideSignatureHelp_ForMov_ReturnsMultipleSignatures()
    {
        // Arrange
        var line = "mov ";
        var position = 4;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void ProvideSignatureHelp_ForPush_ReturnsMultipleSignatures()
    {
        // Arrange
        var line = "push ";
        var position = 5;

        // Act
        var help = _provider.ProvideSignatureHelp(line, position, 0);

        // Assert
        help.Should().NotBeNull();
        help!.Signatures.Count.Should().BeGreaterThan(1);
    }

    #endregion
}
