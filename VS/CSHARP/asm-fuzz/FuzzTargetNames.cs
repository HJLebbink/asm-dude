namespace AsmFuzz;

/// <summary>
/// Canonical list of fuzz-target names, as plain strings with NO delegate references.
/// <para>
/// This is deliberately separate from <see cref="FuzzTargets"/> (the name→delegate registry): the
/// <c>fuzz-all</c> orchestrator must enumerate targets WITHOUT triggering <see cref="FuzzTargets"/>'s
/// static initializer, because that creates delegates into <c>asm-tools-lib</c>/<c>asm-dude2-ls-lib</c>
/// and would load (hence file-lock) those DLLs in the orchestrator process — which then couldn't
/// instrument them with <c>sharpfuzz</c>. A unit test asserts this list stays in sync with
/// <see cref="FuzzTargets.All"/>.
/// </para>
/// </summary>
public static class FuzzTargetNames
{
    public static readonly string[] All =
    [
        // Core parsing (tier 1)
        "parsememoperand", "parseline", "parsemnemonic", "evaluateconstant", "splitintokeywords", "operand", "multisyntax", "classifyconsistency",

        // LSP Protocol (tier 2)
        "documentpipeline", "getdefinition", "sendreferences", "getdocumentsymbols", "getdocumenthighlights", "getcodeactions", "getcodelenses", "documentchange", "multidocisolation", "settings",

        // Semantic analysis (tier 2)
        "labelgraph",
    ];

    /// <summary>
    /// The managed DLLs instrumented for coverage before a coverage-guided run. <c>asm-tools-lib</c>
    /// backs every parser target; <c>asm-dude2-ls-lib</c> backs the LS-tier targets; <c>asm-options-lib</c>
    /// carries the <c>ColorJsonConverter</c> exercised by the <c>settings</c> target. Instrumenting all
    /// three up front (rather than per-target) is harmless — coverage from a DLL a given target never
    /// loads simply never fires — and avoids a stale per-target instrumentation list.
    /// </summary>
    public static readonly string[] LibrariesToInstrument =
    [
        "asm-tools-lib.dll",
        "asm-dude2-ls-lib.dll",
        "asm-options-lib.dll",
    ];
}
