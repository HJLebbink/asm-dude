using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;

namespace AsmDude.ErrorSquiggles {

    internal sealed class LabelErrorTagger : ITagger<ErrorTag>, IDisposable {

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        private readonly ErrorListProvider _errorListProvider;
        private readonly string _filename;

        private readonly IDictionary<int, UndefinedLabelErrorData> _labelUndefined;
        private readonly IDictionary<int, LabelClashErrorData> _labelClashes;
        private readonly ILabelGraph _labelGraph;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        internal LabelErrorTagger(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._errorListProvider = _asmDudeTools.GetErrorListProvider();
            this._filename = AsmDudeToolsStatic.GetFileName(buffer);

            this._labelUndefined = new Dictionary<int, UndefinedLabelErrorData>();
            this._labelClashes = new Dictionary<int, LabelClashErrorData>();
            this._labelGraph = new LabelGraph(buffer, asmTagAggregator);

            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;
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
            if (!this._labelGraph.isEnabled) {
                yield break;
            }

            DateTime time1 = DateTime.Now;
            
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in _aggregator.GetTags(spans)) {
                SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(_sourceBuffer)[0];

                //AsmDudeToolsStatic.Output(string.Format("INFO: ErrorTagger:GetTags: found keyword \"{0}\"", span.GetText()));

                switch (asmTokenTag.Tag.type) {
                    case AsmTokenType.Label: {
                            // an occurrence of a label is updated: 
                            //    1] check whether the label is undefined or defined

                            //int lineNumber = getLineNumber(span);
                            //this.removeUndefinedLabelError(lineNumber);
                            string label = tagSpan.GetText();

                            if (!this._labelGraph.hasLabel(label)) {
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", "Undefined Label"));
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: {
                            // a label definition is updated: 
                            //    1] check all occurrences of labels whether they become undefined or defined
                            //    2] check all definitions of labels whether they clash with the new label, or whether they do not clash anymore.

                            //int lineNumber = getLineNumber(span);
                            //this.removeLabelClashError(lineNumber);
                            string label = tagSpan.GetText();

                            if (this._labelGraph.hasLabelClash(label)) {

                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine("Label Clash");
                                foreach (int lineNumber in this._labelGraph.getLabelDefLineNumbers(label)) {
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                                    sb.AppendLine(AsmDudeToolsStatic.cleanup(string.Format("Label defined at LINE {0}: {1}", lineNumber + 1, lineContent)));
                                }
                                string msg = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", msg));
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
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took LabelErrorTagger {0:F3} seconds to make error tags.", elapsedSec));
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

        private void Dispose() {
            this._sourceBuffer.Changed -= OnTextBufferChanged;
        }
        #region IDisposable
        void IDisposable.Dispose() {
            Dispose();
        }
        #endregion

        async private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (!this._labelGraph.isEnabled) return;

            await System.Threading.Tasks.Task.Run(() => {
                HashSet<int> relatedLineNumbers;
                if (e.Changes.Count == 1) {
                    int lineNumber = e.After.GetLineNumberFromPosition(e.Changes[0].NewPosition);
                    relatedLineNumbers = this._labelGraph.getRelatedLineNumber(lineNumber);
                } else {
                    relatedLineNumbers = new HashSet<int>();
                    foreach (ITextChange textChange in e.Changes) {
                        int lineNumber = e.After.GetLineNumberFromPosition(textChange.NewPosition);
                        relatedLineNumbers.UnionWith(this._labelGraph.getRelatedLineNumber(lineNumber));
                    }
                }

                foreach (int lineNumber in relatedLineNumbers) {
                    TagsChanged(this, new SnapshotSpanEventArgs(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent));
                }
            });
        }

        private void navigateHandler(object sender, EventArgs arguments) {
            Task task = sender as Task;

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
    }
}
