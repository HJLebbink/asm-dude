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
/// The M2 Tier-1 static-cone partial-re-solve correctness gate (INCREMENTAL_SIM_PLAN.md §8): for a
/// topology-preserving instruction edit, re-solving ONLY the forward dataflow cone (and reusing the remapped
/// baseline for everything else) MUST be byte-identical to a full re-simulation of the edited text. The
/// oracle is the real <see cref="SimResultComparer"/> over the production cone path
/// (<see cref="AsmSimulator.SimulateConeForTest"/> → <c>TryConeComponentReuse</c>) versus a fresh full
/// Component re-sim — so an unsound cone (a missed downstream line, a wrongly-reused line) fails here. All on
/// the COMPONENT engine, the editor default and the only engine the cone path runs on.
/// </summary>
public class AsmSimConeReuseTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    private static void AssertConeEqualsFull(string[] first, string[] second)
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///cone.asm");
        sim.SimulateSynchronouslyForTest(uri, first, Component); // commit a Component baseline

        bool reused = sim.SimulateConeForTest(uri, second);
        reused.Should().BeTrue("a topology-preserving instruction edit must take the cone re-solve path");
        SimResultSet incremental = sim.ToResultSet(uri, "incremental");

        using var fresh = new AsmSimulator();
        fresh.SimulateSynchronouslyForTest(uri, second, Component);
        SimResultSet full = fresh.ToResultSet(uri, "full");

        SimDiff diff = SimResultComparer.Compare(incremental, full);
        diff.IsEmpty.Should().BeTrue("cone re-solve must equal a full re-simulation:" + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void OperandEdit_ConeReSolveEqualsFullSim()
    {
        string[] first = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];
        string[] second = ["mov rax, 0x10", "mov rbx, 0x21", "add rax, rbx"]; // edit line 1 operand
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void InsertInstruction_ConeReSolveEqualsFullSim()
    {
        string[] first = ["mov rax, 1", "add rax, rbx"];
        string[] second = ["mov rax, 1", "mov rbx, 5", "add rax, rbx"]; // insert a plain instruction
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void DeleteInstruction_ConeReSolveEqualsFullSim()
    {
        string[] first = ["mov rax, 1", "mov rbx, 5", "add rax, rbx"];
        string[] second = ["mov rax, 1", "add rax, rbx"]; // delete the middle instruction
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void EditInOneComponent_LeavesAnotherComponentEqual()
    {
        // Two CFG components: {mov rax; ret} and {mov rbx; ret} (the ret severs the fall-through, and the
        // second mov has in-degree 0 ⇒ separate component, no label needed). Edit component A only.
        string[] first = ["mov rax, 1", "ret", "mov rbx, 2", "ret"];
        string[] second = ["mov rax, 9", "ret", "mov rbx, 2", "ret"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void EditInOneComponent_DoesNotReSolveTheOther_StringInstancesReused()
    {
        // Direct proof that the untouched component is skipped wholesale: its cached string instance is
        // carried over (reference-identical), which a re-solve would not produce.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///cone_twocomp.asm");
        string[] first = ["mov rax, 1", "ret", "mov rbx, 2", "ret"];
        sim.SimulateSynchronouslyForTest(uri, first, Component);

        var baseline = sim.GetCachedEntry(uri)!;
        string compBAfter = baseline.lineStringsAfter[2]!; // component B's 'mov rbx, 2' after-state

        bool reused = sim.SimulateConeForTest(uri, ["mov rax, 9", "ret", "mov rbx, 2", "ret"]);
        reused.Should().BeTrue();

        var afterEdit = sim.GetCachedEntry(uri)!;
        ReferenceEquals(compBAfter, afterEdit.lineStringsAfter[2]).Should().BeTrue(
            "the untouched component's cached string must be reused, proving its DynamicFlow was never rebuilt");
    }

    [Fact]
    public void LabeledLineEdit_ReusesViaTopologyCone_AndMatchesFull()
    {
        // Editing a LABELED instruction's operand changes no CFG edge, so the topology cone re-solves it +
        // its forward cone and reuses the rest — and must equal a full re-sim. (Before the topology relax this
        // declined to a full sim; the edge-diff now recognizes there is no real topology change.)
        string[] first = ["mov rax, 1", "tgt: mov rbx, 5", "mov rcx, rbx", "ret"];
        string[] second = ["mov rax, 1", "tgt: mov rbx, 9", "mov rcx, rbx", "ret"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void JumpRetarget_ReusesViaTopologyCone_AndMatchesFull()
    {
        string[] first = ["cmp rax, 0", "je far", "mov rbx, 1", "far: mov rcx, 2", "end: mov rdx, 3"];
        string[] second = ["cmp rax, 0", "je end", "mov rbx, 1", "far: mov rcx, 2", "end: mov rdx, 3"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void AddingALabelThatResolvesAJump_ReusesViaTopologyCone_AndMatchesFull()
    {
        // `jmp done` is unresolved until `done:` is introduced — a genuine topology change. The newly-reachable
        // line must be re-solved and the result must equal a full re-sim.
        string[] first = ["mov rax, 1", "jmp done", "mov rcx, 3"];
        string[] second = ["mov rax, 1", "jmp done", "done: mov rcx, 3"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void ColdDocument_DeclinesConePath()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///cone_cold.asm");
        bool reused = sim.SimulateConeForTest(uri, ["mov rax, 1", "add rax, rbx"]);
        reused.Should().BeFalse("no committed baseline ⇒ nothing to reuse around");
    }
}
