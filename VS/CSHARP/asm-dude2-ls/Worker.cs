using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace AsmDude2LS;

public class Worker : BackgroundService
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;


    private readonly ILogger<Worker> _logger;
    private readonly LanguageServer _languageServer;
    private bool _shutdownRequested;

    public Worker(ILogger<Worker> logger)
    {
        ShowWindow(GetConsoleWindow(), SW_HIDE);
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

        this._languageServer = new LanguageServer(writerPipe, readerPipe);
        this._languageServer.Disconnected += this.OnDisconnected;
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("OnDisconnected at: {time}", DateTimeOffset.Now);
        this._shutdownRequested = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();
                if (_shutdownRequested)
                {
                    throw new OperationCanceledException();
                }
                //if (_logger.IsEnabled(LogLevel.Information))
                //{
                //    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                //}
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TestService canceled");
        }
        Environment.Exit(0);
    }
}
