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

namespace AsmDude2LS
{
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.IO;

    using AsmTools;

    public static class Tools
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



        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string Cleanup(string line)
        {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > LanguageServer.MaxNumberOfCharsInToolTips)
            {
                return cleanedString.Substring(0, LanguageServer.MaxNumberOfCharsInToolTips - 3) + "...";
            }
            else
            {
                return cleanedString;
            }
        }

        public static string Make_Full_Qualified_Label(string prefix, string label2, AssemblerEnum assembler)
        {
            if (assembler.HasFlag(AssemblerEnum.MASM))
            {
                if ((prefix != null) && (prefix.Length > 0))
                {
                    return "[" + prefix + "]" + label2;
                }
                else
                {
                    return label2;
                }
            }
            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL))
            {
                if ((prefix != null) && (prefix.Length > 0))
                {
                    return prefix + label2;
                }
                else
                {
                    return label2;
                }
            }
            return prefix + label2;
        }

        public static string Retrieve_Regular_Label(string label, AssemblerEnum assembler)
        {
            Contract.Requires(label != null);

            if (assembler.HasFlag(AssemblerEnum.MASM))
            {
                if ((label.Length > 0) && label[0].Equals('['))
                {
                    for (int i = 1; i < label.Length; ++i)
                    {
                        char c = label[i];
                        if (c.Equals(']'))
                        {
                            return label.Substring(i + 1);
                        }
                    }
                }
            }
            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL))
            {
                for (int i = 0; i < label.Length; ++i)
                {
                    char c = label[i];
                    if (c.Equals('.'))
                    {
                        return label.Substring(i);
                    }
                }
            }
            return label;
        }
    }
}
