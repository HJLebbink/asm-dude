using AsmDude.SyntaxHighlighting;
using Microsoft.Internal.VisualStudio.PlatformUI;
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
using System.Collections.ObjectModel;

namespace AsmDude.ErrorSquiggles {

    internal sealed class ErrorTagger : ITagger<ErrorTag>, IDisposable {

        private readonly ITextView _view;
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ITextSearchService _textSearchService;
        private readonly ErrorListProvider _errorListProvider;
        private readonly string _filename;

        private IDictionary<string, string> _labels;
        private IDictionary<string, string> _labelClashes;
        private readonly IList<LabelErrorData> _labelUndefinedErrors;
        private readonly IList<LabelErrorData> _labelClashErrors;

        private EventHandler<SnapshotSpanEventArgs> _changedEvent;

        internal ErrorTagger(
                ITextView view, 
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                ITextSearchService textSearchService) {

            this._view = view;
            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._textSearchService = textSearchService;
            this._errorListProvider = AsmDudeToolsStatic.GetErrorListProvider();
            this._filename = AsmDudeToolsStatic.GetFileName(buffer);

            this._labelUndefinedErrors = new List<LabelErrorData>();
            this._labelClashErrors = new List<LabelErrorData>();

            this._view.TextBuffer.Changed += OnTextBufferChanged;
        }


        internal struct LabelErrorData {
            internal int _lineNumber;
            internal Task _task;
            internal string _msg;
            internal LabelErrorData(int lineNumber, Task task, string msg) {
                _lineNumber = lineNumber;
                _task = task;
                _msg = msg;
            }
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<ErrorTag>.TagsChanged {
            add {
                //AsmDudeToolsStatic.Output("TagsChanged: add: value=" + value);
                _labels = null;
                _changedEvent += value;
            }
            remove {
                //AsmDudeToolsStatic.Output("TagsChanged: remove: value=" + value);
                _labels = null;
                _changedEvent -= value;
            }
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

            DateTime time1 = DateTime.Now;
            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }

