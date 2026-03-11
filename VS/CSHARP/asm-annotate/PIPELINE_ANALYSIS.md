# AsmDude Data Pipeline Analysis

## Complete Data Processing Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                     DATA FLOW ARCHITECTURE                       │
└─────────────────────────────────────────────────────────────────┘

Input: Intel PDF Documents (Software Developer Manuals)
        ↓
┌──────────────────────────────────┐
│  PdfParser (asm-annotate)        │ [DONE: Architecture + text cleaning]
│  - TextElement extraction         │ [TODO: Content stream parsing]
│  - LineElement extraction         │
│  - ContentPile grouping           │
│  - Hyphenation cleanup (30+ patterns)
└──────────────────────────────────┘
        ↓
┌──────────────────────────────────┐
│  MarkdownGenerator (TODO)         │ [Convert parsed data → wiki format]
│  - Create .md files               │
│  - Generate HTML tables           │
│  - Match existing wiki structure  │
└──────────────────────────────────┘
        ↓
┌──────────────────────────────────┐
│  Wiki Markdown Files (709+)       │ [Already exists: asm-dude.wiki/doc/]
│  - One file per instruction       │ [Format: <b>MNEMONIC</b> — description]
│  - HTML table format              │ [Table: Opcode|Instruction|Operands|...]
└──────────────────────────────────┘
        ↓
