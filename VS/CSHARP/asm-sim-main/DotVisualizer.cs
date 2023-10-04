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

namespace AsmSim
{
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using QuikGraph;
    using QuikGraph.Graphviz;
    using QuikGraph.Graphviz.Dot;

    public static class DotVisualizer
    {
        public static void SaveToDot(StaticFlow sFlow, DynamicFlow dFlow, string filename)
        {
            Contract.Requires(sFlow != null);
            Contract.Requires(dFlow != null);

            AdjacencyGraph<string, TaggedEdge<string, string>> displayGraph = new();

            foreach (string vertex in dFlow.Graph.Vertices)
            {
                displayGraph.AddVertex(vertex);
            }
            foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> edge in dFlow.Graph.Edges)
            {
                int lineNumber = dFlow.LineNumber(edge.Source);
                string displayInfo = sFlow.Get_Line_Str(lineNumber) + "\n" + edge.Tag.stateUpdate.ToString2();
                displayGraph.AddEdge(new TaggedEdge<string, string>(edge.Source, edge.Target, displayInfo));
            }
            Visualize(displayGraph, filename);
        }

        public static void Visualize(
            this IVertexAndEdgeListGraph<string, TaggedEdge<string, string>> graph,
            string fileName,
            string dir = @"C:\Temp\AsmSim")
        {
            string fullFileName = Path.Combine(dir, fileName);
            GraphvizAlgorithm<string, TaggedEdge<string, string>> viz = new(graph);

            viz.FormatVertex += VizFormatVertex;
            viz.FormatEdge += MyEdgeFormatter;
            viz.Generate(new FileDotEngine(), fullFileName);
        }

        private static void MyEdgeFormatter(object sender, FormatEdgeEventArgs<string, TaggedEdge<string, string>> e)
        {
            GraphvizEdgeLabel label = new()
            {
                Value = e.Edge.Tag,
            };
            e.EdgeFormat.Label = label;
        }

        private static void VizFormatVertex(object sender, FormatVertexEventArgs<string> e)
        {
            Contract.Requires(e != null);
            e.VertexFormat.Label = e.Vertex.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class FileDotEngine : IDotEngine
        {
            public string Run(GraphvizImageType imageType, string dot, string outputFileName)
            {
                string output = outputFileName;
                File.WriteAllText(output, dot);

                if (true)
                {
                    // assumes dot.exe is on the path:
                    string args = string.Format(CultureInfo.InvariantCulture, @"{0} -Tjpg -O", output);
                    System.Diagnostics.Process process = System.Diagnostics.Process.Start("dot.exe", args);
                    if (true)
                    {
                        process.WaitForExit();
                    }
                }
                return output;
            }
        }
    }
}
