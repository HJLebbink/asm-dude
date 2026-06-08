using AsmTools;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for ExpressionEvaluator.Parse_Constant — numeric constant parsing.
/// Uses Parse_Constant (not Evaluate_Constant) to avoid slow CSharpScript path.
/// </summary>
public static class EvaluateConstantTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        ExpressionEvaluator.Parse_Constant(input, false);
    }
}
