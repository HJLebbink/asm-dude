using AsmDude.SyntaxHighlighting;
using AsmTools;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace AsmDude.ErrorSquiggles {

    internal sealed class LabelErrorTagger : ITagger<ErrorTag>, IDisposable {

        private readonly ITextView _view;
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly string _filename;

        private readonly IDictionary<int, UndefinedLabelErrorData> _labelUndefined;
        private readonly IDictionary<int, LabelClashErrorData> _labelClashes;

        bool _dirty;

        internal LabelErrorTagger(
                ITextView view, 
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator) {

            this._view = view;
            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._errorListProvider = AsmDudeToolsStatic.GetErrorListProvider();
            this._filename = AsmDudeToolsStatic.GetFileName(buffer);

            this._labelUndefined = new Dictionary<int, UndefinedLabelErrorData>();
            this._labelClashes = new Dictionary<int, LabelClashErrorData>();

            this._dirty = true;

            this._view.TextBuffer.Changed += OnTextBufferChanged;
        }


        private class UndefinedLabelErrorData {
            internal readonly ErrorTask _errorTask;

            internal UndefinedLabelErrorData(int lineNumber, string msg, ErrorListProvider provider, string filename, EventHandler navigateHandler) {
                AsmDudeToolsStatic.Output(string.Format("INFO: UndefinedLabelErrorData: constructor. line={0}; msg={1}", lineNumber, msg));
                ErrorTask task = new ErrorTask();
                task.Line = lineNumber;
                task.Column = 0;
                task.Text = msg;
                task.ErrorCategory = TaskErrorCategory.Warning;
                task.Document = filename;
                task.Navigate += navigateHandler;
                provider.Tasks.Add(task);
                this._errorTask = task;
            }

            public int lineNumber { get { return this._errorTask.Line; } set { this._errorTask.Line = value; } }
            public string msg { get { return this._errorTask.Text; } set { this._errorTask.Text = value; } }
        }

        private class LabelClashErrorData {
            private readonly ErrorListProvider _provider;
            internal readonly IList<ErrorTask> _errorTasks;
            internal readonly IList<int> _lineNumbers;

            internal LabelClashErrorData(IList<int> lineNumbers, string msg, ErrorListProvider provider, string filename, EventHandler navigateHandler) {
                _provider = provider;
                _lineNumbers = lineNumbers;
                _errorTasks = new List<ErrorTask>(lineNumbers.Count);
               
                for (int i = 0; i<lineNumbers.Count; ++i) {
                    ErrorTask task = new ErrorTask();
                    task.Line = lineNumbers[i];
                    task.Column = 0;
                    task.Text = msg;
                    task.ErrorCategory = TaskErrorCategory.Warning;
                    task.Document = filename;
                    task.Navigate += navigateHandler;
                    provider.Tasks.Add(task);
                    this._errorTasks.Add(task);
                }
            }
            ~LabelClashErrorData() {
                for (int i = 0; i < lineNumbers.Count; ++i) {
                    _provider.Tasks.Remove(this._errorTasks[i]);
                }
            }


            public IList<int> lineNumbers {
                get {
                    IList<int> list = new List<int>(this._errorTasks.Count);
                    for (int i = 0; i< this._errorTasks.Count; ++i) {
                        list[i] = this._errorTasks[i].Line;
                    }
                    return list;
                }
                set {
                    for (int i = 0; i < this._errorTasks.Count; ++i) {
                        this._errorTasks[i].Line = value[i];
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private static int getLineNumber(SnapshotSpan span) {
            int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
            int lineNumber2 = span.Snapshot.GetLineNumberFromPosition(span.End);

            if (lineNumber != lineNumber2) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:getLineNumber. line number from start {0} is not equal to line number from end {1}.", lineNumber, lineNumber2));
            }
            return lineNumber;
        }

        private void printCurrentUndefinedLabels() {
            AsmDudeToolsStatic.Output(string.Format("INFO: printCurrentUndefinedLabels begin"));
            foreach (KeyValuePair<int, UndefinedLabelErrorData> entry in _labelUndefined) {
                AsmDudeToolsStatic.Output(string.Format("INFO: undefined label: key={0}; line={1}; msg={2}", entry.Key, entry.Value.lineNumber, entry.Value.msg));
            }
            AsmDudeToolsStatic.Output(string.Format("INFO: printCurrentUndefinedLabels end"));
        }

        /*
        private void updateErrors(NormalizedSnapshotSpanCollection spans) {

            IDictionary<string, string> labels = new Dictionary<string, string>();

            ITextSnapshot snapshot = spans[0].Snapshot;

            #region pass 1: store and analyze label definitions
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(spans)) {

                SnapshotSpan x = asmTokenSpan.Span.GetSpans(snapshot)[0];
                AsmDudeToolsStatic.Output(string.Format("INFO: ErrorTagger:determineLabelErrors: found keyword \"{0}\"", x.GetText()));

                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.LabelDef: {
                            string label = x.GetText();
                            int lineNumber = x.Snapshot.GetLineNumberFromPosition(x.Start);
                            string line = x.Snapshot.GetLineFromLineNumber(lineNumber).Snapshot.GetText();
                            string labelDesc = AsmDudeToolsStatic.cleanup("LINE " + lineNumber + ": " + line);

                            if (labels.ContainsKey(label)) {
                                string clashDesc = "";
                                IList<int> clashingLineNumbers;


                                if (this._labelClashes.ContainsKey(label)) { // update existing data
                                    this._labelClashes[label]

                                    this._labelClashes[label].Item1.Add(lineNumber);
                                    this._labelClashes[label].Item2 = this._labelClashes[label].Item2 + Environment.NewLine + labelDesc;
                                    clashDesc = _labelClashes[label] + Environment.NewLine;
                                    clashingLineNumbers = 
                                } else { // create new label clash data
                                    clashDesc = AsmDudeToolsStatic.cleanup("Multiple definitions for LABEL \"" + label + "\"") + Environment.NewLine;
                                    clashDesc += labels[label] + Environment.NewLine;
                                }
                                clashDesc += labelDesc;
                                AsmDudeToolsStatic.Output(string.Format("INFO: found label clash for label \"{0}\"; description={1}", label, clashDesc));
                                _labelClashes.Add(label, new Tuple<IList<int>, string, SnapshotSpan>(lineNumber, clashDesc, x));
                            } else {
                                labels.Add(label, labelDesc);
                            }
                        }
                        break;
                    default: break;
                }
            }
            #endregion
            #region pass 2: store and analyze usage of labels
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(spans)) {
                NormalizedSnapshotSpanCollection tagSpans = asmTokenSpan.Span.GetSpans(snapshot);
                SnapshotSpan x = tagSpans[0];

                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.Label: {
                            string label = x.GetText();
                            if (!labels.ContainsKey(label)) {
                                int lineNumber = x.Snapshot.GetLineNumberFromPosition(x.Start);
                                AsmDudeToolsStatic.Output(string.Format("INFO: found undefined label \"{0}\"; at line {1}", label, lineNumber));
                                _labelUndefined.Add(label, new Tuple<int, SnapshotSpan>(lineNumber, x));
                            }
                        }
                        break;
                    default: break;
                }
            }
            #endregion
        }

            */
        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }

            DateTime time1 = DateTime.Now;
            ITextSnapshot snapshot = spans[0].Snapshot;

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(spans)) {

                SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                //AsmDudeToolsStatic.Output(string.Format("INFO: ErrorTagger:GetTags: found keyword \"{0}\"", span.GetText()));

                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.Label: {
                            // an occurrence of a label is updated: 
                            //    1] check whether the label is undefined or defined

                            int lineNumber = getLineNumber(span);
                            this.removeUndefinedLabelError(lineNumber);
                            this.printCurrentUndefinedLabels();

                            string label = span.GetText();
                            string line = span.Snapshot.GetLineFromLineNumber(lineNumber).Snapshot.GetText();
                            Tuple<bool, ErrorTag> errorTag = this.updateLabel(lineNumber, label, line);
                            if (errorTag.Item1) {
                                yield return new TagSpan<ErrorTag>(span, errorTag.Item2);
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: {
                            // a label definition is updated: 
                            //    1] check all occurrences of labels whether they become undefined or defined
                            //    2] check all definitions of labels whether they clash with the new label, or whether they do not clash anymore.

                            int lineNumber = getLineNumber(span);
                            this.removeLabelClashError(lineNumber);

                            string label = span.GetText();
                            string line = span.Snapshot.GetLineFromLineNumber(lineNumber).Snapshot.GetText();
                            Tuple<bool, ErrorTag> errorTag = this.updateLabelDef(lineNumber, label, line);
                            if (errorTag.Item1) {
                                yield return new TagSpan<ErrorTag>(span, errorTag.Item2);
                            }
                            break;
                        }
                    default: break;
                }
            }

            this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            this._errorListProvider.Refresh();

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        private void removeUndefinedLabelError(int lineNumber) {
            UndefinedLabelErrorData data;
            if (_labelUndefined.TryGetValue(lineNumber, out data)) {
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError: key={0}; line={1}; msg={2}", lineNumber, data._errorTask.Line, data._errorTask.Text));
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks before {0}", _errorListProvider.Tasks.Count));
                _errorListProvider.Tasks.Remove(data._errorTask);
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks after {0}", _errorListProvider.Tasks.Count));
            }
        }

        private void removeLabelClashError(int lineNumber) {
           LabelClashErrorData data;
            /*
            if (_labelClashes.TryGetValue(lineNumber, out data)) {
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError: key={0}; line={1}; msg={2}", lineNumber, data._errorTask.Line, data._errorTask.Text));
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks before {0}", _errorListProvider.Tasks.Count));
                _errorListProvider.Tasks.Remove(data._errorTask);
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks after {0}", _errorListProvider.Tasks.Count));
            }
            */
        }

        private Tuple<bool, ErrorTag> updateLabel(int lineNumber, string label, string line) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger:updateLabel: line={0}; keyword \"{1}\" is a label", lineNumber, label));
            IDictionary<int, string> labels = AsmSourceTools.getLineNumberWithLabelDef(this._sourceBuffer.CurrentSnapshot.GetText());

            bool definedLabel = false;
            foreach (KeyValuePair<int, string> entry in labels) {
                if (entry.Value.Equals(label)) {
                    definedLabel = true;
                    break;
                }
            }
            if (definedLabel) {
                return new Tuple<bool, ErrorTag>(false, null);
            } else {
                if (_labelUndefined.ContainsKey(lineNumber)) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:updateLabel: line {0} already has a undefined label error", lineNumber));
                } else {
                    _labelUndefined.Add(lineNumber, new UndefinedLabelErrorData(lineNumber, "Undefined Label \""+label+"\"", this._errorListProvider, this._filename, this.navigateHandler));
                }

                //const string errorType = "syntax error";
                //const string errorType = "compiler error";
                const string errorType = "other error";
                //const string errorType = "warning";
                return new Tuple<bool, ErrorTag>(true, new ErrorTag(errorType, "Undefined Label"));
            }
        }

        private Tuple<bool, ErrorTag> updateLabelDef(int lineNumber, string label, string line) {

            AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger:updateLabelDef: line={0}; keyword \"{1}\" is a labelDef", lineNumber, label));
            IDictionary<int, string> labels = AsmSourceTools.getLineNumberWithLabelDef(this._sourceBuffer.CurrentSnapshot.GetText());

            IList<int> lineNumbers = new List<int>(0);
            string clashDesc = "";
            foreach (KeyValuePair<int, string> entry in labels) {
                if (entry.Value.Equals(label) && (entry.Key != lineNumber)) {
                    if (clashDesc.Length == 0) {
                        lineNumbers.Add(entry.Key);
                        clashDesc = AsmDudeToolsStatic.cleanup("Multiple definitions for LABEL \"" + label + "\"") + Environment.NewLine;
                        clashDesc += "Label defined at LINE "+ lineNumber+ Environment.NewLine;
                        clashDesc += "Label defined at LINE " + entry.Key;
                    } else {
                        clashDesc += Environment.NewLine + "Label defined at LINE " + entry.Key;
                    }
                    lineNumbers.Add(lineNumber);
                }
            }
            if (lineNumbers.Count == 0) {
                return new Tuple<bool, ErrorTag>(false, null);
            } else {
                if (_labelClashes.ContainsKey(lineNumber)) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:updateLabelDef: line {0} already has a label clash error", lineNumber));
                } else {
                    _labelClashes.Add(lineNumber, new LabelClashErrorData(lineNumbers, "Label Clash", this._errorListProvider, this._filename, this.navigateHandler));
                }

                //const string errorType = "syntax error";
                //const string errorType = "compiler error";
                const string errorType = "other error";
                //const string errorType = "warning";
                return new Tuple<bool, ErrorTag>(true, new ErrorTag(errorType, clashDesc));
            }
        }

        private void Dispose() {
            this._view.TextBuffer.Changed -= OnTextBufferChanged;
        }
        #region IDisposable
        void IDisposable.Dispose() {
            Dispose();
        }
        #endregion

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            AsmDudeToolsStatic.Output("INFO: LabelErrorTagger:OnTextBufferChanged: number of changes=" + e.Changes.Count);

            //TODO check if the number of lines changed, if yes than dirty=true; if a label is changed than also dirty=true;
            // act as if everything changed.
            //_dirty = true;
            //TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(_sourceBuffer.CurrentSnapshot, new Span(0, _sourceBuffer.CurrentSnapshot.Length))));

            
            foreach (ITextChange textChange in e.Changes) {
                int newLineNumber = e.After.GetLineNumberFromPosition(textChange.NewPosition);
                int oldLineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);

                //this.onTextChange(textChange, oldLineNumber, newLineNumber);
            }
            
        }
        /*
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
        */

        private void navigateHandler(object sender, EventArgs arguments) {
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
