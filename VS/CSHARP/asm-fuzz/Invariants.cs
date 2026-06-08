using AsmDude2LS;

using AsmTools;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using LspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmFuzz;

/// <summary>
/// Thrown when production output violates an invariant the fuzzer asserts. It escapes a target's
/// <c>Run</c> like any other exception, so libFuzzer/the campaign records it as a finding — but the
/// distinct type makes "this is a correctness invariant, not a random crash" obvious during triage.
/// </summary>
internal sealed class InvariantViolation(string message) : Exception(message);

/// <summary>
/// Output invariants the fuzz targets assert in addition to "didn't throw". These need no oracle —
/// they are structural properties the production output must always satisfy — and catch a class of
/// SILENT corruption (out-of-bounds token spans, malformed semantic-token encodings) that the
/// crash-only oracle cannot. See INVARIANT-TAXONOMY.md (families CONS / MONO / DUAL).
/// </summary>
internal static class Invariants
{
    /// <summary>CONS: a token span over a line must satisfy <c>0 ≤ begin &lt; end ≤ lineLength</c>.</summary>
    public static void CheckSpan(int begin, int end, int lineLength, string what)
    {
        if (begin < 0 || begin >= end || end > lineLength)
        {
            throw new InvariantViolation($"{what}: span ({begin},{end}) out of bounds for line length {lineLength}");
        }
    }

    /// <summary>
    /// CONS + MONO: every keyword span from <see cref="AsmSourceTools.SplitIntoKeywordsType"/> is
    /// in-bounds AND the spans are emitted in order without overlapping (<c>begin[i] ≥ end[i-1]</c>),
    /// since the tokenizer scans the line left-to-right.
    /// </summary>
    public static void CheckKeywordSpans(string line, IEnumerable<(int beginPos, int endPos, AsmTokenType type)> tokens)
    {
        int prevEnd = 0;
        foreach ((int begin, int end, AsmTokenType _) in tokens)
        {
            CheckSpan(begin, end, line.Length, "SplitIntoKeywordsType");
            if (begin < prevEnd)
            {
                throw new InvariantViolation($"SplitIntoKeywordsType: span ({begin},{end}) overlaps/precedes previous (ended {prevEnd})");
            }

            prevEnd = end;
        }
    }

    /// <summary>CONS: an LSP range is well-formed and within the document (lines from the server's model).</summary>
    public static void CheckRangeInDocument(LspRange range, string[] lines, string what)
    {
        if (range is null)
        {
            return;
        }

        Position s = range.Start, e = range.End;
        if (s.Line < 0 || e.Line < 0 || s.Character < 0 || e.Character < 0
            || s.Line > e.Line || (s.Line == e.Line && s.Character > e.Character))
        {
            throw new InvariantViolation($"{what}: malformed range [{s.Line}:{s.Character}..{e.Line}:{e.Character}]");
        }

        if (e.Line >= lines.Length)
        {
            throw new InvariantViolation($"{what}: range end line {e.Line} out of range (document has {lines.Length} lines)");
        }

        if (s.Character > lines[s.Line].Length || e.Character > lines[e.Line].Length)
        {
            throw new InvariantViolation($"{what}: range char exceeds line length [{s.Line}:{s.Character}..{e.Line}:{e.Character}]");
        }
    }

    /// <summary>CONS: an LSP position is within the document (line in range, char within that line).</summary>
    public static void CheckPosition(Position pos, string[] lines, string what)
    {
        if (pos is null)
        {
            return;
        }

        if (pos.Line < 0 || pos.Line >= lines.Length)
        {
            throw new InvariantViolation($"{what}: position line {pos.Line} out of range (document has {lines.Length} lines)");
        }

        if (pos.Character < 0 || pos.Character > lines[pos.Line].Length)
        {
            throw new InvariantViolation($"{what}: position char {pos.Character} exceeds line {pos.Line} length {lines[pos.Line].Length}");
        }
    }

