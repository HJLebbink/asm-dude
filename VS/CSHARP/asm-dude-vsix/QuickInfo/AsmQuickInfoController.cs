// The MIT License (MIT)
//
// Copyright (c) 2018 Henk-Jan Lebbink
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
using System.Windows.Controls;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using AsmDude.Tools;
using AsmTools;
using System.Windows;

namespace AsmDude.QuickInfo
{
    internal sealed class AsmQuickInfoController : IIntellisenseController
    {
        private readonly IList<ITextBuffer> _subjectBuffers;
        private readonly IQuickInfoBroker _quickInfoBroker;
        private IQuickInfoSession _session;
        private ITextView _textView;

        private Window _legacyTooltipWindow;
        private Span _legacySpan;

        internal AsmQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            IQuickInfoBroker quickInfoBroker)
        {
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:constructor: file=" + AsmDudeToolsStatic.GetFileName(textView.TextBuffer));
            this._textView = textView;
            this._subjectBuffers = subjectBuffers;
            this._quickInfoBroker = quickInfoBroker;
            this._textView.MouseHover += this.OnTextViewMouseHover;
            /*this._textView.MouseHover += (o, e) => {
                SnapshotPoint? point = GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                if (point.HasValue)
                {
                    ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
                    if (!this._quickInfoBroker.IsQuickInfoActive(this._textView))
                    {
                        this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                    }
                }
            };
            */
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

        /// <summary>
        /// Determine if the mouse is hovering over a token. If so, display QuickInfo
        /// </summary>
        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(this._textView.TextBuffer)));
            try
            {
                SnapshotPoint? point = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
                if (point.HasValue)
                {
                    string contentType = this._textView.TextBuffer.ContentType.DisplayName;
                    if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
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
                                this._session.Dismissed += this._session_Dismissed;
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
                                if (this._session != null) this._session.Dismissed += this._session_Dismissed;
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
                                    if (this._session != null) this._session.Dismissed += this._session_Dismissed;
                                }
                            }
                        }
                    }
                    else if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                    {
                        //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for disassembly view", ToString()));
                        System.Drawing.Point p = System.Windows.Forms.Control.MousePosition;
                        this.ToolTipLegacy(point.Value, new Point(p.X, p.Y));
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: does not have have AsmDudeContentType: but has type {1}", this.ToString(), contentType));
                    }
                }
                else
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: point is null; file=" + AsmDudeToolsStatic.GetFileName(this._textView.TextBuffer));
                }
            }
            catch (Exception e2)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: exception={1}", this.ToString(), e2.Message));
            }
        }

        private void _session_Dismissed(object sender, EventArgs e)
        {
            this._session = null;
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:_session_Dismissed: event={1}", this.ToString(), e));
        }

        /// <summary>
        /// get mouse location on screen. Used to determine what word the cursor is currently hovering over
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
            this._session?.Dismiss();
            this._session = null;
        }

        private void ToolTipLegacy(SnapshotPoint triggerPoint, Point p)
        {
            var span = this.Get_Keyword_Span_At_Point(triggerPoint);
            ITrackingSpan applicableTo = this._textView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

            // check if a tooltip window is already visible for the applicable span
            if ((this._legacySpan != null) && this._legacySpan.OverlapsWith(span))
            {
                //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:ToolTipLegacy: tooltip is already visible. span = " + this._legacySpan.ToString() + ", content =" + applicableTo.GetText(this._textView.TextSnapshot));
                return;
            }

            string keyword = applicableTo.GetText(this._textView.TextSnapshot);

            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword, false);
            if (mnemonic != Mnemonic.NONE)
            {
                var instructionTooltipWindow = new InstructionTooltipWindow(AsmDudeToolsStatic.GetFontColor())
                {
                    Owner = this // set the owner of this windows such that we can manually close this window
                };
                instructionTooltipWindow.SetDescription(mnemonic, AsmDudeTools.Instance);
                instructionTooltipWindow.SetPerformanceInfo(mnemonic, AsmDudeTools.Instance);
                instructionTooltipWindow.Margin = new Thickness(7.0);

                var border = new Border()
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
                    AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:ToolTipLegacy: going to cleanup old window remnants.");
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
                    Left = p.X,
                    Top = p.Y,
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
                        AsmDudeToolsStatic.Output_WARNING("AsmQuickInfoController:LostKeyboardFocus: e=" + e.Message);
                    }
                };
                this._legacySpan = span;
                this._legacyTooltipWindow.Show();
                this._legacyTooltipWindow.Focus(); //give the tooltip window focus, such that we can use the lostKeyboardFocus event to close this window;
            }
        }
    }
}
