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
