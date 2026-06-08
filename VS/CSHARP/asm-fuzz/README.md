# asm-fuzz

Coverage-guided (SharpFuzz + libFuzzer) and in-process fuzzing for the AsmDude2 parser and language
server. See the **"Running the fuzzer"** and **"Reading libfuzzer-dotnet output"** sections below for
how to run it (including from Visual Studio) and how to interpret the results.

**Invariants.** Beyond "doesn't crash", targets assert structural **output invariants** (no oracle
needed) — see `Invariants.cs` and the family checklist in `INVARIANT-TAXONOMY.md`. These have found
**four** real bugs the crash-oracle could not (color ARGB range, a close-time state leak, a tokenizer
overlap, and a `SendReferences` NRE). Eleven families are now asserted:
- **CONS + MONO** — token spans from `SplitIntoKeywordsType`/`ParseLine` stay within the line *and* are
  emitted in order without overlap (`splitintokeywords`, `parseline`). *(Found+fixed a spurious
  overlapping token on remark lines.)*
- **CONS + MONO** — the LSP semantic-token array is well-formed: multiple-of-5, in-document, sorted,
  non-overlapping (`documentpipeline`).
- **CONS** — every position-bearing result points within the document: folding ranges, diagnostics,
  hover, inlay hints (`documentpipeline`), document highlights, definition, references, code lenses,
  document symbols (their targets), and label-graph definitions (`labelgraph`). *(Surfaced a
  `SendReferences` NRE in the no-RPC server.)*
- **DUAL** — `close` is the inverse of `open`: no per-document server state remains after close (all
  open/close targets). *(Found+fixed a folding-range/assembler-type leak.)*
- **IDEM** — two fresh servers opening identical text produce identical semantic tokens (`documentpipeline`).
- **HOM** — `ParseLine` classification depends on the line text only, not on the `lineNumber`/`fileID`
  arguments (which are merely packed into each `KeywordID`) — catches cross-line state bleed (`parseline`).
- **COMM** — opening a second document on a distinct URI leaves the first document's outputs unchanged;
  the only multi-document target, underpinning per-input isolation (`multidocisolation`).
- **BND** — the `Global_MaxFileLines` cap is honored (analysis switches off at/over the cap), and a run
  of edits to one document never grows its tracked per-document footprint (`labelgraph`, `documentchange`).
- **RES** — reaching content X by an *edit* observes the same outputs (lines, semantic tokens, folding)
  as a fresh server that *opened* X. The two paths are distinct code (`OnTextDocumentOpened` vs the
  debounced `UpdateServerSideTextDocument`), so this catches stale residue from pre-edit content; the
  edit is driven through the synchronous `ApplyEditForTest` seam (`documentchange`).
- **NEG** — invalid settings JSON yields only `JsonException` (`settings`); the register/mnemonic
  predicate (`IsRn`/`IsMnemonic`) agrees with the value-parser (`ParseRn`/`ParseMnemonic`) and with
  `ToRn` — bad input is never classified as valid (`classifyconsistency`).
- **TERM** — bounded time (the libFuzzer/campaign timeout).

Skipped as tautological / high-cost: a mnemonic round-trip (`ParseMnemonic`'s table is built from
`Mnemonic.ToString()`); **ORACLE** (would need a reference tokenizer); **SYM** (metamorphic relabeling).

## Fuzzing Targets — status

### LSP Protocol Methods (High Priority)

- [x] **GetDefinition** — jump to label definition
  - Tests: `GetDefinition(TextDocumentPositionParams)` at various positions
  - Edge cases: undefined labels, multiple definitions, position boundaries
  - Target file: `Targets/GetDefinitionTarget.cs`
  - Status: ✅ Implemented

- [x] **SendReferences** — find all label references
  - Tests: `SendReferences(ReferenceParams)` for label usage tracking
  - Edge cases: self-references, unused labels, clashing labels
  - Target file: `Targets/SendReferencesTarget.cs`
  - Status: ✅ Implemented

