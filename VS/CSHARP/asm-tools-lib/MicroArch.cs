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

namespace AsmTools
{
    using System;

    [Flags]
    public enum MicroArch
    {
        NONE = 0,
        SandyBridge = 1 << 0,
        IvyBridge = 1 << 1,
        Haswell = 1 << 2,
        Broadwell = 1 << 3,
        Skylake = 1 << 4,
        SkylakeX = 1 << 5,
        Kabylake = 1 << 6,
        Cannonlake = 1 << 7,
        Icelake = 1 << 8,
        Tigerlake = 1 << 9,

        KnightsCorner = 1 << 10,
        KnightsLanding = 1 << 11,
    }

    public static partial class AsmSourceTools
    {
        public static MicroArch ParseMicroArch(string str, bool strIsCapitals)
        {
            ArgumentNullException.ThrowIfNull(str);

            return ToCapitals(str, strIsCapitals) switch
            {
                "SANDYBRIDGE" => MicroArch.SandyBridge,
                "IVYBRIDGE" => MicroArch.IvyBridge,
                "HASWELL" => MicroArch.Haswell,
                "BROADWELL" => MicroArch.Broadwell,
                "SKYLAKE" => MicroArch.Skylake,
                "SKYLAKEX" => MicroArch.SkylakeX,
                "KABYLAKE" => MicroArch.Kabylake,
                "CANNONLAKE" => MicroArch.Cannonlake,
                "ICELAKE" => MicroArch.Icelake,
                "TIGERLAKE" => MicroArch.Tigerlake,
                "KNIGHTSCORNER" => MicroArch.KnightsCorner,
                "KNIGHTSLANDING" => MicroArch.KnightsLanding,
                _ => MicroArch.NONE,
            };
        }
    }
}