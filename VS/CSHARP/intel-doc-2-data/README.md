# intel-doc-2-data — wiki → AsmDude signature files

This tool turns the **AsmDude wiki** (`asm-dude.wiki/doc/*.md`, one Markdown page per
instruction) into the **signature file** AsmDude loads at runtime
(`asm-dude2-ls-lib/Resources/signature-<sdm>.txt`).

## The full data pipeline (PDF → AsmDude)

```
Intel SDM PDF (325462-NNN)
   │  asm-annotate  (extract)        ── PDF → per-instruction Markdown
   ▼
asm-dude.wiki/doc/<MNEMONIC>.md      ── the "wiki" (committed in the asm-dude.wiki repo)
   │  intel-doc-2-data  (THIS tool)  ── Markdown opcode tables → signatures
   ▼
asm-dude2-ls-lib/Resources/signature-<sdm>.txt
   │  LanguageServer.cs loads it at startup
   ▼
AsmDude LSP server  (completion, signature help, hover, …)
```

Each instruction is recognised through the **`Mnemonic` enum** (see below); the signature file
only carries per-form data (operands, arch, description) for mnemonics that exist in that enum.

## What this tool does

`Program.cs`:
1. Reads every `*.md` in the wiki doc dir.
2. For each page: takes the **description** (text after the title's `—`/`-`, before the first
   `<table>`) and parses the **opcode `<table>`** into one `Signature` per data row
   (mnemonic, parameters, archs, parameter-description, description).
3. Each instruction is matched to the `Mnemonic` enum via `AsmSourceTools.ParseMnemonic`
   (`Parse_Parameters`). **A mnemonic that isn't in the enum is dropped** with a
   `Could not find a mnemonic in string …` warning.
4. Writes the tab-separated signature file:

```
;--------------------------------------------------------
GENERAL <MNEMONIC>  <Description>             <wiki-page>
<MNEMONIC>  <PARAMS> <ARCHS> <PARAM-DESC>     <per-form description>
```

### Usage
```bash
dotnet run --project VS/CSHARP/intel-doc-2-data            # defaults below
dotnet run --project VS/CSHARP/intel-doc-2-data -- <wikiDocDir> <outFile>
```
Defaults: wiki = `C:/Source/Github/asm-dude.wiki/doc`,
out = `…/asm-dude2-ls-lib/Resources/signature-mar2026.txt`. It also writes `overview.txt`
(an HTML table linking each mnemonic to its wiki page) next to the output.

## The `Mnemonic` enum — how instructions are defined

`asm-tools-lib/Mnemonic.cs` (~2600 lines, ~2017 members):
- A big `enum Mnemonic { NONE, HLT, MOV, … }` grouped by `#region` category, each member with a
  `/// <summary>` description.
- Parsing is reflective: a static ctor builds `Mnemonic_cache_` from `Enum.GetValues<Mnemonic>()`
  (`name → value`), and `ParseMnemonic`/`IsMnemonic` just do a dictionary lookup. **So adding a
  member to the enum is all that's needed for the parser to recognise it — there is no switch to
  update.** (`ToString()`/`ParseMnemonic` round-trip on the member name.)

## How to ingest a NEW SDM revision (the recipe)

1. **Update the PDF** and regenerate the wiki:
   `dotnet run --project VS/CSHARP/asm-annotate -- check-latest` then `… -- extract`, and copy
   `asm-annotate/output/*.md` into `asm-dude.wiki/doc/` (see asm-annotate/INTEL_DOC_EXTRACTION.md).
2. **Add new instructions to the enum.** Run this tool once and collect the
   `Could not find a mnemonic` warnings — those are the new mnemonics not yet in `Mnemonic.cs`.
   Add each as a new enum member (with a `/// <summary>`) in the appropriate `#region`.
   *(rev-091 / March 2026 introduced ~143: AES Key Locker `AES*KL`/`ENCODEKEY*`/`LOADIWKEY`,
   CET `INCSSP*`/`RDSSP*`/`RSTORSSP`/`CLRSSBSY`, AMX `LDTILECFG`/`STTILECFG`, APX `CMP*XADD`,
   AVX512-FP16 `V*PH`/`VCOMISH`/`VCVTPH2*`, `PREFETCHIT0/1`, `LKGS`, `UD0`, …)*
3. **Regenerate the signature file** (this tool) → `signature-<month><year>.txt`. With the enum
   updated, the warnings should be (near) zero.
4. **Wire the new file in** (it is NOT auto-discovered — two hard-coded references):
   - `asm-dude2-ls-lib/LanguageServer.cs` (~line 420): change `"signature-may2019.txt"` to the new name.
   - `asm-dude2-ls-lib/asm-dude2-ls-lib.csproj`: add `<None Remove>` + `<Content Include>` (CopyToOutputDirectory) entries for the new file (mirror the may2019 ones).
   `signature-hand-1.txt` is a hand-maintained supplement loaded alongside — keep it.

## Notes / quirks handled by this tool

- The wiki tables now contain footnote superscripts (`imm32<sup>1</sup>`) and `<td>` cells with
  `colspan`/`rowspan`; the parser strips `<sup>…</sup>` and splits cells on `<td …>` (regex), so
  those don't corrupt the parsed mnemonic/parameters.
- The header-column layout varies (3/4/5/6 columns); `To_Signature` maps mnemonic/arch/description
  columns per layout. New table shapes may need a new branch there.
- Output is written **once after** the loop (it used to rewrite the whole file every iteration).
- `Console.ReadKey()` is skipped when input is redirected, so it runs head-less in CI/scripts.
