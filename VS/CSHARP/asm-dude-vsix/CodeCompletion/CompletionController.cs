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

namespace AsmDude {

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class VsTextViewCreationListener : IVsTextViewCreationListener {

        [Import]
        private IVsEditorAdaptersFactoryService _adaptersFactory = null;

        [Import]
        private ICompletionBroker _completionBroker = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter) {
            IWpfTextView view = _adaptersFactory.GetWpfTextView(textViewAdapter);
            Debug.Assert(view != null);
            AsmCommandFilter filter = new AsmCommandFilter(view, _completionBroker);
            IOleCommandTarget next;
            textViewAdapter.AddCommandFilter(filter, out next);
            filter._nextCommandHandler = next;
        }
    }

    internal sealed class AsmCommandFilter : IOleCommandTarget {
        private ICompletionSession _currentSession;

        public AsmCommandFilter(IWpfTextView textView, ICompletionBroker broker) {
            this._currentSession = null;
            this._textView = textView;
            this._broker = broker;
            //Debug.WriteLine(string.Format("INFO: {0}:constructor", this.ToString()));
        }

        public IWpfTextView _textView { get; private set; }
        public ICompletionBroker _broker { get; private set; }
        public IOleCommandTarget _nextCommandHandler { get; set; }

        private char GetTypeChar(IntPtr pvaIn) {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {

            //if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider)) {
            //    return _nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            //}

            if (false) {
                return ExecMethod1(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            } else {
                return ExecMethod2(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
        }

        private int ExecMethod1(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec", this.ToString()));
            char typedChar = char.MinValue;

            //make sure the input is a char before getting it
            if ((pguidCmdGroup == VSConstants.VSStd2K) && (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)) {
                typedChar = this.GetTypeChar(pvaIn);
            }
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec: typedChar={1}", this.ToString(), typedChar));

            //check for a commit character
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
                char.IsWhiteSpace(typedChar) ||
                char.IsPunctuation(typedChar)) {

                //check for a selection
                if ((this._currentSession != null) && !this._currentSession.IsDismissed) {
                    //if the selection is fully selected, commit the current session
                    if (this._currentSession.SelectedCompletionSet.SelectionStatus.IsSelected) {
                        this._currentSession.Commit();

                        //pass along the command so the char is added to the buffer, except if the command is an enter
                        if (nCmdID != (uint)VSConstants.VSStd2KCmdID.RETURN) {
                            this._nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        }
                        return VSConstants.S_OK;
                    } else { //if there is no selection, dismiss the session
                        this._currentSession.Dismiss();
                    }
                }
            }
            //pass along the command so the char is added to the buffer
            int retVal = this._nextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;
            if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar)) {
                if (this._currentSession == null || this._currentSession.IsDismissed) { // If there is no active session, bring up completion
                    if (this.StartSession()) {
                        if (this._currentSession != null) {
                            this._currentSession.Filter();
                        }
                    }
                } else {   //the completion session is already active, so just filter
                    this._currentSession.Filter();
                }
                handled = true;
            } else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE   //redo the filter if there is a deletion
                    || nCmdID == (uint)VSConstants.VSStd2KCmdID.DELETE) {
                if (this._currentSession != null && !this._currentSession.IsDismissed) {
                    this._currentSession.Filter();
                }
                handled = true;
            }
            if (handled) return VSConstants.S_OK;
            return retVal;
        }

        private int ExecMethod2(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:ExecMethod2", this.ToString()));

            bool handledChar = false;
            int hresult = VSConstants.S_OK;
            char typedChar = char.MinValue;

            #region 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        handledChar = this.StartSession();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        handledChar = this.Complete(true);
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        this.Complete(true);
                        handledChar = false;
                        break;
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        handledChar = this.Cancel();
                        break;
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        typedChar = GetTypeChar(pvaIn);
                        if (char.IsWhiteSpace(typedChar)) {
                            this.Complete(true);
                            handledChar = false;
                        } else if (AsmTools.AsmSourceTools.isSeparatorChar(typedChar)) {
                            this.Complete(false);
                            handledChar = false;
                        } else if (AsmTools.AsmSourceTools.isRemarkChar(typedChar)) {
                            this.Complete(true);
                            handledChar = false;
                        }
                        break;
                }
            }
            #endregion

            #region 2. Handle the typed char
            if (!handledChar) {
                hresult = this._nextCommandHandler.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar)) {
                //if (!typedChar.Equals(char.MinValue)) {
                    if ((this._currentSession == null) || this._currentSession.IsDismissed) { // If there is no active session, bring up completion
                        this.StartSession();
                    }
                    this.Filter();
                    hresult = VSConstants.S_OK;
                } else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE   //redo the filter if there is a deletion
                        || nCmdID == (uint)VSConstants.VSStd2KCmdID.DELETE) {
                    if ((this._currentSession != null) && !this._currentSession.IsDismissed) {
                        this.Filter();
                    }
                    hresult = VSConstants.S_OK;
                }
            }
            #endregion

            #region Post-process
            if (ErrorHandler.Succeeded(hresult)) {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                        case VSConstants.VSStd2KCmdID.DELETE:
                            this.Filter();
                            break;
                    }
                }
            }
            #endregion

            return hresult;
        }

        private bool StartSession() {
            if (this._currentSession != null) {
                return false;
            }
            SnapshotPoint caret = this._textView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            if (this._broker.IsCompletionActive(this._textView)) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:StartSession. Recycling an existing auto-complete session", this.ToString()));
                this._currentSession = this._broker.GetSessions(this._textView)[0];
            } else {
                //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:StartSession. Creating a new auto-complete session", this.ToString()));
                this._currentSession = this._broker.CreateCompletionSession(this._textView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            }
            this._currentSession.Dismissed += (sender, args) => _currentSession = null;
            this._currentSession.Start();
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:StartSession", this.ToString()));
            return true;
        }

        /// <summary>
        /// Complete the auto-complete
        /// </summary>
        /// <param name="force">force the selection even if it has not been manually selected</param>
        /// <returns></returns>
        private bool Complete(bool force) {
            if (this._currentSession == null) {
                return false;
            }
            if (!_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected && !force) {
                this._currentSession.Dismiss();
                return false;
            } else {
                this._currentSession.Commit();
                return true;
            }
        }

        private bool Cancel() {
            if (this._currentSession == null) {
                return false;
            }
            this._currentSession.Dismiss();
            return true;
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter() {
            if (this._currentSession == null) {
                return;
            }
           // this._currentSession.SelectedCompletionSet.SelectBestMatch();
            //this._currentSession.SelectedCompletionSet.Recalculate();
            this._currentSession.Filter();
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:QueryStatus", this.ToString()));
            if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID) {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:QueryStatus", this.ToString()));
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return _nextCommandHandler.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}