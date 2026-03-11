# intel-doc-2-data Integration Plan

## Overview

The `intel-doc-2-data` project contains critical logic for parsing wiki markdown files and converting them to AsmDude signature data. Rather than reimplementing this logic, we should:

1. **Extract** reusable components from intel-doc-2-data into a shared library
2. **Integrate** this library into asm-annotate
3. **Extend** it with markdown generation and performance data merging

---

## Current intel-doc-2-data Architecture

**Location**: `C:\Source\Github\asm-dude\VS\CSHARP\intel-doc-2-data\Program.cs`

**Monolithic Design**: All logic is in `Program.cs` (440 lines)

**Key Responsibilities**:
1. **File I/O**: Read markdown files from wiki directory
2. **Markdown Parsing**: Extract description and HTML table
3. **Table Parsing**: Convert HTML table to rows/columns
4. **Data Extraction**: Parse mnemonic, parameters, architecture
5. **Data Generation**: Create signature file format
6. **Output**: Write to hardcoded paths

**Key Methods**:
- `Parse()` - Main entry point
- `To_Signature()` - Table → Signature conversion
- `Parse_Parameters()` - Extract mnemonic and operand syntax
- `Cleanup_Parameters()` - Normalize register names
- `Parse_Archs()` - Map architecture strings to enum
- `Parse_Table()` - Extract HTML table structure

---

## Refactoring Strategy

### Step 1: Create Shared Library (`asm-annotate/MarkdownParser.cs`)

Extract these classes from intel-doc-2-data:

```csharp
namespace AsmAnnotate
{
    /// <summary>
    /// Represents a single instruction signature from a wiki markdown file.
    /// </summary>
    public class InstructionSignature
    {
        public Mnemonic Mnemonic { get; set; }
        public string Parameters { get; set; }
        public string ParameterDescriptions { get; set; }
        public IList<Arch> Architectures { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Format as signature file line: MNEMONIC\tPARAMS\tARCHES\tPARAM_DESCR\tDESCRIPTION
        /// </summary>
        public override string ToString() { ... }
    }

    /// <summary>
    /// Parses wiki markdown files containing instruction documentation.
    ///
    /// Format:
    /// <b>MNEMONIC</b> — Description
    /// <table>
    ///   <tr><td>Opcode</td><td>Instruction</td>...</tr>
    ///   <tr><td>04 ib</td><td>ADD AL, imm8</td>...</tr>
    /// </table>
    /// </summary>
    public class WikiMarkdownParser
    {
        private readonly string _filePath;

        public WikiMarkdownParser(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Wiki file not found: {filePath}");
            _filePath = filePath;
        }

        /// <summary>
        /// Parse a single wiki markdown file and extract all instruction signatures.
        /// </summary>
        public (string Description, IList<InstructionSignature> Signatures) Parse()
        {
            string content = File.ReadAllText(_filePath);
            return ParseContent(content);
        }

        /// <summary>
        /// Parse markdown content string.
        /// </summary>
        public static (string Description, IList<InstructionSignature> Signatures) ParseContent(string content)
        {
            // 1. Extract description (text before <table>)
            int tableStart = content.IndexOf("<table>");
            string descriptionSection = content[..tableStart];
            int hyphenPos = FindFirstDashPosition(descriptionSection);
            string description = descriptionSection[(hyphenPos + 1)..].Trim()
                .Replace("\r\n", " ");

            // 2. Extract table content
            int tableEnd = content.IndexOf("</table>");
            string tableContent = content[tableStart..tableEnd].Replace("<table>", "");
            var tableRows = ParseTableRows(tableContent);

            // 3. Convert table to signatures
            var signatures = ConvertTableToSignatures(tableRows);

            return (description, signatures);
        }

        // Internal helper methods (extracted from intel-doc-2-data):
        // - FindFirstDashPosition()
        // - ParseTableRows()
        // - ParseTableCells()
        // - ConvertTableToSignatures()
        // - ParseParameters()
        // - CleanupParameters()
        // - ParseArchitectures()
        // - InferArchitectureFromOperands()
    }

    /// <summary>
    /// Generates signature file output from instruction data.
    /// </summary>
    public class SignatureFileGenerator
    {
        public string GenerateSignatureFileContent(
            IEnumerable<string> fileDescriptions,
            IEnumerable<InstructionSignature> allSignatures)
        {
            var sb = new StringBuilder();

            foreach (var sig in allSignatures.GroupBy(s => s.Mnemonic))
            {
                sb.AppendLine(";------- " + sig.Key);

                foreach (var signature in sig)
                {
                    sb.AppendLine("GENERAL\t" + signature.Mnemonic + "\t" +
                                  "[description]\t" + sig.Key);
                    sb.AppendLine(signature.ToString());
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Generates wiki markdown HTML overview tables.
    /// </summary>
    public class WikiOverviewGenerator
    {
        public string GenerateOverviewHtml(
            IEnumerable<(string Mnemonic, string Description, string Architectures)> instructions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");

            foreach (var (mnemonic, description, archs) in instructions)
            {
                sb.AppendLine($"<tr><td><a href=\"wiki/{mnemonic}\">{mnemonic}</a></td>" +
                              $"<td>{description}</td><td>{archs}</td></tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }
    }
}
```

