// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
// Port from Python intel-doc-2-md project to C#
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Layout.Layout;
using iText.Layout.Renderer;

namespace AsmAnnotate
{
    /// <summary>
    /// Port of intel-doc-2-md Python project to C#.
    /// Parses Intel PDF documents and extracts instruction definitions.
    ///
    /// Key Implementation Details:
    /// - Text coordinates use (x0, x1, y0, y1) bounding boxes
    /// - Y-coordinates increase upward (typical PDF coordinate system)
    /// - Sorting is done in reverse Y order (top of page first)
    /// - UTF-8 special characters are preserved (em-dash, bullets, etc.)
    /// - Text stripping removes leading/trailing whitespace but preserves internal spacing
    /// </summary>

    /// <summary>
    /// Represents a single PDF text element with coordinates and content.
    /// Corresponds to pdfminer's LTTextLineHorizontal.
    /// </summary>
    public class PdfTextElement
    {
        /// <summary>
        /// The raw text content from PDF
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Left edge X coordinate
        /// </summary>
        public double X0 { get; set; }

        /// <summary>
        /// Right edge X coordinate
        /// </summary>
        public double X1 { get; set; }

        /// <summary>
        /// Bottom edge Y coordinate
        /// </summary>
        public double Y0 { get; set; }

        /// <summary>
        /// Top edge Y coordinate
        /// </summary>
        public double Y1 { get; set; }

        /// <summary>
        /// Height of the text element (Y1 - Y0)
        /// </summary>
        public double Height => Y1 - Y0;

        /// <summary>
        /// Width of the text element (X1 - X0)
        /// </summary>
        public double Width => X1 - X0;

        /// <summary>
        /// Font name (e.g., "NeoSansIntelMedium")
        /// </summary>
        public string FontName { get; set; }

        /// <summary>
        /// Returns trimmed content
        /// </summary>
        public string GetText() => Content?.Trim() ?? string.Empty;

        public override string ToString() => GetText();
    }

