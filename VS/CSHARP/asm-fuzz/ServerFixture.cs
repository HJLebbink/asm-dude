using AsmDude2LS;

using AsmTools;

using System.Diagnostics;

namespace AsmFuzz;

/// <summary>
/// Factory for the LanguageServer used by the LS fuzz targets.
/// <para>
/// A <b>fresh</b> server is built per fuzz input (the caller owns and disposes it). A shared
/// singleton accumulates cross-iteration state (caches, label graphs), which breaks libFuzzer's
/// per-input reproducibility: a crash that depends on accumulated state won't reproduce when
/// libFuzzer replays the single crashing input ("crash does not reproduce"), making any LS find
/// impossible to triage.
/// </para>
/// <para>
/// Building a fresh server would otherwise re-read the file-backed reference data each input (~200 ms);
/// that data is immutable and read-only after load, so it is loaded ONCE and injected into every
/// server (see the field comment), keeping inputs cheap without sacrificing state isolation.
/// </para>
/// </summary>
static class ServerFixture
{
    // The immutable, file-loaded reference data — instruction signatures (MnemonicStore, the
    // signature-*.txt files), performance tables (PerformanceStore: one TSV per ENABLED arch, here
    // Haswell.tsv + Skylake.tsv — NOT all 27 bundled arches), and instruction metadata (AsmDude2Tools,
    // AsmDudeData.xml) — is the dominant per-input cost (~200 ms) and is read-only after load; in
    // production one server already shares it across every document. So we load it ONCE here and inject
    // it into each fresh server. The per-input mutable state (documents, label graphs, caches) is still
    // fresh per server, preserving the crash reproducibility that motivated the fresh-server-per-input
    // design.
    private static readonly TraceSource _traceSource = new("asm-fuzz");
    private static readonly Lock _refLock = new();
    private static LanguageServer.ReferenceData? _refData;

    static ServerFixture()
    {
        // Fuzzing/merge drives thousands of inputs/sec through the LS server. AsmDudeLog defaults to the
        // Debug threshold in DEBUG builds and appends EVERY message to %TEMP%\asmdude-execution.log
        // (open+write+close per call) — during a corpus -merge that grew the log to ~4 GB and throttled the
        // LS targets to a crawl. Kill the disk sink and only surface real errors to the console.
        AsmDudeLog.DisableFileLog = true;
        AsmDudeLog.Threshold = LogLevel.Error;
    }

    private static AsmLanguageServerOptions CreateOptions() => new()
    {
        ARCH_8086 = true,
        ARCH_X64 = true,
        ARCH_SSE = true,
        ARCH_AVX = true,
        CodeCompletion_On = true,
        SignatureHelp_On = true,
        CodeFolding_On = true,
        CodeFolding_BeginTag = "#region",
        CodeFolding_EndTag = "#endregion",
        AsmDoc_On = true,
        IntelliSense_Label_Analysis_On = true,
        PerformanceInfo_On = true,
        PerformanceInfo_Haswell_On = true,
        PerformanceInfo_Skylake_On = true,
        Global_MaxFileLines = 10000,
    };

    /// <summary>
    /// Builds a fresh, initialized <see cref="LanguageServer"/> configured for fuzzing, reusing the
    /// shared (once-loaded) reference data. The caller disposes it (LS targets use <c>using var</c>).
    /// </summary>
    public static LanguageServer CreateServer()
    {
        var options = CreateOptions();

        if (_refData is null)
        {
            lock (_refLock)
            {
                _refData ??= LanguageServer.LoadReferenceData(options, _traceSource);
            }
        }

        var server = new LanguageServer();
        server.Initialize(options);
        server.ApplyReferenceData(_refData.Value);
        return server;
    }
}
