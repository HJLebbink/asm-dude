# CodeLens redraw/blank research — PARKED 2026-06-15

Status: **parked, not solved.** Code is left in the "round 6" state (viewport-aware serve, builds clean,
NOT yet confirmed in the hive). This file is the resume point — read it before touching the CodeLens tagger
again so the dead ends below are not re-walked.

## The symptom

`example_semantic_analysis.asm` shows ~63 CodeLens (5 label "N references" + ~58 sim-state lines). The user
waits for the sim to finish (all lenses visible), then scrolls down with arrow-down. Depending on the build,
either:
- **blank**: lenses below the original viewport never appear; eventually *no* lens is visible on the whole
  screen (round 3, round 5); or
- **flicker**: visible lenses are "first removed then rewritten" with the same text, repeatedly (round 2,
  round 4).

Nobody else seems to hit this because the VS.Extensibility **CodeLens API is still preview**
(`VSEXTPREVIEW_CODELENS`) and almost no shipping extension uses a *custom* `CodeLensTag` tagger.

## Where the code is

- `AsmCodeLensTagger.cs` — the `TextViewTagger<CodeLensTag>`. All the publish/skip logic is here.
- `AsmCodeLensTaggerProvider.cs` — `ITextViewTaggerProvider<CodeLensTag>`, one tagger per doc URI.
- `AsmCodeLensProvider.cs` / `AsmLabelCodeLens.cs` / `AsmSimStateCodeLens.cs` — the `ICodeLensProvider` +
  the two `InvokableCodeLens` renderers (unpack `CodeElement.Description`).
- Data comes from the LSP server over the side pipe (`SimStatePipeClient.GetSimStatesAsync` +
  `GetCodeLensDataAsync`); the tagger only renders. See the CodeLens section in the repo `CLAUDE.md`.
- Logs: `%TEMP%\AsmDude2-extension.log`, category `CodeLens`. Publishes/fetches at Info; per-request skip
  decisions at Trace (raise with `ASMDUDE_LOGLEVEL=trace`).

## Hard facts established (with evidence — do NOT re-question these)

1. **The only callbacks VS gives a `TextViewTagger` are `OnRequestTagsAsync` (VS-driven) and
   `OnTextViewChangedAsync` (edit-only).** There is **no scroll event, no caret event, and no way to read the
   visible viewport** — `ITextViewSnapshot` exposes `Selection`/`Document` but no visible span (verified in the
   18.5 SDK xml). `OnTextViewChangedAsync` firing 0× during a pure scroll is correct (no edits), not a bug.
2. **VS only RENDERS a viewport in response to one of its own `OnRequestTagsAsync` requests.** A proactive
   `UpdateTagsAsync` (e.g. a sim push) updates VS's cache but is not, by itself, reliably drawn. (Round‑3 blank:
   a sim push had pre-published the content and set the dedup signature, so VS's first request matched and was
   skipped → never drawn.)
3. **VS keeps tags only for the viewport it requested and DISCARDS the rest.** Publishing the whole document
   does not keep off-viewport lenses alive; on scroll VS re-requests the new viewport. (Round‑5: whole-doc
   publish + content-only dedup → scrolled-to lines blank because the new-viewport request matched the unchanged
   whole-doc signature and was skipped.)
4. **Every request arrives with `recalculateAll = true`, including the reflexive ECHO after each
   `UpdateTagsAsync`.** So the flag cannot distinguish "VS dropped, re-supply" from "echo/poll, already has it."
5. **`UpdateTagsAsync(ranges, tags)` OUTDATES (removes) every existing tag overlapping `ranges`, then re-adds.**
   So republishing identical tags is a visible remove+rewrite (the flicker). The "removal" the user sees is
   *our* republish side-effect, not VS dropping them.
6. **VS does NOT poll continuously on its own.** When idle it is quiet; requests come on scroll, on edit, and as
   the post-publish echo. The "continuous storm" in round 4 was *self-inflicted*: our self-heal republish fed
   the next echo, ad infinitum.
7. **The official samples don't help directly.** `CodeLensSample` has **no tagger** (it attaches to VS's
   built-in C# `Method` code elements — assembly has none). `TaggersSample/MarkdownCodeLensTagger` is the only
   documented *custom* tagger; it uses a `needsUpdate`/`updateRunning` coalescing loop and publishes the **whole
   document every request with NO dedup**. It survives the echo because markdown parsing is **slow**, so echoes
   arrive *during* the publish and the loop coalesces them. **Our publishes are fast** (cached pipe data), so
   each echo lands *after* the publish and spawns the next → storm. There is **no documented dedup/echo-handling
   pattern**; for a fast-publishing custom tagger the docs are simply incomplete.

## What was tried (chronological)

