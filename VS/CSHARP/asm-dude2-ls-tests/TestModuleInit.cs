// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS.Tests;

using System;
using System.Runtime.CompilerServices;

internal static class TestModuleInit
{
    /// <summary>
    /// Pin the LSP server's simulation to IN-PROCESS for the unit tests (deterministic, no child-process
    /// spawn). Out-of-process is the production default (<c>LanguageServer.SimOutOfProc</c>), but a
    /// <see cref="LanguageServer"/>-based unit test must not launch <c>AsmSim.Server.exe</c> — the tests that
    /// DO exercise the real server (<c>AsmSimClient*Tests</c>) drive it explicitly via
    /// <c>AsmSimClient.TryCreate</c>, which ignores this flag. Runs at assembly load, before
    /// <c>SimOutOfProc</c> reads the env var.
    /// </summary>
    [ModuleInitializer]
    internal static void Init()
        => Environment.SetEnvironmentVariable("ASMDUDE_SIM_OUTOFPROC", "0");
}
