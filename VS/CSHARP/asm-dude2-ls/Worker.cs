using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace AsmDude2LS;

public partial class Worker : BackgroundService
{
    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetConsoleWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private readonly ILogger<Worker> _logger;
    private readonly LanguageServer _languageServer;
    private bool _shutdownRequested;

    public Worker(ILogger<Worker> logger)
    {
#if _DEBUG
        // in a debug run we want to see the console with the LSP
        ShowWindow(GetConsoleWindow(), SW_SHOW);
#else
        // in a release run we do not want to see the console with the LSP
        ShowWindow(GetConsoleWindow(), SW_HIDE);
#endif
        logger.LogInformation("Worker created at: {time}", DateTimeOffset.Now);

        this._logger = logger;
        const string stdInPipeName = @"input";
        const string stdOutPipeName = @"output";

        PipeAccessRule pipeAccessRule = new("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
        PipeSecurity pipeSecurity = new();
        pipeSecurity.AddAccessRule(pipeAccessRule);

        NamedPipeClientStream readerPipe = new(stdInPipeName);
        NamedPipeClientStream writerPipe = new(stdOutPipeName);

        readerPipe.Connect();
        writerPipe.Connect();

        this._shutdownRequested = false;
        this._languageServer = new LanguageServer(writerPipe, readerPipe);
        this._languageServer.Disconnected += this.OnDisconnected;
        this._languageServer.ShowWindow += this.OnShowWindow;
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("OnDisconnected at: {time}", DateTimeOffset.Now);
        this._shutdownRequested = true;
    }

    public void OnShowWindow(object? sender, EventArgs e)
    {
        _logger.LogInformation("OnShowWindow at: {time}", DateTimeOffset.Now);
        ShowWindow(GetConsoleWindow(), SW_SHOW);
    }

    protected override async Task ExecuteAsync(CancellationToken cancelToken)
    {
        try
        {
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                if (this._shutdownRequested)
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(1000, cancelToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AsmDude2 LSP canceled");
        }
        Environment.Exit(0);
    }
}
