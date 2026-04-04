// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

[VisualStudioContribution]
internal class AsmCodeLensProvider : ExtensionPart, ICodeLensProvider
{
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo =
        [
            DocumentFilter.FromDocumentType(AsmLanguageServerProvider.AsmDocumentType),
            DocumentFilter.FromDocumentType(AsmLanguageServerProvider.CodDocumentType),
            DocumentFilter.FromDocumentType(AsmLanguageServerProvider.IncDocumentType),
            DocumentFilter.FromDocumentType(AsmLanguageServerProvider.SDocumentType),
        ],
    };

    public CodeLensProviderConfiguration CodeLensProviderConfiguration =>
        new("AsmDude2 Label References") { Priority = 500 };

    public Task<CodeLens?> TryCreateCodeLensAsync(CodeElement codeElement, CodeElementContext codeElementContext, CancellationToken token)
    {
        if (codeElement.Kind == AsmCodeLensTagger.AsmLabelKind)
        {
            return Task.FromResult<CodeLens?>(new AsmLabelCodeLens(codeElement));
        }

        return Task.FromResult<CodeLens?>(null);
    }
}
