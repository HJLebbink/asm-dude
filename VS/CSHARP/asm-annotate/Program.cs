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

                default:
                    Console.WriteLine("Usage: asm-annotate <command>");
                    Console.WriteLine("  check-latest          verify the local SDM PDF is the latest revision");
                    Console.WriteLine("  find <text>           locate the page(s) containing <text>");
                    Console.WriteLine("  dump <page>           print raw text/line elements for one page");
                    Console.WriteLine("  extract [start] [end] extract all instructions to .\\output");
                    return command.Length == 0 ? 0 : 1;
            }

            Console.WriteLine("\nDone!");
            return 0;
        }
    }
}
