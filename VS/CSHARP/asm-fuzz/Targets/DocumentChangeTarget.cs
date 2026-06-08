using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Text;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.OnTextDocumentChanged (didChange).
/// Tests: incremental text modifications and partial document edits
/// Edge cases: overlapping edits, edits at boundaries, edits on same line, edit beyond document length
/// </summary>
public static class DocumentChangeTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        // Split fuzzed data into: initial text + edit text
        int splitPoint = Math.Min(data.Length / 2, 1024);
        string initialText = Encoding.UTF8.GetString(data[..splitPoint]);
        string editText = Encoding.UTF8.GetString(data[splitPoint..]);

        using var server = ServerFixture.CreateServer();

        string uri = "file:///fuzz/chg.asm";
        var docUri = new Uri(uri);

        // Open document with initial text. The parse pipeline must never throw — NOT guarded.
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = docUri,
                LanguageId = "asm",
                Version = 1,
                Text = initialText,
            },
        };
        server.OnTextDocumentOpened(openParams);

        // BND: opening one document populates the per-document dictionaries; subsequent edits to the SAME
        // uri must not grow that footprint (each dict stays keyed by the one uri). Captured here as the
        // ceiling the edit sequence below may never exceed.
        int baselineEntries = server.TrackedDocumentEntryCount();

        // Edits must also never throw on arbitrary bytes; FuzzGuard rethrows anything but cancellation.

        // Test 1: Single-line edit
        string[] lines = initialText.Split('\n');
        if (lines.Length > 0)
        {
            string newLineText = lines[0] + " " + editText;
            FuzzGuard.Guard(() => server.UpdateServerSideTextDocument(newLineText, 2, uri));
        }

        // Test 2: Multi-line edit (prepend and append)
        var multiLineText = string.Join("\n", editText, initialText, editText);
        FuzzGuard.Guard(() => server.UpdateServerSideTextDocument(multiLineText, 3, uri));

        // Test 3: Empty document
        FuzzGuard.Guard(() => server.UpdateServerSideTextDocument("", 4, uri));

        // Test 4: Replace with fuzzed text
        FuzzGuard.Guard(() => server.UpdateServerSideTextDocument(editText, 5, uri));

        // Test 5: Restore initial text
        FuzzGuard.Guard(() => server.UpdateServerSideTextDocument(initialText, 6, uri));

        // BND: the edit sequence above must not have grown the tracked per-document footprint.
        int afterEdits = server.TrackedDocumentEntryCount();
        if (afterEdits > baselineEntries)
        {
            throw new InvariantViolation($"editing one document grew tracked entries from {baselineEntries} to {afterEdits}");
        }

        // RES (replay equivalence): drive the document to a definite final content via the SYNCHRONOUS
        // edit seam (the production edit path is debounced 100 ms, so it isn't deterministic within one
        // iteration), then assert a fresh server that simply OPENED that content observes identical
        // outputs. Catches stale residue from earlier content surviving an edit. The two paths
        // (UpdateInternals-after-edit vs UpdateInternals-after-open) are distinct code, so this is not
        // tautological.
        server.ApplyEditForTest(multiLineText, 7, uri);
        using (var fresh = ServerFixture.CreateServer())
        {
            fresh.OnTextDocumentOpened(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem { Uri = docUri, LanguageId = "asm", Version = 1, Text = multiLineText },
            });
            Invariants.CheckServerStateEquivalent(server, fresh, docUri);
            fresh.OnTextDocumentClosed(new DidCloseTextDocumentParams { TextDocument = new TextDocumentIdentifier { Uri = docUri } });
        }

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = docUri },
        };
        server.OnTextDocumentClosed(closeParams);

        // DUAL invariant: after open + edits + close, no per-document state should remain.
        int residual = server.TrackedDocumentEntryCount();
        if (residual != 0)
        {
            throw new InvariantViolation($"close did not restore baseline: {residual} per-document entries retained after close");
        }
    }
}
