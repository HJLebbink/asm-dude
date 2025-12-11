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

    public AsmLanguageClient()
    {
        Instance = this;
    }

    /// <summary>
    /// Singleton instance for accessing the language client from options page
    /// </summary>
    internal static AsmLanguageClient? Instance { get; private set; }

    /// <summary>
    /// Name displayed to the user
    /// </summary>
    public string Name => "AsmDude3 Language Client";

    /// <summary>
    /// Configuration sections (none for now)
    /// </summary>
    public IEnumerable<string>? ConfigurationSections => null;

    /// <summary>
    /// Custom initialization options - send settings from VS to LSP server
    /// </summary>
    public object InitializationOptions
    {
        get
        {
            return new Dictionary<string, object?>
            {
                // Syntax Highlighting
                { "SyntaxHighlighting_Opcode", Settings.Default.SyntaxHighlighting_Opcode },
                { "SyntaxHighlighting_Register", Settings.Default.SyntaxHighlighting_Register },
                { "SyntaxHighlighting_Remark", Settings.Default.SyntaxHighlighting_Remark },
                { "SyntaxHighlighting_Directive", Settings.Default.SyntaxHighlighting_Directive },
                { "SyntaxHighlighting_Jump", Settings.Default.SyntaxHighlighting_Jump },
                { "SyntaxHighlighting_Label", Settings.Default.SyntaxHighlighting_Label },
                { "SyntaxHighlighting_Constant", Settings.Default.SyntaxHighlighting_Constant },
                { "SyntaxHighlighting_Misc", Settings.Default.SyntaxHighlighting_Misc },
                { "SyntaxHighlighting_On", Settings.Default.SyntaxHighlighting_On },
                { "SyntaxHighlighting_Opcode_Italic", Settings.Default.SyntaxHighlighting_Opcode_Italic },
                { "SyntaxHighlighting_Register_Italic", Settings.Default.SyntaxHighlighting_Register_Italic },
                { "SyntaxHighlighting_Remark_Italic", Settings.Default.SyntaxHighlighting_Remark_Italic },
                { "SyntaxHighlighting_Directive_Italic", Settings.Default.SyntaxHighlighting_Directive_Italic },
                { "SyntaxHighlighting_Constant_Italic", Settings.Default.SyntaxHighlighting_Constant_Italic },
                { "SyntaxHighlighting_Jump_Italic", Settings.Default.SyntaxHighlighting_Jump_Italic },
                { "SyntaxHighlighting_Label_Italic", Settings.Default.SyntaxHighlighting_Label_Italic },
                { "SyntaxHighlighting_Misc_Italic", Settings.Default.SyntaxHighlighting_Misc_Italic },
                { "SyntaxHighlighting_Userdefined1", Settings.Default.SyntaxHighlighting_Userdefined1 },
                { "SyntaxHighlighting_Userdefined2", Settings.Default.SyntaxHighlighting_Userdefined2 },
                { "SyntaxHighlighting_Userdefined3", Settings.Default.SyntaxHighlighting_Userdefined3 },
                { "SyntaxHighlighting_Userdefined1_Italic", Settings.Default.SyntaxHighlighting_Userdefined1_Italic },
                { "SyntaxHighlighting_Userdefined2_Italic", Settings.Default.SyntaxHighlighting_Userdefined2_Italic },
                { "SyntaxHighlighting_Userdefined3_Italic", Settings.Default.SyntaxHighlighting_Userdefined3_Italic },
                { "CodeFolding_On", Settings.Default.CodeFolding_On },
                { "CodeFolding_BeginTag", Settings.Default.CodeFolding_BeginTag },
                { "CodeFolding_EndTag", Settings.Default.CodeFolding_EndTag },
                { "CodeCompletion_On", Settings.Default.CodeCompletion_On },
                { "SignatureHelp_On", Settings.Default.SignatureHelp_On },
                { "AsmDoc_Url", Settings.Default.AsmDoc_Url },
                { "AsmDoc_On", Settings.Default.AsmDoc_On },
                { "PerformanceInfo_On", Settings.Default.PerformanceInfo_On },
                { "PerformanceInfo_SandyBridge_On", Settings.Default.PerformanceInfo_SandyBridge_On },
                { "PerformanceInfo_IvyBridge_On", Settings.Default.PerformanceInfo_IvyBridge_On },
                { "PerformanceInfo_Haswell_On", Settings.Default.PerformanceInfo_Haswell_On },
                { "PerformanceInfo_Broadwell_On", Settings.Default.PerformanceInfo_Broadwell_On },
                { "PerformanceInfo_Skylake_On", Settings.Default.PerformanceInfo_Skylake_On },
                { "PerformanceInfo_SkylakeX_On", Settings.Default.PerformanceInfo_SkylakeX_On },
                { "PerformanceInfo_KnightsLanding_On", Settings.Default.PerformanceInfo_KnightsLanding_On },
                { "useAssemblerMasm", Settings.Default.useAssemblerMasm },
                { "useAssemblerNasm", Settings.Default.useAssemblerNasm },
                { "useAssemblerNasm_Att", Settings.Default.useAssemblerNasm_Att },
                { "useAssemblerAutoDetect", Settings.Default.useAssemblerAutoDetect },
                { "useAssemblerDisassemblyMasm", Settings.Default.useAssemblerDisassemblyMasm },
                { "useAssemblerDisassemblyNasm_Att", Settings.Default.useAssemblerDisassemblyNasm_Att },
                { "useAssemblerDisassemblyAutoDetect", Settings.Default.useAssemblerDisassemblyAutoDetect },
                { "IntelliSense_Label_Analysis_On", Settings.Default.IntelliSense_Label_Analysis_On },
                { "IntelliSense_Show_Undefined_Labels", Settings.Default.IntelliSense_Show_Undefined_Labels },
                { "IntelliSense_Decorate_Undefined_Labels", Settings.Default.IntelliSense_Decorate_Undefined_Labels },
                { "IntelliSense_Show_Clashing_Labels", Settings.Default.IntelliSense_Show_Clashing_Labels },
                { "IntelliSense_Decorate_Clashing_Labels", Settings.Default.IntelliSense_Decorate_Clashing_Labels },
                { "IntelliSense_Show_Undefined_Includes", Settings.Default.IntelliSense_Show_Undefined_Includes },
                { "IntelliSense_Decorate_Undefined_Includes", Settings.Default.IntelliSense_Decorate_Undefined_Includes },
                { "AsmSim_On", Settings.Default.AsmSim_On },
                { "AsmSim_Z3_Timeout_MS", Settings.Default.AsmSim_Z3_Timeout_MS },
                { "AsmSim_Number_Of_Threads", Settings.Default.AsmSim_Number_Of_Threads },
                { "AsmSim_64_Bits", Settings.Default.AsmSim_64_Bits },
                { "AsmSim_Show_Syntax_Errors", Settings.Default.AsmSim_Show_Syntax_Errors },
                { "AsmSim_Decorate_Syntax_Errors", Settings.Default.AsmSim_Decorate_Syntax_Errors },
                { "AsmSim_Show_Usage_Of_Undefined", Settings.Default.AsmSim_Show_Usage_Of_Undefined },
                { "AsmSim_Decorate_Usage_Of_Undefined", Settings.Default.AsmSim_Decorate_Usage_Of_Undefined },
                { "AsmSim_Show_Redundant_Instructions", Settings.Default.AsmSim_Show_Redundant_Instructions },
                { "AsmSim_Decorate_Redundant_Instructions", Settings.Default.AsmSim_Decorate_Redundant_Instructions },
                { "AsmSim_Show_Unreachable_Instructions", Settings.Default.AsmSim_Show_Unreachable_Instructions },
                { "AsmSim_Decorate_Unreachable_Instructions", Settings.Default.AsmSim_Decorate_Unreachable_Instructions },
                { "AsmSim_Decorate_Registers", Settings.Default.AsmSim_Decorate_Registers },
                { "AsmSim_Show_Register_In_Code_Completion", Settings.Default.AsmSim_Show_Register_In_Code_Completion },
                { "AsmSim_Show_Register_In_Instruction_Tooltip", Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip },
                { "AsmSim_Show_Register_In_Register_Tooltip", Settings.Default.AsmSim_Show_Register_In_Register_Tooltip },
                { "AsmSim_Show_Register_In_Code_Completion_Numeration", Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration },
                { "AsmSim_Show_Register_In_Instruction_Tooltip_Numeration", Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration },
                { "AsmSim_Show_Register_In_Register_Tooltip_Numeration", Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration },
                { "AsmSim_Decorate_Unimplemented", Settings.Default.AsmSim_Decorate_Unimplemented },
                { "AsmSim_Pragma_Assume", Settings.Default.AsmSim_Pragma_Assume },
                { "ARCH_8086", Settings.Default.ARCH_8086 },
                { "ARCH_186", Settings.Default.ARCH_186 },
                { "ARCH_286", Settings.Default.ARCH_286 },
                { "ARCH_386", Settings.Default.ARCH_386 },
                { "ARCH_486", Settings.Default.ARCH_486 },
                { "ARCH_MMX", Settings.Default.ARCH_MMX },
                { "ARCH_SSE", Settings.Default.ARCH_SSE },
                { "ARCH_SSE2", Settings.Default.ARCH_SSE2 },
                { "ARCH_SSE3", Settings.Default.ARCH_SSE3 },
                { "ARCH_SSSE3", Settings.Default.ARCH_SSSE3 },
                { "ARCH_SSE4_1", Settings.Default.ARCH_SSE4_1 },
                { "ARCH_SSE4_2", Settings.Default.ARCH_SSE4_2 },
                { "ARCH_SSE4A", Settings.Default.ARCH_SSE4A },
                { "ARCH_SSE5", Settings.Default.ARCH_SSE5 },
                { "ARCH_AVX", Settings.Default.ARCH_AVX },
                { "ARCH_AVX2", Settings.Default.ARCH_AVX2 },
                { "ARCH_AVX512_VL", Settings.Default.ARCH_AVX512_VL },
                { "ARCH_AVX512_PF", Settings.Default.ARCH_AVX512_PF },
                { "ARCH_AVX512_DQ", Settings.Default.ARCH_AVX512_DQ },
                { "ARCH_AVX512_BW", Settings.Default.ARCH_AVX512_BW },
                { "ARCH_AVX512_ER", Settings.Default.ARCH_AVX512_ER },
                { "ARCH_AVX512_F", Settings.Default.ARCH_AVX512_F },
                { "ARCH_AVX512_CD", Settings.Default.ARCH_AVX512_CD },
                { "ARCH_AVX512_IFMA", Settings.Default.ARCH_AVX512_IFMA },
                { "ARCH_AVX512_VBMI", Settings.Default.ARCH_AVX512_VBMI },
                { "ARCH_AVX512_VPOPCNTDQ", Settings.Default.ARCH_AVX512_VPOPCNTDQ },
                { "ARCH_AVX512_4VNNIW", Settings.Default.ARCH_AVX512_4VNNIW },
                { "ARCH_AVX512_4FMAPS", Settings.Default.ARCH_AVX512_4FMAPS },
                { "ARCH_AVX512_VBMI2", Settings.Default.ARCH_AVX512_VBMI2 },
                { "ARCH_AVX512_VNNI", Settings.Default.ARCH_AVX512_VNNI },
                { "ARCH_AVX512_BITALG", Settings.Default.ARCH_AVX512_BITALG },
                { "ARCH_AVX512_GFNI", Settings.Default.ARCH_AVX512_GFNI },
                { "ARCH_AVX512_VAES", Settings.Default.ARCH_AVX512_VAES },
                { "ARCH_AVX512_VPCLMULQDQ", Settings.Default.ARCH_AVX512_VPCLMULQDQ },
                { "ARCH_X64", Settings.Default.ARCH_X64 },
                { "ARCH_BMI1", Settings.Default.ARCH_BMI1 },
                { "ARCH_BMI2", Settings.Default.ARCH_BMI2 },
                { "ARCH_P6", Settings.Default.ARCH_P6 },
                { "ARCH_IA64", Settings.Default.ARCH_IA64 },
                { "ARCH_FMA", Settings.Default.ARCH_FMA },
                { "ARCH_TBM", Settings.Default.ARCH_TBM },
                { "ARCH_AMD", Settings.Default.ARCH_AMD },
                { "ARCH_PENT", Settings.Default.ARCH_PENT },
                { "ARCH_3DNOW", Settings.Default.ARCH_3DNOW },
                { "ARCH_CYRIX", Settings.Default.ARCH_CYRIX },
                { "ARCH_CYRIXM", Settings.Default.ARCH_CYRIXM },
                { "ARCH_VMX", Settings.Default.ARCH_VMX },
                { "ARCH_RTM", Settings.Default.ARCH_RTM },
                { "ARCH_MPX", Settings.Default.ARCH_MPX },
                { "ARCH_SHA", Settings.Default.ARCH_SHA },
                { "ARCH_ADX", Settings.Default.ARCH_ADX },
                { "ARCH_F16C", Settings.Default.ARCH_F16C },
                { "ARCH_FSGSBASE", Settings.Default.ARCH_FSGSBASE },
                { "ARCH_HLE", Settings.Default.ARCH_HLE },
                { "ARCH_INVPCID", Settings.Default.ARCH_INVPCID },
                { "ARCH_PCLMULQDQ", Settings.Default.ARCH_PCLMULQDQ },
                { "ARCH_LZCNT", Settings.Default.ARCH_LZCNT },
                { "ARCH_PREFETCHWT1", Settings.Default.ARCH_PREFETCHWT1 },
                { "ARCH_RDPID", Settings.Default.ARCH_RDPID },
                { "ARCH_RDRAND", Settings.Default.ARCH_RDRAND },
                { "ARCH_RDSEED", Settings.Default.ARCH_RDSEED },
                { "ARCH_XSAVEOPT", Settings.Default.ARCH_XSAVEOPT },
                { "ARCH_UNDOC", Settings.Default.ARCH_UNDOC },
                { "ARCH_AES", Settings.Default.ARCH_AES },
                { "ARCH_SMX", Settings.Default.ARCH_SMX },
                { "ARCH_SGX1", Settings.Default.ARCH_SGX1 },
                { "ARCH_SGX2", Settings.Default.ARCH_SGX2 },
                { "ARCH_CLDEMOTE", Settings.Default.ARCH_CLDEMOTE },
                { "ARCH_MOVDIR64B", Settings.Default.ARCH_MOVDIR64B },
                { "ARCH_MOVDIRI", Settings.Default.ARCH_MOVDIRI },
                { "ARCH_PCONFIG", Settings.Default.ARCH_PCONFIG },
                { "ARCH_WAITPKG", Settings.Default.ARCH_WAITPKG },
                { "ARCH_PRFCHW", Settings.Default.ARCH_PRFCHW },
                { "ARCH_AVX512_BF16", Settings.Default.ARCH_AVX512_BF16 },
                { "ARCH_AVX512_VP2INTERSECT", Settings.Default.ARCH_AVX512_VP2INTERSECT },
                { "ARCH_ENQCMD", Settings.Default.ARCH_ENQCMD },
                { "Global_MaxFileLines", Settings.Default.Global_MaxFileLines },
            };
        }
    }

    /// <summary>
    /// Custom initialization options (none for now)
    /// </summary>
    public IEnumerable<string>? FilesToWatch => null;

    /// <summary>
    /// Show notification if initialization fails
    /// </summary>
    public bool ShowNotificationOnInitializeFailed => true;

    /// <summary>
    /// No custom message target required
    /// </summary>
    public object? CustomMessageTarget => null;

    /// <summary>
    /// Optional middleware for logging and monitoring LSP communication
    /// Disabled because ILanguageClientMiddleLayer is obsolete
    /// </summary>
    public object? MiddleLayer => null;

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
    /// Restart the LSP server - called when settings change
    /// </summary>
    public async Task RestartServerAsync()
    {
        System.Diagnostics.Debug.WriteLine("AsmDude3: RestartServerAsync called - restarting LSP server");
        try
        {
            // Stop the current server
            if (StopAsync != null)
            {
                await StopAsync.InvokeAsync(this, EventArgs.Empty);
            }

            // Wait a moment for cleanup
            await Task.Delay(500);

            // Restart the server
            await OnLoadedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AsmDude3: Error restarting LSP server: {ex.Message}");
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
