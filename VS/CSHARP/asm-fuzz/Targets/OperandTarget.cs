using AsmTools;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for the Operand constructor — composite parser (register + constant + memory).
/// </summary>
public static class OperandTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        _ = new Operand(new CapitalToken(input));
    }
}
