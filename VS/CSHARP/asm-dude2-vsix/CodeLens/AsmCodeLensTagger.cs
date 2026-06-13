// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

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
/// Sim-state data is fetched from the LSP server via <see cref="SimStatePipeClient"/>.
/// The tagger re-runs whenever the document changes or the pipe client raises
/// <see cref="SimStatePipeClient.SimStateUpdated"/> for this document's URI.
/// </summary>
internal class AsmCodeLensTagger : TextViewTagger<CodeLensTag>
{
    public static readonly CodeElementKind AsmLabelKind = "AsmLabel";
    public static readonly CodeElementKind AsmSimStateKind = "AsmSimState";

    private readonly AsmCodeLensTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly AsyncSemaphore semaphore = new(1);

    private ITextDocumentSnapshot? currentDocumentSnapshot;

    // Coalesces sim refreshes: many sim-state pushes can arrive while one publish is running.
    private bool simRefreshPending;
    private bool simRefreshRunning;

    // ── Server-data cache (the per-scroll latency fix) ──────────────────────────────────────────
    // Sim-state + label data only change when the document is edited or the server pushes a
    // simStateUpdated. A plain scroll re-issues OnRequestTagsAsync (several times) but changes
    // NOTHING — yet BuildTagsAsync used to make two blocking pipe round-trips (each with a 5 s
    // timeout) on every one of those calls. While the server is busy with the slow initial Z3 sim,
    // those round-trips queue behind a lock-contended server and stall for seconds, re-fetching data
    // identical to what is already on screen. We therefore cache the last server answer and re-fetch
    // ONLY when it can have changed: an edit (OnTextViewChangedAsync) or a sim push (OnSimStateUpdated)
    // sets dataDirty_; a pure scroll reuses the cache, making it a local re-render. Accessed only under
    // `semaphore` (all of OnRequestTags/RunSimRefresh/OnTextViewChanged enter it); dataDirty_ is set
    // from event threads so it is volatile.
    private volatile bool dataDirty_ = true;
    private Dictionary<int, string>? cachedSimStates_;
    private IReadOnlyList<AsmLabelRef>? cachedLabels_;

    // Decides which lines to (re)publish (VS-free, unit-tested in CodeLensPublishPlannerTests).
    // Per-line content dedup: a line is (re)emitted only when its content differs from what VS was last
    // sent. Echoes and VS's partial re-requests therefore publish nothing (no churn). Full re-supply is
    // driven by planner.Reset() — ONLY on edit (line positions shift); a sim refresh is a normal content
    // refresh. See CodeLensPublishPlanner.
    private readonly CodeLensPublishPlanner planner = new();

    // ── Storm self-diagnosis (so the log explains the publish↔request loop without guesswork) ──
    // The dedup lives in the planner. This is only the safety net: a rolling publish-rate counter emits
    // ONE throttled WARN if the loop ever fails to converge, plus lastPublishTick_ feeds the "gap=" log.
    private long lastPublishTick_;
    private readonly Queue<long> recentPublishTicks_ = new();
    private long stormWarnedTick_;
    private const int StormWindowMs = 3000;   // look back this far …
    private const int StormThreshold = 30;    // … this many publishes ⇒ not converging (legit scroll: 1
                                              // publish per distinct viewport, well under this; a real
                                              // storm re-publishes continuously because the echo isn't caught)
    private const int StormWarnCooldownMs = 5000;

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
        using var releaser = await this.semaphore.EnterAsync();

