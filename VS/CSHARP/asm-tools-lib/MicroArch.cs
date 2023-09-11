// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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
    using System.Diagnostics.Contracts;

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
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            switch (ToCapitals(str, strIsCapitals))
            {
                case "SANDYBRIDGE": return MicroArch.SandyBridge;
                case "IVYBRIDGE": return MicroArch.IvyBridge;
                case "HASWELL": return MicroArch.Haswell;
                case "BROADWELL": return MicroArch.Broadwell;
                case "SKYLAKE": return MicroArch.Skylake;
                case "SKYLAKEX": return MicroArch.SkylakeX;
                case "KABYLAKE": return MicroArch.Kabylake;
                case "CANNONLAKE": return MicroArch.Cannonlake;
                case "ICELAKE": return MicroArch.Icelake;
                case "TIGERLAKE": return MicroArch.Tigerlake;
                case "KNIGHTSCORNER": return MicroArch.KnightsCorner;
                case "KNIGHTSLANDING": return MicroArch.KnightsLanding;
                default: return MicroArch.NONE;
            }
        }
    }
}