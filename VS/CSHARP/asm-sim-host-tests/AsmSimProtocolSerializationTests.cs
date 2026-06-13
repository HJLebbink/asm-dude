// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using FluentAssertions;

using System.Text.Json;

using Xunit;

/// <summary>
/// The AsmSim out-of-process protocol (<see cref="AsmSimProtocol"/>) travels as JSON-RPC, which uses
/// System.Text.Json. These prove every wire DTO round-trips through STJ — the serializer StreamJsonRpc
/// actually uses — so a record that can't deserialize (missing ctor match, an unsupported member type)
/// is caught here, not as a silent runtime desync between the two processes.
/// </summary>
public class AsmSimProtocolSerializationTests
{
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    [Fact]
    public void LineResults_RoundTrips_WithNullsAndDiagnostics()
    {
        var p = new AsmSimLineResultsParams("file:///a.asm", 7,
        [
            new AsmSimLineDto(2, "RAX=0x10", "RAX=0x20", "r:RAX", "w:RBX", ["SyntaxError:oops"]),
            new AsmSimLineDto(5, null, null, null, null, null),
        ]);

        var back = RoundTrip(p);

        back.Uri.Should().Be("file:///a.asm");
        back.Version.Should().Be(7);
        back.Lines.Should().HaveCount(2);
        back.Lines[0].Before.Should().Be("RAX=0x10");
        back.Lines[0].After.Should().Be("RAX=0x20");
        back.Lines[0].ReadLabel.Should().Be("r:RAX");
        back.Lines[0].WriteLabel.Should().Be("w:RBX");
        back.Lines[0].Diagnostics.Should().ContainSingle().Which.Should().Be("SyntaxError:oops");
        back.Lines[1].Before.Should().BeNull();
        back.Lines[1].Diagnostics.Should().BeNull();
    }

    [Fact]
    public void DocumentParams_RoundTrips()
    {
        var back = RoundTrip(new AsmSimDocumentParams("file:///b.asm", 42, "mov rax, 1\r\nnop", 8));
        back.Uri.Should().Be("file:///b.asm");
        back.Version.Should().Be(42);
        back.Text.Should().Be("mov rax, 1\r\nnop");
        back.Assembler.Should().Be(8);
    }

    [Fact]
    public void Settings_RoundTrips()
    {
        var back = RoundTrip(new AsmSimSettings(AsmSimOn: true, Engine: "component", LoopHandling: "fixpoint", Parallelism: 8, Z3TimeoutMs: 5000));
        back.AsmSimOn.Should().BeTrue();
        back.Engine.Should().Be("component");
        back.LoopHandling.Should().Be("fixpoint");
        back.Parallelism.Should().Be(8);
        back.Z3TimeoutMs.Should().Be(5000);
    }

    [Fact]
    public void Status_RoundTrips()
    {
        var back = RoundTrip(new AsmSimStatusParams("file:///c.asm", 3, "completed", 50, 9, 12345));
        back.State.Should().Be("completed");
        back.LinesWritten.Should().Be(50);
        back.DiagCount.Should().Be(9);
        back.TotalMs.Should().Be(12345);
    }

    [Fact]
    public void InitParamsAndResult_RoundTrip()
    {
        var pBack = RoundTrip(new AsmSimInitParams(1234, new AsmSimSettings(true, "linear", "accept", 4, 5000)));
        pBack.ClientProcessId.Should().Be(1234);
        pBack.Settings.Engine.Should().Be("linear");

        var rBack = RoundTrip(new AsmSimInitResult("component", 8, "1.0.0.0"));
        rBack.Engine.Should().Be("component");
        rBack.Parallelism.Should().Be(8);
        rBack.ServerVersion.Should().Be("1.0.0.0");
    }
}
