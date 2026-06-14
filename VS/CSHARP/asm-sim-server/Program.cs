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
    // The LSP parent owns the pipes; we speak JSON-RPC framed messages over our stdio. The shared helper
    // grabs the raw streams AND redirects Console.Out to stderr — important here because asm-sim-lib still
    // contains debug Console.Write calls that would otherwise corrupt the protocol on stdout.
    (Stream receiving, Stream sending) = StdioRpcChannel.OpenAndRedirectConsole();

    var server = new AsmSimRpcServer();
    var rpc = new JsonRpc(new HeaderDelimitedMessageHandler(sending, receiving));
    server.Attach(rpc);
    rpc.AddLocalRpcTarget(server);
    rpc.StartListening();

    // VSTHRD003: this is a plain stdio console server with no JoinableTaskContext / UI thread, so awaiting
    // StreamJsonRpc's Completion (the documented "run until the connection closes" pattern) cannot deadlock.
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
    await rpc.Completion.ConfigureAwait(false);
#pragma warning restore VSTHRD003
    AsmLog.Info("ASMSIM", "JSON-RPC connection closed; exiting");
}
catch (Exception ex)
{
    // AsmLog's console sink already writes to stderr, so no separate Console.Error dump is needed.
    AsmLog.Error("ASMSIM", "AsmSim.LS server fatal", ex);
    Environment.ExitCode = 1;
}
