using AsmDude2LS;

// These top-level statements are the process bootstrap: they run BEFORE the host is built and before
// AsmDudeLog wires the AsmLog sinks, so AsmLog calls here would be dropped. Diagnostics therefore go
// straight to stderr (never stdout — that is reserved for the LSP JSON-RPC channel). This is the one
// bootstrap exemption from the "route logging through AsmLog" rule; see CLAUDE.md > Logging.
#pragma warning disable RS0030 // Do not use banned APIs

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

    // Route Microsoft.Extensions.Logging through AsmLog so host/framework logs share the unified
    // sinks/format. Added AFTER the stdio ClearProviders so it survives; AsmLog's console sink uses
    // stderr, so this is safe even in --stdio mode (stdout stays reserved for JSON-RPC).
    builder.Logging.AddProvider(new AsmLogLoggerProvider());

    var host = builder.Build();
    host.Run();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}
