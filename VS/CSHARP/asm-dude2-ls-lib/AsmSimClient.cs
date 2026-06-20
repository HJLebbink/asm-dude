// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS;

using AsmSim.Host;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

/// <summary>
/// Out-of-process simulation client (ASMSIM_SERVER_PLAN S2b/S2c). Launches <c>AsmSim.Server.exe</c>, talks
/// <see cref="AsmSimProtocol"/> over its stdio, and MIRRORS the streamed per-line results into the LSP
/// server's <see cref="AsmSimulator"/> cache — which stays the single read source, so hover/CodeLens/
/// diagnostics read paths are UNCHANGED (the simulator just doesn't run Z3 itself in this mode).
///
/// <para><b>Crash robustness</b> (the reason for the split): if the server process dies (Z3 AV/OOM), the
/// JSON-RPC connection drops, this RESPAWNS the server and RE-SENDS every open document, so the editor
/// keeps working. Respawns are capped (a crash loop gives up rather than thrashing).</para>
///
/// <para><b>Settings</b> are applied by launching the server with the engine/loop/parallelism env vars the
/// server reads at startup (runtime <c>settingsChanged</c> re-push is future work).</para>
///
/// Opt-in via <c>ASMDUDE_SIM_OUTOFPROC=1</c>; <see cref="TryCreate"/> returns null if the server can't be
/// launched, and the caller falls back to the in-process engine. The exe is located via
/// <c>ASMDUDE_SIM_SERVER_PATH</c> or next to the LSP exe.
/// </summary>
internal sealed class AsmSimClient : IDisposable
{
    private sealed record OpenDoc(long Version, IReadOnlyList<string> Lines, Action<Uri> OnProgress, Action<Uri> OnCompleted);

    private const int MaxRestarts = 5;
    private const int RestartWindowMs = 60_000;

    private readonly AsmSimulator mirror_;
    private AsmSimSettings settings_; // mutable: updated on settingsChanged so respawns use the latest engine
    private readonly string exePath_;
    private readonly System.Threading.Lock lock_ = new();
    private readonly Dictionary<string, OpenDoc> openDocs_ = []; // tracked so we can re-send after a respawn

    private Process? process_;
    private JsonRpc? rpc_;
    private long versionCounter_;
    private int restartCount_;
    private long restartWindowStartMs_;
    private bool disposed_;

    private AsmSimClient(AsmSimulator mirror, AsmSimSettings settings, string exePath)
    {
        this.mirror_ = mirror;
        this.settings_ = settings;
        this.exePath_ = exePath;
    }

    /// <summary>The PID of the currently-connected server process, or null if none. Test hook for the
    /// crash-respawn test (asserts the PID changes after a kill).</summary>
    internal int? CurrentServerProcessId
    {
        get
        {
            lock (this.lock_)
            {
                // process_.Id throws if the old process was disposed mid-respawn — treat as "no current pid".
                try { return this.process_?.Id; }
                catch (InvalidOperationException) { return null; }
            }
        }
    }

    /// <summary>Launch the server and connect, or return null so the caller stays in-process.</summary>
    internal static AsmSimClient? TryCreate(AsmSimulator mirror, AsmSimSettings settings)
    {
        string? exe = LocateServerExe();
        if (exe == null)
        {
            AsmDudeLog.Warning("AsmSimClient: AsmSim.Server.exe not found (set ASMDUDE_SIM_SERVER_PATH or deploy it) — staying in-process");
            return null;
        }
        var client = new AsmSimClient(mirror, settings, exe);
        return client.Connect() ? client : null;
    }

    private static string? LocateServerExe()
    {
        string? env = Environment.GetEnvironmentVariable("ASMDUDE_SIM_SERVER_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
        string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location) ?? ".";
        string candidate = Path.Combine(dir, "AsmSim.Server.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>(Re)launch the server process + JSON-RPC connection. Returns false on failure.</summary>
    private bool Connect()
    {
        Process? proc = null;
        try
        {
            var psi = new ProcessStartInfo(this.exePath_)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false, // the server logs to its own file (asmdude-simserver.log)
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(this.exePath_) ?? Environment.CurrentDirectory,
            };
            // Apply settings by launching with the env vars the server reads at startup.
            psi.Environment["ASMDUDE_SIM_ENGINE"] = this.settings_.Engine;
            psi.Environment["ASMDUDE_SIM_LOOP"] = this.settings_.LoopHandling;
            if (this.settings_.Parallelism > 0)
            {
                psi.Environment["ASMDUDE_SIM_PARALLEL"] = this.settings_.Parallelism.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            proc = Process.Start(psi);
            if (proc == null)
            {
                AsmDudeLog.Warning("AsmSimClient: failed to start AsmSim.Server.exe — staying in-process");
                return false;
            }

            var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(proc.StandardInput.BaseStream, proc.StandardOutput.BaseStream));
            rpc.AddLocalRpcTarget(this);
            rpc.Disconnected += this.OnDisconnected;
            rpc.StartListening();
            _ = rpc.InvokeAsync<AsmSimInitResult>(AsmSimProtocol.Initialize, new AsmSimInitParams(Environment.ProcessId, this.settings_));

            lock (this.lock_)
            {
                this.process_ = proc;
                this.rpc_ = rpc;
            }
            AsmDudeLog.Info($"AsmSimClient: launched AsmSim.Server (pid {proc.Id}, engine={this.settings_.Engine}): {this.exePath_}");
            return true;
        }
        catch (Exception ex)
        {
            AsmDudeLog.Error($"AsmSimClient.Connect failed: {ex.GetType().Name}: {ex.Message}");
            try { proc?.Kill(); } catch { }
            return false;
        }
    }

    /// <summary>The connection dropped (server crashed or exited). Respawn (capped) and re-send open docs.</summary>
    private void OnDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (this.disposed_) return;

        long now = Environment.TickCount64;
        lock (this.lock_)
        {
            if (now - this.restartWindowStartMs_ > RestartWindowMs)
            {
                this.restartWindowStartMs_ = now;
                this.restartCount_ = 0;
            }
            if (this.restartCount_ >= MaxRestarts)
            {
                AsmDudeLog.Error($"AsmSimClient: AsmSim.Server crashed {this.restartCount_}× within {RestartWindowMs}ms — giving up (no out-of-proc sim until LSP restart). Reason: {e.Description}");
                return;
            }
            this.restartCount_++;
        }

        AsmDudeLog.Warning($"AsmSimClient: AsmSim.Server connection lost ({e.Description}) — respawning (#{this.restartCount_}) and re-sending open docs");
        try { this.process_?.Dispose(); } catch { }

        if (!this.Connect()) return;

        // Re-send every open document so the fresh server rebuilds their state (bump versions so any
        // late results from the dead server are dropped).
        List<(Uri uri, OpenDoc doc)> toResend;
        lock (this.lock_)
        {
            toResend = [];
            foreach (var (key, doc) in this.openDocs_) toResend.Add((new Uri(key), doc));
        }
        foreach (var (uri, doc) in toResend)
        {
            this.DocumentChanged(uri, doc.Lines, doc.OnProgress, doc.OnCompleted);
        }
    }

