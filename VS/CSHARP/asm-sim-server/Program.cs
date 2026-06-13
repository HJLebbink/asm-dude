// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.
//
// AsmSim.LS.exe — the out-of-process AsmSim simulation server. Speaks JSON-RPC (AsmSimProtocol) over
// stdin/stdout to the LSP server that launched it. Logs to stderr + a file (NEVER stdout — that carries
// JSON-RPC). See VS/CSHARP/ASMSIM_SERVER_PLAN.md.

using AsmSim.Host;

using AsmTools;

using StreamJsonRpc;

// ── Logging: file + stderr (stdout is reserved for the JSON-RPC channel) ────────────────────────────
AsmLog.AddSink(AsmLogSinks.File(Path.Combine(Path.GetTempPath(), "asmdude-simserver.log")));
AsmLog.AddSink(AsmLogSinks.Console(useStandardError: true));
if (AsmLog.TryParseLevel(Environment.GetEnvironmentVariable("ASMDUDE_LOGLEVEL"), out AsmLogLevel level))
{
    AsmLog.Threshold = level;
}

AsmLog.Banner("ASMSIM", $"AsmSim.LS server starting (pid {Environment.ProcessId}, .NET {Environment.Version})");

try
{
    // The LSP parent owns the pipes; we speak JSON-RPC framed messages over our stdio.
    Stream sending = Console.OpenStandardOutput();
    Stream receiving = Console.OpenStandardInput();

    var server = new AsmSimRpcServer();
    var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(sending, receiving));
    server.Attach(rpc);
    rpc.AddLocalRpcTarget(server);
    rpc.StartListening();

    await rpc.Completion.ConfigureAwait(false);
    AsmLog.Info("ASMSIM", "JSON-RPC connection closed; exiting");
}
catch (Exception ex)
{
    AsmLog.Error("ASMSIM", "AsmSim.LS server fatal", ex);
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
