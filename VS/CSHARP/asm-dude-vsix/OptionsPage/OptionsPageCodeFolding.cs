
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AsmDude.OptionsPage {

    [Guid(Guids.GuidOptionsPageCodeFolding)]
    public class OptionsPageCodeFolding : DialogPage
    {
        #region Properties
        private const string cat = "Code Folding Tags";

        [Category("General")]
        [Description("Use Code Folding")]
        [DisplayName("Use Code Folding")]
        [DefaultValue(true)]
        public bool _useCodeFolding { get; set; }

        [Category(cat)]
        [Description("the characters that start the outlining region")]
        [DisplayName("Begin Tag")]
        [DefaultValue("#region")]
        public string _beginTag { get; set; }

        [Category(cat)]
        [Description("the characters that end the outlining region")]
        [DisplayName("End Tag")]
        [DefaultValue("#endregion")]
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
            this._useCodeFolding = Settings.Default.CodeFolding_On;
            this._beginTag = Settings.Default.CodeFolding_BeginTag;
            this._endTag = Settings.Default.CodeFolding_EndTag;
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e)
        {
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
            bool changed = false;
            if (Settings.Default.CodeFolding_On != this._useCodeFolding) {
                changed = true;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._beginTag) {
                changed = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._endTag) {
                changed = true;
            }
            if (changed) {
                string title = null;
                string message = "Unsaved changes exist. Would you like to save.";
                int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if (result == (int)VSConstants.MessageBoxResult.IDOK) {
                    this.save();
                } else if (result == (int)VSConstants.MessageBoxResult.IDCANCEL) {
                    e.Cancel = true;
                }
            }
        }

        /// <summary>
        /// Handles "apply" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when VS wants to save the user's 
        /// changes (for example, when the user clicks OK in the dialog).
        /// </devdoc>
        protected override void OnApply(PageApplyEventArgs e) {
            this.save();
            base.OnApply(e);
        }

        private void save() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:OnApply", this.ToString()));

            bool changed = false;
            bool restartNeeded = false;

            if (Settings.Default.CodeFolding_On != this._useCodeFolding) {
                Settings.Default.CodeFolding_On = this._useCodeFolding;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_BeginTag != this._beginTag) {
                Settings.Default.CodeFolding_BeginTag = this._beginTag;
                changed = true;
                restartNeeded = true;
            }
            if (Settings.Default.CodeFolding_EndTag != this._endTag) {
                Settings.Default.CodeFolding_EndTag = this._endTag;
                changed = true;
                restartNeeded = true;
            }
            if (changed) {
                Settings.Default.Save();
            }
            if (restartNeeded) {
                string title = null;
                string message = "You may need to restart visual studio for the changes to take effect.";
                int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        #endregion Event Handlers
    }
}
