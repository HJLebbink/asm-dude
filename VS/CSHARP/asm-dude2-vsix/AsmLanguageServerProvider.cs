// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.IO.Pipelines;
using System.Security.AccessControl;
using System.Security.Principal;
using Nerdbank.Streams;

namespace AsmDude2;

/// <summary>
/// Language server provider for assembly language files
/// </summary>
internal class AsmLanguageServerProvider(ExtensionCore extensionCore, VisualStudioExtensibility extensibility) : LanguageServerProvider(extensionCore, extensibility)
{
    private Process? languageServerProcess;

    /// <summary>
    /// Configures the language server provider
    /// </summary>
    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "%AsmDude3.LanguageServerDisplayName%",
        [
            DocumentFilter.FromDocumentType(AsmDocumentTypes.AsmDocumentType),
        ]);

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
                    Debug.WriteLine("AsmDude3: Could not determine extension directory");
                    return null;
                }

                string lspPath = Path.Combine(extensionDir, "Server", "AsmDude2.LSP.exe");

                if (!File.Exists(lspPath))
                {
                    Debug.WriteLine($"AsmDude3: LSP server not found at {lspPath}");
                    return null;
                }

                Debug.WriteLine($"AsmDude3: Starting LSP server from {lspPath}");

                // Create named pipes for communication
                const string stdInPipeName = "asmdude2-output";
                const string stdOutPipeName = "asmdude2-input";

                // Set up pipe security (allow all users)
                SecurityIdentifier everyone = new(WellKnownSidType.WorldSid, null);
                PipeAccessRule pipeAccessRule = new(everyone, PipeAccessRights.ReadWrite, AccessControlType.Allow);
                PipeSecurity pipeSecurity = new();
                pipeSecurity.AddAccessRule(pipeAccessRule);

                const int bufferSize = 256;

                // Create named pipes using the .NET 10.0 API
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
                ProcessStartInfo startInfo = new()
                {
                    FileName = lspPath,
                    WorkingDirectory = Path.GetDirectoryName(lspPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                languageServerProcess = Process.Start(startInfo);

                if (languageServerProcess == null)
                {
                    Debug.WriteLine("AsmDude2: Failed to start LSP server process");
                    return null;
                }

                Debug.WriteLine($"AsmDude2: LSP server process started with PID {languageServerProcess.Id}");

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
                if (languageServerProcess != null && !languageServerProcess.HasExited)
                {
                    languageServerProcess.Kill();
                    languageServerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AsmDude3: Error disposing language server process: {ex}");
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Simple duplex pipe implementation
    /// </summary>
    private class DuplexPipe(Stream input, Stream output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input.UsePipeReader();
        public PipeWriter Output { get; } = output.UsePipeWriter();
    }
}
