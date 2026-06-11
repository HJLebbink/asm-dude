# Known instruction-data issues (asm-annotate `gen-signatures`)

This documents data-quality issues in the signature-generation pipeline
(`PDF → extract → wiki .md → gen-signatures → signature-mar2026.txt`), what was fixed, and what
remains. The relevant warnings are emitted at generation time via `AsmLog.Warn("ANNOTATE", …)` on
**stderr** (see `SignatureGenerator.To_Signature`).

> ⚠ **The generator's output is not live until regenerated.** Fixing `gen-signatures` only changes the
> bundled `signature-mar2026.txt` after you re-run the command (which also rewrites `overview.txt` and the
> wiki `Home.md`):
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

## Tests
- `asm-annotate-tests` — stage-2 MD→signature generation (column/title heuristics).
- `asm-tools-tests::Test_ArchProfile` — arch-profile expansion + the `Is_Arch_Switched_On` override.
