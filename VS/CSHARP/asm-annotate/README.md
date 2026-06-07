# asm-annotate — PDF → Markdown extractor (stage 1 of the instruction-data pipeline)

`asm-annotate` extracts every x86 instruction from the **Intel SDM** PDF and writes one
`<MNEMONIC>.md` per instruction (opcode table + description + exception sections), matching the
format of the `asm-dude.wiki` pages.

It is the **first stage** of the data pipeline:

```
PDF ──[asm-annotate]──► *.md ──(copy to asm-dude.wiki/doc)──► [intel-doc-2-data] ──► signature-<rev>.txt ──► LSP server
       (this project)                                          (md → txt)
```

## CLI

```bash
cd VS/CSHARP/asm-annotate
dotnet run -- extract            # full run: writes every instruction to .\output\*.md
dotnet run -- extract 700 705    # only pages 700–705 (debugging)
dotnet run -- find "VADDPS"      # locate the page(s) containing some text
dotnet run -- dump 701           # print raw text/line elements for one page
dotnet run -- check-latest       # is the local SDM PDF the newest Intel publishes?
```

The PDF lives in `data/325462-*-sdm-vol-1-2abcd-3abcd-4.pdf` (committed). Output goes to `.\output`
(gitignored). Instruction pages are scattered through the combined PDF, so `extract` walks the whole
document.

After extracting, copy the result into the wiki clone and regenerate signatures:
```bash
cp output/*.md /c/Source/Github/asm-dude.wiki/doc/
dotnet run --project ../intel-doc-2-data        # → signature-mar2026.txt + Home.md
```
(See `wiki` memory / the project root for the full regen workflow.)

## Source layout

| File | Responsibility |
|------|----------------|
| `Program.cs` | CLI entry point (command switch) |
| `PdfDocumentParser.cs` | iText PDF I/O: page → text + vector-line elements (`PdfGraphicsOperatorListener`, `PdfTextOperatorListener`, `PdfPageTextExtractor`) |
| `PdfModels.cs` | DTOs: `PdfTextElement`, `PdfLineElement`, `MarkdownState`, … |
| `ContentPile.cs` | Layout analysis: group elements into table/paragraph piles, detect opcode tables, find the instruction title/mnemonic (`PileKind`, `LooksLikeMnemonic`, `ExpandCompactMnemonic`, …) |
| `MarkdownGenerator.cs` | Write one `.md` per instruction; opcode-table continuation handling |
| `TextCleaner.cs` | PDF artifact fixes (hyphenation, footnote superscripts, bullets) |
| `Diagnostics.cs` | `Extractor.Run` (the production pipeline) + `find`/`dump` helpers |
| `IntelDocChecker.cs` | Locate the local SDM PDF + check for a newer revision online |
| `ParserXed.cs` | **Stub for a future XED-database extension** (not yet wired in) |

## Tests

`asm-annotate-tests` (xUnit) covers the deterministic text/title heuristics — `LooksLikeMnemonic`,
`ExpandCompactMnemonic`, `IndexOfTitleSeparator`, `LowercaseTitleConjunctions`,
`TextCleaner.CleanupHyphenation`. There is no end-to-end PDF golden test (it would need the 26 MB PDF);
the de-facto integration check is re-running `extract` and diffing `output/` against `asm-dude.wiki/doc`.

```bash
dotnet test ../asm-annotate-tests
```

## Dependencies

- **itext 9.5.0** — PDF parsing (vector lines via `PathRenderInfo.GetPath()`/`GetCtm()`).
- **asm-tools-lib** — `Mnemonic` / `Arch` enums.
- .NET 10, C# 14. No Python or external tools.

## See also

- `INTEL_DOC_SOURCE.md` — where the SDM PDF comes from and the freshness check.
- `INTEL_DOC_EXTRACTION.md` — extraction gotchas (title detection, hyphenation, continuation pages).
- `../intel-doc-2-data/README.md` — stage 2 (md → signature txt).
