using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.SendReferences.
/// Tests: find all label references (label usage tracking)
/// Edge cases: undefined labels, unused labels, clashing labels, self-references
/// </summary>
public static class SendReferencesTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/ref.asm");

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

        string[] serverLines = server.GetDocumentLinesForTest(docUri.ToString());
        string[] lines = text.Split('\n');
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];

            // Test at multiple positions on each line
            for (int charIdx = 0; charIdx < line.Length; charIdx += Math.Max(1, line.Length / 5))
            {
                var referenceParams = new ReferenceParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Position = new Position(lineIdx, charIdx),
                    Context = new ReferenceContext { IncludeDeclaration = true },
                };

                var references = server.SendReferences(referenceParams, returnLocationsOnly: true, CancellationToken.None);

                // CONS: every returned reference location must lie within the document.
                if (references != null)
                {
                    foreach (var item in references)
                    {
                        if (item is Location loc && loc.Range is { } refRange)
                        {
                            Invariants.CheckRangeInDocument(refRange, serverLines, "reference");
                        }
                    }
                }
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
