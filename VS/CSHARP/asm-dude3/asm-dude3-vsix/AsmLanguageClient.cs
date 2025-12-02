using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AsmDude3;

/// <summary>
/// Language client for AsmDude3 - connects Visual Studio to the LSP server
/// </summary>
[ContentType(AsmDude3Package.AsmDudeContentType)]
[Export(typeof(ILanguageClient))]
public class AsmLanguageClient : ILanguageClient, IDisposable
{
    private Process? _serverProcess;

    /// <summary>
    /// Name displayed to the user
    /// </summary>
    public string Name => "AsmDude3 Language Client";

    /// <summary>
    /// Configuration sections (none for now)
    /// </summary>
    public IEnumerable<string>? ConfigurationSections => null;

    /// <summary>
    /// Files to watch for changes (none for now)
    /// </summary>
    public object? InitializationOptions => null;

    /// <summary>
    /// Custom initialization options (none for now)
    /// </summary>
    public IEnumerable<string>? FilesToWatch => null;

    /// <summary>
    /// Show notification if initialization fails
    /// </summary>
    public bool ShowNotificationOnInitializeFailed => true;

    /// <summary>
    /// Event raised when the language client should start
    /// </summary>
    public event AsyncEventHandler<EventArgs>? StartAsync;

    /// <summary>
    /// Event raised when the language client should stop
    /// </summary>
    public event AsyncEventHandler<EventArgs>? StopAsync;

    /// <summary>
    /// Called when the extension loads
    /// </summary>
    public async Task OnLoadedAsync()
    {
        System.Diagnostics.Debug.WriteLine("AsmDude3: OnLoadedAsync called");
        if (StartAsync != null)
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Activate the language server - launch process and establish connection
    /// </summary>
    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        System.Diagnostics.Debug.WriteLine("AsmDude3: ActivateAsync called - starting LSP server");
        await Task.Yield(); // Get off the UI thread

        try
        {
            // Get the server executable path
            var serverPath = GetServerExecutablePath();
            if (!File.Exists(serverPath))
            {
                throw new FileNotFoundException($"Language server not found at: {serverPath}");
            }

            // Configure server process
            var processInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = string.Empty,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(serverPath)
            };

            // Start the server process
            _serverProcess = Process.Start(processInfo);
            if (_serverProcess == null)
            {
                throw new InvalidOperationException("Failed to start language server process");
            }

            System.Diagnostics.Debug.WriteLine($"AsmDude3: Server process started, PID={_serverProcess.Id}");

            // Start capturing stderr immediately before process exits
            _serverProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // Filter out verbose JsonRpc Information logs - only show actual errors/warnings
                    if (!args.Data.Contains("JsonRpc Information") &&
                        !args.Data.Contains("Added local RPC method") &&
                        !args.Data.Contains("Invoking AsmDude3.Server"))
                    {
                        System.Diagnostics.Debug.WriteLine($"LSP Server: {args.Data}");
                    }
                }
            };
            _serverProcess.BeginErrorReadLine();

            // Check if process exited immediately
            await Task.Delay(500);
            if (_serverProcess.HasExited)
            {
                throw new InvalidOperationException($"Language server process exited immediately with code {_serverProcess.ExitCode}");
            }

            System.Diagnostics.Debug.WriteLine("AsmDude3: Creating connection from stdin/stdout");

            // Create connection from stdin/stdout
            var connection = new Connection(
                _serverProcess.StandardOutput.BaseStream,
                _serverProcess.StandardInput.BaseStream
            );

            System.Diagnostics.Debug.WriteLine("AsmDude3: Connection created successfully");
            return connection;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to activate language server: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Called when the server successfully initializes
    /// </summary>
    public Task OnServerInitializedAsync()
    {
        System.Diagnostics.Debug.WriteLine("AsmDude3 Language Server initialized successfully");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when server initialization fails (interface implementation)
    /// </summary>
    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AsmDude3 Language Server initialization failed. InitializationState: {initializationState}");
        }
        catch
        {
            // ignore
        }

        // Returning null uses default failure handling
        return Task.FromResult<InitializationFailureContext?>(null);
    }

    /// <summary>
    /// Legacy overload kept for compatibility
    /// </summary>
    public Task OnServerInitializeFailedAsync(Exception e)
    {
        System.Diagnostics.Debug.WriteLine($"AsmDude3 Language Server initialization failed: {e}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the path to the language server executable
    /// </summary>
    private string GetServerExecutablePath()
    {
        // Get the directory of this assembly (the VSIX)
        var extensionDir = Path.GetDirectoryName(typeof(AsmLanguageClient).Assembly.Location);
        if (string.IsNullOrEmpty(extensionDir))
        {
            throw new InvalidOperationException("Could not determine extension directory");
        }

        // Server is in the Server subdirectory
        var serverPath = Path.Combine(extensionDir, "Server", "asm-dude3-server.exe");

        return serverPath;
    }

    /// <summary>
    /// Cleanup when disposing
    /// </summary>
    public void Dispose()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                _serverProcess.Kill();
                _serverProcess.WaitForExit(5000);
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                _serverProcess?.Dispose();
                _serverProcess = null;
            }
        }
    }
}
