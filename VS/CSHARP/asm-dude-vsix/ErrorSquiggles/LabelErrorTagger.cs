using AsmDude.SyntaxHighlighting;
using AsmTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
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

        private readonly IDictionary<string, int> _labelDefLineNumber;
        private readonly IDictionary<string, IList<int>> _labelDefClashLineNumber;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        internal LabelErrorTagger(
                ITextView view, 
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

            this._view = view;
            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._errorListProvider = _asmDudeTools.GetErrorListProvider();
            this._filename = AsmDudeToolsStatic.GetFileName(buffer);

            this._labelUndefined = new Dictionary<int, UndefinedLabelErrorData>();
            this._labelClashes = new Dictionary<int, LabelClashErrorData>();

            this._labelDefLineNumber = new Dictionary<string, int>();
            this._labelDefClashLineNumber = new Dictionary<string, IList<int>>();

            this.initErrorCache();
            //this.initErrorsTasks();

            this._view.TextBuffer.Changed += OnTextBufferChanged;
        }

        [Flags]
        private enum ErrorCategoryEnum {
            NONE = 0,
            LABEL_UNDEFINED = 1<<1,
            LABEL_CLASH = 1<<2,
            ALL = LABEL_UNDEFINED | LABEL_CLASH
        }

        private class UndefinedLabelErrorData {
            internal readonly ErrorTask _errorTask;

            internal UndefinedLabelErrorData(int lineNumber, string msg, ErrorListProvider provider, string filename, EventHandler navigateHandler) {
                AsmDudeToolsStatic.Output(string.Format("INFO: UndefinedLabelErrorData: constructor. line={0}; msg={1}", lineNumber, msg));

                bool alreadyPresent = false;
                var e = provider.Tasks.GetEnumerator();
                while (e.MoveNext()) {
                    Task value = e.Current as Task;

                    AsmDudeToolsStatic.Output(string.Format("INFO: UndefinedLabelErrorData: current Task: line={0}; msg={1}", value.Line, value.Text));

                    if (value != null) {
                        if ((value.Line == lineNumber) && (value.SubcategoryIndex == (int)ErrorCategoryEnum.LABEL_UNDEFINED)) {
                            alreadyPresent = true;
                            break;
                        }
                    }
                }

                if (!alreadyPresent) {
                    this._errorTask = new ErrorTask();
                    this._errorTask.SubcategoryIndex = (int)ErrorCategoryEnum.LABEL_UNDEFINED;
                    this._errorTask.Line = lineNumber;
                    this._errorTask.Column = 0;
                    this._errorTask.Text = msg;
                    this._errorTask.ErrorCategory = TaskErrorCategory.Warning;
                    this._errorTask.Document = filename;
                    this._errorTask.Navigate += navigateHandler;
                    provider.Tasks.Add(this._errorTask);
                }
            }

            public int lineNumber { get { return this._errorTask.Line; } set { this._errorTask.Line = value; } }
            public string msg { get { return this._errorTask.Text; } set { this._errorTask.Text = value; } }
        }

        private class LabelClashErrorData {
            internal readonly ErrorTask _errorTask;

            internal LabelClashErrorData(int lineNumber, string msg, ErrorListProvider provider, string filename, EventHandler navigateHandler) {

                bool alreadyPresent = false;
                var e = provider.Tasks.GetEnumerator();
                while (e.MoveNext()) {
                    Task value = e.Current as Task;
                    if (value != null) {
                        if ((value.Line == lineNumber) && (value.SubcategoryIndex == (int)ErrorCategoryEnum.LABEL_CLASH)) {
                            alreadyPresent = true;
                            break;
                        }
                    }
                }

                if (!alreadyPresent) {
                    this._errorTask = new ErrorTask();
                    this._errorTask.SubcategoryIndex = (int)ErrorCategoryEnum.LABEL_CLASH;
                    this._errorTask.Line = lineNumber;
                    this._errorTask.Column = 0;
                    this._errorTask.Text = msg;
                    this._errorTask.ErrorCategory = TaskErrorCategory.Warning;
                    this._errorTask.Document = filename;
                    this._errorTask.Navigate += navigateHandler;
                    provider.Tasks.Add(this._errorTask);
                }
            }

            public int lineNumber { get { return this._errorTask.Line; } set { this._errorTask.Line = value; } }
            public string msg { get { return this._errorTask.Text; } set { this._errorTask.Text = value; } }
        }

        private void initErrorCache() {
            AsmDudeToolsStatic.Output(string.Format("INFO: initErrorCache"));
            this._labelDefLineNumber.Clear();
            this._labelDefClashLineNumber.Clear();

            ITextSnapshot snapshot = _sourceBuffer.CurrentSnapshot;

            #region initialize label caches
            for (int lineNumber = 0; lineNumber < snapshot.LineCount; ++lineNumber) {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(snapshot.GetLineFromLineNumber(lineNumber).Extent)) {

                    SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                    //AsmDudeToolsStatic.Output(string.Format("INFO: initAllErrors: line={0}; keyword=\"{1}\"", lineNumber, span.GetText()));

                    switch (asmTokenSpan.Tag.type) {
                        case AsmTokenType.LabelDef:
                            string label = span.GetText();
                            if (this._labelDefLineNumber.ContainsKey(label)) {
                                if (this._labelDefLineNumber[label] == lineNumber) {
                                    AsmDudeToolsStatic.Output(string.Format("WARNING: initAllErrors: label={0}; line={1}", label, lineNumber));
                                } else {
                                    if (this._labelDefClashLineNumber.ContainsKey(label)) {
                                        this._labelDefClashLineNumber[label].Add(lineNumber);
                                    } else {
                                        IList<int> lineNumbers = new List<int> { this._labelDefLineNumber[label], lineNumber };
                                        this._labelDefClashLineNumber.Add(label, lineNumbers);
                                    }
                                }
                            } else {
                                this._labelDefLineNumber.Add(label, lineNumber);
                            }
                            break;
                        default: break;
                    }
                }
            }
            #endregion initialize label caches
            if (false) {
                foreach (KeyValuePair<string, int> entry in _labelDefLineNumber) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: initAllErrors: label Def: line={0}; label={1}", entry.Value, entry.Key));
                }
                foreach (KeyValuePair<string, IList<int>> entry in _labelDefClashLineNumber) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: initAllErrors: label Clash: label={0}; lines={1}", entry.Key, string.Join(",", entry.Value)));
                }
            }
        }

        private void initErrorsTasks() {
            /*
            AsmDudeToolsStatic.Output(string.Format("INFO: initAllErrors: number of error tasks={0}", this._errorListProvider.Tasks.Count));

            this._errorListProvider.Tasks.Clear();
            this._labelUndefined.Clear();
            this._labelClashes.Clear();

            ITextSnapshot snapshot = _sourceBuffer.CurrentSnapshot;

            for (int lineNumber = 0; lineNumber < snapshot.LineCount; ++lineNumber) {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(snapshot.GetLineFromLineNumber(lineNumber).Extent)) {
                    switch (asmTokenSpan.Tag.type) {
                        case AsmTokenType.Label: {
                                SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                                string label = span.GetText();
                                if (!this._labelDefLineNumber.ContainsKey(label)) {
                                    if (_labelUndefined.ContainsKey(lineNumber)) {
                                        AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:updateLabel: line {0} already has a undefined label error", lineNumber));
                                    } else {
                                        _labelUndefined.Add(lineNumber, new UndefinedLabelErrorData(lineNumber, "Undefined Label \"" + label + "\"", this._errorListProvider, this._filename, this.navigateHandler));
                                    }
                                }
                                break;
                            }
                        case AsmTokenType.LabelDef: {
                                SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                                string label = span.GetText();
                                if (this._labelDefClashLineNumber.ContainsKey(label)) {
                                    if (_labelClashes.ContainsKey(lineNumber)) {
                                        AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:updateLabel: line {0} already has a undefined label error", lineNumber));
                                    } else {
                                        _labelClashes.Add(lineNumber, new LabelClashErrorData(lineNumber, "Label Clash", this._errorListProvider, this._filename, this.navigateHandler));
                                    }
                                }

                                break;
                            }
                        default: break;
                    }
                }
            }
            */
        }

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

                            //int lineNumber = getLineNumber(span);
                            //this.removeUndefinedLabelError(lineNumber);
                            string label = span.GetText();

                            if (!this._labelDefLineNumber.ContainsKey(label)) {
                                yield return new TagSpan<ErrorTag>(span, new ErrorTag("warning", "Undefined Label"));
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: {
                            // a label definition is updated: 
                            //    1] check all occurrences of labels whether they become undefined or defined
                            //    2] check all definitions of labels whether they clash with the new label, or whether they do not clash anymore.

                            //int lineNumber = getLineNumber(span);
                            //this.removeLabelClashError(lineNumber);
                            string label = span.GetText();

                            if (this._labelDefClashLineNumber.ContainsKey(label)) {
                                yield return new TagSpan<ErrorTag>(span, new ErrorTag("warning", "Label Clash"));
                            }
                            break;
                        }
                    default: break;
                }
            }
            
            //this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            //this._errorListProvider.Refresh();

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private static int getLineNumber(SnapshotSpan span) {
            int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
            //int lineNumber2 = span.Snapshot.GetLineNumberFromPosition(span.End);
            //if (lineNumber != lineNumber2) {
            //    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:getLineNumber. line number from start {0} is not equal to line number from end {1}.", lineNumber, lineNumber2));
            //}
            return lineNumber;
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
            if (_labelClashes.TryGetValue(lineNumber, out data)) {
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError: key={0}; line={1}; msg={2}", lineNumber, data._errorTask.Line, data._errorTask.Text));
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks before {0}", _errorListProvider.Tasks.Count));
                _errorListProvider.Tasks.Remove(data._errorTask);
                AsmDudeToolsStatic.Output(string.Format("INFO: removeUndefinedLabelError. nTasks after {0}", _errorListProvider.Tasks.Count));
            }
        }

        private Tuple<bool, ErrorTag> updateLabelDef(int lineNumber, string label, string line) {

            AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger:updateLabelDef: line={0}; keyword \"{1}\" is a labelDef", lineNumber, label));

            string clashDesc = "";
            foreach (KeyValuePair<string, int> entry in this._labelDefLineNumber) {
                if (entry.Key.Equals(label) && (entry.Value != lineNumber)) {
                    if (clashDesc.Length == 0) {
                        clashDesc = AsmDudeToolsStatic.cleanup("Multiple definitions for LABEL \"" + label + "\"") + Environment.NewLine;
                        clashDesc += "Label defined at LINE " + (lineNumber + 1) + Environment.NewLine;
                        clashDesc += "Label defined at LINE " + (entry.Value + 1);
                    } else {
                        clashDesc += Environment.NewLine + "Label defined at LINE " + (entry.Value + 1);
                    }
                }
            }
            if (clashDesc.Length == 0) {
                return new Tuple<bool, ErrorTag>(false, null);
            } else {
                if (_labelClashes.ContainsKey(lineNumber)) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:updateLabelDef: line {0} already has a label clash error", lineNumber));
                } else {
                    _labelClashes.Add(lineNumber, new LabelClashErrorData(lineNumber, "Label Clash", this._errorListProvider, this._filename, this.navigateHandler));
                }
                return new Tuple<bool, ErrorTag>(true, new ErrorTag("warning", clashDesc));
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
            //AsmDudeToolsStatic.Output("INFO: LabelErrorTagger:OnTextBufferChanged: number of changes=" + e.Changes.Count);

            //TODO check if the number of lines changed, if yes than dirty=true; if a label is changed than also dirty=true;
            // act as if everything changed.
            //_dirty = true;
            TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(_sourceBuffer.CurrentSnapshot, new Span(0, _sourceBuffer.CurrentSnapshot.Length))));

            if (e.Changes.IncludesLineChanges) {
                if (true) {
                    this.initErrorCache();
                } else {
                    foreach (ITextChange textChange in e.Changes) {
                        if (textChange.LineCountDelta != 0) {
                            int newLineNumber = e.After.GetLineNumberFromPosition(textChange.NewPosition);
                            int oldLineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                            this.onTextChange(textChange, oldLineNumber, newLineNumber);
                        }
                    }
                }
            }
            
        }
       
        private void onTextChange(ITextChange textChange, int oldLineNumber, int newLineNumber) {
            /*
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
           */
        }

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