        if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < documentAfter.RpcContract.Version)
        {
            this.currentDocumentSnapshot = documentAfter;
            // Positions shifted: forget per-line publish state so the next publish recomputes from
            // scratch, then proactively refresh (per-line ranges). The edit can change labels/sim
            // state, so invalidate the server-data cache too.
            this.planner.Reset();
            this.dataDirty_ = true;
            await this.PublishAsync(documentAfter, requestedRanges: null);
        }
    }

    protected override async Task OnRequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        long reqVer = requestedRanges.TextDocumentSnapshot?.RpcContract.Version ?? -1;
        long curVer = this.currentDocumentSnapshot?.RpcContract.Version ?? -1;
        // Logged at Info (temporarily) to confirm whether VS sets recalculateAll on the publish ECHO —
        // that determines whether honoring the flag converges or storms. See CodeLensPublishPlanner.
        TaggerLog($"OnRequestTagsAsync: recalculateAll={recalculateAll}, ranges={requestedRanges.Count}, reqVer={reqVer}, curVer={curVer}");

        if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot is null) return;

        var snapshot = requestedRanges.TextDocumentSnapshot;
        var ranges = new List<TextRange>(requestedRanges.Count);
        foreach (var r in requestedRanges) ranges.Add(r);

        using var releaser = await this.semaphore.EnterAsync();

        // Ignore a stale request that references an older snapshot than we already have.
        if (this.currentDocumentSnapshot is not null && this.currentDocumentSnapshot.RpcContract.Version > snapshot.RpcContract.Version)
            return;

        this.currentDocumentSnapshot = snapshot;

        // Answer EXACTLY the ranges VS asked about. recalculateAll ⇒ VS dropped our tags, re-supply them
        // (even unchanged); otherwise publish only content changes so the echo collapses to nothing.
        await this.PublishAsync(snapshot, ranges, recalculateAll);
    }

    private void OnSimStateUpdated(Uri updatedUri, bool completed)
    {
        // Only refresh if the notification is for this document
        if (!updatedUri.Equals(this.documentUri)) return;

        // The server has new sim-state data — invalidate the cache so the refresh re-fetches.
        // A completion push is treated as a normal content refresh: the per-line planner publishes only
        // the lines whose state changed. (It must NOT Reset/full-republish — the sim "completes"
        // repeatedly during a run, and each Reset re-emitted every lens, churning them.)
        this.dataDirty_ = true;

        TaggerLog($"SimStateUpdated for {updatedUri} (completed={completed}) — scheduling tag refresh");
        _ = this.RunSimRefreshAsync();
    }

    // Coalesced incremental refresh driven by sim-state pushes. Publishes only the changed lines
    // (per-line ranges) — NEVER the whole document — so VS keeps unchanged lenses materialized.
    private async Task RunSimRefreshAsync()
    {
        this.simRefreshPending = true;
        if (this.simRefreshRunning) return;

        this.simRefreshRunning = true;
        try
        {
            while (true)
            {
                using var releaser = await this.semaphore.EnterAsync();
                if (!this.simRefreshPending || this.currentDocumentSnapshot is null)
                    return;

                this.simRefreshPending = false;
                var snapshot = this.currentDocumentSnapshot;

                try
                {
                    await this.PublishAsync(snapshot, requestedRanges: null);
                }
                catch (Exception ex)
                {
                    // A failed pass must NOT wedge the tagger (simRefreshRunning is reset in finally).
                    TaggerLog($"PublishAsync (sim) threw: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            this.simRefreshRunning = false;
        }
    }

    /// <summary>
    /// Publishes CodeLens tags for <paramref name="document"/>.
    /// <list type="bullet">
    ///   <item><paramref name="requestedRanges"/> non-null (a VS tag request): answers EXACTLY those
    ///   ranges with the tags that fall inside them. Never the whole document — that would make VS
    ///   treat all tags as outdated and re-request endlessly (an OnRequestTags ↔ UpdateTags storm).</item>
    ///   <item><paramref name="requestedRanges"/> null (a sim/edit refresh): publishes only the lines
    ///   whose tags changed since the last publish (per-line ranges), keeping unchanged lenses warm.</item>
    /// </list>
    /// <paramref name="recalculateAll"/> is the VS request's flag (false for proactive refreshes): when
    /// true VS has dropped our tags, so in-scope lenses are re-emitted even if their content is unchanged.
    /// </summary>
    private async Task PublishAsync(ITextDocumentSnapshot document, IReadOnlyList<TextRange>? requestedRanges, bool recalculateAll = false)
    {
        var (built, sigByLine, lineRanges) = await this.BuildTagsAsync(document).ConfigureAwait(false);

        // Reduce the requested RANGES to a set of LINES (the planner is partition-independent — that
        // is what defeats VS's shifting range groupings). Null = a proactive sim/edit refresh.
        HashSet<int>? requestedLines = null;
        if (requestedRanges is not null)
        {
            requestedLines = [];
            foreach (var (line, range) in lineRanges)
                if (requestedRanges.Any(r => r.Contains(range.Start)))
                    requestedLines.Add(line);
        }

        long now = Environment.TickCount64;
        long gap = this.lastPublishTick_ == 0 ? -1 : now - this.lastPublishTick_;

        // All the publish decision logic lives in the (unit-tested) planner; the tagger just renders the
        // outcome and logs it so the publish↔request loop is legible in the AsmDude2 pane.
        PublishPlan plan = this.planner.Plan(requestedLines, sigByLine, recalculateAll);
        var toPublish = plan.Lines;

        string scope = requestedRanges is null
            ? $"sim/edit, {sigByLine.Count} tagged lines"
            : $"{requestedRanges.Count} req ranges→{requestedLines!.Count} lines, recalcAll={recalculateAll}, gap={gap}ms";

        if (plan.Outcome == PublishOutcome.EchoSkipped)
        {
            TaggerLog($"PublishAsync: skip ECHO — identical re-request ({scope})");
            return;
        }

        if (toPublish.Count == 0)
        {
            TaggerLog($"PublishAsync: nothing to publish ({scope}) — converged [loop-safe]");
            return;
        }

        var updatedRanges = new List<TextRange>(toPublish.Count);
        foreach (int ln in toPublish)
            if (lineRanges.TryGetValue(ln, out var range)) updatedRanges.Add(range);

        if (updatedRanges.Count == 0)
        {
            TaggerLog($"PublishAsync: {toPublish.Count} line(s) to publish but no ranges ({scope}) — skip");
            return;
        }

        var newTags = built.Where(b => toPublish.Contains(b.line)).Select(b => b.tag).ToList();
        await this.UpdateTagsAsync(updatedRanges, newTags, CancellationToken.None);

        this.lastPublishTick_ = now;
        this.NotePublishForStormDetection(now);

        TaggerLog($"PublishAsync: published {toPublish.Count} line(s) [{string.Join(",", toPublish.OrderBy(x => x))}], {newTags.Count} tags ({scope})");
    }

    /// <summary>Records a publish and, if the publish rate shows the publish↔request loop is NOT
    /// converging, emits one throttled WARN that names the likely cause — so a storm is self-evident in
    /// the log instead of having to be inferred from the density of publish lines.</summary>
    private void NotePublishForStormDetection(long now)
    {
        this.recentPublishTicks_.Enqueue(now);
        while (this.recentPublishTicks_.Count > 0 && now - this.recentPublishTicks_.Peek() > StormWindowMs)
            this.recentPublishTicks_.Dequeue();

        if (this.recentPublishTicks_.Count >= StormThreshold && now - this.stormWarnedTick_ > StormWarnCooldownMs)
        {
            this.stormWarnedTick_ = now;
            AsmTools.AsmLog.Warn("CodeLens",
                $"publish↔request loop NOT converging: {this.recentPublishTicks_.Count} publishes in {StormWindowMs}ms for {this.documentUri.Segments[^1]}. " +
                "Per-line content dedup should make repeat/partial requests publish nothing. If the SAME lines are re-published " +
                "every request, the tag data (sigByLine from BuildTagsAsync) is unstable for unchanged lines — fix the data " +
                "source, not the planner. See CodeLensPublishPlanner.");
        }
    }

    /// <summary>
    /// Fetches sim-state + label data from the LSP server and builds the CodeLens tags. Returns the
    /// flat tag list (each with its absolute start offset and line), a per-line signature (for the
    /// incremental diff) and each non-empty line's text range (the unit of invalidation for VS).
    /// </summary>
    private async Task<(List<(int start, int line, TaggedTrackingTextRange<CodeLensTag> tag)> built, Dictionary<int, string> sigByLine, Dictionary<int, TextRange> lineRanges)> BuildTagsAsync(ITextDocumentSnapshot document)
    {
        // Reference counting lives on the (multithreaded) server via its assembler-aware LabelGraph;
        // the tagger only renders the result — it does NOT parse. The two pipe round-trips below are
        // the expensive part of a publish (each blocks up to 5 s if the server is busy with the slow
        // initial sim), so we only pay them when the data can have changed (dataDirty_, set by edits
        // and sim pushes). A pure scroll reuses the cache and re-renders locally — no IPC.
        Dictionary<int, string> simStates;
        IReadOnlyList<AsmLabelRef> labels;
        if (this.dataDirty_ || this.cachedSimStates_ is null || this.cachedLabels_ is null)
        {
            simStates = await SimStatePipeClient.Instance
                .GetSimStatesAsync(this.documentUri).ConfigureAwait(false);
            labels = await SimStatePipeClient.Instance
                .GetCodeLensDataAsync(this.documentUri).ConfigureAwait(false);
            this.cachedSimStates_ = simStates;
            this.cachedLabels_ = labels;
            this.dataDirty_ = false;
            TaggerLog($"BuildTagsAsync: fetched {simStates.Count} sim states, {labels.Count} label(s)");
        }
        else
        {
            simStates = this.cachedSimStates_;
            labels = this.cachedLabels_;
            TaggerLogVerbose($"BuildTagsAsync: reused cache ({simStates.Count} sim states, {labels.Count} label(s)) — no IPC");
        }

        var labelsByLine = new Dictionary<int, AsmLabelRef>();
        foreach (var lbl in labels)
            labelsByLine[lbl.DefinitionLine] = lbl;

        var built = new List<(int start, int line, TaggedTrackingTextRange<CodeLensTag> tag)>();
        var sigByLine = new Dictionary<int, string>();
        var lineRanges = new Dictionary<int, TextRange>();

        foreach (var line in document.Lines)
        {
            int ln = line.LineNumber;
            int lineLen = line.Text.Length;

            // The line's own text extent — never padded (Math.Max(1,…) would push an empty trailing
            // line's range past the document end and throw). Only non-empty lines carry a CodeLens.
            if (lineLen > 0)
                lineRanges[ln] = new(document, line.Text.Start, lineLen);

            // ── Label reference-count tag (positioned on the label token reported by the server) ──
            if (labelsByLine.TryGetValue(ln, out var lbl))
            {
                int col = Math.Max(0, Math.Min(lbl.DefinitionColumn, Math.Max(0, lineLen - 1)));
                int len = Math.Max(1, Math.Min(lbl.DefinitionLength, lineLen - col));
                int tagStart = line.Text.Start + col;

                built.Add((tagStart, ln, new(
                    new(document, tagStart, len, TextRangeTrackingMode.ExtendForwardAndBackward),
                    new(AsmLabelKind)
                    {
                        UniqueIdentifier = lbl.Label,
                        Description = $"refcount:{lbl.ReferenceCount}|Label: {lbl.Label}",
                        DisplayBeforeCreatingCodeLenses = true,
                    })));
                AppendSig(sigByLine, ln, $"L:{col},{len},{lbl.ReferenceCount},{lbl.Label}");
            }

            // ── Sim-state tag (one per instruction line that has a known state) ──
            if (simStates.TryGetValue(ln, out string? simLabel) && !string.IsNullOrEmpty(simLabel))
            {
                // Position the tag above the instruction text, 2 chars to the right of the first
                // non-whitespace character, so VS renders the CodeLens above the instruction.
                string lineText = line.Text.CopyToString();
                int instrCol = lineText.Length - lineText.TrimStart().Length;
                int offsetCol = instrCol + 2;
                int tagStart = line.Text.Start + Math.Min(offsetCol, Math.Max(0, lineLen - 1));
                int tagLen = Math.Max(1, lineLen - Math.Min(offsetCol, lineLen - 1));

                built.Add((tagStart, ln, new(
                    new(document, tagStart, tagLen, TextRangeTrackingMode.ExtendForwardAndBackward),
                    new(AsmSimStateKind)
                    {
                        UniqueIdentifier = $"simstate:{ln}",
                        Description = $"simstate:|{simLabel}",
                        DisplayBeforeCreatingCodeLenses = true,
                    })));
                AppendSig(sigByLine, ln, $"S:{simLabel}");
            }
        }

        return (built, sigByLine, lineRanges);
    }

    private static void AppendSig(Dictionary<int, string> sigByLine, int line, string fragment)
        => sigByLine[line] = sigByLine.TryGetValue(line, out var existing) ? existing + ";" + fragment : fragment;

    // ── Diagnostic logging ──────────────────────────────────────────────────────────────────────
    // Routed through the shared AsmLog under category "CodeLens". Notable, low-frequency events
    // (fetches, actual publishes) log at Info so they are visible in the default extension log; the
    // high-frequency per-request detail logs at Trace (suppressed unless the level threshold is
    // lowered via ASMDUDE_LOGLEVEL / settings). The real call site (member:line) is preserved.
    internal static void TaggerLog(string msg,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Info, "CodeLens", msg, member, line);

    internal static void TaggerLogVerbose(string msg,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Trace, "CodeLens", msg, member, line);
}
