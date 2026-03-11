using System;
using System.IO;
using AsmAnnotate;

namespace asm_annotate
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("asm-annotate: Intel instruction data extraction pipeline");
            Console.WriteLine("=========================================================\n");

            // Choose operation based on args
            if (args.Length > 0 && args[0] == "extract-aaa")
            {
                Console.WriteLine("DEMO MODE: Extracting AAA instruction from page 701\n");
                ExtractAAA();
            }
            else
            {
                // Test CSV parser with icelake.csv
                TestCsvParser();

                // Test TSV parser with Haswell.tsv (if available)
                // TestTsvParser();
            }

            Console.WriteLine("\nDone!");
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

        static void ExtractAAA()
        {
            const string pdfPath = @".\data\325462-090-sdm-vol-1-2abcd-3abcd-4.pdf";

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"❌ PDF not found: {pdfPath}");
                Console.WriteLine("Expected: asm-annotate/data/325462-090-sdm-vol-1-2abcd-3abcd-4.pdf");
                return;
            }

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