### Step 2: Refactor intel-doc-2-data to Use Shared Library

**Before**: 440-line monolithic `Program.cs`
**After**: 50-line `Program.cs` that uses `WikiMarkdownParser` and `SignatureFileGenerator`

```csharp
static class Program
{
    static void Main(string[] args)
    {
        string wikiPath = "C:/Source/Github/asm-dude.wiki/doc";
        var allSignatures = new List<InstructionSignature>();
        var descriptions = new Dictionary<string, string>();

        foreach (var filePath in Directory.EnumerateFiles(wikiPath, "*.md").OrderBy(f => f))
        {
            var parser = new WikiMarkdownParser(filePath);
            var (description, signatures) = parser.Parse();

            string mnemonic = Path.GetFileNameWithoutExtension(filePath);
            descriptions[mnemonic] = description;

            allSignatures.AddRange(signatures);
        }

        // Generate outputs
        var sigGen = new SignatureFileGenerator();
        string sigContent = sigGen.GenerateSignatureFileContent(descriptions.Values, allSignatures);

        var overviewGen = new WikiOverviewGenerator();
        string overviewContent = overviewGen.GenerateOverviewHtml(/* ... */);

        File.WriteAllText(@"C:\Temp\VS\signature.txt", sigContent);
        File.WriteAllText(@"C:\Temp\VS\overview.html", overviewContent);
    }
}
```

### Step 3: Add Markdown Generation to asm-annotate

Create `MarkdownGenerator.cs` that produces wiki-format from `ContentPile`:

```csharp
namespace AsmAnnotate
{
    /// <summary>
    /// Generates wiki-format markdown files from parsed PDF content.
    /// Output format matches intel-doc-2-md and existing 709 wiki files.
    /// </summary>
    public class MarkdownGenerator
    {
        /// <summary>
        /// Generate markdown content from a collection of instruction piles.
        /// </summary>
        public string GenerateMarkdown(string mnemonic, string description,
                                       IEnumerable<InstructionData> instructions)
        {
            var sb = new StringBuilder();

            // Header: <b>MNEMONIC</b> — Description
            sb.AppendLine($"<b>{mnemonic}</b> — {description}");
            sb.AppendLine("<table>");

            // Table header
            sb.AppendLine("\t<tr>");
            sb.AppendLine("\t\t<td><b>Opcode</b></td>");
            sb.AppendLine("\t\t<td><b>Instruction</b></td>");
            sb.AppendLine("\t\t<td><b>Op/ En</b></td>");
            sb.AppendLine("\t\t<td><b>64-bit Mode</b></td>");
            sb.AppendLine("\t\t<td><b>Compat/ Leg Mode</b></td>");
            sb.AppendLine("\t\t<td><b>Description</b></td>");
            sb.AppendLine("\t</tr>");

            // Table rows from instruction data
            foreach (var instr in instructions)
            {
                sb.AppendLine("\t<tr>");
                sb.AppendLine($"\t\t<td>{instr.Opcode}</td>");
                sb.AppendLine($"\t\t<td>{instr.Operands}</td>");
                sb.AppendLine($"\t\t<td>{instr.OperandEncoding}</td>");
                sb.AppendLine($"\t\t<td>{instr.Mode64}</td>");
                sb.AppendLine($"\t\t<td>{instr.ModeCompat}</td>");
                sb.AppendLine($"\t\t<td>{instr.Description}</td>");
                sb.AppendLine("\t</tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }
    }
}
```

### Step 4: Integrate into asm-annotate Pipeline

Update Program.cs:

