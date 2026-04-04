// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Threading;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only
#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

/// <summary>
/// Scans assembly documents for label definitions and creates CodeLensTags with reference counts.
/// A label definition is a non-whitespace identifier followed by ':' at the start of a line.
/// </summary>
internal class AsmCodeLensTagger : TextViewTagger<CodeLensTag>
{
    public static readonly CodeElementKind AsmLabelKind = "AsmLabel";

    private readonly AsmCodeLensTaggerProvider provider;
    private readonly Uri documentUri;
    private readonly AsyncSemaphore semaphore = new(1);

    private ITextDocumentSnapshot? currentDocumentSnapshot;
    private bool needsUpdate;
    private bool updateRunning;

    public AsmCodeLensTagger(AsmCodeLensTaggerProvider provider, Uri documentUri)
    {
        this.provider = provider;
        this.documentUri = documentUri;
    }

    public override void Dispose()
    {
        this.provider.RemoveTagger(this.documentUri, this);
        this.semaphore.Dispose();
        base.Dispose();
    }

    protected override async Task OnTextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
    {
        if (args.Edits.Count == 0) return;

        var documentAfter = args.AfterTextView.Document;
        using var semaphoreReleaser = await this.semaphore.EnterAsync();

        if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < documentAfter.RpcContract.Version)
        {
            this.currentDocumentSnapshot = documentAfter;
            _ = this.RunCreateTagsAsync();
        }
    }

    protected override async Task OnRequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot is null) return;

        using var semaphoreReleaser = await this.semaphore.EnterAsync();

        if (recalculateAll)
        {
            this.currentDocumentSnapshot =
                this.currentDocumentSnapshot is not null && this.currentDocumentSnapshot.RpcContract.Version >= requestedRanges.TextDocumentSnapshot.RpcContract.Version
                    ? this.currentDocumentSnapshot
                    : requestedRanges.TextDocumentSnapshot;
            _ = this.RunCreateTagsAsync();
        }
        else if (this.currentDocumentSnapshot is null || this.currentDocumentSnapshot.RpcContract.Version < requestedRanges.TextDocumentSnapshot.RpcContract.Version)
        {
            this.currentDocumentSnapshot = requestedRanges.TextDocumentSnapshot;
            _ = this.RunCreateTagsAsync();
        }
    }

    private async Task RunCreateTagsAsync()
    {
        this.needsUpdate = true;

        if (this.updateRunning) return;

        this.updateRunning = true;
        while (true)
        {
            ITextDocumentSnapshot document;
            using (var semaphoreReleaser = await this.semaphore.EnterAsync())
            {
                if (!this.needsUpdate || this.currentDocumentSnapshot is null)
                {
                    this.updateRunning = false;
                    return;
                }

                this.needsUpdate = false;
                document = this.currentDocumentSnapshot;
            }

            await this.CreateTagsAsync(document);
        }
    }

    private async Task CreateTagsAsync(ITextDocumentSnapshot document)
    {
        // First pass: find all label definitions and collect all label names
        var labelDefs = new List<(string name, int lineIndex, int start, int length)>();
        var allLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in document.Lines)
        {
            string lineText = line.Text.CopyToString();
            string? labelName = TryParseLabelDefinition(lineText);
            if (labelName != null)
            {
                allLabels.Add(labelName);
                labelDefs.Add((labelName, line.LineNumber, line.Text.Start, line.Text.Length));
            }
        }

        // Second pass: count references for each label
        var refCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in allLabels)
        {
            refCounts[label] = 0;
        }

        foreach (var line in document.Lines)
        {
            string lineText = line.Text.CopyToString();

            // Skip comment-only lines
            string trimmed = lineText.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#') continue;

            // Strip comment from end
            int commentIdx = lineText.IndexOf(';');
            string codePart = commentIdx >= 0 ? lineText[..commentIdx] : lineText;

            foreach (var label in allLabels)
            {
                int searchStart = 0;
                while (true)
                {
                    int idx = codePart.IndexOf(label, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;

                    int endIdx = idx + label.Length;

                    // Check word boundaries
                    bool startOk = idx == 0 || !IsIdentifierChar(codePart[idx - 1]);
                    bool endOk = endIdx >= codePart.Length || !IsIdentifierChar(codePart[endIdx]);

                    // Exclude the definition itself (label followed by ':')
                    bool isDefinition = endOk && endIdx < codePart.Length && codePart[endIdx] == ':';

                    if (startOk && endOk && !isDefinition)
                    {
                        refCounts[label]++;
                    }

                    searchStart = endIdx;
                }
            }
        }

        // Create tags
        var tags = new List<TaggedTrackingTextRange<CodeLensTag>>();
        foreach (var (name, lineIndex, start, length) in labelDefs)
        {
            int count = refCounts.GetValueOrDefault(name, 0);
            tags.Add(new(
                new(document, start, length, TextRangeTrackingMode.ExtendForwardAndBackward),
                new(AsmLabelKind)
                {
                    UniqueIdentifier = name,
                    Description = $"refcount:{count}|Label: {name}",
                    DisplayBeforeCreatingCodeLenses = true,
                }));
        }

        await this.UpdateTagsAsync([new(document, 0, document.Length)], tags, CancellationToken.None);
    }

    /// <summary>
    /// Tries to parse a label definition from a line of assembly code.
    /// Returns the label name if found, null otherwise.
    /// </summary>
    private static string? TryParseLabelDefinition(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.Length == 0) return null;

        // Skip comment lines
        if (trimmed[0] == ';' || trimmed[0] == '#') return null;

        // Find the colon
        int colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0) return null;

        // The part before the colon should be a single identifier
        string candidate = trimmed[..colonIdx].TrimEnd();

        // Must not contain spaces
        if (candidate.Contains(' ') || candidate.Contains('\t')) return null;

        // Must start with letter, underscore, dot, or @
        char first = candidate[0];
        if (!char.IsLetter(first) && first != '_' && first != '.' && first != '@') return null;

        // All chars must be identifier chars
        for (int i = 1; i < candidate.Length; i++)
        {
            if (!IsIdentifierChar(candidate[i])) return null;
        }

        return candidate;
    }

    private static bool IsIdentifierChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '@' || c == '$' || c == '?';
}
