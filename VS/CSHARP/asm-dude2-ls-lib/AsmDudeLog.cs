// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

using AsmTools;

/// <summary>Back-compat level enum for the LSP server; maps onto <see cref="AsmLogLevel"/>.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// LSP-server logging facade. The engine is the shared <see cref="AsmLog"/> (in asm-options-lib), so
/// the server, the AsmSim library and the VSIX all log through one implementation and line format.
/// This type only (a) wires the server's sinks — disk + a stderr console (stderr so it never corrupts
/// <c>--stdio</c> JSON-RPC) + an optional VS TraceSource — and (b) forwards the existing
/// <c>Debug/Info/Warning/Error</c> calls under category <c>LS</c> with their real call site preserved
/// (so log lines carry a greppable <c>(member:line)</c> key). The VS "AsmDude" output pane is fed by a
/// further sink the <see cref="LanguageServer"/> registers (LSP <c>window/logMessage</c>) once connected.
/// </summary>
public static class AsmDudeLog
{
    private static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "asmdude-execution.log");

    static AsmDudeLog()
    {
        // Runtime override: ASMDUDE_LOGLEVEL=trace|debug|info|warn|error|off (else the build default).
        if (AsmLog.TryParseLevel(Environment.GetEnvironmentVariable("ASMDUDE_LOGLEVEL"), out var lvl))
            AsmLog.Threshold = lvl;

        // Disk sink — gated by DisableFileLog (the fuzzer disables it to avoid multi-GB logs).
        var fileSink = AsmLogSinks.File(LogFilePath);
        AsmLog.AddSink(entry => { if (!DisableFileLog) fileSink(entry); });

        // Console sink — always stderr so stdout stays clean for JSON-RPC in --stdio mode.
        AsmLog.AddSink(AsmLogSinks.Console(useStandardError: true));
    }

    /// <summary>Messages below this level are suppressed. Mapped onto <see cref="AsmLog.Threshold"/>.</summary>
    public static LogLevel Threshold
    {
        get => FromAsm(AsmLog.Threshold);
        set => AsmLog.Threshold = ToAsm(value);
    }

    /// <summary>True when stdout carries JSON-RPC. Kept for back-compat; the console sink already uses stderr.</summary>
    public static bool UseStdio { get; set; }

    /// <summary>When true the on-disk sink is skipped (the fuzzer sets this to avoid multi-GB logs).</summary>
    public static bool DisableFileLog { get; set; }

    /// <summary>Optional VS TraceSource; when first set, log lines are mirrored to it.</summary>
    public static TraceSource? TraceSource
    {
        get => traceSource_;
        set
        {
            traceSource_ = value;
            if (value is not null && !traceSinkAdded_)
            {
                traceSinkAdded_ = true;
                AsmLog.AddSink(entry =>
                {
                    try { traceSource_?.TraceEvent(ToTrace(entry.Level), 0, entry.ToDetailedString()); }
                    catch { }
                });
            }
        }
    }

    private static TraceSource? traceSource_;
    private static bool traceSinkAdded_;

    public static void Log(string message, LogLevel level = LogLevel.Info,
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => AsmLog.Log(ToAsm(level), "LS", message, member, line);

    public static void Debug(string message,
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => AsmLog.Log(AsmLogLevel.Debug, "LS", message, member, line);

    public static void Info(string message,
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => AsmLog.Log(AsmLogLevel.Info, "LS", message, member, line);

    public static void Warning(string message,
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => AsmLog.Log(AsmLogLevel.Warn, "LS", message, member, line);

    public static void Error(string message,
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => AsmLog.Log(AsmLogLevel.Error, "LS", message, member, line);

    private static AsmLogLevel ToAsm(LogLevel l) => l switch
    {
        LogLevel.Debug => AsmLogLevel.Debug,
        LogLevel.Info => AsmLogLevel.Info,
        LogLevel.Warning => AsmLogLevel.Warn,
        LogLevel.Error => AsmLogLevel.Error,
        _ => AsmLogLevel.Info,
    };

    private static LogLevel FromAsm(AsmLogLevel l) => l switch
    {
        AsmLogLevel.Trace or AsmLogLevel.Debug => LogLevel.Debug,
        AsmLogLevel.Info => LogLevel.Info,
        AsmLogLevel.Warn => LogLevel.Warning,
        _ => LogLevel.Error,
    };

    private static TraceEventType ToTrace(AsmLogLevel l) => l switch
    {
        AsmLogLevel.Trace or AsmLogLevel.Debug => TraceEventType.Verbose,
        AsmLogLevel.Info => TraceEventType.Information,
        AsmLogLevel.Warn => TraceEventType.Warning,
        _ => TraceEventType.Error,
    };
}
