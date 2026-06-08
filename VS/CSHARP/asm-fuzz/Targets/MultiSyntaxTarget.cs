using AsmTools;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for the dialect-specific line parser. The other parsing targets only fuzz a single
/// assembler dialect; this one parses each fuzzed line under every concrete <see cref="AssemblerEnum"/>
/// dialect (MASM, NASM Intel, NASM AT&amp;T), exercising the MASM/NASM/AT&amp;T-specific branches in
/// <see cref="AsmSourceTools.ParseLine"/>. The parser must never throw on any bytes, so nothing is
/// swallowed — any exception is a real bug.
/// </summary>
public static class MultiSyntaxTarget
{
    private static readonly AssemblerEnum[] Dialects =
    [
        AssemblerEnum.MASM,
        AssemblerEnum.NASM_INTEL,
        AssemblerEnum.NASM_ATT,
    ];

    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        string[] lines = text.Split('\n');

        // Also fuzz the assembler-name parser with the raw input.
        AsmTools.AsmSourceTools.ParseAssembler(text, false);

        for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            string line = lines[lineNumber];
            foreach (AssemblerEnum dialect in Dialects)
            {
                AsmTools.AsmSourceTools.ParseLine(line, lineNumber, 0, dialect);
            }
        }
    }
}
