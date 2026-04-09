// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using Microsoft.VisualStudio.RpcContracts.LanguageServerProvider;
using Nerdbank.Streams;

// ARCHITECTURE NOTE — Hover Tooltips & Clickable Links
//
// The LSP server returns hover with _vs_rawContent (ClassifiedTextElement) for monospace font
// and colored keywords. This works in VS 2022 + 2026.
//
// Clickable links in hover tooltips are NOT possible over LSP — NavigationAction on
// ClassifiedTextRun is an Action delegate, not a URL string. Not serializable over JSON-RPC.
//
// DocumentLink was tried but VS creates an unwanted new document tab as a side effect.
// Mnemonic documentation URLs are shown in hover tooltips instead.

#pragma warning disable VSEXTPREVIEW_LSP // Type is for evaluation purposes only and is subject to change or removal in future updates.
[VisualStudioContribution]
internal class AsmLanguageServerProvider : LanguageServerProvider
{
    private static readonly string DiagLogFile = Path.Combine(Path.GetTempPath(), "AsmDude2-extension-diag.log");

    [VisualStudioContribution]
    public static DocumentTypeConfiguration AsmDocumentType => new("asm")
    {
        FileExtensions = [".asm"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    public static DocumentTypeConfiguration CodDocumentType => new("cod")
    {
        FileExtensions = [".cod"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    public static DocumentTypeConfiguration IncDocumentType => new("inc")
    {
        FileExtensions = [".inc"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    public static DocumentTypeConfiguration SDocumentType => new("s")
    {
        FileExtensions = [".s"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "AsmDude2 Language Server",
        [
            DocumentFilter.FromDocumentType(AsmDocumentType),
            DocumentFilter.FromDocumentType(CodDocumentType),
            DocumentFilter.FromDocumentType(IncDocumentType),
            DocumentFilter.FromDocumentType(SDocumentType),
        ]);

public override Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
        {
            string extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string serverExe = Path.Combine(extensionDir, "Server", LanguageServerConstants.ExecutableName);

        try
        {
            Log($"CreateServerConnectionAsync called (in-proc hybrid mode)");
            Log($"  Assembly location: {Assembly.GetExecutingAssembly().Location}");
            Log($"  Extension dir: {extensionDir}");
            Log($"  Server exe: {serverExe}, exists: {File.Exists(serverExe)}");

            ProcessStartInfo info = new()
            {
                FileName = serverExe,
                Arguments = "--stdio",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
#if DEBUG
                CreateNoWindow = false, // Show LSP server console window for debugging
#else
                CreateNoWindow = false, // TODO: set to true for final release
#endif
            };

            // The VS extension host sets DOTNET_ROOT to its private .NET 8 runtime,
            // which prevents the LSP server (targeting .NET 10) from finding its runtime.
            // Remove these environment overrides so the server uses the system-wide .NET 10.
            info.Environment.Remove("DOTNET_ROOT");
            info.Environment.Remove("DOTNET_ROOT(x86)");

            Log($"  DOTNET_ROOT cleared for child process");

#pragma warning disable CA2000 // The process is disposed after Visual Studio sends the stop command.
            Process process = new();
#pragma warning restore CA2000
            process.StartInfo = info;

            if (process.Start())
            {
                Log($"  Process started: PID={process.Id}");

                // Tell the pipe client which server PID to connect to for sim-state data
                SimStatePipeClient.Instance.SetServerPid(process.Id);

                return Task.FromResult<IDuplexPipe?>(new DuplexPipe(
                    PipeReader.Create(process.StandardOutput.BaseStream),
                    PipeWriter.Create(process.StandardInput.BaseStream)));
            }

            Log($"  ERROR: Process.Start() returned false");
            return Task.FromResult<IDuplexPipe?>(null);
        }
        catch (Exception ex)
        {
            Log($"  EXCEPTION: {ex}");
            return Task.FromResult<IDuplexPipe?>(null);
        }
    }

    public override Task OnServerInitializationResultAsync(ServerInitializationResult serverInitializationResult, LanguageServerInitializationFailureInfo? initializationFailureInfo, CancellationToken cancellationToken)
    {
        Log($"OnServerInitializationResultAsync: Result={serverInitializationResult}");
        if (initializationFailureInfo != null)
        {
            Log($"  FailureInfo: {initializationFailureInfo.StatusMessage}");
            Log($"  Exception: {initializationFailureInfo.Exception}");
        }

        if (serverInitializationResult == ServerInitializationResult.Failed)
        {
            this.Enabled = false;
        }

        return base.OnServerInitializationResultAsync(serverInitializationResult, initializationFailureInfo, cancellationToken);
    }

    private static void Log(string message)
    {
        try { File.AppendAllText(DiagLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } catch { }
    }
}
#pragma warning restore VSEXTPREVIEW_LSP
