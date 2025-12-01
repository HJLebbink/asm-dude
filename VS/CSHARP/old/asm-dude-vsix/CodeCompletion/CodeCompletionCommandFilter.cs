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

namespace AsmDude
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.OLE.Interop;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    internal sealed class CodeCompletionCommandFilter : IOleCommandTarget
    {
        private ICompletionSession currentSession_;

        public CodeCompletionCommandFilter(IWpfTextView textView, ICompletionBroker broker)
        {
            this.currentSession_ = null;
            this.TextView = textView ?? throw new ArgumentNullException(nameof(textView));
            this.Broker = broker ?? throw new ArgumentNullException(nameof(broker));
            //Debug.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: {0}:constructor", this.ToString()));
        }

        public IWpfTextView TextView { get; private set; }

        public ICompletionBroker Broker { get; private set; }

        public IOleCommandTarget NextCommandHandler { get; set; }

        private static char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //if (VsShellUtilities.IsInAutomationFunction(m_provider.ServiceProvider)) {
            //    return nextCommandHandler_.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            //}
            if (false)
            {
                return this.ExecMethod1(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            else
            {
                return this.ExecMethod2(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
        }

        private int ExecMethod1(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec", this.ToString()));
            char typedChar = char.MinValue;

            //make sure the input is a char before getting it
            if ((pguidCmdGroup == VSConstants.VSStd2K) && (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR))
            {
                typedChar = GetTypeChar(pvaIn);
            }
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Exec: typedChar={1}", this.ToString(), typedChar));

            //check for a commit character
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
                char.IsWhiteSpace(typedChar) ||
                char.IsPunctuation(typedChar))
            {
                //check for a selection
                if ((this.currentSession_ != null) && !this.currentSession_.IsDismissed)
                {
                    //if the selection is fully selected, commit the current session
                    if (this.currentSession_.SelectedCompletionSet.SelectionStatus.IsSelected)
                    {
                        this.currentSession_.Commit();

                        //pass along the command so the char is added to the buffer, except if the command is an enter
                        if (nCmdID != (uint)VSConstants.VSStd2KCmdID.RETURN)
                        {
                            this.NextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        }
                        return VSConstants.S_OK;
                    }
                    else
                    { //if there is no selection, dismiss the session
                        this.currentSession_.Dismiss();
                    }
                }
            }
            //pass along the command so the char is added to the buffer
            int retVal = this.NextCommandHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;
            if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
            {
                if (this.currentSession_ == null || this.currentSession_.IsDismissed)
                { // If there is no active session, bring up completion
                    if (this.StartSession())
                    {
                        if (this.currentSession_ != null)
                        {
                            this.currentSession_.Filter();
                        }
                    }
                }
                else
                { //the completion session is already active, so just filter
                    this.currentSession_.Filter();
                }
                handled = true;
            }
            else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE //redo the filter if there is a deletion
                  || nCmdID == (uint)VSConstants.VSStd2KCmdID.DELETE)
            {
                if (this.currentSession_ != null && !this.currentSession_.IsDismissed)
                {
                    this.currentSession_.Filter();
                }
                handled = true;
            }
            if (handled)
            {
                return VSConstants.S_OK;
            }

            return retVal;
        }

        private int ExecMethod2(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:ExecMethod2", this.ToString()));

            bool handledChar = false;
            int hresult = VSConstants.S_OK;
            char typedChar = char.MinValue;

            #region 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
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
                        if (char.IsWhiteSpace(typedChar))
                        {
                            this.Complete(true);
                            handledChar = false;
                        }
                        else if (AsmTools.AsmSourceTools.IsSeparatorChar(typedChar))
                        {
                            this.Complete(false);
                            handledChar = false;
                        }
                        else if (AsmTools.AsmSourceTools.IsRemarkChar(typedChar))
                        {
                            this.Complete(true);
                            handledChar = false;
                        }
                        break;
                }
            }
            #endregion

            #region 2. Handle the typed char
            if (!handledChar)
            {
                hresult = this.NextCommandHandler.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
                {
                    //if (!typedChar.Equals(char.MinValue)) {
                    if ((this.currentSession_ == null) || this.currentSession_.IsDismissed)
                    { // If there is no active session, bring up completion
                        this.StartSession();
                    }
                    this.Filter();
                    hresult = VSConstants.S_OK;
                }
                else if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE //redo the filter if there is a deletion
                      || nCmdID == (uint)VSConstants.VSStd2KCmdID.DELETE)
                {
                    if ((this.currentSession_ != null) && !this.currentSession_.IsDismissed)
                    {
                        this.Filter();
                    }
                    hresult = VSConstants.S_OK;
                }
            }
            #endregion

            #region Post-process
            if (ErrorHandler.Succeeded(hresult))
            {
                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
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

        private bool StartSession()
        {
            if (this.currentSession_ != null)
            {
                return false;
            }
            SnapshotPoint caret = this.TextView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            if (this.Broker.IsCompletionActive(this.TextView))
            {
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:StartSession. Recycling an existing auto-complete session", this.ToString()));
                this.currentSession_ = this.Broker.GetSessions(this.TextView)[0];
            }
            else
            {
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:StartSession. Creating a new auto-complete session", this.ToString()));
                this.currentSession_ = this.Broker.CreateCompletionSession(this.TextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            }
            this.currentSession_.Dismissed += (sender, args) => this.currentSession_ = null;
            this.currentSession_.Start();
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:StartSession", this.ToString()));
            return true;
        }

        /// <summary>
        /// Complete the auto-complete
        /// </summary>
        /// <param name="force">force the selection even if it has not been manually selected</param>
        /// <returns></returns>
        private bool Complete(bool force)
        {
            if (this.currentSession_ == null)
            {
                return false;
            }
            if (!this.currentSession_.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
            {
                this.currentSession_.Dismiss();
                return false;
            }
            else
            {
                this.currentSession_.Commit();
                return true;
            }
        }

        private bool Cancel()
        {
            if (this.currentSession_ == null)
            {
                return false;
            }
            this.currentSession_.Dismiss();
            return true;
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter()
        {
            if (this.currentSession_ == null)
            {
                return;
            }
            // this._currentSession.SelectedCompletionSet.SelectBestMatch();
            //this._currentSession.SelectedCompletionSet.Recalculate();
            this.currentSession_.Filter();
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:QueryStatus", this.ToString()));
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:QueryStatus", this.ToString()));
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:QueryStatus", this.ToString()));
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return this.NextCommandHandler.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}