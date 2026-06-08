using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.SplitIntoKeywordsType — the keyword tokenizer.
/// </summary>
public static class SplitIntoKeywordsTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);

        // CONS invariant: every keyword span must lie within the line (0 <= begin < end <= length).
        // (CheckKeywordSpans also forces enumeration of the lazy iterator.)
        Invariants.CheckKeywordSpans(input, AsmTools.AsmSourceTools.SplitIntoKeywordsType(input));
    }
}
