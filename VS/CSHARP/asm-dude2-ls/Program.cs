using AsmDude2LS;

try
{
    // Log build timestamp for debugging stale binaries
    try
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var buildTime = System.IO.File.GetLastWriteTimeUtc(asm.Location);
        Console.Error.WriteLine($"[AsmDude2.LSP STARTED] BuildTime={buildTime:yyyy-MM-dd HH:mm:ss.fff} UTC, Location={asm.Location}");
    }
    catch { }

    // Check for --stdio flag (used for testing and CLI usage)
    Worker.UseStdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase);

    // Filter out --stdio before passing to host builder (it's not a host argument)
    var hostArgs = args.Where(a => !a.Equals("--stdio", StringComparison.OrdinalIgnoreCase)).ToArray();
    var builder = Host.CreateApplicationBuilder(hostArgs);
    builder.Services.AddHostedService<Worker>();

    // In stdio mode, disable console logging completely to avoid interference with LSP protocol
    // Stdout must be reserved exclusively for LSP JSON-RPC messages
    if (Worker.UseStdio)
    {
        builder.Logging.ClearProviders();
        // Suppress "Application started. Press Ctrl+C to shut down." messages
        // from ConsoleLifetime — even though logging is cleared, be explicit
        builder.Services.Configure<Microsoft.Extensions.Hosting.ConsoleLifetimeOptions>(opts =>
            opts.SuppressStatusMessages = true);
    }

    var host = builder.Build();
    host.Run();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}
