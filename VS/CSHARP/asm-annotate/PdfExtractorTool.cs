// Simple tool to extract and analyze AAA instruction from PDF
// This demonstrates the extraction flow for PdfParser.cs implementation

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace AsmAnnotate
{
    /// <summary>
    /// Simple text extraction tool for analyzing PDF structure.
    /// Demonstrates how to extract pages and text for AAA instruction.
    /// </summary>
    public class PdfExtractorTool
    {
        private readonly string _pdfPath;

        public PdfExtractorTool(string pdfPath)
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException($"PDF not found: {pdfPath}");
            _pdfPath = pdfPath;
        }

        /// <summary>
        /// Extract raw text from a specific page for analysis.
        /// </summary>
        public string ExtractPageText(int pageNumber)
        {
            try
            {
                using (var pdfDoc = new PdfDocument(new PdfReader(_pdfPath)))
                {
                    if (pageNumber < 1 || pageNumber > pdfDoc.GetNumberOfPages())
                        throw new ArgumentException($"Invalid page number: {pageNumber}");

                    var page = pdfDoc.GetPage(pageNumber);
                    var strategy = new SimpleTextExtractionStrategy();
                    var text = PdfTextExtractor.GetTextFromPage(page, strategy);

                    return text;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error extracting page {pageNumber}", ex);
            }
        }

        /// <summary>
        /// Extract text from page range (for checking multiple pages around AAA).
        /// </summary>
        public void ExtractPageRange(int startPage, int endPage, string outputDir)
        {
            Console.WriteLine($"Extracting pages {startPage}-{endPage}...");

            try
            {
                Directory.CreateDirectory(outputDir);

                using (var pdfDoc = new PdfDocument(new PdfReader(_pdfPath)))
                {
                    for (int pageNum = startPage; pageNum <= endPage && pageNum <= pdfDoc.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        var strategy = new SimpleTextExtractionStrategy();
                        var text = PdfTextExtractor.GetTextFromPage(page, strategy);

                        var outputPath = Path.Combine(outputDir, $"page_{pageNum:0000}.txt");
                        File.WriteAllText(outputPath, text, Encoding.UTF8);

                        Console.WriteLine($"  ✓ Page {pageNum}: {text.Length} characters");

                        // Show first 500 chars as preview
                        if (text.Length > 500)
                        {
                            Console.WriteLine($"    Preview: {text.Substring(0, 500)}...\n");
                        }
                        else
                        {
                            Console.WriteLine($"    Content: {text}\n");
                        }
                    }
                }

                Console.WriteLine($"Extracted {endPage - startPage + 1} pages to {outputDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Analyze page 701 specifically for AAA instruction.
        /// </summary>
        public void AnalyzeAAAPage()
        {
            Console.WriteLine("Analyzing page 701 (AAA instruction)...\n");

            try
            {
                var text = ExtractPageText(701);

                // Look for key patterns
                Console.WriteLine("=== PAGE 701 CONTENT ANALYSIS ===\n");

                // Check for AAA instruction header
                if (text.Contains("AAA") && text.Contains("ASCII"))
                {
                    Console.WriteLine("✓ Found AAA instruction");

                    // Extract lines containing AAA
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (line.Contains("AAA"))
                        {
                            Console.WriteLine($"  >> {line.Trim()}");
                        }
                    }
                }

                // Check for em-dash or en-dash (instruction header indicator)
                if (text.Contains("—"))
                {
                    Console.WriteLine("\n✓ Found em-dash (—) - likely instruction header");
                }
                if (text.Contains("–"))
                {
                    Console.WriteLine("\n✓ Found en-dash (–) - alternative instruction header");
                }

                // Check for table headers
                if (text.Contains("Opcode"))
                {
                    Console.WriteLine("✓ Found 'Opcode' - likely instruction table");
                }
                if (text.Contains("Instruction"))
                {
                    Console.WriteLine("✓ Found 'Instruction' - table header");
                }

                // Check for section headers
                var sectionHeaders = new[] { "Description", "Flags Affected", "Exceptions", "Operation" };
                foreach (var header in sectionHeaders)
                {
                    if (text.Contains(header))
                    {
                        Console.WriteLine($"✓ Found '{header}' section");
                    }
                }

                Console.WriteLine($"\n=== SUMMARY ===");
                Console.WriteLine($"Page length: {text.Length} characters");
                Console.WriteLine($"Line count: {text.Split('\n').Length}");

                // Show raw text for manual inspection
                Console.WriteLine($"\n=== RAW TEXT (First 2000 chars) ===\n");
                Console.WriteLine(text.Substring(0, Math.Min(2000, text.Length)));

                if (text.Length > 2000)
                {
                    Console.WriteLine($"\n... ({text.Length - 2000} more characters)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generate AAA.md by parsing page 701 structure.
        /// This is a simplified version - real implementation would use PdfParser.
        /// </summary>
        public void GenerateAAA_md(string outputPath)
        {
            Console.WriteLine($"Generating AAA.md...");

            try
            {
                var text = ExtractPageText(701);

                var sb = new StringBuilder();

                // Parse instruction header
                // Look for pattern: "AAA — ASCII Adjust for Add"
                var headerMatch = System.Text.RegularExpressions.Regex.Match(text, @"AAA\s+[—–]\s+(.+?)[\r\n]");
                if (headerMatch.Success)
                {
                    var description = headerMatch.Groups[1].Value.Trim();
                    sb.AppendLine($"<b>AAA</b> — {description}");
                }
                else
                {
                    sb.AppendLine("<b>AAA</b> — ASCII Adjust for Add");
                }

                // Add table placeholder (simplified)
                sb.AppendLine("<table>");
                sb.AppendLine("\t<tr>");
                sb.AppendLine("\t\t<td><b>Opcode</b></td>");
                sb.AppendLine("\t\t<td><b>Instruction</b></td>");
                sb.AppendLine("\t\t<td><b>Op/En</b></td>");
                sb.AppendLine("\t\t<td><b>64-Bit Mode</b></td>");
                sb.AppendLine("\t\t<td><b>Compat/Leg Mode</b></td>");
                sb.AppendLine("\t\t<td><b>Description</b></td>");
                sb.AppendLine("\t</tr>");
                sb.AppendLine("\t<tr>");
                sb.AppendLine("\t\t<td>37</td>");
                sb.AppendLine("\t\t<td>AAA</td>");
                sb.AppendLine("\t\t<td>NP</td>");
                sb.AppendLine("\t\t<td>Valid</td>");
                sb.AppendLine("\t\t<td>Valid</td>");
                sb.AppendLine("\t\t<td>ASCII Adjust for Add</td>");
                sb.AppendLine("\t</tr>");
                sb.AppendLine("</table>");
                sb.AppendLine();

                // Extract description section
                var descriptionMatch = System.Text.RegularExpressions.Regex.Match(text, @"Description(.*?)(?=Flags Affected|Operation|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (descriptionMatch.Success)
                {
                    sb.AppendLine("### Description");
                    sb.AppendLine();
                    sb.AppendLine(descriptionMatch.Groups[1].Value.Trim());
                    sb.AppendLine();
                }

                // Extract flags section
                var flagsMatch = System.Text.RegularExpressions.Regex.Match(text, @"Flags Affected(.*?)(?=Exceptions|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (flagsMatch.Success)
                {
                    sb.AppendLine("### Flags Affected");
                    sb.AppendLine();
                    sb.AppendLine(flagsMatch.Groups[1].Value.Trim());
                    sb.AppendLine();
                }

                // Save file
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

                Console.WriteLine($"✓ Generated {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                throw;
            }
        }
    }

}
