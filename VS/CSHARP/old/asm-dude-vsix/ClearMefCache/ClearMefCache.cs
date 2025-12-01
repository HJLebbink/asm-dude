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

namespace AsmDude.ClearMefCache
{
    using System.IO;
    using Microsoft;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    internal sealed class ClearMefCache
    {
        private ClearMefCache(AsyncPackage package)
        {
            this.ServiceProvider = package;
        }

        private static ClearMefCache instance;

        private AsyncPackage ServiceProvider { get; }

        public static void Initialize(AsyncPackage package)
        {
            instance = new ClearMefCache(package);
        }

        //Clear the MEF Cache
        public static async System.Threading.Tasks.Task ClearAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsComponentModelHost componentModelHost = await instance.ServiceProvider.GetServiceAsync(typeof(SVsComponentModelHost)).ConfigureAwait(true) as IVsComponentModelHost;
            if (componentModelHost != null)
            {
                string folder = await componentModelHost.GetFolderPathAsync().ConfigureAwait(true);

                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        //Restart Visual Studio
        public static async System.Threading.Tasks.Task RestartAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsShell4 shell = await instance.ServiceProvider.GetServiceAsync(typeof(SVsShell)).ConfigureAwait(true) as IVsShell4;
            Assumes.Present(shell);
            shell.Restart((uint)__VSRESTARTTYPE.RESTART_Normal);
        }
    }
}
