using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsmAnnotate;

namespace asm_annotate
{
    class Program
    {
        /// <summary>Directory holding the Intel source PDFs (relative to the working dir).</summary>
        private const string DataDir = @".\data";

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("asm-annotate: Intel instruction data extraction pipeline");
            Console.WriteLine("=========================================================\n");

            string command = args.Length > 0 ? args[0] : string.Empty;
            switch (command)
            {
                case "check-latest":
                {
                    // Verify the local Intel SDM PDF is the latest revision Intel publishes.
                    bool upToDate = await IntelDocChecker.CheckAndReportAsync(DataDir).ConfigureAwait(false);
                    Console.WriteLine("\nDone!");
                    return upToDate ? 0 : 1;
                }

                case "find":
                {
                    // find <text>  — locate the page(s) containing <text> in the local PDF.
                    (string? pdf, _) = IntelDocChecker.FindLocalPdf(DataDir);
                    if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                    string needle = string.Join(' ', args.Skip(1));
                    if (needle.Length == 0) { Console.WriteLine("Usage: find <text>"); return 1; }
                    Diagnostics.FindText(pdf, needle);
                    break;
                }

                case "dump":
                {
                    // dump <page>  — print raw text elements + extracted lines for one page.
                    (string? pdf, _) = IntelDocChecker.FindLocalPdf(DataDir);
                    if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                    if (args.Length < 2 || !int.TryParse(args[1], out int page)) { Console.WriteLine("Usage: dump <page>"); return 1; }
                    Diagnostics.DumpPage(pdf, page);
                    break;
                }

                case "extract":
                {
                    // extract [startPage] [endPage]  — run the full pipeline to ./output.
                    (string? pdf, int? rev) = IntelDocChecker.FindLocalPdf(DataDir);
                    if (pdf == null) { Console.WriteLine($"❌ No 325462-*.pdf in {DataDir}"); return 1; }
                    int start = (args.Length > 1 && int.TryParse(args[1], out int s)) ? s : 1;
                    int end = (args.Length > 2 && int.TryParse(args[2], out int e)) ? e : int.MaxValue;
                    Console.WriteLine($"Using {Path.GetFileName(pdf)} (revision {rev:000}).\n");
                    Extractor.Run(pdf, @".\output", start, end);
                    break;
                }

                case "perf-uops":
                {
                    // perf-uops <instructions.xml> [outputDir]  — convert uops.info XML to per-arch TSVs.
                    if (args.Length < 2) { Console.WriteLine("Usage: perf-uops <instructions.xml> [outputDir]"); return 1; }
                    string xml = args[1];
                    string outDir = args.Length > 2 ? args[2] : @".\output-perf";
                    return UopsInfoImporter.Run(xml, outDir);
                }

                case "gen-signatures":
                {
                    // gen-signatures [wikiDocDir] [outFile]  — turn the wiki's HTML opcode tables (stage 1
                    // output) into the AsmDude signature file (+ overview.txt + wiki Home.md). Stage 2.
                    string wikiDir = args.Length > 1 ? args[1] : "C:/Source/Github/asm-dude.wiki/doc";
                    string outFile = args.Length > 2 ? args[2]
                        : "C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-ls-lib/Resources/signature-mar2026.txt";
                    return SignatureGenerator.Run(wikiDir, outFile);
                }

                default:
                    Console.WriteLine("Usage: asm-annotate <command>");
                    Console.WriteLine("  check-latest            verify the local SDM PDF is the latest revision");
                    Console.WriteLine("  find <text>             locate the page(s) containing <text>");
                    Console.WriteLine("  dump <page>             print raw text/line elements for one page");
                    Console.WriteLine("  extract [start] [end]   extract all instructions to .\\output (stage 1: PDF->MD)");
                    Console.WriteLine("  gen-signatures [dir] [out] wiki MD -> signature file (stage 2: MD->TXT)");
                    Console.WriteLine("  perf-uops <xml> [out]   convert uops.info instructions.xml to per-arch perf TSVs");
                    return command.Length == 0 ? 0 : 1;
            }

            Console.WriteLine("\nDone!");
            return 0;
        }
    }
}
