using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;

namespace AsmDude.ErrorSquiggles {

    internal sealed class ErrorTagger : ITagger<ErrorTag> {

        private readonly ITextView _view;
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ITextSearchService _textSearchService;
        private readonly ErrorListProvider _errorListProvider;
        private readonly string _filename;
        internal ErrorTagger(
                ITextView view, 
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                ITextSearchService textSearchService) {
            this._view = view;
            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._textSearchService = textSearchService;
            this._errorListProvider = ErrorListHelper.GetErrorListProvider();
            this._filename = GetFileName(buffer);

            //this._view.LayoutChanged += ViewLayoutChanged;
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<ErrorTag>.TagsChanged {
            add {}
            remove {}
        }

        IEnumerable<ITagSpan<ErrorTag>> ITagger<ErrorTag>.GetTags(NormalizedSnapshotSpanCollection spans) {

            DateTime time1 = DateTime.Now;
            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }


            IDictionary<string, string> labels = AsmDudeToolsStatic.getLabels(_sourceBuffer.CurrentSnapshot.GetText());

            foreach (IMappingTagSpan<AsmTokenTag> tagSpan in _aggregator.GetTags(spans)) {

                ITextSnapshot ssp = spans[0].Snapshot;

                NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(ssp);

                switch (tagSpan.Tag.type) {
                    case AsmTokenType.Label:

                        string labelStr = tagSpans[0].GetText();
                        //AsmDudeToolsStatic.Output(string.Format("INFO: label \"{0}\".", labelStr));
                        if (!labels.ContainsKey(labelStr)) {
                            string msg = String.Format("LABEL \"{0}\" is undefined.", labelStr);
                            AsmDudeToolsStatic.Output(string.Format("INFO: {0}", msg));

                            ErrorTask errorTask = new ErrorTask();
                            errorTask.Line = ssp.GetLineNumberFromPosition(spans[0].Start);
                            errorTask.Column = 0;
                            errorTask.Text = msg;
                            errorTask.ErrorCategory = TaskErrorCategory.Warning;
                            errorTask.Document = this._filename;
                            errorTask.Navigate += NavigateHandler;

                            this._errorListProvider.Tasks.Add(errorTask);
                            this._errorListProvider.BringToFront();
                            this._errorListProvider.Refresh();

                            //const string errorType = "syntax error";
                            //const string errorType = "compiler error";
                            const string errorType = "other error";
                            //const string errorType = "warning";

                            yield return new TagSpan<ErrorTag>(tagSpans[0], new ErrorTag(errorType, msg));
                        }
                        break;
                    case AsmTokenType.Mnemonic:
                        break;
                    default: break;
                }
            }
            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        //void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        //  if (e.NewSnapshot != e.OldSnapshot) {//make sure that there has really been a change
        //
        //    }
        //}

        public static string GetFileName(ITextBuffer buffer) {
            Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer bufferAdapter;
            buffer.Properties.TryGetProperty(typeof(Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer), out bufferAdapter);
            if (bufferAdapter != null) {
                var persistFileFormat = bufferAdapter as IPersistFileFormat;
                string ppzsFilename = null;
                uint iii;
                if (persistFileFormat != null) {
                    persistFileFormat.GetCurFile(out ppzsFilename, out iii);
                }
                return ppzsFilename;
            } else {
                return null;
            }
        }

        private void NavigateHandler(object sender, EventArgs arguments) {
            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null) {
                throw new ArgumentException("sender parm cannot be null");
            }

            if (String.IsNullOrEmpty(task.Document)) {
                return;
            }

            //IVsUIShellOpenDocument openDoc = GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            IVsUIShellOpenDocument openDoc = (IVsUIShellOpenDocument)Package.GetGlobalService(typeof(IVsUIShellOpenDocument));


            if (openDoc == null) {
                return;
            }

            IVsWindowFrame frame;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider;
            IVsUIHierarchy hierarchy;
            uint itemId;
            Guid logicalView = VSConstants.LOGVIEWID_Code;

            if (ErrorHandler.Failed(openDoc.OpenDocumentViaProject(
                task.Document, ref logicalView, out serviceProvider, out hierarchy, out itemId, out frame))
                || frame == null) {
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

            //IVsTextManager mgr = ServiceController.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
            IVsTextManager mgr = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            if (mgr == null) {
                return;
            }

            mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, task.Column, task.Line, task.Column);
        }
    }

    internal class ErrorListHelper {
        public static ErrorListProvider GetErrorListProvider() {
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider globalService = 
                (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider));

            System.IServiceProvider serviceProvider = new ServiceProvider(globalService);

            ErrorListProvider mErrorListProvider = new ErrorListProvider(serviceProvider);
            mErrorListProvider.ProviderName = "Asm Errors";
            mErrorListProvider.ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode);
            return mErrorListProvider;
        }
    }



}
