using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsmDude.Tools {

    public class LabelGraph : IDisposable {
        private static readonly SortedSet<int> emptySet = new SortedSet<int>();

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        private readonly IDictionary<string, IList<IMappingTagSpan<AsmTokenTag>>> _usedAt;
        private readonly IDictionary<string, IList<IMappingTagSpan<AsmTokenTag>>> _defAt;
        private readonly IDictionary<int, IMappingTagSpan<AsmTokenTag>> _hasLabel;
        private readonly IDictionary<int, IMappingTagSpan<AsmTokenTag>> _hasDef;


        public LabelGraph(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            _sourceBuffer = buffer;
            _aggregator = aggregator;

            _usedAt = new Dictionary<string, IList<IMappingTagSpan<AsmTokenTag>>>();
            _defAt = new Dictionary<string, IList<IMappingTagSpan<AsmTokenTag>>>();
            _hasLabel = new Dictionary<int, IMappingTagSpan<AsmTokenTag>>();
            _hasDef = new Dictionary<int, IMappingTagSpan<AsmTokenTag>>();

            this.reset();
            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;
        }

        public bool hasLabel(string label) {
            if (this._defAt.ContainsKey(label)) {
                return true;
            }
            return false;
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
                SortedSet<int> clashes1 = new SortedSet<int>();
                foreach (IMappingTagSpan<AsmTokenTag> asmTag in list) {
                    clashes1.Add(getLineNumber(asmTag));
                }
                return clashes1;
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
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:labelUsedAtInfo: label {0} has lineNumber={1}", label, lineNumber));
                    string label2 = span.GetText();
                    if (!label2.Equals(label)) {
                        AsmDudeToolsStatic.Output(string.Format("ERROR: LabelGraph:labelUsedAtInfo:"));
                    }
                    lines2.Add(lineNumber);
                }
                return lines2;
            } else {
                return emptySet;
            }
        }

        public string toString() {
            StringBuilder sb = new StringBuilder();
            /*
            foreach (KeyValuePair<string, int> entry in this._definedAt) {
                sb.AppendLine(string.Format("INFO: getLabelDefinitionInfo: label Def: line={0}; label={1}", entry.Value, entry.Key));
            }
            foreach (KeyValuePair<string, IList<int>> entry in this._labelDefClashLineNumber) {
                sb.AppendLine(string.Format("INFO: getLabelDefinitionInfo: label Clash: label={0}; lines={1}", entry.Key, string.Join(",", entry.Value)));
            }
            */
            return sb.ToString();
        }

        public void reset() {
            DateTime time1 = DateTime.Now;

            _usedAt.Clear();
            _defAt.Clear();
            _hasLabel.Clear();
            _hasDef.Clear();

            this.addAll();

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

            HashSet<int> results = new HashSet<int>(this._hasDef.Keys);
            results.UnionWith(this._hasLabel.Keys);
            return results;
        }


        #region Private Stuff


        private static int getLineNumber(IMappingTagSpan<AsmTokenTag> tag) {
            return getLineNumber(tag.Span.GetSpans(tag.Span.AnchorBuffer)[0]);
        }

        private static int getLineNumber(SnapshotSpan span) {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        async private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            await Task.Run(() => {
                if (true) {
                    this.reset();
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
            });
        }

        private void addAsmTag(int lineNumber, IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            switch (asmTokenSpan.Tag.type) {
                case AsmTokenType.LabelDef: {
                        string label = getText(asmTokenSpan);

                        IList<IMappingTagSpan<AsmTokenTag>> list;
                        if (this._defAt.TryGetValue(label, out list)) {
                            list.Add(asmTokenSpan);
                        } else {
                            this._defAt.Add(label, new List<IMappingTagSpan<AsmTokenTag>> { asmTokenSpan });
                        }
                        if (this._hasDef.ContainsKey(lineNumber)) {
                            AsmDudeToolsStatic.Output(string.Format("WARNING: LabelGraph:addLineNumber: hasDef already has a value for lineNumber {0}", lineNumber));
                            this._hasDef.Remove(lineNumber);
                        }
                        this._hasDef.Add(lineNumber, asmTokenSpan);
                        break;
                    }
                case AsmTokenType.Label: {
                        string label = getText(asmTokenSpan);

                        IList<IMappingTagSpan<AsmTokenTag>> list;
                        if (this._usedAt.TryGetValue(label, out list)) {
                            list.Add(asmTokenSpan);
                        } else {
                            this._usedAt.Add(label, new List<IMappingTagSpan<AsmTokenTag>> { asmTokenSpan });
                        }
                        if (this._hasLabel.ContainsKey(lineNumber)) {
                            AsmDudeToolsStatic.Output(string.Format("WARNING: LabelGraph:addLineNumber: hasLabel already has a value for lineNumber {0}", lineNumber));
                            this._hasLabel.Remove(lineNumber);
                        }
                        this._hasLabel.Add(lineNumber, asmTokenSpan);
                        break;
                    }
                default: break;
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
}
