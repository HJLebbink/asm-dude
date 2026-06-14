// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using AsmTools;

using FluentAssertions;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// Exercises the NON-LINEAR (component / DynamicFlow merge) engine on a real, branchy 150-line program
/// (regions, conditional jumps, <c>#pragma assume</c>) — not a toy. Guards two regressions at once:
/// <list type="bullet">
/// <item>the engine must COMPLETE (no hang / Z3 context crash) on realistic code, and</item>
/// <item>it must actually PROVE values (non-empty write labels with concrete <c>0x…</c>) — a silent-empty
///       result was a real failure mode of earlier merge bugs.</item>
/// </list>
/// It also surfaces per-line solve timing (the metric that motivated the full-dump removal) via an AsmLog
/// sink so the post-optimization speed is visible in the test output. Run in editor mode
/// (<c>computeFullState: false</c>) so it measures exactly what the editor does.
/// </summary>
[Collection("AsmLog")] // shares global AsmLog sinks/threshold — must not run parallel with other AsmLog tests
public sealed class AsmSimComponentEngineTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper output_ = output;

    [Fact]
    public async Task ComponentEngine_RealExampleFile_CompletesAndProvesValues()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "example_semantic_analysis.asm");
        File.Exists(path).Should().BeTrue($"the example asm must be copied next to the test ({path})");
        string[] lines = await File.ReadAllLinesAsync(path);
        lines.Length.Should().BeGreaterThan(100, "this is the full ~150-line semantic-analysis example");

        // Capture the engine's per-line solve timings ("[component] line N: extracted in X ms").
        var perLineMs = new System.Collections.Concurrent.ConcurrentBag<long>();
        void Sink(AsmLogEntry e)
        {
            const string marker = "extracted in ";
            int idx = e.Message.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return;
            string tail = e.Message[(idx + marker.Length)..].Replace(" ms", string.Empty, StringComparison.Ordinal).Trim();
            if (long.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms)) perLineMs.Add(ms);
        }

        AsmLogLevel previousThreshold = AsmLog.Threshold;
        AsmLog.Threshold = AsmLogLevel.Debug; // per-line timing is logged at Info
        AsmLog.AddSink(Sink);
        SimResultSet rs;
        try
        {
            using var sim = new AsmSimulator();
            var uri = new Uri("file:///component_example.asm");

            // Hard time budget: a hang (or a return of the ~28 s/line pathology across 150 lines) fails the
            // test instead of blocking forever. SimulateSynchronouslyForTest blocks, so run it off-thread.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await Task.Run(() => sim.SimulateSynchronouslyForTest(
                    uri, lines, AsmSimulator.SimEngineMode.Component, computeFullState: false))
                .WaitAsync(TimeSpan.FromMinutes(4));
            clock.Stop();

            rs = sim.ToResultSet(uri, "component");

            // Timing summary (visible with `dotnet test -l "console;verbosity=detailed"`).
            long total = clock.ElapsedMilliseconds;
            List<long> ms = [.. perLineMs];
            string timing = ms.Count == 0
                ? "(no per-line timing captured)"
                : $"lines solved={ms.Count}, total={total} ms, per-line avg={ms.Average():F0} ms, max={ms.Max()} ms";
            this.output_.WriteLine($"[component] {timing}");
        }
        finally
        {
            AsmLog.ClearSinks();
            AsmLog.Threshold = previousThreshold;
        }

        // It actually annotated real instructions...
        int writeLabelLines = rs.Lines.Values.Count(l => l.WriteLabel is not null);
        writeLabelLines.Should().BeGreaterThan(10,
            "the merge engine must produce write-side CodeLens labels across the example's instructions");

        // ...and PROVED at least one concrete register value (a silent all-unknown result is the failure
        // mode of a broken merge — the engine ran but proved nothing).
        rs.Lines.Values.Should().Contain(l => l.WriteLabel != null && l.WriteLabel.Contains("0x", StringComparison.Ordinal),
            "the merge engine must prove concrete values (e.g. add rax,rbx over known operands), not only unknowns");
    }
}
