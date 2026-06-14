// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmTools;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

// AsmLog is the sanctioned logging sink: it is the ONE place allowed to write to Console / File.Append*.
// Everywhere else the BannedApiAnalyzers rule (RS0030, see VS/CSHARP/BannedSymbols.txt) routes logging here.
#pragma warning disable RS0030 // Do not use banned APIs

/// <summary>Severity levels, ordered. <see cref="Off"/> disables everything.</summary>
public enum AsmLogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Off = 5,
}

/// <summary>
/// A single structured log event. Sinks receive this (not a pre-baked string) so each destination can
/// render it appropriately — a detailed line with the call-site key for the disk/console log, a clean
/// columnar line for the Visual Studio "AsmDude" / LSP output pane.
/// </summary>
public readonly record struct AsmLogEntry(
    DateTime Timestamp,
    AsmLogLevel Level,
    string Category,
    string Message,
    string? Member,
    int Line)
{
    /// <summary>Three-letter level tag (TRC/DBG/INF/WRN/ERR).</summary>
    public string Tag => Level switch
    {
        AsmLogLevel.Trace => "TRC",
        AsmLogLevel.Debug => "DBG",
        AsmLogLevel.Info => "INF",
        AsmLogLevel.Warn => "WRN",
        AsmLogLevel.Error => "ERR",
        _ => "???",
    };

    /// <summary>
    /// Verbose rendering for the disk/console log: includes the greppable call-site key, e.g.
    /// <c>[12:34:56.789] [DBG] [SIM] (RunSimulation:802) message</c>.
    /// </summary>
    public string ToDetailedString()
    {
        string site = Member is null ? string.Empty : $"({Member}:{Line}) ";
        return string.Create(CultureInfo.InvariantCulture, $"[{Timestamp:HH:mm:ss.fff}] [{Tag}] [{Category}] {site}{Message}");
    }

    /// <summary>
    /// Clean, column-aligned rendering for the Visual Studio output pane (no call-site noise), e.g.
    /// <c>12:34:56.789  INF  SIM        message</c>.
    /// </summary>
    public string ToDisplayString()
        => string.Create(CultureInfo.InvariantCulture, $"{Timestamp:HH:mm:ss.fff}  {Tag}  {Category,-10} {Message}");
}

/// <summary>
/// Process-wide structured logger shared by the VSIX plugin, the LSP server and the AsmSim library
/// (it lives in <c>asm-options-lib</c>, the one assembly every component transitively references).
///
/// <para><b>Design.</b> One engine, configured per process at startup with a set of <i>sinks</i>
/// (<c>Action&lt;AsmLogEntry&gt;</c>). Each host wires the destinations it has: a disk file (always), a
/// console/terminal (the server, on stderr so it never corrupts <c>--stdio</c> JSON-RPC), and a
/// host-specific channel — the Visual Studio "AsmDude" output pane (plugin) or LSP
/// <c>window/logMessage</c> (server). Microsoft.Extensions.Logging in the server is bridged in via a
/// provider, so framework/host logs land in the same place.</para>
///
/// <para><b>Format &amp; call-site key.</b> Every event carries its originating <c>member:line</c>
/// (auto-filled), so the disk log is greppable to the exact statement; the VS pane shows a clean
/// columnar form. See <see cref="AsmLogEntry"/>.</para>
///
/// <para><b>Cost.</b> Below-threshold calls early-out before building anything. <see cref="Threshold"/>
/// defaults to Debug in DEBUG builds and Warn in Release, overridable at runtime (env var / settings)
/// so a shipped build can be made verbose in the field without a rebuild.</para>
///
/// Thread-safe.
/// </summary>
public static class AsmLog
{
    /// <summary>Messages below this level are dropped (cheaply, before formatting).</summary>
    public static volatile AsmLogLevel Threshold =
#if DEBUG
        AsmLogLevel.Debug;
#else
        // TEMPORARY: Info (normally Warn) so Release builds surface Info logs while the dynamic-sim
        // engine is being developed/tested. Revert to AsmLogLevel.Warn before shipping.
        AsmLogLevel.Info;
#endif

    private static readonly List<Action<AsmLogEntry>> Sinks = [];
    private static readonly object SinkLock = new();

    /// <summary>Registers a destination. The sink should not throw and should be cheap (logging is on the hot path).</summary>
    public static void AddSink(Action<AsmLogEntry> sink)
    {
        lock (SinkLock) Sinks.Add(sink);
    }

