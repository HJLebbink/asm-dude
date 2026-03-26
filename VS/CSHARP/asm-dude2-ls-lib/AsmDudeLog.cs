// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS;

using System;
using System.Diagnostics;
using System.IO;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Centralized logging for AsmDude2 LSP server.
/// All output goes to both console (VS Output window) and a log file on disk.
/// Set <see cref="Threshold"/> to control which messages are emitted.
/// </summary>
public static class AsmDudeLog
{
    private static readonly object fileLock = new();
    private static readonly string logPath = Path.Combine(Path.GetTempPath(), "asmdude-execution.log");

    /// <summary>Messages below this level are suppressed.</summary>
    public static LogLevel Threshold { get; set; } =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Warning;
#endif

    /// <summary>When true, log output goes to stderr (needed when stdout carries JSON-RPC).</summary>
    public static bool UseStdio { get; set; }

    /// <summary>Optional TraceSource for VS diagnostics integration.</summary>
    public static TraceSource? TraceSource { get; set; }

    private static TextWriter ConsoleWriter => UseStdio ? Console.Error : Console.Out;

    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (level < Threshold) return;

        string tag = level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "???",
        };

        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string formatted = $"[{timestamp}] {tag}: {message}";

        // Console — always if at or above threshold
        try { ConsoleWriter.WriteLine(formatted); } catch { }

        // Disk
        try
        {
            lock (fileLock)
            {
                File.AppendAllText(logPath, formatted + "\n");
            }
        }
        catch { }

        // VS TraceSource
        TraceEventType traceType = level switch
        {
            LogLevel.Debug => TraceEventType.Verbose,
            LogLevel.Info => TraceEventType.Information,
            LogLevel.Warning => TraceEventType.Warning,
            LogLevel.Error => TraceEventType.Error,
            _ => TraceEventType.Information,
        };
        try { TraceSource?.TraceEvent(traceType, 0, message); } catch { }
    }

    public static void Debug(string message) => Log(message, LogLevel.Debug);
    public static void Info(string message) => Log(message, LogLevel.Info);
    public static void Warning(string message) => Log(message, LogLevel.Warning);
    public static void Error(string message) => Log(message, LogLevel.Error);
}