            if (this._labels == null) {
                var tup = AsmDudeToolsStatic.getLabelClashes(_sourceBuffer.CurrentSnapshot.GetText());
                this._labels = tup.Item1;
                this._labelClashes = tup.Item2;
            }

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(spans)) {

                ITextSnapshot snapshot = spans[0].Snapshot;
                NormalizedSnapshotSpanCollection tagSpans = asmTokenSpan.Span.GetSpans(snapshot);

                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.Label: 
                        {
                            string labelStr = tagSpans[0].GetText();
                            //AsmDudeToolsStatic.Output(string.Format("INFO: label \"{0}\".", labelStr));
                            if (!this._labels.ContainsKey(labelStr)) {

                                const string msg = "Undefined Label";

                                int pos = spans[0].Start;
                                int lineNumber = snapshot.GetLineNumberFromPosition(pos);

                                this.removeUndefinedLabelError(lineNumber);
                                ErrorTag errorTag = this.addUndefinedLabelError(lineNumber, msg);
                                yield return new TagSpan<ErrorTag>(tagSpans[0], errorTag);
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: 
                        {
                            string labelStr = tagSpans[0].GetText();
                            //AsmDudeToolsStatic.Output(string.Format("INFO: labelDef \"{0}\".", labelStr));
                            if (_labelClashes.ContainsKey(labelStr)) {

                                const string msgShort = "Label Clash";
                                string msgLong = "Label Clash: " + _labelClashes[labelStr];

                                int pos = spans[0].Start;
                                int lineNumber = snapshot.GetLineNumberFromPosition(pos);

                                this.removeClashLabelError(lineNumber);
                                ErrorTag errorTag = this.addClashLabelError(lineNumber, msgShort, msgLong);
                                yield return new TagSpan<ErrorTag>(tagSpans[0], errorTag);


                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic:
                        break;
                    default: break;
                }
            }

            //this._errorListProvider.Show(); // dont use BringToFront since that will select the error window.
            this._errorListProvider.Refresh();

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        private void removeUndefinedLabelError(int lineNumber) {
            for (int i = 0; i < this._labelUndefinedErrors.Count; ++i) {
                LabelErrorData value = this._labelUndefinedErrors[i];
                if (value._lineNumber == lineNumber) {
                    if (value._task != null) {
                        this._errorListProvider.Tasks.Remove(value._task);
                    }
                    AsmDudeToolsStatic.Output(string.Format("INFO: removing error {0} at line={1}", value._msg, lineNumber));
                    this._labelUndefinedErrors.RemoveAt(i);
                    return;
                }
            }
        }
        private void removeClashLabelError(int lineNumber) {
            for (int i = 0; i < this._labelClashErrors.Count; ++i) {
                LabelErrorData value = this._labelClashErrors[i];
                if (value._lineNumber == lineNumber) {
                    if (value._task != null) {
                        this._errorListProvider.Tasks.Remove(value._task);
                    }
                    AsmDudeToolsStatic.Output(string.Format("INFO: removing error {0} at line={1}", value._msg, lineNumber));
                    this._labelClashErrors.RemoveAt(i);
                    return;
                }
            }
        }

        private ErrorTag addUndefinedLabelError(int lineNumber, string msg) {

            ErrorTask errorTask = new ErrorTask();
            errorTask.Line = lineNumber;
            errorTask.Column = 0;
            errorTask.Text = msg;
            errorTask.ErrorCategory = TaskErrorCategory.Warning;
            errorTask.Document = this._filename;
            errorTask.Navigate += NavigateHandler;

            this._errorListProvider.Tasks.Add(errorTask);
            this._labelUndefinedErrors.Add(new LabelErrorData(lineNumber, errorTask, msg));

            AsmDudeToolsStatic.Output(string.Format("INFO: adding error {0} at line={1}", msg, lineNumber));


            //const string errorType = "syntax error";
            //const string errorType = "compiler error";
            const string errorType = "other error";
            //const string errorType = "warning";
            return new ErrorTag(errorType, msg);
        }
        private ErrorTag addClashLabelError(int lineNumber, string msgShort, string msgLong) {

            ErrorTask errorTask = new ErrorTask();
            errorTask.Line = lineNumber;
            errorTask.Column = 0;
            errorTask.Text = msgShort;
            errorTask.ErrorCategory = TaskErrorCategory.Warning;
            errorTask.Document = this._filename;
            errorTask.Navigate += NavigateHandler;

            this._errorListProvider.Tasks.Add(errorTask);
            this._labelClashErrors.Add(new LabelErrorData(lineNumber, errorTask, msgShort));

            AsmDudeToolsStatic.Output(string.Format("INFO: adding error {0} at line={1}", msgShort, lineNumber));


            //const string errorType = "syntax error";
            //const string errorType = "compiler error";
            const string errorType = "other error";
            //const string errorType = "warning";
            return new ErrorTag(errorType, msgLong);
        }

        #region IDisposable
        private void Dispose() {
            this._view.TextBuffer.Changed -= OnTextBufferChanged;
        }
        void IDisposable.Dispose() {
            Dispose();
        }
        #endregion

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            foreach (ITextChange textChange in e.Changes) {
                int newLineNumber = e.After.GetLineNumberFromPosition(textChange.NewPosition);
                int oldLineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                this.onTextChange(textChange, oldLineNumber, newLineNumber);
            }
        }

        private void onTextChange(ITextChange textChange, int oldLineNumber, int newLineNumber) {

            AsmDudeToolsStatic.Output(string.Format("INFO: onTextChange: oldLineNumber={0}; newLineNumber={1}; LineCountDelta={2}.", oldLineNumber, newLineNumber, textChange.LineCountDelta));

            int indexToRemove = -1;

            for (int i = 0; i < _labelUndefinedErrors.Count; ++i) {
                LabelErrorData value = this._labelUndefinedErrors[i];

                if (value._lineNumber == oldLineNumber) {
                    indexToRemove = i;
                }
                if (value._lineNumber > newLineNumber) {
                    if (textChange.LineCountDelta != 0) {
                        value._lineNumber += textChange.LineCountDelta;
                    }
                }
            }

            if (indexToRemove != -1) {
                var value = this._labelUndefinedErrors[indexToRemove];
                if (value._task != null) {
                    this._errorListProvider.Tasks.Remove(value._task);
                }
                this._labelUndefinedErrors.RemoveAt(indexToRemove);
            }
        }

        private void NavigateHandler(object sender, EventArgs arguments) {
            Task task = sender as Task;

            if (task == null) {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (String.IsNullOrEmpty(task.Document)) {
                return;
            }

            IVsUIShellOpenDocument openDoc = (IVsUIShellOpenDocument)Package.GetGlobalService(typeof(IVsUIShellOpenDocument));
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
            IVsTextManager mgr = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            if (mgr == null) {
                return;
            }
            mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, task.Column, task.Line, task.Column);
        }
    }
}
