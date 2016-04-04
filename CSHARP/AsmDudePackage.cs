
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;

using AsmDude.OptionsPage;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel.Composition;
using EnvDTE;

namespace AsmDude {

    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    /// 
    // TODO to troubleshoot packaging problemsn, see https://blogs.msdn.microsoft.com/visualstudio/2010/03/22/troubleshooting-pkgdef-files/#registrycollision 
    [PackageRegistration(UseManagedResourcesOnly = true)]
    //[ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(GuidStrings.GuidPackage)]

    [InstalledProductRegistration("AsmDude", GuidStrings.Description, GuidStrings.Version)] // for the help about information

    [ProvideOptionPage(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion", 100, 101, true, new string[] { "Change Code Completion Options" })]
    //[ProvideProfile(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion Options", 100, 101, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting", 100, 102, true, new string[] { "Change Syntax Highlighting Options" })]
    //[ProvideProfile(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting Options", 100, 102, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding", 100, 103, true, new string[] { "Change Code Folding Options" })]
    //[ProvideProfile(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding Options", 100, 103, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation", 100, 104, true, new string[] { "Change Asm Documentation Options" })]
    //[ProvideProfile(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation Options", 100, 104, isToolsOptionPage: false, DescriptionResourceID = 100)]

    [ProvideOptionPage(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting", 100, 105, true, new string[] { "Change Asm Documentation Options" })]
    //[ProvideProfile(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting Options", 100, 105, isToolsOptionPage:false, DescriptionResourceID = 100)]

    [Export]
    public class AsmDudePackage : Package {

        public const string AsmDudeContentType = "asm!";
        public const double slowWarningThresholdSec = 0.0; // threshold to warn that actions are considered slow




        public AsmDudePackage() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// Initialization of the package.  This is where you should put all initialization
        /// code that depends on VS services.
        /// </summary>
        protected override void Initialize() {
            base.Initialize();

            Assembly thisAssem = typeof(AsmDudePackage).Assembly;
            AssemblyName thisAssemName = thisAssem.GetName();
            Version ver = thisAssemName.Version;

            AsmDudeToolsStatic.Output(" _____           ____        _     ");
            AsmDudeToolsStatic.Output("|  _  |___ _____|    \\ _ _ _| |___ ");
            AsmDudeToolsStatic.Output("|     |_ -|     |  |  | | | . | -_|");
            AsmDudeToolsStatic.Output("|__|__|___|_|_|_|____/|___|___|___|");

            AsmDudeToolsStatic.Output(string.Format("INFO: Loaded AsmDude version {0}.", ver));
            AsmDudeToolsStatic.Output(string.Format("INFO: Open source assembly plugin. To make programming assembly bearable."));
            AsmDudeToolsStatic.Output(string.Format("INFO: More info at https://github.com/HJLebbink/asm-dude"));
            AsmDudeToolsStatic.Output("----------------------------------");

            AsmDudeToolsStatic.Output(string.Format("INFO: Is the Tools>Options>AsmDude options pane invisible? Disable and enable this plugin to make it visible again..."));
            this.changeFontAutoComplete();
        }

        /// <summary>
        /// Set font of code completion
        /// tools>options>Environment>Fonts and Colors>statement completion>courier new.
        /// https://msdn.microsoft.com/en-us/library/bb166382.aspx
        /// </summary>
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

        #region OptionPage getters
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
        #endregion
    }
}
