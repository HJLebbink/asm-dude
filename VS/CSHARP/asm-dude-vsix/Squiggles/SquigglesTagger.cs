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
using AsmSimZ3.Mnemonics_ng;

namespace AsmDude.Squiggles
{
    internal sealed class SquigglesTagger : ITagger<IErrorTag>
    {
        #region Private Fields
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly ILabelGraph _labelGraph;
        private readonly AsmSimulator _asmSimulator;
        private readonly Brush _foreground;
        private readonly SyntaxErrorAnalysis _syntaxErrors;
        private readonly SemanticErrorAnalysis _semanticErrors;
        private object _updateLock = new object();
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        #endregion Private Fields

        internal SquigglesTagger(
            ITextBuffer buffer,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            ILabelGraph labelGraph,
            AsmSimulator asmSimulator)
        {
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger: constructor");
            this._sourceBuffer = buffer;
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, aggregatorFactory);
            this._errorListProvider = AsmDudeTools.Instance.Error_List_Provider;
            this._labelGraph = labelGraph;
            this._asmSimulator = asmSimulator;
            this._foreground = AsmDudeToolsStatic.GetFontColor();
            this._syntaxErrors = new SyntaxErrorAnalysis(buffer, asmSimulator.Tools);
            this._semanticErrors = new SemanticErrorAnalysis(buffer, asmSimulator, asmSimulator.Tools);

            this._labelGraph.Reset_Done_Event += this.Handle_Done_Event_LabelGraph_Reset;
            this._labelGraph.Reset_Delayed();

            this._syntaxErrors.Reset_Done_Event += this.Handle_Done_Event_SyntaxErrors_Reset;
            this._syntaxErrors.Reset_Delayed();

            this._semanticErrors.Reset_Done_Event += this.Handle_Done_Event_SemanticErrors_Reset;
            this._semanticErrors.Reset_Delayed();

            this._asmSimulator.Simulate_Done_Event += this.Handle_Simulate_Done_Event;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {  // there is no content in the buffer
                yield break;
            }

            bool labelGraph_Enabled = this._labelGraph.Is_Enabled;
            bool asmSimulator_Enabled = this._asmSimulator.Is_Enabled;

            if (!labelGraph_Enabled && !asmSimulator_Enabled)
            {   // nothing to decorate
                yield break;
            }

            DateTime time1 = DateTime.Now;

            bool Decorate_Undefined_Labels = labelGraph_Enabled && Settings.Default.IntelliSense_Decorate_UndefinedLabels;
            bool Decorate_Clashing_Labels = labelGraph_Enabled && Settings.Default.IntelliSense_Decorate_ClashingLabels;
            bool Decorate_Undefined_Includes = labelGraph_Enabled && Settings.Default.IntelliSense_Show_Undefined_Includes;

            bool Decorate_Registers_Known_Register_Values = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Registers;
            bool Decorate_Syntax_Errors = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Syntax_Errors;
            bool Decorate_Unimplemented = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Unimplemented;
            bool Decorate_Usage_Of_Undefined = asmSimulator_Enabled && Settings.Default.AsmSim_Decorate_Usage_Of_Undefined;

