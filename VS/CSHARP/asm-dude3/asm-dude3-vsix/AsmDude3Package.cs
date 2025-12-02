using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;

namespace AsmDude3;

/// <summary>
/// Package registration for AsmDude3
/// This ensures the extension loads properly and MEF components are activated
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("#110", "#112", "3.0.0", IconResourceID = 400)]
[ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class AsmDude3Package : AsyncPackage
{
    #region Global Constants
    public const string PackageGuidString = "e30816d4-86c9-4f3e-a3e5-c3e5a5a2e9f9";
    internal const string AsmDudeContentType = "asm!";
    #endregion

    public AsmDude3Package()
    {
        Debug.WriteLine("AsmDude3Package: constructor called");
    }

    protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        Debug.WriteLine("AsmDude3Package: InitializeAsync called");
    }
}
