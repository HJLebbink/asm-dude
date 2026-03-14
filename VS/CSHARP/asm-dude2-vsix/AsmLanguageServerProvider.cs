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
[VisualStudioContribution]
public class AsmLanguageServerProvider(ExtensionCore extensionCore, VisualStudioExtensibility extensibility) : LanguageServerProvider(extensionCore, extensibility)
{
    private Process? languageServerProcess;

    static AsmLanguageServerProvider()
    {
        try
        {
            var asm = typeof(AsmLanguageServerProvider).Assembly;
            var asmLocation = asm.Location;
            var buildTime = File.GetLastWriteTimeUtc(asmLocation);
            Debug.WriteLine($"AsmDude2.VSIX LOADED: Assembly={Path.GetFileName(asmLocation)}, BuildTime={buildTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AsmDude2.VSIX BUILD INFO ERROR: {ex.Message}");
        }
    }

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
    private static void LogError(string message)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "AsmDude2_LSP_Error.log");
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
        }
        catch { }
    }

    public override Task<IDuplexPipe?> CreateServerConnectionAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IDuplexPipe?>(async () =>
        {
            try
            {
                // Find LSP server - try multiple paths
                string? lspPath = null;

                // Log diagnostic information
                LogError("CreateServerConnectionAsync starting");
                LogError($"AppContext.BaseDirectory = {AppContext.BaseDirectory}");
                LogError($"Assembly.Location = {typeof(AsmLanguageServerProvider).Assembly.Location}");
                LogError($"AppDomain.CurrentDomain.BaseDirectory = {AppDomain.CurrentDomain.BaseDirectory}");

                // Try 1: AppContext.BaseDirectory/Server/AsmDude2.LSP.exe
                string candidate1 = Path.Combine(AppContext.BaseDirectory, "Server", "AsmDude2.LSP.exe");
                LogError($"Trying path 1: {candidate1}");
                if (File.Exists(candidate1))
                {
                    lspPath = candidate1;
                    LogError($"Found LSP server at path 1");
                }
                else
                {
                    LogError($"Path 1 does not exist");
                }

                // Try 2: AppDomain.CurrentDomain.BaseDirectory + Server
                if (lspPath == null)
                {
                    string candidate2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Server", "AsmDude2.LSP.exe");
                    Debug.WriteLine($"AsmDude2: Trying path 2: {candidate2}");
                    if (File.Exists(candidate2))
                    {
                        lspPath = candidate2;
                        Debug.WriteLine($"AsmDude2: Found LSP server at path 2");
                    }
                }

                // Try 3: Assembly location based (if not empty)
                if (lspPath == null)
                {
                    var assemblyPath = typeof(AsmLanguageServerProvider).Assembly.Location;
                    if (!string.IsNullOrEmpty(assemblyPath))
                    {
                        string dir = Path.GetDirectoryName(assemblyPath) ?? "";
                        string candidate3 = Path.Combine(dir, "Server", "AsmDude2.LSP.exe");
                        Debug.WriteLine($"AsmDude2: Trying path 3: {candidate3}");
                        if (File.Exists(candidate3))
                        {
                            lspPath = candidate3;
                            Debug.WriteLine($"AsmDude2: Found LSP server at path 3");
                        }
                    }
                }

                // Try 4: Search in subdirectories (last resort)
                if (lspPath == null)
                {
                    try
                    {
                        var exeFiles = Directory.GetFiles(AppContext.BaseDirectory, "AsmDude2.LSP.exe", SearchOption.AllDirectories);
                        if (exeFiles.Length > 0)
                        {
                            lspPath = exeFiles[0];
                            Debug.WriteLine($"AsmDude2: Found LSP server via directory search: {lspPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AsmDude2: Directory search failed: {ex.Message}");
                    }
                }

                if (lspPath == null)
                {
                    Debug.WriteLine($"AsmDude2: LSP server NOT FOUND after all attempts");
                    Debug.WriteLine($"AsmDude2: AppContext.BaseDirectory={AppContext.BaseDirectory}");
                    Debug.WriteLine($"AsmDude2: AppDomain.CurrentDomain.BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}");
                    return null;
                }

                Debug.WriteLine($"AsmDude2: Starting LSP server from {lspPath}");

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
                    WorkingDirectory = Path.GetDirectoryName(lspPath) ?? "",
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
                LogError($"ERROR creating server connection: {ex.GetType().Name}: {ex.Message}");
                LogError($"StackTrace: {ex.StackTrace}");
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
