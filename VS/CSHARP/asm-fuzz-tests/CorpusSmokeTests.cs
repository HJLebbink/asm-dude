using AsmFuzz;

using Xunit;

namespace AsmFuzz.Tests;

/// <summary>
/// Anti-rot smoke test. It is NOT a fuzzing campaign — it just feeds every
/// seed-corpus file through its target's entry point once and asserts the harness doesn't throw.
/// This is cheap (no instrumentation) and catches the kind of rot that crept into this project:
/// renamed/removed targets, a corpus directory that no longer maps to a target, or a target whose
/// entry point starts throwing on its own seeds. Because the LS targets no longer swallow
/// exceptions (P1), a corpus file that triggers a real defect will (correctly) turn this red.
/// </summary>
public sealed class CorpusSmokeTests
{
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "corpus");

    /// <summary>(targetName, corpusFilePath) for every seed file under <see cref="CorpusRoot"/>.</summary>
    public static TheoryData<string, string> CorpusFiles()
    {
        var data = new TheoryData<string, string>();
        if (!Directory.Exists(CorpusRoot))
        {
            return data;
        }

        foreach (string dir in Directory.EnumerateDirectories(CorpusRoot))
        {
            string targetName = Path.GetFileName(dir);
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                data.Add(targetName, file);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void Corpus_seed_runs_without_harness_throwing(string targetName, string filePath)
    {
        Assert.True(
            FuzzTargets.All.ContainsKey(targetName),
            $"Corpus directory '{targetName}' has no matching fuzz target. Either the target was " +
            $"renamed/removed (update FuzzTargets.All) or the corpus directory is stale.");

        byte[] bytes = File.ReadAllBytes(filePath);

        // Should not throw. If it does, the exception surfaces here — that is either a real defect
        // the seed encodes, or genuine harness rot. Both are worth a red test.
        FuzzTargets.All[targetName](bytes);
    }

    [Fact]
    public void Corpus_root_is_present_and_populated()
    {
        Assert.True(Directory.Exists(CorpusRoot), $"Corpus root not found at '{CorpusRoot}'. The csproj glob that copies ..\\asm-fuzz\\corpus is likely broken.");
        Assert.NotEmpty(Directory.EnumerateFiles(CorpusRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Every_corpus_directory_maps_to_a_registered_target()
    {
        if (!Directory.Exists(CorpusRoot))
        {
            return; // covered by Corpus_root_is_present_and_populated
        }

        string[] orphans = [.. Directory.EnumerateDirectories(CorpusRoot)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !FuzzTargets.All.ContainsKey(name))
            .Select(name => name!)];

        Assert.True(orphans.Length == 0, $"Corpus directories with no matching target: {string.Join(", ", orphans)}");
    }
}
