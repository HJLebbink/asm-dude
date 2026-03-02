using AsmDude2LS;
using AsmTools;

namespace AsmFuzz;

/// <summary>
/// Shared LanguageServer instance for LS fuzz targets.
/// Created once per process (expensive due to loading signature/performance files).
/// </summary>
static class ServerFixture
{
    private static LanguageServer? _server;
    private static readonly object _lock = new();

    public static LanguageServer GetServer()
    {
        if (_server != null)
        {
            return _server;
        }

        lock (_lock)
        {
            if (_server != null)
            {
                return _server;
            }

            var server = new LanguageServer();
            var options = new AsmLanguageServerOptions
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
            server.Initialize(options);
            server.Initialized();
            _server = server;
            return _server;
        }
    }
}
