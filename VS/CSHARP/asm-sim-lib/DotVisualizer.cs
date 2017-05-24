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
using QuickGraph.Graphviz;
using QuickGraph.Graphviz.Dot;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AsmSim
{
    public static class DotVisualizer
    {
        public static void SaveToDot(StaticFlow sFlow, DynamicFlow dFlow, string filename)
        {
            var displayGraph = new QuickGraph.AdjacencyGraph<string, TaggedEdge<string, string>>();

            foreach (var vertex in dFlow.Graph.Vertices)
            {
                displayGraph.AddVertex(vertex);
            }
            foreach (var edge in dFlow.Graph.Edges)
            {
                int lineNumber = dFlow.LineNumber(edge.Source);
                string displayInfo = sFlow.Get_Line_Str(lineNumber) + "\n" + edge.Tag.StateUpdate.ToString2();
                displayGraph.AddEdge(new TaggedEdge<string, string>(edge.Source, edge.Target, displayInfo));
            }
            DotVisualizer.Visualize(displayGraph, filename);
        }

        public static void ShowPicture(string filename)
        {
            var f = new Form();
            //f.FormBorderStyle = FormBorderStyle.None;

            var picture = new PictureBox()
            {
                ImageLocation = filename,
                SizeMode = PictureBoxSizeMode.Normal,
                Dock = DockStyle.Fill,
                Size = new Size(100, 300)
            };
            f.Controls.Add(picture);
            f.Size = picture.Size;


            f.ShowDialog();
            f.Refresh();
            f.Show();
        }

        public static void Visualize(
            this IVertexAndEdgeListGraph<string, TaggedEdge<string, string>> graph,
            string fileName,
            string dir = @"c:\temp\")
        {
            var fullFileName = Path.Combine(dir, fileName);
            var viz = new GraphvizAlgorithm<string, TaggedEdge<string, string>>(graph);

            viz.FormatVertex += VizFormatVertex;
            viz.FormatEdge += MyEdgeFormatter;
            viz.Generate(new FileDotEngine(), fullFileName);
        }

        static void MyEdgeFormatter(object sender, FormatEdgeEventArgs<string, TaggedEdge<string, string>> e)
        {
            var label = new GraphvizEdgeLabel();
            label.Value = e.Edge.Tag;
            e.EdgeFormatter.Label = label;
        }

        static void VizFormatVertex(object sender, FormatVertexEventArgs<string> e)
        {
            e.VertexFormatter.Label = e.Vertex.ToString();
        }

        public sealed class FileDotEngine : IDotEngine
        {
            public string Run(GraphvizImageType imageType, string dot, string outputFileName)
            {
                string output = outputFileName;
                File.WriteAllText(output, dot);

                // assumes dot.exe is on the path:
                var args = string.Format(@"{0} -Tjpg -O", output);
                var process = System.Diagnostics.Process.Start("dot.exe", args);
                if (true)
                {
                    process.WaitForExit();
                    DotVisualizer.ShowPicture(outputFileName + ".jpg");
                }
                return output;
            }
        }
    }
}
