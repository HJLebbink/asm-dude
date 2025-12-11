using AsmDude2LS;

try
{
    // Check for --stdio flag (used for testing and CLI usage)
    Worker.UseStdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<Worker>();

    // In stdio mode, minimize logging to stderr to avoid interference
    if (Worker.UseStdio)
    {
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
    }

    var host = builder.Build();
    host.Run();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}
