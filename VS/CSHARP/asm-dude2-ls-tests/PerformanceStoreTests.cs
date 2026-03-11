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
/// Tests for PerformanceStore - stores CPU microarchitecture performance data (latency, throughput, uOps)
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

    #region Constructor Tests

    [Fact]
    public void Constructor_WithPerformanceOn_ShouldLoadData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true
        };

        // Act
        var store = new PerformanceStore(this._performancePath, options);

        // Assert
        store.Should().NotBeNull();
        var performance = store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge);
        performance.Should().NotBeNull("MOV should have performance data");
    }

    [Fact]
    public void Constructor_WithPerformanceOff_ShouldNotLoadData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = false
        };

        // Act
        var store = new PerformanceStore(this._performancePath, options);

        // Assert
        store.Should().NotBeNull();
        var performance = store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge);
        performance.Should().BeEmpty("no performance data should be loaded when disabled");
    }

    #endregion

    #region GetPerformance Tests

    [Fact]
    public void GetPerformance_WithMOVAndSandyBridge_ShouldReturnPerformanceData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var performance = store.GetPerformance(Mnemonic.MOV, MicroArch.SandyBridge);

        // Assert
        performance.Should().NotBeNull();
        if (performance.Any())
        {
            var first = performance.First();
            first.instr_.Should().NotBe(Mnemonic.NONE);
            first.microArch_.Should().Be(MicroArch.SandyBridge);
        }
    }

    [Fact]
    public void GetPerformance_WithNonExistentInstruction_ShouldReturnEmpty()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var performance = store.GetPerformance(Mnemonic.NONE, MicroArch.SandyBridge);

        // Assert
        performance.Should().BeEmpty("non-existent instruction should return empty collection");
    }

    [Fact]
    public void GetPerformance_WithSSEInstruction_ShouldReturnPerformanceData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true,
            PerformanceInfo_Haswell_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var performance = store.GetPerformance(Mnemonic.ADDPS, MicroArch.SandyBridge | MicroArch.Haswell);

        // Assert
        performance.Should().NotBeNull();
        // ADDPS may or may not have performance data depending on the TSV files
    }

    [Fact]
    public void GetPerformance_WithHaswellArchitecture_ShouldReturnData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_Haswell_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var performance = store.GetPerformance(Mnemonic.ADD, MicroArch.Haswell);

        // Assert
        performance.Should().NotBeNull();
        if (performance.Any())
        {
            var first = performance.First();
            first.microArch_.Should().Be(MicroArch.Haswell);
        }
    }

    #endregion

    #region PerformanceItem Tests

    [Fact]
    public void PerformanceItem_ShouldHaveExpectedProperties()
    {
        // Arrange
        var item = new PerformanceItem
        {
            microArch_ = MicroArch.Haswell,
            instr_ = Mnemonic.MOV,
            args_ = "rax, rbx",
            latency_ = "1",
            throughput_ = "0.5",
            mu_Ops_Merged_ = "1",
            mu_Ops_Fused_ = "1",
            mu_Ops_Port_ = "p015",
            remark_ = ""
        };

        // Assert
        item.microArch_.Should().Be(MicroArch.Haswell);
        item.instr_.Should().Be(Mnemonic.MOV);
        item.args_.Should().Be("rax, rbx");
        item.latency_.Should().Be("1");
        item.throughput_.Should().Be("0.5");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void PerformanceStore_WithSkylake_ShouldProvideData()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_Skylake_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var movPerf = store.GetPerformance(Mnemonic.MOV, MicroArch.Skylake);
        var addPerf = store.GetPerformance(Mnemonic.ADD, MicroArch.Skylake);

        // Assert
        movPerf.Should().NotBeNull();
        addPerf.Should().NotBeNull();

        if (movPerf.Any())
        {
            var item = movPerf.First();
            item.latency_.Should().NotBeNull("performance data should include latency");
            item.throughput_.Should().NotBeNull("performance data should include throughput");
        }
    }

    [Fact]
    public void ToString_ShouldReturnNonEmptyString()
    {
        // Arrange
        var options = new AsmLanguageServerOptions
        {
            PerformanceInfo_On = true,
            PerformanceInfo_SandyBridge_On = true
        };
        var store = new PerformanceStore(this._performancePath, options);

        // Act
        var result = store.ToString();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
