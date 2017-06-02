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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Adornments;

using AsmTools;
using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmSim;

namespace AsmDude.Squiggles
{
    internal sealed class SquigglesTagger : ITagger<IErrorTag>
    {
        #region Private Fields
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly LabelGraph _labelGraph;
        private readonly AsmSimulator _asmSimulator;
        private readonly Brush _foreground;
        private object _updateLock = new object();
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        #endregion Private Fields

        internal SquigglesTagger(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            LabelGraph labelGraph,
            AsmSimulator asmSimulator)
        {
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger: constructor");
            this._sourceBuffer = buffer;
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, aggregatorFactory);
            this._errorListProvider = AsmDudeTools.Instance.Error_List_Provider;
            this._foreground = AsmDudeToolsStatic.GetFontColor();

            this._labelGraph = labelGraph;
            if (this._labelGraph.Enabled)
            {
                this._labelGraph.Reset_Done_Event += (o, i) => 
                {
                    this.Update_Squiggles_Tasks_Async();
                    this.Update_Error_Tasks_Labels_Async();
                };
                this._labelGraph.Reset();
            }

            this._asmSimulator = asmSimulator;
            if (this._asmSimulator.Enabled)
            {
                this._asmSimulator.Line_Updated_Event += (o, e) =>
                {
                    AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handling _asmSimulator.Line_Updated_Event: event from " + o + ". Line " + e.LineNumber + ": "+e.Message);
                    this.Update_Squiggles_Tasks(e.LineNumber);
                    this.Update_Error_Task_AsmSim(e.LineNumber, e.Message);
                };
                this._asmSimulator.Reset_Done_Event += (o, e) =>
                {
                    AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handling _asmSimulator.Reset_Done_Event: event from " + o);
                    this.Update_Error_Tasks_AsmSim_Async();
                };
                this._asmSimulator.Reset();
            }
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {  // there is no content in the buffer
                yield break;
            }

            bool labelGraph_Enabled = this._labelGraph.Enabled;
            bool asmSimulator_Enabled = this._asmSimulator.Enabled;

            if (!labelGraph_Enabled && !asmSimulator_Enabled)
            {   // nothing to decorate
                yield break;
            }

            DateTime time1 = DateTime.Now;

            //TODO move the followign boolean to constructor
            bool Decorate_Undefined_Labels = labelGraph_Enabled && Settings.Default.IntelliSense_Decorate_UndefinedLabels;
            bool Decorate_Clashing_Labels = labelGraph_Enabled && Settings.Default.IntelliSense_Decorate_ClashingLabels;
            bool Decorate_Undefined_Includes = labelGraph_Enabled && Settings.Default.IntelliSense_Show_Undefined_Includes;

            bool Decorate_Registers_Known_Register_Values = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Registers;
            bool Decorate_Syntax_Errors = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Syntax_Errors;
            bool Decorate_Unimplemented = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Unimplemented;
            bool Decorate_Usage_Of_Undefined = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Usage_Of_Undefined;
            bool Decorate_Redundant_Instructions = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Redundant_Instructions;

            bool Show_Syntax_Error_Error_List = asmSimulator_Enabled && Settings.Default.AsmSim_Show_Syntax_Errors;
            bool Show_Usage_Of_Undefined = asmSimulator_Enabled && Settings.Default.AsmSim_Show_Usage_Of_Undefined;

            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in this._aggregator.GetTags(spans))
            {
                SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(this._sourceBuffer)[0];
                //AsmDudeToolsStatic.Output_INFO(string.Format("SquigglesTagger:GetTags: found keyword \"{0}\"", tagSpan.GetText()));

                int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);

