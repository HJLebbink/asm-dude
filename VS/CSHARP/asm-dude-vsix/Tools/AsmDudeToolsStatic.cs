// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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

using AsmDude.ErrorSquiggles;
using AsmDude.SyntaxHighlighting;
using AsmTools;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsmDude.Tools
{
    public static class AsmDudeToolsStatic
    {
        #region Singleton Factories

        public static ITagAggregator<AsmTokenTag> Get_Aggregator(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {

            Func<ITagAggregator<AsmTokenTag>> sc = delegate ()
            {
                return aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }

        public static ILabelGraph Get_Label_Graph(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            ITextDocumentFactoryService docFactory,
            IContentTypeRegistryService contentService)
        {

            Func<LabelGraph> sc1 = delegate ()
            {
                IContentType contentType = contentService.GetContentType(AsmDudePackage.AsmDudeContentType);
                return new LabelGraph(buffer, aggregatorFactory, AsmDudeTools.Instance.Error_List_Provider, docFactory, contentType);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc1);
        }

        public static void Print_Speed_Warning(DateTime startTime, string component)
        {
            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("SLOW: took {0} {1:F3} seconds to finish", component, elapsedSec));
            }
        }

        #endregion Singleton Factories

        public static AssemblerEnum Used_Assembler
        {
            get
            {
                if (Settings.Default.useAssemblerMasm)
                {
                    return AssemblerEnum.MASM;
                }
                if (Settings.Default.useAssemblerNasm)
                {
                    return AssemblerEnum.NASM;
                }
                Output("WARNING: AsmDudeToolsStatic.usedAssebler: no assembler specified, assuming MASM");
                return AssemblerEnum.MASM;
            }
            set
            {
                Settings.Default.useAssemblerMasm = false;
                Settings.Default.useAssemblerNasm = false;

                if (value.HasFlag(AssemblerEnum.MASM))
                {
                    Settings.Default.useAssemblerMasm = true;
                }
                else if (value.HasFlag(AssemblerEnum.NASM))
                {
                    Settings.Default.useAssemblerNasm = true;
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
        public static string GetFileName(ITextBuffer buffer)
        {
            buffer.Properties.TryGetProperty(typeof(Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer), out IVsTextBuffer bufferAdapter);
            if (bufferAdapter != null)
            {
                IPersistFileFormat persistFileFormat = bufferAdapter as IPersistFileFormat;

                string filename = null;
                if (persistFileFormat != null)
                {
                    persistFileFormat.GetCurFile(out filename, out uint dummyInteger);
                }
                return filename;
            }
            else
            {
                return null;
            }
        }

        public static bool Proper_File(ITextBuffer buffer)
        {
            string filename = GetFileName(buffer);
            if ((filename == null) ||
                (filename.Length == 0) ||
                (filename.EndsWith(".asm", StringComparison.OrdinalIgnoreCase)) ||
                (filename.EndsWith(".cod", StringComparison.OrdinalIgnoreCase)) ||
                (filename.EndsWith(".inc", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        public static void Open_Disassembler()
        {
            try
            {
                DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
                dte.ExecuteCommand("Debug.Disassembly");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "ERROR: AsmDudeToolsStatic:openDisassembler {0}", e.Message));
            }
        }

        public static int Get_Font_Size()
        {
            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            EnvDTE.Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontSize");
            int fontSize = (System.Int16)prop.Value;
            return fontSize;
        }

        public static FontFamily Get_Font_Type()
        {
            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            EnvDTE.Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontFamily");
            string font = (string)prop.Value;
            //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "ERROR: AsmDudeToolsStatic:getFontType {0}", font));
            return new FontFamily(font);
        }

        public static Brush GetFontColor()
        {
            try
            {
                DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
                EnvDTE.Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
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
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("AsmDudeToolsStatic:GetFontColor " + e.Message);
            }
            AsmDudeToolsStatic.Output_WARNING("AsmDudeToolsStatic:GetFontColor: could not retrieve text color");
            return new SolidColorBrush(Colors.Gray);
        }

        public static void Error_Task_Navigate_Handler(object sender, EventArgs arguments)
        {
            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null)
            {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (String.IsNullOrEmpty(task.Document))
            {
                Output("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: task.Document is empty");
                return;
            }

            Output_INFO("AsmDudeToolsStatic: Error_Task_Navigate_Handler: task.Document=" + task.Document);


            IVsUIShellOpenDocument openDoc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null)
            {
                Output("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: openDoc is null");
                return;
            }

            Guid logicalView = VSConstants.LOGVIEWID_Code;

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out var serviceProvider, out var hierarchy, out uint itemId, out var frame);
            if (ErrorHandler.Failed(hr) || (frame == null))
            {
                Output("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: OpenDocumentViaProject failed");
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
                        Output("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: buffer is null");
                        return;
                    }
                }
            }
            IVsTextManager mgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (mgr == null)
            {
                Output("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: IVsTextManager is null");
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

        public static System.Windows.Media.Color ConvertColor(System.Drawing.Color drawingColor)
        {
            return System.Windows.Media.Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        public static System.Drawing.Color ConvertColor(System.Windows.Media.Color mediaColor)
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
                AsmDudeToolsStatic.Output("WARNING: bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
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
            Output("INFO: " + msg);
#           endif
        }
        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_WARNING(string msg)
        {
            Output("WARNING: " + msg);
        }
        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_ERROR(string msg)
        {
            Output("ERROR: " + msg);
        }

        /// <summary>
        /// Output message to the AsmSim window
        /// </summary>
        public static void Output(string msg)
        {
            IVsOutputWindowPane outputPane = GetOutputPane();
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

        public static IVsOutputWindowPane GetOutputPane()
        {
            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5141");
                outputWindow.CreatePane(paneGuid, "AsmSim", 1, 0);
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

                (int, int) t = AsmTools.AsmSourceTools.GetKeywordPos(currentPos - startLine, line);

                int beginPos = t.Item1;
                int endPos = t.Item2;
                int length = endPos - beginPos;

                string result = line.Substring(beginPos, length);
                //AsmDudeToolsStatic.Output("INFO: getKeyword: \"" + result + "\".");
                return result;
            }
            return null;
        }

        public static TextExtent? Get_Keyword(SnapshotPoint? bufferPosition)
        {

            if (bufferPosition != null)
            {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                (int, int) t = AsmTools.AsmSourceTools.GetKeywordPos(currentPos - startLine, line);
                //AsmDudeToolsStatic.Output(string.Format("INFO: getKeywordPos: beginPos={0}; endPos={1}.", t.Item1, t.Item2));

                int beginPos = t.Item1 + startLine;
                int endPos = t.Item2 + startLine;
                int length = endPos - beginPos;

                SnapshotSpan span = new SnapshotSpan(bufferPosition.Value.Snapshot, beginPos, length);
                //AsmDudeToolsStatic.Output("INFO: getKeyword: \"" + span.GetText() + "\".");
                return new TextExtent(span, true);
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
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static void Disable_Message(string msg, string filename, ErrorListProvider errorListProvider)
        {
            AsmDudeToolsStatic.Output_WARNING(msg);

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
                SubcategoryIndex = (int)AsmErrorEnum.OTHER,
                Text = msg,
                ErrorCategory = TaskErrorCategory.Message,
                Document = filename
            };
            errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;

            errorListProvider.Tasks.Add(errorTask);
            errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            errorListProvider.Refresh();
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
                case MicroArch.SandyBridge: return Settings.Default.PerformanceInfo_SandyBridge_On;
                case MicroArch.IvyBridge: return Settings.Default.PerformanceInfo_IvyBridge_On;
                case MicroArch.Haswell: return Settings.Default.PerformanceInfo_Haswell_On;
                case MicroArch.Broadwell: return Settings.Default.PerformanceInfo_Broadwell_On;
                case MicroArch.Skylake: return Settings.Default.PerformanceInfo_Skylake_On;
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
            switch (arch)
            {
                case Arch.ARCH_8086: return Settings.Default.ARCH_8086;
                case Arch.ARCH_186: return Settings.Default.ARCH_186;
                case Arch.ARCH_286: return Settings.Default.ARCH_286;
                case Arch.ARCH_386: return Settings.Default.ARCH_386;
                case Arch.ARCH_486: return Settings.Default.ARCH_486;
                case Arch.PENT: return Settings.Default.ARCH_PENT;
                case Arch.P6: return Settings.Default.ARCH_P6;

                case Arch.ARCH_3DNOW: return Settings.Default.ARCH_3DNOW;
                case Arch.MMX: return Settings.Default.ARCH_MMX;
                case Arch.SSE: return Settings.Default.ARCH_SSE;
                case Arch.SSE2: return Settings.Default.ARCH_SSE2;
                case Arch.SSE3: return Settings.Default.ARCH_SSE3;
                case Arch.SSSE3: return Settings.Default.ARCH_SSSE3;
                case Arch.SSE4_1: return Settings.Default.ARCH_SSE41;
                case Arch.SSE4_2: return Settings.Default.ARCH_SSE42;
                case Arch.SSE4A: return Settings.Default.ARCH_SSE4A;
                case Arch.SSE5: return Settings.Default.ARCH_SSE5;

                case Arch.AVX: return Settings.Default.ARCH_AVX;
                case Arch.AVX2: return Settings.Default.ARCH_AVX2;
                case Arch.AVX512F: return Settings.Default.ARCH_AVX512F;
                case Arch.AVX512CD: return Settings.Default.ARCH_AVX512CD;
                case Arch.AVX512ER: return Settings.Default.ARCH_AVX512ER;
                case Arch.AVX512PF: return Settings.Default.ARCH_AVX512PF;
                case Arch.AVX512VL: return Settings.Default.ARCH_AVX512VL;
                case Arch.AVX512DQ: return Settings.Default.ARCH_AVX512DQ;
                case Arch.AVX512BW: return Settings.Default.ARCH_AVX512BW;

                case Arch.X64: return Settings.Default.ARCH_X64;
                case Arch.BMI1: return Settings.Default.ARCH_BMI1;
                case Arch.BMI2: return Settings.Default.ARCH_BMI2;
                case Arch.IA64: return Settings.Default.ARCH_IA64;
                case Arch.FMA: return Settings.Default.ARCH_FMA;
                case Arch.TBM: return Settings.Default.ARCH_TBM;
                case Arch.AMD: return Settings.Default.ARCH_AMD;
                case Arch.CYRIX: return Settings.Default.ARCH_CYRIX;
                case Arch.INVPCID: return Settings.Default.ARCH_INVPCID;
                case Arch.CYRIXM: return Settings.Default.ARCH_CYRIXM;
                case Arch.VMX: return Settings.Default.ARCH_VMX;
                case Arch.RTM: return Settings.Default.ARCH_RTM;
                case Arch.HLE: return Settings.Default.ARCH_HLE;
                case Arch.MPX: return Settings.Default.ARCH_MPX;
                case Arch.SHA: return Settings.Default.ARCH_SHA;
                case Arch.UNDOC: return Settings.Default.ARCH_UNDOC;
                case Arch.PREFETCHWT1: return Settings.Default.ARCH_PREFETCHWT1;

                case Arch.ADX: return Settings.Default.ARCH_ADX;
                case Arch.AES: return Settings.Default.ARCH_AES;
                case Arch.F16C: return Settings.Default.ARCH_F16C;
                case Arch.FSGSBASE: return Settings.Default.ARCH_FSGSBASE;
                case Arch.PCLMULQDQ: return Settings.Default.ARCH_PCLMULQDQ;
                case Arch.LZCNT: return Settings.Default.ARCH_LZCNT;
                case Arch.PRFCHW: return Settings.Default.ARCH_PRFCHW;
                case Arch.RDPID: return Settings.Default.ARCH_RDPID;
                case Arch.RDRAND: return Settings.Default.ARCH_RDRAND;
                case Arch.RDSEED: return Settings.Default.ARCH_RDSEED;
                case Arch.XSAVEOPT: return Settings.Default.ARCH_XSAVEOPT;

                case Arch.NONE: return true;

                default:
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:AsmDudeToolsStatic::Is_Arch_Switched_On: unsupported arch {0}", arch));
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
            else if (assembler.HasFlag(AssemblerEnum.NASM))
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
            else if (assembler.HasFlag(AssemblerEnum.NASM))
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
