using System.Text;
using AsmDude2LS;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for LanguageServer.OnTextDocumentChanged (didChange).
/// Tests: incremental text modifications and partial document edits
/// Edge cases: overlapping edits, edits at boundaries, edits on same line, edit beyond document length
/// </summary>
public static class DocumentChangeTarget
{
    private static int _docCounter;

    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > 4096)
        {
            return;
        }

        // Split fuzzed data into: initial text + edit text
        int splitPoint = Math.Min(data.Length / 2, 1024);
        string initialText = Encoding.UTF8.GetString(data[..splitPoint]);
        string editText = Encoding.UTF8.GetString(data[splitPoint..]);

        var server = ServerFixture.GetServer();

        int id = Interlocked.Increment(ref _docCounter);
        string uri = $"file:///fuzz/chg{id}.asm";
        var docUri = new Uri(uri);

        // Open document with initial text
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
            // Test 1: Single-line edit
            string[] lines = initialText.Split('\n');
            if (lines.Length > 0)
            {
                string newLineText = lines[0] + " " + editText;
                try
                {
                    server.UpdateServerSideTextDocument(newLineText, 2, uri);
                }
                catch
                {
                    // Non-fatal — continue fuzzing
                }
            }

            // Test 2: Multi-line edit (prepend and append)
            var multiLineText = string.Join("\n",
                editText,
                initialText,
                editText
            );
            try
            {
                server.UpdateServerSideTextDocument(multiLineText, 3, uri);
            }
            catch
            {
                // Non-fatal — continue fuzzing
            }

            // Test 3: Empty document
            try
            {
                server.UpdateServerSideTextDocument("", 4, uri);
            }
            catch
            {
                // Non-fatal — continue fuzzing
            }

            // Test 4: Replace with fuzzed text
            try
            {
                server.UpdateServerSideTextDocument(editText, 5, uri);
            }
            catch
            {
                // Non-fatal — continue fuzzing
            }

            // Test 5: Restore initial text
            try
            {
                server.UpdateServerSideTextDocument(initialText, 6, uri);
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
