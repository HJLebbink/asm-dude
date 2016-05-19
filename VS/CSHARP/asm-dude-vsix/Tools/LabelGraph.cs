using AsmDude.ErrorSquiggles;
using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AsmDude.Tools {

#   pragma warning disable CS0162

    public sealed class LabelGraph : IDisposable, ILabelGraph {

        #region Private Fields

        private static readonly SortedSet<uint> emptySet = new SortedSet<uint>();

        private readonly ITextBuffer _sourceBuffer;
        private readonly IBufferTagAggregatorFactoryService _aggregatorFactory;
        private readonly ErrorListProvider _errorListProvider;
        private readonly ITextDocumentFactoryService _docFactory;
        private readonly IContentType _contentType;

        private readonly IDictionary<uint, string> _filenames;

        private readonly IDictionary<string, IList<uint>> _usedAt;
        private readonly IDictionary<string, IList<uint>> _defAt;
        private readonly HashSet<uint> _hasLabel;
        private readonly HashSet<uint> _hasDef;

        private object _updateLock = new object();
        private bool _enabled;

        #endregion Private Fields

        #region Public Methods

        public LabelGraph(
                ITextBuffer buffer,
                IBufferTagAggregatorFactoryService aggregatorFactory,
                ErrorListProvider errorListProvider,
                ITextDocumentFactoryService docFactory,
                IContentType contentType) {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph: constructor: creating a label graph for {0}", AsmDudeToolsStatic.GetFileName(buffer)));
            this._sourceBuffer = buffer;
            this._aggregatorFactory = aggregatorFactory;
            this._errorListProvider = errorListProvider;
            this._docFactory = docFactory;
            this._contentType = contentType;

            this._filenames = new Dictionary<uint, string>();
            this._usedAt = new Dictionary<string, IList<uint>>();
            this._defAt = new Dictionary<string, IList<uint>>();
            this._hasLabel = new HashSet<uint>();
            this._hasDef = new HashSet<uint>();

            this._enabled = true;
            this.reset_Sync();
            this._sourceBuffer.ChangedLowPriority += OnTextBufferChanged;

            this.addInfoToErrorTask();
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
                string description = "Label Clash";
                lock (_updateLock) {
                    foreach (KeyValuePair<string, IList<uint>> entry in _defAt) {
                        if (entry.Value.Count > 1) {
                            foreach (uint id in entry.Value) {
                                result.Add(id, description);
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
                string description = "Undefined Label";
                lock (_updateLock) {
                    foreach (KeyValuePair<string, IList<uint>> entry in _usedAt) {
                        string label = entry.Key;
                        if (!this._defAt.ContainsKey(label)) {
                            foreach (uint id in entry.Value) {
                                result.Add(id, description);
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
                            lineContent = " :" + this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                        } else {
                            lineContent = "";
                        }
                        result.Add(entry.Key, AsmDudeToolsStatic.cleanup(string.Format("LINE {0} ({1}){2}", lineNumber, filename, lineContent)));
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
                _filenames.Clear();
                _filenames.Add(0, AsmDudeToolsStatic.GetFileName(this._sourceBuffer));

                this.addAll(this._sourceBuffer, 0);
            }

            double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
            if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took LabelGraph {0:F3} seconds to reset file {1}.", elapsedSec, AsmDudeToolsStatic.GetFileName(this._sourceBuffer)));
            }
            if (elapsedSec > AsmDudePackage.slowShutdownThresholdSec) {
                this.disable();
            }
        }

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


        private void addInfoToErrorTask() {
            //TODO this method should not be here
            string msg = "Is the Tools>Options>AsmDude options pane not visible? Disable and enable this plugin to make it visible again...";

            bool alreadyPresent = false;
            foreach (ErrorTask task in this._errorListProvider.Tasks) {
                if (task.Text.Equals(msg)) {
                    alreadyPresent = true;
                    break;
                }
            }
            if (!alreadyPresent) {
                ErrorTask errorTask = new ErrorTask();
                errorTask.SubcategoryIndex = (int)AsmErrorEnum.OTHER;
                errorTask.Text = msg;
                errorTask.ErrorCategory = TaskErrorCategory.Message;
                this._errorListProvider.Tasks.Add(errorTask);

                this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                this._errorListProvider.Refresh();
            }
        }

        private void disable() {
            string filename = AsmDudeToolsStatic.GetFileName(this._sourceBuffer);
            string msg = string.Format("Performance of LabelGraph is horrible: disabling label analysis for {0}.", filename);
            AsmDudeToolsStatic.Output(string.Format("WARNING: " + msg));

            this._enabled = false;
            lock (this._updateLock) {
                this._defAt.Clear();
                this._hasDef.Clear();
                this._usedAt.Clear();
                this._hasLabel.Clear();
            }

            #region Add Error Task

            ErrorTask errorTask = new ErrorTask();
            errorTask.SubcategoryIndex = (int)AsmErrorEnum.OTHER;
            errorTask.Text = msg;
            errorTask.ErrorCategory = TaskErrorCategory.Message;
            errorTask.Document = filename;
            errorTask.Navigate += AsmDudeToolsStatic.errorTaskNavigateHandler;
            this._errorListProvider.Tasks.Add(errorTask);
            this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            this._errorListProvider.Refresh();

            #endregion
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
                lock (_updateLock) {
                    // experimental faster method, but it still has subtle bugs
                    switch (e.Changes.Count) {
                        case 0: return;
                        case 1:
                            ITextChange textChange = e.Changes[0];
                            ITextBuffer buffer = this._sourceBuffer;
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
                    default: break;
                }
            }
        }

        private void handleInclude(string includeFilename) {
            string parentFilename = AsmDudeToolsStatic.GetFileName(this._sourceBuffer);
            string filePath = Path.GetDirectoryName(parentFilename) + Path.DirectorySeparatorChar + includeFilename.Substring(1, includeFilename.Length-2);

            if (File.Exists(filePath)) {
                AsmDudeToolsStatic.Output("INFO: LabelGraph:handleInclude: including file " + filePath);
            } else {
                AsmDudeToolsStatic.Output("WARNING: LabelGraph:handleInclude: file " + filePath + " does not exist");
                return;
            }

            if (!this._filenames.Values.Contains(filePath)) {
                try {
                    bool characterSubstitutionsOccurred;
                    ITextDocument doc = this._docFactory.CreateAndLoadTextDocument(filePath, this._contentType, true, out characterSubstitutionsOccurred);
                    //AsmDudeToolsStatic.Output(doc.TextBuffer.CurrentSnapshot.GetText());

                    doc.FileActionOccurred += Doc_FileActionOccurred;
                    uint fileId = (uint)this._filenames.Count;
                    this._filenames.Add(fileId, filePath);
                    this.addAll(doc.TextBuffer, fileId);
                } catch (Exception e) {
                    AsmDudeToolsStatic.Output("WARNING: LabelGraph:handleInclude. Exception:" + e.Message);
                }
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // dispose managed state (managed objects).
                    this._sourceBuffer.Changed -= OnTextBufferChanged;
                    AsmDudeToolsStatic.Output(string.Format("INFO: LabelGraph:Dispose."));
                    //this._errorListProvider.Tasks.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LabelGraph() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

        #endregion Private Methods
    }
#   pragma warning restore CS0162
}
