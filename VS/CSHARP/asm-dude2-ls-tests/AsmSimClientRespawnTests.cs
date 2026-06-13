// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS.Tests;

using AsmSim.Host;

using FluentAssertions;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// Validates the WHOLE REASON the sim runs out-of-process (ASMSIM_SERVER_PLAN item 2): if the server
/// process dies (a Z3 access-violation/OOM in real life, a <c>Kill()</c> here), <see cref="AsmSimClient"/>
/// must respawn it and re-simulate, so the editor keeps working. Launches the real
/// <c>AsmSim.Server.exe</c>, proves it simulates, kills it, and asserts a NEW server PID comes up and
/// processes a subsequent document.
/// </summary>
public sealed class AsmSimClientRespawnTests
{
    private static string LocateServerExe()
    {
        string baseDir = AppContext.BaseDirectory;
        string config = baseDir.Replace('/', '\\').Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
        DirectoryInfo? d = new(baseDir);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "asm-sim-server")))
        {
            d = d.Parent;
        }
        return d == null ? string.Empty : Path.Combine(d.FullName, "asm-sim-server", "bin", config, "net10.0-windows", "AsmSim.Server.exe");
    }

    [Fact]
    public async Task ServerCrash_RespawnsAndKeepsSimulatingAsync()
    {
        string exe = LocateServerExe();
        File.Exists(exe).Should().BeTrue($"the out-of-process sim server must be built ({exe})");
        Environment.SetEnvironmentVariable("ASMDUDE_SIM_SERVER_PATH", exe);
        try
        {
            var mirror = new AsmSimulator();
            using AsmSimClient? client = AsmSimClient.TryCreate(mirror, new AsmSimSettings(true, "linear", "accept", 0, 5000));
            client.Should().NotBeNull("the server should launch");

            var uri = new Uri("file:///respawn.asm");
            string[] lines = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];

            Task SendAndWaitAsync(int timeoutSeconds)
            {
                var done = new TaskCompletionSource();
                client!.DocumentChanged(uri, lines, _ => { }, _ => done.TrySetResult());
                return done.Task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
            }

            // 1. The server simulates the document.
            await SendAndWaitAsync(60);
            int pid1 = client!.CurrentServerProcessId ?? throw new Xunit.Sdk.XunitException("no server pid");
            mirror.ToResultSet(uri, "before-crash").Lines.Should().ContainKey(2, "the add line was simulated");

            // 2. Crash it.
            Process.GetProcessById(pid1).Kill(entireProcessTree: true);

            // 3. The client must respawn a NEW process.
            int? pid2 = await WaitForNewPidAsync(client, pid1, TimeSpan.FromSeconds(30));
            pid2.Should().NotBeNull("AsmSimClient must respawn the server after it dies");
            pid2.Should().NotBe(pid1, "the respawn is a fresh process");

            // 4. The respawned server keeps working.
            await SendAndWaitAsync(60);
            mirror.ToResultSet(uri, "after-respawn").Lines.Should().ContainKey(2,
                "the respawned server must simulate the re-sent document");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASMDUDE_SIM_SERVER_PATH", null);
        }
    }

    [Fact]
    public async Task SettingsChanged_PushesToServerAndReSimulatesOpenDocsAsync()
    {
        string exe = LocateServerExe();
        File.Exists(exe).Should().BeTrue($"the out-of-process sim server must be built ({exe})");
        Environment.SetEnvironmentVariable("ASMDUDE_SIM_SERVER_PATH", exe);
        try
        {
            var mirror = new AsmSimulator();
            using AsmSimClient? client = AsmSimClient.TryCreate(mirror, new AsmSimSettings(true, "linear", "accept", 0, 5000));
            client.Should().NotBeNull();

            var uri = new Uri("file:///settings-client.asm");
            string[] lines = ["mov rax, 0x10", "mov rbx, 0x20", "add rax, rbx"];

            // A re-settable completion: AsmSimClient.SettingsChanged re-sends the open doc using THIS stored
            // callback, so we reset `current` before the push and await the re-simulation it triggers.
            TaskCompletionSource current = new();
            client!.DocumentChanged(uri, lines, _ => { }, _ => current.TrySetResult());
            await current.Task.WaitAsync(TimeSpan.FromSeconds(60));
            mirror.ToResultSet(uri, "initial").Lines.Should().ContainKey(2);

            // Push a settings change; the contract is: push to the server AND re-send the open document so it
            // re-simulates. If the re-send is dropped, the awaited completion below times out (test fails).
            current = new TaskCompletionSource();
            client.SettingsChanged(new AsmSimSettings(true, "component", "accept", 0, 5000));
            await current.Task.WaitAsync(TimeSpan.FromSeconds(60));

            mirror.ToResultSet(uri, "after-settings").Lines.Should().ContainKey(2,
                "the open document must be re-simulated after a settingsChanged push");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASMDUDE_SIM_SERVER_PATH", null);
        }
    }

    private static async Task<int?> WaitForNewPidAsync(AsmSimClient client, int oldPid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            int? pid = client.CurrentServerProcessId;
            if (pid.HasValue && pid.Value != oldPid)
            {
                return pid;
            }
            await Task.Delay(100);
        }
        return null;
    }
}
