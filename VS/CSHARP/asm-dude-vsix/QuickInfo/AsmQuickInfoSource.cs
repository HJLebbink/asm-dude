// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmDude.QuickInfo
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Media;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using EnvDTE;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
     //internal sealed class QuickInfoSource : IAsyncQuickInfoSource //XYZZY NEW
    internal sealed class AsmQuickInfoSource : IQuickInfoSource //XYZZY OLD
    {
        private readonly ITextBuffer _textBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly LabelGraph _labelGraph;
        private readonly AsmSimulator _asmSimulator;
        private readonly AsmDudeTools _asmDudeTools;

        public object CSharpEditorResources { get; private set; }

        public AsmQuickInfoSource(
                ITextBuffer textBuffer,
                IBufferTagAggregatorFactoryService aggregatorFactory,
                LabelGraph labelGraph,
                AsmSimulator asmSimulator)
        {
            this._textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(textBuffer, aggregatorFactory);
            this._labelGraph = labelGraph ?? throw new ArgumentNullException(nameof(labelGraph));
            this._asmSimulator = asmSimulator ?? throw new ArgumentNullException(nameof(asmSimulator));
            this._asmDudeTools = AsmDudeTools.Instance;
        }

        /// <summary>Determine which pieces of Quickinfo content should be displayed</summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) //XYZZY OLD
        {
            applicableToSpan = null;
            try
            {
                string contentType = this._textBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    this.Handle(session, quickInfoContent, out applicableToSpan);
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:AugmentQuickInfoSession; does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                }
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:AugmentQuickInfoSession; e={1}", this.ToString(), e.ToString()));
            }
        }

        public void AugmentQuickInfoSession_BUG(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) //XYZZY OLD
        {
            applicableToSpan = null;
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(this._textBuffer.CurrentSnapshot);
            if (triggerPoint != null)
            {
                ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
                applicableToSpan = this._textBuffer.CurrentSnapshot.CreateTrackingSpan(line.Extent, SpanTrackingMode.EdgeInclusive);
                quickInfoContent.Add(new InstructionTooltipWindow(AsmDudeToolsStatic.GetFontColor()));
            }
        }

        public void Dispose()
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Dispose", this.ToString()));
        }

        #region Private Methods

        private void Handle(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;
            DateTime time1 = DateTime.Now;

            ITextSnapshot snapshot = this._textBuffer.CurrentSnapshot;
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:Handle: trigger point is null", this.ToString()));
                return;
            }

            Brush foreground = AsmDudeToolsStatic.GetFontColor();

            (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this._aggregator, triggerPoint.Value);
            if (keywordSpan.HasValue)
            {
                SnapshotSpan tagSpan = keywordSpan.Value;
                string keyword = tagSpan.GetText();
                string keywordUpper = keyword.ToUpper();

                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Handle: keyword=\"{1}\"; type={2}; file=\"{3}\"", this.ToString(), keyword, tag.Type, AsmDudeToolsStatic.GetFilename(session.TextView.TextBuffer)));
                applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeInclusive);

                TextBlock description = null;
                switch (tag.Type)
                {
                    case AsmTokenType.Misc:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("Keyword ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Misc))));

                            string descr = this._asmDudeTools.Get_Description(keywordUpper);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.Directive:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("Directive ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Directive))));

                            string descr = this._asmDudeTools.Get_Description(keywordUpper);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.Register:
                        {
                            int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                            if (keywordUpper.StartsWith("%"))
                            {
                                keywordUpper = keywordUpper.Substring(1); // remove the preceding % in AT&T syntax
                            }

                            Rn reg = RegisterTools.ParseRn(keywordUpper, true);
                            if (this._asmDudeTools.RegisterSwitchedOn(reg))
                            {
                                RegisterTooltipWindow registerTooltipWindow = new RegisterTooltipWindow(foreground);
                                registerTooltipWindow.SetDescription(reg, this._asmDudeTools);
                                registerTooltipWindow.SetAsmSim(this._asmSimulator, reg, lineNumber, true);
                                quickInfoContent.Add(registerTooltipWindow);
                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic: // intentional fall through
                    case AsmTokenType.MnemonicOff: // intentional fall through
                    case AsmTokenType.Jump:
                        {
                            (Mnemonic mnemonic, _) = AsmSourceTools.ParseMnemonic_Att(keywordUpper, true);
                            InstructionTooltipWindow instructionTooltipWindow = new InstructionTooltipWindow(foreground)
                            {
                                Session = session, // set the owner of this windows such that we can manually close this window
                            };
                            instructionTooltipWindow.SetDescription(mnemonic, this._asmDudeTools);
                            instructionTooltipWindow.SetPerformanceInfo(mnemonic, this._asmDudeTools);
                            int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                            instructionTooltipWindow.SetAsmSim(this._asmSimulator, lineNumber, true);
                            quickInfoContent.Add(instructionTooltipWindow);
                            break;
                        }
                    case AsmTokenType.Label:
                        {
                            string label = keyword;
                            string labelPrefix = tag.Misc;
                            string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(labelPrefix, label, AsmDudeToolsStatic.Used_Assembler);

                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("Label ", foreground));
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label))));

                            string descr = this.Get_Label_Description(full_Qualified_Label);
                            if (descr.Length == 0)
                            {
                                descr = this.Get_Label_Description(label);
                            }
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.LabelDef:
                        {
                            string label = keyword;
                            string extra_Tag_Info = tag.Misc;
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
                            description.Inlines.Add(Make_Run1("Label ", foreground));
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label))));

                            string descr = this.Get_Label_Def_Description(full_Qualified_Label, label);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.Constant:
                        {
                            (bool valid, ulong value, int nBits) = AsmSourceTools.Evaluate_Constant(keyword);
                            string constantStr = valid
                                ? value + "d = " + value.ToString("X") + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
                                : keyword;


                            if (false) // experiment to get text selectable
                            {
                                TextBoxWindow myWindow = new TextBoxWindow();
                                myWindow.MouseRightButtonUp += this.MyWindow_MouseRightButtonUp;
                                myWindow.MyContent.Text = "Constant X: " + constantStr;
                                myWindow.MyContent.Foreground = foreground;
                                myWindow.MyContent.MouseRightButtonUp += this.MyContent_MouseRightButtonUp;
                                quickInfoContent.Add(myWindow);
                            }
                            else
                            {
                                description = new SelectableTextBlock();
                                description.Inlines.Add(Make_Run1("Constant ", foreground));

                                description.Inlines.Add(Make_Run2(constantStr, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Constant))));
                            }
                            break;
                        }
                    case AsmTokenType.UserDefined1:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("User defined 1: ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined1))));

                            string descr = this._asmDudeTools.Get_Description(keywordUpper);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.UserDefined2:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("User defined 2: ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined2))));

                            string descr = this._asmDudeTools.Get_Description(keywordUpper);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
                                });
                            }
                            break;
                        }
                    case AsmTokenType.UserDefined3:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("User defined 3: ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined3))));

                            string descr = this._asmDudeTools.Get_Description(keywordUpper);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }

                                description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                                {
                                    Foreground = foreground,
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
                    description.Focusable = true;
                    description.FontSize = AsmDudeToolsStatic.GetFontSize() + 2;
                    description.FontFamily = AsmDudeToolsStatic.GetFontType();
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                    quickInfoContent.Add(description);
                }
            }
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: applicableToSpan=\"" + applicableToSpan + "\"; quickInfoContent,Count=" + quickInfoContent.Count);
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "QuickInfo");
        }

        private void MyWindow_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //throw new NotImplementedException();
            //e.Handled = true;
        }

        private void MyContent_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // throw new NotImplementedException();
            //e.Handled = true;
        }

        private static Run Make_Run1(string str, Brush foreground)
        {
            return new Run(str)
            {
                Focusable = true,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
            };
        }

        private static Run Make_Run2(string str, Brush foreground)
        {
            return new Run(str)
            {
                Focusable = true,
                FontWeight = FontWeights.Bold,
                Foreground = foreground,
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
                    sb.AppendLine(string.Empty);
                }
                foreach (uint id in labelDefs)
                {
                    int lineNumber = LabelGraph.Get_Linenumber(id);
                    string filename = Path.GetFileName(this._labelGraph.Get_Filename(id));
                    string lineContent = (LabelGraph.Is_From_Main_File(id))
                        ? " :" + this._textBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText()
                        : string.Empty;
                    sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format("Defined at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                }
                string result = sb.ToString();
                return result.TrimEnd(Environment.NewLine.ToCharArray());
            }
            else
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
                    sb.AppendLine(string.Empty); // add a newline if multiple usage occurances exist
                }
                foreach (uint id in usage)
                {
                    int lineNumber = LabelGraph.Get_Linenumber(id);
                    string filename = Path.GetFileName(this._labelGraph.Get_Filename(id));
                    string lineContent;
                    if (LabelGraph.Is_From_Main_File(id))
                    {
                        lineContent = " :" + this._textBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                    }
                    else
                    {
                        lineContent = string.Empty;
                    }
                    sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format("Used at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:getLabelDefDescription; sb=\"{1}\"", this.ToString(), sb.ToString()));
                }
                string result = sb.ToString();
                return result.TrimEnd(Environment.NewLine.ToCharArray());
            }
            else
            {
                return "Not used";
            }
        }

        #endregion Private Methods
    }


    internal class TextEditorWrapper
    {
        private static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        private static readonly PropertyInfo IsReadOnlyProp = TextEditorType.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo TextViewProp = TextEditorType.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterMethod = TextEditorType.GetMethod(
            "RegisterCommandHandlers",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(bool), typeof(bool), typeof(bool) }, null);

        private static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        private static readonly PropertyInfo TextContainerTextViewProp = TextContainerType.GetProperty("TextView");

        private static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners)
        {
            RegisterMethod.Invoke(null, new object[] { controlType, acceptsRichContent, readOnly, registerEventListeners });
        }

        public static TextEditorWrapper CreateFor(TextBlock tb)
        {
            object textContainer = TextContainerProp.GetValue(tb);

            TextEditorWrapper editor = new TextEditorWrapper(textContainer, tb, false);
            IsReadOnlyProp.SetValue(editor._editor, true);
            TextViewProp.SetValue(editor._editor, TextContainerTextViewProp.GetValue(textContainer));

            return editor;
        }

        private readonly object _editor;

        public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled)
        {
            this._editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null, new[] { textContainer, uiScope, isUndoEnabled }, null);
        }
    }

    public class SelectableTextBlock : TextBlock
    {
        static SelectableTextBlock()
        {
            FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata(true));
            TextEditorWrapper.RegisterCommandHandlers(typeof(SelectableTextBlock), true, true, true);

            // remove the focus rectangle around the control
            FocusVisualStyleProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata((object)null));
        }

        private readonly TextEditorWrapper _editor;

        public SelectableTextBlock()
        {
            this._editor = TextEditorWrapper.CreateFor(this);
        }
    }
}