            bool Show_Syntax_Error_Error_List = asmSimulator_Enabled && Settings.Default.AsmSim_Show_Syntax_Errors;
            bool Show_Usage_Of_Undefined = asmSimulator_Enabled && Settings.Default.AsmSim_Show_Usage_Of_Undefined;

            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in this._aggregator.GetTags(spans))
            {
                SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(this._sourceBuffer)[0];
                //AsmDudeToolsStatic.Output_INFO(string.Format("SquigglesTagger:GetTags: found keyword \"{0}\"", tagSpan.GetText()));

                int lineNumber = Get_Linenumber(tagSpan);

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

                                State2 state = this._asmSimulator.Get_State_After(lineNumber, false);
                                if (state != null)
                                {
                                    //string registerContent = state.GetString(regName);
                                    bool hasRegisterContent = this._asmSimulator.HasRegisterValue(regName, state);

                                    if (hasRegisterContent)
                                    {   // only show squiggles to indicate that information is available

                                        //PredefinedErrorTypeNames.Warning is green
                                        //PredefinedErrorTypeNames.SyntaxError is red
                                        //PredefinedErrorTypeNames.CompilerError is blue
                                        //PredefinedErrorTypeNames.Suggestion is NOTHING
                                        //PredefinedErrorTypeNames.OtherError is purple

                                        //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:GetTags: adding squiggles for register " + regName + " at line " + lineNumber);
                                        yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.Warning));
                                    }
                                }
                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic:
                        {
                            if (Decorate_Syntax_Errors || Decorate_Unimplemented)
                            {
                                if (this._syntaxErrors.IsImplemented(lineNumber))
                                {
                                    if (Decorate_Syntax_Errors && this._syntaxErrors.HasSyntaxError(lineNumber))
                                    {
                                        string message = AsmSourceTools.Linewrap("Syntax Error: " + this._syntaxErrors.GetSyntaxError(lineNumber), AsmDudePackage.maxNumberOfCharsInToolTips);
                                        yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
                                    }
                                }
                                else if (Decorate_Unimplemented)
                                {
                                    string message = AsmSourceTools.Linewrap("Instruction " + tagSpan.GetText() + " is not (yet) supported by the simulator.", AsmDudePackage.maxNumberOfCharsInToolTips);
                                    yield return new TagSpan<IErrorTag>(tagSpan, new ErrorTag(PredefinedErrorTypeNames.CompilerError, message));
                                }
                            }
                            if (Decorate_Usage_Of_Undefined)
                            {
                                if (this._semanticErrors.HasSemanticError(lineNumber))
                                {
                                    string message = AsmSourceTools.Linewrap("Semantic Error: " + this._semanticErrors.GetSemanticError(lineNumber), AsmDudePackage.maxNumberOfCharsInToolTips);
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
                                    if (tup.LineNumber == lineNumber)
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
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:labelClashToolTipContent; e={1}", ToString(), e.ToString()));
            }
            return textBlock;
        }

        private static int Get_Linenumber(SnapshotSpan span)
        {
            int lineNumber = span.Snapshot.GetLineNumberFromPosition(span.Start);
            //int lineNumber2 = span.Snapshot.GetLineNumberFromPosition(span.End);
            //if (lineNumber != lineNumber2) {
            //    AsmDudeToolsStatic.Output(string.Format("WARNING: SquigglesTagger:getLineNumber. line number from start {0} is not equal to line number from end {1}.", lineNumber, lineNumber2));
            //}
            return lineNumber;
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

        #region Event Handlers

        private void Handle_Done_Event_LabelGraph_Reset(object sender, CustomEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handle_Done_Event_LabelGraph_Reset received an event from labelGraph "+ e.Message);
            this.Update_Squiggles_Tasks_Async();
            this.Update_Error_Tasks_Labels_Async();
        }

        private void Handle_Done_Event_SyntaxErrors_Reset(object sender, CustomEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handle_Done_Event_SyntaxErrors_Reset received an event from sender ");
            this.Update_Squiggles_Tasks_Async();
            this.Update_Error_Tasks_AsmSim_Async();
        }

        private void Handle_Done_Event_SemanticErrors_Reset(object sender, CustomEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handle_Done_Event_SemanticErrors_Reset received an event from sender ");
            this.Update_Squiggles_Tasks_Async();
            //this.Update_Error_Tasks_AsmSim_Async();
        }

        private void Handle_Simulate_Done_Event(object sender, CustomEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO("SquigglesTagger:Handle_Simulate_Done_Event received an event "+ e.Message);
            this.Update_Squiggles_Tasks_Async();
        }

        #endregion

        #region Async
        private async void Update_Error_Tasks_AsmSim_Async()
        {
            if (!this._asmSimulator.Is_Enabled) return;
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
                            var errorTasks = this._errorListProvider.Tasks;

                            #region Remove stale error tasks from the error list
                            for (int i = errorTasks.Count - 1; i >= 0; --i)
                            {
                                AsmErrorEnum subCategory = (AsmErrorEnum)errorTasks[i].SubcategoryIndex;
                                if ((subCategory == AsmErrorEnum.USAGE_OF_UNDEFINED) || 
                                    (subCategory == AsmErrorEnum.SYNTAX_ERROR))
                                {
                                    errorTasks.RemoveAt(i);
                                }
                            }
                            bool newErrorsAdded = false;
                            #endregion

                            if (Settings.Default.AsmSim_Show_Syntax_Errors)
                            {
                                foreach (var tup in this._syntaxErrors.SyntaxErrors)
                                {
                                    string message = tup.Message;
                                    Mnemonic mnemonic = tup.Mnemonic;
                                    int lineNumber = tup.LineNumber;
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.SYNTAX_ERROR,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineContent, mnemonic.ToString()),
                                        Text = message,
                                        ErrorCategory = TaskErrorCategory.Error,
                                        Document = AsmDudeToolsStatic.GetFileName(this._sourceBuffer)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                }
                            }
                            if (Settings.Default.AsmSim_Show_Usage_Of_Undefined)
                            {
                                foreach (var tup in this._semanticErrors.SemanticErrors)
                                {
                                    string message = tup.Message;
                                    int lineNumber = tup.LineNumber;
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.SYNTAX_ERROR,
                                        Line = lineNumber,
                                        Column = 0,// Get_Keyword_Begin_End(lineContent, mnemonic.ToString()),
                                        Text = message,
                                        ErrorCategory = TaskErrorCategory.Error,
                                        Document = AsmDudeToolsStatic.GetFileName(this._sourceBuffer)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                }
                            }
                            if (newErrorsAdded)
                            {
                                this._errorListProvider.Refresh();
                                this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
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

        private async void Update_Error_Tasks_Labels_Async()
        {
            if (!this._labelGraph.Is_Enabled) return;

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

                            #region Remove stale error tasks from the error list
                            for (int i = errorTasks.Count - 1; i >= 0; --i)
                            {
                                AsmErrorEnum subCategory = (AsmErrorEnum)errorTasks[i].SubcategoryIndex;
                                if ((subCategory == AsmErrorEnum.LABEL_UNDEFINED) ||
                                    (subCategory == AsmErrorEnum.LABEL_CLASH) ||
                                    (subCategory == AsmErrorEnum.INCLUDE_UNDEFINED))
                                {
                                    errorTasks.RemoveAt(i);
                                }
                            }
                            bool newErrorsAdded = false;
                            #endregion

                            if (Settings.Default.IntelliSense_Show_ClashingLabels)
                            {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.Label_Clashes)
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.LABEL_CLASH,
                                        Line = this._labelGraph.Get_Linenumber(entry.Key),
                                        Column = Get_Keyword_Begin_End(lineContent, label),
                                        Text = "Label Clash: \"" + label + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    newErrorsAdded = true;
                                }
                            }
                            if (Settings.Default.IntelliSense_Show_UndefinedLabels)
                            {
                                foreach (KeyValuePair<uint, string> entry in this._labelGraph.Undefined_Labels)
                                {
                                    string label = entry.Value;
                                    int lineNumber = this._labelGraph.Get_Linenumber(entry.Key);
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.LABEL_UNDEFINED,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineContent, label),
                                        Text = "Undefined Label: \"" + label + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = this._labelGraph.Get_Filename(entry.Key)
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    newErrorsAdded = true;
                                }
                            }
                            if (Settings.Default.IntelliSense_Show_Undefined_Includes)
                            {
                                foreach (var tup in this._labelGraph.Undefined_Includes)
                                {
                                    string include = tup.Include_Filename;
                                    int lineNumber = tup.LineNumber;
                                    //TODO retrieve the lineContent of the correct buffer!
                                    string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();

                                    ErrorTask errorTask = new ErrorTask()
                                    {
                                        SubcategoryIndex = (int)AsmErrorEnum.INCLUDE_UNDEFINED,
                                        Line = lineNumber,
                                        Column = Get_Keyword_Begin_End(lineContent, include),
                                        Text = "Could not resolve include \"" + include + "\" at line " + (lineNumber + 1) + " in file \"" + tup.Source_Filename + "\"",
                                        ErrorCategory = TaskErrorCategory.Warning,
                                        Document = tup.Source_Filename
                                    };
                                    errorTask.Navigate += AsmDudeToolsStatic.Error_Task_Navigate_Handler;
                                    errorTasks.Add(errorTask);
                                    newErrorsAdded = true;
                                }
                            }
                            if (newErrorsAdded)
                            {
                                this._errorListProvider.Refresh();
                                this._errorListProvider.Show(); // do not use BringToFront since that will select the error window.
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
