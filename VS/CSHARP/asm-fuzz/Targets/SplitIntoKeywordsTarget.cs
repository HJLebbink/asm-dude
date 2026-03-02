using System.Text;
using AsmTools;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.SplitIntoKeywordsType — the keyword tokenizer.
/// </summary>
public static class SplitIntoKeywordsTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        foreach (var _ in AsmTools.AsmSourceTools.SplitIntoKeywordsType(input))
        {
            // Force enumeration of the lazy iterator
        }
    }
}
