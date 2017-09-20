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
using System.Windows.Controls;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Adornments;

using AsmDude.Tools;
using AsmTools;
using System.Windows;

namespace AsmDude.QuickInfo
{
    internal sealed class AsmQuickInfoController : IIntellisenseController
    {
        private readonly IList<ITextBuffer> _subjectBuffers;
        private readonly IQuickInfoBroker _quickInfoBroker;
        private readonly IToolTipProvider _toolTipProvider;
        private IQuickInfoSession _session;
        private ITextView _textView;

        internal AsmQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            IQuickInfoBroker quickInfoBroker,
            IToolTipProvider toolTipProvider)
        {
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:constructor: file=" + AsmDudeToolsStatic.GetFileName(textView.TextBuffer));
            this._textView = textView;
            this._subjectBuffers = subjectBuffers;
            this._quickInfoBroker = quickInfoBroker;
            this._toolTipProvider = toolTipProvider;
            this._textView.MouseHover += this.OnTextViewMouseHover;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:ConnectSubjectBuffer");
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:DisconnectSubjectBuffer");
        }

        public void Detach(ITextView textView)
        {
            AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:Detach");
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
            //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: file=" + AsmDudeToolsStatic.GetFileName(this._textView.TextBuffer));
            SnapshotPoint? point = GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
            if (point.HasValue)
            {
                string contentType = this._textView.TextBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    int pos = point.Value.Position;
                    int pos2 = Get_Keyword_Span_At_Point(point.Value).Start;

                    AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: CreateQuickInfoSession for triggerPoint " + pos + "; pos2="+pos2);
                    //ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos, PointTrackingMode.Positive);
                    ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos2, PointTrackingMode.Positive);

                    if (!this._quickInfoBroker.IsQuickInfoActive(this._textView))
                    {

                        if ((this._session != null) && (this._session.ApplicableToSpan != null))
                        {
                            if (this._session.ApplicableToSpan.GetSpan(this._textView.TextSnapshot).IntersectsWith(new Span(point.Value.Position, 0)))
                            {
                                AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: intersects!");
                            }
                            else
                            {
                                AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: collapsing an existing session");
                                this._session.Collapse();
                                this._session = this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                            }
                        }
                        else
                        {
                            this._session = this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);
                        }
                    }
                }
                else if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for disassembly view", ToString()));
                    this.ToolTipLegacy(point.Value);
                }
                else
                {
                    AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:OnTextViewMouseHover: does not have have AsmDudeContentType: but has type {1}", ToString(), contentType));
                }
            }
            else
            {
                //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: point is null; file=" + AsmDudeToolsStatic.GetFileName(this._textView.TextBuffer));
            }
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
            while ((start > line.Start) && !AsmTools.AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
            {
                start -= 1;
            }
            //2] find the end of the current keyword
            SnapshotPoint end = triggerPoint;
            while (((end + 1) < line.End) && !AsmTools.AsmSourceTools.IsSeparatorChar((end + 1).GetChar()))
            {
                end += 1;
            }
            //3] get the word under the mouse
            return new SnapshotSpan(start, end + 1);
        }

        private void ToolTipLegacy(SnapshotPoint triggerPoint)
        {
            var span = Get_Keyword_Span_At_Point(triggerPoint);
            ITrackingSpan applicableTo = this._textView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

            string keyword = applicableTo.GetText(this._textView.TextSnapshot);
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: keyword={1}", ToString(), keyword));

            #region Create a window
            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword, false);
            if (mnemonic != Mnemonic.NONE)
            {
                var instructionTooltipWindow = new InstructionTooltipWindow(AsmDudeToolsStatic.Get_Font_Color());
                instructionTooltipWindow.SetDescription(mnemonic, AsmDudeTools.Instance);
                instructionTooltipWindow.SetPerformanceInfo(mnemonic, AsmDudeTools.Instance, true);
                instructionTooltipWindow.Margin = new Thickness(7.0);

                var border = new Border()
                {
                    BorderBrush = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(1.0),
                    CornerRadius = new CornerRadius(2.0),
                    Background = AsmDudeToolsStatic.Get_Background_Color(),
                    Child = instructionTooltipWindow
                };

                var trackingSpan = this._textView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
                //this._toolTipProvider.ShowToolTip(trackingSpan, border, PopupStyles.DismissOnMouseLeaveTextOrContent);
                this._toolTipProvider.ShowToolTip(trackingSpan, border, PopupStyles.PositionClosest);
            }
            #endregion
        }
    }
}