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

namespace unit_tests_asm_tools
{
    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Test_ArchProfile
    {
        [TestMethod]
        public void TryGetProfileArchs_CumulativeLevels()
        {
            Assert.IsTrue(ArchTools.TryGetProfileArchs(ArchProfileKeys.V1, out var v1));
            Assert.IsTrue(v1.Contains(Arch.ARCH_SSE2), "v1 includes SSE2");
            Assert.IsFalse(v1.Contains(Arch.ARCH_AVX), "v1 excludes AVX");

            Assert.IsTrue(ArchTools.TryGetProfileArchs(ArchProfileKeys.V3, out var v3));
            Assert.IsTrue(v3.Contains(Arch.ARCH_AVX2), "v3 includes AVX2");
            Assert.IsTrue(v3.Contains(Arch.ARCH_SSE2), "v3 is cumulative over v1");
            Assert.IsFalse(v3.Contains(Arch.ARCH_AVX512_F), "v3 excludes AVX-512");

            Assert.IsTrue(ArchTools.TryGetProfileArchs(ArchProfileKeys.V4, out var v4));
            Assert.IsTrue(v4.Contains(Arch.ARCH_AVX512_F), "v4 includes AVX-512 Foundation");
            Assert.IsTrue(v4.Contains(Arch.ARCH_AVX2), "v4 is cumulative over v3");
        }

        [TestMethod]
        public void TryGetProfileArchs_LatestExcludesLegacyButKeepsModern()
        {
            Assert.IsTrue(ArchTools.TryGetProfileArchs(ArchProfileKeys.Latest, out var latest));
            Assert.IsTrue(latest.Contains(Arch.ARCH_AMX), "Latest keeps modern ISA (AMX)");
            Assert.IsTrue(latest.Contains(Arch.ARCH_KEYLOCKER), "Latest keeps Key Locker");
            Assert.IsFalse(latest.Contains(Arch.ARCH_CYRIX), "Latest drops vendor-legacy (Cyrix)");
            Assert.IsFalse(latest.Contains(Arch.ARCH_3DNOW), "Latest drops deprecated (3DNow!)");

            Assert.IsTrue(ArchTools.TryGetProfileArchs(ArchProfileKeys.Everything, out var everything));
            Assert.IsTrue(everything.Contains(Arch.ARCH_CYRIX), "Everything includes even legacy arches");
        }

        [TestMethod]
        public void TryGetProfileArchs_CustomReturnsFalse()
        {
            // Custom (and unknown keys) signal "fall back to the individual toggles".
            Assert.IsFalse(ArchTools.TryGetProfileArchs(ArchProfileKeys.Custom, out _));
            Assert.IsFalse(ArchTools.TryGetProfileArchs("bogus", out _));
            Assert.IsFalse(ArchTools.TryGetProfileArchs(null, out _));
        }

        [TestMethod]
        public void IsArchSwitchedOn_ProfileOverridesIndividualToggles()
        {
            // Profile V1 with the OPPOSITE individual toggles set: the profile must win in both directions.
            var options = new AsmLanguageServerOptions
            {
                ArchProfile = ArchProfileKeys.V1,
                ARCH_SSE2 = false, // would hide SSE2 under Custom, but V1 includes it
                ARCH_AVX = true,   // would show AVX under Custom, but V1 excludes it
            };

            Assert.IsTrue(options.Is_Arch_Switched_On(Arch.ARCH_SSE2), "V1 enables SSE2 even though its toggle is off");
            Assert.IsFalse(options.Is_Arch_Switched_On(Arch.ARCH_AVX), "V1 disables AVX even though its toggle is on");
            Assert.IsTrue(options.Is_Arch_Switched_On(Arch.ARCH_NONE), "ARCH_NONE is always on");
        }

        [TestMethod]
        public void IsArchSwitchedOn_CustomHonorsIndividualToggles()
        {
            var options = new AsmLanguageServerOptions
            {
                ArchProfile = ArchProfileKeys.Custom,
                ARCH_AVX = true,
                ARCH_AVX2 = false,
            };

            Assert.IsTrue(options.Is_Arch_Switched_On(Arch.ARCH_AVX), "Custom honors the AVX toggle (on)");
            Assert.IsFalse(options.Is_Arch_Switched_On(Arch.ARCH_AVX2), "Custom honors the AVX2 toggle (off)");
        }
    }
}
