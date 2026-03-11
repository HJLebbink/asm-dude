# asm-annotate: Data Update Pipeline TODO

## Project Goal
Update AsmDude data files (instruction signatures and performance metrics) by extracting information from multiple public Intel sources.

### Destination Files

#### Primary Destinations (AsmDude2 Extension)
- **Signature Files**: `asm-dude2-ls-lib/Resources/signature-*.txt`
  - Format: `CATEGORY\tMNEMONIC\tDescription\t[Aliases]`
  - Contains instruction documentation and hover information

- **Performance Files**: `asm-dude2-ls-lib/Resources/Performance/*.tsv`
  - **Current Coverage**: Haswell, Broadwell, IvyBridge, Skylake, SkylakeX (last updated: unknown, likely 2019)
  - **Target Coverage**: Add modern microarchitectures (see Microarchitecture Coverage section)
  - Format: Tab-separated with columns: Instruction, Operands, μops, Latency, Throughput, Comments

- **Translation Map**: `asm-dude2-ls-lib/Resources/Performance/Instructions-Translations.tsv`
  - Maps instruction patterns (e.g., CMOVcc) to actual mnemonics

#### Secondary Destinations (Documentation)
- **Wiki Markdown Files**: `asm-dude.wiki/doc/*.md` (via CI/CD or manual)
  - Regenerate instruction documentation markdown files
  - Should match or exceed the 709 files currently in wiki (Nov 2020)
  - Useful for: Users browsing instruction reference, testing PDF parser output, validation baseline

---

## Microarchitecture Coverage

### Current Coverage (Legacy - 2019 or earlier)
| Architecture | Codename | Release | File Status |
|---|---|---|---|
| Ivy Bridge | IvyBridge | Q3 2012 | ✅ `IvyBridge.tsv` |
| Haswell | Haswell | Q3 2013 | ✅ `Haswell.tsv` |
| Broadwell | Broadwell | Q4 2014 | ✅ `Broadwell.tsv` |
| Skylake | Skylake | Q3 2015 | ✅ `Skylake.tsv` |
| Skylake X | SkylakeX | Q3 2017 | ✅ `SkylakeX.tsv` |

### Priority 1: Add to AsmDude (High Value - Modern Mainstream)
| Architecture | Codename | Release | Type | Status | Notes |
|---|---|---|---|---|---|
| Cascade Lake | Cascade Lake | Q1 2019 | Server | ❌ Missing | Xeon Platinum/Gold 2nd Gen |
| Ice Lake | Ice Lake | Q2 2020 | Desktop/Laptop/Server | ⚠️ Partial | Xeon 3rd Gen; in intel-doc-2-md as `icelake.csv` |
| Tiger Lake | Tiger Lake | Q4 2020 | Laptop | ❌ Missing | Core 11th Gen mobile |
| Rocket Lake | Rocket Lake | Q1 2021 | Desktop | ❌ Missing | Core 11th Gen desktop |
| Alder Lake | Alder Lake | Q4 2021 | Desktop/Laptop | ❌ Missing | Core 12th Gen (P-cores + E-cores) |
| Raptor Lake | Raptor Lake | Q4 2022 | Desktop/Laptop/Server | ❌ Missing | Core 13th Gen, Xeon 4th Gen |
| Raptor Lake R | Raptor Lake R | Q1 2023 | Desktop/Laptop | ❌ Missing | Core 14th Gen (refresh) |
| Granite Rapids | Granite Rapids | Q4 2024 | Server | ❌ Missing | Xeon 6th Gen (Sierra Forest variant) |
| Arrow Lake | Arrow Lake | Q4 2024 | Desktop/Laptop | ❌ Missing | Core Ultra (180-series) |
| Lunar Lake | Lunar Lake | Q3 2024 | Laptop | ❌ Missing | Core Ultra (200V-series) |

### Priority 2: Server-Only (Medium Value)
| Architecture | Codename | Release | Type | Status |
|---|---|---|---|---|
| Sierra Forest | Sierra Forest | Q4 2024 | Server (E-cores) | ❌ Missing |
| Clearwater Forest | Clearwater Forest | 2025 | Server | ⏳ Forthcoming |
| Emerald Rapids | Emerald Rapids | Q4 2023 | Server | ❌ Missing |

### Data Source Availability

#### Intel Intrinsics Guide
- Contains performance data for: **Haswell, Broadwell, Skylake, Skylake-X, Cascade Lake, Ice Lake, Rocket Lake, Alder Lake, Raptor Lake, Raptor Lake R, Arrow Lake, Lunar Lake**
- Format: XML (requires parsing)
- Last updated: July 2024 (version 3.6.9)
- URL: https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html

#### Intel Software Developer Manuals
- Contains instruction set definitions (not performance data)
- Performance appendices vary by manual version
- Newer manuals include: Cascade Lake, Ice Lake, Tiger Lake, Rocket Lake, Alder Lake, Raptor Lake

#### XED (Intel X86 Encoder Decoder)
- Provides instruction definitions, not performance metrics
- Actively maintained with latest ISA extensions
- GitHub: https://github.com/intelxed/xed

### Actions Required

