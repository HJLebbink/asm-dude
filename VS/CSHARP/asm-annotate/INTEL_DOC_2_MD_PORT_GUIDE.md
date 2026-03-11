# intel-doc-2-md Python → C# Porting Guide

## Overview

This document provides a complete guide for porting the **intel-doc-2-md Python PDF parser** to C#/PdfParser.cs, with real-world examples using the AAA instruction from page 701 of the Intel SDM.

---

## Python Project Structure

### intel-doc-2-md (Python)
```
VS/Python/intel-doc-2-md/
├── main.py                           # Entry point
├── inteldoc2md/
│   ├── __init__.py                  # Package definition
│   ├── parser.py                    # PDFMiner integration
│   ├── pile.py                      # Core layout analysis (500+ lines)
│   └── writer.py                    # File output & cleanup
└── pile/
    └── pile.py                      # Pile class definition
```

### Key Files to Port

| File | Purpose | Lines | Key Classes |
|------|---------|-------|-------------|
| pile.py | PDF layout analysis | 600+ | `Pile` (MyPile), `LT*` objects |
| writer.py | State machine, output | 160 | `Writer`, `State` |
| parser.py | PDFMiner integration | 78 | `Parser` |

---

## Pipeline Components to Implement

### 1. Parser (parser.py → PdfDocumentParser.cs)

**Python Implementation**:
```python
class Parser(object):
    def __init__(self, filename):
        self._document = self._read_file(filename)
        self._device, self._interpreter = self._prepare_tools()

    def extract(self, page_num_start=None, page_num_end=None):
        for page in PDFPage.create_pages(self._document):
            self._interpreter.process_page(page)
            layout = self._device.get_result()
            self._pages[counter] = layout

    def parse(self, page_num=None):
        piles = []
        for page_num, page in self._pages.items():
            piles += self._parse_page(page)
        return piles

    def _parse_page(self, page):
        pile = Pile()
        pile.parse_layout(page)
        piles = pile.split_piles()
        return piles
```

**C# Target (already started in PdfParser.cs)**:
```csharp
public class PdfDocumentParser
{
    private readonly string _filePath;

    public PdfDocumentParser(string filePath)
    {
        _filePath = filePath;
    }

    public List<ContentPile> ParseDocument()
    {
        var allPiles = new List<ContentPile>();

        using (var pdfDocument = new PdfDocument(new PdfReader(_pdfPath)))
        {
            for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
            {
                var piles = ParsePage(pdfDocument, pageNum);
                allPiles.AddRange(piles);
            }
        }

        return allPiles;
    }

    private List<ContentPile> ParsePage(PdfDocument pdfDocument, int pageNum)
    {
        var pile = new ContentPile();
        var page = pdfDocument.GetPage(pageNum);

        // Extract text and lines
        var textElements = ExtractTextElements(page);
        var lineElements = ExtractLineElements(page);

        // Populate pile
        pile.TextElements.AddRange(textElements);
        foreach (var line in lineElements)
        {
            if (line.IsVertical)
                pile.VerticalLines.Add(line);
            else if (line.IsHorizontal)
                pile.HorizontalLines.Add(line);
        }

        // Split into logical piles
        return pile.SplitIntoPiles();
    }
}
```

**Critical**: Must preserve pdfminer-like object hierarchy:
- `TextElement` ≈ `LTTextLineHorizontal`
- `LineElement` ≈ `LTRect`
- `ContentPile` ≈ `MyPile`

### 2. Pile (pile.py → ContentPile.cs)

**Core Methods to Implement**:

| Python Method | C# Equivalent | Purpose |
|---|---|---|
| `parse_layout()` | `ParsePageLayout()` | Stack-based object traversal |
| `split_piles()` | `SplitIntoPiles()` | Logical grouping (tables + paragraphs) |
| `_get_instruction()` | `GetInstruction()` | Font/height-based title detection |
| `_gen_table_markdown()` | `GenerateTableMarkdown()` | Table → HTML conversion |
| `_gen_paragraph_markdown()` | `GenerateParagraphMarkdown()` | Paragraph → state machine |
| `_find_tables()` | `FindTables()` | Vertical lines → table detection |
| `_find_paragraphs()` | `FindParagraphs()` | Text grouping between tables |

