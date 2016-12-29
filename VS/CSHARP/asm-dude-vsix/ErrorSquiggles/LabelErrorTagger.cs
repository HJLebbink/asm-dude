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

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AsmDude.ErrorSquiggles
{
    internal sealed class LabelErrorTagger : ITagger<ErrorTag>
    {
        #region Private Fields

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly ILabelGraph _labelGraph;

        private object _updateLock = new object();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion Private Fields

        internal LabelErrorTagger(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> aggregator,
                ILabelGraph labelGraph)
        {

            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            this._sourceBuffer = buffer;
            this._aggregator = aggregator;
            this._errorListProvider = AsmDudeTools.Instance.Error_List_Provider;
            this._labelGraph = labelGraph;
            this._labelGraph.Reset_Done_Event += this.Handle_Label_Graph_Reset_Done_Event;
            this._labelGraph.Reset_Delayed();
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {  //there is no content in the buffer
                yield break;
            }
            if (!this._labelGraph.Is_Enabled)
            {
                yield break;
            }

            DateTime time1 = DateTime.Now;

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in this._aggregator.GetTags(spans))
            {
                SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(this._sourceBuffer)[0];
                //AsmDudeToolsStatic.Output(string.Format("INFO: ErrorTagger:GetTags: found keyword \"{0}\"", tagSpan.GetText()));

                switch (asmTokenTag.Tag.Type)
                {
                    case AsmTokenType.Label:
                    {
                        if (Settings.Default.IntelliSenseDecorateUndefinedLabels)
                        {
                            string label = tagSpan.GetText();
                            string full_Qualified_Label = asmTokenTag.Tag.Misc + label;

                            if (!this._labelGraph.Has_Label(full_Qualified_Label))
                            {
                                var toolTipContent = Undefined_Label_Tool_Tip_Content();
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
                            }
                        }
                        break;
                    }
                    case AsmTokenType.LabelDef:
                    {
                        if (Settings.Default.IntelliSenseDecorateClashingLabels)
                        {
                            string label = tagSpan.GetText();
                            string full_Qualified_Label = asmTokenTag.Tag.Misc + label;

                            if (this._labelGraph.Has_Label_Clash(full_Qualified_Label))
                            {
                                var toolTipContent = Label_Clash_Tool_Tip_Content(full_Qualified_Label);
                                yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("warning", toolTipContent));
                            }
                        }
                        break;
                    }
                    default: break;
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "LabelErrorTagger");
        }

        #region Private Methods

        private TextBlock Undefined_Label_Tool_Tip_Content()
        {
            TextBlock textBlock = new TextBlock();
            Run r1 = new Run("Undefined Label")
            {
                FontWeight = FontWeights.Bold
            };
            textBlock.Inlines.Add(r1);
            return textBlock;
        }

        private TextBlock Label_Clash_Tool_Tip_Content(string label)
        {
            TextBlock textBlock = new TextBlock();
            try
            {
                Run r1 = new Run("Label Clash:" + Environment.NewLine)
                {
                    FontWeight = FontWeights.Bold
                };
                textBlock.Inlines.Add(r1);

                StringBuilder sb = new StringBuilder();
                foreach (uint id in this._labelGraph.Get_Label_Def_Linenumbers(label))
                {
                    int lineNumber = this._labelGraph.Get_Linenumber(id);
                    string filename = Path.GetFileName(this._labelGraph.Get_Filename(id));
                    string lineContent;
                    if (this._labelGraph.Is_From_Main_File(id))
                    {
                        lineContent = " :" + this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                    } else
                    {
                        lineContent = "";
                    }
                    sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format("Defined at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                }
                string msg = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());

                Run r2 = new Run(msg);
                textBlock.Inlines.Add(r2);
            } catch (Exception e)
            {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:labelClashToolTipContent; e={1}", ToString(), e.ToString()));
            }
            return textBlock;
        }

        private static int Get_Linenumber(SnapshotSpan span)
        {
            int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
            //int lineNumber2 = span.Snapshot.GetLineNumberFromPosition(span.End);
            //if (lineNumber != lineNumber2) {
            //    AsmDudeToolsStatic.Output(string.Format("WARNING: LabelErrorTagger:getLineNumber. line number from start {0} is not equal to line number from end {1}.", lineNumber, lineNumber2));
            //}
            return lineNumber;
        }

        private int Get_Keyword_Begin_End(int lineNumber, string keyword)
        {
            int lengthKeyword = keyword.Length;
            string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
            //AsmDudeToolsStatic.Output_INFO("LabelErrorTagger:Get_Keyword_Begin_End lineContent=" + lineContent);

            int startPos = -1;
            for (int i = 0; i < lineContent.Length - lengthKeyword; ++i)
            {
                if (lineContent.Substring(i, lengthKeyword).Equals(keyword))
                {
                    startPos = i;
                    break;
                }
            }

            if (startPos == -1)
            {
                return 0;
            }
            return (startPos | ((startPos + lengthKeyword) << 16));
        }

        private void Handle_Label_Graph_Reset_Done_Event(object sender, CustomEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO("LabelErrorTagger: received an event from labelGraph "+ e.Message);
            Update_Error_Tasks_Async();
        }

        async private void Update_Error_Tasks_Async()
        {
            if (!this._labelGraph.Is_Enabled) return;

            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (this._updateLock)
                {
                    try
                    {
                        #region Update Tags
                        var temp = TagsChanged;
                        if (temp != null)
                        {
                            // is this code even reached?
                            foreach (uint id in this._labelGraph.Get_All_Related_Linenumber())
                            {
                                if (this._labelGraph.Is_From_Main_File(id))
                                {
                                    int lineNumber = (int)id;
                                    temp(this, new SnapshotSpanEventArgs(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent));
                                }
                            }
                        }
                        #endregion Update Tags

                        #region Update Error Tasks
                        if (Settings.Default.IntelliSenseShowClashingLabels || 
                            Settings.Default.IntelliSenseShowUndefinedLabels || 
                            Settings.Default.IntelliSenseShowUndefinedIncludes)
                        {
                            var errorTasks = this._errorListProvider.Tasks;

                            for (int i = errorTasks.Count - 1; i >= 0; --i)
                            {
                                AsmErrorEnum subCategory = (AsmErrorEnum)errorTasks[i].SubcategoryIndex;
                                if (subCategory != AsmErrorEnum.NONE)
                                {
                                    errorTasks.RemoveAt(i);
                                }
                            }
                            bool errorExists = false;

                            if (Settings.Default.IntelliSenseShowClashingLabels)
                            {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.Get_Label_Clashes)
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.LABEL_CLASH,
                                        Line = this._labelGraph.Get_Linenumber(entry.Key),
                                        Column = Get_Keyword_Begin_End(lineNumber, label),
                                        Text = "Label Clash: " + label,
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorExists = true;
                                }
                            }
                            if (Settings.Default.IntelliSenseShowUndefinedLabels)
                            {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.Get_Undefined_Labels)
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.LABEL_UNDEFINED,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineNumber, label),
                                        Text = "Undefined Label: " + label,
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorExists = true;
                                }
                            }
                            if (Settings.Default.IntelliSenseShowUndefinedIncludes)
                            {
                                foreach (Undefined_Label_Struct undefinedLabelStruct in this._labelGraph.Get_Undefined_Includes)
                                {
                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.INCLUDE_UNDEFINED,
                                        Line = undefinedLabelStruct.lineNumber,
                                        Column = Get_Keyword_Begin_End(undefinedLabelStruct.lineNumber, undefinedLabelStruct.include_filename),
                                        Text = "Could not resolve include \"" + undefinedLabelStruct.include_filename + "\" at line " + (undefinedLabelStruct.lineNumber + 1) + " in file \"" + undefinedLabelStruct.source_filename + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = undefinedLabelStruct.source_filename
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorExists = true;
                                }
                            }
                            if (errorExists)
                            {
                                this._errorListProvider.Refresh();
                                this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                            }
                        }
                        #endregion Update Error Tasks

                    } catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:updateErrorTasks; e={1}", ToString(), e.ToString()));
                    }
                }
            });
        }

        #endregion Private Methods
    }
}
