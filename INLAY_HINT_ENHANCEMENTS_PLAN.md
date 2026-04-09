# Inlay Hint Enhancements Plan

> Status: Enhancement 1 **done**. Enhancements 2–4 pending.
> Current branch: `dotnet10`

## Background

The LSP server (`asm-dude2-ls-lib`) provides inlay hints via `textDocument/inlayHint`.
After each instruction, an inlay hint shows the register/flag state written by that
instruction. The current implementation (`LspAsmSimulator.cs` + `LanguageServer.cs`)
already does:

- Computes full after-state per line (for hover tooltip)
- Computes filtered after-state per line (for inlay hint label):
  - Shows only registers/flags **written** by the instruction
  - Prefixes with `→` for values that **flow to the next instruction** (i.e. the
    intersection of writes(N) ∩ reads(N+1))
  - Shows `?` for UNKNOWN values (so the chain is always visible)
- Sends `workspace/inlayHint/refresh` after each simulated instruction so hints
  appear progressively rather than all at once after full simulation

---

## Planned Enhancements

### 1. R/W Categorization with `↔` / `←` / `→` Prefixes

**Goal:** Each register/flag in the hint should visually distinguish whether the
instruction reads it, writes it, or both.

| Symbol | Meaning | State shown |
|--------|---------|-------------|
| `↔`    | Read AND written (e.g. `ADD RAX, RBX` → RAX) | After-state (output value) |
| `←`    | Read only (consumed but not modified)          | Before-state (input value) |
| `→`    | Written only / already flows forward           | After-state (output value) |

**Example output:**
```
ADD RAX, RBX    ; ↔RAX=0x10  ←RBX=0x4  →CF=0
CMP RAX, 0      ; ←RAX=0x10  →ZF=1 →CF=0 →SF=0
MOV [RBP-8], RAX; ←RAX=0x10
```

**Key design points:**
- `←` registers show their **before-state** value (from `lineStringsBefore`), not after
- `↔` registers show their **after-state** (the result of the r+w)
- `→` already in use; semantics unchanged
- Read-only registers (written by an earlier instruction) provide the "where did this
  value come from" answer visually without needing a tooltip or click

**Implementation notes:**
- `writtenRegs` = `opcodeBase.RegsWriteStatic` (already computed in `RunSimulation`)
- `readRegs` = `opcodeBase.RegsReadStatic` (already available, not yet stored per-line)
- `rwRegs` = `writtenRegs ∩ readRegs`
- `writeOnlyRegs` = `writtenRegs \ readRegs`
- `readOnlyRegs` = `readRegs \ writtenRegs`
- `ComputeFilteredStateString` needs a third category: read-only registers queried
  from `beforeState` (not `afterState`)
- `DocCache.lineStringsAfterFiltered` stores the result; the name is slightly misleading
  for read-only entries — consider renaming or adding a separate field

---

### 2. Per-Register `InlayHintLabelPart[]` with Tooltips

**Goal:** Each register/flag becomes its own `InlayHintLabelPart` so it can carry
its own tooltip showing data provenance.

**LSP types confirmed available** (`Microsoft.VisualStudio.LanguageServer.Protocol` 18.5.3):
```csharp
InlayHintLabelPart.Value    string
InlayHintLabelPart.ToolTip  SumType<string, MarkupContent>?
InlayHintLabelPart.Location Location         // navigate on click
InlayHintLabelPart.Command  Command          // invoke workspace/executeCommand on click
InlayHint.Label             SumType<string, InlayHintLabelPart[]>
```

**Tooltip content per part** (example for `→RAX=0x10`):
```
RAX — written here by: ADD RAX, 8
      read by line 44: CMP RAX, RBX
```

**Tooltip content for `←RBX=0x4`** (read-only):
```
RBX — last written at line 38: MOV RBX, 4
      read here by: ADD RAX, RBX
```

**Implementation notes:**

1. **Track last-write location during simulation** — in `RunSimulation`, maintain:
   ```csharp
   var lastWriteLineByReg  = new Dictionary<Rn, (int lineIdx, string lineText)>();
   var lastWriteLineByFlag = new Dictionary<Flags, (int lineIdx, string lineText)>();
   ```
   Updated after processing each instruction's `writtenRegs`/`writtenFlags`.