**Python Pile Class**:
```python
class MyPile(object):
    def __init__(self):
        self.verticals = []         # LTRect (lines, width < 1.0)
        self.horizontals = []       # LTRect (lines, height < 1.0)
        self.texts = []             # LTTextLineHorizontal (text elements)
        self.images = []            # LTImage

    def get_type(self):
        if self.verticals:
            return 'table'
        elif self.images:
            return 'image'
        else:
            return 'paragraph'

    def _get_instruction(self):
        # Find text with:
        # - Font: NeoSansIntelMedium
        # - Height: > 14.5
        # - Contains: em-dash (—) or en-dash (–)
        # Returns: (mnemonic, description)
```

**C# Target (ContentPile.cs)**:
```csharp
public class ContentPile
{
    public List<PdfLineElement> VerticalLines { get; } = [];
    public List<PdfLineElement> HorizontalLines { get; } = [];
    public List<PdfTextElement> TextElements { get; } = [];

    public string GetType()
    {
        if (VerticalLines.Count > 0) return "table";
        return "paragraph";
    }

    public (string Mnemonic, string Description) GetInstruction()
    {
        foreach (var text in TextElements)
        {
            if (text.Height <= 14.5) continue;
            if (!text.FontName?.EndsWith("NeoSansIntelMedium") ?? false) continue;

            string content = text.GetText();

            // Try em-dash
            if (content.Contains("—"))
            {
                var parts = content.Split('—');
                return (parts[0].Trim(), parts[1].Trim());
            }

            // Try en-dash
            if (content.Contains("–"))
            {
                var parts = content.Split('–');
                return (parts[0].Trim(), parts[1].Trim());
            }
        }

        return (null, null);
    }

    public List<ContentPile> SplitIntoPiles()
    {
        var tables = FindTables();
        var paragraphs = FindParagraphs(tables);

        var piles = new List<ContentPile>();
        piles.AddRange(tables);
        piles.AddRange(paragraphs);

        // Sort by Y position (top to bottom)
        return piles.OrderByDescending(p => p.TextElements.FirstOrDefault()?.Y0 ?? 0.0).ToList();
    }
}
```

### 3. Writer (writer.py → MarkdownGenerator.cs)

**State Machine** - Track parsing context:

```csharp
public enum ParsingState
{
    None,
    Title,           // Instruction header line
    Description,     // Text under "Description"
    Encoding,        // Instruction Operand Encoding section
    Operation,       // Pseudocode block
    Intrinsics,      // C/C++ intrinsics
    Flags,          // Flags Affected section
    Exceptions      // Exception information
}

public class MarkdownState
{
    public ParsingState CurrentState { get; set; }
    public ParsingState NextState { get; set; }
    public bool InCodeBlock { get; set; }
    public bool IsPrevPileOpcodeTable { get; set; }
    public bool IsCurrPileOpcodeTable { get; set; }
    public bool IsNextPileOpcodeTable { get; set; }
}
```

**Python Writer Logic**:
```python
def write(self, piles):
    state = State()
    markdown = ''

    for i in range(0, len(piles)):
        pile = piles[i]
        pileInstruction, descr = pile._get_instruction()

        if pileInstruction != None:
            if pileInstruction != instruction_curr:
                # New instruction, write previous file
                self.close_file(instruction_prev, markdown)
                markdown = ''

        # Determine opcode table status
        state.curr_pile_is_opcode_table = pile._is_opcode_table()
        if state.curr_pile_is_opcode_table:
            state.prev_pile_is_opcode_table = Writer._find_prev_opcode_table(i, piles, instruction_curr)
            state.next_pile_is_opcode_table = Writer._find_next_opcode_table(i, piles, instruction_curr)

        # Generate markdown for this pile
        markdown += pile.gen_markdown(state)

    self.close_file(instruction_curr, markdown)
```

**C# Target**:
```csharp
public class MarkdownGenerator
{
    public string GenerateForPiles(List<ContentPile> piles)
    {
        var sb = new StringBuilder();
        var state = new MarkdownState();
        string currentInstruction = null;

        for (int i = 0; i < piles.Count; i++)
        {
            var pile = piles[i];
            var (instruction, description) = pile.GetInstruction();

            if (instruction != null && instruction != currentInstruction)
            {
                // Instruction boundary (will be handled by writer)
                currentInstruction = instruction;
            }

            // Detect opcode tables
            state.IsCurrPileOpcodeTable = IsOpcodeTable(pile);
            if (state.IsCurrPileOpcodeTable)
            {
                state.IsPrevPileOpcodeTable = HasPrevOpcodeTable(i, piles, currentInstruction);
                state.IsNextPileOpcodeTable = HasNextOpcodeTable(i, piles, currentInstruction);
            }

            // Generate markdown
            if (pile.GetType() == "table")
                sb.Append(GenerateTableMarkdown(pile, state));
            else
                sb.Append(GenerateParagraphMarkdown(pile, state));
        }

        return sb.ToString();
    }
}
```

