using AsmFuzz;

using Xunit;

namespace AsmFuzz.Tests;

/// <summary>
/// Guards the two target lists against drift. <see cref="FuzzTargetNames.All"/> is a strings-only copy
/// used by the <c>fuzz-all</c> orchestrator (it must not load the delegate registry — see that type's
/// remarks); this test fails if it ever diverges from the real <see cref="FuzzTargets.All"/> registry.
/// </summary>
public sealed class TargetRegistryTests
{
    [Fact]
    public void Names_list_matches_the_delegate_registry()
    {
        var registry = FuzzTargets.All.Keys.ToHashSet();
        var names = FuzzTargetNames.All.ToHashSet();

        Assert.Equal(FuzzTargetNames.All.Length, names.Count); // no duplicate names
        Assert.True(registry.SetEquals(names),
            $"FuzzTargetNames.All is out of sync with FuzzTargets.All. " +
            $"Only in registry: [{string.Join(", ", registry.Except(names))}]; " +
            $"only in names: [{string.Join(", ", names.Except(registry))}].");
    }
}
