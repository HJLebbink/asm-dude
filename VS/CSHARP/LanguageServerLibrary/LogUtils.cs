using System.Diagnostics;
using System.IO;

namespace LanguageServer
{
    public static class LogUtils
    {
        public static TraceSource CreateTraceSource()
        {
            var traceSource = new TraceSource("MockLanguageExtension", SourceLevels.Verbose | SourceLevels.ActivityTracing);

            var traceFileDirectoryPath = Path.Combine(Path.GetTempPath(), "VisualStudio", "LSP");
            var logFilePath = Path.Combine(traceFileDirectoryPath, "MockLog.svclog");
            var traceListener = new XmlWriterTraceListener(logFilePath);

            traceSource.Listeners.Add(traceListener);

            Trace.AutoFlush = true;

            return traceSource;
        }

    }
}
