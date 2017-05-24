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

using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AsmSim
{
    public static class GraphTools<ITag>
    {
        /// <summary>traverse the provided vertex backwards and return the first 
        /// 
        /// </summary>
        /// <param name="vertex"></param>
        /// <param name="graph"></param>
        /// <returns></returns>

        public static IEnumerable<(int Vertex, int Step)> Get_First_Branch_Point_Backwards(int vertex, BidirectionalGraph<int, TaggedEdge<int, ITag>> graph)
        {
            ISet<int> visited = new HashSet<int>();
            return Get_Branch_Point_Backwards_LOCAL(vertex, 0);

            #region Local Method
            IEnumerable<(int Vertex, int Step)> Get_Branch_Point_Backwards_LOCAL(int v1, int step)
            {
                if (visited.Contains(v1)) yield break;

                if (graph.OutDegree(v1) > 1)
                {
                    visited.Add(v1);
                    yield return (v1, step + 1);
                }
                else
                {
                    foreach (var edge in graph.InEdges(v1))
                    {
                        foreach (var v in Get_Branch_Point_Backwards_LOCAL(edge.Source, step + 1)) yield return v;
                    }
                }
            }
            #endregion
        }
        public static IEnumerable<(int Vertex, int Step)> Get_First_Mutual_Branch_Point_Backwards(int vertex, BidirectionalGraph<int, TaggedEdge<int, ITag>> graph)
        {
            int inDegree = graph.InDegree(vertex);
            if (inDegree < 2) yield break; // the provided vertex is not a mergePoint

            if (inDegree == 2)
            {
                int s1 = graph.InEdge(vertex, 0).Source;
                int s2 = graph.InEdge(vertex, 1).Source;

                if (s1 != s2)
                {
                    var branchPoints1 = new Dictionary<int, int>();
                    var branchPoints2 = new Dictionary<int, int>();

                    foreach (var v in Get_First_Branch_Point_Backwards(s1, graph)) branchPoints1.Add(v.Vertex, v.Step);
                    foreach (var v in Get_First_Branch_Point_Backwards(s2, graph)) branchPoints2.Add(v.Vertex, v.Step);

                    var v1 = branchPoints1.Keys;
                    var v2 = branchPoints2.Keys;

                    foreach (int mutual in v1.Intersect<int>(v2))
                    {
                        int step = Math.Max(branchPoints1[mutual], branchPoints2[mutual]);
                        yield return (mutual, step);
                    }
                }
            }
            else
            {
                Console.WriteLine("WARNING: Get_First_Mutual_Branch_Point_Backwards: multiple merge points at this the provided vertex " + vertex);
            }
        }
    }
}
