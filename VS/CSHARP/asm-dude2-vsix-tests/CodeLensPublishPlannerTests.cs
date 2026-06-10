// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2.Vsix.Tests;

using System.Collections.Generic;
using System.Linq;

using AsmDude2;

using Xunit;

/// <summary>
/// Offline replay tests for <see cref="CodeLensPublishPlanner"/> — the VS-free decision logic the
/// CodeLens tagger actually calls. These reproduce the request streams captured from a live VS
/// session (in <c>%TEMP%\asmdude-tagger.log</c>): on every scroll VS fires a burst of
/// <c>OnRequestTagsAsync(recalculateAll: true)</c> over the same lines, and each <c>UpdateTagsAsync</c>
/// we emit triggers another request. The planner's job is to publish each line once and suppress the
/// echo, so a ~40-request burst collapses to a single publish.
///
/// What this canNOT prove: that VS actually goes quiet (that's VS-side behaviour, only observable in
/// the hive). What it DOES prove deterministically: given that request stream, we emit the right,
/// minimal set of publishes — which is exactly the logic I kept getting wrong by eyeballing.
/// </summary>
public sealed class CodeLensPublishPlannerTests
{
    private const long WindowMs = 2000;

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

    [Fact]
    public void FirstRequest_PublishesEveryOnScreenTagLine()
    {
        var planner = new CodeLensPublishPlanner(WindowMs);

        var toPublish = planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        Assert.Equal(new[] { 0, 9, 10, 11 }, toPublish.OrderBy(x => x));
    }

    [Fact]
    public void RepeatedIdenticalRequests_PublishOnceThenNeverAgain()
    {
        // This is the bug: ~40 identical recalculateAll requests per scroll. Must collapse to 1 publish.
        var planner = new CodeLensPublishPlanner(WindowMs);
        long tick = 1000;

        int firstCount = planner.Plan(VisibleLines(), ScreenContent, tick).Count;

        int laterPublishes = 0;
        for (int i = 0; i < 40; i++)
        {
            tick += 5; // a few ms apart, as in the captured burst — well inside the 2s window
            laterPublishes += planner.Plan(VisibleLines(), ScreenContent, tick).Count;
        }

        Assert.Equal(4, firstCount);     // lines 0,9,10,11 published once
        Assert.Equal(0, laterPublishes); // every subsequent identical request is suppressed
    }

    [Fact]
    public void DifferentRangePartition_SameLines_StillSuppressed()
    {
        // VS varies how it groups the requested ranges between requests. After the tagger reduces
        // ranges→lines, the planner sees the SAME line set, so it must still suppress. (This is what
        // the earlier range-keyed dedupe failed to do.)
        var planner = new CodeLensPublishPlanner(WindowMs);
        long tick = 1000;

        planner.Plan(Enumerable.Range(0, 27).ToList(), ScreenContent, tick);
        tick += 10;
        var again = planner.Plan(Enumerable.Range(5, 15).ToList(), ScreenContent, tick); // different line window, same on-screen tags 9,10,11

        // Lines 9,10,11 were already published and are fresh → nothing new. (Line 0 isn't in 5..19.)
        Assert.Empty(again);
    }

    [Fact]
    public void SimRefresh_RepublishesOnlyTheChangedLine()
    {
        var planner = new CodeLensPublishPlanner(WindowMs);
        planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        // Sim advances: line 10's state changes; everything else identical. requestedLines == null
        // models the proactive sim refresh (whole document in scope).
        var updated = Sigs(
            (0, "L:0,6,3,label1"),
            (9, "S:→RAX=0x10"),
            (10, "S:→RBX=0x8"),   // changed
            (11, "S:rw:RCX"));

        var toPublish = planner.Plan(requestedLines: null, updated, nowTick: 1100);

        Assert.Equal(new[] { 10 }, toPublish.OrderBy(x => x));
    }

    [Fact]
    public void GenuineRecalculate_AfterQuiescence_RepublishesEverything()
    {
        // A real recalculateAll (theme change, doc reopen, …) arriving after the window must re-supply
        // the lenses — otherwise skipping would leave them blank (the "space not preserved" regression).
        var planner = new CodeLensPublishPlanner(WindowMs);
        planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        var toPublish = planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000 + WindowMs + 1);

        Assert.Equal(new[] { 0, 9, 10, 11 }, toPublish.OrderBy(x => x));
    }

    [Fact]
    public void LostTag_IsOutdatedSoTheStaleLensIsRemoved()
    {
        var planner = new CodeLensPublishPlanner(WindowMs);
        planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        // Line 11 no longer has a sim state.
        var fewer = Sigs(
            (0, "L:0,6,3,label1"),
            (9, "S:→RAX=0x10"),
            (10, "S:→RBX=0x4"));

        var toPublish = planner.Plan(requestedLines: null, fewer, nowTick: 1100);

        Assert.Equal(new[] { 11 }, toPublish.OrderBy(x => x));
    }

    [Fact]
    public void RequestForOffscreenRegion_DoesNotPublishOnscreenTagsAgain()
    {
        var planner = new CodeLensPublishPlanner(WindowMs);
        planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        // VS asks about lines 100..120 (scrolled far away) — none of our tags live there.
        var toPublish = planner.Plan(Enumerable.Range(100, 20).ToList(), ScreenContent, nowTick: 1100);

        Assert.Empty(toPublish);
    }

    [Fact]
    public void ResetForcesRepublish()
    {
        var planner = new CodeLensPublishPlanner(WindowMs);
        planner.Plan(VisibleLines(), ScreenContent, nowTick: 1000);

        planner.Reset(); // e.g. after an edit

        var toPublish = planner.Plan(VisibleLines(), ScreenContent, nowTick: 1100);
        Assert.Equal(new[] { 0, 9, 10, 11 }, toPublish.OrderBy(x => x));
    }
}
