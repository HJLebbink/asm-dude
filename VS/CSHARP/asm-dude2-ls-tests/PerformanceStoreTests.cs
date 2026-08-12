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
/// Tests for <see cref="PerformanceStore"/> — loads CPU microarchitecture performance data
/// (latency, throughput, µOps, ports) from the uops.info-derived TSV files.
/// </summary>
public class PerformanceStoreTests
{
    private readonly string _performancePath;

    public PerformanceStoreTests()
    {
        var testDir = Directory.GetCurrentDirectory();
        var resourceDir = Path.Combine(testDir, "..", "..", "..", "..", "asm-dude2-ls-lib", "Resources");
        this._performancePath = Path.Combine(resourceDir, "Performance");
    }

    private PerformanceStore Store(params MicroArch[] arches)
    {
        var options = new AsmLanguageServerOptions { PerformanceInfo_On = true };
        foreach (var a in arches)
        {
            switch (a)
            {
                case MicroArch.SandyBridge: options.PerformanceInfo_SandyBridge_On = true; break;
                case MicroArch.Haswell: options.PerformanceInfo_Haswell_On = true; break;
                case MicroArch.Skylake: options.PerformanceInfo_Skylake_On = true; break;
                case MicroArch.Icelake: options.PerformanceInfo_Icelake_On = true; break;
                case MicroArch.Zen4: options.PerformanceInfo_Zen4_On = true; break;
                default: throw new System.ArgumentException($"test helper does not enable {a}", nameof(arches));
            }
        }

        return new PerformanceStore(this._performancePath, options);
    }

    #region Constructor / enable-disable

    [Fact]
    public void Constructor_WithPerformanceOn_LoadsData()
    {
        var store = this.Store(MicroArch.SandyBridge);

        // MOV is present on every microarchitecture; the store must return real rows.
        store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge).Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithPerformanceOff_LoadsNothing()
    {
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = false,
            PerformanceInfo_SandyBridge_On = true,
        };
        var store = new PerformanceStore(this._performancePath, options);

        store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge)
            .Should().BeEmpty("nothing should be loaded when PerformanceInfo_On is false");
    }

    [Fact]
    public void GetPerformance_DisabledArchitecture_IsNotReturned()
    {
        // Only SandyBridge enabled; querying Haswell must yield nothing.
        var store = this.Store(MicroArch.SandyBridge);

        store.GetPerformance(Mnemonic.MOV, MicroArch.Haswell)
            .Should().BeEmpty("Haswell data was not loaded");
    }

    #endregion

    #region GetPerformance content

    [Fact]
    public void GetPerformance_TagsItemsWithTheRequestedArchitecture()
    {
        var store = this.Store(MicroArch.SandyBridge);

        var items = store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge).ToList();

        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i.microArch_ == MicroArch.SandyBridge);
        items.Should().OnlyContain(i => i.instr_ == Mnemonic.MOV);
    }

    [Fact]
    public void GetPerformance_NoneMnemonic_ReturnsEmpty()
    {
        var store = this.Store(MicroArch.SandyBridge);

        store.GetPerformance(Mnemonic.NONE, MicroArch.SandyBridge).Should().BeEmpty();
    }

    [Fact]
    public void GetPerformance_SseInstruction_HasData()
    {
        var store = this.Store(MicroArch.Haswell);

        store.GetPerformance(Mnemonic.ADDPS, MicroArch.Haswell)
            .Should().NotBeEmpty("ADDPS is an SSE instruction present on Haswell");
    }

    /// <summary>
    /// Column-order regression guard: the register-register ADD form has a documented latency of
    /// 1 cycle. If <see cref="PerformanceStore"/> ever parses the wrong TSV column into
    /// <c>latency_</c>, this assertion fails.
    /// </summary>
    [Fact]
    public void GetPerformance_AddRegReg_HasLatencyOne_OnHaswell()
    {
        var store = this.Store(MicroArch.Haswell);

        var regReg = store.GetPerformance(Mnemonic.ADD, MicroArch.Haswell)
            .FirstOrDefault(i => i.args_ == "R32, R32");

        regReg.instr_.Should().Be(Mnemonic.ADD, "an 'ADD R32, R32' row must exist for Haswell");
        regReg.latency_.Should().Be("1");
        regReg.throughput_.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// uops.info names CALL/RET only as <c>CALL_NEAR</c>/<c>RET_NEAR</c> (there is no plain iclass), so
    /// without the importer's mnemonic-suffix normalization these control-flow instructions would have
    /// no performance data at all. Guards that regression.
    /// </summary>
    [Fact]
    public void GetPerformance_CallAndRet_AreRecovered_OnHaswell()
    {
        var store = this.Store(MicroArch.Haswell);

        store.GetPerformance(Mnemonic.CALL, MicroArch.Haswell)
            .Should().NotBeEmpty("CALL (uops iclass CALL_NEAR) must be normalized to CALL");
        store.GetPerformance(Mnemonic.RET, MicroArch.Haswell)
            .Should().NotBeEmpty("RET (uops iclass RET_NEAR) must be normalized to RET");
    }

    #endregion

    #region New microarchitectures (uops.info coverage)

    [Theory]
    [InlineData(MicroArch.Skylake)]
    [InlineData(MicroArch.Icelake)] // formerly a dead enum entry — now backed by uops data
    [InlineData(MicroArch.Zen4)]    // first AMD coverage in asmdude
    public void GetPerformance_ModernArch_HasAddAndMovData(MicroArch arch)
    {
        var store = this.Store(arch);

        store.GetPerformance(Mnemonic.ADD, arch).Should().NotBeEmpty($"ADD should have data on {arch}");
        store.GetPerformance(Mnemonic.MOV, arch).Should().NotBeEmpty($"MOV should have data on {arch}");
    }

    [Fact]
    public void GetPerformance_AcrossTwoArches_ReturnsBoth()
    {
        var store = this.Store(MicroArch.Haswell, MicroArch.Skylake);

        var archs = store.GetPerformance(Mnemonic.ADD, MicroArch.Haswell | MicroArch.Skylake)
            .Select(i => i.microArch_)
            .Distinct()
            .ToList();

        archs.Should().Contain(MicroArch.Haswell);
        archs.Should().Contain(MicroArch.Skylake);
    }

    #endregion

    #region Bundled data integrity

    // Every row of every bundled TSV must carry a mnemonic the Mnemonic enum recognizes; the loader
    // silently drops unknown rows (with a runtime warning), so an unrecognized name means that timing
    // data never reaches the user. This guards the asm-annotate perf-uops importer's normalization:
    // uops.info iclass names carry disambiguation suffixes (PCMPESTRI64, MOV_CR, VPEXTRW_C5, ...) that
    // must be mapped to real mnemonics at TSV generation time.
    [Fact]
    public void BundledTsvFiles_AllMnemonicsAreKnown()
    {
        var unknown = new SortedSet<string>();
        foreach (string file in Directory.EnumerateFiles(this._performancePath, "*.tsv"))
        {
            foreach (string line in File.ReadLines(file))
            {
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                {
                    continue;
                }

                int tab = line.IndexOf('\t');
                string name = (tab < 0) ? line : line[..tab];
                if (AsmTools.AsmSourceTools.ParseMnemonic(name, false) == Mnemonic.NONE)
                {
                    unknown.Add(name);
                }
            }
        }

        unknown.Should().BeEmpty(
            "every bundled TSV row must parse to a known mnemonic, or its data is silently dropped at load time; unknown: {0}",
            string.Join(", ", unknown));
    }

    #endregion
}
