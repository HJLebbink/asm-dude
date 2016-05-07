
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using AsmDude.OptionsPage;
using System.Globalization;
using System.ComponentModel.Composition;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using AsmDude.ErrorSquiggles;

namespace AsmDude {

    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    /// 
    // TODO to troubleshoot packaging problems, see https://blogs.msdn.microsoft.com/visualstudio/2010/03/22/troubleshooting-pkgdef-files/#registrycollision 
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("AsmDude", Vsix.Description, Vsix.Version)] // for the help about information

    [ProvideMenuResource("Menus.ctmenu", 1)] // needed when showing menus
    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(Guids.GuidPackage_str)]
    [ComVisible(true)]


    [ProvideOptionPage(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting", 0, 0, true)]
    //[ProvideProfile(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting Options", 100, 102, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion", 0, 0, true)]
    //[ProvideProfile(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion Options", 100, 101, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding", 0, 0, true)]
    //[ProvideProfile(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding Options", 100, 103, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation", 0, 0, true)]
    //[ProvideProfile(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation Options", 100, 104, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting", 0, 0, true)]
    //[ProvideProfile(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting Options", 100, 105, isToolsOptionPage:false, DescriptionResourceID = 100)]

    public sealed class AsmDudePackage : Package {

        internal const string AsmDudeContentType = "asm!";
        internal const double slowWarningThresholdSec = 0.2; // threshold to warn that actions are considered slow
        internal const int maxNumberOfCharsInToolTips = 150;

        #region Member Variables
        //private OleMenuCommand dynamicVisibilityCommand1;
        //private OleMenuCommand dynamicVisibilityCommand2;
        #endregion

        public AsmDudePackage() {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=========================================\nINFO: AsmDudePackage: Entering constructor"));
            //AsmDudeToolsStatic.Output("INFO: AsmDudePackage: Entering constructor");
        }

        /// <summary>
        /// Initialization of the package.  This is where you should put all initialization
        /// code that depends on VS services.
        /// </summary>
        protected override void Initialize() {
            base.Initialize();
            //this.initMenus();
            //this.changeFontAutoComplete();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@" _____           ____        _     ");
            sb.AppendLine(@"|  _  |___ _____|    \ _ _ _| |___ ");
            sb.AppendLine(@"|     |_ -|     |  |  | | | . | -_|");
            sb.AppendLine(@"|__|__|___|_|_|_|____/|___|___|___|");
            sb.AppendLine(string.Format("INFO: Loaded AsmDude version {0}.", typeof(AsmDudePackage).Assembly.GetName().Version));
            sb.AppendLine(string.Format("INFO: Open source assembly extension. Making programming in assembler bearable."));
            //sb.AppendLine(string.Format("INFO: Why? Because programming assembly is an art which exists outside of contemporary computer science fads and fashions."));

            sb.AppendLine(string.Format("INFO: More info at https://github.com/HJLebbink/asm-dude"));
            sb.AppendLine("----------------------------------");
            AsmDudeToolsStatic.Output(sb.ToString());
        }

        #region Font Change Experiments
        /// <summary>
        /// Set font of code completion
        /// tools>options>Environment>Fonts and Colors>statement completion>courier new.
        /// https://msdn.microsoft.com/en-us/library/bb166382.aspx
        /// </summary>
        /// 
        /*

        private void changeFontAutoComplete() {
            // experiments to change the font of the autocomplate
            try {
                DTE vsEnvironment = (DTE)GetService(typeof(SDTE));

                if (false) { // test to retrieve asm dude properties
                    EnvDTE.Properties asmDudePropertiesList = vsEnvironment.get_Properties("AsmDude", "Asm Documentation");
                    if (asmDudePropertiesList != null) {
                        string url = asmDudePropertiesList.Item("_asmDocUrl").Value as string;
                        AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete. url=", this.ToString(), url));
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
                        AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, message));
                    }
                }
                if (false) {
                    //EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "Keyboard");
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));

                    EnvDTE.Properties propertiesList = vsEnvironment.get_Properties("Environment", "Fonts and Colors");
                    if (propertiesList != null) {
                        AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString()));
                    }
                    //EnvDTE.Property prop = propertiesList.Item("Scheme");
                    //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:changeFontAutoComplete; prop={1}", this.ToString(), prop.Value));
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "ERROR: {0}:changeFontAutoComplete {1}", this.ToString(), e.Message));
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
                AsmDudeToolsStatic.Output("WARNING: could not retrieve the IMenuCommandService.");
            } else {
                //AsmDudeToolsStatic.Output("INFO: retrieved the IMenuCommandService.");
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
