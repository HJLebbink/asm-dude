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

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO.Pipelines;
using System.Security.AccessControl;
using System.Security.Principal;
using Nerdbank.Streams;

namespace AsmDude2;

/// <summary>
/// Language server provider for assembly language files
/// </summary>
[VisualStudioContribution]
internal class AsmLanguageServerProvider : LanguageServerProvider
{
    private Process? _languageServerProcess;

    public AsmLanguageServerProvider(ExtensionCore extensionCore, VisualStudioExtensibility extensibility)
        : base(extensionCore, extensibility)
    {
    }

    /// <summary>
    /// Configures the language server provider
    /// </summary>
    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "AsmDude2 Language Server",
        new[]
        {
            DocumentFilter.FromDocumentType(AsmDocumentTypes.AsmDocumentType),
            DocumentFilter.FromDocumentType(AsmDocumentTypes.CodDocumentType),
            DocumentFilter.FromDocumentType(AsmDocumentTypes.IncDocumentType),
            DocumentFilter.FromDocumentType(AsmDocumentTypes.SDocumentType),
        });

    /// <summary>
    /// Creates the connection to the language server
    /// </summary>
    public override Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IDuplexPipe?>(async () =>
        {
            try
            {
                // Find the LSP server executable
                string? extensionDir = Path.GetDirectoryName(typeof(AsmLanguageServerProvider).Assembly.Location);
                if (extensionDir == null)
                {
                    Debug.WriteLine("AsmDude2: Could not determine extension directory");
                    return null;
                }

                string lspPath = Path.Combine(extensionDir, "Server", "AsmDude2.LSP.exe");

                if (!File.Exists(lspPath))
                {
                    Debug.WriteLine($"AsmDude2: LSP server not found at {lspPath}");
                    return null;
                }

                Debug.WriteLine($"AsmDude2: Starting LSP server from {lspPath}");

                // Create named pipes for communication
                const string stdInPipeName = "output";
                const string stdOutPipeName = "input";

                // Set up pipe security (allow all users)
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                PipeAccessRule pipeAccessRule = new PipeAccessRule(everyone, PipeAccessRights.ReadWrite, AccessControlType.Allow);
                PipeSecurity pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(pipeAccessRule);

                const int bufferSize = 256;

                // Create named pipes using the .NET 8.0 API
                var readerPipe = NamedPipeServerStreamAcl.Create(
                    stdInPipeName,
                    PipeDirection.InOut,
                    4,
                    PipeTransmissionMode.Message,
                    System.IO.Pipes.PipeOptions.Asynchronous,
                    bufferSize,
                    bufferSize,
                    pipeSecurity);

                var writerPipe = NamedPipeServerStreamAcl.Create(
                    stdOutPipeName,
                    PipeDirection.InOut,
                    4,
                    PipeTransmissionMode.Message,
                    System.IO.Pipes.PipeOptions.Asynchronous,
                    bufferSize,
                    bufferSize,
                    pipeSecurity);

                // Start the LSP server process
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = lspPath,
                    WorkingDirectory = Path.GetDirectoryName(lspPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                _languageServerProcess = Process.Start(startInfo);

                if (_languageServerProcess == null)
                {
                    Debug.WriteLine("AsmDude2: Failed to start LSP server process");
                    return null;
                }

                Debug.WriteLine($"AsmDude2: LSP server process started with PID {_languageServerProcess.Id}");

                // Wait for the LSP server to connect to the pipes
                await readerPipe.WaitForConnectionAsync(cancellationToken);
                await writerPipe.WaitForConnectionAsync(cancellationToken);

                Debug.WriteLine("AsmDude2: LSP server connected via named pipes");

                // Return a duplex pipe for bidirectional communication
                return new DuplexPipe(readerPipe, writerPipe);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AsmDude2: Error creating server connection: {ex}");
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Cleanup when the provider is disposed
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (_languageServerProcess != null && !_languageServerProcess.HasExited)
                {
                    _languageServerProcess.Kill();
                    _languageServerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AsmDude2: Error disposing language server process: {ex}");
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Simple duplex pipe implementation
    /// </summary>
    private class DuplexPipe : IDuplexPipe
    {
        public DuplexPipe(Stream input, Stream output)
        {
            Input = input.UsePipeReader();
            Output = output.UsePipeWriter();
        }

        public PipeReader Input { get; }
        public PipeWriter Output { get; }
    }
}
