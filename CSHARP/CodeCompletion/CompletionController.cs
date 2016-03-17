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

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.Diagnostics;
using System.Globalization;

namespace AsmDude {

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("asm!")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class VsTextViewCreationListener : IVsTextViewCreationListener {

        [Import]
        IVsEditorAdaptersFactoryService _adaptersFactory = null;

        [Import]
        ICompletionBroker _completionBroker = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter) {
            IWpfTextView view = _adaptersFactory.GetWpfTextView(textViewAdapter);
            Debug.Assert(view != null);
            AsmCommandFilter filter = new AsmCommandFilter(view, _completionBroker);
            IOleCommandTarget next;
            textViewAdapter.AddCommandFilter(filter, out next);
            filter._m_nextCommandHandler = next;
        }
    }

    internal sealed class AsmCommandFilter : IOleCommandTarget {
        private ICompletionSession _m_session;

        public AsmCommandFilter(IWpfTextView textView, ICompletionBroker broker) {
            this._m_session = null;
            this._m_textView = textView;
            this._broker = broker;
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:constructor", this.ToString()));
        }

        public IOleCommandTarget _m_nextCommandHandler { get; set; }
        public IWpfTextView _m_textView { get; private set; }
        public ICompletionBroker _broker { get; private set; }

        private char GetTypeChar(IntPtr pvaIn) {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec", this.ToString()));

            /*
            if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider)) {
                return m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            */

            //make a copy of this so we can look at it after forwarding some commands
            uint commandID = nCmdID;
            char typedChar = char.MinValue;
            //make sure the input is a char before getting it
            if ((pguidCmdGroup == VSConstants.VSStd2K) && (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)) {
                typedChar = GetTypeChar(pvaIn);
            }
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec: typedChar={1}", this.ToString(), typedChar));

            //check for a commit character
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
                char.IsWhiteSpace(typedChar) ||
                char.IsPunctuation(typedChar)) {

                //check for a selection
                if (this._m_session != null && !this._m_session.IsDismissed) {
                    //if the selection is fully selected, commit the current session
                    if (this._m_session.SelectedCompletionSet.SelectionStatus.IsSelected) {
                        this._m_session.Commit();

                        //pass along the command so the char is added to the buffer, except if the command is an enter
                        if (nCmdID != (uint)VSConstants.VSStd2KCmdID.RETURN) {
                            this._m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        }
                        return VSConstants.S_OK;
                    } else { //if there is no selection, dismiss the session
                        this._m_session.Dismiss();
                    }
                }
            }
            //pass along the command so the char is added to the buffer
            int retVal = this._m_nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;
            if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar)) {
                if (this._m_session == null || this._m_session.IsDismissed) { // If there is no active session, bring up completion
                    if (this.TriggerCompletion()) {
                        if (this._m_session != null) {
                            this._m_session.Filter();
                        }
                    }
                } else {   //the completion session is already active, so just filter
                    this._m_session.Filter();
                }
                handled = true;
            } else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE   //redo the filter if there is a deletion
                    || commandID == (uint)VSConstants.VSStd2KCmdID.DELETE) {
                if (this._m_session != null && !this._m_session.IsDismissed) {
                    this._m_session.Filter();
                }
                handled = true;
            }
            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        private bool TriggerCompletion() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:TriggerCompletion", this.ToString()));

            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint = this._m_textView.Caret.Position.Point.GetPoint(
                textBuffer => (!textBuffer.ContentType.IsOfType("projection")), PositionAffinity.Predecessor);
            if (!caretPoint.HasValue) {
                return false;
            }

            this._m_session = this._broker.CreateCompletionSession(
                this._m_textView,
                caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                true);

            //subscribe to the Dismissed event on the session 
            this._m_session.Dismissed += this.OnSessionDismissed;
            this._m_session.Start();

            return true;
        }

        private void OnSessionDismissed(object sender, EventArgs e) {
            this._m_session.Dismissed -= this.OnSessionDismissed;
            this._m_session = null;
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter() {
            if (this._m_session == null) {
                return;
            }
            this._m_session.SelectedCompletionSet.SelectBestMatch();
            this._m_session.SelectedCompletionSet.Recalculate();
        }
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:QueryStatus", this.ToString()));
            if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID) {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:

                        Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:QueryStatus", this.ToString()));

                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return _m_nextCommandHandler.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}