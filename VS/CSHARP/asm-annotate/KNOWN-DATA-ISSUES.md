# Known instruction-data issues (asm-annotate `gen-signatures`)

This documents data-quality issues in the signature-generation pipeline
(`PDF → extract → wiki .md → gen-signatures → signature-mar2026.txt`), what was fixed, and what
remains. The relevant warnings are emitted at generation time via `AsmLog.Warn("ANNOTATE", …)` on
**stderr** (see `SignatureGenerator.To_Signature`).

> ⚠ **The generator's output is not live until regenerated.** Fixing `gen-signatures` only changes the
> bundled `signature-mar2026.txt` after you re-run the command (which also rewrites the wiki `Home.md`):
> ```
> dotnet run --project VS/CSHARP/asm-annotate -- gen-signatures
> ```

## Why arch can be missing

The arch (CPUID feature) for a signature comes from the `CPUID Feature Flag` column of the SDM opcode
table (rendered into the wiki `.md`). It goes missing for two structural reasons:

1. **No CPUID column in the table.** Legacy instructions (CRC32, FXSAVE, VERR, CMPXCHG16B, CVTPI2PD,
   MASKMOVQ, PSHUFW, CLFLUSH, …) have only `64-Bit Mode` / `Compat-Leg Mode` columns (`Valid`/`N.E.`);
   the feature is named only in prose.
2. **Unmapped or garbled CPUID cell.** The cell holds a token the arch parser doesn't know, or a
   line-wrap/extraction artifact.

## Fixed (current code)

- **Column detection (Bucket B).** `To_Signature` now treats a column as the arch column **only if its
  header contains "CPUID"**; otherwise it falls back to the operand-width heuristic (`arch_column == -1`)
  instead of reading a mode value. This gives legacy instructions a best-effort "available-since" arch
  (e.g. `CRC32 R64 → X64`, `R32 → 386`) rather than an empty column. The heuristic is approximate
  (`CMPXCHG16B`/`VERR` fall to `8086`); refine specific ones via the hand-file if it matters.
- **Arch mapping (Bucket A).** `ArchTools.ParseArch` now maps the previously-unknown CPUID tokens:
  `AES_WIDE`/`KEY_LOCK ER` → KEYLOCKER, `PREFETCHW` → PRFCHW, `EDECCSSA` → SGX2, and the new `CLWB`
  → `ARCH_CLWB`. (The `KEY_LOCK ER` case repairs a PDF line-wrap artifact.)
- **Known-arch exceptions.** `SignatureGenerator.KnownArchExceptions` fills the arch for a handful of
  SGX / Key Locker instructions whose CPUID cell is empty / `NA` / a garbled `Bit 10` (the feature is
  unambiguous from the instruction, just not parseable from the table):
  | Instruction | SDM cell | Mapped arch |
  |---|---|---|
  | `AESDECWIDE128KL`, `AESDECWIDE256KL` | *(empty)* | KEYLOCKER |
  | `ENCLS`, `ENCLU` | `NA` | SGX1 |
  | `EUPDATESVN` | `Bit 10` | SGX2 |

  > **Why this matters beyond noise:** an empty arch parses to `ARCH_NONE` on the server, which is
  > *always-on* — so without these mappings the instructions leak into **every** arch profile (even
  > `x86-64-v1`). Keep this map **small**; a growing list usually means a parser/column-detection bug to
  > fix instead.

## Remaining (open) issues

### 1. `EEXIT.md` — scrambled opcode table (stage-1 extraction defect) — DATA LOSS
The PDF table for EEXIT was extracted with all header + data cells collapsed into a single `<td>`
(`Description CPUID Opcode/ Op/En 64/32 Feature Instruction bit Mode Flag Support EAX = 04H IR V/V SGX1
… ENCLU[EEXIT]`). `To_Signature` sees a 1-column header → `found header count 1` warning, and **EEXIT
gets no signature at all** (not just a missing arch).
- **Root cause:** stage-1 (`asm-annotate extract`, the PDF parser) on a rotated/complex SDM table layout.
- **Fix options:** improve the extractor for this table shape (preferred, durable), or hand-correct
  `asm-dude.wiki/doc/EEXIT.md` (fragile — overwritten by the next `extract`). Not worth chasing for one
  leaf unless the extractor is being revisited anyway.

### 2. `FXSAVE.md` — `found header count 2` is a FALSE POSITIVE
FXSAVE's real opcode table parses fine (arch `8086` via the Bucket-B heuristic). The warning comes from a
secondary **"FXSAVE Field Definition"** sub-table that merely contains the substring `Opcode`, so
`SignatureGenerator.Parse` (`tableHtml.Contains("Opcode")`) tries to parse it as an opcode table.
- **Impact:** none on output — it's noise only.
- **Fix option (low-risk):** tighten opcode-table detection so a sub-table that only *mentions* "Opcode"
  in a body cell isn't parsed — e.g. require "Opcode" in the **header row**, or that the header also
  contains "Instruction". Until then it is a known, harmless warning.

