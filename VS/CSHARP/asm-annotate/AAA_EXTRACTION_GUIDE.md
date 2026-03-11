# AAA Instruction Extraction Guide

## Real-World Example: Extracting AAA from Page 701

This document shows the actual extraction process for the AAA (ASCII Adjust After Addition) instruction, demonstrating how intel-doc-2-md Python code works and how to port it to C#.

---

## Step-by-Step Extraction Process

### 1. PDF Source (Page 701)

**File**: `325462-090-sdm-vol-1-2abcd-3abcd-4.pdf`
**Page**: 701
**Content**: Complete AAA instruction definition

**Raw extracted text** (what PdfTextExtractor gets):
```
AAA—ASCII Adjust After Addition Vol. 2A 3-1
AAA—ASCII Adjust After Addition
Instruction Operand Encoding
Description
Adjusts the sum of two unpacked BCD values to create an unpacked BCD result. The AL register is the implied
source and destination operand for this instruction. The AAA instruction is only useful when it follows an ADD
instruction that adds (binary addition) two unpacked BCD values and stores a byte result in the AL register. The
AAA instruction then adjusts the contents of the AL register to contain the correct 1-digit unpacked BCD result.
If the addition produces a decimal carry, the AH register increments by 1, and the CF and AF flags are set. If there
was no decimal carry, the CF and AF flags are cleared and the AH register is unchanged. In either case, bits 4
through 7 of the AL register are set to 0.
This instruction executes as described in compatibility mode and legacy mode. It is not valid in 64-bit mode.
Operation
IF 64-Bit Mode
THEN
#UD;
ELSE
IF ((AL AND 0FH) > 9) or (AF = 1)
THEN
AX := AX + 106H;
AF := 1;
CF := 1;
ELSE
AF := 0;
CF := 0;
FI;
AL := AL AND 0FH;
FI;
Flags Affected
The AF and CF flags are set to 1 if the adjustment results in a decimal carry; otherwise they are set to 0. The OF,
SF, ZF, and PF flags are undefined.
Protected Mode Exceptions
#UD If the LOCK prefix is used.
Real-Address Mode Exceptions
Same exceptions as protected mode.
Virtual-8086 Mode Exceptions
Same exceptions as protected mode.
Opcode Instruction Op/ En 64-bit Mode Compat/Leg Mode Description
37 AAA ZO Invalid Valid ASCII adjust AL after addition.
Op/En Operand 1 Operand 2 Operand 3 Operand 4
ZO N/A N/A N/A N/A
```

**Character count**: 1,655 characters
**Line count**: 47 lines

---

### 2. How Python intel-doc-2-md Extracts AAA

#### Step 2.1: Parse Layout (pile.py: parse_layout)

```python
def parse_layout(self, layout):
    obj_stack = list(reversed(list(layout)))
    while obj_stack:
        obj = obj_stack.pop()

        # Unwrap containers
        if type(obj) in [LTFigure, LTTextBox, LTTextLine, LTTextBoxHorizontal]:
            obj_stack.extend(reversed(list(obj)))

        # Extract text lines
        elif type(obj) == LTTextLineHorizontal:
            self.texts.append(obj)  # Each text line with coordinates (x0, x1, y0, y1)

        # Extract borders (table lines)
        elif type(obj) == LTRect:
            if obj.width < 1.0:
                self.verticals.append(obj)    # Vertical lines (table columns)
            elif obj.height < 1.0:
                self.horizontals.append(obj)  # Horizontal lines (table rows)
```

**For AAA**: Gets all text elements and lines from page 701
- Text elements: "AAA—ASCII Adjust After Addition", "Description", "Flags Affected", etc.
- Vertical lines: Borders defining opcode table columns
- Horizontal lines: Borders defining opcode table rows

#### Step 2.2: Detect Instruction Header (_get_instruction)

```python
def _get_instruction(self):
    for text in self.texts:
        fontname = text._objs[0].fontname

        # Filter by font and height
        if ((text.height > 14.5) and (fontname.endswith('NeoSansIntelMedium'))):
            content = text.get_text().encode('utf8').strip()

            # Split on em-dash (U+2014)
            if (re.search('—', content)):
                tmp = content.split('—')
                instruction = tmp[0].strip()    # "AAA"
                descr = tmp[1].strip()           # "ASCII Adjust After Addition"
                return instruction, descr
```

