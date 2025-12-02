using Xunit;

namespace AsmDude3.Server.Tests;

/// <summary>
/// Custom Fact attribute that skips tests in DEBUG builds (but runs in RELEASE builds).
/// Use this for tests that are too slow during development but should run in CI/release.
/// </summary>
public sealed class DebugSkippableFactAttribute : FactAttribute
{
    public DebugSkippableFactAttribute()
    {
#if DEBUG
        Skip = "Skipped in DEBUG build (slow test - runs in RELEASE only)";
#endif
    }
}
