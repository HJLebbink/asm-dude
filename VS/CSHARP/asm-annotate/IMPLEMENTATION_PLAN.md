# asm-annotate Implementation Plan

## Project Status

**Phase 1: Architecture & Foundations** - IN PROGRESS

### Completed

1. **PdfParser.cs** ✅ (Complete architecture, text cleaning implemented)
   - `PdfTextElement` - Represents text with coordinates
   - `PdfLineElement` - Represents vector lines (table borders)
   - `ContentPile` - Groups text/lines into logical tables/paragraphs
   - `TextCleaner` - 30+ hardcoded Intel PDF artifact patterns
   - `PdfDocumentParser` - Infrastructure for loading PDFs (itext7 integration)
   - **Status**: Core architecture complete; PDF content parsing TODOs marked

2. **DataParsers.cs** ✅ (Complete - base infrastructure)
   - `InstructionData` - Common data structure for all sources
   - `PerformanceData` - Unified performance metrics format
   - `InstructionDataParser` - Abstract base class for all parsers
   - `PerformanceTsvParser` - Parses TSV files (Haswell, Skylake, etc.)
   - `PerformanceCsvParser` - Parses CSV files (Ice Lake format)
   - `IntrinsicsGuideParser` - Placeholder for XML parsing (TODO)
   - `XedParser` - Placeholder for XED database parsing (TODO)
   - **Status**: Infrastructure complete, ready for implementations

3. **Project Configuration** ✅
   - Added itext7 v9.0.0 NuGet package for PDF manipulation
   - Both asm-annotate.csproj and full solution build successfully
   - No compilation errors

### Current TODOs

#### Phase 2: PDF Content Stream Parsing
- [ ] Implement `PdfPageTextExtractor.ExtractText()`
  - Requires: itext7 PdfContentStreamProcessor or custom PDF operator parser
  - Goal: Extract text elements with coordinate information
  - Similar to pdfminer's layout analysis approach

- [ ] Implement vector line extraction for table borders
  - Parse PDF "m" (moveto), "l" (lineto), "re" (rectangle), "S" (stroke) operators
  - Map graphics state (coordinates, stroke width, color)

- [ ] Test PdfDocumentParser with actual Intel SDM PDFs
  - Verify coordinate system (Y-axis direction, page origin)
  - Verify text stripping and hyphenation cleanup
  - Verify table detection via vertical lines

