// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

// RS0030: VS-Threading bans AsyncSemaphore because waiting on it on the VS UI thread can hang VS. This is an
// OUT-OF-PROCESS extension (runs in a ServiceHub host, not the VS UI thread — see CLAUDE.md), so there is no
// UI thread to hang and no JoinableTaskContext to coordinate with; ReentrantSemaphore offers no benefit here.
// The semaphore just serializes the tagger's async refresh off any UI thread.
#pragma warning disable RS0030 // Do not use banned APIs

using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Threading;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only
#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

/// <summary>
/// Scans assembly documents and creates two kinds of CodeLensTags:
/// <list type="bullet">
///   <item><see cref="AsmLabelKind"/> — label definitions with reference counts.</item>
///   <item><see cref="AsmSimStateKind"/> — instruction lines with Z3-proven sim-state labels.</item>
/// </list>
///
/// <para><b>Publishing model (whole-document + content dedup + bounded self-heal).</b> Following the official
/// MS <c>MarkdownCodeLensTagger</c> sample, every trigger computes tags for the WHOLE document and publishes
/// them with a whole-document range (<c>UpdateTagsAsync([0, length], allTags)</c>) — so VS holds a tag for
/// every line at once and a pure scroll needs no re-request (this is what stops the "lens disappears below the
/// viewport" blank). On top of the sample we add two things the sample omits but our environment needs:</para>
/// <list type="number">
///   <item><b>Content dedup.</b> VS re-issues <c>OnRequestTagsAsync(recalculateAll:true)</c> as an echo after
///   every publish and polls it on scroll. Re-publishing an IDENTICAL whole-doc set OUTDATES then re-adds every
///   lens (the visible "removed then rewritten" flicker). So if the freshly built tag set is byte-identical to
///   the last PUBLISHED set we skip — which also breaks the echo chain so VS quiets.</item>
///   <item><b>Re-serve once after every content change.</b> VS only RENDERS a viewport in response to one of
///   its own requests (a proactive publish alone is not enough — that was the round-3 blank: a sim push had
///   pre-published the content, so VS's next request matched and was skipped, and the lenses never drew). So a
///   content publish (sim/edit) clears the request-serve marker, forcing the NEXT request to re-serve; from then
///   on identical requests (VS's endless echo/poll of a static viewport) skip forever — no self-heal, because
///   re-publishing identical tags on every poll is exactly the continuous flicker.</item>
/// </list>
///
/// Sim-state/label data is fetched from the LSP server via <see cref="SimStatePipeClient"/>; the per-document
/// cache (<see cref="dataDirty_"/>) re-fetches over the pipe ONLY on an edit or sim push, so a scroll/echo
/// rebuild is a local re-render with no IPC.
/// </summary>
internal class AsmCodeLensTagger : TextViewTagger<CodeLensTag>
{
    public static readonly CodeElementKind AsmLabelKind = "AsmLabel";
    public static readonly CodeElementKind AsmSimStateKind = "AsmSimState";

    private readonly AsmCodeLensTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly AsyncSemaphore semaphore = new(1);

    // The newest document snapshot we've been asked about (the unit we (re)tag). Under `semaphore`.
    private ITextDocumentSnapshot? currentDocumentSnapshot;

    // Whole-document content signature of the last set we actually pushed via UpdateTagsAsync (dedups a
    // redundant content publish, e.g. the sim's final "completed" echo).
    private string? lastPublishedSig_;

    // Key of what we last served to a VS REQUEST: the requested viewport's offset span + the content signature.
    // VS keeps tags only for the viewport it asked about and DISCARDS the rest, so on scroll it re-requests the
    // NEW viewport with recalculateAll; we must serve that (different span ⇒ different key). Only a repeat of the
    // SAME viewport span AND same content is VS's echo/poll ⇒ skipped (no flicker). The span is min-start..max-
    // end over the requested ranges, which is independent of how VS partitions them (1 range vs N per-line).
    private string? lastServeKey_;

    // ── Server-data cache ───────────────────────────────────────────────────────────────────────
    // Sim-state + label data only change on an edit or a server sim push. A scroll/echo rebuild reuses the
    // cache (no pipe round-trip). dataDirty_ is set from event threads ⇒ volatile.
    private volatile bool dataDirty_ = true;
    private Dictionary<int, string>? cachedSimStates_;
    private IReadOnlyList<AsmLabelRef>? cachedLabels_;

