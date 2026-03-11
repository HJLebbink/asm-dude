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
        string uri = $"file:///fuzz/ref{id}.asm";
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
                for (int charIdx = 0; charIdx < line.Length; charIdx += Math.Max(1, line.Length / 5))
                {
                    var referenceParams = new ReferenceParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = docUri },
                        Position = new Position(lineIdx, charIdx),
                        Context = new ReferenceContext { IncludeDeclaration = true },
                    };

                    try
                    {
                        server.SendReferences(referenceParams, returnLocationsOnly: true, CancellationToken.None);
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
