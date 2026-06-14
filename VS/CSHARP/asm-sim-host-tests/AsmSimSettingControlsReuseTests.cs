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

namespace AsmSim.Host.Tests;

using FluentAssertions;

using System;

using Xunit;

/// <summary>
/// Proves the incremental SETTING actually controls reuse end-to-end — i.e. that
/// <see cref="AsmSimulator.ApplySettings"/> (the path the <c>AsmSim_Incremental</c> setting flows through:
/// settings.json → server → ApplySettings) flips the runtime switch, and that the switch decides whether a
/// newline edit reuses or recomputes. This is the layer the 2026-06-14 live regression hid in: the engine
/// reuse logic was correct, but the setting never reached it (so nothing reused). These run the REAL dispatch
/// without forcing the flag (<see cref="AsmSimulator.SimulateRespectingFlagForTest"/>).
/// </summary>
public class AsmSimSettingControlsReuseTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    [Fact]
    public void ApplySettings_TogglesTheRuntimeSwitch()
    {
        using var sim = new AsmSimulator();
        sim.ApplySettings("component", "accept", incremental: true);
        sim.IncrementalEnabledForTest.Should().BeTrue();
        sim.ApplySettings("component", "accept", incremental: false);
        sim.IncrementalEnabledForTest.Should().BeFalse();
    }

    [Fact]
    public void IncrementalEnabledViaSetting_NewlineIsReused_NotRecomputed()
    {
        using var sim = new AsmSimulator();
        sim.ApplySettings("component", "accept", incremental: true); // the setting → ApplySettings path
        var uri = new Uri("file:///setting_on.asm");

        sim.SimulateRespectingFlagForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"]); // cold baseline
        string rbxBefore = sim.GetCachedEntry(uri)!.lineStringsWriteLabels[1]!; // 'mov rbx, 0x20'

        // Add a newline after line 0 — 'mov rbx' shifts from line 1 to line 2.
        sim.SimulateRespectingFlagForTest(uri, ["mov rax, 0x10", "", "mov rbx, 0x20", "add rax, rbx"]);
        string rbxAfter = sim.GetCachedEntry(uri)!.lineStringsWriteLabels[2]!;

        ReferenceEquals(rbxBefore, rbxAfter).Should().BeTrue(
            "with the setting ON, a newline must reuse the cached label (same instance), not re-solve it");
    }

    [Fact]
    public void IncrementalDisabledViaSetting_NewlineIsRecomputed()
    {
        using var sim = new AsmSimulator();
        sim.ApplySettings("component", "accept", incremental: false); // setting OFF
        var uri = new Uri("file:///setting_off.asm");

        sim.SimulateRespectingFlagForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"]);
        string rbxBefore = sim.GetCachedEntry(uri)!.lineStringsWriteLabels[1]!;

        sim.SimulateRespectingFlagForTest(uri, ["mov rax, 0x10", "", "mov rbx, 0x20", "add rax, rbx"]);
        string rbxAfter = sim.GetCachedEntry(uri)!.lineStringsWriteLabels[2]!;

        // Same VALUE (rbx=0x20) but a fresh instance, because the line was re-solved.
        ReferenceEquals(rbxBefore, rbxAfter).Should().BeFalse(
            "with the setting OFF, a newline triggers a full re-simulation (new string instance)");
        rbxAfter.Should().Be(rbxBefore, "the recomputed value is of course identical");
    }
}
