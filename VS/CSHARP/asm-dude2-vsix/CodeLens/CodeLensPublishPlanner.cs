// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure (VisualStudio.Extensibility-independent) decision logic for <see cref="AsmCodeLensTagger"/>:
/// given a tag request (or a proactive sim/edit refresh) plus the freshly-built per-line tag
/// content, decides which lines to (re)publish.
///
/// <para>Why it's a separate, VS-free class: calling <c>UpdateTagsAsync</c> makes VS immediately
/// re-issue <c>OnRequestTagsAsync(recalculateAll: true)</c> with SHIFTING range partitions; naively
/// re-publishing the same content re-raises the change and loops (measured ~40 round-trips per
/// scroll). The planner suppresses a line's republish unless its content changed, or — for a VS
/// request — our last publish of it is older than the recency window (so a genuine recalculateAll
/// after quiescence still re-supplies the lens). Because it has no VS types, it is unit-tested
/// offline by replaying the real request streams VS produces — see <c>CodeLensPublishPlannerTests</c>.
/// The tagger calls THIS code (not a copy), so those tests exercise production logic.</para>
/// </summary>
internal sealed class CodeLensPublishPlanner
{
    private readonly long republishWindowMs_;

    // Signature of each line we last told VS about, and when we last published it.
    private readonly Dictionary<int, string> lastSigByLine_ = new();
    private readonly Dictionary<int, long> lastPublishTickByLine_ = new();

    public CodeLensPublishPlanner(long republishWindowMs) => this.republishWindowMs_ = republishWindowMs;

    /// <summary>Forget all state (e.g. after an edit changes line positions/numbers).</summary>
    public void Reset()
    {
        this.lastSigByLine_.Clear();
        this.lastPublishTickByLine_.Clear();
    }

    /// <summary>
    /// Decides which lines to (re)publish, and records that decision as state.
    /// </summary>
    /// <param name="requestedLines">
    /// The lines VS asked about, or <c>null</c> for a proactive sim/edit refresh (covers every line).
    /// </param>
    /// <param name="currentSigByLine">Signature of every line that currently carries a tag.</param>
    /// <param name="nowTick">A monotonic clock in ms (e.g. <c>Environment.TickCount64</c>).</param>
    /// <returns>The set of lines to publish now (may be empty → publish nothing).</returns>
    public IReadOnlyCollection<int> Plan(
        IReadOnlyCollection<int>? requestedLines,
        IReadOnlyDictionary<int, string> currentSigByLine,
        long nowTick)
    {
        var requested = requestedLines is null ? null : new HashSet<int>(requestedLines);
        bool InScope(int line) => requested is null || requested.Contains(line);

        var toPublish = new HashSet<int>();

        // (a) Current tag lines whose content VS doesn't already have.
        foreach (var (line, sig) in currentSigByLine)
        {
            if (!InScope(line)) continue;

            bool sameSig = this.lastSigByLine_.TryGetValue(line, out var old) && old == sig;
            bool fresh = this.lastPublishTickByLine_.TryGetValue(line, out var t) && (nowTick - t) < this.republishWindowMs_;

            // Sim/edit refresh: republish whenever content changed (we always hold the prior copy).
            // VS request: republish when content changed OR our copy may have been dropped (stale).
            bool satisfied = sameSig && (requested is null || fresh);
            if (!satisfied) toPublish.Add(line);
        }

        // (b) Lines that previously had a tag but no longer do — outdate the stale lens.
        foreach (var line in this.lastSigByLine_.Keys.ToList())
            if (!currentSigByLine.ContainsKey(line) && InScope(line))
                toPublish.Add(line);

        // Update baseline to the current content (mirrors the tagger's lastPublishedByLine = sigByLine).
        this.lastSigByLine_.Clear();
        foreach (var (line, sig) in currentSigByLine)
            this.lastSigByLine_[line] = sig;

        foreach (var line in toPublish)
            this.lastPublishTickByLine_[line] = nowTick;

        return toPublish;
    }
}
