using AsmFuzz.Targets;
using SharpFuzz;

if (args.Length == 0)
{
    Console.WriteLine("Usage: asm-fuzz <target>");
    Console.WriteLine();
    Console.WriteLine("Available targets:");
    Console.WriteLine("  parsememoperand    - AsmSourceTools.Parse_Mem_Operand (known bug at line 827)");
    Console.WriteLine("  parseline          - AsmSourceTools.ParseLine");
    Console.WriteLine("  evaluateconstant   - ExpressionEvaluator.Parse_Constant");
    Console.WriteLine("  splitintokeywords  - AsmSourceTools.SplitIntoKeywordsType");
    Console.WriteLine("  operand            - new Operand(input)");
    Console.WriteLine("  parsemnemonic      - AsmSourceTools.ParseMnemonic");
    Console.WriteLine("  documentpipeline   - Full LS document pipeline (open, hover, completion, etc.)");
    Console.WriteLine("  labelgraph         - LabelGraph construction and diagnostics");
    return;
}

string target = args[0].ToLowerInvariant();

Fuzzer.LibFuzzer.Run(target switch
{
    "parsememoperand" => ParseMemOperandTarget.Run,
    "parseline" => ParseLineTarget.Run,
    "evaluateconstant" => EvaluateConstantTarget.Run,
    "splitintokeywords" => SplitIntoKeywordsTarget.Run,
    "operand" => OperandTarget.Run,
    "parsemnemonic" => ParseMnemonicTarget.Run,
    "documentpipeline" => DocumentPipelineTarget.Run,
    "labelgraph" => LabelGraphTarget.Run,
    _ => throw new ArgumentException($"Unknown target: {target}"),
});
