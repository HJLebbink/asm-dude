// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmTools;

using System;

using AsmSourceToolsAlias = AsmTools.AsmSourceTools;

[Flags]
public enum MicroArch
{
    NONE = 0,

    // Intel big cores (chronological)
    Conroe = 1 << 0,
    Wolfdale = 1 << 1,
    Nehalem = 1 << 2,
    Westmere = 1 << 3,
    SandyBridge = 1 << 4,
    IvyBridge = 1 << 5,
    Haswell = 1 << 6,
    Broadwell = 1 << 7,
    Skylake = 1 << 8,
    SkylakeX = 1 << 9,
    Kabylake = 1 << 10,
    CoffeeLake = 1 << 11,
    Cannonlake = 1 << 12,
    CascadeLake = 1 << 13,
    Icelake = 1 << 14,
    Tigerlake = 1 << 15,
    RocketLake = 1 << 16,
    EmeraldRapids = 1 << 17,

    // Intel Atom line
    Bonnell = 1 << 18,
    Airmont = 1 << 19,
    Goldmont = 1 << 20,
    GoldmontPlus = 1 << 21,
    Tremont = 1 << 22,

    // AMD Zen
    Zen2 = 1 << 23,
    Zen3 = 1 << 24,
    Zen4 = 1 << 25,
    Zen5 = 1 << 26,
}

public static partial class AsmSourceTools
{
    public static MicroArch ParseMicroArch(string str, bool strIsCapitals)
    {
        return AsmSourceToolsAlias.ToCapitals(str, strIsCapitals) switch
        {
            "CONROE" => MicroArch.Conroe,
            "WOLFDALE" => MicroArch.Wolfdale,
            "NEHALEM" => MicroArch.Nehalem,
            "WESTMERE" => MicroArch.Westmere,
            "SANDYBRIDGE" => MicroArch.SandyBridge,
            "IVYBRIDGE" => MicroArch.IvyBridge,
            "HASWELL" => MicroArch.Haswell,
            "BROADWELL" => MicroArch.Broadwell,
            "SKYLAKE" => MicroArch.Skylake,
            "SKYLAKEX" => MicroArch.SkylakeX,
            "KABYLAKE" => MicroArch.Kabylake,
            "COFFEELAKE" => MicroArch.CoffeeLake,
            "CANNONLAKE" => MicroArch.Cannonlake,
            "CASCADELAKE" => MicroArch.CascadeLake,
            "ICELAKE" => MicroArch.Icelake,
            "TIGERLAKE" => MicroArch.Tigerlake,
            "ROCKETLAKE" => MicroArch.RocketLake,
            "EMERALDRAPIDS" => MicroArch.EmeraldRapids,
            "BONNELL" => MicroArch.Bonnell,
            "AIRMONT" => MicroArch.Airmont,
            "GOLDMONT" => MicroArch.Goldmont,
            "GOLDMONTPLUS" => MicroArch.GoldmontPlus,
            "TREMONT" => MicroArch.Tremont,
            "ZEN2" => MicroArch.Zen2,
            "ZEN3" => MicroArch.Zen3,
            "ZEN4" => MicroArch.Zen4,
            "ZEN5" => MicroArch.Zen5,
            _ => MicroArch.NONE,
        };
    }
}