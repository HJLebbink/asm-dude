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

using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Path = iText.Kernel.Geom.Path;

namespace AsmAnnotate
{

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
        /// Number of pages in the PDF.
        /// </summary>
        public int GetPageCount()
        {
            using var pdfDocument = new PdfDocument(new PdfReader(_filePath));
            return pdfDocument.GetNumberOfPages();
        }

        /// <summary>
        /// Parses all pages in the PDF and returns ContentPile objects.
        /// Each pile represents a logical section (table or paragraph).
        /// </summary>
        public List<ContentPile> ParseDocument() => ParseDocument(1, int.MaxValue);

        /// <summary>
        /// Parses pages in the inclusive range [<paramref name="startPage"/>, <paramref name="endPage"/>]
        /// (1-based) and returns ContentPile objects. Useful for iterating on a few instructions
        /// without processing the whole ~5000-page manual.
        /// </summary>
        public List<ContentPile> ParseDocument(int startPage, int endPage)
        {
            var allPiles = new List<ContentPile>();

            try
            {
                using var pdfDocument = new PdfDocument(new PdfReader(_filePath));
                int last = Math.Min(endPage, pdfDocument.GetNumberOfPages());

                // Page-sequence-aware filter. We keep only instruction-reference pages:
                //  - a page with a tall title starts a new instruction (becomes "current");
                //  - a page with only the small running header/footer is kept ONLY when that
                //    footer names the *current* instruction (a genuine continuation, e.g. AAA's
                //    2nd page that holds just the Compatibility/64-Bit Mode Exceptions);
                //  - everything else is Vol 1/3/4 descriptive text and is dropped, so it can't
                //    accumulate into the last-opened file (the 50k-line "ERESUME.md" blobs).
                string? current = null;
                for (int pageNum = Math.Max(1, startPage); pageNum <= last; pageNum++)
                {
                    var piles = ParsePage(pdfDocument, pageNum);

                    string? titled = piles.Select(p => p.GetInstruction().Mnemonic).FirstOrDefault(m => m != null);
                    if (titled != null)
                    {
                        current = titled;
                        allPiles.AddRange(piles);
                        continue;
                    }

                    string? running = piles.Select(p => p.GetRunningTitleMnemonic()).FirstOrDefault(m => m != null);
                    if (running != null && running == current)
                        allPiles.AddRange(piles); // continuation of the current instruction
                    // else: not part of the current instruction — drop the page
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing PDF file: {_filePath}", ex);
            }

            return allPiles;
        }

        /// <summary>
        /// Extracts the raw (un-split) text elements and snapped border lines for a single page.
        /// Intended for diagnostics — lets callers inspect exactly what itext handed us before
        /// the table/paragraph splitting heuristics run.
        /// </summary>
        public ContentPile ExtractRawPage(int pageNum)
        {
            using var pdfDocument = new PdfDocument(new PdfReader(_filePath));
            var page = pdfDocument.GetPage(pageNum);
            var pageBox = page.GetMediaBox();
            var pile = new ContentPile();

            // Lines first, so the column boundaries are known before text is grouped.
            foreach (var line in ExtractLines(page, pageBox))
                pile.AddLineSnapped(line);

            var fragments = new PdfPageTextExtractor().ExtractFragments(page, pageBox);
            pile.TextElements.AddRange(PdfPageTextExtractor.GroupIntoLines(fragments, pile.VerticalLines));

            return pile;
        }

        /// <summary>
        /// Parses a single page and extracts text/lines into ContentPile objects.
        /// </summary>
        private List<ContentPile> ParsePage(PdfDocument pdfDocument, int pageNum)
        {
            var pile = new ContentPile();
            var page = pdfDocument.GetPage(pageNum);
            var pageBox = page.GetMediaBox();

            // Extract text + vector lines. Lines are extracted and snapped FIRST so their
            // x-positions can keep the text grouper from merging across table columns.
            try
            {
                // Vector lines (borders), snapping near-coincident grid lines together so the
                // table-grid math (CalcCoordinates / LineExists) lines up.
                foreach (var line in ExtractLines(page, pageBox))
                {
                    pile.AddLineSnapped(line);
                }

                // Text, grouped into per-line runs that never straddle a column border.
                var fragments = new PdfPageTextExtractor().ExtractFragments(page, pageBox);
                pile.TextElements.AddRange(PdfPageTextExtractor.GroupIntoLines(fragments, pile.VerticalLines));

                // NOTE: no heuristic text-position fallback here. pdfminer (and thus the Python
                // reference) only ever sees real vector borders; synthesizing fake grid lines on
                // paragraph-only pages would invent tables that the reference output never has.
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
    /// Event listener that extracts table-border geometry (lines and thin rectangles)
    /// from PDF content streams.
    ///
    /// This is the C# equivalent of what pdfminer hands the Python port as LTRect/LTLine.
    /// itext fully exposes path geometry: <see cref="PathRenderInfo.GetPath"/> returns the
    /// user-space <see cref="Path"/>, and <see cref="PathRenderInfo.GetCtm"/> the current
    /// transformation matrix. We transform every sub-path point into page space, take the
    /// sub-path's bounding box, and — mirroring pdfminer's rule used by the Python writer —
    /// classify thin boxes as lines:
    ///   width  &lt; 1.0  -> a vertical   line (a table column border)
    ///   height &lt; 1.0  -> a horizontal line (a table row border)
    /// Boxes that are thick in both axes (filled regions, glyphs-as-paths, big strokes)
    /// are ignored, exactly as the Python code ignores non-thin LTRects.
    /// </summary>
    internal class PdfGraphicsOperatorListener : IEventListener
    {
        private readonly List<PdfLineElement> _lines = [];

        public List<PdfLineElement> ExtractedLines => _lines;

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_PATH && data is PathRenderInfo info)
            {
                HandlePathRenderEvent(info);
            }
        }

        private void HandlePathRenderEvent(PathRenderInfo info)
        {
            try
            {
                // Skip paths that only modify the clip region and paint nothing.
                if (info.GetOperation() == PathRenderInfo.NO_OP)
                    return;

                Matrix ctm = info.GetCtm();
                Path path = info.GetPath();
                if (path == null)
                    return;

                foreach (Subpath subpath in path.GetSubpaths())
                {
                    if (subpath.IsEmpty())
                        continue;

                    // Piecewise-linear approximation gives the sub-path corners in user space.
                    var points = subpath.GetPiecewiseLinearApproximation();
                    if (points == null || points.Count < 2)
                        continue;

                    double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                    double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

                    foreach (Point p in points)
                    {
                        // Transform the user-space point into page space via the CTM.
                        Vector v = new Vector((float)p.GetX(), (float)p.GetY(), 1).Cross(ctm);
                        double x = v.Get(Vector.I1);
                        double y = v.Get(Vector.I2);
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }

                    double width = maxX - minX;
                    double height = maxY - minY;

                    if (width < 1.0 && height >= 1.0)
                    {
                        // Vertical border: collapse X to the box's left edge.
                        _lines.Add(new PdfLineElement { X0 = minX, X1 = minX, Y0 = minY, Y1 = maxY });
                    }
                    else if (height < 1.0 && width >= 1.0)
                    {
                        // Horizontal border: collapse Y to the box's bottom edge.
                        _lines.Add(new PdfLineElement { X0 = minX, X1 = maxX, Y0 = minY, Y1 = minY });
                    }
                    // else: a thick box (fill/glyph/large stroke) — not a table border; ignore.
                }
            }
            catch { /* Ignore parsing errors for robustness on malformed paths */ }
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
        // Two fragments belong to the same visual line when their baselines (Y0) are within this.
        private const double BaselineTolerance = 3.0;

        // Within a line, a horizontal gap larger than this starts a new run. Bordered table
        // columns are kept apart by the explicit grid-line check (ColumnBoundaryBetween); this
        // gap handles the *border-less* pseudo-columns (exception "code | description" rows,
        // indented "; comment" pseudocode). It must exceed a justified paragraph's inter-word
        // space (~a few pt) yet stay below those pseudo-column gaps (~15pt+).
        private const double RunGap = 10.0;

        // When merging two fragments separated by more than this, insert a single space
        // (covers spaces that were rendered as kerning rather than an actual space glyph).
        private const double SpaceGap = 1.5;

        /// <summary>
        /// Extracts raw glyph-run fragments (one per RENDER_TEXT event) without grouping.
        /// Callers group them with <see cref="GroupIntoLines"/>, supplying the page's vertical
        /// grid lines so runs never merge across a table-column boundary.
        /// </summary>
        public List<PdfTextElement> ExtractFragments(PdfPage page, Rectangle pageBox)
        {
            var fragments = new List<PdfTextElement>();

            try
            {
                // itext raises RENDER_TEXT per text-showing operator, which on Intel PDFs is
                // per glyph-run — far finer than pdfminer's per-line LTTextLineHorizontal.
                var listener = new PdfTextOperatorListener();
                PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                fragments.AddRange(listener.ExtractedTexts);
            }
            catch { /* Ignore extraction errors */ }

            return fragments;
        }

        /// <summary>
        /// Groups raw glyph-run fragments into per-line runs, reproducing pdfminer's
        /// LTTextLineHorizontal granularity. Fragments are bucketed by baseline (Y0) and,
        /// within a bucket, merged left-to-right until either a large horizontal gap or a
        /// vertical grid line (<paramref name="columnXs"/>) separates them — so two table cells
        /// in the same visual row never collapse into one run even when the column is narrow.
        /// Raw <see cref="PdfTextElement.Content"/> is concatenated so the internal spacing the
        /// PDF encodes (often a trailing space on a fragment) survives; only the final line is
        /// trimmed by <see cref="PdfTextElement.GetText"/>.
        /// </summary>
        public static List<PdfTextElement> GroupIntoLines(List<PdfTextElement> fragments, IReadOnlyList<PdfLineElement> verticals)
        {
            var result = new List<PdfTextElement>();
            if (fragments.Count == 0)
                return result;

            // Order top-to-bottom, then left-to-right.
            var ordered = fragments
                .OrderByDescending(f => f.Y0)
                .ThenBy(f => f.X0)
                .ToList();

            // Bucket fragments into visual lines by baseline proximity.
            var lines = new List<List<PdfTextElement>>();
            var current = new List<PdfTextElement> { ordered[0] };
            double lineY = ordered[0].Y0;
            for (int i = 1; i < ordered.Count; i++)
            {
                if (Math.Abs(ordered[i].Y0 - lineY) <= BaselineTolerance)
                {
                    current.Add(ordered[i]);
                }
                else
                {
                    lines.Add(current);
                    current = new List<PdfTextElement> { ordered[i] };
                    lineY = ordered[i].Y0;
                }
            }
            lines.Add(current);

            // Within each line, merge left-to-right into runs split on column-sized gaps.
            foreach (var line in lines)
            {
                line.Sort((a, b) => a.X0.CompareTo(b.X0));

                PdfTextElement? run = null;
                foreach (var frag in line)
                {
                    if (run == null)
                    {
                        run = Clone(frag);
                        continue;
                    }

                    double gap = frag.X0 - run.X1;
                    if (gap <= RunGap && !ColumnBoundaryBetween(run.X1, frag.X0, frag.Y0, verticals))
                    {
                        // Same run: append, inserting a space if the visual gap warrants one.
                        string sep = (gap > SpaceGap
                                      && !run.Content.EndsWith(' ')
                                      && !frag.Content.StartsWith(' ')) ? " " : "";
                        run.Content += sep + frag.Content;
                        run.X1 = Math.Max(run.X1, frag.X1);
                        run.Y0 = Math.Min(run.Y0, frag.Y0);
                        run.Y1 = Math.Max(run.Y1, frag.Y1);
                    }
                    else
                    {
                        result.Add(run);
                        run = Clone(frag);
                    }
                }
                if (run != null)
                    result.Add(run);
            }

            return result;
        }

        /// <summary>
        /// True if a vertical grid line sits strictly between <paramref name="leftX"/> and
        /// <paramref name="rightX"/> AND vertically spans <paramref name="y"/>, i.e. the two
        /// fragments straddle a real table-column boundary at this row and must not be merged.
        /// The Y check is essential: without it, a full-width paragraph line would be split by a
        /// table's column line that merely shares an x-position higher up the page.
        /// </summary>
        private static bool ColumnBoundaryBetween(double leftX, double rightX, double y, IReadOnlyList<PdfLineElement> verticals)
        {
            const double eps = 0.5;
            foreach (PdfLineElement v in verticals)
            {
                if (v.X0 > leftX + eps && v.X0 < rightX - eps
                    && v.Y0 - eps <= y && y <= v.Y1 + eps)
                    return true;
            }
            return false;
        }

        private static PdfTextElement Clone(PdfTextElement f) => new()
        {
            Content = f.Content,
            X0 = f.X0,
            Y0 = f.Y0,
            X1 = f.X1,
            Y1 = f.Y1,
            FontName = f.FontName, // keep the first fragment's font (pdfminer uses _objs[0].fontname)
        };
    }
}
