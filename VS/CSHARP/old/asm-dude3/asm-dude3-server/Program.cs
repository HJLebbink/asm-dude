using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace AsmDude3.Server;

/// <summary>
/// Entry point for the AsmDude3 Language Server
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Setup logging (no output providers - would interfere with JSON-RPC)
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            var logger = loggerFactory.CreateLogger<Program>();

            // Create the language server
            var server = new LanguageServer(logger);

            // Setup JSON-RPC communication over stdin/stdout
            var formatter = new StreamJsonRpc.JsonMessageFormatter
            {
                MultiplexingStream = null
            };
            var handler = new StreamJsonRpc.HeaderDelimitedMessageHandler(Console.OpenStandardOutput(), Console.OpenStandardInput(), formatter);
            using var jsonRpc = new JsonRpc(handler)
            {
                AllowModificationWhileListening = true
            };

            // Log warnings and errors only
            jsonRpc.TraceSource = new System.Diagnostics.TraceSource("JsonRpc")
            {
                Switch = { Level = System.Diagnostics.SourceLevels.Warning }
            };
            jsonRpc.TraceSource.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error));

            // Add the target to register all LSP methods
            jsonRpc.AddLocalRpcTarget(server);

            // Start listening
            jsonRpc.StartListening();
            await Console.Error.WriteLineAsync("=== AsmDude3 Language Server Ready ===");

            // Wait for shutdown
            await server.WaitForExitAsync();

            logger.LogInformation("Language Server shutting down");
            return 0;
        }
        catch (Exception ex)
        {
            // Write to stderr since stdout is for JSON-RPC
            await Console.Error.WriteLineAsync($"FATAL ERROR: {ex}");
            return 1;
        }
    }
}
