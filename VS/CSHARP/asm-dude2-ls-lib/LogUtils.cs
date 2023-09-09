// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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

using System.Diagnostics;
using System.IO;

namespace AsmDude2LS
{
    public static class LogUtils
    {
        public static TraceSource CreateTraceSource()
        {
            var traceSource = new TraceSource("AsmDude2", SourceLevels.Verbose | SourceLevels.ActivityTracing);
            var traceFileDirectoryPath = Path.Combine(Path.GetTempPath(), "VSLogs", "AsmDude2");
            Directory.CreateDirectory(traceFileDirectoryPath);
            var logFilePath = Path.Combine(traceFileDirectoryPath, "log.svclog");
            var traceListener = new XmlWriterTraceListener(logFilePath);
            traceSource.Listeners.Add(traceListener);
            Trace.AutoFlush = true;
            return traceSource;
        }
    }
}
