// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host.Tests;

using FluentAssertions;

using StreamJsonRpc;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// Launches the ACTUAL standalone <c>AsmSim.Server.exe</c> as a child process and drives it over its REAL
/// stdin/stdout JSON-RPC link — the only automated coverage of the out-of-process path as it runs in a
/// deployment (Program.cs stdio host + framing + process lifecycle). The sibling
/// <see cref="AsmSimServerEndToEndTests"/> wires the same server in-memory; this proves the launched
/// process + pipe transport works, for BOTH the linear and the non-linear (component) engine selected via
/// the <c>ASMDUDE_SIM_ENGINE</c> env var (exactly how <c>AsmSimClient</c> launches it).
/// </summary>
public sealed class AsmSimServerProcessTests
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

    [Theory]
    [InlineData("linear")]
    [InlineData("component")]
    public async Task LaunchedServer_StreamsProvenResults_OverRealStdio(string engine)
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "AsmSim.Server.exe");
        File.Exists(exe).Should().BeTrue($"the sim server exe must be built next to the test ({exe})");

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false, // server logs to asmdude-simserver.log, not the JSON-RPC stdout
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["ASMDUDE_SIM_ENGINE"] = engine;

        Process? proc = null;
        JsonRpc? rpc = null;
        try
        {
            proc = Process.Start(psi);
            proc.Should().NotBeNull();

            var mirror = new AsmSimulator();
            var completed = new TaskCompletionSource();
            rpc = new JsonRpc(new HeaderDelimitedMessageHandler(proc!.StandardInput.BaseStream, proc.StandardOutput.BaseStream));
            rpc.AddLocalRpcTarget(new MirrorClient(mirror, completed));
            rpc.StartListening();

            // Straight-line ⇒ engine-independent: add rax,rbx over known operands proves RAX = 0x30.
            var uri = new Uri("file:///proc_straightline.asm");
            string program = "mov rax, 0x10\nmov rbx, 0x20\nadd rax, rbx";
            await rpc.NotifyAsync(AsmSimProtocol.DocumentChanged, new AsmSimDocumentParams(uri.ToString(), 1, program, 0));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(120));

            SimResultSet rs = mirror.ToResultSet(uri, "launched-server");
            rs.Lines.Should().ContainKey(2, "the server simulated the `add` line and streamed it back");
            rs.Lines[2].WriteLabel.Should().NotBeNull();
            rs.Lines[2].WriteLabel!.Should().Contain("w:RAX").And.Contain("0x0000_0000_0000_0030",
                $"the launched {engine} server must prove RAX = 0x30 over real stdio");
        }
        finally
        {
            try { rpc?.Dispose(); } catch { /* shutting down */ }
            try { if (proc is { HasExited: false }) proc.Kill(); } catch { /* already gone */ }
            proc?.Dispose();
        }
    }
}