2. **Store per-line provenance** — add to `DocCache`:
   ```csharp
   // Maps lineIdx → per-register last-write info (snapshot at time of processing)
   internal readonly Dictionary<int, Dictionary<Rn, (int writtenAt, string instruction)>>
       regProvenanceByLine = [];
   internal readonly Dictionary<int, Dictionary<Flags, (int writtenAt, string instruction)>>
       flagProvenanceByLine = [];
   ```
   Or alternatively store a pre-formatted tooltip string per line (simpler, less flexible).

3. **Change label construction in `GetInlayHints`** — replace:
   ```csharp
   Label = simLabel,  // string
   ```
   with:
   ```csharp
   Label = BuildSimInlayLabelParts(stateStrFiltered, provenance, nextLineIdx, lines),
   // returns InlayHintLabelPart[]
   ```

4. **`BuildSimInlayLabelParts`** — one `InlayHintLabelPart` per register/flag token,
   each with `.Value = "→RAX=0x10"` and `.ToolTip = "..."`.

---

### 3. Click Navigation via `InlayHintLabelPart.Location`

**Goal:** Clicking a register part in an inlay hint navigates the cursor to the line
where that register was last written (before this instruction). VS then applies its
built-in `documentHighlight` to all occurrences of the token at the new cursor position.

**Implementation notes:**
- Use the `lastWriteLineByReg[reg].lineIdx` stored during simulation
- Convert to `Location { Uri = documentUri, Range = new Range(lineIdx, 0, lineIdx, 0) }`
- Set on `InlayHintLabelPart.Location`
- For registers written by THIS instruction (not a prior line), `Location` points to
  the current line — less useful; consider omitting or pointing to the first read site

**Uncertainty:** Whether VS 2022/2026's LSP client renders `InlayHintLabelPart.Location`
as a clickable hyperlink is untested. If VS ignores it, nothing breaks — the text label
and tooltip still display. Needs empirical verification with F5.

---

### 4. Click-to-Highlight via `Command` + Custom Notification + `TextViewTagger`

**Goal:** Clicking a register/flag label part highlights all read/write sites for that
register across the document — a full "find all usages" highlight triggered from the hint.

**Architecture confirmed feasible** (custom LSP methods already work: `asm/codeLensData`
is an existing precedent in this project; `TextViewTagger` works in OOP mode via CodeLens).

**Flow:**
```
User clicks hint part
  → VS sends: workspace/executeCommand
              { name: "asm/highlightRegister", args: [uri, lineNumber, "RAX"] }
  → LSP server scans simulation cache for all lines that read/write RAX
  → LSP server sends custom notification: asm/highlightRanges { uri, ranges: [...] }
  → VSIX (AsmLanguageClient) receives notification via custom JsonRpc handler
  → AsmHighlightTagger (TextViewTagger<TextMarkerTag>) applies editor highlights
  → Highlights clear on next document edit or Escape
```

**Implementation — four components:**

1. **`LanguageServerTarget.cs`** — register `asm/highlightRegister` in `ExecuteCommand`:
   ```csharp
   case "asm/highlightRegister":
       var uri  = args[0].ToString();
       var reg  = args[1].ToString();  // e.g. "RAX"
       var ranges = this.server_.GetRegisterRanges(uri, reg);
       // send notification as side-effect (no useful return value from executeCommand)
       await this.server_.SendHighlightRangesAsync(uri, ranges);
       return null;
   ```

2. **`LanguageServer.cs`** — `GetRegisterRanges(uri, regName)`:
   - Scan `parsedDocuments[uri]` for tokens matching `regName`
   - Optionally tag each range with read/write kind (from simulation data)
   - `SendHighlightRangesAsync`: `jsonRpc_.NotifyWithParameterObjectAsync("asm/highlightRanges", payload)`

3. **`AsmLanguageClient.cs` (VSIX)** — register custom notification handler on the
   underlying JsonRpc connection after `CreateServerConnectionAsync`:
   ```csharp
   jsonRpc.AddLocalRpcMethod("asm/highlightRanges", OnHighlightRanges);
   ```
   Store received ranges in a shared singleton (e.g. `AsmHighlightState`).

