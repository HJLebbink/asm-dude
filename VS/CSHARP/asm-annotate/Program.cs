using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsmAnnotate;

namespace asm_annotate
{
    class Program
    {
        /// <summary>Directory holding the Intel source PDFs/CSVs (relative to the working dir).</summary>
        private const string DataDir = @".\data";

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("asm-annotate: Intel instruction data extraction pipeline");
            Console.WriteLine("=========================================================\n");

            // Choose operation based on args
            if (args.Length > 0 && args[0] == "check-latest")
            {
                // Verify the local Intel SDM PDF is the latest revision Intel publishes.
                bool upToDate = await IntelDocChecker.CheckAndReportAsync(DataDir).ConfigureAwait(false);
                Console.WriteLine("\nDone!");
                return upToDate ? 0 : 1;
            }
            else if (args.Length > 0 && args[0] == "find")
            {
                // find <text>  — locate the page(s) containing <text> in the local PDF.
                (string? pdf, _) = IntelDocChecker.FindLocalPdf(DataDir);
                if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                string needle = string.Join(' ', args.Skip(1));
                if (needle.Length == 0) { Console.WriteLine("Usage: find <text>"); return 1; }
                Diagnostics.FindText(pdf, needle);
            }
            else if (args.Length > 1 && args[0] == "dump")
            {
                // dump <page>  — print raw text elements + extracted lines for one page.
                (string? pdf, _) = IntelDocChecker.FindLocalPdf(DataDir);
                if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                if (!int.TryParse(args[1], out int page)) { Console.WriteLine("Usage: dump <page>"); return 1; }
                Diagnostics.DumpPage(pdf, page);
            }
            else if (args.Length > 0 && args[0] == "extract")
            {
                // extract [startPage] [endPage]  — run the full pipeline to ./output.
                (string? pdf, int? rev) = IntelDocChecker.FindLocalPdf(DataDir);
                if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                int start = (args.Length > 1 && int.TryParse(args[1], out int s)) ? s : 1;
                int end = (args.Length > 2 && int.TryParse(args[2], out int e)) ? e : int.MaxValue;
                Console.WriteLine($"Using {Path.GetFileName(pdf)} (revision {rev:000}).\n");
                Extractor.Run(pdf, @".\output", start, end);
            }
            else if (args.Length > 0 && args[0] == "extract-aaa")
            {
                Console.WriteLine("DEMO MODE: Extracting AAA instruction from page 701\n");
                await ExtractAAA().ConfigureAwait(false);
            }
            else
            {
                // Test CSV parser with icelake.csv
                TestCsvParser();

                // Test TSV parser with Haswell.tsv (if available)
                // TestTsvParser();
            }

            Console.WriteLine("\nDone!");
            return 0;
        }

        static void TestCsvParser()
        {
            try
            {
                var csvPath = @".\data\icelake.csv";
                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"❌ CSV file not found: {csvPath}");
                    return;
                }

                Console.WriteLine($"Testing PerformanceCsvParser with {csvPath}...");
                var parser = new PerformanceCsvParser(csvPath, "IceLake");
                var instructions = parser.Parse();

                Console.WriteLine($"✅ Parsed {instructions.Count} instructions from CSV");

                // Show sample instructions
                int count = 0;
                foreach (var instr in instructions)
                {
                    if (count++ < 5)
                    {
                        Console.WriteLine($"  - {instr.Mnemonic}: {instr.InstructionForm}");
                        foreach (var perf in instr.Performance)
                        {
                            Console.WriteLine($"      {perf.Key}: Latency={perf.Value.Latency}, Throughput={perf.Value.Throughput}");
                        }
                    }
                }

                if (instructions.Count > 5)
                    Console.WriteLine($"  ... and {instructions.Count - 5} more");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"   Details: {ex.InnerException.Message}");
            }
        }

        static void TestTsvParser()
        {
            try
            {
                var tsvPath = @".\Resources\Performance\Haswell.tsv";
                if (!File.Exists(tsvPath))
                {
                    Console.WriteLine($"⚠️  TSV file not found: {tsvPath}");
                    return;
                }

                Console.WriteLine($"\nTesting PerformanceTsvParser with {tsvPath}...");
                var parser = new PerformanceTsvParser(tsvPath, "Haswell");
                var instructions = parser.Parse();

                Console.WriteLine($"✅ Parsed {instructions.Count} instructions from TSV");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static async Task ExtractAAA()
        {
            // Locate the newest local combined-volumes SDM PDF instead of hard-coding a revision.
            (string? pdfPath, int? localRev) = IntelDocChecker.FindLocalPdf(DataDir);

            if (pdfPath == null || !File.Exists(pdfPath))
            {
                Console.WriteLine($"❌ No Intel SDM PDF (325462-*.pdf) found in {DataDir}");
                Console.WriteLine($"   Download the latest: {IntelDocChecker.LatestCombinedVolumesRedirectUrl}");
                return;
            }

            Console.WriteLine($"Using {Path.GetFileName(pdfPath)} (revision {localRev:000}).");

            // Warn (but don't block) if a newer revision is available.
            IntelDocChecker.CheckResult check = await IntelDocChecker.CheckAsync(DataDir).ConfigureAwait(false);
            if (check.Error != null)
                Console.WriteLine($"⚠️  Could not verify latest revision online: {check.Error}");
            else if (!check.IsUpToDate)
                Console.WriteLine($"⚠️  A newer revision exists (local {check.LocalRevision:000} < latest {check.LatestRevision:000}). Run 'check-latest' to update.");
            else
                Console.WriteLine("✅ Confirmed latest revision.");
            Console.WriteLine();

            try
            {
                var extractor = new PdfExtractorTool(pdfPath);

                // Analyze AAA instruction on page 701
                extractor.AnalyzeAAAPage();

                // Extract surrounding pages for context
                Console.WriteLine("\n---\n");
                Console.WriteLine("Extracting pages 700-702 for context...\n");
                extractor.ExtractPageRange(700, 702, @".\extracted_pages");

                // Generate AAA.md demo
                Console.WriteLine("\n---\n");
                Console.WriteLine("Generating AAA.md demo...\n");
                extractor.GenerateAAA_md(@".\AAA_demo.md");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"   Details: {ex.InnerException.Message}");
            }
        }
    }
}
