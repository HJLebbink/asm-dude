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

        public static IEnumerable<(string Vertex, int Step)> Get_First_Branch_Point_Backwards(string vertex, BidirectionalGraph<string, TaggedEdge<string, ITag>> graph)
        {
            var visited = new HashSet<string>();
            return Get_Branch_Point_Backwards_LOCAL(vertex, 0);

            #region Local Method
            IEnumerable<(string Vertex, int Step)> Get_Branch_Point_Backwards_LOCAL(string v1, int step)
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
        public static IEnumerable<(string Vertex, int Step)> Get_First_Mutual_Branch_Point_Backwards(string vertex, BidirectionalGraph<string, TaggedEdge<string, ITag>> graph)
        {
            int inDegree = graph.InDegree(vertex);
            if (inDegree < 2) yield break; // the provided vertex is not a mergePoint

            if (inDegree == 2)
            {
                string s1 = graph.InEdge(vertex, 0).Source;
                string s2 = graph.InEdge(vertex, 1).Source;

                if (s1 != s2)
                {
                    var branchPoints1 = new Dictionary<string, int>();
                    var branchPoints2 = new Dictionary<string, int>();

                    foreach (var v in Get_First_Branch_Point_Backwards(s1, graph)) branchPoints1.Add(v.Vertex, v.Step);
                    foreach (var v in Get_First_Branch_Point_Backwards(s2, graph)) branchPoints2.Add(v.Vertex, v.Step);

                    var v1 = branchPoints1.Keys;
                    var v2 = branchPoints2.Keys;

                    foreach (string mutual in v1.Intersect<string>(v2))
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


        public static string Get_Branch_Point(string vertex1, string vertex2, BidirectionalGraph<string, TaggedEdge<string, ITag>> graph)
        {
            if (vertex1 == vertex2)
            {
                Console.WriteLine("WARNING: GraphTools:Get_First_Branch_Point: vertex1=vertex2=" + vertex1);
                return null;
            }

            var branchPoints1 = new HashSet<string>();
            var branchPoints2 = new HashSet<string>();

            foreach (var v in Get_First_Branch_Point_Backwards(vertex1, graph)) branchPoints1.Add(v.Vertex);
            foreach (var v in Get_First_Branch_Point_Backwards(vertex2, graph)) branchPoints2.Add(v.Vertex);

            var m = new List<string>(branchPoints1.Intersect<string>(branchPoints2));
            switch (m.Count)
            {
                case 0:
                    Console.WriteLine("WARNING: GraphTools:Get_First_Branch_Point: no mutual branch point found");
                    return null;
                case 1:
                    return m[0];
                default:
                    Console.WriteLine("WARNING: GraphTools:Get_First_Branch_Point: multiple mutual branch points found, returning first.");
                    return m[0];    
            }
        }
    }
}
