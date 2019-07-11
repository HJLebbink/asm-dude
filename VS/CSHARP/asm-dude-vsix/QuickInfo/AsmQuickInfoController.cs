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
using AsmTools;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AsmDude.QuickInfo
{
    internal sealed class AsmQuickInfoController : IIntellisenseController
    {
        private readonly IList<ITextBuffer> _subjectBuffers;
        private readonly IQuickInfoBroker _quickInfoBroker; //XYZZY OLD
        //private readonly IAsyncQuickInfoBroker _quickInfoBroker; //XYZZY NEW
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        //private IQuickInfoSession _session;
        private ITextView _textView;

        private Window _legacyTooltipWindow;
        private Span _legacySpan;

        internal AsmQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            IQuickInfoBroker quickInfoBroker,
            IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor; file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(textView.TextBuffer)));
            this._textView = textView ?? throw new ArgumentNullException(nameof(textView));
            this._subjectBuffers = subjectBuffers ?? throw new ArgumentNullException(nameof(subjectBuffers));
            this._quickInfoBroker = quickInfoBroker ?? throw new ArgumentNullException(nameof(quickInfoBroker));
            this._aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(textView.TextBuffer, aggregatorFactory);
            this._textView.MouseHover += this.OnTextViewMouseHover;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ConnectSubjectBuffer", this.ToString()));
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:DisconnectSubjectBuffer", this.ToString()));
        }

        public void Detach(ITextView textView)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Detach", this.ToString()));
            if (this._textView == textView)
            {
                this._textView.MouseHover -= this.OnTextViewMouseHover;
                this._textView = null;
            }
        }

        /*
        /// <summary>
        /// Determine if the mouse is hovering over a token. If so, display QuickInfo
        /// </summary>
        private void OnTextViewMouseHover_OLD(object sender, MouseHoverEventArgs e)
        {
            try
            {
                string contentType = this._textView.TextBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for regular view. file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                    SnapshotPoint? point = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                    if (point.HasValue)
                    {
                        int pos = point.Value.Position;
                        int pos2 = this.Get_Keyword_Span_At_Point(point.Value).Start;

                        //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: CreateQuickInfoSession for triggerPoint " + pos + "; pos2=" + pos2);
                        //ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos, PointTrackingMode.Positive);
                        ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos2, PointTrackingMode.Positive);

                        if (this._session == null)
                        {
                            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: A: session was null, create a new session for triggerPoint {1}; pos2={2}", this.ToString(), pos, pos2));
                            this._session = this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                            if (this._session != null)
                            {
                                this._session.Dismissed += this.Session_Dismissed;
                                //this._session.ApplicableToSpanChanged += (o, i) => { AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:ApplicableToSpanChanged Event"); };
                                //this._session.PresenterChanged += (o, i) => { AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PresenterChanged Event"); };
                                //this._session.Recalculated += (o, i) => { AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:Recalculated Event"); };
                            }
                        }
                        else
                        {
                            if (this._session.IsDismissed)
                            {
                                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: B: session was not null but was dismissed, create a new session  for triggerPoint {1}; pos2={2}", this.ToString(), pos, pos2));

                                this._session = this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                                if (this._session != null)
                                {
                                    this._session.Dismissed += this.Session_Dismissed;
                                }
                            }
                            else
                            {
                                if (this._session.ApplicableToSpan.GetSpan(this._textView.TextSnapshot).IntersectsWith(new Span(point.Value.Position, 0)))
                                {
                                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: C: session was not dismissed: intersects!", this.ToString()));
                                }
                                else
                                {
                                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: D: session  was not dismissed but we need a new session for triggerPoint {1}; pos2={2}", this.ToString(), pos, pos2));

                                    //if (this._session != null) this._session.Dismiss();
                                    this._session = this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                                    if (this._session != null)
                                    {
                                        this._session.Dismissed += this.Session_Dismissed;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for disassembly view. file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                    SnapshotPoint? triggerPoint = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                    if (!triggerPoint.HasValue)
                    {
                        AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: trigger point is null", this.ToString()));
                    }
                    else
                    {
                        System.Drawing.Point p = System.Windows.Forms.Control.MousePosition;
                        this.ToolTipLegacy(triggerPoint.Value, new Point(p.X, p.Y));
                    }
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                }
            }
            catch (Exception e2)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: exception={1}", this.ToString(), e2.Message));
            }
        }
        */

        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            try
            {
                string contentType = this._textView.TextBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for regular view. file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                    SnapshotPoint? point = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                    if (point.HasValue)
                    {
                        //int pos = point.Value.Position;
                        int pos = this.Get_Keyword_Span_At_Point(point.Value).Start;

                        ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos, PointTrackingMode.Positive);

                        if (this._quickInfoBroker.IsQuickInfoActive(this._textView))
                        {
                            //IAsyncQuickInfoSession current_Session = this._quickInfoBroker.GetSession(this._textView); //XYZZY NEW
                            IQuickInfoSession current_Session = this._quickInfoBroker.GetSessions(this._textView)[0]; //XYZZY OLD

                            var span = current_Session.ApplicableToSpan;
                            if ((span != null) && span.GetSpan(this._textView.TextSnapshot).IntersectsWith(new Span(pos, 0)))
                            {
                                AsmDudeToolsStatic.Output_INFO(string.Format("{0}::OnTextViewMouseHover: A: quickInfoBroker is already active: intersects!", this.ToString()));
                            }
                            else
                            {
                                AsmDudeToolsStatic.Output_INFO("QuickInfoController:OnTextViewMouseHover: B: quickInfoBroker is already active, but we need a new session at " + pos);
                                //_ = current_Session.DismissAsync(); //XYZZY NEW
                                //_ = this._quickInfoBroker.TriggerQuickInfoAsync(this._textView, triggerPoint, QuickInfoSessionOptions.None); //BUG here QuickInfoSessionOptions.None behaves as TrackMouse  //XYZZY NEW
                                current_Session.Dismiss(); //XYZZY OLD
                                this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);  //XYZZY OLD
                            }
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_INFO(string.Format("{0}::OnTextViewMouseHover: C: quickInfoBroker was not active, create a new session for triggerPoint {1}", this.ToString(), pos));
                            //_ = this._quickInfoBroker.TriggerQuickInfoAsync(this._textView, triggerPoint, QuickInfoSessionOptions.None); //XYZZY NEW
                            this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);  //XYZZY OLD
                        }
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: point has not value", this.ToString()));
                    }
                }
                else if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for disassembly view. file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                    SnapshotPoint? triggerPoint = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                    if (!triggerPoint.HasValue)
                    {
                        AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: trigger point is null", this.ToString()));
                    }
                    else
                    {
                        System.Drawing.Point p = System.Windows.Forms.Control.MousePosition;
                        this.ToolTipLegacy(triggerPoint.Value, new Point(p.X, p.Y));
                    }
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                }
            }
            catch (Exception e2)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: exception={1}", this.ToString(), e2.Message));
            }
        }

        //private void Session_Dismissed(object sender, EventArgs e)
        //{
        //    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Session_Dismissed: event={1}", this.ToString(), e));
        //    this._session = null;
        //}

        /// <summary>
        /// Get mouse location on screen. Used to determine what word the cursor is currently hovering over.
        /// </summary>
        private SnapshotPoint? GetMousePosition(SnapshotPoint topPosition)
        {
            // Map this point down to the appropriate subject buffer.

            return this._textView.BufferGraph.MapDownToFirstMatch(
                topPosition,
                PointTrackingMode.Positive,
                snapshot => this._subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor
            );
        }

        private Span Get_Keyword_Span_At_Point(SnapshotPoint triggerPoint)
        {
            ITextSnapshotLine line = triggerPoint.GetContainingLine();

            //1] find the start of the current keyword
            SnapshotPoint start = triggerPoint;
            while ((start > line.Start) && !AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
            {
                start -= 1;
            }
            //2] find the end of the current keyword
            SnapshotPoint end = triggerPoint;
            while (((end + 1) < line.End) && !AsmSourceTools.IsSeparatorChar((end + 1).GetChar()))
            {
                end += 1;
            }
            //3] get the word under the mouse
            return new SnapshotSpan(start, end + 1);
        }

        public void CloseToolTip()
        {
            this._legacyTooltipWindow?.Close();
            //this._session?.Dismiss();
            //this._session = null;
        }

        private void ToolTipLegacy(SnapshotPoint triggerPoint, Point p)
        {
            DateTime time1 = DateTime.Now;
            ITextSnapshot snapshot = this._textView.TextSnapshot;

            (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this._aggregator, triggerPoint);
            if (keywordSpan.HasValue)
            {
                SnapshotSpan tagSpan = keywordSpan.Value;
                string keyword = tagSpan.GetText();
                string keywordUpper = keyword.ToUpper();

                //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ToolTipLegacy: keyword=\"{1}\"; type={2}; file=\"{3}\"", this.ToString(), keyword, tag.Type, AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
                ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeInclusive);

                // check if a tooltip window is already visible for the applicable span
                if ((this._legacySpan != null) && this._legacySpan.OverlapsWith(tagSpan))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ToolTipLegacy: tooltip is already visible. span = {1}, content = {2}", this.ToString(), this._legacySpan.ToString(), applicableTo.GetText(this._textView.TextSnapshot)));
                    return;
                }

                switch (tag.Type)
                {
                    case AsmTokenType.Mnemonic: // intentional fall through
                    case AsmTokenType.Jump:
                        {
                            (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(keywordUpper, true);

                            InstructionTooltipWindow instructionTooltipWindow = new InstructionTooltipWindow(AsmDudeToolsStatic.GetFontColor())
                            {
                                Owner = this // set the owner of this windows such that we can manually close this window
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
                                Child = instructionTooltipWindow
                            };

                            // cleanup old window remnants
                            if (this._legacyTooltipWindow != null)
                            {
                                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ToolTipLegacy: going to cleanup old window remnants.", this.ToString()));
                                if (this._legacyTooltipWindow.IsLoaded)
                                {
                                    this._legacyTooltipWindow = null;
                                }
                                else
                                {
                                    this._legacyTooltipWindow?.Close();
                                }
                            }

                            this._legacyTooltipWindow = new Window
                            {
                                WindowStyle = WindowStyle.None,
                                ResizeMode = ResizeMode.NoResize,
                                SizeToContent = SizeToContent.WidthAndHeight,
                                ShowInTaskbar = false,
                                Left = p.X + 15, // placement slightly to the right
                                Top = p.Y + 5, // placement slightly lower such that the code that is selected is visible
                                //TODO find the space to the left and if not enough space is available, place the window more to the left
                                Content = border
                            };
                            this._legacyTooltipWindow.LostKeyboardFocus += (o, i) =>
                            {
                                //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:LostKeyboardFocus: going to close the tooltip window.");
                                try
                                {
                                    (o as Window).Close();
                                }
                                catch (Exception e)
                                {
                                    AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:ToolTipLegacy: e={1}", this.ToString(), e.Message));
                                }
                            };
                            this._legacySpan = tagSpan;
                            this._legacyTooltipWindow.Show();
                            this._legacyTooltipWindow.Focus(); //give the tooltip window focus, such that we can use the lostKeyboardFocus event to close this window;
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
