
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;

using AsmDude.OptionsPage;

namespace AsmDude
{
    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]

    [InstalledProductRegistration("Asm-Dude", "Asm-Dude description here", "1.2.4")] // for the help about information

    [ProvideOptionPageAttribute(typeof(OptionsPageCodeCompletion),"AsmDude","General", 100, 101, true, new string[] { "Change sample general options (C#)" })]
    [ProvideProfileAttribute(typeof(OptionsPageCodeCompletion), "AsmDude", "General Options", 100, 101, true, DescriptionResourceID = 100)]
    [ProvideOptionPageAttribute(typeof(OptionsPageCustom), "AsmDude", "Custom", 100, 102, true, new string[] { "Change sample custom options (C#)" })]
    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(GuidStrings.GuidPackage)]
    public class AsmDudePackage : Package
    {
        public AsmDudePackage() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=============================================\n=============================================\nINFO: Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// Initialization of the package.  This is where you should put all initialization
        /// code that depends on VS services.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            Debug.WriteLine("=============================================\n=============================================");
            Debug.WriteLine("=============================================\n=============================================");
            Debug.WriteLine("INFO: AsmDude: q9: Initialize");
            Debug.WriteLine("=============================================\n=============================================");
            Debug.WriteLine("=============================================\n=============================================");
            System.Threading.Thread.Sleep(2000);

            // TODO: add initialization code here
        }
    }
}