- [x] **GetDocumentSymbols** — outline/breadcrumb for labels and procedures
  - Tests: `GetDocumentSymbols(DocumentSymbolParams)` for full document symbols
  - Edge cases: nested symbols, symbols at end of file, empty document
  - Target file: `Targets/GetDocumentSymbolsTarget.cs`
  - Status: ✅ Implemented

- [x] **GetDocumentHighlights** — highlight all occurrences of label on current line
  - Tests: `GetDocumentHighlights(IProgress, Position, uri, token)` at various positions
  - Edge cases: position at end of line, non-existent label, multiple occurrences
  - Target file: `Targets/GetDocumentHighlightsTarget.cs`
  - Status: ✅ Implemented

- [x] **GetCodeActions** — quick fixes (quick actions)
  - Tests: `GetCodeActions(CodeActionParams)` for code actions at position
  - Edge cases: no actions available, position at comment, position at directive
  - Target file: `Targets/GetCodeActionsTarget.cs`
  - Status: ✅ Implemented

- [x] **GetCodeLenses** — reference count badges above labels
  - Tests: `GetCodeLenses(CodeLensParams)` for code lens requests
  - Edge cases: empty document, document with only comments, large documents
  - Target file: `Targets/GetCodeLensesTarget.cs`
  - Status: ✅ Implemented

- [x] **settings** — settings.json deserialization contract (didChangeConfiguration path)
  - Tests: `SettingsManager.DeserializeSettings(json)` (the real settings.json → options path,
    incl. `ColorJsonConverter`) on arbitrary bytes; only `JsonException` (malformed JSON) is expected
  - Edge cases: empty object, null, partial objects, malformed colors, deep nesting
  - Target file: `Targets/SettingsTarget.cs`
  - Status: ✅ Implemented

### Document Changes (Medium Priority)

- [x] **didChange (incremental edits)** — partial text modifications
  - Tests: `UpdateServerSideTextDocument()` with various text edits
  - Edge cases: single-line edits, multi-line edits, empty documents, full replacements
  - Validates that incremental parsing works correctly
  - Target file: `Targets/DocumentChangeTarget.cs`
  - Status: ✅ Implemented

### Assembly Language Features (Medium Priority)

- [x] **multisyntax** — MASM, NASM Intel, NASM AT&T line parsing
  - Parses each fuzzed line under all three concrete `AssemblerEnum` dialects via
    `AsmSourceTools.ParseLine`, plus `ParseAssembler` on the raw input; no swallowing (any throw = bug)
  - Edge cases: mixed syntaxes, syntax-specific keywords, AT&T `%`/`$` sigils
  - Target file: `Targets/MultiSyntaxTarget.cs`
  - Status: ✅ Implemented

- [ ] **Architecture-specific parsing** — x86, x64, AVX, AVX-512
  - Fuzz with different ARCH flags enabled/disabled
  - Tests: instruction validation varies by architecture
  - Target file: `Targets/ArchitectureSpecificTarget.cs`

- [ ] **Register name edge cases** — abbreviated vs. full register names
  - Fuzz: EAX vs RAX vs AX vs AL, R8 vs R8B vs R8W vs R8D
  - Edge cases: invalid register combinations, size mismatches
  - Target file: `Targets/RegisterEdgeCasesTarget.cs`

### Arithmetic & Constants (Low Priority)

- [ ] **Complex expression evaluation** — nested parentheses, operator precedence
  - Fuzz: nested `(1 + (2 * (3 - 4)))`
  - Edge cases: missing operands, operator chaining, mixed bases
  - Extends current `evaluateconstant` target
  - Target file: `Targets/ComplexExpressionTarget.cs`

- [ ] **Numeric boundary testing** — overflow, underflow, large constants
  - Fuzz: `0xFFFFFFFF`, `-2147483648`, `999999999999`
  - Tests: ExpressionEvaluator truncation and validation
  - Target file: `Targets/NumericBoundaryTarget.cs`

### Diagnostic Edge Cases (Low Priority)

