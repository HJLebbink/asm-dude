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

namespace AsmDude2.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using AsmDude2.SyntaxHighlighting;

    using AsmTools;

    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    public static class AsmDudeToolsStatic
    {
        private static bool first_log_message = true;
        public static readonly CultureInfo CultureUI = CultureInfo.CurrentUICulture;

        #region Singleton Factories

        public static ITagAggregator<AsmTokenTag> GetOrCreate_Aggregator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(aggregatorFactory != null);

            ITagAggregator<AsmTokenTag> sc()
            { // this is the only place where ITagAggregator are created
                //AsmDudeToolsStatic.Output_INFO("Creating a ITagAggregator");
                return aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }

        public static void Print_Speed_Warning(DateTime startTime, string component)
        {
            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            if (elapsedSec > AsmDude2Package.SlowWarningThresholdSec)
            {
                Output_WARNING(string.Format(CultureUI, "SLOW: took {0} {1:F3} seconds to finish", component, elapsedSec));
            }
        }

        #endregion Singleton Factories

        public static AssemblerEnum Used_Assembler
        {
            get
            {
                if (Settings.Default.useAssemblerAutoDetect)
                {
                    return AssemblerEnum.AUTO_DETECT;
                }
                if (Settings.Default.useAssemblerMasm)
                {
                    return AssemblerEnum.MASM;
                }
                if (Settings.Default.useAssemblerNasm)
                {
                    return AssemblerEnum.NASM_INTEL;
                }
                if (Settings.Default.useAssemblerNasm_Att)
                {
                    return AssemblerEnum.NASM_ATT;
                }
                Output_WARNING("AsmDudeToolsStatic.Used_Assembler:get: no assembler specified, assuming AUTO_DETECT");
                return AssemblerEnum.AUTO_DETECT;
            }

            set
            {
                Settings.Default.useAssemblerAutoDetect = false;
                Settings.Default.useAssemblerMasm = false;
                Settings.Default.useAssemblerNasm = false;
                Settings.Default.useAssemblerNasm_Att = false;

                if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    Settings.Default.useAssemblerAutoDetect = true;
                }
                else if (value.HasFlag(AssemblerEnum.MASM))
                {
                    Settings.Default.useAssemblerMasm = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_INTEL))
                {
                    Settings.Default.useAssemblerNasm = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    Settings.Default.useAssemblerNasm_Att = true;
                }
                else
                {
                    Output_WARNING(string.Format(CultureUI, "{0}:Used_Assembler:set: no assembler specified; value={1}, assuming AUTO_DETECT", "AsmDudeToolsStatic", value));
                    Settings.Default.useAssemblerAutoDetect = true;
                }
            }
        }

        public static AssemblerEnum Used_Assembler_Disassembly_Window
        {
            get
            {
                if (Settings.Default.useAssemblerDisassemblyAutoDetect)
                {
                    return AssemblerEnum.AUTO_DETECT;
                }
                if (Settings.Default.useAssemblerDisassemblyMasm)
                {
                    return AssemblerEnum.MASM;
                }
                if (Settings.Default.useAssemblerDisassemblyNasm_Att)
                {
                    return AssemblerEnum.NASM_ATT;
                }
                Output_WARNING("AsmDudeToolsStatic.Used_Assembler_Disassembly_Window:get no assembler specified, assuming AUTO_DETECT");
                return AssemblerEnum.AUTO_DETECT;
            }

            set
            {
                Settings.Default.useAssemblerDisassemblyAutoDetect = false;
                Settings.Default.useAssemblerDisassemblyMasm = false;
                Settings.Default.useAssemblerDisassemblyNasm_Att = false;

                if (value.HasFlag(AssemblerEnum.AUTO_DETECT))
                {
                    Settings.Default.useAssemblerDisassemblyAutoDetect = true;
                }
                else if (value.HasFlag(AssemblerEnum.MASM))
                {
                    Settings.Default.useAssemblerDisassemblyMasm = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    Settings.Default.useAssemblerDisassemblyNasm_Att = true;
                }
                else
                {
                    Output_WARNING(string.Format(CultureUI, "{0}:Used_Assembler_Disassembly_Window:set: no assembler specified; value={1}, assuming AUTO_DETECT", "AsmDudeToolsStatic", value));
                    Settings.Default.useAssemblerDisassemblyAutoDetect = true;
                }
            }
        }

        public static string GetFilename(ITextBuffer buffer, int timeout_ms = 200)
        {
            Contract.Requires(buffer != null);

            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Task<string> task = GetFilenameAsync(buffer);
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)).ConfigureAwait(true) == task)
                {
                    return await task.ConfigureAwait(true);
                }
                else
                {
                    Output_ERROR(string.Format(CultureUI, "{0}:GetFilename; could not get filename within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return string.Empty;
                }
            });
        }

        /// <summary>Get the full filename (with path) of the provided buffer; returns null if such name does not exist</summary>
        public static async Task<string> GetFilenameAsync(ITextBuffer buffer)
        {
            Contract.Requires(buffer != null);

            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document);
            string filename = document?.FilePath;
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Get_Filename_Async: retrieving filename {1}", typeof(AsmDudeToolsStatic), filename));
            return filename;
        }
        
        /// <summary>
        /// Get the path where this visual studio extension is installed.
        /// </summary>
        public static string Get_Install_Path()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static Color ConvertColor(System.Drawing.Color drawingColor)
        {
            return Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        public static System.Drawing.Color ConvertColor(Color mediaColor)
        {
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

  
        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_INFO(string msg)
        {
#           if DEBUG
            _ = OutputAsync("INFO: " + msg);
#           endif
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_WARNING(string msg)
        {
            _ = OutputAsync("WARNING: " + msg);
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_ERROR(string msg)
        {
            _ = OutputAsync("ERROR: " + msg);
        }

        /// <summary>
        /// Output message to the AsmSim window
        /// </summary>
        public static async Task OutputAsync(string msg)
        {
            Contract.Requires(msg != null);

            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);

            if (first_log_message)
            {
                first_log_message = false;

                string vsixVersion = ApplicationInformation.VsixVersion();
                string vsixBuildInfo = ApplicationInformation.VsixBuildInfo();
                string lspVersion = ApplicationInformation.LspVersion();

                StringBuilder sb = new StringBuilder();
                //https://patorjk.com/software/taag/#p=display&f=Rectangles&t=AsmDude2
                sb.Append("Welcome to\n");
                sb.Append(" _____           ____        _     ___ \n");
                sb.Append("|  _  |___ _____|    \\ _ _ _| |___|_  |\n");
                sb.Append("|     |_ -|     |  |  | | | . | -_|  _|\n");
                sb.Append("|__|__|___|_|_|_|____/|___|___|___|___|\n");
                sb.Append($"INFO: AsmDude2 VSIX {vsixVersion} ({vsixBuildInfo})\n");
                sb.Append($"INFO: AsmDude2 LSP {lspVersion}\n");
                sb.Append("INFO: Open source assembly extension. Making programming in assembler almost bearable.\n");
                sb.Append("INFO: made possible by generous support from https://Sneller.ai \n");
                sb.Append("INFO: More info at https://github.com/Sneller/asm-dude \n");
                sb.Append("----------------------------------\n");
                msg2 = sb.ToString() + msg2;
            }
            IVsOutputWindowPane outputPane = await GetOutputPaneAsync().ConfigureAwait(true);
            if (outputPane == null)
            {
                Debug.Write(msg2);
            }
            else
            {
                outputPane.OutputStringThreadSafe(msg2);
                outputPane.Activate();
            }
        }

        public static async Task<IVsOutputWindowPane> GetOutputPaneAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5142");
                outputWindow.CreatePane(paneGuid, "AsmDude2", 1, 0);
                outputWindow.GetPane(paneGuid, out IVsOutputWindowPane pane);
                return pane;
            }
        }
    }
}
