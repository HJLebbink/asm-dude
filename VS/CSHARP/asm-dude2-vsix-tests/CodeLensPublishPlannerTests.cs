// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2.Vsix.Tests;

using System.Collections.Generic;
using System.Linq;

using AsmDude2;

using Xunit;

/// <summary>
/// Offline replay tests for <see cref="CodeLensPublishPlanner"/> — the VS-free decision logic the
/// CodeLens tagger actually calls. PROVEN from the log: VS DROPS a CodeLens tag when its line scrolls off
/// and re-requests it on scroll-back, so a request must re-publish the tags it asks for (answering
/// "nothing" left the lens blank). VS also re-issues the immediate identical ECHO, which must be
/// suppressed or it storms. The rule: a request is served by re-publishing its visible tags UNLESS its
/// serve signature (visible tags + content) equals the one just served (the echo). A scroll-back differs
/// from the just-served viewport ⇒ served; an echo matches ⇒ skipped. A proactive sim/edit refresh
/// publishes only content changes and forces the next request to re-serve.
///
/// What this canNOT prove: VS's exact request stream (only observable in the hive). What it DOES prove
/// deterministically: a scroll-back re-publishes (not blank), the immediate echo is suppressed (no storm),
/// and a sim push emits exactly the changed line.
/// </summary>
public sealed class CodeLensPublishPlannerTests
{
    // A representative on-screen tag layout: one label lens + three sim-state lenses.
    private static Dictionary<int, string> Sigs(params (int line, string sig)[] entries)
        => entries.ToDictionary(e => e.line, e => e.sig);

    private static readonly Dictionary<int, string> ScreenContent = Sigs(
        (0, "L:0,6,3,label1"),
        (9, "S:→RAX=0x10"),
        (10, "S:→RBX=0x4"),
        (11, "S:rw:RCX"));

    // The lines VS asks about when those tags are on screen (it requests far more lines than carry a
    // tag; the planner only ever acts on lines that have/had tags).
    private static IReadOnlyCollection<int> VisibleLines()
        => Enumerable.Range(0, 27).ToList();

    private static int[] Ordered(PublishPlan p) => p.Lines.OrderBy(x => x).ToArray();

    [Fact]
    public void FirstRequest_PublishesEveryVisibleTagLine()
    {
        var planner = new CodeLensPublishPlanner();

        // No prior serve ⇒ every visible tagged line is published.
        var plan = planner.Plan(VisibleLines(), ScreenContent);

        Assert.Equal(PublishOutcome.Published, plan.Outcome);
        Assert.Equal(new[] { 0, 9, 10, 11 }, Ordered(plan));
    }

    [Fact]
    public void ImmediateIdenticalRequest_IsEchoSkipped()
    {
        // The publish ECHO: VS re-asks for the identical viewport right after we served it ⇒ same serve
        // signature ⇒ suppressed, so the loop converges.
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent, recalculateAll: true);

        var echo = planner.Plan(VisibleLines(), ScreenContent, recalculateAll: true);

