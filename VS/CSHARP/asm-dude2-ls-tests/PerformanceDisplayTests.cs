// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

using AsmTools;

using FluentAssertions;

using Xunit;

namespace AsmDude2LS.Tests;

/// <summary>
/// Tests for <see cref="PerformanceDisplay"/> — the inlay operand-form matcher and the hover
/// timing-collapse. Both run against real <see cref="PerformanceStore"/> data (Haswell), so a broken
/// classifier or collapse key would fail here.
/// </summary>
public class PerformanceDisplayTests
{
    private readonly List<PerformanceItem> _haswellAdd;

    public PerformanceDisplayTests()
    {
        var testDir = Directory.GetCurrentDirectory();
        var perfPath = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude2-ls-lib", "Resources", "Performance");
        var options = new AsmLanguageServerOptions { PerformanceInfo_On = true, PerformanceInfo_Haswell_On = true };
        var store = new PerformanceStore(perfPath, options);
        this._haswellAdd = store.GetPerformance(Mnemonic.ADD, MicroArch.Haswell).ToList();
    }

    #region SelectBestMatch (inlay)

    [Fact]
    public void SelectBestMatch_RegReg_PicksRegisterForm()
    {
        var pick = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "rbx" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().Be("R64, R64");
    }

    [Fact]
    public void SelectBestMatch_RegMem_PicksMemoryForm()
    {
        var pick = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "[rbx]" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().Be("R64, M64");
    }

    [Fact]
    public void SelectBestMatch_RegImm_PicksImmediateForm()
    {
        var pick = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "100" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().MatchRegex("^R64, I[0-9]+$", "a register/immediate form should be chosen");
    }

    [Fact]
    public void SelectBestMatch_DistinguishesMemFromReg()
    {
        // The whole point: the memory form and the register form must resolve to different rows.
        var reg = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "rbx" });
        var mem = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "[rbx]" });

        reg!.Value.args_.Should().NotBe(mem!.Value.args_);
    }

    [Fact]
    public void SelectBestMatch_NoOperandCountMatch_FallsBackToFirst()
    {
        // ADD has only 2-operand forms; a 3-operand query cannot match, so we fall back (non-null).
        var pick = PerformanceDisplay.SelectBestMatch(this._haswellAdd, new[] { "rax", "rbx", "rcx" });

        pick.Should().NotBeNull();
        pick!.Value.Should().Be(this._haswellAdd[0]);
    }

    [Fact]
    public void SelectBestMatch_EmptyItems_ReturnsNull()
    {
        PerformanceDisplay.SelectBestMatch(new List<PerformanceItem>(), new[] { "rax" })
            .Should().BeNull();
    }

    #endregion

    #region SelectBestMatch — masked AVX-512 (EVEX)

    private static List<PerformanceItem> SkxVaddps()
    {
        var testDir = Directory.GetCurrentDirectory();
        var perfPath = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude2-ls-lib", "Resources", "Performance");
        var options = new AsmLanguageServerOptions { PerformanceInfo_On = true, PerformanceInfo_SkylakeX_On = true };
        var store = new PerformanceStore(perfPath, options);
        return store.GetPerformance(Mnemonic.VADDPS, MicroArch.SkylakeX).ToList();
    }

    [Fact]
    public void SelectBestMatch_MaskedRegForm_AlignsWriteMaskOperand()
    {
        // Line has 3 operands (mask folded into dest as {k1}); uops form is "ZMM, K, ZMM, ZMM" (4 tokens).
        var pick = PerformanceDisplay.SelectBestMatch(SkxVaddps(), new[] { "zmm0{k1}", "zmm1", "zmm2" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().Be("ZMM, K, ZMM, ZMM");
    }

    [Fact]
    public void SelectBestMatch_MaskedForm_MatchesVectorWidth()
    {
        // Width discrimination must survive masking: XMM line must not pick a ZMM form (lat 4 vs 5).
        var pick = PerformanceDisplay.SelectBestMatch(SkxVaddps(), new[] { "xmm0{k1}", "xmm1", "xmm2" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().Be("XMM, K, XMM, XMM");
    }

    [Fact]
    public void SelectBestMatch_MaskedForm_DistinguishesMemoryOperand()
    {
        var pick = PerformanceDisplay.SelectBestMatch(SkxVaddps(), new[] { "zmm0{k1}", "zmm1", "[rax]" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().MatchRegex("^ZMM, K, ZMM, M", "the masked memory form should be chosen");
    }

    [Fact]
    public void SelectBestMatch_UnmaskedZmm_PicksUnmaskedForm()
    {
        // Without a {k} decorator the line is still 3 operands; the unmasked form matches directly.
        var pick = PerformanceDisplay.SelectBestMatch(SkxVaddps(), new[] { "zmm0", "zmm1", "zmm2" });

        pick.Should().NotBeNull();
        pick!.Value.args_.Should().Be("ZMM, ZMM, ZMM");
    }

    #endregion

    #region CollapseByTiming (hover)

    [Fact]
    public void CollapseByTiming_MergesFormsWithIdenticalTiming()
    {
        var collapsed = PerformanceDisplay.CollapseByTiming(this._haswellAdd).ToList();

        this._haswellAdd.Should().HaveCountGreaterThan(1);
        collapsed.Should().HaveCountLessThan(this._haswellAdd.Count, "ADD has several forms that share timing");
        collapsed.Should().OnlyContain(g => g.formCount >= 1);
    }

    [Fact]
    public void CollapseByTiming_PreservesEveryRow_InFormCounts()
    {
        var collapsed = PerformanceDisplay.CollapseByTiming(this._haswellAdd).ToList();

        collapsed.Sum(g => g.formCount).Should().Be(this._haswellAdd.Count, "no row may be dropped, only merged");
    }

    [Fact]
    public void CollapseByTiming_RepresentativesHaveDistinctTiming()
    {
        var collapsed = PerformanceDisplay.CollapseByTiming(this._haswellAdd).ToList();

        var keys = collapsed
            .Select(g => (g.item.mu_Ops_Fused_, g.item.mu_Ops_Merged_, g.item.mu_Ops_Port_, g.item.latency_, g.item.throughput_))
            .ToList();

        keys.Should().OnlyHaveUniqueItems("each representative is a distinct timing profile");
    }

    #endregion
}
