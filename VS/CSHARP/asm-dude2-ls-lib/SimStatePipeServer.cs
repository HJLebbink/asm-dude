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

namespace AsmDude2LS;

using AsmSim.Host;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Named-pipe server that lets the VSIX CodeLens tagger fetch sim-state data
/// from the LSP server process without using temp files.
///
/// Pipe name: <c>asmdude2-simstate-{pid}</c>. The VSIX captures the LSP server PID
/// when it starts the process and connects to this well-known name.
///
/// Protocol (newline-delimited JSON, full-duplex). One request is in flight at a time
/// (the client serializes them); each request marker key also identifies its response:
/// <list type="bullet">
///   <item>Sim state — Request: <c>{"method":"getSimStates","uri":"file:///..."}</c>
///         → Response: <c>{"lines":{"5":"→RAX=0x10, ←RBX=0x4", ...}}</c></item>
///   <item>CodeLens — Request: <c>{"method":"getCodeLensData","uri":"file:///..."}</c>
///         → Response: <c>{"codeLensData":[{"Label":"l","DefinitionLine":0,"DefinitionColumn":0,"DefinitionLength":1,"ReferenceLines":[2]}]}</c></item>
///   <item>Doc URL — Request: <c>{"method":"getMnemonicUrl","mnemonic":"MOV"}</c>
///         → Response: <c>{"mnemonicUrl":"https://.../MOV"}</c> (value null if unknown)</item>
///   <item>Server push: <c>{"method":"simStateUpdated","uri":"file:///..."}</c></item>
/// </list>
///
/// Why a side pipe and not LSP: the VisualStudio.Extensibility extension parts (CodeLens tagger,
/// commands) have no access to the LSP connection, so the server pushes/serves this data here
/// instead. All payloads are computed by the server — the VSIX only renders them.
///
/// The server allows one persistent client connection at a time and automatically
/// accepts a new connection if the client disconnects.
/// </summary>
internal sealed class SimStatePipeServer : IDisposable
{
    private readonly string pipeName_;
    private readonly AsmSimulator simulator_;
    private readonly CancellationTokenSource cts_ = new();
    private volatile StreamWriter? clientWriter_;
    private readonly System.Threading.Lock writerLock_ = new();
    private int disposed_ = 0;

    internal SimStatePipeServer(AsmSimulator simulator)
    {
        this.simulator_ = simulator;
        this.pipeName_ = $"asmdude2-simstate-{Environment.ProcessId}";
    }

    /// <summary>The named pipe this server listens on (includes PID).</summary>
    internal string PipeName => this.pipeName_;

    /// <summary>
    /// Supplies label CodeLens data (definition position + jump/call reference lines) for a
    /// document URI. Set by <see cref="LanguageServer"/>. The VSIX CodeLens tagger fetches this
    /// over the pipe instead of re-parsing the document, so reference counting lives only on the
    /// (multithreaded) server. Null until wired.
    /// </summary>
    internal Func<string, AsmCodeLensData[]>? CodeLensDataProvider { get; set; }

    /// <summary>
    /// Resolves a mnemonic to its documentation URL (configured base + html ref). Set by
    /// <see cref="LanguageServer"/>. The VSIX "open documentation" command fetches this over the
    /// pipe instead of re-reading the signature files, so they are parsed only on the server.
    /// Null until wired.
    /// </summary>
    internal Func<string, string?>? MnemonicUrlProvider { get; set; }

    /// <summary>Start accepting connections in the background.</summary>
    internal void Start()
    {
        _ = Task.Run(this.AcceptLoopAsync);
        AsmDudeLog.Debug($"[SimStatePipeServer] Started on pipe '{this.pipeName_}'");
    }

