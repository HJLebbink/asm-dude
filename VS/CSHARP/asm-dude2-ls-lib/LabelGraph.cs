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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;

    using AsmTools;

    using Microsoft.VisualStudio.LanguageServer.Protocol;

    using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;


    public sealed class LabelGraph
    {
        #region Fields
        private readonly AsmLanguageServerOptions options;

        private readonly string[] lines;
        private readonly string thisFilename_;
        private readonly bool caseSensitiveLabel_;
        private readonly Dictionary<int, string> filenames_;

        /// <summary>
        /// Include_Filename = the file that is supposed to be included
        /// Path = path at which the include_filename is supposed to be found
        /// Source_Filename = full path and name of the source file in which the include is defined
        /// LineNumber = the lineNumber at which the include is defined
        /// </summary>
        private readonly List<(string include_filename, string path, string source_filename, int lineNumber)> undefined_includes_;

        private readonly Dictionary<string, List<KeywordID>> usedAt_;
        private readonly Dictionary<string, List<KeywordID>> defAt_;
        private readonly Dictionary<string, List<KeywordID>> defAt_PROTO_;
        private readonly HashSet<KeywordID> hasLabel_;
        private readonly HashSet<KeywordID> hasDef_;

        private readonly List<VSDiagnostic> current_diagnostics;

        public bool Enabled { get; private set; }

        #endregion Private Fields

        public LabelGraph(
                string[] lines,
                string filename,
                bool caseSensitiveLabel,
                AsmLanguageServerOptions options)
        {
            LanguageServer.LogInfo($"LabelGraph: constructor: creating a label graph for {filename}"); //NOTE first init traceSource!

            this.lines = lines;
            this.thisFilename_ = filename;
            this.caseSensitiveLabel_ = caseSensitiveLabel;
            this.options = options;

            this.filenames_ = new Dictionary<int, string>();
            this.usedAt_ = new Dictionary<string, List<KeywordID>>(); // if LabelGraph is case insensitive then string is UPPERCASE
            this.defAt_ = new Dictionary<string, List<KeywordID>>();// if LabelGraph is case insensitive then string is UPPERCASE
            this.defAt_PROTO_ = new Dictionary<string, List<KeywordID>>();// if LabelGraph is case insensitive then string is UPPERCASE
            this.hasLabel_ = new HashSet<KeywordID>();
            this.hasDef_ = new HashSet<KeywordID>();
            this.undefined_includes_ = new List<(string include_filename, string path, string source_filename, int lineNumber)>();
            this.Enabled = this.options.IntelliSense_Label_Analysis_On;
            this.current_diagnostics = new List<VSDiagnostic>();

            if (lines.Length >= options.MaxFileLines)
            {
                this.Enabled = false;
                LanguageServer.LogWarning($"{this}:LabelGraph; file {filename} contains {lines.Length} lines which is more than maxLines {options.MaxFileLines}; switching off label analysis");
            }

            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                this.Add_Linenumber(lines[lineNumber], lineNumber, 0);
            }
        }

        public void UpdateDiagnostics()
        {

            foreach ((string _, List<KeywordID> labelIDs) in this.defAt_)
            {
                if (labelIDs.Count > 1)
                {
                    foreach (KeywordID labelID in labelIDs)
                    {
                        try
                        {
                            VSTextDocumentIdentifier id = null;
                            int lineNumber = labelID.LineNumber;
                            Range range = new()
                            {
                                Start = new Position(lineNumber, labelID.Start_Pos),
                                End = new Position(lineNumber, labelID.End_Pos),
                            };

                            //TODO handle labels from other files
                            string lineStr = this.lines[lineNumber];
                            string labelStr = lineStr[labelID.Start_Pos..labelID.End_Pos];

                            this.current_diagnostics.Add(new VSDiagnostic()
                            {
                                Message = $"The label '{labelStr}' is a duplicate ({labelIDs.Count} definitions found).",
                                Severity = DiagnosticSeverity.Error,
                                Range = range,
                                //Code = "Error Code Here",
                                //CodeDescription = new CodeDescription
                                //{
                                //    Href = new Uri("https://www.microsoft.com")
                                //},
                                Projects = LanguageServer.GetVSDiagnosticProjectInformation(id),
                                //Identifier = $"{lineNumber},{offsetStart} {lineNumber},{offsetEnd}",
                                Tags = new DiagnosticTag[1] { (DiagnosticTag)AsmDiagnosticTag.IntellisenseError }
                            });
                        }
                        catch (Exception ex)
                        {
                            LanguageServer.LogError(ex.ToString());
                        }
                    }
                }
            }
            foreach (KeywordID labelID in this.Undefined_Labels)
            {
                try
                {
                    VSTextDocumentIdentifier id = null;
                    int lineNumber = labelID.LineNumber;
                    Range range = new()
                    {
                        Start = new Position(lineNumber, labelID.Start_Pos),
                        End = new Position(lineNumber, labelID.End_Pos),
                    };
                    //TODO handle labels from other files
                    string lineStr = this.lines[lineNumber];
                    string labelStr = lineStr[labelID.Start_Pos..labelID.End_Pos];
                    this.current_diagnostics.Add(new VSDiagnostic()
                    {
                        Message = $"No such label '{labelStr}'.",
                        Severity = DiagnosticSeverity.Error,
                        Range = range,
                        //Code = "Error Code Here",
                        //CodeDescription = new CodeDescription
                        //{
                        //    Href = new Uri("https://www.microsoft.com")
                        //},
                        Projects = LanguageServer.GetVSDiagnosticProjectInformation(id),
                        //Identifier = $"{lineNumber},{offsetStart} {lineNumber},{offsetEnd}",
                        Tags = new DiagnosticTag[1] { (DiagnosticTag)AsmDiagnosticTag.IntellisenseError }
                    });
                }
                catch (Exception ex)
                {
                    LanguageServer.LogError(ex.ToString());
                }
            }
        }

        public List<VSDiagnostic> Diagnostics
        {
            get
            {
                return this.current_diagnostics;
            }
        }

        #region Public Methods

        public string Get_Filename(KeywordID labelID)
        {
            if (this.filenames_.TryGetValue(labelID.File_Id, out string filename))
            {
                return filename;
            }
            else
            {
                LanguageServer.LogWarning("LabelGraph:Get_Filename: no filename for labelID=" + labelID + " (fileId " + labelID.File_Id + "; line " + labelID.LineNumber + ")");
                return string.Empty;
            }
        }

        private IEnumerable<KeywordID> Undefined_Labels
        {
            get
            {
                AssemblerEnum usedAssembler = this.options.Used_Assembler;
                foreach ((string full_Qualified_Label, List<KeywordID> labelIDs) in this.usedAt_)
                {
                    // NOTE: if LabelGraph is case insensitive then full_Qualified_Label is UPPERCASE
                    if (this.defAt_.ContainsKey(full_Qualified_Label))
                    {
                        continue;
                    }

                    string regular_Label = Tools.Retrieve_Regular_Label(full_Qualified_Label, usedAssembler);
                    if (this.defAt_.ContainsKey(regular_Label))
                    {
                        continue;
                    }

                    if (this.defAt_PROTO_.ContainsKey(regular_Label))
                    {
                        continue;
                    }

                    foreach (KeywordID labelID in labelIDs)
                    {
                        yield return labelID;
                    }
                }
            }
        }

        public SortedDictionary<string, string> Label_Descriptions
        {
            get
            {
                SortedDictionary<string, string> result = new();
                {
                    foreach (KeyValuePair<string, List<KeywordID>> entry in this.defAt_)
                    {
                        KeywordID id = entry.Value[0];
                        int lineNumber = id.LineNumber;
                        string filename = Path.GetFileName(this.Get_Filename(id));
                        string lineContent;
                        if (id.Is_From_Main_File)
                        {
                            lineContent = " :" + this.lines.ElementAtOrDefault(lineNumber);
                        }
                        else
                        {
                            lineContent = " :TODO";
                        }
                        result.Add(entry.Key, Tools.Cleanup($"LINE {lineNumber + 1} ({filename}){lineContent}"));
                    }
                }
                return result;
            }
        }

        public IEnumerable<(string include_Filename, string path, string source_Filename, int lineNumber)> Undefined_Includes { get { return this.undefined_includes_; } }

        #endregion Public Methods

        #region Private Methods

        private void Disable()
        {
            string msg = $"Performance of LabelGraph is horrible: disabling label analysis for {this.thisFilename_}.";
            LanguageServer.LogWarning(msg);

            this.Enabled = false;
            {
                this.defAt_.Clear();
                this.defAt_PROTO_.Clear();
                this.hasDef_.Clear();
                this.usedAt_.Clear();
                this.hasLabel_.Clear();
                this.undefined_includes_.Clear();
            }
            // Tools.Disable_Message(msg, this.thisFilename_, this.Error_List_Provider);
        }
 
        private void Add_Linenumber(string lineStr, int lineNumber, int fileID)
        {
            AssemblerEnum usedAssembler = this.options.Used_Assembler;

            (object _, string label, Mnemonic mnemonic, string[] args, string _) = AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);

            if (label.Length > 0)
            {
                int startPos = lineStr.IndexOf(label);
                KeywordID labelID = new(lineNumber, fileID, startPos, startPos + label.Length);

                string extra_Tag_Info = null; // TODO asmTokenTag.Tag.Misc;

                if ((extra_Tag_Info != null))// TODO && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
                {
                    LanguageServer.LogInfo("LabelGraph:Add_Linenumber: found PROTO labelDef \"" + label + "\" at line " + lineNumber);
                    Add_To_Dictionary(label, labelID, this.caseSensitiveLabel_, this.defAt_PROTO_);
                }
                else
                {
                    string full_Qualified_Label = Tools.Make_Full_Qualified_Label(extra_Tag_Info, label, usedAssembler);
                    LanguageServer.LogInfo("LabelGraph:Add_Linenumber: found labelDef \"" + label + "\" at line " + lineNumber + "; full_Qualified_Label = \"" + full_Qualified_Label + "\".");
                    Add_To_Dictionary(full_Qualified_Label, labelID, this.caseSensitiveLabel_, this.defAt_);
                }
                this.hasDef_.Add(labelID);
            }
            if (AsmSourceTools.IsJump(mnemonic))
            {
                if (args.Length > 0)
                {
                    string labelStr = args[0];
                    string prefix = null; // TODO asmTokenTag.Tag.Misc 
                    string full_Qualified_Label = Tools.Make_Full_Qualified_Label(prefix, labelStr, usedAssembler);

                    int startPos = lineStr.IndexOf(labelStr);
                    if (startPos < 0)
                    {
                        LanguageServer.LogError($"LabelGraph:Add_Linenumber: startPos {startPos}");
                    } 
                    else
                    {
                        KeywordID labelID = new(lineNumber, fileID, startPos, startPos + labelStr.Length);
                        Add_To_Dictionary(full_Qualified_Label, labelID, this.caseSensitiveLabel_, this.usedAt_);
                        LanguageServer.LogInfo("LabelGraph:Add_Linenumber: used label \"" + label + "\" at line " + lineNumber);
                        this.hasLabel_.Add(labelID);
                    }
                }
            }

            bool hasIncludes = false; //TODO
            if (hasIncludes)
            {
                if (args.Length > 1)
                {
                    string directive_uppercase = args[0].ToUpperInvariant();
                    switch (directive_uppercase)
                    {
                        case "%INCLUDE":
                        case "INCLUDE":
                            {
                                string includeFilename = args[1];
                                this.Handle_Include(includeFilename, lineNumber, this.thisFilename_);
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
        }

        private static void Add_To_Dictionary(string key, KeywordID id, bool caseSensitiveLabels, Dictionary<string, List<KeywordID>> dict)
        {
            if ((key == null) || (key.Length == 0))
            {
                return;
            }
            string key2 = (caseSensitiveLabels) ? key : key.ToUpper();

            if (dict.TryGetValue(key2, out List<KeywordID> list))
            {
                list.Add(id);
            }
            else
            {
                dict.Add(key2, new List<KeywordID> { id });
            }
        }

        private void Handle_Include(string includeFilename, int lineNumber, string currentFilename)
        {
            try
            {
                if (includeFilename.Length < 1)
                {
                    LanguageServer.LogInfo("LabelGraph:Handle_Include: file with name \"" + includeFilename + "\" is too short.");
                    return;
                }
                if (includeFilename.Length > 2)
                {
                    if (includeFilename.StartsWith("[", StringComparison.Ordinal) && includeFilename.EndsWith("]", StringComparison.Ordinal))
                    {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    }
                    else if (includeFilename.StartsWith("\"", StringComparison.Ordinal) && includeFilename.EndsWith("\"", StringComparison.Ordinal))
                    {
                        includeFilename = includeFilename.Substring(1, includeFilename.Length - 2);
                    }
                }
                string filePath = Path.GetDirectoryName(this.thisFilename_) + Path.DirectorySeparatorChar + includeFilename;

                if (!File.Exists(filePath))
                {
                    LanguageServer.LogInfo("LabelGraph:Handle_Include: file " + filePath + " does not exist");
                    this.undefined_includes_.Add((include_filename: includeFilename, path: filePath, source_filename: currentFilename, lineNumber: lineNumber));
                }
                else
                {
                    if (this.filenames_.Values.Contains(filePath))
                    {
                        LanguageServer.LogInfo("LabelGraph:Handle_Include: including file " + filePath + " has already been included");
                    }
                    else
                    {
                        LanguageServer.LogInfo("LabelGraph:Handle_Include: including file " + filePath);

                        //ITextDocument doc = this.docFactory_.CreateAndLoadTextDocument(filePath, this.contentType_, true, out bool characterSubstitutionsOccurred);
                        //doc.FileActionOccurred += this.Doc_File_Action_Occurred;
                        int fileId = this.filenames_.Count;
                        this.filenames_.Add(fileId, filePath);

                        //this.Add_All(doc.TextBuffer, fileId);
                    }
                }
            }
            catch (Exception e)
            {
                LanguageServer.LogWarning("LabelGraph:Handle_Include. Exception:" + e.Message);
            }
        }

        #endregion Private Methods
    }
}
