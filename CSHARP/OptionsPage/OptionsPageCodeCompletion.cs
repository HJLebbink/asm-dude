
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AsmDude.OptionsPage {
    /// <summary>
    // Extends the standard dialog functionality for implementing ToolsOptions pages, 
    // with support for the Visual Studio automation model, Windows Forms, and state 
    // persistence through the Visual Studio settings mechanism.
    /// </summary>
    [Guid(GuidStrings.GuidOptionsPageCodeCompletion)]
    public class OptionsPageCodeCompletion : DialogPage {

        public OptionsPageCodeCompletion() {
            AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0} constructor", this.ToString()));
        }

        #region Properties
        private const string cat = "Architectures used for Code Completion";

        [Category("General")]
        [Description("Use Code Completion")]
        [DisplayName("Use Code Completion")]
        [DefaultValue(true)]
        public bool _useCodeCompletion { get; set; }

        [Category(cat)]
        [Description("x86")]
        [DisplayName("x86")]
        [DefaultValue(true)]
        public bool _x86 { get; set; }

        [Category(cat)]
        [Description("i686 (conditional move and set)")]
        [DisplayName("i686")]
        [DefaultValue(true)]
        public bool _i686 { get; set; }

        [Category(cat)]
        [Description("MMX")]
        [DisplayName("MMX")]
        [DefaultValue(false)]
        public bool _mmx { get; set; }

        [Category(cat)]
        [Description("SSE")]
        [DisplayName("SSE")]
        [DefaultValue(true)]
        public bool _sse { get; set; }

        [Category(cat)]
        [Description("SSE2")]
        [DisplayName("SSE2")]
        [DefaultValue(true)]
        public bool _sse2 { get; set; }

        [Category(cat)]
        [Description("SSE3")]
        [DisplayName("SSE3")]
        [DefaultValue(true)]
        public bool _sse3 { get; set; }

        [Category(cat)]
        [Description("SSSE3")]
        [DisplayName("SSSE3")]
        [DefaultValue(true)]
        public bool _ssse3 { get; set; }

        [Category(cat)]
        [Description("SSE4.1")]
        [DisplayName("SSE4.1")]
        [DefaultValue(true)]
        public bool _sse41 { get; set; }

        [Category(cat)]
        [Description("SSE4.2")]
        [DisplayName("SSE4.2")]
        [DefaultValue(true)]
        public bool _sse42 { get; set; }

        [Category(cat)]
        [Description("AVX")]
        [DisplayName("AVX")]
        [DefaultValue(true)]
        public bool _avx { get; set; }

        [Category(cat)]
        [Description("AVX2")]
        [DisplayName("AVX2")]
        [DefaultValue(false)]
        public bool _avx2 { get; set; }

        [Category(cat)]
        [Description("Xeon Phi Knights Corner")]
        [DisplayName("KNC")]
        [DefaultValue(false)]
        public bool _knc { get; set; }

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
            this._useCodeCompletion = Properties.Settings.Default.CodeCompletion_On;

            this._x86 = Properties.Settings.Default.CodeCompletion_x86;
            this._i686 = Properties.Settings.Default.CodeCompletion_i686;
            this._mmx = Properties.Settings.Default.CodeCompletion_mmx;
            this._sse = Properties.Settings.Default.CodeCompletion_sse;
            this._sse2 = Properties.Settings.Default.CodeCompletion_sse2;
            this._sse3 = Properties.Settings.Default.CodeCompletion_sse3;
            this._ssse3 = Properties.Settings.Default.CodeCompletion_ssse3;
            this._sse41 = Properties.Settings.Default.CodeCompletion_sse41;
            this._sse42 = Properties.Settings.Default.CodeCompletion_sse42;
            this._avx = Properties.Settings.Default.CodeCompletion_avx;
            this._avx2 = Properties.Settings.Default.CodeCompletion_avx2;
            this._knc = Properties.Settings.Default.CodeCompletion_knc;
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
            if (Properties.Settings.Default.CodeCompletion_On != this._useCodeCompletion) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_x86 != this._x86) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_i686 != this._i686) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_mmx != this._mmx) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse != this._sse) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse2 != this._sse2) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse3 != this._sse3) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_ssse3 != this._ssse3) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse41 != this._sse41) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse42 != this._sse42) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_avx != this._avx) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_avx2 != this._avx2) {
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_knc != this._knc) {
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

            if (Properties.Settings.Default.CodeCompletion_On != this._useCodeCompletion) {
                Properties.Settings.Default.CodeCompletion_On = this._useCodeCompletion;
                changed = true;
            }
            if (Properties.Settings.Default.CodeCompletion_x86 != this._x86) {
                Properties.Settings.Default.CodeCompletion_x86 = this._x86;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_i686 != this._i686) {
                Properties.Settings.Default.CodeCompletion_i686 = this._i686;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_mmx != this._mmx) {
                Properties.Settings.Default.CodeCompletion_mmx = this._mmx;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse != this._sse) {
                Properties.Settings.Default.CodeCompletion_sse = this._sse;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse2 != this._sse2) {
                Properties.Settings.Default.CodeCompletion_sse2 = this._sse2;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse3 != this._sse3) {
                Properties.Settings.Default.CodeCompletion_sse3 = this._sse3;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_ssse3 != this._ssse3) {
                Properties.Settings.Default.CodeCompletion_ssse3 = this._ssse3;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse41 != this._sse41) {
                Properties.Settings.Default.CodeCompletion_sse41 = this._sse41;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_sse42 != this._sse42) {
                Properties.Settings.Default.CodeCompletion_sse42 = this._sse42;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_avx != this._avx) {
                Properties.Settings.Default.CodeCompletion_avx = this._avx;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_avx2 != this._avx2) {
                Properties.Settings.Default.CodeCompletion_avx2 = this._avx2;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.CodeCompletion_knc != this._knc) {
                Properties.Settings.Default.CodeCompletion_knc = this._knc;
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
