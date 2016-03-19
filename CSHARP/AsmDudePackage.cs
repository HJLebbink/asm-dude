
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;

using AsmDude.OptionsPage;
using System.Globalization;
using System.Reflection;

namespace AsmDude {
    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]

    [InstalledProductRegistration("AsmDude", GuidStrings.Description, GuidStrings.Version)] // for the help about information

    [ProvideOptionPageAttribute(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion", 100, 101, true, new string[] { "Change Code Completion Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion Options", 100, 101, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting", 100, 102, true, new string[] { "Change Syntax Highlighting Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting Options", 100, 102, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding", 100, 103, true, new string[] { "Change Code Folding Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding Options", 100, 103, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation", 100, 104, true, new string[] { "Change Asm Documentation Options" })]
    [ProvideProfileAttribute(typeof(OptionsPageAsmDoc), "AsmDude", "Asm Documentation Options", 100, 104, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting", 100, 105, true, new string[] { "Change Asm Documentation Options" })]
    [ProvideProfileAttribute(typeof(OptionsPageKeywordHighlighting), "AsmDude", "Keyword Highlighting Options", 100, 105, true)]

    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(GuidStrings.GuidPackage)]
    public class AsmDudePackage : Package {

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

            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:Initializing version {1} of {2}", this.ToString(), ver, thisAssemName.Name));
        }
    }
}
