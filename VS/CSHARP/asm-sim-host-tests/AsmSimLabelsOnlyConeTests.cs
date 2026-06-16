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
/// Correctness gate for the cone re-solve in the editor's LABELS-ONLY mode (computeFullState:false — the mode
/// the CodeLens read/write labels are computed in). For a topology-preserving edit, re-solving only the static
/// forward cone and reusing the remapped baseline for the rest MUST be byte-identical to a full re-sim of the
/// labels (oracle: the real <see cref="SimResultComparer"/>).
///
/// <para>The editor path uses the STATIC cone (every line forward-reachable from the edit), NOT the tighter
/// dynamic dirty-set cone. The dynamic cone was retired here because it over-pruned on branch/loop/label code,
/// leaving downstream CodeLens labels stale; the static cone is a sound superset (the algorithm itself still
/// lives in asm-sim-lib with its own unit tests in Test_DynamicCone). The key invariant these guard is exactly
/// the reported bug: a downstream line's label must reflect an edited upstream value, never a stale cached one.</para>
/// </summary>
public class AsmSimLabelsOnlyConeTests
{
    private const AsmSimulator.SimEngineMode Component = AsmSimulator.SimEngineMode.Component;

    /// <summary>Run <paramref name="first"/> fully in labels-only mode, apply <paramref name="second"/> via the
    /// cone re-solve, and return the simulator for inspection. Asserts the edit took the cone path.</summary>
    private static AsmSimulator RunConeEdit(string[] first, string[] second, Uri uri)
    {
        var sim = new AsmSimulator();
        sim.SimulateSynchronouslyForTest(uri, first, Component, computeFullState: false);
        bool reused = sim.SimulateConeForTest(uri, second);
        reused.Should().BeTrue("a topology-preserving instruction edit must take the cone path");
        return sim;
    }

    private static void AssertConeEqualsFull(string[] first, string[] second)
    {
        var uri = new Uri("file:///labelscone.asm");
        using AsmSimulator sim = RunConeEdit(first, second, uri);
        SimResultSet incremental = sim.ToResultSet(uri, "incremental");

        using var fresh = new AsmSimulator();
        fresh.SimulateSynchronouslyForTest(uri, second, Component, computeFullState: false);
        SimResultSet full = fresh.ToResultSet(uri, "full");

        SimDiff diff = SimResultComparer.Compare(incremental, full);
        diff.IsEmpty.Should().BeTrue("labels-only cone re-solve must equal a full re-sim:" + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void IndependentLineEdit_LabelsEqualFullSim()
    {
        string[] first = ["mov rax, 0x10", "mov rbx, 0x20", "mov rdx, rax"];
        string[] second = ["mov rax, 0x11", "mov rbx, 0x20", "mov rdx, rax"]; // edit rax; rbx is independent
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void KilledRegisterEdit_LabelsEqualFullSim()
    {
        // line 1 fully overwrites RAX, so editing the now-dead first write must not change line 2's read of RAX.
        string[] first = ["mov rax, 1", "mov rax, 9", "mov rcx, rax"];
        string[] second = ["mov rax, 2", "mov rax, 9", "mov rcx, rax"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void DependentReader_IsReSolved_NewValueShown()
    {
        // THE reported bug, as a guard: editing an upstream value must update the downstream line's label —
        // the cone must not over-prune and leave a stale cached label behind.
        var uri = new Uri("file:///labelscone_dep.asm");
        using var sim = RunConeEdit(["mov rax, 0x10", "mov rdx, rax"], ["mov rax, 0x40", "mov rdx, rax"], uri);

        string rdxLabel = sim.GetCachedEntry(uri)!.lineStringsWriteLabels[1]!;
        rdxLabel.Should().Contain("40", "the dependent reader must reflect the edited value, not the stale one");
    }

    [Fact]
    public void FlagConsumer_TrackedAcrossACompare()
    {
        // Editing the input to a cmp changes the flags an adc consumes; the adc's label must update.
        string[] first = ["mov rax, 5", "cmp rax, 5", "mov rcx, 7"];
        string[] second = ["mov rax, 6", "cmp rax, 5", "mov rcx, 7"];
        AssertConeEqualsFull(first, second);
    }

    [Fact]
    public void ChainedDependents_AllUpdateAfterEdit_EqualFullSim()
    {
        // A multi-hop dependency chain: editing rax must propagate through rbx to rcx. The static cone includes
        // every forward-reachable line, so all downstream labels update.
        string[] first = ["mov rax, 0x10", "mov rbx, rax", "mov rcx, rbx"];
        string[] second = ["mov rax, 0x40", "mov rbx, rax", "mov rcx, rbx"];
        AssertConeEqualsFull(first, second);
    }
}
