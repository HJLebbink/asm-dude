// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only
#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

[VisualStudioContribution]
internal class AsmCodeLensTaggerProvider : ExtensionPart, ITextViewTaggerProvider<CodeLensTag>
{
    private readonly object lockObject = new();
    private readonly Dictionary<Uri, List<AsmCodeLensTagger>> taggers = new();

    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo =
        [
            DocumentFilter.FromDocumentType(DocumentType.KnownValues.Code),
        ],
    };

    public Task<TextViewTaggerBase<CodeLensTag>> CreateTaggerAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        var tagger = new AsmCodeLensTagger(this, textView.Document.Uri);
        lock (this.lockObject)
        {
            if (!this.taggers.TryGetValue(textView.Document.Uri, out var list))
            {
                list = new();
                this.taggers[textView.Document.Uri] = list;
            }

            list.Add(tagger);
        }

        return Task.FromResult<TextViewTaggerBase<CodeLensTag>>(tagger);
    }

    internal void RemoveTagger(Uri documentUri, AsmCodeLensTagger toBeRemoved)
    {
        lock (this.lockObject)
        {
            if (this.taggers.TryGetValue(documentUri, out var list))
            {
                list.Remove(toBeRemoved);
                if (list.Count == 0)
                {
                    this.taggers.Remove(documentUri);
                }
            }
        }
    }
}