4. **New `AsmHighlightTagger.cs` (VSIX)** — `TextViewTagger<TextMarkerTag>`:
   - Subscribes to `AsmHighlightState.Changed` event
   - Returns `TextMarkerTag` (or a custom classification tag for read vs. write color)
     for each range in the current document
   - Clears on `ITextBuffer.Changed` or when a new `asm/highlightRanges` arrives with
     an empty range list (server sends empty list to clear)

**Triggering from `InlayHintLabelPart.Command`:**
```csharp
new InlayHintLabelPart
{
    Value = "→RAX=0x10",
    ToolTip = "...",
    Command = new Command
    {
        Title = "Highlight RAX",
        CommandIdentifier = "asm/highlightRegister",
        Arguments = [uri, "RAX"],
    }
}
```

**Uncertainty:** Whether VS renders a label part with `Command` as a clickable element
is untested. If VS ignores `Command` on label parts, fall back to a dedicated CodeLens
button or a hover-action button (VS 2026 supports hover actions via `_vs_hoverActions`
in the hover response). Needs empirical verification with F5.

**Read vs. write highlight colors:**
- Read sites: blue underline (`"MarkerFormatDefinition"` with blue border)
- Write sites: orange/red highlight
- Requires a custom `MarkerFormatDefinition` exported via the VSIX

---

### 5. Hover Highlighting (NOT FEASIBLE via LSP alone)

**Goal:** When *hovering* (not clicking) an inlay hint, highlight the involved registers.

**Why not possible via LSP alone:**
- `textDocument/documentHighlight` is triggered by cursor position in the document,
  not by inlay hint hover events
- No LSP message type maps to "user hovered inlay hint → apply highlights"
- `InlayHintLabelPart` has no hover event — only `ToolTip` (declarative string) and
  `Command` (click-only)

**What IS achievable (approximation):**
- Enhancement 3 (`Location`) moves cursor to last-write line on click → VS applies
  its own `documentHighlight` automatically
- Enhancement 4 (`Command`) triggers explicit highlight on click

**Future path (requires architectural change):**
- Hybrid VSSDK + VisualStudio.Extensibility extension with `RequiresInProcessHosting = true`
- Enables MEF `IMouseProcessor` on the inlay hint adornment layer, reacting to `MouseMove`
  (same pattern as `AsmCodeLensMouseProcessor`)
- **CLAUDE.md explicitly warns**: in-process hosting has been broken and re-debugged 6+
  times; silently breaks LSP activation. Do not attempt without a clear plan.

---

## Implementation Order

1. **Enhancement 1** (r/w categorization) — highest visual value, self-contained change
   in `LspAsmSimulator.ComputeFilteredStateString` and `LanguageServer.BuildSimInlayLabel`
2. **Enhancement 2** (per-part tooltips) — requires provenance tracking in sim loop;
   medium complexity
3. **Enhancement 3** (click navigation via Location) — small addition once Enhancement 2
   is done (provenance data already available); needs F5 verification
4. **Enhancement 4** (click-to-highlight via Command + notification + tagger) — substantial
   plumbing; implement after Enhancement 2 confirms label parts render correctly in VS
5. **Enhancement 5** (hover highlight) — architectural prerequisite not met; defer

---

## Files to Modify

| File | Change |
|------|--------|
| `asm-dude2-ls-lib/LspAsmSimulator.cs` | Track `lastWriteLineByReg/Flag`; extend `DocCache`; extend `ComputeFilteredStateString` for r/w/r-only |
| `asm-dude2-ls-lib/LanguageServer.cs` | `BuildSimInlayLabel` → `BuildSimInlayLabelParts` returning `InlayHintLabelPart[]`; wire provenance tooltip; add `GetRegisterRanges` + `SendHighlightRangesAsync` |
| `asm-dude2-ls-lib/LanguageServerTarget.cs` | Register `asm/highlightRegister` command in `ExecuteCommand` handler |
| `asm-dude2-ls-lib/LspAsmSimulator.cs` | Add `GetRegProvenanceByLine` / `GetAfterStateFilteredTooltip` public accessor |
| `asm-dude2-vsix/AsmLanguageClient.cs` | Register `asm/highlightRanges` custom notification handler on JsonRpc connection |
| `asm-dude2-vsix/AsmHighlightTagger.cs` | New: `TextViewTagger<TextMarkerTag>` driven by highlight notification; clears on edit |
| `asm-dude2-vsix/AsmHighlightState.cs` | New: shared singleton holding current highlight ranges + `Changed` event |
