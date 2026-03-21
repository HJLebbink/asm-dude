// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude2.QuickInfo;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

/// <summary>
/// Adds a clickable documentation link to hover tooltips for assembly mnemonics.
///
/// Ported from VS/CSHARP/old/asm-dude2-vsix-archived/QuickInfo/AsmQuickInfoSource.cs
/// The old code called client.SendHoverRequestAsync() and parsed <![CDATA[<a href=URL>NAME</a>]]>
/// from the response. In the new VisualStudio.Extensibility model, the LSP hover is handled
/// by LanguageServerProvider, so this source only adds the clickable doc link below it.
///
/// Runs in-process via MEF (RequiresInProcessHosting = true) and creates WPF ClassifiedTextRun
/// with a real Action delegate for navigation — something impossible over LSP/JSON-RPC.
/// </summary>
internal sealed class AsmQuickInfoSource(ITextBuffer textBuffer) : IAsyncQuickInfoSource
{
    private const string BaseDocUrl = "https://github.com/HJLebbink/asm-dude/wiki/";
    private bool isDisposed;

    public Task<QuickInfoItem?> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
        if (this.isDisposed)
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        var triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
        if (!triggerPoint.HasValue)
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        var point = triggerPoint.Value;
        var extent = GetWordExtent(point);
        if (extent.Length == 0)
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        string word = point.Snapshot.GetText(extent).Trim();
        if (word.Length == 0)
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        string wordUpper = word.ToUpperInvariant();
        if (wordUpper.Length > 20 || !IsAsmWord(wordUpper))
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        string url = BaseDocUrl + wordUpper;

        // Clickable link — same pattern as old AsmQuickInfoSource line 108:
        //   runs.Add(new ClassifiedTextRun("keyword", linkName, () => Process.Start(capturedUrl)));
        var linkRun = new ClassifiedTextRun(
            "keyword",
            $"Open {wordUpper} documentation",
            () => OpenUrl(url),
            $"Open {url}");

        var textElement = new ClassifiedTextElement(linkRun);
        var container = new ContainerElement(ContainerElementStyle.Stacked, textElement);

        var applicableSpan = point.Snapshot.CreateTrackingSpan(extent, SpanTrackingMode.EdgeInclusive);
        return Task.FromResult<QuickInfoItem?>(new QuickInfoItem(applicableSpan, container));
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private static Span GetWordExtent(SnapshotPoint point)
    {
        var line = point.GetContainingLine();
        string lineText = line.GetText();
        int col = point.Position - line.Start.Position;

        int start = col;
        while (start > 0 && IsWordChar(lineText[start - 1]))
        {
            start--;
        }

        int end = col;
        while (end < lineText.Length && IsWordChar(lineText[end]))
        {
            end++;
        }

        if (start == end)
        {
            return new Span(point.Position, 0);
        }

        return new Span(line.Start.Position + start, end - start);
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '%';
    }

    private static bool IsAsmWord(string word)
    {
        foreach (char c in word)
        {
            if (!char.IsLetter(c) && c != '_')
            {
                return false;
            }
        }
        return true;
    }

    public void Dispose()
    {
        this.isDisposed = true;
    }
}
