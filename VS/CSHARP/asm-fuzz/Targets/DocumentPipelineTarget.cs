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
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string text = Encoding.UTF8.GetString(data);
        using var server = ServerFixture.CreateServer();

        var docUri = new Uri("file:///fuzz/doc.asm");

        // Open the document — triggers UpdateInternals (ParseLine, UpdateFoldingRanges, UpdateLabelGraph).
        // This is the highest-value path: it must never throw on arbitrary bytes, so it is NOT guarded.
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

        // The server's own line model — the frame of reference for all position-based outputs below
        // (so the CONS/MONO checks don't false-positive on line-split differences).
        string[] serverLines = server.GetDocumentLinesForTest(docUri.ToString());

        // CONS+MONO: the delta-encoded semantic-token array must be well-formed (multiple of 5,
        // in-document, sorted, non-overlapping) — silent corruption the crash-oracle can't see.
        var semanticParams = new SemanticTokensParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        var semanticTokens = server.GetSemanticTokens(semanticParams);
        if (semanticTokens?.Data is { } tokenData)
        {
            Invariants.CheckSemanticTokens(tokenData, serverLines);
        }

        // CONS: structural outputs produced on open must point within the document.
        Invariants.CheckDiagnostics(server.DiagnosticsForTest(), serverLines);
        var foldingParams = new FoldingRangeParams { TextDocument = new TextDocumentIdentifier { Uri = docUri } };
        Invariants.CheckFoldingRanges(server.GetFoldingRanges(foldingParams), serverLines);

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
            var hover = server.GetHover(hoverParams);
            // CONS: a hover that carries a range must point within the document.
            if (hover is Hover { Range: { } hoverRange })
            {
                Invariants.CheckRangeInDocument(hoverRange, serverLines, "hover");
            }

            // Completion at midpoint
            var completionParams = new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
                Position = new Position(i, midPos),
            };
            FuzzGuard.Guard(() => server.GetTextDocumentCompletion(completionParams));

            // Signature help after first comma (if any)
            int commaIdx = line.IndexOf(',');
            if (commaIdx >= 0)
            {
                var sigParams = new SignatureHelpParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = docUri },
                    Position = new Position(i, commaIdx + 1),
                };
                FuzzGuard.Guard(() => server.GetTextDocumentSignatureHelp(sigParams));
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
            var inlayHints = server.GetInlayHints(inlayParams);
            // CONS: every inlay hint anchors at a position within the document.
            if (inlayHints != null)
            {
                foreach (var hint in inlayHints)
                {
                    Invariants.CheckPosition(hint.Position, serverLines, "inlayHint");
                }
            }
        }

        // IDEM / determinism: a second fresh server opening identical text must yield the identical
        // semantic-token stream (catches dependence on hidden global/static state).
        using (var server2 = ServerFixture.CreateServer())
        {
            server2.OnTextDocumentOpened(openParams);
            var st2 = server2.GetSemanticTokens(semanticParams);
            Invariants.CheckSemanticTokensEqual(semanticTokens?.Data, st2?.Data);
            server2.OnTextDocumentClosed(new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = docUri },
            });
        }

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        server.OnTextDocumentClosed(closeParams);

        // DUAL invariant: close is the inverse of open. After closing the only document, the server
        // must hold no per-document state (catches leaks like folding-ranges/assembler-type residue).
        int residual = server.TrackedDocumentEntryCount();
        if (residual != 0)
        {
            throw new InvariantViolation($"close did not restore baseline: {residual} per-document entries retained after close");
        }
    }
}
