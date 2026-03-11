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
        if (data.Length > 4096)
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
    }
}
