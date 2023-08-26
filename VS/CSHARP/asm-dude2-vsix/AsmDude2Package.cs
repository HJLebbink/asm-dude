﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using AsmDude2.Tools;
using System.Globalization;
using System.Diagnostics;

namespace AsmDude2
{
    /// <summary>
    /// This package only loads when the AsmLanguageClient.UiContextGuidString UI context is set.  This ensures that this extension is only loaded when the language server is activated.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class AsmDude2Package : AsyncPackage
    {
        #region Global Constants
        public const string PackageGuidString = "4f792145-bd77-482d-bd0e-fcc7ac281c8d";
        internal const string AsmDudeContentType = "asm!";
        internal const string DisassemblyContentType = "Disassembly";
        internal const double SlowWarningThresholdSec = 0.4; // threshold to warn that actions are considered slow
        internal const double SlowShutdownThresholdSec = 4.0; // threshold to switch off components
        internal const int MaxNumberOfCharsInToolTips = 150;
        internal const int MsSleepBeforeAsyncExecution = 1000;
        #endregion Global Constants

        public AsmDude2Package()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=========================================\nINFO: AsmDude2Package: Entering constructor"));
            AsmDudeToolsStatic.Output_INFO("AsmDude2Package: Entering constructor");
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync();
            AsmDudeToolsStatic.Output_INFO("AsmDude2Package: InitializeAsync");
            CustomCommand.Initialize(this);
        }

        #endregion
    }
}
