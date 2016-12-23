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

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using AsmDude.SyntaxHighlighting;
using System.Text;
using AsmDude.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AsmTools;
using System.IO;

namespace AsmDude.QuickInfo
{
    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    internal sealed class AsmQuickInfoSource : IQuickInfoSource
    {
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ILabelGraph _labelGraph;
        private readonly AsmDudeTools _asmDudeTools;

        public object CSharpEditorResources { get; private set; }

        public AsmQuickInfoSource(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> aggregator,
                ILabelGraph labelGraph)
        {
            this._sourceBuffer = buffer;
            this._aggregator = aggregator;
            this._labelGraph = labelGraph;
            this._asmDudeTools = AsmDudeTools.Instance;
        }

        /// <summary>
        /// Determine which pieces of Quickinfo content should be displayed
        /// </summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            //AsmDudeToolsStatic.Output("INFO: AsmQuickInfoSource:AugmentQuickInfoSession");
            applicableToSpan = null;
            try
            {
                DateTime time1 = DateTime.Now;

                ITextSnapshot snapshot = this._sourceBuffer.CurrentSnapshot;
                var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
                if (triggerPoint == null)
                {
                    //AsmDudeToolsStatic.Output("INFO: AsmQuickInfoSource:AugmentQuickInfoSession: trigger point is null");
                    return;
                }
                string keyword = "";

                IEnumerable<IMappingTagSpan<AsmTokenTag>> enumerator = this._aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint));
                //AsmDudeToolsStatic.Output("INFO: AsmQuickInfoSource:AugmentQuickInfoSession: enumerator.Count="+ enumerator.Count());

                if (enumerator.Count() > 0)
                {
                    if (false)
                    {
                        // TODO: multiple tags at the provided triggerPoint is most likely the result of a bug in AsmTokenTagger, but it seems harmless...
                        if (enumerator.Count() > 1)
                        {
                            foreach (IMappingTagSpan<AsmTokenTag> v in enumerator)
                            {
                                AsmDudeToolsStatic.Output(string.Format("WARNING: {0}:AugmentQuickInfoSession. more than one tag! \"{1}\"", ToString(), v.Span.GetSpans(this._sourceBuffer).First().GetText()));
                            }
                        }
                    }

                    IMappingTagSpan<AsmTokenTag> asmTokenTag = enumerator.First();
                    SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(this._sourceBuffer).First();
                    keyword = tagSpan.GetText();

                    //AsmDudeToolsStatic.Output("INFO: AsmQuickInfoSource:AugmentQuickInfoSession: keyword=\""+ keyword + "\"; type=" + asmTokenTag.Tag.type +"; file="+AsmDudeToolsStatic.GetFileName(session.TextView.TextBuffer));
                    string keywordUpper = keyword.ToUpper();
                    applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);

                    TextBlock description = null;

                    switch (asmTokenTag.Tag.Type)
                    {
                        case AsmTokenType.Misc:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Keyword "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Misc));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Directive "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Directive));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.Register:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Register "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Register));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.Mnemonic:
                        case AsmTokenType.Jump:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Mnemonic "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Opcode));

                                string descr = this._asmDudeTools.Mnemonic_Store.getDescription(AsmSourceTools.parseMnemonic(keywordUpper));
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.Label:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Label "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Label));

                                string descr = Get_Label_Description(keyword);
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.LabelDef:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Label "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Label));

                                string descr = Get_Label_Def_Description(keyword);
                                if (descr.Length > 0)
                                {
                                    description.Inlines.Add(new Run(AsmSourceTools.linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips)));
                                }
                                break;
                            }
                        case AsmTokenType.Constant:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Constant "));
                                description.Inlines.Add(Make_Run2(keyword, Settings.Default.SyntaxHighlighting_Constant));
                                break;
                            }
                        default:
                            //description = new TextBlock();
                            //description.Inlines.Add(makeRun1("Unused tagType " + asmTokenTag.Tag.type));
                            break;
                    }
                    if (description != null)
                    {
                        description.FontSize = AsmDudeToolsStatic.Get_Font_Size() + 2;
                        description.FontFamily = AsmDudeToolsStatic.Get_Font_Type();
                        //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                        quickInfoContent.Add(description);
                    }
                }
                //AsmDudeToolsStatic.Output("INFO: AsmQuickInfoSource:AugmentQuickInfoSession: applicableToSpan=\"" + applicableToSpan + "\"; quickInfoContent,Count=" + quickInfoContent.Count);
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "QuickInfo");
            } catch (Exception e)
            {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:AugmentQuickInfoSession; e={1}", ToString(), e.ToString()));
            }
        }

        public void Dispose()
        {
            //empty
        }

        #region Private Methods

        private static Run Make_Run1(string str)
        {
            return new Run(str)
            {
                FontWeight = FontWeights.Bold
            };
        }

        private static Run Make_Run2(string str, System.Drawing.Color color)
        {
            return new Run(str)
            {
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(AsmDudeToolsStatic.Convert_Color(color))
            };
        }

        private string Get_Label_Description(string label)
        {
            if (this._labelGraph.Is_Enabled)
            {
                StringBuilder sb = new StringBuilder();
                SortedSet<uint> labelDefs = this._labelGraph.Get_Label_Def_Linenumbers(label);
                if (labelDefs.Count > 1)
                {
                    sb.AppendLine("");
                }
                foreach (uint id in labelDefs)
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
                string result = sb.ToString();
                return result.TrimEnd(Environment.NewLine.ToCharArray());
            } else
            {
                return "Label analysis is disabled";
            }
        }

        private string Get_Label_Def_Description(string label)
        {
            if (this._labelGraph.Is_Enabled)
            {
                SortedSet<uint> usage = this._labelGraph.Label_Used_At_Info(label);
                if (usage.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    if (usage.Count > 1)
                    {
                        sb.AppendLine("");
                    }
                    foreach (uint id in usage)
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
                        sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format("Used at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                        //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:getLabelDefDescription; sb=\"{1}\"", this.ToString(), sb.ToString()));
                    }
                    string result = sb.ToString();
                    return result.TrimEnd(Environment.NewLine.ToCharArray());
                } else
                {
                    return "Not used";
                }
            } else
            {
                return "Label analysis is disabled";
            }
        }

        #endregion Private Methods
    }
}