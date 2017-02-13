// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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

using System;

namespace AsmTools
{
    [Flags]
    public enum MicroArch
    {
        UNKNOWN,
        SandyBridge,
        IvyBridge,
        Haswell,
        Broadwell,
        Skylake,
        Kabylake,
        Cannonlake,
        Icelake,
        Tigerlake,

        KnightsCorner,
        KnightsLanding
    }

    public static partial class AsmSourceTools
    {
        public static MicroArch parseMicroArch(string str)
        {
            switch (str.ToUpper())
            {
                case "SANDYBRIDGE": return MicroArch.SandyBridge;
                case "IVYBRIDGE": return MicroArch.IvyBridge;
                case "HASWELL": return MicroArch.Haswell;
                case "BROADWELL": return MicroArch.Broadwell;
                case "SKYLAKE": return MicroArch.Skylake;
                case "KABYLAKE": return MicroArch.Kabylake;
                case "CANNONLAKE": return MicroArch.Cannonlake;
                case "ICELAKE": return MicroArch.Icelake;
                case "TIGERLAKE": return MicroArch.Tigerlake;
                case "KNIGHTSCORNER": return MicroArch.KnightsCorner;
                case "KNIGHTSLANDING": return MicroArch.KnightsLanding;
                default: return MicroArch.UNKNOWN;
            }
        }
    }
}