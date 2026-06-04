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
using Path = iText.Kernel.Geom.Path;

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
        /// The instruction title looks like "MNEMONIC<sep>Description", where the separator
        /// is an em-dash (U+2014), en-dash (U+2013) or — in recent SDM revisions, as itext
        /// decodes the title font — a plain hyphen. Example: "AAA-ASCII Adjust After Addition".
        ///
        /// CRITICAL: Font + height select the title and avoid false positives. Titles use the
        /// "NeoSansIntelMedium" font; the running-header title is ~12.4pt while section headers
        /// ("Instruction Operand Encoding") are ~10.3pt and body text ~9.3pt, so a 11.5pt floor
        /// isolates the title. (The 2018 PDF used &gt;14.5pt; 11.5 covers both old and new.)
        /// </summary>
        public (string Mnemonic, string Description) GetInstruction()
        {
            // Title-font lines, top-to-bottom. A very long multi-form title wraps, putting the
            // mnemonic part (ending with the '-' separator) on one line and the description on the
            // next (e.g. "VEXTRACTF128/.../VEXTRACTF64x4-" then "Extract Packed Floating-Point
            // Values"); we have to look at the following line to recover the description.
            var titles = TextElements
                .Where(t => t.Height >= TitleMinHeight
                            && !string.IsNullOrEmpty(t.FontName) && t.FontName.EndsWith("NeoSansIntelMedium"))
                .OrderByDescending(t => t.Y1)
                .ToList();

            for (int i = 0; i < titles.Count; i++)
            {
                string content = titles[i].GetText();
                int idx = IndexOfTitleSeparator(content);
                if (idx <= 0) continue;

                string mnemonic = content[..idx].Trim();
                string descr = content[(idx + 1)..].Trim();
                // Wrapped title: separator at the end, description is the next title line.
                if (descr.Length == 0 && i + 1 < titles.Count)
                    descr = titles[i + 1].GetText().Trim();

                // The part before the separator must look like a mnemonic (rejects pseudocode such
                // as "CPUID.17H.M>..."). Reject a MULTI-WORD ALL-CAPS description: those are
                // appendix/section headings ("GENERAL — PURPOSE INSTRUCTION FORMATS..."), not
                // instructions. Sentence-case ("Complement Carry Flag") and short single-word
                // ("FTST — TEST") descriptions are kept.
                bool headingLike = !descr.Any(char.IsLower) && descr.Contains(' ');
                if (mnemonic.Length > 0 && descr.Length > 0
                    && LooksLikeMnemonic(mnemonic)
                    && !headingLike)
                    return (ExpandCompactMnemonic(mnemonic), descr);
            }

            return (null, null);
        }

        /// <summary>
        /// Returns the mnemonic from this pile's running header/footer — the "MNEMONIC-Description"
        /// line repeated in the top or bottom page margin — or null if there isn't one. Unlike
        /// <see cref="GetInstruction"/> this accepts the small (≈8pt) footer that continuation pages
        /// use *instead* of a tall title (e.g. AAA's 2nd page, which only has the "Compatibility /
        /// 64-Bit Mode Exceptions" sections plus the footer). Restricted to the margins so body
        /// mentions of "IA-32 ..." don't qualify. The caller pairs this with the current instruction
        /// to decide whether the page is a genuine continuation.
        /// </summary>
        public string GetRunningTitleMnemonic()
        {
            foreach (PdfTextElement text in TextElements)
            {
                // Only the page margins (top header band / bottom footer band).
                bool inMargin = text.Y0 > 700.0 || text.Y1 < 65.0;
                if (!inMargin) continue;

                string content = text.GetText();
                int idx = IndexOfTitleSeparator(content);
                if (idx <= 0 || idx >= content.Length - 1) continue;

                string mnemonic = content[..idx].Trim();
                string descr = content[(idx + 1)..].Trim();
                if (mnemonic.Length > 0 && descr.Length > 0
                    && LooksLikeMnemonic(mnemonic)
                    && descr.Any(char.IsLower))
                    return ExpandCompactMnemonic(mnemonic);
            }
            return null;
        }

        /// <summary>Minimum text height (pt) for a line to be considered an instruction title.</summary>
        internal const double TitleMinHeight = 11.5;

        /// <summary>
        /// Expands the SDM's compact multi-form shorthand into the slash-joined explicit list so the
        /// page name matches the other multi-form pages. A bracket group containing commas is a set
        /// of alternatives (an empty alternative is allowed): "VF[,N]MADD[132,213,231]PH" becomes
        /// "VFMADD132PH/VFMADD213PH/VFMADD231PH/VFNMADD132PH/VFNMADD213PH/VFNMADD231PH". A bracket
        /// WITHOUT a comma is a literal part of the name and is kept (e.g. "GETSEC[SENTER]").
        /// </summary>
        internal static string ExpandCompactMnemonic(string m)
        {
            if (m.IndexOf('[') < 0) return m;

            var results = new List<string> { string.Empty };
            int i = 0;
            while (i < m.Length)
            {
                if (m[i] == '[')
                {
                    int close = m.IndexOf(']', i);
                    if (close < 0) break; // malformed — bail
                    string inner = m.Substring(i + 1, close - i - 1);
                    if (inner.Contains(','))
                    {
                        string[] opts = inner.Split(',');
                        var next = new List<string>(results.Count * opts.Length);
                        foreach (string r in results)
                            foreach (string o in opts)
                                next.Add(r + o);
                        results = next;
                    }
                    else
                    {
                        for (int k = 0; k < results.Count; k++) results[k] += "[" + inner + "]";
                    }
                    i = close + 1;
                }
                else
                {
                    int nextBracket = m.IndexOf('[', i);
                    string lit = (nextBracket < 0) ? m[i..] : m[i..nextBracket];
                    for (int k = 0; k < results.Count; k++) results[k] += lit;
                    i += lit.Length;
                }
            }
            return string.Join("/", results);
        }

        /// <summary>
        /// True if <paramref name="token"/> looks like an instruction mnemonic: an upper-case run
        /// of letters/digits, optionally slash- or comma-joined for multi-form titles
        /// ("AAA", "PUNPCKLBW/PUNPCKHBW", "VFMADD132PD/...", "MOVDQA,VMOVDQA32/64"). A few lowercase
        /// placeholders Intel uses are tolerated: the condition code "cc" (Jcc, CMOVcc, SETcc,
        /// CMPccXADD), the EVEX tuple-width "x4/x8/x16" (VEXTRACTF32x4), a single trailing hint
        /// letter ("PREFETCHh"), and a single-letter operand placeholder after a space ("INT n").
        /// Mixed-case words ("General", "Adjusts"), prose with spaces, and pseudocode
        /// ("CPUID.17H.M>...") are rejected so they don't become junk files.
        /// </summary>
        internal static bool LooksLikeMnemonic(string token)
        {
            if (token.Length is 0 or > 80) return false;

            string t = token.Replace("cc", "");                 // condition-code placeholder
            t = Regex.Replace(t, "x[0-9]+", "");                // EVEX tuple width (x4/x8/x16)
            // single-letter operand placeholder after a space/start, before a slash/comma/end
            // ("INT n/INTO" -> "INT/INTO"); a real prose word ("and", "in") is longer and survives.
            t = Regex.Replace(t, @"(?:^|(?<=[/,A-Z0-9])) [a-z](?=[/,]|$)", "");

            bool hasUpper = false;
            int otherLower = 0;
            foreach (char c in t)
            {
                if (c >= 'A' && c <= 'Z') { hasUpper = true; continue; }
                if ((c >= '0' && c <= '9') || c is '/' or '[' or ']' or ',') continue; // '[]' GETSEC; ',' multi-form
                if (c >= 'a' && c <= 'z') { otherLower++; continue; } // e.g. PREFETCHh
                return false; // space (other than the placeholder), punctuation -> not a mnemonic token
            }
            return hasUpper && otherLower <= 1;
        }

        /// <summary>
        /// Index of the first instruction-title separator (em-dash, en-dash, or hyphen) in the
        /// text, or -1 if none. Hyphen is accepted only because the caller has already gated on
        /// the title font/height, where the first hyphen reliably splits mnemonic from description.
        /// </summary>
        internal static int IndexOfTitleSeparator(string content)
        {
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c is '—' or '–' or '-')
                    return i;
            }
            return -1;
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

        /// <summary>
        /// Adds an already-classified line to this pile, first snapping it to a nearby
        /// parallel line (pdfminer's <c>_adjust_to_close</c>). Snapping makes near-coincident
        /// grid lines share an identical coordinate so that <see cref="CalcCoordinates"/> can
        /// dedupe them into a single column/row and <see cref="LineExists"/> can match them.
        /// </summary>
        internal void AddLineSnapped(PdfLineElement line)
        {
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
        /// Calculates the unique grid coordinate boundaries.
        /// Vertical (column) coordinates are returned ascending (left → right) and horizontal
        /// (row) coordinates descending (top → bottom), matching pdfminer/the Python writer
        /// (verticals reverse=False, horizontals reverse=True). Getting this backwards inverts
        /// each cell's left/right edges so no text ever falls "in range" and tables come out empty.
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
            if (isVertical)
                coordList.Sort((a, b) => a.CompareTo(b)); // ascending: left → right
            else
                coordList.Sort((a, b) => b.CompareTo(a)); // descending: top → bottom
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
                    var cellTexts = RenderCellText(texts);

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
        /// Builds a table cell's text from its runs. Footnote-reference superscripts — small
        /// (notably shorter than the cell's body text) 1-2 digit numbers that the PDF raises above
        /// the baseline — are pulled out of the body and rendered as &lt;sup&gt;N&lt;/sup&gt; markers
        /// after the body text. Without this they land inline as a bare " 1" (and, depending on the
        /// glyph's extraction order, sometimes even ahead of the operand: "1 ADD r/m8"). Body runs
        /// are ordered top-to-bottom then left-to-right, and no space is inserted before a leading
        /// punctuation run.
        /// </summary>
        private static string RenderCellText(List<PdfTextElement> texts)
        {
            if (texts.Count == 0) return string.Empty;

            double bodyHeight = texts.Max(t => t.Height);

            var body = new List<PdfTextElement>();
            var marks = new List<string>();
            foreach (var t in texts)
            {
                string s = t.GetText();
                if (IsFootnoteSuperscript(t, bodyHeight, s))
                    marks.Add(s);
                else
                    body.Add(t);
            }

            var sb = new StringBuilder();
            foreach (var t in body.OrderByDescending(t => t.Y1).ThenBy(t => t.X0))
            {
                string s = t.GetText();
                if (s.Length == 0) continue;
                if (sb.Length > 0 && !(s[0] is ',' or '.' or ';' or ':' or ')'))
                    sb.Append(' ');
                sb.Append(s);
            }

            // A superscript that sat between two runs can leave a stray space before punctuation
            // ("r/m8 ," -> "r/m8,").
            string text = sb.ToString().Replace(" ,", ",").Replace(" ;", ";").Replace(" .", ".");

            // Footnote markers after the body; collapse repeats (the same note referenced for
            // several operands shows as one "<sup>1</sup>", not "<sup>1</sup><sup>1</sup>").
            foreach (string m in marks.Distinct())
                text += $"<sup>{m}</sup>";

            return text;
        }

        /// <summary>
        /// True when <paramref name="t"/> is a footnote-reference superscript: a 1-2 digit number
        /// rendered clearly smaller than the cell's body text.
        /// </summary>
        private static bool IsFootnoteSuperscript(PdfTextElement t, double bodyHeight, string s)
        {
            if (s.Length is 0 or > 2) return false;
            foreach (char c in s)
                if (c < '0' || c > '9') return false;
            return t.Height < bodyHeight - 1.0;
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

                // Running header / footer line: "MNEMONIC<sep>Description" (the instruction
                // title repeated at the top/bottom of every page). Detected by a mnemonic-like
                // token before the first separator. The tall NeoSansIntelMedium copy at the top
                // of the first page is emitted as the bold title; every other copy (the small
                // footer, the repeated header on continuation pages) is dropped — mirroring the
                // Python "em-dash + height<10 -> continue" footer skip, generalised to the hyphen
                // separator that recent SDM revisions use.
                {
                    string rawLine = text.GetText();
                    int sepIdx = IndexOfTitleSeparator(rawLine);
                    bool headerLike = sepIdx > 0 && LooksLikeMnemonic(rawLine[..sepIdx].Trim());
                    if (headerLike)
                    {
                        bool isTitle = text.Height >= TitleMinHeight
                                       && !string.IsNullOrEmpty(text.FontName)
                                       && text.FontName.EndsWith("NeoSansIntelMedium");
                        if (isTitle)
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
                        // Not the title (footer / continuation header) — skip it entirely.
                        continue;
                    }
                }

                // Check for section headers. Headers are appended as "\n### X\n" (Python _header)
                // — note Append, not AppendLine, to avoid an extra trailing blank line.
                if (content == "Description" || content == "IA-32 Architecture Compatibility")
                {
                    state.TypeNext = "description";
                    sb.Append($"\n### {content}\n");
                }
                else if (content == "Instruction Operand Encoding")
                {
                    state.TypeNext = "encoding";
                    sb.Append($"\n### {content}\n");
                }
                else if (content == "Operation")
                {
                    // The ```java fence is opened lazily by the first body line (StartCode),
                    // exactly like the Python _start_code, so empty operation blocks never open.
                    // Operation alone gets an extra blank line after the header (Python `_header + '\n'`).
                    state.TypeNext = "operation";
                    sb.Append($"\n### {content}\n\n");
                }
                else if (content == "Flags Affected" || content == "FPU Flags Affected")
                {
                    CloseCode(sb, state);
                    state.TypeNext = "flags";
                    sb.Append($"\n### {content}\n");
                }
                else if (content == "Intel C/C++ Compiler Intrinsic Equivalent" ||
                         content == "C/C++ Compiler Intrinsic Equivalent")
                {
                    CloseCode(sb, state);
                    state.TypeNext = "intrinsics";
                    sb.Append($"\n### {content}\n");
                    StartCode(sb, state, "c");
                }
                else if (IsExceptionHeader(content))
                {
                    CloseCode(sb, state);
                    state.TypeNext = "exceptions";
                    sb.Append($"\n### {content}\n");
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
                            // A bold sub-heading inside Operation: close the current code block,
                            // emit a #### heading, reopen a fresh java block (Python _close_code /
                            // _start_code). content is the escaped/trimmed heading text.
                            CloseCode(sb, state);
                            sb.AppendLine($"\n#### {content}\n");
                            StartCode(sb, state, "java");
                        }
                        else
                        {
                            // Code line: open the java fence lazily, then emit the RAW (unescaped)
                            // text so '#'/'*' stay literal, mapping the Intel assignment operator
                            // (":=" in recent SDM revisions) to '←'. Indent comes from the x-position.
                            StartCode(sb, state, "java");
                            sb.Append(CreateIndent(text.X0));
                            sb.AppendLine(text.GetText().Replace(":=", "←"));
                        }
                    }
                    else if (state.Type == "intrinsics")
                    {
                        // Intrinsics is a ```c code block (opened by the section header) — keep raw text.
                        sb.AppendLine(text.GetText());
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
                        // The content is markdown-escaped, so a '#' exception code appears as "\#".
                        // Turn a leading exception code into "<p>#..." (Python replaces '\#' -> '<p>#'),
                        // but leave "(\#...)" parenthetical references alone. Replacing the full "\#"
                        // (not just '#') is what drops the stray backslash from the output.
                        if (content.Contains("\\#") && !content.Contains("(\\#"))
                        {
                            content = content.Replace("\\#", "<p>#");
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

        /// <summary>Opens a fenced code block if one isn't already open (Python _start_code).</summary>
        private static void StartCode(StringBuilder sb, MarkdownState state, string language)
        {
            if (!state.CodeMode)
            {
                sb.AppendLine("```" + language);
                state.CodeMode = true;
            }
        }

        /// <summary>
        /// Closes the current fenced code block if one is open (Python _close_code). Emits the
        /// closing fence WITHOUT a trailing newline — every caller follows it with text that
        /// begins with '\n' (a "\n### header" or "\n#### header"), which terminates the fence
        /// line. Adding a newline here would double it into a stray blank line.
        /// </summary>
        private static void CloseCode(StringBuilder sb, MarkdownState state)
        {
            if (state.CodeMode)
            {
                sb.Append("```");
                state.CodeMode = false;
            }
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
                string current = null;
                for (int pageNum = Math.Max(1, startPage); pageNum <= last; pageNum++)
                {
                    var piles = ParsePage(pdfDocument, pageNum);

                    string titled = piles.Select(p => p.GetInstruction().Mnemonic).FirstOrDefault(m => m != null);
                    if (titled != null)
                    {
                        current = titled;
                        allPiles.AddRange(piles);
                        continue;
                    }

                    string running = piles.Select(p => p.GetRunningTitleMnemonic()).FirstOrDefault(m => m != null);
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

    /// <summary>
    /// Generates markdown files from ContentPile objects.
    /// Equivalent to the Writer class in Python's writer.py.
    /// Handles instruction boundaries and opcode table merging.
    /// </summary>
    public class MarkdownGenerator
    {
        private readonly string _sourceInfo;
        private readonly string _outputDirectory;

        public MarkdownGenerator(string outputDirectory = "./output", string sourceInfo = "Intel® 64 and IA-32 Architectures Software Developer's Manual, Combined Volumes (Order Number 325462)")
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
        /// Finds whether the opcode table immediately preceding this one (with no title in between)
        /// is also an opcode table — i.e. the two should be merged into one HTML table.
        /// Stops at ANY title pile: a different instruction's title obviously ends the search, but
        /// so does the CURRENT instruction's own title — nothing before that belongs to this
        /// instruction. Without the latter stop the scan runs into the PREVIOUS instruction's
        /// opcode table and wrongly merges, dropping this table's header row and "&lt;table&gt;" tag
        /// (the FBLD/FDIV/... bug).
        /// </summary>
        private bool FindPreviousOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null)
                {
                    return false; // reached a title (this instruction's or another's) — no merge across it
                }

                if (pile.GetType() == "table")
                {
                    return pile.IsOpcodeTable();
                }
            }

            return false;
        }

        /// <summary>
        /// Finds whether the opcode table immediately following this one (no title in between) is
        /// also an opcode table — the symmetric partner of <see cref="FindPreviousOpcodeTable"/>.
        /// Stops at ANY title pile so an instruction's opcode table never merges with the NEXT
        /// instruction's table.
        /// </summary>
        private bool FindNextOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex + 1; j < piles.Count; j++)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null)
                {
                    return false; // reached a title — no merge across it
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

            // Every real instruction-reference page has an OPCODE table (its header cell reads
            // "Opcode" or "Opcode/Instruction"). A "title" without one is junk caught by the title
            // font: a section heading ("IA-32 Memory Models", "RTM — Enabled Debugger Support",
            // "VM — Exit Controls for MSRs"), an Appendix-B encoding table (header "Instruction and
            // Format"), or a cross-reference stub. Requiring "Opcode" also prevents an Appendix-B
            // page from OVERWRITING the real instruction's file (e.g. the real FYL2X is kept, the
            // appendix FYL2X table is dropped).
            if (!markdown.Contains("Opcode", StringComparison.Ordinal)) return;

            // Normalise to LF FIRST. StringBuilder.AppendLine emits CRLF on Windows, but the
            // CleanupHyphenation patterns are written with '\n' (e.g. "oper-\nands"); on CRLF text
            // they would never match and the broken words ("oper- ands") would survive. Also strip
            // any stray BOM/zero-width char. (Do NOT collapse blank runs — the reference puts two
            // blank lines before some section headers, e.g. "</table>\n\n\n### ...".)
            markdown = markdown.Replace("\r\n", "\n").Replace("﻿", "");

            // Re-join words the PDF hyphenated across a line break (curated list, as in the Python).
            markdown = TextCleaner.CleanupHyphenation(markdown);

            string safeFileName = instruction.Replace("/", "_").Replace(" ", "_");
            // Strip any remaining characters that are illegal in Windows file names
            // (e.g. a stray ':' '>' '*' from an oddly-formatted heading) to avoid IOExceptions.
            foreach (char bad in System.IO.Path.GetInvalidFileNameChars())
                safeFileName = safeFileName.Replace(bad, '_');
            string filePath = System.IO.Path.Combine(_outputDirectory, $"{safeFileName}.md");

            var now = DateTime.Now;
            string generatedTime = $"{now.Day}-{now.Month}-{now.Year}";
            markdown += $"\n --- \n<p align=\"right\"><i>Source: {_sourceInfo}<br>Generated: {generatedTime}</i></p>\n";

            // UTF-8 without a BOM (the reference files have no BOM).
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.WriteLine($"Writing {filePath}");
            File.WriteAllText(filePath, markdown, utf8NoBom);
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
        /// <summary>
        /// Words the Intel PDF splits across a line break where the hyphen is an artifact and must
        /// be DROPPED (a single word, e.g. "excep-tion" -> "exception"). Each entry is the broken
        /// word with its single hyphen at the split point. Real compounds that must KEEP their
        /// hyphen ("floating-point", "general-purpose", "64-bit", "machine-check") are deliberately
        /// NOT listed — the general rule in <see cref="CleanupHyphenation"/> handles those.
        /// Curated from the actual rev-091 SDM output (the Python tool kept an equivalent list).
        /// </summary>
        private static readonly string[] SplitWords =
        [
            // originally ported from the Python writer
            "addi-tional", "combina-tion", "compar-ison", "compar-isons", "corre-sponding",
            "documenta-tion", "destina-tion", "desti-nation", "infor-mation", "instruc-tions",
            "instruc-tion", "regis-ters", "regis-ter", "oper-ands", "preci-sion", "loca-tions",
            "loca-tion", "speci-fied", "unpre-dictable", "priv-ilege",
            // extended from the SDM (combined volumes) output
            "Soft-ware", "soft-ware", "hard-ware", "excep-tion", "excep-tions", "proces-sors",
            "pro-cessor", "inter-rupt", "inter-rupts", "inter-rupted", "Inter-rupt", "oper-ation",
            "oper-ations", "oper-ating", "oper-and", "oper-ates", "opera-tion", "opera-tions",
            "Archi-tectures", "archi-tecture", "archi-tectural", "architec-ture", "architec-tures",
            "architec-tural", "execu-tion", "perfor-mance", "Perfor-mance", "gener-ated",
            "gener-ates", "gener-ation", "gener-ations", "align-ment", "condition-ally",
            "condi-tionally", "respec-tively", "imme-diate", "immedi-ately", "spec-ified",
            "proce-dure", "proce-dures", "deter-mine", "deter-mined", "deter-mines", "moni-toring",
            "indi-cates", "indi-cated", "indi-cate", "avail-able", "appro-priate", "tech-nology",
            "Tech-nology", "interme-diate", "inter-mediate", "inte-gers", "environ-ment",
            "envi-ronment", "compar-ison", "transac-tional", "trans-actional", "struc-ture",
            "struc-tures", "recom-mended", "recom-mends", "other-wise", "func-tion", "func-tions",
            "expo-nent", "differ-ences", "attri-bute", "attri-butes", "write-mask", "double-word",
            "double-words", "quad-word", "unde-fined", "subse-quent", "reg-ister", "phys-ical",
            "partic-ular", "microarchi-tecture", "microarchitec-ture", "microar-chitecture",
            "mecha-nism", "mecha-nisms", "mech-anism", "initializa-tion", "initial-ized",
            "exten-sions", "exten-sion", "auto-matically", "applica-tion", "applica-tions",
            "appli-cation", "appli-cable", "under-flow", "over-flow", "over-flows", "refer-ences",
            "refer-ence", "incre-ments", "incre-mented", "incor-rect", "imple-mented",
            "imple-mentation", "imple-mentations", "condi-tions", "compo-nents", "compo-nent",
            "compati-bility", "associ-ated", "asso-ciated", "Optimi-zation", "optimi-zation",
            "optimiza-tions", "transi-tions", "transla-tion", "trans-lation", "signifi-cand",
            "repre-sented", "prop-erly", "opti-mized", "neces-sary", "modi-fied", "logi-cal",
            "depen-dent", "config-ured", "config-uration", "capa-bilities", "arith-metic",
            "Specifi-cally", "virtu-alization", "viola-tions", "subtrac-tion", "sema-phore",
            "prob-lems", "plat-form", "out-side", "magni-tude", "identifi-cation", "iden-tify",
            "illus-trated", "hier-archy", "granu-larity", "exec-utive", "distin-guished",
            "defini-tions", "defi-nition", "compu-tations", "circum-stances", "authenti-cated",
            "allo-cated", "accom-plished", "acces-sible", "There-fore", "band-width", "gath-ered",
            "inter-face", "Devel-oper",
            // added from a rev-091 corpus scan of the table-cell descriptions
            "ele-ments", "val-ues", "han-dle", "permit-ted", "per-mitted", "mes-sage", "descrip-tor",
            "mem-ory", "fea-ture", "sig-naling", "nonsig-naling", "nonsignal-ing", "ver-sion",
            "regis-ter", "regis-ters", "comput-ed", "comput-es", "select-ed", "spec-ifies",
            // mnemonic broken across a line in an opcode cell ("AES- ENCWIDE128KL")
            "AES-DECWIDE128KL", "AES-DECWIDE256KL", "AES-ENCWIDE128KL", "AES-ENCWIDE256KL",
        ];

        public static string CleanupHyphenation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Bullet points to markdown
            text = text.Replace("•\n\n", "\n * ");
            text = text.Replace("•\n", "\n * ");
            text = text.Replace("•", "\n * ");

            // A line break INSIDE a table cell renders a hyphenated word as "func- tions"
            // (hyphen + space) instead of the paragraph form "func-\ntions" (hyphen + newline),
            // because the cell joins its two visual lines with a space. Normalise that hyphen-space
            // to the hyphen-newline form so the curated SplitWords list and the general rule below
            // (which both expect "\n") handle cell text the same as paragraph text. Only a hyphen
            // tight against the preceding letter is a word break (real " - " dashes have a space on
            // BOTH sides and are not matched). The negative lookahead keeps elisions like
            // "16- or 32-bit" / "8- to 64-bit" intact (the continuation is a conjunction, not the
            // rest of the word).
            text = Regex.Replace(text, @"([A-Za-z0-9])- +(?!(?:or|and|to|nor)\b)([A-Za-z])", "$1-\n$2");

            // De-hyphenate the curated single-word splits: "excep-\ntion" -> "exception".
            foreach (string w in SplitWords)
            {
                int h = w.IndexOf('-');
                if (h <= 0) continue;
                string broken = string.Concat(w.AsSpan(0, h), "-\n", w.AsSpan(h + 1));
                string joined = string.Concat(w.AsSpan(0, h), w.AsSpan(h + 1));
                text = text.Replace(broken, joined);
            }

            // Footnote artifact: a trademark glyph (®/™) extracted as its own line in the middle
            // of a hyphenated word break ("Reg-\n®\nisters" -> "Registers"). A lone ®/™ on a line
            // is always noise (a real one is attached, e.g. "Intel®"), so drop the glyph and the
            // hyphen and rejoin the word.
            text = Regex.Replace(text, @"([A-Za-z])-\n[®™]\n([a-z])", "$1$2");

            // General rule for every remaining line-ending hyphen: pull the continuation up onto
            // the same line but KEEP the hyphen. This removes the rendered "foo- bar" space and
            // correctly preserves real compounds the curated list intentionally omits
            // ("floating-\npoint" -> "floating-point", "64-\nbit" -> "64-bit",
            // "general-\nprotection" -> "general-protection"). The lookahead keeps "16-\nor 32-bit"
            // as an elision (renders "16- or 32-bit").
            text = Regex.Replace(text, @"([A-Za-z0-9])-\n(?!(?:or|and|to|nor)\b)([A-Za-z0-9])", "$1-$2");

            // (The old explicit "single- precision" etc. fixes are now covered by the hyphen-space
            // normalisation above plus the general rule, which keeps the hyphen for those compounds.)

            // Edge case: extra space in an instruction alias
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
