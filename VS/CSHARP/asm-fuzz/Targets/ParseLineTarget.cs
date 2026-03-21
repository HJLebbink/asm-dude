using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.ParseLine — the main assembly line parser.
/// </summary>
public static class ParseLineTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        AsmTools.AsmSourceTools.ParseLine(input, 0, 0, AsmTools.AssemblerEnum.MASM);
    }
}
