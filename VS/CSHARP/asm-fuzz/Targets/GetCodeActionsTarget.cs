using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetCodeActions.
/// Tests: quick fixes (code actions) at various positions
/// Edge cases: no actions available, position at comment, position at directive
/// </summary>
public static class GetCodeActionsTarget
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
        string uri = $"file:///fuzz/ca{id}.asm";
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
                    var codeActionParams = new CodeActionParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = docUri },
                        Range = new Microsoft.VisualStudio.LanguageServer.Protocol.Range
                        {
                            Start = new Position(lineIdx, charIdx),
                            End = new Position(lineIdx, Math.Min(charIdx + 1, line.Length)),
                        },
                        Context = new CodeActionContext
                        {
                            Diagnostics = [],
                        },
                    };

                    try
                    {
                        server.GetCodeActions(codeActionParams);
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
