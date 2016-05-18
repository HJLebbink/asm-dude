using AsmDude.SyntaxHighlighting;
using AsmTools;
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
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsmDude.Tools {
    public static class AsmDudeToolsStatic {


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

            Func<ErrorListProvider> sc0 = delegate () {
                IServiceProvider serviceProvider;
                if (true) {
                    serviceProvider = new ServiceProvider(Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
                } else {
#pragma warning disable CS0162 // Unreachable code detected
                    serviceProvider = Package.GetGlobalService(typeof(IServiceProvider)) as ServiceProvider;
#pragma warning restore CS0162 // Unreachable code detected
                }
                ErrorListProvider errorListProvider = new ErrorListProvider(serviceProvider);
                errorListProvider.ProviderName = "Asm Errors";
                errorListProvider.ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode);
                return errorListProvider;
            };

            Func<LabelGraph> sc1 = delegate () {
                IContentType contentType = contentService.GetContentType(AsmDudePackage.AsmDudeContentType);
                ErrorListProvider errorListProvider = buffer.Properties.GetOrCreateSingletonProperty(sc0);
                return new LabelGraph(buffer, aggregatorFactory, errorListProvider, docFactory, contentType);
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc1);
        }

        public static AsmDudeTools getAsmDudeTools(ITextBuffer buffer) {
            Func<AsmDudeTools> sc1 = delegate () {
                return new AsmDudeTools();
            };
            AsmDudeTools asmDudeTools = buffer.Properties.GetOrCreateSingletonProperty<AsmDudeTools>(sc1);
            return asmDudeTools;
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

        public static void errorTaskNavigateHandler(object sender, EventArgs arguments) {
            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null) {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (String.IsNullOrEmpty(task.Document)) {
                return;
            }

            IVsUIShellOpenDocument openDoc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null) {
                return;
            }
            IVsWindowFrame frame;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider;
            IVsUIHierarchy hierarchy;
            uint itemId;
            Guid logicalView = VSConstants.LOGVIEWID_Code;

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out serviceProvider, out hierarchy, out itemId, out frame);
            if (ErrorHandler.Failed(hr) || (frame == null)) {
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
                        return;
                    }
                }
            }
            IVsTextManager mgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (mgr == null) {
                return;
            }
            mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, task.Column, task.Line, task.Column);
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

        public static System.Windows.Media.Color convertColor(System.Drawing.Color color) {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
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

    }
}
