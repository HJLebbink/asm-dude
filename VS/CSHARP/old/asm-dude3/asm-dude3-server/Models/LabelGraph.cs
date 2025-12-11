namespace AsmDude3.Server.Models;

/// <summary>
/// Stub for LabelGraph - full implementation from AsmDude2 not yet ported
/// Tracks labels in assembly code and their relationships
/// </summary>
public class LabelGraph
{
    /// <summary>
    /// Dictionary of labels and their descriptions
    /// </summary>
    public SortedDictionary<string, string> Label_Descriptions { get; } = new();
}