    public AsmCodeLensTagger(AsmCodeLensTaggerProvider provider, Uri documentUri)
    {
        this.provider = provider;
        this.documentUri = documentUri;
        TaggerLog($"Created tagger for {documentUri}");

        SimStatePipeClient.Instance.SimStateUpdated += this.OnSimStateUpdated;
    }

    public override void Dispose()
    {
        SimStatePipeClient.Instance.SimStateUpdated -= this.OnSimStateUpdated;
        this.provider.RemoveTagger(this.documentUri, this);
        this.semaphore.Dispose();
        base.Dispose();
    }

    protected override async Task OnRequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot is null) return;

        var snapshot = requestedRanges.TextDocumentSnapshot;

        // Viewport identity: the offset span the request covers (partition-independent — same for 1 range or N).
        int minStart = int.MaxValue, maxEnd = int.MinValue;
        foreach (var r in requestedRanges) { if (r.Start < minStart) minStart = r.Start; if (r.End > maxEnd) maxEnd = r.End; }
        string viewport = $"{minStart}..{maxEnd}";

        using var releaser = await this.semaphore.EnterAsync();

        // Track the newest snapshot (ignore a stale request older than one we already have).
        if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version <= snapshot.RpcContract.Version)
            this.currentDocumentSnapshot = snapshot;

        await this.PublishWholeDocAsync(this.currentDocumentSnapshot!, requestViewport: viewport,
            $"request recalcAll={recalculateAll} ranges={requestedRanges.Count} vp={viewport} ver={snapshot.RpcContract.Version}");
    }

    protected override async Task OnTextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
    {
        if (args.Edits.Count == 0) return;

        var documentAfter = args.AfterTextView.Document;
        using var releaser = await this.semaphore.EnterAsync();

        if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < documentAfter.RpcContract.Version)
        {
            // The edit can change labels/sim state and shifts line positions ⇒ refetch + re-tag the new doc.
            this.dataDirty_ = true;
            this.currentDocumentSnapshot = documentAfter;
            await this.PublishWholeDocAsync(documentAfter, requestViewport: null, "edit");
        }
    }

    private void OnSimStateUpdated(Uri updatedUri, bool completed)
    {
        if (!updatedUri.Equals(this.documentUri)) return;
        this.dataDirty_ = true;
        _ = this.OnSimStateUpdatedAsync(completed);
    }

    private async Task OnSimStateUpdatedAsync(bool completed)
    {
        using var releaser = await this.semaphore.EnterAsync();
        if (this.currentDocumentSnapshot is null) return; // first VS request will publish
        await this.PublishWholeDocAsync(this.currentDocumentSnapshot, requestViewport: null, $"simPush completed={completed}");
    }

    /// <summary>
    /// Builds the whole-document tag set and publishes it with a whole-document range, with two skips: a
    /// content trigger (sim/edit) whose tags are byte-identical to what's already published is redundant
    /// (skip); a VS request whose tags match the last we served to a request is an echo/poll (skip — this is
    /// what kills the continuous-redraw flicker). A content publish clears <see cref="servedRequestSig_"/> so
    /// the NEXT request re-serves (VS renders only on a request response). Must hold <see cref="semaphore"/>.
    /// </summary>
    /// <param name="requestViewport"><c>null</c> for a content trigger (edit/sim push); the requested viewport's
    /// offset span for a VS tag request (drives the viewport-aware echo skip).</param>
    private async Task PublishWholeDocAsync(ITextDocumentSnapshot document, string? requestViewport, string reason)
    {
        var (tags, taggedLines, signature) = await this.BuildTagsAsync(document).ConfigureAwait(false);

        if (requestViewport is null) // content trigger (sim/edit)
        {
            if (signature == this.lastPublishedSig_)
            {
                TaggerLogVerbose($"skip redundant content publish [{reason}]");
                return;
            }

            // New content ⇒ publish, and force the next request (any viewport) to re-serve so VS draws it.
            this.lastServeKey_ = null;
        }
        else // VS tag request
        {
            string serveKey = requestViewport + "|" + signature;
            if (serveKey == this.lastServeKey_)
            {
                TaggerLogVerbose($"skip echo request [{reason}]");
                return;
            }

            this.lastServeKey_ = serveKey;
        }

        // Whole-document range: invalidates every previous tag and replaces it with the freshly built set.
        await this.UpdateTagsAsync([new(document, 0, document.Length)], tags, CancellationToken.None);
        this.lastPublishedSig_ = signature;

        TaggerLog($"published {tags.Count} tag(s) over {taggedLines} line(s) [whole-doc] [{reason}]");
    }

    /// <summary>
    /// Fetches sim-state + label data from the LSP server (cached unless an edit/sim push set
    /// <see cref="dataDirty_"/>) and builds the whole-document CodeLens tag list plus a content signature
    /// (every tag's kind+offset+length+payload) used to suppress identical republishes.
    /// </summary>
    private async Task<(List<TaggedTrackingTextRange<CodeLensTag>> tags, int taggedLines, string signature)> BuildTagsAsync(ITextDocumentSnapshot document)
    {
        Dictionary<int, string> simStates;
        IReadOnlyList<AsmLabelRef> labels;
        if (this.dataDirty_ || this.cachedSimStates_ is null || this.cachedLabels_ is null)
        {
            simStates = await SimStatePipeClient.Instance.GetSimStatesAsync(this.documentUri).ConfigureAwait(false);
            labels = await SimStatePipeClient.Instance.GetCodeLensDataAsync(this.documentUri).ConfigureAwait(false);
            this.cachedSimStates_ = simStates;
            this.cachedLabels_ = labels;
            this.dataDirty_ = false;
            TaggerLog($"fetched {simStates.Count} sim states, {labels.Count} label(s)");
        }
        else
        {
            simStates = this.cachedSimStates_;
            labels = this.cachedLabels_;
        }

        var labelsByLine = new Dictionary<int, AsmLabelRef>();
        foreach (var lbl in labels)
            labelsByLine[lbl.DefinitionLine] = lbl;

        var tags = new List<TaggedTrackingTextRange<CodeLensTag>>();
        var lines = new HashSet<int>();
        var sig = new System.Text.StringBuilder();

        foreach (var line in document.Lines)
        {
            int ln = line.LineNumber;
            int lineLen = line.Text.Length;
            if (lineLen == 0) continue; // only non-empty lines carry a lens

            // ── Label reference-count tag (positioned on the label token reported by the server) ──
            if (labelsByLine.TryGetValue(ln, out var lbl))
            {
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
                sig.Append("L|").Append(tagStart).Append(':').Append(len).Append(':')
                   .Append(lbl.ReferenceCount).Append(':').Append(lbl.Label).Append(';');
                lines.Add(ln);
            }

            // ── Sim-state tag (one per instruction line that has a known state) ──
            if (simStates.TryGetValue(ln, out string? simLabel) && !string.IsNullOrEmpty(simLabel))
            {
                // 2 chars right of the first non-whitespace char, so VS renders the lens above the instruction.
                string lineText = line.Text.CopyToString();
                int instrCol = lineText.Length - lineText.TrimStart().Length;
                int offsetCol = instrCol + 2;
                int tagStart = line.Text.Start + Math.Min(offsetCol, Math.Max(0, lineLen - 1));
                int tagLen = Math.Max(1, lineLen - Math.Min(offsetCol, lineLen - 1));

                tags.Add(new(
                    new(document, tagStart, tagLen, TextRangeTrackingMode.ExtendForwardAndBackward),
                    new(AsmSimStateKind)
                    {
                        UniqueIdentifier = $"simstate:{ln}",
                        Description = $"simstate:|{simLabel}",
                        DisplayBeforeCreatingCodeLenses = true,
                    }));
                sig.Append("S|").Append(tagStart).Append(':').Append(tagLen).Append(':').Append(simLabel).Append(';');
                lines.Add(ln);
            }
        }

        return (tags, lines.Count, sig.ToString());
    }

    // ── Diagnostic logging (shared AsmLog, category "CodeLens") ──────────────────────────────────
    // Actual publishes + data fetches log at Info (low frequency). The high-frequency per-request skip decisions
    // (echo / redundant-content) log at Trace so normal use stays quiet; raise with ASMDUDE_LOGLEVEL=trace to
    // see them when resuming the redraw research (see CODELENS_REDRAW_RESEARCH.md).
    internal static void TaggerLog(string msg,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Info, "CodeLens", msg, member, line);

    internal static void TaggerLogVerbose(string msg,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Trace, "CodeLens", msg, member, line);
}
