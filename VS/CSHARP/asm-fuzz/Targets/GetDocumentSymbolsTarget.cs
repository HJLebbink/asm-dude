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
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/sym.asm");

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

        var symbolParams = new DocumentSymbolParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        var symbols = server.GetDocumentSymbols(symbolParams);

        // CONS: every document symbol's location must lie within the document.
        if (symbols != null)
        {
            string[] serverLines = server.GetDocumentLinesForTest(docUri.ToString());
            foreach (var sym in symbols)
            {
                if (sym.Location?.Range is { } symRange)
                {
                    Invariants.CheckRangeInDocument(symRange, serverLines, "documentSymbol");
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
