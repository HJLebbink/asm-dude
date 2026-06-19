// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Collections.Generic;
using System.Text;

/// <summary>Which kind of CodeLens a <see cref="CodeLensTagSpec"/> describes (mapped to the SDK's
/// <c>CodeElementKind</c> by the tagger). Kept VS-free so the build logic is unit-testable.</summary>
internal enum AsmCodeLensTagKind
{
    Label,
    SimState,
}

/// <summary>One CodeLens tag, as plain data: where it sits (document offset + length), what it renders
/// (<see cref="Description"/>, the payload the <c>InvokableCodeLens</c> unpacks) and its identity. No VS types.</summary>
internal readonly record struct CodeLensTagSpec(
    AsmCodeLensTagKind Kind,
    int LineNumber,
    int TagStart,
    int TagLength,
    string UniqueIdentifier,
    string Description);

/// <summary>One source line, reduced to what tag-building needs: its number, its start offset in the
/// document, and its text (without the line break).</summary>
internal readonly record struct CodeLensLineInfo(int LineNumber, int StartOffset, string Text);

/// <summary>
/// Pure (VS-free) core of <c>AsmCodeLensTagger.BuildTagsAsync</c>: maps the server-supplied sim-state and
/// label data onto per-line CodeLens tags plus a content <b>signature</b>. The signature folds in every tag's
/// kind+offset+length+payload — including the per-line sim VALUE — so it changes whenever a line's displayed
/// value changes. The tagger uses signature equality to dedup republishes; therefore this signature changing
/// on a value change is exactly what forces a stale downstream lens to be re-published. Extracted so that
/// invariant is unit-testable without the VS.Extensibility SDK.
/// </summary>
internal static class AsmCodeLensTagBuilder
{
    /// <summary>Builds the whole-document tag specs (in document order) and the content signature.</summary>
    public static (List<CodeLensTagSpec> specs, string signature) Build(
        IReadOnlyList<CodeLensLineInfo> lines,
        IReadOnlyDictionary<int, string> simStates,
        IReadOnlyList<AsmLabelRef> labels)
    {
        var labelsByLine = new Dictionary<int, AsmLabelRef>();
        foreach (AsmLabelRef lbl in labels)
        {
            labelsByLine[lbl.DefinitionLine] = lbl;
        }

        var specs = new List<CodeLensTagSpec>();
        var sig = new StringBuilder();

        foreach (CodeLensLineInfo line in lines)
        {
            int ln = line.LineNumber;
            int lineLen = line.Text.Length;
            if (lineLen == 0)
            {
                continue; // only non-empty lines carry a lens
            }

            // ── Label reference-count tag (positioned on the label token reported by the server) ──
            if (labelsByLine.TryGetValue(ln, out AsmLabelRef? lbl))
            {
                int col = System.Math.Max(0, System.Math.Min(lbl.DefinitionColumn, System.Math.Max(0, lineLen - 1)));
                int len = System.Math.Max(1, System.Math.Min(lbl.DefinitionLength, lineLen - col));
                int tagStart = line.StartOffset + col;

                specs.Add(new CodeLensTagSpec(
                    AsmCodeLensTagKind.Label, ln, tagStart, len,
                    UniqueIdentifier: lbl.Label,
                    Description: $"refcount:{lbl.ReferenceCount}|Label: {lbl.Label}"));
                sig.Append("L|").Append(tagStart).Append(':').Append(len).Append(':')
                   .Append(lbl.ReferenceCount).Append(':').Append(lbl.Label).Append(';');
            }

            // ── Sim-state tag (one per instruction line that has a known state) ──
            if (simStates.TryGetValue(ln, out string? simLabel) && !string.IsNullOrEmpty(simLabel))
            {
                // 2 chars right of the first non-whitespace char, so VS renders the lens above the instruction.
                int instrCol = line.Text.Length - line.Text.TrimStart().Length;
                int offsetCol = instrCol + 2;
                int tagStart = line.StartOffset + System.Math.Min(offsetCol, System.Math.Max(0, lineLen - 1));
                int tagLen = System.Math.Max(1, lineLen - System.Math.Min(offsetCol, lineLen - 1));

                specs.Add(new CodeLensTagSpec(
                    AsmCodeLensTagKind.SimState, ln, tagStart, tagLen,
                    UniqueIdentifier: $"simstate:{ln}",
                    Description: $"simstate:|{simLabel}"));
                sig.Append("S|").Append(tagStart).Append(':').Append(tagLen).Append(':').Append(simLabel).Append(';');
            }
        }

        return (specs, sig.ToString());
    }

    /// <summary>Distinct lines that carry at least one tag (for logging parity with the old loop).</summary>
    public static int CountTaggedLines(IReadOnlyList<CodeLensTagSpec> specs)
    {
        var lines = new HashSet<int>();
        foreach (CodeLensTagSpec s in specs)
        {
            lines.Add(s.LineNumber);
        }
        return lines.Count;
    }
}
