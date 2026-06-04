# Intel PDF → per-instruction Markdown extraction

`asm-annotate` parses the Intel SDM PDF and writes one `*.md` file per instruction
(`<MNEMONIC>.md`), reproducing the format of the original Python `intel-doc-2-md`
tool (whose output is the `asm-dude.wiki/doc/*.md` reference set).

## Usage

```bash
# from VS/CSHARP/asm-annotate (so .\data and .\output resolve)
dotnet run -- extract                 # whole PDF  -> .\output\*.md
dotnet run -- extract <start> <end>   # only pages [start,end] (fast iteration)
dotnet run -- find  <text>            # locate the page(s) containing <text>
dotnet run -- dump  <page>            # print a page's text runs + grid lines (debug)
dotnet run -- check-latest            # is the local SDM PDF the newest revision?
```

The PDF is auto-located in `.\data` (`IntelDocChecker.FindLocalPdf`, highest revision wins).
Instruction pages are scattered through the combined manual (the AVX/FMA "V" section sits
*before* the A-Z section), so the default `extract` walks the whole document; non-instruction
pages simply yield no file.

## How it works (and how it mirrors the Python/pdfminer pipeline)

| Stage | Python (pdfminer) | C# (itext) — `PdfParser.cs` |
|-------|-------------------|------------------------------|
| Vector borders | `LTRect`/`LTLine` | `PdfGraphicsOperatorListener` transforms each sub-path by the CTM, emits thin boxes (w<1 → vertical, h<1 → horizontal) as `PdfLineElement` |
| Text | `LTTextLineHorizontal` (per line) | `PdfTextOperatorListener` gives per-glyph-run fragments; `GroupIntoLines` rebuilds visual lines (bucket by baseline, split on a column gap **or** a vertical grid line that overlaps in Y) |
| Piles | `split_piles` (tables vs paragraphs) | `ContentPile.SplitIntoPiles` |
| Tables | grid math on the line coords | `GenerateTableMarkdown` (`CalcCoordinates`: verticals ascending, horizontals descending) |
| Markdown | `writer.py` state machine | `GenerateParagraphMarkdown` / `MarkdownGenerator` |

### Things that bite (documented so they aren't "fixed" back to broken)

- **itext DOES expose path geometry** via `PathRenderInfo.GetPath()`+`GetCtm()`. The old
  "itext can't extract lines, use a text-position heuristic" claim was wrong; the heuristic
  fallback was removed (it invented tables on paragraph pages).
- **Column coordinate order matters**: verticals must sort *ascending*, horizontals *descending*
  (matching pdfminer reverse flags). Getting it backwards makes every cell empty.
- **Text grouping must not cross a column border**: a vertical grid line only splits a run when
  it overlaps the run in **Y** — otherwise a full-width paragraph gets chopped at a table's
  column x that merely shares the column position higher up the page.
- **`RunGap = 10`**: large enough to keep justified prose together, small enough to split the
  *border-less* pseudo-columns (exception "code | description", indented "; comment").
- **Title detection**: title font `NeoSansIntelMedium`, height ≥ 11.5pt (the 2026 running header
  is 12.4pt; the 2018 manual used >14.5pt — both covered). itext decodes the title separator as a
  plain hyphen, so the mnemonic is everything before the first `—`/`–`/`-`.
- **`LooksLikeMnemonic`** gates the title's mnemonic part so pseudocode / headings don't become
  junk files. It tolerates the lowercase placeholders Intel uses: `cc` (Jcc/CMOVcc/SETcc),
  EVEX width `x4/x8/x16` (VEXTRACTF32x4), one trailing hint letter (PREFETCHh), and `[]`
  (GETSEC[SENTER]). A multi-word ALL-CAPS description is treated as a heading and rejected
  (e.g. appendix "GENERAL — PURPOSE INSTRUCTION FORMATS…"), while a single-word all-caps
  description is kept ("FTST — TEST").
- **Assignment operator**: recent SDM pseudocode renders `←` as `:=`; it's mapped back to `←`
  inside the ```java block. Code blocks use the **raw** (un-escaped) text so `#UD` stays literal.
- Output is written UTF-8 **without BOM**, LF line endings, to match the reference byte-for-byte.

## Quality vs the reference (`asm-dude.wiki/doc`, generated 2018 from rev-?? by the Python tool)

Latest full run against SDM **rev 091** (combined volumes):

- **856** `.md` files written, **684** with filenames identical to the 709-file reference
  (96.5%), **0** crashes, **~0** junk files.
- Per-file content matches the reference structurally and nearly byte-for-byte; remaining
  per-file diffs are genuine **2018→2026 manual changes** (e.g. `NA`→`N/A`,
  `single-precision`→`single precision`, dropped `.NDS`, added `AVX10.1`,
  `SET_RM`→`SET_ROUNDING_MODE_FOR_THIS_INSTRUCTION`). Verified on AAA (essentially identical),
  CMC, FTST, ADD, ADDPS.
- ~170 files have no reference counterpart — these are **new instructions** added since 2018
  (AES Key Locker, CET `ENDBR32/64`, AMX `LDTILECFG`, FRED `ERETS/ERETU`, user interrupts) and
  finer multi-form page splits the 2026 manual introduced.
- ~22 reference files have no exact-name match — almost all are multi-form pages the 2026 manual
  **renamed / split / merged** (e.g. reference `VPCOMPRESS` → `VPCOMPRESSB_W` + `VPCOMPRESSD` +
  `VPCOMPRESSQ`; `VAESDEC` folded into `AESDEC.md`); the content is present under the new name.

See `INTEL_DOC_SOURCE.md` for where the PDF comes from and the freshness check.
