// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

namespace AsmDude.Tools {

    public static class AsmDudeToolsStatic {

        #region Singleton Factories

        public static ITagAggregator<AsmTokenTag> getAggregator(
            ITextBuffer buffer, 
            IBufferTagAggregatorFactoryService aggregatorFactory) {

            Func<ITagAggregator<AsmTokenTag>> sc = delegate () {
                return aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }

        public static ILabelGraph getLabelGraph(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            ITextDocumentFactoryService docFactory,
            IContentTypeRegistryService contentService) {

            Func<LabelGraph> sc1 = delegate () {
                IContentType contentType = contentService.GetContentType(AsmDudePackage.AsmDudeContentType);
                return new LabelGraph(buffer, aggregatorFactory, AsmDudeTools.Instance.errorListProvider, docFactory, contentType);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc1);
        }
        
        public static void printSpeedWarning(DateTime startTime, string component) {
            double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0} {1:F3} seconds to finish", component, elapsedSec));
            }
        }

        #endregion Singleton Factories

        public static AssemblerEnum usedAssembler {
            get {
                if (Settings.Default.useAssemblerMasm) {
                    return AssemblerEnum.MASM;
                }
                if (Settings.Default.useAssemblerNasm) {
                    return AssemblerEnum.NASM;
                }
                Output("WARNING: AsmDudeToolsStatic.usedAssebler: no assembler specified, assuming MASM");

                return AssemblerEnum.MASM;
            }
            set {
                Settings.Default.useAssemblerMasm = false;
                Settings.Default.useAssemblerNasm = false;

                switch (value) {
                    case AssemblerEnum.MASM: Settings.Default.useAssemblerMasm = true; break;
                    case AssemblerEnum.NASM: Settings.Default.useAssemblerNasm = true; break;
                    case AssemblerEnum.UNKNOWN:
                    default:
                        Settings.Default.useAssemblerMasm = true; break;
                }
            }
        }

        /// <summary>
        /// get the full filename (with path) for the provided buffer
        /// </summary>
        public static string GetFileName(ITextBuffer buffer) {
            Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer bufferAdapter;
            buffer.Properties.TryGetProperty(typeof(Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer), out bufferAdapter);
            if (bufferAdapter != null) {
                IPersistFileFormat persistFileFormat = bufferAdapter as IPersistFileFormat;

                string filename = null;
                uint dummyInteger;
                if (persistFileFormat != null) {
                    persistFileFormat.GetCurFile(out filename, out dummyInteger);
                }
                return filename;
            } else {
                return null;
            }
        }

