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

        var byLine = new Dictionary<int, CodeLensLineInfo>();
        foreach (CodeLensLineInfo l in lines)
        {
            byLine[l.LineNumber] = l;
        }

        // A CodeLens can only attach to a NON-EMPTY line: VS shifts the whole layout if you anchor one to a
        // blank line (its range spans the line break and binds to the next line). So relocate every sim label
        // whose display position is a blank line DOWN to the next non-empty line, COMBINING labels that end up
        // together. The server places reads at line N and writes at N+1 (so the after-state shows below the
        // instruction); when N+1 is the blank line you just typed, the write floats to the next code line —
        // exactly where it sat before the blank existed — instead of vanishing. A label with no non-empty line
        // below it (trailing) is dropped (nothing can render below the last line).
        var simByLine = new SortedDictionary<int, string>();
        var orderedPositions = new List<int>(simStates.Keys);
        orderedPositions.Sort();
        foreach (int pos in orderedPositions)
        {
            if (!simStates.TryGetValue(pos, out string? label) || string.IsNullOrEmpty(label)) continue;
            int target = NextNonEmptyLine(byLine, pos);
            if (target < 0) continue;
            simByLine[target] = simByLine.TryGetValue(target, out string? existing)
                ? CombineLabels(existing, label)
                : label;
        }

        var specs = new List<CodeLensTagSpec>();
        var sig = new StringBuilder();

        foreach (CodeLensLineInfo line in lines)
        {
            int ln = line.LineNumber;
            int lineLen = line.Text.Length;
            // A lens can only attach to a line with real content. A WHITESPACE-ONLY line counts as blank: VS
            // auto-indents a newline (so it isn't Length 0), and rendering a lens there is exactly the
            // disappear/shift the user hit — those labels were relocated to the next real line above.
            if (string.IsNullOrWhiteSpace(line.Text)) continue;

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

            // ── Sim-state tag (relocated onto this non-empty line by the pass above) ──
            if (simByLine.TryGetValue(ln, out string? simLabel) && !string.IsNullOrEmpty(simLabel))
            {
                // 2 chars right of the first non-whitespace char, so VS renders the lens above the line.
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

    /// <summary>The first line at or after <paramref name="from"/> with real (non-whitespace) content, or -1
    /// if none. A whitespace-only line counts as blank — VS auto-indents a new line, so it isn't Length 0, yet
    /// a lens must not land on it (that is the disappear/shift bug).</summary>
    private static int NextNonEmptyLine(Dictionary<int, CodeLensLineInfo> byLine, int from)
    {
        for (int i = from; byLine.TryGetValue(i, out CodeLensLineInfo li); i++)
        {
            if (!string.IsNullOrWhiteSpace(li.Text)) return i;
        }
        return -1;
    }

    /// <summary>Combine two compact sim-label strings ("prefix:name=value" items, comma/space separated),
    /// deduping by register/flag name: a name seen as both read (r:) and write (w:) collapses to rw:, and a
    /// concrete value wins over an unknown ("?") one. Used when a relocated write lands on a line that already
    /// carries a label (e.g. an instruction's write floating down to the next instruction that reads it).</summary>
    internal static string CombineLabels(string a, string b)
    {
        var order = new List<string>();
        var byName = new Dictionary<string, (string prefix, string value)>(System.StringComparer.OrdinalIgnoreCase);

        void Add(string token)
        {
            int colon = token.IndexOf(':');
            int eq = token.IndexOf('=');
            if (colon < 0 || eq <= colon + 1) return;
            string prefix = token.Substring(0, colon);
            string name = token.Substring(colon + 1, eq - colon - 1);
            string value = token.Substring(eq + 1);
            if (name.Length == 0) return;

            if (!byName.TryGetValue(name, out (string prefix, string value) cur))
            {
                order.Add(name);
                byName[name] = (prefix, value);
            }
            else
            {
                string mergedPrefix = string.Equals(cur.prefix, prefix, System.StringComparison.Ordinal) ? cur.prefix : "rw";
                string mergedValue = (cur.value.IndexOf('?') >= 0 && value.IndexOf('?') < 0) ? value : cur.value;
                byName[name] = (mergedPrefix, mergedValue);
            }
        }

        foreach (string commaPart in (a + ", " + b).Split(','))
        {
            foreach (string tok in commaPart.Split(' '))
            {
                string t = tok.Trim();
                if (t.Length > 0) Add(t);
            }
        }

        var sb = new StringBuilder();
        foreach (string n in order)
        {
            if (sb.Length > 0) sb.Append(", ");
            (string prefix, string value) = byName[n];
            sb.Append(prefix).Append(':').Append(n).Append('=').Append(value);
        }
        return sb.ToString();
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