    /// <summary>
    /// Represents a line (vector graphics, not text).
    /// Can be vertical or horizontal based on width/height.
    /// </summary>
    public class PdfLineElement
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }

        public double Height => Y1 - Y0;
        public double Width => X1 - X0;

        /// <summary>
        /// Vertical lines have width < 1.0
        /// </summary>
        public bool IsVertical => Math.Abs(Width) < 1.0;

        /// <summary>
        /// Horizontal lines have height < 1.0
        /// </summary>
        public bool IsHorizontal => Math.Abs(Height) < 1.0;
    }

    /// <summary>
    /// Tracks state during markdown generation from piles.
    /// Equivalent to the State class in Python's writer.py.
    /// </summary>
    public class MarkdownState
    {
        /// <summary>
        /// Whether we are currently in a code block (```...```)
        /// </summary>
        public bool CodeMode { get; set; } = false;

        /// <summary>
        /// Current section type: title, description, encoding, operation, flags, exceptions, intrinsics, etc.
        /// </summary>
        public string Type { get; set; } = null;

        /// <summary>
        /// Next section type (used for state transitions)
        /// </summary>
        public string TypeNext { get; set; } = null;

        /// <summary>
        /// Whether the previous pile was an opcode table
        /// </summary>
        public bool PreviousPileIsOpcodeTable { get; set; } = false;

        /// <summary>
        /// Whether the current pile is an opcode table
        /// </summary>
        public bool CurrentPileIsOpcodeTable { get; set; } = false;

        /// <summary>
        /// Whether the next pile is an opcode table
        /// </summary>
        public bool NextPileIsOpcodeTable { get; set; } = false;
    }

    /// <summary>
    /// Represents a logical "pile" of content - a table, paragraph, or image.
    ///
    /// Key Design:
    /// - Tables are identified by presence of vertical lines (borders)
    /// - Paragraphs are text blocks between tables
    /// - Tables are detected by checking for "Opcode" in first cell
    ///
    /// CRITICAL PDF Quirks:
    /// - PDF text extraction may have extra spaces: " " should become ""
    /// - Word breaks across lines: "instruc-\ntions" must become "instructions"
    /// - Hyphens in PDFs can be multiple types: em-dash (U+2014), en-dash (U+2013), hyphen-minus (U+002D)
    /// - Font names in PDFs are typically: "NeoSansIntelMedium", "CourierNew", etc.
    /// - Text height varies: titles > 14.5pt, normal text ~11pt, headers/footers < 10pt
    /// </summary>
    public class ContentPile
    {
        // Search distances for snapping lines to nearby lines
        private const double SEARCH_DISTANCE_VERTICAL = 1.0;
        private const double SEARCH_DISTANCE_HORIZONTAL = 8.0;

        public List<PdfLineElement> VerticalLines { get; } = [];
        public List<PdfLineElement> HorizontalLines { get; } = [];
        public List<PdfTextElement> TextElements { get; } = [];
        public List<object> Images { get; } = []; // Placeholder for image support

        /// <summary>
        /// Determines the type of this pile based on its content
        /// </summary>
        public string GetType()
        {
            if (VerticalLines.Count > 0) return "table";
            if (Images.Count > 0) return "image";
            return "paragraph";
        }

        /// <summary>
        /// Detects if this is an opcode table by checking the first cell text
        /// </summary>
        public bool IsOpcodeTable()
        {
            if (GetType() != "table") return false;
            if (TextElements.Count == 0) return false;

            string firstText = TextElements[0].GetText();
            return firstText == "Opcode";
        }

        /// <summary>
        /// Detects table boundaries using text position analysis (heuristic approach).
        /// Workaround for lack of vector line extraction in itext7.
        ///
        /// Algorithm:
        /// 1. Cluster texts by X coordinate (similar left edges = same column)
        /// 2. Cluster texts by Y coordinate (similar bottom edges = same row)
        /// 3. Generate synthetic vertical/horizontal lines from clusters
        /// 4. Let the existing FindTables() logic work with inferred lines
        ///
        /// Threshold: 2.0 points tolerance for coordinate similarity
        /// </summary>
        public void DetectTableBoundariesFromTextPositions()
        {
            const double positionTolerance = 2.0;

            if (TextElements.Count < 4) return; // Need minimum cells for a table

            // Cluster X coordinates (column detection)
            var xClusters = new List<double>();
            foreach (var text in TextElements)
            {
                bool foundCluster = false;
                foreach (var xCluster in xClusters)
                {
                    if (Math.Abs(text.X0 - xCluster) < positionTolerance)
                    {
                        foundCluster = true;
                        break;
                    }
                }
                if (!foundCluster)
                {
                    xClusters.Add(text.X0);
                }
            }
            xClusters.Sort();

            // Cluster Y coordinates (row detection)
            var yClusters = new List<double>();
            foreach (var text in TextElements)
            {
                bool foundCluster = false;
                foreach (var yCluster in yClusters)
                {
                    if (Math.Abs(text.Y0 - yCluster) < positionTolerance)
                    {
                        foundCluster = true;
                        break;
                    }
                }
                if (!foundCluster)
                {
                    yClusters.Add(text.Y0);
                }
            }
            yClusters.Sort((a, b) => b.CompareTo(a)); // Descending for PDF coords

            // Create synthetic vertical lines from X clusters
            if (xClusters.Count >= 2)
            {
                double minY = TextElements.Min(t => t.Y0);
                double maxY = TextElements.Max(t => t.Y1);

                foreach (var xCoord in xClusters)
                {
                    VerticalLines.Add(new PdfLineElement
                    {
                        X0 = xCoord,
                        X1 = xCoord,
                        Y0 = minY,
                        Y1 = maxY
                    });
                }
            }

            // Create synthetic horizontal lines from Y clusters
            if (yClusters.Count >= 2)
            {
                double minX = TextElements.Min(t => t.X0);
                double maxX = TextElements.Max(t => t.X1);

                foreach (var yCoord in yClusters)
                {
                    HorizontalLines.Add(new PdfLineElement
                    {
                        X0 = minX,
                        X1 = maxX,
                        Y0 = yCoord,
                        Y1 = yCoord
                    });
                }
            }
        }

        /// <summary>
        /// Gets the instruction mnemonic and description from this pile.
        /// Looks for text with em-dash (U+2014) or en-dash (U+2013) separator.
        /// Example: "ADD — Add packed FP values"
        ///
        /// CRITICAL: Font selection is key to avoiding false positives.
        /// Titles use "NeoSansIntelMedium" font and height > 14.5.
        /// </summary>
        public (string Mnemonic, string Description) GetInstruction()
        {
            foreach (PdfTextElement text in TextElements)
            {
                // Only titles have this specific font and height
                if (text.Height <= 14.5) continue;
                if (string.IsNullOrEmpty(text.FontName) || !text.FontName.EndsWith("NeoSansIntelMedium")) continue;

                string content = text.GetText();

                // Try em-dash (U+2014): "—"
                if (content.Contains("—"))
                {
                    string[] parts = content.Split('—');
                    if (parts.Length >= 2)
                    {
                        return (parts[0].Trim(), parts[1].Trim());
                    }
                }

                // Try en-dash (U+2013): "–"
                if (content.Contains("–"))
                {
                    string[] parts = content.Split('–');
                    if (parts.Length >= 2)
                    {
                        return (parts[0].Trim(), parts[1].Trim());
                    }
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Parses PDF layout objects and classifies them as text, lines, or images.
        /// This is the core layout parsing logic.
        ///
        /// IMPORTANT: PDF structure is hierarchical:
        /// - Pages contain TextBoxes
        /// - TextBoxes contain TextLines
        /// - TextLines contain characters and spacing
        /// This recursively unwraps the hierarchy.
        /// </summary>
        public void ParsePageLayout(List<object> layoutObjects)
        {
            // Stack-based traversal to handle nested structures
            var stack = new Stack<object>(layoutObjects.AsEnumerable().Reverse());

            while (stack.Count > 0)
            {
                object obj = stack.Pop();

                if (obj == null) continue;

                string typeName = obj.GetType().Name;

                // Recursively process container types
                if (typeName is "LTFigure" or "LTTextBox" or "LTTextLine" or "LTTextBoxHorizontal")
                {
                    // Unwrap container - push children onto stack
                    if (obj is System.Collections.IEnumerable enumerable)
                    {
                        var children = enumerable.Cast<object>().Reverse().ToList();
                        foreach (object child in children)
                        {
                            stack.Push(child);
                        }
                    }
                }
                else if (typeName == "LTTextLineHorizontal")
                {
                    // Leaf text element
                    ExtractTextElement(obj);
                }
                else if (typeName == "LTRect")
                {
                    // Line element (vertical or horizontal)
                    ExtractLineElement(obj);
                }
                else if (typeName is "LTImage" or "LTFigure")
                {
                    // Image - placeholder for now
                    // Images.Add(obj);
                }
                // LTChar, LTAnno, LTLine, LTCurve are ignored (primitives)
            }
        }

        private void ExtractTextElement(object obj)
        {
            // For itext7, obj is a PdfTextInfo object
            if (obj is not PdfTextInfo textInfo) return;

            try
            {
                var element = new PdfTextElement
                {
                    Content = textInfo.Text,
                    X0 = textInfo.X,
                    Y0 = textInfo.Y,
                    X1 = textInfo.X + textInfo.Width,
                    Y1 = textInfo.Y + textInfo.Height,
                    FontName = textInfo.FontName
                };
                if (element.Height > 0 && element.Width > 0)
                {
                    TextElements.Add(element);
                }
            }
            catch { /* Ignore extraction errors */ }
        }

        private void ExtractLineElement(object obj)
        {
            // For itext7, obj is a PdfLineInfo object
            if (obj is not PdfLineInfo lineInfo) return;

            try
            {
                var line = new PdfLineElement
                {
                    X0 = lineInfo.X1,
                    Y0 = lineInfo.Y1,
                    X1 = lineInfo.X2,
                    Y1 = lineInfo.Y2
                };

                if (line.IsVertical)
                {
                    AdjustToClose(line, VerticalLines, SEARCH_DISTANCE_VERTICAL);
                    VerticalLines.Add(line);
                }
                else if (line.IsHorizontal)
                {
                    AdjustToClose(line, HorizontalLines, SEARCH_DISTANCE_HORIZONTAL);
                    HorizontalLines.Add(line);
                }
            }
            catch { /* Ignore extraction errors */ }
        }

        /// <summary>
        /// Snaps a line to a nearby parallel line within search distance.
        /// This handles PDF rendering artifacts where lines may be slightly offset.
        ///
        /// For vertical lines: snaps to close X coordinate
        /// For horizontal lines: snaps to close Y coordinate
        /// </summary>
        private void AdjustToClose(PdfLineElement line, List<PdfLineElement> existingLines, double searchDistance)
        {
            PdfLineElement closest = null;
            double closestDistance = searchDistance;

            foreach (PdfLineElement existing in existingLines)
            {
                double distance;
                if (searchDistance == SEARCH_DISTANCE_VERTICAL)
                {
                    // Vertical line: compare X coordinates
                    distance = Math.Abs(line.X0 - existing.X0);
                }
                else
                {
                    // Horizontal line: compare Y coordinates
                    distance = Math.Abs(line.Y0 - existing.Y0);
                }

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = existing;
                }
            }

            if (closest != null)
            {
                // Snap to closest line
                if (searchDistance == SEARCH_DISTANCE_VERTICAL)
                {
                    line.X0 = closest.X0;
                    line.X1 = closest.X1;
                }
                else
                {
                    line.Y0 = closest.Y0;
                    line.Y1 = closest.Y1;
                }
            }
        }

        /// <summary>
        /// Splits this pile into logical tables and paragraphs.
        ///
        /// Algorithm:
        /// 1. Find connected vertical lines to form table columns
        /// 2. Find horizontal lines to form table rows
        /// 3. Group text into tables vs paragraphs based on table boundaries
        /// </summary>
        public List<ContentPile> SplitIntoPiles()
        {
            var piles = new List<ContentPile>();

            // Find tables by grouping near vertical lines
            var tableTextElements = new HashSet<PdfTextElement>();
            var tables = FindTables();
            foreach (var table in tables)
            {
                piles.Add(table);
                tableTextElements.UnionWith(table.TextElements);
            }

            // Find paragraphs between tables
            var paragraphs = FindParagraphs(tables);
            piles.AddRange(paragraphs);

            // Find images
            var images = FindImages();
            piles.AddRange(images);

            // Sort by Y position (top to bottom, reverse order)
            return piles.OrderByDescending(p =>
            {
                var first = p.TextElements.FirstOrDefault();
                return first?.Y0 ?? 0.0;
            }).ToList();
        }

        private List<ContentPile> FindTables()
        {
            var tables = new List<ContentPile>();
            var visited = new HashSet<PdfLineElement>();

            foreach (PdfLineElement vertical in VerticalLines)
            {
                if (visited.Contains(vertical)) continue;

                var nearVerticals = FindNearVerticals(vertical);
                var (top, bottom) = CalcTopBottom(nearVerticals);
                var includedHorizontals = FindIncluded(top, bottom, HorizontalLines);
                var includedTexts = FindIncluded(top, bottom, TextElements);

                var table = new ContentPile();
                table.VerticalLines.AddRange(nearVerticals);
                table.HorizontalLines.AddRange(includedHorizontals);
                table.TextElements.AddRange(includedTexts);

                tables.Add(table);
                foreach (var v in nearVerticals) visited.Add(v);
            }

            return tables;
        }

        private List<ContentPile> FindParagraphs(List<ContentPile> tables)
        {
            // Calculate top boundary of each table for paragraph grouping
            var tops = new List<double>();
            foreach (var table in tables)
            {
                var (top, _) = CalcTopBottom(table.VerticalLines);
                tops.Add(top);
            }

            // Add negative infinity for the last part of paragraph
            tops.Add(double.NegativeInfinity);

            // Collect all texts that belong to tables
            var allTableTexts = new HashSet<PdfTextElement>();
            foreach (var table in tables)
            {
                foreach (var text in table.TextElements)
                {
                    allTableTexts.Add(text);
                }
            }

            // Create one paragraph slot per table boundary, plus one for final section
            var numSlots = tables.Count + 1;
            var paragraphs = new List<ContentPile>();
            for (int i = 0; i < numSlots; i++)
            {
                paragraphs.Add(new ContentPile());
            }

            // Assign texts to appropriate paragraph based on their position
            foreach (var text in TextElements)
            {
                if (allTableTexts.Contains(text))
                    continue;

                // Find which slot this text belongs to
                for (int idx = 0; idx < tops.Count; idx++)
                {
                    if (text.Y0 > tops[idx])
                    {
                        paragraphs[idx].TextElements.Add(text);
                        break;
                    }
                }
            }

            // Filter out empty paragraphs
            return paragraphs.Where(p => p.TextElements.Count > 0).ToList();
        }

        private List<ContentPile> FindImages()
        {
            var images = new List<ContentPile>();
            foreach (var image in Images)
            {
                var pile = new ContentPile();
                pile.Images.Add(image);
                images.Add(pile);
            }
            return images;
        }

        /// <summary>
        /// Returns the first text element in this pile, or null if empty.
        /// Used for sorting piles by position.
        /// </summary>
        private PdfTextElement GetFirstElement()
        {
            if (TextElements.Count > 0) return TextElements[0];
            if (Images.Count > 0) return null; // Images don't have PdfTextElement
            return null;
        }

        /// <summary>
        /// Generates markdown for this pile based on its type (table or paragraph).
        /// </summary>
        public string GenerateMarkdown(MarkdownState state)
        {
            string pileType = GetType();
            if (pileType == "paragraph")
            {
                return GenerateParagraphMarkdown(state);
            }
            else if (pileType == "table")
            {
                return GenerateTableMarkdown(state);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported markdown type: {pileType}");
            }
        }

        /// <summary>
        /// Generates markdown from a table pile.
        /// Creates HTML table structure with proper colspan/rowspan handling.
        /// </summary>
        public string GenerateTableMarkdown(MarkdownState state)
        {
            var intermediate = GenerateTableIntermediate();
            return IntermediateToMarkdown(intermediate, state);
        }

        /// <summary>
        /// Generates intermediate table structure (2D array of cells with texts).
        /// </summary>
        private List<List<Dictionary<string, object>>> GenerateTableIntermediate()
        {
            var verticalCoords = CalcCoordinates(VerticalLines, isVertical: true);
            var horizontalCoords = CalcCoordinates(HorizontalLines, isVertical: false);

            int numRows = horizontalCoords.Count - 1;
            int numCols = verticalCoords.Count - 1;

            var intermediate = new List<List<Dictionary<string, object>>>();
            for (int i = 0; i < numRows; i++)
            {
                intermediate.Add(new List<Dictionary<string, object>>());
            }

            for (int rowIdx = 0; rowIdx < numRows; rowIdx++)
            {
                for (int colIdx = 0; colIdx < numCols; colIdx++)
                {
                    double left = verticalCoords[colIdx];
                    double top = horizontalCoords[rowIdx];
                    double right = verticalCoords[colIdx + 1];
                    double bottom = horizontalCoords[rowIdx + 1];

                    if (IsIgnoreCell(left, top, right, bottom))
                        continue;

                    var (newRight, colspan) = FindExistCoor(bottom, top, colIdx, verticalCoords, "vertical");
                    var (newBottom, rowspan) = FindExistCoor(left, right, rowIdx, horizontalCoords, "horizontal");

                    var cell = new Dictionary<string, object>();
                    cell["texts"] = FindCellTexts(left, top, newRight, newBottom);
                    if (colspan > 1)
                        cell["colspan"] = colspan;
                    if (rowspan > 1)
                        cell["rowspan"] = rowspan;

                    intermediate[rowIdx].Add(cell);
                }
            }

            return intermediate;
        }

        /// <summary>
        /// Calculates coordinate boundaries (unique X or Y positions).
        /// Returns sorted list in descending order (for top-to-bottom processing).
        /// </summary>
        private List<double> CalcCoordinates(List<PdfLineElement> lines, bool isVertical)
        {
            var coordSet = new HashSet<double>();
            foreach (var line in lines)
            {
                if (isVertical)
                    coordSet.Add(line.X0);
                else
                    coordSet.Add(line.Y0);
            }
            var coordList = coordSet.ToList();
            coordList.Sort((a, b) => b.CompareTo(a)); // Descending order
            return coordList;
        }

        /// <summary>
        /// Finds cell texts that fall within the given boundaries.
        /// </summary>
        private List<PdfTextElement> FindCellTexts(double left, double top, double right, double bottom)
        {
            var texts = new List<PdfTextElement>();
            foreach (var text in TextElements)
            {
                if (IsInRange(left, top, right, bottom, text))
                {
                    texts.Add(text);
                }
            }
            return texts;
        }

        /// <summary>
        /// Checks if a text element is within cell boundaries.
        /// </summary>
        private bool IsInRange(double left, double top, double right, double bottom, PdfTextElement obj)
        {
            // Check for invalid dimensions
            if (obj.X0 >= obj.X1) return false;
            if (obj.Y0 >= obj.Y1) return false;

            // Check left boundary
            if ((left - SEARCH_DISTANCE_VERTICAL) > obj.X0)
                return false;

            // Check right boundary
            if (obj.X0 > right)
                return false;

            // Check top boundary
            if (obj.Y1 > (top + SEARCH_DISTANCE_HORIZONTAL))
                return false;

            // Check bottom boundary
            if ((bottom - SEARCH_DISTANCE_HORIZONTAL) > obj.Y0)
                return false;

            return true;
        }

        /// <summary>
        /// Checks if a cell should be ignored (no left or top border).
        /// </summary>
        private bool IsIgnoreCell(double left, double top, double right, double bottom)
        {
            bool leftExists = LineExists(left, bottom, top, "vertical");
            bool topExists = LineExists(top, left, right, "horizontal");
            return !leftExists || !topExists;
        }

        /// <summary>
        /// Finds the next coordinate where a line exists, and returns span count.
        /// </summary>
        private (double Coordinate, int Span) FindExistCoor(double minimum, double maximum, int startIdx, List<double> lineCoords, string direction)
        {
            int span = 0;
            bool lineExists = false;

            while (!lineExists)
            {
                span++;
                if ((startIdx + span) < lineCoords.Count)
                {
                    double coor = lineCoords[startIdx + span];
                    lineExists = LineExists(coor, minimum, maximum, direction);
                    if (lineExists)
                        return (coor, span);
                }
                else
                {
                    lineExists = true;
                    return (lineCoords[startIdx + span - 1], span);
                }
            }

            return (lineCoords[startIdx + span - 1], span);
        }

        /// <summary>
        /// Checks if a line exists at target coordinate within the range [minimum, maximum].
        /// </summary>
        private bool LineExists(double target, double minimum, double maximum, string direction)
        {
            var lines = (direction == "vertical") ? VerticalLines : HorizontalLines;

            foreach (var line in lines)
            {
                double lineCoord = (direction == "vertical") ? line.X0 : line.Y0;
                if (Math.Abs(lineCoord - target) > 0.01) // Use small epsilon for double comparison
                    continue;

                if (direction == "vertical")
                {
                    // Check if line fills vertical range [bottom, top]
                    if (line.Y0 <= (minimum + SEARCH_DISTANCE_VERTICAL) &&
                        (maximum - SEARCH_DISTANCE_VERTICAL) <= line.Y1)
                    {
                        return true;
                    }
                }
                else
                {
                    // Check if line fills horizontal range [left, right]
                    if (line.X0 <= (minimum + SEARCH_DISTANCE_HORIZONTAL) &&
                        (maximum - SEARCH_DISTANCE_HORIZONTAL) <= line.X1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Converts intermediate table structure to HTML markdown.
        /// Handles opcode table detection and merging.
        /// </summary>
        private string IntermediateToMarkdown(List<List<Dictionary<string, object>>> intermediate, MarkdownState state)
        {
            var sb = new StringBuilder();

            // Skip first row if current and previous are opcode tables
            if (state.CurrentPileIsOpcodeTable && state.PreviousPileIsOpcodeTable)
            {
                if (intermediate.Count > 0)
                {
                    intermediate.RemoveAt(0);
                }
            }
            else
            {
                sb.AppendLine("<table>");
            }

            bool firstLine = true;
            foreach (var row in intermediate)
            {
                sb.AppendLine("\t<tr>");
                foreach (var cell in row)
                {
                    var texts = (List<PdfTextElement>)cell["texts"];
                    var cellTexts = string.Join(" ", texts.Select(t => t.GetText()));

                    int colspan = cell.ContainsKey("colspan") ? (int)cell["colspan"] : 1;
                    int rowspan = cell.ContainsKey("rowspan") ? (int)cell["rowspan"] : 1;

                    string colspanAttr = (colspan > 1) ? $" colspan={colspan}" : "";
                    string rowspanAttr = (rowspan > 1) ? $" rowspan={rowspan}" : "";

                    if (firstLine)
                    {
                        sb.AppendLine($"\t\t<td{colspanAttr}{rowspanAttr}><b>{cellTexts}</b></td>");
                    }
                    else
                    {
                        sb.AppendLine($"\t\t<td{colspanAttr}{rowspanAttr}>{cellTexts}</td>");
                    }
                }
                sb.AppendLine("\t</tr>");
                firstLine = false;
            }

            if (!state.CurrentPileIsOpcodeTable || !state.NextPileIsOpcodeTable)
            {
                sb.AppendLine("</table>");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates markdown from a paragraph pile.
        /// Implements state machine for tracking section types (description, operation, flags, etc.).
        /// </summary>
        public string GenerateParagraphMarkdown(MarkdownState state)
        {
            var sb = new StringBuilder();
            double previousHeight = 0;
            int counter = 0;

            // Sort texts by Y position (top to bottom in reverse order)
            var sortedTexts = TextElements
                .OrderByDescending(t => t.Y1)
                .ThenBy(t => t.X0)
                .ToList();

            foreach (var text in sortedTexts)
            {
                string content = text.GetText();

                // Skip certain header patterns in first line
                if (counter == 0)
                {
                    if (content.Contains("INSTRUCTION SET REFERENCE, ") ||
                        content.Contains("SAFER MODE EXTENSIONS REFERENCE"))
                    {
                        continue;
                    }
                }

                // Skip page metadata
                if (content.Contains("Vol. 2"))
                {
                    if (text.Height < 10.0) continue;
                }

                if (content.Contains("Ref. "))
                {
                    if (text.Height < 10.0) continue;
                }

                if (content.StartsWith("5-"))
                {
                    if (text.Height < 10.0) continue;
                }

                // Escape markdown special characters
                content = content.Replace("#", "\\#").Replace("*", "\\*");

                // Check for em-dash (instruction header)
                if (content.Contains("—"))
                {
                    if (text.Height >= 10.0) // Not a footer
                    {
                        var (instruction, description) = GetInstruction();
                        if (instruction != null)
                        {
                            state.TypeNext = "title";
                            string instructionText = instruction.Replace("/", " / ");
                            sb.AppendLine($"<b>{instructionText}</b> — {description}");
                            counter++;
                            continue;
                        }
                    }
                }

                // Check for section headers
                if (content == "Description" || content == "IA-32 Architecture Compatibility")
                {
                    state.TypeNext = "description";
                    sb.AppendLine($"\n### {content}\n");
                }
                else if (content == "Instruction Operand Encoding")
                {
                    state.TypeNext = "encoding";
                    sb.AppendLine($"\n### {content}\n");
                }
                else if (content == "Operation")
                {
                    state.TypeNext = "operation";
                    sb.AppendLine($"\n### {content}\n");
                    sb.AppendLine("```java");
                }
                else if (content == "Flags Affected" || content == "FPU Flags Affected")
                {
                    if (state.CodeMode)
                    {
                        sb.AppendLine("```");
                        state.CodeMode = false;
                    }
                    state.TypeNext = "flags";
                    sb.AppendLine($"\n### {content}\n");
                }
                else if (content == "Intel C/C++ Compiler Intrinsic Equivalent" ||
                         content == "C/C++ Compiler Intrinsic Equivalent")
                {
                    if (state.CodeMode)
                    {
                        sb.AppendLine("```");
                        state.CodeMode = false;
                    }
                    state.TypeNext = "intrinsics";
                    sb.AppendLine($"\n### {content}\n");
                    sb.AppendLine("```c");
                    state.CodeMode = true;
                }
                else if (IsExceptionHeader(content))
                {
                    if (state.CodeMode)
                    {
                        sb.AppendLine("```");
                        state.CodeMode = false;
                    }
                    state.TypeNext = "exceptions";
                    sb.AppendLine($"\n### {content}\n");
                }
                else
                {
                    // Regular content in current section
                    if (state.Type == "title")
                    {
                        sb.AppendLine(content);
                    }
                    else if (state.Type == "description")
                    {
                        double heightDiff = previousHeight - text.Y1;
                        if (heightDiff > 15)
                            sb.AppendLine();
                        sb.AppendLine(content);
                    }
                    else if (state.Type == "encoding")
                    {
                        // Skip encoding tables for now
                    }
                    else if (state.Type == "operation")
                    {
                        if (!string.IsNullOrEmpty(text.FontName) && text.FontName.EndsWith("NeoSansIntelMedium"))
                        {
                            sb.AppendLine("```");
                            sb.AppendLine($"\n#### {content}\n");
                            sb.AppendLine("```java");
                            state.CodeMode = true;
                        }
                        else
                        {
                            string indent = CreateIndent(text.X0);
                            sb.Append(indent);
                            sb.AppendLine(content.Replace("", "←"));
                        }
                    }
                    else if (state.Type == "intrinsics")
                    {
                        sb.AppendLine(content);
                    }
                    else if (state.Type == "flags")
                    {
                        double heightDiff = previousHeight - text.Y1;
                        if (heightDiff > 15)
                            sb.AppendLine();
                        sb.AppendLine(content);
                    }
                    else if (state.Type == "exceptions")
                    {
                        // Handle special characters in exceptions
                        if (content.Contains("#") && !content.Contains("(#"))
                        {
                            content = content.Replace("#", "<p>#");
                        }
                        double heightDiff = previousHeight - text.Y1;
                        if (heightDiff > 15)
                            sb.AppendLine();
                        sb.AppendLine(content);
                    }
                }

                state.Type = state.TypeNext;
                previousHeight = text.Y1;
                counter++;
            }

            // Close code block if still open
            if (state.CodeMode)
            {
                sb.AppendLine("```");
                state.CodeMode = false;
            }

            return sb.ToString();
        }

        private bool IsExceptionHeader(string content)
        {
            return content is "Other Exceptions" or
                "Compatibility Mode Exceptions" or
                "64-Bit Mode Exceptions" or
                "Exceptions (All Operating Modes)" or
                "Floating-Point Exceptions" or
                "Other Mode Exceptions" or
                "Virtual-8086 Mode Exceptions" or
                "SIMD Floating-Point Exceptions" or
                "SIMD Floating Point Exceptions" or
                "Protected Mode Exceptions" or
                "Exceptions" or
                "Numeric Exceptions" or
                "Virtual 8086 Mode Exceptions" or
                "Real-Address Mode Exceptions";
        }

        private string CreateIndent(double xPos)
        {
            const double offset = 47;
            const double width = 18;
            const string indent = "    ";

            if (xPos < offset) return "";
            if (xPos < offset + width) return indent;
            if (xPos < offset + (2 * width)) return indent + indent;
            if (xPos < offset + (3 * width)) return indent + indent + indent;
            if (xPos < offset + (4 * width)) return indent + indent + indent + indent;
            if (xPos < offset + (5 * width)) return indent + indent + indent + indent + indent;
            if (xPos < offset + (6 * width)) return indent + indent + indent + indent + indent + indent;
            return indent + indent + indent + indent + indent + indent + indent;
        }

        private List<PdfLineElement> FindNearVerticals(PdfLineElement start)
        {
            var near = new List<PdfLineElement> { start };
            double top = start.Y1;
            double bottom = start.Y0;

            foreach (PdfLineElement v in VerticalLines)
            {
                if (v == start) continue;
                if (IsOverlap(top, bottom, v))
                {
                    near.Add(v);
                    (top, bottom) = CalcTopBottom(near);
                }
            }

            return near;
        }

        private (double Top, double Bottom) CalcTopBottom(List<PdfLineElement> objects)
        {
            double top = double.NegativeInfinity;
            double bottom = double.PositiveInfinity;

            foreach (var obj in objects)
            {
                top = Math.Max(top, obj.Y1);
                bottom = Math.Min(bottom, obj.Y0);
            }

            return (top, bottom);
        }

        private (double Top, double Bottom) CalcTopBottom(List<PdfTextElement> objects)
        {
            double top = double.NegativeInfinity;
            double bottom = double.PositiveInfinity;

            foreach (var obj in objects)
            {
                top = Math.Max(top, obj.Y1);
                bottom = Math.Min(bottom, obj.Y0);
            }

            return (top, bottom);
        }

        private bool IsOverlap(double top, double bottom, PdfLineElement obj)
        {
            const double searchDistance = 0.7;
            return ((bottom - searchDistance) <= obj.Y0 && obj.Y0 <= (top + searchDistance)) ||
                   ((bottom - searchDistance) <= obj.Y1 && obj.Y1 <= (top + searchDistance));
        }

        private List<T> FindIncluded<T>(double top, double bottom, List<T> objects) where T : class
        {
            var included = new List<T>();
            foreach (T obj in objects)
            {
                if (obj is PdfLineElement line && IsOverlap(top, bottom, line))
                    included.Add(obj);
                else if (obj is PdfTextElement text && IsOverlap(top, bottom, text))
                    included.Add(obj);
            }
            return included;
        }

        private bool IsOverlap(double top, double bottom, PdfTextElement obj)
        {
            const double searchDistance = 0.7;
            return ((bottom - searchDistance) <= obj.Y0 && obj.Y0 <= (top + searchDistance)) ||
                   ((bottom - searchDistance) <= obj.Y1 && obj.Y1 <= (top + searchDistance));
        }
    }

    /// <summary>
    /// Helper class for itext7 text extraction results
    /// </summary>
    internal class PdfTextInfo
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string FontName { get; set; }
    }

    /// <summary>
    /// Helper class for itext7 line extraction results (vector graphics)
    /// </summary>
    internal class PdfLineInfo
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    /// <summary>
    /// Parses Intel PDF documents using itext7 library.
    /// Extracts text and vector lines (borders) from each page.
    /// </summary>
    public class PdfDocumentParser
    {
        private readonly string _filePath;

        public PdfDocumentParser(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"PDF file not found: {filePath}");
            _filePath = filePath;
        }

        /// <summary>
        /// Parses all pages in the PDF and returns ContentPile objects.
        /// Each pile represents a logical section (table or paragraph).
        /// </summary>
        public List<ContentPile> ParseDocument()
        {
            var allPiles = new List<ContentPile>();

            try
            {
                using (var pdfDocument = new PdfDocument(new PdfReader(_filePath)))
                {
                    for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
                    {
                        var piles = ParsePage(pdfDocument, pageNum);
                        allPiles.AddRange(piles);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing PDF file: {_filePath}", ex);
            }

            return allPiles;
        }

        /// <summary>
        /// Parses a single page and extracts text/lines into ContentPile objects.
        /// </summary>
        private List<ContentPile> ParsePage(PdfDocument pdfDocument, int pageNum)
        {
            var pile = new ContentPile();
            var page = pdfDocument.GetPage(pageNum);
            var pageBox = page.GetMediaBox();

            // Extract text using custom strategy
            try
            {
                var textExtractor = new PdfPageTextExtractor();
                var textElements = textExtractor.ExtractText(page, pageBox);
                pile.TextElements.AddRange(textElements);

                // Extract vector lines (borders)
                var lineElements = ExtractLines(page, pageBox);
                foreach (var line in lineElements)
                {
                    if (line.IsVertical)
                    {
                        pile.VerticalLines.Add(line);
                    }
                    else if (line.IsHorizontal)
                    {
                        pile.HorizontalLines.Add(line);
                    }
                }

                // Fallback: If no lines extracted, use heuristic detection from text positions
                if (pile.VerticalLines.Count == 0 && pile.HorizontalLines.Count == 0)
                {
                    pile.DetectTableBoundariesFromTextPositions();
                }
            }
            catch (Exception ex)
            {
                // Log but continue - PDF parsing can be fragile
                Console.WriteLine($"Warning: Error parsing page {pageNum}: {ex.Message}");
            }

            // Split into logical piles (tables vs paragraphs)
            var piles = pile.SplitIntoPiles();
            return piles.Count > 0 ? piles : [pile];
        }

        /// <summary>
        /// Extracts vector line elements (borders, rules) from a page.
        /// These define table boundaries using custom PDF operator processing.
        /// </summary>
        private List<PdfLineElement> ExtractLines(PdfPage page, Rectangle pageBox)
        {
            var lines = new List<PdfLineElement>();

            try
            {
                var listener = new PdfGraphicsOperatorListener();
                PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                lines.AddRange(listener.ExtractedLines);
            }
            catch { /* Ignore if content extraction fails */ }

            return lines;
        }
    }

    /// <summary>
    /// Custom event listener for extracting graphics operators from PDF content streams.
    /// Implements IEventListener to capture drawing commands (lines, rectangles) that form table borders.
    ///
    /// LIMITATION: itext7's C# API provides limited access to raw PDF operators.
    /// PathRenderInfo doesn't expose the actual path coordinates or operator sequences.
    /// This is a significant limitation compared to pdfminer (Python), which provides
    /// detailed LTLine, LTRect, and other graphical primitives.
    ///
    /// WORKAROUND: For Intel PDFs, table borders can be detected via heuristic analysis
    /// of text element positions instead. Text that aligns vertically or horizontally
    /// with consistent spacing likely represents column/row boundaries.
    ///
    /// Alternative: Use a different PDF library or extract operators directly from
    /// PDF content streams via low-level parsing.
    /// </summary>
    internal class PdfGraphicsOperatorListener : IEventListener
    {
        private readonly List<PdfLineElement> _lines = [];

        public List<PdfLineElement> ExtractedLines => _lines;

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_PATH)
            {
                // Attempt path extraction (limited by itext7 API)
                HandlePathRenderEvent((PathRenderInfo)data);
            }
        }

        private void HandlePathRenderEvent(PathRenderInfo info)
        {
            try
            {
                // itext7's PathRenderInfo provides stroke/fill style info
                // but does not expose the actual path geometry (coordinates)
                // This is a limitation of the C# API binding
            }
            catch { /* Ignore parsing errors */ }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new[] { EventType.RENDER_PATH };
        }
    }

    /// <summary>
    /// Custom event listener for extracting text with position and font information.
    /// Implements IEventListener to capture text rendering events.
    ///
    /// Tracks:
    /// - Text content and positioning (from Tj/TJ operators)
    /// - Font name and size (from Tf operator)
    /// - Text matrix transformation (from Tm operator)
    /// - Character positioning for accurate bounding boxes
    ///
    /// Equivalent to pdfminer's character-level extraction.
    /// </summary>
    internal class PdfTextOperatorListener : IEventListener
    {
        private readonly List<PdfTextElement> _texts = [];
        private string _currentFontName = "";
        private float _currentFontSize = 12;

        public List<PdfTextElement> ExtractedTexts => _texts;

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_TEXT)
            {
                HandleTextRenderEvent((TextRenderInfo)data);
            }
        }

        private void HandleTextRenderEvent(TextRenderInfo info)
        {
            try
            {
                string text = info.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                // Get position information from the text render info
                var baseline = info.GetBaseline();
                var ascent = info.GetAscentLine();

                // Extract bounding box
                float x0 = baseline.GetStartPoint().Get(0);
                float y0 = baseline.GetStartPoint().Get(1);
                float x1 = baseline.GetEndPoint().Get(0);
                float y1 = ascent.GetEndPoint().Get(1);

                // Ensure proper coordinate ordering
                if (x0 > x1) { var temp = x0; x0 = x1; x1 = temp; }
                if (y0 > y1) { var temp = y0; y0 = y1; y1 = temp; }

                var element = new PdfTextElement
                {
                    Content = text,
                    X0 = x0,
                    Y0 = y0,
                    X1 = x1,
                    Y1 = y1,
                    FontName = GetFontName(info)
                };

                if (element.Width > 0 && element.Height > 0)
                {
                    _texts.Add(element);
                }
            }
            catch { /* Ignore text extraction errors */ }
        }

        private string GetFontName(TextRenderInfo info)
        {
            try
            {
                var font = info.GetFont();
                if (font != null)
                {
                    return font.GetFontProgram()?.ToString() ?? "Unknown";
                }
            }
            catch { }
            return _currentFontName;
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new[] { EventType.RENDER_TEXT };
        }
    }

    /// <summary>
    /// Extracts text elements with position and font information from PDF pages.
    /// Equivalent to pdfminer's LTTextLineHorizontal extraction.
    ///
    /// Uses custom PDF content stream processing to track font and position information
    /// via RENDER_TEXT events.
    /// </summary>
    internal class PdfPageTextExtractor
    {
        public List<PdfTextElement> ExtractText(PdfPage page, Rectangle pageBox)
        {
            var elements = new List<PdfTextElement>();

            try
            {
                // Use custom listener to extract text with position and font info
                var listener = new PdfTextOperatorListener();
                PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                elements.AddRange(listener.ExtractedTexts);
            }
            catch { /* Ignore extraction errors */ }

            return elements;
        }
    }

    /// <summary>
    /// Generates markdown files from ContentPile objects.
    /// Equivalent to the Writer class in Python's writer.py.
    /// Handles instruction boundaries and opcode table merging.
    /// </summary>
    public class MarkdownGenerator
    {
        private readonly string _sourceInfo;
        private readonly string _outputDirectory;

        public MarkdownGenerator(string outputDirectory = "./output", string sourceInfo = "Intel® Architecture Instruction Set Extensions and Future Features Programming Reference (December 2020)")
        {
            _outputDirectory = outputDirectory;
            _sourceInfo = sourceInfo;
            Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Processes a list of piles and generates one markdown file per instruction.
        /// </summary>
        public void Write(List<ContentPile> piles)
        {
            if (piles.Count == 0) return;

            string instructionCurrent = null;
            string descriptionCurrent = null;
            (instructionCurrent, descriptionCurrent) = piles[0].GetInstruction();

            string instructionPrev = instructionCurrent;

            var state = new MarkdownState();
            var markdown = new StringBuilder();

            for (int i = 0; i < piles.Count; i++)
            {
                var pile = piles[i];
                var (pileInstruction, pileDescription) = pile.GetInstruction();

                bool createNewFile = false;
                if (pileInstruction != null && pileInstruction != instructionCurrent)
                {
                    createNewFile = true;
                    instructionPrev = instructionCurrent;
                    instructionCurrent = pileInstruction;
                }

                state.CurrentPileIsOpcodeTable = pile.IsOpcodeTable();
                if (state.CurrentPileIsOpcodeTable)
                {
                    state.PreviousPileIsOpcodeTable = FindPreviousOpcodeTable(i, piles, instructionCurrent);
                    state.NextPileIsOpcodeTable = FindNextOpcodeTable(i, piles, instructionCurrent);
                }
                else
                {
                    state.PreviousPileIsOpcodeTable = false;
                    state.NextPileIsOpcodeTable = false;
                }

                if (createNewFile)
                {
                    CloseFile(instructionPrev, markdown.ToString());
                    markdown.Clear();
                    state.PreviousPileIsOpcodeTable = false;
                }

                markdown.Append(pile.GenerateMarkdown(state));
            }

            CloseFile(instructionCurrent, markdown.ToString());
        }

        /// <summary>
        /// Finds if a previous pile (going backward) is an opcode table.
        /// Stops searching if a different instruction is found.
        /// </summary>
        private bool FindPreviousOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null && pileInstruction != instructionCurrent)
                {
                    return false; // Different instruction found
                }

                if (pile.GetType() == "table")
                {
                    return pile.IsOpcodeTable();
                }
            }

            return false;
        }

        /// <summary>
        /// Finds if a next pile (going forward) is an opcode table.
        /// Stops searching if a different instruction is found.
        /// </summary>
        private bool FindNextOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex + 1; j < piles.Count; j++)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null && pileInstruction != instructionCurrent)
                {
                    return false; // Different instruction found
                }

                if (pile.GetType() == "table")
                {
                    return pile.IsOpcodeTable();
                }
            }

            return false;
        }

        /// <summary>
        /// Writes markdown to a file for the given instruction.
        /// Applies hyphenation cleanup and adds generation metadata.
        /// </summary>
        private void CloseFile(string instruction, string markdown)
        {
            if (string.IsNullOrEmpty(instruction)) return;

            markdown = TextCleaner.CleanupHyphenation(markdown);

            string safeFileName = instruction.Replace("/", "_").Replace(" ", "_");
            string filePath = System.IO.Path.Combine(_outputDirectory, $"{safeFileName}.md");

            var now = DateTime.Now;
            string generatedTime = $"{now.Day}-{now.Month}-{now.Year}";
            markdown += $"\n --- \n<p align=\"right\"><i>Source: {_sourceInfo}<br>Generated: {generatedTime}</i></p>\n";

            Console.WriteLine($"Writing {filePath}");
            File.WriteAllText(filePath, markdown, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Cleans up text extracted from PDF documents.
    /// Handles common PDF artifacts like word breaks and special characters.
    ///
    /// CRITICAL: The hyphenation patterns below are empirically determined
    /// from analyzing multiple Intel PDFs. They handle cases where PDFMiner
    /// preserves line breaks: "instruc-\ntions" appears in the extracted text.
    /// </summary>
    public static class TextCleaner
    {
        /// <summary>
        /// Removes PDF hyphenation artifacts and normalizes text.
        ///
        /// The patterns below are specific to Intel documentation.
        /// PDFs often break words across lines with hyphens, which need to be rejoined.
        ///
        /// Example issues in extracted text:
        /// - "instruc-\ntions" should be "instructions"
        /// - "single- precision" should be "single-precision" (space after hyphen is a PDF artifact)
        /// - "•\n" should be "\n * " (bullet points to markdown)
        /// </summary>
        public static string CleanupHyphenation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Bullet points to markdown
            text = text.Replace("•\n\n", "\n * ");
            text = text.Replace("•\n", "\n * ");
            text = text.Replace("•", "\n * ");

            // Word breaks with hyphen-newline pattern
            // These are the most common in Intel docs
            text = text.Replace("addi-\ntional", "additional\n");
            text = text.Replace("combina-\ntion ", "combination\n");
            text = text.Replace("compar-\nison)", "comparison)\n");
            text = text.Replace("compar-\nisons", "comparisons\n");
            text = text.Replace("corre-\nsponding", "corresponding\n");
            text = text.Replace("documenta-\ntion", "documentation\n");
            text = text.Replace("destina-\ntion", "destination\n");
            text = text.Replace("desti-\nnation", "destination\n");
            text = text.Replace("infor-\nmation", "information\n");
            text = text.Replace("instruc-\ntions", "instructions\n");
            text = text.Replace("instruc-\ntion", "instruction\n");
            text = text.Replace("regis-\nters", "registers\n");
            text = text.Replace("regis-\nter", "register\n");
            text = text.Replace("oper-\nands", "operands\n");
            text = text.Replace("oper-\nations", "operations\n");
            text = text.Replace("preci-\nsion", "precision\n");
            text = text.Replace("loca-\ntions", "locations\n");
            text = text.Replace("loca-\ntion", "location\n");
            text = text.Replace("speci-\nfied", "specified\n");
            text = text.Replace("64-\nbit", "64-bit\n");
            text = text.Replace("unpre-\ndictable", "\nunpredictable");
            text = text.Replace("single-\nprecision", "\nsingle-precision");
            text = text.Replace("priv-\nilege", "\nprivilege");

            // Space-hyphen patterns (hyphen with space after it, a PDF artifact)
            text = text.Replace("single- precision", "single-precision");
            text = text.Replace("no- operand", "no-operand");
            text = text.Replace("no- operands", "no-operands");
            text = text.Replace("general- purpose", "general-purpose");
            text = text.Replace("general- protection", "general-protection");
            text = text.Replace("excep- tion", "exception");

            // Edge case: extra space in instruction aliases
            text = text.Replace("REP/REPE/REPZ /REPNE/REPNZ", "REP/REPE/REPZ/REPNE/REPNZ");

            return text;
        }

        /// <summary>
        /// Normalizes text by removing excessive whitespace while preserving structure.
        ///
        /// PDF extraction can introduce:
        /// - Extra spaces between words: "word   word" → "word word"
        /// - Spaces around punctuation: "word , word" → "word, word"
        /// - Multiple newlines: "word\n\n\nword" → "word\n\nword"
        /// </summary>
        public static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Collapse multiple spaces to single space
            text = Regex.Replace(text, @"  +", " ");

            // Collapse multiple newlines to double newline (paragraph break)
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text;
        }

        /// <summary>
        /// Escapes special markdown characters to prevent unintended formatting.
        /// </summary>
        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("#", "\\#").Replace("*", "\\*");
        }
    }
}
