# PDF Extraction Flow: AAA Instruction Case Study

## Complete Python intel-doc-2-md Pipeline

```
INPUT: 325462-090-sdm-vol-1-2abcd-3abcd-4.pdf (page 701 contains AAA)
        ↓
[Parser] - PDFMiner extraction
  ├─ Uses pdfminer.pdfparser.PDFParser to read PDF
  ├─ Uses pdfminer.converter.PDFPageAggregator to get page layout
  ├─ Extracts each page as layout object (with text + graphics)
        ↓
[Pile.parse_layout] - Recursive object traversal
  ├─ Stack-based traversal of PDF objects
  ├─ Unwraps containers: LTFigure, LTTextBox, LTTextLine, LTTextBoxHorizontal
  ├─ Extracts text elements as LTTextLineHorizontal objects
  ├─ Extracts vector lines (borders) as LTRect objects
  │  └─ Vertical lines: width < 1.0
  │  └─ Horizontal lines: height < 1.0
  ├─ Stores lines and text with full coordinates (x0, x1, y0, y1)
        ↓
[Pile.split_piles] - Logical grouping
  ├─ Finds tables via vertical lines (table borders)
  ├─ Finds paragraphs (text between tables)
  ├─ Returns sorted piles (top-to-bottom)
        ↓
[Writer.write] - State machine processing
  ├─ Tracks instruction boundaries (via _get_instruction())
  ├─ Generates markdown per pile (table or paragraph)
  ├─ Creates separate file per instruction
        ↓
[Pile.gen_markdown] - Convert piles to markdown
  ├─ Paragraph: _gen_paragraph_markdown()
  │  └─ Extracts title, description, flags, exceptions, etc.
  │  └─ Identifies instruction header via:
  │     ├─ Font: NeoSansIntelMedium
  │     ├─ Height: > 14.5 points
  │     ├─ Contains em-dash (—) or en-dash (–)
  │     └─ Format: "AAA — ASCII Adjust for Add"
  ├─ Table: _gen_table_markdown()
  │  └─ Parses table cells via vertical/horizontal lines
  │  └─ Generates HTML table format
  │  └─ Detects opcode tables (first cell = "Opcode")
        ↓
[Writer._cleanup_hyphens] - Post-processing
  ├─ Removes PDF word-break artifacts
  ├─ 30+ hardcoded patterns: "instruc-\ntions" → "instructions\n"
        ↓
OUTPUT: AAA.md
  └─ Format: <b>AAA</b> — ASCII Adjust for Add
            <table>...</table>
            ### Description
            ...
            ### Flags Affected
            ...
```

---

## Key Components for AAA Extraction

### 1. Instruction Header Detection (_get_instruction)

**Location**: pile.py, lines 235-263

**Logic**:
```python
def _get_instruction(self):
    for text in self.texts:
        fontname = text._objs[0].fontname

        # Filter by font name and height
        if ((text.height > 14.5) and (fontname.endswith('NeoSansIntelMedium'))):
            content = text.get_text().encode('utf8').strip()

            # Look for em-dash (U+2014)
            if (re.search('—', content)):
                tmp = content.split('—')
                instruction = tmp[0].strip()    # "AAA"
                descr = tmp[1]                  # " ASCII Adjust for Add"
                return instruction, descr

            # Fall back to en-dash (U+2013)
            if (re.search('–', content)):
                tmp = content.split('–')
                instruction = tmp[0].strip()
                descr = tmp[1]
                return instruction, descr

    return None, None
```

**For AAA**: Finds text with:
- Font ending in "NeoSansIntelMedium"
- Height > 14.5 pt
- Contains "—" (em-dash)
- Text: "AAA — ASCII Adjust for Add"
- Returns: ("AAA", " ASCII Adjust for Add")

### 2. Table Parsing (_gen_table_intermediate)

**Location**: pile.py, lines 465-495

**Logic**:
1. Extract vertical line coordinates → column boundaries
2. Extract horizontal line coordinates → row boundaries
3. For each cell (defined by row/col intersections):
   - Find all text elements within cell boundaries
   - Handle colspan/rowspan
   - Create intermediate table structure