---

## Real-World Example: AAA Extraction

### Input
- **File**: `325462-090-sdm-vol-1-2abcd-3abcd-4.pdf`
- **Page**: 701
- **Content**: AAA (ASCII Adjust After Addition) instruction

### Expected Output: AAA.md

```html
<b>AAA</b> — ASCII Adjust After Addition
<table>
	<tr>
		<td><b>Opcode</b></td>
		<td><b>Instruction</b></td>
		<td><b>Op/En</b></td>
		<td><b>64-Bit Mode</b></td>
		<td><b>Compat/Leg Mode</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td>37</td>
		<td>AAA</td>
		<td>ZO</td>
		<td>Invalid</td>
		<td>Valid</td>
		<td>ASCII adjust AL after addition.</td>
	</tr>
</table>

### Description

Adjusts the sum of two unpacked BCD values to create an unpacked BCD result...

### Operation

```java
IF 64-Bit Mode
THEN
#UD;
ELSE
IF ((AL AND 0FH) > 9) or (AF = 1)
...
```

### Flags Affected

The AF and CF flags are set to 1 if the adjustment results in a decimal carry...
```

---

## Implementation Checklist

### Phase 1: Core Data Structures ✅ (DONE)
- [x] `PdfTextElement` - Text with coordinates
- [x] `PdfLineElement` - Vector lines
- [x] `ContentPile` - Logical grouping
- [x] `TextCleaner` - Hyphenation cleanup

### Phase 2: PDF Parsing (TODO)
- [ ] Implement `ParsePageLayout()` - Stack-based traversal
- [ ] Implement `ExtractTextElements()` - Font/height tracking
- [ ] Implement `ExtractLineElements()` - Vertical/horizontal classification
- [ ] Add coordinate system handling (Y-axis direction)

### Phase 3: Table Parsing (TODO)
- [ ] Implement `FindTables()` - Vertical line grouping
- [ ] Implement `FindNearVerticals()` - Connected line detection
- [ ] Implement `GenerateTableMarkdown()- HTML table generation
- [ ] Test with AAA opcode table

### Phase 4: Paragraph Parsing (TODO)
- [ ] Implement `GenerateParagraphMarkdown()` - State machine
- [ ] Implement section header detection (Description, Flags, etc.)
- [ ] Implement code block handling (Operation section)
- [ ] Test with AAA description/flags/exceptions

### Phase 5: Integration (TODO)
- [ ] Implement `MarkdownGenerator` - Full pipeline
- [ ] Implement instruction boundary detection
- [ ] Test round-trip: PDF → Pile → Markdown
- [ ] Compare with existing wiki files

### Phase 6: Validation (TODO)
- [ ] Extract all instructions from SDM
- [ ] Compare generated files with 709 existing wiki files
- [ ] Validate table structures
- [ ] Validate section content

---

## Key Differences: Python vs C#

### 1. PDF Library
| Python | C# |
|--------|-----|
| pdfminer | itext7 |
| `PDFPage.create_pages()` | `PdfDocument.GetPage(n)` |
| `LTTextLineHorizontal` | Custom `PdfTextElement` |
| `LTRect` | Custom `PdfLineElement` |

### 2. Text Extraction
| Python | C# |
|--------|-----|
| `text.get_text()` | `PdfTextExtractor.GetTextFromPage()` |
| `text._objs[0].fontname` | Track FontName in extraction |
| `text.height` | Track Height in extraction |

### 3. Coordinate System
| Python | C# |
|--------|-----|
| Y increases upward | Same (PDF standard) |
| Sort reverse=True | OrderByDescending(y => y) |
| `float('-inf')` / `float('inf')` | `double.NegativeInfinity` / `double.PositiveInfinity` |

### 4. String Operations
| Python | C# |
|--------|-----|
| `text.encode('utf8').strip()` | `text.Trim()` |
| `re.search('—', text)` | `text.Contains("—")` |
| `text.split('—')` | `text.Split('—')` |
| `str.replace(old, new)` | `str.Replace(old, new)` |

### 5. Collections
| Python | C# |
|--------|-----|
| `list()` | `new List<T>()` |
| `set()` | `new HashSet<T>()` |
| `dict()` | `new Dictionary<K, V>()` |
| `.append()` | `.Add()` |
| `.extend()` | `.AddRange()` |

### 6. Regex
| Python | C# |
|--------|-----|
| `re.search(pattern, text)` | `Regex.IsMatch(text, pattern)` |
| `re.match()` | `Regex.Match()` |
| `re.sub()` | `Regex.Replace()` |

---

## Critical Implementation Notes

### 1. Font Detection is Key
Only texts with:
- Fontname ending in "NeoSansIntelMedium"
- Height > 14.5 points
- Containing em-dash (—) or en-dash (–)

Should be treated as instruction headers. This prevents false positives from:
- Page numbers
- Footers
- Table headers
- Small section headers

### 2. Y-Coordinate Sorting
```csharp
// Python: sorted(..., reverse=True, key=lambda x: x.y0)
// C#:
var sorted = piles.OrderByDescending(p => p.TextElements.FirstOrDefault()?.Y0 ?? 0.0).ToList();
```

This sorts top-to-bottom (largest Y first, which is top of page).

### 3. Search Distances
```csharp
const double SEARCH_DISTANCE_VERTICAL = 1.0;      // X coordinate tolerance
const double SEARCH_DISTANCE_HORIZONTAL = 8.0;    // Y coordinate tolerance
```

Used when finding cell boundaries. Allows for small PDF rendering offsets.

### 4. Hyphenation Cleanup
**Must be applied AFTER markdown generation**, not during extraction.

30+ patterns:
```
"instruc-\ntions" → "instructions\n"
"single- precision" → "single-precision"
"•\n" → "\n * "
// ... 27 more patterns
```

### 5. Table Header Detection
First row of table is header if:
- First cell text == "Opcode"
- First cell text == "Instruction"

Headers get `<b>` tags in C#:
```csharp
if (isHeaderRow)
    sb.AppendLine($"<td><b>{content}</b></td>");
