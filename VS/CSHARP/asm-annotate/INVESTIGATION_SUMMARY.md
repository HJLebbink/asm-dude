# intel-doc-2-data Investigation Summary

## Discovery

During investigation of the asm-annotate data extraction pipeline, a **critical existing project** was discovered that bridges the gap between PDF parsing and AsmDude data files.

---

## The intel-doc-2-data Project

### What It Does
Converts wiki markdown files containing instruction documentation into AsmDude signature data files.

**Input**: Wiki markdown files (`asm-dude.wiki/doc/*.md`) - 709 files
**Output**: Signature files for AsmDude extension

### Current Data Flow
```
Instruction PDFs
    ↓
intel-doc-2-md (Python)
    ↓
Wiki Markdown Files (709 files)
    ↓
intel-doc-2-data (C# .NET 10.0)
    ↓
Signature files (format: MNEMONIC\tPARAMS\tARCH\tDESCRIPTION)
```

### Key Capabilities

#### 1. Wiki Markdown Parsing
- **Input Format** (example from ADD.md):
  ```html
  <b>ADD</b> — Add
  <table>
    <tr><td><b>Opcode</b></td><td><b>Instruction</b></td>...</tr>
    <tr><td>04 ib</td><td>ADD AL, imm8</td><td>I</td><td>Valid</td><td>Valid</td><td>Add imm8 to AL.</td></tr>
    ...
  </table>
  ```

- **Extraction**: Description text + HTML table structure

#### 2. Table Format Detection
Handles 3, 4, 5, and 6-column table variations:

| Format | Mnemonic Col | Arch Col | Description Col | Notes |
|--------|--------------|----------|-----------------|-------|
| 3 cols | 1 | -10 (inferred SMX) | 2 | Rare |
| 4 cols | 0 | -1 (inferred) | 3 | Uncommon |
| 5 cols | 0-1 | 3 or -1 | 4 | Common |
| 6 cols | 0-1 | 4 | 5 | Most common |

#### 3. Architecture Inference Algorithm
When architecture column is missing, infers from operand names:
- Contains `R64`, `REL64`, or `RCX` → **X64**
- Contains `R32`, `REL32`, `ECX`, or `M32` → **386**
- Contains `CMOV` + `R64` → **X64** (special case)
- Contains `CMOV` without `R64` → **P6** (special case)
- Default → **8086**

**Importance**: Intel PDFs often omit explicit architecture columns; this inference is critical.

#### 4. Parameter Normalization
Normalizes register names for consistency:
```
XMM1, XMM2, XMM3, XMM4  →  XMM
YMM1, YMM2, YMM3, YMM4  →  YMM
ZMM1, ZMM2, ZMM3        →  ZMM
K1, K2, K3              →  K
BND1, BND2              →  BND
R32A, R32B              →  R32
R64A, R64B              →  R64
M64, M32, etc.          →  Kept as-is
```

**Importance**: Creates consistent parameter syntax across all instructions.

#### 5. Description Cleanup
Normalizes terminology:
```
floating-point, floating- point      →  FP
double-precision, double- precision  →  DP
single-precision, single- precision  →  SP
```

#### 6. Mnemonic Extraction
Parses instruction strings like:
- `"ADD RAX, IMM32"` → Mnemonic: `ADD`, Params: `R64,IMM32`
- `"[MOV] R64, R64"` → Mnemonic: `MOV`, Params: `R64,R64` (handles bracket syntax)
- `"REP ADD RAX, IMM32"` → Mnemonic: `ADD` (strips prefixes)

---

## Code Architecture

### Main Parser Method
```csharp
static (string Description, IList<Signature> Signatures) Parse(string content)
{
    // 1. Extract description (text before <table>)
    // 2. Parse HTML table structure
    // 3. Detect table format (3, 4, 5, or 6 columns)
    // 4. Locate mnemonic, arch, description columns
    // 5. Convert rows to Signature objects
    // 6. Return (description, list of signatures)
}
```