**For AAA**: Opcode table has structure:
```
| Opcode | Instruction | Op/En | 64-bit | Compat | Description |
|--------|-------------|-------|--------|--------|------------|
| 37     | AAA         | NP    | Valid  | Valid  | ASCII Adjust |
```

### 3. Paragraph Parsing (_gen_paragraph_markdown)

**Location**: pile.py, lines 316-457

**State Machine**:
- **title**: Instruction header line (found by _get_instruction)
- **description**: Text after "Description" section header
- **encoding**: Instruction Operand Encoding section
- **operation**: Operation pseudocode (code block)
- **intrinsics**: C/C++ intrinsics (code block)
- **flags**: Flags Affected section
- **exceptions**: Exception section

**For AAA**:
1. Title: `<b>AAA</b> — ASCII Adjust for Add`
2. Opcode table (parsed as table markdown)
3. Description section (lines 373-420)
4. Flags Affected section (lines 436-439)
5. Exceptions section (lines 441-452)

### 4. Markdown Output Format

**Generated AAA.md**:
```html
<b>AAA</b> — ASCII Adjust for Add
<table>
	<tr>
		<td><b>Opcode</b></td>
		<td><b>Instruction</b></td>
		<td><b>Op/En</b></td>
		<td><b>64-bit Mode</b></td>
		<td><b>Compat/Leg Mode</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td>37</td>
		<td>AAA</td>
		<td>NP</td>
		<td>Valid</td>
		<td>Valid</td>
		<td>ASCII Adjust for Add</td>
	</tr>
</table>

### Description

The AAA (ASCII Adjust for Add) instruction is used to adjust the result of a binary add operation...

### Flags Affected

CF (Carry Flag): Set if the adjustment resulted in a carry
PF (Parity Flag): Undefined
AF (Auxiliary Carry): Set if adjustment was made
ZF (Zero Flag): Undefined
SF (Sign Flag): Undefined
OF (Overflow Flag): Undefined

### Other Exceptions

None
```

---

## Data Flow for AAA.md Generation

### Step 1: PDF Reading (page 701)
Input file: `325462-090-sdm-vol-1-2abcd-3abcd-4.pdf`
- PDFMiner extracts page 701
- Gets layout with text elements and graphics

### Step 2: Object Classification
Text objects on page 701:
- `LTTextLineHorizontal` for each text line (with coordinates)
  - "AAA — ASCII Adjust for Add" (font: NeoSansIntelMedium, height: 15.0)
  - "Opcode" (small font, height: 10.0)
  - "37" (table cell)
  - "Instruction" (table header)
  - "ASCII Adjust for Add" (table cell)
  - "Description", "Flags Affected", etc. (section headers)

- `LTRect` for lines/borders
  - Vertical lines at x0: 47, 80, 120, ... (table column boundaries)
  - Horizontal lines at y0: 650, 630, 610, ... (table row boundaries)

### Step 3: Pile Classification
Objects get grouped into piles:
- **Pile 1** (Table): Contains vertical/horizontal lines + table cell texts
  - Type: "table" (has verticals)
  - Content: Opcode table for AAA
  - Is opcode table: YES (first cell = "Opcode")

- **Pile 2** (Paragraph): No lines, just text
  - Type: "paragraph"
  - Content: "Description", description text lines

- **Pile 3** (Paragraph): Flags section
- **Pile 4** (Paragraph): Exceptions section

### Step 4: Instruction Boundary Detection
Writer.write() loops through piles:
- Pile 1: Calls `_get_instruction()` → finds "AAA — ASCII Adjust for Add"
  - Sets `instruction_curr = "AAA"`
  - Not a new instruction (first pile)
- Pile 2: Calls `_get_instruction()` → returns None
  - Continues with same instruction
- Pile 3: Calls `_get_instruction()` → returns None
  - Continues with same instruction
- Pile 4: Calls `_get_instruction()` → returns None
  - End of piles, closes file

