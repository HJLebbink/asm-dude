
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AsmDude.OptionsPage {
    /// <summary>
    // Extends the standard dialog functionality for implementing ToolsOptions pages, 
    // with support for the Visual Studio automation model, Windows Forms, and state 
    // persistence through the Visual Studio settings mechanism.
    /// </summary>
    [Guid(GuidStrings.GuidOptionsPageAsmDoc)]
    public class OptionsPageAsmDoc : DialogPage {
        #region Properties

        [Category("General")]
        [Description("Use Asm Documentation")]
        [DisplayName("Use Asm Documentation")]
        public bool _useAsmDoc { get; set; }

        [Category("Documentation Url")]
        [Description("url to the documentation. E.g. http://www.felixcloutier.com/x86/")]
        [DisplayName("Url")]
        public string _asmDocUrl { get; set; }

        #endregion Properties

        #region Event Handlers

        /// <summary>
        /// Handles "activate" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This method is called when Visual Studio wants to activate this page.  
        /// </devdoc>
        /// <remarks>If this handler sets e.Cancel to true, the activation will not occur.</remarks>
        protected override void OnActivate(CancelEventArgs e) {
            base.OnActivate(e);
            this._useAsmDoc = Properties.Settings.Default.AsmDoc_On;
            this._asmDocUrl = Properties.Settings.Default.AsmDoc_url;
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e) {
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
        protected override void OnDeactivate(CancelEventArgs e) {
            bool changed = false;
            if (Properties.Settings.Default.AsmDoc_On != this._useAsmDoc) {
                changed = true;
            }
            if (Properties.Settings.Default.AsmDoc_url != this._asmDocUrl) {
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
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:save", this.ToString()));
            bool changed = false;
            bool restartNeeded = false;

            if (Properties.Settings.Default.AsmDoc_On != this._useAsmDoc) {
                Properties.Settings.Default.AsmDoc_On = this._useAsmDoc;
                changed = true;
            }
            if (Properties.Settings.Default.AsmDoc_url != this._asmDocUrl) {
                Properties.Settings.Default.AsmDoc_url = this._asmDocUrl;
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
        }

        #endregion Event Handlers
    }
}
