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

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using AsmDude.SyntaxHighlighting;
using System.Text;
using AsmTools;
using AsmDude.Tools;

namespace AsmDude.QuickInfo {

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    internal sealed class AsmQuickInfoSource : IQuickInfoSource {

        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ITextBuffer _sourceBuffer;
        private readonly LabelGraph _labelGraph;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        public AsmQuickInfoSource(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            this._aggregator = aggregator;
            this._sourceBuffer = buffer;

            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
            this._labelGraph = new LabelGraph(buffer, aggregator);
        }

        /// <summary>
        /// Determine which pieces of Quickinfo content should be displayed
        /// </summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            applicableToSpan = null;
            try {
                DateTime time1 = DateTime.Now;

                ITextSnapshot snapshot = _sourceBuffer.CurrentSnapshot;
                var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
                if (triggerPoint == null) {
                    return;
                }
                string keyword = "";

                foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in this._aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {

                    SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(_sourceBuffer).First();
                    keyword = tagSpan.GetText();

                    //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:AugmentQuickInfoSession. keyword=\"{1}\"", this.ToString(), keyword));
                    string keywordUpper = keyword.ToUpper();
                    applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);

                    string description = null;

                    switch (asmTokenTag.Tag.type) {
                        case AsmTokenType.Misc: {
                                string descr = this._asmDudeTools.getDescription(keywordUpper);
                                description = (descr.Length > 0) ? ("Keyword " + keywordUpper + ": " + descr) : "Keyword " + keywordUpper;
                                break;
                            }
                        case AsmTokenType.Directive: {
                                string descr = this._asmDudeTools.getDescription(keywordUpper);
                                description = (descr.Length > 0) ? ("Directive " + keywordUpper + ": " + descr) : "Directive " + keywordUpper;
                                break;
                            }
                        case AsmTokenType.Register: {
                                string descr = this._asmDudeTools.getDescription(keywordUpper);
                                description = (descr.Length > 0) ? (keywordUpper + ": " + descr) : "Register " + keywordUpper;
                                break;
                            }
                        case AsmTokenType.Mnemonic: // intentional fall through
                        case AsmTokenType.Jump: {
                                string descr = this._asmDudeTools.getDescription(keywordUpper);
                                description = (descr.Length > 0) ? ("Mnemonic " + keywordUpper + ": " + descr) : "Mnemonic " + keywordUpper;
                                break;
                            }
                        case AsmTokenType.Label: {
                                string descr = this.getLabelDescription(keyword);
                                description = (descr.Length > 0) ? descr : "Label " + keyword;
                                break;
                            }
                        case AsmTokenType.LabelDef: {
                                string descr = this.getLabelDefDescription(keyword);
                                description = (descr.Length > 0) ? descr : "Label " + keyword;
                                break;
                            }
                        //case AsmTokenType.Constant: {
                        //        description = "Constant " + keyword;
                        //        break;
                        //    }
                        default:
                            break;
                    }
                    if (description != null) {
                        quickInfoContent.Add(AsmSourceTools.linewrap(description, AsmDudePackage.maxNumberOfCharsInToolTips + 1));
                    }
                }

                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to retrieve quick info for tag \"{1}\".", elapsedSec, keyword));
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:AugmentQuickInfoSession; e={1}", this.ToString(), e.ToString()));
            }
        }

        private string getLabelDescription(string label) {
            StringBuilder sb = new StringBuilder();
            foreach (int lineNumber in this._labelGraph.getLabelDefLineNumbers(label)) {
                sb.AppendLine(AsmDudeToolsStatic.cleanup(string.Format("Label defined at LINE {0}: {1}", lineNumber + 1, this.getLineContent(lineNumber))));
            }
            string result = sb.ToString();
            return result.TrimEnd(Environment.NewLine.ToCharArray());
        }

        private string getLabelDefDescription(string label) {

            SortedSet<int> usage = this._labelGraph.labelUsedAtInfo(label);
            if (usage.Count > 0) {
                StringBuilder sb = new StringBuilder();
                foreach (int lineNumber in usage) {
                    sb.AppendLine(AsmDudeToolsStatic.cleanup(string.Format("Label used at LINE {0}: {1}", lineNumber + 1, this.getLineContent(lineNumber))));
                    //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:getLabelDefDescription; sb=\"{1}\"", this.ToString(), sb.ToString()));
                }
                string result = sb.ToString();
                return result.TrimEnd(Environment.NewLine.ToCharArray());
            } else {
                return AsmDudeToolsStatic.cleanup(string.Format("Unused Label {0}", label));
            }
        }

        private string getLineContent(int lineNumber) {
            return this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
        }

        public void Dispose() {
            //empty
        }
    }
}

