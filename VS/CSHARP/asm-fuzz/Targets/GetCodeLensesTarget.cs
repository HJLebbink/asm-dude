using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetCodeLenses.
/// Tests: reference count badges above labels and procedures
/// Edge cases: empty document, document with only comments, large documents, no labels
/// </summary>
public static class GetCodeLensesTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/cl.asm");

        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = docUri,
                LanguageId = "asm",
                Version = 1,
                Text = text,
            },
        };
        server.OnTextDocumentOpened(openParams);

        var codeLensParams = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        var codeLenses = server.GetCodeLenses(codeLensParams);

        // CONS: every code-lens range must lie within the document.
        if (codeLenses != null)
        {
            string[] serverLines = server.GetDocumentLinesForTest(docUri.ToString());
            foreach (var lens in codeLenses)
            {
                Invariants.CheckRangeInDocument(lens.Range, serverLines, "codeLens");
            }
        }

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        server.OnTextDocumentClosed(closeParams);

        // DUAL invariant: no per-document state remains after close.
        int residual = server.TrackedDocumentEntryCount();
        if (residual != 0)
        {
            throw new InvariantViolation($"close did not restore baseline: {residual} per-document entries retained after close");
        }
    }
}
