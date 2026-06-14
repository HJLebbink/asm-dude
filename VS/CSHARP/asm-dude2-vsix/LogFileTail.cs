// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A minimal <c>tail -f</c> over a log file, BCL-only (no VS.Extensibility SDK) so it can be unit-tested
/// directly. Used by <see cref="VsixLog"/> to surface the out-of-process AsmSim server's log file
/// (<c>asmdude-simserver.log</c>) in a VS "AsmSim" output pane: it starts at end-of-file (so existing history
/// isn't dumped), polls for appended lines, and restarts from the top if the file is truncated/rotated.
/// </summary>
internal static class LogFileTail
{
    /// <summary>
    /// Read every line appended to <paramref name="path"/> since byte <paramref name="position"/>, hand each
    /// to <paramref name="emit"/>, and return the position to resume from. Deterministic (no polling) so it
    /// is the unit-testable core. A file shorter than <paramref name="position"/> (truncated/rotated) restarts
    /// from 0; a missing file is a no-op. Opens with <see cref="FileShare.ReadWrite"/> so the writer can keep
    /// appending concurrently.
    /// </summary>
    internal static async Task<long> PumpAsync(string path, long position, Func<string, Task> emit, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return position;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (position > fs.Length) position = 0; // truncated/rotated since last read
        if (position < 0) position = 0;
        fs.Seek(position, SeekOrigin.Begin);

        using var reader = new StreamReader(fs);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            await emit(line).ConfigureAwait(false);
        }
        return fs.Position;
    }

    /// <summary>The tail loop: position at EOF, then repeatedly <see cref="PumpAsync"/> on a poll interval
    /// until cancelled. Best-effort — transient IO errors are swallowed and retried. (Timing-dependent; the
    /// deterministic logic lives in <see cref="PumpAsync"/>.)</summary>
    internal static async Task RunAsync(string path, Func<string, Task> emit, CancellationToken cancellationToken, int pollMs = 500)
    {
        long position = File.Exists(path) ? new FileInfo(path).Length : 0; // tail -f: start at the end

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                position = await PumpAsync(path, position, emit, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch { /* transient (file locked mid-write, etc.) — retry next tick */ }

            try { await Task.Delay(pollMs, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
}
