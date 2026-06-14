// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host;

using AsmSim;

using AsmTools;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// The JSON-RPC target of <c>AsmSim.LS.exe</c>: hosts ONE <see cref="AsmSimulator"/> and exposes the
/// <see cref="AsmSimProtocol"/> over the connection. <c>documentChanged</c> drives the simulator; the
/// simulator's throttled <c>onProgress</c>/<c>onCompleted</c> read the cache (<see cref="AsmSimulator.ToResultSet"/>)
/// and STREAM a per-line batch back to the LSP client, which mirrors it. Results carry the document
/// version so the client (and this server) can drop superseded ones.
/// </summary>
internal sealed class AsmSimRpcServer : IDisposable
{
    private readonly AsmSimulator simulator_ = new();
    private readonly object lock_ = new();
    private readonly Dictionary<string, long> versions_ = [];          // uri → latest version
    private readonly Dictionary<string, Stopwatch> clocks_ = [];       // uri → wall clock for the current run
    private JsonRpc? rpc_;

    /// <summary>Wire the connection after it's created (so notifications can be sent back).</summary>
    public void Attach(JsonRpc rpc) => this.rpc_ = rpc;

    [JsonRpcMethod(AsmSimProtocol.Initialize)]
    public AsmSimInitResult Initialize(AsmSimInitParams parameters)
    {
        AsmLog.Banner("ASMSIM", $"AsmSim.LS server: initialize from pid={parameters.ClientProcessId}, engine={parameters.Settings.Engine}, incremental={parameters.Settings.Incremental}");
        // Apply the initial settings so the first simulation already honors them (the client also pushes a
        // settingsChanged shortly after, but this makes startup self-consistent).
        this.simulator_.ApplySettings(parameters.Settings.Engine, parameters.Settings.LoopHandling, parameters.Settings.Incremental, parameters.Settings.ShowRedundant);
        return new AsmSimInitResult(parameters.Settings.Engine, parameters.Settings.Parallelism, "1.0.0.0");
    }

    [JsonRpcMethod(AsmSimProtocol.SettingsChanged)]
    public void SettingsChanged(AsmSimSettings settings)
    {
        AsmLog.Info("ASMSIM", $"settingsChanged: engine={settings.Engine}, loop={settings.LoopHandling}, parallel={settings.Parallelism}, incremental={settings.Incremental}, showRedundant={settings.ShowRedundant}");
        // Apply at runtime; the client re-sends open documents so they re-simulate with the new settings.
        this.simulator_.ApplySettings(settings.Engine, settings.LoopHandling, settings.Incremental, settings.ShowRedundant);
    }

    [JsonRpcMethod(AsmSimProtocol.DocumentChanged)]
    public void DocumentChanged(AsmSimDocumentParams parameters)
    {
        Uri uri = new(parameters.Uri);
        string[] lines = parameters.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        long version = parameters.Version;

        lock (this.lock_)
        {
            this.versions_[parameters.Uri] = version;
            this.clocks_[parameters.Uri] = Stopwatch.StartNew();
        }

        this.SendStatus(parameters.Uri, version, "started");
        this.simulator_.InvalidateAndSimulate(uri, lines,
            onCompleted: u => this.OnSimEvent(u, version, completed: true),
            onProgress: u => this.OnSimEvent(u, version, completed: false));
    }

    [JsonRpcMethod(AsmSimProtocol.DocumentClosed)]
    public void DocumentClosed(string uri)
    {
        lock (this.lock_)
        {
            this.versions_.Remove(uri);
            this.clocks_.Remove(uri);
        }
        this.simulator_.CancelAndRemove(new Uri(uri));
    }

    [JsonRpcMethod(AsmSimProtocol.Shutdown)]
    public void Shutdown()
    {
        AsmLog.Info("ASMSIM", "shutdown requested");
        this.simulator_.Dispose();
    }

    // Throttled progress / completion: snapshot the cache and stream the resolved lines.
    private void OnSimEvent(Uri uri, long version, bool completed)
    {
        string key = uri.ToString();
        lock (this.lock_)
        {
            if (this.versions_.TryGetValue(key, out long cur) && cur != version)
            {
                return; // superseded by a newer edit
            }
        }

        SimResultSet rs = this.simulator_.ToResultSet(uri, "component");
        var lines = rs.Lines
            .Select(kv => new AsmSimLineDto(
                kv.Key,
                kv.Value.BeforeState,
                kv.Value.AfterState,
                kv.Value.ReadLabel,
                kv.Value.WriteLabel,
                kv.Value.Diagnostics?.ToList()))
            .ToList();

        _ = this.rpc_?.NotifyAsync(AsmSimProtocol.LineResults,
            new AsmSimLineResultsParams(key, version, lines));

        if (completed)
        {
            int diagCount = lines.Sum(l => l.Diagnostics?.Count ?? 0);
            long ms;
            lock (this.lock_) ms = this.clocks_.TryGetValue(key, out Stopwatch? sw) ? sw.ElapsedMilliseconds : 0;
            this.SendStatus(key, version, "completed", lines.Count, diagCount, ms);
        }
    }

    private void SendStatus(string uri, long version, string state, int linesWritten = 0, int diagCount = 0, long totalMs = 0)
        => _ = this.rpc_?.NotifyAsync(AsmSimProtocol.SimStatus,
            new AsmSimStatusParams(uri, version, state, linesWritten, diagCount, totalMs));

    public void Dispose() => this.simulator_.Dispose();
}
