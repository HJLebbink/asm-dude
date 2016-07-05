// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

using AsmDude.Tools;
using AsmTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AsmDude.SignatureHelp {

    internal sealed class AsmSignatureHelpCommandHandler : IOleCommandTarget {
        private readonly ITextView _textView;
        private readonly ISignatureHelpBroker _broker;
        private readonly ITextStructureNavigator _navigator;

        private ISignatureHelpSession _session;
        private IOleCommandTarget _nextCommandHandler;

        internal AsmSignatureHelpCommandHandler(IVsTextView textViewAdapter, ITextView textView, ITextStructureNavigator nav, ISignatureHelpBroker broker) {
            this._textView = textView;
            this._broker = broker;
            this._navigator = nav;

            //add this to the filter chain
            textViewAdapter.AddCommandFilter(this, out _nextCommandHandler);
        }

        private char GetTypeChar(IntPtr pvaIn) {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            char typedChar = char.MinValue;

            bool enterPressed = (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN);

            if ((pguidCmdGroup == VSConstants.VSStd2K) && (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)) {
                typedChar = this.GetTypeChar(pvaIn);

                if (char.IsWhiteSpace(typedChar)) {

                    if (true) {
                        SnapshotPoint point = _textView.Caret.Position.BufferPosition - 1;
                        string lineStr = point.Snapshot.GetLineFromPosition(point.Position).GetText();
                        var t = AsmSourceTools.parseLine(lineStr);
                        switch (t.Item2) {
                            case Mnemonic.ADD:
                            case Mnemonic.AND:
                                if (this._session != null) this._session.Dismiss(); // cleanup previous session
                                this._session = _broker.TriggerSignatureHelp(_textView);
                                break;
                        }
                    } else {
                        //move the point back so it's in the preceding word
                        SnapshotPoint point = _textView.Caret.Position.BufferPosition - 1;
                        TextExtent extent = _navigator.GetExtentOfWord(point);
                        string previousWord = extent.Span.GetText().ToUpper();

                        if (previousWord.Equals("ADD")) {
                            //if (this._session != null) this._session.Dismiss(); // cleanup previous session
                            this._session = _broker.TriggerSignatureHelp(_textView);
                        } else if (previousWord.Equals("AND")) {
                            if (this._session != null) this._session.Dismiss(); // cleanup previous session
                            this._session = _broker.TriggerSignatureHelp(_textView);
                        }
                    }
                } else if (AsmSourceTools.isRemarkChar(typedChar) && (this._session != null)) {
                    this._session.Dismiss();
                    this._session = null;
                }
            } else if (enterPressed && (this._session != null))  {
                this._session.Dismiss();
                this._session = null;
            }
            return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            return _nextCommandHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}