### Step 5: Markdown Generation
For each pile:
- **Pile 1 (Table)**:
  - Calls `_gen_table_markdown()`
  - Calls `_gen_table_intermediate()` to extract table structure
  - Calls `_intermediate_to_markdown()` to generate HTML table
  - Outputs: `<table>...(opcode table)...</table>`

- **Pile 2 (Paragraph)**:
  - Calls `_gen_paragraph_markdown()`
  - State machine: title → description → flags → ...
  - Outputs: `### Description\n...text...`

- **Pile 3 (Paragraph)**:
  - Outputs: `### Flags Affected\n...flags...`

- **Pile 4 (Paragraph)**:
  - Outputs: `### Other Exceptions\n...`

### Step 6: Post-Processing
Writer._cleanup_hyphens():
- Removes word-break artifacts
- Replaces "single- precision" → "single-precision"
- Replaces "•\n" → "\n * " (bullets)

### Step 7: File Output
Writes to: `./output/AAA.md`
- Adds metadata (source, generation date)
- Finalizes file

---

## Critical PDF Features for AAA

### 1. Font Detection
- **NeoSansIntelMedium** at height 15.0+ → Title line
- **Regular font** at height 11.0 → Body text
- **Monospace** at height 10.0 → Code/table headers

### 2. Coordinate System
- Y-axis increases **upward** (PDF standard)
- Top of page: Y = 800
- Bottom of page: Y = 0
- Sorting reverse=True means top-to-bottom

### 3. Line Detection
- **Vertical lines**: width < 1.0 (typically 0.5)
  - Define column boundaries
- **Horizontal lines**: height < 1.0 (typically 0.5)
  - Define row boundaries

### 4. Text Stripping
```python
content = text.get_text().encode('utf8').strip()
```
- `get_text()`: Returns UTF-8 string with all text in element
- `encode('utf8')`: Bytes for processing
- `.strip()`: Removes leading/trailing whitespace
- **Preserves** internal spacing and special characters

---

## Why These Details Matter

### For C# PdfParser Implementation

1. **Font Detection**: Must track fontname in PdfTextElement
   - Check if endswith('NeoSansIntelMedium')
   - Store height for filtering

2. **Line Extraction**: Must classify LTRect as vertical/horizontal
   - width < 1.0 → vertical
   - height < 1.0 → horizontal
   - Store coordinates exactly

3. **Text Coordinates**: Y-axis convention critical
   - Sorting by y0 ascending = left-to-right, bottom-to-top
   - Sorting by y0 descending = left-to-right, top-to-bottom

4. **Table Parsing**: Must find intersections
   - For each (row, col) intersection:
     - Find all texts within boundaries (±0.7 search distance)
     - Build cell content

5. **State Machine**: Track context as processing piles
   - Detect instruction boundaries via _get_instruction()
   - Apply different rendering rules per section

6. **Hyphenation Cleanup**: 30+ patterns must be applied **after** markdown generation
   - Not during PDF parsing
   - Handle both hard-breaks ("instruc-\ntions") and soft-breaks ("single- precision")

---

## Summary

The AAA instruction extraction involves:

1. **PDF Reading**: PDFMiner parses page 701, extracts all objects
2. **Layout Analysis**: Classifies objects (text lines, graphics)
3. **Content Grouping**: Piles organize related objects (tables + paragraphs)
4. **Instruction Detection**: Finds title via font/height/em-dash pattern
5. **Table Parsing**: Extracts opcode table via line boundaries
6. **Markdown Generation**: State machine converts piles to markdown sections
7. **Post-Processing**: Cleans up word-break artifacts
8. **File Output**: Writes AAA.md with complete instruction documentation

**Key insight**: The flow is **top-down by Y-coordinate**. Everything is sorted by Y position (top of page first), and sections are identified sequentially based on text content and position.

For C# implementation in PdfParser.cs:
- Mirror the PyPDF pile-based architecture
- Maintain Y-coordinate sorting (reverse order)
- Implement state machine for section detection
- Apply hyphenation cleanup post-generation
