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

        // Fetch label CodeLens data (definition position + jump/call reference count) from the
        // LSP server. Reference counting lives on the (multithreaded) server via its
        // assembler-aware LabelGraph; the tagger only renders the result — it does NOT parse.
        IReadOnlyList<AsmLabelRef> labels = await SimStatePipeClient.Instance
            .GetCodeLensDataAsync(this.documentUri)
            .ConfigureAwait(false);

        TaggerLog($"CreateTagsAsync: got {labels.Count} label(s) from server");

        var labelsByLine = new Dictionary<int, AsmLabelRef>();
        foreach (var lbl in labels)
            labelsByLine[lbl.DefinitionLine] = lbl;

        // Build tag list
        var tags = new List<TaggedTrackingTextRange<CodeLensTag>>();

        // ── Label reference-count tags (positioned on the label token reported by the server) ──
        foreach (var line in document.Lines)
        {
            if (!labelsByLine.TryGetValue(line.LineNumber, out var lbl)) continue;

            int lineLen = line.Text.Length;
            int col = Math.Max(0, Math.Min(lbl.DefinitionColumn, Math.Max(0, lineLen - 1)));
            int len = Math.Max(1, Math.Min(lbl.DefinitionLength, lineLen - col));
            int tagStart = line.Text.Start + col;

            tags.Add(new(
                new(document, tagStart, len, TextRangeTrackingMode.ExtendForwardAndBackward),
                new(AsmLabelKind)
                {
                    UniqueIdentifier = lbl.Label,
                    Description = $"refcount:{lbl.ReferenceCount}|Label: {lbl.Label}",
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
