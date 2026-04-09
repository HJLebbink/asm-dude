// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Threading;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only
#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

/// <summary>
/// Scans assembly documents and creates two kinds of CodeLensTags:
/// <list type="bullet">
///   <item><see cref="AsmLabelKind"/> — label definitions with reference counts.</item>
///   <item><see cref="AsmSimStateKind"/> — instruction lines with Z3-proven sim-state labels.</item>
/// </list>
///
/// Sim-state data is fetched from the LSP server via <see cref="SimStatePipeClient"/>.
/// The tagger re-runs whenever the document changes or the pipe client raises
/// <see cref="SimStatePipeClient.SimStateUpdated"/> for this document's URI.
/// </summary>
internal class AsmCodeLensTagger : TextViewTagger<CodeLensTag>
{
    public static readonly CodeElementKind AsmLabelKind    = "AsmLabel";
    public static readonly CodeElementKind AsmSimStateKind = "AsmSimState";

    private readonly AsmCodeLensTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly AsyncSemaphore semaphore = new(1);

    private ITextDocumentSnapshot? currentDocumentSnapshot;
    private bool needsUpdate;
    private bool updateRunning;

    public AsmCodeLensTagger(AsmCodeLensTaggerProvider provider, Uri documentUri)
    {
        this.provider = provider;
        this.documentUri = documentUri;
        TaggerLog($"Created tagger for {documentUri}");

        // Subscribe to sim-state updates: re-tag when the LSP server finishes simulating
        SimStatePipeClient.Instance.SimStateUpdated += this.OnSimStateUpdated;
    }

    public override void Dispose()
    {
        SimStatePipeClient.Instance.SimStateUpdated -= this.OnSimStateUpdated;
        this.provider.RemoveTagger(this.documentUri, this);
        this.semaphore.Dispose();
        base.Dispose();
    }

