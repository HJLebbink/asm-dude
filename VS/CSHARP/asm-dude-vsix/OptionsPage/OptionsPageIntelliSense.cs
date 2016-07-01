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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
/*
namespace AsmDude.OptionsPage {

    [Guid(Guids.GuidOptionsPageIntelliSense)]
    public class OptionsPageIntelliSense: DialogPage {

        #region Properties

        [Category("General")]
        [Description("Show undefined labels in error task")]
        [DisplayName("Show undefined labels in error task")]
        [DefaultValue(true)]
        public bool _showUndefinedLabels { get; set; }

        [Category("General")]
        [Description("Decorate undefined labels")]
        [DisplayName("Decorate undefined labels with error squiggles")]
        [DefaultValue(true)]
        public bool _decorateUndefinedLabels { get; set; }

        [Category("General")]
        [Description("Show clashing labels in error task")]
        [DisplayName("Show clashing labels in error task")]
        [DefaultValue(true)]
        public bool _showClashingLabels { get; set; }

        [Category("General")]
        [Description("Decorate clashing labels")]
        [DisplayName("Decorate clashing labels with error squiggles")]
        [DefaultValue(true)]
        public bool _decorateClashingLabels { get; set; }

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
            this._showUndefinedLabels = Settings.Default.IntelliSenseShowUndefinedLabels;
            this._showClashingLabels = Settings.Default.IntelliSenseShowClashingLabels;
            this._decorateUndefinedLabels = Settings.Default.IntelliSenseDecorateUndefinedLabels;
            this._decorateClashingLabels = Settings.Default.IntelliSenseDecorateClashingLabels;
        }

        /// <summary>
        /// Handles "close" messages from the Visual Studio environment.
        /// </summary>
        /// <devdoc>
        /// This event is raised when the page is closed.
        /// </devdoc>
        protected override void OnClosed(EventArgs e) {}

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
            if (Settings.Default.IntelliSenseShowUndefinedLabels != this._showUndefinedLabels) {
                changed = true;
            }
            if (Settings.Default.IntelliSenseShowClashingLabels != this._showClashingLabels) {
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateUndefinedLabels != this._decorateUndefinedLabels) {
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateClashingLabels != this._decorateClashingLabels) {
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

            if (Settings.Default.IntelliSenseShowUndefinedLabels != this._showUndefinedLabels) {
                Settings.Default.IntelliSenseShowUndefinedLabels = this._showUndefinedLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseShowClashingLabels != this._showClashingLabels) {
                Settings.Default.IntelliSenseShowClashingLabels = this._showClashingLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateUndefinedLabels != this._decorateUndefinedLabels) {
                Settings.Default.IntelliSenseDecorateUndefinedLabels = this._decorateUndefinedLabels;
                changed = true;
            }
            if (Settings.Default.IntelliSenseDecorateClashingLabels != this._decorateClashingLabels) {
                Settings.Default.IntelliSenseDecorateClashingLabels = this._decorateClashingLabels;
                changed = true;
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
*/