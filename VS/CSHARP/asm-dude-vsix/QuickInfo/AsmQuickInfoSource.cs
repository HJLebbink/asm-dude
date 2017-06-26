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
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using AsmTools;
using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;
using AsmSim;

namespace AsmDude.QuickInfo
{
    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    internal sealed class AsmQuickInfoSource : IQuickInfoSource
    {
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly LabelGraph _labelGraph;
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmDudeTools _asmDudeTools;
        private readonly Brush _foreground;

        public object CSharpEditorResources { get; private set; }

        public AsmQuickInfoSource(
                ITextBuffer buffer,
                IBufferTagAggregatorFactoryService aggregatorFactory,
                LabelGraph labelGraph,
                AsmSimulator asmSimulator)
        {
            this._sourceBuffer = buffer;
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, aggregatorFactory);
            this._labelGraph = labelGraph;
            this._asmSimulator = asmSimulator;
            this._asmDudeTools = AsmDudeTools.Instance;
            this._foreground = AsmDudeToolsStatic.GetFontColor();
        }

        /// <summary>Determine which pieces of Quickinfo content should be displayed</summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;
            try
            {
                string contentType = this._sourceBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    this.Handle(session, quickInfoContent, out applicableToSpan);
                    return;
                }
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:AugmentQuickInfoSession; does not have have AsmDudeContentType: but has type {1}", ToString(), contentType));
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:AugmentQuickInfoSession; e={1}", ToString(), e.ToString()));
            }
        }

        public void Dispose() {}

        #region Private Methods

        private void Handle(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;
            DateTime time1 = DateTime.Now;

            ITextSnapshot snapshot = this._sourceBuffer.CurrentSnapshot;
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
            if (triggerPoint == null)
            {
                AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: trigger point is null");
                return;
            }

            var enumerator = this._aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)).GetEnumerator();
            if (enumerator.MoveNext())
            {
                var asmTokenTag = enumerator.Current;

                var enumerator2 = asmTokenTag.Span.GetSpans(this._sourceBuffer).GetEnumerator();
                if (enumerator2.MoveNext())
                {
                    SnapshotSpan tagSpan = enumerator2.Current;
                    string keyword = tagSpan.GetText();
                    string keywordUpper = keyword.ToUpper();

                    #region Tests
                    // TODO: multiple tags at the provided triggerPoint is most likely the result of a bug in AsmTokenTagger, but it seems harmless...
                    if (false)
                    {
                        if (enumerator.MoveNext())
                        {
                            var asmTokenTagX = enumerator.Current;
                            var enumeratorX = asmTokenTagX.Span.GetSpans(this._sourceBuffer).GetEnumerator();
                            enumeratorX.MoveNext();
                            AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:AugmentQuickInfoSession. current keyword " + keyword + ": but span has more than one tag! next tag=\"{1}\"", ToString(), enumeratorX.Current.GetText()));
                        }
                    }
                    #endregion

                    int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);

                    //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: keyword=\""+ keyword + "\"; type=" + asmTokenTag.Tag.type +"; file="+AsmDudeToolsStatic.GetFileName(session.TextView.TextBuffer));
                    applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);

                    TextBlock description = null;
                    AsmTokenType type = asmTokenTag.Tag.Type;
                    switch (type)
                    {
                        case AsmTokenType.Misc:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Keyword ", this._foreground));
                                description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Misc))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.Directive:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Directive ", this._foreground));
                                description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Directive))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.Register:
                            {
                                if (keywordUpper.StartsWith("%")) keywordUpper = keywordUpper.Substring(1); // remove the preceding % in AT&T syntax 
                                Rn reg = RegisterTools.ParseRn(keywordUpper, true);

                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Register ", this._foreground));
                                description.Inlines.Add(Make_Run2(reg.ToString(), new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }

                                if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Decorate_Registers)
                                {
                                    string reg_Content_Before = this._asmSimulator.Get_Register_Value(reg, lineNumber, true, false, false).Value;
                                    string reg_Content_After = this._asmSimulator.Get_Register_Value(reg, lineNumber, false, false, false).Value;
                                    string msg = "\n" + reg + " before: " + reg_Content_Before + "\n" + reg + " after:  " + reg_Content_After;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(msg, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.Mnemonic:
                        case AsmTokenType.Jump:
                            {
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic_Att(keywordUpper, true);

                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Mnemonic ", this._foreground));
                                description.Inlines.Add(Make_Run2(mnemonic.ToString(), new SolidColorBrush(AsmDudeToolsStatic.ConvertColor((type == AsmTokenType.Mnemonic) ? Settings.Default.SyntaxHighlighting_Opcode : Settings.Default.SyntaxHighlighting_Jump))));

                                {
                                    string archStr = ":" + ArchTools.ToString(this._asmDudeTools.Mnemonic_Store.GetArch(mnemonic)) + " ";
                                    string descr = this._asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(archStr + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                // add performance information
                                this.Add_Performance_Description(description, mnemonic);
                                break;
                            }
                        case AsmTokenType.Label:
                            {
                                string label = keyword;
                                string labelPrefix = asmTokenTag.Tag.Misc;
                                string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(labelPrefix, label, AsmDudeToolsStatic.Used_Assembler);

                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Label ", this._foreground));
                                description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label))));

                                string descr = Get_Label_Description(full_Qualified_Label);
                                if (descr.Length == 0)
                                {
                                    descr = Get_Label_Description(label);
                                }
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.LabelDef:
                            {
                                string label = keyword;
                                string extra_Tag_Info = asmTokenTag.Tag.Misc;
                                string full_Qualified_Label;
                                if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO))
                                {
                                    full_Qualified_Label = label;
                                }
                                else
                                {
                                    full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(extra_Tag_Info, label, AsmDudeToolsStatic.Used_Assembler);
                                }

                                AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: found label def " + full_Qualified_Label);

                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Label ", this._foreground));
                                description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label))));

                                string descr = Get_Label_Def_Description(full_Qualified_Label, label);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.Constant:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("Constant ", this._foreground));

                                var constant = ExpressionEvaluator.Evaluate(keyword);
                                string constantStr = (constant.Valid)
                                    ? constant.Value + "d = " + constant.Value.ToString("X") + "h = " + AsmSourceTools.ToStringBin(constant.Value, constant.NBits) + "b"
                                    : keyword;

                                description.Inlines.Add(Make_Run2(constantStr, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Constant))));
                                break;
                            }
                        case AsmTokenType.UserDefined1:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("User defined 1: ", this._foreground));
                                description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined1))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.UserDefined2:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("User defined 2: ", this._foreground));
                                description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined2))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
                                break;
                            }
                        case AsmTokenType.UserDefined3:
                            {
                                description = new TextBlock();
                                description.Inlines.Add(Make_Run1("User defined 3: ", this._foreground));
                                description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined3))));

                                string descr = this._asmDudeTools.Get_Description(keywordUpper);
                                if (descr.Length > 0)
                                {
                                    if (keyword.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
                                    description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                                    {
                                        Foreground = this._foreground
                                    });
                                }
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
                        //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                        quickInfoContent.Add(description);
                    }
                }
            }
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: applicableToSpan=\"" + applicableToSpan + "\"; quickInfoContent,Count=" + quickInfoContent.Count);
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "QuickInfo");
        }

        private static Run Make_Run1(string str, Brush foreground)
        {
            return new Run(str)
            {
                FontWeight = FontWeights.Bold,
                Foreground = foreground
            };
        }

        private static Run Make_Run2(string str, Brush foreground)
        {
            return new Run(str)
            {
                FontWeight = FontWeights.Bold,
                Foreground = foreground 
            };
        }

        private string Get_Label_Description(string label)
        {
            if (this._labelGraph.Enabled)
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

        private string Get_Label_Def_Description(string full_Qualified_Label, string label)
        {
            if (!this._labelGraph.Enabled)
            {
                return "Label analysis is disabled";
            }

            SortedSet<uint> usage = this._labelGraph.Label_Used_At_Info(full_Qualified_Label, label);
            if (usage.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                if (usage.Count > 1)
                {
                    sb.AppendLine(""); // add a newline if multiple usage occurances exist
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
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:getLabelDefDescription; sb=\"{1}\"", this.ToString(), sb.ToString()));
                }
                string result = sb.ToString();
                return result.TrimEnd(Environment.NewLine.ToCharArray());
            } else
            {
                return "Not used";
            }
        }

        /// <summary> Add performance description of mnemonic to the provided description</summary>
        private void Add_Performance_Description(TextBlock description, Mnemonic mmemonic)
        {
            MicroArch selectedMicroarchitures = AsmDudeToolsStatic.Get_MicroArch_Switched_On();

            bool first = true;
            FontFamily family = new FontFamily("Consolas");

            string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

            foreach (PerformanceItem item in this._asmDudeTools.Performance_Store.GetPerformance(mmemonic, selectedMicroarchitures))
            {
                if (first)
                {
                    first = false;
                    description.Inlines.Add(new Run(string.Format("\n\n"+ format + "\n",
                        "", "", "µOps", "µOps", "µOps", "", "", ""))
                    {
                        FontFamily = family,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        Foreground = this._foreground
                    });
                    description.Inlines.Add(new Run(string.Format(format + "\n", 
                        "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", ""))
                    {
                        FontFamily = family,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        Foreground = this._foreground
                    });
                }
                description.Inlines.Add(new Run(string.Format(format + "\n",
                    item._microArch, 
                    item._instr + " " + item._args, 
                    item._mu_Ops_Fused,
                    item._mu_Ops_Merged,
                    item._mu_Ops_Port,
                    item._latency, 
                    item._throughput, 
                    item._remark))
                {
                    FontFamily = family,
                    Foreground = this._foreground
                });
            }
        }

        #endregion Private Methods
    }
}