## Garbled operand tokens — FIXED in the generator (grammar-directed repair)
`MnemonicStore` parses each form's operand string via `AsmSignatureTools.Parse_Operand_Type_Enum`; an
operand token it doesn't recognise logs `WRN TOOLS … unknown content <tok>` (visible in the LS pane). All
**legitimate** memory/FP16/decoration tokens are handled explicitly in the grammar (incl. `M16BCST`,
`M16{ER}/{SAE}`, `M384`, `M14_28BYTE`/`M94_108BYTE`, `XMM0{K}{Z}`). The rest were **genuine stage-1
PDF-extraction defects** that leak into the instruction-cell text of the wiki `.md`:

| Defect class | `.md` cell | Was | Now |
|---|---|---|---|
| Op/En code bled into operand | `POPCNT r16, r/m16RM` | `R/M16RM` | `R/M16` |
| footnote superscript digit | `LAR …, r32/m161` | `R32/M161` | `R32/M16` |
| stray `.` | `VPAND …, ymm3/.m256` | `YMM/.M256` | `YMM/M256` |
| doubled `m` in mem size | `VMOVDQU32 …, xmm2/mm128` | `XMM/MM8`¹ | `XMM/M128` |
| stray `/r` (ModRM fragment) | `VREDUCESD …, imm8/r` | `IMM8/R` | `IMM8` |
| implicit operand fused (no comma) | `ENCODEKEY256 r32, r32<XMM0-6>` | `R32,R32XMM_ZERO` | `R32,R32,XMM_ZERO`² |

¹ worsened by `Cleanup_Parameters`' `MM1`/`MM2` register-number stripping cascading `MM128`→`MM8`.
² now consistent with `ENCODEKEY128`/`BLENDVPD`, which already emit `,XMM_ZERO` for the implicit operand.

**Where it's fixed — STAGE 1 (`extract`), so the wiki `.md` itself comes out clean** (the signature file is
then clean for free, and there is NO downstream stage-2 repair). `ContentPile.RepairInstructionCell` runs as
each opcode-table instruction cell is written (`IntermediateToMarkdown`, gated by `IsInstructionTable` which
matches both the `Opcode` and combined `Opcode/Instruction` headers). It is **grammar-directed, not a
hardcoded list of broken strings**, and **case-preserving** (the wiki keeps its lowercase):

- It only rewrites a cell whose post-mnemonic text is a pure operand list. The mnemonic must be preceded
  ONLY by opcode/encoding tokens (`IsOpcodeEncodingToken`) — this rejects a *description* cell that merely
  mentions a short-word mnemonic (`…store the result in xmm1.` — `in` is the `IN` mnemonic). Opcode bytes
  that look like mnemonics (`DB`, `DD`) are skipped (no real mnemonic is two hex digits).
- Per operand (`TryRepairOperand`): a trailing footnote `<sup>…</sup>` is set aside and re-attached;
  `<XMM0-6>` implicit operands and `zmm2+3` register-block notation are left untouched; a stray `.` and a
  doubled-`m` memory size (`mm128`→`m128`) are fixed; then it **validates against the real consumer grammar**
  via `AsmSignatureTools.Is_Known_Operand` (mirrors `MnemonicStore`'s tokenization + tolerates `.md`-form
  register indices/decoration spaces). If still invalid it trims a **short (≤4-char)** trailing run (fused
  Op/En code, inline footnote digit, stray `/r`) to the longest form that parses.
- **If no repair makes it parse, the operand — and the whole cell — is left UNCHANGED so the LSP still
  warns**: a genuinely new/unmodelled token must never be silently truncated.

The implicit operand is handled in **stage-2** (signature *formatting* from the kept `.md` notation, not
garble repair): `Parse_Parameters` calls `SeparateFusedImplicitOperand` to put a comma before a fused
`<…>` ("R32,R32<XMM0-6>" → "R32,R32,<XMM0-6>") on the SHARED operand string, so the abbreviated `parameters`
(`<…>`→`XMM_ZERO`) and the kept-notation `parameter_descriptions` end up with the **same operand count** —
`MnemonicStore.CreateAsmSignatureElement` throws `IndexOutOfRange` if a row's args and sign disagree on
count (ENCODEKEY256 hit this; ENCODEKEY128/BLENDVPD already carried the comma). Tested by
`RepairInstructionCell_*` and `ParseParameters_FusedImplicitOperand_*` (asm-annotate-tests). The grammar
lives in `AsmSignatureTools` (asm-tools-lib); `Is_Known_Operand` is the no-warn validator,
`Parse_Operand_Type_Enum` the warning one — both share `Parse_Operand_Type_Enum_Core` + `SplitOperandTokens`.

## Tests
- `asm-annotate-tests` — stage-2 MD→signature generation (column/title heuristics).
- `asm-tools-tests::Test_ArchProfile` — arch-profile expansion + the `Is_Arch_Switched_On` override.
