using AsmFuzz.Targets;

namespace AsmFuzz;

/// <summary>Signature of a libFuzzer entry point: consume one fuzz input.</summary>
public delegate void FuzzTarget(ReadOnlySpan<byte> data);

/// <summary>
/// Single source of truth mapping target name → entry point. Used by both the CLI dispatch
/// (<c>Program.cs</c>) and the corpus smoke test (<c>asm-fuzz-tests</c>), so a renamed or removed
/// target is a compile/test failure instead of silent rot.
/// </summary>
public static class FuzzTargets
{
    public static readonly IReadOnlyDictionary<string, FuzzTarget> All =
        new Dictionary<string, FuzzTarget>(StringComparer.Ordinal)
        {
            // Core parsing (tier 1)
            ["parsememoperand"] = ParseMemOperandTarget.Run,
            ["parseline"] = ParseLineTarget.Run,
            ["parsemnemonic"] = ParseMnemonicTarget.Run,
            ["evaluateconstant"] = EvaluateConstantTarget.Run,
            ["splitintokeywords"] = SplitIntoKeywordsTarget.Run,
            ["operand"] = OperandTarget.Run,
            ["multisyntax"] = MultiSyntaxTarget.Run,
            ["classifyconsistency"] = ClassifyConsistencyTarget.Run,

            // LSP Protocol (tier 2 - critical path)
            ["documentpipeline"] = DocumentPipelineTarget.Run,
            ["getdefinition"] = GetDefinitionTarget.Run,
            ["sendreferences"] = SendReferencesTarget.Run,
            ["getdocumentsymbols"] = GetDocumentSymbolsTarget.Run,
            ["getdocumenthighlights"] = GetDocumentHighlightsTarget.Run,
            ["getcodeactions"] = GetCodeActionsTarget.Run,
            ["getcodelenses"] = GetCodeLensesTarget.Run,
            ["documentchange"] = DocumentChangeTarget.Run,
            ["multidocisolation"] = MultiDocIsolationTarget.Run,
            ["settings"] = SettingsTarget.Run,

            // Semantic analysis (tier 2)
            ["labelgraph"] = LabelGraphTarget.Run,
        };
}
