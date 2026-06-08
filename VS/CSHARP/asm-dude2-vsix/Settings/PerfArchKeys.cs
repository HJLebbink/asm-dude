// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2.Settings;

/// <summary>
/// Canonical string keys for the <c>perfArch</c> performance-microarchitecture dropdown. Shared between
/// the setting definition (<see cref="AsmDudeSettings.PerfArch"/>'s enum entries) and the value mapping
/// (<c>SettingsSyncService</c>), so both sides reference the same symbol — a rename is a compile error
/// instead of a silently-broken string match.
/// </summary>
internal static class PerfArchKeys
{
    internal const string Conroe = "conroe";
    internal const string Wolfdale = "wolfdale";
    internal const string Nehalem = "nehalem";
    internal const string Westmere = "westmere";
    internal const string SandyBridge = "sandybridge";
    internal const string IvyBridge = "ivybridge";
    internal const string Haswell = "haswell";
    internal const string Broadwell = "broadwell";
    internal const string Skylake = "skylake";
    internal const string SkylakeX = "skylakex";
    internal const string Kabylake = "kabylake";
    internal const string CoffeeLake = "coffeelake";
    internal const string Cannonlake = "cannonlake";
    internal const string CascadeLake = "cascadelake";
    internal const string Icelake = "icelake";
    internal const string Tigerlake = "tigerlake";
    internal const string RocketLake = "rocketlake";
    internal const string EmeraldRapids = "emeraldrapids";
    internal const string Bonnell = "bonnell";
    internal const string Airmont = "airmont";
    internal const string Goldmont = "goldmont";
    internal const string GoldmontPlus = "goldmontplus";
    internal const string Tremont = "tremont";
    internal const string Zen2 = "zen2";
    internal const string Zen3 = "zen3";
    internal const string Zen4 = "zen4";
    internal const string Zen5 = "zen5";
}
