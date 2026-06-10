// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

using System.Threading;
using System.Threading.Tasks;

#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

[VisualStudioContribution]
internal class AsmCodeLensProvider : ExtensionPart, ICodeLensProvider
{
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo =
        [
            DocumentFilter.FromDocumentType(DocumentType.KnownValues.Code),
        ],
    };

    public CodeLensProviderConfiguration CodeLensProviderConfiguration =>
        new("AsmDude2 Label References") { Priority = 500 };

    public Task<CodeLens?> TryCreateCodeLensAsync(CodeElement codeElement, CodeElementContext codeElementContext, CancellationToken token)
    {
        // [Conditional("DEBUG")] — fires whenever VS materializes a lens; a burst here on scroll is
        // the tell-tale of VS re-creating lenses over the OOP boundary. Off in Release.
        AsmCodeLensTagger.TaggerLogVerbose($"[provider] TryCreateCodeLensAsync: kind={codeElement.Kind}, id={codeElement.UniqueIdentifier}");

        if (codeElement.Kind == AsmCodeLensTagger.AsmLabelKind)
            return Task.FromResult<CodeLens?>(new AsmLabelCodeLens(codeElement, this.Extensibility));

        if (codeElement.Kind == AsmCodeLensTagger.AsmSimStateKind)
            return Task.FromResult<CodeLens?>(new AsmSimStateCodeLens(codeElement));

        return Task.FromResult<CodeLens?>(null);
    }
}
