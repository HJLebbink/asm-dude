using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmDude.Tools {

    class LabelGraph : IDisposable {

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly IDictionary<string, IList<int>> _labelUsedAtInfo;
        private readonly IDictionary<string, int> _labelDefLineNumber;
        private readonly IDictionary<string, IList<int>> _labelDefClashLineNumber;

        public LabelGraph(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            _sourceBuffer = buffer;
            _aggregator = aggregator;

            _labelUsedAtInfo = new Dictionary<string, IList<int>>();
            _labelDefLineNumber = new Dictionary<string, int>();
            _labelDefClashLineNumber = new Dictionary<string, IList<int>>();

            this.reset();
            this._sourceBuffer.Changed += OnTextBufferChanged;
        }

        /// <summary>
        /// Returns dictionary of labels with the line numbers in which they are used
        /// </summary>
        public IDictionary<string, IList<int>> labelUsedAtInfo { get { return _labelUsedAtInfo; } }
        public IDictionary<string, int> labelDefInfo { get { return _labelDefLineNumber; } }
        public IDictionary<string, IList<int>> labelDefClashInfo { get { return _labelDefClashLineNumber; } }

        public void reset() {
            init_labelUsedAtInfo();
            init_labelDefInfo();
        }

        private void init_labelUsedAtInfo() {

            _labelUsedAtInfo.Clear();
            ITextSnapshot snapshot = _sourceBuffer.CurrentSnapshot;

            for (int lineNumber = 0; lineNumber < snapshot.LineCount; ++lineNumber) {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(snapshot.GetLineFromLineNumber(lineNumber).Extent)) {
                    switch (asmTokenSpan.Tag.type) {
                        case AsmTokenType.Label:
                            SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                            string label = span.GetText();
                            if (!_labelUsedAtInfo.ContainsKey(label)) {
                                _labelUsedAtInfo[label] = new List<int>(1);
                            }
                            _labelUsedAtInfo[label].Add(lineNumber);
                            break;
                        default: break;
                    }
                }
            }
        }

        public bool updateLabelUsageInfo(int lineNumber) {
            return true;
        }

        public string toString() {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> entry in this._labelDefLineNumber) {
                sb.AppendLine(string.Format("INFO: getLabelDefinitionInfo: label Def: line={0}; label={1}", entry.Value, entry.Key));
            }
            foreach (KeyValuePair<string, IList<int>> entry in this._labelDefClashLineNumber) {
                sb.AppendLine(string.Format("INFO: getLabelDefinitionInfo: label Clash: label={0}; lines={1}", entry.Key, string.Join(",", entry.Value)));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns dictionary of labels with the line numbers in which they are defined
        /// </summary>
        public void init_labelDefInfo() {

            _labelDefLineNumber.Clear();
            _labelDefClashLineNumber.Clear();

            ITextSnapshot snapshot = _sourceBuffer.CurrentSnapshot;

            for (int lineNumber = 0; lineNumber < snapshot.LineCount; ++lineNumber) {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(snapshot.GetLineFromLineNumber(lineNumber).Extent)) {
                    switch (asmTokenSpan.Tag.type) {
                        case AsmTokenType.LabelDef:
                            SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                            string label = span.GetText();
                            if (_labelDefLineNumber.ContainsKey(label)) {
                                if (_labelDefLineNumber[label] == lineNumber) {
                                    AsmDudeToolsStatic.Output(string.Format("WARNING: getLabelDefinitionInfo: label={0}; line={1}", label, lineNumber));
                                } else {
                                    if (_labelDefClashLineNumber.ContainsKey(label)) {
                                        _labelDefClashLineNumber[label].Add(lineNumber);
                                    } else {
                                        IList<int> lineNumbers = new List<int> { _labelDefLineNumber[label], lineNumber };
                                        _labelDefClashLineNumber.Add(label, lineNumbers);
                                    }
                                }
                            } else {
                                _labelDefLineNumber.Add(label, lineNumber);
                            }
                            break;
                        default: break;
                    }
                }
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));

            this.reset();
            /*
            if (e.Changes.IncludesLineChanges) {
                this._labelGraph.reset();
            } else {
                foreach (ITextChange textChange in e.Changes) {
                    int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                    this._labelGraph.updateLabelUsageInfo(lineNumber);
                }
            }
            */
        }

        public void Dispose() {
            this._sourceBuffer.Changed -= OnTextBufferChanged;
        }
    }
}
