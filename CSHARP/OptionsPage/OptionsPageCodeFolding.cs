
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
        public OptionsPageCodeFolding()
        {
            this._useCodeCompletion = true;

            this._x86 = true;
            this._sse = true;
            this._sse2 = true;
            this._sse3 = true;
            this._ssse3 = true;
            this._sse41 = true;
            this._sse42 = true;
            this._avx = true;
            this._avx2 = true;
            this._knc = false;
        }

        #region Properties

        [Category("Use Code Completion")]
        [Description("Use Code Completion")]
        [DisplayName("Use Code Completion")]
        public bool _useCodeCompletion { get; set; }


        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Architectures used for Code Completion")]
        [Description("x86")]
        [DisplayName("x86")]
        public bool _x86 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSE")]
        [DisplayName("SSE")]
        public bool _sse { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSE2")]
        [DisplayName("SSE2")]
        public bool _sse2 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSE3")]
        [DisplayName("SSE3")]
        public bool _sse3 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSSE3")]
        [DisplayName("SSSE3")]
        public bool _ssse3 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSE4.1")]
        [DisplayName("SSE4.1")]
        public bool _sse41 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("SSE4.2")]
        [DisplayName("SSE4.2")]
        public bool _sse42 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("AVX")]
        [DisplayName("AVX")]
        public bool _avx { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("AVX2")]
        [DisplayName("AVX2")]
        public bool _avx2 { get; set; }

        [Category("Architectures used for Code Completion")]
        [Description("Xeon Phi Knights Corner")]
        [DisplayName("KNC")]
        public bool _knc { get; set; }


        /*

        /// <summary>
        /// Gets or sets the String type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("String Options")]
        [Description("My string option")]
        public string OptionString { get; set; }

        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Integer Options")]
        [Description("My integer option")]
        public int OptionInteger { get; set; }

        /// <summary>
        /// Gets or sets the Size type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Expandable Options")]
        [Description("My Expandable option")]
        public Size CustomSize { get; set; }
        */
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
            /*
            string title = "title here";
            int result = VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnActivateEntered, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDCANCEL)
            {
                e.Cancel = true;
            }
            */
            base.OnActivate(e);
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
            /*
            string title = "title here";
            int result = VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnApplyEntered, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDCANCEL)
            {
                e.ApplyBehavior = ApplyKind.Cancel;
            }
            else
            {
                base.OnApply(e);
            }
            string title2 = "title here";
            VsShellUtilities.ShowMessageBox(Site, Resources.MessageOnApply, title2, OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            */
        }

        #endregion Event Handlers
    }
}
