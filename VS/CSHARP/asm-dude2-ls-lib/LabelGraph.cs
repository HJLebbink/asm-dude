// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude2LS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.IO;
    using Amib.Threading;
    //using AsmDude.SyntaxHighlighting;
    using AsmTools;
    //using Microsoft.VisualStudio.Shell;
    //using Microsoft.VisualStudio.Text;
    //using Microsoft.VisualStudio.Text.Tagging;
    //using Microsoft.VisualStudio.Utilities;

    public sealed class LabelGraph
    {
        #region Fields

        /// <summary>
        /// immutable empty set to prevent creating one every time you need one
        /// </summary>
        //private static readonly SortedSet<uint> emptySet = new SortedSet<uint>();

        //private readonly ITextBuffer buffer_;
        //private readonly IBufferTagAggregatorFactoryService aggregatorFactory_;

        //private readonly ITextDocumentFactoryService docFactory_;
        //private readonly IContentType contentType_;

        private readonly TraceSource traceSource;
        private readonly AsmLanguageServerOptions options;

        private readonly string[] lines;
        private readonly string thisFilename_;
        private readonly Dictionary<uint, string> filenames_;

        /// <summary>
        /// Include_Filename = the file that is supposed to be included
        /// Path = path at which the include_filename is supposed to be found
        /// Source_Filename = full path and name of the source file in which the include is defined
        /// LineNumber = the lineNumber at which the include is defined
        /// </summary>
        private readonly IList<(string include_filename, string path, string source_filename, int lineNumber)> undefined_includes_;

        //private readonly BidirectionalGraph<uint, TaggedEdge<uint, (string LabelSource, string LabelTarget)>> graph_; TODO consider using graph

        private readonly ConcurrentDictionary<string, IList<uint>> usedAt_;
        private readonly ConcurrentDictionary<string, IList<uint>> defAt_;
        private readonly ConcurrentDictionary<string, IList<uint>> defAt_PROTO_;
        private readonly HashSet<uint> hasLabel_;
        private readonly HashSet<uint> hasDef_;

        public bool Enabled { get; private set; }

        //public ErrorListProvider Error_List_Provider { get; private set; }

        private readonly Delay delay_;
        private bool bussy_ = false;
        private IWorkItemResult thread_Result_;
        private readonly object updateLock_ = new object();
        #endregion Private Fields

        #region Constructor
        public LabelGraph(
                //ITextBuffer buffer,
                //IBufferTagAggregatorFactoryService aggregatorFactory,
                //ErrorListProvider errorListProvider,
                //ITextDocumentFactoryService docFactory,
                //IContentType contentType,
                string[] lines,
                string filename,
                TraceSource traceSource,
                AsmLanguageServerOptions options)
        {
            this.lines = lines;
            this.thisFilename_ = filename;
            this.traceSource = traceSource;
            this.options = options;

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:constructor: creating a label graph for {0}", AsmDudeToolsStatic.GetFileName(buffer)));
            //this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            //this.aggregatorFactory_ = aggregatorFactory ?? throw new ArgumentNullException(nameof(aggregatorFactory));
            //this.Error_List_Provider = errorListProvider ?? throw new ArgumentNullException(nameof(errorListProvider));
            //this.docFactory_ = docFactory ?? throw new ArgumentNullException(nameof(docFactory));
            //this.contentType_ = contentType;

            this.filenames_ = new Dictionary<uint, string>();

            //this._graph = new BidirectionalGraph<uint, TaggedEdge<uint, (string LabelSource, string LabelTarget)>>(false);
            this.usedAt_ = new ConcurrentDictionary<string, IList<uint>>();
            this.defAt_ = new ConcurrentDictionary<string, IList<uint>>();
            this.defAt_PROTO_ = new ConcurrentDictionary<string, IList<uint>>();
            this.hasLabel_ = new HashSet<uint>();
            this.hasDef_ = new HashSet<uint>();
            this.undefined_includes_ = new List<(string include_filename, string path, string source_filename, int lineNumber)>();


            this.delay_ = new Delay(LanguageServer.MsSleepBeforeAsyncExecution, 100, AsmThreadPool.Instance.threadPool_);

            this.Enabled = this.options.IntelliSense_Label_Analysis_On;


            LogInfo("LabelGraph: constructor");

            //if (buffer.CurrentSnapshot.LineCount >= AsmDudeToolsStatic.MaxFileLines)
            //{
            //    this.Enabled = false;
            //    logWarning(string.Format($"{this.ToString()}:LabelGraph; file {AsmDudeToolsStatic.GetFilename(buffer)} contains {buffer.CurrentSnapshot.LineCount} lines which is more than maxLines {AsmDudeToolsStatic.MaxFileLines}; switching off label analysis"));
            //}

            if (false) {
#pragma warning disable CS0162 // Unreachable code detected
                if (this.Enabled)
                {
                    this.delay_.Done_Event += (o, i) =>
                    {
                        if (this.bussy_)
                        {
                            this.delay_.Reset();
                        }
                        else
                        {
                            if ((this.thread_Result_ != null) && !this.thread_Result_.IsCompleted && !this.thread_Result_.IsCanceled)
                            {
                                this.thread_Result_.Cancel();
                            }
                            this.thread_Result_ = AsmThreadPool.Instance.threadPool_.QueueWorkItem(this.Reset_Private);
                        }
                    };
                    this.Reset();
                    //this.buffer_.ChangedLowPriority += this.Buffer_Changed;
                }
#pragma warning restore CS0162 // Unreachable code detected
            }
            this.Add_All(lines, 0);
        }
        #endregion

        private void LogInfo(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, msg);
        }
        private void LogWarning(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Warning, 0, msg);
        }
        private void LogError(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Error, 0, msg);
        }

        #region Public Methods

        public void Reset()
        {
            this.delay_.Reset();
        }

        public static int Get_Linenumber(uint id)
        {
            return (int)(id & 0x00FFFFFF);
        }

        public static uint Get_File_Id(uint id)
        {
            return id >> 24;
        }

        public string Get_Filename(uint id)
        {
            uint fileId = Get_File_Id(id);
            if (this.filenames_.TryGetValue(fileId, out string filename))
            {
                return filename;
            }
            else
            {
                LogWarning("LabelGraph:Get_Filename: no filename for id=" + id + " (fileId " + fileId + "; line " + Get_Linenumber(id) + ")");
                return string.Empty;
            }
        }

        public static uint Make_Id(int lineNumber, uint fileId)
        {
            return (fileId << 24) | (uint)lineNumber;
        }

        public static bool Is_From_Main_File(uint id)
        {
            return id <= 0xFFFFFF;
        }

        public IEnumerable<(uint key, string value)> Label_Clashes
        {
            get
            {
                foreach (KeyValuePair<string, IList<uint>> entry in this.defAt_)
                {
                    if (entry.Value.Count > 1)
                    {
                        string label = entry.Key;
                        foreach (uint id in entry.Value)
                        {
                            yield return (id, label);
                        }
                    }
                }
            }
        }

        public IEnumerable<(uint key, string value)> Undefined_Labels
        {
            get
            {
                AssemblerEnum usedAssember = this.options.Used_Assembler;
                SortedDictionary<uint, string> result = new SortedDictionary<uint, string>();
                lock (this.updateLock_)
                {
                    foreach (KeyValuePair<string, IList<uint>> entry in this.usedAt_)
                    {
                        string full_Qualified_Label = entry.Key;
                        if (this.defAt_.ContainsKey(full_Qualified_Label))
                        {
                            continue;
                        }

                        string regular_Label = AsmDudeToolsStatic.Retrieve_Regular_Label(full_Qualified_Label, usedAssember);
                        if (this.defAt_.ContainsKey(regular_Label))
                        {
                            continue;
                        }

                        if (this.defAt_PROTO_.ContainsKey(regular_Label))
                        {
                            continue;
                        }

                        //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Undefined_Labels: label=\"" + full_Qualified_Label + "\" is not defined.");

                        foreach (uint used_at_id in entry.Value)
                        {
                            if (result.ContainsKey(used_at_id))
                            { // this should not happen: somehow the (file-line) used_at_id has multiple occurances on the same line?!
                                LogWarning("LabelGraph:Get_Undefined_Labels: id=" + used_at_id + " (" + this.Get_Filename(used_at_id) + "; line " + Get_Linenumber(used_at_id) + ") with label \"" + full_Qualified_Label + "\" already exists and has key \"" + result[used_at_id] + "\".");
                            }
                            else
                            {
                                result.Add(used_at_id, full_Qualified_Label);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<uint, string> v in result)
                {
                    yield return (v.Key, v.Value);
                }
            }
        }

        public SortedDictionary<string, string> Label_Descriptions
        {
            get
            {
                SortedDictionary<string, string> result = new SortedDictionary<string, string>();
                lock (this.updateLock_)
                {
                    foreach (KeyValuePair<string, IList<uint>> entry in this.defAt_)
                    {
                        uint id = entry.Value[0];
                        int lineNumber = Get_Linenumber(id);
                        string filename = Path.GetFileName(this.Get_Filename(id));
                        string lineContent;
                        if (Is_From_Main_File(id))
                        {
                            lineContent = " :TODO";// + this.buffer_.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                        }
                        else
                        {
                            lineContent = string.Empty;
                        }
                        result.Add(entry.Key, AsmDudeToolsStatic.Cleanup(string.Format(AsmDudeToolsStatic.CultureUI, "LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                    }
                }
                return result;
            }
        }

        public bool Has_Label(string label)
        {
            return this.defAt_.ContainsKey(label) || this.defAt_PROTO_.ContainsKey(label);
        }

        public bool Has_Label_Clash(string label)
        {
            if (this.defAt_.TryGetValue(label, out IList<uint> list))
            {
                return list.Count > 1;
            }
            return false;
        }

        public SortedSet<uint> Get_Label_Def_Linenumbers(string label)
        {
            SortedSet<uint> results = new SortedSet<uint>();
            {
                if (this.defAt_.TryGetValue(label, out IList<uint> list))
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Label_Def_Linenumbers: Regular label definitions. label=" + label + ": found "+list.Count +" definitions.");
                    results.UnionWith(list);
                }
            }
            {
                if (this.defAt_PROTO_.TryGetValue(label, out IList<uint> list))
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Label_Def_Linenumbers: PROTO label defintions. label=" + label + ": found "+list.Count +" definitions.");
                    results.UnionWith(list);
                }
            }
            return results;
        }

        public SortedSet<uint> Label_Used_At_Info(string full_Qualified_Label, string label)
        {
            Contract.Requires(full_Qualified_Label != null);
            Contract.Requires(label != null);

            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Label_Used_At_Info: full_Qualified_Label=" + full_Qualified_Label + "; label=" + label);
            SortedSet<uint> results = new SortedSet<uint>();
            {
                if (this.usedAt_.TryGetValue(full_Qualified_Label, out IList<uint> lines))
                {
                    results.UnionWith(lines);
                }
            }
            {
                if (this.usedAt_.TryGetValue(label, out IList<uint> lines))
                {
                    results.UnionWith(lines);
                }
            }
            if (full_Qualified_Label.Equals(label, StringComparison.Ordinal))
            {
                AssemblerEnum usedAssember = this.options.Used_Assembler;
                foreach (KeyValuePair<string, IList<uint>> entry in this.usedAt_)
                {
                    string regular_Label = AsmDudeToolsStatic.Retrieve_Regular_Label(entry.Key, usedAssember);
                    if (label.Equals(regular_Label, StringComparison.Ordinal))
                    {
                        results.UnionWith(entry.Value);
                    }
                }
            }
            return results;
        }

        private void Reset_Private()
        {
            if (!this.Enabled)
            {
                return;
            }

            lock (this.updateLock_)
            {
                DateTime time1 = DateTime.Now;
                this.bussy_ = true;

                this.usedAt_.Clear();
                this.defAt_.Clear();
                this.defAt_PROTO_.Clear();
                this.hasLabel_.Clear();
                this.hasDef_.Clear();
                this.filenames_.Clear();
                this.filenames_.Add(0, this.thisFilename_);//AsmDudeToolsStatic.GetFilename(this.buffer_));
                this.undefined_includes_.Clear();

                // const uint fileId = 0; // use fileId=0 for the main file (and use numbers higher than 0 for included files)
                //TODO this.Add_All(this.buffer_, fileId);

                //AsmDudeToolsStatic.Print_Speed_Warning(time1, "LabelGraph");
                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > LanguageServer.SlowShutdownThresholdSec)
                {
#if DEBUG
                    LogWarning("LabelGraph: Reset: disabled label analysis had I been in Release mode");
#                   else
                    this.Disable();
#                   endif
                }
                this.bussy_ = false;
            }
            this.Reset_Done_Event?.Invoke(this, new EventArgs());
        }

        public event EventHandler<EventArgs> Reset_Done_Event;

        public IEnumerable<(string include_Filename, string path, string source_Filename, int lineNumber)> Undefined_Includes { get { return this.undefined_includes_; } }

        #endregion Public Methods

        #region Private Methods

        private void Disable()
        {
            string msg = $"Performance of LabelGraph is horrible: disabling label analysis for {this.thisFilename_}.";
            LogWarning(msg);

            this.Enabled = false;
            lock (this.updateLock_)
            {
                //this.buffer_.ChangedLowPriority -= this.Buffer_Changed;
                this.defAt_.Clear();
                this.defAt_PROTO_.Clear();
                this.hasDef_.Clear();
                this.usedAt_.Clear();
                this.hasLabel_.Clear();
                this.undefined_includes_.Clear();
            }
            //AsmDudeToolsStatic.Disable_Message(msg, this.thisFilename_, this.Error_List_Provider);
        }

/*
        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (!this.Enabled)
            {
                return;
            }

            if (true)
            {
                this.Reset();
            }
            else
            {
                // not used, because it does not work correctly
                lock (this.updateLock_)
                {
                    // experimental faster method, but it still has subtle bugs
                    switch (e.Changes.Count)
                    {
                        case 0: return;
                        case 1:
                            ITextChange textChange = e.Changes[0];
                            ITextBuffer buffer = this.buffer_;

                            ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, this.aggregatorFactory_);

                            switch (textChange.LineCountDelta)
                            {
                                case 0:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        this.Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case 1:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        this.Shift_Linenumber(lineNumber + 1, 1);
                                        this.Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case -1:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        this.Shift_Linenumber(lineNumber + 1, -1);
                                        this.Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                        this.Update_Linenumber(buffer, aggregator, lineNumber - 1, (uint)lineNumber);
                                    }
                                    break;
                                default:
                                    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:OnTextBufferChanged: lineDelta={0}", textChange.LineCountDelta));
                                    this.Reset();
                                    break;
                            }
                            break;
                        default:
                            this.Reset();
                            break;
                    }
                }
            }
        }
*/

        private void Add_All(string[] lines, uint fileId)
        {
            //ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, this.aggregatorFactory_);
            lock (this.updateLock_)
            {
                if (fileId == 0)
                {
                    for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
                    {
                        this.Add_Linenumber(lines, lineNumber, (uint)lineNumber);
                    }
                }
                else
                {
                    for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
                    {
                        this.Add_Linenumber(lines, lineNumber, Make_Id(lineNumber, fileId));
                    }
                }
            }
        }

        //private void Update_Linenumber(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator, int lineNumber, uint id)
        //{
            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Update_Linenumber: line "+ lineNumber);
            //this.Add_Linenumber(buffer, aggregator, lineNumber, id);
            //this.Remove_Linenumber(lineNumber, id);
        //}

        private void Add_Linenumber(string[] lines, int lineNumber, uint id)
        {
            AssemblerEnum usedAssember = this.options.Used_Assembler;

            (string label, Mnemonic mnemonic, string[] args, string remark) = AsmSourceTools.ParseLine(lines[lineNumber]);

            if (label.Length > 0)
            {
                string extra_Tag_Info = null; //TODO asmTokenTag.Tag.Misc;

                if ((extra_Tag_Info != null))// TODO && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
                {
                    LogInfo("LabelGraph:Add_Linenumber: found PROTO labelDef \"" + label + "\" at line " + lineNumber);
                    Add_To_Dictionary(label, id, this.defAt_PROTO_);
                    this.hasDef_.Add(id);
                }
                else
                {
                    
                    string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(extra_Tag_Info, label, usedAssember);
                    LogInfo("LabelGraph:Add_Linenumber: found labelDef \"" + label + "\" at line " + lineNumber + "; full_Qualified_Label = \"" + full_Qualified_Label + "\".");
                    Add_To_Dictionary(full_Qualified_Label, id, this.defAt_);
                    this.hasDef_.Add(id);
                }
            }
            if (AsmSourceTools.IsJump(mnemonic))
            {
                string labelStr = "";
                if (args.Length > 0)
                {
                    labelStr = args[0];
                }
                string prefix = null; // TODO asmTokenTag.Tag.Misc 
                string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(prefix, labelStr, usedAssember);
                Add_To_Dictionary(full_Qualified_Label, id, this.usedAt_);
                LogInfo("LabelGraph:Add_Linenumber: used label \"" + label + "\" at line " + lineNumber);
                this.hasLabel_.Add(id);
            }

            bool hasIncludes = false; //TODO
            if (hasIncludes)
            {
                //string directive_upcase = Get_Text(buffer, asmTokenTag).ToUpperInvariant();
                //switch (directive_upcase)
                //{
                //    case "%INCLUDE":
                //    case "INCLUDE":
                //        {
                //            if (enumerator.MoveNext()) // check whether a word exists after the include keyword
                //            {
                //                string currentFilename = this.Get_Filename(id);
                //                string includeFilename = Get_Text(buffer, enumerator.Current);
                //                this.Handle_Include(includeFilename, lineNumber, currentFilename);
                //            }
                //            break;
                //        }
                //    default:
                //        {
                //            break;
                //        }
                //}
            }
        }

        private static void Add_To_Dictionary(string key, uint id, IDictionary<string, IList<uint>> dict)
        {
            if ((key == null) || (key.Length == 0))
            {
                return;
            }
            if (dict.TryGetValue(key, out IList<uint> list))
            {
                list.Add(id);
            }
            else
            {
                dict.Add(key, new List<uint> { id });
            }
        }

        //private void Handle_Include(string includeFilename, int lineNumber, string currentFilename)
        //{
        //    try
        //    {
        //        if (includeFilename.Length < 1)
        //        {
        //            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: file with name \"" + includeFilename + "\" is too short.");
        //            return;
        //        }
        //        if (includeFilename.Length > 2)
        //        {
        //            if (includeFilename.StartsWith("[", StringComparison.Ordinal) && includeFilename.EndsWith("]", StringComparison.Ordinal))
        //            {
        //                includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
        //            }
        //            else if (includeFilename.StartsWith("\"", StringComparison.Ordinal) && includeFilename.EndsWith("\"", StringComparison.Ordinal))
        //            {
        //                includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
        //            }
        //        }
        //        string filePath = Path.GetDirectoryName(this.thisFilename_) + Path.DirectorySeparatorChar + includeFilename;

        //        if (!File.Exists(filePath))
        //        {
        //            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: file " + filePath + " does not exist");
        //            this.undefined_includes_.Add((include_filename: includeFilename, path: filePath, source_filename: currentFilename, lineNumber: lineNumber));
        //        }
        //        else
        //        {
        //            if (this.filenames_.Values.Contains(filePath))
        //            {
        //                //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: including file " + filePath + " has already been included");
        //            }
        //            else
        //            {
        //                //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: including file " + filePath);

        //                ITextDocument doc = this.docFactory_.CreateAndLoadTextDocument(filePath, this.contentType_, true, out bool characterSubstitutionsOccurred);
        //                doc.FileActionOccurred += this.Doc_File_Action_Occurred;
        //                uint fileId = (uint)this.filenames_.Count;
        //                this.filenames_.Add(fileId, filePath);
        //                this.Add_All(doc.TextBuffer, fileId);
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        LogWarning("LabelGraph:Handle_Include. Exception:" + e.Message);
        //    }
        //}

        //private void Doc_File_Action_Occurred(object sender, TextDocumentFileActionEventArgs e)
        //{
            //ITextDocument doc = sender as ITextDocument;
            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Doc_File_Action_Occurred: " + doc.FilePath + ":" + e.FileActionType);
        //}

        private void Remove_Linenumber(int lineNumber, uint id)
        {
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

        //private static string Get_Text(ITextBuffer buffer, IMappingTagSpan<AsmTokenTag> asmTokenSpan)
        //{
        //    return asmTokenSpan.Span.GetSpans(buffer)[0].GetText();
        //}

        private void Shift_Linenumber(int lineNumber, int lineCountDelta)
        {
            if (lineCountDelta > 0)
            {
                /*
                AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:shiftLineNumber: starting from line {0} everything is shifted +{1}", lineNumber, lineCountDelta));

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
            }
            else
            {
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "LabelGraph:shiftLineNumber: starting from line {0} everything is shifted {1}", lineNumber, lineCountDelta));
            }
        }

        #endregion Private Methods
    }
}
