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
    using System;
    using System.Linq;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Guards the register data that drives the editor's "highlight the whole register family" feature
    /// (LanguageServer.GetDocumentHighlights): ParseRn must recognise a register name, and
    /// GetRelatedRegisterNew must return every width-alias of the same physical register. A break here
    /// silently disables register highlighting.
    /// </summary>
    [TestClass]
    public class Test_RegisterTools
    {
        [TestMethod]
        public void ParseRn_RecognisesRegisters_CaseInsensitive()
        {
            Assert.AreEqual(Rn.AL, RegisterTools.ParseRn("al"));
            Assert.AreEqual(Rn.AL, RegisterTools.ParseRn("AL"));
            Assert.AreEqual(Rn.RAX, RegisterTools.ParseRn("rax"));
            Assert.AreEqual(Rn.R8B, RegisterTools.ParseRn("r8b"));
            Assert.AreEqual(Rn.NOREG, RegisterTools.ParseRn("notareg"));
            Assert.AreEqual(Rn.NOREG, RegisterTools.ParseRn(""));
        }

        [TestMethod]
        public void GetRelatedRegisterNew_SelectingAnyWidth_ReturnsTheWholeFamily()
        {
            // The reported bug was that selecting AL did not highlight RAX. Every alias of a physical
            // register must map to the SAME complete family, regardless of which width you select.
            string[] expectedA = ["RAX", "EAX", "AX", "AH", "AL"];
            foreach (Rn r in new[] { Rn.RAX, Rn.EAX, Rn.AX, Rn.AH, Rn.AL })
            {
                CollectionAssert.AreEquivalent(
                    expectedA,
                    RegisterTools.GetRelatedRegisterNew(r),
                    $"family of {r} must be the full RAX/EAX/AX/AH/AL set");
            }
        }

        [TestMethod]
        public void GetRelatedRegisterNew_CoversNewAndVectorRegisters()
        {
            CollectionAssert.AreEquivalent(
                new[] { "R8D", "R8W", "R8B", "R8" },
                RegisterTools.GetRelatedRegisterNew(Rn.R8B),
                "extended GPR family (selecting the byte view R8B)");

            CollectionAssert.AreEquivalent(
                new[] { "XMM3", "YMM3", "ZMM3" },
                RegisterTools.GetRelatedRegisterNew(Rn.YMM3),
                "SIMD family (selecting YMM3 highlights XMM3/ZMM3 too)");
        }

        [TestMethod]
        public void GetRelatedRegisterNew_EveryParseableRegister_HasASelfContainingFamily()
        {
            // Invariant: for any real register, GetRelatedRegisterNew returns a non-"UNKNOWN" family that
            // INCLUDES the register's own name. Catches a future enum addition that forgets a cache entry.
            foreach (Rn r in Enum.GetValues<Rn>())
            {
                if (r == Rn.NOREG)
                {
                    continue;
                }

                string[] family = RegisterTools.GetRelatedRegisterNew(r);
                Assert.IsFalse(family.Length == 1 && family[0] == "UNKNOWN", $"{r} has no related-register family");
                Assert.IsTrue(family.Contains(r.ToString(), StringComparer.OrdinalIgnoreCase), $"{r}'s family must contain {r} itself");
            }
        }
    }
}
