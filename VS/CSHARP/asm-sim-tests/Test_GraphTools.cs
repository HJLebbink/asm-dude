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

    using QuikGraph;

    /// <summary>
    /// Topology-only tests for <see cref="GraphTools{Tag}.Get_Branch_Point"/> — the routine DynamicFlow
    /// uses to find the branch whose condition distinguishes two incoming paths at a merge. The invariant
    /// under test: when several common-ancestor branch points exist (the normal case for nested branches /
    /// loops), it returns the NEAREST (innermost) one. An outer shared branch has the same condition value
    /// on both paths, so selecting it would gate the ITE-merge on the wrong predicate.
    /// </summary>
    [TestClass]
    public class Test_GraphTools
    {
        // Tag content is irrelevant to Get_Branch_Point (it inspects only vertices / edge direction /
        // out-degree), so a bool tag keeps the fixtures minimal.
        private static BidirectionalGraph<string, TaggedEdge<string, bool>> NewGraph()
        {
            return new BidirectionalGraph<string, TaggedEdge<string, bool>>(true);
        }

        private static void AddEdge(BidirectionalGraph<string, TaggedEdge<string, bool>> g, string from, string to)
        {
            g.AddVertex(from);
            g.AddVertex(to);
            g.AddEdge(new TaggedEdge<string, bool>(from, to, false));
        }

        /// <summary>
        /// Nested diamonds. Outer branch A (A→B, A→X); the B path reaches inner branch P (P→Q, P→R). Both Q
        /// and R have {P, A} as backward branch points, so P and A are both "mutual". The branch that splits
        /// Q from R is the inner one, P — A is shared but does not distinguish them. Must return P, not A.
        /// </summary>
        [TestMethod]
        public void Test_GraphTools_NearestBranchPoint_NestedBranches()
        {
            var g = NewGraph();
            AddEdge(g, "A", "B");
            AddEdge(g, "A", "X"); // makes A an outer branch point (out-degree 2)
            AddEdge(g, "B", "D");
            AddEdge(g, "D", "P");
            AddEdge(g, "P", "Q");
            AddEdge(g, "P", "R"); // makes P the inner branch point (out-degree 2)

            string? branchPoint = GraphTools<bool>.Get_Branch_Point("Q", "R", g);

            Assert.AreEqual("P", branchPoint, "the nearest (innermost) common branch point distinguishes Q from R");
            Assert.AreNotEqual("A", branchPoint, "A is a shared ancestor but takes the SAME edge to both Q and R — using it would gate the merge on the wrong predicate");
        }

        /// <summary>
        /// Triple nesting (mutual set {P, A1, A2}). The nearest is still the innermost P regardless of how
        /// many enclosing branches are shared, so an arbitrary pick from the common-ancestor set is far more
        /// likely to be wrong here.
        /// </summary>
        [TestMethod]
        public void Test_GraphTools_NearestBranchPoint_DoubleNested()
        {
            var g = NewGraph();
            AddEdge(g, "A2", "A1");
            AddEdge(g, "A2", "Y2"); // outer-outer branch
            AddEdge(g, "A1", "B");
            AddEdge(g, "A1", "Y1"); // outer branch
            AddEdge(g, "B", "P");
            AddEdge(g, "P", "Q");
            AddEdge(g, "P", "R"); // inner branch

            string? branchPoint = GraphTools<bool>.Get_Branch_Point("Q", "R", g);

            Assert.AreEqual("P", branchPoint, "the innermost branch point is nearest, irrespective of the number of enclosing shared branches");
        }

        /// <summary>
        /// A single shared branch point (a plain diamond) must be returned as-is. Guards the count==1 path.
        /// </summary>
        [TestMethod]
        public void Test_GraphTools_SingleCommonBranchPoint()
        {
            var g = NewGraph();
            AddEdge(g, "P", "Q");
            AddEdge(g, "P", "R");

            string? branchPoint = GraphTools<bool>.Get_Branch_Point("Q", "R", g);

            Assert.AreEqual("P", branchPoint);
        }

        /// <summary>
        /// Two independent paths that never share a branch point ⇒ no mutual branch point ⇒ null (the one
        /// case that legitimately remains a warning in production). Guards the count==0 path.
        /// </summary>
        [TestMethod]
        public void Test_GraphTools_NoCommonBranchPoint_ReturnsNull()
        {
            var g = NewGraph();
            // Two disjoint branches; Q1 and Q2 share no ancestor at all.
            AddEdge(g, "P1", "Q1");
            AddEdge(g, "P1", "R1");
            AddEdge(g, "P2", "Q2");
            AddEdge(g, "P2", "R2");

            string? branchPoint = GraphTools<bool>.Get_Branch_Point("Q1", "Q2", g);

            Assert.IsNull(branchPoint);
        }
    }
}
