using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.ParseLine — the main assembly line parser.
/// </summary>
public static class ParseLineTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        var (keywords, _, _, _, _) = AsmTools.AsmSourceTools.ParseLine(input, 0, 0, AsmTools.AssemblerEnum.MASM);

        // CONS invariant: every produced token span stays within the line (catches packing/offset bugs).
        Invariants.CheckKeywordIds(input.Length, keywords);

        // HOM / line-independence: classification must depend on the line text only, not on the
        // lineNumber/fileID arguments (which are merely packed into each KeywordID).
        Invariants.CheckParseLinePositionInvariant(input, AsmTools.AssemblerEnum.MASM);
    }
}
