using System.Text;
using AsmTools;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for AsmSourceTools.ParseMnemonic — mnemonic enum lookup.
/// </summary>
public static class ParseMnemonicTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string input = Encoding.UTF8.GetString(data);
        AsmTools.AsmSourceTools.ParseMnemonic(input, false);
    }
}
