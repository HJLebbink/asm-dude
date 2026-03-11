# Known Issues — AsmDude2 VSIX

## 1. Collapsed folding region does not show hover tooltip

When hovering over a collapsed `#region`/`#endregion` block, no tooltip preview of the region contents is displayed. The region collapses correctly and the collapsed text (e.g., "Test") shows inline, but the hover popup with a preview of the lines inside the region does not appear.

**Status**: Under investigation

**Background**: LSP `FoldingRange.CollapsedText` only controls the inline text shown when collapsed — it does not support hover tooltips. A client-side `ITagger<IOutliningRegionTag>` with `OutliningRegionTag.CollapsedHintForm` was implemented (`CodeFolding/OutliningTagger.cs`) and a `MiddleLayer` (`CodeFolding/FoldingMiddleLayer.cs`) suppresses LSP folding to avoid duplicate regions. However, the hover tooltip still does not appear. The root cause is unknown — it may be that VS's LSP client framework suppresses client-side outlining taggers when an `ILanguageClient` is active for the same content type.

## 2. Hover tooltips cannot render markdown or clickable URLs

Visual Studio's LSP client (both VS 2022 and VS 2026) does not support markdown rendering in `textDocument/hover` responses. The client advertises `hover.contentFormat: ["plaintext"]` only. This means:

- Markdown links `[text](url)` are shown as raw text, not clickable
- Bold `**text**`, tables, and other markdown formatting are not rendered
- Documentation URLs are shown as plain text (`Doc: https://...`)

This is a known VS limitation, not an AsmDude2 bug. Tracked at:
https://developercommunity.visualstudio.com/t/Support-markdown-in-LSP-textDocumenthov/10712890

**Workaround**: Hover content uses `MarkupKind.PlainText` with pre-formatted fixed-width text for the performance table and a plain-text URL at the bottom.

**Status**: Won't fix (VS limitation)
