using AsmDude2LS;

using AsmTools;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LabelGraph construction and diagnostics.
/// Directly exercises LabelGraph constructor (Add_Linenumber for each line)
/// and UpdateDiagnostics (array slicing with potentially negative positions).
/// </summary>
public static class LabelGraphTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        string[] lines = text.Split('\n');

        var options = new AsmLanguageServerOptions
        {
            IntelliSense_Label_Analysis_On = true,
            IntelliSense_Show_Undefined_Labels = true,
            IntelliSense_Show_Clashing_Labels = true,
            IntelliSense_Decorate_Undefined_Labels = true,
            IntelliSense_Decorate_Clashing_Labels = true,
            Global_MaxFileLines = 10000,
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",
        };

        var graph = new LabelGraph(lines, "fuzz.asm", true, options);
        graph.UpdateDiagnostics();

        // CONS: every tracked label definition points at a real line with a well-formed span.
        Invariants.CheckLabelGraphSpans(graph, lines.Length);

        // BND: the Global_MaxFileLines cap must be honored. With a cap at-or-below the line count, label
        // analysis MUST switch itself off (Enabled == false); a cap above it must leave analysis on (it
        // mirrors the requested IntelliSense_Label_Analysis_On). Distinct from the graph above, which uses
        // a large cap that never trips.
        var cappedOptions = new AsmLanguageServerOptions
        {
            IntelliSense_Label_Analysis_On = true,
            Global_MaxFileLines = lines.Length, // lines.Length >= cap  ->  must disable
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",
        };
        var cappedGraph = new LabelGraph(lines, "fuzz.asm", true, cappedOptions);
        if (cappedGraph.Enabled)
        {
            throw new InvariantViolation($"LabelGraph ignored the MaxFileLines cap: {lines.Length} lines with cap {cappedOptions.MaxFileLines} but analysis stayed enabled");
        }

        var roomyOptions = new AsmLanguageServerOptions
        {
            IntelliSense_Label_Analysis_On = true,
            Global_MaxFileLines = lines.Length + 1, // strictly above  ->  cap must NOT trip
            CodeFolding_BeginTag = "#region",
            CodeFolding_EndTag = "#endregion",
        };
        var roomyGraph = new LabelGraph(lines, "fuzz.asm", true, roomyOptions);
        if (!roomyGraph.Enabled)
        {
            throw new InvariantViolation($"LabelGraph wrongly tripped the MaxFileLines cap: {lines.Length} lines with cap {roomyOptions.MaxFileLines} but analysis was disabled");
        }
    }
}