    /// <summary>
    /// IDEM / determinism: two runs over identical input must produce the identical semantic-token
    /// stream. A difference means the output depends on hidden/global/static state — which would make
    /// editor highlighting nondeterministic. (Non-tautological: it crosses two independent server
    /// instances.)
    /// </summary>
    public static void CheckSemanticTokensEqual(int[]? a, int[]? b)
    {
        if (a is null && b is null)
        {
            return;
        }

        if (a is null || b is null || a.Length != b.Length)
        {
            throw new InvariantViolation($"semanticTokens not deterministic: lengths {a?.Length.ToString() ?? "null"} vs {b?.Length.ToString() ?? "null"}");
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                throw new InvariantViolation($"semanticTokens not deterministic: differ at index {i} ({a[i]} vs {b[i]})");
            }
        }
    }

    /// <summary>CONS: every diagnostic points to a valid range inside the document.</summary>
    public static void CheckDiagnostics(IEnumerable<Diagnostic> diagnostics, string[] lines)
    {
        foreach (Diagnostic d in diagnostics)
        {
            CheckRangeInDocument(d.Range, lines, "diagnostic");
        }
    }

    /// <summary>CONS: every folding range is well-formed (<c>0 ≤ startLine ≤ endLine &lt; lineCount</c>).</summary>
    public static void CheckFoldingRanges(IEnumerable<FoldingRange> ranges, string[] lines)
    {
        foreach (FoldingRange r in ranges)
        {
            if (r.StartLine < 0 || r.StartLine > r.EndLine || r.EndLine >= lines.Length)
            {
                throw new InvariantViolation($"foldingRange: lines [{r.StartLine}..{r.EndLine}] out of bounds (document has {lines.Length} lines)");
            }
        }
    }

    /// <summary>
    /// CONS: every <see cref="KeywordID"/> span from <see cref="AsmSourceTools.ParseLine"/> stays within
    /// the line: <c>0 ≤ Start_Pos ≤ End_Pos ≤ lineLength</c>. (Looser than <see cref="CheckSpan"/> —
    /// allows zero-width — since ParseLine may legitimately emit empty spans; the point is that the
    /// 14-bit position packing never produces out-of-line offsets.)
    /// </summary>
    public static void CheckKeywordIds(int lineLength, IEnumerable<KeywordID> keywords)
    {
        foreach (KeywordID kw in keywords)
        {
            if (kw.Start_Pos < 0 || kw.Start_Pos > kw.End_Pos || kw.End_Pos > lineLength)
            {
                throw new InvariantViolation($"ParseLine KeywordID span ({kw.Start_Pos},{kw.End_Pos}) out of bounds for line length {lineLength} [{kw}]");
            }
        }
    }

    /// <summary>
    /// CONS + MONO: the LSP delta-encoded semantic-token array (groups of 5 uints:
    /// deltaLine, deltaStartChar, length, tokenType, tokenModifiers) must be well-formed — a multiple
    /// of 5, every token positive-length and inside its line, and tokens sorted &amp; non-overlapping in
    /// reading order. A violation silently corrupts highlighting in the editor and never throws on its
    /// own, so only an explicit invariant can catch it. <paramref name="lines"/> MUST be the server's
    /// own line model (see <c>GetDocumentLinesForTest</c>) so this doesn't false-positive on line-split
    /// differences.
    /// </summary>
    public static void CheckSemanticTokens(int[] data, string[] lines)
    {
        if (data.Length % 5 != 0)
        {
            throw new InvariantViolation($"semanticTokens: data length {data.Length} is not a multiple of 5");
        }

        int line = 0;
        int startChar = 0;
        int prevEndChar = 0; // exclusive end of the previous token on the current line
        bool first = true;

        for (int i = 0; i < data.Length; i += 5)
        {
            int deltaLine = data[i];
            int deltaStart = data[i + 1];
            int length = data[i + 2];
            int tokenType = data[i + 3];

            // The five values are LSP-encoded as non-negative; a negative is itself malformed.
            if (deltaLine < 0 || deltaStart < 0 || tokenType < 0)
            {
                throw new InvariantViolation($"semanticTokens: negative encoded value at index {i} (deltaLine={deltaLine}, deltaStart={deltaStart}, tokenType={tokenType})");
            }

            if (!first && deltaLine == 0)
            {
                startChar += deltaStart;
            }
            else
            {
                line += deltaLine;
                startChar = deltaStart;
                prevEndChar = 0;
            }

            first = false;

            if (length <= 0)
            {
                throw new InvariantViolation($"semanticTokens: non-positive length {length} at line {line}, char {startChar}");
            }

            if (tokenType >= 32) // legend has ~20 types; 32 is a loose sanity bound for garbage
            {
                throw new InvariantViolation($"semanticTokens: implausible tokenType {tokenType} at line {line}, char {startChar}");
            }

            if (line < 0 || line >= lines.Length)
            {
                throw new InvariantViolation($"semanticTokens: line {line} out of range (document has {lines.Length} lines)");
            }

            if (startChar < prevEndChar)
            {
                throw new InvariantViolation($"semanticTokens: token at line {line} char {startChar} overlaps previous (ends at {prevEndChar})");
            }

            if (startChar + length > lines[line].Length)
            {
                throw new InvariantViolation($"semanticTokens: token at line {line} [{startChar},{startChar + length}) exceeds line length {lines[line].Length}");
            }

            prevEndChar = startChar + length;
        }
    }

    /// <summary>
    /// HOM / line-independence: <see cref="AsmSourceTools.ParseLine"/> classifies a line from the line
    /// TEXT alone — the <c>lineNumber</c>/<c>fileID</c> arguments are only packed into each
    /// <see cref="KeywordID"/> and MUST NOT influence what is produced. So parsing the same line at two
    /// different positions must yield the identical (label, mnemonic, args, remark) and the identical
    /// per-token line-relative span+type. A divergence means line context bled into classification —
    /// a cross-line state-bleed bug that never throws on its own.
    /// </summary>
    public static void CheckParseLinePositionInvariant(string line, AssemblerEnum assembler)
    {
        var (kwA, labelA, mnemA, argsA, remarkA) = AsmTools.AsmSourceTools.ParseLine(line, 0, 0, assembler);
        var (kwB, labelB, mnemB, argsB, remarkB) = AsmTools.AsmSourceTools.ParseLine(line, 7, 3, assembler);

        if (!string.Equals(labelA, labelB, StringComparison.Ordinal) || mnemA != mnemB
            || !string.Equals(remarkA, remarkB, StringComparison.Ordinal) || !argsA.SequenceEqual(argsB))
        {
            throw new InvariantViolation(
                $"ParseLine classification depends on line/file position for '{line}': " +
                $"(label='{labelA}'/'{labelB}', mnem={mnemA}/{mnemB}, remark='{remarkA}'/'{remarkB}', " +
                $"args=[{string.Join(",", argsA)}]/[{string.Join(",", argsB)}])");
        }

        if (kwA.Length != kwB.Length)
        {
            throw new InvariantViolation($"ParseLine token count depends on line/file position for '{line}': {kwA.Length} vs {kwB.Length}");
        }

        for (int i = 0; i < kwA.Length; i++)
        {
            if (kwA[i].Start_Pos != kwB[i].Start_Pos || kwA[i].End_Pos != kwB[i].End_Pos || kwA[i].Type != kwB[i].Type)
            {
                throw new InvariantViolation(
                    $"ParseLine token {i} depends on line/file position for '{line}': " +
                    $"({kwA[i].Start_Pos},{kwA[i].End_Pos},{kwA[i].Type}) vs ({kwB[i].Start_Pos},{kwB[i].End_Pos},{kwB[i].Type})");
            }
        }
    }

    /// <summary>
    /// NEG: a single keyword must be classified consistently by the primitive parsers. The boolean
    /// predicate and the value parser are INDEPENDENT methods (e.g. <c>ContainsKey</c> vs
    /// <c>TryGetValue</c>) that must agree, and a classifier must never report a "valid" register/mnemonic
    /// while the parser hands back the sentinel (or vice-versa) — that is the "bad input is never
    /// classified as valid" law. (Note: a "valid" register need NOT have a positive <c>NBits</c> width —
    /// segment/mask/control/debug/bound registers intentionally size to 0 — so width is NOT asserted.)
    /// </summary>
    public static void CheckClassifyConsistency(string token)
    {
        bool isRn = RegisterTools.IsRn(token, false);
        Rn rn = RegisterTools.ParseRn(token, false);
        if (isRn != (rn != Rn.NOREG))
        {
            throw new InvariantViolation($"register classifier disagreement for '{token}': IsRn={isRn} but ParseRn={rn}");
        }

        (bool valid, Rn reg, int nBits) = RegisterTools.ToRn(token, false);
        if (valid != (rn != Rn.NOREG) || (valid && reg != rn))
        {
            throw new InvariantViolation($"ToRn disagrees with ParseRn for '{token}': ToRn=({valid},{reg},{nBits}) vs ParseRn={rn}");
        }

        bool isMnem = AsmTools.AsmSourceTools.IsMnemonic(token, false);
        Mnemonic m = AsmTools.AsmSourceTools.ParseMnemonic(token, false);
        if (isMnem != (m != Mnemonic.NONE))
        {
            throw new InvariantViolation($"mnemonic classifier disagreement for '{token}': IsMnemonic={isMnem} but ParseMnemonic={m}");
        }
    }

    /// <summary>
    /// CONS (label graph): every label definition the graph tracks points at a real line and carries a
    /// well-formed line-relative span. (Usages are reached through the same <see cref="KeywordID"/>
    /// packing, so checking definitions exercises the same offset code.)
    /// </summary>
    public static void CheckLabelGraphSpans(LabelGraph graph, int lineCount)
    {
        foreach ((string _, List<KeywordID> ids) in graph.Definitions)
        {
            foreach (KeywordID id in ids)
            {
                if (id.LineNumber < 0 || id.LineNumber >= lineCount)
                {
                    throw new InvariantViolation($"LabelGraph definition on line {id.LineNumber} out of range (document has {lineCount} lines) [{id}]");
                }

                if (id.Start_Pos < 0 || id.Start_Pos > id.End_Pos)
                {
                    throw new InvariantViolation($"LabelGraph definition span ({id.Start_Pos},{id.End_Pos}) malformed [{id}]");
                }
            }
        }
    }

    /// <summary>
    /// RES (replay equivalence): a server reaching content X by an EDIT must observe the same outputs as
    /// a fresh server that OPENED X — the open and edit paths are distinct code
    /// (<c>OnTextDocumentOpened</c> adds + parses immediately; <c>UpdateServerSideTextDocument</c> mutates
    /// + re-parses), so this catches stale per-document residue from the pre-edit content surviving the
    /// edit. Compares only the synchronous, deterministic outputs (line model, semantic tokens, folding
    /// ranges); diagnostics are skipped because the AsmSim layer fills them asynchronously.
    /// </summary>
    public static void CheckServerStateEquivalent(LanguageServer edited, LanguageServer fresh, Uri docUri)
    {
        string uri = docUri.ToString();

        string[] le = edited.GetDocumentLinesForTest(uri);
        string[] lf = fresh.GetDocumentLinesForTest(uri);
        if (!le.SequenceEqual(lf, StringComparer.Ordinal))
        {
            throw new InvariantViolation($"edit≠open: line model differs ({le.Length} vs {lf.Length} lines)");
        }

        var stParams = new SemanticTokensParams { TextDocument = new TextDocumentIdentifier { Uri = docUri } };
        CheckSemanticTokensEqual(edited.GetSemanticTokens(stParams)?.Data, fresh.GetSemanticTokens(stParams)?.Data);

        var frParams = new FoldingRangeParams { TextDocument = new TextDocumentIdentifier { Uri = docUri } };
        FoldingRange[] fe = edited.GetFoldingRanges(frParams);
        FoldingRange[] ff = fresh.GetFoldingRanges(frParams);
        if (fe.Length != ff.Length)
        {
            throw new InvariantViolation($"edit≠open: folding-range count differs ({fe.Length} vs {ff.Length})");
        }

        for (int i = 0; i < fe.Length; i++)
        {
            if (fe[i].StartLine != ff[i].StartLine || fe[i].EndLine != ff[i].EndLine)
            {
                throw new InvariantViolation($"edit≠open: folding range {i} differs ([{fe[i].StartLine}..{fe[i].EndLine}] vs [{ff[i].StartLine}..{ff[i].EndLine}])");
            }
        }
    }
}