                switch (asmTokenTag.Tag.Type)
                {
                    case AsmTokenType.Label:
                        {
                            if (Decorate_Undefined_Labels)
                            {
                                string label = tagSpan.GetText();
                                string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(asmTokenTag.Tag.Misc, label, usedAssember);

                                if (this._labelGraph.Has_Label(full_Qualified_Label))
                                {
                                    // Nothing to report
                                }
                                else
                                {
                                    //AsmDudeToolsStatic.Output_INFO(string.Format("SquigglesTagger:GetTags: found label \"{0}\"; full-label \"{1}\"", label, full_Qualified_Label));

                                    if (usedAssember == AssemblerEnum.MASM)
                                    {
                                        if (this._labelGraph.Has_Label(label))
                                        {
                                            // TODO: is this always a valid label? Nothing to report
                                        }
                                        else
                                        {
                                            var toolTipContent = Undefined_Label_Tool_Tip_Content();
                                            yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, toolTipContent));
                                        }
                                    }
                                    else
                                    {
                                        var toolTipContent = Undefined_Label_Tool_Tip_Content();
                                        yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, toolTipContent));
                                    }
                                }
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef:
                        {
                            if (Decorate_Clashing_Labels)
                            {
                                string label = tagSpan.GetText();
                                string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(asmTokenTag.Tag.Misc, label, usedAssember);

                                if (this._labelGraph.Has_Label_Clash(full_Qualified_Label))
                                {
                                    var toolTipContent = Label_Clash_Tool_Tip_Content(full_Qualified_Label);

                                    //PredefinedErrorTypeNames.Warning is green
                                    //PredefinedErrorTypeNames.SyntaxError is red
                                    //PredefinedErrorTypeNames.CompilerError is blue
                                    //PredefinedErrorTypeNames.Suggestion is NOTHING
                                    //PredefinedErrorTypeNames.OtherError is purple

                                    yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, toolTipContent));
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Register:
                        {
                            if (Decorate_Registers_Known_Register_Values)
                            {
                                Rn regName = RegisterTools.ParseRn(tagSpan.GetText());
                                //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:GetTags: found register " + regName + " at line " + lineNumber);
                                bool preCompute = false;
                                var v1 = this._asmSimulator.Has_Register_Value(regName, lineNumber, true, preCompute);
                                if (!v1.Bussy)
                                {
                                    var v2 = this._asmSimulator.Has_Register_Value(regName, lineNumber, false, preCompute);
                                    if (!v2.Bussy)
                                    {
                                        if ((v1.HasValue || v2.HasValue))
                                        {   // only show squiggles to indicate that information is available
                                            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:GetTags: adding squiggles for register " + regName + " at line " + lineNumber);
                                            yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.Warning));
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic:
                        {
                            if (Decorate_Syntax_Errors || Decorate_Unimplemented)
                            {
                                if (this._asmSimulator.Is_Implemented(lineNumber))
                                {
                                    if (Decorate_Syntax_Errors && this._asmSimulator.Has_Syntax_Error(lineNumber))
                                    {
                                        string message = AsmSourceTools.Linewrap("Syntax Error: " + this._asmSimulator.Get_Syntax_Error(lineNumber).Message, AsmDudePackage.maxNumberOfCharsInToolTips);
                                        yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
                                    }
                                }
                                else if (Decorate_Unimplemented)
                                {
                                    string message = AsmSourceTools.Linewrap("Info: Instruction " + tagSpan.GetText() + " is not (yet) supported by the simulator.", AsmDudePackage.maxNumberOfCharsInToolTips);
                                    yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.CompilerError, message));
                                }
                            }
                            if (Decorate_Usage_Of_Undefined)
                            {
                                if (this._asmSimulator.Has_Usage_Undefined_Warning(lineNumber))
                                {
                                    string message = AsmSourceTools.Linewrap("Semantic Warning: " + this._asmSimulator.Get_Usage_Undefined_Warning(lineNumber), AsmDudePackage.maxNumberOfCharsInToolTips);
                                    yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.OtherError, message));
                                }
                            }
                            if (Decorate_Redundant_Instructions)
                            {
                                if (this._asmSimulator.Has_Redundant_Instruction_Warning(lineNumber))
                                {
                                    string message = AsmSourceTools.Linewrap("Semantic Warning: " + this._asmSimulator.Get_Redundant_Instruction_Warning(lineNumber), AsmDudePackage.maxNumberOfCharsInToolTips);
                                    yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.OtherError, message));
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Constant:
                        {
                            if (Decorate_Undefined_Includes)
                            {
                                foreach (var tup in this._labelGraph.Undefined_Includes)
                                { 
                                    if (tup.LineNumber == lineNumber) //TODO this is inefficient!
                                    {
                                        var toolTipContent = "Could not resolve include \"" + tagSpan.GetText() + "\"";
                                        yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, toolTipContent));
                                        break; // leave the foreach loop
                                    }
                                }
                            }
                            break;
                        }
                    default: break;
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "SquiggleTagger");
        }

        #region Private Methods

        private TextBlock Undefined_Label_Tool_Tip_Content()
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Inlines.Add(new Run("Undefined Label")
            {
                FontWeight = FontWeights.Bold,
                Foreground = this._foreground
            });
            return textBlock;
        }

        private TextBlock Label_Clash_Tool_Tip_Content(string label)
        {
            TextBlock textBlock = new TextBlock();
            try
            {
                textBlock.Inlines.Add(new Run("Label Clash:" + Environment.NewLine)
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = this._foreground
                });

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

                textBlock.Inlines.Add(new Run(msg)
                {
                    Foreground = this._foreground
                });
            } catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:labelClashToolTipContent; e={1}", ToString(), e.ToString()));
            }
            return textBlock;
        }

        private int Get_Keyword_Begin_End(string lineContent, string keyword)
        {
            int lengthKeyword = keyword.Length;
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Get_Keyword_Begin_End lineContent=" + lineContent);

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

        private void Update_Error_Task_AsmSim(int lineNumber, AsmMessageEnum error)
        {
            if (!this._asmSimulator.Enabled) return;

            var errorTasks = this._errorListProvider.Tasks;
            bool errorListNeedsRefresh = false;

            #region Remove stale error tasks from the error list
            for (int i = errorTasks.Count - 1; i >= 0; --i)
            {
                var task = errorTasks[i];
                if (((AsmMessageEnum)task.SubcategoryIndex == error) && (task.Line == lineNumber))
                {
                    errorTasks.RemoveAt(i);
                    errorListNeedsRefresh = true;
                }
            }
            #endregion

            switch (error)
            {
                case AsmMessageEnum.SYNTAX_ERROR:
                    {
                        if (Settings.Default.AsmSim_Show_Syntax_Errors)
                        {
                            var tup = this._asmSimulator.Get_Syntax_Error(lineNumber);
                            if (tup.Message.Length > 0) this.AddErrorTask_SyntaxError(lineNumber, tup.Message, tup.Mnemonic);
                            errorListNeedsRefresh = true;
                        }
                        break;
                    }
                case AsmMessageEnum.USAGE_OF_UNDEFINED:
                    {
                        if (Settings.Default.AsmSim_Show_Usage_Of_Undefined)
                        {
                            var tup = this._asmSimulator.Get_Usage_Undefined_Warning(lineNumber);
                            if (tup.Length > 0) this.AddErrorTask_UsageUndefined(lineNumber, tup);
                            errorListNeedsRefresh = true;
                        }
                        break;
                    }
                case AsmMessageEnum.REDUNDANT:
                    {
                        if (Settings.Default.AsmSim_Show_Redundant_Instructions)
                        {
                            var tup = this._asmSimulator.Get_Redundant_Instruction_Warning(lineNumber);
                            if (tup.Length > 0) this.AddErrorTask_RedundantInstruction(lineNumber, tup);
                            errorListNeedsRefresh = true;
                        }
                        break;
                    }
                default: break;
            }

            if (errorListNeedsRefresh)
            {
                this._errorListProvider.Refresh();
                //this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
            }
        }

        #region Async
        private async void Update_Error_Tasks_AsmSim_Async()
        {
            if (!this._asmSimulator.Enabled) return;
            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (this._updateLock)
                {
                    try
                    {
                        #region Update Error Tasks
                        if (Settings.Default.AsmSim_Show_Syntax_Errors ||
                            Settings.Default.AsmSim_Show_Usage_Of_Undefined)
                        {
                            AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Update_Error_Tasks_AsmSim_Async: going to update the error list");

                            var errorTasks = this._errorListProvider.Tasks;
                            bool errorListNeedsRefresh = false;

                            #region Remove stale error tasks from the error list
                            for (int i = errorTasks.Count - 1; i >= 0; --i)
                            {
                                AsmMessageEnum subCategory = (AsmMessageEnum)errorTasks[i].SubcategoryIndex;
                                if ((subCategory == AsmMessageEnum.USAGE_OF_UNDEFINED) || 
                                    (subCategory == AsmMessageEnum.SYNTAX_ERROR) ||
                                    (subCategory == AsmMessageEnum.REDUNDANT))
                                {
                                    errorTasks.RemoveAt(i);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            #endregion

                            if (Settings.Default.AsmSim_Show_Syntax_Errors)
                            {
                                foreach (var tup in this._asmSimulator.Syntax_Errors)
                                {
                                    this.AddErrorTask_SyntaxError(tup.LineNumber, tup.Message, tup.Mnemonic);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            if (Settings.Default.AsmSim_Show_Usage_Of_Undefined)
                            {
                                foreach (var tup in this._asmSimulator.Usage_Undefined)
                                {
                                    this.AddErrorTask_UsageUndefined(tup.LineNumber, tup.Message);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            if (Settings.Default.AsmSim_Show_Redundant_Instructions)
                            {
                                foreach (var tup in this._asmSimulator.Redundant_Instruction)
                                {
                                    this.AddErrorTask_RedundantInstruction(tup.LineNumber, tup.Message);
                                    errorListNeedsRefresh = true;
                                }
                            }

                            if (errorListNeedsRefresh)
                            {
                                this._errorListProvider.Refresh();
                                //this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                            }
                        }
                        #endregion Update Error Tasks
                    }
                    catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Update_AsmSim_Error_Task_Async; e={1}", ToString(), e.ToString()));
                    }
                }
            });
        }

        private void AddErrorTask_SyntaxError(int lineNumber, string message, Mnemonic mnemonic)
        {
            string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

            ErrorTask errorTask = new ErrorTask()
            {
                SubcategoryIndex = (int)AsmMessageEnum.SYNTAX_ERROR,
                Line = lineNumber,
                Column = Get_Keyword_Begin_End(lineContent, mnemonic.ToString()),
                Text = "Syntax Error: " + message,
                ErrorCategory = TaskErrorCategory.Error,
                Document = AsmDudeToolsStatic.GetFileName(this._sourceBuffer)
            };
            errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
            this._errorListProvider.Tasks.Add(errorTask);
        }

        private void AddErrorTask_UsageUndefined(int lineNumber, string message)
        {
            //string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
            ErrorTask errorTask = new ErrorTask()
            {
                SubcategoryIndex = (int)AsmMessageEnum.USAGE_OF_UNDEFINED,
                Line = lineNumber,
                Column = 0, // Get_Keyword_Begin_End(lineContent, mnemonic.ToString()),
                Text = "Semantic Warning: " + message,
                ErrorCategory = TaskErrorCategory.Warning,
                Document = AsmDudeToolsStatic.GetFileName(this._sourceBuffer)
            };
            errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
            this._errorListProvider.Tasks.Add(errorTask);
        }

        private void AddErrorTask_RedundantInstruction(int lineNumber, string message)
        {
            //string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
            ErrorTask errorTask = new ErrorTask()
            {
                SubcategoryIndex = (int)AsmMessageEnum.REDUNDANT,
                Line = lineNumber,
                Column = 0, // Get_Keyword_Begin_End(lineContent, mnemonic.ToString()),
                Text = "Semantic Warning: " + message,
                ErrorCategory = TaskErrorCategory.Warning,
                Document = AsmDudeToolsStatic.GetFileName(this._sourceBuffer)
            };
            errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
            this._errorListProvider.Tasks.Add(errorTask);
        }

        private async void Update_Error_Tasks_Labels_Async()
        {
            if (!this._labelGraph.Enabled) return;

            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (this._updateLock)
                {
                    try
                    {
                        #region Update Error Tasks
                        if (Settings.Default.IntelliSense_Show_ClashingLabels ||
                            Settings.Default.IntelliSense_Show_UndefinedLabels ||
                            Settings.Default.IntelliSense_Show_Undefined_Includes)
                        {
                            var errorTasks = this._errorListProvider.Tasks;
                            bool errorListNeedsRefresh = false;

                            #region Remove stale error tasks from the error list
                            for (int i = errorTasks.Count - 1; i >= 0; --i)
                            {
                                AsmMessageEnum subCategory = (AsmMessageEnum)errorTasks[i].SubcategoryIndex;
                                if ((subCategory == AsmMessageEnum.LABEL_UNDEFINED) ||
                                    (subCategory == AsmMessageEnum.LABEL_CLASH) ||
                                    (subCategory == AsmMessageEnum.INCLUDE_UNDEFINED))
                                {
                                    errorTasks.RemoveAt(i);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            #endregion

                            if (Settings.Default.IntelliSense_Show_ClashingLabels)
                            {
                                foreach (var entry in this._labelGraph.Label_Clashes) // TODO Label_Clashes does not return the classes in any particular order, 
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmMessageEnum.LABEL_CLASH,
                                        Line = this._labelGraph.Get_Linenumber(entry.Key),
                                        Column = Get_Keyword_Begin_End(lineContent, label),
                                        Text = "Label Clash: \"" + label + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            if (Settings.Default.IntelliSense_Show_UndefinedLabels)
                            {
                                foreach (var entry in this._labelGraph.Undefined_Labels)
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmMessageEnum.LABEL_UNDEFINED,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineContent, label),
                                        Text = "Undefined Label: \"" + label + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            if (Settings.Default.IntelliSense_Show_Undefined_Includes)
                            {
                                foreach (var entry in this._labelGraph.Undefined_Includes)
                                {
                                    string include = entry.Include_Filename;
                                    int lineNumber = entry.LineNumber;
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmMessageEnum.INCLUDE_UNDEFINED,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineContent, include),
                                        Text = "Could not resolve include \"" + include + "\" at line " + (lineNumber + 1) + " in file \"" + entry.Source_Filename + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = entry.Source_Filename
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    errorListNeedsRefresh = true;
                                }
                            }
                            if (errorListNeedsRefresh)
                            {
                                this._errorListProvider.Refresh();
                                //this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
                            }
                        }
                        #endregion Update Error Tasks
                    }
                    catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Update_Label_Error_Tasks_Async; e={1}", ToString(), e.ToString()));
                    }
                }
            });
        }

        private void Update_Squiggles_Tasks(int lineNumber)
        {
            try
            {
                this.TagsChanged(this, new SnapshotSpanEventArgs(this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent));
            } catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("SquiggleTagger: Update_Squiggles_Tasks: e=" + e.ToString());
            }
        }
             
        private async void Update_Squiggles_Tasks_Async()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (this._updateLock)
                {
                    try
                    {
                        #region Update Tags
                        foreach (ITextSnapshotLine line in this._sourceBuffer.CurrentSnapshot.Lines)
                        {
                            this.TagsChanged(this, new SnapshotSpanEventArgs(line.Extent));
                        }
                        #endregion Update Tags
                    }
                    catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Update_Squiggles_Tasks_Async; e={1}", ToString(), e.ToString()));
                    }
                }
            });
        }
        #endregion Async
       
        #endregion Private Methods
    }
}
