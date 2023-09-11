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

        /// <summary>Guess whether the provided buffer has assembly in Intel syntax (return true) or AT&T syntax (return false)</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
        public static bool Guess_Intel_Syntax(ITextBuffer buffer, int nLinesMax = 30)
        {
            Contract.Requires(buffer != null);

            bool contains_register_att(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (asmToken[0].Equals('%'))
                    {
                        string asmToken2 = asmToken.Substring(1);
                        if (RegisterTools.IsRn(asmToken2, true))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            bool contains_register_intel(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (RegisterTools.IsRn(asmToken, true))
                    {
                        return true;
                    }
                }
                return false;
            }
            bool contains_constant_att(List<string> line)
            {
                foreach (string asmToken in line)
                {
                    if (asmToken[0].Equals('$'))
                    {
                        return true;
                    }
                }
                return false;
            }
            bool contains_constant_intel(List<string> line)
            {
                return false;
            }
            bool contains_mnemonic_att(List<string> line)
            {
                foreach (string word in line)
                {
                    if (!AsmSourceTools.IsMnemonic(word, true))
                    {
                        if (AsmSourceTools.IsMnemonic_Att(word, true))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            bool contains_mnemonic_intel(List<string> line)
            {
                return false;
            }

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Intel_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            int registers_i = 0;
            int constants_i = 0;
            int mnemonics_i = 0;

            for (int i = 0; i < Math.Min(snapshot.LineCount, nLinesMax); ++i)
            {
                string line_upcase = snapshot.GetLineFromLineNumber(i).GetText().ToUpperInvariant();
                Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_upcase));

                List<string> keywords_upcase = AsmSourceTools.SplitIntoKeywordsList(line_upcase);

                if (contains_register_att(keywords_upcase))
                {
                    registers_i++;
                }

                if (contains_register_intel(keywords_upcase))
                {
                    registers_i--;
                }

                if (contains_constant_att(keywords_upcase))
                {
                    constants_i++;
                }

                if (contains_constant_intel(keywords_upcase))
                {
                    constants_i--;
                }

                if (contains_mnemonic_att(keywords_upcase))
                {
                    mnemonics_i++;
                }

                if (contains_mnemonic_intel(keywords_upcase))
                {
                    mnemonics_i--;
                }
            }
            int total =
                Math.Max(Math.Min(1, registers_i), -1) +
                Math.Max(Math.Min(1, constants_i), -1) +
                Math.Max(Math.Min(1, mnemonics_i), -1);

            bool result = (total <= 0);
            Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax; result {1}; file=\"{2}\"; registers {3}; constants {4}; mnemonics {5}", "AsmDudeToolsStatic", result, GetFilename(buffer), registers_i, constants_i, mnemonics_i));
            return result;
        }

        /// <summary>Guess whether the provided buffer has assembly in Masm syntax (return true) or Gas syntax (return false)</summary>
        public static bool Guess_Masm_Syntax(ITextBuffer buffer, int nLinesMax = 30)
        {
            Contract.Requires(buffer != null);

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            int evidence_masm = 0;

            for (int i = 0; i < Math.Min(snapshot.LineCount, nLinesMax); ++i)
            {
                string line_upcase = snapshot.GetLineFromLineNumber(i).GetText().ToUpperInvariant();
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_capitals));

                List<string> keywords_upcase = AsmSourceTools.SplitIntoKeywordsList(line_upcase);

                foreach (string keyword_upcase in keywords_upcase)
                {
                    switch (keyword_upcase)
                    {
                        case "PTR":
                        case "@B":
                        case "@F":
                            evidence_masm++;
                            break;
                        case ".INTEL_SYNTAX":
                        case ".ATT_SYNTAX":
                            return false; // we know for sure
                    }
                }
            }
            bool result = (evidence_masm > 0);
            Output_INFO(string.Format(CultureUI, "{0}:Guess_Masm_Syntax; result {1}; file=\"{2}\"; evidence_masm {3}", "AsmDudeToolsStatic", result, GetFilename(buffer), evidence_masm));
            return result;
        }

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
        public static async System.Threading.Tasks.Task OutputAsync(string msg)
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
                string lspBuildInfo = ApplicationInformation.LspBuildInfo();

                StringBuilder sb = new StringBuilder();
                //https://patorjk.com/software/taag/#p=display&f=Rectangles&t=AsmDude2
                sb.Append("Welcome to\n");
                sb.Append(" _____           ____        _     ___ \n");
                sb.Append("|  _  |___ _____|    \\ _ _ _| |___|_  |\n");
                sb.Append("|     |_ -|     |  |  | | | . | -_|  _|\n");
                sb.Append("|__|__|___|_|_|_|____/|___|___|___|___|\n");
                sb.Append($"INFO: AsmDude2 VSIX {vsixVersion} ({vsixBuildInfo})\n");
                sb.Append($"INFO: AsmDude2 LSP {lspVersion} ({lspBuildInfo})\n");
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