```csharp
static void Main(string[] args)
{
    // Phase 1: Parse PDFs → Generate ContentPiles
    var pdfParser = new PdfDocumentParser("path/to/Intel-SDM.pdf");
    var piles = pdfParser.ParseDocument();

    // Phase 2: Generate Markdown
    var mdGen = new MarkdownGenerator();
    foreach (var pile in piles)
    {
        string mnemonic = pile.GetInstruction().Mnemonic;
        string description = pile.GetInstruction().Description;
        string markdown = mdGen.GenerateMarkdown(mnemonic, description, pile.GetInstructions());

        File.WriteAllText($"wiki/{mnemonic}.md", markdown);
    }

    // Phase 3: Parse Markdown → Signatures
    var sigGen = new SignatureFileGenerator();
    var allSignatures = new List<InstructionSignature>();

    foreach (var filePath in Directory.EnumerateFiles("wiki", "*.md"))
    {
        var parser = new WikiMarkdownParser(filePath);
        var (desc, sigs) = parser.Parse();
        allSignatures.AddRange(sigs);
    }

    // Phase 4: Merge Performance Data
    var perfMerger = new PerformanceMerger();
    var merged = perfMerger.MergeWithPerformanceData(allSignatures,
        performanceSources: new[] {
            new PerformanceTsvParser("Haswell.tsv", "Haswell"),
            new PerformanceCsvParser("icelake.csv", "IceLake")
        });

    // Phase 5: Output
    var sigOutput = sigGen.GenerateSignatureFileContent(/* ... */);
    File.WriteAllText("signature-combined.txt", sigOutput);
}
```

---

## Current intel-doc-2-data Code Structure

### Main Parser Method
```csharp
static (string Description, IList<Signature> Signatures) Parse(string content)
{
    // Step 1: Find table boundary
    int pos_Start_Table = content.IndexOf("<table>");
    string substr1 = content[..pos_Start_Table];

    // Step 2: Extract description
    int pos_Hyphen = Find_First_Hyphen_Position(substr1);
    string Description = substr1[(pos_Hyphen + 1)..].Trim()
        .Replace("\r\n", " ");

    // Step 3: Parse table
    int pos_End_Table = content.IndexOf("</table>");
    var table = Parse_Table(content[pos_Start_Table..pos_End_Table]
        .Replace("<table>", ""));

    // Step 4: Convert to signatures
    var signatures = To_Signature(table);

    return (Description, signatures);
}
```

### Table Format Detection
```csharp
static IList<Signature> To_Signature(IList<IList<string>> table)
{
    // Detect column layout (3, 4, 5, or 6 columns)
    // Determine which column contains:
    //   - Mnemonic/Instruction
    //   - Architecture
    //   - Description
    //
    // Handle missing arch column by inferring from operands:
    //   R64 → X64
    //   R32 → 386
    //   Default → 8086
}
```

### Parameter Parsing
```csharp
static (Mnemonic mnemonic, string Parameters, string ParamDescriptions)
    Parse_Parameters(string str)
{
    // Extract mnemonic from string like "ADD RAX, IMM32"
    // Return tuple: (Mnemonic.ADD, "R64,IMM32", "ADD RAX,IMM32")
}
```

---

## Files to Create in asm-annotate

### New Files
1. **MarkdownParser.cs** (500 lines)
   - Extract parsing logic from intel-doc-2-data
   - `WikiMarkdownParser` class
   - `InstructionSignature` class

2. **MarkdownGenerator.cs** (200 lines)
   - Generate wiki-format markdown
   - Input: `InstructionData` from PdfParser
   - Output: `.md` files matching existing wiki structure

3. **SignatureFileGenerator.cs** (100 lines)
   - Generate signature file format
   - `SignatureFileGenerator` class
   - `WikiOverviewGenerator` class

4. **PerformanceMerger.cs** (300 lines)
   - Merge signature data with performance metrics
   - Combine CSV/TSV/XML data sources
   - Output unified data files

### Modified Files
1. **asm-annotate.csproj**
   - Add reference to asm-tools-lib (for Mnemonic, Arch enums)
   - Add reference to intel-doc-2-data (to share library)

2. **Program.cs**
   - Update to use all new components
   - Implement full pipeline

### Refactored Files
1. **intel-doc-2-data/Program.cs**
   - Reduce to ~50 lines
   - Use `WikiMarkdownParser` and `SignatureFileGenerator`
   - Remove parsing logic (move to library)

---