**For AAA**:
- Finds text: "AAA—ASCII Adjust After Addition"
- Fontname ends with: "NeoSansIntelMedium"
- Height: 15.0+ points
- Splits on "—" (em-dash, U+2014)
- Returns: `("AAA", "ASCII Adjust After Addition")`

#### Step 2.3: Parse Table Structure (_gen_table_intermediate)

```python
def _gen_table_intermediate(self):
    # Extract column boundaries from vertical lines
    vertical_coor = self._calc_coordinates(self.verticals, 'x0', False)
    # vertical_coor = [47, 80, 120, 150, 200, 250]  # x0 coordinates of vertical lines

    # Extract row boundaries from horizontal lines
    horizontal_coor = self._calc_coordinates(self.horizontals, 'y0', True)
    # horizontal_coor = [700, 680, 660, 640]  # y0 coordinates of horizontal lines (descending)

    # For each cell defined by row/col intersection:
    for row_idx in range(num_rows):
        for col_idx in range(num_cols):
            # Cell boundaries
            left = vertical_coor[col_idx]
            right = vertical_coor[col_idx + 1]
            top = horizontal_coor[row_idx]
            bottom = horizontal_coor[row_idx + 1]

            # Find texts within cell
            cell['texts'] = self._find_cell_texts(left, top, right, bottom)
```

**For AAA Opcode Table**:
```
Column boundaries (vertical lines):
  [47, 80, 120, 150, 200, 250]

Row boundaries (horizontal lines):
  [700, 680, 660]

Cells:
  Row 0 (Headers): [Opcode] [Instruction] [Op/En] [64-bit] [Compat] [Description]
  Row 1 (Data):    [37]     [AAA]        [ZO]   [Invalid] [Valid] [ASCII adjust...]
```

#### Step 2.4: Generate Markdown (gen_markdown)

**State Machine** (pile.py: _gen_paragraph_markdown):
```
Input: pile.texts (all text elements sorted by Y coordinate)
Output: markdown string

For each text element:
  1. If title (font=NeoSansIntelMedium, height>14.5, contains "—"):
     state = 'title'
     output: <b>AAA</b> — ASCII Adjust After Addition

  2. If content = "Instruction Operand Encoding":
     state = 'encoding'
     output: ### Instruction Operand Encoding

  3. If content = "Description":
     state = 'description'
     output: ### Description
     then output description text...

  4. If content = "Operation":
     state = 'operation'
     output: ### Operation
     output code block...

  5. If content = "Flags Affected":
     state = 'flags'
     output: ### Flags Affected
     then output flags text...

  6. If content = "Protected Mode Exceptions":
     state = 'exceptions'
     output: ### Protected Mode Exceptions
     then output exceptions...
```

#### Step 2.5: Table Markdown (_gen_table_markdown)

For the opcode table (type='table'):
```python
def _gen_table_markdown(self, state):
    intermediate = self._gen_table_intermediate()
    return self._intermediate_to_markdown(intermediate, state)

def _intermediate_to_markdown(self, intermediate, state):
    markdown = '<table>\n'

    for row in intermediate:
        markdown += '\t<tr>\n'
        for cell in row:
            texts = [text.get_text().strip() for text in cell['texts']]
            content = ' '.join(texts)

            if firstLine:  # Header row
                markdown += f'\t\t<td><b>{content}</b></td>\n'
            else:          # Data rows
                markdown += f'\t\t<td>{content}</td>\n'

        markdown += '\t</tr>\n'

    markdown += '</table>\n'
    return markdown
```

**Output**:
```html
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
		<td>ZO</td>
		<td>Invalid</td>
		<td>Valid</td>
		<td>ASCII adjust AL after addition.</td>
	</tr>
</table>
```

#### Step 2.6: Cleanup Hyphenation (writer.py: _cleanup_hyphens)