- [ ] **Multiple diagnostics per line** — clashing labels + undefined labels + syntax errors
  - Fuzz: lines with multiple diagnostic triggers
  - Validates diagnostic aggregation and de-duplication
  - Target file: `Targets/MultipleDiagnosticsTarget.cs`

---

## Completed Targets

✅ **parsememoperand** — `AsmSourceTools.Parse_Mem_Operand()`
✅ **parseline** — `AsmSourceTools.ParseLine()`
✅ **evaluateconstant** — `ExpressionEvaluator.Parse_Constant()`
✅ **splitintokeywords** — `AsmSourceTools.SplitIntoKeywordsType()`
✅ **operand** — `new Operand(input)` constructor
✅ **parsemnemonic** — `AsmSourceTools.ParseMnemonic()`
✅ **documentpipeline** — Full LSP document lifecycle
✅ **labelgraph** — `LabelGraph` construction and diagnostics
✅ **classifyconsistency** — register/mnemonic primitive-classifier consistency (NEG)
✅ **multidocisolation** — two open documents on distinct URIs don't interfere (COMM)

---

## Testing Strategy

### Coverage Tiers

1. **Tier 1 (Critical Path)** — LSP methods that are frequently used
   - Priority: GetDefinition, SendReferences, GetDocumentSymbols
   - These are core IDE navigation features

2. **Tier 2 (Document State)** — Features that modify or depend on document state
   - Priority: didChange, SendSettings
   - These test state management across multiple requests

3. **Tier 3 (Edge Cases)** — Assembly language and numeric edge cases
   - Priority: AssemblerSyntax, NumericBoundary, ArchitectureSpecific
   - These stress-test parsing robustness

### Running the fuzzer (4 modes)

`asm-fuzz` is a multi-command CLI (see `Program.cs`):

```bash
# COVERAGE-GUIDED round-robin over all targets (instruments DLLs + drives libfuzzer-dotnet).
# Needs: `dotnet tool install --global SharpFuzz.CommandLine` and libfuzzer-dotnet on PATH.
dotnet run -c Release --project VS/CSHARP/asm-fuzz -- fuzz-all --seconds 600
#   --workers N : parallel libfuzzer-dotnet processes per target (default 8; 1 = single + live console).
#                 N>1 output goes to fuzz-logs/<target>/fuzz-*.log, not the live console.
#   By DEFAULT, fuzz-all first COMPACTS each target's corpus (libFuzzer -merge) to a much smaller set
#   with the same coverage, then fuzzes — without this the persistent corpus grows unbounded across runs
#   (it once reached ~470k files / 850 MB). NOTE: -merge is greedy/order-dependent, so the result is
#   coverage-preserving but NOT minimal (re-running trims a little more each pass). --no-merge skips it.
#   also: --only t1,t2  --passes N  --skip-instrument  --libfuzzer <path>

# COMPACT-ONLY: reduce every corpus to a smaller coverage-preserving set and exit (no fuzzing).
# Run this if the corpus has bloated; it instruments the DLLs then -merges each corpus in place.
dotnet run -c Release --project VS/CSHARP/asm-fuzz -- fuzz-all --merge-only

# IN-PROCESS mutational campaign — no external tools, runs from Visual Studio (F5). DUMB (no
# coverage feedback): good for shallow bugs / smoke / triage, NOT deep states.
dotnet run --project VS/CSHARP/asm-fuzz -- campaign all --seconds 60        # or a single <target>

# REPLAY one input with NO exception handling — debugger breaks on the throwing line.
dotnet run --project VS/CSHARP/asm-fuzz -- replay <target> crashes/<target>/crash-xxxx.bin

# Single libFuzzer target (this is what fuzz-all launches per target under libfuzzer-dotnet):
dotnet run -c Release --project VS/CSHARP/asm-fuzz -- <target>
```

From **Visual Studio**: set `asm-fuzz` as startup project and pick a profile from the Run dropdown
(`Properties/launchSettings.json` defines `fuzz-all`, `campaign all`, and per-target campaigns), then F5.

### Reading libfuzzer-dotnet output — watch `ft`, NOT `cov`

