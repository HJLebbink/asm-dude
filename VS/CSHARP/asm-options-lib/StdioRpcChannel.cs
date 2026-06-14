// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmTools;

using System.IO;

/// <summary>
/// The JSON-RPC stdio transport shared by the out-of-process servers (<c>asm-dude2-ls</c> and
/// <c>asm-sim-server</c>). This is the ONE sanctioned place that touches <see cref="System.Console"/> for
/// the protocol streams: a process's stdin/stdout carry framed JSON-RPC, so they must be grabbed as raw
/// streams — there is no AsmLog alternative for that. Naming the operation here documents the intent in
/// code (instead of a scattered <c>#pragma</c>) and guarantees the protective <see cref="System.Console.Out"/>
/// redirect is never forgotten by a caller. Everything else logs via <see cref="AsmLog"/> (whose console
/// sink targets stderr). See CLAUDE.md &gt; Logging.
/// </summary>
public static class StdioRpcChannel
{
    // Console IS the transport in this type by definition; this is the deliberate, single exemption from the
    // banned-API rule that elsewhere routes all Console use to AsmLog.
#pragma warning disable RS0030 // Do not use banned APIs

    /// <summary>
    /// Captures the process's raw stdin/stdout as the duplex JSON-RPC channel, then repoints
    /// <see cref="System.Console.Out"/> at stderr so that a stray <c>Console.Write</c> anywhere in the
    /// process (e.g. legacy debug output in asm-sim-lib / asm-tools-lib) can never interleave with — and
    /// corrupt — the protocol bytes on stdout.
    /// </summary>
    /// <returns>
    /// <c>input</c> = stdin (the RPC receiving stream), <c>output</c> = stdout (the RPC sending stream).
    /// </returns>
    public static (Stream input, Stream output) OpenAndRedirectConsole()
    {
        Stream input = System.Console.OpenStandardInput();
        Stream output = System.Console.OpenStandardOutput();
        System.Console.SetOut(System.Console.Error);
        return (input, output);
    }

#pragma warning restore RS0030
}
