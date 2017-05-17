// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AsmTools;

namespace AsmDude.Tools
{
    public sealed class LabelGraph : ILabelGraph
    {
        #region Fields
        /// <summary>
        /// immutable empty set to prevent creating one every time you need one
        /// </summary>
        //private static readonly SortedSet<uint> emptySet = new SortedSet<uint>();

        private readonly ITextBuffer _buffer;
        private readonly IBufferTagAggregatorFactoryService _aggregatorFactory;

        private readonly ITextDocumentFactoryService _docFactory;
        private readonly IContentType _contentType;

        private readonly string _thisFilename;
        private readonly IDictionary<uint, string> _filenames;
        /// <summary>
        /// Include_Filename = the file that is supposed to be included
        /// Path = path at which the include_filename is supposed to be found
        /// Source_Filename = full path and name of the source file in which the include is defined
        /// LineNumber = the lineNumber at which the include is defined
        /// </summary>
        private readonly IList<(string Include_Filename, string Path, string Source_Filename, int LineNumber)> _undefined_includes;
        private readonly IDictionary<string, IList<uint>> _usedAt;
        private readonly IDictionary<string, IList<uint>> _defAt;
        private readonly IDictionary<string, IList<uint>> _defAt_PROTO;
        private readonly ISet<uint> _hasLabel;
        private readonly ISet<uint> _hasDef;

        public bool Is_Enabled { get; private set; }
        public ErrorListProvider Error_List_Provider { get; private set; }

        private object _updateLock = new object();

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
                IContentType contentType)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:constructor: creating a label graph for {0}", AsmDudeToolsStatic.GetFileName(buffer)));
            this._buffer = buffer;
            this._aggregatorFactory = aggregatorFactory;
            this.Error_List_Provider = errorListProvider;
            this._docFactory = docFactory;
            this._contentType = contentType;

            this._filenames = new Dictionary<uint, string>();
            this._usedAt = new Dictionary<string, IList<uint>>();
            this._defAt = new Dictionary<string, IList<uint>>();
            this._defAt_PROTO = new Dictionary<string, IList<uint>>();
            this._hasLabel = new HashSet<uint>();
            this._hasDef = new HashSet<uint>();
            this._undefined_includes = new List<(string Include_Filename, string Path, string Source_Filename, int LineNumber)>();