    /// <summary>Removes all sinks (e.g. a fuzzer that wants zero logging overhead).</summary>
    public static void ClearSinks()
    {
        lock (SinkLock) Sinks.Clear();
    }

    public static bool IsEnabled(AsmLogLevel level) => level != AsmLogLevel.Off && level >= Threshold;

    /// <summary>
    /// Core entry point. <paramref name="member"/>/<paramref name="line"/> identify the call site (a
    /// stable, greppable key); they are auto-filled by the convenience overloads, and a facade forwards
    /// its own caller's values so it doesn't mask the true site.
    /// </summary>
    public static void Log(AsmLogLevel level, string category, string message, string? member = null, int line = 0, bool force = false)
    {
        // force = always emit (e.g. a startup banner) regardless of Threshold, but a hard Off still silences all.
        if (!force && !IsEnabled(level)) return;
        if (force && Threshold == AsmLogLevel.Off) return;

        var entry = new AsmLogEntry(DateTime.Now, level, category, message, member, line);

        Action<AsmLogEntry>[] snapshot;
        lock (SinkLock) snapshot = [.. Sinks];
        foreach (var sink in snapshot)
        {
            try { sink(entry); }
            catch { /* a broken sink must never take down logging or the caller */ }
        }
    }

    public static void Trace(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Trace, category, message, member, line);

    public static void Debug(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Debug, category, message, member, line);

    public static void Info(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Info, category, message, member, line);

    public static void Warn(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Warn, category, message, member, line);

    public static void Error(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Error, category, message, member, line);

    public static void Error(string category, string message, Exception ex, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Error, category, $"{message}: {ex.GetType().Name}: {ex.Message}", member, line);

    /// <summary>
    /// Logs an important one-off message (e.g. a startup banner) at Info severity that is ALWAYS emitted,
    /// bypassing <see cref="Threshold"/> (but still silenced by a hard <see cref="AsmLogLevel.Off"/>). Useful
    /// as a session marker even when the log is kept at Warn — and, in the server, it guarantees a
    /// <c>window/logMessage</c> is sent so Visual Studio creates the language-server output pane.
    /// </summary>
    public static void Banner(string category, string message, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => Log(AsmLogLevel.Info, category, message, member, line, force: true);

    /// <summary>Parses a level name (case-insensitive) from an env var / settings value. Returns false if blank/unknown.</summary>
    public static bool TryParseLevel(string? text, out AsmLogLevel level)
    {
        level = AsmLogLevel.Info;
        if (string.IsNullOrWhiteSpace(text)) return false;
        switch (text.Trim().ToUpperInvariant())
        {
            case "TRACE": case "TRC": case "VERBOSE": level = AsmLogLevel.Trace; return true;
            case "DEBUG": case "DBG": level = AsmLogLevel.Debug; return true;
            case "INFO": case "INF": case "INFORMATION": level = AsmLogLevel.Info; return true;
            case "WARN": case "WRN": case "WARNING": level = AsmLogLevel.Warn; return true;
            case "ERROR": case "ERR": level = AsmLogLevel.Error; return true;
            case "OFF": case "NONE": level = AsmLogLevel.Off; return true;
            default: return false;
        }
    }
}

/// <summary>Factory helpers for the common sink destinations, so each host wires them in one line.</summary>
public static class AsmLogSinks
{
    /// <summary>Appends the detailed (call-site-tagged) rendering of each event to <paramref name="path"/>.</summary>
    public static Action<AsmLogEntry> File(string path)
    {
        var fileLock = new object();
        return entry =>
        {
            try
            {
                lock (fileLock) System.IO.File.AppendAllText(path, entry.ToDetailedString() + Environment.NewLine);
            }
            catch { /* disk full / locked — never throw from logging */ }
        };
    }

    /// <summary>
    /// Writes the detailed rendering to the console if one is attached. <paramref name="useStandardError"/>
    /// must be true for the LSP server: in <c>--stdio</c> mode stdout carries JSON-RPC, so logs go to stderr.
    /// </summary>
    public static Action<AsmLogEntry> Console(bool useStandardError)
    {
        return entry =>
        {
            try { (useStandardError ? System.Console.Error : System.Console.Out).WriteLine(entry.ToDetailedString()); }
            catch { }
        };
    }

    /// <summary>Wraps <paramref name="sink"/> so it only receives events at or above <paramref name="minLevel"/>.</summary>
    public static Action<AsmLogEntry> MinLevel(AsmLogLevel minLevel, Action<AsmLogEntry> sink)
        => entry => { if (entry.Level >= minLevel) sink(entry); };
}
