using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetDocumentHighlights.
/// Tests: highlight all occurrences of symbol/label on current line
/// Edge cases: position at end of line, non-existent label, multiple occurrences
/// </summary>
public static class GetDocumentHighlightsTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        string uri = "file:///fuzz/hl.asm";
        var docUri = new Uri(uri);

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
            for (int charIdx = 0; charIdx <= line.Length; charIdx += Math.Max(1, line.Length / 5))
            {
                var position = new Position(lineIdx, charIdx);
                var progress = new Progress<DocumentHighlight[]>(_ => { });
                var highlights = server.GetDocumentHighlights(progress, position, uri, CancellationToken.None);

                // CONS: every returned highlight range must lie within the document.
                if (highlights != null)
                {
                    foreach (var h in highlights)
                    {
                        Invariants.CheckRangeInDocument(h.Range, serverLines, "documentHighlight");
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
