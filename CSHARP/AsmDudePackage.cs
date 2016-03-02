
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;

using AsmDude.OptionsPage;

namespace AsmDude {
    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]

    [InstalledProductRegistration("Asm-Dude", "Asm-Dude description here", "1.4.0")] // for the help about information

    [ProvideOptionPageAttribute(typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion", 100, 101, true, new string[] { "Change Code Completion Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageCodeCompletion), "AsmDude", "Code Completion Options", 100, 101, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting", 100, 102, true, new string[] { "Change Syntax Highlighting Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageSyntaxHighlighting), "AsmDude", "Syntax Highlighting Options", 100, 102, true)]

    [ProvideOptionPageAttribute(typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding", 100, 103, true, new string[] { "Change Code Folding Options" })]
    [ProvideProfileAttribute(   typeof(OptionsPageCodeFolding), "AsmDude", "Code Folding Options", 100, 103, true)]


    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(GuidStrings.GuidPackage)]
    public class AsmDudePackage : Package {

        public AsmDudePackage() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=============================================\n=============================================\nINFO: Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// Initialization of the package.  This is where you should put all initialization
        /// code that depends on VS services.
        /// </summary>
        protected override void Initialize() {
            base.Initialize();
            
            //Debug.WriteLine("=============================================\n=============================================");
            //Debug.WriteLine("=============================================\n=============================================");
            Debug.WriteLine("INFO: AsmDude: q11: Initialize");
            //Debug.WriteLine("=============================================\n=============================================");
            //Debug.WriteLine("=============================================\n=============================================");
            //System.Threading.Thread.Sleep(2000);
            // TODO: add initialization code here
        }
    }
}
