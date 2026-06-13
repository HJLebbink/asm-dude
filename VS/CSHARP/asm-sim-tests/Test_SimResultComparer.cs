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
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace unit_tests_asm_z3
{
    using AsmSim;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Unit tests for the standalone differential oracle <see cref="SimResultComparer"/>. Hand-built
    /// result sets only (no Z3), so they isolate the comparator's logic and run instantly.
    /// </summary>
    [TestClass]
    public class Test_SimResultComparer
    {
        private static SimResultSet Set(string label, params (int line, SimLineResult result)[] lines)
        {
            var dict = new Dictionary<int, SimLineResult>();
            foreach ((int line, SimLineResult result) in lines) dict[line] = result;
            return new SimResultSet(label, dict);
        }

        private static SimLineResult Line(string? before = null, string? after = null, string? read = null, string? write = null, IReadOnlyList<string>? diag = null)
            => new(before, after, read, write, diag);

        [TestMethod]
        public void Identical_Sets_ProduceNoDiff()
        {
            SimResultSet a = Set("A", (0, Line(after: "RAX=1")), (1, Line(after: "RBX=2", read: "r:RAX")));
            SimResultSet b = Set("B", (0, Line(after: "RAX=1")), (1, Line(after: "RBX=2", read: "r:RAX")));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.IsTrue(diff.IsEmpty, diff.ToReport());
            Assert.AreEqual(0, diff.Entries.Count);
            StringAssert.Contains(diff.ToReport(), "identical");
        }

        [TestMethod]
        public void SingleFieldDifference_IsReportedWithFieldAndValues()
        {
            SimResultSet a = Set("A", (3, Line(after: "RAX=0x1")));
            SimResultSet b = Set("B", (3, Line(after: "RAX=0x2")));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.IsFalse(diff.IsEmpty);
            Assert.AreEqual(1, diff.Entries.Count);
            SimDiffEntry e = diff.Entries[0];
            Assert.AreEqual(3, e.Line);
            Assert.AreEqual(SimDiffKind.FieldDiffers, e.Kind);
            Assert.AreEqual("after", e.Field);
            Assert.AreEqual("RAX=0x1", e.ValueA);
            Assert.AreEqual("RAX=0x2", e.ValueB);
        }

        [TestMethod]
        public void NullAndEmptyString_AreTreatedAsEqual()
        {
            // A producer may omit a field (null) where another emits ""; that is not a real difference.
            SimResultSet a = Set("A", (0, Line(read: null)));
            SimResultSet b = Set("B", (0, Line(read: "")));

            Assert.IsTrue(SimResultComparer.Compare(a, b).IsEmpty);
        }

        [TestMethod]
        public void NullVersusValue_IsADifference()
        {
            SimResultSet a = Set("A", (0, Line(write: null)));
            SimResultSet b = Set("B", (0, Line(write: "w:RAX")));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.AreEqual(1, diff.Entries.Count);
            Assert.AreEqual("write", diff.Entries[0].Field);
            Assert.IsNull(diff.Entries[0].ValueA);
            Assert.AreEqual("w:RAX", diff.Entries[0].ValueB);
        }

        [TestMethod]
        public void LinePresentInOnlyOneSet_IsReported()
        {
            SimResultSet a = Set("A", (0, Line(after: "x")), (1, Line(after: "y")));
            SimResultSet b = Set("B", (0, Line(after: "x")));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.AreEqual(1, diff.Entries.Count);
            Assert.AreEqual(1, diff.Entries[0].Line);
            Assert.AreEqual(SimDiffKind.LineOnlyInA, diff.Entries[0].Kind);

            // And symmetrically the other direction.
            SimDiff reverse = SimResultComparer.Compare(b, a);
            Assert.AreEqual(SimDiffKind.LineOnlyInB, reverse.Entries.Single().Kind);
        }

        [TestMethod]
        public void Diagnostics_AreComparedOrderIndependently()
        {
            SimResultSet a = Set("A", (0, Line(diag: new[] { "Unreachable", "UsageUndefined" })));
            SimResultSet b = Set("B", (0, Line(diag: new[] { "UsageUndefined", "Unreachable" })));

            Assert.IsTrue(SimResultComparer.Compare(a, b).IsEmpty, "diagnostic ordering is not semantically meaningful");
        }

        [TestMethod]
        public void Diagnostics_DifferentSets_AreReported()
        {
            SimResultSet a = Set("A", (0, Line(diag: new[] { "Unreachable" })));
            SimResultSet b = Set("B", (0, Line(diag: new[] { "Unreachable", "SyntaxError" })));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.AreEqual(1, diff.Entries.Count);
            Assert.AreEqual("diag", diff.Entries[0].Field);
        }

        [TestMethod]
        public void MultipleDifferences_AreAllReported_AndChangedLinesDeduped()
        {
            SimResultSet a = Set("A", (0, Line(before: "b0", after: "a0")), (2, Line(after: "a2")));
            SimResultSet b = Set("B", (0, Line(before: "B0", after: "A0")), (2, Line(after: "a2")));

            SimDiff diff = SimResultComparer.Compare(a, b);

            Assert.AreEqual(2, diff.Entries.Count, "line 0 differs in both before and after");
            Assert.AreEqual(1, diff.ChangedLines.Count, "both differences are on line 0");
            Assert.IsTrue(diff.ChangedLines.Contains(0));
        }
    }
}