#### 1.1.4 Microarchitecture Prioritization
- [ ] Determine which architectures to prioritize for initial implementation
  - [ ] **Must have**: Cascade Lake, Ice Lake, Alder Lake, Raptor Lake (highest user impact)
  - [ ] **Should have**: Tiger Lake, Rocket Lake, Arrow Lake, Lunar Lake
  - [ ] **Nice to have**: Server-only variants (Sierra Forest, Granite Rapids, Emerald Rapids)
- [ ] Verify data availability in Intel Intrinsics Guide for each architecture
- [ ] Check if performance data exists in intel-doc-2-md resources

#### 1.1.5 Ice Lake Data Recovery
- [ ] Validate `icelake.csv` in intel-doc-2-md (September 2023)
- [ ] Convert icelake.csv format to standard TSV format if needed
- [ ] Verify completeness and accuracy of Ice Lake data
- [ ] Generate official `IceLake.tsv` file for Resources/Performance/

---

## Critical: Python Project Port

### Existing Asset: asm-dude.wiki Documentation
**Location**: `C:\Source\Github\asm-dude.wiki\doc\` (sister repository)
**Status**: ✅ Already populated with 709 instruction markdown files
**Last Updated**: November 2020
**Source**: Generated by intel-doc-2-md Python project
**Format**: HTML tables embedded in markdown

**Content Example**: Each instruction has:
- Instruction mnemonic and brief description
- Opcode table with columns:
  - Opcode (binary encoding)
  - Instruction syntax
  - Op/En (operand encoding)
  - 64-bit mode support
  - Compatibility/Legacy mode support
  - Description

**Value for asm-annotate**:
- Reference implementation of intel-doc-2-md output
- Source of instruction definitions for signature generation
- Can be parsed to extract:
  - Instruction mnemonics
  - Opcode formats
  - Instruction variants
  - Mode support (64-bit, compat, legacy)
- Validation reference: Regenerate and compare with existing wiki files

---

### Python Project: intel-doc-2-md
**Location**: `VS/Python/intel-doc-2-md/`
**Status**: Functional Python 2 project for PDF document parsing
**Purpose**: Extract instruction definitions from Intel PDF documentation and generate markdown
**Output**: The markdown files in asm-dude.wiki were generated by this project

#### Current Python Architecture
1. **Parser** (`inteldoc2md/parser.py`)
   - Uses `pdfminer` library to extract text and layout from PDFs
   - Processes pages via `PDFPageInterpreter` and `PDFPageAggregator`
   - Stores extracted layout in `self._pages` dictionary
   - Methods:
     - `extract(page_num_start, page_num_end)` - Extract pages from PDF
     - `parse()` - Parse extracted pages into logical "piles"

2. **Pile** (`inteldoc2md/pile.py`)
   - Represents logical content blocks (tables, paragraphs, images)
   - Parses PDF layout objects:
     - `LTTextLineHorizontal` → text content
     - `LTRect` → table borders (vertical/horizontal lines)
     - `LTImage` → images/diagrams
   - Identifies content type: table, image, or paragraph
   - Methods:
     - `parse_layout(layout)` - Parse PDF layout into piles
     - `split_piles()` - Split into separate content blocks
     - `gen_markdown(state)` - Generate markdown from content

3. **Writer** (`inteldoc2md/writer.py`)
   - Generates markdown output from parsed piles
   - **Critical feature**: Hyphenation cleanup
     - Handles PDF word breaks: `"instruc-\ntions"` → `"instructions"`
     - Includes 30+ hardcoded replacement patterns
     - Replaces bullet points with markdown lists
   - Identifies instruction sections and opcode tables
   - Output: Individual markdown files per instruction

4. **Main** (`main.py`)
   - CLI entry point
   - Inputs: Intel PDF (e.g., `selection-ext.pdf`)
   - Pipeline: Read PDF → Extract pages → Parse piles → Write markdown

#### Test Resources
- **Input PDFs**:
  - `architecture-instruction-set-extensions-programming-reference-selection.pdf` (284 KB)
  - `325462-sdm-vol-1-2abcd-3abcd-selection.pdf` (archive)
- **Output**: Markdown files per instruction (not committed)

#### Key Challenges in Python Project
1. PDF layout parsing is fragile (depends on PDF structure)
2. Hardcoded hyphenation patterns (need maintenance for each PDF format change)
3. Instruction detection relies on heuristics (opcode table detection)
4. No automated testing (manual verification needed)
5. Python 2 (deprecated; needs migration to Python 3 first or direct C# port)

#### Porting Strategy to C#
**Option 1: Direct C# Port** (Recommended)
- Replace `pdfminer` with .NET PDF library (iText7, PdfSharp, or Aspose.PDF)
- Port Parser, Pile, Writer logic to C#
- Integrate directly into asm-annotate project
- Timeline: Medium (2-3 weeks for full port + testing)

**Option 2: Python 3 Migration + Wrapper** (Intermediate)
- Modernize Python 2 → Python 3
- Call from C# via subprocess (IronPython or shell execution)
- Simpler but less integrated, slower at runtime
- Timeline: Short (1 week for migration)

**Option 3: Leverage Python CLI** (Quick Start)
- Keep Python as-is
- Call from asm-annotate via command-line execution
- Use JSON/TSV for inter-process data exchange
- Timeline: 2-3 days for integration wrapper
- Cons: Runtime dependency on Python, slower

### Recommended: Start with Option 3 (Python CLI Wrapper) for Quick Start
- Gets data extraction working immediately
- Then port Parser/Pile/Writer to C# in Phase 2
- Allows testing of signature file generation workflow

---

## Phase 1: Infrastructure & Source Analysis

### 1.1 Data Source Investigation
- [ ] **Intel XED Project**
  - [ ] Clone or download latest XED from https://github.com/intelxed/xed
  - [ ] Analyze instruction definition format (IFORM registry)
  - [ ] Document how to extract mnemonic, operands, and aliases
  - [ ] Test parsing of XED data files

- [ ] **Intel® Intrinsics Guide**
  - [ ] Download offline package from https://www.intel.com/content/www/us/en/content-details/778560/intel-intrinsics-guide-download.html
  - [ ] Analyze XML structure for performance data (throughput/latency)
  - [ ] Document microarchitecture coverage available in guide
    - [ ] Verify coverage for: Haswell, Broadwell, Skylake, SkylakeX (existing)
    - [ ] Verify coverage for: Cascade Lake, Ice Lake, Alder Lake, Raptor Lake, Raptor Lake R (priority 1)
    - [ ] Verify coverage for: Tiger Lake, Rocket Lake, Arrow Lake, Lunar Lake (priority 2)
    - [ ] Check if server architectures included (Emerald Rapids, Granite Rapids, Sierra Forest)
  - [ ] Create parser for XML format
  - [ ] Build mapping from Intrinsics Guide architecture names to AsmDude TSV filenames

- [ ] **Intel® 64 and IA-32 Architectures Software Developer Manuals**
  - [ ] Locate PDF volumes (Vol. 2A/2B for instruction reference)
  - [ ] Analyze text/table format for instruction descriptions
  - [ ] Determine if OCR or automated extraction is feasible
  - [ ] Document instruction categories/grouping approach

### 1.2 Current Data Analysis
- [ ] Audit existing signature files
  - [ ] Inventory all instruction categories (GENERAL, FP, VECTOR, SSE, AVX, etc.)
  - [ ] Count current instructions and aliases
  - [ ] Identify gaps (instructions in XED but not in signatures)

- [ ] Audit existing performance files
  - [ ] Verify architecture coverage completeness
  - [ ] Identify which architectures need updates
  - [ ] Check data freshness (date of last update)
  - [ ] Validate performance data format consistency

### 1.3 Architecture Decisions
- [ ] **Input Format Selection**
  - [ ] Decide on intermediate data format (CSV, JSON, XML, or direct parsing)
  - [ ] Plan for handling variant instruction forms (e.g., CMOV vs CMOVcc patterns)
  - [ ] Define operand encoding (r, m, i, x, y, z register types)

- [ ] **Update Strategy**
  - [ ] Full regeneration vs. incremental updates?
  - [ ] How to handle hand-crafted vs. auto-generated data?
  - [ ] Versioning and change tracking strategy

---

## Phase 2: Data Extraction Tools

### 2.0 PDF Document Parser (Ported from Python)

#### 2.0.1 PDF Parser Port (PdfDocumentParser.cs)
- [ ] **Library Selection**
  - [ ] Evaluate PDF libraries for .NET:
    - `PdfSharp` - Open source, simple, no external dependencies
    - `iText7` - Comprehensive, commercial license considerations
    - `Aspose.PDF` - Feature-rich but commercial
  - [ ] Recommend: PdfSharp for initial port (open source, no licensing issues)

- [ ] **Core Parser Implementation**
  - [ ] Create `PdfDocumentParser` class
    - [ ] Load PDF file and initialize parser
    - [ ] Extract page content with layout information
    - [ ] Store extracted pages for downstream processing
  - [ ] Methods:
    - [ ] `ExtractPages(filename, startPage, endPage)` - Extract page content
    - [ ] `GetPageCount()` - Return total pages in PDF
    - [ ] `GetExtractedPages()` - Return dictionary of pages

#### 2.0.2 Pile Parser Port (ContentPile.cs)
- [ ] **Pile Structure**
  - [ ] Create `ContentPile` class representing logical content blocks
  - [ ] Properties:
    - [ ] Type: Table, Image, Paragraph
    - [ ] TextLines - List of text content
    - [ ] VerticalLines - Vertical borders (for table detection)
    - [ ] HorizontalLines - Horizontal borders (for table detection)
    - [ ] Images - Image/diagram content

- [ ] **Layout Parsing**
  - [ ] Implement `ParsePageLayout(pdfPage)` method
  - [ ] Traverse PDF layout objects recursively:
    - [ ] Text boxes → extract text lines
    - [ ] Lines/Rectangles → classify as table borders
    - [ ] Images → store for reference
  - [ ] Implement `GetContentType()` - Determine pile type (table/image/paragraph)

- [ ] **Pile Splitting**
  - [ ] Implement `SplitIntoLogicalPiles()` method
  - [ ] Group adjacent content blocks
  - [ ] Separate tables, images, and text paragraphs
  - [ ] Sort by vertical position (top-to-bottom)

#### 2.0.3 PDF Text Cleanup (TextCleaner.cs)
- [ ] **Hyphenation Handling**
  - [ ] Create `TextCleaner` class with static methods
  - [ ] Implement hyphenation pattern replacements:
    ```csharp
    "instruc-\ntions" → "instructions"
    "oper-\nands" → "operands"
    "regis-\nters" → "registers"
    // ... 30+ patterns from Python project
    ```
  - [ ] Handle common hyphenation variants (word-specific patterns)

- [ ] **PDF Artifact Cleanup**
  - [ ] Convert bullet points: `•\n` → `\n * `
  - [ ] Remove extra whitespace/newlines
  - [ ] Normalize spacing around punctuation
  - [ ] Handle special characters (superscripts, Greek letters)

- [ ] **Text Normalization**
  - [ ] Standardize spacing and indentation
  - [ ] Remove page headers/footers (heuristic detection)
  - [ ] Preserve code blocks and tables

#### 2.0.4 Instruction Detector (InstructionDetector.cs)
- [ ] **Instruction Recognition**
  - [ ] Create `InstructionDetector` class
  - [ ] Detect instruction headers: pattern recognition for "MNEMONIC — Description"
  - [ ] Extract mnemonic name from text
  - [ ] Identify instruction sections vs. other content

- [ ] **Opcode Table Detection**
  - [ ] Identify opcode encoding tables (table with vertical/horizontal lines)
  - [ ] Extract opcode bits and fields
  - [ ] Parse operand encoding from tables

- [ ] **Section Boundaries**
  - [ ] Detect instruction section start/end
  - [ ] Group related content (description, operation, flags affected, etc.)
  - [ ] Extract subsection titles

#### 2.0.4.5 Wiki File Parser (WikiMarkdownParser.cs) [Optional Enhancement]
- [ ] **Parse Existing Wiki Markdown Files**
  - [ ] Parse HTML tables from `asm-dude.wiki/doc/*.md` files
  - [ ] Extract instruction opcode information
  - [ ] Extract instruction variants and syntax
  - [ ] Use as additional data source for signature enrichment
  - [ ] Value: Wiki files are already cleaned and structured
  - [ ] Note: Should be regenerated/updated as part of this project

#### 2.0.5 Writer: Signature File Generation (PdfSignatureWriter.cs)
- [ ] **Signature Format Output**
  - [ ] Create `PdfSignatureWriter` class
  - [ ] Generate signature file format:
    ```
    CATEGORY\tMNEMONIC\tDescription\t[Aliases]
    ```
  - [ ] Extract category from ISA extension (SSE, AVX, BMI, etc.)
  - [ ] Clean description text and limit length
  - [ ] **Leverage wiki files**: Use opcode information from wiki when available

- [ ] **Instruction Grouping**
  - [ ] Group instruction variants under canonical name
  - [ ] Handle aliases (e.g., JE/JZ, REPZ/REPE)
  - [ ] Create summary descriptions for each group
  - [ ] Match against existing wiki entries for consistency

- [ ] **File Generation**
  - [ ] Write to `signature-pdf-generated.txt`
  - [ ] Add header with generation date and PDF source
  - [ ] Sort by mnemonic for consistency
  - [ ] Handle conflicts with hand-crafted signatures
  - [ ] **Validation**: Compare generated signatures with wiki files (709 instructions should be covered)

#### 2.0.6 Integration with asm-annotate
- [ ] **Main Pipeline**
  - [ ] Add command-line parameter: `--pdf-input <path>`
  - [ ] Orchestrate: Parse PDF → Extract piles → Detect instructions → Write signatures
  - [ ] Report progress and statistics
  - [ ] Error handling for malformed PDFs

- [ ] **Testing**
  - [ ] Test on provided Intel PDFs
  - [ ] Generate sample signature files
  - [ ] Manual review of extracted instructions
  - [ ] Compare with existing hand-crafted signatures

---

### 2.1 XED Parser
- [ ] **Create XedParser class**
  - [ ] Parse IFORM definitions from XED data files
  - [ ] Extract: mnemonic name, operand types, aliases, ISA extensions
  - [ ] Handle legacy vs. new instruction encodings
  - [ ] Map XED register types to AsmDude notation (r8/16/32/64, m, x, y, z)

- [ ] **Instruction Signature Generation**
  - [ ] Generate category labels from ISA extension (SSE, AVX, AVX-512, BMI, etc.)
  - [ ] Create human-readable descriptions from XED metadata
  - [ ] Group related instructions (e.g., all MOV variants)
  - [ ] Handle aliased instructions (same instruction with multiple names)

### 2.2 Intel Intrinsics Guide Parser
- [ ] **Create IntrinsicsParser class**
  - [ ] Parse XML structure from Intel Intrinsics Guide
  - [ ] Extract: instruction name, intrinsic name, operands, latency, throughput
  - [ ] Map to microarchitecture (Haswell, Broadwell, Skylake, etc.)
  - [ ] Handle scalar vs. vector variants

- [ ] **Performance Data Extraction**
  - [ ] Extract latency values per microarchitecture
  - [ ] Extract reciprocal throughput values
  - [ ] Handle missing data (mark as N/A or unknown)
  - [ ] Validate numeric ranges (sanity checks)

### 2.3 PDF Document Parser (Optional/Advanced)
- [ ] **Determine feasibility**
  - [ ] Test OCR libraries (Tesseract, IronOCR) on Intel PDFs
  - [ ] Evaluate manual vs. automated extraction cost/benefit
  - [ ] Consider using pre-existing extracted data instead

- [ ] **If automated extraction chosen:**
  - [ ] Create PdfInstructionParser class
  - [ ] Extract instruction mnemonic, syntax, description from PDF
  - [ ] Parse operation descriptions and side effects
  - [ ] Build fallback mechanism if OCR fails

### 2.4 Command-Line Interface
- [ ] **Update Program.cs**
  - [ ] Replace placeholder "Hello World"
  - [ ] Add command-line arguments:
    - `--xed-path <path>` - path to XED installation/data
    - `--intrinsics-path <path>` - path to Intel Intrinsics Guide XML
    - `--output-dir <path>` - output directory for generated files
    - `--microarch <name>` - specific microarchitecture to update (or all)
  - [ ] Implement progress reporting and logging

- [ ] **Error Handling & Validation**
  - [ ] Validate input files exist and are readable
  - [ ] Report parsing errors with line/context info
  - [ ] Generate summary of extracted instructions
  - [ ] Verify output file format correctness

---

## Phase 3: Output Generation

### 3.1 Signature File Generation
- [ ] **SignatureFileWriter class**
  - [ ] Generate `signature-auto-generated.txt` with all extracted instructions
  - [ ] Preserve hand-crafted `signature-hand-1.txt` (merge if needed)
  - [ ] Format: `CATEGORY\tMNEMONIC\tDescription\t[Aliases]`
  - [ ] Sort by mnemonic for consistency
  - [ ] Add header with generation date and source

- [ ] **Data Reconciliation**
  - [ ] Compare auto-generated vs. hand-crafted signatures
  - [ ] Identify instructions with conflicting descriptions
  - [ ] Manual review process for discrepancies

### 3.2 Performance File Generation
- [ ] **PerformanceFileWriter class**
  - [ ] Generate TSV files for each microarchitecture
  - [ ] Format: Instruction, Operands, μops fused, μops unfused, ports, Latency, Throughput, Comments
  - [ ] Include header with architecture name and data source
  - [ ] Handle missing data gracefully (blank or N/A)

- [ ] **Architecture Coverage**
  - [ ] **Legacy (maintain existing)**: Haswell, Broadwell, IvyBridge, Skylake, SkylakeX
  - [ ] **Priority 1 (add immediately)**:
    - [ ] Cascade Lake (2019 Xeon)
    - [ ] Ice Lake (2020, desktop/laptop/Xeon)
    - [ ] Alder Lake (2021, 12th Gen, P-cores + E-cores)
    - [ ] Raptor Lake (2022, 13th Gen + Xeon 4th Gen)
    - [ ] Raptor Lake R (2023, 14th Gen refresh)
  - [ ] **Priority 2 (add if data available)**:
    - [ ] Tiger Lake (2020, 11th Gen mobile)
    - [ ] Rocket Lake (2021, 11th Gen desktop)
    - [ ] Arrow Lake (2024, Core Ultra)
    - [ ] Lunar Lake (2024, Core Ultra mobile)
    - [ ] Emerald Rapids (2023, Xeon 5th Gen)
  - [ ] **Priority 3 (server-only, add later)**:
    - [ ] Granite Rapids (2024, Xeon 6th Gen)
    - [ ] Sierra Forest (2024, Xeon E-cores)
  - [ ] Validate each architecture file consistency
  - [ ] Verify data completeness (all instructions have latency/throughput)

### 3.3 Instruction Translation Mapping
- [ ] **TranslationMapGenerator class**
  - [ ] Create `Instructions-Translations.tsv` mapping
  - [ ] Handle instruction patterns (CMOVcc, CMPccPS, VCMPccPD, etc.)
  - [ ] Map each pattern to list of concrete mnemonics
  - [ ] Support for both auto-generated and hand-crafted mappings

---

## Phase 4: Testing & Validation

### 4.1 Unit Tests
- [ ] **ParserTests**
  - [ ] Test XED parser with sample data
  - [ ] Test Intrinsics parser with sample XML
  - [ ] Test PDF parser (PdfDocumentParser) with sample Intel PDFs
  - [ ] Test wiki markdown parser (WikiMarkdownParser) with existing wiki files
  - [ ] Verify correct extraction of all instruction fields
  - [ ] Test error handling for malformed input

- [ ] **OutputTests**
  - [ ] Verify signature file format correctness
  - [ ] Verify performance file format consistency
  - [ ] Check translation map completeness
  - [ ] Validate TSV tab-separated format
  - [ ] **Wiki Comparison Tests**: Verify generated signatures match existing wiki coverage (709 instructions)

### 4.2 Data Validation
- [ ] **Completeness Checks**
  - [ ] Compare instruction count before/after
  - [ ] Identify any instructions lost in conversion
  - [ ] Verify all microarchitectures have performance data

- [ ] **Format Validation**
  - [ ] Verify UTF-8 encoding
  - [ ] Check line endings (CRLF vs LF)
  - [ ] Validate numeric fields (latency, throughput)
  - [ ] Verify operand format consistency

### 4.4 Microarchitecture Validation
- [ ] **Per-Architecture Checks**
  - [ ] For each microarchitecture TSV file:
    - [ ] Verify all instructions have latency data (no missing values)
    - [ ] Verify all instructions have throughput data
    - [ ] Verify latency values are reasonable (e.g., 0-20 cycles for most instructions)
    - [ ] Verify throughput values are reasonable (e.g., 0.25-4 instructions/cycle)
    - [ ] Check for outliers and validate against Intel documentation

- [ ] **Cross-Architecture Consistency**
  - [ ] Verify newer architectures are not slower than predecessors (sanity check)
  - [ ] Identify instructions with significant performance changes between architectures
  - [ ] Validate port distribution ({p0}, {p1}, {p2}, etc.) is valid for each microarchitecture

- [ ] **Completeness Check**
  - [ ] Compare instruction count across architectures
  - [ ] Identify instructions added in newer architectures
  - [ ] Identify instructions removed or deprecated
  - [ ] Verify AVX-512 instructions only in architectures that support AVX-512

### 4.3 Integration Tests
- [ ] **Load generated files into AsmDude**
  - [ ] Verify LanguageServer can load signature files
  - [ ] Verify PerformanceStore can load TSV files
  - [ ] Check hover information displays correctly
  - [ ] Validate code completion with updated mnemonics

- [ ] **Wiki-Based Validation**
  - [ ] Parse all 709 markdown files from `asm-dude.wiki/doc/`
  - [ ] Extract instruction mnemonics (should be 709 unique instructions)
  - [ ] Compare with generated signatures:
    - [ ] Coverage check: How many wiki instructions are in generated signatures?
    - [ ] New instructions: How many generated instructions are NOT in wiki?
    - [ ] Deleted instructions: How many wiki instructions are NOT in generated?
  - [ ] Generate coverage report showing progress
  - [ ] **Goal**: 100% coverage of 709 instructions in regenerated wiki files

---

## Phase 5: Maintenance & Documentation

### 5.1 Documentation
- [ ] **User Guide**
  - [ ] Document how to run asm-annotate
  - [ ] Explain input sources and data formats
  - [ ] Provide examples of updating specific microarchitectures
  - [ ] Troubleshooting guide for common issues

- [ ] **Data Source Updates**
  - [ ] Document which Intel resources to check for updates
  - [ ] Create schedule for periodic updates (annual/biannual)
  - [ ] Track changelog of instruction additions/changes

- [ ] **Code Documentation**
  - [ ] Add XML comments to all parser classes
  - [ ] Document data transformation logic
  - [ ] Include examples of input/output formats

### 5.2 CI/CD Integration
- [ ] **Automated Data Updates (Future)**
  - [ ] Consider adding asm-annotate to build pipeline
  - [ ] Auto-check for new Intel documentation releases
  - [ ] Automated test before committing generated files

### 5.3 Version Control
- [ ] **Track Data Provenance**
  - [ ] Store source version info (XED commit, Intrinsics Guide version)
  - [ ] Add metadata headers to generated files
  - [ ] Git history of changes with dates and sources

---

## Signature File Format Reference

### Overview
Signature files (`.txt`) are tab-separated value files that define instruction mnemonics and their signatures. They are loaded by `MnemonicStore` class in `asm-dude2-ls-lib`.

### File Format

#### Type 1: General Description (4 columns)
Used to define general instruction information accessible via hover.

```
<CATEGORY>\t<MNEMONIC>\t<DESCRIPTION>\t<HTML_REFERENCE>
```

**Columns:**
1. **CATEGORY**: Category label (usually same as mnemonic, but can be different)
   - Examples: `GENERAL`, `REP`, `REPE`, `REPZ`, `INT`
   - Used for grouping related instructions

2. **MNEMONIC**: Instruction mnemonic name
   - Must be parseable by `AsmSourceTools.ParseMnemonic()`
   - Examples: `MOV`, `ADD`, `VADDPS`, `CMOVA`, `BND`

3. **DESCRIPTION**: Human-readable description (1-2 sentences)
   - Displayed in hover tooltips and code completion
   - Examples: "Move", "Add", "Move if above (CF=0 and ZF=0) (CMOVA=CMOVNBE)"
   - Can include aliases in parentheses

4. **HTML_REFERENCE**: URL to instruction documentation
   - Used for "Go to documentation" links
   - Examples: `https://www.intel.com/...`, or empty string `""` for no link
   - Stored in `htmlRef_` dictionary

**Parsing Code** (MnemonicStore.cs, line 273-300):
```csharp
if (columns.Length == 4) // general description
{
    Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[1], false);
    description.Add(mnemonic, columns[2]);  // columns[2] = description
    htmlRef.Add(mnemonic, columns[3]);      // columns[3] = HTML reference
}
```

**Example from signature-hand-1.txt:**
```
GENERAL	BND	Prefix to instruct that the next instruction is MPX-instrumented code.
GENERAL	MOV	Move	MOV
GENERAL	CMOVA	Move if above (CF=0 and ZF=0) (CMOVA=CMOVNBE)	CMOVcc
```

---

#### Type 2: Signature Description (5-6 columns)
Defines specific instruction signatures with operands, architecture, and documentation.

```
<MNEMONIC>\t<OPERANDS>\t<ARCHITECTURE>\t<SIGNATURE>\t<DOCUMENTATION>
```

**Columns:**

1. **MNEMONIC**: Instruction mnemonic
   - Must match a valid mnemonic parsed by `AsmSourceTools.ParseMnemonic()`
   - Examples: `MOV`, `ADD`, `VADDPS`, `CMOVA`

2. **OPERANDS**: Comma-separated list of operand types
   - Format: Each operand is a forward-slash-separated list of possible types
   - **Operand Type Notation:**
     - `r` = General-purpose register
     - `r8`, `r16`, `r32`, `r64` = Specific register widths
     - `r32/64` = Register (32 or 64 bits)
     - `m` = Memory operand
     - `m32`, `m64` = Memory operand of specific size
     - `i` = Immediate value
     - `x` = XMM register (128-bit)
     - `y` = YMM register (256-bit)
     - `z` = ZMM register (512-bit)
     - `mm` = MMX register
     - `cr` = Control register
     - `dr` = Debug register
     - `{K}` = AVX-512 mask register (optional)
     - `{Z}` = AVX-512 zeroing mask (optional)
   - **Forward slashes** (/) mean "or" - multiple possible operand types
   - **Commas** (,) separate operands

   **Examples:**
   ```
   MOV            r,i              (register, immediate)
   MOV            r,m              (register, memory)
   MOV            r32/64,r32/64    (32 or 64-bit register, 32 or 64-bit register)
   VADDPS         XMM{K}{Z},XMM,XMM/M128/M32BCST
   ```

3. **ARCHITECTURE**: Comma-separated list of ISA extensions
   - Format: Architecture codes separated by commas
   - **Architecture Codes:**
     - `8086`, `186`, `286`, `386`, `486`, `586`, `686` = x86 generations
     - `SSE`, `SSE2`, `SSE3`, `SSSE3`, `SSE4_1`, `SSE4_2` = SSE extensions
     - `AVX`, `AVX2`, `AVX512_F`, `AVX512_BW`, `AVX512_VL` = Vector extensions
     - `BMI1`, `BMI2`, `LZCNT`, `POPCNT`, `AES` = Feature extensions
     - Many others (full list in ArchTools.cs)
   - Multiple architectures indicate instruction available in multiple versions

   **Examples:**
   ```
   8086              (8086 and later)
   SSE,SSE2          (SSE or SSE2)
   AVX512_VL,AVX512_F  (AVX-512 with Vector Length and AVX-512 Foundation)
   ```

4. **SIGNATURE**: Complete instruction syntax with operands and register sizes
   - Format: Mnemonic followed by actual operand specifications
   - Used for signature help in code editors (shows what you're typing)
   - Must have same number of operands as OPERANDS column (comma-separated)
   - **Examples:**
     ```
     MOV r,i           → MOV r64, imm32
     MOV r32/64,r32/64 → MOV r32, r32  (or MOV r64, r64)
     VADDPS XMM{K}{Z},XMM,XMM/M128/M32BCST  → VADDPS xmm1{k1}{z}, xmm2, xmm3/m128/m32bcst
     ```
   - Used by `FindParamPositions()` to locate operand boundaries

5. **DOCUMENTATION**: Human-readable description of what the instruction does
   - Detailed explanation of operation
   - Examples:
     ```
     "Add packed SP FP values from xmm3/m128/m32bcst to xmm2 and store result in xmm1 with writemask k1."
     "Move double-precision (64-bit) integer operand into an XMM register without affecting the upper 64 bits."
     ```
   - Displayed in hover and code completion tooltips

**Optional 6th Column** (ignored): Legacy field, still supported but not used

**Parsing Code** (MnemonicStore.cs, line 302-317):
```csharp
if ((columns.Length == 5) || (columns.Length == 6))  // signature description, ignore old sixth column
{
    Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0], false);
    var se = this.CreateAsmSignatureElement(
        mnemonic,
        columns[1],  // operands
        columns[2],  // architecture
        columns[3],  // signature
        columns[4]   // documentation
    );
}
```

**Example from signature files:**
```
MOV	r,i	8086	MOV r32, imm32	Move immediate data into a register.
MOV	r8/16,r8/16	8086	MOV r8, r8	Copy the contents of a register into another register.
VADDPS	XMM{K}{Z},XMM,XMM/M128/M32BCST	AVX512_VL,AVX512_F	VADDPS xmm1{k1}{z}, xmm2, xmm3/m128/m32bcst	Add packed SP FP values with optional masking.
```

---

### Comments and Blank Lines
- Lines starting with `;` are comments and ignored
- Blank lines are ignored
- Multiple signature entries for same mnemonic with different operands are allowed

---

### Data Storage in MnemonicStore
After parsing, the signature files populate:

1. **data_**: `Dictionary<Mnemonic, List<AsmSignatureInformation>>`
   - Key: Instruction mnemonic
   - Value: List of signature variants (different operand combinations)

2. **arch_**: `Dictionary<Mnemonic, List<Arch>>`
   - Key: Mnemonic
   - Value: Aggregated list of architectures where instruction is available

3. **htmlRef_**: `Dictionary<Mnemonic, string>`
   - Key: Mnemonic
   - Value: HTML documentation URL

4. **description_**: `Dictionary<Mnemonic, string>`
   - Key: Mnemonic
   - Value: Brief description text

---

### Files in AsmDude2
- **signature-hand-1.txt**: Hand-crafted signatures (primary source)
  - Generally instruction descriptions and HTML references
  - Maintained manually
  - Takes precedence when same instruction in both files

- **signature-may2019.txt**: Auto-generated signatures (May 2019)
  - Contains signature definitions with operands and architectures
  - Older auto-generated version
  - Should be updated/regenerated by asm-annotate tool

- **signature-pdf-generated.txt** (future): To be generated by asm-annotate
  - Will be auto-generated from Intel PDF documents
  - Combined with hand-crafted file during loading

---

## Data Source Links & Assets
- **Intel XED**: https://github.com/intelxed/xed
- **Intel Intrinsics Guide**: https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html
- **Intel Intrinsics Download**: https://www.intel.com/content/www/us/en/content-details/778560/intel-intrinsics-guide-download.html
- **Intel ISA References**: https://www.intel.com/content/www/us/en/developer/articles/technical/intel-sdm.html
- **AsmDude Resources**: `VS/CSHARP/asm-dude2-ls-lib/Resources/`
- **AsmDude Wiki** (Generated MD Files): `C:\Source\Github\asm-dude.wiki/doc/` (709 instruction markdown files, last updated Nov 2020)
  - Clone: `git clone https://github.com/HenkJanLebbink/asm-dude.wiki.git`
  - Value: Reference output from intel-doc-2-md, validation baseline for regeneration

---

## Current Status
- asm-annotate skeleton exists with ParserXed class (incomplete)
- Legacy icelake.csv data (Sept 2023, from Intel Intrinsics Guide)
- Signature files are hand-crafted + May 2019 version
- Performance files for Haswell, Broadwell, IvyBridge, Skylake, SkylakeX
- **Wiki documentation**: ✅ 709 instruction markdown files exist (Nov 2020)
  - Generated by intel-doc-2-md Python project
  - Can be regenerated and updated as part of this project
  - Useful validation baseline for testing PDF parser

## Microarchitecture Tracking & Maintenance

### Annual Intel Release Cycle
Intel typically releases new microarchitectures on a regular schedule:
- **Q4**: New server platforms (Xeon scalable family)
- **Q1**: New mobile platforms (Core Ultra)
- **Q3-Q4**: Desktop platforms (Core generation updates)

### Tracking Process
1. **Monitor Intel Announcements**
   - Subscribe to Intel Developer Zone announcements
   - Watch GitHub releases for XED project (https://github.com/intelxed/xed/releases)
   - Check Intel Intrinsics Guide updates (https://www.intel.com/content/www/us/en/developer/articles/release-notes/intrinsics-guide-release-notes.html)

2. **Update Schedule**
   - **Q1 each year**: Update with performance data from Intel Intrinsics Guide
   - **When new arch released**: Add to Priority 1 or 2 list if data available
   - **Ongoing**: Monitor Intel Intrinsics Guide for new architectures

3. **Data Availability Checklist**
   - [ ] Intel Intrinsics Guide updated with performance data?
   - [ ] XED project updated with instruction definitions?
   - [ ] Intel Software Developer Manual updated with ISA extensions?
   - [ ] Performance data accessible via public APIs/downloads?

### Future Roadmap
**2026 Updates Expected:**
- [ ] Clearwater Forest (2025 server release) - pending data availability
- [ ] Subsequent generations as released

---

## Next Steps
1. **Immediate**:
   - Complete Phase 1 source analysis (prioritize Intrinsics Guide and Ice Lake data)
   - Investigate which Priority 1 architectures have data available in Intel Intrinsics Guide
   - Create Ice Lake TSV from existing icelake.csv

2. **Short-term**:
   - Implement Phase 2 PDF parser port (Option 3: Python CLI wrapper recommended first)
   - Implement Intrinsics Guide XML parser for performance data
   - Generate Performance TSV files for Priority 1 architectures

3. **Medium-term**:
   - Port PDF parser to C# (Phase 2.0)
   - Generate signature files from PDF documentation
   - Validate all generated files and merge with hand-crafted data

4. **Long-term**:
   - Integrate into CI/CD for automatic updates
   - Add Priority 2 and 3 architectures as data becomes available
   - Establish maintenance schedule for annual updates

