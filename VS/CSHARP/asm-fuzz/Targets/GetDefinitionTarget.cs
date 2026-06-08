using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetDefinition.
/// Tests: jump to label definition at various positions
/// Edge cases: undefined labels, multiple definitions, position boundaries
/// </summary>
public static class GetDefinitionTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/def.asm");

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
                var definitionParams = new TextDocumentPositionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Position = new Position(lineIdx, charIdx),
                };

                var definition = server.GetDefinition(definitionParams);

                // CONS: the definition location must point within the document.
                if (definition?.Range is { } defRange)
                {
                    Invariants.CheckRangeInDocument(defRange, serverLines, "definition");
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
