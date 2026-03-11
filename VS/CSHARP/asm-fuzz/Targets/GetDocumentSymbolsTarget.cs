using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.GetDocumentSymbols.
/// Tests: outline/breadcrumb for labels and procedures
/// Edge cases: nested symbols, symbols at end of file, empty document, malformed lines
/// </summary>
public static class GetDocumentSymbolsTarget
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
        string uri = $"file:///fuzz/sym{id}.asm";
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
            var symbolParams = new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
            };

            try
            {
                server.GetDocumentSymbols(symbolParams);
            }
            catch
            {
                // Non-fatal — continue fuzzing
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
