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
/// A cumulative-edit SOAK for incremental simulation — the automated stand-in for the live-VS soak
/// (INCREMENTAL_SIM_PLAN.md M3 "remaining"). It drives the REAL production decision path
/// (<see cref="AsmSimulator.SimulateIncrementalForTest"/> → <c>RunSimulation</c>'s Tier-0 → cone → full
/// dispatch) through a sequence of mixed edits (operand, insert, comment, blank, flag-input, second
/// component, jump retarget), each applied on top of the previous, and asserts the incremental result stays
/// byte-identical to a full re-simulation at EVERY step. This is the real risk the unit tests don't cover:
/// state drift across many chained reuses.
/// </summary>
public class AsmSimIncrementalSoakTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    private static void AssertMatchesFull(AsmSimulator sim, Uri uri, string[] current, string step)
    {
        SimResultSet incremental = sim.ToResultSet(uri, "incremental");
        using var fresh = new AsmSimulator();
        fresh.SimulateSynchronouslyForTest(uri, current, Component, computeFullState: false);
        SimResultSet full = fresh.ToResultSet(uri, "full");

        SimDiff diff = SimResultComparer.Compare(incremental, full);
        diff.IsEmpty.Should().BeTrue($"after '{step}', incremental must equal a full re-sim:" + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void CumulativeMixedEdits_EachStepEqualsFullSim()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///soak.asm");

        string[] v0 =
        [
            "mov rax, 0x10", "mov rbx, 0x20", "cmp rax, rbx", "jne skip",
            "add rax, rbx", "skip: mov rcx, rax", "ret",
            "mov rsi, 5", "ret", // a second, independent CFG component
        ];
        sim.SimulateSynchronouslyForTest(uri, v0, Component, computeFullState: false); // cold baseline (commits)

        // Each entry: a description + the FULL new text after that edit (applied cumulatively).
        (string step, string[] text)[] edits =
        [
            ("operand edit", ["mov rax, 0x11", "mov rbx, 0x20", "cmp rax, rbx", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 5", "ret"]),
            ("insert instruction", ["mov rax, 0x11", "xor rdx, rdx", "mov rbx, 0x20", "cmp rax, rbx", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 5", "ret"]),
            ("comment-only edit", ["mov rax, 0x11", "xor rdx, rdx", "mov rbx, 0x20 ; widen", "cmp rax, rbx", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 5", "ret"]),
            ("blank line insert", ["mov rax, 0x11", "xor rdx, rdx", "", "mov rbx, 0x20 ; widen", "cmp rax, rbx", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 5", "ret"]),
            ("flag-input edit (cmp)", ["mov rax, 0x11", "xor rdx, rdx", "", "mov rbx, 0x20 ; widen", "cmp rax, 0x20", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 5", "ret"]),
            ("second-component edit", ["mov rax, 0x11", "xor rdx, rdx", "", "mov rbx, 0x20 ; widen", "cmp rax, 0x20", "jne skip", "add rax, rbx", "skip: mov rcx, rax", "ret", "mov rsi, 9", "ret"]),
            ("jump retarget", ["mov rax, 0x11", "xor rdx, rdx", "", "mov rbx, 0x20 ; widen", "cmp rax, 0x20", "jne done", "add rax, rbx", "skip: mov rcx, rax", "done: ret", "mov rsi, 9", "ret"]),
        ];

        foreach ((string step, string[] text) in edits)
        {
            sim.SimulateIncrementalForTest(uri, text);
            AssertMatchesFull(sim, uri, text, step);
        }
    }
}
