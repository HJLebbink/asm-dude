// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AsmTools;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;

/// <summary>
/// Configures <see cref="AsmLog"/> for the VSIX (plugin) process. Same engine/format as the LSP server
/// and AsmSim, but its own destinations: a disk file (<c>%TEMP%\AsmDude2-extension.log</c>) and,
/// optionally, the Visual Studio "AsmDude" output pane (wired by <see cref="AttachOutputSink"/> once the
/// VS.Extensibility output channel exists). Verbosity follows the build default (Debug/Warn), overridable
/// at runtime via the <c>ASMDUDE_LOGLEVEL</c> env var and the <c>LogLevel</c> field in settings.json.
/// </summary>
internal static class VsixLog
{
    internal static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "AsmDude2-extension.log");

    private static int initialized_;
    private static int outputSinkAttached_;

    /// <summary>Idempotent; safe to call from any entry point that might run first.</summary>
    internal static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref initialized_, 1) != 0) return;

        if (AsmLog.TryParseLevel(Environment.GetEnvironmentVariable("ASMDUDE_LOGLEVEL"), out var level))
            AsmLog.Threshold = level;

        AsmLog.AddSink(AsmLogSinks.File(LogFilePath));
    }

    /// <summary>
    /// Registers (once) a sink that mirrors Info+ events to the VS "AsmDude" output pane. <paramref name="write"/>
    /// receives the clean, columnar rendering; the caller supplies it from a VS.Extensibility OutputChannel.
    /// </summary>
    internal static void AttachOutputSink(Action<string> write)
    {
        if (Interlocked.Exchange(ref outputSinkAttached_, 1) != 0) return;

        AsmLog.AddSink(AsmLogSinks.MinLevel(AsmLogLevel.Info, entry =>
        {
            try { write(entry.ToDisplayString()); }
            catch { }
        }));
    }

    /// <summary>Applies a level parsed from settings.json (no-op if blank/unknown).</summary>
    internal static void ApplyConfiguredLevel(string? levelText)
    {
        if (AsmLog.TryParseLevel(levelText, out var level))
            AsmLog.Threshold = level;
    }

    private static int outputChannelCreated_;
    private static OutputChannel? outputChannel_;
    private static readonly object outputWriteLock_ = new();

    /// <summary>
    /// Creates the dedicated "AsmDude2" Visual Studio output pane (once) and attaches an Info+ sink to
    /// it, so the plugin's own logs are visible in VS — not just on disk. Best-effort: any failure
    /// (channel API unavailable, etc.) is swallowed and retried on a later call; disk logging is
    /// unaffected. Call from a context that has the live <see cref="VisualStudioExtensibility"/>.
    /// </summary>
    internal static async Task EnsureOutputChannelAsync(VisualStudioExtensibility extensibility, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref outputChannelCreated_, 1) != 0) return;

        try
        {
            outputChannel_ = await extensibility.Views().Output
                .CreateOutputChannelAsync("AsmDude2", cancellationToken)
                .ConfigureAwait(false);

            AttachOutputSink(line =>
            {
                var channel = outputChannel_;
                if (channel is null) return;

                // OutputChannel.Writer is a TextWriter (not thread-safe); serialize writes.
                lock (outputWriteLock_) channel.Writer.WriteLine(line);
            });

            AsmLog.Info("Server", "AsmDude2 output pane ready");
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref outputChannelCreated_, 0); // allow a later retry
            AsmLog.Warn("Server", $"could not create AsmDude2 output pane: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
