// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
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

using AsmDude.ErrorSquiggles;
using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AsmDude.Tools {

    public sealed class LabelGraph : ILabelGraph {

        #region Private Fields

        private static readonly SortedSet<uint> emptySet = new SortedSet<uint>();

        private readonly ITextBuffer _buffer;
        private readonly IBufferTagAggregatorFactoryService _aggregatorFactory;
        private readonly ErrorListProvider _errorListProvider;
        private readonly ITextDocumentFactoryService _docFactory;
        private readonly IContentType _contentType;

        private readonly string _thisFilename;
        private readonly IDictionary<uint, string> _filenames;

        private readonly IDictionary<string, IList<uint>> _usedAt;
        private readonly IDictionary<string, IList<uint>> _defAt;
        private readonly ISet<uint> _hasLabel;
        private readonly ISet<uint> _hasDef;

        private object _updateLock = new object();
        private bool _enabled;

        private bool _busy;
        private bool _waiting;
        private bool _scheduled;

        #endregion Private Fields

        #region Public Methods

        public LabelGraph(
                ITextBuffer buffer,
                IBufferTagAggregatorFactoryService aggregatorFactory,
                ErrorListProvider errorListProvider,
                ITextDocumentFactoryService docFactory,
                IContentType contentType) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:constructor: creating a label graph for {0}", AsmDudeToolsStatic.GetFileName(buffer)));
            this._buffer = buffer;
            this._aggregatorFactory = aggregatorFactory;
            this._errorListProvider = errorListProvider;
            this._docFactory = docFactory;
            this._contentType = contentType;

            this._filenames = new Dictionary<uint, string>();
            this._usedAt = new Dictionary<string, IList<uint>>();
            this._defAt = new Dictionary<string, IList<uint>>();
            this._hasLabel = new HashSet<uint>();
            this._hasDef = new HashSet<uint>();

            this._thisFilename = AsmDudeToolsStatic.GetFileName(this._buffer);
            this._enabled = true;
            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this._buffer.ChangedLowPriority += this.BufferChanged;
            this.reset_Delayed();
        }

        public int getLinenumber(uint id) {
            return (int)(id & 0x00FFFFFF);
        }

        public uint getFileId(uint id) {
            return (id >> 24);
        }
        public string getFilename(uint id) {
            return this._filenames[this.getFileId(id)];
        }
        public uint makeId(int lineNumber, uint fileId) {
            return (fileId << 24) | (uint)lineNumber;
        }
        public bool isFromMainFile(uint id) {
            return id <= 0xFFFFFF;
        }

        public bool isEnabled { get { return this._enabled; } }

        public SortedDictionary<uint, string> labelClashes {
            get {
                SortedDictionary<uint, string> result = new SortedDictionary<uint, string>();
                lock (_updateLock) {
                    foreach (KeyValuePair<string, IList<uint>> entry in _defAt) {
                        if (entry.Value.Count > 1) {
                            string label = entry.Key;
                            foreach (uint id in entry.Value) {
                                result.Add(id, label);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public SortedDictionary<uint, string> undefinedLabels {
            get {
                SortedDictionary<uint, string> result = new SortedDictionary<uint, string>();
                 lock (_updateLock) {
                    foreach (KeyValuePair<string, IList<uint>> entry in _usedAt) {
                        string label = entry.Key;
                        if (!this._defAt.ContainsKey(label)) {
                            foreach (uint id in entry.Value) {
                                result.Add(id, label);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public SortedDictionary<string, string> getLabelDescriptions {
            get {
                SortedDictionary<string, string> result = new SortedDictionary<string, string>();
                lock (_updateLock) {
                    foreach (KeyValuePair<string, IList<uint>> entry in _defAt) {
                        uint id = entry.Value[0];
                        int lineNumber = getLinenumber(id);
                        string filename = Path.GetFileName(getFilename(id));
                        string lineContent;
                        if (this.isFromMainFile(id)) {
                            lineContent = " :" + this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                        } else {
                            lineContent = "";
                        }
                        result.Add(entry.Key, AsmDudeToolsStatic.cleanup(string.Format("LINE {0} ({1}){2}", (lineNumber+1), filename, lineContent)));
                    }
                }
                return result;
            }
        }

        public bool hasLabel(string label) {
            return this._defAt.ContainsKey(label);
        }

        public bool hasLabelClash(string label) {
            IList<uint> list;
            if (this._defAt.TryGetValue(label, out list)) {
                return (list.Count > 1);
            }
            return false;
        }

        public SortedSet<uint> getLabelDefLineNumbers(string label) {
            IList<uint> list;
            if (this._defAt.TryGetValue(label, out list)) {
                return new SortedSet<uint>(list);
            } else {
                return emptySet;
            }
        }

        public SortedSet<uint> labelUsedAtInfo(string label) {
            IList<uint> lines;
            if (this._usedAt.TryGetValue(label, out lines)) {
                return new SortedSet<uint>(lines);
            } else {
                return emptySet;
            }
        }

        public void reset_Delayed() {
            if (this._waiting) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:reset_delayed: already waiting for execution. Skipping this call."));
                return;
            }
            if (this._busy) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:reset_delayed: busy; scheduling this call."));
                this._scheduled = true;
            } else {
                //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:reset_delayed: going to execute this call."));
                if (true) {
                    AsmDudeTools.Instance.threadPool.QueueWorkItem(this.reset2);
                } else {
                    ThreadPool.QueueUserWorkItem(this.reset);
                }
            }
        }

        private void reset2() {
            this.reset(null);
        }
        private void reset(object threadContext) {
            if (!this._enabled) return;

            this._waiting = true;
            Thread.Sleep(AsmDudePackage.msSleepBeforeAsyncExecution);
            this._busy = true;
            this._waiting = false;

            #region Payload
            lock (_updateLock) {
                DateTime time1 = DateTime.Now;

                _usedAt.Clear();
                _defAt.Clear();
                _hasLabel.Clear();
                _hasDef.Clear();
                _filenames.Clear();
                _filenames.Add(0, AsmDudeToolsStatic.GetFileName(this._buffer));

                this.addAll(this._buffer, 0);

                AsmDudeToolsStatic.printSpeedWarning(time1, "LabelGraph");
                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowShutdownThresholdSec) {
                    this.disable();
                }
            }
            #endregion Payload

            this.OnResetDoneEvent(new CustomEventArgs("Resetting LabelGraph is finished"));

            this._busy = false;
            if (this._scheduled) {
                this._scheduled = false;
                this.reset_Delayed();
            }
        }

        // Declare the event using EventHandler<T>
        public event EventHandler<CustomEventArgs> ResetDoneEvent;

        public ErrorListProvider errorListProvider { get { return this._errorListProvider; } }

        public IList<int> getAllRelatedLineNumber() {
            // it does not work to find all the currently related line numbers. This because, 
            // due to a change in label name any other label can have become related. What works 
            // is to return all line numbers of current labels definitions and usages.
            lock (_updateLock) {

                IList<int> lineNumbers = new List<int>();
                foreach (uint id in _hasDef) {
                    if (isFromMainFile(id)) {
                        lineNumbers.Add((int)id);
                    }
                }
                foreach (uint id in _hasLabel) {
                    if (isFromMainFile(id)) {
                        lineNumbers.Add((int)id);
                    }
                }
                if (false) {
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:getAllRelatedLineNumber results {0}", string.Join(",", lineNumbers)));
                }
                return lineNumbers;
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void disable() {
            string msg = string.Format("Performance of LabelGraph is horrible: disabling label analysis for {0}.", this._thisFilename);
            AsmDudeToolsStatic.Output(string.Format("WARNING: " + msg));

            this._enabled = false;
            lock (this._updateLock) {
                this._buffer.ChangedLowPriority -= this.BufferChanged;
                this._defAt.Clear();
                this._hasDef.Clear();
                this._usedAt.Clear();
                this._hasLabel.Clear();
            }
            AsmDudeToolsStatic.disableMessage(msg, this._thisFilename, this._errorListProvider);
        }

        private static int getLineNumber(IMappingTagSpan<AsmTokenTag> tag) {
            return getLineNumber(tag.Span.GetSpans(tag.Span.AnchorBuffer)[0]);
        }

        private static int getLineNumber(SnapshotSpan span) {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        private void OnResetDoneEvent(CustomEventArgs e) {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber un-subscribes
            // immediately after the null check and before the event is raised.
            EventHandler<CustomEventArgs> handler = ResetDoneEvent;

            // Event will be null if there are no subscribers
            if (handler != null) {
                // Format the string to send inside the CustomEventArgs parameter
                e.Message += String.Format(" at {0}", DateTime.Now.ToString());

                // Use the () operator to raise the event.
                handler(this, e);
            }
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (!_enabled) return;

            if (true) {
                this.reset_Delayed();
            } else {
                lock (_updateLock) {
                    // experimental faster method, but it still has subtle bugs
                    switch (e.Changes.Count) {
                        case 0: return;
                        case 1:
                            ITextChange textChange = e.Changes[0];
                            ITextBuffer buffer = this._buffer;
                            ITagAggregator<AsmTokenTag> aggregator = null;


                            switch (textChange.LineCountDelta) {
                                case 0: {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        this.updateLineNumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case 1: {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        this.shiftLineNumber(lineNumber + 1, 1);
                                        this.updateLineNumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case -1: {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        this.shiftLineNumber(lineNumber + 1, -1);
                                        this.updateLineNumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                        this.updateLineNumber(buffer, aggregator, lineNumber - 1, (uint)lineNumber);
                                    }
                                    break;
                                default:
                                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:OnTextBufferChanged: lineDelta={0}", textChange.LineCountDelta));
                                    this.reset_Delayed();
                                    break;
                            }
                            break;
                        default:
                            this.reset_Delayed();
                            break;
                    }
                }
            }
        }

        private void addAll(ITextBuffer buffer, uint fileId) {
            ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.getAggregator(buffer, this._aggregatorFactory);
            lock (_updateLock) {
                if (fileId == 0) {
                    for (int lineNumber = 0; lineNumber < buffer.CurrentSnapshot.LineCount; ++lineNumber) {
                        this.addLineNumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                    }
                } else {
                    for (int lineNumber = 0; lineNumber < buffer.CurrentSnapshot.LineCount; ++lineNumber) {
                        this.addLineNumber(buffer, aggregator, lineNumber, makeId(lineNumber, fileId));
                    }
                }
            }
        }

        private void updateLineNumber(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator, int lineNumber, uint id) {
            AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:updateLineNumber: line {0}", lineNumber));
            this.addLineNumber(buffer, aggregator, lineNumber, id);
            this.removeLineNumber(lineNumber, id);
        }

        private void addLineNumber(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator, int lineNumber, uint id) {

            IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = aggregator.GetTags(buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
            var enumerator = tags.GetEnumerator();

            while (enumerator.MoveNext()) {
                var asmTokenSpan = enumerator.Current;
                switch (asmTokenSpan.Tag.type) {
                    case AsmTokenType.LabelDef: {
                            string label = getText(buffer, asmTokenSpan);
                            IList<uint> list;

                            if (this._defAt.TryGetValue(label, out list)) {
                                list.Add(id);
                            } else {
                                this._defAt.Add(label, new List<uint> { id });
                            }
                            //AsmDudeToolsStatic.Output("INFO: LabelGraph:addLineNumber: found label \"" +label +"\" at line " + lineNumber);
                            this._hasDef.Add(id);
                        }
                        break;
                    case AsmTokenType.Label: {
                            string label = getText(buffer, asmTokenSpan);
                            IList<uint> list;
                            if (this._usedAt.TryGetValue(label, out list)) {
                                list.Add(id);
                            } else {
                                this._usedAt.Add(label, new List<uint> { id });
                            }
                            //AsmDudeToolsStatic.Output("INFO: LabelGraph:addLineNumber: used label \"" + label + "\" at line " + lineNumber);
                            this._hasLabel.Add(id);
                        }
                        break;
                    case AsmTokenType.Directive:
                        if (getText(buffer, asmTokenSpan).Equals("INCLUDE", StringComparison.OrdinalIgnoreCase)) {
                            if (enumerator.MoveNext()) {
                                this.handleInclude(getText(buffer, enumerator.Current));
                            }
                        }
                        break;
                    default:
                        //AsmDudeToolsStatic.Output("INFO: LabelGraph:addLineNumber: found text \"" + getText(buffer, asmTokenSpan) + "\" at line " + lineNumber);
                        break;
                }
            }
        }

        private void handleInclude(string includeFilename) {
            try {
                if (includeFilename.Length < 1) {
                    return;
                }
                if (includeFilename.Length > 2) {
                    if (includeFilename.StartsWith("[") && includeFilename.EndsWith("]")) {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    } else if (includeFilename.StartsWith("\"") && includeFilename.EndsWith("\"")) {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    }
                }
                string filePath = Path.GetDirectoryName(_thisFilename) + Path.DirectorySeparatorChar + includeFilename;

                if (File.Exists(filePath)) {
                    //AsmDudeToolsStatic.Output("INFO: LabelGraph:handleInclude: including file " + filePath);
                } else {
                    AsmDudeToolsStatic.Output("WARNING: LabelGraph:handleInclude: file " + filePath + " does not exist");
                    return;
                }

                if (!this._filenames.Values.Contains(filePath)) {
                    bool characterSubstitutionsOccurred;
                    ITextDocument doc = this._docFactory.CreateAndLoadTextDocument(filePath, this._contentType, true, out characterSubstitutionsOccurred);
                    //AsmDudeToolsStatic.Output(doc.TextBuffer.CurrentSnapshot.GetText());

                    doc.FileActionOccurred += Doc_FileActionOccurred;
                    uint fileId = (uint)this._filenames.Count;
                    this._filenames.Add(fileId, filePath);
                    this.addAll(doc.TextBuffer, fileId);
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output("WARNING: LabelGraph:handleInclude. Exception:" + e.Message);
            }
        }

        private void Doc_FileActionOccurred(Object sender, TextDocumentFileActionEventArgs e) {
            ITextDocument doc = sender as ITextDocument;
            AsmDudeToolsStatic.Output("INFO: LabelGraph:Doc_FileActionOccurred: "+doc.FilePath +":" + e.FileActionType);
        }

        private void removeLineNumber(int lineNumber, uint id) {
            /*
            lock (_updateLock) {
            IList<string> toDelete = new List<string>();
            if (this._hasLabel.Remove(lineNumber)) {
                foreach (KeyValuePair<string, IList<uint>> entry in this._usedAt) {
                    if (entry.Value.Remove(lineNumber)) {
                        if (entry.Value.Count == 0) {
                            toDelete.Add(entry.Key);
                        }
                    }
                }
            }
            if (toDelete.Count > 0) {
                foreach (string label in toDelete) {
                    this._usedAt.Remove(label);
                }
            }
            toDelete.Clear();
            if (this._hasDef.Remove(lineNumber)) {
                foreach (KeyValuePair<string, IList<uint>> entry in this._defAt) {
                    if (entry.Value.Remove(lineNumber)) {
                        if (entry.Value.Count == 0) {
                            toDelete.Add(entry.Key);
                        }
                    }
                }
            }
            if (toDelete.Count > 0) {
                foreach (string label in toDelete) {
                    this._defAt.Remove(label);
                }
            }
        }
            */
        }

        private string getText(ITextBuffer buffer, IMappingTagSpan<AsmTokenTag> asmTokenSpan) {
            return asmTokenSpan.Span.GetSpans(buffer)[0].GetText();
        }

        private void shiftLineNumber(int lineNumber, int lineCountDelta) {
            if (lineCountDelta > 0) {
                /*
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted +{1}", lineNumber, lineCountDelta));

                foreach (KeyValuePair<string, IList<uint>> entry in this._usedAt) {
                    IList<uint> values = entry.Value;
                    for (int i = 0; i < values.Count; ++i) {
                        if (values[i] >= lineNumber) {
                            values[i] = values[i] + lineCountDelta;
                        }
                    }
                }
                {
                    uint[] original = new uint[this._hasLabel.Count];
                    this._hasLabel.CopyTo(original);
                    this._hasLabel.Clear();
                    foreach (uint i in original) {
                        this._hasLabel.Add((i >= lineNumber) ? (i + lineCountDelta) : i);
                    }
                }
                foreach (KeyValuePair<string, IList<uint>> entry in this._defAt) {
                    IList<uint> values = entry.Value;
                    for (int i = 0; i < values.Count; ++i) {
                        if (values[i] >= lineNumber) {
                            values[i] += lineCountDelta;
                        }
                    }
                }
                {
                    uint[] original = new uint[this._hasDef.Count];
                    this._hasDef.CopyTo(original);
                    this._hasDef.Clear();
                    foreach (uint i in original) {
                        this._hasDef.Add((i >= lineNumber) ? (i + lineCountDelta) : i);
                    }
                }
                */
            } else {
                AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:shiftLineNumber: starting from line {0} everything is shifted {1}", lineNumber, lineCountDelta));
            }
        }

        #endregion Private Methods
    }
}
