using System.Diagnostics;
using System.IO;

namespace LanguageServer
{
    public static class LogUtils
    {
        public static TraceSource CreateTraceSource()
        {
            var traceSource = new TraceSource("AsmDude2", SourceLevels.Verbose | SourceLevels.ActivityTracing);

            var traceFileDirectoryPath = Path.Combine(Path.GetTempPath(), "VisualStudio", "AsmDude2");
            var logFilePath = Path.Combine(traceFileDirectoryPath, "log.svclog");
            var traceListener = new XmlWriterTraceListener(logFilePath);

            traceSource.Listeners.Add(traceListener);

            Trace.AutoFlush = true;

            return traceSource;
        }

    }
}