### Data Structure
```csharp
struct Signature
{
    public Mnemonic mnemonic;              // ADD, MOV, etc.
    public string parameters;              // "R64,R64"
    public string parameter_descriptions;  // "ADD R64,R64"
    public IList<Arch> archs;             // [Arch.ARCH_X64, Arch.ARCH_386]
    public string description;            // "Add imm32 sign-extended to RAX"

    public override string ToString()
    {
        // Format: MNEMONIC\tPARAMS\tARCHES\tPARAM_DESCR\tDESCRIPTION
    }
}
```

### Output Format
**Signature file** (signature-dec2018.txt):
```
;--------- GENERAL Instructions
GENERAL	ADD	Add	ADD
ADD	R8,R8	X64,386	ADD R8,R8	Add AL, imm8
ADD	R8,IMM8	X64,386	ADD R8,IMM8	Add sign-extended imm8 to r/m8
ADD	R64,R64	X64	ADD R64,R64	Add imm32 sign-extended to RAX
```

---

## Integration Strategy for asm-annotate

### Phase 1: Extraction (Reuse)
Create **reusable library** from intel-doc-2-data code:
1. Extract `Signature` struct → `InstructionSignature` class
2. Extract parsing methods → `WikiMarkdownParser` class
3. Create `SignatureFileGenerator` from output logic
4. Create `WikiOverviewGenerator` for HTML tables

### Phase 2: Extension (New)
Create **new components** for asm-annotate:
1. **MarkdownGenerator** - Convert PDF content → wiki format
   - Input: `ContentPile` objects from PdfParser
   - Output: `.md` files matching wiki structure
   - Generate HTML tables with opcode, instruction, operands

2. **PerformanceMerger** - Combine signature + performance data
   - Input: Signatures + CSV/TSV performance metrics
   - Output: Enriched data files with latency/throughput

### Phase 3: Pipeline (Integration)
Connect all components:
```
PDF Files
    ↓
[PdfParser] (existing ContentPile, text cleaning)
    ↓
Parsed Content (TextElement, LineElement, ContentPile)
    ↓
[MarkdownGenerator] (NEW - generate wiki format)
    ↓
Wiki Markdown Files (.md)
    ↓
[WikiMarkdownParser] (EXTRACTED from intel-doc-2-data)
    ↓
InstructionSignature objects
    ↓
[SignatureFileGenerator] (EXTRACTED from intel-doc-2-data)
    ↓
Signature Files (MNEMONIC\tPARAMS\tARCH\tDESCRIPTION)
    ↓
[PerformanceMerger] (NEW - combine with CSV/TSV)
    ↓
Final Data Files for AsmDude
```

---

## Why This Discovery Matters

### 1. **Proven Logic**
intel-doc-2-data has been tested on 709 existing wiki files. The parsing algorithm is battle-tested and handles real-world variations.

### 2. **Table Format Coverage**
Shows all possible table format variations (3-6 columns) that will appear in Intel PDFs. Critical knowledge for PdfParser.

### 3. **Architecture Inference**
Demonstrates the algorithm for inferring architecture from operand names when PDFs don't provide explicit architecture columns.

### 4. **Parameter Normalization**
Shows exact register name transformations needed (XMM1→XMM, etc.) to create consistent instruction signatures.

### 5. **Avoid Reimplementation**
Eliminates need to rewrite 440 lines of proven, working code. Extract and reuse instead.

### 6. **Validation Baseline**
The 709 existing wiki files can be used as:
- **Input baseline**: Test WikiMarkdownParser against known files
- **Output target**: Regenerate markdown and compare signatures
- **Quality metric**: Ensure no regressions in coverage

---

## Action Items

### Immediate (Next Session)
- [ ] Review intel-doc-2-data/Program.cs (440 lines)
- [ ] Extract parsing logic into `WikiMarkdownParser.cs`
- [ ] Test WikiMarkdownParser against existing 709 wiki files
- [ ] Create unit tests for table format detection
- [ ] Create unit tests for architecture inference
- [ ] Create unit tests for parameter cleanup

### Short Term (2-3 Sessions)
- [ ] Implement `MarkdownGenerator` (PDF → wiki markdown)
- [ ] Create `SignatureFileGenerator` from extracted logic
- [ ] Test round-trip: PDF → Markdown → Signature
- [ ] Compare generated signatures against 709 baseline