        Assert.Equal(PublishOutcome.EchoSkipped, echo.Outcome);
        Assert.Empty(echo.Lines);
    }

    [Fact]
    public void RepeatedIdenticalRequests_ServeOnceThenAllEchoSkipped()
    {
        var planner = new CodeLensPublishPlanner();
        var first = planner.Plan(VisibleLines(), ScreenContent, recalculateAll: true);

        int laterPublishes = 0;
        for (int i = 0; i < 40; i++)
            laterPublishes += planner.Plan(VisibleLines(), ScreenContent, recalculateAll: true).Lines.Count;

        Assert.Equal(new[] { 0, 9, 10, 11 }, Ordered(first));
        Assert.Equal(0, laterPublishes);
    }

    [Fact]
    public void ScrollBack_RepublishesTags_BecauseVsDroppedThem()
    {
        // PROVEN from the log: VS drops a tag when it scrolls off and re-requests it on scroll-back. So a
        // scroll-back (different viewport than the one just served) MUST re-publish — NOT answer "nothing",
        // which left the lens blank.
        var planner = new CodeLensPublishPlanner();

        planner.Plan(VisibleLines(), ScreenContent);                       // viewport A (tags) served
        planner.Plan(Enumerable.Range(100, 20).ToList(), ScreenContent);   // scrolled away (different sig)
        var back = planner.Plan(VisibleLines(), ScreenContent);            // back to A

        Assert.Equal(PublishOutcome.Published, back.Outcome);
        Assert.Equal(new[] { 0, 9, 10, 11 }, Ordered(back)); // restored, not blank
    }

    [Fact]
    public void DifferentRangePartition_SameVisibleTags_IsEchoSkipped()
    {
        // VS varies how it partitions the requested ranges. Two requests covering the SAME visible tags
        // produce the same serve signature ⇒ the second is the echo ⇒ suppressed (no re-churn).
        var planner = new CodeLensPublishPlanner();
        planner.Plan(Enumerable.Range(0, 27).ToList(), ScreenContent);   // covers tags 0,9,10,11

        var again = planner.Plan(Enumerable.Range(0, 20).ToList(), ScreenContent); // also covers 0,9,10,11

        Assert.Equal(PublishOutcome.EchoSkipped, again.Outcome);
    }

    [Fact]
    public void ContentChange_OnSameViewport_IsServed_NotEcho()
    {
        // A sim push changed a visible line's content ⇒ different serve signature ⇒ not the echo ⇒ served.
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);

        var changed = Sigs(
            (0, "L:0,6,3,label1"),
            (9, "S:→RAX=0x99"),   // changed
            (10, "S:→RBX=0x4"),
            (11, "S:rw:RCX"));

        var plan = planner.Plan(VisibleLines(), changed);

        Assert.NotEqual(PublishOutcome.EchoSkipped, plan.Outcome);
        Assert.Contains(9, plan.Lines);
    }

    [Fact]
    public void SimRefresh_RepublishesOnlyTheChangedLine()
    {
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);

        // Sim advances: line 10's state changes. requestedLines == null models the proactive sim refresh.
        var updated = Sigs(
            (0, "L:0,6,3,label1"),
            (9, "S:→RAX=0x10"),
            (10, "S:→RBX=0x8"),   // changed
            (11, "S:rw:RCX"));

        var plan = planner.Plan(requestedLines: null, updated);

        Assert.Equal(new[] { 10 }, Ordered(plan));
    }

    [Fact]
    public void SimRefresh_ForcesNextRequestToReServe()
    {
        // A proactive refresh clears the serve baseline, so the next VS request re-serves the viewport
        // (VS may have dropped tags meanwhile). The echo AFTER that re-serve is then suppressed.
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);          // served
        planner.Plan(requestedLines: null, ScreenContent);    // proactive refresh (clears serve baseline)

        var served = planner.Plan(VisibleLines(), ScreenContent);
        var echo = planner.Plan(VisibleLines(), ScreenContent);

        Assert.Equal(new[] { 0, 9, 10, 11 }, Ordered(served)); // re-served after the refresh
        Assert.Equal(PublishOutcome.EchoSkipped, echo.Outcome); // its echo is suppressed
    }

    [Fact]
    public void LostTag_IsOutdatedSoTheStaleLensIsRemoved()
    {
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);

        // Line 11 no longer has a sim state.
        var fewer = Sigs(
            (0, "L:0,6,3,label1"),
            (9, "S:→RAX=0x10"),
            (10, "S:→RBX=0x4"));

        var plan = planner.Plan(requestedLines: null, fewer);

        Assert.Equal(new[] { 11 }, Ordered(plan));
    }

    [Fact]
    public void RequestForOffscreenRegion_PublishesNothing()
    {
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);

        // VS asks about lines 100..120 (scrolled far away) — none of our tags live there.
        var plan = planner.Plan(Enumerable.Range(100, 20).ToList(), ScreenContent);

        Assert.Empty(plan.Lines);
    }

    [Fact]
    public void ResetForcesRepublish()
    {
        var planner = new CodeLensPublishPlanner();
        planner.Plan(VisibleLines(), ScreenContent);

        planner.Reset(); // ONLY on edit (line positions shifted) — NOT on a sim refresh

        // Reset clears the per-line baseline, so the next request re-emits every visible line.
        var plan = planner.Plan(VisibleLines(), ScreenContent);
        Assert.Equal(new[] { 0, 9, 10, 11 }, Ordered(plan));
    }
}
