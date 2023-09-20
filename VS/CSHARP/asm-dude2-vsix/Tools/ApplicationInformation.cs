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

using System.Globalization;
using System.Reflection;
using System;
using System.Diagnostics;
using System.IO;

namespace AsmDude2.Tools
{
    public static class ApplicationInformation
    {
        public static string LspPath()
        {
            return Path.Combine(AsmDudeToolsStatic.Get_Install_Path(), "Server", "AsmDude2.LSP.exe");
        }

        private static string vsixVersion = string.Empty;
        private static string vsixBuildInfo = string.Empty;
        private static string lspVersion = string.Empty;
        private static string lspBuildInfo = string.Empty;

        public static string VsixVersion()
        {
            if (vsixVersion == string.Empty)
            {
                vsixVersion = typeof(AsmDude2Package).Assembly.GetName().Version.ToString();
            }
            return vsixVersion;
        }

        public static string LspVersion()
        {
            if (lspVersion == string.Empty)
            {
                lspVersion = FileVersionInfo.GetVersionInfo(LspPath()).FileVersion;
            }
            return lspVersion;
        }

        public static string VsixBuildInfo()
        {
            if (vsixBuildInfo == string.Empty)
            {
                var d = GetBuildDate(Assembly.GetExecutingAssembly());
                d.AddHours(TimeZone.CurrentTimeZone.GetUtcOffset(d).Hours);
                vsixBuildInfo = d.ToUniversalTime().ToString(AsmDudeToolsStatic.CultureUI);
            }
            return vsixBuildInfo;
        }

        public static string LspBuildInfo()
        {
            if (lspBuildInfo == string.Empty)
            {
                /*
                string lspPath = LspPath();
                lspPath = lspPath.Substring(0, lspPath.Length - 3);// remove the exe
                lspPath += "dll";

                if (!File.Exists(lspPath))
                {
                    lspBuildInfo = $"file {lspPath} does not exist";
                } 
                else
                {
                    //var x = Assembly.LoadFrom(lspPath);


                    var x = Assembly.LoadFile(lspPath);
                    var d = GetBuildDate(x);
                    //d.AddHours(TimeZone.CurrentTimeZone.GetUtcOffset(d).Hours);
                    //lspBuildInfo = d.ToUniversalTime().ToString(AsmDudeToolsStatic.CultureUI);
                }
                */
                lspBuildInfo = "TODO";
            }
            return lspBuildInfo;
        }

        /// <summary>
        /// Retrieves the linker timestamp.
        /// </summary>
        /// <param name="assembly">The assembly</param>
        /// <returns></returns>
        /// <remarks>https://www.meziantou.net/getting-the-date-of-build-of-a-dotnet-assembly-at-runtime.htm</remarks>
        private static DateTime GetBuildDate(Assembly assembly)
        {
            const string BuildVersionMetadataPrefix = "+build";

            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
            }

            return default;
        }
    }
}
