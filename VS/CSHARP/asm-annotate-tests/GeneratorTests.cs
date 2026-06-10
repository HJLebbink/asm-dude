// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmAnnotate.Tests;

using asm_annotate;

using AsmTools;

using System.Collections.Generic;

using Xunit;

/// <summary>
/// Tests for the md→txt stage (<see cref="SignatureGenerator"/>, the `gen-signatures` command — formerly
/// the standalone intel-doc-2-data tool): turning the wiki's HTML opcode tables into AsmDude signature
/// rows. These exercise the real production methods — the table parser, the operand cleanup, the
/// description abbreviation, and the column-detection + DNF-arch end-to-end path.
/// </summary>
public class GeneratorTests
{
    [Fact]
    public void ParseTable_SplitsRowsAndCells_StrippingSupAndBoldAndColspan()
    {
        var rows = SignatureGenerator.Parse_Table(
            "<tr><td><b>Opcode</b></td><td>Instruction</td></tr>" +
            "<tr><td>imm32<sup>1</sup></td><td colspan=\"2\">VADDPS</td></tr>");

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "Opcode", "Instruction" }, rows[0]);
        Assert.Equal(new[] { "imm32", "VADDPS" }, rows[1]); // <sup>1</sup> dropped, colspan attr ignored
    }

    [Theory]
    [InlineData("XMM1,XMM2,XMM3/M128", "XMM,XMM,XMM/M128")] // strip the register index digits
    [InlineData("TMM1,TMM2,TMM3", "TMM,TMM,TMM")]           // AMX tiles incl. TMM3
    [InlineData("R32A,R32B", "R32,R32")]
    [InlineData("<XMM0>", "XMM_ZERO")]                       // implicit XMM operand -> recognised token
    [InlineData("<XMM0-7>", "XMM_ZERO")]
    [InlineData("<EAX>", "EAX")]                             // other implicit register: just drop brackets
    [InlineData("R32,IMM16", "R32,IMM16")]                   // IMM16 must NOT be stripped to IMM
    public void CleanupParameters_NormalisesOperands(string input, string expected)
    {
        Assert.Equal(expected, SignatureGenerator.Cleanup_Parameters(input));
    }

    [Theory]
    [InlineData("Add Packed Double Precision Floating-Point Values", "Add Packed DP FP Values")]
    [InlineData("Add Scalar Single Precision Floating-Point Values", "Add Scalar SP FP Values")]
    [InlineData("single-precision", "SP")] // hyphenated form too
    public void AbbreviateDescription_ShrinksPrecisionPhrases(string input, string expected)
    {
        Assert.Equal(expected, SignatureGenerator.AbbreviateDescription(input));
    }

    [Fact]
    public void ParseParameters_ExtractsMnemonicAndCleanedOperands()
    {
        var (mnemonic, parameters, _) =
            SignatureGenerator.Parse_Parameters("VEX.128.0F.WIG 58 /r VADDPS xmm1, xmm2, xmm3/m128");

        Assert.Equal(Mnemonic.VADDPS, mnemonic);
        Assert.Equal("XMM,XMM,XMM/M128", parameters);
    }

    [Fact]
    public void ToSignature_EndToEnd_DetectsColumns_BuildsDnfArch_AbbreviatesDescription()
    {
        // A realistic 5-column 2026-SDM opcode table: header[0] holds "Instruction", header[3] holds
        // "CPUID" -> mnemonic=col0, arch=col3, description=col4.
        var table = new List<IList<string>>
        {
            new List<string> { "Opcode/ Instruction", "Op / En", "64/32 bit Mode Support", "CPUID Feature Flag", "Description" },
            new List<string>
            {
                "EVEX.128.0F.W0 58 /r VADDPS xmm1 {k1}{z}, xmm2, xmm3/m128/m32bcst",
                "C",
                "V/V",
                "(AVX512VL AND AVX512F) OR AVX10.1",
                "Add packed single precision floating-point values from xmm3/m128/m32bcst to xmm2 and store result in xmm1 with writemask k1.",
            },
        };

        var sigs = SignatureGenerator.To_Signature(table);

        Assert.Single(sigs);
        var s = sigs[0];
        Assert.Equal(Mnemonic.VADDPS, s.mnemonic);
        Assert.Equal("XMM{K}{Z},XMM,XMM/M128/M32BCST", s.parameters);
        // The "(VL AND F) OR AVX10.1" cell becomes the DNF "(VL AND F) OR AVX10".
        Assert.Equal("AVX512_VL+AVX512_F,AVX10", ArchTools.ToStringDnf(s.archs));
        // "single precision floating-point" abbreviated.
        Assert.Equal(
            "Add packed SP FP values from xmm3/m128/m32bcst to xmm2 and store result in xmm1 with writemask k1.",
            s.description);
    }

    [Fact]
    public void ToSignature_UnrecognisedHeaderLayout_ReturnsEmpty_NoCrash()
    {
        // A 1-column "table" (a malformed/continuation fragment) must be skipped, not throw.
        var table = new List<IList<string>> { new List<string> { "Opcode" } };
        Assert.Empty(SignatureGenerator.To_Signature(table));
    }
}
