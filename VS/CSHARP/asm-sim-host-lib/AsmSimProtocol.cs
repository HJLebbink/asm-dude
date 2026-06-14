// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmSim.Host;

using System.Collections.Generic;

/// <summary>
/// The wire contract between the LSP server (client) and the out-of-process AsmSim server
/// (<c>AsmSim.LS.exe</c>) — JSON-RPC 2.0 over the sim server's stdio. These are plain records with
/// primitive members so System.Text.Json (StreamJsonRpc's serializer) round-trips them without fuss.
/// Defined here in <c>asm-sim-host-lib</c> because BOTH processes reference this library, so the contract
/// is compile-checked on both ends (the same trick asm-options-lib uses for settings.json).
/// See <c>VS/CSHARP/ASMSIM_SERVER_PLAN.md</c>.
/// </summary>
public static class AsmSimProtocol
{
    // Client → server.
    public const string Initialize = "asmsim/initialize";
    public const string SettingsChanged = "asmsim/settingsChanged";
    public const string DocumentChanged = "asmsim/documentChanged";
    public const string DocumentClosed = "asmsim/documentClosed";
    public const string Shutdown = "asmsim/shutdown";

    // Server → client (notifications).
    public const string LineResults = "asmsim/lineResults";
    public const string SimStatus = "asmsim/simStatus";
}

/// <summary>The sim-relevant settings subset (a projection of AsmSettingsData), pushed to the sim server.</summary>
public sealed record AsmSimSettings(
    bool AsmSimOn,
    string Engine,         // "linear" | "component" | "shadow"
    string LoopHandling,   // "accept" | "modsethavoc" | "peelonce" | "fullunroll" | "fixpoint"
    int Parallelism,
    int Z3TimeoutMs,
    bool Incremental = false, // reuse unaffected lines + re-solve only the dataflow cone (default off)
    bool ShowRedundant = false); // run the per-line redundant-instruction check (AsmDude1 parity; default off)

/// <summary><c>asmsim/initialize</c> params / result.</summary>
public sealed record AsmSimInitParams(int ClientProcessId, AsmSimSettings Settings);

public sealed record AsmSimInitResult(string Engine, int Parallelism, string ServerVersion);

/// <summary><c>asmsim/documentChanged</c> — full text (the sim re-parses + re-simulates).</summary>
public sealed record AsmSimDocumentParams(string Uri, long Version, string Text, int Assembler);

/// <summary>One document line's simulation output (mirror of a <c>DocCache</c> line / <c>SimLineResult</c>).
/// <paramref name="Diagnostics"/> are the flattened <c>"Kind:Message"</c> strings (same as SimLineResult).</summary>
public sealed record AsmSimLineDto(
    int Line,
    string? Before,
    string? After,
    string? ReadLabel,
    string? WriteLabel,
    IReadOnlyList<string>? Diagnostics);

/// <summary><c>asmsim/lineResults</c> — a batch of resolved lines (streamed on the sim's throttle).</summary>
public sealed record AsmSimLineResultsParams(string Uri, long Version, IReadOnlyList<AsmSimLineDto> Lines);

/// <summary><c>asmsim/simStatus</c> — run lifecycle for the client to drive diagnostics/CodeLens refresh.</summary>
public sealed record AsmSimStatusParams(
    string Uri,
    long Version,
    string State,          // "started" | "progress" | "completed" | "cancelled" | "failed"
    int LinesWritten,
    int DiagCount,
    long TotalMs);
