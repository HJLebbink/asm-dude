// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using FluentAssertions;

using System;
using System.Collections.Generic;

using Xunit;

/// <summary>
/// Tests for <see cref="AsmSimulator.ApplyLineResults"/> — the MIRROR write path used in out-of-process
/// mode (ASMSIM_SERVER_PLAN S2b): the sim server streams per-line results, the LSP server applies them to
/// its <see cref="AsmSimulator"/> cache, and the unchanged read paths serve them. No Z3, no process — pure
/// cache round-trip. If a future change breaks how a streamed line lands in the cache (or how its
/// diagnostics parse, or the stale/snapshot semantics), one of these fails.
/// </summary>
public class AsmSimMirrorTests
{
    private static readonly Uri Uri = new("file:///mirror_test.asm");

    private static AsmSimLineDto Line(int line, string? before = null, string? after = null,
        string? read = null, string? write = null, IReadOnlyList<string>? diags = null)
        => new(line, before, after, read, write, diags);

    [Fact]
    public void ApplyLineResults_RoundTripsBeforeAfterAndLabels()
    {
        var sim = new AsmSimulator();
        sim.ApplyLineResults(Uri, 1,
        [
            Line(2, before: "RAX=0x_0000_0000_0000", after: "RAX=0x_0000_0000_0010", read: "r:RAX", write: "w:RAX"),
            Line(5, after: "RBX=0x_0000_0000_0004"),
        ]);

        sim.GetRegisterStatesBeforeLine(Uri, 2).Should().Be("RAX=0x_0000_0000_0000");
        sim.GetRegisterStatesAfterLine(Uri, 2).Should().Be("RAX=0x_0000_0000_0010");
        sim.GetRegisterStatesAfterLine(Uri, 5).Should().Be("RBX=0x_0000_0000_0004");

        var rs = sim.ToResultSet(Uri, "mirror");
        rs.Lines[2].ReadLabel.Should().Be("r:RAX");
        rs.Lines[2].WriteLabel.Should().Be("w:RAX");
        rs.Lines.Should().ContainKey(5);
    }

    [Fact]
    public void ApplyLineResults_ParsesDiagnosticKindAndMessage()
    {
        var sim = new AsmSimulator();
        // The server flattens each diagnostic to "Kind:Message" (see ToResultSet); the mirror parses it back.
        sim.ApplyLineResults(Uri, 1,
        [
            Line(3, diags: ["SyntaxError:bad operand"]),
            Line(4, diags: ["Unreachable:never runs"]),
        ]);

        var diags = sim.GetDiagnostics(Uri);
        diags.Should().HaveCount(2);
        diags.Should().Contain(d => d.Line == 3 && d.Kind == SimDiagnosticKind.SyntaxError && d.Message == "bad operand");
        diags.Should().Contain(d => d.Line == 4 && d.Kind == SimDiagnosticKind.Unreachable && d.Message == "never runs");
    }

    [Fact]
    public void ApplyLineResults_SnapshotReplacesPreviousContent()
    {
        var sim = new AsmSimulator();
        sim.ApplyLineResults(Uri, 1, [Line(2, after: "RAX=1"), Line(3, diags: ["SyntaxError:x"])]);

        // A later snapshot of the SAME doc is authoritative: line 2 gone, line 7 added, old diag gone.
        sim.ApplyLineResults(Uri, 2, [Line(7, after: "RBX=2")]);

        var rs = sim.ToResultSet(Uri, "mirror");
        rs.Lines.Keys.Should().BeEquivalentTo([7]);
        rs.Lines[7].AfterState.Should().Be("RBX=2");
        sim.GetDiagnostics(Uri).Should().BeEmpty(); // the line-3 diagnostic was replaced away
    }

    [Fact]
    public void ApplyLineResults_StaleVersionIsDropped()
    {
        var sim = new AsmSimulator();
        sim.ApplyLineResults(Uri, 5, [Line(1, after: "current")]);

        // An older snapshot (a late message from a superseded edit) must NOT clobber the newer one.
        sim.ApplyLineResults(Uri, 3, [Line(1, after: "stale")]);

        sim.GetRegisterStatesAfterLine(Uri, 1).Should().Be("current");
    }

    [Fact]
    public void ApplyLineResults_EmptySnapshotClearsTheDocument()
    {
        var sim = new AsmSimulator();
        sim.ApplyLineResults(Uri, 1, [Line(2, after: "RAX=1")]);
        sim.ApplyLineResults(Uri, 2, []);

        sim.ToResultSet(Uri, "mirror").Lines.Should().BeEmpty();
        sim.GetRegisterStatesAfterLine(Uri, 2).Should().BeNull();
    }
}
