// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Right-click context menu command that opens the wiki documentation page
/// for the assembly mnemonic under the cursor.
///
/// The mnemonic-to-URL mapping (and the configurable documentation base URL) lives on the LSP
/// server: this command fetches the full URL over the SimState pipe via
/// <see cref="SimStatePipeClient.GetMnemonicUrlAsync"/>. It does NOT read the signature files
/// itself — the server's MnemonicStore is the single source of that data.
/// </summary>
[VisualStudioContribution]
internal class OpenDocumentationCommand : Command
{

    // guidSHLMainMenu, IDG_VS_CODEWIN_NAVIGATETOLOCATION — the "Go to Definition" group in the code editor context menu
    private static readonly Guid GuidSHLMainMenu = new("D309F791-903F-11D0-9EFC-00A0C911004F");
    private const uint IDG_VS_CODEWIN_NAVIGATETOLOCATION = 0x02B1;

    public OpenDocumentationCommand(TraceSource traceSource) { }

    public override CommandConfiguration CommandConfiguration => new("%AsmDude2.OpenDocumentationCommand.DisplayName%")
    {
        Placements = [CommandPlacement.VsctParent(GuidSHLMainMenu, IDG_VS_CODEWIN_NAVIGATETOLOCATION, 0x0100)],
        Icon = new(ImageMoniker.KnownValues.QuestionMark, IconSettings.IconAndText),
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            ITextViewSnapshot? textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null) return;

            string lineText = textView.Selection.InsertionPosition.GetContainingLine().Text.CopyToString();
            int offsetInLine = textView.Selection.InsertionPosition.Offset
                - textView.Selection.InsertionPosition.GetContainingLine().Text.Start.Offset;

            string? word = GetWordAtPosition(lineText, offsetInLine);
            if (string.IsNullOrEmpty(word)) return;

            string mnemonic = word.TrimStart('%').ToUpperInvariant();

            // Ask the server for the documentation URL (it owns the signature data and the
            // configurable AsmDoc_Url). Returns null for non-mnemonics or undocumented mnemonics.
            string? url = await SimStatePipeClient.Instance.GetMnemonicUrlAsync(mnemonic, cancellationToken);
            if (string.IsNullOrEmpty(url)) return;

            Log($"OpenDocumentationCommand: opening {url}");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log($"OpenDocumentationCommand: {ex.Message}");
        }
    }

    private static string? GetWordAtPosition(string line, int offset)
    {
        if (line.Length == 0 || offset < 0 || offset > line.Length) return null;

        int pos = Math.Min(offset, line.Length - 1);
        if (!IsWordChar(line[pos]) && pos > 0 && IsWordChar(line[pos - 1])) pos--;
        if (!IsWordChar(line[pos])) return null;

        int start = pos;
        while (start > 0 && IsWordChar(line[start - 1])) start--;

        int end = pos;
        while (end < line.Length - 1 && IsWordChar(line[end + 1])) end++;

        return line[start..(end + 1)];
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '%';

    private static void Log(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => AsmTools.AsmLog.Log(AsmTools.AsmLogLevel.Debug, "Command", message, member, line);
}
