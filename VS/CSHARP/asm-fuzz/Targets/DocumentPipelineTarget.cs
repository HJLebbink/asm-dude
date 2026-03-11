using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target exercising the full LS document processing pipeline:
/// OnTextDocumentOpened → UpdateInternals → ParseLine → UpdateFoldingRanges → UpdateLabelGraph,
/// then GetHover, GetSemanticTokens, GetTextDocumentCompletion, GetTextDocumentSignatureHelp,
/// and GetInlayHints on each line.
/// </summary>
public static class DocumentPipelineTarget
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

        // Use a unique URI per invocation to avoid collisions in the server's dictionaries
        int id = Interlocked.Increment(ref _docCounter);
        string uri = $"file:///fuzz/doc{id}.asm";
        var docUri = new Uri(uri);

        // Open the document — triggers UpdateInternals (ParseLine, UpdateFoldingRanges, UpdateLabelGraph)
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
            // Semantic tokens for the full document
            var semanticParams = new SemanticTokensParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
            };
            server.GetSemanticTokens(semanticParams);

            // Per-line requests
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int midPos = line.Length / 2;

                // Hover at midpoint
                var hoverParams = new TextDocumentPositionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Position = new Position(i, midPos),
                };
                server.GetHover(hoverParams);

                // Completion at midpoint
                var completionParams = new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Position = new Position(i, midPos),
                };
                server.GetTextDocumentCompletion(completionParams);

                // Signature help after first comma (if any)
                int commaIdx = line.IndexOf(',');
                if (commaIdx >= 0)
                {
                    var sigParams = new SignatureHelpParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = docUri },
                        Position = new Position(i, commaIdx + 1),
                    };
                    server.GetTextDocumentSignatureHelp(sigParams);
                }

                // Inlay hints for this line
                var inlayParams = new InlayHintParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Range = new Microsoft.VisualStudio.LanguageServer.Protocol.Range
                    {
                        Start = new Position(i, 0),
                        End = new Position(i, line.Length),
                    },
                };
                server.GetInlayHints(inlayParams);
            }
        }
        finally
        {
            // Close the document to free server state
            var closeParams = new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
            };
            server.OnTextDocumentClosed(closeParams);
        }
    }
}
