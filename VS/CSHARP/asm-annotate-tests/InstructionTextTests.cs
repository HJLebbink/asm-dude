// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmAnnotate.Tests;

using Xunit;

/// <summary>
/// Unit tests for asm-annotate's deterministic text/domain heuristics — the title/mnemonic parsing
/// and hyphenation rules that drive PDF→Markdown extraction. These are the trickiest, most
/// regression-prone parts of the tool and had no tests before. They run on real production methods
/// (ContentPile / TextCleaner) with no PDF needed.
/// </summary>
public class InstructionTextTests
{
    [Theory]
    [InlineData("VADDPS", true)]
    [InlineData("MOV", true)]
    [InlineData("VFMADD132PH", true)]
    [InlineData("Add With Carry", false)]              // prose, has spaces
    [InlineData("ASCII Adjust After Addition", false)] // prose
    public void LooksLikeMnemonic_DistinguishesMnemonicsFromProse(string token, bool expected)
    {
        Assert.Equal(expected, ContentPile.LooksLikeMnemonic(token));
    }

    [Fact]
    public void ExpandCompactMnemonic_LeavesPlainMnemonicUnchanged()
    {
        Assert.Equal("VADDPS", ContentPile.ExpandCompactMnemonic("VADDPS"));
    }

    [Fact]
    public void ExpandCompactMnemonic_ExpandsCommaBracketShorthand()
    {
        // "[,N]" and "[132,213,231]" are alternative-sets that multiply out (cartesian product).
        Assert.Equal(
            "VFMADD132PH/VFMADD213PH/VFMADD231PH/VFNMADD132PH/VFNMADD213PH/VFNMADD231PH",
            ContentPile.ExpandCompactMnemonic("VF[,N]MADD[132,213,231]PH"));
    }

    [Fact]
    public void ExpandCompactMnemonic_KeepsCommaLessBracketLiteral()
    {
        // A bracket group WITHOUT a comma is part of the real name, not a set.
        Assert.Equal("GETSEC[SENTER]", ContentPile.ExpandCompactMnemonic("GETSEC[SENTER]"));
    }

    [Theory]
    [InlineData("AAA-ASCII Adjust", 3)]
    [InlineData("AAA — ASCII Adjust", 4)] // em-dash after "AAA "
    [InlineData("NODASH", -1)]
    public void IndexOfTitleSeparator_FindsFirstDashLikeChar(string content, int expected)
    {
        Assert.Equal(expected, ContentPile.IndexOfTitleSeparator(content));
    }

    [Theory]
    [InlineData("Integer Subtraction With Borrow", "Integer Subtraction with Borrow")]
    [InlineData("Add With Carry", "Add with Carry")]
    [InlineData("Bytes With and Without Saturation", "Bytes with and without Saturation")]
    public void LowercaseTitleConjunctions_LowercasesMidTitleWithWithout(string input, string expected)
    {
        Assert.Equal(expected, ContentPile.LowercaseTitleConjunctions(input));
    }

    [Fact]
    public void CleanupHyphenation_JoinsCompoundKeepingHyphen()
    {
        // A line-ending hyphen in a compound is pulled up but the hyphen is kept.
        Assert.Equal("floating-point", TextCleaner.CleanupHyphenation("floating-\npoint"));
    }

    [Fact]
    public void CleanupHyphenation_PreservesElisionSpace()
    {
        // "16- or 32-bit" is an elision, not a word break — the space after the hyphen must remain.
        Assert.Equal("16- or 32-bit", TextCleaner.CleanupHyphenation("16- or 32-bit"));
    }
}
