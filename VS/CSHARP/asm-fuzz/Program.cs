using AsmFuzz;

using SharpFuzz;

if (args.Length == 0)
{
    Console.WriteLine("Usage: asm-fuzz <target>");
    Console.WriteLine();
    Console.WriteLine("Available targets:");
    Console.WriteLine();
    Console.WriteLine("Core parsing (tier 1):");
    Console.WriteLine("  parsememoperand    - AsmSourceTools.Parse_Mem_Operand");
    Console.WriteLine("  parseline          - AsmSourceTools.ParseLine");
    Console.WriteLine("  parsemnemonic      - AsmSourceTools.ParseMnemonic");
    Console.WriteLine("  evaluateconstant   - ExpressionEvaluator.Parse_Constant");
    Console.WriteLine("  splitintokeywords  - AsmSourceTools.SplitIntoKeywordsType");
    Console.WriteLine("  operand            - new Operand(input)");
    Console.WriteLine("  multisyntax        - AsmSourceTools.ParseLine across MASM/NASM/AT&T dialects");
    Console.WriteLine();
    Console.WriteLine("LSP Protocol (tier 2 - critical path):");
    Console.WriteLine("  documentpipeline   - Full LS document pipeline (open, hover, completion, etc.)");
    Console.WriteLine("  getdefinition      - LS.GetDefinition (jump to label)");
    Console.WriteLine("  sendreferences     - LS.SendReferences (find references)");
    Console.WriteLine("  getdocumentsymbols - LS.GetDocumentSymbols (outline/breadcrumb)");
    Console.WriteLine("  getdocumenthighlights - LS.GetDocumentHighlights (highlight all occurrences)");
    Console.WriteLine("  getcodeactions     - LS.GetCodeActions (quick fixes)");
    Console.WriteLine("  getcodelenses      - LS.GetCodeLenses (reference badges)");
    Console.WriteLine("  documentchange     - LS.OnTextDocumentChanged (incremental edits)");
    Console.WriteLine("  settings           - SettingsManager.DeserializeSettings (settings.json contract)");
    Console.WriteLine();
    Console.WriteLine("Semantic analysis (tier 2):");
    Console.WriteLine("  labelgraph         - LabelGraph construction and diagnostics");
    Console.WriteLine();
    Console.WriteLine("Run modes:");
    Console.WriteLine("  <target>                       libFuzzer entry point (single target; launched by libfuzzer-dotnet)");
    Console.WriteLine("  fuzz-all [opts]                COVERAGE-GUIDED round-robin over all targets (instruments + drives libfuzzer-dotnet)");
    Console.WriteLine("  campaign <target|all> [opts]   in-process mutational campaign — runnable from Visual Studio (F5), no external tools");
    Console.WriteLine("  replay <target> <file>         run one input with no catch — to debug a crash in the VS debugger");
    Console.WriteLine();
    Console.WriteLine("  fuzz-all opts: --seconds N (per target, default 600) --passes P (0=loop) --workers N (parallel, default 8; 1=live console) --only t1,t2 --bin DIR --libfuzzer PATH --skip-instrument");
    Console.WriteLine("  campaign opts: --seconds N (per target, default 30) --seed N --corpus DIR --out DIR --stop-on-first --loop");
    return 0;
}

string command = args[0].ToLowerInvariant();

// Coverage-guided round-robin orchestrator (shells out to sharpfuzz + libfuzzer-dotnet).
if (command == "fuzz-all")
{
    return FuzzAll.Run(args[1..]);
}

// In-process modes (no instrumentation / libfuzzer-dotnet needed — usable straight from Visual Studio).
if (command == "campaign")
{
    return Campaign.Run(args[1..]);
}

if (command == "replay")
{
    return Campaign.Replay(args[1..]);
}

// Otherwise treat args[0] as a target name and hand off to the libFuzzer harness (launched per target
// by libfuzzer-dotnet; see the fuzz-all command for the coverage-guided driver).
if (!FuzzTargets.All.TryGetValue(command, out var run))
{
    Console.Error.WriteLine($"Unknown target or command: {command}");
    return 2;
}

Fuzzer.LibFuzzer.Run(data => run(data));
return 0;
