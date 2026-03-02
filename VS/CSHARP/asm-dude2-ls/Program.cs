using AsmDude2LS;

try
{
    // Check for --stdio flag (used for testing and CLI usage)
    Worker.UseStdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<Worker>();

    // In stdio mode, disable console logging completely to avoid interference with LSP protocol
    // Stdout must be reserved exclusively for LSP JSON-RPC messages
    if (Worker.UseStdio)
    {
        builder.Logging.ClearProviders();
    }

    var host = builder.Build();
    host.Run();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}
