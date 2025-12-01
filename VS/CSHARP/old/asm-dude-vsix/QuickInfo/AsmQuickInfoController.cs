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
    using System.Windows;
    using System.Windows.Controls;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class AsmQuickInfoController : IIntellisenseController
    {
        private readonly IList<ITextBuffer> subjectBuffers_;
        private readonly IAsyncQuickInfoBroker quickInfoBroker_;
        private readonly ITagAggregator<AsmTokenTag> aggregator_;
        private ITextView textView_;

        private Window legacyTooltipWindow_;
        private Span legacySpan_;

        internal AsmQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            IAsyncQuickInfoBroker quickInfoBroker,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:constructor; file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(textView.TextBuffer)));
            this.textView_ = textView ?? throw new ArgumentNullException(nameof(textView));
            this.subjectBuffers_ = subjectBuffers ?? throw new ArgumentNullException(nameof(subjectBuffers));
            this.quickInfoBroker_ = quickInfoBroker ?? throw new ArgumentNullException(nameof(quickInfoBroker));
            this.aggregator_ = AsmDudeToolsStatic.GetOrCreate_Aggregator(textView.TextBuffer, aggregatorFactory);
            this.textView_.MouseHover += this.OnTextViewMouseHover;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ConnectSubjectBuffer", this.ToString()));
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:DisconnectSubjectBuffer", this.ToString()));
        }

        public void Detach(ITextView textView)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Detach", this.ToString()));
            if (this.textView_ == textView)
            {
                this.textView_.MouseHover -= this.OnTextViewMouseHover;
                this.textView_ = null;
            }
        }

        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            try
            {
                string contentType = this.textView_.TextBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:OnTextViewMouseHover: Quickinfo for disassembly view. file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this.textView_.TextBuffer)));
                    SnapshotPoint? triggerPoint = this.GetMousePosition(new SnapshotPoint(this.textView_.TextSnapshot, e.Position));
                    if (!triggerPoint.HasValue)
                    {
                        AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:OnTextViewMouseHover: trigger point is null", this.ToString()));
                    }
                    else
                    {
                        System.Drawing.Point p = System.Windows.Forms.Control.MousePosition;
                        this.ToolTipLegacy(triggerPoint.Value, new Point(p.X, p.Y));
                    }
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:OnTextViewMouseHover: does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                }
            }
            catch (Exception e2)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:OnTextViewMouseHover: exception={1}", this.ToString(), e2.Message));
            }
        }

        //private void Session_Dismissed(object sender, EventArgs e)
        //{
        //    AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Session_Dismissed: event={1}", this.ToString(), e));
        //    this._session = null;
        //}

        /// <summary>
        /// Get mouse location on screen. Used to determine what word the cursor is currently hovering over.
        /// </summary>
        private SnapshotPoint? GetMousePosition(SnapshotPoint topPosition)
        {
            // Map this point down to the appropriate subject buffer.

            return this.textView_.BufferGraph.MapDownToFirstMatch(
                topPosition,
                PointTrackingMode.Positive,
                snapshot => this.subjectBuffers_.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);
        }

        public void CloseToolTip()
        {
            this.legacyTooltipWindow_?.Close();
        }

        private void ToolTipLegacy(SnapshotPoint triggerPoint, Point p)
        {
            DateTime time1 = DateTime.Now;
            ITextSnapshot snapshot = this.textView_.TextSnapshot;

            (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this.aggregator_, triggerPoint);
            if (keywordSpan.HasValue)
            {
                SnapshotSpan tagSpan = keywordSpan.Value;
                string keyword = tagSpan.GetText();
                string keyword_upcase = keyword.ToUpperInvariant();

                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ToolTipLegacy: keyword=\"{1}\"; type={2}; file=\"{3}\"", this.ToString(), keyword, tag.Type, AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeInclusive);

                // check if a tooltip window is already visible for the applicable span
                if ((this.legacySpan_ != null) && this.legacySpan_.OverlapsWith(tagSpan))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ToolTipLegacy: tooltip is already visible. span = {1}, content = {2}", this.ToString(), this.legacySpan_.ToString(), applicableTo.GetText(this.textView_.TextSnapshot)));
                    return;
                }

                switch (tag.Type)
                {
                    case AsmTokenType.Mnemonic: // intentional fall through
                    case AsmTokenType.Jump:
                        {
                            (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(keyword_upcase, true);

                            InstructionTooltipWindow instructionTooltipWindow = new InstructionTooltipWindow(AsmDudeToolsStatic.GetFontColor())
                            {
                                Owner = this, // set the owner of this windows such that we can manually close this window
                            };
                            instructionTooltipWindow.SetDescription(mnemonic, AsmDudeTools.Instance);
                            instructionTooltipWindow.SetPerformanceInfo(mnemonic, AsmDudeTools.Instance);
                            instructionTooltipWindow.Margin = new Thickness(7.0);

                            Border border = new Border()
                            {
                                BorderBrush = System.Windows.Media.Brushes.LightGray,
                                BorderThickness = new Thickness(1.0),
                                CornerRadius = new CornerRadius(2.0),
                                Background = AsmDudeToolsStatic.GetBackgroundColor(),
                                Child = instructionTooltipWindow,
                            };

                            // cleanup old window remnants
                            if (this.legacyTooltipWindow_ != null)
                            {
                                AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ToolTipLegacy: going to cleanup old window remnants.", this.ToString()));
                                if (this.legacyTooltipWindow_.IsLoaded)
                                {
                                    this.legacyTooltipWindow_ = null;
                                }
                                else
                                {
                                    this.legacyTooltipWindow_?.Close();
                                }
                            }

                            this.legacyTooltipWindow_ = new Window
                            {
                                WindowStyle = WindowStyle.None,
                                ResizeMode = ResizeMode.NoResize,
                                SizeToContent = SizeToContent.WidthAndHeight,
                                ShowInTaskbar = false,
                                Left = p.X + 15, // placement slightly to the right
                                Top = p.Y + 5, // placement slightly lower such that the code that is selected is visible
                                //TODO find the space to the left and if not enough space is available, place the window more to the left
                                Content = border,
                            };
                            this.legacyTooltipWindow_.LostKeyboardFocus += (o, i) =>
                            {
                                //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:LostKeyboardFocus: going to close the tooltip window.");
                                try
                                {
                                    (o as Window).Close();
                                }
                                catch (Exception e)
                                {
                                    AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ToolTipLegacy: e={1}", this.ToString(), e.Message));
                                }
                            };
                            this.legacySpan_ = tagSpan;
                            this.legacyTooltipWindow_.Show();
                            this.legacyTooltipWindow_.Focus(); //give the tooltip window focus, such that we can use the lostKeyboardFocus event to close this window;
                            break;
                        }
                    default: break;
                }
                //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: applicableToSpan=\"" + applicableToSpan + "\"; quickInfoContent,Count=" + quickInfoContent.Count);
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "QuickInfo ToolTipLegacy");
            }
        }
    }
}
