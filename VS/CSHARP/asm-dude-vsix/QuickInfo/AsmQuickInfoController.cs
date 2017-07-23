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

using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using AsmDude.Tools;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using AsmTools;
using System.Windows.Controls;

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

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) { }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) { }

        public void Detach(ITextView textView)
        {
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
            if (point != null)
            {
                ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
                // Find the broker for this buffer

                string contentType = this._textView.TextBuffer.ContentType.DisplayName;
                if (contentType.Equals(AsmDudePackage.AsmDudeContentType, StringComparison.Ordinal))
                {
                    if (!this._quickInfoBroker.IsQuickInfoActive(this._textView))
                    {
                        //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: CreateQuickInfoSession for triggerPoint "+triggerPoint.TextBuffer+"; file=" + AsmDudeToolsStatic.GetFileName(this._textView.TextBuffer));
                        this._session = this._quickInfoBroker.CreateQuickInfoSession(this._textView, triggerPoint, false);
                        this._session.Start();
                    }
                    else
                    {
                        //AsmDudeToolsStatic.Output_INFO("AsmQuickInfoController:OnTextViewMouseHover: quickInfoBroker is already active; file=" + AsmDudeToolsStatic.GetFileName(this._textView.TextBuffer));
                    }
                }
                else if (contentType.Equals(AsmDudePackage.DisassemblyContentType, StringComparison.Ordinal))
                {
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: Quickinfo for disassembly view", ToString()));
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

        private void ToolTipLegacy(SnapshotPoint triggerPoint)
        {
            ITextSnapshotLine line = triggerPoint.GetContainingLine();

            #region Find Keyword under the Mouse
            //1] find the start of the current keyword
            SnapshotPoint start = triggerPoint;
            while ((start > line.Start) && !AsmTools.AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
            {
                start -= 1;
            }
            //2] find the end of the current keyword
            SnapshotPoint end = triggerPoint;
            while ((end < line.End) && !AsmTools.AsmSourceTools.IsSeparatorChar((end + 1).GetChar()))
            {
                end += 1;
            }
            //3] get the word under the mouse
            ITextSnapshot snapshot = this._textView.TextSnapshot;
            Span span = new SnapshotSpan(start, end + 1);
            ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
            string keyword = applicableTo.GetText(snapshot);
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: keyword={1}", ToString(), keyword));
            #endregion

            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword, false);
            if (mnemonic != Mnemonic.NONE)
            {
                var trackingSpan = this._textView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

                if (false)
                {   // use string 
                    string message = AsmQuickInfoSource.Render_Mnemonic_ToolTip(mnemonic, AsmDudeTools.Instance);
                    this._toolTipProvider.ShowToolTip(trackingSpan, message, PopupStyles.DismissOnMouseLeaveTextOrContent);
                }
                else
                {   // create a WPF view
                    var foreground = AsmDudeToolsStatic.GetFontColor();

                    if (false)
                    {
                        var description = new TextBlock();
                        AsmQuickInfoSource.Render_Mnemonic_ToolTip(description, mnemonic, foreground, AsmDudeTools.Instance);
                        this._toolTipProvider.ShowToolTip(trackingSpan, description, PopupStyles.DismissOnMouseLeaveTextOrContent);
                    }
                    else
                    {
                        DisassemblyMnemonicTooltip wpfView = new DisassemblyMnemonicTooltip();
                        AsmQuickInfoSource.Render_Mnemonic_ToolTip(wpfView.content, mnemonic, foreground, AsmDudeTools.Instance);
                        this._toolTipProvider.ShowToolTip(trackingSpan, wpfView, PopupStyles.DismissOnMouseLeaveTextOrContent);
                    }
                }
            }
        }
    }
}