### Medium Term (4-5 Sessions)
- [ ] Implement `PerformanceMerger` (signature + CSV/TSV)
- [ ] Integrate IntrinsicsGuideParser (XML parsing)
- [ ] Add support for modern architectures (Ice Lake, Raptor Lake)
- [ ] Full pipeline testing and validation

### Long Term (6+ Sessions)
- [ ] Refactor intel-doc-2-data to use shared library
- [ ] Extend for new data sources (XED, Intrinsics Guide)
- [ ] Regenerate entire 709-file wiki from modern PDFs
- [ ] Performance data updates for all supported architectures

---

## Files to Review

### New Documentation (Created This Session)
1. **PIPELINE_ANALYSIS.md**
   - Complete data flow diagram
   - intel-doc-2-data breakdown
   - File format examples
   - Architecture reference

2. **INTEL_DOC_2_DATA_INTEGRATION.md**
   - Refactoring strategy
   - Code extraction checklist
   - Integration timeline
   - Testing strategy

3. **IMPLEMENTATION_PLAN.md** (Updated)
   - Revised 6-phase plan
   - Includes intel-doc-2-data extraction phase
   - Updated next steps

### Source Code to Review
- `VS\CSHARP\intel-doc-2-data\Program.cs` (440 lines)
  - Lines 114-124: `Parse()` method
  - Lines 154-304: `To_Signature()` method
  - Lines 306-348: `Parse_Parameters()` method
  - Lines 350-365: `Cleanup_Parameters()` method
  - Lines 367-379: `Parse_Archs()` method

### Supporting Code (Already Completed)
- `asm-annotate\PdfParser.cs` (700 lines) - PDF extraction
- `asm-annotate\DataParsers.cs` (400 lines) - CSV/TSV parsing
- `asm-annotate\Program.cs` - Updated with parser tests

---

## Key Insights

### 1. The Pipeline is Longer Than Expected
```
Intel Docs → intel-doc-2-md → Wiki → intel-doc-2-data → Signatures
                                                           ↓
                                                        AsmDude
```

Our implementation needs to bridge from PDF directly to signatures.

### 2. Wiki Files are Intermediate Format
The 709 `.md` files are NOT the final output. They're an intermediate step:
- **Input** for intel-doc-2-data
- **Can be regenerated** from new PDFs
- **Provide validation baseline** (709 existing files)

### 3. Architecture Inference is Critical
Without explicit architecture columns, the PDFs rely on operand names to indicate architecture. The inference algorithm in intel-doc-2-data is the KEY to handling this.

### 4. Parameter Syntax is Standardized
Register normalization (XMM1→XMM, K1→K, etc.) is essential for:
- Creating consistent instruction signatures
- Matching patterns for performance data lookup
- Enabling instruction pattern recognition

### 5. Table Format Variations are Common
Different sections of Intel PDFs use different table layouts (3-6 columns). A robust parser must handle all variations.

---

## Risk Mitigation

### Risk: Losing Existing Functionality
**Mitigation**: Extract logic into library without removing from intel-doc-2-data. Both can coexist during refactoring.

### Risk: Architecture Inference Errors
**Mitigation**: Test inference algorithm against 709 existing wiki files before deploying.

### Risk: Table Format Edge Cases
**Mitigation**: Unit test all 4 table format variations independently.

### Risk: Parameter Normalization Issues
**Mitigation**: Validate against AsmTools enum values (Mnemonic, Arch, Register).

---

## Summary

The discovery of intel-doc-2-data **dramatically clarifies** the AsmDude data pipeline and provides **proven, tested logic** for wiki parsing. Rather than building from scratch, we should:

1. **Understand** the existing system (intel-doc-2-md → wiki → intel-doc-2-data)
2. **Extract** reusable components into a shared library
3. **Extend** with PDF generation and performance merging
4. **Integrate** into asm-annotate for a complete, modern pipeline

This approach:
- ✅ Reuses proven code
- ✅ Leverages 709 existing wiki files as validation baseline
- ✅ Maintains backward compatibility with intel-doc-2-data
- ✅ Enables complete PDF→Signature pipeline in asm-annotate
- ✅ Reduces implementation risk significantly

**Next Step**: Extract WikiMarkdownParser from intel-doc-2-data and test against existing 709 wiki files to validate the approach.
