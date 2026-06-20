// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
// Diagnostics + extraction driver for the Intel PDF -> markdown pipeline.
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

using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

using System;
using System.Collections.Generic;
using System.Linq;

namespace AsmAnnotate
{
    /// <summary>
    /// Small helpers used while bringing the C# extractor up to the quality of the
    /// Python reference output. None of this is part of the production data path —
    /// it's the "look at what itext actually gave us" toolbox.
    /// </summary>
    public static class Diagnostics
    {
        /// <summary>
        /// Scans the PDF page-by-page for <paramref name="search"/> (case-insensitive) and
        /// prints the page numbers where it occurs. Used to locate an instruction's page in a
        /// freshly downloaded revision (page numbers drift between revisions).
        /// </summary>
        public static void FindText(string pdfPath, string search, int maxHits = 25)
        {
            Console.WriteLine($"Searching for \"{search}\" ...");
            int hits = 0;
            using var pdf = new PdfDocument(new PdfReader(pdfPath));
            int pages = pdf.GetNumberOfPages();
            for (int p = 1; p <= pages && hits < maxHits; p++)
            {
                string text = PdfTextExtractor.GetTextFromPage(pdf.GetPage(p), new SimpleTextExtractionStrategy());
                if (text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Show the first matching line for context.
                    string? line = text.Split('\n').FirstOrDefault(l => l.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    Console.WriteLine($"  page {p,5}: {line?.Trim()}");
                    hits++;
                }
            }
            Console.WriteLine($"Done ({hits} hit(s), {pages} pages scanned).");
        }

        /// <summary>
        /// Dumps the raw text elements and snapped border lines itext produced for one page,
        /// so we can eyeball font/height/position granularity against pdfminer's behaviour.
        /// </summary>
        public static void DumpPage(string pdfPath, int pageNum)
        {
            var parser = new PdfDocumentParser(pdfPath);
            ContentPile pile = parser.ExtractRawPage(pageNum);

            Console.WriteLine($"=== Page {pageNum}: {pile.TextElements.Count} text elements, " +
                              $"{pile.VerticalLines.Count} vertical + {pile.HorizontalLines.Count} horizontal lines ===\n");

            Console.WriteLine("--- TEXT (top-to-bottom) ---");
            foreach (var t in pile.TextElements.OrderByDescending(t => t.Y1).ThenBy(t => t.X0))
            {
                string font = t.FontName ?? "";
                int dot = font.LastIndexOf('+');
                if (dot >= 0 && dot + 1 < font.Length) font = font[(dot + 1)..];
                Console.WriteLine($"  x0={t.X0,7:0.0} y0={t.Y0,7:0.0} y1={t.Y1,7:0.0} h={t.Height,5:0.0} [{font,-22}] {Truncate(t.GetText(), 70)}");
            }

            Console.WriteLine("\n--- VERTICAL LINES (x | y0..y1) ---");
            foreach (var v in pile.VerticalLines.OrderBy(v => v.X0))
                Console.WriteLine($"  x={v.X0,7:0.0}  y {v.Y0,7:0.0}..{v.Y1,7:0.0}");

            Console.WriteLine("\n--- HORIZONTAL LINES (y | x0..x1) ---");
            foreach (var h in pile.HorizontalLines.OrderByDescending(h => h.Y0))
                Console.WriteLine($"  y={h.Y0,7:0.0}  x {h.X0,7:0.0}..{h.X1,7:0.0}");
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    }

    /// <summary>
    /// Drives the full PDF -> per-instruction-markdown pipeline (PdfDocumentParser +
    /// MarkdownGenerator), optionally restricted to a page range for fast iteration.
    /// </summary>
    public static class Extractor
    {
        public static void Run(string pdfPath, string outputDir, int startPage, int endPage)
        {
            Console.WriteLine($"Extracting {System.IO.Path.GetFileName(pdfPath)} pages {startPage}..{(endPage == int.MaxValue ? "end" : endPage.ToString())}");
            Console.WriteLine($"Output dir: {outputDir}\n");

            var parser = new PdfDocumentParser(pdfPath);

            // Read the real document identity off the cover page so the per-file "Source:" footer
            // names the actual manual + revision + date that was extracted (not a hard-coded string).
            string sourceInfo = ReadSourceInfo(parser);
            Console.WriteLine($"Source: {sourceInfo}");

            IReadOnlyList<ContentPile> piles = parser.ParseDocument(startPage, endPage);
            Console.WriteLine($"Parsed {piles.Count} content piles.");

            var generator = new MarkdownGenerator(outputDir, sourceInfo);
            generator.Write(piles);

            int mdCount = System.IO.Directory.Exists(outputDir)
                ? System.IO.Directory.GetFiles(outputDir, "*.md").Length
                : 0;
            Console.WriteLine($"\nWrote {mdCount} markdown file(s) to {outputDir}");
        }

        /// <summary>
        /// Builds the "Source:" attribution from the PDF's cover page: the title, the
        /// "Order Number: 325462-NNNUS" line, and the "Month Year" date printed beneath it.
        /// Falls back to the bare manual name if the cover can't be parsed.
        /// </summary>
        private static string ReadSourceInfo(PdfDocumentParser parser)
        {
            const string fallback = "Intel® 64 and IA-32 Architectures Software Developer's Manual, Combined Volumes (Order Number 325462)";
            try
            {
                var lines = parser.ExtractRawPage(1).TextElements.Select(t => t.GetText()).ToList();

                var orderMatch = lines
                    .Select(s => System.Text.RegularExpressions.Regex.Match(s, @"325462-\d+US", System.Text.RegularExpressions.RegexOptions.None, System.TimeSpan.FromSeconds(2)))
                    .FirstOrDefault(m => m.Success);
                string orderNo = orderMatch is { Success: true } ? orderMatch.Value : "325462";

                string date = lines.FirstOrDefault(s => System.Text.RegularExpressions.Regex.IsMatch(
                    s.Trim(), @"^(January|February|March|April|May|June|July|August|September|October|November|December)\s+20\d\d$", System.Text.RegularExpressions.RegexOptions.None, System.TimeSpan.FromSeconds(2)))?.Trim() ?? "";

                string suffix = date.Length > 0 ? $", {date}" : "";
                return $"Intel® 64 and IA-32 Architectures Software Developer's Manual, Combined Volumes (Order Number {orderNo}{suffix})";
            }
            catch
            {
                return fallback;
            }
        }
    }
}
