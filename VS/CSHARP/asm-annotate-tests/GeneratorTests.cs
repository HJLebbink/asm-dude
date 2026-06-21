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
    public void ParseParameters_FusedImplicitOperand_ParametersAndDescriptionAgreeOnCount()
    {
        // ENCODEKEY256's SDM cell fuses the implicit operand with no comma ("r32, r32<XMM0-6>"). The
        // abbreviated parameters (<...> -> XMM_ZERO) and the kept-notation description must still have the
        // SAME operand count, else MnemonicStore rejects the row (IndexOutOfRange at load).
        var (mnemonic, parameters, descriptions) =
            SignatureGenerator.Parse_Parameters("F3 0F 38 FB 11:rrr:bbb ENCODEKEY256 r32, r32<XMM0-6>");

        Assert.Equal(Mnemonic.ENCODEKEY256, mnemonic);
        Assert.Equal("R32,R32,XMM_ZERO", parameters);
        int operandsInParameters = parameters.Split(',').Length;
        int operandsInDescription = descriptions["ENCODEKEY256".Length..].Trim().Split(',').Length;
        Assert.Equal(3, operandsInParameters);
        Assert.Equal(operandsInParameters, operandsInDescription);
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

    [Theory]
    // The KNOWN stage-1 PDF-extraction defect classes (see KNOWN-DATA-ISSUES.md), fixed at the source as the
    // .md instruction cell is written — case-preserving (the wiki keeps lowercase). Input is the raw cell text
    // exactly as the extractor renders it (opcode prefix, register numbers, decoration spaces, footnote forms).
    [InlineData("POPCNT r16, r/m16RM", "POPCNT r16, r/m16")]                                   // Op/En code bled in
    [InlineData("LAR r16, r16/m161", "LAR r16, r16/m16")]                                      // footnote digit
    [InlineData("VEX.256.66.0F.WIG DB /r VPAND ymm1, ymm2, ymm3/.m256", "VEX.256.66.0F.WIG DB /r VPAND ymm1, ymm2, ymm3/m256")] // stray dot
    [InlineData("EVEX.128.F3.0F.W0 6F /r VMOVDQU32 xmm1 {k1}{z}, xmm2/mm128", "EVEX.128.F3.0F.W0 6F /r VMOVDQU32 xmm1 {k1}{z}, xmm2/m128")] // doubled m
    [InlineData("VREDUCESD xmm1 {k1}{z}, xmm2, xmm3/m64{sae}, imm8/r", "VREDUCESD xmm1 {k1}{z}, xmm2, xmm3/m64{sae}, imm8")] // stray /r
    [InlineData("POPCNT r64, r/m64", "POPCNT r64, r/m64")]                                     // already clean -> untouched
    [InlineData("POPCNT on r/m16", "POPCNT on r/m16")]                                         // description (no comma) -> untouched
    [InlineData("LAR r32, r32/m16 1<sup>1</sup>", "LAR r32, r32/m16<sup>1</sup>")]             // inline footnote digit removed, <sup> kept
    [InlineData("LAR r16, r16/m16<sup>1</sup>", "LAR r16, r16/m16<sup>1</sup>")]               // clean operand + footnote -> untouched
    [InlineData("V4FMADDPS zmm1 {k1}{z}, zmm2+3, m128", "V4FMADDPS zmm1 {k1}{z}, zmm2+3, m128")] // register-block "+3" preserved
    public void RepairInstructionCell_FixesStage1Garble(string input, string expected)
    {
        Assert.Equal(expected, ContentPile.RepairInstructionCell(input));
    }

    [Theory]
    // Description cells must NEVER be rewritten, even though they contain commas and short-word mnemonics
    // (IN, AND, OR). The mnemonic-must-be-preceded-only-by-opcode-tokens guard rejects them; without it the
    // trailing "." of "…in xmm1." was being stripped (it parsed as "IN xmm1").
    [InlineData("Perform one round of AES, using xmm2 with xmm3/m128; store the result in xmm1.")]
    [InlineData("BLENDVPD xmm1, xmm2/m128 from mask in xmm0, store result")]
    [InlineData("Bitwise AND of ymm2, and ymm3/m256 and store result in ymm1.")]
    public void RepairInstructionCell_LeavesDescriptionsUntouched(string description)
    {
        Assert.Equal(description, ContentPile.RepairInstructionCell(description));
    }

    [Theory]
    // The implicit operand "<XMM0…>" must become a comma-SEPARATED XMM_ZERO even when the SDM fused it onto
    // the previous operand with no comma (ENCODEKEY256); rows that already had the comma must not gain a blank.
    [InlineData("R32,R32<XMM0-6>", "R32,R32,XMM_ZERO")]   // fused (ENCODEKEY256) -> comma inserted
    [InlineData("XMM2/M128,<XMM0>", "XMM/M128,XMM_ZERO")] // already comma'd (BLENDVPD) -> no double comma
    [InlineData("M384,<XMM0-7>", "M384,XMM_ZERO")]        // AESDECWIDE128KL
    public void CleanupParameters_SeparatesFusedImplicitOperand(string input, string expected)
    {
        Assert.Equal(expected, SignatureGenerator.Cleanup_Parameters(input));
    }
}
