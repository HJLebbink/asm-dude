using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// COMM (commutativity on disjoint resources): the server keys every per-document structure by URI, so
/// operations on one document must not perturb another. This target opens document A, captures its
/// semantic tokens and folding ranges, then opens a SECOND document B on a different URI and re-captures
/// A's outputs — they must be unchanged. This is the only target that exercises more than one open
/// document at once, and it underpins the per-input isolation the whole fuzzer relies on. Closing both
/// documents must return the server to baseline (DUAL).
/// </summary>
public static class MultiDocIsolationTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        // Split the input into two independent document bodies.
        int splitPoint = data.Length / 2;
        string textA = Encoding.UTF8.GetString(data[..splitPoint]);
        string textB = Encoding.UTF8.GetString(data[splitPoint..]);

        using var server = ServerFixture.CreateServer();

        var uriA = new Uri("file:///fuzz/iso-a.asm");
        var uriB = new Uri("file:///fuzz/iso-b.asm");

        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = uriA, LanguageId = "asm", Version = 1, Text = textA },
        });

        var stParamsA = new SemanticTokensParams { TextDocument = new TextDocumentIdentifier { Uri = uriA } };
        var frParamsA = new FoldingRangeParams { TextDocument = new TextDocumentIdentifier { Uri = uriA } };

        int[]? tokensBefore = server.GetSemanticTokens(stParamsA)?.Data;
        FoldingRange[] foldingBefore = server.GetFoldingRanges(frParamsA);

        // Opening a second document on a different URI must not touch document A's state.
        server.OnTextDocumentOpened(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem { Uri = uriB, LanguageId = "asm", Version = 1, Text = textB },
        });

        int[]? tokensAfter = server.GetSemanticTokens(stParamsA)?.Data;
        FoldingRange[] foldingAfter = server.GetFoldingRanges(frParamsA);

        // COMM: A's semantic tokens are unchanged by B's presence.
        Invariants.CheckSemanticTokensEqual(tokensBefore, tokensAfter);

        // COMM: A's folding ranges are unchanged by B's presence.
        if (foldingBefore.Length != foldingAfter.Length)
        {
            throw new InvariantViolation($"opening a second document changed document A's folding-range count ({foldingBefore.Length} -> {foldingAfter.Length})");
        }

        for (int i = 0; i < foldingBefore.Length; i++)
        {
            if (foldingBefore[i].StartLine != foldingAfter[i].StartLine || foldingBefore[i].EndLine != foldingAfter[i].EndLine)
            {
                throw new InvariantViolation($"opening a second document changed document A's folding range {i}");
            }
        }

        server.OnTextDocumentClosed(new DidCloseTextDocumentParams { TextDocument = new TextDocumentIdentifier { Uri = uriA } });
        server.OnTextDocumentClosed(new DidCloseTextDocumentParams { TextDocument = new TextDocumentIdentifier { Uri = uriB } });

        // DUAL: closing both documents restores the server to its pre-open baseline.
        int residual = server.TrackedDocumentEntryCount();
        if (residual != 0)
        {
            throw new InvariantViolation($"close did not restore baseline: {residual} per-document entries retained after closing both documents");
        }
    }
}
