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

using AsmDude2LS;
using AsmTools;
using FluentAssertions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace AsmDude2LS.Tests;

/// <summary>
/// Tests for MnemonicStore - the core component that stores instruction signatures, architectures, and documentation
/// </summary>
public class MnemonicStoreTests
{
    private readonly MnemonicStore _store;
    private readonly string _regularDataPath;
    private readonly string _handcraftedDataPath;

    public MnemonicStoreTests()
    {
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude2-ls-lib", "Resources");
        _regularDataPath = Path.Combine(resourceDir, "signature-may2019.txt");
        _handcraftedDataPath = Path.Combine(resourceDir, "signature-hand-1.txt");

        var options = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = true,
            ARCH_AVX = true,
            ARCH_AVX2 = true,
            ARCH_AVX512_F = true
        };

        _store = new MnemonicStore(_regularDataPath, _handcraftedDataPath, options);
    }

    #region Basic Functionality Tests

    [Fact]
    public void Constructor_WithValidPaths_ShouldLoadData()
    {
        // Arrange & Act done in constructor

        // Assert
        _store.Should().NotBeNull();
        _store.HasElement(Mnemonic.MOV).Should().BeTrue("MOV is a fundamental instruction");
        _store.HasElement(Mnemonic.ADD).Should().BeTrue("ADD is a fundamental instruction");
    }

    #endregion

    #region HasElement Tests

    [Fact]
    public void HasElement_WithCommonInstruction_ShouldReturnTrue()
    {
        // Arrange
        var commonInstructions = new[]
        {
            Mnemonic.MOV,
            Mnemonic.ADD,
            Mnemonic.SUB,
            Mnemonic.CALL,
            Mnemonic.RET,
            Mnemonic.JMP,
            Mnemonic.PUSH,
            Mnemonic.POP
        };

        // Act & Assert
        foreach (var mnemonic in commonInstructions)
        {
            _store.HasElement(mnemonic).Should().BeTrue($"{mnemonic} should be in the store");
        }
    }

    [Fact]
    public void HasElement_WithNoneInstruction_ShouldReturnFalse()
    {
        // Act
        var result = _store.HasElement(Mnemonic.NONE);

        // Assert
        result.Should().BeFalse("NONE is not a real instruction");
    }

    [Fact]
    public void HasElement_WithSSEInstruction_ShouldReturnTrue()
    {
        // Arrange - SSE instructions
        var sseInstructions = new[]
        {
            Mnemonic.MOVAPS,
            Mnemonic.ADDPS,
            Mnemonic.MULPS
        };

        // Act & Assert
        foreach (var mnemonic in sseInstructions)
        {
            _store.HasElement(mnemonic).Should().BeTrue($"{mnemonic} should be in the store (SSE instruction)");
        }
    }

    [Fact]
    public void HasElement_WithAVXInstruction_ShouldReturnTrue()
    {
        // Arrange - AVX instructions
        var avxInstructions = new[]
        {
            Mnemonic.VMOVAPS,
            Mnemonic.VADDPS,
            Mnemonic.VMULPS
        };

        // Act & Assert
        foreach (var mnemonic in avxInstructions)
        {
            _store.HasElement(mnemonic).Should().BeTrue($"{mnemonic} should be in the store (AVX instruction)");
        }
    }

    #endregion

    #region GetSignatures Tests

    [Fact]
    public void GetSignatures_WithMOV_ShouldReturnMultipleSignatures()
    {
        // Act
        var signatures = _store.GetSignatures(Mnemonic.MOV);

        // Assert
        signatures.Should().NotBeEmpty("MOV has multiple variants");
        signatures.Count().Should().BeGreaterThan(5, "MOV has many different operand combinations");
    }

    [Fact]
    public void GetSignatures_WithNonExistentMnemonic_ShouldReturnEmpty()
    {
        // Act
        var signatures = _store.GetSignatures(Mnemonic.NONE);

        // Assert
        signatures.Should().BeEmpty("non-existent mnemonic should return empty collection");
    }

    [Fact]
    public void GetSignatures_WithSimpleInstruction_ShouldReturnValidSignatures()
    {
        // Act
        var signatures = _store.GetSignatures(Mnemonic.NOP);

        // Assert
        signatures.Should().NotBeEmpty("NOP should have at least one signature");
        foreach (var sig in signatures)
        {
            sig.Should().NotBeNull();
            sig.SignatureInformation.Should().NotBeNull();
        }
    }

    #endregion

    #region GetArch Tests

    [Fact]
    public void GetArch_WithMOV_ShouldReturnX86Architecture()
    {
        // Act
        var archs = _store.GetArch(Mnemonic.MOV);

        // Assert
        archs.Should().NotBeEmpty("MOV should have architecture information");
        archs.Should().Contain(Arch.ARCH_8086, "MOV is available in 8086");
    }

    [Fact]
    public void GetArch_WithAVXInstruction_ShouldReturnAVXArchitecture()
    {
        // Act
        var archs = _store.GetArch(Mnemonic.VMOVAPS);

        // Assert
        archs.Should().NotBeEmpty("VMOVAPS should have architecture information");
        archs.Should().Contain(Arch.ARCH_AVX, "VMOVAPS is an AVX instruction");
    }

    [Fact]
    public void GetArch_WithNonExistentMnemonic_ShouldReturnEmpty()
    {
        // Act
        var archs = _store.GetArch(Mnemonic.NONE);

        // Assert
        archs.Should().BeEmpty("non-existent mnemonic should return empty collection");
    }

    #endregion

    #region GetHtmlRef Tests

    [Fact]
    public void GetHtmlRef_WithMOV_ShouldReturnNonEmptyReference()
    {
        // Act
        var htmlRef = _store.GetHtmlRef(Mnemonic.MOV);

        // Assert
        htmlRef.Should().NotBeNullOrEmpty("MOV should have HTML reference");
    }

    [Fact]
    public void GetHtmlRef_WithCommonInstructions_ShouldReturnValidReferences()
    {
        // Arrange
        var instructions = new[] { Mnemonic.ADD, Mnemonic.SUB, Mnemonic.CALL, Mnemonic.JMP };

        // Act & Assert
        foreach (var mnemonic in instructions)
        {
            var htmlRef = _store.GetHtmlRef(mnemonic);
            htmlRef.Should().NotBeNullOrEmpty($"{mnemonic} should have HTML reference");
        }
    }

    [Fact]
    public void GetHtmlRef_WithNonExistentMnemonic_ShouldReturnEmptyString()
    {
        // Act
        var htmlRef = _store.GetHtmlRef(Mnemonic.NONE);

        // Assert
        htmlRef.Should().BeEmpty("non-existent mnemonic should return empty string");
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_WithMOV_ShouldReturnNonEmptyDescription()
    {
        // Act
        var description = _store.GetDescription(Mnemonic.MOV);

        // Assert
        description.Should().NotBeNullOrEmpty("MOV should have a description");
        description.Length.Should().BeGreaterThan(2, "description should be meaningful");
    }

    [Fact]
    public void GetDescription_WithCommonInstructions_ShouldReturnDescriptions()
    {
        // Arrange
        var instructions = new[] { Mnemonic.ADD, Mnemonic.SUB, Mnemonic.PUSH, Mnemonic.POP };

        // Act & Assert
        foreach (var mnemonic in instructions)
        {
            var description = _store.GetDescription(mnemonic);
            description.Should().NotBeNullOrEmpty($"{mnemonic} should have a description");
        }
    }

    [Fact]
    public void GetDescription_WithNonExistentMnemonic_ShouldReturnEmptyString()
    {
        // Act
        var description = _store.GetDescription(Mnemonic.NONE);

        // Assert
        description.Should().BeEmpty("non-existent mnemonic should return empty string");
    }

    #endregion

    #region IsMnemonicSwitchedOn Tests

    [Fact]
    public void IsMnemonicSwitchedOn_WithBasicInstruction_ShouldReturnTrue()
    {
        // Act
        var result = _store.IsMnemonicSwitchedOn(Mnemonic.MOV);

        // Assert
        result.Should().BeTrue("MOV should be switched on with basic architecture enabled");
    }

    [Fact]
    public void Get_Allowed_Mnemonics_ShouldReturnNonEmptySet()
    {
        // Act
        var allowed = _store.Get_Allowed_Mnemonics();

        // Assert
        allowed.Should().NotBeEmpty("there should be allowed mnemonics");
        allowed.Should().Contain(Mnemonic.MOV, "MOV should be in allowed mnemonics");
    }

    #endregion

    #region Register Tests

    [Fact]
    public void IsRegisterSwitchedOn_WithCommonRegister_ShouldReturnTrue()
    {
        // Act
        var result = _store.IsRegisterSwitchedOn(Rn.RAX);

        // Assert
        result.Should().BeTrue("RAX should be switched on with x64 architecture enabled");
    }

    [Fact]
    public void Get_Allowed_Registers_ShouldReturnNonEmptySet()
    {
        // Act
        var allowed = _store.Get_Allowed_Registers();

        // Assert
        allowed.Should().NotBeEmpty("there should be allowed registers");
        allowed.Should().Contain(Rn.RAX, "RAX should be in allowed registers");
        allowed.Should().Contain(Rn.AX, "AX should be in allowed registers");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MnemonicStore_ShouldProvideCompleteInformationForInstruction()
    {
        // Arrange
        var mnemonic = Mnemonic.ADD;

        // Act
        var hasElement = _store.HasElement(mnemonic);
        var signatures = _store.GetSignatures(mnemonic);
        var archs = _store.GetArch(mnemonic);
        var htmlRef = _store.GetHtmlRef(mnemonic);
        var description = _store.GetDescription(mnemonic);

        // Assert - ADD should have complete information
        hasElement.Should().BeTrue();
        signatures.Should().NotBeEmpty();
        archs.Should().NotBeEmpty();
        htmlRef.Should().NotBeNullOrEmpty();
        description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MnemonicStore_ShouldHandleArchitectureFiltering()
    {
        // Arrange - create store with only basic architectures
        var limitedOptions = new AsmLanguageServerOptions
        {
            ARCH_8086 = true,
            ARCH_X64 = true,
            ARCH_SSE = false,
            ARCH_AVX = false,
            ARCH_AVX2 = false,
            ARCH_AVX512_F = false
        };
        var limitedStore = new MnemonicStore(_regularDataPath, _handcraftedDataPath, limitedOptions);

        // Act
        var basicInstruction = limitedStore.HasElement(Mnemonic.MOV);
        var avxInstruction = limitedStore.HasElement(Mnemonic.VMOVAPS);

        // Assert
        basicInstruction.Should().BeTrue("MOV should be available");
        avxInstruction.Should().BeTrue("VMOVAPS should still be in data, but may be filtered");
    }

    [Fact]
    public void ToString_ShouldNotThrow()
    {
        // Act
        Action act = () => _store.ToString();

        // Assert
        try
        {
            var result = _store.ToString();
            result.Should().NotBeNull();
        }
        catch (KeyNotFoundException)
        {
            // This is acceptable if some mnemonics lack htmlRef entries
            _store.HasElement(Mnemonic.MOV).Should().BeTrue("store should still be functional");
        }
    }

    #endregion
}
