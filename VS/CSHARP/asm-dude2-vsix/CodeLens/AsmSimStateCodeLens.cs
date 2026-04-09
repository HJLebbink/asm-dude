// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

#pragma warning disable VSEXTPREVIEW_CODELENS // Type is for evaluation purposes only

/// <summary>
/// CodeLens that shows a sim-state label above an assembly instruction line.
/// The label (e.g. "→RAX=0x10, ←RBX=0x4") is encoded in the CodeElement.Description
/// field by the tagger as "simstate:|{label}".
///
/// Uses <see cref="InvokableCodeLens"/> (not the non-invokable base) because VS
/// only renders invokable CodeLens items; clicking is a no-op.
/// </summary>
internal class AsmSimStateCodeLens : InvokableCodeLens
{
    private readonly CodeElement codeElement;

    public AsmSimStateCodeLens(CodeElement codeElement)
    {
        this.codeElement = codeElement;
    }

    public override void Dispose() { }

    /// <summary>Clicking does nothing — this is a display-only CodeLens.</summary>
    public override Task ExecuteAsync(CodeElementContext codeElementContext, IClientContext clientContext, CancellationToken cancelToken)
        => Task.CompletedTask;

    public override Task<CodeLensLabel> GetLabelAsync(CodeElementContext codeElementContext, CancellationToken token)
    {
        string label = string.Empty;
        string? desc = this.codeElement.Description;
        if (desc != null && desc.StartsWith("simstate:|"))
            label = desc["simstate:|".Length..];

        return Task.FromResult(new CodeLensLabel
        {
            Text = label,
            Tooltip = "Assembly simulator register/flag state (Z3-proven)\n" +
                      "rw: = read and written  r: = read only  w: = written only",
        });
    }
}
