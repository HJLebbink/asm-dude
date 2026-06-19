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
/// Guards the "don't blank the CodeLens on every keystroke" fix. <c>InvalidateAndSimulate</c> must RETAIN the
/// previous run's per-line strings so the editor keeps showing the last-known values until the new run
/// overwrites them in place. Blanking the cache on invalidate made every edit collapse all sim-state lenses to
/// nothing for ~1 s — the lens row's vertical space was reclaimed, so the code below jumped up, then back down
/// when the new values arrived.
/// </summary>
public class AsmSimCacheRetentionTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    [Fact]
    public void InvalidateAndSimulate_RetainsPreviousValues_SoCodeLensDoNotCollapse()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///retain.asm");
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 0x10", "mov rdx, rax"], Component, computeFullState: false);

        sim.GetCachedEntry(uri)!.lineStringsWriteLabels.Should().ContainKey(1,
            "baseline: the dependent line has a proven write label");

        // An edit invalidates and schedules a (DebounceMs = 3 s) re-sim. The cache MUST still hold the
        // previous values immediately afterwards. The read happens well within the debounce, so the
        // background re-sim has not run yet — and Dispose() cancels it. A regression to blanking the cache on
        // invalidate would make this entry empty (the lens collapse the user reported).
        sim.InvalidateAndSimulate(uri, ["mov rax, 0x11", "mov rdx, rax"]);

        var retained = sim.GetCachedEntry(uri);
        retained.Should().NotBeNull("the cache entry must be retained across invalidate, not blanked");
        retained!.lineStringsWriteLabels.Should().ContainKey(1,
            "the dependent line's value must stay visible until the new run overwrites it in place (no collapse/jump)");
    }
}