        public static void openDisassembler() {
            try {
                DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
                dte.ExecuteCommand("Debug.Disassembly");
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "ERROR: AsmDudeToolsStatic:openDisassembler {0}", e.Message));
            }
        }

        public static int getFontSize() {
            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            EnvDTE.Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontSize");
            int fontSize = (System.Int16)prop.Value;
            return fontSize;
        }

        public static FontFamily getFontType() {
            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            EnvDTE.Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontFamily");
            string font = (string)prop.Value;
            //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "ERROR: AsmDudeToolsStatic:getFontType {0}", font));
            return new FontFamily(font);
        }

        public static void errorTaskNavigateHandler(object sender, EventArgs arguments) {
            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null) {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (String.IsNullOrEmpty(task.Document)) {
                Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: task.Document is empty");
                return;
            }

            IVsUIShellOpenDocument openDoc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null) {
                Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: openDoc is null");
                return;
            }

            IVsWindowFrame frame;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider;
            IVsUIHierarchy hierarchy;
            uint itemId;
            Guid logicalView = VSConstants.LOGVIEWID_Code;

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out serviceProvider, out hierarchy, out itemId, out frame);
            if (ErrorHandler.Failed(hr) || (frame == null)) {
                Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: OpenDocumentViaProject failed");
                return;
            }

            object docData;
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

            VsTextBuffer buffer = docData as VsTextBuffer;
            if (buffer == null) {
                IVsTextBufferProvider bufferProvider = docData as IVsTextBufferProvider;
                if (bufferProvider != null) {
                    IVsTextLines lines;
                    ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out lines));
                    buffer = lines as VsTextBuffer;

                    if (buffer == null) {
                        Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: buffer is null");
                        return;
                    }
                }
            }
            IVsTextManager mgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (mgr == null) {
                Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: IVsTextManager is null");
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
        public static string getInstallPath() {
            try {
                string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string filenameDll = "AsmDude.dll";
                return fullPath.Substring(0, fullPath.Length - filenameDll.Length);
            } catch (Exception) {
                return "";
            }
        }

        public static System.Windows.Media.Color convertColor(System.Drawing.Color drawingColor) {
            return System.Windows.Media.Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        public static System.Drawing.Color convertColor(System.Windows.Media.Color mediaColor) {
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

        public static ImageSource bitmapFromUri(Uri bitmapUri) {
            var bitmap = new BitmapImage();
            try {
                bitmap.BeginInit();
                bitmap.UriSource = bitmapUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            } catch (Exception e) {
                AsmDudeToolsStatic.Output("WARNING: bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string cleanup(string line) {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > AsmDudePackage.maxNumberOfCharsInToolTips) {
                return cleanedString.Substring(0, AsmDudePackage.maxNumberOfCharsInToolTips - 3) + "...";
            } else {
                return cleanedString;
            }
        }
        /// <summary>
        /// Output message to the AsmDude window
        /// </summary>
        public static void Output(string msg) {
            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);
            if (outputWindow == null) {
                Debug.Write(msg2);
            } else {
                Guid paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
                IVsOutputWindowPane pane;
                outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
                outputWindow.GetPane(paneGuid, out pane);
                pane.OutputString(msg2);
                pane.Activate();
            }
        }

        public static string getKeywordStr(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);

                int beginPos = t.Item1;
                int endPos = t.Item2;
                int length = endPos - beginPos;

                string result = line.Substring(beginPos, length);
                //AsmDudeToolsStatic.Output("INFO: getKeyword: \"" + result + "\".");
                return result;
            }
            return null;
        }

        public static TextExtent? getKeyword(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);
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
        public static string getPreviousKeyword(SnapshotPoint begin, SnapshotPoint end) {
            // return getPreviousKeyword(begin.GetContainingLine.)
            if (end == 0) return "";

            int beginLine = begin.GetContainingLine().Start;
            int beginPos = begin.Position - beginLine;
            int endPos = end.Position - beginLine;
            return AsmSourceTools.getPreviousKeyword(beginPos, endPos, begin.GetContainingLine().GetText());
        }

        public static bool isAllUpper(string input) {
            for (int i = 0; i < input.Length; i++) {
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i])) {
                    return false;
                }
            }
            return true;
        }

        public static void disableMessage(string msg, string filename, ErrorListProvider errorListProvider) {
            AsmDudeToolsStatic.Output(string.Format("WARNING: " + msg));

            for (int i = 0; i < errorListProvider.Tasks.Count; ++i) {
                Task t = errorListProvider.Tasks[i];
                if (t.Text.Equals(msg)) {
                    return;
                }
            }

            ErrorTask errorTask = new ErrorTask();
            errorTask.SubcategoryIndex = (int)AsmErrorEnum.OTHER;
            errorTask.Text = msg;
            errorTask.ErrorCategory = TaskErrorCategory.Message;
            errorTask.Document = filename;
            errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;

            errorListProvider.Tasks.Add(errorTask);
            errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            errorListProvider.Refresh();
        }


        public static ISet<Arch> getArchSwithedOn() {
            ISet<Arch> set = new HashSet<Arch>();
            foreach (Arch arch in Enum.GetValues(typeof(Arch))) {
                if (isArchSwitchedOn(arch)) {
                    set.Add(arch);
                }
            }
            return set;
        }

        public static bool isArchSwitchedOn(Arch arch) {
            switch (arch) {
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
                case Arch.INVPCID: return false;
                case Arch.CYRIXM: return Settings.Default.ARCH_CYRIXM;
                case Arch.VMX: return Settings.Default.ARCH_VMX;
                case Arch.RTM: return Settings.Default.ARCH_RTM;
                case Arch.HLE: return false;
                case Arch.MPX: return Settings.Default.ARCH_MPX;
                case Arch.SHA: return Settings.Default.ARCH_SHA;
                case Arch.UNDOC: return false;
                case Arch.PREFETCHWT1: return false;
                default:
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:isArch2SwitchedOn; unsupported arch {0}", arch));
                    return false;
            }
        }

    }
}
