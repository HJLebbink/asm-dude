// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Threading;
    using AsmDude.OptionsPage;
    using AsmDude.Tools;
 
    using Microsoft.VisualStudio.Shell;

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)] // Info on this package for Help/About
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ComVisible(false)]
    [ProvideOptionPage(typeof(AsmDudeOptionsPage), "AsmDude", "General", 0, 0, true)]

    public sealed class AsmDudePackage : AsyncPackage
    {
        #region Global Constants
        public const string PackageGuidString = "27e0e7ef-ecaf-4b87-a574-6a909383f99f";

        internal const string AsmDudeContentType = "asm!";
        internal const string DisassemblyContentType = "Disassembly";
        internal const double SlowWarningThresholdSec = 0.4; // threshold to warn that actions are considered slow
        internal const double SlowShutdownThresholdSec = 4.0; // threshold to switch off components
        internal const int MaxNumberOfCharsInToolTips = 150;
        internal const int MsSleepBeforeAsyncExecution = 1000;

        #endregion Global Constants

        public AsmDudePackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=========================================\nINFO: AsmDudePackage: Entering constructor"));
            AsmDudeToolsStatic.Output_INFO("AsmDudePackage: Entering constructor");
        }

        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            //await base.InitializeAsync(cancellationToken, progress).ConfigureAwait(true);

            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ClearMefCache.ClearMefCache.Initialize(this);
            //await MyToolWindowCommand.InitializeAsync(this);
        }

        #region Disassembly window experiments
        /*
        private void disassemblyWindow() {
            //IDebugDisassemblyStream2
            //https://msdn.microsoft.com/en-us/library/bb145934(v=vs.110).aspx


            DTE vsEnvironment = (DTE)GetService(typeof(SDTE));
            vsEnvironment.ExecuteCommand("Debug.Disassembly");

            //IVsDebugger4 debugger = (IVsDebugger4)GetService(typeof(IVsDebugger));

            //https://social.msdn.microsoft.com/Forums/vstudio/en-US/8fa0b4bc-7d75-452c-b1ca-b0bf6385baf0/displaying-data-tooltip-or-quickinfo-in-debug-mode?forum=vsx
        }
        */
        #endregion

        #region Font Change Experiments
        /// <summary>
        /// Set font of code completion
        /// tools>options>Environment>Fonts and Colors>statement completion>courier new.
        /// https://msdn.microsoft.com/en-us/library/bb166382.aspx
        /// </summary>
        ///

        /*
        private void changeFontAutoComplete() {
            // experiments to change the font of the autocomplete
            try {
                DTE vsEnvironment = (DTE)GetService(typeof(SDTE));

                if (false) { // test to retrieve asm dude properties
                    EnvDTE.Properties asmDudePropertiesList = vsEnvironment.get_Properties("AsmDude", "Asm Documentation");
                    if (asmDudePropertiesList != null) {
                        string url = asmDudePropertiesList.Item("_asmDocUrl").Value as string;
                        AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:changeFontAutoComplete. url=", this.ToString(), url));
                    }
                }
                if (false) {
                    EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("TextEditor", "Basic");
                    if (propertiesList != null) {

                        Property tabSize = propertiesList.Item("TabSize");
                        short oldSize = (short)tabSize.Value;

                        string message;
                        if (oldSize != 4) {
                            tabSize.Value = 4;
                            message = string.Format(CultureInfo.CurrentUICulture,
                                "For Basic, the Text Editor had a tab size of {0}" +
                                " and now has a tab size of {1}.", oldSize, tabSize.Value);
                        } else {
                            message = string.Format(CultureInfo.CurrentUICulture,
                                "For Basic, the Text Editor has a tab size of {0}.", tabSize.Value);
                        }
                        AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, message));
                    }
                }
                if (false) {
                    //EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "Keyboard");
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));

                    EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "FontsAndColors");
                    if (propertiesList != null) {
                        AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString()));
                    }
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));
                }
                if (true) {
                    //EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "Keyboard");
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));

                    EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "Fonts and Colors");
                    if (propertiesList != null) {
                        AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString()));
                    }
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));
                }



            } catch (Exception e) {
                AsmDudeToolsStatic.Output_INFO(string.Format(CultureInfo.CurrentCulture, "ERROR: {0}:changeFontAutoComplete {1}", this.ToString(), e.Message));
            }
        }
        */
        #endregion

        #region Menus and Commands Actions
        /*
        private void initMenus() {
            // Now get the OleCommandService object provided by the MPF; this object is the one
            // responsible for handling the collection of commands implemented by the package.
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null == mcs) {
                AsmDudeToolsStatic.Output_WARNING("could not retrieve the IMenuCommandService.");
            } else {
                //AsmDudeToolsStatic.Output_INFO("retrieved the IMenuCommandService.");
                // Now create one object derived from MenuCommand for each command defined in
                // the VSCT file and add it to the command service.

                // For each command we have to define its id that is a unique Guid/integer pair.
                CommandID id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidMyCommand);
                // Now create the OleMenuCommand object for this command. The EventHandler object is the
                // function that will be called when the user will select the command.
                OleMenuCommand command = new OleMenuCommand(new EventHandler(MenuCommandCallback), id);
                // Add the command to the command service.
                mcs.AddCommand(command);


                // Create the MenuCommand object for the command placed in the main toolbar.
                id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidMyGraph);
                command = new OleMenuCommand(new EventHandler(GraphCommandCallback), id);
                mcs.AddCommand(command);

                // Create the MenuCommand object for the command placed in our toolbar.
                id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidMyZoom);
                command = new OleMenuCommand(new EventHandler(ZoomCommandCallback), id);
                mcs.AddCommand(command);

                // Create the DynamicMenuCommand object for the command defined with the TextChanges
                // flag.
                id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidDynamicTxt);
                command = new DynamicTextCommand(id, VsPackage.ResourceManager.GetString("DynamicTextBaseText"));
                mcs.AddCommand(command);

                // Now create two OleMenuCommand objects for the two commands with dynamic visibility
                id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidDynVisibility1);
                dynamicVisibilityCommand1 = new OleMenuCommand(new EventHandler(DynamicVisibilityCallback), id);
                mcs.AddCommand(dynamicVisibilityCommand1);

                id = new CommandID(Guids.guidMenuAndCommandsCmdSet, PkgCmdIDList.cmdidDynVisibility2);
                dynamicVisibilityCommand2 = new OleMenuCommand(new EventHandler(DynamicVisibilityCallback), id);
                // This command is the one that is invisible by default, so we have to set its visible
                // property to false because the default value of this property for every object derived
                // from MenuCommand is true.
                dynamicVisibilityCommand2.Visible = false;
                mcs.AddCommand(dynamicVisibilityCommand2);

            }
        }

        /// <summary>
        /// This function prints text on the debug ouput and on the generic pane of the
        /// Output window.
        /// </summary>
        /// <param name="text"></param>
        private void OutputCommandString(string text) {
            // Build the string to write on the debugger and Output window.
            StringBuilder outputText = new StringBuilder();
            outputText.Append(" ================================================\n");
            outputText.AppendFormat("  MenuAndCommands: {0}\n", text);
            outputText.Append(" ================================================\n\n");

            IVsOutputWindowPane windowPane = (IVsOutputWindowPane)GetService(typeof(SVsGeneralOutputWindowPane));
            if (null == windowPane) {
                Debug.WriteLine("Failed to get a reference to the Output window General pane");
                return;
            }
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.OutputString(outputText.ToString()))) {
                Debug.WriteLine("Failed to write on the Output window");
            }
        }

        /// <summary>
        /// Event handler called when the user selects the Sample command.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void MenuCommandCallback(object caller, EventArgs args) {
            OutputCommandString("Sample Command Callback.");
        }

        /// <summary>
        /// Event handler called when the user selects the Graph command.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void GraphCommandCallback(object caller, EventArgs args) {
            OutputCommandString("Graph Command Callback.");
        }

        /// <summary>
        /// Event handler called when the user selects the Zoom command.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", MessageId = "Microsoft.Samples.VisualStudio.MenuCommands.MenuCommandsPackage.OutputCommandString(System.String)")]
        private void ZoomCommandCallback(object caller, EventArgs args) {
            OutputCommandString("Zoom Command Callback.");
        }

        /// <summary>
        /// Event handler called when the user selects one of the two menus with
        /// dynamic visibility.
        /// </summary>
        private void DynamicVisibilityCallback(object caller, EventArgs args) {
            // This callback is supposed to be called only from the two menus with dynamic visibility
            // defined inside this package, so first we have to verify that the caller is correct.

            // Check that the type of the caller is the expected one.
            OleMenuCommand command = caller as OleMenuCommand;
            if (null == command)
                return;

            // Now check the command set.
            if (command.CommandID.Guid != Guids.guidMenuAndCommandsCmdSet)
                return;

            // This is one of our commands. Now what we want to do is to switch the visibility status
            // of the two menus with dynamic visibility, so that if the user clicks on one, then this
            // will make it invisible and the other one visible.
            if (command.CommandID.ID == PkgCmdIDList.cmdidDynVisibility1) {
                // The user clicked on the first one; make it invisible and show the second one.
                dynamicVisibilityCommand1.Visible = false;
                dynamicVisibilityCommand2.Visible = true;
            } else if (command.CommandID.ID == PkgCmdIDList.cmdidDynVisibility2) {
                // The user clicked on the second one; make it invisible and show the first one.
                dynamicVisibilityCommand2.Visible = false;
                dynamicVisibilityCommand1.Visible = true;
            }
        }
        */
        #endregion

        #region OptionPage getters
        /*
        public OptionsPageCodeCompletion OptionsPageCodeCompletion {
            get {
                return GetDialogPage(typeof(OptionsPageCodeCompletion)) as OptionsPageCodeCompletion;
            }
        }
        public OptionsPageSyntaxHighlighting OptionsPageSyntaxHighlighting {
            get {
                return GetDialogPage(typeof(OptionsPageSyntaxHighlighting)) as OptionsPageSyntaxHighlighting;
            }
        }
        public OptionsPageCodeFolding OptionsPageCodeFolding {
            get {
                return GetDialogPage(typeof(OptionsPageCodeFolding)) as OptionsPageCodeFolding;
            }
        }
        public OptionsPageAsmDoc OptionsPageAsmDoc {
            get {
                return GetDialogPage(typeof(OptionsPageAsmDoc)) as OptionsPageAsmDoc;
            }
        }
        public OptionsPageKeywordHighlighting OptionsPageKeywordHighlighting {
            get {
                return GetDialogPage(typeof(OptionsPageKeywordHighlighting)) as OptionsPageKeywordHighlighting;
            }
        }
        */
        #endregion
    }
}
