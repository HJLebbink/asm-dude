using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

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
            this.updateErrorTasks();
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
                //AsmDudeToolsStatic.Output(string.Format("INFO: ErrorTagger:GetTags: found keyword \"{0}\"", tagSpan.GetText()));

                switch (asmTokenTag.Tag.type) {
                    case AsmTokenType.Label: {
                            string label = tagSpan.GetText();
                            if (!this._labelGraph.hasLabel(label)) {
                                var toolTipContent = undefinedlabelToolTipContent();
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: {
                            string label = tagSpan.GetText();
                            if (this._labelGraph.hasLabelClash(label)) {
                                var toolTipContent = labelClashToolTipContent(label);
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
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

        private TextBlock undefinedlabelToolTipContent() {
            TextBlock textBlock = new TextBlock();
            Run r1 = new Run("Undefined Label");
            r1.FontWeight = FontWeights.Bold;
            textBlock.Inlines.Add(r1);
            return textBlock;
        }

        private TextBlock labelClashToolTipContent(string label) {
            TextBlock textBlock = new TextBlock();
            Run r1 = new Run("Label Clash:"+Environment.NewLine);
            r1.FontWeight = FontWeights.Bold;
            textBlock.Inlines.Add(r1);

            StringBuilder sb = new StringBuilder();
            foreach (int lineNumber in this._labelGraph.getLabelDefLineNumbers(label)) {
                string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                sb.AppendLine(AsmDudeToolsStatic.cleanup(string.Format("Label defined at LINE {0}: {1}", lineNumber + 1, lineContent)));
            }
            string msg = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());

            Run r2 = new Run(msg);
            textBlock.Inlines.Add(r2);
            return textBlock;
        }

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

        async private void updateErrorTasks() {
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
                        //errorTask.Column = 0;
                        errorTask.Text = entry.Value;
                        errorTask.ErrorCategory = TaskErrorCategory.Warning;
                        errorTask.Document = this._filename;
                        errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;
                        errorTasks.Add(errorTask);
                        errorExists = true;
                    }
                    foreach (KeyValuePair<int, string> entry in this._labelGraph.undefinedLabels) {
                        ErrorTask errorTask = new ErrorTask();
                        errorTask.SubcategoryIndex = (int)AsmErrorEnum.LABEL_UNDEFINED;
                        errorTask.Line = entry.Key;
                        //errorTask.Column = 0;
                        errorTask.Text = entry.Value;
                        errorTask.ErrorCategory = TaskErrorCategory.Warning;
                        errorTask.Document = this._filename;
                        errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;
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

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            this.updateErrorTasks();
        }
        #endregion Private Methods
    }
}