    /// <summary>Send a document for (re)simulation; results stream back into the mirror cache. The
    /// callbacks fire exactly like the in-process engine's (drive diagnostics + CodeLens refresh).</summary>
    internal void DocumentChanged(Uri uri, IReadOnlyList<string> lines, Action<Uri> onProgress, Action<Uri> onCompleted)
    {
        string key = uri.ToString();
        long version;
        JsonRpc? rpc;
        lock (this.lock_)
        {
            version = ++this.versionCounter_;
            this.openDocs_[key] = new OpenDoc(version, lines, onProgress, onCompleted);
            rpc = this.rpc_;
        }
        string text = string.Join("\n", lines);
        _ = rpc?.NotifyAsync(AsmSimProtocol.DocumentChanged, new AsmSimDocumentParams(key, version, text, 0));
    }

    internal void DocumentClosed(Uri uri)
    {
        string key = uri.ToString();
        JsonRpc? rpc;
        lock (this.lock_)
        {
            this.openDocs_.Remove(key);
            rpc = this.rpc_;
        }
        _ = rpc?.NotifyAsync(AsmSimProtocol.DocumentClosed, key);
    }

    /// <summary>Push changed settings to the running server (it applies engine/loop in place) and re-send
    /// every open document so they re-simulate with the new engine. Also updates the settings used for any
    /// future respawn. No-op if the server isn't connected yet.</summary>
    internal void SettingsChanged(AsmSimSettings settings)
    {
        JsonRpc? rpc;
        List<(Uri uri, OpenDoc doc)> toResend;
        lock (this.lock_)
        {
            this.settings_ = settings;
            rpc = this.rpc_;
            toResend = [];
            foreach (var (key, doc) in this.openDocs_) toResend.Add((new Uri(key), doc));
        }
        if (rpc == null) return;

        _ = rpc.NotifyAsync(AsmSimProtocol.SettingsChanged, settings);
        foreach (var (uri, doc) in toResend)
        {
            this.DocumentChanged(uri, doc.Lines, doc.OnProgress, doc.OnCompleted);
        }
        AsmDudeLog.Info($"AsmSimClient: pushed settingsChanged (engine={settings.Engine}) and re-sent {toResend.Count} open doc(s)");
    }

    // ── Server → client notifications ───────────────────────────────────────────
    [JsonRpcMethod(AsmSimProtocol.LineResults)]
    public void OnLineResults(AsmSimLineResultsParams parameters)
    {
        Action<Uri>? onProgress;
        lock (this.lock_)
        {
            // Drop a snapshot for a superseded version (a newer edit bumped the version).
            if (!this.openDocs_.TryGetValue(parameters.Uri, out OpenDoc? doc) || doc.Version != parameters.Version)
            {
                return;
            }
            onProgress = doc.OnProgress;
        }
        var uri = new Uri(parameters.Uri);
        this.mirror_.ApplyLineResults(uri, parameters.Version, parameters.Lines);
        onProgress.Invoke(uri);
    }

    [JsonRpcMethod(AsmSimProtocol.SimStatus)]
    public void OnSimStatus(AsmSimStatusParams parameters)
    {
        if (!string.Equals(parameters.State, "completed", StringComparison.Ordinal)) return;
        Action<Uri>? onCompleted;
        lock (this.lock_)
        {
            onCompleted = this.openDocs_.TryGetValue(parameters.Uri, out OpenDoc? doc) && doc.Version == parameters.Version
                ? doc.OnCompleted : null;
        }
        onCompleted?.Invoke(new Uri(parameters.Uri));
    }

    public void Dispose()
    {
        this.disposed_ = true;
        JsonRpc? rpc;
        Process? proc;
        lock (this.lock_) { rpc = this.rpc_; proc = this.process_; }
        try { _ = rpc?.NotifyAsync(AsmSimProtocol.Shutdown); } catch { }
        try { rpc?.Dispose(); } catch { }
        try { if (proc is { HasExited: false }) proc.Kill(); } catch { }
        try { proc?.Dispose(); } catch { }
    }
}
