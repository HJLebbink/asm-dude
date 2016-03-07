
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AsmDude.OptionsPage
{
    /// <summary>
    // Extends the standard dialog functionality for implementing ToolsOptions pages, 
    // with support for the Visual Studio automation model, Windows Forms, and state 
    // persistence through the Visual Studio settings mechanism.
    /// </summary>
    [Guid(GuidStrings.GuidOptionsPageCodeFolding)]
    public class OptionsPageCodeFolding : DialogPage
    {
        #region Properties

        [Category("General")]
        [Description("Use Code Folding")]
        [DisplayName("Use Code Folding")]
        public bool _useCodeFolding { get; set; }

        [Category("Code Folding Tags")]
        [Description("the characters that start the outlining region")]
        [DisplayName("Begin Tag")]
        public string _beginTag { get; set; }

        [Category("Code Folding Tags")]
        [Description("the characters that end the outlining region")]
        [DisplayName("End Tag")]
        public string _endTag { get; set; }

        #endregion Properties

        #region Event Handlers

        /// <summary>
        /// Handles "activate" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when Visual Studio wants to activate this page.  
        /// </devdoc>
        /// <remarks>If this handler sets e.Cancel to true, the activation will not occur.</remarks>
        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            this._useCodeFolding = Properties.Settings.Default.CodeFolding_On;
            this._beginTag = Properties.Settings.Default.CodeFolding_BeginTag;
            this._endTag = Properties.Settings.Default.CodeFolding_EndTag;
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e)
        {
            /*
            string title = "title here";
            VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnClosed, title, OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            */
        }

        /// <summary>
        /// Handles "deactivate" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when VS wants to deactivate this
        /// page.  If this handler sets e.Cancel, the deactivation will not occur.
        /// </devdoc>
        /// <remarks>
        /// A "deactivate" message is sent when focus changes to a different page in
        /// the dialog.
        /// </remarks>
        protected override void OnDeactivate(CancelEventArgs e)
        {
            /*
            string title = "title here";
            int result = VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnDeactivateEntered, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDCANCEL)
            {
                e.Cancel = true;
            }
            */
        }

        /// <summary>
        /// Handles "apply" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when VS wants to save the user's 
        /// changes (for example, when the user clicks OK in the dialog).
        /// </devdoc>
        protected override void OnApply(PageApplyEventArgs e) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:OnApply", this.ToString()));

            bool changed = false;
            bool restartNeeded = false;

            if (Properties.Settings.Default.CodeFolding_On != this._useCodeFolding) {
                Properties.Settings.Default.CodeFolding_On = this._useCodeFolding;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeFolding_BeginTag != this._beginTag) {
                Properties.Settings.Default.CodeFolding_BeginTag = this._beginTag;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeFolding_EndTag != this._endTag) {
                Properties.Settings.Default.CodeFolding_EndTag = this._endTag;
                changed = true;
                restartNeeded = true;
            }
            if (changed) {
                Properties.Settings.Default.Save();
            }
            if (restartNeeded) {
                string title = null;
                string message = "You may need to restart visual studio for the changes to take effect.";
                int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            base.OnApply(e);

            /*
            string title = null;
            string message = "Press OK to save changes. You may need to restart visual studio for the changes to take effect.";
            int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDCANCEL) {
                e.ApplyBehavior = ApplyKind.Cancel;
            } else {
                Properties.Settings.Default.Save();
                base.OnApply(e);
            }
            */
        }

        #endregion Event Handlers
    }
}
