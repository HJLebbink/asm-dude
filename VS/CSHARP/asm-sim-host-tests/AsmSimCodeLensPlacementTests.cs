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

using System;
using System.Collections.Generic;

using FluentAssertions;

using Xunit;

/// <summary>
/// CodeLens placement: reads of instruction N are shown ABOVE it (display position N), writes BELOW it
/// (display position N+1). CodeLens render above their line, so "below instruction N" = a lens anchored at
/// line N+1. The write position N+1 may fall on a blank line (Enter pressed under an instruction); the tagger
/// renders the lens on that blank line — directly below the instruction — rather than dropping it.
/// </summary>
public class AsmSimCodeLensPlacementTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    [Fact]
    public void GetSimStatesSummary_ShowsWriteBelowInstruction_AtNplus1()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///placement.asm");
        // 'mov rax, 5' (line 0) writes RAX and reads nothing. Its after-state is shown BELOW the instruction,
        // i.e. at display position 1 — even though line 1 is blank (the tagger renders it on that blank line).
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 5", ""], Component, computeFullState: false);

        Dictionary<int, string> summary = sim.GetSimStatesSummary(uri);

        summary.Should().ContainKey(1, "the write/after-state is shown on the line below the instruction (N+1)");
        summary[1].Should().Contain("RAX", "the below-lens shows what the mov produced");
        summary.Should().NotContainKey(0, "mov reads nothing, so there is no read lens above it");
    }

    [Fact]
    public void GetSimStatesSummary_ReadAboveAndWriteBelow_AreDistinctPositions()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///placement2.asm");
        // 'add rax, rbx' reads (rax,rbx) and writes (rax). Read lens above (its own line), write lens below.
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 5", "mov rbx, 7", "add rax, rbx"], Component, computeFullState: false);

        Dictionary<int, string> summary = sim.GetSimStatesSummary(uri);

        // The add is line 2: its reads show at position 2 (above it), its writes at position 3 (below it).
        summary.Should().ContainKey(2, "add's reads are shown above it");
        summary.Should().ContainKey(3, "add's writes are shown below it (N+1)");
    }
}