## Integration Timeline

### Session 1 (Now)
- ✅ Analyze intel-doc-2-data architecture
- ✅ Document existing code structure
- ⏳ Extract parsing logic into MarkdownParser.cs

### Session 2
- ⏳ Implement WikiMarkdownParser class
- ⏳ Test with existing wiki files
- ⏳ Implement SignatureFileGenerator

### Session 3
- ⏳ Implement MarkdownGenerator (PDF→Markdown)
- ⏳ Test round-trip: PDF→MD→Signature
- ⏳ Compare with existing 709 wiki files

### Session 4
- ⏳ Implement PerformanceMerger
- ⏳ Merge CSV/TSV performance data
- ⏳ Generate final output files

### Session 5
- ⏳ Full integration testing
- ⏳ Validate against AsmDude extension
- ⏳ Modernize intel-doc-2-data to use library

---

## Code Extraction Checklist

- [ ] Extract `Signature` struct → `InstructionSignature` class
- [ ] Extract `Parse()` → `WikiMarkdownParser.ParseContent()`
- [ ] Extract `To_Signature()` → Core conversion logic
- [ ] Extract `Parse_Parameters()` → Parameter parsing
- [ ] Extract `Cleanup_Parameters()` → Register normalization
- [ ] Extract `Parse_Archs()` → Architecture mapping
- [ ] Extract `Parse_Table()` → Table structure parsing
- [ ] Extract `Parse_Table_Row()` → Row parsing
- [ ] Extract `Parse_Table_Cells()` → Cell parsing
- [ ] Extract `Find_First_Hyphen_Position()` → Delimiter finding
- [ ] Extract `InferArchitectureFromOperands()` → Operand analysis
- [ ] Extract `CleanupDescriptions()` → Text normalization
- [ ] Create SignatureFileGenerator class
- [ ] Create WikiOverviewGenerator class

---

## Testing Strategy

### Unit Tests
1. **WikiMarkdownParser**
   - Test each table format variation (3, 4, 5, 6 columns)
   - Test architecture inference
   - Test parameter cleanup
   - Test description extraction

2. **SignatureFileGenerator**
   - Test signature file format
   - Test architecture mapping
   - Test mnemonic grouping

3. **MarkdownGenerator**
   - Test HTML table generation
   - Test header formatting
   - Test cell escaping

### Integration Tests
1. **Round-trip**: Parse existing wiki → Generate markdown → Compare
2. **Compatibility**: Ensure refactored intel-doc-2-data produces identical output
3. **Pipeline**: PDF → Markdown → Signature → Verify against baseline

### Validation
1. **Baseline Comparison**: Against 709 existing wiki files
2. **Signature Count**: Verify instruction coverage
3. **Architecture Support**: Validate all architectures represented

---

## Dependencies & Compatibility

### AsmTools Library
- `Mnemonic` enum (400+ values)
- `Arch` enum (30+ values)
- `ArchTools` utility class
- `Operand` class (for parameter parsing)

### Already Available
- ✅ asm-tools-lib (referenced by both projects)
- ✅ File I/O (System.IO)
- ✅ Collections (System.Collections.Generic)
- ✅ Regular expressions (System.Text.RegularExpressions)

### No New Dependencies Required
- intel-doc-2-data uses only .NET standard libraries
- Can be extracted without introducing new NuGet packages

---

## Risks & Mitigation

### Risk 1: Breaking Changes in intel-doc-2-data
**Mitigation**: Create comprehensive unit tests before refactoring

### Risk 2: Table Format Variations
**Mitigation**: Test with all 709 existing wiki files during refactoring

### Risk 3: Operand Inference Accuracy
**Mitigation**: Validate against AsmTools enum values, fallback to X64

### Risk 4: Performance Data Conflicts
**Mitigation**: Implement conflict resolution strategy (priority order: explicit > inferred)

---

## Summary

The intel-doc-2-data project is **mission-critical** for AsmDude data pipelines. By extracting its parsing logic into a reusable library, we can:

1. **Reduce duplication** - Single source of truth for wiki parsing
2. **Improve maintainability** - Modular components instead of monolithic Program.cs
3. **Enable new functionality** - Add markdown generation, performance merging
4. **Facilitate testing** - Unit test individual components
5. **Support modern architectures** - Extend for Ice Lake, Raptor Lake, etc.

The refactoring should be **non-breaking** - intel-doc-2-data should continue to work with identical output after migration to the library.
