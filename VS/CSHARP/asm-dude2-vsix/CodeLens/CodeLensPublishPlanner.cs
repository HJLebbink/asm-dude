// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Collections.Generic;
using System.Linq;

/// <summary>How a <see cref="CodeLensPublishPlanner.Plan"/> call was resolved (for the tagger's log).</summary>
internal enum PublishOutcome
{
    /// <summary>Lines were produced to (re)publish.</summary>
    Published,

    /// <summary>A VS request identical to the one just served (the echo) — suppressed to stop the storm.</summary>
    EchoSkipped,

    /// <summary>Served/refreshed but there was nothing to draw.</summary>
    NothingChanged,
}

/// <summary>The decision for one publish cycle: which lines to emit and why (the why is for logging).</summary>
internal readonly record struct PublishPlan(IReadOnlyCollection<int> Lines, PublishOutcome Outcome);

/// <summary>
/// Pure (VisualStudio.Extensibility-independent) decision logic for <see cref="AsmCodeLensTagger"/>.
///
/// <para><b>Proven VS behaviour (from the tagger log).</b> VS DROPS a CodeLens tag when its line scrolls
/// out of view, and on scroll-back re-requests it with <c>recalculateAll=true</c>. So a line must be
/// re-published whenever VS asks for it — a per-line "VS already has this content" dedup leaves the lens
/// BLANK on scroll-back (the log showed every scroll request answering "nothing to publish" while the
/// lens was visibly empty). VS ALSO re-issues the publish ECHO (an immediate identical re-request, also
/// <c>recalculateAll=true</c>); answering that re-outdates the tags and loops.</para>
///
/// <para><b>The rule.</b>
/// <list type="bullet">
///   <item><b>VS request</b> (<paramref name="requestedLines"/> != null): the "serve signature" is the
///   set of visible tagged lines + their content. If it is IDENTICAL to the one we just served, this is
///   the echo → <see cref="PublishOutcome.EchoSkipped"/>. Otherwise (a scroll moved the viewport, or
///   content changed) RE-PUBLISH every tagged line in the request — VS may have dropped them.</item>
///   <item><b>Proactive refresh</b> (sim/edit, requestedLines == null): publish only the lines whose
///   content changed, and clear the serve baseline so the next VS request re-serves.</item>
/// </list>
/// Consecutive-identical suppression kills the echo (same serve sig) without ever blanking a scroll-back
/// (which has a different viewport ⇒ different sig ⇒ served). Full re-supply on edit is <see cref="Reset"/>.</para>
///
/// <para>Because it has no VS types it is unit-tested offline by replaying the real request streams VS
/// produces — see <c>CodeLensPublishPlannerTests</c>. The tagger calls THIS code (not a copy).</para>
/// </summary>
internal sealed class CodeLensPublishPlanner
{
    // What VS was last sent per line (used only for the proactive content-diff refresh).
    private readonly Dictionary<int, string> lastSigByLine_ = new();

    // The serve signature (visible tagged lines + content) of the request we last served, to suppress the
    // immediate identical echo. Null ⇒ next request must serve.
    private string? lastServeSig_;

    /// <summary>Forget all state, forcing the next request to re-emit every visible line. Use ONLY when
    /// the existing tags are genuinely invalid — i.e. after an EDIT shifts line positions.</summary>
    public void Reset()
    {
        this.lastSigByLine_.Clear();
        this.lastServeSig_ = null;
    }

    /// <summary>Decides which lines to (re)publish, and records that decision as state.</summary>
    /// <param name="requestedLines">The lines VS asked about, or <c>null</c> for a proactive refresh.</param>
    /// <param name="currentSigByLine">Signature of every line that currently carries a tag.</param>
    /// <param name="recalculateAll">VS's flag — kept only for the tagger's log.</param>
    public PublishPlan Plan(
        IReadOnlyCollection<int>? requestedLines,
        IReadOnlyDictionary<int, string> currentSigByLine,
        bool recalculateAll = false)
    {
        // ── Proactive refresh (sim/edit): content changes only; force next request to re-serve ──────
        if (requestedLines is null)
        {
            var changed = new HashSet<int>();
            foreach (var (line, sig) in currentSigByLine)
                if (!this.lastSigByLine_.TryGetValue(line, out var old) || old != sig)
                    changed.Add(line);
            foreach (var line in this.lastSigByLine_.Keys.ToList())
                if (!currentSigByLine.ContainsKey(line))
                    changed.Add(line); // a tag disappeared

            foreach (var (line, sig) in currentSigByLine) this.lastSigByLine_[line] = sig;
            foreach (var line in this.lastSigByLine_.Keys.ToList())
                if (!currentSigByLine.ContainsKey(line)) this.lastSigByLine_.Remove(line);

            this.lastServeSig_ = null; // content moved ⇒ the next VS request must re-serve
            return new PublishPlan(changed,
                changed.Count == 0 ? PublishOutcome.NothingChanged : PublishOutcome.Published);
        }

        // ── VS request: re-publish all tags it asks for, unless it is the immediate identical echo ───
        var requested = new HashSet<int>(requestedLines);
        var taggedInReq = new List<int>();
        foreach (int line in requested)
            if (currentSigByLine.ContainsKey(line)) taggedInReq.Add(line);

        string serveSig = BuildServeSignature(taggedInReq, currentSigByLine);
        if (serveSig == this.lastServeSig_)
            return new PublishPlan([], PublishOutcome.EchoSkipped);

        this.lastServeSig_ = serveSig;
        foreach (int line in taggedInReq) this.lastSigByLine_[line] = currentSigByLine[line];

        // Outdate any lens this request covers whose tag is gone.
        var toPublish = new HashSet<int>(taggedInReq);
        foreach (var line in this.lastSigByLine_.Keys.ToList())
            if (!currentSigByLine.ContainsKey(line) && requested.Contains(line))
                toPublish.Add(line);

        return new PublishPlan(toPublish,
            toPublish.Count == 0 ? PublishOutcome.NothingChanged : PublishOutcome.Published);
    }

    /// <summary>Order-independent join of each visible tagged line + its sig. A request and its echo (or
    /// differently-partitioned ranges over the same visible tags) share a signature; a viewport or content
    /// change differs.</summary>
    internal static string BuildServeSignature(IReadOnlyCollection<int> taggedLines, IReadOnlyDictionary<int, string> currentSigByLine)
    {
        var parts = new List<string>(taggedLines.Count);
        foreach (int line in taggedLines.OrderBy(x => x))
            parts.Add($"{line}={currentSigByLine[line]}");
        return string.Join("|", parts);
    }
}
