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
using System.Collections.Generic;

using Xunit;

/// <summary>
/// Headless CHARACTERIZATION tests for the editor's linear simulator (<see cref="AsmSimulator"/>).
///
/// <para>Purpose: pin down the simulator's current per-line output (register/flag states) for small,
/// deterministic programs, so that the planned move to a per-CFG-component / DynamicFlow engine
/// (INCREMENTAL_SIM_PLAN.md Phase 2) has a golden baseline to diff against — the oracle the S0 shadow
/// harness will compare the new engine to. These run with NO Visual Studio: they drive the real
/// <see cref="AsmSimulator.RunSimulation"/> synchronously through the
/// <see cref="AsmSimulator.SimulateSynchronouslyForTest"/> seam.</para>
///
/// <para>Reproducibility note: every program here is small enough that Z3 never hits the per-line
/// timeout, so results are deterministic regardless of machine speed. Programs that CAN time out are
/// intentionally avoided (a timed-out query yields <c>unknown</c> on a slow box but a value on a fast
/// one — see the reproducibility caveat in INCREMENTAL_SIM_PLAN.md).</para>
/// </summary>
[Collection("AsmLog")] // the Observability test manipulates global AsmLog sinks/threshold
public class AsmSimulatorTests
{
    /// <summary>Compact after-state of a line, in the stable production form (e.g. "rax=0x30, ZF=0").</summary>
    private static string? CompactAfter(AsmSimulator sim, Uri uri, int line)
        => AsmSimulator.CompactStateString(sim.GetRegisterStatesAfterLine(uri, line));

    [Fact]
    public void StraightLine_RegisterValues_AreProven()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///charac_straightline.asm");
        string[] lines =
        [
            "mov rax, 0x10",   // line 0 -> rax = 0x10
            "mov rbx, 0x20",   // line 1 -> rbx = 0x20
            "add rax, rbx",    // line 2 -> rax = 0x30
        ];

        sim.SimulateSynchronouslyForTest(uri, lines);