┌──────────────────────────────────┐
│  DataConverter (intel-doc-2-data) │ [Parses wiki .md files]
│  - Parse markdown tables          │ [DONE: C# implementation exists]
│  - Extract signatures             │ [Extract: Mnemonic, Params, Arch]
│  - Generate signature files       │
└──────────────────────────────────┘
        ↓
┌──────────────────────────────────┐
│  Performance Data Sources         │ [CSV/TSV files]
│  - icelake.csv (638 instructions) │ [DONE: Parser implemented]
│  - Haswell.tsv - SkylakeX.tsv    │ [DONE: Parser implemented]
│  - Intrinsics Guide XML           │ [TODO: Parser to implement]
└──────────────────────────────────┘
        ↓
┌──────────────────────────────────┐
│  PerformanceMerger (TODO)         │ [Merge signature + performance data]
│  - Combine all data sources       │
│  - Resolve duplicates             │
│  - Output unified TSV files       │
└──────────────────────────────────┘
        ↓
Output: Data Files for AsmDude
  - Signature files: signature-*.txt
  - Performance: Haswell.tsv, IceLake.tsv, etc.
  - Wiki markdown: 709+ .md files (regenerated)
```

---

## Project Roles & Responsibilities

### intel-doc-2-md (Python)
**Purpose**: Extract instruction definitions from Intel PDF documents
**Input**: PDF files (Intel Software Developer Manuals)
**Output**: Markdown files in wiki format
**Status**: Existing Python implementation; C# port partially done
**Key Features**:
- PDF text extraction using pdfminer
- Layout analysis (paragraphs vs tables)
- Instruction header detection (font-based)
- Markdown generation with HTML tables

### intel-doc-2-data (C#)
**Purpose**: Convert wiki markdown files to signature data
**Input**: Wiki markdown files (709+ files from asm-dude.wiki/doc/)
**Output**:
  - Signature file: `signature-*.txt` (mnemonic, parameters, architecture, description)
  - HTML overview table
  - Architecture support mapping
**Status**: Complete and working
**Key Components**:
- `Parse()` - Extract description and table from markdown
- `To_Signature()` - Convert table rows to Signature structs
  - Detects table format (3, 4, 5, or 6 columns)
  - Locates mnemonic, operands, architecture, description columns
  - Infers architecture from operand names if not explicit (R64→X64, R32→386, etc.)
- `Parse_Parameters()` - Extract mnemonic and parameter syntax from instruction column
- `Cleanup_Parameters()` - Normalize register names (XMM1→XMM, K1→K, etc.)
- `Parse_Table()` - Parse HTML table structure

### asm-annotate (C# - NEW)
**Purpose**: Unified data extraction and conversion pipeline
**Components**:
1. **PdfParser.cs** - PDF extraction (DONE)
2. **DataParsers.cs** - CSV/TSV parsing (DONE)
3. **MarkdownGenerator.cs** - Generate wiki format (TODO)
4. **SignatureGenerator.cs** - Reuse intel-doc-2-data logic (TODO)
5. **PerformanceMerger.cs** - Combine all sources (TODO)

---

## intel-doc-2-data: Detailed Analysis

### Data Structures

```csharp
struct Signature
{
    public Mnemonic mnemonic;           // Enum value (ADD, MOV, etc.)
    public string parameters;            // e.g., "R64,R64" or "R/M64,IMM32"
    public string parameter_descriptions; // e.g., "ADD R64,R64"
    public IList<Arch> archs;           // [Arch.ARCH_X64, Arch.ARCH_386]
    public string description;          // e.g., "Add imm32 sign-extended..."

    public override string ToString()
    {
        // Format: MNEMONIC\tPARAMS\tARCHES\tPARAM_DESCR\tDESCRIPTION
        // Example: ADD\tR64,R64\tX64,386\tADD R64,R64\tAdd value in R64...
    }
}
```

### Table Format Detection

Supports 3, 4, 5, or 6 column table layouts:

| Columns | Header | Mnemonic Col | Arch Col | Description Col |
|---------|--------|--------------|----------|-----------------|
| 3 | Varies | 1 | -10 (SMX) | 2 |
| 4 | Varies | 0 | -1 (inferred) | 3 |
| 5 | [Instruction?] | 0 or 1 | 3 or -1 | 4 |
| 6 | [Instruction] | 0 or 1 | 4 | 5 |

**Inference Logic** (when arch_column == -1):
- Contains "R64", "REL64", "RCX" → **X64**
- Contains "R32", "REL32", "ECX", "M32" → **386**
- Default → **8086**

### Parameter Cleanup

Normalizes instruction parameters:
- `XMM1, XMM2, XMM3, XMM4` → `XMM`
- `YMM1, YMM2, YMM3, YMM4` → `YMM`
- `ZMM1, ZMM2, ZMM3` → `ZMM`
- `K1, K2, K3` → `K`
- `BND1, BND2` → `BND`
- `R32A, R32B` → `R32`
- `R64A, R64B` → `R64`
- Removes `+3` suffix
- Preserves `IMM16` (protects it with placeholder)

### Description Cleanup

Normalizes floating-point/precision terminology:
- `floating-point`, `floating- point` → `FP`
- `double-precision`, `double- precision` → `DP`
- `single-precision`, `single- precision` → `SP`

### Output Format

**Signature file** (signature-dec2018.txt):
```
;--------- Header comment
GENERAL	ADD	Add	ADD
ADD	R8,R8	X64,386	ADD R8,R8	Add AL, imm8.
ADD	R8,IMM8	X64,386	ADD R8,IMM8	Add sign-extended imm8 to r/m8.
ADD	R64,R64	X64	ADD R64,R64	Add imm32 sign-extended to 64-bits to RAX.
```

**HTML overview** (overview.txt):
```html
<table>
<tr><td><a href="wiki/ADD">ADD</a></td><td>Add</td><td>X64 386</td></tr>
<tr><td><a href="wiki/ADCX">ADCX</a></td><td>Add with Carry</td><td>X64</td></tr>
</table>
```

---

## Integration Plan for asm-annotate

### Phase 1: Reuse intel-doc-2-data Logic (Immediate)
1. Create `MarkdownFileParser.cs` in asm-annotate
   - Reuse parsing logic from intel-doc-2-data
   - Should work with both generated and existing wiki files
   - Handle all table format variations

2. Create `SignatureGenerator.cs`
   - Reuse Signature struct and conversion logic
   - Output standard signature file format

### Phase 2: Generate Markdown (Next)
1. Create `MarkdownGenerator.cs`
   - Input: `ContentPile` objects from PdfParser
   - Output: Wiki-format markdown files (matching ADD.md structure)
   - Key: Generate HTML tables from instruction data

2. Orchestrate full pipeline:
   ```
   PDF → PdfParser → MarkdownGenerator → .md files
                              ↓
                    intel-doc-2-data logic
                              ↓
                      Signature files
   ```

### Phase 3: Merge Performance Data (Advanced)
1. Create `PerformanceMerger.cs`
   - Input: Signature data + CSV/TSV performance data
   - Output: Enriched data files with performance metrics
   - Merge by mnemonic + operand pattern matching

### Phase 4: Output Unification
1. Create `DataExporter.cs`
   - Export to AsmDude signature files
   - Export to performance TSV files
   - Export to wiki markdown (regenerate 709+ files)

---

## Key Observations

### intel-doc-2-data Strengths
✅ Robust table format detection (3-6 column support)
✅ Smart architecture inference from operand names
✅ Comprehensive parameter normalization
✅ Handles edge cases (CMOV R64 vs R32 distinction)
✅ Proven on 709 existing wiki files

### Gaps to Fill
❌ Assumes wiki files already exist (our PdfParser will create them)
❌ No performance data integration
❌ No modern architecture support (Ice Lake, Raptor Lake, etc.)
❌ Static output paths (C:\Temp\VS\...)

### Integration Strategy
1. **Reuse, Don't Rewrite**: intel-doc-2-data logic is solid
2. **Extend, Don't Replace**: Add MarkdownGenerator before DataConverter
3. **Modularize**: Break intel-doc-2-data into reusable components
4. **Pipeline**: Chain PDF → Markdown → Signature → Performance

---

## File Format Examples

### Wiki Markdown (input to intel-doc-2-data)
```html
<b>ADD</b> — Add
<table>
	<tr>
		<td><b>Opcode</b></td>
		<td><b>Instruction</b></td>
		<td><b>Op/ En</b></td>
		<td><b>64-bit Mode</b></td>
		<td><b>Compat/ Leg Mode</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td>04 ib</td>
		<td>ADD AL, imm8</td>
		<td>I</td>
		<td>Valid</td>
		<td>Valid</td>
		<td>Add imm8 to AL.</td>
	</tr>
</table>
```

### Signature File (output of intel-doc-2-data)
```
GENERAL	ADD	Add	ADD
ADD	R8,R8	X64,386	ADD R8,R8	Add AL, imm8
ADD	R8,IMM8	X64,386	ADD R8,IMM8	Add imm8 to r/m8
ADD	R64,R64	X64	ADD R64,R64	Add imm32 sign-extended to RAX
ADD	R64,IMM32	X64	ADD R64,IMM32	Add imm32 to r/m64
```

### Performance File (TSV format)
```
; Intel Haswell
Instruction	Operands	Fused_ops	Unfused_ops	Ports	Latency	Throughput	Comments
ADD	r32/64,m	1	1	p23	2	0.5	all addressing modes
ADD	m,r	1	2	p237 p4	3	1
ADD	m,i	1	2	p237 p4	 	1
```

---

## Architecture Enum Reference (from AsmTools)

```
ARCH_NONE
ARCH_8086
ARCH_386
ARCH_P6          (Pentium Pro)
ARCH_X64         (x86-64)
ARCH_SMX         (Safer Mode Extension)
ARCH_AVX         (Advanced Vector Extensions)
ARCH_AVX2        (AVX2)
ARCH_AVX512      (AVX-512)
... (and many microarch-specific values)
```

---

## Next Steps

1. **Review existing intel-doc-2-data.cs**: Understand all parsing logic
2. **Extract parsing into reusable library**: Break monolithic Program.cs into components
3. **Implement MarkdownFileParser**: Reuse table parsing from intel-doc-2-data
4. **Implement MarkdownGenerator**: Create wiki format from ContentPile
5. **Create integration tests**: Verify round-trip (PDF → Markdown → Signature)
6. **Test with existing wiki**: Regenerate 709 files, compare with baseline

## Summary

intel-doc-2-data is a **critical component** in the AsmDude data pipeline. It converts human-readable markdown wiki files into machine-readable signature data. To properly integrate asm-annotate:

1. **Reuse its parsing logic** (Don't re-implement)
2. **Add markdown generation** (PdfParser output → wiki format)
3. **Extend for performance data** (Add CSV/TSV merging)
4. **Modernize output paths** (Remove hardcoded C:\Temp\)
5. **Support new architectures** (Ice Lake, Raptor Lake, etc.)

The existing 709 wiki files serve as both:
- **Input** to intel-doc-2-data (validation baseline)
- **Output** target (regenerate with new PDF data)
