using System.Text;
using AsmTools;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.Parse_Mem_Operand.
/// Known bug: line 827 uses token.Length instead of s.Length in Substring call.
/// </summary>
public static class ParseMemOperandTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        AsmTools.AsmSourceTools.Parse_Mem_Operand(input, false);
    }
}
