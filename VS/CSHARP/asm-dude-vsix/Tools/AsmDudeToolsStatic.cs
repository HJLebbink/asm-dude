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

namespace AsmDude.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using AsmDude.SyntaxHighlighting;
    using AsmTools;
    using EnvDTE;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.TextManager.Interop;
    using Microsoft.VisualStudio.Utilities;

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

        public static LabelGraph GetOrCreate_Label_Graph(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            ITextDocumentFactoryService docFactory,
            IContentTypeRegistryService contentService)
        {
            Contract.Requires(buffer != null);


            LabelGraph sc1()
            {
                IContentType contentType = contentService.GetContentType(AsmDudePackage.AsmDudeContentType);
                return new LabelGraph(buffer, aggregatorFactory, AsmDudeTools.Instance.Error_List_Provider, docFactory, contentType);
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc1);
        }

        public static void Print_Speed_Warning(DateTime startTime, string component)
        {
            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.SlowWarningThresholdSec)
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

        public static int MaxFileLines
        {
            get
            {
                return Settings.Default.Global_MaxFileLines;
            }

            set
            {
                Settings.Default.Global_MaxFileLines = value;
            }
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

        public static (AsmTokenTag tag, SnapshotSpan? keywordSpan) GetAsmTokenTag(ITagAggregator<AsmTokenTag> aggregator, SnapshotPoint triggerPoint)
        {
            Contract.Requires(aggregator != null);

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
            {
                foreach (SnapshotSpan span in asmTokenTag.Span.GetSpans(triggerPoint.Snapshot.TextBuffer))
                {
                    return (asmTokenTag.Tag, span);
                }
            }
            return (null, null);
        }

        public static IEnumerable<IMappingTagSpan<AsmTokenTag>> GetAsmTokenTags(ITagAggregator<AsmTokenTag> aggregator, int lineNumber)
        {
            Contract.Requires(aggregator != null);

            return aggregator.GetTags(aggregator.BufferGraph.TopBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
        }

        public static async System.Threading.Tasks.Task Open_Disassembler_Async()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            dte.ExecuteCommand("Debug.Disassembly");
        }

        public static int GetFontSize(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Task<int> task = GetFontSizeAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)).ConfigureAwait(true) == task)
                {
                    return await task.ConfigureAwait(true);
                }
                else
                {
                    Output_ERROR(string.Format(CultureUI, "{0}:GetFontSize; could not get font size within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return 8;
                }
            });
        }

        public static async Task<int> GetFontSizeAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontSize");
            int fontSize = (short)prop.Value;
            return fontSize;
        }

        public static FontFamily GetFontType(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Task<FontFamily> task = GetFontTypeAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)).ConfigureAwait(true) == task)
                {
                    return await task.ConfigureAwait(true);
                }
                else
                {
                    Output_ERROR(string.Format(CultureUI, "{0}:GetFontType; could not get font size within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new FontFamily("Comic Sans MS");
                }
            });
        }

        public static async Task<FontFamily> GetFontTypeAsync()
        {
            await System.Threading.Tasks.Task.Yield();
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontFamily");
            string font = (string)prop.Value;
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "AsmDudeToolsStatic:Get_Font_Type {0}", font));
            return new FontFamily(font);
        }

        public static Brush GetFontColor(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Task<Brush> task = GetFontColorAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)).ConfigureAwait(true) == task)
                {
                    return await task.ConfigureAwait(true);
                }
                else
                {
                    Output_ERROR(string.Format(CultureUI, "{0}:GetFontColor; could not get font color within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new SolidColorBrush(Colors.Gray);
                }
            });
        }

        public static async Task<Brush> GetFontColorAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            EnvDTE80.DTE2 dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE80.DTE2;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontsAndColorsItems");

            FontsAndColorsItems fci = (FontsAndColorsItems)prop.Object;

            for (int i = 1; i < fci.Count; ++i)
            {
                ColorableItems ci = fci.Item(i);
                if (ci.Name.Equals("PLAIN TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(ConvertColor(System.Drawing.ColorTranslator.FromOle((int)ci.Foreground)));
                }
            }
            Output_WARNING("AsmDudeToolsStatic:Get_Font_Color: could not retrieve text color");
            return new SolidColorBrush(Colors.Gray);
        }

        public static Brush GetBackgroundColor(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Task<Brush> task = GetBackgroundColorAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)).ConfigureAwait(true) == task)
                {
                    return await task.ConfigureAwait(true);
                }
                else
                {
                    Output_ERROR(string.Format(CultureUI, "{0}:GetBackgroundColor; could not get font color within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new SolidColorBrush(Colors.Gray);
                }
            });
        }

        public static async Task<Brush> GetBackgroundColorAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontsAndColorsItems");

            FontsAndColorsItems fci = (FontsAndColorsItems)prop.Object;

            for (int i = 1; i < fci.Count; ++i)
            {
                ColorableItems ci = fci.Item(i);
                if (ci.Name.Equals("PLAIN TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(ConvertColor(System.Drawing.ColorTranslator.FromOle((int)ci.Background)));
                }
            }
            Output_WARNING("AsmDudeToolsStatic:Get_Background_Color: could not retrieve text color");
            return new SolidColorBrush(Colors.Gray);
        }

        public static async void Error_Task_Navigate_Handler(object sender, EventArgs arguments)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null)
            {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (string.IsNullOrEmpty(task.Document))
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: task.Document is empty");
                return;
            }

            Output_INFO("AsmDudeToolsStatic: Error_Task_Navigate_Handler: task.Document=" + task.Document);

            IVsUIShellOpenDocument openDoc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null)
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: openDoc is null");
                return;
            }

            Guid logicalView = VSConstants.LOGVIEWID_Code;

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, out IVsUIHierarchy hierarchy, out uint itemId, out IVsWindowFrame frame);
            if (ErrorHandler.Failed(hr) || (frame == null))
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: OpenDocumentViaProject failed");
                return;
            }

            frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out object docData);

            VsTextBuffer buffer = docData as VsTextBuffer;
            if (buffer == null)
            {
                if (docData is IVsTextBufferProvider bufferProvider)
                {
                    ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out IVsTextLines lines));
                    buffer = lines as VsTextBuffer;

                    if (buffer == null)
                    {
                        Output_INFO("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: buffer is null");
                        return;
                    }
                }
            }
            IVsTextManager mgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (mgr == null)
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: IVsTextManager is null");
                return;
            }

            //Output_INFO("AsmDudeToolsStatic:errorTaskNavigateHandler: navigating to row="+task.Line);
            int iStartIndex = task.Column & 0xFFFF;
            int iEndIndex = (task.Column >> 16) & 0xFFFF;
            mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, iStartIndex, task.Line, iEndIndex);
        }

        /// <summary>
        /// Get the path where this visual studio extension is installed.
        /// </summary>
        public static string Get_Install_Path()
        {
            try
            {
                string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string filenameDll = "AsmDude.dll";
                return fullPath.Substring(0, fullPath.Length - filenameDll.Length);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Get the linenumber of the start of the provided span</summary>
        public static int Get_LineNumber(SnapshotSpan span)
        {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        public static Color ConvertColor(System.Drawing.Color drawingColor)
        {
            return Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        public static System.Drawing.Color ConvertColor(Color mediaColor)
        {
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

        public static ImageSource Bitmap_From_Uri(Uri bitmapUri)
        {
            Contract.Requires(bitmapUri != null);

            BitmapImage bitmap = new BitmapImage();
            try
            {
                bitmap.BeginInit();
                bitmap.UriSource = bitmapUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }
            catch (Exception e)
            {
                Output_WARNING("bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string Cleanup(string line)
        {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > AsmDudePackage.MaxNumberOfCharsInToolTips)
            {
                return cleanedString.Substring(0, AsmDudePackage.MaxNumberOfCharsInToolTips - 3) + "...";
            }
            else
            {
                return cleanedString;
            }
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_INFO(string msg)
        {
#           if DEBUG
            OutputAsync("INFO: " + msg).ConfigureAwait(false);
#           endif
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_WARNING(string msg)
        {
            OutputAsync("WARNING: " + msg).ConfigureAwait(false);
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_ERROR(string msg)
        {
            OutputAsync("ERROR: " + msg).ConfigureAwait(false);
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

                StringBuilder sb = new StringBuilder();
                sb.Append("Welcome to\n");
                sb.Append(" _____             ____        _     \n");
                sb.Append("|  _  |___ _____  |    \\ _ _ _| |___ \n");
                sb.Append("|     |_ -|     | |  |  | | | . | -_|\n");
                sb.Append("|__|__|___|_|_|_| |____/|___|___|___|\n");
                sb.Append("INFO: Loaded AsmDude version " + typeof(AsmDudePackage).Assembly.GetName().Version + " (" + ApplicationInformation.CompileDate.ToString(CultureUI) + ")\n");
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
                outputPane.OutputString(msg2);
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
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5141");
                outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
                outputWindow.GetPane(paneGuid, out IVsOutputWindowPane pane);
                return pane;
            }
        }

        public static string Get_Keyword_Str(SnapshotPoint? bufferPosition)
        {
            if (bufferPosition != null)
            {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                (int beginPos, int endPos) = AsmSourceTools.GetKeywordPos(currentPos - startLine, line);
                int length = endPos - beginPos;

                string result = line.Substring(beginPos, length);
                //AsmDudeToolsStatic.Output_INFO("getKeyword: \"" + result + "\".");
                return result;
            }
            return null;
        }

        /// <summary>
        /// Find the previous keyword (if any) that exists BEFORE the provided triggerPoint, and the provided start.
        /// Eg. qqqq xxxxxx yyyyyyy zzzzzz
        ///     ^             ^
        ///     |begin        |end
        /// the previous keyword is xxxxxx
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static string Get_Previous_Keyword(SnapshotPoint begin, SnapshotPoint end)
        {
            // return getPreviousKeyword(begin.GetContainingLine.)
            if (end == 0)
            {
                return string.Empty;
            }

            int beginLine = begin.GetContainingLine().Start;
            int beginPos = begin.Position - beginLine;
            int endPos = end.Position - beginLine;
            return AsmSourceTools.GetPreviousKeyword(beginPos, endPos, begin.GetContainingLine().GetText());
        }

        public static bool Is_All_upcase(string input)
        {
            Contract.Requires(input != null);

            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]) && !char.IsUpper(input[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static void Disable_Message(string msg, string filename, ErrorListProvider errorListProvider)
        {
            Contract.Requires(errorListProvider != null);

            Output_WARNING(msg);

            for (int i = 0; i < errorListProvider.Tasks.Count; ++i)
            {
                Microsoft.VisualStudio.Shell.Task t = errorListProvider.Tasks[i];
                if (t.Text.Equals(msg, StringComparison.Ordinal))
                {
                    return;
                }
            }

            ErrorTask errorTask = new ErrorTask()
            {
                SubcategoryIndex = (int)AsmMessageEnum.OTHER,
                Text = msg,
                ErrorCategory = TaskErrorCategory.Message,
                Document = filename,
            };
            errorTask.Navigate += Error_Task_Navigate_Handler;

            errorListProvider.Tasks.Add(errorTask);
            errorListProvider.Refresh();
            //errorListProvider.Show(); // do not use BringToFront since that will select the error window.
        }

        public static MicroArch Get_MicroArch_Switched_On()
        {
            MicroArch result = MicroArch.NONE;
            foreach (MicroArch microArch in Enum.GetValues(typeof(MicroArch)))
            {
                if (Is_MicroArch_Switched_On(microArch))
                {
                    result |= microArch;
                }
            }
            return result;
        }

        public static bool Is_MicroArch_Switched_On(MicroArch microArch)
        {
            switch (microArch)
            {
                case MicroArch.NONE: return false;
                case MicroArch.SandyBridge: return Settings.Default.PerformanceInfo_SandyBridge_On;
                case MicroArch.IvyBridge: return Settings.Default.PerformanceInfo_IvyBridge_On;
                case MicroArch.Haswell: return Settings.Default.PerformanceInfo_Haswell_On;
                case MicroArch.Broadwell: return Settings.Default.PerformanceInfo_Broadwell_On;
                case MicroArch.Skylake: return Settings.Default.PerformanceInfo_Skylake_On;
                case MicroArch.SkylakeX: return Settings.Default.PerformanceInfo_SkylakeX_On;
                case MicroArch.Kabylake: return false;
                case MicroArch.Cannonlake: return false;
                case MicroArch.Icelake: return false;
                case MicroArch.Tigerlake: return false;
                case MicroArch.KnightsCorner: return false;
                case MicroArch.KnightsLanding: return Settings.Default.PerformanceInfo_KnightsLanding_On;

                default:
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:AsmDudeToolsStatic::Is_MicroArch_Switched_On: unsupported arch {0}", microArch));
                    return false;
            }
        }

        public static ISet<Arch> Get_Arch_Swithed_On()
        {
            ISet<Arch> set = new HashSet<Arch>();
            foreach (Arch arch in Enum.GetValues(typeof(Arch)))
            {
                if (Is_Arch_Switched_On(arch))
                {
                    set.Add(arch);
                }
            }
            return set;
        }

        public static bool Is_Arch_Switched_On(Arch arch)
        {
            try
            {
                return (arch == Arch.ARCH_NONE) ? true : (bool)Settings.Default[arch.ToString()];
            }
            catch (Exception)
            {
                return false;
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
