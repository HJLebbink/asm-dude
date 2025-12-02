using AsmDude3.Server;
using AsmDude3.Server.Providers;
using AsmDude3.Server.Stores;
using AsmTools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Integration tests for hover provider (documentation on hover)
/// Uses real AsmDudeTools and MnemonicStore with actual data
/// </summary>
public class HoverProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly HoverProvider _provider;
    private readonly AsmDude2Tools _asmDudeTools;
    private readonly MnemonicStore _mnemonicStore;

    public HoverProviderTests()
    {
        _loggerMock = new Mock<ILogger>();

        // Create real AsmDudeTools with actual XML data
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude3-server", "Resources");
        var traceSource = new TraceSource("AsmDude3Tests");
        _asmDudeTools = AsmDude2Tools.Create(resourceDir, traceSource);

        // Create real MnemonicStore with actual signature files
        var options = CreateDefaultOptions();
        var filename_Regular = Path.Combine(resourceDir, "signature-may2019.txt");
        var filename_Hand = Path.Combine(resourceDir, "signature-hand-1.txt");
        _mnemonicStore = new MnemonicStore(_loggerMock.Object, filename_Regular, filename_Hand, options);

        _provider = new HoverProvider(_loggerMock.Object, _asmDudeTools, _mnemonicStore);
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
        var act = () => new HoverProvider(null!, _asmDudeTools, _mnemonicStore);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAsmDudeTools_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HoverProvider(_loggerMock.Object, null!, _mnemonicStore);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("asmDudeTools");
    }

    [Fact]
    public void Constructor_WithNullMnemonicStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HoverProvider(_loggerMock.Object, _asmDudeTools, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mnemonicStore");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var provider = new HoverProvider(_loggerMock.Object, _asmDudeTools, _mnemonicStore);

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region Mnemonic Hover Tests

    [Fact]
    public void ProvideHover_OverMovMnemonic_ReturnsDocumentation()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 1; // Inside "mov"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("mov");
        hover.Contents.Should().Contain("Move");
        hover.Contents.Should().Contain("Copy data from source to destination");
    }

    [Fact]
    public void ProvideHover_OverAddMnemonic_ReturnsDocumentation()
    {
        // Arrange
        var line = "add rax, 42";
        var position = 1; // Inside "add"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("add");
        hover.Contents.Should().Contain("Add");
    }

    [Fact]
    public void ProvideHover_OverJmpMnemonic_ReturnsDocumentation()
    {
        // Arrange
        var line = "jmp label";
        var position = 2; // Inside "jmp"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("jmp");
        hover.Contents.Should().Contain("Jump");
    }

    [Theory]
    [InlineData("mov", 0)]
    [InlineData("add", 0)]
    [InlineData("sub", 1)]
    [InlineData("push", 2)]
    [InlineData("pop", 1)]
    [InlineData("call", 2)]
    [InlineData("ret", 1)]
    [InlineData("jmp", 2)]
    [InlineData("je", 0)]
    [InlineData("jne", 1)]
    public void ProvideHover_OverCommonMnemonics_ReturnsDocumentation(string mnemonic, int position)
    {
        // Arrange
        var line = $"{mnemonic} rax";

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().NotBeEmpty();
        hover.Contents.Should().Contain(mnemonic);
    }

    [Fact]
    public void ProvideHover_MnemonicCaseInsensitive_ReturnsDocumentation()
    {
        // Arrange
        var line = "MOV rax, rbx";
        var position = 1; // Inside "MOV"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("Move");
    }

    #endregion

    #region Register Hover Tests

    [Fact]
    public void ProvideHover_OverRaxRegister_ReturnsDocumentation()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 5; // Inside "rax"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("rax");
        hover.Contents.Should().Contain("64-bit");
        hover.Contents.Should().Contain("accumulator");
    }

    [Fact]
    public void ProvideHover_OverEaxRegister_ReturnsDocumentation()
    {
        // Arrange
        var line = "mov eax, ebx";
        var position = 5; // Inside "eax"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("eax");
        hover.Contents.Should().Contain("32-bit");
    }

    [Fact]
    public void ProvideHover_OverAxRegister_ReturnsDocumentation()
    {
        // Arrange
        var line = "mov ax, bx";
        var position = 5; // Inside "ax"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("ax");
        hover.Contents.Should().Contain("16-bit");
    }

    [Theory]
    [InlineData("rax", "64-bit")]
    [InlineData("rbx", "64-bit")]
    [InlineData("rcx", "64-bit")]
    [InlineData("rdx", "64-bit")]
    [InlineData("eax", "32-bit")]
    [InlineData("ebx", "32-bit")]
    [InlineData("ax", "16-bit")]
    [InlineData("al", "8-bit")]
    public void ProvideHover_OverVariousRegisters_ReturnsDocumentation(string register, string expectedSize)
    {
        // Arrange
        var line = $"mov {register}, 0";
        var position = 4; // Inside register name

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain(register);
        hover.Contents.Should().Contain(expectedSize);
    }

    #endregion

    #region Number Hover Tests

    [Fact]
    public void ProvideHover_OverHexNumber_ReturnsConversions()
    {
        // Arrange
        var line = "mov rax, 0x10";
        var position = 11; // Inside "0x10"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("0x10");
        hover.Contents.Should().Contain("16"); // Decimal
        hover.Contents.Should().Contain("0b"); // Binary representation
    }

    [Fact]
    public void ProvideHover_OverDecimalNumber_ReturnsConversions()
    {
        // Arrange
        var line = "mov rax, 42";
        var position = 10; // Inside "42"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("42");
        hover.Contents.Should().Contain("0x"); // Hex representation
        hover.Contents.Should().Contain("0b"); // Binary representation
    }

    [Fact]
    public void ProvideHover_OverBinaryNumber_ReturnsConversions()
    {
        // Arrange
        var line = "mov rax, 0b1010";
        var position = 11; // Inside "0b1010"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("0b1010");
        hover.Contents.Should().Contain("10"); // Decimal
        hover.Contents.Should().Contain("0xA"); // Hex
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ProvideHover_OverWhitespace_ReturnsNull()
    {
        // Arrange
        var line = "mov    rax, rbx";
        var position = 3; // Inside whitespace

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    [Fact]
    public void ProvideHover_OverComment_ReturnsNull()
    {
        // Arrange
        var line = "mov rax, rbx ; copy rbx to rax";
        var position = 15; // Inside comment

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    [Fact]
    public void ProvideHover_OverOperator_ReturnsNull()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 8; // On comma

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    [Fact]
    public void ProvideHover_WithNullLine_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.ProvideHover(null!, 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProvideHover_WithNegativePosition_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _provider.ProvideHover("mov rax, rbx", -1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProvideHover_PositionBeyondLine_ReturnsNull()
    {
        // Arrange
        var line = "mov";
        var position = 100;

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    [Fact]
    public void ProvideHover_EmptyLine_ReturnsNull()
    {
        // Arrange
        var line = "";
        var position = 0;

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    [Fact]
    public void ProvideHover_OverUnknownMnemonic_ReturnsNull()
    {
        // Arrange
        var line = "xyz rax, rbx";
        var position = 1; // Inside "xyz"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().BeNull();
    }

    #endregion

    #region Label Hover Tests

    [Fact]
    public void ProvideHover_OverLabelDefinition_ReturnsInfo()
    {
        // Arrange
        var line = "my_function:";
        var position = 5; // Inside "my_function"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("my_function");
        hover.Contents.Should().Contain("Label");
    }

    [Fact]
    public void ProvideHover_OverLabelReference_ReturnsInfo()
    {
        // Arrange
        var line = "jmp my_function";
        var position = 6; // Inside "my_function"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Contents.Should().Contain("my_function");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProvideHover_ComplexLine_ReturnsCorrectHover()
    {
        // Test: "mov [rax + 0x10], rbx"
        var line = "mov [rax + 0x10], rbx";

        // Hover over "mov"
        var hover1 = _provider.ProvideHover(line, 1);
        hover1.Should().NotBeNull();
        hover1!.Contents.Should().Contain("Move");

        // Hover over "rax"
        var hover2 = _provider.ProvideHover(line, 6);
        hover2.Should().NotBeNull();
        hover2!.Contents.Should().Contain("rax");

        // Hover over "0x10"
        var hover3 = _provider.ProvideHover(line, 12);
        hover3.Should().NotBeNull();
        hover3!.Contents.Should().Contain("0x10");

        // Hover over "rbx"
        var hover4 = _provider.ProvideHover(line, 19);
        hover4.Should().NotBeNull();
        hover4!.Contents.Should().Contain("rbx");
    }

    [Fact]
    public void ProvideHover_HoverRange_CoversEntireToken()
    {
        // Arrange
        var line = "mov rax, rbx";
        var position = 1; // Inside "mov"

        // Act
        var hover = _provider.ProvideHover(line, position);

        // Assert
        hover.Should().NotBeNull();
        hover!.Range.Should().NotBeNull();
        hover.Range!.StartChar.Should().Be(0);
        hover.Range.EndChar.Should().Be(3); // "mov" is 3 characters
    }

    #endregion
}
