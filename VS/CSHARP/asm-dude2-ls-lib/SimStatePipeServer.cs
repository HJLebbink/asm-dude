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
/// Protocol (newline-delimited JSON, full-duplex):
/// <list type="bullet">
///   <item>Request:  <c>{"method":"getSimStates","uri":"file:///..."}</c></item>
///   <item>Response: <c>{"lines":{"5":"→RAX=0x10, ←RBX=0x4", ...}}</c></item>
///   <item>Server push: <c>{"method":"simStateUpdated","uri":"file:///..."}</c></item>
/// </list>
///
/// The server allows one persistent client connection at a time and automatically
/// accepts a new connection if the client disconnects.
/// </summary>
internal sealed class SimStatePipeServer : IDisposable
{
    private readonly string pipeName_;
    private readonly LspAsmSimulator simulator_;
    private readonly CancellationTokenSource cts_ = new();
    private volatile StreamWriter? clientWriter_;
    private readonly object writerLock_ = new();
    private int disposed_ = 0;

    internal SimStatePipeServer(LspAsmSimulator simulator)
    {
        this.simulator_ = simulator;
        this.pipeName_ = $"asmdude2-simstate-{Environment.ProcessId}";
    }

    /// <summary>The named pipe this server listens on (includes PID).</summary>
    internal string PipeName => this.pipeName_;

    /// <summary>Start accepting connections in the background.</summary>
    internal void Start()
    {
        _ = Task.Run(this.AcceptLoopAsync);
        AsmDudeLog.Debug($"[SimStatePipeServer] Started on pipe '{this.pipeName_}'");
    }

    /// <summary>
    /// Push a sim-state-updated notification to the connected VSIX client (if any).
    /// Called from the simulator's background thread whenever a line's sim state is updated.
    /// </summary>
    internal void NotifySimStateUpdated(Uri uri)
    {
        StreamWriter? w;
        lock (this.writerLock_)
            w = this.clientWriter_;
        if (w == null) return;
        try
        {
            string msg = JsonSerializer.Serialize(new { method = "simStateUpdated", uri = uri.ToString() });
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
                pipe?.Dispose();
            }

            // Brief pause before accepting the next connection
            if (!this.cts_.Token.IsCancellationRequested)
                await Task.Delay(200, this.cts_.Token).ConfigureAwait(false);
        }

        AsmDudeLog.Debug("[SimStatePipeServer] Accept loop ended");
    }

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
            }
            catch (Exception ex)
            {
                AsmDudeLog.Debug($"[SimStatePipeServer] Request parse error: {ex.Message}");
            }
        }

        AsmDudeLog.Debug("[SimStatePipeServer] Client disconnected");
    }
}