    /// <summary>
    /// Push a sim-state-updated notification to the connected VSIX client (if any).
    /// Called from the simulator's background thread whenever a line's sim state is updated.
    /// <paramref name="completed"/> marks the FINAL push of a simulation run (vs an intermediate
    /// progress push): the client uses it to force a full re-materialization of every lens, because
    /// VS may have silently dropped lenses during the long, load-heavy sim and the incremental
    /// publisher would otherwise suppress re-publishing lines whose content did not change.
    /// </summary>
    internal void NotifySimStateUpdated(Uri uri, bool completed = false)
    {
        StreamWriter? w;
        lock (this.writerLock_)
            w = this.clientWriter_;
        if (w == null) return;
        try
        {
            string msg = JsonSerializer.Serialize(new { method = "simStateUpdated", uri = uri.ToString(), completed });
            // WriteLine is not thread-safe; use lock
            lock (this.writerLock_)
                this.clientWriter_?.WriteLine(msg);
        }
        catch
        {
            // Client disconnected — the AcceptLoop will clean up
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed_, 1) != 0) return;
        this.cts_.Cancel();
        lock (this.writerLock_) this.clientWriter_ = null;
        this.cts_.Dispose();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task AcceptLoopAsync()
    {
        while (!this.cts_.Token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    this.pipeName_,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                AsmDudeLog.Debug("[SimStatePipeServer] Waiting for client connection...");
                await pipe.WaitForConnectionAsync(this.cts_.Token).ConfigureAwait(false);
                AsmDudeLog.Debug("[SimStatePipeServer] Client connected");

                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

                lock (this.writerLock_)
                    this.clientWriter_ = writer;

                await this.ServeClientAsync(pipe, reader, writer).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AsmDudeLog.Debug($"[SimStatePipeServer] Connection error: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                lock (this.writerLock_)
                    this.clientWriter_ = null;
                if (pipe != null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }
            }

            // Brief pause before accepting the next connection
            if (!this.cts_.Token.IsCancellationRequested)
                await Task.Delay(200, this.cts_.Token).ConfigureAwait(false);
        }

        AsmDudeLog.Debug("[SimStatePipeServer] Accept loop ended");
    }

    // VSTHRD103: the response writes are intentionally synchronous — serialized by writerLock_ (shared with
    // the background NotifySimStateUpdated push), and you cannot await inside a lock.
#pragma warning disable VSTHRD103
    private async Task ServeClientAsync(NamedPipeServerStream pipe, StreamReader reader, StreamWriter writer)
    {
        while (!this.cts_.Token.IsCancellationRequested && pipe.IsConnected)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(this.cts_.Token).ConfigureAwait(false);
            }
            catch
            {
                break; // client disconnected or cancelled
            }

            if (line == null) break; // EOF

            try
            {
                using var doc = JsonDocument.Parse(line);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("method", out var methodEl)) continue;
                string method = methodEl.GetString() ?? string.Empty;

                if (method == "getSimStates")
                {
                    string uriStr = root.TryGetProperty("uri", out var uriEl)
                        ? uriEl.GetString() ?? string.Empty
                        : string.Empty;

                    Dictionary<int, string> states = string.IsNullOrEmpty(uriStr)
                        ? []
                        : this.simulator_.GetSimStatesSummary(new Uri(uriStr));

                    // Serialize as {"lines":{"5":"→RAX=...", "7":"..."}}
                    var linesDict = new Dictionary<string, string>(states.Count);
                    foreach (var (idx, label) in states)
                        linesDict[idx.ToString()] = label;

                    string response = JsonSerializer.Serialize(new { lines = linesDict });
                    lock (this.writerLock_)
                        writer.WriteLine(response);
                }
                else if (method == "getCodeLensData")
                {
                    string uriStr = root.TryGetProperty("uri", out var uriEl)
                        ? uriEl.GetString() ?? string.Empty
                        : string.Empty;

                    AsmCodeLensData[] data = (string.IsNullOrEmpty(uriStr) || this.CodeLensDataProvider == null)
                        ? []
                        : this.CodeLensDataProvider(uriStr);

                    string response = JsonSerializer.Serialize(new { codeLensData = data });
                    lock (this.writerLock_)
                        writer.WriteLine(response);
                }
                else if (method == "getMnemonicUrl")
                {
                    string mnemonic = root.TryGetProperty("mnemonic", out var mnEl)
                        ? mnEl.GetString() ?? string.Empty
                        : string.Empty;

                    string? url = (string.IsNullOrEmpty(mnemonic) || this.MnemonicUrlProvider == null)
                        ? null
                        : this.MnemonicUrlProvider(mnemonic);

                    string response = JsonSerializer.Serialize(new { mnemonicUrl = url });
                    lock (this.writerLock_)
                        writer.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                AsmDudeLog.Debug($"[SimStatePipeServer] Request parse error: {ex.Message}");
            }
        }

        AsmDudeLog.Debug("[SimStatePipeServer] Client disconnected");
    }
#pragma warning restore VSTHRD103
}
