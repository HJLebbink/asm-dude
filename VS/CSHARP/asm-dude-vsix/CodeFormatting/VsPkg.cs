/***************************************************************************
 
Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;

namespace Microsoft.Samples.VisualStudio.MenuCommands
{
    /// <summary>
    /// This is the class that implements the package. This is the class that Visual Studio will create
    /// when one of the commands will be selected by the user, and so it can be considered the main
    /// entry point for the integration with the IDE.
    /// Notice that this implementation derives from Microsoft.VisualStudio.Shell.Package that is the
    /// basic implementation of a package provided by the Managed Package Framework (MPF).
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]

    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidsList.guidMenuAndCommandsPkg_string)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ComVisible(true)]
    public sealed class MenuCommandsPackage : Package
    {
        #region Member Variables
        private OleMenuCommand dynamicVisibilityCommand1;
        private OleMenuCommand dynamicVisibilityCommand2;
        #endregion

       public MenuCommandsPackage()
        {
        }

       protected override void Initialize()
        {
            base.Initialize();

            // Now get the OleCommandService object provided by the MPF; this object is the one
            // responsible for handling the collection of commands implemented by the package.
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                CommandID id1 = new CommandID(GuidsList.guidMenuAndCommandsCmdSet, PkgCmdIDList.asmDudeCommand1);
                mcs.AddCommand(new OleMenuCommand(new EventHandler(this.MenuCommandCallback1), id1));

                CommandID id2 = new CommandID(GuidsList.guidMenuAndCommandsCmdSet, PkgCmdIDList.asmDudeCommand2);
                mcs.AddCommand(new OleMenuCommand(new EventHandler(this.MenuCommandCallback2), id2));

                CommandID id3 = new CommandID(GuidsList.guidMenuAndCommandsCmdSet, PkgCmdIDList.asmDudeCommand3);
                mcs.AddCommand(new OleMenuCommand(new EventHandler(this.MenuCommandCallback3), id3));
            }
        }

        #region Commands Actions
        /// <summary>
        /// This function prints text on the debug ouput and on the generic pane of the 
        /// Output window.
        /// </summary>
        /// <param name="text"></param>
        private void OutputCommandString(string text)
        {
            // Build the string to write on the debugger and Output window.
            StringBuilder outputText = new StringBuilder();
            outputText.Append(" ================================================\n");
            outputText.AppendFormat("  MenuAndCommands: {0}\n", text);

            IVsOutputWindowPane windowPane = (IVsOutputWindowPane)GetService(typeof(SVsGeneralOutputWindowPane));
            if (null == windowPane)
            {
                Debug.WriteLine("Failed to get a reference to the Output window General pane");
                return;
            }
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.OutputString(outputText.ToString())))
            {
                Debug.WriteLine("Failed to write on the Output window");
            }
        }

        private TextSelection GetTextSelection()
        {
            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            if (dte == null) return null;
            Document doc = dte.ActiveDocument;
            if (doc == null) return null;
            return (TextSelection)doc.Selection;
        }

        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void MenuCommandCallback1(object caller, EventArgs args)
        {
            OutputCommandString("MenuCommandCallback1");
            TextSelection sel = this.GetTextSelection();
            if (sel == null) return;
            if (sel.IsEmpty) return;
            sel.Text = sel.Text.ToUpperInvariant();
        }

        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void MenuCommandCallback2(object caller, EventArgs args)
        {
            OutputCommandString("MenuCommandCallback2");
            TextSelection sel = this.GetTextSelection();
            if (sel == null) return;
            if (sel.IsEmpty) return;
            sel.Text = sel.Text.ToLowerInvariant();
        }

        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void MenuCommandCallback3(object caller, EventArgs args)
        {
            OutputCommandString("MenuCommandCallback3");
            TextSelection sel = this.GetTextSelection();
            if (sel == null) return;

            string content;
            if (sel.IsEmpty)
            {
                content = "selection: line " + sel.TopLine + ":" + caller;
            }
            else
            {
                content = "selection: lines " + sel.TopLine + "-" + sel.BottomLine;
            }
        }
        #endregion
    }
}
