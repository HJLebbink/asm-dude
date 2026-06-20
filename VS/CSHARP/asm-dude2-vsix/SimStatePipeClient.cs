// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

// RS0030: VS-Threading bans SemaphoreSlim because waiting on it on the VS UI thread can hang VS. This is an
// OUT-OF-PROCESS extension (runs in a ServiceHub host, not the VS UI thread — see CLAUDE.md), so there is no
// UI thread to hang and no JoinableTaskContext to coordinate with; ReentrantSemaphore offers no benefit here.
// requestSem_ just serializes async pipe requests off any UI thread.
#pragma warning disable RS0030 // Do not use banned APIs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Singleton named-pipe client that connects to the LSP server's <c>SimStatePipeServer</c>.
/// Provides <see cref="GetSimStatesAsync"/> for fetching sim-state labels and raises
/// <see cref="SimStateUpdated"/> whenever the server pushes a progress notification.
///
/// Protocol safety: a single background reader loop owns the <c>StreamReader</c>.
/// <c>GetSimStatesAsync</c> posts a request and awaits a <c>TaskCompletionSource</c>
/// that the reader loop resolves when the response line arrives. This prevents the
/// <c>InvalidOperationException: stream is currently in use</c> caused by concurrent reads.
///
/// Call <see cref="SetServerPid"/> immediately after starting the LSP server process.
/// </summary>
internal sealed class SimStatePipeClient : IDisposable
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    private static SimStatePipeClient? instance_;
    private static readonly System.Threading.Lock instanceLock_ = new();

    internal static SimStatePipeClient Instance
    {
        get
        {
            if (instance_ == null)
            {
                lock (instanceLock_)
                    instance_ ??= new SimStatePipeClient();
            }
            return instance_;
        }
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private string? pipeName_;
    private NamedPipeClientStream? pipe_;
    private StreamWriter? writer_;
    private readonly SemaphoreSlim requestSem_ = new(1, 1); // one request in-flight at a time
    private readonly CancellationTokenSource cts_ = new();
    private volatile TaskCompletionSource<string>? pendingResponse_;
    private int disposed_ = 0;

    /// <summary>
    /// Raised (from a background thread) when the server notifies that sim state
    /// has been updated for a document URI. Listeners should schedule a tagger refresh.
    /// The bool is <c>true</c> on the FINAL push of a simulation run (completion) — a signal to
    /// fully re-materialize lenses rather than do an incremental (suppression-prone) refresh.
    /// </summary>
    internal event Action<Uri, bool>? SimStateUpdated;

    private SimStatePipeClient() { }

    /// <summary>
    /// Call this immediately after starting the LSP server process.
    /// </summary>
    internal void SetServerPid(int pid)
    {
        this.pipeName_ = $"asmdude2-simstate-{pid}";
        PipeClientLog($"SetServerPid: will connect to pipe '{this.pipeName_}'");
        _ = Task.Run(this.ConnectAndListenAsync);
    }

    /// <summary>
    /// Requests a snapshot of sim-state labels for the given document URI.
    /// Returns an empty dictionary if the server is not connected or has no data.
    /// Keys are 0-based line indices; values are compact labels like "→RAX=0x10, ←ZF=0".
    /// </summary>
    internal async Task<Dictionary<int, string>> GetSimStatesAsync(Uri uri, CancellationToken ct = default)
    {
        if (this.writer_ == null) return [];

        await this.requestSem_.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (this.writer_ == null) return [];

            // Set up TCS before writing so the reader loop can't miss the response
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.pendingResponse_ = tcs;

            string request = JsonSerializer.Serialize(new { method = "getSimStates", uri = uri.ToString() });
            await this.writer_.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);

            // Wait up to 5s for the reader loop to fill in the response
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);
            string responseLine = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            return ParseSimStatesResponse(responseLine);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            PipeClientLog($"GetSimStatesAsync error: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
        finally
        {
            this.pendingResponse_ = null;
            this.requestSem_.Release();
        }
    }

    /// <summary>
    /// Requests label CodeLens data (definition position + jump/call reference count) for the
    /// given document URI. The server owns the reference counting (via its assembler-aware
    /// LabelGraph); this client only renders the result. Returns an empty list if the server is
    /// not connected or has no data.
    /// </summary>
    internal async Task<IReadOnlyList<AsmLabelRef>> GetCodeLensDataAsync(Uri uri, CancellationToken ct = default)
    {
        if (this.writer_ == null) return [];

        await this.requestSem_.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (this.writer_ == null) return [];

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.pendingResponse_ = tcs;

            string request = JsonSerializer.Serialize(new { method = "getCodeLensData", uri = uri.ToString() });
            await this.writer_.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);
            string responseLine = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            return ParseCodeLensDataResponse(responseLine);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            PipeClientLog($"GetCodeLensDataAsync error: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
        finally
        {
            this.pendingResponse_ = null;
            this.requestSem_.Release();
        }
    }

    /// <summary>
    /// Resolves the documentation URL for a mnemonic via the LSP server (which owns the signature
    /// data and honors the configured AsmDoc_Url). Returns null if the server is not connected, the
    /// word is not a known mnemonic, or it has no documentation reference.
    /// </summary>
    internal async Task<string?> GetMnemonicUrlAsync(string mnemonic, CancellationToken ct = default)
    {
        if (this.writer_ == null || string.IsNullOrWhiteSpace(mnemonic)) return null;

        await this.requestSem_.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (this.writer_ == null) return null;

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.pendingResponse_ = tcs;

            string request = JsonSerializer.Serialize(new { method = "getMnemonicUrl", mnemonic });
            await this.writer_.WriteLineAsync(request.AsMemory(), ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);
            string responseLine = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(responseLine);
            if (doc.RootElement.TryGetProperty("mnemonicUrl", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                return urlEl.GetString();
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            PipeClientLog($"GetMnemonicUrlAsync error: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            this.pendingResponse_ = null;
            this.requestSem_.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed_, 1) != 0) return;
        this.cts_.Cancel();
        this.pipe_?.Dispose();
        this.requestSem_.Dispose();
        this.cts_.Dispose();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task ConnectAndListenAsync()
    {
        while (!this.cts_.Token.IsCancellationRequested && this.pipeName_ != null)
        {
            NamedPipeClientStream? pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(".", this.pipeName_, PipeDirection.InOut, PipeOptions.Asynchronous);

                PipeClientLog($"Connecting to '{this.pipeName_}'...");
                await pipe.ConnectAsync(10_000, this.cts_.Token).ConfigureAwait(false);
                PipeClientLog("Connected");

                this.pipe_ = pipe;
                this.writer_ = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

                // Single reader loop: owns the StreamReader, dispatches responses and notifications
                using var reader = new StreamReader(pipe, leaveOpen: true);
                await this.ReadLoopAsync(reader).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                PipeClientLog($"Connection failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                this.writer_ = null;
                this.pendingResponse_?.TrySetCanceled();
                this.pendingResponse_ = null;
                if (pipe != null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }

                this.pipe_ = null;
            }

            // Back-off before retrying
            if (!this.cts_.Token.IsCancellationRequested)
            {
                try { await Task.Delay(3000, this.cts_.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        PipeClientLog("Listen loop ended");
    }

    /// <summary>
    /// Sole reader of the pipe. Dispatches:
    /// <list type="bullet">
    ///   <item>Lines with <c>"lines"</c> key → response to pending <see cref="GetSimStatesAsync"/>.</item>
    ///   <item>Lines with <c>"method"</c> key → server-push notification.</item>
    /// </list>
    /// </summary>
    private async Task ReadLoopAsync(StreamReader reader)
    {
        while (!this.cts_.Token.IsCancellationRequested && this.pipe_?.IsConnected == true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(this.cts_.Token).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            if (line == null) break;

            if (line.Contains("\"lines\"") || line.Contains("\"codeLensData\"") || line.Contains("\"mnemonicUrl\""))
            {
                // Response to our getSimStates / getCodeLensData / getMnemonicUrl request — hand off
                // to the awaiting TCS. Only one request is in flight at a time (serialized by requestSem_).
                this.pendingResponse_?.TrySetResult(line);
            }
            else if (line.Contains("\"method\""))
            {
                // Server-push notification
                this.HandleNotification(line);
            }
        }

        PipeClientLog("Read loop ended");
    }

    private void HandleNotification(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("method", out var methodEl)) return;
            if (methodEl.GetString() != "simStateUpdated") return;

            if (!doc.RootElement.TryGetProperty("uri", out var uriEl)) return;
            string? uriStr = uriEl.GetString();
            if (string.IsNullOrEmpty(uriStr)) return;

            bool completed = doc.RootElement.TryGetProperty("completed", out var cEl)
                && cEl.ValueKind == JsonValueKind.True;

            var uri = new Uri(uriStr);
            PipeClientLog($"Received simStateUpdated for {uriStr} (completed={completed})");
            this.SimStateUpdated?.Invoke(uri, completed);
        }
        catch (Exception ex)
        {
            PipeClientLog($"Notification parse error: {ex.Message}");
        }
    }

    private static IReadOnlyList<AsmLabelRef> ParseCodeLensDataResponse(string responseLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseLine);
            if (!doc.RootElement.TryGetProperty("codeLensData", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<AsmLabelRef>(arr.GetArrayLength());
            foreach (var el in arr.EnumerateArray())
            {
                string label = el.TryGetProperty("Label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
                if (label.Length == 0) continue;
                int defLine = el.TryGetProperty("DefinitionLine", out var dl) ? dl.GetInt32() : 0;
                int defCol = el.TryGetProperty("DefinitionColumn", out var dc) ? dc.GetInt32() : 0;
                int defLen = el.TryGetProperty("DefinitionLength", out var dlen) ? dlen.GetInt32() : 0;
                int refCount = el.TryGetProperty("ReferenceLines", out var rl) && rl.ValueKind == JsonValueKind.Array
                    ? rl.GetArrayLength()
                    : 0;
                result.Add(new AsmLabelRef(label, defLine, defCol, defLen, refCount));
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static Dictionary<int, string> ParseSimStatesResponse(string responseLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseLine);
            if (!doc.RootElement.TryGetProperty("lines", out var linesEl)) return [];

            var result = new Dictionary<int, string>();
            foreach (var prop in linesEl.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out int lineIdx))
                    result[lineIdx] = prop.Value.GetString() ?? string.Empty;
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static void PipeClientLog(string msg,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Debug, "Pipe", msg, member, line);
}
