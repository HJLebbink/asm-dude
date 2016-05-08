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

        #region Private Fields

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly string _filename;
        private readonly ILabelGraph _labelGraph;

        private object _updateLock = new object();

        #endregion Private Fields

        internal LabelErrorTagger(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> aggregator,
                ILabelGraph labelGraph,
                ErrorListProvider errorListProvider) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            this._sourceBuffer = buffer;
            this._aggregator = aggregator;
            this._labelGraph = labelGraph;
            this._errorListProvider = errorListProvider;

            this._filename = AsmDudeToolsStatic.GetFileName(buffer);
            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;
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
            
            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took LabelErrorTagger {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #region Private Methods

        private static int getLineNumber(SnapshotSpan span) {
            int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
            //int lineNumber2 = span.Snapshot.GetLineNumberFromPosition(span.End);
            //if (lineNumber != lineNumber2) {
            //    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:getLineNumber. line number from start {0} is not equal to line number from end {1}.", lineNumber, lineNumber2));
            //}
            return lineNumber;
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

                lock (this._updateLock) {

                    #region Update Tags
                    foreach (int lineNumber in this._labelGraph.getAllRelatedLineNumber()) {
                        TagsChanged(this, new SnapshotSpanEventArgs(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent));
                    }
                    #endregion Update Tags

                    #region Update Error Tasks
                    var errorTasks = this._errorListProvider.Tasks;

                    for (int i = errorTasks.Count - 1; i >= 0; --i) {
                        if (AsmErrorEnum.LABEL.HasFlag((AsmErrorEnum)errorTasks[i].SubcategoryIndex)) {
                            errorTasks.RemoveAt(i);
                        }
                    }

                    bool errorExists = false;

                    foreach (KeyValuePair<int, string> entry in this._labelGraph.labelClashes) {
                        ErrorTask errorTask = new ErrorTask();
                        errorTask.SubcategoryIndex = (int)AsmErrorEnum.LABEL_CLASH;
                        errorTask.Line = entry.Key;
                        errorTask.Column = 0;
                        errorTask.Text = entry.Value;
                        errorTask.ErrorCategory = TaskErrorCategory.Warning;
                        errorTask.Document = this._filename;
                        errorTask.Navigate += navigateHandler;
                        errorTasks.Add(errorTask);
                        errorExists = true;
                    }
                    foreach (KeyValuePair<int, string> entry in this._labelGraph.undefinedLabels) {
                        ErrorTask errorTask = new ErrorTask();
                        errorTask.SubcategoryIndex = (int)AsmErrorEnum.LABEL_UNDEFINED;
                        errorTask.Line = entry.Key;
                        errorTask.Column = 0;
                        errorTask.Text = entry.Value;
                        errorTask.ErrorCategory = TaskErrorCategory.Warning;
                        errorTask.Document = this._filename;
                        errorTask.Navigate += navigateHandler;
                        errorTasks.Add(errorTask);
                        errorExists = true;
                    }
                    if (errorExists) {
                        this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                        this._errorListProvider.Refresh();
                    }

                    #endregion Update Error Tasks
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
        #endregion Private Methods
    }
}
