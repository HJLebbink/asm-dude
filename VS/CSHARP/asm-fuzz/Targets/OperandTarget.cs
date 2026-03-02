using System.Text;
using AsmTools;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for the Operand constructor — composite parser (register + constant + memory).
/// </summary>
public static class OperandTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        _ = new Operand(input, false);
    }
}
