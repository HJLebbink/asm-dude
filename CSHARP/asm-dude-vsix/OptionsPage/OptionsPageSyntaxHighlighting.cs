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
    public class OptionsPageSyntaxHighlighting : DialogPage {

        #region Properties
        private const string cat = "Colors used for Syntax Highlighting";

        [Category("General")]
        [Description("Use Syntax Highlighting")]
        [DisplayName("Use Syntax Highlighting")]
        [DefaultValue(true)]
        public bool _useSyntaxHighlighting { get; set; }

        [Category(cat)]
        [Description("Mnemonic")]
        [DisplayName("Mnemonic")]
        [DefaultValue(System.Drawing.KnownColor.Blue)]
        public System.Drawing.Color _colorMnemonic { get; set; }

        [Category(cat)]
        [Description("Register")]
        [DisplayName("Register")]
        [DefaultValue(System.Drawing.KnownColor.DarkRed)]
        public System.Drawing.Color _colorRegister { get; set; }

        [Category(cat)]
        [Description("Remark")]
        [DisplayName("Remark")]
        [DefaultValue(System.Drawing.KnownColor.Green)]
        public System.Drawing.Color _colorRemark { get; set; }

        [Category(cat)]
        [Description("Directive")]
        [DisplayName("Directive")]
        [DefaultValue(System.Drawing.KnownColor.Magenta)]
        public System.Drawing.Color _colorDirective { get; set; }

        [Category(cat)]
        [Description("Constant")]
        [DisplayName("Constant")]
        [DefaultValue(System.Drawing.KnownColor.Chocolate)]
        public System.Drawing.Color _colorConstant { get; set; }

        [Category(cat)]
        [Description("Jump")]
        [DisplayName("Jump")]
        [DefaultValue(System.Drawing.KnownColor.Blue)]
        public System.Drawing.Color _colorJump { get; set; }

        [Category(cat)]
        [Description("Label")]
        [DisplayName("Label")]
        [DefaultValue(System.Drawing.KnownColor.OrangeRed)]
        public System.Drawing.Color _colorLabel { get; set; }

        [Category(cat)]
        [Description("Misc")]
        [DisplayName("Misc")]
        [DefaultValue(System.Drawing.KnownColor.DarkOrange)]
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
        protected override void OnActivate(CancelEventArgs e) {
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
        protected override void OnClosed(EventArgs e) {
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
        protected override void OnDeactivate(CancelEventArgs e) {
            bool changed = false;
            if (Properties.Settings.Default.SyntaxHighlighting_On != this._useSyntaxHighlighting) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Opcode != this._colorMnemonic) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Register != this._colorRegister) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Remark != this._colorRemark) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Directive != this._colorDirective) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Constant != this._colorConstant) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Jump != this._colorJump) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Label != this._colorLabel) {
                changed = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Misc != this._colorMisc) {
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

            if (Properties.Settings.Default.SyntaxHighlighting_On != this._useSyntaxHighlighting) {
                Properties.Settings.Default.SyntaxHighlighting_On = this._useSyntaxHighlighting;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Opcode != this._colorMnemonic) {
                Properties.Settings.Default.SyntaxHighlighting_Opcode = this._colorMnemonic;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Register != this._colorRegister) {
                Properties.Settings.Default.SyntaxHighlighting_Register = this._colorRegister;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Remark != this._colorRemark) {
                Properties.Settings.Default.SyntaxHighlighting_Remark = this._colorRemark;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Directive != this._colorDirective) {
                Properties.Settings.Default.SyntaxHighlighting_Directive = this._colorDirective;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Constant != this._colorConstant) {
                Properties.Settings.Default.SyntaxHighlighting_Constant = this._colorConstant;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Jump != this._colorJump) {
                Properties.Settings.Default.SyntaxHighlighting_Jump = this._colorJump;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Label != this._colorLabel) {
                Properties.Settings.Default.SyntaxHighlighting_Label = this._colorLabel;
                changed = true;
                restartNeeded = true;
            }
            if (Properties.Settings.Default.SyntaxHighlighting_Misc != this._colorMisc) {
                Properties.Settings.Default.SyntaxHighlighting_Misc = this._colorMisc;
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
