using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AsmDude.Tools {

#pragma warning disable CS0162

    public sealed class LabelGraph : IDisposable, ILabelGraph {

        #region Private Fields

        private static readonly SortedSet<int> emptySet = new SortedSet<int>();

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        private readonly IDictionary<string, IList<int>> _usedAt;
        private readonly IDictionary<string, IList<int>> _defAt;
        private readonly HashSet<int> _hasLabel;
        private readonly HashSet<int> _hasDef;

        private object _updateLock = new object();
        private bool _enabled;

        #endregion Private Fields

        #region Public Methods

        public LabelGraph(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:constructor"));
            _sourceBuffer = buffer;
            _aggregator = aggregator;

            _usedAt = new Dictionary<string, IList<int>>();
            _defAt = new Dictionary<string, IList<int>>();
            _hasLabel = new HashSet<int>();
            _hasDef = new HashSet<int>();

            _enabled = true;
            this.reset_Sync();
            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;
        }

        public bool isEnabled { get { return this._enabled; } }

        public SortedDictionary<int, string> labelClashes {
            get {
                SortedDictionary<int, string> result = new SortedDictionary<int, string>();
                string description = "Label Clash";
                foreach (KeyValuePair<string, IList<int>> entry in _defAt) {
                    if (entry.Value.Count > 1) {
                        foreach (int lineNumber in entry.Value) {
                            result.Add(lineNumber, description);
                        }
                    }
                }
                return result;
            }
        }

        public SortedDictionary<int, string> undefinedLabels {
            get {
                SortedDictionary<int, string> result = new SortedDictionary<int, string>();
                string description = "Undefined Label";
                foreach (KeyValuePair<string, IList<int>> entry in _usedAt) {
                    string label = entry.Key;
                    if (!this._defAt.ContainsKey(label)) {
                        foreach (int lineNumber in entry.Value) {
                            result.Add(lineNumber, description);
                        }
                    }
                }
                return result;
            }
        }

        public bool hasLabel(string label) {
            return this._defAt.ContainsKey(label);
        }

        public bool hasLabelClash(string label) {
            IList<int> list;
            if (this._defAt.TryGetValue(label, out list)) {
                return (list.Count > 1);
            }
            return false;
        }

        public SortedSet<int> getLabelDefLineNumbers(string label) {
            IList<int> list;
            if (this._defAt.TryGetValue(label, out list)) {
                return new SortedSet<int>(list);
            } else {
                return emptySet;
            }
        }

        public bool tryGetLineNumber(string label, out int lineNumber) {
            IList<int> list;
            if (this._defAt.TryGetValue(label, out list)) {
                lineNumber = list[0];
                return true;
            } else {
                lineNumber = -1;
                return false;
            }
        }

        public SortedSet<int> labelUsedAtInfo(string label) {
            IList<int> lines;
            if (this._usedAt.TryGetValue(label, out lines)) {
                return new SortedSet<int>(lines);
            } else {
                return emptySet;
            }
        }

        public void reset_Async() {
            if (!_enabled) return;
            ThreadPool.QueueUserWorkItem(reset_private);
        }

        public void reset_Sync() {
            if (!_enabled) return;

            DateTime time1 = DateTime.Now;
            lock (_updateLock) {

                _usedAt.Clear();
                _defAt.Clear();
                _hasLabel.Clear();
                _hasDef.Clear();

                this.addAll();
            }

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took LabelGraph {0:F3} seconds to reset.", elapsedSec));
            }
            if (elapsedSec > 2.0) {
                this.disable();
            }
        }

        public void Dispose() {
            this._sourceBuffer.Changed -= OnTextBufferChanged;
        }

        public HashSet<int> getAllRelatedLineNumber() {
            // it does not work to find all the currently related line numbers. This because, 
            // due to a change in label name any other label can have become related. What works 
            // is to return all line numbers of current labels definitions and usages.
            lock (_updateLock) {
                HashSet<int> lineNumbers = new HashSet<int>(this._hasDef);
                lineNumbers.UnionWith(this._hasLabel);
                if (false) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:getAllRelatedLineNumber results {0}", string.Join(",", lineNumbers)));
                }
                return lineNumbers;
            }
        }
        #endregion Public Methods

        #region Private Methods

        private void disable() {
            AsmDudeToolsStatic.Output(string.Format("WARNING: performance of LabelGraph is horrible, disabling label analysis"));

            this._enabled = false;
            lock (this._updateLock) {
                this._defAt.Clear();
                this._hasDef.Clear();
                this._usedAt.Clear();
                this._hasLabel.Clear();
            }
        }

        private void reset_private(object threadContext) {
            reset_Sync();
        }

        private static int getLineNumber(IMappingTagSpan<AsmTokenTag> tag) {
            return getLineNumber(tag.Span.GetSpans(tag.Span.AnchorBuffer)[0]);
        }

        private static int getLineNumber(SnapshotSpan span) {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (!_enabled) return;

            if (true) {
                this.reset_Async();
            } else {
                // experimental faster method, but it still has subtle bugs
                switch (e.Changes.Count) {
                    case 0: return;
                    case 1:
                        ITextChange textChange = e.Changes[0];
                        switch (textChange.LineCountDelta) {
                            case 0: {
                                    int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                    this.updateLineNumber(lineNumber);
                                }
                                break;
                            case 1: {
                                    int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                    //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                    this.shiftLineNumber(lineNumber + 1, 1);
                                    this.updateLineNumber(lineNumber);
                                }
                                break;
                            case -1: {
                                    int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                    this.shiftLineNumber(lineNumber + 1, -1);
                                    this.updateLineNumber(lineNumber);
                                    this.updateLineNumber(lineNumber - 1);
                                }
                                break;
                            default:
                                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: lineDelta={0}", textChange.LineCountDelta));
                                this.reset_Async();
                                break;
                        }
                        break;
                    default:
                        this.reset_Async();
                        break;
                }
            }
        }

        private void addAsmTag(int lineNumber, IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            lock (_updateLock) {
                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.LabelDef: {
                            string label = getText(asmTokenSpan);
                            IList<int> list;
                            if (this._defAt.TryGetValue(label, out list)) {
                                list.Add(lineNumber);
                            } else {
                                this._defAt.Add(label, new List<int> { lineNumber });
                            }
                            this._hasDef.Add(lineNumber);
                        }
                        break;
                    case AsmTokenType.Label: {
                            string label = getText(asmTokenSpan);
                            IList<int> list;
                            if (this._usedAt.TryGetValue(label, out list)) {
                                list.Add(lineNumber);
                            } else {
                                this._usedAt.Add(label, new List<int> { lineNumber });
                            }
                            this._hasLabel.Add(lineNumber);
                        }
                        break;
                    default: break;
                }
            }
        }

        private void addAll() {
            if (true) {
                for (int lineNumber = 0; lineNumber < this._sourceBuffer.CurrentSnapshot.LineCount; ++lineNumber) {
                    addLineNumber(lineNumber);
                }
            } else {
                SnapshotSpan span = new SnapshotSpan(this._sourceBuffer.CurrentSnapshot, new Span(0, this._sourceBuffer.CurrentSnapshot.Length));
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(span)) {
                    addAsmTag(getLineNumber(asmTokenSpan), asmTokenSpan);
                }
            }
        }

        private void updateLineNumber(int lineNumber) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:updateLineNumber: line {0}", lineNumber));
            this.addLineNumber(lineNumber);
            this.removeLineNumber(lineNumber);
        }

        private void addLineNumber(int lineNumber) {
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent)) {
                addAsmTag(lineNumber, asmTokenSpan);
            }
        }

        private void removeLineNumber(int lineNumber) {
            lock (_updateLock) {
                IList<string> toDelete = new List<string>();
                if (this._hasLabel.Remove(lineNumber)) {
                    foreach (KeyValuePair<string, IList<int>> entry in _usedAt) {
                        if (entry.Value.Remove(lineNumber)) {
                            if (entry.Value.Count == 0) {
                                toDelete.Add(entry.Key);
                            }
                        }
                    }
                }
                if (toDelete.Count > 0) {
                    foreach (string label in toDelete) {
                        _usedAt.Remove(label);
                    }
                }
                toDelete.Clear();
                if (this._hasDef.Remove(lineNumber)) {
                    foreach (KeyValuePair<string, IList<int>> entry in _defAt) {
                        if (entry.Value.Remove(lineNumber)) {
                            if (entry.Value.Count == 0) {
                                toDelete.Add(entry.Key);
                            }
                        }
                    }
                }
                if (toDelete.Count > 0) {
                    foreach (string label in toDelete) {
                        _defAt.Remove(label);
                    }
                }
            }
        }

        private string getText(IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            return asmTokenSpan.Span.GetSpans(_sourceBuffer)[0].GetText();
        }

        private void shiftLineNumber(int lineNumber, int lineCountDelta) {
            if (lineCountDelta > 0) {
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted +{1}", lineNumber, lineCountDelta));

                foreach (KeyValuePair<string, IList<int>> entry in this._usedAt) {
                    IList<int> values = entry.Value;
                    for (int i = 0; i < values.Count; ++i) {
                        if (values[i] >= lineNumber) {
                            values[i] += lineCountDelta;
                        }
                    }
                }
                {
                    int[] original = new int[this._hasLabel.Count];
                    this._hasLabel.CopyTo(original);
                    this._hasLabel.Clear();
                    foreach (int i in original) {
                        this._hasLabel.Add((i >= lineNumber) ? (i + lineCountDelta) : i);
                    }
                }
                foreach (KeyValuePair<string, IList<int>> entry in this._defAt) {
                    IList<int> values = entry.Value;
                    for (int i = 0; i < values.Count; ++i) {
                        if (values[i] >= lineNumber) {
                            values[i] += lineCountDelta;
                        }
                    }
                }
                {
                    int[] original = new int[this._hasDef.Count];
                    this._hasDef.CopyTo(original);
                    this._hasDef.Clear();
                    foreach (int i in original) {
                        this._hasDef.Add((i >= lineNumber) ? (i + lineCountDelta) : i);
                    }
                }
            } else {
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted {1}", lineNumber, lineCountDelta));
            }
        }

        #endregion Private Methods
    }
#pragma warning restore CS0162
}