⚠️ **`cov:` stays pinned at ~2 in a SharpFuzz/libfuzzer-dotnet run and that is CORRECT — do not
diagnose it as broken instrumentation.** `cov:` counts only the *native* inline-8-bit counters of
`libfuzzer-dotnet.exe`'s own C stub (the startup banner shows `Loaded 1 modules (66 inline 8-bit
counters)`), of which ~2 fire every iteration. Your .NET coverage is fed through the
`65536 Extra Counters` table that SharpFuzz fills from the instrumented DLLs, and libFuzzer folds
that into **`ft:` (features)**. So:
- **`ft` rising = real .NET coverage being discovered** (the metric that matters).
- **`ft` plateauing = coverage saturating** for that target/corpus/dict (healthy late-stage behaviour),
  not a stall.
- `cov: 2` flat is expected; ignore it.
- The startup `WARNING: Failed to find function "__sanitizer_*"` lines are benign (libfuzzer-dotnet
  doesn't implement those native hooks; SharpFuzz handles managed crashes itself).

To push `ft` higher (deeper coverage), enrich the seed `corpus/<target>/` and `asm.dict` — not the code.

### Corpus Management

Each target has a `corpus/<target_name>/` directory with seed files; libFuzzer also writes
coverage-increasing inputs back there, so the corpus persists across runs. Coverage-guided fuzzing keeps
*every* coverage-increasing input, so without periodic minimization the corpus grows unbounded (it once
reached ~470k files / 850 MB, most of them redundant). `fuzz-all` therefore **compacts each corpus with
`-merge` to a much smaller coverage-preserving set by default before fuzzing** — see `--no-merge` /
`--merge-only` above. `-merge` is greedy and order-dependent, so the result preserves coverage but is **not
minimal**. The compaction needs the DLLs instrumented (it runs as part of `fuzz-all`, after
instrumentation), so don't run it against a freshly `dotnet build`-ed (un-instrumented) bin or it would
collapse the corpus to ~1 input per target. The generated files are SHA1-named (40 hex) and git-ignored;
only curated named seeds are tracked.

**Don't loop `--merge-only` chasing a "minimal" corpus.** A convergence experiment (6 passes, 473,837 →
13,552 files) showed two distinct behaviours:
- **Parser targets converge to a true fixed point** (after ~5 passes a re-merge is a no-op:
  `parseline 939→939`, `multisyntax 1379→1379`). For these the corpus is minimal-under-greedy.
- **LS targets never converge** — they shed ~15–25% *every* pass. The cause is **non-deterministic
  coverage** in the LS server (async AsmSim, the 100 ms debounce, hash/dict iteration order,
  GC-sensitive paths): each merge run observes a slightly different edge map, so there is no stable
  minimal set. Past the first pass, continued merging of these targets does not "minimize" — it
  **erodes** the corpus, discarding inputs whose unique coverage simply didn't reproduce that run, which
  *hurts* future fuzzing. So merge **once per run** (the default) and leave it; don't re-run `--merge-only`
  on LS targets to "shrink it more". (This noisy-coverage property also makes coverage-guided fuzzing of
  the LS targets less efficient — the `ft:` signal is jittery — and is worth investigating separately. It
  does **not** contradict the IDEM invariant, which checks semantic-token determinism specifically; that
  output is deterministic, it's the *other* LS code paths whose coverage is noisy.)

### Crash Investigation

If a crash is found (libFuzzer writes `crash-*`; `campaign` writes `crashes/<target>/crash-*.bin`):
1. Reproduce/debug in VS: `replay <target> <crash-file>` (no catch → debugger breaks on the throw).
2. Fix production code, then drop the crash file into `corpus/<target>/` as a regression seed.

---

## Implementation Notes

- Each target caps input size via `FuzzLimits.MaxInputLength` (prevent OOM)
- Use `ServerFixture.CreateServer()` for document-based targets (fresh server per input for crash reproducibility); dispose via `using var`
- LSP targets catch only expected exception types via `FuzzGuard.IsExpected` and rethrow the rest,
  so the fuzzer still surfaces real parser/LS bugs (see `FuzzGuard.cs`)