```python
@staticmethod
def _cleanup_hyphens(str):
    # Bullet points
    str = str.replace('•\n', '\n * ')

    # Word breaks
    str = str.replace('addi-\ntional', 'additional\n')
    str = str.replace('compar-\nison)', 'comparison)\n')
    # ... 30+ patterns

    # Space-hyphen fixes
    str = str.replace('single- precision', 'single-precision')
    str = str.replace('general- purpose', 'general-purpose')

    return str
```

#### Step 2.7: File Output (writer.py: close_file)

```python
def close_file(self, instruction, markdown):
    markdown = Writer._cleanup_hyphens(markdown)

    filename = './output/' + str(instruction) + '.md'
    # filename = './output/AAA.md'

    fwrite = open(filename, 'w')
    fwrite.write(markdown)
    fwrite.close()
```

---

## Generated AAA.md Output

**File**: `AAA.md`

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

Adjusts the sum of two unpacked BCD values to create an unpacked BCD result. The AL register is the implied
source and destination operand for this instruction. The AAA instruction is only useful when it follows an ADD
instruction that adds (binary addition) two unpacked BCD values and stores a byte result in the AL register. The
AAA instruction then adjusts the contents of the AL register to contain the correct 1-digit unpacked BCD result.
If the addition produces a decimal carry, the AH register increments by 1, and the CF and AF flags are set. If there
was no decimal carry, the CF and AF flags are cleared and the AH register is unchanged. In either case, bits 4
through 7 of the AL register are set to 0.

This instruction executes as described in compatibility mode and legacy mode. It is not valid in 64-bit mode.

### Operation

```java
IF 64-Bit Mode
THEN
#UD;
ELSE
IF ((AL AND 0FH) > 9) or (AF = 1)
THEN
AX := AX + 106H;
AF := 1;
CF := 1;
ELSE
AF := 0;
CF := 0;
FI;
AL := AL AND 0FH;
FI;
```

### Flags Affected

The AF and CF flags are set to 1 if the adjustment results in a decimal carry; otherwise they are set to 0. The OF,
SF, ZF, and PF flags are undefined.

### Protected Mode Exceptions

#UD If the LOCK prefix is used.

### Real-Address Mode Exceptions

Same exceptions as protected mode.

### Virtual-8086 Mode Exceptions

Same exceptions as protected mode.

---

## Porting to C#: Key Implementation Points

### 1. PDF Text Extraction

**Python**:
```python
text.get_text().encode('utf8').strip()
```

**C# with itext7**:
```csharp
string text = PdfTextExtractor.GetTextFromPage(page, new SimpleTextExtractionStrategy());
```

### 2. Font Detection

**Python**:
```python
fontname = text._objs[0].fontname  # Access pdfminer object properties
if fontname.endswith('NeoSansIntelMedium'):
```

**C# TODO** - Need to track font info during PDF parsing:
```csharp
// In PdfTextElement class
public string FontName { get; set; }

// When extracting from PDF:
if (element.FontName.EndsWith("NeoSansIntelMedium") && element.Height > 14.5)
{
    // This is an instruction header
}
```

### 3. Line Detection

**Python**:
```python
if obj.width < 1.0:
    self.verticals.append(obj)      # Column borders
elif obj.height < 1.0:
    self.horizontals.append(obj)    # Row borders
```

**C#**:
```csharp
if (line.Width < 1.0)
    verticals.Add(line);            // Vertical (column)
else if (line.Height < 1.0)
    horizontals.Add(line);          // Horizontal (row)
```

### 4. Instruction Header Parsing

**Python**:
```python
content = text.get_text().encode('utf8').strip()
if re.search('—', content):         # Em-dash (U+2014)
    tmp = content.split('—')
    instruction = tmp[0].strip()
    descr = tmp[1].strip()
    return instruction, descr
```

**C#**:
```csharp
string content = element.GetText();
if (content.Contains("—"))          // Em-dash
{
    var parts = content.Split('—');
    return (parts[0].Trim(), parts[1].Trim());
}
```

### 5. State Machine for Sections

**C# Implementation**:
```csharp
public enum ParsingState
{
    Title,
    Description,
    Encoding,
    Operation,
    Flags,
    Exceptions
}

foreach (var text in texts)
{
    string content = text.GetText();

    if (content == "Description")
        state = ParsingState.Description;
    else if (content == "Flags Affected")
        state = ParsingState.Flags;
    else if (state == ParsingState.Description)
        descriptionBuilder.Append(content);
    else if (state == ParsingState.Flags)
        flagsBuilder.Append(content);
}
```

