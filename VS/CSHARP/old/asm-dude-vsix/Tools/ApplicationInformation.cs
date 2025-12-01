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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude.Tools
{
    public static class ApplicationInformation
    {
        /// <summary>
        /// Gets the executing assembly.
        /// </summary>
        /// <value>The executing assembly.</value>
        public static System.Reflection.Assembly ExecutingAssembly
        {
            get { return executingAssembly ?? (executingAssembly = System.Reflection.Assembly.GetExecutingAssembly()); }
        }

        private static System.Reflection.Assembly executingAssembly;

        /// <summary>
        /// Gets the executing assembly version.
        /// </summary>
        /// <value>The executing assembly version.</value>
        public static System.Version ExecutingAssemblyVersion
        {
            get { return executingAssemblyVersion ?? (executingAssemblyVersion = ExecutingAssembly.GetName().Version); }
        }

        private static System.Version executingAssemblyVersion;

        /// <summary>
        /// Gets the compile date of the currently executing assembly.
        /// </summary>
        /// <value>The compile date.</value>
        public static System.DateTime CompileDate
        {
            get
            {
                if (!compileDate.HasValue)
                {
                    compileDate = RetrieveLinkerTimestamp(ExecutingAssembly.Location);
                }

                return compileDate ?? default(System.DateTime);
            }
        }

        private static System.DateTime? compileDate;

        /// <summary>
        /// Retrieves the linker timestamp.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        /// <remarks>http://www.codinghorror.com/blog/2005/04/determining-build-date-the-hard-way.html</remarks>
        private static System.DateTime RetrieveLinkerTimestamp(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            byte[] b = new byte[2048];
            System.IO.FileStream s = null;
            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                if (s != null)
                {
                    s.Close();
                }
            }
            System.DateTime dt = new System.DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(System.BitConverter.ToInt32(b, System.BitConverter.ToInt32(b, peHeaderOffset) + linkerTimestampOffset));
            return dt.AddHours(System.TimeZone.CurrentTimeZone.GetUtcOffset(dt).Hours);
        }
    }
}
