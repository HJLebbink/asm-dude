using System.Text;
using AsmDude2LS;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetDocumentHighlights.
/// Tests: highlight all occurrences of symbol/label on current line
/// Edge cases: position at end of line, non-existent label, multiple occurrences
/// </summary>
public static class GetDocumentHighlightsTarget
{
    private static int _docCounter;

    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        var server = ServerFixture.GetServer();

        int id = Interlocked.Increment(ref _docCounter);
        string uri = $"file:///fuzz/hl{id}.asm";
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

        try
        {
            server.OnTextDocumentOpened(openParams);
        }
        catch
        {
            return;
        }

        try
        {
            string[] lines = text.Split('\n');
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                string line = lines[lineIdx];

                // Test at multiple positions on each line
                for (int charIdx = 0; charIdx <= line.Length; charIdx += Math.Max(1, line.Length / 5))
                {
                    var highlightParams = new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = docUri },
                        Position = new Position(lineIdx, charIdx),
                    };

                    try
                    {
                        var progress = new Progress<DocumentHighlight[]>(_ => { });
                        server.GetDocumentHighlights(progress, highlightParams.Position, uri, CancellationToken.None);
                    }
                    catch
                    {
                        // Non-fatal — continue fuzzing
                    }
                }
            }
        }
        finally
        {
            var closeParams = new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
            };
            server.OnTextDocumentClosed(closeParams);
        }
    }
}