        // Golden form captured from the real simulator: "RAX=0x_0000_0000_0010" (upper-case register,
        // underscore-grouped, full 64-bit width). If the engine swap changes this, the diff is here.
        CompactAfter(sim, uri, 0).Should().Contain("RAX=0x_0000_0000_0010", "the immediate move is proven");
        CompactAfter(sim, uri, 2).Should().Contain("RAX=0x_0000_0000_0030", "0x10 + 0x20 is proven concretely");
    }

    [Fact]
    public void XorSelf_ZerosRegisterAndSetsZeroFlag()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///charac_xorself.asm");
        string[] lines = ["xor rax, rax"]; // rax = 0, ZF = 1

        sim.SimulateSynchronouslyForTest(uri, lines);

        string? after = CompactAfter(sim, uri, 0);
        after.Should().Contain("RAX=0x_0000_0000_0000", "xor reg,reg proves the register is zero");
        after.Should().Contain("ZF=1", "a zero result sets the zero flag");
    }

    [Fact]
    public void ParityJump_ReadsPF_IsAnnotated()
    {
        // A parity jump (`jp`) reads PF and nothing else. The read-side CodeLens label must therefore show
        // PF — which requires PF to be a TRACKED status flag (EnableFullStateConfig). Guards the regression
        // where only CF/ZF/SF/OF were tracked, so `jp` got NO read label at all.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///parity_jump.asm");
        string[] lines = ["mov al, 1", "cmp al, 0", "jp label1", "label1:"];

        sim.SimulateSynchronouslyForTest(uri, lines);
        SimResultSet rs = sim.ToResultSet(uri, "parity");

        rs.Lines.Should().ContainKey(2, "the jp instruction line is annotated");
        rs.Lines[2].ReadLabel.Should().NotBeNull("jp reads a flag, so it must have a read label");
        rs.Lines[2].ReadLabel!.Should().Contain("PF", "jp is jump-if-parity — it reads the parity flag");
    }

    [Fact]
    public void SameProgram_SimulatedTwice_ProducesIdenticalOutput()
    {
        // Determinism of the editor sim for a concrete (timeout-free) program: two independent runs
        // must yield byte-identical per-line state. This is the reproducibility property the shadow
        // harness depends on.
        string[] lines = ["mov rax, 0x10", "mov rbx, rax", "add rax, rbx"];

        static List<string?> Run(string[] program)
        {
            using var sim = new AsmSimulator();
            var uri = new Uri("file:///charac_determinism.asm");
            sim.SimulateSynchronouslyForTest(uri, program);
            var result = new List<string?>();
            for (int i = 0; i < program.Length; i++)
                result.Add(sim.GetRegisterStatesAfterLine(uri, i));
            return result;
        }

        List<string?> first = Run(lines);
        List<string?> second = Run(lines);

        second.Should().Equal(first, "the same concrete program must simulate identically every run");
        // sanity: the program actually proved something (not all-null)
        first.Should().Contain(s => s != null);
    }

    [Fact]
    public void Branch_LinearEngine_DoesNotFollowJumps_GoldenBaseline()
    {
        // GOLDEN BASELINE for the engine swap — and it documents a real CURRENT limitation discovered
        // by this characterization: the editor's linear simulator does NOT follow jumps. It walks the
        // lines top-to-bottom, so `mov rax,2` on line 2 IS executed even though line 1 jumped over it,
        // and rax=2 then flows into the join at line 3. The future per-CFG-component / DynamicFlow
        // engine would NOT execute line 2 on the jumped path and would merge real predecessors — a
        // DIFFERENT, more-correct result. When that lands, THIS test must change (with review): the
        // failure here is the visible signal of the semantic shift the plan warns about.
        var uri = new Uri("file:///charac_branch.asm");
        string[] lines =
        [
            "      mov rax, 1",      // line 0
            "      jmp skip",        // line 1 (linear engine ignores the jump target)
            "      mov rax, 2",      // line 2 (jumped over in reality, but STILL simulated linearly)
            "skip: mov rbx, rax",    // line 3 (label / join)
        ];

        using var sim = new AsmSimulator();
        sim.SimulateSynchronouslyForTest(uri, lines);

        CompactAfter(sim, uri, 0).Should().Contain("RAX=0x_0000_0000_0001", "line 0 proves rax=1");

        // CURRENT (imperfect) behavior: the linearly-executed line 2 makes rax=2 at the join.
        string? join = CompactAfter(sim, uri, 3);
        join.Should().Contain("RAX=0x_0000_0000_0002",
            "the linear engine executes line 2 despite the jump — captured as the baseline the merge engine will change");
        join.Should().Contain("RBX=0x_0000_0000_0002", "rbx := rax(=2) at the join");
    }

    [Fact]
    public void Observability_LogsCfgPartition_ForMultiFunctionProgram()
    {
        // The gated CFG-partition observability (the foundation for the per-component engine) must log
        // the weakly-connected decomposition for a real multi-function document. Two functions, each
        // ending in `ret` with no edge between them, form TWO components. Verified END-TO-END through an
        // AsmLog sink (computation + emission + format), not just the underlying StaticFlow unit test.
        var captured = new System.Collections.Concurrent.ConcurrentBag<string>();
        void Sink(AsmTools.AsmLogEntry e)
        {
            if (e.Message.StartsWith("[CFG]", StringComparison.Ordinal)) captured.Add(e.Message);
        }

        AsmTools.AsmLogLevel previousThreshold = AsmTools.AsmLog.Threshold;
        AsmTools.AsmLog.Threshold = AsmTools.AsmLogLevel.Debug; // partition logging is gated on Debug
        AsmTools.AsmLog.AddSink(Sink);
        try
        {
            using var sim = new AsmSimulator();
            var uri = new Uri("file:///obs_cfg_twofunc.asm");
            string[] lines =
            [
                "f1: mov rax, 1",
                "    ret",
                "f2: mov rbx, 2",
                "    ret",
            ];
            sim.SimulateSynchronouslyForTest(uri, lines);

            string? cfg = null;
            foreach (string m in captured)
            {
                if (m.Contains("obs_cfg_twofunc.asm", StringComparison.Ordinal)) { cfg = m; break; }
            }

            cfg.Should().NotBeNull("the CFG partition must be logged when Debug is enabled");
            cfg.Should().Contain("2 component(s)",
                "two functions with no control-flow edge between them are two weakly-connected components");
        }
        finally
        {
            AsmTools.AsmLog.ClearSinks();
            AsmTools.AsmLog.Threshold = previousThreshold;
        }
    }

    [Fact]
    public void SimResultComparer_SameProgramTwice_NoDiff()
    {
        // First real consumer of the standalone differential oracle: two runs of the SAME program must
        // produce ZERO differences. This proves the editor sim is deterministic end-to-end AND exercises
        // SimResultComparer on real production output (the engine-swap shadow is a later consumer).
        string[] lines = ["mov rax, 0x10", "add rax, 0x20", "xor rbx, rbx", "mov rcx, rax"];

        static SimResultSet Run(string[] program, string label)
        {
            using var sim = new AsmSimulator();
            var uri = new Uri("file:///simdiff_determinism.asm");
            sim.SimulateSynchronouslyForTest(uri, program);
            return sim.ToResultSet(uri, label);
        }

        SimResultSet a = Run(lines, "run-1");
        SimResultSet b = Run(lines, "run-2");

        SimDiff diff = SimResultComparer.Compare(a, b);

        diff.IsEmpty.Should().BeTrue(diff.ToReport());
        a.Lines.Should().NotBeEmpty("the runs must actually produce results (empty diff means identical, not both-empty)");
    }

    [Fact]
    public void Shadow_StraightLineProgram_EnginesAgree()
    {
        // S2 shadow: on straight-line code (no joins) the per-component DynamicFlow engine must produce
        // the SAME per-line before/after states as the linear engine. This validates the component engine
        // and the shadow comparison on the case where they MUST agree.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///shadow_straightline.asm");
        string[] lines = ["mov rax, 0x10", "add rax, 0x20", "mov rbx, rax"];

        SimDiff diff = sim.CompareEnginesForTest(uri, lines);

        diff.IsEmpty.Should().BeTrue("straight-line code has no merges, so both engines must agree." + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void Shadow_BranchProgram_EnginesDifferAtJoin()
    {
        // S2 shadow: THE point of the shadow. The linear engine walks top-to-bottom and executes the
        // jumped-over `mov rax,2`, so rax=2 at the join. The component engine builds the CFG (line 2 is a
        // separate entry that merges into the join), so rax is the join of {1,2} = UNKNOWN. They MUST
        // differ at the join — and the shadow surfaces exactly that line, which is what makes the eventual
        // engine flip a reviewed change, not a silent one.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///shadow_branch.asm");
        string[] lines =
        [
            "        mov rax, 1",
            "        jmp skip",
            "        mov rax, 2",
            "skip:   mov rbx, rax",
        ];

        SimDiff diff = sim.CompareEnginesForTest(uri, lines);

        diff.IsEmpty.Should().BeFalse("merge-vs-linear semantics must differ on a branch — the shadow's whole purpose");
        diff.ChangedLines.Should().Contain(3, "the divergence is at the join (line 3)." + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void Shadow_NonInstructionLines_AreNotSpuriousDiffs()
    {
        // Regression for the extractor bug behind the real-file "only in component" noise: label,
        // comment, and directive lines must be skipped by BOTH engines (only real instructions carry
        // state). No #pragma here, so this straight-line program must agree exactly.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///shadow_noninstr.asm");
        string[] lines =
        [
            "        mov rax, 0x10",
            "        ; just a comment",
            "        mov rbx, 0x20",
            "target:",
            "        add rax, rbx",
        ];

        SimDiff diff = sim.CompareEnginesForTest(uri, lines);

        diff.IsEmpty.Should().BeTrue("non-instruction lines must be skipped by both engines; straight-line agrees." + Environment.NewLine + diff.ToReport());
    }

    [Fact]
    public void Shadow_PragmaAssumeAndHlt_EnginesAgree()
    {
        // The #pragma rewrite must make the component engine match the linear engine in #pragma regions:
        //   - `#pragma assume mov rax, 8` injects rax=8  -> line 1 proves rbx=8;
        //   - `#pragma assume HLT` resets the state      -> after it, rbx is unknown again (line 3).
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///shadow_pragma.asm");
        string[] lines =
        [
            "        #pragma assume mov rax, 8",
            "        mov rbx, rax",
            "        #pragma assume HLT",
            "        mov rcx, rbx",
        ];

        SimDiff diff = sim.CompareEnginesForTest(uri, lines);

        diff.IsEmpty.Should().BeTrue("the #pragma rewrite must make the component engine match linear across assume + HLT." + Environment.NewLine + diff.ToReport());
    }

    [Fact(Skip = "Manual exploration only — runs the full sim on a real branch-heavy file; slow due to Z3 timeouts. Writes %TEMP%/shadow_report.txt.")]
    public void Explore_Shadow_OnRealExampleFile()
    {
        // EXPLORATION (not a regression gate): run the shadow on a real editor example file and dump a
        // data sheet (lines, components, diff count, sample diffs, wall time) to %TEMP%/shadow_report.txt.
        // Bounded to keep the run tractable: the component engine does full merge machinery + Z3.
        const string path = @"C:\Source\Github\asm-dude\VS\CSHARP\asm-dude2-vsix\Resources\examples\example_semantic_analysis.asm";
        if (!System.IO.File.Exists(path)) return;

        string[] all = System.IO.File.ReadAllLines(path);
        string[] lines = all.Length > 16 ? all[..16] : all; // the top #pragma "Unreachable code" region + first HLT

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///explore_real.asm");
        SimDiff diff = sim.CompareEnginesForTest(uri, lines);
        sw.Stop();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"file: {path}");
        sb.AppendLine($"lines simulated: {lines.Length} (of {all.Length})");
        sb.AppendLine($"elapsed: {sw.Elapsed.TotalSeconds:F1} s (linear + component)");
        sb.AppendLine($"diff entries: {diff.Entries.Count}; changed lines: {diff.ChangedLines.Count}");
        sb.AppendLine("---- report (first 60 entries) ----");
        int n = 0;
        foreach (SimDiffEntry e in diff.Entries)
        {
            sb.AppendLine("  " + e);
            if (++n >= 60) { sb.AppendLine($"  ... (+{diff.Entries.Count - n} more)"); break; }
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shadow_report.txt"), sb.ToString());
    }

    // ── CodeLens write/read label merge (the per-position "w:"/"r:" → "rw:" combine) ────────────────
    // Invariant: an UNKNOWN-valued write (rendered as binary "0b????…", no hex) must survive compaction
    // and the merge with the next instruction's read. The binary form has spaces around '=' in the raw
    // state string; if CompactStateString doesn't normalize them, ParseCompactItems (which splits on
    // spaces) drops the item and the write silently vanishes — the read appears to "overwrite" it.

    [Fact]
    public void CompactStateString_UnknownRegisterValue_IsNormalizedSpaceFree()
    {
        // Raw production form for an unknown register write: "<prefix>:NAME = 0b<binary>" (no hex part).
        string? compact = AsmSimulator.CompactStateString("\nw:RAX = 0b????_????");

        // Must be space-free so ParseCompactItems can read it back (this is what was broken).
        compact.Should().Be("w:RAX=0b????_????");
    }

    [Fact]
    public void MergeCompactLabels_UnknownWrite_IsNotDroppedByConcreteRead()
    {
        // Line N writes RAX with an unknown value; line N+1 reads a different, concrete register RBX.
        // Both share the CodeLens at display position N+1 and BOTH must show.
        string? write = AsmSimulator.CompactStateString("\nw:RAX = 0b????_????");
        string? read = AsmSimulator.CompactStateString("\nr:RBX = 0b0000_0100 = 0x4");

        string? merged = AsmSimulator.MergeCompactLabels(write, read);

        merged.Should().NotBeNull();
        merged.Should().Contain("w:RAX=0b????_????"); // the write is kept, not overwritten…
        merged.Should().Contain("r:RBX=0x4");         // …alongside the read
    }

    [Fact]
    public void MergeCompactLabels_SameRegisterWrittenThenRead_MergesToReadWrite()
    {
        // Same register on both sides (even with an unknown value) collapses to a single "rw:" item.
        string? write = AsmSimulator.CompactStateString("\nw:RAX = 0b????_????");
        string? read = AsmSimulator.CompactStateString("\nr:RAX = 0b????_????");

        string? merged = AsmSimulator.MergeCompactLabels(write, read);

        merged.Should().Be("rw:RAX=0b????_????");
    }

    // ── Redundant-instruction detection (AsmDude1 parity; REDUNDANT_DIAGNOSTICS_PLAN.md) ────────────────
    // These drive the REAL linear engine via SimulateSynchronouslyForTest. The seam does not touch
    // showRedundant_, so ApplySettings(..., showRedundant: true) BEFORE the run enables the check (and the
    // seam preserves it — it sets engine/incremental/computeFullState but not showRedundant_).

    private static void SimulateWithRedundant(AsmSimulator sim, Uri uri, string[] lines, AsmSimulator.SimEngineMode engine = AsmSimulator.SimEngineMode.Linear)
    {
        // ApplySettings enables showRedundant_ (the seam preserves it); SimulateSynchronouslyForTest pins
        // the engine + incremental-off.
        sim.ApplySettings(engine: "linear", loop: "accept", incremental: false, showRedundant: true);
        sim.SimulateSynchronouslyForTest(uri, lines, engine);
    }

    private static System.Collections.Generic.List<int> RedundantLines(AsmSimulator sim, Uri uri)
    {
        var result = new System.Collections.Generic.List<int>();
        foreach (SimDiagnostic d in sim.GetDiagnostics(uri))
        {
            if (d.Kind == SimDiagnosticKind.Redundant) result.Add(d.Line);
        }
        return result;
    }

    [Fact]
    public void Redundant_MovRegToItself_IsFlagged()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_movself.asm");
        string[] lines =
        [
            "mov rax, rbx",   // line 0: changes rax (not redundant)
            "mov rax, rax",   // line 1: writes rax = rax, no flags → provably unchanged → REDUNDANT
        ];

        SimulateWithRedundant(sim, uri, lines);

        RedundantLines(sim, uri).Should().Contain(1, "\"mov rax, rax\" cannot change machine state");
        RedundantLines(sim, uri).Should().NotContain(0, "\"mov rax, rbx\" changes rax");
    }

    [Fact]
    public void Redundant_RewriteSameImmediate_Component_IsFlagged()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_reimm.asm");
        string[] lines =
        [
            "mov rax, 0x10",   // line 0: defines rax = 0x10 (not redundant)
            "mov rax, 0x10",   // line 1: rax is already 0x10 → SHOULD be redundant (AsmDude1 caught this)
        ];

        SimulateWithRedundant(sim, uri, lines, AsmSimulator.SimEngineMode.Component);

        RedundantLines(sim, uri).Should().Contain(1, "rax already holds 0x10");
        RedundantLines(sim, uri).Should().NotContain(0);
    }

    [Fact]
    public void Redundant_MovRegToItself_Component_IsFlagged()
    {
        // Structural redundancy must also hold under the editor's DEFAULT engine (Component).
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_movself_component.asm");
        string[] lines =
        [
            "mov rax, rbx",
            "mov rax, rax",   // REDUNDANT
        ];

        SimulateWithRedundant(sim, uri, lines, AsmSimulator.SimEngineMode.Component);

        RedundantLines(sim, uri).Should().Contain(1);
        RedundantLines(sim, uri).Should().NotContain(0);
    }

    [Fact]
    public void Redundant_StateChangingInstructions_AreNotFlagged()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_negatives.asm");
        string[] lines =
        [
            "mov rax, 0x10",   // line 0: defines rax
            "add rax, 1",      // line 1: changes rax AND flags → not redundant
            "xor rax, rax",    // line 2: changes rax to 0 + sets flags → not redundant
            "nop",             // line 3: NOP is guarded out → never flagged
        ];

        SimulateWithRedundant(sim, uri, lines);

        var redundant = RedundantLines(sim, uri);
        redundant.Should().NotContain(1, "add changes rax and flags");
        redundant.Should().NotContain(2, "xor self changes rax and flags");
        redundant.Should().NotContain(3, "NOP is explicitly skipped, not warned");
    }

    [Fact]
    public void Redundant_Disabled_ProducesNoRedundantDiagnostics()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_gate.asm");
        string[] lines =
        [
            "mov rax, rbx",
            "mov rax, rax",   // would be redundant IF the check were enabled
        ];

        // No ApplySettings(showRedundant: true) → the production gate is OFF.
        sim.SimulateSynchronouslyForTest(uri, lines);

        RedundantLines(sim, uri).Should().BeEmpty("the redundant check must be gated by the setting");
    }

    [Fact]
    public void Redundant_MovBackKnownEqual_IsFlagged()
    {
        // The canonical demo case (example_semantic_analysis.asm "Redundant instruction warning" region):
        // after `mov rax, rbx`, rbx==rax, so writing rax back into rbx is provably state-preserving.
        // This is VALUE redundancy via known equality — distinct from the structural `mov rax, rax`.
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_movback.asm");
        string[] lines =
        [
            "mov rax, rbx",   // line 0: rax := rbx (changes rax)
            "mov rbx, rax",   // line 1: rbx := rax, but rbx already == rax → REDUNDANT
        ];

        SimulateWithRedundant(sim, uri, lines);

        RedundantLines(sim, uri).Should().Contain(1, "rbx already equals rax, so this mov preserves state");
        RedundantLines(sim, uri).Should().NotContain(0);
    }

    [Fact]
    public void Redundant_MovBackKnownEqual_Component_IsFlagged()
    {
        using var sim = new AsmSimulator();
        var uri = new Uri("file:///redundant_movback_component.asm");
        string[] lines =
        [
            "mov rax, rbx",
            "mov rbx, rax",   // REDUNDANT
        ];

        SimulateWithRedundant(sim, uri, lines, AsmSimulator.SimEngineMode.Component);

        RedundantLines(sim, uri).Should().Contain(1);
        RedundantLines(sim, uri).Should().NotContain(0);
    }
}