            this._thisFilename = AsmDudeToolsStatic.GetFileName(this._buffer);
            this.Is_Enabled = true;
            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this._buffer.ChangedLowPriority += this.Buffer_Changed;
            this.Reset_Delayed();
        }

        public int Get_Linenumber(uint id)
        {
            return (int)(id & 0x00FFFFFF);
        }

        public uint Get_File_Id(uint id)
        {
            return (id >> 24);
        }
        public string Get_Filename(uint id)
        {
            uint fileId = Get_File_Id(id);
            if (this._filenames.TryGetValue(fileId, out string filename))
            {
                return filename;
            } else
            {
                AsmDudeToolsStatic.Output_WARNING("LabelGraph:Get_Filename: no filename for id=" + id + " (fileId " + fileId + "; line " + Get_Linenumber(id) + ")");
                return "";
            }
        }
        public uint Make_Id(int lineNumber, uint fileId)
        {
            return (fileId << 24) | (uint)lineNumber;
        }
        public bool Is_From_Main_File(uint id)
        {
            return id <= 0xFFFFFF;
        }

        public SortedDictionary<uint, string> Label_Clashes //TODO consider returning an IEnumerable
        {
            get
            {
                SortedDictionary<uint, string> result = new SortedDictionary<uint, string>();
                lock (this._updateLock)
                {
                    foreach (KeyValuePair<string, IList<uint>> entry in this._defAt)
                    {
                        if (entry.Value.Count > 1)
                        {
                            string label = entry.Key;
                            foreach (uint id in entry.Value)
                            {
                                result.Add(id, label);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public SortedDictionary<uint, string> Undefined_Labels //TODO consider returning an IEnumerable
        {
            get
            {
                AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;
                SortedDictionary<uint, string> result = new SortedDictionary<uint, string>();
                lock (this._updateLock)
                {
                    foreach (KeyValuePair<string, IList<uint>> entry in this._usedAt)
                    {
                        string full_Qualified_Label = entry.Key;
                        if (this._defAt.ContainsKey(full_Qualified_Label)) continue;

                        string regular_Label = AsmDudeToolsStatic.Retrieve_Regular_Label(full_Qualified_Label, usedAssember);
                        if (this._defAt.ContainsKey(regular_Label)) continue;
                        if (this._defAt_PROTO.ContainsKey(regular_Label)) continue;

                        //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Undefined_Labels: label=\"" + full_Qualified_Label + "\" is not defined.");

                        foreach (uint used_at_id in entry.Value)
                        {
                            if (result.ContainsKey(used_at_id))
                            {   // this should not happen: somehow the (file-line) used_at_id has multiple occurances on the same line?!
                                AsmDudeToolsStatic.Output_WARNING("LabelGraph:Get_Undefined_Labels: id=" + used_at_id + " (" + Get_Filename(used_at_id) + "; line " + Get_Linenumber(used_at_id) + ") with label \"" + full_Qualified_Label + "\" already exists and has key \"" + result[used_at_id] + "\".");
                            }
                            else
                            {
                                result.Add(used_at_id, full_Qualified_Label);
                            }
                        }
                    }
                }
                return result;
            }
        }

        public SortedDictionary<string, string> Label_Descriptions
        {
            get
            {
                SortedDictionary<string, string> result = new SortedDictionary<string, string>();
                lock (this._updateLock)
                {
                    foreach (KeyValuePair<string, IList<uint>> entry in this._defAt)  
                    {
                        uint id = entry.Value[0];
                        int lineNumber = Get_Linenumber(id);
                        string filename = Path.GetFileName(Get_Filename(id));
                        string lineContent;
                        if (Is_From_Main_File(id))
                        {
                            lineContent = " :" + this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                        } else
                        {
                            lineContent = "";
                        }
                        result.Add(entry.Key, AsmDudeToolsStatic.Cleanup(string.Format("LINE {0} ({1}){2}", (lineNumber + 1), filename, lineContent)));
                    }
                }
                return result;
            }
        }

        public bool Has_Label(string label)
        {
            return this._defAt.ContainsKey(label) || this._defAt_PROTO.ContainsKey(label);
        }

        public bool Has_Label_Clash(string label)
        {
            if (this._defAt.TryGetValue(label, out var list))
            {
                return (list.Count > 1);
            }
            return false;
        }

        public SortedSet<uint> Get_Label_Def_Linenumbers(string label)
        {
            SortedSet<uint> results = new SortedSet<uint>();
            {
                if (this._defAt.TryGetValue(label, out var list))
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Label_Def_Linenumbers: Regular label definitions. label=" + label + ": found "+list.Count +" definitions.");
                    results.UnionWith(list);
                }
            }
            {
                if (this._defAt_PROTO.TryGetValue(label, out var list))
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Get_Label_Def_Linenumbers: PROTO label defintions. label=" + label + ": found "+list.Count +" definitions.");
                    results.UnionWith(list);
                }
            }
            return results;
        }

        public SortedSet<uint> Label_Used_At_Info(string full_Qualified_Label, string label)
        {
            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Label_Used_At_Info: full_Qualified_Label=" + full_Qualified_Label + "; label=" + label);
            SortedSet<uint> results = new SortedSet<uint>();
            {
                if (this._usedAt.TryGetValue(full_Qualified_Label, out var lines))
                {
                    results.UnionWith(lines);
                }
            }
            {
                if (this._usedAt.TryGetValue(label, out var lines))
                {
                    results.UnionWith(lines);
                }
            }
            if (full_Qualified_Label.Equals(label))
            {
                AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;
                foreach (KeyValuePair<string, IList<uint>> entry in this._usedAt)
                {
                    string regular_Label = AsmDudeToolsStatic.Retrieve_Regular_Label(entry.Key, usedAssember);
                    if (label.Equals(regular_Label))
                    {
                        results.UnionWith(entry.Value);
                    }
                }
            }
            return results;
        }

        public void Reset_Delayed()
        {
            if (this._waiting)
            {
                AsmDudeToolsStatic.Output_INFO("LabelGraph:Reset_Delayed: already waiting for execution. Skipping this call.");
                return;
            }
            if (this._busy)
            {
                AsmDudeToolsStatic.Output_INFO("LabelGraph:Reset_Delayed: busy; scheduling this call.");
                this._scheduled = true;
            } else
            {
                AsmDudeToolsStatic.Output_INFO("LabelGraph:Reset_Delayed: going to execute this call.");
                AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Reset);
            }
        }

        private void Reset()
        {
            if (!this.Is_Enabled) return;

            this._waiting = true;
            Thread.Sleep(AsmDudePackage.msSleepBeforeAsyncExecution);
            this._busy = true;
            this._waiting = false;

            #region Payload
            lock (this._updateLock)
            {
                DateTime time1 = DateTime.Now;

                this._usedAt.Clear();
                this._defAt.Clear();
                this._defAt_PROTO.Clear();
                this._hasLabel.Clear();
                this._hasDef.Clear();
                this._filenames.Clear();
                this._filenames.Add(0, AsmDudeToolsStatic.GetFileName(this._buffer));
                this._undefined_includes.Clear();

                const uint fileId = 0; // use fileId=0 for the main file (and use numbers higher than 0 for included files)
                Add_All(this._buffer, fileId);

                AsmDudeToolsStatic.Print_Speed_Warning(time1, "LabelGraph");
                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowShutdownThresholdSec)
                {
#                   if DEBUG
                    AsmDudeToolsStatic.Output_WARNING("LabelGraph: Reset: disabled label analysis had I been in Release mode");
#                   else
                    Disable();
#                   endif
                }
            }
            #endregion Payload

            On_Reset_Done_Event(new CustomEventArgs("Resetting LabelGraph is finished"));

            this._busy = false;
            if (this._scheduled)
            {
                this._scheduled = false;
                Reset_Delayed();
            }
        }

        public event EventHandler<CustomEventArgs> Reset_Done_Event;

        public IList<(string Include_Filename, string Path, string Source_Filename, int LineNumber)> Undefined_Includes { get { return this._undefined_includes; } }

        #endregion Public Methods

        #region Private Methods

        private void Disable()
        {
            string msg = string.Format("Performance of LabelGraph is horrible: disabling label analysis for {0}.", this._thisFilename);
            AsmDudeToolsStatic.Output_WARNING(msg);

            this.Is_Enabled = false;
            lock (this._updateLock)
            {
                this._buffer.ChangedLowPriority -= this.Buffer_Changed;
                this._defAt.Clear();
                this._defAt_PROTO.Clear();
                this._hasDef.Clear();
                this._usedAt.Clear();
                this._hasLabel.Clear();
                this._undefined_includes.Clear();
            }
            AsmDudeToolsStatic.Disable_Message(msg, this._thisFilename, this.Error_List_Provider);
        }

        private static int Get_Line_Number(IMappingTagSpan<AsmTokenTag> tag)
        {
            return AsmDudeToolsStatic.Get_LineNumber(tag.Span.GetSpans(tag.Span.AnchorBuffer)[0]);
        }

        private void On_Reset_Done_Event(CustomEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber un-subscribes
            // immediately after the null check and before the event is raised.
            EventHandler<CustomEventArgs> handler = this.Reset_Done_Event;

            // Event will be null if there are no subscribers
            if (handler != null)
            {
                // Format the string to send inside the CustomEventArgs parameter
                e.Message += String.Format(" at {0}", DateTime.Now.ToString());

                // Use the () operator to raise the event.
                handler(this, e);
            }
        }

        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:OnTextBufferChanged: number of changes={0}; first change: old={1}; new={2}", e.Changes.Count, e.Changes[0].OldText, e.Changes[0].NewText));
            if (!this.Is_Enabled) return;

            if (true)
            {
                Reset_Delayed();
            }
            else
            {
                // not used, because it does not work correctly
                lock (this._updateLock)
                {
                    // experimental faster method, but it still has subtle bugs
                    switch (e.Changes.Count)
                    {
                        case 0: return;
                        case 1:
                            ITextChange textChange = e.Changes[0];
                            ITextBuffer buffer = this._buffer;

                            ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, this._aggregatorFactory);

                            switch (textChange.LineCountDelta)
                            {
                                case 0:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case 1:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        //AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        Shift_Linenumber(lineNumber + 1, 1);
                                        Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                    }
                                    break;
                                case -1:
                                    {
                                        int lineNumber = e.Before.GetLineNumberFromPosition(textChange.OldPosition);
                                        //AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:OnTextBufferChanged: old={0}; new={1}; LineNumber={2}", textChange.OldText, textChange.NewText, lineNumber));
                                        Shift_Linenumber(lineNumber + 1, -1);
                                        Update_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                                        Update_Linenumber(buffer, aggregator, lineNumber - 1, (uint)lineNumber);
                                    }
                                    break;
                                default:
                                    //AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:OnTextBufferChanged: lineDelta={0}", textChange.LineCountDelta));
                                    Reset_Delayed();
                                    break;
                            }
                            break;
                        default:
                            Reset_Delayed();
                            break;
                    }
                }
            }
        }

        private void Add_All(ITextBuffer buffer, uint fileId)
        {
            ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, this._aggregatorFactory);
            lock (this._updateLock)
            {
                if (fileId == 0)
                {
                    for (int lineNumber = 0; lineNumber < buffer.CurrentSnapshot.LineCount; ++lineNumber)
                    {
                        Add_Linenumber(buffer, aggregator, lineNumber, (uint)lineNumber);
                    }
                } else
                {
                    for (int lineNumber = 0; lineNumber < buffer.CurrentSnapshot.LineCount; ++lineNumber)
                    {
                        Add_Linenumber(buffer, aggregator, lineNumber, Make_Id(lineNumber, fileId));
                    }
                }
            }
        }

        private void Update_Linenumber(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator, int lineNumber, uint id)
        {
            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Update_Linenumber: line "+ lineNumber);
            Add_Linenumber(buffer, aggregator, lineNumber, id);
            Remove_Linenumber(lineNumber, id);
        }

        private void Add_Linenumber(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator, int lineNumber, uint id)
        {
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = aggregator.GetTags(buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
            var enumerator = tags.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var asmTokenTag = enumerator.Current;
                switch (asmTokenTag.Tag.Type)
                {
                    case AsmTokenType.LabelDef:
                    {
                        string label = Get_Text(buffer, asmTokenTag);
                        string extra_Tag_Info = asmTokenTag.Tag.Misc;

                        if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO))
                            {
                                //AsmDudeToolsStatic.Output_INFO("LabelGraph:Add_Linenumber: found PROTO labelDef \"" + label + "\" at line " + lineNumber);
                                this.Add_To_Dictionary(label, id, this._defAt_PROTO);
                                this._hasDef.Add(id);
                            }
                            else
                            {
                                string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(extra_Tag_Info, label, usedAssember);
                                //AsmDudeToolsStatic.Output_INFO("LabelGraph:Add_Linenumber: found labelDef \"" + label + "\" at line " + lineNumber + "; full_Qualified_Label = \"" + full_Qualified_Label + "\".");
                                this.Add_To_Dictionary(full_Qualified_Label, id, this._defAt);
                                this._hasDef.Add(id);
                            }
                            break;
                    }
                    case AsmTokenType.Label:
                    {
                        string labelStr = Get_Text(buffer, asmTokenTag);
                        string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(asmTokenTag.Tag.Misc, labelStr, usedAssember);

                        Add_To_Dictionary(full_Qualified_Label, id, this._usedAt);

                        //AsmDudeToolsStatic.Output_INFO("LabelGraph:Add_Linenumber: used label \"" + label + "\" at line " + lineNumber);
                        this._hasLabel.Add(id);
                        break;
                    }
                    case AsmTokenType.Directive:
                    {
                        string directiveStr = Get_Text(buffer, asmTokenTag).ToUpper();

                        switch (directiveStr)
                        {
                            case "%INCLUDE":
                            case "INCLUDE":
                            {
                                if (enumerator.MoveNext()) // check whether a word exists after the include keyword
                                {
                                    string currentFilename = Get_Filename(id);
                                    string includeFilename = Get_Text(buffer, enumerator.Current);
                                    Handle_Include(includeFilename, lineNumber, currentFilename);
                                }
                                break;
                            }
                            default:
                            {
                                break;
                            }
                        }
                        break;
                    }
                    default:
                    {
                            //AsmDudeToolsStatic.Output_INFO("LabelGraph:addLineNumber: found text \"" + getText(buffer, asmTokenSpan) + "\" at line " + lineNumber);
                            break;
                    }
                }
            }
        }

        private void Add_To_Dictionary(string key, uint id, IDictionary<string, IList<uint>> dict)
        {
            if ((key == null) || (key.Length == 0))
            {
                return;
            }
            if (dict.TryGetValue(key, out var list))
            {
                list.Add(id);
            } else
            {
                dict.Add(key, new List<uint> { id });
            }
        }
        
        private void Handle_Include(string includeFilename, int lineNumber, string currentFilename)
        {
            try
            {
                if (includeFilename.Length < 1)
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: file with name \"" + includeFilename + "\" is too short.");
                    return;
                }
                if (includeFilename.Length > 2)
                {
                    if (includeFilename.StartsWith("[") && includeFilename.EndsWith("]"))
                    {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    } else if (includeFilename.StartsWith("\"") && includeFilename.EndsWith("\""))
                    {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    }
                }
                string filePath = Path.GetDirectoryName(this._thisFilename) + Path.DirectorySeparatorChar + includeFilename;

                if (!File.Exists(filePath))
                {
                    //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: file " + filePath + " does not exist");
                    this._undefined_includes.Add((Include_Filename: includeFilename, Path: filePath, Source_Filename: currentFilename, LineNumber: lineNumber));
                }
                else
                {
                    if (this._filenames.Values.Contains(filePath))
                    {
                        //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: including file " + filePath + " has already been included");
                    }
                    else
                    {
                        //AsmDudeToolsStatic.Output_INFO("LabelGraph:Handle_Include: including file " + filePath);

                        ITextDocument doc = this._docFactory.CreateAndLoadTextDocument(filePath, this._contentType, true, out var characterSubstitutionsOccurred);
                        doc.FileActionOccurred += this.Doc_File_Action_Occurred;
                        uint fileId = (uint)this._filenames.Count;
                        this._filenames.Add(fileId, filePath);
                        this.Add_All(doc.TextBuffer, fileId);
                    }
                }
            } catch (Exception e)
            {
                AsmDudeToolsStatic.Output_WARNING("LabelGraph:Handle_Include. Exception:" + e.Message);
            }
        }

        private void Doc_File_Action_Occurred(Object sender, TextDocumentFileActionEventArgs e)
        {
            ITextDocument doc = sender as ITextDocument;
            //AsmDudeToolsStatic.Output_INFO("LabelGraph:Doc_File_Action_Occurred: " + doc.FilePath + ":" + e.FileActionType);
        }

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

        private string Get_Text(ITextBuffer buffer, IMappingTagSpan<AsmTokenTag> asmTokenSpan)
        {
            return asmTokenSpan.Span.GetSpans(buffer)[0].GetText();
        }

        private void Shift_Linenumber(int lineNumber, int lineCountDelta)
        {
            if (lineCountDelta > 0)
            {
                /*
                AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:shiftLineNumber: starting from line {0} everything is shifted +{1}", lineNumber, lineCountDelta));

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
                AsmDudeToolsStatic.Output_INFO(string.Format("LabelGraph:shiftLineNumber: starting from line {0} everything is shifted {1}", lineNumber, lineCountDelta));
            }
        }

        #endregion Private Methods
    }
}
