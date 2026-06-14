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

namespace AsmSim
{
    using AsmTools;

    using QuikGraph;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class GraphTools<Tag>
    {
        public static IEnumerable<string> Get_Branch_Points_Backwards(string vertex, BidirectionalGraph<string, TaggedEdge<string, Tag>> graph)
        {
            ArgumentNullException.ThrowIfNull(graph);

            HashSet<string> visited = [];
            return Get_Branch_Points_Backwards_LOCAL(vertex);

            #region Local Method
            IEnumerable<string> Get_Branch_Points_Backwards_LOCAL(string v1)
            {
                if (visited.Contains(v1))
                {
                    yield break;
                }

                if (!graph.ContainsVertex(v1))
                {
                    yield break;
                }

                if (graph.OutDegree(v1) > 1)
                {
                    visited.Add(v1);
                    yield return v1;
                }

                foreach (TaggedEdge<string, Tag> edge in graph.InEdges(v1))
                {
                    foreach (string v in Get_Branch_Points_Backwards_LOCAL(edge.Source))
                    {
                        yield return v;
                    }
                }
            }
            #endregion
        }

        /// <summary>traverse the provided vertex backwards and return the first</summary>
        public static IEnumerable<string> Get_First_Branch_Point_Backwards(string vertex, BidirectionalGraph<string, TaggedEdge<string, Tag>> graph)
        {
            ArgumentNullException.ThrowIfNull(graph);

            HashSet<string> visited = [];
            return Get_Branch_Point_Backwards_LOCAL(vertex);

            #region Local Method
            IEnumerable<string> Get_Branch_Point_Backwards_LOCAL(string v1)
            {
                if (visited.Contains(v1))
                {
                    yield break;
                }

                if (!graph.ContainsVertex(v1))
                {
                    yield break;
                }

                if (graph.OutDegree(v1) > 1)
                {
                    visited.Add(v1);
                    yield return v1;
                }
                else
                {
                    foreach (TaggedEdge<string, Tag> edge in graph.InEdges(v1))
                    {
                        foreach (string v in Get_Branch_Point_Backwards_LOCAL(edge.Source))
                        {
                            yield return v;
                        }
                    }
                }
            }
            #endregion
        }

        public static IEnumerable<string> Get_First_Mutual_Branch_Point_Backwards(string vertex, BidirectionalGraph<string, TaggedEdge<string, Tag>> graph)
        {
            ArgumentNullException.ThrowIfNull(graph);

            if (!graph.ContainsVertex(vertex))
            {
                yield break;
            }

            int inDegree = graph.InDegree(vertex);
            if (inDegree < 2)
            {
                yield break; // the provided vertex is not a mergePoint
            }

            if (inDegree == 2)
            {
                string s1 = graph.InEdge(vertex, 0).Source;
                string s2 = graph.InEdge(vertex, 1).Source;

                if (s1 != s2)
                {
                    HashSet<string> branchPoints1 = [];
                    HashSet<string> branchPoints2 = [];

                    foreach (string v in Get_First_Branch_Point_Backwards(s1, graph))
                    {
                        branchPoints1.Add(v);
                    }

                    foreach (string v in Get_First_Branch_Point_Backwards(s2, graph))
                    {
                        branchPoints2.Add(v);
                    }

                    foreach (string mutual in branchPoints1.Intersect(branchPoints2))
                    {
                        yield return mutual;
                    }
                }
            }
            else
            {
                AsmLog.Warn("SIM", "Get_First_Mutual_Branch_Point_Backwards: multiple merge points at this the provided vertex " + vertex);
            }
        }

        public static string? Get_Branch_Point(string vertex1, string vertex2, BidirectionalGraph<string, TaggedEdge<string, Tag>> graph)
        {
            if (vertex1 == vertex2)
            {
                AsmLog.Info("SIM", "GraphTools:Get_First_Branch_Point: vertex1=vertex2=" + vertex1);
                return vertex1;
            }

            HashSet<string> mutual = new(Get_Branch_Points_Backwards(vertex1, graph));
            mutual.IntersectWith(new HashSet<string>(Get_Branch_Points_Backwards(vertex2, graph)));

            if (mutual.Count == 0)
            {
                AsmLog.Warn("SIM", "GraphTools:Get_Branch_Point: no mutual branch point found between " + vertex1 + " and " + vertex2);
                return null;
            }
            if (mutual.Count == 1)
            {
                return mutual.First();
            }

            // Multiple common branch points is the NORMAL case for nested branches / loops: every enclosing
            // branch is a shared ancestor of both incoming paths. The branch that actually distinguishes the
            // two paths is the NEAREST one (the innermost / latest divergence); an outer branch's condition is
            // identical on both paths, so selecting it would gate the ITE-merge on the wrong predicate. Pick
            // the nearest deterministically with a backward BFS from vertex1 — the first common branch point
            // reached. (The old code returned an arbitrary HashSet element and warned on every such case.)
            HashSet<string> visited = [vertex1];
            Queue<string> queue = new();
            queue.Enqueue(vertex1);
            while (queue.Count > 0)
            {
                string v = queue.Dequeue();
                if (mutual.Contains(v))
                {
                    return v;
                }

                if (graph.ContainsVertex(v))
                {
                    foreach (TaggedEdge<string, Tag> edge in graph.InEdges(v))
                    {
                        if (visited.Add(edge.Source))
                        {
                            queue.Enqueue(edge.Source);
                        }
                    }
                }
            }

            // Unreachable in a well-formed graph (every mutual point is a backward-ancestor of vertex1); kept
            // as a safety net so the result stays deterministic rather than throwing.
            AsmLog.Debug("SIM", "GraphTools:Get_Branch_Point: nearest mutual branch point not reachable from " + vertex1);
            return mutual.OrderBy(static s => s, StringComparer.Ordinal).First();
        }
    }
}
