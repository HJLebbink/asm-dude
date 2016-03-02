using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

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
        #region Properties

        [Category("General")]
        [Description("Use Syntax Highlighting")]
        [DisplayName("Use Syntax Highlighting")]
        public bool _useSyntaxHighlighting { get; set; }

        [Category("Colors used for Code Completion")]
        [Description("Mnemonic")]
        [DisplayName("Mnemonic")]
        public System.Drawing.Color _colorMnemonic { get; set; }

        [Category("Colors used for Code Completion")]
        [Description("Register")]
        [DisplayName("Register")]
        public System.Drawing.Color _colorRegister { get; set; }

        [Category("Colors used for Code Completion")]
        [Description("Remark")]
        [DisplayName("Remark")]
        public System.Drawing.Color _colorRemark { get; set; }

        [Category("Colors used for Code Completion")]
        [Description("Directive")]
        [DisplayName("Directive")]
        public System.Drawing.Color _colorDirective { get; set; }
        
        [Category("Colors used for Code Completion")]
        [Description("Constant")]
        [DisplayName("Constant")]
        public System.Drawing.Color _colorConstant { get; set; }
        
        [Category("Colors used for Code Completion")]
        [Description("Jump")]
        [DisplayName("Jump")]
        public System.Drawing.Color _colorJump { get; set; }
        
        [Category("Colors used for Code Completion")]
        [Description("Label")]
        [DisplayName("Label")]
        public System.Drawing.Color _colorLabel { get; set; }
        
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
            base.OnActivate(e);
            this._useSyntaxHighlighting = Properties.Settings.Default.SyntaxHighlighting_On;
            this._colorMnemonic = Properties.Settings.Default.SyntaxHighlighting_Opcode;
            this._colorRegister = Properties.Settings.Default.SyntaxHighlighting_Register;
            this._colorRemark = Properties.Settings.Default.SyntaxHighlighting_Remark;
            this._colorDirective = Properties.Settings.Default.SyntaxHighlighting_Directive;
            this._colorConstant = Properties.Settings.Default.SyntaxHighlighting_Constant;
            this._colorJump = Properties.Settings.Default.SyntaxHighlighting_Jump;
            this._colorLabel = Properties.Settings.Default.SyntaxHighlighting_Label;
            this._colorMisc = Properties.Settings.Default.SyntaxHighlighting_Misc;
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

            string title = null; //"Save Changes";
            string message = "Press OK to save changes. You may need to restart visual studio for the changes to take effect.";
            int result = VsShellUtilities.ShowMessageBox(Site, message, title, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            if (result == (int)VSConstants.MessageBoxResult.IDCANCEL) {
                e.ApplyBehavior = ApplyKind.Cancel;
            } else {
                Properties.Settings.Default.SyntaxHighlighting_On = this._useSyntaxHighlighting;
                Properties.Settings.Default.SyntaxHighlighting_Opcode = this._colorMnemonic;
                Properties.Settings.Default.SyntaxHighlighting_Register = this._colorRegister;
                Properties.Settings.Default.SyntaxHighlighting_Remark = this._colorRemark;
                Properties.Settings.Default.SyntaxHighlighting_Directive = this._colorDirective;
                Properties.Settings.Default.SyntaxHighlighting_Constant = this._colorConstant;
                Properties.Settings.Default.SyntaxHighlighting_Jump = this._colorJump;
                Properties.Settings.Default.SyntaxHighlighting_Label = this._colorLabel;
                Properties.Settings.Default.SyntaxHighlighting_Misc = this._colorMisc;

                Properties.Settings.Default.Save();
                base.OnApply(e);
            }
        }

        #endregion Event Handlers
    }
}
