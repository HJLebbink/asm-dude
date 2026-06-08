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
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/ca.asm");

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

                FuzzGuard.Guard(() => server.GetCodeActions(codeActionParams));
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
