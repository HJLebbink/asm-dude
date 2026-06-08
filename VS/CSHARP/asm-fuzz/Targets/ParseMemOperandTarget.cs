using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.Parse_Mem_Operand.
/// </summary>
public static class ParseMemOperandTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        AsmTools.AsmSourceTools.Parse_Mem_Operand(input, false);
    }
}
