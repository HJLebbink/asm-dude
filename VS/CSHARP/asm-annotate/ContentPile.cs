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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AsmAnnotate
{

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
    /// <summary>The kind of content a <see cref="ContentPile"/> represents.</summary>
    public enum PileKind
    {
        Table,
        Image,
        Paragraph,
    }


    public class ContentPile
    {
        // Search distances for snapping lines to nearby lines
        private const double SEARCH_DISTANCE_VERTICAL = 1.0;
        private const double SEARCH_DISTANCE_HORIZONTAL = 8.0;

        // Mutable backing buffers — filled while parsing/splitting a page. Exposed read-only
        // below; external callers append via the Add* methods. Private members are type-scoped,
        // so the split/find helpers can fill another pile's buffers directly (e.g. table.verticalLines_).
        private readonly List<PdfLineElement> verticalLines_ = [];
        private readonly List<PdfLineElement> horizontalLines_ = [];
        private readonly List<PdfTextElement> textElements_ = [];
        private readonly List<object> images_ = []; // Placeholder for image support

        public IReadOnlyList<PdfLineElement> VerticalLines => this.verticalLines_;
        public IReadOnlyList<PdfLineElement> HorizontalLines => this.horizontalLines_;
        public IReadOnlyList<PdfTextElement> TextElements => this.textElements_;
        public IReadOnlyList<object> Images => this.images_; // Placeholder for image support

        /// <summary>Appends grouped text lines to this pile (used by the page parser).</summary>
        public void AddTextElements(IEnumerable<PdfTextElement> elements) => this.textElements_.AddRange(elements);

        /// <summary>
        /// The kind of this pile, inferred from its content (lines =&gt; table, images =&gt; image,
        /// otherwise paragraph). Replaces the former <c>GetType()</c>, which shadowed
        /// <see cref="object.GetType()"/> and returned magic strings.
        /// </summary>
        public PileKind Kind
        {
            get
            {
                if (VerticalLines.Count > 0) return PileKind.Table;
                if (Images.Count > 0) return PileKind.Image;
                return PileKind.Paragraph;
            }
        }

        /// <summary>
        /// Detects if this is an opcode table by checking the first cell text
        /// </summary>
        public bool IsOpcodeTable()
        {
            if (Kind != PileKind.Table) return false;
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
                    this.verticalLines_.Add(new PdfLineElement
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
                    this.horizontalLines_.Add(new PdfLineElement
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
        public (string? Mnemonic, string? Description) GetInstruction()
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
                    return (ExpandCompactMnemonic(mnemonic), LowercaseTitleConjunctions(descr));
            }

            return (null, null);
        }

        /// <summary>
        /// In an instruction title the conjunctions "With"/"Without" are written lowercase
        /// ("Multiply and Add ... Bytes With and Without Saturation" -> "... Bytes with and without
        /// Saturation"), matching how the SDM lower-cases them elsewhere ("Pack with Signed
        /// Saturation"). Only MID-title occurrences are changed; a leading word keeps its capital.
        /// </summary>
        internal static string LowercaseTitleConjunctions(string title)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                title,
                @"(?<=\S )\b(?:Without|With)\b",
                m => m.Value.ToLowerInvariant(),
                System.Text.RegularExpressions.RegexOptions.None,
                System.TimeSpan.FromSeconds(2));
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
        public string? GetRunningTitleMnemonic()
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
            t = Regex.Replace(t, "x[0-9]+", "", RegexOptions.None, System.TimeSpan.FromSeconds(2));  // EVEX tuple width (x4/x8/x16)
            // single-letter operand placeholder after a space/start, before a slash/comma/end
            // ("INT n/INTO" -> "INT/INTO"); a real prose word ("and", "in") is longer and survives.
            t = Regex.Replace(t, @"(?:^|(?<=[/,A-Z0-9])) [a-z](?=[/,]|$)", "", RegexOptions.None, System.TimeSpan.FromSeconds(2));

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
        public void ParsePageLayout(IReadOnlyList<object> layoutObjects)
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
                    this.textElements_.Add(element);
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
                AdjustToClose(line, this.verticalLines_, SEARCH_DISTANCE_VERTICAL);
                this.verticalLines_.Add(line);
            }
            else if (line.IsHorizontal)
            {
                AdjustToClose(line, this.horizontalLines_, SEARCH_DISTANCE_HORIZONTAL);
                this.horizontalLines_.Add(line);
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
                    AdjustToClose(line, this.verticalLines_, SEARCH_DISTANCE_VERTICAL);
                    this.verticalLines_.Add(line);
                }
                else if (line.IsHorizontal)
                {
                    AdjustToClose(line, this.horizontalLines_, SEARCH_DISTANCE_HORIZONTAL);
                    this.horizontalLines_.Add(line);
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
        private static void AdjustToClose(PdfLineElement line, IReadOnlyList<PdfLineElement> existingLines, double searchDistance)
        {
            PdfLineElement? closest = null;
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
        public IReadOnlyList<ContentPile> SplitIntoPiles()
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
                table.verticalLines_.AddRange(nearVerticals);
                table.horizontalLines_.AddRange(includedHorizontals);
                table.textElements_.AddRange(includedTexts);

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
                        paragraphs[idx].textElements_.Add(text);
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
                pile.images_.Add(image);
                images.Add(pile);
            }
            return images;
        }

        /// <summary>
        /// Returns the first text element in this pile, or null if empty.
        /// Used for sorting piles by position.
        /// </summary>
        private PdfTextElement? GetFirstElement()
        {
            if (TextElements.Count > 0) return TextElements[0];
            return null; // Images don't have a PdfTextElement
        }

        /// <summary>
        /// Generates markdown for this pile based on its type (table or paragraph).
        /// </summary>
        public string GenerateMarkdown(MarkdownState state)
        {
            return this.Kind switch
            {
                PileKind.Paragraph => GenerateParagraphMarkdown(state),
                PileKind.Table => GenerateTableMarkdown(state),
                _ => throw new InvalidOperationException($"Unsupported markdown type: {this.Kind}"),
            };
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

                    var (newRight, colspan) = FindExistCoor(bottom, top, colIdx, verticalCoords, true);
                    var (newBottom, rowspan) = FindExistCoor(left, right, rowIdx, horizontalCoords, false);

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
        private static List<double> CalcCoordinates(IReadOnlyList<PdfLineElement> lines, bool isVertical)
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
        private static bool IsInRange(double left, double top, double right, double bottom, PdfTextElement obj)
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
            bool leftExists = LineExists(left, bottom, top, true);
            bool topExists = LineExists(top, left, right, false);
            return !leftExists || !topExists;
        }

        /// <summary>
        /// Finds the next coordinate where a line exists, and returns span count.
        /// </summary>
        private (double Coordinate, int Span) FindExistCoor(double minimum, double maximum, int startIdx, List<double> lineCoords, bool isVertical)
        {
            int span = 0;
            bool lineExists = false;

            while (!lineExists)
            {
                span++;
                if ((startIdx + span) < lineCoords.Count)
                {
                    double coor = lineCoords[startIdx + span];
                    lineExists = LineExists(coor, minimum, maximum, isVertical);
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
        private bool LineExists(double target, double minimum, double maximum, bool isVertical)
        {
            var lines = isVertical ? VerticalLines : HorizontalLines;

            foreach (var line in lines)
            {
                double lineCoord = isVertical ? line.X0 : line.Y0;
                if (Math.Abs(lineCoord - target) > 0.01) // Use small epsilon for double comparison
                    continue;

                if (isVertical)
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
        private static string IntermediateToMarkdown(List<List<Dictionary<string, object>>> intermediate, MarkdownState state)
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

        private static bool IsExceptionHeader(string content)
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

        private static string CreateIndent(double xPos)
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

        private static (double Top, double Bottom) CalcTopBottom(IReadOnlyList<PdfLineElement> objects)
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

        private static (double Top, double Bottom) CalcTopBottom(IReadOnlyList<PdfTextElement> objects)
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

        private static bool IsOverlap(double top, double bottom, PdfLineElement obj)
        {
            const double searchDistance = 0.7;
            return ((bottom - searchDistance) <= obj.Y0 && obj.Y0 <= (top + searchDistance)) ||
                   ((bottom - searchDistance) <= obj.Y1 && obj.Y1 <= (top + searchDistance));
        }

        private static List<T> FindIncluded<T>(double top, double bottom, IReadOnlyList<T> objects) where T : class
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

        private static bool IsOverlap(double top, double bottom, PdfTextElement obj)
        {
            const double searchDistance = 0.7;
            return ((bottom - searchDistance) <= obj.Y0 && obj.Y0 <= (top + searchDistance)) ||
                   ((bottom - searchDistance) <= obj.Y1 && obj.Y1 <= (top + searchDistance));
        }
    }
}
