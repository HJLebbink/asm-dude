using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;

namespace AsmDude.OptionsPage {
    /// <summary>
    // Extends the standard dialog functionality for implementing ToolsOptions pages, 
    // with support for the Visual Studio automation model, Windows Forms, and state 
    // persistence through the Visual Studio settings mechanism.
    /// </summary>
    [Export(typeof(DialogPage))]
    [Guid(GuidStrings.GuidOptionsPageSyntaxHighlighting)]
    public class OptionsPageSyntaxHighlighting : DialogPage
    {
        //[Import(typeof(EditorFormatDefinition))]
        //private OperandP _operandP;

        //private static System.Windows.Media.Color ToMediaColor(this System.Drawing.Color color) {
        //    return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        //}

        public OptionsPageSyntaxHighlighting()
        {
            this._useSyntaxHighlighting = true;

            this._colorMnemonic = System.Drawing.Color.Blue;
            this._colorRegister = System.Drawing.Color.DarkRed;
            this._colorRemark = System.Drawing.Color.Green;
            this._colorDirective = System.Drawing.Color.Magenta;
            this._colorConstant = System.Drawing.Color.Chocolate;
            this._colorJump = System.Drawing.Color.Navy;
            this._colorLabel = System.Drawing.Color.OrangeRed;
            this._colorMisc = System.Drawing.Color.DarkOrange;
        }

        #region Properties

        [Category("Use Code Completion")]
        [Description("Use Code Completion")]
        [DisplayName("Use Code Completion")]
        public bool _useSyntaxHighlighting { get; set; }

        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Mnemonic")]
        [DisplayName("Mnemonic")]
        public System.Drawing.Color _colorMnemonic { get; set; }

        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Register")]
        [DisplayName("Register")]

        public System.Drawing.Color _colorRegister { get; set; }
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Remark")]
        [DisplayName("Remark")]

        public System.Drawing.Color _colorRemark { get; set; }
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Directive")]
        [DisplayName("Directive")]
        public System.Drawing.Color _colorDirective { get; set; }
        
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Constant")]
        [DisplayName("Constant")]
        public System.Drawing.Color _colorConstant { get; set; }
        
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Jump")]
        [DisplayName("Jump")]
        public System.Drawing.Color _colorJump { get; set; }
        
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Label")]
        [DisplayName("Label")]
        public System.Drawing.Color _colorLabel { get; set; }
        
        /// <summary>
        /// Gets or sets the integer type custom option value.
        /// </summary>
        /// <remarks>This value is shown in the options page.</remarks>
        [Category("Colors used for Code Completion")]
        [Description("Misc")]
        [DisplayName("Misc")]
        public System.Drawing.Color _colorMisc { get; set; }

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
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:Onclosed", this.ToString()));
            /*
            if (this._operandP == null) {
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:Onclosed:operandP is null", this.ToString()));
            } else {
                //this._operandP.update(ToMediaColor(this._colorMnemonic));
            }
            */
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
