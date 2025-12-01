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

namespace AsmDude.QuickInfo
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Forms;
    using System.Windows.Media;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Adornments;
    using Microsoft.VisualStudio.Text.Tagging;

    using QuickGraph.Serialization;



    using static System.Windows.Forms.VisualStyles.VisualStyleElement;
    using static Microsoft.VisualStudio.Shell.ThreadedWaitDialogHelper;

    using Span = Microsoft.VisualStudio.Text.Span;
    using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;
    using Microsoft.VisualStudio.Core.Imaging;
    using Microsoft.VisualStudio.Imaging;
    using Microsoft.Win32;
    using Microsoft.VisualStudio.Language.StandardClassification;

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    internal sealed class AsmQuickInfoSource : IAsyncQuickInfoSource
   {
        private readonly ITextBuffer textBuffer_;
        private readonly ITagAggregator<AsmTokenTag> aggregator_;
        private readonly LabelGraph labelGraph_;
        private readonly AsmSimulator asmSimulator_;
        private readonly AsmDudeTools asmDudeTools_;

        public object CSharpEditorResources { get; private set; }

        public AsmQuickInfoSource(
                ITextBuffer textBuffer,
                IBufferTagAggregatorFactoryService aggregatorFactory,
                LabelGraph labelGraph,
                AsmSimulator asmSimulator)
        {
            this.textBuffer_ = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
            this.aggregator_ = AsmDudeToolsStatic.GetOrCreate_Aggregator(textBuffer, aggregatorFactory);
            this.labelGraph_ = labelGraph ?? throw new ArgumentNullException(nameof(labelGraph));
            this.asmSimulator_ = asmSimulator ?? throw new ArgumentNullException(nameof(asmSimulator));
            this.asmDudeTools_ = AsmDudeTools.Instance;
        }

        async Task<QuickInfoItem> IAsyncQuickInfoSource.GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:GetQuickInfoItemAsync: session {1}", this.ToString(), session));
            try
            {
                string contentType = this.textBuffer_.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    ITextSnapshot snapshot = this.textBuffer_.CurrentSnapshot;
                    var quickInfo = await this.HandleAsync(session, snapshot, cancellationToken);
                    if (quickInfo is null)
                    {
                        return null;
                    }
                    return new QuickInfoItem(
                        snapshot.CreateTrackingSpan(quickInfo.Value.span, SpanTrackingMode.EdgeExclusive),
                        new ContainerElement(ContainerElementStyle.Stacked, quickInfo.Value.message));
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                    return null;
                }
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; e={1}", this.ToString(), e.ToString()));
                return null;
            }
        }

        #region Private Methods

        private static readonly ImageId _icon = KnownMonikers.AbstractCube.ToImageId();

        private async Task<(List<object> message, Span span)?> HandleAsync(IAsyncQuickInfoSession session, ITextSnapshot snapshot, CancellationToken cancellationToken)
        {
            DateTime time1 = DateTime.Now;

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:HandleAsync: trigger point is null", this.ToString()));
                return null;
            }

            var position = triggerPoint.Value.Position;

            //Task<Brush> foreground = AsmDudeToolsStatic.GetFontColorAsync();
            Brush foreground = null;


            (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this.aggregator_, triggerPoint.Value);
            if (keywordSpan.HasValue)
            {
                SnapshotSpan tagSpan = keywordSpan.Value;
                string keyword = tagSpan.GetText();
                string keyword_upcase = keyword.ToUpperInvariant();

                AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:HandleAsync: keyword=\"{1}\"; type={2}; file=\"{3}\"", this.ToString(), keyword, tag.Type, AsmDudeToolsStatic.GetFilename(session.TextView.TextBuffer)));
                //applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeInclusive);

                TextBlock description = null;
                switch (tag.Type)
                {
                    case AsmTokenType.Misc: // intentional fall through
                    case AsmTokenType.Directive:
                        {
                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
                            if (descr.Length > 0)
                            {
                                if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }
                                descr = AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);

                                var containerElement = new ContainerElement(
                                     ContainerElementStyle.Wrapped,
                                     new ImageElement(_icon),
                                     new ClassifiedTextElement(
                                         new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Directive "),
                                         new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword),
                                         new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, descr)));

                                return (new List<object> { containerElement }, keywordSpan.Value);
                            }
                            break;
                        }
                    case AsmTokenType.Register:
                        {
                            int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                            if (keyword_upcase.StartsWith("%", StringComparison.Ordinal))
                            {
                                keyword_upcase = keyword_upcase.Substring(1); // remove the preceding % in AT&T syntax
                            }

                            Rn reg = RegisterTools.ParseRn(keyword_upcase, true);
                            if (this.asmDudeTools_.RegisterSwitchedOn(reg))
                            {
                                string regStr = reg.ToString();
                                Arch arch = RegisterTools.GetArch(reg);

                                string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "] ";
                                string descr = this.asmDudeTools_.Get_Description(regStr);
                                if (regStr.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                                {
                                    descr = "\n" + descr;
                                }
                                string full_Descr = AsmSourceTools.Linewrap(":" + archStr + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);

                                var containerElement = new ContainerElement(
                                    ContainerElementStyle.Wrapped,
                                    new ImageElement(_icon),
                                    new ClassifiedTextElement(
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Register "),
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, regStr),
                                        new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, full_Descr)));

                                return (new List<object> { containerElement }, keywordSpan.Value);
                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic: // intentional fall through
                    case AsmTokenType.MnemonicOff: // intentional fall through
                    case AsmTokenType.Jump:
                        {
                            (Mnemonic mnemonic, _) = AsmSourceTools.ParseMnemonic_Att(keyword_upcase, true);
                            string mnemonicStr = mnemonic.ToString();

                            string archStr = ":" + ArchTools.ToString(this.asmDudeTools_.Mnemonic_Store.GetArch(mnemonic)) + " ";
                            string descr = this.asmDudeTools_.Mnemonic_Store.GetDescription(mnemonic);
                            string full_Descr = AsmSourceTools.Linewrap(archStr + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);

                            var x = new List<object> { };

                            string urlStr = $"https://github.com/HJLebbink/asm-dude/" + mnemonicStr;

                            x.Add(new ContainerElement(
                                 ContainerElementStyle.Wrapped,
                                 new ImageElement(_icon),
                                 new ClassifiedTextElement(
                                     new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Mnemonic ")),
                                 ClassifiedTextElement.CreateHyperlink(mnemonicStr, urlStr, () =>
                                     {
                                         System.Diagnostics.Process.Start(urlStr);
                                     }),
                                 new ClassifiedTextElement(
                                      new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, full_Descr))));

                            if (AsmDude.Settings.Default.PerformanceInfo_On)
                            {
                                //this.PerformanceExpander.IsExpanded = !Settings.Default.PerformanceInfo_IsDefaultCollapsed; //TODO remove PerformanceInfo_IsDefaultCollapsed

                                bool empty = true;
                                bool first = true;
                                FontFamily family = new FontFamily("Consolas");
                                string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                                var performanceElements = new List<ClassifiedTextRun>();

                                MicroArch selectedMicroarchitures = AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                                foreach (PerformanceItem item in this.asmDudeTools_.Performance_Store.GetPerformance(mnemonic, selectedMicroarchitures))
                                {
                                    empty = false;
                                    if (first)
                                    {
                                        first = false;

                                        string msg1 = string.Format(
                                            AsmDudeToolsStatic.CultureUI,
                                            format,
                                            string.Empty, string.Empty, "µOps", "µOps", "µOps", string.Empty, string.Empty, string.Empty);

                                        string msg2 = string.Format(
                                            AsmDudeToolsStatic.CultureUI,
                                            "\n" + format,
                                            "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", string.Empty);

                                        performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "\n"+msg1, ClassifiedTextRunStyle.UseClassificationFont));
                                        performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, msg2, ClassifiedTextRunStyle.UseClassificationFont));
                                    }

                                    string msg3 = string.Format(
                                        AsmDudeToolsStatic.CultureUI,
                                        "\n" + format,
                                        item.microArch_ + " ",
                                        item.instr_ + " " + item.args_ + " ",
                                        item.mu_Ops_Fused_ + " ",
                                        item.mu_Ops_Merged_ + " ",
                                        item.mu_Ops_Port_ + " ",
                                        item.latency_ + " ",
                                        item.throughput_ + " ",
                                        item.remark_);

                                    performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, msg3, ClassifiedTextRunStyle.UseClassificationFont));
                                }

                                if (!empty)
                                {
                                    x.Add(new ClassifiedTextElement(performanceElements));
                                }
                            }

                            //int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                            //instructionTooltipWindow.SetAsmSim(this.asmSimulator_, lineNumber, true);

                            var linkElement = ClassifiedTextElement.CreateHyperlink("More details", "Click here to see more details about this expression", () =>
                            {
                                System.Diagnostics.Process.Start($"https://github.com/SnellerInc/asm-dude");
                            });
                            x.Add(linkElement);

                            return (x, keywordSpan.Value);
                        }
                    case AsmTokenType.Label:
                        {
                            string label = keyword;
                            string labelPrefix = tag.Misc;
                            string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(labelPrefix, label, AsmDudeToolsStatic.Used_Assembler);

                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("Label ", foreground));
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                            if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
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
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                                ? value + "d = " + value.ToString("X", AsmDudeToolsStatic.CultureUI) + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
                                : keyword;

                            var containerElement = new ContainerElement(
                                ContainerElementStyle.Wrapped,
                                new ImageElement(_icon),
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Constant "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Number, constantStr)));

                            return (new List<object> { containerElement }, keywordSpan.Value);
                        }
                    case AsmTokenType.UserDefined1:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("User defined 1: ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined1))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined2))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined3))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                    //quickInfoContent.Add(description);
                    return (new List<object> { "other" }, keywordSpan.Value);
                }
            }
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: applicableToSpan=\"" + applicableToSpan + "\"; quickInfoContent,Count=" + quickInfoContent.Count);
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "QuickInfo");
            return (new List<object> { "final" }, keywordSpan.Value);


            /*
            var rootNode = await document.GetSyntaxRootAsync(cancellationToken);
            var node = rootNode.FindNode(TextSpan.FromBounds(position, position));

            if (!(node is SyntaxNode identifier))
            {
                return null;
            }

            LiteralExpressionSyntax literalExpressionSyntax = null;

            if (node is ArgumentSyntax argumentSyntax)
            {
                literalExpressionSyntax = argumentSyntax.Expression as LiteralExpressionSyntax;
            }
            else if (node is AttributeArgumentSyntax attributeArgumentSyntax)
            {
                literalExpressionSyntax = attributeArgumentSyntax.Expression as LiteralExpressionSyntax;
            }
            else if (node is LiteralExpressionSyntax literalExpressionSyntax1)
            {
                literalExpressionSyntax = literalExpressionSyntax1;
            }
            if (literalExpressionSyntax == null)
            {
                return null;
            }
            else if (literalExpressionSyntax.Kind() != Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
            {
                return null;
            }

            var text = literalExpressionSyntax.GetText()?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var expression = text.TrimStart('\"').TrimEnd('\"');

            string message = "TODO";

            if (string.IsNullOrWhiteSpace(message) || message.StartsWith("error: ", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var span = identifier.Span;
            return (message, span);
            */
        }


        private void Handle(IAsyncQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;
            DateTime time1 = DateTime.Now;

            ITextSnapshot snapshot = this.textBuffer_.CurrentSnapshot;
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Handle: trigger point is null", this.ToString()));
                return;
            }

            Brush foreground = AsmDudeToolsStatic.GetFontColor();

            (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this.aggregator_, triggerPoint.Value);
            if (keywordSpan.HasValue)
            {
                SnapshotSpan tagSpan = keywordSpan.Value;
                string keyword = tagSpan.GetText();
                string keyword_upcase = keyword.ToUpperInvariant();

                AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Handle: keyword=\"{1}\"; type={2}; file=\"{3}\"", this.ToString(), keyword, tag.Type, AsmDudeToolsStatic.GetFilename(session.TextView.TextBuffer)));
                applicableToSpan = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeInclusive);

                TextBlock description = null;
                switch (tag.Type)
                {
                    case AsmTokenType.Misc:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("Keyword ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Misc))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Directive))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            if (keyword_upcase.StartsWith("%", StringComparison.Ordinal))
                            {
                                keyword_upcase = keyword_upcase.Substring(1); // remove the preceding % in AT&T syntax
                            }

                            Rn reg = RegisterTools.ParseRn(keyword_upcase, true);
                            if (this.asmDudeTools_.RegisterSwitchedOn(reg))
                            {
                                RegisterTooltipWindow registerTooltipWindow = new RegisterTooltipWindow(foreground);
                                registerTooltipWindow.SetDescription(reg, this.asmDudeTools_);
                                registerTooltipWindow.SetAsmSim(this.asmSimulator_, reg, lineNumber, true);
                                quickInfoContent.Add(registerTooltipWindow);
                            }
                            break;
                        }
                    case AsmTokenType.Mnemonic: // intentional fall through
                    case AsmTokenType.MnemonicOff: // intentional fall through
                    case AsmTokenType.Jump:
                        {
                            (Mnemonic mnemonic, _) = AsmSourceTools.ParseMnemonic_Att(keyword_upcase, true);
                            InstructionTooltipWindow instructionTooltipWindow = new InstructionTooltipWindow(foreground)
                            {
                                Session = session, // set the owner of this windows such that we can manually close this window
                            };
                            instructionTooltipWindow.SetDescription(mnemonic, this.asmDudeTools_);
                            instructionTooltipWindow.SetPerformanceInfo(mnemonic, this.asmDudeTools_);
                            int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                            instructionTooltipWindow.SetAsmSim(this.asmSimulator_, lineNumber, true);
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
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                            if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
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
                            description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

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
                                ? value + "d = " + value.ToString("X", AsmDudeToolsStatic.CultureUI) + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
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

                                description.Inlines.Add(Make_Run2(constantStr, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Constant))));
                            }
                            break;
                        }
                    case AsmTokenType.UserDefined1:
                        {
                            description = new TextBlock();
                            description.Inlines.Add(Make_Run1("User defined 1: ", foreground));
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined1))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined2))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                            description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined3))));

                            string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
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
                    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
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
            if (this.labelGraph_.Enabled)
            {
                StringBuilder sb = new StringBuilder();
                SortedSet<uint> labelDefs = this.labelGraph_.Get_Label_Def_Linenumbers(label);
                if (labelDefs.Count > 1)
                {
                    sb.AppendLine(string.Empty);
                }
                foreach (uint id in labelDefs)
                {
                    int lineNumber = LabelGraph.Get_Linenumber(id);
                    string filename = Path.GetFileName(this.labelGraph_.Get_Filename(id));
                    string lineContent = (LabelGraph.Is_From_Main_File(id))
                        ? " :" + this.textBuffer_.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText()
                        : string.Empty;
                    sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format(AsmDudeToolsStatic.CultureUI, "Defined at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
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
            if (!this.labelGraph_.Enabled)
            {
                return "Label analysis is disabled";
            }

            SortedSet<uint> usage = this.labelGraph_.Label_Used_At_Info(full_Qualified_Label, label);
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
                    string filename = Path.GetFileName(this.labelGraph_.Get_Filename(id));
                    string lineContent;
                    if (LabelGraph.Is_From_Main_File(id))
                    {
                        lineContent = " :" + this.textBuffer_.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
                    }
                    else
                    {
                        lineContent = string.Empty;
                    }
                    sb.AppendLine(AsmDudeToolsStatic.Cleanup(string.Format(AsmDudeToolsStatic.CultureUI, "Used at LINE {0} ({1}){2}", lineNumber + 1, filename, lineContent)));
                    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:getLabelDefDescription; sb=\"{1}\"", this.ToString(), sb.ToString()));
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

        #region IDisposable Support

        public void Dispose()
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Dispose", this.ToString()));
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AsmQuickInfoSource()
        {
            this.Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                this.aggregator_.Dispose();
            }
            // free native resources if there are any.
        }

    }
    #endregion

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
            IsReadOnlyProp.SetValue(editor.editor_, true);
            TextViewProp.SetValue(editor.editor_, TextContainerTextViewProp.GetValue(textContainer));

            return editor;
        }

        private readonly object editor_;

        public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled)
        {
            this.editor_ = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
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

        private readonly TextEditorWrapper editor_;

        public SelectableTextBlock()
        {
            this.editor_ = TextEditorWrapper.CreateFor(this);
        }
    }
}