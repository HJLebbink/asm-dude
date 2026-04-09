// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only and is subject to change or removal in future updates.

/// <summary>
/// CodeLens that shows "N references" above assembly label definitions.
/// The reference count is encoded in the CodeElement.Description field by the tagger
/// as "refcount:N|Label: name".
/// </summary>
internal class AsmLabelCodeLens : InvokableCodeLens
{
    private readonly CodeElement codeElement;
    private readonly VisualStudioExtensibility extensibility;

    public AsmLabelCodeLens(CodeElement codeElement, VisualStudioExtensibility extensibility)
    {
        this.codeElement = codeElement;
        this.extensibility = extensibility;
    }

    public override void Dispose() { }

    public override Task ExecuteAsync(CodeElementContext codeElementContext, IClientContext clientContext, CancellationToken cancelToken)
    {
        // No navigation action for now
        return Task.CompletedTask;
    }

    public override Task<CodeLensLabel> GetLabelAsync(CodeElementContext codeElementContext, CancellationToken token)
    {
        string labelName = this.codeElement.UniqueIdentifier ?? "?";
        int count = 0;

        // The tagger encodes the reference count in the Description as "refcount:N|Label: name"
        string? desc = this.codeElement.Description;
        if (desc != null && desc.StartsWith("refcount:"))
        {
            int pipeIdx = desc.IndexOf('|');
            if (pipeIdx > 0)
                int.TryParse(desc.AsSpan(9, pipeIdx - 9), out count);
        }

        string text = count == 1 ? "1 reference" : $"{count} references";
        return Task.FromResult(new CodeLensLabel
        {
            Text = text,
            Tooltip = $"Label '{labelName}' is referenced {count} time(s) in this file",
        });
    }
}