| Round | Approach | Result |
|------|----------|--------|
| pre | per-line publish + `CodeLensPublishPlanner` (serve-signature, echo budget, self-heal) | scroll-blank "VS drops + stops asking"; deleted |
| 1 | (planner era, full-set-on-serve experiment) | no help; VS drops off-viewport regardless |
| 2 | rewrite to MS sample: whole-doc `UpdateTagsAsync` + coalescing loop, **no dedup** | blank fixed, but **continuous flicker** (fast publishes don't coalesce) |
| 3 | + skip if whole-doc signature identical to last publish | flicker gone, but **blank** (skipped VS's first/genuine request) |
| 4 | + bounded self-heal (republish after N identical) | **permanent flicker**: VS echo-polls, self-heal fires every Nth forever |
| 5 | two signatures: content dedup + clear request-serve marker on content change | flicker gone, converges, **but blank** below viewport (skip keyed on whole-doc content, not viewport) |
| 6 | **viewport-aware serve** (current) | built, **untested in hive** |

## Current code (round 6) — what it does

`PublishWholeDocAsync(document, requestViewport, reason)`:
- **Content trigger** (sim push / edit, `requestViewport == null`): publish whole-doc unless byte-identical to
  the last publish (`lastPublishedSig_`); on publish, clear `lastServeKey_ = null` so the next request re-serves
  (VS renders only on a request — fact #2).
- **VS request**: `serveKey = requestViewport + "|" + contentSig`, where `requestViewport` =
  `minStart..maxEnd` offset span over the requested ranges (partition-independent — `TextRange.Start/.End` are
  int offsets; same span whether VS sends 1 range or N per-line ranges; there is no line-number API). Skip iff
  `serveKey == lastServeKey_` (true echo of the same viewport+content); otherwise publish whole-doc and record
  the key. So a new viewport (scroll) is served; a same-viewport poll is skipped (no flicker).
- Still publishes the **whole document** range (VS discards off-viewport anyway, so whole-doc vs requested-range
  only changes redraw cost). `dataDirty_` cache avoids pipe IPC on scroll/echo rebuilds.

## THE decisive open question (answer this first when resuming)

Does VS, on scroll, actually **issue a request for the scrolled-to viewport**? Round 6 logs `vp=<start>..<end>`
on every request.
- **If `vp=` advances as you scroll** → round 6 should work (each new viewport is served, lenses follow). If it
  still flickers *while actively scrolling*, switch the publish from whole-doc to the **requested ranges only**
  (`UpdateTagsAsync(requestedRanges, tagsWithinThem)`) so only the revealed lines are outdated, not the whole
  screen.
- **If `vp=` does NOT advance** (VS stops requesting as you scroll into new lines) → no in-process tagger logic
  can fix it (we have no scroll event and VS isn't asking). That is a **VS.Extensibility CodeLens-preview
  limitation**; file it upstream with the logs, and consider the fallbacks below.

## Fallbacks if the tagger model genuinely can't win

- **File a Developer Community / `microsoft/VSExtensibility` issue** with: repro, the `vp=` log showing VS not
  re-requesting scrolled-to viewports, and the analysis above. This is the most likely correct outcome — the
  preview CodeLens tagger appears to assume slow, whole-doc generation and gives the extension no scroll signal.
- **Hybrid in-proc CodeLens** (`IAsyncCodeLensDataPointProvider` / WPF adornment) like the archived
  `VS/CSHARP/old/asm-dude2-vsix-archived/` did — heavyweight, drags MEF/VSSDK into the extension, contradicts
  the OOP-only design. Last resort.
- **Reduce flicker tolerance**: accept "flicker while scrolling, stable when stopped" (round 6 with requested-
  range publish) as good-enough; the memory note already says flicker ≫ blank.

## Don't re-walk these dead ends
- Per-line / subset publishing + content dedup → blanks (VS drops off-screen, dedup thinks it still has them).
- Bounded self-heal on identical requests → permanent flicker (VS echo-polls; self-heal feeds it).
- "Publish the full set on every serve to keep off-viewport lenses alive" → no help; VS discards them.
- Trying to find a scroll/caret/viewport event or read the visible span → none exists in the SDK.
- Implementing `ITextViewChangedListener` on the provider → unnecessary in 18.5 (the base `TextViewTagger`
  auto-wires `OnTextViewChangedAsync`); it is edit-only anyway and irrelevant to scroll.

## Sources
- Customize CodeLens: https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/editor/walkthroughs/codelens
- Customize Taggers: https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/editor/walkthroughs/taggers
- `TaggersSample/MarkdownCodeLensTagger.cs` (the only documented custom CodeLens tagger):
  https://github.com/microsoft/VSExtensibility/blob/main/New_Extensibility_Model/Samples/TaggersSample/MarkdownCodeLensTagger.cs
- `CodeLensSample` (no tagger; built-in Method elements):
  https://github.com/microsoft/VSExtensibility/tree/main/New_Extensibility_Model/Samples/CodeLensSample

Related memory: `codelens-scroll-loop` (full blow-by-blow), `vsix-lsp-platform-limits`.