    protected override async Task OnTextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
    {
        if (args.Edits.Count == 0) return;

        var documentAfter = args.AfterTextView.Document;
        using var semaphoreReleaser = await this.semaphore.EnterAsync();

        if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < documentAfter.RpcContract.Version)
        {
            this.currentDocumentSnapshot = documentAfter;
            _ = this.RunCreateTagsAsync();
        }
    }

    protected override async Task OnRequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot is null) return;

        using var semaphoreReleaser = await this.semaphore.EnterAsync();

        if (recalculateAll)
        {
            this.currentDocumentSnapshot =
                this.currentDocumentSnapshot is not null && this.currentDocumentSnapshot.RpcContract.Version >= requestedRanges.TextDocumentSnapshot.RpcContract.Version
                    ? this.currentDocumentSnapshot
                    : requestedRanges.TextDocumentSnapshot;
            _ = this.RunCreateTagsAsync();
        }
        else if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < requestedRanges.TextDocumentSnapshot.RpcContract.Version)
        {
            this.currentDocumentSnapshot = requestedRanges.TextDocumentSnapshot;
            _ = this.RunCreateTagsAsync();
        }
    }

    private void OnSimStateUpdated(Uri updatedUri)
    {
        // Only refresh if the notification is for this document
        if (!updatedUri.Equals(this.documentUri)) return;

        TaggerLog($"SimStateUpdated for {updatedUri} — scheduling tag refresh");
        _ = this.ScheduleRefreshAsync();
    }

    private async Task ScheduleRefreshAsync()
    {
        using var semaphoreReleaser = await this.semaphore.EnterAsync();
        if (this.currentDocumentSnapshot is not null)
        {
            TaggerLog("ScheduleRefreshAsync: snapshot available, running CreateTags");
            _ = this.RunCreateTagsAsync();
        }
        else
        {
            TaggerLog("ScheduleRefreshAsync: snapshot is null, skipping");
        }
    }

    private async Task RunCreateTagsAsync()
    {
        this.needsUpdate = true;

        if (this.updateRunning) return;

        this.updateRunning = true;
        while (true)
        {
            ITextDocumentSnapshot document;
            using (var semaphoreReleaser = await this.semaphore.EnterAsync())
            {
                if (!this.needsUpdate || this.currentDocumentSnapshot is null)
                {
                    this.updateRunning = false;
                    return;
                }

                this.needsUpdate = false;
                document = this.currentDocumentSnapshot;
            }

            await this.CreateTagsAsync(document);
        }
    }

    private async Task CreateTagsAsync(ITextDocumentSnapshot document)
    {
        TaggerLog($"CreateTagsAsync: starting for {this.documentUri}");

        // Fetch current sim-state labels from the LSP server (via named pipe)
        Dictionary<int, string> simStates = await SimStatePipeClient.Instance
            .GetSimStatesAsync(this.documentUri)
            .ConfigureAwait(false);

        TaggerLog($"CreateTagsAsync: got {simStates.Count} sim states");

        // First pass: find all label definitions and collect all label names
        var labelDefs = new List<(string name, int lineIndex, int start, int length)>();
        var allLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in document.Lines)
        {
            string lineText = line.Text.CopyToString();
            string? labelName = TryParseLabelDefinition(lineText);
            if (labelName != null)
            {
                allLabels.Add(labelName);
                int leadingSpaces = lineText.Length - lineText.TrimStart().Length;
                int tagStart = line.Text.Start + leadingSpaces;
                int tagLength = line.Text.Length - leadingSpaces;
                labelDefs.Add((labelName, line.LineNumber, tagStart, tagLength));
            }
        }

        // Second pass: count references for each label
        var refCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in allLabels)
            refCounts[label] = 0;

        foreach (var line in document.Lines)
        {
            string lineText = line.Text.CopyToString();

            string trimmed = lineText.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#') continue;

            int commentIdx = lineText.IndexOf(';');
            string codePart = commentIdx >= 0 ? lineText[..commentIdx] : lineText;

            foreach (var label in allLabels)
            {
                int searchStart = 0;
                while (true)
                {
                    int idx = codePart.IndexOf(label, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;

                    int endIdx = idx + label.Length;

                    bool startOk = idx == 0 || !IsIdentifierChar(codePart[idx - 1]);
                    bool endOk = endIdx >= codePart.Length || !IsIdentifierChar(codePart[endIdx]);
                    bool isDefinition = endOk && endIdx < codePart.Length && codePart[endIdx] == ':';

                    if (startOk && endOk && !isDefinition)
                        refCounts[label]++;

                    searchStart = endIdx;
                }
            }
        }

        // Build tag list
        var tags = new List<TaggedTrackingTextRange<CodeLensTag>>();

        // ── Label reference-count tags ────────────────────────────────────────
        foreach (var (name, lineIndex, start, length) in labelDefs)
        {
            int count = refCounts.GetValueOrDefault(name, 0);
            tags.Add(new(
                new(document, start, length, TextRangeTrackingMode.ExtendForwardAndBackward),
                new(AsmLabelKind)
                {
                    UniqueIdentifier = name,
                    Description = $"refcount:{count}|Label: {name}",
                    DisplayBeforeCreatingCodeLenses = true,
                }));
        }

        // ── Sim-state tags — one per instruction line that has a known state ──
        if (simStates.Count > 0)
        {
            foreach (var line in document.Lines)
            {
                if (!simStates.TryGetValue(line.LineNumber, out string? simLabel)) continue;
                if (string.IsNullOrEmpty(simLabel)) continue;

                // Position the tag above the instruction text, 2 chars to the right of
                // the first non-whitespace character, so VS renders the CodeLens above
                // the instruction rather than at the left margin.
                string lineText = line.Text.CopyToString();
                int instrCol = lineText.Length - lineText.TrimStart().Length;
                int offsetCol = instrCol + 2;
                int tagStart = line.Text.Start + Math.Min(offsetCol, Math.Max(0, line.Text.Length - 1));
                int tagLen = Math.Max(1, line.Text.Length - Math.Min(offsetCol, line.Text.Length - 1));

                tags.Add(new(
                    new(document, tagStart, tagLen, TextRangeTrackingMode.ExtendForwardAndBackward),
                    new(AsmSimStateKind)
                    {
                        UniqueIdentifier = $"simstate:{line.LineNumber}",
                        Description = $"simstate:|{simLabel}",
                        DisplayBeforeCreatingCodeLenses = true,
                    }));
            }
        }

        TaggerLog($"CreateTagsAsync: calling UpdateTagsAsync with {tags.Count} total tags ({simStates.Count} sim state tags)");
        await this.UpdateTagsAsync([new(document, 0, document.Length)], tags, CancellationToken.None);
        TaggerLog("CreateTagsAsync: UpdateTagsAsync done");
    }

    /// <summary>
    /// Tries to parse a label definition from a line of assembly code.
    /// Returns the label name if found, null otherwise.
    /// </summary>
    private static string? TryParseLabelDefinition(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.Length == 0) return null;

        if (trimmed[0] == ';' || trimmed[0] == '#') return null;

        int colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0) return null;

        string candidate = trimmed[..colonIdx].TrimEnd();

        if (candidate.Contains(' ') || candidate.Contains('\t')) return null;

        char first = candidate[0];
        if (!char.IsLetter(first) && first != '_' && first != '.' && first != '@') return null;

        for (int i = 1; i < candidate.Length; i++)
        {
            if (!IsIdentifierChar(candidate[i])) return null;
        }

        return candidate;
    }

    private static bool IsIdentifierChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '@' || c == '$' || c == '?';

    private static readonly string TaggerLogPath = Path.Combine(Path.GetTempPath(), "asmdude-tagger.log");
    private static readonly object TaggerLogLock = new();

    private static void TaggerLog(string msg)
    {
        try
        {
            lock (TaggerLogLock)
            {
                File.AppendAllText(TaggerLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
