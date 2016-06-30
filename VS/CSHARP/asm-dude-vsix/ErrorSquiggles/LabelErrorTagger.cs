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

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AsmDude.ErrorSquiggles {

    internal sealed class LabelErrorTagger : ITagger<ErrorTag> {

        #region Private Fields

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly ILabelGraph _labelGraph;

        private object _updateLock = new object();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion Private Fields

        internal LabelErrorTagger(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> aggregator,
                ILabelGraph labelGraph) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            this._sourceBuffer = buffer;
            this._aggregator = aggregator;
            this._errorListProvider = AsmDudeTools.Instance.errorListProvider;
            this._labelGraph = labelGraph;
            this._labelGraph.ResetDoneEvent += this.HandleLabelGraphResetDoneEvent;
            this._labelGraph.reset_Delayed();
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
                            if (Settings.Default.IntelliSenseDecorateUndefinedLabels) {
                                string label = tagSpan.GetText();
                                if (!this._labelGraph.hasLabel(label)) {
                                    var toolTipContent = undefinedlabelToolTipContent();
                                    yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
                                }
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef: {
                            if (Settings.Default.IntelliSenseDecorateClashingLabels) {
                                string label = tagSpan.GetText();
                                if (this._labelGraph.hasLabelClash(label)) {
                                    var toolTipContent = labelClashToolTipContent(label);
                                    yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
                                }
                            }
                            break;
                        }
                    default: break;
                }
            }
            AsmDudeToolsStatic.printSpeedWarning(time1, "LabelErrorTagger");
        }

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
            try {
                Run r1 = new Run("Label Clash:" + Environment.NewLine);
                r1.FontWeight = FontWeights.Bold;
                textBlock.Inlines.Add(r1);

                StringBuilder sb = new StringBuilder();
                foreach (uint id in this._labelGraph.getLabelDefLineNumbers(label)) {
                    int lineNumber = this._labelGraph.getLinenumber(id);
                    string filename = Path.GetFileName(this._labelGraph.getFilename(id));
                    string lineContent;
                    if (this._labelGraph.isFromMainFile(id)) {
                        lineContent = " :" + this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                    } else {
                        lineContent = "";
                    }
                    sb.AppendLine(AsmDudeToolsStatic.cleanup(string.Format("Defined at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                }
                string msg = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());

                Run r2 = new Run(msg);
                textBlock.Inlines.Add(r2);
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:labelClashToolTipContent; e={1}", this.ToString(), e.ToString()));
            }
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

        private int getKeywordBeginEnd(int lineNumber, string keyword) {
            int lengthKeyword = keyword.Length;
            string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
            int startPos = -1;
            for (int i = 0; i<lineContent.Length - lengthKeyword; ++i) {
                if (lineContent.Substring(i, lengthKeyword).Equals(keyword)) {
                    startPos = i;
                    break;
                }
            }

            if (startPos == -1) {
                return 0;
            }
            return (startPos | ((startPos + lengthKeyword) << 16));
        }

        private void HandleLabelGraphResetDoneEvent(object sender, CustomEventArgs e) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: received an event from labelGraph {0}", e.Message));
            this.updateErrorTasks();
        }

        async private void updateErrorTasks() {
            if (!this._labelGraph.isEnabled) return;

            await System.Threading.Tasks.Task.Run(() => {
                lock (this._updateLock) {
                    try {
                        #region Update Tags
                        var temp = this.TagsChanged;
                        if (temp != null) {
                            // is this code even reached?
                            foreach (uint id in this._labelGraph.getAllRelatedLineNumber()) {
                                if (this._labelGraph.isFromMainFile(id)) {
                                    int lineNumber = (int)id;
                                    temp(this, new SnapshotSpanEventArgs(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent));
                                }
                            }
                        }
                        #endregion Update Tags

                        #region Update Error Tasks
                        if (Settings.Default.IntelliSenseShowClashingLabels || Settings.Default.IntelliSenseShowUndefinedLabels) {

                            var errorTasks = this._errorListProvider.Tasks;

                            for (int i = errorTasks.Count - 1; i >= 0; --i) {
                                if (AsmErrorEnum.LABEL.HasFlag((AsmErrorEnum)errorTasks[i].SubcategoryIndex)) {
                                    errorTasks.RemoveAt(i);
                                }
                            }

                            bool errorExists = false;

                            if (Settings.Default.IntelliSenseShowClashingLabels) {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.labelClashes) {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.getLinenumber(entry.Key);

                                    ErrorTask errorTask = new ErrorTask();
                                    errorTask.SubcategoryIndex = (int)AsmErrorEnum.LABEL_CLASH;
                                    errorTask.Line = this._labelGraph.getLinenumber(entry.Key);
                                    errorTask.Column = getKeywordBeginEnd(lineNumber, label);
                                    errorTask.Text = "Label Clash: " + label;
                                    errorTask.ErrorCategory = TaskErrorCategory.Warning;
                                    errorTask.Document = this._labelGraph.getFilename(entry.Key);
                                    errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;
                                    errorTasks.Add(errorTask);
                                    errorExists = true;
                                }
                            }
                            if (Settings.Default.IntelliSenseShowUndefinedLabels) {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.undefinedLabels) {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.getLinenumber(entry.Key);

                                    ErrorTask errorTask = new ErrorTask();
                                    errorTask.SubcategoryIndex = (int)AsmErrorEnum.LABEL_UNDEFINED;
                                    errorTask.Line = lineNumber;
                                    errorTask.Column = getKeywordBeginEnd(lineNumber, label);
                                    errorTask.Text = "Undefined Label: " + label;
                                    errorTask.ErrorCategory = TaskErrorCategory.Warning;
                                    errorTask.Document = this._labelGraph.getFilename(entry.Key);
                                    errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;
                                    errorTasks.Add(errorTask);
                                    errorExists = true;
                                }
                            }
                            if (errorExists) {
                                this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                                this._errorListProvider.Refresh();
                            }
                        }
                        #endregion Update Error Tasks

                    } catch (Exception e) {
                        AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:updateErrorTasks; e={1}", this.ToString(), e.ToString()));
                    }
                }
            });
        }

        #endregion Private Methods
    }
}
