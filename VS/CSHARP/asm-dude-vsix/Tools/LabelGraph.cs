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

        public HashSet<int> getRelatedLineNumber(int lineNumber) {
            // it does not work to find all the currently related line numbers. This because, 
            // due to a change in label name any other label can have become related. What works 
            // is to return all line numbers of current labels definitions and usages.
            lock (_updateLock) {
                HashSet<int> lineNumbers = new HashSet<int>(this._hasDef);
                lineNumbers.UnionWith(this._hasLabel);
                if (false) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:getRelatedLineNumber results {0}", string.Join(",", lineNumbers)));
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

    public sealed class LabelGraph_OLD : IDisposable, ILabelGraph {
        private static readonly SortedSet<int> emptySet = new SortedSet<int>();

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        private readonly IDictionary<string, IList<IMappingTagSpan<AsmTokenTag>>> _usedAt;
        private readonly IDictionary<string, IList<IMappingTagSpan<AsmTokenTag>>> _defAt;
        private readonly IDictionary<int, IMappingTagSpan<AsmTokenTag>> _hasLabel;
        private readonly IDictionary<int, IMappingTagSpan<AsmTokenTag>> _hasDef;

        private object _updateLock = new object();

        public bool isEnabled { get { return true; } }

        public LabelGraph_OLD(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:constructor"));
            _sourceBuffer = buffer;
            _aggregator = aggregator;

            _usedAt = new Dictionary<string, IList<IMappingTagSpan<AsmTokenTag>>>();
            _defAt = new Dictionary<string, IList<IMappingTagSpan<AsmTokenTag>>>();
            _hasLabel = new Dictionary<int, IMappingTagSpan<AsmTokenTag>>();
            _hasDef = new Dictionary<int, IMappingTagSpan<AsmTokenTag>>();

            this.reset_Async();
            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;
        }

        public bool hasLabel(string label) {
            return this._defAt.ContainsKey(label);
        }

        public bool hasLabelClash(string label) {
            IList<IMappingTagSpan<AsmTokenTag>> list;
            if (this._defAt.TryGetValue(label, out list)) {
                return (list.Count > 1);
            }
            return false;
        }

        public SortedSet<int> getLabelDefLineNumbers(string label) {
            IList<IMappingTagSpan<AsmTokenTag>> list;
            if (this._defAt.TryGetValue(label, out list)) {
                SortedSet<int> lineNumbers = new SortedSet<int>();
                foreach (IMappingTagSpan<AsmTokenTag> asmTag in list) {
                    lineNumbers.Add(getLineNumber(asmTag));
                }
                return lineNumbers;
            } else {
                return emptySet;
            }
        }

        public bool tryGetLineNumber(string label, out int lineNumber) {
            IList<IMappingTagSpan<AsmTokenTag>> list;
            if (this._defAt.TryGetValue(label, out list)) {
                lineNumber = getLineNumber(list[0]);
                return true;
            } else {
                lineNumber = -1;
                return false;
            }
        }

        public SortedSet<int> labelUsedAtInfo(string label) {
            ITextSnapshot snapshot = this._sourceBuffer.CurrentSnapshot;
            IList<IMappingTagSpan<AsmTokenTag>> lines;
            if (this._usedAt.TryGetValue(label, out lines)) {
                SortedSet<int> lines2 = new SortedSet<int>();
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in lines) {

                    SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                    int lineNumber = getLineNumber(span);
                    //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:labelUsedAtInfo: label {0} has lineNumber={1}", label, lineNumber));
                    //string label2 = span.GetText();
                    //if (!label2.Equals(label)) {
                    //    AsmDudeToolsStatic.Output(string.Format("ERROR: LabelGraph:labelUsedAtInfo:"));
                    //}
                    lines2.Add(lineNumber);
                }
                return lines2;
            } else {
                return emptySet;
            }
        }

        public void reset_Async() {
            ThreadPool.QueueUserWorkItem(reset_private);
        }

        private void reset_private(object threadContext) {
            reset_Sync();
        }

        public void reset_Sync() {
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
        }

        public void Dispose() {
            this._sourceBuffer.Changed -= OnTextBufferChanged;
        }

        public HashSet<int> getRelatedLineNumber(int lineNumber) {
            // it does not work to find all the currently related line numbers. This because, 
            // due to a change in label name any other label can have become related. What works 
            // is to return all line numbers of current labels definitions and usages.
            lock (_updateLock) {
                HashSet<int> lineNumbers = new HashSet<int>(this._hasDef.Keys);
                lineNumbers.UnionWith(this._hasLabel.Keys);
                if (false) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:getRelatedLineNumber results {0}", string.Join(",", lineNumbers)));
                }
                return lineNumbers;
            }
        }


        #region Private Stuff


        private static int getLineNumber(IMappingTagSpan<AsmTokenTag> tag) {
            return getLineNumber(tag.Span.GetSpans(tag.Span.AnchorBuffer)[0]);
        }

        private static int getLineNumber(SnapshotSpan span) {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (true) {
                this.reset_Async();
            } else {
                foreach (ITextChange textChange in e.Changes) {
                    int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                    this.addLineNumber(lineNumber);
                    this.removeDefLineNumber(lineNumber);
                    this.removeUsageLineNumber(lineNumber);
                    if (textChange.LineCountDelta != 0) {
                        //this.shiftLineNumber(lineNumber, textChange.LineCountDelta);
                    }
                }
            }
        }

        private void addAsmTag(int lineNumber, IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            lock (_updateLock) {
                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.LabelDef: {
                            if (this._hasDef.ContainsKey(lineNumber)) {
                                //This happens with the an update is not finished and a second update interleaves
                                //AsmDudeToolsStatic.Output(string.Format("WARNING: LabelGraph:addLineNumber: hasDef already has a value for lineNumber={0} with label={1}", lineNumber, getText(this._hasDef[lineNumber])));
                            } else {
                                string label = getText(asmTokenSpan);
                                IList<IMappingTagSpan<AsmTokenTag>> list;
                                if (this._defAt.TryGetValue(label, out list)) {
                                    list.Add(asmTokenSpan);
                                } else {
                                    this._defAt.Add(label, new List<IMappingTagSpan<AsmTokenTag>> { asmTokenSpan });
                                }
                                this._hasDef.Add(lineNumber, asmTokenSpan);
                            }
                            break;
                        }
                    case AsmTokenType.Label: {
                            if (this._hasLabel.ContainsKey(lineNumber)) {
                                //This happens with the an update is not finished and a second update interleaves
                                //AsmDudeToolsStatic.Output(string.Format("WARNING: LabelGraph:addLineNumber: hasLabel already has a value for lineNumber={0} with label={1}", lineNumber, getText(this._hasLabel[lineNumber])));
                            } else {
                                string label = getText(asmTokenSpan);
                                IList<IMappingTagSpan<AsmTokenTag>> list;
                                if (this._usedAt.TryGetValue(label, out list)) {
                                    list.Add(asmTokenSpan);
                                } else {
                                    this._usedAt.Add(label, new List<IMappingTagSpan<AsmTokenTag>> { asmTokenSpan });
                                }
                                this._hasLabel.Add(lineNumber, asmTokenSpan);
                            }
                            break;
                        }
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

        private void addLineNumber(int lineNumber) {
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in _aggregator.GetTags(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent)) {
                addAsmTag(lineNumber, asmTokenSpan);
            }
        }

        private void removeDefLineNumber(int lineNumber) {
            /*
            foreach (KeyValuePair<string, IMappingTagSpan<AsmTokenTag>> entry in _definedAt) {
                if (getLineNumber(entry.Value) == lineNumber) {
                    _definedAt.Remove(entry.Key);
                }
            }



            if (_labelDefClashLineNumber.ContainsKey(label)) {
                    _labelDefClashLineNumber[label].Remove(lineNumber);
                    if (_labelDefClashLineNumber[label].Count <= 1) {
                        _labelDefClashLineNumber.Remove(label);
                    }
                }
            }
           */
        }

        private void removeUsageLineNumber(int lineNumber) {
            /* if (_usedAt2.ContainsKey(lineNumber)) {
                 string label = _usedAt2[lineNumber];
                 ITextSnapshot snapshot = this._sourceBuffer.CurrentSnapshot;


                 IList<IMappingTagSpan<AsmTokenTag>> usage;
                 if (this._usedAtX.TryGetValue(label, out usage)) {
                     IMappingTagSpan<AsmTokenTag> asmTokenSpan = this._usedAtX[label];

                     for (int i = (usage.Count - 1); (i >= 0); --i) {

                         SnapshotSpan span = asmTokenSpan.Span.GetSpans(snapshot)[0];
                         int lineNumber2 = getLineNumber(span);
                         if (.)
                     }

                     usage.Remove(lineNumber);
                     if (usage.Count == 0) {
                         this._usedAt.Remove(label);
                     }
                 }
                 
            this._usedAt2.Remove(lineNumber);
            }
            */
        }

        private string getText(IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            return asmTokenSpan.Span.GetSpans(_sourceBuffer)[0].GetText();
        }

        private void shiftLineNumber(int lineNumber, int lineCountDelta) {
            if (lineCountDelta > 0) {
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted +{1}", lineNumber, lineCountDelta));
                /*
                foreach (KeyValuePair<string, IList<int>> entry in _usedAt) {
                    for (int i = 0; i < entry.Value.Count; ++i) {
                        if (entry.Value[i] > lineNumber) {
                            entry.Value[i] += lineCountDelta;
                        }
                    } 
                }
                for (int i = 0; i < _usedAt2.Count; ++i) {
                    //KeyValuePair<int, string> entry = _usedAt2.
                }
                foreach (KeyValuePair<int, string> entry in _usedAt2) {
                    if (entry.Key > lineNumber) {
                        
                    }
                }
                */
            } else {
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted {1}", lineNumber, lineCountDelta));
            }
        }

        #endregion Private Stuff
    }
#pragma warning restore CS0162
}