else
    sb.AppendLine($"<td>{content}</td>");
```

---

## Testing Strategy

### Unit Tests
```csharp
[Test]
public void TestGetInstruction_WithEmDash()
{
    var pile = new ContentPile();
    var text = new PdfTextElement
    {
        Content = "AAA—ASCII Adjust After Addition",
        FontName = "NeoSansIntelMedium",
        Height = 15.0
    };
    pile.TextElements.Add(text);

    var (mnem, desc) = pile.GetInstruction();
    Assert.AreEqual("AAA", mnem);
    Assert.AreEqual("ASCII Adjust After Addition", desc);
}

[Test]
public void TestTableDetection()
{
    var pile = new ContentPile();
    var vertical = new PdfLineElement { Width = 0.5 };
    pile.VerticalLines.Add(vertical);

    Assert.AreEqual("table", pile.GetType());
}
```

### Integration Tests
```csharp
[Test]
public void TestAAA_FullExtraction()
{
    var parser = new PdfDocumentParser(pdfPath);
    var piles = parser.ParseDocument();

    // Find AAA instruction
    var aaaPile = piles.FirstOrDefault(p => p.GetInstruction().Mnemonic == "AAA");
    Assert.IsNotNull(aaaPile);

    var markdown = aaaPile.GenerateMarkdown(new MarkdownState());
    Assert.Contains("<b>AAA</b>", markdown);
    Assert.Contains("ASCII Adjust", markdown);
}
```

### Validation
Compare generated files with existing wiki:
```csharp
var generated = File.ReadAllText("AAA.md");
var existing = File.ReadAllText("asm-dude.wiki/doc/AAA.md");

// Should match (after normalizing whitespace)
Assert.AreEqual(Normalize(existing), Normalize(generated));
```

---

## Summary

Porting intel-doc-2-md from Python to C# requires:

1. **PDF Extraction**: Replace pdfminer with itext7
2. **Layout Analysis**: Implement Pile-like structure with text/line grouping
3. **Instruction Detection**: Font-based title detection (NeoSansIntelMedium + height)
4. **Table Parsing**: Vertical/horizontal line-based cell extraction
5. **Markdown Generation**: State machine for section detection
6. **Post-Processing**: 30+ hyphenation patterns
7. **Testing**: Validate against 709 existing wiki files

**Key Insight**: The Python code is well-structured and tested. By following its architecture closely, the C# port will work correctly with real Intel PDFs.

**Reference**: See `AAA_EXTRACTION_GUIDE.md` for detailed step-by-step example with actual page 701 content.
