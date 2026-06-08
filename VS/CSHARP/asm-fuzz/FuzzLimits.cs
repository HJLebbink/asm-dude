namespace AsmFuzz;

/// <summary>
/// Shared limits for all fuzz targets.
/// </summary>
internal static class FuzzLimits
{
    /// <summary>
    /// Maximum input size (bytes) a target will process; larger inputs are skipped to bound
    /// memory/time. Also passed as libFuzzer's <c>-max_len</c> by the <c>fuzz-all</c> driver, so the
    /// two stay in sync by construction.
    /// </summary>
    public const int MaxInputLength = 4096;
}
