using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace AsmDude.ErrorSquiggles {

    internal sealed class ErrorTagger : ITagger<ErrorTag> {

        private ITextView _view;
        private ITextBuffer _sourceBuffer;
        private ITagAggregator<AsmTokenTag> _aggregator;
        private ITextSearchService _textSearchService;


        internal ErrorTagger(
                ITextView view, 
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                ITextSearchService textSearchService) {
            this._view = view;
            this._sourceBuffer = buffer;
            this._aggregator = asmTagAggregator;
            this._textSearchService = textSearchService;

            this._view.LayoutChanged += ViewLayoutChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        IEnumerable<ITagSpan<ErrorTag>> ITagger<ErrorTag>.GetTags(NormalizedSnapshotSpanCollection spans) {

            DateTime time1 = DateTime.Now;
            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }

            IDictionary<string, string> labels = AsmDudeToolsStatic.getLabels(_sourceBuffer.CurrentSnapshot.GetText());

            foreach (IMappingTagSpan<AsmTokenTag> tagSpan in _aggregator.GetTags(spans)) {
                NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);

                switch (tagSpan.Tag.type) {
                    case TokenType.Label:

                        string labelStr = tagSpans[0].GetText();
                        //AsmDudeToolsStatic.Output(string.Format("INFO: label \"{0}\".", labelStr));
                        if (!labels.ContainsKey(labelStr)) {
                            string msg = String.Format("LABEL \"{0}\" is undefined.", labelStr);
                            AsmDudeToolsStatic.Output(string.Format("INFO: {0}", msg));
                            yield return new TagSpan<ErrorTag>(tagSpans[0], new ErrorTag("smell", msg));
                        }
                        break;
                    case TokenType.Mnemonic:
                        break;
                    default: break;
                }
            }
            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to make error tags.", elapsedSec));
            }
        }

        void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
            if (e.NewSnapshot != e.OldSnapshot) {//make sure that there has really been a change
                //UpdateAtCaretPosition(_view.Caret.Position);
            }
        }

    }

}
