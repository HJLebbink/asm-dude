using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// NEG: the primitive token classifiers (register / mnemonic) must be internally consistent — a boolean
/// predicate and its value-parser must agree, and bad input must never be classified as a valid register
/// or mnemonic. The other parser targets only assert "didn't throw"; this one asserts the classification
/// is *correct*, generalising the negative-outcome conformance the <c>settings</c> target has for JSON to
/// the parser primitives. See <see cref="Invariants.CheckClassifyConsistency"/>.
/// </summary>
public static class ClassifyConsistencyTarget
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ',', '[', ']', '(', ')', '{', '}', ':', '+', '-', '*'];

    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);

        // The whole input as one token (exercises odd whole-string keys).
        Invariants.CheckClassifyConsistency(input);

        // Plus each whitespace/separator-delimited token, so register/mnemonic-shaped fragments get hit.
        foreach (string token in input.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            Invariants.CheckClassifyConsistency(token);
        }
    }
}