### 6. Table Parsing

**Key Algorithm**:
```csharp
// 1. Get column boundaries from vertical lines
var columnX = verticals.Select(v => v.X0).Distinct().OrderBy(x => x).ToList();

// 2. Get row boundaries from horizontal lines
var rowY = horizontals.Select(h => h.Y0).Distinct().OrderByDescending(y => y).ToList();

// 3. For each cell, find enclosed texts
for (int row = 0; row < rowY.Count - 1; row++)
{
    for (int col = 0; col < columnX.Count - 1; col++)
    {
        double left = columnX[col];
        double right = columnX[col + 1];
        double top = rowY[row];
        double bottom = rowY[row + 1];

        var cellTexts = texts.Where(t =>
            t.X0 >= left - 0.7 &&
            t.X0 <= right &&
            t.Y1 <= top + 0.7 &&
            t.Y0 >= bottom - 0.7
        ).ToList();

        table[row][col] = string.Join(" ", cellTexts.Select(t => t.GetText()));
    }
}
```

### 7. HTML Table Generation

**Template**:
```csharp
var sb = new StringBuilder();
sb.AppendLine("<table>");

for (int row = 0; row < table.Count; row++)
{
    sb.AppendLine("\t<tr>");
    for (int col = 0; col < table[row].Count; col++)
    {
        string content = table[row][col];
        bool isHeader = (row == 0);

        if (isHeader)
            sb.AppendLine($"\t\t<td><b>{content}</b></td>");
        else
            sb.AppendLine($"\t\t<td>{content}</td>");
    }
    sb.AppendLine("\t</tr>");
}

sb.AppendLine("</table>");
```

---

## Critical Implementation Details

### 1. Y-Coordinate Direction
- PDF Y increases **upward** (0 at bottom, 800 at top)
- Sorting: `OrderByDescending(y => y)` = top-to-bottom
- Reverse: `Reverse()` after sorted ascending

### 2. Text Stripping
```csharp
// Preserves internal spacing, trims edges
text.Trim();  // Equivalent to Python .strip()
```

### 3. Search Distances
```python
_SEARCH_DISTANCE_VERTICAL = 1.0      # X coordinate tolerance
_SEARCH_DISTANCE_HORIZONTAL = 8.0    # Y coordinate tolerance
```

Use when finding cell boundaries - allows small offsets in PDF rendering.

### 4. Font Filtering
Only texts ending with "NeoSansIntelMedium" and height > 14.5 are instruction headers.
This prevents false positives from smaller text (headers, footers, page numbers).

### 5. Hyphenation Patterns
**30+ patterns** must be applied **after** all markdown generation, not during extraction.

Common patterns:
- `"instruc-\ntions"` → `"instructions\n"`
- `"single- precision"` → `"single-precision"`
- `"•\n"` → `"\n * "`

---

## Summary: AAA Extraction Flow

```
PDF (page 701)
    ↓
[Text Extraction] - Get raw text elements with positions
    ↓
Layout Objects (text + lines)
    ↓
[Pile Creation] - Organize into logical groups
    ├─ Detect vertical/horizontal lines (table borders)
    ├─ Classify as table or paragraph
    ↓
[Instruction Detection] - Find title via font/height/em-dash
    ├─ "AAA—ASCII Adjust After Addition"
    ↓
[Markdown Generation] - State machine for sections
    ├─ Title: <b>AAA</b> — ASCII Adjust After Addition
    ├─ Table: Parse via vertical/horizontal line boundaries
    ├─ Description: Extract text under "Description" header
    ├─ Operation: Extract code block under "Operation"
    ├─ Flags: Extract under "Flags Affected"
    ├─ Exceptions: Extract exception info
    ↓
[Hyphenation Cleanup] - Remove word-break artifacts
    ├─ Apply 30+ patterns
    ↓
[File Output]
    └─ Write AAA.md
```

**Total Steps**: 7-8
**Key Challenges**: Font detection, coordinate tracking, state machine, table parsing
**Success Metric**: Generated AAA.md matches existing wiki format exactly

