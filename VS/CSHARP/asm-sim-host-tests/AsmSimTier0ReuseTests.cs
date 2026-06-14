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
/// The M1 Tier-0 incremental-reuse correctness gate (INCREMENTAL_SIM_PLAN.md §8): for an edit that only
/// shifts inert (blank/comment) lines, the incrementally-remapped result MUST be byte-identical to a full
/// re-simulation of the edited text. The oracle is the real <see cref="SimResultComparer"/> over two real
/// production runs — the incremental path (<see cref="AsmSimulator.SimulateTier0ForTest"/>, which drives the
/// production <c>TryTier0Reuse</c>) versus a fresh full <see cref="AsmSimulator.SimulateSynchronouslyForTest"/>
/// — so an unsound remap (wrong line mapping, a missed topology change, a dropped diagnostic) fails here.
/// </summary>
public class AsmSimTier0ReuseTests
{
    /// <summary>Run <paramref name="first"/> fully (establishing the reusable baseline), apply
    /// <paramref name="second"/> via Tier-0, and assert it (a) actually reused — no Z3 — and (b) produced
    /// exactly what a full re-sim of <paramref name="second"/> produces.</summary>
    private static void AssertIncrementalEqualsFull(string[] first, string[] second,
        AsmSimulator.SimEngineMode engine = AsmSimulator.SimEngineMode.Linear)
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///tier0.asm");
        sim.SimulateSynchronouslyForTest(uri, first, engine);

        bool reused = sim.SimulateTier0ForTest(uri, second);
        reused.Should().BeTrue("an inert-only edit must be satisfied by Tier-0 reuse without running Z3");
        SimResultSet incremental = sim.ToResultSet(uri, "incremental");

        using var fresh = new AsmSimulator();
        fresh.SimulateSynchronouslyForTest(uri, second, engine);
        SimResultSet full = fresh.ToResultSet(uri, "full");

        SimDiff diff = SimResultComparer.Compare(incremental, full);
        diff.IsEmpty.Should().BeTrue("Tier-0 reuse must equal a full re-simulation:" + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void BlankLineInserted_ReusesAndMatchesFullSim()
    {
        string[] first = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];
        string[] second = ["mov rax, 0x10", "", "mov rbx, 0x20", "add rax, rbx"];
        AssertIncrementalEqualsFull(first, second);
    }

    [Fact]
    public void BlankLineInserted_ComponentEngine_ReusesAndMatchesFullSim()
    {
        // The editor's DEFAULT engine is Component (DynamicFlow / branch-aware), not Linear. Tier-0 reuse is
        // engine-agnostic (it remaps strings), but the baseline cache is produced by the engine — so prove a
        // COMPONENT-produced baseline reuses to exactly a full Component re-sim. This is the configuration a
        // live VS session actually runs. (Inert-only edits keep the CFG identical, so every DynamicFlow line
        // state is unchanged — the soundness Tier-0 relies on holds for the merge engine too.)
        string[] first = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];
        string[] second = ["mov rax, 0x10", "", "mov rbx, 0x20", "add rax, rbx"];
        AssertIncrementalEqualsFull(first, second, AsmSimulator.SimEngineMode.Component);
    }

    [Fact]
    public void BlankLineRemoved_ReusesAndMatchesFullSim()
    {
        string[] first = ["mov rax, 0x10", "", "mov rbx, 0x20", "add rax, rbx"];
        string[] second = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];
        AssertIncrementalEqualsFull(first, second);
    }

    [Fact]
    public void CommentLineEdited_ReusesAndMatchesFullSim()
    {
        string[] first = ["mov rax, 0x10", "; original note", "add rax, rax"];
        string[] second = ["mov rax, 0x10", "; a completely different note", "add rax, rax"];
        AssertIncrementalEqualsFull(first, second);
    }

    [Fact]
    public void TrailingCommentOnInstructionEdited_ReusesAndMatchesFullSim()
    {
        // The comment on an instruction line is stripped by the parser, so the instruction still matches —
        // a pure-comment edit on a code line is Tier-0.
        string[] first = ["mov rax, 0x10", "add rax, rax ; double it"];
        string[] second = ["mov rax, 0x10", "add rax, rax ; x2"];
        AssertIncrementalEqualsFull(first, second);
    }

    [Fact]
    public void DiagnosticsAreRemapped_OnBlankInsert()
    {
        // An unimplemented/undefined-using instruction emits a diagnostic; after a blank insert the
        // diagnostic must follow the instruction to its new line, exactly as a full re-sim would place it.
        string[] first = ["add rax, rbx"];          // reads RAX/RBX while both are undefined ⇒ a diagnostic
        string[] second = ["", "add rax, rbx"];
        AssertIncrementalEqualsFull(first, second);
    }

    [Fact]
    public void OperandEdit_DoesNotReuse()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///tier0_operand.asm");
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"]);

        bool reused = sim.SimulateTier0ForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rcx"]);

        reused.Should().BeFalse("changing an operand can change the result — a full (or cone) re-sim is required");
    }

    [Fact]
    public void AddingALabel_DoesNotReuse()
    {
        // A label-only line is NONE-mnemonic but a jump target: adding it is a topology change, so Tier-0
        // (which assumes the CFG is unchanged) must decline.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///tier0_label.asm");
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 1", "jmp done", "mov rbx, 2"]);

        bool reused = sim.SimulateTier0ForTest(uri, ["mov rax, 1", "jmp done", "done:", "mov rbx, 2"]);

        reused.Should().BeFalse("adding a label changes topology — Tier-0 reuse must decline");
    }

    [Fact]
    public void Tier0Reuse_CarriesOverCachedStrings_ProvingNoRecomputation()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///tier0_noredo.asm");
        sim.SimulateSynchronouslyForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"]);

        // The exact cached string INSTANCES the full run produced for the 'add' line (index 2).
        var baseline = sim.GetCachedEntry(uri)!;
        string addAfterBaseline = baseline.lineStringsAfter[2]!;
        string addWriteBaseline = baseline.lineStringsWriteLabels[2]!;

        // Insert a blank line above 'add' so it shifts from line 2 to line 3.
        bool reused = sim.SimulateTier0ForTest(uri, ["mov rax, 0x10", "mov rbx, 0x20", "", "add rax, rbx"]);
        reused.Should().BeTrue();

        var afterEdit = sim.GetCachedEntry(uri)!;
        string addAfterEdit = afterEdit.lineStringsAfter[3]!;
        string addWriteEdit = afterEdit.lineStringsWriteLabels[3]!;

        // The SAME string objects were carried forward to the shifted line — a re-solve would have
        // allocated new instances. Reference identity is a deterministic, parallel-safe proof that the line
        // was NOT recomputed (no Z3 ran for it), which a value-equality check could not distinguish.
        ReferenceEquals(addAfterBaseline, addAfterEdit).Should().BeTrue(
            "the after-state string instance must be reused, not recomputed");
        ReferenceEquals(addWriteBaseline, addWriteEdit).Should().BeTrue(
            "the write-label string instance must be reused, not recomputed");
    }

    [Fact]
    public void ColdDocument_DoesNotReuse()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///tier0_cold.asm");

        // No prior full sim ⇒ nothing committed ⇒ cannot reuse.
        bool reused = sim.SimulateTier0ForTest(uri, ["mov rax, 1"]);

        reused.Should().BeFalse("a document with no completed simulation has no baseline to reuse");
    }
}
