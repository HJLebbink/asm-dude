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

/// <summary>
/// Right-click context menu command that opens the wiki documentation page
/// for the assembly mnemonic under the cursor.
/// </summary>
[VisualStudioContribution]
internal class OpenDocumentationCommand : Command
{
    private const string BaseUrl = "https://github.com/HJLebbink/asm-dude/wiki/";
    private static readonly string DiagLogFile = Path.Combine(Path.GetTempPath(), "AsmDude2-extension-diag.log");

    // guidSHLMainMenu, IDG_VS_CODEWIN_NAVIGATETOLOCATION — the "Go to Definition" group in the code editor context menu
    private static readonly Guid GuidSHLMainMenu = new("D309F791-903F-11D0-9EFC-00A0C911004F");
    private const uint IDG_VS_CODEWIN_NAVIGATETOLOCATION = 0x02B1;

    private Dictionary<string, string>? mnemonicUrlMap;

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
            EnsureMnemonicUrlMap();

            if (!this.mnemonicUrlMap!.TryGetValue(mnemonic, out string? htmlRef) || string.IsNullOrEmpty(htmlRef))
                return;

            string url = BaseUrl + htmlRef;
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

    private void EnsureMnemonicUrlMap()
    {
        if (this.mnemonicUrlMap is not null) return;

        this.mnemonicUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string resourceDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Server", "Resources");

        LoadSignatureFile(Path.Combine(resourceDir, "signature-may2019.txt"));
        LoadSignatureFile(Path.Combine(resourceDir, "signature-hand-1.txt")); // overrides may2019

        Log($"OpenDocumentationCommand: loaded {this.mnemonicUrlMap.Count} mnemonic URL mappings");
    }

    private void LoadSignatureFile(string path)
    {
        if (!File.Exists(path)) return;

        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == ';') continue;

            string[] columns = line.Split('\t');
            if (columns.Length == 4 && columns[3].Length > 0)
            {
                string mnemonic = columns[1].Trim();
                if (mnemonic.Length > 0)
                {
                    this.mnemonicUrlMap![mnemonic] = columns[3].Trim();
                }
            }
        }
    }

    private static void Log(string message)
    {
        try { File.AppendAllText(DiagLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } catch { }
    }
}
