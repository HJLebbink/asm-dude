// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS;

using AsmTools;

using Microsoft.Extensions.Logging;

using System;

using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

/// <summary>
/// Bridges Microsoft.Extensions.Logging into <see cref="AsmLog"/>. Registered on the server's host
/// builder so framework/host logs and any <c>ILogger</c> usage (e.g. <c>Worker</c>) flow into the same
/// sinks, file and format as the rest of AsmDude — instead of the default console provider, which would
/// write to stdout and corrupt the <c>--stdio</c> JSON-RPC stream. The MEL category (last segment of the
/// type name) becomes the AsmLog category.
/// </summary>
public sealed class AsmLogLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Bridge(Shorten(categoryName));

    public void Dispose()
    {
    }

    private static string Shorten(string name)
    {
        int dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }

    private sealed class Bridge(string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(MsLogLevel logLevel) => AsmLog.IsEnabled(Map(logLevel));

        public void Log<TState>(MsLogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            AsmLogLevel level = Map(logLevel);
            if (!AsmLog.IsEnabled(level)) return;

            string message = formatter(state, exception);
            if (exception is not null)
                message = $"{message} | {exception.GetType().Name}: {exception.Message}";

            // No call-site key here — MEL hides the real caller behind the framework; the category
            // (logger name) is the locator.
            AsmLog.Log(level, category, message);
        }

        private static AsmLogLevel Map(MsLogLevel logLevel) => logLevel switch
        {
            MsLogLevel.Trace => AsmLogLevel.Trace,
            MsLogLevel.Debug => AsmLogLevel.Debug,
            MsLogLevel.Information => AsmLogLevel.Info,
            MsLogLevel.Warning => AsmLogLevel.Warn,
            MsLogLevel.Error or MsLogLevel.Critical => AsmLogLevel.Error,
            _ => AsmLogLevel.Off,
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
