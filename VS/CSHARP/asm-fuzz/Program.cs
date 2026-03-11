using AsmFuzz.Targets;
using SharpFuzz;

if (args.Length == 0)
{
    Console.WriteLine("Usage: asm-fuzz <target>");
    Console.WriteLine();
    Console.WriteLine("Available targets:");
    Console.WriteLine();
    Console.WriteLine("Core parsing (tier 1):");
    Console.WriteLine("  parsememoperand    - AsmSourceTools.Parse_Mem_Operand (known bug at line 827)");
    Console.WriteLine("  parseline          - AsmSourceTools.ParseLine");
    Console.WriteLine("  parsemnemonic      - AsmSourceTools.ParseMnemonic");
    Console.WriteLine("  evaluateconstant   - ExpressionEvaluator.Parse_Constant");
    Console.WriteLine("  splitintokeywords  - AsmSourceTools.SplitIntoKeywordsType");
    Console.WriteLine("  operand            - new Operand(input)");
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
    Console.WriteLine();
    Console.WriteLine("Semantic analysis (tier 2):");
    Console.WriteLine("  labelgraph         - LabelGraph construction and diagnostics");
    return;
}

string target = args[0].ToLowerInvariant();

Fuzzer.LibFuzzer.Run(target switch
{
    // Core parsing
    "parsememoperand" => ParseMemOperandTarget.Run,
    "parseline" => ParseLineTarget.Run,
    "parsemnemonic" => ParseMnemonicTarget.Run,
    "evaluateconstant" => EvaluateConstantTarget.Run,
    "splitintokeywords" => SplitIntoKeywordsTarget.Run,
    "operand" => OperandTarget.Run,

    // LSP Protocol (critical path)
    "documentpipeline" => DocumentPipelineTarget.Run,
    "getdefinition" => GetDefinitionTarget.Run,
    "sendreferences" => SendReferencesTarget.Run,
    "getdocumentsymbols" => GetDocumentSymbolsTarget.Run,
    "getdocumenthighlights" => GetDocumentHighlightsTarget.Run,
    "getcodeactions" => GetCodeActionsTarget.Run,
    "getcodelenses" => GetCodeLensesTarget.Run,
    "documentchange" => DocumentChangeTarget.Run,

    // Semantic analysis
    "labelgraph" => LabelGraphTarget.Run,

    _ => throw new ArgumentException($"Unknown target: {target}"),
});
