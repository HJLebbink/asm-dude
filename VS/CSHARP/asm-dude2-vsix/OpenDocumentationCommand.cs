// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Editor;

[VisualStudioContribution]
internal class OpenDocumentationCommand : Command
{
    private const string BaseUrl = "https://github.com/HJLebbink/asm-dude/wiki/";
    private static readonly string DiagLogFile = Path.Combine(Path.GetTempPath(), "AsmDude2-extension-diag.log");

    private static readonly Guid GuidSHLMainMenu = new("D309F791-903F-11D0-9EFC-00A0C911004F");
    private const uint IDG_VS_CODEWIN_NAVIGATETOLOCATION = 0x02B1;

    private Dictionary<string, string>? mnemonicUrlMap;

    public OpenDocumentationCommand(TraceSource traceSource)
    {
    }

    public override CommandConfiguration CommandConfiguration => new("%AsmDude2.OpenDocumentationCommand.DisplayName%")
    {
        Placements =
        [
            CommandPlacement.VsctParent(GuidSHLMainMenu, IDG_VS_CODEWIN_NAVIGATETOLOCATION, 0x0100),
        ],
        Icon = new(ImageMoniker.KnownValues.QuestionMark, IconSettings.IconAndText),
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        try
        {
            ITextViewSnapshot? textView = await context.GetActiveTextViewAsync(cancellationToken);
            if (textView is null)
            {
                Log("OpenDocumentationCommand: no active text view");
                return;
            }

            TextPosition caretPosition = textView.Selection.InsertionPosition;
            ITextDocumentSnapshotLine line = caretPosition.GetContainingLine();
            string lineText = line.Text.CopyToString();
            int offsetInLine = caretPosition.Offset - line.Text.Start.Offset;

            string? word = GetWordAtPosition(lineText, offsetInLine);
            if (string.IsNullOrEmpty(word))
            {
                Log("OpenDocumentationCommand: no word at cursor");
                return;
            }

            string upperWord = word.ToUpperInvariant();
            if (upperWord.StartsWith('%'))
            {
                upperWord = upperWord[1..]; // AT&T syntax prefix
            }

            EnsureMnemonicUrlMap();
            string htmlRef = this.mnemonicUrlMap!.TryGetValue(upperWord, out string? mapped) && !string.IsNullOrEmpty(mapped)
                ? mapped
                : upperWord; // fall back to mnemonic name as wiki page
            string url = BaseUrl + htmlRef;
            Log($"OpenDocumentationCommand: opening {url} for mnemonic {upperWord}");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log($"OpenDocumentationCommand: error: {ex.Message}");
        }
    }

    private static string? GetWordAtPosition(string lineText, int offset)
    {
        if (offset < 0 || offset > lineText.Length || lineText.Length == 0)
        {
            return null;
        }

        // Clamp to valid character position
        int pos = Math.Min(offset, lineText.Length - 1);

        // If we're at a non-word character, try one position back (cursor is between chars)
        if (!IsWordChar(lineText[pos]) && pos > 0 && IsWordChar(lineText[pos - 1]))
        {
            pos--;
        }

        if (!IsWordChar(lineText[pos]))
        {
            return null;
        }

        int start = pos;
        while (start > 0 && IsWordChar(lineText[start - 1]))
        {
            start--;
        }

        int end = pos;
        while (end < lineText.Length - 1 && IsWordChar(lineText[end + 1]))
        {
            end++;
        }

        return lineText[start..(end + 1)];
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '%';
    }

    private void EnsureMnemonicUrlMap()
    {
        if (this.mnemonicUrlMap is not null)
        {
            return;
        }

        this.mnemonicUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string resourceDir = Path.Combine(extensionDir, "Server", "Resources");

        // Load from both signature files (same format, may2019 has POP/PUSH/RET etc.)
        LoadSignatureFile(Path.Combine(resourceDir, "signature-may2019.txt"));
        LoadSignatureFile(Path.Combine(resourceDir, "signature-hand-1.txt")); // hand-1 overrides may2019

        Log($"OpenDocumentationCommand: loaded {this.mnemonicUrlMap.Count} mnemonic URL mappings");
    }

    private void LoadSignatureFile(string path)
    {
        if (!File.Exists(path))
        {
            Log($"OpenDocumentationCommand: signature file not found: {path}");
            return;
        }

        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            string[] columns = line.Split('\t');
            if (columns.Length == 4 && !string.IsNullOrEmpty(columns[3]))
            {
                // Format: GENERAL\tMNEMONIC\tDescription\tHtmlRef
                string mnemonic = columns[1].Trim();
                string htmlRef = columns[3].Trim();
                if (mnemonic.Length > 0)
                {
                    this.mnemonicUrlMap![mnemonic] = htmlRef;
                }
            }
        }
    }

    private static void Log(string message)
    {
        try { File.AppendAllText(DiagLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } catch { }
    }
}
