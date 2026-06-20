// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using AsmTools;

using FluentAssertions;

using Nerdbank.Streams;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// Proves runtime <c>settingsChanged</c> (ASMSIM_SERVER_PLAN item 1): the engine can be switched on a
/// RUNNING server without a restart. The real <see cref="AsmSimRpcServer"/> is wired to a client over an
/// in-memory link (same JSON-RPC the stdio process uses); the server's own engine-selection log
/// (<c>"sim started: engine=…"</c>) is captured to confirm the switch actually took effect — not just that
/// the message was accepted.
/// </summary>
/// <summary>Tests that manipulate the GLOBAL <see cref="AsmLog"/> sinks/threshold must not run in
/// parallel with each other (one's <c>ClearSinks</c> would drop another's sink). Sim logs from OTHER
/// classes are excluded by filtering captured markers on the unique document name.</summary>
[CollectionDefinition("AsmLog", DisableParallelization = true)]
public sealed class AsmLogCollection;

[Collection("AsmLog")]
public sealed class AsmSimSettingsChangedTests
{
    private sealed class MirrorClient(AsmSimulator mirror)
    {
        public volatile TaskCompletionSource Completed = new();

        [JsonRpcMethod(AsmSimProtocol.LineResults)]
        public void OnLineResults(AsmSimLineResultsParams p)
            => mirror.ApplyLineResults(new Uri(p.Uri), p.Version, p.Lines);

        [JsonRpcMethod(AsmSimProtocol.SimStatus)]
        public void OnSimStatus(AsmSimStatusParams p)
        {
            if (string.Equals(p.State, "completed", StringComparison.Ordinal)) this.Completed.TrySetResult();
        }
    }

    [Fact]
    public async Task SettingsChanged_SwitchesEngine_OnTheRunningServer()
    {
        var engineMarkers = new List<string>();
        System.Threading.Lock markerLock = new();
        void Sink(AsmLogEntry e)
        {
            // Filter on this test's unique document name so concurrent sims from other classes can't
            // pollute the count (the "sim started" log ends with the uri's last segment).
            if (e.Message.Contains("sim started: engine=", StringComparison.Ordinal)
                && e.Message.Contains("settings.asm", StringComparison.Ordinal))
            {
                lock (markerLock) engineMarkers.Add(e.Message);
            }
        }

        AsmLogLevel previous = AsmLog.Threshold;
        AsmLog.Threshold = AsmLogLevel.Debug; // "sim started: engine=" is logged at Info
        AsmLog.AddSink(Sink);
        try
        {
            (Stream serverEnd, Stream clientEnd) = FullDuplexStream.CreatePair();
            using var server = new AsmSimRpcServer();
            var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverEnd, serverEnd));
            server.Attach(serverRpc);
            serverRpc.AddLocalRpcTarget(server);
            serverRpc.StartListening();

            var mirror = new AsmSimulator();
            var client = new MirrorClient(mirror);
            var clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(clientEnd, clientEnd));
            clientRpc.AddLocalRpcTarget(client);
            clientRpc.StartListening();

            var uri = new Uri("file:///settings.asm");
            const string program = "mov rax, 0x10\nmov rbx, 0x20\nadd rax, rbx";

            async Task SimulateAsync(long version)
            {
                client.Completed = new TaskCompletionSource();
                await clientRpc.NotifyAsync(AsmSimProtocol.DocumentChanged, new AsmSimDocumentParams(uri.ToString(), version, program, 0));
                await client.Completed.Task.WaitAsync(TimeSpan.FromSeconds(60));
            }

            // Switch the running server to the component engine, then simulate.
            await clientRpc.NotifyAsync(AsmSimProtocol.SettingsChanged, new AsmSimSettings(true, "component", "accept", 0, 5000));
            await SimulateAsync(1);

            // Switch back to linear, then simulate again.
            await clientRpc.NotifyAsync(AsmSimProtocol.SettingsChanged, new AsmSimSettings(true, "linear", "accept", 0, 5000));
            await SimulateAsync(2);

            clientRpc.Dispose();
            serverRpc.Dispose();

            // The server's own engine log proves each run used the engine the settingsChanged selected.
            List<string> markers;
            lock (markerLock) markers = [.. engineMarkers];
            markers.Should().HaveCount(2, "two documents were simulated");
            markers[0].Should().Contain("engine=Component", "the first settingsChanged switched the running server to the merge engine");
            markers[1].Should().Contain("engine=Linear", "the second settingsChanged switched it back — no restart needed");
        }
        finally
        {
            AsmLog.ClearSinks();
            AsmLog.Threshold = previous;
        }
    }
}
