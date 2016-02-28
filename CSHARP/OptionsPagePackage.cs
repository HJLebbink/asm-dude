/***************************************************************************
 
Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using System.Globalization;

namespace Microsoft.Samples.VisualStudio.IDE.OptionsPage
{
    /// <summary>
    /// This class implements a Visual Studio package that is registered for the Visual Studio IDE.
    /// The package class uses a number of registration attributes to specify integration parameters.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideOptionPageAttribute(typeof(OptionsPageGeneral),"My Options Page (C#)","General", 100, 101, true, new string[] { "Change sample general options (C#)" })]
    [ProvideProfileAttribute(typeof(OptionsPageGeneral), "My Options Page (C#)", "General Options", 100, 101, true, DescriptionResourceID = 100)]
    [ProvideOptionPageAttribute(typeof(OptionsPageCustom), "My Options Page (C#)", "Custom", 100, 102, true, new string[] { "Change sample custom options (C#)" })]
    [InstalledProductRegistration("My Options Page (C#)", "My Options Page (C#) Sample", "1.0")]
    [ProvideAutoLoad(UIContextGuids.NoSolution)] //load this package once visual studio starts.
    [Guid(GuidStrings.GuidPackage)]
    public class OptionsPagePackageCS : Package
    {
        public OptionsPagePackageCS() {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=============================================\nINFO:Entering constructor for: {0}", this.ToString()));
        }

        /// <summary>
        /// Initialization of the package.  This is where you should put all initialization
        /// code that depends on VS services.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            Debug.WriteLine("=============================================\nINFO:AsmDude:OptionsPagePackage: Initialize");
            // TODO: add initialization code here
        }
    }
}
