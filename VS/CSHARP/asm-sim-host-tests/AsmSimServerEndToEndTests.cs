// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using FluentAssertions;

using Nerdbank.Streams;

using StreamJsonRpc;

using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// END-TO-END tests of the out-of-process protocol WITHOUT launching a process: the real
/// <see cref="AsmSimRpcServer"/> and a mirror client are wired to each other over an in-memory full-duplex
/// stream (the same JSON-RPC + serializer the real stdio link uses). This exercises the whole S2 path —
/// `documentChanged` → simulate → `ToResultSet` → `lineResults`/`simStatus` → `ApplyLineResults` — so a
/// break anywhere in the server, the protocol wiring, or the mirror is caught here.
///
/// <para>The S2c "shadow" test additionally proves the out-of-process result is IDENTICAL to running the
/// engine in-process — i.e. the RPC + streaming + mirror transport does not alter the simulation — which
/// is the parity gate for eventually flipping the editor default to out-of-process.</para>
///
/// Uses small/straight-line programs so Z3 is fast and the result is engine-independent.
/// </summary>
public class AsmSimServerEndToEndTests
{
    private sealed class MirrorClient(AsmSimulator mirror, TaskCompletionSource completed)
    {
        [JsonRpcMethod(AsmSimProtocol.LineResults)]
        public void OnLineResults(AsmSimLineResultsParams p)
            => mirror.ApplyLineResults(new Uri(p.Uri), p.Version, p.Lines);

        [JsonRpcMethod(AsmSimProtocol.SimStatus)]
        public void OnSimStatus(AsmSimStatusParams p)
        {
            if (string.Equals(p.State, "completed", StringComparison.Ordinal)) completed.TrySetResult();
        }
    }

    /// <summary>Simulate <paramref name="program"/> through the REAL server over an in-memory JSON-RPC
    /// link and return the mirror cache the streamed results landed in.</summary>
    private static async Task<AsmSimulator> RunOutOfProcessAsync(Uri uri, string program)
    {
        (Stream serverEnd, Stream clientEnd) = FullDuplexStream.CreatePair();

        var server = new AsmSimRpcServer();
        var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverEnd, serverEnd));
        server.Attach(serverRpc);
        serverRpc.AddLocalRpcTarget(server);
        serverRpc.StartListening();

        var mirror = new AsmSimulator();
        var completed = new TaskCompletionSource();
        var clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(clientEnd, clientEnd));
        clientRpc.AddLocalRpcTarget(new MirrorClient(mirror, completed));
        clientRpc.StartListening();

        await clientRpc.NotifyAsync(AsmSimProtocol.DocumentChanged, new AsmSimDocumentParams(uri.ToString(), 1, program, 0));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(120));

        clientRpc.Dispose();
        serverRpc.Dispose();
        server.Dispose();
        return mirror;
    }

    [Fact]
    public async Task DocumentChanged_RealServer_StreamsResultsIntoMirror()
    {
        var uri = new Uri("file:///e2e.asm");
        AsmSimulator mirror = await RunOutOfProcessAsync(uri, "mov rax, 0x10");

        var rs = mirror.ToResultSet(uri, "mirror");
        rs.Lines.Should().ContainKey(0);             // the server simulated line 0 and streamed it back
        // The editor path skips the full register dump (BeforeState/AfterState) and produces only the
        // read/write CodeLens labels. mov rax, 0x10 writes RAX, so the WRITE label proves RAX = 0x10.
        rs.Lines[0].WriteLabel.Should().NotBeNull();
        rs.Lines[0].WriteLabel!.Should().Contain("w:RAX").And.Contain("0x0000_0000_0000_0010");
    }

    [Fact]
    public async Task OutOfProcess_MatchesInProcess_OnStraightLine()
    {
        // S2c shadow parity: the same straight-line program, simulated in-process vs streamed back from the
        // server, must produce an IDENTICAL per-line result set — proving the RPC/streaming/mirror transport
        // is faithful (the gate for flipping the editor default). Straight-line ⇒ engine-independent, so the
        // comparison holds regardless of which engine each side runs.
        var uri = new Uri("file:///shadow.asm");
        string[] lines = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];

        // Match the server's editor path exactly: skip the full register dump so the parity is on the
        // read/write CodeLens labels + diagnostics — the actual editor output (SimResultComparer compares
        // before/after AND labels; both sides leave before/after null here).
        using var inproc = new AsmSimulator();
        inproc.SimulateSynchronouslyForTest(uri, lines, AsmSimulator.SimEngineMode.Component, computeFullState: false);
        SimResultSet inprocResult = inproc.ToResultSet(uri, "in-process");

        AsmSimulator mirror = await RunOutOfProcessAsync(uri, string.Join("\n", lines));
        SimResultSet outOfProcResult = mirror.ToResultSet(uri, "out-of-process");

        SimDiff diff = SimResultComparer.Compare(inprocResult, outOfProcResult);
        diff.IsEmpty.Should().BeTrue("in-process and out-of-process must agree on straight-line code:\n" + diff.ToReport());
    }
}
