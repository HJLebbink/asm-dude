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

namespace AsmDude2LS.Tests;

using System;
using System.Collections.Generic;

using AsmSim;

using FluentAssertions;

using Xunit;

/// <summary>
/// Headless CHARACTERIZATION tests for the editor's linear simulator (<see cref="LspAsmSimulator"/>).
///
/// <para>Purpose: pin down the simulator's current per-line output (register/flag states) for small,
/// deterministic programs, so that the planned move to a per-CFG-component / DynamicFlow engine
/// (INCREMENTAL_SIM_PLAN.md Phase 2) has a golden baseline to diff against — the oracle the S0 shadow
/// harness will compare the new engine to. These run with NO Visual Studio: they drive the real
/// <see cref="LspAsmSimulator.RunSimulation"/> synchronously through the
/// <see cref="LspAsmSimulator.SimulateSynchronouslyForTest"/> seam.</para>
///
/// <para>Reproducibility note: every program here is small enough that Z3 never hits the per-line
/// timeout, so results are deterministic regardless of machine speed. Programs that CAN time out are
/// intentionally avoided (a timed-out query yields <c>unknown</c> on a slow box but a value on a fast
/// one — see the reproducibility caveat in INCREMENTAL_SIM_PLAN.md).</para>
/// </summary>
public class LspAsmSimulatorTests
{
    /// <summary>Compact after-state of a line, in the stable production form (e.g. "rax=0x30, ZF=0").</summary>
    private static string? CompactAfter(LspAsmSimulator sim, Uri uri, int line)
        => LspAsmSimulator.CompactStateString(sim.GetRegisterStatesAfterLine(uri, line));

    [Fact]
    public void StraightLine_RegisterValues_AreProven()
    {
        using var sim = new LspAsmSimulator();
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
        using var sim = new LspAsmSimulator();
        var uri = new Uri("file:///charac_xorself.asm");
        string[] lines = ["xor rax, rax"]; // rax = 0, ZF = 1

        sim.SimulateSynchronouslyForTest(uri, lines);

        string? after = CompactAfter(sim, uri, 0);
        after.Should().Contain("RAX=0x_0000_0000_0000", "xor reg,reg proves the register is zero");
        after.Should().Contain("ZF=1", "a zero result sets the zero flag");
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
            using var sim = new LspAsmSimulator();
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

        using var sim = new LspAsmSimulator();
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
            using var sim = new LspAsmSimulator();
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
            using var sim = new LspAsmSimulator();
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
}
