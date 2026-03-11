# asm-fuzz TODO

## Missing Fuzzing Targets

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

- [ ] **SendSettings** — configuration changes (didChangeConfiguration)
  - Tests: `SendSettings(DidChangeConfigurationParams)` with various option changes
  - Edge cases: invalid settings, conflicting settings, settings applied mid-document
  - Target file: `Targets/SendSettingsTarget.cs`
  - Status: ⏳ Not started (lower priority)

### Document Changes (Medium Priority)

- [x] **didChange (incremental edits)** — partial text modifications
  - Tests: `UpdateServerSideTextDocument()` with various text edits
  - Edge cases: single-line edits, multi-line edits, empty documents, full replacements
  - Validates that incremental parsing works correctly
  - Target file: `Targets/DocumentChangeTarget.cs`
  - Status: ✅ Implemented

### Assembly Language Features (Medium Priority)

- [ ] **Different assembler syntaxes** — MASM, NASM, AT&T syntax parsing
  - Fuzz with different syntax variants in same document
  - Edge cases: mixed syntaxes, syntax-specific keywords
  - Target file: `Targets/AssemblerSyntaxTarget.cs`

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

### Running Fuzzer Targets

```bash
# Build the fuzzer
dotnet build VS/CSHARP/asm-fuzz/asm-fuzz.csproj

# Run a target with libfuzzer corpus
./asm-fuzz.exe <target_name>

# Run with custom seed file
./asm-fuzz.exe <target_name> seed_file.txt

# Run multiple targets sequentially
for target in parsememoperand parseline evaluateconstant documentpipeline labelgraph; do
  ./asm-fuzz.exe $target
done
```

### Corpus Management

Each target should have a `corpus/<target_name>/` directory with seed files:
- `corpus/documentpipeline/` — example .asm file snippets
- `corpus/labelgraph/` — files with label definitions and references
- `corpus/getdefinition/` — files with jump targets and label definitions
- etc.

### Crash Investigation

If libfuzzer finds a crash:
1. libfuzzer writes the crashing input to `crash-*` file
2. Reproduce: `./asm-fuzz.exe <target> crash-file`
3. Debug: `dotnet run --project asm-fuzz -- <target>` with Visual Studio debugger
4. Fix: Apply fix to production code, add crash file to corpus as regression test

---

## Implementation Notes

- Each target should limit input size to 4096 bytes (prevent OOM)
- Use `ServerFixture.GetServer()` for document-based targets (reuses singleton)
- Wrap LSP calls in try-catch to skip non-fatal errors (parsing should be robust)
- Document known bugs (e.g., ParseMemOperandTarget known bug at line 827)