#### Phase 3: Data Source Parsers
- [ ] Complete `IntrinsicsGuideParser`
  - Parse Intel Intrinsics Guide XML file (from https://www.intel.com/content/www/us/en/docs/intrinsics-guide/)
  - Extract: instruction names, descriptions, parameters, intrinsic function names
  - Extract performance data by microarchitecture
  - **Data available for**: Haswell, Broadwell, Skylake, Skylake-X, Cascade Lake, Ice Lake, Rocket Lake, Alder Lake, Raptor Lake, Raptor Lake R, Arrow Lake, Lunar Lake

- [ ] Complete `XedParser`
  - Parse Intel XED database (from https://github.com/intelxed/xed)
  - Extract instruction definitions and encodings
  - Map instructions to operand patterns

- [ ] Implement PDF parser for Intel Software Developer Manuals
  - Use `PdfDocumentParser` to extract instruction definitions
  - Focus on instruction headers (mnemonic + em-dash separator)
  - Extract opcode tables for encoding information

#### Phase 4: Wiki & Signature Generation
- [ ] Extract `WikiMarkdownParser` from intel-doc-2-data
  - Reuse existing parsing logic (440-line Program.cs)
  - Handle table format variations (3, 4, 5, 6 columns)
  - Architecture inference from operand names
  - Parameter normalization

- [ ] Create `MarkdownGenerator`
  - Input: InstructionData from PdfParser
  - Output: Wiki-format markdown files (matching ADD.md structure)
  - Generate HTML tables with opcode, instruction, operands columns

- [ ] Create `SignatureFileGenerator`
  - Input: Parsed wiki files via WikiMarkdownParser
  - Output: `signature-*.txt` files for AsmDude
  - Format: MNEMONIC\tPARAMS\tARCH\tPARAM_DESCR\tDESCRIPTION

#### Phase 4b: Output Generators (Advanced)
- [ ] Create `PerformanceTsvGenerator`
  - Input: InstructionData list with Performance metrics
  - Output: `asm-dude2-ls-lib/Resources/Performance/*.tsv` files
  - Format: Tab-separated (Instruction, Operands, μops, Latency, Throughput, Comments)
  - Support multiple microarchitectures

- [ ] Create `WikiOverviewGenerator`
  - Input: InstructionData list from all sources
  - Output: HTML overview table (mnemonic, description, architectures)

#### Phase 5: Microarchitecture Coverage
- [ ] Priority 1: Extract modern architecture data (2019-2023)
  - [ ] Cascade Lake (Q1 2019)
  - [ ] Ice Lake (Q2 2020) - Partial data exists in icelake.csv
  - [ ] Tiger Lake (Q4 2020)
  - [ ] Rocket Lake (Q1 2021)
  - [ ] Alder Lake (Q4 2021)
  - [ ] Raptor Lake (Q4 2022)
  - [ ] Raptor Lake R (Q1 2023)

- [ ] Priority 2: Arrow Lake, Lunar Lake, Granite Rapids, Sierra Forest (2024+)

#### Phase 6: Data Validation & Testing
- [ ] Compare generated signature files with existing asm-dude.wiki (709 files)
- [ ] Validate performance data accuracy
  - Compare against Intel Intrinsics Guide
  - Verify no instruction regressions
- [ ] Test with actual VS extension
  - Verify hover information displays correctly
  - Verify semantic token highlighting still works
  - Verify code completion suggestions

---

## Data Source Integration Map

### Current Sources (Legacy - 2019 or earlier)
- ✅ **Haswell.tsv** through **SkylakeX.tsv** - Already in Resources/Performance/
- Already integrated into AsmDude - form baseline for comparison

### New Sources to Integrate
| Source | Parser | Format | Coverage | Status |
|--------|--------|--------|----------|--------|
| Intel Intrinsics Guide | `IntrinsicsGuideParser` | XML | Haswell through Lunar Lake | TODO |
| Intel SDM (PDF) | `PdfDocumentParser` | PDF | Cascade Lake onwards | TODO |
| XED Database | `XedParser` | Text files | Latest x86-64 + AVX-512 | TODO |
| icelake.csv | `PerformanceCsvParser` | CSV | Ice Lake only | Ready |

---

## Critical Implementation Notes

### PDF Parser
- itext7 API is complex; full content stream parsing requires custom operator listener
- Consider using `PdfContentStreamProcessor` with custom event handlers
- Y-coordinate direction must match PDF standard (Y increases upward)
- Text positioning requires tracking text matrix (Tm operator) and current position

### Performance Data Format
- icelake.csv has different schema than TSV files
  - CSV: iform, regsize, mask, throughput, latency
  - TSV: instruction, operands, fused_ops, unfused_ops, ports, latency, throughput, comments
  - Conversion needed to unify formats

### Testing Strategy
1. Unit tests for each parser (TSV, CSV, XML, PDF)
2. Integration tests combining multiple data sources
3. Comparison with existing wiki (709-file baseline)
4. Visual validation in VS extension (hover, completion)

---

## Build Status
- ✅ asm-annotate.csproj: builds successfully with 0 errors
- ✅ Full solution: builds successfully with 0 errors
- ⚠️ itext7 v9.0.0 resolved (requested v8.1.2+)

## Critical Discovery: intel-doc-2-data Project

**Location**: `VS/CSHARP/intel-doc-2-data/Program.cs` (440 lines)

**Purpose**: Converts wiki markdown files → signature files

**Key Insight**: This is the **missing link** in the pipeline:
```
PDF (PdfParser) → Wiki Markdown (MarkdownGenerator) →
  Signatures (intel-doc-2-data logic) → AsmDude
```

**Action Items**:
1. **Extract** parsing logic into reusable library (WikiMarkdownParser)
2. **Refactor** intel-doc-2-data to use the library
3. **Implement** MarkdownGenerator for PDF→Wiki conversion
4. **Integrate** into asm-annotate pipeline

See `INTEL_DOC_2_DATA_INTEGRATION.md` for detailed refactoring plan.

---

## Next Immediate Steps
1. **Extract intel-doc-2-data logic into WikiMarkdownParser.cs**
   - `Parse()` → `ParseContent()`
   - `To_Signature()` → Core conversion
   - `Parse_Parameters()` → Parameter extraction
   - Support all table format variations (3, 4, 5, 6 columns)

2. **Test WikiMarkdownParser with existing wiki files**
   - Verify against 709 existing .md files
   - Validate architecture inference
   - Test parameter cleanup

3. **Implement MarkdownGenerator**
   - Input: ContentPile objects
   - Output: Wiki-format markdown files

4. **Test round-trip**: PDF → Markdown → Signature → Verify

5. **Implement PerformanceMerger**
   - Combine signature data + CSV/TSV performance metrics
