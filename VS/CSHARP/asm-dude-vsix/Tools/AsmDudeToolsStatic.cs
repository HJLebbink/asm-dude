// The MIT License (MIT)
//
// Copyright (c) 2018 Henk-Jan Lebbink
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Task = Microsoft.VisualStudio.Shell.Task;

namespace AsmDude.Tools
{
    public static class AsmDudeToolsStatic
    {
        #region Singleton Factories

        public static ITagAggregator<AsmTokenTag> GetOrCreate_Aggregator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            ITagAggregator<AsmTokenTag> sc()
            {   // this is the only place where ITagAggregator are created
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
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec)
            {
                Output_WARNING(string.Format("SLOW: took {0} {1:F3} seconds to finish", component, elapsedSec));
            }
        }

        #endregion Singleton Factories

        public static AssemblerEnum Used_Assembler
        {
            get
            {
                if (Settings.Default.useAssemblerMasm) return AssemblerEnum.MASM;
                if (Settings.Default.useAssemblerNasm) return AssemblerEnum.NASM_INTEL;
                if (Settings.Default.useAssemblerNasm_Att) return AssemblerEnum.NASM_ATT;
                Output_WARNING("AsmDudeToolsStatic.Used_Assembler: no assembler specified, assuming MASM");
                return AssemblerEnum.MASM;
            }
            set
            {
                Settings.Default.useAssemblerMasm = false;
                Settings.Default.useAssemblerNasm = false;
                Settings.Default.useAssemblerNasm_Att = false;

                if (value.HasFlag(AssemblerEnum.MASM))
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
                    Settings.Default.useAssemblerMasm = true;
                }
            }
        }

        /// <summary>
        /// get the full filename (with path) of the provided buffer; returns null if such name does not exist
        /// </summary>
        public static async Task<string> Get_Filename_Async(ITextBuffer buffer)
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document);
            string filename = document?.FilePath;
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Get_Filename_Async: retrieving filename {1}", typeof(AsmDudeToolsStatic), filename));
            return filename;
        }

        public static async System.Threading.Tasks.Task Open_Disassembler_Async()
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            dte.ExecuteCommand("Debug.Disassembly");
        }

        public static async Task<int> Get_Font_Size_Async()
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontSize");
            int fontSize = (short)prop.Value;
            return fontSize;
        }

        public static async Task<FontFamily> Get_Font_Type_Async()
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontFamily");
            string font = (string)prop.Value;
            //AsmDudeToolsStatic.Output_INFO(string.Format("AsmDudeToolsStatic:Get_Font_Type {0}", font));
            return new FontFamily(font);
        }

        public static async Task<Brush> Get_Font_Color_Async()
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
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

        public static async Task<Brush> Get_Background_Color_Async()
        {
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Task task = sender as Task;

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

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out var serviceProvider, out var hierarchy, out uint itemId, out var frame);
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
                    ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out var lines));
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

            //Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: navigating to row="+task.Line);
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
                return "";
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
            var bitmap = new BitmapImage();
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
            if (cleanedString.Length > AsmDudePackage.maxNumberOfCharsInToolTips)
            {
                return cleanedString.Substring(0, AsmDudePackage.maxNumberOfCharsInToolTips - 3) + "...";
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
            if (!ThreadHelper.CheckAccess())
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsOutputWindowPane outputPane = await GetOutputPaneAsync();
            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);
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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5141");
                outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
                outputWindow.GetPane(paneGuid, out var pane);
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

                var (beginPos, endPos) = AsmSourceTools.GetKeywordPos(currentPos - startLine, line);
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
            if (end == 0) return "";

            int beginLine = begin.GetContainingLine().Start;
            int beginPos = begin.Position - beginLine;
            int endPos = end.Position - beginLine;
            return AsmSourceTools.GetPreviousKeyword(beginPos, endPos, begin.GetContainingLine().GetText());
        }

        public static bool Is_All_Upper(string input)
        {
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
            Output_WARNING(msg);

            for (int i = 0; i < errorListProvider.Tasks.Count; ++i)
            {
                Task t = errorListProvider.Tasks[i];
                if (t.Text.Equals(msg))
                {
                    return;
                }
            }

            ErrorTask errorTask = new ErrorTask()
            {
                SubcategoryIndex = (int)AsmMessageEnum.OTHER,
                Text = msg,
                ErrorCategory = TaskErrorCategory.Message,
                Document = filename
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
            return (arch == Arch.ARCH_NONE) ? true : (bool)Settings.Default[arch.ToString()];
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
