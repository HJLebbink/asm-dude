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

using AsmSim;

using FluentAssertions;

using System;

using Xunit;

/// <summary>
/// Tests for the M0 incremental-simulation plumbing on the real <see cref="AsmSimulator"/>: the per-URI
/// retention of the previous instruction sequence and the cross-edit diff it produces
/// (INCREMENTAL_SIM_PLAN.md M0). These drive the actual production retain+diff path via
/// <see cref="AsmSimulator.ComputeIncrementalDiffForTest"/> — NOT a re-implementation — so they would fail
/// if the retained state were dropped, keyed wrongly, or diffed against the wrong baseline.
/// </summary>
public class AsmSimIncrementalDiffTests
{
    [Fact]
    public void FirstEdit_IsCold_NoPreviousToDiff()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///inc_cold.asm");

        InstructionDiff? diff = sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1", "add rax, rbx"]);

        diff.Should().BeNull("the first simulation of a document has no previous run to diff against");
    }

    [Fact]
    public void SecondEdit_DiffsAgainstTheRetainedPreviousEdit()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///inc_two_edits.asm");

        // First edit establishes the baseline (cold) ...
        sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1", "add rax, rbx", "mov rcx, rax"]);
        // ... second edit changes only line 1's operand.
        InstructionDiff? diff = sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1", "add rax, rcx", "mov rcx, rax"]);

        diff.Should().NotBeNull();
        diff!.HasNoInstructionChange.Should().BeFalse("an operand changed");
        diff.AddedNewLines.Should().Equal(1);
        diff.RemovedOldLines.Should().Equal(1);
        diff.NewToOld[0].Should().Be(0);
        diff.NewToOld[2].Should().Be(2);
    }

    [Fact]
    public void BlankLineInsert_IsTier0_AndRemapsLineNumbers()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///inc_blank_insert.asm");

        sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1", "add rax, rbx"]);
        InstructionDiff? diff = sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1", "", "add rax, rbx"]);

        diff.Should().NotBeNull();
        diff!.HasNoInstructionChange.Should().BeTrue("inserting a blank line changes no instruction");
        diff.NewToOld[2].Should().Be(1, "the second instruction shifted from old line 1 to new line 2");
    }

    [Fact]
    public void EachDocument_HasItsOwnRetainedHistory()
    {
        using var sim = new AsmSimulator();
        var uriA = new Uri("file:///inc_doc_a.asm");
        var uriB = new Uri("file:///inc_doc_b.asm");

        sim.ComputeIncrementalDiffForTest(uriA, ["mov rax, 1"]);
        // B's first edit must still be cold even though A already has history — the retention is per-URI.
        InstructionDiff? diffB = sim.ComputeIncrementalDiffForTest(uriB, ["mov rbx, 2"]);

        diffB.Should().BeNull();
    }

    [Fact]
    public void ClosingADocument_ForgetsItsHistory()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///inc_close.asm");

        sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1"]);
        sim.CancelAndRemove(uri); // document closed

        // Re-opening: the next diff must be cold again (history was released).
        InstructionDiff? diff = sim.ComputeIncrementalDiffForTest(uri, ["mov rax, 1"]);
        diff.Should().BeNull("CancelAndRemove must drop the retained instruction history");
    }
}
